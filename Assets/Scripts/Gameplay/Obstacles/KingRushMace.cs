using UnityEngine;

namespace ChessFight.Gameplay
{
    public sealed class KingRushMace : KingRushCastleMotion
    {
        [SerializeField] float angle = 55;
        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            float wave = CycleWave(time);
            position = StartPosition; rotation = StartRotation;
            rotation *= Quaternion.Euler(0, 0, wave * angle);
        }
    }
}
