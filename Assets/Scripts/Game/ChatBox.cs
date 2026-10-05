using System;
using System.Collections.Generic;
using System.Linq;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // The chat (Docs/Architecture/UI.md "채팅", R61): chat sample A "기보 로그".
    //
    // One panel for the whole game, on the persistent runtime object, so every
    // scene has it without wiring of its own: bottom left in the lobby, bottom
    // right in any match scene (King Rush, Sword Fight and whichever mode comes
    // next), hidden on the title and behind the loading screen.
    //
    // Settings → 채팅 (R64) can turn it off, mask common swear words and change
    // its text size; settings → 조작 can move the key that opens it.
    //
    // Closed: the key that opens it, and new lines floating for a few seconds.
    // Tab opens it on the input line; Tab again moves 파티 → 팀 → 전체; Enter
    // sends; Esc or the close button closes it. In a match a sent line closes it
    // too, so the character moves again at once.
    //
    // While it is open it holds the keyboard (KeysHeld): the scene's own keys
    // (Enter to start, Esc to leave or for a menu) and the character wait.
    // Steam-agnostic like the rest of this assembly: the runtime connects the log
    // and the call that sends.
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class ChatBox : MonoBehaviour
    {
        public enum Layout { Hidden, Lobby, Match }

        // Above every scene's HUD (those keep the default, 0) and under the
        // loading screen (100).
        const float SortingOrder = 50f;
        // Closed, a new line floats this long, the last FadeSeconds of it fading.
        const float RecentSeconds = 8f, FadeSeconds = 2f, NoteSeconds = 3f;
        const int PanelLines = 12, RecentLines = 4;
        // An Enter pressed while the Korean IME still holds a syllable commits it
        // first, so the send waits for the composition to empty, this long at most.
        const int ImeWaitFrames = 30;
        static readonly ChatChannel[] Channels = { ChatChannel.Party, ChatChannel.Team, ChatChannel.All };

        static ChatBox current;
        // True while the chat is open, and for a frame after it closes: the Esc or
        // Enter that closed it must not also leave a match or start a game.
        public static bool KeysHeld => current != null && current.HoldsKeys;
        // A scene with a text field of its own (the lobby's room-number box) says
        // when the chat must stay shut.
        public static Func<bool> Blocker;

        // Where the chat is drawn; the runtime sets it every frame.
        public Layout Where { get; set; }
        public bool IsOpen => open;

        PanelSettings ownedPanel;
        VisualElement box, panel, recent, closedHint, lines;
        TextField field;
        Label chip, placeholder, hint, note, scrolled, closedUnread, closedKey;
        readonly Dictionary<ChatChannel, Button> tabs = new Dictionary<ChatChannel, Button>();
        readonly Dictionary<ChatChannel, Label> unread = new Dictionary<ChatChannel, Label>();
        readonly List<KeyValuePair<ChatEntry, VisualElement>> recentRows = new List<KeyValuePair<ChatEntry, VisualElement>>();

        ChatLog log;
        Func<ChatChannel, bool> canUse;
        Func<ChatChannel, string, string> send;
        ChatChannel channel = ChatChannel.Party;
        Layout lastWhere;
        // The channel the player moved to with Tab, kept while it can be used and
        // the chat stays in the same place.
        bool open, picked, noteError, drawnOpen;
        int panelVersion = -1, recentVersion = -1, settingsVersion = -1, drawnScroll, scroll, submitFrame = -1, releasedFrame = -10;
        ChatChannel drawnChannel;
        float noteUntil;
        string noteText = "";

        bool HoldsKeys => open || submitFrame >= 0 || Time.frameCount - releasedFrame <= 1;

        public void Build()
        {
            current = this;
            var root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("ChatHud"),
                                            Resources.Load<ThemeStyleSheet>("NetworkTheme"), null,
                                            new Vector2Int(1280, 720), out ownedPanel);
            if (root == null) return;
            if (ownedPanel != null) ownedPanel.sortingOrder = SortingOrder;
            // The chat covers the screen but must not take the clicks meant for the
            // scene's HUD under it: only its own panel does.
            root.pickingMode = PickingMode.Ignore;
            box = root.Q<VisualElement>("chat");
            panel = root.Q<VisualElement>("chat-panel");
            recent = root.Q<VisualElement>("chat-recent");
            closedHint = root.Q<VisualElement>("chat-closed");
            lines = root.Q<VisualElement>("chat-lines");
            field = root.Q<TextField>("chat-field");
            chip = root.Q<Label>("chat-channel");
            placeholder = root.Q<Label>("chat-placeholder");
            hint = root.Q<Label>("chat-hint");
            note = root.Q<Label>("chat-note");
            scrolled = root.Q<Label>("chat-scrolled");
            closedUnread = root.Q<Label>("chat-closed-unread");
            closedKey = root.Q<Label>("chat-closed-key");
            if (box == null || panel == null || recent == null || lines == null || field == null)
            { Debug.LogError("[ChessFight] ChatHud.uxml의 이름이 ChatBox와 맞지 않습니다."); box = null; return; }

            foreach (var c in Channels)
            {
                var tab = root.Q<Button>("chat-tab-" + Key(c));
                if (tab == null) continue;
                tab.focusable = false;
                var target = c;
                tab.clicked += () => Pick(target);
                tabs[c] = tab;
                unread[c] = root.Q<Label>("chat-unread-" + Key(c));
            }
            var close = root.Q<Button>("chat-close");
            if (close != null) { close.focusable = false; close.clicked += () => Close(true); }

            field.maxLength = ChatText.MaxLength;
            // Opening the chat again continues a kept line instead of selecting it,
            // so the next key does not wipe it.
            field.selectAllOnFocus = false;
            field.selectAllOnMouseUp = false;
            // Enter, Tab and Esc belong to the chat. Left to the field, Enter gives
            // up the focus and Tab moves it away. Esc closes here as well as in
            // Update, whichever sees the key first.
            field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Escape) { Swallow(e); Close(true); return; }
                bool enter = e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.character == '\n' || e.character == '\r';
                if (!enter && e.keyCode != KeyCode.Tab && e.character != '\t') return;
                Swallow(e);
            }, TrickleDown.TrickleDown);
            field.RegisterCallback<NavigationMoveEvent>(Swallow, TrickleDown.TrickleDown);
            field.RegisterCallback<NavigationSubmitEvent>(Swallow, TrickleDown.TrickleDown);
            field.RegisterCallback<NavigationCancelEvent>(e => { Swallow(e); Close(true); }, TrickleDown.TrickleDown);
            lines.RegisterCallback<WheelEvent>(e => { Scroll(e.delta.y < 0 ? 2 : -2); e.StopPropagation(); });
            Show(box, false);
        }

        public void Connect(ChatLog chatLog, Func<ChatChannel, bool> usable, Func<ChatChannel, string, string> sender)
        {
            log = chatLog; canUse = usable; send = sender;
            panelVersion = recentVersion = -1;
        }

        bool Typing
        {
            get
            {
                var focused = field?.focusController?.focusedElement as VisualElement;
                return focused != null && (focused == field || field.Contains(focused));
            }
        }

        void Update()
        {
            if (box == null) return;
            if (Where != lastWhere) { lastWhere = Where; picked = false; scroll = 0; }
            if (Where == Layout.Hidden || log == null || !GameSettings.ChatOn)
            {
                if (open) Close(false);
                Show(box, false);
                return;
            }
            box.EnableInClassList("chat-lobby", Where == Layout.Lobby);
            box.EnableInClassList("chat-match", Where == Layout.Match);
            Show(box, true);

            bool blocked = Blocked();
            if (open && blocked) Close(false);
            if (submitFrame >= 0 && Time.frameCount > submitFrame && (Composition() == "" || Time.frameCount > submitFrame + ImeWaitFrames))
            {
                submitFrame = -1;
                Submit();
            }
            else if (open && submitFrame < 0)
            {
                if (LegacyKeys.Down(KeyCode.Escape)) Close(true);
                else if (EnterDown()) submitFrame = Time.frameCount;
                else if (LegacyKeys.Down(KeyCode.Tab)) { channel = ChatText.Next(channel, Usable); picked = true; scroll = 0; }
                // A click elsewhere takes the focus; the chat is still open, so it
                // takes it back for the next key.
                else if (!Typing && !MouseHeld()) field.Focus();
            }
            else if (!open && !blocked && Time.frameCount - releasedFrame > 1 && GameSettings.Pressed(GameKey.Chat)) OpenPanel();
            Refresh();
        }

        void OpenPanel()
        {
            if (!(picked && Usable(channel)))
                channel = Where == Layout.Match
                    ? (Usable(ChatChannel.Team) ? ChatChannel.Team : Usable(ChatChannel.All) ? ChatChannel.All : ChatChannel.Party)
                    : (Usable(channel) ? channel : ChatChannel.Party);
            if (!Usable(channel)) return;
            open = true; scroll = 0;
            Refresh();
            // Shown this frame; if it cannot take the focus until laid out, Update
            // hands it over on the next.
            field.Focus();
        }

        void Close(bool drop)
        {
            if (drop && field != null) field.value = "";
            submitFrame = -1;
            if (open) releasedFrame = Time.frameCount;
            open = false;
            if (Typing && field.focusController?.focusedElement is Focusable focused) focused.Blur();
        }

        void Submit()
        {
            if (!open) return;
            string text = field.value ?? "";
            if (ChatText.Clean(text) == "") { Close(true); return; }
            string refused = send?.Invoke(channel, text) ?? "채팅에 연결되지 않았어요.";
            if (refused != "") { ShowNote(refused); return; }
            field.value = "";
            scroll = 0;
            // The lobby keeps the chat open for the next line; a match gives the
            // keys back to the character.
            if (Where == Layout.Match) Close(false);
        }

        void Pick(ChatChannel c)
        {
            if (!open || !Usable(c)) return;
            channel = c; picked = true; scroll = 0;
            field.Focus();
        }

        void Scroll(int by)
        {
            int count = log == null ? 0 : log.In(channel).Count();
            scroll = Mathf.Clamp(scroll + by, 0, Mathf.Max(0, count - 1));
        }

        void ShowNote(string text)
        {
            noteText = text; noteError = true;
            noteUntil = Time.realtimeSinceStartup + NoteSeconds;
        }

        bool Usable(ChatChannel c) => canUse != null && canUse(c);

        static bool Blocked()
        {
            if (SettingsWindow.IsOpen) return true;
            try { return Blocker != null && Blocker(); }
            catch (Exception e) { Debug.LogException(e); return false; }
        }

        void Swallow(EventBase e)
        {
            e.StopImmediatePropagation();
            field.focusController?.IgnoreEvent(e);
        }

        // ---------- drawing ----------

        void Refresh()
        {
            float now = Time.realtimeSinceStartup;
            // Settings (R64): text size, the swear filter and the key that opens it.
            if (GameSettings.Version != settingsVersion)
            {
                settingsVersion = GameSettings.Version;
                panelVersion = recentVersion = -1;
                for (int i = 0; i < 3; i++) box.EnableInClassList("chat-size-" + i, GameSettings.ChatSize == i);
                if (closedKey != null) closedKey.text = GameSettings.KeyName(GameSettings.Key(GameKey.Chat));
            }
            if (open && !Usable(channel)) channel = ChatText.Next(channel, Usable);
            Show(panel, open);
            Show(recent, !open);
            Show(closedHint, !open);

            int waiting = 0;
            foreach (var c in Channels)
            {
                if (open && c == channel) log.MarkRead(c);
                int count = Usable(c) ? log.Unread(c) : 0;
                waiting += count;
                if (tabs.TryGetValue(c, out var tab))
                {
                    tab.EnableInClassList("chat-tab-on", c == channel);
                    tab.SetEnabled(Usable(c));
                }
                if (unread.TryGetValue(c, out var badge) && badge != null) Badge(badge, c == channel ? 0 : count);
            }
            if (closedUnread != null) Badge(closedUnread, open ? 0 : waiting);

            if (chip != null)
            {
                chip.text = ChatText.Label(channel);
                foreach (var c in Channels) chip.EnableInClassList("chat-chip-" + Key(c), c == channel);
            }
            if (placeholder != null)
            {
                placeholder.text = $"{ChatText.Label(channel)}에 말하기";
                Show(placeholder, string.IsNullOrEmpty(field.value));
            }
            bool error = noteError && now < noteUntil;
            if (!error) noteError = false;
            if (hint != null)
            {
                hint.text = error ? noteText : "Enter 보내기 · Tab 채널";
                hint.EnableInClassList("chat-note-error", error);
            }
            if (note != null)
            {
                note.text = open && Where == Layout.Match ? "입력 중 · 이동 멈춤" : "";
                Show(note, note.text != "");
            }
            if (scrolled != null) Show(scrolled, open && scroll > 0);

            if (open && (log.Version != panelVersion || channel != drawnChannel || scroll != drawnScroll || !drawnOpen))
            {
                panelVersion = log.Version; drawnChannel = channel; drawnScroll = scroll;
                DrawPanel();
            }
            drawnOpen = open;
            if (log.Version != recentVersion) { recentVersion = log.Version; DrawRecent(); }
            if (!open) Fade(now);
        }

        // The open panel: the chosen channel's lines, numbered like a scoresheet.
        void DrawPanel()
        {
            lines.Clear();
            var all = log.In(channel).ToList();
            int end = Mathf.Max(0, all.Count - scroll), start = Mathf.Max(0, end - PanelLines);
            for (int i = start; i < end; i++) lines.Add(Row(all[i], true));
        }

        // Closed: the newest lines of every channel, each fading on its own.
        void DrawRecent()
        {
            recent.Clear();
            recentRows.Clear();
            var all = log.Entries;
            for (int i = Mathf.Max(0, all.Count - RecentLines); i < all.Count; i++)
            {
                var e = all[i];
                if (!Usable(e.Channel)) continue;
                var row = Row(e, false);
                row.AddToClassList("chat-recent-line");
                recent.Add(row);
                recentRows.Add(new KeyValuePair<ChatEntry, VisualElement>(e, row));
            }
        }

        void Fade(float now)
        {
            foreach (var pair in recentRows)
            {
                float alpha = Mathf.Clamp01((RecentSeconds - (now - (float)pair.Key.Time)) / FadeSeconds);
                pair.Value.style.opacity = alpha;
                pair.Value.style.display = alpha > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        VisualElement Row(ChatEntry e, bool numbered)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("chat-line");
            if (numbered) row.Add(Text(e.Number + ".", "chat-num"));
            if (e.Notice) { row.Add(Text(e.Text, "chat-notice")); return row; }
            // The Steam name the line came from; outside the panel, which channel too.
            var name = Text(numbered ? e.Name : $"[{ChatText.Label(e.Channel)}] {e.Name}", "chat-name");
            name.AddToClassList("chat-name-" + Key(e.Channel));
            if (e.Mine) name.AddToClassList("chat-name-me");
            row.Add(name);
            row.Add(Text(GameSettings.Filter(e.Text), "chat-text"));
            return row;
        }

        // Players type these and pick their Steam names: shown as written, never
        // read as markup such as <size=200>.
        static Label Text(string text, string style)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = false, parseEscapeSequences = false };
            label.AddToClassList(style);
            return label;
        }

        static void Badge(Label badge, int count)
        {
            badge.text = count > 99 ? "99+" : count.ToString();
            badge.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static string Key(ChatChannel c) => c == ChatChannel.Party ? "party" : c == ChatChannel.Team ? "team" : "all";

        static void Show(VisualElement element, bool show)
        {
            if (element != null) element.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static bool EnterDown() => LegacyKeys.Down(KeyCode.Return) || LegacyKeys.Down(KeyCode.KeypadEnter);

        static bool MouseHeld()
        {
            try { return Input.GetMouseButton(0) || Input.GetMouseButton(1); }
            catch (InvalidOperationException) { return false; }
        }

        static string Composition()
        {
            try { return Input.compositionString ?? ""; }
            catch (InvalidOperationException) { return ""; }
        }

        void OnDestroy()
        {
            if (current == this) current = null;
            if (ownedPanel != null) Destroy(ownedPanel);
        }
    }
}
