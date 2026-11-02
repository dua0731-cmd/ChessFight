using UnityEngine;

namespace ChessFight.ProtectKing
{
    // Tactical markers slide as one formation, preserving the authored passage between them.
    // Position is evaluated from the shared clock so adjacent rows can keep a fixed phase offset.
    public sealed class TroopFormationSlide : ObstacleMotion
    {
        [SerializeField, Min(0), Tooltip("Maximum displacement from the authored centre, in metres.")]
        float travelDistance = 3;
        [SerializeField, Min(1), Tooltip("Seconds for a full left-right-left cycle.")]
        float period = 6;
        [SerializeField, Tooltip("Cycle offset: 0.5 places this formation opposite the first row.")]
        float phaseOffset;
        [SerializeField, Min(0)] float pushForce = 7;
        [SerializeField, Min(0)] float upwardForce = 1;

        public override float KnockbackForce => Mathf.Max(0, pushForce);
        public override float UpwardForce => Mathf.Max(0, upwardForce);

        protected override Pose Evaluate(double time) => new Pose(
            Origin + RestRotation * Vector3.right *
                (Wave(time, Mathf.Max(1, period), phaseOffset) * Mathf.Max(0, travelDistance)),
            RestRotation);
    }
}
