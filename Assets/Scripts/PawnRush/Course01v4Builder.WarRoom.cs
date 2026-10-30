using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // 승규 님's endgame (phase 3) course for the test scene (seoungtest): the war room (Docs/KingRush/PAWN_RUSH_ENDGAME_RESEARCH.md).
    // v2 (10-30, R244): the look of the S1-S4 concept art (cream marble, gold lines, teal and gold rails, map tables,
    // bookshelves, blue banners) at the size and with the devices of the wardrobe concept (a rotating stage under a
    // sweeping bar, sliding walls and clock gates, swinging pendulums, lift tiles, conveyors under falling pieces, a
    // spinning disc, folding bridges). Six a side: the way is 12 m wide (x -6..6), running north from z 0, one line
    // (no side way past a device). y 0 up to the lift tiles, y 3 after them.
    //
    //   S1   z   0 .. 14    entry corridor, the map table (where the ferry is, which S4 bridge is open)      CP 1
    //   R1   z  14 .. 34    the rotating war table: a 16 m round board turning, a low bar sweeping over it
    //        z  34 .. 40    landing                                                                         CP 2
    //   S2   z  40 .. 60.4  the ferry: an 8 m deck shuttling over the drop (ride it or wait for it)
    //   R2   z 60.4 .. 88   the bookshelf hall: 06 sliding walls, then a pair of 09 clock gates             CP 3
    //   S3   z  88 .. 112   the command landing: compass inlay, two rook screens closing half the way each  CP 4
    //   R3   z 112 .. 132   the banner bridge: 10 m over the drop, two 11 pendulums swinging across
    //   R4   z 132 .. 143   the lift tiles: two 05 sets up to y 3                                           CP 5
    //   R5   z 143 .. 182   conveyors pushing back, then two 10 falling pieces with crates for cover        CP 6
    //   R6   z 182 .. 196   an 01 spinning disc over a gap, the second map table                           CP 7
    //   S4   z 196 .. 210.1 two 12 folding bridges side by side, half a cycle apart
    //   exit z 210.1 ..     runout, the 12 x 6 m terrace (CP 8), the open arch, four steps 1 m down
    //
    // A fall comes back to the last checkpoint (an 8 m fall respawns). The hall round it is looks only (no colliders).
    public static partial class Course01v4Builder
    {
        const float WarHalf = 6f;
        const float FerryHalf = 4f, FerryTravel = 6f, FerryPeriod = 14f;
        const float FerryGap0 = 40f, FerryGap1 = FerryGap0 + 2f * (FerryHalf + FerryTravel) + .4f;   // 0.2 m gaps at the ends
        const float Upper = 3f, LiftZ = 139.6f, UpperZ = 143f;
        const float DiscZ = 186f, S4Start = 196f, BridgeHalf = 7.05f, S4End = S4Start + 2f * BridgeHalf;

        static void WarRoom(CourseBuilder b)
        {
            var k = b.kit;
            Transform ferry = null, ferryAtS1 = null, ferryAtS3 = null;
            var bridges = new FoldingBridge[2];

            b.In("S1 Entry", () =>
            {
                WarFloor(b, "S1 floor", -WarHalf, WarHalf, 0f, 14f, 0f);
                WarWall(b, "S1 back wall", -WarHalf - .5f, WarHalf + .5f, -1f, 4f, -.5f, 0f);
                WarRails(b, "S1", 0f, 14f, 0f);
                b.Checkpoint(1, V(0f, 0f, 3f), 0f, 12f);
            });

            b.In("R1 Rotating war table", () =>
            {
                // A 16 m board turning 18 degrees a second (riders carried), a 15 m low bar (0.2 .. 0.52 m) sweeping
                // over it the other way: jump it or be swept off. Bridges 8 m wide on and off.
                WarFloor(b, "R1 bridge on", -4f, 4f, 14f, 16f, 0f);
                var board = RotatingBoardRound(b, V(0f, 0f, 24f), 16f, 18f);
                foreach (var r in board.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterial = r.gameObject == board ? WarRoomLook.Map : WarRoomLook.Teal;
                b.Cylinder("R1 board rim", V(0f, -.5f, 24f), 16.6f, .45f, WarRoomLook.Gold, false);
                SpinBar(b, "R1", V(0f, 0f, 24f), 15f, -60f);
                WarFloor(b, "R1 bridge off", -4f, 4f, 32f, 34f, 0f);
                WarFloor(b, "R1 landing", -WarHalf, WarHalf, 34f, FerryGap0, 0f);
                WarRails(b, "R1 landing", 34f, FerryGap0, 0f);
                b.Checkpoint(2, V(0f, 0f, 36f), 0f, 12f);
            });

            b.In("S2 Ferry", () =>
            {
                // A wooden map deck in a teal and gold frame, carried by an ObstacleMotion on its root (riders carried).
                // At either end it stops 0.2 m short of the floor; in the middle 6 m of drop lies each side.
                // Max speed 6 * 2pi / 14 = 2.7 m/s, about a run.
                float middle = (FerryGap0 + FerryGap1) * .5f;
                var root = new GameObject("Device_Ferry");
                root.transform.SetParent(b.parent, false);
                root.transform.localPosition = V(0f, 0f, middle);
                var motion = root.AddComponent<ObstacleMotion>();
                motion.kind = MotionKind.Translate;
                motion.axis = Vector3.forward;
                motion.distance = FerryTravel;
                motion.period = FerryPeriod;
                motion.shove = false;
                motion.phase = .75f;   // at S1's side when the clock is 0
                var before = b.parent;
                b.parent = root.transform;
                b.Box("Deck", V(-FerryHalf, -.5f, -FerryHalf), V(FerryHalf, 0f, FerryHalf), WarRoomLook.Wood).AddComponent<CourseFloor>();
                b.Box("Map", V(-FerryHalf + .6f, 0f, -FerryHalf + .6f), V(FerryHalf - .6f, .02f, FerryHalf - .6f), WarRoomLook.Map, false, false);
                foreach (float side in new[] { -1f, 1f })
                {
                    b.Box("Side beam", V(side * FerryHalf - .3f, 0f, -FerryHalf), V(side * FerryHalf + .3f, .5f, FerryHalf), WarRoomLook.Teal, true);
                    foreach (float end in new[] { -1f, 1f })
                        b.Box("Corner post", V(side * FerryHalf - .35f, 0f, end * FerryHalf - .35f), V(side * FerryHalf + .35f, .9f, end * FerryHalf + .35f), WarRoomLook.Gold, false, false);
                }
                b.parent = before;
                ferry = root.transform;
                ferryAtS1 = b.Group("Ferry middle at S1");
                ferryAtS1.localPosition = V(0f, 0f, middle - FerryTravel);
                ferryAtS3 = b.Group("Ferry middle at S3");
                ferryAtS3.localPosition = V(0f, 0f, middle + FerryTravel);
            });

            b.In("R2 Bookshelf hall", () =>
            {
                WarFloor(b, "R2 floor", -WarHalf, WarHalf, FerryGap1, 88f, 0f);
                WarRails(b, "R2", FerryGap1, 88f, 0f);
                b.Checkpoint(3, V(0f, 0f, FerryGap1 + 2f), 0f, 12f);
                // 06: a 3 m wall out of each side in turn (zigzag), dressed as bookshelves.
                WarRoomLook.Paint(Hazard(b, k.slidingWalls, "06 Sliding walls R2", V(0f, 0f, 70f), 0f, 0f), WarRoomLook.Wood);
                // 09: two 4 m doors opening in turn; marble either side closes the rest of the way.
                Hazard(b, k.clockGates, "09 Clock gates R2", V(0f, 0f, 80f), 0f, 0f);
                WarWall(b, "R2 gate side W", -WarHalf, -4.3f, 0f, 6f, 79.4f, 80.6f);
                WarWall(b, "R2 gate side E", 4.3f, WarHalf, 0f, 6f, 79.4f, 80.6f);
            });

            b.In("S3 Command landing", () =>
            {
                WarFloor(b, "S3 floor", -WarHalf, WarHalf, 88f, 112f, 0f);
                WarRails(b, "S3", 88f, 112f, 0f);
                b.Checkpoint(4, V(0f, 0f, 90f), 0f, 12f);
                Compass(b, V(0f, 0f, 100f));
                // Rook screens: each 6 m, closing half the way and sliding to the other half (course 01's C2).
                foreach (var (z, phase) in new[] { (95f, 0f), (106f, 3f) })
                {
                    var screen = Span(RookWall(b, "S3 " + z, V(0f, 0f, z), 6f, 3f, 6f, phase), Vector3.right, -WarHalf, WarHalf, 0f);
                    foreach (var r in screen.GetComponentsInChildren<Renderer>(true))
                        r.sharedMaterial = r.name == "Wall" ? WarRoomLook.Teal : WarRoomLook.Gold;
                }
            });

            b.In("R3 Banner bridge", () =>
            {
                // 10 m over the drop; two pendulums (balls 2.2 m, +-4.9 m) swinging across it, frames outside their reach.
                WarFloor(b, "R3 bridge", -5f, 5f, 112f, 132f, 0f);
                foreach (var (z, phase) in new[] { (118f, 0f), (126f, 2.2f) })
                {
                    Span(Hazard(b, k.pendulum, "11 Pendulum R3 " + z, V(0f, 0f, z), 0f, phase), Vector3.right, -6f, 6f, 0f, "R3");
                    WarRoomLook.Paint(b.Wall("R3 frame post W " + z, -7.8f, -7f, -3f, 8.3f, z - .4f, z + .4f, false), WarRoomLook.Teal);
                    WarRoomLook.Paint(b.Wall("R3 frame post E " + z, 7f, 7.8f, -3f, 8.3f, z - .4f, z + .4f, false), WarRoomLook.Teal);
                    WarRoomLook.Paint(b.Wall("R3 frame beam " + z, -7.8f, 7.8f, 7.8f, 8.3f, z - .4f, z + .4f, false), WarRoomLook.Gold);
                    // A banner hangs from the beam beside the pivot (looks only).
                    b.Box("R3 banner " + z, V(2.4f, 4.6f, z - .05f), V(4.8f, 7.8f, z + .05f), WarRoomLook.Banner, false, false);
                }
            });

            b.In("R4 Lift tiles", () =>
            {
                // Two 05 sets side by side (+-3 m, 6 s, half a cycle apart): step on a tile low, ride it up to y 3.
                WarFloor(b, "R4 floor", -WarHalf, WarHalf, 132f, 136f, 0f);
                b.Checkpoint(5, V(0f, 0f, 133f), 0f, 12f);
                WarRoomLook.Paint(Hazard(b, k.risingTiles, "05 Lift tiles R4 W", V(-3.35f, 0f, LiftZ), 0f, 0f), WarRoomLook.Wood);
                WarRoomLook.Paint(Hazard(b, k.risingTiles, "05 Lift tiles R4 E", V(3.35f, 0f, LiftZ), 0f, 3f), WarRoomLook.Wood);
                // The upper floor's front: marble, not climbed (the tiles are the way up).
                WarWall(b, "R4 upper front", -WarHalf, WarHalf, -2f, Upper, UpperZ, UpperZ + 1f, true);
            });

            b.In("R5 Conveyors and falling pieces", () =>
            {
                WarFloor(b, "R5 floor 1", -WarHalf, WarHalf, UpperZ + 1f, 146f, Upper);
                b.Checkpoint(6, V(0f, Upper, 145f), 0f, 12f);
                // Two 08 belts (6.4 x 8, 1.5 m/s back toward the start), their tops flush with the floor (0.4 over the root).
                foreach (float x in new[] { -3.2f, 3.2f })
                    Hazard(b, k.conveyor, "08 Conveyor R5 " + (x < 0f ? "W" : "E"), V(x, Upper - .4f, 150f), 0f, 0f);
                b.Box("R5 conveyor bed", V(-WarHalf, Upper - 2f, 146f), V(WarHalf, Upper - 1f, 154f), WarRoomLook.Deep, false, false);
                WarFloor(b, "R5 floor 2", -WarHalf, WarHalf, 154f, 182f, Upper);
                WarRails(b, "R5", UpperZ + 1f, 182f, Upper);
                // 10: warning circle (5 m) 1.4 s, the piece drops, a 5 m shockwave; a crate between blocks it.
                Hazard(b, k.fallingPiece, "10 Falling piece R5 W", V(-3f, Upper, 162f), 0f, 0f);
                Hazard(b, k.fallingPiece, "10 Falling piece R5 E", V(3f, Upper, 174f), 0f, 2.3f);
                foreach (var (x, z) in new[] { (2.6f, 159f), (-2.6f, 171f), (0f, 178.5f) })
                    b.Box("R5 crate", V(x - 1f, Upper, z - .75f), V(x + 1f, Upper + 1.5f, z + .75f), WarRoomLook.Wood, true);
            });

            b.In("R6 Disc", () =>
            {
                // An 8 m 01 disc over a gap: its surface spins a pawn round and out; the floor either side is 12 m.
                Disc(b, "R6", V(0f, Upper, DiscZ), false);
                WarFloor(b, "R6 landing", -WarHalf, WarHalf, 190f, S4Start, Upper);
                WarRails(b, "R6 landing", 190f, S4Start, Upper);
                b.Checkpoint(7, V(0f, Upper, 191.5f), 0f, 12f);
            });

            b.In("S4 Folding bridges", () =>
            {
                // 12 is 14.1 m along its x (3 m landings either end, two 3.9 m panels) and 6 m wide; turned 90 it
                // crosses north. Half a cycle (13.5 s) apart, so one of the two is nearly always open.
                float middle = S4Start + BridgeHalf;
                bridges[0] = Hazard(b, k.foldingBridge, "12 FoldingBridge S4 W", V(-3f, Upper, middle), 90f, 0f).GetComponentInChildren<FoldingBridge>();
                bridges[1] = Hazard(b, k.foldingBridge, "12 FoldingBridge S4 E", V(3f, Upper, middle), 90f, 6.75f).GetComponentInChildren<FoldingBridge>();
            });

            b.In("Exit", () =>
            {
                // 12 m runout with nothing pushing, the 12 x 6 m terrace, the open arch (12 m wide, 5.5 m high),
                // four 0.25 m steps down and the 4 m entry strip where the shared mission room would begin.
                float y = Upper, terrace = S4End + 12f, arch = terrace + 6f;
                WarFloor(b, "Runout", -WarHalf, WarHalf, S4End, terrace, y);
                WarFloor(b, "Terrace", -WarHalf, WarHalf, terrace, arch, y);
                WarRails(b, "Exit", S4End, arch, y);
                b.Checkpoint(8, V(0f, y, terrace + 3f), 0f, 12f);
                WarWall(b, "Arch pier W", -WarHalf - 1f, -WarHalf, y - 1f, y + 5.5f, arch - .5f, arch + .5f);
                WarWall(b, "Arch pier E", WarHalf, WarHalf + 1f, y - 1f, y + 5.5f, arch - .5f, arch + .5f);
                WarRoomLook.Paint(b.Wall("Arch lintel", -WarHalf - 1f, WarHalf + 1f, y + 5.5f, y + 6.5f, arch - .5f, arch + .5f, false), WarRoomLook.Gold);
                for (int i = 0; i < 4; i++)
                    WarFloor(b, "Step " + (i + 1), -WarHalf, WarHalf, arch + i * .5f, arch + (i + 1) * .5f, y - .25f * (i + 1));
                WarFloor(b, "Entry strip", -WarHalf, WarHalf, arch + 2f, arch + 6f, y - 1f);
                WarWall(b, "Mission room (not built) wall", -WarHalf - .5f, WarHalf + .5f, y - 2f, y + 4f, arch + 6f, arch + 6.5f);
                Rook(b, "Rook statue W", V(-WarHalf - 3f, y - 1f, arch + 3f));
                Rook(b, "Rook statue E", V(WarHalf + 3f, y - 1f, arch + 3f));
            });

            b.In("Signals", () =>
            {
                // Map tables outside the east rail, their tops (1.3 m over their floor) just over it.
                var s1 = MapTable(b, "S1 map table", 0f, 4f, 10f);
                b.Box("Ferry strip", V(7.35f, 1.31f, 5f), V(7.65f, 1.34f, 8.4f), WarRoomLook.Gold, false, false);
                var from = b.Group("Ferry strip S1 end");
                from.localPosition = V(7.5f, 1.42f, 5.3f);
                var to = b.Group("Ferry strip S3 end");
                to.localPosition = V(7.5f, 1.42f, 8.1f);
                var marker = b.Box("Ferry marker", V(7.2f, 1.34f, 5f), V(7.8f, 1.5f, 5.6f), WarRoomLook.Wood, false, false);
                s1.ShowFerry(ferry, ferryAtS1, ferryAtS3, marker.transform, from, to, Lamp(b, "Ferry lamp", V(7.5f, 1.3f, 4.5f), .6f));
                s1.ShowBridges(bridges, new[] { Lamp(b, "Bridge W lamp", V(7f, 1.3f, 9.3f), .6f), Lamp(b, "Bridge E lamp", V(8f, 1.3f, 9.3f), .6f) });

                var s4 = MapTable(b, "S4 map table", Upper, 190f, S4Start - .5f);
                s4.ShowBridges(bridges, new[] { Lamp(b, "Bridge W lamp", V(7f, Upper + 1.3f, S4Start - 2f), .9f), Lamp(b, "Bridge E lamp", V(8f, Upper + 1.3f, S4Start - 2f), .9f) });
            });

            b.In("Hall", () => Hall(b, -12f, S4End + 40f));
        }

        // ------------------------------------------------------------------ war room parts

        // A floor of cream marble with a gold line across it every 4 m (looks only).
        static void WarFloor(CourseBuilder b, string name, float x0, float x1, float z0, float z1, float top)
        {
            WarRoomLook.Paint(b.Floor(name, x0, x1, z0, z1, top), WarRoomLook.Marble);
            for (float z = Mathf.Ceil(z0 / 4f) * 4f; z < z1 - .1f; z += 4f)
                if (z > z0 + .1f) b.Box(name + " line", V(x0 + .3f, top, z - .06f), V(x1 - .3f, top + .015f, z + .06f), WarRoomLook.Inlay, false, false);
        }

        // Marble that cannot be climbed, in the hall's stone; `floor` when its top is walked on.
        static void WarWall(CourseBuilder b, string name, float x0, float x1, float y0, float y1, float z0, float z1, bool floor = false) =>
            WarRoomLook.Paint(b.Wall(name, x0, x1, y0, y1, z0, z1, false, floor), WarRoomLook.Stone);

        // 1.2 m teal rails along both sides of a fixed floor, gold caps on top.
        static void WarRails(CourseBuilder b, string name, float z0, float z1, float floor)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                float x0 = side < 0f ? -WarHalf - .5f : WarHalf, x1 = x0 + .5f;
                WarRoomLook.Paint(b.Rail(name + (side < 0f ? " rail W" : " rail E"), x0, x1, z0, z1, floor), WarRoomLook.Teal);
                b.Box(name + " rail cap", V(x0 - .08f, floor + 1.2f, z0), V(x1 + .08f, floor + 1.32f, z1), WarRoomLook.Gold, false, false);
            }
        }

        // The compass rose of the command landing (looks only): a gold ring and four points.
        static void Compass(CourseBuilder b, Vector3 at)
        {
            b.Cylinder("Compass ring", at, 9f, .02f, WarRoomLook.Inlay, false);
            b.Cylinder("Compass face", at + Vector3.up * .005f, 8.2f, .02f, WarRoomLook.Map, false);
            for (int i = 0; i < 4; i++)
            {
                var point = b.Box("Compass point", V(-.35f, .02f, -4f), V(.35f, .035f, 4f), WarRoomLook.Gold, false, false);
                point.transform.localPosition = at + V(0f, .03f, 0f);
                point.transform.localRotation = Quaternion.Euler(0f, i * 45f, 0f);
            }
        }

        // A rook statue (looks only).
        static void Rook(CourseBuilder b, string name, Vector3 at)
        {
            var group = b.Group(name);
            group.localPosition = at;
            var before = b.parent;
            b.parent = group;
            b.Cylinder("Base", V(0f, 0f, 0f), 2.4f, .6f, WarRoomLook.Stone, false);
            b.Cylinder("Body", V(0f, .6f, 0f), 1.6f, 3f, WarRoomLook.Marble, false);
            b.Cylinder("Head", V(0f, 3.6f, 0f), 2.1f, .7f, WarRoomLook.Marble, false);
            for (int i = 0; i < 4; i++)
            {
                var merlon = b.Box("Merlon", V(-.3f, 0f, -.3f), V(.3f, .6f, .3f), WarRoomLook.Marble, false, false);
                merlon.transform.localPosition = V(Mathf.Sin(i * Mathf.PI / 2f) * .75f, 4.6f, Mathf.Cos(i * Mathf.PI / 2f) * .75f);
            }
            b.parent = before;
        }

        // The hall round the course (looks only, no colliders): a floor far below, walls of bookshelves between stone
        // pillars with lamps, blue banners with a gold rook.
        static void Hall(CourseBuilder b, float z0, float z1)
        {
            const float Low = -16f, High = 18f, Side = 26f;
            b.Box("Hall floor (far below)", V(-Side - 2f, Low - 1f, z0), V(Side + 2f, Low, z1), WarRoomLook.Deep, false, false);
            int bay = 0;
            for (float z = z0; z < z1; z += 12f, bay++)
                foreach (float side in new[] { -1f, 1f })
                {
                    float x = side * Side;
                    b.Cylinder("Pillar", V(x, Low, z), 2f, High - Low, WarRoomLook.Stone, false);
                    b.Cylinder("Pillar ring", V(x, 4f, z), 2.3f, .4f, WarRoomLook.Gold, false);
                    b.Sphere("Lamp", V(x - side * 1.4f, 6f, z), .8f, WarRoomLook.Lamp, false);
                    // The bookshelf of the bay: walnut, a row of books every 2.5 m.
                    float back = x + side * 1.2f, front = x + side * .2f, s0 = z + 1.2f, s1 = z + 10.8f;
                    b.Box("Bookshelf", V(Mathf.Min(back, front), Low, s0), V(Mathf.Max(back, front), 9f, s1), WarRoomLook.Wood, false, false);
                    float bx = front - side * .05f;
                    for (int row = 0; row < 9; row++)
                    {
                        float y = Low + 1f + row * 2.5f;
                        b.Box("Books", V(Mathf.Min(bx, bx - side * .3f), y, s0 + .3f), V(Mathf.Max(bx, bx - side * .3f), y + 1.6f, s1 - .3f),
                              WarRoomLook.Book(bay * 3 + row + (side < 0f ? 0 : 2)), false, false);
                    }
                    if (bay % 2 == 0)
                    {
                        float fx = front - side * .4f;
                        b.Box("Banner", V(Mathf.Min(fx, fx - side * .1f), 9.5f, z + 4.5f), V(Mathf.Max(fx, fx - side * .1f), 16f, z + 7.5f), WarRoomLook.Banner, false, false);
                        b.Box("Banner rook", V(Mathf.Min(fx, fx - side * .14f), 12f, z + 5.5f), V(Mathf.Max(fx, fx - side * .14f), 14.2f, z + 6.5f), WarRoomLook.Gold, false, false);
                    }
                }
            b.Box("Hall wall W", V(-Side - 2.2f, Low, z0), V(-Side - 1.8f, High, z1), WarRoomLook.Stone, false, false);
            b.Box("Hall wall E", V(Side + 1.8f, Low, z0), V(Side + 2.2f, High, z1), WarRoomLook.Stone, false, false);
        }

        // A map table outside the east rail (floor `y`, z0 .. z1), carrying a CrossingSignal.
        static CrossingSignal MapTable(CourseBuilder b, string name, float y, float z0, float z1)
        {
            var table = b.Box(name, V(WarHalf + .5f, y - 1f, z0), V(WarHalf + 2.5f, y + 1.3f, z1), WarRoomLook.Wood, true);
            b.Box(name + " map", V(WarHalf + .6f, y + 1.3f, z0 + .1f), V(WarHalf + 2.4f, y + 1.31f, z1 - .1f), WarRoomLook.Map, false, false);
            return table.AddComponent<CrossingSignal>();
        }

        // A lamp on a map table (dark until its CrossingSignal lights it).
        static Renderer Lamp(CourseBuilder b, string name, Vector3 at, float diameter) =>
            b.Cylinder(name, at, diameter, .2f, b.kit.trimBlack, false).GetComponent<Renderer>();
    }
}
