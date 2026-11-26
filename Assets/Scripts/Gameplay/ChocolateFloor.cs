using UnityEngine;

namespace ChessFight.Gameplay
{
    // 2026-11-26 (R845 이어서, the grand banquet hall's sounds, design 2-3 #23): melted chocolate underfoot - the chocolate
    // fountain's bowl and tiers. A pawn's footsteps on it are a sticky squelch instead of its dry steps (RagdollLab
    // PawnRushStepSfx, which finds the floor under the hips; ChocoStep*, ArtSource/PawnRush/BanquetHall/Sound/bake.py).
    // Put on the floor's collider, as WoodFloor; GrandBanquetHallSound puts it on the fountain's in play.
    public sealed class ChocolateFloor : MonoBehaviour
    {
    }
}
