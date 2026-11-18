using System;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Observatory's graybox (R712), written from the level design v0.1
    // (Docs/PawnRush/PAWN_RUSH_ROYAL_OBSERVATORY_DESIGN_v0.1.md, its tables' coordinates): boxes and the existing
    // devices, their sizes, periods and phases set on the placed instances. Section frame: the entry's threshold is the
    // origin, +z north (the way in), +x east, y the floor's top. One way, no branches:
    //
    //   B0 entry tunnel (z 0..12) -> B1 ring court (north) -> SW landing -> B2 star-chart belts (east) -> SE landing
    //   -> B3 vent grating bridge (north) -> NE landing -> B4 the orrery's turntable (west) -> W landing
    //   -> B5 planet lifts (north, up to y 6) -> NW landing -> B6 telescope bridge (east) -> the observation platform
    //   -> B7 constellation wall (north, up to y 10.5) -> terrace, steps, run-out -> exit (50, 11.5, 132)
    //
    // Everything that moves is a pure function of the shared obstacle clock (ObstacleMotion, the imported hazards,
    // TelescopeRam), so the section sends nothing in a match. Where the graybox departs from the design's first
    // numbers it says so beside the beat (the design document lists them under "회색 상자에서 바꾼 것").
    public static class RoyalObservatoryBuilder
    {
        public const float Upper = 6f, Terrace = 10.5f, ExitTop = 11.5f, HallFloor = -18f;
        public static readonly Vector3 Entry = Vector3.zero, Exit = new Vector3(50f, ExitTop, 132f);

        const float RailHeight = 1.1f, T = .3f, CourtHalf = 6.2f;
        // B2: the two belt pairs' and the two shield pairs' middles along x.
        const float Belt1 = 17.5f, Shield1 = 25.1f, Belt2 = 32.7f, Shield2 = 40.35f;
        // B3 and B7: the winds' cycle and how fast they carry a pawn.
        const float WindDrift = 4.5f, WindWarn = .8f, WindBlow = 2.2f;
        // B4: the turntable.
        static readonly Vector3 Hub = new Vector3(22f, 0f, 77f);
        const float DiscRadius = 9f;
        // B5: the lifts.
        const float LiftPeriod = 5f, LiftTravel = 1.475f;
        // B6: the rams' cycle (rest, warning, thrust, hold, back = 4.5 s) and the time between neighbours.
        const float RamRest = 1.6f, RamWarn = 1f, RamThrust = .5f, RamHold = .5f, RamBack = .9f, RamStagger = 2.2f;
        // B7: three tiers of 1.5 m, two ledges 1.2 m deep.
        const float Tier = 1.5f, Ledge = 1.2f, WallZ = 109.4f;

        public static GameObject Build(RoyalObservatorySection observatory)
        {
            var kit = observatory != null ? observatory.Kit : null;
            if (kit == null)
            {
                Debug.LogError("[PawnRush] The Royal Observatory has no course kit: nothing built.");
                return null;
            }
            var root = new GameObject(RoyalObservatorySection.ContentName);
            root.SetActive(false);
            root.transform.SetParent(observatory.transform, false);
            var b = new CourseBuilder(kit, root.transform);

            b.In("B0 Entry tunnel", () => EntryTunnel(b));
            b.In("B1 Ring court", () => RingCourt(b));
            b.In("B2 Star-chart belts and shutter shields", () => Belts(b));
            b.In("B3 Vent grating bridge", () => VentBridge(b));
            b.In("B4 Orrery turntable", () => Orrery(b));
            b.In("B5 Planet lifts", () => Lifts(b));
            b.In("B6 Telescope bridge", () => TelescopeBridge(b));
            b.In("B7 Constellation wall", () => ConstellationWall(b));
            b.In("Exit terrace", () => ExitTerrace(b));
            b.In("Hall (looks)", () => Hall(b));
            b.In("KillVolumes", () => b.Trigger<KillVolume>("Kill volume (y -10)", V(-40f, -12f, -20f), V(90f, -10f, 160f)));
            Path(b);

            var section = observatory.GetComponent<PawnRushSection>();
            if (section != null)
                section.Describe(RoyalObservatorySection.Id, RoyalObservatorySection.DisplayName, PawnRushSection.SlotKind.Obstacle,
                                 Entry, Exit, 0f, Beats());
            root.SetActive(true);
            return root;
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // ------------------------------------------------------------------ the vocabulary

        static void Under(CourseBuilder b, Transform under, Action body)
        {
            var before = b.parent;
            b.parent = under;
            try { body(); }
            finally { b.parent = before; }
        }

        // A rail 0.3 m thick outside a floor's edge: along x at the z edge (north: beyond it), or along z at the x edge.
        static void RailX(CourseBuilder b, string name, float x0, float x1, float z, bool north, float floor) =>
            b.Rail(name, x0, x1, north ? z : z - T, north ? z + T : z, floor, RailHeight);

        static void RailZ(CourseBuilder b, string name, float x, bool east, float z0, float z1, float floor) =>
            b.Rail(name, east ? x : x - T, east ? x + T : x, z0, z1, floor, RailHeight);

        // An imported obstacle, turned to its direction of travel (north 0, east 90, south 180, west 270), with its
        // phase in seconds on the shared clock.
        static GameObject Hazard(CourseBuilder b, GameObject prefab, string name, Vector3 at, float yaw = 0f, float phaseSeconds = 0f)
        {
            var go = b.Obstacle(prefab, name, at, yaw);
            ObstaclePhase.Shift(go, phaseSeconds);
            return go;
        }

        static void ForChildren(GameObject go, string name, Action<Transform> act)
        {
            if (go == null) return;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t != go.transform && t.name == name) act(t);
        }

        // An empty device root carrying an ObstacleMotion (its Rigidbody comes with it).
        static ObstacleMotion Mover(CourseBuilder b, string name, Vector3 at, MotionKind kind, Vector3 axis)
        {
            var root = b.Group(name);
            root.localPosition = at;
            var motion = root.gameObject.AddComponent<ObstacleMotion>();
            motion.kind = kind;
            motion.axis = axis;
            motion.useLocalTranslationAxes = false;
            return motion;
        }

        // A bar spinning about its middle, 0.2 to 0.52 m up: jumped (course 01's and the war room's bar).
        static void SpinBar(CourseBuilder b, string name, Vector3 hub, float length, float degreesPerSecond)
        {
            var motion = Mover(b, "Device_SpinBar " + name, hub + Vector3.up * .65f, MotionKind.Rotate, Vector3.up);
            motion.degreesPerSecond = degreesPerSecond;
            Under(b, motion.transform, () =>
            {
                b.Box("Bar", V(-length * .5f, -.45f, -.225f), V(length * .5f, -.13f, .225f), b.kit.hazard);
                b.Cylinder("Hub", V(0f, -.65f, 0f), .85f, .9f, b.kit.rankGold, false);
            });
        }

        // One arm over the heads of pawns standing (1.35 to 1.7 m): it passes over a runner and strikes one who jumps.
        static void HighBar(CourseBuilder b, string name, Vector3 hub, float reach, float degreesPerSecond)
        {
            var motion = Mover(b, "Device_HighBar " + name, hub, MotionKind.Rotate, Vector3.up);
            motion.degreesPerSecond = degreesPerSecond;
            Under(b, motion.transform, () =>
            {
                b.Box("High bar", V(.3f, 1.35f, -.2f), V(reach, 1.7f, .2f), b.kit.hazard);
                b.Box("Mast", V(-.2f, .9f, -.2f), V(.2f, 1.9f, .2f), b.kit.rankGold, false, false);
            });
        }

        // 15, its wind made a box `wide` across, `high` up and `deep` out from its mouth, blowing a pawn along at
        // WindDrift (the grand stair hall's vents carry at 3.5). `rest` seconds between gusts.
        static GameObject Wind(CourseBuilder b, string name, Vector3 at, float yaw, float phaseSeconds, float rest, float wide, float high, float deep)
        {
            var vent = Hazard(b, b.kit.airVent, name, at, yaw, phaseSeconds);
            var air = vent != null ? vent.GetComponentInChildren<AirVent>(true) : null;
            if (air == null) return vent;
            air.DriftSpeed = WindDrift;
            air.IdleDuration = rest;
            air.WarningDuration = WindWarn;
            air.BlowDuration = WindBlow;
            if (air.WindTrigger != null)
            {
                air.WindTrigger.transform.localPosition = new Vector3(0f, high * .5f, .2f + deep * .5f);
                air.WindTrigger.center = Vector3.zero;
                air.WindTrigger.size = new Vector3(wide, high, deep);
            }
            // The drawn streams reach as far as the wind does.
            ForChildren(vent, "Air stream", t =>
            {
                t.localPosition = new Vector3(t.localPosition.x, t.localPosition.y, .2f + deep * .5f);
                t.localScale = new Vector3(t.localScale.x, t.localScale.y, deep);
            });
            ForChildren(vent, "Arrow tip", t => t.localPosition = new Vector3(t.localPosition.x, t.localPosition.y, deep));
            return vent;
        }

        // ------------------------------------------------------------------ B0, B1

        static void EntryTunnel(CourseBuilder b)
        {
            b.Floor("Entry floor", -6f, 6f, 0f, 12f);
            RailZ(b, "Entry rail W", -6f, false, 0f, 12f, 0f);
            RailZ(b, "Entry rail E", 6f, true, 0f, 12f, 0f);
            b.Checkpoint(0, V(0f, 0f, 6f), 0f, 10f);
        }

        // Two ring devices sweep the whole court (the bars are as long as the court is wide; rails either side). Ring 1:
        // a low bar. Ring 2: a low bar the other way and a high bar over it - jump the one, not under the other.
        static void RingCourt(CourseBuilder b)
        {
            b.Floor("Ring court floor", -CourtHalf, CourtHalf, 12f, 38f);
            RailZ(b, "Ring court rail W", -CourtHalf, false, 12f, 38f, 0f);
            RailZ(b, "Ring court rail E", CourtHalf, true, 12f, 38f, 0f);
            foreach (float z in new[] { 18.4f, 31.6f })
                b.Cylinder("Ring inlay " + z, V(0f, .005f, z), CourtHalf * 2f, .02f, b.kit.boardDark, false);
            SpinBar(b, "B1 ring 1", V(0f, 0f, 18.4f), CourtHalf * 2f, 100f);
            SpinBar(b, "B1 ring 2", V(0f, 0f, 31.6f), CourtHalf * 2f, -100f);
            HighBar(b, "B1 ring 2", V(0f, 0f, 31.6f), CourtHalf, 55f);
        }

        // ------------------------------------------------------------------ B2

        // East along z 38..50. Two pairs of belts, each pair one lane onward and one back (1.5 m/s), the lanes swapped in
        // the second pair; after each pair a pair of shutter shields (06 for a 12 m way, as the war room's bookshelves:
        // each closes half the way in turn, 4.5 s), and the half the onward lane leads to is the one that closes first.
        // Graybox: the belts are 8 m (the prefab's own length; the design said 10) and a shield pair takes 6.6 m of the
        // way (the prefab's two 3 m walls), so the pairs stand at x 25.1 and 40.35.
        static void Belts(CourseBuilder b)
        {
            var k = b.kit;
            b.Floor("SW landing", -CourtHalf, 12f, 38f, 50f);
            RailZ(b, "SW landing rail W", -CourtHalf, false, 38f, 50f, 0f);
            RailX(b, "SW landing rail N", -CourtHalf - T, 12f, 50f, true, 0f);
            RailX(b, "SW landing rail S", CourtHalf + T, 12f, 38f, false, 0f);
            b.Checkpoint(1, V(11f, 0f, 44f), 90f, 12f);

            b.Floor("Belt bridge floor a", 12f, Belt1 - 4f, 38f, 50f);
            b.Floor("Belt bridge floor b", Belt1 + 4f, Belt2 - 4f, 38f, 50f);
            b.Floor("Belt bridge floor c", Belt2 + 4f, 44f, 38f, 50f);
            // 08 is 6 x 8, its top 0.4 over its root, and it carries toward its own -z: facing west (270) it carries east.
            Hazard(b, k.conveyor, "08 Belt 1 N (onward)", V(Belt1, -.4f, 47f), 270f);
            Hazard(b, k.conveyor, "08 Belt 1 S (back)", V(Belt1, -.4f, 41f), 90f);
            Hazard(b, k.conveyor, "08 Belt 2 N (back)", V(Belt2, -.4f, 47f), 90f);
            Hazard(b, k.conveyor, "08 Belt 2 S (onward)", V(Belt2, -.4f, 41f), 270f);
            b.Box("Belt bed 1", V(Belt1 - 4f, -2f, 38f), V(Belt1 + 4f, -1f, 50f), k.trimBlack, false, false);
            b.Box("Belt bed 2", V(Belt2 - 4f, -2f, 38f), V(Belt2 + 4f, -1f, 50f), k.trimBlack, false, false);

            Shields(b, "1", Shield1, false, 0f);
            Shields(b, "2", Shield2, true, 2.25f);
            // The rails stop where a shield slides through (it fills the gap itself, at rest and out).
            RailX(b, "Belt rail N 0", 12f, Shield1 - 3.3f, 50f, true, 0f);
            RailX(b, "Belt rail N 1", Shield1 - .3f, Shield2 + .3f, 50f, true, 0f);
            RailX(b, "Belt rail N 2", Shield2 + 3.3f, 44f, 50f, true, 0f);
            RailX(b, "Belt rail S 0", 12f, Shield1 + .3f, 38f, false, 0f);
            RailX(b, "Belt rail S 1", Shield1 + 3.3f, Shield2 - 3.3f, 38f, false, 0f);
            RailX(b, "Belt rail S 2", Shield2 - .3f, 44f, 38f, false, 0f);

            b.Floor("SE landing", 44f, 56f, 38f, 50f);
            RailX(b, "SE landing rail S", 44f, 56f + T, 38f, false, 0f);
            RailZ(b, "SE landing rail E", 56f, true, 38f, 50f, 0f);
            RailX(b, "SE landing rail N w", 44f, 47f, 50f, true, 0f);
            RailX(b, "SE landing rail N e", 53f, 56f + T, 50f, true, 0f);
            b.Checkpoint(2, V(50f, 0f, 49f), 0f, 10f);
        }

        // 06 turned east (its "left" wall is then the north one, met first). `southFirst`: the pair mirrored along the
        // way, so the south wall is met first.
        static void Shields(CourseBuilder b, string name, float x, bool southFirst, float phaseSeconds)
        {
            var go = Hazard(b, b.kit.slidingWalls, "06 Shutter shields " + name, V(x, 0f, 44f), 90f, phaseSeconds);
            foreach (Transform child in go.transform)
            {
                bool left = child.name == "Left sliding wall", right = child.name == "Right sliding wall";
                var p = child.localPosition;
                if (left || right)
                {
                    child.localPosition = new Vector3(left ? -9f : 9f, p.y, southFirst ? -p.z : p.z);
                    var motion = child.GetComponent<ObstacleMotion>();
                    if (motion != null) motion.distance = 6f;
                    var wall = child.Find("Rook wall");
                    if (wall != null) wall.localScale = new Vector3(6f, wall.localScale.y, wall.localScale.z);
                }
                else if (child.name == "Wall housing")
                    child.localPosition = new Vector3(Mathf.Sign(p.x) * 12.3f, p.y, southFirst ? -p.z : p.z);
            }
        }

        // ------------------------------------------------------------------ B3

        // North along x 47..53, no rails, a floor seen through. Three winds from the east blow across it (each 4 m of the
        // way, the bridge's whole width), 1.7 s after one another: rest 2.0, warning 0.8, wind 2.2 (5.0 s).
        static void VentBridge(CourseBuilder b)
        {
            var deck = b.Floor("Grating bridge", 47f, 53f, 50f, 71f);
            if (b.kit.glass != null) deck.GetComponent<MeshRenderer>().sharedMaterial = b.kit.glass;
            float[] zs = { 55f, 62f, 69f };
            for (int i = 0; i < zs.Length; i++)
            {
                Wind(b, "15 Side wind " + (i + 1), V(53.3f, 0f, zs[i]), 270f, 1.7f * i, 2f, 4f, 3f, 6.3f);
                b.Wall("Vent pier " + (i + 1), 53.3f, 54.6f, HallFloor, 0f, zs[i] - 1.3f, zs[i] + 1.3f, false);
                // Where a wind crosses, on the floor.
                b.Box("Wind mark " + (i + 1), V(47f, .005f, zs[i] - 2f), V(53f, .02f, zs[i] + 2f), b.kit.marker, false, false);
            }

            b.Floor("NE landing", 44f, 56f, 71f, 83f);
            RailX(b, "NE landing rail S w", 44f - T, 47f, 71f, false, 0f);
            RailX(b, "NE landing rail S e", 53f, 56f + T, 71f, false, 0f);
            RailZ(b, "NE landing rail E", 56f, true, 71f, 83f, 0f);
            RailX(b, "NE landing rail N", 44f - T, 56f + T, 83f, true, 0f);
            RailZ(b, "NE landing rail W s", 44f, false, 71f, 74f, 0f);
            RailZ(b, "NE landing rail W n", 44f, false, 80f, 83f, 0f);
            b.Checkpoint(3, V(45f, 0f, 77f), 270f, 10f);
        }

        // ------------------------------------------------------------------ B4

        // West across the great orrery's turntable: a disc 18 m across turning 15 deg/s, no rails; a low arm through the
        // hub the other way at 60 deg/s (jumped; to a rider every 2.4 s) and an arm carrying a 2 m globe round the rim
        // at 70 deg/s (not jumped: it sweeps the rim, where the ways on and off are). The hub's pillar stands in the
        // middle, so the way goes round it.
        static void Orrery(CourseBuilder b)
        {
            var k = b.kit;
            b.Floor("Orrery approach", 31f, 44f, 74f, 80f);
            RailX(b, "Approach rail N", 32f, 44f, 80f, true, 0f);
            RailX(b, "Approach rail S", 32f, 44f, 74f, false, 0f);

            var disc = b.Cylinder("Device_OrreryTurntable", Hub - Vector3.up * .5f, DiscRadius * 2f, .5f, k.boardDark);
            disc.AddComponent<CourseFloor>();
            var turn = disc.AddComponent<ObstacleMotion>();
            turn.kind = MotionKind.Rotate;
            turn.axis = Vector3.up;
            turn.degreesPerSecond = 15f;
            // Spokes that turn with it, so its turning is seen.
            Under(b, disc.transform, () =>
            {
                for (int i = 0; i < 8; i++)
                {
                    var spoke = b.Group("Spoke " + (i + 1));
                    spoke.localRotation = Quaternion.Euler(0f, i * 45f, 0f);
                    Under(b, spoke, () => b.Box("Line", V(.09f, 1f, -.008f), V(.49f, 1.04f, .008f), k.rankGold, false, false));
                }
            });

            b.Cylinder("Orrery hub pillar", Hub, 3f, 3.2f, k.wallNoClimb, true, true);
            SpinBar(b, "B4 low arm", Hub, DiscRadius * 2f + .4f, -60f);
            var globe = Mover(b, "Device_GlobeArm", Hub, MotionKind.Rotate, Vector3.up);
            globe.degreesPerSecond = 70f;
            Under(b, globe.transform, () =>
            {
                b.Box("Arm", V(1.5f, 1.8f, -.15f), V(7.5f, 2.1f, .15f), k.rankGold, false, false);
                b.Box("Drop rod", V(7.4f, 1.2f, -.1f), V(7.6f, 1.8f, .1f), k.rankGold, false, false);
                b.Sphere("Globe", V(7.5f, 1.2f, 0f), 2f, k.hazard);
            });

            b.Floor("W landing", 1f, 13f, 71f, 83f);
            RailX(b, "W landing rail S", 1f - T, 13f, 71f, false, 0f);
            RailZ(b, "W landing rail W", 1f, false, 71f, 83f, 0f);
            RailX(b, "W landing rail N w", 1f - T, 4f, 83f, true, 0f);
            RailX(b, "W landing rail N e", 10f, 13f, 83f, true, 0f);
            RailZ(b, "W landing rail E s", 13f, true, 71f, 73f, 0f);
            RailZ(b, "W landing rail E n", 13f, true, 81f, 83f, 0f);
            b.Checkpoint(4, V(7f, 0f, 82f), 0f, 10f);
        }

        // ------------------------------------------------------------------ B5

        // North and up over the drop: lift A (0 to 3), a fixed ledge at 3 with a planet weight (11) swinging across it,
        // lift B (3 to 6). 5 s each; B is at its foot 2.25 s after A is at its top. A fall is the drop (back to CP4).
        // Graybox: the design had three lifts of 2 m over a floor with the weight over the middle lift - a weight cannot
        // swing over a deck that rises through it, and a deck coming down on a floor closes on whoever stands there, so
        // it is two lifts either side of a fixed ledge, over the drop.
        static void Lifts(CourseBuilder b)
        {
            var k = b.kit;
            Lift(b, "A", 83.2f, 88.2f, 1.525f, 0f);
            // The ledge: a slab at y 3.
            b.Floor("Lift ledge", 4f, 10f, 88.4f, 92.4f, 3f);
            var weight = Hazard(b, k.pendulum, "11 Planet weight", V(7f, 3f, 90.4f), 0f, 0f);
            var swing = weight != null ? weight.GetComponentInChildren<PendulumSwing>(true) : null;
            if (swing != null) swing.SwingDuration = 1.15f;   // the grand stair hall's hammers since R680
            ForChildren(weight, "Swing boundary", t => t.gameObject.SetActive(false));
            b.Wall("Weight frame post W", .2f, .8f, HallFloor, 14.3f, 90.1f, 90.7f, false);
            b.Wall("Weight frame post E", 13.2f, 13.8f, HallFloor, 14.3f, 90.1f, 90.7f, false);
            b.Wall("Weight frame beam", .2f, 13.8f, 13.8f, 14.3f, 90.1f, 90.7f, false);
            // B's wave is at its lowest 3.75 s into its cycle and A's at its highest 1.25 s in: 0.25 s on puts B at its
            // foot 2.25 s after A's top (the ledge is 4 m, 1.6 s at a run, and the weight to wait for).
            Lift(b, "B", 92.6f, 97.6f, 4.475f, .25f);

            b.Floor("NW landing", 1f, 13f, 97.8f, 108f, Upper);
            RailZ(b, "NW landing rail W", 1f, false, 97.8f, 108f, Upper);
            RailX(b, "NW landing rail N", 1f - T, 13f + T, 108f, true, Upper);
            RailX(b, "NW landing rail S w", 1f - T, 4f, 97.8f, false, Upper);
            RailX(b, "NW landing rail S e", 10f, 13f + T, 97.8f, false, Upper);
            RailZ(b, "NW landing rail E s", 13f, true, 97.8f, 100f, Upper);
            RailZ(b, "NW landing rail E n", 13f, true, 106f, 108f, Upper);
            b.Checkpoint(5, V(12f, Upper, 103f), 90f, 8f);
        }

        // A deck 6 x 5 m riding up and down LiftTravel either side of `middle` (its top), on a column from the hall floor.
        static void Lift(CourseBuilder b, string name, float z0, float z1, float middle, float phaseSeconds)
        {
            float z = (z0 + z1) * .5f, half = (z1 - z0) * .5f;
            var motion = Mover(b, "Device_PlanetLift " + name, V(7f, middle, z), MotionKind.Translate, Vector3.up);
            motion.distance = LiftTravel;
            motion.period = LiftPeriod;
            motion.shove = false;
            Under(b, motion.transform, () =>
            {
                b.Box("Deck", V(-3f, -.4f, -half), V(3f, 0f, half), b.kit.boardLight).AddComponent<CourseFloor>();
                b.Box("Deck rim", V(-3f, -.5f, -half), V(3f, -.4f, half), b.kit.rankGold, false, false);
                b.Box("Piston", V(-.5f, -8f, -.5f), V(.5f, -.5f, .5f), b.kit.rankGold, false, false);
            });
            ObstaclePhase.Shift(motion.gameObject, phaseSeconds);
            b.Box("Lift column " + name, V(6.2f, HallFloor, z - .8f), V(7.8f, middle - LiftTravel - 2f, z + .8f), b.kit.trimBlack, false, false);
        }

        // ------------------------------------------------------------------ B6

        // East along z 100..106 at y 6, a rail on the north side only: the south is the drop. Three telescope rams 4 m
        // wide thrust from the north across the whole bridge, 2.2 s after one another (the time a sprint takes from one
        // to the next); between them 4 m pockets.
        static void TelescopeBridge(CourseBuilder b)
        {
            b.Floor("Telescope bridge", 13f, 43f, 100f, 106f, Upper);
            float[] xs = { 19f, 27f, 35f };
            RailX(b, "Bridge rail N 0", 13f, xs[0] - 2f, 106f, true, Upper);
            RailX(b, "Bridge rail N 1", xs[0] + 2f, xs[1] - 2f, 106f, true, Upper);
            RailX(b, "Bridge rail N 2", xs[1] + 2f, xs[2] - 2f, 106f, true, Upper);
            RailX(b, "Bridge rail N 3", xs[2] + 2f, 43f, 106f, true, Upper);
            for (int i = 0; i < xs.Length; i++) Ram(b, i + 1, xs[i], RamStagger * i);
        }

        // A block 4 m wide, 1.6 m high and 6.6 m long, its face 0.2 m north of the bridge at rest and 6.2 m on (0.2 m
        // past the south edge) at full reach, so the whole width is closed while it is out.
        static void Ram(CourseBuilder b, int number, float x, float phaseSeconds)
        {
            var k = b.kit;
            var mark = b.Box("Ram " + number + " warning", V(x - 2f, Upper + .005f, 100f), V(x + 2f, Upper + .03f, 106f), k.marker, false, false);
            var root = b.Group("Device_TelescopeRam " + number);
            root.localPosition = V(x, Upper, 106.2f);
            root.localRotation = Quaternion.Euler(0f, 180f, 0f);   // its forward is south, across the bridge
            Under(b, root, () =>
            {
                b.Box("Ram", V(-2f, .05f, -6.6f), V(2f, 1.65f, 0f), k.hazard);
                b.Box("Lens", V(-1.6f, .25f, 0f), V(1.6f, 1.45f, .06f), k.rankGold, false, false);
                b.Box("Barrel", V(-.8f, 1.65f, -6.6f), V(.8f, 2.6f, -.6f), k.boardDark, false, false);
            });
            root.gameObject.AddComponent<TelescopeRam>().Configure(6.4f, RamRest, RamWarn, RamThrust, RamHold, RamBack, mark, phaseSeconds);
            b.Box("Ram mount " + number, V(x - 2.4f, Upper - 1f, 106.3f), V(x + 2.4f, Upper, 113f), k.trimBlack, false, false);
        }

        // ------------------------------------------------------------------ B7

        // The observation platform (the design's round one, square in the graybox) and the wall to the terrace: three
        // tiers of 1.5 m to climb, two ledges 1.2 m deep between them, a wind from the wall behind each ledge blowing
        // out over it (rest 2.2, warning 0.8, wind 2.2), the two half a cycle apart. A pawn on a ledge when its wind
        // blows is carried off it, down a tier.
        // Graybox: the design had two tiers of 2.25 m and winds blowing down the faces. A wind does not move a pawn that
        // is climbing (RagdollPawn.Blow), so the winds are on the ledges, where a pawn stands.
        static void ConstellationWall(CourseBuilder b)
        {
            b.Floor("Observation platform", 43f, 57f, 96f, WallZ, Upper);
            RailZ(b, "Platform rail W s", 43f, false, 96f, 100f, Upper);
            RailZ(b, "Platform rail W n", 43f, false, 106f, WallZ, Upper);
            RailX(b, "Platform rail S", 43f - T, 57f + T, 96f, false, Upper);
            RailZ(b, "Platform rail E", 57f, true, 96f, WallZ, Upper);
            b.Checkpoint(6, V(44.5f, Upper, 103f), 90f, 8f);

            float y1 = Upper + Tier, y2 = Upper + Tier * 2f, z1 = WallZ + Ledge, z2 = WallZ + Ledge * 2f;
            b.Wall("Constellation tier 1", 44f, 56f, Upper - 1f, y1, WallZ, z1, true, true);
            b.Wall("Constellation tier 2", 44f, 56f, Upper - 1f, y2, z1, z2, true, true);
            b.Wall("Constellation tier 3", 44f, 56f, Upper - 1f, Terrace, z2, 118f, true, true);
            // Walls either side: no way round the tiers, nor off a ledge's end.
            b.Wall("Tier cheek W", 43f, 44f, Upper - 1f, Terrace + 2f, WallZ, 120f, false);
            b.Wall("Tier cheek E", 56f, 57f, Upper - 1f, Terrace + 2f, WallZ, 120f, false);

            // Each wind's mouth is in the face behind its ledge, its housing inside the tier (drawn at just over half its
            // height, to stay under the tier's top).
            var low = Wind(b, "15 Ledge wind 1", V(50f, y1, z1), 180f, 0f, 2.2f, 12f, 2.4f, Ledge + .5f);
            var high = Wind(b, "15 Ledge wind 2", V(50f, y2, z2), 180f, 2.6f, 2.2f, 12f, 2.4f, Ledge + .3f);
            foreach (var vent in new[] { low, high })
                ForChildren(vent, "VentModel", t => t.localScale = new Vector3(1f, .55f, 1f));
        }

        static void ExitTerrace(CourseBuilder b)
        {
            b.Checkpoint(7, V(50f, Terrace, 114.5f), 0f, 12f);
            // Four steps of 0.25 m, then the run-out to the arch.
            for (int i = 0; i < 4; i++)
                b.Wall("Exit step " + (i + 1), 44f, 56f, Terrace - 1f, Terrace + .25f * (i + 1), 118f + .5f * i, 118.5f + .5f * i, false, true);
            b.Floor("Exit run-out", 44f, 56f, 120f, Exit.z, ExitTop);
            RailZ(b, "Run-out rail W", 44f, false, 120f, Exit.z, ExitTop);
            RailZ(b, "Run-out rail E", 56f, true, 120f, Exit.z, ExitTop);
            // The arch's posts (looks): 12 m between them, 6 m high.
            b.Box("Exit arch post W", V(43.2f, ExitTop, Exit.z - .6f), V(44f, ExitTop + 6f, Exit.z), b.kit.trimWhite, false, false);
            b.Box("Exit arch post E", V(56f, ExitTop, Exit.z - .6f), V(56.8f, ExitTop + 6f, Exit.z), b.kit.trimWhite, false, false);
            b.Box("Exit arch lintel", V(43.2f, ExitTop + 6f, Exit.z - .6f), V(56.8f, ExitTop + 6.8f, Exit.z), b.kit.trimWhite, false, false);
        }

        // ------------------------------------------------------------------ the hall round it (looks only)

        // The size of the place, nothing collided with: the hall's floor 18 m under the way, its walls to 30 m over it
        // (the south one open at the tunnel, the north one at the exit), and the great orrery - a spire through the
        // turntable, two rings tumbling slowly round it over the heads, planet globes hung in the drop. The walls
        // stand 6 m outside the design's 70 x 99 m on the west, east and north: they are drawn, not collided with,
        // and a camera following a pawn along an outer landing has to stay inside them.
        static void Hall(CourseBuilder b)
        {
            var k = b.kit;
            const float top = 30f, West = -16f, East = 66f, North = 114f;
            b.Box("Hall floor", V(West, HallFloor - 1f, 12f), V(East, HallFloor, North), k.trimBlack, false, false);
            b.Box("Hall wall W", V(West - .5f, HallFloor, 12f), V(West, top, North), k.trimWhite, false, false);
            b.Box("Hall wall E", V(East, HallFloor, 12f), V(East + .5f, top, North), k.trimWhite, false, false);
            b.Box("Hall wall S w", V(West, HallFloor, 11.5f), V(-6.6f, top, 12f), k.trimWhite, false, false);
            b.Box("Hall wall S e", V(6.6f, HallFloor, 11.5f), V(East, top, 12f), k.trimWhite, false, false);
            b.Box("Hall wall S over the tunnel", V(-6.6f, 6f, 11.5f), V(6.6f, top, 12f), k.trimWhite, false, false);
            b.Box("Hall wall N w", V(West, HallFloor, North), V(43f, top, North + .5f), k.trimWhite, false, false);
            b.Box("Hall wall N e", V(57f, HallFloor, North), V(East, top, North + .5f), k.trimWhite, false, false);

            b.Cylinder("Orrery pedestal", V(Hub.x, HallFloor, Hub.z), 4f, -HallFloor - .5f, k.trimBlack, false);
            b.Cylinder("Orrery spire", V(Hub.x, 3.2f, Hub.z), 1f, 22f, k.rankGold, false);
            b.Sphere("Orrery sun", V(Hub.x, 16f, Hub.z), 4f, k.rankGold, false);
            Ring(b, "Orrery ring 1", V(Hub.x, 16f, Hub.z), 11f, Quaternion.Euler(0f, 0f, 25f), Vector3.right, 8f);
            Ring(b, "Orrery ring 2", V(Hub.x, 16f, Hub.z), 9.5f, Quaternion.Euler(35f, 40f, 0f), Vector3.forward, -6f);
            var planets = new[]
            {
                (V(34f, -6f, 60f), 3.5f), (V(8f, -9f, 60f), 2.5f), (V(36f, -4f, 92f), 2f),
                (V(30f, 12f, 52f), 3f), (V(-2f, 10f, 92f), 2.2f), (V(52f, 14f, 90f), 2.8f),
            };
            for (int i = 0; i < planets.Length; i++)
                b.Sphere("Planet globe " + (i + 1), planets[i].Item1, planets[i].Item2, (i & 1) == 0 ? k.boardLight : k.boardDark, false);
        }

        // A ring of 24 boxes turning slowly about one of its own diameters (looks: no colliders).
        static void Ring(CourseBuilder b, string name, Vector3 center, float radius, Quaternion tilt, Vector3 axis, float degreesPerSecond)
        {
            var motion = Mover(b, name, center, MotionKind.Rotate, axis);
            motion.transform.localRotation = tilt;
            motion.degreesPerSecond = degreesPerSecond;
            motion.shove = false;
            const int Segments = 24;
            float chord = 2f * radius * Mathf.Sin(Mathf.PI / Segments) + .05f;
            Under(b, motion.transform, () =>
            {
                for (int i = 0; i < Segments; i++)
                {
                    var piece = b.Group("Segment " + (i + 1));
                    piece.localRotation = Quaternion.Euler(0f, i * 360f / Segments, 0f);
                    Under(b, piece, () => b.Box("Arc", V(-chord * .5f, -.2f, radius - .2f), V(chord * .5f, .2f, radius + .2f), b.kit.rankGold, false, false));
                }
            });
        }

        // ------------------------------------------------------------------ the route and the beats

        static void Path(CourseBuilder b)
        {
            var points = new (Vector3 at, string beat)[]
            {
                (V(0f, 0f, 0f), "0"), (V(0f, 0f, 12f), "1"), (V(0f, 0f, 38f), "1"), (V(0f, 0f, 44f), "2"), (V(44f, 0f, 44f), "2"),
                (V(50f, 0f, 44f), "3"), (V(50f, 0f, 71f), "3"), (V(50f, 0f, 77f), "4"), (V(31f, 0f, 77f), "4"),
                (V(26.5f, 0f, 81.5f), "4"), (V(22f, 0f, 83.5f), "4"), (V(17.5f, 0f, 81.5f), "4"), (V(13f, 0f, 77f), "4"),
                (V(7f, 0f, 77f), "5"), (V(7f, 0f, 83.2f), "5"), (V(7f, 3f, 88.4f), "5"), (V(7f, 3f, 92.6f), "5"), (V(7f, Upper, 97.8f), "5"),
                (V(7f, Upper, 103f), "6"), (V(43f, Upper, 103f), "6"), (V(50f, Upper, 103f), "7"), (V(50f, Upper, WallZ), "7"),
                (V(50f, Upper + Tier, WallZ), "7"), (V(50f, Upper + Tier, WallZ + Ledge), "7"),
                (V(50f, Upper + Tier * 2f, WallZ + Ledge), "7"), (V(50f, Upper + Tier * 2f, WallZ + Ledge * 2f), "7"),
                (V(50f, Terrace, WallZ + Ledge * 2f), "7"), (V(50f, Terrace, 118f), "출구"), (V(50f, ExitTop, 120f), "출구"), (Exit, "출구"),
            };
            var route = new Vector3[points.Length];
            var names = new string[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                route[i] = points[i].at;
                names[i] = points[i].beat;
            }
            var path = new GameObject("ProgressPath");
            path.transform.SetParent(b.parent, false);
            path.AddComponent<ProgressPath>().Configure(route, (Vector3[])route.Clone(), names);
        }

        static SectionBeat[] Beats() => new[]
        {
            new SectionBeat("1 고리 마당", V(-7f, -2f, 12f), V(7f, 6f, 38f)),
            new SectionBeat("2 별지도 벨트와 차양 방패", V(12f, -2f, 37f), V(44f, 6f, 51f)),
            new SectionBeat("3 환기구 격자 다리", V(46f, -2f, 50f), V(55f, 6f, 71f)),
            new SectionBeat("4 대천구의 회전판", V(13f, -2f, 67f), V(44f, 6f, 87f)),
            new SectionBeat("5 행성 승강대", V(3f, -2f, 83f), V(11f, 12f, 97.8f)),
            new SectionBeat("6 망원경 다리", V(13f, 4f, 99f), V(43f, 12f, 107f)),
            new SectionBeat("7 별자리 등반벽", V(43f, 4f, 96f), V(57f, 16f, 112f)),
        };
    }
}
