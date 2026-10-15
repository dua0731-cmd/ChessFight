using System;
using System.Collections.Generic;

namespace ChessFight.Network
{
    // The rules of the Pawn Rush mini-games v1.0 (Docs/KingRush/PAWN_RUSH_MINIGAMES_v1.md) that need no Unity:
    // the board grid, the en passant statues' clock, the drawbridge rope, the knight pads' islands and the king
    // escort's path. The host and the offline playtest run them; every PC can recompute what depends only on the
    // shared clock or a seed; the Core tests check them.
    //
    // Coordinates are a station's (u north 0..20, v across -10..10) as seen from white's station. Black's
    // station is the mirror: the Unity side multiplies v by -1 there.

    // A 2-D point in station coordinates (v across, u north).
    public struct StationPoint
    {
        public float V, U;
        public StationPoint(float v, float u) { V = v; U = u; }
        public float DistanceTo(StationPoint o) { float dv = V - o.V, du = U - o.U; return (float)Math.Sqrt(dv * dv + du * du); }
        public override string ToString() => "(" + V.ToString("0.##") + ", " + U.ToString("0.##") + ")";
    }

    // The 8 x 8 board every board game shares (A, G, H, I): files a..h across (v -9..9), ranks 1..8 north (u 2..20),
    // squares of 2.25 m, a1 dark as on a real board.
    public static class PawnRushBoard
    {
        public const int N = 8;
        public const float Cell = 2.25f, V0 = -9f, U0 = 2f;

        // The middle of file f (0..7), rank r (1..8).
        public static StationPoint Center(int file, int rank) => new StationPoint(V0 + Cell * (file + .5f), U0 + Cell * (rank - .5f));

        // a1 is dark, h1 light.
        public static bool IsLight(int file, int rank) => ((file + rank) & 1) == 0;

        // i = file x 8 + rank - 1 (spec §3.6).
        public static int Index(int file, int rank) => file * N + rank - 1;
        public static int FileOf(int index) => index / N;
        public static int RankOf(int index) => index % N + 1;

        public static bool OnBoard(int file, int rank) => file >= 0 && file < N && rank >= 1 && rank <= N;

        // The square a point is on, or false off the board.
        public static bool SquareAt(StationPoint p, out int file, out int rank)
        {
            file = (int)Math.Floor((p.V - V0) / Cell);
            rank = (int)Math.Floor((p.U - U0) / Cell) + 1;
            return OnBoard(file, rank);
        }

        public static string Name(int file, int rank) => ((char)('a' + file)).ToString() + rank;

        public static bool Parse(string name, out int file, out int rank)
        {
            file = rank = -1;
            if (name == null || name.Length != 2) return false;
            file = name[0] - 'a';
            rank = name[1] - '0';
            return OnBoard(file, rank);
        }
    }

    // F, en passant (spec §4): sixteen statues on a 14 s cycle with staggered offsets. Every position is a
    // function of the shared clock, so a client draws the same statue the host judges, with nothing sent.
    public static class EnPassantRules
    {
        public const int Statues = 16;
        public const float Period = 14f, Advance = 2.2f, Dash = .3f, WindowEnd = 1.5f, ReturnEnd = 4.5f, Warn = 13.5f;
        public const float Grace = .15f, Reach = .8f, Speed = 5f, Bounce = 2f, Repeat = .3f;
        public static readonly float[] Columns = { -6.75f, -2.25f, 2.25f, 6.75f };
        public static readonly float[] Homes = { 6f, 10.4f, 14.8f, 19.2f };

        // i row-first: south row first, west column first.
        public static StationPoint Home(int i) => new StationPoint(Columns[i % 4], Homes[i / 4]);

        public static float Offset(int i) => ((i * 7) % Statues) / (float)Statues * Period;

        public static float Phase(double time, int i)
        {
            double ph = (time + Offset(i)) % Period;
            if (ph < 0) ph += Period;
            return (float)ph;
        }

        // Metres south of home at this phase.
        public static float Displacement(float ph)
        {
            if (ph < Dash) { float x = ph / Dash; return Advance * (1f - (1f - x) * (1f - x)); }
            if (ph < WindowEnd) return Advance;
            if (ph < ReturnEnd) return Advance * (1f - (ph - WindowEnd) / (ReturnEnd - WindowEnd));
            return 0f;
        }

        // The 0.5 s warning before the dash (a bob, 6 Hz).
        public static bool Warning(float ph) => ph >= Warn;

