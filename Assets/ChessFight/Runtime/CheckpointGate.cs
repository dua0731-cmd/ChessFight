using UnityEngine;

namespace ChessFight.ProtectKing
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CheckpointGate : MonoBehaviour
    {
        [Range(1, 5)] public int index = 1;
        public bool teamRestricted;
        public TeamId team;
        public Transform blueRecovery;
        public Transform redRecovery;
        public bool Allows(PlayerIdentity player) => !teamRestricted || player.team == team;

        // Swept plane crossing also catches fast movement that could miss a physics trigger.
        public bool Crossed(Vector3 from, Vector3 to)
        {
            var a = transform.InverseTransformPoint(from);
            var b = transform.InverseTransformPoint(to);
            if (a.z > 0 || b.z < 0 || b.z - a.z < 0.00001f) return false;
            var hit = Vector3.Lerp(a, b, -a.z / (b.z - a.z));
            var box = GetComponent<BoxCollider>();
            return Mathf.Abs(hit.x - box.center.x) <= box.size.x * .5f &&
                   Mathf.Abs(hit.y - box.center.y) <= box.size.y * .5f;
        }

        public Vector3 Recovery(PlayerIdentity player)
        {
            var anchor = player.team == TeamId.Blue ? blueRecovery : redRecovery;
            // Each piece gets a separate slot, including two kings at shared checkpoints.
            return anchor.position + new Vector3(((int)player.piece % 3 - 1) * 1.35f,
                .12f, ((int)player.piece / 3) * 1.5f);
        }
    }
}
