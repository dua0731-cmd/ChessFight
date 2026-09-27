using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChessFight.ProtectKing.Editor
{
    public static class SpringAndVentPrefabBuilder
    {
        public const string SpringPath = ObstaclePrefabBuilder.Folder + "/14_SpringPillar.prefab";
        public const string VentPath = ObstaclePrefabBuilder.Folder + "/15_AirVent.prefab";
        [MenuItem("CHESS FIGHT/Obstacles/Create Spring Pillar and Air Vent (No Map Placement)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play Mode first.");
            var original = SceneManager.GetActiveScene(); bool dirty = original.isDirty;
            var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scratch);
            try
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(SpringPath) == null) Save(Spring(), SpringPath);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(VentPath) == null) Save(Vent(), VentPath);
                AssetDatabase.SaveAssets();
            }
            finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scratch, true); }
            if (original.isDirty != dirty) throw new Exception("Unexpected scene change.");
            Debug.Log("[CHESS FIGHT] Spring/vent prefabs ready; existing assets preserved, no map placement.");
        }
        static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success); Object.DestroyImmediate(root);
            if (!success) throw new Exception("Could not save " + path);
        }
        static GameObject Spring()
        {
            var root = new GameObject("SpringPillar"); var context = root.AddComponent<ObstacleContext>();
            var spring = root.AddComponent<SpringPillar>(); spring.context = context;
            Box("Wall housing", root.transform, new Vector3(0, 1.2f, -1), new Vector3(2.8f, 2.4f, 1.8f), "Stone", true);
            var moving = Group("Pillar", root.transform, new Vector3(0, 1.2f, -.75f));
            spring.Pillar = moving.gameObject.AddComponent<Rigidbody>(); spring.Pillar.isKinematic = true; spring.Pillar.useGravity = false;
            spring.Pillar.interpolation = RigidbodyInterpolation.Interpolate; spring.Pillar.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Box("Solid head", moving, Vector3.zero, new Vector3(1.5f, 1.5f, 1.5f), "BoardDark", true);
            Box("Impact face", moving, new Vector3(0, 0, .765f), new Vector3(1.45f, 1.45f, .035f), "Hazard", false);
            Box("Telescopic shaft", moving, new Vector3(0, 0, -1.8f), new Vector3(.55f, .55f, 3.5f), "Stone", false);
            for (int i = 0; i < 8; i++) Box("Spring collar", moving, new Vector3(0, 0, -.85f - i * .35f), new Vector3(.8f, .8f, .1f), "CheckpointGold", false);
            spring.TriggerArea = Volume("TriggerArea", root.transform, new Vector3(0, 1.2f, 2), new Vector3(3.5f, 2.4f, 4));
            spring.HitArea = Volume("HitArea", root.transform, new Vector3(0, 1.2f, .05f), new Vector3(1.65f, 1.65f, .2f));
            spring.WarningVisual = Box("WarningVisual", root.transform, new Vector3(0, 2.5f, -.2f), new Vector3(2.6f, .16f, .2f), "Hazard", false);
            spring.WarningVisual.SetActive(false); return root;
        }
        static GameObject Vent()
        {
            var root = new GameObject("AirVent"); var context = root.AddComponent<ObstacleContext>();
            var vent = root.AddComponent<AirVent>(); vent.context = context;
            var model = Group("VentModel", root.transform, Vector3.zero);
            Box("Housing", model, new Vector3(0, 1.3f, -.25f), new Vector3(2.8f, 2.6f, .5f), "Stone", true);
            Box("Dark aperture", model, new Vector3(0, 1.3f, .015f), new Vector3(2.3f, 2.1f, .03f), "BoardDark", false);
            for (int i = 0; i < 6; i++) Box("Grille", model, new Vector3(0, .45f + i * .34f, .07f), new Vector3(2.3f, .1f, .08f), "BoardLight", false);
            vent.WindTrigger = Volume("WindTrigger", root.transform, new Vector3(0, 1.5f, 3.2f), new Vector3(3, 3, 6));
            vent.WarningVisual = Box("WarningVisual", root.transform, new Vector3(0, 2.65f, 0), new Vector3(2.6f, .12f, .15f), "Hazard", false);
            vent.WarningVisual.SetActive(false); vent.VFXPoint = Group("VFXPoint", root.transform, new Vector3(0, 1.3f, .15f));
            var wind = Group("WindVisual", root.transform, Vector3.zero); vent.WindVisual = wind.gameObject;
            for (int i = -1; i <= 1; i++)
            {
                Box("Air stream", wind, new Vector3(i * .7f, 1.3f, 2), new Vector3(.055f, .055f, 3.5f), "CheckpointGold", false);
                for (int side = -1; side <= 1; side += 2)
                {
                    var arrow = Box("Arrow tip", wind, new Vector3(i * .7f + side * .15f, 1.3f, 3.5f), new Vector3(.05f, .05f, .45f), "CheckpointGold", false);
                    arrow.transform.localRotation = Quaternion.Euler(0, -side * 45, 0);
                }
            }
            wind.gameObject.SetActive(false); return root;
        }
        static Transform Group(string name, Transform parent, Vector3 at)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = at; return t; }
        static BoxCollider Volume(string name, Transform parent, Vector3 at, Vector3 size)
        {
            var c = Group(name, parent, at).gameObject.AddComponent<BoxCollider>(); c.size = size; c.isTrigger = true; c.enabled = false; return c;
        }
        static GameObject Box(string name, Transform parent, Vector3 at, Vector3 size, string mat, bool solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = at; go.transform.localScale = size;
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/ChessFight/Materials/" + mat + ".mat");
            if (material == null) throw new Exception("Missing material " + mat);
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
    }
}
