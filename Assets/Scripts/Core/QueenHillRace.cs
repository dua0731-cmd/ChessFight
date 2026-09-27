using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChessFight.Network
{
    // The climb rules of Queen of the Hill, 8th revision (R50, DESIGN §3 8차), with
    // no Unity in them so they are tested on their own (Tests/Network/NetworkCoreTests.cs).
    //
    // Floors are numbered 1..7 (floor n stands on rank n and is climbed to rank n+1).
    //
    // - SHORTCUTS, one per floor PER TEAM. The first player of a team to ring its
    //   team's bell at the top of floor n (on rank n+1) opens that team's shortcut
    //   up floor n for the rest of the round. The other team keeps climbing until
    //   its own first player gets there. A team never uses the other's shortcut.
    // - RANKS, PER PLAYER. Stepping on the rank pad of rank r records r as the
    //   highest rank that player has reached (rank 1 is the top of the cliff, reached
    //   by the hook). Nothing else counts; a teammate's progress never carries anyone.
    // - FALLS. There are no checkpoints: a fall puts the player back on rank 1 of its
    //   team's side, beside its team's vacuum tube, which lifts it straight to the
    //   highest rank IT has reached. Before it has ever stood on rank 1 it goes back
    //   to its team's bridge (falling off the bridge must not skip the hook).
    //
    // Only the machine that simulates the match judges; the others apply what it
    // sends (Encode / Apply). Times are the shared obstacle clock's seconds.
    public sealed class QueenHillRace
    {
        public const int White = 0, Black = 1, Teams = 2;

        readonly bool[,] open;              // [floor, team]
        readonly double[,] openedAt;
        readonly ulong[,] openedBy;
        readonly Dictionary<ulong, int> reached = new Dictionary<ulong, int>();

        public int Floors { get; }
        public int TopRank => Floors + 1;

        public QueenHillRace(int floors)
        {
            if (floors < 1) throw new ArgumentOutOfRangeException(nameof(floors));
            Floors = floors;
            open = new bool[floors + 1, Teams];
            openedAt = new double[floors + 1, Teams];
            openedBy = new ulong[floors + 1, Teams];
        }

        bool ValidFloor(int floor) => floor >= 1 && floor <= Floors;
        static bool ValidTeam(int team) => team == White || team == Black;

        // A player of `team` rang its team's bell at the top of `floor`. True when this
        // opened the team's shortcut (only the first ring of the team counts).
        public bool TryOpen(int floor, int team, ulong player, double now)
        {
            if (!ValidFloor(floor) || !ValidTeam(team) || open[floor, team] || double.IsNaN(now) || double.IsInfinity(now))
                return false;
            open[floor, team] = true;
            openedAt[floor, team] = now;
            openedBy[floor, team] = player;
            return true;
        }

        public bool IsOpen(int floor, int team) => ValidFloor(floor) && ValidTeam(team) && open[floor, team];
        public double OpenedAt(int floor, int team) => IsOpen(floor, team) ? openedAt[floor, team] : double.NaN;
        public ulong OpenedBy(int floor, int team) => IsOpen(floor, team) ? openedBy[floor, team] : 0;

        // How many of a team's shortcuts are open.
        public int OpenCount(int team)
        {
            int n = 0;
            for (int f = 1; f <= Floors; f++)
                if (IsOpen(f, team)) n++;
            return n;
        }

        // A player stepped on the rank pad of `rank` (1..TopRank). Never lowers.
        public bool Reach(ulong player, int rank)
        {
            if (rank < 1 || rank > TopRank) return false;
            if (reached.TryGetValue(player, out int best) && best >= rank) return false;
            reached[player] = rank;
            return true;
        }

        // The highest rank this player has stepped on; 0 before it ever landed on rank 1.
        public int ReachedRank(ulong player) => reached.TryGetValue(player, out int best) ? best : 0;

        // Has it made it across from the bridge? Then a fall puts it on rank 1, else on the bridge.
        public bool Landed(ulong player) => ReachedRank(player) >= 1;

        // Where the team's vacuum tube takes this player from rank 1: its own highest
        // rank, never higher than the last floor (the summit is climbed, not ridden).
        public int TubeTarget(ulong player) => Math.Max(1, Math.Min(ReachedRank(player), Floors));

        // The player left the match: forget it.
        public void Forget(ulong player) => reached.Remove(player);

        // A new round: every shortcut closed, every rank forgotten.
        public void Reset()
        {
            Array.Clear(open, 0, open.Length);
            Array.Clear(openedAt, 0, openedAt.Length);
            Array.Clear(openedBy, 0, openedBy.Length);
            reached.Clear();
        }

        // The opened shortcuts for the wire: "floor:team:openedAtMs;...". Ranks travel
        // with each player's own state, not here.
        public string Encode()
        {
            var text = new StringBuilder();
            for (int f = 1; f <= Floors; f++)
                for (int t = 0; t < Teams; t++)
                {
                    if (!open[f, t]) continue;
                    if (text.Length > 0) text.Append(';');
                    text.Append(f.ToString(CultureInfo.InvariantCulture)).Append(':')
                        .Append(t.ToString(CultureInfo.InvariantCulture)).Append(':')
                        .Append(Math.Round(openedAt[f, t] * 1000.0).ToString("0", CultureInfo.InvariantCulture));
                }
            return text.ToString();
        }

        // What the judging machine sent. Only ever opens; anything malformed is
        // dropped whole. True when something new opened.
        public bool Apply(string encoded)
        {
            if (encoded == null || encoded.Length > 512) return false;
            var parsed = new List<int[]>();
            var times = new List<double>();
            if (encoded.Length > 0)
            {
                foreach (string part in encoded.Split(';'))
                {
                    string[] f = part.Split(':');
                    if (f.Length != 3
                        || !int.TryParse(f[0], NumberStyles.None, CultureInfo.InvariantCulture, out int floor)
                        || !int.TryParse(f[1], NumberStyles.None, CultureInfo.InvariantCulture, out int team)
                        || !long.TryParse(f[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long ms)
                        || !ValidFloor(floor) || !ValidTeam(team)) return false;
                    parsed.Add(new[] { floor, team });
                    times.Add(ms / 1000.0);
                }
            }
            bool changed = false;
            for (int i = 0; i < parsed.Count; i++)
                changed |= TryOpen(parsed[i][0], parsed[i][1], 0, times[i]);
            return changed;
        }
    }
}
