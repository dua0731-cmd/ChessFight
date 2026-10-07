using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The sections of course 01 v0.2, in the order the pawn runs them. Every line is a row of the
    // design doc's §6 tables (world coordinates; y is a floor's top). White's wings only: black's
    // are their mirror.
    public static partial class Course01v2Builder
    {
        // ------------------------------------------------------------------ S start square (y 0)

        static void Start(CourseBuilder b)
        {
            var k = b.kit;
            b.Floor("Start floor", -15f, 15f, -14f, 0f, 0f);
            b.Wall("Back wall", -15.5f, 15.5f, 0f, 4f, -15f, -14f, false);
            // The side walls run on to the bank (z 18); B0 (y 12) is the square's roof.
            b.Rail("Side wall W", -15.5f, -15f, -14f, 18f, 0f);
            b.Rail("Side wall E", 15f, 15.5f, -14f, 18f, 0f);
            var bar = b.Box("Start bar", V(-15f, 0f, -.25f), V(15f, 1.2f, .25f), k.hazard, true);
            bar.AddComponent<StartBar>();
            // White's six places at x -4/-7/-10, z -5 and -9, facing north; black is the mirror.
            TeamMirror.MirrorTeam(b.In(TeamMirror.WhiteName, () =>
            {
                int index = 0;
                foreach (float z in new[] { -5f, -9f })
                    foreach (float x in new[] { -4f, -7f, -10f })
                    {
                        var spot = b.Group("Start " + index);
                        spot.localPosition = V(x, 0f, z);
                        spot.gameObject.AddComponent<SpawnPoint>().Configure(index++, Teams.White);
                    }
            }));
            var sign = RankMarker.Build(b.parent, "Rank 2", V(0f, 1.2f, -13.96f), 2, 8f, 1.6f, k.rankGold);
            sign.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        // ------------------------------------------------------------------ A shared A, ground (y 0 -> 6)

        // No cliffs in A: the first 30 s try a jump, a wall and a jump pad without falling off.
        static void Ground(CourseBuilder b)
        {
            var k = b.kit;
            // A1 spin-bar yard (z 0..18). The 05 tiles' pit is cut out of its floor (x +-3.35, z 11.3..18).
            b.Floor("A1 floor", -15f, 15f, 0f, 11.3f, 0f);
            b.Box("A1 floor beside the pit W", V(-15f, -4.5f, 11.3f), V(-3.35f, 0f, 18f), k.floorChecker).AddComponent<CourseFloor>();
            b.Box("A1 floor beside the pit E", V(3.35f, -4.5f, 11.3f), V(15f, 0f, 18f), k.floorChecker).AddComponent<CourseFloor>();
            b.Box("Tile pit front", V(-3.35f, -4.5f, 10.8f), V(3.35f, -1f, 11.3f), k.floorChecker);
            b.Box("Tile pit bottom", V(-3.35f, -4.5f, 11.3f), V(3.35f, -3.5f, 18f), k.floorChecker);
            SpinBar(b, "L", V(-7f, 0f, 6f), 12f, 65f);
            SpinBar(b, "R", V(7f, 0f, 6f), 12f, -65f);

            // A2 bank and disc pit (z 18..40, 0 -> 3). Three ways up the bank: the 30 m wall, two jump
            // pads, the 05 rising tiles (a raised tile's top meets the bank's top).
            b.Wall("A2 bank (face z 18, back z 20)", -15f, 15f, -4.5f, 3f, 18f, 20f, true, true);
            b.Obstacle(k.risingTiles, "05 Rising tiles", V(0f, 0f, 14.65f));
            JumpPad(b, "A2 W", V(-7f, 0f, 15f), 0f);
            JumpPad(b, "A2 E", V(7f, 0f, 15f), 0f);
            RankMarker.Build(b.parent, "Rank 3 W", V(-10.5f, .5f, 17.96f), 3, 6f, 1.6f, k.rankGold);
            RankMarker.Build(b.parent, "Rank 3 E", V(10.5f, .5f, 17.96f), 3, 6f, 1.6f, k.rankGold);
            // The pit: only its front (the bank's back, z 20) can be climbed out of.
            b.Floor("Disc pit floor", -9f, 9f, 20f, 40f, 0f);
            b.Wall("High bank road W", -15f, -9f, -1f, 3f, 20f, 40f, false, true);
            b.Wall("High bank road E", 9f, 15f, -1f, 3f, 20f, 40f, false, true);
            b.Rail("Bank side wall W", -15.5f, -15f, 18f, 47f, 3f);
            b.Rail("Bank side wall E", 15f, 15.5f, 18f, 47f, 3f);
            // 01 discs, top at y 3, 2.8 m apart; west ones clockwise, the east one counter-clockwise.
            Disc(b, "1", V(-4.5f, 3f, 24f), false);
            Disc(b, "2", V(4.5f, 3f, 30f), true);
            Disc(b, "3", V(-4.5f, 3f, 36f), false);
            foreach (var p in new[] { V(-4.5f, 0f, 24f), V(4.5f, 0f, 30f), V(-4.5f, 0f, 36f) })
                b.Cylinder("Disc post", p, 1.2f, 2.6f, k.wallNoClimb, true, true);
            // 03 pushers across the bank roads, shoving into the pit (4 s cycle, opposite pairs).
            Hazard(b, k.pusher, "03 Pusher W1", V(-12f, 3f, 26f), 0f, 0f);
            Hazard(b, k.pusher, "03 Pusher W2", V(-12f, 3f, 34f), 0f, 2f);
            Hazard(b, k.pusher, "03 Pusher E1", V(12f, 3f, 26f), 0f, 2f);
            Hazard(b, k.pusher, "03 Pusher E2", V(12f, 3f, 34f), 0f, 0f);

            // A3 cavalry forecourt and rampart (z 40..64, 3 -> 6). The forecourt's front is the pit's
            // back wall (unclimbable).
            b.Wall("A3 forecourt", -15f, 15f, -1f, 3f, 40f, 47f, false, true);
            // K1 lands at (-3, 3, 42) and charges east to x 5; K2 lands at (3, 3, 45) and charges west
            // to x -5. Both leap from the terrace (y 6, z 48: no post on the forecourt), 2 m high so the
            // knight stays under B's floor (y 11 underneath).
            Knight(b, "K1", V(-1f, 3f, 42f), 0f, 0f, V(-6f, 3f, 6f), 2f);
            Knight(b, "K2", V(1f, 3f, 45f), 180f, 3.81f, null, 2f);
            JumpPad(b, "A3 W", V(-9f, 3f, 43.5f), 0f);
            JumpPad(b, "A3 E", V(9f, 3f, 43.5f), 0f);
            b.Wall("A3 rampart (face z 47)", -15f, 15f, -1f, 6f, 47f, 48f, true, true);
            b.Wall("Terrace", -15f, 15f, -1f, 6f, 48f, 64f, false, true);
            RankMarker.Build(b.parent, "Rank 4 W", V(-5f, 3.5f, 46.96f), 4, 6f, 1.6f, k.rankGold);
            RankMarker.Build(b.parent, "Rank 4 E", V(5f, 3.5f, 46.96f), 4, 6f, 1.6f, k.rankGold);
            // The terrace is a dead end: its only ways on are the alcoves in its 6 m side walls (z 54..58).
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0 ? "W" : "E";
                b.Wall("Terrace side wall " + side + " (south)", Mathf.Min(s * 15f, s * 16f), Mathf.Max(s * 15f, s * 16f), 6f, 12f, 47f, 54f, false);
                b.Wall("Terrace side wall " + side + " (north)", Mathf.Min(s * 15f, s * 16f), Mathf.Max(s * 15f, s * 16f), 6f, 12f, 58f, 64f, false);
            }
            b.Box("Tower door (closed, decoration)", V(-5f, 6f, 63.7f), V(5f, 12f, 64f), k.rankGold, true);
        }

        // ------------------------------------------------------------------ W1 team section 1, west wing (6 -> 9 -> 12)

        // Out of the terrace to the west, south along the outer wing, then east over to B0.
        static void Wing1(CourseBuilder b)
        {
            var k = b.kit;
            b.Floor("Alcove", -20f, -15f, 54f, 58f, 6f);
            b.Wall("Alcove wall S", -20f, -16f, 6f, 12f, 53.5f, 54f, false);
            b.Wall("Alcove wall N", -20f, -16f, 6f, 12f, 58f, 58.5f, false);
            Arch(b, "Team arch", 54f, 58f, 10f, 12f);
            Barrier(b, "Team barrier (entry)", -15f, 52f, 60f, 6f, 16f);
            b.Floor("Link bridge", -32f, -20f, 54f, 58f, 6f);                // W2's corner passes 9 m over it
            b.Floor("Corner", -44f, -32f, 50f, 62f, 6f);
            b.Floor("Wind bridge", -39.5f, -36.5f, 36f, 50f, 6f);            // 3 m, cliffs both sides
            // 15 air vents blowing west across the bridge; the wind box's bottom is 0.5 under the deck.
            // z 42..44 is out of the wind: wait there for the beat.
            Hazard(b, k.airVent, "15 Air vent 1", V(-35.8f, 5.5f, 46f), -90f, 0f);
            Hazard(b, k.airVent, "15 Air vent 2", V(-35.8f, 5.5f, 40f), -90f, 3.4f);
            b.Wall("Vent post 1", -35.8f, -35.3f, -10f, 5.5f, 44.6f, 47.4f, false);
            b.Wall("Vent post 2", -35.8f, -35.3f, -10f, 5.5f, 38.6f, 41.4f, false);
            b.Floor("Step floor", -44f, -32f, 30f, 36f, 6f);
            b.Wall("Fifth rank step (face z 30)", -44f, -32f, 5f, 9f, 24f, 30f, true, true);
            JumpPad(b, "W1", V(-41f, 6f, 33.5f), 180f);
            Slot(b, 1, V(-38f, 9f, 24f), 180f, 3f, 9f, 4f, 5, -1);           // heads south; lets out east
            b.Floor("Exit bridge", -32f, -15f, -11f, -7f, 12f);
            Barrier(b, "Team barrier (exit)", -15f, -13f, -5f, 12f, 22f);
            b.Trigger<TeamZone>("Team zone W1", V(-46f, 4f, -13f), V(-15f, 14f, 64f)).SetTeam(Teams.White);
            b.Checkpoint(3, V(-38f, 6f, 56f), 180f, 10f, Teams.White);
            b.Checkpoint(4, V(-38f, 9f, 27f), 180f, 10f, Teams.White);
        }

        // ------------------------------------------------------------------ B shared B, over A (12 -> 15)

        static void Middle(CourseBuilder b)
        {
            var k = b.kit;
            // B0, the start square's roof. Railed but for the two wing exits (z -11..-7).
            b.Floor("B0 merge floor", -15f, 15f, -14f, 0f, 12f);
            b.Rail("B0 rail S", -15.3f, 15.3f, -14.3f, -14f, 12f);
            foreach (float s in new[] { -1f, 1f })
            {
                float x0 = s < 0 ? -15.3f : 15f, x1 = s < 0 ? -15f : 15.3f;
                b.Rail("B0 rail " + (s < 0 ? "W" : "E") + " (south)", x0, x1, -14f, -11f, 12f);
                b.Rail("B0 rail " + (s < 0 ? "W" : "E") + " (north)", x0, x1, -7f, 0f, 12f);
            }
            // B1 round rotating board (z 2..25), 0.1 m from the bridges.
            b.Floor("B1 entry bridge", -4f, 4f, 0f, 1.9f, 12f);
            RotatingBoardRound(b, V(0f, 12f, 13.5f), 23f, 18f);
            Hazard(b, k.fallingPiece, "10 Falling piece B1", V(0f, 12f, 13.5f), 0f, 0f);
            b.Floor("B1 exit bridge", -4f, 4f, 25.1f, 28f, 12f);
            // B2: the low passage with pendulums, or 3 m up onto the castle beams.
            b.Floor("B2 start floor", -10f, 10f, 28f, 30f, 12f);
            b.Wall("Climb base W", -10f, -8f, 12f, 15f, 29f, 31f, true, true);
            b.Wall("Climb base E", 8f, 10f, 12f, 15f, 29f, 31f, true, true);
            b.Wall("Castle beam W", -10f, -8f, 14f, 15f, 31f, 50f, false, true);
            b.Wall("Castle beam E", 8f, 10f, 14f, 15f, 31f, 50f, false, true);
            b.Floor("Low passage", -4f, 4f, 30f, 41f, 12f);
            Hazard(b, k.pendulum, "11 Pendulum B2 1", V(0f, 12f, 33.5f), 0f, 0f);
            Hazard(b, k.pendulum, "11 Pendulum B2 2", V(0f, 12f, 38.5f), 0f, 2.2f);
            Gantry(b, "Pendulum frame 1", 0f, 33.5f, 12f);
            Gantry(b, "Pendulum frame 2", 0f, 38.5f, 12f);
            b.Ramp("B2 ramp", -4f, 4f, 41f, 50f, 12f, 15f, 1f);
            // B3, the fork: 6 m side walls with each team's alcove (z 51..55).
            b.Floor("B3 fork floor", -15f, 15f, 50f, 56f, 15f);
            b.Rail("B3 rail N", -15f, 15f, 56f, 56.3f, 15f);
            foreach (float s in new[] { -1f, 1f })
            {
                float x0 = s < 0 ? -16f : 15f, x1 = s < 0 ? -15f : 16f;
                b.Wall("B3 side wall " + (s < 0 ? "W" : "E") + " (south)", x0, x1, 15f, 21f, 50f, 51f, false);
                b.Wall("B3 side wall " + (s < 0 ? "W" : "E") + " (north)", x0, x1, 15f, 21f, 55f, 56f, false);
            }
        }

        // ------------------------------------------------------------------ W2 team section 2, inner wing (15 -> 18 -> 24)

        static void Wing2(CourseBuilder b)
        {
            var k = b.kit;
            b.Floor("Alcove", -20f, -15f, 51f, 55f, 15f);
            b.Wall("Alcove wall S", -20f, -16f, 15f, 21f, 50.5f, 51f, false);
            b.Wall("Alcove wall N", -20f, -16f, 15f, 21f, 55f, 55.5f, false);
            Arch(b, "Team arch", 51f, 55f, 19f, 21f);
            Barrier(b, "Team barrier (entry)", -15f, 49f, 57f, 15f, 25f);
            b.Floor("Corner", -32f, -20f, 48f, 58f, 15f);                    // 9 m over W1's link bridge
            b.Floor("Lane", -32f, -20f, 58f, 74f, 15f);
            Hazard(b, k.fallingPiece, "10 Falling piece W2", V(-26f, 15f, 66f), 0f, 0f);
            PawnStatue(b, "S", V(-26f, 15f, 61.5f));
            PawnStatue(b, "N", V(-26f, 15f, 70.5f));
            b.Wall("Sixth rank step (face z 74)", -32f, -20f, 14f, 18f, 74f, 78f, true, true);
            RankMarker.Build(b.parent, "Rank 6", V(-26f, 15.5f, 73.96f), 6, 6f, 1.6f, k.rankGold);
            JumpPad(b, "W2", V(-29.5f, 15f, 70.5f), 0f);                     // inside the piece's warning ring
            Slot(b, 2, V(-26f, 18f, 78f), 0f, 6f, 17f, 6f, 7, 1);            // heads north; lets out east
            b.Floor("Exit bridge", -20f, -15f, 118f, 122f, 24f);
            Barrier(b, "Team barrier (exit)", -15f, 116f, 124f, 24f, 34f);
            // Cut at x -17 so it never reaches the tower ramp (x -16..-12).
            b.Trigger<TeamZone>("Team zone W2", V(-34f, 13f, 47f), V(-17f, 30f, 124f)).SetTeam(Teams.White);
            b.Checkpoint(8, V(-26f, 15f, 53f), 0f, 10f, Teams.White);
            b.Checkpoint(9, V(-26f, 18f, 76f), 0f, 10f, Teams.White);
        }

        // ------------------------------------------------------------------ C shared C, behind the tower (y 24)

        static void Upper(CourseBuilder b)
        {
            var k = b.kit;
            b.Floor("C0 merge floor", -15f, 15f, 112f, 126f, 24f);
            b.Rail("C0 rail N", -15.3f, 15.3f, 126f, 126.3f, 24f);
            foreach (float s in new[] { -1f, 1f })
            {
                float x0 = s < 0 ? -15.3f : 15f, x1 = s < 0 ? -15f : 15.3f;
                b.Rail("C0 rail " + (s < 0 ? "W" : "E") + " (south)", x0, x1, 112f, 118f, 24f);
                b.Rail("C0 rail " + (s < 0 ? "W" : "E") + " (north)", x0, x1, 122f, 126f, 24f);
            }
            // 09 clock gates facing south; the fillers between them are climbable on purpose.
            Hazard(b, k.clockGates, "09 Clock gates W", V(-10.3f, 24f, 112f), 180f, 0f);
            Hazard(b, k.clockGates, "09 Clock gates C", V(0f, 24f, 112f), 180f, 1.25f);
            Hazard(b, k.clockGates, "09 Clock gates E", V(10.3f, 24f, 112f), 180f, 2.5f);
            b.Wall("Gate filler W", -6f, -4.3f, 24f, 29.8f, 111.4f, 112.6f, true);
            b.Wall("Gate filler E", 4.3f, 6f, 24f, 29.8f, 111.4f, 112.6f, true);
            b.Wall("Gate filler WW", -15f, -14.6f, 24f, 29.8f, 111.4f, 112.6f, true);
            b.Wall("Gate filler EE", 14.6f, 15f, 24f, 29.8f, 111.4f, 112.6f, true);
            // C1 rook-wall plaza: cliffs all round; the band z 101..104 is the breather.
            b.Floor("C1 rook wall plaza", -15f, 15f, 94f, 112f, 24f);
            RookWall(b, "A", V(0f, 24f, 106f), 12f, 6f, 0f);
            RookWall(b, "B", V(0f, 24f, 99f), 12f, 6f, 3f);                  // always on A's other side
            // C2 tower forecourt; the spiral ramps start at its ends.
            b.Floor("C2 tower forecourt", -16f, 16f, 88f, 94f, 24f);
            b.Rail("C2 rail W", -16.3f, -16f, 88f, 94f, 24f);
            b.Rail("C2 rail E", 16f, 16.3f, 88f, 94f, 24f);
        }

        // ------------------------------------------------------------------ T the 8th-rank tower (24 -> 36)

        // Two ways up: straight up the 12 m north face (two 0.8 m rest ledges, pendulums sweeping it),
        // or half way round on a spiral ramp to the south balcony. Both leave 9 m to the crown.
        static void Tower(CourseBuilder b)
        {
            var k = b.kit;
            b.In("Body", () =>
            {
                b.Wall("Tower base", -12f, 12f, -1f, 24f, 64f, 88f, false, true);
                b.Wall("Tower body", -12f, 12f, 24f, 36f, 64f, 85.6f, false, true);
                b.Wall("Tower shoulder W", -12f, -6f, 24f, 36f, 85.6f, 87.4f, false, true);
                b.Wall("Tower shoulder E", 6f, 12f, 24f, 36f, 85.6f, 87.4f, false, true);
                RankMarker.Build(b.parent, "Rank 8", V(0f, 13f, 63.96f), 8, 8f, 1.6f, k.rankGold).transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            });
            b.In("NorthFace", () =>
            {
                // Climbable stone x -6..6 only: faces at z 88 (24..28), 87.2 (28..32) and 86.4 (32..36),
                // each step back a 0.8 m ledge to rest on.
                b.Wall("North face 1", -6f, 6f, 24f, 28f, 85.6f, 88f, true, true);
                b.Wall("North face 2", -6f, 6f, 28f, 32f, 85.6f, 87.2f, true, true);
                b.Wall("North face 3", -6f, 6f, 32f, 36f, 85.6f, 86.4f, true, true);
                // 11 pendulums sweeping the face's upper half (ball 30..32 m), on a frame over the top.
                Hazard(b, k.pendulum, "11 Pendulum T W", V(-3f, 30f, 88.6f), 0f, 0f);
                Hazard(b, k.pendulum, "11 Pendulum T E", V(3f, 30f, 88.6f), 0f, 2.2f);
                b.Wall("Pendulum frame post W", -9.9f, -9.1f, 36f, 38.3f, 86.6f, 87.4f, false);
                b.Wall("Pendulum frame post E", 9.1f, 9.9f, 36f, 38.3f, 86.6f, 87.4f, false);
                b.Wall("Pendulum frame beam", -10f, 10f, 37.8f, 38.3f, 86.6f, 89.4f, false);
            });
            var west = b.In("Ramp_West", () =>
            {
                b.Ramp("Ramp 1 (south)", -16f, -12f, 88f, 64f, 24f, 32.5f, 1f);
                b.Slope("Ramp 1 rail", -16.3f, -16f, 88f, 64f, 24f, 32.5f, 0f, 1.5f, k.wallNoClimb, true);
                // 04 hammers, pedestal on the rail side, the head swinging along the ramp.
                Hazard(b, k.hammer, "04 Hammer 1", V(-14f, 26.83f, 80f), -90f, 0f);
                Hazard(b, k.hammer, "04 Hammer 2", V(-14f, 30.38f, 70f), -90f, 2.55f);
                b.Floor("South-west corner", -16f, -12f, 60f, 64f, 32.5f);
                b.Rail("Corner rail W", -16.3f, -16f, 60f, 64f, 32.5f, 1.5f);
                b.Rail("Corner rail S", -16.3f, -12f, 59.7f, 60f, 32.5f, 1.5f);
                b.RampX("Ramp 2 (east)", 60f, 64f, -12f, -2f, 32.5f, 36f, 1f);
                b.SlopeX("Ramp 2 rail", 59.7f, 60f, -12f, -2f, 32.5f, 36f, 0f, 1.5f, k.wallNoClimb, true);
            });
            TeamMirror.MirrorGroup(west, "Ramp_East");                        // hammers turn the other way, same phase
            b.In("Top", () =>
            {
                b.Floor("South balcony", -2f, 2f, 60f, 64f, 36f);
                b.Rail("Balcony rail", -2f, 2f, 59.7f, 60f, 36f, 1.5f);
                b.Rail("Top rail W", -12.3f, -12f, 64f, 87.4f, 36f);
                b.Rail("Top rail E", 12f, 12.3f, 64f, 87.4f, 36f);
                b.Rail("Top rail SW", -12f, -2f, 64f, 64.3f, 36f);
                b.Rail("Top rail SE", 2f, 12f, 64f, 64.3f, 36f);
                // The finish: the crown's 6 m circle; a pawn finishes when its hips are in.
                var finish = b.Cylinder("Finish (crown circle)", V(0f, 36f, 76f), 6f, 3f, null);
                finish.GetComponent<Collider>().isTrigger = true;
                Object.DestroyImmediate(finish.GetComponent<MeshRenderer>());
                finish.AddComponent<FinishZone>().Configure(true);
                b.Cylinder("Crown circle", V(0f, 36f, 76f), 6f, .04f, k.rankGold, false);
                b.Trigger<NoGrabZone>("No-grab zone (whole top)", V(-12f, 36f, 64f), V(12f, 40f, 88f));
                // The crown, 10 m over the circle, seen from anywhere. No collisions.
                var crown = b.Group("8th rank crown");
                crown.localPosition = V(0f, 39f, 76f);
                var before = b.parent;
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
                b.parent = before;
            });
        }

        // ------------------------------------------------------------------ checkpoints and the path

        static void SharedCheckpoints(CourseBuilder b)
        {
            b.Checkpoint(0, V(0f, 0f, -7f), 0f, 30f);
            b.Checkpoint(1, V(0f, 3f, 19f), 0f, 30f);
            b.Checkpoint(2, V(0f, 6f, 50f), 0f, 30f);
            b.Checkpoint(5, V(0f, 12f, -7f), 0f, 30f);
            b.Checkpoint(6, V(0f, 12f, 29f), 0f, 20f);
            b.Checkpoint(7, V(0f, 15f, 53f), 0f, 30f);
            b.Checkpoint(10, V(0f, 24f, 119f), 180f, 30f);
            b.Checkpoint(11, V(0f, 24f, 91f), 180f, 30f);
        }

        // §3 progress points P0..P32 (white, by the ramp); each leg's section.
        static readonly Vector3[] PathPoints =
        {
            V(0, 0, -7), V(0, 0, 17), V(0, 3, 19), V(0, 3, 46), V(0, 6, 48), V(0, 6, 56),             // P0..P5
            V(-17, 6, 56), V(-38, 6, 56), V(-38, 6, 31), V(-38, 9, 29), V(-38, 9, 2), V(-38, 12, -7),  // P6..P11
            V(-38, 12, -9), V(-15, 12, -9), V(0, 12, -7), V(0, 12, 41), V(0, 15, 50), V(0, 15, 53),   // P12..P17
            V(-17, 15, 53), V(-26, 15, 53), V(-26, 15, 73), V(-26, 18, 75), V(-26, 18, 100),           // P18..P22
            V(-26, 24, 117), V(-26, 24, 120), V(-15, 24, 120), V(0, 24, 119), V(0, 24, 91),            // P23..P27
            V(-14, 24, 90), V(-14, 32.5f, 64), V(-14, 32.5f, 62), V(-2, 36, 62), V(0, 36, 73),          // P28..P32
        };

        static string LegSection(int leg) =>
            leg <= 5 ? "S+A" : leg <= 12 ? "W1" : leg <= 17 ? "B" : leg <= 24 ? "W2" : leg <= 26 ? "C" : "T";

        static void Path(CourseBuilder b)
        {
            var sections = new string[PathPoints.Length];
            for (int i = 0; i < sections.Length; i++) sections[i] = LegSection(i);
            var go = new GameObject("ProgressPath");
            go.transform.SetParent(b.parent, false);
            go.AddComponent<ProgressPath>().Configure((Vector3[])PathPoints.Clone(), sections);
        }
    }
}
