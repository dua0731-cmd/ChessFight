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
            // The attackers' earth mound along the wall's west half: the ladders' feet stand on it (R866: the wall 6 m high,
            // the climb 5.6 m), a ramp up from the glacis on its south side, deep enough for a pushed ladder to lie on.
            float mz = Face1 - MoundDepth;
            YardKit.Box(b.parent, "RR siege mound", V((MoundX0 + MoundX1) * .5f, Mound * .5f - .5f, (mz + Face1) * .5f), V(MoundX1 - MoundX0, Mound + 1f, MoundDepth),
                        Earth).AddComponent<CourseFloor>();
            b.Ramp("RR siege mound ramp", MoundX0, MoundX1, mz - 5f, mz, 0f, Mound, 1f);
            for (int i = 0; i < T.Ladders.Length; i++)
            {
                float x = T.Ladders[i];
                var foot = Root(b, "RR siege ladder " + (i + 1), V(x, Mound, LadderFootZ));
                // Its face is held (orange); past upright NoClimbSurface and a shake let its climbers go (SiegeLadder).
                var face = YardKit.Box(foot, "RR siege ladder face", V(0f, LadderL * .5f, 0f), V(LadderW, LadderL, .12f), Look.Climb, YardKit.Solid.Solid);
                var unheld = face.AddComponent<NoClimbSurface>();
                unheld.enabled = false;
                foreach (float s in new[] { -1f, 1f })
                    YardKit.Box(foot, "RR siege ladder rail", V(s * (LadderW * .5f - .04f), LadderL * .5f, -.08f), V(.1f, LadderL, .1f), Wood, YardKit.Solid.None);
                for (float y = .3f; y < LadderL - .1f; y += .33f)
                    YardKit.Box(foot, "RR siege ladder rung", V(0f, y, -.09f), V(LadderW - .08f, .06f, .07f), Wood, YardKit.Solid.None);
                foot.gameObject.AddComponent<SiegeLadder>().Configure(i, unheld, LadderL, LadderW);
                // The attackers' crew at its foot, on the wall's side (the ladder falls the other way): they haul it up again.
                Person(b.parent, "RR ladder crew", V(x - .5f, Mound, LadderFootZ + .55f), Figure);
                Person(b.parent, "RR ladder crew", V(x + .5f, Mound, LadderFootZ + .85f), Figure);
            }
            // The two rook defenders on the walk, each with a forked pole.
            for (int d = 0; d < 2; d++)
            {
                var root = Root(b, "RR rook defender " + (d + 1), V(T.Ladders[3 * d], W1, DefenderZ));
                YardKit.Cylinder(root, "RR rook defender body", V(0f, 1.05f, 0f), Vector3.up, 1.5f, 2.1f, Defender, YardKit.Solid.NoClimb);
                YardKit.Cylinder(root, "RR rook defender crown", V(0f, 2.3f, 0f), Vector3.up, 1.75f, .4f, Defender, YardKit.Solid.None);
                for (int k = 0; k < 4; k++)
                {
                    var turn = Quaternion.Euler(0f, 45f + k * 90f, 0f);
                    YardKit.Box(root, "RR rook defender merlon", V(0f, 2.65f, 0f) + turn * V(0f, 0f, .65f), V(.45f, .32f, .32f), Defender, YardKit.Solid.None, turn);
                }
                var pole = new GameObject("RR forked pole").transform;
                pole.SetParent(root, false);
                YardKit.Cylinder(pole, "RR forked pole shaft", Vector3.zero, Vector3.up, .11f, LadderDefender.PoleLength, Wood, YardKit.Solid.None);
                foreach (float s in new[] { -1f, 1f })
                    YardKit.Box(pole, "RR forked pole fork", V(s * .14f, -LadderDefender.PoleLength * .5f - .12f, 0f), V(.06f, .36f, .06f), Iron, YardKit.Solid.None,
                                Quaternion.Euler(0f, 0f, s * 25f));
                // The ladders' feet are Mound over the ground: the walk is W1 - Mound over them.
                root.gameObject.AddComponent<LadderDefender>().Configure(d, pole, LadderL, LadderFootZ, W1 - Mound, DefenderZ);
            }
        }

        // ------------------------------------------------------------------ 2. the murder holes

        static void MurderHoles(CourseBuilder b)
        {
            // Over the gate the walk is 15 trapdoors of 3 m over the passage; the parapet runs on in front.
            b.Box("RR outer gate parapet", V(GateX0, 0f, Face1), V(GateX1, W1 + SillH, HolesZ0), Look.NoClimb, true);
            for (float x = GateX0; x < GateX1 - 1f; x += Pitch)
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
                    YardKit.Box(hinge, "RR trapdoor lid plank", V(0f, 0f, Hole * .5f - .02f), V(Hole - .08f, .05f, Hole - .08f), light ? Light : Dark, YardKit.Solid.None);
                    lids.Add(hinge);
                    var rim = new GameObject("RR trapdoor rattle").transform;
                    rim.SetParent(group, false);
                    rim.localPosition = V(x0 + Hole * .5f, W1 + .02f, z0 + Hole * .5f);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        YardKit.Box(rim, "RR trapdoor rattle rim", V(s * (Hole * .5f - .08f), 0f, 0f), V(.16f, .03f, Hole), Rattle, YardKit.Solid.None);
                        YardKit.Box(rim, "RR trapdoor rattle rim", V(0f, 0f, s * (Hole * .5f - .08f)), V(Hole, .03f, .16f), Rattle, YardKit.Solid.None);
                    }
                    rims.Add(rim.gameObject);
                }
            group.gameObject.AddComponent<MurderHoles>().Configure(floors.ToArray(), lids.ToArray(), rims.ToArray(), HolesX, HolesZ, Hole);
            // Under it the gate passage; whoever falls in goes back to the walk's checkpoint (the design's stair).
            b.Trigger<KillVolume>("RR kill volume (the gate passage)", V(GateX0, -.5f, HolesZ0), V(GateX1, .6f, Face1 + WallT));
        }

        // ------------------------------------------------------------------ 3. the siege towers

        // The three inner ladders: up the west side, across, up the east side, across, up the west side again - each from a
        // floor through a hatch in the one above, leaning north 0.6 m, 0.5 m over the floor it reaches.
        static readonly float[] TowerLadderX = { -1.5f, 1.5f, -1.5f };
        // A hatch's north edge clear of where the climb's top-out lands (0.63 m north of the climber at the ladder's top).
        const float TowerLadderZ = -1.8f, HatchZ0 = -2.6f, HatchZ1 = -1f, HatchHalf = .65f;

        static void SiegeTowers(CourseBuilder b)
        {
            for (int k = 0; k < SiegeX.Length; k++)
            {
                float x = SiegeX[k], h = SiegeW * .5f, top = Floors[Floors.Length - 1];
                string name = "RR siege tower " + (k == 0 ? "A" : "B");
                var t = Root(b, name, V(x, 0f, SiegeZ0));
                // Floors (violet: they move); every one above the first with its ladder's hatch.
                for (int f = 0; f < Floors.Length; f++)
                {
                    string fn = name + " floor " + (f + 1);
                    if (f == 0) { Slab(t, fn, -h, h, -h, h, Floors[f]); continue; }
                    float hx = TowerLadderX[f - 1], x0 = hx - HatchHalf, x1 = hx + HatchHalf;
                    Slab(t, fn, -h, h, -h, HatchZ0, Floors[f]);
                    Slab(t, fn, -h, h, HatchZ1, h, Floors[f]);
                    Slab(t, fn, -h, x0, HatchZ0, HatchZ1, Floors[f]);
                    Slab(t, fn, x1, h, HatchZ0, HatchZ1, Floors[f]);
                }
                // The inner ladders (held: orange).
                for (int l = 0; l < TowerLadderX.Length; l++)
                {
                    float rise = Floors[l + 1] - Floors[l] + .5f, lean = .6f * rise / (Floors[l + 1] - Floors[l]);
                    YardKit.Slab(t, name + " ladder " + (l + 1), V(TowerLadderX[l], Floors[l], TowerLadderZ), V(TowerLadderX[l], Floors[l] + rise, TowerLadderZ + lean),
                                 LadderW, .12f, Look.Climb, YardKit.Solid.Solid);
                }
                // The hides: both sides and the front up to the top floor (the raised bridge closes the front above it), the
                // roof; the back is open.
                float hideTop = top + 2.1f;
                YardKit.Box(t, name + " hide W", V(-h - .08f, (1.2f + hideTop) * .5f, 0f), V(.16f, hideTop - 1.2f, SiegeW), Hide, YardKit.Solid.NoClimb);
                YardKit.Box(t, name + " hide E", V(h + .08f, (1.2f + hideTop) * .5f, 0f), V(.16f, hideTop - 1.2f, SiegeW), Hide, YardKit.Solid.NoClimb);
                YardKit.Box(t, name + " hide front", V(0f, (1.2f + top) * .5f, h + .08f), V(SiegeW + .32f, top - 1.2f, .16f), Hide, YardKit.Solid.NoClimb);
                YardKit.Box(t, name + " roof", V(0f, hideTop, -.2f), V(SiegeW + .5f, .25f, SiegeW + .1f), Wood, YardKit.Solid.NoClimb);
                // The base: posts, the wheels, the crew pushing inside.
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                        YardKit.Box(t, name + " post", V(sx * (h - .15f), Floors[0] * .5f + .4f, sz * (h - .15f)), V(.3f, Floors[0] - .8f, .3f), Wood, YardKit.Solid.None);
                var wheels = new List<Transform>();
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -2f, 2f })
                        wheels.Add(YardKit.Cylinder(t, name + " wheel", V(sx * (h + .2f), .9f, sz), Vector3.right, 1.8f, .3f, Wood, YardKit.Solid.None).transform);
                var crew = new[] { Person(t, name + " crew", V(-1f, 0f, -.8f), Figure), Person(t, name + " crew", V(1f, 0f, -.4f), Figure),
                                   Person(t, name + " crew", V(0f, 0f, .6f), Figure) };
                t.gameObject.AddComponent<SiegeTower>().Configure(SiegeDelay[k], SiegeZ0, SiegeZ1(k), wheels.ToArray(), crew);

                // The drop-bridge on the front over the top floor: built down (0 degrees), the clock raises it.
                var hinge = Root(b, name + " bridge", V(x, top, SiegeZ0 + h + .08f));
                YardKit.Box(hinge, name + " bridge plank", V(0f, -.1f, BridgeL * .5f), V(3.4f, .2f, BridgeL), Look.Mover, YardKit.Solid.Solid).AddComponent<CourseFloor>();
                hinge.gameObject.AddComponent<SiegeBridge>().Configure(SiegeDelay[k], SiegeZ0, SiegeZ1(k));

                // Its ruts across the lower ward.
                foreach (float s in new[] { -2f, 2f })
                    YardKit.Box(b.parent, "RR siege rut", V(x + s, .01f, (Face1 + WallT + Wall2Face(x)) * .5f), V(.4f, .02f, Wall2Face(x) - Face1 - WallT), Rock, YardKit.Solid.None);
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
                                       file ? V(Square * .3f, .01f, along - .6f) : V(along - .6f, .01f, Square * .3f), LineRest, YardKit.Solid.None);
                var rook = Root(b, "RR drill rook " + (n + 1) + (file ? " (file " : " (rank ") + idx + ")", V(start.x, BoardY, start.y));
                YardKit.Cylinder(rook, "RR drill rook body", V(0f, 1.6f, 0f), Vector3.up, 2.6f, 3.2f, Look.Mover, YardKit.Solid.NoClimb);
                YardKit.Cylinder(rook, "RR drill rook crown", V(0f, 3.45f, 0f), Vector3.up, 2.9f, .5f, Look.Mover, YardKit.Solid.NoClimb);
                for (int k = 0; k < 6; k++)
                {
                    var turn = Quaternion.Euler(0f, k * 60f, 0f);
                    YardKit.Box(rook, "RR drill rook merlon", V(0f, 3.95f, 0f) + turn * V(0f, 0f, 1.1f), V(.6f, .5f, .4f), Look.Mover, YardKit.Solid.None, turn);
                }
                rook.gameObject.AddComponent<DrillRook>().Configure(travel, DrillDelay[n], RookSlide, line.GetComponent<Renderer>(), LineRest, LineGlow, LineSlide);
            }
        }

        // ------------------------------------------------------------------ 5. the cranes

        static void Cranes(CourseBuilder b)
        {
            foreach (float x in new[] { RookCraneX, KnightCraneX })
            {
                float zs = Wall2Inner(x) - 7.5f, zn = Wall3Face(x) + 9.4f;
                foreach (float s in new[] { -2.2f, 2.2f })
                {
                    b.Box("RR gantry leg S", V(x + s - .25f, W2, zs - .25f), V(x + s + .25f, BeamY, zs + .25f), Wood, true);
                    b.Box("RR gantry leg N", V(x + s - .25f, W3, zn - .25f), V(x + s + .25f, BeamY, zn + .25f), Wood, true);
                    YardKit.Box(b.parent, "RR gantry beam", V(x + s, BeamY + .35f, (zs + zn) * .5f), V(.45f, .7f, zn - zs + 1.4f), Wood, YardKit.Solid.None);
                }
                foreach (float z in new[] { zs, zn })
                    YardKit.Box(b.parent, "RR gantry tie", V(x, BeamY + .35f, z), V(4.9f, .5f, .45f), Wood, YardKit.Solid.None);
            }

            // The giant rook: its crown is the floor ridden (violet), six merlons round it with gaps north and south.
            var rook = Root(b, "RR rook crane", V(RookCraneX, W2, RcLowZ));
            YardKit.Cylinder(rook, "RR rook crane crown", V(0f, -.2f, 0f), Vector3.up, 2f * CrownR, .4f, Look.Mover, YardKit.Solid.Solid).AddComponent<CourseFloor>();
            for (int k = 0; k < 6; k++)
            {
                var turn = Quaternion.Euler(0f, 30f + k * 60f, 0f);
                YardKit.Box(rook, "RR rook crane merlon", turn * V(0f, .6f, CrownR - .4f), V(1.5f, 1.2f, .7f), Look.NoClimb, YardKit.Solid.NoClimb, turn);
            }
            YardKit.Cylinder(rook, "RR rook crane body", V(0f, -.4f - (RookBody - .4f) * .5f, 0f), Vector3.up, 5f, RookBody - .4f, Look.NoClimb, YardKit.Solid.NoClimb);
            var trolley = YardKit.Box(b.parent, "RR rook crane trolley", V(RookCraneX, BeamY - .7f, RcLowZ), V(5f, .7f, 2.2f), Iron, YardKit.Solid.None).transform;
            var chainTops = new[] { V(-2.6f, 1.2f, 0f), V(2.6f, 1.2f, 0f) };
            var chains = new Transform[chainTops.Length];
            for (int k = 0; k < chains.Length; k++)
                chains[k] = YardKit.Cylinder(b.parent, "RR rook crane chain", V(RookCraneX + chainTops[k].x, 20f, RcLowZ), Vector3.up, .14f, 2f, Iron, YardKit.Solid.None).transform;
            var rookCrane = rook.gameObject.AddComponent<RookCrane>();
            rookCrane.Configure(W2, W3, RcLowZ, RcHighZ, trolley, chains, chainTops, BeamY);

            // The giant knight, facing north, built at its low end: a base, the body, the neck and head (none held) and its
            // mane on its back (held: orange) - a rider hangs there south of it.
            var knight = Root(b, "RR knight crane", V(KnightCraneX, KnightLowY, KnLowZ));
            YardKit.Cylinder(knight, "RR knight base", V(0f, .4f, 0f), Vector3.up, 2f * KnightR, .8f, KnightWood, YardKit.Solid.NoClimb);
            YardKit.Box(knight, "RR knight body", V(0f, 1.85f, 0f), V(2f, 2.1f, 2.3f), KnightWood, YardKit.Solid.NoClimb);
            YardKit.Box(knight, "RR knight neck", V(0f, 3.9f, .35f), V(1.5f, 2.4f, 1.5f), KnightWood, YardKit.Solid.NoClimb, Quaternion.Euler(18f, 0f, 0f));
            YardKit.Box(knight, "RR knight head", V(0f, 5.1f, 1.3f), V(1.3f, 1.2f, 2.3f), KnightWood, YardKit.Solid.NoClimb, Quaternion.Euler(-12f, 0f, 0f));
            foreach (float s in new[] { -1f, 1f })
                YardKit.Box(knight, "RR knight ear", V(s * .35f, 5.85f, .75f), V(.24f, .55f, .24f), KnightWood, YardKit.Solid.None);
            // South of the body's face (-1.15), so nothing unclimbable stands in front of it.
            YardKit.Box(knight, "RR knight mane", V(0f, 3.4f, -1.3f), V(1.2f, 2.6f, .4f), Look.Climb, YardKit.Solid.Solid);
            var ktrolley = YardKit.Box(b.parent, "RR knight crane trolley", V(KnightCraneX, BeamY - .7f, KnLowZ), V(5f, .7f, 2.2f), Iron, YardKit.Solid.None).transform;
            var cable = YardKit.Cylinder(b.parent, "RR knight crane cable", V(KnightCraneX, 24f, KnLowZ), Vector3.up, .16f, 2f, Iron, YardKit.Solid.None).transform;
            var knightCrane = knight.gameObject.AddComponent<KnightCrane>();
            knightCrane.Configure(KnightLowY, W3, KnLowZ, KnHighZ, ktrolley, cable, BeamY, KnightH);

            // The treadwheels on the inner wall's walk, beside the gantries' north legs, walked by a worker each.
            foreach (var (x, rc, kc) in new[] { (RookCraneX - 5f, rookCrane, (KnightCrane)null), (KnightCraneX + 5f, (RookCrane)null, knightCrane) })
            {
                var w = Root(b, "RR treadwheel", V(x, W3 + 2.1f, Wall3Face(x) + 6.5f));
                var wheel = new GameObject("RR treadwheel wheel").transform;
                wheel.SetParent(w, false);
                YardKit.Cylinder(wheel, "RR treadwheel rim", Vector3.zero, Vector3.right, 4f, .8f, Wood, YardKit.Solid.None);
                for (int k = 0; k < 4; k++)
                    YardKit.Box(wheel, "RR treadwheel spoke", Vector3.zero, V(.12f, 4f, .14f), Iron, YardKit.Solid.None, Quaternion.Euler(k * 45f, 0f, 0f));
                Person(w, "RR treadwheel worker", V(0f, -1.95f, 0f), Figure);
                w.gameObject.AddComponent<Treadwheel>().Configure(rc, kc, wheel);
            }

            // Red marks: where the knight comes down on the inner wall (stand clear).
            YardKit.Cylinder(b.parent, "RR knight landing mark", V(KnightCraneX, W3 + .015f, KnHighZ), Vector3.up, 2f * KnightR + 1.2f, .02f, Mark, YardKit.Solid.None);
        }

        // ------------------------------------------------------------------ 6. the king's banner

        static void Banner(CourseBuilder b)
        {
            float midZ = (BannerZ0 + BannerZ1) * .5f;
            // The stone ledge between the lengths, along the face (not held: it is stood on).
            b.Box("RR banner ledge", V(GateHX0 - LedgeDepth, LedgeY - .3f, LedgeZ0), V(GateHX0, LedgeY, LedgeZ1), Look.Rest, true).AddComponent<CourseFloor>();
            // The lower length hangs from the ledge's front edge to the walk; the upper one from the roof's edge, flat on the wall.
            Length(b, "RR king's banner lower", V(GateHX0 - LedgeDepth - .06f, LedgeY - .05f, midZ), LedgeY - .05f - W3 - .05f, BannerW);
            Length(b, "RR king's banner upper", V(GateHX0 - .05f, GateY, midZ), GateY - LedgeY - .05f, BannerW);
            // The pennants on the roof that stream a second before every gust.
            var flags = new List<Transform>();
            foreach (float z in new[] { GateHZ0 + 11f, GateHZ0 + 14f, GateHZ0 + 17f })
            {
                YardKit.Cylinder(b.parent, "RR pennant pole", V(GateHX0 + 3f, GateY + 1.8f, z), Vector3.up, .14f, 3.6f, Wood, YardKit.Solid.None);
                var flag = new GameObject("RR pennant").transform;
                flag.SetParent(b.parent, false);
                flag.localPosition = V(GateHX0 + 3f, GateY + 3.4f, z);
                YardKit.Box(flag, "RR pennant cloth", V(-1f, 0f, 0f), V(2f, .8f, .05f), Pennant, YardKit.Solid.None);
                flags.Add(flag);
            }
            b.Group("RR pennants").gameObject.AddComponent<RampartPennants>().Configure(flags.ToArray());
        }

        // A length of the banner hanging from `top` (its top edge's middle), `length` down, `width` along z: held on its
        // outer (west) face by one flat plate (no picture); the picture a chain of strips that ripples (KingsBanner),
        // white with a gold border and a gold crown.
        const int BannerStrips = 8;

        static void Length(CourseBuilder b, string name, Vector3 top, float length, float width)
        {
            var hinge = Root(b, name, top);
            var plate = YardKit.Box(hinge, name + " cloth", V(0f, -length * .5f, 0f), V(.06f, length, width), null, YardKit.Solid.Solid);
            Object.DestroyImmediate(plate.GetComponent<MeshRenderer>());
            YardKit.Box(hinge, name + " rod", V(0f, .05f, 0f), V(.16f, .16f, width + .4f), Gold, YardKit.Solid.None);
            float h = length / BannerStrips;
            var strips = new Transform[BannerStrips];
            Transform above = hinge;
            for (int i = 0; i < BannerStrips; i++)
            {
                var s = new GameObject(name + " strip " + (i + 1)).transform;
                s.SetParent(above, false);
                s.localPosition = i == 0 ? Vector3.zero : V(0f, -h, 0f);
                YardKit.Box(s, name + " strip cloth", V(0f, -h * .5f, 0f), V(.05f, h + .02f, width), Cloth, YardKit.Solid.None);
                foreach (float e in new[] { -1f, 1f })
                    YardKit.Box(s, name + " strip border", V(-.03f, -h * .5f, e * (width * .5f - .09f)), V(.02f, h + .02f, .18f), Gold, YardKit.Solid.None);
                if (i == BannerStrips - 1) YardKit.Box(s, name + " hem", V(-.03f, -h + .1f, 0f), V(.02f, .2f, width), Gold, YardKit.Solid.None);
                if (i == BannerStrips / 2 - 1 || i == BannerStrips / 2) YardKit.Box(s, name + " crown", V(-.035f, -h * .5f, 0f), V(.02f, h * .8f, width * .3f), Gold, YardKit.Solid.None);
                strips[i] = s;
                above = s;
            }
            hinge.gameObject.AddComponent<KingsBanner>().Configure(strips, length, width);
        }
    }
}
