using System;
using System.IO;
using ChessFight.Gameplay;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    public static class KingRushBuilder
    {
        public const string ScenePath = "Assets/Scenes/KingRushPrototype.unity";
        // Its menu (ChessFight/King Rush/Open Mechanics Test) went with the scene on 10-08: the scene was
        // deleted and this would rebuild it. The code is kept for now.
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) CreateScene(); else EditorSceneManager.OpenScene(ScenePath);
        }
        static void CreateScene()
        {
            // Never regenerate an existing scene or replace a teammate's course.
            if (File.Exists(ScenePath)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("King Rush · offline foundation"); root.AddComponent<PhysicsProfile>();
            var game = root.AddComponent<KingRushPrototype>();
            const string lab = "Assets/";
            game.pawnPrefab = AssetDatabase.LoadAssetAtPath<RagdollPawn>(lab + "Prefabs/RagdollPawn.prefab");
            game.tuning = AssetDatabase.LoadAssetAtPath<RagdollTuning>(lab + "Settings/RagdollTuning.asset");
            if (game.pawnPrefab == null || game.tuning == null) throw new Exception("Missing existing ragdoll assets");
            var camera = new GameObject("Main Camera"); camera.tag = "MainCamera";
            camera.AddComponent<Camera>().backgroundColor = new Color(.3f, .45f, .6f); camera.AddComponent<AudioListener>();
            var rig = camera.AddComponent<LabCamera>(); rig.distance = 4.5f; rig.pitch = 18;
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional;
            sun.intensity = 1.1f; sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(48, -30, 0);
            RenderSettings.ambientLight = new Color(.6f, .65f, .72f);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        public static void BuildBatch()
        {
            try
            {
                CreateScene(); AssetDatabase.SaveAssets();
                string output = Path.GetFullPath("Builds/KingRushPrototype"); Directory.CreateDirectory(output);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { ScenePath, "Assets/Scenes/Lobby.unity", "Assets/Scenes/PawnRush_Course01.unity", "Assets/Scenes/SwordFight.unity" },
                    locationPathName = Path.Combine(output, "ChessFight.exe"), target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development });
                if (report.summary.result != BuildResult.Succeeded) throw new Exception("King Rush build failed: " + report.summary.result);
                if (File.Exists("steam_appid.txt")) File.Copy("steam_appid.txt", Path.Combine(output, "steam_appid.txt"), true);
                Debug.Log("[KingRushBuild] PASS -> " + output); EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
