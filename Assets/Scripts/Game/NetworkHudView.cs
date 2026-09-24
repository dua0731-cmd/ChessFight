using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // Which button group the action stack is showing. Pressing the play button is
    // navigation, not a session call: nothing reaches Steam until the player picks
    // quick match, or starts the match from the party screen.
    public enum HudStep { Home, Mode, Party, Busy }

    // Everything the HUD needs to draw one frame. The view stays ignorant of Steam
    // so the layout can be edited and tested without a session.
    public struct HudModel
    {
        public HudStep Step;
        public string Status, Error, Details, Roster, RosterTitle, Bots, PartyId, MatchId, Hint;
        public bool CanQuickMatch, CanInvite, CanPartyStart, CanStart, CanCancel, CanLeaveParty;
        public bool CanCreateTest, CanJoinParty, CanJoinMatch, CanRetry;
        public bool CanAddBot, CanRemoveBot, CanFillRoom, CanClearRoomBots;
        public bool FriendsOpen;
    }

    // Binds NetworkHud.uxml. The element names in the UXML and the Q<T>(name)
    // lookups here are a contract: renaming one without the other fails at run time.
    [DisallowMultipleComponent]
    public sealed class NetworkHudView : MonoBehaviour
    {
        public event Action Play, MakeParty, QuickMatch, Invite, PartyStart, StartGame, Cancel, LeaveParty, Back;
        public event Action CreateTest, CopyParty, CopyMatch, AddBot, RemoveBot, FillRoom, ClearRoomBots, RetrySteam;
        public event Action<ulong> JoinParty, JoinMatch, InviteFriend;
        public event Action CloseFriends, RefreshFriends, SteamOverlayInvite;

        PanelSettings ownedPanel;
        VisualElement root;
        // Every button and what it does, so the fallback router below can fire one
        // without going through the event system.
        readonly Dictionary<Button, Action> actions = new Dictionary<Button, Action>();
        bool pointerSeen, fallbackDead, diagnosed;
        float diagnoseAt;
        Label status, error, details, roster, rosterTitle, bots, partyId, matchId, hint;
        TextField code;
        Toggle capture;
        VisualElement stepHome, stepMode, stepParty, stepBusy;
        Button quickMatch, invite, partyStart, start, cancel, leaveParty, createTest, joinParty, joinMatch, retry;
        Button botsLess, botsMore, fillRoom, clearRoomBots;
        VisualElement friendsPanel, friendsList;
        Label friendsInfo, friendsPage;
        Button friendsFilter;
        readonly List<FriendInfo> friends = new List<FriendInfo>();
        readonly List<Button> friendButtons = new List<Button>();
        const int FriendsPerPage = 8;
        int friendPage;
        bool onlineOnly = true;

        public bool IsTyping
        {
            get
            {
                var focused = code?.panel?.focusController.focusedElement as VisualElement;
                return focused != null && (focused == code || code.Contains(focused));
            }
        }
        // Movement is suppressed while a lobby number is being typed or the window
        // is in the background, so stray keys never steer the pawn.
        public bool MovementEnabled => (capture == null || capture.value) && !IsTyping && Application.isFocused;

        public void Build(VisualTreeAsset layout, ThemeStyleSheet theme, PanelSettings settings, Vector2Int referenceResolution)
        {
            root = RuntimePanels.Create(gameObject, layout, theme, settings, referenceResolution, out ownedPanel);
            if (root == null) return;

            status = root.Q<Label>("status"); error = root.Q<Label>("error");
            details = root.Q<Label>("details"); roster = root.Q<Label>("roster");
            rosterTitle = root.Q<Label>("roster-title"); bots = root.Q<Label>("bots");
            partyId = root.Q<Label>("party-id"); matchId = root.Q<Label>("match-id");
            hint = root.Q<Label>("step-hint"); code = root.Q<TextField>("code");
            capture = root.Q<Toggle>("capture");
            if (capture != null) capture.value = true;

            stepHome = root.Q<VisualElement>("step-home"); stepMode = root.Q<VisualElement>("step-mode");
            stepParty = root.Q<VisualElement>("step-party"); stepBusy = root.Q<VisualElement>("step-busy");
            friendsPanel = root.Q<VisualElement>("friends"); friendsList = root.Q<VisualElement>("friends-list");
            friendsInfo = root.Q<Label>("friends-info"); friendsPage = root.Q<Label>("friends-page");

            root.focusable = true;
            root.RegisterCallback<PointerDownEvent>(evt =>
            {
                // One-shot proof that the panel receives pointer events at all. If a
                // button never responds and this never logs, the runtime input
                // backend is the problem, not the layout or the enabled states.
                if (!pointerSeen) { pointerSeen = true; Debug.Log("[ChessFight] HUD 포인터 입력 확인됨."); }
                // Clicking anywhere but the number field hands focus back so WASD resumes.
                if (code == null) return;
                var target = evt.target as VisualElement;
                if (target != code && (target == null || !code.Contains(target))) root.Focus();
            }, TrickleDown.TrickleDown);

            Bind(root, "play", () => Play?.Invoke());
            Bind(root, "make-party", () => MakeParty?.Invoke());
            quickMatch = Bind(root, "quick-match", () => QuickMatch?.Invoke());
            invite = Bind(root, "invite", () => Invite?.Invoke());
            partyStart = Bind(root, "party-start", () => PartyStart?.Invoke());
            start = Bind(root, "start", () => StartGame?.Invoke());
            cancel = Bind(root, "leave", () => Cancel?.Invoke());
            leaveParty = Bind(root, "new-party", () => LeaveParty?.Invoke());
            Bind(root, "back", () => Back?.Invoke());
            Bind(root, "back-party", () => Back?.Invoke());
            createTest = Bind(root, "create-test", () => CreateTest?.Invoke());
            joinParty = Bind(root, "join-party", () => { if (TryReadCode(out ulong id)) JoinParty?.Invoke(id); });
            joinMatch = Bind(root, "join-test", () => { if (TryReadCode(out ulong id)) JoinMatch?.Invoke(id); });
            retry = Bind(root, "retry", () => RetrySteam?.Invoke());
            botsMore = Bind(root, "bots-more", () => AddBot?.Invoke());
            botsLess = Bind(root, "bots-less", () => RemoveBot?.Invoke());
            fillRoom = Bind(root, "fill-bots", () => FillRoom?.Invoke());
            clearRoomBots = Bind(root, "clear-bots", () => ClearRoomBots?.Invoke());
            Bind(root, "copy-party", () => CopyParty?.Invoke());
            Bind(root, "copy-match", () => CopyMatch?.Invoke());
            // Pasting sidesteps the number field entirely, which matters because a
            // panel that gets no pointer events gets no keystrokes either.
            Bind(root, "paste", () => { if (code != null) code.value = GUIUtility.systemCopyBuffer; });
            Bind(root, "friends-close", () => CloseFriends?.Invoke());
            Bind(root, "friends-refresh", () => RefreshFriends?.Invoke());
            Bind(root, "friends-overlay", () => SteamOverlayInvite?.Invoke());
            Bind(root, "friends-prev", () => { friendPage--; DrawFriends(); });
            Bind(root, "friends-next", () => { friendPage++; DrawFriends(); });
            friendsFilter = Bind(root, "friends-filter", () => { onlineOnly = !onlineOnly; friendPage = 0; DrawFriends(); });
            diagnoseAt = Time.unscaledTime + 1f;
        }

        // If the panel never receives a real pointer event, drive the buttons from
        // the mouse directly. This is a safety net, not the intended path: it
        // switches itself off the moment a genuine event arrives, and it cannot
        // help with typing, which is what the paste button is for.
        void Update()
        {
            if (!diagnosed && root != null && Time.unscaledTime >= diagnoseAt) { diagnosed = true; LogDiagnostics(); }
            if (pointerSeen || fallbackDead || root == null) return;
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
            if (hit == null) return;
            if (hit.Value.button != null)
            {
                if (!hit.Value.button.enabledInHierarchy) return;
                hit.Value.action();
            }
            else hit.Value.toggle.value = !hit.Value.toggle.value;
        }

        struct Hit { public Button button; public Action action; public Toggle toggle; }

        Hit? Resolve(IPanel panel, Vector2 point)
        {
            for (var element = panel.Pick(point); element != null; element = element.parent)
            {
                if (element is Button button && actions.TryGetValue(button, out var action))
                    return new Hit { button = button, action = action };
                if (element is Toggle toggle) return new Hit { toggle = toggle };
            }
            return null;
        }

        // The bootstrap pushes a fresh snapshot while the panel is open.
        public void SetFriends(List<FriendInfo> snapshot)
        {
            friends.Clear();
            if (snapshot != null) friends.AddRange(snapshot);
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
            foreach (var friend in friends)
                if (!onlineOnly || friend.Presence != FriendPresence.Offline) shown.Add(friend);

            int pages = Mathf.Max(1, (shown.Count + FriendsPerPage - 1) / FriendsPerPage);
            friendPage = Mathf.Clamp(friendPage, 0, pages - 1);
            if (friendsFilter != null) friendsFilter.text = onlineOnly ? "온라인만" : "전체 보기";
            if (friendsPage != null) friendsPage.text = $"{friendPage + 1} / {pages}";
            if (friendsInfo != null)
                friendsInfo.text = friends.Count == 0
                    ? "친구 목록을 불러오는 중입니다."
                    : $"전체 {friends.Count}명 중 {shown.Count}명 표시";

            if (shown.Count == 0)
            {
                var empty = new Label(onlineOnly ? "접속 중인 친구가 없습니다." : "친구가 없습니다.");
                empty.AddToClassList("friend-empty");
                friendsList.Add(empty);
                return;
            }
            for (int i = friendPage * FriendsPerPage; i < shown.Count && i < (friendPage + 1) * FriendsPerPage; i++)
                friendsList.Add(FriendRow(shown[i]));
        }

        VisualElement FriendRow(FriendInfo friend)
        {
            var row = new VisualElement();
            row.AddToClassList("friend-row");

            var dot = new VisualElement();
            dot.AddToClassList("dot");
            dot.AddToClassList(friend.Presence == FriendPresence.InGame ? "dot-ingame"
                             : friend.Presence == FriendPresence.Online ? "dot-online"
                             : friend.Presence == FriendPresence.Away ? "dot-away" : "dot-offline");
            row.Add(dot);

            var name = new Label(friend.Name);
            name.AddToClassList("friend-name");
            row.Add(name);

            var state = new Label(friend.Presence == FriendPresence.InGame ? "게임 중"
                                : friend.Presence == FriendPresence.Online ? "온라인"
                                : friend.Presence == FriendPresence.Away ? "자리 비움" : "오프라인");
            state.AddToClassList("friend-state");
            row.Add(state);

            ulong id = friend.Id;
            var button = new Button { text = "초대" };
            button.AddToClassList("btn"); button.AddToClassList("friend-invite");
            button.focusable = false;
            Action invite = () => InviteFriend?.Invoke(id);
            button.clicked += invite;
            actions[button] = invite;
            friendButtons.Add(button);
            row.Add(button);
            return row;
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
            var screen = root.Q<VisualElement>(className: "screen");
            Debug.Log($"[ChessFight] HUD 진단 | 입력 백엔드: {(backend.Length == 0 ? "없음" : backend.Trim())} | " +
                      $"패널: {(root.panel == null ? "없음" : "있음")} | root: {root.worldBound.size} | " +
                      $"screen: {(screen == null ? Vector2.zero : screen.worldBound.size)} | " +
                      $"버튼 {actions.Count}개 | 실제 포인터 이벤트: {(pointerSeen ? "수신됨" : "아직 없음 → 대체 클릭 사용")}");
        }

        bool TryReadCode(out ulong id) => ulong.TryParse(code != null ? code.value : "", out id) && id != 0;

        Button Bind(VisualElement parent, string name, Action action)
        {
            var button = parent.Q<Button>(name);
            if (button == null) { Debug.LogError("[ChessFight] HUD 버튼 없음: " + name); return null; }
            button.focusable = false;   // Buttons must never steal keyboard focus from the arena.
            button.clicked += action;
            actions[button] = action;
            return button;
        }

        public void Render(in HudModel model)
        {
            if (status == null) return;
            status.text = model.Status; error.text = model.Error;
            details.text = model.Details; roster.text = model.Roster;
            if (rosterTitle != null) rosterTitle.text = model.RosterTitle;
            if (bots != null) bots.text = model.Bots;
            if (partyId != null) partyId.text = model.PartyId;
            if (matchId != null) matchId.text = model.MatchId;
            if (hint != null) hint.text = model.Hint;

            Show(friendsPanel, model.FriendsOpen);
            Show(stepHome, model.Step == HudStep.Home);
            Show(stepMode, model.Step == HudStep.Mode);
            Show(stepParty, model.Step == HudStep.Party);
            Show(stepBusy, model.Step == HudStep.Busy);
            // The retry button is only an escape hatch from a failed Steam start.
            Show(retry, model.CanRetry);

            Enable(quickMatch, model.CanQuickMatch); Enable(invite, model.CanInvite);
            Enable(partyStart, model.CanPartyStart); Enable(start, model.CanStart);
            Enable(cancel, model.CanCancel); Enable(leaveParty, model.CanLeaveParty);
            Enable(createTest, model.CanCreateTest); Enable(joinParty, model.CanJoinParty);
            Enable(joinMatch, model.CanJoinMatch); Enable(retry, model.CanRetry);
            Enable(botsMore, model.CanAddBot); Enable(botsLess, model.CanRemoveBot);
            Enable(fillRoom, model.CanFillRoom); Enable(clearRoomBots, model.CanClearRoomBots);
        }

        static void Enable(VisualElement element, bool value) { if (element != null) element.SetEnabled(value); }
        static void Show(VisualElement element, bool value)
        { if (element != null) element.style.display = value ? DisplayStyle.Flex : DisplayStyle.None; }

        void OnDestroy() { if (ownedPanel != null) Destroy(ownedPanel); }
    }
}
