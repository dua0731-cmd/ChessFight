using System;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // A trigger volume that becomes the respawn point once a character passes
    // through. Only a higher order replaces the current one, so running back
    // through an earlier checkpoint does not set progress back.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Checkpoint : MonoBehaviour
    {
        [SerializeField] int order;
        public int Order => order;

        // The floor of the trigger volume, so a respawn lands on the ground rather
        // than in mid-air at the volume's centre.
        public Vector3 RespawnPosition
        {
            get
            {
                var box = GetComponent<BoxCollider>();
                return box == null ? transform.position
                    : transform.TransformPoint(box.center - Vector3.up * (box.size.y * .5f));
            }
        }

        // A ragdoll has several colliders, so this can fire more than once per pass.
        // Listeners must be idempotent.
        public static event Action<ICharacterDriver, Checkpoint> Reached;

        void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var driver = other.GetComponentInParent<ICharacterDriver>();
            if (driver != null) Reached?.Invoke(driver, this);
        }

        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = new Color(.45f, .9f, .55f, .35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
