using ChessFight.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;
using Look = ChessFight.PawnRush.RoyalClockTowerLook;

namespace ChessFight.PawnRush
{
    // The grand banquet hall (대연회장, R815 design v0.4, R816 grey box), middle obstacle section 2-4: a royal banquet-and-ball
    // hall laid out as one would be, every device in the place it belongs, on one way that crosses the hall three times.
    // Design and the numbers' reasons: Docs/PawnRush/PAWN_RUSH_BANQUET_HALL_DESIGN_v0.4.md (its '회색 상자' part lists what
    // the grey box changed); ArtSource/PawnRush/BanquetHall/banquet_layout.py worked the numbers out.
    //
    //   entry, the screens passage and the servery (south)
    //   1 the waiters' parade: ride a tray past the counter, climb its plate stack, step off onto the long table
    //   2 the magician's tablecloth trick: north along the table, on a plate when the drum roll ends
    //   3 the cloche lifts: board a platter, ride it up inside the shut cloche to the royal dais (R825, R828)
    //   4 the waltz: jump onto a queen standing still in front of the dais, ride the dance round to the stage
    //   5 the stage curtain: climb it over the double basses and ride it open over the pit to the dessert buffet
    //   6 the chocolate fountain: climb against the flow, three tiers; leap from the top to the loft's ladder
    //   7 the chandeliers: two lines from the loft, north over the dance floor by the great chandelier
    //   8 the king's balcony, where the last ropes end (the exit; R825: the champagne tower is gone)
    //
    // Section frame: x east, y up, z north (the way in), metres; entry (0, 0, 0) in the middle of the south doorway, exit
    // (0, BalconyY, HallZ1). Every mover is a pure function of the obstacle clock (BanquetMovers, BanquetDevices,
    // RopeLine): nothing is sent.
    public static partial class GrandBanquetHallBuilder
    {
        // The hall's levels and frame.
        public const float TableY = 4.4f, StageY = 7.4f, DaisY = 10.4f, HeadY = 14.8f, LoftY = 20f;
        public const float HallX0 = -32f, HallX1 = 32f, HallZ1 = 72f, ScreenZ = 6f, DaisZ = 56f, Ceiling = 34f;

        // 1. The waiters' parade. Four waiters 7.25 m apart walk the lane between the long table and the east wall at 2.2 m/s,
        //    from the kitchen (inside the hall's south-east corner) to the pantry door beside the table's south end. R825 (the
        //    user: "웨이터 파트에서 무조건 최대한 오래 타있는게 이득이야 … 식탁보 빼기 코스의 이동 경로가 웨이터의 이동 방향과
        //    동일해서 굳이 위험하게 일찍 내릴 이유가 없어"): the pantry was beside the table's north end, so a rider stayed on past
        //    the whole tablecloth trick. Now the door is 8 m along the table: step off onto it in those 8 m or be carried in.
        //    The pantry runs on beside the rest of the table, its wall over the table's east edge.
        public const float TrayX = 27.8f, WaiterBodyX = 29.8f, TrayY = 2.4f, TrayR = 1.5f, StackR = .8f, StackTop = 5f;
        public const float WaiterZ0 = .5f, WaiterLane = 29f, WaiterSpeed = 2.2f;
        public const int Waiters = 4;
        public const float CounterX0 = 22f, CounterX1 = 26f, CounterZ0 = 4f, CounterZ1 = 12f, KitchenZ1 = 4f, PantryZ0 = 26f;

        // 2. The long table and the tablecloth trick (a 10 s cycle: lies 3.5, drum roll 2, pull 0.3, gone 2.2, laid 2).
        public const float TableX0 = 14f, TableX1 = 26f, TableZ0 = 18f, ClothZ1 = 50f, RunnerX = 20f, PlateR = 1.2f, LandingZ = 9f;
        public static readonly float[] PlateZ = { 23f, 29f, 35f, 41f, 47f };

        // 3. The cloche lifts at the table's north end, against the dais (R825, ClocheLift): a platter 0.12 m over the table
        //    that rises to the dais's height, a hollow cloche that is open 2 m over it while pawns board or step out and shut
        //    while it rises or comes down. The two run half a cycle (6.2 s) apart: one boards while the other is up.
        public static readonly float[] ClocheX = { 17f, 23f };
        public const float ClocheZ = DaisZ - .1f - 2.2f, ClocheR = 2.2f, PlatterTop = TableY + .12f, ClocheOpen = 2f, ClocheBand = 1.6f;
        public const float ClocheRise = DaisY - PlatterTop;
        // R828 (the user: "은뚜껑을 들어올렸는데 전체가 올라오는건 이상하잖아" - a glove on the handle seemed to lift the whole
        // dish): what lifts them is shown. A dumbwaiter's frame stands over the table's north end - three slate posts (the
        // table's west edge, between the lifts, on the pantry wall) and a beam 16.6 m up - and each platter hangs on two
        // cables from lugs on its rim, each cloche on its own chain from its handle, over pulleys under the beam.
        public static readonly float[] GantryX = { TableX0 + .15f, (ClocheX[0] + ClocheX[1]) * .5f, TableX1 - .15f };
        public const float GantryY = 16.6f, CableX = 2.4f, PantryWallTop = 7.8f;

        // 5. The stage and its curtain. CurtainZ is where couple 0's queen stands still on the west straight 8.36 s after
        //    the dais (banquet_layout.py ALIGHT): the curtain's halves meet there.
        public const float CurtainZ = 40.05f, AlightAt = 8.36f, StageX1 = -20f, StageZ0 = CurtainZ - 11f, StageZ1 = CurtainZ + 9f;
        public const float CurtainX = -24.5f, PleatW = 1.05f, PleatBottom = StageY + .3f, RailY = StageY + 10.6f, CurtainOpenS = 16f;
        public const float CurtainShut = 6f, CurtainOpening = 5f, CurtainOpen = 2f, CurtainClosing = 2f;
        public const int Pleats = 10;
        public static readonly float[] BassZ = { 19f, StageZ0 - 2f };
        // R817: a pawn hangs about 0.9 m under its hands, so with its hands at 10.9 m (3.5 m up the curtain) its feet are at
        // 10.0: the basses' scrolls end at 9.6 so that one clears them and one a hand lower does not.
        public const float BassX = -23.95f, BassTop = 9.6f;

