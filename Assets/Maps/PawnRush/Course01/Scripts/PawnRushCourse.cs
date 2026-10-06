using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush
{
    // The course root in PawnRush_Course01.unity. It lays the layout's modules end to
    // end: M0's origin is (0, 0, -14) and each next one starts at
    // origin + (0, deltaY, length) of the one before (design doc §6), so switching an
    // extension on or off moves everything behind it and nothing is placed by hand.
    //
    // The "ChessFight > Pawn Rush > Build Course01" menu assembles it in the Editor
    // from baked module prefabs. A scene that has never been built (fresh from git)
    // assembles itself from code when Play starts, so it can be walked at once.
    [DefaultExecutionOrder(-500)]
    public sealed class PawnRushCourse : MonoBehaviour
    {
        public static readonly Vector3 FirstOrigin = new Vector3(0f, 0f, -14f);

        [SerializeField] Course01Layout layout;
        [SerializeField] Course01Kit kit;
        [Tooltip("Build the course from code on Play when the scene holds no modules yet.")]
        [SerializeField] bool buildOnPlayIfEmpty = true;

        readonly List<CourseModule> modules = new List<CourseModule>();

        public Course01Layout Layout => layout;
        public Course01Kit Kit => kit;
        public static PawnRushCourse Current { get; private set; }

        // The placed modules, in course order.
        public IReadOnlyList<CourseModule> Modules
        {
            get
            {
                modules.Clear();
                foreach (Transform child in transform)
                {
                    var m = child.GetComponent<CourseModule>();
                    if (m != null && child.gameObject.activeSelf) modules.Add(m);
                }
                return modules;
            }
        }

        void Awake()
        {
            Current = this;
            if (buildOnPlayIfEmpty && Modules.Count == 0 && layout != null && kit != null)
                Assemble((entry, origin) => entry.prefab != null
                    ? Object.Instantiate(entry.prefab, origin, Quaternion.identity, transform)
                    : Course01Modules.Build(entry.module, kit, transform, origin));
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        // Places every enabled module in order. `make` returns the module's root, already at `origin`
        // (world) and under this transform; its CourseModule says where the next one goes.
        public void Assemble(Func<Course01Layout.Entry, Vector3, GameObject> make)
        {
            Vector3 origin = transform.TransformPoint(FirstOrigin);
            foreach (var entry in layout.entries)
            {
                if (!entry.enabled) continue;
                var go = make(entry, origin);
                if (go == null) continue;
                go.transform.SetParent(transform, true);
                go.transform.position = origin;
                go.transform.rotation = transform.rotation;
                var module = go.GetComponent<CourseModule>();
                if (module == null)
                {
                    Debug.LogError("[PawnRush] " + go.name + " has no CourseModule; the modules after it are not placed.");
                    return;
                }
                origin += transform.rotation * new Vector3(0f, module.deltaY, module.length);
            }
        }

        // ------------------------------------------------------------------ progress path

        // The whole course's progress path for a team, world space (design doc §6: the pathPoints of
        // every module joined; the judge's third tiebreak, total progress).
        public List<Vector3> Path(int team)
        {
            var path = new List<Vector3>();
            foreach (var m in Modules)
                for (int i = 0; i < m.pathPoints.Length; i++)
                {
                    var p = m.transform.TransformPoint(m.PathPoint(i, team));
                    if (path.Count == 0 || (path[path.Count - 1] - p).sqrMagnitude > 1e-6f) path.Add(p);
                }
            return path;
        }

        public static float Length(List<Vector3> path)
        {
            float total = 0f;
            for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
            return total;
        }

        // Metres along the path to the point nearest `position`.
        public static float Progress(List<Vector3> path, Vector3 position)
        {
            float best = float.MaxValue, at = 0f, walked = 0f;
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
                }
                walked += len;
            }
            return at;
        }

        // The module whose z range holds a world point (modules run along the course's +z).
        public CourseModule ModuleAt(Vector3 position)
        {
            foreach (var m in Modules)
            {
                float z = m.transform.InverseTransformPoint(position).z;
                if (z >= 0f && z < m.length) return m;
            }
            return null;
        }

        // Rank from height: 2 at the start, +1 for each step (design doc §3).
        public static int RankAt(float heightAboveStart) =>
            heightAboveStart < 1.5f ? 2 : heightAboveStart < 4.5f ? 3 : heightAboveStart < 7.5f ? 4
            : heightAboveStart < 10.5f ? 5 : heightAboveStart < 15f ? 6 : heightAboveStart < 21f ? 7 : 8;
    }
}
