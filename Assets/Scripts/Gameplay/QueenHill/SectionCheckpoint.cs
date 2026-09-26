using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // A section's landing (DESIGN §3.3 C): standing on it makes it a character's
    // own checkpoint, and it is where characters come back to when the rules say
    // this section (QueenHillMatch.TryRespawn). The box is the landing's volume, a
    // trigger; characters come back at `spawn`, or on the middle of its floor.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SectionCheckpoint : MonoBehaviour
    {
        [SerializeField] int section = 1;
        [SerializeField] Transform spawn;

        static readonly List<SectionCheckpoint> active = new List<SectionCheckpoint>();
        BoxCollider box;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        public static IReadOnlyList<SectionCheckpoint> All => active;

        public int Section => section;

        public Vector3 SpawnPosition
        {
            get
            {
                if (spawn != null) return spawn.position;
                var b = Box.bounds;
                return new Vector3(b.center.x, b.min.y, b.center.z);
            }
        }

        public Quaternion SpawnRotation => spawn != null ? spawn.rotation : transform.rotation;

        // Nearby spots to come back at, in the spawn's own frame: the spawn itself first. Two characters
        // put back on the same spot in the same frame knocked each other off the landing.
        static readonly Vector3[] Around =
        {
            Vector3.zero, new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f), new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0f),
            new Vector3(1f, 0f, 1f), new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, -1f), new Vector3(-1f, 0f, -1f),
        };

        // The spawn, or the nearest spot around it with nothing standing there yet.
        public Vector3 FreeSpawnPosition()
        {
            Vector3 origin = SpawnPosition;
            Quaternion frame = SpawnRotation;
            foreach (var offset in Around)
            {
                Vector3 at = origin + frame * offset;
                if (!Physics.CheckCapsule(at + Vector3.up * 0.45f, at + Vector3.up * 1f, 0.35f, ~0, QueryTriggerInteraction.Ignore))
                    return at;
            }
            return origin;
        }

        BoxCollider Box => box != null ? box : (box = GetComponent<BoxCollider>());

        public void Configure(int section, Transform spawn)
        {
            this.section = section;
            this.spawn = spawn;
        }

        void Reset() => GetComponent<BoxCollider>().isTrigger = true;
        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        void OnTriggerEnter(Collider other)
        {
            var match = QueenHillMatch.Current;
            if (match == null) return;
            var driver = other.GetComponentInParent<ICharacterDriver>();
            if (driver != null) match.Reach(driver, section);
        }
    }
}
