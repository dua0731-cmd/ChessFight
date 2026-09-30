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

        public bool Reserve(ulong sender, ulong party, string ticket, ulong[] members, double now, out Group result)
        {
            result = null;
            if (!ValidRequest(sender, party, ticket, members)) return false;
            return Admit(sender, party, ticket, members, now, out result);
        }

        // The shape every party request must have, in a waiting room or for the
        // empty seats of a started match (Backfill).
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

        // Before start, a partial party failure releases the whole group atomically.
        public List<Group> Reconcile(HashSet<ulong> present, double now)
        {
            var removed = new List<Group>();
            foreach (var g in groups.ToArray())
            {
                // Bots never join the Steam lobby, so they always count as arrived.
                bool all = g.Members.All(id => BotIdentity.IsBot(id) || present.Contains(id));
                if (all) g.Committed = true;
                if ((!g.Committed && now > g.Deadline) || (g.Committed && !all))
                { groups.Remove(g); removed.Add(g); }
            }
            return removed;
        }
        public bool Ready => Count == 12 && groups.All(g => g.Committed);
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
        public void Remove(ulong leader) => groups.RemoveAll(g => g.Leader == leader);
        public void RemoveFillerBots() => groups.RemoveAll(g => BotIdentity.IsBot(g.Leader));
        public void Clear() => groups.Clear();
    }
}
