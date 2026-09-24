using UnityEngine;

namespace ChessFight.Gameplay
{
    // Slides back and forth along a local offset: sliding walls, pistons, lifts.
    public sealed class Oscillator : Obstacle
    {
        [Tooltip("Local offset reached at each end of the swing.")]
        [SerializeField] Vector3 travel = new Vector3(5f, 0f, 0f);
        [Tooltip("Seconds for one full out-and-back cycle.")]
        [SerializeField] float period = 3f;

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = StartRotation;
            position = StartPosition + StartRotation * (travel * Wave(time, period));
        }
    }
}
