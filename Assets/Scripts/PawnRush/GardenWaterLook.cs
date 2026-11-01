using UnityEngine;

namespace ChessFight.PawnRush
{
    // Dress material slots, not whole renderers: flowers and stone share meshes with the falls.
    static class GardenWaterLook
    {
        public static void Apply(GameObject garden)
        {
            var water = Resources.Load<Material>("PR_GardenWater");
            var fall = Resources.Load<Material>("PR_GardenWaterfall");
            if (water == null || fall == null)
            {
                Debug.LogError("Royal garden water materials are missing from Resources.");
                return;
            }
            foreach (var renderer in garden.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null) continue;
                    string name = slots[i].name;
                    if (name.StartsWith("G09_")) { slots[i] = water; changed = true; }
                    else if (name.StartsWith("G10_")) { slots[i] = fall; changed = true; }
                }
                if (changed) renderer.sharedMaterials = slots;
            }
        }
    }
}
