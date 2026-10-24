using UnityEngine;

namespace ChessFight.RagdollLab
{
    public partial class SkillInkKit
    {
        /// <summary>
        /// A flat effect on the floor laid over the floor under it (R149). 승규 님 (10-16, a screen recording of the queen by a
        /// mini-game station): "모든 기물이 미니게임 같은 곳 바닥이 두꺼운 곳 부분에 스킬 vfx가 씹혀져서 나와". A lying effect was
        /// one flat quad at the caster's floor height, so where it reached over a raised floor (a station's dais stands 0.4 m
        /// up) it went into it and showed only on the lower side.
        ///
        /// <see cref="Fit"/> looks at the floor under the quad: if some of it is above or below the floor under its middle,
        /// the quad becomes a grid laid over the floor like a sticker (as QueenHillSkillFx's Tile.Drape, R98): each point sits
        /// over the floor found under it by as much as the middle sits over its own, and a cell that would hang down a step's
        /// face is left out, so a ring across the dais edge shows on both floors. The grid is in the quad's own unit space
        /// (lying, its +z is world up), so the shaders' uv-based shapes and the board look's slab draw as before. It is
        /// measured again only when the quad moves, turns or changes size. Only a quad whose middle is within 0.2 m of a floor
        /// (not a badge held up in the air) and only floors <see cref="FloorUnder(Vector3, float, out Vector3)"/> finds (not a
        /// piece, not a moving body).
        /// </summary>
        public sealed class FloorDrape
        {
            const float Cell = 0.3f, Level = 0.02f, OnFloor = 0.2f;
            const int Most = 24;
            static readonly float[] heights = new float[(Most + 1) * (Most + 1)];

            readonly Transform t;
            readonly MeshFilter filter;
            readonly Mesh quad;
            Mesh mesh;
            Vector3[] verts;
            int cols, rows;
            bool draped, measured;
            Vector3 lastAt, lastScale;
            Quaternion lastTurn;

            public FloorDrape(Transform t, MeshFilter filter, Mesh quad)
            {
                this.t = t;
                this.filter = filter;
                this.quad = quad;
            }

            /// <summary>Call after placing and sizing the quad each time it is shown; <paramref name="lying"/> false (standing,
            /// facing something) keeps the flat quad.</summary>
            public void Fit(bool lying)
            {
                // Lying face up (its +z world up) only: a quad turned some other way keeps its flat shape.
                if (!lying || Vector3.Dot(t.forward, Vector3.up) < 0.99f)
                {
                    measured = false;
                    Flat();
                    return;
                }
                if (measured && (t.position - lastAt).sqrMagnitude < 1e-6f && (t.localScale - lastScale).sqrMagnitude < 1e-6f
                    && Quaternion.Angle(t.rotation, lastTurn) < 0.1f)
                    return;
                measured = true;
                lastAt = t.position;
                lastScale = t.localScale;
                lastTurn = t.rotation;
                if (!FloorUnder(t.position, 1f, out Vector3 middle) || Mathf.Abs(t.position.y - middle.y) > OnFloor)
                {
                    Flat();
                    return;
                }
                float width = Mathf.Abs(t.lossyScale.x), length = Mathf.Abs(t.lossyScale.y);
                int c = Mathf.Clamp(Mathf.CeilToInt(width / Cell), 1, Most), r = Mathf.Clamp(Mathf.CeilToInt(length / Cell), 1, Most);
                bool level = true;
                for (int j = 0; j <= r; j++)
                    for (int i = 0; i <= c; i++)
                    {
                        Vector3 p = t.TransformPoint(new Vector3(i / (float)c - 0.5f, j / (float)r - 0.5f, 0f));
                        // Over a gap (no floor) the point stays at the quad's height.
                        float h = FloorUnder(p, 1f, out Vector3 floor) ? floor.y - middle.y : 0f;
                        heights[j * (c + 1) + i] = h;
                        if (Mathf.Abs(h) > Level) level = false;
                    }
                if (level)
                {
                    Flat();
                    return;
                }
                if (mesh == null || cols != c || rows != r) Make(c, r);
                // Heights are metres up; the quad's z is metres up only while its own z scale is 1 (it is, lying).
                float zScale = Mathf.Max(1e-4f, Mathf.Abs(t.lossyScale.z));
                float stepX = 1.5f * width / c, stepY = 1.5f * length / r;
                for (int j = 0; j < r; j++)
                    for (int i = 0; i < c; i++)
                    {
                        int row = j * (c + 1) + i, k = (j * c + i) * 4;
                        float h0 = heights[row], h1 = heights[row + 1], h2 = heights[row + c + 2], h3 = heights[row + c + 1];
                        bool face = Mathf.Abs(h0 - h1) > stepX || Mathf.Abs(h3 - h2) > stepX || Mathf.Abs(h0 - h3) > stepY || Mathf.Abs(h1 - h2) > stepY;
                        float x0 = i / (float)c - 0.5f, x1 = (i + 1) / (float)c - 0.5f, y0 = j / (float)r - 0.5f, y1 = (j + 1) / (float)r - 0.5f;
                        verts[k] = new Vector3(x0, y0, h0 / zScale);
                        // A step's face: the cell shrinks to a point (nothing drawn).
                        verts[k + 1] = face ? verts[k] : new Vector3(x1, y0, h1 / zScale);
                        verts[k + 2] = face ? verts[k] : new Vector3(x1, y1, h2 / zScale);
                        verts[k + 3] = face ? verts[k] : new Vector3(x0, y1, h3 / zScale);
                    }
                mesh.vertices = verts;
                mesh.RecalculateBounds();
                filter.sharedMesh = mesh;
                draped = true;
            }

            void Flat()
            {
                if (!draped) return;
                draped = false;
                filter.sharedMesh = quad;
            }

            void Make(int c, int r)
            {
                if (mesh == null) mesh = new Mesh { name = "Skill floor drape" };
                mesh.Clear();
                cols = c;
                rows = r;
                int cells = c * r;
                verts = new Vector3[cells * 4];
                var uv = new Vector2[cells * 4];
                var normals = new Vector3[cells * 4];
                var tris = new int[cells * 6];
                for (int j = 0; j < r; j++)
                    for (int i = 0; i < c; i++)
                    {
                        int k = (j * c + i) * 4, n = (j * c + i) * 6;
                        float u0 = i / (float)c, u1 = (i + 1) / (float)c, v0 = j / (float)r, v1 = (j + 1) / (float)r;
                        uv[k] = new Vector2(u0, v0);
                        uv[k + 1] = new Vector2(u1, v0);
                        uv[k + 2] = new Vector2(u1, v1);
                        uv[k + 3] = new Vector2(u0, v1);
                        for (int q = 0; q < 4; q++) normals[k + q] = Vector3.forward;
                        // Wound as the quad: 0 2 1, 0 3 2.
                        tris[n] = k; tris[n + 1] = k + 2; tris[n + 2] = k + 1;
                        tris[n + 3] = k; tris[n + 4] = k + 3; tris[n + 5] = k + 2;
                    }
                mesh.vertices = verts;
                mesh.uv = uv;
                mesh.normals = normals;
                mesh.triangles = tris;
            }

            public void Dispose()
            {
                if (mesh != null) Object.Destroy(mesh);
                mesh = null;
            }
        }
    }
}
