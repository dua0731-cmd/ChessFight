using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R282: one fixed name (and GUID) for every version, so a pull never leaves a saved scene pointing at a deleted
        // file; the Blender export overwrites it.
        const string GardenModel = "PawnRushGarden/RoyalWaterGarden";
        const string ObstaclePlaceholder = "V19 | V33 obstacle | ";
        static readonly string[] Coloured = { "tree", "water", "bush", "flower", "fall", "spray", "lily", "shadow" };

        // Blender layers V1x-V26 are untextured clay, except the parts that carry their own colour or shader (R283: the
        // outlets' dark mouths keep their near-black).
        static bool Untextured(string source)
        {
            if (source.StartsWith("V1") || source.StartsWith("V20")) return true;
            if (!source.StartsWith("V21") && !source.StartsWith("V22") && !source.StartsWith("V23") && !source.StartsWith("V24") && !source.StartsWith("V25") && !source.StartsWith("V26")) return false;
            foreach (var word in Coloured) if (source.Contains(word)) return false;
            return true;
        }
        // R307 (승규 11-03): the v31 graybox parts the garden's models stand in for draw nothing but keep their colliders.
        // R309 ("지금 코스는 확정이야"): build_v33.py dressed the rest of the layout too (the canal, the landings, the lock,
        // the dyke, the boats, the shortcut, the spin bars, the tunnels, the exit), so only water, currents and the
        // fountain still draw from code.
        static readonly string[] CoveredByModel =
        {
            "Garden wall", "Garden tower", "Garden water (respawn)", "Garden terrace", "Fountain plaza", "Plaza north apron",
            "Plaza kerb", "Start yard", "Gate yard", "Gate arch", "Drum",
            "Canal walk", "West ramp", "West landing", "East landing", "Lock floor", "Lock wall", "Lock gate ", "Gate panel",
            "Robot dyke", "Dyke wall N", "Reservoir wall S", "Boat tunnel roof", "Boat tunnel eave", "Boat tunnel wall",
            "Geyser rim", "Geyser grate", "Step down ", "Mini-game entrance band", "Exit arch", "Garden island",
        };
        static readonly string[] CoveredExactly = { "Boarding quay", "Exit terrace", "Shortcut post K4", "Hull", "Deck", "Post", "Post rim", "Pad", "Pad rim", "Bar", "Hub" };
        // The v31 robot arms and their gantries; the garden's own robot arms swing over the dyke instead.
        static readonly string[] ReplacedByModel = { "Robot arm ", "Robot gantry ", "Robot axle " };

        static bool Covered(string name)
        {
            foreach (var exact in CoveredExactly) if (name == exact) return true;
            // The platforms', posts' and lotus pads' stems, and every rail.
            if (name.EndsWith(" stem") || name.Contains(" rail ")) return true;
            foreach (var prefix in CoveredByModel) if (name.StartsWith(prefix)) return true;
            return false;
        }

        static void RoyalGarden(CourseBuilder b)
        {
            // R307: main's v31 layout (Early_1-1_RoyalWaterGarden, built from code) under the garden's models, which
            // build_v32.py moved onto it.
            var prefab = (b.kit as Course01Kit)?.royalWaterGarden;
            if (prefab == null) throw new System.Exception("Course01Kit.royalWaterGarden이 비어 있습니다");
            // R309: built inactive, so the movers' ObstacleMotion.Awake has not moved them yet when the models are hung on
            // them at their rest pose; switched on at the end.
            var holder = new GameObject("Royal garden layout");
            holder.transform.SetParent(b.parent, false);
            holder.SetActive(false);
            try { Assemble(b, prefab, holder.transform); }
            finally { holder.SetActive(true); }
        }

        static void Assemble(CourseBuilder b, GameObject prefab, Transform holder)
        {
            var section = CourseBuilder.Spawn(prefab, holder);
            section.transform.localPosition = Vector3.zero;
            section.transform.localRotation = Quaternion.identity;
            var layout = section.GetComponent<RoyalWaterGarden>();
            if (!layout.HasContent) RoyalWaterGardenBuilder.Build(layout);
            var content = section.transform.Find(RoyalWaterGarden.ContentName);
            var parts = new Dictionary<string, Transform>();
            foreach (var t in content.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                bool replaced = false;
                foreach (var prefix in ReplacedByModel) if (t.name.StartsWith(prefix)) replaced = true;
                if (replaced) { Object.DestroyImmediate(t.gameObject); continue; }
                if (Covered(t.name) && t.TryGetComponent<Renderer>(out var cover)) cover.enabled = false;
                parts[t.name] = t;
            }
            // The water the models show lies 0.9 m under v31's (y -1.7, not -0.8): falling in ends the try at the surface.
            if (parts.TryGetValue("Kill volume (garden water)", out var drown)) drown.localPosition += Vector3.down * .9f;
            var water = Resources.Load<Material>("PR_GardenWater");
            if (water != null && parts.TryGetValue("Reservoir water (respawn)", out var reservoir) && reservoir.TryGetComponent<Renderer>(out var surface))
                surface.sharedMaterial = water;

            var model = Resources.Load<GameObject>(GardenModel);
            var motionText = Resources.Load<TextAsset>(GardenModel + "_Motion");
            if (model == null || motionText == null)
            {
                Debug.LogError("[정원] 모델/모션을 불러오지 못했습니다: Resources/" + GardenModel + ".fbx, " + GardenModel + "_Motion.json");
#if UNITY_EDITOR
                // A file of a few hundred bytes is a Git LFS pointer: the real 70 MB model was not downloaded.
                var path = "Assets/Resources/" + GardenModel + ".fbx";
                if (System.IO.File.Exists(path) && new System.IO.FileInfo(path).Length < 4096)
                    Debug.LogError("[정원] " + path + "이(가) Git LFS 포인터 파일입니다(실제 모델이 내려받아지지 않음). GitHub Desktop에서 다시 Pull 하거나 'git lfs pull'을 실행한 뒤 Unity에서 그 파일을 Reimport 하세요.");
#endif
                return;
            }
            var take = JsonUtility.FromJson<SampledGardenMotion.Take>(motionText.text);
            var pivot = new GameObject("Royal garden").transform;
            pivot.SetParent(b.parent, false);
            pivot.localRotation = Quaternion.Euler(0, 180, 0);
            var garden = Object.Instantiate(model, pivot, false);
            garden.name = "Royal garden animated model";
            var nodes = new Dictionary<string, Transform>();
            foreach (var t in garden.GetComponentsInChildren<Transform>(true)) nodes[t.name] = t;
            var clay = Arena.Lit("Garden untextured clay", new Color(.54f, .54f, .54f), 0, .78f);
            // R309 (승규 11-03): the obstacles build_v33.py made are stand-ins for assets to come, so they show in orange.
            var placeholder = Arena.Lit("Garden obstacle placeholder", new Color(.93f, .42f, .08f), 0, .7f);
            foreach (var role in take.meshes)
            {
                if (!nodes.TryGetValue(role.name, out var node)) continue;
                var renderer = node.GetComponent<Renderer>();
                if (renderer != null && Untextured(role.source))
                {
                    var flat = role.source.StartsWith(ObstaclePlaceholder) ? placeholder : clay;
                    // Supplied assets and derived structures stay untextured; trees, bushes, flowers and water keep theirs.
                    var slots = renderer.sharedMaterials;
                    if (slots.Length == 0) renderer.sharedMaterial = flat;
                    else { for (int i = 0; i < slots.Length; i++) slots[i] = flat; renderer.sharedMaterials = slots; }
                }
            }
            GardenWaterLook.Apply(garden);
            GardenWaterLook.ApplyBySource(take, nodes);
            // Create kinematic roots before their moving mesh colliders.
            garden.AddComponent<SampledGardenMotion>().Configure(motionText, b.parent, nodes);
            // R279: boats' wakes and the falls' landing foam on the water shader.
            garden.AddComponent<GardenWaterInteraction>().Configure(take, b.parent, nodes);
            // R307: each drum rides its v31 bobbing platform. R309: so do the boats, sluice gate panels, posts, lotus pads and
            // spin bar arms on their code movers ("Spin bar 3/Arm 2": a child of the named part). Every mover is still at
            // its rest pose, the pose the models were placed on.
            foreach (var role in take.meshes)
            {
                if (string.IsNullOrEmpty(role.attach) || !nodes.TryGetValue(role.name, out var node)) continue;
                var path = role.attach.Split(new[] { '/' }, 2);
                if (!parts.TryGetValue(path[0], out var target)) continue;
                if (path.Length > 1) target = target.Find(path[1]);
                if (target == null) { Debug.LogWarning("[정원] 붙일 곳이 없습니다: " + role.attach); continue; }
                node.SetParent(target, true);
            }
            foreach (var role in take.meshes)
            {
                if (role.collider == "none" || !nodes.TryGetValue(role.name, out var node)) continue;
                var filter = node.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                if (role.collider == "box")
                {
                    var box = node.gameObject.AddComponent<BoxCollider>(); box.center = filter.sharedMesh.bounds.center; box.size = filter.sharedMesh.bounds.size;
                }
                else node.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                // R277: an invisible smooth stand-in collides instead of the grooved decorative plaza.
                if (role.collider == "proxy" && node.TryGetComponent<Renderer>(out var hidden)) hidden.enabled = false;
            }
        }
    }
}
