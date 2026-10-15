using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R130: solid little models made in code for the skill effects, smooth-shaded so a glossy material (Skill Gloss)
    /// gives them real depth: the Staunton chess pieces (the Sword Fight head icons, 승규 님: "입체감 있는 3D 아이콘으로 다시
    /// 만들어줘"; the Queen of the Hill rook's castling ghosts), the knight's horse head as a rounded cut-out, the Queen of
    /// the Hill king's sceptre (a turned chess-piece rod and a king's crown with its cross), a horseshoe (R133: a real shoe's
    /// shape with its groove and nail holes), two arrows chasing
    /// round a circle (a swap). Every piece is 1 tall with its foot at y = 0 unless said otherwise.
    /// </summary>
    public static class SkillMeshes
    {
        static Vector2 V(float x, float y) => new Vector2(x, y);

        /// <summary>Parts put together, then smooth normals (a profile point given twice makes a sharp edge).</summary>
        public class Builder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<int> tri = new List<int>();

            /// <summary>A profile (radius, height; bottom to top) turned about the vertical, <paramref name="y0"/> added.</summary>
            public void Lathe(Vector2[] profile, int segments = 28, float y0 = 0f, float scale = 1f)
            {
                int start = v.Count, ring = segments + 1;
                foreach (var p in profile)
                    for (int k = 0; k <= segments; k++)
                    {
                        float a = k * Mathf.PI * 2f / segments;
                        v.Add(new Vector3(Mathf.Cos(a) * p.x * scale, y0 + p.y * scale, Mathf.Sin(a) * p.x * scale));
                    }
                for (int i = 0; i < profile.Length - 1; i++)
                    for (int k = 0; k < segments; k++)
                    {
                        int a = start + i * ring + k, c = a + ring;
                        tri.AddRange(new[] { a, c, a + 1, a + 1, c, c + 1 });
                    }
            }

            public void Sphere(Vector3 center, float radius, int segments = 16)
            {
                int start = v.Count, rows = segments / 2, ring = segments + 1;
                for (int r = 0; r <= rows; r++)
                {
                    float phi = Mathf.PI * r / rows;
                    for (int k = 0; k <= segments; k++)
                    {
                        float a = k * Mathf.PI * 2f / segments;
                        v.Add(center + new Vector3(Mathf.Sin(phi) * Mathf.Cos(a), -Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(a)) * radius);
                    }
                }
                for (int r = 0; r < rows; r++)
                    for (int k = 0; k < segments; k++)
                    {
                        int a = start + r * ring + k, c = a + ring;
                        tri.AddRange(new[] { a, c, a + 1, a + 1, c, c + 1 });
                    }
            }

            /// <summary>A box with rounded edges (a grid on each face pushed onto the rounded box), any orientation.</summary>
            public void RoundBox(Vector3 center, Vector3 size, float r, Quaternion rot = default, int seg = 3)
            {
                if (rot == default) rot = Quaternion.identity;
                Vector3 half = size * 0.5f;
                r = Mathf.Min(r, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.99f);
                Vector3 core = half - Vector3.one * r;
                int n = seg * 2 + 2;
                Vector3[] faceN = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                foreach (var fn in faceN)
                {
                    Vector3 u = Mathf.Abs(fn.y) > 0.5f ? Vector3.right : Vector3.up;
                    Vector3 w = Vector3.Cross(fn, u);
                    int start = v.Count;
                    for (int j = 0; j <= n; j++)
                        for (int i = 0; i <= n; i++)
                        {
                            Vector3 p = fn + u * (i / (float)n * 2f - 1f) + w * (j / (float)n * 2f - 1f);
                            Vector3 q = Vector3.Scale(p, half);
                            Vector3 inner = new Vector3(Mathf.Clamp(q.x, -core.x, core.x), Mathf.Clamp(q.y, -core.y, core.y), Mathf.Clamp(q.z, -core.z, core.z));
                            Vector3 nrm = (q - inner).sqrMagnitude > 1e-8f ? (q - inner).normalized : fn;
                            v.Add(center + rot * (inner + nrm * r));
                        }
                    for (int j = 0; j < n; j++)
                        for (int i = 0; i < n; i++)
                        {
                            int a = start + j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                            // Wound to face out (checked against the face's normal).
                            Vector3 pa = v[a], pb = v[b], pc = v[c];
                            bool outward = Vector3.Dot(Vector3.Cross(pb - pa, pc - pa), rot * fn) > 0f;
                            if (outward) tri.AddRange(new[] { a, b, c, b, d, c });
                            else tri.AddRange(new[] { a, c, b, b, c, d });
                        }
                }
            }

            /// <summary>A thick ring piece between two angles (degrees) round the vertical: a battlement.</summary>
            public void RingPiece(float inner, float outer, float y0, float y1, float from, float to, int steps = 6)
            {
                Vector3 P(float r, float deg, float y) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad) * r, y, Mathf.Sin(deg * Mathf.Deg2Rad) * r);
                for (int i = 0; i < steps; i++)
                {
                    float a = Mathf.Lerp(from, to, i / (float)steps), c = Mathf.Lerp(from, to, (i + 1) / (float)steps);
                    Quad(P(outer, a, y0), P(outer, a, y1), P(outer, c, y1), P(outer, c, y0));
                    Quad(P(inner, c, y0), P(inner, c, y1), P(inner, a, y1), P(inner, a, y0));
                    Quad(P(inner, a, y1), P(inner, c, y1), P(outer, c, y1), P(outer, a, y1));
                }
                Quad(P(inner, from, y0), P(inner, from, y1), P(outer, from, y1), P(outer, from, y0));
                Quad(P(outer, to, y0), P(outer, to, y1), P(inner, to, y1), P(inner, to, y0));
            }

            /// <summary>
            /// A side view (x forward, y up, counter-clockwise) made <paramref name="depth"/> thick (along z) with rounded edges
            /// of <paramref name="bevel"/>: rings of the outline stepped in toward each face, then a flat face.
            /// </summary>
            public void RoundedExtrude(Vector2[] outline, float depth, float bevel, int bevelSteps = 4)
            {
                int n = outline.Length;
                // Each point's outward miter (unit normal of its two edges, lengthened at corners, capped).
                var miter = new Vector2[n];
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = outline[(i + n - 1) % n], b = outline[i], c = outline[(i + 1) % n];
                    Vector2 e0 = (b - a).normalized, e1 = (c - b).normalized;
                    Vector2 n0 = new Vector2(e0.y, -e0.x), n1 = new Vector2(e1.y, -e1.x);   // outward (CCW)
                    Vector2 m = (n0 + n1);
                    m = m.sqrMagnitude < 1e-6f ? n0 : m.normalized;
                    float cos = Mathf.Max(0.35f, Vector2.Dot(m, n0));
                    miter[i] = m / cos;
                }
                float half = depth * 0.5f;
                bevel = Mathf.Min(bevel, half * 0.95f);
                // The layers from the back face round the side to the front face.
                var layers = new List<(float z, float inset)>();
                for (int s = 0; s <= bevelSteps; s++)
                {
                    float t = s / (float)bevelSteps * Mathf.PI * 0.5f;
                    layers.Add((-(half - bevel) - bevel * Mathf.Cos(t), bevel * (1f - Mathf.Sin(t))));
                }
                for (int s = 0; s <= bevelSteps; s++)
                {
                    float t = s / (float)bevelSteps * Mathf.PI * 0.5f;
                    layers.Add(((half - bevel) + bevel * Mathf.Sin(t), bevel * (1f - Mathf.Cos(t))));
                }
                int start = v.Count;
                foreach (var (z, inset) in layers)
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 p = outline[i] - miter[i] * inset;
                        v.Add(new Vector3(p.x, p.y, z));
                    }
                for (int l = 0; l < layers.Count - 1; l++)
                    for (int i = 0; i < n; i++)
                    {
                        int a = start + l * n + i, b = start + l * n + (i + 1) % n, c = a + n, d = b + n;
                        tri.AddRange(new[] { a, b, d, a, d, c });
                    }
                // The faces: the first and the last layer, triangulated (they share the side's vertices: smooth into the bevel).
                var caps = Triangulate(InsetOutline(outline, miter, layers[0].inset));
                int back = start, front = start + (layers.Count - 1) * n;
                for (int i = 0; i < caps.Count; i += 3)
                {
                    tri.AddRange(new[] { back + caps[i], back + caps[i + 2], back + caps[i + 1] });
                    tri.AddRange(new[] { front + caps[i], front + caps[i + 1], front + caps[i + 2] });
                }
            }

            static Vector2[] InsetOutline(Vector2[] outline, Vector2[] miter, float inset)
            {
                var o = new Vector2[outline.Length];
                for (int i = 0; i < o.Length; i++) o[i] = outline[i] - miter[i] * inset;
                return o;
            }

            /// <summary>A tube of round section along a path (closed or not), radius per point.</summary>
            public void Tube(IList<Vector3> path, IList<float> radius, int sides = 10, bool capEnds = true)
            {
                int start = v.Count, ring = sides + 1;
                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 t = (path[Mathf.Min(path.Count - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                    Vector3 side = Vector3.Cross(t, Vector3.up);
                    if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(t, Vector3.right);
                    side.Normalize();
                    Vector3 up = Vector3.Cross(side, t);
                    for (int k = 0; k <= sides; k++)
                    {
                        float a = k * Mathf.PI * 2f / sides;
                        v.Add(path[i] + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius[i]);
                    }
                }
                for (int i = 0; i < path.Count - 1; i++)
                    for (int k = 0; k < sides; k++)
                    {
                        int a = start + i * ring + k, c = a + ring;
                        tri.AddRange(new[] { a, c, a + 1, a + 1, c, c + 1 });
                    }
                if (!capEnds) return;
                Sphere(path[0], radius[0], 10);
                Sphere(path[path.Count - 1], radius[path.Count - 1], 10);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int s = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                tri.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }

            /// <summary>Ear clipping of a simple counter-clockwise polygon.</summary>
            public static List<int> Triangulate(Vector2[] poly)
            {
                var idx = new List<int>();
                for (int i = 0; i < poly.Length; i++) idx.Add(i);
                var result = new List<int>();
                int guard = 0;
                while (idx.Count > 3 && guard++ < 4000)
                {
                    bool clipped = false;
                    for (int i = 0; i < idx.Count; i++)
                    {
                        int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                        Vector2 a = poly[ia], b = poly[ib], c = poly[ic];
                        if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 0f) continue;
                        bool inside = false;
                        foreach (int j in idx)
                        {
                            if (j == ia || j == ib || j == ic) continue;
                            if (InTriangle(poly[j], a, b, c)) { inside = true; break; }
                        }
                        if (inside) continue;
                        result.AddRange(new[] { ia, ib, ic });
                        idx.RemoveAt(i);
                        clipped = true;
                        break;
                    }
                    if (!clipped) break;
                }
                if (idx.Count == 3) result.AddRange(new[] { idx[0], idx[1], idx[2] });
                return result;
            }

            static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
            {
                float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
                float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
                float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
                bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
                return !(neg && pos);
            }

            /// <summary>The mesh as built (smooth normals, positions kept).</summary>
            public Mesh Build(string name)
            {
                var m = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
                if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v);
                m.SetTriangles(tri, 0);
                m.RecalculateNormals();
                SmoothSeams(m);
                m.RecalculateBounds();
                return m;
            }

            /// <summary>Vertices at the same place with nearly the same normal (a lathe's seam) share one normal, so an
            /// ink hull drawn round the mesh has no crack.</summary>
            static void SmoothSeams(Mesh m)
            {
                var pos = m.vertices;
                var nrm = m.normals;
                var groups = new Dictionary<Vector3Int, List<int>>();
                for (int i = 0; i < pos.Length; i++)
                {
                    var key = new Vector3Int(Mathf.RoundToInt(pos[i].x * 2000f), Mathf.RoundToInt(pos[i].y * 2000f), Mathf.RoundToInt(pos[i].z * 2000f));
                    if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<int>();
                    list.Add(i);
                }
                foreach (var list in groups.Values)
                {
                    if (list.Count < 2) continue;
                    foreach (int i in list)
                    {
                        Vector3 sum = Vector3.zero;
                        foreach (int j in list)
                            if (Vector3.Dot(nrm[i], nrm[j]) > 0.6f) sum += nrm[j];
                        if (sum.sqrMagnitude > 1e-8f) nrm[i] = sum.normalized;
                    }
                }
                m.normals = nrm;
            }
        }

        // ---------------------------------------------------------------- the chess pieces (Staunton, 1 tall, foot at 0)

        static readonly Dictionary<PieceKind, Mesh> pieces = new Dictionary<PieceKind, Mesh>();

        /// <summary>A Staunton chess piece, 1 tall, its foot at y = 0, the knight facing +x.</summary>
        public static Mesh Piece(PieceKind kind)
        {
            if (pieces.TryGetValue(kind, out var cached) && cached != null) return cached;
            var b = new Builder();
            switch (kind)
            {
                case PieceKind.King:
                    b.Lathe(new[] { V(0f, 0f), V(.36f, 0f), V(.37f, .02f), V(.37f, .06f), V(.33f, .08f), V(.3f, .1f), V(.31f, .12f), V(.27f, .14f),
                                    V(.2f, .22f), V(.15f, .34f), V(.13f, .46f), V(.13f, .52f), V(.2f, .54f), V(.21f, .57f), V(.2f, .6f), V(.13f, .62f),
                                    V(.15f, .66f), V(.2f, .72f), V(.24f, .78f), V(.25f, .8f), V(.21f, .83f), V(.12f, .86f), V(0f, .87f) });
                    b.RoundBox(new Vector3(0f, .935f, 0f), new Vector3(.075f, .15f, .075f), .025f);
                    b.RoundBox(new Vector3(0f, .955f, 0f), new Vector3(.19f, .065f, .075f), .025f);
                    break;
                case PieceKind.Queen:
                    b.Lathe(new[] { V(0f, 0f), V(.36f, 0f), V(.37f, .02f), V(.37f, .06f), V(.33f, .08f), V(.3f, .1f), V(.31f, .12f), V(.27f, .14f),
                                    V(.2f, .22f), V(.14f, .36f), V(.12f, .5f), V(.12f, .55f), V(.2f, .57f), V(.21f, .6f), V(.2f, .63f), V(.12f, .65f),
                                    V(.14f, .7f), V(.21f, .78f), V(.27f, .86f), V(.27f, .88f), V(.2f, .89f), V(.11f, .9f), V(.05f, .92f), V(0f, .925f) });
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i * Mathf.PI * 2f / 9f;
                        b.Sphere(new Vector3(Mathf.Cos(a) * .25f, .885f, Mathf.Sin(a) * .25f), .042f, 10);
                    }
                    b.Sphere(new Vector3(0f, .955f, 0f), .055f, 14);
                    break;
                case PieceKind.Rook:
                    b.Lathe(new[] { V(0f, 0f), V(.38f, 0f), V(.39f, .02f), V(.39f, .07f), V(.34f, .09f), V(.31f, .11f), V(.32f, .13f), V(.28f, .16f),
                                    V(.26f, .25f), V(.25f, .6f), V(.27f, .64f), V(.33f, .7f), V(.34f, .72f), V(.34f, .8f), V(.24f, .8f), V(.24f, .76f), V(0f, .76f) });
                    for (int i = 0; i < 5; i++) b.RingPiece(.245f, .34f, .8f, .93f, i * 72f + 9f, i * 72f + 72f - 9f);
                    break;
                case PieceKind.Bishop:
                    b.Lathe(new[] { V(0f, 0f), V(.35f, 0f), V(.36f, .02f), V(.36f, .06f), V(.32f, .08f), V(.29f, .1f), V(.3f, .12f), V(.25f, .15f),
                                    V(.17f, .26f), V(.13f, .4f), V(.115f, .5f), V(.2f, .52f), V(.21f, .55f), V(.2f, .58f), V(.12f, .6f), V(.15f, .64f),
                                    V(.2f, .7f), V(.215f, .77f), V(.2f, .84f), V(.15f, .9f), V(.08f, .94f), V(0f, .95f) });
                    b.Sphere(new Vector3(0f, .985f, 0f), .045f, 12);
                    break;
                case PieceKind.Knight:
                    b.Lathe(new[] { V(0f, 0f), V(.36f, 0f), V(.37f, .02f), V(.37f, .06f), V(.32f, .08f), V(.29f, .1f), V(.3f, .12f), V(.25f, .15f),
                                    V(.22f, .2f), V(.22f, .24f), V(0f, .24f) });
                    b.RoundedExtrude(KnightOutline, .3f, .055f);
                    break;
                default:
                    b.Lathe(new[] { V(0f, 0f), V(.34f, 0f), V(.35f, .02f), V(.35f, .06f), V(.3f, .09f), V(.24f, .12f), V(.17f, .2f), V(.12f, .32f),
                                    V(.11f, .4f), V(.2f, .42f), V(.2f, .46f), V(.11f, .48f), V(0f, .48f) });
                    b.Sphere(new Vector3(0f, .6f, 0f), .16f, 16);
                    break;
            }
            var m = b.Build($"Chess piece ({kind})");
            Normalize(m);
            pieces[kind] = m;
            return m;
        }

        static readonly Dictionary<PieceKind, Mesh> icons = new Dictionary<PieceKind, Mesh>();

        /// <summary>
        /// A chess piece made to be read small, over a head (R132, 승규 님 10-13: "위에 표시되는 아이콘이 각 기물이랑 달라
        /// 킹이면 킹 아이콘 퀸이면 퀸 아이콘으로 나와야 돼" — at icon size the Staunton king's little cross read as the queen's
        /// knob and the queen's coronet as a rook's battlements): squat, a heavy base, and what tells each piece apart made
        /// big — the king's cross a third of his height and solid both ways (a cross from any side), the queen's crown seven
        /// tall spikes leaning out with a pearl on each and one on top, the rook's four battlements with deep gaps, the
        /// bishop's mitre and its pearl, the knight's horse head. 1 tall, its foot at y = 0, the knight facing +x.
        /// </summary>
        public static Mesh PieceIcon(PieceKind kind)
        {
            if (icons.TryGetValue(kind, out var cached) && cached != null) return cached;
            var b = new Builder();
            // The same heavy base under all of them.
            var foot = new[] { V(0f, 0f), V(.42f, 0f), V(.43f, .03f), V(.43f, .09f), V(.37f, .11f), V(.33f, .13f), V(.34f, .155f), V(.28f, .18f) };
            Vector2[] With(params Vector2[] rest)
            {
                var all = new Vector2[foot.Length + rest.Length];
                foot.CopyTo(all, 0);
                rest.CopyTo(all, foot.Length);
                return all;
            }
            switch (kind)
            {
                case PieceKind.King:
                    b.Lathe(With(V(.21f, .26f), V(.18f, .35f), V(.18f, .4f), V(.27f, .42f), V(.28f, .455f), V(.19f, .475f),
                                 V(.21f, .5f), V(.29f, .57f), V(.32f, .62f), V(.32f, .645f), V(.26f, .665f), V(.15f, .68f), V(0f, .69f)), 32);
                    b.RoundBox(new Vector3(0f, .835f, 0f), new Vector3(.12f, .32f, .12f), .035f);
                    b.RoundBox(new Vector3(0f, .88f, 0f), new Vector3(.36f, .11f, .11f), .035f);
                    b.RoundBox(new Vector3(0f, .88f, 0f), new Vector3(.11f, .11f, .36f), .035f);
                    break;
                case PieceKind.Queen:
                    b.Lathe(With(V(.2f, .27f), V(.16f, .37f), V(.16f, .42f), V(.25f, .44f), V(.26f, .47f), V(.17f, .49f),
                                 V(.19f, .52f), V(.27f, .6f), V(.3f, .64f), V(.22f, .66f), V(.12f, .7f), V(0f, .72f)), 32);
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i * Mathf.PI * 2f / 7f;
                        var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        Vector3 tip = outward * .37f + Vector3.up * .86f;
                        b.Tube(new[] { outward * .25f + Vector3.up * .6f, tip }, new[] { .07f, .02f }, 8, false);
                        b.Sphere(tip + outward * .01f + Vector3.up * .028f, .048f, 10);
                    }
                    b.Sphere(new Vector3(0f, .8f, 0f), .085f, 14);
                    break;
                case PieceKind.Rook:
                    b.Lathe(With(V(.3f, .26f), V(.29f, .58f), V(.32f, .62f), V(.38f, .66f), V(.39f, .69f), V(.39f, .75f), V(.27f, .75f), V(.27f, .72f), V(0f, .72f)), 32);
                    for (int i = 0; i < 4; i++) b.RingPiece(.25f, .39f, .75f, .96f, i * 90f + 18f, i * 90f + 72f, 6);
                    break;
                case PieceKind.Bishop:
                    // A tall mitre coming to a point (the slit across it is a dark disc the head mark adds: one colour here).
                    b.Lathe(With(V(.19f, .27f), V(.15f, .37f), V(.14f, .42f), V(.24f, .44f), V(.25f, .47f), V(.15f, .49f),
                                 V(.17f, .52f), V(.23f, .58f), V(.26f, .65f), V(.255f, .72f), V(.22f, .8f), V(.16f, .88f), V(.09f, .95f), V(.035f, .99f), V(0f, 1f)), 32);
                    b.Sphere(new Vector3(0f, 1.045f, 0f), .06f, 12);
                    break;
                case PieceKind.Knight:
                    b.Lathe(With(V(.25f, .21f), V(.24f, .24f), V(0f, .24f)), 32);
                    b.RoundedExtrude(KnightOutline, .34f, .06f);
                    break;
                default:
                    return Piece(kind);
            }
            var m = b.Build($"Chess piece icon ({kind})");
            Normalize(m);
            icons[kind] = m;
            return m;
        }

        /// <summary>The knight's horse head, side on, facing +x (counter-clockwise, its foot on the turned base at 0.22).</summary>
        static readonly Vector2[] KnightOutline =
        {
            V(-.2f, .22f), V(.2f, .22f), V(.23f, .3f), V(.2f, .38f), V(.17f, .44f), V(.24f, .49f), V(.34f, .52f), V(.42f, .55f),
            V(.46f, .6f), V(.46f, .66f), V(.42f, .71f), V(.33f, .75f), V(.25f, .81f), V(.2f, .87f), V(.19f, .93f), V(.15f, 1f),
            V(.1f, .95f), V(.05f, .93f), V(-.03f, .93f), V(-.12f, .88f), V(-.2f, .79f), V(-.26f, .66f), V(-.29f, .52f), V(-.28f, .38f), V(-.24f, .28f),
        };

        /// <summary>Scale a mesh to 1 tall with its foot at y = 0 (keeping it centred round the vertical).</summary>
        static void Normalize(Mesh m)
        {
            var vs = m.vertices;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var p in vs) { lo = Mathf.Min(lo, p.y); hi = Mathf.Max(hi, p.y); }
            float h = Mathf.Max(1e-3f, hi - lo);
            for (int i = 0; i < vs.Length; i++) vs[i] = new Vector3(vs[i].x / h, (vs[i].y - lo) / h, vs[i].z / h);
            m.vertices = vs;
            m.RecalculateBounds();
        }

        // ---------------------------------------------------------------- the Queen of the Hill king's sceptre

        static Mesh sceptreRod, sceptreCrown;

        /// <summary>The rod (R130, 승규 님: "막대는 일반 체스 계열"): turned like a chess piece's stem, a little finial at its
        /// foot, beads and collars along it, a cup at its top that the crown sits in. Local +y along it, from y = 0 to 0.74.</summary>
        public static Mesh SceptreRod()
        {
            if (sceptreRod != null) return sceptreRod;
            var b = new Builder();
            b.Lathe(new[] {
                V(0f, 0f), V(.022f, .004f), V(.034f, .02f), V(.03f, .036f), V(.018f, .046f), V(.018f, .06f), V(.03f, .068f), V(.03f, .08f),
                V(.02f, .088f), V(.02f, .2f), V(.028f, .21f), V(.028f, .23f), V(.02f, .24f), V(.019f, .42f), V(.026f, .43f), V(.033f, .44f),
                V(.026f, .45f), V(.019f, .46f), V(.019f, .62f), V(.028f, .632f), V(.028f, .648f), V(.022f, .66f), V(.03f, .68f), V(.05f, .7f),
                V(.056f, .715f), V(.05f, .73f), V(.035f, .74f), V(0f, .74f) }, 20);
            sceptreRod = b.Build("Sceptre rod");
            return sceptreRod;
        }

        /// <summary>The crown on top (R130, "끝 부분은 지금 왕 색이랑 같은데 모형이나 디자인 왕 스러운걸로"): a chess king's
        /// head — a flared crown with eight points round its rim and a jewel band, a dome, a cross on top. Its foot at y = 0,
        /// about 0.24 tall.</summary>
        public static Mesh SceptreCrown()
        {
            if (sceptreCrown != null) return sceptreCrown;
            var b = new Builder();
            b.Lathe(new[] { V(0f, 0f), V(.04f, 0f), V(.05f, .01f), V(.046f, .022f), V(.05f, .03f), V(.068f, .06f), V(.082f, .09f), V(.086f, .1f),
                            V(.07f, .104f), V(.062f, .11f), V(.05f, .124f), V(.03f, .134f), V(0f, .138f) }, 28);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                b.Sphere(new Vector3(Mathf.Cos(a) * .078f, .112f, Mathf.Sin(a) * .078f), .016f, 8);
            }
            b.RoundBox(new Vector3(0f, .175f, 0f), new Vector3(.03f, .09f, .03f), .012f);
            b.RoundBox(new Vector3(0f, .19f, 0f), new Vector3(.085f, .028f, .03f), .012f);
            sceptreCrown = b.Build("Sceptre crown");
            return sceptreCrown;
        }

        // ---------------------------------------------------------------- a horseshoe (the Queen of the Hill knight's mark)

        static Mesh horseshoe, horseshoeHoles;

        /// <summary>
        /// The shoe's middle line from the left heel round the toe to the right heel (x-z, the toe toward +z), how far along
        /// it each point is, and the way out of the shoe there. R133: a real front shoe's line, not a ring bent round 290°: the
        /// toe a half circle, the branches running back from it nearly straight and closing in a little at the heels.
        /// </summary>
        static void HorseshoeLine(List<Vector3> at, List<float> run, List<Vector3> outward)
        {
            const float R = 0.37f, cz = 0.05f;
            Vector3 b0 = new Vector3(R, 0f, cz), b1 = new Vector3(R, 0f, cz - 0.23f), b2 = new Vector3(0.345f, 0f, cz - 0.42f), b3 = new Vector3(0.31f, 0f, cz - 0.54f);
            Vector3 Branch(float t)
            {
                float u = 1f - t;
                return u * u * u * b0 + 3f * u * u * t * b1 + 3f * u * t * t * b2 + t * t * t * b3;
            }
            const int branch = 30, toe = 48;
            for (int i = branch; i > 0; i--)
            {
                Vector3 p = Branch(i / (float)branch);
                at.Add(new Vector3(-p.x, 0f, p.z));
            }
            for (int i = 0; i <= toe; i++)
            {
                float a = Mathf.PI * (1f - i / (float)toe);
                at.Add(new Vector3(R * Mathf.Cos(a), 0f, cz + R * Mathf.Sin(a)));
            }
            for (int i = 1; i <= branch; i++) at.Add(Branch(i / (float)branch));
            float sum = 0f;
            for (int i = 0; i < at.Count; i++)
            {
                if (i > 0) sum += Vector3.Distance(at[i - 1], at[i]);
                run.Add(sum);
                Vector3 tangent = (at[Mathf.Min(at.Count - 1, i + 1)] - at[Mathf.Max(0, i - 1)]).normalized;
                outward.Add(Vector3.Cross(tangent, Vector3.up));
            }
        }

        /// <summary>
        /// A horseshoe lying in the x-z plane, its toe toward +z and open toward -z, about 0.9 across and 1 long, its middle
        /// at the origin, the ground face up. R133 (승규 님 10-14: "나이트 말 발굽을 조금 더 사실적으로 다시 디자인 해줘"): a
        /// real shoe, not a bent ring with nail heads on it: a U whose web is wider at the toe (0.17) than at the heels
        /// (0.125), 0.07 thick, its edges rounded, square heel ends, and a groove (the fuller) running round its face just
        /// out from the middle, where the nail holes are (<see cref="HorseshoeHoles"/>, drawn dark).
        /// </summary>
        public static Mesh Horseshoe()
        {
            if (horseshoe != null) return horseshoe;
            var at = new List<Vector3>();
            var run = new List<float>();
            var outward = new List<Vector3>();
            HorseshoeLine(at, run, outward);
            float half = run[run.Count - 1] * 0.5f;
            int n = at.Count;
            // The band's section (out from the middle line, up), counter-clockwise: up the outer side, over the face and
            // its groove, down the inner side, back under. Every edge is its own strip, so the faces and the groove keep
            // their edges and the rounded corners (a few strips each) shade round.
            const float hh = 0.035f, bevel = 0.014f, k = 0.293f, gc = 0.024f, gTop = 0.017f, gBottom = 0.009f, gDepth = 0.022f;
            var sections = new Vector2[n][];
            for (int i = 0; i < n; i++)
            {
                float fromToe = Mathf.Abs(run[i] - half) / half;
                float w2 = Mathf.Lerp(0.17f, 0.125f, Mathf.Pow(fromToe, 1.5f)) * 0.5f;
                sections[i] = new[]
                {
                    V(w2, -hh + bevel), V(w2, hh - bevel), V(w2 - bevel * k, hh - bevel * k), V(w2 - bevel, hh),
                    V(gc + gTop, hh), V(gc + gBottom, hh - gDepth), V(gc - gBottom, hh - gDepth), V(gc - gTop, hh),
                    V(-w2 + bevel, hh), V(-w2 + bevel * k, hh - bevel * k), V(-w2, hh - bevel), V(-w2, -hh + bevel),
                    V(-w2 + bevel * k, -hh + bevel * k), V(-w2 + bevel, -hh), V(w2 - bevel, -hh), V(w2 - bevel * k, -hh + bevel * k),
                };
            }
            var b = new Builder();
            Vector3 Point(int i, Vector2 q) => at[i] + outward[i] * q.x + Vector3.up * q.y;
            void Face(int a, int c, int d, Vector3 toward)
            {
                Vector3 nrm = Vector3.Cross(b.v[c] - b.v[a], b.v[d] - b.v[a]);
                if (Vector3.Dot(nrm, toward) >= 0f) b.tri.AddRange(new[] { a, c, d });
                else b.tri.AddRange(new[] { a, d, c });
            }
            int edges = sections[0].Length;
            for (int e = 0; e < edges; e++)
            {
                int start = b.v.Count;
                for (int i = 0; i < n; i++)
                {
                    b.v.Add(Point(i, sections[i][e]));
                    b.v.Add(Point(i, sections[i][(e + 1) % edges]));
                }
                for (int i = 0; i < n - 1; i++)
                {
                    Vector2 dir = sections[i][(e + 1) % edges] - sections[i][e];
                    Vector2 out2 = new Vector2(dir.y, -dir.x);
                    Vector3 toward = outward[i] * out2.x + Vector3.up * out2.y;
                    int a0 = start + i * 2, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                    Face(a0, b0, b1, toward);
                    Face(a0, b1, a1, toward);
                }
            }
            // The heel ends, cut square (their own corners: flat).
            for (int end = 0; end < 2; end++)
            {
                int i = end == 0 ? 0 : n - 1;
                Vector3 toward = end == 0 ? at[0] - at[1] : at[n - 1] - at[n - 2];
                int start = b.v.Count;
                foreach (var q in sections[i]) b.v.Add(Point(i, q));
                var tris = Builder.Triangulate(sections[i]);
                for (int t = 0; t < tris.Count; t += 3) Face(start + tris[t], start + tris[t + 1], start + tris[t + 2], toward);
            }
            horseshoe = b.Build("Horseshoe");
            return horseshoe;
        }

        /// <summary>The horseshoe's eight nail holes (R133), four a branch from a little off the toe back to the quarters,
        /// in its groove: small blocks to draw dark, standing up in the groove to just under the face.</summary>
        public static Mesh HorseshoeHoles()
        {
            if (horseshoeHoles != null) return horseshoeHoles;
            var at = new List<Vector3>();
            var run = new List<float>();
            var outward = new List<Vector3>();
            HorseshoeLine(at, run, outward);
            float half = run[run.Count - 1] * 0.5f;
            var b = new Builder();
            foreach (float off in new[] { 0.3f, 0.44f, 0.58f, 0.72f })
                for (int side = -1; side <= 1; side += 2)
                {
                    float want = half + side * off;
                    int i = 1;
                    while (i < run.Count - 1 && run[i] < want) i++;
                    float f = Mathf.InverseLerp(run[i - 1], run[i], want);
                    Vector3 p = Vector3.Lerp(at[i - 1], at[i], f), o = Vector3.Lerp(outward[i - 1], outward[i], f).normalized;
                    Vector3 along = (at[i] - at[i - 1]).normalized;
                    b.RoundBox(p + o * 0.024f + Vector3.up * 0.017f, new Vector3(0.026f, 0.024f, 0.044f), 0.005f, Quaternion.LookRotation(along, Vector3.up));
                }
            horseshoeHoles = b.Build("Horseshoe nail holes");
            return horseshoeHoles;
        }

        // ---------------------------------------------------------------- a tassel (the bishop's carpet)

        static Mesh tassel;

        /// <summary>A little tassel hanging down from y = 0: a knob, a waist, a flared skirt of threads (0.13 long).</summary>
        public static Mesh Tassel()
        {
            if (tassel != null) return tassel;
            var b = new Builder();
            // (A profile runs bottom to top, so its faces turn outward.)
            b.Lathe(new[] { V(0f, -.134f), V(.03f, -.132f), V(.038f, -.128f), V(.034f, -.11f), V(.024f, -.07f), V(.012f, -.044f),
                            V(.01f, -.036f), V(.018f, -.03f), V(.02f, -.016f), V(.012f, -.004f), V(0f, 0f) }, 14);
            tassel = b.Build("Tassel");
            return tassel;
        }

        // ---------------------------------------------------------------- two arrows chasing round (a swap)

        static Mesh swapArrows;

        /// <summary>Two thick arrows chasing each other round a circle in the x-y plane (facing ±z), about 1 across:
        /// castling's "change places".</summary>
        public static Mesh SwapArrows()
        {
            if (swapArrows != null) return swapArrows;
            var b = new Builder();
            for (int arrow = 0; arrow < 2; arrow++)
            {
                float a0 = arrow * 180f + 20f, a1 = a0 + 125f;
                var path = new List<Vector3>();
                var rad = new List<float>();
                for (int i = 0; i <= 18; i++)
                {
                    float a = Mathf.Lerp(a0, a1, i / 18f) * Mathf.Deg2Rad;
                    path.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.36f);
                    rad.Add(0.055f);
                }
                b.Tube(path, rad, 12, false);
                b.Sphere(path[0], 0.055f, 10);
                // The head: a cone along the circle's tangent at its end.
                float e = a1 * Mathf.Deg2Rad;
                Vector3 tip = new Vector3(Mathf.Cos(e), Mathf.Sin(e), 0f) * 0.36f;
                Vector3 tangent = new Vector3(-Mathf.Sin(e), Mathf.Cos(e), 0f);
                Vector3 side = Vector3.Cross(tangent, Vector3.forward);
                int sides = 14, s0 = b.v.Count;
                for (int k = 0; k <= sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    b.v.Add(tip + (side * Mathf.Cos(a) + Vector3.forward * Mathf.Sin(a)) * 0.13f);
                }
                int apex = b.v.Count;
                b.v.Add(tip + tangent * 0.2f);
                int back = b.v.Count;
                b.v.Add(tip - tangent * 0.01f);
                for (int k = 0; k < sides; k++)
                {
                    b.tri.AddRange(new[] { s0 + k, apex, s0 + k + 1 });
                    b.tri.AddRange(new[] { s0 + k + 1, back, s0 + k });
                }
            }
            swapArrows = b.Build("Swap arrows");
            return swapArrows;
        }
    }
}
