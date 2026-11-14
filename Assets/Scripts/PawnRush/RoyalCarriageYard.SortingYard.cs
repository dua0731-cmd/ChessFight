using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // Zone 2, the royal loading and sorting yard (R659, the user's "짐을 옮기고 정리하며 크레인으로 해당 작업들을 진행하는 듯한
    // 공간" with the second concept picture; "오르막길 … 구역을 제거하고. 제거한 만큼 해당 구역의 넓이를 다시 확보해", "물레방아는
    // 무조건 제거하지 말고 남겨둬야해", harder than before for 6 v 6). The cargo ramps and R532's loading yard between them are
    // gone: the whole yard between the walls is one place where cargo is moved and sorted by cranes - no carriage or horse
    // here, those are the first yard's. In its own frame (sections 3-4), going north from the canal's far landing:
    //
    //   z 106 .. 108.8     the mouth (the canal's landing opens 20 m wide), lamps
    //   z 109.5 .. 115.9   three crane lines (C1-C3), nine hooks each over the whole yard firing in a ripple, one after
    //                      another a third of a second apart, C2 the other way and its splits half a segment along
    //   z 116.7 .. 136.5   the slam hall: two gantries (G1 z 123.7, G2 z 129.5) slam giant chests down at x 0, +-10 or
    //                      +-20, every 2.6 s one or the other, G2 at the mirror of G1's place; a crane line C4 (z 126.6)
    //                      between them; eight sorting docks (0.6 m pallets) to stand on as the wave passes under
    //   z 136.5 .. 139.85  the apron: CP3, then the mill basin and the lifting wheel, as it always was
    //
    // Along both walls, sorting stalls under blue awnings (solid, never climbed) narrow the yard to +-StallFront. Every
    // danger warns a second or more ahead (a crate held still over its orange disc for 0.7 s and the disc 1 s ahead, the
    // giant chest's red circle dim through its carry and bright for the last 1.85 s) and runs on ObstacleClock, so every PC
    // sees the same without a packet. The slam's wave spares whoever is off the floor - in a jump or up on a dock. Nothing
    // throws anyone forward (every knock's part along the way cut to 60 %), nobody is knocked down twice in a row
    // (YardKit.ChainGrace), nothing pins anyone (crates 0.6 m off the stalls, chests 1.9 m, docks 2.2 m off every chest).
    // The numbers and the reasons: Docs/PawnRush/ROYAL_CARRIAGE_YARD.md §15.
    public sealed partial class RoyalCarriageYard
    {
        const float SortHalf = 26f;
        // The sorting stalls along the walls.
        const float StallFront = 23.5f, StallZ0 = 108.8f, StallZ1 = 136.5f;
        // The crane lines: a crate's centre comes down within CraneReach of the middle (its face 0.6 m off the stalls).
        const float CraneReach = 22.2f, CraneHooks = 9f;
        static readonly float[] CraneLines = { 109.5f, 112.7f, 115.9f, 126.6f };
        // The giant chests' lines and places (G2's mirrored); a circle's far edge (z 136.5) stays 0.7 m short of CP3.
        const float SlamLine1 = 123.7f, SlamLine2 = 129.5f, SlamStep = 20f;
        static readonly float[] SlamPlaces = { -20f, -10f, 0f, 10f, 20f };
        // The sorting docks: 2.4 m square, 0.6 m high, between the places (2.2 m off every chest, 1 m off C4's crates).
        const float DockSize = 2.4f, DockTop = .6f;
        static readonly float[] DockXs = { -15f, -5f, 5f, 15f };
        // CP3, on the apron before the basin.
        const float ApronZ = 137.2f;

        void SortingYard(Transform parent)
        {
            var s = Group("Loading and sorting yard", parent);
            var y = sorting;

            foreach (float side in new[] { -1f, 1f })
                SortingStalls(s, side);

            // The crane lines: nine hooks across the yard, firing in a ripple a third of a second apart; C2 and C4 the other
            // way with their splits half a segment along (ten hooks, half ones at the ends), each line 0.75 s after the last.
            float p = y.cranePeriod;
            for (int line = 0; line < CraneLines.Length; line++)
            {
                bool turned = line % 2 == 1;
                Crane(s, CraneLines[line], 6591 + line, p, Ripple(turned, line * .75f, p));
            }

            // The giant chests: G2 at the mirror of G1's place, half a trip later.
            var mirrored = new float[SlamPlaces.Length];
            for (int i = 0; i < SlamPlaces.Length; i++) mirrored[i] = -SlamPlaces[i];
            var g1 = Slam(s, SlamLine1, 6593, SlamPlaces);
            var g2 = Slam(s, SlamLine2, 6593, mirrored);
            if (g1 != null && g2 != null && g2.Chest != null) g2.Chest.SetPhase(g1.Period * .5f);

            // The sorting docks between the places, on both slam lines.
            foreach (float z in new[] { SlamLine1, SlamLine2 })
                foreach (float x in DockXs)
                    Dock(s, new Vector3(x, 0f, z));

            // The straight way: a boardwalk from the mouth to the middle wheel (a picture), lamps on the apron.
            YardKit.Picture(s, "Boardwalk", new Vector3(-1.5f, 0f, ArenaStart), new Vector3(1.5f, .012f, PoolZ0 - .3f), YardKit.WoodLight);
            foreach (float side in new[] { -1f, 1f })
            {
                YardKit.Picture(s, "Boardwalk edge", new Vector3(Mathf.Min(side * 1.5f, side * 1.62f), 0f, ArenaStart),
                                new Vector3(Mathf.Max(side * 1.5f, side * 1.62f), .016f, PoolZ0 - .3f), YardKit.WoodDark);
                LampPost(s, new Vector3(side * 8.5f, 0f, PoolZ0 - .9f));
                LampPost(s, new Vector3(side * 21.5f, 0f, PoolZ0 - .9f));
            }
            Sign(s, "3. 왕실 하역 창고: 떨어지는 짐 · 거대 짐 충격파 (원 밖으로 / 점프 / 받침대 위)", new Vector3(0f, 3.6f, ArenaStart + 1.6f), 1.2f);
        }

        // A crane line's hooks over the yard: CraneHooks segments (`shifted`: their splits half a segment along, so half
        // segments at both ends), each firing `step` after its neighbour, west to east (shifted: east to west), the whole
        // line `start` seconds along.
        static CraneDrop.Hook[] Ripple(bool shifted, float start, float period)
        {
            float r = CraneReach, w = 2f * r / CraneHooks, step = period / CraneHooks;
            int n = shifted ? (int)CraneHooks + 1 : (int)CraneHooks;
            var hooks = new CraneDrop.Hook[n];
            for (int i = 0; i < n; i++)
            {
                float lo = shifted ? Mathf.Max(-r, -r + (i - .5f) * w) : -r + i * w;
                float hi = shifted ? Mathf.Min(r, -r + (i + .5f) * w) : -r + (i + 1) * w;
                int order = shifted ? n - 1 - i : i;
                hooks[i] = new CraneDrop.Hook(lo, hi, start + order * step);
            }
            return hooks;
        }

        // A crane line across the whole yard, its posts in the side walls.
        void Crane(Transform s, float z, int seed, float period, CraneDrop.Hook[] hooks)
        {
            var y = sorting;
            Place<CraneDrop>(s, "Crane", new Vector3(0f, 0f, z))
                .Configure(SortHalf + .45f, 0f, 7.6f, period, y.dropHeight, y.crateSize, y.crateKnockback, seed, hooks, later);
        }

        // A giant chest's gantry across the whole yard, its posts in the side walls; the chest never carried more than
        // SlamStep from one place to the next.
        CargoSlam Slam(Transform s, float z, int seed, float[] places)
        {
            var y = sorting;
            var slam = Place<CargoSlam>(s, "Giant chest gantry", new Vector3(0f, 0f, z));
            slam.LimitStep(SlamStep);
            slam.Configure(SortHalf + .5f, 0f, 11f, new Vector3(3.2f, 2.4f, 2.4f), 6f, places, true, seed,
                           y.slamCarry, y.slamHold, y.slamFall, y.slamSit, y.slamHoist, y.slamRadius, .35f, y.shockPush, 2f, y.shockKnockdown, 6f, later);
            return slam;
        }

        // A sorting dock: a 0.6 m pallet deck on blocks, walked on and jumped onto (the slam's wave passes under whoever
        // stands on it), with sacks at two corners as in the reference picture and a gold edge.
        static void Dock(Transform s, Vector3 at)
        {
            float h = DockSize * .5f;
            var deck = YardKit.Span(s, "Sorting dock", at + new Vector3(-h, 0f, -h), at + new Vector3(h, DockTop, h), YardKit.WoodLight);
            deck.AddComponent<CourseFloor>();
            for (float dz = -h + .25f; dz < h; dz += .5f)
                YardKit.Picture(s, "Dock plank", at + new Vector3(-h + .03f, DockTop, dz - .2f), at + new Vector3(h - .03f, DockTop + .02f, dz + .2f), YardKit.WoodDark);
            YardKit.Picture(s, "Dock trim", at + new Vector3(-h - .02f, DockTop - .1f, -h - .02f), at + new Vector3(h + .02f, DockTop - .04f, h + .02f), YardKit.Gold);
        }

        // The sorting stalls along one wall: one solid block nobody climbs (2.4 m), blue awnings on posts, bays of sorted
        // goods - crates, sacks on pallets, barrels, loads under royal cloth - with numbered boards and a scale.
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
                // Goods 2.5 m deep: the Load pictures are drawn for a 3 m shed, so half a metre is left behind them.
                Load(s, side, front - side * .25f, (z0 + z1) * .5f, step, (i * 3 + (side < 0f ? 1 : 4)) % 5);
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
    }
}
