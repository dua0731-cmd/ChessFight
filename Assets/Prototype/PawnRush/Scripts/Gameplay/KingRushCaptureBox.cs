using UnityEngine;

namespace ChessFight.Gameplay
{
    // Deliberately uses the hip center, not contact count or CountsAsBody:
    // held, thrown and cannon-fired enemies are valid captures.
    public sealed class KingRushCaptureBox : MonoBehaviour
    {
        public int team;
        public Vector3 size = new Vector3(4, 2, 4);
        public bool Contains(Vector3 hips) => new Bounds(Vector3.zero, size).Contains(transform.InverseTransformPoint(hips));
        void OnDrawGizmosSelected()
        { Gizmos.matrix = transform.localToWorldMatrix; Gizmos.color = Color.yellow; Gizmos.DrawWireCube(Vector3.zero, size); }
    }
}
