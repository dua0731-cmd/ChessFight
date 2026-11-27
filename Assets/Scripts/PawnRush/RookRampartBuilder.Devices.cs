using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;
using Look = ChessFight.PawnRush.RoyalClockTowerLook;
using T = ChessFight.PawnRush.RookRampartTimes;

namespace ChessFight.PawnRush
{
    // The rampart's devices, built at their reference poses (RookRampartMovers moves them on the clock).
    public static partial class RookRampartBuilder
    {
        // ------------------------------------------------------------------ 1. the siege ladders and their defenders

        static void Ladders(CourseBuilder b)
        {
            for (int i = 0; i < T.Ladders.Length; i++)
            {
                float x = T.Ladders[i];
                var foot = Root(b, "RR siege ladder " + (i + 1), V(x, 0f, LadderFootZ));
                // Its face is held (orange); past upright NoClimbSurface lets its climbers go (SiegeLadder).
                var face = YardKit.Box(foot, "RR siege ladder face", V(0f, LadderL * .5f, 0f), V(LadderW, LadderL, .1f), Look.Climb, YardKit.Solid.Solid);
                var unheld = face.AddComponent<NoClimbSurface>();
                unheld.enabled = false;
                foreach (float s in new[] { -1f, 1f })
                    YardKit.Box(foot, "RR siege ladder rail", V(s * (LadderW * .5f - .03f), LadderL * .5f, -.07f), V(.08f, LadderL, .08f), Wood, YardKit.Solid.None);
                for (float y = .3f; y < LadderL - .1f; y += .33f)
                    YardKit.Box(foot, "RR siege ladder rung", V(0f, y, -.08f), V(LadderW - .06f, .05f, .06f), Wood, YardKit.Solid.None);
                foot.gameObject.AddComponent<SiegeLadder>().Configure(i, unheld);
                // The attackers' crew at its foot, on the wall's side (the ladder falls the other way): they haul it up again.
                Person(b.parent, "RR ladder crew", V(x - .45f, 0f, LadderFootZ + .5f), Figure);
                Person(b.parent, "RR ladder crew", V(x + .45f, 0f, LadderFootZ + .75f), Figure);
            }
            // The two rook defenders on the walk, each with a forked pole.
            for (int d = 0; d < 2; d++)
            {
                var root = Root(b, "RR rook defender " + (d + 1), V(T.Ladders[3 * d], W1, DefenderZ));
                YardKit.Cylinder(root, "RR rook defender body", V(0f, .8f, 0f), Vector3.up, 1.1f, 1.6f, Defender, YardKit.Solid.NoClimb);
                YardKit.Cylinder(root, "RR rook defender crown", V(0f, 1.75f, 0f), Vector3.up, 1.3f, .3f, Defender, YardKit.Solid.None);
                for (int k = 0; k < 4; k++)
                {
                    var turn = Quaternion.Euler(0f, 45f + k * 90f, 0f);
                    YardKit.Box(root, "RR rook defender merlon", V(0f, 2.02f, 0f) + turn * V(0f, 0f, .5f), V(.35f, .25f, .25f), Defender, YardKit.Solid.None, turn);
                }
                var pole = new GameObject("RR forked pole").transform;
                pole.SetParent(root, false);
                YardKit.Cylinder(pole, "RR forked pole shaft", Vector3.zero, Vector3.up, .09f, 3.6f, Wood, YardKit.Solid.None);
                foreach (float s in new[] { -1f, 1f })
                    YardKit.Box(pole, "RR forked pole fork", V(s * .12f, -1.85f, 0f), V(.05f, .3f, .05f), Iron, YardKit.Solid.None,
                                Quaternion.Euler(0f, 0f, s * 25f));
                root.gameObject.AddComponent<LadderDefender>().Configure(d, pole, LadderL, LadderFootZ, W1, DefenderZ);
            }
        }

        // ------------------------------------------------------------------ 2. the murder holes

