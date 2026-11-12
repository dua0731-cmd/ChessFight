using System;
using System.Collections.Generic;

namespace ChessFight.Network
{
    // The rules of the Pawn Rush mini-games v1.0 (Docs/PawnRush/PAWN_RUSH_MINIGAMES_v1.md) that need no Unity:
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
    //
    // R543: sixteen is five a side's field (the statics here: EnPassantField.Baseline); the number of statues goes with
    // the team's size (EnPassantField.For), the cycle and the window stay.
    public static class EnPassantRules
    {
        public const int Statues = 16;
        public const float Period = 14f, Advance = 2.2f, Dash = .3f, WindowEnd = 1.5f, ReturnEnd = 4.5f, Warn = 13.5f;
        public const float Grace = .15f, Reach = .8f, Speed = 5f, Bounce = 2f, Repeat = .3f;
        public static readonly float[] Columns = { -6.75f, -2.25f, 2.25f, 6.75f };
        public static readonly float[] Homes = { 6f, 10.4f, 14.8f, 19.2f };

        // i row-first: south row first, west column first.
        public static StationPoint Home(int i) => EnPassantField.Baseline.Home(i);

        public static float Offset(int i) => EnPassantField.Baseline.Offset(i);

        public static float Phase(double time, int i) => EnPassantField.Baseline.Phase(time, i);

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

        public static StationPoint Position(double time, int i) => EnPassantField.Baseline.Position(time, i);

        // A capture: our pawn within reach (flat), diving or fast, while the window is open.
        public static bool Captures(float flatDistance, bool diving, float flatSpeed, float ph) =>
            flatDistance <= Reach && (diving || flatSpeed >= Speed) && InWindow(ph, Grace);
    }

    // F's statues for a team size (R543, user 11-11: "앙파상 사냥은 사냥감의 개수를 조절해줘"): ten, thirteen, sixteen or
    // nineteen, in rows of four from the south (a last row with fewer stands in the middle); five rows stand 3.3 m
    // apart instead of 4.4 m (a statue's lane is 3.3 m). Their steps are spread over the same 14 s cycle.
    public sealed class EnPassantField
    {
        public const int MaxStatues = 20;
        public static readonly EnPassantField Baseline = new EnPassantField(EnPassantRules.Statues);

        public static EnPassantField For(int teamSize)
        {
            int n = MiniGameScale.EnPassantStatues(teamSize);
            return n == EnPassantRules.Statues ? Baseline : new EnPassantField(n);
        }

        public readonly int Count, Rows;
        readonly int step;

        public EnPassantField(int count)
        {
            if (count < 1 || count > MaxStatues) throw new ArgumentOutOfRangeException(nameof(count));
            Count = count;
            Rows = (count + 3) / 4;
            // The stagger's step: 7 as before where it shares nothing with the count, else the next that does not.
            step = 7;
            while (Gcd(step, count) != 1) step++;
        }

        static int Gcd(int a, int b) { while (b != 0) { int t = a % b; a = b; b = t; } return a; }

        // The row's home line: up to four rows on the lines there always were, five rows closer together.
        public float HomeU(int row) => Rows <= 4 ? EnPassantRules.Homes[row] : EnPassantRules.Homes[0] + row * (EnPassantRules.Homes[3] - EnPassantRules.Homes[0]) / (Rows - 1);

        // i row-first: south row first, west column first. A last row of fewer than four stands in the middle.
        public StationPoint Home(int i)
        {
            int row = i / 4, inRow = Math.Min(4, Count - row * 4), k = i % 4;
            float v = inRow == 4 ? EnPassantRules.Columns[k] : (k - (inRow - 1) * .5f) * 4.5f;
            return new StationPoint(v, HomeU(row));
        }

        public float Offset(int i) => ((i * step) % Count) / (float)Count * EnPassantRules.Period;

        public float Phase(double time, int i)
        {
            double ph = (time + Offset(i)) % EnPassantRules.Period;
            if (ph < 0) ph += EnPassantRules.Period;
            return (float)ph;
        }

