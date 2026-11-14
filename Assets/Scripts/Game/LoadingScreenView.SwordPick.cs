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
    //   ① "매칭 완료!" - the two teams' player cards drop in facing each other (a ranked lineup, R637), a big VS slams down
    //      (SwordPickDraft.FoundSeconds);
    //   ② "기물을 선택하십시오" - five piece cards (1~5 or a click marks one, Space / Enter / a double click confirms it,
    //      SwordPickRequested), a teammate's piece greyed out with their name, the seconds left (PickSeconds);
    //   ③ "우리 팀이 선택한 기물" - our side's pieces, "자동 배정" on any drawn at the close (TeamSeconds);
    //   ④ A "갈라지는 결투" - the screen splits on a diagonal, our side's pieces from the left and theirs from the right
    //      (their picks shown for the first time), a sword that cuts the seam gold (R636) and a flash where they meet, a VS slam and a shake
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
        // R645 (11-14, 사용자 "칼을 베서 자르는 게 너무 빨라서 안보이고"): the clash runs .4 s longer for the slower cut.
        const float TeamSeconds = 3f, ClashSeconds = 3.9f;
        // ① the plates come in one after another from FoundPlateAt, the VS slams at FoundVsAt
        const float FoundPlateAt = .15f, FoundPlateStep = .08f, FoundVsAt = .55f;
        // ④ the halves meet at ClashHit (the cut, the flash, the shake), the VS slams at ClashVsAt, the plate drops at ClashTitleAt
        // R645: the halves now meet first (ClashMeet), then the sword comes into sight and its tip crosses the middle at
        // ClashHit; the VS waits until the cut is open, so nothing covers the blade.
        const float ClashMeet = .45f, ClashHit = .85f, ClashVsAt = 1.15f, ClashTitleAt = 1.6f;

        // ---- colours ----

        static readonly Color SideUs = MenuArt.Hex(0x1e78be), SideThem = MenuArt.Hex(0xc8283c), UsDeep = MenuArt.Hex(0x0c1824),
                              ThemDeep = MenuArt.Hex(0x240c0c), UsLight = MenuArt.Hex(0x6fb3e8), ThemLight = MenuArt.Hex(0xef7f8c),
                              Gold = MenuArt.Hex(0xf5d180), CardFace = MenuArt.Hex(0x15110d), CardMarked = MenuArt.Hex(0x211a13),
                              CardMine = MenuArt.Hex(0x2a2016),
                              Flash = MenuArt.Hex(0xfff6e2), AutoTag = MenuArt.Hex(0x5a4630);
        static readonly Color InkSoft = new Color(239 / 255f, 230 / 255f, 210 / 255f, .72f);

        // R637 (11-13, 사용자 "여기 좀 더 경쟁 매칭 스럽게 바꿔줘"): ① as a ranked lineup - one tall card per player, the two
        // teams facing each other across the VS, this PC's player nearest the VS; on each card the team's pawn, dimmed,
        // waiting for the piece picked next, the name and what the player is (나 / 봇 / 플레이어, 방장). Each card drops in
        // and lands with a faint flash and a light sweeping over it. R638 (사용자 "살짝 좀 오바한 느낌이야 … 전체적인 컬러랑
        // 잘 안어울려"): in the pick's colours - ebony cards with a brass foot and brass tags, this PC's card lit ivory
        // round its edge as on the pick and team screens; the blue and red only in the team names (no washes, no team
        // banners or frames), the match a plain line, the cards and the VS a little smaller.
        sealed class FoundCard
        {
            public VisualElement Root, Emblem;
            public SoftEdge Edge;
            public RSlant Face, Flash;
            public Label Name, Tag;
            public string Icon = "";
            public float Wide;
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
        VisualElement foundTitle, foundInfo;
        Label foundVs, foundSize;
        readonly VisualElement[] foundHeads = new VisualElement[2];
        readonly Label[] foundCounts = new Label[2];
        readonly List<RSlant> foundSquares = new List<RSlant>();
        readonly List<FoundCard>[] foundCards = { new List<FoundCard>(), new List<FoundCard>() };
        int foundLaid = -1;
        // the lineup (R637): the cards stand from LineupTop, LineupTall high, each side's within LineupWide of the
        // LineupMiddle-wide gap in the middle where the VS stands, at most CardWideMax across and LineupGap apart
        const float LineupTop = 222f, LineupTall = 300f, LineupWide = 500f, LineupMiddle = 170f, LineupGap = 14f,
                     CardWideMax = 140f, CardLean = 14f, FoundVsScale = .66f;
        static readonly Color NameZone = MenuArt.Hex(0x231a12);
        // ② pick
        VisualElement pickGroup, pickConfirm, pickTitle;
        RSlant pickTimer;
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
        SwordPoly clashLeft, clashRight;
        SwordCut clashCut;
        VisualElement clashTitle, clashSword;
        Label clashVs, clashSub;
        readonly List<VisualElement>[] clashFigures = { new List<VisualElement>(), new List<VisualElement>() };
        readonly List<VisualElement> clashGhosts = new List<VisualElement>(), clashSparks = new List<VisualElement>();

        // R636 (11-13, 사용자 "여기 노란색 대각선으로 슬라이스 된 부분 실제 칼로 자르는 연출을 넣어줘"): a longsword
        // (Resources/Loading/SwordPick/ClashSword, drawn by ArtSource/Loading/SwordPick/Blade/clash_sword.py) flies down the
        // seam tip first, so fast it leaves a few fading after-images; the gold cut opens behind its tip, white-hot where the
        // tip is, and cools; steel sparks fly from the tip forward and to both sides; once the tip is out at the bottom the
        // two halves slip along the cut (ours down, theirs up) and part a little, the gold showing between them. The tip
        // crosses the middle of the screen at ClashHit, where the flash, the shake and the clash sound already were.
        const string ClashSwordPath = "Loading/SwordPick/ClashSword";
        static readonly Vector2 CutTop = new Vector2(781f, 0f), CutBottom = new Vector2(499f, 720f);
        // the sword on the stage (its picture is 2048 x 256), how far above the top its tip starts, how fast it goes, and how
        // far it leans off the seam about its tip (degrees), so the cut glowing behind the tip shows beside the blade
        const float BladeLong = 720f, BladeTall = 90f, SwingFrom = -140f, SwingSpeed = 2600f, BladeTilt = 6f;
        // R645: 7800 px/s crossed the screen in a tenth of a second and could not be seen; 2600 takes about .3 s.
        // the cut's width (across, as before), its white-hot stretch behind the tip, how far the halves slip and part
        const float CutWide = 8f, CutHot = 130f, CutSlip = 9f, CutPart = 3f;
        static readonly float[] GhostBack = { 50f, 105f, 165f }, GhostAlpha = { .34f, .18f, .08f };
        const int SparkCount = 12;

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
            WarmSwordClips();
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
            // R645: game lines, not sentences ("상대를 찾았습니다 / 곧 대결이 시작됩니다" read like an editor's notice)
            foundTitle = BuildMatchAnnouncement(foundGroup, "소드 파이트", 22f, 46f, false, "매칭 완료", "");
            // the match under the title, one quiet line: the sides' size and the rule
            foundInfo = RBox(foundGroup, 0f, 134f, StageWide, 24f);
            foundSize = RLabel(foundInfo, "", 15f, InkSoft, false);
            RPlace(foundSize, 0f, 0f, StageWide, 24f);
            for (int side = 0; side < 2; side++)
            {
                // R638 the team's name over its lineup in its light colour (as before R637), the head count beside it;
                // the team colours stay in these words only, the cards are ebony and brass like the pick's
                float near = side == 0 ? StageWide / 2f - LineupMiddle / 2f : StageWide / 2f + LineupMiddle / 2f;
                var head = RBox(foundGroup, side == 0 ? near - LineupWide : near, LineupTop - 34f, LineupWide, 22f);
                foundHeads[side] = head;
                var name = Bold(side == 0 ? Left(RLabel(head, "우리 팀", 14f, UsLight, false)) : Right(RLabel(head, "상대 팀", 14f, ThemLight, false)));
                RPlace(name, 4f, 0f, LineupWide - 8f, 22f);
                name.style.letterSpacing = 6f;
                var count = side == 0 ? Right(RLabel(head, "", 13f, InkSoft, false)) : Left(RLabel(head, "", 13f, InkSoft, false));
                RPlace(count, 4f, 0f, LineupWide - 8f, 22f);
                foundCounts[side] = count;
                for (int i = 0; i < LoadingStudio.PerTeam; i++)
                {
                    var card = new FoundCard { Root = RBox(foundGroup, 0f, 0f, 10f, 10f) };
                    card.Edge = new SoftEdge(card.Root, CardLean);
                    card.Face = new RSlant(CardLean, CardFace);
                    card.Root.Add(card.Face);
                    card.Emblem = RBox(card.Root, 0f, 0f, 10f, 10f);
                    card.Emblem.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                    card.Emblem.style.opacity = .45f;
                    card.Name = Bold(RLabel(card.Root, "", 22f, Ivory, true));
                    card.Name.style.overflow = Overflow.Hidden;
                    card.Name.style.textOverflow = TextOverflow.Ellipsis;
                    card.Name.style.whiteSpace = WhiteSpace.NoWrap;
                    card.Tag = Bold(RLabel(card.Root, "", 13f, Brass, false));
                    card.Flash = new RSlant(CardLean, Flash);
                    card.Flash.style.opacity = 0f;
                    card.Root.Add(card.Flash);
                    foundCards[side].Add(card);
                }
            }
            foundVs = BigVs(foundGroup, LineupTop + LineupTall / 2f - 100f);
            foundCoat = TextCoat.Apply(foundVs, StageWide, 200f, VsGloss, VsGlint, VsFrom, VsTo);
            var soon = RLabel(foundGroup, "곧 기물을 선택합니다", 18f, InkSoft, false);
            RPlace(soon, 0f, 572f, StageWide, 26f);
            const float w = 120f, gap = 12f;
            float left = (StageWide - (3f * w + 2f * gap)) / 2f;
            for (int k = 0; k < 3; k++)
            {
                var square = new RSlant(8f, DotOff);
                RPlace(square, left + k * (w + gap), 606f, w, 12f);
                square.Bar(0f, 12f, Brass);
                foundGroup.Add(square);
                foundSquares.Add(square);
            }
        }

        void BuildPick(VisualElement box)
        {
            pickGroup = RBox(box, 0f, 0f, StageWide, StageTall);
            pickTitle = BuildMatchAnnouncement(pickGroup, "소드 파이트", 16f, 40f, true, "기물 선택", "");
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
            var shadow = RBox(pickConfirm, 2f, 4f, 206f, 50f);
            shadow.style.backgroundColor = new Color(0f, 0f, 0f, .35f);
            shadow.style.borderTopLeftRadius = shadow.style.borderTopRightRadius =
                shadow.style.borderBottomLeftRadius = shadow.style.borderBottomRightRadius = 8f;
            var button = RBox(pickConfirm, 0f, 0f, 210f, 52f);
            button.name = "pick-confirm-face";
            button.style.backgroundColor = MenuArt.Hex(0x25221e);
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius =
                button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 8f;
            button.style.borderTopWidth = button.style.borderRightWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = 1f;
            var rim = new Color(Ivory.r, Ivory.g, Ivory.b, .18f);
            button.style.borderTopColor = button.style.borderRightColor = button.style.borderBottomColor = button.style.borderLeftColor = rim;
            var press = Left(RLabel(button, "확정", 22f, Ivory, true));
            RPlace(press, 22f, 0f, 84f, 52f);
            var key = RBox(button, 126f, 12f, 64f, 28f);
            key.style.backgroundColor = new Color(Ivory.r, Ivory.g, Ivory.b, .07f);
            key.style.borderTopLeftRadius = key.style.borderTopRightRadius =
                key.style.borderBottomLeftRadius = key.style.borderBottomRightRadius = 5f;
            var shortcut = RLabel(key, "SPACE", 12f, InkSoft, false);
            shortcut.style.letterSpacing = 1.2f;
            RPlace(shortcut, 0f, 0f, 64f, 28f);
            pickConfirm.RegisterCallback<PointerEnterEvent>(e =>
            {
                button.style.backgroundColor = MenuArt.Hex(0x34302a);
                button.style.translate = new Translate(0f, -1f);
            });
            pickConfirm.RegisterCallback<PointerLeaveEvent>(e =>
            {
                button.style.backgroundColor = MenuArt.Hex(0x25221e);
                button.style.translate = new Translate(0f, 0f);
            });
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
            // the halves reach 40 px past the stage on every side, so their slip along the cut never shows an edge
            clashLeft = new SwordPoly(UsDeep, new Vector2(-40f, -40f), new Vector2(SeamX(-40f), -40f), new Vector2(SeamX(760f), 760f), new Vector2(-40f, 760f));
            RPlace(clashLeft, 0f, 0f, StageWide, StageTall);
            clashShake.Add(clashLeft);
            clashRight = new SwordPoly(ThemDeep, new Vector2(SeamX(-40f), -40f), new Vector2(StageWide + 40f, -40f), new Vector2(StageWide + 40f, 760f), new Vector2(SeamX(760f), 760f));
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
            clashCut = new SwordCut(new Vector2(SeamX(-12f), -12f), new Vector2(SeamX(732f), 732f), CutWide);
            RPlace(clashCut, 0f, 0f, StageWide, StageTall);
            clashShake.Add(clashCut);
            clashFlash = RLayer(clashShake);
            clashFlash.style.backgroundColor = Flash;
            // the after-images first, so the sword itself is drawn over them; each sits with its tip on CutTop, turned
            // about its tip to point down the seam, and is moved along the seam
            var blade = Resources.Load<Texture2D>(ClashSwordPath);
            if (blade == null) Debug.LogWarning("[Loading] 결투 칼 그림이 없습니다: Resources/" + ClashSwordPath);
            var down = CutDir;
            for (int k = 0; k <= GhostBack.Length; k++)
            {
                var e = RBox(clashShake, CutTop.x - BladeLong, CutTop.y - BladeTall / 2f, BladeLong, BladeTall);
                RStretch(e, blade);
                e.style.transformOrigin = new TransformOrigin(Length.Percent(100f), Length.Percent(50f), 0f);
                e.style.rotate = new Rotate(new Angle(Mathf.Atan2(down.y, down.x) * Mathf.Rad2Deg + BladeTilt, AngleUnit.Degree));
                e.style.opacity = 0f;
                if (k < GhostBack.Length) clashGhosts.Add(e);
                else clashSword = e;
            }
            for (int k = 0; k < SparkCount; k++)
            {
                var spark = RBox(clashShake, 0f, 0f, SparkLong(k), 3f);
                spark.style.backgroundColor = k % 3 == 0 ? Flash : Gold;
                spark.style.borderTopLeftRadius = spark.style.borderTopRightRadius = spark.style.borderBottomLeftRadius = spark.style.borderBottomRightRadius = 1.5f;
                spark.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(50f), 0f);
                spark.style.opacity = 0f;
                clashSparks.Add(spark);
            }
            clashVs = BigVs(clashShake, 250f);
            clashCoat = TextCoat.Apply(clashVs, StageWide, 200f, VsGloss, VsGlint, VsFrom, VsTo);
            clashTitle = BuildMatchAnnouncement(clashShake, "소드 파이트", 508f, 40f, false, "결투 개시", "");
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
            if (!on) { swordClosedAt = -1f; swordScreen = -1; SyncSwordSound(-1f); return; }
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
            // R631: the screen's sound (LoadingScreenView.SwordPickSound.cs)
            SyncSwordSound(t);
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

        PieceKind PickOf(int i) => i >= 0 && i < State.Picks.Count ? State.Picks[i] : PieceKind.Pawn;

        static void Text(Label label, string text)
        {
            if (label.text != text) label.text = text;
        }

        void SyncFound(float t)
        {
            SyncMatchAnnouncement(foundTitle, t - .1f);
            foundInfo.style.opacity = Ph(t, .2f, .25f);
            // R640: the "떨어뜨리고 빼앗기" subtitle is gone (no steal any more), nothing in its place.
            Text(foundSize, Mathf.Max(1, sideUs.Count) + " VS " + Mathf.Max(1, sideThem.Count));
            int laid = sideUs.Count * 32 + sideThem.Count;
            if (laid != foundLaid)
            {
                foundLaid = laid;
                LayFound(0, sideUs.Count);
                LayFound(1, sideThem.Count);
            }
            int myTeam = MyTeam();
            float banner = Eo3(Ph(t, .1f, .3f));
            for (int side = 0; side < 2; side++)
            {
                var list = side == 0 ? sideUs : sideThem;
                foundHeads[side].style.translate = new Translate((side == 0 ? -1f : 1f) * 30f * (1f - banner), 0f);
                foundHeads[side].style.opacity = banner;
                Text(foundCounts[side], list.Count + "명");
                string icon = (side == 0 ? myTeam : 1 - myTeam) == 0 ? "Pawn_White" : "Pawn_Black";
                var cards = foundCards[side];
                for (int k = 0; k < cards.Count; k++)
                {
                    var card = cards[k];
                    bool shown = k < list.Count;
                    card.Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                    if (!shown) continue;
                    int i = list[k];
                    bool me = i == State.Me;
                    Text(card.Name, i < State.Names.Count ? State.Names[i] : "");
                    string tag = me ? "나" : i < State.Bots.Count && State.Bots[i] ? "봇" : "플레이어";
                    if (i == State.Manager) tag += " · 방장";
                    Text(card.Tag, tag);
                    card.Tag.style.color = me ? Ivory : Brass;
                    card.Edge.Set(me ? Ivory : Color.black);
                    // R645 (사용자 "실제 스팀 아이콘으로"): a player's Steam picture in the pawn's place, full strength and
                    // filling the square, in a thin brass frame; a bot (or a picture Steam has not sent yet) keeps the pawn.
                    var face = i < State.Faces.Count ? State.Faces[i] : null;
                    string look = face != null ? "face:" + face.GetInstanceID() : icon;
                    if (card.Icon != look)
                    {
                        card.Icon = look;
                        var texture = face != null ? face : Icon(icon);
                        card.Emblem.style.backgroundImage = texture != null ? new StyleBackground(texture) : new StyleBackground(StyleKeyword.None);
                        card.Emblem.style.backgroundSize = new BackgroundSize(face != null ? BackgroundSizeType.Cover : BackgroundSizeType.Contain);
                        card.Emblem.style.opacity = face != null ? 1f : .45f;
                        float frame = face != null ? 2f : 0f;
                        card.Emblem.style.borderTopWidth = frame; card.Emblem.style.borderBottomWidth = frame;
                        card.Emblem.style.borderLeftWidth = frame; card.Emblem.style.borderRightWidth = frame;
                        var rim = me ? Ivory : Brass;
                        card.Emblem.style.borderTopColor = rim; card.Emblem.style.borderBottomColor = rim;
                        card.Emblem.style.borderLeftColor = rim; card.Emblem.style.borderRightColor = rim;
                        float round = face != null ? 6f : 0f;
                        card.Emblem.style.borderTopLeftRadius = round; card.Emblem.style.borderTopRightRadius = round;
                        card.Emblem.style.borderBottomLeftRadius = round; card.Emblem.style.borderBottomRightRadius = round;
                    }
                    // drops in, lands (with the plate sound) with a flash, then a light sweeps across it
                    float a = FoundPlateAt + FoundPlateStep * k, land = a + PlateSlide;
                    card.Root.style.translate = new Translate(0f, -50f * (1f - Eo3(Ph(t, a, PlateSlide))));
                    card.Root.style.opacity = Ph(t, a, .1f);
                    card.Flash.style.opacity = t < land ? 0f : .08f * (1f - Ph(t, land, .16f));
                    float sweep = Ph(t, land + .05f, .4f);
                    card.Face.SliceAt(-80f + (card.Wide + 160f) * sweep, sweep > 0f && sweep < 1f ? 1f : 0f);
                }
            }
            float vs = Ph(t, FoundVsAt, .18f);
            foundVs.style.opacity = Ph(t, FoundVsAt, .06f);
            float scale = FoundVsScale * (1f + 1.4f * (1f - Eo3(vs)));
            foundVs.style.scale = new Scale(new Vector2(scale, scale));
            foundCoat.Sync(t - (FoundVsAt + .3f));
            var shake = Shake(t - (FoundVsAt + .18f), 10f, .2f, 2);
            foundGroup.style.translate = new Translate(shake.x, shake.y);
            // the three squares fill one after another across the whole screen (SwordPickDraft.FoundSeconds)
            float step = (float)SwordPickDraft.FoundSeconds / Mathf.Max(1, foundSquares.Count);
            for (int k = 0; k < foundSquares.Count; k++) foundSquares[k].BarFill(Ph(t, k * step, step));
        }

        // R637 one side's cards for `n` players: as wide as fit (at most CardWideMax), the first nearest the VS; inside, the
        // pawn above and the name and tag in the darker band at the foot, each nudged along the card's slant.
        void LayFound(int side, int n)
        {
            n = Mathf.Clamp(n, 1, LoadingStudio.PerTeam);
            float w = Mathf.Min(CardWideMax, (LineupWide - (n - 1) * LineupGap) / n), h = LineupTall;
            float near = side == 0 ? StageWide / 2f - LineupMiddle / 2f : StageWide / 2f + LineupMiddle / 2f;
            bool small = n > 3;
            float zone = small ? 62f : 76f, emblemTop = 30f, emblem = Mathf.Min(w - 22f, h - zone - emblemTop - 18f);
            for (int k = 0; k < foundCards[side].Count; k++)
            {
                var card = foundCards[side][k];
                card.Wide = w;
                float x = side == 0 ? near - (k + 1) * w - k * LineupGap : near + k * (w + LineupGap);
                RPlace(card.Root, x, LineupTop, w, h);
                card.Edge.Place(w, h);
                RPlace(card.Face, 0f, 0f, w, h);
                RPlace(card.Flash, 0f, 0f, w, h);
                card.Face.Band(h - zone, NameZone);
                card.Face.Bar(h - 6f, 6f, Brass);
                card.Face.BarFill(1f);
                card.Face.SliceLook(Mathf.Max(22f, w * .2f), 30f, -18f, new Color(1f, 1f, 1f, .08f));
                float emblemY = emblemTop + (h - zone - emblemTop - emblem) / 2f;
                RPlace(card.Emblem, (w - emblem) / 2f + Slant(emblemY + emblem / 2f, h), emblemY, emblem, emblem);
                float nameY = h - zone + (small ? 8f : 10f), nameTall = small ? 24f : 30f;
                RPlace(card.Name, 8f + Slant(nameY + nameTall / 2f, h), nameY, w - 16f, nameTall);
                card.Name.style.fontSize = small ? 15f : 20f;
                float tagY = h - (small ? 26f : 32f);
                RPlace(card.Tag, 8f + Slant(tagY + 9f, h), tagY, w - 16f, 18f);
                card.Tag.style.fontSize = small ? 11f : 13f;
            }
        }

        // how far the middle of a CardLean card sits from the box's middle at height y
        static float Slant(float y, float h) => CardLean * (.5f - y / h);

        void SyncPick(float t, float since)
        {
            SyncMatchAnnouncement(pickTitle, t);
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
            // R645 (11-14, 사용자 "너무 에디터 느낌 … 실제 게임에서 문구를 넣어줘, 필요 없으면 삭제"): short game lines, no
            // instructions - the cards and the confirm button already show how to pick.
            Text(pickHint, mine != PieceKind.Pawn ? "팀원 대기 중"
                         : swordAsked.HasValue ? "확정 중"
                         : swordMarked.HasValue ? "SPACE  확정"
                         : "");
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
            if (swordMarked != piece) SwordMark(false);
            swordMarked = piece;
        }

        void ConfirmPiece(PieceKind piece)
        {
            if (!PickOpen || Holder(piece) != null) return;
            swordMarked = piece;
            swordAsked = piece;
            SwordMark(true);
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
            var down = CutDir;
            var across = new Vector2(down.y, -down.x);   // from our half into theirs
            float tip = SwingTip(c), cutEnd = ClashHit + CutLength * .5f / SwingSpeed;
            float meet = EoBack(Ph(c, 0f, ClashMeet));
            float slip = Eo3(Ph(c, cutEnd, .3f));
            var part = down * (CutSlip * slip) - across * (CutPart * slip);
            clashLeft.style.translate = new Translate(-StageWide * (1f - meet) + part.x, part.y);
            clashRight.style.translate = new Translate(StageWide * (1f - meet) - part.x, -part.y);
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

            float hit = c - ClashHit, cooled = c - cutEnd;
            // the cut, measured from its own start 12 px above the stage
            clashCut.Set(tip + 12f / down.y, 1f - Ph(cooled, 0f, .35f));
            clashCut.style.opacity = cooled < .1f ? 1f : Mathf.Lerp(1f, .55f, Ph(cooled, .1f, .5f));
            // R636: the project blends in linear light, where .1 already reads as a light flash (.55 washed the cut out)
            clashFlash.style.opacity = hit < 0f ? 0f : .1f * (1f - Ph(hit, 0f, .12f));
            bool flying = tip > SwingFrom && tip - BladeLong < CutLength + 20f;
            clashSword.style.opacity = flying ? 1f : 0f;
            clashSword.style.translate = new Translate(down.x * tip, down.y * tip);
            for (int k = 0; k < clashGhosts.Count; k++)
            {
                float back = tip - GhostBack[k];
                clashGhosts[k].style.opacity = flying && back > SwingFrom ? GhostAlpha[k] : 0f;
                clashGhosts[k].style.translate = new Translate(down.x * back, down.y * back);
            }
            for (int k = 0; k < clashSparks.Count; k++)
            {
                // spark k leaves the tip as it passes its own point on the seam, flies on with the blade and out to one
                // side, slows in the air and drops, and goes out
                float along = (.12f + .76f * ((k * 7) % SparkCount) / (SparkCount - 1f)) * CutLength;
                float age = c - (ClashHit + (along - CutLength * .5f) / SwingSpeed), life = .28f + .04f * (k % 4);
                var spark = clashSparks[k];
                if (age < 0f || age > life) { spark.style.opacity = 0f; continue; }
                var push = down * (260f + 90f * ((k * 5) % 4)) + across * ((k % 2 == 0 ? 1f : -1f) * (160f + 70f * ((k * 3) % 5)));
                float drag = Mathf.Exp(-6f * age);
                var at = CutTop + down * along + push * ((1f - drag) / 6f) + new Vector2(0f, 700f * age * age);
                var heading = push * drag + new Vector2(0f, 1400f * age);
                float len = SparkLong(k);
                spark.style.translate = new Translate(at.x - len / 2f, at.y - 1.5f);
                spark.style.rotate = new Rotate(new Angle(Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg, AngleUnit.Degree));
                spark.style.scale = new Scale(new Vector2(.35f + .65f * drag, 1f));
                spark.style.opacity = 1f - Mathf.Pow(age / life, 2f);
            }
            float vs = Ph(c, ClashVsAt, .2f);
            clashVs.style.opacity = Ph(c, ClashVsAt, .06f);
            float scale = 1f + 2.2f * (1f - Eo3(vs));
            clashVs.style.scale = new Scale(new Vector2(scale, scale));
            clashCoat.Sync(c - (ClashVsAt + .35f));
            SyncMatchAnnouncement(clashTitle, c - ClashTitleAt);
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

        // ---- the cut (R636)

        static Vector2 CutDir => (CutBottom - CutTop).normalized;
        static float CutLength => (CutBottom - CutTop).magnitude;
        // where the seam crosses height y
        static float SeamX(float y) => CutTop.x + (CutBottom.x - CutTop.x) * (y - CutTop.y) / (CutBottom.y - CutTop.y);
        // how far down the seam the sword's tip is (stage pixels from CutTop): the middle of the screen at ClashHit
        static float SwingTip(float c) => CutLength * .5f + SwingSpeed * (c - ClashHit);
        static float SparkLong(int k) => 12f + 3f * (k % 4);

        // The gold cut, drawn from its start down to `reach` along the seam: gold, then over its last CutHot pixels
        // warming to white-hot at the tip by `heat`. Repainted only when either changes.
        sealed class SwordCut : VisualElement
        {
            readonly Vector2 from, along;
            readonly float length, half;
            float reach = -1f, heat = -1f;

            public SwordCut(Vector2 from, Vector2 to, float wide)
            {
                this.from = from;
                length = (to - from).magnitude;
                along = (to - from) / length;
                half = wide / 2f;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            public void Set(float reachTo, float heatNow)
            {
                reachTo = Mathf.Clamp(reachTo, 0f, length);
                heatNow = Mathf.Clamp01(heatNow);
                if (Mathf.Abs(reachTo - reach) < .01f && Mathf.Abs(heatNow - heat) < .002f) return;
                reach = reachTo;
                heat = heatNow;
                MarkDirtyRepaint();
            }

            void Draw(MeshGenerationContext context)
            {
                if (reach <= 0f) return;
                var mesh = context.Allocate(6, 12);
                Station(mesh, 0f, Gold);
                Station(mesh, Mathf.Max(0f, reach - CutHot), Gold);
                Station(mesh, reach, Color.Lerp(Gold, Color.white, heat));
                // each stretch between two stations as two clockwise triangles
                for (int i = 0; i < 2; i++)
                {
                    int l = i * 2;
                    mesh.SetNextIndex((ushort)l);
                    mesh.SetNextIndex((ushort)(l + 1));
                    mesh.SetNextIndex((ushort)(l + 2));
                    mesh.SetNextIndex((ushort)(l + 1));
                    mesh.SetNextIndex((ushort)(l + 3));
                    mesh.SetNextIndex((ushort)(l + 2));
                }
            }

            void Station(MeshWriteData mesh, float at, Color color)
            {
                var p = from + along * at;
                Color32 tint = color;
                mesh.SetNextVertex(new Vertex { position = new Vector3(p.x - half, p.y, Vertex.nearZ), tint = tint });
                mesh.SetNextVertex(new Vertex { position = new Vector3(p.x + half, p.y, Vertex.nearZ), tint = tint });
            }
        }

        // A filled convex polygon (the clash's two halves), in the element's own pixels.
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
