using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.PawnRush
{
    // Character controller reads both the travel and the backwards belt velocity.
    // The belt runs against the deck's forward, which is toward the goal once the pivot turns 180 degrees;
    // that reverse run is intended (승규 님, R258). The carriage is animated in the physics step
    // (Animation.animatePhysics), so the per-step difference here is a steady speed, not frame jitter.
    public sealed class LibraryConveyorSurface : MonoBehaviour, IMovingSurface
    {
        Vector3 previous, velocity;
        void OnEnable() { previous = transform.position; }
        void FixedUpdate()
        {
            velocity = (transform.position - previous) / Mathf.Max(Time.fixedDeltaTime, .0001f);
            previous = transform.position;
        }
        public Vector3 PointVelocity(Vector3 point) => velocity - transform.forward * .644f;
        public Quaternion DeltaRotation => Quaternion.identity;
    }
}
