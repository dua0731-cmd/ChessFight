using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ChessFight.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush.Editor
{
    // Checks for the royal clock tower's grey box (왕실 시계탑, R775), written the royal vault's way but far shorter.
    //   Edit: the prefab built by RoyalClockTowerBuilder in a scratch scene (every mover in its rest pose): the section checks
    //         (SectionValidator), what is there (movers, ropes, combs, checkpoints), and which faces can be held - a ray at each
    //         face the design calls a wall to climb or a wall that must not be.
    //   Play: the tower alone in Play Mode for a quarter of a minute: every weight, block, rope and the drum read each frame
    //         (how far they go, which way the drum turns), errors logged, and pictures.
    // Nobody runs the course here: whether a pawn can ride the drum, take a corner or catch a weight is for a person to try.
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.RoyalClockTowerValidation.RunBatch
    //         [-clockTowerNoCapture] -logFile <file>          (Tools/Run-RoyalClockTower.ps1 does it on a copy of the project)
    // Results: Logs/RoyalClockTowerValidation/tower.txt and *.png.
    [InitializeOnLoad]
    public static class RoyalClockTowerValidation
    {
        const string ActiveKey = "ChessFight.R775.ClockTower.Play", ExitKey = "ChessFight.R775.ClockTower.Exit",
                     CodeKey = "ChessFight.R775.ClockTower.Code", EditBadKey = "ChessFight.R775.ClockTower.EditBad";
        const string Folder = "Logs/RoyalClockTowerValidation", Report = "tower.txt";
        public const string SectionPrefab = "Assets/Prefabs/PawnRushSections/Mid_2-2_RoyalClockTower.prefab";
        const float WatchSeconds = 14.5f, RuntimeLimit = 240f;

        static readonly List<string> failures = new List<string>();
        static readonly List<string> errors = new List<string>();
        static readonly Queue<Action> pictures = new Queue<Action>();
        static readonly Dictionary<string, Vector2> ranges = new Dictionary<string, Vector2>();
        static readonly List<Renderer> shell = new List<Renderer>();
        static readonly Dictionary<string, float> handLast = new Dictionary<string, float>(), handTurn = new Dictionary<string, float>();
        static Transform frame, content, drum;
        static Camera captureCamera;
        static double beganAt, playRequestedAt, editorIdleAt;
        static float watchFrom = -1f, lastYaw, turned, turnedOver, oneDown, samples;
        static bool stopping, judged;

        static RoyalClockTowerValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        // ------------------------------------------------------------------ the report

        static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static string V(Vector3 v) => "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")";

        static void Log(string value)
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Path.Combine(Folder, Report), value + "\n");
            if (value.Length > 0) Debug.Log("[RoyalClockTower] " + value);
        }

        static void Check(string name, bool ok, string detail)
        {
            Log((ok ? "PASS  " : "FAIL  ") + name + ": " + detail);
            if (!ok) failures.Add(name + ": " + detail);
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (condition != null && condition.StartsWith("Host type is not matching any asset type") && condition.Contains(".urtshader")) return;
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) errors.Add(condition + "\n" + stack);
        }

        static bool NoCapture => Array.IndexOf(Environment.GetCommandLineArgs(), "-clockTowerNoCapture") >= 0;

        // ------------------------------------------------------------------ Edit

        [MenuItem("ChessFight/Pawn Rush/Validate/Royal Clock Tower (Edit + Play)", false, 208)]
        public static void RunMenu() => RunBatch();

        public static void RunBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, Report), "R775 royal clock tower checks (" +
                DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "). Not a play test: no pawn runs it.\n");
            failures.Clear();
            errors.Clear();
            int bad;
            try { bad = RunEdit(); }
            catch (Exception e)
            {
                Log("FAIL: " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
                bad = 1;
            }
            SessionState.SetInt(EditBadKey, bad);
            AssetDatabase.ImportAsset(SectionPrefab, ImportAssetOptions.ForceUpdate);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitKey, false);
            SessionState.SetInt(CodeKey, 0);
            playRequestedAt = editorIdleAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= BeginPlayWhenIdle;
            EditorApplication.update += BeginPlayWhenIdle;
        }

        static int RunEdit()
        {
            Log("");
            Log("== Edit: built in a scratch scene, every mover in its rest pose");
            var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Application.logMessageReceived += OnLog;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SectionPrefab);
                if (prefab == null) { Check("E0 prefab", false, "none at " + SectionPrefab); return failures.Count; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scratch);
                var tower = go.GetComponent<RoyalClockTowerSection>();
                var section = go.GetComponent<PawnRushSection>();
                if (tower == null || section == null) { Check("E0 prefab", false, "no RoyalClockTowerSection or PawnRushSection on it"); return failures.Count; }
                RoyalClockTowerBuilder.Build(tower);
                frame = section.transform;
                content = frame.Find(RoyalClockTowerSection.ContentName);
                Physics.SyncTransforms();
                Inventory("E1");

                var notes = new List<string>();
                var found = SectionValidator.Run(section, notes);
                foreach (var n in notes) Log("      " + n);
                Check("E2 section checks (SectionValidator)", found.Count == 0, found.Count == 0 ? "no errors" : found.Count + " error(s)");
                foreach (var e in found) Log("        " + e);

                Check("E3 entry and exit", section.EntryLocal.position == RoyalClockTowerSection.Entry && section.ExitLocal.position == RoyalClockTowerSection.Exit,
                      "entry " + V(section.EntryLocal.position) + ", exit " + V(section.ExitLocal.position));
                Faces();
                Check("E5 errors logged while building and checking", errors.Count == 0,
                      errors.Count + (errors.Count > 0 ? ", the first: " + errors[0].Split('\n')[0] : ""));
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }
            return failures.Count;
        }

        static void Inventory(string id)
        {
            int movers = content.GetComponentsInChildren<MovingPlatform>(true).Length, ratchets = content.GetComponentsInChildren<RatchetStep>(true).Length,
                ropes = content.GetComponentsInChildren<RopeLine>(true).Length,
                combs = content.GetComponentsInChildren<ClimbPeel>(true).Length, cps = content.GetComponentsInChildren<CourseCheckpoint>(true).Length,
                kills = content.GetComponentsInChildren<KillVolume>(true).Length, gaps = content.GetComponentsInChildren<IntendedGap>(true).Length;
            Check(id + " what is there", movers == 11 && ratchets == 11 && ropes == 5 && combs == 2 && cps == 10 && kills == 3 && gaps == 3,
                  "moving platforms " + movers + " (8 weights + the drum + 2 hands = 11), ratchet blocks " + ratchets + " (11), ropes " + ropes + " (5), combs " + combs + " (2), checkpoints " + cps +
                  " (10), kill volumes " + kills + " (3), meant gaps " + gaps + " (3); colliders " + content.GetComponentsInChildren<Collider>(true).Length);
        }

        // A ray from a point toward a face: the first solid it meets must be one that can be held, or one that cannot.
        static void Faces()
        {
            int bad = 0, n = 0;
            void Face(string what, bool climb, Vector3 from, Vector3 dir)
            {
                n++;
                bool hit = Physics.Raycast(frame.TransformPoint(from), frame.TransformDirection(dir), out var h, 4f, ~0, QueryTriggerInteraction.Ignore);
                bool held = hit && h.collider.GetComponentInParent<NoClimbSurface>() == null && h.collider.GetComponentInParent<GrabOnlySurface>() == null;
                bool ok = hit && held == climb;
                if (!ok) bad++;
                Log((ok ? "      ok    " : "      WRONG ") + what + ": " + (hit ? h.collider.name + (held ? " (can be held)" : " (cannot)") + " at " + F(h.distance) + " m" : "nothing there") +
                    ", wanted " + (climb ? "a face to climb" : "a face that cannot be climbed"));
            }
            Vector3 n1 = Vector3.forward, s1 = Vector3.back, e1 = Vector3.right, w1 = Vector3.left;
            Face("the teaching wall", true, new Vector3(0f, 1.5f, 9.4f), n1);
            Face("a low weight's head", true, new Vector3(-15.5f, 4.2f, 17.4f), n1);
            Face("a high weight's head (from the low one, up)", true, new Vector3(-6f, 8.4f, 20.6f), n1);
            Face("the cliff between the weights", true, new Vector3(-9f, 7f, 23.4f), n1);
            Face("the yard grille", false, new Vector3(10f, 5f, 22.8f), n1);
            Face("the drop's face (the movement's east side)", true, new Vector3(3f, 10f, 26f), w1);
            Face("a pier's south face", true, new Vector3(9.2f, 6f, 36.4f), n1);
            Face("a pier's east face (band)", true, new Vector3(11.2f, 6.5f, 38.5f), w1);
            Face("a pier over its band", false, new Vector3(11.2f, 10f, 38.5f), w1);
            Face("the island's south side", false, new Vector3(6.5f, 5.9f, 39f), n1);
            Face("the drum", true, new Vector3(8f, 7f, 66f), w1);
            Face("the drum's cap", false, new Vector3(8f, 11.5f, 66f), w1);
            Face("the ratchet wall", true, new Vector3(-14f, 9f, 75.4f), n1);
            Face("the court's south side east of the ratchet wall", false, new Vector3(15f, 9f, 75.4f), n1);
            Face("the dial's face (beside the hands)", false, new Vector3(5f, 17f, 89.4f), n1);
            Face("the minute hand at six (from the plinth)", true, new Vector3(0f, 17f, 86.8f), n1);
            Face("the cornice (from the noon ledge)", true, new Vector3(1.2f, 27.6f, 89f), n1);
            Face("the tower's east side", false, new Vector3(8.8f, 20f, 92f), w1);
            Face("the tower's north side", false, new Vector3(0f, 20f, 100.8f), s1);
            Check("E4 faces to climb and faces that must not be", bad == 0, n + " rays, " + bad + " wrong");
        }

        // ------------------------------------------------------------------ Play

        static void BeginPlayWhenIdle()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - playRequestedAt > 120f)
            {
                EditorApplication.update -= BeginPlayWhenIdle;
                SessionState.SetBool(ActiveKey, false);
                Log("FAIL: the Editor did not settle before Play Mode");
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
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                failures.Clear(); errors.Clear(); pictures.Clear(); ranges.Clear(); shell.Clear(); handLast.Clear(); handTurn.Clear();
                stopping = judged = false; watchFrom = -1f; turned = turnedOver = oneDown = samples = 0f;
                frame = content = drum = null;
                beganAt = EditorApplication.timeSinceStartup;
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
                captureCamera.nearClipPlane = .1f; captureCamera.farClipPlane = 600f;
                captureCamera.enabled = true;
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) CompleteExit();
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || stopping) return;
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            try
            {
                if (EditorApplication.timeSinceStartup - beganAt > RuntimeLimit) throw new InvalidOperationException("the check ran over " + RuntimeLimit + " s");
                if (frame == null) { Place(); return; }
                if (!judged)
                {
                    Watch();
                    if (Time.time - watchFrom < WatchSeconds) return;
                    Judge();
                    judged = true;
                    Pictures();
                    return;
                }
                if (pictures.Count > 0) { pictures.Dequeue()(); return; }
                Finish();
            }
            catch (Exception e)
            {
                Log("FAIL: " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
                failures.Add("exception in the Play check");
                StopPlay(1);
            }
        }

        static void Place()
        {
            Log("");
            Log("== Play: the tower alone, its entry on the origin, watched for " + F(WatchSeconds) + " s of game time");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SectionPrefab);
            var holder = new GameObject("Course being checked");
            holder.SetActive(false);
            var go = Object.Instantiate(prefab, holder.transform);
            go.name = prefab.name;
            var section = go.GetComponent<PawnRushSection>();
            section.PlaceEntryOn(new Pose(Vector3.zero, Quaternion.identity));
            section.Configure(2, 775, 6, ObstacleClock.Now);
            holder.SetActive(true);
            Physics.SyncTransforms();
            frame = section.transform;
            content = frame.Find(RoyalClockTowerSection.ContentName);
            if (content == null) throw new InvalidOperationException("the prefab woke without building " + RoyalClockTowerSection.ContentName);
            var d = content.Find("CT drum");
            drum = d;
            foreach (var r in content.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("CT wall") || r.name.StartsWith("CT entry bay") || r.name.StartsWith("CT entrance lintel")) shell.Add(r);
            Inventory("P1");
            watchFrom = Time.time;
            lastYaw = drum != null ? drum.eulerAngles.y : 0f;
        }

        static void Note(string name, float value)
        {
            if (!ranges.TryGetValue(name, out var r)) r = new Vector2(value, value);
            ranges[name] = new Vector2(Mathf.Min(r.x, value), Mathf.Max(r.y, value));
        }

        // Each frame: how high every weight's top is, where every block's front is, where every rope's end is, how far the
        // drum has turned, and whether some weight stands at the bottom.
        static void Watch()
        {
            bool down = false;
            foreach (var m in content.GetComponentsInChildren<MovingPlatform>())
            {
                var p = frame.InverseTransformPoint(m.transform.position);
                if (m.name.StartsWith("CT weight")) { Note(m.name, p.y); down |= m.name.EndsWith("low") && p.y < RoyalClockTowerBuilder.LowDown + .05f; }
                else if (m.name.EndsWith("hand"))
                {
                    // Degrees clockwise from noon as seen from the court: the bar points along the pivot's local +y.
                    var up = frame.InverseTransformDirection(m.transform.up);
                    float clock = Mathf.Atan2(up.x, up.y) * Mathf.Rad2Deg;
                    if (handLast.TryGetValue(m.name, out float before)) handTurn[m.name] = (handTurn.TryGetValue(m.name, out float t) ? t : 0f) + Mathf.DeltaAngle(before, clock);
                    handLast[m.name] = clock;
                }
            }
            foreach (var r in content.GetComponentsInChildren<RatchetStep>())
            {
                var p = frame.InverseTransformPoint(r.transform.position);
                Note(r.name + " top", p.y + .25f);
                Note(r.name + " front", p.z - .45f);
            }
            foreach (var rope in content.GetComponentsInChildren<RopeLine>())
                Note(rope.name, frame.InverseTransformPoint(rope.Point(rope.Length, ObstacleClock.Now)).z);
            if (drum != null)
            {
                float yaw = drum.eulerAngles.y;
                turned += Mathf.DeltaAngle(lastYaw, yaw);
                lastYaw = yaw;
                turnedOver = Time.time - watchFrom;
            }
            samples++;
            if (down) oneDown++;
        }

        static void Judge()
        {
            bool Near(float a, float b, float within) => Mathf.Abs(a - b) <= within;
            int weights = 0, weightsOk = 0, teeth = 0, teethOk = 0, ropes = 0, ropesOk = 0;
            string worst = "";
            foreach (var kv in ranges)
            {
                if (kv.Key.StartsWith("CT weight"))
                {
                    weights++;
                    float down = kv.Key.EndsWith("low") ? RoyalClockTowerBuilder.LowDown : RoyalClockTowerBuilder.HighDown;
                    bool ok = Near(kv.Value.x, down, .06f) && Near(kv.Value.y, down + RoyalClockTowerBuilder.WeightLift, .06f);
                    if (ok) weightsOk++; else worst += " " + kv.Key + " " + F(kv.Value.x) + ".." + F(kv.Value.y);
                }
                else if (kv.Key.StartsWith("CT ratchet") && kv.Key.EndsWith(" top"))
                {
                    teeth++;
                    int row = kv.Key["CT ratchet ".Length] - '1';
                    float low = RoyalClockTowerBuilder.RatchetLow[row], high = low + RoyalClockTowerBuilder.RatchetRise[row];
                    var front = ranges[kv.Key.Substring(0, kv.Key.Length - 4) + " front"];
                    bool ok = Near(kv.Value.x, low, .06f) && Near(kv.Value.y, high, .06f) && Near(front.x, 75.08f, .06f) && Near(front.y, 75.98f, .06f);
                    if (ok) teethOk++; else worst += " " + kv.Key + " " + F(kv.Value.x) + ".." + F(kv.Value.y) + " front " + F(front.x) + ".." + F(front.y);
                }
                else if (kv.Key.StartsWith("CT rope"))
                {
                    ropes++;
                    bool ok = Near(kv.Value.x, 44.02f, .15f) && Near(kv.Value.y, 52.38f, .15f);
                    if (ok) ropesOk++; else worst += " " + kv.Key + " " + F(kv.Value.x) + ".." + F(kv.Value.y);
                }
            }
            Check("P2 weights' tops: low 5 .. 7.5, high 9 .. 11.5", weights == 8 && weightsOk == 8, weightsOk + " of " + weights + worst);
            Check("P3 ratchet blocks: out 0.9 m, tops from their row's low to high", teeth == 11 && teethOk == 11, teethOk + " of " + teeth);
            foreach (var hand in new[] { ("CT minute hand", RoyalClockTowerBuilder.MinuteSpin), ("CT hour hand", RoyalClockTowerBuilder.HourSpin) })
            {
                float handRate = handTurn.TryGetValue(hand.Item1, out float t) && turnedOver > .5f ? t / turnedOver : 0f;
                Check("P5b " + hand.Item1 + " turns clockwise at " + F(hand.Item2) + " deg/s", Near(handRate, hand.Item2, .5f), F(handRate) + " deg/s");
            }
            Check("P4 ropes' ends go z 44.0 .. 52.4", ropes == 5 && ropesOk == 5, ropesOk + " of " + ropes);
            float rate = turnedOver > .5f ? turned / turnedOver : 0f;
            // Unity's yaw grows from north toward east; east -> north -> west is a falling yaw.
            Check("P5 the drum turns east -> north -> west at 15 deg/s", Near(rate, -15f, .8f), "yaw rate " + F(rate) + " deg/s over " + F(turnedOver) + " s");
            Log("      a low weight standing at the bottom in " + F(100f * oneDown / Mathf.Max(1f, samples)) + " % of " + F(samples) + " frames");
        }

        static void Pictures()
        {
            if (NoCapture) return;
            void Shot(string name, Vector3 eye, Vector3 target, float fov, bool hideShell = true) =>
                pictures.Enqueue(() => Capture(name, eye, target, Vector3.up, fov, false, 1536, 1024, hideShell));
            pictures.Enqueue(() => Capture("tower_00_top", new Vector3(0f, 90f, 60f), new Vector3(0f, 0f, 60f), Vector3.forward, 64f, true, 1024, 1536, true));
            Shot("tower_01_from_south_east", new Vector3(62f, 58f, -30f), new Vector3(-2f, 8f, 62f), 46f);
            Shot("tower_02_from_the_entry", new Vector3(0f, 1.6f, 1.5f), new Vector3(0f, 9f, 40f), 70f, false);
            Shot("tower_03_weights", new Vector3(6f, 10f, 2f), new Vector3(-9f, 8f, 24f), 60f);
            Shot("tower_04_drop_piers_ropes", new Vector3(34f, 24f, 22f), new Vector3(9f, 5f, 44f), 55f);
            Shot("tower_04b_piers_close", new Vector3(17f, 9f, 31f), new Vector3(10f, 6f, 39f), 55f);
            Shot("tower_05_drum", new Vector3(24f, 20f, 48f), new Vector3(0f, 7f, 68f), 55f);
            Shot("tower_06_ratchet_wall", new Vector3(-13f, 9f, 58f), new Vector3(-13f, 10f, 76f), 65f);
            Shot("tower_07_dial", new Vector3(0f, 20f, 70f), new Vector3(0f, 21.5f, 90f), 60f);
            Shot("tower_08_noon_and_roof", new Vector3(18f, 32f, 74f), new Vector3(0f, 25f, 92f), 50f);
        }

        static void Capture(string name, Vector3 eye, Vector3 target, Vector3 up, float fieldOfView, bool flat, int width, int height, bool hideShell)
        {
            if (frame == null || captureCamera == null) return;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Log("NOT RUN: picture " + name + " (no graphics device)"); return; }
            captureCamera.transform.position = frame.TransformPoint(eye);
            captureCamera.transform.rotation = Quaternion.LookRotation(frame.TransformPoint(target) - frame.TransformPoint(eye), frame.TransformDirection(up));
            captureCamera.orthographic = flat;
            if (flat) captureCamera.orthographicSize = fieldOfView;
            else captureCamera.fieldOfView = fieldOfView;
            captureCamera.aspect = (float)width / height;
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var before = RenderTexture.active;
            Texture2D picture = null;
            if (hideShell) foreach (var r in shell) r.enabled = false;
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
                Log("      picture " + name + ".png");
            }
            finally
            {
                foreach (var r in shell) r.enabled = true;
                RenderTexture.active = before;
                if (picture != null) Object.DestroyImmediate(picture);
                texture.Release(); Object.DestroyImmediate(texture);
            }
        }

        static void Finish()
        {
            foreach (var e in errors) Log("ERROR logged in Play: " + e.Split('\n')[0]);
            Check("P6 errors logged in Play", errors.Count == 0, errors.Count.ToString());
            int bad = failures.Count + SessionState.GetInt(EditBadKey, 0);
            Log("");
            Log("Royal clock tower result: " + (bad == 0 ? "PASS" : "FAIL (" + bad + ", Edit and Play together)"));
            StopPlay(bad == 0 ? 0 : 1);
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
