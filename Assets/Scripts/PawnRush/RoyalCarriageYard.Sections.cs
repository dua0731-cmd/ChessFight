using ChessFight.Gameplay;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // The four sections of the Royal Carriage Yard. Sections 1 and 2 are in map coordinates (metres, the start court's
    // north edge at z 0, the ground at y 0); 3 and 4 in their own frame (the 11-02 numbers), built under `later`, which
    // is moved LaterShift south. The numbers and why they are what they are: Docs/PawnRush/ROYAL_CARRIAGE_YARD.md.
    public sealed partial class RoyalCarriageYard
    {
        // 1 and 2, map coordinates.
        const float YardHalf = 22f, YardZ0 = 2f, YardZ1 = 50f;
        const float CanalZ0 = 50f, CanalZ1 = 82f, WalkZ0 = 58f, WalkZ1 = 74f, WheelZ = 66f, CanalWater = -1.2f;
        // The walkways' inner edges, either side of the giant wheel's axle.
        const float WalkInner = 3f;
        // The walkways' break at the wheel (the wheel itself is 1 m deep), CrossingSettings.gapLength's default. At the
        // pawn's real speeds (Settings/RagdollTuning: run 2.5, sprint 3.6 m/s; ~0.69 s in the air) a sprint jump reaches
        // ~3.1 m with the ledge help and ~3.5 m pressed late in the coyote time, so 2.6 and even 3.2 m were crossed
        // without a dive. At 3.4 m a sprint jump from the deck falls 0.3-0.6 m short, and it takes the user's
        // "점프+슬라이딩": a sprint jump from within 0.3 m of the edge with a dive (+3 m/s, limp, no ledge help) pressed
        // by ~0.1 s after the top of the jump, which lands the body 0.15-0.45 m past the far edge. Whoever waits at the
        // edge stands 1.1 m off the spokes' hit shell.
        public const float DefaultGap = 3.4f;
        // The canal's parapets beside the landings and the entry landing's side railings, over the body of a pawn
        // knocked while it jumps (hips up to ~1.6 m), so the yard never drops anyone into the canal: from the yard and the
        // landing's first metres, before CP1, that was back to the start. The north ones, off every knock's way, 1.5 m.
        const float ParapetTop = 2f, NorthParapetTop = 1.5f;
        // 3 and 4, their own frame: the arena starts at z 106 there, and is moved to start where the canal ends.
        const float ArenaStart = 106f, LaterShift = CanalZ1 - ArenaStart;
        // The lifting waterwheel and its pool, where several parts are fitted round each other.
        // The pool's near edge (R287, 0.55 m nearer the wheel than R284's 139.3): the paddle tips pass 0.7 m out at hip
        // height and the handles 0.3-0.6 m out at a jump's reach, while a pawn standing at the edge stays clear of them.
        const float PoolZ0 = 139.85f, LiftZ = 144f, LiftY = 4.6f, LiftRadius = 5.2f, CliffZ = 150f, Top = 8f;
        // The final gate's wall, 13 m past the cliff (R287: it was 23, an empty run once the gate was reached).
        const float GateZ = 163f;

        // ------------------------------------------------------------------ 1. the outer gate and the yard

        void Entrance()
        {
            var s = Group("1 Entrance");
            // The start court behind the gate and its twelve start places: the teams side by side, white west and
            // black east of the gate's middle, three across and two deep each, so neither is nearer the gate (R287:
            // white stood all in front, 3 m ahead of black). Its floor runs on under the gate to the yard: the opening
            // has no floor of its own.
            YardKit.Floor(s, "Start court", -12f, 12f, -16f, YardZ0, 0f);
            YardKit.Wall(s, "Court wall W", -13f, -12f, 0f, 5f, -17f, 0f, YardKit.WallStone);
            YardKit.Wall(s, "Court wall E", 12f, 13f, 0f, 5f, -17f, 0f, YardKit.WallStone);
            YardKit.Wall(s, "Court wall S", -13f, 13f, 0f, 5f, -17f, -16f, YardKit.WallStone);
            // As a section the round puts its own start places here (R508: two sets would be one too many).
            for (int i = 0; i < 6 && !asSection; i++)
            {
                float x = 1.5f * (1 + i % 3), z = i < 3 ? -7f : -10f;
                Place<SpawnPoint>(s, "Start white " + i, new Vector3(-x, 0f, z)).Configure(i, Teams.White);
                Place<SpawnPoint>(s, "Start black " + i, new Vector3(x, 0f, z)).Configure(i, Teams.Black);
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
            startWest = Place<TimedGate>(s, "Start gate W", new Vector3(-3f, 0f, 1f));
            startWest.Configure(3f, 5.4f, false, gates.startDelay, gates.startDuration, count);
            startEast = Place<TimedGate>(s, "Start gate E", new Vector3(3f, 0f, 1f));
            startEast.Configure(3f, 5.4f, true, gates.startDelay, gates.startDuration);
            Sign(s, "왕실 마차뜰", new Vector3(0f, 8.2f, -.3f), 2.2f);

            // The yard, right behind the gate: carriages drawn by horses charge about it at random between the storage
            // sheds along both walls (R659).
            YardKit.Floor(s, "Yard", -YardHalf, YardHalf, YardZ0, YardZ1, 0f);
            YardKit.Wall(s, "Yard wall W", -YardHalf - 1f, -YardHalf, 0f, 5f, YardZ0, YardZ1, YardKit.WallStone);
            YardKit.Wall(s, "Yard wall E", YardHalf, YardHalf + 1f, 0f, 5f, YardZ0, YardZ1, YardKit.WallStone);
            Granaries(s);
            RoamingCarriages(s);
            // In front of the closed gate, where the start court looks.
            Sign(s, "1. 짐마차 마당: 돌진하는 마차와 말 피하기", new Vector3(0f, 4.3f, -.7f), 1.1f);
        }

        // The horse-drawn carriages share the whole yard between the sheds (CarriageFleet): each keeps charging to a
        // random point anywhere in it, planned apart from the others in time (R659, RoamingFleet.Rules.Timed), so they
        // never meet and there are no lanes or seams to hide in. Every body stays 0.8 m past its reach inside the sheds'
        // fronts and the canal's parapet, wider than a pawn, so nobody is pinned (R287; 2.5 m more clear of the gate's
        // open leaves): the area is for the biggest body, a loose horse comes as much nearer the walls as it reaches
        // less. They start moving when the gate opens.
        void RoamingCarriages(Transform parent)
        {
            float keep = RoamingCarriage.Reach(roaming.teams > 0 ? RoamingCarriage.Look.Team : RoamingCarriage.Look.Horse) + .8f;
            carriages = Place<CarriageFleet>(parent, "Roaming teams and horses", Vector3.zero);
            carriages.Configure(-(ShedFront - keep), ShedFront - keep, YardZ0 + keep + 2.5f, YardZ1 - keep, 0, roaming.moveSpeed, roaming.turnSeconds,
                                roaming.minAim, roaming.maxAim, gates.startDelay, roaming.seed, roaming.knockbackForce, roaming.knockdownSeconds);
            carriages.Compose(roaming.teams, roaming.teamTurnSeconds, roaming.horses, roaming.horseSpeed, roaming.horseTurnSeconds, roaming.timed, roaming.tries,
                              .3f, .9f, 10f, 30f, roaming.shortCharge);
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
            // Parapets on the canal's edges beside the landings, over a knocked pawn's hips: nobody drops in from the
            // yard or the arena (from the yard, with no checkpoint yet, that was back to the start).
            foreach (float side in new[] { -1f, 1f })
            {
                float a = side * 10f, b = side * YardHalf;
                YardKit.Wall(s, "Parapet S", Mathf.Min(a, b), Mathf.Max(a, b), bed, ParapetTop, CanalZ0, CanalZ0 + .4f, YardKit.WallStone);
                YardKit.Wall(s, "Parapet N", Mathf.Min(a, b), Mathf.Max(a, b), bed, NorthParapetTop, CanalZ1 - .4f, CanalZ1, YardKit.WallStone);
            }

            // The landings either side, railed but where the walkways meet them; the entry landing's sides as high as
            // the parapets they meet.
            Landing(s, "Entry landing", CanalZ0, WalkZ0, true, inner, outer, bed, ParapetTop);
            Landing(s, "Far landing", WalkZ1, CanalZ1, false, inner, outer, bed, .9f);

            // The two walkways, wood, either side of the wheel's axle, broken where the wheel turns: the gap is jumped
            // through a hole between two spokes.
            float gap = Mathf.Clamp(crossing.gapLength, 2.2f, 3.6f) * .5f;
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
            var far = Checkpoint(s, 2, new Vector3(0f, 0f, CanalZ1 - 3.5f), 19.2f);
            // CP2 is reached as soon as a pawn is across the wheel, a metre on along either far walkway, but it comes
            // back on the far landing: whoever is shoved off there after crossing does not cross again (R287: a pawn
            // past CP2 could come back and shove crossers into the canal at no cost, the crossers paying a whole new
            // crossing). A metre on, so a jump that falls short does not count; from 0.3 m over the deck, so a diver still
            // sliding on its front does.
            foreach (float side in new[] { -1f, 1f })
                Reach(far, new Vector3(side < 0f ? -outer : inner, .3f, WheelZ + gap + 1f), new Vector3(side < 0f ? -inner : outer, 3f, WalkZ1));
            Sign(s, "2. 거대 물레방아: 바퀴살 사이 구멍으로 점프 + 다이빙", new Vector3(0f, 2.6f, CanalZ0 + 5.5f), 1.2f);
        }

        // More of a checkpoint's trigger, min..max in its parent's frame: passing there counts as passing the
        // checkpoint, and the respawn stays on the checkpoint's own floor. Checkpoint.RespawnPosition and its spots
        // belong to its first box (the one CourseBuilder made); every trigger box on its object raises Reached.
        static void Reach(CourseCheckpoint checkpoint, Vector3 min, Vector3 max)
        {
            var t = checkpoint.transform;
            var box = checkpoint.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = t.InverseTransformPoint(t.parent.TransformPoint((min + max) * .5f));
            box.size = YardKit.Abs(max - min);
        }

        // A stone landing over the canal, z0..z1, 20 m wide: railed on its sides (`sideTop` high) and on its canal edge
        // (the north edge when `walkwaysNorth`, else the south) but where the two walkways meet it.
        void Landing(Transform s, string name, float z0, float z1, bool walkwaysNorth, float inner, float outer, float bed, float sideTop)
        {
            YardKit.Floor(s, name, -10f, 10f, z0, z1, 0f);
            YardKit.Wall(s, name + " railing W", -10f, -9.6f, 0f, sideTop, z0, z1);
            YardKit.Wall(s, name + " railing E", 9.6f, 10f, 0f, sideTop, z0, z1);
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

        // ------------------------------------------------------------------ 3. the loading and sorting yard, the lifts (own frame)

        void CarriageArena()
        {
            var s = Group("3 Loading and sorting yard", later);
            // The floor: the whole width up to the mill basin (R659: the basin runs wall to wall, no dead ends beside it).
            YardKit.Floor(s, "Arena", -SortHalf, SortHalf, ArenaStart, PoolZ0, 0f);
            YardKit.Wall(s, "Arena wall SW", -27f, -22f, 0f, 6f, ArenaStart - 1f, ArenaStart, YardKit.WallStone);
            YardKit.Wall(s, "Arena wall SE", 22f, 27f, 0f, 6f, ArenaStart - 1f, ArenaStart, YardKit.WallStone);
            YardKit.Wall(s, "Side wall W", -27f, -26f, 0f, Top + 3f, ArenaStart - 1f, GateZ + 3f, YardKit.WallStone);
            YardKit.Wall(s, "Side wall E", 26f, 27f, 0f, Top + 3f, ArenaStart - 1f, GateZ + 3f, YardKit.WallStone);

            SortingYard(s);
            Lifts(s);
        }

        // The mill basin, the lifting waterwheel, the falling water behind it and the landing at its top: the wheel exactly as
        // before (R287, the one way up since R659 took the ramps away: the user's "물레방아는 중간에 있는 하나만"), in one
        // basin from wall to wall (a fall anywhere is back at CP3), its sides dressed as the yard's water stairs (BasinDock).
        void Lifts(Transform parent)
        {
            var s = Group("Waterfall and lifting wheel", parent);
            const float water = -.5f;
            YardKit.Floor(s, "Pool bed", -SortHalf, SortHalf, PoolZ0, CliffZ, -3f, YardKit.WallStone);
            YardKit.Span(s, "Pool side S", new Vector3(-SortHalf, -3f, PoolZ0 - 1f), new Vector3(SortHalf, -1f, PoolZ0), YardKit.WallStone);
            YardKit.Picture(s, "Pool water", new Vector3(-SortHalf, -3f, PoolZ0), new Vector3(SortHalf, water, CliffZ), YardKit.Water);
            Trigger<KillVolume>(s, "Pool (fall)", new Vector3(-SortHalf, -3f, PoolZ0), new Vector3(SortHalf, water - .3f, CliffZ));
            YardKit.Picture(s, "Landing edge trim", new Vector3(-SortHalf, -.01f, PoolZ0 - .25f), new Vector3(SortHalf, .02f, PoolZ0), YardKit.Gold);

            // The wheel on two piers in the basin. Its near side rises past the basin's edge.
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

            foreach (float side in new[] { -1f, 1f })
                BasinDock(s, side);

            // The whole yard's width, so nobody reaches the basin without it: on the apron 0.7 m past the second giant
            // chest's circle, its respawn spots in front of the wheel.
            Checkpoint(s, 3, new Vector3(0f, 0f, ApronZ), SortHalf * 2f);
            // The handles pass just out of a standing pawn's reach: it jumps for one.
            Sign(s, "물레방아에 점프해서 매달리기 (우클릭을 누른 채로)", new Vector3(0f, 2.8f, 138.2f), 1.1f);
        }

        // One side of the basin beside the wheel (R659, in place of the side wheels the user took out: "다른 장식을 생각해봐"),
        // the yard's water stairs: a sluice arch in the cliff with its gate raised and the water pouring out of it, a cargo
        // barge moored in front of it loaded with crates, barrels and a load under royal cloth, a jib crane on the platform
        // over it lowering a net of sacks, banners on the cliff. Pictures only, but the crane's post (on the platform, never
        // climbed): the barge's near side is 5 m and more out from the basin's edge, out of any jump's or dive's reach, so
        // nothing here is a way up or a place to be stuck.
        void BasinDock(Transform parent, float side)
        {
            var s = Group(side < 0f ? "Basin dock W" : "Basin dock E", parent);
            const float water = -.5f, x = 15.5f;
            float cx = side * x;
            // The sluice arch: two stone jambs, a lintel with a gold keystone, the dark tunnel, the gate raised in it.
            foreach (float j in new[] { -2.6f, 2.6f })
                YardKit.Picture(s, "Sluice jamb", new Vector3(cx + j - .45f, water, CliffZ - .45f), new Vector3(cx + j + .45f, 5.4f, CliffZ - .02f), YardKit.WallStone);
            YardKit.Picture(s, "Sluice lintel", new Vector3(cx - 3.1f, 4.6f, CliffZ - .5f), new Vector3(cx + 3.1f, 5.6f, CliffZ - .02f), YardKit.WallStone);
            YardKit.Picture(s, "Sluice keystone", new Vector3(cx - .35f, 4.7f, CliffZ - .55f), new Vector3(cx + .35f, 5.5f, CliffZ - .5f), YardKit.Gold);
            YardKit.Picture(s, "Sluice tunnel", new Vector3(cx - 2.15f, water, CliffZ - .04f), new Vector3(cx + 2.15f, 4.6f, CliffZ - .02f), YardKit.Glass);
            YardKit.Picture(s, "Sluice gate", new Vector3(cx - 2.1f, 3.4f, CliffZ - .3f), new Vector3(cx + 2.1f, 4.55f, CliffZ - .2f), YardKit.WoodDark);
            for (float gx = -1.8f; gx <= 1.81f; gx += .6f)
                YardKit.Picture(s, "Gate bar", new Vector3(cx + gx - .05f, 3.3f, CliffZ - .34f), new Vector3(cx + gx + .05f, 4.55f, CliffZ - .3f), YardKit.Iron);
            // The gate's winch: a gear wheel on the jamb.
            YardKit.Cylinder(s, "Sluice gear", new Vector3(cx + side * 3.4f, 3.2f, CliffZ - .3f), Vector3.forward, 1.6f, .14f, YardKit.WoodDark, Solid.None);
            YardKit.Cylinder(s, "Sluice gear hub", new Vector3(cx + side * 3.4f, 3.2f, CliffZ - .4f), Vector3.forward, .45f, .08f, YardKit.Gold, Solid.None);
            // The water out of the sluice, falling into the basin.
            var pour = YardKit.Picture(s, "Sluice water", new Vector3(cx - 2f, water, CliffZ - .6f), new Vector3(cx + 2f, 3.3f, CliffZ - .56f), YardKit.Waterfall);
            pour.AddComponent<WaterfallFlow>();
            YardKit.Picture(s, "Sluice foam", new Vector3(cx - 2.4f, water, CliffZ - 2.2f), new Vector3(cx + 2.4f, water + .06f, CliffZ - .5f), Arena.Glow(new Color(1f, 1f, 1f, .45f)));

            // The barge, its length along the cliff, moored 1.4 m off it.
            const float z0 = 145.2f, z1 = 148.4f, deck = .15f;
            float bx0 = cx - 4.6f, bx1 = cx + 4.6f, mid = (z0 + z1) * .5f;
            YardKit.Picture(s, "Barge hull", new Vector3(bx0 + .4f, water - .5f, z0), new Vector3(bx1 - .4f, deck, z1), YardKit.WoodDark);
            YardKit.Picture(s, "Barge bow", new Vector3(bx0, water - .2f, z0 + .4f), new Vector3(bx0 + .4f, deck, z1 - .4f), YardKit.WoodDark);
            YardKit.Picture(s, "Barge stern", new Vector3(bx1 - .4f, water - .2f, z0 + .4f), new Vector3(bx1, deck, z1 - .4f), YardKit.WoodDark);
            YardKit.Picture(s, "Barge rail", new Vector3(bx0 + .4f, deck, z0), new Vector3(bx1 - .4f, deck + .3f, z0 + .12f), YardKit.WoodLight);
            YardKit.Picture(s, "Barge rail", new Vector3(bx0 + .4f, deck, z1 - .12f), new Vector3(bx1 - .4f, deck + .3f, z1), YardKit.WoodLight);
            YardKit.Picture(s, "Barge trim", new Vector3(bx0 + .4f, deck + .3f, z0 - .02f), new Vector3(bx1 - .4f, deck + .36f, z0 + .02f), YardKit.Gold);
            // Its load: crates, barrels, a load under royal cloth.
            for (int i = 0; i < 3; i++)
            {
                float lx = cx - side * (2.6f - i * 1.5f);
                YardKit.Picture(s, "Barge crate", new Vector3(lx - .6f, deck, mid - .6f), new Vector3(lx + .6f, deck + 1.2f, mid + .6f), i == 1 ? YardKit.WoodDark : YardKit.WoodLight);
                YardKit.Picture(s, "Crate band", new Vector3(lx - .62f, deck + .5f, mid - .62f), new Vector3(lx + .62f, deck + .64f, mid + .62f), YardKit.Banner);
            }
            YardKit.Picture(s, "Barge crate", new Vector3(cx - side * 2.1f - .45f, deck + 1.2f, mid - .45f), new Vector3(cx - side * 2.1f + .45f, deck + 2.1f, mid + .45f), YardKit.WoodLight);
            foreach (float dz in new[] { -.8f, .8f })
                YardKit.Cylinder(s, "Barge barrel", new Vector3(cx + side * 1.9f, deck + .55f, mid + dz), Vector3.up, .8f, 1.1f, YardKit.WoodDark, Solid.None);
            YardKit.Picture(s, "Barge cloth", new Vector3(cx + side * 3.1f - .7f, deck, mid - .9f), new Vector3(cx + side * 3.1f + .7f, deck + 1.3f, mid + .9f), YardKit.Banner);
            YardKit.Picture(s, "Cloth stripe", new Vector3(cx + side * 3.1f - .12f, deck, mid - .92f), new Vector3(cx + side * 3.1f + .12f, deck + 1.32f, mid + .92f), YardKit.Gold);
            // Mooring ropes to rings on the cliff.
            foreach (float rx in new[] { bx0 + 1f, bx1 - 1f })
            {
                YardKit.Picture(s, "Mooring rope", new Vector3(rx - .04f, deck + .2f, z1), new Vector3(rx + .04f, deck + .28f, CliffZ - .05f), YardKit.WoodDark);
                YardKit.Cylinder(s, "Mooring ring", new Vector3(rx, deck + .25f, CliffZ - .06f), Vector3.forward, .3f, .05f, YardKit.Iron, Solid.None);
            }

            // The jib crane on the platform over the barge, a net of sacks hanging from it.
            float jx = cx + side * 2.2f;
            YardKit.Box(s, "Jib post", new Vector3(jx, Top + 2.4f, CliffZ + 1.4f), new Vector3(.5f, 4.8f, .5f), YardKit.WoodDark, Solid.NoClimb);
            YardKit.Box(s, "Jib arm", new Vector3(jx, Top + 4.6f, CliffZ - 1.6f), new Vector3(.36f, .36f, 6.4f), YardKit.WoodDark, Solid.None);
            YardKit.Box(s, "Jib brace", new Vector3(jx, Top + 3.4f, CliffZ - .1f), new Vector3(.22f, .22f, 3.4f), YardKit.WoodDark, Solid.None,
                        Quaternion.Euler(-40f, 0f, 0f));
            YardKit.Box(s, "Jib band", new Vector3(jx, Top + 4.6f, CliffZ - 4.6f), new Vector3(.4f, .4f, .2f), YardKit.Gold, Solid.None);
            YardKit.Picture(s, "Jib rope", new Vector3(jx - .04f, deck + 3.2f, CliffZ - 4.74f), new Vector3(jx + .04f, Top + 4.42f, CliffZ - 4.66f), YardKit.WoodDark);
            var net = YardKit.Ball(s, "Sack net", new Vector3(jx, deck + 2.7f, CliffZ - 4.7f), 1f, Arena.Lit("M_YardSack", Arena.Hex("#CDB48A"), 0f, .95f), Solid.None);
            net.transform.localScale = new Vector3(1.3f, 1.1f, 1.3f);
            // Banners on the cliff over the sluice.
            BannerOn(s, new Vector3(cx - 4.4f, 7.6f, CliffZ - .02f), 180f);
            BannerOn(s, new Vector3(cx + 4.4f, 7.6f, CliffZ - .02f), 180f);
        }

        // ------------------------------------------------------------------ 4. the platform and the final gate (own frame)

        void Platform()
        {
            var s = Group("4 Final gate", later);
            // The platform behind the cliff's marble face, on to just past the final gate's wall. Since R659 the marble runs
            // the whole width (the ramps' tops are gone): nothing to climb, the lifting wheel is the way up.
            Block(s, "Platform", -26f, 26f, CliffZ + .6f, GateZ + 3f, -3f);
            var cliff = YardKit.Span(s, "Cliff (marble)", new Vector3(-26f, -3f, CliffZ), new Vector3(26f, Top, CliffZ + .6f), YardKit.Marble, Solid.NoClimb);
            cliff.AddComponent<CourseFloor>();
            // The edge's railing but where the wheel's landing meets the platform.
            YardKit.Wall(s, "Edge railing W", -26f, -3.5f, Top, Top + 1f, CliffZ, CliffZ + .4f);
            YardKit.Wall(s, "Edge railing E", 3.5f, 26f, Top, Top + 1f, CliffZ, CliffZ + .4f);
            // Across the platform, and reached already on the lifting wheel's landing: a rider let off there and shoved
            // back into the pool comes back on the platform, not at the foot of the wheel (R287: 12-15 s, the map's
            // biggest loss, for nothing the shover paid).
            var top = Checkpoint(s, 4, new Vector3(0f, Top, 153f), 52f);
            Reach(top, new Vector3(-3.5f, Top + .3f, 148.4f), new Vector3(3.5f, Top + 3f, CliffZ));

            // The final gate in its wall, towers either side; the leaves open away from the platform.
            const float z = GateZ, h = 7.2f;
            YardKit.Wall(s, "Gate wall W", -26f, -4f, Top, Top + 11f, z - 1f, z + 1f, YardKit.WallStone);
            YardKit.Wall(s, "Gate wall E", 4f, 26f, Top, Top + 11f, z - 1f, z + 1f, YardKit.WallStone);
            YardKit.Wall(s, "Gate lintel", -4f, 4f, Top + h + .1f, Top + 11f, z - 1f, z + 1f, YardKit.WallStone);
            YardKit.Picture(s, "Gate arch trim", new Vector3(-4.3f, Top + h, z - 1.08f), new Vector3(4.3f, Top + h + .3f, z - 1f), YardKit.Gold);
            foreach (float x in new[] { -6.6f, 6.6f })
                Tower(s, new Vector3(x, Top, z), 3.6f, 14f);
            Place<PushGate>(s, "Final gate", new Vector3(0f, Top, z)).Configure(8f, h, gates.finalPushSeconds, gates.finalCrowdBonus, 1f);
            if (asSection)
            {
                Sign(s, "4. 최종 성문: 우클릭을 누른 채로 밀기", new Vector3(0f, Top + 2.6f, z - 11f), 1.3f);
                SectionEnd(s, z + 1f);
                return;
            }

            // The finish court behind it.
            Block(s, "Finish court", -10f, 10f, z + 3f, z + 22f, -3f);
            YardKit.Wall(s, "Finish wall W", -11f, -10f, Top, Top + 5f, z + 1f, z + 23f, YardKit.WallStone);
            YardKit.Wall(s, "Finish wall E", 10f, 11f, Top, Top + 5f, z + 1f, z + 23f, YardKit.WallStone);
            YardKit.Wall(s, "Finish wall N", -11f, 11f, Top, Top + 5f, z + 22f, z + 23f, YardKit.WallStone);
            for (int i = 0; i < 20; i++)
                YardKit.Picture(s, "Finish line", new Vector3(-10f + i, Top, z + 11f), new Vector3(-9f + i, Top + .02f, z + 12f), i % 2 == 0 ? YardKit.Gold : YardKit.Iron);
            Trigger<FinishZone>(s, "Finish", new Vector3(-10f, Top, z + 12f), new Vector3(10f, Top + 3f, z + 22f)).Configure(true);
            YardKit.Span(s, "Crown pedestal", new Vector3(-1.2f, Top, z + 18f), new Vector3(1.2f, Top + 1.4f, z + 20.4f), YardKit.Marble, Solid.NoClimb);
            YardKit.Cylinder(s, "Crown", new Vector3(0f, Top + 1.9f, z + 19.2f), Vector3.up, 1.3f, .9f, YardKit.Gold, Solid.None);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                YardKit.Ball(s, "Crown point", new Vector3(Mathf.Sin(a) * .6f, Top + 2.55f, z + 19.2f + Mathf.Cos(a) * .6f), .26f, YardKit.Gold, Solid.None);
            }
            Sign(s, "4. 최종 성문: 우클릭을 누른 채로 밀기", new Vector3(0f, Top + 2.6f, z - 11f), 1.3f);
            Sign(s, "결승", new Vector3(0f, Top + 4.5f, z + 13f), 2.2f);
        }

        // As a section (R508): behind the final gate's wall (`from`, own frame) the boundary every early section ends with
        // (PAWN_RUSH_SECTIONS.md, the mini-game boundary): 12 m of runout with nothing in it, a 12 x 6 m terrace 1 m over the
        // next mini-game with a checkpoint, an open arch 12 m wide, four steps of 0.25 m down and the 12 x 4 m entrance
        // band at y 7, whose end is the section's exit.
        void SectionEnd(Transform s, float from)
        {
            float runout = from + 12f, terrace = runout + 6f, steps = terrace + 2f, band = steps + 4f;
            Block(s, "Runout", -6f, 6f, GateZ + 3f, runout, -3f);
            Block(s, "Terrace", -6f, 6f, runout, terrace, -3f);
            foreach (float x in new[] { -1f, 1f })
            {
                float a = x * 6f, c = x * 6.3f;
                YardKit.Wall(s, "Runout railing", Mathf.Min(a, c), Mathf.Max(a, c), Top, Top + 1.1f, from, terrace - .4f);
                YardKit.Wall(s, "Band railing", Mathf.Min(a, c), Mathf.Max(a, c), Top - 1f, Top + .1f, terrace + .4f, band);
                float p0 = x * 6f, p1 = x * 7f;
                YardKit.Wall(s, "Arch pillar", Mathf.Min(p0, p1), Mathf.Max(p0, p1), Top - 1f, Top + 7f, terrace - .4f, terrace + .4f, YardKit.WallStone);
            }
            YardKit.Wall(s, "Arch lintel", -7f, 7f, Top + 6f, Top + 7f, terrace - .4f, terrace + .4f, YardKit.WallStone);
            for (int i = 1; i <= 4; i++)
                YardKit.Span(s, "Step down " + i, new Vector3(-6f, -3f, terrace + (i - 1) * .5f), new Vector3(6f, Top - .25f * i, terrace + i * .5f),
                             YardKit.Stone).AddComponent<CourseFloor>();
            YardKit.Span(s, "Mini-game entrance band", new Vector3(-6f, -3f, steps), new Vector3(6f, Top - 1f, band), YardKit.Stone).AddComponent<CourseFloor>();
            Checkpoint(s, 5, new Vector3(0f, Top, runout + 3f), 10f);
        }

        // A solid block whose top is the platform (y 8), from `bottom` up.
        static void Block(Transform s, string name, float x0, float x1, float z0, float z1, float bottom)
        {
            YardKit.Span(s, name, new Vector3(x0, bottom, z0), new Vector3(x1, Top, z1), YardKit.Stone).AddComponent<CourseFloor>();
        }
    }
}
