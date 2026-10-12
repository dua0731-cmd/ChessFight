using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.ProtectKing
{
    // Attach to the tilting body only; the root also owns stationary ramps and supports.
    [DisallowMultipleComponent]
    public sealed class WeightedBridgeSurface : MonoBehaviour, IMovingSurface
    {
        [SerializeField, HideInInspector] WeightedBridge bridge;
        public static void Bind(Rigidbody body, WeightedBridge owner)
        {
            var surface = body.GetComponent<WeightedBridgeSurface>();
            if (surface == null) surface = body.gameObject.AddComponent<WeightedBridgeSurface>();
            surface.bridge = owner;
        }
        public Quaternion DeltaRotation => bridge != null ? bridge.DeltaRotation : Quaternion.identity;
        public Vector3 PointVelocity(Vector3 point) => bridge != null ? bridge.PointVelocity(point) : Vector3.zero;
    }
}
