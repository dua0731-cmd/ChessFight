using UnityEngine;

namespace ChessFight.Gameplay
{
    public sealed class KingRushClockButton : KingRushToyMotion
    {
        [SerializeField] float amplitude = .4f;
        [SerializeField] float verticalOffset = -.25f;
        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            float f = Fraction(Cycle(time));
            position = StartPosition + Vector3.up * (amplitude * Mathf.Cos(f * Mathf.PI * 2) + verticalOffset);
            rotation = StartRotation;
        }
    }
}
