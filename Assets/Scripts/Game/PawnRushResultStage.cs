using System.Collections.Generic;
using System.Linq;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    // The Pawn Rush result stage (design "결승 중계" on scene B, Docs/Architecture/UI.md §13):
    // a chessboard on a table under a hanging lamp in the dark walnut hall. The winners
    // stand on the near rank of the victory camera and cheer, the losers' king loses its
    // crown and falls, the other losers sit down; gold rings light under each finisher in
    // finish order and the finish that decided the match bursts.
    //
    // A port of the approved review page's world (worldB in its gl.js). Positions are
    // kept in the page's coordinates, where the winners stand at +Z and the victory
    // camera looks from +Z; W turns them into Unity's (Z flipped). Everything is a
    // function of the time since the result began, so a replay is the same show.
    public sealed class PawnRushResultStage
    {
        public const float WinBoardAt = 3.2f, LoseBoardAt = 3.8f;
        // The board's face: base .55 + frame .34 + .005 (squareStage with table: true).
        const float H = .895f;
        const float RankZ = 3f;

        // King and queen on the middle files, so a falling king is never under a panel.
        static float FileX(PieceKind k) =>
            k == PieceKind.Rook ? -3f : k == PieceKind.Knight ? -1.8f : k == PieceKind.King ? -.6f :
            k == PieceKind.Queen ? .6f : k == PieceKind.Bishop ? 1.8f : 3f;
        static readonly float[] Files = { -3f, -1.8f, -.6f, .6f, 1.8f, 3f };
        // Cheer stagger and idle phase: king first, then outward.
        static int Order(PieceKind k) =>
            k == PieceKind.King ? 0 : k == PieceKind.Queen ? 1 : k == PieceKind.Rook ? 2 : k == PieceKind.Bishop ? 3 : k == PieceKind.Knight ? 4 : 5;

        static Vector3 W(float x, float y, float z) => new Vector3(x, y, -z);
        static Vector3 W(Vector3 v) => new Vector3(v.x, v.y, -v.z);
        static Color Hex(int rgb) => PieceFigure.Hex(rgb);
        static Color Lin(int rgb) => Hex(rgb).linear;
        static float Clamp01(float x) => Mathf.Clamp01(x);
        static float EaseIO(float x) { x = Clamp01(x); return x * x * (3 - 2 * x); }
        static float EaseIn(float x) { x = Clamp01(x); return x * x; }
        static float Hop(float u, float a, float b) => u < a || u > b ? 0 : Mathf.Sin(Mathf.PI * (u - a) / (b - a));

        sealed class Piece
        {
            public ResultPlayer Player;
            public LastSceneFigure Figure;
            public float X, Z;         // page coordinates
            public float Yaw;          // page yaw it faces (for moves in its own frame)
            public int Order;
            public Renderer[] Parts;
        }

        sealed class Ring
        {
            public Transform Root;
            public MeshRenderer Renderer;
            public Color Color;        // linear
            public float X, Z;
            public int Beat;
            public bool Counts, Decides;
        }

        // The page's FRAMES['1']: a low hero shot for the ceremony, then the broadcast
        // framing that keeps the pieces between the top ribbon and the scoreboard.
        static readonly Vector3 HeroP = new Vector3(0, 4.5f, 12.4f), HeroL = new Vector3(0, 2.05f, 1.1f);
        static readonly Vector3 FrameP = new Vector3(0, 7.6f, 18.4f), FrameL = new Vector3(0, .55f, -.6f);

        readonly Camera view;
        readonly bool win;
        readonly float boardAt, side;           // side: +1 victory camera at +Z, -1 lose camera at -Z
        readonly Vector3 heroP, heroL, frameP, frameL;
        readonly List<Piece> pieces = new List<Piece>();
        readonly List<Ring> rings = new List<Ring>();
        readonly Piece me, loserKing, winnerKing;
        readonly Ring decider;
        readonly Vector2 kingW, kingB;          // the winners' and losers' king squares
        readonly Transform lamp, lampCone;
        readonly Light lampSpot, cold;
        readonly Material coneMaterial;
        readonly GameObject looseCrown;
        readonly List<(Vector3 p, Vector3 r)> crownPath = new List<(Vector3, Vector3)>();
        readonly MeshRenderer[] cells = new MeshRenderer[64];
        readonly Vector2[] cellAt = new Vector2[64];
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly Transform meRing;
        readonly MeshRenderer meRenderer;
        readonly Dots burst, fireworks, motes, glows, dust;
        readonly (Vector3 d, float v, Color c)[] burstData = new (Vector3, float, Color)[90];
        readonly (int b, Vector3 c, Vector3 d, float v, Color col)[] sparkData = new (int, Vector3, Vector3, float, Color)[6 * 56];
        readonly (float a, float v, float y)[] puffData = new (float, float, float)[26];
        readonly (float x, float y, float z, float ph, float speed)[] moteData = new (float, float, float, float, float)[180];
        readonly Confetti confetti;
        readonly Backdrop backdrop;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly Color Gold = Lin(0xFFC24A), Blue = Lin(0x8FB2FF);
        float mix, lastTime = -1f;

        public bool HasSelf => me != null;

        // Just above the top of my piece, wherever it is (also when it sits or lies down).
        public Vector3 SelfHead
        {
            get
            {
                if (me == null) return Vector3.zero;
                var box = new Bounds(me.Figure.Position, Vector3.zero);
                foreach (var r in me.Parts) if (r != null && r.enabled) box.Encapsulate(r.bounds);
                return new Vector3(box.center.x, box.max.y + .08f, box.center.z);
            }
        }

        public PawnRushResultStage(Transform world, Camera camera, MatchResult result, int myTeam, bool win)
        {
            view = camera;
            this.win = win;
            boardAt = win ? WinBoardAt : LoseBoardAt;
            side = win ? 1f : -1f;
            heroP = Side(HeroP); heroL = Side(HeroL); frameP = Side(FrameP); frameL = Side(FrameL);
            myTeam = myTeam == 1 ? 1 : 0;
            int winners = result.WinningTeam >= 0 ? result.WinningTeam : (win ? myTeam : 1 - myTeam);

            Hall(world);
            backdrop = new Backdrop(view);
            Table(world);

            // ---- the lamp: hangs from the dark, sways; its light and beam follow the shade ----
            lamp = LastSceneArt.Group(world, "Lamp", W(0, 24, 0)).transform;
            LastSceneArt.Part(lamp, "Cord", LastSceneArt.Cylinder(.035f, .035f, 7.2f, 8), LastSceneArt.Lit(0x2A1E14, .5f, .7f), new Vector3(0, -3.6f, 0), null, null, false);
            LastSceneArt.Part(lamp, "Shade", Shade(), LastSceneArt.Lit(0xD9AE62, .7f, .85f), new Vector3(0, -7.6f, 0), null, null, false);
            LastSceneArt.Part(lamp, "Cap", LastSceneArt.Cylinder(.3f, .22f, .3f, 20), MenuArt.BrassMetal(), new Vector3(0, -7f, 0), null, null, false);
            LastSceneArt.Part(lamp, "Bulb", LastSceneArt.Sphere(.26f, 16, 12), MenuArt.Lamp(0xFFF2D8), new Vector3(0, -7.95f, 0), null, null, false);
            lampSpot = MenuArt.Spot(world, W(0, 15.9f, 0), W(0, H, 0), 0xFFE6C2, 2.1f, .42f, .55f, true);
            lampSpot.shadowStrength = .85f;
            lampCone = MenuArt.Beam(world, W(0, 15.9f, 0), W(0, H, 0), 0xFFE8C8, 4.6f, .06f);
            coneMaterial = lampCone.GetComponent<MeshRenderer>().sharedMaterial;
            MenuArt.Spot(world, W(-11, 8, 9), W(0, H, 0), MenuArt.Warm, 1.15f, .5f, .7f);
            MenuArt.Spot(world, W(11, 8, -9), W(0, H, 0), MenuArt.Warm, 1.15f, .5f, .7f);
            MenuArt.Spot(world, W(0, 6, -14), W(0, H + 1, 0), 0xFFD9A8, 1.0f, .5f, .7f);
            MenuArt.Spot(world, W(0, 6, 14), W(0, H + 1, 0), 0xFFD9A8, .7f, .5f, .7f);
            cold = MenuArt.Spot(world, W(0, 12, -4), W(0, H, -3), 0x9DB6E2, 0f, .42f, .7f, true);

            // ---- the pieces: winners on the +Z rank, losers on the -Z rank, all facing the camera ----
            var lookZ = FrameP.z * side;
            for (int team = 0; team < 2; team++)
            {
                float z = team == winners ? RankZ : -RankZ;
                var taken = new bool[6];
                var players = result.Players.Where(p => p.Team == team).Take(6).ToList();
                var chosen = new List<(ResultPlayer p, float x)>();
                foreach (var p in players)
                {
                    int f = System.Array.IndexOf(Files, FileX(p.Piece));
                    if (taken[f]) continue;
                    taken[f] = true;
                    chosen.Add((p, Files[f]));
                }
                foreach (var p in players)
                {
                    if (chosen.Any(c => c.p == p)) continue;
                    int f = System.Array.FindIndex(taken, t => !t);
                    if (f < 0) break;
                    taken[f] = true;
                    chosen.Add((p, Files[f]));
                }
                foreach (var (p, x) in chosen)
                {
                    var figure = LastSceneFigure.Build(p.Piece, team, world);
                    float lookX = x * .3f;
                    figure.Place(W(x, H, z), W(lookX, H, lookZ));
                    pieces.Add(new Piece
                    {
                        Player = p, Figure = figure, X = x, Z = z, Yaw = Mathf.Atan2(lookX - x, lookZ - z), Order = Order(p.Piece),
                        Parts = figure.Root.GetComponentsInChildren<Renderer>()
                    });
                }
            }
            me = pieces.FirstOrDefault(p => p.Player.Self);
            winnerKing = pieces.FirstOrDefault(p => p.Player.Team == winners && p.Player.Piece == PieceKind.King);
            loserKing = pieces.FirstOrDefault(p => p.Player.Team != winners && p.Player.Piece == PieceKind.King && p.Figure.Crown != null);
            kingW = winnerKing != null ? new Vector2(winnerKing.X, winnerKing.Z) : new Vector2(-.6f, RankZ);
            kingB = loserKing != null ? new Vector2(loserKing.X, loserKing.Z) : new Vector2(-.6f, -RankZ);

            // The losers' king crown: thrown up and sideways, bounces twice, rolls to a stop.
            looseCrown = LastSceneArt.Group(world, "Crown (popped)", Vector3.zero);
            LastSceneFigure.KingCrown(looseCrown.transform);
            looseCrown.SetActive(false);
            if (loserKing != null)
            {
                loserKing.Figure.Pose(default);
                CrownPath(W(loserKing.Figure.Crown.transform.position), win ? 1f : -1f);
            }

            // ---- light: the 64 squares, the finish rings, my ring ----
            var squareMaterial = Additive("square");
            var square = LastSceneArt.Panel(1.14f, 1.14f);
            for (int i = 0, k = 0; i < 8; i++)
                for (int j = 0; j < 8; j++, k++)
                {
                    float x = -4.2f + i * 1.2f, z = -4.2f + j * 1.2f;
                    cells[k] = Glow(world, "Square Light", square, squareMaterial, W(x, H + .007f, z), Quaternion.Euler(90f, 0, 0));
                    cellAt[k] = new Vector2(x, z);
                }
            var ringMaterial = Additive("ring");
            var order = result.FinishOrder();
            for (int i = 0; i < order.Count; i++)
            {
                var line = order[i];
                var piece = pieces.FirstOrDefault(p => p.Player == line.Player);
                if (piece == null) continue;
                int col = line.CountsToWin ? 0xFFD27A : (line.Player.Team == 0 ? 0x7FDFFF : 0xFF6B7E);
                var r = Glow(world, "Finish Ring", MenuArt.Torus(.56f, .05f, 48, 8), ringMaterial, W(piece.X, H + .012f, piece.Z), Quaternion.identity);
                rings.Add(new Ring { Root = r.transform, Renderer = r, Color = Lin(col), X = piece.X, Z = piece.Z, Beat = i, Counts = line.CountsToWin, Decides = line.Clinched });
            }
            decider = rings.FirstOrDefault(r => r.Decides);
            meRenderer = Glow(world, "Me Ring", MenuArt.Torus(.74f, .045f, 56, 8), ringMaterial, Vector3.zero, Quaternion.identity);
            meRing = meRenderer.transform;

            // ---- sparks: the decisive burst, fireworks, the king's dust, cold motes, glows ----
            // Point sizes are the page's (three.js points: size * 360 / depth px) in world units.
            var additive = Dots.AdditiveMaterial();
            burst = new Dots(world, "Decisive Burst", 90, .3f * .268f, additive);
            fireworks = new Dots(world, "Fireworks", sparkData.Length, .26f * .268f, additive);
            motes = new Dots(world, "Cold Motes", moteData.Length, .08f * .268f, additive);
            glows = new Dots(world, "Glows", 2, 1f, additive);
            dust = new Dots(world, "King Dust", puffData.Length, .55f * .268f, MenuArt.Glow("result dust", MenuArt.RadialTexture(), Color.white));
            var rnd = new Seeded(53);
            for (int k = 0; k < burstData.Length; k++)
            {
                float u = .15f + rnd.Next() * .85f, a = rnd.Next() * 6.283f, s = Mathf.Sqrt(1 - u * u);
                burstData[k] = (new Vector3(s * Mathf.Cos(a), u, s * Mathf.Sin(a)), 3f + rnd.Next() * 2.6f, Lin(k % 4 == 0 ? 0xFFFFFF : (k % 3 != 0 ? 0xFFD27A : 0xFFB45A)));
            }
            rnd = new Seeded(41);
            int[] hues = { 0xFFD27A, 0xFFF3DA, 0x9BE8FF, 0xFFB45A };
            for (int b = 0, n = 0; b < 6; b++)
            {
                var c = new Vector3(-3.6f + rnd.Next() * 7.2f, H + 5.2f + rnd.Next() * 2.6f, -1f + rnd.Next() * 3.4f);
                for (int k = 0; k < 56; k++, n++)
                {
                    float u = rnd.Next() * 2 - 1, a = rnd.Next() * 6.283f, s = Mathf.Sqrt(1 - u * u);
                    sparkData[n] = (b, c, new Vector3(s * Mathf.Cos(a), u, s * Mathf.Sin(a)), 2.4f + rnd.Next() * 1.4f, Lin(k % 5 == 0 ? 0xFFFFFF : hues[b % 4]));
                }
            }
            rnd = new Seeded(77);
            for (int k = 0; k < puffData.Length; k++) puffData[k] = (rnd.Next() * 6.283f, .9f + rnd.Next() * 1.3f, .15f + rnd.Next() * .5f);
            rnd = new Seeded(9);
            for (int k = 0; k < moteData.Length; k++)
                moteData[k] = ((rnd.Next() * 2 - 1) * 4.2f, rnd.Next() * 6f, -3f + (rnd.Next() * 2 - 1) * 4.2f * .7f, rnd.Next() * 6.3f, .04f + rnd.Next() * .1f);
            confetti = new Confetti(H);
        }

        Vector3 Side(Vector3 v) => new Vector3(v.x, v.y, v.z * side);

        // ---------- the hall, the table, the lamp shade ----------

        void Hall(Transform world)
        {
            MenuArt.Hall(world, view, 26f, 90f);
            // The page draws its warm background behind a transparent canvas; here a backdrop
            // at the far plane does that (Backdrop), so the camera clears to the haze colour.
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = Hex(MenuArt.HallColor);
            view.fieldOfView = 30f;
            view.nearClipPlane = .1f;
            view.farClipPlane = 300f;
            // Ambient as the page's hemisphere light plus its warm hall environment, and that
            // environment (not the scene's default blue sky) in every reflection.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex(0x5A3C26);
            RenderSettings.ambientEquatorColor = Hex(0x4A3020);
            RenderSettings.ambientGroundColor = Hex(0x120A06);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = HallReflection();
            RenderSettings.reflectionIntensity = 1f;
        }

        void Table(Transform world)
        {
            var table = LastSceneArt.Group(world, "Table", Vector3.zero).transform;
            MenuArt.Slab(table, "Base", new Vector3(0, .275f, 0), new Vector3(10.8f, .55f, 10.8f), MenuArt.Wood(MenuArt.DarkWood, .55f, new Vector2(3f, 1f)));
            MenuArt.Slab(table, "Frame", new Vector3(0, .55f + .17f, 0), new Vector3(10.4f, .34f, 10.4f), MenuArt.Wood(MenuArt.Mahogany, .68f, new Vector2(2f, 1f)));
            // The squares: lacquer (the page's roughness .2, a little duller here, where no tone
            // mapping tames the lamp's highlight). Texture (0, 0), a1 walnut, is at the victory
            // camera's near left.
            var board = new Material(PieceFigure.Lit(Color.white, .66f, .05f)) { name = "Result board", mainTexture = MenuArt.BoardTexture(8) };
            var top = LastSceneArt.Part(table, "Squares", LastSceneArt.Panel(9.6f, 9.6f), board, new Vector3(0, H, 0), Quaternion.Euler(90f, 0, 0), null, false);
            top.GetComponent<MeshRenderer>().receiveShadows = true;
            var brass = MenuArt.BrassMetal();
            for (int s = -1; s <= 1; s += 2)
            {
                MenuArt.Slab(table, "Inlay", W(0, H + .006f, s * (4.8f + .06f)), new Vector3(9.6f + .18f, .025f, .06f), brass);
                MenuArt.Slab(table, "Inlay", W(s * (4.8f + .06f), H + .006f, 0), new Vector3(.06f, .025f, 9.6f + .18f), brass);
            }
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    LastSceneArt.Part(table, "Corner", LastSceneArt.RoundedBox(new Vector3(.42f, .07f, .42f), .03f), brass, W(sx * 5f, H + .02f, sz * 5f));
            BoardLetters(table);
        }

        // a-h along both ends of the frame and 1-8 along its sides, in gold, read from outside.
        static void BoardLetters(Transform parent)
        {
            var glyphs = BoardGlyphs.Build();
            if (glyphs == null) return;
            var quad = LastSceneArt.Panel(.34f, .34f);
            const string files = "abcdefgh";
            for (int i = 0; i < 8; i++)
            {
                float x = -4.8f + .6f + i * 1.2f;
                // (position, the glyph's up on the board) in page coordinates.
                Place(files[i], new Vector3(x, 0, 5f), new Vector3(0, 0, -1));
                Place((char)('1' + i), new Vector3(5f, 0, -x), new Vector3(1, 0, 0));
                Place(files[i], new Vector3(x, 0, -5f), new Vector3(0, 0, 1));
                Place((char)('1' + i), new Vector3(-5f, 0, -x), new Vector3(-1, 0, 0));
            }

            void Place(char c, Vector3 at, Vector3 up)
            {
                var material = glyphs.Material(c);
                if (material == null) return;
                var go = LastSceneArt.Part(parent, "Letter " + c, quad, material, W(at.x, H + .012f, at.z),
                                           Quaternion.LookRotation(Vector3.down, W(up)), null, false);
                go.GetComponent<MeshRenderer>().receiveShadows = false;
            }
        }

        // The lamp's open brass cone, seen inside and out: a lathe shell, inner face first.
        static Mesh Shade() => LastSceneArt.Lathe("result shade", new[]
        {
            new Vector2(.02f, .48f), new Vector2(1.23f, -.5f), new Vector2(1.23f, -.5f),
            new Vector2(1.25f, -.5f), new Vector2(1.25f, -.5f), new Vector2(.02f, .5f)
        }, 40);

        // ---------- poses (the page's idle, crouch, cheer, kingDown, sitDown) ----------

        static FigurePose Idle(float t, int i, float amp)
        {
            float b = Mathf.Max(0, Mathf.Sin(t * 2.4f + i * 1.3f));
            return new FigurePose { Jump = b * .08f * amp, Squash = -.04f * b, Arm = .25f + .2f * Mathf.Sin(t * 2.4f + i) };
        }

        static FigurePose Crouch(float e)
        {
            float s = Mathf.Sin(Mathf.PI * Clamp01(e));
            return new FigurePose { Squash = .17f * s, ArmLeft = -.25f, ArmRight = -.25f, Lean = .12f * s };
        }

        // A move of `fwd` toward where the piece faces and `side` to its right, as the page's localMove.
        static void Move(Piece piece, float fwd, float right, ref FigurePose pose)
        {
            float y = piece.Yaw;
            pose.Dx = fwd * Mathf.Sin(y) + right * Mathf.Cos(y);
            pose.Dz = fwd * Mathf.Cos(y) - right * Mathf.Sin(y);
        }

        static FigurePose Cheer(Piece piece, float t, int i)
        {
            float d = t - i * .16f;
            if (d < 0) return Idle(t, i, .6f);
            var kind = piece.Player.Piece;
            float period = kind == PieceKind.Rook ? .62f : 1.5f;
            int n = Mathf.FloorToInt(d / period);
            float u = (d - n * period) / period;
            float Land(float a, float b) => u < a ? .15f * (1 - u / a) : (u > b && u < b + .08f ? .12f : 0f);
            if (kind == PieceKind.King)
            {
                float h = Hop(u, .08f, .64f);
                return new FigurePose
                {
                    Jump = h * 1.6f, Flip = n % 2 == 1 ? -Mathf.PI * 2 * EaseIO((u - .1f) / .5f) : 0f,
                    Squash = Land(.08f, .64f) - .06f * h, ArmLeft = .4f + 2.1f * h, ArmRight = .4f + 2.1f * h
                };
            }
            if (kind == PieceKind.Queen)
            {
                float h = Hop(u, .06f, .62f);
                return new FigurePose { Jump = h * 1.05f, Yaw = Mathf.PI * 2 * EaseIO((u - .06f) / .56f), Squash = Land(.06f, .62f) - .05f * h, ArmLeft = 2.4f, ArmRight = .6f + 1.8f * h };
            }
            if (kind == PieceKind.Rook)
            {
                float h = Hop(u, 0, .66f);
                return new FigurePose { Jump = h * .42f, Squash = u > .66f ? .2f * (1 - (u - .66f) / .34f) : -.04f * h, ArmLeft = .5f + .9f * h, ArmRight = .5f + .9f * h };
            }
            float hh = Hop(u, .06f, .58f), e = EaseIO((u - .06f) / .52f), s = n % 2 != 0 ? 1 - e : e;
            var p = new FigurePose { Jump = hh * (kind == PieceKind.Pawn ? .7f : .85f), Squash = Land(.06f, .58f) - .05f * hh, ArmLeft = 1.3f + hh, ArmRight = 1.3f + hh };
            if (kind == PieceKind.Bishop) Move(piece, .55f * s, .55f * s, ref p);
            else if (kind == PieceKind.Knight) Move(piece, .95f * Clamp01(s * 1.5f), .5f * Clamp01(s * 3 - 2), ref p);
            else Move(piece, .95f * s, 0, ref p);
            return p;
        }

        // The losers' king: trembles, its crown flies off at 1.0 s, falls on its back at 1.6 s.
        // (LastSceneFigure lifts a lying figure by its half width, the page's "jump .3 e".)
        static FigurePose KingDown(float rt, float t)
        {
            if (rt < 1f) return new FigurePose { Arm = .22f, Squash = .02f * Mathf.Sin(t * 2.4f) };
            if (rt < 1.6f) return new FigurePose { Yaw = Mathf.Sin(rt * 55f) * .05f, Squash = .04f, ArmLeft = .35f, ArmRight = .35f, Dx = Mathf.Sin(rt * 61f) * .02f };
            float e = EaseIn((rt - 1.6f) / .32f), b = rt > 1.92f ? Mathf.Exp(-(rt - 1.92f) * 6f) * Mathf.Abs(Mathf.Sin((rt - 1.92f) * 16f)) * .08f : 0f;
            return new FigurePose { Pitch = -1.42f * e, Jump = b, ArmLeft = .3f + 1.1f * e, ArmRight = .3f + 1.1f * e };
        }

        // The other losers drop where they stand (sinking to the hips), the pawn pounding the board.
        static FigurePose SitDown(float rt, float start, bool pound)
        {
            if (rt < start) return new FigurePose { Arm = .15f, Squash = .03f };
            float e = EaseIO((rt - start) / .3f), k = pound && rt > start + .4f ? Mathf.Abs(Mathf.Sin((rt - start) * 6.5f)) : 0f;
            return new FigurePose { Jump = -.24f * e, Squash = .1f * e, Lean = .32f * e, ArmLeft = .15f + (pound ? .2f : .05f), ArmRight = .15f + (pound ? .25f + 1.1f * k : .05f) };
        }

        void CrownPath(Vector3 from, float dir)
        {
            const float dt = 1 / 120f;
            Vector3 p = from, v = new Vector3(dir * 2.1f, 4.6f, 1.2f), rot = Vector3.zero;
            for (int i = 0; i <= 480; i++)
            {
                crownPath.Add((p, rot));
                v.y -= 13f * dt;
                p += v * dt;
                rot.x += v.magnitude * dt * 1.6f;
                rot.z += dir * v.magnitude * dt * 2.2f;
                if (p.y < H + .08f && v.y < 0)
                {
                    p.y = H + .08f; v.y = -v.y * .42f; v.x *= .62f; v.z *= .62f;
                    if (Mathf.Abs(v.y) < .6f) v.y = 0;
                }
                if (p.y <= H + .081f) { v.x *= .985f; v.z *= .985f; }
            }
        }

        public float BeatAt(int i) => boardAt + .45f + i * .24f;

        // A replay starts the camera from the ceremony shot again.
        public void Reset() { mix = 0; lastTime = -1f; }

        // ---------- the show ----------

        // `time` runs on, `tp` restarts with each replay, `package` is whether the board is shown.
        public void Update(float time, float tp, bool package)
        {
            float rt = tp, step = lastTime < 0 ? 0 : Mathf.Clamp(time - lastTime, 0f, .25f);
            lastTime = time;

            foreach (var piece in pieces)
            {
                FigurePose pose;
                if (loserKing != null && piece == loserKing) pose = KingDown(rt, time);
                else if (piece.Z > 0)
                    pose = rt < .45f ? Idle(time, piece.Order, .5f) : rt < .9f ? Crouch((rt - .45f) / .45f) : Cheer(piece, rt - .9f, piece.Order);
                else pose = SitDown(rt, 2f + Mathf.Abs(piece.X - kingB.x) * .09f, piece.Player.Piece == PieceKind.Pawn);
                piece.Figure.Pose(pose);
            }
            if (loserKing != null)
            {
                bool popped = rt >= 1f;
                loserKing.Figure.Crown.SetActive(!popped);
                looseCrown.SetActive(popped);
                if (popped)
                {
                    var s = crownPath[Mathf.Clamp(Mathf.FloorToInt((rt - 1f) * 120f), 0, crownPath.Count - 1)];
                    looseCrown.transform.position = W(s.p);
                    looseCrown.transform.rotation = Quaternion.Euler(-s.r.x * Mathf.Rad2Deg, 0, s.r.z * Mathf.Rad2Deg);
                }
            }

            // squares: the victory wave from the winners' king, their shimmer, the impact ring
            // under the falling king, and a steady glow under each finisher who counted
            float r = (rt - .95f) * 7.5f, ri = (rt - 1.92f) * 6f;
            for (int k = 0; k < 64; k++)
            {
                var c = cellAt[k];
                float g = 0, b = 0;
                if (rt > .95f) { float d = Vector2.Distance(c, kingW); g += Mathf.Exp(-(d - r) * (d - r) / .9f) * .6f * Mathf.Max(0, 1 - r / 15f); }
                if (c.y > .5f && rt > 1.6f) g += (.06f + .045f * Mathf.Sin(time * 2.6f + c.x * 1.3f + c.y * .7f)) * EaseIO((rt - 1.6f) / 1.2f);
                if (rt > 1.92f) { float d = Vector2.Distance(c, kingB); b += Mathf.Exp(-(d - ri) * (d - ri) / .55f) * .5f * Mathf.Max(0, 1 - ri / 9f); }
                foreach (var ring in rings)
                    if (ring.Counts && Mathf.Abs(c.x - ring.X) < .1f && Mathf.Abs(c.y - ring.Z) < .1f)
                    {
                        float t = rt - BeatAt(ring.Beat);
                        if (t > 0) g += .3f + .08f * Mathf.Sin(time * 3f + ring.Beat) + .6f * Mathf.Max(0, 1 - t * 2.5f);
                    }
                bool on = g + b > .004f;
                cells[k].enabled = on;
                if (on) Emit(cells[k], Gold * g + Blue * b);
            }

            foreach (var ring in rings)
            {
                float t = rt - BeatAt(ring.Beat), k = ring.Counts ? 1f : .7f;
                float a = t > 0 ? k * Mathf.Min(1f, t * 4f) * (.75f + .2f * Mathf.Sin(time * 3f + ring.Beat)) : 0f;
                ring.Renderer.enabled = a > .002f;
                if (a > .002f) Emit(ring.Renderer, ring.Color * a);
                ring.Root.localScale = Vector3.one * (t > 0 ? 1 + (ring.Decides ? .9f : .35f) * Mathf.Max(0, 1 - t * 3f) : 1f);
            }
            if (me != null)
            {
                meRing.position = W(me.X, H + .014f, me.Z);
                float a = rt > .6f ? .55f + .3f * Mathf.Sin(time * 4.2f) : 0f;
                meRenderer.enabled = a > 0;
                if (a > 0) Emit(meRenderer, Lin(0x7FDFFF) * a);
                meRing.localScale = Vector3.one * (1 + .06f * Mathf.Sin(time * 4.2f));
            }

            // the deciding finish lands: a burst of gold from that piece
            float dt = decider != null ? rt - BeatAt(decider.Beat) : -1f;
            bool burstOn = dt > 0 && dt < 1.9f;
            for (int k = 0; k < burstData.Length; k++)
            {
                if (!burstOn) { burst.Hide(k); continue; }
                var q = burstData[k];
                float e = (1 - Mathf.Exp(-2.4f * dt)) / 2.4f, f = Mathf.Pow(1 - dt / 1.9f, 1.4f);
                burst.Set(k, W(decider.X + q.d.x * q.v * e, H + 1.2f + q.d.y * q.v * e - 1.1f * dt * dt, decider.Z + q.d.z * q.v * e), q.c * f);
            }
            burst.Flush(view);

            // fireworks over the winners' half, six bursts that repeat (seen from both sides)
            const float cycle = 5.4f;
            for (int k = 0; k < sparkData.Length; k++)
            {
                var q = sparkData[k];
                float ts = 1f + q.b * .9f;
                int n = rt >= ts ? Mathf.FloorToInt((rt - ts) / cycle) : -1;
                float tau = n >= 0 ? rt - ts - n * cycle : -1f;
                if (!(tau >= 0 && tau < 1.7f)) { fireworks.Hide(k); continue; }
                float e = (1 - Mathf.Exp(-2.2f * tau)) / 2.2f, f = Mathf.Pow(1 - tau / 1.7f, 1.6f);
                fireworks.Set(k, W(q.c.x + q.d.x * q.v * e, q.c.y + q.d.y * q.v * e - .9f * tau * tau, q.c.z + q.d.z * q.v * e), q.col * f);
            }
            fireworks.Flush(view);

            // the losers' king lands: dust, a flicker of the lamp, a small shake
            float it = rt - 1.92f;
            bool puff = it > 0 && it < 1.4f && loserKing != null;
            for (int k = 0; k < puffData.Length; k++)
            {
                if (!puff) { dust.Hide(k); continue; }
                var q = puffData[k];
                var kp = W(loserKing.Figure.Position);
                float e = (1 - Mathf.Exp(-3f * it)) / 3f;
                var col = Lin(0xC9A57A);
                col.a = .55f * Mathf.Max(0, 1 - it / 1.4f);
                dust.Set(k, W(kp.x + Mathf.Cos(q.a) * q.v * e * 1.4f, H + q.y * e * 2f, kp.z + Mathf.Sin(q.a) * q.v * e * 1.4f), col);
            }
            dust.Flush(view);

            float flick = !win && it > 0 && it < .45f ? (Mathf.Sin(it * 70f) > .2f ? .45f : 1f) : 1f;
            float swing = .018f * Mathf.Sin(time * 1.1f) + (!win && it > 0 ? .07f * Mathf.Exp(-it * 1.1f) * Mathf.Sin(it * 3.4f) : 0f);
            lamp.localRotation = Quaternion.Euler(-.012f * Mathf.Sin(time * .8f) * Mathf.Rad2Deg, 0, swing * Mathf.Rad2Deg);
            var shade = lamp.TransformPoint(new Vector3(0, -7.9f, 0));
            lampSpot.transform.position = shade;
            lampSpot.transform.rotation = Quaternion.LookRotation(W(0, H, 0) - shade);
            MenuArt.Aim(lampCone, shade, new Vector3(shade.x * .4f, H, shade.z * .4f));
            float cool = win ? 0f : EaseIO((rt - 2.4f) / 1.6f);
            lampSpot.intensity = 2.1f * flick * (1 - .25f * cool);
            var cone = coneMaterial.color; cone.a = .06f * flick * (1 - .4f * cool); coneMaterial.color = cone;
            cold.intensity = 1.6f * cool;
            glows.Set(0, lamp.TransformPoint(new Vector3(0, -8.1f, 0)), Lin(0xFFF0D8) * .9f * flick, 3.4f);

            // cold motes drifting over the losers after a defeat
            for (int k = 0; k < moteData.Length; k++)
            {
                if (cool < .01f) { motes.Hide(k); continue; }
                var q = moteData[k];
                float y = (q.y - time * q.speed) % 6f;
                if (y < 0) y += 6f;
                motes.Set(k, W(q.x + Mathf.Sin(time * .5f + q.ph) * .18f, y, q.z + Mathf.Cos(time * .4f + q.ph) * .1f), Lin(0xCFE0FF) * .7f * cool);
            }
            motes.Flush(view);

            // the winning king's crown shines, and pops when the decisive finish lands
            if (winnerKing != null && rt > .9f)
            {
                float pop = dt > 0 && dt < .6f ? 1 - dt / .6f : 0f;
                float a = Mathf.Min(1f, .55f + .25f * Mathf.Sin(time * 4f) + .5f * pop);
                glows.Set(1, winnerKing.Figure.Root.transform.TransformPoint(new Vector3(0, 1.95f, 0)), Lin(0xFFD27A) * a, 1.4f + .3f * Mathf.Sin(time * 4f) + 3f * pop);
            }
            else glows.Hide(1);
            glows.Flush(view);

            confetti.Draw(rt);

            // ---- camera: a low hero shot for the ceremony (a crane down on a win, close on the
            // falling king on a loss), the broadcast framing once the package is in, and back to
            // the hero shot while it is hidden ----
            Vector3 p, l;
            if (win)
            {
                float e = EaseIO(rt / 1.7f);
                p = Vector3.Lerp(new Vector3(heroP.x * .2f, heroP.y + 6.5f, heroP.z * .6f), heroP, e);
                l = heroL;
            }
            else
            {
                float e = EaseIO((rt - 2.2f) / 2.2f);
                p = Vector3.Lerp(new Vector3(kingB.x * .6f, H + 4.1f, kingB.y * 2.95f), heroP, e);
                l = Vector3.Lerp(new Vector3(kingB.x, H + 1f, kingB.y), heroL, e);
            }
            float want = package && rt >= boardAt ? 1f : 0f;
            mix += (want - mix) * (1f - Mathf.Exp(-step * 2.6f));
            if (Mathf.Abs(want - mix) < .002f) mix = want;
            float mx = EaseIO(mix);
            p = Vector3.Lerp(p, frameP, mx);
            l = Vector3.Lerp(l, frameL, mx);
            float sh = Mathf.Max(it > 0 && it < .5f ? (win ? .03f : .14f) * (1 - it / .5f) : 0f, win && dt > 0 && dt < .35f ? .05f * (1 - dt / .35f) : 0f) * GameSettings.ShakeScale;
            p.x += Mathf.Sin(time * .13f) * .35f + Mathf.Sin(rt * 61f) * sh;
            p.y += Mathf.Sin(rt * 53f) * sh;
            view.transform.position = W(p);
            view.transform.LookAt(W(l));
            backdrop.Update(time);
        }

        // ---------- materials and helpers ----------

        void Emit(Renderer renderer, Color linear)
        {
            block.SetVector(EmissionId, new Vector4(linear.r, linear.g, linear.b, 1f));
            renderer.SetPropertyBlock(block);
        }

        static MeshRenderer Glow(Transform parent, string name, Mesh mesh, Material material, Vector3 at, Quaternion rotation)
        {
            var go = LastSceneArt.Part(parent, name, mesh, material, at, rotation, null, false);
            var r = go.GetComponent<MeshRenderer>();
            r.receiveShadows = false;
            r.enabled = false;
            return r;
        }

        // Additive light that ignores the lamp: Standard with a black albedo, its emission (set per
        // renderer) blended One + One, as the page's additive square lights and rings.
        static Material Additive(string key)
        {
            var shader = Shader.Find("Standard");
            if (shader == null) return MenuArt.Glow("result " + key, null, Color.white);
            var m = new Material(shader) { name = "Result " + key, color = Color.black };
            m.SetFloat("_Glossiness", 0f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.SetFloat("_GlossyReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            m.SetInt("_SrcBlend", (int)BlendMode.One);
            m.SetInt("_DstBlend", (int)BlendMode.One);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", Color.black);
            m.renderQueue = (int)RenderQueue.Transparent + 5;
            return m;
        }

        // The page's hall environment (envMap 'hall'): warm haze, dark above, near black below,
        // with the lamp and three warm windows of light, as the reflection on every glossy part.
        static Cubemap hallCube;
        static Cubemap HallReflection()
        {
            if (hallCube != null) return hallCube;
            const int size = 64;
            var cube = new Cubemap(size, TextureFormat.RGBA32, true) { name = "Result hall reflection" };
            Color top = Hex(0x140D08), horizon = Hex(0x5A3A22), ground = Hex(0x070403);
            var panels = new[]
            {
                (W(0, 40, 0).normalized, Hex(0xFFF1DA), .36f), (W(-30, 22, -20).normalized, Hex(0xFFB45A), .2f),
                (W(30, 22, -20).normalized, Hex(0xFFB45A), .2f), (W(0, 18, 35).normalized, Hex(0xFFE2B8), .22f)
            };
            var faces = new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ };
            var px = new Color[size * size];
            foreach (var face in faces)
            {
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        // Cubemap faces are stored from the top row down.
                        float u = 2f * (x + .5f) / size - 1f, v = 2f * (y + .5f) / size - 1f;
                        Vector3 d = face == CubemapFace.PositiveX ? new Vector3(1, -v, -u)
                                  : face == CubemapFace.NegativeX ? new Vector3(-1, -v, u)
                                  : face == CubemapFace.PositiveY ? new Vector3(u, 1, v)
                                  : face == CubemapFace.NegativeY ? new Vector3(u, -1, -v)
                                  : face == CubemapFace.PositiveZ ? new Vector3(u, -v, 1)
                                  : new Vector3(-u, -v, -1);
                        d.Normalize();
                        var c = d.y >= 0 ? Color.Lerp(horizon, top, Mathf.Pow(d.y, .55f)) : Color.Lerp(horizon, ground, Mathf.Min(1f, -d.y * 4f));
                        foreach (var (dir, col, spread) in panels)
                        {
                            float k = Mathf.Clamp01((Vector3.Dot(d, dir) - (1f - spread * spread)) / (spread * spread));
                            c = Color.Lerp(c, col, k * k * (3 - 2 * k));
                        }
                        c.a = 1;
                        px[y * size + x] = c;
                    }
                cube.SetPixels(px, face);
            }
            cube.Apply(true);
            return hallCube = cube;
        }

        // Park-Miller, as the page seeds its random numbers.
        sealed class Seeded
        {
            long s;
            public Seeded(int seed) { s = System.Math.Abs(seed) % 2147483646 + 1; }
            public float Next() { s = s * 16807 % 2147483647; return (s - 1) / 2147483646f; }
        }

        // ---------- the warm background behind everything ----------

        // The page's .bg (a warm radial glow from the top) and its three swaying shafts of
        // light, drawn behind the 3D on a canvas transparent wherever nothing is. Here: quads
        // at the far end of the camera's view, which the hall floor hides where it reaches.
        sealed class Backdrop
        {
            const float Distance = 250f;
            readonly Camera view;
            readonly Transform plate;
            readonly (Transform t, float left, float a1, float a2, float period)[] beams;

            public Backdrop(Camera view)
            {
                this.view = view;
                // One per camera: a rebuilt stage replaces the last one's.
                var old = view.transform.Find("Backdrop");
                if (old != null) Object.Destroy(old.gameObject);
                var root = LastSceneArt.Group(view.transform, "Backdrop", Vector3.zero).transform;
                var glow = LastSceneArt.Blended("result backdrop", Gradient(), Color.white, false);
                glow.renderQueue = (int)RenderQueue.Transparent - 100;
                plate = LastSceneArt.Part(root, "Backdrop Glow", LastSceneArt.Panel(1f, 1f), glow, new Vector3(0, 0, Distance), null, null, false).transform;
                var shaft = ShaftTexture();
                beams = new[]
                {
                    (Shaft(root, shaft, new Color(1f, 190 / 255f, 110 / 255f)), .18f, -18f, 8f, 7f),
                    (Shaft(root, shaft, new Color(1f, 170 / 255f, 90 / 255f)), .82f, 16f, -8f, 8f),
                    (Shaft(root, shaft, new Color(1f, 228 / 255f, 180 / 255f)), .5f, -6f, 6f, 9f)
                };
            }

            static Transform Shaft(Transform root, Texture2D texture, Color tint)
            {
                var m = LastSceneArt.Blended("result shaft " + ColorUtility.ToHtmlStringRGB(tint), texture, tint, false);
                m.renderQueue = (int)RenderQueue.Transparent - 99;
                return LastSceneArt.Part(root, "Backdrop Shaft", TopPivotQuad(), m, Vector3.zero, null, null, false).transform;
            }

            public void Update(float time)
            {
                // Laid out on the page's 1280 x 720 stage, scaled to the view's height.
                float h = 2f * Distance * Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad), px = h / 720f;
                plate.localScale = new Vector3(h * view.aspect, h, 1f);
                plate.localPosition = new Vector3(0, 0, Distance);
                foreach (var (t, left, a1, a2, period) in beams)
                {
                    // CSS: alternate, ease-in-out, from a1 to a2 over `period` seconds.
                    float k = time % (period * 2f) / period;
                    if (k > 1f) k = 2f - k;
                    float angle = Mathf.Lerp(a1, a2, k * k * (3 - 2 * k));
                    t.localPosition = new Vector3((left * 1280f - 640f) * px, (360f + 40f) * px, Distance - 1f);
                    t.localRotation = Quaternion.Euler(0, 0, -angle);
                    t.localScale = new Vector3(280f * px, 900f * px, 1f);
                }
            }

            // radial-gradient(100% 70% at 50% 0%, #4A2E19 0%, #26180E 42%, #140D08 70%)
            static Texture2D Gradient()
            {
                const int w = 320, h = 180;
                var px = new Color[w * h];
                Color a = Hex(0x4A2E19), b = Hex(0x26180E), c = Hex(0x140D08);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float dx = (x + .5f) / w - .5f, dy = (h - y - .5f) / (h * .7f);   // y = 0 is the bottom row
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        px[y * w + x] = r < .42f ? Color.Lerp(a, b, r / .42f) : r < .7f ? Color.Lerp(b, c, (r - .42f) / .28f) : c;
                    }
                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Result backdrop" };
                texture.SetPixels(px);
                texture.Apply();
                return texture;
            }

            // A shaft: narrow at the top (44 % - 56 %), full width at the bottom, soft edges,
            // .28 alpha at the top fading out by three quarters of the way down.
            static Texture2D ShaftTexture()
            {
                const int w = 64, h = 256;
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                {
                    float t = 1f - (y + .5f) / h;   // 0 at the top
                    float half = .06f + .44f * t, fade = .28f * Mathf.Clamp01(1f - t / .75f);
                    for (int x = 0; x < w; x++)
                    {
                        float off = Mathf.Abs((x + .5f) / w - .5f);
                        float edge = Mathf.Clamp01((half + .025f - off) / .05f);
                        px[y * w + x] = new Color(1, 1, 1, fade * edge * edge * (3 - 2 * edge));
                    }
                }
                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Result shaft" };
                texture.SetPixels(px);
                texture.Apply();
                return texture;
            }

            static Mesh quad;
            static Mesh TopPivotQuad()
            {
                if (quad != null) return quad;
                quad = new Mesh { name = "Result shaft quad" };
                quad.vertices = new[] { new Vector3(-.5f, 0), new Vector3(.5f, 0), new Vector3(.5f, -1f), new Vector3(-.5f, -1f) };
                quad.uv = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
                quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                quad.RecalculateNormals();
                quad.RecalculateBounds();
                return quad;
            }
        }

        // ---------- a cloud of soft dots in one mesh ----------

        // Sparks, dust, motes and glows: each frame the caller places every dot (its colour
        // already faded) and flushes the mesh toward the camera.
        sealed class Dots
        {
            readonly Mesh mesh;
            readonly Vector3[] centers, verts;
            readonly Color[] colors, vertColors;
            readonly float[] sizes;
            readonly bool additive;

            // The page's additive points; a build without the legacy particle shader blends instead.
            public static Material AdditiveMaterial()
            {
                var shader = Shader.Find("Legacy Shaders/Particles/Additive");
                if (shader == null) return MenuArt.Glow("result sparks", MenuArt.RadialTexture(), Color.white);
                var m = new Material(shader) { name = "Result sparks", mainTexture = MenuArt.RadialTexture() };
                m.SetColor("_TintColor", new Color(.5f, .5f, .5f, .5f));
                m.renderQueue = (int)RenderQueue.Transparent + 20;
                return m;
            }

            public Dots(Transform parent, string name, int count, float size, Material material)
            {
                additive = material.shader != null && material.shader.name.Contains("Additive");
                centers = new Vector3[count];
                colors = new Color[count];
                sizes = Enumerable.Repeat(size, count).ToArray();
                verts = new Vector3[count * 4];
                vertColors = new Color[count * 4];
                var uv = new Vector2[count * 4];
                var tri = new int[count * 6];
                for (int i = 0; i < count; i++)
                {
                    uv[i * 4] = new Vector2(0, 0); uv[i * 4 + 1] = new Vector2(1, 0); uv[i * 4 + 2] = new Vector2(1, 1); uv[i * 4 + 3] = new Vector2(0, 1);
                    int t = i * 6, v = i * 4;
                    tri[t] = v; tri[t + 1] = v + 2; tri[t + 2] = v + 1; tri[t + 3] = v; tri[t + 4] = v + 3; tri[t + 5] = v + 2;
                }
                mesh = new Mesh { name = name };
                mesh.MarkDynamic();
                mesh.vertices = verts;
                mesh.uv = uv;
                mesh.colors = vertColors;
                mesh.triangles = tri;
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2000f);
                var go = LastSceneArt.Part(parent, name, mesh, material, Vector3.zero, null, null, false);
                go.GetComponent<MeshRenderer>().receiveShadows = false;
            }

            // `color` is linear with its fade already in it; for blended dots its alpha is the
            // fade too. Additive dots add the colour as it is (the page's additive points).
            public void Set(int i, Vector3 position, Color color, float size = -1f)
            {
                centers[i] = position;
                if (additive) color.a = 1f;
                else if (color.a >= 1f) color.a = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
                colors[i] = color;
                if (size > 0) sizes[i] = size;
            }

            public void Hide(int i) => colors[i] = Color.clear;

            public void Flush(Camera camera)
            {
                Vector3 right = camera.transform.right * .5f, up = camera.transform.up * .5f;
                for (int i = 0; i < centers.Length; i++)
                {
                    var c = centers[i];
                    float s = sizes[i];
                    Vector3 r = right * s, u = up * s;
                    verts[i * 4] = c - r - u; verts[i * 4 + 1] = c + r - u; verts[i * 4 + 2] = c + r + u; verts[i * 4 + 3] = c - r + u;
                    var col = colors[i];
                    vertColors[i * 4] = vertColors[i * 4 + 1] = vertColors[i * 4 + 2] = vertColors[i * 4 + 3] = col;
                }
                mesh.vertices = verts;
                mesh.colors = vertColors;
            }
        }

        // ---------- confetti over the winners' half ----------

        // 320 pieces falling from ten units above the board and starting over when they reach
        // it, fluttering and tumbling (the page's confetti() without cannons). Lit like the
        // page's emissive-tinted metal paper and drawn instanced, one batch per colour.
        sealed class Confetti
        {
            static readonly int[] Colors = { 0xFFC93D, 0xFFE7A6, 0xF2C14E, 0xFFF3DA, 0xD9AE62, 0xE7C28C, 0x3CD0FF };
            readonly (float x, float z, float off, float v, float sw, float sf, float sp, Vector3 axis, float rs)[] pieces = new (float, float, float, float, float, float, float, Vector3, float)[320];
            readonly Mesh mesh = LastSceneArt.Panel(.17f, .1f);
            readonly Material[] materials;
            readonly List<Matrix4x4>[] batches;
            readonly float floor;

            public Confetti(float floor)
            {
                this.floor = floor;
                materials = Colors.Select(Paper).ToArray();
                batches = Colors.Select(_ => new List<Matrix4x4>(64)).ToArray();
                var r = new Seeded(23);
                for (int i = 0; i < pieces.Length; i++)
                {
                    float x = -4.8f + r.Next() * 9.6f, z = -1.2f + r.Next() * 6f, off = r.Next() * 3.6f, v = .8f + r.Next() * .7f;
                    float sw = .15f + r.Next() * .35f, sf = 1.2f + r.Next() * 2.2f, sp = r.Next() * 6.3f;
                    r.Next(); r.Next(); r.Next();
                    var axis = new Vector3(r.Next() * 2 - 1, r.Next() * 2 - 1, r.Next() * 2 - 1).normalized;
                    pieces[i] = (x, z, off, v, sw, sf, sp, axis, 4f + r.Next() * 9f);
                    r.Next();
                }
            }

            static Material Paper(int rgb)
            {
                var m = new Material(PieceFigure.Lit(Hex(rgb), .55f, .35f)) { name = "Result confetti " + rgb.ToString("X6"), enableInstancing = true };
                if (m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    m.SetColor("_EmissionColor", Hex(rgb) * .28f + Hex(0x2A1A08) * .6f);
                }
                return m;
            }

            public void Draw(float rt)
            {
                foreach (var b in batches) b.Clear();
                const float height = 10f;
                if (rt >= 1f)
                    for (int i = 0; i < pieces.Length; i++)
                    {
                        var q = pieces[i];
                        float tau = rt - 1f - q.off;
                        if (tau <= 0) continue;
                        float tt = tau % (height / q.v);
                        var at = W(q.x + Mathf.Sin(tau * q.sf + q.sp) * q.sw, floor + height - tt * q.v, q.z + Mathf.Cos(tau * q.sf * .7f + q.sp) * q.sw * .6f);
                        batches[i % batches.Length].Add(Matrix4x4.TRS(at, Quaternion.AngleAxis(tau * q.rs * Mathf.Rad2Deg, W(q.axis)), Vector3.one));
                    }
                for (int i = 0; i < batches.Length; i++)
                    if (batches[i].Count > 0)
                        Graphics.DrawMeshInstanced(mesh, 0, materials[i], batches[i], null, ShadowCastingMode.Off, false);
            }
        }

        // ---------- the board's letters ----------

        // Gold a-h and 1-8 cut from the display font (Black Han Sans) into textures once, so a
        // rebuild of the font's atlas never touches them. Top to bottom #FFF0C2, #E9C57A, #8A6224,
        // as the page's letterTex.
        sealed class BoardGlyphs
        {
            readonly Dictionary<char, Material> materials = new Dictionary<char, Material>();
            public Material Material(char c) => materials.TryGetValue(c, out var m) ? m : null;

            public static BoardGlyphs Build()
            {
                var font = RuntimePanels.DisplayFont;
                if (font == null) return null;
                const string chars = "abcdefgh12345678";
                const int request = 64, size = 64;
                font.RequestCharactersInTexture(chars, request, FontStyle.Normal);
                if (!(font.material != null && font.material.mainTexture is Texture atlas)) return null;
                var rt = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Graphics.Blit(atlas, rt);
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var copy = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false, true);
                copy.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0);
                copy.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);

                var glyphs = new BoardGlyphs();
                Color hi = Hex(0xFFF0C2), mid = Hex(0xE9C57A), lo = Hex(0x8A6224);
                foreach (char c in chars)
                {
                    if (!font.GetCharacterInfo(c, out var info, request, FontStyle.Normal)) continue;
                    // The page draws a 46 px glyph centred in a 64 px tile.
                    float scale = 46f / request, gw = (info.maxX - info.minX) * scale, gh = (info.maxY - info.minY) * scale;
                    float x0 = (size - gw) / 2f, y0 = (size - gh) / 2f;
                    var px = new Color[size * size];
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float u = (x + .5f - x0) / gw, v = (y + .5f - y0) / gh;   // v = 0 at the glyph's bottom
                            float a = 0;
                            if (u >= 0 && u <= 1 && v >= 0 && v <= 1)
                            {
                                // Bilinear over the glyph's four atlas corners (they may be flipped).
                                var bottom = Vector2.Lerp(info.uvBottomLeft, info.uvBottomRight, u);
                                var topUv = Vector2.Lerp(info.uvTopLeft, info.uvTopRight, u);
                                var uv = Vector2.Lerp(bottom, topUv, v);
                                a = copy.GetPixelBilinear(uv.x, uv.y).a;
                            }
                            float t = 1f - (y + .5f) / size;   // 0 at the tile's top
                            float g = Mathf.Clamp01((t * 64f - 10f) / 44f);
                            var col = g < .5f ? Color.Lerp(hi, mid, g * 2f) : Color.Lerp(mid, lo, g * 2f - 1f);
                            col.a = a;
                            px[y * size + x] = col;
                        }
                    var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Result letter " + c, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
                    texture.SetPixels(px);
                    texture.Apply(true);
                    glyphs.materials[c] = LastSceneArt.Blended("result letter " + c, texture, Color.white);
                }
                Object.Destroy(copy);
                return glyphs;
            }
        }
    }
}
