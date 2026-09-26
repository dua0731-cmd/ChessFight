using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // Where a pawn may throw its grappling hook from (Queen of the Hill: the broken
    // end of its team's bridge, DESIGN §4.1). The hook itself sticks in anything it
    // hits; this only says where the throw may start.
    //
    // A level with no zones at all allows the throw anywhere (the ragdoll lab), so
    // a level only needs zones once it wants to limit the hook.
    //
    // The box is read as axis-aligned: rotate the zone's parent, not the zone.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class HookStartZone : MonoBehaviour
    {
        static readonly List<HookStartZone> active = new List<HookStartZone>();
        BoxCollider box;
        BoxCollider Box => box != null ? box : (box = GetComponent<BoxCollider>());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        void Reset() => GetComponent<BoxCollider>().isTrigger = true;
        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        // May a hook be thrown from this point (usually the thrower's hips)?
        public static bool Allows(Vector3 point)
        {
            if (active.Count == 0) return true;
            foreach (var zone in active)
                if (zone != null && zone.Box.bounds.Contains(point)) return true;
            return false;
        }
    }
}
