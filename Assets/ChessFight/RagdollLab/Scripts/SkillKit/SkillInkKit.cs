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
    /// Design A's parts ("잉크 테두리 장난감 체스", picked 2026-10-08 for Pawn Rush, Queen of the Hill and Sword Fight), for any
    /// mode's skill effects (R90; the same parts as the Queen of the Hill effects' own kit, QueenHillSkillFx.Kit.cs, R89,
    /// made standalone so the Pawn Rush effects draw with them too):
    ///
    /// flat inked shapes on a quad (board squares, rings, a horseshoe, arcs: <c>ChessFight/Skill Tile</c>), cartoon puffs and
    /// drops (<c>ChessFight/Skill Toon</c>), inked ribbons (<c>ChessFight/Skill Band</c>) and chunky props with an ink rim
    /// (<c>ChessFight/Skill Ink</c> + <c>Skill Ink Line</c>); the colours, read for the viewer (mine, my side's, the other
    /// side's); a runner for timed effects; and the language every skill shares: warning squares that fill as a windup
    /// runs, snap full in its last six frames, flash white for two and are gone in six; the hit flash (white for two
    /// frames, then the hitter's colour for two); an impact ring and a C half ring; round drops flying off one way (never a
    /// star); cream dust; the captured square stamped under a fallen piece; three little cream pawns circling a dazed head;
    /// squashes of the skin. Everything is a thick painted shape with a dark ink edge in its own darkest colour, no glow.
    /// Every shape is made in code; no node graphs (built-in render pipeline).
    /// </summary>
    public class SkillInkKit
    {
        // ---------------------------------------------------------------- colours (design A, viewer-relative sides)

        public static Color Hex(string hex, float a = 1f)
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

        public static readonly Palette King = new Palette("#34B85A", "#E2FFE6", "#1A7A36", "#0A3318");
        public static readonly Palette Queen = new Palette("#FFD34D", "#FFF0A8", "#E89B1A", "#3A2208");
        public static readonly Palette Rook = new Palette("#FF8A1F", "#FFF3C4", "#C2410C", "#3A1606");
        public static readonly Palette Bishop = new Palette("#994DFF", "#E6D6FF", "#6A2FC9", "#2A0F57");
        public static readonly Palette Knight = new Palette("#6BB3FF", "#DBF2FF", "#1F5CF2", "#0A144D");
        public static readonly Palette TeamAlly = new Palette("#2EC4E6", "#FFF1D2", "#1593B0", "#0B4F5C");
        public static readonly Palette TeamEnemy = new Palette("#FF3B4E", "#FFE1E4", "#C81E33", "#5A0F1A");
        public static readonly Palette Dust = new Palette("#D9C3A0", "#FFF1D2", "#8F7A63", "#2B1A0E");
        public static readonly Palette Grey = new Palette("#9AA3B5", "#DDE2EC", "#6B7385", "#2D3344");
        public static readonly Palette Stone = new Palette("#B7AE9F", "#E9E4DA", "#6F6658", "#2B2620");
        public static readonly Color SelfFill = Hex("#FFF4DC"), SelfInk = Hex("#1B2340");

        public enum Side { Self, Ally, Enemy }

        /// <summary>Whose it is, as the viewer sees it: mine, my side's or the other side's.</summary>
        public Side SideOf(RagdollPawn pawn)
        {
            var v = viewer != null ? viewer() : null;
            if (pawn == null || v == null) return Side.Enemy;
            if (pawn == v) return Side.Self;
            return v.Team != Teams.None && pawn.Team == v.Team ? Side.Ally : Side.Enemy;
        }

        /// <summary>The colours of the side a piece is on as the viewer sees it: teal for mine and my side's, red for the
        /// other side's (the pawn's own colours: it has none of its own).</summary>
        public Palette SideColors(RagdollPawn pawn) => SideOf(pawn) == Side.Enemy ? TeamEnemy : TeamAlly;

        /// <summary>A piece's own colours; the pawn wears its side's.</summary>
        public Palette PieceColors(RagdollPawn pawn)
        {
            if (pawn == null) return Grey;
            switch (pawn.Piece)
            {
                case PieceKind.King: return King;
                case PieceKind.Queen: return Queen;
                case PieceKind.Rook: return Rook;
                case PieceKind.Bishop: return Bishop;
                case PieceKind.Knight: return Knight;
                default: return SideColors(pawn);
            }
        }

        public static Color A(Color c, float a)
        {
            c.a = a;
            return c;
        }

        // ---------------------------------------------------------------- the kit

        public readonly Transform root;
        readonly Func<RagdollPawn> viewer;
        readonly Func<Vector3> eye;
        /// <summary>The little pawn the daze draws (a chess pawn mesh, height 1, centred).</summary>
        public Mesh pawnMesh;
        public float Clock { get; private set; }
        public Vector3 Eye => eye != null ? eye() : Vector3.up * 5f;

        public SkillInkKit(Transform parent, Func<RagdollPawn> viewer, Func<Vector3> eye)
        {
            root = new GameObject("Design A parts").transform;
            if (parent != null) root.SetParent(parent, false);
            this.viewer = viewer;
            this.eye = eye;
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
            meshQuad = Quad();
            meshPuff = Icosphere(3);
            meshBall = Icosphere(1);
            meshTorus = Torus(0.09f);
        }

        public void Destroy()
        {
            foreach (var f in live) f.Destroy();
            live.Clear();
            foreach (var b in flashes.Values) b.Restore();
            flashes.Clear();
            foreach (var s in squashes.Values) s.Restore();
            squashes.Clear();
            foreach (var m in new Object[] { matTile, matInk, matLine, matBand, matToon, meshQuad, meshPuff, meshTorus, meshBall })
                if (m != null) Object.Destroy(m);
            foreach (var m in roundBoxes.Values) if (m != null) Object.Destroy(m);
            foreach (var m in flatMats.Values) if (m != null) Object.Destroy(m);
            if (root != null) Object.Destroy(root.gameObject);
        }

        /// <summary>One frame of the effects (<paramref name="dt"/> in effect time): every timed effect, the body flashes
        /// and, after the skeleton has been posed, the squashes.</summary>
        public void Step(float dt)
        {
            Clock += dt;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var f = live[i];
                f.age += dt;
                bool alive = !f.dead && f.age <= f.life && f.step(f, dt);
                if (alive) continue;
                f.Destroy();
                live.RemoveAt(i);
            }
            var gone = new List<(object, string)>();
            foreach (var kv in kept) if (kv.Value.dead) gone.Add(kv.Key);
            foreach (var k in gone) kept.Remove(k);
            StepFlashes(dt);
            ApplySquashes();
            // Only now: what was kept this frame (Keep, before Step) is Held while the effects step.
            frame++;
        }

        // ---------------------------------------------------------------- meshes

        public Mesh meshQuad, meshPuff, meshTorus, meshBall;
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
        public Mesh RoundBox(Vector3 size, float r, int seg = 4)
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

        readonly Shader shTile, shInk, shLine, shBand, shToon;
        public readonly Material matTile, matInk, matLine, matBand, matToon;
        readonly Dictionary<Color, Material> flatMats = new Dictionary<Color, Material>();

        static Shader Find(string name, string resource)
        {
            var s = Shader.Find(name);
            if (s == null)
            {
                var r = Resources.Load<Shader>(resource);
                if (r != null) s = r;
            }
            if (s == null) Debug.LogError($"[스킬 이펙트 A] 셰이더 {name} 을 찾지 못함");
            return s;
        }

        /// <summary>A plain unlit colour for the body flash.</summary>
        Material Flat(Color c)
        {
            c.a = 1f;
            if (flatMats.TryGetValue(c, out var m) && m != null) return m;
            // R111: Unlit/Color is not in a player build (only the editor finds it); Sprites/Default always is.
            var shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            m = new Material(shader) { color = c, hideFlags = HideFlags.HideAndDontSave };
            flatMats[c] = m;
            return m;
        }

        // ---------------------------------------------------------------- parts

        /// <summary>A flat inked shape on a quad (Skill Tile): a rounded box, a ring (with a gap, or only part of it shown)
        /// or a disc.</summary>
        public class Tile
        {
            public readonly Transform t;
            readonly MeshRenderer r;
            readonly MaterialPropertyBlock b = new MaterialPropertyBlock();
            public Color fill = new Color(1f, 1f, 1f, 0.35f), core, ink = Color.black, rim, stripe;
            public float inkWidth = 0.05f, rimWidth = 0.08f, coreWidth, round = 0.06f, shape, inner = 0.8f, gap, arc = 1f, flash, fade = 1f, stripePhase;
            public Vector2 size = Vector2.one;

            public Tile(SkillInkKit kit, string name)
            {
                var go = new GameObject(name);
                go.transform.SetParent(kit.root, false);
                go.AddComponent<MeshFilter>().sharedMesh = kit.meshQuad;
                r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = kit.matTile;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;   // nothing until it is placed and painted (Apply)
                t = go.transform;
            }

            /// <summary>Lying on the floor at <paramref name="at"/>, its length (local +y) along <paramref name="along"/>.</summary>
            public void Floor(Vector3 at, Vector3 along, Vector2 metres)
            {
                along.y = 0f;
                if (along.sqrMagnitude < 1e-6f) along = Vector3.forward;
                t.SetPositionAndRotation(at, Quaternion.LookRotation(Vector3.up, along.normalized));
                Size(metres);
            }

            /// <summary>Standing up, facing along <paramref name="normal"/> (an impact ring facing the hit, a ring on a wall).</summary>
            public void Facing(Vector3 at, Vector3 normal, Vector2 metres, Vector3 up = default)
            {
                if (normal.sqrMagnitude < 1e-6f) normal = Vector3.up;
                if (up.sqrMagnitude < 1e-6f) up = Mathf.Abs(Vector3.Dot(normal.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
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

            public void Hide() => r.enabled = false;

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

            public Puff(SkillInkKit kit, Palette colors, Mesh mesh = null)
            {
                var go = new GameObject("Puff");
                go.transform.SetParent(kit.root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh != null ? mesh : kit.meshPuff;
                r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = kit.matToon;
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

            public Strip(SkillInkKit kit, Palette colors)
            {
                go = new GameObject("Strip");
                go.transform.SetParent(kit.root, false);
                mesh = new Mesh { name = "Skill strip" };
                mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = kit.matBand;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.enabled = false;
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
                r.SetPropertyBlock(b);
                r.enabled = fade > 0.001f && head > tail;
            }

            public void Hide() => r.enabled = false;

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

            public Prop(SkillInkKit kit, Mesh mesh, Palette colors, string name, Transform parent = null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent != null ? parent : kit.root, false);
                t = go.transform;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                fill = go.AddComponent<MeshRenderer>();
                fill.sharedMaterial = kit.matInk;
                fill.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var lineGo = new GameObject("Ink");
                lineGo.transform.SetParent(go.transform, false);
                lineGo.AddComponent<MeshFilter>().sharedMesh = mesh;
                line = lineGo.AddComponent<MeshRenderer>();
                line.sharedMaterial = kit.matLine;
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
        public class Fx
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
                if (dead && tiles.Count == 0 && puffs.Count == 0 && strips.Count == 0 && props.Count == 0) return;
                dead = true;
                end?.Invoke();
                end = null;
                foreach (var x in tiles) x.Destroy();
                foreach (var x in puffs) x.Destroy();
                foreach (var x in strips) x.Destroy();
                foreach (var x in props) x.Destroy();
                tiles.Clear();
                puffs.Clear();
                strips.Clear();
                props.Clear();
            }
        }

        readonly List<Fx> live = new List<Fx>();
        readonly Dictionary<(object, string), Fx> kept = new Dictionary<(object, string), Fx>();
        int frame;

        /// <summary>An effect that runs for <paramref name="life"/> seconds (or until <paramref name="step"/> says no).</summary>
        public Fx Run(float life, Func<Fx, float, bool> step)
        {
            var f = new Fx { life = life, step = step };
            live.Add(f);
            return f;
        }

        /// <summary>An effect that lasts while its owner keeps asking for it each frame (<see cref="Held"/>), then lets go.</summary>
        public Fx Keep(object owner, string key, Func<Fx> make)
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

        public bool Held(Fx f) => f.touched == frame;

        // ---------------------------------------------------------------- small helpers

        /// <summary>One frame at 60 per second, in effect time.</summary>
        public const float F = 1f / 60f;

        public static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        public static float EaseIn(float t) => Mathf.Pow(Mathf.Clamp01(t), 3f);

        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        /// <summary>A pop: 0 → <paramref name="over"/> at f3 → 1 at f6 (design A: everything lands with a small overshoot).</summary>
        public static float Pop(float age, float over = 1.1f, float from = 0f)
        {
            float t = age / F;
            if (t < 3f) return Mathf.Lerp(from, over, t / 3f);
            if (t < 6f) return Mathf.Lerp(over, 1f, (t - 3f) / 3f);
            return 1f;
        }

        public static Vector3 FlatDir(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : fallback;
        }

        /// <summary>The floor under a point (not a piece, not a moving body), or the point itself if there is none.</summary>
        public static Vector3 FloorUnder(Vector3 p, float above = 1.2f)
        {
            FloorUnder(p, above, out Vector3 found);
            return found;
        }

        /// <summary>The floor under a point (within 6 m), and whether there is any (else <paramref name="floor"/> is the point).</summary>
        public static bool FloorUnder(Vector3 p, float above, out Vector3 floor)
        {
            var hits = Physics.RaycastAll(p + Vector3.up * above, Vector3.down, above + 6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            floor = p;
            foreach (var h in hits)
            {
                if (RagdollPawn.ColliderOwner.ContainsKey(h.collider)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                if (h.normal.y < 0.5f || h.distance >= best) continue;
                best = h.distance;
                floor = h.point;
            }
            return best < float.MaxValue;
        }

        // ---------------------------------------------------------------- warning squares

        /// <summary>
        /// Paint a warning shape: its fill (the piece's own colour, or the side's for a pawn), the viewer's side rim, sliding
        /// stripes if it is the other side's, an ink edge. <paramref name="strength"/> 0..1 as the windup fills it;
        /// <paramref name="flash"/> 1 turns it white (the two frames it turns into the move).
        /// </summary>
        public void Warn(Tile t, RagdollPawn caster, float strength, float flash = 0f, Palette? fillOverride = null)
        {
            var side = SideOf(caster);
            Color sideFill = side == Side.Self ? SelfFill : side == Side.Ally ? TeamAlly.main : TeamEnemy.main;
            Color sideInk = side == Side.Self ? SelfInk : side == Side.Ally ? TeamAlly.ink : TeamEnemy.ink;
            float sideAlpha = side == Side.Self ? 0.35f : side == Side.Ally ? 0.3f : 0.4f;
            bool pawn = caster != null && caster.Piece == PieceKind.Pawn && fillOverride == null;
            if (pawn)
            {
                t.fill = A(sideFill, sideAlpha * Mathf.Lerp(0.35f, 1f, strength));
                t.ink = A(sideInk, Mathf.Lerp(0.5f, 1f, strength));
                t.rim = Color.clear;
            }
            else
            {
                var p = fillOverride ?? PieceColors(caster);
                t.fill = A(p.main, Mathf.Lerp(0.14f, 0.42f, strength));
                t.ink = A(p.ink, Mathf.Lerp(0.5f, 1f, strength));
                t.rim = A(sideFill, Mathf.Lerp(0.45f, 0.95f, strength));
            }
            t.inkWidth = 0.05f;
            t.rimWidth = 0.09f;
            t.stripe = side == Side.Enemy ? A(TeamEnemy.deep, 0.55f * strength) : Color.clear;
            t.stripePhase = Clock * 0.5f;
            t.flash = flash;
            t.Apply();
        }

        /// <summary>Squares of the board along a flat line from <paramref name="from"/>, each on the floor under it.</summary>
        public static List<Vector3> SquaresAlong(Vector3 from, Vector3 dir, float length, float square)
        {
            var list = new List<Vector3>();
            int n = Mathf.Max(1, Mathf.CeilToInt(length / square - 0.15f));
            for (int i = 0; i < n; i++)
            {
                Vector3 c = from + dir * (square * (i + 0.5f));
                list.Add(FloorUnder(c, 1.5f) + Vector3.up * 0.016f);
            }
            return list;
        }

        /// <summary>A windup's strength: rises with the windup, full in its last six frames (A).</summary>
        public static float Charge(float time, float total)
        {
            if (total <= 0f) return 1f;
            if (time >= total - 6f * F) return 1f;
            return Mathf.Lerp(0.3f, 0.85f, Mathf.Clamp01(time / Mathf.Max(F, total - 6f * F)));
        }

        /// <summary>Squares that flash white for two frames and are gone in six (the moment a warning turns into the move).</summary>
        public void Release(List<Vector3> squares, Vector3 along, RagdollPawn caster, float square, Palette? fill = null)
        {
            var f = Run(6f * F, (fx, d) =>
            {
                float a = fx.age / F;
                foreach (var t in fx.tiles)
                {
                    t.fade = 1f - Mathf.Clamp01((a - 2f) / 4f);
                    Warn(t, caster, 1f, a < 2f ? 1f : 0f, fill);
                }
                return true;
            });
            foreach (var c in squares)
            {
                var t = new Tile(this, "Warning release");
                t.Floor(c, along, Vector2.one * square);
                f.tiles.Add(t);
            }
        }

        // ---------------------------------------------------------------- the hit kit

        /// <summary>The hit body: white for two frames, then <paramref name="color"/> for two.</summary>
        public void FlashBody(RagdollPawn pawn, Color color)
        {
            if (pawn == null) return;
            if (!flashes.TryGetValue(pawn, out var b))
            {
                b = new BodyFlash();
                foreach (var r in pawn.GetComponentsInChildren<Renderer>())
                    if ((r is SkinnedMeshRenderer || r is MeshRenderer) && r.enabled)
                    {
                        b.renderers.Add(r);
                        b.materials.Add(r.sharedMaterials);
                    }
                flashes[pawn] = b;
            }
            b.age = 0f;
            b.phase = 0;
            b.color = color;
            b.Paint(Flat(Color.white));
        }

        class BodyFlash
        {
            public readonly List<Renderer> renderers = new List<Renderer>();
            public readonly List<Material[]> materials = new List<Material[]>();
            public float age;
            public Color color;
            public int phase;

            public void Paint(Material m)
            {
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = m;
                    r.sharedMaterials = mats;
                }
            }

            public void Restore()
            {
                for (int i = 0; i < renderers.Count; i++)
                    if (renderers[i] != null) renderers[i].sharedMaterials = materials[i];
            }
        }

        readonly Dictionary<RagdollPawn, BodyFlash> flashes = new Dictionary<RagdollPawn, BodyFlash>();

        void StepFlashes(float dt)
        {
            List<RagdollPawn> done = null;
            foreach (var kv in flashes)
            {
                var b = kv.Value;
                b.age += dt;
                if (kv.Key == null || b.age >= 4f * F)
                {
                    b.Restore();
                    (done ??= new List<RagdollPawn>()).Add(kv.Key);
                    continue;
                }
                int phase = b.age < 2f * F ? 0 : 1;
                if (phase != b.phase)
                {
                    b.phase = phase;
                    b.Paint(phase == 0 ? Flat(Color.white) : Flat(b.color));
                }
            }
            if (done != null) foreach (var p in done) flashes.Remove(p);
        }

        /// <summary>A ring standing up at the hit, facing along it: white core, the hitter's colour, ink; 110% at f3, 100% at
        /// f6, then eaten round. Turned half toward the camera so a hit seen from the side is not a ring edge-on.</summary>
        public void ImpactRing(Vector3 at, Vector3 normal, Palette p, float r0, float r1, bool towardEye = true)
        {
            Vector3 toEye = Eye - at;
            if (towardEye && normal.sqrMagnitude > 1e-4f && toEye.sqrMagnitude > 1e-4f)
            {
                normal = normal.normalized;
                if (Vector3.Dot(normal, toEye) < 0f) normal = -normal;
                normal = (normal + toEye.normalized).normalized;
            }
            var t = new Tile(this, "Impact ring");
            var f = Run(14f * F, (fx, d) =>
            {
                float a = fx.age / F;
                float k = a < 3f ? Mathf.Lerp(0f, 1.1f, a / 3f) : a < 6f ? Mathf.Lerp(1.1f, 1f, (a - 3f) / 3f) : 1f;
                float r = Mathf.Lerp(r0, r1, Mathf.Min(1f, k));
                if (k > 1f) r = r1 * k;
                t.Facing(at, normal, Vector2.one * (2f * r));
                t.shape = 1f;
                t.inner = 0.62f;
                t.fill = A(p.main, 1f);
                t.core = A(Color.white, 1f);
                t.coreWidth = 0.035f;
                t.ink = A(p.ink, 1f);
                t.inkWidth = 0.03f;
                t.rim = Color.clear;
                t.stripe = Color.clear;
                t.arc = a < 6f ? 1f : 1f - (a - 6f) / 8f;   // eaten round from its start
                t.fade = 1f;
                t.Apply();
                return true;
            });
            f.tiles.Add(t);
        }

        /// <summary>
        /// A C half ring flung the way a piece flies (design A's shove: a 140° sector of a flat ring, white core, the
        /// hitter's colour, ink): radius 0.5 → 1.1 at f3 → 1 at f6 of <paramref name="radius"/>, then eaten away from
        /// its tail over eight frames. <paramref name="dir"/> is the way it opens (the push); <paramref name="tilt"/> tips
        /// it down (degrees).
        /// </summary>
        public void HalfRing(Vector3 at, Vector3 dir, Palette p, float radius, float band = 0.18f, float tilt = 0f)
        {
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            Vector3 flat = FlatDir(dir, Vector3.forward);
            // The ring stands across the push (the quad faces it) and only 140° of it shows, arching over the top. The
            // shader shows the part clockwise from local +y, so local +y is turned 70° back from straight up: the shown
            // part is then centred on up.
            Vector3 n = Quaternion.AngleAxis(tilt, Vector3.Cross(Vector3.up, flat)) * flat;
            Vector3 u = Vector3.ProjectOnPlane(Vector3.up, n);
            if (u.sqrMagnitude < 1e-4f) u = Vector3.ProjectOnPlane(Vector3.forward, n);
            u.Normalize();
            Vector3 side = Vector3.Cross(u, n);
            const float half = 70f * Mathf.Deg2Rad;
            Vector3 quadUp = Mathf.Cos(half) * u - Mathf.Sin(half) * side;
            var t = new Tile(this, "Shove ring");
            var f = Run(14f * F, (fx, d) =>
            {
                float a = fx.age / F;
                float k = Pop(fx.age, 1.1f, 0.5f);
                float r = radius * k;
                t.Facing(at + dir * (0.15f + 0.2f * EaseOut(a / 6f)), n, Vector2.one * (2f * r), quadUp);
                t.shape = 1f;
                t.inner = Mathf.Clamp01(1f - band / Mathf.Max(0.05f, r));
                t.fill = A(p.main, 1f);
                t.core = A(Color.white, 1f);
                t.coreWidth = Mathf.Min(0.04f, band * 0.25f);
                t.ink = A(p.ink, 1f);
                t.inkWidth = 0.035f;
                t.rim = Color.clear;
                t.stripe = Color.clear;
                float eaten = a < 6f ? 0f : Mathf.Clamp01((a - 6f) / 8f);
                t.arc = 140f / 360f * (1f - eaten);
                t.fade = 1f;
                t.Apply();
                return true;
            });
            f.tiles.Add(t);
        }

        /// <summary>3-5 round drops flying off one way, at uneven angles (A: never a star).</summary>
        public void Drops(Vector3 at, Vector3 dir, Palette p, int n, float speed = 7f)
        {
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.up;
            float[] spread = { -12f, 4f, 17f, -24f, 28f };
            for (int i = 0; i < n; i++)
            {
                var drop = new Puff(this, new Palette { main = p.main, light = Color.white, deep = p.deep, ink = p.ink }, meshBall) { ink = 0.35f, lump = 0f };
                Vector3 axis = Vector3.Cross(dir, Vector3.up);
                if (axis.sqrMagnitude < 1e-4f) axis = Vector3.right;
                Vector3 v = Quaternion.AngleAxis(spread[i % spread.Length], Vector3.up) * Quaternion.AngleAxis(Random.Range(-10f, 10f), axis) * dir;
                float sp = speed * Random.Range(0.8f, 1.2f), len = Random.Range(0.26f, 0.42f);
                Vector3 pos = at;
                var f = Run(0.16f + i * 0.015f, (fx, d) =>
                {
                    pos += v * sp * d;
                    float k = 1f - fx.age / fx.life;
                    drop.t.SetPositionAndRotation(pos, Quaternion.LookRotation(v));
                    drop.t.localScale = new Vector3(0.1f * k + 0.02f, 0.1f * k + 0.02f, len * k + 0.05f);
                    drop.Apply(fx.age);
                    return true;
                });
                f.puffs.Add(drop);
            }
        }

        /// <summary>Cream dust puffs (or another palette's) rolling out on the floor at uneven angles and eaten from below.</summary>
        public void DustRing(Vector3 at, int n, float radius, Palette? colors = null, float size = 0.3f, float life = 0.5f, float roll = 10f)
        {
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < n; i++)
            {
                float ang = (start + i * 360f / n + Random.Range(-25f, 25f)) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var puff = new Puff(this, colors ?? Dust) { ink = 0.32f };
                float r = size * Random.Range(0.8f, 1.2f), far = radius * Random.Range(0.75f, 1.1f);
                var f = Run(life, (fx, d) =>
                {
                    float k = fx.age / fx.life;
                    float go = EaseOut(Mathf.Min(1f, fx.age / (roll * F)));
                    puff.t.position = at + dir * (far * go) + Vector3.up * (r * 0.6f + 0.15f * k);
                    puff.t.localScale = Vector3.one * (r * (0.5f + 0.5f * Pop(fx.age, 1.15f)) * (1f + 0.3f * k));
                    puff.dissolve = Mathf.Clamp01((k - 0.35f) / 0.65f);
                    puff.Apply(fx.age);
                    return true;
                });
                f.puffs.Add(puff);
            }
        }

        /// <summary>Puffs in a palette bursting from a point (an impact's dust, a pop).</summary>
        public void PuffBurst(Vector3 at, Palette colors, int n, float radius, float size, float life = 0.45f, float up = 0.3f)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.6f;
                var puff = new Puff(this, colors) { ink = 0.32f };
                float r = size * Random.Range(0.75f, 1.2f);
                var f = Run(life, (fx, d) =>
                {
                    float k = fx.age / fx.life;
                    puff.t.position = at + dir * (radius * EaseOut(k * 2f)) + Vector3.up * (up * k);
                    puff.t.localScale = Vector3.one * (r * Pop(fx.age, 1.2f));
                    puff.dissolve = Mathf.Clamp01((k - 0.3f) / 0.7f);
                    puff.Apply(fx.age);
                    return true;
                });
                f.puffs.Add(puff);
            }
        }

        /// <summary>A ring of white flashing for two frames on the floor and fading in six (a take-off).</summary>
        public void FlashRing(Vector3 at, float radius, Palette p)
        {
            var ring = new Tile(this, "Flash ring");
            var f = Run(8f * F, (fx, d) =>
            {
                ring.Floor(at, Vector3.forward, Vector2.one * (2f * radius) * (1f + fx.age / fx.life * 0.4f));
                ring.shape = 1f;
                ring.inner = 0.72f;
                ring.fill = A(Color.white, 1f);
                ring.ink = A(p.ink, 1f);
                ring.inkWidth = 0.025f;
                ring.core = Color.clear;
                ring.rim = Color.clear;
                ring.stripe = Color.clear;
                ring.fade = fx.age < 2f * F ? 1f : 1f - (fx.age - 2f * F) / (6f * F);
                ring.Apply();
                return true;
            });
            f.tiles.Add(ring);
        }

        /// <summary>
        /// The knockdown kit, once the piece lies: the captured square in the hitter's colour under it (80%, a thick ink
        /// edge, scale 1.1 → 1 in six frames, held 30, faded in 15), an ink ring opening on the floor, four cream dust puffs
        /// and a squash. Nothing if it did not go down after all (a guard, a stagger).
        /// </summary>
        public void Down(RagdollPawn target, Palette p, Vector3 fallback, float square = 1.5f)
        {
            if (target == null) return;
            bool stamped = false;
            var tile = new Tile(this, "Captured square");
            var ring = new Tile(this, "Floor ink ring");
            Vector3 at = Vector3.zero;
            float since = 0f;
            var f = Run(3f, (fx, d) =>
            {
                if (!stamped)
                {
                    if (target == null) return false;
                    if (fx.age > 0.2f && target.State != PawnState.Ragdoll) return false;   // it did not go down
                    // Stamped once it has come down: settled, or soon anyway (it may lie crumpled, not flat).
                    bool lying = fx.age > 0.5f || (fx.age > 0.15f && target.Hips.linearVelocity.magnitude < 2.5f);
                    if (!lying) return true;
                    // Thrown off the floor altogether (off a platform's edge): no square in the air.
                    if (!FloorUnder(target.Hips.position, 0.6f, out Vector3 under)) return false;
                    stamped = true;
                    since = fx.age;
                    at = under + Vector3.up * 0.014f;
                    DustRing(at, 4, 0.8f);
                    AddSquash(target, SquashKind.Down);
                }
                float a = (fx.age - since) / F;
                tile.Floor(at, Vector3.forward, Vector2.one * (square * (a < 6f ? Mathf.Lerp(1.1f, 1f, a / 6f) : 1f)));
                tile.shape = 0f;
                tile.fill = A(p.main, 0.8f);
                tile.ink = A(p.ink, 1f);
                tile.inkWidth = 0.08f;
                tile.rim = Color.clear;
                tile.stripe = Color.clear;
                tile.round = 0.06f;
                tile.fade = a < 36f ? 1f : 1f - (a - 36f) / 15f;
                tile.Apply();
                float rr = Mathf.Lerp(0.3f, 0.9f, EaseOut(a / 10f));
                ring.Floor(at + Vector3.up * 0.003f, Vector3.forward, Vector2.one * (2f * rr));
                ring.shape = 1f;
                ring.inner = Mathf.Max(0.01f, 1f - 0.06f / rr);
                ring.fill = A(Dust.ink, 0.6f);
                ring.ink = Color.clear;
                ring.core = Color.clear;
                ring.rim = Color.clear;
                ring.stripe = Color.clear;
                ring.fade = 1f - Mathf.Clamp01((a - 10f) / 12f);
                ring.Apply();
                return a < 51f;
            });
            f.tiles.Add(tile);
            f.tiles.Add(ring);
        }

        /// <summary>A dazed piece: three little cream pawns circling over its head (one turn per 0.6 s), then hopping down
        /// into it (A: never stars).</summary>
        public void Daze(RagdollPawn pawn, float delay, float life, float pawnHeight = 0.16f, float radius = 0.26f)
        {
            if (pawn == null || pawnMesh == null) return;
            var f = Run(delay + life + 0.2f, (fx, d) =>
            {
                if (pawn == null) return false;
                Vector3 head = pawn.bodies[(int)BodyId.Head].position;
                for (int i = 0; i < fx.props.Count; i++)
                {
                    var p = fx.props[i];
                    float age = fx.age - delay;
                    if (age < 0f) { p.Show(false); continue; }
                    p.Show(true);
                    float a = age / 0.6f * Mathf.PI * 2f + i * Mathf.PI * 2f / 3f;
                    float hop = age > life ? Mathf.Clamp01((age - life) / 0.2f) : 0f;
                    Vector3 orbit = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * (1f - hop);
                    p.t.position = head + Vector3.up * (0.3f * (1f - hop) + 0.12f * Mathf.Sin(hop * Mathf.PI)) + orbit;
                    p.t.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 8f * Mathf.Sin(age * 14f + i));
                    p.t.localScale = Vector3.one * pawnHeight * Pop(age, 1.15f) * (1f - hop * 0.8f);
                }
                return true;
            });
            for (int i = 0; i < 3; i++)
            {
                var prop = new Prop(this, pawnMesh, Dust, "Daze pawn") { inkWidth = 0.012f };
                prop.colors = new Palette { main = Dust.light, light = Color.white, deep = Dust.main, ink = Dust.ink };
                prop.Apply();
                prop.Show(false);
                f.props.Add(prop);
            }
        }

        /// <summary>Paint a horseshoe: a thick ring open to local -x (white core, the colour, ink).</summary>
        public static void Horseshoe(Tile t, Palette p, float alpha, float gap = 110f)
        {
            t.shape = 1f;
            t.inner = 0.66f;
            t.gap = gap;
            t.arc = 1f;
            t.fill = A(p.main, alpha);
            t.core = A(Color.white, alpha > 0.9f ? 1f : alpha);
            t.coreWidth = 0.04f;
            t.ink = A(p.ink, 1f);
            t.inkWidth = 0.03f;
            t.rim = Color.clear;
            t.stripe = Color.clear;
        }

        // ---------------------------------------------------------------- squashes (the skin's root bone, after the pose)

        public enum SquashKind { Down, Flatten, Bump, Wide, Spring }

        class Squash
        {
            public RagdollPawn pawn;
            public Transform bone;
            public Vector3 baseScale, lastOffset, lastSet;
            public int axis;
            public readonly List<(SquashKind kind, float born, float hold)> marks = new List<(SquashKind, float, float)>();
            public float coil, coilSetAt = -1f;

            public void Restore()
            {
                if (bone == null) return;
                if (bone.position == lastSet) bone.position -= lastOffset;
                bone.localScale = baseScale;
            }
        }

        readonly Dictionary<RagdollPawn, Squash> squashes = new Dictionary<RagdollPawn, Squash>();

        Squash SquashOf(RagdollPawn pawn)
        {
            if (pawn == null || pawn.skin == null) return null;
            if (squashes.TryGetValue(pawn, out var s)) return s;
            Transform bone = pawn.skin.rootBone != null ? pawn.skin.rootBone : pawn.skin.transform;
            int axis = 1;
            float best = -1f;
            for (int a = 0; a < 3; a++)
            {
                Vector3 dir = a == 0 ? bone.right : a == 1 ? bone.up : bone.forward;
                float dd = Mathf.Abs(Vector3.Dot(dir, Vector3.up));
                if (dd > best) { best = dd; axis = a; }
            }
            s = new Squash { pawn = pawn, bone = bone, baseScale = bone.localScale, axis = axis };
            squashes[pawn] = s;
            return s;
        }

        /// <summary>A squash of the skin: Down (a body hitting the floor), Flatten (stomped flat, held <paramref name="hold"/>,
        /// back with a boing), Bump (a short squat), Wide (squashed wide against a wall), Spring (out of a coil).</summary>
        public void AddSquash(RagdollPawn pawn, SquashKind kind, float hold = 0f)
        {
            var s = SquashOf(pawn);
            if (s != null) s.marks.Add((kind, Clock, hold));
        }

        /// <summary>A held coil this frame (a windup crouch: 0 none .. 1 full, here 10% lower and a little wider).</summary>
        public void Coil(RagdollPawn pawn, float amount)
        {
            var s = SquashOf(pawn);
            if (s == null) return;
            s.coil = Mathf.Clamp01(amount);
            s.coilSetAt = Clock;
        }

        /// <summary>Height of one squash at this age: 1 = none, below 0 = over.</summary>
        static float SquashHeight(SquashKind kind, float age, float hold)
        {
            float a = age / F;
            switch (kind)
            {
                case SquashKind.Down:   // 6 frames down to 0.8, back with a 1.05 overshoot
                    if (a < 3f) return Mathf.Lerp(1f, 0.8f, a / 3f);
                    if (a < 7f) return Mathf.Lerp(0.8f, 1.05f, (a - 3f) / 4f);
                    if (a < 11f) return Mathf.Lerp(1.05f, 1f, (a - 7f) / 4f);
                    return -1f;
                case SquashKind.Spring:   // out of a coil: up past full height and back (a queen springing open)
                    if (a < 3f) return Mathf.Lerp(0.9f, 1.07f, a / 3f);
                    if (a < 8f) return Mathf.Lerp(1.07f, 1f, (a - 3f) / 5f);
                    return -1f;
                case SquashKind.Bump:
                    if (a < 2f) return Mathf.Lerp(1f, 0.9f, a / 2f);
                    if (a < 6f) return Mathf.Lerp(0.9f, 1f, (a - 2f) / 4f);
                    return -1f;
                case SquashKind.Wide:   // 1.2x wide for four frames, then back
                    if (a < 2f) return Mathf.Lerp(1f, 0.7f, a / 2f);
                    if (a < 4f) return 0.7f;
                    if (a < 9f) return Mathf.Lerp(0.7f, 1f, (a - 4f) / 5f);
                    return -1f;
                case SquashKind.Flatten:   // flat in 4 frames, held, then a springy way back (1.15 overshoot)
                    if (a < 4f) return Mathf.Lerp(1f, 0.6f, a / 4f);
                    if (age < hold) return 0.6f;
                    float t = age - hold;
                    if (t > 0.35f) return -1f;
                    return 1f + (0.6f - 1f) * Mathf.Exp(-t * 9f) * Mathf.Cos(t * 18f) + 0.15f * Mathf.Sin(Mathf.Clamp01(t / 0.2f) * Mathf.PI) * (1f - t / 0.35f);
            }
            return -1f;
        }

        void ApplySquashes()
        {
            List<RagdollPawn> done = null;
            foreach (var kv in squashes)
            {
                var s = kv.Value;
                if (s.bone == null || kv.Key == null) { (done ??= new List<RagdollPawn>()).Add(kv.Key); continue; }
                if (s.bone.position == s.lastSet) s.bone.position -= s.lastOffset;   // nothing re-posed it since last frame
                float sy = 1f;
                for (int i = s.marks.Count - 1; i >= 0; i--)
                {
                    var m = s.marks[i];
                    float h = SquashHeight(m.kind, Clock - m.born, m.hold);
                    if (h < 0f) { s.marks.RemoveAt(i); continue; }
                    sy = Mathf.Min(sy, h) + Mathf.Max(0f, h - 1f);
                }
                bool coiling = s.coilSetAt >= 0f && Clock - s.coilSetAt < 0.1f && s.coil > 0.01f;
                if (coiling) sy = Mathf.Min(sy, 1f - 0.1f * s.coil);
                if (s.marks.Count == 0 && !coiling)
                {
                    s.bone.localScale = s.baseScale;
                    s.lastOffset = Vector3.zero;
                    (done ??= new List<RagdollPawn>()).Add(kv.Key);
                    continue;
                }
                float sx = 1f + (1f - Mathf.Min(sy, 1f)) * 0.6f;
                var sc = s.baseScale;
                for (int a = 0; a < 3; a++) sc[a] *= a == s.axis ? sy : sx;
                s.bone.localScale = sc;
                float above = Mathf.Max(0f, s.bone.position.y - (s.pawn.Hips.position.y - s.pawn.standHeight));
                s.lastOffset = Vector3.down * above * (1f - sy);
                s.bone.position += s.lastOffset;
                s.lastSet = s.bone.position;
            }
            if (done != null)
                foreach (var p in done)
                {
                    if (p != null && squashes.TryGetValue(p, out var s) && s.bone != null) s.bone.localScale = s.baseScale;
                    squashes.Remove(p);
                }
        }
    }
}
