using UnityEngine;

namespace ChessFight.ProtectKing
{
    public enum MotionKind { Translate, Rotate, Gate }

    [RequireComponent(typeof(Rigidbody))]
    public sealed class ObstacleMotion : MonoBehaviour
    {
        public MotionKind kind;
        public Vector3 axis = Vector3.right;
        public float distance = 4;
        public float period = 5;
        public float phase;
        public float degreesPerSecond = 60;
        public bool shove = true;
        public ProtectTheKingMatchController match;
        Rigidbody body;
        Vector3 origin;
        Quaternion rotation;
        Vector3 previous;
        Vector3 velocity;
        Vector3 angularVelocity;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            origin = transform.position;
            rotation = transform.rotation;
            previous = origin;
        }

        void FixedUpdate()
        {
            float t = match != null ? match.Elapsed : Time.time;
            var unit = axis.sqrMagnitude > .001f ? axis.normalized : Vector3.right;
            if (kind == MotionKind.Rotate)
            {
                body.MoveRotation(rotation * Quaternion.AngleAxis(t * degreesPerSecond + phase * 360, unit));
                angularVelocity = rotation * unit * (degreesPerSecond * Mathf.Deg2Rad);
            }
            else
            {
                float wave = Mathf.Sin((t / Mathf.Max(.5f, period) + phase) * Mathf.PI * 2);
                float fraction = kind == MotionKind.Gate ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.35f, .35f, wave)) : wave;
                var next = origin + unit * (distance * fraction);
                velocity = (next - previous) / Time.fixedDeltaTime;
                previous = next;
                body.MovePosition(next);
            }
        }

        public Vector3 PointVelocity(Vector3 point) => velocity + Vector3.Cross(angularVelocity, point - transform.position);
    }
}
