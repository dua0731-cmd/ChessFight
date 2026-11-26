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
using B = ChessFight.PawnRush.GrandBanquetHallBuilder;

namespace ChessFight.PawnRush.Editor
{
    // Checks for the grand banquet hall's grey box (대연회장, R816), written the royal clock tower's way.
    //   Edit: the prefab built by GrandBanquetHallBuilder in a scratch scene (every mover where it was built): the section
    //         checks (SectionValidator), what is there, which faces can be held (a ray at each face the design calls a wall
    //         to climb or one that must not be), and the waltz's arithmetic (where couple 0's queen stands still, how close
    //         two couples come).
    //   Play: the hall alone in Play Mode for 14 s: every waiter, cloche, couple, pleat and rope read each frame (how far
    //         they go), the cloche lifts' hoist lines and the chocolate's ripples (R828) followed, the tablecloth's pulls
    //         counted, errors logged, and pictures.
    // Nobody runs the course here: whether a pawn can ride a tray, catch a queen or hang on the curtain is for a person.
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.GrandBanquetHallValidation.RunBatch
    //         [-banquetNoCapture] -logFile <file>              (Tools/Run-GrandBanquetHall.ps1 does it on a copy of the project)
    // Results: Logs/GrandBanquetHallValidation/hall.txt and *.png.
    [InitializeOnLoad]
    public static class GrandBanquetHallValidation
    {
        const string ActiveKey = "ChessFight.R816.Banquet.Play", ExitKey = "ChessFight.R816.Banquet.Exit",
                     CodeKey = "ChessFight.R816.Banquet.Code", EditBadKey = "ChessFight.R816.Banquet.EditBad";
        const string Folder = "Logs/GrandBanquetHallValidation", Report = "hall.txt";
        public const string SectionPrefab = "Assets/Prefabs/PawnRushSections/Mid_2-4_GrandBanquetHall.prefab";
        // Offline the obstacle clock is the scene's time, so the pictures come about 14 s in: the drum rolling (the cloth
        // red) and the cloches up.
        const float WatchSeconds = 14f, RuntimeLimit = 480f;

        static readonly List<string> failures = new List<string>();
        static readonly List<string> errors = new List<string>();
        static readonly Queue<Action> pictures = new Queue<Action>();
        static readonly Dictionary<string, Vector2> ranges = new Dictionary<string, Vector2>();
        static readonly List<Renderer> shell = new List<Renderer>();
        static Transform frame, content;
        static Camera captureCamera;
        static double beganAt, playRequestedAt, editorIdleAt;
        static float watchFrom = -1f, samples;
        static int wraps;
        static readonly Dictionary<string, float> lastZ = new Dictionary<string, float>();
        static bool stopping, judged;

        static GrandBanquetHallValidation()
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
            if (value.Length > 0) Debug.Log("[GrandBanquetHall] " + value);
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

        static bool NoCapture => Array.IndexOf(Environment.GetCommandLineArgs(), "-banquetNoCapture") >= 0;

