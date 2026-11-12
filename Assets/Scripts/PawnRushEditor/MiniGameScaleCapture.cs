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
    // R543 (user 11-11: the mini-games' scale goes with the team's size): pictures of every mini-game room at every
    // team size, to look at what the numbers build - the board, the towers, the statues, the king's road - without
    // playing ten rooms four times. In Play (the rooms' models are hung at Play) each room is built for 3, 4, 5 and 6
    // a side and photographed twice into Logs/MiniGameScale:
    //   <game>_<n>_top.png    straight down, the picture starting 11.6 m up (under the ceilings, over the 9 m towers)
    //   <game>_<n>_view.png   from high in the entry's corner, inside the room
    //   <game>_sheet.jpg      the four top views on one sheet: 3 a side top left, 4 top right, 5 bottom left, 6 bottom right
    // and a line a room says what stands in it (report.txt). An error logged while a room is built fails the run.
    //
    //   Unity.exe -batchmode -projectPath <project copy> -executeMethod ChessFight.PawnRush.Editor.MiniGameScaleCapture.RunBatch
    //             [-scaleOnly A,E,G] [-scaleSizes 3,6] [-scaleSeed 7]        (not -nographics: it renders; no -quit)
    //
    // An AI's look at the layout, not a person's playtest.
    [InitializeOnLoad]
    public static class MiniGameScaleCapture
    {
        const string ActiveKey = "ChessFight.MiniGameScaleCapture.Active", ExitKey = "ChessFight.MiniGameScaleCapture.Exit";
        const string CodeKey = "ChessFight.MiniGameScaleCapture.Code";
        const string Folder = "Logs/MiniGameScale";
        const float TopCut = 11.6f, Limit = 1500f;

        static readonly List<string> errors = new List<string>();
        static GameObject holder;
        static MiniGameSection current;
        static Camera camera;
        static int index, sizeAt, phase;
        static double phaseAt, beganAt, requestedAt, idleAt;
        static float fixedAt;
        static bool stopping;

        static MiniGameScaleCapture()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        [MenuItem("ChessFight/Development/Mini-game Scale Pictures (3 to 6 a side)", false, 320)]
        public static void RunBatch()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "report.txt"), "R543 mini-game rooms at 3 to 6 a side: what is built, pictures beside this file.\nNot a playtest.\n");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitKey, false);
            SessionState.SetInt(CodeKey, 0);
            requestedAt = idleAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= BeginWhenIdle;
            EditorApplication.update += BeginWhenIdle;
        }

        static void BeginWhenIdle()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - requestedAt > 90f)
            {
                EditorApplication.update -= BeginWhenIdle;
                SessionState.SetBool(ActiveKey, false);
                Log("FAIL: the editor did not settle before Play");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { idleAt = now; return; }
            if (now - idleAt < 2f) return;
            EditorApplication.update -= BeginWhenIdle;
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                index = sizeAt = phase = 0;
                holder = null; current = null; errors.Clear(); stopping = false;
                beganAt = EditorApplication.timeSinceStartup;
                Application.logMessageReceived += OnLog;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.72f, .74f, .78f);
                var light = new GameObject("Capture sun").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.6f;
                light.color = new Color(1f, .95f, .86f); light.shadows = LightShadows.None;
                light.transform.rotation = Quaternion.Euler(62f, -20f, 0f);
                camera = new GameObject("Capture camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.69f, .76f, .82f);
                camera.enabled = true;
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) CompleteExit();
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (condition != null && condition.StartsWith("Host type is not matching any asset type") && condition.Contains(".urtshader")) return;
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) errors.Add(condition + "\n" + stack);
        }

        static int[] Sizes()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-scaleSizes");
            if (at >= 0 && at + 1 < args.Length)
            {
                var list = args[at + 1].Split(',').Select(s => int.TryParse(s, out int n) ? n : 0).Where(n => n >= MiniGameScale.Min && n <= MiniGameScale.Max).ToArray();
                if (list.Length > 0) return list;
            }
            return Enumerable.Range(MiniGameScale.Min, MiniGameScale.Max - MiniGameScale.Min + 1).ToArray();
        }

        static bool Wanted(int game)
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-scaleOnly");
            if (at < 0 || at + 1 >= args.Length) return true;
            return args[at + 1].Split(',').Any(letter => PawnRushMissions.Ids[game].StartsWith(letter.Trim() + "_", StringComparison.OrdinalIgnoreCase));
        }

        static int Seed()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-scaleSeed");
            return at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], out int n) ? n : 542;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || stopping) return;
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            try
            {
                if (EditorApplication.timeSinceStartup - beganAt > Limit) throw new InvalidOperationException("the run exceeded " + Limit + " seconds");
                if (errors.Count > 0) throw new InvalidOperationException("an error was logged: " + errors[0]);
                var sizes = Sizes();
                if (phase == 0)
                {
                    while (index < MiniGamePrefabMenu.Games.Length && !Wanted(MiniGamePrefabMenu.Games[index])) index++;
                    if (index >= MiniGamePrefabMenu.Games.Length) { Stop(0); return; }
                    if (holder != null) Object.DestroyImmediate(holder);
                    int game = MiniGamePrefabMenu.Games[index], size = sizes[sizeAt];
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MiniGamePrefabMenu.PrefabPath(game));
                    if (prefab == null) throw new InvalidOperationException("no prefab for " + PawnRushMissions.Ids[game]);
                    holder = new GameObject("Scale " + PawnRushMissions.Ids[game] + " " + size);
                    holder.SetActive(false);
                    var instance = Object.Instantiate(prefab, holder.transform, false);
                    current = instance.GetComponent<MiniGameSection>();
                    instance.GetComponent<PawnRushSection>().Configure(1, Seed() + game, size, ObstacleClock.Now);
                    holder.SetActive(true);
                    phase = 1; phaseAt = EditorApplication.timeSinceStartup; fixedAt = Time.fixedTime;
                    return;
                }
                if (EditorApplication.timeSinceStartup - phaseAt < .5 || Time.fixedTime - fixedAt < .1f) return;
                {
                    int game = MiniGamePrefabMenu.Games[index], size = sizes[sizeAt];
                    string name = PawnRushMissions.Ids[game] + "_" + size;
                    Log(name + ": " + Describe(game));
                    var root = current.transform;
                    // straight down over the whole room (x -23..23, z -1..37), starting under the ceiling
                    Capture(name + "_top", root, new Vector3(0f, 40f, 18f), new Vector3(0f, 0f, 18f), Vector3.forward, true, 40f - TopCut);
                    // from high in the entry's corner, inside the walls
                    Capture(name + "_view", root, new Vector3(19.5f, 10.8f, 1.2f), new Vector3(-9f, 1.5f, 17f), Vector3.up, false, .3f);
                    // the same top view, half size, onto the game's sheet (a full run only: four sizes)
                    if (sizes.Length == 4)
                    {
                        if (sheet == null) sheet = new Texture2D(1536, 1024, TextureFormat.RGB24, false);
                        Capture(null, root, new Vector3(0f, 40f, 18f), new Vector3(0f, 0f, 18f), Vector3.forward, true, 40f - TopCut, sizeAt % 2 * 768, (1 - sizeAt / 2) * 512);
                        if (sizeAt == 3) File.WriteAllBytes(Path.Combine(Folder, PawnRushMissions.Ids[game] + "_sheet.jpg"), sheet.EncodeToJPG(82));
                    }
                    phase = 0;
                    if (++sizeAt >= sizes.Length) { sizeAt = 0; index++; }
                }
            }
            catch (Exception e)
            {
                Log("FAIL: " + e.Message);
                Stop(1);
            }
        }

        // What the room holds, in words (both stations build the same thing; white's is read).
        static string Describe(int game)
        {
            MiniGameBase g = null;
            foreach (var station in current.Stations)
                if (station != null && station.Team == Teams.White) g = station.Game;
            if (g == null) return "no game";
            switch (g)
            {
                case MgPaint paint: return "board " + PaintGrid(paint) + ", " + paint.Total + " squares a team";
                case MgPlanks planks: return planks.Layout.Rows + " rows of " + planks.Layout.Row.ToString("0.##") + " m, " + planks.Layout.PreLaidRows + " laid from the start, " + planks.Total + " planks to carry";
                case MgCapstan capstan: return capstan.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Bar") + " bars, " + capstan.TurnsToOpen + " turns";
                case MgBells bells: return bells.Total + " towers: " + string.Join(" ", bells.Towers.Select(t => "(" + t.x.ToString("0.#") + ", " + t.z.ToString("0.#") + ") " + t.y.ToString("0.#") + " m"));
                case MgEnPassant ep: return ep.Total + " statues in " + ep.Field.Rows + " rows";
                case MgKnightPads pads: return pads.Road.Squares.Length + " squares of " + pads.Road.SquareSize + " m (" + string.Join(" ", pads.Road.Squares) + "), " + pads.Total + " pieces";
                case MgKingEscort escort: return "seed " + escort.Seed + ": " + string.Join(" ", escort.Road.Cells) + " | sand " + string.Join(" ", escort.Road.Sand) + " | rooks " + string.Join(" ", escort.Road.RookCells) + " -> " + string.Join(" ", escort.Road.RookGarages);
                case MgCheckmate hunt: return hunt.Total + " kings";
                case MgCheckBlock block: return "seed " + block.Seed + ": " + block.Total + " checks to answer";
                case MgDrawbridge _: return "a puller counts " + MiniGameScale.RopeForce(current.GetComponent<PawnRushSection>().TeamSize).ToString("0.##");
            }
            return g.GetType().Name;
        }

        static string PaintGrid(MgPaint paint)
        {
            var board = paint.GetComponentInParent<MiniGameSection>().GetComponentInChildren<PaintBoard>(true);
            return board != null ? board.Grid.Files + " x " + board.Grid.Ranks : "?";
        }

        static Texture2D sheet;

        // A picture of its own (`name`), or - without a name - a half-size one into the sheet at (sheetX, sheetY).
        static void Capture(string name, Transform level, Vector3 eye, Vector3 target, Vector3 up, bool flat, float near, int sheetX = 0, int sheetY = 0)
        {
            camera.transform.position = level.TransformPoint(eye);
            camera.transform.LookAt(level.TransformPoint(target), level.TransformDirection(up));
            camera.orthographic = flat;
            camera.orthographicSize = 20f;
            camera.fieldOfView = 62f;
            camera.aspect = 1.5f;
            camera.nearClipPlane = near; camera.farClipPlane = 120f;
            var texture = name != null ? new RenderTexture(1536, 1024, 24, RenderTextureFormat.ARGB32) : new RenderTexture(768, 512, 24, RenderTextureFormat.ARGB32);
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
                if (name == null)
                {
                    sheet.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), sheetX, sheetY);
                    sheet.Apply();
                    return;
                }
                picture = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                picture.Apply();
                File.WriteAllBytes(Path.Combine(Folder, name + ".png"), picture.EncodeToPNG());
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
            File.AppendAllText(Path.Combine(Folder, "report.txt"), value + "\n");
            Debug.Log("[MiniGameScale] " + value);
        }

        static void Stop(int code)
        {
            stopping = true;
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick;
            Log("Result: " + (code == 0 ? "PASS" : "FAIL"));
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
