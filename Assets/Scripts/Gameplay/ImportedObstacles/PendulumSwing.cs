using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.ProtectKing
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PendulumSwing : MonoBehaviour
    {
        float RuntimeTime => SampleTime(ObstacleClock.Now);
        float SampleTime(double t) => ObstacleContext.PeriodicTime(t,StartDelay,2 * (Mathf.Max(.3f,SwingDuration)+Mathf.Max(0,PauseAtEnds)),true);
        public ObstacleContext context;
        public Transform Ball;
        [Range(0, 85)] public float SwingAngle = 55;
        [Min(.3f), Tooltip("Seconds from one extreme to the other, excluding end pauses.")]
        public float SwingDuration = 2;
        [Min(0)] public float StartDelay = .5f;
        [Min(0)] public float PauseAtEnds = .2f;
        [Min(0)] public float KnockbackForce = 14;
        [Min(0)] public float UpwardForce = 3;
        public bool Active = true;
        public float Angle { get; private set; }
        public Vector3 BallVelocity { get; private set; }
        public bool CanHit => isActiveAndEnabled && Active && (context == null || context.Running);
        Rigidbody body;
        Quaternion restLocalRotation;
        float previousTime;

        void Awake()
        {
            if (context == null) context = GetComponentInParent<ObstacleContext>();
            body = GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            restLocalRotation = transform.localRotation;
            previousTime = RuntimeTime;
            Angle = EvaluateAngle(previousTime);
            body.rotation = WorldRestRotation * Quaternion.AngleAxis(Angle, Vector3.forward);
        }
        Quaternion WorldRestRotation => (transform.parent != null ? transform.parent.rotation : Quaternion.identity) * restLocalRotation;

        public float EvaluateAngle(float elapsed)
        {
            float amplitude = Mathf.Clamp(SwingAngle, 0, 85);
            float duration = Mathf.Max(.3f, SwingDuration), pause = Mathf.Max(0, PauseAtEnds);
            if (elapsed <= Mathf.Max(0, StartDelay)) return -amplitude;
            float t = Mathf.Repeat(elapsed - Mathf.Max(0, StartDelay), 2 * (duration + pause));
            if (t < pause) return -amplitude;
            t -= pause;
            if (t < duration) return -amplitude * Mathf.Cos(Mathf.PI * t / duration);
            t -= duration;
            if (t < pause) return amplitude;
            return amplitude * Mathf.Cos(Mathf.PI * (t - pause) / duration);
        }
        void FixedUpdate()
        {
            float time = RuntimeTime;
            bool reset = time < previousTime; previousTime = time;
            BallVelocity = Vector3.zero;
            if (reset)
            {
                Angle = EvaluateAngle(time);
                body.rotation = WorldRestRotation * Quaternion.AngleAxis(Angle, Vector3.forward);
                return;
            }
            if (!CanHit) return;
            float before = Angle;
            // Normal operation follows the cosine exactly. Reactivation catches up at a bounded speed.
            float maxRate = Mathf.Clamp(SwingAngle, 0, 85) * Mathf.PI / Mathf.Max(.3f, SwingDuration);
            Angle = EvaluateAngle(time);
            var rotation = WorldRestRotation * Quaternion.AngleAxis(Angle, Vector3.forward);
            body.MoveRotation(rotation);
            if (Ball != null)
            {
                var angular = WorldRestRotation * Vector3.forward * ((Angle - before) * Mathf.Deg2Rad / Time.fixedDeltaTime);
                BallVelocity = Vector3.Cross(angular, Ball.position - transform.position);
            }
        }
        void OnDisable() { BallVelocity = Vector3.zero; }
        void OnDrawGizmosSelected()
        {
            if (Ball == null) return;
            var rest = Application.isPlaying ? WorldRestRotation : transform.rotation;
            float length = Vector3.Distance(transform.position, Ball.position);
            var collider = Ball.GetComponent<SphereCollider>();
            float radius = collider != null ? collider.radius * Mathf.Max(Mathf.Abs(Ball.lossyScale.x), Mathf.Abs(Ball.lossyScale.y), Mathf.Abs(Ball.lossyScale.z)) : .5f;
            Vector3 previous = default;
            Gizmos.color = Color.yellow;
            for (int i = 0; i <= 40; i++)
            {
                float angle = Mathf.Lerp(-SwingAngle, SwingAngle, i / 40f);
                var point = transform.position + rest * Quaternion.AngleAxis(angle, Vector3.forward) * Vector3.down * length;
                if (i > 0) Gizmos.DrawLine(previous, point);
                if (i == 0 || i == 40) { Gizmos.DrawLine(transform.position, point); Gizmos.DrawWireSphere(point, radius); }
                previous = point;
            }
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(Ball.position, radius);
        }
    }
}
