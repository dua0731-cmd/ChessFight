using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    // Count occupants by physical top contact, independent of allegiance or ragdoll state.
    // Keep per-collider contacts so one limb leaving does not remove the whole pawn.
    public sealed class ChessTimerPlate : MonoBehaviour
    {
        readonly Dictionary<Collider, RagdollPawn> contacts = new Dictionary<Collider, RagdollPawn>();
        readonly HashSet<RagdollPawn> unique = new HashSet<RagdollPawn>();
        readonly List<Collider> stale = new List<Collider>();
        Collider surface;
        void Awake() { surface = GetComponent<Collider>(); }
        void OnCollisionEnter(Collision collision) { Track(collision); }
        void OnCollisionStay(Collision collision) { Track(collision); }
        void OnCollisionExit(Collision collision) { contacts.Remove(collision.collider); }
        void Track(Collision collision)
        {
            RagdollPawn pawn;
            if (!RagdollPawn.ColliderOwner.TryGetValue(collision.collider, out pawn)) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                if (Mathf.Abs(contact.normal.y) > .55f && contact.point.y >= surface.bounds.max.y - .12f)
                { contacts[collision.collider] = pawn; return; }
            }
            contacts.Remove(collision.collider);
        }
        public int Occupants
        {
            get
            {
                unique.Clear(); stale.Clear();
                var contactBounds = surface.bounds; contactBounds.Expand(.15f);
                foreach (var pair in contacts)
                {
                    if (pair.Key == null || !pair.Key.enabled || !pair.Key.gameObject.activeInHierarchy ||
                        pair.Value == null || !pair.Value.isActiveAndEnabled || !pair.Key.bounds.Intersects(contactBounds))
                        stale.Add(pair.Key);
                    else unique.Add(pair.Value);
                }
                foreach (var key in stale) contacts.Remove(key);
                return unique.Count;
            }
        }
        public void ClearContacts() { contacts.Clear(); }
        void OnDisable() { contacts.Clear(); }
    }
}
