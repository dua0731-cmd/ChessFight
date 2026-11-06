using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush.Editor
{
    // R312: inspects real saved assets and their loaded lifecycle. ForceComplete proves completion
    // wiring, not that a player can solve every game or that online replication is connected.
    [InitializeOnLoad]
    public static class MiniGamePrefabValidation
    {
        const string ActiveKey = "ChessFight.R312.MiniGames.ValidatePlay";
        const string ExitKey = "ChessFight.R312.MiniGames.ValidateExit";
        const string CodeKey = "ChessFight.R312.MiniGames.ValidateCode";
        // Unity deletes Temp on normal batch exit, so evidence belongs in its ignored Logs folder.
        const string Folder = "Logs/MiniGameValidation";
        static readonly List<string> errors = new List<string>();
        static GameObject holder;
        static MiniGameSection current;
        static Camera captureCamera;
        static int index, phase, kingRenderers;
        static double phaseAt, beganAt, playRequestedAt, editorIdleAt;
        static float fixedAt;
        static bool stopping;

        static MiniGamePrefabValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        [MenuItem("ChessFight/Pawn Rush/Validate Mini Game Prefabs", false, 27)]
        public static void ValidateMenu()
        {
            var issues = CheckSavedPrefabs();
            Debug.Log("[MiniGameValidation] Saved prefab result: " + (issues.Count == 0 ? "PASS" : "FAIL")
                + ". Details: " + Path.GetFullPath(Path.Combine(Folder, "saved-prefabs.txt")));
        }

        public static void RunBatch()
        {
            var issues = CheckSavedPrefabs();
            if (Application.isBatchMode) EditorApplication.Exit(issues.Count == 0 ? 0 : 1);
        }

        static List<string> CheckSavedPrefabs()
        {
            Directory.CreateDirectory(Folder);
            var issues = new List<string>();
            var report = new List<string>
            {
                "R312 saved prefab inspection",
                "Persistent asset references are checked before loading a preview scene and running ExecuteAlways bindings.",
                "Not player-input, Steam, or visual-quality validation."
            };
            var identifiers = new HashSet<string>();
            foreach (int game in MiniGamePrefabMenu.Games)
            {
                string path = MiniGamePrefabMenu.PrefabPath(game);
                GameObject root = null;
                int before = issues.Count;
                try
                {
                    var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Need(saved != null, "Missing prefab: " + path, issues);
                    if (saved == null) continue;
                    // The saved fallback font/material must be persistent. Loading prefab contents runs
                    // PalaceTextBinding, which deliberately replaces them with a live OS-font atlas.
                    CheckRenderAssets(saved, path + " saved asset", true, issues);
                    root = PrefabUtility.LoadPrefabContents(path);
                    var level = root.GetComponent<MiniGameSection>();
                    var section = root.GetComponent<PawnRushSection>();
                    Need(level != null && section != null, path + ": missing section components", issues);
                    if (level == null || section == null) continue;
                    Need(level.Kit != null && level.HasContent, path + ": missing kit or baked hierarchy", issues);
                    Need(level.GameIndex == game, path + ": wrong fixed game", issues);
                    Need(section.Kind == PawnRushSection.SlotKind.MiniGame, path + ": not a mini-game section", issues);
                    Need(identifiers.Add(section.Id), path + ": duplicate section id", issues);
                    Need(section.EntryLocal.position.sqrMagnitude < .0001f
                        && (section.ExitLocal.position - new Vector3(0, 0, 36)).sqrMagnitude < .0001f,
                        path + ": entry/exit does not match the 36m connection", issues);
                    var stations = root.GetComponentsInChildren<MissionStation>(true);
                    Need(stations.Length == 2 && stations.Count(s => s.Team == Teams.White) == 1
                        && stations.Count(s => s.Team == Teams.Black) == 1, path + ": expected one station per team", issues);
                    foreach (var station in stations)
                        Need(station.Kit != null && station.Socket != null && station.Gate != null
                            && station.Game != null && station.GameIndex == game, path + ": incomplete station wiring", issues);
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        Need(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0,
                            path + ": missing script at " + t.name, issues);
                    CheckRenderAssets(root, path + " loaded preview", false, issues);
                    if (game == PawnRushMissions.Paint)
                    {
                        var boards = root.GetComponentsInChildren<PaintBoard>(true);
                        Need(boards.Length == 1, path + ": A must contain one shared board", issues);
                        if (boards.Length == 1)
                        {
                            Need(boards[0].GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Square ")) == 64,
                                path + ": A must contain 64 square cells", issues);
                            Need((root.transform.InverseTransformPoint(boards[0].transform.position) - new Vector3(0f, .4f, 4f)).sqrMagnitude < .0001f,
                                path + ": A board is not centred between its stations", issues);
                        }
                    }
                    Physics.SyncTransforms();
                    Need(FloorBelow(root.transform, Vector3.zero), path + ": no entry floor", issues);
                    Need(FloorBelow(root.transform, new Vector3(0, 0, 35.8f)), path + ": no exit floor", issues);
                    foreach (float x in new[] { -12f, 12f })
                        Need(ClearPortal(root.transform, x), path + ": solid geometry blocks team passage x=" + x, issues);
                    report.Add(path + ": " + (issues.Count == before ? "PASS" : "FAIL")
                        + ", renderers=" + root.GetComponentsInChildren<Renderer>(true).Length);
                }
                catch (Exception e) { issues.Add(path + ": " + e.GetType().Name + " " + e.Message); }
                finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
            }
            var set = AssetDatabase.LoadAssetAtPath<PawnRushSectionSet>(SectionPrefabMenu.SectionSet);
            Need(set != null && set.SlotCount == 7, "Missing seven-slot section set", issues);
            if (set != null && set.SlotCount >= 6)
                foreach (var entry in new[] { (slot: 1, count: 3), (slot: 3, count: 4), (slot: 5, count: 3) })
                {
                    var list = set[entry.slot]?.prefabs;
                    int zone = entry.slot / 2;
                    var owned = MiniGamePrefabMenu.Games.Where(g => MiniGamePrefabMenu.Zone(g) == zone)
                        .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(MiniGamePrefabMenu.PrefabPath(g))).ToArray();
                    Need(owned.Length == entry.count && list != null && owned.All(p => p != null && list.Contains(p)),
                        "Missing generated mini-game choices at slot " + (entry.slot + 1), issues);
                }
            CheckTestScene(issues, report);
            report.AddRange(issues.Select(e => "FAIL: " + e));
            report.Add("Saved asset result: " + (issues.Count == 0 ? "PASS" : "FAIL") + ", failures=" + issues.Count);
            File.WriteAllLines(Path.Combine(Folder, "saved-prefabs.txt"), report);
            Debug.Log("[MiniGameValidation] " + report[report.Count - 1] + ". Report: "
                + Path.GetFullPath(Path.Combine(Folder, "saved-prefabs.txt")));
            foreach (var issue in issues) Debug.LogError("[MiniGameValidation] " + issue);
            return issues;
        }

        static void Need(bool condition, string message, List<string> issues)
        {
            if (!condition) issues.Add(message);
        }

        static void CheckRenderAssets(GameObject root, string context, bool requirePersistent, List<string> issues)
        {
            foreach (var mesh in root.GetComponentsInChildren<MeshFilter>(true))
                Need(mesh.sharedMesh != null && (!requirePersistent || EditorUtility.IsPersistent(mesh.sharedMesh)),
                    context + ": transient or missing mesh at " + mesh.name, issues);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                Need(materials.Length > 0, context + ": no material at " + renderer.name, issues);
                foreach (var material in materials)
                    Need(material != null && material.shader != null
                        && (!requirePersistent || EditorUtility.IsPersistent(material)),
                        context + ": transient or missing material at " + renderer.name, issues);
            }
            foreach (var label in root.GetComponentsInChildren<TextMesh>(true))
                Need(label.font != null && (!requirePersistent || EditorUtility.IsPersistent(label.font)),
                    context + ": transient or missing font at " + label.name, issues);
        }

        static void CheckTestScene(List<string> issues, List<string> report)
        {
            string path = MiniGamePrefabMenu.TestScene;
            int before = issues.Count;
            var scene = default(UnityEngine.SceneManagement.Scene);
            try
            {
                Need(AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null, "Missing test scene: " + path, issues);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) return;
                // A preview scene leaves the user's open scenes and unsaved edits untouched.
                scene = EditorSceneManager.OpenPreviewScene(path);
                var roots = scene.GetRootGameObjects();
                foreach (var t in roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
                    Need(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0,
                        path + ": missing script at " + t.name, issues);
                var viewers = roots.SelectMany(r => r.GetComponentsInChildren<MiniGamePrefabTest>(true)).ToArray();
                var spawners = roots.SelectMany(r => r.GetComponentsInChildren<PlaytestSpawner>(true)).ToArray();
                Need(viewers.Length == 1 && spawners.Length == 1,
                    path + ": expected one mini-game selector and one player spawner", issues);
                if (viewers.Length != 1 || spawners.Length != 1) return;
                var viewer = new SerializedObject(viewers[0]);
                var prefabs = viewer.FindProperty("prefabs");
                Need(prefabs != null && prefabs.arraySize == MiniGamePrefabMenu.Games.Length,
                    path + ": selector does not contain all ten saved prefabs", issues);
                if (prefabs != null && prefabs.arraySize == MiniGamePrefabMenu.Games.Length)
                    for (int i = 0; i < MiniGamePrefabMenu.Games.Length; i++)
                    {
                        var expected = AssetDatabase.LoadAssetAtPath<GameObject>(
                            MiniGamePrefabMenu.PrefabPath(MiniGamePrefabMenu.Games[i]));
                        Need(expected != null && prefabs.GetArrayElementAtIndex(i).objectReferenceValue == expected,
                            path + ": wrong or missing selector prefab at key " + (i == 9 ? 0 : i + 1), issues);
                    }
                var spawner = spawners[0];
                Need(viewer.FindProperty("spawner")?.objectReferenceValue == spawner,
                    path + ": selector is not wired to its scene player spawner", issues);
                Need(spawner.CharacterPrefab != null && EditorUtility.IsPersistent(spawner.CharacterPrefab)
                    && spawner.CharacterPrefab.GetComponentInChildren<ICharacterDriver>(true) != null,
                    path + ": missing persistent character prefab or character driver", issues);
                Need(spawner.Orbit != null && spawner.Orbit.gameObject.scene == scene
                    && spawner.Orbit.GetComponent<Camera>() != null,
                    path + ": missing scene orbit camera or Camera component", issues);
                Need(spawner.enabled && viewers[0].enabled && spawner.gameObject.activeInHierarchy,
                    path + ": test selector or player spawner is disabled", issues);
            }
            catch (Exception e) { issues.Add(path + ": " + e.GetType().Name + " " + e.Message); }
            finally
            {
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                report.Add(path + ": " + (issues.Count == before ? "PASS" : "FAIL")
                    + ", serialized selector/player/camera references only; Start and keyboard input not exercised");
            }
        }

        static bool FloorBelow(Transform root, Vector3 local)
        {
            var ray = new Ray(root.TransformPoint(local + Vector3.up * 2f), -root.up);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                if (collider.enabled && !collider.isTrigger && collider.Raycast(ray, out var hit, 3f)
                    && Vector3.Dot(hit.normal, root.up) > .7f) return true;
            return false;
        }

        static bool ClearPortal(Transform root, float x)
        {
            var ray = new Ray(root.TransformPoint(new Vector3(x, 1.3f, 24.15f)), root.forward);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled || collider.isTrigger || collider.GetComponentInParent<TeamGate>() != null
                    || collider.GetComponentInParent<TeamBarrier>() != null) continue;
                if (collider.Raycast(ray, out _, 5.7f)) return false;
            }
            return true;
        }

        // Do not pass -quit: the method exits after Play Mode and its domain-reload cleanup.
        public static void RunPlayBatch()
        {
            if (CheckSavedPrefabs().Count != 0) { if (Application.isBatchMode) EditorApplication.Exit(1); return; }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "runtime.txt"),
                "R312 runtime smoke: loaded baked prefabs, completion wiring, reset, seed rebuild, duplicate A ownership.\nNot player-input or multiplayer validation.\n");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitKey, false);
            SessionState.SetInt(CodeKey, 0);
            // Batch executeMethod can run before the Editor's delayed startup callbacks. Let
            // import/search initialization settle before the Play Mode domain reload begins.
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
                index = phase = 0;
                holder = null; current = null; errors.Clear(); stopping = false;
                beganAt = EditorApplication.timeSinceStartup;
                Application.logMessageReceived += OnLog;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.65f, .69f, .76f);
                var light = new GameObject("Validation sun").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.8f;
                light.color = new Color(1f, .94f, .84f); light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(45f, -25f, 0f);
                captureCamera = new GameObject("Validation camera").AddComponent<Camera>();
                captureCamera.clearFlags = CameraClearFlags.SolidColor;
                captureCamera.backgroundColor = new Color(.69f, .76f, .82f);
                captureCamera.nearClipPlane = .1f; captureCamera.farClipPlane = 250f;
                // An active camera initializes SRP in the otherwise empty batch scene before render requests.
                captureCamera.enabled = true;
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) CompleteExit();
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                errors.Add(condition + "\n" + stack);
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || stopping) return;
            try
            {
                if (EditorApplication.timeSinceStartup - beganAt > 180f)
                    throw new InvalidOperationException("Runtime smoke exceeded 180 seconds");
                if (errors.Count > 0) throw new InvalidOperationException("Runtime error was logged: " + errors[0]);
                if (phase == 0)
                {
                    if (index >= MiniGamePrefabMenu.Games.Length) { StopPlay(0); return; }
                    if (holder != null) Object.DestroyImmediate(holder);
                    int game = MiniGamePrefabMenu.Games[index];
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MiniGamePrefabMenu.PrefabPath(game));
                    holder = new GameObject("Validation instance " + game);
                    holder.SetActive(false);
                    holder.transform.SetPositionAndRotation(new Vector3(3f, 0f, -5f), Quaternion.Euler(0f, 37f, 0f));
                    var instance = Object.Instantiate(prefab, holder.transform, false);
                    current = instance.GetComponent<MiniGameSection>();
                    instance.GetComponent<PawnRushSection>().Configure(1, 1200 + game, 6, ObstacleClock.Now);
                    holder.SetActive(true);
                    Log("Runtime " + (index + 1) + "/10: " + instance.name);
                    Next(1); return;
                }
                if (EditorApplication.timeSinceStartup - phaseAt < .32 || Time.fixedTime - fixedAt < .06f) return;
                if (phase == 1)
                {
                    CheckRuntimeWiring();
                    if (current.GameIndex == PawnRushMissions.Paint) CheckDuplicatePaint();
                    if (current.GameIndex == PawnRushMissions.CheckBlock) kingRenderers = KingRendererCount();
                    if (current.GameIndex == PawnRushMissions.Drawbridge || current.GameIndex == PawnRushMissions.Bells
                        || current.GameIndex == PawnRushMissions.KingEscort) Capture(current);
                    current.Stations.Single(s => s.Team == Teams.White).Game.ForceComplete();
                    Next(2); return;
                }
                if (phase == 2)
                {
                    Require(current.Stations.Single(s => s.Team == Teams.White).Gate.IsOpen, "White completion did not open white gate");
                    Require(!current.Stations.Single(s => s.Team == Teams.Black).Gate.IsOpen, "White completion opened black gate");
                    current.Stations.Single(s => s.Team == Teams.Black).Game.ForceComplete();
                    Next(3); return;
                }
                if (phase == 3)
                {
                    Require(current.Stations.All(s => s.Gate.IsOpen), "Both completions did not open both gates");
                    current.ResetGames(); Next(4); return;
                }
                if (phase == 4) { CheckReset(); current.ResetGames(); Next(5); return; }
                if (phase == 5)
                {
                    CheckReset();
                    if (current.GameIndex == PawnRushMissions.CheckBlock)
                        Require(KingRendererCount() >= kingRenderers && kingRenderers > 0,
                            "I lost king/bed dressing after repeated reset");
                    var section = current.GetComponent<PawnRushSection>();
                    section.Configure(section.Slot, section.Seed + 1, section.TeamSize, ObstacleClock.Now);
                    Next(6); return;
                }
                CheckRuntimeWiring(); CheckReset();
                Log("PASS: loaded runtime caches, independent gates, repeated reset and seed rebuild");
                index++; phase = 0;
            }
            catch (Exception e)
            {
                Log("FAIL: " + e);
                foreach (var error in errors) Log("Runtime log: " + error);
                StopPlay(1);
            }
        }

        static void Next(int value)
        {
            phase = value; phaseAt = EditorApplication.timeSinceStartup; fixedAt = Time.fixedTime;
        }
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void CheckRuntimeWiring()
        {
            Require(current != null && current.HasContent && current.Stations.Length == 2, "Runtime section not built");
            foreach (var station in current.Stations)
            {
                Require(station != null && station.Game != null && station.Game.isActiveAndEnabled && station.Gate != null,
                    "Missing active game or gate");
                foreach (var body in station.GetComponentsInChildren<Rigidbody>(true))
                    Require(Finite(body.position), "Non-finite game body position");
            }
            Require(current.GetComponentsInChildren<Transform>(true).Count(t => t.name == MiniGamePalaceArt.ShellName) == 1,
                "Static shell duplicated or missing");
        }

        static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x)
            && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);

        static void CheckReset()
        {
            Require(current.Stations.All(s => !s.Gate.IsOpen && !s.Game.Completed && s.Game.Progress01 < .001f),
                "Reset did not close gates and clear progress");
        }

        static int KingRendererCount()
        {
            var king = current.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "King base");
            return king != null ? king.GetComponentsInChildren<Renderer>(true).Length : 0;
        }

        static void CheckDuplicatePaint()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MiniGamePrefabMenu.PrefabPath(PawnRushMissions.Paint));
            var other = Object.Instantiate(prefab, new Vector3(500f, 0f, 0f), Quaternion.identity);
            try
            {
                Require(current.GetComponentsInChildren<PaintBoard>(true).Length == 1
                    && other.GetComponentsInChildren<PaintBoard>(true).Length == 1,
                    "Two A prefab instances reused one shared board across section owners");
                Require(current.GetComponentInChildren<PaintBoard>(true) != other.GetComponentInChildren<PaintBoard>(true),
                    "Duplicate A shared board is not isolated");
            }
            finally { Object.DestroyImmediate(other); }
        }

        static void Capture(MiniGameSection level)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Log("NOT RUN: visual capture unavailable with the null graphics device"); return;
            }
            foreach (string view in new[] { "entry", "overview" })
            {
                Vector3 eye = view == "entry" ? new Vector3(0f, 4f, 1f) : new Vector3(-12f, 38f, -26f);
                captureCamera.transform.position = level.transform.TransformPoint(eye);
                captureCamera.transform.LookAt(level.transform.TransformPoint(view == "entry"
                    ? new Vector3(0f, 3.5f, 22f) : new Vector3(0f, 3f, 17f)), level.transform.up);
                captureCamera.fieldOfView = view == "entry" ? 82f : 48f;
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
                        Require(RenderPipeline.SupportsRenderRequest(captureCamera, request),
                            "URP does not support this capture request");
                        RenderPipeline.SubmitRenderRequest(captureCamera, request);
                    }
                    else { captureCamera.targetTexture = texture; captureCamera.Render(); captureCamera.targetTexture = null; }
                    RenderTexture.active = texture;
                    picture = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                    picture.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                    picture.Apply();
                    string path = Path.Combine(Folder, PawnRushMissions.Ids[level.GameIndex] + "_" + view + ".png");
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
        }

        static void Log(string value)
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Path.Combine(Folder, "runtime.txt"), value + "\n");
            Debug.Log("[MiniGameValidation] " + value);
        }

        static void StopPlay(int code)
        {
            stopping = true;
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick;
            Log("Runtime result: " + (code == 0 ? "PASS" : "FAIL"));
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
