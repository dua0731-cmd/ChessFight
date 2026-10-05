using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // The settings window (R64, Docs/Architecture/UI.md §12): Esc on the title
    // screen and in the lobby opens it, Esc again or "닫기" closes it. A walnut
    // card over a dimmed screen, drawn above every other panel (the entrance
    // screen included), with five tabs: 소리, 화면, 조작, 채팅, 기타. Every change
    // is saved at once through GameSettings. Design sample:
    // https://claude.ai/artifact/C7WFSCyXfVj6wLX4zDRw4g
    //
    // The screens own the Esc key: IntroController and NetworkHudView call
    // Toggle/Back, so there is one place per screen that decides what Esc does.
    // While it is open those screens ignore their other keys and clicks, and the
    // chat stays shut. It belongs to the scene it was opened in and goes with it.
    [DisallowMultipleComponent]
    public sealed class SettingsWindow : MonoBehaviour
    {
        // Above the entrance screen (LoadingScreenView, 100).
        const float SortingOrder = 200f;
        const float QuitArmSeconds = 3f;
        static readonly string[] Tabs = { "소리", "화면", "조작", "채팅", "기타" };
        static readonly (GameKey bind, string label)[] KeyRows =
        {
            (GameKey.Forward, "앞으로"), (GameKey.Back, "뒤로"), (GameKey.Left, "왼쪽"), (GameKey.Right, "오른쪽"),
            (GameKey.Jump, "점프"), (GameKey.Sprint, "질주"), (GameKey.Chat, "채팅 열기"),
        };

        static SettingsWindow current;
        static KeyCode[] capturable;
        static int tab;

        public static bool IsOpen => current != null;

        PanelSettings ownedPanel;
        VisualElement body;
        Button quit;
        readonly Button[] tabButtons = new Button[Tabs.Length];
        // Each control's "show the current value" step, run after any change.
        readonly List<Action> refreshers = new List<Action>();
        GameKey? waiting;
        float quitArmedAt = -1;

        public static void Toggle()
        {
            if (IsOpen) Back();
            else Open();
        }

        public static void Open()
        {
            if (IsOpen) return;
            current = new GameObject("Settings Window").AddComponent<SettingsWindow>();
            current.Build();
        }

        // Esc while open: first calls off a key being rebound, then closes.
        public static void Back()
        {
            if (!IsOpen) return;
            if (current.waiting != null) { current.waiting = null; current.Refresh(); return; }
            Close();
        }

        public static void Close()
        {
            if (!IsOpen) return;
            Destroy(current.gameObject);
            current = null;
        }

        void Build()
        {
            var root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("SettingsHud"),
                                            Resources.Load<ThemeStyleSheet>("NetworkTheme"), null,
                                            new Vector2Int(1280, 720), out ownedPanel);
            if (root == null) return;
            if (ownedPanel != null) ownedPanel.sortingOrder = SortingOrder;

            var card = root.Q<VisualElement>("settings-card");
            if (card != null)
            {
                card.style.backgroundImage = new StyleBackground(MenuArt.PanelTexture());
                var accent = new VisualElement { pickingMode = PickingMode.Ignore };
                accent.AddToClassList("panel-accent");
                card.Insert(0, accent);
            }
            body = root.Q<VisualElement>("settings-body");
            for (int i = 0; i < Tabs.Length; i++)
            {
                int index = i;
                var button = tabButtons[i] = root.Q<Button>("settings-tab-" + i);
                if (button == null) continue;
                button.focusable = false;
                button.clicked += () => { if (tab != index) { tab = index; waiting = null; Disarm(); Render(); } };
                ChunkyButtons.Springy(button);
            }
            Wire(root.Q<Button>("settings-x"), Close);
            Wire(root.Q<Button>("settings-close"), Close);
            Wire(root.Q<Button>("settings-reset"), ResetTab);
            quit = root.Q<Button>("settings-quit");
            Wire(quit, Quit);
            ChunkyButtons.Attach(root);
            Render();
        }

        static void Wire(Button button, Action action)
        {
            if (button == null) return;
            button.focusable = false;
            button.clicked += action;
        }

        void Update()
        {
            if (quitArmedAt >= 0 && Time.unscaledTime - quitArmedAt > QuitArmSeconds) Disarm();
            if (waiting == null) return;
            foreach (var key in Capturable())
            {
                if (!LegacyKeys.Down(key)) continue;
                GameSettings.SetKey(waiting.Value, key);
                waiting = null;
                Refresh();
                return;
            }
        }

        // Every key a player could bind: not the mouse or pads, not Esc (it cancels).
        static KeyCode[] Capturable()
        {
            if (capturable != null) return capturable;
            var list = new List<KeyCode>();
            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (key == KeyCode.None || key == KeyCode.Escape || list.Contains(key)) continue;
                if (key >= KeyCode.Mouse0) continue;   // Mouse0..6 and every joystick button follow
                list.Add(key);
            }
            return capturable = list.ToArray();
        }

        // ---------- tabs ----------

        void Render()
        {
            if (body == null) return;
            body.Clear();
            refreshers.Clear();
            for (int i = 0; i < tabButtons.Length; i++) tabButtons[i]?.EnableInClassList("settings-tab-on", i == tab);
            switch (tab)
            {
                case 0: SoundTab(); break;
                case 1: DisplayTab(); break;
                case 2: ControlsTab(); break;
                case 3: ChatTab(); break;
                default: OtherTab(); break;
            }
            RuntimePanels.ApplyDisplay(body);
            ChunkyButtons.Attach(body);
            Refresh();
        }

        void Refresh()
        {
            foreach (var refresh in refreshers) refresh();
        }

        void SoundTab()
        {
            Volume("전체 소리", null, () => GameSettings.Master, GameSettings.SetMaster);
            Volume("배경 음악", "음악이 들어가면 적용", () => GameSettings.Music, GameSettings.SetMusic);
            Volume("효과음", "효과음이 들어가면 적용", () => GameSettings.Effects, GameSettings.SetEffects);
            Volume("음성 채팅", "음성 채팅이 생기면 적용", () => GameSettings.Voice, GameSettings.SetVoice);
        }

        void DisplayTab()
        {
            string editor = Application.isEditor ? "에디터에서는 안 바뀜 · 빌드에서 적용" : null;
            Choices("화면 모드", editor, GameSettings.ModeNames, () => GameSettings.ScreenModeIndex, GameSettings.SetScreenMode);
            Stepper("해상도", () => editor,
                    () => { var s = GameSettings.CurrentSize; return s.x + " × " + s.y; },
                    step =>
                    {
                        var sizes = GameSettings.Sizes;
                        int at = Mathf.Max(0, sizes.IndexOf(GameSettings.CurrentSize));
                        GameSettings.SetSize(sizes[(at + step + sizes.Count) % sizes.Count]);
                    });
            var levels = new string[QualitySettings.names.Length];
            for (int i = 0; i < levels.Length; i++) levels[i] = GameSettings.QualityName(i);
            Choices("그래픽 품질", "낮음 = 사양 낮은 PC용", levels, () => GameSettings.Quality, GameSettings.SetQuality);
            OnOff("수직 동기화", "화면 찢어짐 방지", () => GameSettings.VSync, GameSettings.SetVSync);
            Stepper("프레임 제한", () => GameSettings.VSync ? "수직 동기화가 켜져 있으면 모니터 주사율" : null,
                    () => GameSettings.FrameLimit > 0 ? GameSettings.FrameLimit.ToString() : "제한 없음",
                    step =>
                    {
                        var limits = GameSettings.FrameLimits;
                        int at = Mathf.Max(0, Array.IndexOf(limits, GameSettings.FrameLimit));
                        GameSettings.SetFrameLimit(limits[(at + step + limits.Length) % limits.Length]);
                    });
        }

        void ControlsTab()
        {
            Slider("마우스 감도", null, .1f, 3f, .1f, () => GameSettings.MouseScale, GameSettings.SetMouseScale,
                   v => v.ToString("0.0") + "배");
            OnOff("상하 반전", "마우스를 올리면 아래를 봄", () => GameSettings.InvertY, GameSettings.SetInvertY);

            var head = Add(body, "set-section");
            Add(head, "disp set-section-title", "키 설정");
            Add(head, "set-hint", "칸을 누르고 바꿀 키를 누르세요 · 이미 쓰는 키면 서로 바뀝니다 · Esc는 취소");
            var grid = Add(body, "set-keys");
            foreach (var (bind, label) in KeyRows)
            {
                var row = Add(grid, "set-key-row");
                Add(row, "set-label", label);
                var button = new Button { focusable = false };
                button.AddToClassList("chunky");
                button.AddToClassList("ch-ivory");
                button.AddToClassList("ch-xs");
                button.AddToClassList("set-key");
                button.clicked += () => { waiting = waiting == bind ? (GameKey?)null : bind; Disarm(); Refresh(); };
                row.Add(button);
                refreshers.Add(() =>
                {
                    bool asking = waiting == bind;
                    // Gold while it waits for a key; ivory otherwise.
                    button.EnableInClassList("ch-ivory", !asking);
                    SetText(button, asking ? "키를 누르세요…" : GameSettings.KeyName(GameSettings.Key(bind)));
                    button.MarkDirtyRepaint();
                });
            }
        }

        void ChatTab()
        {
            OnOff("채팅", null, () => GameSettings.ChatOn, GameSettings.SetChatOn);
            OnOff("욕설 가리기", "흔한 욕을 ** 로 가림", () => GameSettings.ChatFilter, GameSettings.SetChatFilter);
            Choices("글자 크기", null, new[] { "작게", "보통", "크게" }, () => GameSettings.ChatSize, GameSettings.SetChatSize);

            var preview = Add(body, "set-preview");
            Add(preview, "settings-kicker", "미리 보기");
            var lines = Add(preview, "");
            var first = Add(lines, "set-preview-row");
            var second = Add(lines, "set-preview-row");
            var off = Add(preview, "set-note", "");
            Label[] Line(VisualElement row, string number, string name, Color color)
            {
                var n = Add(row, "chat-num", number);
                var who = Add(row, "chat-name", name);
                who.style.color = color;
                return new[] { n, who, Add(row, "chat-text", "") };
            }
            var a = Line(first, "12.", "캐슬링", new Color(60 / 255f, 208 / 255f, 1f));
            var b = Line(second, "13.", "앙파상", new Color(1f, 214 / 255f, 120 / 255f));
            a[2].text = "2랭크까지 같이 가자";
            foreach (var label in new[] { a[2], b[2] }) { label.style.flexGrow = 0; label.style.flexBasis = StyleKeyword.Auto; }
            refreshers.Add(() =>
            {
                b[2].text = GameSettings.Filter("아 ㅅㅂ 또 물에 빠졌네");
                for (int i = 0; i < 3; i++) lines.EnableInClassList("chat-size-" + i, GameSettings.ChatSize == i);
                Show(lines, GameSettings.ChatOn);
                off.text = GameSettings.ChatOn ? "" : $"채팅이 꺼져 있어요. {GameSettings.KeyName(GameSettings.Key(GameKey.Chat))}을 눌러도 열리지 않고 새 말도 뜨지 않습니다.";
                Show(off, !GameSettings.ChatOn);
            });
        }

        void OtherTab()
        {
            OnOff("핑 보이기", "경기 화면의 핑·응답 줄", () => GameSettings.ShowPing, GameSettings.SetShowPing);
            OnOff("화면 흔들림 줄이기", "카메라 흔들림·질주 시야 넓어짐 끔", () => GameSettings.ReduceShake, GameSettings.SetReduceShake);
            var language = Stepper("언어", () => "다른 언어는 준비 중", () => "한국어", _ => { });
            language.Query<Button>().ForEach(b => b.SetEnabled(false));

            var match = Add(body, "set-section");
            Add(match, "settings-kicker", "경기 화면");
            var ping = Add(match, "set-ping", "핑 42ms · 응답 51ms");
            var hidden = Add(match, "set-hint", "핑 줄을 숨깁니다 (연결 불안정 경고는 그대로 떠요)");
            hidden.style.marginLeft = 12;
            refreshers.Add(() => { Show(ping, GameSettings.ShowPing); Show(hidden, !GameSettings.ShowPing); });
        }

        // ---------- row kinds ----------

        // Label on the left (with a small note under it), the control on the right.
        VisualElement Row(string label, Func<string> note)
        {
            var row = Add(body, "set-row");
            var left = Add(row, "set-left");
            Add(left, "set-label", label);
            if (note != null)
            {
                var small = Add(left, "set-note", "");
                refreshers.Add(() =>
                {
                    string text = note();
                    small.text = text ?? "";
                    Show(small, !string.IsNullOrEmpty(text));
                });
            }
            return Add(row, "set-ctrl");
        }

        void Volume(string label, string note, Func<float> get, Action<float> set) =>
            Slider(label, note, 0f, 1f, .1f, get, set, v => Mathf.RoundToInt(v * 100).ToString());

        // ‹ bar › value. The bar takes a click or a drag, the arrows a step.
        void Slider(string label, string note, float min, float max, float step, Func<float> get, Action<float> set, Func<float, string> show)
        {
            var ctrl = Row(label, note == null ? null : (Func<string>)(() => note));
            Arrow(ctrl, "‹", () => set(Snap(get() - step)));
            var bar = Add(ctrl, "set-bar");
            bar.pickingMode = PickingMode.Position;
            var fill = Add(bar, "set-fill");
            var knob = Add(bar, "set-knob");
            Arrow(ctrl, "›", () => set(Snap(get() + step)));
            var value = Add(ctrl, "disp set-value", "");
            ChunkyButtons.Springy(bar);

            float Snap(float v) => Mathf.Clamp(Mathf.Round(v / step) * step, min, max);
            void Show()
            {
                float t = Mathf.InverseLerp(min, max, get());
                fill.style.width = new Length(t * 100f, LengthUnit.Percent);
                knob.style.left = new Length(t * 100f, LengthUnit.Percent);
                value.text = show(get());
            }
            void At(Vector2 local)
            {
                float width = bar.layout.width;
                if (width <= 0) return;
                set(Snap(Mathf.Lerp(min, max, Mathf.Clamp01(local.x / width))));
                Disarm();
                Show();
            }
            bar.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                bar.CapturePointer(e.pointerId);
                At(e.localPosition);
            });
            bar.RegisterCallback<PointerMoveEvent>(e => { if (bar.HasPointerCapture(e.pointerId)) At(e.localPosition); });
            bar.RegisterCallback<PointerUpEvent>(e => { if (bar.HasPointerCapture(e.pointerId)) bar.ReleasePointer(e.pointerId); });
            refreshers.Add(Show);
        }

        // ‹ value ›
        VisualElement Stepper(string label, Func<string> note, Func<string> value, Action<int> step)
        {
            var ctrl = Row(label, note);
            Arrow(ctrl, "‹", () => step(-1));
            var box = Add(ctrl, "set-box", "");
            Arrow(ctrl, "›", () => step(1));
            refreshers.Add(() => box.text = value());
            return ctrl;
        }

        // A row of choices, the chosen one gold.
        void Choices(string label, string note, string[] names, Func<int> get, Action<int> set)
        {
            var ctrl = Row(label, note == null ? null : (Func<string>)(() => note));
            var group = Add(ctrl, "set-seg");
            var buttons = new Button[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var button = buttons[i] = new Button { text = names[i], focusable = false };
                button.AddToClassList("set-seg-btn");
                button.clicked += () => { if (get() != index) set(index); Changed(); };
                ChunkyButtons.Springy(button);
                group.Add(button);
            }
            refreshers.Add(() =>
            {
                int on = get();
                for (int i = 0; i < buttons.Length; i++) buttons[i].EnableInClassList("set-seg-on", i == on);
            });
        }

        void OnOff(string label, string note, Func<bool> get, Action<bool> set) =>
            Choices(label, note, new[] { "켜짐", "꺼짐" }, () => get() ? 0 : 1, i => set(i == 0));

        void Arrow(VisualElement parent, string text, Action action)
        {
            var button = new Button { text = text, focusable = false };
            button.AddToClassList("chunky");
            button.AddToClassList("ch-wood");
            button.AddToClassList("ch-xs");
            button.AddToClassList("set-arrow");
            button.clicked += () => { action(); Changed(); };
            parent.Add(button);
        }

        void Changed()
        {
            Disarm();
            Refresh();
        }

        // ---------- footer ----------

        void ResetTab()
        {
            switch (tab)
            {
                case 0:
                    GameSettings.SetMaster(1f); GameSettings.SetMusic(.8f); GameSettings.SetEffects(.8f); GameSettings.SetVoice(.8f);
                    break;
                case 1: GameSettings.ResetDisplay(); break;
                case 2: GameSettings.SetMouseScale(1f); GameSettings.SetInvertY(false); GameSettings.ResetKeys(); waiting = null; break;
                case 3: GameSettings.SetChatOn(true); GameSettings.SetChatFilter(true); GameSettings.SetChatSize(1); break;
                default: GameSettings.SetShowPing(true); GameSettings.SetReduceShake(false); break;
            }
            Changed();
        }

        // Twice to quit: the first press asks, the second (within three seconds) quits.
        void Quit()
        {
            if (quitArmedAt < 0)
            {
                quitArmedAt = Time.unscaledTime;
                SetText(quit, "한 번 더 누르면 종료");
                return;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Disarm()
        {
            if (quitArmedAt < 0) return;
            quitArmedAt = -1;
            SetText(quit, "게임 종료");
        }

        // ---------- helpers ----------

        // A slab button keeps its words on a child label (ChunkyButtons); write
        // there so the change shows this frame, not on the next poll.
        static void SetText(Button button, string text)
        {
            if (button == null) return;
            var label = button.Q<Label>(className: "chunky-text");
            if (label != null) label.text = text;
            else button.text = text;
        }

        static Label Add(VisualElement parent, string classes, string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (var c in classes.Split(' ')) if (c.Length > 0) label.AddToClassList(c);
            parent.Add(label);
            return label;
        }

        static VisualElement Add(VisualElement parent, string classes)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (var c in classes.Split(' ')) if (c.Length > 0) element.AddToClassList(c);
            parent.Add(element);
            return element;
        }

        static void Show(VisualElement element, bool show) =>
            element.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

        void OnDestroy()
        {
            if (current == this) current = null;
            if (ownedPanel != null) Destroy(ownedPanel);
        }
    }
}
