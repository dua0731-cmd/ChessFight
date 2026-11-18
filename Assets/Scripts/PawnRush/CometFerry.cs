using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Observatory's comet (R720, obstacle plan v0.1 #3): a platform that shuttles across the Big Dipper's
    // bowl, the short way from its fourth star to its seventh. Its cycle:
    //   wait at the near dock -> cross -> wait at the far dock -> cross back
    // a pure function of the shared obstacle clock, as every obstacle is; who stands on it rides it. `travel` is
    // from where it was built to the far dock, in its parent's frame.
    public sealed class CometFerry : Obstacle
    {
        [SerializeField] Vector3 travel = new Vector3(0f, 0f, 5f);
        [SerializeField] float dwell = 1.5f, cross = 3.5f;

        Vector3 worldTravel;

        public float Cycle => 2f * (dwell + cross);

        // Call it on an inactive object (the builder's root is switched on at the end).
        public void Configure(Vector3 travel, float dwell, float cross, float phaseSeconds)
        {
            this.travel = travel;
            this.dwell = Mathf.Max(0f, dwell);
            this.cross = Mathf.Max(.1f, cross);
            SetPhase(phaseSeconds);
        }

        protected override void Awake()
        {
            // Before the first pose is worked out (the base's Awake asks for it).
            worldTravel = transform.parent != null ? transform.parent.TransformVector(travel) : travel;
            base.Awake();
        }

        // Where it is between its docks at a time: 0 the near one, 1 the far one.
        public float Across(double time)
        {
            double cycle = Cycle;
            double wrapped = time % cycle;
            float t = (float)(wrapped < 0 ? wrapped + cycle : wrapped);
            if (t < dwell) return 0f;
            t -= dwell;
            if (t < cross) return Mathf.SmoothStep(0f, 1f, t / cross);
            t -= cross;
            if (t < dwell) return 1f;
            t -= dwell;
            return 1f - Mathf.SmoothStep(0f, 1f, t / cross);
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = StartRotation;
            position = StartPosition + worldTravel * Across(time);
        }
    }
}
