using UnityEngine;

namespace ChessFight.PawnRush
{
    // Dress material slots, not whole renderers: flowers and stone share meshes with the falls.
    static class GardenWaterLook
    {
        // R279: leaves, flowers and lily pads are matte (they read as candy with the stone's sheen).
        static readonly string[] Foliage = { "잎", "꽃", "leaf", "flower" };

        public static void Apply(GameObject garden)
        {
            var water = Resources.Load<Material>("PR_GardenWater");
            var fall = Resources.Load<Material>("PR_GardenWaterfall");
            var sheet = Resources.Load<Material>("PR_GardenFallSheet");
            var spray = Resources.Load<Material>("PR_GardenSpray");
            if (water == null || fall == null || sheet == null || spray == null)
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
                    if (name.StartsWith("G09_") || name.Contains("calm water")) { slots[i] = water; changed = true; }
                    else if (name.StartsWith("V23 | waterfall sheet")) { slots[i] = sheet; changed = true; }
                    else if (name.StartsWith("V23 | fall spray")) { slots[i] = spray; changed = true; }
                    else if (name.StartsWith("G10_") || name == "V07 | waterfall") { slots[i] = fall; changed = true; }
                    else
                    {
                        slots[i] = Arena.Lit(name, slots[i].color, 0, IsFoliage(name) ? .92f : .7f); changed = true;
                    }
                }
                if (changed) renderer.sharedMaterials = slots;
            }
        }

        // R281: the waterfalls and their spray by the object they belong to, so they get their shaders even if an
        // importer renames the material slots.
        public static void ApplyBySource(SampledGardenMotion.Take take, System.Collections.Generic.Dictionary<string, Transform> nodes)
        {
            var sheet = Resources.Load<Material>("PR_GardenFallSheet");
            var spray = Resources.Load<Material>("PR_GardenSpray");
            if (sheet == null || spray == null) return;
            foreach (var role in take.meshes)
            {
                Material use = role.source.StartsWith("V25 | fall") ? sheet : role.source.StartsWith("V23 | fall spray") ? spray : null;
                if (use == null || !nodes.TryGetValue(role.name, out var node) || !node.TryGetComponent<Renderer>(out var renderer)) continue;
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = use;
                renderer.sharedMaterials = slots.Length > 0 ? slots : new[] { use };
            }
        }

        static bool IsFoliage(string name)
        {
            foreach (var word in Foliage) if (name.Contains(word)) return true;
            return false;
        }
    }
}
