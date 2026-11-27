using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // R862 (the user 2026-11-27: "로비 친구 탭에서 친구를 눌러 파티 없이 1:1 대화"; Docs/Architecture/UI.md "친구 1:1 대화"):
    // the 1:1 chat with one Steam friend over the lobby. A friend's "대화" button in the friends card opens it; it
    // stands left of that card while the card is open and at the right edge once the card closes. It shows one
    // conversation (Core FriendChatBook, kept for the run by the runtime), the friend's lines on the left, mine on
    // the right; Enter sends, Esc or ✕ closes it. The conversation on screen is read as lines come.
    //
    // Steam-agnostic like the rest of this assembly: the lobby (LobbyBootstrap) connects the book, the names and the
    // call that sends (Network/SteamFriendChat, a Steam friend message). While its line has the keyboard it holds the
    // keys (KeysHeld, folded into ChatBox.KeysHeld), so the lobby's Enter / Esc / F wait.
    [DisallowMultipleComponent]
    public sealed class FriendChatWindow : MonoBehaviour
    {
        // Over the lobby HUD (0) and the name screen (20), under the chat box (50) and the loading screen (100).
        const float SortingOrder = 40f;
        const int PanelLines = 11;
        const float NoteSeconds = 3f;
        // As in the chat box: an Enter pressed while the IME still holds a syllable waits for it, this long at most.
        const int ImeWaitFrames = 30;

        static FriendChatWindow current;
        public static bool KeysHeld => current != null && current.HoldsKeys;

        // The lobby's say: whether it may show now (the play tab, not matching), and whether the friends card is open.
        public bool Allowed { get; set; } = true;
        public bool BesideFriends { get; set; }
        // The friend this window talks to (0 while shut).
        public ulong Friend => open ? friend : 0;
        public bool IsOpen => open;

        PanelSettings ownedPanel;
        VisualElement window, lines, avatar;
        Label letter, nameLabel, stateLabel, placeholder, hint, scrolled;
        TextField field;

        FriendChatBook book;
        Func<ulong, string> realName, stateOf;
        Func<ulong, string, string> send;
        ulong friend;
        bool open, noteError;
        // Frames left to hand the line the keyboard once the window shows (a hidden field cannot take it).
        int focusFrames;
        int drawnVersion = -1, drawnStreamer = -1, settingsVersion = -1, scroll, drawnScroll = -1, submitFrame = -1, releasedFrame = -10;
        ulong drawnFriend;
        float noteUntil;
        string noteText = "";

        bool HoldsKeys => (open && (Typing || submitFrame >= 0)) || Time.frameCount - releasedFrame <= 1;

        public void Build()
        {
            current = this;
            var root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("FriendChatHud"),
                                            Resources.Load<ThemeStyleSheet>("NetworkTheme"), null,
                                            new Vector2Int(1280, 720), out ownedPanel);
            if (root == null) return;
            if (ownedPanel != null) ownedPanel.sortingOrder = SortingOrder;
            var sheet = Resources.Load<StyleSheet>("FriendChat");
            if (sheet != null) root.styleSheets.Add(sheet);
            else Debug.LogError("[ChessFight] Resources/FriendChat.uss를 불러오지 못했습니다.");
            // Over the whole screen, but only the window takes clicks: the lobby under it keeps the rest.
            root.pickingMode = PickingMode.Ignore;
            window = root.Q<VisualElement>("fc-window");
            lines = root.Q<VisualElement>("fc-lines");
            avatar = root.Q<VisualElement>("fc-avatar");
            letter = root.Q<Label>("fc-letter");
            nameLabel = root.Q<Label>("fc-name");
            stateLabel = root.Q<Label>("fc-state");
            placeholder = root.Q<Label>("fc-placeholder");
            hint = root.Q<Label>("fc-hint");
            scrolled = root.Q<Label>("fc-scrolled");
            field = root.Q<TextField>("fc-field");
            if (window == null || lines == null || field == null)
            { Debug.LogError("[ChessFight] FriendChatHud.uxml의 이름이 FriendChatWindow와 맞지 않습니다."); window = null; return; }
            ImeGuard.Watch(field);   // the IME on only while typing here (Korean), as in the chat box
            if (nameLabel != null) { nameLabel.enableRichText = false; nameLabel.parseEscapeSequences = false; }

            var close = root.Q<Button>("fc-close");
            if (close != null) { close.focusable = false; close.clicked += () => { MenuSound.Play(MenuSound.Kind.Close); Close(); }; }
            var sendButton = root.Q<Button>("fc-send");
            if (sendButton != null) { sendButton.focusable = false; sendButton.clicked += () => { if (open) submitFrame = Time.frameCount; }; }

            field.maxLength = ChatText.MaxLength;
            field.selectAllOnFocus = false;
            field.selectAllOnMouseUp = false;
            // Enter and Esc belong to the window (left to the field, Enter gives up the focus).
            field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Escape) { Swallow(e); Close(); return; }
                bool enter = e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.character == '\n' || e.character == '\r';
                if (enter || e.keyCode == KeyCode.Tab || e.character == '\t') Swallow(e);
            }, TrickleDown.TrickleDown);
            field.RegisterCallback<NavigationMoveEvent>(Swallow, TrickleDown.TrickleDown);
            field.RegisterCallback<NavigationSubmitEvent>(Swallow, TrickleDown.TrickleDown);
            field.RegisterCallback<NavigationCancelEvent>(e => { Swallow(e); Close(); }, TrickleDown.TrickleDown);
            lines.RegisterCallback<WheelEvent>(e => { Scroll(e.delta.y < 0 ? 2 : -2); e.StopPropagation(); });
            Show(window, false);
        }

        // realName: a friend's Steam name (the window shows "플레이어 N" itself under streamer mode, R792);
        // stateOf: the line under it ("이 게임 중", "오프라인"); send: "" once a line is on its way, else why not.
        public void Connect(FriendChatBook chatBook, Func<ulong, string> names, Func<ulong, string> states, Func<ulong, string, string> sender)
        {
            book = chatBook; realName = names; stateOf = states; send = sender;
            drawnVersion = -1;
        }

        public void Open(ulong id)
        {
            if (window == null || id == 0) return;
            if (open && friend == id) { field.Focus(); return; }
            if (friend != id) { field.value = ""; scroll = 0; }
            friend = id; open = true; noteError = false;
            drawnVersion = -1;
            focusFrames = 3;
        }

        public void Close()
        {
            submitFrame = -1;
            if (open) releasedFrame = Time.frameCount;
            open = false;
            if (book != null) book.Viewing = 0;
            if (Typing && field.focusController?.focusedElement is Focusable focused) focused.Blur();
            Show(window, false);
        }

        bool Typing
        {
            get
            {
                var focused = field?.focusController?.focusedElement as VisualElement;
                return focused != null && (focused == field || field.Contains(focused));
            }
        }

        bool Covered => !Allowed || NameScreen.Showing || SettingsWindow.IsOpen || SceneTransition.Busy;

        void Update()
        {
            if (window == null || book == null) return;
            if (!open) { Show(window, false); return; }
            if (Covered)
            {
                // Shut for now, the conversation kept: what arrives meanwhile counts as unread.
                book.Viewing = 0;
                if (Typing && field.focusController?.focusedElement is Focusable focused) focused.Blur();
                submitFrame = -1;
                Show(window, false);
                return;
            }
            Show(window, true);
            window.EnableInClassList("fc-beside", BesideFriends);
            book.Viewing = friend;
            book.MarkRead(friend);
            if (focusFrames > 0) { focusFrames--; if (Typing) focusFrames = 0; else field.Focus(); }

            if (submitFrame >= 0 && Time.frameCount > submitFrame && (Composition() == "" || Time.frameCount > submitFrame + ImeWaitFrames))
            {
                submitFrame = -1;
                Submit();
            }
            else if (Typing && submitFrame < 0)
            {
                KeepCaretInside();
                if (LegacyKeys.Down(KeyCode.Escape)) { MenuSound.Play(MenuSound.Kind.Close); Close(); return; }
                if (LegacyKeys.Down(KeyCode.Return) || LegacyKeys.Down(KeyCode.KeypadEnter)) submitFrame = Time.frameCount;
            }
            Refresh();
        }

        void KeepCaretInside()
        {
            int len = (field.value ?? "").Length;
            if (field.cursorIndex > len || field.selectIndex > len) field.SelectRange(len, len);
        }

        void Submit()
        {
            if (!open) return;
            string text = field.value ?? "";
            if (ChatText.Clean(text) == "") return;
            string refused = send?.Invoke(friend, text) ?? Loc.T("채팅에 연결되지 않았어요.");
            if (refused != "") { ShowNote(refused); return; }
            field.value = "";
            scroll = 0;
            field.Focus();
        }

        void Scroll(int by)
        {
            int count = book == null ? 0 : book.Lines(friend).Count;
            scroll = Mathf.Clamp(scroll + by, 0, Mathf.Max(0, count - 1));
        }

        void ShowNote(string text)
        {
            noteText = text; noteError = true;
            noteUntil = Time.realtimeSinceStartup + NoteSeconds;
        }

        void Swallow(EventBase e)
        {
            e.StopImmediatePropagation();
            field.focusController?.IgnoreEvent(e);
        }

        // ---------- drawing ----------

        void Refresh()
        {
            if (GameSettings.Version != settingsVersion)
            {
                settingsVersion = GameSettings.Version;
                drawnVersion = -1;
                for (int i = 0; i < 3; i++) window.EnableInClassList("fc-size-" + i, GameSettings.ChatSize == i);
            }
            string shownName = StreamerMode.Name(friend, realName?.Invoke(friend) ?? "", false);
            if (nameLabel != null) nameLabel.text = shownName;
            if (letter != null) letter.text = string.IsNullOrEmpty(shownName) ? "?" : shownName.Substring(0, 1);
            if (avatar != null) avatar.style.backgroundColor = AvatarColor(friend);
            if (stateLabel != null) stateLabel.text = stateOf?.Invoke(friend) ?? "";
            if (placeholder != null)
            {
                placeholder.text = Loc.F("{0}님에게 보내기", shownName);
                Show(placeholder, string.IsNullOrEmpty(field.value));
            }
            float now = Time.realtimeSinceStartup;
            bool error = noteError && now < noteUntil;
            if (!error) noteError = false;
            if (hint != null)
            {
                hint.text = error ? noteText : $"{(field.value ?? "").Length}/{ChatText.MaxLength}";
                hint.EnableInClassList("fc-hint-error", error);
            }
            if (scrolled != null) Show(scrolled, scroll > 0);
            if (book.Version != drawnVersion || friend != drawnFriend || scroll != drawnScroll || StreamerMode.Version != drawnStreamer)
            {
                drawnVersion = book.Version; drawnFriend = friend; drawnScroll = scroll; drawnStreamer = StreamerMode.Version;
                DrawLines();
            }
        }

        void DrawLines()
        {
            lines.Clear();
            var all = book.Lines(friend);
            if (all.Count == 0)
            {
                lines.Add(Text(Loc.T("아직 나눈 대화가 없어요. 첫 메시지를 보내 보세요."), "fc-empty"));
                return;
            }
            int end = Mathf.Max(0, all.Count - scroll), start = Mathf.Max(0, end - PanelLines);
            for (int i = start; i < end; i++) lines.Add(Row(all[i]));
        }

        // One line: a bubble (mine gold on the right, theirs blue on the left) and the clock time it came.
        VisualElement Row(FriendChatLine line)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("fc-line");
            if (line.Mine) row.AddToClassList("fc-line-me");
            var bubble = new VisualElement { pickingMode = PickingMode.Ignore };
            bubble.AddToClassList("fc-bubble");
            // Settings → 채팅 → 욕설 가리기 for the friend's words, as in the chat box.
            bubble.Add(Text(line.Mine ? line.Text : GameSettings.Filter(line.Text), "fc-text"));
            row.Add(bubble);
            var at = DateTime.Now.AddSeconds(line.Time - Time.realtimeSinceStartup);
            row.Add(Text(at.ToString("HH:mm"), "fc-time"));
            return row;
        }

        // Typed by players: shown as written, never read as markup.
        static Label Text(string text, string style)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = false, parseEscapeSequences = false };
            label.AddToClassList(style);
            return label;
        }

        // The friends card's pastel per friend (NetworkHudView.AvatarColor).
        static Color AvatarColor(ulong id) => Color.HSVToRGB((id * 2654435761UL % 360UL) / 360f, .35f, .95f);

        static void Show(VisualElement element, bool show)
        {
            if (element != null) element.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static string Composition()
        {
            try { return Input.compositionString ?? ""; }
            catch (InvalidOperationException) { return ""; }
        }

        void OnDestroy()
        {
            if (current == this) current = null;
            if (book != null) book.Viewing = 0;
            if (ownedPanel != null) Destroy(ownedPanel);
        }
    }
}
