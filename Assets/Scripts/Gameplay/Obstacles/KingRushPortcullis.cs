using UnityEngine;

namespace ChessFight.Gameplay
{
    public sealed class KingRushPortcullis : KingRushCastleMotion
    {
        [SerializeField] float height = 6;
        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            float wave = CycleWave(time);
            position = StartPosition; rotation = StartRotation;
            position += Vector3.up * (height * Mathf.Clamp01(wave * 1.5f + .5f));
        }
    }
}
