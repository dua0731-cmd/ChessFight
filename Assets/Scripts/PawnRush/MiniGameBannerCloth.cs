using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R505: a hanging banner that sways, made in code (the user, 11-07: a model's cloth cannot move, so hanging cloth is
    // code). A two-sided grid hangs from its top edge (the rod, fixed) and ripples more toward the bottom, like cloth in
    // a light wind. Picture only: no collider, not synced (each PC sways on its own).
    public sealed class MiniGameBannerCloth : MonoBehaviour
    {
        const int Cols = 8, Rows = 12;
        Mesh mesh;
        Vector3[] rest, now;
        float width, height, seed;

        // Builds the cloth under parent with its top edge's middle at localTop, w wide and h long, facing local -z.
        public static MiniGameBannerCloth Hang(Transform parent, Vector3 localTop, float w, float h, Material material)
        {
            var go = new GameObject("Banner cloth (sways)", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localTop;
            var c = go.AddComponent<MiniGameBannerCloth>();
            c.width = w; c.height = h; c.seed = Random.value * 10f;
            c.Build();
            go.GetComponent<MeshFilter>().sharedMesh = c.mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return c;
        }

        void Build()
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int side = 0; side < 2; side++)                   // front and back, so it shows from both sides
                for (int r = 0; r <= Rows; r++)
                    for (int c = 0; c <= Cols; c++)
                    {
                        v.Add(new Vector3((c / (float)Cols - .5f) * width, -r / (float)Rows * height, 0f));
                        uv.Add(new Vector2(c / (float)Cols, 1f - r / (float)Rows));
                    }
            int stride = Cols + 1, back = (Rows + 1) * stride;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    int a = r * stride + c, b = a + 1, d = a + stride, e = d + 1;
                    tri.AddRange(new[] { a, b, d, b, e, d });                                   // front (toward -z)
                    tri.AddRange(new[] { back + a, back + d, back + b, back + b, back + d, back + e });
                }
            mesh = new Mesh { name = "Banner cloth" };
            mesh.MarkDynamic();
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            rest = v.ToArray(); now = v.ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }

        void Update()
        {
            if (mesh == null) return;
            float t = Time.time;
            for (int i = 0; i < rest.Length; i++)
            {
                var p = rest[i];
                float down = -p.y / height;                       // 0 at the rod, 1 at the hem
                float u = p.x / width;
                float sway = Mathf.Pow(down, 1.5f);
                float wave = Mathf.Sin(t * 2.2f + down * 3.2f + u * 2.4f + seed) * .16f + Mathf.Sin(t * 1.15f + seed * 2f) * .07f;
                now[i] = new Vector3(p.x + Mathf.Sin(t * .9f + seed) * .05f * sway, p.y + Mathf.Abs(wave) * .05f * sway, wave * sway);
            }
            mesh.vertices = now;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