        // 6. The dessert buffet (south-west corner, at the stage's height) and the chocolate fountain on it: three tiers with
        //    rings a metre deep (R817). R828 (the user: "사다리를 초코분수에 넣으면 어떡해"; R825 had made the chocolate too
        //    fast to climb and put gilded ladders on the tiers): the chocolate flows down the whole of every tier, rim to foot,
        //    and is climbed against, as designed - 0.3 m/s down against a 1.2 m/s climb: a pawn takes 3.0 s and 38 % of its
        //    stamina a tier, a rook (0.7 of the climb speed) 5.0 s and 62 %, about R817's (3.0 s, 36 % · 65 %) that the user
        //    climbed. Ripples run down the whole of it to show the flow (ChocolateRipples). Only the last 0.45 m under a rim,
        //    where the hands are on the lip and the climb over it begins, does not pull - R817's slip at the rim stays fixed,
        //    and nothing of it is seen (R817's 1.3 m pale band was: "한 단에서 반은 초코가 흐르고 반은 안흐른다는게 상식선에서
        //    이해가 안 가").
        public const float BuffetX1 = -23.5f, BuffetZ0 = 3f, BuffetZ1 = 17f, BuffetY = StageY;
        public static readonly Vector2 Fountain = new Vector2(-27.7f, 9.2f);
        public const float BowlR = 4.2f, BowlTop = 7.9f, Flow = .3f, DomeFlow = 0f, StillBand = .45f;
        public static readonly float[] TierR = { 3.3f, 2.3f, 1.3f };
        public static readonly float[] TierTop = { 10.733f, 13.567f, 16.4f };
        public const int Ripples = 4;   // on each tier, 0.71 m apart

        // 7. The chandeliers. R817 (the user: "샹들리에 코스가 너무 김"): two lines of three from the loft's edge to the
        //    great chandelier in the middle of the hall - a wide still ring to stand on, get the breath back and a
        //    checkpoint - and two lines of two longer ones from it to the king's balcony (R825: the end). Each rope swings 40 degrees each
        //    way along the hall in 5 s; neighbours are half a swing apart, so their ends meet at the ends of their swings.
        //    Every handover is a plain pass (0.6 - 0.8 m, 0.5 m lower); the flying catch is gone.
        public static readonly float[] LinesX = { -2.6f, 2.6f };
        public const float RopeSwing = 40f, RopePeriod = 5f, GreatR = 4.2f, GreatY = 17.8f;
        public const int FirstLine = 3;   // the ropes before the great chandelier
        public static readonly float[] RopeL = { 6.5f, 6.5f, 6.5f, 8f, 8f };
        public static readonly float[] RopeLow = { LoftY + .55f, LoftY + .05f, LoftY - .45f, GreatY + .55f, GreatY + .05f };
        // Half a rope's travel along the hall (its end at the end of its swing, from under its pivot).
        public static float RopeEnd(int k) => RopeL[k] * Mathf.Sin(RopeSwing * Mathf.Deg2Rad);
        // The great chandelier's ring under each line: from its middle to its rim along a line 2.6 m off the middle.
        static readonly float GreatChord = Mathf.Sqrt(GreatR * GreatR - 2.6f * 2.6f);
        public static readonly float[] RopeAlong = Along();
        public static readonly float GreatZ = ScreenZ + RopeAlong[FirstLine - 1] + RopeEnd(FirstLine - 1) + .3f + GreatChord;

        // 8. The king's balcony, the end. R825 (the user: "마지막 샴페인 구간은 빼자. 샹들리에를 넘어가면 끝인거야"): the
        //    champagne tower is gone; the last ropes' ends come 0.8 m over the balcony's front, 0.95 m over its floor, and the
        //    exit is at its back. It stands on the dais between the two wings of the head table, its front a sheer face.
        public static readonly float LandZ = ScreenZ + RopeAlong[RopeAlong.Length - 1] + RopeEnd(RopeAlong.Length - 1);
        public static readonly float BalconyZ0 = LandZ - .8f;
        public const float BalconyY = 16f, BalconyX = 6f;
        public static readonly float TablesZ0 = BalconyZ0 + .9f;

        // Pivots along the hall from the loft's edge: the first line from 0.35 m off the loft, the second from 0.3 m off the
        // great chandelier's north rim.
        static float[] Along()
        {
            float[] gap = { .8f, .6f, 0f, .8f };
            var a = new float[RopeL.Length];
            a[0] = .35f + RopeEnd(0);
            for (int k = 1; k < RopeL.Length; k++)
            {
                if (k == FirstLine)
                {
                    float greatZ = ScreenZ + a[k - 1] + RopeEnd(k - 1) + .3f + GreatChord;
                    a[k] = greatZ + GreatChord + .3f - ScreenZ + RopeEnd(k);
                }
                else a[k] = a[k - 1] + RopeEnd(k - 1) + gap[k - 1] + RopeEnd(k);
            }
            return a;
        }

        // A rope's pivot height: its end at the end of its swing is RopeLow.
        public static float PivotY(int k) => RopeLow[k] + RopeL[k] * Mathf.Cos(RopeSwing * Mathf.Deg2Rad);

        // Grey-box colours over the clock tower's (orange climb, slate no, violet moves, green rest, red cue): people and
        // things that are only pictures, the cloth, chocolate, and the floors that end a run.
        static Material Figure => Arena.Lit("M_BanquetFigure", Arena.Hex("#9A9284"), 0f, .85f);
        static Material Cloth => Arena.Lit("M_BanquetCloth", Arena.Hex("#EFE8D6"), 0f, .85f);
        static Material Runner => Arena.Lit("M_BanquetRunner", Arena.Hex("#7F8C42"), 0f, .85f);
        // R825: the whole tier flows, so all of it is the dark, glossy chocolate; R828: with lighter ripples running down it.
        static Material ChocolateFlowing => Arena.Lit("M_BanquetChocolateFlowing", Arena.Hex("#3E2218"), 0f, .15f);
        static Material ChocolateRipple => Arena.Lit("M_BanquetChocolateRipple", Arena.Hex("#7A4830"), 0f, .35f);
        static Material FallFloor => Arena.Lit("M_BanquetFallFloor", Arena.Hex("#3E3746"), 0f, .7f);
        static Material Brass => Arena.Lit("M_BanquetBrass", Arena.Hex("#C9A240"), .1f, .4f);
        static Material Candle => Arena.Lit("M_BanquetCandle", Arena.Hex("#FFC86A"), 1.4f, .5f);
        // R817 (the user: "길을 모르겠음"): the way is laid with a red carpet and gold arrows, and two lamps say when to go.
        static Material Carpet => Arena.Lit("M_BanquetCarpet", Arena.Hex("#B3263A"), 0f, .9f);
        static Material Arrow => Arena.Lit("M_BanquetArrow", Arena.Hex("#FFD45A"), .7f, .5f);
        static Material LampOn => Arena.Lit("M_BanquetLampOn", Arena.Hex("#7CFF9A"), 2.2f, .4f);
        static Material LampOff => Arena.Lit("M_BanquetLampOff", Arena.Hex("#4A4636"), 0f, .6f);

