using System;
using ChessFight.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;
using Look = ChessFight.PawnRush.RoyalClockTowerLook;
using T = ChessFight.PawnRush.RookRampartTimes;

namespace ChessFight.PawnRush
{
    // The rooks' rampart walk (룩 성벽길, R843 design v0.1, R859 2D explainer, R860 grey box), early obstacle section 1-5: the
    // royal castle's outer defences on a rock spur between two gorges, three curtain walls one above the other, climbed in
    // switchbacks on the garrison's siege drill day (the pawns storm, the rooks defend). Design and the numbers' reasons:
    // Docs/PawnRush/PAWN_RUSH_ROOK_RAMPART_DESIGN_v0.1.md; ArtSource/PawnRush/RookRampart/rampart_layout.py worked them out.
    //
    //   the glacis (0) -> 1 the siege ladders up the outer wall (3.5) -> east along its walk -> 2 the murder holes over the
    //   outer gate -> 3 a siege tower north across the lower ward onto the middle wall (9) -> west across 4 the rooks' drill
    //   square on the great tower's roof -> 5 the rook or the knight crane north over the ditch onto the inner wall (14.5) ->
    //   east to the inner gatehouse -> 6 up the king's banner to its roof (20) -> north over the keep bridge (the exit).
    //
    // Section frame: x east, y up, z north, metres; entry (0, 0, 0) on the glacis, exit (0, GateY, KeepZ) at the keep's door.
    // Every mover is a pure function of the obstacle clock (RookRampartMovers): nothing is sent.
    //
    // R860 grey-box changes to the design (in the design doc's '회색 상자' part): the great tower's roof is level with the
    // middle wall's walk (a ramp up 2 m had nowhere to go but where the siege towers' bridges land); the banner's ledge is
    // 1.2 m deep and the lower length hangs from its front edge (the climb's top-out lands 0.75 m in from a face: with the
    // ledge 0.6 m deep and the cloth on the wall it landed inside the gatehouse); the banner is 0.6 m further north, clear of
    // the gatehouse's south-west tower.
    public static partial class RookRampartBuilder
    {
        // ------------------------------------------------------------------ the levels and the walls
        public const float W1 = 3.5f, W2 = 9f, W3 = 14.5f, GateY = 20f, BoardY = W2, DitchY = -6f, BaileyY = 9f, SpurX = 46f;
        public const float Parapet = 1f, MerlonH = 1.8f, SillH = .9f, MerlonL = 1.8f, Pitch = 3f, WallT = 7f;

        // Each wall by its outer face, west to east, as (x, z); it is WallT thick, inward (north) of that line.
        public static readonly Vector2[] Wall1 = { new Vector2(-44f, 25f), new Vector2(-24f, 11f), new Vector2(24f, 11f), new Vector2(44f, 25f) };
        public static readonly Vector2[] Wall2W = { new Vector2(-44f, 51f), new Vector2(-11.6f, 46f) };
        public static readonly Vector2[] Wall2E = { new Vector2(11.6f, 38f), new Vector2(44f, 47f) };
        public static readonly Vector2[] Wall3W = { new Vector2(-42f, 64f), new Vector2(-10f, 70f) };
        public static readonly Vector2[] Wall3E = { new Vector2(10f, 70f), new Vector2(42f, 64f) };
        // Round towers (rooks, flat crenellated tops): x, z, radius, top.
        static readonly Vector4[] Towers =
        {
            new Vector4(-44f, 28f, 5f, 9f), new Vector4(-24f, 14.5f, 4.2f, 8f), new Vector4(24f, 14.5f, 4.2f, 8f), new Vector4(44f, 28f, 5f, 9f),
            new Vector4(-44f, 54f, 5f, 14.5f), new Vector4(44f, 50f, 5f, 14.5f), new Vector4(-42f, 67f, 5f, 19.5f), new Vector4(42f, 67f, 5f, 19.5f),
        };

