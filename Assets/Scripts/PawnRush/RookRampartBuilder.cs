using System;
using ChessFight.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;
using Look = ChessFight.PawnRush.RoyalClockTowerLook;
using T = ChessFight.PawnRush.RookRampartTimes;

namespace ChessFight.PawnRush
{
    // The rooks' rampart walk (룩 성벽길, R843 design v0.1, R859 2D explainer, R860 grey box, R866 1.5 times as big), early
    // obstacle section 1-5: the royal castle's outer defences on a rock spur between two gorges, three curtain walls one
    // above the other, climbed in switchbacks on the garrison's siege drill day (the pawns storm, the rooks defend). Design
    // and the numbers' reasons: Docs/PawnRush/PAWN_RUSH_ROOK_RAMPART_DESIGN_v0.1.md; ArtSource/PawnRush/RookRampart/
    // rampart_layout.py worked them out.
    //
    //   the glacis (0) -> 1 the siege ladders up the outer wall (6) from the attackers' mound -> east along its walk -> 2 the
    //   murder holes over the outer gate -> 3 a siege tower north across the lower ward onto the middle wall (14) -> west
    //   across 4 the rooks' drill square on the great tower's roof -> 5 the rook or the knight crane north over the ditch onto
    //   the inner wall (22) -> east to the inner gatehouse -> 6 up the king's banner to its roof (30) -> north over the keep
    //   bridge (the exit).
    //
    // Section frame: x east, y up, z north, metres; entry (0, 0, 0) on the glacis, exit (0, GateY, KeepZ) at the keep's door.
    // Every mover is a pure function of the obstacle clock (RookRampartMovers): nothing is sent.
    //
    // R860 grey-box changes to the design: the great tower's roof level with the middle wall's walk; the banner's ledge
    // 1.2 m deep with the lower length hung from its front edge (the climb's top-out lands 0.63 m in from a face's top);
    // the banner clear of the gatehouse's tower. R866 (the user: "전체적으로 사이즈가 작은 것 같아. 왕국 성벽이면 성벽의 높이도
    // 그렇고 성벽 폭도 더 넓어야 해. 길이가 길어져도 좋으니 전체적으로 사이즈를 좀 더 키워줘"): the plan 1.5 times as big
    // (K), the walls 10.5 m thick with 1.5 m parapets, the walks at 6 / 14 / 22 m and the gatehouse's roof at 30 (each
    // climb kept within a rook's stamina: 8 s at 0.84 m/s is 6.7 m) - the ladders stand on the attackers' earth mound
    // (1.5 m) so they climb 5.6 m, the siege towers have four floors and three inner ladders, the banner's lengths are 4 m.
    public static partial class RookRampartBuilder
    {
        public const float K = 1.5f;   // the plan's scale over v0.1's numbers

        // ------------------------------------------------------------------ the levels and the walls
        public const float W1 = 6f, W2 = 14f, W3 = 22f, GateY = 30f, BoardY = W2, DitchY = -6f, BaileyY = 14f, SpurX = 46f * K;
        public const float Parapet = 1.5f, MerlonH = 2.4f, SillH = 1.1f, MerlonL = 2.4f, Pitch = 4f, WallT = 10.5f;

        static Vector2 P(float x, float z) => new Vector2(x * K, z * K);
        // Each wall by its outer face, west to east, as (x, z); it is WallT thick, inward (north) of that line.
        public static readonly Vector2[] Wall1 = { P(-44f, 25f), P(-24f, 11f), P(24f, 11f), P(44f, 25f) };
        public static readonly Vector2[] Wall2W = { P(-44f, 51f), P(-11.6f, 46f) };
        public static readonly Vector2[] Wall2E = { P(11.6f, 38f), P(44f, 47f) };
        public static readonly Vector2[] Wall3W = { P(-42f, 64f), P(-10f, 70f) };
        public static readonly Vector2[] Wall3E = { P(10f, 70f), P(42f, 64f) };
        // Round towers (rooks, flat crenellated tops): x, z, radius, top.
        static readonly Vector4[] Towers =
        {
            new Vector4(-44f * K, 28f * K, 5f * K, W1 + 6f), new Vector4(-24f * K, 14.5f * K, 4.2f * K, W1 + 5f),
            new Vector4(24f * K, 14.5f * K, 4.2f * K, W1 + 5f), new Vector4(44f * K, 28f * K, 5f * K, W1 + 6f),
            new Vector4(-44f * K, 54f * K, 5f * K, W2 + 6f), new Vector4(44f * K, 50f * K, 5f * K, W2 + 6f),
            new Vector4(-42f * K, 67f * K, 5f * K, W3 + 6f), new Vector4(42f * K, 67f * K, 5f * K, W3 + 6f),
        };

