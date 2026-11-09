using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush.Editor
{
    // R517: builds section rounds in Play Mode and checks the corridors between their sections (SectionCorridor): one at
    // every join, joined to both sections' entry and exit, its floor level with both neighbours' floors where they meet,
    // its walls and lid where they belong, nothing solid in its way, and pictures of every corridor (Logs/CorridorValidation):
    // from 5 m before it, from 6 m past it looking back, inside, from 16 m before and 16 m past it, and the war room's
    // end walls. Not a play test: no pawn walks through, nothing goes online.
    //
    // Batch (do not pass -quit; it exits after Play Mode):
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.SectionCorridorValidation.RunPlayBatch
    //         [-corridorSeeds 11,12] [-corridorNoCapture] -logFile <file>
    // The first seed's corridors are pictured from every place, the others' from the first two. The light is the round scene's
    // (PawnRush_Round: the default sky and a 1.2 sun at 50, -30).
    [InitializeOnLoad]
    public static class SectionCorridorValidation
    {
        const string ActiveKey = "ChessFight.R517.Corridors.ValidatePlay";
        const string ExitKey = "ChessFight.R517.Corridors.ValidateExit";
        const string CodeKey = "ChessFight.R517.Corridors.ValidateCode";
        const string Folder = "Logs/CorridorValidation";
        const string SetPath = "Assets/Settings/PawnRushSections.asset";
        const float RuntimeLimit = 1200f;   // s for the whole Play Mode run (a hang guard)
        const float Settle = 1.5f;          // s of play before a round is measured
        static readonly int[] DefaultSeeds = { 11, 12 };
        static readonly List<string> errors = new List<string>();
        static readonly List<string> failures = new List<string>();
        static readonly Queue<Action> pictures = new Queue<Action>();
        static Camera captureCamera;
        static PawnRushRound round;
        static int[] seeds;
        static int seedIndex, phase;
        static double beganAt, phaseAt, playRequestedAt, editorIdleAt;
        static bool stopping;

        static SectionCorridorValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        [MenuItem("ChessFight/Pawn Rush/Validate/Section Corridors (Play)", false, 203)]
        public static void RunMenu() => RunPlayBatch();

        public static void RunPlayBatch()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "corridors.txt"),
                "R517 corridor check: section rounds built in Play Mode, the corridors' joins, floors, walls, lids and clearance, and pictures.\n" +
                "Not a play test (no pawn walks through) and not online.\n");
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
            if (now - playRequestedAt > 60f)
            {
                EditorApplication.update -= BeginPlayWhenIdle;
                SessionState.SetBool(ActiveKey, false);
                Log("FAIL: Editor startup did not settle before Play Mode");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { editorIdleAt = now; return; }
            if (now - editorIdleAt < 2f) return;
            EditorApplication.update -= BeginPlayWhenIdle;
            Log("Beginning Play Mode after Editor startup settled");
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                seeds = SeedsFromCommandLine();
                seedIndex = phase = 0;
                round = null; errors.Clear(); failures.Clear(); pictures.Clear(); stopping = false;
                beganAt = phaseAt = EditorApplication.timeSinceStartup;
                Application.logMessageReceived += OnLog;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
                RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 1f;
                DynamicGI.UpdateEnvironment();
                var sun = new GameObject("Validation sun").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.2f; sun.color = Color.white; sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                RenderSettings.sun = sun;
                captureCamera = new GameObject("Validation camera").AddComponent<Camera>();
                captureCamera.clearFlags = CameraClearFlags.Skybox;
                captureCamera.nearClipPlane = .1f; captureCamera.farClipPlane = 400f;
                captureCamera.enabled = true;
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) CompleteExit();
        }

        static int[] SeedsFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-corridorSeeds");
            if (at < 0 || at + 1 >= args.Length) return DefaultSeeds;
            var list = new List<int>();
            foreach (var part in args[at + 1].Split(','))
                if (int.TryParse(part.Trim(), out int s) && s != 0) list.Add(s);
            return list.Count > 0 ? list.ToArray() : DefaultSeeds;
        }

        static bool NoCapture => Array.IndexOf(Environment.GetCommandLineArgs(), "-corridorNoCapture") >= 0;

        static void OnLog(string condition, string stack, LogType type)
        {
            // The render pipeline's editor package logs this about its own ray-tracing shader the first time a capture asks
            // for a render request in batch mode (R516, MiniGamePrefabValidation): not the game's.
            if (condition != null && condition.StartsWith("Host type is not matching any asset type") && condition.Contains(".urtshader")) return;
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                errors.Add(condition + "\n" + stack);
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || stopping) return;
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;   // Error Pause (R516)
            try
            {
                if (EditorApplication.timeSinceStartup - beganAt > RuntimeLimit)
                    throw new InvalidOperationException("Corridor check exceeded " + RuntimeLimit + " seconds");
                if (pictures.Count > 0) { pictures.Dequeue()(); return; }   // one picture a frame
                double now = EditorApplication.timeSinceStartup;
                switch (phase)
                {
                    case 0:
                        if (seedIndex >= seeds.Length) { Finish(); return; }
                        BuildRound(seeds[seedIndex]);
                        phase = 1; phaseAt = now;
                        return;
                    case 1:
                        if (now - phaseAt < Settle) return;
                        Physics.SyncTransforms();
                        Measure(seeds[seedIndex], seedIndex == 0);
                        seedIndex++;
                        phase = 0;
                        return;
                }
            }
            catch (Exception e)
            {
                Log("FAIL: " + e.GetType().Name + ": " + e.Message);
                StopPlay(1);
            }
        }

        static void BuildRound(int seed)
        {
            if (round == null)
            {
                var go = new GameObject("Corridor validation round");
                go.SetActive(false);
                round = go.AddComponent<PawnRushRound>();
                var so = new SerializedObject(round);
                so.FindProperty("set").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PawnRushSectionSet>(SetPath);
                so.FindProperty("buildOnAwake").boolValue = false;
                so.FindProperty("startSpots").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                go.SetActive(true);
            }
            round.Build(seed);
            Log("");
            Log("Round seed " + seed + ": " + string.Join(" | ", round.Sections.Select(s => s.name)));
        }

        static void Measure(int seed, bool allViews)
        {
            var sections = round.Sections;
            var corridors = round.Corridors;
            Check(corridors.Count == sections.Count - 1, "seed " + seed + ": " + corridors.Count + " corridors for " + sections.Count + " sections");
            for (int i = 0; i < corridors.Count && i + 1 < sections.Count; i++)
            {
                var c = corridors[i];
                var before = sections[i];
                var after = sections[i + 1];
                string name = "seed " + seed + " corridor " + (i + 1) + " (" + c.name + ", " + (before.Slot + 1) + " " + before.DisplayName + " -> " + (after.Slot + 1) + " " + after.DisplayName + ")";
                var style = SectionCorridor.After(before.Slot);
                float from = before.Kind == PawnRushSection.SlotKind.MiniGame ? SectionCorridor.FromMiniGame : 0f;
                float to = SectionCorridor.Length - (after.Kind == PawnRushSection.SlotKind.MiniGame ? SectionCorridor.IntoMiniGame : 0f);
                var notes = new List<string>();

                // Joined to both sections.
                float a = Vector3.Distance(c.position, before.ExitWorld.position), b = Vector3.Distance(SectionCorridor.Exit(c).position, after.EntryWorld.position);
                Check(a < .01f && b < .01f, name + ": joins off by " + a.ToString("0.000") + " / " + b.ToString("0.000") + " m");

                // The floor: the corridor's own (z 0.3 .. 9.7) at 0, and either neighbour's 0.6 m beyond the ends.
                foreach (float z in new[] { -.6f, .3f, 5f, 9.7f, SectionCorridor.Length + .6f })
                    foreach (float x in new[] { -5.5f, 0f, 5.5f })
                    {
                        var top = c.TransformPoint(new Vector3(x, 2.5f, z));
                        bool own = z > 0f && z < SectionCorridor.Length;
                        if (!Physics.Raycast(top, -c.up, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            Check(false, name + ": no floor at x " + x + ", z " + z);
                            continue;
                        }
                        float y = c.InverseTransformPoint(hit.point).y;
                        if (own) Check(Mathf.Abs(y) < .02f, name + ": corridor floor at x " + x + ", z " + z + " is " + y.ToString("0.000") + " m (" + hit.collider.name + ")");
                        else if (Mathf.Abs(y) > .06f) Check(false, name + ": step at the join, x " + x + ", z " + z + ": " + y.ToString("0.000") + " m (" + hit.collider.name + ")");
                        else if (Mathf.Abs(y) > .005f) notes.Add("join x " + x + " z " + z + " " + y.ToString("+0.000;-0.000") + " m");
                    }

                // The walls at |x| 6 along their length, the lid over the middle.
                foreach (float z in new[] { from + .3f, 5f, to - .3f })
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var start = c.TransformPoint(new Vector3(0f, 1.2f, z));
                        var dir = c.TransformDirection(new Vector3(side, 0f, 0f));
                        bool wall = Physics.Raycast(start, dir, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore);
                        float x = wall ? Mathf.Abs(c.InverseTransformPoint(hit.point).x) : -1f;
                        Check(wall && Mathf.Abs(x - SectionCorridor.Half) < .02f, name + ": wall at z " + z + " side " + side + " is at " + x.ToString("0.00") + " m");
                    }
                {
                    bool lid = Physics.Raycast(c.TransformPoint(new Vector3(0f, 1f, 5f)), c.up, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore);
                    float y = lid ? c.InverseTransformPoint(hit.point).y : -1f;
                    Check(lid && Mathf.Abs(y - SectionCorridor.Lid(style)) < .02f, name + ": lid at " + y.ToString("0.00") + " m, expected " + SectionCorridor.Lid(style));
                }

                // Nothing solid in the way (a pawn's height over the walked width, end to end).
                var solid = Physics.OverlapBox(c.TransformPoint(new Vector3(0f, 1.65f, SectionCorridor.Length * .5f)), new Vector3(5.9f, 1.5f, SectionCorridor.Length * .5f - .1f),
                                               c.rotation, ~0, QueryTriggerInteraction.Ignore);
                Check(solid.Length == 0, name + ": solid in the way: " + string.Join(", ", solid.Select(s => s.name).Distinct()));
                Check(round.InCorridor(c.TransformPoint(new Vector3(0f, 1f, 5f))), name + ": InCorridor misses its middle");

                Log(name + ": checked" + (notes.Count > 0 ? " (" + string.Join("; ", notes) + ")" : ""));
                if (NoCapture) continue;
                string id = "s" + seed + "_c" + (i + 1) + "_" + style;
                var corridor = c;
                pictures.Enqueue(() => CaptureView(corridor, id + "_approach", new Vector3(0f, 2.8f, -5f), new Vector3(0f, 2.2f, 8f), 68f));
                pictures.Enqueue(() => CaptureView(corridor, id + "_back", new Vector3(0f, 3f, 16f), new Vector3(0f, 2.6f, 4f), 68f));
                if (after.Kind == PawnRushSection.SlotKind.Final)
                {
                    // The war room's start floor: the end walls either side of the way in.
                    pictures.Enqueue(() => CaptureView(corridor, id + "_finalE", new Vector3(8f, 3.5f, 21f), new Vector3(-3f, 5f, 10f), 72f));
                    pictures.Enqueue(() => CaptureView(corridor, id + "_finalW", new Vector3(-8f, 3.5f, 21f), new Vector3(3f, 5f, 10f), 72f));
                }
                if (!allViews) continue;
                pictures.Enqueue(() => CaptureView(corridor, id + "_inside", new Vector3(-4.2f, 1.8f, 1f), new Vector3(1.5f, 3f, 9f), 78f));
                pictures.Enqueue(() => CaptureView(corridor, id + "_far", new Vector3(0f, 6f, -16f), new Vector3(0f, 3.5f, 4f), 58f));
                pictures.Enqueue(() => CaptureView(corridor, id + "_farback", new Vector3(0f, 6f, 26f), new Vector3(0f, 3.5f, 6f), 58f));
            }
        }

        static void Check(bool ok, string failure)
        {
            if (ok) return;
            failures.Add(failure);
            Log("FAIL: " + failure);
        }

        // One picture from `eye` toward `target` (both in the corridor's frame) into the validation folder.
        static void CaptureView(Transform frame, string name, Vector3 eye, Vector3 target, float fieldOfView)
        {
            if (frame == null) return;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Log("NOT RUN: capture " + name + " (null graphics device)"); return; }
            captureCamera.transform.position = frame.TransformPoint(eye);
            captureCamera.transform.LookAt(frame.TransformPoint(target), frame.up);
            captureCamera.fieldOfView = fieldOfView;
            captureCamera.aspect = 1.5f;
            var texture = new RenderTexture(1536, 1024, 24, RenderTextureFormat.ARGB32);
            var before = RenderTexture.active;
            Texture2D picture = null;
            try
            {
                texture.Create();
                var request = new RenderPipeline.StandardRequest { destination = texture };
                if (GraphicsSettings.currentRenderPipeline != null)
                {
                    if (!RenderPipeline.SupportsRenderRequest(captureCamera, request)) { Log("NOT RUN: capture " + name + " (render request unsupported)"); return; }
                    RenderPipeline.SubmitRenderRequest(captureCamera, request);
                }
                else { captureCamera.targetTexture = texture; captureCamera.Render(); captureCamera.targetTexture = null; }
                RenderTexture.active = texture;
                picture = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                picture.Apply();
                string path = Path.Combine(Folder, name + ".png");
                File.WriteAllBytes(path, picture.EncodeToPNG());
                Log("Captured " + path);
            }
            finally
            {
                RenderTexture.active = before;
                if (picture != null) Object.DestroyImmediate(picture);
                texture.Release(); Object.DestroyImmediate(texture);
            }
        }

        static void Finish()
        {
            foreach (var e in errors) Log("ERROR logged in Play: " + e.Split('\n')[0]);
            int code = failures.Count == 0 && errors.Count == 0 ? 0 : 1;
            Log("Corridor result: " + (code == 0 ? "PASS" : "FAIL (" + failures.Count + " checks, " + errors.Count + " errors)"));
            StopPlay(code);
        }

        static void Log(string value)
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Path.Combine(Folder, "corridors.txt"), value + "\n");
            if (value.Length > 0) Debug.Log("[CorridorValidation] " + value);
        }

        static void StopPlay(int code)
        {
            stopping = true;
            Application.logMessageReceived -= OnLog;
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
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(CodeKey, 1));
        }
    }
}
