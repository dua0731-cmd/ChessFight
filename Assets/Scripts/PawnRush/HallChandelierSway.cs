using UnityEngine;

namespace ChessFight.PawnRush
{
    // R330 (승규 11-03, "흔들리는 애니메이션만 넣어"): the round hall's hub chandelier sways gently on its chain, about the
    // chain's top (this transform), in two slow swings at odd periods so the path is an ellipse that never quite repeats.
    // A picture only - nothing walks on it or is hit by it - so it reads Time.time rather than the obstacle clock.
    public sealed class HallChandelierSway : MonoBehaviour
    {
        [Tooltip("Degrees either way, across the first and the second axis.")]
        public float AmplitudeA = 2.2f, AmplitudeB = 1.4f;
        [Tooltip("Seconds per swing on each axis.")]
        public float PeriodA = 6.5f, PeriodB = 8.9f;

        Quaternion rest;

        void Awake() => rest = transform.localRotation;

        void Update()
        {
            float t = Time.time;
            float a = AmplitudeA * Mathf.Sin(2f * Mathf.PI * t / PeriodA);
            float b = AmplitudeB * Mathf.Sin(2f * Mathf.PI * t / PeriodB + 1.1f);
            transform.localRotation = rest * Quaternion.Euler(a, 0f, b);
        }
    }
}
