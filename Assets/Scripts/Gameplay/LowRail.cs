using UnityEngine;

namespace ChessFight.Gameplay
{
    // R856 (user 11-26: "난간에 점프를 하면 높이 올라갈 때도 있다"): a low thing to jump over - the tutorial's hurdles and
    // limbo bar (TutorialCourse marks their colliders). A foot touching one counted as footing: a second press of the
    // jump key took off again in mid-air (hips to 1.95 m over the bar, 1.09 m from the floor), and the standing spring,
    // finding the rail under a foot and the floor under the hips by turns, kicked the pawn up on a single press (2.15 m).
    // Rails are now no footing for the feet, and a jump from on top of one tops out no higher than a jump from the floor
    // (RagdollPawn.PawnRushPhysics.cs).
    public sealed class LowRail : MonoBehaviour
    {
        /// <summary>The height of the floor the rail stands over.</summary>
        public float floorY;
    }
}
