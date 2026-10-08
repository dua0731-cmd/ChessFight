using System;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // The 8 m drop rule (Pawn Rush course 01 v0.2): a course whose floors are stacked must not let
    // a fall from the upper floor land on the part of the course below, already run. The character
    // remembers the height of the last floor it stood on; once it is more than maxDrop under it, it
    // is reported before it lands, and whoever simulates it puts it back on its last checkpoint
    // (PlaytestSpawner). Stacked floors are 9 m apart, so the report always comes first.
    //
    // A wall being climbed is not a floor: the remembered height stays at the floor under the wall,
    // so falling off a wall back to its foot is never a drop. Short planned drops (a 3 m pit, a tower
    // ledge) stay under the limit.
    //
    // Goes on the character (any ICharacterDriver; the hips are its FollowTarget). It only reports.
    public sealed class FallDistanceRespawn : MonoBehaviour
    {
        [Tooltip("Metres under the last floor stood on that count as a fall.")]
        [SerializeField] float maxDrop = 8f;
        [Tooltip("Seconds from the report to the respawn. 0: before the character can land below.")]
        [SerializeField] float respawnDelay;
        [Tooltip("How far under the hips a floor still counts as stood on.")]
        [SerializeField] float footReach = .9f;

        public float MaxDrop => maxDrop;
        public float RespawnDelay => respawnDelay;
        // The last floor's height, NaN until it has stood somewhere.
        public float FloorHeight { get; private set; } = float.NaN;

        public static event Action<ICharacterDriver, FallDistanceRespawn> Fell;

        ICharacterDriver driver;
        Vector3 lastHips;
        bool reported;
        readonly RaycastHit[] hits = new RaycastHit[16];

        void Awake()
        {
            driver = GetComponentInChildren<ICharacterDriver>();
            if (driver == null) driver = GetComponentInParent<ICharacterDriver>();
        }

        void FixedUpdate()
        {
            var hips = driver?.FollowTarget;
            if (hips == null) return;
            Vector3 p = hips.position;
            // A respawn or any teleport moves the hips metres in one step: start over from there.
            if ((p - lastHips).sqrMagnitude > 16f) Forget();
            lastHips = p;
            if (Standing(p, out float floor))
            {
                FloorHeight = floor;
                reported = false;
            }
            else if (!reported && !float.IsNaN(FloorHeight) && p.y < FloorHeight - maxDrop)
            {
                reported = true;
                Fell?.Invoke(driver, this);
            }
        }

        public void Forget()
        {
            FloorHeight = float.NaN;
            reported = false;
        }

        // A walkable surface right under the hips that is not part of this character.
        bool Standing(Vector3 hips, out float floor)
        {
            floor = 0f;
            int n = Physics.RaycastNonAlloc(hips + Vector3.up * .1f, Vector3.down, hits, footReach + .1f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider.transform.IsChildOf(transform) || h.normal.y < .5f || h.distance >= best) continue;
                var body = h.collider.attachedRigidbody;
                if (body != null && !body.isKinematic) continue;
                best = h.distance;
                floor = h.point.y;
                found = true;
            }
            return found;
        }
    }
}
