using UnityEngine;
using UnityEngine.Serialization;

namespace ChessFight.ProtectKing
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SlidingWall : ObstacleMotion
    {
        [SerializeField] Vector3 axis = Vector3.right;
        [SerializeField, Tooltip("Offset in cycles.")] float phase;
        [SerializeField, Min(0f), Tooltip("Travel distance in metres; sine platforms use centre-to-end distance.")] float distance = 4f;
        [SerializeField, Min(0f), Tooltip("Peak movement speed in metres per second. Zero stops movement.")] float moveSpeed = 10.7711748f;
        [SerializeField, HideInInspector, FormerlySerializedAs("period")] float legacyPeriod;
        [SerializeField] bool useLocalTranslationAxes;
        [SerializeField, Min(0), InspectorName("Push Force"), Tooltip("Horizontal velocity added on impact (m/s).")] float strength = 9f;
        [SerializeField, Min(0)] float lift = 2f;
        [SerializeField, Min(.05f)] float hitCooldown = .35f;
        public override float KnockbackForce => strength;
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
            distance = Mathf.Max(0f, distance); moveSpeed = Mathf.Max(0f, moveSpeed);
        }
        void MigratePeriod()
        {
            if (legacyPeriod <= 0f) return;
            moveSpeed = Mathf.Abs(distance) * (2f * Mathf.PI * 2.14285714f) / Mathf.Max(.5f, legacyPeriod);
            legacyPeriod = 0f;
        }

        protected override Pose Evaluate(double time)
        {
            double cycles = distance > .0001f ? time * Mathf.Max(0f, moveSpeed) / (distance * 2.0 * System.Math.PI * 2.14285714) : 0.0;
            float wave = (float)System.Math.Sin((cycles + phase) % 1.0 * System.Math.PI * 2.0);
            wave = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.35f, .35f, wave));
            return new Pose(Origin + TranslationAxis(axis, useLocalTranslationAxes) * (distance * wave), RestRotation);
        }
    }
}