        public StationPoint Position(double time, int i)
        {
            var home = Home(i);
            return new StationPoint(home.V, home.U - EnPassantRules.Displacement(Phase(time, i)));
        }
    }

    // C, the drawbridge rope (spec §9): one heavy rope, pulled 7 m in all.
    public static class DrawbridgeRope
    {
        public const float Length = 7f, SprintForce = 1.8f, HoldForce = .5f, MaxSpeed = .35f, Factor = .045f, Power = 1.35f, MinSpeed = .04f;

        // pullers: the team's pawns holding a knot and walking away from the drum (sprinters count 1.8);
        // holders: the other team's pawns holding a knot and walking toward it (0.5 each).
        // R543 (user 11-11: "도개교는 당겨지는 힘(진척도가 올라가는 정도)를 조절해줘"): `worth` is what one pawn's pull or hold
        // counts for (MiniGameScale.RopeForce: 5 / the team's size, 1 as it was), so a small team's rope is lighter and
        // a big team's heavier. The rope never runs faster than MaxSpeed whatever the team.
        public static float Speed(float pull, float hold, float worth = 1f)
        {
            float net = (pull - hold) * worth;
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

    // J's road for a team size (R543, user 11-11: "나이트의 길은 돌 수반의 개수와 사이즈를 조절해줘"): six, seven, eight or
    // nine squares, a piece to take on every one but the first and the last, and a square 1.9 to 2.2 m wide (a bigger
    // team needs the room to stand). Every road starts on d1 at the entrance and ends by the gate - on f8, or on e8
    // where the count is odd (a knight changes its square's colour with every move). No two squares of a road touch,
    // not even at a corner (Spread): the only way from one to the next is the jump.
    public sealed class KnightRoad
    {
        public static readonly KnightRoad Baseline = new KnightRoad(KnightRoute.Squares, KnightRoute.PieceKinds, KnightRoute.SquareSize);

        static readonly string[] Six = { "d1", "b2", "a4", "b6", "d7", "f8" };
        static readonly string[] Seven = { "d1", "b2", "a4", "c5", "e4", "f6", "e8" };
        static readonly string[] Nine = { "d1", "b2", "a4", "c5", "e4", "g3", "h5", "g7", "e8" };
        static readonly PieceKind[] FourKinds = { PieceKind.Pawn, PieceKind.Knight, PieceKind.Rook, PieceKind.King };
        static readonly PieceKind[] FiveKinds = { PieceKind.Pawn, PieceKind.Bishop, PieceKind.Knight, PieceKind.Rook, PieceKind.King };
        static readonly PieceKind[] SevenKinds = { PieceKind.Pawn, PieceKind.Bishop, PieceKind.Knight, PieceKind.Pawn, PieceKind.Rook, PieceKind.Queen, PieceKind.King };

        public static KnightRoad For(int teamSize)
        {
            float size = MiniGameScale.KnightSquareSize(teamSize);
            switch (MiniGameScale.KnightSquares(teamSize))
            {
                case 6: return new KnightRoad(Six, FourKinds, size);
                case 7: return new KnightRoad(Seven, FiveKinds, size);
                case 9: return new KnightRoad(Nine, SevenKinds, size);
                default: return Baseline;
            }
        }

        public readonly string[] Squares, PieceSquares;
        public readonly PieceKind[] PieceKinds;
        public readonly float SquareSize;

        public KnightRoad(string[] squares, PieceKind[] kinds, float squareSize)
        {
            if (squares == null || squares.Length < 3 || kinds == null || kinds.Length != squares.Length - 2 || kinds.Length > 8)
                throw new ArgumentException("A knight's road has a piece on every square but its first and its last (eight at most).");
            Squares = squares;
            PieceKinds = kinds;
            SquareSize = squareSize;
            PieceSquares = new string[squares.Length - 2];
            Array.Copy(squares, 1, PieceSquares, 0, PieceSquares.Length);
        }

        public int Pieces => PieceSquares.Length;

        // Every square a knight's move from the one before.
        public bool Joined()
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

        // No two squares side by side or corner to corner, only the first on rank 1 (the entrance floor's edge) and only
        // the last on rank 8 in front of the gate strip (files c..f): nothing to walk across.
        public bool Spread()
        {
            for (int i = 0; i < Squares.Length; i++)
            {
                if (!PawnRushBoard.Parse(Squares[i], out int f0, out int r0)) return false;
                if (i > 0 && r0 == 1) return false;
                if (i < Squares.Length - 1 && r0 == 8 && f0 >= 2 && f0 <= 5) return false;
                for (int k = i + 1; k < Squares.Length; k++)
                {
                    PawnRushBoard.Parse(Squares[k], out int f1, out int r1);
                    if (Math.Max(Math.Abs(f1 - f0), Math.Abs(r1 - r0)) < 2) return false;
                }
            }
            return true;
        }

        public StationPoint Center(int i) => KnightRoute.Center(Squares[i]);

        // The road square a point stands on (within its floor), or -1.
        public int SquareAt(StationPoint p)
        {
            for (int i = 0; i < Squares.Length; i++)
            {
                var c = Center(i);
                if (Math.Abs(p.V - c.V) <= SquareSize * .5f && Math.Abs(p.U - c.U) <= SquareSize * .5f) return i;
            }
            return -1;
        }
    }

    // G, the king escort (spec §5): the sled's road of board squares, d1 to e8 in front of the team gate, as one distance
    // s. R524 (user 11-09, the list's 6: "룩이 있는 그 라인 후에 출구 벽 속까지 밀어야 되는 현상"): the road ends on e8's middle,
    // the 1.6 m sled whole on the board in front of the gate. Before, a last leg ran on north from e8 to u 20.3, past the
    // board's north edge (u 20): the sled was pushed 1.1 m into the team door's tunnel, into the north wall's depth and
    // over the tunnel floor 0.4 m below the board.
    //
    // R543: the road is a KingRoad, drawn anew for every game (KingRoad.Generate). These statics are the one road there
    // was until then (KingRoad.Baseline, fifteen squares) and the numbers every road shares.
    public static class KingEscortPath
    {
        public static readonly string[] Cells = { "d1", "d2", "d3", "c3", "b3", "b4", "b5", "b6", "c6", "d6", "e6", "f6", "f7", "f8", "e8" };
        public static readonly string[] Sand = { "b4", "b5", "d6", "e6" };
        // The two rooks in the way: the cell they stand on and the garage they are dragged into.
        public static readonly string[] RookCells = { "c6", "f7" };
        public static readonly string[] RookGarages = { "c7", "g7" };
        public const float RookStopShort = 2.05f, RookDrag = 2.25f, TurnSeconds = .4f;
        public static readonly float[] Flat = { 0f, .62f, .85f, 1f, 1.1f };
        public static readonly float[] SandSpeed = { 0f, .22f, .42f, .6f, .72f };
        public const float RookDragOne = .6f, RookDragTwo = 1.1f;

        public static StationPoint[] Points => KingRoad.Baseline.Points;
        public static float Length => KingRoad.Baseline.Length;
        public static float At(int i) => KingRoad.Baseline.At(i);
        public static int CellIndex(string name) => KingRoad.Baseline.CellIndex(name);
        public static StationPoint Position(float s, out float dirV, out float dirU) => KingRoad.Baseline.Position(s, out dirV, out dirU);
        public static bool Turns(int i) => KingRoad.Baseline.Turns(i);
        public static bool IsSand(float s) => KingRoad.Baseline.IsSand(s);
        public static int Nearest(float s) => KingRoad.Baseline.Nearest(s);
        public static float RookStop(int k) => KingRoad.Baseline.RookStop(k);
        public static bool Connected() => KingRoad.Baseline.Connected();

        // The sled's speed for this many pulling (effective, fractional) on flat or sand.
        public static float Speed(float effective, bool sand)
        {
            var table = sand ? SandSpeed : Flat;
            effective = Math.Max(0f, Math.Min(4f, effective));
            int lo = (int)Math.Floor(effective);
            if (lo >= 4) return table[4];
            return table[lo] + (table[lo + 1] - table[lo]) * (effective - lo);
        }

        // R543 (user 11-11: "킹이 움직이는 속도(무게)"): the sled's weight goes with the team. `crew` pushing move it at
        // its full speed (MiniGameScale.KingSledCrew: four as before, three of three, five of six); fewer move it as
        // that share of four did. Each of the other team holding it back takes half a pusher off, as before.
        public static float Speed(int pushers, int holders, bool sand, int crew)
        {
            crew = Math.Max(1, crew);
            float effective = Math.Max(0f, Math.Min(crew, pushers) - .5f * holders) * 4f / crew;
            return Speed(effective, sand);
        }
    }

    // One king's road (R543, user 11-11: "킹 호위는 ... 킹이 움직이는 속도(무게)와 길의 거리, 장애물 등을 적절히 조절해줘. 그리고
    // 매번 똑같은 패턴의 맵이 안나오게 해줘"): the squares the sled is pushed over, which of them are sand, and the rooks
    // standing in it with the garage each is pushed into. Generate draws one from a seed for a team size - 11, 13, 15
    // or 17 squares with 2 to 5 of sand and 1 to 3 rooks (MiniGameScale) - the same road from the same seed on every PC.
    //
    // What every road keeps: it starts on d1 going north and ends on e8 in front of the team gate; no square of it lies
    // beside an earlier one (the road never runs along itself, so its curbs and the 1.6 m sled have room); a rook
    // stands on a straight square and its garage is the free square beside it, touching nothing else of the road; and
    // road and garages keep to files b..g, as the old road did - the hall's wall columns stand along file a's side and
    // its rope posts along file h's (the garages are arched alcoves 2.6 m wide), and a garage keeps off rank 1, the way in.
    public sealed class KingRoad
    {
        public static readonly KingRoad Baseline =
            new KingRoad(KingEscortPath.Cells, KingEscortPath.Sand, KingEscortPath.RookCells, KingEscortPath.RookGarages);

        public const string Start = "d1", End = "e8";
        public const int MaxRooks = 3, MaxAttempts = 400;
        // Files b..g.
        public const int FirstFile = 1, LastFile = 6;

        public readonly string[] Cells, Sand, RookCells, RookGarages;
        readonly StationPoint[] points;
        readonly float[] along;
        readonly bool[] sandy;
        readonly int[] rookIndex;

        public KingRoad(string[] cells, string[] sand, string[] rookCells, string[] rookGarages)
        {
            if (cells == null || cells.Length < 3) throw new ArgumentException("A king's road has three squares or more.");
            Cells = cells;
            Sand = sand ?? new string[0];
            RookCells = rookCells ?? new string[0];
            RookGarages = rookGarages ?? new string[0];
            if (RookCells.Length != RookGarages.Length || RookCells.Length > MaxRooks) throw new ArgumentException("A garage for each rook, three rooks at most.");
            points = new StationPoint[cells.Length];
            along = new float[cells.Length];
            sandy = new bool[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                if (!PawnRushBoard.Parse(cells[i], out int f, out int r)) throw new ArgumentException("Not a board square: " + cells[i]);
                points[i] = PawnRushBoard.Center(f, r);
                if (i > 0) along[i] = along[i - 1] + points[i].DistanceTo(points[i - 1]);
                sandy[i] = Array.IndexOf(Sand, cells[i]) >= 0;
            }
            rookIndex = new int[RookCells.Length];
            for (int k = 0; k < RookCells.Length; k++) rookIndex[k] = Array.IndexOf(cells, RookCells[k]);
        }

        public int Rooks => RookCells.Length;

        // The path's corners: every cell's middle; the last one's is the end.
        public StationPoint[] Points => points;

        public float Length => along[along.Length - 1];

        // s of the middle of cell i (the last cell's is Length, the end).
        public float At(int i) => along[i];

        public int CellIndex(string name) => Array.IndexOf(Cells, name);

        // The point at s, and which way the road goes there (unit, v and u).
        public StationPoint Position(float s, out float dirV, out float dirU)
        {
            var p = points;
            s = Math.Max(0f, Math.Min(Length, s));
            int i = 0;
            while (i < p.Length - 2 && along[i + 1] <= s) i++;
            float seg = along[i + 1] - along[i];
            float t = seg > 0f ? (s - along[i]) / seg : 0f;
            dirV = (p[i + 1].V - p[i].V) / seg;
            dirU = (p[i + 1].U - p[i].U) / seg;
            return new StationPoint(p[i].V + (p[i + 1].V - p[i].V) * t, p[i].U + (p[i + 1].U - p[i].U) * t);
        }

        // A turntable cell: the road bends there (never the first or the last cell).
        public bool Turns(int i)
        {
            var p = points;
            if (i <= 0 || i >= p.Length - 1) return false;
            float a1 = p[i].V - p[i - 1].V, b1 = p[i].U - p[i - 1].U, a2 = p[i + 1].V - p[i].V, b2 = p[i + 1].U - p[i].U;
            return Math.Abs(a1 * b2 - b1 * a2) > 1e-3f;
        }

        public int TurnCount { get { int n = 0; for (int i = 0; i < Cells.Length; i++) if (Turns(i)) n++; return n; } }

        public bool IsSand(float s) => sandy[Nearest(s)];
        public bool IsSandCell(int i) => i >= 0 && i < sandy.Length && sandy[i];

        public int Nearest(float s)
        {
            int best = 0;
            for (int i = 1; i < points.Length; i++) if (Math.Abs(along[i] - s) < Math.Abs(along[best] - s)) best = i;
            return best;
        }

        // Where the sled has to stop for rook k while it is still in the way.
        public float RookStop(int k) => At(rookIndex[k]) - KingEscortPath.RookStopShort;

        // Every next cell a king's step from the last (no jumps in the road).
        public bool Connected()
        {
            for (int i = 1; i < Cells.Length; i++)
            {
                PawnRushBoard.Parse(Cells[i - 1], out int f0, out int r0);
                PawnRushBoard.Parse(Cells[i], out int f1, out int r1);
                if (Math.Abs(f1 - f0) + Math.Abs(r1 - r0) != 1) return false;
            }
            return true;
        }

        // No square twice, and none beside a square of the road other than the one before and the one after it.
        public bool Apart()
        {
            for (int i = 0; i < Cells.Length; i++)
            {
                PawnRushBoard.Parse(Cells[i], out int f0, out int r0);
                for (int k = i + 1; k < Cells.Length; k++)
                {
                    PawnRushBoard.Parse(Cells[k], out int f1, out int r1);
                    int d = Math.Abs(f1 - f0) + Math.Abs(r1 - r0);
                    if (d == 0 || (d == 1 && k != i + 1)) return false;
                }
            }
            return true;
        }

        // Every rook on a straight square of the road (not its first three nor its last two), in the road's order and
        // three squares or more apart; every garage beside its rook across the road, on the board, off the road, and
        // beside no other square of it; no two garages the same.
        public bool RooksFit()
        {
            int last = -10;
            for (int k = 0; k < Rooks; k++)
            {
                int i = rookIndex[k];
                if (i < 3 || i > Cells.Length - 3 || Turns(i) || i - last < 3) return false;
                last = i;
                if (!PawnRushBoard.Parse(RookGarages[k], out int gf, out int gr) || Array.IndexOf(Cells, RookGarages[k]) >= 0) return false;
                if (gf < FirstFile || gf > LastFile || gr < 2) return false;
                PawnRushBoard.Parse(Cells[i], out int f, out int r);
                PawnRushBoard.Parse(Cells[i - 1], out int pf, out int pr);
                if (Math.Abs(gf - f) + Math.Abs(gr - r) != 1 || (gf - f) * (f - pf) + (gr - r) * (r - pr) != 0) return false;
                for (int c = 0; c < Cells.Length; c++)
                {
                    PawnRushBoard.Parse(Cells[c], out int cf, out int cr);
                    if (c != i && Math.Abs(cf - gf) + Math.Abs(cr - gr) == 1) return false;
                }
                for (int o = 0; o < k; o++) if (RookGarages[o] == RookGarages[k]) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ a road from a seed

        // The same road from the same seed and team size on every PC (SeededRandom, no system generator).
        public static KingRoad Generate(int seed, int teamSize)
        {
            int cells = MiniGameScale.KingRoadCells(teamSize), sand = MiniGameScale.KingRoadSand(teamSize), rooks = MiniGameScale.KingRoadRooks(teamSize);
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var rng = new SeededRandom(SeededSchedule.Hash(SeededSchedule.Hash(seed, cells), unchecked((uint)attempt)));
                var road = Try(ref rng, cells, sand, rooks);
                if (road != null) return road;
            }
            // Never met in the tests' seeds; the old road rather than no game.
            return Baseline;
        }

        static readonly int[] StepF = { 0, 1, 0, -1 }, StepR = { 1, 0, -1, 0 };   // north, east, south, west

        static KingRoad Try(ref SeededRandom rng, int count, int sandCount, int rookCount)
        {
            PawnRushBoard.Parse(Start, out int sf, out int sr);
            PawnRushBoard.Parse(End, out int ef, out int er);
            var path = new List<int> { PawnRushBoard.Index(sf, sr), PawnRushBoard.Index(sf, sr + 1) };   // d1, then north to d2
            var dirs = new List<int> { 0, 0 };                                                        // dirs[i]: the step onto square i
            int budget = 3000;
            int minTurns = 3, maxTurns = count / 3 + 1;
            if (!Walk(ref rng, path, dirs, count, PawnRushBoard.Index(ef, er), 0, minTurns, maxTurns, ref budget)) return null;

            var cells = new string[count];
            for (int i = 0; i < count; i++) cells[i] = PawnRushBoard.Name(PawnRushBoard.FileOf(path[i]), PawnRushBoard.RankOf(path[i]));

            // The rooks: straight squares in the road's middle, three or more apart, each with a free garage beside it.
            var rookAt = new List<int>();
            var garages = new List<int>();
            var candidates = new List<int>();
            for (int i = 3; i <= count - 3; i++) if (dirs[i] == dirs[i + 1]) candidates.Add(i);
            Shuffle(ref rng, candidates);
            foreach (int i in candidates)
            {
                if (rookAt.Count >= rookCount) break;
                bool near = false;
                foreach (int o in rookAt) if (Math.Abs(o - i) < 3) near = true;
                if (near) continue;
                int f = PawnRushBoard.FileOf(path[i]), r = PawnRushBoard.RankOf(path[i]);
                int side = rng.Next(2) == 0 ? 1 : 3;
                for (int s = 0; s < 2; s++, side = 4 - side)
                {
                    int d = (dirs[i] + side) % 4, gf = f + StepF[d], gr = r + StepR[d];
                    if (!PawnRushBoard.OnBoard(gf, gr) || gf < FirstFile || gf > LastFile || gr < 2) continue;
                    int g = PawnRushBoard.Index(gf, gr);
                    if (path.Contains(g) || garages.Contains(g) || Beside(path, gf, gr, path[i])) continue;
                    rookAt.Add(i);
                    garages.Add(g);
                    break;
                }
            }
            if (rookAt.Count < rookCount) return null;
            var order = new List<int>();
            for (int k = 0; k < rookAt.Count; k++) order.Add(k);
            order.Sort((a, b) => rookAt[a].CompareTo(rookAt[b]));
            var rookCells = new string[rookCount];
            var rookGarages = new string[rookCount];
            for (int k = 0; k < rookCount; k++)
            {
                rookCells[k] = cells[rookAt[order[k]]];
                int g = garages[order[k]];
                rookGarages[k] = PawnRushBoard.Name(PawnRushBoard.FileOf(g), PawnRushBoard.RankOf(g));
            }

            // The sand: squares of the road's middle that hold no rook, two in a row where they fit.
            var free = new List<int>();
            for (int i = 2; i < count - 2; i++) if (!rookAt.Contains(i)) free.Add(i);
            var sandAt = new List<int>();
            for (int tries = 0; tries < 60 && sandAt.Count < sandCount && free.Count > 0; tries++)
            {
                int i = free[rng.Next(free.Count)];
                if (sandAt.Count + 2 <= sandCount && free.Contains(i + 1) && tries < 40)
                {
                    sandAt.Add(i); sandAt.Add(i + 1);
                    free.Remove(i); free.Remove(i + 1);
                }
                else if (sandAt.Count + 1 == sandCount || tries >= 40)
                {
                    sandAt.Add(i);
                    free.Remove(i);
                }
            }
            if (sandAt.Count < sandCount) return null;
            sandAt.Sort();
            var sand = new string[sandCount];
            for (int k = 0; k < sandCount; k++) sand[k] = cells[sandAt[k]];
            return new KingRoad(cells, sand, rookCells, rookGarages);
        }

        // A random walk with backtracking from the path's last square to `end` in exactly `count` squares: never onto
        // rank 1 again, never beside an earlier square, turning `minTurns` to `maxTurns` times, and likelier straight on.
        static bool Walk(ref SeededRandom rng, List<int> path, List<int> dirs, int count, int end, int turns, int minTurns, int maxTurns, ref int budget)
        {
            int at = path[path.Count - 1];
            if (path.Count == count) return at == end && turns >= minTurns;
            if (--budget <= 0) return false;
            int f = PawnRushBoard.FileOf(at), r = PawnRushBoard.RankOf(at), came = dirs[dirs.Count - 1];
            int ef = PawnRushBoard.FileOf(end), er = PawnRushBoard.RankOf(end);
            var order = new List<int> { 0, 1, 2, 3 };
            Shuffle(ref rng, order);
            if (rng.Next(100) < 55) { order.Remove(came); order.Insert(0, came); }
            foreach (int d in order)
            {
                if ((d + 2) % 4 == came) continue;
                int nf = f + StepF[d], nr = r + StepR[d];
                if (!PawnRushBoard.OnBoard(nf, nr) || nr == 1 || nf < FirstFile || nf > LastFile) continue;
                int next = PawnRushBoard.Index(nf, nr), left = count - path.Count - 1, away = Math.Abs(ef - nf) + Math.Abs(er - nr);
                if (away > left || ((left - away) & 1) != 0) continue;
                if (next == end && left != 0) continue;
                int turned = turns + (d != came ? 1 : 0);
                if (turned > maxTurns) continue;
                if (path.Contains(next) || Beside(path, nf, nr, at)) continue;
                path.Add(next);
                dirs.Add(d);
                if (Walk(ref rng, path, dirs, count, end, turned, minTurns, maxTurns, ref budget)) return true;
                path.RemoveAt(path.Count - 1);
                dirs.RemoveAt(dirs.Count - 1);
                if (budget <= 0) return false;
            }
            return false;
        }

        // Is (f, r) beside a square of the path other than `except`.
        static bool Beside(List<int> path, int f, int r, int except)
        {
            foreach (int c in path)
                if (c != except && Math.Abs(PawnRushBoard.FileOf(c) - f) + Math.Abs(PawnRushBoard.RankOf(c) - r) == 1) return true;
            return false;
        }

        static void Shuffle(ref SeededRandom rng, List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }
    }
}
