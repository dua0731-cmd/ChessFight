using UnityEngine;

namespace ChessFight.Gameplay
{
    // Something a character can stand on or hang from while it moves: lifts,
    // shuttles, turntables, a sliding wall. A character standing on one adds this
    // motion to its own, so it is carried along instead of being left behind.
    //
    // Both answers describe the physics step ABOUT TO RUN, not the one that just
    // finished, so it does not matter whether the character or the surface runs
    // its FixedUpdate first. Every Obstacle is one.
    public interface IMovingSurface
    {
        // Velocity of the surface at a world point this step, m/s.
        Vector3 PointVelocity(Vector3 worldPoint);

        // How far the surface turns during this step.
        Quaternion DeltaRotation { get; }
    }
}
