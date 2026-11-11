using System;
using System.IO;
using System.Linq;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    public static class SwordFightBuilder
    {
        public const string ScenePath = "Assets/Scenes/SwordFight.unity";
        const string Lab = "Assets/";
        [MenuItem("ChessFight/Sword Fight/Open Test Scene", false, 0)]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) CreateScene();
            else EditorSceneManager.OpenScene(ScenePath);
        }
        // Initial creation only: never rebuild another mode or overwrite a designer's scene.
        static void CreateScene()
        {
            if (File.Exists(ScenePath)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Sword Fight · pawn prototype");
            root.AddComponent<PhysicsProfile>();
            root.AddComponent<GameSceneConfig>().customMatchSimulation = true;
            var game = root.AddComponent<SwordFightGame>();
            game.pawnPrefab = AssetDatabase.LoadAssetAtPath<RagdollPawn>(Lab + "Prefabs/RagdollPawn.prefab");
            game.tuning = AssetDatabase.LoadAssetAtPath<RagdollTuning>(Lab + "Settings/RagdollTuning.asset");
            if (game.pawnPrefab == null || game.tuning == null) throw new Exception("Existing ragdoll assets are missing.");
            // A single continuous collider avoids tile seams. This is not the final map.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Temporary flat platform · final map later";
            floor.transform.position = new Vector3(0, -.3f, 0); floor.transform.localScale = new Vector3(14, .6f, 14);
            floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Lab + "Materials/Lab_Floor.mat");
            var camera = new GameObject("Main Camera"); camera.tag = "MainCamera";
            camera.AddComponent<Camera>().backgroundColor = new Color(.20f, .28f, .36f);
            camera.AddComponent<AudioListener>();
            var rig = camera.AddComponent<LabCamera>(); rig.distance = 3.2f; rig.pitch = 20;
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional;
            sun.intensity = 1.15f; sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(48, -30, 0);
            RenderSettings.ambientLight = new Color(.64f, .68f, .75f);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        public static void PrepareBatch()
        {
            try
            {
                CreateScene();
                var scenes = EditorBuildSettings.scenes.ToList();
                if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray(); AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        /// <summary>R534: 승규 님's Sword Fight sandbox (SeoungTest_SwordFight) as a development player, for two copies on one
        /// PC (-labLoopback host / client) to show each side's skills as the other sees them. Output: -labOut, else
        /// Builds/SeoungSwordFight. Logs a property block colour round trip first (the team tint reads colours back).</summary>
        public static void BuildSandboxBatch()
        {
            try
            {
                var probe = new MaterialPropertyBlock();
                var gold = new Color(1f, 0.83f, 0.3f, 0.6f);
                probe.SetColor("_BaseColor", gold);
                Debug.Log($"[SwordFightBuild] colour space {PlayerSettings.colorSpace}, block colour set {gold} read {probe.GetColor("_BaseColor")}");
                int at = Array.IndexOf(Environment.GetCommandLineArgs(), "-labOut");
                string output = Path.GetFullPath(at >= 0 && at + 1 < Environment.GetCommandLineArgs().Length ? Environment.GetCommandLineArgs()[at + 1] : "Builds/SeoungSwordFight");
                Directory.CreateDirectory(output);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/SeoungTest/SeoungTest_SwordFight.unity" },
                    locationPathName = Path.Combine(output, "ChessFight_SwordFight.exe"), target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded) throw new Exception("Sword Fight sandbox build failed: " + report.summary.result);
                if (File.Exists("steam_appid.txt")) File.Copy("steam_appid.txt", Path.Combine(output, "steam_appid.txt"), true);
                Debug.Log("[SwordFightBuild] PASS sandbox -> " + output);
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void BuildBatch()
        {
            try
            {
                string output = Path.GetFullPath("Builds/SwordFight"); Directory.CreateDirectory(output);
                bool test = Environment.GetCommandLineArgs().Contains("-swordFightTestBuild");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = test ? new[] { ScenePath, "Assets/Scenes/Lobby.unity" } : EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = Path.Combine(output, "ChessFight.exe"), target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded) throw new Exception("Sword Fight build failed: " + report.summary.result);
                // steam_appid.txt: SteamAppIdBuildStep copies it next to the exe (R523).
                Debug.Log("[SwordFightBuild] PASS " + (test ? "test scene" : "Intro/Lobby and all enabled modes") + " -> " + output);
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
