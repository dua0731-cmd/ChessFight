using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.ProtectKing.Editor
{
    // One-shot editor handoff. A request file is consumed only after the scene is saved.
    [InitializeOnLoad]
    public static class GrayboxSceneInstaller
    {
        const string Request = "Artifacts/ChessFight/BuildScene.request";
        static bool running;
        static GrayboxSceneInstaller() { EditorApplication.update += InstallRequestedScene; }
        static void InstallRequestedScene()
        {
            if (File.Exists("Artifacts/ChessFight/StopVerification.request") &&
                !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                SessionState.SetBool("ChessFight.VerificationPending",false);
                SessionState.SetBool("ChessFight.PreviewOnly",false);
                if (EditorApplication.isPlaying) { EditorApplication.isPlaying=false; return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete("Artifacts/ChessFight/StopVerification.request");
            }
            if (!running && File.Exists("Artifacts/ChessFight/VerifyScene.request") &&
                !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                !EditorApplication.isPlayingOrWillChangePlaymode &&
                SceneManager.GetActiveScene().path == GrayboxBuilder.ScenePath &&
                !SceneManager.GetActiveScene().isDirty)
            {
                running=true;
                SessionState.SetBool("ChessFight.PreviewOnly", File.ReadAllText("Artifacts/ChessFight/VerifyScene.request").Trim()=="preview");
                File.Delete("Artifacts/ChessFight/VerifyScene.request");
                EditorApplication.update-=InstallRequestedScene;
                GrayboxVerification.Begin();
                return;
            }
            if (running || !File.Exists(Request) || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            for (int i=0; i<SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return;
            running=true;
            try
            {
                GrayboxBuilder.Build();
                GrayboxBuilder.Validate();
                var view=SceneView.lastActiveSceneView;
                if (view==null) view=EditorWindow.GetWindow<SceneView>();
                view.LookAt(new Vector3(0,0,65),Quaternion.Euler(48,-25,0),105);
                view.Focus();
                Selection.activeGameObject=GameObject.Find("Map");
                File.WriteAllText("Artifacts/ChessFight/SceneInstalled.txt",
                    "SAVED AND OPENED: "+GrayboxBuilder.ScenePath+"\n"+DateTime.Now.ToString("s"));
                File.Delete(Request);
                Debug.Log("[CHESS FIGHT] Playable map is saved and open in Scene view.");
            }
            catch(Exception e)
            {
                File.WriteAllText("Artifacts/ChessFight/SceneInstallError.txt",e.ToString());
                Debug.LogException(e);
            }
            finally { running=false; EditorApplication.update-=InstallRequestedScene; }
        }
    }
}