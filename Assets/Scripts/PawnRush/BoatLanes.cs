using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The royal water garden's boat lanes (v31 beat 6): four lanes across the reservoir, two boats each. When the
    // round is set (ISectionPart), the section's seed picks every lane's way and speed (RoyalWaterGardenRules.
    // RollBoatLanes: at least one lane each way, 1.6 / 2.0 / 2.4 m/s) and the boats take them. The same seed gives
    // every PC the same lanes, and each boat's place is a function of its lane and the shared clock: nothing is sent.
    public sealed class BoatLanes : MonoBehaviour, ISectionPart
    {
        [SerializeField] GardenBoat[] boats = new GardenBoat[0];
        [Tooltip("The lane (0 = south) of each boat above.")]
        [SerializeField] int[] laneOf = new int[0];

        RoyalWaterGardenRules.BoatLane[] lanes;

        public RoyalWaterGardenRules.BoatLane[] Lanes => lanes;

        // For the builder: on an inactive object, before Awake.
        public void Configure(GardenBoat[] boats, int[] laneOf)
        {
            this.boats = boats;
            this.laneOf = laneOf;
        }

        public void OnSection(PawnRushSection section)
        {
            lanes = RoyalWaterGardenRules.RollBoatLanes(section.Seed);
            for (int i = 0; i < boats.Length && i < laneOf.Length; i++)
                if (boats[i] != null && laneOf[i] >= 0 && laneOf[i] < lanes.Length) boats[i].SetLane(lanes[laneOf[i]]);
        }
    }
}