        static void MurderHoles(CourseBuilder b)
        {
            // Over the gate the walk is 15 trapdoors over the passage (3.3 m high); the parapet runs on in front.
            b.Box("RR outer gate parapet", V(GateX0, 0f, Face1), V(GateX1, W1 + SillH, HolesZ0), Look.NoClimb, true);
            for (float x = GateX0 + 1f; x < GateX1; x += Pitch)
                b.Box("RR outer gate merlon", V(x - MerlonL * .5f, W1 + SillH, Face1), V(x + MerlonL * .5f, W1 + MerlonH, HolesZ0), Look.NoClimb, true);
            var group = b.Group("RR murder holes");
            var floors = new List<Collider>();
            var lids = new List<Transform>();
            var rims = new List<GameObject>();
            for (int i = 0; i < HolesX; i++)
                for (int j = 0; j < HolesZ; j++)
                {
                    float x0 = GateX0 + i * Hole, z0 = HolesZ0 + j * Hole;
                    bool light = (i + j) % 2 == 0;
                    var floor = YardKit.Box(group, "RR trapdoor floor", V(x0 + Hole * .5f, W1 - .1f, z0 + Hole * .5f), V(Hole - .04f, .2f, Hole - .04f), null, YardKit.Solid.Solid);
                    Object.DestroyImmediate(floor.GetComponent<MeshRenderer>());
                    floor.AddComponent<CourseFloor>();
                    floors.Add(floor.GetComponent<Collider>());
                    // The lid hangs from its south edge.
                    var hinge = new GameObject("RR trapdoor lid").transform;
                    hinge.SetParent(group, false);
                    hinge.localPosition = V(x0 + Hole * .5f, W1 - .02f, z0 + .02f);
                    YardKit.Box(hinge, "RR trapdoor lid plank", V(0f, 0f, Hole * .5f - .02f), V(Hole - .08f, .04f, Hole - .08f), light ? Light : Dark, YardKit.Solid.None);
                    lids.Add(hinge);
                    var rim = new GameObject("RR trapdoor rattle").transform;
                    rim.SetParent(group, false);
                    rim.localPosition = V(x0 + Hole * .5f, W1 + .02f, z0 + Hole * .5f);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        YardKit.Box(rim, "RR trapdoor rattle rim", V(s * (Hole * .5f - .06f), 0f, 0f), V(.12f, .03f, Hole), Rattle, YardKit.Solid.None);
                        YardKit.Box(rim, "RR trapdoor rattle rim", V(0f, 0f, s * (Hole * .5f - .06f)), V(Hole, .03f, .12f), Rattle, YardKit.Solid.None);
                    }
                    rims.Add(rim.gameObject);
                }
            group.gameObject.AddComponent<MurderHoles>().Configure(floors.ToArray(), lids.ToArray(), rims.ToArray(), HolesX, HolesZ, Hole);
            // Under it the gate passage; whoever falls in goes back to the walk's checkpoint (the design's stair).
            b.Trigger<KillVolume>("RR kill volume (the gate passage)", V(GateX0, -.5f, HolesZ0), V(GateX1, .6f, Face1 + WallT));
        }

        // ------------------------------------------------------------------ 3. the siege towers

