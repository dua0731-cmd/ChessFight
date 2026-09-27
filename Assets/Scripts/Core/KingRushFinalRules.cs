using System;

namespace ChessFight.Network
{
    // Offline owner today; these facts must be replicated by a future host, not inferred by clients.
    public sealed class KingRushFinalRules
    {
        readonly double[] arrival = { -1, -1 }, earned = { 0, 0 };
        double last = -1;
        public double StartedAt { get; private set; } = -1;
        public double FirstArrival => arrival[0] < 0 ? arrival[1] : arrival[1] < 0 ? arrival[0] : Math.Min(arrival[0], arrival[1]);
        public bool Started => StartedAt >= 0;
        public bool Ended => Winner != -2;
        public int Winner { get; private set; } = -2; // -2 playing, -1 draw, 0 white, 1 black.
        public int Seated { get; private set; } = -1;
        public string Reason { get; private set; } = "";
        public double Progress(int team) => team < 0 || team > 1 ? 0 : earned[team] / 8;
        public double Elapsed(double now) => Started ? Math.Max(0, now - StartedAt) : 0;
        public bool Sudden(double now) => Started && now >= StartedAt + 140;
        static bool Valid(double now) => !double.IsNaN(now) && !double.IsInfinity(now) && now >= 0;
        public void ArriveKing(int team, double now)
        {
            if (Ended || team < 0 || team > 1 || !Valid(now) || now < last || arrival[team] >= 0) return;
            arrival[team] = now;
        }
        // Seat mask: eligible kings in the throne. Block mask: team whose dais is contested.
        // Fallen kings are submitted together so iteration order cannot decide simultaneous falls.
        public void Advance(double now, int seatMask, int blockedMask, int fallenKings)
        {
            if (Ended || !Valid(now) || now < last) return;
            double previous = last < 0 ? now : last; last = now;
            if (!Started && FirstArrival >= 0)
            {
                double both = arrival[0] >= 0 && arrival[1] >= 0 ? Math.Max(arrival[0], arrival[1]) : double.PositiveInfinity;
                double start = Math.Min(both, FirstArrival + 25);
                if (now >= start) StartedAt = start;
            }
            if (!Started) return;
            if (Sudden(now) && (fallenKings & 3) != 0)
            { Finish((fallenKings & 3) == 3 ? -1 : (fallenKings & 1) != 0 ? 1 : 0, "초읽기 킹 추락"); return; }
            seatMask &= 3;
            if (Seated >= 0 && (seatMask & (1 << Seated)) == 0) Seated = -1;
            if (Seated < 0) Seated = seatMask == 1 ? 0 : seatMask == 2 ? 1 : -1;
            double from = Math.Max(previous, StartedAt), to = Math.Min(now, StartedAt + 180);
            if (Seated >= 0 && (blockedMask & (1 << Seated)) == 0 && to > from)
            {
                double fast = Math.Max(0, to - Math.Max(from, StartedAt + 140));
                earned[Seated] = Math.Min(8, earned[Seated] + to - from + fast);
                if (earned[Seated] >= 8 - 1e-8) { Finish(Seated, "왕관 게이지 완성"); return; }
            }
            if (now >= StartedAt + 180)
                Finish(Math.Abs(earned[0] - earned[1]) < 1e-8 ? -1 : earned[0] > earned[1] ? 0 : 1, "결승 제한 시간");
        }
        void Finish(int winner, string reason) { Winner = winner; Reason = reason; }
        public static double TileFallsAt(int x, int z)
        {
            if (x < 0 || x > 7 || z < 0 || z > 7) throw new ArgumentOutOfRangeException();
            if (x >= 3 && x <= 4 && z >= 3 && z <= 4) return double.PositiveInfinity;
            if ((x + z) % 2 == 0) return 50;
            return x < 2 || x > 5 || z < 2 || z > 5 ? 100 : 140;
        }
    }
}
