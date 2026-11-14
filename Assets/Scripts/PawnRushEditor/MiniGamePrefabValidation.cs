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
        const float RuntimeLimit = 1200f;   // s for the whole Play Mode run (a hang guard, not a speed target)
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

        [MenuItem("ChessFight/Pawn Rush/Validate/Mini Game Prefabs", false, 201)]
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
                            // R543: the board goes with the team's size (6 x 6 to 10 x 8); a saved prefab holds one of them.
                            int cells = boards[0].GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Square "));
                            Need(Enumerable.Range(MiniGameScale.Min, MiniGameScale.Max - MiniGameScale.Min + 1).Any(n => MiniGameScale.PaintBoard(n).Count == cells),
                                path + ": A must contain a board of 36, 48, 64 or 80 square cells, not " + cells, issues);
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
            // R516: the render pipeline's editor package logs this about its own ray-tracing shader asset the first time a
            // capture asks for a render request in batch mode (Unity 6000.3, every run since): an editor asset message, not
            // the game's. The picture is taken all the same.
            if (condition != null && condition.StartsWith("Host type is not matching any asset type") && condition.Contains(".urtshader")) return;
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                errors.Add(condition + "\n" + stack);
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || stopping) return;
            // R516: an error the editor itself logs in Play (the render pipeline's ".urtshader" message, see OnLog) pauses
            // Play Mode when the Console's Error Pause is on; the run then waited for game time that never came (11-09:
            // after room C's captures and at room F's start, 1200 s each).
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            try
            {
                // R516: 180 s was enough for the R312 rooms; with the rooms' Blender art each first render in batch mode
                // compiles its shaders, and the pad captures render every room (the first room alone took over 180 s).
                if (EditorApplication.timeSinceStartup - beganAt > RuntimeLimit)
                    throw new InvalidOperationException("Runtime smoke exceeded " + RuntimeLimit + " seconds");
                if (errors.Count > 0) throw new InvalidOperationException("Runtime error was logged: " + errors[0]);
                if (phase == 0)
                {
                    while (index < MiniGamePrefabMenu.Games.Length && !Wanted(MiniGamePrefabMenu.Games[index])) index++;
                    if (index >= MiniGamePrefabMenu.Games.Length) { StopPlay(0); return; }
                    if (holder != null) Object.DestroyImmediate(holder);
                    int game = MiniGamePrefabMenu.Games[index];
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MiniGamePrefabMenu.PrefabPath(game));
                    holder = new GameObject("Validation instance " + game);
                    holder.SetActive(false);
                    holder.transform.SetPositionAndRotation(new Vector3(3f, 0f, -5f), Quaternion.Euler(0f, 37f, 0f));
                    var instance = Object.Instantiate(prefab, holder.transform, false);
                    current = instance.GetComponent<MiniGameSection>();
                    // R543: the games are checked at the size they were made for (five a side); -miniGameTeamSize 3..6 another
                    instance.GetComponent<PawnRushSection>().Configure(1, 1200 + game, RuntimeTeamSize(), ObstacleClock.Now);
                    holder.SetActive(true);
                    Log("Runtime " + (index + 1) + "/10: " + instance.name);
                    Next(1); return;
                }
                if (EditorApplication.timeSinceStartup - phaseAt < .32 || Time.fixedTime - fixedAt < .06f) return;
                if (phase == 1)
                {
                    CheckRuntimeWiring();
                    CheckPads();   // R516: the promotion pads are back in the palace sections
                    CheckDirections();
                    if (current.GameIndex == PawnRushMissions.Checkmate) CheckHuntFeedback();
                    CapturePads(current);
                    if (current.GameIndex == PawnRushMissions.Paint) CheckDuplicatePaint();
                    if (current.GameIndex == PawnRushMissions.EnPassant) CheckDancers();
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
                    var entry = current.transform.TransformPoint(Vector3.forward * 2f);
                    Require(current.DirectionHint(Teams.White, entry).Contains("왼쪽 출구"), "Open gate guidance missed white's exit");
                    Require(!current.DirectionHint(Teams.Black, entry).Contains("열렸"), "Black was directed to an open exit before completion");
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

        // R516: -miniGameOnly G,H,I runs just those rooms (the letters of PawnRushMissions.Ids); without it, all ten.
        static string[] onlyRooms;

        static bool Wanted(int game)
        {
            if (onlyRooms == null)
            {
                var args = Environment.GetCommandLineArgs();
                int at = Array.IndexOf(args, "-miniGameOnly");
                onlyRooms = at >= 0 && at + 1 < args.Length
                    ? args[at + 1].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray()
                    : new string[0];
            }
            if (onlyRooms.Length == 0) return true;
            string id = PawnRushMissions.Ids[game];
            return onlyRooms.Any(r => id.Equals(r, StringComparison.OrdinalIgnoreCase)
                                      || id.StartsWith(r + "_", StringComparison.OrdinalIgnoreCase));
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

        // R543: the team size the runtime check builds the games for (the command line's -miniGameTeamSize, else five).
        static int RuntimeTeamSize()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-miniGameTeamSize");
            return at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], out int n) ? MiniGameScale.Clamp(n) : MiniGameScale.Baseline;
        }

        static void CheckDirections()
        {
            var entry = current.transform.TransformPoint(new Vector3(0f, 1f, -5f));
            var beyond = current.transform.TransformPoint(Vector3.forward * 35f);
            foreach (var station in current.Stations)
            {
                int team = station.Team;
                bool white = team == Teams.White;
                bool shared = current.GameIndex == PawnRushMissions.Paint;
                string entrance = current.DirectionHint(team, entry);
                Require(entrance != null && entrance.Contains(shared ? (white ? "밝은 칸" : "어두운 칸") : (white ? "왼쪽" : "오른쪽")), "Incorrect team direction at entrance");
                Require(current.DirectionHint(team, beyond) == null, "Direction persisted beyond the exit");
                var labels = station.GetComponentsInChildren<TextMesh>().Where(t => t.GetComponent<Renderer>().enabled).ToArray();
                Require(labels.Any(t => t.name == "Team entrance label" && t.text.StartsWith(Teams.Name(team)))
                        && labels.Any(t => t.name == "Team exit label" && t.text == Teams.Name(team) + "팀 출구"), "Missing team plaques");
                var otherSide = current.transform.TransformPoint(new Vector3(white ? 12f : -12f, 1f, 12f));
                Require(shared || current.DirectionHint(team, otherSide).Contains("상대 진영"), "No correction in the other team's lane");
            }
        }

        static void CheckHuntFeedback()
        {
            var game = (MgCheckmate)current.Stations[0].Game;
            var hunt = new CheckmateHunt(game.Total);
            var data = new byte[PawnRushNetState.BlockBytes];
            void Apply()
            {
                var block = new NetBlock(data, 0, data.Length);
                foreach (var king in hunt.All) block.U16(CheckmateHunt.Pack(king));
                block.U8(hunt.Captured);
                block.At = 0;
                game.ReadNet(ref block);
            }
            hunt.All[0].State = CheckmateHunt.KingState.Captured;
            Apply();
            Require(game.Done == 1 && !game.HuntHint.Contains("포획"), "First snapshot replayed an old capture");
            hunt.All[1].State = CheckmateHunt.KingState.Frozen;
            Apply();
            Require(game.HuntHint.Contains("다이빙"), "Frozen king has no capture prompt");
            hunt.All[1].State = CheckmateHunt.KingState.Captured;
            Apply();
            Apply();
            Require(game.Done == 2 && game.HuntHint.Contains("2 / " + game.Total), "Capture feedback repeated or lost the tally");
            game.ResetGame();
            Require(game.Done == 0 && !game.HuntHint.Contains("포획"), "Capture feedback survived round reset");
        }

        // R520: in the ballroom (F) every statue shows its doll and nothing else - the team band ring, the capture halo and
        // the doll with its key draw; the game's pawn and R312's code costume do not (in Play they stayed on, 11-09) - and
        // each has its MiniGameDancerLook (the doll's glow in the capture window).
        static void CheckDancers()
        {
            int statues = 0;
            foreach (var station in current.Stations)
                foreach (var t in station.Game.GetComponentsInChildren<Transform>(true))
                {
                    var look = t.name.StartsWith("Statue ") ? t.Find("Look") : null;
                    if (look == null) continue;
                    statues++;
                    var stray = look.GetComponentsInChildren<Renderer>(true)
                        .Where(r => r.enabled && r.name != "Ring" && !(r is LineRenderer)
                                    && !r.GetComponentsInParent<Transform>(true).Any(p => p.parent == look && (p.name == "MODEL_Ride" || p.name == "MODEL_Key")))
                        .Select(r => r.name).Distinct().ToList();
                    Require(stray.Count == 0, "Ballroom statue shows more than its doll: " + station.name + " " + t.name + " (" + string.Join(", ", stray) + ")");
                    Require(look.Find("MODEL_Ride") != null && t.GetComponent<MiniGameDancerLook>() != null, "Ballroom statue has no doll: " + station.name + " " + t.name);
                }
            int expected = 2 * EnPassantField.For(current.GetComponent<PawnRushSection>().TeamSize).Count;
            Require(statues == expected, "Ballroom: " + statues + " statues, expected " + expected);
            Log("Ballroom dancers: all " + statues + " statues show only their dolls");
        }

        // R516: each station's promotion pad is there, lies on the dais (a ray from above lands on the floor it is drawn on),
        // and nothing solid stands in the room a pawn needs on it. Pictures (no collider) are left to the captures.
        static void CheckPads()
        {
            var up = current.transform.up;
            foreach (var station in current.Stations)
            {
                var pad = station.Promotion;
                Require(pad != null, "No promotion pad: " + station.name);
                Vector3 at = pad.transform.position;
                bool floor = Physics.Raycast(at + up * 1.5f, -up, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore);
                float off = floor ? Vector3.Dot(hit.point - at, up) : float.NaN;
                Require(floor && Mathf.Abs(off) < .06f, "Promotion pad not on the dais: " + station.name
                    + (floor ? $" ({off:F2} m from {hit.collider.name})" : " (no floor under it)"));
                var solid = Physics.OverlapBox(at + up * 1.1f, new Vector3(StationPromotion.Half * .9f, .9f, StationPromotion.Half * .9f),
                    pad.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
                Require(solid.Length == 0, "Something solid over the promotion pad: " + station.name + " ("
                    + string.Join(", ", solid.Select(c => c.name)) + ")");
                Log($"Promotion pad {station.name}: on {hit.collider.name} ({off:F3} m), nothing solid over it");
            }
        }

        // Each pad from inside the room - over the entry band, halfway to the middle, above a pawn's eye - so a person can
        // see what stands round it. (6.5 m south of the pad was outside the entry wall: the indoor rooms showed its back.)
        static void CapturePads(MiniGameSection level)
        {
            foreach (var station in level.Stations)
            {
                var pad = station.Promotion;
                if (pad == null) continue;
                Vector3 local = level.transform.InverseTransformPoint(pad.transform.position);
                string name = PawnRushMissions.Ids[level.GameIndex] + "_pad_" + (station.Team == Teams.White ? "white" : "black");
                CaptureView(level, name, new Vector3(local.x * .5f, local.y + 4.6f, 2.5f), local + new Vector3(0f, .5f, .3f), 55f);
            }
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
            string id = PawnRushMissions.Ids[level.GameIndex];
            CaptureView(level, id + "_entry", new Vector3(0f, 4f, 1f), new Vector3(0f, 3.5f, 22f), 82f);
            CaptureView(level, id + "_overview", new Vector3(-12f, 38f, -26f), new Vector3(0f, 3f, 17f), 48f);
        }

        // One picture of the section from `eye` toward `target` (both in the section's frame) into the validation folder.
        static void CaptureView(MiniGameSection level, string name, Vector3 eye, Vector3 target, float fieldOfView)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Log("NOT RUN: visual capture unavailable with the null graphics device"); return;
            }
            // R516: -miniGameNoCapture runs the checks without pictures (11-09 in batch mode the editor stopped advancing
            // frames after the first room's captures, the rooms' art in).
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-miniGameNoCapture") >= 0)
            {
                Log("NOT RUN: capture " + name + " (-miniGameNoCapture)"); return;
            }
            captureCamera.transform.position = level.transform.TransformPoint(eye);
            captureCamera.transform.LookAt(level.transform.TransformPoint(target), level.transform.up);
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
                    Require(RenderPipeline.SupportsRenderRequest(captureCamera, request),
                        "URP does not support this capture request");
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
