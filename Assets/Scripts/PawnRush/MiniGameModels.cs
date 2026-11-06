using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush
{
    // R501: Blender models over a mini-game section (ArtSource/PawnRush/MiniGames/<phase>/, the R499 art standard).
    // Like the garden's concept dressing, a model is only a picture: every collider, door, rope, knot and lamp stays the
    // game's own, and the code-built pictures it replaces stop drawing (their colliders stay). Moving parts ride the
    // game's own transforms (the drawbridge's hinge, the winch's drum), so the game moves them as before.
    //
    // Resources/PawnRushMiniGames/<phase>/<name>.fbx; materials are the phase kit's (phase 1: the garden's GC_ set,
    // Resources/PawnRushGardenConcept/Materials, by name). A missing model leaves the R312 code-built art as it was.
    public static class MiniGameModels
    {
        const string Folder = "PawnRushMiniGames/";
        const string Materials = "PawnRushGardenConcept/Materials/";
        const string EnvName = "MODEL_Env";
        const string RideName = "MODEL_Ride";

        // Called after a section's games are (re)installed and dressed, and after a build's shell.
        public static void Apply(MiniGameSection owner)
        {
            if (owner == null) return;
            if (owner.GameIndex == PawnRushMissions.Drawbridge) Drawbridge(owner);
        }

        // C · 해자 도개교 (ArtSource/PawnRush/MiniGames/Early/c_drawbridge_v1.py).
        static void Drawbridge(MiniGameSection owner)
        {
            var content = owner.transform.Find(MiniGameSection.ContentName);
            var env = Load("Early/C_Drawbridge_Env");
            if (content == null || env == null) return;
            var cache = new Dictionary<string, Material>();
            if (content.Find(EnvName) == null) Hang(content, EnvName, env, cache);
            Hide(content.Find(MiniGamePalaceArt.ShellName));
            foreach (var floor in new[] { "Entry band", "Rear corridor", "Central aisle south", "Central aisle north" })
                Hide(content.Find(floor));

            var deck = Load("Early/C_Drawbridge_Deck");
            var drum = Load("Early/C_Drawbridge_Drum");
            foreach (var station in owner.Stations)
            {
                if (station == null) continue;
                Hide(station.transform.Find("Gate passage"));
                var game = station.Game != null ? station.Game.transform : null;
                if (game == null) continue;
                foreach (var t in game.GetComponentsInChildren<Transform>(true))
                    switch (t.name)
                    {
                        case "Moat wall S look": case "Moat wall N look": case "Moat wall W": case "Moat wall E": case "Moat bottom":
                        case "Bridge tower": case "Winch frame": case "Winch post E": case "Rope lane": case "Lane tick": case "Lane arrow":
                            Hide(t);
                            break;
                    }
                var hinge = Find(game, "Drawbridge hinge");
                if (hinge != null && deck != null)
                {
                    foreach (Transform part in hinge)
                        if (part.name != RideName) Hide(part);
                    if (hinge.Find(RideName) == null) Hang(hinge, RideName, deck, cache);
                }
                var drumAt = Find(game, "Drum");
                if (drumAt != null && drum != null)
                {
                    foreach (Transform part in drumAt)
                        if (part.name != RideName) Hide(part);
                    if (drumAt.Find(RideName) == null) Hang(drumAt, RideName, drum, cache);
                }
            }
        }

        static GameObject Load(string name)
        {
            var model = Resources.Load<GameObject>(Folder + name);
            if (model == null) Debug.LogWarning("[미니게임 모델] 불러오지 못했습니다: Resources/" + Folder + name + ".fbx — 코드로 만든 그림을 그대로 씁니다");
            return model;
        }

        // The model under a pivot turned 180 degrees about y, as the garden's (Blender x, y, z = the parent's x, z, y).
        static void Hang(Transform parent, string name, GameObject model, Dictionary<string, Material> cache)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var copy = Object.Instantiate(model, pivot, false);
            copy.name = model.name;
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] != null) slots[i] = KitMaterial(slots[i], cache);
                renderer.sharedMaterials = slots;
            }
        }

        // "GC | c slate" (Unity imports it as "GC _ c slate") -> Materials/GC_c_slate, as the garden's ConceptMaterial.
        static Material KitMaterial(Material imported, Dictionary<string, Material> cache)
        {
            string name = System.Text.RegularExpressions.Regex.Replace(imported.name.Replace(" (Instance)", ""), "[^A-Za-z0-9]+", "_").Trim('_');
            if (!cache.TryGetValue(name, out var material))
            {
                material = Resources.Load<Material>(Materials + name);
                if (material == null) Debug.LogWarning("[미니게임 모델] 재질이 없습니다: Resources/" + Materials + name);
                cache[name] = material;
            }
            return material != null ? material : imported;
        }

        // Stops `root`'s pictures drawing; colliders and the game's components are untouched.
        static void Hide(Transform root)
        {
            if (root == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
