using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ChessFight.Network
{
    // Refilling the seats of players who left a started match (Docs/Network/SESSION.md
    // "빈자리 채우기"). A player who leaves a started match leaves an empty seat, and
    // players searching for that mode may take it: only on the team that lost
    // someone, and only up to the size that team started with, so a 6 v 6 match
    // refills to six and a 5 v 5 to five. A party still sits together on one team.
    //
    // The host decides, and everything it needs lives in the lobby ("seats",
    // "held"), so a new host (Docs/Network/HOST.md) carries on with the same seats.
    public static class Backfill
    {
        // A joining party's members still on their way keep their seats this long,
        // like a reservation in the waiting room.
        public const double HoldSeconds = 25;
        // After the shared start (MatchStart, "go") empty seats are offered for
        // four minutes, the user's choice (R60).
        public const double OpenSeconds = 240;

        // Who sits on which team and spawn slot. Until is only set on a held seat:
        // the Steam server time its player has to arrive by.
        public struct Seat
        {
            public ulong Id;
            public int Team, Slot;
            public double Until;
        }

        // Whether a started match offers its empty seats: always while the start
        // time is not set yet (everyone is still loading), then for OpenSeconds.
        public static bool Open(double startAt, double now) => startAt <= 0 || now < startAt + OpenSeconds;

        // Seats a team can still give out: what it started with, less everyone
        // seated or on the way. None without a known size.
        public static int Free(int[] capacity, IEnumerable<Seat> taken, int team)
        {
            if (capacity == null || capacity.Length != 2 || team < 0 || team > 1) return 0;
            return Math.Max(0, capacity[team] - taken.Count(s => s.Team == team));
        }

        // Where a party of `count` sits: the team with more empty seats that fits all
        // of it (a tie goes to white), on that team's lowest free spawn slots.
        public static bool Place(int[] capacity, IList<Seat> taken, int count, out int team, out int[] slots)
        {
            team = -1; slots = null;
            if (count < 1) return false;
            int free0 = Free(capacity, taken, 0), free1 = Free(capacity, taken, 1);
            foreach (int t in free1 > free0 ? new[] { 1, 0 } : new[] { 0, 1 })
            {
                if (Free(capacity, taken, t) < count) continue;
                var used = new HashSet<int>(taken.Where(s => s.Team == t).Select(s => s.Slot));
                var open = Enumerable.Range(0, TeamReservations.TeamSize).Where(s => !used.Contains(s)).Take(count).ToArray();
                if (open.Length < count) continue;
                team = t; slots = open;
                return true;
            }
            return false;
        }

        // The size each team started with, as the room keeps it: "6,6".
        public static string EncodeCapacity(int white, int black) =>
            white.ToString(CultureInfo.InvariantCulture) + "," + black.ToString(CultureInfo.InvariantCulture);
        // Null unless it is two team sizes of 0-6.
        public static int[] DecodeCapacity(string text)
        {
            var parts = (text ?? "").Split(',');
            if (parts.Length != 2) return null;
            var sizes = new int[2];
            for (int i = 0; i < 2; i++)
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out sizes[i]) || sizes[i] > TeamReservations.TeamSize)
                    return null;
            return sizes;
        }

        // Held seats as the room keeps them: "id:team:slot:until;...".
        public static string EncodeHeld(IEnumerable<Seat> held) =>
            string.Join(";", held.Select(s => string.Join(":",
                s.Id.ToString(CultureInfo.InvariantCulture), s.Team.ToString(CultureInfo.InvariantCulture),
                s.Slot.ToString(CultureInfo.InvariantCulture), s.Until.ToString("F0", CultureInfo.InvariantCulture))));
        // Unreadable entries, zero IDs, repeats and impossible teams or slots are dropped.
        public static List<Seat> DecodeHeld(string text)
        {
            var seats = new List<Seat>();
            foreach (string entry in (text ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = entry.Split(':');
                if (p.Length != 4 ||
                    !ulong.TryParse(p[0], NumberStyles.None, CultureInfo.InvariantCulture, out ulong id) || id == 0 ||
                    !int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out int team) || team > 1 ||
                    !int.TryParse(p[2], NumberStyles.None, CultureInfo.InvariantCulture, out int slot) || slot >= TeamReservations.TeamSize ||
                    !double.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double until) ||
                    double.IsNaN(until) || double.IsInfinity(until) ||
                    seats.Exists(s => s.Id == id || (s.Team == team && s.Slot == slot)))
                    continue;
                seats.Add(new Seat { Id = id, Team = team, Slot = slot, Until = until });
            }
            return seats;
        }
    }
}
