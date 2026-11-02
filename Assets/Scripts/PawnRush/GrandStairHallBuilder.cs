using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // Mid-game obstacle section 2-1, the grand stair hall (level design v0.3, Docs/KingRush/
    // PAWN_RUSH_GRAND_STAIR_HALL_DESIGN_v0.3.md, reshaped by the user in R303): the porch, the rolling grand
    // stair and the chandelier landing of the doc lead into a round hall climbed by one spiral way - a curving
    // stair along the wall, floating stones, a jump pad, gaps to dive across, a windy way, a last stair with
    // falling pieces - up to the royal dais in its middle, then glass stones (one of each pair holds) to the
    // royal balcony, the knights and the gate. Section frame: origin on the porch's threshold line z 0, +z north
    // (the way in), +x east, y = a floor's top. The section is entered at z -12 and left at z 158.3 (the
    // mini-game entrance band), both 12 m wide.
    //
    //   Mid_2-1_GrandStairHall (GrandStairHall, PawnRushSection)
    //     Content
    //       B1_GrandStair      porch, foyer and pillars, the drain, the lower flight, the middle landing, the upper
    //                          flight, four rolling holes and the lanes (RollingSpawner)
    //       B2_CentralLanding  the landing, the king statue, the falling chandeliers (ChandelierRain)
    //       B3_Spiral          the spiral's foot, the curving stair (2 chandeliers), 8 floating stones, the north
    //                          landing, the jump pad, the dive landings, the windy way (2 vents), the last stair
    //                          (2 falling pieces), CP2 ~ CP5
    //       B5_RoyalDais       the dais, CP6, the glass stones (GuessBridge) and the kill volume under them
    //       B6_RoyalBalcony    the balcony, two knights, the clock gate, the runout, the terrace, the arch, four
    //                          steps down, the mini-game entrance band
    //       Hall               the round wall, the walls round the stair, the respawn floor (y -4) and its kill
    //                          volume, chandeliers
    //       Checkpoints        CP0, CP1, CP 끝 (CP7)
    //       ProgressPath       the one route (given as both "white" and "black"), for the HUD and the validator
    //
    // Everything solid is unclimbable (NoClimbSurface). The grand stair's steps are pictures over an invisible
    // walking slope along the steps' middle line: the ragdoll's hips ride 0.26 m over the ground, so a 0.25 m step
    // is a wall to it and a 14° slope is not; the spiral's slopes are gentler and walked as they are drawn.
    // Built inactive and switched on at the end, so nothing inside wakes up half placed.
    public static partial class GrandStairHallBuilder
    {
        public const string DisplayName = "대계단 현관홀";
        public const float HallFloor = -4f, Deep = -5f;
        public static readonly Vector3 Entry = new Vector3(0f, 0f, -12f), Exit = new Vector3(0f, 15f, 158.3f);

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
            b.In("B3_Spiral", () => Spiral(b));
            b.In("B5_RoyalDais", () => RoyalDais(b));
            b.In("B6_RoyalBalcony", () => RoyalBalcony(b));
            b.In("Hall", () => Shell(b));
            b.In("Checkpoints", () => Checkpoints(b));
            Path(b);

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
            const float side = 34f;
            // The hall floor: falling onto it ends the try (1.5 s, back on the highest checkpoint; §8).
            b.Box("Hall floor (respawn)", V(-side, Deep, 12f), V(side, HallFloor, 125f), CourseKit.Or(k.respawnFloor, k.trimBlack), false);
            b.Trigger<KillVolume>("Kill volume (hall floor)", V(-side, HallFloor, 12f), V(side, HallFloor + 1.5f, 125f));
            b.Trigger<KillVolume>("Kill volume (far below)", V(-70f, -42f, -30f), V(70f, -40f, 180f));
            // Round the grand stair: south beside the foyer, west and east up to the round wall's widest point.
            Solid(b, "Hall wall S (west)", V(-side, Deep, 12f), V(-21f, 26f, 14f));
            Solid(b, "Hall wall S (east)", V(21f, Deep, 12f), V(side, 26f, 14f));
            Solid(b, "Hall wall W", V(-side - 2f, Deep, 12f), V(-side, 26f, Hub.y));
            Solid(b, "Hall wall E", V(side, Deep, 12f), V(side + 2f, 26f, Hub.y));
            // The round wall (R303), 6° at a time, 2 m thick outside 31 m: open to the south where the landing comes
            // in (φ 234..306) and to the north for the balcony (φ 72..108).
            for (float phi = 306f; phi < 594f - .01f; phi += 6f)
            {
                float mid = (phi + 3f) % 360f;
                if (mid > 72f && mid < 108f) continue;
                b.SlopeBetween("Hall wall (round)", Polar(phi - .3f, HallRadius + 1f, Deep), Polar(phi + 6.3f, HallRadius + 1f, Deep),
                               2f, 0f, 26f - Deep, k.wallNoClimb, true);
            }
            // Chandeliers hanging over the landing and the dais (pictures; the falling ones are ChandelierRain's own).
            foreach (var at in new[] { V(-9f, 21f, 55f), V(9f, 21f, 55f), V(-9f, 21f, 63f), V(9f, 21f, 63f), V(0f, 22f, 57f), V(0f, 26f, 92f) })
            {
                b.Cylinder("Chandelier ring", at, 2.4f, .12f, k.rankGold, false);
                b.Cylinder("Chandelier column", at, .25f, .7f, k.rankGold, false);
                b.Cylinder("Chandelier chain", at + Vector3.up * .7f, .08f, 30f - at.y, k.rankGold, false);
            }
        }

        // ------------------------------------------------------------------ checkpoints, path, beats

        // §8 (R303 numbering): CP0 porch, CP1 central landing, CP2 end of the curving stair, CP3 north landing, CP4
        // after the dive gaps, CP5 start of the last stair, CP6 the dais, CP7 (CP 끝) the exit terrace. CP2 ~ CP6 are
        // built with the spiral and the dais.
        static void Checkpoints(CourseBuilder b)
        {
            b.Checkpoint(0, V(0f, 0f, -6f), 0f, 10f);
            b.Checkpoint(1, V(0f, 8f, 53.5f), 0f, 12f);
            b.Checkpoint(7, V(0f, 16f, 149.3f), 0f, 10f);
        }

        // The one route (R303: no wings), given as both teams' route: the grand stair, the landing, the spiral's
        // middle line (straight across the jump and the dive gaps), the dais, the left glass squares, the balcony.
        // Each point names the beat of the leg that starts at it.
        static void Path(CourseBuilder b)
        {
            var points = new System.Collections.Generic.List<Vector3>();
            var beats = new System.Collections.Generic.List<string>();
            System.Action<Vector3, string> add = (p, beat) => { points.Add(p); beats.Add(beat); };
            foreach (var p in new[] { V(0, 0, -12), V(0, 0, -6), V(0, 0, 13.5f), V(0, 4, 29.5f), V(0, 4, 35.5f), V(0, 8, 51.5f) }) add(p, "1");
            foreach (var p in new[] { V(0, 8, 54), V(8, 8, 64), V(11.5f, 8, 67.5f) }) add(p, "2");
            for (float phi = 300f; phi <= 372f; phi += 12f) add(OnSpiral(phi, Mathf.Lerp(8f, 10f, (phi - 297f) / 75f)), "3");
            for (int i = 0; i < 8; i++) add(OnSpiral(372f + (i + .5f) * 8.5f, 10f + 1.5f * (i + .5f) / 8f), "3");
            add(OnSpiral(445f, 11.5f), "3");
            add(OnSpiral(460f, 11.5f), "3");
            foreach (float phi in new[] { 476f, 510f, 522f, 533.1f, 548f, 560.3f, 575f }) add(OnSpiral(phi, phi < 500f ? 11.5f : 14f), "4");
            for (float phi = 590f; phi <= 635f; phi += 15f) add(OnSpiral(phi, Mathf.Lerp(14f, 14.8f, (phi - 575f) / 65f)), "4");
            for (float phi = 650f; phi <= 710f; phi += 15f) add(OnSpiral(phi, Mathf.Lerp(14.8f, 16f, (phi - 640f) / 77f)), "4");
            add(OnSpiral(716f, 16f), "4");
            add(Polar(717f, DaisRadius - 1f, DaisTop), "5");
            add(V(-1.5f, DaisTop, Hub.y + DaisRadius - .8f), "5");
            for (int i = 0; i < 7; i++) add(V(-1.5f, DaisTop, Hub.y + DaisRadius + 1.05f + i * 2.85f + .9f), "5");
            foreach (var p in new[] { V(-1.5f, 16, 121.5f), V(0, 16, 124.5f), V(0, 16, 128.3f), V(0, 16, 149.3f), V(0, 16, 152.3f), V(0, 15, 154.3f), V(0, 15, 158.3f) })
                add(p, "6");
            var route = points.ToArray();
            var go = new GameObject("ProgressPath");
            go.transform.SetParent(b.parent, false);
            go.AddComponent<ProgressPath>().Configure(route, (Vector3[])route.Clone(), beats.ToArray());
        }

        // Areas for the HUD and the CSV, the first that holds a pawn wins: the dais and the glass stones before the
        // spiral's upper part (hips over y 13), the lower part last.
        public static SectionBeat[] Beats() => new[]
        {
            new SectionBeat("1 굴러떨어지는 대계단", V(-21f, -2f, -13f), V(21f, 12f, 51f)),
            new SectionBeat("2 중앙 계단참 샹들리에 낙하", V(-14f, 6f, 51f), V(15.5f, 13f, 71.5f)),
            new SectionBeat("5 왕실 단과 유리 징검다리", V(-5.5f, 15f, 86.5f), V(5.5f, 24f, 97.5f)),
            new SectionBeat("5 왕실 단과 유리 징검다리", V(-3f, 13f, 97.5f), V(3f, 24f, BalconyFront)),
            new SectionBeat("6 왕실 발코니와 대문", V(-9f, 13f, BalconyFront), V(9f, 26f, 159f)),
            new SectionBeat("4 점프대·다이빙·바람·떨어지는 말", V(-34f, 13f, 60f), V(34f, 24f, 124f)),
            new SectionBeat("3 휘어 오르는 계단과 떠 있는 발판", V(-34f, 6f, 60f), V(34f, 13f, 124f)),
        };
    }
}
