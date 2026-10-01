using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    // Meshes, textures and materials for the last scene, all made from code: the
    // project keeps no art assets for it yet. Smooth shaded, unlike PieceFigure's
    // faceted menu pieces, to match the PAWN RUSH picture the course is being drawn
    // to (Docs/Architecture/UI.md "결과 화면"). When the art team's models land they
    // replace these; callers only need the GameObjects back.
    public static class LastSceneArt
    {
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        public static Color Hex(int rgb) => PieceFigure.Hex(rgb);
        static Vector2 V(float x, float y) => new Vector2(x, y);

        // ---------- materials ----------

        public static Material Lit(int rgb, float gloss, float metal = 0f) => PieceFigure.Lit(Hex(rgb), gloss, metal);

        public static Material Textured(string key, Texture texture, float gloss, Vector2 tiling)
        {
            if (materials.TryGetValue("T" + key, out var cached) && cached != null) return cached;
            var material = new Material(PieceFigure.Lit(Color.white, gloss)) { name = "Last " + key, mainTexture = texture, mainTextureScale = tiling };
            return materials["T" + key] = material;
        }

        // Standard in cutout mode, for banners with a swallowtail.
        public static Material Cutout(string key, Texture texture, float gloss)
        {
            if (materials.TryGetValue("C" + key, out var cached) && cached != null) return cached;
            var material = new Material(PieceFigure.Lit(Color.white, gloss)) { name = "Last " + key, mainTexture = texture };
            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 1f);
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.EnableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.SetFloat("_Cutoff", .5f);
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            return materials["C" + key] = material;
        }

        // Unlit and blended with no fog (Sprites/Default is always in the build):
        // clouds, dust rings. `shared` false hands back a copy to fade on its own.
        public static Material Blended(string key, Texture texture, Color tint, bool shared = true)
        {
            if (shared && materials.TryGetValue("B" + key, out var cached) && cached != null) return cached;
            var shader = Shader.Find("Sprites/Default");
            var material = shader != null ? new Material(shader) : new Material(PieceFigure.Lit(tint, 0f));
            material.name = "Last " + key;
            if (texture != null) material.mainTexture = texture;
            material.color = tint;
            if (shared) materials["B" + key] = material;
            return material;
        }

        // For Graphics.DrawMeshInstanced (the confetti).
        public static Material Instanced(int rgb)
        {
            string key = "I" + rgb;
            if (materials.TryGetValue(key, out var cached) && cached != null) return cached;
            var material = new Material(Lit(rgb, .2f)) { name = "Last confetti " + rgb.ToString("X6"), enableInstancing = true };
            return materials[key] = material;
        }

        // A vertical gradient on the four sides of Skybox/6 Sided, as StageKit does
        // for the menus, in the reference picture's deep blue or a rain grey.
        public static Material Sky(bool sunny)
        {
            string key = sunny ? "Ssun" : "Sgrey";
            if (materials.TryGetValue(key, out var cached) && cached != null) return cached;
            var shader = Shader.Find("Skybox/6 Sided");
            if (shader == null) return null;
            Color top = Hex(sunny ? 0x2F7BDC : 0x55637A), mid = Hex(sunny ? 0x6AAEF0 : 0x7D8AA0), low = Hex(sunny ? 0xA9D4F8 : 0xA3AFC0);
            Color haze = Hex(sunny ? 0xD6ECFB : 0xC3CCD8);
            var side = new Texture2D(4, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Last sky " + key };
            for (int y = 0; y < 256; y++)
            {
                // v .5 is the horizon.
                float up = Mathf.Clamp01((y / 255f - .5f) * 2f);
                Color c = y < 128 ? haze : up < .12f ? Color.Lerp(haze, low, up / .12f) : up < .4f ? Color.Lerp(low, mid, (up - .12f) / .28f) : Color.Lerp(mid, top, (up - .4f) / .6f);
                for (int x = 0; x < 4; x++) side.SetPixel(x, y, c);
            }
            side.Apply();
            var material = new Material(shader) { name = "Last sky " + key };
            foreach (string face in new[] { "_FrontTex", "_BackTex", "_LeftTex", "_RightTex" }) material.SetTexture(face, side);
            material.SetTexture("_UpTex", Solid(top));
            material.SetTexture("_DownTex", Solid(haze));
            return materials[key] = material;
        }

        static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return texture;
        }

        // ---------- meshes ----------

        // Spins a (radius, height) profile round Y with smooth normals worked out
        // from the profile itself, so there is no seam. Repeat a point to keep a
        // hard edge there (a cylinder's rim). Profiles run bottom to top.
        public static Mesh Lathe(string key, Vector2[] profile, int segments)
        {
            if (meshes.TryGetValue("L" + key, out var cached) && cached != null) return cached;
            int n = profile.Length, row = segments + 1;
            var v = new Vector3[n * row];
            var normals = new Vector3[n * row];
            var uv = new Vector2[n * row];
            for (int i = 0; i < n; i++)
            {
                bool sharpBefore = i > 0 && (profile[i] - profile[i - 1]).sqrMagnitude < 1e-10f;
                bool sharpAfter = i < n - 1 && (profile[i + 1] - profile[i]).sqrMagnitude < 1e-10f;
                Vector2 before = i > 0 && !sharpBefore ? profile[i - 1] : profile[i];
                Vector2 after = i < n - 1 && !sharpAfter ? profile[i + 1] : profile[i];
                if (sharpBefore && i < n - 1) before = profile[i];
                if (sharpAfter && i > 0) after = profile[i];
                Vector2 tangent = after - before;
                if (tangent.sqrMagnitude < 1e-12f) tangent = i < n - 1 ? profile[Mathf.Min(n - 1, i + 2)] - profile[i] : profile[i] - profile[Mathf.Max(0, i - 2)];
                Vector2 normal = new Vector2(tangent.y, -tangent.x).normalized;
                for (int j = 0; j <= segments; j++)
                {
                    float a = Mathf.PI * 2 * j / segments, c = Mathf.Cos(a), s = Mathf.Sin(a);
                    v[i * row + j] = new Vector3(c * profile[i].x, profile[i].y, s * profile[i].x);
                    normals[i * row + j] = new Vector3(c * normal.x, normal.y, s * normal.x);
                    uv[i * row + j] = new Vector2((float)j / segments, (float)i / Mathf.Max(1, n - 1));
                }
            }
            var t = new List<int>();
            for (int i = 0; i < n - 1; i++)
            {
                if ((profile[i + 1] - profile[i]).sqrMagnitude < 1e-10f) continue;
                for (int j = 0; j < segments; j++)
                {
                    int a = i * row + j, b = a + row;
                    t.Add(a); t.Add(b); t.Add(a + 1);
                    t.Add(a + 1); t.Add(b); t.Add(b + 1);
                }
            }
            var mesh = new Mesh { name = "Last " + key, vertices = v, normals = normals, uv = uv };
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return meshes["L" + key] = mesh;
        }

        public static Mesh Cylinder(float rBottom, float rTop, float h, int segments = 24) =>
            Lathe("cyl " + rBottom + "_" + rTop + "_" + h + "_" + segments,
                  new[] { V(0, -h / 2), V(rBottom, -h / 2), V(rBottom, -h / 2), V(rTop, h / 2), V(rTop, h / 2), V(0, h / 2) }, segments);

        public static Mesh Cone(float r, float h, int segments = 20) =>
            Lathe("cone " + r + "_" + h + "_" + segments, new[] { V(0, -h / 2), V(r, -h / 2), V(r, -h / 2), V(0, h / 2) }, segments);

        public static Mesh Sphere(float r = .5f, int segments = 24, int rings = 16)
        {
            var profile = new Vector2[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float a = Mathf.PI * i / rings;
                profile[i] = new Vector2(Mathf.Sin(a) * r, -Mathf.Cos(a) * r);
            }
            return Lathe("sphere " + r + "_" + segments + "_" + rings, profile, segments);
        }

        // A box whose edges and corners are rounded by `radius`, smooth shaded:
        // the chunky red and cream blocks, the merlons.
        public static Mesh RoundedBox(Vector3 size, float radius, int cells = 5)
        {
            string key = "R" + size.x + "_" + size.y + "_" + size.z + "_" + radius;
            if (meshes.TryGetValue(key, out var cached) && cached != null) return cached;
            Vector3 half = size / 2, inner = half - Vector3.one * radius;
            inner = Vector3.Max(inner, Vector3.zero);
            // Each face as (normal, u, v) with Cross(u, v) = -normal: Unity's front faces.
            var faces = new[]
            {
                (Vector3.right, Vector3.forward, Vector3.up), (Vector3.left, Vector3.up, Vector3.forward),
                (Vector3.up, Vector3.right, Vector3.forward), (Vector3.down, Vector3.forward, Vector3.right),
                (Vector3.forward, Vector3.up, Vector3.right), (Vector3.back, Vector3.right, Vector3.up)
            };
            var v = new List<Vector3>();
            var normals = new List<Vector3>();
            var t = new List<int>();
            foreach (var (normal, u, w) in faces)
            {
                int start = v.Count;
                for (int i = 0; i <= cells; i++)
                    for (int j = 0; j <= cells; j++)
                    {
                        Vector3 q = normal + u * (2f * i / cells - 1f) + w * (2f * j / cells - 1f);
                        Vector3 p = Vector3.Scale(q, half);
                        Vector3 core = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 dir = p - core;
                        Vector3 nrm = dir.sqrMagnitude < 1e-10f ? normal : dir.normalized;
                        v.Add(core + nrm * radius);
                        normals.Add(nrm);
                    }
                for (int i = 0; i < cells; i++)
                    for (int j = 0; j < cells; j++)
                    {
                        int a = start + i * (cells + 1) + j, b = a + 1, c = a + cells + 1, d = c + 1;
                        t.Add(a); t.Add(b); t.Add(c);
                        t.Add(c); t.Add(b); t.Add(d);
                    }
            }
            var mesh = new Mesh { name = "Last rounded box" };
            mesh.SetVertices(v);
            mesh.SetNormals(normals);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return meshes[key] = mesh;
        }

        // The finish arch: a flat ∩ `w` wide and `h` high with a round-topped opening
        // `ow` wide and `oh` high, `d` deep on Z. Submesh 0 is the two faces, 1 the
        // sides (drawn darker, as in the picture).
        public static Mesh Arch(float w, float h, float ow, float oh, float d, int curve = 28)
        {
            string key = "A" + w + "_" + h + "_" + ow + "_" + oh + "_" + d;
            if (meshes.TryGetValue(key, out var cached) && cached != null) return cached;
            var v = new List<Vector3>();
            var normals = new List<Vector3>();
            var caps = new List<int>();
            var sides = new List<int>();
            float r = ow / 2, yc = oh - r, zf = -d / 2, zb = d / 2;
            var arc = new Vector2[curve + 1];
            for (int k = 0; k <= curve; k++)
            {
                float a = Mathf.PI * (1f - (float)k / curve);
                arc[k] = new Vector2(Mathf.Cos(a) * r, yc + Mathf.Sin(a) * r);
            }
            foreach (float z in new[] { zf, zb })
            {
                var n = new Vector3(0, 0, z < 0 ? -1 : 1);
                Quad(v, normals, caps, new Vector3(-w / 2, 0, z), new Vector3(-r, 0, z), new Vector3(-r, h, z), new Vector3(-w / 2, h, z), n);
                Quad(v, normals, caps, new Vector3(r, 0, z), new Vector3(w / 2, 0, z), new Vector3(w / 2, h, z), new Vector3(r, h, z), n);
                for (int k = 0; k < curve; k++)
                    Quad(v, normals, caps, new Vector3(arc[k].x, arc[k].y, z), new Vector3(arc[k + 1].x, arc[k + 1].y, z),
                         new Vector3(arc[k + 1].x, h, z), new Vector3(arc[k].x, h, z), n);
            }
            Quad(v, normals, sides, new Vector3(-w / 2, 0, zf), new Vector3(-w / 2, h, zf), new Vector3(-w / 2, h, zb), new Vector3(-w / 2, 0, zb), Vector3.left);
            Quad(v, normals, sides, new Vector3(w / 2, 0, zf), new Vector3(w / 2, h, zf), new Vector3(w / 2, h, zb), new Vector3(w / 2, 0, zb), Vector3.right);
            Quad(v, normals, sides, new Vector3(-w / 2, h, zf), new Vector3(w / 2, h, zf), new Vector3(w / 2, h, zb), new Vector3(-w / 2, h, zb), Vector3.up);
            Quad(v, normals, sides, new Vector3(-r, 0, zf), new Vector3(-r, yc, zf), new Vector3(-r, yc, zb), new Vector3(-r, 0, zb), Vector3.right);
            Quad(v, normals, sides, new Vector3(r, 0, zf), new Vector3(r, yc, zf), new Vector3(r, yc, zb), new Vector3(r, 0, zb), Vector3.left);
            for (int k = 0; k < curve; k++)
            {
                Vector3 n0 = -new Vector3(arc[k].x, arc[k].y - yc, 0).normalized, n1 = -new Vector3(arc[k + 1].x, arc[k + 1].y - yc, 0).normalized;
                int s = v.Count;
                v.Add(new Vector3(arc[k].x, arc[k].y, zf)); v.Add(new Vector3(arc[k + 1].x, arc[k + 1].y, zf));
                v.Add(new Vector3(arc[k + 1].x, arc[k + 1].y, zb)); v.Add(new Vector3(arc[k].x, arc[k].y, zb));
                normals.Add(n0); normals.Add(n1); normals.Add(n1); normals.Add(n0);
                AddFacing(sides, v, s, (n0 + n1) * .5f);
            }
            var mesh = new Mesh { name = "Last arch", subMeshCount = 2 };
            mesh.SetVertices(v);
            mesh.SetNormals(normals);
            mesh.SetTriangles(caps, 0);
            mesh.SetTriangles(sides, 1);
            mesh.RecalculateBounds();
            return meshes[key] = mesh;
        }

        // A closed prism from a 2D outline (XY), `depth` deep on Z: the crown emblem.
        public static Mesh Prism(string key, Vector2[] outline, float depth)
        {
            if (meshes.TryGetValue("P" + key, out var cached) && cached != null) return cached;
            var v = new List<Vector3>();
            var normals = new List<Vector3>();
            var t = new List<int>();
            var tris = Triangulate(outline);
            foreach (float z in new[] { -depth / 2, depth / 2 })
            {
                int s = v.Count;
                var n = new Vector3(0, 0, z < 0 ? -1 : 1);
                foreach (var p in outline) { v.Add(new Vector3(p.x, p.y, z)); normals.Add(n); }
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = s + tris[i], b = s + tris[i + 1], c = s + tris[i + 2];
                    if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), n) < 0) { int x = b; b = c; c = x; }
                    t.Add(a); t.Add(b); t.Add(c);
                }
            }
            // Outward is to the right of travel round a counter-clockwise outline.
            float area = 0;
            for (int i = 0; i < outline.Length; i++) area += Cross(outline[i], outline[(i + 1) % outline.Length]);
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 p = outline[i], q = outline[(i + 1) % outline.Length], e = q - p;
                Vector3 outward = (area > 0 ? new Vector3(e.y, -e.x, 0) : new Vector3(-e.y, e.x, 0)).normalized;
                Quad(v, normals, t, new Vector3(p.x, p.y, -depth / 2), new Vector3(q.x, q.y, -depth / 2), new Vector3(q.x, q.y, depth / 2), new Vector3(p.x, p.y, depth / 2), outward);
            }
            var mesh = new Mesh { name = "Last " + key };
            mesh.SetVertices(v);
            mesh.SetNormals(normals);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return meshes["P" + key] = mesh;
        }

        // The crown from the HUD and the loading screen, 1 wide and ~.75 high, centred.
        public static readonly Vector2[] CrownOutline =
        {
            V(-11 / 24f, -8 / 24f), V(-12 / 24f, 5 / 24f), V(-5 / 24f, 0), V(0, 10 / 24f), V(5 / 24f, 0), V(12 / 24f, 5 / 24f), V(11 / 24f, -8 / 24f)
        };

        // A flat rectangle seen from both sides (banners, flags), pivot at its centre.
        public static Mesh Panel(float w, float h)
        {
            string key = "Q" + w + "_" + h;
            if (meshes.TryGetValue(key, out var cached) && cached != null) return cached;
            var mesh = new Mesh { name = "Last panel" };
            Vector3[] corners = { new Vector3(-w / 2, -h / 2), new Vector3(w / 2, -h / 2), new Vector3(w / 2, h / 2), new Vector3(-w / 2, h / 2) };
            Vector2[] uv = { V(0, 0), V(1, 0), V(1, 1), V(0, 1) };
            var v = new List<Vector3>(); var n = new List<Vector3>(); var u = new List<Vector2>(); var t = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                int s = v.Count;
                for (int i = 0; i < 4; i++) { v.Add(corners[i]); n.Add(side == 0 ? Vector3.back : Vector3.forward); u.Add(uv[i]); }
                if (side == 0) { t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2); }
                else { t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3); }
            }
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, u); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return meshes[key] = mesh;
        }

        // A pennant pointing +X from its pole edge, both sides.
        public static Mesh Pennant()
        {
            if (meshes.TryGetValue("pennant", out var cached) && cached != null) return cached;
            var mesh = new Mesh { name = "Last pennant" };
            mesh.vertices = new[] { new Vector3(0, .3f), new Vector3(1.7f, 0), new Vector3(0, -.3f), new Vector3(0, .3f), new Vector3(1.7f, 0), new Vector3(0, -.3f) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.triangles = new[] { 0, 1, 2, 3, 5, 4 };
            mesh.RecalculateBounds();
            return meshes["pennant"] = mesh;
        }

        // A flat ring lying on XZ, both sides: the dust puff where a piece lands.
        public static Mesh Ring(float inner, float outer, int segments = 28)
        {
            string key = "G" + inner + "_" + outer;
            if (meshes.TryGetValue(key, out var cached) && cached != null) return cached;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                int s = v.Count;
                for (int j = 0; j <= segments; j++)
                {
                    float a = Mathf.PI * 2 * j / segments;
                    v.Add(new Vector3(Mathf.Cos(a) * inner, 0, Mathf.Sin(a) * inner)); v.Add(new Vector3(Mathf.Cos(a) * outer, 0, Mathf.Sin(a) * outer));
                    n.Add(side == 0 ? Vector3.up : Vector3.down); n.Add(side == 0 ? Vector3.up : Vector3.down);
                }
                for (int j = 0; j < segments; j++)
                {
                    int a = s + j * 2, b = a + 1, c = a + 2, d = a + 3;
                    if (side == 0) { t.Add(a); t.Add(c); t.Add(b); t.Add(b); t.Add(c); t.Add(d); }
                    else { t.Add(a); t.Add(b); t.Add(c); t.Add(b); t.Add(d); t.Add(c); }
                }
            }
            var mesh = new Mesh { name = "Last ring" };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return meshes[key] = mesh;
        }

        // Adds a quad (corners in loop order) facing `normal`, flat shaded.
        static void Quad(List<Vector3> v, List<Vector3> normals, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int s = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int i = 0; i < 4; i++) normals.Add(normal);
            AddFacing(t, v, s, normal);
        }

        static void AddFacing(List<int> t, List<Vector3> v, int s, Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(v[s + 1] - v[s], v[s + 2] - v[s]), normal) >= 0)
            { t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3); }
            else
            { t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2); }
        }

        // Ear clipping for a simple polygon, concave allowed (as in PieceFigure).
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

        // ---------- textures ----------

        // The glossy runway: cream and slate-blue tiles, two a side, with soft bevels.
        public static Texture2D Checker()
        {
            if (textures.TryGetValue("checker", out var cached) && cached != null) return cached;
            const int size = 256, half = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8, name = "Last checker"
            };
            Color light = Hex(0xF4EEE3), dark = Hex(0x66718D), seam = Hex(0x3B4258);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int cx = x / half, cy = y / half, lx = x % half, ly = y % half;
                    Color c = (cx + cy) % 2 == 1 ? dark : light;
                    // Light from the top left, a darker lower right: a slight bevel.
                    float shade = ((half - lx) + ly) / (2f * half) - .5f;
                    c = Color.Lerp(c, shade > 0 ? Color.white : Color.black, Mathf.Abs(shade) * .12f);
                    if (lx < 2 || ly < 2 || lx > half - 3 || ly > half - 3) c = Color.Lerp(c, seam, .45f);
                    pixels[y * size + x] = c;
                }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return textures["checker"] = texture;
        }

        // A hanging banner: team colour, gold border and top bar, a gold crown, and a
        // swallowtail cut out of the bottom.
        public static Texture2D Banner(int rgb)
        {
            string key = "banner" + rgb;
            if (textures.TryGetValue(key, out var cached) && cached != null) return cached;
            const int w = 128, h = 320, notch = 46, border = 7;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Last banner" };
            Color fill = Hex(rgb), gold = Hex(0xF2C14E), clear = new Color(0, 0, 0, 0);
            var crown = new Vector2[CrownOutline.Length];
            for (int i = 0; i < crown.Length; i++) crown[i] = new Vector2(w / 2f, h * .66f) + CrownOutline[i] * 84f;
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Distance up from the swallowtail's V at the bottom.
                    float vy = y - notch * (1f - Mathf.Abs(x - w / 2f) / (w / 2f));
                    if (vy < 0) { pixels[y * w + x] = clear; continue; }
                    bool edge = x < border || x >= w - border || y >= h - border - 10 || vy < border;
                    float sideShade = 1f - Mathf.Abs(x - w / 2f) / (w / 2f) * .18f;
                    Color c = edge ? gold : fill * sideShade;
                    if (!edge && InsidePolygon(new Vector2(x, y), crown)) c = gold;
                    c.a = 1f;
                    pixels[y * w + x] = c;
                }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return textures[key] = texture;
        }

        public static Texture2D CheckerFlag()
        {
            if (textures.TryGetValue("flag", out var cached) && cached != null) return cached;
            const int w = 64, h = 48, q = 8;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point, name = "Last flag" };
            Color ink = Hex(0x1E2030), paper = Hex(0xF7F7F2);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) texture.SetPixel(x, y, ((x / q + y / q) % 2 == 1) ? ink : paper);
            texture.Apply();
            return textures["flag"] = texture;
        }

        // A soft cloud: a few overlapping puffs, white on top and blue-grey underneath.
        public static Texture2D Cloud(bool sunny)
        {
            string key = sunny ? "cloud" : "rain cloud";
            if (textures.TryGetValue(key, out var cached) && cached != null) return cached;
            const int w = 256, h = 128;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Last " + key };
            var puffs = new[] { new Vector3(.28f, .38f, .2f), new Vector3(.46f, .55f, .26f), new Vector3(.66f, .46f, .22f), new Vector3(.8f, .34f, .15f), new Vector3(.38f, .3f, .2f) };
            Color top = sunny ? Color.white : Hex(0xD9DFE8), bottom = sunny ? Hex(0xD3E3F4) : Hex(0x96A2B4);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h, alpha = 0;
                    foreach (var puff in puffs)
                    {
                        float dx = (u - puff.x) / puff.z, dy = (v - puff.y) / (puff.z * 2f);
                        alpha = Mathf.Max(alpha, Mathf.Clamp01((1f - (dx * dx + dy * dy)) * 3f));
                    }
                    alpha *= Mathf.Clamp01(v * 6f);
                    Color c = Color.Lerp(bottom, top, Mathf.Clamp01(v * 1.6f - .1f));
                    c.a = alpha;
                    pixels[y * w + x] = c;
                }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return textures[key] = texture;
        }

        static bool InsidePolygon(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        // ---------- objects ----------

        public static GameObject Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position,
                                      Quaternion? rotation = null, Vector3? scale = null, bool shadow = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = scale ?? Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        public static GameObject Group(Transform parent, string name, Vector3 position, Quaternion? rotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            return go;
        }
    }
}
