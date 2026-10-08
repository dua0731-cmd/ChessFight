using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // The bottom of a cliff (Pawn Rush course 01 puts one 8 m under every module's
    // entry floor). A character that falls in is reported once; whoever simulates
    // it puts it back on its last checkpoint after RespawnDelay (the offline
    // playtest does, PlaytestSpawner). Like WaterZone it only reports and never
    // moves anyone itself, but nothing floats in it.
    //
    // The box is read as axis-aligned: rotate the volume's parent, not the volume.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class KillVolume : MonoBehaviour
    {
        [Tooltip("Seconds between falling in and coming back at the last checkpoint.")]
        [SerializeField] float respawnDelay = 1.5f;
        public float RespawnDelay => respawnDelay;

        public static event Action<ICharacterDriver, KillVolume> Entered;

        readonly Dictionary<ICharacterDriver, int> inside = new Dictionary<ICharacterDriver, int>();
        readonly List<ICharacterDriver> gone = new List<ICharacterDriver>();

        public Bounds Bounds => GetComponent<BoxCollider>().bounds;

        void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        // Once per fall, not once per limb.
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
            Gizmos.color = new Color(.95f, .25f, .2f, .18f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
