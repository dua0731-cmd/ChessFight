using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // The Royal Carriage Yard's dressing: towers with blue roofs, battlements, banners, parked carriages, crates, trees
    // and the palace beyond the walls. Pictures, except the towers, carriages and crates that stand where a character
    // can reach them (solid, never climbed). Only the look: none of it is in the way of the race.
    public sealed partial class RoyalCarriageYard
    {
        void Scenery()
        {
            var s = Group("Scenery");
            // Battlements on the walls a player sees from inside.
            Merlons(s, -23f, 21f, 52f, 5f);
            Merlons(s, 22f, 21f, 52f, 5f);
            Merlons(s, -27f, 86f, 176f, Top + 3f);
            Merlons(s, 26f, 86f, 176f, Top + 3f);
            // Banners on the long walls, facing in.
            for (float z = 26f; z < 50f; z += 8f)
            {
                BannerOn(s, new Vector3(-21.9f, 4.3f, z), 90f);
                BannerOn(s, new Vector3(21.9f, 4.3f, z), -90f);
            }
            for (float z = 92f; z < 170f; z += 12f)
            {
                BannerOn(s, new Vector3(-25.9f, 9.5f, z), 90f);
                BannerOn(s, new Vector3(25.9f, 9.5f, z), -90f);
            }
            foreach (float x in new[] { -18f, -12f, 12f, 18f })
                BannerOn(s, new Vector3(x, Top + 9.5f, 171.9f), 180f);
            // Lamp posts along the yard and at the bridges.
            foreach (var at in new[] { new Vector3(-6f, 0f, 24f), new Vector3(6f, 0f, 24f), new Vector3(-6f, 0f, 50f), new Vector3(6f, 0f, 50f),
                                       new Vector3(-24.5f, 0f, 88f), new Vector3(24.5f, 0f, 88f), new Vector3(-8f, Top, 151.5f), new Vector3(8f, Top, 151.5f) })
                LampPost(s, at);
            // Corner towers and the palace behind the finish (beyond the walls, pictures only).
            foreach (var at in new[] { new Vector3(-23f, 0f, 21f), new Vector3(23f, 0f, 21f), new Vector3(-27f, 0f, 85f), new Vector3(27f, 0f, 85f),
                                       new Vector3(-27f, 0f, 176f), new Vector3(27f, 0f, 176f) })
                Tower(s, at, 4f, at.z > 100f ? 19f : 12f, false);
            Palace(s);
        }

        // A round tower standing on `at`, a blue cone on top. Solid where players can reach it.
        void Tower(Transform s, Vector3 at, float diameter, float height, bool solid = true)
        {
            YardKit.Cylinder(s, "Tower", at + Vector3.up * (height * .5f), Vector3.up, diameter, height, YardKit.WallStone, solid ? Solid.NoClimb : Solid.None);
            YardKit.Cylinder(s, "Tower band", at + Vector3.up * (height - .3f), Vector3.up, diameter + .2f, .3f, YardKit.Gold, Solid.None);
            YardKit.Cone(s, "Tower roof", at + Vector3.up * height, diameter + .6f, diameter * 1.1f, YardKit.Roof);
            YardKit.Ball(s, "Tower finial", at + Vector3.up * (height + diameter * 1.1f + .15f), .35f, YardKit.Gold, Solid.None);
        }

        // Teeth along the top of a wall running along z at x (its inner face at x or x+1).
        static void Merlons(Transform s, float x, float z0, float z1, float top)
        {
            for (float z = z0 + .5f; z < z1 - .5f; z += 2f)
                YardKit.Picture(s, "Merlon", new Vector3(x, top, z), new Vector3(x + 1f, top + .8f, z + 1f), YardKit.WallStone);
        }

        // A hanging banner on a wall, its face to local +z after `yaw`.
        static void BannerOn(Transform s, Vector3 at, float yaw)
        {
            var b = new GameObject("Banner").transform;
            b.SetParent(s, false);
            b.localPosition = at;
            b.localRotation = Quaternion.Euler(0f, yaw, 0f);
            YardKit.Box(b, "Cloth", new Vector3(0f, -1.2f, .06f), new Vector3(1.4f, 2.6f, .05f), YardKit.Banner, Solid.None);
            YardKit.Box(b, "Stripe", new Vector3(0f, -1.2f, .09f), new Vector3(.25f, 2.6f, .02f), YardKit.Gold, Solid.None);
            YardKit.Box(b, "Crown", new Vector3(0f, -.7f, .1f), new Vector3(.6f, .4f, .02f), YardKit.Gold, Solid.None);
            YardKit.Box(b, "Rod", new Vector3(0f, .1f, .1f), new Vector3(1.7f, .08f, .08f), YardKit.Gold, Solid.None);
        }

        static void LampPost(Transform s, Vector3 at)
        {
            YardKit.Cylinder(s, "Lamp post", at + Vector3.up * 1.6f, Vector3.up, .18f, 3.2f, YardKit.Iron, Solid.NoClimb);
            YardKit.Box(s, "Lamp", at + Vector3.up * 3.35f, new Vector3(.4f, .5f, .4f), YardKit.Lamp, Solid.None);
            YardKit.Cone(s, "Lamp cap", at + Vector3.up * 3.6f, .6f, .35f, YardKit.Iron);
        }

        // A carriage left in the yard: the picture and its solid box, nothing that moves or hits.
        static void ParkedCarriage(Transform s, Vector3 at, float yaw, Material body)
        {
            var c = new GameObject("Parked carriage").transform;
            c.SetParent(s, false);
            c.localPosition = at;
            c.localRotation = Quaternion.Euler(0f, yaw, 0f);
            YardKit.Carriage(c, body);
            YardKit.Box(c, "Carriage (collider)", new Vector3(0f, YardKit.CarriageHeight * .5f, 0f),
                        new Vector3(YardKit.CarriageWidth, YardKit.CarriageHeight, YardKit.CarriageLength), null, Solid.NoClimb)
                   .GetComponent<MeshRenderer>().enabled = false;
        }

        // A stack of crates: `count` in a row along z, every other one with one more on top.
        static void Crates(Transform s, Vector3 at, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var p = at + new Vector3(0f, 0f, i * 1.6f);
                YardKit.Box(s, "Crate", p + Vector3.up * .7f, Vector3.one * 1.4f, YardKit.WoodLight, Solid.NoClimb);
                YardKit.Box(s, "Crate band", p + Vector3.up * .7f, new Vector3(1.44f, .14f, 1.44f), YardKit.WoodDark, Solid.None);
                if (i % 2 == 0) YardKit.Box(s, "Crate", p + new Vector3(.1f, 1.95f, 0f), Vector3.one * 1.1f, YardKit.WoodDark, Solid.NoClimb);
            }
        }

        static void Tree(Transform s, Vector3 at)
        {
            YardKit.Cylinder(s, "Trunk", at + Vector3.up * .7f, Vector3.up, .3f, 1.4f, YardKit.WoodDark, Solid.None);
            YardKit.Cone(s, "Crown", at + Vector3.up * 1.1f, 1.8f, 3.2f, YardKit.Hedge);
        }

        // The palace the race runs towards, behind the finish: a keep, wings and towers, pictures only.
        void Palace(Transform parent)
        {
            var s = Group("Palace", parent);
            YardKit.Picture(s, "Keep", new Vector3(-12f, 0f, 200f), new Vector3(12f, Top + 20f, 214f), YardKit.WallStone);
            YardKit.Picture(s, "Keep roof band", new Vector3(-12.2f, Top + 19.5f, 199.8f), new Vector3(12.2f, Top + 20.2f, 214.2f), YardKit.Gold);
            for (int i = 0; i < 5; i++)
                YardKit.Picture(s, "Keep window", new Vector3(-9f + i * 4.2f, Top + 10f, 199.9f), new Vector3(-7.6f + i * 4.2f, Top + 14f, 200f), YardKit.Glass);
            YardKit.Picture(s, "Wing W", new Vector3(-34f, 0f, 196f), new Vector3(-12f, Top + 12f, 208f), YardKit.WallStone);
            YardKit.Picture(s, "Wing E", new Vector3(12f, 0f, 196f), new Vector3(34f, Top + 12f, 208f), YardKit.WallStone);
            foreach (var at in new[] { new Vector3(-12f, Top, 200f), new Vector3(12f, Top, 200f), new Vector3(-12f, Top, 214f), new Vector3(12f, Top, 214f) })
                Tower(s, at, 5f, 26f, false);
            foreach (var at in new[] { new Vector3(-34f, 0f, 196f), new Vector3(34f, 0f, 196f) })
                Tower(s, at, 5f, 24f, false);
            Tower(s, new Vector3(0f, Top + 20f, 207f), 6f, 10f, false);
        }
    }
}
