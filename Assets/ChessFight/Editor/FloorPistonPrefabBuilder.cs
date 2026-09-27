using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChessFight.ProtectKing.Editor
{
    public static class FloorPistonPrefabBuilder
    {
        public const string Path = ObstaclePrefabBuilder.Folder + "/13_FloorPiston.prefab";
        [MenuItem("CHESS FIGHT/Obstacles/Create Floor Piston Prefab (No Map Placement)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Path) != null) { Debug.Log("Floor piston exists; preserving edits."); return; }
            var original = SceneManager.GetActiveScene(); bool dirty = original.isDirty;
            var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scratch);
            try
            {
                var root = new GameObject("FloorPiston"); var context = root.AddComponent<ObstacleContext>();
                var piston = root.AddComponent<FloorPiston>(); piston.context = context;
                var baseRoot = Group("Base", root.transform);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Frame", baseRoot, new Vector3(side * 2.15f, -.18f, 0), new Vector3(.3f, .36f, 4.6f), "BoardDark", true);
                    Box("Frame", baseRoot, new Vector3(0, -.18f, side * 2.15f), new Vector3(4, .36f, .3f), "BoardDark", true);
                }
                Box("Socket", baseRoot, new Vector3(0, -2.9f, 0), new Vector3(3.6f, .3f, 3.6f), "Stone", false);
                var moving = Group("Piston", root.transform);
                piston.Piston = moving.gameObject.AddComponent<Rigidbody>(); piston.Piston.isKinematic = true; piston.Piston.useGravity = false;
                piston.Piston.interpolation = RigidbodyInterpolation.Interpolate; piston.Piston.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                Box("Solid cap", moving, new Vector3(0, -.2f, 0), new Vector3(3.9f, .4f, 3.9f), "Stone", true);
                Box("Shaft", moving, new Vector3(0, -1.65f, 0), new Vector3(1.8f, 2.9f, 1.8f), "BoardDark", false);
                for (int x = 0; x < 4; x++) for (int z = 0; z < 4; z++)
                    Box("Checker", moving, new Vector3((x - 1.5f) * .95f, .012f, (z - 1.5f) * .95f), new Vector3(.92f, .02f, .92f), (x + z) % 2 == 0 ? "BoardLight" : "BoardDark", false);
                var warning = Group("WarningArea", root.transform); piston.WarningArea = warning.gameObject;
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Warning strip", warning, new Vector3(side * 1.8f, .045f, 0), new Vector3(.16f, .02f, 3.75f), "Hazard", false);
                    Box("Warning strip", warning, new Vector3(0, .045f, side * 1.8f), new Vector3(3.75f, .02f, .16f), "Hazard", false);
                }
                warning.gameObject.SetActive(false);
                var hit = Group("HitArea", root.transform); hit.localPosition = Vector3.up * .25f;
                piston.HitArea = hit.gameObject.AddComponent<BoxCollider>(); piston.HitArea.size = new Vector3(3.8f, .5f, 3.8f);
                piston.HitArea.isTrigger = true; piston.HitArea.enabled = false; // Geometry reference for a swept query; never a solid collider.
                PrefabUtility.SaveAsPrefabAsset(root, Path, out bool success);
                if (!success) throw new Exception("Could not save floor piston.");
                AssetDatabase.SaveAssets(); Object.DestroyImmediate(root);
            }
            finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scratch, true); }
            if (original.isDirty != dirty) throw new Exception("Unexpected scene change.");
            Debug.Log("[CHESS FIGHT] Floor piston prefab created without map placement.");
        }
        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); return t;
        }
        static void Box(string name, Transform parent, Vector3 at, Vector3 size, string material, bool solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = at; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/ChessFight/Materials/" + material + ".mat");
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
}
