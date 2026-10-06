using UnityEngine;

namespace ChessFight.PawnRush
{
    // One module of the course, on its root. The origin is the middle of the entry
    // edge on the top of the entry floor; +z is the way to the finish, +x the black
    // team's side. The assembler places the next module at
    // origin + (0, deltaY, length).
    [DisallowMultipleComponent]
    public sealed class CourseModule : MonoBehaviour
    {
        public ModuleId module;
        public string title = "";
        [Min(1)] public float length = 10;
        [Tooltip("Exit floor height minus entry floor height.")]
        public float deltaY;
        [Tooltip("Built for the white team (x < 0) and mirrored for black (TeamMirror).")]
        public bool isTeamModule;
        [Tooltip("The progress path in this module, module-local. Team modules: the WHITE lane; black is its mirror.")]
        public Vector3[] pathPoints = new Vector3[0];

        public void Configure(ModuleId module, string title, float length, float deltaY, bool isTeamModule, Vector3[] pathPoints)
        {
            this.module = module;
            this.title = title;
            this.length = length;
            this.deltaY = deltaY;
            this.isTeamModule = isTeamModule;
            this.pathPoints = pathPoints;
        }

        public string Code => module.ToString().Split('_')[0];

        // The path for a team, module-local. Shared modules give both teams the same one.
        public Vector3 PathPoint(int i, int team)
        {
            var p = pathPoints[i];
            if (isTeamModule && team == Gameplay.Teams.Black) p.x = -p.x;
            return p;
        }

        void OnDrawGizmosSelected()
        {
            if (pathPoints == null) return;
            for (int team = 0; team < (isTeamModule ? 2 : 1); team++)
            {
                Gizmos.color = team == 0 ? new Color(1f, .85f, .3f) : new Color(.5f, .3f, 1f);
                for (int i = 1; i < pathPoints.Length; i++)
                    Gizmos.DrawLine(transform.TransformPoint(PathPoint(i - 1, team)) + Vector3.up * .2f,
                                    transform.TransformPoint(PathPoint(i, team)) + Vector3.up * .2f);
            }
        }
    }
}
