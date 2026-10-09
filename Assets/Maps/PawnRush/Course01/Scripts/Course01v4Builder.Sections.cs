using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The sections of course 01 v0.4, in the order the pawn runs them. Every line is a row of the
    // design doc's §4 tables (world coordinates; y is a floor's top).
    public static partial class Course01v4Builder
    {
        // ------------------------------------------------------------------ S start square (y 0, north)

        static void Start(CourseBuilder b)
        {
            var k = b.kit;
            b.Floor("Start floor", -8f, 8f, -12f, 0f, 0f);
            b.Wall("Back wall", -8.5f, 8.5f, -1f, 4f, -13f, -12f, false);
            b.Rail("Side wall W", -8.5f, -8f, -12f, 0f, 0f);
            b.Rail("Side wall E", 8f, 8.5f, -12f, 0f, 0f);
            b.Rail("Front rail W", -8.5f, -5.4f, 0f, .3f, 0f);
            b.Rail("Front rail E", 5.4f, 8.5f, 0f, .3f, 0f);
            b.Box("Start bar", V(-8f, 0f, -.25f), V(8f, 1.2f, .25f), k.hazard, true).AddComponent<StartBar>();
            // White x -2/-4/-6, black x 2/4/6, at z -4 and -8, facing north.
            for (int team = 0; team < 2; team++)
            {
                int index = 0;
                foreach (float z in new[] { -4f, -8f })
                    foreach (float x in new[] { 2f, 4f, 6f })
                    {
                        var spot = b.Group("Start " + Teams.Name(team) + " " + index);
                        spot.localPosition = V(team == Teams.White ? -x : x, 0f, z);
                        spot.gameObject.AddComponent<SpawnPoint>().Configure(index++, team);
                    }
            }
        }

        // ------------------------------------------------------------------ A shared A (0 -> 3 -> 0, north then east)

        static void SharedA(CourseBuilder b)
        {
            var k = b.kit;
            // A1: 10 m wide, 0.4 m kerbs the bars pass over; two §6 jump bars sweep it all.
            b.Floor("A1 road", -5f, 5f, 0f, 28f, 0f);
            b.Rail("A1 kerb W", -5.4f, -5f, 0f, 28f, 0f, .4f);
            b.Rail("A1 kerb E", 5f, 5.4f, 0f, 28f, 0f, .4f);
            Span(SpinBar(b, "1", V(0f, 0f, 8f), 12f, 65f), Vector3.right, -6f, 6f, 0f);
            Span(SpinBar(b, "2", V(0f, 0f, 20f), 12f, -65f), Vector3.right, -6f, 6f, 0f);

            // A2: the first wall climb, 3 m.
            b.Wall("A2 wall (face z 28) and edge", -5f, 5f, -1f, 3f, 28f, 31f, true, true);

            // A3: three discs over the drop, 2.4 m apart; the end floor turns east.
            Disc(b, "1", V(-1.5f, 3f, 35f), false);
            Disc(b, "2", V(1.5f, 3f, 45f), true);
            Disc(b, "3", V(-1.5f, 3f, 55f), false);
            foreach (var p in new[] { V(-1.5f, -6f, 35f), V(1.5f, -6f, 45f), V(-1.5f, -6f, 55f) })
                b.Cylinder("Disc post", p, 1.2f, 8.6f, k.wallNoClimb, true, true);
            b.Floor("A3 end floor", -6f, 6f, 59f, 70f, 3f);
            b.Rail("A3 rail W", -6.3f, -6f, 59f, 70.3f, 3f);
            b.Rail("A3 rail N", -6f, 6f, 70f, 70.3f, 3f);

            // A4: 10 m road east between 1.2 m walls, two PR_06 sliding walls across it.
            b.Floor("A4 road", 6f, 40f, 60f, 70f, 3f);
            b.Rail("A4 wall S", 6f, 40f, 59.7f, 60f, 3f);
            b.Rail("A4 wall N", 6f, 40f, 70f, 70.3f, 3f);
            Span(SlidingWalls10(b, "1", V(16f, 3f, 65f), 90f, 0f), Vector3.forward, -15f, 15f, 3f);
            Span(SlidingWalls10(b, "2", V(28f, 3f, 65f), 90f, 2.25f), Vector3.forward, -15f, 15f, 3f);   // half of 4.5 s
            b.Wall("A4 drop face", 39f, 40f, -1f, 2f, 60f, 70f, false);

            // The yard in front of plaza 1 (y 0).
            b.Floor("Plaza 1 yard", 40f, 52f, 58f, 72f, 0f);
            b.Rail("Yard rail S", 39.7f, 52.3f, 57.7f, 58f, 0f);
            b.Rail("Yard rail E", 52f, 52.3f, 58f, 71f, 0f);
            b.Rail("Yard rail W (south)", 39.7f, 40f, 58f, 60f, 0f);
            b.Rail("Yard rail W (north)", 39.7f, 40f, 70f, 71f, 0f);
        }

        // ------------------------------------------------------------------ B shared B (0 -> 3, north, west, north)

        static void SharedB(CourseBuilder b)
        {
            var k = b.kit;
            // B0: the two gates' passages let out here; both run 12 m to the middle (x 46).
            b.Floor("B0 exit floor", 24f, 68f, 101f, 110f, 0f);
            b.Wall("B0 wall W", 23f, 24f, -1f, 6f, 101f, 111f, false);
            b.Wall("B0 wall E", 68f, 69f, -1f, 6f, 101f, 111f, false);
            b.Wall("B0 wall N (west)", 24f, 40f, -1f, 6f, 110f, 111f, false);
            b.Wall("B0 wall N (east)", 52f, 68f, -1f, 6f, 110f, 111f, false);
            b.Floor("B turn floor", 36f, 52f, 110f, 124f, 0f);
            b.Rail("B turn rail E", 52f, 52.3f, 111f, 124.3f, 0f);
            b.Rail("B turn rail N", 36f, 52f, 124f, 124.3f, 0f);
            b.Rail("B turn rail W", 35.7f, 36f, 111f, 116f, 0f);

            // B1: the round rotating board and a falling piece on its middle; cliffs either side.
            b.Floor("B1 entry bridge", 34f, 36f, 116f, 124f, 0f);
            RotatingBoardRound(b, V(27f, 0f, 120f), 14f, 18f);
            Hazard(b, k.fallingPiece, "10 Falling piece B1", V(27f, 0f, 120f), 0f, 0f);
            b.Floor("B1 exit bridge", 17f, 20f, 116f, 124f, 0f);

            // B2: an 8 m passage over the drop, three pendulums swinging across it (z).
            b.Floor("B2 pendulum passage", 3f, 17f, 116f, 124f, 0f);
            float[] xs = { 14.5f, 11f, 7.5f }, phases = { 0f, 1.47f, 2.93f };
            for (int i = 0; i < 3; i++)
            {
                Span(Hazard(b, k.pendulum, "11 Pendulum B2 " + (i + 1), V(xs[i], 0f, 120f), 90f, phases[i]), Vector3.forward, -6f, 6f, 0f);
                GantryZ(b, "Pendulum frame " + (i + 1), xs[i], 120f, 0f, 6.4f);
            }

            // B3: up 3 m onto the floor that leads north to plaza 2 - the wall or the jump pad.
            b.Wall("B3 wall (face x 3) and floor", -9f, 3f, -1f, 3f, 112f, 130f, true, true);
            b.Rail("B3 rail W", -9.3f, -9f, 112f, 129f, 3f);
            b.Rail("B3 rail S", -9.3f, 3f, 111.7f, 112f, 3f);
            JumpPad(b, "B3", V(4.6f, 0f, 120f), 270f);
        }

        // ------------------------------------------------------------------ C shared C (3 -> 6, north)

        static void SharedC(CourseBuilder b)
        {
            var k = b.kit;
            b.Floor("Plaza 2 exit floor", -25f, 19f, 161f, 170f, 3f);
            b.Wall("Exit wall W", -26f, -25f, 2f, 9f, 161f, 171f, false);
            b.Wall("Exit wall E", 19f, 20f, 2f, 9f, 161f, 171f, false);
            b.Wall("Exit wall N (west)", -25f, -9f, 2f, 9f, 170f, 171f, false);
            b.Wall("Exit wall N (east)", 3f, 19f, 2f, 9f, 170f, 171f, false);

            b.Floor("C road", -9f, 3f, 170f, 202f, 3f);
            // C1: one pair of 09 clock gates, the road narrowed to it by marble either side.
            Span(Hazard(b, k.clockGates, "09 Clock gates C1", V(-3f, 3f, 176f), 0f, 0f), Vector3.right, -4.3f, 4.3f, 3f);
            b.Wall("Clock gate wall W", -9f, -7.3f, 3f, 8.8f, 175.4f, 176.6f, false);
            b.Wall("Clock gate wall E", 1.3f, 3f, 3f, 8.8f, 175.4f, 176.6f, false);
            // C2: two 6 m rook walls, each closing half the road and sliding to the other half.
            Span(RookWall(b, "1", V(-3f, 3f, 186f), 6f, 3f, 6f, 0f), Vector3.right, -6f, 6f, 3f);
            Span(RookWall(b, "2", V(-3f, 3f, 194f), 6f, 3f, 6f, 3f), Vector3.right, -6f, 6f, 3f);
            // C3: 3 m up to the tower forecourt (y 6), or the two jump pads.
            b.Wall("C3 wall (face z 202) and tower forecourt", -17f, 11f, 2f, 6f, 202f, 210f, true, true);
            JumpPad(b, "C3 W", V(-6f, 3f, 199.5f), 0f);
            JumpPad(b, "C3 E", V(0f, 3f, 199.5f), 0f);
            b.Rail("Forecourt rail W", -17.3f, -17f, 202f, 210f, 6f);
            b.Rail("Forecourt rail E", 11f, 11.3f, 202f, 210f, 6f);
        }

        // ------------------------------------------------------------------ T the 8th-rank tower (6 -> 15)

        // Two ways up: straight up the 9 m south face (rest ledges at 9 and 12, two pendulums sweeping
        // it), or round on a ramp either side to the north balcony. Both end 7 m from the crown circle.
        static void Tower(CourseBuilder b)
        {
            var k = b.kit;
            b.In("Body", () =>
            {
                b.Wall("Tower base", -13f, 7f, 2f, 6f, 210f, 230f, false, true);
                b.Wall("Tower core", -13f, 7f, 6f, 15f, 212f, 230f, false, true);
                // Marble shoulders either side of the face, set back 0.6 m so the pendulum balls clear them.
                b.Wall("Tower shoulder W", -13f, -8f, 6f, 15f, 210.6f, 212f, false, true);
                b.Wall("Tower shoulder E", 2f, 7f, 6f, 15f, 210.6f, 212f, false, true);
            });
            b.In("SouthFace", () =>
            {
                // Climbable stone x -8..2 only, in three 3 m steps with a 0.8 m ledge each.
                b.Wall("South face 1", -8f, 2f, 6f, 9f, 210f, 212f, true, true);
                b.Wall("South face 2", -8f, 2f, 9f, 12f, 210.8f, 212f, true, true);
                b.Wall("South face 3", -8f, 2f, 12f, 15f, 211.6f, 212f, true, true);
                // Two pendulums swinging along x (balls at y 11..13), hung from a frame on the top.
                var pw = Hazard(b, k.pendulum, "11 Pendulum T W", V(-5.5f, 10.8f, 209.4f), 0f, 0f);
                var pe = Hazard(b, k.pendulum, "11 Pendulum T E", V(-.5f, 10.8f, 209.4f), 0f, 2.2f);
                foreach (var p in new[] { pw, pe })
                    p.AddComponent<ObstacleSpan>().Configure(Vector3.right, -6f, 6f, ObstacleSpan.Probe.Face, 11.5f, Vector3.forward, "T face");
                b.Wall("Pendulum frame post W", -12.9f, -12.1f, 15f, 19.1f, 210.6f, 211.4f, false);
                b.Wall("Pendulum frame post E", 6.1f, 6.9f, 15f, 19.1f, 210.6f, 211.4f, false);
                b.Wall("Pendulum frame beam", -12.9f, 6.9f, 18.6f, 19.1f, 208.9f, 211.4f, false);
            });
            // The ramps, built round x -3 (the tower's middle) so the east one is their mirror.
            var west = b.Group("Ramp_West");
            west.localPosition = V(-3f, 0f, 0f);
            var before = b.parent;
            b.parent = west;
            b.Ramp("Ramp 1 (north)", -14f, -10f, 210f, 230f, 6f, 12.5f, 1f);
            b.Slope("Ramp 1 rail", -14.3f, -14f, 210f, 230f, 6f, 12.5f, 0f, 1.5f, k.wallNoClimb, true);
            // 04 hammers swinging across the ramp, the pedestal moved out onto the rail side.
            float[] hz = { 215f, 225f }, phase = { 0f, 2.55f };
            for (int i = 0; i < 2; i++)
            {
                float y = 6f + 6.5f * (hz[i] - 210f) / 20f;
                var hammer = Hazard(b, k.hammer, "04 Hammer " + (i + 1), V(-12f, y, hz[i]), 0f, phase[i]);
                var pedestal = hammer.transform.Find("Back pedestal");
                if (pedestal != null) pedestal.localPosition = V(-2.2f, 1.9f, 1.2f);
                Span(hammer, Vector3.right, -3.6f, 3.6f, y);
            }
            b.Floor("Corner", -14f, -10f, 230f, 234f, 12.5f);
            b.Rail("Corner rail W", -14.3f, -14f, 230f, 234.3f, 12.5f, 1.5f);
            b.Rail("Corner rail N", -14f, -10f, 234f, 234.3f, 12.5f, 1.5f);
            b.RampX("Ramp 2 (east)", 230f, 234f, -10f, -2f, 12.5f, 15f, 1f);
            b.SlopeX("Ramp 2 rail", 234f, 234.3f, -10f, -2f, 12.5f, 15f, 0f, 1.5f, k.wallNoClimb, true);
            b.parent = before;
            var east = TeamMirror.MirrorGroup(west, "Ramp_East");                  // hammers turn the other way, same phase
            foreach (Transform t in east.GetComponentsInChildren<Transform>(true))
                if (t.name == "Back pedestal") t.localPosition = V(2.2f, 1.9f, 1.2f);   // the prefab is not mirrored inside
            b.In("Top", () =>
            {
                b.Floor("North balcony", -5f, -1f, 230f, 234f, 15f);
                b.Rail("Balcony rail", -5f, -1f, 234f, 234.3f, 15f, 1.5f);
                b.Rail("Top rail W", -13.3f, -13f, 210.6f, 230f, 15f);
                b.Rail("Top rail E", 7f, 7.3f, 210.6f, 230f, 15f);
                b.Rail("Top rail NW", -13.3f, -5f, 230f, 230.3f, 15f);
                b.Rail("Top rail NE", -1f, 7.3f, 230f, 230.3f, 15f);
                // The finish: the crown's 6 m circle; a pawn finishes when its hips are in.
                var finish = b.Cylinder("Finish (crown circle)", V(-3f, 15f, 220f), 6f, 3f, null);
                finish.GetComponent<Collider>().isTrigger = true;
                Object.DestroyImmediate(finish.GetComponent<MeshRenderer>());
                finish.AddComponent<FinishZone>().Configure(true);
                b.Cylinder("Crown circle", V(-3f, 15f, 220f), 6f, .04f, k.rankGold, false);
                b.Trigger<NoGrabZone>("No-grab zone (whole top)", V(-13f, 15f, 210f), V(7f, 20f, 234f));
                // The crown over the circle, seen from anywhere. No collisions.
                var crown = b.Group("8th rank crown");
                crown.localPosition = V(-3f, 19f, 220f);
                var outer = b.parent;
                b.parent = crown;
                b.Cylinder("Band", V(0f, 0f, 0f), 6f, 1.6f, k.rankGold, false);
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.PI * 2f / 5f;
                    var point = b.Box("Point", V(-.4f, 0f, -.4f), V(.4f, 2.4f, .4f), k.rankGold, false, false);
                    point.transform.localPosition = V(Mathf.Sin(a) * 2.6f, 2.8f, Mathf.Cos(a) * 2.6f);
                }
                b.Sphere("Orb", V(0f, 5f, 0f), 2f, k.rankGold, false);
                b.Box("Cross upright", V(-.35f, 5.8f, -.35f), V(.35f, 10f, .35f), k.rankGold, false, false);
                b.Box("Cross bar", V(-1.4f, 8f, -.35f), V(1.4f, 8.7f, .35f), k.rankGold, false, false);
                b.parent = outer;
            });
        }

        // ------------------------------------------------------------------ checkpoints and the path

        // §6: all shared. A fall in B's chasm or C's moat goes back to CP3 or CP6, the plaza's entry.
        static void Checkpoints(CourseBuilder b)
        {
            b.Checkpoint(0, V(0f, 0f, -6f), 0f, 14f);
            b.Checkpoint(1, V(0f, 3f, 29.5f), 0f, 10f);
            b.Checkpoint(2, V(8f, 3f, 65f), 90f, 10f);
            b.Checkpoint(3, V(46f, 0f, 74f), 0f, 12f);
            b.Checkpoint(4, V(46f, 0f, 112f), 0f, 10f);
            b.Checkpoint(5, V(18.5f, 0f, 120f), 270f, 8f);
            b.Checkpoint(6, V(-3f, 3f, 132f), 0f, 12f);
            b.Checkpoint(7, V(-3f, 3f, 171f), 0f, 12f);
            b.Checkpoint(8, V(-3f, 6f, 206f), 0f, 12f);
        }

        // §6 progress points P0..P32 (white). Black's differ only inside the plazas: its own
        // station's middle (x 34 -> 58 in plaza 1, -15 -> 9 in plaza 2), so both are the same length.
        static readonly Vector3[] PathPoints =
        {
            V(0, 0, -6), V(0, 0, 27), V(0, 3, 29.5f), V(0, 3, 65), V(40, 3, 65), V(42, 0, 65),           // P0..P5
            V(46, 0, 70), V(46, 0, 74), V(34, 0, 80), V(34, 0, 95), V(34, 0, 99), V(34, 0, 105),         // P6..P11
            V(46, 0, 111), V(46, 0, 120), V(36, 0, 120), V(20, 0, 120), V(17, 0, 120), V(3.5f, 0, 120),  // P12..P17
            V(2.5f, 3, 120), V(-3, 3, 120), V(-3, 3, 132), V(-15, 3, 138), V(-15, 3, 153), V(-15, 3, 160), // P18..P23
            V(-3, 3, 165), V(-3, 3, 201), V(-3, 6, 203), V(-3, 6, 208), V(-15, 6, 210),                  // P24..P28
            V(-15, 12.5f, 230), V(-15, 12.5f, 232), V(-5, 15, 232), V(-3, 15, 223),                      // P29..P32
        };

        public static readonly string[] Sections = { "S+A", "M1", "B", "M2", "C", "T" };

        static string LegSection(int leg) =>
            leg <= 6 ? "S+A" : leg <= 10 ? "M1" : leg <= 19 ? "B" : leg <= 23 ? "M2" : leg <= 26 ? "C" : "T";

        static void Path(CourseBuilder b)
        {
            var white = (Vector3[])PathPoints.Clone();
            var black = (Vector3[])PathPoints.Clone();
            for (int i = 8; i <= 11; i++) black[i].x = 58f;
            for (int i = 21; i <= 23; i++) black[i].x = 9f;
            var sections = new string[PathPoints.Length];
            for (int i = 0; i < sections.Length; i++) sections[i] = LegSection(i);
            var go = new GameObject("ProgressPath");
            go.transform.SetParent(b.parent, false);
            go.AddComponent<ProgressPath>().Configure(white, black, sections);
        }
    }
}
