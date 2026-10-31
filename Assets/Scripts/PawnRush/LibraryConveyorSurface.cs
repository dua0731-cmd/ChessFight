using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.PawnRush
{
    // Character controller reads both the travel and the backwards belt velocity.
    // The belt runs against the deck's forward, which is toward the goal once the pivot turns 180 degrees;
    // that reverse run is intended (승규 님, R258). The carriage's travel comes from LibraryShuttleDriver,
    // which moves it from the shared clock in the same physics step the riders read it (R259).
    public sealed class LibraryConveyorSurface : MonoBehaviour, IMovingSurface
    {
        LibraryShuttleDriver driver;
        void Awake() { driver = GetComponentInParent<LibraryShuttleDriver>(); }
        public Vector3 PointVelocity(Vector3 point) =>
            (driver != null ? driver.CarriageVelocity : Vector3.zero) - transform.forward * .644f;
        public Quaternion DeltaRotation => Quaternion.identity;
    }
}
