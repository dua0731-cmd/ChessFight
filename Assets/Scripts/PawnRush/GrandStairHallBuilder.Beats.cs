using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The grand stair hall's beats in the order a pawn runs them (section frame, y = a floor's top). Beats 1, 2 and
    // 6 are the level design's §4 tables; since R303 the hall between them is round and climbed by one spiral way
    // (the user's spiral-stair picture). Where the build differs from the doc, the reason is next to it and in
    // Docs/KingRush/PAWN_RUSH_SECTIONS.md §6.
    public static partial class GrandStairHallBuilder
    {
        public static readonly float[] LaneX = { -6f, -2f, 2f, 6f };
        public const float LaneWidth = 3.6f;

        // ------------------------------------------------------------------ beat 1: the rolling grand stair (y 0 -> 8)

        static void GrandStair(CourseBuilder b)
        {
            var k = b.kit;
            // The porch (x -6..6, z -12..0): the previous section joins at its arch, z -12, 12 m wide.
            Mass(b, "Porch floor", -6f, 6f, -12f, 0f, 0f);
            Solid(b, "Porch wall W", V(-6.6f, 0f, -12f), V(-6f, 6f, 0f));
            Solid(b, "Porch wall E", V(6f, 0f, -12f), V(6.6f, 6f, 0f));
            Solid(b, "Entrance arch lintel", V(-6.6f, 6f, -12.4f), V(6.6f, 7f, -11.6f));
            // The foyer (x -20..20, z 0..14): two rows of pillars, the stair straight ahead.
            Mass(b, "Foyer floor", -20f, 20f, 0f, 14f, 0f);
            Solid(b, "Foyer wall W", V(-21f, Deep, -1f), V(-20f, 12f, 14f));
            Solid(b, "Foyer wall E", V(20f, Deep, -1f), V(21f, 12f, 14f));
            Solid(b, "Foyer wall S (west of the door)", V(-20f, Deep, -1f), V(-6.6f, 12f, 0f));
            Solid(b, "Foyer wall S (east of the door)", V(6.6f, Deep, -1f), V(20f, 12f, 0f));
            foreach (float x in new[] { -14f, 14f })
                foreach (float z in new[] { 3f, 7f, 11f })
                    b.Cylinder("Foyer pillar", V(x, 0f, z), 1.2f, 12f, k.wallNoClimb, true, true);
            // The foyer's north edge looks over the hall (4 m down): railed but for the stair.
            Rail(b, "Foyer rail N (west)", -20f, -8.3f, 13.7f, 14f, 0f);
            Rail(b, "Foyer rail N (east)", 8.3f, 20f, 13.7f, 14f, 0f);
            // The drain at the stair's foot (z 12..14): the rolls sink through its grate and are gone.
            Picture(b, "Drain grate", V(-8f, 0f, 12f), V(8f, .02f, 14f), k.trimBlack);
            for (int i = 0; i <= 16; i++)
                Picture(b, "Drain bar", V(-8f + i - .05f, 0f, 12f), V(-8f + i + .05f, .03f, 14f), k.wallNoClimb);

            // The lower flight (z 14..30, y 0 -> 4, 16 steps 0.25 x 1.0), the middle landing (z 30..36, y 4), the
            // upper flight (z 36..52, y 4 -> 8). Each flight's last step is the landing above it.
            FlightZ(b, "Lower flight", -8f, 8f, 14f, 16, 1f, 0f);
            Mass(b, "Middle landing", -8f, 8f, 29f, 36f, 4f);
            FlightZ(b, "Upper flight", -8f, 8f, 36f, 16, 1f, 4f);
            // Railings x +-8, 1.1 m, unclimbable: nobody falls into the hall from the stair.
            RailAlongZ(b, "Lower flight rail W", -8.3f, -8f, 14f, 16, 1f, 0f);
            RailAlongZ(b, "Lower flight rail E", 8f, 8.3f, 14f, 16, 1f, 0f);
            Solid(b, "Middle landing rail W", V(-8.3f, Deep, 29.5f), V(-8f, 5.2f, 35.5f));
            Solid(b, "Middle landing rail E", V(8f, Deep, 29.5f), V(8.3f, 5.2f, 35.5f));
            RailAlongZ(b, "Upper flight rail W", -8.3f, -8f, 36f, 16, 1f, 4f);
            RailAlongZ(b, "Upper flight rail E", 8f, 8.3f, 36f, 16, 1f, 4f);

            // Four rolling holes at the top (z 51..52, 3.6 m) and the lanes (§5): RollingSpawner draws and rolls.
            foreach (float x in LaneX)
            {
                Picture(b, "Rolling hole", V(x - LaneWidth * .5f, 8f, 51f), V(x + LaneWidth * .5f, 8.02f, 52f), k.trimBlack);
                Picture(b, "Rolling hole rim", V(x - LaneWidth * .5f, 8f, 50.9f), V(x + LaneWidth * .5f, 8.03f, 51f), k.hazard);
            }
            var lanes = b.Group("Rolling lanes");
            lanes.gameObject.AddComponent<RollingSpawner>().Configure(k.hazard, null);
            // The four lanes cover the stair's 16 m (the rolls are 2.6 m and 3.4 m long in 3.6 m lanes).
            SpanAt(b, "Rolling lanes span", V(0f, 4f, 33f), Vector3.right, -8f, 8f);
        }

        // ------------------------------------------------------------------ beat 2: the central landing's chandeliers (y 8)

        static void CentralLanding(CourseBuilder b)
        {
            var k = b.kit;
            // x -14..14, z 52..66 (from z 51: the upper flight's last step); at its north-east corner the spiral's foot
            // leads on (R303). R340 (승규 11-04): no railings round it any more - a chandelier's hit can throw a pawn
            // off its edges (the hall model's balustrades there are cut out too, Course01v4Builder.OpenLanding).
            Mass(b, "Central landing", -14f, 14f, 51f, 66f, 8f);
            // The king statue: a 4 x 4 m base at (0, 8, 63), 6 m tall, unclimbable. The one fixed cover from the shock.
            // R413 (승규 11-05 "교체 해주고 발판 지우고"): the statue is a queen now (queen_statue_v52.py, 6 m, on the
            // floor with no pedestal; Course01v4Builder.LandingQueen draws it), so the blocks are her shape: her round foot
            // and body (2.6 m across up to 4.5 m, the names kept for SectionValidator's unclimbable list) and her crown.
            // Her two crossed maces are thin and collide with nothing.
            b.Cylinder("King statue body", V(0f, 8f, 63f), 2.6f, 4.5f, k.wallNoClimb, true, true);
            b.Cylinder("King statue collar", V(0f, 12.5f, 63f), 1.6f, 1.5f, k.wallNoClimb, true, true);
            // The falling chandeliers (§5): ChandelierRain draws them.
            var rain = b.Group("Chandelier rain");
            rain.gameObject.AddComponent<ChandelierRain>().Configure(k.rankGold);
            // A drop anywhere in x -13..13 reaches 4.5 m round it: past the landing's edges.
            SpanAt(b, "Chandelier rain span", V(0f, 8f, 55f), Vector3.right, -17.5f, 17.5f);
        }

        // ------------------------------------------------------------------ beats 3 ~ 4: the spiral (R303)

        // The hall beyond the landing is round (wall 31 m out from Hub), the royal dais in its middle. One way climbs
        // it anticlockwise, round the wall first and inwards as it rises: 420° from φ 297° (the landing's north-east
        // corner) to φ 717° (east of the dais, 10 m out), its middle line SpiralRadius(φ). φ is measured anticlockwise
        // from east, so 90° is north. A loop lies 13 m inside the one below it, so the way never passes over itself.
        public static readonly Vector2 Hub = new Vector2(0f, 92f);
        public const float HallRadius = 31f, SpiralFrom = 297f, SpiralTo = 717f, SpiralOuter = 28.75f, SpiralInner = 10f;
        public const float DaisTop = 16f, DaisRadius = 8f, BalconyFront = 121f;

        // R403: the spiral foot's north edge (the curving stair's inner corner at SpiralFrom) and the gold threshold on the
        // way where the foot's marble gives way to the chessboard (PR_SpiralChecker), 1 m along its middle line.
        public const float SpiralFootNorth = 68.39f, SpiralThreshold = SpiralFrom + 2f;

        public static float SpiralRadius(float phi) => Mathf.Lerp(SpiralOuter, SpiralInner, (phi - SpiralFrom) / (SpiralTo - SpiralFrom));

        /// <summary>A point at φ (degrees, anticlockwise from +x), r from Hub, height y, in the hall's content frame.</summary>
        public static Vector3 HallPolar(float phi, float r, float y) => Polar(phi, r, y);

        /// <summary>The way's heading at φ (anticlockwise), a level unit vector in the content frame.</summary>
        public static Vector3 SpiralHeading(float phi)
        {
            float a = phi * Mathf.Deg2Rad;
            return new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
        }

        static Vector3 Polar(float phi, float r, float y)
        {
            float a = phi * Mathf.Deg2Rad;
            return V(Hub.x + r * Mathf.Cos(a), y, Hub.y + r * Mathf.Sin(a));
        }

        static Vector3 OnSpiral(float phi, float y, float outwards = 0f) => Polar(phi, SpiralRadius(phi) + outwards, y);

        static Vector3 Outward(float phi)
        {
            float a = phi * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        // The way's heading at φ (going anticlockwise) and the outward one, as yaws from +z.
        static float Heading(float phi) { var o = Outward(phi); return Mathf.Atan2(-o.z, o.x) * Mathf.Rad2Deg; }
        static float OutwardYaw(float phi) { var o = Outward(phi); return Mathf.Atan2(o.x, o.z) * Mathf.Rad2Deg; }

        static void Spiral(CourseBuilder b)
        {
            var k = b.kit;
            // The spiral's foot off the landing's north-east corner (y 8). R348 (승규 11-04): no railings, like the landing
            // it opens off (the hall model's are cut out too, Course01v4Builder.OpenLanding).
            // R403 (승규 11-04 "대리석 바닥에서 나선형 코스로 넘어 갈 때 합쳐지는 구간을 자연스럽게"): its north edge on the
            // curving stair's inner corner (φ 297°, r 26.5: z 68.39), not 3.1 m further at z 71.5 - that part stuck out as
            // a flat marble shelf beside the rising way, a lip with a gold edge along it. Everything behind the stair's
            // first edge is still covered (the landing to z 66, this foot from there to the corner).
            Mass(b, "Spiral foot", 4f, 15.5f, 63.5f, SpiralFootNorth, 8f);

            // 3a. The curving stair (φ 297 -> 372, y 8 -> 10): along the wall, its inner edge open to the hall floor
            // 12 m down (a fall goes back to CP1). Two chandeliers swing across it, out to the wall.
            Walkway(b, "Curving stair", 297f, 372f, 8f, 10f, CurvingStairHalfIn, 0f, true, 1.2f);
            Chandelier(b, "11 Pendulum (chandelier) 1", 322f, Mathf.Lerp(8f, 10f, 25f / 75f), 0f);
            Chandelier(b, "11 Pendulum (chandelier) 2", 350f, Mathf.Lerp(8f, 10f, 53f / 75f), 1.6f);
            b.Checkpoint(2, OnSpiral(368f, 9.9f), Heading(368f), 4.5f);

            // 3b. The floating stones (φ 372 -> 440, y 10 -> 11.5): eight round slabs 2.6 m across, 0.75 m apart, every
            // second one bobbing 0.45 m up and down every 3 s (ObstacleMotion, the shared clock), so some steps want
            // a jump at the right moment.
            for (int i = 0; i < 8; i++)
            {
                float phi = 372f + (i + .5f) * 68f / 8f;
                Stone(b, "Floating stone " + (i + 1), OnSpiral(phi, 10f + 1.5f * (i + .5f) / 8f), 2.6f, i % 2 == 1 ? .45f : 0f, 3f, i * .25f);
            }

            // The north landing (φ 440 -> 482, y 11.5), under the glass stones: a rest (CP3) and the jump pad.
            Walkway(b, "North landing", 440f, 482f, 11.5f, 11.5f, 2.5f, 2.5f);
            b.Checkpoint(3, OnSpiral(452f, 11.5f), Heading(452f), 5f);

            // 4a. The jump pad ("점프대"): from φ 476 up 2.5 m across 23° of void to the first dive landing (φ 510).
            JumpPad(b, "02 Jump pad", OnSpiral(476f, 11.5f), OnSpiral(510f, 14f));

            // 4b. The dive gaps ("다이빙해야만 넘어갈 수 있는 구간", φ 500 -> 575, y 14): three landings 3 m wide with two
            // gaps 3.0 m on their inner side (3.3 along the middle, 3.6 outside) - more than a sprint jump carries (about 2.8 m
            // at 3.6 m/s), less than a jump with a dive at the top. A fall goes back to CP3.
            Walkway(b, "Dive landing 1", 500f, 522f, 14f, 14f, 1.5f, 1.5f);
            Walkway(b, "Dive landing 2", 533.1f, 548f, 14f, 14f, 1.5f, 1.5f);
            Walkway(b, "Dive landing 3", 560.3f, 575f, 14f, 14f, 1.5f, 1.5f);
            Gap(b, "Dive gap 1", OnSpiral(522f, 14f), OnSpiral(533.1f, 14f), 2.5f);
            Gap(b, "Dive gap 2", OnSpiral(548f, 14f), OnSpiral(560.3f, 14f), 2.5f);
            b.Checkpoint(4, OnSpiral(569f, 14f), Heading(569f), 3f);

            // 4c. The windy way (φ 575 -> 640, y 14 -> 14.8): 3 m wide, no railings, two vents at its inner edge blowing
            // out over the edge, half their cycle apart. The wind carries a pawn at 3.5 m/s (R301). R351 (승규 "위치도 길
            // 쪽에 자연스럽게 배치해주고"): each vent's face on the way's inner edge, standing on a stone ledge out of the
            // way's side on two consoles, instead of 0.1 m into the way on a post up from the hall floor.
            Walkway(b, "Windy way", 575f, 640f, 14f, 14.8f, 1.5f, 1.5f);
            float[] ventPhi = { 594f, 621f }, ventPhase = { 0f, 3.4f };
            for (int i = 0; i < ventPhi.Length; i++)
            {
                float phi = ventPhi[i], y = Mathf.Lerp(14f, 14.8f, (phi - 575f) / 65f);
                var at = OnSpiral(phi, y, -1.5f);
                Gust(Hazard(b, k.airVent, "15 Wind vent " + (i + 1), at, OutwardYaw(phi), ventPhase[i]));
                // The ledge under the fan's 0.5 m housing (a hair under the way's top where they meet), its consoles under it.
                b.SlopeBetween("Wind vent ledge " + (i + 1), OnSpiral(phi, y - .01f, -2.25f), OnSpiral(phi, y - .01f, -1.48f), 3.2f, .6f, 0f,
                               k.wallNoClimb, true);
                var across = Vector3.Cross(Vector3.up, Outward(phi));
                for (int side = -1; side <= 1; side += 2)
                    b.SlopeBetween("Wind vent console " + (i + 1) + (side < 0 ? " a" : " b"), OnSpiral(phi, y - .61f, -2.1f) + across * side,
                                   OnSpiral(phi, y - .61f, -1.52f) + across * side, .45f, 1.1f, 0f, k.wallNoClimb, true);
                // Its wind (3 x 3 x 6 m from the inner edge outwards) covers the way's 3 m.
                SpanAt(b, "Wind span " + (i + 1), OnSpiral(phi, y), Outward(phi), -1.6f, 1.6f);
            }
            b.Checkpoint(5, OnSpiral(645f, 14.9f), Heading(645f), 3.5f);

            // 4d. The last stair (φ 640 -> 717, y 14.8 -> 16) round the dais: two 10 falling chess pieces on it, half
            // their cycle apart, then onto the dais.
            Walkway(b, "Last stair", 640f, 717f, 14.8f, 16f, 1.75f, 1.75f, false, 1.2f);
            float[] piecePhi = { 662f, 694f }, piecePhase = { 0f, 2.27f };
            for (int i = 0; i < piecePhi.Length; i++)
            {
                float phi = piecePhi[i], y = Mathf.Lerp(14.8f, 16f, (phi - 640f) / 77f);
                var piece = Hazard(b, k.fallingPiece, "10 Falling piece " + (i + 1), OnSpiral(phi, y), Heading(phi), piecePhase[i]);
                Span(piece, Outward(phi), -5f, 5f, y);
            }
            b.SlopeBetween("Onto the dais", OnSpiral(716f, 16f), Polar(717f, DaisRadius - .5f, 16f), 3.5f, .6f, 0f, k.floorChecker, true)
             .AddComponent<CourseFloor>();
        }

        // A way along the spiral from φA to φB rising yA -> yB, `halfIn` inside its middle line and `halfOut` outside
        // (or out to the hall wall), made of chord slabs no more than 5° long that overlap a little. R341 (승규 11-04
        // "검은색 줄 지워"): the stairs no longer get a dark step line at each slab's start.
        static void Walkway(CourseBuilder b, string name, float phiA, float phiB, float yA, float yB, float halfIn, float halfOut,
                            bool toWall = false, float thickness = .6f)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt((phiB - phiA) / 5f));
            for (int i = 0; i < n; i++)
            {
                float u0 = i / (float)n, u1 = (i + 1f) / n;
                float p0 = Mathf.Lerp(phiA, phiB, u0), p1 = Mathf.Lerp(phiA, phiB, u1), pm = (p0 + p1) * .5f;
                float inner = SpiralRadius(pm) - halfIn, outer = toWall ? HallRadius : SpiralRadius(pm) + halfOut;
                float mid = (inner + outer) * .5f, y0 = Mathf.Lerp(yA, yB, u0), y1 = Mathf.Lerp(yA, yB, u1);
                Vector3 a = Polar(p0, mid, y0), c = Polar(p1, mid, y1), along = (c - a).normalized * .15f;
                var slab = b.SlopeBetween(name + " " + (i + 1), a - along, c + along, outer - inner, thickness, 0f, b.kit.floorChecker, true);
                slab.AddComponent<CourseFloor>().sloped = !Mathf.Approximately(y0, y1);
            }
        }

        // A floating stone: a round slab with its top at `top`; `bob` > 0 makes it rise and sink that far either way.
        static void Stone(CourseBuilder b, string name, Vector3 top, float diameter, float bob, float period, float phase)
        {
            var stone = b.Cylinder(name, top - Vector3.up * .5f, diameter, .5f, b.kit.floorChecker, true, true);
            stone.AddComponent<CourseFloor>();
            if (bob <= 0f) return;
            var body = stone.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var motion = stone.AddComponent<ObstacleMotion>();
            motion.kind = MotionKind.Translate;
            motion.axis = Vector3.up;
            motion.useLocalTranslationAxes = false;
            motion.distance = bob;
            motion.period = period;
            motion.phase = phase;
            motion.shove = false;
        }

        // A chandelier (11 pendulum) swinging across the way at φ, 40°, hung from a thin beam from a post inside the way
        // to the hall wall. R326 (승규 11-03, the round hall checked in numbers): hung on the way's middle line its ball
        // (1.1 m, 6 m under the pivot: 4.96 m out at 40°) went 1.3 m into the wall and its two swing-boundary marks (12 m
        // in the prefab) out through the windows. Now it hangs as far in as stops the ball's outer side 0.1 m short of
        // the wall face (r 31) at the top of its swing - its inner side swings out over the drop, so it still sweeps the
        // whole way - and the marks run from the way's inner edge to that point.
        const float CurvingStairHalfIn = 2.25f;

        static void Chandelier(CourseBuilder b, string name, float phi, float y, float phase)
        {
            var pendulum = Hazard(b, b.kit.pendulum, name, OnSpiral(phi, y + .15f), Heading(phi), phase);
            var swing = pendulum != null ? pendulum.GetComponentInChildren<PendulumSwing>(true) : null;
            float reach = 4.96f;
            if (swing != null)
            {
                swing.SwingAngle = 40f;
                reach = BallReach(swing);
            }
            float r = Mathf.Min(SpiralRadius(phi), HallRadius - .1f - reach), inner = SpiralRadius(phi) - CurvingStairHalfIn;
            var at = Polar(phi, r, y + .15f);
            if (pendulum != null)
            {
                pendulum.transform.localPosition = at;
                // The marks lie along the prefab's x, across the way.
                float outX = Mathf.Sign(Vector3.Dot(pendulum.transform.localRotation * Vector3.right, Outward(phi)));
                float from = inner - r, to = reach;
                ForChildren(pendulum, "Swing boundary", t =>
                {
                    var p = t.localPosition; p.x = outX * (from + to) * .5f; t.localPosition = p;
                    var s = t.localScale; s.x = to - from; t.localScale = s;
                });
            }
            Span(pendulum, Outward(phi), -reach, reach, y);
            float pivotY = at.y + 7.3f, postR = r - 6.2f;
            var post = Polar(phi, postR, 0f);
            Beam(b, name + " post", V(post.x - .2f, Deep, post.z - .2f), V(post.x + .2f, pivotY + .8f, post.z + .2f));
            // R325: on into the Blender round wall (its face 31 .. 32.8 there, a window's recess at φ 322) as far as the
            // window bars (r 33), so its end no longer hangs in the air in front of them.
            b.SlopeBetween(name + " beam", Polar(phi, postR, pivotY + .6f), Polar(phi, HallRadius + 2f, pivotY + .6f), .4f, .2f, .2f, b.kit.trimBlack, true);
        }

        // How far the ball's far side swings out from under the pivot: the cable's length times sin(the swing), plus
        // the ball's radius.
        static float BallReach(PendulumSwing swing)
        {
            if (swing.Ball == null) return 4.96f;
            float length = Vector3.Distance(swing.transform.position, swing.Ball.position);
            var sphere = swing.Ball.GetComponent<SphereCollider>();
            var s = swing.Ball.lossyScale;
            float radius = sphere != null ? sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)) : 1.1f;
            return length * Mathf.Sin(Mathf.Clamp(swing.SwingAngle, 0f, 85f) * Mathf.Deg2Rad) + radius;
        }

        // A 02 jump pad at `from` throwing a pawn onto `to`: 9 m/s up and forward fast enough to cover the distance in
        // the time the arc takes to come down to `to`'s height. The flight is an intended gap.
        static void JumpPad(CourseBuilder b, string name, Vector3 from, Vector3 to)
        {
            var flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
            float distance = flat.magnitude, rise = to.y - from.y, up = 9f;
            float time = (up + Mathf.Sqrt(up * up - 2f * 9.81f * rise)) / 9.81f;
            var pad = Hazard(b, b.kit.jumpPad, name, from, Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg);
            var surface = pad != null ? pad.GetComponentInChildren<ObstacleSurface>(true) : null;
            if (surface != null) surface.launchVelocity = new Vector3(0f, up, distance / time);
            Gap(b, name + " flight", from, to, 3f);
        }

        // An intended gap between two points on the way (IntendedGap: the validator's 1.5 m rule does not apply).
        static void Gap(CourseBuilder b, string name, Vector3 from, Vector3 to, float halfWidth)
        {
            var flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
            var box = b.Group(name);
            box.localPosition = (from + to) * .5f;
            box.localRotation = Quaternion.Euler(0f, Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, 0f);
            box.gameObject.AddComponent<IntendedGap>().Configure(new Vector3(halfWidth * 2f, Mathf.Abs(to.y - from.y) + 8f, flat.magnitude + 2f), name);
        }

        // A 15 vent's wind (R301): it carries a pawn (DriftSpeed) instead of pushing its bodies, and its arrows reach
        // the far edge of what it blows over (the prefab draws them 3.7 m out of a 6.2 m wind).
        const float WindWide = 3f, WindDrift = 3.5f;

        static void Gust(GameObject vent)
        {
            var air = vent != null ? vent.GetComponentInChildren<AirVent>(true) : null;
            if (air == null) return;
            air.DriftSpeed = WindDrift;
            if (air.WindTrigger != null) air.WindTrigger.size = new Vector3(WindWide, air.WindTrigger.size.y, air.WindTrigger.size.z);
            ForChildren(vent, "Air stream", t =>
            {
                t.localPosition = new Vector3(t.localPosition.x, t.localPosition.y, 3.05f);
                t.localScale = new Vector3(t.localScale.x, t.localScale.y, 5.6f);
            });
            ForChildren(vent, "Arrow tip", t => t.localPosition = new Vector3(t.localPosition.x, t.localPosition.y, 5.6f));
        }

        static GameObject Beam(CourseBuilder b, string name, Vector3 a, Vector3 c) => b.Box(name, a, c, b.kit.trimBlack, true);

        // ------------------------------------------------------------------ beat 5: the royal dais and the glass stones (y 16)

        static void RoyalDais(CourseBuilder b)
        {
            var k = b.kit;
            // The dais: a round tower 16 m across in the middle of the hall, its top the spiral's end. CP6 where the
            // spiral comes onto it.
            b.Cylinder("Royal dais", V(Hub.x, Deep, Hub.y), DaisRadius * 2f, DaisTop - Deep, k.floorChecker, true, true).AddComponent<CourseFloor>();
            var inward = -Outward(717f);
            b.Checkpoint(6, Polar(717f, DaisRadius - 1f, DaisTop), Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg, 3.5f);

            // The glass stones (R303, "오징어게임 징검다리처럼 옳바른 길은 정해져 있고 직접 밟아 봐야 안다"): seven rows of two
            // 1.8 m squares north from the dais to the balcony, 1.05 m between rows and 1.2 m between the two of a
            // row. GuessBridge picks which of each pair holds, from the section's seed.
            const int rows = 7;
            const float size = 1.8f, gap = 1.05f;
            // R333 (승규 11-03): in the Pawn Rush glass (PR_Glass.shader; R334: crystal clear colours on the ice blue sample's
            // frost, distortion and reflection) - both of a pair alike, as the
            // game wants - and the shards GuessBridge breaks off take the same material. The kit's glass if it is missing.
            var glass = Resources.Load<Material>("PR_GlassStones");
            if (glass == null) glass = k.glass;
            var left = new GameObject[rows];
            var right = new GameObject[rows];
            var stones = b.In("Glass stones", () =>
            {
                for (int i = 0; i < rows; i++)
                {
                    float z0 = Hub.y + DaisRadius + gap + i * (size + gap);
                    left[i] = b.Box("Glass square L" + (i + 1), V(-.6f - size, DaisTop - .3f, z0), V(-.6f, DaisTop, z0 + size), glass, true);
                    right[i] = b.Box("Glass square R" + (i + 1), V(.6f, DaisTop - .3f, z0), V(.6f + size, DaisTop, z0 + size), glass, true);
                }
            });
            stones.gameObject.AddComponent<GuessBridge>().Configure(left, right);
            // Under them a fall goes back to the dais (CP6), not onto the spiral below. Low enough to miss a jump on
            // the north landing (y 11.5) under it.
            b.Trigger<KillVolume>("Kill volume (glass stones)", V(-3.5f, DaisTop - 1.4f, Hub.y + DaisRadius + .5f), V(3.5f, DaisTop - .6f, BalconyFront - .2f));
        }

        // ------------------------------------------------------------------ beat 6: the royal balcony, the gate, the way out (y 16 -> 15)

        // The doc's beat 6 moved 26.3 m north and 4 m down (R303): its balcony now opens off the round wall at the end
        // of the glass stones.
        static void RoyalBalcony(CourseBuilder b)
        {
            var k = b.kit;
            // The balcony: x -8..8, z 121..128.3, y 16.
            Mass(b, "Royal balcony", -8f, 8f, 121f, 128.3f, 16f, 8f);
            Solid(b, "Balcony wall W", V(-8.6f, 8f, 121f), V(-8f, 22f, 128.3f));
            Solid(b, "Balcony wall E", V(8f, 8f, 121f), V(8.6f, 22f, 128.3f));
            // The guard knights' alcoves either side, 3 m up, unclimbable. They fill the balcony's sides along the
            // charge lanes, so the two charges cover the way across (§7: "기병(발코니 폭 16 m를 두 돌진 띠가 나눠 덮음)").
            Solid(b, "Knight alcove W", V(-8f, 16f, 121.9f), V(-5.2f, 19f, 127.7f));
            Solid(b, "Knight alcove E", V(5.2f, 16f, 121.9f), V(8f, 19f, 127.7f));
            // Two 17 knights: land at (-4, 16, 123.3) and charge 8 m east; land at (4, 16, 126.3) and charge west. Half
            // a cycle apart (7.62 s), so one lane is always quiet. They leap from their alcoves.
            Knight(b, "W", V(-2f, 16f, 123.3f), 0f, 0f);
            Knight(b, "E", V(2f, 16f, 126.3f), 180f, 3.81f);
            // The 09 clock gate (two doors, 5 s, out of step) and its blocking walls to the sides (5.8 m).
            var gates = Hazard(b, k.clockGates, "09 Clock gate (the palace door)", V(0f, 16f, 128.3f), 0f, 0f);
            Span(gates, Vector3.right, -4.3f, 4.3f, 16f);
            Solid(b, "Gate wall W", V(-8f, 16f, 127.7f), V(-4.3f, 21.8f, 128.9f));
            Solid(b, "Gate wall E", V(4.3f, 16f, 127.7f), V(8f, 21.8f, 128.9f));
            // The runout: 18 m without obstacles, railed.
            Mass(b, "Runout", -6f, 6f, 128.3f, 146.3f, 16f, 8f);
            Rail(b, "Runout rail W", -6.3f, -6f, 128.9f, 146.3f, 16f);
            Rail(b, "Runout rail E", 6f, 6.3f, 128.9f, 146.3f, 16f);
            // The exit terrace (12 x 6 m, CP 끝), 1 m over the mini-game.
            Mass(b, "Exit terrace", -6f, 6f, 146.3f, 152.3f, 16f, 8f);
            Marker(b, "Exit terrace (CP 끝)", -6f, 6f, 146.3f, 152.3f, 16f);
            Rail(b, "Terrace rail W", -6.3f, -6f, 146.3f, 151.9f, 16f);
            Rail(b, "Terrace rail E", 6f, 6.3f, 146.3f, 151.9f, 16f);
            // The open arch (12 wide, 5 high, no door), four steps of 0.25 down, the entrance band (z 154.3..158.3, y 15).
            Solid(b, "Arch pillar W", V(-7f, 15f, 151.9f), V(-6f, 22f, 152.7f));
            Solid(b, "Arch pillar E", V(6f, 15f, 151.9f), V(7f, 22f, 152.7f));
            Solid(b, "Arch lintel", V(-7f, 21f, 151.9f), V(7f, 22f, 152.7f));
            for (int i = 1; i <= 4; i++)
                Mass(b, "Step down " + i, -6f, 6f, 152.3f + (i - 1) * .5f, 152.3f + i * .5f, 16f - .25f * i, 8f);
            Mass(b, "Mini-game entrance band", -6f, 6f, 154.3f, 158.3f, 15f, 8f);
            Rail(b, "Band rail W", -6.3f, -6f, 152.7f, 158.3f, 15f);
            Rail(b, "Band rail E", 6f, 6.3f, 152.7f, 158.3f, 15f);
        }

        // A 17 knight whose root is `root` (it lands 2 m behind its root and charges 8 m along its +x), leaping
        // from the alcove beside it (3 m up).
        static void Knight(CourseBuilder b, string side, Vector3 root, float yaw, float phase)
        {
            var go = Hazard(b, b.kit.knightCavalry, "17 Guard knight " + side, root, yaw, phase);
            var knight = go != null ? go.GetComponent<KnightCavalryCharge>() : null;
            if (knight == null) return;
            var start = new Vector3(-4.6f, 3f, 0f);
            if (knight.StartPoint != null) knight.StartPoint.position = go.transform.TransformPoint(start);
            if (knight.KnightRoot != null) knight.KnightRoot.transform.position = go.transform.TransformPoint(start);
            // Its lane across the balcony, between the alcoves: landing -2 .. end +6 (its +x), the body 1.2 m round.
            float sign = Mathf.Approximately(yaw, 180f) ? -1f : 1f;
            Span(go, Vector3.right, sign > 0 ? -3.2f : -7.2f, sign > 0 ? 7.2f : 3.2f, root.y);
        }
    }
}
