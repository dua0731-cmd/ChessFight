using UnityEngine;

namespace ChessFight.ChessBoxingPrototype
{
    public enum CBDirection { Left, Right, High, Low }

    // The cursor lives in a stable input space, independent of the camera and pawn silhouette.
    public sealed class CBControl
    {
        public Vector2 Position { get; private set; } = new Vector2(-0.45f, 0.35f);
        public bool LeftHand { get; private set; } = true;
        public CBDirection Direction { get; private set; } = CBDirection.Left;
        public bool BodyPunch => Position.y < -0.30f;

        public void Set(Vector2 position)
        {
            Position = Vector2.ClampMagnitude(position, 1f);
            if (Position.x < -0.18f) LeftHand = true;
            else if (Position.x > 0.18f) LeftHand = false;
            Direction = Classify(Position, Direction);
        }

        public static CBDirection Classify(Vector2 position, CBDirection fallback = CBDirection.High)
        {
            if (position.sqrMagnitude < 0.025f) return fallback;
            if (Mathf.Abs(position.x) > Mathf.Abs(position.y))
                return position.x < 0f ? CBDirection.Left : CBDirection.Right;
            return position.y < 0f ? CBDirection.Low : CBDirection.High;
        }

        public void Reset() { LeftHand = true; Direction = CBDirection.Left; Set(new Vector2(-0.45f, 0.35f)); }
    }
}
