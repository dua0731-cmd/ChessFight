using System;

namespace ChessFight.Network
{
    // R682 (the user after the 6 vs 6 test, 11-16: "현재 순위 실시간 표시", "코스 진행도 표시"; the mock-up's version B
    // chosen, "B안으로 진행"): the arithmetic behind the Pawn Rush match's standings strip and ranking list
    // (PawnRushStandingsHud). No Unity: the tests run it.
    //
    // A racer's place along the round is its section's number plus how far through that section it is: by distance
    // along the section's progress path in an obstacle section, by its team's progress in a mini-game (a room is not a
    // distance - the team works there until its gate opens). Nothing is sent for this: every PC already has every
    // pawn's position and every station's progress, and works the same standings out of them.
    public static class PawnRushStandings
    {
        // Two racers this close are level (teammates in one mini-game are exactly level).
        public const double Level = 1e-4;

        public static double Clamp01(double v) => double.IsNaN(v) ? 0 : v < 0 ? 0 : v > 1 ? 1 : v;

        // Section 0 is the first; `through` 0 at its entrance, 1 at its exit.
        public static double Progress(int section, double through) => Math.Max(0, section) + Clamp01(through);

        // Each racer's rank, 1 first: one more than the racers clearly ahead of it, so level racers share a rank
        // (1, 2, 2, 4).
        public static void Ranks(double[] progress, int count, int[] ranks)
        {
            for (int i = 0; i < count; i++)
            {
                int ahead = 0;
                for (int j = 0; j < count; j++)
                    if (j != i && progress[j] > progress[i] + Level) ahead++;
                ranks[i] = ahead + 1;
            }
        }

        // The racers in the order a list shows them: by rank, level racers in the order they were given (the roster's),
        // so two level names do not swap places from one look to the next.
        public static void Order(int[] ranks, int count, int[] order)
        {
            for (int i = 0; i < count; i++) order[i] = i;
            for (int i = 1; i < count; i++)
            {
                int racer = order[i], at = i - 1;
                while (at >= 0 && ranks[order[at]] > ranks[racer])
                {
                    order[at + 1] = order[at];
                    at--;
                }
                order[at + 1] = racer;
            }
        }

        // Which racers a list of `rows` lines shows (indices into `order`'s racers, written to `shown`): the first
        // `rows`, or - when this PC's own racer is further down - the first rows - 1 and then its own. Returns how many.
        public static int ListRows(int[] order, int count, int self, int rows, int[] shown)
        {
            int n = Math.Min(Math.Max(0, rows), count);
            bool selfIn = false;
            for (int i = 0; i < n; i++)
            {
                shown[i] = order[i];
                selfIn |= order[i] == self;
            }
            if (!selfIn && self >= 0 && self < count && n > 0 && n == rows) shown[n - 1] = self;
            return n;
        }

        // A section's share of the strip's track. A mini-game takes a fixed 1.1 (it fills by progress, whatever its
        // size); another section the root of its path's length - 60 m is 1 - so that the garden's 260 m is twice the
        // war room's 56 m, not five times; never under 0.7 (its name has to fit) nor over 2.
        public static double Weight(bool miniGame, double metres)
        {
            if (miniGame) return 1.1;
            if (double.IsNaN(metres) || metres <= 0) return 1;
            double w = Math.Sqrt(metres / 60.0);
            return w < .7 ? .7 : w > 2 ? 2 : w;
        }

        // The segments along a track of length 1: `gap` between neighbours, the rest shared by weight.
        public static void Layout(double[] weights, int count, double gap, double[] start, double[] width)
        {
            if (count <= 0) return;
            double total = 0;
            for (int i = 0; i < count; i++) total += Math.Max(0, weights[i]);
            double room = Math.Max(0, 1.0 - gap * (count - 1));
            double at = 0;
            for (int i = 0; i < count; i++)
            {
                start[i] = at;
                width[i] = total > 0 ? room * Math.Max(0, weights[i]) / total : room / count;
                at += width[i] + gap;
            }
        }

        // Where on the track (0..1) a racer's dot goes.
        public static double OnTrack(double[] start, double[] width, int count, int section, double through)
        {
            if (count <= 0) return 0;
            int s = section < 0 ? 0 : section >= count ? count - 1 : section;
            return start[s] + width[s] * Clamp01(through);
        }
    }
}
