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
    //         they go), the tablecloth's pulls and the tower's overflows counted, errors logged, and pictures.
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
        const float WatchSeconds = 14f, RuntimeLimit = 300f;

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
                Log("      the tower at z " + F(B.TowerZ) + ", the chandeliers' landing z " + F(B.LandZ) + ", the great chandelier at z " + F(B.GreatZ) + ", rope pivots z " +
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
            int waiters = content.GetComponentsInChildren<BanquetWaiter>(true).Length, cloches = content.GetComponentsInChildren<BanquetCloche>(true).Length,
                couples = content.GetComponentsInChildren<WaltzCouple>(true).Length, pleats = content.GetComponentsInChildren<CurtainPleat>(true).Length,
                ropes = content.GetComponentsInChildren<RopeLine>(true).Length, tricks = content.GetComponentsInChildren<TableclothTrick>(true).Length,
                flows = content.GetComponentsInChildren<ChocolateFlow>(true).Length, towers = content.GetComponentsInChildren<ChampagneOverflow>(true).Length,
                levels = content.GetComponentsInChildren<ChampagneLevel>(true).Length, lamps = content.GetComponentsInChildren<ClockLamp>(true).Length,
                great = content.Find("BH great chandelier") != null ? 1 : 0, cps = content.GetComponentsInChildren<CourseCheckpoint>(true).Length,
                kills = content.GetComponentsInChildren<KillVolume>(true).Length, gaps = content.GetComponentsInChildren<IntendedGap>(true).Length;
            Check(id + " what is there", waiters == 5 && cloches == 2 && couples == 10 && pleats == 20 && ropes == 10 && great == 1 && tricks == 1 && flows == 3 &&
                  towers == 1 && levels == 4 && lamps == 4 && cps == 9 && kills == 5 && gaps == 5,
                  "waiters " + waiters + " (5), cloches " + cloches + " (2), couples " + couples + " (10), pleats " + pleats + " (20), chandeliers " + ropes +
                  " (10) and the great one " + great + " (1), tablecloth tricks " + tricks + " (1), chocolate tiers " + flows + " (3), towers " + towers + " (1) with washing levels " +
                  levels + " (4), lamps " + lamps + " (4), checkpoints " + cps + " (9), kill volumes " + kills + " (5), meant gaps " + gaps + " (5); colliders " +
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
            Face("a plate stack (the second waiter's, by the counter)", true, new Vector3(27.4f, 3.7f, 7.8f), n1);
            Face("a waiter's body", false, new Vector3(32f, 3.5f, 10.3f), w1);
            Face("the long table's east side", false, new Vector3(28.6f, 2f, 25f), w1);
            Face("the guests' chairs (from the dance floor)", false, new Vector3(10f, 1f, 30f), e1);
            Face("a cloche's band", true, new Vector3(17f, 5.2f, 55.8f), s1);
            Face("a cloche's dome (from its rim)", false, new Vector3(17f, 6.6f, 55.1f), s1);
            Face("the dais's front", false, new Vector3(0f, 5f, 54f), n1);
            Face("the queen's gown (couple 0, from the dais's edge)", true, new Vector3(-4.25f, 6f, 55.9f), s1);
            Face("the king (couple 0)", false, new Vector3(-4.25f, 4f, 46.8f), n1);
            Face("the stage's front", false, new Vector3(-18.5f, 4f, 36f), w1);
            Face("the curtain (shut)", true, new Vector3(-22.5f, 9f, 38f), w1);
            Face("a double bass", false, new Vector3(-21.5f, 4f, 19f), w1);
            Face("the buffet's east side", false, new Vector3(-21.5f, 4f, 10f), w1);
            Face("the buffet's north side (from the pit)", false, new Vector3(-28f, 4f, 19.5f), s1);
            Face("chocolate tier 2", true, new Vector3(B.Fountain.x, 11f, B.Fountain.y + B.TierR[1] + 2f), s1);
            Face("the loft's face", true, new Vector3(B.Fountain.x, 18.5f, 8f), s1);
            Face("the head table's side", false, new Vector3(12f, 12f, B.TowerZ - 5.5f), n1);
            Face("the pedestal's side", false, new Vector3(0f, 12f, B.TowerZ - 7f), n1);
            Face("the tower's second level", true, new Vector3(0f, 17f, B.TowerZ - 4f), n1);
            Face("the balcony's edge (from the dais)", false, new Vector3(4f, 20.5f, B.TowerZ - 1f), n1);
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
                stopping = judged = false; watchFrom = -1f; samples = 0f; wraps = 0;
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
            foreach (var w in content.GetComponentsInChildren<BanquetWaiter>())
            {
                var p = frame.InverseTransformPoint(w.transform.position);
                Note(w.name, p.z);
                if (lastZ.TryGetValue(w.name, out float z) && p.z < z - 10f) wraps++;
                lastZ[w.name] = p.z;
            }
            foreach (var c in content.GetComponentsInChildren<BanquetCloche>())
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
                if (lamp.Lit(ObstacleClock.Now)) lampLit[lamp.transform.parent.name] = lampLit.TryGetValue(lamp.transform.parent.name, out float f) ? f + 1f : 1f;
            samples++;
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
                    // 14 s at 2.2 m/s is 31 m of a 49 m lane: each covers at least 20 m and stays in the lane.
                    bool ok = kv.Value.x >= B.WaiterZ0 - .05f && kv.Value.y <= B.WaiterZ0 + B.WaiterLane + .05f && kv.Value.y - kv.Value.x > 20f;
                    if (ok) waitersOk++; else worst += " " + kv.Key + " " + F(kv.Value.x) + ".." + F(kv.Value.y);
                }
                else if (kv.Key.StartsWith("BH cloche"))
                {
                    cloches++;
                    bool ok = Near(kv.Value.x, B.TableY + .06f, .06f) && Near(kv.Value.y, B.TableY + .06f + B.ClocheLift, .06f);
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
            Check("P2 waiters walk the lane (and are cut back to the kitchen)", waiters == 5 && waitersOk == 5 && wraps >= 3,
                  waitersOk + " of " + waiters + ", " + wraps + " cuts back to the kitchen" + worst);
            Check("P3 cloches go from the table to 5.6 m over it", cloches == 2 && clochesOk == 2, clochesOk + " of " + cloches + worst);
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
            var tower = content.GetComponentInChildren<ChampagneOverflow>();
            Check("P7 the tablecloth is pulled every 10 s and the tower runs over every 4 s", trick != null && trick.Pulls >= 1 && tower != null && tower.Spills >= 8,
                  "pulls " + (trick != null ? trick.Pulls : -1) + " (nobody on the cloth: throws " + (trick != null ? trick.Throws : -1) + "), level spills " + (tower != null ? tower.Spills : -1));
            // The lamps: lit for 1.6 s of every 4.8 s (a third of the frames, roughly).
            string lit = "";
            bool lampsOk = lampLit.Count == 4;
            foreach (var kv in lampLit)
            {
                float share = kv.Value / Mathf.Max(1f, samples);
                lit += " " + kv.Key + " " + F(100f * share) + " %";
                lampsOk &= share > .2f && share < .45f;
            }
            Check("P9 the boarding and alighting lamps light as queens stop", lampsOk, lampLit.Count + " of 4 lit at all;" + lit);
            Log("      " + F(samples) + " frames watched");
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
            if (NoCapture) return;
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
            Shot("hall_10_tower_balcony", new Vector3(14f, 22f, 52f), new Vector3(0f, 17f, B.TowerZ + 1f), 55f, false);
            Shot("hall_11_dais_gallery", new Vector3(16f, 13.5f, 59f), new Vector3(-4.25f, 10.5f, 56.5f), 60f, false);
            Shot("hall_12_fountain_close", new Vector3(-19f, 13.5f, 16f), new Vector3(-27.7f, 12f, 9.2f), 60f, false);
            Shot("hall_13_from_the_loft", new Vector3(-2.6f, 21.6f, 4f), new Vector3(0f, 18f, 30f), 66f, false);
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
