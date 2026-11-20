using System;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R725 ro-vfx (user 11-20 "대천구 유리 충돌 판정이 없어 넣어줘"): the great globe's glass holds a pawn. The builder
    // draws it only ("Great globe glass": glass does not hold a pawn, and a camera sees through it); as the telescope's
    // fork arms (.Telescope.cs), the builder is left as it is and the collider goes on as the models hang.
    //   - a sphere shell of the glass's own radius (14 m) round its middle, open at the two gates exactly where the glass
    //     model is (ro_r3_globe_model.glass_gates: round the point 0.5 m outside each gate and 3 m over its threshold,
    //     3.45 m across the sphere - the gold lip): no collider inside the lip, at most one cell (0.46 m) past it.
    //     The user: "보이는 구멍 그대로" - at the threshold's height it is about 3.4 m wide, at a pawn's waist 5.6 m.
    //   - not climbable (NoClimbSurface; standing on its top is still possible from outside) and the follow cameras
    //     look through it (CameraSeeThrough), so the glass never pulls the view in (PLAN section 3).
    // The solar flare's throw no longer carries a pawn out over the floor's edge: it strikes the glass and stays
    // inside, knocked down, sliding down the floor (the user: "괜찮아, 안에 남기"). Its timing and push are unchanged.
    // (First written as .GlobeGlass.cs, which the Editor never took in as a script - the user's Play passed through,
    // "아직 통과 돼" - so it is this file now.)
    public sealed partial class RoyalObservatoryDressing
    {
        const int GlassSegments = 192, GlassRings = 96;
        static readonly Vector3[] GlassGates = { new Vector3(-12.65f, 4f, 64f), new Vector3(13.86f, 12f, 64f) };
        const float GlassGateRadius = 3.45f, GlassGateOut = .5f, GlassGateUp = 3f;

        static readonly Action<RoyalObservatoryDressing> GlobeGlassCollider = dressing =>
        {
            var content = dressing.transform;
            foreach (var glass in dressing.FindAll("Great globe glass"))
            {
                float radius = .5f * glass.lossyScale.x;   // Unity's sphere, scaled to the globe
                var go = new GameObject("Great globe glass (collider)");
                go.layer = glass.gameObject.layer;
                go.transform.SetParent(glass.parent, false);
                go.transform.SetPositionAndRotation(glass.position, content.rotation);
                var holes = new List<(Vector3 dir, float angle)>();
                for (int i = 0; i < GlassGates.Length; i++)
                {
                    var g = GlassGates[i];
                    float side = g.x < 0f ? -1f : 1f;
                    var lip = content.TransformPoint(new Vector3(g.x + side * GlassGateOut, g.y + GlassGateUp, g.z));
                    var dir = go.transform.InverseTransformDirection((lip - glass.position).normalized);
                    holes.Add((dir.normalized, Mathf.Asin(Mathf.Clamp01(GlassGateRadius / Mathf.Max(radius, .01f)))));
                }
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = GlassShell(radius / Mathf.Max(go.transform.lossyScale.x, 1e-4f), holes);
                go.AddComponent<NoClimbSurface>();
                go.AddComponent<CameraSeeThrough>();
                Debug.Log("[천문대] 대천구 유리 충돌: 반지름 " + radius.ToString("0.0") + " m 구 껍데기, 문 구멍 " + holes.Count
                          + "개(보이는 금 테두리 그대로), 카메라 통과 · 못 기어오름");
            }
        };

        // A sphere of `radius` (lines of latitude round y), every face twice (once each way round, so it holds from
        // inside and outside), without every cell that reaches inside a hole.
        static Mesh GlassShell(float radius, List<(Vector3 dir, float angle)> holes)
        {
            var points = new List<Vector3>((GlassRings + 1) * (GlassSegments + 1));
            for (int i = 0; i <= GlassRings; i++)
            {
                float th = Mathf.PI * i / GlassRings;
                for (int k = 0; k <= GlassSegments; k++)
                {
                    float ph = 2f * Mathf.PI * k / GlassSegments;
                    points.Add(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * radius);
                }
            }
            bool Open(Vector3 p)
            {
                var d = p.normalized;
                foreach (var (dir, angle) in holes)
                    if (Vector3.Angle(d, dir) * Mathf.Deg2Rad < angle) return true;
                return false;
            }
            var triangles = new List<int>(GlassRings * GlassSegments * 12);
            int row = GlassSegments + 1;
            for (int i = 0; i < GlassRings; i++)
                for (int k = 0; k < GlassSegments; k++)
                {
                    int a = i * row + k, b = a + 1, c = a + row, d = c + 1;
                    if (Open(points[a]) || Open(points[b]) || Open(points[c]) || Open(points[d])) continue;
                    // Each face both ways round: a mesh collider's face holds only from its front (user 11-20 "아직 통과
                    // 돼"), and the glass must hold from inside (the floor, the flare's throw) and from outside.
                    if (i > 0) triangles.AddRange(new[] { a, c, b, a, b, c });
                    if (i < GlassRings - 1) triangles.AddRange(new[] { b, c, d, b, d, c });
                }
            var mesh = new Mesh { name = "Great globe glass collider", hideFlags = HideFlags.DontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(points);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
