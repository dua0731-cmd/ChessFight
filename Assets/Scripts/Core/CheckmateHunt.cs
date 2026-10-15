using System;
using System.Collections.Generic;

namespace ChessFight.Network
{
    // H, the checkmate hunt (spec §6): six enemy kings flee across an 8 x 8 board. Each of our standing pawns
    // attacks the square under its hips and the eight round it; a king never steps onto an attacked square.
    // On its turn a king that stands attacked takes a check (or, with nowhere safe to go, is mated); four checks
    // or a mate freeze it, and a frozen king is captured by a dive. An enemy pawn reaching a frozen king frees it.
    //
    // The kings' minds live here, without Unity, so the host and the tests run the same thing: the same pawns
    // in, the same moves out. Board space: files 0..7 (a..h), ranks 1..8, as in PawnRushBoard.
    public sealed class CheckmateHunt
    {
        public const int Kings = 6;
        public static readonly string[] Starts = { "b7", "e6", "g5", "c3", "g2", "b4" };
        public static readonly string[] Pillars = { "c6", "f3", "f7" };
        public const float TurnSeconds = 1f, FirstTurn = .45f, React = .2f, Immunity = 3f, RescueImmunity = 1.5f;
        public const float StaleRange = 3.2f, MoveRange = 9f, MoveGain = .3f, NeighbourWeight = 1.3f;
        public const float CaptureReach = .8f, CaptureSpeed = 5f, RescueReach = .9f;
        public const int ChecksToFreeze = 4;

        public enum KingState : byte { Free = 0, Frozen = 1, Captured = 2 }

        public sealed class King
        {
            public int File, Rank, Checks;
            public KingState State;
            public float Immune;
            public double NextTurn;
            public bool Attacked;   // its square was attacked at the last step
            public bool Mated;      // frozen by a mate (not by four checks)
        }

        // North, north-east, east, south-east, south, south-west, west, north-west: the tie order (§6.2.4).
        static readonly int[] DF = { 0, 1, 1, 1, 0, -1, -1, -1 };
        static readonly int[] DR = { 1, 1, 0, -1, -1, -1, 0, 1 };

        public readonly King[] All = new King[Kings];
        readonly bool[] pillar = new bool[64];
        readonly bool[] attacked = new bool[64];
        double last = double.NaN;

        public int Captured { get { int n = 0; foreach (var k in All) if (k.State == KingState.Captured) n++; return n; } }
        public bool Done => Captured >= Kings;

        public CheckmateHunt()
        {
            foreach (var name in Pillars)
            {
                PawnRushBoard.Parse(name, out int f, out int r);
                pillar[PawnRushBoard.Index(f, r)] = true;
            }
            for (int k = 0; k < Kings; k++) All[k] = new King();
            Reset(0);
        }

        public void Reset(double now)
        {
            for (int k = 0; k < Kings; k++)
            {
                PawnRushBoard.Parse(Starts[k], out int f, out int r);
                var king = All[k];
                king.File = f; king.Rank = r; king.Checks = 0; king.State = KingState.Free; king.Immune = 0f;
                king.NextTurn = now + k * FirstTurn; king.Attacked = false; king.Mated = false;
            }
            last = now;
        }

        public static bool IsPillar(int file, int rank)
        {
            foreach (var name in Pillars)
            {
                PawnRushBoard.Parse(name, out int f, out int r);
                if (f == file && r == rank) return true;
            }
            return false;
        }

        public bool Attacked(int file, int rank) => PawnRushBoard.OnBoard(file, rank) && attacked[PawnRushBoard.Index(file, rank)];

        // Squares our pawns attack: the square under each pawn's hips and the eight round it.
        void Mark(IList<StationPoint> ours)
        {
            Array.Clear(attacked, 0, attacked.Length);
            foreach (var p in ours)
            {
                if (!PawnRushBoard.SquareAt(p, out int f, out int r)) continue;
                for (int df = -1; df <= 1; df++)
                    for (int dr = -1; dr <= 1; dr++)
                        if (PawnRushBoard.OnBoard(f + df, r + dr)) attacked[PawnRushBoard.Index(f + df, r + dr)] = true;
            }
        }

        bool Blocked(int file, int rank, King self)
        {
            if (!PawnRushBoard.OnBoard(file, rank) || pillar[PawnRushBoard.Index(file, rank)]) return true;
            foreach (var k in All)
                if (k != self && k.State != KingState.Captured && k.File == file && k.Rank == rank) return true;
            return false;
        }

        bool Safe(int file, int rank, King self) => !Blocked(file, rank, self) && !attacked[PawnRushBoard.Index(file, rank)];

        int SafeNeighbours(int file, int rank, King self)
        {
            int n = 0;
            for (int d = 0; d < 8; d++) if (Safe(file + DF[d], rank + DR[d], self)) n++;
            return n;
        }

        static float Nearest(StationPoint at, IList<StationPoint> ours)
        {
            float best = 99f;
            foreach (var p in ours) best = Math.Min(best, at.DistanceTo(p));
            return best;
        }

