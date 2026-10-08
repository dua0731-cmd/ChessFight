using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The course root in PawnRush_Course01.unity (course 01 v0.4: one shared road and two mission
    // plazas). It sits at the world origin and holds one child, Course01v4_Root, with every section
    // placed at the design doc's world coordinates (Course01v4Builder).
    //
    // The menu ChessFight > Pawn Rush > Build Course01 v4 builds it in the Editor (the scene keeps
    // the result). A scene that has never been built (fresh from git) builds it from code when
    // Play starts, so it can be walked at once.
    [DefaultExecutionOrder(-500)]
    public sealed class PawnRushCourse : MonoBehaviour
    {
        public const string RootName = "Course01v4_Root";

        [SerializeField] Course01Kit kit;
        [Tooltip("Build the course from code on Play when the scene does not hold it yet.")]
        [SerializeField] bool buildOnPlayIfEmpty = true;

        readonly Dictionary<int, List<Vector3>> paths = new Dictionary<int, List<Vector3>>();

        public Course01Kit Kit => kit;
        public static PawnRushCourse Current { get; private set; }
        public Transform Root => transform.Find(RootName);
        public ProgressPath Progress => GetComponentInChildren<ProgressPath>(true);
        public MissionPicker Missions => GetComponentInChildren<MissionPicker>(true);

        void Awake()
        {
            Current = this;
            if (buildOnPlayIfEmpty && Root == null && kit != null) Course01v4Builder.Build(transform, kit);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public void Clear()
        {
            paths.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        // The whole progress path for a team, world space.
        public List<Vector3> Path(int team)
        {
            if (paths.TryGetValue(team, out var path) && path.Count > 0) return path;
            var progress = Progress;
            path = progress != null ? progress.World(team) : new List<Vector3>();
            paths[team] = path;
            return path;
        }

        // Metres along the team's path, and the section (S+A, M1, B, M2, C, T) the point is in.
        public float Along(Vector3 position, int team, out string section)
        {
            var path = Path(team);
            float along = ProgressPath.Along(path, position, out int leg);
            var progress = Progress;
            section = progress != null ? progress.Section(leg) : "-";
            return along;
        }
    }
}
