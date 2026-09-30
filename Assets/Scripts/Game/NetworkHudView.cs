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
        public string PlaySub, BusyTitle, BusySub;

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

        Label status, details, profile, version, toast, offlineText;
        Button friendsOpen, retry, play, cancel, start, codeOpen, createTest, leaveParty, copyParty;
        Button botsLess, botsMore, fillRoom, clearRoomBots, modeOpen, joinParty, joinMatch;
        Label partyCount, partyCode, bots, botsNote, playSub, busyTitle, busySub;
        Label modeName, modeTagline, modeBadge, modeChange, modeHint;
        VisualElement offline, busy, spinner, modeTile, roomTools;
        VisualElement matchPanel, slotsOurs, slotsTheirs;
        Label matchKicker, matchTitle, matchTimer, matchNote, roomCode;
        VisualElement testBots;
        Label allyDummies, enemyDummies, testHint, partyBotsTitle;
        Button allyLess, allyMore, enemyLess, enemyMore;
        readonly List<VisualElement> ourSlots = new List<VisualElement>(), theirSlots = new List<VisualElement>();
        TextField code;

        VisualElement friendsPanel, detailsPanel, codeModal, modeModal;
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
        VisualElement railList;
        Label railHint;
        readonly Dictionary<string, Button> railButtons = new Dictionary<string, Button>();
        readonly List<Texture2D> ownedTextures = new List<Texture2D>();

        // Nameplates over the 3D lineup.
        VisualElement lineupLayer;
        Camera lineupCamera;
        readonly List<LineupEntry> lineup = new List<LineupEntry>();
        readonly List<VisualElement> plates = new List<VisualElement>();
        readonly List<Button> inviteButtons = new List<Button>();

        public bool IsTyping
        {
            get
            {
                var focused = code?.panel?.focusController.focusedElement as VisualElement;
                return focused != null && (focused == code || code.Contains(focused));
            }
        }
        public bool FriendsOpen => Visible(friendsPanel);
        // The chat box above the party bar; the lobby controller connects it.
        public ChatBox Chat { get; private set; }

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
            playSub = root.Q<Label>("play-sub"); busy = root.Q<VisualElement>("busy");
            busyTitle = root.Q<Label>("busy-title"); busySub = root.Q<Label>("busy-sub");
            spinner = root.Q<VisualElement>("spinner");
            modeName = root.Q<Label>("mode-name"); modeTagline = root.Q<Label>("mode-tagline");
            modeBadge = root.Q<Label>("mode-badge"); modeTile = root.Q<VisualElement>("mode-tile");
            modeChange = root.Q<Label>("mode-change"); modeHint = root.Q<Label>("mode-hint");
            matchPanel = root.Q<VisualElement>("match-panel");
            matchKicker = root.Q<Label>("match-kicker"); matchTitle = root.Q<Label>("match-title");
            matchTimer = root.Q<Label>("match-timer"); matchNote = root.Q<Label>("match-note");
            roomTools = root.Q<VisualElement>("room-tools"); roomCode = root.Q<Label>("room-code");
            testBots = root.Q<VisualElement>("test-bots");
            allyDummies = root.Q<Label>("ally-dummies"); enemyDummies = root.Q<Label>("enemy-dummies");
            testHint = root.Q<Label>("test-hint"); partyBotsTitle = root.Q<Label>("party-bots-title");
            slotsOurs = root.Q<VisualElement>("slots-ours"); slotsTheirs = root.Q<VisualElement>("slots-theirs");
            code = root.Q<TextField>("code");
            friendsPanel = root.Q<VisualElement>("friends"); detailsPanel = root.Q<VisualElement>("details-card");
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
                // Clicking anywhere but the number field or the chat box hands focus
                // back, so the keyboard shortcuts work again.
                if (code == null) return;
                var target = evt.target as VisualElement;
                if (Chat != null && Chat.Contains(target)) return;
                if (target != code && (target == null || !code.Contains(target))) root.Focus();
            }, TrickleDown.TrickleDown);

            play = Bind(root, "play", () => Play?.Invoke());
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

            friendsOpen = Bind(root, "friends-open", () => { if (FriendsOpen) Close(friendsPanel); else OpenFriends(); });
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
            Chat = new ChatBox(root.Q<VisualElement>("chat-slot"), ChatBox.Layout.Lobby);
            // Start from a known state instead of trusting the stylesheet defaults,
            // so "is this panel open" is answerable before the first layout pass.
            foreach (var hidden in new[] { friendsPanel, detailsPanel, codeModal, modeModal, toast, offline, busy, matchPanel, roomTools, testBots })
                Show(hidden, false);
            diagnoseAt = Time.unscaledTime + 1f;
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
            // The chat's own keys first: Enter while typing sends a line, it does
            // not start a game.
            Chat?.Tick();
            if (Chat == null || !Chat.HoldsKeys) Shortcuts();
            FallbackClick();
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
            if (LegacyKeys.Down(KeyCode.Escape))
            {
                if (Visible(modeModal)) Close(modeModal);
                else if (Visible(codeModal)) Close(codeModal);
                else if (Visible(friendsPanel)) Close(friendsPanel);
                else if (Visible(detailsPanel)) Close(detailsPanel);
                return;
            }
            bool modal = Visible(modeModal) || Visible(codeModal);
            if (!modal && (LegacyKeys.Down(KeyCode.Return) || LegacyKeys.Down(KeyCode.KeypadEnter)))
            {
                if (Usable(start)) StartGame?.Invoke();
                else if (Usable(play)) Play?.Invoke();
            }
            if (!modal && LegacyKeys.Down(KeyCode.F)) { if (FriendsOpen) Close(friendsPanel); else if (canInvite) OpenFriends(); }
            if (!modal && LegacyKeys.Down(KeyCode.M) && Usable(modeOpen)) Open(modeModal);
        }

        // If the panel never receives a real pointer event, drive the buttons from
        // the mouse directly. This is a safety net, not the intended path: it
        // switches itself off the moment a genuine event arrives, and it cannot
        // help with typing, which is what the paste button is for.
        void FallbackClick()
        {
            if (pointerSeen || fallbackDead) return;
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
            if (!pressed) return;

            var panel = root.panel;
            if (panel == null) return;
            // Unity's own samples flip Y before converting; try the plain point too
            // rather than betting the whole fallback on the convention.
            var hit = Resolve(panel, new Vector2(screen.x, Screen.height - screen.y)) ?? Resolve(panel, screen);
            if (hit == null || !hit.Value.button.enabledInHierarchy) return;
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
            Open(friendsPanel);
            FriendsOpened?.Invoke();
        }

        public void CloseFriends() => Close(friendsPanel);

        void Open(VisualElement panel)
        {
            if (panel == detailsPanel) Close(friendsPanel);
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
            lineupLayer.Clear();
            for (int i = 0; i < lineup.Count; i++)
            {
                var entry = lineup[i];
                var plate = entry.Invite ? InviteSpot() : Nameplate(entry);
                plate.style.display = DisplayStyle.None;   // until placed
                lineupLayer.Add(plate);
                plates.Add(plate);
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

        static VisualElement Nameplate(LineupEntry entry)
        {
            var plate = new VisualElement { pickingMode = PickingMode.Ignore };
            plate.AddToClassList("plate");
            if (entry.Me) plate.AddToClassList("plate-me");
            var name = new Label((entry.Leader ? "★ " : "") + entry.Name) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("plate-name");
            plate.Add(name);
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
            var plus = new Button { text = "+", focusable = false };
            plus.AddToClassList("invite-plus");
            Action open = OpenFriends;
            plus.clicked += open;
            actions[plus] = open;
            inviteButtons.Add(plus);
            spot.Add(plus);
            var label = new Label("친구 초대") { pickingMode = PickingMode.Ignore };
            label.AddToClassList("invite-label");
            spot.Add(label);
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
                plates[i].style.left = at.x - (invite ? 60f : 90f);
                plates[i].style.top = at.y - (invite ? 20f : 46f);
            }
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
                ModeLook(mode.Key, out Color from, out Color to, out PieceKind piece, out PieceSkin skin, out float yaw);
                card.style.backgroundImage = new StyleBackground(Diagonal(from, to));

                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList("rail-art");
                var portrait = PiecePortraits.Get(piece, skin, yaw, Color.Lerp(from, to, .6f));
                if (portrait != null) art.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(portrait));
                card.Add(art);

                var text = new VisualElement { pickingMode = PickingMode.Ignore };
                text.AddToClassList("rail-text");
                var badges = new VisualElement { pickingMode = PickingMode.Ignore };
                badges.AddToClassList("rail-badges");
                var state = new Label { name = "state", pickingMode = PickingMode.Ignore };
                state.AddToClassList("rail-badge");
                badges.Add(state);
                text.Add(badges);
                var name = new Label(mode.Name) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("rail-name");
                var summary = new Label(WrapWords(mode.Summary, RailLineUnits)) { pickingMode = PickingMode.Ignore };
                summary.AddToClassList("rail-summary");
                var tagline = new Label(mode.Tagline) { pickingMode = PickingMode.Ignore };
                tagline.AddToClassList("rail-tagline");
                text.Add(name); text.Add(summary); text.Add(tagline);
                card.Add(text);

                string key = mode.Key;
                Action pick = () => PickMode?.Invoke(key);
                card.clicked += pick;
                actions[card] = pick;
                railButtons[key] = card;
                railList.Add(card);
            }
        }

        // UI Toolkit breaks Korean between any two syllables, even mid-word, so
        // break the card text at spaces here instead. Widths are estimated: a
        // Hangul syllable counts two units, anything else one.
        const int RailLineUnits = 38;   // .rail-text is 232px of 11px text

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

        // Each mode's card: a diagonal colour wash and the piece that stars in it.
        static void ModeLook(string key, out Color from, out Color to, out PieceKind piece, out PieceSkin skin, out float yaw)
        {
            switch (key)
            {
                case "queenhill":
                    from = PieceFigure.Hex(0x3B5BC4); to = PieceFigure.Hex(0x1B2C66); piece = PieceKind.Queen; skin = PieceSkin.White; yaw = -9f;
                    return;
                case "swordfight":
                    from = PieceFigure.Hex(0x8A3A3A); to = PieceFigure.Hex(0x3A1620); piece = PieceKind.Knight; skin = PieceSkin.Black; yaw = 52f;
                    return;
                case "kingrush":
                    from = PieceFigure.Hex(0x2F7D6E); to = PieceFigure.Hex(0x123A36); piece = PieceKind.King; skin = PieceSkin.White; yaw = -9f;
                    return;
                default:
                    from = PieceFigure.Hex(0x3A4660); to = PieceFigure.Hex(0x1A2236); piece = PieceKind.Pawn; skin = PieceSkin.White; yaw = 0f;
                    return;
            }
        }

        // USS has no gradients: a small texture, top left `from` to bottom right `to`.
        Texture2D Diagonal(Color from, Color to)
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Mode Card" };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    texture.SetPixel(x, y, Color.Lerp(from, to, (x + (size - 1 - y)) / (2f * (size - 1))));
            texture.Apply();
            ownedTextures.Add(texture);
            return texture;
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
            if (friendsOpen != null) friendsOpen.text = model.Online ? $"친구  {model.FriendsOnline}" : "친구";

            Show(offline, !model.Online);
            if (offlineText != null && !string.IsNullOrEmpty(model.Status) && !model.Online) offlineText.text = model.Status;
            Enable(retry, model.CanRetry);

            RenderMode(model);

            if (partyCount != null) partyCount.text = $"{model.PartySize} / 6";
            if (partyCode != null) partyCode.text = model.PartyCode;
            if (bots != null) bots.text = model.Bots;
            if (botsNote != null) { botsNote.text = model.BotsNote; Show(botsNote, !string.IsNullOrEmpty(model.BotsNote)); }
            Enable(copyParty, model.CanCopyParty);
            Enable(botsMore, model.CanAddBot); Enable(botsLess, model.CanRemoveBot);
            Enable(leaveParty, model.CanLeaveParty);

            Show(play, !model.Busy);
            Enable(play, model.CanPlay);
            if (playSub != null) playSub.text = model.PlaySub;
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
            if (createTest != null) createTest.text = sword ? "더미 테스트 (혼자 가능)" : "비공개 방";
            if (partyBotsTitle != null) partyBotsTitle.text = sword ? "파티 더미 (아군)" : "AI 봇";
            if (clearRoomBots != null) clearRoomBots.text = sword ? "추가 더미 비우기" : "봇 비우기";
            if (start != null) start.text = model.ShowTestBots ? "테스트 시작  Enter" : "경기 시작";

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
                pair.Value.SetEnabled(model.CanChangeMode && playable && !on);
                var state = pair.Value.Q<Label>("state");
                if (state != null)
                {
                    state.text = on ? "선택됨" : playable ? "플레이 가능" : "준비 중";
                    state.EnableInClassList("rail-badge-on", on);
                }
            }
            if (railHint != null)
                railHint.text = !model.Online ? "Steam 연결이 필요해요"
                              : model.Busy ? "매칭 중엔 바꿀 수 없어요"
                              : !model.CanChangeMode ? "파티장만 바꿀 수 있어요"
                              : "모드마다 따로 매칭돼요";
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
            if (ownedPanel != null) Destroy(ownedPanel);
            foreach (var texture in ownedTextures) if (texture != null) Destroy(texture);
            ownedTextures.Clear();
        }
    }
}
