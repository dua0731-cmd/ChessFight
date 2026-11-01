using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    // Static prototype dressing. The chess controller owns rules, pawns and board collision.
    [DisallowMultipleComponent]
    public sealed class GiantChessWarRoom : MonoBehaviour
    {
        [SerializeField] bool showRoom = true;
        [SerializeField, Range(60, 110)] float wallHeight = 80;
        [SerializeField, Range(12, 30)] float tableHeight = 22;
        [SerializeField] bool solidEntryExitProps = true;
        readonly List<Material> materials = new List<Material>();
        Transform root;
        Material wood, darkWood, stone, brass, paper, ink, ivory, charcoal, blue, purple;

        Material Material(string name, Color color)
        {
            var material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Glossiness", .25f);
            materials.Add(material);
            return material;
        }
        Transform Group(string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(root, false);
            return group;
        }
        GameObject Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material,
            bool solid = false, PrimitiveType type = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) { var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider); }
            return go;
        }
        void Bar(Transform parent, string name, Vector3 a, Vector3 b, float width, Material material)
        {
            var go = Part(parent, name, (a + b) * .5f, new Vector3(width, width, Vector3.Distance(a, b)), material);
            go.transform.localRotation = Quaternion.LookRotation(b - a);
        }
        public void Build(float tileSize)
        {
            if (!showRoom || root != null) return;
            root = new GameObject("War Room - prototype environment").transform;
            root.SetParent(transform, false);
            wood = Material("Warm oak", new Color(.36f, .19f, .09f));
            darkWood = Material("Dark walnut", new Color(.14f, .075f, .04f));
            stone = Material("Warm stone", new Color(.31f, .32f, .34f));
            brass = Material("Brass", new Color(.73f, .49f, .18f));
            paper = Material("Parchment", new Color(.79f, .70f, .49f));
            ink = Material("Map ink", new Color(.10f, .24f, .27f));
            ivory = Material("White army", new Color(.86f, .83f, .72f));
            charcoal = Material("Black army", new Color(.10f, .12f, .17f));
            blue = Material("Blue pennant", new Color(.15f, .34f, .47f));
            purple = Material("Royal purple", new Color(.30f, .14f, .32f));
            float half = tileSize * 4, length = tileSize * 8;
            Table(half, length);
            Room(half, length);
            for (int side = -1; side <= 1; side += 2)
            {
                Chair(side, half, length);
                Briefing(side, half, length);
                TrophyShelf(side, half, length);
            }
        }
        void Table(float half, float length)
        {
            var g = Group("01 - Giant war table");
            // No new walking surface beside/beneath the board: falling still triggers the existing reset.
            Part(g, "Oak tabletop underside", new Vector3(0, -2.8f, length * .5f), new Vector3(half * 2 + 2, 4, length + 40), wood);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(g, "Inlaid side edge", new Vector3(side * (half + .4f), -.65f, length * .5f), new Vector3(.8f, .3f, length + 40), brass);
                foreach (float z in new[] { -10f, length + 10 })
                {
                    Part(g, "Carved table leg", new Vector3(side * (half - 8), -tableHeight * .5f - 2, z), new Vector3(7, tableHeight - 4, 7), darkWood);
                    Part(g, "Leg brass collar", new Vector3(side * (half - 8), -tableHeight + 3, z), new Vector3(8, 2, 8), brass);
                }
            }
            Part(g, "Front apron", new Vector3(0, -4, -20), new Vector3(half * 2, 5, 1), darkWood);
        }
        void Room(float half, float length)
        {
            var g = Group("02 - Room shell and campaign wall");
            float outer = half + 60, back = length + 42;
            Part(g, "Distant room floor - visual only", new Vector3(0, -tableHeight - 1, length * .5f), new Vector3(outer * 2, 2, length + 110), stone);
            Part(g, "Back wall", new Vector3(0, wallHeight * .5f - tableHeight, back), new Vector3(outer * 2, wallHeight, 3), stone);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(g, "Side wall", new Vector3(side * outer, wallHeight * .5f - tableHeight, length * .5f), new Vector3(3, wallHeight, length + 84), stone);
                for (int i = 0; i < 4; i++)
                {
                    float z = -12 + i * (length + 24) / 3;
                    Part(g, "Wall pier", new Vector3(side * (outer - 2), 6, z), new Vector3(5, 54, 6), darkWood);
                    Part(g, "Royal hanging banner", new Vector3(side * (outer - 4), 14, z + 8), new Vector3(.4f, 24, 9), purple);
                    Part(g, "Banner crest", new Vector3(side * (outer - 4.4f), 16, z + 8), new Vector3(.3f, 6, 4), brass);
                }
            }
            Part(g, "Campaign map frame", new Vector3(0, 17, back - 2), new Vector3(76, 34, 2), darkWood);
            Part(g, "Campaign parchment", new Vector3(0, 17, back - 3.1f), new Vector3(72, 30, .2f), paper);
            for (int i = 0; i < 6; i++)
            {
                float x = -29 + i * 11;
                float y = 10 + (i % 3) * 6;
                Part(g, "Map fortress", new Vector3(x, y, back - 3.4f), new Vector3(4, 3, .3f), ink);
                if (i < 5) Bar(g, "Campaign route", new Vector3(x + 2, y, back - 3.6f), new Vector3(x + 9, 10 + ((i + 1) % 3) * 6, back - 3.6f), .45f, brass);
                Part(g, "Map flag pin", new Vector3(x, y + 3, back - 3.7f), new Vector3(.3f, 4, .3f), brass);
                Part(g, "Map flag", new Vector3(x + 1.5f, y + 4, back - 3.7f), new Vector3(3, 2, .25f), blue);
            }
        }
        void Chair(int side, float half, float length)
        {
            var g = Group(side < 0 ? "03 - White commander station" : "03 - Black commander station");
            float x = side * (half + 22), z = length * .5f;
            Part(g, "Command chair seat", new Vector3(x, 1, z), new Vector3(22, 4, 26), darkWood);
            Part(g, "Upholstered back", new Vector3(x + side * 11, 18, z), new Vector3(4, 34, 28), side < 0 ? purple : blue);
            Part(g, "Chair crest", new Vector3(x + side * 8.8f, 28, z), new Vector3(.5f, 7, 7), brass);
            for (int s = -1; s <= 1; s += 2)
            {
                Part(g, "Arm rest", new Vector3(x, 8, z + s * 13), new Vector3(22, 2, 3), wood);
                Part(g, "Chair leg", new Vector3(x + side * 7, -tableHeight * .5f, z + s * 10), new Vector3(3, tableHeight, 3), darkWood);
            }
            // Pointer rests outside the playing grid, never obscuring attack cells.
            Bar(g, "Resting command pointer", new Vector3(side * (half + 5), 3, z - 25), new Vector3(side * (half + 17), 5, z - 5), .6f, brass);
        }
        void Briefing(int side, float half, float length)
        {
            var g = Group(side < 0 ? "04 - Left briefing and orders" : "04 - Right briefing and orders");
            float x = side * (half - 10);
            foreach (float z in new[] { -11f, length + 11 })
            {
                Part(g, "Document chest", new Vector3(x, 2, z), new Vector3(12, 4, 8), darkWood, solidEntryExitProps);
                Part(g, "Chest brass band", new Vector3(x, 2.1f, z - 4.05f), new Vector3(1, 3.8f, .1f), brass);
                for (int i = 0; i < 3; i++)
                {
                    Part(g, "Tactical book cover", new Vector3(x, 4.2f + i * .65f, z), new Vector3(10 - i, .18f, 6), i % 2 == 0 ? ink : purple);
                    Part(g, "Book pages", new Vector3(x, 4.45f + i * .65f, z), new Vector3(9.5f - i, .32f, 5.7f), paper);
                }
                Part(g, "Unrolled orders", new Vector3(x - side * 10, .04f, z), new Vector3(6, .05f, 9), paper);
                for (int line = 0; line < 5; line++)
                    Part(g, "Ink on orders", new Vector3(x - side * 10, .075f, z - 3 + line), new Vector3(4, .015f, .13f), ink);
                Part(g, "Royal seal", new Vector3(x - side * 10, .17f, z + 3), new Vector3(1.4f, .12f, 1.4f), brass, type: PrimitiveType.Cylinder);
                Part(g, "Ink bottle", new Vector3(x + side * 7, 1.5f, z), new Vector3(2, 1.5f, 2), ink, type: PrimitiveType.Cylinder);
                Bar(g, "Quill", new Vector3(x + side * 7, 3, z), new Vector3(x + side * 9, 7, z + 1), .25f, ivory);
            }
            // Small flat gold marks guide the existing spawn lanes without making a new bottleneck.
            for (int i = 0; i < 4; i++)
                Part(g, "Entry guidance inlay", new Vector3(side * half * .25f, .025f, -17 + i * 4), new Vector3(2, .025f, 1), brass);
        }
        void TrophyShelf(int side, float half, float length)
        {
            var g = Group(side < 0 ? "05 - White captured army display" : "05 - Black captured army display");
            float x = side * (half + 16), z = length * .5f + 38;
            Part(g, "Captured army cabinet", new Vector3(x, 0, z), new Vector3(12, 8, 22), darkWood);
            var color = side < 0 ? charcoal : ivory;
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = new Vector3(x, 4, z - 7 + i * 7);
                Part(g, "Display pawn base", p + Vector3.up * .5f, new Vector3(4, .5f, 4), color, type: PrimitiveType.Cylinder);
                Part(g, "Display pawn stem", p + Vector3.up * 2.3f, new Vector3(1.6f, 2, 1.6f), color, type: PrimitiveType.Cylinder);
                Part(g, "Display pawn head", p + Vector3.up * 4.8f, Vector3.one * 2.8f, color, type: PrimitiveType.Sphere);
            }
        }
        void OnDestroy()
        {
            foreach (var material in materials) if (material) Destroy(material);
        }
    }
}
