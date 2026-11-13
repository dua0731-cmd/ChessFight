using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // Sword Fight's entrance screen from the host's start (R624, 11-13). The piece pick moved here from the match scene
    // (사용자: "플레이어 찾기 후 매칭 스러운 걸 뭔가 추가 후 지금 소드파이트 기물 선택을 매칭으로 가지고 오고 싶어"), in the
    // order the user set on the canvas https://claude.ai/artifact/P4pMoyFbZdQrjixsfSfbNe ("전체 흐름을 1>2>3>A로 하자"):
    //   ① "매칭 완료!" - the two teams' name plates slide in from the sides, a big VS slams down (SwordPickDraft.FoundSeconds);
    //   ② "기물을 선택하십시오" - five piece cards (1~5 or a click marks one, Space / Enter / a double click confirms it,
    //      SwordPickRequested), a teammate's piece greyed out with their name, the seconds left (PickSeconds);
    //   ③ "우리 팀이 선택한 기물" - our side's pieces, "자동 배정" on any drawn at the close (TeamSeconds);
    //   ④ A "갈라지는 결투" - the screen splits on a diagonal, our side's pieces from the left and theirs from the right
    //      (their picks shown for the first time), a gold cut and a flash where they meet, a VS slam and a shake
    //      (ClashSeconds), then it holds while the scene finishes loading, and the match's own 3·2·1 follows.
    // Every frame comes from LoadingState.Reveal (seconds since the host started, on the shared clock) and the picks the
    // session reads (MatchLoader fills State.Picks), so every PC is at the same beat; only the close of the picks is this
    // PC's own moment when every player picked early. The match scene waits to switch on until ④ has played
    // (SwordHolding), as Pawn Rush's course reveal does, since switching on freezes the frame.
    // Laid out on the canvas's 1280 x 720 stage and scaled to fit, in the course reveal's colours (C1: ebony, ivory,
    // brass) with our side blue and theirs red (R534). The pieces are flat chess icons as boards and chess apps draw them
    // (사용자 "사진으로 그냥 가져다 붙여 놓은 거 같은 느낌 … 실제 체스에서 쓰는 아이콘으로": the rendered figures went),
    // Resources/Loading/ChessIcons, drawn by ArtSource/Loading/ChessIcons/chess_icons.py: a white piece ivory with an
    // ebony line, a black piece ebony with a gold line (the black team's set, R527).
    public sealed partial class LoadingScreenView
    {
        // ---- timing (seconds) ----

        // After the picks close: our team's pieces, then the clash; the scene may switch on once both have played.
        const float TeamSeconds = 3f, ClashSeconds = 3.5f;
        // ① the plates come in one after another from FoundPlateAt, the VS slams at FoundVsAt
        const float FoundPlateAt = .15f, FoundPlateStep = .08f, FoundVsAt = .55f;
        // ④ the halves meet at ClashHit (the cut, the flash, the shake), the VS slams at ClashVsAt, the plate drops at ClashTitleAt
        const float ClashHit = .45f, ClashVsAt = .5f, ClashTitleAt = 1.0f;

        // ---- colours ----

        static readonly Color SideUs = MenuArt.Hex(0x1e78be), SideThem = MenuArt.Hex(0xc8283c), UsDeep = MenuArt.Hex(0x0c1824),
                              ThemDeep = MenuArt.Hex(0x240c0c), UsLight = MenuArt.Hex(0x6fb3e8), ThemLight = MenuArt.Hex(0xef7f8c),
                              Gold = MenuArt.Hex(0xf5d180), CardFace = MenuArt.Hex(0x15110d), CardMarked = MenuArt.Hex(0x211a13),
                              CardMine = MenuArt.Hex(0x2a2016),
                              Flash = MenuArt.Hex(0xfff6e2), AutoTag = MenuArt.Hex(0x5a4630);
        static readonly Color InkSoft = new Color(239 / 255f, 230 / 255f, 210 / 255f, .72f);

        sealed class NamePlate
        {
            public VisualElement Root;
            public Label Name;
        }

        sealed class PickCard
        {
            public PieceKind Piece;
            public VisualElement Root, Figure;
            public SoftEdge Edge;
            public RSlant Face;
            public Label Tag;
            public string FigureKey = "";
        }

        sealed class TeamCard
        {
            public VisualElement Root, Figure;
            public SoftEdge Edge;
            public RSlant Face;
            public Label Owner, Tag, Name, Skill;
            public string FigureKey = "";
        }

        // R624 11-13 (사용자 "테두리 색 지워주고 자연스럽게"): a card has no rim line; widening, fading layers behind it give
        // a soft edge instead - a shadow while it is free, a brass glow when marked, an ivory glow once it is mine.
        // R624 11-13 (사용자 "테두리랑 vs 문구에 층 처럼 보여 자연스럽게 이어지게"): 3 layers (3 / 7 / 12 px) showed as steps;
        // now Layers thin ones, packed closer near the card, each adding the same little, so the glow fades smoothly.
        sealed class SoftEdge
        {
            const int Layers = 14;
            const float Reach = 14f, Each = .045f;
            readonly RSlant[] layers = new RSlant[Layers];

            public SoftEdge(VisualElement root, float lean)
            {
                // the widest first, at the back
                for (int k = Layers - 1; k >= 0; k--)
                {
                    layers[k] = new RSlant(lean, Color.clear);
                    root.Add(layers[k]);
                }
            }

            static float Spread(int k) => Reach * Mathf.Pow((k + 1f) / Layers, 1.6f);

            public void Place(float w, float h)
            {
                for (int k = 0; k < Layers; k++)
                {
                    float s = Spread(k);
                    RPlace(layers[k], -s, -s, w + 2f * s, h + 2f * s);
                }
            }

            public void Set(Color color)
            {
                var c = new Color(color.r, color.g, color.b, color.a * Each);
                for (int k = 0; k < Layers; k++) layers[k].SetFill(c);
            }
        }

        sealed class Chip
        {
            public RSlant Face;
            public Label Text;
        }

        VisualElement sword, swordBox, swordStage, swordDim;
        readonly List<VisualElement> swordHides = new List<VisualElement>();
        Texture2D swordVignette;
        bool swordShown;
        // The moment (Reveal seconds) this PC saw the picks close, -1 before.
        float swordClosedAt = -1f;
        // The card marked (a click, 1~5) and the one sent, waiting for the session to show it as ours.
        PieceKind? swordMarked, swordAsked;
        int swordScreen = -1;

        // ① found
        VisualElement foundGroup;
        RSlant foundTitle;
        Label foundSub, foundVs;
        readonly List<RSlant> foundSquares = new List<RSlant>();
        readonly List<NamePlate>[] foundPlates = { new List<NamePlate>(), new List<NamePlate>() };
        // ② pick
        VisualElement pickGroup, pickConfirm;
        RSlant pickTitle, pickTimer;
        Label pickClock, pickHint;
        readonly List<PickCard> pickCards = new List<PickCard>();
        readonly List<Chip> pickChips = new List<Chip>();
        // ③ team
        VisualElement teamGroup;
        RSlant teamTitle, teamBar;
        Label teamLoad, teamPercent;
        readonly List<TeamCard> teamCards = new List<TeamCard>();
        // ④ clash
        VisualElement clashGroup, clashShake, clashFlash;
        SwordPoly clashLeft, clashRight, clashSlash;
        RSlant clashTitle;
        Label clashVs, clashSub;
        readonly List<VisualElement>[] clashFigures = { new List<VisualElement>(), new List<VisualElement>() };
        readonly List<VisualElement> clashShards = new List<VisualElement>();
        static readonly Vector2[] ShardFly = { new Vector2(-260f, -190f), new Vector2(300f, 160f), new Vector2(180f, -240f), new Vector2(-220f, 230f) };
        static readonly Vector2[] ShardSize = { new Vector2(26f, 10f), new Vector2(18f, 18f), new Vector2(30f, 8f), new Vector2(14f, 22f) };

        readonly List<int> sideUs = new List<int>(), sideThem = new List<int>();
        // R624 11-13 (사용자 "vs 문구에 코팅처럼"): the lacquer coat on ①'s and ④'s VS (TextCoat)
        TextCoat foundCoat, clashCoat;
        static readonly Color VsGloss = new Color(1f, 1f, 1f, .55f), VsGlint = new Color(1f, 1f, 1f, .95f);
        const float VsFrom = 430f, VsTo = 870f;

        /// <summary>While Sword Fight's screens play (the picks open, then ③ and ④), the match scene waits to switch on:
        /// MatchLoader asks this.</summary>
        public bool SwordHolding => sword != null && State.SwordPick && !State.Matching && State.Reveal >= 0f
                                    && (swordClosedAt < 0f || State.Reveal - swordClosedAt < TeamSeconds + ClashSeconds);

        // ---------------------------------------------------------------- building

        void BuildSword(VisualElement stage)
        {
            if (screen == null) return;
            swordStage = stage;
            // what the screens cover: the team lists, the title, the VS, the buttons, the progress line and the tip
            foreach (var name in new[] { "load-side-white", "load-side-black", "load-shade", "load-roster-white", "load-roster-black", "load-start-note", "load-tip" })
            {
                var e = screen.Q<VisualElement>(name);
                if (e != null) swordHides.Add(e);
            }
            foreach (var cls in new[] { "load-head", "load-vs-row", "load-allin-row", "load-cancel-row", "load-progress" })
            {
                var e = screen.Q<VisualElement>(className: cls);
                if (e != null) swordHides.Add(e);
            }

            sword = RLayer(screen);
            sword.name = "sword-pick";
            sword.style.display = DisplayStyle.None;
            swordDim = RLayer(sword);
            var dark = RevealDim;
            dark.a = .82f;
            swordDim.style.backgroundColor = dark;
            swordVignette = MenuMarks.Vignette(MenuArt.Hex(0x060403), .25f, .85f);
            RStretch(RLayer(swordDim), swordVignette);

            swordBox = RBox(sword, 0f, 0f, StageWide, StageTall);
            swordBox.style.transformOrigin = new TransformOrigin(0f, 0f);
            BuildFound(swordBox);
            BuildPick(swordBox);
            BuildTeam(swordBox);
            BuildClash(swordBox);
        }

        RSlant TitlePlate(VisualElement parent, string text, float width)
        {
            var plate = new RSlant(14f, Ivory);
            RPlace(plate, 56f, 26f, width, 66f);
            var label = RLabel(plate, text, 40f, Ebony, true);
            RPlace(label, 0f, 0f, width, 66f);
            parent.Add(plate);
            return plate;
        }

        static Label Left(Label label)
        {
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            return label;
        }

        static Label Right(Label label)
        {
            label.style.unityTextAlign = TextAnchor.MiddleRight;
            return label;
        }

        static Label Bold(Label label)
        {
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        static Label BigVs(VisualElement parent, float top)
        {
            var vs = RLabel(parent, "VS", 190f, Ivory, false);
            RPlace(vs, 0f, top, StageWide, 200f);
            vs.style.textShadow = new TextShadow { offset = new Vector2(9f, 9f), blurRadius = 0f, color = Brass };
            var font = RuntimePanels.TitleFont;
            if (font != null) vs.style.unityFont = font;
            else RuntimePanels.Display(vs);
            return vs;
        }

        void BuildFound(VisualElement box)
        {
            foundGroup = RBox(box, 0f, 0f, StageWide, StageTall);
            foundTitle = TitlePlate(foundGroup, "매칭 완료!", 330f);
            foundSub = Left(RLabel(foundGroup, "", 16f, InkSoft, false));
            RPlace(foundSub, 66f, 104f, 900f, 24f);
            var ours = Bold(Left(RLabel(foundGroup, "우리 팀", 14f, UsLight, false)));
            RPlace(ours, 90f, 196f, 300f, 22f);
            var theirs = Bold(Right(RLabel(foundGroup, "상대 팀", 14f, ThemLight, false)));
            RPlace(theirs, 890f, 196f, 300f, 22f);
            for (int side = 0; side < 2; side++)
                for (int i = 0; i < LoadingStudio.PerTeam; i++)
                {
                    float x = side == 0 ? 90f - 18f * i : 790f + 18f * i, y = 228f + 64f * i;
                    var plate = new NamePlate { Root = RBox(foundGroup, x, y, 400f, 54f) };
                    var face = new RSlant(12f, CardFace);
                    RPlace(face, 0f, 0f, 400f, 54f);
                    plate.Root.Add(face);
                    var stripe = new RSlant(12f, side == 0 ? SideUs : SideThem);
                    RPlace(stripe, side == 0 ? 0f : 386f, 0f, 14f, 54f);
                    plate.Root.Add(stripe);
                    plate.Name = Bold(side == 0 ? Left(RLabel(plate.Root, "", 26f, Ivory, true)) : Right(RLabel(plate.Root, "", 26f, Ivory, true)));
                    RPlace(plate.Name, 34f, 0f, 332f, 54f);
                    foundPlates[side].Add(plate);
                }
            foundVs = BigVs(foundGroup, 236f);
            foundCoat = TextCoat.Apply(foundVs, StageWide, 200f, VsGloss, VsGlint, VsFrom, VsTo);
            var soon = RLabel(foundGroup, "곧 기물을 선택합니다", 18f, InkSoft, false);
            RPlace(soon, 0f, 604f, StageWide, 26f);
            const float w = 72f, gap = 12f;
            float left = (StageWide - (3f * w + 2f * gap)) / 2f;
            for (int k = 0; k < 3; k++)
            {
                var square = new RSlant(8f, DotOff);
                RPlace(square, left + k * (w + gap), 640f, w, 14f);
                square.Bar(0f, 14f, Brass);
                foundGroup.Add(square);
                foundSquares.Add(square);
            }
        }

        void BuildPick(VisualElement box)
        {
            pickGroup = RBox(box, 0f, 0f, StageWide, StageTall);
            pickTitle = TitlePlate(pickGroup, "기물을 선택하십시오", 430f);
            var sub = Left(RLabel(pickGroup, "팀원끼리 같은 기물은 못 골라요 · 상대 팀 기물은 경기 시작 직전에 공개", 16f, InkSoft, false));
            RPlace(sub, 66f, 104f, 900f, 24f);
            pickClock = Right(RLabel(pickGroup, "15", 56f, Ivory, false));
            RPlace(pickClock, 960f, 28f, 210f, 62f);
            pickClock.style.textShadow = new TextShadow { offset = new Vector2(4f, 4f), blurRadius = 0f, color = Brass };
            var font = RuntimePanels.TitleFont;
            if (font != null) pickClock.style.unityFont = font;
            var unit = Left(RLabel(pickGroup, "초", 16f, InkSoft, false));
            RPlace(unit, 1178f, 52f, 40f, 30f);
            pickTimer = new RSlant(6f, DotOff);
            RPlace(pickTimer, 996f, 98f, 220f, 10f);
            pickTimer.Bar(0f, 10f, Brass);
            pickGroup.Add(pickTimer);

            // R624 11-13 (사용자 "테두리 색 지워주고 자연스럽게 … 기물 선택하는 칸을 좀 더 넓게 … 좀더 크게"): wider and taller
            // cards (210 x 380 -> 236 x 424) across the stage, a bigger piece (160 -> 200), no rim line (SoftEdge instead).
            // R624 11-13 (사용자 "기물 선택 칸 옆으로 좀 더 넓게"): 236 -> 250 wide, 6 apart, the five across nearly all the
            // stage; the chips and the confirm button keep RowMargin from its sides.
            const float cardW = 250f, cardH = 424f, cardStep = 256f, cardTop = 140f, cardLean = 34f, RowMargin = 48f;
            float cardsLeft = (StageWide - (cardStep * 4f + cardW)) / 2f;
            var pieces = SwordMatchRules.Pickable;
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                var card = new PickCard { Piece = piece, Root = RBox(pickGroup, cardsLeft + cardStep * i, cardTop, cardW, cardH) };
                card.Root.pickingMode = PickingMode.Position;
                card.Edge = new SoftEdge(card.Root, cardLean);
                card.Edge.Place(cardW, cardH);
                card.Face = new RSlant(cardLean, CardFace);
                RPlace(card.Face, 0f, 0f, cardW, cardH);
                card.Face.Bar(cardH - 8f, 8f, Brass);
                card.Root.Add(card.Face);
                var number = Left(RLabel(card.Root, (i + 1).ToString(), 32f, Brass, false));
                RPlace(number, 44f, 10f, 50f, 40f);
                if (font != null) number.style.unityFont = font;
                card.Tag = Bold(Right(RLabel(card.Root, "", 14f, Brass, false)));
                RPlace(card.Tag, cardW - 156f, 18f, 136f, 20f);
                card.Figure = RBox(card.Root, (cardW - 200f) / 2f, 40f, 200f, 200f);
                var name = RLabel(card.Root, ChessPieces.Name(piece), 36f, Ivory, true);
                RPlace(name, 0f, 246f, cardW, 46f);
                var skill = Left(RLabel(card.Root, "F  " + SwordSkillNames.Skill(piece), 15f, Ivory, false));
                RPlace(skill, 24f, 306f, cardW - 36f, 24f);
                var edge = Left(RLabel(card.Root, "E  " + SwordSkillNames.Edge(piece), 15f, Ivory, false));
                RPlace(edge, 18f, 340f, cardW - 36f, 24f);
                card.Root.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0) return;
                    MarkPiece(piece);
                    if (e.clickCount >= 2) ConfirmPiece(piece);
                });
                pickCards.Add(card);
            }

            for (int i = 0; i < LoadingStudio.PerTeam; i++)
            {
                var chip = new Chip { Face = new RSlant(12f, CardFace) };
                RPlace(chip.Face, RowMargin + 150f * i, 580f, 142f, 40f);
                chip.Text = Bold(RLabel(chip.Face, "", 15f, Ivory, false));
                RPlace(chip.Text, 0f, 0f, 142f, 40f);
                pickGroup.Add(chip.Face);
                pickChips.Add(chip);
            }

            pickConfirm = RBox(pickGroup, StageWide - RowMargin - 210f, 574f, 210f, 52f);
            pickConfirm.pickingMode = PickingMode.Position;
            var button = new RSlant(12f, Brass);
            RPlace(button, 0f, 0f, 210f, 52f);
            pickConfirm.Add(button);
            var press = Bold(RLabel(pickConfirm, "확정  Space", 18f, Ebony, false));
            RPlace(press, 0f, 0f, 210f, 52f);
            pickConfirm.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0 && swordMarked.HasValue) ConfirmPiece(swordMarked.Value);
            });

            pickHint = RLabel(pickGroup, "", 16f, InkSoft, false);
            RPlace(pickHint, 0f, 668f, StageWide, 26f);
        }

        void BuildTeam(VisualElement box)
        {
            teamGroup = RBox(box, 0f, 0f, StageWide, StageTall);
            teamTitle = TitlePlate(teamGroup, "우리 팀이 선택한 기물", 450f);
            for (int i = 0; i < LoadingStudio.PerTeam; i++)
            {
                var card = new TeamCard { Root = RBox(teamGroup, 0f, 0f, 220f, 360f) };
                card.Edge = new SoftEdge(card.Root, 24f);
                card.Face = new RSlant(24f, CardFace);
                card.Root.Add(card.Face);
                card.Owner = Bold(Left(RLabel(card.Root, "", 14f, Brass, false)));
                card.Tag = Bold(RLabel(card.Root, "자동 배정", 13f, Ivory, false));
                card.Tag.style.backgroundColor = AutoTag;
                card.Figure = RBox(card.Root, 0f, 0f, 10f, 10f);
                card.Name = RLabel(card.Root, "", 32f, Ivory, true);
                card.Skill = RLabel(card.Root, "", 15f, InkSoft, false);
                teamCards.Add(card);
            }
            teamLoad = Left(RLabel(teamGroup, "", 16f, InkSoft, false));
            RPlace(teamLoad, 260f, 610f, 600f, 24f);
            teamPercent = Bold(Right(RLabel(teamGroup, "", 16f, Ivory, false)));
            RPlace(teamPercent, 860f, 610f, 160f, 24f);
            teamBar = new RSlant(4f, DotOff);
            RPlace(teamBar, 260f, 642f, 760f, 9f);
            teamBar.Bar(0f, 9f, Gold);
            teamGroup.Add(teamBar);
            var then = RLabel(teamGroup, "모두 준비되면 화면이 열리고 경기장에서 3 · 2 · 1", 14f, new Color(Ivory.r, Ivory.g, Ivory.b, .6f), false);
            RPlace(then, 0f, 664f, StageWide, 22f);
        }

        void BuildClash(VisualElement box)
        {
            clashGroup = RBox(box, 0f, 0f, StageWide, StageTall);
            clashGroup.style.overflow = Overflow.Hidden;
            clashShake = RBox(clashGroup, 0f, 0f, StageWide, StageTall);
            clashLeft = new SwordPoly(UsDeep, new Vector2(0f, 0f), new Vector2(781f, 0f), new Vector2(499f, 720f), new Vector2(0f, 720f));
            RPlace(clashLeft, 0f, 0f, StageWide, StageTall);
            clashShake.Add(clashLeft);
            clashRight = new SwordPoly(ThemDeep, new Vector2(781f, 0f), new Vector2(1280f, 0f), new Vector2(1280f, 720f), new Vector2(499f, 720f));
            RPlace(clashRight, 0f, 0f, StageWide, StageTall);
            clashShake.Add(clashRight);
            var ours = Bold(Left(RLabel(clashLeft, "우리 팀", 16f, UsLight, false)));
            RPlace(ours, 60f, 150f, 300f, 24f);
            ours.style.letterSpacing = 6f;
            var theirs = Bold(Right(RLabel(clashRight, "상대 팀", 16f, ThemLight, false)));
            RPlace(theirs, 920f, 546f, 300f, 24f);
            theirs.style.letterSpacing = 6f;
            for (int side = 0; side < 2; side++)
                for (int i = 0; i < LoadingStudio.PerTeam; i++)
                {
                    var parent = side == 0 ? clashLeft : clashRight;
                    // R624 11-13 (사용자 "마지막 경기 시작 부분 밑에 이거 지워주고"): no name chips under the pieces any more
                    clashFigures[side].Add(RBox(parent, 0f, 0f, 10f, 10f));
                }
            clashSlash = new SwordPoly(Gold, new Vector2(777f, -10f), new Vector2(785f, -10f), new Vector2(503f, 730f), new Vector2(495f, 730f));
            RPlace(clashSlash, 0f, 0f, StageWide, StageTall);
            clashShake.Add(clashSlash);
            clashFlash = RLayer(clashShake);
            clashFlash.style.backgroundColor = Flash;
            for (int k = 0; k < ShardFly.Length; k++)
            {
                var shard = RBox(clashShake, 636f, 356f, ShardSize[k].x, ShardSize[k].y);
                shard.style.backgroundColor = k % 2 == 0 ? Gold : Brass;
                clashShards.Add(shard);
            }
            clashVs = BigVs(clashShake, 250f);
            clashCoat = TextCoat.Apply(clashVs, StageWide, 200f, VsGloss, VsGlint, VsFrom, VsTo);
            clashTitle = new RSlant(14f, Ivory);
            RPlace(clashTitle, 450f, 520f, 380f, 70f);
            var title = RLabel(clashTitle, "결투 준비 완료!", 42f, Ebony, true);
            RPlace(title, 0f, 0f, 380f, 70f);
            clashShake.Add(clashTitle);
            clashSub = RLabel(clashGroup, "", 15f, InkSoft, false);
            RPlace(clashSub, 0f, 684f, StageWide, 24f);
        }

        // ---------------------------------------------------------------- every frame

        void SyncSword(float now)
        {
            if (sword == null || screen == null) return;
            float t = State.SwordPick && !State.Matching ? State.Reveal : -1f;
            bool on = t >= 0f;
            if (on != swordShown)
            {
                swordShown = on;
                sword.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                var hidden = on ? new StyleEnum<Visibility>(Visibility.Hidden) : new StyleEnum<Visibility>(StyleKeyword.Null);
                foreach (var e in swordHides) e.style.visibility = hidden;
                if (swordStage != null)
                {
                    swordStage.style.filter = on ? new StyleList<FilterFunction>(new List<FilterFunction> { BlurFilter(10f), GreyFilter(.35f) })
                                                 : new StyleList<FilterFunction>(StyleKeyword.Null);
                    swordStage.style.scale = on ? new Scale(new Vector2(1.05f, 1.05f)) : new StyleScale(StyleKeyword.Null);
                }
            }
            if (!on) { swordClosedAt = -1f; swordScreen = -1; return; }
            FitBox(swordBox);
            swordDim.style.opacity = Eo3(Ph(t, 0f, .2f));
            Sides(sideUs, sideThem);

            var stage = SwordPickDraft.StageAt(t, State.SwordAllPicked);
            if (stage == SwordPickDraft.Stage.Closed && swordClosedAt < 0f) swordClosedAt = Mathf.Min(t, (float)SwordPickDraft.CloseAt);
            float u = swordClosedAt >= 0f ? t - swordClosedAt : -1f;
            int which = stage == SwordPickDraft.Stage.Found ? 0 : stage == SwordPickDraft.Stage.Picking ? 1 : u < TeamSeconds ? 2 : 3;
            if (which != swordScreen)
            {
                swordScreen = which;
                foundGroup.style.display = which == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                pickGroup.style.display = which == 1 ? DisplayStyle.Flex : DisplayStyle.None;
                teamGroup.style.display = which == 2 ? DisplayStyle.Flex : DisplayStyle.None;
                clashGroup.style.display = which == 3 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            switch (which)
            {
                case 0: SyncFound(t); break;
                case 1: SyncPick(t - (float)SwordPickDraft.FoundSeconds, t); break;
                case 2: SyncTeam(u); break;
                default: SyncClash(u - TeamSeconds); break;
            }
        }

        void FitBox(VisualElement box)
        {
            float w = screen.layout.width, h = screen.layout.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0f || h <= 0f) return;
            float k = Mathf.Min(w / StageWide, h / StageTall);
            box.style.left = (w - StageWide * k) / 2f;
            box.style.top = (h - StageTall * k) / 2f;
            box.style.scale = new Scale(new Vector2(k, k));
        }

        // The players on this PC's side (this PC's own first) and the others, as indexes into State's lists.
        void Sides(List<int> ours, List<int> theirs)
        {
            ours.Clear();
            theirs.Clear();
            int myTeam = MyTeam();
            if (State.Me >= 0 && State.Me < State.Teams.Count) ours.Add(State.Me);
            for (int i = 0; i < State.Teams.Count; i++)
            {
                if (i == State.Me) continue;
                if (State.Teams[i] == myTeam) ours.Add(i); else theirs.Add(i);
            }
        }

        int MyTeam() => State.Me >= 0 && State.Me < State.Teams.Count ? State.Teams[State.Me] : 0;

        string EntryName(int i)
        {
            string name = i < State.Names.Count ? State.Names[i] : "";
            return i == State.Me ? "나 · " + name : name;
        }

        PieceKind PickOf(int i) => i >= 0 && i < State.Picks.Count ? State.Picks[i] : PieceKind.Pawn;

        static void Text(Label label, string text)
        {
            if (label.text != text) label.text = text;
        }

        void SyncFound(float t)
        {
            foundTitle.style.translate = new Translate(-700f * (1f - EoBack(Ph(t, .1f, .24f))), 0f);
            Text(foundSub, "소드 파이트 · " + Mathf.Max(1, sideUs.Count) + " VS " + Mathf.Max(1, sideThem.Count) + " · 떨어뜨리고 빼앗기");
            for (int side = 0; side < 2; side++)
            {
                var list = side == 0 ? sideUs : sideThem;
                var plates = foundPlates[side];
                for (int k = 0; k < plates.Count; k++)
                {
                    var plate = plates[k];
                    bool shown = k < list.Count;
                    plate.Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                    if (!shown) continue;
                    Text(plate.Name, EntryName(list[k]));
                    float a = FoundPlateAt + FoundPlateStep * k;
                    plate.Root.style.translate = new Translate((side == 0 ? -1f : 1f) * 560f * (1f - Eo3(Ph(t, a, .3f))), 0f);
                    plate.Root.style.opacity = Ph(t, a, .1f);
                }
            }
            float vs = Ph(t, FoundVsAt, .18f);
            foundVs.style.opacity = Ph(t, FoundVsAt, .06f);
            float scale = 1f + 1.4f * (1f - Eo3(vs));
            foundVs.style.scale = new Scale(new Vector2(scale, scale));
            foundCoat.Sync(t - (FoundVsAt + .3f));
            var shake = Shake(t - (FoundVsAt + .18f), 10f, .2f, 2);
            foundGroup.style.translate = new Translate(shake.x, shake.y);
            // the three squares fill one after another across the whole screen (SwordPickDraft.FoundSeconds)
            float step = (float)SwordPickDraft.FoundSeconds / Mathf.Max(1, foundSquares.Count);
            for (int k = 0; k < foundSquares.Count; k++) foundSquares[k].BarFill(Ph(t, k * step, step));
        }

        void SyncPick(float t, float since)
        {
            pickTitle.style.translate = new Translate(-700f * (1f - EoBack(Ph(t, 0f, .24f))), 0f);
            double left = SwordPickDraft.Left(since);
            Text(pickClock, Mathf.CeilToInt((float)left).ToString());
            pickTimer.BarFill((float)(left / SwordPickDraft.PickSeconds));

            var mine = PickOf(State.Me);
            if (mine != PieceKind.Pawn) { swordAsked = null; swordMarked = null; }
            for (int k = 1; k < sideUs.Count; k++)
            {
                var p = PickOf(sideUs[k]);
                // a teammate holds the piece first: the card frees for another pick
                if (swordAsked.HasValue && p == swordAsked.Value) swordAsked = null;
                if (swordMarked.HasValue && p == swordMarked.Value) swordMarked = null;
            }

            for (int i = 0; i < pickCards.Count; i++)
            {
                var card = pickCards[i];
                string holder = Holder(card.Piece);
                bool taken = holder != null, isMine = mine == card.Piece;
                bool pending = mine == PieceKind.Pawn && swordAsked.HasValue && swordAsked.Value == card.Piece;
                bool marked = mine == PieceKind.Pawn && !pending && swordMarked.HasValue && swordMarked.Value == card.Piece;
                bool off = taken || (mine != PieceKind.Pawn && !isMine);
                card.Face.SetFill(isMine ? CardMine : marked || pending ? CardMarked : CardFace);
                card.Edge.Set(isMine ? Ivory : marked || pending ? Brass : Color.black);
                card.Face.BarFill(isMine ? 1f : pending ? .7f : marked ? .35f : 0f);
                Text(card.Tag, taken ? holder + " 선택" : isMine ? "확정" : pending ? "보내는 중" : marked ? "선택됨" : "");
                card.Tag.style.color = isMine ? Ivory : Brass;
                float a = .05f * i, lift = isMine ? -14f : marked || pending ? -10f : 0f;
                card.Root.style.translate = new Translate(0f, 60f * (1f - Eo3(Ph(t, a, .3f))) + lift);
                card.Root.style.opacity = Ph(t, a, .12f) * (off ? .38f : 1f);
                SetFigure(card.Figure, ref card.FigureKey, card.Piece, MyTeam());
            }

            for (int k = 0; k < pickChips.Count; k++)
            {
                var chip = pickChips[k];
                bool shown = k < sideUs.Count;
                chip.Face.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!shown) continue;
                int e = sideUs[k];
                var p = PickOf(e);
                bool bot = e < State.Bots.Count && State.Bots[e];
                string who = e == State.Me ? "나" : e < State.Names.Count ? State.Names[e] : "";
                Text(chip.Text, who + " · " + (p != PieceKind.Pawn ? ChessPieces.Name(p) : bot ? "자동" : "고르는 중"));
                chip.Face.SetFill(e == State.Me ? Ivory : CardFace);
                chip.Text.style.color = e == State.Me ? Ebony : p != PieceKind.Pawn ? Ivory : InkSoft;
            }

            bool canConfirm = mine == PieceKind.Pawn && !swordAsked.HasValue && swordMarked.HasValue;
            pickConfirm.style.display = canConfirm ? DisplayStyle.Flex : DisplayStyle.None;
            Text(pickHint, mine != PieceKind.Pawn ? "골랐어요. 다른 팀원을 기다리는 중 · 모두 고르면 바로 넘어가요"
                         : swordAsked.HasValue ? ChessPieces.Name(swordAsked.Value) + " 보내는 중"
                         : swordMarked.HasValue ? ChessPieces.Name(swordMarked.Value) + " 선택 · Space나 더블클릭으로 확정"
                         : "클릭 · 1~5로 선택, Space나 더블클릭으로 확정 · 시간이 끝나면 남은 기물로 자동 배정");
            if (!Hiding) SwordKeys();
        }

        // The teammate (name) holding this piece now, null for none.
        string Holder(PieceKind piece)
        {
            for (int k = 1; k < sideUs.Count; k++)
            {
                int e = sideUs[k];
                if (PickOf(e) == piece) return e < State.Names.Count ? State.Names[e] : "";
            }
            return null;
        }

        void SwordKeys()
        {
            if (ChatBox.KeysHeld || GameMenu.IsOpen || SettingsWindow.IsOpen) return;
            var pieces = SwordMatchRules.Pickable;
            for (int i = 0; i < pieces.Length; i++)
                if (KeyDown(KeyCode.Alpha1 + i) || KeyDown(KeyCode.Keypad1 + i)) MarkPiece(pieces[i]);
            if (swordMarked.HasValue && (KeyDown(KeyCode.Space) || KeyDown(KeyCode.Return) || KeyDown(KeyCode.KeypadEnter)))
                ConfirmPiece(swordMarked.Value);
        }

        static bool KeyDown(KeyCode key)
        {
            try { return Input.GetKeyDown(key); }
            catch (InvalidOperationException) { return false; }
        }

        bool PickOpen => swordScreen == 1 && !Hiding && PickOf(State.Me) == PieceKind.Pawn && !swordAsked.HasValue;

        void MarkPiece(PieceKind piece)
        {
            if (!PickOpen || Holder(piece) != null) return;
            swordMarked = piece;
        }

        void ConfirmPiece(PieceKind piece)
        {
            if (!PickOpen || Holder(piece) != null) return;
            swordMarked = piece;
            swordAsked = piece;
            SwordPickRequested?.Invoke(piece);
        }

        void SyncTeam(float u)
        {
            teamTitle.style.translate = new Translate(-700f * (1f - EoBack(Ph(u, 0f, .24f))), 0f);
            int n = Mathf.Min(sideUs.Count, teamCards.Count);
            const float bigW = 260f, bigH = 400f;
            float gap = n <= 3 ? 40f : 24f;
            float w = n <= 1 ? 220f : Mathf.Min(220f, (1180f - bigW - gap * (n - 1)) / (n - 1)), h = w * 360f / 220f;
            float total = bigW + (n - 1) * (w + gap), x = (StageWide - total) / 2f;
            for (int k = 0; k < teamCards.Count; k++)
            {
                var card = teamCards[k];
                bool shown = k < n;
                card.Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!shown) continue;
                int e = sideUs[k];
                bool me = e == State.Me;
                float cw = me ? bigW : w, ch = me ? bigH : h, top = me ? 140f : 140f + (bigH - ch) / 2f;
                RPlace(card.Root, x, top, cw, ch);
                card.Edge.Place(cw, ch);
                RPlace(card.Face, 0f, 0f, cw, ch);
                card.Face.Bar(ch - 8f, 8f, Brass);
                card.Face.BarFill(1f);
                card.Edge.Set(me ? Ivory : Color.black);
                card.Face.SetFill(me ? CardMine : CardFace);
                x += cw + gap;

                var piece = PickOf(e);
                bool auto = e < State.PickAuto.Count && State.PickAuto[e];
                Text(card.Owner, me ? "나" : e < State.Names.Count ? State.Names[e] : "");
                RPlace(card.Owner, 24f, 12f, cw - 40f, 22f);
                card.Tag.style.display = auto ? DisplayStyle.Flex : DisplayStyle.None;
                RPlace(card.Tag, cw - 96f, 12f, 76f, 22f);
                float fig = cw * .72f;
                RPlace(card.Figure, (cw - fig) / 2f, 40f, fig, fig);
                SetFigure(card.Figure, ref card.FigureKey, piece, MyTeam());
                Text(card.Name, piece != PieceKind.Pawn ? ChessPieces.Name(piece) : "");
                card.Name.style.fontSize = me ? 38f : 30f;
                RPlace(card.Name, 0f, 44f + fig, cw, 46f);
                Text(card.Skill, me && piece != PieceKind.Pawn ? "F " + SwordSkillNames.Skill(piece) + " · E " + SwordSkillNames.Edge(piece) : "");
                RPlace(card.Skill, 0f, 94f + fig, cw, 22f);

                float a = .07f * k;
                card.Root.style.translate = new Translate(0f, 50f * (1f - Eo3(Ph(u, a, .3f))) + (me ? -12f : 0f));
                card.Root.style.opacity = Ph(u, a, .12f);
            }
            Text(teamLoad, string.IsNullOrEmpty(State.Step) ? "경기장을 불러오는 중" : State.Step);
            Text(teamPercent, State.Percent + "%");
            teamBar.BarFill(Mathf.Clamp01(State.Percent / 100f));
        }

        void SyncClash(float c)
        {
            float meet = EoBack(Ph(c, 0f, ClashHit));
            clashLeft.style.translate = new Translate(-StageWide * (1f - meet), 0f);
            clashRight.style.translate = new Translate(StageWide * (1f - meet), 0f);
            int myTeam = MyTeam();
            for (int side = 0; side < 2; side++)
            {
                var list = side == 0 ? sideUs : sideThem;
                int n = Mathf.Max(1, Mathf.Min(list.Count, LoadingStudio.PerTeam));
                float size = Mathf.Min(300f, 640f / n * 1.15f), step = size * .62f;
                for (int k = 0; k < clashFigures[side].Count; k++)
                {
                    var figure = clashFigures[side][k];
                    bool shown = k < list.Count;
                    figure.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                    if (!shown) continue;
                    // our side bottom left, the first (this PC's player) biggest; theirs top right, mirrored
                    float s = size * (1f - .12f * k);
                    float x = side == 0 ? 30f + step * k : StageWide - 30f - s - step * k;
                    float y = side == 0 ? StageTall - 110f - s : 70f;
                    RPlace(figure, x, y, s, s);
                    figure.style.opacity = 1f - .1f * k;
                    var piece = PickOf(list[k]);
                    string key = figure.userData as string ?? "";
                    SetFigure(figure, ref key, piece, side == 0 ? myTeam : 1 - myTeam);
                    figure.userData = key;
                }
            }

            float hit = c - ClashHit;
            clashSlash.style.opacity = hit < 0f ? 0f : hit < .1f ? 1f : Mathf.Lerp(1f, .55f, Ph(hit, .1f, .5f));
            clashFlash.style.opacity = hit < 0f ? 0f : .9f * (1f - Ph(hit, 0f, .3f));
            for (int k = 0; k < clashShards.Count; k++)
            {
                float fly = Eo3(Ph(hit, 0f, .8f + .1f * k));
                clashShards[k].style.translate = new Translate(ShardFly[k].x * fly, ShardFly[k].y * fly);
                clashShards[k].style.rotate = new Rotate(new Angle(220f * fly * (k % 2 == 0 ? 1f : -1f), AngleUnit.Degree));
                clashShards[k].style.opacity = hit < 0f ? 0f : 1f - fly;
            }
            float vs = Ph(c, ClashVsAt, .2f);
            clashVs.style.opacity = Ph(c, ClashVsAt, .06f);
            float scale = 1f + 2.2f * (1f - Eo3(vs));
            clashVs.style.scale = new Scale(new Vector2(scale, scale));
            clashCoat.Sync(c - (ClashVsAt + .35f));
            clashTitle.style.opacity = Ph(c, ClashTitleAt, .1f);
            clashTitle.style.translate = new Translate(0f, -120f * (1f - EoBack(Ph(c, ClashTitleAt, .25f))));
            var shake = Shake(hit, 14f, .25f, 3);
            clashShake.style.translate = new Translate(shake.x, shake.y);
            Text(clashSub, c >= ClashSeconds && !string.IsNullOrEmpty(State.Step) ? State.Step + "  ·  " + State.Percent + "%" : "");
        }

        const string IconFolder = "Loading/ChessIcons/";
        static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();

        // A piece's chess icon on an element (white for the white team, black for the black), set only when it changes.
        static void SetFigure(VisualElement element, ref string key, PieceKind piece, int team)
        {
            string want = piece == PieceKind.Pawn ? "" : piece + (team == 0 ? "_White" : "_Black");
            if (key == want) return;
            key = want;
            var texture = want == "" ? null : Icon(want);
            element.style.backgroundImage = texture != null ? new StyleBackground(texture) : new StyleBackground(StyleKeyword.None);
            element.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        }

        static Texture2D Icon(string name)
        {
            if (!icons.TryGetValue(name, out var texture) || texture == null)
            {
                texture = Resources.Load<Texture2D>(IconFolder + name);
                if (texture == null) Debug.LogWarning("[Loading] 기물 아이콘이 없습니다: Resources/" + IconFolder + name);
                icons[name] = texture;
            }
            return texture;
        }

        void DisposeSword()
        {
            if (swordVignette != null) Destroy(swordVignette);
        }

        // A filled convex polygon (the clash's two halves and the cut between them), in the element's own pixels.
        sealed class SwordPoly : VisualElement
        {
            readonly Vector2[] points;
            readonly Color color;

            public SwordPoly(Color color, params Vector2[] points)
            {
                this.color = color;
                this.points = points;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext context)
            {
                int n = points.Length;
                if (n < 3) return;
                var mesh = context.Allocate(n, (n - 2) * 3);
                Color32 tint = color;
                for (int k = 0; k < n; k++)
                    mesh.SetNextVertex(new Vertex { position = new Vector3(points[k].x, points[k].y, Vertex.nearZ), tint = tint });
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
