using UnityEngine;

namespace ChessFight.Gameplay
{
    public sealed class KingRushCourseCheckpoint : MonoBehaviour
    {
        public int order;
        public string title;
        public Vector3 size = new Vector3(16, 5, 3);
        public bool Contains(Vector3 hips) => new Bounds(transform.position + Vector3.up * 2, size).Contains(hips);
        void OnDrawGizmosSelected()
        { Gizmos.color = Color.green; Gizmos.DrawWireCube(transform.position + Vector3.up * 2, size); }
    }
}
