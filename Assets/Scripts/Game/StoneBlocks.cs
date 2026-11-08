using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Game
{
    // Real stone blocks made from code (10-17, 승규 님: walls and pillars with more polygons, the same
    // in the title hall and the lobby castle): rounded boxes with uneven faces and a fine sandstone grain
    // with its relief, and the helpers that lay them as a wall face or round a tower in running bond.
    // Used by IntroStage.Pier.cs and LobbyStage.Blocks.cs.
    internal static class StoneBlocks
    {
        static Texture2D grain, grainNormal;

        internal static readonly Color[] Shades =
            { MenuArt.Hex(0xEDE1C6), MenuArt.Hex(0xE4D6B6), MenuArt.Hex(0xDACAA6), MenuArt.Hex(0xD1BF99), MenuArt.Hex(0xE8DBBE) };

        // A rounded box (as LastSceneArt.RoundedBox) with `cells` squares a face, its flat parts pushed
        // in and out by up to `bump` with noise, UVs projected along each face in metres.
        internal static Mesh Block(Vector3 size, float radius, int cells, float bump, float seed)
        {
            Vector3 half = size / 2, inner = Vector3.Max(half - Vector3.one * radius, Vector3.zero);
            var faces = new[]
            {
                (Vector3.right, Vector3.forward, Vector3.up), (Vector3.left, Vector3.up, Vector3.forward),
                (Vector3.up, Vector3.right, Vector3.forward), (Vector3.down, Vector3.forward, Vector3.right),
                (Vector3.forward, Vector3.up, Vector3.right), (Vector3.back, Vector3.right, Vector3.up)
            };
            var v = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            foreach (var (normal, u, w) in faces)
            {
                int start = v.Count;
                for (int i = 0; i <= cells; i++)
                    for (int j = 0; j <= cells; j++)
                    {
                        // Denser towards the edges, where the bevel bends.
                        float fi = Ease(i / (float)cells), fj = Ease(j / (float)cells);
                        Vector3 q = normal + u * (2f * fi - 1f) + w * (2f * fj - 1f);
                        Vector3 p = Vector3.Scale(q, half);
                        Vector3 core = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 dir = p - core;
                        Vector3 nrm = dir.sqrMagnitude < 1e-10f ? normal : dir.normalized;
                        var point = core + nrm * radius;
                        // Uneven face: strongest in the middle of a face, none on the arrises.
                        float flat = Mathf.Clamp01(Vector3.Dot(nrm, normal) * 4f - 3f);
                        float noise = Mathf.PerlinNoise(point.x * 5f + point.z * 3f + seed, point.y * 5f + point.z * 2f + seed) - .5f;
                        point += normal * noise * bump * flat * Mathf.Min(1f, (1f - Mathf.Abs(2f * fi - 1f)) * 3f) * Mathf.Min(1f, (1f - Mathf.Abs(2f * fj - 1f)) * 3f);
                        v.Add(point);
                        normals.Add(nrm);
                        uv.Add(new Vector2(Vector3.Dot(point, u) + seed, Vector3.Dot(point, w) + seed * .7f));
                    }
                for (int i = 0; i < cells; i++)
                    for (int j = 0; j < cells; j++)
                    {
                        int a = start + i * (cells + 1) + j, b = a + 1, c = a + cells + 1, d = c + 1;
                        t.Add(a); t.Add(b); t.Add(c);
                        t.Add(c); t.Add(b); t.Add(d);
                    }
            }
            var mesh = new Mesh { name = "Intro pier block" };
            mesh.SetVertices(v); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        static float Ease(float x) => .5f - .5f * Mathf.Cos(x * Mathf.PI);

        // A lit material with the stone grain and its relief.
        internal static Material Grained(string name, Color color, float gloss)
        {
            Grain();
            var material = new Material(PieceFigure.Lit(color, gloss)) { name = name, mainTexture = grain, color = color };
            material.mainTextureScale = new Vector2(1.6f, 1.6f);
            material.SetTexture("_BumpMap", grainNormal);
            material.SetTextureScale("_BumpMap", new Vector2(1.6f, 1.6f));
            material.SetFloat("_BumpScale", .6f);
            material.EnableKeyword("_NORMALMAP");
            return material;
        }

        // Fine sandstone grain: soft mottling, a few pores and darker flecks, near white so the
        // block's own shade shows through; and its relief as a normal map.
        static void Grain()
        {
            if (grain != null) return;
            const int n = 256;
            var height = new float[n * n];
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Tileable: noise sampled round two circles.
                    float ax = x / (float)n * 2 * Mathf.PI, ay = y / (float)n * 2 * Mathf.PI;
                    float cx = Mathf.Cos(ax), sx = Mathf.Sin(ax), cy = Mathf.Cos(ay), sy = Mathf.Sin(ay);
                    float Noise(float f, float o) => Mathf.PerlinNoise((cx + sy * .5f) * f + o, (sx + cy * .5f) * f + o)
                                                   * .5f + Mathf.PerlinNoise((cy + sx * .5f) * f + o + 7f, (sy + cx * .5f) * f + o + 3f) * .5f;
                    float mottle = Noise(2.5f, 0f), fine = Noise(14f, 20f), pore = Noise(30f, 40f);
                    float h = mottle * .5f + fine * .4f - Mathf.Clamp01((pore - .62f) * 8f) * .5f;
                    height[y * n + x] = h;
                    float shade = .9f + mottle * .12f + (fine - .5f) * .08f - Mathf.Clamp01((pore - .62f) * 8f) * .12f;
                    px[y * n + x] = new Color(shade, shade * .985f, shade * .96f, 1f);
                }
            grain = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Intro stone grain", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            grain.SetPixels(px); grain.Apply(true);
            var nm = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = height[y * n + (x + 1) % n] - height[y * n + (x + n - 1) % n];
                    float dy = height[((y + 1) % n) * n + x] - height[((y + n - 1) % n) * n + x];
                    var v = new Vector3(-dx * 3f, -dy * 3f, 1f).normalized;
                    nm[y * n + x] = new Color(1f, v.y * .5f + .5f, 1f, v.x * .5f + .5f);
                }
            grainNormal = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { name = "Intro stone grain normal", wrapMode = TextureWrapMode.Repeat };
            grainNormal.SetPixels(nm); grainNormal.Apply(true);
        }
        static Material[] shadeMaterials;

        // One grained material per shade, kept for the whole session (shared by every scene's blocks).
        internal static Material ShadeMaterial(int i)
        {
            if (shadeMaterials == null) shadeMaterials = new Material[Shades.Length];
            if (shadeMaterials[i] == null) shadeMaterials[i] = Grained("Stone block " + i, Shades[i], .18f);
            return shadeMaterials[i];
        }
    }

    // Lays blocks and merges them into one mesh per shade, so hundreds of blocks cost a few draw calls.
    // Positions are in the space of the parent passed to Build.
    internal sealed class Mason
    {
        readonly System.Random random;
        readonly List<CombineInstance>[] lists = new List<CombineInstance>[StoneBlocks.Shades.Length];
        readonly Dictionary<string, Mesh[]> shapes = new Dictionary<string, Mesh[]>();
        public readonly List<Mesh> Meshes = new List<Mesh>();
        public int Count;

        public Mason(int seed)
        {
            random = new System.Random(seed);
            for (int i = 0; i < lists.Length; i++) lists[i] = new List<CombineInstance>();
        }

        float Next(float range) => ((float)random.NextDouble() - .5f) * range;

        // A few shapes per size (rounded to centimetres), reused at random.
        Mesh Shape(Vector3 size)
        {
            size = new Vector3(Mathf.Round(size.x * 100f) / 100f, Mathf.Round(size.y * 100f) / 100f, Mathf.Round(size.z * 100f) / 100f);
            string key = size.x + "_" + size.y + "_" + size.z;
            if (!shapes.TryGetValue(key, out var set))
            {
                set = new Mesh[size.x > .4f ? 6 : 3];
                for (int i = 0; i < set.Length; i++)
                {
                    set[i] = StoneBlocks.Block(size, .028f, 7, .007f, (float)random.NextDouble() * 50f);
                    Meshes.Add(set[i]);
                }
                shapes[key] = set;
            }
            return set[random.Next(set.Length)];
        }

        // One block, its local Z along `inward` (into the wall), centred at `centre`.
        public void Add(Vector3 size, Vector3 centre, Vector3 inward, Vector3 up)
        {
            var rotation = Quaternion.LookRotation(inward, up) * Quaternion.Euler(Next(.7f), Next(.9f), random.Next(2) * 180f + Next(.6f));
            var instance = new CombineInstance { mesh = Shape(size), transform = Matrix4x4.TRS(centre, rotation, Vector3.one) };
            lists[random.Next(lists.Length)].Add(instance);
            Count++;
        }

        // A flat face in running bond: `origin` is its bottom-left corner on the surface, `right` and `up`
        // span it, `outward` points out of it. Blocks stand `proud` out of the surface. `skip` gets the
        // block's centre in (right, up) metres from the origin.
        public void Face(Vector3 origin, Vector3 right, Vector3 up, Vector3 outward, float width, float height,
                         float blockWidth, float blockHeight, float depth, float proud, System.Func<float, float, bool> skip = null)
        {
            const float gap = .016f;
            int rows = Mathf.Max(1, Mathf.RoundToInt(height / blockHeight));
            float bh = height / rows;
            for (int row = 0; row < rows; row++)
            {
                float v = (row + .5f) * bh;
                float u0 = row % 2 == 0 ? 0f : -blockWidth / 2f;
                for (float u = u0; u < width - .05f; u += blockWidth)
                {
                    float a = Mathf.Max(0f, u), b = Mathf.Min(width, u + blockWidth);
                    if (b - a < .12f) continue;
                    float uc = (a + b) / 2f;
                    if (skip != null && skip(uc, v)) continue;
                    var centre = origin + right * uc + up * (v + Next(.005f)) + outward * (proud - depth / 2f + Next(.01f));
                    Add(new Vector3(b - a - gap, bh - gap, depth), centre, -outward, up);
                }
            }
        }

        // Blocks round a round tower from y0 to y1 (in the parent's space), courses offset by half a block;
        // `keep` gets each block's direction from the axis and says whether to lay it (the side the camera sees).
        public void Ring(Vector3 axis, float radius, float y0, float y1, float blockWidth, float blockHeight, float depth, float proud,
                         System.Func<Vector3, bool> keep)
        {
            const float gap = .016f;
            int n = Mathf.Max(6, Mathf.RoundToInt(2 * Mathf.PI * radius / blockWidth));
            int rows = Mathf.Max(1, Mathf.RoundToInt((y1 - y0) / blockHeight));
            float bh = (y1 - y0) / rows;
            float chord = 2f * (radius + proud) * Mathf.Sin(Mathf.PI / n);
            for (int row = 0; row < rows; row++)
            {
                float y = y0 + (row + .5f) * bh;
                for (int i = 0; i < n; i++)
                {
                    float angle = (i + (row % 2 == 0 ? 0f : .5f)) / n * 2f * Mathf.PI;
                    var dir = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                    if (keep != null && !keep(dir)) continue;
                    var centre = new Vector3(axis.x, y + Next(.005f), axis.z) + dir * (radius + proud - depth / 2f + Next(.01f));
                    Add(new Vector3(chord - gap, bh - gap, depth), centre, -dir, Vector3.up);
                }
            }
        }

        // The merged meshes, one child of `parent` per shade.
        public void Build(Transform parent, string name)
        {
            for (int i = 0; i < lists.Length; i++)
            {
                if (lists[i].Count == 0) continue;
                var mesh = new Mesh { name = name + " " + i, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(lists[i].ToArray(), true, true);
                mesh.RecalculateBounds();
                Meshes.Add(mesh);
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = StoneBlocks.ShadeMaterial(i);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        public void Dispose()
        {
            foreach (var m in Meshes) if (m != null) Object.Destroy(m);
            Meshes.Clear();
        }
    }
}
