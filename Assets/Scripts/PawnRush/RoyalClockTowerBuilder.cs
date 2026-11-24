using ChessFight.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;
using Look = ChessFight.PawnRush.RoyalClockTowerLook;

namespace ChessFight.PawnRush
{
    // The royal clock tower (왕실 시계탑, R775 design, R780 grey box, R781 after the user's first run), middle obstacle section
    // 2-2: the section whose walls are the way. No stairs; every climb ends on something to stand on or something that carries.
    // Design and the numbers' reasons: Docs/PawnRush/PAWN_RUSH_ROYAL_CLOCK_TOWER_DESIGN_v0.1.md.
    //
    //   1 the wall that teaches (3.5 m)
    //   2 the weight chains: climb onto a low weight, ride it up, climb onto the next one (down while yours is up), ride, step off
    //   3 the drop (7 m: a ledge, a jump, a dive)
    //   4 the corner piers: tall, unclimbable above a band, so the only way to the island is round the corner (ㄱ)
    //   5 the bell ropes (five swings over 8 m)
    //   6 the carillon drum: no way onto its top; boards on its side to stand on, two fixed combs to get past
    //   7 the ratchet wall: blocks slide out, lift whoever stands on them a row, slide back in (an escapement)
    //   8 the great dial: ride the turning minute hand from six to noon, step onto the noon ledge
    //   9 the copper roof (a slide down)
    //
    // Section frame: x across (east +), y up, +z the way on, metres; entry (0, 0, 0) in the middle of the 12 m threshold, exit
    // (0, ExitY, ExitZ). Bearings round the drum are degrees from east, counter-clockwise seen from above (90 = north).
    // Every mover is a pure function of the obstacle clock (MovingPlatform, RatchetStep, RopeLine): nothing is sent.
    public static partial class RoyalClockTowerBuilder
    {
        public const float YardY = 3.5f, GalleryY = 12f, FloorY = 5f, IslandY = 6.4f, CourtY = 15f, BellY = 28f,
                           ExitY = 22f, ExitZ = 120f;
        const float Deep = -8f, WallTop = 34f, Cornice = 26.2f;

        // 2. The weight chains: four lanes, each a low weight then a high one, a pair half a cycle apart (one up while the
        //    other is down), the lanes a quarter apart. Only the top 2.2 m of a weight (its head) can be held.
        public static readonly float[] WeightX = { -15.5f, -12f, -6f, -2.5f };
        public const float LowZ0 = 18f, LowZ1 = 20.4f, HighZ0 = 20.9f, HighZ1 = 23.3f;
        public const float LowDown = 5f, HighDown = 9f, WeightLift = 2.5f;
        const float WeightHead = 2.2f, WeightSpeed = 1.25f, WeightPause = 2.5f, WeightRamp = .4f, WeightSide = 2.4f;

        // 4. The corner piers and the island: the band to climb, and above it a face that cannot be held.
        static readonly float[] PierX0 = { 8f, 13.6f };
        public const float PierWidth = 2.4f, PierZ0 = 37f, PierZ1 = 40f, PierBand = 8.4f, PierTop = 14f,
                           IslandZ0 = 39.6f, IslandZ1 = 44f;

        // 5. The bell ropes: the lab's swing (an 8 m gap, 40 degrees each way, 5 s), a second apart.
        static readonly float[] RopeX = { 6f, 9f, 12f, 15f, 18f };
        const float RopeLength = 6.5f, RopeSwing = 40f, RopePeriod = 5f, RopePivotZ = 48.2f;

        // 6. The drum: centre, radius, degrees a second (north-about: east -> north -> west), its climbable height, the cap
        //    over it, the boards round it (alternately high and low) and the combs.
        public const float DrumX = 2f, DrumZ = 66f, DrumRadius = 5f, DrumSpin = 15f, DrumTop = 10.1f, DrumCapTop = 13.5f;
        public const float BoardHigh = 8.6f, BoardLow = 5.8f;
        const float Comb1Bearing = 70f, Comb1Top = 8.2f, Comb2Bearing = 135f, Comb2Bottom = 7.4f;

