using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.PawnRush.Editor
{
    // 승규 님's test scenes (seoungtest branch, 10-30), made from the game's own scenes:
    //   SeoungTest_PawnRush   course 01's camera, light and playtest pawn, the course itself swapped for its sections and
    //                         mini-games side by side (SeoungTestCourse)
    //   SeoungTest_SwordFight the arena skill test with the game's rules off (SwordFightGame.sandbox)
    // Running a menu again builds its scene again (hand edits inside SeoungTest_Zones are lost).
    public static class SeoungTestMenu
    {
        const string Folder = "Assets/Scenes/SeoungTest";
        public const string PawnRushScene = Folder + "/SeoungTest_PawnRush.unity";
        public const string SwordFightScene = Folder + "/SeoungTest_SwordFight.unity";
        const string CourseSource = "Assets/Scenes/PawnRush_Course01.unity";
        const string ArenaSource = "Assets/Scenes/SkillTest/SwordFight_ArenaSkillTest/SwordFight_ArenaSkillTest.unity";

        [MenuItem("ChessFight/SeoungTest/Build Pawn Rush Test Scene", false, 0)]
        public static void BuildPawnRush()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Copy(CourseSource, PawnRushScene);
            var scene = EditorSceneManager.OpenScene(PawnRushScene, OpenSceneMode.Single);

            // Course 01's own course, its art and its playtest keys go; the camera, light and pawn spawner stay.
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
            var test = Object.FindFirstObjectByType<SeoungTestCourse>();
            if (kit == null && test != null) kit = test.Kit;
            if (kit == null) kit = AssetDatabase.LoadAssetAtPath<Course01Kit>(AssetDatabase.GUIDToAssetPath(
                AssetDatabase.FindAssets("t:Course01Kit").Length > 0 ? AssetDatabase.FindAssets("t:Course01Kit")[0] : ""));
            if (kit == null)
            {
                EditorUtility.DisplayDialog("SeoungTest", "Course01Kit 에셋을 찾지 못했습니다.", "확인");
                return;
            }

            var spawner = Object.FindFirstObjectByType<PlaytestSpawner>();
            var orbit = Object.FindFirstObjectByType<OrbitCamera>();
            if (test == null) test = new GameObject("SeoungTest Course").AddComponent<SeoungTestCourse>();
            var old = test.transform.Find(SeoungTestCourse.RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var previous = CourseBuilder.Spawn;
            CourseBuilder.Spawn = (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            GameObject root;
            try { root = SeoungTestCourse.Build(test.transform, kit); }
            finally { CourseBuilder.Spawn = previous; }

            var so = new SerializedObject(test);
            so.FindProperty("kit").objectReferenceValue = kit;
            so.FindProperty("spawner").objectReferenceValue = spawner;
            so.FindProperty("orbit").objectReferenceValue = orbit;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (spawner != null)
            {
                var first = root.GetComponentInChildren<SeoungTestZone>();
                var sp = new SerializedObject(spawner);
                sp.FindProperty("spawnPoint").objectReferenceValue = first != null ? first.start : null;
                // Its own help names F1 for the free camera, which this scene gives to the zones.
                sp.FindProperty("showHelp").boolValue = false;
                sp.FindProperty("team").intValue = Teams.White;
                sp.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddToBuild(PawnRushScene);
            Debug.Log("[SeoungTest] 폰 러쉬 시험 씬을 만들었습니다: " + PawnRushScene + " (구역 " + root.GetComponentsInChildren<SeoungTestZone>().Length + "개)");
        }

        [MenuItem("ChessFight/SeoungTest/Build Sword Fight Sandbox Scene", false, 1)]
        public static void BuildSwordFight()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Copy(ArenaSource, SwordFightScene);
            var scene = EditorSceneManager.OpenScene(SwordFightScene, OpenSceneMode.Single);
            int set = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null || behaviour.GetType().Name != "SwordFightGame") continue;
                    var so = new SerializedObject(behaviour);
                    so.FindProperty("sandbox").boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    set++;
                }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddToBuild(SwordFightScene);
            Debug.Log("[SeoungTest] 소드 파이트 샌드박스 씬을 만들었습니다: " + SwordFightScene + " (SwordFightGame " + set + "개)");
        }

        // Batch mode: both scenes (Unity -batchmode -executeMethod ChessFight.PawnRush.Editor.SeoungTestMenu.BuildAll).
        public static void BuildAll()
        {
            BuildPawnRush();
            BuildSwordFight();
        }

        static void Copy(string from, string to)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(to) != null) return;
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Scenes", "SeoungTest");
            AssetDatabase.CopyAsset(from, to);
        }

        static void AddToBuild(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
