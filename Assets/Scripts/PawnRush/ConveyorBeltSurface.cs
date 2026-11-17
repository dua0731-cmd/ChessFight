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

        // R685 (승규 11-17 "기물이 여기에 닿으면 뭔가 날아가는 버그"): only over the belt's top. It said the belt's speed for any
        // point, so a pawn that touched the belt's side - its hands on the side as a climb face (RagdollPawn's climb
        // surface rides along with it), or the ground ray catching the edge - was dragged along x at 3.2 m/s and flung.
        // Now a point counts only within the belt's width (less EdgeMargin each side) and no lower than TopBand under its
        // top; anywhere else the belt stands still. This object is the belt's box, scaled to its size: local -0.5 .. 0.5.
        const float EdgeMargin = .06f, TopBand = .12f;

        public Vector3 PointVelocity(Vector3 worldPoint)
        {
            var p = transform.InverseTransformPoint(worldPoint);
            var size = transform.lossyScale;
            float halfWidth = .5f - EdgeMargin / Mathf.Max(.01f, Mathf.Abs(size.z));
            float low = .5f - TopBand / Mathf.Max(.01f, Mathf.Abs(size.y));
            bool onTop = Mathf.Abs(p.z) <= halfWidth && p.y >= low && Mathf.Abs(p.x) <= .5f;
            return onTop ? transform.right * speed : Vector3.zero;
        }

        public Quaternion DeltaRotation => Quaternion.identity;
    }
}
