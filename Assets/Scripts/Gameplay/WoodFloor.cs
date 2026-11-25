using UnityEngine;

namespace ChessFight.Gameplay
{
    // 2026-11-25 (user, on the tutorial's plank bridge: "외나무다리 이 소리가 아니야"): a wooden beam floor. A pawn's footsteps
    // on it are a hollow woody knock instead of its dry steps (RagdollLab PawnRushStepSfx, which finds the floor under the
    // hips; WoodWalk* / WoodRun*, ArtSource/PawnRush/Sfx/bake_wood.py). Put on the floor's collider, as GlassFloor.
    // Only the tutorial's bridge has it so far (TutorialCourse.Sound MarkBridge).
    public sealed class WoodFloor : MonoBehaviour
    {
    }
}
