using UnityEngine;
namespace ChessFight.Gameplay
{
    // Called only when a grounded character actually jumps. Walking off never triggers a rebound.
    public interface IJumpSpringSurface { float TakeJumpBonus(Vector3 point); }
}
