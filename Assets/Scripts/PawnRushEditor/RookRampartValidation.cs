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
using B = ChessFight.PawnRush.RookRampartBuilder;
using T = ChessFight.PawnRush.RookRampartTimes;

namespace ChessFight.PawnRush.Editor
{
    // Checks for the rooks' rampart walk's grey box (룩 성벽길, R860), in Edit mode only: the prefab built by RookRampartBuilder
    // in a scratch scene, the section checks (SectionValidator), what is there, which faces can be held (a ray at each face
    // the design climbs and at walls that must not be), where each climb's top-out lands (RagdollPawn.TryStartTopOut's probe:
    // 0.9 m up and climbHug + 0.4 m in from the top of the face, a floor at most 0.25 m over the anchor), and pictures with
    // every mover put where the shared clock has it at a chosen time (IRampartPreview). Nobody runs the course here: whether
    // a pawn can ride a tower, grab the knight or wait on the ledge is for a person.
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.RookRampartValidation.RunBatch -logFile <file>
    //         (Tools/Run-RookRampart.ps1 does it on a copy of the project)
    // Results: Logs/RookRampartValidation/rampart.txt and *.png.
    public static class RookRampartValidation
    {
        const string Folder = "Logs/RookRampartValidation", Report = "rampart.txt";
        public const string SectionPrefab = "Assets/Prefabs/PawnRushSections/Early_1-5_RookRampart.prefab";
        const float Hug = .23f;   // RagdollTuning climbHug

        static readonly List<string> failures = new List<string>();
        static Transform frame;
        static Camera camera;

        static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        static string Vs(Vector3 v) => "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")";

