using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ChessFight.RagdollLab.Editor
{
    // R834: the first-run tutorial grey box's automatic check (Tools/Run-Tutorial.ps1 runs it on the project copy).
    //   Edit: builds Assets/Scenes/Tutorial.unity (RagdollLabBuilder.BuildTutorial) and reads what is in it.
    //   Play: the pawn is put through every lesson by moving it (no keys are pressed): across the hurdles' line, under the
    //         limbo bar's line, onto each wall top (it must stand there, the physics' own answer), onto the beam's landing.
    //         Lesson 4 (sprint) needs a held key, so it is passed by hand (SkipLessonForTest) and says so. A drop off the
    //         beam must bring the pawn back to the beam's start. Errors logged in Play are failures. Pictures.
    // It proves the lessons' logic and the scene, not that a person can do the moves (dive under 0.55 m, the ledge grab,
    // the 3 m climb): that is the user's run in Unity.
    [InitializeOnLoad]
    public static class TutorialValidation
    {
        const string ActiveKey = "ChessFight.R834.Tutorial.Play", ExitKey = "ChessFight.R834.Tutorial.Exit",
                     CodeKey = "ChessFight.R834.Tutorial.Code", EditBadKey = "ChessFight.R834.Tutorial.EditBad";
        const string Folder = "Logs/TutorialValidation", Report = "tutorial.txt";
        const float RuntimeLimit = 180f;

        static readonly List<string> failures = new List<string>();
        static readonly List<string> errors = new List<string>();
        static readonly Queue<Step> steps = new Queue<Step>();
        static double playRequestedAt, editorIdleAt, beganAt, waitUntil;
        static bool hadDone, doneBefore, stopping;
        static Camera captureCamera;

        sealed class Step
        {
            public string name;
            public float wait;
            public Action act;
        }

        static TutorialValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        static string V(Vector3 v) => "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")";

        static void Log(string value)
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Path.Combine(Folder, Report), value + "\n");
            if (value.Length > 0) Debug.Log("[Tutorial] " + value);
        }

        static void Check(string name, bool ok, string detail)
        {
            Log((ok ? "PASS  " : "FAIL  ") + name + ": " + detail);
            if (!ok) failures.Add(name);
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (condition != null && condition.StartsWith("Host type is not matching any asset type") && condition.Contains(".urtshader")) return;
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) errors.Add(condition + "\n" + stack);
        }

        // ------------------------------------------------------------------ Edit

        [MenuItem("ChessFight/Development/Tutorial/Validate (Edit + Play)", false, 20)]
        public static void RunMenu() => RunBatch();

        public static void RunBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, Report), "R834 first-run tutorial grey box checks (" +
                DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "). The pawn is moved, no keys are pressed.\n");
            failures.Clear();
            int bad;
            try
            {
                Log("");
                Log("== Edit: build the scene");
                RagdollLabBuilder.BuildTutorialForCheck();
                var spawner = Object.FindFirstObjectByType<PlaytestSpawner>();
                var course = Object.FindFirstObjectByType<TutorialCourse>();
                Check("E1 scene saved", AssetDatabase.LoadAssetAtPath<SceneAsset>(RagdollLabBuilder.TutorialScenePath) != null, RagdollLabBuilder.TutorialScenePath);
                Check("E2 playtest spawner with the pawn prefab", spawner != null && spawner.CharacterPrefab != null,
                      spawner != null && spawner.CharacterPrefab != null ? spawner.CharacterPrefab.name : "missing");
                Check("E3 tutorial course", course != null, course != null ? "present" : "missing");
                int hurdles = 0, labels = 0;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (t.name.StartsWith("Hurdle ")) hurdles++;
                labels = Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Length;
                Check("E4 two hurdles", hurdles == TutorialLayout.HurdleX.Length, hurdles.ToString());
                Check("E5 no lab floor labels", labels == 0, labels.ToString());
                Check("E6 no map test leftovers", GameObject.Find("Pawn Rush Map Test") == null, "");
                bad = failures.Count;
            }
            catch (Exception e)
            {
                Log("FAIL: " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
                bad = failures.Count + 1;
            }
            SessionState.SetInt(EditBadKey, bad);
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
                Log("FAIL: the Editor did not settle before Play Mode");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { editorIdleAt = now; return; }
            if (now - editorIdleAt < 2f) return;
            EditorApplication.update -= BeginPlayWhenIdle;
            EditorApplication.isPlaying = true;
        }

        // ------------------------------------------------------------------ Play

        static PlaytestSpawner Spawner => Object.FindFirstObjectByType<PlaytestSpawner>();
        static TutorialCourse Course => TutorialCourse.Current;

        static RagdollPawn Pawn => Spawner != null && Spawner.Driver is RagdollDriver d ? d.Pawn : null;

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                failures.Clear(); errors.Clear(); steps.Clear();
                stopping = false;
                beganAt = EditorApplication.timeSinceStartup;
                waitUntil = beganAt + 3f;   // the pawn stands up, the lesson begins
                // The finish writes the "done" flag; this PC's own value is put back at the end.
                hadDone = GameSettings.Has(TutorialCourse.DoneKey);
                doneBefore = GameSettings.Bool(TutorialCourse.DoneKey, false);
                Application.logMessageReceived += OnLog;
                captureCamera = new GameObject("Validation camera").AddComponent<Camera>();
                captureCamera.enabled = false;
                captureCamera.nearClipPlane = 0.1f; captureCamera.farClipPlane = 400f;
                Plan();
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) CompleteExit();
        }

        static void Put(Vector3 at, float yaw = 0f) => Spawner.MoveTo(at + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));

        static void Expect(string name, int lesson) =>
            Check(name, Course != null && Course.LessonNumber == lesson, "lesson now " + (Course != null ? Course.LessonNumber : -1) + ", expected " + lesson
                  + (Pawn != null ? ", hips " + V(Pawn.Hips.position) : ""));

        static void Add(string name, float wait, Action act) => steps.Enqueue(new Step { name = name, wait = wait, act = act });

        static void Plan()
        {
            Add("start", 0f, () =>
            {
                Check("P1 pawn spawned", Pawn != null, Pawn != null ? V(Pawn.Hips.position) : "none");
                Check("P2 lesson 1 at the start", Course != null && Course.LessonNumber == 1, Course != null ? Course.LessonNumber.ToString() : "no course");
                Check("P3 Pawn Rush skills on the pawn", Pawn != null && Pawn.PawnRushSkills != null, "");
                Picture("01_start", Pawn.Hips.position + new Vector3(0f, 3.2f, -5.5f), Pawn.Hips.position + new Vector3(0f, 0.5f, 3f), 60f);
                Picture("00_overview", new Vector3(4f, 55f, -8f), new Vector3(4f, 0f, -7.9f), 32f, true);
                Put(TutorialLayout.WalkMark);
            });
            Add("walk", 1.2f, () =>
            {
                Expect("L1 walking onto the gold square ends lesson 1", 2);
                // Across both hurdle lines (moved, not jumped) into the square past them.
                Put(TutorialLayout.JumpStart + new Vector3(0.8f, 0f, 0f), 90f);
            });
            Add("hurdle 0", 0.5f, () => Put(new Vector3(TutorialLayout.HurdleX[0] + 0.9f, 0f, TutorialLayout.HurdleZ), 90f));
            Add("hurdle 1", 0.5f, () => Put(new Vector3(TutorialLayout.HurdleX[1] + 0.9f, 0f, TutorialLayout.HurdleZ), 90f));
            Add("jump mark", 0.5f, () => Put(TutorialLayout.JumpMark, 90f));
            Add("jump", 1.2f, () =>
            {
                Expect("L2 both hurdle lines crossed, then the square", 3);
                // Not around the bar: into the square with the bar's line crossed beside the posts does not count.
                Put(new Vector3(LabLayout.LimboCenter.x + 3.2f, 0f, -5.5f), 180f);
            });
            // Past the bar's line beside the posts (x stays outside the bar), then sideways into the square.
            Add("around 0", 0.5f, () => Put(new Vector3(LabLayout.LimboCenter.x + 3.2f, 0f, TutorialLayout.DiveMark.z), 180f));
            Add("around", 0.5f, () => Put(TutorialLayout.DiveMark, 180f));
            Add("around check", 1.2f, () =>
            {
                Expect("L3a reaching the square round the bar does not count", 3);
                Put(TutorialLayout.DiveStart, 180f);
            });
            // R856: past the bar's line without a dive (walked under, jumped over) does not count.
            Add("under 0", 0.6f, () => Put(LabLayout.LimboCenter + new Vector3(0f, 0f, -1.1f), 180f));
            Add("under 1", 0.5f, () => Put(TutorialLayout.DiveMark, 180f));
            Add("no dive", 1.2f, () =>
            {
                Expect("L3e past the bar without a dive, then the square, does not count", 3);
                // A real dive under it (the check drives the pawn itself; no keys).
                Put(LabLayout.LimboCenter + new Vector3(0f, 0f, 1.3f), 180f);
                Spawner.enabled = false;
            });
            Add("dive at the bar", 0.6f, () => Drive(Vector3.back, false, true));
            Add("let go of the dive", 0.1f, () => Drive(Vector3.back, false, false));
            Add("slid under", 1.5f, () =>
            {
                Drive(Vector3.zero, false, false);
                Spawner.enabled = true;
                Check("L3f the dive takes the pawn under the bar", Pawn.Hips.position.z < LabLayout.LimboCenter.z, "hips " + V(Pawn.Hips.position));
                Put(TutorialLayout.DiveMark, 180f);
            });
            Add("dive", 1.2f, () =>
            {
                Check("L3b under the bar's line, then the square: the jump dive (3-2) next", Course.LessonLabel == "3-2", Course.LessonLabel);
                Put(TutorialLayout.AirDiveMark, 270f);
            });
            Add("no air dive", 1.0f, () =>
            {
                Check("L3c the mark without a dive from a jump does not count", Course.LessonLabel == "3-2", Course.LessonLabel);
                Log("NOTE  L3-2 the jump dive needs keys: passed by hand (SkipLessonForTest)");
                Course.SkipLessonForTest();
            });
            Add("to the sprint", 0.4f, () =>
            {
                Expect("L3d the sprint after the jump dive", 4);
                Put(TutorialLayout.SprintMark, 90f);
            });
            Add("no sprint", 1.5f, () =>
            {
                Expect("L4a the square without sprinting does not count", 4);
                Log("NOTE  L4 sprint needs a held key: passed by hand (SkipLessonForTest)");
                Course.SkipLessonForTest();
            });
            Add("ledge", 0.4f, () =>
            {
                Expect("L4b next lesson after the sprint", 5);
                Put(TutorialLayout.WallTopMark(0) + Vector3.up * 0.2f, 90f);
            });
            Add("on 2 m", 2.5f, () =>
            {
                Expect("L5 standing on the 2 m wall ends lesson 5", 6);
                Picture("02_wall_tops", new Vector3(13f, 6f, -14f), new Vector3(22f, 2f, -3f), 55f);
                Put(TutorialLayout.WallTopMark(1) + Vector3.up * 0.2f, 90f);
            });
            Add("on 3 m", 2.5f, () =>
            {
                Expect("L6 standing on the 3 m wall ends lesson 6", 7);
                var bots = Course.Bots;
                Check("B1 three practice bots", bots != null && bots.Count == 3, bots != null ? bots.Count.ToString() : "none");
                botsAt = new Vector3[bots != null ? bots.Count : 0];
                for (int i = 0; i < botsAt.Length; i++) botsAt[i] = bots[i] != null ? bots[i].Hips.position : Vector3.zero;
                botsWent = new float[botsAt.Length];
                watchBots = true;
                Put(TutorialLayout.FightStart, TutorialLayout.FightStartYaw);
            });
            Add("bots wander", 4f, () =>
            {
                var bots = Course.Bots;
                var skin = bots[0].skin != null ? bots[0].skin.sharedMaterial : null;
                Check("B5 the bots wear the black set", skin != null && skin.name.StartsWith("Pawn_P2"), skin != null ? skin.name : "none");
                watchBots = false;
                int walked = 0, inYard = 0;
                string where = "";
                var yard = TutorialLayout.BotYard;
                for (int i = 0; i < botsAt.Length; i++)
                {
                    var hips = bots[i].Hips.position;
                    if (botsWent[i] > 1f) walked++;   // the way it walked: a bot may stroll out and back, or stand a moment first
                    if (hips.x > yard.xMin - 0.6f && hips.x < yard.xMax + 0.6f && hips.z > yard.yMin - 0.6f && hips.z < yard.yMax + 0.6f) inYard++;
                    where += " " + V(hips) + " walked " + F(botsWent[i]) + " m" + (bots[i].Team == Teams.Black ? " black" : " team " + bots[i].Team);
                }
                Check("B2 the bots walk about the yard on their own (4 s, more than 1 m, still in the yard)",
                      walked == botsAt.Length && inYard == botsAt.Length, walked + " walked, " + inYard + " in the yard:" + where);
                Picture("04_fight_yard", TutorialLayout.FightStart + new Vector3(3f, 3.5f, -4f), TutorialLayout.FightMark + Vector3.up * 0.5f, 60f);
                Check("L7-1 is lesson 7's first part", Course.LessonLabel == "7-1", Course.LessonLabel);
                // A dive at a bot that is watching: bot 2 faces the player 5.5 m away, the others are stood aside.
                var c = TutorialLayout.FightMark;
                bots.PinForTest(0, new Vector3(8.2f, 0f, 7.6f), 0f, 12f);
                bots.PinForTest(2, new Vector3(8.2f, 0f, 2.4f), 0f, 12f);
                bots.PinForTest(1, c, 90f, 12f);
                Spawner.enabled = false;      // the check drives the player itself for a moment (no keys)
                Put(c + new Vector3(5.5f, 0f, 0f), 270f);
            });
            Add("run at the watcher", 1f, () => Drive(Vector3.left, true, false));
            Add("dive at the watcher", 0.55f, () =>
            {
                diveFrom = Vector3.Distance(Flat(Pawn.Hips.position), Flat(Course.Bots[1].Hips.position));
                botFrom = Course.Bots[1].Hips.position;
                Drive(Vector3.left, true, true);
            });
            Add("stop", 0.35f, () => Drive(Vector3.zero, false, false));
            Add("dodged", 1.3f, () =>
            {
                var b = Course.Bots[1];
                float aside = Mathf.Abs(b.Hips.position.z - botFrom.z);
                Check("B6 a bot that sees a dive coming steps aside and stays up",
                      aside > 0.5f && b.State == PawnState.Active && Course.LessonLabel == "7-1",
                      "dive began " + F(diveFrom) + " m away, the bot moved " + F(aside) + " m aside, " + b.State + ", lesson " + Course.LessonLabel);
                // From behind: bot 3 faces away, the player runs up behind it and dives.
                Course.Bots.PinForTest(2, new Vector3(11f, 0f, 3f), 270f, 8f);
                Put(new Vector3(14.4f, 0f, 3f), 270f);
            });
            Add("run up behind", 0.8f, () => Drive(Vector3.left, true, false));
            Add("dive from behind", 0.5f, () =>
            {
                diveFrom = Vector3.Distance(Flat(Pawn.Hips.position), Flat(Course.Bots[2].Hips.position));
                labelBefore = Course.LessonLabel;
                Drive(Vector3.left, true, true);
            });
            Add("stop 2", 0.35f, () => Drive(Vector3.zero, false, false));
            Add("tackled", 1.5f, () =>
            {
                if (labelBefore != "7-1") Log("NOT RUN  L7-1 from behind: 7-1 was already over before this dive");
                else Check("L7-1 a dive from behind knocks the bot over (physics) and ends 7-1", Course.LessonLabel == "7-2",
                           "dive began " + F(diveFrom) + " m behind, bot " + Course.Bots[2].State + ", lesson " + Course.LessonLabel);
                Spawner.enabled = true;
                if (Course.LessonLabel == "7-1") Course.SkipLessonForTest();
            });
            // 7-2: the bots run from the player.
            // Bot 2 is stood in the middle of the yard, the player 1.6 m west of it.
            Add("evade pin", 1f, () =>
            {
                Put(TutorialLayout.FightStart + new Vector3(2f, 0f, 0f), 270f);
                Course.Bots.PinForTest(1, TutorialLayout.FightMark, 90f, 0.6f);
            });
            Add("evade setup", 0.5f, () =>
            {
                var b = Course.Bots[1].Hips.position;
                Put(new Vector3(b.x - 1.6f, 0f, b.z), 90f);
                fleeFrom = Vector3.Distance(Flat(Pawn.Hips.position), Flat(b));
            });
            Add("fled", 1.4f, () =>
            {
                float d = Vector3.Distance(Flat(Pawn.Hips.position), Flat(Course.Bots[1].Hips.position));
                Check("B8 a bot runs from the player in 7-2", d > 2.6f, F(fleeFrom) + " m -> " + F(d) + " m in 1.4 s");
                Check("L7-2 then the grab", Course.LessonLabel == "7-2", Course.LessonLabel);
                // The player grabs bot 2 from right behind it (the check holds the grab key itself, spawner off).
                Course.Bots.PinForTest(1, TutorialLayout.FightMark, 90f, 0.5f);
                Spawner.enabled = false;
            });
            // From right behind it the player sprints after it with the grab key down, as a person would (every frame,
            // Tick): the bot starts (0.25 s), runs, tires; the hold must take, and the held bot must struggle free.
            Add("grab", 0.45f, () =>
            {
                var b = Course.Bots[1].Hips.position;
                Put(new Vector3(b.x - 0.6f, 0f, b.z), 90f);
                botHeldSeen = botFreed = false;
                botHeldAt = -1f;
                chaseClosest = float.MaxValue;
                chaseBot = 1;
            });
            Add("hold", 6f, () =>
            {
                Check("L7-2 chasing and holding the bot 0.8 s ends 7-2 (physics, a real grip)", Course.LessonLabel == "7-3",
                      "bot held " + (botHeldAt >= 0f ? F(botHeldAt) + " s into the chase" : "never, closest " + F(chaseClosest) + " m") + ", lesson " + Course.LessonLabel);
            });
            Add("struggle", 4f, () =>
            {
                chaseBot = -1;
                var b = Course.Bots[1];
                Check("B10 a held bot struggles free (clicks with jumps, pulling away)", botFreed,
                      "held " + botHeldSeen + ", got free " + botFreed + ", gauge now " + F(b.EscapeProgress));
                Spawner.enabled = true;
                if (Course.LessonLabel == "7-2") Course.SkipLessonForTest();
            });
            Add("7-3", 0.5f, () =>
            {
                Check("L7-3 then getting free", Course.LessonLabel == "7-3", Course.LessonLabel);
                Put(TutorialLayout.FightMark + new Vector3(0f, 0f, -3f), 0f);
                heldSeen = -1f;
                watchHeld = true;
            });
            Add("held", 9f, () =>
            {
                watchHeld = false;
                var bots = Course.Bots;
                Check("B3 the first bot comes and holds the player (physics, both hands)", heldSeen >= 0f,
                      heldSeen >= 0f ? "held " + F(heldSeen) + " s after the lesson began" : "never held in 9 s; grabber at " + V(bots.Grabber.Hips.position));
                float f1 = Vector3.Distance(Flat(Pawn.Hips.position), Flat(bots[1].Hips.position));
                float f2 = Vector3.Distance(Flat(Pawn.Hips.position), Flat(bots[2].Hips.position));
                Check("B9 the other two stand by the player to cut the way out", f1 < 3.6f && f2 < 3.6f, F(f1) + " m, " + F(f2) + " m");
                Picture("05_hunt", Pawn.Hips.position + new Vector3(3.5f, 3.5f, -3.5f), Pawn.Hips.position + Vector3.up * 0.4f, 55f);
                Check("L7-3 not done while only held (needs the struggle)", Course.LessonLabel == "7-3", Course.LessonLabel);
                // Freed by a teleport (R, a fall) is not getting free.
                Put(Flat(Pawn.Hips.position) + new Vector3(0f, 0f, -1.5f), 0f);
            });
            Add("teleported free", 0.6f, () =>
            {
                Check("L7-3 a teleport out of the bot's hands does not count", Course.LessonLabel == "7-3", Course.LessonLabel);
                // The struggle itself, by the check: clicks every 0.2 s, every other with a jump, pulling away.
                struggleTaps = 0;
                struggling = true;
                Spawner.enabled = false;
            });
            Add("struggled free", 6f, () =>
            {
                struggling = false;
                Spawner.enabled = true;
                Check("L7-3 struggling free (mashing the left button) ends 7-3", Course.LessonLabel == "8-1",
                      struggleTaps + " clicks, lesson " + Course.LessonLabel);
                if (Course.LessonLabel == "7-3") Course.SkipLessonForTest();
            });
            Add("pawn skill", 1.0f, () =>
            {
                Check("L8 the pawn's step (8-1) waits for its key", Course.LessonLabel == "8-1", Course.LessonLabel);
                Log("NOTE  L8 the pawn's step and second step need the skill key: passed by hand (SkipLessonForTest twice)");
                Course.SkipLessonForTest();
                Course.SkipLessonForTest();
            });
            Add("let go", 2f, () =>
            {
                Check("B4 the bot lets go once lesson 7 is over", !Pawn.BeingHeld, Pawn.BeingHeld ? "still held" : "free");
                Expect("L7 done: lesson 10 next", 10);
                Put(TutorialLayout.BeamStart, TutorialLayout.BeamStartYaw);
            });
            Add("beam", 1.5f, () =>
            {
                Picture("03_beam", TutorialLayout.BeamStart + new Vector3(2.5f, 3.5f, 3.5f), TutorialLayout.BeamStart + new Vector3(0f, 0f, -10f), 60f);
                // Off the side, over the void.
                Put(new Vector3(3f, 0f, -22f), 180f);
                floatedAt = float.MaxValue;
            });
            Add("fall", 8f, () =>
            {
                // R857: on the way the moat's water held the pawn (watched every frame, Tick).
                Check("P8 a fall into the moat floats at the water", floatedAt < -6f && floatedAt > -11f,
                      floatedAt < float.MaxValue ? "floated, hips down to y " + F(floatedAt) : "never floated");
                var hips = Pawn.Hips.position;
                float off = Vector2.Distance(new Vector2(hips.x, hips.z), new Vector2(TutorialLayout.BeamStart.x, TutorialLayout.BeamStart.z));
                Check("P4 a fall comes back to the lesson's start", off < 2.5f && hips.y > -1f, "hips " + V(hips) + ", " + F(off) + " m from the beam's start");
                Expect("P5 still lesson 10 after the fall", 10);
                Put(TutorialLayout.GoalMark, 180f);
            });
            Add("goal", 1.5f, () =>
            {
                Check("L10 the landing finishes the tutorial", Course != null && Course.Finished, Course != null && Course.Finished ? "finished" : "not finished");
                Check("P6 the done flag is written", GameSettings.Bool(TutorialCourse.DoneKey, false), "");
            });
        }

        static Vector3[] botsAt = new Vector3[0];
        static float[] botsWent = new float[0];
        static bool watchBots;
        static Vector3 botFrom;
        static float diveFrom, fleeFrom;
        static string labelBefore = "";
        static bool botHeldSeen, botFreed, botWasHeld;
        static int chaseBot = -1, struggleTaps;
        static bool struggling;
        static double nextStruggle;
        static float botHeldAt = -1f;
        static double chaseLetGo;
        static float chaseClosest = float.MaxValue;
        static double chaseFrom;

        // The player chasing bot `chaseBot` at a sprint with the grab key down (every editor frame while it is set).
        static void ChaseStep(double now)
        {
            if (chaseBot < 0 || Course == null || Pawn == null) { botWasHeld = false; return; }
            var bot = Course.Bots[chaseBot];
            if (!botHeldSeen && !botWasHeld && botHeldAt < 0f && chaseFrom <= 0) chaseFrom = now;
            bool held = bot.BeingHeld;
            if (held && !botHeldSeen) { botHeldSeen = true; botHeldAt = (float)(now - chaseFrom); }
            if (botWasHeld && !held) botFreed = true;
            botWasHeld = held;
            var to = Flat(bot.Hips.position - Pawn.Hips.position);
            float gap = to.magnitude;
            var dir = gap > 0.01f ? to / gap : Pawn.Facing;
            if (gap < chaseClosest) chaseClosest = gap;
            // As a person chases: sprint only from afar, ease off close in so the body does not overshoot and circle the
            // bot, keep walking right into it (the palms reach 0.25 m), and press grab only within arm's reach - a hand
            // takes whatever is nearest while the key is down, a wall or the floor included, so anything else is let go.
            if (Pawn.Grabbing && !Pawn.HoldingPawn(out _)) chaseLetGo = now + 0.25;
            bool grab = gap < 1.2f && now >= chaseLetGo;
            if (Spawner != null && Spawner.Driver is RagdollDriver driver)
                driver.SetCommand(new CharacterCommand { Move = gap > 0.25f ? dir * Mathf.Clamp(gap / 1.2f, 0.5f, 1f) : Vector3.zero,
                                                         Sprint = gap > 1.6f, Grab = grab, Aim = dir });
        }

        // The player holding the grab key toward dir (spawner off, so it stays held).
        static void GrabAt(Vector3 dir)
        {
            if (Spawner != null && Spawner.Driver is RagdollDriver driver)
                driver.SetCommand(new CharacterCommand { Move = dir * 0.3f, Grab = true, Aim = dir });
        }

        // The player driven by the check (the spawner is switched off meanwhile, so the command stays): run, sprint, dive.
        static void Drive(Vector3 dir, bool sprint, bool dive)
        {
            if (Spawner != null && Spawner.Driver is RagdollDriver driver)
                driver.SetCommand(new CharacterCommand { Move = dir, Sprint = sprint, Shove = dive, Aim = dir.sqrMagnitude > 0f ? dir : Pawn.Facing });
        }
        static bool watchHeld;
        static float heldSeen = -1f;
        static double heldWatchFrom;

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float floatedAt = float.MaxValue;   // the lowest the hips were while floating

        static void Tick()
        {
            if (stopping) return;
            double now = EditorApplication.timeSinceStartup;
            if (watchHeld)
            {
                if (heldSeen < 0f && heldWatchFrom <= 0) heldWatchFrom = now;
                if (heldSeen < 0f && Pawn != null && Pawn.BeingHeld) heldSeen = (float)(now - heldWatchFrom);
            }
            else heldWatchFrom = 0;
            if (watchBots && Course != null && Course.Bots != null)
                for (int i = 0; i < botsAt.Length && i < Course.Bots.Count; i++)
                {
                    var at = Course.Bots[i].Hips.position;
                    botsWent[i] += Vector3.Distance(Flat(at), Flat(botsAt[i]));
                    botsAt[i] = at;
                }
            if (chaseBot >= 0) ChaseStep(now);
            else chaseFrom = 0;
            if (Pawn != null && Pawn.Floating) floatedAt = Mathf.Min(floatedAt, Pawn.Hips.position.y);
            if (struggling && now >= nextStruggle && Pawn != null && Spawner.Driver is RagdollDriver mashing)
            {
                // Only while held (a click when free is a dive); away from the grabber.
                if (Pawn.BeingHeld)
                {
                    var away = Flat(Pawn.Hips.position - Course.Bots.Grabber.Hips.position).normalized;
                    mashing.SetCommand(new CharacterCommand { Move = away, Shove = true, Jump = (struggleTaps & 1) == 0, Aim = Pawn.Facing });
                    struggleTaps++;
                }
                else mashing.SetCommand(new CharacterCommand { Aim = Pawn.Facing });
                nextStruggle = now + 0.2;
            }
            if (now - beganAt > RuntimeLimit) { Log("FAIL: ran out of time"); failures.Add("time"); Finish(); return; }
            if (now < waitUntil) return;
            if (steps.Count == 0) { Finish(); return; }
            var step = steps.Dequeue();
            try { step.act(); }
            catch (Exception e)
            {
                Log("FAIL  step " + step.name + ": " + e.GetType().Name + ": " + e.Message);
                failures.Add(step.name);
                Finish();
                return;
            }
            waitUntil = now + (steps.Count > 0 ? steps.Peek().wait : 0f);
        }

        static void Picture(string name, Vector3 eye, Vector3 target, float size, bool flat = false)
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tutorialNoCapture") >= 0) return;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Log("NOT RUN: picture " + name + " (no graphics device)"); return; }
            const int width = 1280, height = 720;
            captureCamera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, flat ? Vector3.forward : Vector3.up));
            captureCamera.orthographic = flat;
            if (flat) captureCamera.orthographicSize = size;
            else captureCamera.fieldOfView = size;
            captureCamera.aspect = (float)width / height;
            var main = Camera.main;
            if (main != null) { captureCamera.clearFlags = main.clearFlags; captureCamera.backgroundColor = main.backgroundColor; }
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var before = RenderTexture.active;
            Texture2D picture = null;
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
                picture = new Texture2D(width, height, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                picture.Apply();
                File.WriteAllBytes(Path.Combine(Folder, name + ".png"), picture.EncodeToPNG());
                Log("      picture " + name + ".png");
            }
            finally
            {
                RenderTexture.active = before;
                if (picture != null) Object.DestroyImmediate(picture);
                texture.Release(); Object.DestroyImmediate(texture);
            }
        }

        static void Finish()
        {
            foreach (var e in errors) Log("ERROR logged in Play: " + e.Split('\n')[0]);
            Check("P7 errors logged in Play", errors.Count == 0, errors.Count.ToString());
            if (hadDone) GameSettings.SetBool(TutorialCourse.DoneKey, doneBefore);
            else GameSettings.Forget(TutorialCourse.DoneKey);
            int bad = failures.Count + SessionState.GetInt(EditBadKey, 0);
            Log("");
            Log("Tutorial result: " + (bad == 0 ? "PASS" : "FAIL (" + bad + ")"));
            stopping = true;
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick;
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(ExitKey, true);
            SessionState.SetInt(CodeKey, bad == 0 ? 0 : 1);
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
