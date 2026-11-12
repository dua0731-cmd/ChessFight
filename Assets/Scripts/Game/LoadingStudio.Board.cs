using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // R548 (11-11, 승규 님 picked B "보드게임 길" of three samples, https://claude.ai/artifact/Q1pnipcuhtPnDwTcCXfS7P:
    // "보드 게임 길로 해주는데 이건 방장이 게임 시작을 누른 후 모두 보이는 화면으로 하자 ... B로 폰 움직이는 거 넣지 플레이어가
    // 이 번에는 어떤 맵이지? 라고 기대감을 가질 수 있게"): Pawn Rush's entrance screen is a board game on a dark walnut table.
    // A path of lacquered squares winds over an ebony board in three rows (near row left to right, up, middle row right to
    // left, up, far row left to right) in the colours of the palace's zones (outer court cream stone, lower palace walnut,
    // upper palace lilac marble, the war room gold). Seven stops on it, one for each of the round's sections (obstacle,
    // mini-game, obstacle, mini-game, obstacle, mini-game, the war room), end at the war room's castle with the king's crown
    // on top. R550 (11-11, 승규 님: "매칭만 잡고 있으면 우리팀이랑 상대팀만 뜨고 체스판 위에 들어오는 걸로 (옛날 거)"): while
    // the room fills the study's chessboard shows instead, the players walking onto it (LoadingStudio.Study); the board
    // appears the moment the manager starts the match (ShowSet, LoadingScreenView dips from dark over the switch), a white
    // and a black pawn (the match's own) on its start square and every stop's plate "?". The two pawns hop square by square
    // to each stop in turn; the stop's plate rolls through the sections its slot may draw and lands on this round's as they
    // land (LoadingScreenView draws the plates over the picture, at PlateAnchor). Then the castle's windows light, the crown
    // flashes and the pawns cheer.
    //
    // Every beat is a pure function of the time since the host started the match on the shared clock (RevealTime, from
    // LoadingState.Reveal) and the round's sections come from the match's seed (PawnRushCourseTable): every PC shows the
    // same frame at the same moment and names the same sections the round will be built from.
    //
    // ArtSource/Loading/PawnRushBoard/board_layout.py lays the board out the same way (change both) and board_preview.py
    // draws it through this camera with the screen's own parts on top, to check nothing covers anything.
    public sealed partial class LoadingStudio
    {
        // ---- the board's layout (board units: a square is .9 of one) ----
        const int RowSquares = 7, RowStep = 3, Stops = PawnRushSections.SlotCount;
        const float RowShift = -.6f, GateStep = 1.3f, PadBack = 1.5f;
        const float SquareTop = .16f, StopTop = .19f, RimTop = .23f;
        const float CastleWide = 1.6f, CastleDeep = 1.6f, CastleTall = 2.2f, CastleBack = .2f;
        // From the gate square's middle to the keep's wall (room for the black pawn, TokenLane off the middle), and the
        // crown's glow once the pawns are in (it flashes bigger as they arrive).
        const float CastleGap = .7f, CrownGlow = 1.7f;
        // Which squares of each row (in the order the pawns cross it) are stops.
        static readonly int[,] RowStops = { { 2, 6 }, { 2, 6 }, { 1, 5 } };
        // Each stop's zone, and each zone's two square colours.
        static readonly int[] StopZone = { 0, 0, 1, 1, 2, 2, 3 };
        static readonly int[] ZoneLight = { 0xE9DDC3, 0xC9A57A, 0xBDB4CF, 0xECC97E };
        static readonly int[] ZoneDark = { 0x8A6A49, 0x5A3B24, 0x3E3656, 0x7A5A22 };
        const int Ivory = 0xF1E6CF, Ebony = 0x1A1411, GoldLeaf = 0xE3B866, Stone = 0xE6DCC8;
        // The camera: high in front, looking down about 55 degrees at the board's middle. 26 degrees tall on a 16:9 screen;
        // on a narrower one the board keeps its width (the view grows taller), on a wider one its height.
        static readonly Vector3 BoardEye = new Vector3(0f, 25.4f, -17.3f), BoardAim = new Vector3(0f, 0f, .5f);
        // 11-11 (Unity check): the pawns a little smaller and further apart (1.2 tall, .36 off the middle) so white and
        // black no longer cover each other on a square
        const float BoardFov = 26f, TokenTall = 1.2f, TokenLane = .36f, PlateLift = 1.9f;

        // ---- the reveal's beats (seconds from the start) ----
        // A breath before the first hop, each hop, the rest on an obstacle stop and on a mini-game stop, and the end.
        public const float RevealIntro = .8f, HopSeconds = .2f, ObstacleRest = .45f, GameRest = .6f, FinaleSeconds = 1f;
        // The war room's plate is up and its flash gone: everything is shown, the scene may switch on (its freeze lands
        // in the finale's cheer, not on a plate).
        const float ShownAfterFinal = .35f;
        // Black hops this much after white, so the two read as racing rather than as one.
        const float BlackLag = .06f;

        static List<Vector3> boardPath;   // every square's middle on its top face, the start's first, the castle gate's last
        static int[] stopSquare;          // the square each stop is
        static float[] departAt, landAt;  // when the pawns leave for each stop and land on it

        bool board;
        float revealTime = -1f;
        readonly LastSceneFigure[] tokens = new LastSceneFigure[2];
        readonly Transform[] tokenShadows = new Transform[2];
        readonly Material[] tokenShadowMats = new Material[2];
        readonly int[] tokenJoined = new int[2];
        readonly float[] tokenJoinAt = { -10f, -10f };
        readonly Transform[] stopFlash = new Transform[Stops], stopGlow = new Transform[Stops];
        readonly Material[] stopFlashMats = new Material[Stops], stopGlowMats = new Material[Stops];
        readonly List<Material> boardMaterials = new List<Material>();
        readonly List<Material> windowMats = new List<Material>();
        Transform castleCrown, crownGlow;
        Material crownGlowMat;
        Light boardKey;
        float boardKeyFull;

        // True when this studio has the Pawn Rush course board (R550: shown from the reveal's start, ShowingBoard).
        public bool IsBoard => board;

        // R550: Pawn Rush's two sets, the study (the players walking onto its chessboard while matching) and the course
        // board (from the reveal's start), one shown at a time with its own camera; setShown -1 none yet, 0 the study,
        // 1 the board.
        GameObject studySet, boardSet;
        Transform studyRoot;
        int setShown = -1;
        float studyFov, boardFov;
        Color studyBackground, boardBackground;

        // True while the course board shows: the reveal has started (the host started the match).
        public bool ShowingBoard => board && revealTime >= 0f;

        void ShowSet(bool boardView)
        {
            if (!board || camera == null) return;
            int want = boardView ? 1 : 0;
            if (setShown == want) return;
            setShown = want;
            if (studySet != null) studySet.SetActive(!boardView);
            if (boardSet != null) boardSet.SetActive(boardView);
            camera.fieldOfView = boardView ? boardFov : studyFov;
            camera.nearClipPlane = boardView ? 1f : .3f;
            camera.farClipPlane = boardView ? 120f : 90f;
            camera.backgroundColor = boardView ? boardBackground : studyBackground;
        }

        // Seconds into the reveal (LoadingState.Reveal): below 0 before the start (matching).
        public float RevealTime { get => revealTime; set => revealTime = value; }

        public static float RevealSeconds { get { Beats(); return landAt[Stops - 1] + FinaleSeconds; } }
        public static float RevealShownSeconds { get { Beats(); return landAt[Stops - 1] + ShownAfterFinal; } }
        public static float StopDepart(int stop) { Beats(); return departAt[Mathf.Clamp(stop, 0, Stops - 1)]; }
        public static float StopLand(int stop) { Beats(); return landAt[Mathf.Clamp(stop, 0, Stops - 1)]; }

        static float RowX(int i) => i - (RowSquares - 1) * .5f + RowShift;
        static Vector3 Pad => new Vector3(RowX(0) - PadBack, StopTop, -RowStep);
        static Vector3 CastleAt => new Vector3(RowX(RowSquares - 1) + GateStep + CastleGap + CastleWide * .5f, 0f, RowStep + CastleBack);

        static void LayOut()
        {
            if (boardPath != null) return;
            var path = new List<Vector3>();
            var stops = new List<int>();
            for (int row = 0; row < 3; row++)
            {
                float z = (row - 1) * RowStep;
                bool right = row != 1;
                for (int k = 0; k < RowSquares; k++)
                {
                    bool stop = k == RowStops[row, 0] || k == RowStops[row, 1];
                    if (stop) stops.Add(path.Count);
                    path.Add(new Vector3(RowX(right ? k : RowSquares - 1 - k), stop ? StopTop : SquareTop, z));
                }
                // up to the next row at the end of this one
                if (row < 2)
                    for (int step = 1; step < RowStep; step++)
                        path.Add(new Vector3(RowX(right ? RowSquares - 1 : 0), SquareTop, z + step));
            }
            stops.Add(path.Count);
            path.Add(new Vector3(RowX(RowSquares - 1) + GateStep, StopTop, RowStep));
            boardPath = path;
            stopSquare = stops.ToArray();
        }

        static void Beats()
        {
            if (landAt != null) return;
            LayOut();
            departAt = new float[Stops];
            landAt = new float[Stops];
            float t = RevealIntro;
            int from = -1;
            for (int i = 0; i < Stops; i++)
            {
                departAt[i] = t;
                t += (stopSquare[i] - from) * HopSeconds;
                landAt[i] = t;
                from = stopSquare[i];
                t += PawnRushSections.Kind[i] == PawnRushSections.KindMiniGame ? GameRest : ObstacleRest;
            }
        }

        // The square `index` of the path, -1 the start.
        static Vector3 Square(int index) => index < 0 ? Pad : boardPath[Mathf.Min(index, boardPath.Count - 1)];
        static int ZoneOf(int square)
        {
            for (int i = 0; i < Stops; i++)
                if (square <= stopSquare[i]) return StopZone[i];
            return StopZone[Stops - 1];
        }
        static int StopAt(int square) => System.Array.IndexOf(stopSquare, square);

        // Shared lit materials (cached by PieceFigure.Lit: never destroyed here).
        static Material BoardLit(int rgb, float gloss, float metal = 0f) => LastSceneArt.Lit(rgb, gloss, metal);

        void Board(Transform t, int width, int height)
        {
            LayOut();
            Beats();
            board = true;
            float aspect = width / Mathf.Max(1f, height);
            float tall = Mathf.Tan(BoardFov * .5f * Mathf.Deg2Rad);
            boardFov = aspect >= 16f / 9f ? BoardFov : 2f * Mathf.Atan(tall * (16f / 9f) / aspect) * Mathf.Rad2Deg;
            boardBackground = MenuArt.Hex(0x0B0705);
            camera.fieldOfView = boardFov;
            camera.aspect = aspect;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 120f;
            camera.backgroundColor = boardBackground;
            AimBoard();

            // The table: dark walnut, wider than any screen shows.
            MenuArt.Slab(t, "Table", new Vector3(0f, -.3f, 3f), new Vector3(60f, .6f, 44f), MenuArt.Wood(MenuArt.DarkWood, .5f, new Vector2(8f, 6f)));
            // The board: a walnut edge, an ebony face and a line of gold leaf round it.
            var castle = CastleAt;
            float left = Pad.x - 1f, right = castle.x + CastleWide * .5f + .3f, near = -RowStep - 1f, far = castle.z + CastleDeep * .5f + .3f;
            var middle = new Vector3((left + right) * .5f, 0f, (near + far) * .5f);
            var size = new Vector2(right - left, far - near);
            MenuArt.Slab(t, "Board Edge", middle + Vector3.up * .05f, new Vector3(size.x, .1f, size.y), MenuArt.Wood(MenuArt.Mahogany, .6f, new Vector2(3f, 1f)));
            MenuArt.Slab(t, "Board Face", middle + Vector3.up * .103f, new Vector3(size.x - .36f, .01f, size.y - .36f), BoardLit(Ebony, .7f));
            var leaf = BoardLit(GoldLeaf, .85f, .9f);
            for (int s = -1; s <= 1; s += 2)
            {
                MenuArt.Slab(t, "Gold Leaf", middle + new Vector3(0f, .109f, s * (size.y * .5f - .42f)), new Vector3(size.x - .8f, .004f, .045f), leaf);
                MenuArt.Slab(t, "Gold Leaf", middle + new Vector3(s * (size.x * .5f - .42f), .109f, 0f), new Vector3(.045f, .004f, size.y - .8f), leaf);
            }

            // The path: lacquered squares in each zone's two colours, the stops bigger with a raised rim (brass on an obstacle,
            // gold on a mini-game, which also has a gold medallion in its middle).
            var brass = MenuArt.BrassMetal();
            var gold = BoardLit(GoldLeaf, .8f, .95f);
            for (int i = 0; i < boardPath.Count; i++)
            {
                var at = boardPath[i];
                int zone = ZoneOf(i), stop = StopAt(i);
                if (stop < 0)
                {
                    MenuArt.Slab(t, "Square", new Vector3(at.x, SquareTop - .03f, at.z), new Vector3(.9f, .06f, .9f),
                                 BoardLit(i % 2 == 0 ? ZoneLight[zone] : ZoneDark[zone], .55f));
                    continue;
                }
                bool game = PawnRushSections.Kind[stop] == PawnRushSections.KindMiniGame, final = stop == Stops - 1;
                MenuArt.Slab(t, "Stop " + (stop + 1), new Vector3(at.x, StopTop - .05f, at.z), new Vector3(1.2f, .1f, 1.2f),
                             BoardLit(game ? ZoneDark[zone] : ZoneLight[zone], .6f));
                Rim(t, at, 1.2f, game || final ? gold : brass);
                if (game) LastSceneArt.Part(t, "Medallion", LastSceneArt.Cylinder(.24f, .24f, .02f, 32), gold, new Vector3(at.x, StopTop + .01f, at.z), null, null, false);
                // a glow that stays on the stop once its section is out, and the flash as the pawns land
                int glow = game || final ? MenuArt.Gold : 0xFFE6B8;
                stopGlow[stop] = MenuArt.FloorGlow(t, new Vector3(at.x, StopTop + .015f, at.z), glow, 1.15f, 0f);
                stopGlowMats[stop] = stopGlow[stop].GetComponent<Renderer>().sharedMaterial;
                stopFlash[stop] = MenuArt.Halo(t, new Vector3(at.x, StopTop + .7f, at.z), glow, 1f, 0f, camera);
                stopFlashMats[stop] = stopFlash[stop].GetComponent<Renderer>().sharedMaterial;
                boardMaterials.Add(stopGlowMats[stop]);
                boardMaterials.Add(stopFlashMats[stop]);
            }
            // The start: an ivory square with a brass rim.
            MenuArt.Slab(t, "Start", new Vector3(Pad.x, StopTop - .05f, Pad.z), new Vector3(1.5f, .1f, 1.5f), BoardLit(Ivory, .55f));
            Rim(t, Pad, 1.5f, brass);

            Castle(t, castle, gold);

            // The light: a warm key high to the left in front, wide enough for the whole board and falling off over the
            // table, soft shadows; a faint warm fill from the camera's side and a cool rim from behind.
            boardKey = MenuArt.Spot(t, new Vector3(-3.5f, 15f, -7f), new Vector3(.3f, 0f, .6f), 0xFFDDB0, 2.6f, .62f, .75f, true);
            boardKeyFull = boardKey.intensity;
            MenuArt.Spot(t, new Vector3(0f, 10f, -16f), new Vector3(0f, 0f, 1f), 0xC8946A, .55f, .55f, .9f);
            MenuArt.Spot(t, new Vector3(2f, 7f, 15f), new Vector3(0f, .5f, 0f), 0x8FA6D0, .6f, .6f, .9f);

            // The two pawns: the match's own Pawn Rush pawn in each team's colour, TokenTall high whatever the model's size.
            float scale = TokenTall / Mathf.Max(.5f, PawnHeight(t));
            for (int team = 0; team < 2; team++)
            {
                var at = TokenSpot(-1, team);
                tokens[team] = MenuArt.Figure(t, PieceKind.Pawn, team, at, BoardEye, scale);
                tokens[team].WearPawnRushLook();
                var shadow = Blob(t, at + Vector3.up * .01f);
                shadow.localScale = new Vector3(.75f, .62f, 1f);
                tokenShadows[team] = shadow;
                tokenShadowMats[team] = shadow.GetComponent<Renderer>().sharedMaterial;
                tokenShadowMats[team].color = new Color(.05f, .03f, .02f, .55f);
                boardMaterials.Add(tokenShadowMats[team]);
            }
            AnimateBoard(0f);
        }

        // A raised rim round a stop: four thin bars standing proud of its face.
        static void Rim(Transform t, Vector3 at, float wide, Material material)
        {
            float h = RimTop - StopTop + .02f, y = StopTop + h * .5f - .02f, half = wide * .5f;
            MenuArt.Slab(t, "Rim", new Vector3(at.x, y, at.z - half), new Vector3(wide + .07f, h, .07f), material);
            MenuArt.Slab(t, "Rim", new Vector3(at.x, y, at.z + half), new Vector3(wide + .07f, h, .07f), material);
            MenuArt.Slab(t, "Rim", new Vector3(at.x - half, y, at.z), new Vector3(.07f, h, wide), material);
            MenuArt.Slab(t, "Rim", new Vector3(at.x + half, y, at.z), new Vector3(.07f, h, wide), material);
        }

        // The war room's castle: an ivory keep with battlements, two round towers behind it with ebony roofs, its gate in the
        // side facing the last stop, windows that light up as the pawns arrive, and the king's crown turning on top.
        void Castle(Transform t, Vector3 c, Material gold)
        {
            var stone = BoardLit(Stone, .3f);
            var ebony = BoardLit(0x241B17, .55f);
            var dark = BoardLit(0x140D09, .1f);
            float baseY = .1f, top = baseY + CastleTall, hw = CastleWide * .5f, hd = CastleDeep * .5f;
            MenuArt.Slab(t, "Keep", new Vector3(c.x, baseY + CastleTall * .5f, c.z), new Vector3(CastleWide, CastleTall, CastleDeep), stone);
            MenuArt.Slab(t, "Plinth", new Vector3(c.x, baseY + .06f, c.z), new Vector3(CastleWide + .14f, .12f, CastleDeep + .14f), stone);
            // battlements round the top
            for (int k = 0; k < 4; k++)
            {
                float x = c.x - hw + .2f + k * (CastleWide - .4f) / 3f;
                MenuArt.Slab(t, "Merlon", new Vector3(x, top + .14f, c.z - hd + .1f), new Vector3(.24f, .28f, .2f), stone);
                MenuArt.Slab(t, "Merlon", new Vector3(x, top + .14f, c.z + hd - .1f), new Vector3(.24f, .28f, .2f), stone);
            }
            for (int k = 0; k < 2; k++)
            {
                float z = c.z - .3f + k * .6f;
                MenuArt.Slab(t, "Merlon", new Vector3(c.x - hw + .1f, top + .14f, z), new Vector3(.2f, .28f, .24f), stone);
                MenuArt.Slab(t, "Merlon", new Vector3(c.x + hw - .1f, top + .14f, z), new Vector3(.2f, .28f, .24f), stone);
            }
            // two round towers at the back corners, ebony roofs with a gold ball
            for (int s = -1; s <= 1; s += 2)
            {
                var foot = new Vector3(c.x + s * hw, baseY, c.z + hd);
                const float towerTall = 2.9f;
                LastSceneArt.Part(t, "Tower", LastSceneArt.Cylinder(.34f, .32f, towerTall, 28), stone, foot + Vector3.up * towerTall * .5f);
                LastSceneArt.Part(t, "Roof", LastSceneArt.Cone(.44f, .75f, 28), ebony, foot + Vector3.up * (towerTall + .375f));
                LastSceneArt.Part(t, "Finial", LastSceneArt.Sphere(.07f, 12, 8), gold, foot + Vector3.up * (towerTall + .8f));
            }
            // the gate, in the side facing the last stop, framed in gold
            float gx = c.x - hw - .005f, gz = RowStep, gateTall = 1.15f;
            MenuArt.Slab(t, "Gate", new Vector3(gx, baseY + gateTall * .5f, gz), new Vector3(.02f, gateTall, .72f), dark);
            MenuArt.Slab(t, "Gate Lintel", new Vector3(gx - .01f, baseY + gateTall + .04f, gz), new Vector3(.03f, .08f, .86f), gold);
            MenuArt.Slab(t, "Gate Post", new Vector3(gx - .01f, baseY + gateTall * .5f, gz - .4f), new Vector3(.03f, gateTall, .07f), gold);
            MenuArt.Slab(t, "Gate Post", new Vector3(gx - .01f, baseY + gateTall * .5f, gz + .4f), new Vector3(.03f, gateTall, .07f), gold);
            // windows on the face towards the camera: dark until the pawns arrive, then warm light
            for (int k = 0; k < 3; k++)
            {
                var w = new Vector3(c.x - .45f + k * .45f, baseY + (k == 1 ? 1.55f : 1.35f), c.z - hd - .006f);
                MenuArt.Slab(t, "Window", w + Vector3.forward * .004f, new Vector3(.15f, .34f, .01f), dark);
                var lit = LastSceneArt.Part(t, "Window Light", LastSceneArt.Panel(.15f, .34f), MenuArt.Glow("castle window", null, MenuArt.Hex(0xFFC66B), false),
                                            w - Vector3.forward * .004f, null, null, false);
                var material = lit.GetComponent<Renderer>().sharedMaterial;
                SetAlpha(material, 0f);
                windowMats.Add(material);
                boardMaterials.Add(material);
            }
            // the king's crown over the keep, turning slowly, and a glow behind it for the finish
            var holder = LastSceneArt.Group(t, "Crown", new Vector3(c.x, top + .42f, c.z));
            holder.transform.localScale = Vector3.one * 1.5f;
            LastSceneFigure.KingCrown(holder.transform);
            castleCrown = holder.transform;
            crownGlow = MenuArt.Halo(t, new Vector3(c.x, top + .7f, c.z), MenuArt.Gold, CrownGlow, 0f, camera);
            crownGlowMat = crownGlow.GetComponent<Renderer>().sharedMaterial;
            boardMaterials.Add(crownGlowMat);
        }

        // Where a pawn stands on square `index` (-1 the start), side by side across the screen: white left, black right.
        static Vector3 TokenSpot(int index, int team) => Square(index) + Vector3.right * (team == 0 ? -TokenLane : TokenLane);

        // ---- each frame ----

        void SyncBoard(IReadOnlyList<int> teams, IReadOnlyList<bool> joined, float now)
        {
            int[] count = { 0, 0 };
            for (int i = 0; i < teams.Count; i++)
                if (i < joined.Count && joined[i]) count[teams[i] == 0 ? 0 : 1]++;
            for (int team = 0; team < 2; team++)
            {
                // someone came into this team: its pawn hops on the start square
                if (count[team] > tokenJoined[team] && revealTime < 0f) tokenJoinAt[team] = now;
                tokenJoined[team] = count[team];
            }
        }

        void AnimateBoard(float now)
        {
            if (!board) return;
            float r = revealTime;
            for (int team = 0; team < 2; team++) AnimateToken(team, r < 0f ? -1f : Mathf.Max(0f, r - (team == 1 ? BlackLag : 0f)), now);

            // the stops: a flash as the pawns land on one, then a glow that stays
            for (int i = 0; i < Stops; i++)
            {
                float a = r < 0f ? -1f : r - landAt[i];
                if (stopFlash[i] != null)
                {
                    bool on = a >= 0f && a < .55f;
                    SetAlpha(stopFlashMats[i], on ? .9f * (1f - a / .55f) : 0f);
                    stopFlash[i].localScale = Vector3.one * (on ? .8f + a * 5.5f : .01f);
                }
                if (stopGlowMats[i] != null) SetAlpha(stopGlowMats[i], a < 0f ? 0f : .42f * Mathf.Clamp01(a / .3f));
            }
            // the castle: the windows light as the pawns reach its gate, the crown flashes, then glows on
            float end = r < 0f ? -1f : r - landAt[Stops - 1];
            foreach (var material in windowMats) SetAlpha(material, end < 0f ? 0f : Mathf.Clamp01(end / .25f));
            if (crownGlow != null)
            {
                float flash = end < 0f ? 0f : end < .6f ? 1f - end / .6f * .55f : .45f + .08f * Mathf.Sin(now * 2.2f);
                // 11-11 (Unity check): smaller and fainter than at first (2.4 + 1.6, .8), which covered the towers
                SetAlpha(crownGlowMat, flash * .65f);
                crownGlow.localScale = Vector3.one * (end < 0f ? .01f : CrownGlow + (end < .6f ? (1f - end / .6f) * 1.1f : 0f));
            }
            if (castleCrown != null) castleCrown.localRotation = Quaternion.Euler(0f, now * 28f, 0f);
            // the room wakes up as the reveal starts
            if (boardKey != null) boardKey.intensity = boardKeyFull * (r < 0f ? .8f : Mathf.Lerp(.8f, 1f, Mathf.Clamp01(r / RevealIntro)));
        }

        // One pawn at reveal time `r` (below 0: waiting on the start square).
        void AnimateToken(int team, float r, float now)
        {
            var figure = tokens[team];
            if (figure == null) return;
            FigurePose pose;
            if (r < 0f || r < departAt[0])
            {
                // on the start square, facing the camera, turning to the path just before the first hop; a hop on the spot
                // when a teammate comes in
                var start = TokenSpot(-1, team);
                float yaw = Yaw(start, BoardEye);
                if (r >= 0f) yaw = Mathf.LerpAngle(yaw, Yaw(Vector3.zero, Square(0) - Square(-1)), Smooth((r - (departAt[0] - .14f)) / .14f));
                float j = now - tokenJoinAt[team];
                if (r < 0f && j >= 0f && j < .4f)
                {
                    float h = Mathf.Sin(j / .4f * Mathf.PI);
                    pose = new FigurePose { Jump = h * .45f, Squash = j < .05f ? .1f : -.05f * h, ArmLeft = .4f + 1.9f * h, ArmRight = .4f + 1.9f * h };
                }
                // the room is full and everyone is ready: both pawns cheer on the start
                else if (r < 0f && Cheer) pose = CheerPose(now, team);
                else pose = IdlePose(now, team, .7f);
                Place(figure, start, yaw, pose);
                Shadow(team, start, pose.Jump);
                return;
            }
            for (int i = 0; i < Stops; i++)
            {
                int from = i == 0 ? -1 : stopSquare[i - 1];
                if (r < landAt[i])
                {
                    if (r < departAt[i])
                    {
                        // resting on the previous stop
                        Rest(figure, team, i - 1, r, now);
                        return;
                    }
                    // hopping: square to square
                    float s = (r - departAt[i]) / HopSeconds;
                    int hop = Mathf.Min(Mathf.FloorToInt(s), stopSquare[i] - from - 1);
                    float f = Mathf.Clamp01(s - hop);
                    var a = TokenSpot(from + hop, team);
                    var b = TokenSpot(from + hop + 1, team);
                    var at = Vector3.Lerp(a, b, f);
                    var toward = b - a;
                    float up = Mathf.Sin(f * Mathf.PI);
                    float lift = up * (.42f + .12f * new Vector2(toward.x, toward.z).magnitude);
                    pose = new FigurePose
                    {
                        Jump = lift, Lean = .18f, Squash = f < .1f ? .1f : f > .9f ? .08f : -.06f * up,
                        ArmLeft = .35f + 1.4f * up, ArmRight = .35f + 1.4f * up, Leg = .35f * up
                    };
                    Place(figure, at, Yaw(Vector3.zero, toward), pose);
                    Shadow(team, at, lift);
                    return;
                }
            }
            Rest(figure, team, Stops - 1, r, now);
        }

        // Standing on a stop: turn to the camera after landing (a cheer on a mini-game, the castle's gate), turn back
        // to the way on before the next hop.
        void Rest(LastSceneFigure figure, int team, int stop, float r, float now)
        {
            var at = TokenSpot(stopSquare[stop], team);
            float came = Yaw(Vector3.zero, Square(stopSquare[stop]) - Square(stopSquare[stop] - 1));
            float cam = Yaw(at, BoardEye);
            float a = r - landAt[stop];
            float yaw = Mathf.LerpAngle(came, cam, Smooth(a / .16f));
            if (stop < Stops - 1)
            {
                float left = departAt[stop + 1] - r;
                float next = Yaw(Vector3.zero, Square(stopSquare[stop] + 1) - Square(stopSquare[stop]));
                yaw = Mathf.LerpAngle(next, yaw, Smooth(left / .14f));
            }
            bool cheer = stop == Stops - 1 || PawnRushSections.Kind[stop] == PawnRushSections.KindMiniGame;
            FigurePose pose;
            if (a < .12f) pose = new FigurePose { Squash = .14f * (1f - a / .12f), ArmLeft = .5f, ArmRight = .5f };
            else if (stop == Stops - 1) pose = CheerPose(now, team);
            else if (cheer && a < .55f)
            {
                float h = Mathf.Sin(Mathf.Clamp01((a - .12f) / .43f) * Mathf.PI);
                pose = new FigurePose { Jump = h * .18f, ArmLeft = .6f + 1.8f * h, ArmRight = .6f + 1.8f * h };
            }
            else pose = IdlePose(now, team, .55f);
            Place(figure, at, yaw, pose);
            Shadow(team, at, pose.Jump);
        }

        static FigurePose IdlePose(float t, int i, float amp)
        {
            float b = Mathf.Max(0, Mathf.Sin(t * 2.4f + i * 1.3f));
            return new FigurePose { Jump = b * .06f * amp, Squash = -.04f * b, Arm = .25f + .2f * Mathf.Sin(t * 2.4f + i) };
        }

        static FigurePose CheerPose(float t, int i)
        {
            float b = Mathf.Max(0, Mathf.Sin(t * 3.2f + i * 1.1f));
            return new FigurePose { Jump = b * .2f, Squash = -.06f * b, ArmLeft = 1.6f + .9f * b, ArmRight = 1.6f + .9f * b };
        }

        // Stands a pawn at `at` facing `yaw` (degrees round Y, 0 away from the camera) in `pose`.
        static void Place(LastSceneFigure figure, Vector3 at, float yaw, in FigurePose pose)
        {
            float rad = yaw * Mathf.Deg2Rad;
            figure.Place(at, at + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)));
            figure.Pose(pose);
        }

        // The soft dark patch under a pawn, lighter and wider while it is up in a hop.
        void Shadow(int team, Vector3 at, float lift)
        {
            var shadow = tokenShadows[team];
            if (shadow == null) return;
            float k = Mathf.Clamp01(lift / .6f);
            shadow.localPosition = new Vector3(at.x, at.y + .012f, at.z);
            shadow.localScale = new Vector3(.75f, .62f, 1f) * (1f + .25f * k);
            SetAlpha(tokenShadowMats[team], .55f * (1f - .55f * k));
        }

        // The heading from `from` to `to` in degrees (Mathf.LerpAngle's).
        static float Yaw(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        void AimBoard()
        {
            if (camera == null) return;
            var eye = BoardEye;
            // a small knock as the pawns land on a mini-game stop and at the castle (the same on every PC: from the beats)
            if (revealTime >= 0f)
                for (int i = 0; i < Stops; i++)
                {
                    float a = revealTime - landAt[i];
                    if (a < 0f || a > .3f) continue;
                    float amp = i == Stops - 1 ? .09f : PawnRushSections.Kind[i] == PawnRushSections.KindMiniGame ? .05f : .02f;
                    float k = (1f - a / .3f) * (1f - a / .3f);
                    eye += new Vector3(Mathf.Sin(a * 71f + i) * amp * k, Mathf.Sin(a * 53f + 2f * i) * amp * k, 0f);
                }
            camera.transform.localPosition = eye;
            camera.transform.rotation = Quaternion.LookRotation(BoardAim - BoardEye);
        }

        // Where stop `stop`'s plate goes, as a point of the camera's view (0..1 from the bottom left): `plate` the middle of
        // its bottom edge, PlateLift over the stop (the war room's over its crown), `tail` the end of the line down towards
        // the stop, just over the pawns' heads. False when the board is not built or not showing (R550: the study while
        // matching).
        public bool PlateAnchor(int stop, out Vector2 plate, out Vector2 tail)
        {
            plate = tail = Vector2.zero;
            if (!ShowingBoard || camera == null || root == null || stop < 0 || stop >= Stops) return false;
            Vector3 top, end;
            if (stop == Stops - 1)
            {
                // a little left of the crown: between the title's lines and the black team's list on 16:9 and 16:10
                var c = CastleAt;
                top = new Vector3(c.x - .4f, .1f + CastleTall + 1.45f, c.z);
                end = new Vector3(c.x, .1f + CastleTall + 1.05f, c.z);
            }
            else
            {
                var at = Square(stopSquare[stop]);
                top = at + Vector3.up * PlateLift;
                end = at + Vector3.up * (TokenTall + .12f);
            }
            plate = View(top);
            tail = View(end);
            return true;
        }

        // Where the start's label goes: under the start square.
        public bool StartAnchor(out Vector2 at)
        {
            at = Vector2.zero;
            if (!ShowingBoard || camera == null || root == null) return false;
            at = View(Pad + new Vector3(0f, 0f, -.95f));
            return true;
        }

        Vector2 View(Vector3 local)
        {
            var v = camera.WorldToViewportPoint(root.transform.TransformPoint(local));
            return new Vector2(v.x, v.y);
        }

        void DisposeBoard()
        {
            foreach (var material in boardMaterials) if (material != null) Object.Destroy(material);
            boardMaterials.Clear();
            windowMats.Clear();
        }
    }
}
