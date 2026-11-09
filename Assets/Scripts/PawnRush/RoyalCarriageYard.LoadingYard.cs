using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // The centre way of the arena (R532, the user's reference picture of a royal carriage yard with cranes, crates and
    // blue-covered wagons; was the carousel): the loading yard, the straight way to the waterwheel and the gate beyond it,
    // harder than the cargo ramps either side. In its own frame (sections 3-4), going north:
    //
    //   z 112.8 .. 115.2   a wall of crates across the way with one 7 m gate in it (the ramps' way goes round it)
    //   z 115.6 .. 123.8   two stables either side: from one on each line a royal carriage bolts across the way to the
    //                      other side's stable front and backs home (lines z 117.5 and 121.9, half a cycle apart)
    //   z 124.4 .. 129     a horse bolts from its stall on the east diagonally across the way to the west wall, turns
    //                      round, rears and gallops back the same way (a run every ~5 s)
    //   z 131.4 .. 136.2   two cranes on the walls drop crates anywhere across the way, three hooks each (lines z 132.1
    //                      and 135.5; the hooks' splits staggered, so no line across is spared by both)
    //   z 136.4 .. 139.85  the apron before the pool: CP3, then the lifting waterwheel as before
    //
    // Every danger warns a second or more before it comes (red floor lines, red lamps, a rearing horse, a crate held still
    // over an orange disc) and runs on ObstacleClock, so every PC sees the same without a packet. Between two dangers
    // there is always a strip of floor nothing reaches. Nothing here throws anyone forward: every knock's part along the
    // way is cut to 60 % (YardImpact.course). Nothing pins anyone: every moving body keeps a pawn's width (0.6 m) off the
    // walls round where it stops or turns, and a crate lets whoever it knocked down through until it goes back up. The
    // numbers and the reasons: Docs/PawnRush/ROYAL_CARRIAGE_YARD.md §14.
    public sealed partial class RoyalCarriageYard
    {
        const float LoadGateZ0 = 112.8f, LoadGateZ1 = 115.2f, LoadGateHalf = 3.5f;
        // The stables: bays from |x| 7.2 (their fronts, the way between them 14.4 m) to the dividers at 13, 3.5 m wide (0.6 m
        // either side of a carriage); a carriage stands at home with its rear 1.6 m off the back wall.
        const float StableLaneA = 117.5f, StableLaneB = 121.9f, StableFront = 7.2f, StableHalf = 1.75f, StableHome = 9.3f;
        // The horse's stall on the east and the end of its run by the west wall: its half turns there clear the stall's
        // walls, lane B's west bay and the divider by 0.6 m or more.
        static readonly Vector3 HorseHome = new Vector3(10.6f, 0f, 128.65f), HorseEnd = new Vector3(-10.8f, 0f, 125.8f);
        const float StallZ0 = 126.5f, StallZ1 = 130.8f, StallFront = 8.4f;
        // The cranes: a crate face never comes within 0.6 m of a wall, the stall or the apron's crates.
        const float CraneAZ = 132.1f, CraneBZ = 135.5f, CraneReach = 11.7f;
        // CP3, on the apron past the second crane: 0.7 m past where the last crate can still knock anyone.
        const float LoadApronZ = 137.2f;

        void LoadingYard(Transform parent)
        {
            var s = Group("Loading yard", parent);
            var y = loadingYard;

            // The dividers between the middle way and the ramps (not to be climbed), stone with a cap and crenels.
            foreach (float side in new[] { -1f, 1f })
            {
                YardKit.Wall(s, "Divider", Mathf.Min(side * 13f, side * 18f), Mathf.Max(side * 13f, side * 18f), 0f, 2.2f, 116f, CliffZ, YardKit.WallStone);
                YardKit.Picture(s, "Divider cap", new Vector3(Mathf.Min(side * 12.95f, side * 18.05f), 2.2f, 115.95f),
                                new Vector3(Mathf.Max(side * 12.95f, side * 18.05f), 2.4f, CliffZ), YardKit.WallStone);
                for (float z = 116.5f; z < CliffZ - 1f; z += 2f)
                    YardKit.Picture(s, "Crenel", new Vector3(Mathf.Min(side * 13f, side * 13.7f), 2.4f, z),
                                    new Vector3(Mathf.Max(side * 13f, side * 13.7f), 3f, z + 1f), YardKit.WallStone);
                // Crates and barrels stored on the wall tops, out of everyone's reach (pictures).
                for (float z = 118f; z < CliffZ - 3f; z += 6.5f)
                {
                    float x = side * 16f;
                    YardKit.Picture(s, "Stored crate", new Vector3(x - .6f, 2.4f, z - .6f), new Vector3(x + .6f, 3.6f, z + .6f), YardKit.WoodLight);
                    YardKit.Cylinder(s, "Stored barrel", new Vector3(x, 2.95f, z + 1.5f), Vector3.up, .8f, 1.1f, YardKit.WoodDark, Solid.None);
                }
            }

            LoadingGate(s);

            // The stables. Lane A's carriage bolts east from the west stable, lane B's west from the east one; across
            // each line the other side's bay keeps a covered wagon. Half a cycle apart.
            var a = Stable(s, -1f, StableLaneA, true, 0);
            Stable(s, 1f, StableLaneA, false, 1);
            var b = Stable(s, 1f, StableLaneB, true, 2);
            Stable(s, -1f, StableLaneB, false, 3);
            if (a != null && b != null) b.SetPhase(a.CycleSeconds * .5f);

            HorseStall(s);

            // The cranes, three hooks each over thirds of the way, a third of a trip apart; crane B's splits fall where
            // crane A's hooks are thickest and the other way round, so no straight line has both cranes' thin spots, and
            // the boardwalk is in both cranes' middle hooks.
            float p = y.cranePeriod, r = CraneReach;
            Crane(s, CraneAZ, 5321, p, new[]
            {
                new CraneDrop.Hook(-r, -4.6f, 0f), new CraneDrop.Hook(-3.2f, 3.2f, p / 3f), new CraneDrop.Hook(4.6f, r, p * 2f / 3f),
            });
            Crane(s, CraneBZ, 5322, p, new[]
            {
                new CraneDrop.Hook(-r, -8.5f, p / 6f), new CraneDrop.Hook(-7.1f, 7.1f, p * .5f), new CraneDrop.Hook(8.5f, r, p * 5f / 6f),
            });

            // The way's straight line: wooden boards from the gate to the pool (a picture), and the apron's dressing.
            YardKit.Picture(s, "Boardwalk", new Vector3(-1.2f, 0f, LoadGateZ1), new Vector3(1.2f, .012f, PoolZ0 - .3f), YardKit.WoodLight);
            foreach (float side in new[] { -1f, 1f })
            {
                YardKit.Picture(s, "Boardwalk edge", new Vector3(Mathf.Min(side * 1.2f, side * 1.32f), 0f, LoadGateZ1),
                                new Vector3(Mathf.Max(side * 1.2f, side * 1.32f), .016f, PoolZ0 - .3f), YardKit.WoodDark);
                LampPost(s, new Vector3(side * 7.6f, 0f, PoolZ0 - 1.2f));
                Crates(s, new Vector3(side * 11.4f, 0f, 137.5f), 1);
            }
        }

        void Crane(Transform s, float z, int seed, float period, CraneDrop.Hook[] hooks)
        {
            var y = loadingYard;
            Place<CraneDrop>(s, "Crane", new Vector3(0f, 0f, z))
                .Configure(13.4f, 2.2f, 7.6f, period, y.dropHeight, y.crateSize, y.crateKnockback, seed, hooks, later);
        }

        // The wall of crates across the way, its 7 m gate, the lamps and the sign over it.
        void LoadingGate(Transform s)
        {
            const float end = 12.45f;
            foreach (float side in new[] { -1f, 1f })
            {
                float inner = side * LoadGateHalf, outer = side * end;
                // One solid box for the wall (2.2 m: out of a jump's reach, and the pawn's F step and a jump's 1.42 m),
                // as far as the last crate goes.
                var wall = YardKit.Span(s, "Crate wall (collider)", new Vector3(Mathf.Min(inner, outer), 0f, LoadGateZ0),
                                        new Vector3(Mathf.Max(inner, outer), 2.2f, LoadGateZ1), null, Solid.NoClimb);
                wall.GetComponent<MeshRenderer>().enabled = false;
                int n = 0;
                for (float x = LoadGateHalf + .75f; x < end; x += 1.5f, n++)
                {
                    float cx = side * x;
                    YardKit.Picture(s, "Crate", new Vector3(cx - .7f, 0f, LoadGateZ0 + .1f), new Vector3(cx + .7f, 1.4f, LoadGateZ0 + 1.5f), YardKit.WoodLight);
                    YardKit.Picture(s, "Crate", new Vector3(cx - .7f, 0f, LoadGateZ1 - 1.15f), new Vector3(cx + .7f, 1.2f, LoadGateZ1 - .05f), YardKit.WoodDark);
                    YardKit.Picture(s, "Crate band", new Vector3(cx - .72f, .6f, LoadGateZ0 + .08f), new Vector3(cx + .72f, .74f, LoadGateZ0 + 1.52f), YardKit.WoodDark);
                    if (n % 2 == 0)
                        YardKit.Picture(s, "Crate", new Vector3(cx - .55f, 1.4f, LoadGateZ0 + .25f), new Vector3(cx + .55f, 2.5f, LoadGateZ0 + 1.35f), YardKit.WoodLight);
                    else
                    {
                        // Every other stack under a royal cloth.
                        YardKit.Picture(s, "Cloth", new Vector3(cx - .75f, 1.4f, LoadGateZ0 + .05f), new Vector3(cx + .75f, 2.2f, LoadGateZ1 - .05f), YardKit.Banner);
                        YardKit.Picture(s, "Cloth stripe", new Vector3(cx - .1f, 1.4f, LoadGateZ0 + .03f), new Vector3(cx + .1f, 2.22f, LoadGateZ1 - .03f), YardKit.Gold);
                    }
                }
                // The gate's posts on the walls' inner ends.
                YardKit.Picture(s, "Gate post", new Vector3(Mathf.Min(inner, inner + side * .45f), 0f, LoadGateZ0 + .7f),
                                new Vector3(Mathf.Max(inner, inner + side * .45f), 4.8f, LoadGateZ0 + 1.15f), YardKit.WoodDark);
                LampPost(s, new Vector3(side * (LoadGateHalf + 1f), 0f, LoadGateZ0 - .6f));
            }
            YardKit.Picture(s, "Gate lintel", new Vector3(-LoadGateHalf - .5f, 4.5f, LoadGateZ0 + .6f), new Vector3(LoadGateHalf + .5f, 5.1f, LoadGateZ0 + 1.25f), YardKit.WoodDark);
            YardKit.Picture(s, "Gate trim", new Vector3(-LoadGateHalf - .5f, 4.62f, LoadGateZ0 + .56f), new Vector3(LoadGateHalf + .5f, 4.72f, LoadGateZ0 + .6f), YardKit.Gold);
            YardKit.Picture(s, "Gate banner", new Vector3(-.8f, 2.8f, LoadGateZ0 + .5f), new Vector3(.8f, 4.5f, LoadGateZ0 + .55f), YardKit.Banner);
            YardKit.Picture(s, "Gate banner stripe", new Vector3(-.12f, 2.8f, LoadGateZ0 + .47f), new Vector3(.12f, 4.5f, LoadGateZ0 + .5f), YardKit.Gold);
            Sign(s, "가운데: 마차 · 말 · 떨어지는 짐을 피해 곧장", new Vector3(0f, 5.7f, LoadGateZ0 + .5f), 1.2f);
        }

        // A stable bay on `side` (-1 west, 1 east) round line `z`: two walls, a blue roof and a lamp, open to the way, high
        // enough for a carriage's crown. With `charger` a royal carriage stands in it and bolts across the way to the
        // other side's bay front; without, a covered wagon is kept in it (solid, still).
        StableCharger Stable(Transform s, float side, float z, bool charger, int paint)
        {
            float front = side * StableFront, back = side * 13f;
            float x0 = Mathf.Min(front, back), x1 = Mathf.Max(front, back);
            foreach (float w in new[] { z - StableHalf - .15f, z + StableHalf })
                YardKit.Wall(s, "Stable wall", x0, x1, 0f, 3.3f, w, w + .15f, YardKit.WallStone);
            float eave = front - side * .2f;
            YardKit.Picture(s, "Stable roof", new Vector3(Mathf.Min(eave, back), 3.3f, z - StableHalf - .35f),
                            new Vector3(Mathf.Max(eave, back), 3.55f, z + StableHalf + .35f), YardKit.Roof);
            YardKit.Picture(s, "Stable beam", new Vector3(Mathf.Min(front, front + side * .3f), 3.05f, z - StableHalf - .15f),
                            new Vector3(Mathf.Max(front, front + side * .3f), 3.3f, z + StableHalf + .15f), YardKit.WoodDark);
            YardKit.Picture(s, "Stable trim", new Vector3(Mathf.Min(front, front - side * .02f), 3.1f, z - StableHalf - .15f),
                            new Vector3(Mathf.Max(front, front - side * .02f), 3.2f, z + StableHalf + .15f), YardKit.Gold);
            YardKit.Box(s, "Stable lamp", new Vector3(front - side * .1f, 3.75f, z), new Vector3(.3f, .4f, .3f), YardKit.Lamp, Solid.None);

            if (!charger)
            {
                CoveredWagon(s, new Vector3(side * 9.9f, 0f, z), side < 0f ? 90f : -90f);
                return null;
            }
            var y = loadingYard;
            // Facing across the way, its nose at its bay's front; it bolts until its nose is at the other side's bay front
            // (14.4 m), so the whole width between the bays is swept.
            var c = Place<StableCharger>(s, "Stable carriage", new Vector3(side * StableHome, 0f, z), side < 0f ? 90f : -90f);
            c.Configure(StableCharger.Look.Carriage, new Vector3(0f, 0f, 2f * StableFront), y.carriageSpeed, y.carriageReturnSpeed, y.carriageWait, .5f,
                        y.warning, y.knockbackForce, 1f, paint % 2 == 0 ? 2 : 0);
            return c;
        }

        // The horse's stall on the east, and the horse: it bolts from it diagonally across the way to the west wall.
        void HorseStall(Transform s)
        {
            float x0 = StallFront, x1 = 13f;
            // High enough for the horse rearing in it (its head comes up to about 3.5 m).
            YardKit.Wall(s, "Stall wall", x0, x1, 0f, 3.8f, StallZ0, StallZ0 + .15f, YardKit.WallStone);
            YardKit.Wall(s, "Stall wall", x0, x1, 0f, 3.8f, StallZ1 - .15f, StallZ1, YardKit.WallStone);
            YardKit.Picture(s, "Stall roof", new Vector3(x0 - .2f, 3.8f, StallZ0 - .2f), new Vector3(x1, 4.05f, StallZ1 + .2f), YardKit.Roof);
            YardKit.Picture(s, "Stall beam", new Vector3(x0, 3.45f, StallZ0), new Vector3(x0 + .3f, 3.8f, StallZ1), YardKit.WoodDark);
            YardKit.Picture(s, "Hay", new Vector3(12f, 0f, StallZ1 - 1.1f), new Vector3(12.95f, .8f, StallZ1 - .2f), Arena.Lit("M_YardStraw", Arena.Hex("#D9B65E"), 0f, .9f));
            YardKit.Picture(s, "Trough", new Vector3(12.3f, 0f, StallZ0 + .3f), new Vector3(12.95f, .55f, StallZ0 + 1.5f), YardKit.WoodDark);
            YardKit.Box(s, "Stall lamp", new Vector3(x0 + .1f, 4.2f, (StallZ0 + StallZ1) * .5f), new Vector3(.3f, .4f, .3f), YardKit.Lamp, Solid.None);

            var y = loadingYard;
            var run = HorseEnd - HorseHome;
            float yaw = Mathf.Atan2(run.x, run.z) * Mathf.Rad2Deg;
            var horse = Place<StableCharger>(s, "Bolting horse", HorseHome, yaw);
            horse.Configure(StableCharger.Look.Horse, new Vector3(0f, 0f, run.magnitude), y.horseSpeed, y.horseReturnSpeed, y.horseWait, y.horseWait,
                            y.warning, y.knockbackForce, 1f, 2, 1.6f, y.horseBothWays);
            horse.SetPhase(2f);
        }

        // A wagon under a royal blue cloth, its length along local z: pictures and one solid box (never climbed).
        static void CoveredWagon(Transform s, Vector3 at, float yaw)
        {
            var w = new GameObject("Covered wagon").transform;
            w.SetParent(s, false);
            w.localPosition = at;
            w.localRotation = Quaternion.Euler(0f, yaw, 0f);
            YardKit.Box(w, "Chassis", new Vector3(0f, .62f, 0f), new Vector3(1.4f, .2f, 3.3f), YardKit.WoodDark, Solid.None);
            YardKit.Box(w, "Bed", new Vector3(0f, .98f, 0f), new Vector3(1.8f, .55f, 3.5f), YardKit.WoodLight, Solid.None);
            YardKit.Box(w, "Cloth", new Vector3(0f, 1.65f, -.1f), new Vector3(1.84f, .85f, 3.2f), YardKit.Banner, Solid.None);
            YardKit.Cylinder(w, "Cloth top", new Vector3(0f, 2.05f, -.1f), Vector3.forward, 1.84f, 3.2f, YardKit.Banner, Solid.None);
            YardKit.Box(w, "Cloth stripe", new Vector3(0f, 1.95f, -.1f), new Vector3(1.9f, .14f, .3f), YardKit.Gold, Solid.None);
            YardKit.Box(w, "Cloth stripe", new Vector3(0f, 2.05f, -.1f), new Vector3(.3f, .14f, 3.24f), YardKit.Gold, Solid.None);
            YardKit.Box(w, "Shaft", new Vector3(0f, .6f, 2.05f), new Vector3(.12f, .1f, .8f), YardKit.WoodDark, Solid.None);
            foreach (float z in new[] { -1.15f, 1.15f })
                foreach (float x in new[] { -.98f, .98f })
                {
                    YardKit.Cylinder(w, "Wheel", new Vector3(x, .5f, z), Vector3.right, 1f, .12f, YardKit.WoodDark, Solid.None);
                    YardKit.Cylinder(w, "Hub", new Vector3(x * 1.06f, .5f, z), Vector3.right, .26f, .05f, YardKit.Gold, Solid.None);
                }
            YardKit.Box(w, "Wagon (collider)", new Vector3(0f, 1.3f, 0f), new Vector3(2f, 2.6f, 3.6f), null, Solid.NoClimb)
                   .GetComponent<MeshRenderer>().enabled = false;
        }
    }
}
