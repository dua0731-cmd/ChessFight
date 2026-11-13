using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush
{
    public static partial class MiniGamePalaceArt
    {
        public const string CeilingName = "ENV_Ceiling_InspectionToggle";

        public static void BuildShell(Transform content, CourseKit kit, int game)
        {
            if (content == null) return;
            var old = content.Find(ShellName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = Group(content, ShellName);
            bool early = Early(game), late = Late(game);
            float height = early ? 11f : late ? 17f : 13f;
            Paving(root, early, late);
            Facade(root, height, early, late);
            SideWalls(root, height, early, late, game);
            EntryFrame(root, height, early);
            RearPassage(root, height, early, late);
            if (early) GardenEdge(root);
            else InteriorCeiling(root, height, late);
            EntryProps(root, game);
            if (game == PawnRushMissions.Drawbridge)
            {
                var raised = Group(root, "DEC_CentralFixedDrawbridge", new Vector3(0f, .25f, 20f));
                raised.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Deck(raised, 3.6f, 4f);
                for (int side = -1; side <= 1; side += 2)
                {
                    var pylon = Group(root, "DEC_CentralRookPylon", new Vector3(side * 2.6f, 0f, 20f));
                    RookPylon(pylon, 3.1f);
                }
            }
        }

        static void Paving(Transform root, bool early, bool late)
        {
            var paving = Group(root, "ENV_EntryAndRearPaving");
            var light = early ? Cream : Marble;
            var dark = M("PavingCool", late ? "#A7AAA9" : "#A0A5A6", 0f, .82f);
            for (int x = -11; x < 11; x++)
                for (int z = 0; z < 18; z++)
                {
                    if (z >= 2 && z < 15) continue;
                    Box(paving, "Paving module", new Vector3(x * 2f + 1f, .012f, z * 2f + 1f), new Vector3(1.97f, .024f, 1.97f), early || ((x + z) & 1) == 0 ? light : dark);
                }
            if (late)
            {
                Box(paving, "Central velvet runner", new Vector3(0f, .022f, 18f), new Vector3(2.4f, .035f, 36f), Violet);
                foreach (float x in new[] { -1.24f, 1.24f }) Box(paving, "Runner selvedge", new Vector3(x, .045f, 18f), new Vector3(.07f, .018f, 36f), Brass);
            }
        }

        static void Facade(Transform root, float height, bool early, bool late)
        {
            var front = Group(root, "ENV_NorthFacade");
            foreach (var span in new[] { new Vector2(-22f, -14.35f), new Vector2(-9.65f, 9.65f), new Vector2(14.35f, 22f) })
            {
                Box(front, "North wall mass", new Vector3((span.x + span.y) * .5f, height * .5f, 25f), new Vector3(span.y - span.x, height, 1.9f), early ? Stone : Marble);
                Masonry(front, span.x, span.y, .1f, height, 23.99f, early ? Cream : Marble);
            }
            foreach (float x in new[] { -12f, 12f })
            {
                Box(front, "Portal upper wall", new Vector3(x, (height + 6f) * .5f, 25f), new Vector3(4.7f, height - 6f, 1.9f), early ? Cream : Marble);
                Arch(front, "Team portal front", x, 23.8f, 4.5f, 5.8f, early ? Cream : Marble);
                Arch(front, "Team portal rear", x, 29.9f, 4.5f, 5.8f, early ? Cream : Marble);
                foreach (float side in new[] { -1f, 1f })
                    Box(front, "Portal tunnel lining", new Vector3(x + side * 2.22f, 2.8f, 27f), new Vector3(.12f, 5.6f, 6f), Stone);
                Box(front, "Gate pocket cover", new Vector3(x, 8.1f, 26.5f), new Vector3(5.1f, 4.2f, 2.4f), early ? Cream : Marble);
                var team = M(x < 0f ? "GateBannerWhite" : "GateBannerBlack", x < 0f ? "#3FC1D9" : "#F08A2C");
                Banner(front, new Vector3(x, 8.7f, 23.35f), 2.35f, 2.3f, team, true);
                Sconce(front, new Vector3(x - 2.9f, 3.7f, 23.1f));
                Sconce(front, new Vector3(x + 2.9f, 3.7f, 23.1f));
            }
            var mainDoor = Group(front, "DEC_ClosedCeremonialDoor", new Vector3(0f, 0f, 23.69f));
            Box(mainDoor, "Closed central door", new Vector3(0f, 3.8f, 0f), new Vector3(6.7f, 7.6f, .22f), late ? M("RoyalDoor", "#28344D") : Wood);
            for (int i = -3; i <= 3; i++) Box(mainDoor, "Door oak board", new Vector3(i * .92f, 3.8f, -.15f), new Vector3(.86f, 7.25f, .1f), late ? M("RoyalDoorInset", "#32425A") : Wood);
            foreach (float y in new[] { 1f, 3f, 5.1f, 7f })
            {
                Box(mainDoor, "Iron door strap", new Vector3(0f, y, -.24f), new Vector3(6.4f, .14f, .1f), Iron);
                foreach (float x in new[] { -2.9f, -1.8f, 1.8f, 2.9f }) Ball(mainDoor, "Door strap rivet", new Vector3(x, y, -.32f), .075f, Brass);
            }
            foreach (float x in new[] { -.38f, .38f })
            {
                var ring = Ring(mainDoor, "Central door ring", new Vector3(x, 3.2f, -.35f), .2f, Brass);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            Arch(front, "Crown arch", 0f, 23.25f, 7.5f, 9.4f, early ? Cream : Marble);
            Crown(front, new Vector3(0f, 10f, 23.15f), 2f);
            foreach (float x in new[] { -21f, -16.2f, -7.3f, 7.3f, 16.2f, 21f })
                Pilaster(front, new Vector3(x, 0f, 23.4f), height, early ? Cream : Marble);
            foreach (float y in new[] { .25f, height - .4f })
                Box(front, "Facade cornice", new Vector3(0f, y, 23.55f), new Vector3(44.5f, .4f, .55f), Stone);
            if (early)
            {
                foreach (float x in new[] { -19f, 19f })
                {
                    var tower = Group(front, "ENV_RookGatehouse", new Vector3(x, 0f, 25f));
                    Box(tower, "Tower body", new Vector3(0f, 5.8f, 0f), new Vector3(4.3f, 11.6f, 4.2f), Cream);
                    Box(tower, "Tower cap", new Vector3(0f, 11.8f, 0f), new Vector3(4.7f, .4f, 4.6f), Stone);
                    for (int i = 0; i < 4; i++) Box(tower, "Neutral slate roof tier", new Vector3(0f, 12.2f + i * .38f, 0f), new Vector3(4.8f - i * .9f, .43f, 4.7f - i * .9f), M("SlateRoof", "#535A62"));
                }
            }
            else
            {
                Box(front, "Royal emblem back", new Vector3(0f, height - 1.65f, 22.98f), new Vector3(3.8f, 2.7f, .12f), late ? Violet : M("EmblemNavy", "#344350"));
                Crown(front, new Vector3(0f, height - 1.65f, 22.8f), .85f);
                int ranks = late ? 8 : 6;
                for (int i = 0; i < ranks; i++) Box(front, "Rank insignia", new Vector3((i - (ranks - 1) * .5f) * .34f, height - 2.5f, 22.8f), new Vector3(.2f, .2f + i * .025f, .08f), Brass);
                Balcony(front, new Vector3(0f, late ? 10.7f : 8.3f, 26.8f), 43f, 3f);
            }
        }

        static void Masonry(Transform parent, float x0, float x1, float y0, float y1, float z, Material material)
        {
            for (float y = y0; y < y1 - .2f; y += 1.05f)
                for (float x = x0; x < x1 - .15f; x += 2.4f)
                {
                    float w = Mathf.Min(2.34f, x1 - x - .045f), h = Mathf.Min(1f, y1 - y);
                    if (w > .12f) Box(parent, "Ashlar face", new Vector3(x + w * .5f, y + h * .5f, z), new Vector3(w, h, .14f), material);
                }
        }

        static void Arch(Transform parent, string name, float x, float z, float width, float height, Material material)
        {
            var arch = Group(parent, name, new Vector3(x, 0f, z));
            float radius = width * .5f, spring = height - radius;
            foreach (float side in new[] { -1f, 1f })
                for (float y = .4f; y < spring; y += .8f)
                    Box(arch, "Arch jamb block", new Vector3(side * (radius + .3f), Mathf.Min(spring - .1f, y), 0f), new Vector3(.58f, .75f, .66f), material);
            for (int i = 0; i < 15; i++)
            {
                float a = (i + .5f) * Mathf.PI / 15f;
                var stone = Box(arch, "Arch voussoir", new Vector3(Mathf.Cos(a) * (radius + .28f), spring + Mathf.Sin(a) * (radius + .28f), 0f), new Vector3(.64f, .62f, .7f), material);
                stone.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f);
            }
            Box(arch, "Arch keystone", new Vector3(0f, height + .25f, -.04f), new Vector3(.72f, .85f, .82f), Stone);
        }

        static void Pilaster(Transform parent, Vector3 at, float height, Material material)
        {
            Box(parent, "Column plinth", at + Vector3.up * .32f, new Vector3(1.4f, .64f, 1.15f), Stone);
            Box(parent, "Column shaft", at + Vector3.up * (height * .5f), new Vector3(.85f, height - 1.1f, .65f), material);
            Box(parent, "Column capital", at + Vector3.up * (height - .55f), new Vector3(1.3f, .42f, 1.1f), Stone);
            Box(parent, "Column brass fillet", at + Vector3.up * (height - .8f), new Vector3(1.05f, .09f, .85f), Brass);
        }

        static void Crown(Transform parent, Vector3 at, float scale)
        {
            var crown = Group(parent, "Crown emblem", at);
            crown.localScale = Vector3.one * scale;
            Box(crown, "Crown band", Vector3.zero, new Vector3(1.2f, .22f, .16f), Brass);
            for (int i = -2; i <= 2; i++)
            {
                float h = i == 0 ? .65f : Mathf.Abs(i) == 1 ? .42f : .56f;
                var arm = Box(crown, "Crown point", new Vector3(i * .26f, .16f + h * .5f, 0f), new Vector3(.13f, h, .15f), Brass);
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, -i * 9f);
                Ball(crown, "Crown jewel", new Vector3(i * .29f, .16f + h, 0f), .1f, Brass);
            }
        }

        static void Banner(Transform parent, Vector3 at, float width, float height, Material fabric, bool pawn)
        {
            Box(parent, "Banner cloth", at - Vector3.up * height * .5f, new Vector3(width, height, .075f), fabric);
            Box(parent, "Banner rod", at + Vector3.up * .08f, new Vector3(width + .45f, .11f, .12f), Brass);
            foreach (float x in new[] { -width * .43f, width * .43f }) Box(parent, "Banner border", at + new Vector3(x, -height * .5f, -.05f), new Vector3(.055f, height - .16f, .025f), Brass);
            if (pawn)
            {
                Ball(parent, "Banner pawn head", at + new Vector3(0f, -height * .4f, -.09f), width * .12f, Cloth);
                Box(parent, "Banner pawn body", at + new Vector3(0f, -height * .65f, -.09f), new Vector3(width * .27f, height * .3f, .05f), Cloth);
            }
        }

        static void SideWalls(Transform root, float height, bool early, bool late, int game)
        {
            float wallHeight = early ? 3.2f : height;
            foreach (float side in new[] { -1f, 1f })
            {
                var wall = Group(root, "ENV_SideWall", new Vector3(side * 22.2f, 0f, 12f));
                Box(wall, "Wall backing", new Vector3(side * .35f, wallHeight * .5f, 0f), new Vector3(.65f, wallHeight, 25f), early ? Cream : Marble);
                for (int i = 0; i < 6; i++)
                {
                    float z = -10f + i * 4f;
                    var post = Group(wall, "Wall bay", new Vector3(0f, 0f, z));
                    post.localRotation = Quaternion.Euler(0f, side < 0f ? 90f : -90f, 0f);
                    Pilaster(post, Vector3.zero, early ? 4f : height, early ? Cream : Marble);
                    if (early)
                    {
                        for (int c = -1; c <= 1; c += 2) Box(wall, "Battlement", new Vector3(-side * .08f, wallHeight + .35f, z + c * .62f), new Vector3(.9f, .9f, .7f), Cream);
                    }
                    else
                    {
                        Box(wall, "Walnut wainscot", new Vector3(-side * .4f, 1.4f, z + 1.7f), new Vector3(.14f, 2.8f, 3.05f), late ? M("LowMarble", "#9E9E99") : Wood);
                        for (int j = 0; j < 3; j++)
                            Box(wall, "Wainscot inset", new Vector3(-side * .5f, 1.4f, z + .65f + j), new Vector3(.06f, 2.15f, .82f), late ? Marble : M("WalnutInset", "#76523A", 0f, .9f, "PR_Wood"));
                        var window = Group(wall, "ENV_HighWindow", new Vector3(-side * .36f, late ? 10f : 7f, z + 1.7f));
                        Box(window, "Window blue glass", Vector3.zero, new Vector3(.09f, late ? 8f : 5.4f, 2.65f), Glass);
                        foreach (float dz in new[] { -1.4f, 0f, 1.4f }) Box(window, "Window mullion", new Vector3(-side * .08f, 0f, dz), new Vector3(.14f, late ? 8.4f : 5.8f, .11f), Cream);
                        foreach (float dy in new[] { -1.5f, 1.5f }) Box(window, "Window transom", new Vector3(-side * .08f, dy, 0f), new Vector3(.14f, .11f, 2.9f), Cream);
                        if (late && (i & 1) == 0)
                        {
                            foreach (float dz in new[] { -1.65f, 1.65f })
                            {
                                Box(window, "Tied royal curtain", new Vector3(-side * .3f, .3f, dz), new Vector3(.45f, 8.8f, .5f), Violet);
                                Box(window, "Curtain tie", new Vector3(-side * .32f, -1f, dz), new Vector3(.49f, .15f, .57f), Brass);
                            }
                        }
                    }
                }
            }
        }

        static void RearPassage(Transform root, float height, bool early, bool late)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Box(root, "Rear passage side wall", new Vector3(side * 22.25f, (early ? 3.2f : height) * .5f, 30f), new Vector3(.5f, early ? 3.2f : height, 12f), early ? Cream : Marble);
                Box(root, "Rear passage end wall", new Vector3(side * 14.15f, (early ? 7f : height) * .5f, 36.3f), new Vector3(15.7f, early ? 7f : height, .6f), early ? Cream : Marble);
            }
            foreach (float x in new[] { -19f, -6.5f, 6.5f, 19f })
            {
                Pilaster(root, new Vector3(x, 0f, 35.8f), early ? 7f : height - 1f, Cream);
                Sconce(root, new Vector3(x, 3f, 35.2f));
            }
            Arch(root, "ENV_CommonExitArch", 0f, 36f, 12.5f, early ? 7.5f : 9f, early ? Cream : Marble);
            foreach (float x in new[] { -15f, 15f }) Banner(root, new Vector3(x, early ? 6f : 10f, 35.55f), 1.7f, 3f, late ? Violet : Cloth, true);
        }

        static void EntryFrame(Transform root, float height, bool early)
        {
            foreach (float side in new[] { -1f, 1f })
                Box(root, "Entry wing wall", new Vector3(side * 14.15f, (early ? 3.2f : height) * .5f, -.4f), new Vector3(15.7f, early ? 3.2f : height, .7f), early ? Cream : Marble);
            Arch(root, "ENV_OpenEntryArch", 0f, -.25f, 12.5f, 7.5f, early ? Cream : Marble);
        }

        static void GardenEdge(Transform root)
        {
            foreach (float side in new[] { -1f, 1f })
                foreach (float z in new[] { 2f, 8f, 14f, 21f, 33f })
                {
                    Planter(root, new Vector3(side * 20.8f, 0f, z), z > 5f && z < 22f);
                    if (z == 2f || z == 33f) Bench(root, new Vector3(side * 18.7f, 0f, z), side < 0f ? 90f : -90f, false);
                }
        }

        static void InteriorCeiling(Transform root, float height, bool late)
        {
            var ceiling = Group(root, CeilingName);
            foreach (float z in new[] { 3f, 10f, 17f, 24f, 32f })
            {
                Box(ceiling, "Roof cross beam", new Vector3(0f, height - .3f, z), new Vector3(44.5f, .5f, .55f), late ? Marble : Wood);
                if (!late) Chandelier(ceiling, new Vector3(0f, height - 2.7f, z));
            }
            foreach (float x in new[] { -16f, -8f, 8f, 16f }) Box(ceiling, "Roof length beam", new Vector3(x, height - .2f, 18f), new Vector3(.4f, .4f, 36f), late ? Marble : Wood);
            if (late)
            {
                foreach (float x in new[] { -8f, 8f })
                {
                    Box(ceiling, "Skylight glazing", new Vector3(x, height + .05f, 15f), new Vector3(5.5f, .1f, 26f), Glass);
                    foreach (float z in new[] { 3f, 9f, 15f, 21f, 27f }) Box(ceiling, "Skylight frame", new Vector3(x, height, z), new Vector3(5.9f, .2f, .15f), Brass);
                }
            }
        }

        static void Balcony(Transform parent, Vector3 at, float width, float depth)
        {
            Box(parent, "Upper balcony slab", at, new Vector3(width, .45f, depth), Marble);
            Box(parent, "Balcony handrail", at + new Vector3(0f, 1.45f, -depth * .5f), new Vector3(width, .14f, .22f), Stone);
            for (float x = -width * .5f + .6f; x < width * .5f; x += 1.4f)
                Cylinder(parent, "Balcony baluster", at + new Vector3(x, .82f, -depth * .5f), .1f, 1.25f, Stone);
        }

        static void Chandelier(Transform parent, Vector3 at)
        {
            Ring(parent, "Chandelier frame", at, 1.5f, Brass);
            Beam(parent, "Chandelier stem", at, at + Vector3.up * 2.6f, .08f, Iron);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * .25f;
                var p = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.45f;
                Cylinder(parent, "Chandelier candle", p + Vector3.up * .25f, .09f, .5f, Cloth);
                Ball(parent, "Chandelier flame", p + Vector3.up * .56f, .09f, Lamp);
            }
        }

        static void Sconce(Transform parent, Vector3 at)
        {
            Box(parent, "Sconce backplate", at + new Vector3(0f, 0f, .12f), new Vector3(.28f, .7f, .12f), Iron);
            Box(parent, "Lantern cage", at, new Vector3(.38f, .6f, .32f), Brass);
            Box(parent, "Lantern glass", at + new Vector3(0f, .01f, -.04f), new Vector3(.27f, .43f, .27f), Lamp);
            Box(parent, "Lantern hood", at + Vector3.up * .4f, new Vector3(.52f, .16f, .44f), Iron);
        }

        static void Planter(Transform parent, Vector3 at, bool cypress)
        {
            Cylinder(parent, "Planter foot", at + Vector3.up * .15f, .6f, .3f, Stone);
            Cylinder(parent, "Planter bowl", at + Vector3.up * .65f, .75f, .7f, Cream);
            Cylinder(parent, "Planter rim", at + Vector3.up * 1f, .84f, .15f, Stone);
            Ball(parent, "Planter foliage", at + Vector3.up * 1.45f, .68f, Leaf);
            if (cypress)
                for (int i = 0; i < 4; i++) Ball(parent, "Cypress tier", at + Vector3.up * (1.8f + i * .58f), .68f - i * .12f, Leaf);
            else
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.PI * .4f;
                    Ball(parent, "Planter flower", at + new Vector3(Mathf.Cos(a) * .42f, 1.8f, Mathf.Sin(a) * .42f), .17f, Cloth);
                }
        }

        static void Bench(Transform parent, Vector3 at, float yaw, bool velvet)
        {
            var root = Group(parent, "DEC_Bench", at);
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box(root, "Bench seat", new Vector3(0f, .65f, 0f), new Vector3(2.7f, .22f, .85f), velvet ? Violet : Wood);
            foreach (float x in new[] { -1f, 1f }) Box(root, "Bench leg", new Vector3(x, .3f, 0f), new Vector3(.22f, .6f, .6f), velvet ? Brass : Stone);
        }

        static void RookPylon(Transform parent, float height)
        {
            Box(parent, "Rook pylon shaft", Vector3.up * height * .5f, new Vector3(1f, height, 1f), Cream);
            Box(parent, "Rook pylon cap", Vector3.up * height, new Vector3(1.4f, .25f, 1.4f), Stone);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * .5f;
                Box(parent, "Pylon crenellation", new Vector3(Mathf.Cos(a) * .48f, height + .3f, Mathf.Sin(a) * .48f), new Vector3(.42f, .45f, .42f), Cream);
            }
        }

        static void Deck(Transform parent, float width, float depth)
        {
            for (int i = 0; i < 8; i++) Box(parent, "Oak deck plank", new Vector3(0f, .03f, -(i + .5f) * depth / 8f), new Vector3(width, .18f, depth / 8f - .035f), Maple);
            foreach (float x in new[] { -width * .42f, width * .42f })
            {
                Box(parent, "Deck iron strap", new Vector3(x, .14f, -depth * .5f), new Vector3(.14f, .05f, depth), Iron);
                for (int i = 0; i < 7; i++) Ball(parent, "Deck rivet", new Vector3(x, .19f, -(i + .5f) * depth / 7f), .045f, Brass);
            }
        }
    }
}
