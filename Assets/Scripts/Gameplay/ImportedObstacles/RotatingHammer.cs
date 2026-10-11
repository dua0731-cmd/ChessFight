using UnityEngine;
using UnityEngine.Serialization;

namespace ChessFight.ProtectKing
{
    public sealed class RotatingHammer : ObstacleMotion
    {
        [SerializeField, HideInInspector] Rigidbody movingBody;
        protected override Rigidbody MotionBody => movingBody != null ? movingBody : base.MotionBody;

        [SerializeField] Vector3 axis = Vector3.right;
        [SerializeField, Tooltip("Offset in cycles.")] float phase;
        [SerializeField, FormerlySerializedAs("degreesPerSecond"), Tooltip("Degrees per second. Zero stops rotation; a negative value reverses it.")] float rotationSpeed = 60f;
        [SerializeField, Min(0f), FormerlySerializedAs("strength"), Tooltip("Horizontal velocity added on impact (m/s). Independent of rotation speed.")] float pushForce = 9f;
        [SerializeField, Min(0)] float lift = 2f;
        [SerializeField, Min(.05f)] float hitCooldown = .35f;
        public override float KnockbackForce => pushForce;
        public override float UpwardForce => lift;
        public override float HitCooldown => hitCooldown;

        protected override Pose Evaluate(double time)
        {
            var unit = axis.sqrMagnitude > .001f ? axis.normalized : Vector3.right;
            return new Pose(Origin, RestRotation * Quaternion.AngleAxis((float)((time * rotationSpeed + phase * 360) % 360), unit));
        }
    }
}