        static float OnLine(Vector2 p, Vector2 q, float x) => Mathf.Lerp(p.y, q.y, (x - p.x) / (q.x - p.x));
        public static float Wall2Face(float x) => x > 0f ? OnLine(Wall2E[0], Wall2E[1], x) : OnLine(Wall2W[0], Wall2W[1], x);
        public static float Wall2Inner(float x) => Wall2Face(x) + WallT;
        public static float Wall3Face(float x) => x < 0f ? OnLine(Wall3W[0], Wall3W[1], x) : OnLine(Wall3E[0], Wall3E[1], x);

        // ------------------------------------------------------------------ 1. the siege ladders
        public const float Face1 = 11f, LadderClimb = W1 + SillH, LadderW = .55f, DefenderZ = Face1 + Parapet + 1.2f;
        public static readonly float LadderL = LadderClimb / Mathf.Sin(T.Lean * Mathf.Deg2Rad) + .6f;
        public static readonly float LadderFootZ = Face1 - LadderL * Mathf.Cos(T.Lean * Mathf.Deg2Rad) + .1f;

        // ------------------------------------------------------------------ 2. the murder holes over the outer gate
        public const float GateX0 = 0f, GateX1 = 10f, Hole = 2f, HolesZ0 = Face1 + Parapet;
        public const int HolesX = 5, HolesZ = 3;

        // ------------------------------------------------------------------ 3. the siege towers
        public static readonly float[] SiegeX = { 13.5f, 18f };
        public static readonly float[] Floors = { W1, W1 + 3.2f, W1 + 6.4f };
        public const float SiegeW = 4f, SiegeZ0 = Face1 + WallT + SiegeW * .5f, BridgeL = 2.4f;
        public static float SiegeZ1(int k) => Wall2Face(SiegeX[k]) - SiegeW * .5f;
        public static readonly float[] SiegeDelay = { 0f, T.SiegeCycle * .5f };

        // ------------------------------------------------------------------ 4. the drill square on the great tower
        public const int BoardN = 9;
        public const float Square = 2.4f, BoardX0 = -BoardN * Square * .5f, BoardZ0 = 36.2f;
        public const float TowerX0 = -11.6f, TowerX1 = 11.6f, TowerZ0 = 35.4f, TowerZ1 = 58.6f;
        public const float EntryZ = BoardZ0 + 2.5f * Square, ExitZ = BoardZ0 + 5.5f * Square;
        public static Vector2 SquareMid(float c, float r) => new Vector2(BoardX0 + (c + .5f) * Square, BoardZ0 + (r + .5f) * Square);
        // Along a file (north-south at a column) or a rank (east-west at a row), its index, its delay.
        public static readonly bool[] DrillFile = { true, false, true, false };
        public static readonly int[] DrillIndex = { 2, 3, 6, 6 };
        public static readonly float[] DrillDelay = { 0f, 2.7f, 5.4f, 8.1f };
        public static readonly float RookSlide = (BoardN - 1) * Square / T.RookSpeed;

        // ------------------------------------------------------------------ 5. the cranes over the ditch
        public const float RookCraneX = -30f, KnightCraneX = -24.5f, CrownR = 2.5f, RookBody = 5f, BeamY = 23f;
        public static readonly float RcLowZ = Wall2Inner(RookCraneX) + CrownR + .15f, RcHighZ = Wall3Face(RookCraneX) - CrownR - .15f;
        public const float KnightH = 4.6f, KnightR = 1.2f, KnightLowY = W2 - 1.8f;
        public static readonly float KnLowZ = Wall2Inner(KnightCraneX) + 1.8f, KnHighZ = Wall3Face(KnightCraneX) + Parapet + 1.5f;
        public const float DoorX0 = -33.25f, DoorX1 = -21.25f;   // the inner wall's hoist door: no parapet

        // ------------------------------------------------------------------ 6. the inner gatehouse and the king's banner
        public const float GateHX0 = -10f, GateHX1 = 10f, GateHZ0 = 70f, GateHZ1 = 82f, GateTowerX = 6.5f, GateTowerR = 4f;
        public const float BannerZ0 = 72.2f, BannerZ1 = 75.8f, LedgeY = (W3 + GateY) * .5f, LedgeDepth = 1.2f, LedgeZ0 = 72f, LedgeZ1 = GateHZ1 - .5f;
        public const float KeepZ = 98f, BridgeX = 2f;

