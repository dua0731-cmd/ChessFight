using UnityEngine;
using UnityEngine.Serialization;

namespace ChessFight.ProtectKing
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RisingFallingTiles : ObstacleMotion
    {
        [SerializeField] Vector3 axis = Vector3.right;
        [SerializeField, Tooltip("Offset in cycles.")] float phase;
        [SerializeField, Min(0f), Tooltip("Travel distance in metres; sine platforms use centre-to-end distance.")] float distance = 4f;
        [SerializeField, Min(0f), Tooltip("Peak movement speed in metres per second. Zero stops movement.")] float moveSpeed = 5.02654825f;
        [SerializeField, HideInInspector, FormerlySerializedAs("period")] float legacyPeriod;
        [SerializeField] bool useLocalTranslationAxes;

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
            moveSpeed = Mathf.Abs(distance) * (2f * Mathf.PI * 1f) / Mathf.Max(.5f, legacyPeriod);
            legacyPeriod = 0f;
        }

        protected override Pose Evaluate(double time)
        {
            double cycles = distance > .0001f ? time * Mathf.Max(0f, moveSpeed) / (distance * 2.0 * System.Math.PI * 1) : 0.0;
            float wave = (float)System.Math.Sin((cycles + phase) % 1.0 * System.Math.PI * 2.0);
            return new Pose(Origin + TranslationAxis(axis, useLocalTranslationAxes) * (distance * wave), RestRotation);
        }
    }
}