        static void Log(string value)
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Path.Combine(Folder, Report), value + "\n");
            if (value.Length > 0) Debug.Log("[RookRampart] " + value);
        }

        static void Check(string name, bool ok, string detail)
        {
            Log((ok ? "PASS  " : "FAIL  ") + name + ": " + detail);
            if (!ok) failures.Add(name + ": " + detail);
        }

        [MenuItem("ChessFight/Pawn Rush/Validate/Rooks' Rampart Walk (Edit)", false, 210)]
        public static void RunMenu() => RunBatch();

        public static void RunBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, Report), "R860 rooks' rampart walk checks (" +
                DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "). Edit mode, not a play test: no pawn runs it.\n");
            failures.Clear();
            try { Run(); }
            catch (Exception e) { Check("E0 run", false, e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace); }
            Log(failures.Count == 0 ? "ALL PASS" : failures.Count + " FAILED");
            if (Application.isBatchMode) EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
        }

        static void Run()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SectionPrefab);
            if (prefab == null) { Check("E0 prefab", false, "none at " + SectionPrefab); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var rampart = go.GetComponent<RookRampartSection>();
            var section = go.GetComponent<PawnRushSection>();
            if (rampart == null || section == null) { Check("E0 prefab", false, "no RookRampartSection or PawnRushSection"); return; }
            B.Build(rampart);
            frame = go.transform;
            var content = frame.Find(RookRampartSection.ContentName);
            Physics.SyncTransforms();

            Log("");
            Log("== The section checks (SectionValidator)");
            var notes = new List<string>();
            var errors = SectionValidator.Run(section, notes);
            foreach (var n in notes) Log("      " + n);
            foreach (var e in errors) Log("      error: " + e);
            Check("E1 section checks", errors.Count == 0, errors.Count + " errors");
            Check("E1 entry and exit", section.EntryLocal.position == Vector3.zero && section.ExitLocal.position == new Vector3(0f, B.GateY, B.KeepZ),
                  Vs(section.EntryLocal.position) + " -> " + Vs(section.ExitLocal.position));

            Log("");
            Log("== What is there");
            void Count<TC>(string what, int expected) where TC : Component
            {
                int n = content.GetComponentsInChildren<TC>(true).Length;
                Check("E2 " + what, n == expected, n + " (expected " + expected + ")");
            }
            Count<SiegeLadder>("siege ladders", 6);
            Count<LadderDefender>("rook defenders", 2);
            Count<MurderHoles>("murder holes", 1);
            Count<SiegeTower>("siege towers", 2);
            Count<SiegeBridge>("drop-bridges", 2);
            Count<DrillRook>("drill rooks", 4);
            Count<RookCrane>("rook crane", 1);
            Count<KnightCrane>("knight crane", 1);
            Count<KingsBanner>("banner lengths", 2);
            Count<CourseCheckpoint>("checkpoints", 8);
            Count<ProgressPath>("progress path", 1);

            Log("");
            Log("== Faces: held or not (a ray at each, every mover where the clock has it at 4.0 s: ladders up, banner calm)");
            Pose(4.0);
            // Ladder 1 (pushed at 0 s) leans on the wall at 4 s; ladder 4 is being hauled up then.
            float lad = T.Ladders[0], bz = (B.BannerZ0 + B.BannerZ1) * .5f;
            Face("H1 a siege ladder (1, against the wall)", V(lad, 2.2f, B.LadderFootZ - 1.2f), Vector3.forward, true);
            Face("H2 siege tower A's first ladder (docked)", V(B.SiegeX[0] - .9f, B.Floors[0] + 1.4f, B.SiegeZ0 - 1.9f), Vector3.forward, true);
            Face("H4 the banner's lower length", V(B.GateHX0 - 3.5f, B.W3 + 1.4f, bz), Vector3.right, true);
            Face("H5 the banner's upper length (from the ledge)", V(B.GateHX0 - 1f, B.LedgeY + 1.2f, bz), Vector3.right, true);
            Face("N1 the outer wall's face", V(-20f, 2f, 8f), Vector3.forward, false);
            Face("N2 the middle wall's face (from the lower ward)", V(-25f, 4f, 44f), Vector3.forward, false);
            Face("N3 the inner wall's face (from the ditch)", V(-36f, 0f, 60f), Vector3.forward, false);
            Face("N4 the gatehouse's west face beside the banner", V(B.GateHX0 - 2f, B.W3 + 1.2f, 79f), Vector3.right, false);
            Face("N5 a siege tower's side hide", V(B.SiegeX[0] - 3.5f, 6f, B.SiegeZ0), Vector3.right, false);
            // The knight at its low end, just turned north (its mane faces the middle wall), and a drill rook resting at its end.
            Pose(T.KnightDelay + T.KnightTurn + .02);
            Face("H3 the knight's mane (low end, facing north)", V(B.KnightCraneX, B.KnightLowY + 2.6f, B.KnLowZ - 3f), Vector3.forward, true);
            Pose(.5);
            Face("N6 a drill rook (resting at its end)", V(B.SquareMid(2, 0).x, B.BoardY + 1.8f, B.SquareMid(2, 0).y - 2.5f), Vector3.forward, false);
            Pose(T.GustWarn + T.GustOut + .5);
            Face("N7 the banner's lower length at 40 degrees (too flat)", V(B.GateHX0 - 4f, B.W3 + 1.4f, bz), Vector3.right, false);

            Log("");
            Log("== Top-outs (where the climb over each face's top lands)");
            Pose(4.0);
            TopOut("T1 a siege ladder onto its crenel's sill", V(lad, B.LadderClimb + .3f, B.LadderFootZ - 1.2f), Vector3.forward, B.W1 + B.SillH, .2f);
            TopOut("T2 siege tower A's first ladder onto its second floor", V(B.SiegeX[0] - .9f, B.Floors[1] + .25f, B.SiegeZ0 - 1.9f), Vector3.forward, B.Floors[1], .2f);
            TopOut("T3 siege tower A's second ladder onto its top floor", V(B.SiegeX[0] + .9f, B.Floors[2] + .25f, B.SiegeZ0 - .3f), Vector3.forward, B.Floors[2], .2f);
            TopOut("T4 the banner's lower length onto the ledge", V(B.GateHX0 - 3.5f, B.LedgeY - .3f, bz), Vector3.right, B.LedgeY, .2f);
            TopOut("T5 the banner's upper length onto the roof", V(B.GateHX0 - 1f, B.GateY - .3f, bz), Vector3.right, B.GateY, .2f);

            Log("");
            Log("== Moves (the clock's functions at their marked times)");
            Check("M1 ladder 1 leaning at 4 s, upright about 0.94 s, down at 1.8 s", Mathf.Abs(T.LadderAngle(0, 4.0) - 75f) < .01f
                  && Mathf.Abs(T.LadderAngle(0, .94) - 90f) < 2f && Mathf.Abs(T.LadderAngle(0, 1.8) - 180f) < .01f,
                  F(T.LadderAngle(0, 4.0)) + " / " + F(T.LadderAngle(0, .94)) + " / " + F(T.LadderAngle(0, 1.8)));
            T.Siege(13.0, 0f, B.SiegeZ0, B.SiegeZ1(0), out float zA, out float bA);
            Check("M2 siege tower A at the middle wall, its bridge down, at 13 s", Mathf.Abs(zA - B.SiegeZ1(0)) < .01f && bA < .01f, "z " + F(zA) + ", bridge " + F(bA));
            float gap = Wall2Gap();
            Check("M3 a docked tower's bridge reaches over the middle wall's sill", gap > .5f, "the bridge's tip " + F(gap) + " m past the wall's face");
            T.Knight(T.KnightDelay + T.KnightTurn + T.KnightLeap + .3, B.KnightLowY, B.W3, B.KnLowZ, B.KnHighZ, out float ky, out float kz, out float kf);
            Check("M4 the knight turning on the inner wall", Mathf.Abs(kz - B.KnHighZ) < .01f && kf > 0f && kf < 180f, "z " + F(kz) + ", facing " + F(kf));
            Check("M5 the banner at 40 degrees in the gust", Mathf.Abs(T.BannerAngle(2.0) - 40f) < .01f, F(T.BannerAngle(2.0)));

            Pictures(content);
            Object.DestroyImmediate(go);
        }

        // How far a docked tower's lowered bridge reaches past the middle wall's face (its tip over the walk).
        static float Wall2Gap() => B.SiegeZ1(0) + B.SiegeW * .5f + B.BridgeL - B.Wall2Face(B.SiegeX[0]);

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // Every mover (and its pictures) where the clock has it at `time`.
        static void Pose(double time)
        {
            foreach (var p in frame.GetComponentsInChildren<IRampartPreview>(true)) p.Preview(time);
            foreach (var d in frame.GetComponentsInChildren<LadderDefender>(true)) d.PosePole(time);
            foreach (var t in frame.GetComponentsInChildren<SiegeTower>(true)) t.PosePictures(time);
            foreach (var r in frame.GetComponentsInChildren<DrillRook>(true)) r.PoseLine(time);
            foreach (var r in frame.GetComponentsInChildren<RookCrane>(true)) r.PoseRig(time);
            foreach (var k in frame.GetComponentsInChildren<KnightCrane>(true)) k.PoseRig(time);
            Physics.SyncTransforms();
        }

        // A face in front of `from` along `dir`: held (orange) or not, by the climb's own rule (RagdollPawn.ClimbableHit).
        static void Face(string name, Vector3 from, Vector3 dir, bool held)
        {
            Vector3 o = frame.TransformPoint(from), d = frame.TransformDirection(dir);
            if (!Physics.Raycast(o, d, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore)) { Check(name, false, "nothing hit from " + Vs(from)); return; }
            bool climbable = Holdable(hit);
            Check(name, climbable == held, hit.collider.name + " at " + Vs(frame.InverseTransformPoint(hit.point)) + ", normal y " + F(hit.normal.y) +
                  (climbable ? " - held" : " - not held"));
        }

        static bool Holdable(RaycastHit hit)
        {
            var mark = hit.collider.GetComponent<NoClimbSurface>();
            if (mark != null && mark.enabled) return false;
            if (hit.collider.GetComponent<GrabOnlySurface>() != null) return false;
            var rb = hit.collider.attachedRigidbody;
            // A mover's body turns kinematic when it wakes (Obstacle.Awake); in Edit mode it has not woken.
            if (rb != null && !rb.isKinematic && rb.GetComponent<Obstacle>() == null) return false;
            return Mathf.Abs(hit.normal.y) <= Mathf.Cos(55f * Mathf.Deg2Rad);
        }

        // TryStartTopOut's probe from a climber whose chest has just passed the top of the face ahead of `from` along `dir`.
        static void TopOut(string name, Vector3 from, Vector3 dir, float expectTop, float tolerance)
        {
            Vector3 o = frame.TransformPoint(from), d = frame.TransformDirection(dir);
            if (!Physics.Raycast(o, d, out var face, 6f, ~0, QueryTriggerInteraction.Ignore)) { Check(name, false, "no face ahead of " + Vs(from)); return; }
            Vector3 anchor = face.point + face.normal * Hug;
            Vector3 over = anchor + Vector3.up * .9f - face.normal * (Hug + .4f);
            bool inside = Physics.CheckSphere(over, .05f, ~0, QueryTriggerInteraction.Ignore);
            bool floor = Physics.Raycast(over, Vector3.down, out var top, 1.5f, ~0, QueryTriggerInteraction.Ignore) && top.normal.y >= .6f;
            float at = floor ? frame.InverseTransformPoint(top.point).y : float.NaN;
            bool ok = !inside && floor && Mathf.Abs(at - expectTop) <= tolerance;
            Check(name, ok, (inside ? "the probe starts inside " : "") + (floor ? "lands on " + top.collider.name + " at y " + F(at) : "no floor under the probe") +
                  " (face " + face.collider.name + ")");
        }

        // ------------------------------------------------------------------ pictures

        static void Pictures(Transform content)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Log("NOT RUN: pictures (no graphics device)"); return; }
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            var sun = new GameObject("Validation sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.25f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -40f, 0f);
            RenderSettings.sun = sun;
            camera = new GameObject("Validation camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = .1f; camera.farClipPlane = 800f;
            Log("");
            Log("== Pictures (each at a clock time: where the shared clock puts every mover)");
            Shot("rr_01_overview", 13.75, V(58f, 62f, -38f), V(-2f, 6f, 48f), 50f);
            Shot("rr_02_from_the_glacis", 1.2, V(2f, 1.7f, -3f), V(-9f, 4f, 11f), 62f);
            Shot("rr_03_siege_ladders", 1.2, V(-3f, 7.5f, -2f), V(-11f, 3f, 11f), 60f);
            Shot("rr_04_murder_holes", .8, V(5f, 15f, 6.5f), V(5f, 3.5f, 15f), 60f);
            Shot("rr_05_siege_towers", 13.0, V(5f, 13f, 21f), V(16f, 6.5f, 34f), 66f);
            Shot("rr_06_drill_square", 7.6, V(0f, 38f, 30f), V(0f, 9f, 47f), 55f);
            Shot("rr_07_cranes", 13.75, V(-37f, 22f, 51f), V(-27f, 12f, 62f), 60f);
            Shot("rr_08_banner_calm", 4.5, V(-22f, 18f, 66f), V(-10f, 17f, 74f), 55f);
            Shot("rr_09_banner_gust", 9.0, V(-22f, 18f, 66f), V(-10f, 17f, 74f), 55f);
            Shot("rr_10_from_the_inner_wall", 13.75, V(-24f, 16.5f, 72f), V(0f, 10f, 46f), 70f);
            Object.DestroyImmediate(camera.gameObject);
            Object.DestroyImmediate(sun.gameObject);
        }

        static void Shot(string name, double time, Vector3 eye, Vector3 target, float fieldOfView)
        {
            Pose(time);
            const int width = 1600, height = 900;
            camera.transform.position = frame.TransformPoint(eye);
            camera.transform.rotation = Quaternion.LookRotation(frame.TransformPoint(target) - frame.TransformPoint(eye), Vector3.up);
            camera.fieldOfView = fieldOfView;
            camera.aspect = (float)width / height;
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var before = RenderTexture.active;
            Texture2D picture = null;
            try
            {
                texture.Create();
                var request = new RenderPipeline.StandardRequest { destination = texture };
                if (GraphicsSettings.currentRenderPipeline != null && RenderPipeline.SupportsRenderRequest(camera, request))
                    RenderPipeline.SubmitRenderRequest(camera, request);
                else { camera.targetTexture = texture; camera.Render(); camera.targetTexture = null; }
                RenderTexture.active = texture;
                picture = new Texture2D(width, height, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                picture.Apply();
                File.WriteAllBytes(Path.Combine(Folder, name + ".png"), picture.EncodeToPNG());
                Log("      picture " + name + ".png (clock " + F(time) + " s)");
            }
            finally
            {
                RenderTexture.active = before;
                if (picture != null) Object.DestroyImmediate(picture);
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }
    }

    // The rampart in Play Mode for 13 s, offline (the obstacle clock is the scene's time): every mover woken kinematic and
    // where the clock puts it, the murder holes' floors and the ladders' past-upright marks switched on time, no errors.
    // Still nobody runs it.
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.RookRampartPlayCheck.RunPlayBatch -logFile <file>
    // Results: Logs/RookRampartValidation/play.txt.
    [InitializeOnLoad]
    public static class RookRampartPlayCheck
    {
        const string ActiveKey = "ChessFight.R860.Rampart.Play", ExitKey = "ChessFight.R860.Rampart.Exit", BadKey = "ChessFight.R860.Rampart.Bad";
        const string Folder = "Logs/RookRampartValidation", Report = "play.txt";
        const double Watch = 13.2, Limit = 300.0;

        static readonly List<string> errors = new List<string>();
        static readonly List<string> failures = new List<string>();
        static GameObject placed;
        static double began, placedAt;
        static int steps, ladderMismatch, holeMismatch;

        static RookRampartPlayCheck()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += Finish;
        }

        static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        static void Log(string value)
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Path.Combine(Folder, Report), value + "\n");
            if (value.Length > 0) Debug.Log("[RookRampart] " + value);
        }

        static void Check(string name, bool ok, string detail)
        {
            Log((ok ? "PASS  " : "FAIL  ") + name + ": " + detail);
            if (!ok) failures.Add(name);
        }

        public static void RunPlayBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, Report), "R860 rooks' rampart walk in Play (" +
                DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "), offline, no pawn.\n");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitKey, false);
            SessionState.SetInt(BadKey, 0);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                errors.Clear(); failures.Clear();
                placed = null; steps = ladderMismatch = holeMismatch = 0;
                began = EditorApplication.timeSinceStartup;
                Application.logMessageReceived += OnLog;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) Finish();
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (condition != null && condition.StartsWith("Host type is not matching any asset type") && condition.Contains(".urtshader")) return;
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) errors.Add(condition + "\n" + stack);
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup - began > Limit) throw new InvalidOperationException("ran over " + Limit + " s");
                if (placed == null)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RookRampartValidation.SectionPrefab);
                    placed = Object.Instantiate(prefab);
                    placedAt = Time.timeAsDouble;
                    return;
                }
                Sample();
                if (Time.timeAsDouble - placedAt < Watch) return;
                Judge();
            }
            catch (Exception e)
            {
                Log("FAIL  P0 run: " + e.GetType().Name + ": " + e.Message);
                failures.Add("P0");
            }
            Stop();
        }

        // Each frame: the ladders' past-upright marks and the murder holes' floors against the clock (one step's grace).
        static void Sample()
        {
            steps++;
            double now = ObstacleClock.Now;
            foreach (var ladder in placed.GetComponentsInChildren<SiegeLadder>())
            {
                var mark = ladder.GetComponentInChildren<NoClimbSurface>(true);
                bool past = ladder.Angle(now) > 90f, pastBefore = ladder.Angle(now - .05) > 90f;
                if (mark != null && mark.enabled != past && mark.enabled != pastBefore) ladderMismatch++;
            }
            var holes = placed.GetComponentInChildren<MurderHoles>();
            if (holes != null)
            {
                var floors = holes.GetComponentsInChildren<Collider>(true);
                int k = 0;
                for (int i = 0; i < holes.Across; i++)
                    for (int j = 0; j < holes.Rows; j++, k++)
                    {
                        if (k >= floors.Length) break;
                        bool shut = holes.State(i, j, now) != 2, shutBefore = holes.State(i, j, now - .05) != 2;
                        if (floors[k].enabled != shut && floors[k].enabled != shutBefore) holeMismatch++;
                    }
            }
        }

        static void Judge()
        {
            double now = ObstacleClock.Now;
            var frame = placed.transform;
            Log("");
            Log("== After " + F(Time.timeAsDouble - placedAt) + " s in Play (clock " + F(now) + " s, " + steps + " frames)");
            var movers = placed.GetComponentsInChildren<RampartMover>();
            int loose = 0;
            foreach (var m in movers) if (!m.GetComponent<Rigidbody>().isKinematic) loose++;
            // 6 ladders, 2 defenders, 2 towers and 2 bridges, 4 drill rooks, the rook and the knight, 2 banner lengths.
            Check("P1 every mover woken kinematic", movers.Length == 20 && loose == 0, movers.Length + " movers, " + loose + " not kinematic");
            // Where the movers are against where the clock puts them (interpolation and a step's lag allowed).
            var tower = placed.GetComponentsInChildren<SiegeTower>()[0];
            float tz = frame.InverseTransformPoint(tower.transform.position).z, want = tower.Z(now);
            Check("P2 siege tower A where the clock puts it", Mathf.Abs(tz - want) < .3f, "z " + F(tz) + " (clock " + F(want) + ")");
            var crane = placed.GetComponentInChildren<RookCrane>();
            crane.Crown(now, out float cy, out float cz);
            Vector3 cp = frame.InverseTransformPoint(crane.transform.position);
            Check("P3 the rook crane's crown where the clock puts it", Mathf.Abs(cp.y - cy) < .3f && Mathf.Abs(cp.z - cz) < .3f,
                  "(" + F(cp.y) + ", " + F(cp.z) + ") (clock " + F(cy) + ", " + F(cz) + ")");
            var knight = placed.GetComponentInChildren<KnightCrane>();
            knight.Pose(now, out float ky, out float kz, out float kf);
            Vector3 kp = frame.InverseTransformPoint(knight.transform.position);
            Check("P4 the knight where the clock puts it", Mathf.Abs(kp.y - ky) < .4f && Mathf.Abs(kp.z - kz) < .4f,
                  "(" + F(kp.y) + ", " + F(kp.z) + ") facing " + F(knight.transform.eulerAngles.y) + " (clock " + F(ky) + ", " + F(kz) + ", " + F(kf) + ")");
            var ladder = placed.GetComponentsInChildren<SiegeLadder>()[0];
            float tilt = Vector3.Angle(frame.up, ladder.transform.up), angle = ladder.Angle(now);
            Check("P5 ladder 1 at the clock's angle", Mathf.Abs(tilt - Mathf.Abs(90f - angle)) < 4f, "tilt from upright " + F(tilt) + " (clock angle " + F(angle) + ")");
            Check("P6 the ladders' past-upright marks on time", ladderMismatch == 0, ladderMismatch + " mismatches over " + steps + " frames");
            Check("P7 the murder holes' floors on time", holeMismatch == 0, holeMismatch + " mismatches over " + steps + " frames");
            foreach (var e in errors) Log("ERROR logged in Play: " + e.Split('\n')[0]);
            Check("P8 errors logged in Play", errors.Count == 0, errors.Count.ToString());
        }

        static void Stop()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            SessionState.SetInt(BadKey, failures.Count);
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(ExitKey, true);
            EditorApplication.isPlaying = false;
        }

        static void Finish()
        {
            SessionState.SetBool(ExitKey, false);
            int bad = SessionState.GetInt(BadKey, 1);
            Log(bad == 0 ? "ALL PASS" : bad + " FAILED");
            if (Application.isBatchMode) EditorApplication.Exit(bad == 0 ? 0 : 1);
        }
    }
}
