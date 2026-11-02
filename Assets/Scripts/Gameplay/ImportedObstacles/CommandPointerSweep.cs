using UnityEngine;

namespace ChessFight.ProtectKing
{
    // Opposing pointers open a gap, then sweep it shut on the shared obstacle clock.
    public sealed class CommandPointerSweep : ObstacleMotion
    {
        [SerializeField, Range(0, 85)] float swingAngle = 55;
        [SerializeField, Min(1)] float period = 6;
        [SerializeField] float phaseOffset;
        [SerializeField, Min(0)] float pushForce = 8;
        [SerializeField, Min(0)] float upwardForce = 1;
        public override float KnockbackForce => Mathf.Max(0, pushForce);
        public override float UpwardForce => Mathf.Max(0, upwardForce);
        protected override Pose Evaluate(double time) => new Pose(Origin,
            RestRotation * Quaternion.AngleAxis(Wave(time, Mathf.Max(1, period), phaseOffset) * swingAngle, Vector3.up));
    }
}
