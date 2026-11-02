using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // Mid-game obstacle section 2-1, the grand stair hall (level design v0.3, Docs/KingRush/
    // PAWN_RUSH_GRAND_STAIR_HALL_DESIGN_v0.3.md): an imperial staircase - the grand stair up the middle, the
    // central landing, the stair splitting into west and east wings, the window gallery along the north
    // wall, the royal dais the wings meet on again, the royal balcony and the gate - at the design doc's
    // coordinates (§2, §4). Section frame: origin on the porch's threshold line z 0, +z north (the way in),
    // +x east, y = a floor's top. The section is entered at z -12 and left at z 132 (the mini-game entrance
    // band), both 12 m wide.
    //
    //   Mid_2-1_GrandStairHall (GrandStairHall, PawnRushSection)
    //     Content
    //       B1_GrandStair      porch, foyer and pillars, the drain, the lower flight, the middle landing, the upper
    //                          flight, four rolling holes and the lanes (RollingSpawner)
    //       B2_CentralLanding  the landing, the king statue, the falling chandeliers (ChandelierRain)
    //       Wing_West          the ledge, wing stair 1 (3 chandelier pendulums, no railings), the turning landing
    //       Wing_East            (a C round a rook tower, its sweeper), the moving stair, the window gallery (4 vents whose
    //                            wind carries a pawn), bridge A, the island, bridge B to the dais; east = the mirror
    //                            of west, built by the same code with x -> -x. Since R150 the wings stand 11 m
    //                            further out than the doc's (WingOut) and the central landing reaches them
    //       B5_RoyalDais       the dais, the lift landing (05 tiles x 2), the shaft
    //       B6_RoyalBalcony    the balcony, two knights, the clock gate, the runout, the terrace, the arch, four
    //                          steps down, the mini-game entrance band
    //       Hall               the hall's walls, the respawn floor (y -4) and its kill volume, chandeliers
    //       Checkpoints        CP0 ~ CP5, CP 끝 (CP6)
    //       ProgressPath       the west route ("white") and the east route ("black"), for the HUD and the validator
    //
    // Everything solid is unclimbable (NoClimbSurface): this section is climbed by its stairs and its lift only
    // (§7). The stairs are pictures of steps over an invisible walking slope along the steps' middle line: the
    // ragdoll's hips ride 0.26 m over the ground, so a 0.25 m step is a wall to it and a 14° slope is not.
    // Built inactive and switched on at the end, so nothing inside wakes up half placed.
    public static partial class GrandStairHallBuilder
    {
        public const string DisplayName = "대계단 현관홀";
        public const float HallFloor = -4f, Deep = -5f;
        public static readonly Vector3 Entry = new Vector3(0f, 0f, -12f), Exit = new Vector3(0f, 19f, 132f);

        public static GameObject Build(GrandStairHall hall)
        {
            var kit = hall.Kit;
            var old = hall.transform.Find(GrandStairHall.ContentName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(GrandStairHall.ContentName);
            root.SetActive(false);
            root.transform.SetParent(hall.transform, false);
            var b = new CourseBuilder(kit, root.transform);

            b.In("B1_GrandStair", () => GrandStair(b));
            b.In("B2_CentralLanding", () => CentralLanding(b));
            GameObject westGate = null, eastGate = null;
            var west = b.In("Wing_West", () => westGate = Wing(b, -1f));
            var east = b.In("Wing_East", () => eastGate = Wing(b, 1f));
            GameObject lift = null, liftWall = null;
            b.In("B5_RoyalDais", () => RoyalDais(b, out lift, out liftWall));
            b.In("B6_RoyalBalcony", () => RoyalBalcony(b));
            b.In("Hall", () => Shell(b));
            b.In("Checkpoints", () => Checkpoints(b));
            Path(b);

            hall.Bind(west, east, westGate, eastGate, lift, liftWall);
            var section = hall.GetComponent<PawnRushSection>();
            if (section != null)
                section.Describe(GrandStairHall.Id, DisplayName, PawnRushSection.SlotKind.Obstacle, Entry, Exit, 0f, Beats());
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

        // The walking surface of a flight: the slope stays, its picture goes.
        static GameObject Walk(GameObject slope)
        {
            var renderer = slope.GetComponent<MeshRenderer>();
            if (renderer != null) Object.DestroyImmediate(renderer);
            var filter = slope.GetComponent<MeshFilter>();
            if (filter != null) Object.DestroyImmediate(filter);
            slope.AddComponent<CourseFloor>().sloped = true;
            return slope;
        }

        // A flight rising along +z from zFoot: `steps` steps of `rise` x `depth`, x0..x1 wide. The steps are
        // pictures; the walking slope runs along their middle line, from half a step before the first riser
        // (yFoot) to half a step before the last one (the top) - the last step is the landing's own floor.
        static void FlightZ(CourseBuilder b, string name, float x0, float x1, float zFoot, int steps, float depth, float yFoot, float rise = .25f)
        {
            for (int k = 1; k <= steps; k++)
                Picture(b, name + " step " + k, V(x0, Deep, zFoot + (k - 1) * depth), V(x1, yFoot + k * rise, zFoot + k * depth), b.kit.floorChecker);
            Walk(b.Slope(name + " (walking slope)", x0, x1, zFoot - depth * .5f, zFoot + (steps - .5f) * depth,
                         yFoot, yFoot + steps * rise, .6f, 0f, b.kit.floorChecker, true));
        }

        // The same rising outwards along x (side s = -1 west, +1 east) from |x| = foot.
        static void FlightX(CourseBuilder b, string name, float s, float foot, int steps, float depth, float z0, float z1, float yFoot, float rise = .25f)
        {
            for (int k = 1; k <= steps; k++)
                Picture(b, name + " step " + k, V(s * (foot + (k - 1) * depth), Deep, z0), V(s * (foot + k * depth), yFoot + k * rise, z1), b.kit.floorChecker);
            Walk(b.SlopeX(name + " (walking slope)", z0, z1, s * (foot - depth * .5f), s * (foot + (steps - .5f) * depth),
                          yFoot, yFoot + steps * rise, .6f, 0f, b.kit.floorChecker, true));
        }

        // Height of a flight's walking slope `along` metres past its foot riser.
        static float SlopeY(float along, float depth, float yFoot, float rise = .25f) => yFoot + rise * .5f + rise * along / depth;

        // The highest step top between `a` and `c` metres past the foot riser (k-th step: top yFoot + k rise).
        static float StepTopMax(float a, float c, float depth, float yFoot, float rise = .25f)
        {
            int k = Mathf.CeilToInt(Mathf.Max(a, c) / depth - 1e-4f);
            return yFoot + Mathf.Max(1, k) * rise;
        }

        // A railing along a flight (along z): the walking line + 1.2 on top, deep below.
        static void RailAlongZ(CourseBuilder b, string name, float x0, float x1, float zFoot, int steps, float depth, float yFoot, float rise = .25f)
        {
            float z0 = zFoot - depth * .5f, z1 = zFoot + (steps - .5f) * depth;
            b.Slope(name, x0, x1, z0, z1, yFoot, yFoot + steps * rise, 6f, 1.2f, b.kit.wallNoClimb, true);
        }

        // The same along x.
        static void RailAlongX(CourseBuilder b, string name, float s, float foot, int steps, float depth, float z0, float z1, float yFoot, float rise = .25f)
        {
            b.SlopeX(name, z0, z1, s * (foot - depth * .5f), s * (foot + (steps - .5f) * depth), yFoot, yFoot + steps * rise,
                     6f, 1.2f, b.kit.wallNoClimb, true);
        }

        // An imported obstacle at its doc place, turned (north 0, east 90, south 180, west 270), its phase in
        // seconds on the shared clock.
        static GameObject Hazard(CourseBuilder b, GameObject prefab, string name, Vector3 at, float yaw = 0f, float phaseSeconds = 0f)
        {
            var go = b.Obstacle(prefab, name, at, yaw);
            ObstaclePhase.Shift(go, phaseSeconds);
            return go;
        }

        // How far an obstacle reaches across the way where it stands (for the validator, §7 "우회로 확인").
        static void Span(GameObject go, Vector3 across, float reachMin, float reachMax, float probeY, string group = null)
        {
            if (go == null) return;
            go.AddComponent<ObstacleSpan>().Configure(across, reachMin, reachMax, ObstacleSpan.Probe.Floor, probeY, Vector3.zero, group);
        }

        // A span on an empty marker (a spawner's lanes, a vent's wind across the gallery).
        static void SpanAt(CourseBuilder b, string name, Vector3 at, Vector3 across, float reachMin, float reachMax)
        {
            var marker = b.Group(name);
            marker.localPosition = at;
            marker.gameObject.AddComponent<ObstacleSpan>().Configure(across, reachMin, reachMax, ObstacleSpan.Probe.Floor, at.y, Vector3.zero);
        }

        // Children of a placed obstacle prefab, by name, anywhere below it.
        static void ForChildren(GameObject go, string name, System.Action<Transform> act)
        {
            if (go == null) return;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t != go.transform && t.name == name) act(t);
        }

        // ------------------------------------------------------------------ the hall around it all

        static void Shell(CourseBuilder b)
        {
            var k = b.kit;
            float h = HallHalf;   // 51 since R150 (the doc's 40): the wings stand WingOut further out
            // The hall floor: falling onto it ends the try (1.5 s, back on the highest checkpoint; §8).
            b.Box("Hall floor (respawn)", V(-h, Deep, 12f), V(h, HallFloor, 88f), CourseKit.Or(k.respawnFloor, k.trimBlack), false);
            b.Trigger<KillVolume>("Kill volume (hall floor)", V(-h, HallFloor, 12f), V(h, HallFloor + 1.5f, 88f));
            b.Trigger<KillVolume>("Kill volume (far below)", V(-70f, -42f, -30f), V(70f, -40f, 150f));
            // Walls: west, east and north (the gallery's windows are in it), south beside the foyer.
            Solid(b, "Hall wall W", V(-h - 2f, Deep, 12f), V(-h, 26f, 88f));
            Solid(b, "Hall wall E", V(h, Deep, 12f), V(h + 2f, 26f, 88f));
            Solid(b, "Hall wall N (west)", V(-h, Deep, 86f), V(-8f, 26f, 88f));
            Solid(b, "Hall wall N (east)", V(8f, Deep, 86f), V(h, 26f, 88f));
            Solid(b, "Hall wall S (west)", V(-h, Deep, 12f), V(-21f, 26f, 14f));
            Solid(b, "Hall wall S (east)", V(21f, Deep, 12f), V(h, 26f, 14f));
            // Chandeliers hanging over the landing (pictures; the falling ones are ChandelierRain's own).
            foreach (var at in new[] { V(-9f, 21f, 55f), V(9f, 21f, 55f), V(-9f, 21f, 63f), V(9f, 21f, 63f), V(0f, 22f, 57f),
                                       V(-19f, 21f, 59f), V(19f, 21f, 59f), V(-31f, 22f, 72f), V(31f, 22f, 72f) })
            {
                b.Cylinder("Chandelier ring", at, 2.4f, .12f, k.rankGold, false);
                b.Cylinder("Chandelier column", at, .25f, .7f, k.rankGold, false);
                b.Cylinder("Chandelier chain", at + Vector3.up * .7f, .08f, 26f - at.y, k.rankGold, false);
            }
        }

        // ------------------------------------------------------------------ checkpoints, path, beats

        // §8 (R150 numbering): CP0 porch, CP1 central landing, CP2 turning landing, CP3 gallery start, CP4 before
        // bridge A (west and east), CP5 dais, CP6 (CP 끝) the exit terrace. Wing checkpoints are built with their wings.
        static void Checkpoints(CourseBuilder b)
        {
            b.Checkpoint(0, V(0f, 0f, -6f), 0f, 10f);
            b.Checkpoint(1, V(0f, 8f, 53.5f), 0f, 12f);
            b.Checkpoint(5, V(0f, 14f, 81f), 0f, 10f);
            b.Checkpoint(6, V(0f, 20f, 123f), 0f, 10f);
        }

        // Both routes, the same length (the wings are mirrors): white = by the west wing, black = by the east.
        // R150: the wing's points stand WingOut (11 m) further out; P9 -> P12 is the C round the rook tower on the
        // turning landing, P12 -> P14 the moving stair lined up, P15 -> P16 bridge A, the island and bridge B.
        static readonly Vector3[] WestRoute =
        {
            V(0, 0, -12), V(0, 0, -6), V(0, 0, 13.5f), V(0, 4, 29.5f), V(0, 4, 35.5f), V(0, 8, 51.5f), V(0, 8, 54),     // P0..P6
            V(-14, 8, 60), V(-24.333f, 8, 60), V(-40.333f, 11, 60), V(-42.2f, 11, 56), V(-49.7f, 11, 56),               // P7..P11
            V(-49.7f, 11, 65.5f), V(-48.5f, 11, 66.5f), V(-48.5f, 14, 79.5f), V(-48.5f, 14, 83), V(-24.8f, 14, 83),       // P12..P16
            V(-6, 14, 83), V(-3.35f, 14, 87), V(-3.35f, 14, 91.35f), V(-3.35f, 20, 91.35f), V(0, 20, 96),                // P17..P21
            V(0, 20, 102), V(0, 20, 123), V(0, 20, 126), V(0, 19, 128), V(0, 19, 132),                                   // P22..P26
        };

        static string LegBeat(int leg) =>
            leg <= 5 ? "1" : leg <= 7 ? "2" : leg <= 14 ? "3" : leg <= 15 ? "4" : leg <= 20 ? "5" : "6";

        static void Path(CourseBuilder b)
        {
            var west = (Vector3[])WestRoute.Clone();
            var east = new Vector3[west.Length];
            for (int i = 0; i < west.Length; i++) east[i] = V(-west[i].x, west[i].y, west[i].z);
            var sections = new string[west.Length];
            for (int i = 0; i < sections.Length; i++) sections[i] = LegBeat(i);
            var go = new GameObject("ProgressPath");
            go.transform.SetParent(b.parent, false);
            go.AddComponent<ProgressPath>().Configure(west, east, sections);
        }

        public static SectionBeat[] Beats() => new[]
        {
            new SectionBeat("1 굴러떨어지는 대계단", V(-21f, -2f, -13f), V(21f, 12f, 51f)),
            new SectionBeat("2 중앙 계단참 샹들리에 낙하", V(-LandingHalf, 6f, 51f), V(LandingHalf, 18f, 66f)),
            new SectionBeat("3 갈라지는 날개 계단", V(-HallHalf - 1f, 6f, 51f), V(-LandingHalf, 20f, 80f)),
            new SectionBeat("3 갈라지는 날개 계단", V(LandingHalf, 6f, 51f), V(HallHalf + 1f, 20f, 80f)),
            new SectionBeat("4 창가 회랑 바람", V(-HallHalf - 1f, 12f, 80f), V(-13.8f - WingOut, 22f, 86f)),
            new SectionBeat("4 창가 회랑 바람", V(13.8f + WingOut, 12f, 80f), V(HallHalf + 1f, 22f, 86f)),
            new SectionBeat("5 다리와 승강 계단참", V(-13.8f - WingOut, 10f, 77f), V(13.8f + WingOut, 23f, 94.7f)),
            new SectionBeat("6 왕실 발코니와 대문", V(-9f, 17f, 94.7f), V(9f, 30f, 133f)),
        };
    }
}