        // Capturable: from the end of the dash to the end of the window, plus the host's grace (§1.2).
        public static bool InWindow(float ph, float grace = 0f) => ph >= Dash && ph <= WindowEnd + grace;

        public static StationPoint Position(double time, int i)
        {
            var home = Home(i);
            return new StationPoint(home.V, home.U - Displacement(Phase(time, i)));
        }

        // A capture: our pawn within reach (flat), diving or fast, while the window is open.
        public static bool Captures(float flatDistance, bool diving, float flatSpeed, float ph) =>
            flatDistance <= Reach && (diving || flatSpeed >= Speed) && InWindow(ph, Grace);
    }

    // C, the drawbridge rope (spec §9): one heavy rope, pulled 7 m in all.
    public static class DrawbridgeRope
    {
        public const float Length = 7f, SprintForce = 1.8f, HoldForce = .5f, MaxSpeed = .35f, Factor = .045f, Power = 1.35f, MinSpeed = .04f;

        // pullers: the team's pawns holding a knot and walking away from the drum (sprinters count 1.8);
        // holders: the other team's pawns holding a knot and walking toward it (0.5 each).
        public static float Speed(float pull, float hold)
        {
            float net = pull - hold;
            float v = net > 0f ? Math.Min(MaxSpeed, Factor * (float)Math.Pow(net, Power)) : 0f;
            // One pawn of the team pulling always gets it somewhere (a lone holder never stops it for good).
            return pull >= 1f ? Math.Max(MinSpeed, v) : v;
        }
    }

    // J, the knight's road (R129, replacing the spec's 25 islands and aimed launches): floor only on board squares
    // that follow one another by a knight's move, from d1 at the entrance to f8 by the gate; water everywhere
    // else. A pad in the middle of every square only jumps higher (no direction): the pawn runs the way it wants,
    // jumps on the pad and dives to reach the next square. Enemy pieces stand on six of the squares; a dive (or a
    // pad jump) onto one takes it. All six: the gate opens.
    public static class KnightRoute
    {
        public static readonly string[] Squares = { "d1", "b2", "a4", "c5", "e4", "g5", "h7", "f8" };
        // The pieces to take: on the squares between the first and the last, and what they are.
        public static readonly string[] PieceSquares = { "b2", "a4", "c5", "e4", "g5", "h7" };
        public static readonly PieceKind[] PieceKinds = { PieceKind.Pawn, PieceKind.Bishop, PieceKind.Knight, PieceKind.Rook, PieceKind.Queen, PieceKind.King };
        // The pad's jump: straight up this fast, the pawn's own run carried along (about 1.4 s in the air).
        public const float PadRadius = .8f, PadUp = 7f, Reach = .9f, Speed = 4.5f, JumpCatch = 2f, SquareSize = 2.05f;

        // Every square a knight's move from the one before.
        public static bool Joined()
        {
            for (int i = 1; i < Squares.Length; i++)
            {
                PawnRushBoard.Parse(Squares[i - 1], out int f0, out int r0);
                PawnRushBoard.Parse(Squares[i], out int f1, out int r1);
                int df = Math.Abs(f1 - f0), dr = Math.Abs(r1 - r0);
                if (!(df == 1 && dr == 2 || df == 2 && dr == 1)) return false;
            }
            return true;
        }

        public static StationPoint Center(string square)
        {
            PawnRushBoard.Parse(square, out int f, out int r);
            return PawnRushBoard.Center(f, r);
        }

        // The route square a point stands on (within its floor), or -1.
        public static int SquareAt(StationPoint p)
        {
            for (int i = 0; i < Squares.Length; i++)
            {
                var c = Center(Squares[i]);
                if (Math.Abs(p.V - c.V) <= SquareSize * .5f && Math.Abs(p.U - c.U) <= SquareSize * .5f) return i;
            }
            return -1;
        }

        // A take: our pawn's hips within reach of the piece, diving, fast, or still in the air from a pad jump.
        public static bool Takes(float flatDistance, bool diving, float flatSpeed, bool padJump) =>
            flatDistance <= Reach && (diving || flatSpeed >= Speed || padJump);
    }

