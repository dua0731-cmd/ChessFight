using UnityEngine;
namespace ChessFight.ProtectKing
{
    // Authored map anchors only. No player type, respawn, scoring or victory rule.
    public sealed class CheckpointGate : MonoBehaviour
    {
        public int index;
        public bool teamRestricted;
        public int team;
        public Transform blueRecovery, redRecovery;
    }
}
