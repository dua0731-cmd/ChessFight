using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The shapes of the Sword Fight demo (R149, "소드 파이트 핵심 규칙 시연 — 떨어뜨리고 빼앗기", Docs/GameModes/SwordFight/
    /// DEMO.md): its turned chess pieces (lathe profiles, the same numbers as the demo's three.js), the stone rook
    /// pedestal, and the materials it is lit with. The ghost pieces, the pieces lying on the trays and the pawn the hand
    /// lowers are these; the fighters stay the ragdolls.
    /// </summary>
    public static class SwordFightShapes
    {
        static readonly Vector2[] Base = { new Vector2(0f, 0f), new Vector2(0.36f, 0f), new Vector2(0.36f, 0.08f), new Vector2(0.28f, 0.14f), new Vector2(0.21f, 0.22f) };

        static Vector2[] With(params float[] ry)
        {
            var list = new List<Vector2>(Base);
            for (int i = 0; i + 1 < ry.Length; i += 2) list.Add(new Vector2(ry[i], ry[i + 1]));
            return list.ToArray();
        }

        /// <summary>A surface of revolution round y from (radius, height) points, <paramref name="segments"/> round.</summary>
        public static Mesh Lathe(Vector2[] profile, int segments = 26)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int rows = profile.Length;
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                float c = Mathf.Cos(a), n = Mathf.Sin(a);
                foreach (var p in profile) verts.Add(new Vector3(p.x * c, p.y, p.x * n));
            }
            for (int s = 0; s < segments; s++)
                for (int r = 0; r + 1 < rows; r++)
                {
                    int i0 = s * rows + r, i1 = (s + 1) * rows + r;
                    tris.AddRange(new[] { i0, i0 + 1, i1, i1, i0 + 1, i1 + 1 });
                }
            var m = new Mesh { name = "Lathe" };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static readonly Dictionary<string, Mesh> lathes = new Dictionary<string, Mesh>();

        static Mesh Cached(string key, Vector2[] profile)
        {
            if (!lathes.TryGetValue(key, out var m) || m == null) lathes[key] = m = Lathe(profile);
            return m;
        }

        public static GameObject MeshObject(string name, Mesh mesh, Material mat, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>A primitive without its collider.</summary>
        public static GameObject Part(PrimitiveType type, Material mat, Transform parent, Vector3 at, Vector3 scale, Quaternion? turn = null)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            if (turn.HasValue) go.transform.localRotation = turn.Value;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>The demo's turned piece (about 1.1–1.4 m tall at scale 1), its foot at the origin.</summary>
        public static GameObject Piece(PieceKind kind, Material mat, Material trim = null)
        {
            var g = new GameObject("Turned " + kind);
            var t = g.transform;
            switch (kind)
            {
                case PieceKind.Pawn:
                    MeshObject("body", Cached("pawn", With(0.155f, 0.45f, 0.135f, 0.6f, 0.25f, 0.64f, 0.25f, 0.69f, 0.13f, 0.73f, 0f, 0.73f)), mat, t);
                    Part(PrimitiveType.Sphere, mat, t, new Vector3(0f, 0.9f, 0f), Vector3.one * 0.4f);
                    break;
                case PieceKind.King:
                    MeshObject("body", Cached("king", new[] { new Vector2(0, 0), new Vector2(0.4f, 0), new Vector2(0.4f, 0.1f), new Vector2(0.3f, 0.16f), new Vector2(0.22f, 0.26f),
                        new Vector2(0.17f, 0.75f), new Vector2(0.3f, 0.82f), new Vector2(0.17f, 0.88f), new Vector2(0.27f, 1.12f), new Vector2(0.22f, 1.2f), new Vector2(0, 1.2f) }), mat, t);
                    Part(PrimitiveType.Cube, mat, t, new Vector3(0f, 1.35f, 0f), new Vector3(0.07f, 0.3f, 0.07f));
                    Part(PrimitiveType.Cube, mat, t, new Vector3(0f, 1.38f, 0f), new Vector3(0.22f, 0.07f, 0.07f));
                    break;
                case PieceKind.Rook:
                    MeshObject("body", Cached("rook", With(0.2f, 0.75f, 0.3f, 0.85f, 0.3f, 1.08f, 0f, 1.08f)), mat, t);
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k / 6f * Mathf.PI * 2f;
                        Part(PrimitiveType.Cube, mat, t, new Vector3(Mathf.Cos(a) * 0.24f, 1.15f, Mathf.Sin(a) * 0.24f), Vector3.one * 0.13f);
                    }
                    break;
                case PieceKind.Bishop:
                    MeshObject("body", Cached("bishop", With(0.15f, 0.6f, 0.26f, 0.66f, 0.15f, 0.72f, 0.23f, 0.9f, 0.21f, 1.05f, 0.12f, 1.2f, 0.04f, 1.27f, 0f, 1.27f)), mat, t);
                    Part(PrimitiveType.Cube, trim != null ? trim : mat, t, new Vector3(0.1f, 1.05f, 0f), new Vector3(0.04f, 0.18f, 0.3f), Quaternion.Euler(0f, 0f, 0.5f * Mathf.Rad2Deg));
                    break;
                case PieceKind.Knight:
                    MeshObject("body", Cached("knight", With(0.2f, 0.5f, 0f, 0.5f)), mat, t);
                    Part(PrimitiveType.Cube, mat, t, new Vector3(0f, 0.78f, -0.02f), new Vector3(0.3f, 0.6f, 0.42f), Quaternion.Euler(-0.25f * Mathf.Rad2Deg, 0f, 0f));
                    Part(PrimitiveType.Cube, mat, t, new Vector3(0f, 1.0f, 0.22f), new Vector3(0.26f, 0.24f, 0.42f), Quaternion.Euler(0.35f * Mathf.Rad2Deg, 0f, 0f));
                    break;
                default:   // the queen
                    MeshObject("body", Cached("queen", With(0.14f, 0.7f, 0.27f, 0.8f, 0.16f, 0.86f, 0.27f, 1.1f, 0f, 1.1f)), mat, t);
                    for (int k = 0; k < 7; k++)
                    {
                        float a = k / 7f * Mathf.PI * 2f;
                        Part(PrimitiveType.Sphere, trim != null ? trim : mat, t, new Vector3(Mathf.Cos(a) * 0.23f, 1.14f, Mathf.Sin(a) * 0.23f), Vector3.one * 0.11f);
                    }
                    break;
            }
            return g;
        }

        /// <summary>The demo's stone rook pedestal (scaled 0.85 × 0.5 × 0.85 as there), with a convex collider.</summary>
        public static GameObject Pedestal(Material stone, Transform parent, Vector3 at)
        {
            var g = new GameObject("Rook pedestal");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = at;
            g.transform.localScale = new Vector3(0.85f, 0.5f, 0.85f);
            var profile = new[] { new Vector2(0, 0), new Vector2(0.85f, 0), new Vector2(0.85f, 0.15f), new Vector2(0.7f, 0.25f), new Vector2(0.62f, 0.95f),
                new Vector2(0.8f, 1.05f), new Vector2(0.8f, 1.35f), new Vector2(0, 1.35f) };
            var mesh = Cached("pedestal", profile);
            var body = MeshObject("stone", mesh, stone, g.transform);
            var col = body.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            col.convex = true;
            for (int k = 0; k < 6; k++)
            {
                float a = k / 6f * Mathf.PI * 2f;
                Part(PrimitiveType.Cube, stone, g.transform, new Vector3(Mathf.Cos(a) * 0.62f, 1.46f, Mathf.Sin(a) * 0.62f), new Vector3(0.28f, 0.22f, 0.28f));
            }
            return g;
        }

        // ---------------------------------------------------------------- materials

        /// <summary>The Standard shader in a colour (three.js roughness / metalness as glossiness / metallic).</summary>
        public static Material Lit(string hex, float roughness = 0.6f, float metal = 0f)
        {
            var m = new Material(Shader.Find("Standard")) { name = "Demo " + hex };
            m.color = Hex(hex);
            m.SetFloat("_Glossiness", 1f - roughness);
            m.SetFloat("_Metallic", metal);
            return m;
        }

        /// <summary>Unlit and see-through (Sprites/Default): ghosts, rings, the bubble, the pillar.</summary>
        public static Material Glow(string hex, float alpha)
        {
            var m = new Material(Shader.Find("Sprites/Default")) { name = "Demo glow " + hex, renderQueue = 3100 };
            var c = Hex(hex);
            c.a = alpha;
            m.color = c;
            return m;
        }

        /// <summary>"#rrggbb" in plain C# (no engine call: static colour fields use it while a component is made).</summary>
        public static Color Hex(string hex)
        {
            string h = hex.TrimStart('#');
            if (h.Length != 6) return Color.magenta;
            int v = System.Convert.ToInt32(h, 16);
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
        }
    }
}