        float Score(int file, int rank, King self, IList<StationPoint> ours) =>
            Nearest(PawnRushBoard.Center(file, rank), ours) + NeighbourWeight * SafeNeighbours(file, rank, self);

        // One step of the hunt at `now`: our standing pawns' hips and the other team's (both in board space).
        public void Step(double now, IList<StationPoint> ours, IList<StationPoint> theirs)
        {
            float dt = double.IsNaN(last) ? 0f : (float)Math.Max(0, now - last);
            last = now;
            Mark(ours);
            foreach (var king in All)
            {
                if (king.State == KingState.Captured) continue;
                king.Immune = Math.Max(0f, king.Immune - dt);
                var at = PawnRushBoard.Center(king.File, king.Rank);
                if (king.State == KingState.Frozen)
                {
                    // An enemy pawn at a frozen king sets it free.
                    foreach (var p in theirs)
                        if (at.DistanceTo(p) <= RescueReach)
                        {
                            king.State = KingState.Free;
                            king.Checks = 0;
                            king.Immune = RescueImmunity;
                            king.Mated = false;
                            king.NextTurn = now + TurnSeconds;
                            break;
                        }
                    continue;
                }
                bool attackedNow = attacked[PawnRushBoard.Index(king.File, king.Rank)];
                // A king that comes under attack takes its turn sooner (a reaction).
                if (attackedNow && !king.Attacked) king.NextTurn = Math.Min(king.NextTurn, now + React);
                king.Attacked = attackedNow;
                if (now < king.NextTurn) continue;
                king.NextTurn = now + TurnSeconds;
                Turn(king, attackedNow, ours);
            }
        }

        void Turn(King king, bool attackedNow, IList<StationPoint> ours)
        {
            int safe = SafeNeighbours(king.File, king.Rank, king);
            if (attackedNow)
            {
                if (king.Immune <= 0f)
                {
                    if (safe == 0)
                    {
                        king.State = KingState.Frozen;   // checkmate
                        king.Mated = true;
                        return;
                    }
                    king.Checks++;
                    if (king.Checks >= ChecksToFreeze)
                    {
                        king.State = KingState.Frozen;
                        return;
                    }
                    king.Immune = Immunity;
                }
                if (safe > 0) MoveBest(king, ours, float.MinValue);
                return;
            }
            float nearest = Nearest(PawnRushBoard.Center(king.File, king.Rank), ours);
            if (safe == 0)
            {
                if (nearest <= StaleRange) Stalemate(king, ours);
                return;
            }
            if (nearest <= MoveRange) MoveBest(king, ours, Score(king.File, king.Rank, king, ours) + MoveGain);
        }

        // To the best safe neighbour, if it scores more than `beat`.
        void MoveBest(King king, IList<StationPoint> ours, float beat)
        {
            int best = -1;
            float bestScore = beat;
            for (int d = 0; d < 8; d++)
            {
                int f = king.File + DF[d], r = king.Rank + DR[d];
                if (!Safe(f, r, king)) continue;
                float score = Score(f, r, king, ours);
                if (score > bestScore) { bestScore = score; best = d; }
            }
            if (best < 0) return;
            king.File += DF[best];
            king.Rank += DR[best];
        }

        // Nowhere to go and a pawn close: away to the free, unattacked square farthest from our pawns.
        void Stalemate(King king, IList<StationPoint> ours)
        {
            int bestF = -1, bestR = -1;
            float best = -1f;
            for (int f = 0; f < PawnRushBoard.N; f++)
                for (int r = 1; r <= PawnRushBoard.N; r++)
                {
                    if (!Safe(f, r, king)) continue;
                    float d = Nearest(PawnRushBoard.Center(f, r), ours);
                    if (d > best) { best = d; bestF = f; bestR = r; }
                }
            if (bestF < 0) return;
            king.File = bestF;
            king.Rank = bestR;
        }

        // A dive onto king k: captured if it is frozen and the pawn is close enough and diving or fast.
        public bool TryCapture(int k, StationPoint pawn, bool diving, float flatSpeed)
        {
            var king = All[k];
            if (king.State != KingState.Frozen) return false;
            if (PawnRushBoard.Center(king.File, king.Rank).DistanceTo(pawn) > CaptureReach || !(diving || flatSpeed >= CaptureSpeed)) return false;
            king.State = KingState.Captured;
            return true;
        }

        // Two bytes a king (spec §6.6): square 6 bits, checks 3, state 2, immune 1.
        public static ushort Pack(King k) =>
            (ushort)((PawnRushBoard.Index(k.File, k.Rank) & 63) | (Math.Min(7, k.Checks) & 7) << 6 | ((int)k.State & 3) << 9 | (k.Immune > 0f ? 1 : 0) << 11 | (k.Mated ? 1 : 0) << 12);

        public static void Unpack(ushort v, King into)
        {
            int index = v & 63;
            into.File = PawnRushBoard.FileOf(index);
            into.Rank = PawnRushBoard.RankOf(index);
            into.Checks = v >> 6 & 7;
            into.State = (KingState)(v >> 9 & 3);
            into.Immune = (v >> 11 & 1) != 0 ? 1f : 0f;
            into.Mated = (v >> 12 & 1) != 0;
        }
    }
}
