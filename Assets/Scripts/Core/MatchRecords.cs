using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ChessFight.Network
{
    // R736 (user 11-21, the lobby's 전적 tab, mock-up A; Docs/Architecture/UI.md "전적"): this player's own record of
    // matches, kept on this PC. Every online match counts (random matchmaking and created rooms alike); spectators and
    // the offline test scenes do not. A player who leaves a started match gets it as "나감", counted a loss; the others
    // in it get their win or loss as usual. Coming back into the same match and seeing its end replaces the "나감".
    //
    // R745 (the 순위 tab's limits, user 11-21):
    //  - a match is written as "나감" the moment it starts (RecordBook.Hold) and its end takes that place, so a game
    //    that is killed or crashes mid-match leaves the "나감" behind as well;
    //  - a match that counts for the 순위 boards is marked (MatchRecord.Ranked, RankRules.RankedMatch) and counted
    //    apart in its mode's totals: the boards read only those, this tab and the 도감 every match as before;
    //  - the totals are backed up on Steam (RankProgress) and come back from there on a new PC (RecordBook.Restore).
    //
    // Pure data and rules, no Unity: the Game side (MatchRecords in ChessFight.Game) arms a match, writes these and
    // keeps the encoded book in PlayerPrefs.
    public enum RecordResult : byte
    {
        Win = 0,
        Loss = 1,
        Left = 2,
        Draw = 3,
    }

    // How a Sword Fight match ended: a side reached the target, the clock ran out, or the sudden death decided it.
    public enum SwordEnd : byte
    {
        Target = 0,
        TimeUp = 1,
        SuddenDeath = 2,
    }

    public sealed class MatchRecord
    {
        // The match's room (Steam lobby id): the same match is written once (a "나감" may be replaced by its result).
        public ulong Match;
        // GameModes key.
        public string Mode = "";
        public RecordResult Result;
        // The piece this player ended the match as (Pawn Rush: after any promotion), or played (Sword Fight).
        public PieceKind Piece;
        // Players on this player's side and on the other when it ended (or when this player left).
        public int Ours, Theirs;
        // When it ended: seconds since 1970-01-01 UTC.
        public double When;

        // Pawn Rush: place over both teams among those who reached the crown platform (1-based; 0 = did not), the race
        // clock then, how far from the line this player still was (non-finishers, -1 unknown), and whether this
        // crossing was one of the winning team's heads.
        public int Place;
        public double FinishSeconds = -1;
        public float MetersLeft = -1;
        public bool CountsToWin;

        // Sword Fight: the score (this player's side first), the falls this player caused (the feed's credit) and took.
        public int ScoreOurs, ScoreTheirs, Knockouts, Falls;
        public SwordEnd End;

        // R742: what the match held for this player (the 전적 tab's double-click); null for a record from before R742.
        public MatchDetail Detail;

        // R745: the match counts for the 순위 boards (RankRules.RankedMatch: matchmaking made its room, people only, two
        // a side or more), settled when it started. False for every record from before R745.
        public bool Ranked;

        public bool CountsAsLoss => Result == RecordResult.Loss || Result == RecordResult.Left;
    }

    // One mode's totals over every match ever written (the recent list keeps only the last RecordBook.Kept).
    public sealed class ModeTotals
    {
        public int Games, Wins, Losses, Left, Draws;
        // Pawn Rush: the fastest crossing (-1 none yet), first places, matches ended off the platform.
        public double BestFinish = -1;
        public int Firsts, NotFinished;
        // Sword Fight: falls caused and taken, the most caused in one match.
        public int Knockouts, Falls, BestKnockouts;
        // R743: when the newest match ended (seconds since 1970 UTC; 0 for a book from before R743 until its next
        // match): the 순위 tab's "쉬는 중".
        public double Last;
        // R745: of these matches, the ones that count for the 순위 boards (MatchRecord.Ranked) - how many, the wins and
        // the draws among them, and when the newest ended. The 순위 tab reads only these. All 0 for a book from before
        // R745: none of its matches count.
        public int RankedGames, RankedWins, RankedDraws;
        public double RankedLast;

        // Losses with the left matches, as the screen counts them ("32승 30패").
        public int AllLosses => Losses + Left;
        // The same among the ranked matches (a ranked match left is a loss too).
        public int RankedLosses => RankedGames - RankedWins - RankedDraws;

        public ModeTotals Copy() => (ModeTotals)MemberwiseClone();

        public void AddTo(ModeTotals sum)
        {
            sum.Games += Games; sum.Wins += Wins; sum.Losses += Losses; sum.Left += Left; sum.Draws += Draws;
            sum.Last = Math.Max(sum.Last, Last);
            sum.RankedGames += RankedGames; sum.RankedWins += RankedWins; sum.RankedDraws += RankedDraws;
            sum.RankedLast = Math.Max(sum.RankedLast, RankedLast);
            if (BestFinish >= 0 && (sum.BestFinish < 0 || BestFinish < sum.BestFinish)) sum.BestFinish = BestFinish;
            sum.Firsts += Firsts; sum.NotFinished += NotFinished;
            sum.Knockouts += Knockouts; sum.Falls += Falls; sum.BestKnockouts = Math.Max(sum.BestKnockouts, BestKnockouts);
        }
    }

    public sealed class RecordBook
    {
        // Matches the recent list keeps (the screen pages through them).
        public const int Kept = 50;
        const string Version = "CFR1";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // Newest first.
        public readonly List<MatchRecord> Recent = new List<MatchRecord>();
        readonly Dictionary<string, ModeTotals> modes = new Dictionary<string, ModeTotals>();
        // Matches and wins by piece, both modes together (the 도감's "이 기물로 n판 m승").
        public readonly int[] PieceGames = new int[ChessPieces.Count], PieceWins = new int[ChessPieces.Count];

        // A mode's totals (empty when it has none).
        public ModeTotals Mode(string key) => key != null && modes.TryGetValue(key, out var t) ? t : new ModeTotals();

        // Every mode together.
        public ModeTotals All
        {
            get
            {
                var sum = new ModeTotals();
                foreach (var t in modes.Values) t.AddTo(sum);
                return sum;
            }
        }

        // Writes one match. A match already written is not written again, except that a "나감" gives way to the
        // match's real end (the player came back into it). Returns whether the book changed.
        public bool Add(MatchRecord r)
        {
            if (r == null || string.IsNullOrEmpty(r.Mode) || r.Mode.IndexOf('|') >= 0 || r.Mode.IndexOf('\n') >= 0) return false;
            int at = r.Match != 0 ? Recent.FindIndex(x => x.Match == r.Match && x.Mode == r.Mode) : -1;
            if (at >= 0)
            {
                if (Recent[at].Result != RecordResult.Left || r.Result == RecordResult.Left) return false;
                Count(Recent[at], -1);
                Recent.RemoveAt(at);
            }
            Count(r, 1);
            Recent.Insert(0, r);
            if (Recent.Count > Kept) Recent.RemoveRange(Kept, Recent.Count - Kept);
            return true;
        }

        // R745: a match under way is held as "나감" from its start, so one that never reaches its end on this PC (the
        // game killed, a crash, the power) stays one. A match not written yet gets `left`; a match held already gets
        // it in the old one's place, where it stands in the list (the piece and the sides as they are now); a match
        // whose end is written is left alone. Its end then takes the place through Add. Returns whether the book
        // changed.
        public bool Hold(MatchRecord left)
        {
            if (left == null || left.Result != RecordResult.Left || left.Match == 0) return false;
            int at = Recent.FindIndex(x => x.Match == left.Match && x.Mode == left.Mode);
            if (at < 0) return Add(left);
            if (Recent[at].Result != RecordResult.Left) return false;
            Count(Recent[at], -1);
            Count(left, 1);
            Recent[at] = left;
            return true;
        }

        // R745: a mode's totals as Steam kept them (RankProgress, the progress board) take the place of this book's
        // when they hold more matches: a new PC, a reinstall, a match played on another PC. Fewer or as many leave the
        // book as it is, so a smaller count never writes over a larger one. The recent list stays this PC's own - Steam
        // keeps no list. Returns whether the book changed.
        public bool Restore(string key, ModeTotals kept)
        {
            if (string.IsNullOrEmpty(key) || kept == null || key.IndexOf('|') >= 0 || key.IndexOf('\n') >= 0) return false;
            var mine = Mode(key);
            if (kept.Games <= mine.Games) return false;
            var t = kept.Copy();
            t.Last = Math.Max(mine.Last, kept.Last);
            t.RankedLast = Math.Max(mine.RankedLast, kept.RankedLast);
            modes[key] = t;
            return true;
        }

        // The same for the matches and wins by piece (both modes together): Steam's, when it kept more matches.
        public bool RestorePieces(int[] games, int[] wins)
        {
            if (games == null || wins == null || games.Length < PieceGames.Length || wins.Length < PieceWins.Length) return false;
            if (games.Take(PieceGames.Length).Sum() <= PieceGames.Sum()) return false;
            Array.Copy(games, PieceGames, PieceGames.Length);
            Array.Copy(wins, PieceWins, PieceWins.Length);
            return true;
        }

        void Count(MatchRecord r, int sign)
        {
            if (!modes.TryGetValue(r.Mode, out var t)) modes[r.Mode] = t = new ModeTotals();
            t.Games += sign;
            if (sign > 0 && r.When > t.Last) t.Last = r.When;
            switch (r.Result)
            {
                case RecordResult.Win: t.Wins += sign; break;
                case RecordResult.Loss: t.Losses += sign; break;
                case RecordResult.Left: t.Left += sign; break;
                default: t.Draws += sign; break;
            }
            // R745: the same match among the ranked ones, when it is one.
            if (r.Ranked)
            {
                t.RankedGames += sign;
                if (r.Result == RecordResult.Win) t.RankedWins += sign;
                else if (r.Result == RecordResult.Draw) t.RankedDraws += sign;
                if (sign > 0 && r.When > t.RankedLast) t.RankedLast = r.When;
            }
            int p = (int)r.Piece;
            if (ChessPieces.IsValid(p))
            {
                PieceGames[p] += sign;
                if (r.Result == RecordResult.Win) PieceWins[p] += sign;
            }
            if (r.Result == RecordResult.Left) return;
            // What only a played-out match brings. Only a "나감" is ever taken back, so the bests never need undoing.
            if (r.Mode == GameModes.SwordFight.Key)
            {
                t.Knockouts += sign * r.Knockouts;
                t.Falls += sign * r.Falls;
                if (sign > 0) t.BestKnockouts = Math.Max(t.BestKnockouts, r.Knockouts);
            }
            else
            {
                if (r.Place == 1) t.Firsts += sign;
                if (r.Place <= 0) t.NotFinished += sign;
                if (sign > 0 && r.Place > 0 && r.FinishSeconds >= 0 && (t.BestFinish < 0 || r.FinishSeconds < t.BestFinish))
                    t.BestFinish = r.FinishSeconds;
            }
        }

        // ---------- kept as text (PlayerPrefs) ----------

        public string Encode()
        {
            var b = new StringBuilder(256 + Recent.Count * 64);
            b.Append(Version).Append('\n');
            foreach (var pair in modes.OrderBy(m => m.Key, StringComparer.Ordinal))
            {
                var t = pair.Value;
                b.Append("M|").Append(pair.Key).Append('|').Append(t.Games).Append('|').Append(t.Wins).Append('|').Append(t.Losses)
                 .Append('|').Append(t.Left).Append('|').Append(t.Draws).Append('|').Append(t.BestFinish.ToString("0.###", Inv))
                 .Append('|').Append(t.Firsts).Append('|').Append(t.NotFinished).Append('|').Append(t.Knockouts).Append('|').Append(t.Falls)
                 .Append('|').Append(t.BestKnockouts).Append('|').Append(t.Last.ToString("0", Inv))
                 // R745: the ranked matches among them, after everything an older build reads.
                 .Append('|').Append(t.RankedGames).Append('|').Append(t.RankedWins).Append('|').Append(t.RankedDraws)
                 .Append('|').Append(t.RankedLast.ToString("0", Inv)).Append('\n');
            }
            b.Append("P|").Append(string.Join(",", PieceGames)).Append('|').Append(string.Join(",", PieceWins)).Append('\n');
            // Oldest first, so decoding can add them in order.
            for (int i = Recent.Count - 1; i >= 0; i--)
            {
                var r = Recent[i];
                b.Append("R|").Append(r.Match.ToString(Inv)).Append('|').Append(r.Mode).Append('|').Append((int)r.Result).Append('|').Append((int)r.Piece)
                 .Append('|').Append(r.Ours).Append('|').Append(r.Theirs).Append('|').Append(r.When.ToString("0", Inv))
                 .Append('|').Append(r.Place).Append('|').Append(r.FinishSeconds.ToString("0.###", Inv)).Append('|').Append(r.MetersLeft.ToString("0.#", Inv))
                 .Append('|').Append(r.CountsToWin ? 1 : 0).Append('|').Append(r.ScoreOurs).Append('|').Append(r.ScoreTheirs)
                 .Append('|').Append(r.Knockouts).Append('|').Append(r.Falls).Append('|').Append((int)r.End)
                 .Append('|').Append(r.Detail != null ? r.Detail.Pack() : "").Append('|').Append(r.Ranked ? 1 : 0).Append('\n');
            }
            return b.ToString();
        }

        // A book from its text; an empty book for nothing, another version or text that is not one. A line that does not
        // read is skipped, not the whole book.
        public static RecordBook Decode(string text)
        {
            var book = new RecordBook();
            if (string.IsNullOrEmpty(text)) return book;
            var lines = text.Replace("\r", "").Split('\n');
            if (lines.Length == 0 || lines[0] != Version) return book;
            var records = new List<MatchRecord>();
            for (int i = 1; i < lines.Length; i++)
            {
                var f = lines[i].Split('|');
                if (f.Length == 0) continue;
                switch (f[0])
                {
                    case "M":
                        if (f.Length < 13 || string.IsNullOrEmpty(f[1])) break;
                        var t = new ModeTotals();
                        if (!Int(f[2], out t.Games) || !Int(f[3], out t.Wins) || !Int(f[4], out t.Losses) || !Int(f[5], out t.Left) ||
                            !Int(f[6], out t.Draws) || !Num(f[7], out t.BestFinish) || !Int(f[8], out t.Firsts) || !Int(f[9], out t.NotFinished) ||
                            !Int(f[10], out t.Knockouts) || !Int(f[11], out t.Falls) || !Int(f[12], out t.BestKnockouts)) break;
                        // R743: the newest match's time; a line from before it has none.
                        if (f.Length >= 14 && Num(f[13], out double last) && last > 0) t.Last = last;
                        // R745: the ranked matches among them; a line from before it has none, and numbers that do not
                        // hold together (more than the matches, more wins and draws than there are) count as none.
                        if (f.Length >= 18 && Int(f[14], out int ranked) && Int(f[15], out int rankedWins) && Int(f[16], out int rankedDraws) &&
                            Num(f[17], out double rankedLast) && ranked >= 0 && ranked <= t.Games && rankedWins >= 0 && rankedDraws >= 0 &&
                            rankedWins + rankedDraws <= ranked)
                        {
                            t.RankedGames = ranked; t.RankedWins = rankedWins; t.RankedDraws = rankedDraws;
                            t.RankedLast = Math.Max(0, rankedLast);
                        }
                        book.modes[f[1]] = t;
                        break;
                    case "P":
                        if (f.Length < 3) break;
                        Ints(f[1], book.PieceGames);
                        Ints(f[2], book.PieceWins);
                        break;
                    case "R":
                        if (f.Length < 17) break;
                        var r = new MatchRecord { Mode = f[2] };
                        if (!ulong.TryParse(f[1], NumberStyles.None, Inv, out r.Match) || string.IsNullOrEmpty(r.Mode)) break;
                        if (!Int(f[3], out int result) || result < 0 || result > 3) break;
                        if (!Int(f[4], out int piece) || !ChessPieces.IsValid(piece)) break;
                        if (!Int(f[5], out r.Ours) || !Int(f[6], out r.Theirs) || !Num(f[7], out r.When) || !Int(f[8], out r.Place) ||
                            !Num(f[9], out r.FinishSeconds) || !Num(f[10], out double meters) || !Int(f[11], out int counts) ||
                            !Int(f[12], out r.ScoreOurs) || !Int(f[13], out r.ScoreTheirs) || !Int(f[14], out r.Knockouts) ||
                            !Int(f[15], out r.Falls) || !Int(f[16], out int end) || end < 0 || end > 2) break;
                        r.Result = (RecordResult)result;
                        r.Piece = (PieceKind)piece;
                        r.MetersLeft = (float)meters;
                        r.CountsToWin = counts == 1;
                        r.End = (SwordEnd)end;
                        if (f.Length >= 18) r.Detail = MatchDetail.Unpack(f[17]);   // R742; records before it have none
                        if (f.Length >= 19) r.Ranked = f[18] == "1";                // R745; records before it are not ranked
                        records.Add(r);
                        break;
                }
            }
            // Oldest first in the text: the newest ends up first. The totals are the text's own, not recounted.
            for (int i = 0; i < records.Count; i++) book.Recent.Insert(0, records[i]);
            if (book.Recent.Count > Kept) book.Recent.RemoveRange(Kept, book.Recent.Count - Kept);
            return book;
        }

        static bool Int(string s, out int value) => int.TryParse(s, NumberStyles.AllowLeadingSign, Inv, out value);
        static bool Num(string s, out double value) =>
            double.TryParse(s, NumberStyles.Float, Inv, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
        static void Ints(string s, int[] into)
        {
            var parts = s.Split(',');
            for (int i = 0; i < into.Length && i < parts.Length; i++) Int(parts[i], out into[i]);
        }
    }

    // Records from what each mode knows at its end.
    public static class RecordBuild
    {
        // This player's Pawn Rush: their line in the result (null when they have none: a spectator).
        public static MatchRecord PawnRush(MatchResult result, MatchDetail detail = null)
        {
            var self = result?.Players.FirstOrDefault(p => p.Self);
            if (self == null) return null;
            var r = new MatchRecord
            {
                Mode = GameModes.KingRush.Key,
                Result = result.WinningTeam < 0 ? RecordResult.Draw : self.Team == result.WinningTeam ? RecordResult.Win : RecordResult.Loss,
                Piece = self.Piece,
                Ours = result.Players.Count(p => p.Team == self.Team),
                Theirs = result.Players.Count(p => p.Team != self.Team),
                MetersLeft = self.Finished ? -1 : self.MetersLeft,
            };
            // R742: the detail (what the match logged for this player) gets every player's line and the match's end.
            var d = detail ?? new MatchDetail();
            d.Players.Clear();
            d.Duration = result.MatchSeconds;
            var order = result.FinishOrder();
            foreach (var line in order)
            {
                if (line.Clinched) d.Clincher = line.Player.Name;
                if (line.Player != self) continue;
                r.Place = line.Place;
                r.FinishSeconds = self.FinishSeconds;
                r.CountsToWin = line.CountsToWin;
            }
            // Ours first, then by place (the finishers in crossing order), the rest by name.
            foreach (var p in result.Players.OrderBy(p => p.Team == self.Team ? 0 : 1)
                                            .ThenBy(p => p.Finished ? p.FinishSeconds : double.MaxValue)
                                            .ThenBy(p => p.Name, StringComparer.Ordinal))
            {
                var line = order.FirstOrDefault(l => l.Player == p);
                d.Add(new DetailPlayer
                {
                    Name = p.Name, Ours = p.Team == self.Team, Self = p == self, Piece = p.Piece,
                    Place = line.Player == p ? line.Place : 0, Seconds = p.Finished ? p.FinishSeconds : -1,
                });
            }
            r.Detail = d;
            return r;
        }

        // This player's Sword Fight: their side (0 white, 1 black), the score, the winner (-1 a draw), the match clock at
        // the end against its length, the piece and the falls they caused and took.
        public static MatchRecord SwordFight(int team, int white, int black, int winner, double clock, double duration,
                                             PieceKind piece, int ours, int theirs, int knockouts, int falls, MatchDetail detail = null)
        {
            // R742: the detail (the match's falls and this player's skills, logged as it went; the players' line-up the
            // match fills) gets the match's end.
            if (detail != null) detail.Duration = clock;
            return new MatchRecord
            {
                Detail = detail,
                Mode = GameModes.SwordFight.Key,
                Result = winner < 0 ? RecordResult.Draw : winner == team ? RecordResult.Win : RecordResult.Loss,
                Piece = piece,
                Ours = ours,
                Theirs = theirs,
                ScoreOurs = team == 1 ? black : white,
                ScoreTheirs = team == 1 ? white : black,
                End = clock < duration - 0.01 ? SwordEnd.Target : clock > duration + 0.01 ? SwordEnd.SuddenDeath : SwordEnd.TimeUp,
                Knockouts = Math.Max(0, knockouts),
                Falls = Math.Max(0, falls),
            };
        }

        // The "나감" of a match this player left before its end.
        public static MatchRecord Left(string mode, PieceKind piece, int ours, int theirs, MatchDetail detail = null) =>
            new MatchRecord { Mode = mode, Result = RecordResult.Left, Piece = piece, Ours = ours, Theirs = theirs, Detail = detail };
    }

    // What the 전적 tab writes for a record and a total.
    public static class RecordText
    {
        // "52%"; "—" before any match.
        public static string Rate(int wins, int games) =>
            games <= 0 ? "—" : (int)Math.Round(100.0 * wins / games, MidpointRounding.AwayFromZero) + "%";

        public static string Badge(RecordResult result)
        {
            switch (result)
            {
                case RecordResult.Win: return Loc.T("승");
                case RecordResult.Loss: return Loc.T("패");
                case RecordResult.Left: return Loc.T("탈주");
                default: return Loc.T("무");
            }
        }

        public static string ModeName(string key) => Loc.T(GameModes.Resolve(key).Name);

        // "6 대 6".
        public static string Size(MatchRecord r) => Loc.F("{0} 대 {1}", r.Ours, r.Theirs);

        // The line in big type: "2위 · 4:21", "도착 못 함", "7 : 10 · 시간 종료"; nothing for a match left (R804, user
        // 11-23: "탈주 된 것에는 경기 중 나감 패배로 쓰지마 아무것도 쓰지마" - the badge says 탈주; "시간 표시 하고 내가
        // 몇등에 들었는 지만 간단하게").
        public static string Headline(MatchRecord r)
        {
            if (r.Result == RecordResult.Left) return "";
            if (r.Mode == GameModes.SwordFight.Key)
            {
                string score = r.ScoreOurs + " : " + r.ScoreTheirs;
                return r.End == SwordEnd.TimeUp ? Loc.F("{0} · 시간 종료", score) : r.End == SwordEnd.SuddenDeath ? Loc.F("{0} · 서든데스", score) : score;
            }
            return r.Place > 0 ? Loc.F("{0}위 · {1}", r.Place, MatchResult.Clock(r.FinishSeconds)) : Loc.T("도착 못 함");
        }

        // The small line under it: nothing for a match left or a place reached (R804).
        public static string Sub(MatchRecord r)
        {
            if (r.Result == RecordResult.Left) return "";
            if (r.Mode == GameModes.SwordFight.Key) return Loc.F("내가 떨어뜨림 {0} · 떨어짐 {1}", r.Knockouts, r.Falls);
            if (r.Place > 0) return "";
            // R741 (user 11-21: "거리 몇 미터 이런건 빼"): no distance to the line; MetersLeft stays in the record unshown.
            return r.Result == RecordResult.Win ? Loc.T("우리 팀 승리") : "";
        }

        // "오늘 21:40", "어제 22:30", "11-19 21:02", in the PC's own time (`offset` from UTC).
        public static string When(double when, DateTime nowLocal, TimeSpan offset)
        {
            if (when <= 0 || double.IsNaN(when) || double.IsInfinity(when)) return "";
            var at = DateTimeOffset.FromUnixTimeSeconds((long)when).ToOffset(offset).DateTime;
            string clock = at.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (at.Date == nowLocal.Date) return Loc.F("오늘 {0}", clock);
            if (at.Date == nowLocal.Date.AddDays(-1)) return Loc.F("어제 {0}", clock);
            return at.ToString("MM-dd", CultureInfo.InvariantCulture) + " " + clock;
        }
    }
}
