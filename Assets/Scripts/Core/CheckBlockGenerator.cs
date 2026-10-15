using System;
using System.Collections.Generic;

namespace ChessFight.Network
{
    // I, block the check (spec §7.2): a seed becomes a position. Our king on rank 7 (c..f) is in check from two
    // or three sliding pieces whose lines are blocked by pushing a shield cart along a rail; maybe a knight to be
    // dragged off or a pawn to be knocked down; maybe a hidden double check that rises once everything else is
    // solved and is answered by dragging the king to a safe square. The host sends only the seed; every PC builds
    // the same position from it.
    //
    // Board space (PawnRushBoard): files 0..7, ranks 1..8. Everything stands on a square's middle.
    public static class CheckBlockGenerator
    {
        public enum Kind : byte { Rail = 0, Knight = 1, Pawn = 2 }

        public struct Square
        {
            public int File, Rank;
            public Square(int f, int r) { File = f; Rank = r; }
            public bool Is(int f, int r) => File == f && Rank == r;
            public override string ToString() => PawnRushBoard.Name(File, Rank);
        }

        public sealed class Situation
        {
            public Kind Kind;
            public PieceKind Attacker;
            public Square From;          // the attacker's square
            public int DirF, DirR;       // from the king toward the attacker (sliders)
            public Square Block;         // rail: the square to fill
            public Square RailStart;     // rail: where the cart starts (two squares off the line)
            public Square RailMid;       // rail: the square between
            public PieceKind Shield;     // rail: the piece on the cart
            public float RailLength;     // metres
            public float SoloSpeed;      // m/s for one pawn pushing
            public float Points;         // the budget's points
        }

        public sealed class Layout
        {
            public int Seed, Attempt;
            public Square King;
            public readonly List<Situation> Situations = new List<Situation>();
            // The hidden double check: a rook down the king's file from rank 1 and a bishop at the end of the free
            // diagonal; the king is dragged to Escape.
            public bool Double;
            public Square DoubleRook, DoubleBishop, Escape;
            public float Points;

            // Every square something stands on or a line crosses, for the overlap checks.
            // The squares a rail situation's attack line crosses, from the king (exclusive) to the attacker (exclusive).
            public IEnumerable<Square> LineSquares(Situation s)
            {
                if (s.Kind != Kind.Rail) yield break;
                int f = King.File + s.DirF, r = King.Rank + s.DirR;
                while (!(f == s.From.File && r == s.From.Rank))
                {
                    yield return new Square(f, r);
                    f += s.DirF;
                    r += s.DirR;
                }
            }
        }

        public const int MaxAttempts = 600;
        public const float MinPoints = 55f, MaxPoints = 125f;
        public const float KnightPoints = 14f, PawnPoints = 8f, DoublePoints = 19f, RailBonus = 6f;

        // Directions from the king: south, west, east (rook lines), south-west, south-east (bishop lines).
        static readonly int[] DirF = { 0, -1, 1, -1, 1 };
        static readonly int[] DirR = { -1, 0, 0, -1, -1 };
        const int South = 0, West = 1, East = 2, SouthWest = 3, SouthEast = 4;

        public static float SoloSpeed(PieceKind shield)
        {
            switch (shield)
            {
                case PieceKind.Pawn: return .3f;
                case PieceKind.Knight:
                case PieceKind.Bishop: return .22f;
                case PieceKind.Queen: return .18f;
                default: return .15f;   // the rook
            }
        }

