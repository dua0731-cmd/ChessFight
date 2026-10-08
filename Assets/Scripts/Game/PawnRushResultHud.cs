using System;
using System.Collections.Generic;
using System.Linq;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // The Pawn Rush result screen's broadcast package ("결승 중계", Docs/Architecture/UI.md §13),
    // a port of the approved review page (screenBC and the .bc-* rules): a walnut-and-brass
    // wipe, the result word slamming in and docking above the scoreboard, the finish order
    // along the top as one chip per finisher, the two teams' tally at the bottom, a crawl
    // bar with who never arrived, the 12 s countdown and the two buttons. H hides the
    // package and leaves a small score bug.
    //
    // Everything is laid out on the page's 1280 x 720 stage. The page's gradients, wood
    // stripes and clip-paths are painted with Painter2D (fill gradients, polygons cut to the
    // same shapes); its keyframe animations run off the result time `tp` and its transitions
    // (classes changing) run on real time, so a held moment still settles.
    [DisallowMultipleComponent]
    public sealed class PawnRushResultHud : MonoBehaviour
    {
        public event Action Requeue, Lobby, ToggleBoard;
        public const float LobbySeconds = 12f;

        // The page's TIMES: the word slams in (p1), docks (p2), the package comes in (p3, when
        // the stage's camera eases back), the crawl bar fills in (p4).
        static readonly float[] WinTimes = { .45f, 2.9f, 3.2f, 4.3f }, LoseTimes = { .9f, 3.2f, 3.8f, 4.9f };
        // A chip lands .45 s after p3 and every .24 s after that (T0 and BEAT in screenBC).
        const float ChipFirst = .45f, ChipStep = .24f;

        static Color Hex(int rgb, float a = 1f) { var c = PieceFigure.Hex(rgb); c.a = a; return c; }
        static Color Rgba(int r, int g, int b, float a) => new Color(r / 255f, g / 255f, b / 255f, a);
        static Vector2 V(float x, float y) => new Vector2(x, y);

        PanelSettings owned;
        VisualElement root, screen, ovc, cold, vig, tick, sb, foot, crawl, flash, metag, wipeBox, ttl, ttlIn, rays, word, mini, toastBox;
        Shape scan, shine, countFill, wipe;
        Label keys, toast, sub, countText, hideText;
        Button hide;
        readonly List<(VisualElement e, float at)> chips = new List<(VisualElement, float)>();
        readonly List<(VisualElement face, VisualElement ring, bool dec, float at)> slots = new List<(VisualElement, VisualElement, bool, float)>();
        readonly List<(VisualElement e, float inAt, float outAt)> digits = new List<(VisualElement, float, float)>();
        readonly List<VisualElement> lostCrowns = new List<VisualElement>(), lostFaces = new List<VisualElement>();
        readonly List<(Label label, Color color)> lostLabels = new List<(Label, Color)>();
        readonly List<Shape> lostShapes = new List<Shape>();
        // The losers' half after the decision: the page's filter saturate(.35) brightness(.8), 0..1.
        float lostFilter;
        VisualElement wonWash, lostWash, decGlow;
        readonly Dictionary<Button, Action> actions = new Dictionary<Button, Action>();
        bool preview, win = true, boardOn = true, realPointer, built;
        float[] times = WinTimes;
        float decisiveAt, toastAt = -100f, shineX = -170f;
        int phase = -1;
        Font mono;
        Button pressed;
        Vector2 pressedPoint;
        int pressedFrame = -1;

        // Transitions, on real time (the page's transition rules).
        readonly Tween brdMove = new Tween(0, .62f, Ease.Board), brdFade = new Tween(0, .3f, Ease.Css);
        readonly Tween ttlMove = new Tween(0, .62f, Ease.Dock), ttlHide = new Tween(0, .62f, Ease.Dock), ttlFade = new Tween(0, .12f, Ease.Css);
        readonly Tween raysFade = new Tween(0, .5f, Ease.Css), xtraFade = new Tween(0, .45f, Ease.Css), chipFade = new Tween(0, .3f, Ease.Css);
        readonly Tween subFade = new Tween(0, .35f, Ease.Css, .12f), subMove = new Tween(0, .45f, Ease.Pop, .12f);
        readonly Tween miniFade = new Tween(0, .3f, Ease.Css), miniMove = new Tween(0, .4f, Ease.Mini);
        readonly Tween coldFade = new Tween(0, 1.6f, Ease.Css), tagFade = new Tween(0, .35f, Ease.Css);

        public void Build(bool preview)
        {
            this.preview = preview;
            root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("PawnRushResultHud"),
                                        Resources.Load<ThemeStyleSheet>("PawnRushResultTheme"), null, new Vector2Int(1280, 720), out owned);
            if (root == null) return;
            screen = root.Q<VisualElement>("pr-screen");
            root.RegisterCallback<PointerDownEvent>(_ => realPointer = true, TrickleDown.TrickleDown);
            // The page's ui-monospace / Cascadia Mono / Consolas, whichever this PC has. A font
            // asked for by a name the OS lacks makes UI Toolkit draw nothing (or other text).
            try
            {
                var installed = Font.GetOSInstalledFontNames();
                string name = new[] { "Consolas", "Cascadia Mono", "Lucida Console" }.FirstOrDefault(n => installed.Contains(n));
                mono = name != null ? Font.CreateDynamicFontFromOSFont(name, 16) : null;
            }
            catch (Exception) { mono = null; }
        }

        // Fill the screen for `result` as the player on `myTeam` sees it; `boardSeconds` is p3,
        // when the package comes in (the stage's camera eases back at the same moment).
        public void Show(MatchResult result, int myTeam, bool victory, float boardSeconds)
        {
            if (root == null) return;
            win = victory;
            times = (float[])(win ? WinTimes : LoseTimes).Clone();
            times[2] = boardSeconds;
            screen.Clear();
            chips.Clear(); slots.Clear(); digits.Clear(); lostCrowns.Clear(); lostFaces.Clear(); lostLabels.Clear(); lostShapes.Clear(); actions.Clear();
            lostFilter = 0;
            wonWash = lostWash = decGlow = null;
            shine = null;
            phase = -1;

            var order = result.FinishOrder();
            int need = result.Needed, winner = result.WinningTeam;
            int dec = order.FindIndex(o => o.Clinched);
            decisiveAt = ChipAt(dec >= 0 ? dec : order.Count);

            // The page's layers, back to front: the vignette over the 3D, the cold wash on a
            // defeat, the package (in .bc-root's stacking order), the 나 tag, the toast.
            ovc = Full(Overlays.Vignette());
            screen.Add(ovc);
            cold = Full(Overlays.Cold());
            cold.style.opacity = 0;
            screen.Add(cold);
            vig = new Shape(PaintVig);
            Abs(vig, 0, 0, 1280, 720);
            vig.style.opacity = 0;
            screen.Add(vig);
            BuildRibbon(order, need);
            BuildScoreboard(result, order, myTeam, need, winner);
            BuildFoot(result);
            BuildHideChip();
            flash = null;
            if (win)
            {
                flash = Full(Overlays.Flash());
                flash.style.opacity = 0;
                screen.Add(flash);
            }
            BuildTag();
            BuildWipe();
            BuildWord(winner);
            BuildMini(result);
            if (preview)
            {
                keys = Text("미리보기 · R 다시 재생 · H 결과판 숨기기", 12f, Hex(0xC9B79F, .8f));
                Abs(keys, 22, 96, 400, 18);
                screen.Add(keys);
            }
            toastBox = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(toastBox, 0, 0, 1280, 720);
            toastBox.style.alignItems = Align.Center;
            toastBox.style.justifyContent = Justify.Center;
            toast = Text("", 16f, Hex(0xF6ECDF));
            toast.style.whiteSpace = WhiteSpace.Normal;
            toast.style.maxWidth = 520;
            toast.style.unityTextAlign = TextAnchor.MiddleCenter;
            toast.style.paddingTop = toast.style.paddingBottom = 14;
            toast.style.paddingLeft = toast.style.paddingRight = 20;
            toast.style.backgroundColor = Rgba(26, 16, 9, .94f);
            Border(toast, 1f, Rgba(217, 174, 98, .5f));
            toast.style.opacity = 0;
            toastBox.Add(toast);
            screen.Add(toastBox);
            built = true;
            Tick(0f);
        }

        float ChipAt(int i) => times[2] + ChipFirst + i * ChipStep;

        // ---------- the ribbon: the finish order, one chip per finisher ----------

        void BuildRibbon(List<FinishLine> order, int need)
        {
            tick = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(tick, 22, 14, 1236, 76);
            tick.style.opacity = 0;
            screen.Add(tick);
            var label = new Shape((p, w, h) =>
            {
                var shape = new[] { V(12, 0), V(w, 0), V(w - 8, h), V(0, h), V(0, 12) };
                Paint.Fill(p, shape, Paint.Linear(135f, w, h, (0, Hex(0x7A5420)), (.35f, Hex(0xE9C57A)), (.5f, Hex(0xFFF0C2)), (.65f, Hex(0xC99A4A)), (1, Hex(0x8A6224))));
            });
            Abs(label, 0, 16, 72, 60);
            var l1 = Display("결승", 18f, Hex(0x2A1A00));
            var l2 = Display("순서", 18f, Hex(0x2A1A00));
            Abs(l1, 0, 9, 72, 20);
            Abs(l2, 0, 31, 72, 20);
            l1.style.unityTextAlign = l2.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.Add(l1);
            label.Add(l2);
            tick.Add(label);

            float grow = order.Sum(o => o.Clinched ? 1.22f : 1f), free = 1236f - 72f - 6f * order.Count, x = 78f;
            for (int i = 0; i < order.Count; i++)
            {
                var line = order[i];
                float w = free * (line.Clinched ? 1.22f : 1f) / Mathf.Max(1f, grow);
                var chip = Chip(line, need, w);
                Abs(chip, x, 16, w, 60);
                chip.style.opacity = 0;
                tick.Add(chip);
                chips.Add((chip, ChipAt(i)));
                x += w + 6f;
            }

            // A light sweeping along the ribbon as the chips land.
            scan = new Shape((p, w, h) =>
            {
                var bar = Paint.Rect(0, 0, w, h);
                Paint.Glow(p, bar, Rgba(255, 196, 70, .55f), 10f);
                Paint.Fill(p, bar, Paint.Linear(180f, w, h, (0, Rgba(255, 224, 122, 0)), (.3f, Hex(0xFFF6D8)), (.7f, Hex(0xFFE07A)), (1, Rgba(255, 224, 122, 0))));
            });
            Abs(scan, 76, 12, 4, 70);
            scan.style.opacity = 0;
            tick.Add(scan);
        }

        VisualElement Chip(FinishLine line, int need, float w)
        {
            var player = line.Player;
            bool counts = line.CountsToWin, dec = line.Clinched, me = player.Self;
            Color tc = player.Team == 0 ? Hex(0x3CD0FF) : Hex(0xFF4D62);
            var chip = new Shape((p, cw, ch) =>
            {
                var shape = new[] { V(8, 0), V(cw, 0), V(cw - 8, ch), V(0, ch) };
                if (me)
                {
                    Paint.Glow(p, shape, Rgba(127, 223, 255, .55f), 7f);
                    Paint.Glow(p, shape, Hex(0x9BE8FF), 1.5f);
                }
                if (dec) Paint.Fill(p, shape, Paint.Linear(180f, cw, ch, (0, Hex(0xFFE48A)), (1, Hex(0xF2B94A))));
                else
                {
                    Paint.Fill(p, shape, counts ? Paint.Linear(180f, cw, ch, (0, Rgba(80, 52, 30, .97f)), (1, Rgba(40, 26, 15, .95f)))
                                                : Paint.Linear(180f, cw, ch, (0, Rgba(74, 48, 28, .96f)), (1, Rgba(38, 24, 14, .95f))));
                    Paint.Grain(p, shape, cw, ch);
                    Paint.Fill(p, shape, counts ? Paint.Linear(180f, cw, ch, (0, Rgba(255, 201, 61, .16f)), (.45f, Rgba(255, 201, 61, 0)))
                                                : Paint.Linear(180f, cw, ch, (0, Rgba(255, 230, 190, .08f)), (.4f, Rgba(255, 230, 190, 0))));
                    if (counts) Paint.Fill(p, Paint.Cut(shape, 0, 2), Hex(0xFFC93D));
                }
                // the order block, cut on the chip's slant
                var block = new[] { V(8, 0), V(30, 0), V(30, ch - 3), V(0, ch - 3) };
                if (me) Paint.Fill(p, block, Paint.Linear(180f, 30, ch - 3, (0, Hex(0x9BE8FF)), (1, Hex(0x2FA8E0))));
                else Paint.Fill(p, block, dec ? Rgba(122, 74, 0, .16f) : Rgba(0, 0, 0, .3f));
                // the team underline and its glow
                var under = Paint.Rect(3, ch - 3, cw - 14, 3);
                Paint.Glow(p, under, new Color(tc.r, tc.g, tc.b, .5f), 8f);
                Paint.Fill(p, under, tc);
            });
            chip.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            var no = Display(line.Place.ToString(), 22f, me ? Hex(0x06253A) : dec ? Hex(0x7A4A00) : Hex(0xE9C57A));
            Abs(no, 0, 0, 30, 54);
            no.style.unityTextAlign = TextAnchor.MiddleCenter;
            chip.Add(no);
            var face = Portrait(player.Piece, player.Team);
            Abs(face, 39, 10.5f, 36, 36);
            chip.Add(face);
            var who = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(who, 84, 10.5f, Mathf.Max(10f, w - 96f), 38);
            var top = Row();
            var name = Display(player.Name, 17f, dec ? Hex(0x2A1A00) : Hex(0xFFF3E2));
            name.style.overflow = Overflow.Hidden;
            name.style.textOverflow = TextOverflow.Ellipsis;
            name.style.flexShrink = 1;
            top.Add(name);
            if (me) { var tag = MeChip(); tag.style.marginLeft = 5; top.Add(tag); }
            who.Add(top);
            var bottom = Row();
            bottom.style.marginTop = 5;
            Color em = dec ? Hex(0x6B4A10) : Hex(0xCDBBA2);
            bottom.Add(Mono(MatchResult.Clock(player.FinishSeconds), 12.5f, em));
            if (!counts) bottom.Add(Text(" · " + (player.Team == 0 ? "백팀" : "흑팀"), 11.5f, em));
            who.Add(bottom);
            chip.Add(who);
            if (counts) chip.Add(Tab(dec ? "승리 확정" : "승리 " + line.TeamPlace + "/" + need, dec));
            return chip;
        }

        // The little plate above a counted chip: "승리 n/4", or "승리 확정" with a checker flag.
        static VisualElement Tab(string text, bool dec)
        {
            float h = dec ? 19f : 17f;
            var tab = new Shape((p, w, hh) =>
            {
                var shape = new[] { V(6, 0), V(w - 6, 0), V(w, hh), V(0, hh) };
                Paint.Fill(p, shape, dec ? Paint.Linear(180f, w, hh, (0, Hex(0x5A3A22)), (1, Hex(0x2E1D11)))
                                         : Paint.Linear(180f, w, hh, (0, Hex(0xFFE7A0)), (1, Hex(0xF2B94A))));
                if (dec) Paint.Fill(p, Paint.Cut(shape, 0, 2), Hex(0xFFC93D));
            });
            tab.style.position = Position.Absolute;
            tab.style.left = 36;
            tab.style.top = -h;
            tab.style.height = h;
            tab.style.flexDirection = FlexDirection.Row;
            tab.style.alignItems = Align.Center;
            tab.style.paddingLeft = tab.style.paddingRight = 9;
            if (dec)
            {
                var flag = new Shape(PaintChecker);
                flag.style.width = flag.style.height = 11;
                flag.style.marginRight = 4;
                tab.Add(flag);
            }
            tab.Add(Text(text, dec ? 12f : 11f, dec ? Hex(0xFFE7A0) : Hex(0x3A2400), true));
            return tab;
        }

        // ---------- the scoreboard ----------

        void BuildScoreboard(MatchResult result, List<FinishLine> order, int myTeam, int need, int winner)
        {
            sb = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(sb, 240, 498, 800, 148);
            sb.style.opacity = 0;
            screen.Add(sb);
            Color line = win ? Hex(0xFFC93D) : Hex(0xA49D94), lineHi = win ? Hex(0xFFF0C2) : Hex(0xE6E1DA);
            Color lineOff = new Color(line.r, line.g, line.b, 0);
            var plate = new Shape((p, w, h) =>
            {
                var shape = new[] { V(30, 0), V(w - 30, 0), V(w, h), V(0, h) };
                Paint.Fill(p, shape, Paint.Linear(180f, w, h, (0, Rgba(40, 26, 15, .72f)), (1, Rgba(58, 38, 22, .97f))));
                Paint.Grain(p, shape, w, h);
                Paint.Fill(p, Paint.Rect(30, 0, w - 60, 3), Paint.Linear(90f, w - 60, 3, (0, lineOff), (.22f, line), (.5f, lineHi), (.78f, line), (1, lineOff), 30, 0));
            });
            Abs(plate, 150, 0, 500, 84);
            sb.Add(plate);
            string winName = winner == 0 ? "백팀" : "흑팀";
            sub = Display(winner < 0 ? "두 팀 모두 결승 인원을 채우지 못했어요"
                          : win ? winName + " " + need + "명이 먼저 결승선을 넘었어요" : winName + "이 먼저 " + need + "명 결승선을 넘었어요", 17f, Hex(0xFFE7B8));
            Abs(sub, 0, 52, 500, 22);
            sub.style.unityTextAlign = TextAnchor.MiddleCenter;
            sub.style.opacity = 0;
            plate.Add(sub);

            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(bar, 0, 84, 800, 64);
            sb.Add(bar);
            var barShape = new[] { V(16, 0), V(784, 0), V(800, 16), V(800, 64), V(0, 64), V(0, 16) };
            bar.Add(Layer((p, w, h) =>
            {
                Paint.Fill(p, barShape, Paint.Linear(180f, w, h, (0, Hex(0x5A3A22)), (1, Hex(0x2E1D11))));
                Paint.Grain(p, barShape, w, h);
                Paint.Fill(p, Paint.Cut(barShape, 0, 2), Rgba(217, 174, 98, .6f));
            }));
            // the team washes (cyan from the left, red from the right), gold over the winners'
            var washW = Layer((p, w, h) => Paint.Fill(p, Paint.Clip(barShape, 0, 0, 400, 64), Paint.Linear(90f, 400, 64, (0, Rgba(60, 208, 255, .34f)), (.72f, Rgba(60, 208, 255, 0)))));
            var washK = Layer((p, w, h) => Paint.Fill(p, Paint.Clip(barShape, 400, 0, 400, 64), Paint.Linear(270f, 400, 64, (0, Rgba(255, 77, 98, .34f)), (.72f, Rgba(255, 77, 98, 0)), 400, 0)));
            bar.Add(washW);
            bar.Add(washK);
            if (winner >= 0)
            {
                bool left = winner == 0;
                wonWash = Layer((p, w, h) => Paint.Fill(p, Paint.Clip(barShape, left ? 0 : 400, 0, 400, 64),
                    Paint.Linear(left ? 90f : 270f, 400, 64, (0, Rgba(255, 201, 61, .5f)), (.8f, Rgba(255, 201, 61, .08f)), left ? 0 : 400, 0)));
                wonWash.style.opacity = 0;
                bar.Add(wonWash);
                lostWash = left ? washK : washW;
            }
            if (win)
            {
                shine = new Shape((p, w, h) =>
                {
                    // the light band, skewed -20 degrees, cut to the bar
                    float x = shineX, s = Mathf.Tan(20f * Mathf.Deg2Rad) * h / 2f;
                    var band = new[] { V(x + s, 0), V(x + 150 + s, 0), V(x + 150 - s, h), V(x - s, h) };
                    Paint.Fill(p, Paint.Intersect(band, barShape), Paint.Linear(100f, 150, h, (0, Rgba(255, 240, 200, 0)), (.5f, Rgba(255, 240, 200, .42f)), (1, Rgba(255, 240, 200, 0)), x, 0));
                });
                Abs(shine, 0, 0, 800, 64);
                bar.Add(shine);
            }

            for (int team = 0; team < 2; team++)
            {
                bool left = team == 0, won = team == winner, mine = team == myTeam, lost = !won && winner >= 0;
                var half = new VisualElement { pickingMode = PickingMode.Ignore };
                Abs(half, left ? 0 : 456, 0, 344, 64);
                bar.Add(half);
                var teamBox = new VisualElement { pickingMode = PickingMode.Ignore };
                Abs(teamBox, left ? 22 : 252, 12, 70, 40);
                var tn = Display(left ? "백팀" : "흑팀", 22f, left ? Hex(0xBFEFFF) : Hex(0xFFC2CA));
                var ts = Text(mine ? "우리 팀" : "상대 팀", 10.5f, mine ? Hex(0x9BE8FF) : Hex(0xB9A58C), true);
                ts.style.letterSpacing = 1f;
                ts.style.marginTop = 6;
                tn.style.unityTextAlign = ts.style.unityTextAlign = left ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
                teamBox.Add(tn);
                teamBox.Add(ts);
                half.Add(teamBox);
                if (lost) { lostLabels.Add((tn, tn.style.color.value)); lostLabels.Add((ts, ts.style.color.value)); }

                var fin = order.Where(o => o.Player.Team == team).Take(need).ToList();
                for (int k = 0; k < need; k++)
                {
                    bool has = k < fin.Count, goal = k == need - 1;
                    bool isDec = has && fin[k].Clinched, isMe = has && fin[k].Player.Self;
                    var slot = new VisualElement { pickingMode = PickingMode.Ignore };
                    Abs(slot, left ? 100 + k * 44 : 206 - k * 44, 13, 38, 38);
                    half.Add(slot);
                    if (isDec && win)
                    {
                        decGlow = new Shape((p, w, h) => Paint.Glow(p, Paint.Hexagon(0, 0, w, h), Rgba(255, 200, 80, .95f), 10f));
                        Abs(decGlow, 0, 0, 38, 38);
                        decGlow.style.opacity = 0;
                        slot.Add(decGlow);
                    }
                    bool lostGoal = goal && !won && winner >= 0;
                    Shape sock = null;
                    sock = new Shape((p, w, h) =>
                    {
                        float f = lost ? lostFilter : 0f;
                        Color F(int rgb) => Filter(Hex(rgb), f);
                        FillGradient g;
                        if (isMe) g = Paint.Linear(180f, w, h, (0, F(0xCFF4FF)), (1, F(0x2FA8E0)));
                        else if (isDec) g = Paint.Linear(180f, w, h, (0, F(0xFFFFFF)), (.45f, F(0xFFC93D)), (1, F(0xB9781A)));
                        else if (lostGoal) g = Paint.Linear(180f, w, h, (0, F(0xD8D3CC)), (.5f, F(0x8F8982)), (1, F(0x4E4944)));
                        else if (goal) g = Paint.Linear(180f, w, h, (0, F(0xFFF0C2)), (.5f, F(0xD9AE62)), (1, F(0x7A5420)));
                        else g = Paint.Linear(180f, w, h, (0, F(0xC99A4A)), (1, F(0x5A3A18)));
                        Paint.Fill(p, Paint.Hexagon(0, 0, w, h), g);
                        Paint.Fill(p, Paint.Hexagon(2, 2, w - 4, h - 4), Paint.Radial(V(w / 2, h * .35f), w * .55f, (0, F(0x2E1F14)), (1, F(0x120B06))));
                    });
                    Abs(sock, 0, 0, 38, 38);
                    slot.Add(sock);
                    if (lost) lostShapes.Add(sock);
                    if (has)
                    {
                        var face = Portrait(fin[k].Player.Piece, team);
                        Abs(face, 3, 3, 32, 32);
                        face.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
                        face.style.opacity = 0;
                        slot.Add(face);
                        if (lost)
                        {
                            // the same face with the losers' filter, faded in over it
                            var grey = Portrait(fin[k].Player.Piece, team, true);
                            Abs(grey, 0, 0, 32, 32);
                            grey.style.opacity = 0;
                            face.Add(grey);
                            lostFaces.Add(grey);
                        }
                        Color rc = isDec ? Hex(0xFFE07A) : left ? Hex(0x9BE8FF) : Hex(0xFF9AA6);
                        float rw = isDec ? 3f : 2f;
                        var ring = new Shape((p, w, h) =>
                        {
                            p.strokeColor = rc;
                            p.lineWidth = rw;
                            p.BeginPath();
                            p.Arc(V(w / 2, h / 2), w / 2 - rw / 2, new Angle(0f), new Angle(360f));
                            p.Stroke();
                        });
                        Abs(ring, 0, 0, 38, 38);
                        ring.style.opacity = 0;
                        ring.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
                        slot.Add(ring);
                        slots.Add((face, ring, isDec, ChipAt(order.IndexOf(fin[k]))));
                    }
                    if (goal)
                    {
                        Color cc = lostGoal ? Hex(0xBDB6AD) : Hex(0xFFC93D);
                        var crown = new Shape((p, w, h) =>
                        {
                            Paint.Fill(p, Paint.Offset(Paint.Crown(w, h), 0, 1f), Rgba(0, 0, 0, .45f));
                            Paint.Fill(p, Paint.Crown(w, h), cc);
                        });
                        Abs(crown, 10, -9, 18, 13);
                        crown.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));
                        slot.Add(crown);
                        if (lostGoal) lostCrowns.Add(crown);
                    }
                }

                // the rolling score: a reel of digits, each rolling in as a finisher counts
                float scoreX = left ? 282 : 18;
                var reel = new VisualElement { pickingMode = PickingMode.Ignore };
                Abs(reel, scoreX, 8, 28, 48);
                reel.style.overflow = Overflow.Hidden;
                half.Add(reel);
                int n = Mathf.Min(result.GoalCount(team), fin.Count);
                var stops = won ? new[] { (0f, Hex(0xFFFFFF)), (.5f, Hex(0xFFE7A0)), (1f, Hex(0xD2922A)) }
                                : new[] { (0f, Hex(0xF4F1EC)), (.55f, Hex(0xB9B2A9)), (1f, Hex(0x7E7770)) };
                for (int v = 0; v <= n; v++)
                {
                    var digit = GoldText(v.ToString(), 46f, 28, 48, stops, 16, false);
                    digit.style.opacity = 0;
                    reel.Add(digit);
                    float inAt = v == 0 ? times[2] : ChipAt(order.IndexOf(fin[v - 1]));
                    float outAt = v < n ? ChipAt(order.IndexOf(fin[v])) : float.MaxValue;
                    digits.Add((digit, inAt, outAt));
                }
                var of = Display("/" + need, 17f, Hex(0xB9A58C));
                Abs(of, scoreX + 29, 30, 30, 20);
                half.Add(of);
                if (lost) lostLabels.Add((of, of.style.color.value));
            }

            var mid = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(mid, 344, 0, 112, 64);
            bar.Add(mid);
            mid.Add(Layer((p, w, h) =>
            {
                Paint.Fill(p, Paint.Rect(0, 13, 1, h - 26), Rgba(217, 174, 98, .45f));
                Paint.Fill(p, Paint.Rect(w - 1, 13, 1, h - 26), Rgba(217, 174, 98, .45f));
            }));
            var ml = Text("경기 시간", 10.5f, Hex(0xC9B79F), true);
            ml.style.letterSpacing = 1.5f;
            Abs(ml, 0, 12, 112, 14);
            ml.style.unityTextAlign = TextAnchor.MiddleCenter;
            mid.Add(ml);
            var clock = Mono(MatchResult.Clock(result.MatchSeconds), 23f, Hex(0xFFE7B8));
            Abs(clock, 0, 29, 112, 26);
            clock.style.unityTextAlign = TextAnchor.MiddleCenter;
            mid.Add(clock);
        }

        // ---------- the crawl bar, the countdown and the buttons ----------

        void BuildFoot(MatchResult result)
        {
            foot = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(foot, 0, 656, 1280, 64);
            foot.style.opacity = 0;
            screen.Add(foot);
            foot.Add(Layer((p, w, h) =>
            {
                // the shadow it casts upward, the wood, the brass edge
                Paint.Fill(p, Paint.Rect(0, -34, w, 34), Paint.Linear(180f, w, 34, (0, Rgba(0, 0, 0, 0)), (1, Rgba(0, 0, 0, .3f)), 0, -34));
                var shape = Paint.Rect(0, 0, w, h);
                Paint.Fill(p, shape, Paint.Linear(180f, w, h, (0, Rgba(62, 40, 24, .97f)), (1, Rgba(30, 19, 11, .98f))));
                Paint.Grain(p, shape, w, h);
                Paint.Fill(p, Paint.Rect(0, 0, w, 1), Rgba(217, 174, 98, .5f));
            }));
            // the countdown, a thin bar along the top edge
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(track, 0, 0, 1280, 3);
            track.style.backgroundColor = Rgba(255, 220, 170, .1f);
            foot.Add(track);
            countFill = new Shape((p, w, h) =>
            {
                var r = Paint.Rect(0, 0, w, h);
                Paint.Glow(p, r, Rgba(255, 200, 80, .6f), 8f);
                Paint.Fill(p, r, Paint.Linear(90f, w, h, (0, Hex(0xFFB72A)), (1, Hex(0xFFE07A))));
            });
            Abs(countFill, 0, 0, 1280, 3);
            foot.Add(countFill);

            crawl = Row();
            Abs(crawl, 234, 0, 660, 64);
            var lbl = Text("미도착", 11f, Hex(0xE9D7B8), true);
            lbl.style.letterSpacing = 1.5f;
            lbl.style.paddingTop = lbl.style.paddingBottom = 6;
            lbl.style.paddingLeft = lbl.style.paddingRight = 9;
            Border(lbl, 1f, Rgba(217, 174, 98, .45f));
            crawl.Add(lbl);
            for (int team = 0; team < 2; team++)
            {
                var group = Row();
                group.style.marginLeft = 18;
                var tn = Display(team == 0 ? "백팀" : "흑팀", 15f, team == 0 ? Hex(0x7FDFFF) : Hex(0xFF8A98));
                tn.style.marginRight = 9;
                group.Add(tn);
                var missing = result.NotFinished(team);
                if (missing.Count == 0) group.Add(Text("없음", 14f, Hex(0xE9DCC9)));
                for (int i = 0; i < missing.Count; i++)
                {
                    if (i > 0)
                    {
                        var dot = Text("·", 14f, Hex(0x8A7158));
                        dot.style.marginLeft = dot.style.marginRight = 7;
                        group.Add(dot);
                    }
                    group.Add(Text(missing[i].Name, 14f, missing[i].Self ? Hex(0xFFC93D) : Hex(0xE9DCC9)));
                    if (missing[i].Self) { var tag = MeChip(); tag.style.marginLeft = 4; group.Add(tag); }
                }
                crawl.Add(group);
            }
            crawl.style.opacity = 0;
            foot.Add(crawl);

            var act = Row();
            act.style.position = Position.Absolute;
            act.style.right = 22;
            act.style.top = 0;
            act.style.height = 64;
            foot.Add(act);
            countText = Text("", 13f, Hex(0xE3D2BA));
            countText.style.marginRight = 18;
            act.Add(countText);
            var again = Slab("다시 매칭", false, 20f, () => Requeue?.Invoke());
            again.style.marginRight = 12;
            act.Add(again);
            act.Add(Slab("로비로", true, 20f, () => Lobby?.Invoke()));
        }

        // The menus' slab button (ChunkyButtons): gold for the main action, walnut otherwise.
        Button Slab(string text, bool wood, float size, Action onClick)
        {
            var button = new Button { text = text };
            button.AddToClassList(ChunkyButtons.ClassName);
            if (wood) button.AddToClassList("ch-wood");
            button.style.fontSize = size;
            button.style.paddingTop = 12;
            button.style.paddingBottom = 12 + 6;
            button.style.paddingLeft = button.style.paddingRight = 22;
            RuntimePanels.Display(button);
            button.focusable = false;
            ChunkyButtons.Make(button);
            button.clicked += onClick;
            actions[button] = onClick;
            return button;
        }

        void BuildHideChip()
        {
            hide = new Button();
            hide.AddToClassList(ChunkyButtons.ClassName);
            hide.AddToClassList("ch-wood");
            hide.AddToClassList("ch-sm");
            hide.style.position = Position.Absolute;
            hide.style.left = 20;
            hide.style.bottom = 11;
            hide.style.paddingTop = 11;
            hide.style.paddingBottom = 11 + 4;
            hide.style.paddingLeft = hide.style.paddingRight = 15;
            hide.style.flexDirection = FlexDirection.Row;
            hide.style.alignItems = Align.Center;
            hide.focusable = false;
            var eye = new Shape(PaintEye);
            eye.style.width = 18;
            eye.style.height = 18;
            hide.Add(eye);
            hideText = Display("결과판 숨기기", 16f, Hex(0xF6ECDF));
            hideText.style.marginLeft = 9;
            hide.Add(hideText);
            var key = Mono("H", 11f, Hex(0xF6ECDF));
            key.style.marginLeft = 9;
            key.style.minWidth = 26;
            key.style.height = 20;
            key.style.unityTextAlign = TextAnchor.MiddleCenter;
            Border(key, 1.5f, Hex(0xF6ECDF, .85f));
            key.style.borderTopLeftRadius = key.style.borderTopRightRadius = key.style.borderBottomLeftRadius = key.style.borderBottomRightRadius = 4;
            key.style.opacity = .85f;
            hide.Add(key);
            ChunkyButtons.Make(hide);
            Action toggle = () => ToggleBoard?.Invoke();
            hide.clicked += toggle;
            actions[hide] = toggle;
            hide.style.opacity = 0;
            screen.Add(hide);
        }

        // ---------- the 나 tag over my piece ----------

        void BuildTag()
        {
            metag = new VisualElement { pickingMode = PickingMode.Ignore };
            metag.style.position = Position.Absolute;
            metag.style.width = 37;
            metag.style.height = 36;
            var plate = new Shape((p, w, h) =>
            {
                var shape = new[] { V(6, 0), V(w, 0), V(w - 6, 28), V(0, 28) };
                Paint.Glow(p, Paint.Offset(shape, 0, 3), Rgba(0, 0, 0, .3f), 6f);
                Paint.Fill(p, shape, Paint.Linear(180f, w, 28, (0, Hex(0x9BE8FF)), (1, Hex(0x2FA8E0))));
                Paint.Fill(p, new[] { V(w / 2 - 6, 28), V(w / 2 + 6, 28), V(w / 2, 36) }, Hex(0x2FA8E0));
            });
            Abs(plate, 0, 0, 37, 36);
            metag.Add(plate);
            var t = Display("나", 17f, Hex(0x06253A));
            Abs(t, 0, 4, 37, 20);
            t.style.unityTextAlign = TextAnchor.MiddleCenter;
            metag.Add(t);
            metag.style.opacity = 0;
            screen.Add(metag);
        }

        // ---------- the wipe that opens the show ----------

        void BuildWipe()
        {
            wipeBox = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(wipeBox, 0, 0, 1280, 720);
            wipeBox.style.overflow = Overflow.Hidden;
            screen.Add(wipeBox);
            bool lose = !win;
            wipe = new Shape((p, w, h) =>
            {
                // A walnut slab with brass rails and a gold leading edge, 1180 tall, skewed 18
                // degrees about its middle (the page's .bc-wipe b).
                float skew = Mathf.Tan(18f * Mathf.Deg2Rad) * h / 2f, x = 0;
                void Band(float bw, FillGradient fill, bool grain = false)
                {
                    float top = lose ? -skew : skew;
                    var band = new[] { V(x + top, 0), V(x + bw + top, 0), V(x + bw - top, h), V(x - top, h) };
                    Paint.Fill(p, band, fill);
                    if (grain) Paint.Stripes(p, band, bw, h, 90f, 15f, (0, 1, Rgba(0, 0, 0, lose ? .14f : .13f)), (7, 8, lose ? Rgba(200, 210, 230, .04f) : Rgba(255, 215, 160, .05f)), x, 0);
                    x += bw;
                }
                if (!lose)
                {
                    Band(7, Paint.Linear(90f, 7, h, (0, Hex(0xD9AE62)), (1, Hex(0xD9AE62)), x, 0));
                    Band(880, Paint.Linear(90f, 880, h, (0, Hex(0x2E1C10)), (.46f, Hex(0x6B4428)), (.8f, Hex(0x4A2E19)), (1, Hex(0x3A2414)), x, 0), true);
                    Band(7, Paint.Linear(90f, 7, h, (0, Hex(0xD9AE62)), (1, Hex(0xD9AE62)), x, 0));
                    Band(120, Paint.Linear(90f, 120, h, (0, Hex(0xE9A12A)), (.55f, Hex(0xFFD35A)), (1, Hex(0xFFF7DC)), x, 0));
                    Band(26, Paint.Linear(90f, 26, h, (0, Rgba(255, 246, 220, .9f)), (1, Rgba(255, 246, 220, 0)), x, 0));
                }
                else
                {
                    // row-reverse: the bright edge leads, on the left, moving left
                    Band(26, Paint.Linear(270f, 26, h, (0, Rgba(220, 226, 236, .6f)), (1, Rgba(220, 226, 236, 0)), x, 0));
                    Band(70, Paint.Linear(270f, 70, h, (0, Hex(0x6F6963)), (.6f, Hex(0xCFC9C1)), (1, Hex(0xF4F1EC)), x, 0));
                    Band(7, Paint.Linear(90f, 7, h, (0, Hex(0x8F8982)), (1, Hex(0x8F8982)), x, 0));
                    Band(880, Paint.Linear(90f, 880, h, (0, Hex(0x1E1712)), (.46f, Hex(0x3E3128)), (.8f, Hex(0x2C231C)), (1, Hex(0x221A14)), x, 0), true);
                    Band(7, Paint.Linear(90f, 7, h, (0, Hex(0x8F8982)), (1, Hex(0x8F8982)), x, 0));
                }
            });
            Abs(wipe, 0, -230, lose ? 990 : 1040, 1180);
            wipe.style.display = DisplayStyle.None;
            wipeBox.Add(wipe);
        }

        // ---------- the result word ----------

        void BuildWord(int winner)
        {
            ttl = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(ttl, 340, 444, 600, 80);
            ttl.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            screen.Add(ttl);
            rays = null;
            if (win)
            {
                rays = new Shape(PaintRays);
                Abs(rays, 20, -240, 560, 560);
                rays.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
                rays.style.opacity = 0;
                ttl.Add(rays);
            }
            ttlIn = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(ttlIn, 0, 0, 600, 80);
            ttlIn.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            ttl.Add(ttlIn);
            string text = winner < 0 ? "무승부" : win ? "승리!" : "패배";
            var stops = win ? new[] { (0f, Hex(0xFFFFFF)), (.38f, Hex(0xFFF2CF)), (.52f, Hex(0xF7D58A)), (1f, Hex(0xD2922A)) }
                            : new[] { (0f, Hex(0xF4F1EC)), (.46f, Hex(0xCFC9C1)), (.54f, Hex(0xA49D94)), (1f, Hex(0x6F6963)) };
            word = GoldText(text, 80f, 600, 80, stops, 32, true);
            word.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            ttlIn.Add(word);
            ttl.style.opacity = 0;
        }

        // ---------- the score bug left while the package is hidden ----------

        void BuildMini(MatchResult result)
        {
            Color top = win ? Hex(0xFFC93D) : Hex(0xA49D94);
            mini = new Shape((p, w, h) =>
            {
                var shape = new[] { V(0, 0), V(w, 0), V(w - 10, h), V(0, h) };
                Paint.Fill(p, shape, Paint.Linear(180f, w, h, (0, Rgba(74, 48, 28, .96f)), (1, Rgba(38, 24, 14, .95f))));
                Paint.Grain(p, shape, w, h);
                Paint.Fill(p, Paint.Cut(shape, 0, 2), top);
            });
            mini.style.position = Position.Absolute;
            mini.style.left = 22;
            mini.style.top = 18;
            mini.style.height = 42;
            mini.style.flexDirection = FlexDirection.Row;
            mini.style.alignItems = Align.Center;
            mini.style.paddingRight = 20;
            var badge = new Shape((p, w, h) => Paint.Fill(p, Paint.Rect(0, 0, w, h), win
                ? Paint.Linear(180f, w, h, (0, Hex(0xFFE48A)), (1, Hex(0xF2B94A)))
                : Paint.Linear(180f, w, h, (0, Hex(0xE6E1DA)), (1, Hex(0x8F8982)))));
            badge.style.alignSelf = Align.Stretch;
            badge.style.justifyContent = Justify.Center;
            badge.style.paddingLeft = badge.style.paddingRight = 13;
            badge.Add(Display(win ? "승리!" : "패배", 21f, win ? Hex(0x2A1A00) : Hex(0x1E1A16)));
            mini.Add(badge);
            var w1 = Display("백팀", 16f, Hex(0x9BE8FF));
            w1.style.marginLeft = 10;
            mini.Add(w1);
            var score = Mono(result.GoalCount(0) + " : " + result.GoalCount(1), 19f, Hex(0xFFF1D6));
            score.style.marginLeft = 10;
            mini.Add(score);
            var k1 = Display("흑팀", 16f, Hex(0xFF9AA6));
            k1.style.marginLeft = 10;
            mini.Add(k1);
            mini.style.opacity = 0;
            screen.Add(mini);
        }

        // ---------- the timeline ----------

        public void SetBoard(bool on)
        {
            boardOn = on;
            if (hideText != null) hideText.text = on ? "결과판 숨기기" : "결과판 보기";
        }

        public void Toast(string text)
        {
            if (toast == null) return;
            toast.text = text;
            toastAt = Time.unscaledTime;
        }

        // Where my piece's head is, for the 나 tag just above it.
        public void PlaceMarker(Vector3 world, Camera camera, bool show)
        {
            if (metag == null || root?.panel == null) return;
            bool visible = show && camera != null && camera.WorldToViewportPoint(world).z > 0;
            float now = Time.unscaledTime;
            tagFade.Target(visible ? 1f : 0f, now);
            metag.style.opacity = tagFade.Update(now);
            if (!visible) return;
            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, camera);
            metag.style.left = p.x - 18.5f;
            metag.style.top = p.y - 36f;
        }

        // `tp`: seconds since the result began.
        public void Tick(float tp)
        {
            if (!built) return;
            float now = Time.unscaledTime;
            phase = tp >= times[3] ? 4 : tp >= times[2] ? 3 : tp >= times[1] ? 2 : tp >= times[0] ? 1 : 0;
            bool package = phase >= 3 && boardOn, hidden = phase >= 3 && !boardOn;

            // transitions (the page's classes p1..p4 and hide-board)
            brdMove.Target(package ? 1 : 0, now);
            brdFade.Target(package ? 1 : 0, now);
            ttlFade.Target(phase >= 1 && !hidden ? 1 : 0, now);
            ttlMove.Target(phase >= 2 ? 1 : 0, now);
            ttlHide.Target(hidden ? 1 : 0, now);
            raysFade.Target(phase >= 3 ? .22f : phase >= 2 ? .45f : phase >= 1 ? 1f : 0f, now);
            xtraFade.Target(phase >= 4 ? 1 : 0, now);
            chipFade.Target(phase >= 3 ? 1 : 0, now);
            subFade.Target(phase >= 2 ? 1 : 0, now);
            subMove.Target(phase >= 2 ? 1 : 0, now);
            miniFade.Target(hidden ? 1 : 0, now);
            miniMove.Target(hidden ? 1 : 0, now);
            coldFade.Target(!win && phase >= 2 ? 1 : 0, now);

            float move = brdMove.Update(now), fade = brdFade.Update(now);
            tick.style.translate = new Translate(0, -130f * (1 - move));
            sb.style.translate = new Translate(0, 190f * (1 - move));
            foot.style.translate = new Translate(0, 80f * (1 - move));
            tick.style.opacity = sb.style.opacity = foot.style.opacity = vig.style.opacity = fade;
            cold.style.opacity = coldFade.Update(now);
            crawl.style.opacity = xtraFade.Update(now);
            hide.style.opacity = chipFade.Update(now);
            hide.pickingMode = phase >= 3 ? PickingMode.Position : PickingMode.Ignore;
            sub.style.opacity = subFade.Update(now);
            sub.style.translate = new Translate(0, 10f * (1 - subMove.Update(now)));
            mini.style.opacity = miniFade.Update(now);
            mini.style.translate = new Translate(-24f * (1 - miniMove.Update(now)), 0);

            // the word: slams in mid-frame at p1, docks on the scoreboard at p2, punches when
            // the deciding chip lands, drops away while the package is hidden
            float dock = ttlMove.Update(now), away = ttlHide.Update(now);
            float ttlScale = Mathf.LerpUnclamped(2.1f, 1f, dock);
            ttl.style.opacity = ttlFade.Update(now);
            ttl.style.translate = new Translate(0, Mathf.LerpUnclamped(-178f, 0f, dock) + 70f * away);
            ttl.style.scale = new Scale(new Vector2(ttlScale, ttlScale));
            float u = tp - times[0];
            if (win)
            {
                // slam: 2.4 -> .94 (55 %) -> 1, fading in on the way down
                float k = Mathf.Clamp01(u / .62f);
                float s = Keys(k, Ease.Slam, (0, 2.4f), (.55f, .94f), (1, 1f));
                ttlIn.style.scale = new Scale(new Vector2(s, s));
                ttlIn.style.opacity = Keys(k, Ease.Slam, (0, 0f), (.55f, 1f), (1, 1f));
                float pu = Mathf.Clamp01((tp - decisiveAt) / .5f);
                float punch = phase >= 3 && tp >= decisiveAt ? Keys(pu, Ease.Punch, (0, 1f), (.35f, 1.14f), (1, 1f)) : 1f;
                word.style.scale = new Scale(new Vector2(punch, punch));
                rays.style.opacity = raysFade.Update(now);
                rays.style.rotate = new Rotate(now / 22f * 360f % 360f);
            }
            else
            {
                // thud: drops from 90 px above, overshoots 7 px, settles
                float k = Mathf.Clamp01(u / .72f);
                ttlIn.style.translate = new Translate(0, Keys(k, Ease.Thud, (0, -90f), (.55f, 7f), (.78f, -2f), (1, 0f)));
                float s = Keys(k, Ease.Thud, (0, 1.05f), (.55f, 1f), (1, 1f));
                ttlIn.style.scale = new Scale(new Vector2(s, s));
                ttlIn.style.opacity = Keys(k, Ease.Thud, (0, 0f), (.55f, 1f), (1, 1f));
            }

            Wipe(tp);

            // chips drop into the ribbon one by one; the scan light runs along it
            foreach (var (e, at) in chips)
            {
                float k = phase >= 3 ? Mathf.Clamp01((tp - at) / .5f) : 0f, eased = Ease.Chip(k);
                e.style.opacity = phase >= 3 && tp >= at ? Mathf.Clamp01(eased) : 0f;
                e.style.translate = new Translate(0, -30f * (1 - eased));
                float s = Mathf.LerpUnclamped(.86f, 1f, eased);
                e.style.scale = new Scale(new Vector2(s, s));
            }
            {
                float k = phase >= 3 ? (tp - times[2] - .36f) / 1.5f : -1f;
                scan.style.opacity = k <= 0 || k >= 1 ? 0 : k < .06f ? k / .06f : k > .9f ? (1 - k) / .1f : 1f;
                scan.style.translate = new Translate(1160f * Mathf.Clamp01(k), 0);
            }

            // portraits drop into the slots on the same beat, a ring pulses out of each
            foreach (var (face, ring, dec, at) in slots)
            {
                float k = phase >= 3 ? Mathf.Clamp01((tp - at) / .5f) : 0f, eased = Ease.Drop(k);
                face.style.opacity = phase >= 3 && tp >= at ? Mathf.Clamp01(Ease.Drop(Mathf.Clamp01(k / .45f))) : 0f;
                face.style.translate = new Translate(0, -36f * (1 - eased));
                float s = Mathf.LerpUnclamped(.5f, 1f, eased);
                face.style.scale = new Scale(new Vector2(s, s));
                float rk = phase >= 3 ? (tp - at - .16f) / (dec ? .9f : .6f) : -1f, re = Ease.Out(Mathf.Clamp01(rk));
                ring.style.opacity = rk <= 0 || rk >= 1 ? 0 : Mathf.Lerp(dec ? 1f : .9f, 0f, re);
                float rs = Mathf.Lerp(.7f, dec ? 4.2f : 1.9f, re);
                ring.style.scale = new Scale(new Vector2(rs, rs));
            }
            if (decGlow != null)
            {
                float g = tp - decisiveAt - .6f;
                decGlow.style.opacity = phase >= 3 && g > 0 ? .3f + .7f * (.5f - .5f * Mathf.Cos(g / 1.8f * Mathf.PI * 2)) : 0f;
            }

            // the rolling digits
            foreach (var (e, inAt, outAt) in digits)
            {
                float y = 0, a = 0;
                if (phase >= 3 && tp >= inAt)
                {
                    float k = Mathf.Clamp01((tp - inAt) / .34f);
                    y = 33.6f * (1 - Ease.Drop(k));
                    a = Mathf.Clamp01(Ease.Drop(k));
                    if (tp >= outAt)
                    {
                        float o = Ease.In(Mathf.Clamp01((tp - outAt) / .22f));
                        y = -33.6f * o;
                        a = 1 - o;
                    }
                }
                e.style.opacity = a;
                e.style.translate = new Translate(0, y);
            }

            // the deciding finish: the winners' half washes gold, the losers' fades and its crown tips
            float dk = phase >= 3 ? tp - decisiveAt : -1f;
            if (wonWash != null) wonWash.style.opacity = dk > 0 ? Ease.Out(Mathf.Clamp01(dk / .6f)) : 0f;
            if (lostWash != null) lostWash.style.opacity = dk > 0 ? Mathf.Lerp(1f, .3f, Ease.Out(Mathf.Clamp01(dk / .9f))) : 1f;
            float filter = dk > 0 ? Ease.Out(Mathf.Clamp01(dk / .9f)) : 0f;
            if (!Mathf.Approximately(filter, lostFilter))
            {
                lostFilter = filter;
                foreach (var (label, color) in lostLabels) label.style.color = Filter(color, filter);
                foreach (var face in lostFaces) if (face != null) face.style.opacity = filter;
                foreach (var shape in lostShapes) shape.MarkDirtyRepaint();
            }
            foreach (var crown in lostCrowns)
            {
                float k = dk > 0 ? Ease.Crown(Mathf.Clamp01(dk / .7f)) : 0f;
                crown.style.translate = new Translate(8f * k, 5f * k);
                crown.style.rotate = new Rotate(30f * k);
            }
            if (shine != null)
            {
                // once as the decision lands, then every 1.4 s from three seconds after it
                float x = -170f;
                if (dk > 0 && dk < 1f) x = -170f + 1140f * Ease.Shine(dk);
                else if (dk >= 3f) x = -170f + 1140f * Ease.Shine((dk - 3f) % 1.4f / 1.4f);
                if (!Mathf.Approximately(x, shineX)) { shineX = x; shine.MarkDirtyRepaint(); }
            }
            if (flash != null)
            {
                float k = phase >= 3 ? tp - decisiveAt - .1f : -1f;
                flash.style.opacity = k < 0 || k > 1 ? 0 : k < .14f ? Ease.Out(k / .14f) : 1 - Ease.Out((k - .14f) / .86f);
            }

            // the countdown, from p3
            float left = Mathf.Clamp(LobbySeconds - Mathf.Max(0f, tp - times[2]), 0f, LobbySeconds);
            countFill.style.width = 1280f * left / LobbySeconds;
            countText.text = left > 0 ? Mathf.CeilToInt(left) + "초 뒤 자동으로 로비로" : preview ? "미리보기라 여기서 멈춰요" : "로비로 가는 중";

            if (toast != null)
            {
                float k = (now - toastAt) / 2.6f;
                toast.style.opacity = k < 0 || k > 1 ? 0 : k < .08f ? k / .08f : k < .82f ? 1 : 1 - (k - .82f) / .18f;
                toast.style.translate = new Translate(0, k < .08f ? 6f * (1 - k / .08f) : 0);
            }
        }

        // The stinger: once at p1, .86 s on a win (left to right), 1.15 s on a loss (right to left).
        void Wipe(float tp)
        {
            float k = (tp - times[0]) / (win ? .86f : 1.15f);
            if (phase < 1 || k < 0 || k >= 1) { wipe.style.display = DisplayStyle.None; return; }
            wipe.style.display = DisplayStyle.Flex;
            float e = win ? Ease.WipeWin(k) : Ease.WipeLose(k);
            wipe.style.translate = new Translate(win ? Mathf.LerpUnclamped(-1320f, 1560f, e) : Mathf.LerpUnclamped(1560f, -1320f, e), 0);
        }

        // Keyframes with the timing function applied to each segment, held at the ends.
        static float Keys(float k, Func<float, float> ease, params (float at, float v)[] keys)
        {
            if (k <= keys[0].at) return keys[0].v;
            for (int i = 1; i < keys.Length; i++)
                if (k <= keys[i].at)
                {
                    float s = (k - keys[i - 1].at) / Mathf.Max(1e-5f, keys[i].at - keys[i - 1].at);
                    return Mathf.LerpUnclamped(keys[i - 1].v, keys[i].v, ease(s));
                }
            return keys[keys.Length - 1].v;
        }

        // The same safety net as the menus (Docs/Architecture/UI.md §2): until a real pointer
        // event arrives, a press is held a frame and then given to the button under the mouse.
        void Update()
        {
            if (realPointer || root?.panel == null) return;
            if (pressedFrame >= 0 && Time.frameCount > pressedFrame)
            {
                pressedFrame = -1;
                var hit = Pick(pressedPoint);
                if (!realPointer && hit != null && hit == pressed && actions.TryGetValue(hit, out var action))
                {
                    ChunkyButtons.Pulse(hit);
                    action();
                }
                pressed = null;
            }
            if (Input.GetMouseButtonDown(0)) pressed = Pick(Input.mousePosition);
            if (Input.GetMouseButtonUp(0) && pressed != null)
            {
                pressedPoint = Input.mousePosition;
                pressedFrame = Time.frameCount;
            }
        }

        Button Pick(Vector2 screenPoint)
        {
            var point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPoint.x, UnityEngine.Screen.height - screenPoint.y));
            for (var e = root.panel.Pick(point); e != null; e = e.parent)
                if (e is Button b && actions.ContainsKey(b)) return b;
            return null;
        }

        void OnDestroy()
        {
            if (owned != null) Destroy(owned);
        }

        // ---------- building blocks ----------

        static void Abs(VisualElement e, float x, float y, float w, float h)
        {
            e.style.position = Position.Absolute;
            e.style.left = x;
            e.style.top = y;
            e.style.width = w;
            e.style.height = h;
        }

        static VisualElement Full(Texture2D texture)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(e, 0, 0, 1280, 720);
            e.style.backgroundImage = Background.FromTexture2D(texture);
            return e;
        }

        static VisualElement Row()
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.flexDirection = FlexDirection.Row;
            e.style.alignItems = Align.Center;
            return e;
        }

        static Shape Layer(Action<Painter2D, float, float> paint)
        {
            var s = new Shape(paint);
            s.style.position = Position.Absolute;
            s.style.left = 0;
            s.style.top = 0;
            s.style.right = 0;
            s.style.bottom = 0;
            return s;
        }

        static void Border(VisualElement e, float width, Color color)
        {
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = width;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = color;
        }

        static Label Text(string text, float size, Color color, bool bold = false)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            return label;
        }

        // Black Han Sans: titles, names, numbers on plates.
        static Label Display(string text, float size, Color color)
        {
            var label = Text(text, size, color);
            RuntimePanels.Display(label);
            return label;
        }

        // The page's monospace figures (finish times, the clock, the score bug).
        Label Mono(string text, float size, Color color)
        {
            var label = Text(text, size, color, true);
            if (mono != null) label.style.unityFont = mono;
            return label;
        }

        // The small sky-blue "나" chip after my name.
        static VisualElement MeChip()
        {
            var chip = new Shape((p, w, h) => Paint.Fill(p, new[] { V(4, 0), V(w, 0), V(w - 4, h), V(0, h) }, Paint.Linear(180f, w, h, (0, Hex(0x9BE8FF)), (1, Hex(0x2FA8E0)))));
            chip.style.paddingLeft = chip.style.paddingRight = 7;
            chip.style.paddingTop = 3;
            chip.style.paddingBottom = 4;
            chip.style.flexShrink = 0;
            chip.Add(Display("나", 13f, Hex(0x06253A)));
            return chip;
        }

        static VisualElement Portrait(PieceKind kind, int team, bool filtered = false)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            var texture = Overlays.HexPortrait(kind, team, filtered);
            if (texture != null) e.style.backgroundImage = Background.FromTexture2D(texture);
            return e;
        }

        // CSS filter: saturate(1 - .65 k) brightness(1 - .2 k), the losers' half at k = 1.
        static Color Filter(Color c, float k)
        {
            if (k <= 0) return c;
            float lum = .2126f * c.r + .7152f * c.g + .0722f * c.b, s = 1f - .65f * k, b = 1f - .2f * k;
            return new Color((lum + (c.r - lum) * s) * b, (lum + (c.g - lum) * s) * b, (lum + (c.b - lum) * s) * b, c.a);
        }

        // Text in a vertical gradient: in each horizontal band of the box a copy of the label is
        // cut to that band and coloured with the gradient there. `shadow`: the word's drop
        // shadows (a hard one 5 px down, a soft one 14 px down).
        static VisualElement GoldText(string text, float size, float w, float h, (float at, Color c)[] stops, int bands, bool shadow)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(box, 0, 0, w, h);
            Label Make(Color c)
            {
                var label = Display(text, size, c);
                Abs(label, 0, 0, w, h);
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                return label;
            }
            if (shadow)
            {
                var under = Make(Rgba(40, 20, 6, .92f));
                under.style.top = 5;
                under.style.textShadow = new TextShadow { offset = new Vector2(0, 9), blurRadius = 22, color = Rgba(0, 0, 0, .6f) };
                box.Add(under);
            }
            for (int i = 0; i < bands; i++)
            {
                float y0 = h * i / bands, y1 = h * (i + 1) / bands;
                var clip = new VisualElement { pickingMode = PickingMode.Ignore };
                Abs(clip, 0, y0, w, y1 - y0 + (i < bands - 1 ? .5f : 0f));
                clip.style.overflow = Overflow.Hidden;
                var label = Make(Paint.Sample(stops, (y0 + y1) / 2f / h));
                label.style.top = -y0;
                clip.Add(label);
                box.Add(clip);
            }
            return box;
        }

        // .bc-vig: dark at the top (120 px) and the bottom (290 px), under the package.
        static void PaintVig(Painter2D p, float w, float h)
        {
            Paint.Fill(p, Paint.Rect(0, 0, w, 120), Paint.Linear(180f, w, 120, (0, Rgba(10, 6, 3, .66f)), (1, Rgba(10, 6, 3, 0))));
            Paint.Fill(p, Paint.Rect(0, h - 290, w, 290), Paint.Linear(0f, w, 290, (0, Rgba(10, 6, 3, .82f)), (170f / 290f, Rgba(10, 6, 3, .42f)), (1, Rgba(10, 6, 3, 0)), 0, h - 290));
        }

        // 24 rays of 5 degrees every 15, fading out from a fifth of the way (.rays).
        static void PaintRays(Painter2D p, float w, float h)
        {
            var c = V(w / 2, h / 2);
            p.fillGradient = Paint.Radial(c, 253f, (0, Rgba(255, 214, 120, .2f)), (55f / 253f, Rgba(255, 214, 120, .2f)), (1, Rgba(255, 214, 120, 0)));
            for (int i = 0; i < 24; i++)
            {
                float a0 = i * 15f * Mathf.Deg2Rad, a1 = (i * 15f + 5f) * Mathf.Deg2Rad;
                p.BeginPath();
                p.MoveTo(c);
                p.LineTo(c + new Vector2(Mathf.Sin(a0), -Mathf.Cos(a0)) * 260f);
                p.LineTo(c + new Vector2(Mathf.Sin(a1), -Mathf.Cos(a1)) * 260f);
                p.ClosePath();
                p.Fill();
            }
        }

        static void PaintChecker(Painter2D p, float w, float h)
        {
            float q = w / 4f;
            Paint.Fill(p, Paint.Rect(0, 0, w, h), Color.white);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    if ((x + y) % 2 == 0) Paint.Fill(p, Paint.Rect(x * q, y * q, q, q), Hex(0x1A1208));
        }

        // The page's eye: an almond, a dark iris ring and a light pupil.
        static void PaintEye(Painter2D p, float w, float h)
        {
            var c = V(w / 2, h / 2);
            var outline = new List<Vector2>();
            for (int i = 0; i < 32; i++)
            {
                float a = i / 32f * Mathf.PI * 2;
                outline.Add(c + new Vector2(Mathf.Cos(a) * w * .46f, Mathf.Sin(a) * h * .3f * (1 - .25f * Mathf.Abs(Mathf.Cos(a)))));
            }
            Paint.Fill(p, outline, Hex(0xF6ECDF));
            Paint.Fill(p, Paint.Circle(c, h * .18f), Hex(0x563620));
            Paint.Fill(p, Paint.Circle(c, h * .08f), Hex(0xF6ECDF));
        }

        // ---------- painting ----------

        // A plain element painted by a delegate (painter, width, height); repainted on resize.
        sealed class Shape : VisualElement
        {
            public Shape(Action<Painter2D, float, float> paint)
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += context =>
                {
                    float w = layout.width, h = layout.height;
                    if (float.IsNaN(w) || float.IsNaN(h) || w <= 0 || h <= 0) return;
                    paint(context.painter2D, w, h);
                };
            }
        }

        // CSS backgrounds and clip-paths in Painter2D terms. Coordinates are px, y down.
        static class Paint
        {
            public static Vector2[] Rect(float x, float y, float w, float h) => new[] { V(x, y), V(x + w, y), V(x + w, y + h), V(x, y + h) };

            // polygon(25% 0, 75% 0, 100% 50%, 75% 100%, 25% 100%, 0 50%)
            public static Vector2[] Hexagon(float x, float y, float w, float h) =>
                new[] { V(x + w * .25f, y), V(x + w * .75f, y), V(x + w, y + h * .5f), V(x + w * .75f, y + h), V(x + w * .25f, y + h), V(x, y + h * .5f) };

            public static Vector2[] Circle(Vector2 c, float r)
            {
                var points = new Vector2[24];
                for (int i = 0; i < points.Length; i++) { float a = i / (float)points.Length * Mathf.PI * 2; points[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r; }
                return points;
            }

            // The page's crown icon (viewBox -13 -11 26 20).
            static readonly Vector2[] CrownPoints = { V(-11, 8), V(-12, -5), V(-5, 0), V(0, -10), V(5, 0), V(12, -5), V(11, 8) };
            public static Vector2[] Crown(float w, float h) => CrownPoints.Select(q => V((q.x + 13f) * w / 26f, (q.y + 11f) * h / 20f)).ToArray();

            public static Vector2[] Offset(IList<Vector2> shape, float dx, float dy) => shape.Select(q => q + V(dx, dy)).ToArray();

            public static void Fill(Painter2D p, IList<Vector2> shape, Color c)
            {
                if (shape == null || shape.Count < 3 || c.a <= 0) return;
                // Through a gradient with alpha keys: the two-colour helper drops the alpha.
                Fill(p, shape, FillGradient.MakeLinearGradient(Gradient(new[] { (0f, c), (1f, c) }), Vector2.zero, Vector2.right, AddressMode.Clamp));
            }

            public static void Fill(Painter2D p, IList<Vector2> shape, FillGradient gradient)
            {
                if (shape == null || shape.Count < 3) return;
                p.fillGradient = gradient;
                p.BeginPath();
                p.MoveTo(shape[0]);
                for (int i = 1; i < shape.Count; i++) p.LineTo(shape[i]);
                p.ClosePath();
                p.Fill();
            }

            // linear-gradient(angle, stops) over a w x h box (whose top left is x0, y0).
            public static FillGradient Linear(float angle, float w, float h, params (float at, Color c)[] stops) => Make(angle, w, h, stops, 0, 0);
            public static FillGradient Linear(float angle, float w, float h, (float at, Color c) s0, (float at, Color c) s1, float x0, float y0) => Make(angle, w, h, new[] { s0, s1 }, x0, y0);
            public static FillGradient Linear(float angle, float w, float h, (float at, Color c) s0, (float at, Color c) s1, (float at, Color c) s2, float x0, float y0) => Make(angle, w, h, new[] { s0, s1, s2 }, x0, y0);
            public static FillGradient Linear(float angle, float w, float h, (float at, Color c) s0, (float at, Color c) s1, (float at, Color c) s2, (float at, Color c) s3, float x0, float y0) => Make(angle, w, h, new[] { s0, s1, s2, s3 }, x0, y0);
            public static FillGradient Linear(float angle, float w, float h, (float at, Color c) s0, (float at, Color c) s1, (float at, Color c) s2, (float at, Color c) s3, (float at, Color c) s4, float x0, float y0) => Make(angle, w, h, new[] { s0, s1, s2, s3, s4 }, x0, y0);

            static FillGradient Make(float angle, float w, float h, (float at, Color c)[] stops, float x0, float y0)
            {
                float a = angle * Mathf.Deg2Rad;
                var dir = V(Mathf.Sin(a), -Mathf.Cos(a));
                float len = Mathf.Abs(w * Mathf.Sin(a)) + Mathf.Abs(h * Mathf.Cos(a));
                var center = V(x0 + w / 2f, y0 + h / 2f);
                return FillGradient.MakeLinearGradient(Gradient(stops), center - dir * len / 2f, center + dir * len / 2f, AddressMode.Clamp);
            }

            public static FillGradient Radial(Vector2 center, float radius, params (float at, Color c)[] stops) =>
                FillGradient.MakeRadialGradient(Gradient(stops), center, radius, center, AddressMode.Clamp);

            static Gradient Gradient((float at, Color c)[] stops)
            {
                var g = new Gradient();
                g.SetKeys(stops.Select(s => new GradientColorKey(s.c, s.at)).ToArray(), stops.Select(s => new GradientAlphaKey(s.c.a, s.at)).ToArray());
                return g;
            }

            public static Color Sample((float at, Color c)[] stops, float t)
            {
                if (t <= stops[0].at) return stops[0].c;
                for (int i = 1; i < stops.Length; i++)
                    if (t <= stops[i].at) return Color.Lerp(stops[i - 1].c, stops[i].c, (t - stops[i - 1].at) / Mathf.Max(1e-5f, stops[i].at - stops[i - 1].at));
                return stops[stops.Length - 1].c;
            }

            // The page's wood: repeating-linear-gradient(178deg, rgba(0,0,0,.12) 0 1px, transparent
            // 1px 6px, rgba(255,215,160,.035) 6px 7px, transparent 7px 13px), cut to the shape.
            public static void Grain(Painter2D p, IList<Vector2> shape, float w, float h) =>
                Stripes(p, shape, w, h, 178f, 13f, (0, 1, Rgba(0, 0, 0, .08f)), (6, 7, Rgba(255, 215, 160, .022f)), 0, 0);

            // repeating-linear-gradient with two thin lines per period, over a w x h box at (x0, y0).
            public static void Stripes(Painter2D p, IList<Vector2> shape, float w, float h, float angle, float period,
                                       (float from, float to, Color c) l0, (float from, float to, Color c) l1, float x0, float y0)
            {
                float a = angle * Mathf.Deg2Rad;
                var dir = V(Mathf.Sin(a), -Mathf.Cos(a));
                float len = Mathf.Abs(w * Mathf.Sin(a)) + Mathf.Abs(h * Mathf.Cos(a));
                float off = Vector2.Dot(V(x0 + w / 2f, y0 + h / 2f), dir) - len / 2f;
                for (float k = 0; k < len; k += period)
                {
                    foreach (var (from, to, c) in new[] { l0, l1 })
                    {
                        if (k + from >= len) continue;
                        Fill(p, Clip(Clip(shape, dir, k + from + off), -dir, -(k + to + off)), c);
                    }
                }
            }

            // The part of `shape` where dot(n, point) >= d (Sutherland-Hodgman, one edge).
            public static List<Vector2> Clip(IList<Vector2> shape, Vector2 n, float d)
            {
                var result = new List<Vector2>();
                if (shape == null || shape.Count == 0) return result;
                for (int i = 0; i < shape.Count; i++)
                {
                    Vector2 a = shape[i], b = shape[(i + 1) % shape.Count];
                    float da = Vector2.Dot(n, a) - d, db = Vector2.Dot(n, b) - d;
                    if (da >= 0) result.Add(a);
                    if ((da >= 0) != (db >= 0)) result.Add(a + (b - a) * (da / (da - db)));
                }
                return result;
            }

            // The shape cut to the rectangle (x, y, w, h).
            public static List<Vector2> Clip(IList<Vector2> shape, float x, float y, float w, float h) =>
                Clip(Clip(Clip(Clip(shape, V(1, 0), x), V(-1, 0), -(x + w)), V(0, 1), y), V(0, -1), -(y + h));

            // The band of the shape from y0 to y1 (a CSS "0 0 / 100% 2px" layer).
            public static List<Vector2> Cut(IList<Vector2> shape, float y0, float y1) => Clip(Clip(shape, V(0, 1), y0), V(0, -1), -y1);

            // Convex `a` cut by convex `b`.
            public static List<Vector2> Intersect(IList<Vector2> a, IList<Vector2> b)
            {
                var inside = Vector2.zero;
                foreach (var q in b) inside += q;
                inside /= b.Count;
                var result = a.ToList();
                for (int i = 0; i < b.Count && result.Count > 0; i++)
                {
                    Vector2 p0 = b[i], e = b[(i + 1) % b.Count] - p0, n = V(-e.y, e.x);
                    if (Vector2.Dot(n, inside - p0) < 0) n = -n;
                    result = Clip(result, n, Vector2.Dot(n, p0));
                }
                return result;
            }

            // A soft halo (the page's box-shadow and drop-shadow blurs): strokes round the
            // outline, the widest and faintest first.
            public static void Glow(Painter2D p, IList<Vector2> shape, Color c, float radius)
            {
                if (shape == null || shape.Count < 2) return;
                p.lineJoin = LineJoin.Round;
                const int steps = 5;
                for (int i = steps; i >= 1; i--)
                {
                    float t = i / (float)steps;
                    var col = c;
                    col.a = c.a * .12f;
                    p.strokeColor = col;
                    p.lineWidth = radius * 2f * t;
                    p.BeginPath();
                    p.MoveTo(shape[0]);
                    for (int k = 1; k < shape.Count; k++) p.LineTo(shape[k]);
                    p.ClosePath();
                    p.Stroke();
                }
            }
        }

        // ---------- full-screen layers and portraits (textures made once) ----------

        static class Overlays
        {
            static Texture2D vignette, coldWash, flashLayer;
            static readonly Dictionary<string, Texture2D> portraits = new Dictionary<string, Texture2D>();

            // A 1280 x 720 layer from a function of page coordinates, at a quarter of the size.
            static Texture2D Layer(string name, Func<float, float, Color> at)
            {
                const int w = 320, h = 180;
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        px[(h - 1 - y) * w + x] = at((x + .5f) * 4f, (y + .5f) * 4f);
                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Result " + name };
                texture.SetPixels(px);
                texture.Apply();
                return texture;
            }

            // .ovc: radial-gradient(90% 90% at 50% 45%, transparent 55%, rgba(8,4,2,.72) 100%)
            public static Texture2D Vignette() => vignette != null ? vignette : vignette = Layer("vignette", (x, y) =>
            {
                float t = Mathf.Sqrt(Sq((x - 640f) / 1152f) + Sq((y - 324f) / 648f));
                return Rgba(8, 4, 2, .72f * Mathf.Clamp01((t - .55f) / .45f));
            });

            // .cold: radial-gradient(70% 80% at 50% 60%, rgba(70,96,140,0) 30%, rgba(24,34,56,.55) 100%)
            // over linear-gradient(180deg, rgba(40,58,92,.22), rgba(20,28,44,.3))
            public static Texture2D Cold() => coldWash != null ? coldWash : coldWash = Layer("cold", (x, y) =>
            {
                var under = Color.Lerp(Rgba(40, 58, 92, .22f), Rgba(20, 28, 44, .3f), y / 720f);
                float t = Mathf.Clamp01((Mathf.Sqrt(Sq((x - 640f) / 896f) + Sq((y - 432f) / 576f)) - .3f) / .7f);
                return Over(Color.Lerp(Rgba(70, 96, 140, 0), Rgba(24, 34, 56, .55f), t), under);
            });

            // .bc-flash: radial-gradient(55% 45% at 50% 84%, rgba(255,214,120,.55), rgba(255,214,120,0) 70%)
            public static Texture2D Flash() => flashLayer != null ? flashLayer : flashLayer = Layer("flash", (x, y) =>
            {
                float t = Mathf.Sqrt(Sq((x - 640f) / 704f) + Sq((y - 604.8f) / 324f));
                return Rgba(255, 214, 120, .55f * Mathf.Clamp01(1f - t / .7f));
            });

            static float Sq(float v) => v * v;

            static Color Over(Color top, Color under)
            {
                float a = top.a + under.a * (1 - top.a);
                if (a <= 0) return Color.clear;
                var c = (top * top.a + under * under.a * (1 - top.a)) / a;
                c.a = a;
                return c;
            }

            // A face in its team's hexagon (.hx: the team gradient, the portrait over it, cut to
            // the hexagon), copied once out of the menus' portrait render (MenuArt.Portrait).
            public static Texture2D HexPortrait(PieceKind kind, int team, bool filtered)
            {
                string key = kind + "_" + team + (filtered ? "_filtered" : "");
                if (portraits.TryGetValue(key, out var cached) && cached != null) return cached;
                var rt = MenuArt.Portrait("face", kind, team, new FigurePose { Arm = .3f }, new Vector2(.6f, 6f), true);
                if (rt == null) return null;
                int size = rt.width;
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var face = new Texture2D(size, size, TextureFormat.RGBA32, false);
                face.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                face.Apply();
                RenderTexture.active = previous;
                Color top = team == 0 ? Hex(0x7FDFFF) : Hex(0xFF8A98), bottom = team == 0 ? Hex(0x1A6FA8) : Hex(0x9A1E32);
                var src = face.GetPixels();
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + .5f) / size, v = 1f - (y + .5f) / size;   // v = 0 at the top
                        float dx = Mathf.Abs(u - .5f), dy = Mathf.Abs(v - .5f);
                        // inside the flat top and bottom, and inside the slanted sides 2 dx + dy <= 1
                        float inside = Mathf.Min(.5f - dy, (1f - 2f * dx - dy) / Mathf.Sqrt(5f)) * size;
                        var f = src[y * size + x];
                        var c = Color.Lerp(Color.Lerp(top, bottom, v), new Color(f.r, f.g, f.b, 1), f.a);
                        if (filtered) c = Filter(c, 1f);
                        c.a = Mathf.Clamp01(inside + .5f);
                        px[y * size + x] = c;
                    }
                UnityEngine.Object.Destroy(face);
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Result face " + key, filterMode = FilterMode.Trilinear };
                texture.SetPixels(px);
                texture.Apply(true);
                return portraits[key] = texture;
            }
        }

        // ---------- timing ----------

        // The page's timing functions (cubic-bezier) and CSS's default "ease".
        static class Ease
        {
            public static float Css(float x) => Bezier(.25f, .1f, .25f, 1f, x);
            public static float Out(float x) => Bezier(0f, 0f, .58f, 1f, x);
            public static float In(float x) => Bezier(.42f, 0f, 1f, 1f, x);
            public static float Board(float x) => Bezier(.3f, 1.2f, .5f, 1f, x);
            public static float Dock(float x) => Bezier(.6f, 0f, .25f, 1f, x);
            public static float Pop(float x) => Bezier(.3f, 1.5f, .5f, 1f, x);
            public static float Mini(float x) => Bezier(.3f, 1.4f, .5f, 1f, x);
            public static float Slam(float x) => Bezier(.2f, 1.5f, .4f, 1f, x);
            public static float Thud(float x) => Bezier(.5f, 0f, .6f, 1f, x);
            public static float Punch(float x) => Bezier(.3f, 1.7f, .5f, 1f, x);
            public static float Chip(float x) => Bezier(.3f, 1.5f, .5f, 1f, x);
            public static float Drop(float x) => Bezier(.3f, 1.6f, .5f, 1f, x);
            public static float Crown(float x) => Bezier(.5f, 0f, .6f, 1.5f, x);
            public static float Shine(float x) => Bezier(.5f, 0f, .3f, 1f, x);
            public static float WipeWin(float x) => Bezier(.72f, 0f, .24f, 1f, x);
            public static float WipeLose(float x) => Bezier(.6f, 0f, .3f, 1f, x);

            public static float Bezier(float x1, float y1, float x2, float y2, float x)
            {
                x = Mathf.Clamp01(x);
                float lo = 0, hi = 1, t = x;
                for (int i = 0; i < 20; i++)
                {
                    float cx = Curve(t, x1, x2);
                    if (Mathf.Abs(cx - x) < 1e-5f) break;
                    if (cx < x) lo = t; else hi = t;
                    t = (lo + hi) / 2f;
                }
                return Curve(t, y1, y2);
            }

            static float Curve(float t, float a, float b) => ((1 - 3 * b + 3 * a) * t + (3 * b - 6 * a)) * t * t + 3 * a * t;
        }

        // A CSS transition: from wherever the value is toward the target, on real time.
        sealed class Tween
        {
            readonly float duration, delay;
            readonly Func<float, float> ease;
            float from, to, start = -1000f, value;

            public Tween(float initial, float duration, Func<float, float> ease, float delay = 0f)
            {
                from = to = value = initial;
                this.duration = duration;
                this.ease = ease;
                this.delay = delay;
            }

            public void Target(float target, float now)
            {
                if (Mathf.Approximately(target, to)) return;
                from = value;
                to = target;
                start = now + delay;
            }

            public float Update(float now)
            {
                float k = duration <= 0 ? 1 : Mathf.Clamp01((now - start) / duration);
                value = Mathf.LerpUnclamped(from, to, ease(k));
                return value;
            }
        }
    }
}