        static void SiegeTowers(CourseBuilder b)
        {
            for (int k = 0; k < SiegeX.Length; k++)
            {
                float x = SiegeX[k], h = SiegeW * .5f;
                string name = "RR siege tower " + (k == 0 ? "A" : "B");
                var tower = Root(b, name, V(x, 0f, SiegeZ0));
                var t = tower;
                // Floors (violet: they move), each with a hatch for its ladder.
                Slab(t, name + " floor 1", -h, h, -h, h, Floors[0]);
                Slab(t, name + " floor 2", -h, h, -h, -1.7f, Floors[1]);
                Slab(t, name + " floor 2", -h, h, -.4f, h, Floors[1]);
                Slab(t, name + " floor 2", -h, -1.45f, -1.7f, -.4f, Floors[1]);
                Slab(t, name + " floor 2", -.35f, h, -1.7f, -.4f, Floors[1]);
                Slab(t, name + " floor 3", -h, h, -h, .1f, Floors[2]);
                Slab(t, name + " floor 3", -h, h, 1.5f, h, Floors[2]);
                Slab(t, name + " floor 3", -h, .35f, .1f, 1.5f, Floors[2]);
                Slab(t, name + " floor 3", 1.45f, h, .1f, 1.5f, Floors[2]);
                // The two inner ladders (held: orange), each leaning north through its hatch and 0.5 m over the floor above.
                YardKit.Slab(t, name + " ladder 1", V(-.9f, Floors[0], -1.2f), V(-.9f, Floors[1] + .5f, -1.2f + .6f * (3.7f / 3.2f)), LadderW, .1f, Look.Climb, YardKit.Solid.Solid);
                YardKit.Slab(t, name + " ladder 2", V(.9f, Floors[1], .6f), V(.9f, Floors[2] + .5f, .6f + .6f * (3.7f / 3.2f)), LadderW, .1f, Look.Climb, YardKit.Solid.Solid);
                // The hides: both sides and the front up to the top floor (the raised bridge closes the front above it), the
                // roof; the back is open.
                YardKit.Box(t, name + " hide W", V(-h - .06f, (.9f + Floors[2] + 1.6f) * .5f, 0f), V(.12f, Floors[2] + 1.6f - .9f, SiegeW), Hide, YardKit.Solid.NoClimb);
                YardKit.Box(t, name + " hide E", V(h + .06f, (.9f + Floors[2] + 1.6f) * .5f, 0f), V(.12f, Floors[2] + 1.6f - .9f, SiegeW), Hide, YardKit.Solid.NoClimb);
                YardKit.Box(t, name + " hide front", V(0f, (.9f + Floors[2]) * .5f, h + .06f), V(SiegeW + .24f, Floors[2] - .9f, .12f), Hide, YardKit.Solid.NoClimb);
                YardKit.Box(t, name + " roof", V(0f, Floors[2] + 1.6f, -.15f), V(SiegeW + .4f, .2f, SiegeW + .1f), Wood, YardKit.Solid.NoClimb);
                // The base: posts and braces, the wheels, the crew pushing inside.
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                        YardKit.Box(t, name + " post", V(sx * (h - .1f), Floors[0] * .5f + .3f, sz * (h - .1f)), V(.22f, Floors[0] - .6f, .22f), Wood, YardKit.Solid.None);
                var wheels = new List<Transform>();
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1.25f, 1.25f })
                    {
                        var w = YardKit.Cylinder(t, name + " wheel", V(sx * (h + .15f), .62f, sz), Vector3.right, 1.24f, .22f, Wood, YardKit.Solid.None).transform;
                        wheels.Add(w);
                    }
                var crew = new[] { Person(t, name + " crew", V(-.6f, 0f, -.5f), Figure), Person(t, name + " crew", V(.6f, 0f, -.2f), Figure) };
                tower.gameObject.AddComponent<SiegeTower>().Configure(SiegeDelay[k], SiegeZ0, SiegeZ1(k), wheels.ToArray(), crew);

                // The drop-bridge on the front over the top floor: built down (0 degrees), the clock raises it.
                var hinge = Root(b, name + " bridge", V(x, Floors[2], SiegeZ0 + h + .05f));
                var plank = YardKit.Box(hinge, name + " bridge plank", V(0f, -.08f, BridgeL * .5f), V(2.4f, .16f, BridgeL), Look.Mover, YardKit.Solid.Solid);
                plank.AddComponent<CourseFloor>();
                hinge.gameObject.AddComponent<SiegeBridge>().Configure(SiegeDelay[k], SiegeZ0, SiegeZ1(k));