    // G, the king escort (spec §5): the sled's road of fifteen board squares to the gate, as one distance s.
    public static class KingEscortPath
    {
        public static readonly string[] Cells = { "d1", "d2", "d3", "c3", "b3", "b4", "b5", "b6", "c6", "d6", "e6", "f6", "f7", "f8", "e8" };
        public const float GateU = 20.3f;
        public static readonly string[] Sand = { "b4", "b5", "d6", "e6" };
        // The two rooks in the way: the cell they stand on and the garage they are dragged into.
        public static readonly string[] RookCells = { "c6", "f7" };
        public static readonly string[] RookGarages = { "c7", "g7" };
        public const float RookStopShort = 2.05f, RookDrag = 2.25f, TurnSeconds = .4f;
        public static readonly float[] Flat = { 0f, .62f, .85f, 1f, 1.1f };
        public static readonly float[] SandSpeed = { 0f, .22f, .42f, .6f, .72f };
        public const float RookDragOne = .6f, RookDragTwo = 1.1f;

        static StationPoint[] points;
        static float[] along;

        // The path's corners: every cell's middle, then the gate.
        public static StationPoint[] Points
        {
            get
            {
                if (points != null) return points;
                var list = new List<StationPoint>();
                foreach (var c in Cells)
                {
                    PawnRushBoard.Parse(c, out int f, out int r);
                    list.Add(PawnRushBoard.Center(f, r));
                }
                var last = list[list.Count - 1];
                list.Add(new StationPoint(last.V, GateU));
                points = list.ToArray();
                along = new float[points.Length];
                for (int i = 1; i < points.Length; i++) along[i] = along[i - 1] + points[i].DistanceTo(points[i - 1]);
                return points;
            }
        }

        public static float Length { get { var p = Points; return along[p.Length - 1]; } }

        // s of the middle of cell i (and of the gate, i = Cells.Length).
        public static float At(int i) { var p = Points; return along[i]; }

        public static int CellIndex(string name) => Array.IndexOf(Cells, name);

        // The point at s, and which way the road goes there (unit, v and u).
        public static StationPoint Position(float s, out float dirV, out float dirU)
        {
            var p = Points;
            s = Math.Max(0f, Math.Min(Length, s));
            int i = 0;
            while (i < p.Length - 2 && along[i + 1] <= s) i++;
            float seg = along[i + 1] - along[i];
            float t = seg > 0f ? (s - along[i]) / seg : 0f;
            dirV = (p[i + 1].V - p[i].V) / seg;
            dirU = (p[i + 1].U - p[i].U) / seg;
            return new StationPoint(p[i].V + (p[i + 1].V - p[i].V) * t, p[i].U + (p[i + 1].U - p[i].U) * t);
        }

        // A turntable cell: the road bends there (and at the last cell, into the gate).
        public static bool Turns(int i)
        {
            var p = Points;
            if (i <= 0 || i >= p.Length - 1) return false;
            float a1 = p[i].V - p[i - 1].V, b1 = p[i].U - p[i - 1].U, a2 = p[i + 1].V - p[i].V, b2 = p[i + 1].U - p[i].U;
            return Math.Abs(a1 * b2 - b1 * a2) > 1e-3f;
        }

        public static bool IsSand(float s)
        {
            int i = Nearest(s);
            return i < Cells.Length && Array.IndexOf(Sand, Cells[i]) >= 0;
        }

        public static int Nearest(float s)
        {
            var p = Points;
            int best = 0;
            for (int i = 1; i < p.Length; i++) if (Math.Abs(along[i] - s) < Math.Abs(along[best] - s)) best = i;
            return best;
        }

        // The sled's speed for this many pulling (effective, fractional) on flat or sand.
        public static float Speed(float effective, bool sand)
        {
            var table = sand ? SandSpeed : Flat;
            effective = Math.Max(0f, Math.Min(4f, effective));
            int lo = (int)Math.Floor(effective);
            if (lo >= 4) return table[4];
            return table[lo] + (table[lo + 1] - table[lo]) * (effective - lo);
        }

        // Where the sled has to stop for rook k while it is still in the way.
        public static float RookStop(int k) => At(CellIndex(RookCells[k])) - RookStopShort;

        // Every next cell a king's step from the last (no jumps in the road).
        public static bool Connected()
        {
            for (int i = 1; i < Cells.Length; i++)
            {
                PawnRushBoard.Parse(Cells[i - 1], out int f0, out int r0);
                PawnRushBoard.Parse(Cells[i], out int f1, out int r1);
                if (Math.Abs(f1 - f0) + Math.Abs(r1 - r0) != 1) return false;
            }
            return true;
        }
    }
}
