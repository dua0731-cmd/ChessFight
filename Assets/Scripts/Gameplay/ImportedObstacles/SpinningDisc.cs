using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.ProtectKing
{
    public sealed class SpinningDisc : ObstacleSurface, ISurfaceTransport
    {
        [SerializeField, Tooltip("Degrees per second. Negative reverses rotation; zero stops it.")]
        float rotationSpeed = 45f;
        [SerializeField] Transform[] rotors;
        Quaternion[] rotorOrigins;

        public bool TransportActive => Running;
        public override Quaternion DeltaRotation => Running
            ? Quaternion.AngleAxis(rotationSpeed * Time.fixedDeltaTime, transform.up) : Quaternion.identity;
        public override Vector3 PointVelocity(Vector3 point) => Running
            ? Vector3.Cross(transform.up * (rotationSpeed * Mathf.Deg2Rad), point - transform.position) : Vector3.zero;
        public Vector3 PointDisplacement(Vector3 point)
        {
            Vector3 offset = point - transform.position;
            return DeltaRotation * offset - offset;
        }
        protected override void Awake()
        {
            base.Awake();
            rotorOrigins = new Quaternion[rotors == null ? 0 : rotors.Length];
            for (int i = 0; i < rotorOrigins.Length; i++) rotorOrigins[i] = rotors[i].localRotation;
        }
        void Update()
        {
            if (!Running) return;
            float angle = (float)(SharedTime * rotationSpeed % 360.0);
            for (int i = 0; i < rotorOrigins.Length; i++)
                rotors[i].localRotation = rotorOrigins[i] * Quaternion.Euler(0, angle, 0);
        }
    }
}
