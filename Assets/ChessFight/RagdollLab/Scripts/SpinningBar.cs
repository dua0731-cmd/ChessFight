using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>Kinematic sweeper that rotates around the world Y axis at an adjustable speed.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SpinningBar : MonoBehaviour
    {
        public float degreesPerSecond = 90f;

        Rigidbody body;
        float angle;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            angle = transform.eulerAngles.y;
        }

        void FixedUpdate()
        {
            angle = Mathf.Repeat(angle + degreesPerSecond * Time.fixedDeltaTime, 360f);
            body.MoveRotation(Quaternion.Euler(0f, angle, 0f));
        }
    }
}
