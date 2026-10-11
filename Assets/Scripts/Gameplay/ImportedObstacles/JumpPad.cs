using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.ProtectKing
{
    public sealed class JumpPad : ObstacleSurface
    {
        [SerializeField, Min(0), Tooltip("Target rise in metres above takeoff, using current gravity.")]
        float jumpHeight = 10f;
        [SerializeField, Min(0f), Tooltip("Maximum horizontal speed requested by movement input while airborne (m/s).")]
        float airMoveSpeed = 6f;
        [SerializeField, Min(0f), Tooltip("Horizontal acceleration and braking from movement input (m/s squared). Releasing input keeps momentum.")]
        float airAcceleration = 12f;
        [SerializeField, Min(.1f)] float launchCooldown = .7f;
        readonly Dictionary<ImportedObstacleActor, float> lastLaunch = new Dictionary<ImportedObstacleActor, float>();
        readonly Collider[] occupants = new Collider[512];
        readonly HashSet<ImportedObstacleActor> visited = new HashSet<ImportedObstacleActor>();
        BoxCollider pad;

        void FixedUpdate()
        {
            if (!Running || (context != null && !context.Authority)) return;
            if (pad == null) pad = GetComponentInChildren<BoxCollider>();
            if (pad == null) return;
            var box = pad.bounds;
            int count = Physics.OverlapBoxNonAlloc(box.center + Vector3.up * (box.extents.y + .25f),
                new Vector3(box.extents.x, .35f, box.extents.z), occupants, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            visited.Clear();
            for (int i = 0; i < count; i++)
            {
                var actor = ImportedObstacleActor.Find(occupants[i]);
                if (actor != null && actor.CanReceiveObstacle && actor.Position.y >= box.max.y - .2f &&
                    actor.Position.y <= box.max.y + .4f && visited.Add(actor)) Launch(actor);
            }
        }
        public void Launch(ImportedObstacleActor actor)
        {
            if (!Running || !actor.CanReceiveObstacle || (context != null && !context.Authority)) return;
            float time = context != null ? context.Elapsed : Time.time;
            if (lastLaunch.TryGetValue(actor, out float previous) && time >= previous && time - previous < launchCooldown) return;
            lastLaunch[actor] = time;
            float rise = Mathf.Sqrt(2f * Mathf.Max(0f, -Physics.gravity.y) * Mathf.Max(0f, jumpHeight));
            actor.LaunchWithAirControl(rise, airMoveSpeed, airAcceleration);
        }
        void OnDisable() { lastLaunch.Clear(); }
    }
}
