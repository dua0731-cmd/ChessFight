using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ChessFight.Network
{
    // R533 (user 11-10, matching rule 3): a player who leaves a started match before it is over has deserted it. Two
    // desertions are free; the third stops that player's matchmaking for 10 minutes, the fourth for 30, the fifth and
    // every one after it for an hour, and a desertion stops counting 24 hours after it happened.
    //
    // Pure rules. The session says what a desertion is and refuses the search (SteamSession); the game keeps the list
    // between runs (NetworkRuntime: PlayerPrefs, builds only - the Editor neither counts nor stops). Times are seconds
    // on Steam's server clock, the same on every PC and out of reach of the PC's own clock.
    public static class LeavePenalty
    {
        // Desertions that cost nothing yet.
        public const int Free = 2;
        // How long a desertion counts.
        public const double Window = 24 * 60 * 60;
        // What the third, the fourth, and the fifth and later cost.
        static readonly double[] Bans = { 10 * 60, 30 * 60, 60 * 60 };
        // Entries kept between runs: far more than one day's bans can use.
        public const int Kept = 16;

        // How long matchmaking stops after a desertion that is the `count`th of its 24 hours.
        public static double BanSeconds(int count) => count <= Free ? 0 : Bans[Math.Min(count - Free - 1, Bans.Length - 1)];

        // Desertions that still count at `now`.
        public static int Count(IEnumerable<double> leaves, double now) =>
            leaves == null ? 0 : leaves.Count(t => t <= now && now - t < Window);

        // Until when matchmaking is stopped (0: never was). Each desertion is numbered among those of the 24 hours
        // before it, and stops matchmaking from its own moment for what that number costs.
        public static double BannedUntil(IEnumerable<double> leaves)
        {
            if (leaves == null) return 0;
            var sorted = leaves.OrderBy(t => t).ToList();
            double until = 0;
            for (int i = 0; i < sorted.Count; i++)
            {
                int count = 1;
                for (int j = i - 1; j >= 0 && sorted[i] - sorted[j] < Window; j--) count++;
                until = Math.Max(until, sorted[i] + BanSeconds(count));
            }
            return until;
        }

        public static double SecondsLeft(IEnumerable<double> leaves, double now) => Math.Max(0, BannedUntil(leaves) - now);

        // The list with one more desertion at `now`. What no longer counts is dropped (a ban is over long before
        // its desertion stops counting), and so is anything dated after `now`.
        public static List<double> Add(IEnumerable<double> leaves, double now)
        {
            var list = (leaves ?? Enumerable.Empty<double>()).Where(t => t <= now && now - t < Window).OrderBy(t => t).ToList();
            list.Add(now);
            if (list.Count > Kept) list.RemoveRange(0, list.Count - Kept);
            return list;
        }

        // As the game keeps it: whole seconds, "t,t,...". Anything unreadable is dropped.
        public static string Encode(IEnumerable<double> leaves) =>
            leaves == null ? "" : string.Join(",", leaves.Select(t => Math.Round(t).ToString("F0", CultureInfo.InvariantCulture)));

        public static List<double> Decode(string text)
        {
            var list = new List<double>();
            foreach (string part in (text ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (list.Count < Kept && double.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out double t) && t > 0) list.Add(t);
            return list;
        }

        // "9:59" or "1:00:00", for the HUD.
        public static string Clock(double seconds)
        {
            int s = (int)Math.Ceiling(Math.Max(0, seconds));
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
        }
    }
}
