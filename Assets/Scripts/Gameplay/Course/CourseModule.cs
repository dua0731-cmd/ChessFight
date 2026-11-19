using UnityEngine;

namespace ChessFight.Gameplay
{
    // Saved authoring data, independent of a game mode or a scene singleton.
    [DisallowMultipleComponent]
    public sealed class CourseModule : MonoBehaviour
    {
        [SerializeField] string courseId;
        [SerializeField] string displayName;
        [SerializeField] Transform entry;
        [SerializeField] Transform exit;
        [SerializeField] Transform[] checkpoints;
        [SerializeField] Transform[] route;
        [SerializeField] Transform minigameSlot;
        [SerializeField] Bounds placementBounds;
        [SerializeField, Min(1)] float targetSeconds = 240;
        [SerializeField, Min(1)] float laneWidth = 12;

        public string CourseId => courseId;
        public string DisplayName => displayName;
        public Transform Entry => entry;
        public Transform Exit => exit;
        public Transform[] Checkpoints => checkpoints;
        public Transform[] Route => route;
        public Transform MinigameSlot => minigameSlot;
        public Bounds PlacementBounds => placementBounds;
        public float TargetSeconds => targetSeconds;
        public float LaneWidth => laneWidth;

        // Both connectors point along travel. The entire module keeps unit scale.
        public void AlignEntryTo(Transform previousExit)
        {
            if (entry == null || previousExit == null || previousExit.IsChildOf(transform)) return;
            transform.rotation = previousExit.rotation * Quaternion.Inverse(entry.rotation) * transform.rotation;
            transform.position += previousExit.position - entry.position;
        }

        public float RouteLength
        {
            get
            {
                float length = 0;
                if (route != null)
                    for (int i = 1; i < route.Length; i++)
                        if (route[i - 1] != null && route[i] != null)
                            length += Vector3.Distance(route[i - 1].position, route[i].position);
                return length;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.3f, .85f, 1f, .7f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(placementBounds.center, placementBounds.size);
            Gizmos.matrix = Matrix4x4.identity;
            if (route != null)
                for (int i = 1; i < route.Length; i++)
                    if (route[i - 1] != null && route[i] != null)
                        Gizmos.DrawLine(route[i - 1].position, route[i].position);
            DrawConnector(entry, Color.green);
            DrawConnector(exit, Color.yellow);
        }

        static void DrawConnector(Transform point, Color color)
        {
            if (point == null) return;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(point.position, 1);
            Gizmos.DrawLine(point.position, point.position + point.forward * 4);
        }
    }
}
