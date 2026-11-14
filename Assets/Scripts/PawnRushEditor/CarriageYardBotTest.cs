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
    // real ragdoll pawns (the RagdollPawn prefab; the Pawn Rush movement and skills, PawnRushCourseSkills, since R661 -
    // R660's bots ran with the shared ragdoll movement) run
    //   zone 1   from the start court through the outer gate, across the yard of horse-drawn carriages between the sheds,
    //            to the canal's entry landing (map z 52)
    //   zone 2   from the canal's far landing across the loading and sorting yard - since R661 the conveyor, the three
    //            storage rows with their moving gaps and the heavy berth - to the mill basin's edge (map z 115.2)
    // several times each on lanes drawn from a seed, and every hit a yard hazard gives them is counted (YardKit.HitGiven):
    // knocked down or staggered, by which hazard; how long each crossing took; and anything that looks wrong - a pawn
    // pressing on without moving for 4 s (stuck), its hips over 14 m/s or 3.2 m up (flung), in the water.
    //
    // The bots: four run, four sprint, four sprint and jump just before a giant chest lands within reach of its wave (the
    // way the shockwave is meant to be dodged). In zone 1 none of them reads a carriage's line, shoves, or rides the
    // lifting wheel: they are the blind runners the models assumed, now with the real physics (bodies, pushes, the pawns'
    // own bumping), not people. Zone 2 (R661) cannot be crossed blind - its rows open only at their gaps - so there every
    // bot hops the conveyor and heads for the nearer gap of each row: the runners and sprinters as they come (at the belt
    // whatever is on it, into a gap whatever its colour), the jumpers with care (a clear belt, a gap that stays open
    // long enough, a jump for the chest's wave). Pictures from above go to Logs/CarriageYardBots with the report and
    // every hit.
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
        const string Report = "report.txt", HitsFile = "hits.csv", RunsFile = "crossings.csv", TraceFile = "trace.csv";
        // Every bot's state every TraceStep for the first TraceSeconds of each zone's first run (R661: why a bot does what
        // it does at the conveyor and the rows).
        const float TraceSeconds = 20f, TraceStep = .2f;
        const string MapPath = "Assets/Prefabs/PawnRushMaps/RoyalCarriageYard.prefab";
        const string PawnPath = "Assets/Prefabs/RagdollPawn.prefab";
        const int BotCount = 12;
        const float RuntimeLimit = 1800f;
        // Map frame (the map at the origin): zone 1's finish line on the entry landing, zone 2's at the basin's edge.
        const float Zone1Finish = 52f, Zone2Finish = 115.2f, Zone1Limit = 55f, Zone2Limit = 45f;
        // The canal's far landing ends here (map z): railed either side (x +-10), so out of it first, then along.
        const float Zone2Mouth = 82.8f;

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
            public bool Running, Done, Fell, Stuck, Flung, Pinned, Waiting;
            public float Started, Finished, LastHit = -99f, CheckTime, MaxSpeed, MaxRise, Lane;
            public float LastJump = -99f, PressSince = -1f, InsideSince = -1f, BackOffUntil = -1f;
            public Vector3 CheckAt;
            public double JumpedFor = double.NaN;
            public readonly Dictionary<string, int> DownsBy = new Dictionary<string, int>();
        }

        static readonly List<Bot> bots = new List<Bot>();
        static readonly List<string> errors = new List<string>();
        static readonly List<string> anomalies = new List<string>();
        static readonly StringBuilder hits = new StringBuilder(), crossings = new StringBuilder(), trace = new StringBuilder();
        static float nextTrace;
        static readonly Queue<Action> pictures = new Queue<Action>();
        static readonly Dictionary<int, Dictionary<string, int>> downsByZone = new Dictionary<int, Dictionary<string, int>>();
        static readonly Dictionary<int, List<float[]>> results = new Dictionary<int, List<float[]>>();
        static GameObject map;
        static Camera captureCamera;
        static SlamChest[] chests = new SlamChest[0];
        static CargoConveyor conveyor;
        static StackRow[] rows = new StackRow[0];
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
                "Carriage yard bot test (R660, zone 2 R661): twelve ragdoll pawns run zone 1 and zone 2 of the Royal Carriage Yard in Play Mode.\n" +
                "Zone 1 blind runners; zone 2 bots that hop the conveyor and head for the rows' gaps, the jumpers with care.\n" +
                "Not people, not online.\n");
            File.WriteAllText(Path.Combine(Folder, HitsFile), "zone,run,bot,policy,t,kind,knockdown_s,push,x,y,z\n");
            File.WriteAllText(Path.Combine(Folder, RunsFile), "zone,run,bot,policy,lane,finished,seconds,downs,staggers,fell,stuck,flung,max_speed,max_rise\n");
            File.WriteAllText(Path.Combine(Folder, TraceFile), "zone,bot,policy,t,x,y,z,vx,vy,vz,tilt,ground,on_feet,move_x,move_z,jump,waiting\n");
            // The map's settings are code defaults (the prefab serializes none of them), but Unity bakes a prefab's fields into
            // its imported artifact and does not import it again when only a default changes (R661: the copy kept the first
            // R661 conveyor and crane numbers through three tunings). Imported again here, the bots run the code as it is.
            AssetDatabase.ImportAsset(MapPath, ImportAssetOptions.ForceUpdate);
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
                bots.Clear(); errors.Clear(); anomalies.Clear(); pictures.Clear(); hits.Clear(); crossings.Clear(); trace.Clear();
                nextTrace = 0f;
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
            // The Pawn Rush movement every pawn of a Pawn Rush scene has - its jump, gravity, the 0.25 m it walks up by
            // itself and the ledge assist (PawnRushPhysicsParams) - and the piece skills: PawnRushCourseSkills hands them
            // out, and attaches itself only when a course scene with a playtest spawner loads; the bot test builds into an
            // empty scene, so it is added here, by name (this editor assembly does not reference RagdollLab). R661 found
            // the R660 runs went without it: those bots had the shared ragdoll movement, not Pawn Rush's.
            var skills = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ChessFight.RagdollLab.PawnRushCourseSkills")).FirstOrDefault(t => t != null);
            if (skills == null) throw new InvalidOperationException("PawnRushCourseSkills not found: the bots would not move as Pawn Rush pawns");
            new GameObject("Pawn Rush skills (bot test)").AddComponent(skills);
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
            conveyor = map.GetComponentInChildren<CargoConveyor>(true);
            rows = map.GetComponentsInChildren<StackRow>(true).OrderBy(r => r.transform.position.z).ToArray();
            Log("Pawn Rush movement and skills: " + skills.Name + " added (every pawn gets PawnRushPhysicsParams on its first frame)");
            Log("Map built: " + chests.Length + " giant chest(s), " + map.GetComponentsInChildren<RoamingCarriage>(true).Length + " roaming bodies, "
                + (conveyor != null ? conveyor.Crates.Length + " conveyor loads at " + conveyor.Speed.ToString("0.0") + " m/s (belt " + conveyor.Height.ToString("0.00") + " m), " : "no conveyor, ")
                + rows.Length + " storage rows (" + map.GetComponentsInChildren<StackCrate>(true).Length + " moving stacks, trips "
                + string.Join(" / ", rows.Select(r => r.Trip.ToString("0.0"))) + " s), "
                + map.GetComponentsInChildren<VerticalWaterwheel>(true).Length + " lifting wheel(s)");

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
                bot.Running = bot.Done = bot.Fell = bot.Stuck = bot.Flung = bot.Pinned = bot.Waiting = false;
                bot.LastJump = -99f; bot.PressSince = bot.InsideSince = bot.BackOffUntil = -1f;
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
                    // Where along the conveyor it crosses; the rest of the way is steered as it goes (Steer2).
                    bot.Lane = (float)(rng.NextDouble() * 40.0 - 20.0);
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
            bool tracing = run == 0 && now >= nextTrace;
            if (tracing) nextTrace = now + TraceStep;
            foreach (var bot in bots)
            {
                if (!bot.Running || bot.Done || bot.Hips == null) continue;
                var p = bot.Hips.position;
                Vector3 dir;
                bool jump;
                bot.Waiting = false;
                if (zone == 2) Steer2(bot, p, now, out dir, out jump);
                else
                {
                    while (bot.Next < bot.Route.Count - 1 && Flat(bot.Route[bot.Next] - p).magnitude < 1.5f) bot.Next++;
                    dir = Flat(bot.Route[bot.Next] - p);
                    if (dir.sqrMagnitude > 1e-4f) dir.Normalize();
                    jump = bot.Policy == Policy.Jumper && JumpForSlam(bot, p);
                }
                // Pressing on without getting anywhere (against the belt's side, a stack's face): a jump.
                jump |= Unstick(bot, dir, now);
                if (jump) bot.LastJump = now;
                if (tracing && now - bot.Started < TraceSeconds) Trace(bot, p, dir, jump, now);
                var aim = dir.sqrMagnitude > 1e-4f ? dir : Vector3.forward;
                bot.Driver.SetCommand(new CharacterCommand { Move = dir, Sprint = bot.Policy != Policy.Run, Jump = jump, Aim = aim });
                if (p.z >= (zone == 1 ? Zone1Finish : Zone2Finish))
                {
                    bot.Done = true;
                    bot.Finished = now;
                    bot.Driver.SetCommand(default);
                }
            }
        }

        static void Trace(Bot bot, Vector3 p, Vector3 dir, bool jump, float now)
        {
            var v = bot.Hips.linearVelocity;
            float tilt = Vector3.Angle(bot.Hips.transform.up, Vector3.up);
            float ground = float.NaN;
            int n = Physics.RaycastNonAlloc(p + Vector3.up * .1f, Vector3.down, under, 3f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (under[i].collider.GetComponentInParent<ICharacterDriver>() == null)
                    ground = float.IsNaN(ground) ? under[i].distance - .1f : Mathf.Min(ground, under[i].distance - .1f);
            trace.Append(string.Join(",", zone, bot.Index + 1, bot.Policy, F(now - bot.Started), F(p.x), F(p.y), F(p.z), F(v.x), F(v.y), F(v.z), F(tilt),
                                     F(ground), OnFeet(bot) ? 1 : 0, F(dir.x), F(dir.z), jump ? 1 : 0, bot.Waiting ? 1 : 0)).Append('\n');
        }

        // Zone 2 (R661): over the conveyor through a gap in its loads, through each storage row's gap, on to the basin. The
        // careful (the jumpers) wait at the belt until no load will be where they cross while they are on it, make for the
        // gap that will still be open when they get there, hold back from a gap about to close, keep out from under the heavy
        // chest and jump its wave; the others go at the belt whatever is on it and into the nearer gap whatever its colour.
        static void Steer2(Bot bot, Vector3 p, float now, out Vector3 dir, out bool jump)
        {
            jump = false;
            bool careful = bot.Policy == Policy.Jumper;
            float pace = bot.Policy == Policy.Run ? 4.5f : 6.5f;
            if (p.z < Zone2Mouth)
            {
                dir = Toward(p, new Vector3(Mathf.Clamp(bot.Lane, -7f, 7f), 0f, Zone2Mouth + .6f));
                return;
            }
            if (conveyor != null)
            {
                var c = conveyor.transform;
                float south = c.position.z - conveyor.Width * .5f - .1f, north = c.position.z + conveyor.Width * .5f + .1f;
                if (p.z < north + .25f)
                {
                    // Up to the belt at its lane; the careful stop 1.8 m short of it (a run-up for the jump) until it is clear.
                    if (p.z < south - 1.9f)
                    {
                        dir = Toward(p, new Vector3(Mathf.Clamp(bot.Lane, -21f, 21f), 0f, south - 1.5f));
                        return;
                    }
                    float x = c.InverseTransformPoint(p).x;
                    // On the belt from about 0.3 s to 1.1 s from now: no load there then (one passing now goes on by).
                    if (careful && p.z < south - .15f && !conveyor.ClearAt(x, .3f, 1.1f, .4f))
                    {
                        bot.Waiting = true;
                        dir = Vector3.zero;
                        return;
                    }
                    // A jump at the belt, the body at its side (the ledge assist carries feet within 0.2 m of its top over
                    // it; a pawn's jump carries it on only 0.2 - 0.4 m, so from further off it falls short); pressed
                    // against it without getting on, step back half a second and run at it again.
                    float before = south - p.z;
                    if (now < bot.BackOffUntil)
                    {
                        dir = Vector3.back;
                        return;
                    }
                    bool feet = OnFeet(bot);
                    if (before < .45f && before > -.25f && feet && Flat(bot.Hips.linearVelocity).magnitude < 1f && now - bot.LastJump > 1.5f)
                    {
                        bot.BackOffUntil = now + .45f;
                        dir = Vector3.back;
                        return;
                    }
                    dir = Vector3.forward;
                    jump = before < .5f && before > -.25f && feet && now - bot.LastJump > .9f;
                    return;
                }
            }
            foreach (var row in rows)
            {
                float rz = row.transform.position.z, half = row.StackSize.z * .5f, face = rz - half;
                if (p.z > rz + half + .5f) continue;
                // In the gap (the hips past the row's near face): straight on through.
                if (p.z > face + .1f)
                {
                    dir = Vector3.forward;
                    return;
                }
                Vector3 gap = default;
                float openFor = 0f, best = float.MaxValue;
                for (int h = 0; h < 2; h++)
                {
                    var g = row.GapWorld(h, 0f, out float open);
                    if (careful && Mathf.Abs(g.x - p.x) > .45f)
                        g = row.GapWorld(h, Mathf.Abs(g.x - p.x) / pace + .3f, out open);
                    float d = Mathf.Abs(g.x - p.x);
                    if (d < best) { best = d; gap = g; openFor = open; }
                }
                float dx = gap.x - p.x;
                if (Mathf.Abs(dx) < .45f)
                {
                    // Lined up: the careful only go if it stays open until they are through.
                    float through = (face + 2f * half + .4f - p.z) / pace + .3f;
                    if (careful && openFor < through)
                    {
                        bot.Waiting = true;
                        var hold = new Vector3(gap.x, 0f, face - 1.3f);
                        dir = Flat(hold - p).magnitude > .5f ? Toward(p, hold) : Vector3.zero;
                        return;
                    }
                    dir = new Vector3(Mathf.Clamp(dx * 2f, -.6f, .6f), 0f, 1f).normalized;
                    return;
                }
                // Along the row a metre and a bit off its face, to the gap.
                dir = Toward(p, new Vector3(gap.x, 0f, face - 1.3f));
                jump = careful && JumpForSlam(bot, p);
                return;
            }
            dir = Toward(p, new Vector3(Mathf.Clamp(p.x, -6f, 6f), 0f, Zone2Finish + 1f));
            if (careful && UnderChest(p, out var away)) dir = away;
            jump = careful && JumpForSlam(bot, p);
        }

        // Under the heavy chest's footprint (and a metre round it) while it is carried there, held or lowered: the way
        // out, sideways along the berth line.
        static bool UnderChest(Vector3 p, out Vector3 away)
        {
            away = Vector3.zero;
            foreach (var chest in chests)
            {
                if (chest == null || !chest.NextLanding(out var at, out float t) || t > 4.5f) continue;
                var off = Flat(p - at);
                if (Mathf.Abs(off.x) > 2.8f || Mathf.Abs(off.z) > 2.4f) continue;
                away = new Vector3(off.x >= 0f ? 1f : -1f, 0f, 0f);
                return true;
            }
            return false;
        }

        // On its feet on something: not in the air (the hips over 0.8 m above whatever is under them, or rising or falling
        // fast) and not lying (tilted over 60 degrees with the hips under 0.31 m, as RagdollPawn judges it: a pawn stands
        // them 0.26 m up, standHeight; a sprinting one leans far forward and is still on its feet). A jump pressed in the
        // air is a dive in Pawn Rush.
        static readonly RaycastHit[] under = new RaycastHit[16];
        static bool OnFeet(Bot bot)
        {
            if (bot.Hips == null || Mathf.Abs(bot.Hips.linearVelocity.y) > 2.5f) return false;
            var from = bot.Hips.position + Vector3.up * .1f;
            int n = Physics.RaycastNonAlloc(from, Vector3.down, under, 1.2f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (under[i].collider.GetComponentInParent<ICharacterDriver>() == null) best = Mathf.Min(best, under[i].distance - .1f);
            if (best > .8f) return false;
            bool lying = Vector3.Angle(bot.Hips.transform.up, Vector3.up) > 60f && best < .31f;
            return !lying;
        }

        static Vector3 Toward(Vector3 from, Vector3 to)
        {
            var d = Flat(to - from);
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.zero;
        }

        // A bot pushing on (not waiting) but not moving along its way for half a second jumps, at most every 1.2 s.
        static bool Unstick(Bot bot, Vector3 dir, float now)
        {
            if (bot.Waiting || dir.sqrMagnitude < .25f || bot.Hips == null || !OnFeet(bot)) { bot.PressSince = -1f; return false; }
            if (Vector3.Dot(Flat(bot.Hips.linearVelocity), dir) > .8f) { bot.PressSince = -1f; return false; }
            if (bot.PressSince < 0f) bot.PressSince = now;
            if (now - bot.PressSince < .5f || now - bot.LastJump < 1.2f) return false;
            bot.PressSince = -1f;
            return true;
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
                // Inside something solid for 1.5 s (a stack set down on it, the chest, the belt, a stall): pinned.
                var inside = Physics.OverlapSphere(p, .12f, ~0, QueryTriggerInteraction.Ignore).FirstOrDefault(c => c.GetComponentInParent<ICharacterDriver>() == null);
                if (inside == null) bot.InsideSince = -1f;
                else if (bot.InsideSince < 0f) bot.InsideSince = now;
                else if (!bot.Pinned && now - bot.InsideSince > 1.5f)
                {
                    bot.Pinned = true;
                    Anomaly(bot, "inside " + inside.name + " for 1.5 s");
                }
                // Waiting for a gap on purpose is not stuck.
                if (bot.Waiting)
                {
                    bot.CheckAt = p;
                    bot.CheckTime = now;
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
            File.AppendAllText(Path.Combine(Folder, TraceFile), trace.ToString());
            trace.Clear();
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
                case ConveyorCrate _: return "conveyor load";
                case StackCrate _: return "storage stack";
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
                // R661's yard in the map's frame: the conveyor at z 88, the rows at 93.5 / 99.5 / 105.5, the berth at 109.2.
                Later(3.5f, () => Picture("zone2_overview", new Vector3(0f, 30f, 70f), new Vector3(0f, 0f, 104f), 64f));
                Later(5.5f, () => Picture("zone2_top", new Vector3(0f, 64f, 100f), new Vector3(0f, 0f, 100.01f), 54f));
                Later(8f, () => Picture("zone2_low", new Vector3(-21f, 5f, 83.5f), new Vector3(2f, 1f, 97f), 62f));
                Later(10f, () => Picture("zone2_rows", new Vector3(0f, 10f, 89.5f), new Vector3(0f, 0f, 106f), 64f));
                Later(12.5f, () => Picture("zone2_berth", new Vector3(-19f, 7f, 103.5f), new Vector3(0f, 1.5f, 110.5f), 64f));
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
