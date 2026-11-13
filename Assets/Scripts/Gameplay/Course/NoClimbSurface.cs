using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // A face that cannot be climbed (Pawn Rush course 01: team dividers, the team
    // lanes' front walls, island railings, the pit's back wall). The ragdoll climbs
    // anything steeper than 55 degrees, so without this a lane wall is only a
    // suggestion. Every collider on this GameObject is left out of the climb
    // search, hides the climbable faces behind it, and cannot be gripped as a
    // ledge. Standing on its top is unaffected.
    //
    // It only marks colliders; the character asks Blocks(). Shape tells the player
    // which is which: climbable walls wear the block-jointed stone, unclimbable ones
    // the smooth marble (Wall_Climbable / Wall_NoClimb).
    [DisallowMultipleComponent]
    public sealed class NoClimbSurface : MonoBehaviour
    {
        static readonly HashSet<Collider> marked = new HashSet<Collider>();
        readonly List<Collider> mine = new List<Collider>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => marked.Clear();

        public static bool Blocks(Collider c) => c != null && marked.Contains(c);

        void OnEnable()
        {
            GetComponents(mine);
            foreach (var c in mine) marked.Add(c);
        }

        void OnDisable()
        {
            foreach (var c in mine) marked.Remove(c);
            mine.Clear();
        }
    }
}
