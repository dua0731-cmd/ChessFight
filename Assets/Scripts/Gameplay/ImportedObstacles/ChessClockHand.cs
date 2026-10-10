using UnityEngine;

namespace ChessFight.ProtectKing
{
    // A rotating visual part of the chess clock, separate from its sliding gates.
    public sealed class ChessClockHand : ObstacleMotion
    {
        [SerializeField] Vector3 axis = Vector3.forward;
        [SerializeField, Tooltip("Offset in cycles.")] float phase;
        [SerializeField, Tooltip("Degrees per second.")] float degreesPerSecond = -72f;
        protected override Pose Evaluate(double time)
        {
            var unit = axis.sqrMagnitude > .001f ? axis.normalized : Vector3.right;
            return new Pose(Origin, RestRotation * Quaternion.AngleAxis((float)((time * degreesPerSecond + phase * 360) % 360), unit));
        }
    }
}
