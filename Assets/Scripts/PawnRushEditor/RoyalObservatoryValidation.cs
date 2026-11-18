using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush.Editor
{
    // The Royal Observatory's graybox checked in Play Mode, in the course prefab test scene (the way a person opens
    // it). R712 for the first build; R720 for the second, on structure v0.3 with the eight new obstacles: the draft is
    // listed there, it builds, the section check passes (the route's gaps, floor under every checkpoint, what may be
    // climbed, the kill volume, entry and exit), its devices are all there and move as the builder set them (the
    // variable stars' petals, the comet, the moon, the planets, the binary star, the irises' leaves, the focus beams'
    // spots, the meteors and their rule), and pictures from above and along the way.
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

        // What is watched while the course runs: each device's least and greatest value, how far round each turning
        // thing has gone, where each sliding thing started, the most meteors showing at once.
        static readonly Dictionary<string, Vector2> ranges = new Dictionary<string, Vector2>();
        static readonly Dictionary<string, float> turned = new Dictionary<string, float>(), lastHeading = new Dictionary<string, float>();
        static readonly Dictionary<string, Vector3> firstPlace = new Dictionary<string, Vector3>(), farPlace = new Dictionary<string, Vector3>();
        // How many meteors each shower had landed when the watch began (they fall from the section's start).
        static readonly Dictionary<MeteorShower, int> landedBefore = new Dictionary<MeteorShower, int>();
        static int showing;
        static double watchedFrom;

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
                "Royal Observatory graybox on structure v0.3 (R720): built in the course prefab test scene in Play Mode. Not a person playing, not online.\n");
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
                errors.Clear(); ranges.Clear(); turned.Clear(); lastHeading.Clear(); firstPlace.Clear(); farPlace.Clear(); landedBefore.Clear();
                failures = 0; stage = 0; section = null; showing = 0; watchedFrom = double.NaN;
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

        // The turning things: the root that turns and the child that is watched going round it.
        static readonly (string root, string child, float degreesPerSecond)[] Turning =
        {
            ("Device_EclipseMoon", "Moon", 30f), ("Device_Planet 1", "Planet body", 40f), ("Device_Planet 2", "Planet body", -72f),
            ("Device_Planet 3", "Planet body", 360f / 5.5f), ("Device_Planet 4", "Planet body", -36f), ("Device_BinaryStar", "Binary star A", -45f),
        };

        static void Built()
        {
            var test = Object.FindFirstObjectByType<CoursePrefabTest>();
            section = test != null ? test.Current : null;
            if (!Check(section != null && section.Id == RoyalObservatorySection.Id, "built: " + (section != null ? section.Id + " · " + section.DisplayName + ", slot " + (section.Slot + 1) : "nothing"))) return;
            Check(section.Kind == PawnRushSection.SlotKind.Obstacle && section.Slot == 4, "an obstacle section in the late slot (5)");
            Check((section.ExitLocal.position - RoyalObservatoryBuilder.Exit).magnitude < .01f && section.EntryLocal.position == Vector3.zero,
                  "entry " + section.EntryLocal.position + ", exit " + section.ExitLocal.position);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PawnRushSections/" + RoyalObservatorySection.Id + ".prefab");
            var saved = prefab != null ? prefab.GetComponent<PawnRushSection>() : null;
            Check(saved != null && (saved.ExitLocal.position - RoyalObservatoryBuilder.Exit).magnitude < .01f,
                  "the prefab's own exit (read before it is built) is the same: " + (saved != null ? saved.ExitLocal.position.ToString() : "-"));
            Check(section.Beats.Length == RoyalObservatoryBuilder.BeatBoxes, "beat boxes: " + section.Beats.Length);

            var pawn = MiniGameBase.LocalDriver?.FollowTarget;
            Check(pawn != null && Vector3.Distance(Local(pawn.position), Vector3.zero) < 12f, "the pawn stands at the entry: " + (pawn != null ? Local(pawn.position).ToString("0.0") : "-"));

            var notes = new List<string>();
            var found = SectionValidator.Run(section, notes);
            foreach (var n in notes) Log("  note " + n);
            foreach (var e in found) Log("  section check: " + e);
            Check(found.Count == 0, "the section check: " + found.Count + " errors");

            int checkpoints = section.GetComponentsInChildren<CourseCheckpoint>(true).Length;
            int petals = section.GetComponentsInChildren<StarPetal>(true).Length;
            int showers = section.GetComponentsInChildren<MeteorShower>(true).Length;
            int comets = section.GetComponentsInChildren<CometFerry>(true).Length;
            int leaves = section.GetComponentsInChildren<IrisLeaf>(true).Length;
            int beams = section.GetComponentsInChildren<FocusBeam>(true).Length;
            Check(checkpoints == RoyalObservatoryBuilder.Checkpoints, "checkpoints: " + checkpoints);
            Check(petals == 15 && showers == 2 && comets == 1 && leaves == 6 && beams == 2,
                  $"devices: {petals} petals (three variable stars), {showers} meteor showers, {comets} comet, {leaves} iris leaves (three gates), {beams} focus beams");
            int turning = 0;
            foreach (var t in Turning)
            {
                var root = Named(t.root);
                if (root != null && root.Find(t.child) != null && root.GetComponent<ChessFight.ProtectKing.ObstacleMotion>() != null) turning++;
            }
            Check(turning == Turning.Length, $"the moon, the four planets and the binary star are there and turn ({turning} of {Turning.Length})");
            Check(Named("Planet ring") != null && Named("Great globe glass") != null && Named("Globe climb") != null && Named("Telescope deck") != null
                  && Named("Observing stage") != null && Named("Balcony") != null, "the globe, its climb, the fourth planet's ring, the stage, the deck and the balcony are there");
            Check(section.GetComponentsInChildren<AirVent>(true).Length == 0 && section.GetComponentsInChildren<PendulumSwing>(true).Length == 0,
                  "none of the first build's imported hazards are left");

            // The meteors' rule, over ten minutes of them: never the same line twice running, never two closer in
            // time than half the interval, every one clear of its line's ends.
            foreach (var shower in section.GetComponentsInChildren<MeteorShower>(true))
            {
                int count = (int)(600f / shower.Interval), repeats = 0, close = 0, off = 0;
                var lines = new int[shower.LineCount];
                var before = shower.At(0);
                lines[before.Line]++;
                for (int n = 1; n < count; n++)
                {
                    var m = shower.At(n);
                    lines[m.Line]++;
                    if (m.Line == before.Line) repeats++;
                    if (m.Time - before.Time < shower.Interval * .5 - 1e-6) close++;
                    if (m.Along < 0f || m.Along > 1f) off++;
                    before = m;
                }
                Check(repeats == 0 && close == 0 && off == 0 && System.Array.TrueForAll(lines, c => c > count / (lines.Length * 2)),
                      $"{shower.name}: {count} meteors on {lines.Length} lines ({string.Join(" / ", lines)}), {repeats} twice running on a line, {close} too close in time");
            }
            foreach (var shower in section.GetComponentsInChildren<MeteorShower>(true)) landedBefore[shower] = shower.Landed;
            watchedFrom = ObstacleClock.Now;
        }

        static void Range(string key, float value)
        {
            ranges[key] = ranges.TryGetValue(key, out var r) ? new Vector2(Mathf.Min(r.x, value), Mathf.Max(r.y, value)) : new Vector2(value, value);
        }

        static void Sample()
        {
            if (section == null) return;
            var up = section.transform.up;
            foreach (var petal in section.GetComponentsInChildren<StarPetal>(true))
                Range("petal " + petal.name, Vector3.Angle(petal.transform.up, up));
            foreach (var leaf in section.GetComponentsInChildren<IrisLeaf>(true)) Slide("leaf " + leaf.name, leaf.transform);
            foreach (var comet in section.GetComponentsInChildren<CometFerry>(true)) Slide("comet", comet.transform);
            foreach (var beam in section.GetComponentsInChildren<FocusBeam>(true))
            {
                var spot = beam.transform.Find("Hot spot");
                if (spot == null) continue;
                Range("beam " + beam.name + " across", spot.localPosition.x);
                Range("beam " + beam.name + " along", spot.localPosition.z);
            }
            foreach (var t in Turning)
            {
                var root = Named(t.root);
                var child = root != null ? root.Find(t.child) : null;
                if (child == null) continue;
                var offset = Local(child.position) - Local(root.position);
                float heading = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
                if (lastHeading.TryGetValue(t.root, out float was))
                    turned[t.root] = (turned.TryGetValue(t.root, out float sum) ? sum : 0f) + Mathf.DeltaAngle(was, heading);
                lastHeading[t.root] = heading;
            }
            foreach (var shower in section.GetComponentsInChildren<MeteorShower>(true)) showing = Mathf.Max(showing, shower.Showing);
        }

        // How far apart the places a sliding thing has been seen at are (it slides along a straight line, so the
        // corners of the box it has been in are its two ends - wherever in its travel it was first seen).
        static void Slide(string key, Transform t)
        {
            var at = Local(t.position);
            var least = firstPlace.TryGetValue(key, out var a) ? Vector3.Min(a, at) : at;
            var most = farPlace.TryGetValue(key, out var c) ? Vector3.Max(c, at) : at;
            firstPlace[key] = least;
            farPlace[key] = most;
            Range(key, (most - least).magnitude);
        }

        static void Moved()
        {
            if (section == null) return;
            float watched = (float)(ObstacleClock.Now - watchedFrom);
            Log($"  watched {watched:0.0} s of the obstacle clock");

            int folded = 0, petals = 0;
            float least = 180f, most = 0f;
            foreach (var kv in ranges)
                if (kv.Key.StartsWith("petal "))
                {
                    petals++;
                    least = Mathf.Min(least, kv.Value.y);
                    most = Mathf.Max(most, kv.Value.x);
                    if (kv.Value.x < 1f && kv.Value.y > 80f) folded++;
                }
            Check(petals == 15 && folded == 15, $"the fifteen petals each lay level and hung folded ({folded} of {petals}; the least fold {least:0} deg, the least level {most:0.0} deg)");

            int leaves = 0, closed = 0;
            foreach (var kv in ranges)
                if (kv.Key.StartsWith("leaf "))
                {
                    leaves++;
                    if (kv.Value.y > 2.5f && kv.Value.y < 2.7f) closed++;
                    Log($"  {kv.Key}: slid {kv.Value.y:0.00} m");
                }
            Check(leaves == 6 && closed == 6, $"the six iris leaves each slid to the deck's middle and back ({closed} of {leaves} went 2.58 m)");

            Has("comet", out var comet);
            Check(comet.y > 4.7f && comet.y < 5.2f, $"the comet crossed between its docks: {comet.y:0.00} m");

            foreach (var beam in section.GetComponentsInChildren<FocusBeam>(true))
            {
                Has("beam " + beam.name + " across", out var across);
                Has("beam " + beam.name + " along", out var along);
                Check(across.x < -1.7f && across.y > 1.7f && along.x < -1.7f && along.y > 1.7f,
                      $"{beam.name}: its spot went {across.x:0.0} .. {across.y:0.0} m across the deck and {along.x:0.0} .. {along.y:0.0} m along it");
            }

            foreach (var t in Turning)
            {
                float went = turned.TryGetValue(t.root, out float sum) ? sum : 0f, expected = t.degreesPerSecond * watched;
                Check(Mathf.Abs(went - expected) < Mathf.Abs(expected) * .1f + 10f, $"{t.root} went round {went:0} deg in {watched:0.0} s ({t.degreesPerSecond:0.#} deg/s: {expected:0})");
            }

            foreach (var shower in section.GetComponentsInChildren<MeteorShower>(true))
            {
                float expected = watched / shower.Interval;
                int landed = shower.Landed - (landedBefore.TryGetValue(shower, out int before) ? before : 0);
                Check(landed >= expected - 2f && landed <= expected + 2f, $"{shower.name}: {landed} meteors landed in {watched:0.0} s (one in {shower.Interval} s: {expected:0.0})");
            }
            Check(showing >= 1, "meteors were drawn: at most " + showing + " at once on one shower");

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
            Capture("00_plan", new Vector3(-3f, 150f, 66f), new Vector3(-3f, 0f, 66f), Vector3.forward, 70f, true, 1200, 1600);
            Capture("01_from_the_tunnel", new Vector3(1f, 2.6f, 11f), new Vector3(-12f, 4f, 52f), view, 72f, false, 1600, 900);
            Capture("02_overview", new Vector3(30f, 40f, 30f), new Vector3(-4f, 5f, 68f), view, 64f, false, 1600, 900);
            Capture("03_dipper_handle", new Vector3(5f, 10f, 20f), new Vector3(-19f, 1f, 35f), view, 60f, false, 1600, 900);
            Capture("04_dipper_bowl", new Vector3(-18f, 15f, 36f), new Vector3(-33f, 2f, 55f), view, 60f, false, 1600, 900);
            Capture("05_west_gate", new Vector3(-31f, 9f, 55f), new Vector3(-8f, 7f, 64f), view, 60f, false, 1600, 900);
            Capture("06_inside_the_globe", new Vector3(-11.5f, 7.6f, 61.2f), new Vector3(6f, 10.5f, 64.6f), view, 70f, false, 1600, 900);
            Capture("07_cassiopeia", new Vector3(36f, 20f, 64f), new Vector3(21f, 11.5f, 80f), view, 60f, false, 1600, 900);
            Capture("08_stage_and_tube", new Vector3(25f, 20f, 82f), new Vector3(9f, 15f, 103f), view, 60f, false, 1600, 900);
            Capture("09_up_the_tube", new Vector3(9f, 14.4f, 94f), new Vector3(9f, 19.4f, 114f), view, 70f, false, 1600, 900);
            Capture("10_back_from_the_balcony", new Vector3(9f, 25f, 128f), new Vector3(-4f, 6f, 66f), view, 64f, false, 1600, 900);
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
