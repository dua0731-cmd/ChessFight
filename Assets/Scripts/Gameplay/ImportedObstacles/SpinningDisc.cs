using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.ProtectKing
{
    public sealed class SpinningDisc : ObstacleSurface, ISurfaceTransport
    {
        public enum RotationDirection { Clockwise, Counterclockwise }

        [SerializeField, Min(0.1f), Tooltip("Disc diameter in metres before the obstacle root's Transform scale.")]
        float diameter = 8f;
        [SerializeField, Tooltip("Direction viewed from above the disc.")]
        RotationDirection rotationDirection = RotationDirection.Clockwise;
        [SerializeField, Min(0f), Tooltip("Degrees per second. Zero stops rotation; use Rotation Direction to reverse it.")]
        float rotationSpeed = 45f;
        [SerializeField, HideInInspector] Transform geometryRoot;
        [SerializeField, HideInInspector] Transform rotatingSurface;
        Quaternion rotationOrigin;

        float AngularSpeed => Mathf.Max(0f, rotationSpeed) *
            (rotationDirection == RotationDirection.Clockwise ? 1f : -1f);
        public bool TransportActive => Running;
        public override Quaternion DeltaRotation => Running
            ? Quaternion.AngleAxis(AngularSpeed * Time.fixedDeltaTime, transform.up) : Quaternion.identity;
        public override Vector3 PointVelocity(Vector3 point) => Running
            ? Vector3.Cross(transform.up * (AngularSpeed * Mathf.Deg2Rad), point - transform.position) : Vector3.zero;
        public Vector3 PointDisplacement(Vector3 point)
        {
            Vector3 offset = point - transform.position;
            return DeltaRotation * offset - offset;
        }
        protected override void Awake()
        {
            base.Awake();
            ApplySize();
            if (rotatingSurface != null) rotationOrigin = rotatingSurface.localRotation;
        }
        void OnValidate()
        {
            diameter = Mathf.Max(0.1f, diameter);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            ApplySize();
        }
        void ApplySize()
        {
            // Resize all authored meshes and colliders without changing their height.
            if (geometryRoot != null)
                geometryRoot.localScale = new Vector3(diameter / 8f, 1f, diameter / 8f);
        }
        void Update()
        {
            if (!Running || rotatingSurface == null) return;
            float angle = (float)(SharedTime * AngularSpeed % 360.0);
            rotatingSurface.localRotation = rotationOrigin * Quaternion.Euler(0, angle, 0);
        }
    }
}
