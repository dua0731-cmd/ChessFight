using UnityEngine;

namespace ChessFight.Gameplay
{
    // Where a player enters the course: place it ON the ground, where the feet go.
    // Index 0 is where the offline playtest spawns; a match will hand out the rest
    // by roster slot.
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField] int index;
        [Tooltip("0 = BLUE, 1 = ORANGE.")]
        [SerializeField, Range(0, 1)] int team;

        public int Index => index;
        public int Team => team;

        void OnDrawGizmos()
        {
            Gizmos.color = team == 0 ? new Color(.28f, .72f, 1f) : new Color(1f, .51f, .3f);
            Gizmos.DrawWireSphere(transform.position, .5f);
            Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
        }
    }
}
