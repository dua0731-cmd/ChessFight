using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // Everything the lobby HUD needs to draw one refresh. The view stays ignorant
    // of Steam so the layout can be edited and tested without a session.
    public struct HudModel
    {
        public bool Online, CanRetry;
        public string PlayerName, Status, Details, Version;
        public int FriendsOnline;

        // The mode card. Only an idle party leader may change it.
        public string ModeKey, ModeHint;
        public bool CanChangeMode;

        public int PartySize;
        public string PartyCode, Bots, BotsNote;
        public bool CanAddBot, CanRemoveBot, CanLeaveParty, CanCopyParty;

        // Idle: the start button. Busy: the matching card in its place.
        public bool Busy, CanPlay, CanCancel, ShowStart, CanStart;
        public string BusyTitle, BusySub;

        // The matchmaking panel at the top: our side on the left, 6 slots each.
        public bool ShowMatch, ShowRoomTools, CanFillRoom, CanClearRoomBots;
        public string MatchKicker, MatchTitle, MatchTimer, MatchNote, RoomCode;
        public int OurTeam, OursFilled, TheirsFilled;

        public bool CanInvite, CanJoinParty, CanJoinMatch, CanCreateTest;
        public bool ShowTestBots, CanAddAlly, CanRemoveAlly, CanAddEnemy, CanRemoveEnemy;
        public int AllyDummies, EnemyDummies;
        public string TestHint;
    }

    // Binds NetworkHud.uxml. The element names in the UXML and the Q<T>(name)
    // lookups here are a contract: renaming one without the other fails at run time.
    [DisallowMultipleComponent]
    public sealed class NetworkHudView : MonoBehaviour
    {
        public event Action Play, StartGame, Cancel, LeaveParty, CreateTest, CopyParty, CopyMatch;
        public event Action AddBot, RemoveBot, FillRoom, ClearRoomBots, RetrySteam;
        public event Action AddAllyDummy, RemoveAllyDummy, AddEnemyDummy, RemoveEnemyDummy;
        public event Action FriendsOpened, RefreshFriends, SteamOverlayInvite;
        // My name top right was pressed: change it (R85, NameChange).
        public event Action Rename;
        public event Action<ulong> JoinParty, JoinMatch, InviteFriend;
        public event Action<string> PickMode;

        const int FriendsPerPage = 7;
        const float ToastSeconds = 3.5f;

        PanelSettings ownedPanel;
        VisualElement root;
        // Every button and what it does, so the fallback router below can fire one
        // without going through the event system.
        readonly Dictionary<Button, Action> actions = new Dictionary<Button, Action>();
        bool pointerSeen, fallbackDead, diagnosed;
        float diagnoseAt, toastUntil, spin;
        // A left press the fallback router holds for a frame (FallbackClick).
        int pressedAt = -1;
        Vector2 pressedPoint;
        Func<bool> chatBlocker;

        Label status, details, profile, version, toast, offlineText;
        Button partyOpen, friendsOpen, profileOpen, retry, play, cancel, start, codeOpen, createTest, leaveParty, copyParty;
        Button botsLess, botsMore, fillRoom, clearRoomBots, modeOpen, joinParty, joinMatch;
        Label partyCount, partyCode, bots, botsNote, busyTitle, busySub;
        Label modeName, modeTagline, modeBadge, modeChange, modeHint;
        VisualElement offline, busy, spinner, modeTile, roomTools;
        VisualElement matchPanel, slotsOurs, slotsTheirs;
        Label matchKicker, matchTitle, matchTimer, matchNote, matchCount, roomCode;
        VisualElement matchLive;
        VisualElement testBots;
        Label allyDummies, enemyDummies, testHint, partyBotsTitle;
        Button allyLess, allyMore, enemyLess, enemyMore;
        readonly List<VisualElement> ourSlots = new List<VisualElement>(), theirSlots = new List<VisualElement>();
        TextField code;

        VisualElement friendsPanel, partyPanel, detailsPanel, codeModal, modeModal;
        VisualElement friendsList;
        Label friendsInfo, friendsPage;
        Button friendsOnline, friendsAll;
        readonly List<FriendInfo> friends = new List<FriendInfo>();
        readonly HashSet<ulong> partyIds = new HashSet<ulong>();
        readonly List<Button> friendButtons = new List<Button>();
        int friendPage;
        bool onlineOnly = true, canInvite;

        VisualElement modeList;
        readonly Dictionary<string, Button> modeButtons = new Dictionary<string, Button>();

        // The game-mode rail down the right, one card per mode.
        VisualElement railList, rail;
        Label playLabel;
        bool railOpen;

        // 10-17 (승규 님): the mode cards wait below the screen. The pointer resting on "모드 선택" brings them
        // up one after another from the bottom, and they stay while it moves onto them; when it leaves both
        // they sink back, top one first. While they are up the button reads "게임 시작" and a click starts
        // the game. (Enter does the same in two presses: the first brings them up.)
        int railHover;
        IVisualElementScheduledItem railClose;

        void PlayPressed()
        {
            if (!railOpen) { SetRail(true); return; }
            Play?.Invoke();
        }

        void HoverRail(VisualElement element)
        {
            if (element == null) return;
            element.RegisterCallback<PointerEnterEvent>(_ =>
            {
                railHover++;
                railClose?.Pause();
                if (Usable(play)) SetRail(true);
            });
            element.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                railHover = Mathf.Max(0, railHover - 1);
                // A moment's grace, so moving from the button up to the cards does not close them.
                railClose?.Pause();
                railClose = element.schedule.Execute(() => { if (railHover == 0) SetRail(false); }).StartingIn(250);
            });
        }

        const float RailStep = 82f;   // a card's height and the gap under it (NetworkHud.uss)
        IVisualElementScheduledItem railReset;

        // A rail card's slide (top) and fade, each with its own delay, length and easing.
        static void Card(VisualElement card, float topDelay, float topTime, float fadeTime, float fadeDelay, float unused,
                         float extraDelay, EasingMode topEase, EasingMode fadeEase, float top, float opacity)
        {
            card.style.transitionProperty = new List<StylePropertyName> { "translate", "top", "opacity" };
            card.style.transitionDuration = new List<TimeValue> { new TimeValue(.3f), new TimeValue(topTime), new TimeValue(fadeTime) };
            card.style.transitionDelay = new List<TimeValue> { new TimeValue(0f), new TimeValue(topDelay), new TimeValue(fadeDelay + extraDelay) };
            card.style.transitionTimingFunction = new List<EasingFunction>
                { new EasingFunction(EasingMode.EaseOutBack), new EasingFunction(topEase), new EasingFunction(fadeEase) };
            card.style.top = top;
            card.style.opacity = opacity;
        }

        void SetRail(bool open, bool force = false)
        {
            if (!force && railOpen == open && rail != null && rail.ClassListContains("rail-closed") == !open) return;
            railOpen = open;
            if (rail != null)
            {
                rail.EnableInClassList("rail-closed", !open);
                // Not disabled while closed (that switched the cards to their disabled look at once); the
                // hidden cards just stop taking the pointer.
                if (railList != null)
                {
                    int n = railList.childCount;
                    for (int i = 0; i < n; i++)
                    {
                        var card = railList[i];
                        card.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
                        if (force) { Card(card, 0f, 0f, 0f, .3f, .3f, 0f, EasingMode.Linear, EasingMode.Linear, 70f, 0f); continue; }
                        if (open)
                            // Up one after another from below, the bottom card first, with a little bounce.
                            Card(card, (n - 1 - i) * .06f, .34f, .22f, 0f, 0f, (n - 1 - i) * .06f, EasingMode.EaseOutBack, EasingMode.EaseOut, 0f, 1f);
                        else
                        {
                            // Down: the bottom card (Sword Fight) stays; the ones above slide softly down into it,
                            // the nearest first, and then they all fade out together.
                            float drop = (n - 1 - i) * RailStep;
                            Card(card, (n - 1 - i) == 0 ? 0f : (i == 0 ? .06f : 0f), .5f, .3f, .38f, .38f, 0f, EasingMode.EaseInOutSine, EasingMode.EaseIn, drop, 0f);
                        }
                    }
                    // Once it has faded, move the hidden cards back below (unseen), ready to rise again.
                    railReset?.Pause();
                    if (!open && !force)
                        railReset = railList.schedule.Execute(() =>
                        {
                            if (railOpen) return;
                            foreach (var card in railList.Children()) Card(card, 0f, 0f, 0f, 0f, 0f, 0f, EasingMode.Linear, EasingMode.Linear, 70f, 0f);
                        }).StartingIn(800);
                }
            }
            if (playLabel != null) playLabel.text = open ? "게임 시작" : "모드 선택";
        }
        Label railHint;
        readonly Dictionary<string, Button> railButtons = new Dictionary<string, Button>();
        // Each card's glass (10-27, design A): brighter, with a fuller gold edge, while its mode is picked.
        readonly Dictionary<string, GlassPlate> railPlates = new Dictionary<string, GlassPlate>();
        // The picked mode, and the last card clicked and when (two clicks on one card in this time: start).
        const float DoubleClick = .4f;
        string pickedRail, lastRailKey;
        float lastRailClick = -10f;

        // Nameplates over the 3D lineup.
        VisualElement lineupLayer;
        Camera lineupCamera;
        readonly List<LineupEntry> lineup = new List<LineupEntry>();
        readonly List<VisualElement> plates = new List<VisualElement>();
        readonly List<Button> inviteButtons = new List<Button>();
        // Voice chat on the nameplates (R131): a loudspeaker before each member's name (party green
        // while talking, red when I muted them), and a click on another member's plate opens a small
        // menu with one button, 음소거 / 음소거 풀기 (kept on this PC, the other side never knows).
        readonly List<ChessGlyphs.SpeakerMark> plateVoices = new List<ChessGlyphs.SpeakerMark>();
        readonly List<VisualElement> plateRings = new List<VisualElement>(), plateRows = new List<VisualElement>();
        VisualElement voiceMenu;
        Button voiceMenuButton;
        ChessGlyphs.SpeakerMark voiceMenuIcon;
        Label voiceMenuName, voiceMenuText;
        int voiceMenuIndex = -1;

        public bool IsTyping
        {
            get
            {
                var focused = code?.panel?.focusController.focusedElement as VisualElement;
                return focused != null && (focused == code || code.Contains(focused));
            }
        }
        public bool FriendsOpen => Visible(friendsPanel);

        // The centre of my name top right, as a fraction of the screen from the top
        // left: the transition into the name screen closes there.
        public Vector2 ProfileAt
        {
            get
            {
                if (profileOpen?.panel == null || root == null || root.worldBound.width <= 0) return new Vector2(.9f, .05f);
                var c = profileOpen.worldBound.center;
                var r = root.worldBound;
                return new Vector2((c.x - r.x) / r.width, (c.y - r.y) / r.height);
            }
        }

        public void Build(VisualTreeAsset layout, ThemeStyleSheet theme, PanelSettings settings, Vector2Int referenceResolution)
        {
            root = RuntimePanels.Create(gameObject, layout, theme, settings, referenceResolution, out ownedPanel);
            if (root == null) return;

            status = root.Q<Label>("status"); details = root.Q<Label>("details");
            profile = root.Q<Label>("profile"); version = root.Q<Label>("version");
            toast = root.Q<Label>("toast"); offline = root.Q<VisualElement>("offline");
            offlineText = root.Q<Label>("offline-text");
            partyCount = root.Q<Label>("party-count"); partyCode = root.Q<Label>("party-code");
            bots = root.Q<Label>("bots"); botsNote = root.Q<Label>("bots-note");
            busy = root.Q<VisualElement>("busy");
            busyTitle = root.Q<Label>("busy-title"); busySub = root.Q<Label>("busy-sub");
            spinner = root.Q<VisualElement>("spinner");
            modeName = root.Q<Label>("mode-name"); modeTagline = root.Q<Label>("mode-tagline");
            modeBadge = root.Q<Label>("mode-badge"); modeTile = root.Q<VisualElement>("mode-tile");
            modeChange = root.Q<Label>("mode-change"); modeHint = root.Q<Label>("mode-hint");
            matchPanel = root.Q<VisualElement>("match-panel");
            matchKicker = root.Q<Label>("match-kicker"); matchTitle = root.Q<Label>("match-title");
            matchTimer = root.Q<Label>("match-timer"); matchNote = root.Q<Label>("match-note");
            matchCount = root.Q<Label>("match-count"); matchLive = root.Q<VisualElement>("match-live");
            roomTools = root.Q<VisualElement>("room-tools"); roomCode = root.Q<Label>("room-code");
            testBots = root.Q<VisualElement>("test-bots");
            allyDummies = root.Q<Label>("ally-dummies"); enemyDummies = root.Q<Label>("enemy-dummies");
            testHint = root.Q<Label>("test-hint"); partyBotsTitle = root.Q<Label>("party-bots-title");
            slotsOurs = root.Q<VisualElement>("slots-ours"); slotsTheirs = root.Q<VisualElement>("slots-theirs");
            code = root.Q<TextField>("code");
            friendsPanel = root.Q<VisualElement>("friends"); detailsPanel = root.Q<VisualElement>("details-card");
            partyPanel = root.Q<VisualElement>("party-card");
            codeModal = root.Q<VisualElement>("code-modal"); modeModal = root.Q<VisualElement>("mode-modal");
            friendsList = root.Q<VisualElement>("friends-list");
            friendsInfo = root.Q<Label>("friends-info"); friendsPage = root.Q<Label>("friends-page");
            modeList = root.Q<VisualElement>("mode-list");
            railList = root.Q<VisualElement>("mode-rail-list");
            railHint = root.Q<Label>("mode-rail-hint");
            lineupLayer = root.Q<VisualElement>("lineup");
            for (int i = 0; i < TeamReservations.TeamSize; i++)
            {
                ourSlots.Add(Slot(slotsOurs));
                theirSlots.Add(Slot(slotsTheirs));
            }

            root.focusable = true;
            root.RegisterCallback<PointerDownEvent>(evt =>
            {
                // One-shot proof that the panel receives pointer events at all. If a
                // button never responds and this never logs, the runtime input
                // backend is the problem, not the layout or the enabled states.
                if (!pointerSeen) { pointerSeen = true; Debug.Log("[ChessFight] HUD 포인터 입력 확인됨."); }
                var target = evt.target as VisualElement;
                // A click outside the nameplate menu closes it (its own plate toggles it instead).
                if (voiceMenu != null && voiceMenuIndex >= 0 &&
                    !(target != null && (target == voiceMenu || voiceMenu.Contains(target) || target.ClassListContains("plate-hit"))))
                    CloseVoiceMenu();
                // Clicking anywhere but the number field hands focus back, so the
                // keyboard shortcuts work again.
                if (code == null) return;
                if (target != code && (target == null || !code.Contains(target))) root.Focus();
            }, TrickleDown.TrickleDown);

            play = Bind(root, "play", PlayPressed);
            // 10-17 (승규 님): the mode cards wait below the screen; the button reads "모드 선택" until they
            // slide up, then "게임 시작".
            rail = root.Q<VisualElement>("mode-rail");
            playLabel = play?.Q<Label>(className: "a-cta-text");
            HoverRail(play);
            HoverRail(rail);
            SetRail(false);
            start = Bind(root, "start", () => StartGame?.Invoke());
            cancel = Bind(root, "cancel", () => Cancel?.Invoke());
            leaveParty = Bind(root, "leave-party", () => LeaveParty?.Invoke());
            createTest = Bind(root, "create-test", () => CreateTest?.Invoke());
            retry = Bind(root, "retry", () => RetrySteam?.Invoke());
            botsMore = Bind(root, "bots-more", () => AddBot?.Invoke());
            botsLess = Bind(root, "bots-less", () => RemoveBot?.Invoke());
            fillRoom = Bind(root, "fill-bots", () => FillRoom?.Invoke());
            clearRoomBots = Bind(root, "clear-bots", () => ClearRoomBots?.Invoke());
            allyMore = Bind(root, "ally-more", () => AddAllyDummy?.Invoke());
            allyLess = Bind(root, "ally-less", () => RemoveAllyDummy?.Invoke());
            enemyMore = Bind(root, "enemy-more", () => AddEnemyDummy?.Invoke());
            enemyLess = Bind(root, "enemy-less", () => RemoveEnemyDummy?.Invoke());
            copyParty = Bind(root, "copy-party", () => CopyParty?.Invoke());
            Bind(root, "copy-match", () => CopyMatch?.Invoke());

            partyOpen = Bind(root, "party-open", () => TogglePanel(partyPanel));
            Bind(root, "party-close", () => Close(partyPanel));
            friendsOpen = Bind(root, "friends-open", () => { if (FriendsOpen) Close(friendsPanel); else OpenFriends(); });
            profileOpen = Bind(root, "profile-open", () => Rename?.Invoke());
            Bind(root, "details-open", () => TogglePanel(detailsPanel));
            Bind(root, "details-close", () => Close(detailsPanel));
            codeOpen = Bind(root, "code-open", () => Open(codeModal));
            Bind(root, "code-close", () => Close(codeModal));
            modeOpen = Bind(root, "mode-open", () => Open(modeModal));
            Bind(root, "mode-close", () => Close(modeModal));

            joinParty = Bind(root, "join-party", () => { if (TryReadCode(out ulong id)) { Close(codeModal); JoinParty?.Invoke(id); } });
            joinMatch = Bind(root, "join-test", () => { if (TryReadCode(out ulong id)) { Close(codeModal); JoinMatch?.Invoke(id); } });
            // Pasting sidesteps the number field entirely, which matters because a
            // panel that gets no pointer events gets no keystrokes either.
            Bind(root, "paste", () => { if (code != null) code.value = GUIUtility.systemCopyBuffer.Trim(); });

            Bind(root, "friends-close", () => Close(friendsPanel));
            Bind(root, "friends-refresh", () => RefreshFriends?.Invoke());
            Bind(root, "friends-overlay", () => SteamOverlayInvite?.Invoke());
            Bind(root, "friends-prev", () => { friendPage--; DrawFriends(); });
            Bind(root, "friends-next", () => { friendPage++; DrawFriends(); });
            friendsOnline = Bind(root, "friends-online", () => { onlineOnly = true; friendPage = 0; DrawFriends(); });
            friendsAll = Bind(root, "friends-all", () => { onlineOnly = false; friendPage = 0; DrawFriends(); });

            BuildModeList();
            BuildModeRail();
            SetRail(false, true);   // the cards exist now: hide them and stop them taking the pointer
            BuildVoiceMenu();
            Dress();
            // The chat (ChatBox, on the runtime) stays shut while the room-number
            // box or a picker is open.
            ChatBox.Blocker = chatBlocker = () => IsTyping || Visible(codeModal) || Visible(modeModal);
            // Start from a known state instead of trusting the stylesheet defaults,
            // so "is this panel open" is answerable before the first layout pass.
            foreach (var hidden in new[] { friendsPanel, detailsPanel, codeModal, modeModal, toast, offline, busy, matchPanel, roomTools, testBots })
                Show(hidden, false);
            diagnoseAt = Time.unscaledTime + 1f;
        }

        // Lobby design A (R83), the name screen's look, where USS cannot express it:
        // the warm shades behind the HUD, the panels' fill with their gold and silver
        // rule, the tab and title rules, the silver "게임 모드", the line icons and
        // the ivory start button. Only the 2D layer changes; the stage is LobbyStage's.
        static readonly Color Gold = PieceFigure.Hex(0xD9AE62), Brass = PieceFigure.Hex(0xE2B866), Ink = PieceFigure.Hex(0x24170D);
        static readonly Color Silver = PieceFigure.Hex(0xDCE2E8);
        // The name screen's title: linear-gradient(180deg, #FFF 0%, #E4E9EE 28%, #A3AEB9 49%, #66707B 53%, #BCC5CE 76%, #F2F5F8 100%).
        static readonly (float at, Color c)[] SilverStops =
        {
            (0f, Color.white), (.28f, PieceFigure.Hex(0xE4E9EE)), (.49f, PieceFigure.Hex(0xA3AEB9)),
            (.53f, PieceFigure.Hex(0x66707B)), (.76f, PieceFigure.Hex(0xBCC5CE)), (1f, PieceFigure.Hex(0xF2F5F8))
        };

        void Dress()
        {
            // 10-17, the lobby castle, option B "ebony lacquer and gold leaf": ebony shades only where
            // text sits, ebony panels with a gold rule over the bright courtyard (was warm dark glass, R83).
            Color shade = new Color(18 / 255f, 14 / 255f, 11 / 255f), floor = new Color(16 / 255f, 12 / 255f, 9 / 255f);
            const MenuArt.HudBlend dark = MenuArt.HudBlend.Shade;
            Paint(root.Q("scrim-top"), MenuArt.Ramp(true, dark, (0f, Alpha(shade, .85f)), (.46f, Alpha(shade, .44f)), (1f, Alpha(shade, 0f))));
            Paint(root.Q("scrim-right"), MenuArt.Ramp(false, dark, (0f, Alpha(shade, 0f)), (.35f, Alpha(shade, .38f)), (1f, Alpha(shade, .55f))));
            Paint(root.Q("scrim-bottom"), MenuArt.Ramp(true, dark, (0f, Alpha(floor, 0f)), (.58f, Alpha(floor, .4f)), (1f, Alpha(floor, .62f))));

            // Windows that open over other HUD (friends, details, the pickers) are nearly solid.
            var cover = MenuArt.Ramp(true, dark, (0f, new Color(26 / 255f, 21 / 255f, 17 / 255f, .98f)), (1f, new Color(17 / 255f, 13 / 255f, 10 / 255f, .99f)));
            root.Query<VisualElement>(className: "a-panel").ForEach(e =>
            {
                // 10-27 (design A): the panels over the lobby (the matching card, the banner) are smoked glass with
                // a gold hairline; the windows that open over other HUD stay nearly solid.
                if (!e.ClassListContains("side-card") && !e.ClassListContains("modal-card"))
                {
                    e.AddToClassList("glass");
                    GlassPlate.On(e);
                    return;
                }
                Paint(e, cover);
                e.Insert(0, Rule(46f, 150f, 2f, "a-panel-rule"));
            });
            // On the tab's box, not the label: a label with children stops measuring its text.
            root.Query<VisualElement>(className: "a-tab-box").ForEach(e => e.Add(Rule(14f, 30f, 2f, "a-tab-rule")));
            root.Q("rail-title")?.Add(BandText("게임 모드", 27f, 220f, 32f, SilverStops, 24));
            root.Q("rail-rule")?.Add(Rule(46f, 170f, 3f));
            var line = root.Q<VisualElement>("match-line");
            if (line != null)
            {
                Color red = PieceFigure.Hex(0xFF4D62), gold = PieceFigure.Hex(0xFFC93D), clear = new Color(1f, .3f, .38f, 0f);
                line.style.backgroundImage = new StyleBackground(MenuArt.Line(clear, red, gold, red, clear));
            }

            IconText(friendsOpen, MenuMarks.Icon.People);
            IconText(root.Q<Button>("details-open"), MenuMarks.Icon.Info);
            IconText(codeOpen, MenuMarks.Icon.Hash);
            IconText(createTest, MenuMarks.Icon.Lock);
            IconText(copyParty, MenuMarks.Icon.Copy);
            IconText(leaveParty, MenuMarks.Icon.Exit);
            root.Q("profile-crown")?.Add(new MenuMarks.CrownMark(Brass, Color.clear, 0f));
            // 10-27 (design A): smoked glass with a polished gold edge, no arrow, no key hints under it
            if (play != null) GlassPlate.Button(play);
            if (start != null) GlassPlate.Button(start, 10f);
            // Whatever still carries the slab class (nothing in the lobby since R83).
            ChunkyButtons.Attach(root);
        }

        static Color Alpha(Color c, float a) { c.a = a; return c; }

        static void Paint(VisualElement e, Texture2D texture)
        {
            if (e != null && texture != null) e.style.backgroundImage = new StyleBackground(texture);
        }

        // The name screen's rule: a short gold bar, then silver fading out to the right.
        static VisualElement Rule(float gold, float fade, float thick, string className = null)
        {
            var rule = new VisualElement { pickingMode = PickingMode.Ignore };
            rule.style.flexDirection = FlexDirection.Row;
            if (className != null) rule.AddToClassList(className);
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.width = gold; bar.style.height = thick; bar.style.backgroundColor = Gold;
            var tail = new VisualElement { pickingMode = PickingMode.Ignore };
            tail.style.width = fade; tail.style.height = thick;
            tail.style.backgroundImage = new StyleBackground(MenuArt.Ramp(false, MenuArt.HudBlend.Tint, (0f, Silver), (1f, Alpha(Silver, 0f))));
            rule.Add(bar);
            rule.Add(tail);
            return rule;
        }

        // Display-font text in a vertical gradient, left aligned: in each horizontal
        // band of the box a copy of the label is cut to that band and coloured there.
        static VisualElement BandText(string text, float size, float w, float h, (float at, Color c)[] stops, int bands)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.width = w; box.style.height = h;
            for (int i = 0; i < bands; i++)
            {
                float y0 = h * i / bands, y1 = h * (i + 1) / bands;
                var clip = new VisualElement { pickingMode = PickingMode.Ignore };
                clip.style.position = Position.Absolute;
                clip.style.left = 0; clip.style.top = y0; clip.style.width = w;
                clip.style.height = y1 - y0 + (i < bands - 1 ? .5f : 0f);
                clip.style.overflow = Overflow.Hidden;
                var label = new Label(text) { pickingMode = PickingMode.Ignore };
                RuntimePanels.Display(label);
                label.style.position = Position.Absolute;
                label.style.left = 0; label.style.top = -y0; label.style.width = w; label.style.height = h;
                label.style.fontSize = size;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.marginLeft = 0; label.style.paddingLeft = 0;
                label.style.color = Sample(stops, (y0 + y1) / 2f / h);
                clip.Add(label);
                box.Add(clip);
            }
            return box;
        }

        static Color Sample((float at, Color c)[] stops, float t)
        {
            if (t <= stops[0].at) return stops[0].c;
            for (int i = 1; i < stops.Length; i++)
                if (t <= stops[i].at) return Color.Lerp(stops[i - 1].c, stops[i].c, (t - stops[i - 1].at) / Mathf.Max(1e-5f, stops[i].at - stops[i - 1].at));
            return stops[stops.Length - 1].c;
        }

        // A line icon before the button's text (Button.text cannot sit beside a
        // child, so the text moves into a label; SetText reaches it).
        static void IconText(Button button, MenuMarks.Icon icon)
        {
            if (button == null) return;
            var mark = new MenuMarks.IconMark(icon);
            mark.AddToClassList("a-icon");
            var label = new Label(button.text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("a-btn-text");
            button.text = "";
            button.Add(mark);
            button.Add(label);
        }

        static void SetText(Button button, string text)
        {
            if (button == null) return;
            var label = button.Q<Label>(className: "a-btn-text") ?? button.Q<Label>(className: "a-cta-text");
            if (label != null) label.text = text; else button.text = text;
        }

        static VisualElement Slot(VisualElement parent)
        {
            var slot = new VisualElement();
            slot.AddToClassList("slot");
            parent?.Add(slot);
            return slot;
        }

        // ---------- frame loop: shortcuts, fallback clicks, toast, spinner, lineup ----------

        void Update()
        {
            if (root == null) return;
            if (!diagnosed && Time.unscaledTime >= diagnoseAt) { diagnosed = true; LogDiagnostics(); }
            if (toastUntil > 0 && Time.unscaledTime >= toastUntil) { toastUntil = 0; Show(toast, false); }
            if (spinner != null && Visible(busy))
            {
                spin = (spin + Time.unscaledDeltaTime * 360f) % 360f;
                spinner.style.rotate = new StyleRotate(new Rotate(new Angle(spin, AngleUnit.Degree)));
            }
            // The banner's live dot breathes once a second.
            if (matchLive != null && Visible(matchPanel))
                matchLive.style.opacity = .25f + .75f * (.5f + .5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f));
            // While the chat is open its keys are its own: Enter sends a line, it
            // does not start a game. So are the name screen's (R85: Enter there
            // changes the name) and nothing here moves under a transition.
            bool covered = NameScreen.Showing || SceneTransition.Busy;
            if (!ChatBox.KeysHeld && !covered) Shortcuts();
            FallbackClick(covered);
        }

        void LateUpdate() => PlaceLineup();

        // Keys work whatever happens to the pointer path, which is the point: the
        // start button is also Enter, the friend list F, the mode picker M.
        void Shortcuts()
        {
            if (IsTyping)
            {
                if (LegacyKeys.Down(KeyCode.Escape)) { Close(codeModal); root.Focus(); }
                return;
            }
            // Esc closes the settings window, else the topmost lobby window, else
            // opens the settings window (R64). It never calls off matching: the
            // entrance screen's "매칭 취소" is click only.
            if (LegacyKeys.Down(KeyCode.Escape))
            {
                if (SettingsWindow.IsOpen) SettingsWindow.Back();
                else if (voiceMenuIndex >= 0) CloseVoiceMenu();
                else if (Visible(modeModal)) Close(modeModal);
                else if (railOpen && Usable(play)) SetRail(false);
                else if (Visible(codeModal)) Close(codeModal);
                else if (Visible(friendsPanel)) Close(friendsPanel);
                else if (Visible(detailsPanel)) Close(detailsPanel);
                else SettingsWindow.Open();
                return;
            }
            if (SettingsWindow.IsOpen) return;
            bool modal = Visible(modeModal) || Visible(codeModal);
            if (!modal && (LegacyKeys.Down(KeyCode.Return) || LegacyKeys.Down(KeyCode.KeypadEnter)))
            {
                if (Usable(start)) StartGame?.Invoke();
                else if (Usable(play)) PlayPressed();
            }
            if (!modal && LegacyKeys.Down(KeyCode.F)) { if (FriendsOpen) Close(friendsPanel); else if (canInvite) OpenFriends(); }
            if (!modal && LegacyKeys.Down(KeyCode.M) && Usable(modeOpen)) Open(modeModal);
        }

        // If the panel never receives a real pointer event, drive the buttons from
        // the mouse directly. This is a safety net, not the intended path: it
        // switches itself off the moment a genuine event arrives, and it cannot
        // help with typing, which is what the paste button is for.
        void FallbackClick(bool covered)
        {
            // A click on the name screen or the transition is not the lobby's.
            if (covered) { pressedAt = -1; return; }
            if (pointerSeen || fallbackDead || SettingsWindow.IsOpen) return;
            bool pressed;
            Vector2 screen;
            try { pressed = Input.GetMouseButtonDown(0); screen = Input.mousePosition; }
            catch (InvalidOperationException)
            {
                fallbackDead = true;
                Debug.LogError("[ChessFight] 레거시 입력이 꺼져 있어 대체 클릭 처리도 쓸 수 없습니다. " +
                               "Project Settings > Player > Active Input Handling을 'Input Manager (Old)'로 바꾸고 Unity를 재시작하세요.");
                return;
            }
            // A press is handled a frame late: the genuine pointer event for the same
            // click is dispatched after this Update, and when it comes it must win
            // (pointerSeen, above, then drops the press). Handled at once, the very
            // first click of every session also went through here, and a click on
            // no button (the chat line) could land on a button elsewhere through
            // the plain-point guess below.
            bool due = pressedAt >= 0 && Time.frameCount > pressedAt;
            Vector2 point = pressedPoint;
            if (due) pressedAt = -1;
            if (pressed) { pressedAt = Time.frameCount; pressedPoint = screen; }
            if (due) Fire(point);
        }

        void Fire(Vector2 screen)
        {
            var panel = root.panel;
            if (panel == null) return;
            // Unity's own samples flip Y before converting; try the plain point too
            // rather than betting the whole fallback on the convention.
            var hit = Resolve(panel, new Vector2(screen.x, Screen.height - screen.y)) ?? Resolve(panel, screen);
            if (hit == null || !hit.Value.button.enabledInHierarchy) return;
            if (hit.Value.button.ClassListContains(ChunkyButtons.ClassName)) ChunkyButtons.Pulse(hit.Value.button);
            hit.Value.action();
        }

        struct Hit { public Button button; public Action action; }

        Hit? Resolve(IPanel panel, Vector2 point)
        {
            for (var element = panel.Pick(point); element != null; element = element.parent)
                if (element is Button button && actions.TryGetValue(button, out var action))
                    return new Hit { button = button, action = action };
            return null;
        }

        // ---------- panels ----------

        void OpenFriends()
        {
            if (!canInvite) return;
            Close(detailsPanel);
            Close(partyPanel);
            Open(friendsPanel);
            FriendsOpened?.Invoke();
        }

        public void CloseFriends() => Close(friendsPanel);

        void Open(VisualElement panel)
        {
            // One side card at a time (party, friends, details share the right edge).
            if (panel == detailsPanel || panel == partyPanel) Close(friendsPanel);
            if (panel != detailsPanel) Close(detailsPanel);
            if (panel != partyPanel) Close(partyPanel);
            Show(panel, true);
        }
        void Close(VisualElement panel) => Show(panel, false);
        void TogglePanel(VisualElement panel) { if (Visible(panel)) Close(panel); else Open(panel); }

        public void ShowToast(string text, bool error = false)
        {
            if (toast == null || string.IsNullOrEmpty(text)) return;
            toast.text = text;
            toast.EnableInClassList("toast-error", error);
            Show(toast, true);
            toastUntil = Time.unscaledTime + ToastSeconds;
        }

        // ---------- lineup nameplates ----------

        // The lobby hands over who stands where; positions follow the camera each frame.
        public void SetLineup(IReadOnlyList<LineupEntry> entries, Camera camera)
        {
            lineupCamera = camera;
            if (lineupLayer == null || Same(entries)) return;
            lineup.Clear();
            lineup.AddRange(entries);
            foreach (var stale in inviteButtons) actions.Remove(stale);
            inviteButtons.Clear();
            plates.Clear();
            plateVoices.Clear();
            plateRings.Clear();
            plateRows.Clear();
            CloseVoiceMenu();
            lineupLayer.Clear();
            for (int i = 0; i < lineup.Count; i++)
            {
                var entry = lineup[i];
                var plate = entry.Invite ? InviteSpot() : Nameplate(entry, i);
                plate.style.display = DisplayStyle.None;   // until placed
                lineupLayer.Add(plate);
                plates.Add(plate);
                if (entry.Invite) { plateVoices.Add(null); plateRings.Add(null); plateRows.Add(null); }
            }
        }

        bool Same(IReadOnlyList<LineupEntry> entries)
        {
            if (entries.Count != lineup.Count) return false;
            for (int i = 0; i < entries.Count; i++)
            {
                var a = entries[i]; var b = lineup[i];
                if (a.Id != b.Id || a.Invite != b.Invite || a.Name != b.Name || a.Tag != b.Tag || a.Leader != b.Leader) return false;
            }
            return true;
        }

        VisualElement Nameplate(LineupEntry entry, int index)
        {
            var plate = new VisualElement { pickingMode = PickingMode.Ignore };
            plate.AddToClassList("plate");
            if (entry.Me) plate.AddToClassList("plate-me");
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("plate-row");
            if (entry.Leader)
            {
                var crown = new VisualElement { pickingMode = PickingMode.Ignore };
                crown.AddToClassList("plate-crown");
                crown.Add(new MenuMarks.CrownMark(Brass, Color.clear, 0f));
                row.Add(crown);
            }
            ChessGlyphs.SpeakerMark voice = null;
            VisualElement ring = null;
            if (!entry.Bot)
            {
                var holder = new VisualElement { pickingMode = PickingMode.Ignore };
                holder.AddToClassList("plate-voice");
                ring = new VisualElement { pickingMode = PickingMode.Ignore };
                ring.AddToClassList("plate-voice-ring");
                holder.Add(ring);
                voice = new ChessGlyphs.SpeakerMark(PlateQuiet, 2.4f);
                voice.AddToClassList("plate-voice-icon");
                holder.Add(voice);
                holder.style.display = DisplayStyle.None;   // until the voice runs
                row.Add(holder);
            }
            var name = new Label(entry.Name) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("plate-name");
            row.Add(name);
            plate.Add(row);
            plateVoices.Add(voice);
            plateRings.Add(ring);
            plateRows.Add(row);
            // Another person's plate opens the mute menu; mine and the bots' do nothing.
            if (!entry.Me && !entry.Bot && entry.Id != 0)
            {
                var hit = new Button { focusable = false };
                hit.AddToClassList("plate-hit");
                Action open = () => ToggleVoiceMenu(index);
                hit.clicked += open;
                actions[hit] = open;
                inviteButtons.Add(hit);
                row.Add(hit);
            }
            if (!string.IsNullOrEmpty(entry.Tag))
            {
                var tag = new Label(entry.Tag) { pickingMode = PickingMode.Ignore };
                tag.AddToClassList("plate-tag");
                plate.Add(tag);
            }
            return plate;
        }

        VisualElement InviteSpot()
        {
            var spot = new VisualElement { pickingMode = PickingMode.Ignore };
            spot.AddToClassList("invite-spot");
            // 10-17: one pill button "+ 친구 초대" (was a round "+" with a label under it)
            var plus = new Button { text = "+  친구 초대", focusable = false };
            plus.AddToClassList("invite-plus");
            RuntimePanels.Display(plus);
            Action open = OpenFriends;
            plus.clicked += open;
            actions[plus] = open;
            inviteButtons.Add(plus);
            spot.Add(plus);
            return spot;
        }

        void PlaceLineup()
        {
            if (root?.panel == null || lineupCamera == null) return;
            for (int i = 0; i < plates.Count && i < lineup.Count; i++)
            {
                bool invite = lineup[i].Invite;
                Vector3 world = invite ? LobbyStage.InviteAnchor(i) : LobbyStage.Anchor(i);
                bool front = lineupCamera.WorldToViewportPoint(world).z > 0;
                plates[i].style.display = front ? DisplayStyle.Flex : DisplayStyle.None;
                if (!front) continue;
                Vector2 at = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, lineupCamera);
                // Fixed widths from the USS, so centring needs no layout pass.
                plates[i].style.left = at.x - (invite ? 70f : 100f);
                plates[i].style.top = at.y - (invite ? 18f : 64f);
            }
            for (int i = 0; i < plateVoices.Count && i < lineup.Count; i++) DrawVoice(i);
            PlaceVoiceMenu();
        }

        // ---------- voice on the nameplates (R131) ----------

        static readonly Color PlateQuiet = new Color(239 / 255f, 228 / 255f, 204 / 255f, .5f);

        void DrawVoice(int i)
        {
            var mark = plateVoices[i];
            if (mark == null) return;
            var holder = mark.parent;
            bool running = VoiceBoard.Running;
            holder.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;
            var row = plateRows[i];
            if (!running) { row.style.borderTopColor = row.style.borderBottomColor = row.style.borderLeftColor = row.style.borderRightColor = StyleKeyword.Null; return; }
            var entry = lineup[i];
            bool muted = !entry.Me && VoiceBoard.IsMuted(entry.Id);
            var channel = VoiceChannel.Party;
            bool talking = !muted && VoiceBoard.IsTalking(entry.Id, out channel);
            var color = talking ? VoiceBoard.ColorOf(channel) : muted ? VoiceBoard.MutedColor : PlateQuiet;
            mark.Set(muted ? ChessGlyphs.SpeakerMark.Look.Muted : talking ? ChessGlyphs.SpeakerMark.Look.Talking : ChessGlyphs.SpeakerMark.Look.Quiet, color);
            // A ring that keeps spreading from the loudspeaker while they talk, and the plate's edge in their colour.
            var ring = plateRings[i];
            float t = Time.unscaledTime / 1.1f % 1f;
            ring.style.display = talking ? DisplayStyle.Flex : DisplayStyle.None;
            if (talking)
            {
                ring.style.scale = new Scale(Vector3.one * Mathf.Lerp(.7f, 1.7f, t));
                ring.style.opacity = .9f * (1f - t);
                ring.style.borderTopColor = ring.style.borderBottomColor = ring.style.borderLeftColor = ring.style.borderRightColor = color;
            }
            StyleColor edge = talking ? color : muted ? new Color(VoiceBoard.MutedColor.r, VoiceBoard.MutedColor.g, VoiceBoard.MutedColor.b, IntroFlowStyle.Light(.6f))
                                                      : (StyleColor)StyleKeyword.Null;
            row.style.borderTopColor = row.style.borderBottomColor = row.style.borderLeftColor = row.style.borderRightColor = edge;
        }

        void BuildVoiceMenu()
        {
            voiceMenu = new VisualElement { pickingMode = PickingMode.Position };
            voiceMenu.AddToClassList("plate-menu");
            voiceMenuName = new Label { pickingMode = PickingMode.Ignore, enableRichText = false };
            voiceMenuName.AddToClassList("plate-menu-name");
            voiceMenu.Add(voiceMenuName);
            voiceMenuButton = new Button { focusable = false };
            voiceMenuButton.AddToClassList("plate-menu-btn");
            voiceMenuIcon = new ChessGlyphs.SpeakerMark(PieceFigure.Hex(0xEFE4CC), 2.4f);
            voiceMenuIcon.AddToClassList("plate-menu-icon");
            voiceMenuButton.Add(voiceMenuIcon);
            voiceMenuText = new Label("음소거") { pickingMode = PickingMode.Ignore };
            voiceMenuText.AddToClassList("plate-menu-text");
            voiceMenuButton.Add(voiceMenuText);
            Action toggle = () =>
            {
                if (voiceMenuIndex >= 0 && voiceMenuIndex < lineup.Count) VoiceBoard.ToggleMuted(lineup[voiceMenuIndex].Id);
                CloseVoiceMenu();
            };
            voiceMenuButton.clicked += toggle;
            actions[voiceMenuButton] = toggle;
            voiceMenu.Add(voiceMenuButton);
            root.Add(voiceMenu);
            Show(voiceMenu, false);
        }

        void ToggleVoiceMenu(int index)
        {
            if (voiceMenu == null || index < 0 || index >= lineup.Count) return;
            if (voiceMenuIndex == index) { CloseVoiceMenu(); return; }
            voiceMenuIndex = index;
            var entry = lineup[index];
            bool muted = VoiceBoard.IsMuted(entry.Id);
            voiceMenuName.text = entry.Name;
            voiceMenuText.text = muted ? "음소거 풀기" : "음소거";
            voiceMenuButton.EnableInClassList("plate-menu-btn-on", muted);
            voiceMenuIcon.Set(muted ? ChessGlyphs.SpeakerMark.Look.Quiet : ChessGlyphs.SpeakerMark.Look.Muted,
                              muted ? PieceFigure.Hex(0x1A1410) : PieceFigure.Hex(0xEFE4CC));
            Show(voiceMenu, true);
            voiceMenu.BringToFront();
            PlaceVoiceMenu();
        }

        void CloseVoiceMenu()
        {
            voiceMenuIndex = -1;
            Show(voiceMenu, false);
        }

        // Centred just above the plate it belongs to, which moves with the camera: clear of my own plate
        // in the middle and of the game-mode list on the right.
        void PlaceVoiceMenu()
        {
            if (voiceMenu == null || voiceMenuIndex < 0) return;
            if (voiceMenuIndex >= plateRows.Count || plateRows[voiceMenuIndex] == null || !VoiceBoard.Running) { CloseVoiceMenu(); return; }
            var box = plateRows[voiceMenuIndex].worldBound;
            if (box.width <= 0) return;
            var top = root.WorldToLocal(new Vector2(box.center.x, box.yMin));
            float width = voiceMenu.layout.width > 0 ? voiceMenu.layout.width : 130f;
            float height = voiceMenu.layout.height > 0 ? voiceMenu.layout.height : 66f;
            voiceMenu.style.left = top.x - width * .5f;
            voiceMenu.style.top = Mathf.Max(70f, top.y - height - 6f);
        }

        // ---------- mode picker ----------

        void BuildModeList()
        {
            if (modeList == null) return;
            foreach (var mode in GameModes.All)
            {
                var option = new Button { focusable = false };
                option.AddToClassList("mode-option");
                option.AddToClassList("row");
                if (!mode.Playable) option.AddToClassList("mode-locked");

                var tile = new VisualElement { pickingMode = PickingMode.Ignore };
                tile.AddToClassList("mode-tile");
                tile.style.backgroundColor = ModeColor(mode.Key);
                var badge = new Label(mode.Badge) { pickingMode = PickingMode.Ignore };
                badge.AddToClassList("mode-badge");
                tile.Add(badge);
                option.Add(tile);

                var text = new VisualElement { pickingMode = PickingMode.Ignore };
                text.AddToClassList("grow");
                var name = new Label(mode.Name) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("mode-name");
                var tagline = new Label(mode.Tagline) { pickingMode = PickingMode.Ignore };
                tagline.AddToClassList("mode-tagline");
                var summary = new Label(mode.Summary) { pickingMode = PickingMode.Ignore };
                summary.AddToClassList("mode-summary");
                text.Add(name); text.Add(tagline); text.Add(summary);
                option.Add(text);

                var state = new Label(mode.Playable ? "" : "준비 중") { name = "state", pickingMode = PickingMode.Ignore };
                state.AddToClassList("mode-state");
                option.Add(state);

                string key = mode.Key;
                Action pick = () => { Close(modeModal); PickMode?.Invoke(key); };
                option.clicked += pick;
                actions[option] = pick;
                modeButtons[key] = option;
                modeList.Add(option);
            }
        }

        // The right rail: every mode as a card with its piece, always on screen, so
        // picking a mode is one click. Followers see the leader's choice.
        void BuildModeRail()
        {
            if (railList == null) return;
            foreach (var mode in GameModes.All)
            {
                var card = new Button { focusable = false };
                card.AddToClassList("rail-card");
                if (!mode.Playable) card.AddToClassList("rail-locked");
                // 10-27 (design A, was option B's ebony lacquer): smoked glass with a gold hairline
                railPlates[mode.Key] = GlassPlate.On(card);

                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList("rail-art");
                var portrait = ModePortrait(mode.Key);
                if (portrait != null) art.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(portrait));
                card.Add(art);
                var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                bar.AddToClassList("rail-bar");
                card.Add(bar);

                var text = new VisualElement { pickingMode = PickingMode.Ignore };
                text.AddToClassList("rail-text");
                var state = new Label { name = "state", pickingMode = PickingMode.Ignore };
                state.AddToClassList("rail-badge");
                var name = new Label(mode.Name) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("rail-name");
                RuntimePanels.Display(name);
                var summary = new Label(WrapWords(mode.Summary, RailLineUnits)) { pickingMode = PickingMode.Ignore };
                summary.AddToClassList("rail-summary");
                var tagline = new Label(mode.Tagline) { pickingMode = PickingMode.Ignore };
                tagline.AddToClassList("rail-tagline");
                // The line along the bottom: players and kind, then the state badge.
                var foot = new VisualElement { pickingMode = PickingMode.Ignore };
                foot.AddToClassList("rail-foot");
                foot.Add(tagline);
                foot.Add(state);
                text.Add(name); text.Add(summary); text.Add(foot);
                card.Add(text);

                string key = mode.Key;
                // 10-27 (승규 님): a double click on a card starts the game with that mode, as "게임 시작" does
                Action pick = () =>
                {
                    bool twice = lastRailKey == key && Time.unscaledTime - lastRailClick < DoubleClick;
                    lastRailKey = twice ? null : key;
                    lastRailClick = Time.unscaledTime;
                    if (key != pickedRail) PickMode?.Invoke(key);
                    if (twice) Play?.Invoke();
                };
                card.clicked += pick;
                actions[card] = pick;
                railButtons[key] = card;
                railList.Add(card);
            }
        }

        // UI Toolkit breaks Korean between any two syllables, even mid-word, so
        // break the card text at spaces here instead. Widths are estimated: a
        // Hangul syllable counts two units, anything else one.
        const int RailLineUnits = 32;   // .rail-text is 232px of 13px text

        static string WrapWords(string text, int units)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var wrapped = new System.Text.StringBuilder();
            int line = 0;
            foreach (string word in text.Split(' '))
            {
                int width = 0;
                foreach (char c in word) width += c >= 'ᄀ' ? 2 : 1;
                if (line > 0 && line + 1 + width > units) { wrapped.Append('\n'); line = 0; }
                else if (line > 0) { wrapped.Append(' '); line++; }
                wrapped.Append(word);
                line += width;
            }
            return wrapped.ToString();
        }

        // The piece that stars in each mode, posed as on the design sample: a pawn
        // running, the queen waving, a rook with its arm up for a fight.
        static RenderTexture ModePortrait(string key)
        {
            switch (key)
            {
                case "kingrush":
                    return MenuArt.Portrait("run", PieceKind.Pawn, 0,
                        new FigurePose { Leg = .7f, ArmLeft = 1.1f, ArmRight = -.1f, Lean = .22f, Jump = .15f }, new Vector2(3, 4), false);
                case "queenhill":
                    return MenuArt.Portrait("wave", PieceKind.Queen, 0, new FigurePose { ArmLeft = 2.3f, ArmRight = .5f, Jump = .05f }, new Vector2(1, 6), false);
                case "swordfight":
                    return MenuArt.Portrait("fight", PieceKind.Rook, 0, new FigurePose { ArmRight = 2f, ArmLeft = .4f, Lean = .08f }, new Vector2(-1.5f, 6), false, 7.2f, 1.35f);
                default:
                    return MenuArt.Portrait("stand", PieceKind.Pawn, 0, new FigurePose { Arm = .3f }, new Vector2(.6f, 6), false);
            }
        }

        static Color ModeColor(string key)
        {
            switch (key)
            {
                case "kingrush": return new Color(.27f, .51f, .94f);
                case "queenhill": return new Color(.62f, .38f, .88f);
                case "swordfight": return new Color(.86f, .32f, .28f);
                default: return new Color(.4f, .45f, .55f);
            }
        }

        // ---------- friends ----------

        // The bootstrap pushes a fresh snapshot while the panel is open. Party
        // members are marked instead of offered an invite.
        public void SetFriends(List<FriendInfo> snapshot, IEnumerable<ulong> party)
        {
            friends.Clear();
            if (snapshot != null) friends.AddRange(snapshot);
            partyIds.Clear();
            if (party != null) foreach (ulong id in party) partyIds.Add(id);
            DrawFriends();
        }

        void DrawFriends()
        {
            if (friendsList == null) return;
            // Dynamic buttons must leave the fallback router's map with their rows.
            foreach (var stale in friendButtons) actions.Remove(stale);
            friendButtons.Clear();
            friendsList.Clear();

            var shown = new List<FriendInfo>();
            int online = 0;
            foreach (var friend in friends)
            {
                if (friend.Presence != FriendPresence.Offline) online++;
                if (!onlineOnly || friend.Presence != FriendPresence.Offline) shown.Add(friend);
            }

            int pages = Mathf.Max(1, (shown.Count + FriendsPerPage - 1) / FriendsPerPage);
            friendPage = Mathf.Clamp(friendPage, 0, pages - 1);
            friendsOnline?.EnableInClassList("seg-on", onlineOnly);
            friendsAll?.EnableInClassList("seg-on", !onlineOnly);
            if (friendsPage != null) friendsPage.text = $"{friendPage + 1} / {pages}";
            if (friendsInfo != null) friendsInfo.text = friends.Count == 0 ? "" : $"{online} / {friends.Count} 온라인";

            if (shown.Count == 0)
            {
                var empty = new Label(friends.Count == 0 ? "친구 목록을 불러오는 중입니다."
                                    : onlineOnly ? "접속 중인 친구가 없습니다." : "친구가 없습니다.");
                empty.AddToClassList("friend-empty");
                friendsList.Add(empty);
                return;
            }
            int first = friendPage * FriendsPerPage;
            int end = Mathf.Min(shown.Count, first + FriendsPerPage);
            for (int i = first; i < end; i++)
            {
                // A heading wherever the presence group changes (the list is sorted by it).
                if (i == first || shown[i].Presence != shown[i - 1].Presence)
                    friendsList.Add(GroupHeading(shown[i].Presence, Count(shown, shown[i].Presence)));
                friendsList.Add(FriendRow(shown[i]));
            }
        }

        static int Count(List<FriendInfo> list, FriendPresence presence)
        {
            int n = 0;
            foreach (var f in list) if (f.Presence == presence) n++;
            return n;
        }

        static string Describe(FriendPresence presence) =>
            presence == FriendPresence.InGame ? "이 게임 중" : presence == FriendPresence.Online ? "온라인"
          : presence == FriendPresence.Away ? "자리 비움" : "오프라인";

        static VisualElement GroupHeading(FriendPresence presence, int count)
        {
            var row = new VisualElement();
            row.AddToClassList("friend-group");
            var label = new Label(Describe(presence));
            label.AddToClassList("friend-group-label");
            var number = new Label(count.ToString());
            number.AddToClassList("friend-group-label");
            row.Add(label); row.Add(number);
            return row;
        }

        VisualElement FriendRow(FriendInfo friend)
        {
            var row = new VisualElement();
            row.AddToClassList("friend-row");

            var avatar = new VisualElement();
            avatar.AddToClassList("avatar");
            avatar.style.backgroundColor = AvatarColor(friend.Id, friend.Presence);
            var letter = new Label(string.IsNullOrEmpty(friend.Name) ? "?" : friend.Name.Substring(0, 1));
            letter.AddToClassList("avatar-letter");
            avatar.Add(letter);
            row.Add(avatar);

            var text = new VisualElement();
            text.AddToClassList("friend-text");
            var name = new Label(friend.Name);
            name.AddToClassList("friend-name");
            var state = new Label(!string.IsNullOrEmpty(friend.Detail) ? friend.Detail
                                : friend.Presence == FriendPresence.Offline ? "오프라인"
                                : friend.Presence == FriendPresence.Away ? "자리 비움" : "Steam 온라인");
            state.AddToClassList("friend-state");
            text.Add(name); text.Add(state);
            row.Add(text);

            if (partyIds.Contains(friend.Id))
            {
                var member = new Label("내 파티");
                member.AddToClassList("friend-member");
                row.Add(member);
                return row;
            }
            ulong id = friend.Id;
            var button = new Button { text = "초대", focusable = false };
            button.AddToClassList("friend-invite");
            button.SetEnabled(canInvite);
            Action invite = () => InviteFriend?.Invoke(id);
            button.clicked += invite;
            actions[button] = invite;
            friendButtons.Add(button);
            row.Add(button);
            return row;
        }

        // Pastel per friend, greyed when they cannot join right now.
        static Color AvatarColor(ulong id, FriendPresence presence)
        {
            if (presence == FriendPresence.Offline) return new Color(.45f, .48f, .54f);
            float hue = (id * 2654435761UL % 360UL) / 360f;
            return Color.HSVToRGB(hue, .35f, .95f);
        }

        // ---------- render ----------

        public void Render(in HudModel model)
        {
            if (root == null) return;

            if (status != null) status.text = model.Status;
            if (details != null) details.text = model.Details;
            if (profile != null) profile.text = string.IsNullOrEmpty(model.PlayerName) ? "—" : model.PlayerName;
            if (version != null) version.text = model.Version;
            SetText(friendsOpen, model.Online ? $"친구 <color=#E2B866><b>{model.FriendsOnline}</b></color>" : "친구");

            Show(offline, !model.Online);
            if (offlineText != null && !string.IsNullOrEmpty(model.Status) && !model.Online) offlineText.text = model.Status;
            Enable(retry, model.CanRetry);

            RenderMode(model);

            if (partyCount != null) partyCount.text = $"{model.PartySize}<color=#E7BE72> / 6</color>";
            SetText(partyOpen, $"파티 <color=#E9C46A><b>{model.PartySize}</b></color>/6");
            if (partyCode != null) partyCode.text = model.PartyCode;
            if (bots != null) bots.text = model.Bots;
            if (botsNote != null) { botsNote.text = model.BotsNote; Show(botsNote, !string.IsNullOrEmpty(model.BotsNote)); }
            Enable(copyParty, model.CanCopyParty);
            Enable(botsMore, model.CanAddBot); Enable(botsLess, model.CanRemoveBot);
            Enable(leaveParty, model.CanLeaveParty);

            Show(play, !model.Busy);
            Enable(play, model.CanPlay);
            Show(busy, model.Busy);
            if (busyTitle != null) busyTitle.text = model.BusyTitle;
            if (busySub != null) busySub.text = model.BusySub;
            Enable(cancel, model.CanCancel);
            Show(start, model.ShowStart);
            Enable(start, model.CanStart);

            Show(matchPanel, model.ShowMatch);
            // The matchmaking panel takes the space above the lineup, as in the video.
            Show(lineupLayer, !model.ShowMatch);
            if (model.ShowMatch)
            {
                if (matchKicker != null) matchKicker.text = model.MatchKicker;
                if (matchTitle != null) matchTitle.text = model.MatchTitle;
                if (matchTimer != null) matchTimer.text = model.MatchTimer;
                if (matchNote != null) matchNote.text = model.MatchNote;
                Fill(ourSlots, model.OursFilled, model.OurTeam);
                Fill(theirSlots, model.TheirsFilled, 1 - model.OurTeam);
                if (matchCount != null) matchCount.text = (model.OursFilled + model.TheirsFilled) + " / " + TeamReservations.TeamSize * 2;
                if (matchNote != null) Show(matchNote, !string.IsNullOrEmpty(model.MatchNote));
            }
            Show(roomTools, model.ShowRoomTools);
            if (roomCode != null) roomCode.text = model.RoomCode;
            Enable(fillRoom, model.CanFillRoom); Enable(clearRoomBots, model.CanClearRoomBots);
            bool sword = model.ModeKey == GameModes.SwordFight.Key;
            Show(testBots, model.ShowTestBots);
            matchPanel?.EnableInClassList("dummy-room", model.ShowTestBots);
            if (allyDummies != null) allyDummies.text = model.AllyDummies.ToString();
            if (enemyDummies != null) enemyDummies.text = model.EnemyDummies.ToString();
            if (testHint != null) testHint.text = model.TestHint;
            Enable(allyMore, model.CanAddAlly); Enable(allyLess, model.CanRemoveAlly);
            Enable(enemyMore, model.CanAddEnemy); Enable(enemyLess, model.CanRemoveEnemy);
            SetText(createTest, sword ? "더미 테스트 (혼자 가능)" : "비공개 방");
            if (partyBotsTitle != null) partyBotsTitle.text = sword ? "파티 더미 (아군)" : "AI 봇";
            if (clearRoomBots != null) clearRoomBots.text = sword ? "추가 더미 비우기" : "봇 비우기";
            SetText(start, model.ShowTestBots ? "테스트 시작  Enter" : "경기 시작");

            Enable(codeOpen, model.CanJoinParty || model.CanJoinMatch);
            Enable(joinParty, model.CanJoinParty); Enable(joinMatch, model.CanJoinMatch);
            Enable(createTest, model.CanCreateTest);

            // Inviting needs a joinable party, which searching turns off.
            if (canInvite != model.CanInvite) { canInvite = model.CanInvite; if (FriendsOpen) DrawFriends(); }
            Enable(friendsOpen, model.Online);
            if (!model.CanInvite) Close(friendsPanel);
            if (!model.CanJoinParty && !model.CanJoinMatch) Close(codeModal);
        }

        void RenderMode(in HudModel model)
        {
            var mode = GameModes.Resolve(model.ModeKey);
            if (modeName != null) modeName.text = mode.Name;
            if (modeTagline != null) modeTagline.text = mode.Tagline;
            if (modeBadge != null) modeBadge.text = mode.Badge;
            if (modeTile != null) modeTile.style.backgroundColor = ModeColor(mode.Key);
            if (modeChange != null) modeChange.text = model.CanChangeMode ? "변경 >" : "";
            if (modeHint != null) modeHint.text = model.ModeHint;
            // Followers may look at the list; only the idle leader's choice counts.
            Enable(modeOpen, model.Online);
            foreach (var pair in modeButtons)
            {
                var info = GameModes.Find(pair.Key);
                bool on = pair.Key == mode.Key;
                pair.Value.EnableInClassList("mode-option-on", on);
                pair.Value.SetEnabled(model.CanChangeMode && info != null && info.Playable && !on);
                var state = pair.Value.Q<Label>("state");
                if (state != null)
                {
                    state.text = on ? "선택됨" : info != null && info.Playable ? "" : "준비 중";
                    state.EnableInClassList("mode-state-on", on);
                    Show(state, state.text.Length > 0);
                }
            }
            foreach (var pair in railButtons)
            {
                var info = GameModes.Find(pair.Key);
                bool on = pair.Key == mode.Key, playable = info != null && info.Playable;
                pair.Value.EnableInClassList("rail-on", on);
                if (railPlates.TryGetValue(pair.Key, out var plate)) plate.Set(on, false);
                if (on) pickedRail = pair.Key;
                // the picked card stays clickable too, for the double click that starts the game
                pair.Value.SetEnabled(model.CanChangeMode && playable);
                var state = pair.Value.Q<Label>("state");
                if (state != null)
                {
                    state.text = on ? "선택됨" : playable ? "플레이 가능" : "준비 중";
                    state.EnableInClassList("rail-badge-on", on);
                    state.EnableInClassList("rail-badge-ok", !on && playable);
                }
            }
            if (railHint != null)
                railHint.text = !model.Online ? "Steam 연결이 필요해요"
                              : model.Busy ? "매칭 중엔 바꿀 수 없어요"
                              : !model.CanChangeMode ? "파티장만 바꿀 수 있어요"
                              : "";   // 10-27 (승규 님): "모드마다 따로 매칭돼요" taken off
        }

        static void Fill(List<VisualElement> slots, int filled, int team)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].EnableInClassList("slot-blue", i < filled && team == 0);
                slots[i].EnableInClassList("slot-orange", i < filled && team == 1);
            }
        }

        void LogDiagnostics()
        {
            string backend = "";
#if ENABLE_LEGACY_INPUT_MANAGER
            backend += "LEGACY ";
#endif
#if ENABLE_INPUT_SYSTEM
            backend += "INPUTSYSTEM ";
#endif
            Debug.Log($"[ChessFight] HUD 진단 | 입력 백엔드: {(backend.Length == 0 ? "없음" : backend.Trim())} | " +
                      $"패널: {(root.panel == null ? "없음" : "있음")} | root: {root.worldBound.size} | " +
                      $"버튼 {actions.Count}개 | 실제 포인터 이벤트: {(pointerSeen ? "수신됨" : "아직 없음 → 대체 클릭 사용")}");
        }

        bool TryReadCode(out ulong id) => ulong.TryParse(code != null ? code.value.Replace(" ", "").Trim() : "", out id) && id != 0;

        Button Bind(VisualElement parent, string name, Action action)
        {
            var button = parent.Q<Button>(name);
            if (button == null) { Debug.LogError("[ChessFight] HUD 버튼 없음: " + name); return null; }
            button.focusable = false;   // Buttons must never steal keyboard focus from the shortcuts.
            button.clicked += action;
            actions[button] = action;
            return button;
        }

        // What Show last set, or the stylesheet's default before that. Reading the
        // inline value first means a panel opened this frame already counts as open.
        static bool Visible(VisualElement element)
        {
            if (element == null) return false;
            var inline = element.style.display;
            return inline.keyword == StyleKeyword.Undefined ? inline.value != DisplayStyle.None
                                                            : element.resolvedStyle.display != DisplayStyle.None;
        }
        static bool Usable(VisualElement element) => Visible(element) && element.enabledInHierarchy;
        static void Enable(VisualElement element, bool value) { if (element != null) element.SetEnabled(value); }
        static void Show(VisualElement element, bool value)
        { if (element != null) element.style.display = value ? DisplayStyle.Flex : DisplayStyle.None; }

        void OnDestroy()
        {
            if (ChatBox.Blocker == chatBlocker) ChatBox.Blocker = null;
            if (ownedPanel != null) Destroy(ownedPanel);
        }
    }
}
