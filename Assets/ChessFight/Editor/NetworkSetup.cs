using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.Editor
{
    // Installs the two optional packages the prototype is gated on, opens the lab
    // scene and produces the Windows test build.
    //
    // Both packages are installed through the UPM Client API rather than pinned by
    // hand in manifest.json: Steamworks.NET is a git dependency and the Input
    // System version that matches the Editor is resolved by Unity. Commit the
    // manifest.json and packages-lock.json that installation writes.
    [InitializeOnLoad]
    public static class NetworkSetup
    {
        public const string LabScene = "Assets/ChessFight/Game/Scenes/ChessFightLab.unity";

        const string Steamworks = "com.rlabrecque.steamworks.net";
        const string SteamworksUrl = "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#c21a8f0e31c56ae8707130967faf491f7dd7c0d8";
        const string InputSystem = "com.unity.inputsystem";

        static readonly Queue<(string name, string identifier)> queue = new Queue<(string, string)>();
        static AddRequest request;
        static string installing;
        static double started;

        static bool Installed(string package) =>
            UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Any(p => p.name == package);
        public static bool SteamworksInstalled => Installed(Steamworks);

        static NetworkSetup() { EditorApplication.delayCall += AutoInstall; }

        static void AutoInstall()
        {
            if (Application.isBatchMode || SessionState.GetBool("ChessFight.InstallAttempted", false)) return;
            if (Installed(Steamworks) && Installed(InputSystem)) return;
            Install();
        }

        [MenuItem("ChessFight/Setup/Install dependencies")]
        public static void Install()
        {
            SessionState.SetBool("ChessFight.InstallAttempted", true);
            if (!Installed(Steamworks)) Enqueue(Steamworks, SteamworksUrl);
            // No version: Unity resolves the Input System release that matches this Editor.
            if (!Installed(InputSystem)) Enqueue(InputSystem, InputSystem);
            Next();
        }

        static void Enqueue(string name, string identifier)
        {
            if (installing == name || queue.Any(item => item.name == name)) return;
            queue.Enqueue((name, identifier));
        }

        static void Next()
        {
            if (request != null || queue.Count == 0) return;
            var item = queue.Dequeue();
            installing = item.name;
            Debug.Log("[ChessFight] Installing " + item.name + " through the Unity Package Manager...");
            request = Client.Add(item.identifier);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (request == null) return;
            if (!request.IsCompleted && EditorApplication.timeSinceStartup - started < 180) return;
            EditorApplication.update -= Poll;
            bool ok = request.IsCompleted && request.Status == StatusCode.Success;
            string error = request.Error?.message ?? "Timed out";
            if (ok) Debug.Log("[ChessFight] Installed " + installing + ". Commit Packages/manifest.json and packages-lock.json.");
            else Debug.LogError("[ChessFight] Could not install " + installing + ": " + error +
                                ". Check the internet connection (and Git for Windows for Steamworks), " +
                                "then retry with ChessFight > Setup > Install dependencies.");
            request = null; installing = null;
            Next();
        }

        [MenuItem("ChessFight/Network/Open test scene")]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(File.Exists(LabScene) ? LabScene : "Assets/Scenes/SampleScene.unity");
        }

        [MenuItem("ChessFight/Network/Build Windows development test")]
        public static void Build()
        {
            if (!SteamworksInstalled) throw new InvalidOperationException("Install dependencies first from ChessFight > Setup.");
            const string path = "Builds/NetworkTest/ChessFight.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { File.Exists(LabScene) ? LabScene : "Assets/Scenes/SampleScene.unity" },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Network test build failed.");
            File.Copy("steam_appid.txt", "Builds/NetworkTest/steam_appid.txt", true);
            Debug.Log("[ChessFight] Development build: " + Path.GetFullPath(path));
        }
    }
}
