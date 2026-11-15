using ChessFight.Network;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // Zone 2, the royal loading and sorting yard, a flow of cargo that blocks the way (R661), half again as long and
    // recomposed round two new machines (R662). The user on R659's yard: "위에서 짐들을 그냥 바닥에 떨궈버리고 … 바닥에
    // 내려놓거나 하질 않아"; on R661: "플레이어들을 확실히 방해할 수 있을만한 동선과 장애물 형태로 구성해서 레벨디자인"; on R662:
    // "2구역의 세로길이를 현재의 1.5배 정도 늘려주고 조금 더 재구성해줘, 지금 너무 짧은 느낌이야. 그리고 지금처럼 좌우로 한칸씩
    // 이동하며 플레이어를 방해하는 장애물과 말고도 다른 매커니즘의 장애물이 한두개 정도는 더". Nothing is dropped and nothing
    // hammers: cargo is carried, swung, hoisted and set down, and every step of it stands across the way. A design panel
    // (three designs, three judges, Docs/PawnRush/ROYAL_CARRIAGE_YARD.md §18) and the bot test chose and tuned it. In its
    // own frame (sections 3-4), going north from the canal's far landing:
    //
    //   z 106 .. 111.2     the mouth: the canal's landing opens 20 m wide, lamps either side
    //   z 111.2 .. 112.8   the conveyor (CargoConveyor, R661): a 0.45 m belt from stall to stall, loads out of the west
    //                      stall's hatch (입고) into the east one's (분류); hop on through a gap in the loads
    //   z 116.6 .. 118.4   storage row 1 (StackRow, R661): a wall of two-high stacks with one gap in each half, walked along
    //                      by its crane (green, red 0.8 s before a stack swings in)
    //   z 122.6 .. 133     NEW: the timber crane field - two slewing jib cranes (SlewCrane) on drums at x +-9.5, z 123,
    //                      swinging 8 m timber loads a hand over the cobbles through the north half round their drums; both
    //                      jibs always point the same way, their sweeps overlap in the middle, so there is no lane they never
    //                      sweep: jump the load as it comes, follow it out, or keep to the ring by a drum. Timber stores
    //                      along the walls (|x| 20.11 .. 23.5) leave only a single-file pinch beside the sweeps, a long way
    //                      round
    //   z 133 .. 137.4     NEW: the loft hoists - a loft (137.4 .. 140.6) too high to climb closes the yard; two mirrored
    //                      counterweighted hoists (PalletHoist) either side of the middle lift pallets to its deck, one of
    //                      each pair always down to board while the other is up; never wait under a raised one
    //   z 137.4 .. 140.6   the loft: a deck 2.4 m up over |x| <= 12 (high ends beyond it), open to the north - drop down
    //   z 145.1 .. 146.9   storage row 2 (StackRow): the re-merge, twelve pawns and two gaps, inside the berth's circle
    //   z 148.2 .. 150.6   the heavy berth (CargoSlam lowered on chains, R661): the royal chest set down with a shockwave
    //                      (6 m circle, jump it) at one of four berths and left 4.5 s as a wall
    //   z 155.8            CP3 on the apron, then the mill basin and the one lifting wheel, as it always was
    //
    // Along both walls, sorting stalls under blue awnings (solid, never climbed) narrow the yard to +-StallFront, from the
    // mouth to the apron. Every danger warns ahead and runs on ObstacleClock, so every PC sees the same without a packet;
    // the cranes' and hoists' motion is SortingYardRules, checked by the Core tests. Nothing throws anyone forward (every
    // knock's part along the way cut to 60 %), nobody is knocked down twice in a row (YardKit.ChainGrace), the
    // knocked-down are let through what knocked them until clear.
    public sealed partial class RoyalCarriageYard
    {
        const float SortHalf = 26f;
        // The sorting stalls along the walls, from the mouth to the apron.
        const float StallFront = 23.5f, StallZ0 = ArenaStart, StallZ1 = PoolZ0 - 3.35f;
        // The conveyor's middle line and its belt; the hatch it runs through in each stall.
        const float ConveyorZ = 112f, BeltWidth = 1.6f, BeltTop = .45f, HatchHalf = 1.15f, HatchTop = 3.2f;
        // The storage rows' middle lines - one before the crane field, one after the loft (R662: a third in a row was the
        // repetition the user named) - and the clock seconds each row's crane is ahead (its own pace, sorting.rowOpen).
        static readonly float[] RowZs = { 117.5f, 146f };
        static readonly float[] RowPhases = { 0f, 13.7f };
        const int RowSlots = 9;
        const float RowPitch = 2.2f, RowBeam = 6.5f;
        static readonly Vector3 StackSize = new Vector3(2f, 2.4f, 1.8f);
        // The crane field and the stores beside it (SortingYardRules: the masts, radii and stores the Core tests check).
        const float CraneX = (float)SortingYardRules.CraneX, CraneZ = (float)SortingYardRules.CraneZ;
        const float StoreFront = (float)SortingYardRules.StoreFront, StoreZ0 = (float)SortingYardRules.StoreZ0, StoreZ1 = (float)SortingYardRules.StoreZ1;
        // The loft and its hoists: the posts at x +-HoistPostX, the plates' middle at HoistZ.
        const float LoftZ0 = (float)SortingYardRules.LoftZ0, LoftZ1 = (float)SortingYardRules.LoftZ1, LoftDeckHalf = 12f, LoftTop = 2.4f, LoftEndTop = 4.4f;
        const float HoistPostX = 3.5f, HoistZ = (float)((SortingYardRules.HoistZ0 + SortingYardRules.HoistZ1) * .5);
        // The heavy berth: its line, the four berths (the chest carried at most BerthStep from one to the next), the gantry.
        const float BerthZ = 149.4f, BerthStep = 18.01f, BerthBeam = 9f, BerthHang = 3.6f;
        static readonly float[] BerthPlaces = { -13.5f, -4.5f, 4.5f, 13.5f };
        // CP3, on the apron: 0.4 m past the heavy chest's circle (BerthZ + slamRadius 6 = 155.4), 1.05 m short of the basin.
        const float ApronZ = PoolZ0 - 1.05f;

        void SortingYard(Transform parent)
        {
            var s = Group("Loading and sorting yard", parent);
            var y = sorting;

            foreach (float side in new[] { -1f, 1f })
                SortingStalls(s, side);

            // 1. The conveyor from stall to stall, loads out of the west hatch and into the east one.
            Place<CargoConveyor>(s, "Conveyor", new Vector3(0f, 0f, ConveyorZ))
                .Configure(StallFront * 2f, BeltWidth, BeltTop, y.conveyorSpeed, y.conveyorLoads, 9f, 2.6f, 6611, y.conveyorKnockback, later);

            // 2. The storage rows, each crane at its own pace and phase.
            for (int i = 0; i < RowZs.Length; i++)
            {
                float open = y.rowOpen != null && y.rowOpen.Length > 0 ? y.rowOpen[i % y.rowOpen.Length] : 1.1f;
                Place<StackRow>(s, "Storage row " + (i + 1), new Vector3(0f, 0f, RowZs[i]))
                    .Configure(RowSlots, RowPitch, StackSize, StallFront, SortHalf + .45f, RowBeam, open, .3f, y.rowCarry, .3f, .25f, .8f,
                               y.rowKnockback, RowPhases[i], later);
            }

            // 3. The timber crane field: two cranes, both jibs always pointing the same way (rest west, slew through the
            // north, rest east, back), and the timber stores along the walls beside it.
            foreach (float side in new[] { -1f, 1f })
                Place<SlewCrane>(s, side < 0f ? "Timber crane W" : "Timber crane E", new Vector3(side * CraneX, 0f, CraneZ))
                    .Configure((float)SortingYardRules.CraneDrum, 2.4f, 7.6f, (float)SortingYardRules.CraneInner, (float)SortingYardRules.CraneOuter,
                               new Vector2(.42f, .42f), .13f, -180f, 0f, false, y.craneRest, y.craneSlew, y.craneRamp, 1f, 0f,
                               y.craneKnockback, .5f, y.cranePush, y.craneKnockdownSpeed, 1f, later);
            foreach (float side in new[] { -1f, 1f })
                TimberStore(s, side);

            // 4. The loft and its two counterweighted hoists, mirrored either side of the middle.
            Loft(s);
            foreach (float side in new[] { -1f, 1f })
                Place<PalletHoist>(s, side < 0f ? "Loft hoist W" : "Loft hoist E", new Vector3(side * HoistPostX, 0f, HoistZ))
                    .Configure(side, new Vector3(2.37f, .22f, 2.4f), .4f, .03f, 6f, y.hoistDown, y.hoistMove, (float)SortingYardRules.HoistLow,
                               (float)SortingYardRules.HoistHigh, y.hoistPush, later);

            // 5. The heavy berth: the chest lowered on its chains at one of four berths, never more than two berths on.
            var berth = Place<CargoSlam>(s, "Heavy berth gantry", new Vector3(0f, 0f, BerthZ));
            berth.LimitStep(BerthStep);
            berth.LowerOnChains();
            berth.Configure(SortHalf + .5f, 0f, BerthBeam, new Vector3(3.2f, 2.4f, 2.4f), BerthHang, BerthPlaces, true, 6612,
                            y.slamCarry, y.slamHold, y.slamFall, y.slamSit, y.slamHoist, y.slamRadius, .35f, y.shockPush, 2f, y.shockKnockdown, 6f, later);
            foreach (float x in BerthPlaces)
                BerthMark(s, new Vector3(x, 0f, BerthZ));

            // Lamps either side of the mouth and along the apron; the signs.
            foreach (float side in new[] { -1f, 1f })
            {
                LampPost(s, new Vector3(side * 11.2f, 0f, ArenaStart + .8f));
                LampPost(s, new Vector3(side * 8.5f, 0f, PoolZ0 - .9f));
                LampPost(s, new Vector3(side * 21.5f, 0f, PoolZ0 - .9f));
            }
            Sign(s, "3. 왕실 하역 창고: 컨베이어는 짐 사이로 · 짐더미 줄은 초록 빈칸으로", new Vector3(0f, 3.9f, ArenaStart + 1.8f), 1.05f);
            Sign(s, "들보 크레인은 넘거나 뒤따라 · 승강대는 초록 불에 · 궤짝엔 점프", new Vector3(0f, 3.25f, ArenaStart + 1.8f), 1.05f);
            Sign(s, "목재 크레인: 낮은 들보는 점프 · 지나간 뒤를 따라가기", new Vector3(0f, 3.2f, 119.6f), 1.05f);
            // On the loft's edge between the two hoists' posts (|x| < 3.3): two short lines, so no post hides a letter.
            Sign(s, "도르래 승강대: 초록 불에 타기", new Vector3(0f, 4.25f, LoftZ0 - .05f), 1f);
            Sign(s, "매달린 짐 아래 서지 말 것", new Vector3(0f, 3.7f, LoftZ0 - .05f), 1f);
        }

        // The timber stores along one wall beside the crane field: one solid block nobody climbs (2.4 m), stacked squared
        // beams with their ends to the yard and iron bands, where the cranes' outer rests lay their loads.
        static void TimberStore(Transform s, float side)
        {
            float front = side * StoreFront, back = side * StallFront;
            float x0 = Mathf.Min(front, back), x1 = Mathf.Max(front, back);
            var block = YardKit.Span(s, "Timber store (collider)", new Vector3(x0, 0f, StoreZ0), new Vector3(x1, 2.4f, StoreZ1), null, Solid.NoClimb);
            block.GetComponent<MeshRenderer>().enabled = false;
            float mid = (x0 + x1) * .5f, depth = x1 - x0, length = StoreZ1 - StoreZ0, centre = (StoreZ0 + StoreZ1) * .5f;
            // Layers of beams lying along the wall (alternate shades), their joints every 0.88 m.
            int layer = 0;
            for (float y = .2f; y < 2.3f; y += .42f, layer++)
                YardKit.Box(s, "Stored beams", new Vector3(mid, y, centre), new Vector3(depth - .02f, .4f, length - .04f), layer % 2 == 0 ? YardKit.WoodLight : YardKit.WoodDark, Solid.None);
            for (float z = StoreZ0 + .44f; z < StoreZ1 - .2f; z += .88f)
                YardKit.Box(s, "Beam joint", new Vector3(front - side * .01f, 1.2f, z), new Vector3(.02f, 2.36f, .03f), YardKit.Iron, Solid.None);
            for (float z = StoreZ0 + 1.5f; z < StoreZ1; z += 3.5f)
                YardKit.Box(s, "Store band", new Vector3(front, 1.2f, z), new Vector3(.08f, 2.42f, .1f), YardKit.Iron, Solid.None);
            YardKit.Box(s, "Store trim", new Vector3(front, 2.4f, (StoreZ0 + StoreZ1) * .5f), new Vector3(.1f, .06f, StoreZ1 - StoreZ0), YardKit.Gold, Solid.None);
        }

        // The storage loft (다락) across the yard: a block too high to climb from the floor, its middle a plank deck 2.4 m up
        // (|x| <= LoftDeckHalf, open to the north: drop down) and its ends 4.4 m up - two metres over the deck, out of any
        // jump's or the pawn's F step's reach - so the hoists are the only way up and the deck the only way on. The follow
        // camera looks through it.
        void Loft(Transform s)
        {
            var deck = YardKit.Span(s, "Loft deck", new Vector3(-LoftDeckHalf, 0f, LoftZ0), new Vector3(LoftDeckHalf, LoftTop, LoftZ1), YardKit.WoodDark, Solid.NoClimb);
            deck.AddComponent<CourseFloor>();
            deck.AddComponent<ChessFight.Game.CameraSeeThrough>();
            for (float z = LoftZ0 + .2f; z < LoftZ1; z += .4f)
                YardKit.Picture(s, "Loft plank", new Vector3(-LoftDeckHalf + .03f, LoftTop, z - .17f), new Vector3(LoftDeckHalf - .03f, LoftTop + .012f, z + .17f), YardKit.WoodLight);
            YardKit.Picture(s, "Loft edge", new Vector3(-LoftDeckHalf, LoftTop, LoftZ1 - .12f), new Vector3(LoftDeckHalf, LoftTop + .016f, LoftZ1), YardKit.Gold);
            YardKit.Picture(s, "Loft lip", new Vector3(-LoftDeckHalf, LoftTop, LoftZ0), new Vector3(LoftDeckHalf, LoftTop + .016f, LoftZ0 + .1f), YardKit.Gold);
            foreach (float side in new[] { -1f, 1f })
            {
                float a = side * LoftDeckHalf, b = side * StallFront;
                var end = YardKit.Span(s, "Loft end", new Vector3(Mathf.Min(a, b), 0f, LoftZ0), new Vector3(Mathf.Max(a, b), LoftEndTop, LoftZ1), YardKit.WoodDark, Solid.NoClimb);
                end.AddComponent<ChessFight.Game.CameraSeeThrough>();
                // Crates stacked on the ends, sacks along the deck's sides: the loft's stores.
                for (float x = Mathf.Min(a, b) + 1.1f; x < Mathf.Max(a, b) - 1f; x += 2.3f)
                    StackRow.StackPicture(s, new Vector3(x, LoftEndTop, (LoftZ0 + LoftZ1) * .5f), new Vector3(2f, 1.6f, 2.6f), (int)(x * 3f));
                YardKit.Box(s, "Loft end trim", new Vector3((a + b) * .5f, LoftEndTop, LoftZ0 - .02f), new Vector3(Mathf.Abs(b - a), .1f, .06f), YardKit.Gold, Solid.None);
            }
            // The face boards under the deck, between the hoists' wells (pictures on the solid block).
            for (float x = -LoftDeckHalf + .6f; x < LoftDeckHalf; x += 1.2f)
                YardKit.Picture(s, "Loft face board", new Vector3(x - .55f, .1f, LoftZ0 - .02f), new Vector3(x + .55f, LoftTop - .1f, LoftZ0), YardKit.WoodLight);
        }

        // A berth's place on the floor, where the heavy chest is set down: worn boards a little bigger than the chest, iron
        // plates at the corners (pictures).
        static void BerthMark(Transform s, Vector3 at)
        {
            const float hx = 1.95f, hz = 1.55f;
            YardKit.Picture(s, "Berth boards", at + new Vector3(-hx, 0f, -hz), at + new Vector3(hx, .014f, hz), YardKit.WoodDark);
            foreach (float a in new[] { -1f, 1f })
                foreach (float b in new[] { -1f, 1f })
                    YardKit.Picture(s, "Berth corner", at + new Vector3(a * hx - .28f, .014f, b * hz - .28f), at + new Vector3(a * hx + .28f, .022f, b * hz + .28f), YardKit.Iron);
        }

        // The sorting stalls along one wall: one solid block nobody climbs (2.4 m), blue awnings on posts, bays of sorted
        // goods - crates, sacks on pallets, barrels, loads under royal cloth - with numbered boards and a scale, and the
        // conveyor's hatch in the bay it runs through.
        void SortingStalls(Transform s, float side)
        {
            float front = side * StallFront, back = side * SortHalf;
            float x0 = Mathf.Min(front, back), x1 = Mathf.Max(front, back);
            var goods = YardKit.Span(s, "Stall goods (collider)", new Vector3(x0, 0f, StallZ0), new Vector3(x1, GoodsTop, StallZ1), null, Solid.NoClimb);
            goods.GetComponent<MeshRenderer>().enabled = false;
            YardKit.Picture(s, "Stall boards", new Vector3(x0, 0f, StallZ0), new Vector3(x1, .03f, StallZ1), YardKit.WoodDark);
            float eave = front - side * .35f;
            const float bay = 4.6f;
            int bays = Mathf.RoundToInt((StallZ1 - StallZ0) / bay);
            float step = (StallZ1 - StallZ0) / bays;
            for (int i = 0; i < bays; i++)
            {
                float z0 = StallZ0 + i * step, z1 = z0 + step;
                // An awning a bay long, each its own, with a gap between them as in the picture.
                Awning(s, back, eave, z0 + .1f, z1 - .1f);
                if (ConveyorZ > z0 && ConveyorZ < z1) Hatch(s, side, front, back, z0, z1);
                // Goods 2.5 m deep: the Load pictures are drawn for a 3 m shed, so half a metre is left behind them.
                else Load(s, side, front - side * .25f, (z0 + z1) * .5f, step, (i * 3 + (side < 0f ? 1 : 4)) % 5);
                // A numbered board on the bay's post (gold), the sorting yard's.
                YardKit.Box(s, "Bay number", new Vector3(front - side * .05f, 2.9f, z0 + .3f), new Vector3(.04f, .34f, .5f), YardKit.Gold, Solid.None);
            }
            for (int i = 0; i <= bays; i++)
                YardKit.Box(s, "Stall post", new Vector3(front + side * .14f, 1.75f, StallZ0 + i * step), new Vector3(.24f, 3.5f, .24f), YardKit.WoodDark, Solid.None);
            // A big scale at the middle, over the goods.
            float mid = (StallZ0 + StallZ1) * .5f;
            YardKit.Box(s, "Scale post", new Vector3(back - side * .8f, 1.6f, mid), new Vector3(.16f, 3.2f, .16f), YardKit.Iron, Solid.None);
            YardKit.Box(s, "Scale beam", new Vector3(back - side * .8f, 3.15f, mid), new Vector3(.12f, .12f, 2.2f), YardKit.Gold, Solid.None);
            foreach (float dz in new[] { -1f, 1f })
                YardKit.Cylinder(s, "Scale pan", new Vector3(back - side * .8f, 2.5f, mid + dz), Vector3.up, .8f, .06f, YardKit.Gold, Solid.None);
        }

        // The conveyor's hatch in a stall (pictures in the stall's solid block): a dark doorway in an iron frame with a gold
        // lintel and a blue valance over the loads, a board over it - 입고 on the west stall the loads come out of, 분류 on
        // the east one they go into - and crates waiting either side of it in the rest of the bay.
        static void Hatch(Transform s, float side, float front, float back, float z0, float z1)
        {
            float x0 = Mathf.Min(front, back), x1 = Mathf.Max(front, back);
            float a = ConveyorZ - HatchHalf, b = ConveyorZ + HatchHalf;
            YardKit.Picture(s, "Hatch tunnel", new Vector3(x0, 0f, a), new Vector3(x1, HatchTop, b), YardKit.Glass);
            foreach (float z in new[] { a - .14f, b + .14f })
                YardKit.Box(s, "Hatch post", new Vector3(front - side * .07f, (HatchTop + .25f) * .5f, z), new Vector3(.24f, HatchTop + .25f, .28f), YardKit.Iron, Solid.None);
            YardKit.Box(s, "Hatch lintel", new Vector3(front - side * .07f, HatchTop + .12f, ConveyorZ), new Vector3(.26f, .26f, b - a + .56f), YardKit.Gold, Solid.None);
            YardKit.Box(s, "Hatch valance", new Vector3(front - side * .03f, HatchTop - .12f, ConveyorZ), new Vector3(.04f, .22f, b - a), YardKit.Banner, Solid.None);
            YardKit.Box(s, "Hatch board", new Vector3(front - side * .2f, HatchTop + .62f, ConveyorZ), new Vector3(.06f, .7f, 1.6f), YardKit.WoodDark, Solid.None);
            YardKit.Label(s, side < 0f ? "입고" : "분류", new Vector3(front - side * .24f, HatchTop + .62f, ConveyorZ), .9f, new Color(.98f, .86f, .5f), side * 90f);
            // Crates either side of the hatch, in what is left of the bay.
            float x = front + side * 1.25f;
            foreach (var (from, to) in new[] { (z0 + .2f, a - .35f), (b + .35f, z1 - .2f) })
            {
                if (to - from < .8f) continue;
                float w = Mathf.Min(2.2f, to - from), mid = (from + to) * .5f;
                YardKit.Box(s, "Stored crate", new Vector3(x, .6f, mid), new Vector3(2.2f, 1.2f, w), YardKit.WoodLight, Solid.None);
                YardKit.Box(s, "Crate band", new Vector3(x, .6f, mid), new Vector3(2.24f, .12f, w + .02f), YardKit.WoodDark, Solid.None);
                if (w > 1.2f) YardKit.Box(s, "Stored crate", new Vector3(x + side * .2f, 1.8f, mid), new Vector3(1.4f, 1.2f, w - .3f), YardKit.WoodDark, Solid.None);
            }
        }
    }
}
