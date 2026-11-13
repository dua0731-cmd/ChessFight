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
        float localTop;
        void Awake()
        {
            surface = GetComponent<Collider>();
            localTop = ((MeshCollider)surface).sharedMesh.bounds.max.y;
        }
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
                if (Mathf.Abs(Vector3.Dot(contact.normal, transform.up)) > .55f && transform.InverseTransformPoint(contact.point).y >= localTop - .12f)
                { contacts[collision.collider] = pawn; return; }
            }
            contacts.Remove(collision.collider);
        }
        public void Count(out int left, out int right)
        {
            left = right = 0;
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
            // A straddling ragdoll counts once, on the side containing its hips.
            foreach (var pawn in unique)
                if (transform.InverseTransformPoint(pawn.Hips.position).x <= 0) left++;
                else right++;
        }
        public void ClearContacts() { contacts.Clear(); }
        void OnDisable() { contacts.Clear(); }
    }
}
