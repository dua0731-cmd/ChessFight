using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // Everything the HUD needs to draw one frame. The view stays ignorant of
    // Steam so the layout can be edited and tested without a session.
    public struct HudModel
    {
        public string Status, Error, Details, Roster, Bots;
        public bool CanFindMatch, CanCreateTest, CanInvite, CanJoinParty, CanJoinMatch;
        public bool CanStart, CanCancel, CanLeaveParty;
        public bool CanAddBot, CanRemoveBot, CanFillRoom, CanClearRoomBots;
    }

    // Binds NetworkHud.uxml. The element names in the UXML and the Q<T>(name)
    // lookups here are a contract: renaming one without the other fails at run time.
    [DisallowMultipleComponent]
    public sealed class NetworkHudView : MonoBehaviour
    {
        public event Action FindMatch, CreateTest, Invite, StartGame, Cancel, LeaveParty;
        public event Action CopyParty, CopyMatch, AddBot, RemoveBot, FillRoom, ClearRoomBots;
        public event Action<ulong> JoinParty, JoinMatch;

        PanelSettings ownedPanel;
        Label status, error, details, roster, bots;
        TextField code;
        Toggle capture;
        Button find, createTest, invite, joinParty, joinMatch, start, cancel, leaveParty;
        Button addBot, removeBot, fillRoom, clearRoomBots;

        public bool IsTyping
        {
            get
            {
                var focused = code?.panel?.focusController.focusedElement as VisualElement;
                return focused != null && (focused == code || code.Contains(focused));
            }
        }
        // Movement is suppressed while a lobby ID is being typed or the window is
        // in the background, so stray keys never steer the pawn.
        // A missing toggle must not silently disable movement, so treat it as on.
        public bool MovementEnabled => (capture == null || capture.value) && !IsTyping && Application.isFocused;

        public void Build(VisualTreeAsset layout, ThemeStyleSheet theme, PanelSettings settings, Vector2Int referenceResolution)
        {
            if (layout == null) { Debug.LogError("[ChessFight] HUD layout missing; assign it on GameSceneConfig."); return; }
            var document = gameObject.AddComponent<UIDocument>();
            if (settings == null)
            {
                settings = ownedPanel = ScriptableObject.CreateInstance<PanelSettings>();
                settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                settings.referenceResolution = referenceResolution;
                if (theme != null) settings.themeStyleSheet = theme;
            }
            document.panelSettings = settings;

            var root = document.rootVisualElement;
            root.style.unityFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            layout.CloneTree(root);

            status = root.Q<Label>("status"); error = root.Q<Label>("error");
            details = root.Q<Label>("details"); roster = root.Q<Label>("roster");
            bots = root.Q<Label>("bots"); code = root.Q<TextField>("code");
            capture = root.Q<Toggle>("capture");
            if (capture != null) capture.value = true;

            root.focusable = true;
            root.RegisterCallback<PointerDownEvent>(evt =>
            {
                // Clicking anywhere but the ID field hands focus back so WASD resumes.
                if (code == null) return;
                var target = evt.target as VisualElement;
                if (target != code && (target == null || !code.Contains(target))) root.Focus();
            }, TrickleDown.TrickleDown);

            find = Bind(root, "find", () => FindMatch?.Invoke());
            createTest = Bind(root, "create-test", () => CreateTest?.Invoke());
            invite = Bind(root, "invite", () => Invite?.Invoke());
            start = Bind(root, "start", () => StartGame?.Invoke());
            cancel = Bind(root, "leave", () => Cancel?.Invoke());
            leaveParty = Bind(root, "new-party", () => LeaveParty?.Invoke());
            joinParty = Bind(root, "join-party", () => { if (TryReadCode(out ulong id)) JoinParty?.Invoke(id); });
            joinMatch = Bind(root, "join-test", () => { if (TryReadCode(out ulong id)) JoinMatch?.Invoke(id); });
            addBot = Bind(root, "bots-more", () => AddBot?.Invoke());
            removeBot = Bind(root, "bots-less", () => RemoveBot?.Invoke());
            fillRoom = Bind(root, "fill-bots", () => FillRoom?.Invoke());
            clearRoomBots = Bind(root, "clear-bots", () => ClearRoomBots?.Invoke());
            Bind(root, "copy-party", () => CopyParty?.Invoke());
            Bind(root, "copy-match", () => CopyMatch?.Invoke());
        }

        bool TryReadCode(out ulong id) => ulong.TryParse(code != null ? code.value : "", out id) && id != 0;

        static Button Bind(VisualElement root, string name, Action action)
        {
            var button = root.Q<Button>(name);
            if (button == null) { Debug.LogError("[ChessFight] HUD button missing: " + name); return null; }
            button.focusable = false;   // Buttons must never steal keyboard focus from the arena.
            button.clicked += action;
            return button;
        }

        public void Render(in HudModel model)
        {
            if (status == null) return;
            status.text = model.Status; error.text = model.Error;
            details.text = model.Details; roster.text = model.Roster;
            if (bots != null) bots.text = model.Bots;
            Enable(find, model.CanFindMatch); Enable(createTest, model.CanCreateTest);
            Enable(invite, model.CanInvite); Enable(joinParty, model.CanJoinParty);
            Enable(joinMatch, model.CanJoinMatch); Enable(start, model.CanStart);
            Enable(cancel, model.CanCancel); Enable(leaveParty, model.CanLeaveParty);
            Enable(addBot, model.CanAddBot); Enable(removeBot, model.CanRemoveBot);
            Enable(fillRoom, model.CanFillRoom); Enable(clearRoomBots, model.CanClearRoomBots);
        }
        static void Enable(Button button, bool value) { if (button != null) button.SetEnabled(value); }

        void OnDestroy() { if (ownedPanel != null) Destroy(ownedPanel); }
    }
}
