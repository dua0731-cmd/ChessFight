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
            if (sender == 0 || party == 0 || string.IsNullOrEmpty(ticket) || ticket.Length > 64 ||
                members == null || members.Length < 1 || members.Length > TeamSize ||
                !members.Contains(sender) || members.Any(id => id == 0) || members.Distinct().Count() != members.Length)
                return false;
            var prior = groups.Find(g => g.Leader == sender);
            if (prior != null)
            {
                if (prior.Party != party || prior.Ticket != ticket || !prior.Members.SequenceEqual(members)) return false;
                result = prior; // Retries must not extend the lease forever.
                return true;
            }
            if (members.Any(id => Find(id) != null)) return false;
            int a = Used(0), b = Used(1), team = a <= b ? 0 : 1;
            if (Used(team) + members.Length > TeamSize) team = 1 - team;
            if (Used(team) + members.Length > TeamSize) return false;
            result = new Group { Leader = sender, Party = party, Ticket = ticket,
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
                bool all = g.Members.All(present.Contains);
                if (all) g.Committed = true;
                if ((!g.Committed && now > g.Deadline) || (g.Committed && !all))
                { groups.Remove(g); removed.Add(g); }
            }
            return removed;
        }
        public bool Ready => Count == 12 && groups.All(g => g.Committed);
        public void Remove(ulong leader) => groups.RemoveAll(g => g.Leader == leader);
        public void Clear() => groups.Clear();
    }
}
