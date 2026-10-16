using UnityEngine;

namespace ChessFight.ProtectKing
{
    // A rotating visual part of the chess clock, separate from its sliding gates.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ChessClockHand : ObstacleMotion
    {
        [SerializeField] Vector3 axis = Vector3.forward;
        [SerializeField, Tooltip("Offset in cycles.")] float phase;
        [SerializeField, InspectorName("Rotation Speed"), Tooltip("Degrees per second. Negative reverses rotation; zero stops it.")] float degreesPerSecond = -72f;
        protected override Pose Evaluate(double time)
        {
            var unit = axis.sqrMagnitude > .001f ? axis.normalized : Vector3.right;
            return new Pose(Origin, RestRotation * Quaternion.AngleAxis((float)((time * degreesPerSecond + phase * 360) % 360), unit));
        }
    }
}
