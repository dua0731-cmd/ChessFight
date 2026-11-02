using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // Early obstacle section 1-1, the royal water garden v31 (Docs/KingRush/PAWN_RUSH_ROYAL_GARDEN.md, the user's
    // 2026-11-02 review of the Blender garden). The garden's walled square (92 x 123 m inside the walls) is walked as
    // one folded way instead of straight up its middle: the fountain plaza and its five small spin bars, west along the
    // geyser canal and up a ramp, east across the water on eight bobbing platforms (a very hard shortcut leaves the
    // fourth one across the water), north through the east lock's sluice gates against the current, west along the
    // robot dyke, north across the reservoir on the boats crossing it sideways, through the gate and out.
    //
    // Section frame: the Blender garden's own numbers, x east, z north (Blender's y), y up = a floor's top. The section
    // is entered at z -61 (the start yard) and left at z 72.5 (the mini-game entrance band), both 12 m wide.
    //
    //   Early_1-1_RoyalWaterGarden (RoyalWaterGarden, PawnRushSection)
    //     Content
    //       B1_FountainPlaza   the start yard, the round plaza and its kerb, five spin bars (two kinds), CP0
    //       B2_GeyserCanal     the canal walk west, the ramp north, eight geysers (GardenGeyser), CP1
    //       B3_Platforms       the west landing (CP2), eight bobbing platforms
    //       B3_Shortcut        three bobbing posts, a post with a small spin bar, two tilting lotus pads (TiltingPad)
    //       B4_EastLock        the east landing (CP3), the lock, three sluice gates, the current (WaterCurrent)
    //       B5_RobotDyke       the dyke (CP4, CP5 where the shortcut lands), two robot arms (RobotSweep)
    //       B6_BoatCrossing    the boarding quay (CP6), the reservoir, the boat tunnels, eight boats (BoatLanes)
    //       B7_Gate            the gate yard and arch, the exit terrace (CP7), four steps down, the entrance band
    //       Garden             the walls and towers, the water (respawn) and its kill volumes, the island
    //       ProgressPath       the one route, for the HUD and the validator
    //
    // Everything solid is unclimbable (NoClimbSurface). Built inactive and switched on at the end, so nothing inside
    // wakes up half placed.
    public static partial class RoyalWaterGardenBuilder
    {
        public const string DisplayName = "왕실 수로 정원";
        public const float Deep = -6f, WaterY = -.8f, ReservoirY = 1.95f, Upper = 3.8f, WestLandingY = 1.8f;
        public static readonly Vector3 Entry = new Vector3(0f, 0f, -61f), Exit = new Vector3(0f, Upper - 1f, 72.5f);

        public static GameObject Build(RoyalWaterGarden garden)
        {
            var kit = garden.Kit;
            var old = garden.transform.Find(RoyalWaterGarden.ContentName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(RoyalWaterGarden.ContentName);
            root.SetActive(false);
            root.transform.SetParent(garden.transform, false);
            var b = new CourseBuilder(kit, root.transform);

            b.In("B1_FountainPlaza", () => FountainPlaza(b));
            b.In("B2_GeyserCanal", () => GeyserCanal(b));
            b.In("B3_Platforms", () => Platforms(b));
            b.In("B3_Shortcut", () => Shortcut(b));
            b.In("B4_EastLock", () => EastLock(b));
            b.In("B5_RobotDyke", () => RobotDyke(b));
            b.In("B6_BoatCrossing", () => BoatCrossing(b));
            b.In("B7_Gate", () => Gate(b));
            b.In("Garden", () => Shell(b));
            Path(b);

            var section = garden.GetComponent<PawnRushSection>();
            if (section != null)
                section.Describe(RoyalWaterGarden.Id, DisplayName, PawnRushSection.SlotKind.Obstacle, Entry, Exit, 0f, Beats());
            root.SetActive(true);
            return root;
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // ------------------------------------------------------------------ the vocabulary

        // A floor block: blue 1 m grid on top, grey grid on its sides, unclimbable.
        static GameObject Mass(CourseBuilder b, string name, float x0, float x1, float z0, float z1, float top, float bottom = Deep)
        {
            var go = b.Box(name, V(x0, bottom, z0), V(x1, top, z1), b.kit.floorChecker, true);
            go.AddComponent<CourseFloor>();
            return go;
        }

        // A wall, a railing, a pillar's block: grey grid, unclimbable.
        static GameObject Solid(CourseBuilder b, string name, Vector3 a, Vector3 c) => b.Box(name, a, c, b.kit.wallNoClimb, true);

        // A 1.1 m railing on a floor at `floor`.
        static GameObject Rail(CourseBuilder b, string name, float x0, float x1, float z0, float z1, float floor, float height = 1.1f) =>
            Solid(b, name, V(x0, floor, z0), V(x1, floor + height, z1));

        // A picture: no collider.
        static GameObject Picture(CourseBuilder b, string name, Vector3 a, Vector3 c, Material material) =>
            b.Box(name, a, c, material, false, false);

        // A sky-blue patch on a floor: a checkpoint's or a rest spot's.
        static GameObject Marker(CourseBuilder b, string name, float x0, float x1, float z0, float z1, float y) =>
            Picture(b, name, V(x0, y + .005f, z0), V(x1, y + .03f, z1), CourseKit.Or(b.kit.marker, b.kit.rankGold));

        // A flat picture line from `a` to `c` (the current's arrows).
        static void Line(CourseBuilder b, string name, Vector3 a, Vector3 c, float width, Material material)
        {
            var go = b.SlopeBetween(name, a, c, width, .01f, .01f, material, false);
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        // A group whose children move together: a kinematic root driven by ObstacleMotion (phase in cycles).
        static ObstacleMotion Mover(CourseBuilder b, string name, Vector3 at, MotionKind kind, Vector3 axis, float phaseCycles)
        {
            var root = b.Group(name);
            root.localPosition = at;
            var body = root.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var motion = root.gameObject.AddComponent<ObstacleMotion>();
            motion.kind = kind;
            motion.axis = axis;
            motion.useLocalTranslationAxes = false;
            motion.phase = Mathf.Repeat(phaseCycles, 1f);
            motion.shove = false;
            return motion;
        }

        // Up and down `bob` either side of where it is placed, once every `period` seconds.
        static Transform Bobbing(CourseBuilder b, string name, Vector3 at, float bob, float period, float phaseCycles)
        {
            if (bob <= 0f)
            {
                var still = b.Group(name);
                still.localPosition = at;
                return still;
            }
            var motion = Mover(b, name, at, MotionKind.Translate, Vector3.up, phaseCycles);
            motion.distance = bob;
            motion.period = period;
            return motion.transform;
        }

        // Calls inside `body` build under `under`.
        static void Under(CourseBuilder b, Transform under, System.Action body)
        {
            var before = b.parent;
            b.parent = under;
            try { body(); }
            finally { b.parent = before; }
        }

        // How far an obstacle reaches across the way where it stands (for the validator).
        static void Span(GameObject go, Vector3 across, float reachMin, float reachMax, float probeY)
        {
            if (go == null) return;
            go.AddComponent<ObstacleSpan>().Configure(across, reachMin, reachMax, ObstacleSpan.Probe.Floor, probeY, Vector3.zero);
        }

        // An intended gap between two points on the way (the validator's 1.5 m rule does not apply).
        static void Gap(CourseBuilder b, string name, Vector3 from, Vector3 to, float halfWidth)
        {
            var flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
            var box = b.Group(name);
            box.localPosition = (from + to) * .5f;
            box.localRotation = Quaternion.Euler(0f, Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, 0f);
            box.gameObject.AddComponent<IntendedGap>().Configure(new Vector3(halfWidth * 2f, Mathf.Abs(to.y - from.y) + 8f, flat.magnitude + 2f), name);
        }

        // ------------------------------------------------------------------ the garden around it all

        static void Shell(CourseBuilder b)
        {
            var k = b.kit;
            // The outer walls (x +-46.5..48.5, z -63..62), open 12.6 m where the way comes in (south) and goes out (north).
            Solid(b, "Garden wall W", V(-48.5f, Deep, -63f), V(-46.5f, 7f, 62f));
            Solid(b, "Garden wall E", V(46.5f, Deep, -63f), V(48.5f, 7f, 62f));
            Solid(b, "Garden wall S (west)", V(-46.5f, Deep, -63f), V(-6.3f, 7f, -61.5f));
            Solid(b, "Garden wall S (east)", V(6.3f, Deep, -63f), V(46.5f, 7f, -61.5f));
            Solid(b, "Garden wall N (west)", V(-46.5f, Deep, 60.5f), V(-6.3f, 9f, 62f));
            Solid(b, "Garden wall N (east)", V(6.3f, Deep, 60.5f), V(46.5f, 9f, 62f));
            foreach (var at in new[] { V(-47.5f, Deep, -62f), V(47.5f, Deep, -62f), V(-47.5f, Deep, 61f), V(47.5f, Deep, 61f) })
                b.Cylinder("Garden tower", at, 8f, 22f - Deep, k.wallNoClimb, true, true);
            // The water between everything: falling in ends the try (1.5 s, back on the highest checkpoint).
            Picture(b, "Garden water (respawn)", V(-46.5f, WaterY - .05f, -61.5f), V(46.5f, WaterY, 60.5f), CourseKit.Or(k.respawnFloor, k.trimBlack));
            b.Trigger<KillVolume>("Kill volume (garden water)", V(-46.5f, WaterY - 2.2f, -61.5f), V(46.5f, WaterY - .1f, 60.5f));
            b.Trigger<KillVolume>("Kill volume (far below)", V(-80f, -42f, -90f), V(80f, -40f, 100f));
            // The island in the north-west water and its fountain (scenery).
            b.Cylinder("Garden island", V(-18f, Deep, 16f), 10f, .6f - Deep, k.wallNoClimb, true, true);
            b.Cylinder("Island fountain", V(-18f, .6f, 16f), 2f, 2f, k.glass, false);
            // The terraces either side of the reservoir and the gate yard (scenery, not the way).
            Solid(b, "Garden terrace W (reservoir)", V(-44f, Deep, 33f), V(-26f, Upper, 48.6f));
            Solid(b, "Garden terrace E (reservoir)", V(26f, Deep, 33f), V(44f, Upper, 48.6f));
            Solid(b, "Garden terrace W (gate)", V(-44f, Deep, 48.6f), V(-14.3f, Upper, 59f));
            Solid(b, "Garden terrace E (gate)", V(14.3f, Deep, 48.6f), V(44f, Upper, 59f));
        }

        // ------------------------------------------------------------------ path and beats

        // The one route, given as both teams' (no team lanes in this garden). Each point names the beat of the leg
        // that starts at it. Across the platforms it runs through their middles at their middle height; across the
        // reservoir straight north (the boats carry it sideways).
        static void Path(CourseBuilder b)
        {
            var points = new List<Vector3>();
            var beats = new List<string>();
            System.Action<Vector3, string> add = (p, beat) => { points.Add(p); beats.Add(beat); };
            foreach (var p in new[] { V(0, 0, -61), V(0, 0, -57), V(0, 0, -52), V(0, 0, -45), V(-6.3f, 0, -37.2f), V(-3, 0, -30), V(0, 0, -23), V(0, 0, -18.5f) })
                add(p, "1");
            foreach (var p in new[] { V(-2, 0, -15.5f), V(-31, 0, -15.5f), V(-31, 0, -12.5f), V(-31, WestLandingY, -1) }) add(p, "2");
            add(V(-28.5f, WestLandingY, 3.5f), "3");
            for (int i = 0; i < PlatformCount; i++) add(PlatformTop(i), "3");
            foreach (var p in new[] { V(28, Upper, 5.5f), V(31, Upper, 9), V(31, Upper, 24) }) add(p, "4");
            foreach (var p in new[] { V(31, Upper, 27), V(ShortcutLanding.x, Upper, 27), V(8, Upper, 27) }) add(p, "5");
            add(V(2, Upper, 30.5f), "6");
            add(V(0, Upper, 33), "6");
            foreach (float z in LaneZ) add(V(0, Upper, z), "6");
            add(V(0, Upper, 48.6f), "6");
            foreach (var p in new[] { V(0, Upper, 51), V(0, Upper, 55), V(0, Upper, 60.5f), V(0, Upper, 63.5f), V(0, Upper, 66.5f), V(0, Upper - 1f, 68.5f), V(0, Upper - 1f, 72.5f) })
                add(p, "7");
            var route = points.ToArray();
            var go = new GameObject("ProgressPath");
            go.transform.SetParent(b.parent, false);
            go.AddComponent<ProgressPath>().Configure(route, (Vector3[])route.Clone(), beats.ToArray());
        }

        // Areas for the HUD and the CSV, the first that holds a pawn wins.
        public static SectionBeat[] Beats() => new[]
        {
            new SectionBeat("1 분수 광장 (작은 회전 막대 5개)", V(-18f, -2f, -62f), V(18f, 5f, -18.5f)),
            new SectionBeat("2 물기둥 수로길", V(-36f, -2f, -18.5f), V(8f, 5f, -1f)),
            new SectionBeat("4 동쪽 갑문", V(25.2f, 1f, -1f), V(36f, 10f, 24f)),
            new SectionBeat("3 출렁 발판과 지름길", V(-36f, -1f, -1f), V(25.2f, 10f, 24f)),
            new SectionBeat("5 로봇 팔 둑길", V(8f, 1f, 24f), V(36f, 12f, 33f)),
            new SectionBeat("6 보트 건너기", V(-27f, -1f, 26.5f), V(27f, 10f, 48.6f)),
            new SectionBeat("7 성문", V(-15f, 1f, 48.6f), V(15f, 14f, 73f)),
        };
    }
}