        static float OnLine(Vector2 p, Vector2 q, float x) => Mathf.Lerp(p.y, q.y, (x - p.x) / (q.x - p.x));
        public static float Wall2Face(float x) => x > 0f ? OnLine(Wall2E[0], Wall2E[1], x) : OnLine(Wall2W[0], Wall2W[1], x);
        public static float Wall2Inner(float x) => Wall2Face(x) + WallT;
        public static float Wall3Face(float x) => x < 0f ? OnLine(Wall3W[0], Wall3W[1], x) : OnLine(Wall3E[0], Wall3E[1], x);

        // ------------------------------------------------------------------ 1. the siege ladders
        // The ladders stand on the attackers' earth mound in front of the wall's west half (Mound high, MoundDepth deep, a
        // ramp up from the glacis): 5.6 m to the crenel's sill.
        public const float Face1 = 11f * K, Mound = 1.5f, MoundDepth = 9f, MoundX0 = -30f, MoundX1 = -2f;
        public const float LadderClimb = W1 + SillH - Mound, LadderW = .6f, DefenderZ = Face1 + Parapet + 1.5f;
        public static readonly float LadderL = LadderClimb / Mathf.Sin(T.Lean * Mathf.Deg2Rad) + .6f;
        public static readonly float LadderFootZ = Face1 - LadderL * Mathf.Cos(T.Lean * Mathf.Deg2Rad) + .1f;

        // ------------------------------------------------------------------ 2. the murder holes over the outer gate
        public const float GateX0 = 0f, GateX1 = 10f * K, Hole = 3f, HolesZ0 = Face1 + Parapet;
        public const int HolesX = 5, HolesZ = 3;

        // ------------------------------------------------------------------ 3. the siege towers
        // Docked at the middle wall, tower A stands just east of the great tower (x 17.4) and B 7.5 m further.
        public static readonly float[] SiegeX = { 21f, 28.5f };
        public const float SiegeW = 6f, SiegeZ0 = Face1 + WallT + SiegeW * .5f, BridgeL = 3.6f;
        public const float FloorStep = (W2 + SillH - W1) / 3f;
        public static readonly float[] Floors = { W1, W1 + FloorStep, W1 + 2f * FloorStep, W2 + SillH };
        public static float SiegeZ1(int k) => Wall2Face(SiegeX[k]) - SiegeW * .5f;
        public static readonly float[] SiegeDelay = { 0f, T.SiegeCycle * .5f };

        // ------------------------------------------------------------------ 4. the drill square on the great tower
        public const int BoardN = 9;
        public const float Square = 2.4f * K, BoardX0 = -BoardN * Square * .5f, BoardZ0 = 36.2f * K;
        public const float TowerX0 = -11.6f * K, TowerX1 = 11.6f * K, TowerZ0 = 35.4f * K, TowerZ1 = 58.6f * K;
        public const float EntryZ = BoardZ0 + 2.5f * Square, ExitZ = BoardZ0 + 5.5f * Square;
        public static Vector2 SquareMid(float c, float r) => new Vector2(BoardX0 + (c + .5f) * Square, BoardZ0 + (r + .5f) * Square);
        // Along a file (north-south at a column) or a rank (east-west at a row), its index, its delay.
        public static readonly bool[] DrillFile = { true, false, true, false };
        public static readonly int[] DrillIndex = { 2, 3, 6, 6 };
        public static readonly float RookSlide = (BoardN - 1) * Square / T.RookSpeed;
        public static readonly float RookCycle = 2f * (T.RookRest + T.RookGlow + RookSlide);
        public static readonly float[] DrillDelay = { 0f, RookCycle * .25f, RookCycle * .5f, RookCycle * .75f };

