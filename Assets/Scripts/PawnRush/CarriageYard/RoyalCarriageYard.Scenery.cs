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
            var own = Group("Scenery (own frame)", later);
            // Battlements on the walls a player sees from inside: the yard's and the canal's, the arena's.
            Merlons(s, -YardHalf - 1f, 0f, CanalZ1, 5f);
            Merlons(s, YardHalf, 0f, CanalZ1, 5f);
            Merlons(own, -27f, ArenaStart, 176f, Top + 3f);
            Merlons(own, 26f, ArenaStart, 176f, Top + 3f);
            // Banners: on the gate wall's yard face (where the carriages keep off), along the arena, on the final gate's
            // wall. None on the yard's side walls: a charging carriage would run through them.
            foreach (float x in new[] { -17f, -10f, 10f, 17f })
                BannerOn(s, new Vector3(x, 5.6f, YardZ0 + .02f), 0f);
            for (float z = ArenaStart + 6f; z < 170f; z += 12f)
            {
                BannerOn(own, new Vector3(-25.9f, 9.5f, z), 90f);
                BannerOn(own, new Vector3(25.9f, 9.5f, z), -90f);
            }
            foreach (float x in new[] { -18f, -12f, 12f, 18f })
                BannerOn(own, new Vector3(x, Top + 9.5f, 171.9f), 180f);
            // Lamp posts on the canal's landings, at the arena's mouth, on the platform (none in the yard: the carriages).
            foreach (var at in new[] { new Vector3(-9f, 0f, CanalZ0 + 1f), new Vector3(9f, 0f, CanalZ0 + 1f),
                                       new Vector3(-9f, 0f, CanalZ1 - 1f), new Vector3(9f, 0f, CanalZ1 - 1f) })
                LampPost(s, at);
            foreach (var at in new[] { new Vector3(-24.5f, 0f, ArenaStart + 2f), new Vector3(24.5f, 0f, ArenaStart + 2f),
                                       new Vector3(-8f, Top, 151.5f), new Vector3(8f, Top, 151.5f) })
                LampPost(own, at);
            // Corner towers outside the walls (pictures only) and the palace behind the finish.
            foreach (var at in new[] { new Vector3(-24f, 0f, 0f), new Vector3(24f, 0f, 0f), new Vector3(-28f, 0f, CanalZ1), new Vector3(28f, 0f, CanalZ1) })
                Tower(s, at, 4f, 12f, false);
            foreach (var at in new[] { new Vector3(-28f, 0f, 176f), new Vector3(28f, 0f, 176f) })
                Tower(own, at, 4f, 19f, false);
            Palace(own);
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
