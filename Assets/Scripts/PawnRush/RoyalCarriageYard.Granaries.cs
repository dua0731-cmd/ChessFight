using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // The yard's storage sheds (R659, the user's zone 1: "양쪽 성벽쪽에는 짐을 임시적으로 놓아두는 곳간 같은 것들이 있는 게
    // 컨셉"): along both side walls, ShedDepth deep, blue awnings on wooden posts over goods put down for a while - crate
    // stacks, sacks, barrels, hay, loads under royal cloth - stacked right up to the awning's front. Behind the front
    // it is one solid block nobody climbs (2.4 m: over a jump's reach and the pawn's F step), so there is no pocket to
    // hide in: the strip in front of the sheds is the same margin the roaming bodies keep off any wall (0.8 m past their
    // reach), never a place to be pinned. The yard between the sheds is ShedFront either side of its middle.
    public sealed partial class RoyalCarriageYard
    {
        public const float ShedDepth = 3f;
        // Between the gate wall and the canal's parapet, clear of the corners where the walls meet.
        const float ShedZ0 = 2.6f, ShedZ1 = 47.6f, ShedBay = 4.5f, GoodsTop = 2.4f;
        public static float ShedFront => YardHalf - ShedDepth;

        static Material Straw => Arena.Lit("M_YardStraw", Arena.Hex("#D9B65E"), 0f, .9f);
        static Material Sack => Arena.Lit("M_YardSack", Arena.Hex("#CDB48A"), 0f, .95f);

        void Granaries(Transform parent)
        {
            var s = Group("Storage sheds", parent);
            int bays = Mathf.Max(1, Mathf.RoundToInt((ShedZ1 - ShedZ0) / ShedBay));
            float bay = (ShedZ1 - ShedZ0) / bays;
            foreach (float side in new[] { -1f, 1f })
            {
                float front = side * ShedFront, back = side * YardHalf;
                float x0 = Mathf.Min(front, back), x1 = Mathf.Max(front, back);
                // The goods as one solid box, unclimbable; the pictures stand in it.
                var goods = YardKit.Span(s, "Shed goods (collider)", new Vector3(x0, 0f, ShedZ0), new Vector3(x1, GoodsTop, ShedZ1), null, Solid.NoClimb);
                goods.GetComponent<MeshRenderer>().enabled = false;
                // The floor of the sheds: planks under the goods.
                YardKit.Picture(s, "Shed boards", new Vector3(x0, 0f, ShedZ0), new Vector3(x1, .03f, ShedZ1), YardKit.WoodDark);
                // The awning, sloping from the wall's top down to the posts at the front, with a gold trim along its edge.
                float eave = front - side * .35f;
                Awning(s, back, eave, ShedZ0 - .2f, ShedZ1 + .2f);
                for (int i = 0; i <= bays; i++)
                {
                    float z = ShedZ0 + i * bay;
                    // Inside the goods' block (its front face is the shed's front), the lamps over head height.
                    var at = new Vector3(front + side * .14f, 0f, z);
                    YardKit.Box(s, "Shed post", at + Vector3.up * 1.75f, new Vector3(.24f, 3.5f, .24f), YardKit.WoodDark, Solid.None);
                    if (i % 2 == 1) YardKit.Box(s, "Shed lamp", new Vector3(front - side * .1f, 3.05f, z), new Vector3(.26f, .34f, .26f), YardKit.Lamp, Solid.None);
                }
                for (int i = 0; i < bays; i++)
                    Load(s, side, front, ShedZ0 + (i + .5f) * bay, bay, (i * 7 + (side < 0f ? 0 : 3)) % 5);
                // The middle bay of each side under a royal awning with a crown, as in the reference picture.
                float mid = (ShedZ0 + ShedZ1) * .5f;
                YardKit.Picture(s, "Royal awning", new Vector3(Mathf.Min(front - side * .45f, front + side * 1.2f), 3.55f, mid - bay * .55f),
                                new Vector3(Mathf.Max(front - side * .45f, front + side * 1.2f), 3.75f, mid + bay * .55f), YardKit.Banner);
                YardKit.Picture(s, "Royal awning trim", new Vector3(Mathf.Min(front - side * .47f, front - side * .43f), 3.3f, mid - bay * .55f),
                                new Vector3(Mathf.Max(front - side * .47f, front - side * .43f), 3.75f, mid + bay * .55f), YardKit.Banner);
                YardKit.Box(s, "Royal awning crown", new Vector3(front - side * .5f, 3.5f, mid), new Vector3(.04f, .4f, .7f), YardKit.Gold, Solid.None);
            }
        }

        // A sloped blue awning from the wall (x `back`, y 4.6) down to its eave (x `eave`, y 3.4), z0..z1, with a gold
        // trim and a scalloped valance along the eave.
        static void Awning(Transform s, float back, float eave, float z0, float z1)
        {
            const float high = 4.6f, low = 3.4f;
            var from = new Vector3(back, high, (z0 + z1) * .5f);
            var to = new Vector3(eave, low, (z0 + z1) * .5f);
            var across = to - from;
            float run = new Vector2(across.x, across.y).magnitude;
            var tilt = Quaternion.Euler(0f, 0f, Mathf.Atan2(across.y, across.x) * Mathf.Rad2Deg);
            YardKit.Box(s, "Shed awning", (from + to) * .5f, new Vector3(run, .12f, z1 - z0), YardKit.Roof, Solid.None, tilt);
            YardKit.Box(s, "Awning trim", new Vector3(eave, low - .05f, (z0 + z1) * .5f), new Vector3(.08f, .1f, z1 - z0), YardKit.Gold, Solid.None);
            for (float z = z0 + .45f; z < z1 - .3f; z += .9f)
                YardKit.Box(s, "Valance", new Vector3(eave, low - .22f, z), new Vector3(.05f, .34f, .7f), YardKit.Banner, Solid.None);
        }

        // One bay's load (pictures in the solid block): `kind` 0 crates, 1 sacks, 2 barrels, 3 a load under royal cloth, 4 hay.
        static void Load(Transform s, float side, float front, float z, float bay, int kind)
        {
            float x = front + side * 1.5f;   // the bay's middle across its depth
            float w = bay - .5f;
            switch (kind)
            {
                case 0:
                    foreach (float dz in new[] { -w * .25f, w * .25f })
                    {
                        YardKit.Box(s, "Stored crate", new Vector3(x, .6f, z + dz), new Vector3(2.6f, 1.2f, w * .45f), YardKit.WoodLight, Solid.None);
                        YardKit.Box(s, "Crate band", new Vector3(x, .6f, z + dz), new Vector3(2.64f, .12f, w * .46f), YardKit.WoodDark, Solid.None);
                    }
                    YardKit.Box(s, "Stored crate", new Vector3(x + side * .3f, 1.8f, z), new Vector3(1.6f, 1.2f, 1.4f), YardKit.WoodDark, Solid.None);
                    break;
                case 1:
                    YardKit.Box(s, "Pallet", new Vector3(x, .08f, z), new Vector3(2.7f, .16f, w * .9f), YardKit.WoodDark, Solid.None);
                    for (int i = 0; i < 6; i++)
                    {
                        float dz = (i % 3 - 1) * w * .28f, dx = (i / 3 - .5f) * 1.2f;
                        var sack = YardKit.Ball(s, "Sack", new Vector3(x + dx, .55f, z + dz), 1f, Sack, Solid.None);
                        sack.transform.localScale = new Vector3(1.1f, .8f, 1.2f);
                    }
                    var top = YardKit.Ball(s, "Sack", new Vector3(x + side * .2f, 1.25f, z), 1f, Sack, Solid.None);
                    top.transform.localScale = new Vector3(1.1f, .75f, 1.3f);
                    break;
                case 2:
                    for (int i = 0; i < 4; i++)
                    {
                        float dz = (i % 2 - .5f) * w * .45f, dx = (i / 2 - .5f) * 1.2f;
                        YardKit.Cylinder(s, "Barrel", new Vector3(x + dx, .65f, z + dz), Vector3.up, 1f, 1.3f, YardKit.WoodDark, Solid.None);
                        YardKit.Cylinder(s, "Barrel hoop", new Vector3(x + dx, .95f, z + dz), Vector3.up, 1.04f, .08f, YardKit.Iron, Solid.None);
                    }
                    YardKit.Cylinder(s, "Barrel", new Vector3(x + side * .3f, 1.8f, z), Vector3.forward, 1f, 1.3f, YardKit.WoodDark, Solid.None);
                    break;
                case 3:
                    YardKit.Box(s, "Covered load", new Vector3(x, .9f, z), new Vector3(2.6f, 1.8f, w * .85f), YardKit.Banner, Solid.None);
                    YardKit.Box(s, "Cloth stripe", new Vector3(x, .9f, z), new Vector3(2.64f, 1.84f, .22f), YardKit.Gold, Solid.None);
                    YardKit.Box(s, "Rope", new Vector3(x, 1.4f, z), new Vector3(2.66f, .06f, w * .87f), YardKit.WoodDark, Solid.None);
                    break;
                default:
                    for (int i = 0; i < 3; i++)
                        YardKit.Box(s, "Hay bale", new Vector3(x, .45f + (i == 2 ? .9f : 0f), z + (i == 2 ? 0f : (i - .5f) * w * .48f)),
                                    new Vector3(2.4f, .9f, w * .44f), Straw, Solid.None);
                    break;
            }
        }
    }
}
