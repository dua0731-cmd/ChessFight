using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The royal water garden's lock floor (v31 beat 4): water running over it carries whoever stands in it, 1.5 m/s
    // south by default. Like the grand stair hall's wind it is added to the frame the pawn runs in
    // (ImportedObstacleActor.Blow), so running against it gains 1.0 m/s and sprinting 2.1; a pawn that is down is
    // pushed along instead. The authority's (ObstacleClock.Authority). The box is this object's BoxCollider (a trigger).
    [RequireComponent(typeof(BoxCollider))]
    public sealed class WaterCurrent : MonoBehaviour
    {
        [Tooltip("Local velocity of the water, m/s.")]
        [SerializeField] Vector3 flow = new Vector3(0f, 0f, -1.5f);
        [Tooltip("How hard it drags a pawn that cannot be carried (down), m/s².")]
        [SerializeField] float drag = 6f;

        readonly Collider[] overlaps = new Collider[64];
        readonly HashSet<ImportedObstacleActor> visited = new HashSet<ImportedObstacleActor>();
        BoxCollider box;

        public Vector3 Flow => flow;

        // For the builder: on an inactive object, before it wakes.
        public void Configure(Vector3 localFlow) => flow = localFlow;

        void Awake()
        {
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        void FixedUpdate()
        {
            if (!ObstacleClock.Authority || box == null || flow.sqrMagnitude < 1e-4f) return;
            var s = transform.lossyScale;
            int n = Physics.OverlapBoxNonAlloc(transform.TransformPoint(box.center),
                Vector3.Scale(box.size * .5f, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z))), overlaps,
                transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            visited.Clear();
            var world = transform.TransformDirection(flow);
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor == null || !actor.CanReceiveObstacle || !visited.Add(actor)) continue;
                if (actor.Blow(world)) continue;
                actor.Accelerate(world.normalized * drag, world.magnitude, Time.fixedDeltaTime);
            }
        }
    }
}
