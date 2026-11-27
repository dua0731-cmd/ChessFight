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
    // Checks for the rooks' rampart walk's grey box (룩 성벽길, R860; R866 at 1.5 times the size, with floors checked where
    // the inner wall meets the gatehouse), in Edit mode only: the prefab built by RookRampartBuilder
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
            File.WriteAllText(Path.Combine(Folder, Report), "R866 rooks' rampart walk checks (" +
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
            // Ladder 1 (pushed at 0 s) leans on the wall at 4 s; ladder 4 lies on the mound then. Tower A is still docked at
            // the outer wall (it rolls from 4 s); the rays at its ladders come in at its open back.
            float lad = T.Ladders[0], bz = (B.BannerZ0 + B.BannerZ1) * .5f, back = B.SiegeZ0 - B.SiegeW * .5f - .2f;
            Face("H1 a siege ladder (1, against the wall, from the mound)", V(lad, B.Mound + 2.2f, B.LadderFootZ - 1.2f), Vector3.forward, true);
            Face("H2 siege tower A's first ladder (docked)", V(B.SiegeX[0] - 1.5f, B.Floors[0] + 1.4f, back), Vector3.forward, true);
            Face("H4 the banner's lower length", V(B.GateHX0 - 3.5f, B.W3 + 1.4f, bz), Vector3.right, true);
            Face("H5 the banner's upper length (from the ledge)", V(B.GateHX0 - 1f, B.LedgeY + 1.2f, bz), Vector3.right, true);
            Face("N1 the outer wall's face (over the mound)", V(-20f, 3f, B.Face1 - 4f), Vector3.forward, false);
            Face("N2 the middle wall's face (from the lower ward)", V(-25f, 4f, B.Wall2Face(-25f) - 3f), Vector3.forward, false);
            Face("N3 the inner wall's face (from the ditch)", V(-36f, 0f, B.Wall3Face(-36f) - 4f), Vector3.forward, false);
            Face("N4 the gatehouse's west face beside the banner", V(B.GateHX0 - 2f, B.W3 + 1.2f, B.BannerZ1 + 1.2f), Vector3.right, false);
            Face("N5 a siege tower's side hide", V(B.SiegeX[0] - 3.5f, B.Floors[0] + 1.5f, B.SiegeZ0), Vector3.right, false);
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
            TopOut("T1 a siege ladder onto its crenel's sill", V(lad, B.Mound + B.LadderClimb + .3f, B.LadderFootZ - 1.2f), Vector3.forward, B.W1 + B.SillH, .2f);
            TopOut("T2 siege tower A's first ladder onto its second floor", V(B.SiegeX[0] - 1.5f, B.Floors[1] + .25f, back), Vector3.forward, B.Floors[1], .2f);
            TopOut("T3 siege tower A's second ladder onto its third floor", V(B.SiegeX[0] + 1.5f, B.Floors[2] + .25f, back), Vector3.forward, B.Floors[2], .2f);
            TopOut("T4 siege tower A's third ladder onto its top floor", V(B.SiegeX[0] - 1.5f, B.Floors[3] + .25f, back), Vector3.forward, B.Floors[3], .2f);
            TopOut("T5 the banner's lower length onto the ledge", V(B.GateHX0 - 3.5f, B.LedgeY - .3f, bz), Vector3.right, B.LedgeY, .2f);
            TopOut("T6 the banner's upper length onto the roof", V(B.GateHX0 - 1f, B.GateY - .3f, bz), Vector3.right, B.GateY, .2f);

            Log("");
            Log("== Floors (a ray down: what is under the way where a wall meets the gatehouse, R866)");
            Under("F1 the inner wall's walk at the gatehouse's foot, under the banner's north end", V(B.GateHX0 - .3f, B.W3 + 3f, B.BannerZ1), B.W3);
            Under("F2 the inner wall's walk at the gatehouse's east corner", V(B.GateHX1 + .3f, B.W3 + 3f, B.BannerZ1), B.W3);

            Log("");
            Log("== Moves (the clock's functions at their marked times)");
            Check("M1 ladder 1 leaning at 4 s, upright about 0.98 s, down at 1.9 s", Mathf.Abs(T.LadderAngle(0, 4.0) - 75f) < .01f
                  && Mathf.Abs(T.LadderAngle(0, .98) - 90f) < 2f && Mathf.Abs(T.LadderAngle(0, 1.9) - 180f) < .01f,
                  F(T.LadderAngle(0, 4.0)) + " / " + F(T.LadderAngle(0, .98)) + " / " + F(T.LadderAngle(0, 1.9)));
            T.Siege(18.0, 0f, B.SiegeZ0, B.SiegeZ1(0), out float zA, out float bA);
            Check("M2 siege tower A at the middle wall, its bridge down, at 18 s", Mathf.Abs(zA - B.SiegeZ1(0)) < .01f && bA < .01f, "z " + F(zA) + ", bridge " + F(bA));
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
            foreach (var k in frame.GetComponentsInChildren<KingsBanner>(true)) k.PoseFlutter(time);
            Physics.SyncTransforms();
        }

        // What a ray straight down from `from` lands on: the floor at `expect` (not a crack to the ground below).
        static void Under(string name, Vector3 from, float expect)
        {
            Vector3 o = frame.TransformPoint(from);
            bool hit = Physics.Raycast(o, Vector3.down, out var floor, 40f, ~0, QueryTriggerInteraction.Ignore);
            float at = hit ? frame.InverseTransformPoint(floor.point).y : float.NaN;
            Check(name, hit && Mathf.Abs(at - expect) < .1f, hit ? floor.collider.name + " at y " + F(at) : "nothing under " + Vs(from));
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
            // R866: every eye and target 1.5 times as far out as R860's (the plan is), the heights with the walls.
            Shot("rr_01_overview", 13.75, V(87f, 93f, -57f), V(-3f, 9f, 72f), 50f);
            Shot("rr_02_from_the_glacis", 1.2, V(3f, 1.7f, -4.5f), V(-13.5f, 6f, 16.5f), 62f);
            Shot("rr_03_siege_ladders", 1.2, V(-4.5f, 11f, -3f), V(-16.5f, 4.5f, 16.5f), 60f);
            Shot("rr_04_murder_holes", .8, V(7.5f, 22.5f, 9.75f), V(7.5f, 5.25f, 22.5f), 60f);
            Shot("rr_05_siege_towers", 18.0, V(7.5f, 19.5f, 31.5f), V(24f, 9.75f, 51f), 66f);
            Shot("rr_06_drill_square", 7.6, V(0f, 57f, 45f), V(0f, 13.5f, 70.5f), 55f);
            Shot("rr_07_cranes", 13.75, V(-55.5f, 33f, 76.5f), V(-40.5f, 18f, 93f), 60f);
            Shot("rr_08_banner_calm", 4.5, V(-33f, 27f, 99f), V(-15f, 25.5f, 111f), 55f);
            Shot("rr_09_banner_gust", 10.0, V(-33f, 27f, 99f), V(-15f, 25.5f, 111f), 55f);
            Shot("rr_10_from_the_inner_wall", 13.75, V(-36f, 24.75f, 108f), V(0f, 15f, 69f), 70f);
            // A pawn's eye on the outer wall's walk (the scale the user asked for: the walk 9 m wide, the wall 6 m high).
            Shot("rr_11_on_the_outer_walk", 4.0, V(-30f, B.W1 + 1.6f, B.Face1 + 5f), V(-6f, B.W1 + 1f, B.Face1 + 6f), 70f);
            // The banner close up from the walk below it, calm and at the gust's height.
            Shot("rr_12_banner_from_the_walk_calm", 4.5, V(B.GateHX0 - 11f, B.W3 + 1.6f, B.BannerZ0 - 1f), V(B.GateHX0, B.LedgeY, (B.BannerZ0 + B.BannerZ1) * .5f), 60f);
            Shot("rr_13_banner_from_the_walk_gust", 10.0, V(B.GateHX0 - 11f, B.W3 + 1.6f, B.BannerZ0 - 1f), V(B.GateHX0, B.LedgeY, (B.BannerZ0 + B.BannerZ1) * .5f), 60f);
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
    // R866: stand-ins for climbers (ShakeProbe: a body that can be driven and hit, riding what it is put on) on ladder 1's
    // climbing side and on the banner's lower length - each must be let go once a push or gust, and only past upright or
    // at 35 degrees - and one waiting on the walk under the banner, which must not be. Still nobody runs it.
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
        static ShakeProbe onLadder, onBanner, besideOnLedge, besideOnWalk;

        // A stand-in for a climber: RampartShake looks for a character (ICharacterDriver) that can be hit (IHitReceiver);
        // this one only notes when it is told to let go.
        sealed class ShakeProbe : MonoBehaviour, ICharacterDriver, IHitReceiver
        {
            public readonly List<double> Drops = new List<double>();
            public void SetCommand(in CharacterCommand command) { }
            public Transform FollowTarget => transform;
            public void Teleport(Vector3 position, Quaternion rotation) { }
            public void ApplyHit(Vector3 push, float knockdownSeconds, float staminaDamage, bool dropFromWallOrRide)
            {
                if (dropFromWallOrRide) Drops.Add(ObstacleClock.Now);
            }
        }

        static ShakeProbe Probe(string name, Transform parent, Vector3 localPosition, Vector3 size)
        {
            var go = new GameObject("R866 stand-in " + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<BoxCollider>().size = size;
            return go.AddComponent<ShakeProbe>();
        }

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
            File.WriteAllText(Path.Combine(Folder, Report), "R866 rooks' rampart walk in Play (" +
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
                onLadder = onBanner = besideOnLedge = besideOnWalk = null;
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
                    // Ladder 1's climbers hug its -z side half way up; the banner's hug its west (-x) face. The one waiting
                    // stands on the walk 1.2 m out from the lower length (the cloth lifts over it).
                    var ladder = placed.GetComponentsInChildren<SiegeLadder>()[0];
                    onLadder = Probe("on ladder 1", ladder.transform, new Vector3(0f, B.LadderL * .5f, -.35f), new Vector3(.4f, 1f, .3f));
                    KingsBanner lower = null;
                    foreach (var k in placed.GetComponentsInChildren<KingsBanner>())
                        if (lower == null || k.transform.position.y < lower.transform.position.y) lower = k;
                    onBanner = Probe("on the banner", lower.transform, new Vector3(-.35f, -2f, 0f), new Vector3(.3f, 1f, .4f));
                    // Waiting beside the banner (the design's rule): on the ledge 1.5 m north of it, and on the walk there.
                    float beside = B.BannerZ1 + 1.2f;
                    besideOnLedge = Probe("beside the banner on the ledge", placed.transform,
                                          new Vector3(B.GateHX0 - .6f, B.LedgeY + .91f, beside), new Vector3(.4f, 1.8f, .4f));
                    besideOnWalk = Probe("beside the banner on the walk", placed.transform,
                                         new Vector3(B.GateHX0 - B.LedgeDepth - .6f, B.W3 + .91f, beside), new Vector3(.4f, 1.8f, .4f));
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
            // The stand-ins (R866): let go once a push or a gust, and only past upright or at 35 degrees; beside the banner, never.
            Func<double, float> gust = t => T.BannerAngle(t);
            Check("P9 a stand-in on ladder 1 let go past upright, once a push", Dropped(onLadder, ladder.Angle, a => a > 90f && a < T.Fallen, T.PushAt[0], T.PushCycle),
                  Drops(onLadder, ladder.Angle));
            Check("P10 a stand-in on the banner's lower length thrown off at 35 degrees or more, once a gust", Dropped(onBanner, gust, a => a >= 34.9f, 0f, T.GustCycle),
                  Drops(onBanner, gust));
            Check("P11 a stand-in waiting beside the banner on the ledge left alone", besideOnLedge.Drops.Count == 0, Drops(besideOnLedge, gust));
            Check("P12 a stand-in waiting beside the banner on the walk left alone", besideOnWalk.Drops.Count == 0, Drops(besideOnWalk, gust));
            foreach (var e in errors) Log("ERROR logged in Play: " + e.Split('\n')[0]);
            Check("P8 errors logged in Play", errors.Count == 0, errors.Count.ToString());
        }

        // At least one drop, each where `ok` says of the angle then, and no two in the same cycle (`start` + k `cycle`).
        static bool Dropped(ShakeProbe probe, Func<double, float> angle, Func<float, bool> ok, float start, float cycle)
        {
            var cycles = new HashSet<long>();
            foreach (var t in probe.Drops)
                if (!ok(angle(t)) || !cycles.Add((long)Math.Floor((t - start) / cycle))) return false;
            return probe.Drops.Count > 0;
        }

        static string Drops(ShakeProbe probe, Func<double, float> angle)
        {
            var parts = new List<string>();
            foreach (var t in probe.Drops) parts.Add("clock " + F(t) + " s at " + F(angle(t)) + " deg");
            return probe.Drops.Count + " let go" + (parts.Count > 0 ? ": " + string.Join(", ", parts) : "");
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
