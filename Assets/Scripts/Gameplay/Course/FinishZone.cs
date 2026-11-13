using System;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // The goal. It only reports who arrived; what arriving means (score, round
    // end, qualification) belongs to each game mode's rules.
    //
    // Any trigger shape: a line across a course (box) or the crown circle on top of
    // the Pawn Rush tower (a cylinder). With hipsOnly, a ragdoll counts when its hips
    // (the driver's FollowTarget) are in, not when a hand reaches over the edge.
    [RequireComponent(typeof(Collider))]
    public sealed class FinishZone : MonoBehaviour
    {
        [Tooltip("Count a character only when its FollowTarget (a ragdoll's hips) enters.")]
        [SerializeField] bool hipsOnly;

        public void Configure(bool hipsOnly) => this.hipsOnly = hipsOnly;

        // Can fire more than once per arrival for a multi-collider ragdoll.
        public static event Action<ICharacterDriver> Reached;

        void Awake() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var driver = other.GetComponentInParent<ICharacterDriver>();
            if (driver == null) return;
            if (hipsOnly && driver.FollowTarget != null && other.transform != driver.FollowTarget &&
                (other.attachedRigidbody == null || other.attachedRigidbody.transform != driver.FollowTarget)) return;
            Reached?.Invoke(driver);
        }

        void OnDrawGizmos()
        {
            var collider = GetComponent<Collider>();
            if (collider == null) return;
            Gizmos.color = new Color(1f, .84f, .2f, .35f);
            if (collider is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
            }
            else Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
        }
    }
}
