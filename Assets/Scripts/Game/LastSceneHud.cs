using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // The last scene's screen, in the PAWN RUSH HUD style of the reference picture
    // (Docs/Architecture/UI.md "결과 화면"): the PAWN RUSH banner and a big 승리! / 패배
    // top left; the team goal card and the finish order top right, the winners' four
    // in gold and the fourth flagged 승리 확정; who never arrived, with how far they
    // were from the line; a hide toggle bottom left; and the 12 s countdown with
    // 다시 매칭 and 로비로 bottom right.
    //
    // UI Toolkit has no skew, so the slanted plates, crowns, badges and flags are
    // painted with the vector API. Everything enters on the timeline that Tick is fed.
    [DisallowMultipleComponent]
    public sealed class LastSceneHud : MonoBehaviour
    {
        public event Action Requeue, Lobby, ToggleBoard;
        public const float LobbySeconds = 12f;

        static readonly Color Navy = PieceFigure.Hex(0x1F2B5B), NavyUpper = PieceFigure.Hex(0x2A3B7E), Edge = PieceFigure.Hex(0x0D1433);
        static readonly Color GoldFill = PieceFigure.Hex(0xE9A92C), GoldUpper = PieceFigure.Hex(0xFFD866), GoldEdge = PieceFigure.Hex(0x8A5A00);
        static readonly Color BlueFill = PieceFigure.Hex(0x2F6FE0), BlueUpper = PieceFigure.Hex(0x4C8BF0), BlueEdge = PieceFigure.Hex(0x1747A6);
        static readonly Color White = Color.white;

        PanelSettings owned;
        VisualElement root, screen, right, rowsBox, dnfBox, legend, leave, barFill, marker;
        SkewPlate title, sub, goal, hide;
        Label word, goalKicker, goalValue, goalSub, goalOther, subText, countText, toast, keys, hideText;
        readonly List<VisualElement> rows = new List<VisualElement>();
        bool win = true, boardOn = true, realPointer;
        float boardAt = 3.2f, boardShown = 1f, toastAt = -10f, lastTp;
        SkewPlate pressed, queued;
        int queuedFrame;

        public void Build(bool preview)
        {
            root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("LastSceneHud"),
                                        Resources.Load<ThemeStyleSheet>("LastSceneTheme"), null, new Vector2Int(1280, 720), out owned);
            if (root == null) return;
            screen = root.Q<VisualElement>("ls-screen");
            root.RegisterCallback<PointerDownEvent>(_ => realPointer = true, TrickleDown.TrickleDown);

            title = Plate(screen, "ls-title ls-abs", Navy, NavyUpper, Edge, 12f);
            title.Add(new CrownIcon { style = { width = 46, height = 38, marginRight = 10 } });
            title.Add(Text("PAWN RUSH", "ls-title-text"));
            word = Text("", "ls-word");
            word.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);
            screen.Add(word);
            sub = Plate(screen, "ls-sub ls-abs", Navy, NavyUpper, Edge, 9f);
            sub.Add(new PennantIcon { style = { width = 22, height = 24, marginRight = 8 } });
            subText = Text("", "ls-sub-text");
            sub.Add(subText);

            right = new VisualElement { pickingMode = PickingMode.Ignore };
            right.AddToClassList("ls-right");
            screen.Add(right);
            goal = Plate(right, "ls-goal", Navy, NavyUpper, Edge, 14f);
            goalKicker = Text("", "ls-goal-k");
            goal.Add(goalKicker);
            var value = new VisualElement { pickingMode = PickingMode.Ignore };
            value.AddToClassList("ls-goal-v");
            value.Add(new CrownIcon { style = { width = 46, height = 38 } });
            goalValue = Text("", "ls-goal-num");
            value.Add(goalValue);
            goal.Add(value);
            goalSub = Text("", "ls-goal-s");
            goalOther = Text("", "ls-goal-o");
            goal.Add(goalSub);
            goal.Add(goalOther);
            rowsBox = Box(right, "ls-rows");
            dnfBox = Box(right, null);
            legend = Box(right, "ls-legend");
            Legend(GoldFill, "승리에 필요한 인원");
            Legend(BlueFill, "백팀");
            Legend(PieceFigure.Hex(0x2E2C36), "흑팀");

            hide = Plate(screen, "ls-hint ls-abs", Navy, NavyUpper, Edge, 10f);
            hide.Add(new EyeIcon { style = { width = 30, height = 30 } });
            hideText = Text("결과판 숨기기", "ls-hint-t");
            hide.Add(hideText);
            hide.Add(Text("H", "ls-kbd"));
            hide.MakeButton(() => ToggleBoard?.Invoke());

            leave = Box(screen, "ls-leave");
            var count = Box(leave, "ls-count");
            countText = Text("", "ls-count-t");
            count.Add(countText);
            var bar = Box(count, "ls-bar");
            barFill = Box(bar, "ls-bar-fill");
            var buttons = Box(leave, "ls-row-flex");
            var again = Plate(buttons, "ls-button", Navy, NavyUpper, Edge, 10f);
            again.Add(Text("다시 매칭", "ls-button-t"));
            again.MakeButton(() => Requeue?.Invoke());
            var lobby = Plate(buttons, "ls-button", BlueFill, BlueUpper, BlueEdge, 10f);
            lobby.Add(Text("로비로", "ls-button-t"));
            lobby.MakeButton(() => Lobby?.Invoke());

            marker = Box(screen, "ls-marker");
            marker.Add(Text("나", "ls-marker-t"));
            marker.Add(new Triangle { style = { width = 22, height = 14 } });
            marker.style.display = DisplayStyle.None;

            toast = Text("", "ls-toast");
            toast.style.opacity = 0;
            screen.Add(toast);
            keys = Text("미리보기 · F1 이긴 팀 화면 · F2 진 팀 화면 · R 다시 재생 · H 결과판 숨기기", "ls-keys");
            keys.style.display = preview ? DisplayStyle.Flex : DisplayStyle.None;
            screen.Add(keys);
        }

        // Fill the screen for `result` as the player on `myTeam` sees it.
        public void Show(MatchResult result, int myTeam, float boardSeconds)
        {
            if (root == null) return;
            win = result.WinningTeam == myTeam;
            bool draw = result.WinningTeam < 0;
            boardAt = boardSeconds;
            word.text = draw ? "무승부" : win ? "승리!" : "패배";
            word.EnableInClassList("lose", !win);
            string winner = result.WinningTeam == 0 ? "백팀" : "흑팀";
            subText.text = draw ? "두 팀 모두 결승 인원을 채우지 못했어요"
                         : win ? winner + " " + result.Needed + "명이 먼저 결승선을 넘었어요"
                         : winner + "이 먼저 " + result.Needed + "명 결승선을 넘었어요";
            string mine = myTeam == 0 ? "백팀" : "흑팀", theirs = myTeam == 0 ? "흑팀" : "백팀";
            goalKicker.text = mine + " · 팀 목표";
            goalValue.text = result.GoalCount(myTeam) + " / " + result.Needed;
            goalValue.style.color = win ? (StyleColor)PieceFigure.Hex(0xF6C445) : (StyleColor)PieceFigure.Hex(0xC3CEDF);
            goalSub.text = "결승선 통과 · " + MatchResult.Clock(result.MatchSeconds);
            int other = 1 - myTeam;
            goalOther.text = theirs + " " + result.GoalCount(other) + " / " + result.Needed + (result.WinningTeam == other ? " 먼저 달성" : "");

            rowsBox.Clear();
            rows.Clear();
            foreach (var line in result.FinishOrder()) rows.Add(Row(line, result.Needed));
            dnfBox.Clear();
            for (int team = 0; team < 2; team++)
            {
                var missing = result.NotFinished(team);
                if (missing.Count == 0) continue;
                var box = Box(dnfBox, "ls-dnf");
                box.Add(Text((team == 0 ? "백팀" : "흑팀") + " 미도착 · 결승까지", "ls-dnf-k"));
                foreach (var p in missing)
                {
                    box.Add(Text(p.Name + (p.Self ? " (나)" : ""), "ls-dnf-p"));
                    box.Add(Text(MatchResult.Meters(p.MetersLeft), "ls-dnf-m"));
                }
            }
            Tick(0f);
        }

        VisualElement Row(FinishLine line, int needed)
        {
            var p = line.Player;
            var plate = line.CountsToWin ? Plate(rowsBox, "ls-rank need", GoldFill, GoldUpper, GoldEdge, 9f) : Plate(rowsBox, "ls-rank", Navy, NavyUpper, Edge, 9f);
            plate.BlockWidth = 38f;
            plate.Add(Text(line.Place.ToString(), "ls-no"));
            plate.Add(new HexBadge(p.Team) { style = { width = 30, height = 28, flexShrink = 0 } });
            plate.Add(Text(p.Name, "ls-name"));
            if (p.Self) plate.Add(Text("나", "ls-tag me"));
            string tag = line.Clinched ? "승리 확정" : line.CountsToWin ? "승리 " + line.TeamPlace + "/" + needed : (p.Team == 0 ? "백 · " : "흑 · ") + PieceName(p.Piece);
            plate.Add(Text(tag, line.CountsToWin ? "ls-tag need" : "ls-tag"));
            if (line.Clinched) plate.Add(new FlagIcon { style = { width = 14, height = 14, marginLeft = 8, flexShrink = 0 } });
            plate.Add(Text(MatchResult.Clock(p.FinishSeconds), "ls-time"));
            return plate;
        }

        static string PieceName(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.King: return "킹";
                case PieceKind.Queen: return "퀸";
                case PieceKind.Rook: return "룩";
                case PieceKind.Bishop: return "비숍";
                case PieceKind.Knight: return "나이트";
                default: return "폰";
            }
        }

        public void SetBoard(bool on)
        {
            boardOn = on;
            if (hideText != null) hideText.text = on ? "결과판 숨기기" : "결과판 보기";
        }

        public void Toast(string text)
        {
            if (toast == null) return;
            toast.text = text;
            toastAt = lastTp;
        }

        // Where my piece's head is, for the marker above it.
        public void PlaceMarker(Vector3 world, Camera camera, bool show)
        {
            if (marker == null || root?.panel == null) return;
            bool visible = show && camera != null && camera.WorldToViewportPoint(world).z > 0;
            marker.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, camera);
            marker.style.translate = new Translate(p.x - 22f, p.y - 40f, 0);
        }

        // `tp`: seconds since the result began; the layout's whole entrance runs off it.
        public void Tick(float tp)
        {
            if (root == null) return;
            float dt = Mathf.Clamp(tp - lastTp, 0f, .1f);
            lastTp = tp;
            Slide(title, tp, .2f, -120f, 0f);
            Word(tp);
            Slide(sub, tp, boardAt + .1f, -120f, 0f);
            Drop(goal, tp, boardAt);
            for (int i = 0; i < rows.Count; i++) Slide(rows[i], tp, boardAt + .35f + i * .09f, 90f, .15f);
            Fade(dnfBox, tp, boardAt + 1f);
            Fade(legend, tp, boardAt + 1f);
            Fade(hide, tp, boardAt + .7f);
            Fade(leave, tp, boardAt + .7f);
            float left = Mathf.Clamp(LobbySeconds - (tp - boardAt), 0f, LobbySeconds);
            barFill.style.width = Length.Percent(tp < boardAt ? 100f : left / LobbySeconds * 100f);
            countText.text = Mathf.CeilToInt(left) + "초 뒤 자동으로 로비로";
            // The hide toggle slides the board out to the right.
            boardShown = Mathf.MoveTowards(boardShown, boardOn ? 1f : 0f, dt * 3.5f);
            float k = boardShown * boardShown * (3 - 2 * boardShown);
            right.style.translate = new Translate((1 - k) * 380f, 0, 0);
            right.style.opacity = k;
            leave.style.translate = new Translate((1 - k) * 380f, 0, 0);
            if (toast != null) toast.style.opacity = Mathf.Clamp01(1.6f - (tp - toastAt) * .5f);
        }

        // The big word lands in the middle, holds, then flies into place top left.
        void Word(float tp)
        {
            float start = win ? .45f : .9f, length = win ? 3f : 3.3f, u = (tp - start) / length;
            if (u < 0) { word.style.opacity = 0; return; }
            float opacity, scale, x, y, turn = 0;
            if (win)
            {
                opacity = Key(u, new[] { 0f, .12f }, new[] { 0f, 1f });
                scale = Key(u, new[] { 0f, .12f, .17f, .22f, .78f, 1f }, new[] { 2.6f, 1.9f, 2.08f, 2f, 2f, 1f });
                x = Key(u, new[] { .78f, 1f }, new[] { 450f, 0f });
                y = Key(u, new[] { .78f, 1f }, new[] { 190f, 0f });
            }
            else
            {
                opacity = Key(u, new[] { 0f, .18f }, new[] { 0f, 1f });
                scale = Key(u, new[] { .8f, 1f }, new[] { 2f, 1f });
                x = Key(u, new[] { .8f, 1f }, new[] { 450f, 0f });
                y = Key(u, new[] { 0f, .18f, .26f, .8f, 1f }, new[] { 60f, 205f, 190f, 190f, 0f });
                turn = Key(u, new[] { 0f, .18f, .26f, .32f }, new[] { -5f, 2f, -1f, 0f });
            }
            word.style.opacity = opacity;
            word.style.translate = new Translate(x, y, 0);
            word.style.scale = new Scale(new Vector3(scale, scale, 1));
            word.style.rotate = new Rotate(turn);
        }

        // Piecewise eased keyframes, held at both ends.
        static float Key(float u, float[] times, float[] values)
        {
            if (u <= times[0]) return values[0];
            for (int i = 1; i < times.Length; i++)
                if (u <= times[i])
                {
                    float k = (u - times[i - 1]) / Mathf.Max(1e-5f, times[i] - times[i - 1]);
                    k = k * k * (3 - 2 * k);
                    return Mathf.Lerp(values[i - 1], values[i], k);
                }
            return values[values.Length - 1];
        }

        static void Slide(VisualElement e, float tp, float at, float from, float overshoot)
        {
            float u = Mathf.Clamp01((tp - at) / .45f);
            float k = 1 - Mathf.Pow(1 - u, 3);
            float back = overshoot > 0 ? Mathf.Sin(u * Mathf.PI) * overshoot * 40f : 0f;
            e.style.opacity = Mathf.Clamp01(u * 2.5f);
            e.style.translate = new Translate(from * (1 - k) - back * Mathf.Sign(from), 0, 0);
        }

        static void Drop(VisualElement e, float tp, float at)
        {
            float u = Mathf.Clamp01((tp - at) / .5f), k = 1 - Mathf.Pow(1 - u, 3);
            e.style.opacity = Mathf.Clamp01(u * 2f);
            e.style.translate = new Translate(0, -60f * (1 - k) + Mathf.Sin(u * Mathf.PI) * 6f, 0);
        }

        static void Fade(VisualElement e, float tp, float at) => e.style.opacity = Mathf.Clamp01((tp - at) / .4f);

        // The same safety net as the other HUDs (Docs/Architecture/UI.md §2): with no real
        // pointer event yet, press the button under the mouse a frame later.
        void LateUpdate()
        {
            if (queued != null && Time.frameCount > queuedFrame)
            {
                var button = queued;
                queued = null;
                if (!realPointer) button.Click();
            }
            if (realPointer || root?.panel == null) return;
            Vector2 point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
            var hit = root.panel.Pick(point);
            SkewPlate plate = null;
            for (var e = hit; e != null; e = e.parent) if (e is SkewPlate s && s.IsButton) { plate = s; break; }
            if (Input.GetMouseButtonDown(0)) pressed = plate;
            if (Input.GetMouseButtonUp(0))
            {
                if (pressed != null && plate == pressed) { queued = pressed; queuedFrame = Time.frameCount; }
                pressed = null;
            }
        }

        void OnDestroy()
        {
            if (owned != null) Destroy(owned);
        }

        // ---------- building blocks ----------

        static Label Text(string text, string classes)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("ls-text");
            foreach (var c in classes.Split(' ')) if (c.Length > 0) label.AddToClassList(c);
            return label;
        }

        static VisualElement Box(VisualElement parent, string classes)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            if (classes != null) foreach (var c in classes.Split(' ')) box.AddToClassList(c);
            parent.Add(box);
            return box;
        }

        static SkewPlate Plate(VisualElement parent, string classes, Color fill, Color upper, Color edge, float skew)
        {
            var plate = new SkewPlate(fill, upper, edge, skew);
            foreach (var c in classes.Split(' ')) plate.AddToClassList(c);
            parent.Add(plate);
            return plate;
        }

        void Legend(Color swatch, string text)
        {
            var item = Box(legend, "ls-legend-i");
            var chip = Box(item, "ls-swatch");
            chip.style.backgroundColor = swatch;
            item.Add(Text(text, "ls-legend-t"));
        }

        // ---------- painted parts ----------

        // A slanted plate: an edge shadow below, the fill, a lighter upper band, and
        // optionally a darker block on the left for a rank number. Can be a button.
        sealed class SkewPlate : VisualElement
        {
            readonly Color fill, upper, edge;
            readonly float skew;
            public float BlockWidth;
            Action clicked;
            public bool IsButton => clicked != null;

            public SkewPlate(Color fill, Color upper, Color edge, float skew)
            {
                this.fill = fill; this.upper = upper; this.edge = edge; this.skew = skew;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Paint;
            }

            public void MakeButton(Action onClick)
            {
                clicked = onClick;
                pickingMode = PickingMode.Position;
                focusable = false;
                this.AddManipulator(new Clickable(() => clicked?.Invoke()));
            }

            public void Click() => clicked?.Invoke();

            void Paint(MeshGenerationContext context)
            {
                float w = layout.width, h = layout.height;
                if (float.IsNaN(w) || float.IsNaN(h) || w < 2 || h < 2) return;
                var painter = context.painter2D;
                Shape(painter, w, h, 4f, edge);
                Shape(painter, w, h, 0f, fill);
                float band = h * .46f, inset = skew * band / h;
                painter.fillColor = upper;
                painter.BeginPath();
                painter.MoveTo(new Vector2(skew, 0));
                painter.LineTo(new Vector2(w, 0));
                painter.LineTo(new Vector2(w - inset, band));
                painter.LineTo(new Vector2(skew - inset, band));
                painter.ClosePath();
                painter.Fill();
                if (BlockWidth > 0)
                {
                    painter.fillColor = new Color(0, 0, 0, .22f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(skew, 0));
                    painter.LineTo(new Vector2(skew + BlockWidth, 0));
                    painter.LineTo(new Vector2(BlockWidth, h));
                    painter.LineTo(new Vector2(0, h));
                    painter.ClosePath();
                    painter.Fill();
                }
            }

            void Shape(Painter2D painter, float w, float h, float dy, Color color)
            {
                painter.fillColor = color;
                painter.BeginPath();
                painter.MoveTo(new Vector2(skew, dy));
                painter.LineTo(new Vector2(w, dy));
                painter.LineTo(new Vector2(w - skew, h + dy));
                painter.LineTo(new Vector2(0, h + dy));
                painter.ClosePath();
                painter.Fill();
            }
        }

        // The crown from the reference HUD: gold with a dark outline.
        sealed class CrownIcon : VisualElement
        {
            public CrownIcon() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
            void Draw(MeshGenerationContext context) =>
                PaintCrown(context.painter2D, contentRect, PieceFigure.Hex(0xF6C445), PieceFigure.Hex(0x7A4E00), 1f);
        }

        // A team's hexagon badge with a crown: blue for white, charcoal for black.
        sealed class HexBadge : VisualElement
        {
            readonly int team;
            public HexBadge(int team) { this.team = team; pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
            void Draw(MeshGenerationContext context)
            {
                var r = contentRect;
                if (r.width < 2) return;
                var painter = context.painter2D;
                painter.fillColor = team == 0 ? BlueFill : PieceFigure.Hex(0x2E2C36);
                painter.BeginPath();
                float w = r.width, h = r.height;
                painter.MoveTo(new Vector2(w * .25f, h * .03f));
                painter.LineTo(new Vector2(w * .75f, h * .03f));
                painter.LineTo(new Vector2(w * .99f, h * .5f));
                painter.LineTo(new Vector2(w * .75f, h * .97f));
                painter.LineTo(new Vector2(w * .25f, h * .97f));
                painter.LineTo(new Vector2(w * .01f, h * .5f));
                painter.ClosePath();
                painter.Fill();
                var inner = new Rect(r.x + w * .17f, r.y + h * .2f, w * .66f, h * .6f);
                PaintCrown(painter, inner, team == 0 ? Color.white : PieceFigure.Hex(0xF6C445), team == 0 ? PieceFigure.Hex(0x123C8F) : PieceFigure.Hex(0x7A4E00), .8f);
            }
        }

        static readonly Vector2[] CrownPoints =
        {
            new Vector2(-11, 8), new Vector2(-12, -5), new Vector2(-5, 0), new Vector2(0, -10), new Vector2(5, 0), new Vector2(12, -5), new Vector2(11, 8)
        };

        static void PaintCrown(Painter2D painter, Rect rect, Color fill, Color outline, float lineWidth)
        {
            if (rect.width <= 0 || rect.height <= 0) return;
            float scale = Mathf.Min(rect.width / 26f, rect.height / 20f);
            var center = rect.center + new Vector2(0, scale);
            painter.lineJoin = LineJoin.Round;
            painter.lineWidth = lineWidth * Mathf.Max(1f, scale * .8f);
            painter.BeginPath();
            for (int i = 0; i < CrownPoints.Length; i++)
            {
                var point = center + CrownPoints[i] * scale;
                if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
            }
            painter.ClosePath();
            painter.fillColor = fill;
            painter.Fill();
            painter.strokeColor = outline;
            painter.Stroke();
        }

        // The checkered flag beside the clinching time.
        sealed class FlagIcon : VisualElement
        {
            public FlagIcon() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
            void Draw(MeshGenerationContext context)
            {
                var r = contentRect;
                float q = r.width / 4f;
                var painter = context.painter2D;
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 4; j++)
                    {
                        painter.fillColor = (i + j) % 2 == 0 ? Color.white : PieceFigure.Hex(0x1F2433);
                        painter.BeginPath();
                        painter.MoveTo(new Vector2(r.x + i * q, r.y + j * q));
                        painter.LineTo(new Vector2(r.x + (i + 1) * q, r.y + j * q));
                        painter.LineTo(new Vector2(r.x + (i + 1) * q, r.y + (j + 1) * q));
                        painter.LineTo(new Vector2(r.x + i * q, r.y + (j + 1) * q));
                        painter.ClosePath();
                        painter.Fill();
                    }
            }
        }

        // A small gold flag on a pole, for the result line.
        sealed class PennantIcon : VisualElement
        {
            public PennantIcon() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
            void Draw(MeshGenerationContext context)
            {
                var r = contentRect;
                var painter = context.painter2D;
                painter.strokeColor = Color.white;
                painter.lineWidth = 2.4f;
                painter.lineCap = LineCap.Round;
                painter.BeginPath();
                painter.MoveTo(new Vector2(r.x + r.width * .22f, r.y + r.height * .1f));
                painter.LineTo(new Vector2(r.x + r.width * .22f, r.y + r.height * .92f));
                painter.Stroke();
                painter.fillColor = PieceFigure.Hex(0xF6C445);
                painter.strokeColor = PieceFigure.Hex(0x7A4E00);
                painter.lineWidth = 1.2f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(r.x + r.width * .27f, r.y + r.height * .14f));
                painter.LineTo(new Vector2(r.x + r.width * .95f, r.y + r.height * .14f));
                painter.LineTo(new Vector2(r.x + r.width * .78f, r.y + r.height * .32f));
                painter.LineTo(new Vector2(r.x + r.width * .95f, r.y + r.height * .5f));
                painter.LineTo(new Vector2(r.x + r.width * .27f, r.y + r.height * .5f));
                painter.ClosePath();
                painter.Fill();
                painter.Stroke();
            }
        }

        // An eye for the hide toggle: an outline and a gold pupil.
        sealed class EyeIcon : VisualElement
        {
            public EyeIcon() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
            void Draw(MeshGenerationContext context)
            {
                var r = contentRect;
                var c = r.center;
                var painter = context.painter2D;
                painter.strokeColor = Color.white;
                painter.lineWidth = 2.4f;
                painter.BeginPath();
                for (int i = 0; i <= 32; i++)
                {
                    float a = i / 32f * Mathf.PI * 2;
                    var p = c + new Vector2(Mathf.Cos(a) * r.width * .4f, Mathf.Sin(a) * r.height * .25f);
                    if (i == 0) painter.MoveTo(p); else painter.LineTo(p);
                }
                painter.Stroke();
                painter.fillColor = PieceFigure.Hex(0xF6C445);
                painter.BeginPath();
                for (int i = 0; i <= 16; i++)
                {
                    float a = i / 16f * Mathf.PI * 2;
                    var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r.height * .12f;
                    if (i == 0) painter.MoveTo(p); else painter.LineTo(p);
                }
                painter.ClosePath();
                painter.Fill();
            }
        }

        // The marker's pointer.
        sealed class Triangle : VisualElement
        {
            public Triangle() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
            void Draw(MeshGenerationContext context)
            {
                var r = contentRect;
                var painter = context.painter2D;
                painter.fillColor = PieceFigure.Hex(0x0F6F93);
                Tri(painter, r, 2f);
                painter.fillColor = PieceFigure.Hex(0x3CC8F5);
                Tri(painter, r, 0f);
            }
            static void Tri(Painter2D painter, Rect r, float dy)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(r.x, r.y + dy));
                painter.LineTo(new Vector2(r.xMax, r.y + dy));
                painter.LineTo(new Vector2(r.center.x, r.yMax + dy));
                painter.ClosePath();
                painter.Fill();
            }
        }
    }
}
