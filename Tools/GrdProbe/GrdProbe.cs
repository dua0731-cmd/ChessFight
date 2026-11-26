using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ChessFight.Gameplay;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush.Editor
{
    // R719 GPU Resident Drawer probe (Docs/Environment/GPU_RESIDENT_DRAWER.md). Lives in Tools/GrdProbe, outside Assets:
    // Tools/GrdProbe/Run-GrdProbe.ps1 puts it into a COPY of the project (<copy>/Assets/GrdProbe/Editor, its own editor
    // assembly - the GRD types are in Unity.RenderPipelines.GPUDriven.Runtime) and runs it in batch mode with a graphics
    // device, so the Editor keeps the original open:
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.GrdProbe.RunPlayBatch
    //         -grdProbe baseline|ab|sweep [-grdProbeOut <dir>] [-grdProbeRounds 3] [-grdProbeFrames 30] [-grdProbeOnly a,b]
    // baseline: the project's settings as they are (GRD must be off) - the Royal Library at three views, camera render times.
    // ab:       BatchRendererGroup Variants = Keep All for the run, then GRD off / on / on + GPU occlusion culling taken in
    //           turns in one play session at the same views: times, pictures (the first frame after a jump and steady) and
    //           their differences, GRD's counters, a fast turn and a run through the door between the gallery and the
    //           chess room (pop-in), drawn by hand and by the player loop; also whether the Editor reloads an edited
    //           ProjectSettings/GraphicsSettings.asset.
    // sweep:    every course of the course prefab test, every mini-game room, the section test and the plain scenes:
    //           a picture per variant, the difference to "off", magenta pixels, GRD's counters, console messages.
    // What is timed is one camera drawn into a 1920x1080 target (CPU: Camera.Render returns; total: plus waiting for the
    // GPU to finish) and the game loop beside it - not the game's frame rate, not a player build, and in batch mode the
    // Editor draws on one thread (no render thread). Settings are put back before Unity quits.
    [InitializeOnLoad]
    public static class GrdProbe
    {
        const string K = "GrdProbe.";
        enum Variant { Off, On, Occl, Off2 }

        static string Arg(string name, string fallback)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return fallback;
        }

        static string Mode => Arg("-grdProbe", "");
        static string Out => Arg("-grdProbeOut", Path.Combine(Directory.GetCurrentDirectory(), "Logs", "GrdProbe", Mode));
        static int Rounds => int.Parse(Arg("-grdProbeRounds", "3"), CultureInfo.InvariantCulture);
        static int Frames => int.Parse(Arg("-grdProbeFrames", "30"), CultureInfo.InvariantCulture);
        static string Only => Arg("-grdProbeOnly", "");

        static string[] Jobs()
        {
            if (Mode == "sweep")
            {
                var all = new[] { "courses", "minigames", "sections", "sword", "arena", "lobby", "intro", "victory", "lose" };
                if (Only.Length > 0) all = all.Where(j => Only.Split(',').Contains(j)).ToArray();
                return all;
            }
            return new[] { "library" };
        }

        static string ScenePath(string job)
        {
            switch (job)
            {
                case "library":
                case "courses": return CoursePrefabMenu.TestScene;
                case "minigames": return MiniGamePrefabMenu.TestScene;
                case "sections": return SectionPrefabMenu.TestScene;
                case "sword": return "Assets/Scenes/SwordFight.unity";
                case "arena": return "Assets/Scenes/SkillTest/SwordFight_ArenaSkillTest/SwordFight_ArenaSkillTest.unity";
                case "lobby": return "Assets/Scenes/Lobby.unity";
                case "intro": return "Assets/Scenes/Intro.unity";
                case "victory": return "Assets/Scenes/PawnRushVictory.unity";
                case "lose": return "Assets/Scenes/PawnRushLose.unity";
            }
            return null;
        }

        static GrdProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(K + "Exit", false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        // ---- batch entry -------------------------------------------------------------------------------------------

        public static void RunPlayBatch()
        {
            Directory.CreateDirectory(Out);
            Directory.CreateDirectory(Path.Combine(Out, "shots"));
            File.WriteAllText(Path.Combine(Out, "report.txt"), "GRD probe (R719) mode " + Mode + " " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n");
            File.WriteAllText(Path.Combine(Out, "timings.csv"), "job,shot,variant,round,frame,cpu_ms,total_ms,playerloop_ms,grd_on,pipeline\n");
            File.WriteAllText(Path.Combine(Out, "shots.csv"), "job,shot,variant,magenta_px,diff16_pct,diff48_pct,mean_diff,file,grd_on\n");
            File.WriteAllText(Path.Combine(Out, "console.txt"), "");
            var urp = ProjectAsset();
            Log("device: " + SystemInfo.graphicsDeviceName + " | " + SystemInfo.graphicsDeviceVersion + " | " + SystemInfo.graphicsDeviceType + " | threading " + SystemInfo.renderingThreadingMode);
            Log("cpu: " + SystemInfo.processorType + " x" + SystemInfo.processorCount + ", memory " + SystemInfo.systemMemorySize + " MB, batch " + Application.isBatchMode);
            Log("settings as found: BRG variants " + EditorGraphicsSettings.batchRendererGroupShaderStrippingMode + " (" + (int)EditorGraphicsSettings.batchRendererGroupShaderStrippingMode + ")"
                + ", PC_RPAsset GRD " + (urp != null ? urp.gpuResidentDrawerMode + " occlusion " + urp.gpuResidentDrawerEnableOcclusionCullingInCameras + " small mesh " + urp.smallMeshScreenPercentage : "missing")
                + ", quality " + QualitySettings.GetQualityLevel() + " " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
            SessionState.SetInt(K + "BrgWas", (int)EditorGraphicsSettings.batchRendererGroupShaderStrippingMode);
            if (Mode == "baseline")
            {
                if (urp != null && urp.gpuResidentDrawerMode != GPUResidentDrawerMode.Disabled) Fail("baseline needs GRD off on PC_RPAsset");
            }
            else if (Mode == "ab" || Mode == "sweep")
            {
                if (Mode == "ab") { BeginReloadTest(); return; }
                KeepAllAndOpen();
                return;
            }
            else { Fail("unknown -grdProbe mode '" + Mode + "'"); SessionState.SetBool(K + "Exit", true); CompleteExit(); return; }
            SessionState.SetInt(K + "Job", 0);
            SessionState.SetInt(K + "Failures", SessionState.GetInt(K + "Failures", 0));
            OpenJob();
        }

        static void KeepAllAndOpen()
        {
            SetBrg(BatchRendererGroupStrippingMode.KeepAll);
            Log("BRG variants set to " + EditorGraphicsSettings.batchRendererGroupShaderStrippingMode + " (" + (int)EditorGraphicsSettings.batchRendererGroupShaderStrippingMode + ") for this run");
            if (EditorGraphicsSettings.batchRendererGroupShaderStrippingMode != BatchRendererGroupStrippingMode.KeepAll) Fail("could not set BatchRendererGroup Variants to Keep All");
            SessionState.SetInt(K + "Job", 0);
            OpenJob();
        }

        // Does a running Editor pick up ProjectSettings/GraphicsSettings.asset changed on disk (as a text edit or a git
        // checkout would)? Write m_BrgStripping 2 into the file, refresh, read what Unity uses, put the file back.
        static string reloadText;
        static double reloadAt;
        static int reloadStep, reloadBefore;

        static void BeginReloadTest()
        {
            const string path = "ProjectSettings/GraphicsSettings.asset";
            reloadText = File.ReadAllText(path);
            reloadBefore = (int)EditorGraphicsSettings.batchRendererGroupShaderStrippingMode;
            File.WriteAllText(path, System.Text.RegularExpressions.Regex.Replace(reloadText, @"m_BrgStripping: \d+", "m_BrgStripping: 2"));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            reloadAt = EditorApplication.timeSinceStartup;
            reloadStep = 0;
            EditorApplication.update -= ReloadTick;
            EditorApplication.update += ReloadTick;
        }

        static void ReloadTick()
        {
            if (EditorApplication.timeSinceStartup - reloadAt < 3) return;
            if (reloadStep == 0)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                reloadStep = 1;
                reloadAt = EditorApplication.timeSinceStartup;
                return;
            }
            EditorApplication.update -= ReloadTick;
            int after = (int)EditorGraphicsSettings.batchRendererGroupShaderStrippingMode;
            Log("reload test: m_BrgStripping written as 2 into ProjectSettings/GraphicsSettings.asset while this Editor runs; the Editor reads "
                + reloadBefore + " before and " + after + " after two AssetDatabase.Refresh calls 3 s apart -> "
                + (after == 2 ? "it reloads the file" : "it does NOT reload the file (a restart or the Graphics window is needed)"));
            File.WriteAllText("ProjectSettings/GraphicsSettings.asset", reloadText);
            AssetDatabase.Refresh();
            KeepAllAndOpen();
        }

        static void OpenJob()
        {
            var jobs = Jobs();
            int index = SessionState.GetInt(K + "Job", 0);
            if (index >= jobs.Length) { Finish(); return; }
            var path = ScenePath(jobs[index]);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Log("SKIP " + jobs[index] + ": no scene " + path);
                SessionState.SetInt(K + "Job", index + 1);
                OpenJob();
                return;
            }
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(K + "Active", true);
            SessionState.SetBool(K + "Exit", false);
            requested = idleAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= BeginWhenIdle;
            EditorApplication.update += BeginWhenIdle;
        }

        static double requested, idleAt, jobStarted;

        static void BeginWhenIdle()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - requested > 300) { EditorApplication.update -= BeginWhenIdle; Fail("the Editor did not settle"); Finish(); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { idleAt = now; return; }
            if (now - idleAt < 2) return;
            EditorApplication.update -= BeginWhenIdle;
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(K + "Active", false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                var jobs = Jobs();
                int index = SessionState.GetInt(K + "Job", 0);
                string job = index < jobs.Length ? jobs[index] : "?";
                Application.logMessageReceived -= OnLog;
                Application.logMessageReceived += OnLog;
                stack.Clear();
                stack.Push(RunJob(job));
                jobStarted = EditorApplication.timeSinceStartup;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                int index = SessionState.GetInt(K + "Job", 0) + 1;
                SessionState.SetInt(K + "Job", index);
                if (SessionState.GetBool(K + "Exit", false)) { CompleteExit(); return; }
                EditorApplication.delayCall += OpenJob;
            }
        }

        // ---- a tiny coroutine runner on EditorApplication.update -------------------------------------------------------

        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();

        static void Tick()
        {
            if (!EditorApplication.isPlaying || stack.Count == 0) return;
            if (EditorApplication.timeSinceStartup - jobStarted > 1500) { Fail("job timed out"); stack.Clear(); EndJob(); return; }
            try
            {
                while (stack.Count > 0)
                {
                    var top = stack.Peek();
                    if (!top.MoveNext()) { stack.Pop(); continue; }
                    if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                    break;
                }
            }
            catch (Exception e)
            {
                Fail("exception: " + e);
                stack.Clear();
            }
            if (stack.Count == 0) EndJob();
        }

        static IEnumerator Wait(double seconds)
        {
            double end = EditorApplication.timeSinceStartup + seconds;
            while (EditorApplication.timeSinceStartup < end) yield return null;
        }

        static IEnumerator Ticks(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static void EndJob()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            RestoreAssets();
            EditorApplication.isPlaying = false;
        }

        static void Finish()
        {
            var was = (BatchRendererGroupStrippingMode)SessionState.GetInt(K + "BrgWas", (int)EditorGraphicsSettings.batchRendererGroupShaderStrippingMode);
            if (EditorGraphicsSettings.batchRendererGroupShaderStrippingMode != was) SetBrg(was);
            RestoreAssets();
            int failures = SessionState.GetInt(K + "Failures", 0);
            Log("BRG variants put back to " + EditorGraphicsSettings.batchRendererGroupShaderStrippingMode);
            Log(failures == 0 ? "RESULT PASS (probe ran; see the numbers)" : "RESULT FAIL (" + failures + ")");
            SessionState.SetBool(K + "Active", false);
            SessionState.SetBool(K + "Exit", true);
            if (!EditorApplication.isPlaying) CompleteExit();
        }

        // EditorGraphicsSettings.batchRendererGroupShaderStrippingMode is read-only in 6000.3: the Graphics settings'
        // serialized m_BrgStripping (0 keep if Entities Graphics, 1 strip all, 2 keep all) is what the window changes.
        static void SetBrg(BatchRendererGroupStrippingMode mode)
        {
            Object settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
            if (settings == null) settings = Unsupported.GetSerializedAssetInterfaceSingleton("GraphicsSettings");
            if (settings == null) { Fail("GraphicsSettings not found"); return; }
            var so = new SerializedObject(settings);
            var p = so.FindProperty("m_BrgStripping");
            if (p == null) { Fail("m_BrgStripping not found"); return; }
            p.intValue = (int)mode;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CompleteExit()
        {
            if (!SessionState.GetBool(K + "Exit", false)) return;
            SessionState.SetBool(K + "Exit", false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(K + "Failures", 0) == 0 ? 0 : 1);
        }

        // ---- the jobs ----------------------------------------------------------------------------------------------

        static IEnumerator RunJob(string job)
        {
            Log("== job " + job + " (" + SceneManagerActive() + ")");
            yield return Wait(4);
            if (QualitySettings.GetQualityLevel() != QualitySettings.names.Length - 1)
            {
                Log("quality level was " + QualitySettings.names[QualitySettings.GetQualityLevel()] + " (the player's preset): set to PC for this run only, no PlayerPrefs written");
                QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
                yield return Ticks(5);
            }
            Log("pipeline in use: " + PipelineName() + ", quality " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
            StartRecorders();
            try
            {
                switch (job)
                {
                    case "library": yield return LibraryJob(); break;
                    case "courses": yield return CoursesJob(); break;
                    case "minigames": yield return MiniGamesJob(); break;
                    case "sections": yield return SceneShots(job, 6, true); break;
                    default: yield return SceneShots(job, 8, false); break;
                }
            }
            finally
            {
                StopRecorders();
                if (probe != null) Object.Destroy(probe.gameObject);
                probe = null;
            }
            Log("== job " + job + " done");
        }

        static string SceneManagerActive() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

        // The Royal Library (course prefab test key 5): the three views of ROYAL_LIBRARY_PERF section 4.
        static IEnumerator LibraryJob()
        {
            var test = Object.FindFirstObjectByType<CoursePrefabTest>();
            if (test == null) { Fail("no CoursePrefabTest"); yield break; }
            int library = CourseIndex(test, "RoyalLibrary");
            if (library < 0) { Fail("the Royal Library is not in the course list"); yield break; }
            test.Select(library);
            Log("course " + (library + 1) + " selected: " + (test.Current != null ? test.Current.name : "-"));
            yield return Wait(12);
            var s = test.Current;
            if (s == null) { Fail("library not built"); yield break; }
            Probe();
            var views = LibraryViews(s.transform);
            bool baseline = Mode == "baseline";
            var variants = baseline ? new[] { Variant.Off } : new[] { Variant.Off, Variant.On, Variant.Occl };
            // pictures first (one per view and variant), then the timed rounds in turns
            var pictures = new Dictionary<string, Color32[]>();
            foreach (var view in views)
                foreach (var v in variants)
                {
                    yield return Use(v);
                    Place(view.Value);
                    yield return Ticks(3);
                    // the first frame after the camera jumps here (occlusion has the last view's depth), then steady
                    Picture("library", view.Key + " - first frame after a jump", v, pictures);
                    for (int k = 0; k < 10; k++) { Kick(); yield return null; }
                    Picture("library", view.Key, v, pictures);
                }
            foreach (var view in views)
                foreach (var v in variants)
                    yield return Counts("library", view.Key, view.Value, v);
            for (int round = 1; round <= Rounds; round++)
                foreach (var view in views)
                    foreach (var v in (round % 2 == 0 ? variants.Reverse().ToArray() : variants))
                        yield return Measure("library", view.Key, view.Value, v, round, 8, Frames);
            if (!baseline)
            {
                // late pop-in: a fast turn at the door between the gallery and the chess room, then a run through it
                Pose Door(int k, int n) { float yaw = 360f * k / n; var at = s.transform.TransformPoint(new Vector3(0f, 4f, -14f)); return new Pose(at, s.transform.rotation * Quaternion.Euler(0f, yaw, 0f)); }
                Pose Run(int k, int n) { float z = -40f + 60f * k / n; var at = s.transform.TransformPoint(new Vector3(0f, 4f, z)); return new Pose(at, s.transform.rotation); }
                yield return PopIn("library", "door turn 360deg/1.5s", Door, 90, false);
                yield return PopIn("library", "run through the door 60 m/1.5s", Run, 90, false);
                // the same with the camera drawn by the player loop (enabled, a target texture), as the game draws it
                yield return PopIn("library", "LIVE door turn 360deg/1.5s", Door, 90, true);
                yield return PopIn("library", "LIVE run through the door 60 m/1.5s", Run, 90, true);
            }
            yield return Use(Variant.Off);
        }

        static Dictionary<string, Pose> LibraryViews(Transform section)
        {
            Pose View(Vector3 at, Vector3 look)
            {
                var a = section.TransformPoint(at);
                var b = section.TransformPoint(look);
                return new Pose(a, Quaternion.LookRotation(b - a, Vector3.up));
            }
            return new Dictionary<string, Pose>
            {
                ["chess room centre"] = View(new Vector3(0f, 7f, 28f), new Vector3(0f, 3f, 62f)),
                ["gallery entrance"] = View(new Vector3(0f, 5f, -132f), new Vector3(0f, 3f, -95f)),
                ["from above"] = View(new Vector3(0f, 50f, -8f), new Vector3(0f, 0f, 70f)),
            };
        }

        static int CourseIndex(CoursePrefabTest test, string contains)
        {
            var list = Entries(test);
            for (int i = 0; i < list.Count; i++)
            {
                var prefab = list[i].GetType().GetField("prefab").GetValue(list[i]) as GameObject;
                if (prefab != null && prefab.name.Contains(contains)) return i;
            }
            return -1;
        }

        static IList Entries(CoursePrefabTest test) =>
            typeof(CoursePrefabTest).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(test) as IList;

        static IEnumerator CoursesJob()
        {
            var test = Object.FindFirstObjectByType<CoursePrefabTest>();
            if (test == null) { Fail("no CoursePrefabTest"); yield break; }
            var list = Entries(test);
            for (int i = 0; i < list.Count && i < 10; i++)
            {
                var prefab = list[i].GetType().GetField("prefab").GetValue(list[i]) as GameObject;
                test.Select(i);
                string name = "course " + (i + 1) + " " + (prefab != null ? prefab.name : "?");
                Log(name);
                yield return Wait(prefab != null && prefab.name.Contains("RoyalLibrary") ? 12 : 8);
                yield return PlayerShots("courses", name);
            }
        }

        static IEnumerator MiniGamesJob()
        {
            var test = Object.FindFirstObjectByType<MiniGamePrefabTest>();
            if (test == null) { Fail("no MiniGamePrefabTest"); yield break; }
            var prefabs = typeof(MiniGamePrefabTest).GetField("prefabs", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(test) as GameObject[];
            int count = prefabs != null ? prefabs.Length : 0;
            for (int i = 0; i < count && i < 10; i++)
            {
                test.Select(i);
                string name = "mini-game " + ((i + 1) % 10) + " " + (prefabs[i] != null ? prefabs[i].name : "?");
                Log(name);
                yield return Wait(7);
                yield return PlayerShots("minigames", name);
            }
        }

        static IEnumerator SceneShots(string job, double settle, bool high)
        {
            yield return Wait(settle);
            yield return PlayerShots(job, job, high);
        }

        // The game's own camera where it is, and (for the courses) a view 12 m higher looking down the way on.
        static IEnumerator PlayerShots(string job, string name, bool high = true)
        {
            var main = MainCamera();
            if (main == null) { Log("SKIP " + name + ": no camera"); yield break; }
            Probe(main);
            var eye = new Pose(main.transform.position, main.transform.rotation);
            var shots = new List<KeyValuePair<string, Pose>> { new KeyValuePair<string, Pose>(name + " | player view", eye) };
            if (high)
            {
                var fwd = Vector3.ProjectOnPlane(main.transform.forward, Vector3.up).normalized;
                if (fwd.sqrMagnitude < .5f) fwd = Vector3.forward;
                var at = eye.position + Vector3.up * 12f - fwd * 6f;
                shots.Add(new KeyValuePair<string, Pose>(name + " | high view", new Pose(at, Quaternion.LookRotation(eye.position + fwd * 30f - at, Vector3.up))));
            }
            var pictures = new Dictionary<string, Color32[]>();
            foreach (var shot in shots)
                foreach (var v in new[] { Variant.Off, Variant.On, Variant.Occl })
                {
                    yield return Use(v);
                    Place(shot.Value);
                    for (int k = 0; k < 10; k++) { Kick(); yield return null; }
                    Picture(job, shot.Key, v, pictures);
                    yield return Counts(job, shot.Key, shot.Value, v);
                    yield return Measure(job, shot.Key, shot.Value, v, 1, 4, 12);
                }
            yield return Use(Variant.Off);
        }

        static Camera MainCamera()
        {
            var main = Camera.main;
            if (main != null && main.isActiveAndEnabled) return main;
            Camera best = null;
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c.isActiveAndEnabled && c.targetTexture == null && (best == null || c.depth > best.depth)) best = c;
            return best;
        }

        // ---- variants ------------------------------------------------------------------------------------------------

        static Variant applied = Variant.Off;
        static readonly List<UniversalRenderPipelineAsset> touched = new List<UniversalRenderPipelineAsset>();

        static UniversalRenderPipelineAsset ProjectAsset() =>
            AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");

        static string PipelineName()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            return rp != null ? rp.name : "(none)";
        }

        static IEnumerator Use(Variant v)
        {
            if (v == Variant.Off2) v = Variant.Off;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) { Fail("no URP asset in use"); yield break; }
            var mode = v == Variant.Off ? GPUResidentDrawerMode.Disabled : GPUResidentDrawerMode.InstancedDrawing;
            bool occl = v == Variant.Occl;
            if (urp.gpuResidentDrawerMode == mode && urp.gpuResidentDrawerEnableOcclusionCullingInCameras == occl && applied == v) yield break;
            if (!touched.Contains(urp)) touched.Add(urp);
            urp.gpuResidentDrawerMode = mode;
            urp.gpuResidentDrawerEnableOcclusionCullingInCameras = occl;
            applied = v;
            // In batch mode nothing draws by itself, and the pipeline (GRD with it) is made again only at the next camera
            // render: draw the probe until GRD is in the state asked for, then a few frames more for its renderer lists.
            bool want = v != Variant.Off;
            int kicks = 0;
            while (GrdEnabled() != want && kicks < 40) { Kick(); kicks++; yield return null; }
            for (int i = 0; i < 10; i++) { Kick(); yield return null; }
            DisableStats();
            bool on = GrdEnabled();
            if (on != want) Fail("GRD " + (on ? "on" : "off") + " after asking for " + v + " on " + urp.name + " (" + kicks + " renders)");
        }

        static void Kick()
        {
            if (probe == null || pictureTarget == null) return;
            var was = probe.targetTexture;
            probe.targetTexture = pictureTarget;
            probe.Render();
            probe.targetTexture = was;
        }

        static void RestoreAssets()
        {
            foreach (var urp in touched)
            {
                if (urp == null) continue;
                urp.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
                urp.gpuResidentDrawerEnableOcclusionCullingInCameras = false;
            }
            touched.Clear();
            applied = Variant.Off;
            var project = ProjectAsset();
            if (project != null && (project.gpuResidentDrawerMode != GPUResidentDrawerMode.Disabled || project.gpuResidentDrawerEnableOcclusionCullingInCameras))
            {
                project.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
                project.gpuResidentDrawerEnableOcclusionCullingInCameras = false;
            }
        }

        // ---- GRD internals (reflection: the API is internal) -----------------------------------------------------------

        static readonly MethodInfo isEnabled = typeof(GPUResidentDrawer).GetMethod("IsEnabled", BindingFlags.NonPublic | BindingFlags.Static);
        static readonly MethodInfo getStats = typeof(GPUResidentDrawer).GetMethod("GetDebugStats", BindingFlags.NonPublic | BindingFlags.Static);

        static bool GrdEnabled() => isEnabled != null && (bool)isEnabled.Invoke(null, null);

        static object Stats() => getStats?.Invoke(null, null);

        static void EnableStats()
        {
            var s = Stats();
            s?.GetType().GetField("enabled")?.SetValue(s, true);
        }

        // The Rendering Debugger's GRD counters cost time (job syncs, read-backs): off in every timed block.
        static void DisableStats()
        {
            var s = Stats();
            s?.GetType().GetField("enabled")?.SetValue(s, false);
        }

        struct GrdNumbers { public int camInstances, camGpu, camCommands, lightInstances, lightCommands; }

        static bool statsBroken;

        static GrdNumbers ReadStats(int cameraId)
        {
            if (statsBroken) return new GrdNumbers();
            try { return ReadStatsUnsafe(cameraId); }
            catch (Exception e) { statsBroken = true; Log("GRD debug stats unreadable: " + e.GetType().Name + " " + e.Message); return new GrdNumbers(); }
        }

        static GrdNumbers ReadStatsUnsafe(int cameraId)
        {
            var n = new GrdNumbers();
            var s = Stats();
            if (s == null) return n;
            var list = s.GetType().GetField("instanceCullerStats")?.GetValue(s);
            if (list == null) return n;
            var lt = list.GetType();
            int length = (int)lt.GetProperty("Length").GetValue(list);
            var item = lt.GetProperty("Item");
            for (int i = 0; i < length; i++)
            {
                var v = item.GetValue(list, new object[] { i });
                var vt = v.GetType();
                int type = Convert.ToInt32(vt.GetField("viewType").GetValue(v));
                int id = (int)vt.GetField("viewInstanceID").GetValue(v);
                int inst = (int)vt.GetField("visibleInstancesOnCPU").GetValue(v);
                int gpu = (int)vt.GetField("visibleInstancesOnGPU").GetValue(v);
                int cmds = (int)vt.GetField("drawCommands").GetValue(v);
                if (type == 1 && id == cameraId) { n.camInstances += inst; n.camGpu += gpu; n.camCommands += cmds; }
                else if (type == 2) { n.lightInstances += inst; n.lightCommands += cmds; }
            }
            return n;
        }

        // ---- the probe camera ----------------------------------------------------------------------------------------

        static Camera probe;
        static RenderTexture timingTarget, pictureTarget;
        static Texture2D sync;

        static void Probe(Camera from = null)
        {
            from = from != null ? from : MainCamera();
            if (probe == null)
            {
                var go = new GameObject("GRD probe camera");
                probe = go.AddComponent<Camera>();
            }
            if (from != null)
            {
                probe.CopyFrom(from);
                var src = from.GetComponent<UniversalAdditionalCameraData>();
                var dst = probe.GetUniversalAdditionalCameraData();
                if (src != null && dst != null)
                {
                    dst.renderPostProcessing = src.renderPostProcessing;
                    dst.antialiasing = src.antialiasing;
                    dst.antialiasingQuality = src.antialiasingQuality;
                    dst.stopNaN = src.stopNaN;
                    dst.dithering = src.dithering;
                    dst.renderShadows = src.renderShadows;
                    dst.volumeLayerMask = src.volumeLayerMask;
                    dst.requiresColorOption = src.requiresColorOption;
                    dst.requiresDepthOption = src.requiresDepthOption;
                }
            }
            probe.enabled = false;
            probe.targetTexture = null;
            if (timingTarget == null) timingTarget = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGBHalf) { name = "GRD probe 1080p" };
            if (pictureTarget == null) pictureTarget = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGBHalf) { name = "GRD probe picture" };
            if (sync == null) sync = new Texture2D(1, 1, TextureFormat.RGBAHalf, false, true);
        }

        static void Place(Pose pose) => probe.transform.SetPositionAndRotation(pose.position, pose.rotation);

        static void WaitForGpu(RenderTexture rt)
        {
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            sync.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
            RenderTexture.active = was;
        }

        // ---- timing --------------------------------------------------------------------------------------------------

        // The game's own frame on the main thread (scripts, physics, GRD's PostLateUpdate upkeep) - the probe's renders
        // run from EditorApplication.update, outside it.
        static ProfilerRecorder rLoop;

        static void StartRecorders()
        {
            rLoop = new ProfilerRecorder("PlayerLoop");
            rLoop.Start();
        }

        static void StopRecorders() => rLoop.Dispose();

        static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        static readonly Dictionary<string, List<double[]>> summary = new Dictionary<string, List<double[]>>();

        static IEnumerator Measure(string job, string shot, Pose pose, Variant v, int round, int warm, int frames)
        {
            yield return Use(v);
            Place(pose);
            probe.targetTexture = timingTarget;
            for (int i = 0; i < warm; i++)
            {
                probe.Render();
                WaitForGpu(timingTarget);
                yield return null;
            }
            var rows = new StringBuilder();
            var key = job + "|" + shot + "|" + Name(v);
            if (!summary.TryGetValue(key, out var list)) summary[key] = list = new List<double[]>();
            for (int i = 0; i < frames; i++)
            {
                // the game loop of the frame that just ended (one probe render beside it)
                double loop = rLoop.LastValue / 1e6;
                if (i == 0) DisableStats();
                long t0 = Stopwatch.GetTimestamp();
                probe.Render();
                long t1 = Stopwatch.GetTimestamp();
                WaitForGpu(timingTarget);
                long t2 = Stopwatch.GetTimestamp();
                bool on = GrdEnabled();
                double cpu = Ms(t1 - t0), total = Ms(t2 - t0);
                list.Add(new[] { cpu, total, loop, on ? 1 : 0 });
                rows.Append(Csv(job)).Append(',').Append(Csv(shot)).Append(',').Append(Name(v)).Append(',').Append(round).Append(',').Append(i).Append(',')
                    .Append(F(cpu)).Append(',').Append(F(total)).Append(',').Append(F(loop)).Append(',')
                    .Append(on ? 1 : 0).Append(',').Append(Csv(PipelineName())).Append('\n');
                yield return null;
            }
            probe.targetTexture = null;
            File.AppendAllText(Path.Combine(Out, "timings.csv"), rows.ToString());
        }

        // GRD's own counters (the Rendering Debugger's GPU Resident Drawer page), read in a short block of their own:
        // collecting them costs time, so never in a timed block. Unity's Batches / SetPass / Draw Calls / Triangles
        // counters stay 0 in batch mode even with the profiler recording (R719) - the Game view's Stats is where to see them.
        static IEnumerator Counts(string job, string shot, Pose pose, Variant v)
        {
            yield return Use(v);
            Place(pose);
            var rows = new List<long[]>();
            probe.targetTexture = timingTarget;
            for (int i = 0; i < 6; i++)
            {
                EnableStats();
                probe.Render();
                WaitForGpu(timingTarget);
                yield return null;
                var g = ReadStats(probe.GetInstanceID());
                rows.Add(new long[] { g.camInstances, g.camGpu, g.camCommands, g.lightInstances, g.lightCommands });
            }
            probe.targetTexture = null;
            DisableStats();
            var keep = rows.Skip(2).ToList();
            long Med(int c) { var a = keep.Select(r => r[c]).OrderBy(x => x).ToArray(); return a.Length == 0 ? 0 : a[a.Length / 2]; }
            var csv = Path.Combine(Out, "counts.csv");
            if (!File.Exists(csv)) File.WriteAllText(csv, "job,shot,variant,grd_cam_instances_cpu,grd_cam_instances_after_occlusion,grd_cam_drawcmds,grd_shadow_instances,grd_shadow_drawcmds\n");
            var sb = new StringBuilder(Csv(job) + "," + Csv(shot) + "," + Name(v));
            for (int c = 0; c < 5; c++) sb.Append(',').Append(Med(c));
            File.AppendAllText(csv, sb.Append('\n').ToString());
        }

        static string Name(Variant v) => v == Variant.Off ? "off" : v == Variant.On ? "grd" : v == Variant.Occl ? "grd+occlusion" : "off-again";

        static string F(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);

        static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "'") + "\"";

        static void WriteSummary()
        {
            if (summary.Count == 0) return;
            var sb = new StringBuilder();
            sb.Append("\nsummary (medians over all timed frames; GRD's own counters are in counts.csv, read in blocks of their own)\n");
            sb.Append("job | shot | variant | n | cpu ms (Camera.Render) | total ms (+ GPU done) | playerloop ms | GRD on\n");
            foreach (var pair in summary)
            {
                var rows = pair.Value;
                double Med(int c) { var a = rows.Select(r => r[c]).OrderBy(x => x).ToArray(); return a.Length == 0 ? 0 : a[a.Length / 2]; }
                sb.Append(pair.Key.Replace("|", " | ")).Append(" | ").Append(rows.Count)
                  .Append(" | ").Append(F(Med(0))).Append(" | ").Append(F(Med(1))).Append(" | ").Append(F(Med(2)))
                  .Append(" | ").Append(F(Med(3))).Append('\n');
            }
            File.AppendAllText(Path.Combine(Out, "report.txt"), sb.ToString());
            summary.Clear();
        }

        // ---- pictures ------------------------------------------------------------------------------------------------

        static void Picture(string job, string shot, Variant v, Dictionary<string, Color32[]> made)
        {
            probe.targetTexture = pictureTarget;
            probe.Render();
            probe.targetTexture = null;
            var pixels = Read(pictureTarget);
            int magenta = 0;
            foreach (var c in pixels) if (c.r > 200 && c.g < 70 && c.b > 200) magenta++;
            string file = Safe(job + "_" + shot + "_" + Name(v)) + ".png";
            Save(pixels, pictureTarget.width, pictureTarget.height, Path.Combine(Out, "shots", file));
            string diff16 = "", diff48 = "", mean = "";
            if (v != Variant.Off && made.TryGetValue(shot + "|off", out var off))
            {
                Compare(off, pixels, out double d16, out double d48, out double m, out var heat);
                diff16 = F(d16); diff48 = F(d48); mean = F(m);
                if (d16 > .5) Save(heat, pictureTarget.width, pictureTarget.height, Path.Combine(Out, "shots", Safe(job + "_" + shot + "_" + Name(v)) + "_DIFF.png"));
            }
            made[shot + "|" + (v == Variant.Off ? "off" : Name(v))] = pixels;
            File.AppendAllText(Path.Combine(Out, "shots.csv"), Csv(job) + "," + Csv(shot) + "," + Name(v) + "," + magenta + "," + diff16 + "," + diff48 + "," + mean + "," + Csv(file) + "," + (GrdEnabled() ? 1 : 0) + "\n");
            if (magenta > 200) Log("WARN " + shot + " " + Name(v) + ": " + magenta + " magenta pixels");
        }

        static void Compare(Color32[] a, Color32[] b, out double pct16, out double pct48, out double mean, out Color32[] heat)
        {
            int n16 = 0, n48 = 0; long sum = 0;
            heat = new Color32[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                int d = Math.Max(Math.Abs(a[i].r - b[i].r), Math.Max(Math.Abs(a[i].g - b[i].g), Math.Abs(a[i].b - b[i].b)));
                sum += d;
                if (d > 16) n16++;
                if (d > 48) n48++;
                byte g = (byte)(b[i].r / 4 + b[i].g / 4 + b[i].b / 4);
                heat[i] = d > 48 ? new Color32(255, 0, 0, 255) : d > 16 ? new Color32(255, 200, 0, 255) : new Color32(g, g, g, 255);
            }
            pct16 = 100.0 * n16 / a.Length;
            pct48 = 100.0 * n48 / a.Length;
            mean = (double)sum / a.Length;
        }

        static Color32[] Read(RenderTexture rt)
        {
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            var linear = new Texture2D(rt.width, rt.height, TextureFormat.RGBAHalf, false, true);
            linear.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = was;
            var src = linear.GetPixels();
            Object.Destroy(linear);
            var pixels = new Color32[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var c = src[i];
                pixels[i] = new Color32(Encode(c.r), Encode(c.g), Encode(c.b), 255);
            }
            return pixels;
        }

        static byte Encode(float linear) => (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.LinearToGammaSpace(Mathf.Max(0f, linear))) * 255f);

        static void Save(Color32[] pixels, int w, int h, string path)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        static string Safe(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString().Replace("__", "_");
        }

        // ---- late pop-in -----------------------------------------------------------------------------------------------

        static IEnumerator PopIn(string job, string name, Func<int, int, Pose> poseAt, int frames, bool live)
        {
            const int W = 320, H = 180;
            var target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGBHalf) { name = "GRD probe pop-in" };
            target.Create();
            var seen = new Dictionary<Variant, List<Color32[]>>();
            foreach (var v in new[] { Variant.Off, Variant.Occl, Variant.On, Variant.Off2 })
            {
                yield return Use(v);
                var frame = new List<Color32[]>();
                probe.targetTexture = target;
                if (live)
                {
                    // enabled: the player loop draws it once a frame, as the game's camera is drawn
                    Place(poseAt(0, frames));
                    probe.enabled = true;
                    yield return null;
                    yield return null;
                    for (int k = 0; k < frames; k++)
                    {
                        Place(poseAt(k, frames));
                        yield return null;
                        frame.Add(Read(target));
                    }
                    probe.enabled = false;
                }
                else
                {
                    for (int k = 0; k < frames; k++)
                    {
                        Place(poseAt(k, frames));
                        probe.Render();
                        frame.Add(Read(target));
                        yield return null;
                    }
                }
                probe.targetTexture = null;
                seen[v] = frame;
            }
            if (live)
            {
                // did the loop draw it at all? the first and the last picture of a 60 m run or a full turn differ
                Compare(seen[Variant.Off][0], seen[Variant.Off][frames / 2], out _, out double moved, out _, out _);
                Log("live drawing check (" + name + "): first vs middle picture of 'off' differ by " + F(moved) + "% of pixels"
                    + (moved < 1 ? " -> the player loop did NOT draw the camera in batch mode; this run says nothing" : ""));
            }
            var sb = new StringBuilder();
            double worstOccl = 0, worstOn = 0, worstNoise = 0; int worstK = 0;
            int popOccl = 0, popOn = 0;
            for (int k = 0; k < frames; k++)
            {
                Compare(seen[Variant.Off][k], seen[Variant.Occl][k], out _, out double occl, out _, out _);
                Compare(seen[Variant.Off][k], seen[Variant.On][k], out _, out double on, out _, out _);
                Compare(seen[Variant.Off][k], seen[Variant.Off2][k], out _, out double noise, out _, out _);
                if (occl > noise + 1.0) popOccl++;
                if (on > noise + 1.0) popOn++;
                if (occl - noise > worstOccl - worstNoise) { worstOccl = occl; worstNoise = noise; worstK = k; }
                worstOn = Math.Max(worstOn, on);
                sb.Append(Csv(job)).Append(',').Append(Csv(name)).Append(',').Append(k).Append(',').Append(F(noise)).Append(',').Append(F(on)).Append(',').Append(F(occl)).Append('\n');
            }
            var csv = Path.Combine(Out, "popin.csv");
            if (!File.Exists(csv)) File.WriteAllText(csv, "job,run,frame,off_vs_off_again_pct48,off_vs_grd_pct48,off_vs_grd_occlusion_pct48\n");
            File.AppendAllText(csv, sb.ToString());
            foreach (var v in new[] { Variant.Off, Variant.Occl, Variant.Off2 })
                Save(seen[v][worstK], W, H, Path.Combine(Out, "shots", Safe(job + "_popin_" + name + "_frame" + worstK + "_" + Name(v)) + ".png"));
            Log("pop-in " + name + ": frames where GRD+occlusion differs from off by >1% more than off differs from itself: " + popOccl + "/" + frames
                + " (GRD alone " + popOn + "/" + frames + "); worst frame " + worstK + " occlusion " + F(worstOccl) + "% vs noise " + F(worstNoise) + "%");
            Object.Destroy(target);
        }

        // ---- logging -------------------------------------------------------------------------------------------------

        static void OnLog(string condition, string stack, LogType type)
        {
            bool grd = condition.IndexOf("Resident", StringComparison.OrdinalIgnoreCase) >= 0
                || condition.IndexOf("BatchRendererGroup", StringComparison.OrdinalIgnoreCase) >= 0
                || condition.IndexOf("DOTS", StringComparison.Ordinal) >= 0
                || condition.IndexOf("BRG", StringComparison.Ordinal) >= 0
                || condition.IndexOf("instancing", StringComparison.OrdinalIgnoreCase) >= 0
                || condition.IndexOf("occlusion", StringComparison.OrdinalIgnoreCase) >= 0;
            if (type == LogType.Log && !grd) return;
            File.AppendAllText(Path.Combine(Out, "console.txt"), "[" + Name(applied) + "] " + type + (grd ? " GRD-RELATED" : "") + ": " + condition.Replace("\n", " / ") + "\n");
        }

        static void Log(string value)
        {
            Directory.CreateDirectory(Out);
            File.AppendAllText(Path.Combine(Out, "report.txt"), value + "\n");
            Debug.Log("[GrdProbe] " + value);
            if (value.StartsWith("== job ") && value.EndsWith(" done")) WriteSummary();
        }

        static void Fail(string value)
        {
            SessionState.SetInt(K + "Failures", SessionState.GetInt(K + "Failures", 0) + 1);
            Log("FAIL " + value);
        }
    }
}
