using UnityEngine;

namespace ChessFight.ProtectKing
{
    // Timed upward-opening variant; the existing weight-driven FoldingBridge is unchanged.
    public sealed class RoyalDrawbridgeLeaf : ObstacleMotion
    {
        [SerializeField, Range(0, 85)] float openAngle = 65;
        [SerializeField, Min(.1f)] float closedSeconds = 5;
        [SerializeField, Min(.1f)] float travelSeconds = 2;
        [SerializeField, Min(0)] float openSeconds = 2;
        [SerializeField] float phaseSeconds;
        [SerializeField] bool reverse;

        public float AngleAt(double time)
        {
            float closed = Mathf.Max(.1f, closedSeconds);
            float travel = Mathf.Max(.1f, travelSeconds);
            float held = Mathf.Max(0, openSeconds);
            double period = closed + 2 * travel + held;
            float t = (float)(((time + phaseSeconds) % period + period) % period);
            float fraction = t < closed ? 0 : t < closed + travel
                ? Mathf.SmoothStep(0, 1, (t - closed) / travel)
                : t < closed + travel + held ? 1
                : 1 - Mathf.SmoothStep(0, 1, (t - closed - travel - held) / travel);
            return (reverse ? 1 : -1) * Mathf.Clamp(openAngle, 0, 85) * fraction;
        }

        protected override Pose Evaluate(double time) =>
            new Pose(Origin, RestRotation * Quaternion.AngleAxis(AngleAt(time), Vector3.right));
    }
}
