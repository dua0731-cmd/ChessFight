using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The entry band of a mission plaza: tells who walked in, once per entry (the picture card,
    // mini-game spec v0.1: the game's name and one line, 3 s, when the plaza is entered).
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PlazaEntry : MonoBehaviour
    {
        [SerializeField, Range(1, 2)] int plaza = 1;

        public int Plaza => plaza;

        public static event Action<PlazaEntry, ICharacterDriver> Entered;

        readonly Dictionary<ICharacterDriver, int> inside = new Dictionary<ICharacterDriver, int>();

        public void Configure(int plaza) => this.plaza = plaza;

        void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var driver = other.GetComponentInParent<ICharacterDriver>();
            if (driver == null) return;
            inside.TryGetValue(driver, out int count);
            inside[driver] = count + 1;
            if (count == 0) Entered?.Invoke(this, driver);
        }

        void OnTriggerExit(Collider other)
        {
            var driver = other.GetComponentInParent<ICharacterDriver>();
            if (driver == null || !inside.TryGetValue(driver, out int count)) return;
            if (count <= 1) inside.Remove(driver);
            else inside[driver] = count - 1;
        }
    }
}
