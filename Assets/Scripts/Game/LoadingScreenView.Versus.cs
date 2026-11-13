using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // R625 (11-13): Pawn Rush's versus scene, between finding the players and the mini-game reveal ("현재 매칭씬 구조가
    // 플레이어 찾기 후 이번 판 미니 게임 소개 인데 그 사이에 뭔가 매칭스러운 씬을 추가 하고 싶어"). Of five samples
    // (https://claude.ai/artifact/749X7nB5FUHKMR6MYhhQxn) the user picked D "슬랜트 로스터", asked to match the reveal's look,
    // last about six seconds and be the high-effort one ("와 잘만들었다 수준으로").
    //
    // Second look, the same day (the first was flat panels with a drawn pawn icon: "뭔가 사진으로 그냥 가져다 붙여 놓은 거
    // 같은 느낌이야 퀄리티가 부족해"): the panels are windows on a lit 3D stage (VersusStage): the players' own pawns in a
    // row on a maple and walnut board, white's turned toward black's, each with a rim light in its team's colour. The whole
    // stage, dimmed, fills the screen from the start, the pawns standing in the dark; then one panel per player, as tall and
    // as slanted as the reveal's (its ebony band and bar), slams down in turn, alternately from above and below, stretching
    // as it falls and bouncing as it lands, a thin glint crossing it, and lights its player, who hops at the hit. Each
    // panel throws a shadow and catches the light on its slanted edge; the name runs up its left edge, the number and the
    // team sit in the band. This PC's panel gets a brass frame and its bar fills while its pawn throws its arms up. A
    // brass VS panel slams into the gap between the teams, both teams' panels jolt apart and every pawn takes its stance.
    // Then the panels gather, a few at a time, into the reveal's three panels, the title plate's "이번 판 대결" swaps to
    // "이번 판 미니게임", and the reveal holds its first frame ("? ? ?") until VersusSeconds and plays on from there.
    //
    // Like the reveal, every frame is worked out from the time since the host started (LoadingState.Reveal, the shared
    // clock) and the roster as it stood when the scene began, so every PC shows the same frame at the same moment.
    public sealed partial class LoadingScreenView
    {
        // ---- timing (seconds from the host's start: the screen's time) ----

        // The versus scene runs from 0 to VersusMerge, where its panels have gathered into the reveal's; the reveal shows
        // its frame RevealJoin (title and panels in place, nothing rolling yet) from then and plays on from it at
        // VersusSeconds. So the reveal's own times (CountAt, RollLand...) are VersusSeconds - RevealJoin later on screen.
        // R625 (the user on the second look: "중간에 사이에 있는 걸 좀만 더 천천히 해"): every beat of the scene Pace times
        // later than it was (the panels come in 1.2x further apart, the gathering takes 1.2x as long), the hits themselves
        // (drops, bounces, glints, shakes) as crisp as before: the numbers below are the second look's, times Pace.
        const float Pace = 1.2f;
        public const float VersusSeconds = 5.6f * Pace;
        const float VersusMerge = 5.05f * Pace, RevealJoin = .62f;
        const float RevealOffset = VersusSeconds - RevealJoin;

        // The reveal's time at the screen's time `t` (below 0: not showing). Its entrance (title and panels sliding in) is
        // never played: the versus scene ends in the frame it would have ended in.
        static float RevealLocal(float t) => t < 0f ? -1f : Mathf.Max(RevealJoin, t - RevealOffset);

        // The stage fades up; the title plate and the counts plate slide in; the panels drop from StripAt, one team's next
        // player StripStep after the last, black's StripBlackLag after white's, each for StripDrop, bounce for StripBounce;
        // the glint crosses SliceDelay after the drop starts, for StripSlice.
        const float StageIn = .3f * Pace, VTitleAt = .1f * Pace, VTeamsAt = .2f * Pace, VPlateSlide = .26f;
        const float StripAt = .42f * Pace, StripStep = .15f * Pace, StripBlackLag = .075f * Pace, StripDrop = .2f, StripBounce = .18f, SliceDelay = .17f, StripSlice = .3f;
        // This PC's panel: its frame shows, its bar fills, its pawn cheers.
        const float MeAt = 2f * Pace;
        // The VS panel (still called the blade: the sound's cues name it so) drops from BladeAt and lands at VsAt; the
        // screen shakes and both teams' panels jolt TeamJolt px apart. Then a slow push in until the gathering.
        const float BladeAt = 2.28f * Pace, BladeDrop = .16f, VsAt = BladeAt + BladeDrop, TeamJolt = 16f, PushAt = 2.6f * Pace, PushFor = 1.6f * Pace;
        // The way out: the VS panel drops away, the panels' insides fade, the counts plate leaves, the panels gather, the
        // stage fades, and the title swaps (half out, half in).
        const float BladeOut = 4.25f * Pace, InsideOut = 4.3f * Pace, TeamsOut = 4.35f * Pace, MergeAt = 4.35f * Pace, TitleSwapAt = 4.45f * Pace, TitleSwap = .34f;

        // ---- layout (the stage's pixels) ----

        // Each team's panels fill its half (white 36..594, black 686..1244), centred, StripGap apart. A slanted panel's box
        // is its face's width plus the lean (the face is a parallelogram), so the faces are StripGap apart: six a side are
        // 82 wide on screen (116 boxes); fewer are wider, at most StripMax. The VS panel fills the gap between the halves
        // (its face 114 wide).
        const float SideWhite = 36f, SideBlack = 686f, SideWide = 558f, StripGap = 6f, StripMax = 150f;
        const float VsLeft = 566f, VsWide = 148f;
        // The pawn's feet (stage y), on the panel's middle, its turn toward the other team (radians), and its height on
        // the stage for a six-a-side face (wider faces scale it up, VersusStage.PawnPixels).
        const float FeetY = 470f, FeetRight = 0f, PawnTurn = .45f, FaceSix = 82f;
        // The name runs up the panel's left edge from above the pawn's head; its size shrinks for a long name from
        // NameSize to fit.
        const float NameSize = 26f, NameMin = 15f;
        // The panel's shadow on the stage behind it.
        static readonly Vector2 StripShadow = new Vector2(10f, 14f);

        static readonly Color StripBase = MenuArt.Hex(0x0b0806), ShadowColor = new Color(0f, 0f, 0f, .55f);
        static readonly Color EdgeLight = new Color(1f, .95f, .85f, .38f), TagColor = new Color(.937f, .902f, .824f, .62f);

        sealed class VersusStrip
        {
            public VisualElement Root, Inside, Shadow, FrameBox;
            public RSlant Face, Frame;
            public float X, W, At, Dir;
            public int Pos, Panel, Team, Order;
            public bool Me;
        }

        VisualElement versus, versusBox, versusBackdrop;
        VisualElement versusTitle;
        RSlant versusTeams, versusBlade;
        VisualElement versusBladeBox, versusBladeShadow;
        Label versusTeamsText;
        readonly List<Label> versusLetters = new List<Label>();
        // R624 (사용자 "vs 문구에 코팅처럼 … 폰러쉬 vs문구도 같이"): a lacquer coat on V and S (TextCoat)
        readonly List<TextCoat> versusCoats = new List<TextCoat>();
        readonly List<VersusStrip> versusStrips = new List<VersusStrip>();
        VersusStage versusStage;
        bool versusFilled;

        // ---- building ----

        void BuildVersus(VisualElement parent)
        {
            versus = RBox(parent, 0f, 0f, StageWide, StageTall);
            versus.style.display = DisplayStyle.None;
            versusBox = RBox(versus, 0f, 0f, StageWide, StageTall);
            versusBox.style.transformOrigin = new TransformOrigin(StageWide / 2f, 380f);

            // the 3D stage: built now (while matching), drawn only while the scene plays
            try
            {
                versusStage = new VersusStage();
                int width = Mathf.Clamp(Screen.width, 960, 1920);
                versusStage.Build(transform, width, Mathf.RoundToInt(width * StageTall / StageWide));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ChessFight] 대결 장면 3D 무대를 만들지 못했습니다: " + e.Message);
                versusStage?.Dispose();
                versusStage = null;
            }

            // the whole stage, dimmed, behind the panels, and the reveal's vignette over it
            versusBackdrop = RBox(versusBox, 0f, 0f, StageWide, StageTall);
            if (versusStage?.Texture != null)
            {
                versusBackdrop.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(versusStage.Texture));
                versusBackdrop.style.unityBackgroundImageTintColor = new Color(.4f, .4f, .4f, 1f);
            }
            else versusBackdrop.style.backgroundColor = StripBase;
            RStretch(RLayer(versusBackdrop), revealVignette);

            versusTitle = BuildMatchAnnouncement(versusBox, "폰 러쉬", 16f, 36f, true);

            versusTeams = new RSlant(14f, Ebony);
            versusTeams.style.position = Position.Absolute;
            versusTeams.style.right = 56f;
            versusTeams.style.top = 26f;
            versusTeamsText = RLabel(versusTeams, "", 30f, Ivory, true);
            versusTeamsText.style.position = Position.Relative;
            versusTeamsText.style.paddingLeft = 34f;
            versusTeamsText.style.paddingRight = 30f;
            versusTeamsText.style.paddingTop = 16f;
            versusTeamsText.style.paddingBottom = 14f;
            versusTeamsText.enableRichText = true;
            versusBox.Add(versusTeams);

            // the VS panel: brass, as tall and as slanted as the others, its own band, V over S in the countdown's face
            versusBladeShadow = RBox(null, VsLeft + StripShadow.x, PanelTop + StripShadow.y, VsWide, PanelTall);
            var bladeShadow = new RSlant(PanelLean, ShadowColor);
            Fill(bladeShadow);
            versusBladeShadow.Add(bladeShadow);
            versusBladeBox = RBox(null, VsLeft, PanelTop, VsWide, PanelTall);
            versusBladeBox.style.transformOrigin = new TransformOrigin(VsWide / 2f, PanelTall / 2f);
            versusBlade = new RSlant(PanelLean, Brass);
            Fill(versusBlade);
            versusBlade.Band(PanelTall - BandTall, Ebony);
            versusBlade.Bar(PanelTall - BandTall - BarTall, BarTall, Ivory);
            versusBlade.BarFill(1f);
            versusBlade.Edge(2.5f, new Color(1f, .97f, .88f, .6f));
            versusBlade.SliceLook(16f, 20f, -14f, SliceLight);
            versusBladeBox.Add(versusBlade);
            versusLetters.Clear();
            versusCoats.Clear();
            var font = RuntimePanels.TitleFont;
            string[] letters = { "V", "S" };
            float[] at = { 92f, 214f };
            const float LetterSize = 112f;
            for (int i = 0; i < letters.Length; i++)
            {
                var letter = RLabel(versusBladeBox, letters[i], LetterSize, Ebony, false);
                // centred on the slant's middle at the letter's height
                float mid = at[i] + LetterSize / 2f, shift = PanelLean * (.5f - mid / PanelTall);
                RPlace(letter, shift, at[i], VsWide, LetterSize);
                letter.style.textShadow = new TextShadow { offset = new Vector2(5f, 5f), blurRadius = 0f, color = new Color(1f, .93f, .78f, .55f) };
                if (font != null) letter.style.unityFont = font;
                else RuntimePanels.Display(letter);
                letter.style.transformOrigin = new TransformOrigin(VsWide / 2f, LetterSize / 2f);
                versusLetters.Add(letter);
                versusCoats.Add(TextCoat.Apply(letter, VsWide, LetterSize, new Color(1f, .95f, .84f, .32f), new Color(1f, .97f, .9f, .85f), 24f, 124f));
            }
            float duelMid = PanelTall - BandTall / 2f;
            var duel = RLabel(versusBladeBox, "대결", 24f, Ivory, true);
            RPlace(duel, PanelLean * (.5f - duelMid / PanelTall), duelMid - 16f, VsWide, 32f);
            // the reveal's title comes in over this scene as it swaps (SyncVersus)
            revealTitle?.BringToFront();
        }

        void DisposeVersus()
        {
            versusStage?.Dispose();
            versusStage = null;
        }

        // The panels for the roster as it stands now; the scene keeps them whoever leaves or comes meanwhile.
        void FillVersus()
        {
            versusFilled = true;
            foreach (var s in versusStrips)
            {
                s.Root.RemoveFromHierarchy();
                s.Shadow?.RemoveFromHierarchy();
                s.FrameBox?.RemoveFromHierarchy();
            }
            versusStrips.Clear();
            versusBladeShadow.RemoveFromHierarchy();
            versusBladeBox.RemoveFromHierarchy();
            versusStage?.HideAll();

            var white = new List<int>();
            var black = new List<int>();
            for (int i = 0; i < State.Teams.Count; i++) (State.Teams[i] == 0 ? white : black).Add(i);
            if (white.Count > VersusStage.PerTeam) white.RemoveRange(VersusStage.PerTeam, white.Count - VersusStage.PerTeam);
            if (black.Count > VersusStage.PerTeam) black.RemoveRange(VersusStage.PerTeam, black.Count - VersusStage.PerTeam);
            versusTeamsText.text = $"백 <color=#B88A3E>{white.Count}</color>   :   흑 <color=#B88A3E>{black.Count}</color>";

            int total = white.Count + black.Count;
            AddSide(white, SideWhite, false, 0, total);
            AddSide(black, SideBlack, true, white.Count, total);

            versusBox.Add(versusBladeShadow);
            versusBox.Add(versusBladeBox);
            // the sound's cues follow these panels (one slam each)
            revealCues = null;
        }

        // One team's panels: white's first player outermost on the left, black's outermost on the right. `face` is a
        // panel's width on screen, its box that plus the lean.
        void AddSide(List<int> entries, float side, bool black, int before, int total)
        {
            int n = entries.Count;
            if (n == 0) return;
            float face = Mathf.Min(StripMax, (SideWide - PanelLean - StripGap * (n - 1)) / n);
            float group = n * face + (n - 1) * StripGap + PanelLean, left = side + (SideWide - group) / 2f;
            for (int k = 0; k < n; k++)
            {
                int column = black ? n - 1 - k : k, pos = before + column;
                float x = left + column * (face + StripGap);
                versusStrips.Add(BuildStrip(entries[k], k, black, x, face + PanelLean, pos, total));
            }
        }

        VersusStrip BuildStrip(int entry, int order, bool black, float x, float w, int pos, int total)
        {
            bool me = entry == State.Me;
            bool bot = entry < State.Bots.Count && State.Bots[entry];
            var s = new VersusStrip
            {
                X = x, W = w, Pos = pos, Me = me, Team = black ? 1 : 0, Order = order,
                Panel = total > 0 ? Mathf.Min(2, pos * 3 / total) : 0,
                At = StripAt + order * StripStep + (black ? StripBlackLag : 0f),
                Dir = pos % 2 == 1 ? 1f : -1f,
            };
            // its shadow on the stage, and this PC's brass frame, behind it
            s.Shadow = RBox(versusBox, x + StripShadow.x, PanelTop + StripShadow.y, w, PanelTall);
            var shadow = new RSlant(PanelLean, ShadowColor);
            Fill(shadow);
            s.Shadow.Add(shadow);
            if (me)
            {
                s.FrameBox = RBox(versusBox, x - 8f, PanelTop - 8f, w + 16f, PanelTall + 16f);
                s.Frame = new RSlant(PanelLean + 2f, Brass);
                Fill(s.Frame);
                s.Frame.Edge(3f, new Color(1f, .97f, .86f, .7f));
                s.FrameBox.Add(s.Frame);
            }

            s.Root = RBox(versusBox, x, PanelTop, w, PanelTall);
            s.Root.style.transformOrigin = new TransformOrigin(w / 2f, PanelTall / 2f);
            s.Face = new RSlant(PanelLean, StripBase);
            Fill(s.Face);
            if (versusStage?.Texture != null)
            {
                // its own part of the stage's picture: where its player stands
                s.Face.Picture(versusStage.Texture, 1f);
                s.Face.PictureRegion(new Rect(x / StageWide, 1f - (PanelTop + PanelTall) / StageTall, w / StageWide, PanelTall / StageTall));
            }
            s.Face.Band(PanelTall - BandTall, Ebony);
            s.Face.Bar(PanelTall - BandTall - BarTall, BarTall, me ? Brass : black ? Brass : Ivory);
            s.Face.BarFill(me ? 0f : 1f);
            s.Face.SliceLook(11f, 20f, -14f, SliceLight);
            s.Face.Edge(2f, EdgeLight);
            s.Root.Add(s.Face);

            s.Inside = RLayer(s.Root);
            // the pawn's size: a six-a-side face's, larger on a wider face
            float face = w - PanelLean, scale = Mathf.Clamp(face / FaceSix, 1f, 1.35f);
            // the name up the left edge, turned to its slant, from above the pawn's head
            float nameFoot = FeetY - PanelTop - VersusStage.ShownPawnPixels * scale - 14f, nameRoom = nameFoot - 16f;
            string name = entry < State.Names.Count ? (State.Names[entry] ?? "").Trim() : "";
            var label = RLabel(s.Inside, name, NameFit(name, nameRoom), me ? Brass : Ivory, true);
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.width = nameRoom;
            label.style.height = 32f;
            label.style.left = PanelLean * (1f - nameFoot / PanelTall) + 9f;
            label.style.top = nameFoot;
            label.style.transformOrigin = new TransformOrigin(0f, 0f);
            label.style.rotate = new Rotate(new Angle(-90f + Mathf.Atan2(PanelLean, PanelTall) * Mathf.Rad2Deg, AngleUnit.Degree));
            label.style.textShadow = new TextShadow { offset = new Vector2(2f, 2f), blurRadius = 0f, color = new Color(0f, 0f, 0f, .85f) };
            // the number big in the band, the team (or "나", or BOT) small under it
            float bandTop = PanelTall - BandTall;
            var number = RLabel(s.Inside, (order + 1).ToString(), 54f, me || black ? Brass : Ivory, false);
            var font = RuntimePanels.TitleFont;
            if (font != null) number.style.unityFont = font;
            else RuntimePanels.Display(number);
            number.style.unityTextAlign = TextAnchor.MiddleLeft;
            RPlace(number, 12f, bandTop + 12f, w - 16f, 58f);
            string tag = me ? "나" : (black ? "흑" : "백") + (bot ? " · BOT" : "");
            var team = RLabel(s.Inside, tag, me ? 20f : 15f, me ? Brass : TagColor, true);
            team.style.unityTextAlign = TextAnchor.MiddleLeft;
            RPlace(team, 10f, bandTop + 78f, w - 12f, 24f);

            // its player on the stage, where the panel shows it
            float feetX = x + w / 2f + PanelLean * (.5f - (FeetY - PanelTop) / PanelTall) + FeetRight;
            versusStage?.Stand(s.Team, order, new Vector2(feetX, FeetY), black ? PawnTurn : -PawnTurn, scale);
            return s;
        }

        // The name's size: smaller for a long one, so it fits `room` (a Hangul syllable about its size wide, a letter or
        // digit about .62 of it).
        static float NameFit(string name, float room)
        {
            float units = 0f;
            var e = StringInfo.GetTextElementEnumerator(name ?? "");
            while (e.MoveNext())
            {
                string c = e.GetTextElement();
                units += c.Length > 0 && c[0] >= 0x1100 && c[0] <= 0xD7A3 ? 1f : .62f;
            }
            return units <= 0f ? NameSize : Mathf.Clamp(room / units, NameMin, NameSize);
        }

        static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = e.style.top = 0f;
            e.style.width = e.style.height = new Length(100f, LengthUnit.Percent);
        }

        void ResetVersus()
        {
            versusFilled = false;
            if (versus != null) versus.style.display = DisplayStyle.None;
        }

        // ---- each frame ----

        // The versus scene at the screen's time `t`; true while it still covers the reveal (before VersusMerge).
        bool SyncVersus(float t)
        {
            if (versus == null) return false;
            bool on = t >= 0f && t < VersusMerge;
            versus.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on) return false;
            if (!versusFilled) FillVersus();

            // the stage: up from the dark, down again as the panels gather
            float merge = Eo3(Ph(t, MergeAt, VersusMerge - MergeAt));
            versusBackdrop.style.opacity = Eo3(Ph(t, 0f, StageIn)) * (1f - Ph(t, MergeAt, VersusMerge - MergeAt));

            // the plates: in from the sides, the counts out at the end, the title out and the reveal's in its place
            float swapHalf = TitleSwap / 2f;
            SyncMatchAnnouncement(versusTitle, t - VTitleAt);
            float titleOut = Ph(t, TitleSwapAt, swapHalf);
            versusTitle.style.opacity = Ph(t, VTitleAt, .28f) * (1f - titleOut);
            versusTeams.style.translate = new Translate(560f * (1f - EoBack(Ph(t, VTeamsAt, VPlateSlide))) + 600f * Io3(Ph(t, TeamsOut, .25f)), 0f);
            versusTeams.style.opacity = t < VTeamsAt ? 0f : 1f;
            float titleIn = TitleSwapAt + swapHalf;
            revealTitle.style.opacity = t < titleIn ? 0f : 1f;
            revealTitle.style.translate = new Translate(-700f * (1f - EoBack(Ph(t, titleIn, swapHalf))), 0f);

            // VS lands: the teams jolt apart and spring back
            float joltAge = t - VsAt, jolt = joltAge >= 0f ? TeamJolt * Mathf.Pow(Mathf.Max(0f, 1f - joltAge / .45f), 2f) * Mathf.Cos(joltAge * 14f) : 0f;

            // the panels: drop (stretching), bounce, glint, gather
            var shake = Vector2.zero;
            float inside = 1f - Ph(t, InsideOut, .22f);
            foreach (var s in versusStrips)
            {
                float k = Ph(t, s.At, StripDrop), landed = t - (s.At + StripDrop);
                float fall = s.Dir * 760f * (1f - Eo3(k));
                float bounce = landed >= 0f && landed < StripBounce ? -s.Dir * 10f * Mathf.Sin(Mathf.PI * landed / StripBounce) * (1f - landed / StripBounce) : 0f;
                float stretch = k > 0f && k < 1f ? 1f + .12f * Mathf.Sin(Mathf.PI * k) : 1f;
                bool shown = t >= s.At;
                float side = (s.Team == 0 ? -1f : 1f) * jolt * (1f - merge);
                float panelX = PanelLeft + s.Panel * PanelStep;
                float x = Mathf.Lerp(s.X, panelX, merge), w = Mathf.Lerp(s.W, PanelWide, merge);
                s.Root.style.left = x;
                s.Root.style.width = w;
                s.Root.style.translate = new Translate(side, fall + bounce);
                s.Root.style.scale = new Scale(new Vector2(1f, stretch));
                s.Root.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
                s.Shadow.style.left = x + StripShadow.x;
                s.Shadow.style.width = w;
                s.Shadow.style.translate = new Translate(side, fall + bounce);
                s.Shadow.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
                s.Shadow.style.opacity = 1f - merge;
                s.Inside.style.opacity = inside;
                s.Face.Picture(versusStage?.Texture, inside);
                // the edge's light goes with the inside, or the gathering panels stack their lines
                if (t >= InsideOut) s.Face.Edge(2f, new Color(EdgeLight.r, EdgeLight.g, EdgeLight.b, EdgeLight.a * inside));

                float age = t - (s.At + SliceDelay), sweep = Ph(age, 0f, StripSlice);
                s.Face.SliceAt(-40f + sweep * (s.W + 80f), age >= 0f && age < StripSlice ? .5f * (1f - sweep) : 0f);
                shake += Shake(landed, 2.4f, .16f, s.Pos);

                if (s.Me)
                {
                    s.FrameBox.style.left = x - 8f;
                    s.FrameBox.style.translate = new Translate(side, fall + bounce);
                    s.FrameBox.style.scale = new Scale(new Vector2(1f, stretch));
                    s.FrameBox.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
                    s.FrameBox.style.opacity = Ph(t, MeAt, .12f) * (1f - Ph(t, InsideOut, .2f));
                    s.Face.BarFill(landed < 0f ? 0f : Mathf.Max(.08f, Eo3(Ph(t, MeAt, .35f))));
                }
                versusStage?.Pose(s.Team, s.Order, PawnPose(s, t));
            }

            // the VS panel
            float vk = Ph(t, BladeAt, BladeDrop), vLanded = t - VsAt;
            float vFall = -760f * (1f - Eo3(vk)) + 760f * Io3(Ph(t, BladeOut, .22f));
            float vBounce = vLanded >= 0f && vLanded < .2f ? 12f * Mathf.Sin(Mathf.PI * vLanded / .2f) * (1f - vLanded / .2f) : 0f;
            float vStretch = vk > 0f && vk < 1f ? 1f + .16f * Mathf.Sin(Mathf.PI * vk) : 1f;
            bool vShown = t >= BladeAt && t < BladeOut + .22f;
            versusBladeBox.style.translate = new Translate(0f, vFall + vBounce);
            versusBladeBox.style.scale = new Scale(new Vector2(1f, vStretch));
            versusBladeBox.style.visibility = vShown ? Visibility.Visible : Visibility.Hidden;
            versusBladeShadow.style.translate = new Translate(0f, vFall + vBounce);
            versusBladeShadow.style.visibility = vShown ? Visibility.Visible : Visibility.Hidden;
            float vAge = t - (BladeAt + .12f), vSweep = Ph(vAge, 0f, .32f);
            versusBlade.SliceAt(-40f + vSweep * (VsWide + 80f), vAge >= 0f && vAge < .32f ? .8f * (1f - vSweep) : 0f);
            for (int i = 0; i < versusLetters.Count; i++)
            {
                // each letter pops after the landing, V then S
                float pop = Ph(t, VsAt + i * .06f, .2f), size = vLanded < 0f ? 1f : 1f + .35f * (1f - Eo3(pop));
                versusLetters[i].style.scale = new Scale(new Vector2(size, size));
                if (i < versusCoats.Count) versusCoats[i].Sync(t - VsAt - .3f - i * .08f);
            }
            shake += Shake(vLanded, 12f, .36f, 7);

            float push = 1f + .03f * Eo3(Ph(t, PushAt, PushFor)) * (1f - merge);
            versusBox.style.translate = new Translate(shake.x, shake.y * .5f);
            versusBox.style.scale = new Scale(new Vector2(push, push));

            versusStage?.Render();
            return true;
        }

        // A pawn on the stage at `t`: still in the dark, then a hop as its panel hits (crouch, up with its arms out, a
        // squash as it lands), breathing and swaying a little; this PC's throws its arms up and hops at MeAt; at VS every
        // pawn leans in with its arms up, ready.
        FigurePose PawnPose(VersusStrip s, float t)
        {
            float turn = s.Team == 1 ? PawnTurn : -PawnTurn;
            var p = new FigurePose { Yaw = turn, Arm = -.8f };
            // idle
            p.Squash = .018f * Mathf.Sin(t * 5.3f + s.Pos * 1.7f);
            p.Yaw += .06f * Mathf.Sin(t * 1.6f + s.Pos * 1.3f);
            // the hit
            float a = t - (s.At + StripDrop);
            if (a >= 0f)
            {
                if (a < .1f) p.Squash += .2f * Mathf.Sin(Mathf.PI * a / .1f);
                float up = Ph(a, .08f, .38f);
                if (up > 0f && up < 1f)
                {
                    float arc = Mathf.Sin(Mathf.PI * up);
                    p.Jump += .38f * arc;
                    p.Squash -= .07f * arc;
                    p.Arm += 1.1f * arc;
                }
                float land = Ph(a, .46f, .16f);
                if (land > 0f && land < 1f) p.Squash += .12f * Mathf.Sin(Mathf.PI * land);
            }
            // this PC's cheer
            if (s.Me)
            {
                float c = t - MeAt;
                if (c >= 0f && c < 1.1f)
                {
                    float arms = Eo3(Ph(c, 0f, .18f)) * (1f - Ph(c, .8f, .3f));
                    p.Arm = Mathf.Lerp(p.Arm, 1.8f, arms);
                    float hop = Ph(c, .05f, .4f);
                    if (hop > 0f && hop < 1f) p.Jump += .3f * Mathf.Sin(Mathf.PI * hop);
                }
            }
            // the stance at VS
            float stance = Eo3(Ph(t, VsAt, .22f));
            if (stance > 0f)
            {
                // arms only a little up: straight out they would reach into the next panel
                p.Lean += .22f * stance;
                p.Arm = Mathf.Lerp(p.Arm, -.25f, stance);
                float hit = t - VsAt;
                if (hit < .25f) p.Squash += .14f * (1f - hit / .25f);
            }
            return p;
        }

        static float Io3(float x) => x < .5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }
}