        public static Layout Generate(int seed)
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var layout = TryGenerate(seed, attempt);
                if (layout != null) return layout;
            }
            throw new InvalidOperationException("CheckBlockGenerator: no position for seed " + seed);
        }

        static Layout TryGenerate(int seed, int attempt)
        {
            var rng = new Random(unchecked(seed * 7919 + attempt * 104729 + 17));
            var layout = new Layout { Seed = seed, Attempt = attempt };
            layout.King = new Square(2 + rng.Next(4), 7);   // c7..f7
            var used = new bool[5];
            var taken = new HashSet<int>();   // squares something stands or a rail runs on
            var lines = new HashSet<int>();   // squares an attack line crosses
            taken.Add(Key(layout.King));

            int rails = rng.NextDouble() < .55 ? 2 : 3;
            for (int i = 0; i < rails; i++)
            {
                var s = Rail(rng, layout, used, taken, lines);
                if (s == null) return null;
                layout.Situations.Add(s);
            }

            double extra = rng.NextDouble();
            if (extra < .30)
            {
                var s = Knight(rng, layout, taken, lines);
                if (s == null) return null;
                layout.Situations.Add(s);
            }
            else if (extra < .55)
            {
                var s = Pawn(rng, layout, taken, lines);
                if (s == null) return null;
                layout.Situations.Add(s);
            }

            if (rng.NextDouble() < .40 && !used[South] && (!used[SouthWest] || !used[SouthEast]))
                if (!Double(rng, layout, used, taken, lines)) return null;

            float points = 0f;
            foreach (var s in layout.Situations) points += s.Points;
            if (layout.Double) points += DoublePoints;
            layout.Points = points;
            return points >= MinPoints && points <= MaxPoints ? layout : null;
        }

        static int Key(Square s) => PawnRushBoard.Index(s.File, s.Rank);
        static int Key(int f, int r) => PawnRushBoard.Index(f, r);

        static Situation Rail(Random rng, Layout layout, bool[] used, HashSet<int> taken, HashSet<int> lines)
        {
            double roll = rng.NextDouble();
            var attacker = roll < .4 ? PieceKind.Rook : roll < .8 ? PieceKind.Bishop : PieceKind.Queen;
            var dirs = new List<int>();
            if (attacker != PieceKind.Bishop) { dirs.Add(South); dirs.Add(West); dirs.Add(East); }
            if (attacker != PieceKind.Rook) { dirs.Add(SouthWest); dirs.Add(SouthEast); }
            dirs.RemoveAll(d => used[d]);
            if (dirs.Count == 0) return null;
            int dir = dirs[rng.Next(dirs.Count)];
            int df = DirF[dir], dr = DirR[dir];
            var king = layout.King;

            // How far it can stand: three squares or more, to the board's edge.
            int reach = 0;
            while (PawnRushBoard.OnBoard(king.File + df * (reach + 1), king.Rank + dr * (reach + 1))) reach++;
            if (reach < 3) return null;
            int distance = 3 + rng.Next(reach - 2);
            var from = new Square(king.File + df * distance, king.Rank + dr * distance);
            if (taken.Contains(Key(from)) || lines.Contains(Key(from))) return null;

            // The line's squares: none may already hold anything.
            var line = new List<Square>();
            for (int k = 1; k < distance; k++) line.Add(new Square(king.File + df * k, king.Rank + dr * k));
            foreach (var sq in line) if (taken.Contains(Key(sq))) return null;

            // Block squares: any between, the one next to the king last.
            var order = new List<Square>();
            for (int k = 2; k < distance; k++) order.Add(line[k - 1]);
            Shuffle(rng, order);
            order.Add(line[0]);

            bool diagonal = df != 0 && dr != 0;
            // Perpendicular to the line: across for a file or rank line, the other diagonal for a diagonal.
            int pf = diagonal ? df : dr, pr = diagonal ? -dr : df;
            if (!diagonal) { pf = dr != 0 ? 1 : 0; pr = dr != 0 ? 0 : 1; }
            var sides = new List<int> { 1, -1 };
            Shuffle(rng, sides);

            // The line's own squares count against the rails from here on.
            var lineKeys = new HashSet<int>(lines);
            foreach (var sq in line) lineKeys.Add(Key(sq));

            foreach (var block in order)
                foreach (int side in sides)
                {
                    var mid = new Square(block.File + pf * side, block.Rank + pr * side);
                    var start = new Square(block.File + pf * side * 2, block.Rank + pr * side * 2);
                    if (!PawnRushBoard.OnBoard(mid.File, mid.Rank) || !PawnRushBoard.OnBoard(start.File, start.Rank)) continue;
                    if (taken.Contains(Key(mid)) || taken.Contains(Key(start)) || taken.Contains(Key(block))) continue;
                    if (lineKeys.Contains(Key(mid)) || lineKeys.Contains(Key(start))) continue;
                    // The cart moves from start toward the block: (-pf, -pr) x side.
                    int mf = -pf * side, mr = -pr * side;
                    var shield = Shield(rng, attacker, diagonal, mr > 0 && mf == 0);
                    var s = new Situation
                    {
                        Kind = Kind.Rail, Attacker = attacker, From = from, DirF = df, DirR = dr, Block = block,
                        RailStart = start, RailMid = mid, Shield = shield,
                        RailLength = diagonal ? 2f * PawnRushBoard.Cell * 1.41421356f : 2f * PawnRushBoard.Cell,
                        SoloSpeed = SoloSpeed(shield),
                    };
                    s.Points = s.RailLength / s.SoloSpeed + RailBonus;
                    used[dir] = true;
                    taken.Add(Key(from));
                    taken.Add(Key(mid));
                    taken.Add(Key(start));
                    taken.Add(Key(block));
                    foreach (var sq in line) lines.Add(Key(sq));
                    lines.Add(Key(from));
                    return s;
                }
            return null;
        }

        // The cart's piece moves the rail's way (§7.2.3): rook or queen across a file or rank (a pawn too, pushed
        // north only), bishop or queen on a diagonal; a queen's thick line needs a rook (straight) or a queen.
        static PieceKind Shield(Random rng, PieceKind attacker, bool diagonal, bool north)
        {
            if (attacker == PieceKind.Queen) return diagonal ? PieceKind.Queen : PieceKind.Rook;
            if (diagonal) return rng.NextDouble() < .5 ? PieceKind.Bishop : PieceKind.Queen;
            var options = new List<PieceKind> { PieceKind.Rook, PieceKind.Queen };
            if (north) options.Add(PieceKind.Pawn);
            return options[rng.Next(options.Count)];
        }

        static Situation Knight(Random rng, Layout layout, HashSet<int> taken, HashSet<int> lines)
        {
            var king = layout.King;
            var spots = new List<Square>
            {
                new Square(king.File - 1, king.Rank - 2), new Square(king.File + 1, king.Rank - 2),
                new Square(king.File - 2, king.Rank - 1), new Square(king.File + 2, king.Rank - 1),
            };
            spots.RemoveAll(s => !PawnRushBoard.OnBoard(s.File, s.Rank) || taken.Contains(Key(s)) || lines.Contains(Key(s)));
            if (spots.Count == 0) return null;
            var at = spots[rng.Next(spots.Count)];
            taken.Add(Key(at));
            return new Situation { Kind = Kind.Knight, Attacker = PieceKind.Knight, From = at, Points = KnightPoints };
        }

        static Situation Pawn(Random rng, Layout layout, HashSet<int> taken, HashSet<int> lines)
        {
            var king = layout.King;
            var spots = new List<Square> { new Square(king.File - 1, 8), new Square(king.File + 1, 8) };
            spots.RemoveAll(s => !PawnRushBoard.OnBoard(s.File, s.Rank) || taken.Contains(Key(s)) || lines.Contains(Key(s)));
            if (spots.Count == 0) return null;
            var at = spots[rng.Next(spots.Count)];
            taken.Add(Key(at));
            return new Situation { Kind = Kind.Pawn, Attacker = PieceKind.Pawn, From = at, Points = PawnPoints };
        }

        // The double check (§7.2.5): a rook on the king's file at rank 1 and a bishop at the end of the free
        // diagonal, both lines clear of everything; the king's escape square on neither line.
        static bool Double(Random rng, Layout layout, bool[] used, HashSet<int> taken, HashSet<int> lines)
        {
            var king = layout.King;
            int diag = !used[SouthWest] && !used[SouthEast] ? (rng.NextDouble() < .5 ? SouthWest : SouthEast) : !used[SouthWest] ? SouthWest : SouthEast;
            var rook = new Square(king.File, 1);
            var newLines = new HashSet<int>();
            for (int r = king.Rank - 1; r >= 1; r--) newLines.Add(Key(king.File, r));
            int f = king.File, rr = king.Rank;
            while (PawnRushBoard.OnBoard(f + DirF[diag], rr + DirR[diag])) { f += DirF[diag]; rr += DirR[diag]; newLines.Add(Key(f, rr)); }
            var bishop = new Square(f, rr);
            if (bishop.File == king.File && bishop.Rank == king.Rank) return false;
            // It rises once everything else is solved: the carts then stand on their block squares and the rails
            // are empty, the knight has been dragged off and the pawn knocked down. So only the attackers and the
            // placed shields can be in its way. A rolled double check that still cannot stand is a failed attempt
            // (not a missing double check: otherwise crowded positions would almost never get one).
            var standing = new HashSet<int>();
            foreach (var s in layout.Situations)
                if (s.Kind == Kind.Rail) { standing.Add(Key(s.From)); standing.Add(Key(s.Block)); }
            foreach (int k in newLines) if (standing.Contains(k)) return false;
            // Escape: a neighbour on neither new line (rank 8 first), free of everything.
            var upper = new List<Square>();
            var other = new List<Square>();
            for (int df = -1; df <= 1; df++)
                for (int dr = -1; dr <= 1; dr++)
                {
                    if (df == 0 && dr == 0) continue;
                    var s = new Square(king.File + df, king.Rank + dr);
                    if (!PawnRushBoard.OnBoard(s.File, s.Rank) || newLines.Contains(Key(s)) || standing.Contains(Key(s)) || lines.Contains(Key(s))) continue;
                    if (OnDiagonalFrom(bishop, s) || s.File == rook.File) continue;
                    (dr > 0 ? upper : other).Add(s);
                }
            var pick = upper.Count > 0 ? upper : other;
            if (pick.Count == 0) return false;
            layout.Double = true;
            layout.DoubleRook = rook;
            layout.DoubleBishop = bishop;
            layout.Escape = pick[rng.Next(pick.Count)];
            foreach (int k in newLines) lines.Add(k);
            taken.Add(Key(layout.Escape));
            return true;
        }

        // Would the bishop on `from` attack `s` on the same diagonal (ignoring blockers)?
        static bool OnDiagonalFrom(Square from, Square s) => Math.Abs(from.File - s.File) == Math.Abs(from.Rank - s.Rank) && from.File != s.File;

        static void Shuffle<T>(Random rng, List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }
    }
}