        public static void Build(GrandBanquetHallSection hall)
        {
            var root = hall.transform;
            var old = root.Find(GrandBanquetHallSection.ContentName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var content = new GameObject(GrandBanquetHallSection.ContentName);
            content.SetActive(false);
            content.transform.SetParent(root, false);
            var b = new CourseBuilder(Look.Kit, content.transform);

            Shell(b);
            Servery(b);
            WaitersParade(b);
            LongTable(b);
            Cloches(b);
            Dais(b);
            RoyalBalcony(b);
            Waltz(b);
            StageAndCurtain(b);
            Buffet(b);
            Loft(b);
            Chandeliers(b);
            GreatChandelier(b);
            TheWay(b);
            Course(b);
            if (Application.isPlaying) content.AddComponent<GrandBanquetHallDressing>();   // B0 (R837): the Blender models over the graybox
            if (Application.isPlaying) content.AddComponent<GrandBanquetHallSound>();      // R844: the devices' and the hall's sounds

            hall.GetComponent<PawnRushSection>().Describe(GrandBanquetHallSection.Id, GrandBanquetHallSection.DisplayName,
                PawnRushSection.SlotKind.Obstacle, GrandBanquetHallSection.Entry, GrandBanquetHallSection.Exit, 0f, Beats());
            content.SetActive(true);   // the movers wake here with their pose on the clock
        }

        // ------------------------------------------------------------------ the shell, the screens

        static void Shell(CourseBuilder b)
        {
            Floor(b, "BH hall floor", HallX0, HallX1, 0f, HallZ1, 0f);
            Wall(b, "BH hall wall W", HallX0 - 1f, HallX0, 0f, Ceiling, 0f, HallZ1);
            Wall(b, "BH hall wall E", HallX1, HallX1 + 1f, 0f, Ceiling, 0f, HallZ1);
            Wall(b, "BH hall wall S-W", HallX0 - 1f, -6f, 0f, Ceiling, -1f, 0f);
            Wall(b, "BH hall wall S-E", 6f, HallX1 + 1f, 0f, Ceiling, -1f, 0f);
            Wall(b, "BH entry lintel", -6f, 6f, 7f, Ceiling, -1f, 0f);
            Wall(b, "BH hall wall N-W", HallX0 - 1f, -6f, 0f, Ceiling, HallZ1, HallZ1 + 1f);
            Wall(b, "BH hall wall N-E", 6f, HallX1 + 1f, 0f, Ceiling, HallZ1, HallZ1 + 1f);
            Wall(b, "BH hall wall N under the balcony", -6f, 6f, 0f, BalconyY - .6f, HallZ1, HallZ1 + 1f);
            Wall(b, "BH exit lintel", -6f, 6f, BalconyY + 8f, Ceiling, HallZ1, HallZ1 + 1f);

            // The screens: the passage from the doorway along the south wall is shut off from the hall, and they turn north
            // round the servery to the seated guests. Nothing from the passage reaches the dance floor.
            Wall(b, "BH screen S", BuffetX1, 13.2f, 0f, 6f, ScreenZ - .2f, ScreenZ + .2f);
            Wall(b, "BH screen E", 12.8f, 13.2f, 0f, 6f, ScreenZ + .2f, TableZ0);
            Paint(b, "BH screen cap S", V(BuffetX1, 6f, ScreenZ - .3f), V(13.3f, 6.3f, ScreenZ + .3f), Brass);
            // R817: the passage goes one way, east to the servery; its west end (nothing there) is shut at the doorway.
            Wall(b, "BH passage end", -6.4f, -6f, 0f, 6f, 0f, ScreenZ - .2f);

            var start = new GameObject("Start").transform;   // where the map test drops the pawn
            start.SetParent(b.parent, false);
            start.localPosition = V(0f, 0f, 3f);
            start.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }

        // ------------------------------------------------------------------ the servery, the kitchen and the pantry

        static void Servery(CourseBuilder b)
        {
            Wall(b, "BH counter", CounterX0, CounterX1, 0f, TrayY, CounterZ0, CounterZ1).AddComponent<CourseFloor>();
            b.RampX("BH counter ramp", CounterZ0 + 1f, CounterZ1 - 1f, CounterX0 - 7f, CounterX0, 0f, TrayY, 1f);
            for (int i = 0; i < 3; i++)
                YardKit.Cylinder(b.parent, "BH spare plates", V(23f + i * 1.1f, TrayY + .4f, 5.2f), Vector3.up, 1f, .8f, Cloth, YardKit.Solid.None);

            // The kitchen in the south-east corner: the waiters come out of its doorway onto the lane.
            Wall(b, "BH kitchen wall", 25.7f, 26f, 0f, 7.8f, 0f, KitchenZ1);
            Wall(b, "BH kitchen roof", 25.7f, HallX1, 7.5f, 7.8f, 0f, KitchenZ1);
            Paint(b, "BH kitchen lintel", V(26f, 7.2f, KitchenZ1 - .3f), V(HallX1, 7.5f, KitchenZ1), Look.NoClimb);

            // The pantry beside the table's north end: they go in at its doorway and are back in the kitchen (a cut).
            Wall(b, "BH pantry post", 31.7f, HallX1, 0f, 7.5f, PantryZ0, PantryZ0 + .3f);
            Wall(b, "BH pantry lintel", 26f, HallX1, 7.2f, 7.8f, PantryZ0, PantryZ0 + .3f);
            Wall(b, "BH pantry wall W", 25.7f, 26f, TableY, 7.8f, PantryZ0, DaisZ);
            Wall(b, "BH pantry roof", 25.7f, HallX1, 7.5f, 7.8f, PantryZ0, DaisZ);
        }

        // ------------------------------------------------------------------ 1. the waiters' parade

        // A waiter: his tray (stood on, 2.4 m, level with the counter) with the plate stack on it (climbed, 2.6 m, its top
        // stood on), and his body beside it (not climbed). The tray passes 0.3 m from the counter every 4.5 s.
        static void WaitersParade(CourseBuilder b)
        {
            float gap = WaiterLane / Waiters, body = WaiterBodyX - TrayX;
            for (int k = 0; k < Waiters; k++)
            {
                var w = Root(b, "BH waiter " + (k + 1), V(TrayX, 0f, WaiterZ0 + k * gap));   // where the clock's 0 has him
                YardKit.Cylinder(w, "BH tray", V(0f, TrayY - .075f, 0f), Vector3.up, TrayR * 2f, .15f, Look.Mover, YardKit.Solid.Solid)
                       .AddComponent<CourseFloor>();
                YardKit.Cylinder(w, "BH plate stack", V(0f, (TrayY + StackTop) * .5f, 0f), Vector3.up, StackR * 2f, StackTop - TrayY,
                                 Look.Mover, YardKit.Solid.Solid).AddComponent<CourseFloor>();
                YardKit.Cylinder(w, "BH waiter body", V(body, 2.7f, 0f), Vector3.up, 2f, 5.4f, Look.NoClimb, YardKit.Solid.NoClimb);
                YardKit.Ball(w, "BH waiter head", V(body, 6.05f, 0f), 1.2f, Figure, YardKit.Solid.None);
                YardKit.Slab(w, "BH waiter arm", V(body - .6f, 4.6f, 0f), V(.3f, TrayY - .05f, 0f), .3f, .3f, Figure, YardKit.Solid.None);
                w.gameObject.AddComponent<BanquetWaiter>().Configure(WaiterLane, WaiterSpeed, k * gap);
            }
        }

        // ------------------------------------------------------------------ 2. the long table and the tablecloth trick

        static void LongTable(CourseBuilder b)
        {
            Wall(b, "BH long table", TableX0, TableX1, 0f, TableY, TableZ0, DaisZ).AddComponent<CourseFloor>();
            // The guests seated along its inner edge, backs to the dance floor: their chairs shut the strip under the table's
            // west edge off from the servery.
            Wall(b, "BH guests' chairs", 11.6f, TableX0, 0f, 2.2f, TableZ0, 54.5f);
            for (int i = 0; i < 9; i++)
            {
                float z = 21f + 4f * i;
                YardKit.Cylinder(b.parent, "BH guest " + (i + 1), V(12.8f, 2.3f, z), Vector3.up, 2.2f, 4.6f, Figure, YardKit.Solid.NoClimb);
                YardKit.Ball(b.parent, "BH guest head " + (i + 1), V(12.8f, 5.3f, z), 1.2f, Figure, YardKit.Solid.None);
            }

            var plates = new Vector2[PlateZ.Length];
            for (int i = 0; i < PlateZ.Length; i++)
            {
                YardKit.Cylinder(b.parent, "BH plate " + (i + 1), V(RunnerX, TableY + .06f, PlateZ[i]), Vector3.up, PlateR * 2f, .12f,
                                 Look.Rest, YardKit.Solid.Solid).AddComponent<CourseFloor>();
                plates[i] = new Vector2(RunnerX, PlateZ[i]);
            }

            // The cloth (a picture pivoted on its south end, at the magician's hands) and the trick on the section's frame.
            var trick = b.Group("BH tablecloth trick");
            var cloth = b.Group("BH tablecloth", trick);
            float mid = (TableX0 + TableX1) * .5f, length = ClothZ1 - TableZ0;
            cloth.localPosition = V(mid, TableY + .005f, TableZ0);
            YardKit.Box(cloth, "BH cloth", V(0f, .02f, length * .5f), V(TableX1 - TableX0 + .3f, .04f, length), Cloth, YardKit.Solid.None);
            YardKit.Box(cloth, "BH runner", V(RunnerX - mid, .045f, length * .5f), V(5f, .02f, length - 2f), Runner, YardKit.Solid.None);
            trick.gameObject.AddComponent<TableclothTrick>().Configure(V(TableX0, TableY, TableZ0), V(TableX1, TableY, ClothZ1), plates, PlateR,
                                                                       LandingZ, cloth, Cloth, Look.Comb);

            // The magician at the south end, on the floor, holding the cloth's end (a picture: the thrown fly over him).
            var mage = Root(b, "BH magician", V(RunnerX, 0f, 15.2f));
            YardKit.Cylinder(mage, "BH magician robe", V(0f, 3.2f, 0f), Vector3.up, 2.6f, 6.4f, Figure, YardKit.Solid.None);
            YardKit.Ball(mage, "BH magician head", V(0f, 7.1f, 0f), 1.4f, Figure, YardKit.Solid.None);
            YardKit.Cylinder(mage, "BH magician hat", V(0f, 8.5f, 0f), Vector3.up, 1.3f, 1.6f, Look.NoClimb, YardKit.Solid.None);
            foreach (float s in new[] { -1f, 1f })
                YardKit.Slab(mage, "BH magician arm", V(s * .9f, 5.8f, .3f), V(s * 2.6f, TableY + .2f, TableZ0 - 15.3f), .45f, .45f, Figure, YardKit.Solid.None);
        }

        // ------------------------------------------------------------------ 3. the cloche lifts

        // A cloche lift (ClocheLift, R825): the platter (a floor 4.4 m across, 0.12 m over the table; at the top level with
        // the dais and 0.1 m off its front) and over it the cloche - a hollow dome whose band is a ring of slate pieces
        // reaching down to the platter when shut, open 2 m over it to walk under. Nothing of it is climbed. R828: it hangs
        // from the dumbwaiter's frame (DumbwaiterFrame) - the platter on two cables from brass lugs outside the band, the
        // cloche on a chain from its handle (HoistLine, pictures); the glove and the sleeve that lifted it are gone.
        static void Cloches(CourseBuilder b)
        {
            const int ring = 20;
            for (int i = 0; i < ClocheX.Length; i++)
            {
                float x = ClocheX[i], delay = i * .5f * 12.4f;
                string side = x < 20f ? "W" : "E";
                var platter = Root(b, "BH cloche platter " + side, V(x, PlatterTop, ClocheZ));
                YardKit.Cylinder(platter, "BH platter", V(0f, -.06f, 0f), Vector3.up, ClocheR * 2f, .12f, Look.Mover, YardKit.Solid.Solid)
                       .AddComponent<CourseFloor>();
                platter.gameObject.AddComponent<ClocheLift>().Configure(ClocheLift.Part.Platter, ClocheRise, ClocheOpen, delay);

                var c = Root(b, "BH cloche " + side, V(x, PlatterTop + ClocheOpen, ClocheZ));
                float chord = 2f * ClocheR * Mathf.Sin(Mathf.PI / ring) + .08f;
                for (int k = 0; k < ring; k++)
                {
                    var turn = Quaternion.Euler(0f, k * 360f / ring, 0f);
                    YardKit.Box(c, "BH cloche band", turn * V(0f, 0f, ClocheR - .06f) + V(0f, ClocheBand * .5f, 0f), V(chord, ClocheBand, .12f),
                                Look.NoClimb, YardKit.Solid.NoClimb, turn);
                }
                YardKit.Cylinder(c, "BH cloche lid", V(0f, ClocheBand + .1f, 0f), Vector3.up, ClocheR * 2f + .1f, .2f, Look.NoClimb, YardKit.Solid.NoClimb);
                YardKit.Ball(c, "BH cloche dome", V(0f, ClocheBand + .2f, 0f), ClocheR * 2f, Look.NoClimb, YardKit.Solid.None).transform.localScale =
                    V(ClocheR * 2f, 1.6f, ClocheR * 2f);
                YardKit.Ball(c, "BH cloche handle", V(0f, ClocheBand + 1.05f, 0f), .6f, Brass, YardKit.Solid.None);
                c.gameObject.AddComponent<ClocheLift>().Configure(ClocheLift.Part.Cloche, ClocheRise, ClocheOpen, delay);

                // What it hangs from: a cable from each lug (east and west, clear of the band) and the chain from the handle's top.
                foreach (float s in new[] { -1f, 1f })
                {
                    YardKit.Box(platter, "BH platter lug", V(s * (ClocheR + .18f), 0f, 0f), V(.36f, .14f, .3f), Brass, YardKit.Solid.None);
                    Hoist(b, "BH platter cable " + side + (s < 0f ? "1" : "2"), platter, V(s * CableX, .07f, 0f), V(x + s * CableX, GantryY - .2f, ClocheZ),
                          .07f, YardKit.Iron);
                }
                Hoist(b, "BH cloche chain " + side, c, V(0f, ClocheBand + 1.35f, 0f), V(x, GantryY - .2f, ClocheZ), .12f, Brass);
            }
            DumbwaiterFrame(b);
        }

        // R828: the dumbwaiter's frame over the table's north end, on the platters' east - west line. Three slate posts, not
        // climbed (on the table's west edge, between the lifts, on the pantry's wall over the table's east edge), a beam
        // across them 16.6 m up - over the cloche's handle when it is open at the dais (15.35 m) - and under the beam a brass
        // pulley over every cable and chain. The posts stand clear of the platters (0.5 m) and of the way on and off them.
        static void DumbwaiterFrame(CourseBuilder b)
        {
            const float post = .3f;
            foreach (float x in GantryX)
            {
                float foot = x > TableX1 - 1f ? PantryWallTop : TableY;
                Wall(b, "BH dumbwaiter post", x - post * .5f, x + post * .5f, foot, GantryY, ClocheZ - post * .5f, ClocheZ + post * .5f);
                Paint(b, "BH dumbwaiter post cap", V(x - .25f, GantryY + .35f, ClocheZ - .3f), V(x + .25f, GantryY + .5f, ClocheZ + .3f), Brass);
            }
            Wall(b, "BH dumbwaiter beam", TableX0, TableX1, GantryY, GantryY + .35f, ClocheZ - .25f, ClocheZ + .25f);
            foreach (float cx in ClocheX)
                foreach (float s in new[] { -1f, 0f, 1f })
                    YardKit.Cylinder(b.parent, "BH dumbwaiter pulley", V(cx + s * CableX, GantryY - .2f, ClocheZ), Vector3.forward, .42f, .12f,
                                     Brass, YardKit.Solid.None);
        }

        // A hoist line (HoistLine): built at its pulley (section frame), stretched every frame down to `hook` on `load`.
        static void Hoist(CourseBuilder b, string name, Transform load, Vector3 hook, Vector3 pulley, float thickness, Material material)
        {
            var line = YardKit.Cylinder(b.parent, name, pulley, Vector3.up, thickness, 1f, material, YardKit.Solid.None);
            line.AddComponent<HoistLine>().Configure(load, hook, thickness);
        }

        // ------------------------------------------------------------------ the royal dais and the head table

        // The dais's front is a sheer marble face (its stairs are behind, for the court): the cloche lifts are the way up.
        // R817 (the user: "쓸데없는 길도 너무 많음", "왈츠를 타기까지 유도가 잘 안됨"): in front of the head table the dais is
        // a gallery from the cloche lifts to the boarding place and no further - walled at both ends - and its edge has a
        // balustrade with two openings: where the lifts come up, and where a queen stands still (a lamp on each side of it
        // lights as one is about to). R825: the head table's two wings stand either side of the king's balcony.
        static void Dais(CourseBuilder b)
        {
            Wall(b, "BH royal dais", HallX0, HallX1, 0f, DaisY, DaisZ, HallZ1).AddComponent<CourseFloor>();
            Paint(b, "BH dais edge", V(HallX0, DaisY - .4f, DaisZ - .12f), V(HallX1, DaisY + .02f, DaisZ), Brass);
            float tables = TablesZ0;
            Wall(b, "BH head table W", -24f, -BalconyX, DaisY, HeadY, tables, tables + 6f).AddComponent<CourseFloor>();
            Wall(b, "BH head table E", BalconyX, 24f, DaisY, HeadY, tables, tables + 6f).AddComponent<CourseFloor>();

            float board = BanquetWaltz.BoardX, opening = 1.35f;
            Wall(b, "BH balustrade", HallX0, board - opening, DaisY, DaisY + 1.1f, DaisZ, DaisZ + .3f);
            Wall(b, "BH balustrade", board + opening, ClocheX[0] - ClocheR - .4f, DaisY, DaisY + 1.1f, DaisZ, DaisZ + .3f);
            Wall(b, "BH balustrade", ClocheX[1] + ClocheR + .4f, HallX1, DaisY, DaisY + 1.1f, DaisZ, DaisZ + .3f);
            Wall(b, "BH gallery end W", board - opening - 1.7f, board - opening - 1.4f, DaisY, DaisY + 2.6f, DaisZ + .3f, tables);
            Wall(b, "BH gallery end E", 26f, 26.3f, DaisY, DaisY + 2.6f, DaisZ + .3f, tables + .3f);
            Wall(b, "BH gallery end NE", 24f, 26f, DaisY, DaisY + 2.6f, tables, tables + .3f);
            foreach (float s in new[] { -1f, 1f })
            {
                var post = Root(b, "BH boarding lamp " + (s < 0f ? "W" : "E"), V(board + s * (opening + .15f), DaisY, DaisZ + .15f));
                YardKit.Box(post, "BH boarding post", V(0f, .9f, 0f), V(.3f, 1.8f, .3f), Brass, YardKit.Solid.NoClimb);
                YardKit.Ball(post, "BH boarding lamp globe", V(0f, 2.1f, 0f), .6f, LampOff, YardKit.Solid.None);
                post.Find("BH boarding lamp globe").gameObject.AddComponent<ClockLamp>()
                    .Configure(BanquetWaltz.Stop / BanquetWaltz.Speed, 0f, -1.2f, .4f, LampOn, LampOff);
            }
            for (int i = 0; i < 6; i++)
            {
                float x = (i < 3 ? -21f : 9f) + 6f * (i % 3);
                YardKit.Cylinder(b.parent, "BH head table candle", V(x, HeadY + .9f, tables + 1.2f), Vector3.up, .25f, 1.8f, Cloth, YardKit.Solid.None);
                YardKit.Ball(b.parent, "BH head table flame", V(x, HeadY + 1.95f, tables + 1.2f), .3f, Candle, YardKit.Solid.None);
            }
        }

        // ------------------------------------------------------------------ 8. the king's balcony (the end)

        // R825: a block on the dais between the head table's wings, its top 16 m, its front a sheer face 0.8 m inside the
        // last ropes' reach (a fall short of it is caught over the gallery - Course). The exit is at its back.
        static void RoyalBalcony(CourseBuilder b)
        {
            Wall(b, "BH balcony", -BalconyX, BalconyX, DaisY, BalconyY, BalconyZ0, HallZ1).AddComponent<CourseFloor>();
            Paint(b, "BH balcony edge", V(-BalconyX, BalconyY - .4f, BalconyZ0 - .12f), V(BalconyX, BalconyY + .02f, BalconyZ0), Brass);
            Wall(b, "BH balcony rail W", -BalconyX - .3f, -BalconyX, BalconyY, BalconyY + 1.1f, BalconyZ0, HallZ1);
            Wall(b, "BH balcony rail E", BalconyX, BalconyX + .3f, BalconyY, BalconyY + 1.1f, BalconyZ0, HallZ1);
            foreach (float s in new[] { -1f, 1f })
                Paint(b, "BH throne", V(s * 4.2f - .9f, BalconyY, HallZ1 - 2.6f), V(s * 4.2f + .9f, BalconyY + 3.6f, HallZ1 - 1.2f), Brass);
            Paint(b, "BH canopy", V(-5f, BalconyY + 7.8f, HallZ1 - 3f), V(5f, BalconyY + 8.6f, HallZ1), Look.Comb);
        }

        // ------------------------------------------------------------------ 4. the waltz

        // The dance floor ends a run (KillVolume in Course): nobody walks it, the couples carry. Each couple: the queen's gown
        // (climbed; its top, the shoulders, stood on round her head) and the king (not climbed), joined by their arms.
        static void Waltz(CourseBuilder b)
        {
            Paint(b, "BH dance floor", V(HallX0, 0f, ScreenZ + .2f), V(11.6f, .02f, DaisZ), FallFloor);
            for (int i = 0; i < BanquetWaltz.Couples; i++)
            {
                BanquetWaltz.Pose(i, 0.0, out var middle, out float bearing);
                var c = Root(b, "BH couple " + (i + 1), V(middle.x, 0f, middle.y));
                c.localRotation = Quaternion.Euler(0f, -bearing * Mathf.Rad2Deg, 0f);
                float p = BanquetWaltz.Pair;
                YardKit.Cylinder(c, "BH queen gown", V(p, StageY * .5f, 0f), Vector3.up, 2.6f, StageY, Look.Mover, YardKit.Solid.Solid)
                       .AddComponent<CourseFloor>();
                YardKit.Ball(c, "BH queen head", V(p, StageY + .6f, 0f), 1.2f, Look.NoClimb, YardKit.Solid.NoClimb);
                YardKit.Cylinder(c, "BH queen crown", V(p, StageY + 1.45f, 0f), Vector3.up, .75f, .5f, Brass, YardKit.Solid.None);
                YardKit.Cylinder(c, "BH king", V(-p, StageY * .5f, 0f), Vector3.up, 2.6f, StageY, Look.NoClimb, YardKit.Solid.NoClimb);
                YardKit.Ball(c, "BH king head", V(-p, StageY + .6f, 0f), 1.2f, Look.NoClimb, YardKit.Solid.NoClimb);
                YardKit.Cylinder(c, "BH king crown", V(-p, StageY + 1.65f, 0f), Vector3.up, .75f, .9f, Brass, YardKit.Solid.None);
                YardKit.Box(c, "BH joined arms", V(0f, StageY - .5f, 0f), V(2f * p - 2.2f, .4f, .4f), Figure, YardKit.Solid.None);
                c.gameObject.AddComponent<WaltzCouple>().Configure(i);
            }
        }

        // ------------------------------------------------------------------ 5. the stage, the curtain, the orchestra pit

        static void StageAndCurtain(CourseBuilder b)
        {
            Wall(b, "BH stage", HallX0, StageX1, 0f, StageY, StageZ0, StageZ1).AddComponent<CourseFloor>();
            Paint(b, "BH curtain rail", V(CurtainX - .15f, RailY, CurtainOpenS - 3.5f), V(CurtainX + .15f, RailY + .3f, StageZ1 + 2f), Brass);
            Paint(b, "BH pelmet", V(CurtainX - .5f, RailY + .3f, CurtainOpenS - 3.5f), V(CurtainX + .5f, RailY + 1.9f, StageZ1 + 2f), Look.Comb);
            // Twenty pleats in one plane, each a moving wall to climb. Shut, the halves meet at CurtainZ; open, the south half
            // is bunched over the buffet's north end and the north half at the stage's north end.
            foreach (int side in new[] { -1, 1 })
                for (int k = 0; k < Pleats; k++)
                {
                    float shut = CurtainZ + side * (.55f + PleatW * k);
                    float open = side > 0 ? StageZ1 + .5f - .3f * k : CurtainOpenS - .3f * k;
                    var p = Root(b, "BH pleat " + (side < 0 ? "S" : "N") + (k + 1), V(CurtainX, 0f, shut));
                    YardKit.Box(p, "BH curtain", V(0f, (PleatBottom + RailY) * .5f, 0f), V(.24f, RailY - PleatBottom, PleatW), Look.Mover);
                    p.gameObject.AddComponent<CurtainPleat>().Configure(open - shut, CurtainShut, CurtainOpening, CurtainOpen, CurtainClosing, 0f);
                }
            // R817: the place where a queen sets a pawn down, lit as she is about to stand still in front of it.
            foreach (float s in new[] { -1f, 1f })
            {
                var post = Root(b, "BH alighting lamp " + (s < 0f ? "S" : "N"), V(StageX1 - .2f, StageY, CurtainZ + s * 1.7f));
                YardKit.Box(post, "BH alighting post", V(0f, .9f, 0f), V(.3f, 1.8f, .3f), Brass, YardKit.Solid.NoClimb);
                YardKit.Ball(post, "BH alighting lamp globe", V(0f, 2.1f, 0f), .6f, LampOff, YardKit.Solid.None);
                post.Find("BH alighting lamp globe").gameObject.AddComponent<ClockLamp>()
                    .Configure(BanquetWaltz.Stop / BanquetWaltz.Speed, AlightAt, -1.2f, .4f, LampOn, LampOff);
            }
            // The orchestra behind the curtain (pictures).
            foreach (var at in new[] { V(-29f, StageY, StageZ0 + 4f), V(-29f, StageY, StageZ0 + 9f), V(-27f, StageY, StageZ0 + 14f), V(-29f, StageY, StageZ1 - 3f) })
            {
                YardKit.Cylinder(b.parent, "BH musician", at + V(0f, 1.8f, 0f), Vector3.up, 1.6f, 3.6f, Figure, YardKit.Solid.None);
                YardKit.Ball(b.parent, "BH musician head", at + V(0f, 4.1f, 0f), 1f, Figure, YardKit.Solid.None);
            }
            // The double basses in the pit, against the curtain's line: a pawn hanging lower than 10.6 m is knocked off.
            foreach (float z in BassZ)
            {
                Wall(b, "BH double bass", BassX - .35f, BassX + .35f, 0f, 7f, z - .8f, z + .8f);
                Wall(b, "BH bass neck", BassX - .15f, BassX + .15f, 7f, BassTop, z - .15f, z + .15f);
                YardKit.Cylinder(b.parent, "BH bassist", V(-21.4f, 2.2f, z), Vector3.up, 1.6f, 4.4f, Figure, YardKit.Solid.None);
            }
        }

        // ------------------------------------------------------------------ 6. the dessert buffet and the chocolate fountain

        // The buffet's east edge is 4.1 m from a queen's shoulders at her stop beside it: too far to jump. Its skirts are not
        // climbed. On it the fountain: a bowl (a 0.5 m step) and three tiers of chocolate flowing down from rim to foot, each
        // climbed against the flow (R828), ripples running down the whole of it; their tops (rings a metre deep) are still.
        static void Buffet(CourseBuilder b)
        {
            Wall(b, "BH dessert buffet", HallX0, BuffetX1, 0f, BuffetY, BuffetZ0, BuffetZ1).AddComponent<CourseFloor>();
            var f = Fountain;
            YardKit.Cylinder(b.parent, "BH fountain bowl", V(f.x, (BuffetY + BowlTop) * .5f, f.y), Vector3.up, BowlR * 2f, BowlTop - BuffetY,
                             Brass, YardKit.Solid.Solid).AddComponent<CourseFloor>();
            float below = BowlTop;
            for (int k = 0; k < TierR.Length; k++)
            {
                var tier = YardKit.Cylinder(b.parent, "BH chocolate tier " + (k + 1), V(f.x, (below + TierTop[k]) * .5f, f.y), Vector3.up,
                                            TierR[k] * 2f, TierTop[k] - below, ChocolateFlowing, YardKit.Solid.Solid);
                tier.AddComponent<CourseFloor>();
                tier.AddComponent<ChocolateFlow>().Configure(Flow, DomeFlow, StillBand);
                // Its ripples (pictures, ChocolateRipples): on its axis at its foot.
                var ripples = Root(b, "BH chocolate ripples " + (k + 1), V(f.x, below, f.y));
                var rings = new Transform[Ripples];
                for (int i = 0; i < Ripples; i++)
                    rings[i] = YardKit.Cylinder(ripples, "BH chocolate ripple", Vector3.zero, Vector3.up, TierR[k] * 2f, 2f * ChocolateRipples.Half, ChocolateRipple,
                                                YardKit.Solid.None).transform;
                ripples.gameObject.AddComponent<ChocolateRipples>().Configure(TierTop[k] - below, TierR[k], Flow, rings);
                below = TierTop[k];
            }
            float top = TierTop[TierTop.Length - 1];
            YardKit.Cylinder(b.parent, "BH fountain crown", V(f.x, top + .6f, f.y), Vector3.up, .7f, 1.2f, Look.NoClimb, YardKit.Solid.NoClimb);
            YardKit.Cone(b.parent, "BH fountain finial", V(f.x, top + 1.2f, f.y), 1.2f, 1f, Brass);
            for (int i = 0; i < 6; i++)
            {
                float a = (30f + 60f * i) * Mathf.Deg2Rad;
                var at = V(f.x + 5.2f * Mathf.Sin(a), BuffetY + .45f, f.y + 5.2f * Mathf.Cos(a));
                if (at.x < BuffetX1 - .6f && at.z > BuffetZ0 + .6f && at.z < BuffetZ1 - .6f)
                    YardKit.Ball(b.parent, "BH strawberry", at, .9f, Look.Comb, YardKit.Solid.None);
            }
        }

        // ------------------------------------------------------------------ the lamplighters' loft

        // Over the screens passage, 20 m up. Its face to the hall is not climbed; a lamplighter's ladder hangs from it in
        // front of the fountain (R825: "벽을 탈 수 있구나라는게 바로 생각이 안나" - the user did not see the face could be
        // climbed): 1.6 m from the fountain's top, its foot 1 m under it. Its rail leaves the ladder and the two rope lanes
        // open. R817: it ends past the second rope lane (it ran the hall's width, 27 m of it going nowhere). R828: it is the
        // hall's only ladder (the fountain's went: "사다리를 초코분수에 넣으면 어떡해"); its board stands LadderOut proud.
        public const float LoftX0 = -31f, LoftX1 = 4.6f, LadderOut = .3f;

        static void Loft(CourseBuilder b)
        {
            Wall(b, "BH loft", LoftX0, LoftX1, LoftY - .6f, LoftY, 0f, ScreenZ - .4f).AddComponent<CourseFloor>();
            Wall(b, "BH loft face", LoftX0, LoftX1, LoftY - 4f, LoftY, ScreenZ - .4f, ScreenZ).AddComponent<CourseFloor>();
            Ladder(b, "BH loft ladder", V(Fountain.x, 0f, ScreenZ), Vector3.forward, TierTop[TierTop.Length - 1] - 1f, LoftY, 1.6f);
            Wall(b, "BH loft end", LoftX1, LoftX1 + .3f, LoftY - 4f, LoftY + 3f, 0f, ScreenZ);
            foreach (var x in new[] { V(-24f, 0f, -3.6f), V(-1.6f, 0f, 1.6f), V(3.6f, 0f, LoftX1) })
                Wall(b, "BH loft rail", x.x, x.z, LoftY, LoftY + 1.1f, ScreenZ - .25f, ScreenZ);
        }

        // A ladder up a face (R825; R828: only the loft's): a climbable board LadderOut deep standing out of the face at
        // `foot` (the face's point on the hall floor's level; x, z), facing `outward`, from y0 to y1, `width` across - orange,
        // as every face to climb - with gilded rails and rungs on it (pictures). Its top is level with the floor over the
        // face, so the climb ends on it.
        static void Ladder(CourseBuilder b, string name, Vector3 foot, Vector3 outward, float y0, float y1, float width)
        {
            var turn = Quaternion.LookRotation(outward, Vector3.up);
            float h = y1 - y0, depth = LadderOut + .1f;
            var at = Root(b, name, V(foot.x, y0, foot.z) + outward * (depth * .5f - .1f));
            at.localRotation = turn;
            YardKit.Box(at, name + " board", V(0f, h * .5f, 0f), V(width, h, depth), Look.Climb);
            foreach (float s in new[] { -1f, 1f })
                YardKit.Box(at, name + " rail", V(s * (width * .5f - .06f), h * .5f, depth * .5f + .03f), V(.1f, h, .08f), Brass, YardKit.Solid.None);
            for (float y = .3f; y < h - .1f; y += .4f)
                YardKit.Box(at, name + " rung", V(0f, y, depth * .5f + .04f), V(width - .12f, .07f, .08f), Brass, YardKit.Solid.None);
        }

        // ------------------------------------------------------------------ 7. the chandeliers

        // R818 (the user: "샹들리에가 샹들리에처럼 안생겼어 … 디자인적으로 신경써서"): each rope is a gilded crown chandelier
        // on its chain (BanquetChandelierShape.Hanging - the hook, the chain, the crown cap, fanned chains, the stem, an
        // upper and a main ring of candles, crystal drops, and at the rope's end the hoop a pawn hangs from), turned with
        // the rope; above its pivot a still rod to a rose on the ceiling, with a swivel at the pivot. The rope draws
        // nothing itself.
        static void Chandeliers(CourseBuilder b)
        {
            for (int lane = 0; lane < LinesX.Length; lane++)
                for (int k = 0; k < RopeAlong.Length; k++)
                {
                    float x = LinesX[lane], z = ScreenZ + RopeAlong[k], y = PivotY(k);
                    var pivot = Root(b, "BH chandelier " + (lane == 0 ? "W" : "E") + (k + 1), V(x, y, z));
                    var rope = pivot.gameObject.AddComponent<RopeLine>();
                    rope.Configure(RopeL[k], RopeSwing, RopePeriod, ((k + lane) % 2) * RopePeriod * .5f, 0f, null);
                    var look = b.Group("BH chandelier look", pivot);
                    Picture(look, "BH chandelier", BanquetChandelierShape.Hanging(RopeL[k]));
                    look.gameObject.AddComponent<ChandelierLook>().Configure(rope);
                    YardKit.Cylinder(b.parent, "BH chandelier rod", V(x, (y + Ceiling) * .5f, z), Vector3.up, .09f, Ceiling - y, Brass, YardKit.Solid.None);
                    YardKit.Ball(b.parent, "BH chandelier swivel", V(x, y + .02f, z), .24f, Brass, YardKit.Solid.None);
                    YardKit.Cylinder(b.parent, "BH ceiling rose", V(x, Ceiling - .12f, z), Vector3.up, 1.1f, .24f, Brass, YardKit.Solid.None);
                }
        }

        // The great chandelier in the middle of the hall, between the two lines of ropes: a still gilded corona 8.4 m
        // across hung from the ceiling (BanquetChandelierShape.Great: a dish with a lip of candles, a column with S-arms
        // and a crown, chains to the lip), its dish's top a floor with a green carpet (a place to rest) and a checkpoint.
        // The first line's last ropes swing their ends 0.3 m short of its south rim (1.7 m over it); the second line's
        // first ropes come 0.3 m off its north rim, 0.55 m over it. Its floor and column are plain colliders under the
        // picture.
        static void GreatChandelier(CourseBuilder b)
        {
            var g = Root(b, "BH great chandelier", V(0f, GreatY, GreatZ));
            var floor = YardKit.Cylinder(g, "BH great chandelier floor", V(0f, -.15f, 0f), Vector3.up, GreatR * 2f, .3f, Look.Rest, YardKit.Solid.Solid);
            floor.AddComponent<CourseFloor>();
            Object.DestroyImmediate(floor.GetComponent<MeshRenderer>());
            var column = YardKit.Cylinder(g, "BH great chandelier column", V(0f, 1.5f, 0f), Vector3.up, BanquetChandelierShape.GreatColumn * 2f, 3f,
                                          Look.NoClimb, YardKit.Solid.NoClimb);
            Object.DestroyImmediate(column.GetComponent<MeshRenderer>());
            YardKit.Cylinder(g, "BH great chandelier carpet", V(0f, .008f, 0f), Vector3.up, GreatR * 2f - .5f, .016f, Look.Rest, YardKit.Solid.None);
            Picture(g, "BH great chandelier body", BanquetChandelierShape.Great(Ceiling - GreatY - BanquetChandelierShape.GreatColumnTop));
        }

        static Material Crystal => Arena.Lit("M_BanquetCrystal", Arena.Hex("#DDEEFF"), .5f, .12f);

        // A chandelier's picture: its mesh with brass, wax, flame and crystal (BanquetChandelierShape's four parts).
        static void Picture(Transform parent, string name, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { Brass, Cloth, Candle, Crystal };
        }

        // ------------------------------------------------------------------ the way (R817)

        // A red carpet on every floor the way crosses and gold arrows on it every 3 m, pointing on (pictures, 1 cm over the
        // floor). The user's first run: "길을 모르겠음 (쓸데없는 길도 너무 많음) - 가장 큰 문제".
        static void TheWay(CourseBuilder b)
        {
            var at = b.Group("BH the way");
            void Lay(float y, params Vector2[] points)
            {
                for (int i = 1; i < points.Length; i++)
                {
                    Vector3 from = V(points[i - 1].x, y, points[i - 1].y), to = V(points[i].x, y, points[i].y), d = to - from;
                    float length = d.magnitude;
                    if (length < .05f) continue;
                    var turn = Quaternion.LookRotation(d / length, Vector3.up);
                    YardKit.Box(at, "BH carpet", (from + to) * .5f + V(0f, .01f, 0f), V(1.6f, .02f, length + 1.6f), Carpet, YardKit.Solid.None, turn);
                    for (float s = 1.5f; s < length - .5f; s += 3f)
                        Chevron(at, from + d / length * s + V(0f, .03f, 0f), turn);
                }
            }
            Vector2 P(float x, float z) => new Vector2(x, z);
            Lay(0f, P(0f, 1.2f), P(0f, 3f), P(15f, 3f), P(15f, 8f));
            Lay(TrayY, P(CounterX0 + .3f, 8f), P(CounterX1 - .6f, 8f));
            Lay(DaisY, P((ClocheX[0] + ClocheX[1]) * .5f, DaisZ + 1.3f), P(BanquetWaltz.BoardX, DaisZ + 1.3f), P(BanquetWaltz.BoardX, DaisZ + .9f));
            Lay(StageY, P(StageX1 - .9f, CurtainZ), P(CurtainX + 1.2f, CurtainZ - .5f));
            Lay(BuffetY, P(CurtainX + .3f, CurtainOpenS - .3f), P(Fountain.x, BuffetZ1 - 1.4f), P(Fountain.x, Fountain.y + BowlR + .9f));
            Lay(LoftY, P(Fountain.x, ScreenZ - 1.2f), P(LinesX[0], ScreenZ - 1.2f), P(LinesX[0], ScreenZ - .9f));
            Lay(GreatY, P(0f, GreatZ - GreatR + 1.2f), P(0f, GreatZ + GreatR - 1.2f));
            Lay(BalconyY, P(0f, BalconyZ0 + 1.6f), P(0f, HallZ1 - .3f));
            // The fountain's top: one arrow to the loft.
            Chevron(at, V(Fountain.x, TierTop[TierTop.Length - 1] + .03f, Fountain.y - .3f), Quaternion.LookRotation(Vector3.back));
        }

        // An arrow ">" pointing along `turn`: two gold bars.
        static void Chevron(Transform parent, Vector3 at, Quaternion turn)
        {
            foreach (float s in new[] { -1f, 1f })
            {
                var arm = turn * Quaternion.Euler(0f, s * 40f, 0f);
                YardKit.Box(parent, "BH arrow", at - arm * V(0f, 0f, .35f), V(.16f, .02f, .8f), Arrow, YardKit.Solid.None, arm);
            }
        }

        // ------------------------------------------------------------------ the vocabulary

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static GameObject Floor(CourseBuilder b, string name, float x0, float x1, float z0, float z1, float top)
        {
            var go = b.Box(name, V(x0, top - 1f, z0), V(x1, top, z1), Look.Floor, true);
            go.AddComponent<CourseFloor>();
            return go;
        }

        // A solid that cannot be climbed (slate), x0..x1, y0..y1, z0..z1.
        static GameObject Wall(CourseBuilder b, string name, float x0, float x1, float y0, float y1, float z0, float z1) =>
            b.Box(name, V(x0, y0, z0), V(x1, y1, z1), Look.NoClimb, true);

        // A picture: no collider.
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
