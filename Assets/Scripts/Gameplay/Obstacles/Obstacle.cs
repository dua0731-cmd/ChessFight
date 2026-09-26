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
    // Characters standing on it read the same motion through IMovingSurface and
    // ride along.
    [RequireComponent(typeof(Rigidbody))]
    public abstract class Obstacle : MonoBehaviour, IMovingSurface
    {
        [Tooltip("Seconds added to the shared clock, so identical obstacles do not move in lockstep.")]
        [SerializeField] protected float phase;

        Rigidbody body;
        Vector3 startPosition, fromPosition, toPosition, linearVelocity, angularVelocity;
        Quaternion startRotation, fromRotation, toRotation, stepTurn = Quaternion.identity;
        double stepKey = double.NaN, lastTime = double.NaN;
        bool jumped;

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
            Evaluate(ObstacleClock.Now + phase, out toPosition, out toRotation);
            fromPosition = toPosition;
            fromRotation = toRotation;
            transform.SetPositionAndRotation(toPosition, toRotation);
        }

        protected virtual void FixedUpdate()
        {
            Step();
            if (jumped)
            {
                // Straight there: MovePosition would sweep the whole jump and bat away whatever is in the way.
                body.position = toPosition;
                body.rotation = toRotation;
                return;
            }
            body.MovePosition(toPosition);
            body.MoveRotation(toRotation);
        }

        // Works out where this physics step takes the obstacle, once per step, for
        // whoever asks first: its own FixedUpdate, or a character standing on it
        // whose FixedUpdate happens to run earlier (the ragdoll runs before default
        // order). Either way the character sees the motion the obstacle is about to
        // make, not the one it made last step - one step late was 2.5 cm of lag on a
        // 3 m/s lift at 120 Hz, fed back into the rider every step.
        void Step()
        {
            double key = Time.fixedTimeAsDouble;
            if (key == stepKey) return;
            stepKey = key;
            fromPosition = toPosition;
            fromRotation = toRotation;
            double now = ObstacleClock.Now + phase;
            Evaluate(now, out toPosition, out toRotation);
            float dt = Time.fixedDeltaTime;
            // The clock itself jumped - switched to the shared match clock or back, or a long hitch:
            // go there without reporting the jump as speed to whoever stands on the obstacle.
            jumped = !double.IsNaN(lastTime) && Math.Abs(now - lastTime - dt) > 0.25;
            lastTime = now;
            if (jumped)
            {
                fromPosition = toPosition;
                fromRotation = toRotation;
            }
            linearVelocity = (toPosition - fromPosition) / dt;
            stepTurn = toRotation * Quaternion.Inverse(fromRotation);
            stepTurn.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180f) degrees -= 360f;
            bool still = Mathf.Abs(degrees) < 1e-4f || float.IsNaN(axis.x) || float.IsInfinity(axis.x);
            angularVelocity = still ? Vector3.zero : axis * (degrees * Mathf.Deg2Rad / dt);
            if (still) stepTurn = Quaternion.identity;
        }

        // Surface velocity at a world point: how fast this obstacle is moving
        // where it touches something.
        public Vector3 PointVelocity(Vector3 worldPoint)
        {
            Step();
            return linearVelocity + Vector3.Cross(angularVelocity, worldPoint - fromPosition);
        }

        public Quaternion DeltaRotation
        {
            get
            {
                Step();
                return stepTurn;
            }
        }

        // The older name, kept for the playtest stand-in.
        public Vector3 VelocityAt(Vector3 worldPoint) => PointVelocity(worldPoint);

        // Wrap in double before narrowing. The shared clock is Unix time, and
        // degrees-per-second times 1.7e9 has no precision left as a float.
        protected static float WrapDegrees(double degrees) => (float)(degrees % 360.0);

        // -1..1 sine with the given period in seconds.
        protected static float Wave(double time, float period) =>
            period <= 0f ? 0f : (float)Math.Sin(2.0 * Math.PI * (time / period % 1.0));
    }
}
