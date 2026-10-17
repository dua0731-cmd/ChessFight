using System;

namespace ChessFight.Network
{
    // One physical 8x8 training puzzle. Coordinates are zero-based; both teams receive the same layout.
    public sealed class ChessWirePuzzle
    {
        public const int Size = 8, RookFile = 2, RookRank = 2;
        static readonly int[] Files = { 2, 6, 6 }, Ranks = { 6, 2, 6 };
        public enum Result { None, Wrong, Solved }
        public readonly float CutSeconds, LockSeconds;
        public bool Solved { get; private set; }
        public float Progress { get; private set; }
        public float LockLeft { get; private set; }
        readonly bool[] cut = new bool[3];
        int actor = -1, wire = -1;
        public ChessWirePuzzle(float cutSeconds = 1.5f, float lockSeconds = 2f)
        { CutSeconds = Math.Max(.1f, cutSeconds); LockSeconds = Math.Max(0, lockSeconds); }
        public static int File(int index) => Files[index];
        public static int Rank(int index) => Ranks[index];
        public static bool Attacked(int file, int rank) => file == RookFile || rank == RookRank;
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
