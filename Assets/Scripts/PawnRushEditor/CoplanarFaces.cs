using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush.Editor
{
    // R904: two triangles facing the same way within `tolerance` of one plane and sharing more than `minOverlap` of area
    // flicker in Unity (z-fighting, PITFALLS #39). Shared by CoplanarScan (a section as drawn in Play) and
    // CoplanarLiftImporter (the faces inside one FBX). Faces are bucketed by their normal (cells of .02) and their plane
    // (bands of `tolerance`); a bucket is compared with itself and the next band, swept along the axis the normal is
    // least along. Normals on a cell's edge can be missed; good enough for the walls, floors and trims it is for.
    public static class CoplanarFaces
    {
        public struct Face
        {
            public Vector3 a, b, c, n;
            public float d, area;
            public int group;   // the caller's: a renderer + material, a mesh + submesh
            public int tri;     // the caller's: where the face starts in its submesh's indices
            public int lod;     // -1: compared with every level; else only with -1 and the same level
        }

        const float NormalCell = .02f;

        // The face of three points, or false when it is smaller than `minArea`.
        public static bool Make(Vector3 a, Vector3 b, Vector3 c, int group, int lod, float minArea, out Face face)
        {
            var cross = Vector3.Cross(b - a, c - a);
            float area = cross.magnitude * .5f;
            face = default;
            if (area < minArea) return false;
            var n = cross / (area * 2);
            face = new Face { a = a, b = b, c = c, n = n, d = Vector3.Dot(n, a), area = area, group = group, lod = lod };
            return true;
        }

        // Calls `pair(i, j, overlap)` once for every overlapping pair (i < j not promised).
        public static void Pairs(List<Face> faces, float tolerance, float minOverlap, Action<int, int, float> pair)
        {
            var groups = new Dictionary<(int, int, int, int), List<int>>();
            for (int i = 0; i < faces.Count; i++)
            {
                var f = faces[i];
                var key = (Mathf.RoundToInt(f.n.x / NormalCell), Mathf.RoundToInt(f.n.y / NormalCell), Mathf.RoundToInt(f.n.z / NormalCell),
                           Mathf.FloorToInt(f.d / tolerance));
                if (!groups.TryGetValue(key, out var g)) groups[key] = g = new List<int>();
                g.Add(i);
            }
            var cand = new List<(int face, bool own)>();
            foreach (var kv in groups)
            {
                var k = kv.Key;
                cand.Clear();
                foreach (var i in kv.Value) cand.Add((i, true));
                if (groups.TryGetValue((k.Item1, k.Item2, k.Item3, k.Item4 + 1), out var next))
                    foreach (var i in next) cand.Add((i, false));
                if (cand.Count < 2) continue;
                var n0 = faces[kv.Value[0]].n;
                int axis = Mathf.Abs(n0.x) <= Mathf.Abs(n0.y) && Mathf.Abs(n0.x) <= Mathf.Abs(n0.z) ? 0 : Mathf.Abs(n0.y) <= Mathf.Abs(n0.z) ? 1 : 2;
                cand.Sort((x, y) => Min(faces[x.face], axis).CompareTo(Min(faces[y.face], axis)));
                for (int p = 0; p < cand.Count; p++)
                {
                    var A = faces[cand[p].face];
                    float maxA = Max(A, axis);
                    for (int q = p + 1; q < cand.Count && Min(faces[cand[q].face], axis) <= maxA; q++)
                    {
                        if (!cand[p].own && !cand[q].own) continue;
                        var B = faces[cand[q].face];
                        if (A.lod >= 0 && B.lod >= 0 && A.lod != B.lod) continue;
                        if (Vector3.Dot(A.n, B.n) < .9995f || Mathf.Abs(A.d - B.d) > tolerance) continue;
                        if (!Touch(A, B, tolerance)) continue;
                        float area = Overlap(A, B);
                        if (area >= minOverlap) pair(cand[p].face, cand[q].face, area);
                    }
                }
            }
        }

        static float Min(Face f, int axis) => Mathf.Min(f.a[axis], Mathf.Min(f.b[axis], f.c[axis]));
        static float Max(Face f, int axis) => Mathf.Max(f.a[axis], Mathf.Max(f.b[axis], f.c[axis]));

        static bool Touch(Face a, Face b, float e)
        {
            for (int k = 0; k < 3; k++)
                if (Min(a, k) > Max(b, k) + e || Min(b, k) > Max(a, k) + e) return false;
            return true;
        }

        // The area two near-coplanar triangles share: both laid on A's plane, B clipped by A's three edges.
        static float Overlap(Face A, Face B)
        {
            var u = Vector3.Cross(A.n, Mathf.Abs(A.n.y) < .9f ? Vector3.up : Vector3.right).normalized;
            var v = Vector3.Cross(A.n, u);
            Vector2 P(Vector3 p) => new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v));
            var clip = new List<Vector2> { P(A.a), P(A.b), P(A.c) };
            if (Signed(clip) < 0) clip.Reverse();
            var poly = new List<Vector2> { P(B.a), P(B.b), P(B.c) };
            for (int e = 0; e < 3 && poly.Count > 2; e++)
            {
                Vector2 c0 = clip[e], c1 = clip[(e + 1) % 3];
                var output = new List<Vector2>(poly.Count + 2);
                for (int i = 0; i < poly.Count; i++)
                {
                    Vector2 s = poly[i], t = poly[(i + 1) % poly.Count];
                    float ds = Side(c0, c1, s), dt = Side(c0, c1, t);
                    if (ds >= 0) output.Add(s);
                    if ((ds >= 0) != (dt >= 0)) output.Add(s + (t - s) * (ds / (ds - dt)));
                }
                poly = output;
            }
            return poly.Count > 2 ? Mathf.Abs(Signed(poly)) : 0f;
        }

        static float Side(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        static float Signed(List<Vector2> p)
        {
            float s = 0;
            for (int i = 0; i < p.Count; i++) { var a = p[i]; var b = p[(i + 1) % p.Count]; s += a.x * b.y - b.x * a.y; }
            return s * .5f;
        }

        // "<name>_LOD2" -> 2, else -1.
        public static int LodOf(string name)
        {
            int at = name.LastIndexOf("_LOD", StringComparison.Ordinal);
            return at >= 0 && int.TryParse(name.Substring(at + 4), out int k) ? k : -1;
        }
    }
}
