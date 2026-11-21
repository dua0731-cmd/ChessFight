using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ChessFight.Network
{
    // R743 (user 11-21: "A", the mock-up https://claude.ai/artifact/6NgDdWLAxx2WaZLduqD2Cb; Docs/Architecture/UI.md §3-3):
    // the lobby's 순위 tab, on Steam leaderboards of the cumulative win rate. The rules the user set:
    //  - the rate over the matches this player ever finished, with no seasons ("한 달 시즌으로 하면 우리 팀이 이 게임을
    //    계속 개발할지 의문");
    //  - a board takes a player from 20 matches of its mode (the mock-up's 20; R744 tried 10 and R745 went back, user
    //    11-21 "20판으로 할게");
    //  - an equal rate puts the one with more matches first (it is in the score itself);
    //  - 30 days without a match of the board's mode keep the place and dim the line ("쉬는 중");
    //  - a match left is a loss (the record's own rule, RecordBook).
    // R745 (user 11-21, the limits of R743 that code alone mends):
    //  - only ranked matches count (RankedMatch): a room matchmaking made, no bots, two people a side or more. A room a
    //    player made and a match with bots stay in the 전적 tab and count for nothing here;
    //  - every player's whole count is kept on a board of its own (RankProgress): it comes back from there on a new PC,
    //    and it shows a Steam friend who is not on a board yet.
    // R746 (user 11-21: "전체 승률로 하면 안돼 폰 러쉬랑 소드파이트 순위를 구분 지어야 해 … 각각 20판씩 하면 그 후 순위에
    // 찍혀야 돼"): there is no board over both modes. Pawn Rush and Sword Fight each have their own, and a player is on
    // a mode's board from 20 ranked matches of that mode, whatever they have of the other. (R743 had a third board, 전체,
    // over both modes together; R745 made it ask for 20 of each - 40 in all - which is not what the user meant.)
    // Asked and left as they were (user 11-21): the order stays the plain rate (no confidence bound), a draw is not half
    // a win, a resting player keeps the place.
    // Each PC uploads its own line from its own book, so a downloaded line is checked against itself: its numbers have
    // to give its score.
    //
    // Pure rules and text, no Unity, no Steam: the Steam side (Bootstrap/SteamLeaderboards) uploads and downloads, the
    // Game side (Rankings) holds what the tab shows.
    public enum RankBoard : byte
    {
        PawnRush = 0,
        SwordFight = 1,
    }

    // One player's line on a board.
    public sealed class RankEntry
    {
        public ulong Id;
        // Steam's global rank on the board, from 1; 0 for a player who is not on it (Waiting).
        public int Rank;
        // The name the player chose (R84) as the line carries it; "" when it carries none (the Steam side then puts
        // the Steam name in).
        public string Name = "";
        // Ranked matches of the board's mode and the wins among them.
        public int Games, Wins;
        // The day (RankRules.Day) of the player's newest ranked match of the board's mode; 0 unknown.
        public int LastDay;
        // The piece the player plays most on this board.
        public PieceKind Piece;
        // This player's own line; a Steam friend's.
        public bool Self, Friend;
        // R745: a player not on the board yet (a friend's progress line, this player's own book): the matches that
        // count toward it and how many it asks for ("6/20판"). Both 0 for a line on the board.
        public int Have, Need;

        public bool Waiting => Need > 0;
        public int Score => RankRules.Score(Games, Wins);
    }

    public static class RankRules
    {
        public const int MinGames = 20;
        // R745 (user 11-21: "각 팀 2명 이상"): the people a side a match needs to count for the boards.
        public const int MinSide = 2;
        public const int RestDays = 30;
        // Steam's k_cLeaderboardDetailsMax: the ints a line may carry.
        public const int MaxDetails = 64;
        // R745: the board every player's whole count is kept on (RankProgress), whatever their matches.
        public const string ProgressBoard = "ChessFight_Progress";
        const int GamesCap = 99999;

        // R746: one board for each mode, none over both.
        public static readonly RankBoard[] Boards = { RankBoard.PawnRush, RankBoard.SwordFight };

        // The Steam leaderboard's name. App ID 480 is Valve's shared test app, so the names carry the game's own prefix
        // there; the release App ID gets the same names. R745: a name ends in the version of what its lines count
        // (RankDetails.Version): lines counted by an older rule - R743's "ChessFight_WinRate_PawnRush" and its like took
        // every online match - stay on their own boards, and never stand between two places on these (Steam numbers
        // the places over every line of a board, also the ones this build would not read).
        public static string BoardName(RankBoard board) =>
            "ChessFight_WinRate_" + (board == RankBoard.SwordFight ? "SwordFight" : "PawnRush") + "_v" + RankDetails.Version;

        // The GameModes key a board counts.
        public static string ModeKey(RankBoard board) => board == RankBoard.SwordFight ? GameModes.SwordFight.Key : GameModes.KingRush.Key;

        // The tab's name for it: "폰 러쉬", "소드 파이트".
        public static string Title(RankBoard board) => board == RankBoard.SwordFight ? GameModes.SwordFight.Name : GameModes.KingRush.Name;

        // The rate in tenths of a percent (0 to 1000), rounded half away from zero.
        public static int Permille(int wins, int games)
        {
            if (games <= 0) return 0;
            int p = (int)Math.Round(1000.0 * Math.Max(0, wins) / games, MidpointRounding.AwayFromZero);
            return Math.Min(1000, Math.Max(0, p));
        }

        // What goes on the board: the rate first, then the matches, so an equal rate sorts the one with more matches
        // first. At most 1000 * 100000 + 99999, inside Steam's int.
        public static int Score(int games, int wins) => Permille(wins, games) * 100000 + Math.Min(GamesCap, Math.Max(0, games));

        // Enough ranked matches of a mode for its board.
        public static bool Ranked(int games) => games >= MinGames;

        // R745: whether a started match counts for the boards, from what its room wrote when it started - so every PC
        // in it, and a player who comes back into it, reads the same answer. `matchmade`: matchmaking made the room
        // (not a room a player made, "방 생성"). `seats`: the size each team started with, bots and all ("3,3").
        // `starters`: the people it started with and their seats (Starters; bots are not written). It counts with
        // every seat a person's and MinSide people a side or more. While TestRulesInEveryBuild (R540) lets bots into
        // matchmaking's rooms, a match with one in it does not count.
        public static bool RankedMatch(bool matchmade, string seats, string starters)
        {
            if (!matchmade) return false;
            var sizes = Starters.DecodeCapacity(seats);
            if (sizes == null) return false;
            var people = Starters.Decode(starters);
            int white = people.Count(s => s.Team == 0), black = people.Count(s => s.Team == 1);
            return white == sizes[0] && black == sizes[1] && white >= MinSide && black >= MinSide;
        }

        // Whole days since 1970-01-01 UTC.
        public static int Day(double unixSeconds) =>
            unixSeconds <= 0 || double.IsNaN(unixSeconds) || double.IsInfinity(unixSeconds) ? 0 : (int)Math.Floor(unixSeconds / 86400.0);

        public static int DaysSince(int lastDay, int today) => lastDay <= 0 ? 0 : Math.Max(0, today - lastDay);

        // 30 days or more since the board's mode was last played.
        public static bool Resting(int lastDay, int today) => lastDay > 0 && today - lastDay >= RestDays;
    }

    // R745: what a board counts of a player - the ranked matches of its mode only (ModeTotals.RankedGames). R746: a
    // board takes a player from MinGames of them, and looks at nothing of the other mode.
    public struct RankCount
    {
        // Ranked matches of the board's mode, the wins and the draws among them.
        public int Games, Wins, Draws;
        // When the newest of those matches ended (seconds since 1970 UTC; 0 none).
        public double Last;

        public bool Ranked => RankRules.Ranked(Games);
        public int Losses => Games - Wins - Draws;
        // How far along ("6/20판"): the matches so far, no more than the board asks for, and the ones it asks for.
        public int Have => Math.Min(RankRules.MinGames, Math.Max(0, Games));
        public int Need => RankRules.MinGames;
        // Ranked matches still to go; 0 once the player is on the board.
        public int ToGo => Need - Have;

        public static RankCount Of(ModeTotals mode) => mode == null ? new RankCount()
            : new RankCount { Games = mode.RankedGames, Wins = mode.RankedWins, Draws = mode.RankedDraws, Last = mode.RankedLast };

        public static RankCount Of(RecordBook book, RankBoard board) => Of(book?.Mode(RankRules.ModeKey(board)));
    }

    // A line's details on the board: [version, matches, wins, last day, piece, name bytes, the name in UTF-8 four bytes
    // to an int]. The name is the chosen one (12 characters at most, 36 bytes), so a line is 15 ints at most.
    // R745 (version 2): the matches are the ranked ones of the board's mode. A version 1 line (R743: every online
    // match) no longer reads, and lies on a board of another name (RankRules.BoardName).
    public static class RankDetails
    {
        public const int Version = 2;
        const int Head = 6;

        public static int[] Pack(RankEntry line, string name)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(name ?? "");
            if (bytes.Length > (RankRules.MaxDetails - Head) * 4) bytes = new byte[0];
            var d = new int[Head + (bytes.Length + 3) / 4];
            d[0] = Version;
            d[1] = line.Games;
            d[2] = line.Wins;
            d[3] = Math.Max(0, line.LastDay);
            d[4] = (int)line.Piece;
            d[5] = bytes.Length;
            for (int i = 0; i < bytes.Length; i++) d[Head + i / 4] |= bytes[i] << (8 * (i % 4));
            return d;
        }

        // A downloaded line, or null when it does not hold together: another version, fewer than MinGames matches,
        // more wins than matches, or a score its numbers do not give. A name that does not read or is not a name the
        // name screen allows is dropped, not the line.
        public static RankEntry Read(ulong id, int rank, int score, int[] details, int count)
        {
            if (details == null || count < Head || count > details.Length || details[0] != Version) return null;
            int games = details[1], wins = details[2];
            if (!RankRules.Ranked(games) || wins < 0 || wins > games || RankRules.Score(games, wins) != score) return null;
            int piece = ChessPieces.IsValid(details[4]) ? details[4] : (int)PieceKind.Pawn;
            return new RankEntry
            {
                Id = id, Rank = Math.Max(0, rank), Name = RankNames.Read(details, Head - 1, count), Games = games, Wins = wins,
                LastDay = Math.Max(0, details[3]), Piece = (PieceKind)piece,
            };
        }
    }

    // A chosen name inside a line's details: its byte count at `at`, then the UTF-8 bytes four to an int.
    static class RankNames
    {
        public static string Read(int[] details, int at, int count)
        {
            int length = details[at], first = at + 1;
            if (length <= 0 || length > (count - first) * 4) return "";
            var bytes = new byte[length];
            for (int i = 0; i < length; i++) bytes[i] = (byte)(details[first + i / 4] >> (8 * (i % 4)));
            string name;
            try { name = new UTF8Encoding(false, true).GetString(bytes); }
            catch (ArgumentException) { return ""; }
            return PlayerNames.Showable(name) ? name : "";
        }
    }

    // This player's own line on a board, from their record book.
    public static class RankUpload
    {
        // Null while the player is not on the board (fewer than MinGames ranked matches of its mode: nothing goes up
        // then).
        public static RankEntry From(RecordBook book, RankBoard board)
        {
            if (book == null) return null;
            var c = RankCount.Of(book, board);
            if (!c.Ranked) return null;
            return new RankEntry
            {
                Games = c.Games, Wins = c.Wins, LastDay = RankRules.Day(c.Last), Piece = Favourite(book, RankRules.ModeKey(board)), Self = true,
            };
        }

        // The piece played most in a mode: over its recent matches (the book keeps no piece count by mode), over every
        // match when those have none (a book that came back from Steam has no recent matches). A tie goes to the
        // earlier piece.
        public static PieceKind Favourite(RecordBook book, string mode)
        {
            var counts = new int[ChessPieces.Count];
            bool any = false;
            if (mode != null)
                foreach (var r in book.Recent)
                    if (r.Mode == mode && ChessPieces.IsValid((int)r.Piece)) { counts[(int)r.Piece]++; any = true; }
            if (!any) Array.Copy(book.PieceGames, counts, Math.Min(counts.Length, book.PieceGames.Length));
            int best = 0;
            for (int i = 1; i < counts.Length; i++) if (counts[i] > counts[best]) best = i;
            return (PieceKind)best;
        }
    }

    // R745 (user 11-21: the record was each PC's own, and a new PC wrote a small count over Steam's): a player's whole
    // count as Steam keeps it, on a board of its own (RankRules.ProgressBoard) that every player is on whatever their
    // matches. Its two uses:
    //  - this player's own line brings the totals back on a new PC or after a reinstall (RestoreInto: a mode's count
    //    from Steam when Steam's is the larger; the recent list cannot come back). The Steam side reads it before it
    //    uploads anything, so a small book never goes up over a larger count;
    //  - a Steam friend's line shows them in the 친구 view before they are on a board ("6/20판").
    // The details: [version, Pawn Rush's 13, Sword Fight's 13, matches by piece, wins by piece, each mode's piece, name
    // bytes, name]. A mode's 13: matches, wins, losses, left, draws, ranked matches, ranked wins, ranked draws, last
    // day, last ranked day, then Pawn Rush's fastest crossing in milliseconds (-1 none), first places and matches off
    // the platform, or Sword Fight's falls caused, falls taken and the most caused in a match.
    public sealed class RankProgress
    {
        public const int Version = 1;
        const int PerMode = 13;
        const int Head = 1 + PerMode * 2 + ChessPieces.Count * 2 + 2 + 1;
        const int GamesCap = 10000000;

        public ulong Id;
        public string Name = "";
        public bool Self, Friend;
        public ModeTotals Rush = new ModeTotals(), Sword = new ModeTotals();
        public readonly int[] PieceGames = new int[ChessPieces.Count], PieceWins = new int[ChessPieces.Count];
        // The piece played most in each mode's recent matches when the line went up (the 친구 view's icon).
        public PieceKind RushPiece, SwordPiece;

        // Every match of both modes: the line's score on the progress board.
        public int Games => Rush.Games + Sword.Games;
        public int Score => Math.Min(GamesCap, Math.Max(0, Games));

        public RankCount Count(RankBoard board) => RankCount.Of(board == RankBoard.SwordFight ? Sword : Rush);

        public PieceKind Piece(RankBoard board) => board == RankBoard.SwordFight ? SwordPiece : RushPiece;

        // The player as a board's 친구 view shows one who is not on it: no rank, "6/20판".
        public RankEntry Waiting(RankBoard board)
        {
            var c = Count(board);
            return new RankEntry
            {
                Id = Id, Name = Name, Self = Self, Friend = Friend, Games = c.Games, Wins = c.Wins, LastDay = RankRules.Day(c.Last),
                Piece = Piece(board), Have = c.Have, Need = c.Need,
            };
        }

        public static RankProgress From(RecordBook book)
        {
            var p = new RankProgress { Self = true };
            if (book == null) return p;
            p.Rush = book.Mode(GameModes.KingRush.Key).Copy();
            p.Sword = book.Mode(GameModes.SwordFight.Key).Copy();
            Array.Copy(book.PieceGames, p.PieceGames, p.PieceGames.Length);
            Array.Copy(book.PieceWins, p.PieceWins, p.PieceWins.Length);
            p.RushPiece = RankUpload.Favourite(book, GameModes.KingRush.Key);
            p.SwordPiece = RankUpload.Favourite(book, GameModes.SwordFight.Key);
            return p;
        }

        public int[] Pack(string name)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(name ?? "");
            if (bytes.Length > (RankRules.MaxDetails - Head) * 4) bytes = new byte[0];
            var d = new int[Head + (bytes.Length + 3) / 4];
            d[0] = Version;
            Write(d, 1, Rush, true);
            Write(d, 1 + PerMode, Sword, false);
            int at = 1 + PerMode * 2;
            for (int i = 0; i < ChessPieces.Count; i++) { d[at + i] = PieceGames[i]; d[at + ChessPieces.Count + i] = PieceWins[i]; }
            at += ChessPieces.Count * 2;
            d[at] = (int)RushPiece;
            d[at + 1] = (int)SwordPiece;
            d[at + 2] = bytes.Length;
            for (int i = 0; i < bytes.Length; i++) d[Head + i / 4] |= bytes[i] << (8 * (i % 4));
            return d;
        }

        static void Write(int[] d, int at, ModeTotals t, bool rush)
        {
            d[at] = t.Games; d[at + 1] = t.Wins; d[at + 2] = t.Losses; d[at + 3] = t.Left; d[at + 4] = t.Draws;
            d[at + 5] = t.RankedGames; d[at + 6] = t.RankedWins; d[at + 7] = t.RankedDraws;
            d[at + 8] = RankRules.Day(t.Last); d[at + 9] = RankRules.Day(t.RankedLast);
            if (rush)
            {
                d[at + 10] = t.BestFinish >= 0 ? (int)Math.Min(int.MaxValue, Math.Round(t.BestFinish * 1000.0)) : -1;
                d[at + 11] = t.Firsts; d[at + 12] = t.NotFinished;
            }
            else { d[at + 10] = t.Knockouts; d[at + 11] = t.Falls; d[at + 12] = t.BestKnockouts; }
        }

        // A downloaded line, or null when it does not hold together: another version, a count below zero, results
        // that do not add up to the matches, more ranked matches than matches (or ranked wins than wins), or a score
        // that is not its matches.
        public static RankProgress Read(ulong id, int score, int[] details, int count)
        {
            if (details == null || count < Head || count > details.Length || details[0] != Version) return null;
            var p = new RankProgress { Id = id };
            if (!Read(details, 1, p.Rush, true) || !Read(details, 1 + PerMode, p.Sword, false) || p.Games > GamesCap || p.Score != score) return null;
            int at = 1 + PerMode * 2, total = p.Games;
            for (int i = 0; i < ChessPieces.Count; i++)
            {
                p.PieceGames[i] = Math.Min(total, Math.Max(0, details[at + i]));
                p.PieceWins[i] = Math.Min(p.PieceGames[i], Math.Max(0, details[at + ChessPieces.Count + i]));
            }
            at += ChessPieces.Count * 2;
            p.RushPiece = ChessPieces.IsValid(details[at]) ? (PieceKind)details[at] : PieceKind.Pawn;
            p.SwordPiece = ChessPieces.IsValid(details[at + 1]) ? (PieceKind)details[at + 1] : PieceKind.Pawn;
            p.Name = RankNames.Read(details, at + 2, count);
            return p;
        }

        static bool Read(int[] d, int at, ModeTotals t, bool rush)
        {
            for (int i = 0; i < 10; i++) if (d[at + i] < 0) return false;
            t.Games = d[at]; t.Wins = d[at + 1]; t.Losses = d[at + 2]; t.Left = d[at + 3]; t.Draws = d[at + 4];
            t.RankedGames = d[at + 5]; t.RankedWins = d[at + 6]; t.RankedDraws = d[at + 7];
            if ((long)t.Wins + t.Losses + t.Left + t.Draws != t.Games || t.RankedGames > t.Games ||
                (long)t.RankedWins + t.RankedDraws > t.RankedGames || t.RankedWins > t.Wins || t.RankedDraws > t.Draws) return false;
            // Only the day comes back: its first second.
            t.Last = d[at + 8] * 86400.0;
            t.RankedLast = d[at + 9] * 86400.0;
            if (rush)
            {
                t.BestFinish = d[at + 10] >= 0 ? d[at + 10] / 1000.0 : -1;
                t.Firsts = Math.Max(0, d[at + 11]); t.NotFinished = Math.Max(0, d[at + 12]);
            }
            else
            {
                t.Knockouts = Math.Max(0, d[at + 10]); t.Falls = Math.Max(0, d[at + 11]); t.BestKnockouts = Math.Max(0, d[at + 12]);
            }
            return true;
        }

        // Steam's count into this PC's book, mode by mode where Steam kept more matches (RecordBook.Restore). Returns
        // whether the book changed.
        public bool RestoreInto(RecordBook book)
        {
            if (book == null) return false;
            bool changed = book.Restore(GameModes.KingRush.Key, Rush);
            changed |= book.Restore(GameModes.SwordFight.Key, Sword);
            changed |= book.RestorePieces(PieceGames, PieceWins);
            return changed;
        }
    }

    // What the 순위 tab writes.
    public static class RankText
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // "51.6"; "—" before any match.
        public static string Rate(int wins, int games) =>
            games <= 0 ? "—" : (RankRules.Permille(wins, games) / 10.0).ToString("0.0", Inv);

        // "62판 · 32승".
        public static string Games(int games, int wins) => games + "판 · " + wins + "승";

        // "6/20판": the ranked matches that count toward a board, of the ones it asks for.
        public static string Progress(int have, int need) => have + "/" + need + "판";

        // "오늘", "어제", "3일 전", "쉬는 중 · 35일 전"; "" when unknown.
        public static string Last(int lastDay, int today)
        {
            if (lastDay <= 0) return "";
            int days = RankRules.DaysSince(lastDay, today);
            if (days >= RankRules.RestDays) return "쉬는 중 · " + days + "일 전";
            return days == 0 ? "오늘" : days == 1 ? "어제" : days + "일 전";
        }

        // "상위 18%" (rounded up, at least 1%); "" without a rank.
        public static string Top(int rank, int total) =>
            rank <= 0 || total <= 0 ? "" : "상위 " + Math.Max(1, Math.Min(100, (int)Math.Ceiling(100.0 * rank / total))) + "%";

        // What a player short of a board still needs: "순위 판 14판 더 하면 순위에 올라가요".
        public static string Need(RankCount count) => "순위 판 " + Math.Max(1, count.ToGo) + "판 더 하면 순위에 올라가요";
    }
}
