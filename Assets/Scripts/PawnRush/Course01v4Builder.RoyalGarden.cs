using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        const string GardenModel = "PawnRushGarden/RoyalWaterGarden_v21";
        static void RoyalGarden(CourseBuilder b)
        {
            var model = Resources.Load<GameObject>(GardenModel);
            var motionText = Resources.Load<TextAsset>(GardenModel + "_Motion");
            if (model == null || motionText == null) { Debug.LogError("Royal garden v21 model/motion missing: " + GardenModel); return; }
            var take = JsonUtility.FromJson<SampledGardenMotion.Take>(motionText.text);
            var pivot = new GameObject("Royal garden v21").transform;
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
                bool untextured = role.source.StartsWith("V1") || role.source.StartsWith("V20")
                    || (role.source.StartsWith("V21") && !role.source.Contains("tree"));
                if (renderer != null && untextured)
                {
                    // Supplied assets and derived structures stay untextured; the R277 trees keep their trunk/leaf colours.
                    var slots = renderer.sharedMaterials;
                    if (slots.Length == 0) renderer.sharedMaterial = clay;
                    else { for (int i = 0; i < slots.Length; i++) slots[i] = clay; renderer.sharedMaterials = slots; }
                }
            }
            GardenWaterLook.Apply(garden);
            // Create kinematic roots before their moving mesh colliders.
            garden.AddComponent<SampledGardenMotion>().Configure(motionText, b.parent, nodes);
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
