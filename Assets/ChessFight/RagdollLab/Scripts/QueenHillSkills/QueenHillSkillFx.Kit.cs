using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The parts the Queen of the Hill skill effects are drawn with (R89, design A "잉크 테두리 장난감 체스"): flat inked
    /// shapes on the floor (board squares, rings, a horseshoe, bars: <c>ChessFight/Skill Tile</c>), cartoon puffs and
    /// drops (the Pawn Rush effects' <c>ChessFight/Skill Toon</c>), inked ribbons (<c>ChessFight/Skill Band</c>) and
    /// chunky props with an ink rim (<c>ChessFight/Skill Ink</c> + <c>Skill Ink Line</c>). Every shape is made in code.
    /// </summary>
    public partial class QueenHillSkillFx
    {
        // ---------------------------------------------------------------- colours (design A, viewer-relative sides)

        static Color Hex(string hex, float a = 1f)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            c.a = a;
            return c;
        }

        public struct Palette
        {
            public Color main, light, deep, ink;
            public Palette(string main, string light, string deep, string ink)
            {
                this.main = Hex(main);
                this.light = Hex(light);
                this.deep = Hex(deep);
                this.ink = Hex(ink);
            }
        }

        static readonly Palette King = new Palette("#34B85A", "#E2FFE6", "#1A7A36", "#0A3318");
        static readonly Palette Queen = new Palette("#FFD34D", "#FFF0A8", "#E89B1A", "#3A2208");
        static readonly Palette Rook = new Palette("#FF8A1F", "#FFF3C4", "#C2410C", "#3A1606");
        static readonly Palette Bishop = new Palette("#994DFF", "#E6D6FF", "#6A2FC9", "#2A0F57");
        static readonly Palette Knight = new Palette("#6BB3FF", "#DBF2FF", "#1F5CF2", "#0A144D");
        static readonly Palette TeamAlly = new Palette("#2EC4E6", "#FFF1D2", "#1593B0", "#0B4F5C");
        static readonly Palette TeamEnemy = new Palette("#FF3B4E", "#FFE1E4", "#C81E33", "#5A0F1A");
        static readonly Palette Dust = new Palette("#D9C3A0", "#FFF1D2", "#8F7A63", "#2B1A0E");
        static readonly Palette Grey = new Palette("#9AA3B5", "#DDE2EC", "#6B7385", "#2D3344");
        static readonly Palette Stone = new Palette("#8E8A96", "#C9C4D1", "#4D4757", "#1E1A26");
        static readonly Color SelfFill = Hex("#FFF4DC"), SelfInk = Hex("#1B2340");
        static readonly Color SocketColor = Hex("#1E1A26"), Walnut = Hex("#8C613D");
        static readonly Color GoldAccent = Hex("#FFD34D");

        enum Side { Self, Ally, Enemy }

        /// <summary>Whose it is, as the viewer (P1) sees it: mine, my side's or the other side's.</summary>
        Side SideOf(RagdollPawn pawn)
        {
            var viewer = Viewer;
            if (pawn == null || viewer == null) return Side.Enemy;
            if (pawn == viewer) return Side.Self;
            return viewer.Team != Teams.None && pawn.Team == viewer.Team ? Side.Ally : Side.Enemy;
        }

        /// <summary>A piece's own colours; the pawn has none of its own and wears its side's (teal us, red them).</summary>
        Palette PieceColors(RagdollPawn pawn)
        {
            if (pawn == null) return Grey;
            switch (pawn.Piece)
            {
                case PieceKind.King: return King;
                case PieceKind.Queen: return Queen;
                case PieceKind.Rook: return Rook;
                case PieceKind.Bishop: return Bishop;
                case PieceKind.Knight: return Knight;
                default: return SideOf(pawn) == Side.Enemy ? TeamEnemy : TeamAlly;
            }
        }

        // ---------------------------------------------------------------- meshes

        Mesh meshQuad, meshPuff, meshTorus, meshBall;
        readonly Dictionary<Vector4, Mesh> roundBoxes = new Dictionary<Vector4, Mesh>();

        static Mesh Quad()
        {
            var m = new Mesh { name = "Skill tile quad" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            m.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.1f));
            return m;
        }

        /// <summary>A sphere 1 across made of triangles of about the same size (the toon puffs lump it by noise).</summary>
        static Mesh Icosphere(int subdivisions)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            var tris = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var cache = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
                if (cache.TryGetValue(key, out int i)) return i;
                verts.Add((verts[a] + verts[b]) * 0.5f);
                cache[key] = verts.Count - 1;
                return verts.Count - 1;
            }
            for (int s = 0; s < subdivisions; s++)
            {
                var next = new List<int>();
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = next;
            }
            var normals = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                normals[i] = verts[i].normalized;
                verts[i] = normals[i] * 0.5f;
            }
            var m = new Mesh { name = "Skill puff" };
            m.SetVertices(verts);
            m.normals = normals;
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>A box with rounded edges and corners and smooth normals all round (so its ink rim has no cracks),
        /// size across, corner radius r.</summary>
        Mesh RoundBox(Vector3 size, float r, int seg = 4)
        {
            var key = new Vector4(size.x, size.y, size.z, r);
            if (roundBoxes.TryGetValue(key, out var cached)) return cached;
            Vector3 half = size * 0.5f;
            r = Mathf.Min(r, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.99f);
            Vector3 core = half - Vector3.one * r;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            int n = seg * 2 + 2;
            // Six faces of a cube made of a grid each, every grid point pushed onto the rounded box.
            Vector3[] faceN = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var fn in faceN)
            {
                Vector3 u = Mathf.Abs(fn.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(fn, u);
                int start = verts.Count;
                for (int j = 0; j <= n; j++)
                    for (int i = 0; i <= n; i++)
                    {
                        Vector3 p = fn + u * (i / (float)n * 2f - 1f) + v * (j / (float)n * 2f - 1f);   // on the unit cube
                        Vector3 q = Vector3.Scale(p, half);
                        Vector3 inner = new Vector3(Mathf.Clamp(q.x, -core.x, core.x), Mathf.Clamp(q.y, -core.y, core.y), Mathf.Clamp(q.z, -core.z, core.z));
                        Vector3 nrm = (q - inner).sqrMagnitude > 1e-8f ? (q - inner).normalized : fn;
                        verts.Add(inner + nrm * r);
                        normals.Add(nrm);
                    }
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        int a = start + j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                        tris.AddRange(new[] { a, c, b, b, c, d });
                    }
            }
            var m = new Mesh { name = "Skill round box" };
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetTriangles(tris, 0);
            // The winding of each face's grid depends on its axes: make every triangle face out.
            var tri = m.triangles;
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector3 a = verts[tri[i]], b = verts[tri[i + 1]], c = verts[tri[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normals[tri[i]] + normals[tri[i + 1]] + normals[tri[i + 2]]) < 0f)
                    (tri[i + 1], tri[i + 2]) = (tri[i + 2], tri[i + 1]);
            }
            m.triangles = tri;
            m.RecalculateBounds();
            roundBoxes[key] = m;
            return m;
        }

        /// <summary>A ring (torus) of radius 1 round local Y, tube <paramref name="tube"/>.</summary>
        static Mesh Torus(float tube, int around = 40, int sides = 10)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= around; i++)
            {
                float a = i / (float)around * Mathf.PI * 2f;
                Vector3 c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j <= sides; j++)
                {
                    float b = j / (float)sides * Mathf.PI * 2f;
                    Vector3 nrm = c * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    verts.Add(c + nrm * tube);
                    normals.Add(nrm);
                }
            }
            for (int i = 0; i < around; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = i * (sides + 1) + j, b = a + sides + 1;
                    tris.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            var m = new Mesh { name = "Skill torus" };
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        // ---------------------------------------------------------------- materials

        Shader shTile, shInk, shLine, shBand, shToon;
        Material matTile, matInk, matLine, matBand, matToon;
        // The king's guard drawn round a body (R93): an emerald outline with an ink edge outside it.
        Material matWardLine, matWardInk;
        readonly Dictionary<Color, Material> flatMats = new Dictionary<Color, Material>();

        static Shader Find(string name, string resource)
        {
            var s = Shader.Find(name);
            if (s == null)
            {
                var r = Resources.Load<Shader>(resource);
                if (r != null) s = r;
            }
            if (s == null) Debug.LogError($"[QotH 이펙트] 셰이더 {name} 을 찾지 못함");
            return s;
        }

        void BuildKit()
        {
            shTile = Find("ChessFight/Skill Tile", "QueenHillSkillFx/SkillTile");
            shInk = Find("ChessFight/Skill Ink", "QueenHillSkillFx/SkillInk");
            shLine = Find("ChessFight/Skill Ink Line", "QueenHillSkillFx/SkillInkLine");
            shBand = Find("ChessFight/Skill Band", "QueenHillSkillFx/SkillBand");
            shToon = Find("ChessFight/Skill Toon", "PawnRushSkillFx/SkillToon");
            matTile = new Material(shTile) { hideFlags = HideFlags.HideAndDontSave };
            matInk = new Material(shInk) { hideFlags = HideFlags.HideAndDontSave };
            matLine = new Material(shLine) { hideFlags = HideFlags.HideAndDontSave };
            matBand = new Material(shBand) { hideFlags = HideFlags.HideAndDontSave };
            matToon = new Material(shToon) { hideFlags = HideFlags.HideAndDontSave };
            matToon.SetVector("_Bands", new Vector4(0.42f, 0.74f, 0.08f, 0f));
            matToon.SetFloat("_RimPower", 2.4f);
            matToon.SetColor("_Fire", Color.black);
            matToon.SetFloat("_Heat", 0f);
            matWardLine = new Material(shLine) { hideFlags = HideFlags.HideAndDontSave };
            matWardLine.SetColor("_InkColor", King.main);
            matWardLine.SetFloat("_Width", 0.024f);
            matWardInk = new Material(shLine) { hideFlags = HideFlags.HideAndDontSave };
            matWardInk.SetColor("_InkColor", King.ink);
            matWardInk.SetFloat("_Width", 0.038f);
            meshQuad = Quad();
            meshPuff = Icosphere(3);
            meshBall = Icosphere(1);
            meshTorus = Torus(0.09f);
        }

        void DestroyKit()
        {
            foreach (var m in new Object[] { matTile, matInk, matLine, matBand, matToon, matWardLine, matWardInk, meshQuad, meshPuff, meshTorus, meshBall })
                if (m != null) Destroy(m);
            foreach (var m in roundBoxes.Values) if (m != null) Destroy(m);
            foreach (var m in flatMats.Values) if (m != null) Destroy(m);
        }

        /// <summary>A plain unlit colour for the body flash.</summary>
        Material Flat(Color c)
        {
            c.a = 1f;
            if (flatMats.TryGetValue(c, out var m) && m != null) return m;
            var shader = Shader.Find("Unlit/Color");
            m = new Material(shader) { color = c, hideFlags = HideFlags.HideAndDontSave };
            flatMats[c] = m;
            return m;
        }

        // ---------------------------------------------------------------- parts

        /// <summary>A flat inked shape on a quad (Skill Tile).</summary>
        public class Tile
        {
            public readonly Transform t;
            readonly MeshRenderer r;
            readonly MaterialPropertyBlock b = new MaterialPropertyBlock();
            public Color fill = new Color(1f, 1f, 1f, 0.35f), core, ink = Color.black, rim, stripe;
            public float inkWidth = 0.05f, rimWidth = 0.08f, coreWidth, round = 0.06f, shape, inner = 0.8f, gap, arc = 1f, flash, fade = 1f, stripePhase;
            public Vector2 size = Vector2.one;

            public Tile(QueenHillSkillFx fx, string name)
            {
                var go = new GameObject(name);
                go.transform.SetParent(fx.root, false);
                go.AddComponent<MeshFilter>().sharedMesh = fx.meshQuad;
                r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = fx.matTile;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;   // nothing until it is placed and painted (Apply)
                t = go.transform;
            }

            /// <summary>Lying on the floor at <paramref name="at"/>, its length along <paramref name="along"/>.</summary>
            public void Floor(Vector3 at, Vector3 along, Vector2 metres)
            {
                along.y = 0f;
                if (along.sqrMagnitude < 1e-6f) along = Vector3.forward;
                t.SetPositionAndRotation(at, Quaternion.LookRotation(Vector3.up, along.normalized));
                Size(metres);
            }

            /// <summary>Standing up, facing along <paramref name="normal"/> (an impact ring facing the hit).</summary>
            public void Facing(Vector3 at, Vector3 normal, Vector2 metres)
            {
                if (normal.sqrMagnitude < 1e-6f) normal = Vector3.up;
                Vector3 up = Mathf.Abs(Vector3.Dot(normal.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
                t.SetPositionAndRotation(at, Quaternion.LookRotation(normal.normalized, up));
                Size(metres);
            }

            public void Size(Vector2 metres)
            {
                size = new Vector2(Mathf.Max(metres.x, 1e-3f), Mathf.Max(metres.y, 1e-3f));
                t.localScale = new Vector3(size.x, size.y, 1f);
            }

            public void Apply()
            {
                b.SetColor("_Fill", fill);
                b.SetColor("_Core", core);
                b.SetFloat("_CoreWidth", coreWidth);
                b.SetColor("_InkColor", ink);
                b.SetFloat("_Ink", inkWidth);
                b.SetColor("_Rim", rim);
                b.SetFloat("_RimWidth", rimWidth);
                b.SetColor("_Stripe", stripe);
                b.SetFloat("_StripePhase", stripePhase);
                b.SetVector("_Size", new Vector4(size.x, size.y, 0f, 0f));
                b.SetFloat("_Round", round);
                b.SetFloat("_Shape", shape);
                b.SetFloat("_Inner", inner);
                b.SetFloat("_Gap", gap);
                b.SetFloat("_Arc", arc);
                b.SetFloat("_Flash", flash);
                b.SetFloat("_Fade", Mathf.Clamp01(fade));
                r.SetPropertyBlock(b);
                r.enabled = fade > 0.001f;
            }

            public void Destroy()
            {
                if (t != null) Object.Destroy(t.gameObject);
            }
        }

        /// <summary>A cartoon puff or drop (Skill Toon): hard colour bands, an ink edge, eaten away as it goes.</summary>
        public class Puff
        {
            public readonly Transform t;
            readonly MeshRenderer r;
            readonly MaterialPropertyBlock b = new MaterialPropertyBlock();
            public Palette colors;
            public float ink = 0.3f, lump = 0.3f, scale = 2.2f, dissolve;
            public Vector4 seed;

            public Puff(QueenHillSkillFx fx, Palette colors, Mesh mesh = null)
            {
                var go = new GameObject("Puff");
                go.transform.SetParent(fx.root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh != null ? mesh : fx.meshPuff;
                r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = fx.matToon;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.enabled = false;   // nothing until it is placed and painted (Apply)
                t = go.transform;
                this.colors = colors;
                seed = new Vector4(Random.value * 50f, Random.value * 50f, Random.value * 50f, 0f);
            }

            public void Apply(float age)
            {
                b.SetColor("_Lit", colors.light);
                b.SetColor("_Mid", colors.main);
                b.SetColor("_Shade", colors.deep);
                b.SetColor("_InkColor", colors.ink);
                b.SetColor("_Bite", colors.ink);
                b.SetFloat("_Ink", ink);
                b.SetFloat("_Lump", lump);
                b.SetFloat("_Scale", scale);
                b.SetFloat("_Dissolve", Mathf.Clamp01(dissolve));
                b.SetVector("_Seed", new Vector4(seed.x, seed.y, seed.z, age));
                r.SetPropertyBlock(b);
                r.enabled = dissolve < 0.999f;
            }

            public void Destroy()
            {
                if (t != null) Object.Destroy(t.gameObject);
            }
        }

        /// <summary>An inked ribbon along points (Skill Band): facing the camera round its length, or lying flat.</summary>
        public class Strip
        {
            readonly Mesh mesh;
            readonly MeshRenderer r;
            readonly MaterialPropertyBlock b = new MaterialPropertyBlock();
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> tris = new List<int>();
            readonly GameObject go;
            public Palette colors;
            public float head = 1f, tail, taper, fade = 1f, coreShare = 0.3f, inkShare = 0.22f, opacity = 1f;
            public Color core = Color.white;
            /// <summary>Toward its head the ribbon can turn into a second set of colours (R93: the castling arch's two
            /// halves meet in a gradient): from <see cref="blendFrom"/> to <see cref="blendTo"/> along it (0 tail .. 1
            /// head), up to <see cref="blendAmount"/> of <see cref="blendColors"/> (0 = none).</summary>
            public Palette blendColors;
            public Color blendCore = Color.white;
            public float blendFrom = 0.5f, blendTo = 1f, blendAmount;

            public Strip(QueenHillSkillFx fx, Palette colors)
            {
                go = new GameObject("Strip");
                go.transform.SetParent(fx.root, false);
                mesh = new Mesh { name = "Skill strip" };
                mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = fx.matBand;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                this.colors = colors;
                core = colors.light;
            }

            /// <summary>Lay the ribbon along <paramref name="pts"/> (tail first), <paramref name="width"/> across: facing
            /// <paramref name="eye"/>, or flat on the floor if <paramref name="flatUp"/>.</summary>
            public void Build(IList<Vector3> pts, Func<int, float> width, Vector3 eye, bool flatUp = false)
            {
                verts.Clear();
                uvs.Clear();
                tris.Clear();
                int n = pts.Count;
                if (n < 2) { mesh.Clear(); return; }
                for (int i = 0; i < n; i++)
                {
                    Vector3 tangent = pts[Mathf.Min(n - 1, i + 1)] - pts[Mathf.Max(0, i - 1)];
                    if (tangent.sqrMagnitude < 1e-8f) tangent = Vector3.forward;
                    Vector3 side = flatUp ? Vector3.Cross(Vector3.up, tangent) : Vector3.Cross(tangent, eye - pts[i]);
                    if (side.sqrMagnitude < 1e-8f) side = Vector3.Cross(Vector3.up, tangent);
                    if (side.sqrMagnitude < 1e-8f) side = Vector3.right;
                    side = side.normalized * (width(i) * 0.5f);
                    verts.Add(pts[i] - side);
                    verts.Add(pts[i] + side);
                    float u = i / (float)(n - 1);
                    uvs.Add(new Vector2(u, 0f));
                    uvs.Add(new Vector2(u, 1f));
                    if (i < n - 1)
                    {
                        int a = i * 2;
                        tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                    }
                }
                mesh.Clear();
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
            }

            public void Apply()
            {
                b.SetColor("_Core", core);
                b.SetColor("_Mid", colors.main);
                b.SetColor("_InkColor", colors.ink);
                b.SetFloat("_CoreShare", coreShare);
                b.SetFloat("_InkShare", inkShare);
                b.SetFloat("_Head", head);
                b.SetFloat("_Tail", tail);
                b.SetFloat("_Taper", taper);
                b.SetFloat("_Opacity", opacity);
                b.SetFloat("_Fade", Mathf.Clamp01(fade));
                bool blend = blendAmount > 0f;
                b.SetColor("_Core2", blend ? blendCore : core);
                b.SetColor("_Mid2", blend ? blendColors.main : colors.main);
                b.SetColor("_Ink2", blend ? blendColors.ink : colors.ink);
                b.SetVector("_Blend", new Vector4(blendFrom, Mathf.Max(blendFrom + 1e-3f, blendTo), blend ? Mathf.Clamp01(blendAmount) : 0f, 0f));
                r.SetPropertyBlock(b);
                r.enabled = fade > 0.001f && head > tail;
            }

            public void Destroy()
            {
                if (go != null) Object.Destroy(go);
                if (mesh != null) Object.Destroy(mesh);
            }
        }

        /// <summary>A chunky painted prop with an ink rim (Skill Ink + Skill Ink Line).</summary>
        public class Prop
        {
            public readonly Transform t;
            readonly MeshRenderer fill, line;
            readonly MaterialPropertyBlock bf = new MaterialPropertyBlock(), bl = new MaterialPropertyBlock();
            public Palette colors;
            public float inkWidth = 0.018f, flash;

            public Prop(QueenHillSkillFx fx, Mesh mesh, Palette colors, string name, Transform parent = null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent != null ? parent : fx.root, false);
                t = go.transform;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                fill = go.AddComponent<MeshRenderer>();
                fill.sharedMaterial = fx.matInk;
                fill.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var lineGo = new GameObject("Ink");
                lineGo.transform.SetParent(go.transform, false);
                lineGo.AddComponent<MeshFilter>().sharedMesh = mesh;
                line = lineGo.AddComponent<MeshRenderer>();
                line.sharedMaterial = fx.matLine;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                this.colors = colors;
                Apply();
            }

            public void Apply()
            {
                bf.SetColor("_Lit", colors.light);
                bf.SetColor("_Mid", colors.main);
                bf.SetColor("_Shade", colors.deep);
                bf.SetFloat("_Flash", flash);
                fill.SetPropertyBlock(bf);
                bl.SetColor("_InkColor", colors.ink);
                bl.SetFloat("_Width", inkWidth);
                line.SetPropertyBlock(bl);
            }

            public void Show(bool on)
            {
                fill.enabled = line.enabled = on;
            }

            public void Destroy()
            {
                if (t != null) Object.Destroy(t.gameObject);
            }
        }

        // ---------------------------------------------------------------- timed effects

        /// <summary>One effect: its parts, its age and what it does each frame (false = done).</summary>
        class Fx
        {
            public float age, life = 1f;
            public int touched = -1;
            public bool dead;
            public Func<Fx, float, bool> step;
            public Action end;
            public readonly List<Tile> tiles = new List<Tile>();
            public readonly List<Puff> puffs = new List<Puff>();
            public readonly List<Strip> strips = new List<Strip>();
            public readonly List<Prop> props = new List<Prop>();

            public void Destroy()
            {
                dead = true;
                end?.Invoke();
                foreach (var x in tiles) x.Destroy();
                foreach (var x in puffs) x.Destroy();
                foreach (var x in strips) x.Destroy();
                foreach (var x in props) x.Destroy();
            }
        }

        readonly List<Fx> live = new List<Fx>();
        readonly Dictionary<(RagdollPawn, string), Fx> kept = new Dictionary<(RagdollPawn, string), Fx>();
        int frame;

        Fx Run(float life, Func<Fx, float, bool> step)
        {
            var f = new Fx { life = life, step = step };
            live.Add(f);
            return f;
        }

        /// <summary>An effect that lasts while its owner's state keeps asking for it this frame (Held), then lets go.</summary>
        Fx Keep(RagdollPawn owner, string key, Func<Fx> make)
        {
            var k = (owner, key);
            if (!kept.TryGetValue(k, out var f) || f.dead)
            {
                f = make();
                f.life = float.MaxValue;
                kept[k] = f;
                live.Add(f);
            }
            f.touched = frame;
            return f;
        }

        bool Held(Fx f) => f.touched == frame;

        void StepEffects(float dt)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var f = live[i];
                f.age += dt;
                bool alive = !f.dead && f.age <= f.life && f.step(f, dt);
                if (alive) continue;
                f.Destroy();
                live.RemoveAt(i);
            }
            var gone = new List<(RagdollPawn, string)>();
            foreach (var kv in kept) if (kv.Value.dead) gone.Add(kv.Key);
            foreach (var k in gone) kept.Remove(k);
        }

        // ---------------------------------------------------------------- small helpers

        /// <summary>One frame at 60 per second, in effect time.</summary>
        const float F = 1f / 60f;

        static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        /// <summary>A pop: 0 → 1.1 at f3 → 1.0 at f6 (A: everything lands with a small overshoot).</summary>
        static float Pop(float age, float over = 1.1f)
        {
            float t = age / F;
            if (t < 3f) return Mathf.Lerp(0f, over, t / 3f);
            if (t < 6f) return Mathf.Lerp(over, 1f, (t - 3f) / 3f);
            return 1f;
        }

        static Color A(Color c, float a)
        {
            c.a = a;
            return c;
        }

        static Vector3 FlatDir(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : fallback;
        }

        /// <summary>The floor under a point (not a piece), or the point itself if there is none.</summary>
        static Vector3 FloorUnder(Vector3 p, float above = 1.2f)
        {
            var hits = Physics.RaycastAll(p + Vector3.up * above, Vector3.down, above + 6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 found = p;
            foreach (var h in hits)
            {
                if (RagdollPawn.ColliderOwner.ContainsKey(h.collider)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                if (h.normal.y < 0.5f || h.distance >= best) continue;
                best = h.distance;
                found = h.point;
            }
            return found;
        }
    }
}
