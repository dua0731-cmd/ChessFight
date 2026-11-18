using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R688 (seoungtest, 승규 with Unity shots of the flat yellow marks: "6번 노란색 원형 그래픽 좋게 교체 7번 노란색 원형
    // 그래픽 좋게", "8번 ... 사진 효과 교체", "10번. 2번 째 사진 그래픽 좋게"): the mini-games' gold marks - rings, the
    // corner arrows, the shield rail's diamond - were one-colour lines (MiniGameParts.Loop, flat boxes) that read as
    // painted on. Here they are made as brass inlays, code-built meshes, no assets, nothing sent.
    // 2nd look (승규 with two more shots: "깜빡 거리고 디자인이 별로야"): the first version flickered - its underside lay
    // on the floor's own top (z-fighting: the corner ring broke into pieces) and its see-through light spill sorted
    // against the laundry's water - and its wide flat band plus the spill still read as yellow paint. Now:
    //   - no see-through spill at all (nothing transparent, nothing to sort), the metal lifted Lift off the floor and no
    //     face of it level with the floor;
    //   - an aged brass bezel instead of a flat band: two raised lips with a sunken channel between them and round
    //     rivets set in the channel (wide marks), a plain bevelled band for the thin ones; darker, less saturated
    //     brass, metallic, no glow of its own;
    //   - lit up only when a game says so (MarkGlow.Boost: a pressed gold tub, an open window), steadily, no breathing.
    public static class MiniGameMarks
    {
        static readonly Color Brass = new Color(.66f, .5f, .24f);
        public static readonly Color WarmGlow = new Color(1f, .7f, .32f);
        const float Lift = .012f;
        static Material brass;
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { brass = null; meshes.Clear(); }

        /// <summary>A closed brass bezel lying flat: a circle (sides 24 and up) or a polygon (4 = a square/diamond).
        /// radius to the band's middle (a polygon's: to its sides' middles); yaw turns a polygon about its middle.
        /// `glow` adds a MarkGlow the game can light up (its Boost); `light` is that glow's colour.</summary>
        public static Transform Ring(Transform parent, string name, Vector3 at, float radius, float width, int sides = 48,
                                     float yaw = 0f, Color? light = null, float height = .025f, bool glow = true)
        {
            var path = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (yaw + 90f) * Mathf.Deg2Rad + i * Mathf.PI * 2f / sides;
                path[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (sides <= 8 ? radius / Mathf.Cos(Mathf.PI / sides) : radius);
            }
            return Build(parent, name, at, path, true, width, height, light ?? WarmGlow, glow);
        }

        /// <summary>A brass chevron (an arrow head, "^") lying flat and pointing along +z turned by yaw.</summary>
        public static Transform Chevron(Transform parent, string name, Vector3 at, float size, float yaw, Color? light = null, float thickness = .3f)
        {
            var path = new[] { new Vector3(-size, 0f, -size * .5f), new Vector3(0f, 0f, size * .5f), new Vector3(size, 0f, -size * .5f) };
            var t = Build(parent, name, at, path, false, size * thickness, .035f, light ?? WarmGlow, true);
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        static Transform Build(Transform parent, string name, Vector3 at, Vector3[] path, bool closed, float width, float height, Color light, bool glow)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = at;

            var metal = new GameObject("Brass", typeof(MeshFilter), typeof(MeshRenderer));
            metal.transform.SetParent(root, false);
            metal.transform.localPosition = new Vector3(0f, Lift, 0f);
            metal.GetComponent<MeshFilter>().sharedMesh = Bezel(path, closed, width, height);
            var mr = metal.GetComponent<MeshRenderer>();
            mr.sharedMaterial = BrassMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (glow) root.gameObject.AddComponent<MarkGlow>().Init(mr, light);
            return root;
        }

        static Material BrassMaterial()
        {
            if (brass != null) return brass;
            brass = LitMaterials.NewLit(Brass, .62f, .9f);
            brass.name = "M_MarkBrass";
            LitMaterials.SetEmission(brass, Color.black);   // the keyword on; MarkGlow lights one mark through its block
            return brass;
        }

        // The path's miter directions (flat, scaled so the band keeps its width at the corners).
        static Vector3[] Sides(Vector3[] p, bool closed)
        {
            int n = p.Length;
            var side = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = closed ? p[(i - 1 + n) % n] : p[Mathf.Max(i - 1, 0)];
                Vector3 next = closed ? p[(i + 1) % n] : p[Mathf.Min(i + 1, n - 1)];
                Vector3 dIn = (p[i] - prev).normalized, dOut = (next - p[i]).normalized;
                if (dIn == Vector3.zero) dIn = dOut;
                if (dOut == Vector3.zero) dOut = dIn;
                Vector3 nIn = new Vector3(-dIn.z, 0f, dIn.x), nOut = new Vector3(-dOut.z, 0f, dOut.x);
                Vector3 m = (nIn + nOut).normalized;
                side[i] = m / Mathf.Max(Vector3.Dot(m, nOut), .35f);
            }
            return side;
        }

        // The cross-section, across the band from inside (-) to outside (+), as (offset, height) faces. Wide bands
        // (0.11 m and up): bevel, lip, wall down, channel, wall up, lip, bevel. Thin ones: bevel, top, bevel.
        static List<(float a, float ya, float b, float yb)> Profile(float width, float h)
        {
            float w = width * .5f;
            var f = new List<(float, float, float, float)>();
            if (width < .11f)
            {
                float bevel = w * .45f;
                f.Add((-w, 0f, -w + bevel, h));
                f.Add((-w + bevel, h, w - bevel, h));
                f.Add((w - bevel, h, w, 0f));
            }
            else
            {
                float lip = width * .24f, bevel = lip * .45f, deep = h * .35f;
                float l0 = -w, l1 = -w + bevel, l2 = -w + lip, r2 = w - lip, r1 = w - bevel, r0 = w;
                f.Add((l0, 0f, l1, h));             // inner bevel
                f.Add((l1, h, l2, h));              // inner lip
                f.Add((l2, h, l2, deep));           // wall down
                f.Add((l2, deep, r2, deep));        // channel
                f.Add((r2, deep, r2, h));           // wall up
                f.Add((r2, h, r1, h));              // outer lip
                f.Add((r1, h, r0, 0f));             // outer bevel
            }
            return f;
        }

        static Mesh Bezel(Vector3[] p, bool closed, float width, float height)
        {
            string key = "bezel" + closed + width + height + string.Join(",", p);
            if (meshes.TryGetValue(key, out var cached) && cached != null) return cached;
            var side = Sides(p, closed);
            var faces = Profile(width, height);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int n = p.Length, segs = closed ? n : n - 1;
            foreach (var f in faces)
            {
                int start = verts.Count;
                for (int i = 0; i < n; i++)
                {
                    verts.Add(p[i] + side[i] * f.a + Vector3.up * f.ya);
                    verts.Add(p[i] + side[i] * f.b + Vector3.up * f.yb);
                }
                for (int s = 0; s < segs; s++)
                {
                    // wound so the faces look up and out (Unity's front faces turn clockwise to the eye; the first
                    // version had them backwards, so from above only its floor-level underside showed - flat, flickering)
                    int i0 = start + s * 2, i1 = start + ((s + 1) % n) * 2;
                    tris.AddRange(new[] { i0, i0 + 1, i1, i1, i0 + 1, i1 + 1 });
                }
            }
            if (!closed)
                foreach (int end in new[] { 0, n - 1 })
                    foreach (bool flip in new[] { false, true })
                    {
                        // a flat end cap over the section's outline, one face each way, each on its own vertices.
                        // R688 (승규's Scene shot of the corner chevron under a white blow-out): the first caps shared
                        // their vertices between the two faces, so their normals cancelled to zero, lit to NaN, and
                        // bloom spread that over the screen.
                        int start = verts.Count;
                        Vector3 o = p[end], s = side[end];
                        verts.Add(o + s * faces[0].a);
                        foreach (var f in faces) verts.Add(o + s * f.b + Vector3.up * f.yb);
                        for (int k = 1; k < faces.Count; k++)
                            tris.AddRange(flip ? new[] { start, start + k + 1, start + k } : new[] { start, start + k, start + k + 1 });
                    }
            if (width >= .11f && closed) Rivets(p, width, height, verts, tris);
            var mesh = new Mesh { name = "Mark bezel" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            // no zero normal anywhere (a vertex only on flat-folded faces): it lights to NaN, which bloom blows up
            var normals = mesh.normals;
            bool fixedAny = false;
            for (int i = 0; i < normals.Length; i++)
                if (!(normals[i].sqrMagnitude > 1e-8f)) { normals[i] = Vector3.up; fixedAny = true; }
            if (fixedAny) mesh.normals = normals;
            mesh.RecalculateBounds();
            meshes[key] = mesh;
            return mesh;
        }

        // Round rivet heads in the channel, about every 0.3 m along it (a low 8-sided dome each).
        static void Rivets(Vector3[] p, float width, float h, List<Vector3> verts, List<int> tris)
        {
            float length = 0f;
            int n = p.Length;
            for (int i = 0; i < n; i++) length += Vector3.Distance(p[i], p[(i + 1) % n]);
            int count = Mathf.Max(4, Mathf.RoundToInt(length / .3f));
            float step = length / count, r = Mathf.Min(width * .17f, .03f), deep = h * .35f;
            int seg = 0;
            float walked = 0f, segLen = Vector3.Distance(p[0], p[1 % n]);
            for (int k = 0; k < count; k++)
            {
                float want = (k + .5f) * step;
                while (walked + segLen < want && seg < n - 1) { walked += segLen; seg++; segLen = Vector3.Distance(p[seg], p[(seg + 1) % n]); }
                float u = segLen > 1e-5f ? (want - walked) / segLen : 0f;
                var c = Vector3.Lerp(p[seg], p[(seg + 1) % n], u) + Vector3.up * deep;
                int start = verts.Count;
                verts.Add(c + Vector3.up * r * .9f);
                for (int j = 0; j < 8; j++)
                {
                    float a = j * Mathf.PI / 4f;
                    verts.Add(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r + Vector3.up * r * .25f);
                }
                for (int j = 0; j < 8; j++) tris.AddRange(new[] { start, start + 1 + (j + 1) % 8, start + 1 + j });
            }
        }
    }

    /// <summary>A mark's brass lit up from within while Boost is up (a pressed tub, an open window); steady, no
    /// flicker. Through the renderer's property block, so the shared material stays dark.</summary>
    public sealed class MarkGlow : MonoBehaviour
    {
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        Renderer metal;
        Color tint;
        MaterialPropertyBlock block;
        float shown = -1f;
        public float Boost;     // 0 plain brass .. 1 glowing

        public void Init(Renderer r, Color c)
        {
            metal = r;
            tint = c;
            block = new MaterialPropertyBlock();
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            if (metal == null) return;
            float k = Mathf.Clamp01(Boost);
            if (Mathf.Abs(k - shown) < .002f) return;
            shown = k;
            metal.GetPropertyBlock(block);
            block.SetColor(EmissionId, tint * (k * 1.6f));
            metal.SetPropertyBlock(block);
        }
    }
}