        // R845: "-banquetOnly <start>": only the held-clock pictures whose names start so (no hall or table-show pictures).
        static string Only
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args, "-banquetOnly");
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            }
        }

        // ------------------------------------------------------------------ Edit

        [MenuItem("ChessFight/Pawn Rush/Validate/Grand Banquet Hall (Edit + Play)", false, 209)]
        public static void RunMenu() => RunBatch();

        public static void RunBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, Report), "R816 grand banquet hall checks (" +
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
            Log("== Edit: built in a scratch scene, every mover where it was built (the clock's 0)");
            var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Application.logMessageReceived += OnLog;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SectionPrefab);
                if (prefab == null) { Check("E0 prefab", false, "none at " + SectionPrefab); return failures.Count; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scratch);
                var hall = go.GetComponent<GrandBanquetHallSection>();
                var section = go.GetComponent<PawnRushSection>();
                if (hall == null || section == null) { Check("E0 prefab", false, "no GrandBanquetHallSection or PawnRushSection on it"); return failures.Count; }
                B.Build(hall);
                frame = section.transform;
                content = frame.Find(GrandBanquetHallSection.ContentName);
                Physics.SyncTransforms();
                Inventory("E1");

                var notes = new List<string>();
                var found = SectionValidator.Run(section, notes);
                foreach (var n in notes) Log("      " + n);
                Check("E2 section checks (SectionValidator)", found.Count == 0, found.Count == 0 ? "no errors" : found.Count + " error(s)");
                foreach (var e in found) Log("        " + e);

                Check("E3 entry and exit", section.EntryLocal.position == GrandBanquetHallSection.Entry && section.ExitLocal.position == GrandBanquetHallSection.Exit,
                      "entry " + V(section.EntryLocal.position) + ", exit " + V(section.ExitLocal.position));
                Faces();
                WaltzArithmetic();
                Log("      the balcony from z " + F(B.BalconyZ0) + ", the chandeliers' landing z " + F(B.LandZ) + ", the great chandelier at z " + F(B.GreatZ) + ", rope pivots z " +
                    F(B.ScreenZ + B.RopeAlong[0]) + " .. " + F(B.ScreenZ + B.RopeAlong[B.RopeAlong.Length - 1]) + ", ends " + F(B.RopeLow[0]) + " .. " + F(B.RopeLow[B.RopeLow.Length - 1]) + " m");
                Check("E6 errors logged while building and checking", errors.Count == 0,
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
            int waiters = content.GetComponentsInChildren<BanquetWaiter>(true).Length, cloches = content.GetComponentsInChildren<ClocheLift>(true).Length,
                couples = content.GetComponentsInChildren<WaltzCouple>(true).Length, pleats = content.GetComponentsInChildren<CurtainPleat>(true).Length,
                ropes = content.GetComponentsInChildren<RopeLine>(true).Length, tricks = content.GetComponentsInChildren<TableclothTrick>(true).Length,
                flows = content.GetComponentsInChildren<ChocolateFlow>(true).Length, lamps = content.GetComponentsInChildren<ClockLamp>(true).Length,
                ripples = content.GetComponentsInChildren<ChocolateRipples>(true).Length, hoists = content.GetComponentsInChildren<HoistLine>(true).Length,
                ladders = 0, posts = 0,
                great = content.Find("BH great chandelier") != null ? 1 : 0, cps = content.GetComponentsInChildren<CourseCheckpoint>(true).Length,
                kills = content.GetComponentsInChildren<KillVolume>(true).Length, gaps = content.GetComponentsInChildren<IntendedGap>(true).Length;
            foreach (Transform t in content)
            {
                if (t.name.EndsWith(" ladder") || t.name.StartsWith("BH fountain ladder")) ladders++;
                if (t.name == "BH dumbwaiter post") posts++;
            }
            // R828: the loft's ladder is the only one (the fountain's are gone); the cloche lifts hang from a frame of three posts
            // on four cables and two chains.
            Check(id + " what is there", waiters == 4 && cloches == 4 && couples == 10 && pleats == 20 && ropes == 10 && great == 1 && tricks == 1 && flows == 3 &&
                  ripples == 3 && ladders == 1 && posts == 3 && hoists == 6 && lamps == 4 && cps == 8 && kills == 5 && gaps == 6,
                  "waiters " + waiters + " (4), cloche lifts' parts " + cloches + " (2 platters + 2 cloches), their frame's posts " + posts + " (3) and hoist lines " + hoists +
                  " (4 cables + 2 chains), couples " + couples + " (10), pleats " + pleats + " (20), chandeliers " + ropes + " (10) and the great one " + great +
                  " (1), tablecloth tricks " + tricks + " (1), chocolate tiers " + flows + " (3) with ripples " + ripples + " (3), ladders " + ladders +
                  " (the loft's only), lamps " + lamps + " (4), checkpoints " + cps + " (8), kill volumes " + kills + " (5), meant gaps " + gaps + " (6); colliders " +
                  content.GetComponentsInChildren<Collider>(true).Length);
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
            Face("the screens", false, new Vector3(0f, 3f, 3f), n1);
            Face("the counter's side", false, new Vector3(23f, 1.2f, 2.6f), n1);
            Face("a plate stack (the second waiter's, by the counter)", true, new Vector3(27.4f, 3.7f, 5f), n1);
            Face("a waiter's body", false, new Vector3(32f, 3.5f, B.WaiterZ0 + B.WaiterLane / B.Waiters), w1);
            Face("the pantry's wall over the table's east edge", false, new Vector3(22f, 6f, 40f), e1);
            Face("the long table's east side", false, new Vector3(28.6f, 2f, 25f), w1);
            Face("the guests' chairs (from the dance floor)", false, new Vector3(10f, 1f, 30f), e1);
            Face("a cloche's band (not climbed)", false, new Vector3(19.6f, B.PlatterTop + B.ClocheOpen + .8f, B.ClocheZ), w1);
            Face("the dumbwaiter's middle post (not climbed)", false, new Vector3(B.GantryX[1] - .6f, 12f, B.ClocheZ), e1);
            Face("the dumbwaiter's west post (not climbed)", false, new Vector3(B.GantryX[0] + .6f, 12f, B.ClocheZ), w1);
            Face("the dais's front", false, new Vector3(0f, 5f, 54f), n1);
            Face("the queen's gown (couple 0, from the dais's edge)", true, new Vector3(-4.25f, 6f, 55.9f), s1);
            Face("the king (couple 0)", false, new Vector3(-4.25f, 4f, 46.8f), n1);
            Face("the stage's front", false, new Vector3(-18.5f, 4f, 36f), w1);
            Face("the curtain (shut)", true, new Vector3(-22.5f, 9f, 38f), w1);
            Face("a double bass", false, new Vector3(-21.5f, 4f, 19f), w1);
            Face("the buffet's east side", false, new Vector3(-21.5f, 4f, 10f), w1);
            Face("the buffet's north side (from the pit)", false, new Vector3(-28f, 4f, 19.5f), s1);
            // R828: the chocolate is the way up again, every tier from its north (no ladders on the fountain).
            Face("chocolate tier 1 (its north side)", true, new Vector3(B.Fountain.x, 9f, B.Fountain.y + B.TierR[0] + 2f), s1);
            Face("chocolate tier 2", true, new Vector3(B.Fountain.x, 11f, B.Fountain.y + B.TierR[1] + 2f), s1);
            Face("chocolate tier 3 (its north side)", true, new Vector3(B.Fountain.x, 15f, B.Fountain.y + B.TierR[2] + 2f), s1);
            Face("the loft's ladder", true, new Vector3(B.Fountain.x, 18.5f, 8f), s1);
            Face("the loft's face beside its ladder", false, new Vector3(-20f, 18.5f, 8f), s1);
            Face("the head table's side", false, new Vector3(12f, 12f, B.TablesZ0 - 2f), n1);
            Face("the balcony's front (from the dais)", false, new Vector3(0f, 12f, B.BalconyZ0 - 2f), n1);
            Face("the dais's balustrade", false, new Vector3(5f, 10.9f, 54.5f), n1);
            Face("the dais gallery's west end", false, new Vector3(-9f, 11f, 58f), e1);
            Face("the passage's west end", false, new Vector3(-3f, 2f, 3f), w1);
            Face("the loft's east end", false, new Vector3(2f, 21f, 3f), e1);
            Check("E4 faces to climb and faces that must not be", bad == 0, n + " rays, " + bad + " wrong");
            // The openings: nothing in the boarding place's way down to the queen, nor over the cloches' landing.
            bool Open(Vector3 from, Vector3 dir, float length) => !Physics.Raycast(frame.TransformPoint(from), frame.TransformDirection(dir), length, ~0, QueryTriggerInteraction.Ignore);
            bool boarding = Open(new Vector3(BanquetWaltz.BoardX, 10.9f, 58f), s1, 2.4f), cloches = Open(new Vector3(B.ClocheX[0], 10.9f, 58f), s1, 2.4f);
            Check("E7 the balustrade's openings (the boarding place, the cloches)", boarding && cloches, "boarding " + (boarding ? "open" : "shut") + ", cloches " + (cloches ? "open" : "shut"));
        }

        // Couple 0's queen must stand still in front of the dais at 0 s (1 m off its face) and in front of the stage at about
        // 8.4 s (0.6 m off it), and no two couples may come within 3.2 m (two gowns 2.6 m wide) in a whole lap.
        static void WaltzArithmetic()
        {
            var stops = new List<(float t, Vector2 at)>();
            Vector2 prev = BanquetWaltz.Queen(0, 0.0);
            float prevSpeed = float.MaxValue;
            bool falling = true;
            const float dt = .01f;
            for (float t = dt; t < BanquetWaltz.Lap; t += dt)
            {
                var q = BanquetWaltz.Queen(0, t);
                float speed = (q - prev).magnitude / dt;
                if (falling && speed > prevSpeed && prevSpeed < .3f) stops.Add((t - dt, prev));
                falling = speed <= prevSpeed;
                prevSpeed = speed;
                prev = q;
            }
            var first = BanquetWaltz.Queen(0, 0.0);
            bool board = Mathf.Abs(first.x - BanquetWaltz.BoardX) < .05f && Mathf.Abs(B.DaisZ - (first.y + 1.3f) - 1f) < .1f;
            (float t, Vector2 at) alight = (0f, Vector2.zero);
            foreach (var s in stops)
                if (s.at.x < BanquetWaltz.TrackW - 1f && Mathf.Abs(s.at.y - B.CurtainZ) < .5f) { alight = s; break; }
            bool atStage = alight.t > 0f && Mathf.Abs(B.StageX1 - (alight.at.x - 1.3f) + 0f) < .65f && Mathf.Abs(alight.at.y - B.CurtainZ) < .1f;
            float closest = float.MaxValue;
            for (float t = 0f; t < BanquetWaltz.Lap; t += .05f)
                for (int i = 0; i < BanquetWaltz.Couples; i++)
                    for (int j = i + 1; j < BanquetWaltz.Couples; j++)
                    {
                        BanquetWaltz.Pose(i, t, out var mi, out float bi);
                        BanquetWaltz.Pose(j, t, out var mj, out float bj);
                        Vector2 di = BanquetWaltz.Pair * new Vector2(Mathf.Cos(bi), Mathf.Sin(bi)), dj = BanquetWaltz.Pair * new Vector2(Mathf.Cos(bj), Mathf.Sin(bj));
                        foreach (var a in new[] { mi + di, mi - di })
                            foreach (var c in new[] { mj + dj, mj - dj })
                                closest = Mathf.Min(closest, (a - c).magnitude);
                    }
            Log("      queen 0 stands still " + stops.Count + " times a lap of " + F(BanquetWaltz.Lap) + " s; the corner radius " + F(BanquetWaltz.Radius) + " m");
            Check("E5 the waltz: queen 0 still at the dais at 0 s, at the stage later; couples apart", board && atStage && closest > 3.2f,
                  "at 0 s " + F(first.x) + ", " + F(first.y) + " (shoulders " + F(B.DaisZ - first.y - 1.3f) + " m off the dais); at the stage at " + F(alight.t) + " s " +
                  F(alight.at.x) + ", " + F(alight.at.y) + " (" + F(alight.at.x - 1.3f - B.StageX1) + " m off it); two partners of different couples never closer than " + F(closest) + " m");
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
                failures.Clear(); errors.Clear(); pictures.Clear(); ranges.Clear(); shell.Clear(); lastZ.Clear(); lampLit.Clear();
                stopping = judged = false; watchFrom = -1f; samples = 0f; wraps = 0; showNext = -1; showTaken = 0; heldNext = -1; watchClock = -1.0; watchStep = 0f; watchedTime = 0f;
                frame = content = null;
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
                sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
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
                if (Only == null && ShowPictures()) return;
                if (HeldPictures()) return;
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
            Log("== Play: the hall alone, its entry on the origin, watched for " + F(WatchSeconds) + " s of game time");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SectionPrefab);
            var holder = new GameObject("Course being checked");
            holder.SetActive(false);
            var go = Object.Instantiate(prefab, holder.transform);
            go.name = prefab.name;
            var section = go.GetComponent<PawnRushSection>();
            section.PlaceEntryOn(new Pose(Vector3.zero, Quaternion.identity));
            section.Configure(2, 816, 6, ObstacleClock.Now);
            holder.SetActive(true);
            Physics.SyncTransforms();
            frame = section.transform;
            content = frame.Find(GrandBanquetHallSection.ContentName);
            if (content == null) throw new InvalidOperationException("the prefab woke without building " + GrandBanquetHallSection.ContentName);
            foreach (var r in content.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("BH hall wall") || r.name.StartsWith("BH entry lintel") || r.name.StartsWith("BH exit lintel")) shell.Add(r);
            Inventory("P1");
            watchFrom = Time.time;
        }

        static void Note(string name, float value)
        {
            if (!ranges.TryGetValue(name, out var r)) r = new Vector2(value, value);
            ranges[name] = new Vector2(Mathf.Min(r.x, value), Mathf.Max(r.y, value));
        }

        // Each frame: every waiter's place along the lane (and its wraps), every cloche's height, every couple's middle
        // (its distance off the track's middle), the leading pleats' places, and every rope's end.
        static void Watch()
        {
            // R845: each frame counts for the clock time since the last (frames are not even: a frame the shows move
            // many vertices in is longer), so a lamp's share is of the time, not of the frames.
            double clockNow = ObstacleClock.Now;
            watchStep = watchClock < 0.0 ? 0f : (float)(clockNow - watchClock);
            watchClock = clockNow;
            watchedTime += watchStep;
            foreach (var w in content.GetComponentsInChildren<BanquetWaiter>())
            {
                var p = frame.InverseTransformPoint(w.transform.position);
                Note(w.name, p.z);
                if (lastZ.TryGetValue(w.name, out float z) && p.z < z - 10f) wraps++;
                lastZ[w.name] = p.z;
            }
            foreach (var c in content.GetComponentsInChildren<ClocheLift>())
                Note(c.name, frame.InverseTransformPoint(c.transform.position).y);
            foreach (var p in content.GetComponentsInChildren<CurtainPleat>())
                if (p.name.EndsWith("S1") || p.name.EndsWith("N1")) Note(p.name, frame.InverseTransformPoint(p.transform.position).z);
            foreach (var c in content.GetComponentsInChildren<WaltzCouple>())
            {
                var p = frame.InverseTransformPoint(c.transform.position);
                Note("couple x", p.x);
                Note("couple z", p.z);
            }
            foreach (var rope in content.GetComponentsInChildren<RopeLine>())
                Note(rope.name, frame.InverseTransformPoint(rope.Point(rope.Length, ObstacleClock.Now)).z);
            foreach (var lamp in content.GetComponentsInChildren<ClockLamp>())
                if (lamp.Lit(ObstacleClock.Now)) lampLit[lamp.transform.parent.name] = (lampLit.TryGetValue(lamp.transform.parent.name, out float f) ? f : 0f) + watchStep;
            // R828: how far each hoist line's hook goes and how far the line's ends are from its hook and its pulley (a line
            // left behind would be metres off; one stretched a frame before this read, a few centimetres); how far down each
            // tier's ripples run.
            foreach (var line in content.GetComponentsInChildren<HoistLine>())
            {
                var t = line.transform;
                Vector3 low = t.position - t.up * t.lossyScale.y, high = t.position + t.up * t.lossyScale.y;
                Note("hoist ends off", Mathf.Max((low - line.HookPoint).magnitude, (high - line.PulleyPoint).magnitude));
                Note("hoist " + line.name, frame.InverseTransformPoint(line.HookPoint).y);
            }
            foreach (var r in content.GetComponentsInChildren<ChocolateRipples>())
                for (int i = 0; i < r.Count; i++) Note("ripples " + r.name, r.Ring(i).localPosition.y);
            // The dressed table's show (BanquetTableShow, 11-25): how far each plate tips and leaves its place, and how
            // many candles burn while the cloth lies and while it is away.
            foreach (var p in content.GetComponentsInChildren<BanquetPlate>())
            {
                var at = frame.InverseTransformPoint(p.transform.position);
                Note("plate tip", Vector3.Angle(frame.up, p.transform.up));
                Note("plate off", new Vector2(at.x - B.RunnerX, at.z - (B.TableZ0 + p.Along)).magnitude);
                Note("plate lift", at.y - (B.TableY + .06f));
            }
            var show = content.GetComponent<BanquetTableShow>();
            var trick = content.GetComponentInChildren<TableclothTrick>();
            if (show != null && trick != null && samples > 2f)
            {
                float u = trick.Phase(ObstacleClock.Now);
                if (u > .5f && u < trick.PullAt - .1f) Note("candles lying", show.Lit);
                // (the last candles are still dying for a moment after the cloth's end has gone into the hands)
                if (show.ClothAway && u > trick.PullAt + 1.4f) Note("candles away", show.Lit);
            }
            // R845: the cloche lifts' show - the pulleys' turn, the steam, the jolt.
            var liftShow = content.GetComponent<BanquetLiftShow>();
            if (liftShow != null && samples > 2f) { Note("lift turned", liftShow.Turned); Note("lift steam", liftShow.Steam); Note("lift jolt", liftShow.Jolted); }
            // R845: the stage's show - how far its pictures overlap when the curtain stands shut or open, how narrow a
            // pleat is drawn, how far a hem lags, the leak and the stage lights.
            var stage = content.GetComponent<BanquetStageShow>();
            if (stage != null && samples > 2f)
            {
                if (stage.Opened <= 0f || stage.Opened >= 1f) { Note("stage overlap still", stage.TopOverlap); Note("stage hem overlap still", stage.HemOverlap); }
                Note("stage narrowest", stage.Narrowest);
                Note("stage hem lag", stage.HemLag);
                Note("stage leak", stage.Leak);
                Note("stage lit", stage.Lit);
                Note("stage tassels", stage.Swung);
                Note("stage players", stage.Playing);
                if (stage.Opened <= 0f) Note("stage lit shut", stage.Lit);
            }
            samples++;
        }

        // The show's pictures: the table at these moments of the trick's cycle (each taken on the first frame past it).
        static readonly (float phase, string name)[] showMoments =
        {
            (1f, "lying"), (5.2f, "drum_roll"), (5.62f, "pull_a"), (5.82f, "pull_b"), (6.05f, "pull_c"), (6.6f, "away"),
            (8.1f, "lay_a"), (8.35f, "lay_b"), (8.7f, "lay_c"), (9.3f, "lay_d"), (9.85f, "lay_e"),
        };
        static int showNext = -1;
        static float showLast;

        // True while the show's pictures are still being taken.
        static bool ShowPictures()
        {
            if (NoCapture || content == null) return false;
            var show = content.GetComponent<BanquetTableShow>();
            var trick = content.GetComponentInChildren<TableclothTrick>();
            if (show == null || trick == null) return false;
            float u = trick.Phase(ObstacleClock.Now);
            if (showNext < 0)
            {
                // Begin with the moment that comes next in this cycle.
                showNext = 0;
                showTaken = 0;
                while (showNext < showMoments.Length && showMoments[showNext].phase <= u) showNext++;
                showNext %= showMoments.Length;
                showLast = u;
                return true;
            }
            if (showTaken >= showMoments.Length) return false;
            float want = showMoments[showNext].phase;
            bool passed = showLast <= u ? showLast < want && want <= u : want > showLast || want <= u;
            showLast = u;
            if (!passed) return true;
            string name = "show_" + (showNext + 1).ToString("00", CultureInfo.InvariantCulture) + "_" + showMoments[showNext].name;
            Log("      the show at " + F(u) + " s of the trick (wanted " + F(want) + "): candles burning " + show.Lit + " of " + show.Candles +
                ", the cloth " + (show.ClothAway ? "away" : "drawn " + F(show.Run.Slide(u)) + " m south"));
            Capture(name + "_wide", new Vector3(3f, 14f, 9f), new Vector3(20f, 4.6f, 31f), Vector3.up, 55f, false, 1536, 1024, true);
            Capture(name + "_near", new Vector3(10.5f, 9f, 17f), new Vector3(20f, 4.9f, 25.5f), Vector3.up, 55f, false, 1536, 1024, true);
            showNext = (showNext + 1) % showMoments.Length;
            showTaken++;
            return showTaken < showMoments.Length;
        }

        static int showTaken;

        // ------------------------------------------------------------------ R845: the shows at a held clock

        // The shows (BanquetStageShow ...) at chosen moments: the obstacle clock is held there (ObstacleClock.Use) for a
        // few frames, so every mover and every picture stands where that moment puts it, then the picture. `at` is the
        // clock (300 s: a whole number of the curtain's 15 s and the trick's 10 s).
        readonly struct Held
        {
            public readonly double at;
            public readonly string name;
            public readonly Vector3 eye, target;
            public readonly float fov, wait;
            public readonly Action before;

            // `before`: done as the clock is held (a show set off); `wait`: seconds of the scene's time before the picture.
            public Held(double clock, string picture, Vector3 from, Vector3 to, float fieldOfView, Action beforeIt = null, float waitFor = .12f)
            {
                at = clock; name = picture; eye = from; target = to; fov = fieldOfView; before = beforeIt; wait = waitFor;
            }
        }

        static readonly List<Held> held = new List<Held>();
        static int heldNext = -1, heldFrame;
        static double watchClock = -1.0;
        static float watchStep, watchedTime;
        static float heldTime;

        static void HeldMoments()
        {
            held.Clear();
            const double T = 300.0;
            // The curtain (15 s: shut 0 .. 6, opening 6 .. 11, open 11 .. 13, closing 13 .. 15).
            var curtain = new (float u, string name)[]
            {
                (3f, "shut"), (5.7f, "leak"), (6.9f, "opening_a"), (8.3f, "opening_b"), (10.2f, "opening_c"), (11.5f, "open_settling"),
                (12.8f, "open"), (13.8f, "closing"), (15.35f, "shut_settling"),
            };
            for (int i = 0; i < curtain.Length; i++)
            {
                string n = "stage_" + (i + 1).ToString("00", CultureInfo.InvariantCulture) + "_" + curtain[i].name;
                held.Add(new Held(T + curtain[i].u, n + "_wide", new Vector3(6f, 15f, 31f), new Vector3(-24.5f, 12f, 31.5f), 74f));
                held.Add(new Held(T + curtain[i].u, n + "_seam", new Vector3(-13f, 13f, 40.5f), new Vector3(-24.5f, 12.5f, 40.05f), 62f));
                held.Add(new Held(T + curtain[i].u, n + "_south", new Vector3(-14f, 13f, 22f), new Vector3(-24.5f, 12.5f, 16f), 62f));
            }
            // The orchestra (playing while the curtain is open: 11 .. 13 s) at two moments of a bar, close.
            held.Add(new Held(T + 11.2, "stage_10_players_a", new Vector3(-17f, 11.5f, 39f), new Vector3(-28.5f, 9.8f, 39f), 58f));
            held.Add(new Held(T + 12.0, "stage_11_players_b", new Vector3(-17f, 11.5f, 39f), new Vector3(-28.5f, 9.8f, 39f), 58f));
            held.Add(new Held(T + 12.0, "stage_12_bassists", new Vector3(-12f, 6.5f, 24f), new Vector3(-21.4f, 3f, 23f), 62f));
            // The waltz: a bar's first beat (the gowns kick out) and its rise (beat 2 .. 3), from the floor and close.
            // (300 s is half a bar in: a bar's first beat at 300.8)
            held.Add(new Held(T + 0.8, "waltz_01_beat_one", new Vector3(12f, 10f, 17f), new Vector3(-4f, 5.5f, 30f), 62f));
            held.Add(new Held(T + 0.0, "waltz_02_rise", new Vector3(12f, 10f, 17f), new Vector3(-4f, 5.5f, 30f), 62f));
            var queen = BanquetWaltz.Queen(0, T + 0.8);
            BanquetWaltz.Pose(0, T + 0.8, out var middle, out _);
            var outward = (queen - middle).normalized;
            var near = new Vector3(queen.x, 6f, queen.y);
            held.Add(new Held(T + 0.8, "waltz_03_gown_close", near + new Vector3(outward.x * 6f - outward.y * 2f, .9f, outward.y * 6f + outward.x * 2f), near, 60f));
            // The west cloche lift (12.4 s; at 310 s its cycle starts): rising (the pulleys turn), arriving at the dais (the
            // jolt, the cloche lifting off), the steam, and the cloche lifting off again at the table.
            var dais = new Vector3(B.ClocheX[0], B.DaisY + 1f, B.ClocheZ);
            held.Add(new Held(314.7, "lift_01_rising_pulleys", new Vector3(B.ClocheX[0] + 2f, 15.2f, B.ClocheZ - 3.4f), new Vector3(B.ClocheX[0] + .6f, 16.3f, B.ClocheZ), 48f));
            held.Add(new Held(315.85, "lift_02_arriving", new Vector3(25f, 14.5f, 44f), dais, 55f));
            held.Add(new Held(316.4, "lift_03_steam", new Vector3(25f, 14.5f, 44f), dais, 55f));
            held.Add(new Held(317.2, "lift_04_steam_late", new Vector3(25f, 14.5f, 44f), dais, 55f));
            held.Add(new Held(316.4, "lift_05_steam_close", new Vector3(21.5f, 12.6f, 48.5f), dais + new Vector3(0f, .6f, 0f), 50f));
            // (11-26) just down at the table: the jolt hops up, never into the table
            held.Add(new Held(321.88, "lift_07_table_arrive_low", new Vector3(21.8f, 5.3f, 48.6f), new Vector3(B.ClocheX[0], B.TableY + .4f, B.ClocheZ), 55f));
            // (11-26) the platter at the table, cloche off, seen from above (the user saw the table through it)
            held.Add(new Held(311.5, "lift_08_platter_from_above", new Vector3(B.ClocheX[0] + 2.5f, B.TableY + 4.5f, B.ClocheZ - 3f), new Vector3(B.ClocheX[0], B.TableY, B.ClocheZ), 55f,
                              PlatterFaces));
            held.Add(new Held(322.2, "lift_06_table_open", new Vector3(23f, 8.5f, 45f), new Vector3(B.ClocheX[0], B.TableY + 1.2f, B.ClocheZ), 55f));
            // The chocolate fountain: whole and its crown, at two moments.
            var fountain = new Vector3(B.Fountain.x, 12.5f, B.Fountain.y);
            held.Add(new Held(T, "fountain_01_whole", fountain + new Vector3(8.5f, 2f, 4.8f), fountain, 58f));
            held.Add(new Held(T + 0.9, "fountain_02_whole_later", fountain + new Vector3(8.5f, 2f, 4.8f), fountain, 58f));
            held.Add(new Held(T, "fountain_03_crown", fountain + new Vector3(3.4f, 5.6f, 2.6f), fountain + new Vector3(0f, 4.4f, 0f), 46f));
            held.Add(new Held(T + 0.9, "fountain_04_tier", fountain + new Vector3(5.2f, 0f, 3f), fountain + new Vector3(0f, -1.5f, 0f), 50f));
            held.Add(new Held(T + 1.7, "fountain_05_side_close", fountain + new Vector3(4.2f, -2.6f, 2.2f), fountain + new Vector3(0f, -3.2f, 0f), 48f));
            held.Add(new Held(T + 0.4, "fountain_06_from_below", fountain + new Vector3(7.5f, -3.8f, 6f), fountain + new Vector3(0f, 1f, 0f), 60f));
            // The waiters' walk, the great chandelier's candles and crystals, the dais's candles and the balcony's shower, the
            // guests at the trick (the trick's 10 s: the drum 5 .. 5.6, the pull 5.6, laid again 8 ..), the hall's walls.
            held.Add(new Held(T + 1.0, "waiter_01_walk", new Vector3(22f, 5.5f, 9f), new Vector3(29.8f, 3.5f, 16f), 58f));
            held.Add(new Held(T + 1.25, "waiter_02_walk_step", new Vector3(22f, 5.5f, 9f), new Vector3(29.8f, 3.5f, 16f), 58f));
            held.Add(new Held(T, "great_01_candles", new Vector3(9f, 21.5f, B.GreatZ - 8f), new Vector3(0f, 18.2f, B.GreatZ), 52f));
            held.Add(new Held(T, "great_02_crystals", new Vector3(6.5f, 16.2f, B.GreatZ - 5f), new Vector3(3f, 16.8f, B.GreatZ - 1.5f), 50f));
            held.Add(new Held(T, "dais_01_candelabra", new Vector3(14f, 17f, 52f), new Vector3(12f, 15.5f, B.TablesZ0 + 3f), 55f));
            held.Add(new Held(T, "dais_02_balcony_shower", new Vector3(10f, 22f, B.BalconyZ0 - 6f), new Vector3(0f, 18.5f, B.BalconyZ0 + 4f), 58f,
                              () => content.GetComponent<BanquetDaisShow>()?.Celebrate(), 1.1f));
            held.Add(new Held(T + 5.4, "guests_01_drum", new Vector3(22f, 8.5f, 20f), new Vector3(12.8f, 3.6f, 30f), 58f));
            held.Add(new Held(T + 5.9, "guests_02_pull", new Vector3(22f, 8.5f, 20f), new Vector3(12.8f, 3.6f, 30f), 58f));
            held.Add(new Held(T + 8.6, "guests_03_clap", new Vector3(22f, 8.5f, 20f), new Vector3(12.8f, 3.6f, 30f), 58f));
            held.Add(new Held(T, "hall_wall_01_sconces", new Vector3(-20f, 10f, 30f), new Vector3(-31f, 8.5f, 36f), 60f));
            // The long table's cue and folds (11-26): the drum early and late (the tug's folds, the red beating), the
            // pull's long folds, the folds it settles out of once laid (the trick's 10 s: drum 3.5 .. 5.5, pulled 5.5, laid by 10).
            var trickNear = new Vector3(16f, 7.5f, 15.5f);
            foreach (var (u, n) in new[] { (4.0, "table_01_drum_early"), (5.3, "table_02_drum_late"), (5.65, "table_03_pull"), (10.15, "table_04_settling") })
            {
                held.Add(new Held(T + u, n + "_near", trickNear, new Vector3(20f, 4.4f, 20.5f), 50f));
                held.Add(new Held(T + u, n + "_wide", new Vector3(10.5f, 9f, 17f), new Vector3(20f, 4.9f, 25.5f), 55f));
            }
            // The candle flame close (11-26, BanquetShowKit.NewFlame): a table candelabrum, a wall sconce, the great
            // chandelier's rim, a hanging chandelier (its candles lit without a rider for the picture).
            held.Add(new Held(T, "candle_01_table", new Vector3(17.2f, 8f, 20.4f), new Vector3(15.25f, 7.4f, 22.4f), 42f));
            held.Add(new Held(T, "candle_02_sconce", new Vector3(-27.5f, 8.4f, 39f), new Vector3(-30.7f, 7.9f, 40.97f), 42f));
            held.Add(new Held(T, "candle_03_great", new Vector3(6.5f, 19.3f, B.GreatZ - 1.2f), new Vector3(4.26f, 18.65f, B.GreatZ + .56f), 40f));
            held.Add(new Held(T, "candle_04_hanging", new Vector3(-8.5f, 21f, 13.5f), new Vector3(-2.6f, 19.8f, B.ScreenZ + B.RopeAlong[1]), 42f,
                              () => ChandelierRideLights.CandlesOnlyWhenRidden = false, .6f));
            held.Add(new Held(T + 2.0, "hall_wall_02_banners", new Vector3(-12f, 22f, 26f), new Vector3(-31f, 24f, 36f), 60f));
            var only = Only;
            if (only != null) held.RemoveAll(h => Array.TrueForAll(only.Split(','), s => !h.name.StartsWith(s.Trim(), StringComparison.Ordinal)));
        }

        // True while the held pictures are still being taken.
        static bool HeldPictures()
        {
            if (NoCapture || content == null) return false;
            if (heldNext < 0) { HeldMoments(); heldNext = 0; heldFrame = -1; }
            if (heldNext >= held.Count)
            {
                if (ObstacleClock.Source != null) ObstacleClock.Use(null);
                return false;
            }
            var h = held[heldNext];
            if (heldFrame < 0)
            {
                double at = h.at;
                ObstacleClock.Use(() => at);
                heldFrame = Time.frameCount;
                heldTime = Time.time;
                h.before?.Invoke();
                return true;
            }
            // a few physics steps for the movers (their interpolation settles) and a frame for the pictures
            if (Time.frameCount - heldFrame < 6 || Time.time - heldTime < h.wait) return true;
            var waltz = content.GetComponent<BanquetWaltzShow>();
            if (waltz != null && h.name.StartsWith("waltz", StringComparison.Ordinal))
                Log("      " + h.name + ": gowns out " + F(100f * waltz.Flared) + " %, partners up " + F(waltz.Risen) + " m");
            var fountainShow = content.GetComponent<BanquetFountainShow>();
            if (fountainShow != null && h.name.StartsWith("fountain", StringComparison.Ordinal))
                Log("      " + h.name + ": drops running " + fountainShow.Falling + ", the surface slid " + F(fountainShow.Slid % 10f) + " m");
            var guests = content.GetComponent<BanquetGuestShow>();
            if (guests != null && h.name.StartsWith("guests", StringComparison.Ordinal)) Log("      " + h.name + ": guests turned up to " + F(guests.Moved) + " deg");
            var dais = content.GetComponent<BanquetDaisShow>();
            if (dais != null && h.name.StartsWith("dais", StringComparison.Ordinal)) Log("      " + h.name + ": confetti in the air " + dais.Falling);
            var hallShow = content.GetComponent<BanquetHallShow>();
            if (hallShow != null && h.name.StartsWith("hall_wall", StringComparison.Ordinal)) Log("      " + h.name + ": banners' points out " + F(hallShow.Stirred) + " m");
            var lifts = content.GetComponent<BanquetLiftShow>();
            if (lifts != null && h.name.StartsWith("lift", StringComparison.Ordinal))
                Log("      " + h.name + ": steam puffs " + lifts.Steam + ", pulleys turned " + F(lifts.Turned) + " deg, jolt " + F(lifts.Jolted) + " m");
            var stage = content.GetComponent<BanquetStageShow>();
            if (stage != null && h.name.StartsWith("stage", StringComparison.Ordinal))
                Log("      " + h.name + " at " + F(h.at % 15.0) + " s of the curtain: open " + F(stage.Opened) + ", hem lag " + F(stage.HemLag) +
                    " m, overlap rail " + F(stage.TopOverlap) + " / hem " + F(stage.HemOverlap) + " m, narrowest " + F(stage.Narrowest) +
                    ", leak " + F(stage.Leak) + ", stage lights " + F(stage.Lit));
            Capture(h.name, h.eye, h.target, Vector3.up, h.fov, false, 1536, 1024, true);
            heldNext++;
            heldFrame = -1;
            return true;
        }

        static readonly Dictionary<string, float> lampLit = new Dictionary<string, float>();

        static void Judge()
        {
            bool Near(float a, float b, float within) => Mathf.Abs(a - b) <= within;
            int waiters = 0, waitersOk = 0, cloches = 0, clochesOk = 0, ropes = 0, ropesOk = 0;
            string worst = "";
            foreach (var kv in ranges)
            {
                if (kv.Key.StartsWith("BH waiter"))
                {
                    waiters++;
                    // 14 s at 2.2 m/s is 31 m, more than the 29 m lane: each covers at least 20 m and stays in the lane.
                    bool ok = kv.Value.x >= B.WaiterZ0 - .05f && kv.Value.y <= B.WaiterZ0 + B.WaiterLane + .05f && kv.Value.y - kv.Value.x > 20f;
                    if (ok) waitersOk++; else worst += " " + kv.Key + " " + F(kv.Value.x) + ".." + F(kv.Value.y);
                }
                else if (kv.Key.StartsWith("BH cloche"))
                {
                    // The platter from the table to the dais; the cloche from shut on the platter at the table to open over it
                    // at the dais.
                    cloches++;
                    bool platter = kv.Key.StartsWith("BH cloche platter");
                    float low = B.PlatterTop, high = B.DaisY + (platter ? 0f : B.ClocheOpen);
                    bool ok = Near(kv.Value.x, low, .06f) && Near(kv.Value.y, high, .06f);
                    if (ok) clochesOk++; else worst += " " + kv.Key + " " + F(kv.Value.x) + ".." + F(kv.Value.y);
                }
                else if (kv.Key.StartsWith("BH chandelier"))
                {
                    ropes++;
                    int k = kv.Key[kv.Key.Length - 1] - '1';
                    float mid = B.ScreenZ + B.RopeAlong[k];
                    bool ok = Near(kv.Value.x, mid - B.RopeEnd(k), .2f) && Near(kv.Value.y, mid + B.RopeEnd(k), .2f);
                    if (ok) ropesOk++; else worst += " " + kv.Key + " " + F(kv.Value.x) + ".." + F(kv.Value.y);
                }
            }
            Check("P2 waiters walk the lane (and are cut back to the kitchen)", waiters == B.Waiters && waitersOk == B.Waiters && wraps >= B.Waiters,
                  waitersOk + " of " + waiters + ", " + wraps + " cuts back to the kitchen" + worst);
            Check("P3 cloche lifts: platters from the table to the dais, cloches shut on them below and open over them above", cloches == 4 && clochesOk == 4,
                  clochesOk + " of " + cloches + worst);
            Check("P4 chandeliers' ends swing 4.18 m each way (5.14 m for the second line)", ropes == 10 && ropesOk == 10, ropesOk + " of " + ropes + worst);
            var south = ranges.TryGetValue("BH pleat S1", out var s) ? s : Vector2.zero;
            var north = ranges.TryGetValue("BH pleat N1", out var n) ? n : Vector2.zero;
            Check("P5 the curtain opens: the south half's leading pleat goes to the buffet, the north one to the stage's end",
                  Near(south.x, B.CurtainOpenS, .1f) && Near(south.y, B.CurtainZ - .55f, .1f) && Near(north.y, B.StageZ1 + .5f, .1f),
                  "south " + F(south.x) + ".." + F(south.y) + ", north " + F(north.x) + ".." + F(north.y));
            var cx = ranges.TryGetValue("couple x", out var x) ? x : Vector2.zero;
            var cz = ranges.TryGetValue("couple z", out var z) ? z : Vector2.zero;
            Check("P6 the couples go round the whole floor", Near(cx.x, BanquetWaltz.TrackW, .1f) && Near(cx.y, BanquetWaltz.TrackE, .1f) &&
                  Near(cz.x, BanquetWaltz.TrackS, .1f) && Near(cz.y, BanquetWaltz.TrackN, .1f),
                  "middles x " + F(cx.x) + ".." + F(cx.y) + ", z " + F(cz.x) + ".." + F(cz.y));
            var trick = content.GetComponentInChildren<TableclothTrick>();
            Check("P7 the tablecloth is pulled every 10 s", trick != null && trick.Pulls >= 1,
                  "pulls " + (trick != null ? trick.Pulls : -1) + " (nobody on the cloth: throws " + (trick != null ? trick.Throws : -1) + ")");
            // The lamps: lit for 1.6 s of every 4.8 s (a third of the frames, roughly).
            string lit = "";
            bool lampsOk = lampLit.Count == 4;
            foreach (var kv in lampLit)
            {
                float share = kv.Value / Mathf.Max(.001f, watchedTime);
                lit += " " + kv.Key + " " + F(100f * share) + " %";
                lampsOk &= share > .2f && share < .45f;
            }
            Check("P9 the boarding and alighting lamps light as queens stop", lampsOk, lampLit.Count + " of 4 lit at all;" + lit);
            // R828: the cables' hooks (the platters' lugs) go from the table to the dais, the chains' (the cloches' handles) from
            // shut on the platter at the table to open over it at the dais, and every line stays on its hook and its pulley.
            int hoists = 0, hoistsOk = 0;
            string hoistWorst = "";
            foreach (var kv in ranges)
            {
                if (!kv.Key.StartsWith("hoist BH")) continue;
                hoists++;
                bool chain = kv.Key.Contains("chain");
                float handle = B.ClocheBand + 1.35f, low = B.PlatterTop + (chain ? handle : .07f), high = B.DaisY + (chain ? B.ClocheOpen + handle : .07f);
                bool ok = Near(kv.Value.x, low, .08f) && Near(kv.Value.y, high, .08f);
                if (ok) hoistsOk++; else hoistWorst += " " + kv.Key.Substring(6) + " " + F(kv.Value.x) + ".." + F(kv.Value.y);
            }
            float off = ranges.TryGetValue("hoist ends off", out var o) ? o.y : float.NaN;
            Check("P10 the cloche lifts hang from the frame: cables and chains follow them from the table to the dais", hoists == 6 && hoistsOk == 6 && off < .35f,
                  hoistsOk + " of " + hoists + " lines' hooks over the whole way, the farthest a line's end was from its hook or pulley " + F(off) + " m" + hoistWorst);
            int tiers = 0, tiersOk = 0;
            string rippleRuns = "";
            foreach (var r in content.GetComponentsInChildren<ChocolateRipples>())
            {
                tiers++;
                var run = ranges.TryGetValue("ripples " + r.name, out var v) ? v : new Vector2(float.NaN, float.NaN);
                float lowest = -ChocolateRipples.Half - .01f;   // a ring's middle: its top 1 cm under the rim, a tier's height on
                bool ok = Near(run.x, lowest, .1f) && Near(run.y, r.Height + lowest, .1f);
                if (ok) tiersOk++;
                rippleRuns += " " + r.name.Substring(3) + " " + F(run.x) + ".." + F(run.y) + " of " + F(r.Height) + " m";
            }
            Check("P11 the chocolate's ripples run down the whole of each tier, rim to foot", tiers == 3 && tiersOk == 3, tiersOk + " of " + tiers + ";" + rippleRuns);
            // 11-25: the dressed table's show. The plates' colliders rock (a few degrees, never off their place, never
            // under the table) and every candle is out while the cloth is in the magician's hands and burns while it lies.
            var show = content.GetComponent<BanquetTableShow>();
            if (show == null) Log("NOT RUN: P12 the dressed table's show (no BanquetTableShow: the dressing is off or the trick is missing)");
            else
            {
                Vector2 R(string key) => ranges.TryGetValue(key, out var r) ? r : new Vector2(float.NaN, float.NaN);
                Vector2 tip = R("plate tip"), offPlace = R("plate off"), lift = R("plate lift"), lying = R("candles lying"), away = R("candles away");
                Check("P12 the dressed table's show: the plates rock where they stand, the candles go out and are lit with the cloth",
                      show.Plates == B.PlateZ.Length && show.Sheets > 0 && show.Candles > 0 && tip.y > 2f && tip.y < 8f && offPlace.y < .02f &&
                      lift.x > -.01f && lift.y < .2f && lying.x == show.Candles && away.y == 0f,
                      "plates " + show.Plates + ", tipped up to " + F(tip.y) + " deg, off their place " + F(offPlace.y) + " m, lifted " + F(lift.x) + ".." + F(lift.y) +
                      " m; cloth meshes " + show.Sheets + ", ware " + show.WarePieces + "; candles " + show.Candles + ", burning " + F(lying.x) + ".." + F(lying.y) +
                      " while the cloth lies and " + F(away.x) + ".." + F(away.y) + " while it is away");
            }
            // R845: the stage's show. The pleats' pictures do not lie through one another while the curtain stands shut or
            // open, gather no narrower than the bunched spacing, their hems lag and settle; the seam leaks light before it
            // opens; the stage lights come up open and are out shut.
            var stage = content.GetComponent<BanquetStageShow>();
            if (stage == null) Log("NOT RUN: P13 the stage's show (no BanquetStageShow: the dressing is off)");
            else
            {
                Vector2 R(string key) => ranges.TryGetValue(key, out var r) ? r : new Vector2(float.NaN, float.NaN);
                Vector2 still = R("stage overlap still"), hemStill = R("stage hem overlap still"), swung = R("stage tassels"), playing = R("stage players"), narrow = R("stage narrowest"), hem = R("stage hem lag"), leak = R("stage leak"),
                        stageLit = R("stage lit"), litShut = R("stage lit shut");
                Check("P13 the stage's show: the curtain's pictures gather without lying through one another, the hems swing, light leaks and the stage lights follow",
                      stage.Pleats == 2 * B.Pleats && stage.Meshes >= stage.Pleats && still.y < .05f && hemStill.y < .15f && swung.y > 1f && playing.y > 1f && narrow.x >= .29f && hem.y > .05f && hem.y < .8f &&
                      leak.y > .5f && stageLit.y > .9f && litShut.y < .01f,
                      "pleats " + stage.Pleats + " (skinned meshes " + stage.Meshes + "), tassels " + stage.TasselCount + " swinging up to " + F(swung.y) + " deg; overlap while still up to " + F(still.y) + " m at the rail, " + F(hemStill.y) + " at the hem" +
                      " m, narrowest " + F(narrow.x) + ", hems lag up to " + F(hem.y) + " m, leak up to " + F(leak.y) + ", stage lights " + F(stageLit.x) + ".." + F(stageLit.y) +
                      " (shut " + F(litShut.y) + "); players " + stage.Musicians + " leaning up to " + F(playing.y) + " deg; " + F(stage.Cost) + " ms a frame setting " + stage.Bones + " bones");
            }
            // R845: the waltz's show - the gowns and the partners found, every collider left as it was built.
            var waltzShow = content.GetComponent<BanquetWaltzShow>();
            if (waltzShow == null) Log("NOT RUN: P14 the waltz's show (no BanquetWaltzShow: the dressing is off)");
            else
                Check("P14 the waltz's show: the queens' gowns flare and the partners rise with the beat", waltzShow.Couples == BanquetWaltz.Couples &&
                      waltzShow.Partners == BanquetWaltz.Couples && waltzShow.Gowns > 0,
                      "couples " + waltzShow.Couples + ", partners " + waltzShow.Partners + ", skinned gowns " + waltzShow.Gowns);
            var fountainCheck = content.GetComponent<BanquetFountainShow>();
            if (fountainCheck == null) Log("NOT RUN: P16 the fountain's show (no BanquetFountainShow: the dressing is off)");
            else
                Check("P16 the fountain's show: chocolate sheets over every tier and round the crown, drops down them, rings across the pools, the chocolate sliding",
                      fountainCheck.Sheets == B.TierR.Length + 1 && fountainCheck.RipplesHidden == 3 * B.Ripples && fountainCheck.Drops == 21 && fountainCheck.Rings == 12 && fountainCheck.Surfaces > 0,
                      "chocolate sheets " + fountainCheck.Sheets + " (the builder's ripple rings drawn as their swells: " + fountainCheck.RipplesHidden + " hidden), drops " + fountainCheck.Drops + ", rings " + fountainCheck.Rings + ", chocolate surfaces " + fountainCheck.Surfaces);
            ShowsChecks();
            var liftCheck = content.GetComponent<BanquetLiftShow>();
            if (liftCheck == null) Log("NOT RUN: P15 the cloche lifts' show (no BanquetLiftShow: the dressing is off)");
            else
            {
                Vector2 R(string key) => ranges.TryGetValue(key, out var r) ? r : new Vector2(float.NaN, float.NaN);
                Vector2 turned = R("lift turned"), steamed = R("lift steam"), jolt = R("lift jolt");
                Check("P15 the cloche lifts' show: the pulleys turn with the lines, steam rises as a cloche lifts off, the lifts jolt as they arrive",
                      liftCheck.Lifts == 2 && liftCheck.PulleyMeshes > 0 && turned.y > 1000f && steamed.y >= 4f && jolt.y > .01f && jolt.y < .06f,
                      "lifts " + liftCheck.Lifts + ", skinned frame meshes " + liftCheck.PulleyMeshes + ", pulleys turned up to " + F(turned.y) +
                      " deg, steam puffs up to " + F(steamed.y) + ", jolt up to " + F(jolt.y) + " m");
            }
            Log("      " + F(samples) + " frames watched");
        }

        // (11-26) Which way the platters' walked tops face: their triangles within 1 cm of the top and 2 m of the middle,
        // how many face up (seen from above) and how many down (seen from below only: the table shows through them).
        static void PlatterFaces()
        {
            foreach (var lift in content.GetComponentsInChildren<ClocheLift>())
            {
                if (lift.Which != ClocheLift.Part.Platter) continue;
                int up = 0, down = 0;
                foreach (var f in lift.GetComponentsInChildren<MeshFilter>())
                {
                    var m = f.sharedMesh;
                    if (m == null || !m.isReadable || !f.name.EndsWith("_LOD0")) continue;
                    var v = m.vertices;
                    var tr = m.triangles;
                    for (int i = 0; i + 2 < tr.Length; i += 3)
                    {
                        Vector3 a = f.transform.TransformPoint(v[tr[i]]), b = f.transform.TransformPoint(v[tr[i + 1]]), c = f.transform.TransformPoint(v[tr[i + 2]]);
                        var top = lift.transform.position.y;
                        if (Mathf.Abs(a.y - top) > .01f || Mathf.Abs(b.y - top) > .01f || Mathf.Abs(c.y - top) > .01f) continue;
                        var mid = (a + b + c) / 3f - lift.transform.position;
                        if (new Vector2(mid.x, mid.z).magnitude > 2f) continue;
                        if (Vector3.Cross(b - a, c - a).y > 0f) up++; else down++;
                    }
                    Log("      platter " + lift.name + " mesh " + f.name + ": top triangles facing up " + up + ", down " + down +
                        "; root y " + F(frame.InverseTransformPoint(lift.transform.position).y));
                    var rend = f.GetComponent<Renderer>();
                    string mats = "";
                    if (rend != null) foreach (var mm in rend.sharedMaterials) mats += " " + (mm != null ? mm.name + "/" + mm.shader.name + "/q" + mm.renderQueue : "null");
                    Log("        submeshes " + m.subMeshCount + ", renderer " + (rend == null ? "none" : rend.GetType().Name + " enabled " + rend.enabled + " visible " + rend.isVisible) + ", materials" + mats);
                    for (int s = 0; s < m.subMeshCount; s++) Log("        submesh " + s + ": " + m.GetSubMesh(s).indexCount / 3 + " triangles");
                }
            }
        }

        // R845: the shows' parts found and every skinned picture drawn by its part's LOD group (a skinned mesh out of
        // its group would be drawn at every level at once).
        static void ShowsChecks()
        {
            var grouped = new HashSet<Renderer>();
            foreach (var g in content.GetComponentsInChildren<LODGroup>(true))
                foreach (var lod in g.GetLODs())
                    if (lod.renderers != null)
                        foreach (var r in lod.renderers) if (r != null) grouped.Add(r);
            int skins = 0, loose = 0;
            string looseNames = "";
            foreach (var s in content.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (s.sharedMesh == null || !s.sharedMesh.name.EndsWith("(연출 뼈대)", StringComparison.Ordinal)) continue;
                skins++;
                if (s.name.Contains("_LOD") && !grouped.Contains(s)) { loose++; if (loose <= 3) looseNames += " " + s.name; }
            }
            Check("P17 the shows' skinned pictures are each drawn by their part's LOD group", skins > 0 && loose == 0,
                  skins + " skinned, " + loose + " outside a LOD group" + looseNames);
            var waiter = content.GetComponent<BanquetWaiterShow>();
            var great = content.GetComponent<BanquetGreatChandelierShow>();
            var dais = content.GetComponent<BanquetDaisShow>();
            var guests = content.GetComponent<BanquetGuestShow>();
            var hall = content.GetComponent<BanquetHallShow>();
            if (waiter == null || great == null || dais == null || guests == null || hall == null)
            {
                Log("NOT RUN: P18 the waiters', great chandelier's, dais's, guests' and walls' shows (the dressing is off)");
                return;
            }
            Check("P18 the waiters waddle, the great chandelier and the head tables' candles burn, the guests watch, the walls' sconces burn and banners stir",
                  waiter.Waiters == B.Waiters && great.Candles == 24 && great.Skinned > 0 && dais.Candles > 0 && dais.Pieces > 0 &&
                  guests.Guests == 9 && hall.Candles > 0 && hall.Banners > 0,
                  "waiters " + waiter.Waiters + "; great chandelier candles " + great.Candles + ", crystal meshes " + great.Skinned + "; head tables' candles " +
                  dais.Candles + ", confetti " + dais.Pieces + "; guests " + guests.Guests + "; sconce candles " + hall.Candles + ", banners " + hall.Banners);
        }

        static void Pictures()
        {
            // What the moving pictures show at the moment of the pictures (the cloth is away for 2.2 s of its 10).
            var trick = content.GetComponentInChildren<TableclothTrick>();
            if (trick != null)
            {
                var cloth = trick.transform.Find("BH tablecloth/BH cloth");
                var shown = cloth != null ? cloth.GetComponent<Renderer>() : null;
                Log("      at the pictures: the trick " + F(trick.Phase(ObstacleClock.Now)) + " s into its 10 s, the cloth " +
                    (shown == null ? "missing" : shown.enabled ? "drawn at z " + F(frame.InverseTransformPoint(cloth.position).z) + ", scale z " + F(cloth.parent.localScale.z) : "away"));
            }
            if (NoCapture || Only != null) return;
            void Shot(string name, Vector3 eye, Vector3 target, float fov, bool hideShell = true) =>
                pictures.Enqueue(() => Capture(name, eye, target, Vector3.up, fov, false, 1536, 1024, hideShell));
            pictures.Enqueue(() => Capture("hall_00_top", new Vector3(0f, 120f, 36f), new Vector3(0f, 0f, 36f), Vector3.forward, 40f, true, 1024, 1152, true));
            Shot("hall_01_from_south_east", new Vector3(70f, 62f, -24f), new Vector3(-4f, 6f, 38f), 50f);
            Shot("hall_02_from_east", new Vector3(78f, 40f, 40f), new Vector3(-6f, 8f, 36f), 52f);
            Shot("hall_03_servery_waiters", new Vector3(14f, 8f, 1f), new Vector3(27f, 3f, 14f), 62f, false);
            Shot("hall_04_long_table", new Vector3(24f, 9.5f, 13f), new Vector3(19f, 5f, 40f), 60f, false);
            Shot("hall_05_cloches_dais", new Vector3(30f, 14f, 44f), new Vector3(17f, 8f, 56f), 55f);
            Shot("hall_06_waltz_from_dais", new Vector3(-4f, 14f, 60f), new Vector3(-6f, 3f, 36f), 66f, false);
            Shot("hall_07_stage_curtain", new Vector3(-6f, 14f, 30f), new Vector3(-24f, 10f, 30f), 66f, false);
            Shot("hall_08_buffet_fountain", new Vector3(-12f, 16f, 22f), new Vector3(-27f, 11f, 8f), 60f, false);
            Shot("hall_09_chandeliers", new Vector3(18f, 26f, 2f), new Vector3(0f, 19f, 36f), 62f, false);
            Shot("hall_10_balcony", new Vector3(14f, 22f, 50f), new Vector3(0f, 16f, B.BalconyZ0 + 3f), 55f, false);
            Shot("hall_11_dais_gallery", new Vector3(16f, 13.5f, 59f), new Vector3(-4.25f, 10.5f, 56.5f), 60f, false);
            Shot("hall_12_fountain_close", new Vector3(-19f, 13.5f, 16f), new Vector3(-27.7f, 12f, 9.2f), 60f, false);
            Shot("hall_13_from_the_loft", new Vector3(-2.6f, 21.6f, 4f), new Vector3(0f, 18f, 30f), 66f, false);
            Shot("hall_14_chandelier_close", new Vector3(-8.5f, 21f, 13.5f), new Vector3(-2.6f, 19.8f, B.ScreenZ + B.RopeAlong[1]), 42f, false);
            Shot("hall_15_great_chandelier", new Vector3(10f, 22f, 28f), new Vector3(0f, 18.5f, B.GreatZ), 52f, false);
            Shot("hall_16_cloche_lifts", new Vector3(20f, 9f, 41.5f), new Vector3(20f, 10.5f, B.ClocheZ), 66f, false);
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
            Check("P8 errors logged in Play", errors.Count == 0, errors.Count.ToString());
            int bad = failures.Count + SessionState.GetInt(EditBadKey, 0);
            Log("");
            Log("Grand banquet hall result: " + (bad == 0 ? "PASS" : "FAIL (" + bad + ", Edit and Play together)"));
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
