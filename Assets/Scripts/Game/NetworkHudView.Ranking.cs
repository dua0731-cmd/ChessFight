using System.Collections.Generic;
using System.Globalization;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // The 순위 list's views: the global list a page at a time, this player's neighbours (R745), their Steam friends.
    public enum RankScope { Global, Around, Friends }

    // R743 (user 11-21: "A", the mock-up https://claude.ai/artifact/6NgDdWLAxx2WaZLduqD2Cb board "A안"): the top bar's
    // fourth tab, 순위 (Docs/Architecture/UI.md §3-3).
    //  - Left: this player's rank on the board shown (or how many ranked matches are still to go), their rank on each
    //    board (a row picks that board), the rules.
    //  - Right: the board - the global list ten lines a page, this player's line with the three above and below it,
    //    or their Steam friends - with this player's own line held under it when it is not among the lines shown; a
    //    line 30 days without a match of the board's mode is dimmed.
    // The boards are Steam's (Rankings, filled by Bootstrap/SteamLeaderboards). This player's own rate and matches are
    // the record book's ranked ones (RankCount), so they are right before the board has the newest match.
    // R745: 내 주변, the pages past the tenth place, the Steam profile pictures and the friends who are not on a board
    // yet ("6/20판", from the progress board).
    // R746 (user 11-21: "전체 승률로 하면 안돼 폰 러쉬랑 소드파이트 순위를 구분 지어야 해"): two boards, Pawn Rush's and
    // Sword Fight's - no 전체 tab, no rank over both modes; each takes a player from 20 ranked matches of its own mode.
    public sealed partial class NetworkHudView
    {
        VisualElement rankPage;
        RankBoard rankBoard = RankBoard.PawnRush;
        RankScope rankScope = RankScope.Global;
        int rankPageAt;

        Label rkMineKicker, rkMineRank, rkMineOf, rkMineRate, rkMineGames, rkMineNote;
        VisualElement rkMineBar, rkMineFill;
        // One of each for a board (RankRules.Boards).
        readonly Button[] rkModeRows = new Button[RankRules.Boards.Length];
        readonly Label[] rkModeLine = new Label[RankRules.Boards.Length];
        readonly Button[] rkTabs = new Button[RankRules.Boards.Length];
        readonly Button[] rkScopes = new Button[3];
        VisualElement rkPager;
        Button rkPrev, rkNext;
        Label rkPageText;
        sealed class RankRow
        {
            public VisualElement Root, Icon, Fill, Face;
            public Label Rank, Name, Tag, Sub, Rate, Games, Last, Letter;
        }
        readonly List<RankRow> rkRows = new List<RankRow>();
        RankRow rkPin;
        VisualElement rkPinBox;
        Label rkStatus, rkStatusSub, rkFootLeft;
        // A line of the view and the place it shows (0: "-", not on the board).
        readonly List<(RankEntry entry, int place)> rkLines = new List<(RankEntry, int)>(), rkFriendLines = new List<(RankEntry, int)>();

        public RankBoard RankingBoard => rankBoard;
        public RankScope RankingScope => rankScope;
        public int RankingPage => rankPageAt;

        void BuildRanking()
        {
            var left = Element("rk-left", rankPage);
            var mine = Element("lt-card rk-mine", left);
            rkMineKicker = Text("", "a-kicker", mine);
            var top = Element("rk-mine-row", mine);
            rkMineRank = Text("", "rk-mine-rank", top, true);
            rkMineOf = Text("", "rk-mine-of", top, true);
            var texts = Element("rk-mine-texts", top);
            rkMineRate = Text("", "rk-mine-rate", texts);
            rkMineGames = Text("", "rk-mine-games", texts);
            rkMineBar = Element("rk-mine-bar", mine);
            rkMineFill = Element("rk-mine-fill", rkMineBar);
            rkMineNote = Text("", "rk-mine-note", mine);

            var modes = Element("lt-card rk-modes", left);
            Text(Loc.T("모드별 내 순위"), "a-label rk-modes-label", modes);
            for (int i = 0; i < RankRules.Boards.Length; i++)
            {
                var board = RankRules.Boards[i];
                rkModeRows[i] = Pressable("", "rk-mode-row", modes, () => SelectRanking(board, rankScope));
                var name = Element("row", rkModeRows[i]);
                Element(board == RankBoard.SwordFight ? "cx-dot cx-dot-sf" : "cx-dot cx-dot-pr", name);
                Text(Loc.T(RankRules.Title(board)), "rk-mode-name", name);
                rkModeLine[i] = Text("", "rk-mode-line", rkModeRows[i]);
            }

            var rules = Element("lt-card rk-rules", left);
            Text(Loc.T("순위 규칙"), "a-label", rules);
            Text(Loc.T("· 폰 러쉬와 소드 파이트는 순위를 따로 집계합니다"), "rk-rule", rules);
            Text(Loc.F("· 모드별로 순위 판을 {0}판 이상 하면 순위에 오릅니다", RankRules.MinGames), "rk-rule", rules);
            Text(Loc.F("· 순위 판: 랜덤 매칭 · 봇 없음 · {0} 대 {0} 이상", RankRules.MinSide), "rk-rule", rules);
            Text(Loc.T("· 누적 승률 순이며, 같으면 판수가 많은 쪽이 위입니다"), "rk-rule", rules);
            Text(Loc.T("· 시즌 없이 누적되며, 이탈하면 패배로 처리됩니다"), "rk-rule", rules);
            Text(Loc.F("· {0}일간 쉬어도 순위는 유지되며 '쉬는 중'으로 표시됩니다", RankRules.RestDays), "rk-rule rk-rule-dim", rules);

            var card = Element("lt-card rk-board", rankPage, PickingMode.Position);
            var head = Element("rk-head", card);
            Text(Loc.T("순위표"), "rk-title", head, true);
            for (int i = 0; i < RankRules.Boards.Length; i++)
            {
                var board = RankRules.Boards[i];
                rkTabs[i] = Pressable(Loc.T(RankRules.Title(board)), "rk-tab", head, () => SelectRanking(board, rankScope));
            }
            Element("grow", head);
            // R745: the pages of the list shown (the global list past the tenth place, a long friend list).
            rkPager = Element("rc-pager rk-pager", head);
            rkPrev = Pressable("‹", "rc-page-btn", rkPager, () => TurnRanking(-1));
            rkPageText = Text("1 / 1", "rc-page", rkPager);
            rkNext = Pressable("›", "rc-page-btn", rkPager, () => TurnRanking(1));
            var seg = Element("rk-seg", head);
            string[] scopes = { "전체", "내 주변", "친구" };
            for (int i = 0; i < 3; i++)
            {
                var scope = (RankScope)i;
                rkScopes[i] = Pressable(Loc.T(scopes[i]), "rk-scope", seg, () => SelectRanking(rankBoard, scope));
            }

            var cols = Element("rk-cols", card);
            Text(Loc.T("순위"), "rk-col rk-c-rank", cols);
            Element("rk-c-icon", cols);
            Text(Loc.T("플레이어 · 가장 많이 쓴 기물"), "rk-col rk-c-name", cols);
            Text(Loc.T("누적 승률"), "rk-col rk-c-rate", cols);
            Text(Loc.T("순위 판 · 승"), "rk-col rk-c-games", cols);
            Text(Loc.T("마지막 경기"), "rk-col rk-c-last", cols);

            var rows = Element("rk-rows", card);
            for (int i = 0; i < Rankings.PageSize; i++) rkRows.Add(RankLine(rows));
            rkStatus = Text("", "rk-status", rows);
            rkStatusSub = Text("", "rk-status-sub", rows);
            Element("grow", card);
            rkPinBox = Element("rk-pin", card);
            Text("···", "rk-dots", rkPinBox);
            rkPin = RankLine(rkPinBox);
            var foot = Element("rk-foot", card);
            rkFootLeft = Text("", "rk-foot-text", foot);
            Text(Loc.T("Steam 순위표 · 경기가 끝날 때마다 갱신됩니다"), "rk-foot-text", foot);
        }

        RankRow RankLine(VisualElement parent)
        {
            var row = new RankRow { Root = Element("rk-row", parent) };
            var rank = Element("rk-c-rank rk-rank-cell", row.Root);
            row.Rank = Text("", "rk-medal", rank, true);
            row.Icon = Element("rk-c-icon rk-icon", row.Root);
            var name = Element("rk-c-name rk-name-cell", row.Root);
            // R745: the player's Steam profile picture; a coloured initial until it comes (or without Steam).
            row.Face = Element("rk-face", name);
            row.Letter = Text("", "rk-face-letter", row.Face);
            row.Name = Text("", "rk-name", name);
            row.Tag = Text("", "rk-tag", name);
            row.Sub = Text("", "rk-sub", name);
            var rate = Element("rk-c-rate rk-rate-cell", row.Root);
            var track = Element("rk-track", rate);
            row.Fill = Element("rk-fill", track);
            row.Rate = Text("", "rk-rate", rate);
            row.Games = Text("", "rk-c-games rk-games", row.Root);
            var last = Element("rk-c-last rk-last-cell", row.Root);
            row.Last = Text("", "rk-last", last);
            return row;
        }

        void OnRankingsChanged()
        {
            if (Tab == LobbyTab.Rank) RefreshRanking();
        }

        // The tab opens: every board is asked for (the left shows this player's rank on each), the one shown first.
        void OpenRanking()
        {
            rankPageAt = 0;
            Rankings.Request(rankBoard);
            foreach (var board in RankRules.Boards) if (board != rankBoard) Rankings.Request(board);
            RefreshRanking();
        }

        // The board and the view, as the tab's buttons choose them.
        public void SelectRanking(RankBoard board, RankScope scope)
        {
            rankBoard = board;
            rankScope = scope;
            rankPageAt = 0;
            Rankings.Request(board);
            RefreshRanking();
        }

        // ‹ ›: the next or the last page of the list shown. A page of the global list past the first is fetched as it is
        // turned to (RefreshRanking asks for one that is not there yet).
        public void TurnRanking(int by)
        {
            rankPageAt = Mathf.Max(0, rankPageAt + by);
            if (rankScope == RankScope.Global) Rankings.RequestPage(rankBoard, rankPageAt);
            RefreshRanking();
        }

        // This account's Steam ID (the record book's owner), 0 without Steam.
        static ulong RankSelfId => ulong.TryParse(MatchRecords.Owner, NumberStyles.None, CultureInfo.InvariantCulture, out ulong id) ? id : 0;

        static string Tally(RankCount c) => c.Draws > 0 ? Loc.F("{0}승 {1}패 {2}무", c.Wins, c.Losses, c.Draws) : Loc.F("{0}승 {1}패", c.Wins, c.Losses);

        // This player from their own book, for a view their line is not in: no rank, and the ranked matches so far.
        static RankEntry SelfLine(RecordBook book, RankBoard board, RankCount own) => new RankEntry
        {
            Id = RankSelfId, Self = true, Games = own.Games, Wins = own.Wins, LastDay = RankRules.Day(own.Last),
            Piece = RankUpload.Favourite(book, RankRules.ModeKey(board)), Have = own.Have, Need = own.Need,
        };

        // The 친구 view: the friends on the board by rank (their place among them), then the ones who are not on it yet
        // (the progress board's lines, and this player from their own book) by how far along they are.
        void FriendLines(Rankings.Board shown, RecordBook book, RankCount own)
        {
            rkFriendLines.Clear();
            for (int i = 0; i < shown.Friends.Count; i++) rkFriendLines.Add((shown.Friends[i], i + 1));
            var waiting = new List<RankEntry>();
            foreach (var p in Rankings.Progress)
                if (!p.Self && p.Games > 0 && !shown.Friends.Exists(e => e.Id == p.Id)) waiting.Add(p.Waiting(rankBoard));
            if (!shown.Friends.Exists(e => e.Self)) waiting.Add(SelfLine(book, rankBoard, own));
            waiting.Sort((a, b) =>
            {
                if (a.Have != b.Have) return b.Have.CompareTo(a.Have);
                int pa = RankRules.Permille(a.Wins, a.Games), pb = RankRules.Permille(b.Wins, b.Games);
                if (pa != pb) return pb.CompareTo(pa);
                return a.Games != b.Games ? b.Games.CompareTo(a.Games) : string.CompareOrdinal(a.Name, b.Name);
            });
            foreach (var e in waiting) rkFriendLines.Add((e, 0));
        }

        void RefreshRanking()
        {
            if (rankPage == null) return;
            var book = MatchRecords.Book;
            int today = Rankings.Today;
            var shown = Rankings.Get(rankBoard);
            var own = RankCount.Of(book, rankBoard);
            bool under = !own.Ranked;
            bool ready = shown.Status == Rankings.Status.Ready;

            // ---- left: this player ----
            rkMineKicker.text = "MY RANK · " + Loc.T(RankRules.Title(rankBoard));
            Show(rkMineOf, under);
            Show(rkMineBar, under);
            Show(rkMineGames, !under);
            if (under)
            {
                rkMineRank.text = own.Have.ToString();
                rkMineOf.text = " / " + Loc.F("{0}판", own.Need);
                // Beside the big number the label is narrow: broken where it reads well, not in the middle of a word
                // (RankText.Need's sentence with a line break, a pattern of its own so each language breaks it itself).
                rkMineRate.text = Loc.F("순위 판 {0}판을 더 하면\n순위에 오릅니다", Mathf.Max(1, own.ToGo));
                rkMineFill.style.width = Length.Percent(100f * Mathf.Clamp01((float)own.Have / Mathf.Max(1, own.Need)));
                rkMineNote.text = own.Games > 0
                    ? Loc.F("현재 승률 {0}% · {1}\n{2}판을 채우면 그때 승률로 순위에 오릅니다", RankText.Rate(own.Wins, own.Games), Tally(own), RankRules.MinGames)
                    : Loc.F("순위 판: 랜덤 매칭 · 봇 없음 · {0} 대 {0} 이상", RankRules.MinSide);
            }
            else
            {
                var mine = shown.Mine;
                rkMineRank.text = mine != null ? "#" + mine.Rank : "#-";
                rkMineRate.text = Loc.F("승률 <color=#F3EBDD>{0}%</color>", RankText.Rate(own.Wins, own.Games));
                rkMineGames.text = Loc.F("{0}판 · {1}", own.Games, Tally(own));
                if (mine != null)
                {
                    string top = RankText.Top(mine.Rank, shown.Total), friends = FriendPlace(shown);
                    rkMineNote.text = top != "" && friends != "" ? top + " · " + friends : top + friends;
                }
                else rkMineNote.text = Waiting(shown.Status);
            }

            for (int i = 0; i < RankRules.Boards.Length; i++)
            {
                var board = RankRules.Boards[i];
                var c = RankCount.Of(book, board);
                var m = Rankings.Get(board).Mine;
                rkModeLine[i].text = !c.Ranked ? RankText.Progress(c.Have, c.Need)
                    : Loc.F("{0}판 {1}% · {2}", c.Games, RankText.Rate(c.Wins, c.Games), m != null ? "<color=#FABE46>#" + m.Rank + "</color>" : "—");
                rkModeRows[i].EnableInClassList("rk-mode-on", board == rankBoard);
                rkTabs[i].EnableInClassList("rk-tab-on", board == rankBoard);
            }
            for (int i = 0; i < 3; i++) rkScopes[i].EnableInClassList("rk-scope-on", (RankScope)i == rankScope);

            // ---- right: the lines of the view, a page of them ----
            rkLines.Clear();
            int pages = 1, friendsOn = 0, friendsShort = 0;
            bool pageMissing = false;
            if (ready)
            {
                switch (rankScope)
                {
                    case RankScope.Around:
                        foreach (var e in shown.Around) rkLines.Add((e, e.Rank));
                        break;
                    case RankScope.Friends:
                        FriendLines(shown, book, own);
                        friendsOn = shown.Friends.Count;
                        friendsShort = rkFriendLines.Count - friendsOn;
                        pages = Mathf.Max(1, (rkFriendLines.Count + Rankings.PageSize - 1) / Rankings.PageSize);
                        rankPageAt = Mathf.Clamp(rankPageAt, 0, pages - 1);
                        for (int i = rankPageAt * Rankings.PageSize; i < rkFriendLines.Count && i < (rankPageAt + 1) * Rankings.PageSize; i++)
                            rkLines.Add(rkFriendLines[i]);
                        break;
                    default:
                        pages = shown.PageCount;
                        rankPageAt = Mathf.Clamp(rankPageAt, 0, pages - 1);
                        var page = shown.Page(rankPageAt);
                        if (page != null) foreach (var e in page) rkLines.Add((e, e.Rank));
                        else
                        {
                            pageMissing = true;
                            Rankings.RequestPage(rankBoard, rankPageAt);
                        }
                        break;
                }
            }
            if (rankScope == RankScope.Around) rankPageAt = 0;

            bool meShown = false;
            for (int i = 0; i < rkRows.Count; i++)
            {
                bool has = i < rkLines.Count;
                Show(rkRows[i].Root, has);
                if (!has) continue;
                meShown |= rkLines[i].entry.Self;
                FillRank(rkRows[i], rkLines[i].place, rkLines[i].entry, today);
            }

            // This player's line under the list when it is not among the lines shown: their line on the board, or -
            // while they are not on it - their own numbers with "6/20판" in place of the matches.
            RankEntry pin = null;
            int pinPlace = 0;
            if (ready && !meShown)
            {
                if (rankScope == RankScope.Friends)
                {
                    int at = rkFriendLines.FindIndex(l => l.entry.Self);
                    if (at >= 0) { pin = rkFriendLines[at].entry; pinPlace = rkFriendLines[at].place; }
                }
                else if (shown.Mine != null) { pin = shown.Mine; pinPlace = shown.Mine.Rank; }
                else pin = SelfLine(book, rankBoard, own);
            }
            Show(rkPinBox, pin != null);
            if (pin != null) FillRank(rkPin, pinPlace, pin, today);

            Show(rkPager, ready && pages > 1 && rankScope != RankScope.Around);
            rkPageText.text = (rankPageAt + 1) + " / " + pages;
            rkPrev.SetEnabled(rankPageAt > 0);
            rkNext.SetEnabled(rankPageAt < pages - 1);

            string status = "", sub = "";
            switch (shown.Status)
            {
                case Rankings.Status.Offline: status = Loc.T("Steam에 연결하면 순위를 볼 수 있습니다"); break;
                case Rankings.Status.Loading: status = Loc.T("순위를 불러오는 중입니다…"); break;
                // The Steam side's reason (SteamLeaderboards' Korean), translated here where it is shown.
                case Rankings.Status.Failed: status = shown.Error != "" ? Loc.T(shown.Error) : Loc.T("Steam 순위표를 불러오지 못했습니다"); break;
                default:
                    if (rkLines.Count > 0) break;
                    if (rankScope == RankScope.Around)
                    {
                        status = under ? Loc.F("순위에 오르면 내 위아래 {0}명이 표시됩니다", Rankings.AroundSpan) : Loc.T("곧 순위에 오릅니다");
                        sub = under ? RankText.Need(own) : "";
                    }
                    else if (pageMissing) status = Loc.T("순위를 불러오는 중입니다…");
                    else if (rankPageAt > 0) status = Loc.T("이 페이지에는 순위가 없습니다");
                    else
                    {
                        status = Loc.T("아직 순위에 오른 플레이어가 없습니다");
                        sub = Loc.F("순위 판을 {0}판 이상 하면 순위에 오릅니다", RankRules.MinGames);
                    }
                    break;
            }
            rkStatus.text = status;
            rkStatusSub.text = sub;
            Show(rkStatus, status != "");
            Show(rkStatusSub, sub != "");

            string title = Loc.T(RankRules.Title(rankBoard));
            rkFootLeft.text = !ready ? title
                : rankScope == RankScope.Friends
                    ? Loc.F("{0} · 순위에 오른 친구 {1}명", title, friendsOn) + (shown.Friends.Exists(e => e.Self) ? " (" + Loc.T("나 포함") + ")" : "")
                      + (friendsShort > 0 ? " · " + Loc.F("아직 {0}명", friendsShort) : "")
                : rankScope == RankScope.Around ? Loc.F("{0} · 내 위아래 {1}명씩", title, Rankings.AroundSpan)
                : Loc.F("{0} · 순위에 오른 {1}명", title, shown.Total);
        }

        void FillRank(RankRow row, int place, RankEntry e, int today)
        {
            row.Rank.text = place > 0 ? place.ToString() : "-";
            for (int m = 1; m <= 3; m++) row.Rank.EnableInClassList("rk-medal-" + m, place == m);
            SetIcon(row.Icon, e.Piece, 0);
            string name = e.Self && PlayerProfile.Name != "" ? PlayerProfile.Name : e.Name != "" ? e.Name : Loc.T("나");
            // R792: another player as "플레이어 N", and no Steam picture, while streamer mode hides names.
            bool hidden = !e.Self && StreamerMode.HideNames;
            if (hidden) name = StreamerMode.Name(e.Id, name, false);
            row.Name.text = name;
            SetFace(row, e.Self && e.Id == 0 ? RankSelfId : e.Id, name, hidden);
            string tag = e.Self ? Loc.T("나") : e.Friend && rankScope != RankScope.Friends ? Loc.T("친구") : "";
            row.Tag.text = tag;
            row.Tag.EnableInClassList("rk-tag-me", e.Self);
            Show(row.Tag, tag != "");
            row.Sub.text = rankScope == RankScope.Friends && e.Rank > 0 ? Loc.F("전체 {0}위", e.Rank) : "";
            Show(row.Sub, row.Sub.text != "");
            row.Fill.style.width = Length.Percent(RankRules.Permille(e.Wins, e.Games) / 10f);
            row.Rate.text = e.Games > 0 ? RankText.Rate(e.Wins, e.Games) + "%" : "—";
            row.Rate.EnableInClassList("rk-rate-gold", e.Self || (place >= 1 && place <= 3));
            // A player still short of the board: "6/20판" in place of the matches and wins.
            row.Games.text = e.Waiting && e.Have < e.Need ? RankText.Progress(e.Have, e.Need) : RankText.Games(e.Games, e.Wins);
            bool resting = RankRules.Resting(e.LastDay, today);
            row.Last.text = RankText.Last(e.LastDay, today);
            row.Last.EnableInClassList("rk-rest", resting);
            row.Root.EnableInClassList("rk-row-rest", resting);
            row.Root.EnableInClassList("rk-row-me", e.Self);
        }

        // R745: the Steam profile picture (Rankings.Face, from the Steam side); until it is there - or with no Steam -
        // a soft colour of the player's own and the name's first letter.
        static void SetFace(RankRow row, ulong id, string name, bool hidden = false)
        {
            var picture = hidden ? null : Rankings.Face(id);
            if (picture != null)
            {
                row.Face.style.backgroundImage = new StyleBackground(picture);
                row.Face.style.backgroundColor = Color.clear;
                Show(row.Letter, false);
                return;
            }
            ulong seed = id != 0 ? id : (ulong)(uint)(name ?? "").GetHashCode();
            row.Face.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            row.Face.style.backgroundColor = Color.HSVToRGB((seed * 2654435761UL % 360UL) / 360f, .35f, .95f);
            row.Letter.text = string.IsNullOrEmpty(name) ? "?" : char.IsSurrogate(name[0]) && name.Length > 1 ? name.Substring(0, 2) : name.Substring(0, 1);
            Show(row.Letter, true);
        }

        // "친구 중 4위", "" when this player is not on the friends' board.
        static string FriendPlace(Rankings.Board board)
        {
            int at = board.Friends.FindIndex(e => e.Self);
            return at >= 0 ? Loc.F("친구 중 {0}위", at + 1) : "";
        }

        static string Waiting(Rankings.Status status)
        {
            switch (status)
            {
                case Rankings.Status.Offline: return Loc.T("Steam에 연결하면 순위를 볼 수 있습니다");
                case Rankings.Status.Loading: return Loc.T("순위를 불러오는 중입니다…");
                case Rankings.Status.Failed: return Loc.T("Steam 순위표를 불러오지 못했습니다");
                default: return Loc.T("곧 순위에 오릅니다");
            }
        }
    }
}