        // Grey-box colours over the clock tower's (orange climb, slate no, violet moves, green rest, red cue).
        static Material Grass => Arena.Lit("M_RampartGrass", Arena.Hex("#9DBB72"), 0f, .9f);
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
            var g = YardKit.Box(b.parent, "RR glacis and lower ward", V(0f, -.5f, (TowerZ0 - 14f) * .5f), V(2f * SpurX, 1f, TowerZ0 + 14f), Grass);
            g.AddComponent<CourseFloor>();
            foreach (var seg in new[] { Wall2W, Wall2E })
                Along(b, "RR lower ward by the middle wall", seg[0], seg[1], -14f, 0f, -1f, 0f, Grass, YardKit.Solid.Solid).AddComponent<CourseFloor>();
            YardKit.Box(b.parent, "RR ditch floor", V(0f, DitchY - .5f, 58f), V(2f * SpurX, 1f, 28f), Rock);
            YardKit.Box(b.parent, "RR inner bailey", V(0f, BaileyY - .5f, 86f), V(2f * SpurX, 1f, 32f), Grass);
            // The spur's cliffs over the gorges (pictures down to the river).
            foreach (float s in new[] { -1f, 1f })
                YardKit.Box(b.parent, "RR cliff", V(s * (SpurX + .5f), -14f, 45f), V(1f, 28f, 118f), Rock, YardKit.Solid.None);
        }

        // ------------------------------------------------------------------ the curtain walls

        static void OuterWall(CourseBuilder b)
        {
            // The middle stretch is split at the outer gate: over it the walk is the murder holes (MurderHoles).
            Curtain(b, "RR outer wall W wing", Wall1[0], Wall1[1], -1f, W1, 1.5f, null);
            Curtain(b, "RR outer wall W", Wall1[1], new Vector2(GateX0, Face1), -1f, W1, 1f, null);
            Curtain(b, "RR outer wall E", new Vector2(GateX1, Face1), Wall1[2], -1f, W1, 0f, null);
            Curtain(b, "RR outer wall E wing", Wall1[2], Wall1[3], -1f, W1, 1.5f, null);
            // The outer gate: its block in front of the wall, the portcullis down.
            b.Box("RR outer gate", V(GateX0, 0f, 3f), V(GateX1, W1 + MerlonH, Face1), Look.NoClimb, true);
            for (float x = GateX0 + 2f; x < GateX1 - 1.5f; x += .9f)
                YardKit.Box(b.parent, "RR portcullis bar", V(x, 1.6f, 2.95f), V(.12f, 3.2f, .12f), Iron, YardKit.Solid.None);
        }

        static void MiddleWall(CourseBuilder b)
        {
            // Crenels left open where the siege towers' bridges come down on the sill.
            Func<Vector2, bool> bridges = c => Mathf.Abs(c.x - SiegeX[0]) < 1.7f || Mathf.Abs(c.x - SiegeX[1]) < 1.7f;
            Curtain(b, "RR middle wall W", Wall2W[0], Wall2W[1], DitchY, W2, 1.5f, null);
            Curtain(b, "RR middle wall E", Wall2E[0], Wall2E[1], DitchY, W2, 1.5f, bridges);
        }

