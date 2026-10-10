using UnityEngine;

namespace ChessFight.Gameplay
{
    public sealed class KingRushOrbitPawn : KingRushToyMotion
    {
        [SerializeField] float radius = 2;
        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            double cycle = Cycle(time);
            float a = Fraction(cycle) * Mathf.PI * 2;
            position = StartPosition + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * radius;
            rotation = StartRotation * Quaternion.Euler(0, WrapDegrees(cycle * 360), 0);
        }
    }
}
