using UnityEngine;

namespace ChessFight.PawnRush
{
    // One test zone of 승규 님's test scene (SeoungTestCourse): a course section or a mini-game, where a pawn starts
    // in it, and the box it fills (world space, measured when it was built).
    public sealed class SeoungTestZone : MonoBehaviour
    {
        public string label;
        public Transform start;
        public MissionStation station;
        public Bounds bounds;

        public bool IsMiniGame => station != null;
    }
}
