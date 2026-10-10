using UnityEngine;

namespace ChessFight.Gameplay
{
    public sealed class KingRushRollingHead : KingRushToyMotion
    {
        [SerializeField] Vector3 travel = new Vector3(0, -5.9f, -22);
        [SerializeField, Min(0)] float returnDepth = 8;
        [SerializeField] float rollDegreesPerCycle = -1080;
        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            double cycle = Cycle(time);
            float f = Fraction(cycle);
            position = StartPosition;
            // Return beneath the track, without a wrap sweep through runners.
            if (f < .65f) position += travel * (f / .65f);
            else if (f < .72f) position += travel + Vector3.down * (returnDepth * (f - .65f) / .07f);
            else if (f < .93f) position += travel * (1 - (f - .72f) / .21f) + Vector3.down * returnDepth;
            else position += Vector3.down * (returnDepth * (1 - f) / .07f);
            rotation = StartRotation * Quaternion.Euler(WrapDegrees(cycle * rollDegreesPerCycle), 0, 0);
        }
    }
}
