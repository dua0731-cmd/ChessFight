using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessFight.Network
{
    // Pure C#: the host is the only writer. A reservation counts every party member,
    // including members whose asynchronous Steam lobby join has not completed yet.
    public sealed class TeamReservations
    {
        public const int TeamSize = 6;
        // The host cannot see who is really in a party (that lobby is the party's own),
        // so a request may declare made-up members who never come and keep their seats
        // for a reservation's 25 seconds. A sender whose people never came waits this
        // long before it may ask for seats again. (A started match seats nobody but
        // the players it started with, each for themselves: R538, Starters.)
        // Only the host keeps it: a new host starts without it (Docs/Network/SESSION.md §4).
        public const double Cooldown = 60;
        public sealed class Group
        {
            public ulong Leader, Party;
            public string Ticket;
            public ulong[] Members;
            public int Team;
            public double Deadline;
            public bool Committed;
        }
        readonly List<Group> groups = new List<Group>();
        public IReadOnlyList<Group> Groups => groups;
        public int Count => groups.Sum(g => g.Members.Length);
        public int Used(int team) => groups.Where(g => g.Team == team).Sum(g => g.Members.Length);
        public Group Find(ulong id) => groups.Find(g => Array.IndexOf(g.Members, id) >= 0);
        // Senders waiting out the Cooldown, with the time they may ask again.
        readonly Dictionary<ulong, double> cooldowns = new Dictionary<ulong, double>();
        public bool CoolingDown(ulong sender, double now) => cooldowns.TryGetValue(sender, out double until) && now < until;
        // Also for a started match's held seat that nobody came for (SteamSession.Refill).
        public void StartCooldown(ulong sender, double now) { if (sender != 0) cooldowns[sender] = now + Cooldown; }

        public bool Reserve(ulong sender, ulong party, string ticket, ulong[] members, double now, out Group result)
        {
            result = null;
            if (!ValidRequest(sender, party, ticket, members) || CoolingDown(sender, now)) return false;
            return Admit(sender, party, ticket, members, now, out result);
        }

        // The shape every seat request must have, in a waiting room or from a player
        // coming back into a started match (R538: then for themselves alone).
        public static bool ValidRequest(ulong sender, ulong party, string ticket, ulong[] members)
        {
            // A bot has no Steam presence, so it can never be the sender of a request.
            if (BotIdentity.IsBot(sender)) return false;
            if (sender == 0 || party == 0 || string.IsNullOrEmpty(ticket) || ticket.Length > 64 ||
                members == null || members.Length < 1 || members.Length > TeamSize ||
                !members.Contains(sender) || members.Any(id => id == 0) || members.Distinct().Count() != members.Length)
                return false;
            // A declared bot must be derived from the leader declaring it, so one
            // client cannot claim slots using another party's bot identifiers.
            return !members.Any(id => BotIdentity.IsBot(id) && !BotIdentity.OwnedBy(id, sender));
        }

        // Host-only. Filler bots exist to reach 12 pawns without 12 testers; they
        // have no lobby membership and therefore no chat path to arrive on.
        public bool ReserveBots(ulong host, ulong[] members, double now, out Group result)
            => ReserveBots(host, members, now, -1, out result);

        // A private test may deliberately be asymmetric. Never rebalance an
        // explicitly chosen team or split a human party to make room for bots.
        public bool ReserveBots(ulong host, ulong[] members, double now, int team, out Group result)
        {
            result = null;
            if (team < -1 || team > 1 || host == 0 || BotIdentity.IsBot(host) || members == null || members.Length < 1 || members.Length > TeamSize ||
                members.Distinct().Count() != members.Length ||
                members.Any(id => !BotIdentity.OwnedBy(id, host)))
                return false;
            return Admit(members[0], host, "bots:" + members[0], members, now, out result, team);
        }

        bool Admit(ulong leader, ulong party, string ticket, ulong[] members, double now, out Group result, int requestedTeam = -1)
        {
            result = null;
            var prior = groups.Find(g => g.Leader == leader);
            if (prior != null)
            {
                if (prior.Party != party || prior.Ticket != ticket || !prior.Members.SequenceEqual(members) ||
                    (requestedTeam >= 0 && prior.Team != requestedTeam)) return false;
                result = prior; // Retries must not extend the lease forever.
                return true;
            }
            if (members.Any(id => Find(id) != null)) return false;
            int a = Used(0), b = Used(1), team = requestedTeam >= 0 ? requestedTeam : a <= b ? 0 : 1;
            if (requestedTeam < 0 && Used(team) + members.Length > TeamSize) team = 1 - team;
            if (Used(team) + members.Length > TeamSize) return false;
            result = new Group { Leader = leader, Party = party, Ticket = ticket,
                Members = (ulong[])members.Clone(), Team = team, Deadline = now + 25 };
            groups.Add(result);
            return true;
        }

        // Before the start: a group whose people never all came is released whole and starts its
        // sender's Cooldown. R644 (user 11-14: "그 사람만 나가고, 나머지 파티원과 방은 그대로"): a group
        // that came and then lost a member keeps the others - only the one who left (or whose PC
        // died) gives up its seat. It used to be released whole, so one player's crash threw its
        // party mates out of the waiting room. A group with no person left goes (its bots with it).
        // Returns the groups removed whole.
        public List<Group> Reconcile(HashSet<ulong> present, double now)
        {
            var removed = new List<Group>();
            foreach (var g in groups.ToArray())
            {
                // Bots never join the Steam lobby, so they always count as arrived.
                bool all = g.Members.All(id => BotIdentity.IsBot(id) || present.Contains(id));
                if (all) g.Committed = true;
                if (!g.Committed && now > g.Deadline)
                {
                    groups.Remove(g); removed.Add(g);
                    StartCooldown(g.Leader, now);
                }
                else if (g.Committed && !all)
                {
                    g.Members = g.Members.Where(id => BotIdentity.IsBot(id) || present.Contains(id)).ToArray();
                    if (!g.Members.Any(id => !BotIdentity.IsBot(id))) { groups.Remove(g); removed.Add(g); }
                }
            }
            return removed;
        }
        // R644: one player out of a group, the others keep their seats (a host that leaves the
        // waiting room alone). False when the player had no seat.
        public bool RemoveMember(ulong id)
        {
            var g = Find(id);
            if (g == null) return false;
            g.Members = g.Members.Where(m => m != id).ToArray();
            if (!g.Members.Any(m => !BotIdentity.IsBot(m))) groups.Remove(g);
            return true;
        }
        public bool Ready => Count == 12 && groups.All(g => g.Committed);
        // R148 (Pawn Rush): the host may start before twelve once each side has at least MinTeamToStart and the
        // two sides are the same size (3v3, 4v4, 5v5), everyone arrived.
        public const int MinTeamToStart = 3;
        public bool ReadyEarly => ReadyEarlyFrom(MinTeamToStart);
        // R525: the same from another minimum a side (a development build's host starts from one: SteamSession.MinTeamToStart).
        public bool ReadyEarlyFrom(int minimum) => Used(0) >= Math.Max(1, minimum) && Used(0) == Used(1) && groups.All(g => g.Committed);
        public int Bots => groups.Sum(g => g.Members.Count(BotIdentity.IsBot));
        public int FillerBots(int team) => groups.Where(g => g.Team == team && BotIdentity.IsBot(g.Leader)).Sum(g => g.Members.Length);
        public bool RemoveFillerBot(int team)
        {
            var group = groups.FindLast(g => g.Team == team && BotIdentity.IsBot(g.Leader));
            if (group == null) return false;
            // Remove from the tail so the group's leader remains its first member.
            if (group.Members.Length == 1) groups.Remove(group);
            else group.Members = group.Members.Take(group.Members.Length - 1).ToArray();
            return true;
        }
        // R159: the waiting room's host role moves to the best PC in it, and the
        // reservations move with it (the room carries them, "resv"). The new host
        // takes them as they are; a group that does not fit (a clash, a full team)
        // is dropped, and its party asks again on its next try.
        public void Restore(IEnumerable<Group> list)
        {
            groups.Clear();
            foreach (var g in list ?? Enumerable.Empty<Group>())
            {
                if (g == null || g.Leader == 0 || g.Members == null || g.Members.Length < 1 || g.Members.Length > TeamSize ||
                    g.Team < 0 || g.Team > 1 || g.Members.Any(id => id == 0) || g.Members.Distinct().Count() != g.Members.Length ||
                    g.Members.Any(id => Find(id) != null) || groups.Any(o => o.Leader == g.Leader) ||
                    Used(g.Team) + g.Members.Length > TeamSize) continue;
                groups.Add(new Group { Leader = g.Leader, Party = g.Party, Ticket = g.Ticket ?? "", Members = (ulong[])g.Members.Clone(),
                                       Team = g.Team, Deadline = g.Deadline, Committed = g.Committed });
            }
        }
        public void Remove(ulong leader) => groups.RemoveAll(g => g.Leader == leader);
        public void RemoveFillerBots() => groups.RemoveAll(g => BotIdentity.IsBot(g.Leader));
        // A new room, or the host role gone elsewhere: nothing reserved, nobody waiting.
        public void Clear() { groups.Clear(); cooldowns.Clear(); }
    }
}
