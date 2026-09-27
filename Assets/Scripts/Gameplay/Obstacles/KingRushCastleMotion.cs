using UnityEngine;

namespace ChessFight.Gameplay
{
    public sealed class KingRushCastleMotion : Obstacle
    {
        public enum Kind { Ferry, SlidingWall, Drawbridge, Portcullis, Mace }
        public Kind kind;
        public float period = 8;
        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            float wave = Wave(time, period);
            position = StartPosition; rotation = StartRotation;
            switch (kind)
            {
                case Kind.Ferry: position += Vector3.forward * (6 * wave); break;
                case Kind.SlidingWall: position += Vector3.right * (3.3f * wave); break;
                case Kind.Drawbridge: rotation *= Quaternion.Euler(-65 * Mathf.Max(0, wave), 0, 0); break;
                case Kind.Portcullis: position += Vector3.up * (6 * Mathf.Clamp01(wave * 1.5f + .5f)); break;
                case Kind.Mace: rotation *= Quaternion.Euler(0, 0, wave * 55); break;
            }
        }
    }
}