        // ------------------------------------------------------------------ 5. the cranes over the ditch
        public const float RookCraneX = -30f * K, KnightCraneX = -24.5f * K, CrownR = 2.5f * K, RookBody = 7f, BeamY = W3 + 8.5f;
        public static readonly float RcLowZ = Wall2Inner(RookCraneX) + CrownR + .15f, RcHighZ = Wall3Face(RookCraneX) - CrownR - .15f;
        public const float KnightH = 6f, KnightR = 1.6f, KnightLowY = W2 - 2.4f;
        public static readonly float KnLowZ = Wall2Inner(KnightCraneX) + 2.4f, KnHighZ = Wall3Face(KnightCraneX) + Parapet + 2f;
        public const float DoorX0 = -33.25f * K, DoorX1 = -21.25f * K;   // the inner wall's hoist door: no parapet

        // ------------------------------------------------------------------ 6. the inner gatehouse and the king's banner
        public const float GateHX0 = -10f * K, GateHX1 = 10f * K, GateHZ0 = 70f * K, GateHZ1 = 82f * K, GateTowerX = 6.5f * K, GateTowerR = 4f * K;
        public const float BannerZ0 = 72.2f * K, BannerW = 3.6f * K, BannerZ1 = BannerZ0 + BannerW;
        public const float LedgeY = (W3 + GateY) * .5f, LedgeDepth = 1.2f, LedgeZ0 = BannerZ0 - .3f, LedgeZ1 = GateHZ1 - .5f;
        public const float KeepZ = 98f * K, BridgeX = 3f;

        // Grey-box colours over the clock tower's (orange climb, slate no, violet moves, green rest, red cue).
        static Material Grass => Arena.Lit("M_RampartGrass", Arena.Hex("#9DBB72"), 0f, .9f);
        static Material Earth => Arena.Lit("M_RampartEarth", Arena.Hex("#A88C66"), 0f, .95f);
        static Material Rock => Arena.Lit("M_RampartRock", Arena.Hex("#8F8170"), 0f, .9f);
        static Material Wood => Arena.Lit("M_RampartWood", Arena.Hex("#9B6B3E"), 0f, .8f);
        static Material Hide => Arena.Lit("M_RampartHide", Arena.Hex("#7A5232"), 0f, .85f);
        static Material Cloth => Arena.Lit("M_RampartCloth", Arena.Hex("#F2EDE0"), 0f, .85f);
        static Material Gold => Arena.Lit("M_RampartGold", Arena.Hex("#C9972E"), .1f, .45f);
        static Material Figure => Arena.Lit("M_RampartFigure", Arena.Hex("#C9A77A"), 0f, .85f);
        static Material Defender => Arena.Lit("M_RampartDefender", Arena.Hex("#5D7192"), 0f, .6f);
        static Material KnightWood => Arena.Lit("M_RampartKnight", Arena.Hex("#8A5A36"), 0f, .6f);
        static Material Light => Arena.Lit("M_RampartLightSquare", Arena.Hex("#F2E8D2"), 0f, .8f);
        static Material Dark => Arena.Lit("M_RampartDarkSquare", Arena.Hex("#7D6649"), 0f, .8f);
        static Material LineRest => Arena.Lit("M_RampartLineRest", Arena.Hex("#6F8FC9"), 0f, .7f);
        static Material LineGlow => Arena.Lit("M_RampartLineGlow", Arena.Hex("#FFC400"), 1.4f, .4f);
        static Material LineSlide => Arena.Lit("M_RampartLineSlide", Arena.Hex("#F0503C"), 1.2f, .4f);
        static Material Rattle => Arena.Lit("M_RampartRattle", Arena.Hex("#FF8A1E"), 1.6f, .4f);
        static Material Pennant => Arena.Lit("M_RampartPennant", Arena.Hex("#C8472F"), 0f, .8f);
        static Material Carpet => Arena.Lit("M_RampartCarpet", Arena.Hex("#B3263A"), 0f, .9f);
        static Material Arrow => Arena.Lit("M_RampartArrow", Arena.Hex("#FFD45A"), .7f, .5f);
        static Material Mark => Arena.Lit("M_RampartMark", Arena.Hex("#E03131"), .4f, .6f);
        static Material Iron => YardKit.Iron;

