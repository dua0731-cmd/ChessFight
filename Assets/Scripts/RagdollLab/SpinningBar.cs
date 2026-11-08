using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Kinematic sweeper that rotates around the world Y axis at an adjustable speed.
    /// Its angle is a pure function of ObstacleClock (DECISIONS G2), not a per-PC running total:
    /// accumulated, every PC's bar sat at a different angle and online players were knocked down
    /// by a bar their own screen showed elsewhere.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SpinningBar : MonoBehaviour
    {
        public float degreesPerSecond = 90f;

        Rigidbody body;
        float startAngle, appliedSpeed;
        double phase;   // degrees, so a speed change (tuning panel, tests) keeps the bar where it is

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            startAngle = transform.eulerAngles.y;
            appliedSpeed = degreesPerSecond;
        }

        void FixedUpdate()
        {
            double now = ObstacleClock.Now;
            if (degreesPerSecond != appliedSpeed)
            {
                phase += (appliedSpeed - degreesPerSecond) * now;
                appliedSpeed = degreesPerSecond;
            }
            // Wrap in double: the match clock is Unix time, too large for float degrees.
            double angle = (startAngle + phase + appliedSpeed * now) % 360.0;
            body.MoveRotation(Quaternion.Euler(0f, (float)angle, 0f));
        }
    }
}
