using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // Course 01 v0.2 "여덟 번째 랭크" (Pawn Rush course 01 level design v0.2, Docs/KingRush/
    // PAWN_RUSH_COURSE01_DESIGN_v0.2.md): a three-storey fort wound once round the 8th-rank tower
    // in the middle of the map, every part at the design doc's WORLD coordinates (§5, §6) -
    // +x east (black), +y up, +z north. White's wings are built at x < 0 and mirrored for black.
    //
    //   Course01v2_Root
    //     S_Start, A_Ground, W1_White, W1_Black, B_Middle, W2_White, W2_Black, C_Upper, T_Tower,
    //     Checkpoints (shared ones; a wing keeps its own), KillVolumes, ProgressPath
    //
    // Built inactive and switched on at the end, so nothing inside wakes up half placed.
    public static partial class Course01v2Builder
    {
        // PR_02: the jump pad made gentle for the ragdoll (up 9, forward 4.5 m/s): about 4 m up,
        // landing about 6 m on and 3 m higher.
        static readonly Vector3 PadLaunch = new Vector3(0f, 9f, 4.5f);

        public static GameObject Build(Transform parent, Course01Kit kit)
        {
            var root = new GameObject(PawnRushCourse.RootName);
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            var b = new CourseBuilder(kit, root.transform);
            b.In("S_Start", () => Start(b));
            b.In("A_Ground", () => Ground(b));
            TeamMirror.MirrorTeam(b.In("W1_White", () => Wing1(b)));
            b.In("B_Middle", () => Middle(b));
            TeamMirror.MirrorTeam(b.In("W2_White", () => Wing2(b)));
            b.In("C_Upper", () => Upper(b));
            b.In("T_Tower", () => Tower(b));
            b.In("Checkpoints", () => SharedCheckpoints(b));
            b.In("KillVolumes", () => b.Trigger<KillVolume>("Kill volume (y -10)", V(-70f, -12f, -40f), V(70f, -10f, 150f)));
            Path(b);
            root.SetActive(true);
            return root;
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // ------------------------------------------------------------------ obstacles and devices

        // An imported obstacle, turned to its direction of travel (north 0, east 90, south 180,
        // west 270), with its phase in seconds on the shared clock.
        static GameObject Hazard(CourseBuilder b, GameObject prefab, string name, Vector3 at, float yaw = 0f, float phaseSeconds = 0f)
        {
            var go = b.Obstacle(prefab, name, at, yaw);
            ObstaclePhase.Shift(go, phaseSeconds);
            return go;
        }

        // PR_02 (design doc: `PR_02_JumpPad_Rank`), firing toward `yaw`.
        static void JumpPad(CourseBuilder b, string name, Vector3 at, float yaw)
        {
            var pad = b.Obstacle(b.kit.jumpPad, "PR_02 Jump pad " + name, at, yaw);
            var surface = pad.GetComponent<ObstacleSurface>();
            if (surface != null) surface.launchVelocity = PadLaunch;
        }

        static void Disc(CourseBuilder b, string name, Vector3 top, bool counterClockwise)
        {
            // The prefab's top is 0.4 over its root.
            var disc = b.Obstacle(b.kit.spinningDisc, "01 Spinning disc " + name, top - Vector3.up * .4f);
            var surface = disc.GetComponent<ObstacleSurface>();
            if (surface != null) surface.counterClockwise = counterClockwise;
        }

        // 17 knight cavalry. The prefab lands 2 m behind its root and charges 8 m along its +x;
        // `start` (local to the root) moves where it leaps from, `leap` how high it arcs.
        static void Knight(CourseBuilder b, string name, Vector3 root, float yaw, float phase, Vector3? start, float leap)
        {
            var go = Hazard(b, b.kit.knightCavalry, "17 Knight cavalry " + name, root, yaw, phase);
            var knight = go.GetComponent<KnightCavalryCharge>();
            if (knight == null) return;
            knight.LeapHeight = leap;
            if (start.HasValue && knight.StartPoint != null)
                knight.StartPoint.position = go.transform.TransformPoint(start.Value);
        }

        // §6 rotating jump bar: a bar spinning about its middle, its top 0.8 m up (contact only).
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

        // §6 moving rook wall: 11 x 3.2 x 2 m, sliding +-distance along x (contact only).
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

        // §6 rotating chessboard, round: a disc 0.5 m thick, its top at `center.y`, riders carried by
        // the same moving-surface handling as the square board.
        static GameObject RotatingBoardRound(CourseBuilder b, Vector3 center, float diameter, float degreesPerSecond)
        {
            var board = b.Cylinder("Device_RotatingBoardRound", center - Vector3.up * .5f, diameter, .5f, b.kit.trimWhite);
            board.AddComponent<CourseFloor>();
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
                    b.Box("Dark square", V(cx - cell * .48f, 1f, cz - cell * .48f), V(cx + cell * .48f, 1.04f, cz + cell * .48f),
                          b.kit.trimBlack, false, false);
                }
            b.parent = before;
            return board;
        }

        // §6 pawn statue cover: a 3.6 m base and a pawn about 3.6 m tall; it stops the shockwave.
        static void PawnStatue(CourseBuilder b, string name, Vector3 at)
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
        }

        // A frame for pendulums swinging along x (the prefab only has a ceiling mount): unclimbable
        // posts 7 m either side of the axis, clear of the ball (it reaches 6 m), and a beam over it.
        static void Gantry(CourseBuilder b, string name, float axisX, float z, float floor)
        {
            float lo = axisX - 7f, hi = axisX + 7f;
            b.Wall(name + " post A", lo - .4f, lo + .4f, floor - 1f, floor + 8.3f, z - .4f, z + .4f, false);
            b.Wall(name + " post B", hi - .4f, hi + .4f, floor - 1f, floor + 8.3f, z - .4f, z + .4f, false);
            b.Wall(name + " beam", lo - .4f, hi + .4f, floor + 7.8f, floor + 8.3f, z - .4f, z + .4f, false);
        }

        // A team barrier across x = `x` from z0 to z1: its own team (white here) walks through, the
        // other team is stopped; it cannot be climbed.
        static void Barrier(CourseBuilder b, string name, float x, float z0, float z1, float y0, float y1)
        {
            var wall = b.Box(name, V(x - .1f, y0, z0), V(x + .1f, y1, z1), b.kit.glass, true);
            wall.AddComponent<TeamBarrier>().SetTeam(Teams.White);
        }

        // The team-coloured arch over an alcove opening in a wall at x -16..-15.
        static void Arch(CourseBuilder b, string name, float z0, float z1, float y0, float y1)
        {
            var k = b.kit;
            var arch = b.Box(name, V(-16f, y0, z0), V(-15f, y1, z1), k.trimWhite, true);
            arch.AddComponent<TeamTint>().Configure(k.trimWhite, k.trimBlack, Teams.White);
        }

        // ------------------------------------------------------------------ mini-game island

        // A mini-game island (§7 slot spec, as in v0.1) turned to its direction of travel. Island-local:
        // x 0 = its middle (12 m wide), z 0 = the entry, +z the way through:
        //   0..4 promotion pads · 4..22 game area (18 x 12) · 22..exit ramp or high stage ·
        //   door at the exit height · landing to the end.
        // exitSide: where the landing lets out: -1 its -x side, +1 its +x side, 0 its far end.
        static MiniGameSlot Slot(CourseBuilder b, int slot, Vector3 at, float yaw, float exitHeight, float rampLength,
                                 float landing, int rank, int exitSide)
        {
            var k = b.kit;
            var g = b.Group("MiniGameSlot_" + slot);
            g.localPosition = at;
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var before = b.parent;
            b.parent = g;
            float exit = 22f + rampLength, end = exit + landing, top = exitHeight;

            b.Floor("Island floor", -6f, 6f, 0f, exit);
            b.Wall("Landing", -6f, 6f, -1f, top, exit, end, false, true);
            // 1.5 m unclimbable railing all round, open only at the entry and where the landing lets out.
            b.Rail("Entry rail L", -6.3f, -1.5f, 0f, .3f, 0f, 1.5f);
            b.Rail("Entry rail R", 1.5f, 6.3f, 0f, .3f, 0f, 1.5f);
            b.Rail("Side rail L", -6.3f, -6f, 0f, exit, 0f, 1.5f);
            b.Rail("Side rail R", 6f, 6.3f, 0f, exit, 0f, 1.5f);
            if (exitSide != -1) b.Rail("Landing rail L", -6.3f, -6f, exit, end, top, 1.5f);
            if (exitSide != 1) b.Rail("Landing rail R", 6f, 6.3f, exit, end, top, 1.5f);
            if (exitSide != 0) b.Rail("Landing end rail", -6.3f, 6.3f, end, end + .3f, top, 1.5f);

            // The exit part: a ramp (A, C, E) or a block whose front cannot be climbed (B, D).
            var ramp = b.Ramp("Exit ramp", -3f, 3f, 22f, exit, 0f, top);
            var stage = b.Wall("Exit high stage", -6f, 6f, -1f, top, 22f, exit, false, true);

            // The door at the exit height, with walls either side so the 6 m ramp cannot go round it.
            b.Wall("Door wall L", -6f, -1.5f, top, top + 4f, exit, exit + .4f, false);
            b.Wall("Door wall R", 1.5f, 6f, top, top + 4f, exit, exit + .4f, false);
            b.Wall("Door lintel", -1.5f, 1.5f, top + 3.6f, top + 4f, exit, exit + .4f, false);
            var door = b.Box("Door", V(-1.5f, top, exit + .05f), V(1.5f, top + 3.5f, exit + .35f), k.hazard, true)
                        .AddComponent<SlotDoor>();
            RankMarker.Build(g, "Rank " + rank, V(-3.75f, top + .7f, exit - .04f), rank, 4f, 1.6f, k.rankGold);

            Promotion(b, slot == 1 ? 1 : 2, V(0f, 0f, 2f));
            if (slot == 2) Promotion(b, 3, V(0f, top, exit + 2.5f));   // zone 3 on slot 2's landing
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

        // Four 2 x 2 m pads across at -5, -3, 3, 5 (the middle 4 m is the team's way through).
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
