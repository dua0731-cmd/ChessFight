using UnityEngine;

namespace ChessFight.Gameplay
{
    // Rotates forever about a local axis: sweeper bars, turntables.
    public sealed class Spinner : Obstacle
    {
        [Tooltip("Local axis. Up is a horizontal sweeper; forward spins like a wheel.")]
        [SerializeField] Vector3 axis = Vector3.up;
        [Tooltip("Negative reverses the direction.")]
        [SerializeField] float degreesPerSecond = 90f;

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            position = StartPosition;
            rotation = StartRotation * Quaternion.AngleAxis(WrapDegrees(degreesPerSecond * time), axis);
        }
    }
}
