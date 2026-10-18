using System;

namespace ChessFight.Network
{
    // Physical 8x8 wire puzzles. Coordinates are zero-based; both teams receive the same layout.
    public sealed class ChessWirePuzzle
    {
        public const int Size = 8, PieceCount = 3;
        // Immutable layout is shared by both teams; cutting state remains per team.
        public sealed class Layout
        {
            readonly int[] pieces, wires;
            readonly bool[] bishops;
            internal Layout(int[] pieces, bool[] bishops, int[] wires)
            { this.pieces = (int[])pieces.Clone(); this.bishops = (bool[])bishops.Clone(); this.wires = (int[])wires.Clone(); }
            public int File(int index) => wires[index] % Size;
            public int Rank(int index) => wires[index] / Size;
            public int PieceFile(int index) => pieces[index] % Size;
            public int PieceRank(int index) => pieces[index] / Size;
            public bool IsBishop(int index) => bishops[index];
            public bool Occupied(int file, int rank)
            {
                for (int i = 0; i < PieceCount; i++)
                    if (PieceFile(i) == file && PieceRank(i) == rank) return true;
                return false;
            }
            public bool Attacked(int file, int rank)
            {
                if (file < 0 || file >= Size || rank < 0 || rank >= Size) return false;
                if (Occupied(file, rank)) return true;
                for (int i = 0; i < PieceCount; i++)
                    for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                    {
                        bool diagonal = dx != 0 && dy != 0;
                        if ((dx == 0 && dy == 0) || IsBishop(i) != diagonal) continue;
                        int x = PieceFile(i) + dx, y = PieceRank(i) + dy;
                        while (x >= 0 && x < Size && y >= 0 && y < Size)
                        {
                            if (x == file && y == rank) return true;
                            if (Occupied(x, y)) break;
                            x += dx; y += dy;
                        }
                    }
                return false;
            }
        }
        public static Layout Training => new Layout(new[] { 18, 45, 42 },
            new[] { false, false, true }, new[] { 13, 28, 50 });
        public static Layout Generate(Random random, Layout previous = null)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            for (int attempt = 0; attempt < 128; attempt++)
            {
                var cells = new System.Collections.Generic.List<int>();
                for (int cell = 0; cell < Size * Size; cell++)
                {
                    bool old = false;
                    for (int i = 0; previous != null && i < PieceCount; i++)
                        if (cell == previous.PieceRank(i) * Size + previous.PieceFile(i)) old = true;
                    if (!old) cells.Add(cell);
                }
                var pieces = new int[PieceCount];
                for (int i = 0; i < PieceCount; i++)
                { int pick = random.Next(cells.Count); pieces[i] = cells[pick]; cells.RemoveAt(pick); }
                var bishops = new[] { false, true, random.Next(2) == 0 };
                var candidate = new Layout(pieces, bishops, new int[3]);
                var safe = new System.Collections.Generic.List<int>();
                var danger = new System.Collections.Generic.List<int>();
                for (int cell = 0; cell < Size * Size; cell++)
                    if (!candidate.Occupied(cell % Size, cell / Size))
                        (candidate.Attacked(cell % Size, cell / Size) ? danger : safe).Add(cell);
                if (safe.Count == 0 || danger.Count < 2) continue;
                var wires = new int[3];
                wires[0] = safe[random.Next(safe.Count)];
                for (int i = 1; i < 3; i++)
                { int pick = random.Next(danger.Count); wires[i] = danger[pick]; danger.RemoveAt(pick); }
                for (int i = 2; i > 0; i--)
                { int pick = random.Next(i + 1); int tmp = wires[i]; wires[i] = wires[pick]; wires[pick] = tmp; }
                return new Layout(pieces, bishops, wires);
            }
            // Translations of the verified training board preserve blocking and a unique safe wire.
            // Each previous piece excludes at most three of these 12 translations.
            for (int dx = -2; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
            {
                var basis = Training;
                var pieces = new int[3]; var wires = new int[3]; bool overlap = false;
                // C4 (rook), E4 (bishop), C7 (safe) fit every translation.
                int[] baseWires = { 26, 28, 50 };
                for (int i = 0; i < 3; i++)
                {
                    pieces[i] = (basis.PieceRank(i) + dy) * Size + basis.PieceFile(i) + dx;
                    wires[i] = baseWires[i] + dy * Size + dx;
                    for (int j = 0; previous != null && j < 3; j++)
                        if (pieces[i] == previous.PieceRank(j) * Size + previous.PieceFile(j)) overlap = true;
                }
                if (!overlap)
                {
                    for (int i = 2; i > 0; i--)
                    { int pick = random.Next(i + 1); int tmp = wires[i]; wires[i] = wires[pick]; wires[pick] = tmp; }
                    return new Layout(pieces, new[] { false, false, true }, wires);
                }
            }
            throw new InvalidOperationException("No fallback layout available.");
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
