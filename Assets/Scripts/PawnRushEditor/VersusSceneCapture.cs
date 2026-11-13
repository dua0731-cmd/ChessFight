using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ChessFight.Game;
using ChessFight.Network;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush.Editor
{
    // R625: pictures of Pawn Rush's entrance screen from the host's start: the versus scene (LoadingScreenView.Versus.cs)
    // and the course reveal after it, at chosen times, with a made-up roster (white 6 with this PC third and a bot last,
    // black the same). In Play Mode a loading screen is built as MatchLoader builds it, its panel drawing into a texture
    // (RuntimePanels.CaptureTarget), and each picture is that texture after the screen has shown the time for a few frames.
    // The study behind is the real LoadingStudio. Not a play test and not online: nobody matched, no scene loads.
    //
    // Batch (do not pass -quit; it exits after Play Mode; with a graphics device):
    //   Unity -batchmode -projectPath <project> -executeMethod ChessFight.PawnRush.Editor.VersusSceneCapture.RunPlayBatch
    //         [-versusTimes .5,1,2.4] [-versusTeams 6,6] [-versusOut Logs/VersusCapture] -logFile <file>
    // Pictures: <out>/versus_<time>.png (1920 x 1080), and <out>/versus.txt.
    [InitializeOnLoad]
    public static class VersusSceneCapture
    {
        const string ActiveKey = "ChessFight.R625.Versus.Capture";
        const string ExitKey = "ChessFight.R625.Versus.Exit";
        const string CodeKey = "ChessFight.R625.Versus.Code";
        const string DefaultFolder = "Logs/VersusCapture";
        const float RuntimeLimit = 400f;
        // Frames each time is held before its picture: layout, the studio's draw and the panel's; the first waits longer
        // (fonts, the studio's first frame, textures).
        const int HoldFrames = 4, FirstHold = 60;
        static readonly float[] DefaultTimes =
            { .3f, .55f, .75f, 1f, 1.3f, 1.7f, 2.1f, 2.4f, 2.5f, 2.7f, 3.4f, 4.25f, 4.5f, 4.7f, 4.9f, 5.2f, 5.6f, 6.5f, 8f };
        static readonly string[] White = { "폰돌이", "나이트라이더", "룩앤롤", "비숍뷰", "앙파상", "BOT 1" };
        static readonly string[] Black = { "체크메이트", "흑기사", "캐슬링", "퀸즈갬빗", "스테일메이트", "BOT 2" };

        static LoadingScreenView view;
        static RenderTexture target;
        static float[] times = DefaultTimes;
        static int index, held;
        static string folder = DefaultFolder;
        static double beganAt, playRequestedAt, editorIdleAt;
        static readonly List<string> errors = new List<string>();
        static readonly List<string> lines = new List<string>();

        static VersusSceneCapture()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        [MenuItem("ChessFight/Pawn Rush/Validate/Versus Scene Capture (Play)", false, 205)]
        public static void RunMenu() => RunPlayBatch();

        public static void RunPlayBatch()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitKey, false);
            SessionState.SetInt(CodeKey, 0);
            playRequestedAt = editorIdleAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= BeginPlayWhenIdle;
            EditorApplication.update += BeginPlayWhenIdle;
        }

        static void BeginPlayWhenIdle()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - playRequestedAt > 90f)
            {
                EditorApplication.update -= BeginPlayWhenIdle;
                SessionState.SetBool(ActiveKey, false);
                Debug.LogError("[VersusCapture] FAIL: Editor startup did not settle before Play Mode");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { editorIdleAt = now; return; }
            if (now - editorIdleAt < 2f) return;
            EditorApplication.update -= BeginPlayWhenIdle;
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false)) Begin();
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) CompleteExit();
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        static void Begin()
        {
            errors.Clear();
            lines.Clear();
            index = held = 0;
            beganAt = EditorApplication.timeSinceStartup;
            folder = Arg("-versusOut") ?? DefaultFolder;
            Directory.CreateDirectory(folder);
            times = DefaultTimes;
            var given = Arg("-versusTimes");
            if (!string.IsNullOrEmpty(given))
            {
                var list = new List<float>();
                foreach (var part in given.Split(','))
                    if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out float t)) list.Add(t);
                if (list.Count > 0) times = list.ToArray();
            }
            int white = 6, black = 6;
            var teams = Arg("-versusTeams");
            if (!string.IsNullOrEmpty(teams))
            {
                var parts = teams.Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int b))
                {
                    white = Mathf.Clamp(w, 1, 6);
                    black = Mathf.Clamp(b, 1, 6);
                }
            }
            Application.logMessageReceived += OnLog;

            target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { name = "Versus capture" };
            target.Create();
            RuntimePanels.CaptureTarget = target;
            var host = new GameObject("Loading Screen (capture)");
            try
            {
                view = host.AddComponent<LoadingScreenView>();
                view.Build(LoadingContent.For(GameModes.KingRush, Mathf.Max(white, black)));
            }
            finally { RuntimePanels.CaptureTarget = null; }
            Fill(view.State, white, black);
            view.State.Reveal = times[0];
            lines.Add($"R625 versus capture: white {white}, black {black}, this PC = white 3 (or the first), times {string.Join(", ", times)}");
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        // The roster as MatchLoader lists a match's: by team, this PC's player third on white, a bot last on each side.
        static void Fill(LoadingState s, int white, int black)
        {
            s.Clear();
            s.Matching = false;
            s.CanCancel = false;
            s.Expected = white + black;
            s.Step = "코스를 불러오는 중";
            s.Percent = 40;
            s.CourseKnown = true;
            s.CourseSeed = PawnRushSections.MatchSeed(0x5A11CE0FUL);
            ulong key = 1;
            for (int i = 0; i < white; i++) Add(s, 0, i == white - 1 && white >= 4 ? "BOT 1" : White[i], i == white - 1 && white >= 4, key++);
            for (int i = 0; i < black; i++) Add(s, 1, i == black - 1 && black >= 4 ? "BOT 2" : Black[i], i == black - 1 && black >= 4, key++);
            s.Me = Mathf.Min(2, white - 1);
            s.Host = 0;
            s.Manager = 0;
        }

        static void Add(LoadingState s, int team, string name, bool bot, ulong key)
        {
            s.Keys.Add(key);
            s.Teams.Add(team);
            s.Joined.Add(true);
            s.Ready.Add(false);
            s.Names.Add(name);
            s.Bots.Add(bot);
            s.CanGive.Add(false);
            s.CanKick.Add(false);
            s.Scores.Add(0);
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (condition != null && condition.StartsWith("Host type is not matching any asset type") && condition.Contains(".urtshader")) return;
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) errors.Add(condition + "\n" + stack);
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || view == null) return;
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            try
            {
                if (EditorApplication.timeSinceStartup - beganAt > RuntimeLimit) throw new InvalidOperationException("capture took too long");
                if (index >= times.Length) { Finish(errors.Count == 0 ? 0 : 1); return; }
                view.State.Reveal = times[index];
                if (++held < (index == 0 ? FirstHold : HoldFrames)) return;
                held = 0;
                Save(times[index]);
                index++;
            }
            catch (Exception e)
            {
                errors.Add(e.ToString());
                Finish(1);
            }
        }

        static void Save(float t)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var picture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            picture.Apply();
            RenderTexture.active = previous;
            string path = Path.Combine(folder, "versus_" + t.ToString("00.00", CultureInfo.InvariantCulture) + ".png");
            File.WriteAllBytes(path, picture.EncodeToPNG());
            Object.DestroyImmediate(picture);
            lines.Add("picture " + path);
        }

        static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            foreach (var e in errors) lines.Add("ERROR " + e);
            lines.Add(code == 0 ? "PASS: pictures taken, no errors logged" : "FAIL: see the errors above");
            try { File.WriteAllLines(Path.Combine(folder, "versus.txt"), lines); } catch (Exception) { }
            if (view != null) Object.Destroy(view.gameObject);
            view = null;
            if (target != null) { target.Release(); Object.Destroy(target); target = null; }
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetInt(CodeKey, code);
            SessionState.SetBool(ExitKey, true);
            EditorApplication.isPlaying = false;
        }

        static void CompleteExit()
        {
            SessionState.SetBool(ExitKey, false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(CodeKey, 1));
        }
    }
}
