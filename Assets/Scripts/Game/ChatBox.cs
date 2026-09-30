using System;
using System.Collections.Generic;
using System.Linq;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // The chat box (Docs/Architecture/UI.md "채팅", R61): chat sample A "기보 로그".
    //
    // Lobby: a panel bottom left, above the party bar. A tab per channel (파티,
    // 전체, 팀) with a count of unread lines, lines numbered like the moves on a
    // scoresheet, and the input line always there: click it to type. The mouse
    // wheel scrolls back.
    // Match: bottom right, no panel. The last lines of every channel float over
    // the game and fade after a few seconds. Enter opens the input line, and the
    // owner stops the character while Typing.
    // Both: Enter sends, Tab changes the channel, Esc drops the line.
    //
    // Steam-agnostic like the rest of this assembly: the owner connects the log
    // and the call that sends. Not a component: the owner's Update calls Tick
    // before its own shortcuts and skips them while HoldsKeys.
    public sealed class ChatBox
    {
        public enum Layout { Lobby, Match }

        // Match lines stay this long, the last FadeSeconds of it fading.
        const float ShowSeconds = 10f, FadeSeconds = 2f, NoteSeconds = 3f;
        // Lines drawn: the lobby clips the oldest at the top of its panel.
        const int LobbyLines = 12, MatchLines = 8;
        // An Enter pressed while the Korean IME still holds a syllable commits it
        // first, so the send waits for the composition to empty, this long at most.
        const int ImeWaitFrames = 30;
        static readonly ChatChannel[] Channels = { ChatChannel.Party, ChatChannel.All, ChatChannel.Team };

        readonly Layout layout;
        readonly VisualElement box, lines, entry;
        readonly TextField field;
        readonly Label chip, placeholder, hint, note, scrolled;
        readonly Dictionary<ChatChannel, Button> tabs = new Dictionary<ChatChannel, Button>();
        readonly Dictionary<ChatChannel, Label> unread = new Dictionary<ChatChannel, Label>();
        readonly List<KeyValuePair<ChatEntry, VisualElement>> rows = new List<KeyValuePair<ChatEntry, VisualElement>>();

        ChatLog log;
        Func<ChatChannel, bool> canUse;
        Func<ChatChannel, string, string> send;
        ChatChannel channel = ChatChannel.Party;
        // Match: the channel the player last picked with Tab, kept while usable.
        bool picked;
        bool wasTyping, drawnTyping, noteError;
        int drawnVersion = -1, drawnScroll, scroll, submitFrame = -1, releasedFrame = -10;
        ChatChannel drawnChannel;
        float noteUntil;
        string noteText = "";

        public ChatBox(VisualElement slot, Layout layout)
        {
            this.layout = layout;
            var tree = Resources.Load<VisualTreeAsset>("ChatHud");
            if (slot == null || tree == null) { Debug.LogError("[ChessFight] 채팅창을 만들지 못했습니다 (Resources/ChatHud)."); return; }
            tree.CloneTree(slot);
            box = slot.Q<VisualElement>("chat");
            lines = slot.Q<VisualElement>("chat-lines");
            entry = slot.Q<VisualElement>("chat-entry");
            field = slot.Q<TextField>("chat-field");
            chip = slot.Q<Label>("chat-channel");
            placeholder = slot.Q<Label>("chat-placeholder");
            hint = slot.Q<Label>("chat-hint");
            note = slot.Q<Label>("chat-note");
            scrolled = slot.Q<Label>("chat-scrolled");
            if (box == null || lines == null || entry == null || field == null)
            { Debug.LogError("[ChessFight] ChatHud.uxml의 이름이 ChatBox와 맞지 않습니다."); box = null; return; }
            box.AddToClassList(layout == Layout.Lobby ? "chat-lobby" : "chat-match");

            foreach (var c in Channels)
            {
                var tab = slot.Q<Button>("chat-tab-" + Key(c));
                if (tab == null) continue;
                tab.focusable = false;
                var target = c;
                tab.clicked += () => Pick(target);
                tabs[c] = tab;
                unread[c] = slot.Q<Label>("chat-unread-" + Key(c));
            }

            field.maxLength = ChatText.MaxLength;
            // Enter, Tab and Esc belong to the chat. Left to the field, Enter gives
            // up the focus, so the next Enter started a game, and Tab moves the
            // focus away. Esc closes the line here as well as in Tick, whichever
            // sees the key first: the field's own Esc only reverts the text.
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
            // Whatever ends the typing (Esc handled by the field itself, a click
            // elsewhere), its key must not also reach the scene this frame.
            field.RegisterCallback<FocusOutEvent>(_ => releasedFrame = Time.frameCount);

            if (layout == Layout.Lobby)
                lines.RegisterCallback<WheelEvent>(e => { Scroll(e.delta.y < 0 ? 2 : -2); e.StopPropagation(); });
            else
            {
                // Over the game the box takes no clicks; only its input line does.
                box.pickingMode = PickingMode.Ignore;
                lines.pickingMode = PickingMode.Ignore;
                entry.pickingMode = PickingMode.Ignore;
            }
            Refresh();
        }

        public void Connect(ChatLog chatLog, Func<ChatChannel, bool> usable, Func<ChatChannel, string, string> sender)
        {
            log = chatLog; canUse = usable; send = sender;
            drawnVersion = -1;
        }

        public bool Typing
        {
            get
            {
                var focused = field?.focusController?.focusedElement as VisualElement;
                return focused != null && (focused == field || field.Contains(focused));
            }
        }

        // Typing now, or it ended this frame or the last: the Enter or Esc that
        // ended it must not also start a game or leave a match.
        public bool HoldsKeys => Typing || wasTyping || submitFrame >= 0 || Time.frameCount - releasedFrame <= 1;

        public bool Contains(VisualElement element) =>
            box != null && element != null && (element == box || box.Contains(element));

        public void Tick()
        {
            if (box == null) return;
            bool typing = Typing;
            if (submitFrame >= 0 && Time.frameCount > submitFrame && (Composition() == "" || Time.frameCount > submitFrame + ImeWaitFrames))
            {
                submitFrame = -1;
                Submit();
                typing = Typing;
            }
            else if (typing && submitFrame < 0)
            {
                if (EnterDown()) submitFrame = Time.frameCount;
                else if (LegacyKeys.Down(KeyCode.Escape)) { Close(true); typing = false; }
                else if (LegacyKeys.Down(KeyCode.Tab)) { channel = ChatText.Next(channel, Usable); picked = true; }
            }
            else if (!typing && !wasTyping && layout == Layout.Match && EnterDown() && log != null) { Open(); typing = Typing; }
            wasTyping = typing;
            Refresh();
        }

        void Open()
        {
            if (layout == Layout.Match && !(picked && Usable(channel)))
                channel = Usable(ChatChannel.Team) ? ChatChannel.Team : Usable(ChatChannel.All) ? ChatChannel.All : ChatChannel.Party;
            if (!Usable(channel)) return;
            field.Focus();
        }

        void Close(bool drop)
        {
            if (drop) field.value = "";
            submitFrame = -1;
            if (field.focusController?.focusedElement is Focusable focused && Typing) focused.Blur();
            releasedFrame = Time.frameCount;
        }

        void Submit()
        {
            string text = field.value ?? "";
            if (ChatText.Clean(text) == "") { Close(true); return; }
            string refused = send?.Invoke(channel, text) ?? "채팅에 연결되지 않았어요.";
            if (refused != "") { ShowNote(refused); if (!Typing) field.Focus(); return; }
            field.value = "";
            scroll = 0;
            // The lobby keeps the line open for the next message; a match gives the
            // keys back to the character.
            if (layout == Layout.Match) Close(false);
            else if (!Typing) field.Focus();
        }

        void Pick(ChatChannel c)
        {
            if (!Usable(c)) return;
            channel = c; scroll = 0;
            Refresh();
        }

        void Scroll(int by)
        {
            int count = log == null ? 0 : log.In(channel).Count();
            scroll = Mathf.Clamp(scroll + by, 0, Mathf.Max(0, count - 1));
            Refresh();
        }

        void ShowNote(string text)
        {
            noteText = text; noteError = true;
            noteUntil = Time.realtimeSinceStartup + NoteSeconds;
        }

        bool Usable(ChatChannel c) => canUse != null && canUse(c);

        void Swallow(EventBase e)
        {
            e.StopImmediatePropagation();
            field.focusController?.IgnoreEvent(e);
        }

        // ---------- drawing ----------

        void Refresh()
        {
            bool typing = Typing;
            if (layout == Layout.Lobby && !Usable(channel) && !typing) { channel = ChatChannel.Party; scroll = 0; }
            float now = Time.realtimeSinceStartup;

            foreach (var c in Channels)
            {
                if (!tabs.TryGetValue(c, out var tab)) continue;
                tab.EnableInClassList("chat-tab-on", c == channel);
                tab.SetEnabled(Usable(c));
                if (c == channel) log?.MarkRead(c);
                var badge = unread[c];
                if (badge == null) continue;
                int count = log == null || c == channel ? 0 : log.Unread(c);
                badge.text = count > 99 ? "99+" : count.ToString();
                badge.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (layout == Layout.Match && log != null) foreach (var c in Channels) log.MarkRead(c);

            if (chip != null)
            {
                chip.text = ChatText.Label(channel);
                foreach (var c in Channels) chip.EnableInClassList("chat-chip-" + Key(c), c == channel);
            }
            box.EnableInClassList("chat-typing", typing);
            Show(entry, layout == Layout.Lobby || typing || submitFrame >= 0);
            if (placeholder != null)
            {
                placeholder.text = layout == Layout.Lobby ? $"여기를 눌러 {ChatText.Label(channel)}에 말하기" : "";
                Show(placeholder, !typing && string.IsNullOrEmpty(field.value));
            }
            if (scrolled != null) Show(scrolled, scroll > 0);

            // A refused line says why for a few seconds: in the lobby at the end of
            // the input line (the panel has a fixed height), in a match under it.
            bool error = noteError && now < noteUntil;
            if (!error) noteError = false;
            bool lobbyError = error && layout == Layout.Lobby;
            if (hint != null)
            {
                hint.text = lobbyError ? noteText : typing ? (layout == Layout.Lobby ? "Enter 보내기 · Tab 채널 · Esc 닫기" : "Tab 채널 · Esc 취소") : "";
                hint.EnableInClassList("chat-note-error", lobbyError);
            }
            if (note != null)
            {
                string text = layout == Layout.Lobby ? "" : error ? noteText : typing ? "입력 중 · 이동 멈춤" : "";
                note.text = text;
                note.EnableInClassList("chat-note-error", error);
                Show(note, text != "");
            }

            int version = log?.Version ?? -1;
            if (version != drawnVersion || channel != drawnChannel || scroll != drawnScroll || typing != drawnTyping)
            {
                drawnVersion = version; drawnChannel = channel; drawnScroll = scroll; drawnTyping = typing;
                Redraw();
            }
            if (layout == Layout.Match) Fade(now, typing);
        }

        void Redraw()
        {
            lines.Clear();
            rows.Clear();
            if (log == null) return;
            List<ChatEntry> list;
            if (layout == Layout.Lobby)
            {
                var all = log.In(channel).ToList();
                int end = Mathf.Max(0, all.Count - scroll);
                list = all.GetRange(Mathf.Max(0, end - LobbyLines), end - Mathf.Max(0, end - LobbyLines));
            }
            else
            {
                var all = log.Entries;
                list = all.Skip(Mathf.Max(0, all.Count - MatchLines)).ToList();
            }
            foreach (var e in list)
            {
                var row = Row(e);
                lines.Add(row);
                rows.Add(new KeyValuePair<ChatEntry, VisualElement>(e, row));
            }
        }

        // Match lines fade out a few seconds after they arrive; typing brings the
        // recent ones back.
        void Fade(float now, bool typing)
        {
            foreach (var pair in rows)
            {
                float age = now - (float)pair.Key.Time;
                float alpha = typing ? 1f : Mathf.Clamp01((ShowSeconds - age) / FadeSeconds);
                pair.Value.style.opacity = alpha;
                pair.Value.style.display = alpha > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        VisualElement Row(ChatEntry e)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("chat-line");
            if (layout == Layout.Lobby) row.Add(Text(e.Number + ".", "chat-num"));
            if (e.Notice) { row.Add(Text(e.Text, "chat-notice")); return row; }
            // The Steam name the line came from; in a match, which channel too.
            var name = Text(layout == Layout.Match ? $"[{ChatText.Label(e.Channel)}] {e.Name}" : e.Name, "chat-name");
            name.AddToClassList("chat-name-" + Key(e.Channel));
            if (e.Mine) name.AddToClassList("chat-name-me");
            row.Add(name);
            row.Add(Text(e.Text, "chat-text"));
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

        static string Key(ChatChannel c) => c == ChatChannel.Party ? "party" : c == ChatChannel.All ? "all" : "team";

        static void Show(VisualElement element, bool show)
        {
            if (element != null) element.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static bool EnterDown() => LegacyKeys.Down(KeyCode.Return) || LegacyKeys.Down(KeyCode.KeypadEnter);

        static string Composition()
        {
            try { return Input.compositionString ?? ""; }
            catch (InvalidOperationException) { return ""; }
        }
    }
}
