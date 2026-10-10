using UnityEngine;

namespace ChessFight.Gameplay
{
    public sealed class KingRushCastleWall : KingRushCastleMotion
    {
        [SerializeField] float distance = 3.3f;
        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            float wave = CycleWave(time);
            position = StartPosition; rotation = StartRotation;
            position += Vector3.right * (distance * wave);
        }
    }
}
