using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The last 8 m before the finish where grabbing is off (Pawn Rush design v1.0). Only
    // the place for now: the rule that reads it comes with the finish rules.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class NoGrabZone : MonoBehaviour
    {
        static readonly List<NoGrabZone> active = new List<NoGrabZone>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        void Awake() => GetComponent<BoxCollider>().isTrigger = true;
        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        public static bool Contains(Vector3 point)
        {
            foreach (var zone in active)
                if (zone != null && zone.GetComponent<BoxCollider>().bounds.Contains(point)) return true;
            return false;
        }

        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = new Color(.9f, .5f, .1f, .15f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
