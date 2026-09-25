using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>Colliders under this component knock a pawn down on any real hit (spec: rotating bar).</summary>
    public class RagdollHazard : MonoBehaviour
    {
        public bool alwaysKnockdown = true;
        [Tooltip("Minimum contact speed (m/s) that counts as a hit.")]
        public float minImpact = 1f;
    }
}
