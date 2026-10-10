using UnityEngine;

namespace ChessFight.Gameplay
{
    public sealed class KingRushDrawbridge : KingRushCastleMotion
    {
        [SerializeField] float angle = 65;
        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            float wave = CycleWave(time);
            position = StartPosition; rotation = StartRotation;
            rotation *= Quaternion.Euler(-angle * Mathf.Max(0, wave), 0, 0);
        }
    }
}
