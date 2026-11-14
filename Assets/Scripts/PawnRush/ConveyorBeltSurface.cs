using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The top of a CargoConveyor's belt (R661): it does not move, but says it does (IMovingSurface), so a pawn standing on it
    // is carried along at the belt's speed (the ragdoll adds the surface's velocity under its feet, as on a lift or the
    // war room's conveyor). Along this object's local x.
    public sealed class ConveyorBeltSurface : MonoBehaviour, IMovingSurface
    {
        [SerializeField] float speed = 3.2f;

        public float Speed => speed;

        public void Configure(float speed) => this.speed = speed;

        public Vector3 PointVelocity(Vector3 worldPoint) => transform.right * speed;

        public Quaternion DeltaRotation => Quaternion.identity;
    }
}
