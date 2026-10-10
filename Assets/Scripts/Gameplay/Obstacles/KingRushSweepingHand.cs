using UnityEngine;

namespace ChessFight.Gameplay
{
    public sealed class KingRushSweepingHand : KingRushSkyMotion
    {
        [SerializeField] GameObject shadow;
        [SerializeField] float halfTravel = 16;
        [SerializeField] float withdrawnHeight = 10;
        public void ConfigureShadow(GameObject value) => shadow = value;
        protected override float ActiveUntil => 3.5f;
        protected override void ShowVisuals(float cycle)
        {
            if (shadow != null) shadow.SetActive(cycle < 3.5f);
        }
        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            float cycle = Cycle(time, offset);
            float across = cycle < 1.5f ? -halfTravel : cycle < 3.5f ? Mathf.Lerp(-halfTravel, halfTravel, (cycle - 1.5f) / 2) : halfTravel;
            position = StartPosition + new Vector3(across, cycle >= 3.5f ? withdrawnHeight : 0, 0);
            rotation = StartRotation;
        }
    }
}
