using System.Collections.Generic;
using System.Linq;

namespace ChessFight.Network
{
    // R868 (user 11-27: "소드 파이트에서 경기가 끝나면 … 최고의 플레이나 등등 경기끝나고 나오는 화면" → design B,
    // Docs/GameModes/SwordFight/RESULT_SCREEN_DESIGN_v0.1.md): what the Sword Fight result screen shows, worked out from
    // the ring-out lines alone (SwordFeed: who fell, who gets the credit, with what). Every PC hears every line as it comes,
    // so every PC that saw the whole match gets the same answer, with nothing new on the wire. A PC that came back into
    // the match mid-way missed some lines: Complete is false then, and the screen shows the team scores only.
    public enum SwordAward : byte { FinishingBlow, IronWall, FreeFall, FirstBlood, Streak, WallBreaker }

    public sealed class SwordResultPlayer
    {
        public ulong Id;
        public string Name = "";
        public int Team;
        public PieceKind Piece;
        public bool Self;
        // Ring-outs credited to this player, its own falls (credited to someone or not), the falls nobody pushed.
        public int Knockouts, Falls, SelfFalls;
        // The most ring-outs in a row without falling in between.
        public int BestStreak;
        // How most of its ring-outs were made (the cause and the piece then, for the name of the skill).
        public SwordCause MainCause = SwordCause.Self;
        public PieceKind MainPiece;
        public int WallBreaks;
    }

    public struct SwordResultLine
    {
        public ulong Victim, Credit;   // Credit 0: fell on its own
        public int VictimTeam;
        public SwordCause Cause;
        public PieceKind CausePiece;
    }

    public struct SwordAwardPick
    {
        public SwordAward Award;
        public ulong Player;
        public int Count;              // the number the card shows (falls, streak, wall breaks); 0 when it shows none
        public SwordCause Cause;       // the finishing blow's and the first blood's means
        public PieceKind CausePiece;
    }

    public sealed class SwordResult
    {
        public const int MaxAwards = 3;

        public readonly List<SwordResultPlayer> Players = new List<SwordResultPlayer>();
        public readonly List<SwordAwardPick> Awards = new List<SwordAwardPick>();
        public int White, Black, Winner = -1;
        // Every point of the match has its line here (a PC that joined late has fewer).
        public bool Complete;
        // The best piece of the match (0: none, every point was a fall on its own or nothing happened).
        public ulong Best;

        public SwordResultPlayer Find(ulong id) => Players.FirstOrDefault(p => p.Id == id);

        /// <summary>The players by team, the most ring-outs first, then the fewest falls, then the name.</summary>
        public IEnumerable<SwordResultPlayer> Team(int team) =>
            Players.Where(p => p.Team == team).OrderByDescending(p => p.Knockouts).ThenBy(p => p.Falls).ThenBy(p => p.Name, System.StringComparer.Ordinal);

        /// <summary>The result from the players (everyone known at the end, and anyone a line names) and the lines in the
        /// order they were scored. <paramref name="white"/> and <paramref name="black"/> are the match's score.</summary>
        public static SwordResult Build(IEnumerable<SwordResultPlayer> players, IList<SwordResultLine> lines, int white, int black, int winner)
        {
            var r = new SwordResult { White = white, Black = black, Winner = winner };
            foreach (var p in players)
            {
                if (p == null || r.Find(p.Id) != null) continue;
                r.Players.Add(new SwordResultPlayer { Id = p.Id, Name = p.Name ?? "", Team = p.Team, Piece = p.Piece, Self = p.Self });
            }
            lines = lines ?? new List<SwordResultLine>();
            r.Complete = lines.Count == white + black;

            var streak = new Dictionary<ulong, int>();
            var means = new Dictionary<ulong, Dictionary<(SwordCause, PieceKind), int>>();
            int firstAt = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                var victim = r.Find(l.Victim);
                if (victim != null)
                {
                    victim.Falls++;
                    if (l.Credit == 0) victim.SelfFalls++;
                }
                streak[l.Victim] = 0;
                var credit = l.Credit != 0 ? r.Find(l.Credit) : null;
                if (credit == null) continue;
                if (firstAt < 0) firstAt = i;
                credit.Knockouts++;
                if (l.Cause == SwordCause.Collapse) credit.WallBreaks++;
                int s = (streak.TryGetValue(credit.Id, out int was) ? was : 0) + 1;
                streak[credit.Id] = s;
                if (s > credit.BestStreak) credit.BestStreak = s;
                if (!means.TryGetValue(credit.Id, out var m)) means[credit.Id] = m = new Dictionary<(SwordCause, PieceKind), int>();
                var key = (l.Cause, l.Cause == SwordCause.Sword ? PieceKind.Pawn : l.CausePiece);
                m[key] = (m.TryGetValue(key, out int n) ? n : 0) + 1;
            }
            foreach (var p in r.Players)
                if (means.TryGetValue(p.Id, out var m))
                {
                    // the means used most; the one used first of equals (the dictionary keeps the order they came in)
                    var top = m.OrderByDescending(e => e.Value).First().Key;
                    p.MainCause = top.Item1;
                    p.MainPiece = top.Item2;
                }
            if (!r.Complete) return r;

