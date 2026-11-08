using UnityEngine;

namespace ChessFight.PawnRush
{
    // E's visible bookshelf turns while the original gate collider remains owned by TeamGate.
    public sealed class PalaceGateLeaf : MonoBehaviour
    {
        [SerializeField] TeamGate gate;
        [SerializeField] Transform leaf;
        float shown;

        public void Configure(TeamGate source, Transform visibleLeaf)
        {
            gate = source;
            leaf = visibleLeaf;
        }

        void LateUpdate()
        {
            if (gate == null || leaf == null) return;
            shown = Mathf.MoveTowards(shown, gate.IsOpen ? 1f : 0f, Time.deltaTime / 1.2f);
            leaf.localRotation = Quaternion.Euler(0f, -95f * Mathf.SmoothStep(0f, 1f, shown), 0f);
        }
    }
}
