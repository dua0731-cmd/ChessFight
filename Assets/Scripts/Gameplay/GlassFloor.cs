using UnityEngine;

namespace ChessFight.Gameplay
{
    // 2026-11-23 (사용자 "대계단 코스에서 유리 밟을 때 나는 소리 넣어줘"): a glass floor - the grand stair hall's glass
    // stepping stones (GuessBridge puts it on every square). A pawn's footsteps on it, and its landing on it, are a
    // crisp glassy tap instead of its dry steps (RagdollLab PawnRushStepSfx, which finds the floor under the hips).
    // Put on the floor's collider, as WetFloor.
    public sealed class GlassFloor : MonoBehaviour
    {
    }
}
