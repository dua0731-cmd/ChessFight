using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // Course 01 v0.4 (Pawn Rush course 01 level design v0.4, Docs/KingRush/PAWN_RUSH_COURSE01_DESIGN_v0.4.md):
    // one road both teams run, through two mission plazas where each team's mini-game station sits
    // beside the other's. Every part is at the design doc's WORLD coordinates (§4) - +x east, +y up,
    // +z north; y is a floor's top. Nothing is mirrored between the teams but the plaza stations
    // (side by side, the same game) and the tower's east ramp.
    //
    //   Course01v4_Root
    //     S_Start, A_Shared, Plaza1, B_Shared, Plaza2, C_Shared, T_Tower,
    //     Checkpoints, KillVolumes, ProgressPath, Missions (MissionPicker)
    //
    // Built inactive and switched on at the end, so nothing inside wakes up half placed.
    public static partial class Course01v4Builder
    {
        // PR_02: the jump pad made gentle for the ragdoll (up 9, forward 4.5 m/s): about 4 m up,
        // landing about 6 m on and 3 m higher.
        static readonly Vector3 PadLaunch = new Vector3(0f, 9f, 4.5f);

        // The games built into the stations in the Editor, so the scene shows a whole course (and
        // the validator finds floors on the path). Play draws two afresh (MissionPicker).
        public const int PreviewFirst = PawnRushMissions.Paint, PreviewSecond = PawnRushMissions.Bells;

        public static GameObject Build(Transform parent, Course01Kit kit)
        {
            var root = new GameObject(PawnRushCourse.RootName);
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            var b = new CourseBuilder(kit, root.transform);
            b.In("S_Start", () => Start(b));
            b.In("A_Shared", () => SharedA(b));
            var stations = new MissionStation[4];
            b.In("Plaza1", () => Plaza(b, 1, 24f, 0f, 72f, 4f, 5f, stations));
            b.In("B_Shared", () => SharedB(b));
            b.In("Plaza2", () => Plaza(b, 2, -25f, 3f, 130f, 6f, 7f, stations));
            b.In("C_Shared", () => SharedC(b));
            b.In("T_Tower", () => Tower(b));
            b.In("Checkpoints", () => Checkpoints(b));
            b.In("KillVolumes", () => b.Trigger<KillVolume>("Kill volume (y -10)", V(-45f, -12f, -25f), V(85f, -10f, 250f)));
            Path(b);
            var picker = b.Group("Missions").gameObject.AddComponent<MissionPicker>();
            picker.Configure(stations);
            for (int team = 0; team < 2; team++)
            {
                stations[team].Install(PreviewFirst);
                stations[2 + team].Install(PreviewSecond);
            }
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

        // What an obstacle sweeps across the way (design doc v0.4 §3: no line past it untouched),
        // for the validator.
        static GameObject Span(GameObject go, Vector3 across, float reachMin, float reachMax, float probeY, string group = null)
        {
            go.AddComponent<ObstacleSpan>().Configure(across, reachMin, reachMax, ObstacleSpan.Probe.Floor, probeY, Vector3.zero, group);
            return go;
        }

        // PR_02, firing toward `yaw`.
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

        // §6 moving rook wall: `length` x 3.2 x 2 m, sliding +-distance along x (contact only).
        static GameObject RookWall(CourseBuilder b, string name, Vector3 center, float length, float distance, float period, float phaseSeconds)
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
            float half = length * .5f;
            b.Box("Wall", V(-half, -1.6f, -1f), V(half, 1.6f, 1f), b.kit.hazard);
            int battlements = Mathf.Max(2, Mathf.RoundToInt(length / 2.2f));
            for (int i = 0; i < battlements; i++)
            {
                float x = -half + .5f + i * (length - 1f) / (battlements - 1);
                b.Box("Battlement", V(x - .5f, 1.6f, -1f), V(x + .5f, 2.1f, 1f), b.kit.hazard, false, false);
            }
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
            float cell = 2f / diameter, r = .5f - .12f / diameter;
            for (int ix = -3; ix <= 3; ix++)
                for (int iz = -3; iz <= 3; iz++)
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

        // A frame for a pendulum swinging along z (turned 90): unclimbable posts either side of the
        // way, clear of the ball (it reaches 6 m from the axis), and a beam over the pivot (7.3 up).
        static void GantryZ(CourseBuilder b, string name, float x, float axisZ, float floor, float half)
        {
            float lo = axisZ - half, hi = axisZ + half;
            b.Wall(name + " post S", x - .4f, x + .4f, floor - 1f, floor + 8.3f, lo - .4f, lo + .4f, false);
            b.Wall(name + " post N", x - .4f, x + .4f, floor - 1f, floor + 8.3f, hi - .4f, hi + .4f, false);
            b.Wall(name + " beam", x - .4f, x + .4f, floor + 7.8f, floor + 8.3f, lo - .4f, hi + .4f, false);
        }

        // PR_06_Sliding10 (design doc v0.4 §4 A4): the 06 sliding walls stretched to close a 10 m way.
        // Each wall 5 m wide, resting 7.5 m out and sliding 5 m: one closes half the way while the
        // other is out of it, and the closed half keeps changing. Set on the placed instance; the
        // imported prefab is not touched.
        static GameObject SlidingWalls10(CourseBuilder b, string name, Vector3 at, float yaw, float phaseSeconds)
        {
            var go = Hazard(b, b.kit.slidingWalls, "PR_06_Sliding10 " + name, at, yaw, phaseSeconds);
            foreach (Transform child in go.transform)
            {
                bool left = child.name == "Left sliding wall", right = child.name == "Right sliding wall";
                if (left || right)
                {
                    var p = child.localPosition;
                    child.localPosition = new Vector3(left ? -7.5f : 7.5f, p.y, p.z);
                    var motion = child.GetComponent<ObstacleMotion>();
                    if (motion != null) motion.distance = 5f;
                    var wall = child.Find("Rook wall");
                    if (wall != null) wall.localScale = new Vector3(5f, wall.localScale.y, wall.localScale.z);
                }
                else if (child.name == "Wall housing")
                {
                    var p = child.localPosition;
                    child.localPosition = new Vector3(Mathf.Sign(p.x) * 15.3f, p.y, p.z);
                }
            }
            return go;
        }

        // ------------------------------------------------------------------ mission plaza (§4, §5)

        // A mission plaza: entry band (4 m), white's station (west) and black's (east), 20 x 20 m each
        // with a 4 m shared aisle between, and the north block with one team gate per station.
        //   x0     west edge (white's station x0..x0+20, aisle +20..+24, black's +24..+44)
        //   z0     south edge (entry band z0..z0+4, stations z0+4..z0+24, block z0+24..z0+24+depth)
        // Outer walls 6 m, the north block 10 m (E's 9 m towers stand 2 m in front of it), all marble.
        // The floors of the stations are the games' own (MissionGames).
        static void Plaza(CourseBuilder b, int plaza, float x0, float y, float z0, float gateWidth, float depth, MissionStation[] stations)
        {
            var k = b.kit;
            float x1 = x0 + 44f, cx = x0 + 22f, zs = z0 + 4f, zn = z0 + 24f, ze = zn + depth;
            float[] centers = { x0 + 10f, x0 + 34f };
            const float outer = 6f, north = 10f;
            b.Floor("Entry band", x0, x1, z0, zs, y);
            b.Floor("Aisle", x0 + 20f, x0 + 24f, zs, zn, y);
            b.Wall("Outer wall W", x0 - 1f, x0, y - 1f, y + outer, z0 - 1f, ze, false);
            b.Wall("Outer wall E", x1, x1 + 1f, y - 1f, y + outer, z0 - 1f, ze, false);
            b.Wall("Outer wall S (west of the entry)", x0, cx - 6f, y - 1f, y + outer, z0 - 1f, z0, false);
            b.Wall("Outer wall S (east of the entry)", cx + 6f, x1, y - 1f, y + outer, z0 - 1f, z0, false);
            float g = gateWidth * .5f;
            b.Wall("North block W", x0, centers[0] - g, y - 1f, y + north, zn, ze, false);
            b.Wall("North block middle", centers[0] + g, centers[1] - g, y - 1f, y + north, zn, ze, false);
            b.Wall("North block E", centers[1] + g, x1, y - 1f, y + north, zn, ze, false);
            b.Trigger<PlazaEntry>("Plaza entry (picture card)", V(x0, y, z0), V(x1, y + 3f, zs)).Configure(plaza);
            for (int team = 0; team < 2; team++)
                stations[(plaza - 1) * 2 + team] = Station(b, plaza, team, centers[team], y, zs, gateWidth, depth);
        }

        static MissionStation Station(CourseBuilder b, int plaza, int team, float cx, float y, float z0, float gateWidth, float depth)
        {
            var k = b.kit;
            var g = b.Group("Station_" + plaza + "_" + Teams.Name(team));
            g.localPosition = V(cx, y, z0);
            var before = b.parent;
            b.parent = g;
            float half = gateWidth * .5f, u = MissionStation.Size;
            // The gate passage through the north block: its floor, the door at the station's edge,
            // the team barrier just behind it (before plaza 2's promotion pads), the team's colour over the door.
            b.Floor("Gate passage", -half, half, u, u + depth, 0f);
            var door = b.Box("Team gate", V(-half, 0f, u), V(half, 4.6f, u + .4f), k.Team(team), true);
            var gate = door.AddComponent<TeamGate>();
            var barrier = b.Box("Team barrier", V(-half, 0f, u + .6f), V(half, 10f, u + .8f), k.glass, true);
            barrier.AddComponent<TeamBarrier>().SetTeam(team);
            b.Box("Gate colour", V(-half - .3f, 4.6f, u - .05f), V(half + .3f, 5.2f, u), k.Team(team), false, false);
            // Black's pads are the mirror of white's (the queen on the outer side of both).
            float s = team == Teams.Black ? -1f : 1f;
            Promotion(b, plaza, team, V(0f, 0f, 1f), new[] { -8f * s, -6f * s, 6f * s, 8f * s }, new[] { 0f, 0f, 0f, 0f });
            // Promotion 3 in plaza 2's 6 x 6 gate passage, beside the way through (only the team gets in).
            if (plaza == 2) Promotion(b, 3, team, V(0f, 0f, u + 4f), new[] { -2f * s, 2f * s, -2f * s, 2f * s }, new[] { -1f, -1f, 1f, 1f });
            var socket = b.Group("Game socket");
            b.parent = before;
            var station = g.gameObject.AddComponent<MissionStation>();
            station.Configure(team, plaza, team == Teams.Black, gate, socket, k);
            return station;
        }

        // Four 2 x 2 m pads (queen, rook, bishop, knight) at (xs[i], zs[i]) from `at`.
        static void Promotion(CourseBuilder b, int zone, int team, Vector3 at, float[] xs, float[] zs)
        {
            var k = b.kit;
            var g = b.Group("PromotionZone_" + zone);
            g.localPosition = at;
            var before = b.parent;
            b.parent = g;
            Material[] looks = { k.rankGold, k.wallNoClimb, k.trimBlack, k.hazard };
            string[] names = { "Queen pad", "Rook pad", "Bishop pad", "Knight pad" };
            var pads = new Transform[4];
            for (int i = 0; i < 4; i++)
                pads[i] = b.Box(names[i], V(xs[i] - .95f, 0f, zs[i] - .95f), V(xs[i] + .95f, .04f, zs[i] + .95f), looks[i], false, false).transform;
            b.parent = before;
            g.gameObject.AddComponent<PromotionZone>().Configure(zone, team, pads);
        }
    }
}
