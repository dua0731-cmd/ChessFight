using System;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R725 ro-vfx (user 11-20 "경통에 콜라이더 해서 충돌 판정 넣어"): the great telescope's tube holds a pawn. The builder
    // draws it only (a truss tube round the deck: "its sides are open, and off the deck is a fall"); as the globe's glass
    // (.Glass.cs) the builder is left as it is and the collider goes on as the models hang.
    //   - an open cylinder the tube's own radius (3.7 m) round its axis (2.73 m over the deck's middle line, along it),
    //     from the tube's first ring to the objective's (0.04 to 0.96 of the deck): the deck is a chord of it, so its
    //     edges meet the wall and nothing falls off the sides now. Both ends open: in from the stage, out to the balcony.
    //   - every face both ways round (a mesh collider's face holds only from its front), not climbable (NoClimbSurface)
    //     and the follow cameras look through it (CameraSeeThrough), as the glass does.
    // The iris gates' leaves wait outside the deck's edges and so through this wall; they are kinematic and push nothing
    // here, as before. The builder's numbers (RoyalObservatoryBuilder: DeckFoot, DeckHead, TubeRadius, DeckHalf,
    // TubeRings) are copied below; change them together.
    public sealed partial class RoyalObservatoryDressing
    {
        static readonly Vector3 TubeDeckFoot = new Vector3(9f, 12f, 93f), TubeDeckHead = new Vector3(9f, 20f, 118f);
        const float TubeWallRadius = 3.7f, TubeDeckHalf = 2.5f, TubeFrom = .04f, TubeTo = .96f;
        const int TubeSegments = 64, TubeRows = 24;

        static readonly Action<RoyalObservatoryDressing> TelescopeTubeCollider = dressing =>
        {
            var content = dressing.transform;
            foreach (var group in dressing.FindAll("5 Great telescope"))
            {
                var along = (TubeDeckHead - TubeDeckFoot).normalized;
                var normal = Vector3.Cross(along, Vector3.right);
                float axis = Mathf.Sqrt(TubeWallRadius * TubeWallRadius - TubeDeckHalf * TubeDeckHalf);
                var start = Vector3.LerpUnclamped(TubeDeckFoot, TubeDeckHead, TubeFrom) + normal * axis;
                var end = Vector3.LerpUnclamped(TubeDeckFoot, TubeDeckHead, TubeTo) + normal * axis;

                var go = new GameObject("Telescope tube (collider)");
                go.layer = group.gameObject.layer;
                go.transform.SetParent(group, false);
                // In the section's frame (Content), where the builder's numbers are.
                go.transform.SetPositionAndRotation(content.position, content.rotation);
                var s = content.lossyScale;
                var g = group.lossyScale;
                go.transform.localScale = new Vector3(s.x / Mathf.Max(g.x, 1e-4f), s.y / Mathf.Max(g.y, 1e-4f), s.z / Mathf.Max(g.z, 1e-4f));
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = TubeWall(start, end, Vector3.right, normal, TubeWallRadius);
                go.AddComponent<NoClimbSurface>();
                go.AddComponent<CameraSeeThrough>();
                Debug.Log("[천문대] 경통 충돌: 반지름 " + TubeWallRadius.ToString("0.0") + " m 원통 벽, 길이 "
                          + (end - start).magnitude.ToString("0.0") + " m, 위아래 입구 열림, 카메라 통과 · 못 기어오름");
            }
        };

        // An open cylinder from `start` to `end` of `radius` round them (`x` and `y` across it), every face both ways.
        static Mesh TubeWall(Vector3 start, Vector3 end, Vector3 x, Vector3 y, float radius)
        {
            var points = new List<Vector3>((TubeRows + 1) * (TubeSegments + 1));
            for (int j = 0; j <= TubeRows; j++)
            {
                var c = Vector3.Lerp(start, end, j / (float)TubeRows);
                for (int k = 0; k <= TubeSegments; k++)
                {
                    float a = 2f * Mathf.PI * k / TubeSegments;
                    points.Add(c + (x * Mathf.Cos(a) + y * Mathf.Sin(a)) * radius);
                }
            }
            var triangles = new List<int>(TubeRows * TubeSegments * 12);
            int row = TubeSegments + 1;
            for (int j = 0; j < TubeRows; j++)
                for (int k = 0; k < TubeSegments; k++)
                {
                    int a = j * row + k, b = a + 1, c = a + row, d = c + 1;
                    triangles.AddRange(new[] { a, c, b, a, b, c, b, c, d, b, d, c });
                }
            var mesh = new Mesh { name = "Telescope tube collider", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(points);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
