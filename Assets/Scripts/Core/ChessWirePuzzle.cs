using System;
using System.Collections.Generic;

namespace ChessFight.Network
{
    public sealed class ChessWirePuzzle
    {
        public const int Size = 8;
        public enum Kind { Rook, Bishop, Knight, Pawn, Queen }
        // Immutable board data; each team owns separate cutting state.
        public sealed class Layout
        {
            readonly int[] pieces, wires;
            readonly Kind[] kinds;
            public int PieceCount => pieces.Length;
            public int WireCount => wires.Length;
            public int RequiredCuts => WireCount - 2;
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
                int[] p = new int[PieceCount], w = new int[WireCount];
                for (int i = 0; i < p.Length; i++) p[i] = (7 - PieceRank(i)) * 8 + PieceFile(i);
                for (int i = 0; i < WireCount; i++) w[i] = (7 - Rank(i)) * 8 + File(i);
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
                    bool straight = dx == 0 || dy == 0, diagonal = Math.Abs(dx) == Math.Abs(dy);
                    bool aligned = kinds[i] == Kind.Queen ? straight || diagonal : kinds[i] == Kind.Rook ? straight : diagonal;
                    if (!aligned) continue;
                    bool blocked = false;
                    for (int step = 1; step < Math.Max(Math.Abs(dx), Math.Abs(dy)); step++)
                        if (Occupied(PieceFile(i) + Math.Sign(dx) * step, PieceRank(i) + Math.Sign(dy) * step)) { blocked = true; break; }
                    if (!blocked) return true;
                }
                return false;
            }
        }
        static readonly Kind[][] StageKinds = {
            new[] { Kind.Rook, Kind.Rook, Kind.Bishop, Kind.Bishop },
            new[] { Kind.Rook, Kind.Rook, Kind.Bishop, Kind.Bishop, Kind.Knight,
                Kind.Pawn, Kind.Pawn, Kind.Pawn, Kind.Pawn, Kind.Pawn, Kind.Pawn },
            new[] { Kind.Rook, Kind.Rook, Kind.Bishop, Kind.Bishop, Kind.Knight, Kind.Knight, Kind.Queen,
                Kind.Pawn, Kind.Pawn, Kind.Pawn, Kind.Pawn, Kind.Pawn, Kind.Pawn, Kind.Pawn, Kind.Pawn }
        };
        static readonly int[][] FallbackPieces = {
            new[] { 8, 13, 53, 33 },
            new[] { 8, 13, 53, 33, 19, 61, 35, 22, 18, 17, 44 },
            new[] { 17, 28, 5, 2, 7, 52, 24, 15, 50, 8, 46, 36, 49, 45, 35 }
        };
        public static Layout Training => new Layout(FallbackPieces[0], StageKinds[0], new[] { 1, 2, 0, 5 });
        static Layout WithWires(Random random, int[] pieces, int stage)
        {
            var board = new Layout(pieces, StageKinds[stage], new int[stage + 4]);
            var safe = new List<int>(); var danger = new List<int>();
            for (int cell = 0; cell < 64; cell++)
                if (!board.Occupied(cell % 8, cell / 8))
                    (board.Attacked(cell % 8, cell / 8) ? danger : safe).Add(cell);
            if (safe.Count < stage + 2 || danger.Count < 2) return null;
            var wires = new int[stage + 4];
            for (int i = 0; i < wires.Length; i++)
            {
                var source = i < stage + 2 ? safe : danger;
                int pick = random.Next(source.Count); wires[i] = source[pick]; source.RemoveAt(pick);
            }
            for (int i = wires.Length - 1; i > 0; i--)
            { int pick = random.Next(i + 1); int t = wires[i]; wires[i] = wires[pick]; wires[pick] = t; }
            return new Layout(pieces, StageKinds[stage], wires);
        }
        static Layout TryGenerate(Random random, int stage, Layout foundation = null, Layout previous = null)
        {
            for (int attempt = 0; attempt < 128; attempt++)
            {
                var pieces = new int[StageKinds[stage].Length]; var cells = new List<int>();
                int fixedCount = foundation == null ? 0 : 4;
                for (int i = 0; i < fixedCount; i++) pieces[i] = foundation.PieceRank(i) * 8 + foundation.PieceFile(i);
                for (int cell = 0; cell < 64; cell++)
                    if ((foundation == null || !foundation.Occupied(cell % 8, cell / 8)) &&
                        (previous == null || !previous.Occupied(cell % 8, cell / 8))) cells.Add(cell);
                var files = new List<int>(); for (int file = 0; file < 8; file++) files.Add(file);
                bool valid = true;
                for (int i = fixedCount; i < pieces.Length; i++)
                {
                    List<int> candidates = cells;
                    if (StageKinds[stage][i] == Kind.Pawn)
                    {
                        files.RemoveAll(candidateFile => !cells.Exists(cell => cell % 8 == candidateFile));
                        if (files.Count == 0) { valid = false; break; }
                        int filePick = random.Next(files.Count), file = files[filePick]; files.RemoveAt(filePick);
                        candidates = cells.FindAll(cell => cell % 8 == file);
                    }
                    if (candidates.Count == 0) { valid = false; break; }
                    pieces[i] = candidates[random.Next(candidates.Count)]; cells.Remove(pieces[i]);
                }
                if (!valid) continue;
                var result = WithWires(random, pieces, stage);
                if (result != null) return result;
            }
            return null;
        }
        public static Layout[] GenerateCourse(Random random, Layout previousFirst = null)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            for (int attempt = 0; attempt < 16; attempt++)
            {
                var first = TryGenerate(random, 0, previous: previousFirst);
                if (first == null) continue;
                var second = TryGenerate(random, 1, first);
                var third = TryGenerate(random, 2);
                if (second != null && third != null) return new[] { first, second, third };
            }
            return FallbackCourse(random, previousFirst);
        }
        internal static Layout[] FallbackCourse(Random random, Layout previousFirst = null)
        {
            // Verified fixtures, optionally reflected across files; preserve pawn direction and stage-one pieces.
            bool reflect = random.Next(2) == 0;
            if (previousFirst != null)
                reflect = previousFirst.PieceFile(0) == FallbackPieces[0][0] % 8;
            var result = new Layout[3];
            for (int stage = 0; stage < 3; stage++)
            {
                var pieces = (int[])FallbackPieces[stage].Clone();
                if (reflect) for (int i = 0; i < pieces.Length; i++) pieces[i] = pieces[i] / 8 * 8 + 7 - pieces[i] % 8;
                result[stage] = WithWires(random, pieces, stage);
                if (result[stage] == null) throw new InvalidOperationException("Invalid fallback fixture.");
            }
            return result;
        }
        public sealed class Course
        {
            readonly Layout[] stages;
            public int Stage { get; private set; }
            public int StageCount => stages.Length;
            public ChessWirePuzzle Current { get; private set; }
            public bool Complete => Stage == stages.Length - 1 && Current.Solved;
            readonly float cutSeconds, lockSeconds;
            public Course(Layout[] layouts, float cutSeconds = 7, float lockSeconds = 2)
            {
                if (layouts == null || layouts.Length != 3 || Array.Exists(layouts, layout => layout == null))
                    throw new ArgumentException("Three valid stages are required.");
                stages = (Layout[])layouts.Clone(); this.cutSeconds = cutSeconds; this.lockSeconds = lockSeconds;
                Current = new ChessWirePuzzle(cutSeconds, lockSeconds, stages[0]);
            }
            public bool Advance()
            {
                if (Stage >= stages.Length - 1 || !Current.Solved) return false;
                Stage++; Current = new ChessWirePuzzle(cutSeconds, lockSeconds, stages[Stage]); return true;
            }
        }
        public enum Result { None, Wrong, Correct, Solved }
        public readonly float CutSeconds, LockSeconds;
        public readonly Layout Board;
        public int CorrectCuts { get; private set; }
        public bool Solved => CorrectCuts >= Board.RequiredCuts;
        public float Progress { get; private set; }
        public float LockLeft { get; private set; }
        readonly bool[] cut;
        int actor = -1, wire = -1;
        public ChessWirePuzzle(float cutSeconds = 7f, float lockSeconds = 2f, Layout layout = null)
        { CutSeconds = Math.Max(.1f, cutSeconds); LockSeconds = Math.Max(0, lockSeconds); Board = layout ?? Training; cut = new bool[Board.WireCount]; }
        public bool Cut(int index) => index >= 0 && index < cut.Length && cut[index];
        // One worker per team per tick; changing worker/wire, releasing, leaving range or losing a tool resets the hold.
        public Result Step(float dt, int worker, int target, bool canUse)
        {
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return Result.None;
            if (LockLeft > 0) { LockLeft = Math.Max(0, LockLeft - dt); Interrupt(); return Result.None; }
            if (Solved || !canUse || worker < 0 || target < 0 || target >= cut.Length || cut[target])
            { Interrupt(); return Result.None; }
            if (actor != worker || wire != target) { Progress = 0; actor = worker; wire = target; }
            Progress += dt;
            if (Progress < CutSeconds) return Result.None;
            cut[target] = true; Interrupt();
            if (!Board.Attacked(Board.File(target), Board.Rank(target)))
            { CorrectCuts++; return Solved ? Result.Solved : Result.Correct; }
            LockLeft = LockSeconds;
            return Result.Wrong;
        }
        void Interrupt() { Progress = 0; actor = wire = -1; }
    }
}
