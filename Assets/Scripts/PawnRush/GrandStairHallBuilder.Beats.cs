using System.Collections.Generic;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The grand stair hall's beats in the order a pawn runs them, each line a row of the level design's §4
    // tables (section frame, y = a floor's top). Where the build differs from the doc, the reason is next to
    // it and in Docs/KingRush/PAWN_RUSH_SECTIONS.md §6.
    public static partial class GrandStairHallBuilder
    {
        public static readonly float[] LaneX = { -6f, -2f, 2f, 6f };
        public const float LaneWidth = 3.6f;

        // R150: each wing stands 11 m further out than the doc's (the hall is 102 m wide, not 80): the room
        // between the gallery's end and the dais takes two bridges and an island instead of one bridge, and the
        // central landing reaches out to the wings (x +-25).
        public const float WingOut = 11f;
        public const float LandingHalf = 14f + WingOut;
        public const float HallHalf = 40f + WingOut;

        // ------------------------------------------------------------------ beat 1: the rolling grand stair (y 0 -> 8)

        static void GrandStair(CourseBuilder b)
        {
            var k = b.kit;
            // The porch (x -6..6, z -12..0): the previous section joins at its arch, z -12, 12 m wide.
            Mass(b, "Porch floor", -6f, 6f, -12f, 0f, 0f);
            Solid(b, "Porch wall W", V(-6.6f, 0f, -12f), V(-6f, 6f, 0f));
            Solid(b, "Porch wall E", V(6f, 0f, -12f), V(6.6f, 6f, 0f));
            Solid(b, "Entrance arch lintel", V(-6.6f, 6f, -12.4f), V(6.6f, 7f, -11.6f));
            // The foyer (x -20..20, z 0..14): two rows of pillars, the stair straight ahead.
            Mass(b, "Foyer floor", -20f, 20f, 0f, 14f, 0f);
            Solid(b, "Foyer wall W", V(-21f, Deep, -1f), V(-20f, 12f, 14f));
            Solid(b, "Foyer wall E", V(20f, Deep, -1f), V(21f, 12f, 14f));
            Solid(b, "Foyer wall S (west of the door)", V(-20f, Deep, -1f), V(-6.6f, 12f, 0f));
            Solid(b, "Foyer wall S (east of the door)", V(6.6f, Deep, -1f), V(20f, 12f, 0f));
            foreach (float x in new[] { -14f, 14f })
                foreach (float z in new[] { 3f, 7f, 11f })
                    b.Cylinder("Foyer pillar", V(x, 0f, z), 1.2f, 12f, k.wallNoClimb, true, true);
            // The foyer's north edge looks over the hall (4 m down): railed but for the stair.
            Rail(b, "Foyer rail N (west)", -20f, -8.3f, 13.7f, 14f, 0f);
            Rail(b, "Foyer rail N (east)", 8.3f, 20f, 13.7f, 14f, 0f);
            // The drain at the stair's foot (z 12..14): the rolls sink through its grate and are gone.
            Picture(b, "Drain grate", V(-8f, 0f, 12f), V(8f, .02f, 14f), k.trimBlack);
            for (int i = 0; i <= 16; i++)
                Picture(b, "Drain bar", V(-8f + i - .05f, 0f, 12f), V(-8f + i + .05f, .03f, 14f), k.wallNoClimb);

            // The lower flight (z 14..30, y 0 -> 4, 16 steps 0.25 x 1.0), the middle landing (z 30..36, y 4), the
            // upper flight (z 36..52, y 4 -> 8). Each flight's last step is the landing above it.
            FlightZ(b, "Lower flight", -8f, 8f, 14f, 16, 1f, 0f);
            Mass(b, "Middle landing", -8f, 8f, 29f, 36f, 4f);
            FlightZ(b, "Upper flight", -8f, 8f, 36f, 16, 1f, 4f);
            // Railings x +-8, 1.1 m, unclimbable: nobody falls into the hall from the stair.
            RailAlongZ(b, "Lower flight rail W", -8.3f, -8f, 14f, 16, 1f, 0f);
            RailAlongZ(b, "Lower flight rail E", 8f, 8.3f, 14f, 16, 1f, 0f);
            Solid(b, "Middle landing rail W", V(-8.3f, Deep, 29.5f), V(-8f, 5.2f, 35.5f));
            Solid(b, "Middle landing rail E", V(8f, Deep, 29.5f), V(8.3f, 5.2f, 35.5f));
            RailAlongZ(b, "Upper flight rail W", -8.3f, -8f, 36f, 16, 1f, 4f);
            RailAlongZ(b, "Upper flight rail E", 8f, 8.3f, 36f, 16, 1f, 4f);

            // Four rolling holes at the top (z 51..52, 3.6 m) and the lanes (§5): RollingSpawner draws and rolls.
            foreach (float x in LaneX)
            {
                Picture(b, "Rolling hole", V(x - LaneWidth * .5f, 8f, 51f), V(x + LaneWidth * .5f, 8.02f, 52f), k.trimBlack);
                Picture(b, "Rolling hole rim", V(x - LaneWidth * .5f, 8f, 50.9f), V(x + LaneWidth * .5f, 8.03f, 51f), k.hazard);
            }
            var lanes = b.Group("Rolling lanes");
            lanes.gameObject.AddComponent<RollingSpawner>().Configure(k.hazard, null);
            // The four lanes cover the stair's 16 m (the rolls are 2.6 m and 3.4 m long in 3.6 m lanes).
            SpanAt(b, "Rolling lanes span", V(0f, 4f, 33f), Vector3.right, -8f, 8f);
        }

        // ------------------------------------------------------------------ beat 2: the central landing's chandeliers (y 8)

        static void CentralLanding(CourseBuilder b)
        {
            var k = b.kit;
            float h = LandingHalf;
            // x -25..25 (R150; the doc's -14..14), z 52..66 (from z 51: the upper flight's last step). West and
            // east it runs on into the wings' ledges at the same height (Wing), so those edges are open.
            Mass(b, "Central landing", -h, h, 51f, 66f, 8f);
            Rail(b, "Central landing rail N", -h, h, 65.7f, 66f, 8f);
            Rail(b, "Central landing rail S (west)", -h, -8.3f, 51f, 51.3f, 8f);
            Rail(b, "Central landing rail S (east)", 8.3f, h, 51f, 51.3f, 8f);
            // The king statue: a 4 x 4 m base at (0, 8, 63), 6 m tall, unclimbable. The one fixed cover from the shock.
            Solid(b, "King statue base", V(-2f, 8f, 61f), V(2f, 9.2f, 65f));
            b.Cylinder("King statue body", V(0f, 9.2f, 63f), 2.4f, 2.6f, k.wallNoClimb, true, true);
            b.Cylinder("King statue collar", V(0f, 11.8f, 63f), 3f, .3f, k.wallNoClimb, true, true);
            b.Sphere("King statue head", V(0f, 12.9f, 63f), 1.7f, k.wallNoClimb).AddComponent<Gameplay.NoClimbSurface>();
            Solid(b, "King statue cross (upright)", V(-.2f, 13.5f, 62.8f), V(.2f, 14f, 63.2f));
            Solid(b, "King statue cross (bar)", V(-.5f, 13.65f, 62.8f), V(.5f, 13.85f, 63.2f));
            // The falling chandeliers (§5): ChandelierRain draws them, 1 m inside the landing's edges. The landing
            // is 48 m across instead of 26, so they fall 1.85 times as often (0.4 ~ 0.75 s instead of 0.7 ~ 1.4)
            // and a square metre of it sees as many as the doc's.
            var rain = b.Group("Chandelier rain");
            rain.gameObject.AddComponent<ChandelierRain>().Configure(k.rankGold, new Vector2(-(h - 1f), 54f), new Vector2(h - 1f, 65f), .4f, .75f);
            // Measured across the way to the west wing (z), where nothing beside the rain leads on: a drop at
            // z 54..65 reaches 4.5 m round it.
            SpanAt(b, "Chandelier rain span", V(-12f, 8f, 58f), Vector3.forward, -8.5f, 11.5f);
        }

        // ------------------------------------------------------------------ beats 3 ~ 5: a wing (s = -1 west, +1 east)

        // The ledge either side of wing stair 1 (the central landing's floor running on), wing stair 1 (out from
        // the landing, 12 steps, three chandeliers close together and no railings: a hit throws the pawn down
        // onto the ledge), the turning landing (two carpets flowing back towards the stair), wing stair 2 (north,
        // 12 steps), the window gallery with four vents whose wind carries a pawn, and two bridges with an island
        // between them to the dais. The east wing is the same code with x -> -x: turning ways, the carpets' and
        // the wind's directions follow. Returns the wing's portcullis (down only when the wing is shut). Every
        // |x| is the doc's + WingOut (R150) but the last bridge's, onto the dais.
        static GameObject Wing(CourseBuilder b, float s)
        {
            var k = b.kit;
            string side = s < 0 ? "W" : "E";
            float d1 = 4f / 3f, o = WingOut;
            float foot1 = 14f + o, top1 = 30f + o, outer = HallHalf;   // wing stair 1 |x| 25..41, the hall wall |x| 51

            // The ledge (y 8) beside wing stair 1, railed on the hall side and where it meets the turning landing.
            Mass(b, "Wing ledge S " + side, s * foot1, s * top1, 51f, 57f, 8f);
            Mass(b, "Wing ledge N " + side, s * foot1, s * top1, 63f, 66f, 8f);
            Rail(b, "Wing ledge rail S " + side, s * foot1, s * top1, 51f, 51.3f, 8f);
            Rail(b, "Wing ledge rail N " + side, s * foot1, s * top1, 65.7f, 66f, 8f);
            Rail(b, "Wing ledge rail (end) " + side, s * (top1 - .3f), s * top1, 51.3f, 54f, 8f);

            // Wing stair 1: |x| 25..41 (outwards), z 57..63 (6 m; the doc's 8), y 8 -> 11, 12 steps 0.25 x 1.33.
            // No railings: a chandelier throws a pawn off it, 0 ~ 3 m down onto the ledge, and it walks back.
            FlightX(b, "Wing stair 1 " + side, s, foot1, 12, d1, 57f, 63f, 8f);
            Mass(b, "Wing stair 1 top step " + side, s * (foot1 + 11f * d1), s * top1, 57f, 63f, 11f);
            // Three chandeliers (11 pendulums) swinging across the stair (z), 4.7 m apart: 2.5 m of rest between
            // the balls' paths, room for two or three. Each 3.9 s (of 4.4) behind the one before, so who crosses
            // one stops before the next whether running (2.5 m/s: 9% go straight on) or sprinting (3.6: 19%) -
            // the doc's two, 7 m and 2.2 s apart, let a sprinter through both in one go 97% of the time.
            float[] along = { 4f, 8.7f, 13.4f }, phase = { 0f, 3.9f, 7.8f };
            for (int i = 0; i < along.Length; i++)
            {
                float top = StepTopMax(along[i] - 1.1f, along[i] + 1.1f, d1, 8f);
                float x = foot1 + along[i];
                var pendulum = Hazard(b, k.pendulum, "11 Pendulum (chandelier) " + side + (i + 1), V(s * x, top - .1f, 60f), s < 0 ? 90f : -90f, phase[i]);
                Span(pendulum, Vector3.forward, -6f, 6f, SlopeY(along[i], d1, 8f));
                GantryZ(b, "Chandelier beam " + side + (i + 1), s * x, 60f, top - .1f + 7.3f, 6.4f);
            }

            // The turning landing (R150, the user: "꺾이는 부분만 사용되고 넓은쪽은 활용이 안돼", "C자로 이동"): |x| 41..51,
            // z 54..66, y 11, round a rook tower (|x| 46, z 60, 5 m across). The way to the moving stair (its foot on
            // the north edge, |x| 46..51) goes south and west round the tower, a C; north of the tower a 2.5 m gap
            // (|x| 43.5..46) is a sprint jump straight across. A sweeper of two arms turns round the tower against
            // the C (an arm every 3.6 s, 0.45 m high: jump it); the corners beyond its 5.6 m are the pockets to wait
            // in. Its checkpoint (CP2) is the top of wing stair 1: a fall off the moving stair comes back here.
            float hub = 35f + o, gap0 = 32.5f + o;   // |x| 46, 43.5
            Mass(b, "Turning landing S " + side, s * top1, s * outer, 54f, 60f, 11f);
            Mass(b, "Turning landing W " + side, s * hub, s * outer, 60f, 66f, 11f);
            Mass(b, "Turning landing E " + side, s * top1, s * gap0, 60f, 66f, 11f);
            Mass(b, "Turning landing (under the tower) " + side, s * gap0, s * hub, 60f, 62.5f, 11f);
            Rail(b, "Turning landing rail S " + side, s * top1, s * outer, 53.7f, 54f, 11f);
            Rail(b, "Turning landing rail (stair 1 side, south) " + side, s * (top1 - .3f), s * top1, 54f, 57f, 11f);
            Rail(b, "Turning landing rail (stair 1 side, north) " + side, s * (top1 - .3f), s * top1, 63f, 66f, 11f);
            Rail(b, "Turning landing rail N " + side, s * top1, s * gap0, 66f, 66.3f, 11f);
            b.Cylinder("Rook tower " + side, V(s * hub, 11f, 60f), 5f, 9f, k.wallNoClimb, true, true);
            b.Cylinder("Rook tower crown " + side, V(s * hub, 20f, 60f), 5.8f, .8f, k.wallNoClimb, true, true);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * .5f;
                var at = V(s * hub + 2.3f * Mathf.Cos(a), 20.8f, 60f + 2.3f * Mathf.Sin(a));
                Solid(b, "Rook tower merlon " + side, at - V(.6f, 0f, .6f), at + V(.6f, 1f, .6f));
            }
            Sweeper(b, "Sweeper " + side, V(s * hub, 11f, 60f), s * 50f);
            b.Checkpoint(2, V(s * (top1 - .9f), 11f, 60f), s < 0 ? 270f : 90f, 6f);

            // Wing stair 2 is the moving stair (R150, "해리포터의 움직이는 계단처럼"; the doc's fixed flight with one
            // chandelier was "너무 허전해"): |x| 46..51 from the landing's north edge, 13 m up to 0.5 m short of the
            // gallery, turning 60° towards the hall's middle about its foot's inner corner every 16 s (lined up 5,
            // warning 1, turning 3.5, away 3, back 3.5). Turned away its top hangs 5 m short of the gallery.
            MovingStair(b, side, s, hub, outer);

            // The window gallery: |x| 24.8..51, z 80..86, y 14, 6 wide; its south edge open to the hall 18 m below.
            Mass(b, "Window gallery " + side, s * (13.8f + o), s * outer, 80f, 86f, 14f);
            // Four 15 vents (the windows) in the north wall blowing into the hall, a quarter of 6.8 s apart. Their
            // wind carries a pawn towards the edge at 3.5 m/s (4.55 off the ground): walking or running along the
            // gallery goes over the edge inside 2 s, sprinting into it (3.6) just holds. The doc's push, 20 m/s² on
            // the bodies, moved a running pawn about 0.2 m. 3 m wide (the prefab's) with 1.5 m rests, all of them inside
            // |x| 43 so the stair comes up on the CP3 rest (|x| 43..51): over the doc's stair top a pawn stepped off past
            // the first wind (the validator, R150 in Unity). The rest before bridge A is 1.7 m.
            float[] ventX = { 30.5f + o, 26f + o, 21.5f + o, 17f + o }, ventPhase = { 0f, 1.7f, 3.4f, 5.1f };
            Marker(b, "Rest (CP3) " + side, s * (ventX[0] + WindWide * .5f), s * outer, 80f, 86f, 14f);
            for (int i = 0; i + 1 < ventX.Length; i++)
                Marker(b, "Rest between the vents " + side, s * (ventX[i + 1] + WindWide * .5f), s * (ventX[i] - WindWide * .5f), 80f, 86f, 14f);
            Marker(b, "Rest before the bridge (CP4) " + side, s * (13.8f + o), s * (ventX[3] - WindWide * .5f), 80f, 86f, 14f);
            for (int i = 0; i < 4; i++)
            {
                float x = s * ventX[i];
                Gust(Hazard(b, k.airVent, "15 Wind vent (window) " + side + (i + 1), V(x, 14f, 86f), 180f, ventPhase[i]));
                // The window over it: glass and a frame in the wall.
                Picture(b, "Window", V(x - 1.4f, 17f, 85.9f), V(x + 1.4f, 24f, 85.98f), k.glass);
                Picture(b, "Window frame", V(x - 1.6f, 16.8f, 85.85f), V(x + 1.6f, 17f, 86f), k.rankGold);
                Picture(b, "Window frame", V(x - 1.6f, 24f, 85.85f), V(x + 1.6f, 24.2f, 86f), k.rankGold);
                // Its wind (3 x 3 x 6 m) covers the gallery's 6 m.
                SpanAt(b, "Wind span " + side + (i + 1), V(x, 14f, 83f), Vector3.forward, -3.2f, 3f);
            }
            b.Checkpoint(3, V(s * (36f + o), 14f, 83f), s < 0 ? 90f : 270f, 5.5f);
            b.Checkpoint(4, V(s * (14.65f + o), 14f, 83f), s < 0 ? 90f : 270f, 5.5f);

            // Two 12 bridges with an island between them to the dais (R150; the doc's one bridge was 8 m, shut
            // 0.5 s of 9, and a pawn on it as it folded slid down its panel back to the gallery or on to the dais):
            // bridge A |x| 16.8..24.8, the island 13.8..16.8 (3 x 6 m, edges open 18 m over the hall floor; a fall
            // goes back to CP4), bridge B 5.9..13.9 onto the dais. B runs half a cycle behind A, so whoever crosses
            // A waits on the island with whoever else did. Both wings the same.
            float bridgeA = 13.8f + o - 4f, islandOuter = bridgeA - 4f;   // 20.8, 16.8
            Bridge(b, "12 Bridge A " + side, s * bridgeA, 0f);
            Mass(b, "Island " + side, s * (islandOuter - 3f), s * islandOuter, 80f, 86f, 14f, 12f);
            Bridge(b, "12 Bridge B to the dais " + side, s * 9.9f, BridgeCycle * .5f);

            // The portcullis at this wing's entrance (|x| 25, across the landing and its ledges, z 51..66): down
            // only when the round shuts the wing (3 or 4 a side, GrandStairHall).
            var gate = Solid(b, "Wing portcullis " + side, V(s * (foot1 - .3f), 8f, 51.3f), V(s * (foot1 + .1f), 13f, 65.7f));
            gate.GetComponent<Renderer>().sharedMaterial = k.trimBlack;
            for (int i = 0; i < 9; i++)
                Picture(b, "Portcullis bar", V(s * (foot1 - .35f), 8f, 51.8f + i * 1.65f), V(s * (foot1 + .15f), 13.2f, 52.1f + i * 1.65f), k.wallNoClimb).transform.SetParent(gate.transform, true);
            gate.SetActive(false);
            return gate;
        }

        // A 15 vent's wind (R150): it carries a pawn (DriftSpeed) instead of pushing its bodies, and
        // its arrows reach the gallery's far edge (the prefab draws them 3.7 m out of a 6.2 m wind).
        const float WindWide = 3f, WindDrift = 3.5f;

        static void Gust(GameObject vent)
        {
            var air = vent != null ? vent.GetComponentInChildren<AirVent>(true) : null;
            if (air == null) return;
            air.DriftSpeed = WindDrift;
            if (air.WindTrigger != null) air.WindTrigger.size = new Vector3(WindWide, air.WindTrigger.size.y, air.WindTrigger.size.z);
            ForChildren(vent, "Air stream", t =>
            {
                t.localPosition = new Vector3(t.localPosition.x, t.localPosition.y, 3.05f);
                t.localScale = new Vector3(t.localScale.x, t.localScale.y, 5.6f);
            });
            ForChildren(vent, "Arrow tip", t => t.localPosition = new Vector3(t.localPosition.x, t.localPosition.y, 5.6f));
        }

        // A 12 bridge every 11.5 s: open 4, warning 1, folding 1.75 (the prefab folds 70° no faster than that),
        // shut 3, unfolding 1.75. Its own landings would overlap the gallery, the island and the dais.
        const float BridgeCycle = 4f + 1f + 1.75f + 3f + 1.75f;

        static void Bridge(CourseBuilder b, string name, float x, float phase)
        {
            var bridge = Hazard(b, b.kit.foldingBridge, name, V(x, 14f, 83f), 0f, phase);
            var folding = bridge != null ? bridge.GetComponent<FoldingBridge>() : null;
            if (folding != null)
            {
                folding.OpenDuration = 4f;
                folding.WarningDuration = 1f;
                folding.FoldDuration = 1.5f;
                folding.ClosedDuration = 3f;
            }
            ForChildren(bridge, "LeftLanding", t => t.gameObject.SetActive(false));
            ForChildren(bridge, "RightLanding", t => t.gameObject.SetActive(false));
        }

        // A beam over a pendulum swinging along z (posts out of the ball's reach, 6.4 m from the axis). Thin and
        // dark: the doc-sized grey ones filled the camera on the wing stair.
        static void GantryZ(CourseBuilder b, string name, float x, float axisZ, float pivotY, float half)
        {
            float lo = axisZ - half, hi = axisZ + half;
            Beam(b, name + " post S", V(x - .2f, Deep, lo - .2f), V(x + .2f, pivotY + .8f, lo + .2f));
            Beam(b, name + " post N", V(x - .2f, Deep, hi - .2f), V(x + .2f, pivotY + .8f, hi + .2f));
            Beam(b, name, V(x - .2f, pivotY + .4f, lo - .2f), V(x + .2f, pivotY + .8f, hi + .2f));
        }

        static GameObject Beam(CourseBuilder b, string name, Vector3 a, Vector3 c) => b.Box(name, a, c, b.kit.trimBlack, true);

        // A sweeper of two arms turning round a vertical axis at `hub` (R150, the turning landing): 0.45 m high from
        // 2.6 to 5.6 m out on a collar round the tower. Its turn is ObstacleMotion's (a function of the shared clock)
        // and an arm knocks a pawn it meets (ObstacleImpact, as the 04 hammer's head does: 9 + up 2).
        static void Sweeper(CourseBuilder b, string name, Vector3 hub, float degreesPerSecond)
        {
            var sensors = new List<GameObject>();
            var root = b.In(name, () =>
            {
                b.Cylinder(name + " collar", V(0f, 0f, 0f), 5.6f, .6f, b.kit.hazard, true, true);
                foreach (float dir in new[] { 1f, -1f })
                {
                    b.Box(name + " arm", V(dir * 2.6f, .05f, -.18f), V(dir * 5.6f, .45f, .18f), b.kit.hazard, true);
                    var sensor = b.Box(name + " impact sensor", V(dir * 2.6f, 0f, -.45f), V(dir * 5.7f, .9f, .45f), null, false);
                    Object.DestroyImmediate(sensor.GetComponent<MeshRenderer>());
                    Object.DestroyImmediate(sensor.GetComponent<MeshFilter>());
                    sensor.GetComponent<Collider>().isTrigger = true;
                    sensors.Add(sensor);
                }
            });
            root.localPosition = hub;
            var body = root.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var context = root.gameObject.AddComponent<ObstacleContext>();
            var motion = root.gameObject.AddComponent<ObstacleMotion>();
            motion.kind = MotionKind.Rotate;
            motion.axis = Vector3.up;
            motion.degreesPerSecond = degreesPerSecond;
            motion.shove = false;
            foreach (var sensor in sensors)
            {
                var impact = sensor.AddComponent<ObstacleImpact>();
                impact.motion = motion;
                impact.context = context;
                impact.strength = 9f;
                impact.lift = 2f;
                impact.hitCooldown = .5f;
            }
        }

        // The moving stair (SwingingStair): its axis at the foot's inner corner (|x| `inner`, z 66.5, y 11), the
        // flight out to |x| `outer`, 13 m up 3 m to 0.5 m short of the gallery. 12 steps (pictures, solid-looking
        // 1.4 m down) over a walking ramp through their middles; an amber strip on the first step, lit while it
        // warns and turns. It turns towards the hall's middle; both wings in step.
        static void MovingStair(CourseBuilder b, string side, float s, float inner, float outer)
        {
            const float foot = 66.5f, length = 13f, rise = 3f;
            float width = outer - inner, d = length / 12f;
            Renderer strip = null;
            var root = b.In("Moving stair " + side, () =>
            {
                for (int k = 0; k < 12; k++)
                {
                    float top = rise * (k + .5f) / 12f;
                    Picture(b, "Moving stair step " + (k + 1), V(0f, top - 1.4f, k * d), V(s * width, top, (k + 1) * d), b.kit.floorChecker);
                }
                Walk(b.SlopeBetween("Moving stair (walking slope)", V(s * width * .5f, 0f, 0f), V(s * width * .5f, rise, length),
                                    width, .6f, 0f, b.kit.floorChecker, true));
                strip = Picture(b, "Moving stair warning strip", V(s * .1f, .13f, .1f), V(s * (width - .1f), .16f, .4f), b.kit.hazard).GetComponent<Renderer>();
            });
            root.localPosition = V(s * inner, 11f, foot);
            var body = root.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            root.gameObject.AddComponent<SwingingStair>().Configure(-s * 60f, 0f, new[] { strip });
        }

        // ------------------------------------------------------------------ beat 5: the royal dais and the lift landing (y 14 <-> 20)

        static void RoyalDais(CourseBuilder b, out GameObject eastLift, out GameObject eastLiftWall)
        {
            var k = b.kit;
            // The dais floating in the middle of the hall (x -6..6, z 78..88, y 14), where the two wings meet again.
            Mass(b, "Royal dais", -6f, 6f, 78f, 86f, 14f, 12f);
            Mass(b, "Royal dais (north)", -6.7f, 6.7f, 86f, 88f, 14f, 12f);
            Rail(b, "Royal dais rail S", -6f, 6f, 78f, 78.3f, 14f);
            Rail(b, "Royal dais rail W", -6f, -5.7f, 78.3f, 80f, 14f);
            Rail(b, "Royal dais rail E", 5.7f, 6f, 78.3f, 80f, 14f);
            // The lift landing (x -6.7..6.7, z 88..94.7): two 05 bundles rising 14 <-> 20 every 6 s, phases 0 / 3. Low
            // they meet the dais, high the balcony. Their sides cannot be climbed; their pit's rims are not needed.
            eastLift = null;
            foreach (float x in new[] { -3.35f, 3.35f })
            {
                var lift = Hazard(b, k.risingTiles, "05 Lift landing " + (x < 0 ? "W" : "E"), V(x, 17f, 91.35f), 0f, x < 0 ? 0f : 3f);
                ForChildren(lift, "Pit edge", t => t.gameObject.SetActive(false));
                ForChildren(lift, "Tile body", t => { if (t.GetComponent<Gameplay.NoClimbSurface>() == null) t.gameObject.AddComponent<Gameplay.NoClimbSurface>(); });
                if (x > 0) eastLift = lift;
            }
            // The shaft: walls either side (a fall off a high tile lands on the dais), a floor under the tiles'
            // lowest place so the column under a raised tile is no hole.
            Solid(b, "Lift shaft wall W", V(-8f, 11f, 86f), V(-6.7f, 21f, 94.7f));
            Solid(b, "Lift shaft wall E", V(6.7f, 11f, 86f), V(8f, 21f, 94.7f));
            Solid(b, "Lift shaft floor", V(-6.7f, 13f, 88f), V(6.7f, 13.4f, 94.7f));
            // 3 or 4 a side: one bundle (west); the east half is an unclimbable wall.
            eastLiftWall = Solid(b, "Lift landing E (wall for 3 or 4 a side)", V(0f, 13.4f, 88f), V(6.7f, 20f, 94.7f));
            eastLiftWall.SetActive(false);
        }

        // ------------------------------------------------------------------ beat 6: the royal balcony, the gate, the way out (y 20 -> 19)

        static void RoyalBalcony(CourseBuilder b)
        {
            var k = b.kit;
            // The balcony: x -8..8, z 94.7..102, y 20; its south edge is the lift landing.
            Mass(b, "Royal balcony", -8f, 8f, 94.7f, 102f, 20f, 12f);
            Solid(b, "Balcony wall W", V(-8.6f, 12f, 94.7f), V(-8f, 26f, 102f));
            Solid(b, "Balcony wall E", V(8f, 12f, 94.7f), V(8.6f, 26f, 102f));
            // The guard knights' alcoves either side, 3 m up, unclimbable. They fill the balcony's sides along the
            // charge lanes, so the two charges cover the way across (§7: "기병(발코니 폭 16 m를 두 돌진 띠가 나눠 덮음)").
            Solid(b, "Knight alcove W", V(-8f, 20f, 95.6f), V(-5.2f, 23f, 101.4f));
            Solid(b, "Knight alcove E", V(5.2f, 20f, 95.6f), V(8f, 23f, 101.4f));
            // Two 17 knights: land at (-4, 20, 97) and charge 8 m east; land at (4, 20, 100) and charge west. Half a
            // cycle apart (7.62 s), so one lane is always quiet. They leap from their alcoves.
            Knight(b, "W", V(-2f, 20f, 97f), 0f, 0f);
            Knight(b, "E", V(2f, 20f, 100f), 180f, 3.81f);
            // The 09 clock gate (two doors, 5 s, out of step) and its blocking walls to the sides (5.8 m).
            var gates = Hazard(b, k.clockGates, "09 Clock gate (the palace door)", V(0f, 20f, 102f), 0f, 0f);
            Span(gates, Vector3.right, -4.3f, 4.3f, 20f);
            Solid(b, "Gate wall W", V(-8f, 20f, 101.4f), V(-4.3f, 25.8f, 102.6f));
            Solid(b, "Gate wall E", V(4.3f, 20f, 101.4f), V(8f, 25.8f, 102.6f));
            // The runout: 18 m without obstacles, railed.
            Mass(b, "Runout", -6f, 6f, 102f, 120f, 20f, 12f);
            Rail(b, "Runout rail W", -6.3f, -6f, 102.6f, 120f, 20f);
            Rail(b, "Runout rail E", 6f, 6.3f, 102.6f, 120f, 20f);
            // The exit terrace (12 x 6 m, CP 끝), 1 m over the mini-game.
            Mass(b, "Exit terrace", -6f, 6f, 120f, 126f, 20f, 12f);
            Marker(b, "Exit terrace (CP 끝)", -6f, 6f, 120f, 126f, 20f);
            Rail(b, "Terrace rail W", -6.3f, -6f, 120f, 125.6f, 20f);
            Rail(b, "Terrace rail E", 6f, 6.3f, 120f, 125.6f, 20f);
            // The open arch (12 wide, 5 high, no door), four steps of 0.25 down, the entrance band (z 128..132, y 19).
            Solid(b, "Arch pillar W", V(-7f, 19f, 125.6f), V(-6f, 26f, 126.4f));
            Solid(b, "Arch pillar E", V(6f, 19f, 125.6f), V(7f, 26f, 126.4f));
            Solid(b, "Arch lintel", V(-7f, 25f, 125.6f), V(7f, 26f, 126.4f));
            for (int i = 1; i <= 4; i++)
                Mass(b, "Step down " + i, -6f, 6f, 126f + (i - 1) * .5f, 126f + i * .5f, 20f - .25f * i, 12f);
            Mass(b, "Mini-game entrance band", -6f, 6f, 128f, 132f, 19f, 12f);
            Rail(b, "Band rail W", -6.3f, -6f, 126.4f, 132f, 19f);
            Rail(b, "Band rail E", 6f, 6.3f, 126.4f, 132f, 19f);
        }

        // A 17 knight whose root is `root` (it lands 2 m behind its root and charges 8 m along its +x), leaping
        // from the alcove beside it (3 m up).
        static void Knight(CourseBuilder b, string side, Vector3 root, float yaw, float phase)
        {
            var go = Hazard(b, b.kit.knightCavalry, "17 Guard knight " + side, root, yaw, phase);
            var knight = go != null ? go.GetComponent<KnightCavalryCharge>() : null;
            if (knight == null) return;
            var start = new Vector3(-4.6f, 3f, 0f);
            if (knight.StartPoint != null) knight.StartPoint.position = go.transform.TransformPoint(start);
            if (knight.KnightRoot != null) knight.KnightRoot.transform.position = go.transform.TransformPoint(start);
            // Its lane across the balcony, between the alcoves: landing -2 .. end +6 (its +x), the body 1.2 m round.
            float sign = Mathf.Approximately(yaw, 180f) ? -1f : 1f;
            Span(go, Vector3.right, sign > 0 ? -3.2f : -7.2f, sign > 0 ? 7.2f : 3.2f, root.y);
        }
    }
}
