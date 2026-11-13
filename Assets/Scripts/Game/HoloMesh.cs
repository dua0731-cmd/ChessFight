using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    // The geometry of a hologram (10-26, the entrance screen's course model): faces for its faint light
    // (ChessFight/Hologram) and lines for its glowing edges (ChessFight/Hologram Line). Every line is a quad the line
    // shader turns to the camera about the line, so each of its corners carries both end points (uv1: the other end),
    // which side it is on (uv0.x), which end (uv0.y), how far along a path it is (uv0.z, for the route's light) and
    // whether the line has round ends (uv0.w); the vertex colour's alpha is how strongly it glows.
    public sealed class HoloMesh
    {
        readonly List<Vector3> faceVerts = new List<Vector3>(), faceNormals = new List<Vector3>();
        readonly List<int> faceTris = new List<int>();
        readonly List<Vector3> lineVerts = new List<Vector3>();
        readonly List<Vector4> lineUv = new List<Vector4>(), lineOther = new List<Vector4>();
        readonly List<Color32> lineColors = new List<Color32>();
        readonly List<int> lineTris = new List<int>();

        // A flat face, its corners in order round it.
        public void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var n = Vector3.Cross(b - a, d - a).normalized;
            int i = faceVerts.Count;
            faceVerts.Add(a);
            faceVerts.Add(b);
            faceVerts.Add(c);
            faceVerts.Add(d);
            for (int k = 0; k < 4; k++) faceNormals.Add(n);
            faceTris.Add(i);
            faceTris.Add(i + 1);
            faceTris.Add(i + 2);
            faceTris.Add(i);
            faceTris.Add(i + 2);
            faceTris.Add(i + 3);
        }

        // A glowing line from a to b. `from` and `to`: how far along a path its ends are. Round ends make the corners
        // where edges meet glow; lines that continue one another (a ring, a path) go without.
        public void Line(Vector3 a, Vector3 b, float strength = 1f, float from = 0f, float to = 0f, bool round = true)
        {
            if ((b - a).sqrMagnitude < 1e-8f) return;
            int i = lineVerts.Count;
            var colour = (Color32)new Color(1f, 1f, 1f, Mathf.Clamp01(strength));
            for (int end = 0; end < 2; end++)
                for (int side = -1; side <= 1; side += 2)
                {
                    lineVerts.Add(end == 0 ? a : b);
                    lineOther.Add(end == 0 ? b : a);
                    lineUv.Add(new Vector4(side, end, end == 0 ? from : to, round ? 1f : 0f));
                    lineColors.Add(colour);
                }
            lineTris.Add(i);
            lineTris.Add(i + 1);
            lineTris.Add(i + 2);
            lineTris.Add(i + 2);
            lineTris.Add(i + 1);
            lineTris.Add(i + 3);
        }

        // A box: its faces (the bottom one only when asked: one standing on a floor would light the floor twice) and
        // its twelve edges.
        public void Box(Vector3 min, Vector3 max, bool bottom = true, float strength = 1f)
        {
            Vector3 P(int x, int y, int z) => new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
            Face(P(0, 1, 0), P(0, 1, 1), P(1, 1, 1), P(1, 1, 0));
            if (bottom) Face(P(0, 0, 0), P(1, 0, 0), P(1, 0, 1), P(0, 0, 1));
            Face(P(0, 0, 0), P(0, 1, 0), P(1, 1, 0), P(1, 0, 0));
            Face(P(0, 0, 1), P(1, 0, 1), P(1, 1, 1), P(0, 1, 1));
            Face(P(0, 0, 0), P(0, 0, 1), P(0, 1, 1), P(0, 1, 0));
            Face(P(1, 0, 0), P(1, 1, 0), P(1, 1, 1), P(1, 0, 1));
            for (int a = 0; a < 2; a++)
                for (int b = 0; b < 2; b++)
                {
                    Line(P(0, a, b), P(1, a, b), strength);
                    Line(P(a, 0, b), P(a, 1, b), strength);
                    Line(P(a, b, 0), P(a, b, 1), strength);
                }
        }

        // A level ring round `centre`.
        public void Ring(Vector3 centre, float radius, int segments, float strength = 1f)
        {
            for (int k = 0; k < segments; k++)
            {
                float a0 = k * Mathf.PI * 2f / segments, a1 = (k + 1) * Mathf.PI * 2f / segments;
                Line(centre + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius,
                     centre + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius, strength, round: false);
            }
        }

        public Mesh FaceMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (faceVerts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(faceVerts);
            mesh.SetNormals(faceNormals);
            mesh.SetTriangles(faceTris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // `reach`: how far (in the mesh's units) the quads may stand out past the end points, for the bounds.
        public Mesh LineMesh(string name, float reach)
        {
            var mesh = new Mesh { name = name };
            if (lineVerts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(lineVerts);
            mesh.SetUVs(0, lineUv);
            mesh.SetUVs(1, lineOther);
            mesh.SetColors(lineColors);
            mesh.SetTriangles(lineTris, 0);
            mesh.RecalculateBounds();
            var bounds = mesh.bounds;
            bounds.Expand(reach * 2f);
            mesh.bounds = bounds;
            return mesh;
        }
    }
}
