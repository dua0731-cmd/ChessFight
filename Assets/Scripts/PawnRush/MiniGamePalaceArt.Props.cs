using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class MiniGamePalaceArt
    {
        static void EntryProps(Transform parent, int game)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var root = Group(parent, "DEC_ThresholdProps", new Vector3(side * 18.5f, .04f, 1.8f));
                switch (game)
                {
                    case PawnRushMissions.Drawbridge:
                        for (int i = 0; i < 4; i++) Ring(root, "Spare rope coil", new Vector3(-1.5f, .1f + i * .065f, 0f), .48f - i * .035f, M("CoiledRope", "#AA8C60"));
                        Cog(root, new Vector3(0f, .45f, .35f), .6f);
                        Ring(root, "Neutral life buoy", new Vector3(1.2f, .6f, .4f), .5f, Cloth).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        for (int i = 0; i < 4; i++)
                        {
                            float a = i * Mathf.PI * .5f;
                            var stripe = Box(root, "Buoy neutral stripe", new Vector3(1.2f + Mathf.Cos(a) * .5f, .6f + Mathf.Sin(a) * .5f, .4f), new Vector3(.19f, .19f, .16f), Iron);
                            stripe.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                        }
                        break;
                    case PawnRushMissions.Capstan:
                        Box(root, "Spare bar rack", new Vector3(-1f, .7f, .5f), new Vector3(1.3f, 1.4f, .2f), Wood);
                        for (int i = 0; i < 4; i++) Box(root, "Spare capstan bar", new Vector3(-1f, .25f + i * .3f, .22f), new Vector3(1.9f, .12f, .12f), Maple);
                        for (int i = 0; i < 5; i++) Ring(root, "Spare chain coil", new Vector3(.7f, .1f + i * .07f, .1f), .4f, Iron);
                        Bench(root, new Vector3(1.8f, 0f, .5f), 90f, false);
                        Ball(root, "Guard helmet", new Vector3(1.8f, 1f, .5f), .3f, Iron);
                        Box(root, "Helmet visor", new Vector3(1.8f, 1f, .22f), new Vector3(.42f, .09f, .05f), M("HelmetVisor", "#1C2027"));
                        break;
                    case PawnRushMissions.Paint:
                        PaintPot(root, new Vector3(-1.1f, 0f, 0f), Cloth);
                        PaintPot(root, new Vector3(-.3f, 0f, -.2f), Iron);
                        Box(root, "Brush rack uprights", new Vector3(1f, .7f, .5f), new Vector3(1.5f, 1.4f, .15f), Wood);
                        for (int i = 0; i < 4; i++)
                        {
                            Box(root, "Brush handle", new Vector3(.4f + i * .4f, .9f, .3f), new Vector3(.065f, .9f, .06f), Maple);
                            Box(root, "Brush bristles", new Vector3(.4f + i * .4f, .4f, .3f), new Vector3(.2f, .2f, .07f), Cloth);
                        }
                        Cylinder(root, "Rolled ceremonial carpet", new Vector3(2.4f, .34f, 0f), .34f, 1.5f, Violet).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                        break;
                    case PawnRushMissions.Planks:
                        Table(root, new Vector3(-.9f, 0f, 0f), 1.8f, .9f);
                        Box(root, "Saw blade", new Vector3(-.9f, .92f, 0f), new Vector3(1.2f, .08f, .15f), Iron);
                        Box(root, "Saw handle", new Vector3(-.2f, .95f, 0f), new Vector3(.3f, .15f, .25f), Wood);
                        Chest(root, new Vector3(1f, 0f, .2f), false);
                        Cylinder(root, "Rolled dust sheet", new Vector3(2.4f, .3f, 0f), .3f, 1.4f, Cloth).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                        break;
                    case PawnRushMissions.Bells:
                        Table(root, new Vector3(-1f, 0f, .2f), 1.5f, .8f);
                        for (int i = 0; i < 4; i++) Box(root, "Closed book stack", new Vector3(-1f + i % 2 * .12f, .95f + i * .12f, .2f), new Vector3(.8f, .1f, .55f), i % 2 == 0 ? Wood : M("BookCloth", "#7E8374"));
                        Table(root, new Vector3(1f, 0f, .2f), 1.3f, .8f);
                        Ball(root, "Library globe", new Vector3(1f, 1.35f, .2f), .4f, M("Globe", "#78928F"));
                        Ring(root, "Globe meridian", new Vector3(1f, 1.35f, .2f), .47f, Brass).transform.localRotation = Quaternion.Euler(65f, 0f, 20f);
                        Box(root, "Book trolley handle", new Vector3(-1.7f, 1.1f, .2f), new Vector3(.08f, .65f, .75f), Brass);
                        foreach (float x in new[] { -1.55f, -.45f }) Cylinder(root, "Book trolley wheel", new Vector3(x, .15f, 0f), .12f, .08f, Iron).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                        break;
                    case PawnRushMissions.EnPassant:
                        Cylinder(root, "Music stand stem", new Vector3(-1.2f, .7f, 0f), .035f, 1.4f, Iron);
                        Box(root, "Music score rest", new Vector3(-1.2f, 1.4f, 0f), new Vector3(.8f, .55f, .05f), Wood).transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
                        Box(root, "Open music score", new Vector3(-1.2f, 1.43f, -.04f), new Vector3(.65f, .42f, .02f), Cloth);
                        Cylinder(root, "Coat rack stem", new Vector3(.3f, .9f, .3f), .04f, 1.8f, Wood);
                        for (int i = 0; i < 3; i++)
                        {
                            float a = i * Mathf.PI * 2f / 3f;
                            Beam(root, "Coat hook", new Vector3(.3f, 1.65f, .3f), new Vector3(.3f + Mathf.Cos(a) * .4f, 1.8f, .3f + Mathf.Sin(a) * .4f), .05f, Brass);
                        }
                        Table(root, new Vector3(1.8f, 0f, .2f), 1.3f, .7f);
                        Cylinder(root, "Drink tray", new Vector3(1.8f, .94f, .2f), .48f, .035f, Brass);
                        for (int i = 0; i < 3; i++) Cylinder(root, "Tray glass", new Vector3(1.5f + i * .25f, 1.09f, .2f), .08f, .28f, Glass);
                        break;
                    case PawnRushMissions.KnightPads:
                        Basket(root, new Vector3(-1.1f, 0f, .1f));
                        Table(root, new Vector3(.7f, 0f, .3f), 1.3f, .8f);
                        for (int i = 0; i < 4; i++) Box(root, "Folded white linen", new Vector3(.7f, .96f + i * .1f, .3f), new Vector3(.9f - i * .08f, .09f, .55f), Cloth);
                        var board = Box(root, "Wash board", new Vector3(2.1f, .65f, .1f), new Vector3(.65f, 1.2f, .1f), Maple);
                        board.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
                        for (int i = 0; i < 7; i++) Box(root, "Washboard ribs", new Vector3(2.1f, .24f + i * .12f, -.04f), new Vector3(.52f, .04f, .04f), Iron);
                        Box(root, "Soap crate", new Vector3(1.6f, .22f, -.55f), new Vector3(.65f, .44f, .5f), Wood);
                        break;
                    case PawnRushMissions.KingEscort:
                        foreach (float x in new[] { -1.3f, 1.3f })
                        {
                            Cylinder(root, "Velvet stanchion foot", new Vector3(x, .08f, .3f), .25f, .16f, Brass);
                            Cylinder(root, "Velvet stanchion", new Vector3(x, .65f, .3f), .045f, 1.2f, Brass);
                            Ball(root, "Stanchion finial", new Vector3(x, 1.3f, .3f), .12f, Brass);
                        }
                        Beam(root, "Velvet cord", new Vector3(-1.3f, 1.1f, .3f), new Vector3(1.3f, 1.1f, .3f), .08f, Violet);
                        Box(root, "Folded procession flag", new Vector3(.5f, .18f, -.5f), new Vector3(1.3f, .35f, .65f), Violet);
                        Beam(root, "Ceremonial trumpet tube", new Vector3(-.7f, .5f, -.6f), new Vector3(.6f, .5f, -.6f), .12f, Brass);
                        Cylinder(root, "Trumpet bell", new Vector3(-.8f, .5f, -.6f), .25f, .2f, Brass).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                        break;
                    case PawnRushMissions.Checkmate:
                        Table(root, new Vector3(-.9f, 0f, 0f), 2f, .8f);
                        Box(root, "Chess clock body", new Vector3(-.9f, 1.27f, 0f), new Vector3(1.65f, .75f, .4f), Wood);
                        foreach (float x in new[] { -1.32f, -.48f })
                        {
                            var clock = Ring(root, "Chess clock dial", new Vector3(x, 1.27f, -.25f), .27f, Brass);
                            clock.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                            Box(root, "Clock hand", new Vector3(x, 1.35f, -.27f), new Vector3(.025f, .2f, .04f), Iron);
                            Cylinder(root, "Clock button", new Vector3(x, 1.73f, 0f), .09f, .13f, Brass);
                        }
                        Chest(root, new Vector3(1.2f, 0f, .1f), true);
                        var tablet = Box(root, "Stone score tablet", new Vector3(2.5f, .8f, .5f), new Vector3(.8f, 1.4f, .12f), Stone);
                        tablet.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
                        Crown(root, new Vector3(2.5f, .85f, .34f), .35f);
                        break;
                    case PawnRushMissions.CheckBlock:
                        Table(root, new Vector3(-.9f, 0f, .3f), 1.1f, .8f);
                        Cylinder(root, "Bedside candle", new Vector3(-.9f, 1.12f, .3f), .07f, .4f, Cloth);
                        Ball(root, "Candle flame", new Vector3(-.9f, 1.38f, .3f), .07f, Lamp);
                        foreach (float x in new[] { .8f, 1.15f }) Box(root, "Royal slipper", new Vector3(x, .1f, -.3f), new Vector3(.24f, .2f, .5f), Cloth);
                        Box(root, "Folded royal gown", new Vector3(1.7f, .2f, .5f), new Vector3(.9f, .4f, .7f), Violet);
                        break;
                }
            }
        }

        static void Table(Transform parent, Vector3 at, float width, float depth)
        {
            Box(parent, "Table top", at + Vector3.up * .85f, new Vector3(width, .14f, depth), Wood);
            foreach (float x in new[] { -width * .38f, width * .38f }) foreach (float z in new[] { -depth * .32f, depth * .32f })
                Box(parent, "Table leg", at + new Vector3(x, .4f, z), new Vector3(.12f, .8f, .12f), Wood);
        }

        static void Chest(Transform parent, Vector3 at, bool open)
        {
            Box(parent, "Chest body", at + Vector3.up * .35f, new Vector3(1.1f, .7f, .75f), Wood);
            var lid = Group(parent, "Chest lid hinge", at + new Vector3(0f, .7f, .38f));
            if (open) lid.localRotation = Quaternion.Euler(-60f, 0f, 0f);
            Box(lid, "Chest lid", new Vector3(0f, .07f, -.38f), new Vector3(1.15f, .14f, .8f), Wood);
            foreach (float x in new[] { -.4f, .4f }) Box(parent, "Chest iron strap", at + new Vector3(x, .35f, -.4f), new Vector3(.08f, .66f, .06f), Iron);
            Box(parent, "Chest lock", at + new Vector3(0f, .52f, -.41f), new Vector3(.17f, .2f, .07f), Brass);
        }

        static void PaintPot(Transform parent, Vector3 at, Material paint)
        {
            Cylinder(parent, "Paint pail", at + Vector3.up * .26f, .3f, .52f, Wood);
            Cylinder(parent, "Paint surface", at + Vector3.up * .53f, .28f, .04f, paint);
            Ring(parent, "Pail rim", at + Vector3.up * .54f, .31f, Brass);
            Ring(parent, "Pail handle", at + Vector3.up * .55f, .29f, Iron).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        static void Basket(Transform parent, Vector3 at)
        {
            Cylinder(parent, "Laundry basket body", at + Vector3.up * .35f, .5f, .7f, Maple);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * .25f;
                Box(parent, "Basket reed", at + new Vector3(Mathf.Cos(a) * .5f, .35f, Mathf.Sin(a) * .5f), new Vector3(.04f, .62f, .04f), Wood);
            }
            Ring(parent, "Basket rim", at + Vector3.up * .71f, .52f, Wood);
            Box(parent, "Basket linen", at + new Vector3(0f, .71f, 0f), new Vector3(.6f, .15f, .45f), Cloth);
        }

        static void Cog(Transform parent, Vector3 at, float radius)
        {
            var cog = Group(parent, "Spare winch cog", at);
            Ring(cog, "Cog rim", Vector3.zero, radius * .75f, Brass).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                var tooth = Box(cog, "Cog tooth", new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f), new Vector3(.18f, .18f, .15f), Brass);
                tooth.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            }
        }
    }
}
