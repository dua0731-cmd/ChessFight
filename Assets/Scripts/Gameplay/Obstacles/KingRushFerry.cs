using UnityEngine;

namespace ChessFight.Gameplay
{
    public sealed class KingRushFerry : KingRushCastleMotion
    {
        [SerializeField] float distance = 6;
        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            float wave = CycleWave(time);
            position = StartPosition; rotation = StartRotation;
            position += Vector3.forward * (distance * wave);
        }
    }
}
