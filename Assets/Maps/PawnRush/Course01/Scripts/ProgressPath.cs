using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The progress path (design doc v0.2 §3, points P0..P32): white's points, local to the course
    // root; black's are their mirror (x -> -x), so both are the same length. Each leg (from point i
    // to i + 1) belongs to a section (S+A, W1, B, W2, C, T) for the HUD and the playtest CSV.
    // Progress is the distance along the path to the point nearest the character (the judge's
    // third tiebreak, total progress, Pawn Rush v1.0).
    public sealed class ProgressPath : MonoBehaviour
    {
        [SerializeField] Vector3[] points = new Vector3[0];
        [Tooltip("The section of the leg that starts at each point.")]
        [SerializeField] string[] sections = new string[0];

        public int Count => points.Length;

        public void Configure(Vector3[] points, string[] sections)
        {
            this.points = points;
            this.sections = sections;
        }

        public Vector3 Point(int i, int team)
        {
            var p = points[i];
            if (team == Teams.Black) p.x = -p.x;
            return transform.TransformPoint(p);
        }

        public List<Vector3> World(int team)
        {
            var list = new List<Vector3>(points.Length);
            for (int i = 0; i < points.Length; i++) list.Add(Point(i, team));
            return list;
        }

        public static float Length(List<Vector3> path)
        {
            float total = 0f;
            for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
            return total;
        }

        // Metres along the path to the point nearest `position`, and the leg it lies on.
        public static float Along(List<Vector3> path, Vector3 position, out int leg)
        {
            float best = float.MaxValue, at = 0f, walked = 0f;
            leg = 0;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 a = path[i - 1], b = path[i], ab = b - a;
                float len = ab.magnitude;
                float t = len > 1e-4f ? Mathf.Clamp01(Vector3.Dot(position - a, ab) / (len * len)) : 0f;
                float d = (a + ab * t - position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    at = walked + len * t;
                    leg = i - 1;
                }
                walked += len;
            }
            return at;
        }

        public string Section(int leg) => leg >= 0 && leg < sections.Length ? sections[leg] : "-";

        // Distance along the path where each section starts, for the docs and the validator's notes.
        public float SectionStart(string section, int team)
        {
            var path = World(team);
            float walked = 0f;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                if (Section(i) == section) return walked;
                walked += Vector3.Distance(path[i], path[i + 1]);
            }
            return -1f;
        }

        void OnDrawGizmosSelected()
        {
            for (int team = 0; team < 2; team++)
            {
                Gizmos.color = team == 0 ? new Color(1f, .85f, .3f) : new Color(.5f, .3f, 1f);
                for (int i = 1; i < points.Length; i++)
                    Gizmos.DrawLine(Point(i - 1, team) + Vector3.up * .3f, Point(i, team) + Vector3.up * .3f);
            }
        }
    }
}
