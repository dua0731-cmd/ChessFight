using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // B, laying the plank bridge: a 10 m chasm across the station (u 6..16) and ten 2.5 x 2 m planks
    // to lay in two columns of five rows (v -2.5..2.5), nearest row first. Stand at a plank pile
    // (u 3, v +-8, five each) for 1 s to pick one up; stand at the end of the bridge so far for
    // 1.5 s to lay it. A carrier that is knocked over, falls or respawns drops it, and the plank is
    // back on its pile 5 s later. At most four of a team carry at once. Laid planks stay.
    //
    // Carried planks are pictures over the carrier's head, laid ones fixed boxes: no physics props
    // (mini-game spec: at most ten moving props per game, and twenty planks a plaza would be more).
    public sealed class MgPlanks : MiniGameBase
    {
        public const int Rows = 5, Columns = 2, PerPile = 5;
        public const float PickSeconds = 1f, LaySeconds = 1.5f, ReturnSeconds = 5f;
        public const float Chasm0 = 6f, Row = 2f, PlankWidth = 2.5f;
        static readonly float[] PileX = { -8f, 8f };
        const float PileZ = 3f;

        [SerializeField] MissionStation station;
        [Tooltip("Row by row, west column first: hidden until laid.")]
        [SerializeField] GameObject[] slots = new GameObject[Rows * Columns];
        [SerializeField] GameObject[] pileBoards = new GameObject[2 * PerPile];

        sealed class Carry
        {
            public GameObject Board;
            public int Pile;
            public Vector3 Velocity;
        }

        readonly bool[] laid = new bool[Rows * Columns];
        readonly int[] stock = { PerPile, PerPile };
        readonly Dictionary<ICharacterDriver, Carry> carriers = new Dictionary<ICharacterDriver, Carry>();
        readonly Dictionary<ICharacterDriver, float> picking = new Dictionary<ICharacterDriver, float>();
        readonly Dictionary<ICharacterDriver, float> laying = new Dictionary<ICharacterDriver, float>();
        readonly List<float> returnAt = new List<float>();
        readonly List<int> returnPile = new List<int>();
        readonly HashSet<ICharacterDriver> present = new HashSet<ICharacterDriver>();
        readonly List<ICharacterDriver> dropped = new List<ICharacterDriver>();
        int count;

        public int Laid => count;

        public static MgPlanks Build(CourseBuilder b, MissionStation station)
        {
            var k = b.kit;
            MissionGames.Floor(b, 0f, Chasm0);
            MissionGames.Floor(b, Chasm0 + Rows * Row, 20f);
            var slots = new GameObject[Rows * Columns];
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                {
                    float x0 = c == 0 ? -PlankWidth : 0f, z0 = Chasm0 + r * Row;
                    // Where a plank goes: a gold outline over the chasm (no collider).
                    b.Box("Plank place", MissionGames.V(x0 + .05f, -.05f, z0 + .05f), MissionGames.V(x0 + PlankWidth - .05f, -.02f, z0 + Row - .05f),
                          k.rankGold, false, false);
                    var plank = b.Box("Plank (laid)", MissionGames.V(x0, -.3f, z0), MissionGames.V(x0 + PlankWidth, 0f, z0 + Row), k.boardDark);
                    plank.AddComponent<CourseFloor>();
                    plank.SetActive(false);
                    slots[r * Columns + c] = plank;
                }
            var boards = new GameObject[2 * PerPile];
            for (int p = 0; p < 2; p++)
                for (int i = 0; i < PerPile; i++)
                    boards[p * PerPile + i] = b.Box("Pile plank", MissionGames.V(PileX[p] - 1.25f, i * .14f, PileZ - 1f),
                                                    MissionGames.V(PileX[p] + 1.25f, i * .14f + .12f, PileZ + 1f), k.boardDark, false, false);
            var game = b.parent.gameObject.AddComponent<MgPlanks>();
            game.station = station;
            game.slots = slots;
            game.pileBoards = boards;
            return game;
        }

        int FrontRow()
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                    if (!laid[r * Columns + c]) return r;
            return Rows;
        }

        void FixedUpdate()
        {
            if (Completed || station == null) return;
            float dt = Time.fixedDeltaTime;
            for (int i = returnAt.Count - 1; i >= 0; i--)
            {
                if (Time.time < returnAt[i]) continue;
                stock[returnPile[i]]++;
                returnAt.RemoveAt(i);
                returnPile.RemoveAt(i);
                ShowPiles();
            }

            var pawns = station.Pawns();
            present.Clear();
            foreach (var p in pawns) present.Add(p.Driver);
            // Carriers that fell, left, respawned or were knocked over drop their plank.
            dropped.Clear();
            foreach (var kv in carriers)
            {
                bool here = false;
                foreach (var p in pawns)
                {
                    if (p.Driver != kv.Key) continue;
                    here = p.Local.y > -1.5f;
                    if ((p.Velocity - kv.Value.Velocity).magnitude > 7f) here = false;   // knocked over
                    kv.Value.Velocity = p.Velocity;
                }
                if (!here || (kv.Key is Object o && o == null)) dropped.Add(kv.Key);
            }
            foreach (var d in dropped) Drop(d);

            int front = FrontRow();
            foreach (var p in pawns)
            {
                if (!p.Own) continue;
                if (carriers.TryGetValue(p.Driver, out var carry))
                {
                    // At the end of the bridge so far, in the bridge's width.
                    float edge = Chasm0 + front * Row;
                    bool atEdge = front < Rows && p.Local.z > edge - 1.6f && p.Local.z < edge + .4f && Mathf.Abs(p.Local.x) <= PlankWidth + .7f
                                  && p.Local.y > -.5f && p.Local.y < 2f;
                    float t = atEdge ? Tick(laying, p.Driver, dt) : Clear(laying, p.Driver);
                    if (t >= LaySeconds) Lay(p.Driver, carry, front, p.Local.x);
                }
                else if (carriers.Count < PawnRushMissions.WorkerCap)
                {
                    int pile = -1;
                    for (int i = 0; i < 2; i++)
                        if (stock[i] > 0 && new Vector2(p.Local.x - PileX[i], p.Local.z - PileZ).magnitude < 1.9f && p.Local.y < 2f) pile = i;
                    float t = pile >= 0 ? Tick(picking, p.Driver, dt) : Clear(picking, p.Driver);
                    if (pile >= 0 && t >= PickSeconds) Pick(p.Driver, pile, p.Velocity);
                }
            }
            Progress01 = count / (float)(Rows * Columns);
            if (count >= Rows * Columns) Complete();
        }

        static float Tick(Dictionary<ICharacterDriver, float> timers, ICharacterDriver who, float dt)
        {
            timers.TryGetValue(who, out float t);
            timers[who] = t + dt;
            return t + dt;
        }

        static float Clear(Dictionary<ICharacterDriver, float> timers, ICharacterDriver who)
        {
            timers.Remove(who);
            return 0f;
        }

        void Pick(ICharacterDriver who, int pile, Vector3 velocity)
        {
            stock[pile]--;
            ShowPiles();
            picking.Remove(who);
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Plank (carried)";
            Destroy(board.GetComponent<Collider>());
            board.transform.SetParent(transform, false);
            board.transform.localScale = new Vector3(PlankWidth, .12f, Row);
            board.GetComponent<Renderer>().sharedMaterial = station.Kit.boardDark;
            carriers[who] = new Carry { Board = board, Pile = pile, Velocity = velocity };
        }

        void Lay(ICharacterDriver who, Carry carry, int row, float x)
        {
            int c = laid[row * Columns] ? 1 : laid[row * Columns + 1] ? 0 : x < 0f ? 0 : 1;
            int slot = row * Columns + c;
            laid[slot] = true;
            count++;
            if (slots[slot] != null) slots[slot].SetActive(true);
            Destroy(carry.Board);
            carriers.Remove(who);
            laying.Remove(who);
        }

        void Drop(ICharacterDriver who)
        {
            if (!carriers.TryGetValue(who, out var carry)) return;
            if (carry.Board != null) Destroy(carry.Board);
            carriers.Remove(who);
            laying.Remove(who);
            returnAt.Add(Time.time + ReturnSeconds);
            returnPile.Add(carry.Pile);
        }

        void ShowPiles()
        {
            for (int p = 0; p < 2; p++)
                for (int i = 0; i < PerPile; i++)
                    if (pileBoards[p * PerPile + i] != null) pileBoards[p * PerPile + i].SetActive(i < stock[p]);
        }

        // The carried planks ride over their carriers' heads.
        void LateUpdate()
        {
            foreach (var kv in carriers)
            {
                if (kv.Value.Board == null || kv.Key is Object o && o == null || kv.Key.FollowTarget == null) continue;
                kv.Value.Board.transform.position = kv.Key.FollowTarget.position + Vector3.up * 1.1f;
                kv.Value.Board.transform.rotation = transform.rotation;
            }
        }

        public override void ResetGame()
        {
            base.ResetGame();
            foreach (var kv in carriers) if (kv.Value.Board != null) Destroy(kv.Value.Board);
            carriers.Clear();
            picking.Clear();
            laying.Clear();
            returnAt.Clear();
            returnPile.Clear();
            count = 0;
            stock[0] = stock[1] = PerPile;
            for (int i = 0; i < laid.Length; i++)
            {
                laid[i] = false;
                if (slots[i] != null) slots[i].SetActive(false);
            }
            ShowPiles();
        }
    }
}
