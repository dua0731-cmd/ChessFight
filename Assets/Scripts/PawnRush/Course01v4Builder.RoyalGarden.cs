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
        static void RoyalGarden(CourseBuilder b)
        {
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
            foreach (var role in take.meshes)
            {
                if (!nodes.TryGetValue(role.name, out var node)) continue;
                var renderer = node.GetComponent<Renderer>();
                if (renderer != null && Untextured(role.source))
                {
                    // Supplied assets and derived structures stay untextured; trees, bushes, flowers and water keep theirs.
                    var slots = renderer.sharedMaterials;
                    if (slots.Length == 0) renderer.sharedMaterial = clay;
                    else { for (int i = 0; i < slots.Length; i++) slots[i] = clay; renderer.sharedMaterials = slots; }
                }
            }
            GardenWaterLook.Apply(garden);
            GardenWaterLook.ApplyBySource(take, nodes);
            // Create kinematic roots before their moving mesh colliders.
            garden.AddComponent<SampledGardenMotion>().Configure(motionText, b.parent, nodes);
            // R279: boats' wakes and the falls' landing foam on the water shader.
            garden.AddComponent<GardenWaterInteraction>().Configure(take, b.parent, nodes);
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
            b.Trigger<KillVolume>("Moat (fall)", V(-46.5f, -3f, -58.5f), V(46.5f, -1.45f, 58.5f));
            b.Trigger<KillVolume>("Reservoir (fall)", V(-18f, 2.2f, 31f), V(18f, 3.15f, 59f));
        }
    }
}
