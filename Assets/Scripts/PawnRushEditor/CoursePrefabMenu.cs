using System;
using System.Collections.Generic;
using System.IO;
using ChessFight.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush.Editor
{
    // R708: ChessFight > Pawn Rush > Open Course Prefab Test - the obstacle courses one at a time (CoursePrefabTest), course 1
    // early / course 2 mid / course 3 late from the section set, number keys as in the mini-game prefab test. The scene
    // Assets/Scenes/PawnRush_Courses.unity is the section test scene copied with its round and playtest taken out (made
    // the first time the menu opens it). Check in batch on a copy of the project:
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.CoursePrefabMenu.RunPlayBatch
    // (builds the scene if missing, plays it, opens every course in turn; report Logs/CoursePrefabTest/report.txt).
    [InitializeOnLoad]
    public static class CoursePrefabMenu
    {
        public const string TestScene = "Assets/Scenes/PawnRush_Courses.unity";
        const string ActiveKey = "CoursePrefabTest.Active", ExitKey = "CoursePrefabTest.Exit", CodeKey = "CoursePrefabTest.Code";
        static string Folder => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "CoursePrefabTest");

        [MenuItem("ChessFight/Pawn Rush/Open Course Prefab Test", false, 2)]
        public static void OpenTest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScene) == null) BuildScene();
            EditorSceneManager.OpenScene(TestScene);
        }

        public static void BuildScene()
        {
            var set = AssetDatabase.LoadAssetAtPath<PawnRushSectionSet>(SectionPrefabMenu.SectionSet);
            if (set == null) throw new InvalidOperationException("Missing section set: " + SectionPrefabMenu.SectionSet);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScene) == null && !AssetDatabase.CopyAsset(SectionPrefabMenu.TestScene, TestScene))
                throw new InvalidOperationException("Could not copy the section test scene.");
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(TestScene);
            bool close = !scene.isLoaded;
            if (close) scene = EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                PlaytestSpawner spawner = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var round in root.GetComponentsInChildren<PawnRushRound>(true)) Object.DestroyImmediate(round);
                    foreach (var test in root.GetComponentsInChildren<SectionPlaytest>(true)) Object.DestroyImmediate(test);
                    if (spawner == null) spawner = root.GetComponentInChildren<PlaytestSpawner>(true);
                }
                if (spawner == null) throw new InvalidOperationException("The section test scene has no PlaytestSpawner.");
                var viewer = spawner.GetComponent<CoursePrefabTest>();
                if (viewer == null) viewer = spawner.gameObject.AddComponent<CoursePrefabTest>();
                viewer.Configure(set, spawner);
                var settings = new SerializedObject(spawner);
                var help = settings.FindProperty("showHelp");
                if (help != null) help.boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, TestScene);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (close) EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("[PawnRush] 코스 프리팹 시험 장면: " + TestScene);
        }

        // ---- batch check: every course opens without an error -----------------------------------------------------------

        static readonly List<string> errors = new List<string>();
        static int index, count, failures;
        static double stepAt, requested, idleAt;

        static CoursePrefabMenu()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        public static void RunPlayBatch()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "report.txt"), "Course prefab test (R708): each course opened in Play Mode. Not people, not online.\n");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScene) == null) BuildScene();
            EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Single);
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitKey, false);
            SessionState.SetInt(CodeKey, 1);
            requested = idleAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= BeginWhenIdle;
            EditorApplication.update += BeginWhenIdle;
        }

        static void BeginWhenIdle()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - requested > 180) { EditorApplication.update -= BeginWhenIdle; Log("FAIL: the Editor did not settle"); if (Application.isBatchMode) EditorApplication.Exit(1); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { idleAt = now; return; }
            if (now - idleAt < 2) return;
            EditorApplication.update -= BeginWhenIdle;
            EditorSceneManager.playModeStartScene = null;
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                errors.Clear(); index = -1; count = 0; failures = 0;
                stepAt = EditorApplication.timeSinceStartup;
                Application.logMessageReceived += OnLog;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) CompleteExit();
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                if (errors.Count < 40) errors.Add((index >= 0 ? "course key " + (index + 1) + ": " : "") + type + ": " + condition);
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - stepAt < 4) return;
            var test = Object.FindFirstObjectByType<CoursePrefabTest>();
            if (test == null) { Log("FAIL: no CoursePrefabTest in the scene"); failures++; Finish(); return; }
            if (index >= 0)
            {
                var s = test.Current;
                var pawn = MiniGameBase.LocalDriver?.FollowTarget;
                string where = s != null && pawn != null ? s.transform.InverseTransformPoint(pawn.position).ToString("0.0") : "-";
                bool ok = s != null && pawn != null && Vector3.Distance(s.transform.InverseTransformPoint(pawn.position), s.EntryLocal.position) < 12f;
                if (!ok) failures++;
                Log((ok ? "PASS " : "FAIL ") + "key " + (index + 1) + ": " + (s != null ? s.Id + " · " + s.DisplayName + " (slot " + (s.Slot + 1) + ")" : "nothing built") + ", pawn at " + where);
            }
            index++;
            var entries = typeof(CoursePrefabTest).GetField("entries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(test) as System.Collections.IList;
            count = entries != null ? entries.Count : 0;
            if (index >= count || index >= 10) { Finish(); return; }
            test.Select(index);
            stepAt = now;
        }

        static void Finish()
        {
            Log("courses: " + count);
            if (count < 4) { failures++; Log("FAIL: fewer than four courses listed"); }
            foreach (var e in errors) Log("console " + e);
            if (errors.Count > 0) failures++;
            Log(failures == 0 ? "RESULT PASS" : "RESULT FAIL (" + failures + ")");
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick;
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(ExitKey, true);
            SessionState.SetInt(CodeKey, failures == 0 ? 0 : 1);
            EditorApplication.isPlaying = false;
        }

        static void Log(string value)
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Path.Combine(Folder, "report.txt"), value + "\n");
            Debug.Log("[CoursePrefabTest] " + value);
        }

        static void CompleteExit()
        {
            if (!SessionState.GetBool(ExitKey, false)) return;
            SessionState.SetBool(ExitKey, false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(CodeKey, 1));
        }
    }
}