                // Its ruts across the lower ward.
                foreach (float s in new[] { -1.25f, 1.25f })
                    YardKit.Box(b.parent, "RR siege rut", V(x + s, .01f, (Face1 + WallT + Wall2Face(x)) * .5f), V(.3f, .02f, Wall2Face(x) - Face1 - WallT), Rock, YardKit.Solid.None);
            }
        }

        // A tower's floor slab (violet, walked on) x0..x1, z0..z1 in the tower's frame, its top at `top`.
        static void Slab(Transform tower, string name, float x0, float x1, float z0, float z1, float top) =>
            YardKit.Box(tower, name, V((x0 + x1) * .5f, top - .1f, (z0 + z1) * .5f), V(x1 - x0, .2f, z1 - z0), Look.Mover, YardKit.Solid.Solid)
                   .AddComponent<CourseFloor>();

        // ------------------------------------------------------------------ 4. the rooks' drill square

        static void DrillSquare(CourseBuilder b)
        {
            for (int n = 0; n < DrillFile.Length; n++)
            {
                bool file = DrillFile[n];
                int idx = DrillIndex[n];
                Vector2 start = file ? SquareMid(idx, 0) : SquareMid(0, idx);
                Vector3 travel = file ? V(0f, 0f, (BoardN - 1) * Square) : V((BoardN - 1) * Square, 0f, 0f);
                // Its line: a strip on the board that glows before it slides and burns red while it does.
                float along = BoardN * Square;
                var line = YardKit.Box(b.parent, "RR drill line " + (n + 1),
                                       file ? V(start.x, BoardY + .016f, BoardZ0 + along * .5f) : V(BoardX0 + along * .5f, BoardY + .016f, start.y),
                                       file ? V(Square * .3f, .01f, along - .4f) : V(along - .4f, .01f, Square * .3f), LineRest, YardKit.Solid.None);
                var rook = Root(b, "RR drill rook " + (n + 1) + (file ? " (file " : " (rank ") + idx + ")", V(start.x, BoardY, start.y));
                YardKit.Cylinder(rook, "RR drill rook body", V(0f, 1.2f, 0f), Vector3.up, 1.9f, 2.4f, Look.Mover, YardKit.Solid.NoClimb);
                YardKit.Cylinder(rook, "RR drill rook crown", V(0f, 2.6f, 0f), Vector3.up, 2.1f, .4f, Look.Mover, YardKit.Solid.NoClimb);
                for (int k = 0; k < 6; k++)
                {
                    var turn = Quaternion.Euler(0f, k * 60f, 0f);
                    YardKit.Box(rook, "RR drill rook merlon", V(0f, 3f, 0f) + turn * V(0f, 0f, .8f), V(.45f, .4f, .3f), Look.Mover, YardKit.Solid.None, turn);
                }
                rook.gameObject.AddComponent<DrillRook>().Configure(travel, DrillDelay[n], RookSlide, line.GetComponent<Renderer>(), LineRest, LineGlow, LineSlide);
            }
        }

        // ------------------------------------------------------------------ 5. the cranes

        static void Cranes(CourseBuilder b)
        {
            foreach (float x in new[] { RookCraneX, KnightCraneX })
            {
                float zs = Wall2Inner(x) - 5.2f, zn = Wall3Face(x) + 6.4f;
                foreach (float s in new[] { -1.6f, 1.6f })
                {
                    b.Box("RR gantry leg S", V(x + s - .2f, W2, zs - .2f), V(x + s + .2f, BeamY, zs + .2f), Wood, true);
                    b.Box("RR gantry leg N", V(x + s - .2f, W3, zn - .2f), V(x + s + .2f, BeamY, zn + .2f), Wood, true);
                    YardKit.Box(b.parent, "RR gantry beam", V(x + s, BeamY + .25f, (zs + zn) * .5f), V(.35f, .5f, zn - zs + 1f), Wood, YardKit.Solid.None);
                }
                foreach (float z in new[] { zs, zn })
                    YardKit.Box(b.parent, "RR gantry tie", V(x, BeamY + .25f, z), V(3.6f, .4f, .35f), Wood, YardKit.Solid.None);
            }

            // The giant rook: its crown is the floor ridden (violet), six merlons round it with gaps north and south.
            var rook = Root(b, "RR rook crane", V(RookCraneX, W2, RcLowZ));
            YardKit.Cylinder(rook, "RR rook crane crown", V(0f, -.15f, 0f), Vector3.up, 2f * CrownR, .3f, Look.Mover, YardKit.Solid.Solid).AddComponent<CourseFloor>();
            for (int k = 0; k < 6; k++)
            {
                var turn = Quaternion.Euler(0f, 30f + k * 60f, 0f);
                YardKit.Box(rook, "RR rook crane merlon", turn * V(0f, .45f, CrownR - .3f), V(1f, .9f, .5f), Look.NoClimb, YardKit.Solid.NoClimb, turn);
            }
            YardKit.Cylinder(rook, "RR rook crane body", V(0f, -.3f - (RookBody - .3f) * .5f, 0f), Vector3.up, 3.4f, RookBody - .3f, Look.NoClimb, YardKit.Solid.NoClimb);
            var trolley = YardKit.Box(b.parent, "RR rook crane trolley", V(RookCraneX, BeamY - .6f, RcLowZ), V(3.6f, .5f, 1.6f), Iron, YardKit.Solid.None).transform;
            var chainTops = new[] { V(-1.7f, .9f, 0f), V(1.7f, .9f, 0f) };
            var chains = new Transform[chainTops.Length];
            for (int k = 0; k < chains.Length; k++)
                chains[k] = YardKit.Cylinder(b.parent, "RR rook crane chain", V(RookCraneX + chainTops[k].x, 15f, RcLowZ), Vector3.up, .1f, 2f, Iron, YardKit.Solid.None).transform;
            // The cylinder's own scale is (d, length / 2, d): the rig stretches its y.
            var rookCrane = rook.gameObject.AddComponent<RookCrane>();
            rookCrane.Configure(W2, W3, RcLowZ, RcHighZ, trolley, chains, chainTops, BeamY);

            // The giant knight, facing north, built at its low end: a base, the body, the neck and head (none held) and
            // its mane on its back (held: orange) - a rider hangs there south of it.
            var knight = Root(b, "RR knight crane", V(KnightCraneX, KnightLowY, KnLowZ));
            YardKit.Cylinder(knight, "RR knight base", V(0f, .3f, 0f), Vector3.up, 2f * KnightR, .6f, KnightWood, YardKit.Solid.NoClimb);
            YardKit.Box(knight, "RR knight body", V(0f, 1.4f, 0f), V(1.5f, 1.6f, 1.7f), KnightWood, YardKit.Solid.NoClimb);
            YardKit.Box(knight, "RR knight neck", V(0f, 2.9f, .25f), V(1.1f, 1.8f, 1.1f), KnightWood, YardKit.Solid.NoClimb, Quaternion.Euler(18f, 0f, 0f));
            YardKit.Box(knight, "RR knight head", V(0f, 3.85f, .95f), V(.95f, .9f, 1.7f), KnightWood, YardKit.Solid.NoClimb, Quaternion.Euler(-12f, 0f, 0f));
            foreach (float s in new[] { -1f, 1f })
                YardKit.Box(knight, "RR knight ear", V(s * .25f, 4.45f, .55f), V(.18f, .4f, .18f), KnightWood, YardKit.Solid.None);
            // South of the body's face (-0.85), so nothing unclimbable stands in front of it: a rider hangs 1.45 m behind
            // the knight's axis (rampart_layout.py KNIGHT_HANG).
            YardKit.Box(knight, "RR knight mane", V(0f, 2.65f, -.95f), V(.9f, 1.9f, .3f), Look.Climb, YardKit.Solid.Solid);
            var ktrolley = YardKit.Box(b.parent, "RR knight crane trolley", V(KnightCraneX, BeamY - .6f, KnLowZ), V(3.6f, .5f, 1.6f), Iron, YardKit.Solid.None).transform;
            var cable = YardKit.Cylinder(b.parent, "RR knight crane cable", V(KnightCraneX, 18f, KnLowZ), Vector3.up, .12f, 2f, Iron, YardKit.Solid.None).transform;
            var knightCrane = knight.gameObject.AddComponent<KnightCrane>();
            knightCrane.Configure(KnightLowY, W3, KnLowZ, KnHighZ, ktrolley, cable, BeamY, KnightH);

            // The treadwheels on the inner wall's walk, beside the gantries' north legs, walked by a worker each.
            foreach (var (x, rc, kc) in new[] { (RookCraneX - 3.6f, rookCrane, (KnightCrane)null), (KnightCraneX + 3.6f, (RookCrane)null, knightCrane) })
            {
                var w = Root(b, "RR treadwheel", V(x, W3 + 1.6f, Wall3Face(x) + 4.6f));
                var wheel = new GameObject("RR treadwheel wheel").transform;
                wheel.SetParent(w, false);
                YardKit.Cylinder(wheel, "RR treadwheel rim", Vector3.zero, Vector3.right, 3f, .6f, Wood, YardKit.Solid.None);
                for (int k = 0; k < 4; k++)
                    YardKit.Box(wheel, "RR treadwheel spoke", Vector3.zero, V(.1f, 3f, .12f), Iron, YardKit.Solid.None, Quaternion.Euler(k * 45f, 0f, 0f));
                Person(w, "RR treadwheel worker", V(0f, -1.45f, 0f), Figure);
                w.gameObject.AddComponent<Treadwheel>().Configure(rc, kc, wheel);
            }

            // Red marks: where the knight comes down on the inner wall (stand clear) and where the rook's crown docks.
            YardKit.Cylinder(b.parent, "RR knight landing mark", V(KnightCraneX, W3 + .015f, KnHighZ), Vector3.up, 2f * KnightR + .8f, .02f, Mark, YardKit.Solid.None);
        }

        // ------------------------------------------------------------------ 6. the king's banner

        static void Banner(CourseBuilder b)
        {
            float midZ = (BannerZ0 + BannerZ1) * .5f, width = BannerZ1 - BannerZ0;
            // The stone ledge between the lengths, the face's whole width (not held: it is stood on).
            b.Box("RR banner ledge", V(GateHX0 - LedgeDepth, LedgeY - .3f, LedgeZ0), V(GateHX0, LedgeY, LedgeZ1), Look.Rest, true).AddComponent<CourseFloor>();
            // The lower length hangs from the ledge's front edge to the walk; the upper one from the roof's edge, flat on the wall.
            Length(b, "RR king's banner lower", V(GateHX0 - LedgeDepth - .06f, LedgeY - .05f, midZ), LedgeY - .05f - W3 - .05f, width);
            Length(b, "RR king's banner upper", V(GateHX0 - .05f, GateY, midZ), GateY - LedgeY - .05f, width);
            // The pennants on the roof that stream a second before every gust.
            var flags = new List<Transform>();
            foreach (float z in new[] { 77f, 79f, 81f })
            {
                YardKit.Cylinder(b.parent, "RR pennant pole", V(GateHX0 + 2f, GateY + 1.3f, z), Vector3.up, .1f, 2.6f, Wood, YardKit.Solid.None);
                var flag = new GameObject("RR pennant").transform;
                flag.SetParent(b.parent, false);
                flag.localPosition = V(GateHX0 + 2f, GateY + 2.45f, z);
                YardKit.Box(flag, "RR pennant cloth", V(-.65f, 0f, 0f), V(1.3f, .55f, .04f), Pennant, YardKit.Solid.None);
                flags.Add(flag);
            }
            b.Group("RR pennants").gameObject.AddComponent<RampartPennants>().Configure(flags.ToArray());
        }

        // A length of the banner hanging from `top` (its top edge's middle), `length` down, `width` along z: white with a gold
        // crown, held on its outer (west) face.
        static void Length(CourseBuilder b, string name, Vector3 top, float length, float width)
        {
            var hinge = Root(b, name, top);
            YardKit.Box(hinge, name + " cloth", V(0f, -length * .5f, 0f), V(.06f, length, width), Cloth, YardKit.Solid.Solid);
            YardKit.Box(hinge, name + " rod", V(0f, .04f, 0f), V(.12f, .12f, width + .3f), Gold, YardKit.Solid.None);
            YardKit.Box(hinge, name + " border", V(-.04f, -length + .1f, 0f), V(.02f, .2f, width), Gold, YardKit.Solid.None);
            YardKit.Box(hinge, name + " crown", V(-.04f, -length * .5f, 0f), V(.02f, .7f, 1.4f), Gold, YardKit.Solid.None);
            hinge.gameObject.AddComponent<KingsBanner>();
        }
    }
}
