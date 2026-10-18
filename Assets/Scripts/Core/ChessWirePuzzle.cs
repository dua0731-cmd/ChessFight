using System;

namespace ChessFight.Network
{
    // One physical 8x8 training puzzle. Coordinates are zero-based; both teams receive the same layout.
    public sealed class ChessWirePuzzle
    {
        public const int Size = 8, PieceCount = 3;
        static readonly int[] PieceFiles = { 2, 5, 2 }, PieceRanks = { 2, 5, 5 };
        static readonly int[] Files = { 5, 4, 2 }, Ranks = { 1, 3, 6 };
        public enum Result { None, Wrong, Solved }
        public readonly float CutSeconds, LockSeconds;
        public bool Solved { get; private set; }
        public float Progress { get; private set; }
        public float LockLeft { get; private set; }
        readonly bool[] cut = new bool[3];
        int actor = -1, wire = -1;
        public ChessWirePuzzle(float cutSeconds = 7f, float lockSeconds = 2f)
        { CutSeconds = Math.Max(.1f, cutSeconds); LockSeconds = Math.Max(0, lockSeconds); }
        public static int File(int index) => Files[index];
        public static int Rank(int index) => Ranks[index];
        public static int PieceFile(int index) => PieceFiles[index];
        public static int PieceRank(int index) => PieceRanks[index];
        public static bool IsBishop(int index) => index == 2;
        static bool Occupied(int file, int rank)
        {
            for (int i = 0; i < PieceCount; i++)
                if (PieceFile(i) == file && PieceRank(i) == rank) return true;
            return false;
        }
        public static bool Attacked(int file, int rank)
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
            if (!Attacked(File(target), Rank(target))) { Solved = true; return Result.Solved; }
            LockLeft = LockSeconds;
            return Result.Wrong;
        }
        void Interrupt() { Progress = 0; actor = wire = -1; }
    }
}
