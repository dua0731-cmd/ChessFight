using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    public enum PieceSkin { White, Black, Bot }

    // Menu-screen stand-ins for the six chess-piece characters, built from code in
    // the style of the team's 3D pawn: a faceted body, big glossy eyes, blush, a
    // coloured collar band and dark round mittens and feet.
    //
    // Display only (title screen, lobby, mode art): no colliders, no physics, no
    // input. The pivot is on the ground between the feet and the figure faces +Z.
    // When the artist's models land they replace these; callers only need the
    // GameObject back.
    public static class PieceFigure
    {
        public const float Height = 2.3f;   // ground to the tip of the king's cross

        const float FootDrop = .07f;        // the feet sink this far below the body's origin

        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static Mesh smoothSphere, cube;

        static readonly Color BodyWhite = Hex(0xF1E7DA), BodyBlack = Hex(0x3A3541), BodyBot = Hex(0xC3C7CF);
        // White team wears the model's blue collar, black team gold, bots grey.
        static readonly Color CollarWhite = Hex(0x4F6FD0), CollarBlack = Hex(0xE9B93C), CollarBot = Hex(0x8E96A6);
        static readonly Color Mitten = Hex(0x352C30), Gold = Hex(0xE9B93C), Eye = Hex(0x141016);
        // Blush is a translucent disc on the model; on a dark body it is pre-mixed here.
        static readonly Color Blush = Hex(0xF29C9C), BlushOnDark = Hex(0x915E64);
        static readonly Color GhostBody = Hex(0xB9C0CC), GhostTint = new Color(.04f, .08f, .18f, .42f);

        public static GameObject Build(PieceKind kind, PieceSkin skin, Transform parent)
        {
            var root = new GameObject(kind + " (" + skin + ")");
            if (parent != null) root.transform.SetParent(parent, false);
            var rig = Child(root, "Body", new Vector3(0, FootDrop, 0));

            bool dark = skin == PieceSkin.Black;
            Material body = Lit(skin == PieceSkin.Black ? BodyBlack : skin == PieceSkin.Bot ? BodyBot : BodyWhite, .12f);
            Material collar = Lit(skin == PieceSkin.Black ? CollarBlack : skin == PieceSkin.Bot ? CollarBot : CollarWhite, .3f);
            Material mitten = Lit(Mitten, .4f), gold = Lit(Gold, .5f, .2f);
            Color blush = dark ? BlushOnDark : Blush;

            Torso(rig, body, collar);
            for (int s = -1; s <= 1; s += 2)
            {
                Part(rig, "Arm", Cylinder("arm", .085f, .075f, .34f, 8), body, new Vector3(s * .4f, .93f, 0), Quaternion.Euler(0, 0, s * 83f));
                Part(rig, "Mitten", Sphere("mitten", .175f, 9, 7), mitten, new Vector3(s * .65f, .9f, .03f));
                Part(rig, "Foot", Sphere("foot", .2f, 9, 7), mitten, new Vector3(s * .2f, .09f, .07f), Quaternion.identity, new Vector3(1.15f, .8f, 1.3f));
            }

            switch (kind)
            {
                case PieceKind.King:
                    Part(rig, "Head", Sphere("head40", .4f, 14, 10), body, new Vector3(0, 1.52f, 0), Quaternion.identity, new Vector3(1, 1.04f, 1));
                    Face(rig, new Vector3(0, 1.52f, 0), .4f, .15f, .04f, .25f, -.11f, 1f, false, blush);
                    Part(rig, "Crown", Cylinder("kingband", .31f, .27f, .14f, 12), gold, new Vector3(0, 1.9f, 0));
                    Part(rig, "Cross", Cube(), gold, new Vector3(0, 2.12f, 0), Quaternion.identity, new Vector3(.1f, .36f, .1f));
                    Part(rig, "Cross Bar", Cube(), gold, new Vector3(0, 2.17f, 0), Quaternion.identity, new Vector3(.28f, .1f, .1f));
                    break;
                case PieceKind.Queen:
                    Part(rig, "Head", Sphere("head40", .4f, 14, 10), body, new Vector3(0, 1.52f, 0));
                    Face(rig, new Vector3(0, 1.52f, 0), .4f, .15f, .04f, .25f, -.11f, 1f, false, blush);
                    Part(rig, "Coronet", Cylinder("queenband", .26f, .3f, .16f, 12), gold, new Vector3(0, 1.9f, 0));
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2;
                        Part(rig, "Pearl", Sphere("pearl", .055f, 8, 6), gold, new Vector3(Mathf.Sin(a) * .3f, 2f, Mathf.Cos(a) * .3f));
                    }
                    Part(rig, "Top Pearl", Sphere("toppearl", .09f, 10, 8), gold, new Vector3(0, 2.02f, 0));
                    break;
                case PieceKind.Rook:
                    Part(rig, "Tower", Cylinder("rookhead", .42f, .4f, .66f, 12), body, new Vector3(0, 1.48f, 0));
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2 + Mathf.PI / 6;
                        Part(rig, "Merlon", Cube(), body, new Vector3(Mathf.Sin(a) * .31f, 1.89f, Mathf.Cos(a) * .31f),
                             Quaternion.Euler(0, a * Mathf.Rad2Deg, 0), new Vector3(.2f, .16f, .14f));
                    }
                    Face(rig, new Vector3(0, 1.46f, 0), .405f, .14f, .04f, .24f, -.1f, 1f, true, blush);
                    break;
                case PieceKind.Bishop:
                    Part(rig, "Mitre", Lathe("mitre", new[] { V(0, 1.13f), V(.27f, 1.15f), V(.35f, 1.3f), V(.37f, 1.46f), V(.33f, 1.63f), V(.23f, 1.81f), V(.09f, 1.93f), V(0, 1.96f) }, 14), body, Vector3.zero);
                    Part(rig, "Knob", Sphere("knob", .09f, 10, 8), body, new Vector3(0, 2.03f, 0));
                    Part(rig, "Slit", Cube(), Lit(dark ? Hex(0x4A3A30) : Hex(0x6B5446), .1f), new Vector3(.13f, 1.72f, .18f),
                         Quaternion.Euler(0, 28f, -35f), new Vector3(.055f, .32f, .16f));
                    Face(rig, new Vector3(0, 1.43f, 0), .36f, .13f, .02f, .22f, -.1f, .9f, false, blush);
                    break;
                case PieceKind.Knight:
                    Knight(rig, body, dark, blush);
                    break;
                default:
                    Part(rig, "Head", Sphere("head42", .42f, 14, 10), body, new Vector3(0, 1.55f, 0));
                    Face(rig, new Vector3(0, 1.55f, 0), .42f, .15f, .04f, .25f, -.11f, 1f, false, blush);
                    break;
            }
            return root;
        }

        // A faceless see-through navy pawn: the "nobody here yet" silhouette on an
        // empty lobby spot. Casts no shadow.
        public static GameObject Ghost(Transform parent)
        {
            var root = new GameObject("Ghost");
            if (parent != null) root.transform.SetParent(parent, false);
            var rig = Child(root, "Body", Vector3.zero);
            var ghost = GhostMaterial();
            Torso(rig, ghost, ghost);
            Part(rig, "Head", Sphere("head42", .42f, 14, 10), ghost, new Vector3(0, 1.55f, 0));
            foreach (var part in root.GetComponentsInChildren<Renderer>())
            {
                part.shadowCastingMode = ShadowCastingMode.Off;
                part.receiveShadows = false;
            }
            return root;
        }

        // Sprites/Default is unlit, blended and always in the build; plain grey if not.
        static Material GhostMaterial()
        {
            const string key = "Ghost";
            if (materials.TryGetValue(key, out var cached) && cached != null) return cached;
            var shader = Shader.Find("Sprites/Default");
            var material = shader != null ? new Material(shader) { color = GhostTint, name = "Figure Ghost" } : Lit(GhostBody, .05f);
            materials[key] = material;
            return material;
        }

        static void Torso(GameObject rig, Material body, Material collar)
        {
            Part(rig, "Skirt", Lathe("skirt", new[] { V(0, .16f), V(.56f, .16f), V(.58f, .24f), V(.54f, .32f), V(.48f, .36f), V(.47f, .44f), V(.41f, .48f), V(.37f, .58f), V(.31f, .76f), V(.27f, .94f), V(.25f, 1.03f), V(0, 1.03f) }, 14), body, Vector3.zero);
            Part(rig, "Collar", Cylinder("band", .3f, .3f, .07f, 14), collar, new Vector3(0, 1.065f, 0));
            Part(rig, "Disc", Cylinder("disc", .35f, .35f, .08f, 14), body, new Vector3(0, 1.135f, 0));
        }

        // The horse head is a side profile extruded sideways, turned so the snout points +Z.
        static void Knight(GameObject rig, Material body, bool dark, Color blush)
        {
            var head = Child(rig, "Head", Vector3.zero);
            head.transform.localRotation = Quaternion.Euler(0, -90f, 0);
            var profile = new[] { V(-.28f, 0), V(-.31f, .34f), V(-.27f, .6f), V(-.17f, .8f), V(-.11f, .96f), V(-.05f, 1.08f), V(.02f, .92f), V(.14f, .86f), V(.4f, .6f), V(.5f, .47f), V(.47f, .34f), V(.34f, .31f), V(.21f, .35f), V(.12f, .22f), V(.2f, 0) };
            Part(head, "Horse", Extrude("horse", profile, .42f), body, new Vector3(.04f, 1.14f, 0));
            Part(head, "Mane", Cube(), Lit(dark ? Hex(0x5E4638) : Hex(0xD2A884), .1f), new Vector3(-.28f, 1.66f, 0),
                 Quaternion.Euler(0, 0, -16f), new Vector3(.1f, .62f, .18f));
            Material eye = Lit(Eye, .85f), shine = Unlit(Color.white), pink = Lit(blush, .05f);
            for (int s = -1; s <= 1; s += 2)
            {
                Decal(head, "Eye", eye, new Vector3(.19f, 1.86f, s * .23f), Quaternion.LookRotation(new Vector3(.35f, 0, s)), new Vector3(.17f, .23f, .08f));
                Decal(head, "Shine", shine, new Vector3(.22f, 1.91f, s * .265f), Quaternion.identity, Vector3.one * .052f);
                Decal(head, "Blush", pink, new Vector3(.27f, 1.66f, s * .222f), Quaternion.LookRotation(new Vector3(0, 0, s)), new Vector3(.18f, .12f, .02f));
                Decal(head, "Nostril", eye, new Vector3(.55f, 1.59f, s * .09f), Quaternion.identity, Vector3.one * .06f);
            }
        }

        // Two eyes with a shine and two blush discs on the front of a round (or
        // cylindrical) head centred at `center` with radius r.
        static void Face(GameObject rig, Vector3 center, float r, float ex, float ey, float bx, float by, float size, bool cylinder, Color blush)
        {
            Material eye = Lit(Eye, .85f), shine = Unlit(Color.white), pink = Lit(blush, .05f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 n = new Vector3(s * ex / r, ey / r, 1).normalized;
                Vector3 p = center + n * r * .985f;
                if (cylinder)
                {
                    float z = Mathf.Sqrt(Mathf.Max(0, r * r - ex * ex));
                    p = center + new Vector3(s * ex, ey, z);
                    n = new Vector3(s * ex, 0, z).normalized;
                }
                Decal(rig, "Eye", eye, p, Quaternion.LookRotation(n), new Vector3(.136f, .2f, .07f) * size);
                Decal(rig, "Shine", shine, p + new Vector3(s * .01f + .018f, .04f * size, .03f), Quaternion.identity, Vector3.one * .048f * size);

                Vector3 bn = new Vector3(s * bx / r, by / r, 1).normalized;
                Vector3 bp = center + bn * r * 1.004f;
                if (cylinder)
                {
                    float z = Mathf.Sqrt(Mathf.Max(0, r * r - bx * bx));
                    bp = center + new Vector3(s * bx, by, z + .004f);
                    bn = new Vector3(s * bx, 0, z).normalized;
                }
                Decal(rig, "Blush", pink, bp, Quaternion.LookRotation(bn), new Vector3(.175f, .12f, .02f) * size);
            }
        }

        // ---------- parts and materials ----------

        static GameObject Child(GameObject parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = position;
            return go;
        }

        static void Part(GameObject parent, string name, Mesh mesh, Material material, Vector3 position)
            => Part(parent, name, mesh, material, position, Quaternion.identity, Vector3.one);

        static void Part(GameObject parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation)
            => Part(parent, name, mesh, material, position, rotation, Vector3.one);

        static MeshRenderer Part(GameObject parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var go = Child(parent, name, position);
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        // Small smooth face parts: too small to matter for shadows.
        static void Decal(GameObject parent, string name, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var renderer = Part(parent, name, SmoothSphere(), material, position, rotation, scale);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // Standard is what this project's Built-in path lights with (see
        // QueenHillLevel); a build that stripped it falls back to Diffuse.
        public static Material Lit(Color color, float gloss, float metal = 0f)
        {
            string key = "L" + ColorUtility.ToHtmlStringRGB(color) + "_" + gloss + "_" + metal;
            if (materials.TryGetValue(key, out var cached) && cached != null) return cached;
            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
            var material = new Material(shader) { color = color, name = "Figure " + key };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", gloss);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metal);
            materials[key] = material;
            return material;
        }

        static Material Unlit(Color color)
        {
            string key = "U" + ColorUtility.ToHtmlStringRGB(color);
            if (materials.TryGetValue(key, out var cached) && cached != null) return cached;
            var shader = Shader.Find("Unlit/Color");
            if (shader == null) return Lit(color, 0f);
            var material = new Material(shader) { color = color, name = "Figure " + key };
            materials[key] = material;
            return material;
        }

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
        static Vector2 V(float x, float y) => new Vector2(x, y);

        // ---------- meshes: faceted, low poly, two-sided so winding never matters ----------

        static Mesh SmoothSphere()
        {
            if (smoothSphere == null) smoothSphere = Builtin(PrimitiveType.Sphere);
            return smoothSphere;
        }

        public static Mesh Cube()
        {
            if (cube == null) cube = Builtin(PrimitiveType.Cube);
            return cube;
        }

        static Mesh Builtin(PrimitiveType type)
        {
            var probe = GameObject.CreatePrimitive(type);
            var mesh = probe.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(probe);
            return mesh;
        }

        static Mesh Sphere(string key, float r, int segments, int rings)
        {
            var profile = new Vector2[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float t = Mathf.PI * i / rings;
                profile[i] = new Vector2(Mathf.Sin(t) * r, -Mathf.Cos(t) * r);
            }
            return Lathe("sphere " + key, profile, segments);
        }

        static Mesh Cylinder(string key, float rBottom, float rTop, float h, int segments)
            => Lathe("cylinder " + key, new[] { V(0, -h / 2), V(rBottom, -h / 2), V(rTop, h / 2), V(0, h / 2) }, segments);

        // Spins a (radius, height) profile round the Y axis.
        static Mesh Lathe(string key, Vector2[] profile, int segments)
        {
            if (meshes.TryGetValue(key, out var cached) && cached != null) return cached;
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < profile.Length; i++)
                for (int j = 0; j <= segments; j++)
                {
                    float a = Mathf.PI * 2 * j / segments;
                    v.Add(new Vector3(Mathf.Cos(a) * profile[i].x, profile[i].y, Mathf.Sin(a) * profile[i].x));
                }
            int row = segments + 1;
            for (int i = 0; i < profile.Length - 1; i++)
                for (int j = 0; j < segments; j++)
                {
                    int a = i * row + j, b = a + row;
                    t.Add(a); t.Add(b); t.Add(a + 1);
                    t.Add(a + 1); t.Add(b); t.Add(b + 1);
                }
            return meshes[key] = Faceted(key, v, t);
        }

        // Extrudes a 2D outline (in XY) to `depth` along Z, centred on Z = 0.
        static Mesh Extrude(string key, Vector2[] outline, float depth)
        {
            if (meshes.TryGetValue(key, out var cached) && cached != null) return cached;
            var v = new List<Vector3>();
            var t = new List<int>();
            int n = outline.Length;
            foreach (var p in outline) v.Add(new Vector3(p.x, p.y, depth / 2));
            foreach (var p in outline) v.Add(new Vector3(p.x, p.y, -depth / 2));
            var caps = Triangulate(outline);
            foreach (int i in caps) t.Add(i);
            foreach (int i in caps) t.Add(i + n);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                t.Add(i); t.Add(j); t.Add(i + n);
                t.Add(j); t.Add(j + n); t.Add(i + n);
            }
            return meshes[key] = Faceted(key, v, t);
        }

        // Unwelds every triangle so each face gets its own flat normal (the model's
        // faceted look), and adds the reverse face so it reads from both sides.
        static Mesh Faceted(string name, List<Vector3> v, List<int> t)
        {
            var nv = new List<Vector3>();
            var nt = new List<int>();
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-12f) continue;
                int k = nv.Count;
                nv.Add(a); nv.Add(b); nv.Add(c);
                nt.Add(k); nt.Add(k + 1); nt.Add(k + 2);
                k = nv.Count;
                nv.Add(a); nv.Add(c); nv.Add(b);
                nt.Add(k); nt.Add(k + 1); nt.Add(k + 2);
            }
            var mesh = new Mesh { name = "Figure " + name };
            mesh.SetVertices(nv);
            mesh.SetTriangles(nt, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Ear clipping for a simple polygon, concave allowed.
        static List<int> Triangulate(Vector2[] p)
        {
            var index = new List<int>();
            for (int i = 0; i < p.Length; i++) index.Add(i);
            float area = 0;
            for (int i = 0; i < p.Length; i++) area += Cross(p[i], p[(i + 1) % p.Length]);
            if (area < 0) index.Reverse();

            var tris = new List<int>();
            for (int guard = 0; index.Count > 3 && guard < 1000; guard++)
            {
                bool clipped = false;
                for (int i = 0; i < index.Count && !clipped; i++)
                {
                    int i0 = index[(i + index.Count - 1) % index.Count], i1 = index[i], i2 = index[(i + 1) % index.Count];
                    Vector2 a = p[i0], b = p[i1], c = p[i2];
                    if (Cross(b - a, c - b) <= 0) continue;
                    bool blocked = false;
                    foreach (int j in index)
                        if (j != i0 && j != i1 && j != i2 && Inside(p[j], a, b, c)) { blocked = true; break; }
                    if (blocked) continue;
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    index.RemoveAt(i);
                    clipped = true;
                }
                if (!clipped) break;
            }
            if (index.Count == 3) { tris.Add(index[0]); tris.Add(index[1]); tris.Add(index[2]); }
            return tris;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            bool negative = d1 < 0 || d2 < 0 || d3 < 0, positive = d1 > 0 || d2 > 0 || d3 > 0;
            return !(negative && positive);
        }
    }
}
