using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.ProtectKing
{
    // Shared Rigidbody transport; parameters belong to concrete obstacle classes.
    // Reads ObstacleClock through SharedTime, independent of execution order.
    [RequireComponent(typeof(Rigidbody))]
    public abstract class ObstacleMotion : Obstacle
    {
        Rigidbody body;
        ImportedMovingSurface surface;
        protected Vector3 Origin { get; private set; }
        protected Quaternion RestRotation { get; private set; }
        public virtual float KnockbackForce => 0f;
        public virtual float UpwardForce => 0f;
        public virtual float HitCooldown => .35f;

        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Origin = transform.position; RestRotation = transform.rotation;
            ImportedMovingSurface.Bind(body, Evaluate, () => isActiveAndEnabled);
            surface = GetComponent<ImportedMovingSurface>();
            var first = Evaluate(SharedTime); body.position = first.position; body.rotation = first.rotation;
        }
        protected abstract Pose Evaluate(double time);
        protected void FixedUpdate()
        {
            var pose = Evaluate(SharedTime);
            body.MovePosition(pose.position); body.MoveRotation(pose.rotation);
        }
        public Vector3 PointVelocity(Vector3 point) => surface != null ? surface.PointVelocity(point) : Vector3.zero;
        protected Vector3 TranslationAxis(Vector3 axis, bool local)
        {
            var unit = axis.sqrMagnitude > .001f ? axis.normalized : Vector3.right;
            return local ? RestRotation * unit : unit;
        }
        protected static float Wave(double time, float period, float phase) =>
            (float)System.Math.Sin((time / System.Math.Max(.5f, period) + phase) % 1 * System.Math.PI * 2);
    }
}
