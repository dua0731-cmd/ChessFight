using System;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // A platform that goes round a circle and stays level: the gondolas of a big
    // wheel, islands drifting round the tower. The object is placed at the centre
    // of the circle; `axis` (local) is the circle's axis, `radius` its size, and it
    // takes `period` seconds for a full turn.
    //
    // Like every Obstacle its pose is a pure function of the shared clock (G2), and
    // characters ride it through IMovingSurface.
    public sealed class OrbitPlatform : Obstacle
    {
        [SerializeField] float radius = 8f;
        [Tooltip("Seconds for one full turn. Negative turns the other way.")]
        [SerializeField] float period = 30f;
        [Tooltip("Local axis of the circle. Forward (z) is a big wheel standing in the x-y plane; up is a merry-go-round.")]
        [SerializeField] Vector3 axis = Vector3.forward;

        public float Radius => radius;
        public float Period => period;

        // For platforms built from code: on an INACTIVE object, before Awake.
        public void Configure(float radius, float period, Vector3 axis, float phase = 0f)
        {
            this.radius = radius;
            this.period = period;
            this.axis = axis;
            SetPhase(phase);
        }

        // Where on the circle the platform is at `time`, relative to the centre, in the centre's frame.
        // Tools/QueenHill/preview/index.html (orbitOffset) mirrors this; keep them the same.
        public static Vector3 Offset(Vector3 axis, float radius, float period, double time)
        {
            Vector3 a = axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector3.forward;
            Vector3 u = Vector3.Cross(a, Vector3.up);
            if (u.sqrMagnitude < 1e-6f) u = Vector3.right;
            u.Normalize();
            Vector3 w = Vector3.Cross(a, u);
            if (Mathf.Abs(period) < 1e-3f) return u * radius;
            double turn = time / period;
            double theta = 2.0 * Math.PI * (turn - Math.Floor(turn));
            return u * (radius * (float)Math.Cos(theta)) + w * (radius * (float)Math.Sin(theta));
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = StartRotation;
            position = StartPosition + StartRotation * Offset(axis, radius, period, time);
        }
    }
}
