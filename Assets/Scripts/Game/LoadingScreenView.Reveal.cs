using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // Pawn Rush's course reveal: from the moment the host starts the match, every PC shows which three mini-games this
    // round draws, over the study behind (the players who walked onto its chessboard), blurred and darkened.
    //
    // R551 to R564 (11-11) built design A "왕실 룰렛" here (gilded frames rolling like a slot machine over a chessboard
    // track, a "코스 확정!" stamp) and R557 its sound (LoadingScreenView.RevealSound.cs); both are kept in
    // ArtSource/Loading/CourseReveal/R564_backup.
    //
    // R579 (11-12): 승규 님 found that look AI-made and flat ("테두리 등등 별로", "효과가 너무 ai가 티가 나고 … 매칭 같은
    // 느낌이 더 들었으면") and picked, from https://claude.ai/artifact/2Fx6jPUMwAa3d5bzcxuKbT, design C (loading-screen
    // panels) in colour set C1 (ebony, ivory, brass) with finale F2 ("F2로 해서 유니티에 넣어주고 소리는 뺴줘"). The title
    // "이번 판 미니게임" slides in on an ivory plate; three tall slanted panels drop in (from above, below, above), cut
    // hard through the pictures their slot may draw, slower and slower, and stop on this round's: the panel pops, a
    // brass bar runs under the picture, the name slides in and a light slice crosses it, the screen shakes a little.
    // R611: the panels roll one after another, each starting once the one before it has stopped (they rolled together).
    // Seven squares under the panels fill one by one, one per section. Then 3, 2, 1 punch in, big, in the middle of
    // the screen while the pictures dim. No gold gradients, glows, rays or sparks, no zone names, no "매칭 완료" and no
    // "코스 확정"; no sound (R583 added sound made in code: LoadingScreenView.RevealSound.cs, CourseRevealSynth).
    //
    // Everything is laid out on the sample's 1280 x 720 stage (scaled to fit the screen), and every frame is worked out
    // from the time since the host started (LoadingState.Reveal, on the shared clock) and the round's seed alone, so every
    // PC shows the same frame at the same moment. The numbers are the sample's (template_c.html, fin 'count'). Each
    // mini-game's picture is Resources/Loading/CourseReveal/<its prefab's name> (ArtSource/Loading/CourseReveal).
    public sealed partial class LoadingScreenView
    {
        // ---- timing (seconds from the host's start) ----

        // The panels slide in from PanelEnter for PanelSlide, start cutting through the pictures at RollStart and stop on
        // this round's at RollLand. R592 ("살짝만 천천히 해줘서 이런 코스 구나 라는 걸 느끼게"): the stops are 1.4 s apart
        // (were 1.2 s, 1.7 / 2.9 / 4.1), so each roll slows down longer and each mini-game stays a moment on its own.
        // R597 ("미니게임 고르는 장면이 좀 빨라"): 1.8 s apart (were 1.4 s, 2.0 / 3.4 / 4.8), and the cuts start slower
        // (HopTimes' first cut), so the first panel's last cuts no longer rush into its stop.
        // R611 ("미니게임 1 정하고 그다음 미니게임 2 정하고 그 다음 미니게임 3 정하는 방식으로 해야 해"): one panel at a time.
        // All three used to start cutting together (.55 / .65 / .75) and only stop in turn; now each starts 0.4 s after
        // the one before it stops, and every roll is the first one's (R597's, 1.85 s: 12 cuts, the last 0.30 s):
        // 0.55 to 2.4, 2.8 to 4.65, 5.05 to 6.9. A panel still waiting for its turn shows its picture dark under "? ? ?".
        // R612 (on a still of all three at "? ? ?": "맵이 선택 되기 전에 좀 더 어둡게 맵을 해줘", then on a sheet of .72 / .45
        // / .16: "고르기 확정 전까지는 다 제일 밑에 꺼 어두운 걸로 해"): every picture not picked yet, waiting or rolling, is
        // at UnpickedBright (rolling was .72, waiting R611's .25; each cut still flashes it 1.35x), and the picked one
        // comes up full, so it lights up as it locks.
        // R615 ("고르기 전 미니게임 밝기를 0.25로 올려"): .25 (R612 .16).
        const float UnpickedBright = .25f;
        // R613 ("카운트 다운 빼고 전체적으로 좀만 천천히 해줘"): everything up to the countdown about 1.2x slower, the hits
        // themselves (slides, pops, slices, shakes) as crisp as before: the panels come in 0.1 s apart (were 0.08), each
        // roll lasts 2.2 s (was 1.85) with its first cut 1.2x longer (HopTimes: 12 cuts, the last 0.35 s), 0.5 s between a
        // stop and the next roll (was 0.4): 0.7 to 2.9, 3.4 to 5.6, 6.1 to 8.3 (were 0.55 to 2.4, 2.8 to 4.65, 5.05 to 6.9).
        static readonly float[] PanelEnter = { .2f, .3f, .4f }, RollStart = { .7f, 3.4f, 6.1f }, RollLand = { 2.9f, 5.6f, 8.3f };
        const float PanelSlide = .22f;
        // The title slides in from TitleAt for TitleSlide, easing out past its place and back (EoBack, pulled back by
        // BackPull).
        const float TitleAt = .1f, TitleSlide = .24f, BackPull = 2.1f;
        // The seven section squares are full at these times (each mini-game's as its panel stops), filling for DotFill.
        // R597: moved with the stops (were 1.3 / 2.0 / 2.7 / 3.4 / 4.1 / 4.8 / 5.2). R611: with them again (were 1.5 / 2.4
        // / 3.3 / 4.2 / 5.1 / 6.0 / 6.4): an obstacle's about halfway through the next roll, the final's 0.4 s after the
        // last stop. R613: with the slower rolls (were 1.5 / 2.4 / 3.7 / 4.65 / 5.95 / 6.9 / 7.3), the final's 0.5 s after.
        static readonly float[] DotAt = { 1.8f, 2.9f, 4.5f, 5.6f, 7.2f, 8.3f, 8.8f };
        const float DotFill = .1f;
        // 3, 2, 1; the countdown shows CountIn before the 3 and the pictures dim with it. R592: a full second after the
        // last square (5.2 s) to take the whole course in before the countdown (the 3 came 0.4 s after it before).
        // R596 ("카운트 다운 시간도 좀 더 천천히"): the numbers 1.0 s apart (were 0.8 s, 6.2 / 7.0 / 7.8), the 3 0.4 s later.
        // R597: 1.2 s later with the slower stops (were 6.6 / 7.6 / 8.6), still 1.4 s after the last square. R611: 0.9 s
        // later with the last square (were 7.8 / 8.8 / 9.8), still 1.4 s after it. R613: the countdown itself unchanged
        // ("카운트 다운 빼고"), 1.5 s later with the slower picking (were 8.7 / 9.7 / 10.7).
        static readonly float[] CountAt = { 10.2f, 11.2f, 12.2f };
        const float CountIn = .25f;
        // Everything has been shown (the 1 has stood CountGo): the match scene may switch on (MatchLoader waits for it).
        // R596 tried switching it on under the reveal at 5.4 s, but in the editor switching on plus the warm-up froze
        // and stuttered for about 7 s and the 3, 2, 1 never showed ("지금 에디터에는 3 2 1 이 안나 오니까 전 처럼 하자"),
        // so it is after the countdown again, as before.
        const float CountGo = .5f;
        // In the screen's time: R625's versus scene (LoadingScreenView.Versus.cs) plays first, so every time above is
        // RevealOffset later on screen.
        public const float RevealShownSeconds = RevealOffset + 12.2f + CountGo;

        // ---- the stage (the sample's pixels) ----

        const float StageWide = 1280f, StageTall = 720f;
        const float PanelLeft = 36f, PanelStep = 400f, PanelTop = 104f, PanelWide = 420f, PanelTall = 520f, PanelLean = 34f;
        const float BandTall = 128f, BarTall = 10f;
        const float DotLeft = 96f, DotStep = 70f, DotTop = 666f, DotWide = 58f, DotTall = 14f;
        // the countdown (design C): the number (its box as tall as its font size), its shadow offset, and the row of
        // squares over it
        const float CountTop = 178f, CountSize = 330f, CountShadow = 10f;
        // R596: the squares sit over the number (they were at 560, over the panels' name band)
        const float CountSquareTop = 176f, CountSquareWide = 72f, CountSquareTall = 18f, CountSquareGap = 14f;
        const string RevealArt = "Loading/CourseReveal/";
        static readonly string[] PanelLabels = { "미니게임 1", "미니게임 2", "미니게임 3" };

        // C1: ebony, ivory and brass.
        static readonly Color RevealDim = MenuArt.Hex(0x0a0806), Brass = MenuArt.Hex(0xb88a3e), Ebony = MenuArt.Hex(0x15110d), Ivory = MenuArt.Hex(0xefe6d2),
                              PanelBase = MenuArt.Hex(0x05070c), SliceLight = MenuArt.Hex(0xfff6e2), DotOff = MenuArt.Hex(0x2e2620),
                              LabelBack = new Color(21 / 255f, 17 / 255f, 13 / 255f, .85f);

        sealed class RevealPanel
        {
            public int Index, Slot, Pick = -1;
            public VisualElement Root;
            public RSlant Face, Slice;
            public Label Name;
            public Texture2D[] Pictures = new Texture2D[0];
            public string[] Names = new string[0];
            // When each cut lands (the last is RollLand): the same for every round.
            public float[] Hops;
        }

        VisualElement reveal, revealBox, revealShake, revealStage, revealDim, revealTitle, revealCount;
        Label revealCountNumber, revealSub;
        readonly List<RSlant> revealCountSquares = new List<RSlant>();
        Texture2D revealVignette;
        readonly List<VisualElement> revealHides = new List<VisualElement>();
        readonly List<RevealPanel> revealPanels = new List<RevealPanel>();
        readonly List<RSlant> revealDots = new List<RSlant>();
        readonly Dictionary<string, Texture2D> revealArt = new Dictionary<string, Texture2D>();
        bool revealShown, revealSeedKnown;
        int revealSeed;

        // While the course reveal plays, the match scene waits to switch on (its freeze would stop the panels):
        // MatchLoader asks this.
        public bool RevealHolding => reveal != null && State.CourseKnown && State.Reveal >= 0f && State.Reveal < RevealShownSeconds;

        Texture2D Art(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!revealArt.TryGetValue(name, out var texture))
                revealArt[name] = texture = Resources.Load<Texture2D>(RevealArt + name);
            return texture;
        }

        // ---- building ----

        void BuildReveal(VisualElement root, VisualElement stage)
        {
            if (screen == null) return;
            revealStage = stage;
            // what the reveal covers: the team lists, the title, the VS, the buttons, the progress line and the tip
            foreach (var name in new[] { "load-side-white", "load-side-black", "load-shade", "load-roster-white", "load-roster-black", "load-start-note", "load-tip" })
            {
                var e = screen.Q<VisualElement>(name);
                if (e != null) revealHides.Add(e);
            }
            foreach (var cls in new[] { "load-head", "load-vs-row", "load-allin-row", "load-cancel-row", "load-progress" })
            {
                var e = screen.Q<VisualElement>(className: cls);
                if (e != null) revealHides.Add(e);
            }

            reveal = RLayer(screen);
            reveal.name = "course-reveal";
            reveal.style.display = DisplayStyle.None;
            // the study behind goes dark, deeper at the rim, as the reveal starts
            revealDim = RLayer(reveal);
            var dark = RevealDim;
            dark.a = .74f;
            revealDim.style.backgroundColor = dark;
            revealVignette = MenuMarks.Vignette(MenuArt.Hex(0x060403), .25f, .85f);
            RStretch(RLayer(revealDim), revealVignette);

            revealBox = RBox(reveal, 0f, 0f, StageWide, StageTall);
            revealBox.style.transformOrigin = new TransformOrigin(0f, 0f);
            revealShake = RBox(revealBox, 0f, 0f, StageWide, StageTall);
            var s = revealShake;

            // the title on an ivory plate
            revealTitle = new RSlant(14f, Ivory);
            revealTitle.style.position = Position.Absolute;
            revealTitle.style.left = 56f;
            revealTitle.style.top = 26f;
            var title = RLabel(revealTitle, "이번 판 미니게임", 40f, Ebony, true);
            title.style.position = Position.Relative;
            title.style.paddingLeft = 30f;
            title.style.paddingRight = 34f;
            title.style.paddingTop = 12f;
            title.style.paddingBottom = 10f;
            s.Add(revealTitle);

            int index = 0;
            for (int slot = 0; slot < PawnRushSections.SlotCount && index < PanelLabels.Length; slot++)
                if (PawnRushSections.Kind[slot] == PawnRushSections.KindMiniGame) revealPanels.Add(BuildPanel(s, index++, slot));

            // the seven section squares
            for (int k = 0; k < DotAt.Length; k++)
            {
                var dot = new RSlant(6f, DotOff);
                RPlace(dot, DotLeft + k * DotStep, DotTop, DotWide, DotTall);
                dot.Bar(0f, DotTall, k == DotAt.Length - 1 ? Ivory : k % 2 == 1 ? Brass : Ivory);
                s.Add(dot);
                revealDots.Add(dot);
            }

            // once everything is shown, a PC still loading says so here
            revealSub = RLabel(s, "", 15f, new Color(Ivory.r, Ivory.g, Ivory.b, .7f), false);
            RPlace(revealSub, 700f, 661f, 510f, 24f);
            revealSub.style.unityTextAlign = TextAnchor.MiddleRight;

            // the countdown (R594, design C from the countdown page): a huge ivory number with a hard brass shadow, no
            // plate, and three slanted brass squares over it that go dark one per number. R598 took out the "경기 시작"
            // line over them ("자연스럽지가 않아 경기 시작 문구는 빼": it ran into the middle panel's "미니게임 2" tag).
            revealCount = RBox(s, 0f, 0f, StageWide, StageTall);
            revealCount.style.opacity = 0f;
            revealCountNumber = RLabel(revealCount, "3", CountSize, Ivory, false);
            RPlace(revealCountNumber, 0f, CountTop, StageWide, CountSize);
            revealCountNumber.style.textShadow = new TextShadow { offset = new Vector2(CountShadow, CountShadow), blurRadius = 0f, color = Brass };
            var digits = RuntimePanels.TitleFont;
            if (digits != null) revealCountNumber.style.unityFont = digits;
            else RuntimePanels.Display(revealCountNumber);
            revealCountSquares.Clear();
            float squaresLeft = (StageWide - (CountAt.Length * CountSquareWide + (CountAt.Length - 1) * CountSquareGap)) / 2f;
            for (int q = 0; q < CountAt.Length; q++)
            {
                var square = new RSlant(12f, DotOff);
                RPlace(square, squaresLeft + q * (CountSquareWide + CountSquareGap), CountSquareTop, CountSquareWide, CountSquareTall);
                square.Bar(0f, CountSquareTall, Brass);
                square.BarFill(1f);
                revealCount.Add(square);
                revealCountSquares.Add(square);
            }
            // each number slams in from 2.1x over the squares, not under them
            revealCountNumber.BringToFront();
            // R625: the versus scene, over everything until its panels gather into these
            BuildVersus(s);
            WarmRevealClips();
        }

        RevealPanel BuildPanel(VisualElement parent, int index, int slot)
        {
            var p = new RevealPanel { Index = index, Slot = slot, Hops = HopTimes(RollStart[index], RollLand[index]) };
            p.Root = RBox(parent, PanelLeft + index * PanelStep, PanelTop, PanelWide, PanelTall);
            p.Root.style.opacity = 0f;
            // the picture, the name band across its foot and the brass bar on the band's top edge, one slanted shape
            p.Face = new RSlant(PanelLean, PanelBase);
            RPlace(p.Face, 0f, 0f, PanelWide, PanelTall);
            p.Face.Band(PanelTall - BandTall, Ebony);
            p.Face.Bar(PanelTall - BandTall, BarTall, Brass);
            p.Root.Add(p.Face);

            var label = RLabel(p.Root, PanelLabels[index], 22f, Ivory, true);
            label.style.left = 44f;
            label.style.top = 18f;
            label.style.backgroundColor = LabelBack;
            label.style.paddingLeft = label.style.paddingRight = 12f;
            label.style.paddingTop = label.style.paddingBottom = 7f;

            // the name, kept inside the band while it slides in
            var band = RBox(p.Root, 10f, PanelTall - BandTall, PanelWide - 20f, BandTall);
            band.style.overflow = Overflow.Hidden;
            p.Name = RLabel(band, "? ? ?", 46f, Ivory, true);
            RPlace(p.Name, 20f, 46f, 350f, 46f);

            // the light slice that crosses the panel as it stops
            p.Slice = new RSlant(PanelLean, Color.clear);
            RPlace(p.Slice, 0f, 0f, PanelWide, PanelTall);
            p.Slice.SliceLook(70f, 20f, -14f, SliceLight);
            p.Root.Add(p.Slice);
            return p;
        }

        // This round's draw: each panel's pictures and names, and the one it stops on.
        void FillReveal(int seed)
        {
            revealSeedKnown = true;
            revealSeed = seed;
            var table = PawnRushCourseNames.Table;
            var picks = table.Picks(seed);
            foreach (var p in revealPanels)
            {
                var names = table.Names(p.Slot);
                var keys = table.Keys(p.Slot);
                p.Names = new string[names.Count];
                p.Pictures = new Texture2D[names.Count];
                for (int k = 0; k < names.Count; k++)
                {
                    p.Names[k] = names[k];
                    p.Pictures[k] = k < keys.Count ? Art(keys[k]) : null;
                }
                p.Pick = p.Slot < picks.Length && picks[p.Slot] < names.Count ? picks[p.Slot] : -1;
            }
            // which panels cut follows the draw
            revealCues = null;
        }

        // ---- each frame ----

        void SyncReveal()
        {
            if (reveal == null || screen == null) return;
            float t = State.CourseKnown ? State.Reveal : -1f;
            bool on = t >= 0f;
            if (on != revealShown)
            {
                revealShown = on;
                reveal.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                // hidden by visibility, since the view itself turns some of them on and off by display every frame
                var hidden = on ? new StyleEnum<Visibility>(Visibility.Hidden) : new StyleEnum<Visibility>(StyleKeyword.Null);
                foreach (var e in revealHides) e.style.visibility = hidden;
                // the study behind goes soft and a little grey, a little larger so its blurred edge stays off screen
                if (revealStage != null)
                {
                    revealStage.style.filter = on ? new StyleList<FilterFunction>(new List<FilterFunction> { BlurFilter(10f), GreyFilter(.35f) })
                                                  : new StyleList<FilterFunction>(StyleKeyword.Null);
                    revealStage.style.scale = on ? new Scale(new Vector2(1.05f, 1.05f)) : new StyleScale(StyleKeyword.Null);
                }
            }
            if (!on) { StopRevealSound(); ResetVersus(); return; }
            if (!revealSeedKnown || revealSeed != State.CourseSeed) FillReveal(State.CourseSeed);
            FitReveal();
            // R625: `t` is the screen's time (the versus scene first), `r` the reveal's own (LoadingScreenView.Versus.cs)
            // (the versus scene first: its panels are the roster the sound's cues are built from)
            bool versusOn = SyncVersus(t);
            SyncRevealSound(t);
            float r = RevealLocal(t);

            revealDim.style.opacity = Eo3(Ph(t, 0f, .2f));
            // while the versus scene plays it moves the title in itself
            if (!versusOn)
            {
                revealTitle.style.opacity = r < TitleAt ? 0f : 1f;
                revealTitle.style.translate = new Translate(-700f * (1f - EoBack(Ph(r, TitleAt, TitleSlide))), 0f);
            }

            var shake = Vector2.zero;
            foreach (var p in revealPanels)
            {
                shake += SyncPanel(p, r, t);
                if (versusOn) p.Root.style.opacity = 0f;
            }
            for (int k = 0; k < revealDots.Count; k++)
            {
                revealDots[k].BarFill(Eo3(Ph(r, DotAt[k] - DotFill, DotFill)));
                // the squares come up once the versus panels have gathered
                float up = Ph(t, VersusMerge + .25f + k * .03f, .1f);
                revealDots[k].style.opacity = up;
                revealDots[k].style.translate = new Translate(0f, 20f * (1f - Eo3(up)));
            }
            shake += SyncCount(r);
            revealShake.style.translate = new Translate(shake.x, shake.y);

            string sub = t >= RevealShownSeconds && !State.Matching && !string.IsNullOrEmpty(State.Step)
                ? State.Step + "  ·  " + State.Percent + "%" : "";
            if (revealSub.text != sub) revealSub.text = sub;
        }

        // The sample's stage, scaled to fit the screen and centred.
        void FitReveal()
        {
            float w = screen.layout.width, h = screen.layout.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0f || h <= 0f) return;
            float k = Mathf.Min(w / StageWide, h / StageTall);
            revealBox.style.left = (w - StageWide * k) / 2f;
            revealBox.style.top = (h - StageTall * k) / 2f;
            revealBox.style.scale = new Scale(new Vector2(k, k));
        }

        // One panel at the reveal's time `t` (`screen`: the screen's); returns its share of the screen's shake.
        Vector2 SyncPanel(RevealPanel p, float t, float screen)
        {
            int i = p.Index, count = p.Names.Length;
            float a = PanelEnter[i], from = i % 2 == 1 ? 1f : -1f;
            // R625: no landing shake: the panels no longer slide in (the versus scene's gather into them), and `t` holds at
            // RevealJoin, where it would have stood still mid-shake
            var settle = Vector2.zero;
            int j = HopIndex(p.Hops, t), last = p.Hops.Length - 1;
            bool known = count > 0 && p.Pick >= 0;
            int shown = !known ? -1 : j >= 0 ? Mod(p.Pick - (last - j), count) : Mod(p.Pick + 1, count);
            float hopAge = j >= 0 ? t - p.Hops[j] : 1f, age = t - RollLand[i];
            bool locked = known && age >= 0f;
            // every cut jolts the panel sideways (alternately) and flashes the picture a little brighter
            float jolt = !locked && j >= 0 ? 12f * (1f - Ph(hopAge, 0f, .06f)) * (j % 2 == 1 ? 1f : -1f) : 0f;
            float pop = locked ? 1f + .045f * (1f - Eo3(Ph(age, 0f, .22f))) : 1f;
            p.Root.style.opacity = t < a ? 0f : 1f;
            p.Root.style.translate = new Translate(settle.x + jolt, from * 760f * (1f - Eo3(Ph(t, a, PanelSlide))) + settle.y);
            p.Root.style.scale = new Scale(new Vector2(pop, pop));

            float cut = !locked && j >= 0 ? 1f + .35f * (1f - Ph(hopAge, 0f, .05f)) : 1f;
            float dim = t >= CountAt[0] - CountIn ? .45f * Eo3(Ph(t, CountAt[0] - CountIn, .2f)) : 0f;
            // R612: dark until it is picked, waiting for its turn (R611) or rolling
            p.Face.Picture(shown >= 0 ? p.Pictures[shown] : null, locked ? 1f - dim : UnpickedBright * cut);
            p.Face.BarFill(locked ? Eo3(Ph(age, .02f, .18f)) : 0f);

            string name = locked ? p.Names[p.Pick] : "? ? ?";
            if (p.Name.text != name) p.Name.text = name;
            p.Name.style.opacity = locked ? 1f : .4f;
            // R625: "? ? ?" slides in once the versus panels have gathered into this one
            p.Name.style.translate = new Translate(locked ? -50f * (1f - EoBack(Ph(age, .02f, .16f)))
                                                          : -380f * (1f - Eo3(Ph(screen, VersusMerge + .1f + i * .08f, .22f))), 0f);

            // R614 ("정해질 때 대각선 방향으로 흰색 효과가 나오는데 너무 빨리 돼"): the light slice crosses in SliceFor (was
            // .14 s), easing in and out, and fades over its last third
            const float SliceFor = .42f;
            float sweep = Ph(age, 0f, SliceFor);
            sweep = sweep * sweep * (3f - 2f * sweep);
            float sliceLight = locked && age < SliceFor ? .85f * Mathf.Clamp01((1f - Ph(age, 0f, SliceFor)) * 3f) : 0f;
            p.Slice.SliceAt(-90f + 560f * sweep, sliceLight);
            return locked ? Shake(age, 6f, .2f, i + 5) : Vector2.zero;
        }

        // 3, 2, 1 in the middle; returns the shake each number gives the screen as it lands.
        Vector2 SyncCount(float t)
        {
            int k = -1;
            for (int q = 0; q < CountAt.Length; q++) if (t >= CountAt[q]) k = q;
            float from = CountAt[0] - CountIn;
            revealCount.style.opacity = t >= from ? 1f : 0f;
            string number = k < 0 ? "3" : (3 - k).ToString();
            if (revealCountNumber.text != number) revealCountNumber.text = number;
            float age = k < 0 ? 1f : t - CountAt[k];
            // before the 3 lands the number settles from 1.3x and fades in; each number then slams from 2.1x
            float scale = k < 0 ? 1.3f - .3f * Eo3(Ph(t, from, .2f)) : 1f + 1.1f * (1f - Eo3(Ph(age, 0f, .13f)));
            revealCountNumber.style.scale = new Scale(new Vector2(scale, scale));
            revealCountNumber.style.opacity = k < 0 ? Ph(t, from, .15f) : Ph(age, 0f, .05f);
            // one square goes dark per number: 3 lit before the 3, two after it, none after the 1
            int lit = CountAt.Length - (k + 1);
            for (int q = 0; q < revealCountSquares.Count; q++) revealCountSquares[q].BarFill(q < lit ? 1f : 0f);
            return k >= 0 ? Shake(age, 14f, .18f, k + 11) : Vector2.zero;
        }

        void DisposeReveal()
        {
            revealArt.Clear();
            if (revealVignette != null) Destroy(revealVignette);
            // R625: the versus scene's 3D stage and its texture
            DisposeVersus();
        }

        // ---- timing helpers (the sample's) ----

        // When each cut of a roll from `start` to `land` lands: a short first cut, each one 1.15 times the last, scaled
        // so the last lands on `land`.
        static float[] HopTimes(float start, float land)
        {
            // R597: first cut .065 s (was .045 s): the rolls flicker a little slower and their last cuts are longer.
            // R613: .078 s (1.2x, with the 1.2x longer rolls).
            const float first = .078f, grow = 1.15f;
            int hops = 1;
            float sum = first;
            while (sum < land - start) { sum += first * Mathf.Pow(grow, hops); hops++; }
            float k = (land - start) / sum, at = start;
            var times = new float[hops + 1];
            times[0] = start;
            for (int j = 0; j < hops; j++) { at += first * Mathf.Pow(grow, j) * k; times[j + 1] = at; }
            times[hops] = land;
            return times;
        }

        static int HopIndex(float[] times, float t)
        {
            int j = -1;
            for (int i = 0; i < times.Length; i++) if (times[i] <= t) j = i;
            return j;
        }

        static int Mod(int a, int n) => ((a % n) + n) % n;

        // How far through a beat that starts at `from` and lasts `length`, 0 to 1.
        static float Ph(float t, float from, float length) => Mathf.Clamp01((t - from) / length);

        static float Eo3(float x) => 1f - Mathf.Pow(1f - x, 3f);

        static float EoBack(float x)
        {
            const float c3 = BackPull + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + BackPull * Mathf.Pow(x - 1f, 2f);
        }

        static Vector2 Shake(float age, float amount, float length, int seed)
        {
            if (age < 0f || age > length) return Vector2.zero;
            float k = Mathf.Pow(1f - age / length, 2f) * amount;
            return new Vector2(Mathf.Sin(age * 97f + seed * 3.1f) * k, Mathf.Cos(age * 83f + seed * 1.7f) * k);
        }

        static FilterFunction BlurFilter(float sigma)
        {
            var filter = new FilterFunction(FilterFunctionType.Blur);
            filter.AddParameter(new FilterParameter(sigma));
            return filter;
        }

        static FilterFunction GreyFilter(float amount)
        {
            var filter = new FilterFunction(FilterFunctionType.Grayscale);
            filter.AddParameter(new FilterParameter(amount));
            return filter;
        }

        static VisualElement BuildMatchAnnouncement(VisualElement parent, string mode, float top, float size, bool compact = false,
                                                    string headlineText = "상대를 찾았습니다", string subtitleText = "곧 대결이 시작됩니다")
        {
            var header = RBox(parent, 0f, top, StageWide, compact ? 88f : 104f);
            header.name = "match-announcement";
            var modeLabel = RLabel(header, mode, 15f, Brass, false);
            RPlace(modeLabel, StageWide / 2f - 120f, 0f, 240f, 20f);

            var headline = RLabel(header, headlineText, size, Ivory, true);
            headline.name = "match-announcement-title";
            RPlace(headline, 40f, compact ? 20f : 24f, StageWide - 80f, compact ? 44f : 52f);
            headline.style.textShadow = new TextShadow { offset = new Vector2(0f, 2f), blurRadius = 8f, color = new Color(0f, 0f, 0f, .6f) };
            if (!string.IsNullOrEmpty(subtitleText))
            {
                var subtitle = RLabel(header, subtitleText, compact ? 16f : 18f, Ivory, false);
                subtitle.name = "match-announcement-subtitle";
                RPlace(subtitle, 40f, compact ? 64f : 78f, StageWide - 80f, compact ? 22f : 26f);
                subtitle.style.opacity = .8f;
            }
            return header;
        }

        static void SyncMatchAnnouncement(VisualElement header, float age)
        {
            header.style.opacity = Ph(age, 0f, .28f);
            header.style.translate = new Translate(0f, 14f * (1f - Eo3(Ph(age, 0f, .32f))));
        }

        // ---- building blocks (sample pixels) ----

        static VisualElement RBox(VisualElement parent, float x, float y, float w, float h)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            RPlace(e, x, y, w, h);
            parent?.Add(e);
            return e;
        }

        static VisualElement RLayer(VisualElement parent)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = e.style.top = e.style.right = e.style.bottom = 0f;
            parent?.Add(e);
            return e;
        }

        static void RPlace(VisualElement e, float x, float y, float w, float h)
        {
            e.style.position = Position.Absolute;
            e.style.left = x;
            e.style.top = y;
            e.style.width = w;
            e.style.height = h;
        }

        static void RStretch(VisualElement e, Texture2D image)
        {
            if (image == null) return;
            e.style.backgroundImage = new StyleBackground(image);
            e.style.backgroundSize = new BackgroundSize(new Length(100f, LengthUnit.Percent), new Length(100f, LengthUnit.Percent));
        }

        // A one-line label in the sample's style: no margins or padding, centred, no shadow.
        static Label RLabel(VisualElement parent, string text, float size, Color color, bool display)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.fontSize = size;
            label.style.color = color;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0f;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0f;
            if (display) RuntimePanels.Display(label);
            parent?.Add(label);
            return label;
        }

        // A parallelogram like the sample's clip-path polygon(L 0, 100% 0, calc(100% - L) 100%, 0 100%): the top edge
        // starts Lean px in from the left, the bottom edge stops Lean px short of the right (UI Toolkit has no clip-path).
        // Inside its shape, in this order: its fill, a picture cropped to cover it (tinted darker or brighter), a band
        // across its foot, a bar filled from the left and a skewed light slice.
        sealed class RSlant : VisualElement
        {
            readonly float lean;
            Color fill;
            // R625: any texture (the versus scene's panels show its 3D stage's render texture), and optionally a part of it
            // (pictureUv, in the texture's 0..1 with v up) stretched over the shape's box instead of cropped to cover it
            Texture picture;
            Rect pictureUv;
            bool pictureUvSet;
            float edgeWide;
            Color edge;
            float bright = 1f;
            float bandTop = -1f;
            Color band;
            float barTop, barTall, barFill;
            Color bar;
            float sliceWide, sliceOver, sliceLean, sliceX, sliceAlpha;
            Color slice;

            static readonly List<Vector2> Shape = new List<Vector2>(4), Cut = new List<Vector2>(12), Spare = new List<Vector2>(12);

            public RSlant(float lean, Color fill)
            {
                this.lean = lean;
                this.fill = fill;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            // R624: the Sword Fight pick cards change colour with their state (free, marked, mine, taken).
            public void SetFill(Color color)
            {
                if (color == fill) return;
                fill = color;
                MarkDirtyRepaint();
            }

            public void Picture(Texture image, float brightness)
            {
                brightness = Mathf.Round(brightness * 200f) / 200f;
                if (image == picture && brightness == bright) return;
                picture = image;
                bright = brightness;
                MarkDirtyRepaint();
            }

            // R625: show only this part of the picture (0..1, v up), stretched over the shape's box.
            public void PictureRegion(Rect uv)
            {
                if (pictureUvSet && uv == pictureUv) return;
                pictureUv = uv;
                pictureUvSet = true;
                MarkDirtyRepaint();
            }

            // R625: a thin light line down the slanted left edge, `wide` px, as a bevel catching the light.
            public void Edge(float wide, Color color)
            {
                edgeWide = wide;
                edge = color;
                MarkDirtyRepaint();
            }

            public void Band(float top, Color color)
            {
                bandTop = top;
                band = color;
                MarkDirtyRepaint();
            }

            public void Bar(float top, float tall, Color color)
            {
                barTop = top;
                barTall = tall;
                bar = color;
                MarkDirtyRepaint();
            }

            public void BarFill(float amount)
            {
                if (amount == barFill) return;
                barFill = amount;
                MarkDirtyRepaint();
            }

            // A band `wide` across reaching `over` past the top and bottom, skewed by `skew` degrees about its middle
            // (CSS skewX), in `color`.
            public void SliceLook(float wide, float over, float skew, Color color)
            {
                sliceWide = wide;
                sliceOver = over;
                sliceLean = Mathf.Tan(skew * Mathf.Deg2Rad);
                slice = color;
            }

            public void SliceAt(float x, float alpha)
            {
                if (alpha <= 0f && sliceAlpha <= 0f) return;
                if (x == sliceX && alpha == sliceAlpha) return;
                sliceX = x;
                sliceAlpha = alpha;
                MarkDirtyRepaint();
            }

            void Draw(MeshGenerationContext context)
            {
                float w = layout.width, h = layout.height;
                if (float.IsNaN(w) || float.IsNaN(h) || w <= 0f || h <= 0f) return;
                Shape.Clear();
                Shape.Add(new Vector2(lean, 0f));
                Shape.Add(new Vector2(w, 0f));
                Shape.Add(new Vector2(w - lean, h));
                Shape.Add(new Vector2(0f, h));

                if (fill.a > 0f) Fan(context, Shape, fill, null, w, h);
                if (picture != null && bright > 0f)
                    Fan(context, Shape, new Color(bright, bright, bright, 1f), picture, w, h, pictureUvSet ? pictureUv : (Rect?)null);
                if (bandTop >= 0f && band.a > 0f) Clipped(context, 0f, bandTop, w, h, 0f, band, w, h);
                if (bar.a > 0f && barFill > 0f) Clipped(context, 0f, barTop, w * barFill, barTop + barTall, 0f, bar, w, h);
                if (slice.a > 0f && sliceAlpha > 0f)
                {
                    var c = slice;
                    c.a *= sliceAlpha;
                    Clipped(context, sliceX, -sliceOver, sliceX + sliceWide, h + sliceOver, sliceLean, c, w, h);
                }
                // the left edge runs from (lean, 0) to (0, h): a band from lean / 2 skewed by -lean / h about its middle
                if (edgeWide > 0f && edge.a > 0f) Clipped(context, lean / 2f, 0f, lean / 2f + edgeWide, h, -lean / h, edge, w, h);
            }

            // The rectangle (x0, y0)-(x1, y1), skewed by `skew` about its middle, cut to the shape and drawn.
            void Clipped(MeshGenerationContext context, float x0, float y0, float x1, float y1, float skew, Color color, float w, float h)
            {
                float mid = (y0 + y1) / 2f;
                Cut.Clear();
                Cut.Add(new Vector2(x0 + skew * (y0 - mid), y0));
                Cut.Add(new Vector2(x1 + skew * (y0 - mid), y0));
                Cut.Add(new Vector2(x1 + skew * (y1 - mid), y1));
                Cut.Add(new Vector2(x0 + skew * (y1 - mid), y1));
                // Sutherland-Hodgman against each edge of the (convex, clockwise on screen) shape
                for (int e = 0; e < Shape.Count && Cut.Count > 0; e++)
                {
                    Vector2 a = Shape[e], b = Shape[(e + 1) % Shape.Count];
                    Spare.Clear();
                    for (int k = 0; k < Cut.Count; k++)
                    {
                        Vector2 p = Cut[k], q = Cut[(k + 1) % Cut.Count];
                        float sp = Side(a, b, p), sq = Side(a, b, q);
                        if (sp >= 0f) Spare.Add(p);
                        if ((sp >= 0f) != (sq >= 0f)) Spare.Add(Vector2.Lerp(p, q, sp / (sp - sq)));
                    }
                    Cut.Clear();
                    Cut.AddRange(Spare);
                }
                if (Cut.Count >= 3) Fan(context, Cut, color, null, w, h);
            }

            static float Side(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

            // A convex polygon (clockwise on screen) as a triangle fan; with a picture, cropped to cover w x h, or (`part`)
            // that part of it stretched over w x h.
            static void Fan(MeshGenerationContext context, List<Vector2> points, Color color, Texture image, float w, float h, Rect? part = null)
            {
                int n = points.Count;
                var mesh = context.Allocate(n, (n - 2) * 3, image);
                var region = mesh.uvRegion;
                float su = 1f, sv = 1f;
                if (image != null && image.height > 0)
                {
                    float picture = (float)image.width / image.height, frame = w / h;
                    if (picture > frame) su = frame / picture;
                    else sv = picture / frame;
                }
                Color32 tint = color;
                for (int k = 0; k < n; k++)
                {
                    var p = points[k];
                    var uv = Vector2.zero;
                    if (image != null)
                    {
                        float u, v;
                        if (part.HasValue)
                        {
                            u = part.Value.x + p.x / w * part.Value.width;
                            v = part.Value.y + (1f - p.y / h) * part.Value.height;
                        }
                        else
                        {
                            u = .5f + (p.x / w - .5f) * su;
                            v = .5f - (p.y / h - .5f) * sv;
                        }
                        uv = new Vector2(region.x + u * region.width, region.y + v * region.height);
                    }
                    mesh.SetNextVertex(new Vertex { position = new Vector3(p.x, p.y, Vertex.nearZ), tint = tint, uv = uv });
                }
                for (int k = 1; k < n - 1; k++)
                {
                    mesh.SetNextIndex(0);
                    mesh.SetNextIndex((ushort)k);
                    mesh.SetNextIndex((ushort)(k + 1));
                }
            }
        }
    }
}
