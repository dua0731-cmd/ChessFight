using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // Course 01 "여덟 번째 랭크" (Pawn Rush course 01 level design v0.1, §4 and §6), module by
    // module, in each module's own coordinates: origin at the middle of the entry edge on the
    // entry floor, +z toward the finish, +x the black side. Every number here is a line of
    // the design doc's tables; the comments name the line where it is not obvious.
    //
    // Build makes one module's root (inactive while it is built, so nothing inside wakes up
    // before it is in place). The editor bakes the result into Prefabs/Modules once; the
    // course root builds straight from here when the scene has never been built.
    public static partial class Course01Modules
    {
        public static string PrefabName(ModuleId id)
        {
            string[] parts = id.ToString().Split('_');
            string code = parts[0];
            if (code[0] == 'M' && code.Length == 2) code = "M0" + code.Substring(1);
            return "PR01_" + code + "_" + parts[1];
        }

        public static GameObject Build(ModuleId id, Course01Kit kit, Transform parent, Vector3 worldOrigin)
        {
            var root = new GameObject(PrefabName(id));
            root.SetActive(false);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.position = worldOrigin;
            root.transform.rotation = parent != null ? parent.rotation : Quaternion.identity;
            var b = new CourseBuilder(kit, root.transform);
            var m = root.AddComponent<CourseModule>();
            switch (id)
            {
                case ModuleId.M0_Start: M0(b, m); break;
                case ModuleId.M1_SpinBarYard: M1(b, m); break;
                case ModuleId.M2_PusherCorridor: M2(b, m); break;
                case ModuleId.M3_ThirdRank: M3(b, m); break;
                case ModuleId.M4_ClockTerrace: M4(b, m); break;
                case ModuleId.M5_DiscPit: M5(b, m); break;
                case ModuleId.M6_TeamSection1: M6(b, m); break;
                case ModuleId.M7_RotatingBoard: M7(b, m); break;
                case ModuleId.M8_PendulumHall: M8(b, m); break;
                case ModuleId.M9_FoldingBridges: M9(b, m); break;
                case ModuleId.M10_KnightPlaza: M10(b, m); break;
                case ModuleId.M11_TeamSection2: M11(b, m); break;
                case ModuleId.M12_ThreeBridges: M12(b, m); break;
                case ModuleId.M13_RookWallPlaza: M13(b, m); break;
                case ModuleId.M14_EighthRank: M14(b, m); break;
                case ModuleId.X1_ConveyorHill: X1(b, m); break;
                case ModuleId.X2_HammerHall: X2(b, m); break;
                case ModuleId.X3_FastBarIsland: X3(b, m); break;
            }
            root.SetActive(true);
            return root;
        }

        static Vector3[] Line(float length, float x = 0f) => new[] { new Vector3(x, 0f, 0f), new Vector3(x, 0f, length) };

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // The 1.2 m unclimbable side walls of M0..M4 (x = +-15, outside the floor).
        static void SideRails(CourseBuilder b, float z0, float z1, float floor = 0f, float half = 15f)
        {
            b.Rail("Side wall L", -half - .5f, -half, z0, z1, floor);
            b.Rail("Side wall R", half, half + .5f, z0, z1, floor);
        }

        // ------------------------------------------------------------------ kit devices (재료집 §6)

        // §6 rotating jump bar: a bar spinning about its middle, its top 0.8 m up - jumpable. Pushes by
        // contact only (no push script, as in the source scene).
        static GameObject SpinBar(CourseBuilder b, string name, Vector3 hub, float length, float degreesPerSecond)
        {
            var root = new GameObject("Device_SpinBar " + name);
            root.transform.SetParent(b.parent, false);
            root.transform.localPosition = hub + Vector3.up * .65f;
            var motion = root.AddComponent<ObstacleMotion>();
            motion.kind = MotionKind.Rotate;
            motion.axis = Vector3.up;
            motion.degreesPerSecond = degreesPerSecond;
            var before = b.parent;
            b.parent = root.transform;
            b.Box("Bar", V(-length * .5f, -.16f, -.225f), V(length * .5f, .16f, .225f), b.kit.hazard);
            b.Cylinder("Hub", V(0f, -.65f, 0f), .85f, .9f, b.kit.rankGold, false);
            b.parent = before;
            return root;
        }

        // §6 moving rook wall: 11 x 3.2 x 2 m, sliding +-distance across the course (contact only).
        static GameObject RookWall(CourseBuilder b, string name, Vector3 center, float distance, float period, float phaseSeconds)
        {
            var root = new GameObject("Device_RookWall " + name);
            root.transform.SetParent(b.parent, false);
            root.transform.localPosition = center + Vector3.up * 1.6f;
            var motion = root.AddComponent<ObstacleMotion>();
            motion.kind = MotionKind.Translate;
            motion.axis = Vector3.right;
            motion.distance = distance;
            motion.period = period;
            motion.useLocalTranslationAxes = true;
            var before = b.parent;
            b.parent = root.transform;
            b.Box("Wall", V(-5.5f, -1.6f, -1f), V(5.5f, 1.6f, 1f), b.kit.hazard);
            for (int i = -2; i <= 2; i++)
                b.Box("Battlement", V(i * 2.2f - .5f, 1.6f, -1f), V(i * 2.2f + .5f, 2.1f, 1f), b.kit.hazard, false, false);
            b.parent = before;
            ObstaclePhase.Shift(root, phaseSeconds);
            return root;
        }

        // §6 rotating chessboard, round (new): a 23 m disc, 0.5 m thick, its top at `center.y`, turning
        // 18 degrees a second; riders are carried by the same IMovingSurface handling as the square
        // board (ObstacleMotion binds ImportedMovingSurface). A square board's corners reach out to
        // 16.3 m as it turns, so the gap to the bridges kept changing between 0 and 5 m.
        static GameObject RotatingBoardRound(CourseBuilder b, Vector3 center, float diameter, float degreesPerSecond)
        {
            var board = b.Cylinder("Device_RotatingBoardRound", center - Vector3.up * .5f, diameter, .5f, b.kit.trimWhite);
            var motion = board.AddComponent<ObstacleMotion>();
            motion.kind = MotionKind.Rotate;
            motion.axis = Vector3.up;
            motion.degreesPerSecond = degreesPerSecond;
            // Dark squares that turn with it (the floor material's checker is fixed to the world).
            var before = b.parent;
            b.parent = board.transform;
            float cell = 4.5f / diameter, r = .5f - .12f / diameter;
            for (int ix = -2; ix <= 2; ix++)
                for (int iz = -2; iz <= 2; iz++)
                {
                    if (((ix + iz) & 1) != 0) continue;
                    float cx = ix * cell, cz = iz * cell;
                    if (Mathf.Sqrt(cx * cx + cz * cz) + cell * .72f > r) continue;
                    var tile = b.Box("Tile", V(cx - cell * .48f, 1f, cz - cell * .48f), V(cx + cell * .48f, 1.04f, cz + cell * .48f),
                                     b.kit.trimBlack, false, false);
                    tile.name = "Dark square";
                }
            b.parent = before;
            return board;
        }

        // §6 pawn statue cover: a 3.6 m base and a pawn about 3.6 m tall. It blocks the falling
        // pieces' shockwave (재료집 §5-10).
        static GameObject PawnStatue(CourseBuilder b, string name, Vector3 at)
        {
            var root = b.Group("Device_PawnStatue " + name);
            root.localPosition = at;
            var before = b.parent;
            b.parent = root;
            var k = b.kit;
            b.Box("Base", V(-1.8f, 0f, -1.8f), V(1.8f, .6f, 1.8f), k.wallNoClimb, true);
            b.Cylinder("Body", V(0f, .6f, 0f), 1.9f, 2f, k.wallNoClimb, true, true);
            b.Cylinder("Collar", V(0f, 2.55f, 0f), 2.3f, .25f, k.wallNoClimb, true, true);
            b.Sphere("Head", V(0f, 3.15f, 0f), 1.1f, k.wallNoClimb);
            b.parent = before;
            return root.gameObject;
        }

        // An imported obstacle with its phase in seconds.
        static GameObject Hazard(CourseBuilder b, GameObject prefab, string name, Vector3 at, float yaw = 0f, float phaseSeconds = 0f)
        {
            var go = b.Obstacle(prefab, name, at, yaw);
            ObstaclePhase.Shift(go, phaseSeconds);
            return go;
        }

        // A gantry for a pendulum (its prefab only has a ceiling mount): two unclimbable posts rising
        // from below the cliff and a beam, clear of the ball (it reaches 6 m either side of the axis).
        static void Gantry(CourseBuilder b, string name, float axisX, float z)
        {
            float lo = axisX - 7f, hi = axisX + 7f;
            b.Wall(name + " post A", lo - .4f, lo + .4f, -8f, 8.3f, z - .4f, z + .4f, false);
            b.Wall(name + " post B", hi - .4f, hi + .4f, -8f, 8.3f, z - .4f, z + .4f, false);
            b.Wall(name + " beam", lo - .4f, hi + .4f, 7.8f, 8.3f, z - .4f, z + .4f, false);
        }

        // ------------------------------------------------------------------ team sections

        // The front of a team section, white half (x < 0): the 6 m front wall with the white alcove,
        // the team arch and the entry barrier. x -17..17 overall, 2 m wider than the course.
        static void TeamEntry(CourseBuilder b)
        {
            var k = b.kit;
            b.Wall("Front wall", -17f, -8.5f, 0f, 6f, 0f, 5f, false);
            b.Wall("Front wall (middle)", -4.5f, 0f, 0f, 6f, 0f, 5f, false);
            b.Floor("Alcove floor", -8.5f, -4.5f, 0f, 5f);
            var arch = b.Box("Team arch", V(-8.5f, 4.4f, 0f), V(-4.5f, 6.2f, .6f), k.trimWhite, true);
            arch.AddComponent<TeamTint>().Configure(k.trimWhite, k.trimBlack, Teams.White);
            Barrier(b, "Team barrier (entry)", 0f, -1f, 10f);
        }

        // A team barrier across x -18..0 at local z, its own team through, the other team stopped.
        static void Barrier(CourseBuilder b, string name, float z, float y0, float y1)
        {
            var wall = b.Box(name, V(-18f, y0, z - .1f), V(0f, y1, z + .1f), b.kit.glass, true);
            wall.AddComponent<TeamBarrier>().SetTeam(Teams.White);
        }

        // The white half of the glass divider (x -0.25..0), 10 m over the floor and over the exit
        // where the floor has risen. It reaches down past the cliffs so nobody slips under it.
        static void Divider(CourseBuilder b, float rampStart, float length, float exitHeight)
        {
            b.Box("Glass divider", V(-.25f, -8f, 0f), V(0f, 10f, rampStart), b.kit.glass, true);
            b.Box("Glass divider (raised)", V(-.25f, -8f, rampStart), V(0f, 10f + exitHeight, length), b.kit.glass, true);
        }

        // A mini-game island (§5 slot spec) for the white lane. `at`: the middle of its entry edge.
        // Island-local: x 0 = island centre (lane x -6.5), +x toward the glass; z 0 = entry.
        //   0..4 promotion pads · 4..22 game area (18 x 12) · 22..exit ramp or high stage ·
        //   door at the exit height · landing to the end.
        static MiniGameSlot Slot(CourseBuilder b, int slot, Vector3 at, float exitHeight, float rampLength, float landing, int rank)
        {
            var k = b.kit;
            var g = b.Group("MiniGameSlot_" + slot);
            g.localPosition = at;
            var before = b.parent;
            b.parent = g;
            const float outer = -6f, inner = 6.25f;           // inner meets the glass (lane x -0.25)
            float exit = 22f + rampLength, end = exit + landing;

            b.Floor("Island floor", outer, inner, 0f, exit);
            b.Wall("Landing", outer, inner, -1f, exitHeight, exit, end, false);
            // 1.5 m unclimbable railing; the glass is the wall on the inner side.
            b.Rail("Entry rail (outer)", outer - .3f, -1.5f, 0f, .3f, 0f, 1.5f);
            b.Rail("Entry rail (inner)", 1.5f, inner, 0f, .3f, 0f, 1.5f);
            b.Rail("Outer rail", outer - .3f, outer, 0f, exit, 0f, 1.5f);
            b.Wall("Outer rail (landing)", outer - .3f, outer, 0f, exitHeight + 1.5f, exit, end, false);

            // The exit part: a ramp (A, C, E) or a block whose front cannot be climbed (B, D).
            var ramp = b.Ramp("Exit ramp", -3f, 3f, 22f, exit, 0f, exitHeight);
            var stage = b.Wall("Exit high stage", outer, inner, -1f, exitHeight, 22f, exit, false);

            // The door, at the exit height (moved there from the area's far floor, §5).
            b.Wall("Door wall (outer)", outer, -1.5f, exitHeight, exitHeight + 4f, exit, exit + .4f, false);
            b.Wall("Door wall (inner)", 1.5f, inner, exitHeight, exitHeight + 4f, exit, exit + .4f, false);
            b.Wall("Door lintel", -1.5f, 1.5f, exitHeight + 3.6f, exitHeight + 4f, exit, exit + .4f, false);
            var door = b.Box("Door", V(-1.5f, exitHeight, exit + .05f), V(1.5f, exitHeight + 3.5f, exit + .35f), k.hazard, true)
                        .AddComponent<SlotDoor>();
            RankMarker.Build(g, "Rank " + rank, V(-3.75f, exitHeight + .7f, exit - .04f), rank, 4f, 1.6f, k.rankGold);

            Promotion(b, slot == 1 ? 1 : 2, V(0f, 0f, 2f));
            if (slot == 2) Promotion(b, 3, V(0f, exitHeight, exit + 2.5f));   // zone 3 on slot 2's landing (z 40.5~42.5)
            b.Trigger<SlotEntry>("Entry (picture card)", V(-1.5f, 0f, 0f), V(1.5f, 3f, 1.2f));

            var socket = b.Group("Game socket");
            socket.localPosition = V(0f, 0f, 13f);
            Placeholder(b, socket);

            b.parent = before;
            var component = g.gameObject.AddComponent<MiniGameSlot>();
            component.Configure(slot, Teams.White, exitHeight, ramp, stage, door, socket);
            return component;
        }

        // MG_Placeholder: a 3 m button in the middle of the area.
        static void Placeholder(CourseBuilder b, Transform socket)
        {
            var before = b.parent;
            var root = b.Group("MG_Placeholder", socket);
            b.parent = root;
            b.Cylinder("Button base", V(0f, 0f, 0f), 3f, .15f, b.kit.hazard);
            var cap = b.Cylinder("Button cap", V(0f, .15f, 0f), 2.6f, .14f, b.kit.rankGold, false);
            b.parent = before;
            root.gameObject.AddComponent<MiniGamePlaceholder>().Configure(cap.transform, 1.5f);
        }

        // Four 2 x 2 m pads at x -5, -3, 3, 5 (the middle 4 m is the team's way through).
        static void Promotion(CourseBuilder b, int zone, Vector3 at)
        {
            var k = b.kit;
            var g = b.Group("PromotionZone_" + zone);
            g.localPosition = at;
            var before = b.parent;
            b.parent = g;
            float[] xs = { -5f, -3f, 3f, 5f };
            Material[] looks = { k.rankGold, k.wallNoClimb, k.trimBlack, k.hazard };
            string[] names = { "Queen pad", "Rook pad", "Bishop pad", "Knight pad" };
            var pads = new Transform[4];
            for (int i = 0; i < 4; i++)
                pads[i] = b.Box(names[i], V(xs[i] - .95f, 0f, -.95f), V(xs[i] + .95f, .04f, .95f), looks[i], false, false).transform;
            b.parent = before;
            g.gameObject.AddComponent<PromotionZone>().Configure(zone, Teams.White, pads);
        }
    }
}
