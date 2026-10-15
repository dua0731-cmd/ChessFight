using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // A thing to take hold of, never to climb (R128, user: "잡기만 해야 하는 영역, 벽을 탈 수 없는 영역"). The
    // ragdoll's grab button also climbs: holding it while pushing into a face steeper than 55 degrees starts a
    // climb, so a pawn pushing G's king statue or dragging a rook kept going up its side instead. The three kinds
    // of surface:
    //   - nothing           climbable (stone blocks, the default for anything steep and static or kinematic)
    //   - GrabOnlySurface   gripped by a hand (and dragged along with it), never climbed, never a ledge to vault:
    //                       things that are pushed and pulled (mini-game statues, carts, knots)
    //   - NoClimbSurface    neither climbed nor gripped (marble walls, railings, team dividers)
    // Every collider on this GameObject is marked; the character asks Marks(). A grab-only face in front also
    // hides climbable faces behind it, as a marble one does.
    [DisallowMultipleComponent]
    public sealed class GrabOnlySurface : MonoBehaviour
    {
        static readonly HashSet<Collider> marked = new HashSet<Collider>();
        readonly List<Collider> mine = new List<Collider>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => marked.Clear();

        public static bool Marks(Collider c) => c != null && marked.Contains(c);

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
