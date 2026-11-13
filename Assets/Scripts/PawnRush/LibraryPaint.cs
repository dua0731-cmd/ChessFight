using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R386 (승규 11-04, the war room's concept art): a war room prop painted part by part in PR_LibraryPaint.shader - the
    // bookcases' books, the crates' teal boards and brass corners, the desks' books and quills, the lanterns' glass, the
    // chests' brass bands, the balustrades' brass caps. Its mesh (PawnRushLibraryEnvironment.fbx, readable) is copied in
    // Awake, once per mesh and recipe and never saved with the scene, with vertex colours: RGB the part's colour (sRGB), A
    // its kind (Kind / 7). A part is an island of the mesh (vertices welded at 1 mm); the recipe Course01v4Builder.
    // LibraryDressing chose by the prop's name tells the parts apart by their size and height in metres.
    [RequireComponent(typeof(MeshFilter))]
    public sealed class LibraryPaint : MonoBehaviour
    {
        public enum Kind { Stone, Wood, Paint, Brass, Iron, Paper, Glow, Cloth }

        // The concept's palette (sRGB), the flat looks' colours where they meet (Course01v4Builder.LibraryLooks).
        // R389: the stone a warm, slightly rosy beige as the concept's slabs (was .90 .85 .77 / .83 .79 .71).
        public static readonly Color WallStone = new Color(.87f, .81f, .74f), FloorStone = new Color(.81f, .75f, .69f),
            Marble = new Color(.94f, .92f, .88f), Grout = new Color(.50f, .46f, .41f), Wood = new Color(.53f, .37f, .23f),
            DarkWood = new Color(.36f, .23f, .14f), Teal = new Color(.30f, .44f, .47f), DeepTeal = new Color(.22f, .34f, .37f),
            Brass = new Color(.75f, .61f, .32f), Iron = new Color(.22f, .23f, .25f), Paper = new Color(.90f, .85f, .72f),
            Glass = new Color(1f, .76f, .46f), Leather = new Color(.45f, .2f, .15f), Ribbon = new Color(.55f, .14f, .12f),
            Feather = new Color(.93f, .91f, .86f);
        static readonly Color[] BookColours =
        {
            new Color(.50f, .16f, .14f), new Color(.16f, .22f, .36f), new Color(.18f, .33f, .23f), new Color(.43f, .27f, .16f),
            new Color(.62f, .45f, .20f), new Color(.20f, .38f, .40f), new Color(.38f, .12f, .17f), new Color(.28f, .20f, .32f),
        };

        [SerializeField] string recipe;
        static readonly Dictionary<(Mesh, string), Mesh> painted = new Dictionary<(Mesh, string), Mesh>();

        public void Configure(string recipe) => this.recipe = recipe;

        void Awake()
        {
            var filter = GetComponent<MeshFilter>();
            var source = filter.sharedMesh;
            if (source == null || !source.isReadable || string.IsNullOrEmpty(recipe)) return;
            if (!painted.TryGetValue((source, recipe), out var mesh) || mesh == null)
            {
                mesh = Instantiate(source);
                mesh.name = source.name + " (" + recipe + ")";
                mesh.hideFlags = HideFlags.DontSave;
                mesh.colors32 = Paint(source, recipe, transform);
                painted[(source, recipe)] = mesh;
            }
            filter.sharedMesh = mesh;
        }

        public static Color32 Vertex(Kind kind, Color colour)
        {
            Color32 c = colour;
            c.a = (byte)Mathf.RoundToInt((int)kind * 255f / 7f);
            return c;
        }

        // One island of the mesh in metres: `lo` .. `hi` its height over the mesh's lowest point, `a` <= `b` its two level
        // sizes, `centre` its middle along the mesh's axes (scaled), `verts` its vertices.
        struct Part
        {
            public float lo, hi, a, b;
            public Vector3 min, max, centre;
            public int verts;
            public float H => hi - lo;
        }

        static Color32[] Paint(Mesh mesh, string recipe, Transform t)
        {
            var v = mesh.vertices;
            var tris = mesh.triangles;
            int n = v.Length;
            var parent = new int[n];
            var at = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < n; i++)
            {
                var key = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000f), Mathf.RoundToInt(v[i].y * 1000f), Mathf.RoundToInt(v[i].z * 1000f));
                if (at.TryGetValue(key, out int j)) parent[i] = j;
                else
                {
                    at[key] = i;
                    parent[i] = i;
                }
            }
            for (int k = 0; k < tris.Length; k += 3)
            {
                Union(parent, tris[k], tris[k + 1]);
                Union(parent, tris[k], tris[k + 2]);
            }

            // Metres along the mesh's axes; up is the mesh axis the world's up lies along.
            var s = t.lossyScale;
            var scale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            var upLocal = t.InverseTransformDirection(Vector3.up);
            int up = Mathf.Abs(upLocal.x) > Mathf.Abs(upLocal.y) ? (Mathf.Abs(upLocal.x) > Mathf.Abs(upLocal.z) ? 0 : 2)
                   : (Mathf.Abs(upLocal.y) > Mathf.Abs(upLocal.z) ? 1 : 2);
            float sign = upLocal[up] < 0 ? -1f : 1f;
            int h0 = (up + 1) % 3, h1 = (up + 2) % 3;
            var root = new int[n];
            var parts = new Dictionary<int, Part>();
            float floor = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                int r = Find(parent, i);
                root[i] = r;
                var p = Vector3.Scale(v[i], scale);
                floor = Mathf.Min(floor, p[up] * sign);
                if (!parts.TryGetValue(r, out var part)) part = new Part { min = p, max = p };
                part.min = Vector3.Min(part.min, p);
                part.max = Vector3.Max(part.max, p);
                part.verts++;
                parts[r] = part;
            }
            var keys = new List<int>(parts.Keys);
            foreach (int r in keys)
            {
                var part = parts[r];
                float x = part.max[h0] - part.min[h0], z = part.max[h1] - part.min[h1];
                part.a = Mathf.Min(x, z);
                part.b = Mathf.Max(x, z);
                float u0 = part.min[up] * sign, u1 = part.max[up] * sign;
                part.lo = Mathf.Min(u0, u1) - floor;
                part.hi = Mathf.Max(u0, u1) - floor;
                part.centre = (part.min + part.max) * .5f;
                parts[r] = part;
            }

            var looks = new Dictionary<int, Color32>();
            if (recipe == "bookcase") Bookcase(parts, looks);
            else
                foreach (var pair in parts)
                    looks[pair.Key] = Look(recipe, pair.Value);
            var colours = new Color32[n];
            for (int i = 0; i < n; i++) colours[i] = looks[root[i]];
            return colours;
        }

        static Color32 Look(string recipe, Part p)
        {
            switch (recipe)
            {
                case "crate":
                    // Teal boards between brass corner posts; brass feet, bolts and chevrons; iron rails along the foot.
                    if (p.H >= .9f) return p.a >= .11f && p.b <= .16f ? Vertex(Kind.Brass, Brass) : Vertex(Kind.Paint, Teal);
                    return p.b >= .9f ? Vertex(Kind.Iron, Iron) : Vertex(Kind.Brass, Brass);
                case "desk":
                    // A reading desk and its stool in wood; on the top (from 1.12 m) an open book on a leather cover, a quill
                    // and an inkwell.
                    if (p.lo < 1.12f) return Vertex(Kind.Wood, Wood);
                    if (p.a <= .03f && p.H >= .3f) return Vertex(Kind.Paper, Feather);
                    if (p.verts >= 100 && p.b <= .25f) return Vertex(Kind.Iron, Iron);
                    if (p.lo < 1.16f && p.b >= .55f) return Vertex(Kind.Cloth, Leather);
                    return Vertex(Kind.Paper, Paper);
                case "lantern":
                    // Brass frames, uprights and caps, an iron bracket arm or post (0.8 m and longer), the glass (its core
                    // 0.15 x 0.4 m) lit.
                    if (p.verts == 72 || (p.a >= .12f && p.b <= .2f && p.H >= .3f && p.H <= .5f)) return Vertex(Kind.Glow, Glass);
                    if (Mathf.Max(p.b, p.H) >= .75f) return Vertex(Kind.Iron, Iron);
                    return Vertex(Kind.Brass, Brass);
                case "chest":
                    // Dark wood with brass corners, lid bands, hinges, handles and lock.
                    if (p.b <= .3f && p.H <= .3f) return Vertex(Kind.Brass, Brass);
                    if (p.a <= .1f && p.b <= .12f && p.H >= .5f) return Vertex(Kind.Brass, Brass);
                    if (p.a <= .09f && p.b >= .5f && p.H <= .35f) return Vertex(Kind.Brass, Brass);
                    return Vertex(Kind.Wood, DarkWood);
                case "railing":
                    // Teal posts, rails and balusters, deeper teal infill panels, brass plinths, bases, caps and rivets.
                    if (p.b <= .08f && p.H <= .08f) return Vertex(Kind.Brass, Brass);
                    if (p.H <= .2f && p.b <= .4f && p.a >= .25f) return Vertex(Kind.Brass, Brass);
                    if (p.b >= 1.5f && p.H >= .3f) return Vertex(Kind.Paint, DeepTeal);
                    return Vertex(Kind.Paint, Teal);
                case "scrolls":
                    // Rolled paper tied with a red ribbon, dark wooden ends.
                    if (p.b >= .5f) return Vertex(Kind.Paper, Paper);
                    if (p.a <= .02f) return Vertex(Kind.Wood, DarkWood);
                    return Vertex(Kind.Cloth, Ribbon);
                default:
                    return Vertex(Kind.Stone, WallStone);
            }
        }

        // A bookcase: its frame, shelves and dividers in wood; every book (two boards, a spine and a page block, found as
        // the small parts that touch one another) in one cloth colour of the palette by where it stands, its pages paper.
        static void Bookcase(Dictionary<int, Part> parts, Dictionary<int, Color32> looks)
        {
            var books = new List<int>();
            foreach (var pair in parts)
            {
                var p = pair.Value;
                bool book = p.H >= .15f && p.H <= .7f && p.b <= .6f && p.lo > .15f;
                if (book) books.Add(pair.Key);
                else looks[pair.Key] = Vertex(Kind.Wood, Wood);
            }
            var group = new int[books.Count];
            for (int i = 0; i < group.Length; i++) group[i] = i;
            for (int i = 0; i < books.Count; i++)
            for (int j = i + 1; j < books.Count; j++)
            {
                var p = parts[books[i]];
                var q = parts[books[j]];
                if (Touch(p, q)) Union(group, i, j);
            }
            for (int i = 0; i < books.Count; i++)
            {
                var p = parts[books[i]];
                if (p.a >= .08f)
                {
                    looks[books[i]] = Vertex(Kind.Paper, Paper);
                    continue;
                }
                var g = parts[books[Find(group, i)]];
                int pick = Mathf.Abs(Mathf.RoundToInt(g.centre.x * 37f + g.centre.y * 53f + g.centre.z * 71f)) % BookColours.Length;
                looks[books[i]] = Vertex(Kind.Cloth, BookColours[pick]);
            }
        }

        // A book's own parts touch (its spine spans its boards and page block); the next book stands 2 cm or more away.
        static bool Touch(Part p, Part q)
        {
            const float gap = .004f;
            for (int k = 0; k < 3; k++)
                if (p.min[k] > q.max[k] + gap || q.min[k] > p.max[k] + gap) return false;
            return true;
        }

        static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        static void Union(int[] parent, int a, int b)
        {
            a = Find(parent, a);
            b = Find(parent, b);
            if (a != b) parent[b] = a;
        }
    }
}
