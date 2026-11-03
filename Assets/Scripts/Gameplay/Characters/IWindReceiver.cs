using UnityEngine;

namespace ChessFight.Gameplay
{
    // Something a wind can carry over the ground (an air vent with a drift speed, Pawn Rush grand stair hall
    // R301): the velocity is added to the frame the character runs in, the way a moving floor's is, so its
    // own footing moves with the wind instead of holding it in place. The vent calls it every physics step it
    // reaches the character; when the calls stop the drift eases out. Returns false when the character
    // cannot be carried that way right now (down, climbing, swimming) - the vent then pushes its bodies.
    // Only the machine that simulates the character calls it; a network puppet ignores it. Look it up next
    // to the driver (GetComponentInParent<IWindReceiver>()).
    public interface IWindReceiver
    {
        bool Blow(Vector3 velocity);
    }
}
