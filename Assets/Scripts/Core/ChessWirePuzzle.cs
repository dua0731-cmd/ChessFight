using System;
using System.Collections.Generic;

namespace ChessFight.Network
{
    public sealed class ChessWirePuzzle
    {
        public const int Size = 8;
        public enum Kind { Rook, Bishop, Knight, Pawn }
        // Immutable board data; each team owns separate cutting state.
        public sealed class Layout
        {
            readonly int[] pieces, wires;
            readonly Kind[] kinds;
            public int PieceCount => pieces.Length;
            public int PawnDirection { get; }
            internal Layout(int[] pieces, Kind[] kinds, int[] wires, int pawnDirection = -1)
            { this.pieces = (int[])pieces.Clone(); this.kinds = (Kind[])kinds.Clone(); this.wires = (int[])wires.Clone(); PawnDirection = pawnDirection; }
            public int File(int i) => wires[i] % Size;
            public int Rank(int i) => wires[i] / Size;
            public int PieceFile(int i) => pieces[i] % Size;
            public int PieceRank(int i) => pieces[i] / Size;
            public Kind PieceKind(int i) => kinds[i];
            public bool IsBishop(int i) => kinds[i] == Kind.Bishop;
            public bool Occupied(int x, int y) => Array.IndexOf(pieces, y * Size + x) >= 0;
            public Layout MirrorRanks()
            {
                int[] p = new int[PieceCount], w = new int[3];
                for (int i = 0; i < p.Length; i++) p[i] = (7 - PieceRank(i)) * 8 + PieceFile(i);
                for (int i = 0; i < 3; i++) w[i] = (7 - Rank(i)) * 8 + File(i);
                return new Layout(p, kinds, w, -PawnDirection);
            }
            public bool Attacked(int x, int y)
            {
                if (x < 0 || x >= 8 || y < 0 || y >= 8) return false;
                if (Occupied(x, y)) return true;
                for (int i = 0; i < PieceCount; i++)
                {
                    int dx = x - PieceFile(i), dy = y - PieceRank(i);
                    if (kinds[i] == Kind.Pawn)
                    { if (Math.Abs(dx) == 1 && dy == PawnDirection) return true; continue; }
                    if (kinds[i] == Kind.Knight)
                    { if (Math.Abs(dx) * Math.Abs(dy) == 2) return true; continue; }
                    bool aligned = kinds[i] == Kind.Rook ? dx == 0 || dy == 0 : Math.Abs(dx) == Math.Abs(dy);
                    if (!aligned) continue;
                    bool blocked = false;
                    for (int step = 1; step < Math.Max(Math.Abs(dx), Math.Abs(dy)); step++)
                        if (Occupied(PieceFile(i) + Math.Sign(dx) * step, PieceRank(i) + Math.Sign(dy) * step)) { blocked = true; break; }
                    if (!blocked) return true;
                }
                return false;
            }
        }
        // Fixed valid fixture also used as a bounded-generation fallback.
        public static Layout Training => new Layout(new[] { 18, 45, 42, 7 },
            new[] { Kind.Rook, Kind.Rook, Kind.Bishop, Kind.Bishop }, new[] { 13, 28, 50 });
        static readonly Kind[] FirstKinds = { Kind.Rook, Kind.Rook, Kind.Bishop, Kind.Bishop };
        static readonly Kind[] SecondKinds = { Kind.Rook, Kind.Rook, Kind.Bishop, Kind.Bishop, Kind.Knight,
            Kind.Pawn, Kind.Pawn, Kind.Pawn, Kind.Pawn, Kind.Pawn, Kind.Pawn };
        static Layout WithWires(Random random, int[] pieces, Kind[] kinds)
        {
            var board = new Layout(pieces, kinds, new int[3]);
            var safe = new List<int>(); var danger = new List<int>();
            for (int cell = 0; cell < 64; cell++)
                if (!board.Occupied(cell % 8, cell / 8))
                    (board.Attacked(cell % 8, cell / 8) ? danger : safe).Add(cell);
            if (safe.Count == 0 || danger.Count < 2) return null;
            var wires = new int[3]; wires[0] = safe[random.Next(safe.Count)];
            for (int i = 1; i < 3; i++)
            { int pick = random.Next(danger.Count); wires[i] = danger[pick]; danger.RemoveAt(pick); }
            for (int i = 2; i > 0; i--)
            { int pick = random.Next(i + 1); int t = wires[i]; wires[i] = wires[pick]; wires[pick] = t; }
            return new Layout(pieces, kinds, wires);
        }
        public static Layout Generate(Random random, Layout previous = null)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            for (int attempt = 0; attempt < 128; attempt++)
            {
                var cells = new List<int>();
                for (int cell = 0; cell < 64; cell++)
                    if (previous == null || !previous.Occupied(cell % 8, cell / 8)) cells.Add(cell);
                var pieces = new int[4];
                for (int i = 0; i < 4; i++)
                { int pick = random.Next(cells.Count); pieces[i] = cells[pick]; cells.RemoveAt(pick); }
                var result = WithWires(random, pieces, FirstKinds);
                if (result != null) return result;
            }
            // Two verified mirrored fixtures guarantee a different overall layout on fallback.
            var fallback = Training;
            if (previous != null && previous.Occupied(2, 2)) fallback = fallback.MirrorRanks();
            var fixedPieces = new int[4];
            for (int i = 0; i < 4; i++) fixedPieces[i] = fallback.PieceRank(i) * 8 + fallback.PieceFile(i);
            return WithWires(random, fixedPieces, FirstKinds);
        }
        public static Layout GenerateSecond(Random random, Layout first)
        {
            if (random == null || first == null) throw new ArgumentNullException();
            if (first.PieceCount != 4 || first.PawnDirection != -1) throw new ArgumentException("A canonical first-stage layout is required.");
            for (int attempt = 0; attempt < 128; attempt++)
            {
                var pieces = new int[11]; var cells = new List<int>();
                for (int i = 0; i < 4; i++) pieces[i] = first.PieceRank(i) * 8 + first.PieceFile(i);
                for (int cell = 0; cell < 64; cell++) if (!first.Occupied(cell % 8, cell / 8)) cells.Add(cell);
                int knightPick = random.Next(cells.Count);
                pieces[4] = cells[knightPick]; cells.RemoveAt(knightPick);
                if (!PlacePawns(random, cells, pieces)) continue;
                var result = WithWires(random, pieces, SecondKinds);
                if (result != null) return result;
            }
            return SafeSecond(random, first);
        }
        internal static Layout SafeSecond(Random random, Layout first)
        {
            int safe = -1;
            for (int i = 0; i < 3; i++) if (!first.Attacked(first.File(i), first.Rank(i))) safe = first.Rank(i) * 8 + first.File(i);
            if (safe < 0) throw new ArgumentException("No safe first-stage wire.");
            var pieces = new int[11]; var cells = new List<int>();
            for (int i = 0; i < 4; i++) pieces[i] = first.PieceRank(i) * 8 + first.PieceFile(i);
            // Keep the old safe cell free and exclude incoming pawn/knight attacks.
            for (int cell = 0; cell < 64; cell++)
            {
                int dx = Math.Abs(cell % 8 - safe % 8), dy = cell / 8 - safe / 8;
                if (!first.Occupied(cell % 8, cell / 8) && cell != safe &&
                    !(dx * Math.Abs(dy) == 2 || dx == 1 && dy == 1)) cells.Add(cell);
            }
            foreach (int knight in cells)
            {
                var targets = new List<int>();
                foreach (int cell in cells)
                    if (Math.Abs(cell % 8 - knight % 8) * Math.Abs(cell / 8 - knight / 8) == 2) targets.Add(cell);
                if (targets.Count < 2) continue;
                // Reserve two knight targets as guaranteed unoccupied dangerous wire candidates.
                pieces[4] = knight;
                var pawns = new List<int>(cells); pawns.Remove(knight); pawns.Remove(targets[0]); pawns.Remove(targets[1]);
                if (!PlacePawns(random, pawns, pieces)) continue;
                return WithWires(random, pieces, SecondKinds);
            }
            throw new InvalidOperationException("No safe extension found.");
        }
        // Pick six distinct files first, then a free square within each selected file.
        static bool PlacePawns(Random random, List<int> cells, int[] pieces)
        {
            var files = new List<int>();
            for (int file = 0; file < Size; file++)
                if (cells.Exists(cell => cell % Size == file)) files.Add(file);
            if (files.Count < 6) return false;
            for (int i = 5; i < 11; i++)
            {
                int pick = random.Next(files.Count), file = files[pick]; files.RemoveAt(pick);
                var candidates = cells.FindAll(cell => cell % Size == file);
                pieces[i] = candidates[random.Next(candidates.Count)];
            }
            return true;
        }
        public sealed class Course
        {
            public readonly Layout[] Stages;
            public int Stage { get; private set; }
            public ChessWirePuzzle Current { get; private set; }
            public bool Complete => Stage == 1 && Current.Solved;
            readonly float cutSeconds, lockSeconds;
            public Course(Layout first, Layout second, float cutSeconds = 7, float lockSeconds = 2)
            { Stages = new[] { first, second }; this.cutSeconds = cutSeconds; this.lockSeconds = lockSeconds; Current = new ChessWirePuzzle(cutSeconds, lockSeconds, first); }
            public bool Advance()
            {
                if (Stage != 0 || !Current.Solved) return false;
                Stage = 1; Current = new ChessWirePuzzle(cutSeconds, lockSeconds, Stages[1]); return true;
            }
        }
        public enum Result { None, Wrong, Solved }
        public readonly float CutSeconds, LockSeconds;
        public readonly Layout Board;
        public bool Solved { get; private set; }
        public float Progress { get; private set; }
        public float LockLeft { get; private set; }
        readonly bool[] cut = new bool[3];
        int actor = -1, wire = -1;
        public ChessWirePuzzle(float cutSeconds = 7f, float lockSeconds = 2f, Layout layout = null)
        { CutSeconds = Math.Max(.1f, cutSeconds); LockSeconds = Math.Max(0, lockSeconds); Board = layout ?? Training; }
        public bool Cut(int index) => index >= 0 && index < 3 && cut[index];
        // One worker per team per tick; changing worker/wire, releasing, leaving range or losing a tool resets the hold.
        public Result Step(float dt, int worker, int target, bool canUse)
        {
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return Result.None;
            if (LockLeft > 0) { LockLeft = Math.Max(0, LockLeft - dt); Interrupt(); return Result.None; }
            if (Solved || !canUse || worker < 0 || target < 0 || target >= 3 || cut[target])
            { Interrupt(); return Result.None; }
            if (actor != worker || wire != target) { Progress = 0; actor = worker; wire = target; }
            Progress += dt;
            if (Progress < CutSeconds) return Result.None;
            cut[target] = true; Interrupt();
            if (!Board.Attacked(Board.File(target), Board.Rank(target))) { Solved = true; return Result.Solved; }
            LockLeft = LockSeconds;
            return Result.Wrong;
        }
        void Interrupt() { Progress = 0; actor = wire = -1; }
    }
}
