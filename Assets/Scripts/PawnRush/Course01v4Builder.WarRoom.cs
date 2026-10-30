using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // 승규 님's endgame (phase 3) course, grey boxes for the test scene (seoungtest, 10-30): the war room of the
    // three-phase level design, "read the next crossing, break through together" (Docs/KingRush/PAWN_RUSH_ENDGAME_RESEARCH.md).
    // Six a side, so the way is 12 m wide (x -6..6), running north from z 0 on y 0:
    //
    //   S1  z  0 .. 12    entry corridor: see both crossings and the others before going; the map table
    //                     beside it shows where the ferry is and which S4 bridge is open (CrossingSignal) CP 1
    //   S2  z 12 .. 32.4  first crossing: an 8 m ferry shuttling over the drop (ride it or wait)
    //   S3  z 32.4 .. 56.4 command landing: fixed floor, two rook walls each closing half the way; a second
    //                     map table at its end shows the two S4 bridges                                 CP 2
    //   S4  z 56.4 .. 70.5 last crossing: two 12 folding bridges side by side, half a cycle apart
    //   exit z 70.5 .. 94.5 runout, the 12 x 6 m terrace (CP 3), the open arch and four steps 1 m down
    //
    // A fall in S2 comes back to CP 1, in S4 to CP 2 (an 8 m fall respawns at the last checkpoint).
    public static partial class Course01v4Builder
    {
        const float WarHalf = 6f;                                   // half the way's width
        const float FerryHalf = 4f, FerryTravel = 6f, FerryPeriod = 14f;
        const float S1End = 12f, S3Start = S1End + 2f * (FerryHalf + FerryTravel) + .4f;   // 0.2 m gaps at the ends
        const float S3End = S3Start + 24f, BridgeHalf = 7.05f, S4End = S3End + 2f * BridgeHalf;

        static void WarRoom(CourseBuilder b)
        {
            var k = b.kit;
            Transform ferry = null, ferryAtS1 = null, ferryAtS3 = null;
            var bridges = new FoldingBridge[2];
            b.In("S1 Entry", () =>
            {
                b.Floor("S1 floor", -WarHalf, WarHalf, 0f, S1End, 0f);
                b.Wall("S1 back wall", -WarHalf - .5f, WarHalf + .5f, -1f, 4f, -.5f, 0f, false);
                WarRails(b, "S1", 0f, S1End);
                b.Checkpoint(1, V(0f, 0f, 3f), 0f, 12f);
            });

            b.In("S2 Ferry", () =>
            {
                // An ObstacleMotion slab carries whoever stands on it (the moving-surface handling of the
                // rotating boards). At either end it stops 0.2 m short of the floor; in the middle 6 m of
                // drop lies on each side. Max speed 6 * 2pi / 14 = 2.7 m/s, about a run.
                float middle = (S1End + S3Start) * .5f;
                var slab = b.Box("Device_Ferry", V(-FerryHalf, -.5f, middle - FerryHalf), V(FerryHalf, 0f, middle + FerryHalf), k.trimWhite);
                slab.AddComponent<CourseFloor>();
                var motion = slab.AddComponent<ObstacleMotion>();
                motion.kind = MotionKind.Translate;
                motion.axis = Vector3.forward;
                motion.distance = FerryTravel;
                motion.period = FerryPeriod;
                motion.shove = false;
                // Start at S1's edge (sin = -1 a quarter period before 0).
                motion.phase = .75f;
                ferry = slab.transform;
                // Where its middle is at either landing, for the map tables.
                ferryAtS1 = b.Group("Ferry middle at S1");
                ferryAtS1.localPosition = V(0f, -.25f, middle - FerryTravel);
                ferryAtS3 = b.Group("Ferry middle at S3");
                ferryAtS3.localPosition = V(0f, -.25f, middle + FerryTravel);
            });

            b.In("S3 Command landing", () =>
            {
                b.Floor("S3 floor", -WarHalf, WarHalf, S3Start, S3End, 0f);
                WarRails(b, "S3", S3Start, S3End);
                b.Checkpoint(2, V(0f, 0f, S3Start + 2f), 0f, 12f);
                // Each 6 m rook wall closes half the way and slides to the other half (course 01's C2).
                Span(RookWall(b, "S3 1", V(0f, 0f, S3Start + 10f), 6f, 3f, 6f, 0f), Vector3.right, -WarHalf, WarHalf, 0f);
                Span(RookWall(b, "S3 2", V(0f, 0f, S3Start + 18f), 6f, 3f, 6f, 3f), Vector3.right, -WarHalf, WarHalf, 0f);
            });

            b.In("S4 Folding bridges", () =>
            {
                // 12 is 14.1 m along its x (3 m landings either end, two 3.9 m panels) and 6 m wide; turned 90 it
                // crosses north. Half a cycle (13.5 s) apart, so one of the two is nearly always open.
                float middle = S3End + BridgeHalf;
                bridges[0] = Hazard(b, k.foldingBridge, "12 FoldingBridge S4 W", V(-3f, 0f, middle), 90f, 0f).GetComponentInChildren<FoldingBridge>();
                bridges[1] = Hazard(b, k.foldingBridge, "12 FoldingBridge S4 E", V(3f, 0f, middle), 90f, 6.75f).GetComponentInChildren<FoldingBridge>();
            });

            b.In("Exit", () =>
            {
                // 12 m runout with nothing pushing, the 12 x 6 m safe terrace, the open arch (12 m wide, 5.5 m high),
                // four 0.25 m steps down and the 4 m entry strip at y -1 where the shared mission room would begin.
                float terrace = S4End + 12f, arch = terrace + 6f;
                b.Floor("Runout", -WarHalf, WarHalf, S4End, terrace, 0f);
                b.Floor("Terrace", -WarHalf, WarHalf, terrace, arch, 0f);
                WarRails(b, "Exit", S4End, arch);
                b.Checkpoint(3, V(0f, 0f, terrace + 3f), 0f, 12f);
                b.Wall("Arch pier W", -WarHalf - 1f, -WarHalf, -1f, 5.5f, arch - .5f, arch + .5f, false);
                b.Wall("Arch pier E", WarHalf, WarHalf + 1f, -1f, 5.5f, arch - .5f, arch + .5f, false);
                b.Wall("Arch lintel", -WarHalf - 1f, WarHalf + 1f, 5.5f, 6.5f, arch - .5f, arch + .5f, false);
                for (int i = 0; i < 4; i++)
                    b.Floor("Step " + (i + 1), -WarHalf, WarHalf, arch + i * .5f, arch + (i + 1) * .5f, -.25f * (i + 1));
                b.Floor("Entry strip", -WarHalf, WarHalf, arch + 2f, arch + 6f, -1f);
                b.Wall("Mission room (not built) wall", -WarHalf - .5f, WarHalf + .5f, -1f, 4f, arch + 6f, arch + 6.5f, false);
            });

            b.In("Signals", () =>
            {
                // The map tables stand outside the east rail, their tops (1.3 m) just over it.
                var s1 = MapTable(b, "S1 map table", 4f, 10f);
                b.Box("Ferry strip", V(7.35f, 1.3f, 5f), V(7.65f, 1.33f, 8.4f), k.trimWhite, false, false);
                var from = b.Group("Ferry strip S1 end");
                from.localPosition = V(7.5f, 1.42f, 5.3f);
                var to = b.Group("Ferry strip S3 end");
                to.localPosition = V(7.5f, 1.42f, 8.1f);
                var marker = b.Box("Ferry marker", V(7.2f, 1.34f, 5f), V(7.8f, 1.5f, 5.6f), k.trimWhite, false, false);
                var ferryLamp = Lamp(b, "Ferry lamp", V(7.5f, 1.3f, 4.5f), .6f);
                s1.ShowFerry(ferry, ferryAtS1, ferryAtS3, marker.transform, from, to, ferryLamp);
                s1.ShowBridges(bridges, new[] { Lamp(b, "Bridge W lamp", V(7f, 1.3f, 9.3f), .6f), Lamp(b, "Bridge E lamp", V(8f, 1.3f, 9.3f), .6f) });

                var s3 = MapTable(b, "S3 map table", S3End - 7f, S3End - 1f);
                s3.ShowBridges(bridges, new[] { Lamp(b, "Bridge W lamp", V(7f, 1.3f, S3End - 2.5f), .9f), Lamp(b, "Bridge E lamp", V(8f, 1.3f, S3End - 2.5f), .9f) });
            });
        }

        // A map table outside the east rail, z0 .. z1, carrying a CrossingSignal.
        static CrossingSignal MapTable(CourseBuilder b, string name, float z0, float z1) =>
            b.Box(name, V(WarHalf + .5f, -1f, z0), V(WarHalf + 2.5f, 1.3f, z1), b.kit.trimBlack, true).AddComponent<CrossingSignal>();

        // A lamp on a map table (dark until its CrossingSignal lights it).
        static Renderer Lamp(CourseBuilder b, string name, Vector3 at, float diameter) =>
            b.Cylinder(name, at, diameter, .2f, b.kit.trimBlack, false).GetComponent<Renderer>();

        // 1.2 m marble rails along both sides of a fixed floor.
        static void WarRails(CourseBuilder b, string name, float z0, float z1)
        {
            b.Rail(name + " rail W", -WarHalf - .5f, -WarHalf, z0, z1, 0f);
            b.Rail(name + " rail E", WarHalf, WarHalf + .5f, z0, z1, 0f);
        }
    }
}
