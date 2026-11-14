using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // Zone 2, the royal loading and sorting yard, rebuilt as a flow of cargo (R661). The user on R659's yard: "위에서 짐들을
    // 그냥 바닥에 떨궈버리고 있고, 심지어 거대한 짐덩이는 마치 망치처럼 바닥에 연속해서 쿵쿵 내려찍기만 하고, 바닥에 내려놓거나
    // 하질 않아"; on the cargo flow proposed instead, "컨셉 자체는 좋은데 플레이어들을 확실히 방해할 수 있을만한 동선과 장애물
    // 형태로 구성해서 레벨디자인". So the cargo comes in, is carried, stored and set down - nothing is dropped - and every
    // step of it stands across the way: the whole yard is crossed through gaps that move. In its own frame (sections 3-4),
    // going north from the canal's far landing:
    //
    //   z 106 .. 110.3     the mouth: the canal's landing opens 20 m wide, lamps either side
    //   z 111.2 .. 112.8   the conveyor (CargoConveyor): a 0.45 m belt from stall to stall carrying crates and two-high
    //                      stacks out of the west stall's hatch (입고) into the east one's (분류); hop over it through a
    //                      gap in the loads - a load knocks whoever is on the belt in its way off it, and down
    //   z 116.6 .. 130.4   three storage rows (StackRow, z 117.5 / 123.5 / 129.5): walls of two-high stacks from stall to
    //                      stall with one empty slot in each half; each row's crane fills the gap with the stack beside
    //                      it, so the gap walks out along its half and back (both halves mirrored, meeting in the middle);
    //                      each row's crane keeps its own pace, so the three rows' gaps never stay lined up and the way
    //                      zigzags; the gap shows green, flashes red before a stack swings in, and whoever is still in
    //                      it is shoved out of the row and knocked down. Aisles 4.2 m between the rows
    //   z 131.7 .. 134.1   the heavy berth (CargoSlam lowered on chains): a royal chest the size of a cart carried
    //                      overhead to one of four berths, lowered and set down hard - the floor shakes, a wave knocks
    //                      down whoever is on the floor inside its red circle (jump it) - and left there 4.5 seconds
    //                      as a wall before it is hoisted and taken to the next berth
    //   z 138.8            CP3 on the apron, then the mill basin and the lifting wheel, as it always was
    //
    // Along both walls, sorting stalls under blue awnings (solid, never climbed) narrow the yard to +-StallFront, from the
    // mouth to the apron (no corner to be pinned in). Every danger warns ahead (a load is seen coming along the belt, a
    // gap flashes red 0.8 s before a stack swings in, the chest's circle is dim through its carry and bright for 2.1 s
    // before it lands) and runs on ObstacleClock, so every PC sees the same without a packet. Nothing throws anyone
    // forward (every knock's part along the way cut to 60 %), nobody is knocked down twice in a row (YardKit.ChainGrace),
    // the knocked-down are let through what knocked them until clear. The numbers and the reasons:
    // Docs/PawnRush/ROYAL_CARRIAGE_YARD.md §17.
    public sealed partial class RoyalCarriageYard
    {
        const float SortHalf = 26f;
        // The sorting stalls along the walls, from the mouth to the apron.
        const float StallFront = 23.5f, StallZ0 = ArenaStart, StallZ1 = 136.5f;
        // The conveyor's middle line and its belt; the hatch it runs through in each stall.
        const float ConveyorZ = 112f, BeltWidth = 1.6f, BeltTop = .45f, HatchHalf = 1.15f, HatchTop = 3.2f;
        // The storage rows' middle lines, and the clock seconds each row's crane is ahead (with its own pace, sorting.rowOpen).
        static readonly float[] RowZs = { 117.5f, 123.5f, 129.5f };
        static readonly float[] RowPhases = { 0f, 13.7f, 29.3f };
        const int RowSlots = 9;
        const float RowPitch = 2.2f, RowBeam = 6.5f;
        static readonly Vector3 StackSize = new Vector3(2f, 2.4f, 1.8f);
        // The heavy berth: its line, the four berths (the chest carried at most BerthStep from one to the next), the gantry.
        const float BerthZ = 132.9f, BerthStep = 18.01f, BerthBeam = 9f, BerthHang = 3.6f;
        static readonly float[] BerthPlaces = { -13.5f, -4.5f, 4.5f, 13.5f };
        // CP3, on the apron: 0.4 m past the heavy chest's circle (BerthZ + slamRadius 5.5 = 138.4), 1 m short of the basin.
        const float ApronZ = 138.8f;

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

            // 3. The heavy berth: the chest lowered on its chains at one of four berths, never more than two berths on.
            var berth = Place<CargoSlam>(s, "Heavy berth gantry", new Vector3(0f, 0f, BerthZ));
            berth.LimitStep(BerthStep);
            berth.LowerOnChains();
            berth.Configure(SortHalf + .5f, 0f, BerthBeam, new Vector3(3.2f, 2.4f, 2.4f), BerthHang, BerthPlaces, true, 6612,
                            y.slamCarry, y.slamHold, y.slamFall, y.slamSit, y.slamHoist, y.slamRadius, .35f, y.shockPush, 2f, y.shockKnockdown, 6f, later);
            foreach (float x in BerthPlaces)
                BerthMark(s, new Vector3(x, 0f, BerthZ));

            // Lamps either side of the mouth and along the apron.
            foreach (float side in new[] { -1f, 1f })
            {
                LampPost(s, new Vector3(side * 11.2f, 0f, ArenaStart + .8f));
                LampPost(s, new Vector3(side * 8.5f, 0f, PoolZ0 - .9f));
                LampPost(s, new Vector3(side * 21.5f, 0f, PoolZ0 - .9f));
            }
            Sign(s, "3. 왕실 하역 창고: 컨베이어는 짐 사이로 · 짐더미 줄은 초록 빈칸으로 · 궤짝 내려놓을 땐 점프",
                 new Vector3(0f, 3.6f, ArenaStart + 1.8f), 1.05f);
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
