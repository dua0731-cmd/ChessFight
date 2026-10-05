using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.ProtectKing
{
    // A small trigger around a moving head also hits stationary CharacterControllers.
    public sealed class ObstacleImpact : MonoBehaviour
    {
        public ObstacleMotion motion;
        [Tooltip("Optional pendulum motion. Its inspector owns force and lift when assigned.")]
        public PendulumSwing pendulum;
        public ObstacleContext context;
        [Min(0)] public float strength = 9;
        [Min(0)] public float lift = 2;
        [Min(.05f)] public float hitCooldown = .35f;
        readonly Dictionary<ImportedObstacleActor, float> hitTimes = new Dictionary<ImportedObstacleActor, float>();
        void Awake()
        {
            if (context == null) context = GetComponentInParent<ObstacleContext>();
            if (motion == null) motion = GetComponent<ObstacleMotion>();
        }
        void OnTriggerStay(Collider other)
        {
            var motor = ImportedObstacleActor.Find(other);
            if (motor != null) Hit(motor, motor.Position - transform.position);
        }
        public bool Hit(ImportedObstacleActor motor, Vector3 fallbackDirection)
        {
            if (!isActiveAndEnabled || !motor.CanReceiveObstacle || (motion == null && pendulum == null) ||
                (context != null && (!context.Running || !context.Authority))) return false;
            if (pendulum != null && !pendulum.CanHit) return false;
            var velocity = pendulum != null ? pendulum.BallVelocity : motion.PointVelocity(motor.Position + Vector3.up);
            if (velocity.sqrMagnitude < .1f) return false;
            float t = context != null ? context.Elapsed : Time.time;
            if (hitTimes.TryGetValue(motor, out float previous) && t >= previous && t - previous < hitCooldown) return false;
            hitTimes[motor] = t;
            var outward = Vector3.ProjectOnPlane(velocity, Vector3.up);
            if (outward.sqrMagnitude < .01f) outward = Vector3.ProjectOnPlane(fallbackDirection, Vector3.up);
            if (outward.sqrMagnitude < .01f) outward = transform.forward;
            motor.AddObstacleImpulse(outward.normalized * (pendulum != null ? pendulum.KnockbackForce : strength) +
                Vector3.up * (pendulum != null ? pendulum.UpwardForce : lift));
            return true;
        }
        void OnDisable() { hitTimes.Clear(); }
    }
}
