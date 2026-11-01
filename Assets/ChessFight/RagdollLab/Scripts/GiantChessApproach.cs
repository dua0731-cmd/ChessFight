using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    // Optional saved course before the chess board. The existing controller still owns all pawns.
    public sealed class GiantChessApproach : MonoBehaviour
    {
        [SerializeField] CourseModule course;
        readonly int[] checkpoints = new int[12];
        readonly int[] routeIndices = new int[12];
        readonly bool[] entered = new bool[12];
        public bool Available => isActiveAndEnabled && course != null && course.Entry != null && course.Exit != null;
        public bool ChessStarted { get; private set; }
        public bool Entered(int index) => !Available || entered[index];
        public int Checkpoint(int index) => checkpoints[index];
        public int CheckpointCount => course != null && course.Checkpoints != null ? course.Checkpoints.Length : 0;

        public void ResetProgress()
        {
            ChessStarted = false;
            for (int i = 0; i < entered.Length; i++)
            { checkpoints[i] = 0; routeIndices[i] = 1; entered[i] = false; }
        }
        Transform Recovery(int index) => checkpoints[index] == 0 ? course.Entry : course.Checkpoints[checkpoints[index] - 1];
        public Vector3 Spawn(int index)
        {
            var point = Recovery(index);
            // Twelve bodies fit inside the 12m-wide path and 6m-deep checkpoint terrace.
            return point.TransformPoint(new Vector3((index % 4 - 1.5f) * 2, 0, (index / 4 - 1) * 1.6f));
        }
        public Vector3 Facing(int index) => Recovery(index).forward;
        public void RecoverRoute(int index)
        {
            if (!Available || entered[index] || course.Route == null) return;
            var point = Recovery(index).position;
            float closest = float.PositiveInfinity;
            for (int i = 0; i < course.Route.Length; i++)
                if (course.Route[i] != null)
                {
                    float distance = (course.Route[i].position - point).sqrMagnitude;
                    if (distance < closest) { closest = distance; routeIndices[index] = i + 1; }
                }
        }
        bool Near(Transform target, Vector3 position)
        {
            var p = target.InverseTransformPoint(position);
            return Mathf.Abs(p.x) < course.LaneWidth * .5f && Mathf.Abs(p.z) < 2.5f && p.y > -.2f && p.y < 3;
        }
        public void Observe(int index, RagdollPawn pawn)
        {
            if (!Available || entered[index]) return;
            if (checkpoints[index] < CheckpointCount && Near(course.Checkpoints[checkpoints[index]], pawn.Hips.position))
                checkpoints[index]++;
            if (checkpoints[index] == CheckpointCount && Near(course.Exit, pawn.Hips.position))
            { entered[index] = true; ChessStarted = true; }
        }
        public void DriveBot(int index, RagdollPawn pawn)
        {
            var route = course.Route;
            if (route == null || route.Length == 0) return;
            int next = Mathf.Clamp(routeIndices[index], 0, route.Length - 1);
            if (route[next] == null) return;
            var delta = Vector3.ProjectOnPlane(route[next].position - pawn.Hips.position, Vector3.up);
            if (delta.magnitude < 2 && next < route.Length - 1)
            { routeIndices[index] = ++next; delta = Vector3.ProjectOnPlane(route[next].position - pawn.Hips.position, Vector3.up); }
            Vector3 direction = delta.normalized;
            // Basic test navigation: wait for moving decks instead of intentionally walking into gaps.
            Vector3 feet = pawn.Hips.position - Vector3.up * pawn.standHeight;
            if (!Physics.Raycast(feet + direction * 1.4f + Vector3.up * 4, Vector3.down, out var hit, 6,
                    ~0, QueryTriggerInteraction.Ignore) || hit.point.y - feet.y > .6f || feet.y - hit.point.y > 1.5f)
                direction = Vector3.zero;
            pawn.SetInput(new PawnInput { move = direction, aim = delta.sqrMagnitude > .01f ? delta.normalized : pawn.Facing });
        }
    }
}
