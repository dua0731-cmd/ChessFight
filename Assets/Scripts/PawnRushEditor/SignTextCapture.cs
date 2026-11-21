using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ChessFight.Game;
using ChessFight.Network;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush.Editor
{
    // R753: pictures of the course's 3D words (SignText) in a language, each sign from straight in front - the royal
    // carriage yard and a mini-game room, built in Play Mode in the course prefab test scene as CheckpointBoxCapture
    // builds them. The AI's own look at whether a translated sign fits its board, not a user check.
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.SignTextCapture.RunPlayBatch
    //         -captureLanguage zh-Hans
    // (no -quit, no -nographics; Tools/Run-SignTextCapture.ps1). Pictures and a list: Logs/SignText/<language>.
    [InitializeOnLoad]
    public static class SignTextCapture
    {
        const string ActiveKey = "SignTextCapture.Active", ExitKey = "SignTextCapture.Exit", CodeKey = "SignTextCapture.Code";
        const double Settle = 6;
        const int MaxPictures = 18;

        static string language = "";
        static string Folder => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "SignText", language.Length > 0 ? language : "ko");

        static int stage, failures, taken;
        static bool room;
        static double stepAt, requested, idleAt;
        static Camera captureCamera;
        static readonly HashSet<string> seen = new HashSet<string>();

        static SignTextCapture()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        [MenuItem("ChessFight/Pawn Rush/Validate/Course signs in a language (Play, pictures)", false, 214)]
        public static void RunPlayBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            language = Argument();
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "report.txt"), "Course signs (SignText, R753) in '" + (language.Length > 0 ? language : "ko") + "'.\n");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CoursePrefabMenu.TestScene) == null) CoursePrefabMenu.BuildScene();
            EditorSceneManager.OpenScene(CoursePrefabMenu.TestScene, OpenSceneMode.Single);
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitKey, false);
            SessionState.SetInt(CodeKey, 1);
            requested = idleAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= BeginWhenIdle;
            EditorApplication.update += BeginWhenIdle;
        }

        static string Argument()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-captureLanguage") return args[i + 1].Trim();
            return "";
        }

        static void BeginWhenIdle()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - requested > 180) { EditorApplication.update -= BeginWhenIdle; Log("FAIL: the Editor did not settle"); if (Application.isBatchMode) EditorApplication.Exit(1); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { idleAt = now; return; }
            if (now - idleAt < 2) return;
            EditorApplication.update -= BeginWhenIdle;
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                language = Argument();
                stage = 0; failures = 0; taken = 0; room = false;
                seen.Clear();
                if (language.Length > 0 && !Loc.Use(language)) { failures++; Log("FAIL: the string table has no column '" + language + "'"); }
                Log("language " + Loc.Lang);
                stepAt = EditorApplication.timeSinceStartup;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) CompleteExit();
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - stepAt < Settle) return;
            stepAt = now;
            try
            {
                switch (stage++)
                {
                    case 0: if (Open(typeof(RoyalCarriageYard))) Translate(); break;
                    case 1: Signs("yard"); break;
                    case 2: if (Open(typeof(MiniGameSection), false)) { Translate(); room = true; } break;
                    case 3: if (room) Signs("room"); Finish(); break;
                }
            }
            catch (System.Exception e)
            {
                failures++;
                Log("FAIL: the capture threw " + e);
                Finish();
            }
        }

        static PawnRushSection Current => Object.FindFirstObjectByType<CoursePrefabTest>()?.Current;

        static bool Open(System.Type kind, bool needed = true)
        {
            var test = Object.FindFirstObjectByType<CoursePrefabTest>();
            if (test == null) { failures++; Log("FAIL: no course prefab test in the scene"); return false; }
            var entries = typeof(CoursePrefabTest).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(test) as System.Collections.IList;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                var prefab = entries[i].GetType().GetField("prefab")?.GetValue(entries[i]) as GameObject;
                if (prefab != null && prefab.GetComponent(kind) != null) { test.Select(i); Log("opened " + prefab.name); return true; }
            }
            if (needed) failures++;
            Log((needed ? "FAIL: " : "NOT RUN: ") + kind.Name + " is not in the course prefab test's list");
            return false;
        }

        // The course prefab test builds a section without the round: sweep it as PawnRushRound does.
        static void Translate()
        {
            var section = Current;
            if (section == null) { failures++; Log("FAIL: nothing built"); return; }
            SignText.TranslateUnder(section.transform);
        }

        // Every SignText word under the section, one picture each (a text once), straight in front of it.
        static void Signs(string prefix)
        {
            var section = Current;
            if (section == null) { failures++; Log("FAIL: nothing built"); return; }
            int words = 0;
            Begin();
            try
            {
                foreach (var sign in section.GetComponentsInChildren<SignText>(true))
                {
                    var text = sign.GetComponent<TextMesh>();
                    var renderer = sign.GetComponent<MeshRenderer>();
                    if (text == null || renderer == null) continue;
                    words++;
                    if (!sign.gameObject.activeInHierarchy || !seen.Add(text.text)) continue;
                    var b = renderer.bounds;
                    Log($"{prefix}: \"{text.text.Replace("\n", "\\n")}\" width {b.size.magnitude:0.00} m at {section.transform.InverseTransformPoint(b.center):0.0}");
                    if (taken >= MaxPictures) continue;
                    float reach = Mathf.Max(b.extents.magnitude, .5f) * 1.6f / Mathf.Tan(20f * Mathf.Deg2Rad);
                    var forward = text.transform.forward;
                    Capture(prefix + "_" + (++taken).ToString("00"), b.center - forward * reach, b.center, text.transform.up);
                }
            }
            finally { End(); }
            Log(prefix + ": " + words + " words with SignText");
            if (words == 0) { failures++; Log("FAIL: no SignText words in " + section.name); }
        }

        static void Begin()
        {
            var go = new GameObject("Sign capture camera");
            captureCamera = go.AddComponent<Camera>();
            captureCamera.enabled = false;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = new Color(.06f, .07f, .11f);
            captureCamera.fieldOfView = 40f;
            captureCamera.nearClipPlane = .05f;
            captureCamera.farClipPlane = 400f;
        }

        static void End()
        {
            if (captureCamera != null) Object.Destroy(captureCamera.gameObject);
        }

        static void Capture(string name, Vector3 eye, Vector3 target, Vector3 up)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Log("NOT RUN: picture " + name + " (no graphics device)"); return; }
            const int width = 1280, height = 720;
            captureCamera.transform.position = eye;
            captureCamera.transform.rotation = Quaternion.LookRotation(target - eye, up);
            captureCamera.aspect = (float)width / height;
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var before = RenderTexture.active;
            Texture2D picture = null;
            try
            {
                texture.Create();
                var request = new RenderPipeline.StandardRequest { destination = texture };
                if (GraphicsSettings.currentRenderPipeline != null)
                {
                    if (!RenderPipeline.SupportsRenderRequest(captureCamera, request)) { Log("NOT RUN: picture " + name + " (render request unsupported)"); return; }
                    RenderPipeline.SubmitRenderRequest(captureCamera, request);
                }
                else { captureCamera.targetTexture = texture; captureCamera.Render(); captureCamera.targetTexture = null; }
                RenderTexture.active = texture;
                picture = new Texture2D(width, height, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                picture.Apply();
                File.WriteAllBytes(Path.Combine(Folder, name + ".png"), picture.EncodeToPNG());
                Log("picture " + name + ".png");
            }
            finally
            {
                RenderTexture.active = before;
                if (picture != null) Object.Destroy(picture);
                texture.Release();
                Object.Destroy(texture);
            }
        }

        static void Finish()
        {
            Log(failures == 0 ? "RESULT PASS" : "RESULT FAIL (" + failures + ")");
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
            Debug.Log("[SignTextCapture] " + value);
        }

        static void CompleteExit()
        {
            if (!SessionState.GetBool(ExitKey, false)) return;
            SessionState.SetBool(ExitKey, false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(CodeKey, 1));
        }
    }
}
