using UnityEngine;

namespace ChessFight.Gameplay
{
    // A rank's pad (8th revision, R50): standing on it records that rank as the
    // highest the character has reached, for its own vacuum tube. Nothing else is
    // shared. The box is the pad's volume, a trigger.
    //
    // It only records (no teleport inside a physics callback, PITFALLS).
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RankPad : MonoBehaviour
    {
        [SerializeField] int rank = 2;

        public int Rank => rank;

        public void Configure(int rank) => this.rank = rank;

        void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var match = QueenHillRaceMatch.Current;
            if (match == null) return;
            var driver = other.GetComponentInParent<ICharacterDriver>();
            if (driver != null) match.Reach(driver, rank);
        }
    }
}
