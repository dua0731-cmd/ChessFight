using UnityEngine;

namespace ChessFight.PawnRush
{
    // What an obstacle sweeps across the way it stands on (design doc v0.4 §3: every obstacle covers
    // the whole width; there is no line past it it cannot touch). Read by Course01Validator, which
    // measures the way itself - the walkable floor across at the obstacle (Floor), or the climbable
    // face in front of it (Face) - and checks that all of it lies inside the reach. Spans of one
    // group cover the way together (the tower face's two pendulums).
    public sealed class ObstacleSpan : MonoBehaviour
    {
        public enum Probe { Floor, Face }

        [Tooltip("World direction across the way.")]
        [SerializeField] Vector3 across = Vector3.right;
        [Tooltip("How far the obstacle reaches along `across`, from its position (m).")]
        [SerializeField] float reachMin = -5f, reachMax = 5f;
        [SerializeField] Probe probe = Probe.Floor;
        [Tooltip("Floor: the floor's top (world y). Face: the height the face is checked at.")]
        [SerializeField] float probeY;
        [Tooltip("Face: world direction from the obstacle to the face.")]
        [SerializeField] Vector3 faceDirection = Vector3.forward;
        [SerializeField] string group;

        public Vector3 Across => across.normalized;
        public float ReachMin => reachMin;
        public float ReachMax => reachMax;
        public Probe Kind => probe;
        public float ProbeY => probeY;
        public Vector3 FaceDirection => faceDirection.normalized;
        public string Group => string.IsNullOrEmpty(group) ? name + "#" + GetInstanceID() : group;

        public void Configure(Vector3 across, float reachMin, float reachMax, Probe probe, float probeY, Vector3 faceDirection, string group = null)
        {
            this.across = across;
            this.reachMin = reachMin;
            this.reachMax = reachMax;
            this.probe = probe;
            this.probeY = probeY;
            this.faceDirection = faceDirection;
            this.group = group;
        }

        void OnDrawGizmosSelected()
        {
            var a = Across;
            var at = new Vector3(transform.position.x, probe == Probe.Floor ? probeY + .2f : probeY, transform.position.z);
            Gizmos.color = new Color(1f, .4f, .2f);
            Gizmos.DrawLine(at + a * reachMin, at + a * reachMax);
        }
    }
}