        public static void Build(RookRampartSection rampart)
        {
            var root = rampart.transform;
            var old = root.Find(RookRampartSection.ContentName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var content = new GameObject(RookRampartSection.ContentName);
            content.SetActive(false);
            content.transform.SetParent(root, false);
            var b = new CourseBuilder(Look.Kit, content.transform);

            Ground(b);
            OuterWall(b);
            MiddleWall(b);
            GreatTower(b);
            InnerWall(b);
            Gatehouse(b);
            KeepBridge(b);
            RoundTowers(b);
            Ladders(b);
            MurderHoles(b);
            SiegeTowers(b);
            DrillSquare(b);
            Cranes(b);
            Banner(b);
            TheWay(b);
            Course(b);

            rampart.GetComponent<PawnRushSection>().Describe(RookRampartSection.Id, RookRampartSection.DisplayName,
                PawnRushSection.SlotKind.Obstacle, RookRampartSection.Entry, RookRampartSection.Exit, 0f, Beats());
            content.SetActive(true);   // the movers wake here with their pose on the clock
        }

        // ------------------------------------------------------------------ the ground

        static void Ground(CourseBuilder b)
        {
            // The glacis and the lower ward, up to the great tower's south face; south of the middle wall's slanted halves,
            // a strip under each up to its face. The ditch's floor and the inner bailey lower and higher.
            float z0 = -12f * K;
            YardKit.Box(b.parent, "RR glacis and lower ward", V(0f, -.5f, (z0 + TowerZ0) * .5f), V(2f * SpurX, 1f, TowerZ0 - z0), Grass)
                   .AddComponent<CourseFloor>();
            foreach (var seg in new[] { Wall2W, Wall2E })
                Along(b, "RR lower ward by the middle wall", seg[0], seg[1], -21f, 0f, -1f, 0f, Grass, YardKit.Solid.Solid).AddComponent<CourseFloor>();
            YardKit.Box(b.parent, "RR ditch floor", V(0f, DitchY - .5f, 58f * K), V(2f * SpurX, 1f, 28f * K), Rock);
            YardKit.Box(b.parent, "RR inner bailey", V(0f, BaileyY - .5f, (70f * K + 102f * K) * .5f), V(2f * SpurX, 1f, 32f * K), Grass);
            // The spur's cliffs over the gorges (pictures down to the river).
            foreach (float s in new[] { -1f, 1f })
                YardKit.Box(b.parent, "RR cliff", V(s * (SpurX + .75f), -18f, 45f * K), V(1.5f, 36f, 118f * K), Rock, YardKit.Solid.None);
        }

        // ------------------------------------------------------------------ the curtain walls

        static void OuterWall(CourseBuilder b)
        {
            // The middle stretch is split at the outer gate: over it the walk is the murder holes (MurderHoles). Merlons every
            // 4 m centred on x = 4k, so the crenels - and the ladders - are at x = 4k + 2.
            Curtain(b, "RR outer wall W wing", Wall1[0], Wall1[1], -1f, W1, 2f, null);
            Curtain(b, "RR outer wall W", Wall1[1], new Vector2(GateX0, Face1), -1f, W1, 0f, null);
            Curtain(b, "RR outer wall E", new Vector2(GateX1, Face1), Wall1[2], -1f, W1, 1f, null);
            Curtain(b, "RR outer wall E wing", Wall1[2], Wall1[3], -1f, W1, 2f, null);
            // The outer gate: its block in front of the wall, the portcullis down.
            b.Box("RR outer gate", V(GateX0, 0f, 3f * K), V(GateX1, W1 + MerlonH, Face1), Look.NoClimb, true);
            for (float x = GateX0 + 2.5f; x < GateX1 - 2f; x += 1.1f)
                YardKit.Box(b.parent, "RR portcullis bar", V(x, 2.4f, 3f * K - .05f), V(.16f, 4.8f, .16f), Iron, YardKit.Solid.None);
        }

        static void MiddleWall(CourseBuilder b)
        {
            // No merlons over the siege towers' front, from the great tower to past tower B: the bridges come down on the sill.
            Func<Vector2, bool> bridges = c => c.x < SiegeX[1] + 2.4f;
            Curtain(b, "RR middle wall W", Wall2W[0], Wall2W[1], DitchY, W2, 2f, null);
            Curtain(b, "RR middle wall E", Wall2E[0], Wall2E[1], DitchY, W2, 2f, bridges);
        }

        static void InnerWall(CourseBuilder b)
        {
            // The hoist door: 18 m of the west half with no parapet, where the cranes set their loads down. Each half runs on
            // 2.5 m into the gatehouse (R866): the wall is slanted and 10.5 m thick, so its square end stopped at the
            // gatehouse's corner left a crack up to 1.9 m wide along the gatehouse's face, under the banner.
            Curtain(b, "RR inner wall W", Wall3W[0], Past(Wall3W[0], Wall3W[1], 2.5f), DitchY, W3, 2f,
                    c => c.x > DoorX0 - 1.2f && c.x < DoorX1 + 1.2f, DoorX0, DoorX1);
            Curtain(b, "RR inner wall E", Past(Wall3E[1], Wall3E[0], 2.5f), Wall3E[1], DitchY, W3, 2f + 2.5f, null);
        }

        // `q` moved on `metres` along p -> q.
        static Vector2 Past(Vector2 p, Vector2 q, float metres) => q + (q - p).normalized * metres;

        // A curtain wall along p -> q (its outer face; the wall WallT inward of it, to the left of p -> q) from `bottom` to its
        // walk; on the face a parapet: a sill all along (none between x `noSill0` and `noSill1`) and merlons every Pitch metres
        // from `first` metres along, none where `open` says (given a merlon's middle).
        static void Curtain(CourseBuilder b, string name, Vector2 p, Vector2 q, float bottom, float walk, float first,
                            Func<Vector2, bool> open, float noSill0 = float.NaN, float noSill1 = float.NaN)
        {
            Vector2 d = q - p;
            float len = d.magnitude;
            Vector2 dir = d / len, n = new Vector2(-dir.y, dir.x);
            var turn = Quaternion.LookRotation(new Vector3(n.x, 0f, n.y), Vector3.up);
            Vector3 At(float s, float inward, float y) { var pt = p + dir * s + n * inward; return V(pt.x, y, pt.y); }

            var mass = YardKit.Box(b.parent, name, At(len * .5f, WallT * .5f, (bottom + walk) * .5f), V(len, walk - bottom, WallT),
                                   Look.NoClimb, YardKit.Solid.NoClimb, turn);
            mass.AddComponent<CourseFloor>();
            YardKit.Box(b.parent, name + " walk", At(len * .5f, (Parapet + WallT) * .5f, walk + .01f), V(len, .02f, WallT - Parapet),
                        Look.Floor, YardKit.Solid.None, turn);
            // The sill, in one or two runs.
            float a = len, c = len;
            if (!float.IsNaN(noSill0))
            {
                a = Mathf.Clamp((noSill0 - p.x) / dir.x, 0f, len);
                c = Mathf.Clamp((noSill1 - p.x) / dir.x, 0f, len);
                if (a > c) { float t = a; a = c; c = t; }
            }
            void Sill(float s0, float s1)
            {
                if (s1 - s0 < .05f) return;
                YardKit.Box(b.parent, name + " sill", At((s0 + s1) * .5f, Parapet * .5f, walk + SillH * .5f), V(s1 - s0, SillH, Parapet),
                            Look.NoClimb, YardKit.Solid.NoClimb, turn);
            }
            Sill(0f, a);
            if (c < len) Sill(c, len);
            for (float s = first; s <= len - MerlonL * .5f + .01f; s += Pitch)
            {
                var mid = p + dir * s;
                if (open != null && open(mid)) continue;
                if (s < a || s > c)
                    YardKit.Box(b.parent, name + " merlon", At(s, Parapet * .5f, walk + (SillH + MerlonH) * .5f), V(MerlonL, MerlonH - SillH, Parapet),
                                Look.NoClimb, YardKit.Solid.NoClimb, turn);
            }
        }

        // A box laid along p -> q (x along it), between `in0` and `in1` metres inward of the line, `y0` .. `y1`.
        static GameObject Along(CourseBuilder b, string name, Vector2 p, Vector2 q, float in0, float in1, float y0, float y1,
                                Material material, YardKit.Solid solid)
        {
            Vector2 d = q - p;
            float len = d.magnitude;
            Vector2 dir = d / len, n = new Vector2(-dir.y, dir.x), mid = (p + q) * .5f + n * ((in0 + in1) * .5f);
            var turn = Quaternion.LookRotation(new Vector3(n.x, 0f, n.y), Vector3.up);
            return YardKit.Box(b.parent, name, V(mid.x, (y0 + y1) * .5f, mid.y), V(len, y1 - y0, in1 - in0), material, solid, turn);
        }

        static void RoundTowers(CourseBuilder b)
        {
            foreach (var t in Towers)
            {
                float bottom = t.y > 45f * K ? DitchY : -1f;
                YardKit.Cylinder(b.parent, "RR round tower", V(t.x, (bottom + t.w) * .5f, t.y), Vector3.up, 2f * t.z, t.w - bottom, Look.NoClimb, YardKit.Solid.NoClimb);
                Crown(b.parent, "RR round tower", V(t.x, t.w, t.y), t.z, 10);
            }
        }

        // A ring of merlons round a flat top (pictures).
        static void Crown(Transform parent, string name, Vector3 top, float radius, int count)
        {
            for (int k = 0; k < count; k++)
            {
                var turn = Quaternion.Euler(0f, k * 360f / count, 0f);
                YardKit.Box(parent, name + " merlon", top + turn * V(0f, .6f, radius - .5f), V(radius * 2f * Mathf.Sin(Mathf.PI / count) * .55f, 1.2f, 1f),
                            Look.NoClimb, YardKit.Solid.None, turn);
            }
        }

        // ------------------------------------------------------------------ the great tower, the inner gatehouse, the keep

        static void GreatTower(CourseBuilder b)
        {
            // Its roof is the drill square, level with the middle wall's walk; a parapet ring round it, open where the way
            // comes on (east, rank 2) and goes off (west, rank 5).
            b.Box("RR great tower", V(TowerX0, DitchY, TowerZ0), V(TowerX1, BoardY, TowerZ1), Look.NoClimb, true).AddComponent<CourseFloor>();
            float rim = BoardX0 - TowerX0, gap = Square * .5f;
            void Rail(string name, float x0, float x1, float z0, float z1) =>
                b.Box(name, V(x0, BoardY, z0), V(x1, BoardY + 1.4f, z1), Look.NoClimb, true);
            Rail("RR great tower parapet S", TowerX0, TowerX1, TowerZ0, TowerZ0 + rim);
            Rail("RR great tower parapet N", TowerX0, TowerX1, TowerZ1 - rim, TowerZ1);
            Rail("RR great tower parapet E1", TowerX1 - rim, TowerX1, TowerZ0 + rim, EntryZ - gap);
            Rail("RR great tower parapet E2", TowerX1 - rim, TowerX1, EntryZ + gap, TowerZ1 - rim);
            Rail("RR great tower parapet W1", TowerX0, TowerX0 + rim, TowerZ0 + rim, ExitZ - gap);
            Rail("RR great tower parapet W2", TowerX0, TowerX0 + rim, ExitZ + gap, TowerZ1 - rim);
            for (int c = 0; c < BoardN; c++)
                for (int r = 0; r < BoardN; r++)
                    YardKit.Box(b.parent, "RR board square", V(BoardX0 + (c + .5f) * Square, BoardY + .006f, BoardZ0 + (r + .5f) * Square),
                                V(Square, .012f, Square), (c + r) % 2 == 0 ? Dark : Light, YardKit.Solid.None);
        }

        static void Gatehouse(CourseBuilder b)
        {
            b.Box("RR inner gatehouse", V(GateHX0, DitchY, GateHZ0), V(GateHX1, GateY, GateHZ1), Look.NoClimb, true).AddComponent<CourseFloor>();
            YardKit.Box(b.parent, "RR inner gatehouse roof", V(0f, GateY + .01f, (GateHZ0 + GateHZ1) * .5f), V(GateHX1 - GateHX0, .02f, GateHZ1 - GateHZ0),
                        Look.Floor, YardKit.Solid.None);
            // The roof's parapet: merlons on a sill, none over the banner (the climb comes over there) or at the bridge.
            void Edge(string name, Vector2 p, Vector2 q, Func<Vector2, bool> open)
            {
                Vector2 d = q - p;
                float len = d.magnitude;
                Vector2 dir = d / len, n = new Vector2(-dir.y, dir.x);
                var turn = Quaternion.LookRotation(new Vector3(n.x, 0f, n.y), Vector3.up);
                for (float s = .5f; s < len - .4f; s += .5f)
                {
                    var mid = p + dir * (s + .25f) + n * (Parapet * .5f);
                    if (open(mid)) continue;
                    bool merlon = Mathf.Repeat(s, Pitch) < MerlonL - .01f;
                    YardKit.Box(b.parent, name, V(mid.x, GateY + (merlon ? MerlonH : SillH) * .5f, mid.y), V(.5f, merlon ? MerlonH : SillH, Parapet),
                                Look.NoClimb, YardKit.Solid.NoClimb, turn);
                }
            }
            Func<Vector2, bool> none = c => false;
            Edge("RR gatehouse parapet W", new Vector2(GateHX0, GateHZ1), new Vector2(GateHX0, GateHZ0), c => c.y > BannerZ0 - .4f && c.y < BannerZ1 + .4f);
            Edge("RR gatehouse parapet S", new Vector2(GateHX0, GateHZ0), new Vector2(GateHX1, GateHZ0), none);
            Edge("RR gatehouse parapet E", new Vector2(GateHX1, GateHZ0), new Vector2(GateHX1, GateHZ1), none);
            Edge("RR gatehouse parapet N", new Vector2(GateHX1, GateHZ1), new Vector2(GateHX0, GateHZ1), c => Mathf.Abs(c.x) < BridgeX + .4f);
            // Its two round towers on the south face.
            foreach (float s in new[] { -1f, 1f })
            {
                YardKit.Cylinder(b.parent, "RR gatehouse tower", V(s * GateTowerX, (DitchY + GateY + 3.5f) * .5f, GateHZ0), Vector3.up, 2f * GateTowerR,
                                 GateY + 3.5f - DitchY, Look.NoClimb, YardKit.Solid.NoClimb);
                Crown(b.parent, "RR gatehouse tower", V(s * GateTowerX, GateY + 3.5f, GateHZ0), GateTowerR, 10);
                YardKit.Cone(b.parent, "RR gatehouse tower roof", V(s * GateTowerX, GateY + 4.7f, GateHZ0), 2f * GateTowerR + .6f, 6f, YardKit.Roof);
            }
        }

        static void KeepBridge(CourseBuilder b)
        {
            b.Box("RR keep bridge", V(-BridgeX, GateY - 1.5f, GateHZ1), V(BridgeX, GateY, KeepZ), Look.Floor, false).AddComponent<CourseFloor>();
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box("RR keep bridge rail", V(s * BridgeX - (s > 0f ? 0f : .3f), GateY, GateHZ1), V(s * BridgeX + (s > 0f ? .3f : 0f), GateY + 1.2f, KeepZ),
                      Look.NoClimb, true);
                for (float z = GateHZ1 + 6f; z < KeepZ - 3f; z += 9f)
                    YardKit.Box(b.parent, "RR keep bridge pier", V(s * (BridgeX - .9f), (BaileyY + GateY - 1.5f) * .5f, z), V(1.8f, GateY - 1.5f - BaileyY, 2.4f),
                                Look.NoClimb, YardKit.Solid.None);
            }
            // The keep's front with its door, the exit.
            b.Box("RR keep W", V(-21f, BaileyY, KeepZ), V(-BridgeX, GateY + 14f, KeepZ + 3f), Look.NoClimb, true);
            b.Box("RR keep E", V(BridgeX, BaileyY, KeepZ), V(21f, GateY + 14f, KeepZ + 3f), Look.NoClimb, true);
            b.Box("RR keep door lintel", V(-BridgeX, GateY + 5f, KeepZ), V(BridgeX, GateY + 14f, KeepZ + 3f), Look.NoClimb, true);
            b.Box("RR keep under the door", V(-BridgeX, BaileyY, KeepZ), V(BridgeX, GateY - 1.5f, KeepZ + 3f), Look.NoClimb, true);
        }

