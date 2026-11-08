using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The six chess pieces in the Staunton shapes, made in code for the queen's gold pieces (R81: "체스 모양이랑
    /// 똑같아야"): turned bodies for all of them, then what a lathe cannot make: the rook's battlements, the knight's
    /// horse head (a side view cut out and given thickness), the queen's coronet of balls, the king's cross. Heights
    /// follow a real set (king tallest, pawn smallest); each mesh is 1 tall about its middle. Taken out with the
    /// cartoon queen of R86 and brought back in R88 (승규 님: "체스들이 모이면서 터지는 듯한 느낌"), drawn in cartoon gold.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        /// <summary>Pawn, knight, bishop, rook, queen, king; and how tall each stands next to the king.</summary>
        static readonly float[] ChessHeights = { 0.55f, 0.72f, 0.82f, 0.62f, 0.92f, 1f };

        static Mesh[] ChessPieces()
        {
            var pieces = new Mesh[6];

            // Pawn: a wide stepped base, a waisted stem, a collar, a round head.
            var b = new MeshBuilder();
            b.Lathe(new[] { V(0f, 0f), V(.36f, 0f), V(.36f, 0f), V(.36f, .05f), V(.36f, .05f), V(.3f, .07f), V(.3f, .1f), V(.24f, .12f),
                            V(.16f, .2f), V(.12f, .3f), V(.11f, .36f), V(.2f, .38f), V(.2f, .38f), V(.2f, .42f), V(.2f, .42f), V(.1f, .44f), V(0f, .44f) });
            b.Sphere(new Vector3(0f, .58f, 0f), .16f);
            pieces[0] = b.Build();

            // Knight: a turned base, then the horse's head, a side view given thickness, facing +x.
            b = new MeshBuilder();
            b.Lathe(new[] { V(0f, 0f), V(.38f, 0f), V(.38f, 0f), V(.38f, .06f), V(.38f, .06f), V(.3f, .09f), V(.27f, .14f), V(.22f, .2f), V(.22f, .24f), V(0f, .24f) });
            b.Extrude(new[]
            {
                V(-.22f, .2f), V(.18f, .2f), V(.2f, .3f), V(.13f, .42f), V(.3f, .5f), V(.4f, .56f), V(.4f, .66f), V(.27f, .73f),
                V(.15f, .84f), V(.1f, .94f), V(.04f, 1.02f), V(-.03f, .92f), V(-.14f, .87f), V(-.24f, .7f), V(-.28f, .48f), V(-.26f, .3f),
            }, .3f);
            pieces[1] = b.Build();

            // Bishop: base, stem, collar, the mitre (a tall rounded head) and a small ball on top.
            b = new MeshBuilder();
            b.Lathe(new[] { V(0f, 0f), V(.36f, 0f), V(.36f, 0f), V(.36f, .05f), V(.36f, .05f), V(.28f, .09f), V(.24f, .13f), V(.15f, .3f),
                            V(.12f, .48f), V(.22f, .52f), V(.22f, .52f), V(.22f, .55f), V(.22f, .55f), V(.13f, .58f), V(.17f, .64f), V(.21f, .72f),
                            V(.2f, .8f), V(.15f, .88f), V(.07f, .94f), V(0f, .96f) });
            b.Sphere(new Vector3(0f, 1.0f, 0f), .055f);
            pieces[2] = b.Build();

            // Rook: a tower, a flared top, four battlements round an open top.
            b = new MeshBuilder();
            b.Lathe(new[] { V(0f, 0f), V(.4f, 0f), V(.4f, 0f), V(.4f, .06f), V(.4f, .06f), V(.32f, .1f), V(.28f, .16f), V(.27f, .62f),
                            V(.35f, .7f), V(.35f, .7f), V(.35f, .78f), V(.35f, .78f), V(.24f, .78f), V(.24f, .78f), V(.24f, .74f), V(0f, .74f) });
            for (int i = 0; i < 4; i++) b.RingPiece(.24f, .35f, .78f, .94f, i * 90f + 45f - 26f, i * 90f + 45f + 26f);
            pieces[3] = b.Build();

            // Queen: base, a long stem, a collar, a bowl that opens up to the crown, a coronet of balls, a ball on top.
            b = new MeshBuilder();
            b.Lathe(new[] { V(0f, 0f), V(.38f, 0f), V(.38f, 0f), V(.38f, .05f), V(.38f, .05f), V(.3f, .09f), V(.25f, .14f), V(.14f, .4f),
                            V(.12f, .56f), V(.23f, .6f), V(.23f, .6f), V(.23f, .63f), V(.23f, .63f), V(.13f, .66f), V(.17f, .76f), V(.27f, .86f),
                            V(.27f, .86f), V(.22f, .88f), V(.12f, .9f), V(.06f, .93f), V(0f, .94f) });
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                b.Sphere(new Vector3(Mathf.Cos(a) * .25f, .88f, Mathf.Sin(a) * .25f), .045f, 8);
            }
            b.Sphere(new Vector3(0f, 1.0f, 0f), .07f);
            pieces[4] = b.Build();

            // King: like the queen, a closed top, and a cross.
            b = new MeshBuilder();
            b.Lathe(new[] { V(0f, 0f), V(.38f, 0f), V(.38f, 0f), V(.38f, .05f), V(.38f, .05f), V(.3f, .09f), V(.25f, .14f), V(.14f, .42f),
                            V(.12f, .56f), V(.23f, .6f), V(.23f, .6f), V(.23f, .63f), V(.23f, .63f), V(.13f, .66f), V(.18f, .76f), V(.25f, .82f),
                            V(.25f, .82f), V(.22f, .85f), V(.12f, .88f), V(0f, .89f) });
            b.Box(new Vector3(0f, .97f, 0f), new Vector3(.07f, .2f, .07f));
            b.Box(new Vector3(0f, 1.0f, 0f), new Vector3(.19f, .065f, .07f));
            pieces[5] = b.Build();

            return pieces;
        }

        /// <summary>Parts of a mesh put together: turned profiles, balls, boxes, ring pieces, cut-out shapes. A
        /// profile point given twice makes a sharp edge there (the two copies get their own normals).</summary>
        class MeshBuilder
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<int> tri = new List<int>();

            /// <summary>A profile (radius, height; bottom to top) turned about the vertical.</summary>
            public void Lathe(Vector2[] profile, int segments = 20)
            {
                int start = v.Count, ring = segments + 1;
                foreach (var p in profile)
                    for (int k = 0; k <= segments; k++)
                    {
                        float a = k * Mathf.PI * 2f / segments;
                        v.Add(new Vector3(Mathf.Cos(a) * p.x, p.y, Mathf.Sin(a) * p.x));
                    }
                for (int i = 0; i < profile.Length - 1; i++)
                    for (int k = 0; k < segments; k++)
                    {
                        int a = start + i * ring + k, c = a + ring;
                        tri.AddRange(new[] { a, c, a + 1, a + 1, c, c + 1 });
                    }
            }

            public void Sphere(Vector3 center, float radius, int segments = 12)
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

            /// <summary>An upright box, each face with its own corners (flat shaded).</summary>
            public void Box(Vector3 center, Vector3 size)
            {
                Vector3 h = size * 0.5f;
                Quad(center + new Vector3(-h.x, -h.y, h.z), center + new Vector3(h.x, -h.y, h.z), center + new Vector3(h.x, h.y, h.z), center + new Vector3(-h.x, h.y, h.z));
                Quad(center + new Vector3(h.x, -h.y, -h.z), center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3(-h.x, h.y, -h.z), center + new Vector3(h.x, h.y, -h.z));
                Quad(center + new Vector3(h.x, -h.y, h.z), center + new Vector3(h.x, -h.y, -h.z), center + new Vector3(h.x, h.y, -h.z), center + new Vector3(h.x, h.y, h.z));
                Quad(center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3(-h.x, -h.y, h.z), center + new Vector3(-h.x, h.y, h.z), center + new Vector3(-h.x, h.y, -h.z));
                Quad(center + new Vector3(-h.x, h.y, h.z), center + new Vector3(h.x, h.y, h.z), center + new Vector3(h.x, h.y, -h.z), center + new Vector3(-h.x, h.y, -h.z));
                Quad(center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3(h.x, -h.y, -h.z), center + new Vector3(h.x, -h.y, h.z), center + new Vector3(-h.x, -h.y, h.z));
            }

            /// <summary>A piece of a thick ring between two angles (degrees): a rook's battlement.</summary>
            public void RingPiece(float inner, float outer, float y0, float y1, float from, float to, int steps = 5)
            {
                Vector3 P(float r, float deg, float y) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad) * r, y, Mathf.Sin(deg * Mathf.Deg2Rad) * r);
                for (int i = 0; i < steps; i++)
                {
                    float a = Mathf.Lerp(from, to, i / (float)steps), c = Mathf.Lerp(from, to, (i + 1) / (float)steps);
                    Quad(P(outer, a, y0), P(outer, a, y1), P(outer, c, y1), P(outer, c, y0));   // outside
                    Quad(P(inner, c, y0), P(inner, c, y1), P(inner, a, y1), P(inner, a, y0));   // inside
                    Quad(P(inner, a, y1), P(inner, c, y1), P(outer, c, y1), P(outer, a, y1));   // top
                }
                Quad(P(inner, from, y0), P(inner, from, y1), P(outer, from, y1), P(outer, from, y0));
                Quad(P(outer, to, y0), P(outer, to, y1), P(inner, to, y1), P(inner, to, y0));
            }

            /// <summary>A shape drawn side on (x forward, y up, counter-clockwise) made <paramref name="depth"/> thick.</summary>
            public void Extrude(Vector2[] outline, float depth)
            {
                float z = depth * 0.5f;
                var caps = Triangulate(outline);
                int front = v.Count;
                foreach (var p in outline) v.Add(new Vector3(p.x, p.y, z));
                int back = v.Count;
                foreach (var p in outline) v.Add(new Vector3(p.x, p.y, -z));
                // Counter-clockwise in x-y reads clockwise from +z (Unity's front face): as is in front, turned round behind.
                for (int i = 0; i < caps.Count; i += 3)
                {
                    tri.AddRange(new[] { front + caps[i], front + caps[i + 1], front + caps[i + 2] });
                    tri.AddRange(new[] { back + caps[i], back + caps[i + 2], back + caps[i + 1] });
                }
                for (int i = 0; i < outline.Length; i++)
                {
                    Vector2 a = outline[i], c = outline[(i + 1) % outline.Length];
                    Quad(new Vector3(a.x, a.y, z), new Vector3(a.x, a.y, -z), new Vector3(c.x, c.y, -z), new Vector3(c.x, c.y, z));
                }
            }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int s = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                tri.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }

            /// <summary>Ear clipping of a simple counter-clockwise polygon.</summary>
            static List<int> Triangulate(Vector2[] poly)
            {
                var idx = new List<int>();
                for (int i = 0; i < poly.Length; i++) idx.Add(i);
                var result = new List<int>();
                int guard = 0;
                while (idx.Count > 3 && guard++ < 1000)
                {
                    bool clipped = false;
                    for (int i = 0; i < idx.Count; i++)
                    {
                        int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                        Vector2 a = poly[ia], b = poly[ib], c = poly[ic];
                        if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 0f) continue;   // reflex
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

            /// <summary>The mesh, its middle at the origin (so it spins about its own centre), 1 tall.</summary>
            public Mesh Build()
            {
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var p in v) { lo = Mathf.Min(lo, p.y); hi = Mathf.Max(hi, p.y); }
                float h = Mathf.Max(1e-3f, hi - lo), mid = (lo + hi) * 0.5f;
                var verts = new List<Vector3>(v.Count);
                foreach (var p in v) verts.Add(new Vector3(p.x, p.y - mid, p.z) / h);
                var m = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                m.SetVertices(verts);
                m.SetTriangles(tri, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
