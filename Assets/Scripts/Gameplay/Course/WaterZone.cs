using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // Water: the sea around the Queen of the Hill tower, or any fall that should
    // end a climb. A character that touches it with any part is reported once
    // through Entered; what happens next is the mode's business (stage one: back
    // to a checkpoint after RespawnDelay; stage two: out of the round).
    //
    // It only reports. It does not move anyone: the machine that simulates the
    // characters decides, and ICharacterDriver.Teleport lets go of everything.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class WaterZone : MonoBehaviour
    {
        [Tooltip("Seconds a character spends in the water before it is put back.")]
        [SerializeField] float respawnDelay = 2f;
        public float RespawnDelay => respawnDelay;

        // Once per dip, not once per limb: a ragdoll is a dozen colliders, so they
        // are counted in and out and only the first one in is reported.
        public static event Action<ICharacterDriver, WaterZone> Entered;

        readonly Dictionary<ICharacterDriver, int> inside = new Dictionary<ICharacterDriver, int>();
        readonly List<ICharacterDriver> gone = new List<ICharacterDriver>();

        void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var driver = other.GetComponentInParent<ICharacterDriver>();
            if (driver == null) return;
            inside.TryGetValue(driver, out int count);
            inside[driver] = count + 1;
            if (count == 0) Entered?.Invoke(driver, this);
        }

        void OnTriggerExit(Collider other)
        {
            var driver = other.GetComponentInParent<ICharacterDriver>();
            if (driver == null || !inside.TryGetValue(driver, out int count)) return;
            if (count <= 1) inside.Remove(driver);
            else inside[driver] = count - 1;
        }

        // A destroyed character never sends its exits; forget it so the table
        // does not grow over a long session.
        void FixedUpdate()
        {
            if (inside.Count == 0) return;
            gone.Clear();
            foreach (var driver in inside.Keys)
                if (driver is UnityEngine.Object o && o == null) gone.Add(driver);
            foreach (var driver in gone) inside.Remove(driver);
        }

        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = new Color(.2f, .45f, .95f, .3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
