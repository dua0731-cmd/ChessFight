using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R505: the D capstan's chain and pulley working together (user, 11-07: "회전 바퀴랑 쇠 사슬이랑 실제로 엉키면서
    // 상호작용"). Pictures only: no collider, no body. The chain is a row of links laid along a path that leaves the
    // drum, wraps over the pulley wheel's groove and drops into the arch. When pawns turn the capstan the wheel turns
    // with it and the links run along the path by as much as the wheel's groove moves, so the chain is seen wrapping
    // and feeding over the wheel. Both ends of the path are hidden (inside the drum, inside the arch), where links
    // leave and come back round.
    // StateDrivenMover: it follows the capstan's bars, which pawns push (physics on the host, the host's angle on the
    // others, MgCapstan), not the obstacle clock.
    public sealed class MiniGamePulleyChain : MonoBehaviour
    {
        Transform source, wheel;
        Quaternion wheelRest;
        float ratio, groove, pitch, length;
        float lastYaw, turned;      // the source's angle in degrees, unwrapped (no jump at 360 -> 0)
        readonly List<Vector3> points = new List<Vector3>();
        readonly List<float> along = new List<float>();
        readonly List<Transform> links = new List<Transform>();

        // path: points in this transform's space; wheel turns about its own local x; groove: the radius the chain lies on.
        public void Setup(Transform turning, Transform pulleyWheel, float turnsPerTurn, IList<Vector3> path, float grooveRadius,
                          float linkPitch, Mesh link, Material material)
        {
            source = turning; wheel = pulleyWheel; ratio = turnsPerTurn; groove = grooveRadius; pitch = linkPitch;
            wheelRest = wheel.localRotation;
            lastYaw = source.localEulerAngles.y;
            points.Clear(); along.Clear();
            float total = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0) total += Vector3.Distance(path[i - 1], path[i]);
                points.Add(path[i]); along.Add(total);
            }
            length = total;
            int n = Mathf.Max(2, Mathf.FloorToInt(length / pitch));
            if (n % 2 == 1) n--;                             // even, so the turned/flat order holds across the hidden seam
            pitch = length / n;
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Pulley chain link", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = link;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                links.Add(go.transform);
            }
            Pose(0f);
        }

        void LateUpdate()
        {
            if (source == null) return;
            float yaw = source.localEulerAngles.y;
            turned += Mathf.DeltaAngle(lastYaw, yaw);
            lastYaw = yaw;
            float wheelDegrees = turned * ratio;
            if (wheel != null) wheel.localRotation = wheelRest * Quaternion.AngleAxis(wheelDegrees, Vector3.right);
            // A positive turn about +x carries the wheel's top toward +z, the way the path runs over it.
            Pose(wheelDegrees * Mathf.Deg2Rad * groove);
        }

        void Pose(float offset)
        {
            for (int i = 0; i < links.Count; i++)
            {
                float s = Mathf.Repeat(i * pitch + offset, length);
                Eval(s, out var at, out var forward);
                var flatUp = Vector3.Cross(forward, Vector3.right);
                links[i].localPosition = at;
                links[i].localRotation = Quaternion.LookRotation(forward, (i & 1) == 0 ? Vector3.right : flatUp);
            }
        }

        void Eval(float s, out Vector3 at, out Vector3 forward)
        {
            int k = 1;
            while (k < along.Count - 1 && along[k] < s) k++;
            float span = Mathf.Max(1e-4f, along[k] - along[k - 1]);
            float t = Mathf.Clamp01((s - along[k - 1]) / span);
            at = Vector3.Lerp(points[k - 1], points[k], t);
            forward = (points[k] - points[k - 1]).normalized;
        }

        // One oval link: long along local z, wide along local y, wire round in its cross-section.
        public static Mesh LinkMesh(float linkLength, float linkWidth, float wire)
        {
            const int seg = 12, ring = 6;
            var v = new List<Vector3>(); var tri = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                float cz = Mathf.Cos(a), cy = Mathf.Sin(a);
                var centre = new Vector3(0f, cy * (linkWidth * .5f - wire), cz * (linkLength * .5f - wire));
                var radial = new Vector3(0f, cy, cz * (linkWidth / linkLength)).normalized;
                for (int j = 0; j < ring; j++)
                {
                    float b = j * Mathf.PI * 2f / ring;
                    v.Add(centre + (radial * Mathf.Cos(b) + Vector3.right * Mathf.Sin(b)) * wire);
                }
            }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < ring; j++)
                {
                    int a0 = i * ring + j, a1 = ((i + 1) % seg) * ring + j, b0 = i * ring + (j + 1) % ring, b1 = ((i + 1) % seg) * ring + (j + 1) % ring;
                    tri.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }
            var mesh = new Mesh { name = "D capstan chain link" };
            mesh.SetVertices(v); mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
