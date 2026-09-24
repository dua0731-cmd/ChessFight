using System;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // Base for every moving obstacle.
    //
    // THE RULE: a pose is a pure function of ObstacleClock time. Never accumulate
    // "angle += speed * deltaTime". Accumulating drifts per machine, so in a match
    // each player would see the bar somewhere else and a hit on one screen would be
    // a miss on another. A pure function of a shared clock needs no networking.
    //
    // The obstacle only moves. What a hit does is the character's business: the
    // ragdoll is made of rigidbodies and takes the hit physically (add the lab's
    // RagdollHazard to decide knockdowns); the playtest stand-in reads VelocityAt.
    [RequireComponent(typeof(Rigidbody))]
    public abstract class Obstacle : MonoBehaviour
    {
        [Tooltip("Seconds added to the shared clock, so identical obstacles do not move in lockstep.")]
        [SerializeField] protected float phase;

        Rigidbody body;
        Vector3 startPosition, lastPosition, linearVelocity, angularVelocity;
        Quaternion startRotation, lastRotation;

        protected Vector3 StartPosition => startPosition;
        protected Quaternion StartRotation => startRotation;

        // Where the obstacle is at this time. The only thing a new obstacle type writes.
        protected abstract void Evaluate(double time, out Vector3 position, out Quaternion rotation);

        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            startPosition = transform.position;
            startRotation = transform.rotation;
            // Start at the current pose, so the first step does not report a huge
            // velocity from the authored pose to wherever the clock says it is.
            Evaluate(ObstacleClock.Now + phase, out lastPosition, out lastRotation);
            transform.SetPositionAndRotation(lastPosition, lastRotation);
        }

        protected virtual void FixedUpdate()
        {
            Evaluate(ObstacleClock.Now + phase, out var position, out var rotation);
            float dt = Time.fixedDeltaTime;
            linearVelocity = (position - lastPosition) / dt;
            (rotation * Quaternion.Inverse(lastRotation)).ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180f) degrees -= 360f;
            bool still = Mathf.Abs(degrees) < 1e-4f || float.IsNaN(axis.x) || float.IsInfinity(axis.x);
            angularVelocity = still ? Vector3.zero : axis * (degrees * Mathf.Deg2Rad / dt);
            body.MovePosition(position);
            body.MoveRotation(rotation);
            lastPosition = position;
            lastRotation = rotation;
        }

        // Surface velocity at a world point: how fast this obstacle is moving
        // where it touches something.
        public Vector3 VelocityAt(Vector3 worldPoint) =>
            linearVelocity + Vector3.Cross(angularVelocity, worldPoint - lastPosition);

        // Wrap in double before narrowing. The shared clock is Unix time, and
        // degrees-per-second times 1.7e9 has no precision left as a float.
        protected static float WrapDegrees(double degrees) => (float)(degrees % 360.0);

        // -1..1 sine with the given period in seconds.
        protected static float Wave(double time, float period) =>
            period <= 0f ? 0f : (float)Math.Sin(2.0 * Math.PI * (time / period % 1.0));
    }
}
