using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // Start and shared section A (learning, no cliffs until M5): M0..M5.
    public static partial class Course01Modules
    {
        // M0 start square: 14 x 30. Both teams on the same line, the 8th-rank tower ahead.
        static void M0(CourseBuilder b, CourseModule m)
        {
            var k = b.kit;
            b.Floor("Floor", -15f, 15f, 0f, 14f);
            b.Wall("Back wall", -15.5f, 15.5f, 0f, 4f, -1f, 0f, false);
            SideRails(b, 0f, 14f);
            // Sinks under the floor when the countdown reaches 0 (new simple device).
            var bar = b.Box("Start bar", V(-15f, 0f, 13.75f), V(15f, 1.2f, 14.25f), k.hazard, true);
            bar.AddComponent<StartBar>();
            // White's six places at x -4/-7/-10, z 9 and 5, facing +z; black is the mirror.
            var white = b.In(TeamMirror.WhiteName, () =>
            {
                int index = 0;
                foreach (float z in new[] { 9f, 5f })
                    foreach (float x in new[] { -4f, -7f, -10f })
                    {
                        var spot = b.Group("Start " + index);
                        spot.localPosition = V(x, 0f, z);
                        spot.gameObject.AddComponent<SpawnPoint>().Configure(index++, Teams.White);
                    }
            });
            TeamMirror.MirrorTeam(white);
            b.Checkpoint(0, 7f);
            var sign = RankMarker.Build(b.parent, "Rank 2", V(0f, 1.2f, .04f), 2, 8f, 1.6f, k.rankGold);
            sign.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            b.KillFloor(14f);
            m.Configure(ModuleId.M0_Start, "출발 광장", 14f, 0f, false, Line(14f));
        }

        // M1 spin-bar yard: the first jump timing. Two 12 m bars turning opposite ways; the 2 m middle
        // lane (x -1..1) is out of their reach - safe, but crowded.
        static void M1(CourseBuilder b, CourseModule m)
        {
            b.Floor("Floor", -15f, 15f, 0f, 22f);
            SideRails(b, 0f, 22f);
            SpinBar(b, "L", V(-7f, 0f, 11f), 12f, 65f);
            SpinBar(b, "R", V(7f, 0f, 11f), 12f, -65f);
            b.KillFloor(22f);
            m.Configure(ModuleId.M1_SpinBarYard, "회전 막대 마당", 22f, 0f, false, Line(22f));
        }

        // M2 pusher corridor: the first push. The side walls keep everyone on the course.
        static void M2(CourseBuilder b, CourseModule m)
        {
            var k = b.kit;
            b.Floor("Floor", -15f, 15f, 0f, 28f);
            SideRails(b, 0f, 28f);
            Hazard(b, k.pusher, "03 Pusher L", V(-8.5f, 0f, 8f), 0f, 0f);
            Hazard(b, k.pusher, "03 Pusher R", V(8.5f, 0f, 8f), 0f, 2f);     // half of the 4 s cycle: L's mirror
            Hazard(b, k.pusher, "03 Pusher C", V(0f, 0f, 20f), 0f, 1f);
            b.KillFloor(28f);
            m.Configure(ModuleId.M2_PusherCorridor, "밀대 복도", 28f, 0f, false, Line(28f));
        }

        // M3 third rank: the first height, three ways up. Ramps at the sides (longer, crowded), the
        // climbing wall (straight, 2.5 stamina), the rising tiles in the middle (up to 6 s wait).
        static void M3(CourseBuilder b, CourseModule m)
        {
            var k = b.kit;
            b.Floor("Floor", -15f, 15f, 0f, 13.3f);
            // The pit the tiles sink into (6.7 x 3.5 x 6.7 under the tiles at z 13.3..20).
            b.Box("Floor beside the pit L", V(-15f, -4.5f, 13.3f), V(-3.35f, 0f, 20f), k.floorChecker);
            b.Box("Floor beside the pit R", V(3.35f, -4.5f, 13.3f), V(15f, 0f, 20f), k.floorChecker);
            b.Box("Pit front wall", V(-3.35f, -4.5f, 12.8f), V(3.35f, -1f, 13.3f), k.floorChecker);
            b.Box("Pit bottom", V(-3.35f, -4.5f, 13.3f), V(3.35f, -3.5f, 20f), k.floorChecker);
            // The step: its face at z 20 is the climbing wall (x +-3.35..9), its top the upper level.
            b.Wall("Third rank step", -15f, 15f, -4.5f, 3f, 20f, 22f, true);
            b.Ramp("Ramp L", -15f, -9f, 10f, 20f, 0f, 3f);
            b.Ramp("Ramp R", 9f, 15f, 10f, 20f, 0f, 3f);
            SideRails(b, 0f, 10f);
            b.Slope("Ramp rail L", -15.5f, -15f, 10f, 20f, 0f, 3f, 0f, 1.2f, k.wallNoClimb, true);
            b.Slope("Ramp rail R", 15f, 15.5f, 10f, 20f, 0f, 3f, 0f, 1.2f, k.wallNoClimb, true);
            SideRails(b, 20f, 22f, 3f);
            // 05 rising tiles: a raised tile's top meets the upper level.
            b.Obstacle(k.risingTiles, "05 Rising tiles", V(0f, 0f, 16.65f));
            RankMarker.Build(b.parent, "Rank 3 L", V(-6.2f, .5f, 19.96f), 3, 5f, 1.6f, k.rankGold);
            RankMarker.Build(b.parent, "Rank 3 R", V(6.2f, .5f, 19.96f), 3, 5f, 1.6f, k.rankGold);
            b.KillFloor(22f);
            m.Configure(ModuleId.M3_ThirdRank, "3랭크 단", 22f, 3f, false,
                new[] { V(0f, 0f, 0f), V(0f, 0f, 20f), V(0f, 3f, 20f), V(0f, 3f, 22f) });
        }

        // M4 clock-gate terrace: three gate pairs, go through the open one (at most ~2.5 s waiting).
        static void M4(CourseBuilder b, CourseModule m)
        {
            var k = b.kit;
            b.Floor("Floor", -15f, 15f, 0f, 26f);
            SideRails(b, 0f, 26f);
            Hazard(b, k.clockGates, "09 Clock gates L", V(-10.3f, 0f, 12f), 0f, 0f);
            Hazard(b, k.clockGates, "09 Clock gates C", V(0f, 0f, 12f), 0f, 1.25f);
            Hazard(b, k.clockGates, "09 Clock gates R", V(10.3f, 0f, 12f), 0f, 2.5f);   // half of the 5 s cycle
            // Filling between the gates. Climbable on purpose: about 5 s over, so waiting is faster.
            b.Wall("Gate filler L", -6f, -4.3f, 0f, 5.8f, 11.4f, 12.6f, true);
            b.Wall("Gate filler R", 4.3f, 6f, 0f, 5.8f, 11.4f, 12.6f, true);
            b.Wall("Gate filler LL", -15f, -14.6f, 0f, 5.8f, 11.4f, 12.6f, true);
            b.Wall("Gate filler RR", 14.6f, 15f, 0f, 5.8f, 11.4f, 12.6f, true);
            b.Checkpoint(1, 1f);
            b.KillFloor(26f);
            m.Configure(ModuleId.M4_ClockTerrace, "시계 문 테라스", 26f, 0f, false, Line(26f));
        }

        // M5 disc pit: jump from disc to disc. The first miss lands in a pit, not off a cliff: climb
        // its front wall back out (about 8 s lost). The 1 m beams along the sides are the risky way.
        static void M5(CourseBuilder b, CourseModule m)
        {
            var k = b.kit;
            b.Wall("Front edge", -13f, 13f, -4f, 0f, 0f, 2f, true);            // its back is the pit's climbable wall
            b.Box("Pit bottom", V(-12f, -4f, 2f), V(12f, -3f, 26f), k.floorChecker);
            b.Wall("Beam L", -13f, -12f, -4f, 0f, 2f, 26f, false);             // the pit's sides cannot be climbed
            b.Wall("Beam R", 12f, 13f, -4f, 0f, 2f, 26f, false);
            b.Wall("Back edge", -13f, 13f, -4f, 0f, 26f, 30f, false);           // nor its back
            // 01 spinning discs, 8 m across, top at y 0 (the prefab's top is 0.4 over its root).
            // Left and middle turn clockwise, right counter-clockwise; 2 m gaps between the rows.
            Vector3[] discs = { V(-6f, 0f, 6f), V(6f, 0f, 6f), V(0f, 0f, 14f), V(-6f, 0f, 22f), V(6f, 0f, 22f) };
            for (int i = 0; i < discs.Length; i++)
            {
                var disc = b.Obstacle(k.spinningDisc, "01 Spinning disc " + (i + 1), discs[i] - Vector3.up * .4f);
                var surface = disc.GetComponent<ObstacleSurface>();
                if (surface != null) surface.counterClockwise = discs[i].x > 0f;
                b.Cylinder("Disc post " + (i + 1), V(discs[i].x, -3f, discs[i].z), 1.2f, 2.6f, k.wallNoClimb, true, true);
            }
            b.KillFloor(30f);
            m.Configure(ModuleId.M5_DiscPit, "원판 구덩이", 30f, 0f, false, Line(30f));
        }
    }
}
