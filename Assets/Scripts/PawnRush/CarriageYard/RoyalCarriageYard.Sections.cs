using ChessFight.Gameplay;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // The four sections of the Royal Carriage Yard. Sections 1 and 2 are in map coordinates (metres, the start court's
    // north edge at z 0, the ground at y 0); 3 and 4 in their own frame (the 11-02 numbers), built under `later`, which
    // is moved LaterShift south. The numbers and why they are what they are: Docs/KingRush/ROYAL_CARRIAGE_YARD.md.
    public sealed partial class RoyalCarriageYard
    {
        // 1 and 2, map coordinates.
        const float YardHalf = 22f, YardZ0 = 2f, YardZ1 = 50f;
        const float CanalZ0 = 50f, CanalZ1 = 82f, WalkZ0 = 58f, WalkZ1 = 74f, WheelZ = 66f, CanalWater = -1.2f;
        // The walkways' inner edges, either side of the giant wheel's axle.
        const float WalkInner = 3f;
        // Half the walkways' break at the wheel (the wheel itself is 1 m deep): 2.6 m is a jump, not a step. At the
        // pawn's real speeds (Settings/RagdollTuning: run 2.5, sprint 3.6 m/s; ~0.69 s in the air) a sprint jump from
        // within ~0.45 m of the edge clears it on the ledge help, a plain run jump falls short and a jump + dive
        // (+3 m/s) has room to spare. Whoever waits at the edge stands 0.7 m off the spokes' hit shell.
        public const float WheelGapHalf = 1.3f;
        // 3 and 4, their own frame: the arena starts at z 106 there, and is moved to start where the canal ends.
        const float ArenaStart = 106f, LaterShift = CanalZ1 - ArenaStart;
        // The arena's carousel and the lifting waterwheel, where several parts are fitted round each other.
        const float CarouselZ = 125.5f, PoolZ0 = 139.3f, LiftZ = 144f, LiftY = 4.6f, LiftRadius = 5.2f, CliffZ = 150f, Top = 8f;

        // ------------------------------------------------------------------ 1. the outer gate and the yard

        void Entrance()
        {
            var s = Group("1 Entrance");
            // The start court behind the gate, its twelve start places (white in front: the map test starts there). Its
            // floor runs on under the gate to the yard: the opening has no floor of its own.
            YardKit.Floor(s, "Start court", -12f, 12f, -16f, YardZ0, 0f);
            YardKit.Wall(s, "Court wall W", -13f, -12f, 0f, 5f, -17f, 0f, YardKit.WallStone);
            YardKit.Wall(s, "Court wall E", 12f, 13f, 0f, 5f, -17f, 0f, YardKit.WallStone);
            YardKit.Wall(s, "Court wall S", -13f, 13f, 0f, 5f, -17f, -16f, YardKit.WallStone);
            for (int i = 0; i < 6; i++)
            {
                Place<SpawnPoint>(s, "Start white " + i, new Vector3(-5f + 2f * i, 0f, -7f)).Configure(i, Teams.White);
                Place<SpawnPoint>(s, "Start black " + i, new Vector3(-5f + 2f * i, 0f, -10f)).Configure(i, Teams.Black);
            }
            ParkedCarriage(s, new Vector3(-9.8f, 0f, -12.5f), 0f, YardKit.CarriageBlue);
            Crates(s, new Vector3(9.8f, 0f, -14.6f), 2);

            // The outer gate across the yard's whole width: a 6 m opening, two towers, two leaves that open when the
            // race starts.
            YardKit.Wall(s, "Gate wall W", -YardHalf - 1f, -3f, 0f, 7f, 0f, YardZ0, YardKit.WallStone);
            YardKit.Wall(s, "Gate wall E", 3f, YardHalf + 1f, 0f, 7f, 0f, YardZ0, YardKit.WallStone);
            YardKit.Wall(s, "Gate lintel", -3f, 3f, 5.6f, 7f, 0f, YardZ0, YardKit.WallStone);
            YardKit.Picture(s, "Gate lintel trim", new Vector3(-3.2f, 5.5f, -.08f), new Vector3(3.2f, 5.7f, .02f), YardKit.Gold);
            foreach (float x in new[] { -4.8f, 4.8f })
                Tower(s, new Vector3(x, 0f, 1f), 3f, 10f);
            var count = YardKit.Label(s, "", new Vector3(0f, 6.3f, -.3f), 3f, new Color(1f, .86f, .4f));
            Place<TimedGate>(s, "Start gate W", new Vector3(-3f, 0f, 1f)).Configure(3f, 5.4f, false, gates.startDelay, gates.startDuration, count);
            Place<TimedGate>(s, "Start gate E", new Vector3(3f, 0f, 1f)).Configure(3f, 5.4f, true, gates.startDelay, gates.startDuration);
            Sign(s, "왕실 마차뜰", new Vector3(0f, 8.2f, -.3f), 2.2f);

            // The yard, right behind the gate: four carriages charge about it at random.
            YardKit.Floor(s, "Yard", -YardHalf, YardHalf, YardZ0, YardZ1, 0f);
            YardKit.Wall(s, "Yard wall W", -YardHalf - 1f, -YardHalf, 0f, 5f, YardZ0, YardZ1, YardKit.WallStone);
            YardKit.Wall(s, "Yard wall E", YardHalf, YardHalf + 1f, 0f, 5f, YardZ0, YardZ1, YardKit.WallStone);
            RoamingCarriages(s);
            // In front of the closed gate, where the start court looks.
            Sign(s, "1. 마차뜰: 돌진하는 마차 피하기", new Vector3(0f, 4.3f, -.7f), 1.1f);
        }

        // The four carriages share the whole yard (CarriageFleet): each keeps charging to a random point anywhere in it,
        // planned clear of the others', so they never meet and there are no lanes or seams to hide in. Their centres
        // stay BodyRadius + 0.2 m inside the walls and the canal's parapet (2.5 m more clear of the gate's open leaves),
        // so the strip along the walls is seldom swept: a safer line, but the long way round (~33 m more than straight
        // across). They start moving when the gate opens.
        void RoamingCarriages(Transform parent)
        {
            float keep = RoamingCarriage.BodyRadius + .2f;
            Place<CarriageFleet>(parent, "Roaming carriages", Vector3.zero)
                .Configure(-(YardHalf - keep), YardHalf - keep, YardZ0 + keep + 2.5f, YardZ1 - keep, 4, roaming.moveSpeed, roaming.turnSeconds,
                           roaming.minAim, roaming.maxAim, gates.startDelay, roaming.seed, roaming.knockbackForce, roaming.knockdownSeconds);
        }

        // ------------------------------------------------------------------ 2. the canal and the giant waterwheel

        void Canal()
        {
            var s = Group("2 Canal");
            float inner = WalkInner, width = crossing.passageWidth, outer = inner + width, radius = crossing.wheelRadius;
            // The axle as high as lets the round frame's inside pass 0.4 m under the walkways' outer edges: the walkways
            // go through the wheel inside its frame, through the holes between the spokes.
            float inside = radius - HorizontalWaterwheel.DefaultRim;
            float axle = Mathf.Sqrt(Mathf.Max(0f, inside * inside - outer * outer)) - .4f;
            float bed = Mathf.Min(-4.5f, axle - radius - .5f);
            YardKit.Floor(s, "Canal bed", -YardHalf, YardHalf, CanalZ0, CanalZ1, bed, YardKit.WallStone);
            YardKit.Span(s, "Bank S", new Vector3(-YardHalf, bed, CanalZ0 - 1f), new Vector3(YardHalf, -1f, CanalZ0), YardKit.WallStone);
            YardKit.Span(s, "Bank N", new Vector3(-YardHalf, bed, CanalZ1), new Vector3(YardHalf, -1f, CanalZ1 + 1f), YardKit.WallStone);
            YardKit.Wall(s, "Canal wall W", -YardHalf - 1f, -YardHalf, bed, 5f, CanalZ0, CanalZ1, YardKit.WallStone);
            YardKit.Wall(s, "Canal wall E", YardHalf, YardHalf + 1f, bed, 5f, CanalZ0, CanalZ1, YardKit.WallStone);
            YardKit.Picture(s, "Water", new Vector3(-YardHalf, bed, CanalZ0), new Vector3(YardHalf, CanalWater, CanalZ1), YardKit.Water);
            // A fall into the canal: in the water, a moment, back at the last checkpoint.
            Trigger<KillVolume>(s, "Canal (fall)", new Vector3(-YardHalf, bed, CanalZ0), new Vector3(YardHalf, CanalWater - .3f, CanalZ1));
            // Parapets on the canal's edges beside the landings: nobody drops in from the yard or the arena by accident.
            foreach (float side in new[] { -1f, 1f })
            {
                float a = side * 10f, b = side * YardHalf;
                YardKit.Wall(s, "Parapet S", Mathf.Min(a, b), Mathf.Max(a, b), bed, .9f, CanalZ0, CanalZ0 + .4f, YardKit.WallStone);
                YardKit.Wall(s, "Parapet N", Mathf.Min(a, b), Mathf.Max(a, b), bed, .9f, CanalZ1 - .4f, CanalZ1, YardKit.WallStone);
            }

            // The landings either side, railed but where the walkways meet them.
            Landing(s, "Entry landing", CanalZ0, WalkZ0, true, inner, outer, bed);
            Landing(s, "Far landing", WalkZ1, CanalZ1, false, inner, outer, bed);

            // The two walkways, wood, either side of the wheel's axle, broken where the wheel turns: the gap is jumped
            // through a hole between two spokes.
            float gap = WheelGapHalf;
            foreach (float side in new[] { -1f, 1f })
            {
                Walkway(s, side, inner, outer, WalkZ0, WheelZ - gap, bed, false);
                Walkway(s, side, inner, outer, WheelZ + gap, WalkZ1, bed, true);
            }

            // The wheel, its axle on two posts in the water between the walkways (as in the photo), the flume over it.
            Place<HorizontalWaterwheel>(s, "Giant waterwheel", new Vector3(0f, axle, WheelZ))
                .Configure(crossing.rotationSpeed, crossing.clockwise, radius, crossing.spokeCount, crossing.knockbackForce,
                           inner + width * .5f, width, axle);
            foreach (float z in new[] { WheelZ - 1.2f, WheelZ + 1.2f })
            {
                YardKit.Span(s, "Axle post", new Vector3(-.35f, bed, z - .3f), new Vector3(.35f, axle - .3f, z + .3f), YardKit.WoodDark, Solid.NoClimb);
                YardKit.Span(s, "Bearing", new Vector3(-.45f, axle - .35f, z - .35f), new Vector3(.45f, axle + .3f, z + .35f), YardKit.Iron, Solid.None);
            }
            YardKit.Cylinder(s, "Axle", new Vector3(0f, axle, WheelZ), Vector3.forward, .45f, 3.2f, YardKit.Iron, Solid.None);
            Flume(s, axle, radius, bed);

            // Across the whole landing, railing to railing: a pawn heading for either walkway's outer edge passes them.
            Checkpoint(s, 1, new Vector3(0f, 0f, CanalZ0 + 3f), 19.2f);
            Checkpoint(s, 2, new Vector3(0f, 0f, CanalZ1 - 3.5f), 19.2f);
            Sign(s, "2. 거대 물레방아: 바퀴살 사이 구멍으로 점프 + 다이빙", new Vector3(0f, 2.6f, CanalZ0 + 5.5f), 1.2f);
        }

        // A stone landing over the canal, z0..z1, 20 m wide: railed on its sides and on its canal edge (the north edge
        // when `walkwaysNorth`, else the south) but where the two walkways meet it.
        void Landing(Transform s, string name, float z0, float z1, bool walkwaysNorth, float inner, float outer, float bed)
        {
            YardKit.Floor(s, name, -10f, 10f, z0, z1, 0f);
            YardKit.Wall(s, name + " railing W", -10f, -9.6f, 0f, .9f, z0, z1);
            YardKit.Wall(s, name + " railing E", 9.6f, 10f, 0f, .9f, z0, z1);
            float e0 = walkwaysNorth ? z1 - .4f : z0, e1 = e0 + .4f;
            YardKit.Wall(s, name + " railing", -9.6f, -outer, 0f, .9f, e0, e1);
            YardKit.Wall(s, name + " railing", -inner, inner, 0f, .9f, e0, e1);
            YardKit.Wall(s, name + " railing", outer, 9.6f, 0f, .9f, e0, e1);
            float mid = (z0 + z1) * .5f;
            foreach (float x in new[] { -7f, 7f })
                YardKit.Span(s, name + " pier", new Vector3(x - .7f, bed, mid - .7f), new Vector3(x + .7f, -1f, mid + .7f), YardKit.WallStone);
        }

        // One wooden walkway over the water on the `side` of the axle (-1 west, +1 east), z0..z1, posts down to the canal
        // bed, gold along the end that meets the wheel's gap (`gapAtStart`: its south end): jump from there.
        static void Walkway(Transform s, float side, float inner, float outer, float z0, float z1, float bed, bool gapAtStart)
        {
            float x0 = side < 0f ? -outer : inner, x1 = side < 0f ? -inner : outer;
            YardKit.Span(s, "Walkway", new Vector3(x0, -.35f, z0), new Vector3(x1, 0f, z1), YardKit.WoodLight).AddComponent<CourseFloor>();
            for (float z = z0 + .5f; z + .45f <= z1; z += 1f)
                YardKit.Picture(s, "Walkway plank", new Vector3(x0 + .03f, 0f, z - .45f), new Vector3(x1 - .03f, .03f, z + .45f), YardKit.WoodDark);
            float edge = gapAtStart ? z0 : z1 - .18f;
            YardKit.Picture(s, "Gap edge", new Vector3(x0, 0f, edge), new Vector3(x1, .04f, edge + .18f), YardKit.Gold);
            foreach (float z in new[] { z0 + .4f, z1 - .4f })
                foreach (float x in new[] { x0 + .25f, x1 - .25f })
                    YardKit.Cylinder(s, "Walkway post", new Vector3(x, (bed - .35f) * .5f, z), Vector3.up, .25f, -.35f - bed, YardKit.WoodDark, Solid.None);
        }

        // The wooden flume that feeds the wheel from above (as in the photo): a trough from over the wheel's top to a
        // cistern on two trestles standing in the water between the walkways, water pouring from its mouth onto the
        // wheel and spilling down the side the wheel turns down. Pictures, but for the trestles.
        void Flume(Transform s, float axle, float radius, float bed)
        {
            float y = axle + radius + .35f, z0 = WheelZ - .3f, z1 = WalkZ1 - 1f;
            YardKit.Span(s, "Flume", new Vector3(-.8f, y, z0), new Vector3(.8f, y + .2f, z1), YardKit.WoodDark, Solid.None);
            foreach (float x in new[] { -.8f, .7f })
                YardKit.Span(s, "Flume side", new Vector3(x, y + .2f, z0), new Vector3(x + .1f, y + .7f, z1), YardKit.WoodDark, Solid.None);
            YardKit.Picture(s, "Flume water", new Vector3(-.7f, y + .2f, z0), new Vector3(.7f, y + .5f, z1), YardKit.Water);
            YardKit.Span(s, "Cistern", new Vector3(-1.2f, y - .2f, z1 - .6f), new Vector3(1.2f, y + 1.4f, z1 + .8f), YardKit.WoodDark, Solid.None);
            YardKit.Picture(s, "Cistern band", new Vector3(-1.25f, y + .9f, z1 - .65f), new Vector3(1.25f, y + 1.05f, z1 + .85f), YardKit.Iron);
            foreach (float z in new[] { WheelZ + 4f, z1 })
            {
                foreach (float x in new[] { -.6f, .6f })
                    YardKit.Span(s, "Trestle post", new Vector3(x - .15f, bed, z - .15f), new Vector3(x + .15f, y - .2f, z + .15f), YardKit.WoodDark, Solid.NoClimb);
                YardKit.Span(s, "Trestle beam", new Vector3(-.9f, y - .45f, z - .18f), new Vector3(.9f, y - .2f, z + .18f), YardKit.WoodDark, Solid.None);
            }
            // The pour from the flume's mouth onto the top of the wheel, and the spill down its falling side.
            var pour = YardKit.Picture(s, "Pour", new Vector3(-.6f, axle + radius - .2f, z0 - .06f), new Vector3(.6f, y + .3f, z0 - .02f), YardKit.Waterfall);
            pour.AddComponent<WaterfallFlow>();
            float fall = crossing.clockwise ? 1f : -1f;
            var spill = YardKit.Picture(s, "Spill", new Vector3(Mathf.Min(fall * (radius - .9f), fall * (radius + .1f)), CanalWater, WheelZ - HorizontalWaterwheel.DefaultDepth * .5f - .14f),
                                        new Vector3(Mathf.Max(fall * (radius - .9f), fall * (radius + .1f)), axle, WheelZ - HorizontalWaterwheel.DefaultDepth * .5f - .1f), YardKit.Waterfall);
            spill.AddComponent<WaterfallFlow>();
            YardKit.Picture(s, "Foam", new Vector3(Mathf.Min(fall * (radius - 1.6f), fall * (radius + .8f)), CanalWater, WheelZ - 1.4f),
                            new Vector3(Mathf.Max(fall * (radius - 1.6f), fall * (radius + .8f)), CanalWater + .06f, WheelZ + 1.4f), Arena.Glow(new Color(1f, 1f, 1f, .45f)));
        }

        // ------------------------------------------------------------------ 3. the arena, the ramps, the waterfall (own frame)

        void CarriageArena()
        {
            var s = Group("3 Arena", later);
            // Floors: the whole width up to the pool, then either side of it.
            YardKit.Floor(s, "Arena", -26f, 26f, ArenaStart, PoolZ0, 0f);
            YardKit.Floor(s, "Arena W of pool", -26f, -6f, PoolZ0, CliffZ, 0f);
            YardKit.Floor(s, "Arena E of pool", 6f, 26f, PoolZ0, CliffZ, 0f);
            YardKit.Wall(s, "Arena wall SW", -27f, -22f, 0f, 6f, ArenaStart - 1f, ArenaStart, YardKit.WallStone);
            YardKit.Wall(s, "Arena wall SE", 22f, 27f, 0f, 6f, ArenaStart - 1f, ArenaStart, YardKit.WallStone);
            YardKit.Wall(s, "Side wall W", -27f, -26f, 0f, Top + 3f, ArenaStart - 1f, 176f, YardKit.WallStone);
            YardKit.Wall(s, "Side wall E", 26f, 27f, 0f, Top + 3f, ArenaStart - 1f, 176f, YardKit.WallStone);

            Carousel(s);
            CargoRamp(s, -1f);
            CargoRamp(s, 1f);
            Waterfall(s);
            Sign(s, "3. 마차 경기장: 가운데 회전 마차 · 좌우 화물 경사로", new Vector3(0f, 3.6f, ArenaStart + 3.5f), 1.2f);
        }

        void Carousel(Transform parent)
        {
            var s = Group("Carousel", parent);
            Place<RotatingCarriageGroup>(s, "Rotating carriages", new Vector3(0f, 0f, CarouselZ))
                .Configure(carousel.rotationSpeed, carousel.clockwise, Mathf.Min(7.5f, carousel.rotationRadius), carousel.carriageCount,
                           carousel.knockbackForce);
            // Dividers between the middle way and the ramps (not to be climbed), and planters that close the middle
            // round the carousel, so the way through is between the carriages and not round them.
            foreach (float side in new[] { -1f, 1f })
            {
                YardKit.Wall(s, "Divider", Mathf.Min(side * 13f, side * 18f), Mathf.Max(side * 13f, side * 18f), 0f, 2.2f, 116f, CliffZ);
                YardKit.Picture(s, "Divider hedge", new Vector3(Mathf.Min(side * 13.3f, side * 17.7f), 2.2f, 116.3f),
                                new Vector3(Mathf.Max(side * 13.3f, side * 17.7f), 2.9f, CliffZ - .3f), YardKit.Hedge);
                YardKit.Wall(s, "Planter", Mathf.Min(side * 10f, side * 13f), Mathf.Max(side * 10f, side * 13f), 0f, 1.2f, 117.5f, 133.5f);
                YardKit.Picture(s, "Planter hedge", new Vector3(Mathf.Min(side * 10.2f, side * 12.8f), 1.2f, 117.7f),
                                new Vector3(Mathf.Max(side * 10.2f, side * 12.8f), 1.8f, 133.3f), YardKit.Hedge);
                for (float z = 120f; z < CliffZ; z += 7f)
                    Tree(s, new Vector3(side * 15.5f, 2.9f, z));
            }
            Sign(s, "가운데: 회전 마차 사이로", new Vector3(0f, 6.8f, 114.5f), 1.2f);
        }

        // A ramp up to the platform at x side*22 (8 m wide), z 118..150, y 0..8, with cargo rolling down it out of a
        // hatch in the gatehouse over its top, into a drain at its foot.
        void CargoRamp(Transform parent, float side)
        {
            var s = Group(side < 0f ? "Left cargo ramp" : "Right cargo ramp", parent);
            float x = side * 22f, x0 = x - 4f, x1 = x + 4f;
            var ramp = YardKit.Slab(s, "Ramp", new Vector3(x, 0f, 118f), new Vector3(x, Top, CliffZ), 8f, 8.5f, YardKit.Stone);
            ramp.AddComponent<CourseFloor>().sloped = true;
            YardKit.Slab(s, "Ramp edge trim", new Vector3(x - side * 3.95f, .02f, 118f), new Vector3(x - side * 3.95f, Top + .02f, CliffZ), .1f, .05f, YardKit.Gold, Solid.None);
            YardKit.Picture(s, "Drain grate", new Vector3(x0, 0f, 116f), new Vector3(x1, .03f, 118.2f), YardKit.Iron);
            for (float gx = x0 + .4f; gx < x1; gx += .6f)
                YardKit.Picture(s, "Grate slot", new Vector3(gx - .1f, .03f, 116.2f), new Vector3(gx + .1f, .035f, 118f), Arena.Lit("M_YardDrain", Arena.Hex("#0E0F12"), 0f, .9f));

            // The gatehouse over the top of the ramp: two pillars, a lintel with the hatch the cargo falls out of.
            YardKit.Span(s, "Gatehouse pillar", new Vector3(x0, 5f, 145.5f), new Vector3(x0 + .6f, 12.8f, 147f), YardKit.WallStone, Solid.NoClimb);
            YardKit.Span(s, "Gatehouse pillar", new Vector3(x1 - .6f, 5f, 145.5f), new Vector3(x1, 12.8f, 147f), YardKit.WallStone, Solid.NoClimb);
            YardKit.Span(s, "Gatehouse lintel", new Vector3(x0, 10.4f, 145f), new Vector3(x1, 13f, CliffZ), YardKit.WallStone, Solid.NoClimb);
            YardKit.Picture(s, "Hatch", new Vector3(x0 + .8f, 10.37f, 145.6f), new Vector3(x1 - .8f, 10.4f, 149.6f), YardKit.Glass);
            YardKit.Picture(s, "Gatehouse trim", new Vector3(x0 - .05f, 10.3f, 144.92f), new Vector3(x1 + .05f, 10.5f, 145f), YardKit.Gold);
            YardKit.Cone(s, "Gatehouse roof", new Vector3(x, 13f, 147.5f), 7f, 3f, YardKit.Roof);

            // The lane starts on the ramp's surface at z 148 and runs down it to the foot.
            const float laneTop = 148f;
            float yTop = Top * (laneTop - 118f) / (CliffZ - 118f);
            var down = new Vector3(0f, -yTop, 118f - laneTop);
            var chute = Place<CargoChute>(s, "Cargo chute", new Vector3(x, yTop, laneTop));
            chute.transform.localRotation = Quaternion.LookRotation(down.normalized, Vector3.up);
            chute.Configure(cargo.spawnInterval, cargo.rollingSpeed, cargo.cargoSizes, down.magnitude, 7f, 3.4f,
                            side > 0f ? cargo.rightRampOffset : 0f, side > 0f ? 7 : 3, cargo.knockbackForce);
            Sign(s, side < 0f ? "왼쪽: 화물 경사로" : "오른쪽: 화물 경사로", new Vector3(x, 3.2f, 115.5f), 1.2f);
        }

        // The pool, the lifting waterwheel, the falling water behind it and the landing at its top.
        void Waterfall(Transform parent)
        {
            var s = Group("Waterfall and lifting wheel", parent);
            const float water = -.5f;
            YardKit.Floor(s, "Pool bed", -6f, 6f, PoolZ0, CliffZ, -3f, YardKit.WallStone);
            YardKit.Span(s, "Pool side W", new Vector3(-7f, -3f, PoolZ0), new Vector3(-6f, -1f, CliffZ), YardKit.WallStone);
            YardKit.Span(s, "Pool side E", new Vector3(6f, -3f, PoolZ0), new Vector3(7f, -1f, CliffZ), YardKit.WallStone);
            YardKit.Span(s, "Pool side S", new Vector3(-6f, -3f, PoolZ0 - 1f), new Vector3(6f, -1f, PoolZ0), YardKit.WallStone);
            YardKit.Picture(s, "Pool water", new Vector3(-6f, -3f, PoolZ0), new Vector3(6f, water, CliffZ), YardKit.Water);
            Trigger<KillVolume>(s, "Pool (fall)", new Vector3(-6f, -3f, PoolZ0), new Vector3(6f, water - .3f, CliffZ));
            YardKit.Picture(s, "Landing edge trim", new Vector3(-6f, -.01f, PoolZ0 - .25f), new Vector3(6f, .02f, PoolZ0), YardKit.Gold);

            // The wheel on two piers in the pool. Its near side rises past the landing's edge.
            Place<VerticalWaterwheel>(s, "Lifting waterwheel", new Vector3(0f, LiftY, LiftZ))
                .Configure(lift.rotationSpeed, LiftRadius, lift.bladeCount, lift.releaseAngle, lift.releaseForward, lift.releaseUp);
            foreach (float x in new[] { -2.5f, 2.5f })
            {
                YardKit.Span(s, "Pier", new Vector3(x - .5f, -3f, LiftZ - .7f), new Vector3(x + .5f, LiftY - .3f, LiftZ + .7f), YardKit.WallStone, Solid.NoClimb);
                YardKit.Span(s, "Bearing", new Vector3(x - .55f, LiftY - .3f, LiftZ - .45f), new Vector3(x + .55f, LiftY + .2f, LiftZ + .45f), YardKit.Iron, Solid.None);
            }
            YardKit.Cylinder(s, "Axle", new Vector3(0f, LiftY, LiftZ), Vector3.right, .5f, 6f, YardKit.Iron, Solid.None);

            // The falling water over the cliff, behind the wheel.
            var fall = YardKit.Picture(s, "Waterfall", new Vector3(-5f, water, CliffZ - .14f), new Vector3(5f, Top - .3f, CliffZ - .1f), YardKit.Waterfall);
            fall.AddComponent<WaterfallFlow>();
            YardKit.Picture(s, "Foam", new Vector3(-5.2f, water, CliffZ - 1.4f), new Vector3(5.2f, water + .06f, CliffZ - .1f), Arena.Glow(new Color(1f, 1f, 1f, .45f)));

            // The landing the wheel lets its riders off onto, in front of the platform's edge.
            var landing = YardKit.Span(s, "Landing", new Vector3(-3.5f, Top - .3f, 148.4f), new Vector3(3.5f, Top, CliffZ), YardKit.WoodLight);
            landing.AddComponent<CourseFloor>();
            YardKit.Picture(s, "Landing trim", new Vector3(-3.55f, Top - .32f, 148.35f), new Vector3(3.55f, Top - .2f, 148.45f), YardKit.Gold);

            // The middle way's whole width (between the dividers), so nobody reaches the pool without it.
            Checkpoint(s, 3, new Vector3(0f, 0f, 137f), 26f);
            Sign(s, "물레방아에 매달리기: 우클릭을 누른 채로", new Vector3(0f, 2.8f, 136f), 1.1f);
        }

        // ------------------------------------------------------------------ 4. the platform and the final gate (own frame)

        void Platform()
        {
            var s = Group("4 Final gate", later);
            // The platform: either side of the ramps' tops, and the middle behind the cliff's marble face.
            Block(s, "Platform W", -26f, -18f, CliffZ, 176f, -1f);
            Block(s, "Platform E", 18f, 26f, CliffZ, 176f, -1f);
            Block(s, "Platform middle", -18f, 18f, CliffZ + .6f, 176f, -3f);
            var cliff = YardKit.Span(s, "Cliff (marble)", new Vector3(-18f, -3f, CliffZ), new Vector3(18f, Top, CliffZ + .6f), YardKit.Marble, Solid.NoClimb);
            cliff.AddComponent<CourseFloor>();
            YardKit.Wall(s, "Edge railing W", -18f, -3.5f, Top, Top + 1f, CliffZ, CliffZ + .4f);
            YardKit.Wall(s, "Edge railing E", 3.5f, 18f, Top, Top + 1f, CliffZ, CliffZ + .4f);
            Checkpoint(s, 4, new Vector3(0f, Top, 157f), 48f);

            // The final gate in its wall, towers either side; the leaves open away from the platform.
            const float z = 173f, h = 7.2f;
            YardKit.Wall(s, "Gate wall W", -26f, -4f, Top, Top + 11f, z - 1f, z + 1f, YardKit.WallStone);
            YardKit.Wall(s, "Gate wall E", 4f, 26f, Top, Top + 11f, z - 1f, z + 1f, YardKit.WallStone);
            YardKit.Wall(s, "Gate lintel", -4f, 4f, Top + h + .1f, Top + 11f, z - 1f, z + 1f, YardKit.WallStone);
            YardKit.Picture(s, "Gate arch trim", new Vector3(-4.3f, Top + h, z - 1.08f), new Vector3(4.3f, Top + h + .3f, z - 1f), YardKit.Gold);
            foreach (float x in new[] { -6.6f, 6.6f })
                Tower(s, new Vector3(x, Top, z), 3.6f, 14f);
            Place<PushGate>(s, "Final gate", new Vector3(0f, Top, z)).Configure(8f, h, gates.finalPushSeconds, gates.finalCrowdBonus);

            // The finish court behind it.
            Block(s, "Finish court", -10f, 10f, 176f, 195f, -3f);
            YardKit.Wall(s, "Finish wall W", -11f, -10f, Top, Top + 5f, z + 1f, 196f, YardKit.WallStone);
            YardKit.Wall(s, "Finish wall E", 10f, 11f, Top, Top + 5f, z + 1f, 196f, YardKit.WallStone);
            YardKit.Wall(s, "Finish wall N", -11f, 11f, Top, Top + 5f, 195f, 196f, YardKit.WallStone);
            for (int i = 0; i < 20; i++)
                YardKit.Picture(s, "Finish line", new Vector3(-10f + i, Top, 184f), new Vector3(-9f + i, Top + .02f, 185f), i % 2 == 0 ? YardKit.Gold : YardKit.Iron);
            Trigger<FinishZone>(s, "Finish", new Vector3(-10f, Top, 185f), new Vector3(10f, Top + 3f, 195f)).Configure(true);
            YardKit.Span(s, "Crown pedestal", new Vector3(-1.2f, Top, 191f), new Vector3(1.2f, Top + 1.4f, 193.4f), YardKit.Marble, Solid.NoClimb);
            YardKit.Cylinder(s, "Crown", new Vector3(0f, Top + 1.9f, 192.2f), Vector3.up, 1.3f, .9f, YardKit.Gold, Solid.None);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                YardKit.Ball(s, "Crown point", new Vector3(Mathf.Sin(a) * .6f, Top + 2.55f, 192.2f + Mathf.Cos(a) * .6f), .26f, YardKit.Gold, Solid.None);
            }
            Sign(s, "4. 최종 성문: 우클릭을 누른 채로 밀기", new Vector3(0f, Top + 2.6f, 162f), 1.3f);
            Sign(s, "결승", new Vector3(0f, Top + 4.5f, 186f), 2.2f);
        }

        // A solid block whose top is the platform (y 8), from `bottom` up.
        static void Block(Transform s, string name, float x0, float x1, float z0, float z1, float bottom)
        {
            YardKit.Span(s, name, new Vector3(x0, bottom, z0), new Vector3(x1, Top, z1), YardKit.Stone).AddComponent<CourseFloor>();
        }
    }
}
