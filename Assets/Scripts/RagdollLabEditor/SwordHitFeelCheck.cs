using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    /// <summary>
    /// R808 (승규 님 11-23: "자동 시험 돌려줘", after the online play's "피격을 해도 허공을 가로지르는 것 같음" and its fixes):
    /// the match scene (SwordFight.unity) played alone in Play Mode with SwordHitFeelProbe on it, which measures the click
    /// cut's windups, combo beat, the hit's catch and hold, the cutter found, the hit mark, the sounds' levels, the
    /// protection shell and a client's timing (its report: Logs/SwordHitFeel/report.txt, with pictures). Batch on the
    /// test copy so the Editor can keep this project open: Tools/Run-SwordHitFeel.ps1, which runs
    ///   Unity -batchmode -projectPath &lt;copy&gt; -executeMethod ChessFight.RagdollLab.Editor.SwordHitFeelCheck.RunPlayBatch -logFile &lt;file&gt;
    /// (exits by itself, never -quit: 0 when every check passed). Also from the menu. Not a play test with people and not
    /// online.
    /// </summary>
    [InitializeOnLoad]
    public static class SwordHitFeelCheck
    {
        const string ActiveKey = "ChessFight.R808.SwordHitFeel.Active";
        const string ExitKey = "ChessFight.R808.SwordHitFeel.Exit";
        const string CodeKey = "ChessFight.R808.SwordHitFeel.Code";
        const string Scene = "Assets/Scenes/SwordFight.unity";
        const double RuntimeLimit = 240, StartLimit = 120;
        static double requestedAt, idleAt, beganAt;

        static SwordHitFeelCheck()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        [MenuItem("ChessFight/Sword Fight/Check Hit Feel (R808)", false, 40)]
        public static void RunMenu() => RunPlayBatch();

        public static void RunPlayBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitKey, false);
            SessionState.SetInt(CodeKey, 1);
            requestedAt = idleAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= BeginPlayWhenIdle;
            EditorApplication.update += BeginPlayWhenIdle;
        }

        static void BeginPlayWhenIdle()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - requestedAt > StartLimit)
            {
                EditorApplication.update -= BeginPlayWhenIdle;
                SessionState.SetBool(ActiveKey, false);
                Debug.LogError("[SwordHitFeel] FAIL: the Editor did not settle before Play Mode");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { idleAt = now; return; }
            if (now - idleAt < 2f) return;
            EditorApplication.update -= BeginPlayWhenIdle;
            Debug.Log("[SwordHitFeel] entering Play Mode in " + Scene);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                beganAt = EditorApplication.timeSinceStartup;
                new GameObject("Sword hit feel probe (R808)").AddComponent<SwordHitFeelProbe>();
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) CompleteExit();
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            // R516: the Editor's Error Pause would stop Play on a pipeline message that is not the game's.
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            bool late = EditorApplication.timeSinceStartup - beganAt > RuntimeLimit;
            if (!SwordHitFeelProbe.Done && !late) return;
            if (late && !SwordHitFeelProbe.Done) Debug.LogError("[SwordHitFeel] FAIL: the check ran past " + RuntimeLimit + " s");
            int code = SwordHitFeelProbe.Done && SwordHitFeelProbe.Failures == 0 ? 0 : 1;
            EditorApplication.update -= Tick;
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(ExitKey, true);
            SessionState.SetInt(CodeKey, code);
            EditorApplication.isPlaying = false;
        }

        static void CompleteExit()
        {
            if (!SessionState.GetBool(ExitKey, false)) return;
            SessionState.SetBool(ExitKey, false);
            int code = SessionState.GetInt(CodeKey, 1);
            Debug.Log("[SwordHitFeel] done, exit code " + code);
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
