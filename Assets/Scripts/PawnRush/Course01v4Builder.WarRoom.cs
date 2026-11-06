using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // 승규 님's endgame (phase 3) course for the test scene (seoungtest): the war room (Docs/KingRush/PAWN_RUSH_ENDGAME_RESEARCH.md).
    // v2 (10-30, R244): the look of the S1-S4 concept art (cream marble, gold lines, teal and gold rails, map tables,
    // bookshelves, blue banners) at the size and with the devices of the wardrobe concept.
    // Hard LD v1 (10-30, R255, Docs/KingRush/ENDGAME_LD_HARD_v1.md §6): every value below is the one in
    // ArtSource/PawnRush/Endgame/eg_layout.py ld_v1(); change both together and run `python eg_timing.py ld_v1` again.
    // Six a side: the way is 12 m wide (x -6..6), running north from z 0, one line (no side way past a device).
    // y 0 up to the lift tiles, y 3 after them.
    //
    //   S1   z   0 .. 14     entry corridor, the map table (where the ferry is, which S4 bridge is open)          CP 1
    //   R1   z  14 .. 40     the rotating war table (16 m), a 16.4 m bar sweeping over it                          CP 2
    //   S2   z  40 .. 60.4   the ferry (8 x 8, 12 s) under a fixed hurdle bar across the middle of the drop
    //   R2   z 60.4 .. 98    the bookshelf hall: two pairs of 06 walls stretched to 12 m, the 09 clock gates     CP 3
    //   S3   z  98 .. 124    the command landing: two rook screens on the gates' 5 s                              CP 4
    //   R3   z 124 .. 144    the banner bridge, 6 m wide, two 11 pendulums
    //   R4   z 144 .. 156    the lift tiles up to y 3, a 15 side gust over each set's top row                    CP 5
    //   R5   z 156 .. 184.6  two belts north through a pair of falling pieces (no rails along the belts)          CP 6
    //   R5b  z 184.6 .. 203.9 four crates hanging over the drop, each bobbing on its own beat
    //   R6   z 203.9 .. 220.5 an 01 spinning disc, the second map table                                           CP 7
    //   S4   z 220.5 .. 237.1 two 12 folding bridges, a rook screen on the far side
    //   exit z 237.1 ..      15 m runout, the 12 x 6 m terrace (CP 8), the open arch, four steps 1 m down
    //
    // A fall comes back to the last checkpoint (an 8 m fall respawns). The hall round it is looks only (no colliders).
    // The devices and the look-only boxes are registered for the Blender models (Course01v4Builder.WarRoomModels.cs).
    public static partial class Course01v4Builder
    {
        const float WarHalf = 6f;
        const float FerryHalf = 4f, FerryTravel = 6f, FerryPeriod = 12f;
        const float FerryGap0 = 40f, FerryGap1 = FerryGap0 + 2f * (FerryHalf + FerryTravel) + .4f;   // 0.2 m gaps at the ends
        const float FerryMiddle = (FerryGap0 + FerryGap1) * .5f;                                        // 50.2
        const float R2End = 98f, S3End = 124f, R3End = 144f, R4End = 156f;
        const float Upper = 3f, LiftZ = 151.6f, UpperZ = 155f;
        const float BeltZ = 173f, R5End = 184.6f, R5bEnd = 203.9f;
        const float DiscZ = 210.5f, S4Start = 220.5f, BridgeHalf = 7.05f, S4End = S4Start + 2f * BridgeHalf;   // 234.6
        const float S4ScreenZ = S4End + 1.5f, ExitStart = S4ScreenZ + 1f, Runout = 15f;                         // 236.1, 237.1

        static void WarRoom(CourseBuilder b)
        {
            var k = b.kit;
            WarBegin();
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
                // A 16 m board turning 18 degrees a second (riders carried), a 16.4 m low bar (0.2 .. 0.52 m) sweeping
                // over all of it the other way: jump it or be swept off. Bridges 8 m wide on and off.
                WarFloor(b, "R1 bridge on", -4f, 4f, 14f, 16f, 0f);
                var board = RotatingBoardRound(b, V(0f, 0f, 24f), 16f, 18f);
                foreach (var r in board.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterial = r.gameObject == board ? WarRoomLook.Map : WarRoomLook.Teal;
                WarDevice("R1_board", board);
                // The rim inside the board's edge (v1: 16.0, the 16.6 rim overlapped the bridges' ends).
                WarLook(b.Cylinder("R1 board rim", V(0f, -.5f, 24f), 16f, .45f, WarRoomLook.Gold, false));
                WarDevice("R1_bar", SpinBar(b, "R1", V(0f, 0f, 24f), 16.4f, -60f));
                WarFloor(b, "R1 bridge off", -4f, 4f, 32f, 34f, 0f);
                WarFloor(b, "R1 landing", -WarHalf, WarHalf, 34f, FerryGap0, 0f);
                WarRails(b, "R1 landing", 34f, FerryGap0, 0f);
                b.Checkpoint(2, V(0f, 0f, 36f), 0f, 12f);
            });

            b.In("S2 Ferry", () =>
            {
                // A wooden map deck in a teal frame, carried by an ObstacleMotion on its root (riders carried).
                // At either end it stops 0.2 m short of the floor; in the middle 6 m of drop lies each side.
                // Max speed 6 * 2pi / 12 = 3.1 m/s. Its rim is flush (0.12 m) so it passes under the hurdle.
                var root = new GameObject("Device_Ferry");
                root.transform.SetParent(b.parent, false);
                root.transform.localPosition = V(0f, 0f, FerryMiddle);
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
                    b.Box("Side beam", V(side * FerryHalf - .3f, 0f, -FerryHalf), V(side * FerryHalf + .3f, .12f, FerryHalf), WarRoomLook.Teal, true);
                b.parent = before;
                ferry = root.transform;
                WarDevice("S2_ferry", root);
                ferryAtS1 = b.Group("Ferry middle at S1");
                ferryAtS1.localPosition = V(0f, 0f, FerryMiddle - FerryTravel);
                ferryAtS3 = b.Group("Ferry middle at S3");
                ferryAtS3.localPosition = V(0f, 0f, FerryMiddle + FerryTravel);

                // The fixed hurdle across the middle of the drop (contact only): to a rider the deck carries it in.
                var hurdle = b.Box("S2 hurdle", V(-5.6f, .2f, FerryMiddle - .225f), V(5.6f, .52f, FerryMiddle + .225f), WarRoomLook.Gold, false, true);
                WarDevice("S2_hurdle", Span(hurdle, Vector3.right, -5.6f, 5.6f, 0f));
                WarWall(b, "S2 hurdle pier W", -6.4f, -5.6f, -16f, 1f, FerryMiddle - .4f, FerryMiddle + .4f);
                WarWall(b, "S2 hurdle pier E", 5.6f, 6.4f, -16f, 1f, FerryMiddle - .4f, FerryMiddle + .4f);
            });

            b.In("R2 Bookshelf hall", () =>
            {
                WarFloor(b, "R2 floor", -WarHalf, WarHalf, FerryGap1, R2End, 0f);
                b.Checkpoint(3, V(0f, 0f, FerryGap1 + 2f), 0f, 12f);
                // 06 stretched to the 12 m way, two pairs half a cycle apart (each pair 1.95 s open, both in one go
                // 0.75 s: the pocket between them is the wait). Dressed as bookshelves.
                foreach (var (tag, z, phase) in new[] { ("A", 70f, 0f), ("B", 79.5f, 2.75f) })
                {
                    var walls = SlidingWalls12(b, "R2 " + tag, V(0f, 0f, z), 0f, phase);
                    WarRoomLook.Paint(walls, WarRoomLook.Wood);
                    WarDevice("R2_walls_" + tag, Span(walls, Vector3.right, -WarHalf, WarHalf, 0f));
                }
                // The rails stop where a wall slides through them (W at z - 3.3 .. z - 0.3, E at z + 0.3 .. z + 3.3).
                WarRail(b, "R2 rail W 0", -1f, FerryGap1, 66.7f, 0f);
                WarRail(b, "R2 rail W 1", -1f, 69.7f, 76.2f, 0f);
                WarRail(b, "R2 rail W 2", -1f, 79.2f, R2End, 0f);
                WarRail(b, "R2 rail E 0", 1f, FerryGap1, 70.3f, 0f);
                WarRail(b, "R2 rail E 1", 1f, 73.3f, 79.8f, 0f);
                WarRail(b, "R2 rail E 2", 1f, 82.8f, R2End, 0f);
                // 09: two 4 m doors opening in turn; marble either side closes the rest of the way.
                WarDevice("R2_gates", Hazard(b, k.clockGates, "09 Clock gates R2", V(0f, 0f, 90f), 0f, 0f));
                WarWall(b, "R2 gate side W", -WarHalf, -4.3f, 0f, 6f, 89.4f, 90.6f);
                WarWall(b, "R2 gate side E", 4.3f, WarHalf, 0f, 6f, 89.4f, 90.6f);
            });

            b.In("S3 Command landing", () =>
            {
                WarFloor(b, "S3 floor", -WarHalf, WarHalf, R2End, S3End, 0f);
                WarRails(b, "S3", R2End, S3End, 0f);
                b.Checkpoint(4, V(0f, 0f, R2End + 2f), 0f, 12f);
                Compass(b, V(0f, 0f, 110.5f));
                // Rook screens: each 6 m, closing half the way and sliding to the other half, on the gates' 5 s: a
                // runner leaving a gate door meets the open half of the first (1.55 s), changes sides for the second.
                foreach (var (z, phase) in new[] { (105f, .25f), (116f, 2.25f) })
                {
                    var screen = Span(RookWall(b, "S3 " + z, V(0f, 0f, z), 6f, 3f, 5f, phase), Vector3.right, -WarHalf, WarHalf, 0f);
                    foreach (var r in screen.GetComponentsInChildren<Renderer>(true))
                        r.sharedMaterial = r.name == "Wall" ? WarRoomLook.Teal : WarRoomLook.Gold;
                    WarDevice("S3_screen_" + (int)z, screen);
                }
            });

            b.In("R3 Banner bridge", () =>
            {
                // 6 m over the drop (v1: at 10 m the lanes |x| > 3.4 never met a ball); two pendulums swinging
                // across it, frames outside their reach.
                WarFloor(b, "R3 bridge", -3f, 3f, S3End, R3End, 0f);
                foreach (var (z, phase) in new[] { (130f, 0f), (138f, 1.1f) })
                {
                    WarDevice("R3_pendulum_" + (int)z, Span(Hazard(b, k.pendulum, "11 Pendulum R3 " + z, V(0f, 0f, z), 0f, phase), Vector3.right, -3f, 3f, 0f, "R3"));
                    WarLook(WarRoomLook.Paint(b.Wall("R3 frame post W " + z, -7.8f, -7f, -3f, 8.3f, z - .4f, z + .4f, false), WarRoomLook.Teal));
                    WarLook(WarRoomLook.Paint(b.Wall("R3 frame post E " + z, 7f, 7.8f, -3f, 8.3f, z - .4f, z + .4f, false), WarRoomLook.Teal));
                    WarLook(WarRoomLook.Paint(b.Wall("R3 frame beam " + z, -7.8f, 7.8f, 7.8f, 8.3f, z - .4f, z + .4f, false), WarRoomLook.Gold));
                    // A banner hangs from the beam beside the pivot (looks only).
                    WarLook(b.Box("R3 banner " + z, V(2.4f, 4.6f, z - .05f), V(4.8f, 7.8f, z + .05f), WarRoomLook.Banner, false, false));
                }
            });

            b.In("R4 Lift tiles", () =>
            {
                // Two 05 sets side by side (+-3 m, 6 s, half a cycle apart): step on a tile low, ride it up to y 3.
                // Over each set's north row a 15 vent on a stone bracket outside the way blows toward the middle.
                WarFloor(b, "R4 floor", -WarHalf, WarHalf, R3End, R3End + 4f, 0f);
                WarRails(b, "R4", R3End, R3End + 4f, 0f);
                b.Checkpoint(5, V(0f, 0f, R3End + 1f), 0f, 12f);
                WarDevice("R4_tiles_W", WarRoomLook.Paint(Hazard(b, k.risingTiles, "05 Lift tiles R4 W", V(-3.35f, 0f, LiftZ), 0f, 0f), WarRoomLook.Wood));
                WarDevice("R4_tiles_E", WarRoomLook.Paint(Hazard(b, k.risingTiles, "05 Lift tiles R4 E", V(3.35f, 0f, LiftZ), 0f, 3f), WarRoomLook.Wood));
                // The upper floor's front: marble, not climbed (the tiles are the way up).
                WarWall(b, "R4 upper front", -WarHalf, WarHalf, -2f, Upper, UpperZ, R4End, true);
                float row = LiftZ + 1.575f;   // 153.175
                WarDevice("R4_vent_W", Hazard(b, k.airVent, "15 Side wind R4 W", V(-6.85f, Upper, row), 90f, 0f));
                WarDevice("R4_vent_E", Hazard(b, k.airVent, "15 Side wind R4 E", V(6.85f, Upper, row), 270f, 3.4f));
                WarWall(b, "R4 vent bracket W", -8.2f, -6.7f, -2f, Upper, row - 1.6f, row + 1.6f);
                WarWall(b, "R4 vent bracket E", 6.7f, 8.2f, -2f, Upper, row - 1.6f, row + 1.6f);
            });

            b.In("R5 Conveyors and falling pieces", () =>
            {
                // Two belts run north through a pair of falling pieces landing on them, half a cycle apart: wait on the
                // floor, step on when your side has just struck. The seam between the belts is under both pieces.
                // No rails along the belts: a shock there throws a pawn off the edge.
                WarFloor(b, "R5 floor 1", -WarHalf, WarHalf, R4End, BeltZ - 5f, Upper);
                WarRails(b, "R5 south", R4End, BeltZ - 5f, Upper);
                foreach (float x in new[] { -3f, 3f })
                {
                    // 08 is 6 x 8; 1.25 along its length makes 10 m. Turned 180 it pushes north. Top 0.4 over the root.
                    var belt = Hazard(b, k.conveyor, "08 Conveyor R5 " + (x < 0f ? "W" : "E"), V(x, Upper - .4f, BeltZ), 180f, 0f);
                    var s = belt.transform.localScale;
                    belt.transform.localScale = new Vector3(s.x, s.y, s.z * 1.25f);
                    WarDevice("R5_belt_" + (x < 0f ? "W" : "E"), belt);
                }
                WarLook(b.Box("R5 conveyor bed", V(-WarHalf, Upper - 2f, BeltZ - 5f), V(WarHalf, Upper - 1f, BeltZ + 5f), WarRoomLook.Deep, false, false));
                WarFloor(b, "R5 floor 2", -WarHalf, WarHalf, BeltZ + 5f, R5End, Upper);
                WarRails(b, "R5 north", BeltZ + 5f, R5End, Upper);
                // 10: warning circle (5 m) 1.4 s, the piece drops, a 5 m shockwave.
                WarDevice("R5_piece_W", Hazard(b, k.fallingPiece, "10 Falling piece R5 W", V(-3f, Upper, BeltZ), 0f, 0f));
                WarDevice("R5_piece_E", Hazard(b, k.fallingPiece, "10 Falling piece R5 E", V(3f, Upper, BeltZ), 0f, 2.275f));
                b.Checkpoint(6, V(0f, Upper, R5End - 2.6f), 0f, 12f);
            });

            b.In("R5b Hanging crates", () =>
            {
                // Four 6 x 3 crates on chains over the drop, bobbing +-0.5 m along the way every 4.4 s, every other one
                // half a cycle apart: each hop (1.5 m at rest) opens and closes in turn. Built like the ferry.
                for (int i = 0; i < 4; i++)
                {
                    float z = R5End + 2.9f + 4.5f * i;
                    var root = new GameObject("Device_Crate " + (i + 1));
                    root.transform.SetParent(b.parent, false);
                    root.transform.localPosition = V(0f, Upper, z);
                    var motion = root.AddComponent<ObstacleMotion>();
                    motion.kind = MotionKind.Translate;
                    motion.axis = Vector3.forward;
                    motion.distance = .5f;
                    motion.period = 4.4f;
                    motion.shove = false;
                    motion.phase = .5f * (i % 2);
                    var before = b.parent;
                    b.parent = root.transform;
                    b.Box("Deck", V(-3f, -.5f, -1.5f), V(3f, 0f, 1.5f), WarRoomLook.Wood).AddComponent<CourseFloor>();
                    b.Box("Map", V(-2.6f, 0f, -1.1f), V(2.6f, .02f, 1.1f), WarRoomLook.Map, false, false);
                    foreach (float side in new[] { -1f, 1f })
                        b.Box("Chain", V(side * 2.8f - .05f, 0f, -.05f), V(side * 2.8f + .05f, 9f, .05f), b.kit.trimBlack, false, false);
                    b.parent = before;
                    WarDevice("R5b_crate_" + (i + 1), root);
                }
            });

            b.In("R6 Disc", () =>
            {
                // An 8 m 01 disc over a gap: its surface spins a pawn round and out; the floor either side is 12 m.
                WarFloor(b, "R6 ledge", -WarHalf, WarHalf, R5bEnd, DiscZ - 4f, Upper);
                WarRails(b, "R6 ledge", R5bEnd, DiscZ - 4f, Upper);
                WarDevice("R6_disc", Disc(b, "R6", V(0f, Upper, DiscZ), false));
                WarFloor(b, "R6 landing", -WarHalf, WarHalf, DiscZ + 4f, S4Start, Upper);
                WarRails(b, "R6 landing", DiscZ + 4f, S4Start, Upper);
                b.Checkpoint(7, V(0f, Upper, DiscZ + 5.5f), 0f, 12f);
            });

            b.In("S4 Folding bridges", () =>
            {
                // 12 is 14.1 m along its x (3 m landings either end, two 3.9 m panels) and 6 m wide; turned 90 it
                // crosses north. Half a cycle (13.5 s) apart, so one of the two is nearly always open. A rook screen
                // past them sweeps once a bridge cycle: 2.5 s to cross a bridge and the screen in one go.
                float middle = S4Start + BridgeHalf;
                var w = Hazard(b, k.foldingBridge, "12 FoldingBridge S4 W", V(-3f, Upper, middle), 90f, 0f);
                var e = Hazard(b, k.foldingBridge, "12 FoldingBridge S4 E", V(3f, Upper, middle), 90f, 6.75f);
                bridges[0] = w.GetComponentInChildren<FoldingBridge>();
                bridges[1] = e.GetComponentInChildren<FoldingBridge>();
                WarDevice("S4_bridge_W", w);
                WarDevice("S4_bridge_E", e);
                var screen = Span(RookWall(b, "S4", V(0f, Upper, S4ScreenZ), 6f, 3f, 13.5f, 7.25f), Vector3.right, -WarHalf, WarHalf, Upper);
                foreach (var r in screen.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterial = r.name == "Wall" ? WarRoomLook.Teal : WarRoomLook.Gold;
                WarDevice("S4_screen", screen);
                WarFloor(b, "S4 far floor", -WarHalf, WarHalf, S4End, ExitStart, Upper);
                WarRails(b, "S4 far floor", S4End, ExitStart, Upper);
            });

            b.In("Exit", () =>
            {
                // 15 m runout with nothing pushing, the 12 x 6 m terrace, the open arch (12 m wide, 5.5 m high),
                // four 0.25 m steps down between marble walls and the 4 m entry strip where the mission room begins.
                float y = Upper, terrace = ExitStart + Runout, arch = terrace + 6f;
                WarFloor(b, "Runout", -WarHalf, WarHalf, ExitStart, terrace, y);
                WarFloor(b, "Terrace", -WarHalf, WarHalf, terrace, arch, y);
                WarRails(b, "Exit", ExitStart, arch, y);
                b.Checkpoint(8, V(0f, y, terrace + 3f), 0f, 12f);
                WarWall(b, "Arch pier W", -WarHalf - 1f, -WarHalf, y - 1f, y + 5.5f, arch - .5f, arch + .5f);
                WarWall(b, "Arch pier E", WarHalf, WarHalf + 1f, y - 1f, y + 5.5f, arch - .5f, arch + .5f);
                WarLook(WarRoomLook.Paint(b.Wall("Arch lintel", -WarHalf - 1f, WarHalf + 1f, y + 5.5f, y + 6.5f, arch - .5f, arch + .5f, false), WarRoomLook.Gold));
                for (int i = 0; i < 4; i++)
                    WarFloor(b, "Step " + (i + 1), -WarHalf, WarHalf, arch + i * .5f, arch + (i + 1) * .5f, y - .25f * (i + 1));
                WarFloor(b, "Entry strip", -WarHalf, WarHalf, arch + 2f, arch + 6f, y - 1f);
                WarLook(WarRoomLook.Paint(b.Wall("Exit side wall W", -WarHalf - .5f, -WarHalf, y - 2f, y + 1.2f, arch + .5f, arch + 6f, false), WarRoomLook.Marble));
                WarLook(WarRoomLook.Paint(b.Wall("Exit side wall E", WarHalf, WarHalf + .5f, y - 2f, y + 1.2f, arch + .5f, arch + 6f, false), WarRoomLook.Marble));
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

                var s4 = MapTable(b, "S4 map table", Upper, DiscZ + 4f, S4Start - .5f);
                s4.ShowBridges(bridges, new[] { Lamp(b, "Bridge W lamp", V(7f, Upper + 1.3f, S4Start - 2f), .9f), Lamp(b, "Bridge E lamp", V(8f, Upper + 1.3f, S4Start - 2f), .9f) });
            });

            b.In("Hall", () => Hall(b, -12f, ExitStart + Runout + 12f + 40f));

            WarRoomModels(b);
        }

        // ------------------------------------------------------------------ war room parts

        // PR_06 for the 12 m way (LD v1, like SlidingWalls10): each wall 6 m wide, resting 9 m out and sliding 6 m,
        // the housing plates at +-12.3 (eg_layout.py; the doc's 12.6 is their outer face). Set on the placed instance.
        static GameObject SlidingWalls12(CourseBuilder b, string name, Vector3 at, float yaw, float phaseSeconds)
        {
            var go = Hazard(b, b.kit.slidingWalls, "PR_06_Sliding12 " + name, at, yaw, phaseSeconds);
            foreach (Transform child in go.transform)
            {
                bool left = child.name == "Left sliding wall", right = child.name == "Right sliding wall";
                if (left || right)
                {
                    var p = child.localPosition;
                    child.localPosition = new Vector3(left ? -9f : 9f, p.y, p.z);
                    var motion = child.GetComponent<ObstacleMotion>();
                    if (motion != null) motion.distance = 6f;
                    var wall = child.Find("Rook wall");
                    if (wall != null) wall.localScale = new Vector3(6f, wall.localScale.y, wall.localScale.z);
                }
                else if (child.name == "Wall housing")
                {
                    var p = child.localPosition;
                    child.localPosition = new Vector3(Mathf.Sign(p.x) * 12.3f, p.y, p.z);
                }
            }
            return go;
        }

        // A floor of cream marble with a gold line across it every 4 m (looks only).
        static void WarFloor(CourseBuilder b, string name, float x0, float x1, float z0, float z1, float top)
        {
            WarLook(WarRoomLook.Paint(b.Floor(name, x0, x1, z0, z1, top), WarRoomLook.Marble));
            for (float z = Mathf.Ceil(z0 / 4f) * 4f; z < z1 - .1f; z += 4f)
                if (z > z0 + .1f) WarLook(b.Box(name + " line", V(x0 + .3f, top, z - .06f), V(x1 - .3f, top + .015f, z + .06f), WarRoomLook.Inlay, false, false));
        }

        // Marble that cannot be climbed, in the hall's stone; `floor` when its top is walked on.
        static void WarWall(CourseBuilder b, string name, float x0, float x1, float y0, float y1, float z0, float z1, bool floor = false) =>
            WarLook(WarRoomLook.Paint(b.Wall(name, x0, x1, y0, y1, z0, z1, false, floor), WarRoomLook.Stone));

        // 1.2 m teal rails along both sides of a fixed floor, gold caps on top.
        static void WarRails(CourseBuilder b, string name, float z0, float z1, float floor)
        {
            WarRail(b, name + " rail W", -1f, z0, z1, floor);
            WarRail(b, name + " rail E", 1f, z0, z1, floor);
        }

        // One rail, west (side -1) or east (+1).
        static void WarRail(CourseBuilder b, string name, float side, float z0, float z1, float floor)
        {
            float x0 = side < 0f ? -WarHalf - .5f : WarHalf, x1 = x0 + .5f;
            WarLook(WarRoomLook.Paint(b.Rail(name, x0, x1, z0, z1, floor), WarRoomLook.Teal));
            WarLook(b.Box(name + " cap", V(x0, floor + 1.2f, z0), V(x1, floor + 1.32f, z1), WarRoomLook.Gold, false, false));
        }

        // The compass rose of the command landing (looks only): a gold ring and four points.
        static void Compass(CourseBuilder b, Vector3 at)
        {
            WarLook(b.Cylinder("Compass ring", at, 9f, .02f, WarRoomLook.Inlay, false));
            WarLook(b.Cylinder("Compass face", at + Vector3.up * .005f, 8.2f, .02f, WarRoomLook.Map, false));
            for (int i = 0; i < 4; i++)
            {
                var point = WarLook(b.Box("Compass point", V(-.35f, .02f, -4f), V(.35f, .035f, 4f), WarRoomLook.Gold, false, false));
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
            WarLook(b.Cylinder("Base", V(0f, 0f, 0f), 2.4f, .6f, WarRoomLook.Stone, false));
            WarLook(b.Cylinder("Body", V(0f, .6f, 0f), 1.6f, 3f, WarRoomLook.Marble, false));
            WarLook(b.Cylinder("Head", V(0f, 3.6f, 0f), 2.1f, .7f, WarRoomLook.Marble, false));
            for (int i = 0; i < 4; i++)
            {
                var merlon = WarLook(b.Box("Merlon", V(-.3f, 0f, -.3f), V(.3f, .6f, .3f), WarRoomLook.Marble, false, false));
                merlon.transform.localPosition = V(Mathf.Sin(i * Mathf.PI / 2f) * .75f, 4.6f, Mathf.Cos(i * Mathf.PI / 2f) * .75f);
            }
            b.parent = before;
        }

        // The hall round the course (looks only, no colliders): a floor far below, walls of bookshelves between stone
        // pillars with lamps, blue banners with a gold rook.
        static void Hall(CourseBuilder b, float z0, float z1)
        {
            const float Low = -30f, High = 18f, Side = 26f;   // v1: the floor 30 m down (respawn stays at y -10)
            WarLook(b.Box("Hall floor (far below)", V(-Side - 2f, Low - 1f, z0), V(Side + 2f, Low, z1), WarRoomLook.Deep, false, false));
            int bay = 0;
            for (float z = z0; z < z1; z += 12f, bay++)
                foreach (float side in new[] { -1f, 1f })
                {
                    float x = side * Side;
                    WarLook(b.Cylinder("Pillar", V(x, Low, z), 2f, High - Low, WarRoomLook.Stone, false));
                    WarLook(b.Cylinder("Pillar ring", V(x, 4f, z), 2.3f, .4f, WarRoomLook.Gold, false));
                    WarLook(b.Sphere("Lamp", V(x - side * 1.4f, 6f, z), .8f, WarRoomLook.Lamp, false));
                    // The bookshelf of the bay: walnut, a row of books every 2.5 m.
                    float back = x + side * 1.2f, front = x + side * .2f, s0 = z + 1.2f, s1 = z + 10.8f;
                    WarLook(b.Box("Bookshelf", V(Mathf.Min(back, front), Low, s0), V(Mathf.Max(back, front), 9f, s1), WarRoomLook.Wood, false, false));
                    float bx = front - side * .05f;
                    for (int row = 0; row < 15; row++)
                    {
                        float y = Low + 1f + row * 2.5f;
                        if (y > 7f) break;
                        WarLook(b.Box("Books", V(Mathf.Min(bx, bx - side * .3f), y, s0 + .3f), V(Mathf.Max(bx, bx - side * .3f), y + 1.6f, s1 - .3f),
                              WarRoomLook.Book(bay * 3 + row + (side < 0f ? 0 : 2)), false, false));
                    }
                    if (bay % 2 == 0)
                    {
                        float fx = front - side * .4f;
                        WarLook(b.Box("Banner", V(Mathf.Min(fx, fx - side * .1f), 9.5f, z + 4.5f), V(Mathf.Max(fx, fx - side * .1f), 16f, z + 7.5f), WarRoomLook.Banner, false, false));
                        WarLook(b.Box("Banner rook", V(Mathf.Min(fx, fx - side * .14f), 12f, z + 5.5f), V(Mathf.Max(fx, fx - side * .14f), 14.2f, z + 6.5f), WarRoomLook.Gold, false, false));
                    }
                }
            WarLook(b.Box("Hall wall W", V(-Side - 2.2f, Low, z0), V(-Side - 1.8f, High, z1), WarRoomLook.Stone, false, false));
            WarLook(b.Box("Hall wall E", V(Side + 1.8f, Low, z0), V(Side + 2.2f, High, z1), WarRoomLook.Stone, false, false));
        }

        // A map table outside the east rail (floor `y`, z0 .. z1), carrying a CrossingSignal.
        static CrossingSignal MapTable(CourseBuilder b, string name, float y, float z0, float z1)
        {
            var table = WarLook(b.Box(name, V(WarHalf + .5f, y - 1f, z0), V(WarHalf + 2.5f, y + 1.3f, z1), WarRoomLook.Wood, true));
            WarLook(b.Box(name + " map", V(WarHalf + .6f, y + 1.3f, z0 + .1f), V(WarHalf + 2.4f, y + 1.31f, z1 - .1f), WarRoomLook.Map, false, false));
            return table.AddComponent<CrossingSignal>();
        }

        // A lamp on a map table (dark until its CrossingSignal lights it).
        static Renderer Lamp(CourseBuilder b, string name, Vector3 at, float diameter) =>
            b.Cylinder(name, at, diameter, .2f, b.kit.trimBlack, false).GetComponent<Renderer>();
    }
}