            // The best piece: the most ring-outs, then the fewest falls, then whoever reached that many first.
            var best = r.Players.Where(p => p.Knockouts > 0)
                .OrderByDescending(p => p.Knockouts).ThenBy(p => p.Falls).ThenBy(p => ReachedAt(lines, p.Id, p.Knockouts)).FirstOrDefault();
            r.Best = best != null ? best.Id : 0;

            var picks = new List<SwordAwardPick>();
            var last = lines.Count > 0 ? lines[lines.Count - 1] : default;
            if (lines.Count > 0 && last.Credit != 0 && r.Find(last.Credit) != null && winner >= 0)
                picks.Add(new SwordAwardPick { Award = SwordAward.FinishingBlow, Player = last.Credit, Cause = last.Cause, CausePiece = last.CausePiece });
            var wall = r.Players.OrderBy(p => p.Falls).ThenByDescending(p => p.Knockouts).ThenBy(p => p.Name, System.StringComparer.Ordinal).FirstOrDefault();
            // (only a real wall: once down at most)
            if (wall != null && wall.Falls <= 1 && r.Players.Count > 1 && lines.Count > 0)
                picks.Add(new SwordAwardPick { Award = SwordAward.IronWall, Player = wall.Id, Count = wall.Falls });
            var free = r.Players.Where(p => p.SelfFalls > 0).OrderByDescending(p => p.SelfFalls).ThenBy(p => p.Name, System.StringComparer.Ordinal).FirstOrDefault();
            if (free != null) picks.Add(new SwordAwardPick { Award = SwordAward.FreeFall, Player = free.Id, Count = free.SelfFalls });
            // the spares, for a card left empty
            if (firstAt >= 0)
                picks.Add(new SwordAwardPick { Award = SwordAward.FirstBlood, Player = lines[firstAt].Credit, Cause = lines[firstAt].Cause, CausePiece = lines[firstAt].CausePiece });
            var run = r.Players.Where(p => p.BestStreak >= 3).OrderByDescending(p => p.BestStreak).ThenBy(p => p.Name, System.StringComparer.Ordinal).FirstOrDefault();
            if (run != null) picks.Add(new SwordAwardPick { Award = SwordAward.Streak, Player = run.Id, Count = run.BestStreak });
            var breaker = r.Players.Where(p => p.WallBreaks > 0).OrderByDescending(p => p.WallBreaks).ThenBy(p => p.Name, System.StringComparer.Ordinal).FirstOrDefault();
            if (breaker != null) picks.Add(new SwordAwardPick { Award = SwordAward.WallBreaker, Player = breaker.Id, Count = breaker.WallBreaks });
            foreach (var pick in picks)
            {
                if (r.Awards.Count >= MaxAwards) break;
                r.Awards.Add(pick);
            }
            return r;
        }

        // The line at which this player made its n-th ring-out.
        static int ReachedAt(IList<SwordResultLine> lines, ulong id, int n)
        {
            int seen = 0;
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Credit == id && ++seen == n) return i;
            return int.MaxValue;
        }
    }
}
