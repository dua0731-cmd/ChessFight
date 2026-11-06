using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // Evaluate all zones as a union, avoiding overlapping-zone exit races.
    public sealed class AbilityZone : MonoBehaviour
    {
        public KingRushSection section = KingRushSection.Blue1;
        public Vector3 size = new Vector3(20, 8, 16);
        public bool Contains(Vector3 position) => new Bounds(transform.position, size).Contains(position);
    }
}
