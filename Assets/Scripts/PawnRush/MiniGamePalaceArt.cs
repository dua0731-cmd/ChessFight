using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush
{
    // R312: the palace art is a picture over the existing mini-game bodies. It never adds a collider,
    // changes a rule, or owns a network state. The section builder owns the walkable shell and portals.
    public static partial class MiniGamePalaceArt
    {
        public const string ShellName = "ENV_PalaceArt_R312";
        const string DressName = "DEC_PalaceGame_R312";
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static Mesh block, torus;

        public static bool Early(int game) => game == PawnRushMissions.Paint || game == PawnRushMissions.Drawbridge || game == PawnRushMissions.Capstan;
        public static bool Late(int game) => game == PawnRushMissions.KingEscort || game == PawnRushMissions.Checkmate || game == PawnRushMissions.CheckBlock;
        public static Color TeamColor(int team) => Arena.Hex(team == Teams.Black ? "#F08A2C" : "#3FC1D9");
        public static Material DaisMaterial(int game) => M("Dais" + (Early(game) ? "Early" : Late(game) ? "Late" : "Mid"), Early(game) ? "#303641" : Late(game) ? "#29334A" : "#34404C");

        static Material Cream => M("Limestone", "#DDD1B9", 0f, .9f, "PR_StoneGrain");
        static Material Stone => M("StoneShadow", "#B7AA92", 0f, .92f, "PR_StoneGrain");
        static Material Marble => M("Marble", "#DDD7C9", 0f, .72f, "PR_StoneGrain");
        static Material Wood => M("Walnut", "#65432D", 0f, .8f, "PR_Wood");
        static Material Maple => M("Maple", "#BC9665", 0f, .8f, "PR_Wood");
        static Material Brass => M("BrassDecor", "#9C8354", 0f, .78f);
        static Material Gold => M("InteractionGold", "#E2B23C", .4f, .45f);
        static Material Iron => M("Iron", "#393D43", 0f, .7f);
        static Material Violet => M("PurpleVelvet", "#604B72", 0f, .95f);
        static Material Cloth => M("CreamCloth", "#E5DECC", 0f, .98f);
        static Material Leaf => M("Cypress", "#425B42", 0f, 1f);
        static Material Glass => M("Window", "#AFC7CE", .12f, .35f);
        static Material Lamp => M("Lamp", "#F9D594", .6f, .65f);

        static Material M(string name, string hex, float glow = 0f, float roughness = .85f, string texture = null)
        {
            if (materials.TryGetValue(name, out var found) && found != null) return found;
            var value = Arena.Lit("PR_MG_" + name, Arena.Hex(hex), glow, roughness);
            value.enableInstancing = true;
            if (!string.IsNullOrEmpty(texture)) MapLook.Surface(value, texture, 1f, .22f);
            materials[name] = value;
            return value;
        }

        static Transform Group(Transform parent, string name, Vector3 at = default, bool unscale = false)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = at;
            if (unscale)
            {
                var s = parent.localScale;
                t.localScale = new Vector3(1f / Mathf.Max(.0001f, Mathf.Abs(s.x)), 1f / Mathf.Max(.0001f, Mathf.Abs(s.y)), 1f / Mathf.Max(.0001f, Mathf.Abs(s.z)));
            }
            return t;
        }

        static GameObject Box(Transform parent, string name, Vector3 at, Vector3 size, Material material, bool beveled = true)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.GetComponent<MeshFilter>().sharedMesh = Block;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static GameObject Cylinder(Transform parent, string name, Vector3 at, float radius, float height, Material material)
        {
            var go = MiniGameParts.Part(parent, PrimitiveType.Cylinder, at, new Vector3(radius * 2f, height * .5f, radius * 2f), material);
            go.name = name;
            return go;
        }

        static GameObject Ball(Transform parent, string name, Vector3 at, float radius, Material material)
        {
            var go = MiniGameParts.Part(parent, PrimitiveType.Sphere, at, Vector3.one * radius * 2f, material);
            go.name = name;
            return go;
        }

        static GameObject Ring(Transform parent, string name, Vector3 at, float radius, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = Vector3.one * radius;
            go.GetComponent<MeshFilter>().sharedMesh = Torus;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static void Beam(Transform parent, string name, Vector3 a, Vector3 b, float width, Material material)
        {
            if ((b - a).sqrMagnitude < .0001f) return;
            var go = Box(parent, name, (a + b) * .5f, new Vector3(width, width, (b - a).magnitude), material);
            go.transform.localRotation = Quaternion.LookRotation(b - a, Vector3.up);
        }

        static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        static void MaterialOn(Transform root, Material material, bool recurse = false)
        {
            if (root == null) return;
            if (recurse) foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = material;
            else if (root.TryGetComponent<Renderer>(out var r)) r.sharedMaterial = material;
        }

        // A small, shared chamfer is visible on all large masonry and joinery edges. Meshes are cached;
        // the editor's prefab bake persists generated meshes/materials, while runtime falls back safely.
        static Mesh Block
        {
            get
            {
                if (block != null) return block;
                var vertices = new List<Vector3>(); var triangles = new List<int>(); var normals = new List<Vector3>(); var uv = new List<Vector2>();
                Action<Vector3[]> face = p =>
                {
                    var n = Vector3.Cross(p[1] - p[0], p[2] - p[0]).normalized;
                    var centre = Vector3.zero; foreach (var v in p) centre += v;
                    if (Vector3.Dot(n, centre) < 0f) { Array.Reverse(p); n = -n; }
                    int start = vertices.Count;
                    foreach (var v in p) { vertices.Add(v); normals.Add(n); uv.Add(new Vector2(v.x + v.z + .5f, v.y + v.z + .5f)); }
                    for (int i = 1; i < p.Length - 1; i++) { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
                };
                const float o = .5f, b = .455f;
                foreach (float sign in new[] { -1f, 1f })
                {
                    face(new[] { new Vector3(sign*o,-b,-b),new Vector3(sign*o,b,-b),new Vector3(sign*o,b,b),new Vector3(sign*o,-b,b) });
                    face(new[] { new Vector3(-b,sign*o,-b),new Vector3(b,sign*o,-b),new Vector3(b,sign*o,b),new Vector3(-b,sign*o,b) });
                    face(new[] { new Vector3(-b,-b,sign*o),new Vector3(b,-b,sign*o),new Vector3(b,b,sign*o),new Vector3(-b,b,sign*o) });
                }
                foreach (float a in new[] { -1f, 1f }) foreach (float c in new[] { -1f, 1f })
                {
                    face(new[] { new Vector3(a*o,c*b,-b),new Vector3(a*b,c*o,-b),new Vector3(a*b,c*o,b),new Vector3(a*o,c*b,b) });
                    face(new[] { new Vector3(a*o,-b,c*b),new Vector3(a*b,-b,c*o),new Vector3(a*b,b,c*o),new Vector3(a*o,b,c*b) });
                    face(new[] { new Vector3(-b,a*o,c*b),new Vector3(-b,a*b,c*o),new Vector3(b,a*b,c*o),new Vector3(b,a*o,c*b) });
                }
                foreach (float x in new[] { -1f, 1f }) foreach (float y in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                    face(new[] { new Vector3(x*o,y*b,z*b),new Vector3(x*b,y*o,z*b),new Vector3(x*b,y*b,z*o) });
                block = new Mesh { name = "PR_MG_BeveledBlock" };
                block.SetVertices(vertices); block.SetNormals(normals); block.SetUVs(0, uv); block.SetTriangles(triangles, 0); block.RecalculateBounds();
                return block;
            }
        }

        static Mesh Torus
        {
            get
            {
                if (torus != null) return torus;
                const int segments = 32, tube = 6;
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
                for (int i = 0; i <= segments; i++) for (int j = 0; j <= tube; j++)
                {
                    float a = i * Mathf.PI * 2f / segments, b = j * Mathf.PI * 2f / tube;
                    var normal = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(b), Mathf.Sin(a) * Mathf.Cos(b));
                    v.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) + normal * .065f); n.Add(normal); uv.Add(new Vector2(i / (float)segments, j / (float)tube));
                }
                for (int i = 0; i < segments; i++) for (int j = 0; j < tube; j++)
                { int a=i*(tube+1)+j,b=a+tube+1; indices.AddRange(new[] {a,b,a+1,a+1,b,b+1}); }
                torus = new Mesh { name = "PR_MG_Torus" }; torus.SetVertices(v); torus.SetNormals(n); torus.SetUVs(0,uv); torus.SetTriangles(indices,0); torus.RecalculateBounds(); return torus;
            }
        }
    }
}
