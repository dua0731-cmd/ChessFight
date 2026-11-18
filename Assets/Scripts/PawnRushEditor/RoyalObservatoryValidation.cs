using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ChessFight.ProtectKing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush.Editor
{
    // R712: the Royal Observatory's graybox checked in Play Mode, in the course prefab test scene (the way a person opens
    // it): the draft is listed there, it builds, the section check passes (the route's gaps, floor under every
    // checkpoint, what may be climbed, the kill volume, entry and exit), its devices are all there and move as the
    // builder set them (lifts, rams, winds, shields, the turntable), and pictures from above and along the way.
    // Not a person playing it: nothing here says a beat can be passed or how long the course takes.
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.RoyalObservatoryValidation.RunPlayBatch
    // (no -quit, no -nographics). Report and pictures: Logs/RoyalObservatory.
    [InitializeOnLoad]
    public static class RoyalObservatoryValidation
    {
        const string ActiveKey = "RoyalObservatory.Active", ExitKey = "RoyalObservatory.Exit", CodeKey = "RoyalObservatory.Code";
        const double Settle = 4, Watch = 13;

        static string Folder => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "RoyalObservatory");

        static readonly List<string> errors = new List<string>();
        static int failures, stage;
        static double stepAt, requested, idleAt;
        static PawnRushSection section;
        static Camera captureCamera;

        // What is watched while the course runs: each device's least and greatest value.
        static readonly Dictionary<string, Vector2> ranges = new Dictionary<string, Vector2>();
        static readonly Dictionary<AirVent, int> windStates = new Dictionary<AirVent, int>();
        static float discTurned, discLast;

        static RoyalObservatoryValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        [MenuItem("ChessFight/Pawn Rush/Validate/Royal Observatory graybox (Play)", false, 212)]
        public static void RunPlayBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "report.txt"),
                "Royal Observatory graybox (R712): built in the course prefab test scene in Play Mode. Not a person playing, not online.\n");
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

        static void BeginWhenIdle()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - requested > 180) { EditorApplication.update -= BeginWhenIdle; Log("FAIL: the Editor did not settle"); if (Application.isBatchMode) EditorApplication.Exit(1); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { idleAt = now; return; }
            if (now - idleAt < 2) return;
            EditorApplication.update -= BeginWhenIdle;
            EditorSceneManager.playModeStartScene = null;
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                errors.Clear(); ranges.Clear(); windStates.Clear();
                failures = 0; stage = 0; section = null; discTurned = 0f; discLast = float.NaN;
                stepAt = EditorApplication.timeSinceStartup;
                Application.logMessageReceived += OnLog;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) CompleteExit();
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                if (errors.Count < 40) errors.Add(type + ": " + condition);
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            try
            {
                if (stage == 0)
                {
                    if (now - stepAt < Settle) return;
                    if (!Open()) { Finish(); return; }
                    stage = 1; stepAt = now;
                }
                else if (stage == 1)
                {
                    if (now - stepAt < Settle) return;
                    Built();
                    stage = 2; stepAt = now;
                }
                else if (stage == 2)
                {
                    Sample();
                    if (now - stepAt < Watch) return;
                    Moved();
                    Pictures();
                    Finish();
                }
            }
            catch (System.Exception e)
            {
                failures++;
                Log("FAIL: the check itself threw " + e);
                Finish();
            }
        }

        // The draft is in the course prefab test's list; open it there.
        static bool Open()
        {
            var test = Object.FindFirstObjectByType<CoursePrefabTest>();
            if (!Check(test != null, "the course prefab test is in the scene")) return false;
            var entries = typeof(CoursePrefabTest).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(test) as System.Collections.IList;
            int index = -1, count = entries != null ? entries.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var prefab = entries[i].GetType().GetField("prefab")?.GetValue(entries[i]) as GameObject;
                if (prefab != null && prefab.GetComponent<RoyalObservatorySection>() != null) index = i;
            }
            if (!Check(index >= 0, "the Royal Observatory is listed in the course prefab test: key " + (index + 1) + " of " + count)) return false;
            // A draft, not a round's section: the set's lists the rounds draw from do not hold it.
            var set = AssetDatabase.LoadAssetAtPath<PawnRushSectionSet>(SectionPrefabMenu.SectionSet);
            var drawn = new List<GameObject>();
            if (set != null) set.Options(drawn);
            Check(set != null && !drawn.Exists(p => p != null && p.GetComponent<RoyalObservatorySection>() != null),
                  "no round draws it (it is in a slot's drafts only); the rounds draw from " + drawn.Count + " prefabs as before");
            test.Select(index);
            return true;
        }

        static Vector3 Local(Vector3 world) => section.transform.InverseTransformPoint(world);
        static Vector3 World(Vector3 local) => section.transform.TransformPoint(local);

        static Transform Named(string name)
        {
            foreach (var t in section.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        static void Built()
        {
            var test = Object.FindFirstObjectByType<CoursePrefabTest>();
            section = test != null ? test.Current : null;
            if (!Check(section != null && section.Id == RoyalObservatorySection.Id, "built: " + (section != null ? section.Id + " · " + section.DisplayName + ", slot " + (section.Slot + 1) : "nothing"))) return;
            Check(section.Kind == PawnRushSection.SlotKind.Obstacle && section.Slot == 4, "an obstacle section in the late slot (5)");
            Check((section.ExitLocal.position - RoyalObservatoryBuilder.Exit).magnitude < .01f && section.EntryLocal.position == Vector3.zero,
                  "entry " + section.EntryLocal.position + ", exit " + section.ExitLocal.position);
            Check(section.Beats.Length == 7, "beats: " + section.Beats.Length);

            var pawn = MiniGameBase.LocalDriver?.FollowTarget;
            Check(pawn != null && Vector3.Distance(Local(pawn.position), Vector3.zero) < 12f, "the pawn stands at the entry: " + (pawn != null ? Local(pawn.position).ToString("0.0") : "-"));

            var notes = new List<string>();
            var found = SectionValidator.Run(section, notes);
            foreach (var n in notes) Log("  note " + n);
            foreach (var e in found) Log("  section check: " + e);
            Check(found.Count == 0, "the section check: " + found.Count + " errors");

            int checkpoints = section.GetComponentsInChildren<CourseCheckpoint>(true).Length;
            int rams = section.GetComponentsInChildren<TelescopeRam>(true).Length;
            int winds = section.GetComponentsInChildren<AirVent>(true).Length;
            int weights = section.GetComponentsInChildren<PendulumSwing>(true).Length;
            int belts = 0, shields = 0;
            foreach (var t in section.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("08 Belt")) belts++;
                if (t.name.StartsWith("06 Shutter shields")) shields++;
            }
            Check(checkpoints == 8, "checkpoints: " + checkpoints);
            Check(rams == 3 && winds == 5 && weights == 1 && belts == 4 && shields == 2,
                  $"devices: {rams} rams, {winds} winds, {weights} weight, {belts} belts, {shields} shield pairs");
            Check(Named("Device_PlanetLift A") != null && Named("Device_PlanetLift B") != null && Named("Device_OrreryTurntable") != null
                  && Named("Device_GlobeArm") != null && Named("Device_SpinBar B1 ring 1") != null && Named("Device_SpinBar B1 ring 2") != null
                  && Named("Device_HighBar B1 ring 2") != null && Named("Device_SpinBar B4 low arm") != null,
                  "the lifts, the turntable, its two arms and the ring court's three bars are there");
            foreach (var vent in section.GetComponentsInChildren<AirVent>(true)) windStates[vent] = 0;
        }

        static void Range(string key, float value)
        {
            ranges[key] = ranges.TryGetValue(key, out var r) ? new Vector2(Mathf.Min(r.x, value), Mathf.Max(r.y, value)) : new Vector2(value, value);
        }

        static void Sample()
        {
            if (section == null) return;
            foreach (var name in new[] { "Device_PlanetLift A", "Device_PlanetLift B" })
            {
                var t = Named(name);
                if (t != null) Range(name, Local(t.position).y);
            }
            for (int i = 1; i <= 3; i++)
            {
                var t = Named("Device_TelescopeRam " + i);
                if (t != null) Range("ram " + i, Local(t.position).z);
            }
            foreach (var t in section.GetComponentsInChildren<Transform>(true))
                if (t.name == "Left sliding wall" || t.name == "Right sliding wall")
                    Range(t.parent.name + " / " + t.name, Local(t.position).z);
            var disc = Named("Device_OrreryTurntable");
            if (disc != null)
            {
                float yaw = disc.eulerAngles.y;
                if (!float.IsNaN(discLast)) discTurned += Mathf.DeltaAngle(discLast, yaw);
                discLast = yaw;
            }
            var keys = new List<AirVent>(windStates.Keys);
            foreach (var vent in keys)
                if (vent != null) windStates[vent] |= 1 << (int)vent.State;
        }

        static void Moved()
        {
            if (section == null) return;
            Has("Device_PlanetLift A", out var a);
            Has("Device_PlanetLift B", out var bb);
            Check(a.x < .15f && a.y > 2.9f && a.y < 3.05f, $"lift A's deck rides {a.x:0.00} .. {a.y:0.00} m (the W landing 0, the ledge 3)");
            Check(bb.x > 2.9f && bb.x < 3.1f && bb.y > 5.85f && bb.y < 6.05f, $"lift B's deck rides {bb.x:0.00} .. {bb.y:0.00} m (the ledge 3, the NW landing 6)");
            for (int i = 1; i <= 3; i++)
            {
                Has("ram " + i, out var r);
                Check(r.x < 99.9f && r.y > 106.1f, $"ram {i}'s face goes from z {r.y:0.00} to {r.x:0.00} (the bridge is z 100 .. 106)");
            }
            int moved = 0, walls = 0;
            foreach (var kv in ranges)
                if (kv.Key.Contains("sliding wall"))
                {
                    walls++;
                    if (kv.Value.y - kv.Value.x > 5.5f) moved++;
                    Log($"  {kv.Key}: z {kv.Value.x:0.00} .. {kv.Value.y:0.00}");
                }
            Check(walls == 4 && moved == 4, $"the four shield walls slide across their half ({moved} of {walls} moved 6 m)");
            Check(discTurned > 100f && discTurned < 300f, $"the turntable turned {discTurned:0} deg in {Watch:0} s (15 deg/s)");
            int all = 0;
            foreach (var kv in windStates) if (kv.Value == 7) all++;
            Check(all == windStates.Count && all == 5, $"the five winds each rested, warned and blew ({all} of {windStates.Count})");
            var pawn = MiniGameBase.LocalDriver?.FollowTarget;
            Check(pawn != null && Local(pawn.position).y > -1f, "the pawn is still on the entry floor after " + (Settle + Watch) + " s: " + (pawn != null ? Local(pawn.position).ToString("0.0") : "-"));
        }

        static bool Has(string key, out Vector2 range)
        {
            if (ranges.TryGetValue(key, out range)) return true;
            failures++;
            Log("FAIL: nothing watched for " + key);
            return false;
        }

        static void Pictures()
        {
            if (section == null) return;
            var go = new GameObject("Observatory capture camera");
            captureCamera = go.AddComponent<Camera>();
            captureCamera.enabled = false;
            captureCamera.nearClipPlane = .1f;
            captureCamera.farClipPlane = 600f;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = new Color(.06f, .07f, .11f);
            var view = Vector3.up;
            Capture("00_plan", new Vector3(25f, 120f, 66f), new Vector3(25f, 0f, 66f), Vector3.forward, 72f, true, 1200, 1600);
            Capture("01_from_the_tunnel", new Vector3(0f, 2.4f, 11f), new Vector3(30f, 7f, 92f), view, 70f, false, 1600, 900);
            Capture("02_overview", new Vector3(57f, 28f, 15f), new Vector3(20f, 0f, 78f), view, 62f, false, 1600, 900);
            Capture("03_ring_court", new Vector3(-5f, 9f, 8f), new Vector3(0f, 0f, 27f), view, 60f, false, 1600, 900);
            Capture("04_belts_and_shields", new Vector3(8f, 11f, 31f), new Vector3(30f, 0f, 44f), view, 60f, false, 1600, 900);
            Capture("05_vent_bridge", new Vector3(41f, 9f, 43f), new Vector3(50f, 0f, 62f), view, 60f, false, 1600, 900);
            Capture("06_orrery", new Vector3(42f, 11f, 94f), new Vector3(22f, 1f, 77f), view, 60f, false, 1600, 900);
            Capture("07_lifts", new Vector3(24f, 10f, 80f), new Vector3(7f, 3f, 91f), view, 60f, false, 1600, 900);
            Capture("08_telescope_bridge", new Vector3(9f, 12f, 95f), new Vector3(30f, 6f, 104f), view, 60f, false, 1600, 900);
            Capture("09_constellation_wall", new Vector3(41f, 11f, 98f), new Vector3(50f, 8.5f, 112f), view, 60f, false, 1600, 900);
            Capture("10_back_from_the_terrace", new Vector3(50f, 14f, 126f), new Vector3(20f, 0f, 70f), view, 62f, false, 1600, 900);
            Object.Destroy(go);
        }

        // One picture from `eye` toward `target` (the section's frame); orthographic with `fieldOfView` as its half
        // height when `flat`.
        static void Capture(string name, Vector3 eye, Vector3 target, Vector3 up, float fieldOfView, bool flat, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Log("NOT RUN: picture " + name + " (no graphics device)"); return; }
            captureCamera.transform.position = World(eye);
            captureCamera.transform.rotation = Quaternion.LookRotation(World(target) - World(eye), section.transform.TransformDirection(up));
            captureCamera.orthographic = flat;
            if (flat) captureCamera.orthographicSize = fieldOfView;
            else captureCamera.fieldOfView = fieldOfView;
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
                picture = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
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

        static bool Check(bool ok, string what)
        {
            if (!ok) failures++;
            Log((ok ? "PASS " : "FAIL ") + what);
            return ok;
        }

        static void Finish()
        {
            foreach (var e in errors) Log("console " + e);
            if (errors.Count > 0) failures++;
            Log(failures == 0 ? "RESULT PASS" : "RESULT FAIL (" + failures + ")");
            Application.logMessageReceived -= OnLog;
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
            Debug.Log("[RoyalObservatory] " + value);
        }

        static void CompleteExit()
        {
            if (!SessionState.GetBool(ExitKey, false)) return;
            SessionState.SetBool(ExitKey, false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(CodeKey, 1));
        }
    }
}
