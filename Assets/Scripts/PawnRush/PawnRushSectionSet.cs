using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The section prefabs a round may draw, slot by slot (Assets/Settings/PawnRushSections.asset): seven slots
    // in the round's order (PawnRushSections: obstacle, mini-game, obstacle, mini-game, obstacle, mini-game,
    // final), each a list of section prefabs (roots carry PawnRushSection). Add a new section by dropping its
    // prefab into its slot's list. A prefab listed in several slots (a mini-game that may come in any of the
    // three) is drawn at most once a round while its slot has another.
    [CreateAssetMenu(menuName = "ChessFight/Pawn Rush/Section Set", fileName = "PawnRushSections")]
    public sealed class PawnRushSectionSet : ScriptableObject
    {
        [Serializable]
        public sealed class Slot
        {
            public string name;
            public PawnRushSection.SlotKind kind;
            public GameObject[] prefabs = new GameObject[0];
        }

        [SerializeField] Slot[] slots = DefaultSlots();

        public int SlotCount => slots.Length;
        public Slot this[int i] => slots[i];

        // For the Core draw: every distinct prefab gets one id (its index in `prefabs`), every slot the ids it
        // may take.
        public int[][] Options(List<GameObject> prefabs)
        {
            prefabs.Clear();
            var options = new int[slots.Length][];
            for (int s = 0; s < slots.Length; s++)
            {
                var ids = new List<int>();
                var list = slots[s] != null ? slots[s].prefabs : null;
                if (list != null)
                    foreach (var prefab in list)
                    {
                        if (prefab == null) continue;
                        int id = prefabs.IndexOf(prefab);
                        if (id < 0)
                        {
                            id = prefabs.Count;
                            prefabs.Add(prefab);
                        }
                        if (!ids.Contains(id)) ids.Add(id);
                    }
                options[s] = ids.ToArray();
            }
            return options;
        }

        public static Slot[] DefaultSlots()
        {
            var list = new Slot[PawnRushSections.SlotCount];
            for (int i = 0; i < list.Length; i++)
                list[i] = new Slot
                {
                    name = PawnRushSections.SlotNames[i],
                    kind = (PawnRushSection.SlotKind)PawnRushSections.Kind[i],
                };
            return list;
        }
    }
}