        // 7. The ratchet wall: three rows; a block's top goes from Low to Low + Rise, then the next row's block is out
        //    1.5 m higher, half a column along.
        public static readonly float[] RatchetLow = { FloorY + .25f, 9.25f, 13.25f };
        public static readonly float[] RatchetRise = { 2.5f, 2.5f, CourtY - 13.25f };
        const float RatchetWidth = 1.8f, RatchetThick = .5f, RatchetOut = .9f, RatchetPeriod = 8f,
                    RatchetRowLag = 3.5f, RatchetColumnLag = 2f;

        // 8. The great dial: centre, the minute hand (length, turn) and the hour hand, the plinth under six o'clock and the
        //    noon ledge.
        public const float DialX = 0f, DialY = 21.5f, MinuteLength = 5.5f, MinuteSpin = 15f, HourLength = 3.4f, HourSpin = 4f;
        public const float NoonLedgeTop = 27f, PlinthTop = 15.8f;

        public static void Build(RoyalClockTowerSection tower)
        {
            var root = tower.transform;
            var old = root.Find(RoyalClockTowerSection.ContentName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var content = new GameObject(RoyalClockTowerSection.ContentName);
            content.SetActive(false);
            content.transform.SetParent(root, false);
            var b = new CourseBuilder(Look.Kit, content.transform);

            Shell(b);
            TeachingWall(b);
            Weights(b);
            Drop(b);
            CornerPiers(b);
            BellRopes(b);
            Drum(b);
            RatchetWall(b);
            Dial(b);
            Roof(b);
            Course(b);

            tower.GetComponent<PawnRushSection>().Describe(RoyalClockTowerSection.Id, RoyalClockTowerSection.DisplayName,
                PawnRushSection.SlotKind.Obstacle, RoyalClockTowerSection.Entry, RoyalClockTowerSection.Exit, 0f, Beats());
            content.SetActive(true);   // the movers wake here with their final pose
        }

        // ------------------------------------------------------------------ the shell

        static void Shell(CourseBuilder b)
        {
            Wall(b, "CT wall S-W", -22.6f, -6f, Deep, WallTop, -.6f, 0f);
            Wall(b, "CT wall S-E", 6f, 22.6f, Deep, WallTop, -.6f, 0f);
            Wall(b, "CT entrance lintel", -6f, 6f, 6.5f, WallTop, -.6f, 0f);
            Wall(b, "CT entry bay W", -22.6f, -12f, Deep, WallTop, 0f, 10f);
            Wall(b, "CT entry bay E", 12f, 22.6f, Deep, WallTop, 0f, 10f);
            Wall(b, "CT wall W", -22.6f, -22f, Deep, WallTop, 10f, 90f);
            Wall(b, "CT wall E", 22f, 22.6f, Deep, WallTop, 10f, 90f);
            Floor(b, "CT entry floor", -12f, 12f, 0f, 10f, 0f, true);

            var start = new GameObject("Start").transform;   // where the map test drops the pawn
            start.SetParent(b.parent, false);
            start.localPosition = V(0f, 0f, 3f);
            start.localRotation = Quaternion.identity;
        }

        // ------------------------------------------------------------------ 1. the wall that teaches

        static void TeachingWall(CourseBuilder b)
        {
            Floor(b, "CT weight yard", -22f, 22f, 10f, 24f, YardY, false);
            Paint(b, "CT teaching wall", V(-12f, 0f, 9.97f), V(12f, YardY, 10f), Look.Climb);
        }

        // ------------------------------------------------------------------ 2. the weight chains

        // R781 (the user: "보라빛 꼭대기에 올라가기만 해서 밋밋함"): one weight lifting to the gallery was a single climb and a
        // wait. Now each lane is a chain: climb 1.5 m onto the low weight while it is down, ride it 2.5 m up, climb 1.5 m onto
        // the high weight - down exactly while the low one is up - ride it up and step off onto the gallery. Climb, ride,
        // climb, ride: missing the beat means hanging on a weight's head while it carries you (no stamina), or climbing more.
        // The block's own face is still the 8.5 m cliff, in three lanes, for whoever will not ride.
        static void Weights(CourseBuilder b)
        {
            Floor(b, "CT movement", -22f, 2f, 24f, 34f, GalleryY, false);
            foreach (var lane in new[] { V(-22f, 0f, -17f), V(-10.5f, 0f, -7.5f), V(-1f, 0f, 2f) })
                Paint(b, "CT cliff lane", V(lane.x, YardY, 23.97f), V(lane.z, GalleryY, 24f), Look.Climb);
            Wall(b, "CT gallery rail N", -22f, 2f, GalleryY, GalleryY + 1.2f, 33.7f, 34f);

            float leg = WeightLift / WeightSpeed + WeightRamp, cycle = 2f * (WeightPause + leg);
            for (int i = 0; i < WeightX.Length; i++)
            {
                float lane = cycle * .25f * i;
                Weight(b, "CT weight " + (i + 1) + " low", WeightX[i], LowZ0, LowZ1, LowDown, YardY, lane);
                Weight(b, "CT weight " + (i + 1) + " high", WeightX[i], HighZ0, HighZ1, HighDown, YardY, lane + cycle * .5f);
            }
        }

        // A weight whose top is at `down` at the start of its cycle and WeightLift higher half a cycle later; its slate shaft
        // reaches below `floor` at the top of its travel, so it always goes down through the floor.
        static void Weight(CourseBuilder b, string name, float x, float z0, float z1, float down, float floor, float phase)
        {
            float depth = z1 - z0, shaft = down + WeightLift - WeightHead - floor + .3f;
            var pillar = Root(b, name, V(x, down, (z0 + z1) * .5f));
            YardKit.Box(pillar, "CT weight head", V(0f, -WeightHead * .5f, 0f), V(WeightSide, WeightHead, depth), Look.Mover, YardKit.Solid.Solid);
            YardKit.Box(pillar, "CT weight shaft", V(0f, -WeightHead - shaft * .5f, 0f), V(WeightSide, shaft, depth), Look.NoClimb, YardKit.Solid.NoClimb);
            pillar.gameObject.AddComponent<MovingPlatform>()
                  .Configure(V(0f, WeightLift, 0f), WeightSpeed, WeightPause, WeightRamp, 0f, null, phase);
        }

        // ------------------------------------------------------------------ 3. the drop

        static void Drop(CourseBuilder b)
        {
            Floor(b, "CT ringers floor", 2f, 22f, 24f, PierZ0, FloorY, false);
            Paint(b, "CT drop face", V(2f, FloorY, 24f), V(2.03f, GalleryY, 34f), Look.Down);
            Shelf(b, "CT drop ledge", 2f, 4.5f, 29f, 34f, 8.5f);
            // The grille is seen through (the dial shows from the entry): its solid is not drawn, bars are.
            var grille = Wall(b, "CT yard grille", 2f, 22f, YardY, 16.5f, 23.4f, 24f);
            Object.DestroyImmediate(grille.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(grille.GetComponent<MeshFilter>());
            for (float x = 2.5f; x < 22f; x += 1.5f)
                Paint(b, "CT grille bar", V(x - .08f, YardY, 23.62f), V(x + .08f, 16.5f, 23.78f), Look.NoClimb);
            foreach (float y in new[] { 7.5f, 12f, 16.4f })
                Paint(b, "CT grille rail", V(2f, y - .08f, 23.6f), V(22f, y + .08f, 23.8f), Look.NoClimb);
            Wall(b, "CT ringers rail W", 1.7f, 2f, FloorY, FloorY + 1.2f, 34f, PierZ0);
        }

        // ------------------------------------------------------------------ 4. the corner piers

        // R781 (the user: "바로 옆으로 가서 가면 ㄱ자 벽타기를 못함 … 높이도 올려줘서 ㄱ자만 가능하게"): the piers stand 9 m over
        // the floor and only their lowest 3.4 m can be held (the slate above opens the hands), so nobody goes over the top; the
        // island is 2.6 m from the floor's edge, past any jump, and 1.4 m up with sides that cannot be held. The one way: take
        // a pier's south face, round its corner and go 2.6 m along its side face, climbing a little, until the island is
        // under the feet.
        static void CornerPiers(CourseBuilder b)
        {
            Floor(b, "CT rope island", 5f, 19f, IslandZ0, IslandZ1, IslandY, true);
            for (int i = 0; i < PierX0.Length; i++)
            {
                float x0 = PierX0[i], x1 = x0 + PierWidth;
                b.Box("CT corner pier " + (i + 1), V(x0, Deep, PierZ0), V(x1, PierBand, PierZ1), Look.Climb);
                b.Box("CT corner pier top " + (i + 1), V(x0, PierBand, PierZ0), V(x1, PierTop, PierZ1), Look.NoClimb, true);
            }
        }

        // ------------------------------------------------------------------ 5. the bell ropes

        static void BellRopes(CourseBuilder b)
        {
            Floor(b, "CT drum floor S", 2f, 22f, 52f, 58f, FloorY, false);
            Wall(b, "CT drum floor rail W", 1.7f, 2f, FloorY, FloorY + 1.2f, 52f, 58f);
            float pivotY = IslandY + 1.1f + RopeLength * Mathf.Cos(RopeSwing * Mathf.Deg2Rad);
            for (int i = 0; i < RopeX.Length; i++)
            {
                var top = Root(b, "CT rope " + (i + 1), V(RopeX[i], pivotY, RopePivotZ));
                top.gameObject.AddComponent<RopeLine>().Configure(RopeLength, RopeSwing, RopePeriod, i * 1f, .1f, Look.Mover);
                YardKit.Ball(b.parent, "CT bell " + (i + 1), V(RopeX[i], pivotY + .9f, RopePivotZ), 1.4f, Look.Climb, YardKit.Solid.None);
            }
        }

        // ------------------------------------------------------------------ 6. the carillon drum

        // R781 (the user: "편종 원통 위에는 아예 올라가지 못하고 옆면에 판자를 놓아서 쉴 수 있게"): the drum's top is capped by a
        // still slate cylinder 3.4 m tall that cannot be held, so the high road over it is gone. Six boards ride round on the
        // drum's side, alternately high (8.6) and low (5.8): stand on one and the drum carries you, resting. A high board goes
        // over the first comb (which shuts the lower 3.2 m); a low board goes under the second (which shuts the upper part) -
        // so get onto a high board before the first comb, and down onto a low one (or hang under it) before the second.
        static void Drum(CourseBuilder b)
        {
            Floor(b, "CT drum floor E", 10f, 22f, 58f, 76f, FloorY, false);
            Floor(b, "CT west floor", -22f, -6f, 58f, 76f, FloorY, false);
            Wall(b, "CT west floor rail S", -22f, -6f, FloorY, FloorY + 1.2f, 58f, 58.3f);

            var drum = Root(b, "CT drum", V(DrumX, 0f, DrumZ));
            float height = DrumTop - Deep;
            YardKit.Cylinder(drum, "CT drum body", V(0f, Deep + height * .5f, 0f), Vector3.up, DrumRadius * 2f, height, Look.Mover, YardKit.Solid.Solid);
            for (int k = 0; k < 6; k++)
            {
                float bearing = 30f + 60f * k, top = k % 2 == 0 ? BoardHigh : BoardLow, a = bearing * Mathf.Deg2Rad;
                var board = YardKit.Box(drum, "CT drum board " + (k + 1) + (k % 2 == 0 ? " high" : " low"),
                                        V((DrumRadius + .45f) * Mathf.Cos(a), top - .15f, (DrumRadius + .45f) * Mathf.Sin(a)),
                                        V(1.6f, .3f, .9f), Look.Rest, YardKit.Solid.NoClimb, Outward(bearing));
                board.AddComponent<CourseFloor>();
            }
            // Unity turns +z toward +x for a positive angle about y; east -> north is the other way.
            drum.gameObject.AddComponent<MovingPlatform>().Configure(Vector3.zero, 0f, 0f, .5f, -DrumSpin, Vector3.up, 0f);
            YardKit.Cylinder(b.parent, "CT drum cap", V(DrumX, DrumTop + (DrumCapTop - DrumTop) * .5f, DrumZ), Vector3.up,
                             DrumRadius * 2f, DrumCapTop - DrumTop, Look.NoClimb, YardKit.Solid.NoClimb);

            for (float bearing = -25f; bearing <= 5.01f; bearing += 6f) Tongue(b, "CT tongue E", bearing);
            for (float bearing = 175f; bearing <= 205.01f; bearing += 6f) Tongue(b, "CT tongue W", bearing);

            Comb(b, "CT comb 1", Comb1Bearing, FloorY, Comb1Top);
            Comb(b, "CT comb 2", Comb2Bearing, Comb2Bottom, DrumTop - .1f);
        }

        public static Vector3 OnDrum(float bearing, float radius, float y)
        {
            float a = bearing * Mathf.Deg2Rad;
            return V(DrumX + radius * Mathf.Cos(a), y, DrumZ + radius * Mathf.Sin(a));
        }

        static Quaternion Outward(float bearing)
        {
            float a = bearing * Mathf.Deg2Rad;
            return Quaternion.LookRotation(V(Mathf.Cos(a), 0f, Mathf.Sin(a)), Vector3.up);
        }

        static void Tongue(CourseBuilder b, string name, float bearing)
        {
            const float gap = .08f, reach = 4.2f;
            var slab = YardKit.Box(b.parent, name + " " + Mathf.RoundToInt(bearing), OnDrum(bearing, DrumRadius + gap + reach * .5f, FloorY - .2f),
                                   V(1.1f, .4f, reach), Look.Rest, YardKit.Solid.NoClimb, Outward(bearing));
            slab.AddComponent<CourseFloor>();
        }

        // A comb: a picture reaching past the boards to the drum's side, and the trigger that opens a climber's hands.
        static void Comb(CourseBuilder b, string name, float bearing, float y0, float y1)
        {
            const float inner = .1f, length = 1.4f;
            var at = Root(b, name, OnDrum(bearing, DrumRadius + inner + length * .5f, (y0 + y1) * .5f));
            at.localRotation = Outward(bearing);
            YardKit.Box(at, name + " picture", Vector3.zero, V(.5f, y1 - y0, length), Look.Comb, YardKit.Solid.None);
            var box = at.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = V(.7f, y1 - y0, length);
            at.gameObject.AddComponent<ClimbPeel>().Configure(Vector3.forward, 2f);
        }

        // ------------------------------------------------------------------ 7. the ratchet wall

        // R781 (the user: "망치벽을 좀 더 재밌게"): the clock court's south face over the west floor, 10 m, is an escapement.
        // Blocks slide out of the wall, wait 1.5 s to be stepped on, lift whoever stands on them a row (2.5 m), hold 1.2 s and
        // slide back in. When a row's block reaches the top, the block of the row above - 1.5 m higher and half a column
        // along - has just come out: climb the wall to it (diagonally, about a third of the bar) and ride again. Three rides,
        // two short climbs; the wall can always be held, so a block that leaves without you is a hang, not a fall. Four
        // columns a row, 2 s apart, so another block is always on its way.
        static void RatchetWall(CourseBuilder b)
        {
            Floor(b, "CT clock court", -22f, 22f, 76f, 90f, CourtY, true);
            Skin(b, "CT ratchet wall", V(-22f, FloorY, 75.96f), V(-6f, CourtY, 76f));
            Wall(b, "CT court rail S", -6f, 22f, CourtY, CourtY + 1.2f, 76f, 76.3f);
            Wall(b, "CT court rail N-W", -22f, -2.2f, CourtY, CourtY + 1.2f, 89.7f, 90f);
            Wall(b, "CT court rail N-E", 2.2f, 22f, CourtY, CourtY + 1.2f, 89.7f, 90f);

            for (int row = 0; row < 3; row++)
                for (int c = 0; c < (row == 1 ? 3 : 4); c++)
                {
                    float x = (row == 1 ? -18f : -20f) + 4f * c, top = RatchetLow[row];
                    // At rest inside the wall (its front just behind the skin's) at its low height; out is 0.9 m toward -z.
                    var block = b.Box("CT ratchet " + (row + 1) + "-" + (c + 1), V(x - RatchetWidth * .5f, top - RatchetThick, 75.98f),
                                      V(x + RatchetWidth * .5f, top, 75.98f + RatchetOut), Look.Mover);
                    block.AddComponent<CourseFloor>();
                    block.AddComponent<RatchetStep>().Configure(V(0f, 0f, -RatchetOut), RatchetRise[row], RatchetPeriod,
                                                               row * RatchetRowLag + c * RatchetColumnLag);
                }
        }

        // ------------------------------------------------------------------ 8. the great dial

        // R781 (the user: "기둥 보다는 실제 대 시계 시침과 분침이 가고 그거 대로 올라탄다"): the dial's face cannot be held; its
        // hands turn. The minute hand goes round in 24 s; at six its tip is over a plinth in front of the dial - climb onto
        // the plinth and take hold of the hand's front face as it passes, hang on (no stamina) and it carries you up the west
        // side to noon, where its tip is just under a ledge: climb the last metre and over onto the noon ledge (level with the tip at noon), then up
        // the cornice (1 m, orange) to the belfry. Or stand on the hand while it is level and ride it, sliding as it tips. The hour hand,
        // shorter and slow (90 s round), turns behind it: a slow platform for whoever misses the minute hand.
        static void Dial(CourseBuilder b)
        {
            Floor(b, "CT tower", -8f, 8f, 90f, 100f, BellY, true);
            Wall(b, "CT dial wall", -8f, 8f, CourtY, Cornice, 89.96f, 90f);
            Skin(b, "CT cornice", V(-8.4f, Cornice, 89.5f), V(8.4f, BellY, 90f));   // held from the noon ledge only: the dial under it cannot be
            YardKit.Cylinder(b.parent, "CT dial", V(DialX, DialY, 89.93f), Vector3.forward, 12f, .05f, Look.Dial, YardKit.Solid.None);
            for (int h = 0; h < 12; h++)
            {
                float a = h * 30f * Mathf.Deg2Rad;
                Paint(b, "CT dial mark " + h, V(DialX + 5.4f * Mathf.Sin(a) - .12f, DialY + 5.4f * Mathf.Cos(a) - .3f, 89.88f),
                      V(DialX + 5.4f * Mathf.Sin(a) + .12f, DialY + 5.4f * Mathf.Cos(a) + .3f, 89.9f), Look.NoClimb);
            }

            Hand(b, "CT hour hand", HourLength, .7f, 89.0f, 89.9f, HourSpin, 90f);
            Hand(b, "CT minute hand", MinuteLength, .8f, 87.4f, 88.2f, MinuteSpin, 180f);
            YardKit.Ball(b.parent, "CT dial hub", V(DialX, DialY, 87.2f), 1f, Look.Climb, YardKit.Solid.None);

            var plinth = b.Box("CT plinth", V(-1.2f, CourtY, 86.4f), V(1.2f, PlinthTop, 87.3f), Look.Rest, true);
            plinth.AddComponent<CourseFloor>();
            Shelf(b, "CT noon ledge", -1.5f, 1.5f, 88.2f, 89.5f, NoonLedgeTop);
        }

        // A hand pivoted on the dial's centre, `start` degrees clockwise from noon at clock 0, turning clockwise as seen from
        // the court: a negative turn about +z (into the dial), measured in the R781 check.
        static void Hand(CourseBuilder b, string name, float length, float width, float z0, float z1, float spin, float start)
        {
            var pivot = Root(b, name, V(DialX, DialY, (z0 + z1) * .5f));
            pivot.localRotation = Quaternion.AngleAxis(-start, Vector3.forward);
            var bar = YardKit.Box(pivot, name + " bar", V(0f, (length - .6f) * .5f, 0f), V(width, length + .6f, z1 - z0), Look.Mover, YardKit.Solid.Solid);
            bar.AddComponent<CourseFloor>();
            pivot.gameObject.AddComponent<MovingPlatform>().Configure(Vector3.zero, 0f, 0f, .5f, -spin, Vector3.forward, 0f);
        }

        // ------------------------------------------------------------------ 9. the copper roof

        static void Roof(CourseBuilder b)
        {
            b.Ramp("CT copper roof", -8f, 8f, 100f, 112f, BellY, ExitY, 3f).GetComponent<MeshRenderer>().sharedMaterial = Look.Down;
            b.Slope("CT roof rail W", -8.3f, -8f, 100f, 112f, BellY, ExitY, 3f, 1.2f, Look.NoClimb, true);
            b.Slope("CT roof rail E", 8f, 8.3f, 100f, 112f, BellY, ExitY, 3f, 1.2f, Look.NoClimb, true);
            Floor(b, "CT exit terrace", -8f, 8f, 112f, ExitZ, ExitY, true);
            Wall(b, "CT exit rail W", -8.3f, -8f, ExitY, ExitY + 1.2f, 112f, ExitZ);
            Wall(b, "CT exit rail E", 8f, 8.3f, ExitY, ExitY + 1.2f, 112f, ExitZ);
            Wall(b, "CT belfry rail W", -8.3f, -8f, BellY, BellY + 1.2f, 90f, 100f);
            Wall(b, "CT belfry rail E", 8f, 8.3f, BellY, BellY + 1.2f, 90f, 100f);
        }

        // ------------------------------------------------------------------ the vocabulary

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static GameObject Floor(CourseBuilder b, string name, float x0, float x1, float z0, float z1, float top, bool noClimb)
        {
            var go = b.Box(name, V(x0, Deep, z0), V(x1, top, z1), Look.Floor, noClimb);
            go.AddComponent<CourseFloor>();
            return go;
        }

        static GameObject Shelf(CourseBuilder b, string name, float x0, float x1, float z0, float z1, float top)
        {
            var go = b.Box(name, V(x0, top - .3f, z0), V(x1, top, z1), Look.Rest, true);
            go.AddComponent<CourseFloor>();
            return go;
        }

        static GameObject Wall(CourseBuilder b, string name, float x0, float x1, float y0, float y1, float z0, float z1) =>
            b.Box(name, V(x0, y0, z0), V(x1, y1, z1), Look.NoClimb, true);

        static GameObject Skin(CourseBuilder b, string name, Vector3 min, Vector3 max) => b.Box(name, min, max, Look.Climb);

        static GameObject Paint(CourseBuilder b, string name, Vector3 min, Vector3 max, Material material) =>
            b.Box(name, min, max, material, false, false);

        static Transform Root(CourseBuilder b, string name, Vector3 at)
        {
            var t = b.Group(name);
            t.localPosition = at;
            return t;
        }
    }
}
