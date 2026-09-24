using System;
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
    }

    // Binds NetworkHud.uxml. The element names in the UXML and the Q<T>(name)
    // lookups here are a contract: renaming one without the other fails at run time.
    [DisallowMultipleComponent]
    public sealed class NetworkHudView : MonoBehaviour
    {
        public event Action Play, MakeParty, QuickMatch, Invite, PartyStart, StartGame, Cancel, LeaveParty, Back;
        public event Action CreateTest, CopyParty, CopyMatch, AddBot, RemoveBot, FillRoom, ClearRoomBots, RetrySteam;
        public event Action<ulong> JoinParty, JoinMatch;

        PanelSettings ownedPanel;
        bool pointerSeen;
        Label status, error, details, roster, rosterTitle, bots, partyId, matchId, hint;
        TextField code;
        Toggle capture;
        VisualElement stepHome, stepMode, stepParty, stepBusy;
        Button quickMatch, invite, partyStart, start, cancel, leaveParty, createTest, joinParty, joinMatch, retry;
        Button botsLess, botsMore, fillRoom, clearRoomBots;

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
            if (layout == null) { Debug.LogError("[ChessFight] HUD 레이아웃을 찾지 못했습니다. GameSceneConfig에 지정하세요."); return; }
            var document = gameObject.AddComponent<UIDocument>();
            if (settings == null)
            {
                settings = ownedPanel = ScriptableObject.CreateInstance<PanelSettings>();
                settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                settings.referenceResolution = referenceResolution;
                // A PanelSettings without a theme renders nothing at all, so say so
                // loudly rather than leaving an invisible HUD to explain.
                if (theme != null) settings.themeStyleSheet = theme;
                else Debug.LogError("[ChessFight] Resources/NetworkTheme (.tss)를 불러오지 못했습니다. " +
                                    "테마가 없으면 HUD가 아예 보이지 않습니다.");
            }
            document.panelSettings = settings;

            var root = document.rootVisualElement;
            root.style.unityFont = ResolveFont();
            layout.CloneTree(root);

            status = root.Q<Label>("status"); error = root.Q<Label>("error");
            details = root.Q<Label>("details"); roster = root.Q<Label>("roster");
            rosterTitle = root.Q<Label>("roster-title"); bots = root.Q<Label>("bots");
            partyId = root.Q<Label>("party-id"); matchId = root.Q<Label>("match-id");
            hint = root.Q<Label>("step-hint"); code = root.Q<TextField>("code");
            capture = root.Q<Toggle>("capture");
            if (capture != null) capture.value = true;

            stepHome = root.Q<VisualElement>("step-home"); stepMode = root.Q<VisualElement>("step-mode");
            stepParty = root.Q<VisualElement>("step-party"); stepBusy = root.Q<VisualElement>("step-busy");

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
        }

        // The built-in LegacyRuntime font carries no Hangul, so take a Korean
        // capable OS font first and keep the built-in one as the fallback.
        static Font ResolveFont()
        {
            string[] candidates = { "Malgun Gothic", "맑은 고딕", "NanumGothic", "Noto Sans KR",
                                    "Gulim", "Dotum", "Arial Unicode MS" };
            try
            {
                var os = Font.CreateDynamicFontFromOSFont(candidates, 16);
                if (os != null) { Debug.Log("[ChessFight] HUD 폰트: " + os.name); return os; }
            }
            catch (Exception e) { Debug.LogWarning("[ChessFight] OS 폰트를 불러오지 못했습니다: " + e.Message); }
            Debug.LogWarning("[ChessFight] 한글 폰트를 찾지 못해 기본 폰트를 씁니다. 한글이 깨질 수 있습니다.");
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        bool TryReadCode(out ulong id) => ulong.TryParse(code != null ? code.value : "", out id) && id != 0;

        static Button Bind(VisualElement root, string name, Action action)
        {
            var button = root.Q<Button>(name);
            if (button == null) { Debug.LogError("[ChessFight] HUD 버튼 없음: " + name); return null; }
            button.focusable = false;   // Buttons must never steal keyboard focus from the arena.
            button.clicked += action;
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
