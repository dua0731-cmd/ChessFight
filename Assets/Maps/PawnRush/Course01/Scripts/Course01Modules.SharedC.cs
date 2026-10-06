using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // Shared section C (the fiercest, then the final climb): M12..M14.
    public static partial class Course01Modules
    {
        // M12 three bridges (36 m): pick one of three 3 m ways. Left and right: pendulum timing; the
        // middle: straight, but jumping between stones in the crowd.
        static void M12(CourseBuilder b, CourseModule m)
        {
            var k = b.kit;
            b.Floor("Merge floor", -15f, 15f, 0f, 6f);
            SideRails(b, 0f, 6f);
            b.Floor("Left bridge", -12f, -9f, 6f, 30.5f);
            b.Floor("Right bridge", 9f, 12f, 6f, 30.5f);
            Hazard(b, k.pendulum, "11 Pendulum L1", V(-10.5f, 0f, 12f), 0f, 0f);
            Hazard(b, k.pendulum, "11 Pendulum L2", V(-10.5f, 0f, 24f), 0f, 2.2f);
            Hazard(b, k.pendulum, "11 Pendulum R1", V(10.5f, 0f, 12f), 0f, 2.2f);      // the left pair's mirror
            Hazard(b, k.pendulum, "11 Pendulum R2", V(10.5f, 0f, 24f), 0f, 0f);
            foreach (float z in new[] { 12f, 24f })
            {
                Gantry(b, "Pendulum frame L" + z.ToString("0"), -10.5f, z);
                Gantry(b, "Pendulum frame R" + z.ToString("0"), 10.5f, z);
            }
            // Stepping stones, 3 x 3, every gap 2.5 m (from the merge floor to the back floor).
            float[][] stones = { new[] { 8.5f, 11.5f }, new[] { 14f, 17f }, new[] { 19.5f, 22.5f }, new[] { 25f, 28f } };
            foreach (var s in stones)
            {
                b.Floor("Stepping stone", -1.5f, 1.5f, s[0], s[1]);
                b.Cylinder("Stone post", V(0f, -8f, (s[0] + s[1]) * .5f), 1f, 7f, k.wallNoClimb, true, true);
            }
            b.Floor("Back floor", -15f, 15f, 30.5f, 36f);
            b.Checkpoint(10, 2f);
            b.KillFloor(36f);
            m.Configure(ModuleId.M12_ThreeBridges, "세 갈래 다리", 36f, 0f, false, Line(36f));
        }

        // M13 rook-wall plaza (30 m): the fiercest cliffs. Two rook walls cross the plaza in opposite
        // directions; the band between them (z 11..19) is the breather.
        static void M13(CourseBuilder b, CourseModule m)
        {
            b.Floor("Plaza", -15f, 15f, 0f, 30f);                          // edges: cliffs
            RookWall(b, "A", V(0f, 0f, 9f), 12f, 6f, 0f);
            RookWall(b, "B", V(0f, 0f, 21f), 12f, 6f, 3f);                  // always on the other side of A
            b.Checkpoint(11, 1f);
            b.KillFloor(30f);
            m.Configure(ModuleId.M13_RookWallPlaza, "룩 벽 광장", 30f, 0f, false, Line(30f));
        }

        // M14 eighth-rank tower (34 m, 0 -> +6): the last shove ends in climbing again, not in a fall.
        // The middle wall is the fastest (two 3 m faces with a 0.8 m ledge, about 5 stamina without a
        // rest); the side stairs rest on a shelf, for whoever has sprinted their stamina away.
        static void M14(CourseBuilder b, CourseModule m)
        {
            var k = b.kit;
            b.Floor("Forecourt", -15f, 15f, 0f, 6f);
            SideRails(b, 0f, 6f);
            b.Wall("Tower wall (first face)", -6f, 6f, -1f, 3f, 6f, 6.8f, true);
            b.Wall("Tower wall (top)", -6f, 6f, -1f, 6f, 6.8f, 34f, true);
            b.Wall("Side stair L (shelf)", -15f, -6f, -1f, 3f, 6f, 12f, true);
            b.Wall("Side stair R (shelf)", 6f, 15f, -1f, 3f, 6f, 12f, true);
            b.Wall("Side stair L (top)", -15f, -6f, -1f, 6f, 12f, 34f, true);
            b.Wall("Side stair R (top)", 6f, 15f, -1f, 6f, 12f, 34f, true);
            SideRails(b, 6f, 12f, 3f);
            SideRails(b, 12f, 34f, 6f);
            b.Rail("Back rail", -15.5f, 15.5f, 34f, 34.5f, 6f);
            RankMarker.Build(b.parent, "Rank 8", V(0f, 3.4f, 6.76f), 8, 8f, 1.6f, k.rankGold);
            // Places for the rules to come: the last 8 m without grabbing, the finish line (global z 501).
            b.Trigger<NoGrabZone>("No-grab zone", V(-15f, 6f, 18f), V(15f, 10f, 26f));
            b.Trigger<FinishZone>("Finish line", V(-15f, 6f, 25.8f), V(15f, 9f, 26.2f));
            b.Box("Finish stripe", V(-15f, 6f, 25.75f), V(15f, 6.02f, 26.25f), k.rankGold, false, false);
            // Behind the spectators' stand: the 8th-rank crown, 10 m, seen from the start. No collisions.
            b.Wall("Crown plinth", -3f, 3f, -8f, 6f, 34.5f, 38f, false);
            var crown = b.Group("8th rank crown");
            crown.localPosition = V(0f, 6f, 36.25f);
            var before = b.parent;
            b.parent = crown;
            b.Cylinder("Foot", V(0f, 0f, 0f), 4f, 1.5f, k.rankGold, false);
            b.Cylinder("Body", V(0f, 1.5f, 0f), 2.4f, 4.5f, k.rankGold, false);
            b.Cylinder("Crown band", V(0f, 6f, 0f), 3.4f, 1.2f, k.rankGold, false);
            for (int i = 0; i < 5; i++)
            {
                var point = b.Box("Crown point", V(-.25f, 0f, -.25f), V(.25f, 1.1f, .25f), k.rankGold, false, false);
                float a = i * Mathf.PI * 2f / 5f;
                point.transform.localPosition = V(Mathf.Sin(a) * 1.45f, 7.75f, Mathf.Cos(a) * 1.45f);
            }
            b.Box("Cross upright", V(-.3f, 7.2f, -.3f), V(.3f, 10f, .3f), k.rankGold, false, false);
            b.Box("Cross bar", V(-1f, 8.9f, -.3f), V(1f, 9.5f, .3f), k.rankGold, false, false);
            b.parent = before;
            b.Checkpoint(12, 2f);
            b.KillFloor(34f);
            m.Configure(ModuleId.M14_EighthRank, "8랭크 탑", 34f, 6f, false,
                new[] { V(0f, 0f, 0f), V(0f, 0f, 6f), V(0f, 3f, 6f), V(0f, 3f, 6.8f), V(0f, 6f, 6.8f), V(0f, 6f, 34f) });
        }

        // ------------------------------------------------------------------ extensions (off by default)

        // X1 conveyor hill (after M4, 24 m, 16 wide): 06 sliding walls, then two 08 conveyors running
        // back at 3 m/s with plain floor at the edges (x +-6.6..8).
        static void X1(CourseBuilder b, CourseModule m)
        {
            var k = b.kit;
            b.Rail("Narrowing L", -15.5f, -8f, 0f, .5f, 0f);
            b.Rail("Narrowing R", 8f, 15.5f, 0f, .5f, 0f);
            b.Floor("Floor", -8f, 8f, 0f, 12f);
            b.Floor("Floor between conveyors", -.4f, .4f, 12f, 20f);
            b.Floor("Edge L", -8f, -6.4f, 12f, 20f);
            b.Floor("Edge R", 6.4f, 8f, 12f, 20f);
            b.Floor("Floor after", -8f, 8f, 20f, 24f);
            SideRails(b, 0f, 24f, 0f, 8f);
            Hazard(b, k.slidingWalls, "06 Sliding walls", V(0f, 0f, 5f), 0f, 0f);
            b.Obstacle(k.conveyor, "08 Conveyor L", V(-3.4f, -.4f, 16f));    // its top meets the floor
            b.Obstacle(k.conveyor, "08 Conveyor R", V(3.4f, -.4f, 16f));
            b.KillFloor(24f);
            m.Configure(ModuleId.X1_ConveyorHill, "컨베이어 언덕", 24f, 0f, false, Line(24f));
        }

        // X2 hammer hall (after M7, 26 m, 15 wide, cliffs): two 04 hammers turning opposite ways in
        // step, then 06 sliding walls.
        static void X2(CourseBuilder b, CourseModule m)
        {
            var k = b.kit;
            b.Floor("Floor", -7.5f, 7.5f, 0f, 26f);
            b.Obstacle(k.hammer, "04 Hammer L", V(-3.5f, 0f, 8f));
            var right = b.Obstacle(k.hammer, "04 Hammer R", V(3.5f, 0f, 8f));
            foreach (var motion in right.GetComponentsInChildren<ProtectKing.ObstacleMotion>(true))
                motion.degreesPerSecond = -motion.degreesPerSecond;
            Hazard(b, k.slidingWalls, "06 Sliding walls", V(0f, 0f, 19f), 0f, 0f);
            b.KillFloor(26f);
            m.Configure(ModuleId.X2_HammerHall, "해머 회랑", 26f, 0f, false, Line(26f));
        }

        // X3 fast-bar island (after M12, 26 m, 30 wide, cliffs): two 12 m bars at 85 degrees a second.
        static void X3(CourseBuilder b, CourseModule m)
        {
            b.Floor("Floor", -15f, 15f, 0f, 26f);
            SpinBar(b, "L", V(-7f, 0f, 13f), 12f, 85f);
            SpinBar(b, "R", V(7f, 0f, 13f), 12f, -85f);
            b.KillFloor(26f);
            m.Configure(ModuleId.X3_FastBarIsland, "빠른 막대 섬", 26f, 0f, false, Line(26f));
        }
    }
}
