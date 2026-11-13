using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // Early obstacle section 1-1, the royal water garden v31 (Docs/PawnRush/PAWN_RUSH_ROYAL_GARDEN.md, the user's
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
    //       Garden             the castle ring (walls, towers, banks, the strip by the gate; R474), the terraces, the water
    //                          (respawn) and its kill volumes, the island
    //       ProgressPath       the one route, for the HUD and the validator
    //
    // Everything solid is unclimbable (NoClimbSurface). Built inactive and switched on at the end, so nothing inside
    // wakes up half placed.
    public static partial class RoyalWaterGardenBuilder
    {
        public const string DisplayName = "왕실 수로 정원";
        public const float Deep = -6f, WaterY = -.8f, ReservoirY = 3.0f, Upper = 3.8f, WestLandingY = 1.8f;   // R441: the reservoir 3.0 (was 1.95)
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
            // R400: the reservoir in the garden's water shader too.
            var reservoir = root.transform.Find("B6_BoatCrossing/Reservoir water (respawn)");
            var water = Resources.Load<Material>("PR_GardenWater");
            if (reservoir != null && water != null && reservoir.TryGetComponent<Renderer>(out var surface)) surface.sharedMaterial = water;
            // R412: the concept-art dressing over the graybox (beat by beat; R440: the water's pools and falls too).
            ConceptDressing(root.transform);

            var section = garden.GetComponent<PawnRushSection>();
            if (section != null)
                section.Describe(RoyalWaterGarden.Id, DisplayName, PawnRushSection.SlotKind.Obstacle, Entry, Exit, 0f, Beats());
            root.SetActive(true);
            return root;
        }

        // ------------------------------------------------------------------ the water (R400; R440 in tiers)

        // R440 (승규 11-05, "물을 초반 중반 후반 으로 층으로 나누어서 … 물이 떨어지게 수학적으로 자연스럽게 계산해"): the
        // single sheet under the whole garden (y -1.7, R400) is now three level pools, stepping up as the course climbs -
        // the plaza moat (beats 1-2), the platform lake (beats 3-5) and the reservoir (beat 6, ReservoirY) - the water
        // falling from each to the next over a spillway in the reservoir's south wall and two weirs on the canal walk's
        // line (ArtSource/PawnRush/GardenConcept/water_tiers.py works them out; these heights must match it). The pools'
        // and falls' meshes come with the concept dressing (GardenWater.fbx, ConceptPieces); here is what is played:
        // falling into a pool ends the try (1.5 s, back on the last checkpoint), a kill volume 0.1 m under its surface.
        // R441 (승규 "층이 더 확실히 보이게 좀 더 높게 … 발판이 물에 잠기지 않게"): the steps made bigger - the lake down to -0.55
        // (P1's tub feet, -0.40 at the bottom of its bob, were dipping into -0.35), the moat to -1.9, the reservoir up to
        // 3.0: drops of 3.45 m and 1.31 m (were 2.2 and 0.81).
        public const float EarlyWaterY = -1.9f, MiddleWaterY = -.55f;
        // The spillway's crest: the reservoir 0.20 m over it (water_tiers.SPILL_HEAD).
        public const float SpillwayCrestY = ReservoirY - .2f;
        const float WeirLineZ = -15.5f;   // the early / middle boundary: the canal walk's middle, carried east and west

        static void Water(CourseBuilder b)
        {
            b.Trigger<KillVolume>("Kill volume (moat, early)", V(-46.5f, EarlyWaterY - 2.2f, -61.5f), V(46.5f, EarlyWaterY - .1f, WeirLineZ));
            b.Trigger<KillVolume>("Kill volume (lake, middle)", V(-46.5f, MiddleWaterY - 2.2f, WeirLineZ), V(46.5f, MiddleWaterY - .1f, 60.5f));
            Pool(b, "moat, early", -46.5f, 46.5f, -61.5f, WeirLineZ, EarlyWaterY);
            Pool(b, "lake, middle", -46.5f, 46.5f, WeirLineZ, 60.5f, MiddleWaterY);
        }

        // R442 (승규 11-05, "기물이 바닥에 떨어졌을 때 물에 풍덩 한 것처럼 해"): a pool catches a piece at its surface. It floats
        // there, its hips 0.2 m under (a WaterZone that reports nothing: the kill volume, 0.1 m under the surface, still
        // ends the try), instead of sinking 11 m out of sight in the 1.5 s before it is put back with the camera after it
        // (from under the surface the sky showed). The camera stays over the water (WaterZone -> OrbitCamera.FloorAt), and
        // the piece splashes where it goes in (GardenSplashPool -> GardenSplashFx). The zones' heights do not overlap where
        // they lie over one another (the lake's over the reservoir's), so a point finds its own pool.
        static void Pool(CourseBuilder b, string name, float x0, float x1, float z0, float z1, float surface)
        {
            b.Trigger<WaterZone>("Water (" + name + ")", V(x0, surface - 2.2f, z0), V(x1, surface, z1)).Report = false;
            const float under = .6f, over = .05f;
            b.Trigger<GardenSplashPool>("Splash (" + name + ")", V(x0, surface - under, z0), V(x1, surface + over, z1))
                .Configure((under - over) * .5f);
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

        // A group whose children move together: a kinematic root driven by CourseObstacleMotion (phase in cycles).
        static CourseObstacleMotion Mover(CourseBuilder b, string name, Vector3 at, MotionKind kind, Vector3 axis, float phaseCycles)
        {
            var root = b.Group(name);
            root.localPosition = at;
            var body = root.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var motion = root.gameObject.AddComponent<CourseObstacleMotion>();
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

        // R474 (승규 "직사각형 모양으로 하지 말고 저런 모양으로", "평면을 더 둥글게, 탑을 그림처럼 길쭉하게", "왼쪽 오른쪽 각각
        // 6개만"): the castle wall round the garden is a ring (ArtSource/PawnRush/GardenConcept/walls_sample_v2.py,
        // GardenWalls.fbx) instead of v31's square walls (x +-46.5..48.5, z -63..62) and four corner towers. An oval
        // (x / 54)^2.4 + ((z - 4.21) / 66.81)^2.4 = 1 through the gate wings' ends and the way in, six slim towers a side
        // evenly along it (the way in's is the sixth), straight walls 2.4 m thick between. These numbers are the Blender
        // sample's and must match it (the east side; the west mirrors it).
        static readonly Vector2[] CastleTowersEast =
        {
            new Vector2(44.405f, 48.592f), new Vector2(52.888f, 23.179f), new Vector2(53.864f, -3.740f),
            new Vector2(49.124f, -30.181f), new Vector2(34.150f, -52.228f), new Vector2(9.500f, -62.168f),
        };
        // Towers r 2.1 (the way in's 2.4) on a foot 0.4 wider, the eave at 17.98; the walls to their merlons' tops.
        const float CastleTowerR = 2.1f, CastleDoorTowerR = 2.4f, CastleFoot = .4f, CastleEave = 17.98f;
        const float CastleWallHalf = 1.2f, CastleWallTop = 12.3f;
        // The first wall leaves the gate wing's low end block on its end plane (x 24.8), its outer face out of the
        // block's outer corner, and reaches 1.05 m back into the block.
        static readonly Vector2 CastleWingStart = new Vector2(24.8f, 66.128f);
        const float CastleWingBack = 1.047f;
        // The bank (scenery) between the lake's edge and the wall where the wall stands out past the water.
        const float CastleBankY = .6f, CastleBankEdge = 45.8f, CastleBankZ0 = -35.08f, CastleBankZ1 = 44.41f;

        static void Shell(CourseBuilder b)
        {
            var k = b.kit;
            // R474: the castle ring (see CastleTowersEast), open where the way comes in between the two towers at the
            // way in (x +-9.5, z -62.2) and goes out through the gate (the wings' ends at x +-24.8).
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0 ? "W" : "E";
                var from = new Vector2(s * CastleWingStart.x, CastleWingStart.y);
                for (int i = 0; i < CastleTowersEast.Length; i++)
                {
                    var to = new Vector2(s * CastleTowersEast[i].x, CastleTowersEast[i].y);
                    var start = i == 0 ? from - (to - from).normalized * CastleWingBack : from;
                    b.SlopeBetween("Castle wall " + side + (i + 1), V(start.x, CastleWallTop, start.y), V(to.x, CastleWallTop, to.y),
                        CastleWallHalf * 2f, CastleWallTop - Deep, 0f, k.wallNoClimb, true);
                    float r = (i == CastleTowersEast.Length - 1 ? CastleDoorTowerR : CastleTowerR) + CastleFoot;
                    b.Cylinder("Castle tower " + side + (i + 1), V(to.x, Deep, to.y), r * 2f, CastleEave - Deep, k.wallNoClimb, true, true);
                    from = to;
                }
                // The bank along the side (under the wall beyond its inner face; past the wall nobody comes).
                Solid(b, "Castle bank " + side, V(s < 0 ? -54.5f : CastleBankEdge, Deep, CastleBankZ0),
                    V(s < 0 ? -CastleBankEdge : 54.5f, CastleBankY, CastleBankZ1));
                // The strip at 2.8 where the old north wall stood: between the gate yard's north rail, the gate terrace's
                // north edge (z 59), the gate's towers and wings and the first wall.
                Solid(b, "Castle strip " + side + " (yard)", V(s < 0 ? -14.3f : 6.3f, Deep, 60.5f), V(s < 0 ? -6.3f : 14.3f, Upper - 1f, 65.3f));
                Solid(b, "Castle strip " + side + " (terrace)", V(s < 0 ? -25.2f : 14.3f, Deep, 59f), V(s < 0 ? -14.3f : 25.2f, Upper - 1f, 65.3f));
                Solid(b, "Castle strip " + side + " (corner)", V(s < 0 ? -31f : 25.2f, Deep, 59f), V(s < 0 ? -25.2f : 31f, Upper - 1f, 63.1f));
            }
            // The water between everything: falling in ends the try (1.5 s, back on the highest checkpoint).
            // R400: the Blender garden's water sheet instead of v31's flat picture; R440: three pools, a kill volume under each.
            Water(b);
            b.Trigger<KillVolume>("Kill volume (far below)", V(-80f, -42f, -90f), V(80f, -40f, 100f));
            // The island in the north-west water and its fountain (scenery).
            b.Cylinder("Garden island", V(-18f, Deep, 16f), 10f, .6f - Deep, k.wallNoClimb, true, true);
            b.Cylinder("Island fountain", V(-18f, .6f, 16f), 2f, 2f, k.glass, false);
            // The terraces either side of the reservoir and the gate yard (scenery, not the way). R474: the castle wall cuts
            // the gate terraces' outer corners (dressed so in GardenWalls); their boxes run on under it, out of reach.
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
