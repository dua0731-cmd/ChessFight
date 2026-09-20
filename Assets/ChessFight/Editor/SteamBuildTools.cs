using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChessFight.ProtectKing.Editor
{
    public static class SteamBuildTools
    {
        [MenuItem("CHESS FIGHT/Online/Use Development App ID 480")]
        public static void DevelopmentId()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode first.");
            var transport=UnityEngine.Object.FindFirstObjectByType<SteamLobbyTransport>();
            if(transport==null)throw new Exception("Install online scene components first.");
            Undo.RecordObject(transport,"Steam development App ID");transport.appId=480;
            EditorUtility.SetDirty(transport);
            File.WriteAllText("steam_appid.txt","480",new System.Text.UTF8Encoding(false));
            EditorSceneManager.MarkSceneDirty(transport.gameObject.scene);
            EditorSceneManager.SaveScene(transport.gameObject.scene);
            Debug.Log("[CHESS FIGHT] Development Steam App ID 480 saved.");
        }
        [MenuItem("CHESS FIGHT/Online/Build Windows Test Player")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode first.");
            string folder="Artifacts/ChessFight/WindowsTest";
            Directory.CreateDirectory(folder);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{GrayboxBuilder.ScenePath},locationPathName=folder+"/ChessFight.exe",
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development
            });
            var s=report.summary;
            File.WriteAllText("Artifacts/ChessFight/OnlineBuildVerification.txt",
                DateTime.Now.ToString("s")+"\n"+s.result+"\nErrors: "+s.totalErrors+"\nWarnings: "+s.totalWarnings+"\nBytes: "+s.totalSize);
            if(s.result!=BuildResult.Succeeded)throw new Exception("Windows test build failed.");
            var steam=UnityEngine.Object.FindFirstObjectByType<SteamLobbyTransport>();
            File.WriteAllText(folder+"/steam_appid.txt",steam.appId.ToString(),new System.Text.UTF8Encoding(false));
        }
    }
}