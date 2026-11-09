using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChessFight.Editor
{
    // R523: every Windows or Linux player build gets the project root's steam_appid.txt next to its exe, whichever
    // way it was built (File > Build Profiles, Build And Run, or a ChessFight build menu). Unity never copies the file
    // and Steamworks.NET's own build step does not either, so a build made with Unity's Build button had no App ID
    // when started outside Steam: Steam refused it ("No appID found") while it was running and logged in, and only
    // the Editor, whose working folder is the project root, connected. SteamSession falls back to DevAppId at run time
    // as well; the file keeps a build on the same App ID as the Editor.
    // A build uploaded to Steam under a real App ID should leave the file out of what it uploads.
    sealed class SteamAppIdBuildStep : IPostprocessBuildWithReport
    {
        const string AppIdFile = "steam_appid.txt";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneLinux64) return;
            string source = Path.GetFullPath(AppIdFile);
            // The output path is the exe; the file goes in the folder that holds it.
            string folder = Path.GetDirectoryName(Path.GetFullPath(report.summary.outputPath));
            if (!File.Exists(source))
            {
                Debug.LogWarning("[ChessFight] " + AppIdFile + " is missing from the project root, so the build has none; " +
                                 "it will tell Steam the development App ID by itself.");
                return;
            }
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
            File.Copy(source, Path.Combine(folder, AppIdFile), true);
            Debug.Log("[ChessFight] Copied " + AppIdFile + " next to the build: " + Path.Combine(folder, AppIdFile));
        }
    }
}
