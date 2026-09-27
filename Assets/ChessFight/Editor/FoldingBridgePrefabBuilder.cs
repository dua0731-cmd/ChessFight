using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChessFight.ProtectKing.Editor
{
    public static class FoldingBridgePrefabBuilder
    {
        public const string Path = ObstaclePrefabBuilder.Folder + "/12_FoldingBridge.prefab";
        [MenuItem("CHESS FIGHT/Obstacles/Create Folding Bridge Prefab (No Map Placement)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Path) != null) { Debug.Log("Folding bridge already exists; preserving edits."); return; }
            var original = SceneManager.GetActiveScene(); bool dirty = original.isDirty;
            var temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temporary);
            try
            {
                var root = new GameObject("FoldingBridge"); var context = root.AddComponent<ObstacleContext>();
                var bridge = root.AddComponent<FoldingBridge>(); bridge.context = context;
                for (int side = -1; side <= 1; side += 2)
                {
                    string label = side < 0 ? "Left" : "Right";
                    var pivot = Group(label + "Pivot", root.transform, new Vector3(side * 4, 0, 0));
                    var rb = pivot.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
                    rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    if (side < 0) bridge.LeftPivot = rb; else bridge.RightPivot = rb;
                    var panel = Box(label + "Bridge", pivot, new Vector3(-side * 1.995f, -.15f, 0), new Vector3(3.89f, .3f, 6), "Stone", true);
                    for (int x = 0; x < 4; x++) for (int z = 0; z < 6; z++)
                        Box("Checker", pivot, new Vector3(-side * (.55f + x * .97f), .012f, -2.5f + z), new Vector3(.94f, .02f, .97f), (x + z) % 2 == 0 ? "BoardLight" : "BoardDark", false);
                    for (int edge = -1; edge <= 1; edge += 2)
                        Box("Panel edge", pivot, new Vector3(-side * 2, .025f, edge * 2.96f), new Vector3(3.88f, .025f, .08f), "CheckpointGold", false);
                    Box(label + "Landing", root.transform, new Vector3(side * 5.55f, -.15f, 0), new Vector3(3, .3f, 6), "BoardDark", true);
                    Box(label + "Hinge marker", root.transform, new Vector3(side * 4, -.2f, 0), new Vector3(.12f, .12f, 6), "CheckpointGold", false);
                }
                var warning = Group("WarningVisual", root.transform, Vector3.zero);
                bridge.WarningVisuals = new Renderer[2];
                for (int i = 0; i < 2; i++)
                    bridge.WarningVisuals[i] = Box("Warning strip " + i, warning, new Vector3(i == 0 ? -5.4f : 5.4f, .03f, 0),
                        new Vector3(.3f, .025f, 5.8f), "Hazard", false).GetComponent<Renderer>();
                bridge.WarningAudio = warning.gameObject.AddComponent<AudioSource>();
                bridge.WarningAudio.playOnAwake = false; bridge.WarningAudio.spatialBlend = 1; bridge.WarningAudio.maxDistance = 25;
                PrefabUtility.SaveAsPrefabAsset(root, Path, out bool success);
                if (!success) throw new Exception("Could not save folding bridge prefab.");
                AssetDatabase.SaveAssets(); Object.DestroyImmediate(root);
            }
            finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(temporary, true); }
            if (original.isDirty != dirty) throw new Exception("Unexpected scene change.");
            Debug.Log("[CHESS FIGHT] Folding bridge prefab created without map placement.");
        }
        static Transform Group(string name, Transform parent, Vector3 at)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = at; return t;
        }
        static GameObject Box(string name, Transform parent, Vector3 at, Vector3 size, string material, bool solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = at; go.transform.localScale = size;
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ChessFight/Materials/" + material + ".mat");
            if (mat == null) throw new Exception("Missing material " + material);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
    }
}
