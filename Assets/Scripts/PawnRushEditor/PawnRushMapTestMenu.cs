using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.PawnRush.Editor
{
    // The Pawn Rush map test scene (11-02) and its map folder, in main's menu groups since the merge (R507):
    //   Pawn Rush > Open Map Test                               opens Assets/Scenes/PawnRush_MapTest.unity
    //   Development > Map Authoring > Test Selected Pawn Rush Map (also right click a prefab > ChessFight > ...)
    //                                                           opens the scene and plays that map
    //   Development > Map Authoring > New Pawn Rush Map Prefab  a template in the map folder: start, floor, a fall
    //   Development > Map Authoring > Rebuild Pawn Rush Map Test Scene
    //                                                           makes the scene again from course 01's camera, light,
    //                                                           physics and playtest pawn
    public static class PawnRushMapTestMenu
    {
        public const string ScenePath = "Assets/Scenes/PawnRush_MapTest.unity";
        const string CourseSource = "Assets/Prototype/PawnRush/Scenes/PawnRush_Course01.unity";

        [MenuItem("ChessFight/Pawn Rush/Open Map Test", false, 3)]
        public static void OpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Rebuild();
            else EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("ChessFight/Development/Map Authoring/Test Selected Pawn Rush Map", false, 10)]
        [MenuItem("Assets/ChessFight/Test In Pawn Rush Map Test", false, 2000)]
        public static void TestSelected()
        {
            var prefab = Selection.activeObject as GameObject;
            string path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : null;
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
            {
                EditorUtility.DisplayDialog("맵 시험", "Project 창에서 맵 프리팹(.prefab)을 하나 고른 뒤 실행하세요.", "확인");
                return;
            }
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // A prefab outside the map folder is played too, through the scene's own list.
            bool inFolder = path.StartsWith(PawnRushMapTest.MapFolder + "/");
            if (!inFolder)
                Debug.Log("[MapTest] " + path + "은(는) 맵 폴더 밖에 있어 씬의 maps 목록에 넣습니다. 맵 폴더에 두려면 " + PawnRushMapTest.MapFolder + "로 옮기세요.");
            // The scene finds a folder map by its path and a listed one by its name.
            PlayerPrefs.SetString(PawnRushMapTest.LastMapKey, inFolder ? path : prefab.name);
            PlayerPrefs.Save();
            OpenScene();
            if (!inFolder) AddOutsideMap(prefab);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Assets/ChessFight/Test In Pawn Rush Map Test", true)]
        static bool CanTestSelected() => Selection.activeObject is GameObject go && AssetDatabase.GetAssetPath(go).EndsWith(".prefab");

        // The scene's maps list keeps a prefab from elsewhere (saved with the scene: it is in the list from then on).
        static void AddOutsideMap(GameObject prefab)
        {
            var test = Object.FindFirstObjectByType<PawnRushMapTest>();
            if (test == null) return;
            var so = new SerializedObject(test);
            var list = so.FindProperty("maps");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == prefab) return;
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = prefab;
            so.ApplyModifiedProperties();
            EditorSceneManager.SaveScene(test.gameObject.scene);
        }

        [MenuItem("ChessFight/Development/Map Authoring/New Pawn Rush Map Prefab", false, 11)]
        public static void NewMap()
        {
            EnsureFolder(PawnRushMapTest.MapFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath(PawnRushMapTest.MapFolder + "/NewMap.prefab");
            var kit = FindKit();
            if (kit == null)
            {
                EditorUtility.DisplayDialog("맵 시험", "Course01Kit 에셋을 찾지 못했습니다(바닥 재질).", "확인");
                return;
            }
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            try
            {
                var start = new GameObject("Start");
                start.transform.SetParent(root.transform, false);
                start.AddComponent<SpawnPoint>().Configure(0, Teams.White);
                var b = new CourseBuilder(kit, root.transform);
                b.Floor("Floor", -10f, 10f, -4f, 40f, 0f);
                b.Trigger<KillVolume>("Fall (KillVolume)", new Vector3(-60f, -30f, -60f), new Vector3(60f, -12f, 100f));
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                Selection.activeObject = saved;
                EditorGUIUtility.PingObject(saved);
                Debug.Log("[MapTest] 새 맵 프리팹: " + path + " — 열어서 꾸민 뒤 우클릭 > ChessFight > Test In Pawn Rush Map Test");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // The scene again from course 01's camera, light, physics profile and playtest pawn (the course itself goes).
        [MenuItem("ChessFight/Development/Map Authoring/Rebuild Pawn Rush Map Test Scene", false, 12)]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("맵 시험 씬", ScenePath + "을(를) 다시 만듭니다. 씬에서 손으로 바꾼 것은 사라집니다.", "다시 만들기", "취소"))
                return;
            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.CopyAsset(CourseSource, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Course01Kit kit = null;
            var course = Object.FindFirstObjectByType<PawnRushCourse>();
            if (course != null)
            {
                kit = course.Kit;
                Object.DestroyImmediate(course.gameObject);
            }
            var dress = GameObject.Find("Course01_ArtDress");
            if (dress != null) Object.DestroyImmediate(dress);
            foreach (var playtest in Object.FindObjectsByType<Course01Playtest>(FindObjectsSortMode.None)) Object.DestroyImmediate(playtest);
            if (kit == null) kit = FindKit();

            var spawner = Object.FindFirstObjectByType<PlaytestSpawner>();
            var orbit = Object.FindFirstObjectByType<OrbitCamera>();
            var startPoint = new GameObject("Map Test Start").transform;
            var test = new GameObject("Pawn Rush Map Test").AddComponent<PawnRushMapTest>();
            var so = new SerializedObject(test);
            so.FindProperty("kit").objectReferenceValue = kit;
            so.FindProperty("spawner").objectReferenceValue = spawner;
            so.FindProperty("orbit").objectReferenceValue = orbit;
            so.FindProperty("startPoint").objectReferenceValue = startPoint;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (spawner != null)
            {
                var sp = new SerializedObject(spawner);
                sp.FindProperty("spawnPoint").objectReferenceValue = startPoint;
                sp.FindProperty("showHelp").boolValue = false;   // the map test draws its own help
                sp.FindProperty("team").intValue = Teams.White;
                sp.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MapTest] 맵 시험 씬을 만들었습니다: " + ScenePath);
        }

        static Course01Kit FindKit()
        {
            var found = AssetDatabase.FindAssets("t:Course01Kit");
            return found.Length > 0 ? AssetDatabase.LoadAssetAtPath<Course01Kit>(AssetDatabase.GUIDToAssetPath(found[0])) : null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = new List<string>(path.Split('/'));
            string at = parts[0];
            for (int i = 1; i < parts.Count; i++)
            {
                string next = at + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(at, parts[i]);
                at = next;
            }
        }
    }
}
