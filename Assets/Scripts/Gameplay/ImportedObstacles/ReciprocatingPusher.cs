using UnityEngine;
using UnityEngine.Serialization;

namespace ChessFight.ProtectKing
{
    public sealed class ReciprocatingPusher : ObstacleMotion
    {
        [SerializeField] Vector3 axis = Vector3.right;
        [SerializeField, Tooltip("Offset in cycles.")] float phase;
        [SerializeField, Min(0f), Tooltip("Distance from the centre to either end of the travel (metres). Everyone uses the same smooth sine motion.")] float distance = 4f;
        [SerializeField, Min(0f), Tooltip("Peak movement speed in metres per second. Zero stops movement.")] float moveSpeed = 5.026548f;
        [SerializeField, HideInInspector, FormerlySerializedAs("period")] float legacyPeriod;
        [SerializeField] bool useLocalTranslationAxes;
        [SerializeField, Min(0f), FormerlySerializedAs("strength"), Tooltip("Horizontal velocity added on impact (m/s). Independent of movement speed.")] float pushForce = 9f;
        [SerializeField, Min(0)] float lift = 2f;
        [SerializeField, Min(.05f)] float hitCooldown = .35f;
        public override float KnockbackForce => pushForce;
        public override float UpwardForce => lift;
        public override float HitCooldown => hitCooldown;

        protected override void Awake()
        {
            MigratePeriod();
            base.Awake();
        }
        void OnValidate()
        {
            MigratePeriod();
            moveSpeed = Mathf.Max(0f, moveSpeed);
            distance = Mathf.Max(0f, distance);
            pushForce = Mathf.Max(0f, pushForce);
        }
        void MigratePeriod()
        {
            if (legacyPeriod <= 0f) return;
            moveSpeed = Mathf.Abs(distance) * 2f * Mathf.PI / Mathf.Max(.5f, legacyPeriod);
            legacyPeriod = 0f;
        }

        protected override Pose Evaluate(double time)
        {
            // dx/dt peaks at Move Speed. Evaluate directly from the shared clock, without accumulation.
            double cycles = distance > .0001f ? time * Mathf.Max(0f, moveSpeed) / (distance * 2.0 * System.Math.PI) : 0.0;
            float wave = (float)System.Math.Sin((cycles + phase) % 1.0 * System.Math.PI * 2.0);
            return new Pose(Origin + TranslationAxis(axis, useLocalTranslationAxes) * (distance * wave), RestRotation);
        }
    }
}
