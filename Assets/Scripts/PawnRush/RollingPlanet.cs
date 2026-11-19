using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Observatory's rolling planet (R724): the short way across the Big Dipper's bowl is one ball that
    // goes to and fro between two stars, rolling as it goes - as far round as a ball rolling that far along the
    // ground turns, its top running the way it travels. A pawn rides its top the way a log is ridden on water: the
    // ball carries what stands on it forward off its crest, so the rider keeps walking back against the roll to
    // stay on top, and the other way when the ball comes back.
    //   wait at the near dock (still) -> cross, rolling -> wait at the far dock (still) -> cross back, rolling back
    // Both where it is and how far it has turned are a pure function of the shared obstacle clock, as every
    // obstacle's pose is. `travel` is from where it was built to the far dock, in its parent's frame.
    public sealed class RollingPlanet : Obstacle
    {
        [SerializeField] Vector3 travel = new Vector3(0f, 0f, 8f);
        [SerializeField] float radius = 2.2f;
        [Tooltip("Seconds it stands at a dock, seconds a crossing takes, and seconds of that it speeds up and slows down.")]
        [SerializeField] float dwell = 1.2f;
        [SerializeField] float cross = 6f, ease = .6f;

        Vector3 worldTravel, axis = Vector3.right;
        float length;

        public float Cycle => 2f * (dwell + cross);
        public float Radius => radius;
        // How fast it goes between its speeding up and its slowing down (m/s), for a crossing `length` long.
        public float CruiseSpeed(float length) => length / (cross - Mathf.Min(ease, cross * .5f));

        // Call it on an inactive object (the builder's root is switched on at the end).
        public void Configure(Vector3 travel, float radius, float dwell, float cross, float ease, float phaseSeconds)
        {
            this.travel = travel;
            this.radius = Mathf.Max(.1f, radius);
            this.dwell = Mathf.Max(0f, dwell);
            this.cross = Mathf.Max(.2f, cross);
            this.ease = Mathf.Clamp(ease, .01f, this.cross * .5f);
            SetPhase(phaseSeconds);
        }

        protected override void Awake()
        {
            // Before the first pose is worked out (the base's Awake asks for it).
            worldTravel = transform.parent != null ? transform.parent.TransformVector(travel) : travel;
            length = worldTravel.magnitude;
            var level = Vector3.ProjectOnPlane(worldTravel, Vector3.up);
            // About this axis a turn carries the ball's top the way it travels.
            if (level.sqrMagnitude > 1e-6f) axis = Vector3.Cross(Vector3.up, level.normalized);
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
            if (t < cross) return Run(t);
            t -= cross;
            if (t < dwell) return 1f;
            t -= dwell;
            return 1f - Run(t);
        }

        // 0 -> 1 over a crossing: speeding up over `ease`, one speed, slowing down over `ease`.
        float Run(float t)
        {
            float a = Mathf.Min(ease, cross * .5f), speed = 1f / (cross - a);
            if (t < a) return speed * t * t / (2f * a);
            if (t > cross - a)
            {
                float left = cross - t;
                return 1f - speed * left * left / (2f * a);
            }
            return speed * (t - a * .5f);
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            float across = Across(time);
            position = StartPosition + worldTravel * across;
            // Rolling without slipping: as far round as it has gone along, over its radius.
            rotation = Quaternion.AngleAxis(length * across / radius * Mathf.Rad2Deg, axis) * StartRotation;
        }
    }
}
