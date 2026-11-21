using System;
using System.Collections.Generic;
using System.Linq;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // The lobby's tabs at the top: 플레이 (the party, the modes, the start), 기물 도감, 전적 and (R743) 순위.
    public enum LobbyTab { Play, Codex, Record, Rank }

    // R736 (user 11-21: "도감 A + 전적 A", the mock-up https://claude.ai/artifact/37oLnyQwLavWDdPo5x2ScH): the top bar's
    // 기물 도감 and 전적 open pages over the lobby in place of the play HUD (Docs/Architecture/UI.md §3-1).
    //  - 기물 도감: the six pieces down the left, the chosen one big on the lobby's board (LobbyStage.Showcase; drag to
    //    turn it), and on the right its stats, its skills in both modes and this player's matches with it.
    //  - 전적: the overall rate and each mode's totals on the left, the recent matches on the right a page at a time
    //    (MatchRecords, Core RecordBook).
    // Built in code over NetworkHud.uxml's tabs (tab-play, tab-codex, tab-record); the play HUD (class play-only) is
    // hidden by visibility while another tab is up, so Render's own show / hide of it never fights this.
    public sealed partial class NetworkHudView
    {
        public event Action<LobbyTab> TabChanged;
        // The piece and team the codex shows (0 white, 1 black).
        public event Action<PieceKind, int> CodexPiece;
        // Degrees to turn the codex piece by (the pointer dragged across the stage).
        public event Action<float> CodexSpin;

        public LobbyTab Tab { get; private set; } = LobbyTab.Play;
        public PieceKind CodexKind => CodexOrder[codexAt];
        public int CodexTeam => codexTeam;

        // The codex's list, in the chess value order.
        static readonly PieceKind[] CodexOrder = { PieceKind.Pawn, PieceKind.Rook, PieceKind.Knight, PieceKind.Bishop, PieceKind.Queen, PieceKind.King };
        const int RowsPerPage = 8;

        int codexAt, codexTeam;
        readonly Dictionary<LobbyTab, Button> tabButtons = new Dictionary<LobbyTab, Button>();
        VisualElement lobbyRoot, pageShade, codexPage, recordPage;

        // ---------- codex widgets ----------
        readonly List<(Button button, VisualElement icon, Label sub)> codexItems = new List<(Button, VisualElement, Label)>();
        Button codexWhite, codexBlack;
        Label cxEn, cxName, cxWhere, cxPrName, cxPrLine, cxPrNone, cxSfF, cxSfE, cxSfNone, cxRecord;
        VisualElement cxPrSkill, cxSfSkillF, cxSfSkillE, cxImmune;
        readonly List<(VisualElement fill, Label value)> cxBars = new List<(VisualElement, Label)>();
        bool spinning;
        float spinX;

        // ---------- record widgets ----------
        Label rcRate, rcTotals, rcEmpty, rcEmptySub, rcPage;
        readonly Label[] rcModeSum = new Label[2];
        readonly VisualElement[] rcModeFill = new VisualElement[2];
        readonly Label[,] rcStat = new Label[2, 3];
        readonly Button[] rcFilters = new Button[3];
        Button rcPrev, rcNext;
        int recordFilter, recordPageAt;   // filter: 0 every mode, 1 Pawn Rush, 2 Sword Fight
        sealed class RecordRow
        {
            public VisualElement Root, Dot, Icon;
            public Label Badge, Mode, Piece, Main, Sub, Size, When, Ranked;
        }
        readonly List<RecordRow> rcRows = new List<RecordRow>();
        // R742: the record on each row of the page shown (for the double click).
        readonly List<MatchRecord> rcShown = new List<MatchRecord>();

        static readonly string[] FilterModes = { null, GameModes.KingRush.Key, GameModes.SwordFight.Key };

        void BuildTabs()
        {
            lobbyRoot = root.Q(className: "lobby") ?? root;
            Tabbed(LobbyTab.Play, "tab-play");
            Tabbed(LobbyTab.Codex, "tab-codex");
            Tabbed(LobbyTab.Record, "tab-record");
            Tabbed(LobbyTab.Rank, "tab-rank");
            // What only the play tab shows.
            foreach (var name in new[] { "lineup", "mode-rail" }) root.Q(name)?.AddToClassList("play-only");
            root.Q(className: "bottom-right")?.AddToClassList("play-only");

            // The pages go under the top bar and every window (they come after in the tree), over the lineup's layer.
            var lineup = root.Q("lineup");
            int at = lineup != null && lineup.parent == lobbyRoot ? lobbyRoot.IndexOf(lineup) : 0;
            pageShade = Element("lt-shade", null);
            codexPage = Element("lt-page", null);
            recordPage = Element("lt-page", null);
            rankPage = Element("lt-page", null);
            lobbyRoot.Insert(at, rankPage);
            lobbyRoot.Insert(at, recordPage);
            lobbyRoot.Insert(at, codexPage);
            lobbyRoot.Insert(at, pageShade);
            BuildCodex();
            BuildRecord();
            BuildRanking();
            foreach (var page in new[] { pageShade, codexPage, recordPage, rankPage }) page.style.display = DisplayStyle.None;
            MatchRecords.Changed += OnRecordsChanged;
            Rankings.Changed += OnRankingsChanged;
        }

        void Tabbed(LobbyTab tab, string name)
        {
            var button = Bind(root, name, () => SetTab(tab));
            if (button != null) tabButtons[tab] = button;
        }

        void OnRecordsChanged()
        {
            if (Tab == LobbyTab.Record) RefreshRecord();
            if (Tab == LobbyTab.Codex) RefreshCodex();
            if (Tab == LobbyTab.Rank) RefreshRanking();
        }

        // Shows a tab: its page, its rule under the name, the 3D stage's showcase (through TabChanged).
        public void SetTab(LobbyTab tab, bool force = false)
        {
            if (Tab == tab && !force) return;
            Tab = tab;
            bool away = tab != LobbyTab.Play;
            lobbyRoot?.EnableInClassList("tab-away", away);
            Show(pageShade, tab == LobbyTab.Record || tab == LobbyTab.Rank);
            Show(codexPage, tab == LobbyTab.Codex);
            Show(recordPage, tab == LobbyTab.Record);
            Show(rankPage, tab == LobbyTab.Rank);
            foreach (var pair in tabButtons)
            {
                bool on = pair.Key == tab;
                pair.Value.EnableInClassList("a-tab-on", on);
                var rule = pair.Value.parent?.Q(className: "a-tab-rule");
                if (rule != null) rule.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (away)
            {
                SetRail(false);
                CloseVoiceMenu();
                Close(partyPanel);
                Close(friendsPanel);
                Close(detailsPanel);
            }
            if (tab == LobbyTab.Codex) RefreshCodex();
            CloseRecordDetail();   // R742: a tab always opens on its own first view
            if (tab == LobbyTab.Record) { recordPageAt = 0; RefreshRecord(); }
            if (tab == LobbyTab.Rank) OpenRanking();
            if (!force) TabChanged?.Invoke(tab);
        }

        // ---------- building helpers ----------

        static VisualElement Element(string classes, VisualElement parent, PickingMode picking = PickingMode.Ignore)
        {
            var e = new VisualElement { pickingMode = picking };
            foreach (var c in classes.Split(' ')) if (c.Length > 0) e.AddToClassList(c);
            parent?.Add(e);
            return e;
        }

        static Label Text(string text, string classes, VisualElement parent, bool display = false)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (var c in classes.Split(' ')) if (c.Length > 0) label.AddToClassList(c);
            if (display) RuntimePanels.Display(label);
            parent?.Add(label);
            return label;
        }

        // A button the fallback click path knows too.
        Button Pressable(string text, string classes, VisualElement parent, Action action)
        {
            var button = new Button { text = text, focusable = false };
            foreach (var c in classes.Split(' ')) if (c.Length > 0) button.AddToClassList(c);
            button.clicked += action;
            actions[button] = action;
            parent?.Add(button);
            return button;
        }

        static readonly Dictionary<string, Texture2D> pieceIcons = new Dictionary<string, Texture2D>();

        // The chess icon (Resources/Loading/ChessIcons, the entrance screen's) of a piece and team.
        static void SetIcon(VisualElement element, PieceKind piece, int team)
        {
            string key = piece + (team == 1 ? "_Black" : "_White");
            if (!pieceIcons.TryGetValue(key, out var texture))
                pieceIcons[key] = texture = Resources.Load<Texture2D>("Loading/ChessIcons/" + key);
            element.style.backgroundImage = texture != null ? new StyleBackground(texture) : new StyleBackground(StyleKeyword.None);
        }

        // ---------- 기물 도감 ----------

        void BuildCodex()
        {
            var list = Element("cx-list", codexPage);
            Text("PIECES · " + CodexOrder.Length, "a-kicker cx-kicker", list);
            for (int i = 0; i < CodexOrder.Length; i++)
            {
                int index = i;
                var button = Pressable("", "cx-item", list, () => PickCodex(index));
                var icon = Element("cx-icon", button);
                var texts = Element("cx-texts", button);
                Text(Loc.T(ChessPieces.Name(CodexOrder[i])), "cx-item-name", texts);
                var sub = Text(Availability(CodexOrder[i]).short_, "cx-item-sub", texts);
                codexItems.Add((button, icon, sub));
            }
            Text(Loc.T("팀 색으로 보기"), "a-label cx-team-label", list);
            var seg = Element("row", list);
            codexWhite = Pressable(Loc.T("백"), "cx-seg", seg, () => SetCodexTeam(0));
            codexBlack = Pressable(Loc.T("흑"), "cx-seg", seg, () => SetCodexTeam(1));

            // The stage: the board shows through; a drag across it turns the piece.
            var stage = Element("cx-stage", codexPage, PickingMode.Position);
            Text(Loc.T("끌어서 돌려 보기"), "cx-hint", stage);
            stage.RegisterCallback<PointerDownEvent>(e =>
            {
                spinning = true;
                spinX = e.position.x;
                stage.CapturePointer(e.pointerId);
            });
            stage.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!spinning) return;
                float dx = e.position.x - spinX;
                spinX = e.position.x;
                // R738 (user 11-21: "왼쪽으로 마우스로 돌리면 화면에서 왼쪽으로 돌아야 하는데 반대가 됐어"): the piece's front follows
                // the pointer - a drag to the left turns it to the left on the screen.
                if (Mathf.Abs(dx) > 0f) CodexSpin?.Invoke(-dx * .6f);
            });
            stage.RegisterCallback<PointerUpEvent>(e =>
            {
                spinning = false;
                if (stage.HasPointerCapture(e.pointerId)) stage.ReleasePointer(e.pointerId);
            });
            stage.RegisterCallback<PointerCaptureOutEvent>(_ => spinning = false);

            // R740 (user 11-21: "기물 도감도 해", after the record's R739): a solid card, not the see-through glass.
            var panel = Element("lt-card cx-panel", codexPage, PickingMode.Position);
            cxEn = Text("", "a-kicker", panel);
            cxName = Text("", "cx-name", panel, true);
            cxWhere = Text("", "cx-where", panel);

            var stats = Element("cx-section", panel);
            Text(Loc.T("능력치"), "a-label", stats);
            foreach (var label in new[] { Loc.T("무게"), Loc.T("이동"), Loc.T("오르기") })
            {
                var row = Element("cx-bar-row", stats);
                Text(label, "cx-bar-label", row);
                var track = Element("cx-bar-track", row);
                var fill = Element("cx-bar-fill", track);
                var value = Text("", "cx-bar-value", row);
                cxBars.Add((fill, value));
            }
            var tags = Element("cx-tags", stats);
            Text(Loc.T("질주 가능"), "cx-tag", tags);
            cxImmune = Text(Loc.T("눌림 · 비틀거림 면역"), "cx-tag cx-tag-gold", tags);

            Element("cx-rule", panel);

            var pr = Element("cx-section", panel);
            var prHead = Element("cx-mode-head", pr);
            Element("cx-dot cx-dot-pr", prHead);
            Text(Loc.T("폰 러쉬"), "a-label cx-mode-label", prHead);
            cxPrSkill = Element("cx-skill", pr);
            Text("F", "cx-key", cxPrSkill);
            cxPrName = Text("", "cx-skill-name", cxPrSkill);
            cxPrLine = Text("", "cx-skill-line", pr);
            cxPrNone = Text(Loc.T("이 모드에는 나오지 않아요"), "cx-none", pr);

            var sf = Element("cx-section", panel);
            var sfHead = Element("cx-mode-head", sf);
            Element("cx-dot cx-dot-sf", sfHead);
            Text(Loc.T("소드 파이트"), "a-label cx-mode-label", sfHead);
            cxSfSkillF = Element("cx-skill", sf);
            Text("F", "cx-key", cxSfSkillF);
            cxSfF = Text("", "cx-skill-name", cxSfSkillF);
            cxSfSkillE = Element("cx-skill", sf);
            Text("E", "cx-key", cxSfSkillE);
            cxSfE = Text("", "cx-skill-name", cxSfSkillE);
            cxSfNone = Text(Loc.T("고를 수 없는 기물이에요"), "cx-none", sf);

            Element("cx-grow", panel);
            var record = Pressable("", "cx-record", panel, () => SetTab(LobbyTab.Record));
            cxRecord = Text("", "cx-record-text", record);
            Text(Loc.T("전적 보기 ›"), "cx-record-go", record);
        }

        // Where a piece comes from in each mode: the list's short line and the panel's longer one.
        static (string short_, string long_) Availability(PieceKind kind)
        {
            bool rush = kind == PieceKind.Pawn || PawnRushPromotion.Pieces.Contains(kind);
            bool sword = SwordMatchRules.Pickable.Contains(kind);
            if (kind == PieceKind.Pawn) return (Loc.T("폰 러쉬 시작 기물"), Loc.T("폰 러쉬에서 모두가 폰으로 출발 · 소드 파이트에서는 못 고름"));
            if (rush && sword) return (Loc.T("두 모드"), Loc.T("폰 러쉬 승격 발판 · 소드 파이트 고르기"));
            if (sword) return (Loc.T("소드 파이트 전용"), Loc.T("소드 파이트 고르기 · 폰 러쉬에는 나오지 않음"));
            return (Loc.T("폰 러쉬 전용"), Loc.T("폰 러쉬 승격 발판 · 소드 파이트에서는 못 고름"));
        }

        // The codex's piece and team, as a click on the list and the 백 / 흑 buttons would choose them.
        public void SelectCodex(PieceKind kind, int team)
        {
            codexTeam = team == 1 ? 1 : 0;
            PickCodex(Array.IndexOf(CodexOrder, kind));
        }

        // The record list's filter (0 every mode, 1 Pawn Rush, 2 Sword Fight), as its buttons choose it.
        public void SelectRecordFilter(int filter)
        {
            recordFilter = Mathf.Clamp(filter, 0, 2);
            recordPageAt = 0;
            CloseRecordDetail();
            RefreshRecord();
        }

        void PickCodex(int index)
        {
            if (index < 0 || index >= CodexOrder.Length) return;
            codexAt = index;
            RefreshCodex();
            CodexPiece?.Invoke(CodexKind, codexTeam);
        }

        void SetCodexTeam(int team)
        {
            codexTeam = team == 1 ? 1 : 0;
            RefreshCodex();
            CodexPiece?.Invoke(CodexKind, codexTeam);
        }

        void RefreshCodex()
        {
            if (codexPage == null) return;
            for (int i = 0; i < codexItems.Count; i++)
            {
                codexItems[i].button.EnableInClassList("cx-item-on", i == codexAt);
                SetIcon(codexItems[i].icon, CodexOrder[i], codexTeam);
            }
            codexWhite?.EnableInClassList("cx-seg-on", codexTeam == 0);
            codexBlack?.EnableInClassList("cx-seg-on", codexTeam == 1);

            var kind = CodexKind;
            var stats = ChessPieces.Stats(kind);
            cxEn.text = kind.ToString().ToUpperInvariant();
            cxName.text = Loc.T(ChessPieces.Name(kind));
            cxWhere.text = Availability(kind).long_;
            Bar(0, stats.Weight, 3f);
            Bar(1, stats.Move, 1f);
            Bar(2, stats.Climb, 1f);
            Show(cxImmune, stats.StatusImmune);

            bool rush = kind == PieceKind.Pawn || PawnRushPromotion.Pieces.Contains(kind);
            Show(cxPrSkill, rush);
            Show(cxPrLine, rush);
            Show(cxPrNone, !rush);
            cxPrName.text = rush ? Loc.T(PawnRushPromotion.SkillName(kind)) : "";
            cxPrLine.text = rush ? Loc.T(PawnRushPromotion.SkillLine(kind)) : "";

            bool sword = SwordMatchRules.Pickable.Contains(kind);
            Show(cxSfSkillF, sword);
            Show(cxSfSkillE, sword);
            Show(cxSfNone, !sword);
            cxSfF.text = sword ? Loc.T(SwordSkillNames.Skill(kind)) : "";
            cxSfE.text = sword ? Loc.T(SwordSkillNames.Edge(kind)) : "";

            var book = MatchRecords.Book;
            int games = book.PieceGames[(int)kind], wins = book.PieceWins[(int)kind];
            cxRecord.text = games > 0 ? Loc.F("내 기록 · 이 기물로 <color=#E2B866>{0}판 {1}승</color>", games, wins) : Loc.T("내 기록 · 아직 이 기물로 한 판도 안 했어요");
        }

        void Bar(int i, float value, float max)
        {
            if (i >= cxBars.Count) return;
            cxBars[i].fill.style.width = Length.Percent(Mathf.Clamp01(value / max) * 100f);
            cxBars[i].value.text = "×" + value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        // ---------- 전적 ----------

        void BuildRecord()
        {
            // R739 (user 11-21: the lobby's hall showed through and the record was hard to read): solid cards (lt-card),
            // not the lobby's see-through glass (a-panel), the overall rate on one too.
            var left = Element("rc-left", recordPage);
            var summary = Element("lt-card rc-summary", left);
            Text("MATCH RECORD", "a-kicker", summary);
            var rateRow = Element("rc-rate-row", summary);
            rcRate = Text("—", "rc-rate", rateRow, true);
            var rateTexts = Element("rc-rate-texts", rateRow);
            Text(Loc.T("전체 승률"), "rc-rate-cap", rateTexts);
            rcTotals = Text("", "rc-rate-sum", rateTexts);

            string[,] captions = { { Loc.T("가장 빠른 도착"), Loc.T("1위 도착"), Loc.T("도착 못 함") }, { Loc.T("내가 떨어뜨림"), Loc.T("떨어짐"), Loc.T("한 판 최다 떨어뜨림") } };
            for (int m = 0; m < 2; m++)
            {
                var card = Element("lt-card rc-mode", left);
                var head = Element("rc-mode-head", card);
                var name = Element("row", head);
                Element(m == 0 ? "cx-dot cx-dot-pr" : "cx-dot cx-dot-sf", name);
                Text(Loc.T(m == 0 ? GameModes.KingRush.Name : GameModes.SwordFight.Name), "rc-mode-name", name);
                rcModeSum[m] = Text("", "rc-mode-sum", head);
                var bar = Element("rc-bar", card);
                rcModeFill[m] = Element("rc-bar-fill", bar);
                var statsRow = Element("rc-stats", card);
                for (int s = 0; s < 3; s++)
                {
                    var stat = Element("rc-stat", statsRow);
                    rcStat[m, s] = Text("—", "rc-stat-value", stat, true);
                    Text(captions[m, s], "rc-stat-cap", stat);
                }
            }
            // R745: the totals are backed up on Steam (the progress board); the list of recent matches is this PC's own.
            Text(Loc.T("온라인 경기만 셈 · 판수는 Steam에 보관, 최근 경기 목록은 이 PC에만"), "rc-note", left);

            var list = Element("lt-card rc-list", recordPage, PickingMode.Position);
            var head2 = Element("rc-list-head", list);
            var filters = Element("row", head2);
            for (int f = 0; f < 3; f++)
            {
                int index = f;
                rcFilters[f] = Pressable("", "rc-filter", filters, () => { recordFilter = index; recordPageAt = 0; RefreshRecord(); });
            }
            var pager = Element("rc-pager", head2);
            rcPrev = Pressable("‹", "rc-page-btn", pager, () => { recordPageAt--; RefreshRecord(); });
            rcPage = Text("1 / 1", "rc-page", pager);
            rcNext = Pressable("›", "rc-page-btn", pager, () => { recordPageAt++; RefreshRecord(); });

            var rows = Element("rc-rows", list);
            for (int i = 0; i < RowsPerPage; i++)
            {
                var row = new RecordRow { Root = Element("rc-row", rows) };
                row.Badge = Text("", "rc-badge", row.Root);
                var mode = Element("rc-mode-cell", row.Root);
                row.Dot = Element("cx-dot", mode);
                row.Mode = Text("", "rc-mode-text", mode);
                var piece = Element("rc-piece-cell", row.Root);
                row.Icon = Element("rc-piece-icon", piece);
                row.Piece = Text("", "rc-piece-name", piece);
                var main = Element("rc-main-cell", row.Root);
                row.Main = Text("", "rc-main", main);
                row.Sub = Text("", "rc-sub", main);
                // R745: the sides, and under them a small mark on a match that counted for the 순위 boards.
                var size = Element("rc-size-cell", row.Root);
                row.Size = Text("", "rc-size", size);
                row.Ranked = Text(Loc.T("순위"), "rc-ranked", size);
                row.When = Text("", "rc-when", row.Root);
                // R742: a double click opens the match's detail in the list's place (NetworkHudView.RecordDetail.cs).
                Text(Loc.T("더블 클릭 · 자세히"), "rc-hint", row.Root);
                row.Root.pickingMode = PickingMode.Position;
                int slot = i;
                row.Root.RegisterCallback<ClickEvent>(e =>
                {
                    if (e.clickCount < 2 || slot >= rcShown.Count) return;
                    MenuSound.Play(MenuSound.Kind.Open);
                    OpenRecordDetail(rcShown[slot]);
                });
                rcRows.Add(row);
            }
            rcEmpty = Text(Loc.T("아직 기록이 없어요"), "rc-empty", rows);
            rcEmptySub = Text(Loc.T("온라인 경기를 끝까지 하면 여기에 쌓여요"), "rc-empty-sub", rows);
            Text(Loc.F("최근 {0}판까지 보관", RecordBook.Kept), "rc-foot", list);
            BuildRecordDetail(list);
        }

        void RefreshRecord()
        {
            if (recordPage == null) return;
            var book = MatchRecords.Book;
            var all = book.All;
            rcRate.text = RecordText.Rate(all.Wins, all.Games);
            rcTotals.text = Loc.F("{0}판 · {1}승 {2}패", all.Games, all.Wins, all.AllLosses) + (all.Draws > 0 ? " " + Loc.F("{0}무", all.Draws) : "");

            for (int m = 0; m < 2; m++)
            {
                var t = book.Mode(m == 0 ? GameModes.KingRush.Key : GameModes.SwordFight.Key);
                rcModeSum[m].text = Loc.F("{0}판 · {1}승 {2}패", t.Games, t.Wins, t.AllLosses) + $" · <color=#E2B866>{RecordText.Rate(t.Wins, t.Games)}</color>";
                rcModeFill[m].style.width = Length.Percent(t.Games > 0 ? 100f * t.Wins / t.Games : 0f);
                if (m == 0)
                {
                    rcStat[0, 0].text = t.BestFinish >= 0 ? MatchResult.Clock(t.BestFinish) : "—";
                    rcStat[0, 1].text = Loc.F("{0}번", t.Firsts);
                    rcStat[0, 2].text = Loc.F("{0}번", t.NotFinished);
                }
                else
                {
                    rcStat[1, 0].text = t.Knockouts.ToString();
                    rcStat[1, 1].text = t.Falls.ToString();
                    rcStat[1, 2].text = t.BestKnockouts.ToString();
                }
            }

            string[] names = { Loc.T("전체"), Loc.T(GameModes.KingRush.Name), Loc.T(GameModes.SwordFight.Name) };
            for (int f = 0; f < 3; f++)
            {
                int games = f == 0 ? all.Games : book.Mode(FilterModes[f]).Games;
                rcFilters[f].text = names[f] + " " + games;
                rcFilters[f].EnableInClassList("rc-filter-on", f == recordFilter);
            }

            string mode = FilterModes[Mathf.Clamp(recordFilter, 0, 2)];
            var shown = mode == null ? book.Recent : book.Recent.Where(r => r.Mode == mode).ToList();
            int pages = Mathf.Max(1, (shown.Count + RowsPerPage - 1) / RowsPerPage);
            recordPageAt = Mathf.Clamp(recordPageAt, 0, pages - 1);
            rcPage.text = (recordPageAt + 1) + " / " + pages;
            rcPrev.SetEnabled(recordPageAt > 0);
            rcNext.SetEnabled(recordPageAt < pages - 1);

            var now = DateTime.Now;
            var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
            rcShown.Clear();
            for (int i = 0; i < rcRows.Count; i++)
            {
                int at = recordPageAt * RowsPerPage + i;
                var row = rcRows[i];
                bool has = at < shown.Count;
                Show(row.Root, has);
                if (!has) continue;
                var r = shown[at];
                rcShown.Add(r);
                row.Badge.text = Loc.T(RecordText.Badge(r.Result));
                foreach (var c in new[] { "rc-badge-win", "rc-badge-loss", "rc-badge-left", "rc-badge-draw" }) row.Badge.RemoveFromClassList(c);
                row.Badge.AddToClassList(r.Result == RecordResult.Win ? "rc-badge-win" : r.Result == RecordResult.Loss ? "rc-badge-loss"
                                         : r.Result == RecordResult.Left ? "rc-badge-left" : "rc-badge-draw");
                bool sword = r.Mode == GameModes.SwordFight.Key;
                row.Dot.EnableInClassList("cx-dot-pr", !sword);
                row.Dot.EnableInClassList("cx-dot-sf", sword);
                row.Mode.text = Loc.T(RecordText.ModeName(r.Mode));
                SetIcon(row.Icon, r.Piece, 0);
                row.Piece.text = Loc.T(ChessPieces.Name(r.Piece));
                row.Main.text = RecordText.Headline(r);
                row.Sub.text = RecordText.Sub(r);
                row.Size.text = RecordText.Size(r);
                Show(row.Ranked, r.Ranked);
                row.When.text = RecordText.When(r.When, now, offset);
            }
            Show(rcEmpty, shown.Count == 0);
            Show(rcEmptySub, shown.Count == 0);
        }
    }
}
