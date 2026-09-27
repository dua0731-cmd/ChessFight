using UnityEngine;

namespace ChessFight.Gameplay
{
    // A player-driven mission platform. Its authoritative angle is supplied by mission rules.
    // Unlike periodic obstacles, the state will need replication when this course goes online.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class KingRushSeesawBoard : MonoBehaviour, IMovingSurface
    {
        Rigidbody body;
        Quaternion delta = Quaternion.identity;
        Vector3 angular;
        public float Angle { get; private set; }
        void Awake()
        {
            body = GetComponent<Rigidbody>(); body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
        public void SetAngle(float angle, float dt)
        {
            var next = Quaternion.Euler(0, 0, -angle);
            delta = next * Quaternion.Inverse(Quaternion.Euler(0, 0, -Angle));
            angular = Vector3.back * ((angle - Angle) * Mathf.Deg2Rad / Mathf.Max(.001f, dt));
            Angle = angle; body.MoveRotation(next);
        }
        public void ResetBoard()
        { Angle = 0; delta = Quaternion.identity; angular = Vector3.zero; body.rotation = Quaternion.identity; }
        public Vector3 PointVelocity(Vector3 point) => Vector3.Cross(angular, point - body.position);
        public Quaternion DeltaRotation => delta;
        public bool Supports(Vector3 hips)
        {
            Vector3 local = Quaternion.Inverse(body.rotation) * (hips - body.position);
            return Mathf.Abs(local.x) < 12 && Mathf.Abs(local.z) < 12 && local.y > -.15f && local.y < 1.5f;
        }
        public float Weight(Vector3 hips)
        {
            float x = (Quaternion.Inverse(body.rotation) * (hips - body.position)).x;
            return Mathf.Abs(x) <= .5f ? 0 : x;
        }
    }
}
