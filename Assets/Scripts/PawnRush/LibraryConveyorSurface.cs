using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.PawnRush
{
    // Character controller reads both the travel and the backwards belt velocity.
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
