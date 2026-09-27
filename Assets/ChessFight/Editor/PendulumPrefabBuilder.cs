using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChessFight.ProtectKing.Editor
{
    public static class PendulumPrefabBuilder
    {
        public const string Path = ObstaclePrefabBuilder.Folder + "/11_PendulumBall.prefab";
        [MenuItem("CHESS FIGHT/Obstacles/Create Pendulum Prefab (No Map Placement) %&#p")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Path) != null) { Debug.Log("Pendulum prefab already exists; preserving edits."); return; }
            var original = SceneManager.GetActiveScene(); bool dirty = original.isDirty;
            var temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temporary);
            try
            {
                var root = new GameObject("PendulumObstacle"); var context = root.AddComponent<ObstacleContext>();
                var pivot = Group("Pivot", root.transform, new Vector3(0, 7.3f, 0));
                var swing = pivot.gameObject.AddComponent<PendulumSwing>(); swing.context = context;
                var body = pivot.GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                var impact = pivot.gameObject.AddComponent<ObstacleImpact>(); impact.pendulum = swing; impact.context = context; impact.hitCooldown = .5f;
                var chain = Group("Chain", pivot, Vector3.zero);
                Shape("Suspension cable", chain, PrimitiveType.Cylinder, new Vector3(0, -3, 0), new Vector3(.15f, 3, .15f), "Stone", false);
                for (int i = 1; i <= 10; i++)
                {
                    var link = Shape("Chain collar", chain, PrimitiveType.Cylinder, new Vector3(0, -i * .53f, 0), new Vector3(.26f, .08f, .26f), "BoardDark", false);
                    link.transform.localRotation = Quaternion.Euler(i % 2 == 0 ? 90 : 0, 0, 0);
                }
                var ball = Shape("Ball", chain, PrimitiveType.Sphere, new Vector3(0, -6, 0), Vector3.one * 2.2f, "Stone", true);
                swing.Ball = ball.transform;
                var sensor = Group("ImpactSensor", ball.transform, Vector3.zero).gameObject.AddComponent<SphereCollider>();
                sensor.radius = .56f; sensor.isTrigger = true;
                Shape("Ceiling mount", root.transform, PrimitiveType.Cube, new Vector3(0, 7.55f, 0), new Vector3(1.8f, .5f, 1.5f), "BoardDark", true);
                var warning = Group("OptionalWarningArea", root.transform, new Vector3(0, .025f, 0));
                for (int side = -1; side <= 1; side += 2)
                    Shape("Swing boundary", warning, PrimitiveType.Cube, new Vector3(0, 0, side * 1.3f), new Vector3(12, .025f, .09f), "Hazard", false);
                PrefabUtility.SaveAsPrefabAsset(root, Path, out bool success);
                if (!success) throw new Exception("Could not save pendulum prefab.");
                AssetDatabase.SaveAssets(); Object.DestroyImmediate(root);
            }
            finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(temporary, true); }
            if (original.isDirty != dirty) throw new Exception("Unexpected scene change.");
            Debug.Log("[CHESS FIGHT] Pendulum prefab created. No map placement: " + Path);
        }
        static Transform Group(string name, Transform parent, Vector3 at)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = at; return t;
        }
        static GameObject Shape(string name, Transform parent, PrimitiveType kind, Vector3 at, Vector3 scale, string materialName, bool solid)
        {
            var go = GameObject.CreatePrimitive(kind); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = at; go.transform.localScale = scale;
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/ChessFight/Materials/" + materialName + ".mat");
            if (material == null) throw new Exception("Missing material " + materialName);
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
    }
}
