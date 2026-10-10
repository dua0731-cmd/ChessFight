using UnityEngine;

namespace ChessFight.ProtectKing
{
    public sealed class RotatingHammer : ObstacleMotion
    {
        [SerializeField] Vector3 axis = Vector3.right;
        [SerializeField, Tooltip("Offset in cycles.")] float phase;
        [SerializeField, Tooltip("Degrees per second.")] float degreesPerSecond = 60f;
        [SerializeField, Min(0)] float strength = 9f;
        [SerializeField, Min(0)] float lift = 2f;
        [SerializeField, Min(.05f)] float hitCooldown = .35f;
        public override float KnockbackForce => strength;
        public override float UpwardForce => lift;
        public override float HitCooldown => hitCooldown;

        protected override Pose Evaluate(double time)
        {
            var unit = axis.sqrMagnitude > .001f ? axis.normalized : Vector3.right;
            return new Pose(Origin, RestRotation * Quaternion.AngleAxis((float)((time * degreesPerSecond + phase * 360) % 360), unit));
        }
    }
}