        static void InnerWall(CourseBuilder b)
        {
            // The hoist door: twelve metres of the west half with no parapet, where the cranes set their loads down.
            Curtain(b, "RR inner wall W", Wall3W[0], Wall3W[1], DitchY, W3, 1.5f, c => c.x > DoorX0 - .9f && c.x < DoorX1 + .9f, DoorX0, DoorX1);
            Curtain(b, "RR inner wall E", Wall3E[0], Wall3E[1], DitchY, W3, 1.5f, null);
        }

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
                float bottom = t.y > 45f ? DitchY : -1f;
                YardKit.Cylinder(b.parent, "RR round tower", V(t.x, (bottom + t.w) * .5f, t.y), Vector3.up, 2f * t.z, t.w - bottom, Look.NoClimb, YardKit.Solid.NoClimb);
                Crown(b.parent, "RR round tower", V(t.x, t.w, t.y), t.z, 8);
            }
        }

        // A ring of merlons round a flat top (pictures).
        static void Crown(Transform parent, string name, Vector3 top, float radius, int count)
        {
            for (int k = 0; k < count; k++)
            {
                var turn = Quaternion.Euler(0f, k * 360f / count, 0f);
                YardKit.Box(parent, name + " merlon", top + turn * V(0f, .45f, radius - .35f), V(radius * 2f * Mathf.Sin(Mathf.PI / count) * .55f, .9f, .7f),
                            Look.NoClimb, YardKit.Solid.None, turn);
            }
        }

        // ------------------------------------------------------------------ the great tower, the inner gatehouse, the keep

        static void GreatTower(CourseBuilder b)
        {
            // Its roof is the drill square, level with the middle wall's walk; a parapet ring round it, open where the way
            // comes on (east, rank 2) and goes off (west, rank 5).
            b.Box("RR great tower", V(TowerX0, DitchY, TowerZ0), V(TowerX1, BoardY, TowerZ1), Look.NoClimb, true).AddComponent<CourseFloor>();
            float rim = BoardX0 - TowerX0;   // 0.8
            void Rail(string name, float x0, float x1, float z0, float z1) =>
                b.Box(name, V(x0, BoardY, z0), V(x1, BoardY + 1.2f, z1), Look.NoClimb, true);
            Rail("RR great tower parapet S", TowerX0, TowerX1, TowerZ0, TowerZ0 + rim);
            Rail("RR great tower parapet N", TowerX0, TowerX1, TowerZ1 - rim, TowerZ1);
            Rail("RR great tower parapet E1", TowerX1 - rim, TowerX1, TowerZ0 + rim, EntryZ - 1.2f);
            Rail("RR great tower parapet E2", TowerX1 - rim, TowerX1, EntryZ + 1.2f, TowerZ1 - rim);
            Rail("RR great tower parapet W1", TowerX0, TowerX0 + rim, TowerZ0 + rim, ExitZ - 1.2f);
            Rail("RR great tower parapet W2", TowerX0, TowerX0 + rim, ExitZ + 1.2f, TowerZ1 - rim);
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
                    var mid = p + dir * (s + .25f) + n * .4f;
                    if (open(mid)) continue;
                    bool merlon = Mathf.Repeat(s, Pitch) < MerlonL - .01f;
                    YardKit.Box(b.parent, name, V(mid.x, GateY + (merlon ? MerlonH : SillH) * .5f, mid.y), V(.5f, merlon ? MerlonH : SillH, .8f),
                                Look.NoClimb, YardKit.Solid.NoClimb, turn);
                }
            }
            Func<Vector2, bool> none = c => false;
            Edge("RR gatehouse parapet W", new Vector2(GateHX0, GateHZ1), new Vector2(GateHX0, GateHZ0), c => c.y > BannerZ0 - .3f && c.y < BannerZ1 + .3f);
            Edge("RR gatehouse parapet S", new Vector2(GateHX0, GateHZ0), new Vector2(GateHX1, GateHZ0), none);
            Edge("RR gatehouse parapet E", new Vector2(GateHX1, GateHZ0), new Vector2(GateHX1, GateHZ1), none);
            Edge("RR gatehouse parapet N", new Vector2(GateHX1, GateHZ1), new Vector2(GateHX0, GateHZ1), c => Mathf.Abs(c.x) < BridgeX + .3f);
            // Its two round towers on the south face.
            foreach (float s in new[] { -1f, 1f })
            {
                YardKit.Cylinder(b.parent, "RR gatehouse tower", V(s * GateTowerX, (DitchY + GateY + 2.5f) * .5f, GateHZ0), Vector3.up, 2f * GateTowerR,
                                 GateY + 2.5f - DitchY, Look.NoClimb, YardKit.Solid.NoClimb);
                Crown(b.parent, "RR gatehouse tower", V(s * GateTowerX, GateY + 2.5f, GateHZ0), GateTowerR, 8);
                YardKit.Cone(b.parent, "RR gatehouse tower roof", V(s * GateTowerX, GateY + 3.4f, GateHZ0), 2f * GateTowerR + .4f, 4f, YardKit.Roof);
            }
        }

        static void KeepBridge(CourseBuilder b)
        {
            b.Box("RR keep bridge", V(-BridgeX, GateY - 1.2f, GateHZ1), V(BridgeX, GateY, KeepZ), Look.Floor, false).AddComponent<CourseFloor>();
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box("RR keep bridge rail", V(s * BridgeX - (s > 0f ? 0f : .25f), GateY, GateHZ1), V(s * BridgeX + (s > 0f ? .25f : 0f), GateY + 1.1f, KeepZ),
                      Look.NoClimb, true);
                for (float z = 86f; z < KeepZ - 2f; z += 6f)
                    YardKit.Box(b.parent, "RR keep bridge pier", V(s * (BridgeX - .6f), (BaileyY + GateY - 1.2f) * .5f, z), V(1.2f, GateY - 1.2f - BaileyY, 1.6f),
                                Look.NoClimb, YardKit.Solid.None);
            }
            // The keep's front with its door, the exit.
            b.Box("RR keep W", V(-14f, BaileyY, KeepZ), V(-BridgeX, GateY + 10f, KeepZ + 2f), Look.NoClimb, true);
            b.Box("RR keep E", V(BridgeX, BaileyY, KeepZ), V(14f, GateY + 10f, KeepZ + 2f), Look.NoClimb, true);
            b.Box("RR keep door lintel", V(-BridgeX, GateY + 4f, KeepZ), V(BridgeX, GateY + 10f, KeepZ + 2f), Look.NoClimb, true);
            b.Box("RR keep under the door", V(-BridgeX, BaileyY, KeepZ), V(BridgeX, GateY - 1.2f, KeepZ + 2f), Look.NoClimb, true);
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
                    YardKit.Box(at, "RR carpet", (from + to) * .5f + V(0f, .02f, 0f), V(1.6f, .02f, length + 1.6f), Carpet, YardKit.Solid.None, turn);
                    for (float s = 1.5f; s < length - .5f; s += 3f)
                        Chevron(at, from + d / length * s + V(0f, .04f, 0f), turn);
                }
            }
            Vector2 P(float x, float z) => new Vector2(x, z);
            Lay(0f, P(0f, 1.2f), P(-5f, 5f), P(-11f, 6.6f));
            Lay(W1, P(-11f, 15f), P(GateX0 - .8f, 15f));
            Lay(W1, P(GateX1 + .8f, 15f), P(SiegeX[0] + 1.5f, 15f), P(SiegeX[0] + 1.5f, 17.4f));
            Lay(W2, P(SiegeX[0] + 1.6f, Wall2Face(SiegeX[0]) + 4.4f), P(TowerX1 + .2f, EntryZ));
            Lay(W2, P(TowerX0 - .2f, ExitZ), P(RookCraneX + 3.5f, Wall2Inner(RookCraneX) - 2.5f));
            Lay(W3, P(RookCraneX + 1f, Wall3Face(RookCraneX) + 4f), P(GateHX0 - 2.6f, (BannerZ0 + BannerZ1) * .5f));
            Lay(GateY, P(GateHX0 + 1.5f, (BannerZ0 + BannerZ1) * .5f), P(0f, 78f), P(0f, KeepZ - .5f));
        }

        // An arrow ">" pointing along `turn`: two gold bars.
        static void Chevron(Transform parent, Vector3 at, Quaternion turn)
        {
            foreach (float s in new[] { -1f, 1f })
            {
                var arm = turn * Quaternion.Euler(0f, s * 40f, 0f);
                YardKit.Box(parent, "RR arrow", at - arm * V(0f, 0f, .35f), V(.16f, .02f, .8f), Arrow, YardKit.Solid.None, arm);
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