        // ------------------------------------------------------------------ the way (as the banquet hall's, R817)

        static void TheWay(CourseBuilder b)
        {
            var at = b.Group("RR the way");
            void Lay(float y, params Vector2[] points)
            {
                for (int i = 1; i < points.Length; i++)
                {
                    Vector3 from = V(points[i - 1].x, y, points[i - 1].y), to = V(points[i].x, y, points[i].y), d = to - from;
                    float length = d.magnitude;
                    if (length < .05f) continue;
                    var turn = Quaternion.LookRotation(d / length, Vector3.up);
                    YardKit.Box(at, "RR carpet", (from + to) * .5f + V(0f, .02f, 0f), V(2f, .02f, length + 2f), Carpet, YardKit.Solid.None, turn);
                    for (float s = 2f; s < length - .5f; s += 4f)
                        Chevron(at, from + d / length * s + V(0f, .04f, 0f), turn);
                }
            }
            Vector2 Q(float x, float z) => new Vector2(x, z);
            float mz = Face1 - MoundDepth;
            Lay(0f, Q(0f, 1.5f), Q(-8f, mz - 6f), Q(-16f, mz - 5.5f));
            Lay(Mound, Q(-16f, mz + .8f), Q(-16f, LadderFootZ - 1.6f));
            Lay(W1, Q(-16f, Face1 + 4f), Q(GateX0 - 1f, Face1 + 6f));
            Lay(W1, Q(GateX1 + 1f, Face1 + 6f), Q(SiegeX[0] + 2.5f, Face1 + 6f), Q(SiegeX[0] + 2.5f, Face1 + WallT - .8f));
            Lay(W2, Q(SiegeX[0] + 2.5f, Wall2Face(SiegeX[0]) + 6f), Q(TowerX1 + .3f, EntryZ));
            Lay(W2, Q(TowerX0 - .3f, ExitZ), Q(RookCraneX + 5f, Wall2Inner(RookCraneX) - 3.5f));
            Lay(W3, Q(RookCraneX + 1.5f, Wall3Face(RookCraneX) + 6f), Q(GateHX0 - 3.5f, (BannerZ0 + BannerZ1) * .5f));
            Lay(GateY, Q(GateHX0 + 2f, (BannerZ0 + BannerZ1) * .5f), Q(0f, GateHZ1 - 6f), Q(0f, KeepZ - .5f));
        }

        // An arrow ">" pointing along `turn`: two gold bars.
        static void Chevron(Transform parent, Vector3 at, Quaternion turn)
        {
            foreach (float s in new[] { -1f, 1f })
            {
                var arm = turn * Quaternion.Euler(0f, s * 40f, 0f);
                YardKit.Box(parent, "RR arrow", at - arm * V(0f, 0f, .45f), V(.2f, .02f, 1f), Arrow, YardKit.Solid.None, arm);
            }
        }

        // ------------------------------------------------------------------ the vocabulary

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static Transform Root(CourseBuilder b, string name, Vector3 at)
        {
            var t = b.Group(name);
            t.localPosition = at;
            return t;
        }

        // A standing figure (a worker, the attackers' crew): a body and a head, pictures.
        static Transform Person(Transform parent, string name, Vector3 feet, Material material)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = feet;
            YardKit.Cylinder(t, name + " body", V(0f, .45f, 0f), Vector3.up, .5f, .9f, material, YardKit.Solid.None);
            YardKit.Ball(t, name + " head", V(0f, 1.08f, 0f), .38f, material, YardKit.Solid.None);
            return t;
        }
    }
}
