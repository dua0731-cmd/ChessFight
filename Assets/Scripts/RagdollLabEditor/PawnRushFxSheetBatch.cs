using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    /// <summary>
    /// Films the skills' effect contact sheets (PawnRushFxSheet) in a batch-mode Unity with a graphics device, as the
    /// carriage yard's bot test plays (R660): opens PawnRush_SkillTest, enters Play once the editor has settled, films the
    /// shots named by -fxShots (comma-separated, or "set3") into -fxOut, leaves Play and quits. Tools/Film-SkillFx.ps1
    /// starts it: Unity -batchmode -projectPath ... -executeMethod ChessFight.RagdollLab.Editor.PawnRushFxSheetBatch.Run.
    /// </summary>
    [InitializeOnLoad]
    public static class PawnRushFxSheetBatch
    {
        const string Scene = "Assets/Scenes/SkillTest/PawnRush_SkillTest/PawnRush_SkillTest.unity";
        const string ActiveKey = "ChessFight.FxSheet.Active", ExitKey = "ChessFight.FxSheet.Exit", BeganKey = "ChessFight.FxSheet.Began";
        const double PlayLimit = 900.0;   // seconds of Play before giving up
        static double requestedAt, idleAt;

        static PawnRushFxSheetBatch()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += Quit;
            else if (EditorApplication.isPlaying)
                EditorApplication.update += Watchdog;
        }

        [MenuItem("ChessFight/Pawn Rush/Skill FX contact sheets (3판)", false, 230)]
        public static void RunMenu() => Run();

        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitKey, false);
            requestedAt = idleAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= BeginPlayWhenIdle;
            EditorApplication.update += BeginPlayWhenIdle;
        }

        static void BeginPlayWhenIdle()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - requestedAt > 300.0)
            {
                EditorApplication.update -= BeginPlayWhenIdle;
                SessionState.SetBool(ActiveKey, false);
                Debug.LogError("[FxSheet] FAIL: the editor did not settle before Play");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { idleAt = now; return; }
            if (now - idleAt < 2.0) return;
            EditorApplication.update -= BeginPlayWhenIdle;
            Debug.Log("[FxSheet] entering Play");
            EditorApplication.isPlaying = true;
        }

        static string Arg(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : fallback;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode && !SessionState.GetBool(ExitKey, false))
            {
                SessionState.SetFloat(BeganKey, (float)EditorApplication.timeSinceStartup);
                EditorApplication.update -= Watchdog;
                EditorApplication.update += Watchdog;
                string folder = Arg("-fxOut", "Logs/FxSheets");
                PawnRushFxSheet.Note = Arg("-fxNote", "");
                PawnRushFxSheet.Begin(folder, Arg("-fxShots", "set3"), () =>
                {
                    SessionState.SetBool(ExitKey, true);
                    EditorApplication.isPlaying = false;
                });
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) Quit();
        }

        static void Watchdog()
        {
            if (!EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(BeganKey, 0f) < PlayLimit) return;
            EditorApplication.update -= Watchdog;
            Debug.LogError($"[FxSheet] FAIL: still filming after {PlayLimit} s ({PawnRushFxSheet.Status})");
            SessionState.SetBool(ExitKey, true);
            EditorApplication.isPlaying = false;
        }

        static void Quit()
        {
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(ExitKey, false);
            Debug.Log("[FxSheet] done: " + PawnRushFxSheet.Status);
            if (Application.isBatchMode) EditorApplication.Exit(PawnRushFxSheet.Status.StartsWith("done") ? 0 : 2);
        }
    }
}
