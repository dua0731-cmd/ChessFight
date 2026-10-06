using UnityEngine;

namespace ChessFight.PawnRush
{
    // Shared section B (shoving and cliffs): M7..M10.
    public static partial class Course01Modules
    {
        // M7 rotating board (35 m, 30 -> 8): the teams meet again. The middle turns slowly but a chess
        // piece falls on it; the rim moves at 3.6 m/s and throws the careless off.
        static void M7(CourseBuilder b, CourseModule m)
        {
            var k = b.kit;
            b.Floor("Merge floor", -15f, 15f, 0f, 6f);
            SideRails(b, 0f, 6f);
            // The bridges stop 0.1 m short of the disc (its edge is at z 9 and 32). At the bridge's
            // corners (x +-4) the gap grows to about 0.8 m: check it can be stepped over.
            b.Floor("Entry bridge", -4f, 4f, 6f, 8.9f);
            RotatingBoardRound(b, V(0f, 0f, 20.5f), 23f, 18f);
            b.Cylinder("Board post", V(0f, -8f, 20.5f), 2f, 7.5f, k.wallNoClimb, true, true);
            Hazard(b, k.fallingPiece, "10 Falling piece", V(0f, 0f, 20.5f), 0f, 0f);
            b.Floor("Exit bridge", -4f, 4f, 32.1f, 35f);
            b.Checkpoint(4, 2f);
            b.KillFloor(35f);
            m.Configure(ModuleId.M7_RotatingBoard, "회전 체스판", 35f, 0f, false, Line(35f));
        }

        // M8 pendulum hall (36 m, 0 -> +3): two heights. Below, time the three pendulums; above,
        // climb 3 m to a 2 m castle beam and shove on a narrow path.
        static void M8(CourseBuilder b, CourseModule m)
        {
            var k = b.kit;
            b.Floor("Start floor", -10f, 10f, 0f, 3f);
            b.Wall("Climb base L", -10f, -8f, 0f, 3f, 1f, 3f, true);
            b.Wall("Climb base R", 8f, 10f, 0f, 3f, 1f, 3f, true);
            b.Wall("Castle beam L", -10f, -8f, 2f, 3f, 3f, 33f, false);      // cliffs both sides
            b.Wall("Castle beam R", 8f, 10f, 2f, 3f, 3f, 33f, false);
            b.Floor("Lower passage", -4f, 4f, 3f, 24f);                      // cliffs both sides
            float[] zs = { 7f, 14f, 21f };
            float[] phases = { 0f, 1.47f, 2.93f };
            for (int i = 0; i < 3; i++)
            {
                Hazard(b, k.pendulum, "11 Pendulum " + (i + 1), V(0f, 0f, zs[i]), 0f, phases[i]);
                // Unclimbable posts from below the cliff (a beam walker cannot climb over them).
                Gantry(b, "Pendulum frame " + (i + 1), 0f, zs[i]);
            }
            b.Ramp("Ramp", -4f, 4f, 24f, 33f, 0f, 3f);
            b.Wall("Upper floor", -10f, 10f, -1f, 3f, 33f, 36f, true);
            RankMarker.Build(b.parent, "Rank 5", V(-6f, .5f, 32.96f), 5, 3.5f, 1.6f, k.rankGold);
            b.Checkpoint(5, 1f, 0f, 20f);
            b.KillFloor(36f);
            m.Configure(ModuleId.M8_PendulumHall, "진자 회랑", 36f, 3f, false,
                new[] { V(0f, 0f, 0f), V(0f, 0f, 24f), V(0f, 3f, 33f), V(0f, 3f, 36f) });
        }

        // M9 folding bridges (22 m): two bridges folding out of step. When one folds, everyone crowds
        // onto the other; the worst wait is about 3.5 s.
        static void M9(CourseBuilder b, CourseModule m)
        {
            var k = b.kit;
            b.Floor("Front floor", -10f, 10f, 0f, 4f);
            // 12 folding bridge, turned 90 degrees to cross along +z: its landings meet both floors.
            Hazard(b, k.foldingBridge, "12 Folding bridge L", V(-4f, 0f, 11.05f), -90f, 0f);
            Hazard(b, k.foldingBridge, "12 Folding bridge R", V(4f, 0f, 11.05f), -90f, 6.75f);   // half of 13.5 s
            b.Floor("Back floor", -10f, 10f, 18.1f, 22f);
            b.Checkpoint(6, 1f, 0f, 20f);
            b.KillFloor(22f);
            // The path takes the left bridge: between the bridges (x -1..1) is a cliff.
            m.Configure(ModuleId.M9_FoldingBridges, "접이식 다리", 22f, 0f, false,
                new[] { V(0f, 0f, 0f), V(0f, 0f, 3f), V(-4f, 0f, 4f), V(-4f, 0f, 18.1f), V(0f, 0f, 19f), V(0f, 0f, 22f) });
        }

        // M10 knight plaza (38 m, 0 -> +3): the peak of section B. Dodge the cavalry's landing and
        // charge to reach the sixth-rank wall; or the jump pads, or the side ramps.
        static void M10(CourseBuilder b, CourseModule m)
        {
            var k = b.kit;
            b.Floor("Plaza", -15f, 15f, 0f, 23f);                          // edges: cliffs
            // 17 knight cavalry. K1 lands at (-3, 0, 6) and charges +x to x 5; K2 lands at (3, 0, 15)
            // and charges -x to x -5. The prefab lands 2 m behind its root and charges 8 m on.
            Hazard(b, k.knightCavalry, "17 Knight cavalry K1", V(-1f, 0f, 6f), 0f, 0f);
            Hazard(b, k.knightCavalry, "17 Knight cavalry K2", V(1f, 0f, 15f), 180f, 3.81f);   // half of 7.62 s
            // Their 3 m starting posts stand on the plaza: unclimbable.
            b.Wall("Cavalry post K1", -8.2f, -5.8f, 0f, 3f, 1.6f, 4.4f, false);
            b.Wall("Cavalry post K2", 5.8f, 8.2f, 0f, 3f, 16.6f, 19.4f, false);
            // 02 jump pads, made gentler (up 9, forward 4.5 m/s): about 4 m up, landing near z 25.
            // The original (14 / 8) throws a ragdoll off the plaza.
            foreach (float x in new[] { -9.5f, 9.5f })
            {
                var pad = b.Obstacle(k.jumpPad, "02 Jump pad (rank variant) " + (x < 0 ? "L" : "R"), V(x, 0f, 19f));
                var surface = pad.GetComponent<ProtectKing.ObstacleSurface>();
                if (surface != null) surface.launchVelocity = V(0f, 9f, 4.5f);
            }
            b.Ramp("Side ramp L", -15f, -12f, 13f, 23f, 0f, 3f);           // no outer railing
            b.Ramp("Side ramp R", 12f, 15f, 13f, 23f, 0f, 3f);
            b.Wall("Sixth rank wall and upper floor", -15f, 15f, -1f, 3f, 23f, 38f, true);
            RankMarker.Build(b.parent, "Rank 6", V(0f, .5f, 22.96f), 6, 8f, 1.6f, k.rankGold);
            b.Checkpoint(7, 1f);
            b.KillFloor(38f);
            m.Configure(ModuleId.M10_KnightPlaza, "기병 광장", 38f, 3f, false,
                new[] { V(0f, 0f, 0f), V(0f, 0f, 23f), V(0f, 3f, 23f), V(0f, 3f, 38f) });
        }
    }
}
