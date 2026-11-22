using UnityEngine;

namespace ChessFight.Gameplay
{
    // 2026-11-22 (승규 on the royal water garden's lock: "갑문 물 기물이랑 물이랑 걸을 때 사운드 넣어줘", then "잔잔한 물 헤치는
    // 소리"): a floor under a few centimetres of water. A pawn's footsteps on it are soft water parting instead of its
    // dry steps (RagdollLab PawnRushStepSfx, which finds the floor under the hips). Put on the floor's collider.
    public sealed class WetFloor : MonoBehaviour
    {
    }
}
