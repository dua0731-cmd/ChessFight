using UnityEngine;

namespace ChessFight.Gameplay
{
    // Common identity and clock for obstacles. Static pads and state-driven hazards
    // must not acquire a moving Rigidbody just by inheriting this class.
    public abstract class Obstacle : MonoBehaviour
    {
        protected double SharedTime => ObstacleClock.Now;
    }
}
