using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // B, laying the plank bridge: a 10 m chasm across the station (u 6..16) and ten 2.5 x 2 m slots over it in two
    // columns of five rows (v -2.5..2.5), and the team's own ten planks on two piles (u 3.5, v +-8, five each). The
    // numbers and the rules are Core's PlankBridge; this plays them with the pawns.
    //
    // R528 (user 11-09 list 2: "나무판자는 잡기로 잡아 옮길 수 있게, 상대방 것도 가능, 일정 미니게임 지역 이탈 시 자동으로 원래 자리로
    // 복구, 나무 판자 색 구분 가능하도록 양 팀이 다르게, 잡아서 판자 설치해야하는 자리에 놓으면 자동 건설되도록. 판정은 적당히"):
    //   - take: the grab button down and a hand within PickReach of a pile or of a plank lying on the floor (the hands of
    //     a standing pawn do not reach a floor plank, so the game reaches for them). Anyone may, the other team too.
    //   - carry: the plank rides in front of the pawn, its near edge over the hands that reach forward while the button
    //     is down (CarryAhead, CarryUp); the hands grip and climb nothing meanwhile (MiniGameHooks.HandsFull).
    //   - lay: put down (button up) or held for HoverSeconds with its centre near a free slot (SnapAcross / SnapAlong):
    //     the slot is laid. Put down anywhere else on a floor it lies there, flat and square to the room, for anyone to
    //     take again; over the chasm it falls and goes back on its own pile, as it does when it leaves the station
    //     (carried out, or with a carrier who fell, respawned or left). Knocked over or diving, the carrier puts it down.
    //   - the teams' planks look different: white's pale maple, black's dark ebony, both banded at the ends in the team
    //     colour.
    //
    // No physics props (mini-game spec v1.0 §1.2): a plank is kinematic, moved by the host's numbers; a carried one is
    // a picture with no collider (nothing to shove pawns with), a lying one a 0.12 m board to walk over.
    //
    // Online: the block is a PlankBoard (every plank's state, the carrier's seat or where it lies, the laid slots,
    // 26 bytes at most). Every PC draws a carried plank in front of the carrier it draws.
    public sealed class MgPlanks : MiniGameBase, IProgressCount
    {
        public const int Rows = PlankBridge.Rows, Columns = PlankBridge.Columns, PerPile = PlankBridge.PerPile;
        public const float Chasm0 = PlankBridge.Chasm0, Row = PlankBridge.Row, PlankWidth = PlankBridge.PlankWidth;
        // The carried plank's centre in front of the hips (its near edge 0.2 m ahead, just past the belly) and above them
        // (its underside on the hands, which reach forward 0.4 .. 0.48 m up while the grab button is down; hips 0.25).
        public const float CarryAhead = 1.2f, CarryUp = .28f;
        const float EaseSeconds = .25f, PileHeight = PerPile * PlankBridge.PileStep;

        [SerializeField] MissionStation station;
        [Tooltip("Row by row, west column first: hidden until laid.")]
        [SerializeField] GameObject[] slots = new GameObject[PlankBridge.Slots];
        [SerializeField] GameObject[] pileBoards = new GameObject[PlankBridge.Planks];
        [Tooltip("Each pile's solid stack (held, never climbed), as high as the planks on it.")]
        [SerializeField] BoxCollider[] pileBlocks = new BoxCollider[PlankBridge.Piles];

        // A plank of the team: one body (kinematic, held only), its collider on only while it lies on a floor, and its
        // picture, which eases into place after every change.
        sealed class Plank
        {
            public GameObject Root;
            public BoxCollider Collider;
            public Rigidbody Body;
            public Transform Look;
            public Vector3 FromPosition;
            public Quaternion FromRotation;
            public float Eased = 1f;
            public PlankState Shown = PlankState.Pile;
        }

        readonly PlankBoard board = new PlankBoard();
        readonly PlankBoard incoming = new PlankBoard();
        // Host: who carries each plank. Every PC: how the carried plank faces (degrees in the game's frame, eased) and
        // how long it has hovered over a free slot.
        readonly ICharacterDriver[] carrier = new ICharacterDriver[PlankBridge.Planks];
        readonly float[] yaw = new float[PlankBridge.Planks];
        readonly float[] hover = new float[PlankBridge.Planks];
        readonly Vector3[] lastHips = new Vector3[PlankBridge.Planks];
        // Pawns whose plank was sent home under them (carried out of the station, a fall, a respawn): they take nothing
        // until they let go of the grab button, or the next step would take one off the pile beside them and send it
        // home again, every step.
        readonly HashSet<ICharacterDriver> letGoFirst = new HashSet<ICharacterDriver>();
        readonly List<ICharacterDriver> letGo = new List<ICharacterDriver>();
        Plank[] planks;
        int count;
        static readonly List<MgPlanks> live = new List<MgPlanks>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => live.Clear();

        public int Laid => count;
        public int Done => count;
        public int Total => PlankBridge.Slots;

        public static MgPlanks Build(CourseBuilder b, MissionStation station)
        {
            var k = b.kit;
            MissionGames.Floor(b, 0f, Chasm0);
            MissionGames.Floor(b, PlankBridge.Chasm1, 20f);
            var slots = new GameObject[PlankBridge.Slots];
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
            var boards = new GameObject[PlankBridge.Planks];
            var blocks = new BoxCollider[PlankBridge.Piles];
            for (int p = 0; p < PlankBridge.Piles; p++)
            {
                float px = PlankBridge.PileCentreX(p), pz = PlankBridge.PileZ;
                for (int i = 0; i < PerPile; i++)
                    boards[p * PerPile + i] = b.Box("Pile plank", MissionGames.V(px - PlankWidth * .5f, i * PlankBridge.PileStep, pz - Row * .5f),
                                                    MissionGames.V(px + PlankWidth * .5f, i * PlankBridge.PileStep + PlankBridge.Thickness, pz + Row * .5f), k.boardDark, false, false);
                // The stack is solid (a pawn walks into it, not through it) and held, never climbed; the boards draw it.
                var block = b.Box("Plank pile", MissionGames.V(px - PlankWidth * .5f, 0f, pz - Row * .5f), MissionGames.V(px + PlankWidth * .5f, PileHeight, pz + Row * .5f), null);
                block.GetComponent<Renderer>().enabled = false;
                block.AddComponent<GrabOnlySurface>();
                blocks[p] = block.GetComponent<BoxCollider>();
            }
            var game = b.parent.gameObject.AddComponent<MgPlanks>();
            game.station = station;
            game.slots = slots;
            game.pileBoards = boards;
            game.pileBlocks = blocks;
            return game;
        }

        void OnEnable()
        {
            if (!live.Contains(this)) live.Add(this);
            MiniGameHooks.HandsFull = HandsFull;
        }

        void OnDisable()
        {
            live.Remove(this);
            if (live.Count == 0 && MiniGameHooks.HandsFull == (System.Func<ICharacterDriver, bool>)HandsFull) MiniGameHooks.HandsFull = null;
        }

        void Start()
        {
            DressTeams();
            ShowPiles();
        }

        // A pawn carrying a plank of any B game has its hands full (the host and offline, where pawns are simulated).
        static bool HandsFull(ICharacterDriver driver)
        {
            if (driver == null) return false;
            foreach (var game in live)
                if (game != null && game.Carries(driver) >= 0) return true;
            return false;
        }

        int Carries(ICharacterDriver driver)
        {
            for (int i = 0; i < PlankBridge.Planks; i++)
                if (board.State[i] == PlankState.Carried && ReferenceEquals(carrier[i], driver)) return i;
            return -1;
        }

        int Team => station != null ? station.Team : Teams.White;

        // ---------------------------------------------------------------- the host

        public override void WriteNet(ref NetBlock block) => board.Write(ref block);

        public override void ReadNet(ref NetBlock block)
        {
            incoming.Read(ref block);
            board.CopyFrom(incoming);
            count = board.LaidCount;
            ShowSlots();
            ShowPiles();
        }

        void FixedUpdate()
        {
            if (station == null || Remote) return;
            float dt = Time.fixedDeltaTime;
            var pawns = station.Pawns();
            for (int i = 0; i < PlankBridge.Planks; i++)
                if (board.State[i] == PlankState.Carried) Carry(i, pawns, dt);
            letGo.Clear();
            foreach (var d in letGoFirst)
                if (d == null || d is Object o && o == null || !(d is IMiniGamePawn m) || !m.GrabHeld) letGo.Add(d);
            foreach (var d in letGo) letGoFirst.Remove(d);
            if (!Completed)
                foreach (var p in pawns) TryTake(p);
            count = board.LaidCount;
            Progress01 = count / (float)PlankBridge.Slots;
            if (count >= PlankBridge.Slots) Complete();
        }

        void Carry(int i, List<MissionStation.Pawn> pawns, float dt)
        {
            var who = carrier[i];
            bool here = false;
            foreach (var p in pawns) if (ReferenceEquals(p.Driver, who)) here = true;
            var target = who != null && !(who is Object o && o == null) ? who.FollowTarget : null;
            if (!here || target == null) { SendHome(i); return; }
            var hips = transform.InverseTransformPoint(target.position);
            // A respawn or a throw across the room is no walk: the plank goes home.
            if ((hips - lastHips[i]).sqrMagnitude > 9f || hips.y < -1.5f) { SendHome(i); return; }
            lastHips[i] = hips;
            yaw[i] = Mathf.LerpAngle(yaw[i], Facing(target, yaw[i]), 1f - Mathf.Exp(-12f * dt));
            var c = Centre(hips, yaw[i]);
            if (!PlankBridge.InCarryArea(c.x, c.z)) { SendHome(i); return; }
            var mg = who as IMiniGamePawn;
            if (mg != null && (!mg.GrabHeld || mg.Down || mg.Diving)) { PutDown(i, c, yaw[i]); return; }
            if (PlankBridge.Snap(c.x, c.z, board.Laid) >= 0)
            {
                hover[i] += dt;
                if (hover[i] >= PlankBridge.HoverSeconds) PutDown(i, c, yaw[i]);
            }
            else hover[i] = 0f;
        }

        // Let go of at `c` (the game's frame): laid in the free slot there, back on its pile if it falls into the chasm,
        // leaves the station or lands on a pile, else lying on the floor, square to the room.
        void PutDown(int i, Vector3 c, float facing)
        {
            int slot = PlankBridge.Snap(c.x, c.z, board.Laid);
            if (slot >= 0)
            {
                board.Laid |= 1 << slot;
                board.State[i] = PlankState.Laid;
                Free(i);
                ShowSlots();
                return;
            }
            if (!PlankBridge.InArea(c.x, c.z) || PlankBridge.OverChasm(c.x, c.z, board.Laid) || PlankBridge.OnPile(c.x, c.z)) { Return(i); return; }
            board.State[i] = PlankState.Lying;
            board.X[i] = PlankBoard.GridX(c.x);
            board.Z[i] = PlankBoard.GridZ(c.z);
            board.Turned[i] = Mathf.Abs(Mathf.Sin(facing * Mathf.Deg2Rad)) > .7071f;
            Free(i);
        }

        // Home while still in the carrier's hands: the carrier lets go of the button before taking another.
        void SendHome(int i)
        {
            if (carrier[i] != null) letGoFirst.Add(carrier[i]);
            Return(i);
        }

        void Return(int i)
        {
            board.State[i] = PlankState.Pile;
            board.Turned[i] = false;
            Free(i);
            ShowPiles();
        }

        void Free(int i)
        {
            carrier[i] = null;
            board.Seat[i] = PlankBoard.NoSeat;
            hover[i] = 0f;
        }

        // The grab button down, the hands free and one of them within reach of a pile or a lying plank: it is taken.
        void TryTake(MissionStation.Pawn p)
        {
            var who = p.Driver;
            if (!(who is IMiniGamePawn mg) || !mg.GrabHeld || mg.Down || mg.Diving || HandsFull(who) || letGoFirst.Contains(who)) return;
            for (int hand = 0; hand < 2; hand++)
            {
                var held = mg.Held(hand);
                if (held != null && !held.transform.IsChildOf(transform)) return;   // that hand holds a pawn, a wall, a rope
            }
            int take = -1;
            float best = PlankBridge.PickReach;
            for (int hand = 0; hand < 2; hand++)
            {
                var at = transform.InverseTransformPoint(mg.HandPosition(hand));
                for (int pile = 0; pile < PlankBridge.Piles; pile++)
                {
                    int n = board.Stock(pile);
                    if (n == 0) continue;
                    float h = n * PlankBridge.PileStep * .5f;
                    float d = BoxDistance(at, new Vector3(PlankBridge.PileCentreX(pile), h, PlankBridge.PileZ), new Vector3(PlankWidth * .5f, h, Row * .5f));
                    if (d < best) { best = d; take = board.TopOf(pile); }
                }
                for (int i = 0; i < PlankBridge.Planks; i++)
                {
                    if (board.State[i] != PlankState.Lying) continue;
                    var half = board.Turned[i] ? new Vector3(Row * .5f, PlankBridge.Thickness * .5f, PlankWidth * .5f)
                                               : new Vector3(PlankWidth * .5f, PlankBridge.Thickness * .5f, Row * .5f);
                    float d = BoxDistance(at, new Vector3(board.X[i], LyingY(i), board.Z[i]), half);
                    if (d < best) { best = d; take = i; }
                }
            }
            if (take < 0) return;
            board.State[take] = PlankState.Carried;
            board.Turned[take] = false;
            carrier[take] = who;
            board.Seat[take] = Seat(who);
            hover[take] = 0f;
            lastHips[take] = transform.InverseTransformPoint(who.FollowTarget.position);
            yaw[take] = Facing(who.FollowTarget, 0f);
            ShowPiles();
        }

        static float BoxDistance(Vector3 p, Vector3 centre, Vector3 half)
        {
            var d = p - centre;
            d = new Vector3(Mathf.Max(0f, Mathf.Abs(d.x) - half.x), Mathf.Max(0f, Mathf.Abs(d.y) - half.y), Mathf.Max(0f, Mathf.Abs(d.z) - half.z));
            return d.magnitude;
        }

        // Which way a pawn faces, in degrees about the game's up (its hips' forward), or `fallback` when they point up.
        float Facing(Transform hips, float fallback)
        {
            var f = transform.InverseTransformDirection(hips.forward);
            f.y = 0f;
            return f.sqrMagnitude < 1e-4f ? fallback : Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        static Vector3 Centre(Vector3 hips, float facing) => hips + Quaternion.Euler(0f, facing, 0f) * new Vector3(0f, CarryUp, CarryAhead);

        // Planks lying on top of each other differ by 4 mm, so their faces never share a plane.
        static float LyingY(int i) => PlankBridge.Thickness * .5f + i * .004f;

        // ---------------------------------------------------------------- what every PC draws

        void LateUpdate()
        {
            if (station == null) return;
            MakePlanks();
            for (int i = 0; i < PlankBridge.Planks; i++) Draw(i);
        }

        void Draw(int i)
        {
            var plank = planks[i];
            var state = board.State[i];
            ICharacterDriver who = null;
            if (state == PlankState.Carried)
            {
                who = Remote ? Driver(board.Seat[i]) : carrier[i];
                if (who is Object o && o == null) who = null;
                if (who == null || who.FollowTarget == null) state = PlankState.Pile;   // a carrier this PC cannot draw
            }
            if (state != plank.Shown)
            {
                // The picture starts where it was drawn and eases to its new place (picked up, put down).
                bool from = plank.Root.activeSelf && plank.Look != null;
                plank.FromPosition = from ? plank.Look.position : PileTop(PlankBridge.PileOf(i));
                plank.FromRotation = from ? plank.Look.rotation : transform.rotation;
                plank.Eased = state == PlankState.Carried || state == PlankState.Lying ? 0f : 1f;
                plank.Shown = state;
                bool on = state == PlankState.Carried || state == PlankState.Lying;
                if (plank.Root.activeSelf != on) plank.Root.SetActive(on);
                plank.Collider.enabled = state == PlankState.Lying;
                if (state == PlankState.Carried && who != null) yaw[i] = Facing(who.FollowTarget, yaw[i]);
                if (state == PlankState.Lying)
                {
                    plank.Root.transform.SetPositionAndRotation(transform.TransformPoint(new Vector3(board.X[i], LyingY(i), board.Z[i])),
                        transform.rotation * Quaternion.Euler(0f, board.Turned[i] ? 90f : 0f, 0f));
                    plank.Body.position = plank.Root.transform.position;
                    plank.Body.rotation = plank.Root.transform.rotation;
                }
            }
            if (state == PlankState.Carried)
            {
                var hips = transform.InverseTransformPoint(who.FollowTarget.position);
                if (Remote) yaw[i] = Mathf.LerpAngle(yaw[i], Facing(who.FollowTarget, yaw[i]), 1f - Mathf.Exp(-12f * Time.deltaTime));
                plank.Root.transform.SetPositionAndRotation(transform.TransformPoint(Centre(hips, yaw[i])), transform.rotation * Quaternion.Euler(0f, yaw[i], 0f));
            }
            if (plank.Look == null || plank.Eased >= 1f) return;
            plank.Eased = Mathf.Min(1f, plank.Eased + Time.deltaTime / EaseSeconds);
            float k = 1f - (1f - plank.Eased) * (1f - plank.Eased);
            plank.Look.SetPositionAndRotation(Vector3.Lerp(plank.FromPosition, plank.Root.transform.position, k),
                Quaternion.Slerp(plank.FromRotation, plank.Root.transform.rotation, k));
            if (plank.Eased >= 1f) plank.Look.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        void MakePlanks()
        {
            if (planks != null) return;
            planks = new Plank[PlankBridge.Planks];
            for (int i = 0; i < PlankBridge.Planks; i++)
            {
                var root = new GameObject("Plank " + (i + 1));
                root.transform.SetParent(transform, false);
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.None;
                var box = root.AddComponent<BoxCollider>();
                box.size = new Vector3(PlankWidth, PlankBridge.Thickness, Row);
                box.enabled = false;
                root.AddComponent<GrabOnlySurface>();
                var look = MakeLook(root.transform, "Plank (board)");
                root.SetActive(false);
                planks[i] = new Plank { Root = root, Collider = box, Body = body, Look = look.transform };
            }
        }

        // A plank's picture: a 2.5 x 0.12 x 2 box (the walnut model inside it when the B art is installed), the team's
        // wood and its colour bands. No collider.
        GameObject MakeLook(Transform parent, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(PlankWidth, PlankBridge.Thickness, Row);
            go.GetComponent<Renderer>().sharedMaterial = station.Kit.boardDark;
            MiniGameModels.DressPlank(go);   // the walnut plank model when the B art is installed (R503)
            DressTeam(go, true);
            return go;
        }

        public Vector3 PileTop(int pile) =>
            transform.TransformPoint(new Vector3(PlankBridge.PileCentreX(pile), board.Stock(pile) * PlankBridge.PileStep + PlankBridge.Thickness * .5f, PlankBridge.PileZ));

        void ShowSlots()
        {
            for (int s = 0; s < PlankBridge.Slots && s < slots.Length; s++)
            {
                bool on = PlankBridge.IsLaid(board.Laid, s);
                if (slots[s] != null && slots[s].activeSelf != on) slots[s].SetActive(on);
            }
        }

        void ShowPiles()
        {
            for (int p = 0; p < PlankBridge.Piles; p++)
            {
                int n = board.Stock(p);
                for (int i = 0; i < PerPile; i++)
                    if (pileBoards[p * PerPile + i] != null) pileBoards[p * PerPile + i].SetActive(i < n);
                var block = p < pileBlocks.Length ? pileBlocks[p] : null;
                if (block == null) continue;
                block.enabled = n > 0;
                // the box is the full stack's size (scale); the collider is the part that has planks on it
                float f = n / (float)PerPile;
                block.size = new Vector3(1f, Mathf.Max(.01f, f), 1f);
                block.center = new Vector3(0f, -.5f + f * .5f, 0f);
            }
        }

        // ---------------------------------------------------------------- the teams' planks

        static readonly Dictionary<(Material, int), Material> tinted = new Dictionary<(Material, int), Material>();
        static readonly HashSet<Material> tints = new HashSet<Material>();
        // Black's dark ebony over the plank model's walnut texture (multiplied). White's pale maple is its own copy of the
        // atlas (PR_Mid_WoodKit_Maple, ArtSource/PawnRush/MiniGames/Mid/b_plank_maple.py: a multiply cannot lighten a
        // dark texture); PaleWood only stands in without it. The flat box without a model takes plain woods.
        static readonly Color PaleWood = new Color(1.55f, 1.38f, 1.12f), DarkWood = new Color(.36f, .29f, .26f);
        const string MapleMaterial = "PawnRushMiniGames/Materials/PR_Mid_WoodKit_Maple";

        // The laid slots and the piles' boards in the team's wood (MiniGameModels dresses them first; Start and the
        // B art call this), and the piles' boards banded in the team's colour.
        public void DressTeams()
        {
            foreach (var s in slots) if (s != null) DressTeam(s, false);
            foreach (var p in pileBoards) if (p != null) DressTeam(p, true);
        }

        void DressTeam(GameObject box, bool banded)
        {
            int team = Team;
            var own = box.GetComponent<Renderer>();
            if (own != null && own.enabled && !tints.Contains(own.sharedMaterial))
            {
                own.sharedMaterial = Arena.Lit(team == Teams.Black ? "M_PlankEbony" : "M_PlankMaple",
                                               team == Teams.Black ? Arena.Hex("#3A2C25") : Arena.Hex("#D9B98A"), 0f, .7f);
                tints.Add(own.sharedMaterial);
            }
            var model = box.transform.Find("MODEL_Ride");
            if (model != null)
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (mats[m] == null || tints.Contains(mats[m])) continue;
                        mats[m] = Tint(mats[m], team);
                        changed = true;
                    }
                    if (changed) r.sharedMaterials = mats;
                }
            if (banded && box.transform.Find("Team band") == null)
            {
                // Two bands across the plank near its ends, standing a little proud of every face (no shared planes).
                var paint = Arena.Lit("M_PlankTeamBand", station != null ? station.Accent(team) : Arena.TeamColor(team), .35f, .5f);
                foreach (float z in new[] { -.4f, .4f })
                {
                    var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    band.name = "Team band";
                    DestroyImmediate(band.GetComponent<Collider>());
                    band.transform.SetParent(box.transform, false);
                    band.transform.localPosition = new Vector3(0f, 0f, z);
                    band.transform.localScale = new Vector3(1.012f, 1.12f, .07f);
                    var r = band.GetComponent<Renderer>();
                    r.sharedMaterial = paint;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        static Material Tint(Material source, int team)
        {
            if (tinted.TryGetValue((source, team), out var m) && m != null) return m;
            if (team != Teams.Black && source.name == "PR_Mid_WoodKit")
            {
                m = Resources.Load<Material>(MapleMaterial);
                if (m != null)
                {
                    tinted[(source, team)] = m;
                    tints.Add(m);
                    return m;
                }
            }
            m = new Material(source) { name = source.name + (team == Teams.Black ? " (ebony)" : " (maple)") };
            var wood = team == Teams.Black ? DarkWood : PaleWood;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * wood);
            else if (m.HasProperty("_Color")) m.SetColor("_Color", m.GetColor("_Color") * wood);
            tinted[(source, team)] = m;
            tints.Add(m);
            return m;
        }

        // ---------------------------------------------------------------- for the effects (MiniGamePlankFx)

        public PlankBoard Board => board;

        // The picture of plank i (null until the planks are made).
        public Transform PlankLook(int i) => planks != null && i >= 0 && i < planks.Length ? planks[i].Look : null;

        public bool OverChasm(Vector3 world)
        {
            var at = transform.InverseTransformPoint(world);
            return PlankBridge.OverChasm(at.x, at.z, board.Laid);
        }

        // Where a carried plank would be laid: the slot, and how sure (1 laid now, about .5 within reach, .3 nearly).
        public int Preview(int i, out float strength)
        {
            strength = 0f;
            if (planks == null || board.State[i] != PlankState.Carried || !planks[i].Root.activeSelf) return -1;
            var c = transform.InverseTransformPoint(planks[i].Root.transform.position);
            int slot = PlankBridge.Snap(c.x, c.z, board.Laid);
            if (slot >= 0)
            {
                strength = .55f + .45f * Mathf.Clamp01(hover[i] / PlankBridge.HoverSeconds);
                return slot;
            }
            slot = PlankBridge.Snap(c.x, c.z, board.Laid, 1.7f);
            if (slot >= 0) strength = .3f;
            return slot;
        }

        public override void ResetGame()
        {
            base.ResetGame();
            board.Reset();
            for (int i = 0; i < PlankBridge.Planks; i++) Free(i);
            letGoFirst.Clear();
            count = 0;
            ShowSlots();
            ShowPiles();
        }
    }
}
