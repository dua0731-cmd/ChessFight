using ChessFight.Gameplay;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // The four sections of the Royal Carriage Yard, in map coordinates (metres, the start court's north edge at z 0,
    // the ground at y 0). The numbers and why they are what they are: Docs/KingRush/ROYAL_CARRIAGE_YARD.md.
    public sealed partial class RoyalCarriageYard
    {
        // The arena's carousel and the lifting waterwheel, where several parts are fitted round each other.
        const float CarouselZ = 125.5f, PoolZ0 = 139.3f, LiftZ = 144f, LiftY = 4.6f, LiftRadius = 5.2f, CliffZ = 150f, Top = 8f;

        // ------------------------------------------------------------------ 1. the outer gate and the yard

        void Entrance()
        {
            var s = Group("1 Entrance");
            // The start court behind the gate, its twelve start places (white in front: the map test starts there).
            YardKit.Floor(s, "Start court", -12f, 12f, -16f, 0f, 0f);
            YardKit.Wall(s, "Court wall W", -13f, -12f, 0f, 5f, -17f, 2f, YardKit.WallStone);
            YardKit.Wall(s, "Court wall E", 12f, 13f, 0f, 5f, -17f, 2f, YardKit.WallStone);
            YardKit.Wall(s, "Court wall S", -13f, 13f, 0f, 5f, -17f, -16f, YardKit.WallStone);
            for (int i = 0; i < 6; i++)
            {
                Place<SpawnPoint>(s, "Start white " + i, new Vector3(-5f + 2f * i, 0f, -7f)).Configure(i, Teams.White);
                Place<SpawnPoint>(s, "Start black " + i, new Vector3(-5f + 2f * i, 0f, -10f)).Configure(i, Teams.Black);
            }

            // The outer gate: a wall with a 6 m opening, two towers, two leaves that open when the race starts.
            YardKit.Wall(s, "Gate wall W", -13f, -3f, 0f, 7f, 0f, 2f, YardKit.WallStone);
            YardKit.Wall(s, "Gate wall E", 3f, 13f, 0f, 7f, 0f, 2f, YardKit.WallStone);
            YardKit.Wall(s, "Gate lintel", -3f, 3f, 5.6f, 7f, 0f, 2f, YardKit.WallStone);
            YardKit.Picture(s, "Gate lintel trim", new Vector3(-3.2f, 5.5f, -.08f), new Vector3(3.2f, 5.7f, .02f), YardKit.Gold);
            foreach (float x in new[] { -4.8f, 4.8f })
                Tower(s, new Vector3(x, 0f, 1f), 3f, 10f);
            var count = YardKit.Label(s, "", new Vector3(0f, 6.3f, -.3f), 3f, new Color(1f, .86f, .4f));
            Place<TimedGate>(s, "Start gate W", new Vector3(-3f, 0f, 1f)).Configure(3f, 5.4f, false, gates.startDelay, gates.startDuration, count);
            Place<TimedGate>(s, "Start gate E", new Vector3(3f, 0f, 1f)).Configure(3f, 5.4f, true, gates.startDelay, gates.startDuration);
            Sign(s, "왕실 마차뜰", new Vector3(0f, 8.2f, -.3f), 2.2f);

            // The narrow corridor: twelve players fit, but only just, and bunch up.
            YardKit.Floor(s, "Corridor", -3f, 3f, 2f, 22f, 0f);
            YardKit.Wall(s, "Corridor wall W", -4f, -3f, 0f, 4f, 2f, 22f);
            YardKit.Wall(s, "Corridor wall E", 3f, 4f, 0f, 4f, 2f, 22f);

            // The wide yard: room to spread out. Nothing moves here.
            YardKit.Floor(s, "Yard", -22f, 22f, 22f, 52f, 0f);
            YardKit.Wall(s, "Yard wall SW", -23f, -3f, 0f, 5f, 21f, 22f, YardKit.WallStone);
            YardKit.Wall(s, "Yard wall SE", 3f, 23f, 0f, 5f, 21f, 22f, YardKit.WallStone);
            YardKit.Wall(s, "Yard wall W", -23f, -22f, 0f, 5f, 21f, 52f, YardKit.WallStone);
            YardKit.Wall(s, "Yard wall E", 22f, 23f, 0f, 5f, 21f, 52f, YardKit.WallStone);
            // Parked carriages and crates along the walls: solid, out of the way.
            ParkedCarriage(s, new Vector3(-18.5f, 0f, 44f), 0f, YardKit.CarriageBlue);
            ParkedCarriage(s, new Vector3(18.5f, 0f, 29f), 180f, YardKit.CarriageRed);
            Crates(s, new Vector3(-19.5f, 0f, 27f), 3);
            Crates(s, new Vector3(19.5f, 0f, 41f), 4);
            Crates(s, new Vector3(-19.5f, 0f, 33f), 2);
            Sign(s, "1. 마차뜰 진입", new Vector3(0f, 3.4f, 30f));
        }

        // ------------------------------------------------------------------ 2. the canal and the crossing wheel

        void Canal()
        {
            var s = Group("2 Canal");
            const float water = -1.2f;
            YardKit.Floor(s, "Canal bed", -22f, 22f, 52f, 86f, -4f, YardKit.WallStone);
            YardKit.Span(s, "Bank S", new Vector3(-22f, -4f, 51f), new Vector3(22f, -1f, 52f), YardKit.WallStone);
            YardKit.Span(s, "Bank N", new Vector3(-22f, -4f, 86f), new Vector3(22f, -1f, 87f), YardKit.WallStone);
            YardKit.Wall(s, "Canal wall W", -23f, -22f, -4f, 5f, 52f, 86f, YardKit.WallStone);
            YardKit.Wall(s, "Canal wall E", 22f, 23f, -4f, 5f, 52f, 86f, YardKit.WallStone);
            YardKit.Picture(s, "Water", new Vector3(-22f, -4f, 52f), new Vector3(22f, water, 86f), YardKit.Water);
            // A fall into the canal: in the water, a moment, back at the last checkpoint.
            Trigger<KillVolume>(s, "Canal (fall)", new Vector3(-22f, -4f, 52f), new Vector3(22f, water - .3f, 86f));

            // The entry bridge, the catwalk through the wheel, the far bridge.
            Bridge(s, "Entry bridge", 52f, 62f);
            Bridge(s, "Far bridge", 76f, 86f);
            float half = crossing.passageWidth * .5f;
            var walk = YardKit.Span(s, "Catwalk", new Vector3(-half, -.35f, 62f), new Vector3(half, 0f, 76f), YardKit.WoodLight);
            walk.AddComponent<CourseFloor>();
            for (float z = 62.5f; z < 76f; z += 1f)
                YardKit.Picture(s, "Catwalk plank", new Vector3(-half + .03f, 0f, z - .45f), new Vector3(half - .03f, .03f, z + .45f), YardKit.WoodDark);
            foreach (float z in new[] { 63f, 66f, 72f, 75f })
                foreach (float x in new[] { -half + .2f, half - .2f })
                    YardKit.Cylinder(s, "Catwalk post", new Vector3(x, (-4f - .35f) * .5f, z), Vector3.up, .25f, 3.65f, YardKit.WoodDark, Solid.None);

            // The wheel and the frame it hangs from (posts in the water, beams over the top, hangers down to the axle).
            const float z0 = 69f;
            float centre = crossing.wheelRadius + crossing.tipClearance;
            float beam = centre + crossing.wheelRadius + .8f;
            Place<HorizontalWaterwheel>(s, "Crossing waterwheel", new Vector3(0f, centre, z0))
                .Configure(crossing.rotationSpeed, crossing.clockwise, crossing.wheelRadius, crossing.bladeCount, crossing.passageWidth,
                           crossing.tipClearance, crossing.knockbackForce);
            float post = crossing.wheelRadius + 1f;
            foreach (float z in new[] { z0 - 2.3f, z0 + 2.3f })
            {
                foreach (float x in new[] { -post, post })
                    YardKit.Span(s, "Frame post", new Vector3(x - .3f, -4f, z - .3f), new Vector3(x + .3f, beam + .6f, z + .3f), YardKit.WoodDark, Solid.NoClimb);
                YardKit.Span(s, "Frame beam", new Vector3(-post - .3f, beam, z - .3f), new Vector3(post + .3f, beam + .6f, z + .3f), YardKit.WoodDark, Solid.NoClimb);
                YardKit.Picture(s, "Hanger", new Vector3(-.22f, centre, z - .22f), new Vector3(.22f, beam, z + .22f), YardKit.Iron);
            }
            YardKit.Cylinder(s, "Axle", new Vector3(0f, centre, z0), Vector3.forward, .5f, 4.9f, YardKit.Iron, Solid.None);

            Checkpoint(s, 1, new Vector3(0f, 0f, 54f), 9f);
            Checkpoint(s, 2, new Vector3(0f, 0f, 81.5f), 9f);
            Sign(s, "2. 가로 물레방아 — 날개 사이로 지나가기", new Vector3(0f, 2.6f, 57f), 1.3f);
        }

        // A stone bridge over the canal, z0..z1, 10 m wide, with railings and two piers.
        void Bridge(Transform s, string name, float z0, float z1)
        {
            YardKit.Floor(s, name, -5f, 5f, z0, z1, 0f);
            YardKit.Wall(s, name + " railing W", -5f, -4.6f, 0f, .9f, z0, z1);
            YardKit.Wall(s, name + " railing E", 4.6f, 5f, 0f, .9f, z0, z1);
            float mid = (z0 + z1) * .5f;
            foreach (float x in new[] { -3.6f, 3.6f })
                YardKit.Span(s, name + " pier", new Vector3(x - .7f, -4f, mid - .7f), new Vector3(x + .7f, -1f, mid + .7f), YardKit.WallStone);
        }

        // ------------------------------------------------------------------ 3. the carriage arena, the ramps, the waterfall

        void CarriageArena()
        {
            var s = Group("3 Arena");
            // Floors: the whole width up to the pool, then either side of it.
            YardKit.Floor(s, "Arena", -26f, 26f, 86f, PoolZ0, 0f);
            YardKit.Floor(s, "Arena W of pool", -26f, -6f, PoolZ0, CliffZ, 0f);
            YardKit.Floor(s, "Arena E of pool", 6f, 26f, PoolZ0, CliffZ, 0f);
            YardKit.Wall(s, "Arena wall SW", -27f, -22f, 0f, 6f, 85f, 86f, YardKit.WallStone);
            YardKit.Wall(s, "Arena wall SE", 22f, 27f, 0f, 6f, 85f, 86f, YardKit.WallStone);
            YardKit.Wall(s, "Side wall W", -27f, -26f, 0f, Top + 3f, 85f, 176f, YardKit.WallStone);
            YardKit.Wall(s, "Side wall E", 26f, 27f, 0f, Top + 3f, 85f, 176f, YardKit.WallStone);

            ChargingCarriages(s);
            Carousel(s);
            CargoRamp(s, -1f);
            CargoRamp(s, 1f);
            Waterfall(s);
            Sign(s, "3. 마차 충돌 경기장", new Vector3(0f, 3.6f, 90f));
        }

        void ChargingCarriages(Transform parent)
        {
            var s = Group("Charging carriages", parent);
            // Two rows charging from the walls to the middle and from the middle to the walls (opposite ways), then one
            // carriage across the whole width; every one waits and charges on its own beat.
            Cart(s, "Carriage A1", new Vector3(-22f, 0f, 96f), 90f, 18f, carts.moveSpeed, 0f, 0);
            Cart(s, "Carriage A2", new Vector3(22f, 0f, 96f), -90f, 18f, carts.moveSpeed, 1.3f, 1);
            Cart(s, "Carriage B1", new Vector3(-3f, 0f, 103f), -90f, 19f, carts.moveSpeed, 2.1f, 2);
            Cart(s, "Carriage B2", new Vector3(3f, 0f, 103f), 90f, 19f, carts.moveSpeed, .6f, 0);
            var sweeper = Cart(s, "Carriage C (full width)", new Vector3(-21f, 0f, 110f), 90f, 42f, carts.sweeperSpeed, 3f, 1);
            sweeper.Configure(new Vector3(0f, 0f, 42f), carts.sweeperSpeed, carts.returnSpeed + 1f, carts.waitAtStart + .7f, carts.returnDelay + .2f,
                              carts.knockbackForce, carts.knockdownSeconds, 1);
            // Lane marks on the floor under each row.
            foreach (float z in new[] { 96f, 103f, 110f })
                YardKit.Picture(s, "Lane mark", new Vector3(-25.5f, 0f, z - 1.35f), new Vector3(25.5f, .02f, z + 1.35f), YardKit.WoodDark);
        }

        ChargingCarriage Cart(Transform s, string name, Vector3 at, float yaw, float distance, float speed, float phase, int paint)
        {
            var cart = Place<ChargingCarriage>(s, name, at, yaw);
            cart.Configure(new Vector3(0f, 0f, distance), speed, carts.returnSpeed, carts.waitAtStart, carts.returnDelay,
                           carts.knockbackForce, carts.knockdownSeconds, paint);
            cart.SetPhase(phase);
            return cart;
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

            Checkpoint(s, 3, new Vector3(0f, 0f, 137f), 11f);
            Sign(s, "물레방아에 매달리기: 우클릭을 누른 채로", new Vector3(0f, 2.8f, 136f), 1.1f);
        }

        // ------------------------------------------------------------------ 4. the platform and the final gate

        void Platform()
        {
            var s = Group("4 Final gate");
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
