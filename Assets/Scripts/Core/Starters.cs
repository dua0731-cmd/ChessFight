using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ChessFight.Network
{
    // Who a started match is made of (Docs/Network/SESSION.md "경기 중 나간 사람과 돌아오기"). R538 (user 11-10: "게임이
    // 시작되면 그 방은 게임이 시작됐던 그 인원들로만 이루어져 있어야 해 ... 나간 사람만 돌아올 수 있어야"): a match belongs to the
    // players it started with. Whoever leaves it leaves their seat empty; nobody else is let into it; and only that
    // player may come back to it - to the same team and the same seat - for as long as the match is undecided.
    //
    // (R59 ~ R536 filled a leaver's seat from matchmaking instead, for four minutes after the start: "Backfill".)
    //
    // The host decides, and what it needs lives in the room ("starters", written at the start), so a new host
    // (Docs/Network/HOST.md) lets the same players back in.
    public static class Starters
    {
        // A player the match started with, and the seat that stays theirs.
        public struct Seat
        {
            public ulong Id;
            public int Team, Slot;
        }

        // The players of a team who are away: they started the match and are not seated in it now.
        public static int Away(IEnumerable<Seat> starters, ICollection<ulong> seated, int team) =>
            starters == null || seated == null ? 0 : starters.Count(s => s.Team == team && !seated.Contains(s.Id));

        // The seat a player comes back to. False for anyone the match did not start with.
        public static bool SeatOf(IEnumerable<Seat> starters, ulong id, out Seat seat)
        {
            seat = default;
            if (starters == null || id == 0) return false;
            foreach (var s in starters)
                if (s.Id == id) { seat = s; return true; }
            return false;
        }

        // As the room keeps them: "id:team:slot;...". Bots are not written: they never leave.
        public static string Encode(IEnumerable<Seat> starters) =>
            string.Join(";", starters.Select(s => string.Join(":",
                s.Id.ToString(CultureInfo.InvariantCulture), s.Team.ToString(CultureInfo.InvariantCulture),
                s.Slot.ToString(CultureInfo.InvariantCulture))));
        // Unreadable entries, zero IDs, repeats and impossible teams or slots are dropped; twelve at most.
        public static List<Seat> Decode(string text)
        {
            var seats = new List<Seat>();
            foreach (string entry in (text ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = entry.Split(':');
                if (p.Length != 3 || seats.Count >= TeamReservations.TeamSize * 2 ||
                    !ulong.TryParse(p[0], NumberStyles.None, CultureInfo.InvariantCulture, out ulong id) || id == 0 ||
                    !int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out int team) || team > 1 ||
                    !int.TryParse(p[2], NumberStyles.None, CultureInfo.InvariantCulture, out int slot) || slot >= TeamReservations.TeamSize ||
                    seats.Exists(s => s.Id == id || (s.Team == team && s.Slot == slot)))
                    continue;
                seats.Add(new Seat { Id = id, Team = team, Slot = slot });
            }
            return seats;
        }

        // The size each team started with, as the room keeps it: "6,6" (the room's seats, rule 1-1).
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
    }
}
