using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A, painting the chessboard: an 8 x 8 board of 2.25 m squares (u 2..20, v -9..9). A square
    // a pawn of the team stands on for 1.5 s turns gold; leaving it (or being pushed off) resets
    // its time. A square the other team stands on cannot be painted; painted ones stay. At most
    // four squares of a team fill at once. 64 x 1.5 s: about two minutes alone.
    public sealed class MgPaint : MiniGameBase
    {
        public const int N = 8;
        public const float Cell = 2.25f, U0 = 2f, Hold = 1.5f;

        [SerializeField] MissionStation station;
        [SerializeField] Renderer[] squares = new Renderer[N * N];

        readonly float[] timer = new float[N * N];
        readonly bool[] painted = new bool[N * N];
        readonly HashSet<int> blocked = new HashSet<int>(), active = new HashSet<int>();
        int count;

        public int Painted => count;

        public static MgPaint Build(CourseBuilder b, MissionStation station)
        {
            var k = b.kit;
            MissionGames.Floor(b, 0f, 20f);
            var squares = new Renderer[N * N];
            for (int iz = 0; iz < N; iz++)
                for (int ix = 0; ix < N; ix++)
                {
                    float x0 = -9f + ix * Cell, z0 = U0 + iz * Cell;
                    var sq = b.Box("Square", MissionGames.V(x0 + .03f, 0f, z0 + .03f), MissionGames.V(x0 + Cell - .03f, .03f, z0 + Cell - .03f),
                                   ((ix + iz) & 1) == 0 ? k.boardDark : k.boardLight, false, false);
                    squares[iz * N + ix] = sq.GetComponent<Renderer>();
                }
            var game = b.parent.gameObject.AddComponent<MgPaint>();
            game.station = station;
            game.squares = squares;
            return game;
        }

        static int SquareAt(Vector3 local)
        {
            if (local.y > 2.5f) return -1;
            int ix = Mathf.FloorToInt((local.x + 9f) / Cell), iz = Mathf.FloorToInt((local.z - U0) / Cell);
            return ix < 0 || iz < 0 || ix >= N || iz >= N ? -1 : iz * N + ix;
        }

        public override ulong StateBits
        {
            get
            {
                ulong bits = 0;
                for (int s = 0; s < painted.Length; s++) if (painted[s]) bits |= 1UL << s;
                return bits;
            }
        }

        // Online, not the host: the host's squares (and the count the host would take over from).
        public override void ApplyRemote(float progress, bool completed, ulong bits)
        {
            count = 0;
            for (int s = 0; s < painted.Length; s++)
            {
                bool on = (bits >> s & 1UL) != 0;
                if (on) count++;
                if (on == painted[s]) continue;
                painted[s] = on;
                int ix = s % N, iz = s / N;
                if (squares[s] != null)
                    squares[s].sharedMaterial = on ? station.Kit.rankGold : ((ix + iz) & 1) == 0 ? station.Kit.boardDark : station.Kit.boardLight;
            }
            base.ApplyRemote(progress, completed, bits);
        }

        void FixedUpdate()
        {
            if (Completed || station == null || Remote) return;
            blocked.Clear();
            active.Clear();
            var pawns = station.Pawns();
            foreach (var p in pawns)
                if (!p.Own) { int s = SquareAt(p.Local); if (s >= 0) blocked.Add(s); }
            foreach (var p in pawns)
            {
                if (!p.Own || active.Count >= PawnRushMissions.WorkerCap) continue;
                int s = SquareAt(p.Local);
                if (s >= 0 && !painted[s] && !blocked.Contains(s)) active.Add(s);
            }
            for (int s = 0; s < timer.Length; s++)
            {
                if (painted[s]) continue;
                if (!active.Contains(s)) { timer[s] = 0f; continue; }
                timer[s] += Time.fixedDeltaTime;
                if (timer[s] < Hold) continue;
                painted[s] = true;
                count++;
                if (squares[s] != null) squares[s].sharedMaterial = station.Kit.rankGold;
            }
            Progress01 = count / (float)(N * N);
            if (count >= N * N) Complete();
        }

        public override void ResetGame()
        {
            base.ResetGame();
            count = 0;
            for (int s = 0; s < timer.Length; s++)
            {
                timer[s] = 0f;
                painted[s] = false;
                int ix = s % N, iz = s / N;
                if (squares[s] != null) squares[s].sharedMaterial = ((ix + iz) & 1) == 0 ? station.Kit.boardDark : station.Kit.boardLight;
            }
        }
    }
}
