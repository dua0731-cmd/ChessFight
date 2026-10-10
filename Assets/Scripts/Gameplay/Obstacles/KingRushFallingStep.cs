using UnityEngine;

namespace ChessFight.Gameplay
{
    public sealed class KingRushFallingStep : KingRushSkyMotion
    {
        [SerializeField, Min(0)] float fallDistance = 12;
        [SerializeField, Min(0)] float fallAcceleration = 8;
        [SerializeField, Min(0)] float warningAmplitude = .035f;
        protected override float ActiveUntil => 4;
        protected override bool WarningAt(float cycle) => cycle >= 3 && cycle < 4;
        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            float cycle = Cycle(time, offset);
            position = StartPosition; rotation = StartRotation;
            if (cycle >= 4) position += Vector3.down * Mathf.Min(fallDistance, (cycle - 4) * (cycle - 4) * fallAcceleration);
            else if (cycle >= 3) position += Vector3.up * Mathf.Sin(cycle * 40) * warningAmplitude;
        }
    }
}
