using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ChessFight.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush.Editor
{
    // R660: the Royal Carriage Yard run by test bots (the user: "사본과 시험 봇" - a copy of the project the AI runs the game
    // in itself, and bots that run the course). In Play Mode the map is built as the map test scene builds it and twelve
    // real ragdoll pawns (the RagdollPawn prefab, Pawn Rush movement) run
    //   zone 1   from the start court through the outer gate, across the yard of horse-drawn carriages between the sheds,
    //            to the canal's entry landing (map z 52)
    //   zone 2   from the canal's far landing across the loading and sorting yard - the crane lines and the two giant
    //            chests - to the mill basin's edge (map z 115.2)
    // several times each on lanes drawn from a seed, and every hit a yard hazard gives them is counted (YardKit.HitGiven):
    // knocked down or staggered, by which hazard; how long each crossing took; and anything that looks wrong - a pawn
    // pressing on without moving for 4 s (stuck), its hips over 14 m/s or 3.2 m up (flung), in the water.
    //
    // The bots: four run, four sprint, four sprint and jump just before a giant chest lands within reach of its wave (the
    // way the shockwave is meant to be dodged). None of them reads a carriage's line or a crate's disc, shoves, or rides
    // the lifting wheel: they are the blind runners the models assumed, now with the real physics (bodies, pushes, the
    // pawns' own bumping), not people. Pictures from above go to Logs/CarriageYardBots with the report and every hit.
    //
    // Batch (on a copy of the project, so the user's Editor can keep the original open; do not pass -quit, it exits after
    // Play Mode; with a graphics device for the pictures):
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.CarriageYardBotTest.RunPlayBatch
    //         [-yardBotRuns 4] [-yardBotZone 1|2] [-yardBotSeed 7] [-yardBotNoCapture] -logFile <file>
    [InitializeOnLoad]
    public static class CarriageYardBotTest
    {
        const string ActiveKey = "ChessFight.R660.YardBots.Play";
        const string ExitKey = "ChessFight.R660.YardBots.Exit";
        const string CodeKey = "ChessFight.R660.YardBots.Code";
        const string Folder = "Logs/CarriageYardBots";
        const string Report = "report.txt", HitsFile = "hits.csv", RunsFile = "crossings.csv";
        const string MapPath = "Assets/Prefabs/PawnRushMaps/RoyalCarriageYard.prefab";
        const string PawnPath = "Assets/Prefabs/RagdollPawn.prefab";
        const int BotCount = 12;
        const float RuntimeLimit = 1800f;
        // Map frame (the map at the origin): zone 1's finish line on the entry landing, zone 2's at the basin's edge.
        const float Zone1Finish = 52f, Zone2Finish = 115.2f, Zone1Limit = 55f, Zone2Limit = 45f;

        enum Policy { Run, Sprint, Jumper }

        sealed class Bot
        {
            public int Index, Team;
            public Policy Policy;
            public GameObject Body;
            public ICharacterDriver Driver;
            public Rigidbody Hips;
            public readonly List<Vector3> Route = new List<Vector3>();
            public int Next, Downs, Staggers;
            public bool Running, Done, Fell, Stuck, Flung;
            public float Started, Finished, LastHit = -99f, CheckTime, MaxSpeed, MaxRise, Lane;
            public Vector3 CheckAt;
            public double JumpedFor = double.NaN;
            public readonly Dictionary<string, int> DownsBy = new Dictionary<string, int>();
        }

        static readonly List<Bot> bots = new List<Bot>();
        static readonly List<string> errors = new List<string>();
        static readonly List<string> anomalies = new List<string>();
        static readonly StringBuilder hits = new StringBuilder(), crossings = new StringBuilder();
        static readonly Queue<Action> pictures = new Queue<Action>();
        static readonly Dictionary<int, Dictionary<string, int>> downsByZone = new Dictionary<int, Dictionary<string, int>>();
        static readonly Dictionary<int, List<float[]>> results = new Dictionary<int, List<float[]>>();
        static GameObject map;
        static Camera captureCamera;
        static SlamChest[] chests = new SlamChest[0];
        static int phase, zone, run, runs, seed, onlyZone;
        static float runBegan;
        static double beganAt, playRequestedAt, editorIdleAt;
        static bool stopping;

        static CarriageYardBotTest()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        [MenuItem("ChessFight/Pawn Rush/Validate/Carriage Yard Bots (Play)", false, 205)]
        public static void RunMenu() => RunPlayBatch();

        public static void RunPlayBatch()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, Report),
                "Carriage yard bot test (R660): twelve ragdoll pawns run zone 1 and zone 2 of the Royal Carriage Yard in Play Mode.\n" +
                "Blind runners (and jumpers for the giant chests): not people, not online.\n");
            File.WriteAllText(Path.Combine(Folder, HitsFile), "zone,run,bot,policy,t,kind,knockdown_s,push,x,y,z\n");
            File.WriteAllText(Path.Combine(Folder, RunsFile), "zone,run,bot,policy,lane,finished,seconds,downs,staggers,fell,stuck,flung,max_speed,max_rise\n");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitKey, false);
            SessionState.SetInt(CodeKey, 0);
            playRequestedAt = editorIdleAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= BeginPlayWhenIdle;
            EditorApplication.update += BeginPlayWhenIdle;
        }

        static void BeginPlayWhenIdle()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - playRequestedAt > 120f)
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

        static int Arg(string name, int fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], out int value) ? value : fallback;
        }

        static bool NoCapture => Array.IndexOf(Environment.GetCommandLineArgs(), "-yardBotNoCapture") >= 0;

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                runs = Mathf.Clamp(Arg("-yardBotRuns", 4), 1, 20);
                onlyZone = Arg("-yardBotZone", 0);
                seed = Arg("-yardBotSeed", 7);
                phase = 0; zone = onlyZone == 2 ? 2 : 1; run = 0;
                bots.Clear(); errors.Clear(); anomalies.Clear(); pictures.Clear(); hits.Clear(); crossings.Clear();
                downsByZone.Clear(); results.Clear();
                stopping = false;
                beganAt = EditorApplication.timeSinceStartup;
                Application.logMessageReceived += OnLog;
                YardKit.HitGiven += OnHit;
                KillVolume.Entered += OnFall;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) CompleteExit();
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            // The render pipeline's editor package about its own shader on the first capture in batch mode (R516): not the game's.
            if (condition != null && condition.StartsWith("Host type is not matching any asset type") && condition.Contains(".urtshader")) return;
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                errors.Add(condition + "\n" + stack);
        }

        // ------------------------------------------------------------------ the run

        static void Tick()
        {
            if (!EditorApplication.isPlaying || stopping) return;
            // R516: the Editor's Error Pause stops Play on a render pipeline message that is not the game's.
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            try
            {
                if (EditorApplication.timeSinceStartup - beganAt > RuntimeLimit)
                    throw new InvalidOperationException("the bot test exceeded " + RuntimeLimit + " seconds");
                if (pictures.Count > 0) { pictures.Dequeue()(); return; }
                float now = Time.time;
                switch (phase)
                {
                    case 0:
                        Build();
                        phase = 1; runBegan = now;
                        return;
                    case 1:
                        // The map's own start: the outer gate opens 3 s after it is built and takes 2.5 s; the bots set off
                        // as it starts to open (zone 1) or straight away (zone 2 alone).
                        if (now - runBegan < (zone == 1 ? 2.6f : 1f)) return;
                        StartRun(now);
                        phase = 2;
                        return;
                    case 2:
                        Drive(now);
                        Watch(now);
                        if (bots.All(b => b.Done) || now - runBegan > (zone == 1 ? Zone1Limit : Zone2Limit))
                        {
                            EndRun(now);
                            run++;
                            if (run >= runs)
                            {
                                run = 0;
                                zone++;
                                if (onlyZone != 0 || zone > 2) { phase = 3; return; }
                            }
                            Place(zone);
                            phase = 1; runBegan = now;
                        }
                        return;
                    case 3:
                        Finish();
                        return;
                }
            }
            catch (Exception e)
            {
                Log("FAIL: " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
                StopPlay(1);
            }
        }

        static void Build()
        {
            CourseSkills.PawnRush = true;
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            DynamicGI.UpdateEnvironment();
            var sun = new GameObject("Bot test sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f; sun.color = Color.white; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.sun = sun;
            captureCamera = new GameObject("Bot test camera").AddComponent<Camera>();
            captureCamera.clearFlags = CameraClearFlags.Skybox;
            captureCamera.nearClipPlane = .1f; captureCamera.farClipPlane = 500f;
            captureCamera.gameObject.AddComponent<AudioListener>();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapPath);
            if (prefab == null) throw new InvalidOperationException("no map prefab at " + MapPath);
            map = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            map.name = "Royal Carriage Yard (bot test)";
            chests = map.GetComponentsInChildren<SlamChest>(true);
            Log("Map built: " + chests.Length + " giant chests, " + map.GetComponentsInChildren<RoamingCarriage>(true).Length + " roaming bodies, "
                + map.GetComponentsInChildren<CraneCrate>(true).Length + " crane hooks, " + map.GetComponentsInChildren<VerticalWaterwheel>(true).Length + " lifting wheel(s)");

            var pawn = AssetDatabase.LoadAssetAtPath<GameObject>(PawnPath);
            if (pawn == null) throw new InvalidOperationException("no pawn prefab at " + PawnPath);
            for (int i = 0; i < BotCount; i++)
            {
                var body = Object.Instantiate(pawn, new Vector3(0f, 0f, -12f), Quaternion.identity);
                body.name = "Bot " + (i + 1);
                var bot = new Bot
                {
                    Index = i, Team = i % 2 == 0 ? Teams.White : Teams.Black, Body = body,
                    Driver = body.GetComponentInChildren<ICharacterDriver>(),
                    Policy = i < 4 ? Policy.Run : i < 8 ? Policy.Sprint : Policy.Jumper,
                };
                if (bot.Driver == null) throw new InvalidOperationException("the pawn prefab has no ICharacterDriver");
                if (bot.Driver is ITeamAssignable member) member.AssignTeam(bot.Team);
                bot.Hips = bot.Driver.FollowTarget != null ? bot.Driver.FollowTarget.GetComponent<Rigidbody>() : null;
                bots.Add(bot);
            }
            Place(zone);
        }

        // Every bot to its start: zone 1 the start court's twelve places (both teams side by side, as the map has them),
        // zone 2 the canal's far landing in four columns of three.
        static void Place(int z)
        {
            var starts = map.GetComponentsInChildren<SpawnPoint>(true).OrderBy(p => p.Team).ThenBy(p => p.Index).ToArray();
            var rng = new System.Random(seed * 7919 + z * 131 + run * 17);
            foreach (var bot in bots)
            {
                Vector3 at;
                if (z == 1 && starts.Length >= BotCount) at = starts[bot.Index].transform.position;
                else if (z == 1) at = new Vector3(-4.5f + (bot.Index % 4) * 3f, 0f, -7f - (bot.Index / 4) * 2.5f);
                else at = new Vector3(-6f + (bot.Index % 4) * 4f, 0f, 75.5f + (bot.Index / 4) * 2f);
                bot.Driver.Teleport(at, Quaternion.identity);
                bot.Driver.SetCommand(default);
                bot.Running = bot.Done = bot.Fell = bot.Stuck = bot.Flung = false;
                bot.Next = bot.Downs = bot.Staggers = 0;
                bot.MaxSpeed = bot.MaxRise = 0f;
                bot.JumpedFor = double.NaN;
                bot.DownsBy.Clear();
                bot.Route.Clear();
                if (z == 1)
                {
                    bot.Lane = (float)(rng.NextDouble() * 28.0 - 14.0);
                    float gate = Mathf.Clamp(bot.Lane * .15f, -1.8f, 1.8f), landing = bot.Lane < 0f ? -4f : 4f;
                    bot.Route.Add(new Vector3(gate, 0f, -1.5f));
                    bot.Route.Add(new Vector3(gate, 0f, 2.5f));
                    bot.Route.Add(new Vector3(bot.Lane, 0f, 9f));
                    bot.Route.Add(new Vector3(bot.Lane, 0f, 40f));
                    bot.Route.Add(new Vector3(landing, 0f, 48.5f));
                    bot.Route.Add(new Vector3(landing, 0f, Zone1Finish + 2f));
                }
                else
                {
                    bot.Lane = (float)(rng.NextDouble() * 42.0 - 21.0);
                    bot.Route.Add(new Vector3(Mathf.Clamp(bot.Lane, -8f, 8f), 0f, 83f));
                    bot.Route.Add(new Vector3(bot.Lane, 0f, 89f));
                    bot.Route.Add(new Vector3(bot.Lane, 0f, Zone2Finish + 1f));
                }
            }
        }

        static void StartRun(float now)
        {
            foreach (var bot in bots)
            {
                bot.Running = true;
                bot.Started = now;
                bot.CheckTime = now;
                bot.CheckAt = bot.Hips != null ? bot.Hips.position : Vector3.zero;
            }
            Log("Zone " + zone + " run " + (run + 1) + " started at " + now.ToString("0.0") + " s (clock " + ObstacleClock.Now.ToString("0.0") + ")");
            if (run == 0) SchedulePictures();
        }

        static void Drive(float now)
        {
            foreach (var bot in bots)
            {
                if (!bot.Running || bot.Done || bot.Hips == null) continue;
                var p = bot.Hips.position;
                while (bot.Next < bot.Route.Count - 1 && Flat(bot.Route[bot.Next] - p).magnitude < 1.5f) bot.Next++;
                var dir = Flat(bot.Route[bot.Next] - p);
                if (dir.sqrMagnitude > 1e-4f) dir.Normalize();
                bool jump = bot.Policy == Policy.Jumper && JumpForSlam(bot, p);
                bot.Driver.SetCommand(new CharacterCommand { Move = dir, Sprint = bot.Policy != Policy.Run, Jump = jump, Aim = dir });
                if (p.z >= (zone == 1 ? Zone1Finish : Zone2Finish))
                {
                    bot.Done = true;
                    bot.Finished = now;
                    bot.Driver.SetCommand(default);
                }
            }
        }

        // A jumper leaves the floor ~0.22 s before a giant chest lands within its wave's reach, once a slam.
        static bool JumpForSlam(Bot bot, Vector3 p)
        {
            foreach (var chest in chests)
            {
                if (chest == null || !chest.NextLanding(out var at, out float t)) continue;
                if (t > .26f || t < .12f || Flat(at - p).magnitude > 7.6f) continue;
                double key = Math.Round(ObstacleClock.Now + t, 1) + chest.GetInstanceID() * 1e-3;
                if (!double.IsNaN(bot.JumpedFor) && Math.Abs(bot.JumpedFor - key) < .5) return false;
                bot.JumpedFor = key;
                return true;
            }
            return false;
        }

        // Stuck, flung, the highest and fastest: watched while a bot runs.
        static void Watch(float now)
        {
            foreach (var bot in bots)
            {
                if (!bot.Running || bot.Done || bot.Hips == null) continue;
                var p = bot.Hips.position;
                var v = bot.Hips.linearVelocity;
                bot.MaxSpeed = Mathf.Max(bot.MaxSpeed, v.magnitude);
                bot.MaxRise = Mathf.Max(bot.MaxRise, p.y);
                if (!bot.Flung && (v.magnitude > 14f || p.y > 3.2f))
                {
                    bot.Flung = true;
                    Anomaly(bot, "flung: " + v.magnitude.ToString("0.0") + " m/s, hips " + p.y.ToString("0.00") + " m up");
                }
                if (now - bot.CheckTime >= 4f)
                {
                    if (!bot.Stuck && Flat(p - bot.CheckAt).magnitude < .4f && now - bot.LastHit > 2.5f)
                    {
                        bot.Stuck = true;
                        Anomaly(bot, "stuck 4 s pressing on, near " + Near(p));
                    }
                    bot.CheckAt = p;
                    bot.CheckTime = now;
                }
            }
        }

        static void EndRun(float now)
        {
            foreach (var bot in bots)
            {
                bool finished = bot.Done && !bot.Fell;
                float seconds = finished ? bot.Finished - bot.Started : float.NaN;
                if (!results.TryGetValue(zone, out var list)) results[zone] = list = new List<float[]>();
                list.Add(new[] { finished ? 1f : 0f, seconds, bot.Downs, bot.Staggers, bot.Fell ? 1f : 0f, bot.Stuck ? 1f : 0f, bot.Flung ? 1f : 0f, (float)bot.Policy });
                crossings.Append(string.Join(",", zone, run + 1, bot.Index + 1, bot.Policy, F(bot.Lane), finished ? 1 : 0, finished ? F(seconds) : "",
                                             bot.Downs, bot.Staggers, bot.Fell ? 1 : 0, bot.Stuck ? 1 : 0, bot.Flung ? 1 : 0, F(bot.MaxSpeed), F(bot.MaxRise))).Append('\n');
                bot.Running = false;
                bot.Driver.SetCommand(default);
            }
            File.AppendAllText(Path.Combine(Folder, RunsFile), crossings.ToString());
            crossings.Clear();
            File.AppendAllText(Path.Combine(Folder, HitsFile), hits.ToString());
            hits.Clear();
            int done = bots.Count(b => b.Done && !b.Fell);
            Log("Zone " + zone + " run " + (run + 1) + ": " + done + " of " + bots.Count + " across, knockdowns " + bots.Sum(b => b.Downs)
                + ", staggers " + bots.Sum(b => b.Staggers) + ", knocked at least once " + bots.Count(b => b.Downs > 0));
        }

        static void OnHit(ICharacterDriver driver, Component source, Vector3 push, float knockdown)
        {
            var bot = bots.FirstOrDefault(b => b.Driver == driver);
            if (bot == null || !bot.Running || bot.Done) return;
            string kind = Kind(source);
            float now = Time.time;
            bot.LastHit = now;
            if (knockdown > 0f)
            {
                bot.Downs++;
                bot.DownsBy[kind] = bot.DownsBy.TryGetValue(kind, out int n) ? n + 1 : 1;
                if (!downsByZone.TryGetValue(zone, out var by)) downsByZone[zone] = by = new Dictionary<string, int>();
                by[kind] = by.TryGetValue(kind, out int m) ? m + 1 : 1;
            }
            else bot.Staggers++;
            var p = bot.Hips != null ? bot.Hips.position : Vector3.zero;
            hits.Append(string.Join(",", zone, run + 1, bot.Index + 1, bot.Policy, F(now - bot.Started), kind, F(knockdown), F(push.magnitude), F(p.x), F(p.y), F(p.z))).Append('\n');
        }

        static void OnFall(ICharacterDriver driver, KillVolume volume)
        {
            var bot = bots.FirstOrDefault(b => b.Driver == driver);
            if (bot == null || !bot.Running || bot.Done) return;
            bot.Fell = true;
            bot.Done = true;
            bot.Finished = Time.time;
            Anomaly(bot, "fell into " + volume.name);
            // Out of the water and out of the way until the next run (a callback: no teleport here, absolute rule 14).
            EditorApplication.delayCall += () => { if (bot.Driver != null) bot.Driver.Teleport(new Vector3(-9f + bot.Index, 0f, -14f), Quaternion.identity); };
        }

        static string Kind(Component source)
        {
            if (source is SlamChest) return "giant chest wave";
            var obstacle = source != null ? source.GetComponentInParent<Obstacle>() : null;
            switch (obstacle)
            {
                case SlamChest _: return "giant chest (under it)";
                case CraneCrate _: return "crane crate";
                case RoamingCarriage rc: return rc.Kind == RoamingCarriage.Look.Team ? "horse-drawn carriage" : "roaming " + rc.Kind;
                case StableCharger _: return "stable charger";
                case HorizontalWaterwheel _: return "giant waterwheel";
                case null: return source != null ? source.name : "?";
                default: return obstacle.GetType().Name;
            }
        }

        static void Anomaly(Bot bot, string what)
        {
            var p = bot.Hips != null ? bot.Hips.position : Vector3.zero;
            string line = "zone " + zone + " run " + (run + 1) + " bot " + (bot.Index + 1) + " (" + bot.Policy + ") at " + V(p) + ": " + what;
            anomalies.Add(line);
            Log("ANOMALY " + line);
        }

        // What solid things are within 1.5 m (for a stuck bot): what it may be caught on.
        static string Near(Vector3 p)
        {
            var names = Physics.OverlapSphere(p, 1.5f, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => c.GetComponentInParent<ICharacterDriver>() == null).Select(c => c.name).Distinct().Take(5).ToArray();
            return names.Length > 0 ? string.Join(", ", names) : "nothing solid (other pawns?)";
        }

        // ------------------------------------------------------------------ pictures

        static void SchedulePictures()
        {
            if (NoCapture) return;
            if (zone == 1)
            {
                Later(6.5f, () => Picture("zone1_overview", new Vector3(0f, 34f, -14f), new Vector3(0f, 0f, 24f), 60f));
                Later(8f, () => Picture("zone1_top", new Vector3(0f, 62f, 26f), new Vector3(0f, 0f, 26.01f), 52f));
                Later(10f, () => Picture("zone1_low", new Vector3(-16f, 6f, 6f), new Vector3(4f, 1f, 28f), 62f));
                Later(13f, () => Picture("zone1_shed_side", new Vector3(10f, 5f, 12f), new Vector3(-19f, 1.5f, 30f), 60f));
            }
            else
            {
                Later(3.5f, () => Picture("zone2_overview", new Vector3(0f, 30f, 70f), new Vector3(0f, 0f, 104f), 64f));
                Later(5.5f, () => Picture("zone2_top", new Vector3(0f, 64f, 100f), new Vector3(0f, 0f, 100.01f), 54f));
                Later(8f, () => Picture("zone2_low", new Vector3(-14f, 5f, 86f), new Vector3(4f, 1.5f, 108f), 62f));
                // Clear of the second giant chest's gantry (its beam 11 m up at map z 104.3 - 106.7).
                Later(11f, () => Picture("zone2_basin", new Vector3(0f, 7.6f, 110.4f), new Vector3(0f, 4.2f, 122.5f), 70f));
            }
        }

        static void Later(float afterRunStart, Action shot)
        {
            float at = runBegan + afterRunStart;
            EditorApplication.CallbackFunction wait = null;
            wait = () =>
            {
                if (!EditorApplication.isPlaying) { EditorApplication.update -= wait; return; }
                if (Time.time < at) return;
                EditorApplication.update -= wait;
                pictures.Enqueue(shot);
            };
            EditorApplication.update += wait;
        }

        static void Picture(string name, Vector3 eye, Vector3 target, float fieldOfView)
        {
            if (NoCapture || captureCamera == null) return;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Log("NOT RUN: capture " + name + " (null graphics device)"); return; }
            const int width = 1600, height = 1000;
            captureCamera.transform.position = eye;
            var look = target - eye;
            captureCamera.transform.rotation = Quaternion.LookRotation(look, Mathf.Abs(Vector3.Dot(look.normalized, Vector3.up)) > .99f ? Vector3.forward : Vector3.up);
            captureCamera.orthographic = false;
            captureCamera.fieldOfView = fieldOfView;
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
                    if (!RenderPipeline.SupportsRenderRequest(captureCamera, request)) { Log("NOT RUN: capture " + name + " (render request unsupported)"); return; }
                    RenderPipeline.SubmitRenderRequest(captureCamera, request);
                }
                else { captureCamera.targetTexture = texture; captureCamera.Render(); captureCamera.targetTexture = null; }
                RenderTexture.active = texture;
                picture = new Texture2D(width, height, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                picture.Apply();
                string path = Path.Combine(Folder, name + ".png");
                File.WriteAllBytes(path, picture.EncodeToPNG());
                Log("Captured " + path + " at run time " + (Time.time - runBegan).ToString("0.0") + " s");
            }
            finally
            {
                RenderTexture.active = before;
                if (picture != null) Object.DestroyImmediate(picture);
                texture.Release(); Object.DestroyImmediate(texture);
            }
        }

        // ------------------------------------------------------------------ the report

        static void Finish()
        {
            Log("");
            foreach (var pair in results.OrderBy(r => r.Key))
            {
                var rows = pair.Value;
                Log("ZONE " + pair.Key + " (" + rows.Count + " crossings by " + BotCount + " bots, " + rows.Count / BotCount + " runs)");
                foreach (var policy in new[] { -1, 0, 1, 2 })
                {
                    var set = rows.Where(r => policy < 0 || (int)r[7] == policy).ToList();
                    if (set.Count == 0) continue;
                    var done = set.Where(r => r[0] > 0f).ToList();
                    Log("  " + (policy < 0 ? "all     " : ((Policy)policy).ToString().PadRight(8)) + " knocked down at least once " + Pct(set.Count(r => r[2] > 0f), set.Count)
                        + ", knockdowns per crossing " + (set.Sum(r => r[2]) / set.Count).ToString("0.00")
                        + ", staggers " + (set.Sum(r => r[3]) / set.Count).ToString("0.00")
                        + ", across " + Pct(done.Count, set.Count)
                        + (done.Count > 0 ? ", seconds " + done.Average(r => r[1]).ToString("0.0") + " (" + done.Min(r => r[1]).ToString("0.0") + "-" + done.Max(r => r[1]).ToString("0.0") + ")" : "")
                        + ", fell " + set.Count(r => r[4] > 0f) + ", stuck " + set.Count(r => r[5] > 0f) + ", flung " + set.Count(r => r[6] > 0f));
                }
                if (downsByZone.TryGetValue(pair.Key, out var by))
                    Log("  knockdowns by: " + string.Join(", ", by.OrderByDescending(k => k.Value).Select(k => k.Key + " " + k.Value)));
            }
            Log("Anomalies: " + anomalies.Count);
            foreach (var a in anomalies) Log("  " + a);
            foreach (var e in errors) Log("ERROR logged in Play: " + e.Split('\n')[0]);
            int code = errors.Count == 0 ? 0 : 1;
            Log("Bot test result: " + (code == 0 ? "DONE" : "DONE with " + errors.Count + " errors in Play"));
            CourseSkills.PawnRush = false;
            StopPlay(code);
        }

        static string Pct(int part, int whole) => whole > 0 ? (100f * part / whole).ToString("0") + "%" : "-";
        static string F(float v) => float.IsNaN(v) ? "" : v.ToString("0.###", CultureInfo.InvariantCulture);
        static string V(Vector3 v) => "(" + v.x.ToString("0.00") + ", " + v.y.ToString("0.00") + ", " + v.z.ToString("0.00") + ")";
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static void Log(string value)
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Path.Combine(Folder, Report), value + "\n");
            if (value.Length > 0) Debug.Log("[CarriageYardBots] " + value);
        }

        static void StopPlay(int code)
        {
            stopping = true;
            Application.logMessageReceived -= OnLog;
            YardKit.HitGiven -= OnHit;
            KillVolume.Entered -= OnFall;
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
