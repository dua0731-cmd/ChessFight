using System;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // The name screen (R84, Docs/Architecture/UI.md §15), shown the first time the
    // game starts, between the title and the lobby: a walnut background with a
    // pattern of little pieces, a glass pawn slowly turning on the right, and on
    // the left one field. Beside the count "n / 12" sits "추천 이름"; it floats a
    // suggested name over the field, taken with a click. A name that may not be
    // used shows why in place of the count (PlayerNames). Enter or the ivory
    // button confirms; the title screen's controller then runs the transition.
    // Built `renaming` (R85: the title screen's "이름 바꾸기" or the lobby's
    // own-name button, through NameChange) it says "이름 바꾸기", shows the
    // current name, and "돌아가기" or Esc leaves it as it was (Cancelled).
    //
    // Clicks are read from the mouse (like the title's start plate), so the screen
    // also works where UI Toolkit pointer events do not arrive.
    [DisallowMultipleComponent]
    public sealed class NameScreen : MonoBehaviour
    {
        // While one is up, the lobby's shortcuts and the chat stand down (its
        // field has the keys) and the chat box is hidden.
        public static bool Showing => current != null;
        static NameScreen current;

        // Over the title's HUD (0), under the chat (50) and the settings window (200).
        const float SortingOrder = 20f;
        const int ImeWaitFrames = 30;
        // The plate's release plays before the transition starts.
        const float ConfirmDelay = .35f;

        static readonly (float at, Color c)[] Silver =
        {
            (0f, Hex(0xFFFFFF)), (.28f, Hex(0xE4E9EE)), (.49f, Hex(0xA3AEB9)), (.53f, Hex(0x66707B)), (.76f, Hex(0xBCC5CE)), (1f, Hex(0xF2F5F8))
        };
        static readonly Color LineIdle = new Color(217 / 255f, 174 / 255f, 98 / 255f, .55f);

        public bool Ready { get; private set; }
        // Where the pawn stands, as a fraction of the screen (0..1 from the top left):
        // the transition closes its king-shaped hole there.
        public Vector2 PawnAt => Fraction(pawnBox, new Vector2(250f, 270f));
        public event Action<string> Confirmed;
        // "돌아가기" or Esc while renaming: the name stays as it was.
        public event Action Cancelled;

        PanelSettings ownedPanel;
        VisualElement root, fieldBox, bubble, startButton, startArrow, suggestButton, pawnBox, dice, backButton;
        TextField field;
        Label placeholder, counter, bubbleText, startText;
        GlassPawn pawn;
        readonly System.Random random = new System.Random();
        string suggestion = "";
        bool renaming, confirming, leaving, fromSuggestion;
        float confirmAt = -1, diceTurn, diceShown, bubbleShownAt = -10;
        int submitFrame = -1, builtFrame;
        VisualElement pressedOn;

        public void Build(string prefill, bool renaming = false)
        {
            this.renaming = renaming;
            root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("NameHud"),
                                        Resources.Load<ThemeStyleSheet>("NetworkTheme"), null,
                                        new Vector2Int(1280, 720), out ownedPanel);
            if (root == null) return;
            if (ownedPanel != null) ownedPanel.sortingOrder = SortingOrder;
            IntroFlowStyle.Apply(root);
            fieldBox = root.Q<VisualElement>("name-field-box");
            field = root.Q<TextField>("name-field");
            placeholder = root.Q<Label>("name-placeholder");
            counter = root.Q<Label>("name-counter");
            suggestButton = root.Q<VisualElement>("name-suggest");
            dice = root.Q<VisualElement>("name-dice");
            bubble = root.Q<VisualElement>("name-bubble");
            bubbleText = root.Q<Label>("name-bubble-text");
            startButton = root.Q<VisualElement>("name-start");
            startText = root.Q<Label>("name-start-text");
            startArrow = root.Q<VisualElement>("name-start-arrow");
            pawnBox = root.Q<VisualElement>("name-pawn");
            backButton = root.Q<VisualElement>("name-back");
            if (fieldBox == null || field == null || counter == null || suggestButton == null || bubble == null || startButton == null || pawnBox == null || backButton == null)
            { Debug.LogError("[ChessFight] NameHud.uxml의 이름이 NameScreen과 맞지 않습니다."); root = null; return; }
            current = this;

            var backdrop = root.Q<VisualElement>("name-backdrop");
            var picture = StreamingArt.Picture(StreamingArt.NameBackdrop);
            if (backdrop != null && picture != null) backdrop.style.backgroundImage = new StyleBackground(picture);
            root.Q<VisualElement>("name-screen")?.EnableInClassList("name-renaming", renaming);
            string now = PlayerNames.Tidy(prefill ?? "");
            SetText("name-kicker-note", renaming ? (now != "" ? "지금 이름 · " + now : "") : "나중에 언제든 바꿀 수 있어요");
            SetText("name-key-enter", renaming ? "바꾸기" : "시작");
            SetText("name-key-esc", renaming ? "돌아가기" : "설정");
            var title = root.Q<VisualElement>("name-title");
            title?.Add(SilverText(renaming ? "이름 바꾸기" : "이름을 정해 주세요", 52f, 560f, 62f));
            var rule = root.Q<VisualElement>("name-rule-silver");
            if (rule != null) rule.style.backgroundImage = new StyleBackground(MenuArt.Line(new Color(.863f, .886f, .91f, 1f), new Color(.863f, .886f, .91f, 0f)));

            pawn = new GlassPawn();
            pawnBox.Add(pawn);
            dice.generateVisualContent += PaintDice;
            startArrow.generateVisualContent += PaintArrow;
            var backArrow = root.Q<VisualElement>("name-back-arrow");
            if (backArrow != null) backArrow.generateVisualContent += PaintBackArrow;
            ChunkyButtons.Make(startButton);
            ChunkyButtons.Springy(suggestButton);
            ChunkyButtons.Springy(bubble);
            ChunkyButtons.Springy(backButton);
            foreach (var b in new[] { startButton, suggestButton, backButton }) b.focusable = false;

            field.maxLength = PlayerNames.MaxLength;
            field.selectAllOnFocus = false;
            field.selectAllOnMouseUp = false;
            field.value = prefill ?? "";
            // Enter, Tab and Esc belong to the screen: left to the field, Enter
            // gives up the focus and Tab moves it away (as in ChatBox).
            field.RegisterCallback<KeyDownEvent>(e =>
            {
                bool enter = e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.character == '\n' || e.character == '\r';
                if (!enter && e.keyCode != KeyCode.Tab && e.character != '\t' && e.keyCode != KeyCode.Escape) return;
                Swallow(e);
            }, TrickleDown.TrickleDown);
            field.RegisterCallback<NavigationMoveEvent>(Swallow, TrickleDown.TrickleDown);
            field.RegisterCallback<NavigationSubmitEvent>(Swallow, TrickleDown.TrickleDown);
            field.RegisterCallback<NavigationCancelEvent>(Swallow, TrickleDown.TrickleDown);
            field.RegisterValueChangedCallback(_ =>
            {
                if (!fromSuggestion) ShowBubble(false);
                fromSuggestion = false;
            });
            builtFrame = Time.frameCount;
            Refresh();
        }

        void Update()
        {
            if (root == null) return;
            // Laid out and drawn once: the transition may open over it.
            if (!Ready && Time.frameCount > builtFrame + 1) Ready = true;
            pawn?.MarkDirtyRepaint();
            AnimateDice();

            if (confirmAt >= 0)
            {
                if (Time.unscaledTime >= confirmAt) { confirmAt = -1; Confirmed?.Invoke(PlayerNames.Tidy(field.value)); }
                Refresh();
                return;
            }
            bool blocked = SettingsWindow.IsOpen || SceneTransition.Busy || confirming || leaving;
            if (blocked)
            {
                if (Typing && field.focusController?.focusedElement is Focusable focused) focused.Blur();
                submitFrame = -1;
                pressedOn = null;
                Refresh();
                return;
            }
            if (renaming && LegacyKeys.Down(KeyCode.Escape)) { Back(); Refresh(); return; }

            if (submitFrame >= 0 && Time.frameCount > submitFrame && (Composition() == "" || Time.frameCount > submitFrame + ImeWaitFrames))
            {
                submitFrame = -1;
                Confirm(true);
            }
            else if (submitFrame < 0 && (LegacyKeys.Down(KeyCode.Return) || LegacyKeys.Down(KeyCode.KeypadEnter)))
                submitFrame = Time.frameCount;

            Clicks();
            // The field keeps the keys: a click elsewhere takes the focus, so it is
            // handed back for the next key.
            if (!Typing && !MouseHeld()) field.Focus();
            Refresh();
        }

        void Clicks()
        {
            bool down, up;
            Vector2 mouse;
            try { down = Input.GetMouseButtonDown(0); up = Input.GetMouseButtonUp(0); mouse = Input.mousePosition; }
            catch (InvalidOperationException) { return; }
            if (!down && !up) return;
            var point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(mouse.x, Screen.height - mouse.y));
            VisualElement hit = null;
            if (Shown(bubble) && bubble.worldBound.Contains(point)) hit = bubble;
            else if (suggestButton.worldBound.Contains(point)) hit = suggestButton;
            else if (startButton.worldBound.Contains(point)) hit = startButton;
            else if (renaming && backButton.worldBound.Contains(point)) hit = backButton;
            if (down) { pressedOn = hit; return; }
            var pressed = pressedOn;
            pressedOn = null;
            if (hit == null || hit != pressed) return;
            if (hit == bubble) TakeSuggestion();
            else if (hit == suggestButton) Suggest();
            else if (hit == startButton) Confirm(false);
            else if (hit == backButton) Back();
        }

        void Suggest()
        {
            string current = Shown(bubble) ? suggestion : PlayerNames.Tidy(field.value);
            suggestion = PlayerNames.Suggest(current, random);
            bubbleText.text = suggestion;
            ShowBubble(true);
            diceTurn += 1f;
        }

        void TakeSuggestion()
        {
            if (suggestion == "") return;
            fromSuggestion = true;
            field.value = suggestion;
            ShowBubble(false);
            field.Focus();
            field.SelectRange(suggestion.Length, suggestion.Length);
        }

        // Renaming only: leaves without a change.
        void Back()
        {
            if (!renaming || confirming || leaving) return;
            leaving = true;
            ShowBubble(false);
            Cancelled?.Invoke();
        }

        void Confirm(bool byKey)
        {
            if (confirming || leaving) return;
            var verdict = PlayerNames.Check(field.value, false);
            if (!verdict.Ok) return;
            confirming = true;
            fromSuggestion = true;
            field.value = PlayerNames.Tidy(field.value);
            ShowBubble(false);
            if (byKey) ChunkyButtons.Pulse(startButton);
            confirmAt = Time.unscaledTime + ConfirmDelay;
        }

        void ShowBubble(bool show)
        {
            if (bubble == null) return;
            bubble.EnableInClassList("name-bubble-shown", show);
            if (show) bubbleShownAt = Time.unscaledTime;
        }

        // ---------- drawing ----------

        void Refresh()
        {
            string value = field.value ?? "";
            var verdict = PlayerNames.Check(value, Typing && !confirming);
            bool bad = verdict.Error != "";
            counter.text = bad ? verdict.Error : value.Length + " / " + PlayerNames.MaxLength;
            counter.EnableInClassList("name-counter-bad", bad);
            fieldBox.EnableInClassList("name-field-bad", bad);
            fieldBox.EnableInClassList("name-field-on", !bad && Typing);
            if (placeholder != null) placeholder.style.display = value.Length == 0 && Composition() == "" ? DisplayStyle.Flex : DisplayStyle.None;

            bool ok = verdict.Ok || confirming;
            startButton.SetEnabled(ok);
            startButton.style.opacity = ok ? 1f : .45f;
            if (startText != null)
                startText.text = renaming ? (confirming ? "바꿨어요! 돌아가는 중" : "이 이름으로 바꾸기")
                                          : (confirming ? "좋아요! 로비로 가는 중" : "이 이름으로 시작");
            if (startArrow != null) startArrow.style.display = confirming ? DisplayStyle.None : DisplayStyle.Flex;

            // The suggestion pops in.
            float pop = Mathf.Clamp01((Time.unscaledTime - bubbleShownAt) / .18f);
            float scale = 1f - .08f * (1f - pop) * (1f - pop);
            bubble.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }

        void AnimateDice()
        {
            if (dice == null) return;
            // A quarter turn springs past and settles, like the design's dice icon.
            diceShown = Mathf.Lerp(diceShown, diceTurn, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
            dice.style.rotate = new Rotate(new Angle(diceShown * 360f, AngleUnit.Degree));
        }

        // Text in the silver gradient: the label cut into horizontal bands, each
        // coloured from the gradient at its height (as the result screen's title).
        static VisualElement SilverText(string text, float size, float w, float h)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.position = Position.Absolute;
            box.style.left = 0; box.style.top = 0; box.style.width = w; box.style.height = h;
            const int bands = 16;
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
                label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
                label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                label.style.color = Sample(Silver, (y0 + y1) / 2f / h);
                clip.Add(label);
                box.Add(clip);
            }
            return box;
        }

        static void PaintDice(MeshGenerationContext context)
        {
            var p = context.painter2D;
            var c = new Color(227 / 255f, 211 / 255f, 184 / 255f);
            float s = 16f / 24f;
            p.strokeColor = c;
            p.lineWidth = 2f * s;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            const float r = 4f;
            float x0 = 3.5f * s, y0 = 3.5f * s, x1 = 20.5f * s, y1 = 20.5f * s, rr = r * s;
            p.MoveTo(new Vector2(x0 + rr, y0));
            p.LineTo(new Vector2(x1 - rr, y0)); p.ArcTo(new Vector2(x1, y0), new Vector2(x1, y0 + rr), rr);
            p.LineTo(new Vector2(x1, y1 - rr)); p.ArcTo(new Vector2(x1, y1), new Vector2(x1 - rr, y1), rr);
            p.LineTo(new Vector2(x0 + rr, y1)); p.ArcTo(new Vector2(x0, y1), new Vector2(x0, y1 - rr), rr);
            p.LineTo(new Vector2(x0, y0 + rr)); p.ArcTo(new Vector2(x0, y0), new Vector2(x0 + rr, y0), rr);
            p.ClosePath();
            p.Stroke();
            p.fillColor = c;
            foreach (var d in new[] { new Vector2(8.5f, 8.5f), new Vector2(12f, 12f), new Vector2(15.5f, 15.5f) })
            {
                p.BeginPath();
                p.Arc(d * s, 1.3f * s, 0f, 360f);
                p.Fill();
            }
        }

        static void PaintArrow(MeshGenerationContext context)
        {
            var p = context.painter2D;
            float s = 20f / 24f;
            p.strokeColor = new Color(36 / 255f, 23 / 255f, 13 / 255f);
            p.lineWidth = 2.4f * s;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.BeginPath(); p.MoveTo(new Vector2(5, 12) * s); p.LineTo(new Vector2(19, 12) * s); p.Stroke();
            p.BeginPath(); p.MoveTo(new Vector2(13, 6) * s); p.LineTo(new Vector2(19, 12) * s); p.LineTo(new Vector2(13, 18) * s); p.Stroke();
        }

        // The start arrow turned round, in the light text colour of "돌아가기".
        static void PaintBackArrow(MeshGenerationContext context)
        {
            var p = context.painter2D;
            float s = 16f / 24f;
            p.strokeColor = new Color(227 / 255f, 211 / 255f, 184 / 255f);
            p.lineWidth = 2.2f * s;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.BeginPath(); p.MoveTo(new Vector2(19, 12) * s); p.LineTo(new Vector2(5, 12) * s); p.Stroke();
            p.BeginPath(); p.MoveTo(new Vector2(11, 6) * s); p.LineTo(new Vector2(5, 12) * s); p.LineTo(new Vector2(11, 18) * s); p.Stroke();
        }

        // ---------- helpers ----------

        void SetText(string name, string text)
        {
            var label = root?.Q<Label>(name);
            if (label != null) label.text = text;
        }

        bool Typing
        {
            get
            {
                var focused = field?.focusController?.focusedElement as VisualElement;
                return focused != null && (focused == field || field.Contains(focused));
            }
        }

        void Swallow(EventBase e)
        {
            e.StopImmediatePropagation();
            field.focusController?.IgnoreEvent(e);
        }

        Vector2 Fraction(VisualElement element, Vector2 local)
        {
            if (element?.panel == null || root == null) return new Vector2(.5f, .5f);
            var world = element.LocalToWorld(local);
            var size = root.worldBound;
            if (size.width <= 0 || size.height <= 0) return new Vector2(.5f, .5f);
            return new Vector2((world.x - size.x) / size.width, (world.y - size.y) / size.height);
        }

        static bool Shown(VisualElement e) => e != null && e.resolvedStyle.display == DisplayStyle.Flex;

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

        static Color Hex(int rgb) => new Color((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f);

        static Color Sample((float at, Color c)[] stops, float t)
        {
            if (t <= stops[0].at) return stops[0].c;
            for (int i = 1; i < stops.Length; i++)
                if (t <= stops[i].at) return Color.Lerp(stops[i - 1].c, stops[i].c, (t - stops[i - 1].at) / Mathf.Max(1e-5f, stops[i].at - stops[i - 1].at));
            return stops[stops.Length - 1].c;
        }

        void OnDestroy()
        {
            if (current == this) current = null;
            if (ownedPanel != null) Destroy(ownedPanel);
        }
    }
}
