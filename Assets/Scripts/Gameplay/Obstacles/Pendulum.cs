using UnityEngine;

namespace ChessFight.Gameplay
{
    // Swings to and fro about a local axis. Put this on the pivot at the top and
    // hang the arm and head underneath as children: they swing with it.
    public sealed class Pendulum : Obstacle
    {
        [SerializeField] Vector3 axis = Vector3.forward;
        [Tooltip("Degrees either side of the authored pose.")]
        [SerializeField] float amplitude = 60f;
        [Tooltip("Seconds for one full swing there and back.")]
        [SerializeField] float period = 2.5f;

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            position = StartPosition;
            rotation = StartRotation * Quaternion.AngleAxis(amplitude * Wave(time, period), axis);
        }
    }
}
