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
            // x -14..14, z 52..66 (from z 51: the upper flight's last step).
            Mass(b, "Central landing", -14f, 14f, 51f, 66f, 8f);
            Rail(b, "Central landing rail N", -14f, 14f, 65.7f, 66f, 8f);
            // West and east edges are railed but for the wing entrances (z 56..64); the south edge beside the stair.
            Rail(b, "Central landing rail W (south)", -14f, -13.7f, 51f, 56f, 8f);
            Rail(b, "Central landing rail W (north)", -14f, -13.7f, 64f, 65.7f, 8f);
            Rail(b, "Central landing rail E (south)", 13.7f, 14f, 51f, 56f, 8f);
            Rail(b, "Central landing rail E (north)", 13.7f, 14f, 64f, 65.7f, 8f);
            Rail(b, "Central landing rail S (west)", -14f, -8.3f, 51f, 51.3f, 8f);
            Rail(b, "Central landing rail S (east)", 8.3f, 14f, 51f, 51.3f, 8f);
            // The king statue: a 4 x 4 m base at (0, 8, 63), 6 m tall, unclimbable. The one fixed cover from the shock.
            Solid(b, "King statue base", V(-2f, 8f, 61f), V(2f, 9.2f, 65f));
            b.Cylinder("King statue body", V(0f, 9.2f, 63f), 2.4f, 2.6f, k.wallNoClimb, true, true);
            b.Cylinder("King statue collar", V(0f, 11.8f, 63f), 3f, .3f, k.wallNoClimb, true, true);
            b.Sphere("King statue head", V(0f, 12.9f, 63f), 1.7f, k.wallNoClimb).AddComponent<Gameplay.NoClimbSurface>();
            Solid(b, "King statue cross (upright)", V(-.2f, 13.5f, 62.8f), V(.2f, 14f, 63.2f));
            Solid(b, "King statue cross (bar)", V(-.5f, 13.65f, 62.8f), V(.5f, 13.85f, 63.2f));
            // The falling chandeliers (§5): ChandelierRain draws them.
            var rain = b.Group("Chandelier rain");
            rain.gameObject.AddComponent<ChandelierRain>().Configure(k.rankGold);
            // A drop anywhere in x -13..13 reaches 4.5 m round it: past the landing's edges.
            SpanAt(b, "Chandelier rain span", V(0f, 8f, 55f), Vector3.right, -17.5f, 17.5f);
        }

        // ------------------------------------------------------------------ beats 3 ~ 5: a wing (s = -1 west, +1 east)

        // Wing stair 1 (out from the landing, 12 steps), the turning landing (two carpets flowing back towards
        // the stair), wing stair 2 (north, 12 steps), the window gallery with four vents, the bridge to the
        // dais. The east wing is the same code with x -> -x: turning ways, the carpets' and the wind's
        // directions follow. Returns the wing's portcullis (down only when the wing is shut).
        static GameObject Wing(CourseBuilder b, float s)
        {
            var k = b.kit;
            string side = s < 0 ? "W" : "E";
            float d1 = 4f / 3f, d2 = 14f / 12f;

            // Wing stair 1: x 14..30 (outwards), z 56..64, y 8 -> 11, 12 steps 0.25 x 1.33, railed both sides.
            FlightX(b, "Wing stair 1 " + side, s, 14f, 12, d1, 56f, 64f, 8f);
            Mass(b, "Wing stair 1 top step " + side, s * (14f + 11f * d1), s * 30f, 56f, 64f, 11f);
            RailAlongX(b, "Wing stair 1 rail S " + side, s, 14f, 12, d1, 55.7f, 56f, 8f);
            RailAlongX(b, "Wing stair 1 rail N " + side, s, 14f, 12, d1, 64f, 64.3f, 8f);
            // Two chandeliers (11 pendulums) swinging across the stair (z), axes at the stair + 7.3: phases 0 / 2.2.
            float[] across = { 19f, 26f }, phase = { 0f, 2.2f };
            for (int i = 0; i < 2; i++)
            {
                float x = across[i];
                float top = StepTopMax(x - 14f - 1.1f, x - 14f + 1.1f, d1, 8f);
                var pendulum = Hazard(b, k.pendulum, "11 Pendulum (chandelier) " + side + (i + 1), V(s * x, top - .1f, 60f), s < 0 ? 90f : -90f, phase[i]);
                Span(pendulum, Vector3.forward, -6f, 6f, SlopeY(x - 14f, d1, 8f));
                GantryZ(b, "Chandelier beam " + side + (i + 1), s * x, 60f, top - .1f + 7.3f, 6.4f);
            }

            // The turning landing: x 30..40, z 54..66, y 11. Two 08 conveyors (each 10 x 6 m) cover it, flowing
            // back towards wing stair 1 at 1.5 m/s (their floor block is under them, top 10.6).
            Mass(b, "Turning landing " + side, s * 30f, s * 40f, 54f, 66f, 10.6f);
            foreach (float z in new[] { 57f, 63f })
            {
                var belt = Hazard(b, k.conveyor, "08 Carpet conveyor " + side + " z" + z, V(s * 35f, 10.6f, z), s < 0 ? -90f : 90f);
                belt.transform.localScale = new Vector3(1f, 1f, 1.25f);   // 8 m -> 10 m along the flow
                // The belt's own rims would be kerbs in the middle of the landing: the landing has its railings.
                ForChildren(belt, "Belt rim", t => t.gameObject.SetActive(false));
            }
            Rail(b, "Turning landing rail S " + side, s * 30f, s * 40f, 53.7f, 54f, 11f);
            Rail(b, "Turning landing rail (stair 1 side, south) " + side, s * 29.7f, s * 30f, 54f, 56f, 11f);
            Rail(b, "Turning landing rail (stair 1 side, north) " + side, s * 29.7f, s * 30f, 64f, 66f, 11f);
            Rail(b, "Turning landing rail N " + side, s * 30f, s * 32f, 66f, 66.3f, 11f);

            // Wing stair 2: x 32..40, z 66..80, y 11 -> 14, 12 steps 0.25 x 1.17; the hall wall on the outer side.
            float x0 = s * 32f, x1 = s * 40f;
            FlightZ(b, "Wing stair 2 " + side, Mathf.Min(x0, x1), Mathf.Max(x0, x1), 66f, 12, d2, 11f);
            Mass(b, "Wing stair 2 top step " + side, x0, x1, 66f + 11f * d2, 80f, 14f);
            RailAlongZ(b, "Wing stair 2 rail " + side, Mathf.Min(s * 31.7f, s * 32f), Mathf.Max(s * 31.7f, s * 32f), 66f, 12, d2, 11f);
            // One chandelier across this stair (x), phase 1.1. 30° instead of 55°: the stair is 8 m between
            // its railing and the hall wall, and a 55° swing would put the ball 2 m into the wall.
            {
                float top = StepTopMax(73f - 66f - 1.1f, 73f - 66f + 1.1f, d2, 11f);
                var pendulum = Hazard(b, k.pendulum, "11 Pendulum (chandelier) " + side + "3", V(s * 36f, top - .1f, 73f), 0f, 1.1f);
                var swing = pendulum != null ? pendulum.GetComponentInChildren<PendulumSwing>(true) : null;
                if (swing != null) swing.SwingAngle = 30f;
                Span(pendulum, Vector3.right, -4.1f, 4.1f, SlopeY(73f - 66f, d2, 11f));
                GantryX(b, "Chandelier beam " + side + "3", s, 36f, 73f, top - .1f + 7.3f);
            }

            // The window gallery: x 13.8..40, z 80..86, y 14, 6 wide; its south edge open to the hall 18 m below.
            Mass(b, "Window gallery " + side, s * 13.8f, s * 40f, 80f, 86f, 14f);
            // Rest spots: CP2's (x 35.5..40), between the vents, CP3's before the bridge.
            Marker(b, "Rest (CP2) " + side, s * 35.5f, s * 40f, 80f, 86f, 14f);
            foreach (float x in new[] { 30.5f, 25.5f, 20.5f })
                Marker(b, "Rest between the vents " + side, s * x, s * (x + 2f), 80f, 86f, 14f);
            Marker(b, "Rest before the bridge (CP3) " + side, s * 13.8f, s * 17.5f, 80f, 86f, 14f);
            // Four 15 vents (the windows) in the north wall blowing into the hall, a quarter of 6.8 s apart.
            float[] ventX = { 34f, 29f, 24f, 19f }, ventPhase = { 0f, 1.7f, 3.4f, 5.1f };
            for (int i = 0; i < 4; i++)
            {
                float x = s * ventX[i];
                Hazard(b, k.airVent, "15 Wind vent (window) " + side + (i + 1), V(x, 14f, 86f), 180f, ventPhase[i]);
                // The window over it: glass and a frame in the wall.
                Picture(b, "Window", V(x - 1.4f, 17f, 85.9f), V(x + 1.4f, 24f, 85.98f), k.glass);
                Picture(b, "Window frame", V(x - 1.6f, 16.8f, 85.85f), V(x + 1.6f, 17f, 86f), k.rankGold);
                Picture(b, "Window frame", V(x - 1.6f, 24f, 85.85f), V(x + 1.6f, 24.2f, 86f), k.rankGold);
                // Its wind (3 x 3 x 6 m) covers the gallery's 6 m.
                SpanAt(b, "Wind span " + side + (i + 1), V(x, 14f, 83f), Vector3.forward, -3.2f, 3f);
            }
            b.Checkpoint(2, V(s * 38f, 14f, 83f), s < 0 ? 90f : 270f, 5.5f);
            b.Checkpoint(3, V(s * 15.6f, 14f, 83f), s < 0 ? 90f : 270f, 5.5f);

            // The 12 bridge to the dais (x 6..13.8): two plates on hinges 8 m apart, the prefab's way across (x).
            // Cycle 9 s (open 4, warning 1, folding 1.75 - the prefab's turning-speed limit, 0.25 more than the
            // doc's 1.5 - closed 0.5, unfolding 1.75), phase 0 in both wings. Its own landings are the gallery and
            // the dais here.
            var bridge = Hazard(b, k.foldingBridge, "12 Bridge to the dais " + side, V(s * 9.9f, 14f, 83f), 0f, 0f);
            var folding = bridge != null ? bridge.GetComponent<FoldingBridge>() : null;
            if (folding != null)
            {
                folding.OpenDuration = 4f;
                folding.WarningDuration = 1f;
                folding.FoldDuration = 1.5f;
                folding.ClosedDuration = .5f;
            }
            ForChildren(bridge, "LeftLanding", t => t.gameObject.SetActive(false));
            ForChildren(bridge, "RightLanding", t => t.gameObject.SetActive(false));

            // The portcullis at this wing's entrance on the central landing (x 14, z 56..64): down only when the
            // round shuts the wing (3 or 4 a side, GrandStairHall).
            var gate = Solid(b, "Wing portcullis " + side, V(s * 13.7f, 8f, 56f), V(s * 14.1f, 13f, 64f));
            gate.GetComponent<Renderer>().sharedMaterial = k.trimBlack;
            for (int i = 0; i < 5; i++)
                Picture(b, "Portcullis bar", V(s * 13.65f, 8f, 56.6f + i * 1.7f), V(s * 14.15f, 13.2f, 56.9f + i * 1.7f), k.wallNoClimb).transform.SetParent(gate.transform, true);
            gate.SetActive(false);
            return gate;
        }

        // A beam over a pendulum swinging along z (posts out of the ball's reach, 6 m from the axis).
        static void GantryZ(CourseBuilder b, string name, float x, float axisZ, float pivotY, float half)
        {
            float lo = axisZ - half, hi = axisZ + half;
            Solid(b, name + " post S", V(x - .4f, Deep, lo - .4f), V(x + .4f, pivotY + 1f, lo + .4f));
            Solid(b, name + " post N", V(x - .4f, Deep, hi - .4f), V(x + .4f, pivotY + 1f, hi + .4f));
            Solid(b, name, V(x - .4f, pivotY + .5f, lo - .4f), V(x + .4f, pivotY + 1f, hi + .4f));
        }

        // A beam over a pendulum swinging along x: one post on the open side, the other end in the hall wall.
        static void GantryX(CourseBuilder b, string name, float s, float x, float z, float pivotY)
        {
            float post = x - 6.4f;
            Solid(b, name + " post", V(s * (post - .4f), Deep, z - .4f), V(s * (post + .4f), pivotY + 1f, z + .4f));
            Solid(b, name, V(s * (post - .4f), pivotY + .5f, z - .4f), V(s * 40f, pivotY + 1f, z + .4f));
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
