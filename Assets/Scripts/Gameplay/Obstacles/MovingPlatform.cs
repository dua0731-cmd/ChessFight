using System;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // A platform that carries characters: lifts, gondolas, shuttles, turntables,
    // orbit rings. Travels from where it was placed to `travel` (a local offset)
    // and back at a steady `speed`, easing in and out over `rampTime` and waiting
    // `pause` at each end; it can also spin about a local axis the whole time.
    //
    // Like every Obstacle its pose is a pure function of the shared clock (G2), so
    // every PC sees it in the same place without a packet. Characters ride it
    // through IMovingSurface: a ragdoll standing on it, or climbing its side, adds
    // its motion to its own.
    //
    // Keep rampTime at speed / 6 or more. The rider is only held down by gravity,
    // so a lift that stops going up harder than 9.81 m/s^2 throws it off the top.
    public sealed class MovingPlatform : Obstacle
    {
        [Tooltip("Local offset of the far end. Zero for a platform that only spins.")]
        [SerializeField] Vector3 travel = new Vector3(0f, 12f, 0f);
        [Tooltip("Cruising speed along the way, m/s.")]
        [SerializeField] float speed = 3f;
        [Tooltip("Seconds to reach full speed from a stop, and to stop again.")]
        [SerializeField] float rampTime = 0.5f;
        [Tooltip("Seconds it waits at each end.")]
        [SerializeField] float pause = 2f;
        [Tooltip("Degrees per second about spinAxis, all the time. Negative turns the other way.")]
        [SerializeField] float spinDegreesPerSecond;
        [Tooltip("Local axis. Up is a turntable.")]
        [SerializeField] Vector3 spinAxis = Vector3.up;

        public Vector3 Travel => travel;
        public float Speed => speed;

        // One full out-and-back, pauses included.
        public float CycleSeconds => 2f * (pause + LegSeconds);

        float Distance => travel.magnitude;
        float Ramp => speed <= 0f ? 0f : Mathf.Clamp(rampTime, 0f, Distance / speed);
        float LegSeconds => speed <= 0f || Distance < 1e-4f ? 0f : Distance / speed + Ramp;

        // For platforms built from code (test beds). Call it on an INACTIVE object
        // before it is switched on: Awake evaluates the first pose from these values.
        public void Configure(Vector3 travel, float speed, float pause, float rampTime = 0.5f,
                              float spinDegreesPerSecond = 0f, Vector3? spinAxis = null)
        {
            this.travel = travel;
            this.speed = speed;
            this.pause = pause;
            this.rampTime = rampTime;
            this.spinDegreesPerSecond = spinDegreesPerSecond;
            this.spinAxis = spinAxis ?? Vector3.up;
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = Mathf.Abs(spinDegreesPerSecond) > 1e-4f && spinAxis.sqrMagnitude > 1e-6f
                ? StartRotation * Quaternion.AngleAxis(WrapDegrees(spinDegreesPerSecond * time), spinAxis)
                : StartRotation;
            position = StartPosition + StartRotation * (travel * Along(time));
        }

        // 0 at the start, 1 at the far end.
        float Along(double time)
        {
            float leg = LegSeconds;
            if (leg <= 0f) return 0f;
            double cycle = 2.0 * (pause + leg);
            double u = time % cycle;
            if (u < 0.0) u += cycle;
            if (u < pause) return 0f;
            u -= pause;
            if (u < leg) return Covered((float)u) / Distance;
            u -= leg;
            if (u < pause) return 1f;
            u -= pause;
            return 1f - Covered((float)u) / Distance;
        }

        // Distance covered t seconds into one leg: speed up evenly over the ramp,
        // cruise, slow down evenly. Position and velocity are both continuous.
        float Covered(float t)
        {
            float v = speed, r = Ramp, leg = LegSeconds, d = Distance;
            if (r <= 1e-4f) return Mathf.Min(d, v * t);
            if (t < r) return 0.5f * v / r * t * t;
            if (t < leg - r) return 0.5f * v * r + v * (t - r);
            float left = Mathf.Max(0f, leg - t);
            return d - 0.5f * v / r * left * left;
        }

        void OnDrawGizmosSelected()
        {
            // Where it goes, from wherever it was placed.
            Vector3 from = Application.isPlaying ? StartPosition : transform.position;
            Quaternion rot = Application.isPlaying ? StartRotation : transform.rotation;
            Vector3 to = from + rot * travel;
            Gizmos.color = new Color(.3f, .7f, 1f, .8f);
            Gizmos.DrawLine(from, to);
            Gizmos.DrawWireSphere(to, .25f);
        }
    }
}
