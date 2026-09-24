using System;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // The goal. It only reports who arrived; what arriving means (score, round
    // end, qualification) belongs to each game mode's rules.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class FinishZone : MonoBehaviour
    {
        // Can fire more than once per arrival for a multi-collider ragdoll.
        public static event Action<ICharacterDriver> Reached;

        void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var driver = other.GetComponentInParent<ICharacterDriver>();
            if (driver != null) Reached?.Invoke(driver);
        }

        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = new Color(1f, .84f, .2f, .35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
