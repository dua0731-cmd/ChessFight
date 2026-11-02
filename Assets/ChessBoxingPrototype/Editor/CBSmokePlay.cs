using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.ChessBoxingPrototype.Editor
{
    // Explicitly requested Editor observations only. Physics and contacts run in the real PlayerLoop.
    [InitializeOnLoad]
    public static class CBSmokePlay
    {
        const string ScenePath = "Assets/ChessBoxingPrototype/Scenes/ChessBoxingPrototype.unity";
        static readonly string Folder = Path.GetFullPath("Library/ChessBoxingR160");
        static readonly string[] Names = { "camera", "hands", "move", "guard-upper", "guard-wrong", "evade", "evade-hit", "orbit-punch", "recoil", "down", "ai", "ai-match", "ko", "idle" };
        static readonly Vector2[] Directions = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
        static CBMatch match;
        static CBFighter player, enemy;
        static CBBrain brain;
        static bool running, previousExternal, phaseShot, phaseAction, sawDown, sawRising, sawRecovery;
        static bool leftCommitted, lowCommitted, commitmentHeld, reentryRejected;
        static double nextPoll;
        static float began, phaseBegan, nextPunch, startHealth, startEnemyHealth, maxTilt, maxSpeed;
        static float steadySum, stopSpeed;
        static int phase = -1, frames, lastFrame = -1, attempts, accepted, steadyFrames, startLanded, startBlocks;
        static string scenario, lastStates;
        static Vector3 phasePosition, headOrigin, headMin, headMax;
        static Quaternion phaseRotation;
        static readonly List<Fact> facts = new List<Fact>();
        static readonly List<Sample> samples = new List<Sample>();
        static readonly List<string> screenshots = new List<string>();
        static readonly float[] moveSpeeds = new float[3];
        static readonly List<OrbitSample> orbitSamples = new List<OrbitSample>();
        static readonly List<OrbitShot> orbitShots = new List<OrbitShot>();
        static OrbitShot orbitShot;
        static float orbitArc, orbitLastYaw, orbitNextSample;
        static int orbitShotCount, orbitPhaseAccepted, orbitPhaseLanded;
        static readonly List<EvadeTrial> evadeTrials = new List<EvadeTrial>();
        static EvadeTrial evadeTrial;
        static bool trialDodged;
        static int combatLastLanded;
        static RecoilMeasurement recoil;
        static readonly List<RecoilSample> recoilSamples = new List<RecoilSample>();
        static Vector3 recoilBodyOrigin, recoilHeadOrigin, recoilHeadLocal;
        static Quaternion recoilYaw;
        static float recoilHitClock, recoilStableSince, recoilLastFixedTime;
        static bool recording, recordingTail;
        static string recordingFolder;
        static double recordingBegan, recordingNextFrame;
        static float recordingGameBegan;
        static int recordingFrames, recordingLastFrame;
        static RecordingInfo recordingInfo;

        [Serializable] sealed class Fact { public string name, evidence; public bool passed; }
        [Serializable] sealed class FighterSample
        {
            public string state, lastEvent;
            public float health, stamina, opacity, gloveMinAlpha, speed, tilt;
            public int landed, blocks, removedArmTriangles, gloveMaterialCount;
            public bool guarding, attacking, attackLeft, attackLow, uppercut;
            public Vector3 position, head, leftGlove, rightGlove;
            public Vector3 bodyForward, headForward, leftGloveForward, rightGloveForward;
        }
        [Serializable] sealed class Sample
        {
            public string phase;
            public float elapsed, cameraDistance;
            public Vector2 intent;
            public Vector3 cameraPosition, playerViewport, enemyHeadViewport, enemyBodyViewport;
            public FighterSample player, enemy;
        }
        [Serializable] sealed class Report
        {
            public string utc, scenario, status, note;
            public bool running, passed, sawDown, sawRising, sawRecovery;
            public int frames, attempts, accepted;
            public float elapsed, maxTilt, maxSpeed;
            public Fact[] facts;
            public Sample[] samples;
            public string[] screenshots;
            public OrbitSample[] orbitSamples;
            public OrbitShot[] orbitShots;
            public EvadeTrial[] evadeTrials;
            public RecoilMeasurement recoil;
            public RecoilSample[] recoilSamples;
        }
        [Serializable] sealed class OrbitSample
        {
            public float time, arcDegrees, distance, bodyTargetError, headTargetError, leftGloveTargetError, rightGloveTargetError;
            public string movement;
            public bool attacking, leftHand;
            public Vector3 bodyForward, headForward, leftGloveForward, rightGloveForward;
        }
        [Serializable] sealed class OrbitShot
        {
            public string movement;
            public bool left, accepted;
            public float began, launchArc, launchDistance, launchBodyError, launchHeadError;
            public float peakForwardReach, peakAxisError, peakGloveForwardError, closestHeadGap = 100f, closestBodyGap = 100f;
            public int landedBefore, landed;
        }
        [Serializable] sealed class EvadeTrial
        {
            public string action, contactPart;
            public bool attackAccepted, dodgeAccepted;
            public float damage, minimumHeadGap = 100f, minimumBodyGap = 100f;
            public int landed, blocks;
            public Vector3 minimumHeadDelta, maximumHeadDelta;
        }
        [Serializable] sealed class RecoilMeasurement
        {
            public string runId;
            public bool punchAccepted, actualHit, recovered;
            public int landed, blocks;
            public float initialDistance, initialBodyTilt, damage, hitAt, maxBodyTilt, maxAbsPitch, maxAbsRoll;
            public float maxHeadWorldDisplacement, maxHeadRelativeDisplacement, maxBodyDisplacement, peakAt, recoverySeconds = -1f;
            public float finalBodyTilt, finalHeadRelativeDisplacement, finalBodySpeed, finalAngularSpeed;
            public string finalState;
        }
        [Serializable] sealed class RecoilSample
        {
            public float afterHit, bodyTilt, pitch, roll, headWorldDisplacement, headRelativeDisplacement, bodyDisplacement, bodySpeed, angularSpeed;
            public string state;
        }
        [Serializable] sealed class RecordingInfo
        {
            public string scenario, startedUtc, endedUtc, reason, timestamps = "frames.csv";
            public int maximumFps = 20, scheduledFrames, writtenFramesAtFinish;
            public double elapsedRealSeconds;
            public string note = "Real Game-view PNGs requested at frame end. Use CSV realSeconds differences for encoding; do not assume constant frame spacing. Capturing may reduce frame rate.";
        }

        static CBSmokePlay()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    if (running) Stop("interrupted", "Play mode ended before the observation window completed.");
                    if (recording) FinishRecording("Play mode ended.");
                }
            };
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                if (running) Stop("interrupted", "Assembly reload interrupted this observation.");
                if (recording) FinishRecording("Assembly reload interrupted recording.");
            };
        }

        static bool ExactScene() => SceneManager.GetActiveScene().path == ScenePath;
        static bool LiveScene() => EditorApplication.isPlaying && !EditorApplication.isPaused && ExactScene();
        static bool FindMatch()
        {
            match = UnityEngine.Object.FindFirstObjectByType<CBMatch>();
            if (match == null || match.gameObject.scene.path != ScenePath) return false;
            player = match.Player; enemy = match.Opponent;
            return player != null && enemy != null && player.Body != null && enemy.Body != null && match.Camera != null;
        }

        static void Update()
        {
            if (EditorApplication.timeSinceStartup >= nextPoll)
            {
                nextPoll = EditorApplication.timeSinceStartup + .2;
                string request = Path.Combine(Folder, "request.txt");
                if (File.Exists(request))
                {
                    try
                    {
                        string command = File.ReadAllText(request).Trim();
                        if (command.Length > 0)
                        {
                            File.WriteAllText(request, "");
                            Command(command);
                        }
                    }
                    catch (Exception e)
                    {
                        if (running) Stop("error", e.ToString());
                        else Status("error", e.ToString());
                        Debug.LogException(e);
                    }
                }
            }
            if (recording) RecordFrame();
            if (!running) return;
            if (!LiveScene() || match == null || player == null || enemy == null)
            { Stop("interrupted", "Scene, Play mode, or fighter changed."); return; }
            // Editor update may run more than once per rendered frame.
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            try { Tick(); }
            catch (Exception e) { Stop("error", e.ToString()); Debug.LogException(e); }
        }

        static void Command(string command)
        {
            Directory.CreateDirectory(Folder);
            if (command == "stop")
            {
                if (running) Stop("interrupted", "Stopped by explicit request.");
                else if (recording) FinishRecording("Stopped by explicit request.");
                else Status("idle", "No observation was running. Play mode was left unchanged.");
                return;
            }
            if (command == "play")
            {
                if (!ExactScene()) { Status("refused", "Open the exact ChessBoxingPrototype scene first; no scene was changed."); return; }
                if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.isPlaying = true;
                Status("requested", "Play requested only for the exact prototype scene. Wait for the match to initialize.");
                return;
            }
            if (command == "rules") { Rules(); return; }
            if (command == "reload")
            {
                if (!LiveScene() || running) { Status("refused", "Reload requires an idle live prototype scene."); return; }
                Status("requested", "Explicit script reload; inspect live snapshot and new errors after reload.");
                EditorUtility.RequestScriptReload();
                return;
            }
            if (!LiveScene() || !FindMatch())
            { Status("refused", "Requires unpaused Play and initialized fighters in the exact prototype scene."); return; }
            if (command == "snapshot")
            {
                if (running) { Capture("requested"); Write("snapshot.json", "observed", "Live snapshot; no feel verdict."); }
                else
                {
                    scenario = "snapshot"; facts.Clear(); samples.Clear(); screenshots.Clear(); orbitSamples.Clear(); orbitShots.Clear(); evadeTrials.Clear(); recoil = null; recoilSamples.Clear(); frames = attempts = accepted = 0;
                    began = Time.time; Capture("current"); Write("snapshot.json", "observed", "Live snapshot; no pass/fail inferred.");
                }
                return;
            }
            if (command == "record:recoil" || command == "record:down")
            {
                if (running) Stop("interrupted", "Replaced by an explicitly requested recording.");
                if (recording) FinishRecording("Replaced by a new recording.");
                Start(command.Substring(7));
                BeginRecording();
                return;
            }
            if (command.StartsWith("run:", StringComparison.Ordinal))
            {
                string name = command.Substring(4);
                if (Array.IndexOf(Names, name) < 0) { Status("refused", "Unknown scenario: " + name); return; }
                if (running) Stop("interrupted", "Replaced by another explicitly requested scenario.");
                if (recording) FinishRecording("Replaced by another explicitly requested scenario.");
                Start(name);
                return;
            }
            Status("refused", "Commands: play, stop, snapshot, rules, record:recoil, record:down, run:" + string.Join("|", Names));
        }

        static void Start(string name)
        {
            scenario = name; previousExternal = match.ExternalControl;
            match.Restart(); match.ExternalControl = true;
            brain = new CBBrain(enemy, player, match.settings); brain.Reset();
            facts.Clear(); samples.Clear(); screenshots.Clear(); orbitSamples.Clear(); orbitShots.Clear(); orbitShot = null;
            evadeTrials.Clear(); evadeTrial = null; combatLastLanded = 0;
            recoil = null; recoilSamples.Clear();
            frames = attempts = accepted = 0; phase = -1; lastFrame = -1;
            maxTilt = maxSpeed = 0f; sawDown = sawRising = sawRecovery = false;
            began = Time.time; nextPunch = 0f; lastStates = "";
            running = true;
            Write("status.json", "running", "Waiting 2.25 game seconds for Restart's countdown. No damage or physics steps are injected.");
        }

        static void Tick()
        {
            frames++;
            float elapsed = Time.time - began;
            if (elapsed < 2.25f) return;
            float t = elapsed - 2.25f;
            ObserveStates(t);
            maxTilt = Mathf.Max(maxTilt, Tilt(player), Tilt(enemy));
            maxSpeed = Mathf.Max(maxSpeed, player.Body.linearVelocity.magnitude, enemy.Body.linearVelocity.magnitude);
            if (scenario == "camera") CameraScenario(t);
            else if (scenario == "hands") HandsScenario(t);
            else if (scenario == "move") MoveScenario(t);
            else if (scenario == "evade") EvadeScenario(t);
            else if (scenario == "evade-hit") EvadeHitScenario(t);
            else if (scenario == "orbit-punch") OrbitPunchScenario(t);
            else if (scenario == "recoil") RecoilScenario(t);
            else CombatScenario(t);
        }

        static void BeginPhase(int number, float clock, float gap = .85f)
        {
            phase = number; phaseBegan = clock; phaseAction = phaseShot = false;
            player.ResetFighter(); enemy.ResetFighter();
            Reposition(player, new Vector3(0f, player.Visual.BodyHeight, -gap * .5f));
            Reposition(enemy, new Vector3(0f, enemy.Visual.BodyHeight, gap * .5f));
            player.Command(Vector3.zero, false); enemy.Command(Vector3.zero, false);
            startHealth = player.Health; startEnemyHealth = enemy.Health;
            startLanded = player.Landed; startBlocks = player.Blocks;
            phasePosition = player.Position; phaseRotation = player.Body.rotation;
            headOrigin = player.Head.position; headMin = headMax = Vector3.zero;
            steadySum = stopSpeed = 0f; steadyFrames = 0;
            commitmentHeld = reentryRejected = true;
        }

        static void Reposition(CBFighter fighter, Vector3 bodyPosition)
        {
            // Setup outside physics callbacks; every actual punch still uses live physical motion.
            Vector3 delta = bodyPosition - fighter.Body.position;
            foreach (var body in new[] { fighter.Body, fighter.Head, fighter.LeftHand, fighter.RightHand })
            {
                body.position += delta; body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.WakeUp();
            }
        }

        static void CameraScenario(float t)
        {
            int desired = Mathf.Min(3, Mathf.FloorToInt(t / 2f));
            if (phase != desired) BeginPhase(desired, t, 1.25f);
            match.Drive(Directions[phase], Vector2.zero, false, false, false);
            enemy.Command(Vector3.zero, false);
            if (t - phaseBegan >= 1.35f && !phaseShot)
            {
                phaseShot = true; Sample sample = Capture("camera-" + phase);
                bool bodyOnScreen = Inside(sample.enemyBodyViewport), headOnScreen = Inside(sample.enemyHeadViewport);
                Add("Camera " + phase + " opponent framed", bodyOnScreen && headOnScreen,
                    "body=" + sample.enemyBodyViewport + ", head=" + sample.enemyHeadViewport);
                if (phase == 0 || phase == 1)
                    Add("Camera shoulder sign " + phase, phase == 0 ? sample.playerViewport.x > .5f : sample.playerViewport.x < .5f,
                        "player viewport x=" + sample.playerViewport.x.ToString("F3"));
                Add("Gloves remain opaque " + phase, sample.player.gloveMaterialCount > 0 && sample.player.gloveMinAlpha > .995f,
                    "glove alpha=" + sample.player.gloveMinAlpha.ToString("F3") + ", body opacity=" + sample.player.opacity.ToString("F3"));
            }
            if (t >= 8f) Complete();
        }

        static void HandsScenario(float t)
        {
            int desired = Mathf.Min(3, Mathf.FloorToInt(t / 2f));
            if (desired != phase)
            {
                if (phase >= 0) FinishHandPhase();
                BeginPhase(desired, t);
            }
            float clock = t - phaseBegan;
            Vector2 intent = new Vector2(phase % 2 == 0 ? -.8f : .8f, phase < 2 ? .4f : -.6f);
            bool punch = clock >= .85f && !phaseAction;
            if (phaseAction && clock > 1f) intent.x *= -1f;
            match.Drive(intent, Vector2.zero, false, punch, false);
            enemy.Command(Vector3.zero, false);
            if (punch)
            {
                phaseAction = true; attempts++;
                if (player.Attacking) accepted++;
                leftCommitted = player.AttackLeft; lowCommitted = player.AttackLow;
                reentryRejected = !player.Punch(false);
                Add("Hand choice " + phase, player.Attacking && leftCommitted == (phase % 2 == 0) && lowCommitted == (phase >= 2),
                    "attacking=" + player.Attacking + ", left=" + leftCommitted + ", body=" + lowCommitted);
            }
            if (phaseAction && player.Attacking)
                commitmentHeld &= player.AttackLeft == leftCommitted && player.AttackLow == lowCommitted;
            if (clock >= 1.18f && !phaseShot) { phaseShot = true; Capture("punch-" + phase); }
            if (t >= 8f) { FinishHandPhase(); Complete(); }
        }

        static void FinishHandPhase()
        {
            Add("Punch cannot restart during active action " + phase, reentryRejected, "Second immediate Punch returned false.");
            Add("Committed hand survives cursor crossing " + phase, commitmentHeld, "Observed AttackLeft/AttackLow while attack was active.");
            Add("Actual unguarded contact " + phase, enemy.Health < startEnemyHealth && player.Landed > 0,
                "enemy health " + startEnemyHealth.ToString("F1") + " -> " + enemy.Health.ToString("F1") + ", landed=" + player.Landed);
            samples.Add(Snapshot("punch-end-" + phase));
        }

        static void MoveScenario(float t)
        {
            int desired = Mathf.Min(2, Mathf.FloorToInt(t / 2.6f));
            if (desired != phase)
            {
                if (phase >= 0) FinishMovePhase();
                BeginPhase(desired, t, 2.8f);
                Reposition(player, new Vector3(0f, player.Visual.BodyHeight, -.45f));
                Reposition(enemy, new Vector3(0f, enemy.Visual.BodyHeight, 2.35f));
                phasePosition = player.Position;
            }
            float clock = t - phaseBegan;
            Vector2 direction = phase == 0 ? Vector2.up : phase == 1 ? Vector2.right : Vector2.down;
            match.Drive(Vector2.up, clock >= .3f && clock < 1.1f ? direction : Vector2.zero, false, false, false);
            enemy.Command(Vector3.zero, false);
            if (clock >= .75f && clock < 1.05f) { steadySum += Flat(player.Body.linearVelocity).magnitude; steadyFrames++; }
            if (clock > 2f) stopSpeed = Flat(player.Body.linearVelocity).magnitude;
            if (clock > 1.05f && !phaseShot) { phaseShot = true; Capture("move-" + phase); }
            if (t >= 7.8f)
            {
                FinishMovePhase();
                Add("Forward faster than strafe and backward", moveSpeeds[0] > moveSpeeds[1] * 1.05f && moveSpeeds[0] > moveSpeeds[2] * 1.05f,
                    "forward=" + moveSpeeds[0].ToString("F3") + ", strafe=" + moveSpeeds[1].ToString("F3") + ", backward=" + moveSpeeds[2].ToString("F3"));
                Complete();
            }
        }

        static void FinishMovePhase()
        {
            moveSpeeds[phase] = steadyFrames > 0 ? steadySum / steadyFrames : 0f;
            Add("Move and stop " + phase, moveSpeeds[phase] > .3f && stopSpeed < .12f,
                "mean speed=" + moveSpeeds[phase].ToString("F3") + ", stop=" + stopSpeed.ToString("F3") + ", travel=" + Vector3.Distance(phasePosition, player.Position).ToString("F3"));
        }

        static void EvadeScenario(float t)
        {
            int desired = Mathf.Min(3, Mathf.FloorToInt(t / 2f));
            if (desired != phase)
            {
                if (phase >= 0) FinishEvadePhase();
                BeginPhase(desired, t, 1.1f);
            }
            float clock = t - phaseBegan;
            bool dodge = clock >= .85f && !phaseAction;
            if (dodge) { headOrigin = player.Head.position; phaseRotation = player.Body.rotation; phaseAction = true; attempts++; }
            match.Drive(Directions[phase], Vector2.zero, false, false, dodge);
            enemy.Command(Vector3.zero, false);
            if (dodge && player.Evading) accepted++;
            if (phaseAction && clock < 1.55f)
            {
                Vector3 delta = Quaternion.Inverse(phaseRotation) * (player.Head.position - headOrigin);
                headMin = Vector3.Min(headMin, delta); headMax = Vector3.Max(headMax, delta);
            }
            if (clock >= 1.08f && !phaseShot) { phaseShot = true; Capture("evade-" + phase); }
            if (t >= 8f) { FinishEvadePhase(); Complete(); }
        }

        static void FinishEvadePhase()
        {
            bool moved = phase == 0 ? headMin.x < -.05f : phase == 1 ? headMax.x > .05f
                : phase == 2 ? headMin.y < -.05f : headMin.z < -.05f;
            Add("Physical head evade " + phase, moved, "min=" + headMin + ", max=" + headMax + " in initial fighter axes; no invulnerability claim.");
        }

        static void RecoilScenario(float t)
        {
            if (phase < 0)
            {
                BeginPhase(0, t, .85f);
                recoil = new RecoilMeasurement { runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") };
                recoilHitClock = recoilStableSince = -1f; recoilLastFixedTime = -1f;
            }
            float clock = t - phaseBegan;
            bool punch = clock >= .85f && !phaseAction;
            if (punch)
            {
                recoilBodyOrigin = enemy.Position; recoilHeadOrigin = enemy.Head.position;
                recoilHeadLocal = Quaternion.Inverse(enemy.Body.rotation) * (enemy.Head.position - enemy.Position);
                recoilYaw = Quaternion.LookRotation(enemy.Facing);
                recoil.initialDistance = Flat(enemy.Position - player.Position).magnitude;
                recoil.initialBodyTilt = Tilt(enemy);
                Capture(recoil.runId + "-before");
            }
            // Exactly one ordinary mouse punch; both fighters otherwise receive zero movement and no guard.
            match.Drive(new Vector2(-.8f, .4f), Vector2.zero, false, punch, false);
            enemy.Command(Vector3.zero, false);
            if (punch)
            {
                phaseAction = true; attempts++;
                recoil.punchAccepted = player.Attacking && player.AttackLeft && !player.HeavyAttack;
                if (recoil.punchAccepted) accepted++;
            }
            if (!recoil.actualHit && player.Landed > 0 && enemy.Health < startEnemyHealth)
            {
                recoil.actualHit = true; recoilHitClock = t; recoil.hitAt = t;
                samples.Add(Snapshot("recoil-contact"));
            }
            if (recoil.actualHit)
            {
                float afterHit = t - recoilHitClock;
                if (Time.fixedTime != recoilLastFixedTime)
                {
                    recoilLastFixedTime = Time.fixedTime;
                    Vector3 localUp = Quaternion.Inverse(recoilYaw) * (enemy.Body.rotation * Vector3.up);
                    Vector3 headLocal = Quaternion.Inverse(enemy.Body.rotation) * (enemy.Head.position - enemy.Position);
                    var sample = new RecoilSample
                    {
                        afterHit = afterHit, bodyTilt = Tilt(enemy),
                        pitch = Mathf.Atan2(localUp.z, localUp.y) * Mathf.Rad2Deg,
                        roll = Mathf.Atan2(-localUp.x, localUp.y) * Mathf.Rad2Deg,
                        headWorldDisplacement = Vector3.Distance(recoilHeadOrigin, enemy.Head.position),
                        headRelativeDisplacement = Vector3.Distance(recoilHeadLocal, headLocal),
                        bodyDisplacement = Vector3.Distance(recoilBodyOrigin, enemy.Position),
                        bodySpeed = enemy.Body.linearVelocity.magnitude,
                        angularSpeed = enemy.Body.angularVelocity.magnitude, state = enemy.State.ToString()
                    };
                    recoilSamples.Add(sample);
                    if (sample.bodyTilt > recoil.maxBodyTilt) { recoil.maxBodyTilt = sample.bodyTilt; recoil.peakAt = afterHit; }
                    recoil.maxAbsPitch = Mathf.Max(recoil.maxAbsPitch, Mathf.Abs(sample.pitch));
                    recoil.maxAbsRoll = Mathf.Max(recoil.maxAbsRoll, Mathf.Abs(sample.roll));
                    recoil.maxHeadWorldDisplacement = Mathf.Max(recoil.maxHeadWorldDisplacement, sample.headWorldDisplacement);
                    recoil.maxHeadRelativeDisplacement = Mathf.Max(recoil.maxHeadRelativeDisplacement, sample.headRelativeDisplacement);
                    recoil.maxBodyDisplacement = Mathf.Max(recoil.maxBodyDisplacement, sample.bodyDisplacement);
                    recoil.finalBodyTilt = sample.bodyTilt; recoil.finalHeadRelativeDisplacement = sample.headRelativeDisplacement;
                    recoil.finalBodySpeed = sample.bodySpeed; recoil.finalAngularSpeed = sample.angularSpeed;
                    recoil.finalState = sample.state;
                    bool settled = afterHit >= .2f && enemy.State == CBBodyState.Standing
                        && sample.bodyTilt <= Mathf.Max(4f, recoil.initialBodyTilt + 2f)
                        && sample.headRelativeDisplacement < .04f && sample.bodySpeed < .2f && sample.angularSpeed < .75f;
                    if (settled)
                    {
                        if (recoilStableSince < 0f) recoilStableSince = afterHit;
                        if (!recoil.recovered && afterHit - recoilStableSince >= .15f)
                        { recoil.recovered = true; recoil.recoverySeconds = recoilStableSince; }
                    }
                    else
                    {
                        recoilStableSince = -1f;
                        // Do not report the first quiet frame as a final recovery if a later rebound follows.
                        recoil.recovered = false; recoil.recoverySeconds = -1f;
                    }
                }
                if (afterHit >= .14f && !phaseShot) { phaseShot = true; Capture(recoil.runId + "-reaction"); }
                if (afterHit < 2f) return;
            }
            else if (clock < 2.8f) return;

            recoil.landed = player.Landed; recoil.blocks = enemy.Blocks; recoil.damage = startEnemyHealth - enemy.Health;
            Add("Exactly one actual unguarded normal punch", recoil.punchAccepted && recoil.actualHit && accepted == 1 && recoil.landed == 1 && recoil.blocks == 0,
                "accepted=" + accepted + ", landed=" + recoil.landed + ", blocks=" + recoil.blocks + ", damage=" + recoil.damage.ToString("F1"));
            Add("Single-hit body visibly tilts", recoil.actualHit && recoil.maxBodyTilt - recoil.initialBodyTilt >= 5f,
                "initial tilt=" + recoil.initialBodyTilt.ToString("F2") + ", peak=" + recoil.maxBodyTilt.ToString("F2")
                + ", abs pitch/roll=" + recoil.maxAbsPitch.ToString("F2") + "/" + recoil.maxAbsRoll.ToString("F2")
                + " degrees. Five degrees is this probe's explicit observation threshold, not human feel approval.");
            Add("Articulated head responds relative to torso", recoil.actualHit && recoil.maxHeadRelativeDisplacement >= .02f,
                "head relative peak=" + recoil.maxHeadRelativeDisplacement.ToString("F4")
                + ", world peak=" + recoil.maxHeadWorldDisplacement.ToString("F4") + ", torso travel=" + recoil.maxBodyDisplacement.ToString("F4"));
            Add("Single-hit stance settles within two seconds", recoil.actualHit && recoil.recovered && enemy.State == CBBodyState.Standing,
                "recovery=" + recoil.recoverySeconds.ToString("F3") + " s, final tilt=" + recoil.finalBodyTilt.ToString("F2")
                + ", head relative=" + recoil.finalHeadRelativeDisplacement.ToString("F4") + ", speed=" + recoil.finalBodySpeed.ToString("F3")
                + ", angular speed=" + recoil.finalAngularSpeed.ToString("F3") + ", state=" + enemy.State);
            Capture(recoil.runId + "-after-two-seconds");
            Complete();
        }

        static void EvadeHitScenario(float t)
        {
            int desired = Mathf.Min(4, Mathf.FloorToInt(t / 2f));
            if (phase != desired)
            {
                if (phase >= 0) FinishEvadeTrial();
                BeginPhase(desired, t, .85f);
                trialDodged = false;
                evadeTrial = new EvadeTrial { action = phase == 0 ? "baseline" : ((CBDirection)(phase - 1)).ToString() };
                evadeTrials.Add(evadeTrial);
            }
            float clock = t - phaseBegan;
            Vector2 intent = phase == 0 ? Vector2.up : Directions[phase - 1];
            bool dodge = phase > 0 && phaseAction && !trialDodged && clock >= .92f;
            if (dodge)
            {
                trialDodged = true; headOrigin = player.Head.position; phaseRotation = player.Body.rotation;
                headMin = headMax = Vector3.zero;
            }
            match.Drive(intent, Vector2.zero, false, false, dodge);
            if (dodge) evadeTrial.dodgeAccepted = player.Evading;
            enemy.Command(Vector3.zero, false);
            if (clock >= .85f && !phaseAction)
            {
                phaseAction = true; attempts++;
                // Same unguarded head punch and initial distance in baseline and all four dodges.
                evadeTrial.attackAccepted = enemy.PunchSelected(true, false);
                if (evadeTrial.attackAccepted) accepted++;
            }
            if (trialDodged && clock < 1.6f)
            {
                Vector3 delta = Quaternion.Inverse(phaseRotation) * (player.Head.position - headOrigin);
                headMin = Vector3.Min(headMin, delta); headMax = Vector3.Max(headMax, delta);
                evadeTrial.minimumHeadDelta = headMin; evadeTrial.maximumHeadDelta = headMax;
            }
            if (enemy.Attacking && !enemy.WindingUp)
            {
                Vector3 glove = enemy.LeftHand.position;
                float radius = enemy.Visual.HandRadius;
                Collider head = player.Head.GetComponent<Collider>(), body = player.Body.GetComponent<Collider>();
                evadeTrial.minimumHeadGap = Mathf.Min(evadeTrial.minimumHeadGap, Vector3.Distance(glove, head.ClosestPoint(glove)) - radius);
                evadeTrial.minimumBodyGap = Mathf.Min(evadeTrial.minimumBodyGap, Vector3.Distance(glove, body.ClosestPoint(glove)) - radius);
            }
            if (clock >= 1.13f && !phaseShot) { phaseShot = true; Capture("evade-hit-" + evadeTrial.action); }
            if (t >= 10f) { FinishEvadeTrial(); Complete(); }
        }

        static void FinishEvadeTrial()
        {
            evadeTrial.damage = startHealth - player.Health;
            evadeTrial.contactPart = player.LastContactPart;
            evadeTrial.landed = enemy.Landed;
            evadeTrial.blocks = player.Blocks;
            if (phase == 0)
                Add("Evade baseline is a real damaging head attack", evadeTrial.attackAccepted && evadeTrial.landed > 0 && evadeTrial.damage > 0f,
                    "damage=" + evadeTrial.damage.ToString("F1") + ", landed=" + evadeTrial.landed);
            else
            {
                EvadeTrial baseline = evadeTrials[0];
                Add("Real head attack avoided " + evadeTrial.action,
                    baseline.damage > 0f && baseline.landed > 0 && evadeTrial.attackAccepted && evadeTrial.dodgeAccepted
                    && evadeTrial.landed == 0 && evadeTrial.blocks == 0 && evadeTrial.damage < .01f,
                    "baseline damage=" + baseline.damage.ToString("F1") + ", evade damage=" + evadeTrial.damage.ToString("F1")
                    + ", landed=" + evadeTrial.landed + ", blocks=" + evadeTrial.blocks + ", contact=" + evadeTrial.contactPart
                    + ", closest head/body gap=" + evadeTrial.minimumHeadGap.ToString("F3") + "/" + evadeTrial.minimumBodyGap.ToString("F3")
                    + "; movement alone cannot pass this check.");
            }
            samples.Add(Snapshot("evade-hit-end-" + evadeTrial.action));
        }

        static void OrbitPunchScenario(float t)
        {
            int desired = Mathf.Min(1, Mathf.FloorToInt(t / 5f));
            if (phase != desired)
            {
                if (phase >= 0) FinishOrbitPhase();
                BeginPhase(desired, t, .80f);
                orbitArc = 0f; orbitLastYaw = OrbitYaw(); orbitNextSample = t;
                orbitShotCount = orbitPhaseAccepted = 0; orbitPhaseLanded = player.Landed;
                orbitShot = null;
            }
            float clock = t - phaseBegan;
            float yaw = OrbitYaw();
            orbitArc += Mathf.DeltaAngle(orbitLastYaw, yaw); orbitLastYaw = yaw;
            float turned = Mathf.Abs(orbitArc);
            bool moving = clock > .25f && turned < 178f;
            Vector2 movement = moving ? (phase == 0 ? Vector2.left : Vector2.right) : Vector2.zero;
            // Both hands are tested after substantial real orbiting. No mid-flight repositioning.
            bool requestPunch = !player.Attacking && orbitShotCount < 2
                && (orbitShotCount == 0 ? clock > .85f && (turned >= 60f || clock >= 1.7f)
                    : clock > 2.1f && (turned >= 125f || clock >= 3.4f));
            if (orbitShot != null) ObserveOrbitShot(t);
            if (requestPunch)
            {
                FinishOrbitShot();
                bool left = orbitShotCount == 0;
                Vector3 toward = Flat(enemy.Position - player.Position).normalized;
                orbitShot = new OrbitShot
                {
                    movement = phase == 0 ? "A" : "D", left = left, began = t, launchArc = turned,
                    launchDistance = Flat(enemy.Position - player.Position).magnitude,
                    launchBodyError = Vector3.Angle(player.Facing, toward),
                    launchHeadError = Vector3.Angle(Flat(player.Head.rotation * Vector3.forward), toward),
                    landedBefore = player.Landed, peakAxisError = 180f, peakGloveForwardError = 180f
                };
                orbitShots.Add(orbitShot);
                phaseShot = false;
                match.Drive(new Vector2(left ? -.8f : .8f, .4f), movement, false, true, false);
                attempts++; orbitShotCount++;
                orbitShot.accepted = player.Attacking && player.AttackLeft == left;
                if (orbitShot.accepted) { accepted++; orbitPhaseAccepted++; }
                samples.Add(Snapshot("orbit-" + phase + "-launch-" + orbitShotCount));
            }
            else
            {
                bool left = orbitShotCount <= 1;
                match.Drive(new Vector2(left ? -.8f : .8f, .4f), movement, false, false, false);
            }
            enemy.Command(Vector3.zero, false);
            if (t >= orbitNextSample)
            {
                orbitNextSample = t + .1f;
                Vector3 toward = Flat(enemy.Position - player.Position).normalized;
                Vector3 bodyForward = player.Body.rotation * Vector3.forward;
                Vector3 headForward = player.Head.rotation * Vector3.forward;
                Vector3 leftForward = player.LeftHand.rotation * Vector3.forward;
                Vector3 rightForward = player.RightHand.rotation * Vector3.forward;
                orbitSamples.Add(new OrbitSample
                {
                    time = t, movement = phase == 0 ? "A" : "D", arcDegrees = orbitArc,
                    distance = Flat(enemy.Position - player.Position).magnitude,
                    bodyTargetError = Vector3.Angle(Flat(bodyForward), toward), headTargetError = Vector3.Angle(Flat(headForward), toward),
                    leftGloveTargetError = Vector3.Angle(Flat(leftForward), toward), rightGloveTargetError = Vector3.Angle(Flat(rightForward), toward),
                    attacking = player.Attacking, leftHand = player.AttackLeft,
                    bodyForward = bodyForward, headForward = headForward, leftGloveForward = leftForward, rightGloveForward = rightForward
                });
            }
            if (orbitShot != null && t - orbitShot.began > .33f && !phaseShot)
            { phaseShot = true; Capture("orbit-" + phase + "-active-" + orbitShotCount); }
            if (t >= 10f) { FinishOrbitPhase(); Complete(); }
        }

        static float OrbitYaw()
        {
            Vector3 radial = Flat(player.Position - enemy.Position);
            return Mathf.Atan2(radial.x, radial.z) * Mathf.Rad2Deg;
        }

        static void ObserveOrbitShot(float t)
        {
            if (orbitShot == null || !orbitShot.accepted) return;
            orbitShot.landed = player.Landed - orbitShot.landedBefore;
            if (!player.Attacking || player.AttackLeft != orbitShot.left) return;
            Rigidbody glove = orbitShot.left ? player.LeftHand : player.RightHand;
            Vector3 toward = Flat(enemy.Position - player.Position).normalized;
            Vector3 reach = Flat(glove.position - player.Position);
            float forward = Vector3.Dot(reach, toward);
            if (forward > orbitShot.peakForwardReach)
            {
                orbitShot.peakForwardReach = forward;
                orbitShot.peakAxisError = Vector3.Angle(reach, toward);
                orbitShot.peakGloveForwardError = Vector3.Angle(Flat(glove.rotation * Vector3.forward), toward);
            }
            if (player.WindingUp) return;
            float radius = player.Visual.HandRadius;
            Collider head = enemy.Head.GetComponent<Collider>(), body = enemy.Body.GetComponent<Collider>();
            orbitShot.closestHeadGap = Mathf.Min(orbitShot.closestHeadGap, Vector3.Distance(glove.position, head.ClosestPoint(glove.position)) - radius);
            orbitShot.closestBodyGap = Mathf.Min(orbitShot.closestBodyGap, Vector3.Distance(glove.position, body.ClosestPoint(glove.position)) - radius);
        }

        static void FinishOrbitShot()
        {
            if (orbitShot == null) return;
            orbitShot.landed = player.Landed - orbitShot.landedBefore;
            string label = orbitShot.movement + " " + (orbitShot.left ? "left" : "right");
            Add("Orbit punch alignment " + label, orbitShot.accepted && orbitShot.peakForwardReach > .35f && orbitShot.peakAxisError < 35f,
                "launch arc=" + orbitShot.launchArc.ToString("F1") + ", body error=" + orbitShot.launchBodyError.ToString("F1")
                + ", peak forward reach=" + orbitShot.peakForwardReach.ToString("F3") + ", peak axis error=" + orbitShot.peakAxisError.ToString("F1")
                + ", glove forward error=" + orbitShot.peakGloveForwardError.ToString("F1"));
            Add("Orbit real contact " + label, orbitShot.landed > 0,
                "landed=" + orbitShot.landed + ", launch distance=" + orbitShot.launchDistance.ToString("F3")
                + ", closest head/body gap=" + orbitShot.closestHeadGap.ToString("F3") + "/" + orbitShot.closestBodyGap.ToString("F3")
                + "; a miss is retained, not interpreted as the same root cause as a stale punch direction.");
            orbitShot = null;
        }

        static void FinishOrbitPhase()
        {
            FinishOrbitShot();
            Add("Real A/D orbit " + (phase == 0 ? "A" : "D"), Mathf.Abs(orbitArc) >= 60f && orbitPhaseAccepted == 2,
                "turn=" + orbitArc.ToString("F1") + " degrees, accepted=" + orbitPhaseAccepted + ", landed=" + (player.Landed - orbitPhaseLanded));
            samples.Add(Snapshot("orbit-end-" + phase));
        }

        static void CombatScenario(float t)
        {
            if (phase < 0)
            {
                float gap = scenario == "ai" || scenario == "ai-match" ? 2.1f : scenario == "down" || scenario == "ko" ? .80f : .85f;
                BeginPhase(0, t, gap); nextPunch = .95f;
            }
            bool guard = scenario == "guard-upper" || scenario == "guard-wrong";
            match.Drive(guard ? Vector2.down : new Vector2(-.7f, .5f), Vector2.zero, guard, false, false);
            enemy.Command(Vector3.zero, false);
            if (scenario == "ai") brain.Tick(Time.deltaTime);
            else if (scenario == "ai-match")
            {
                brain.Tick(Time.deltaTime);
                Vector3 toward = Flat(enemy.Position - player.Position);
                if (!player.Attacking) player.Command(toward.magnitude > .72f ? toward.normalized : Vector3.zero, false);
                if (t >= nextPunch && toward.magnitude < 1f && enemy.State == CBBodyState.Standing)
                { attempts++; if (player.Punch(true)) accepted++; nextPunch = t + 1.15f; }
            }
            else if (guard && t >= nextPunch && t < 7f)
            {
                // Re-establish the same setup between attacks, never while a glove is in flight.
                if (!enemy.Attacking)
                {
                    Reposition(enemy, player.Position + player.Facing * .85f);
                    enemy.SetIntent(Vector2.down);
                    attempts++;
                    if (enemy.PunchSelected(attempts % 2 == 1, false, scenario == "guard-upper")) accepted++;
                    nextPunch = t + 2f;
                }
            }
            else if (scenario == "down" || scenario == "ko")
            {
                // Verify the current public mouse attack, not the legacy heavy Punch(true) path.
                // Track the target by real forward/back movement; never reset health or teleport between punches.
                float distance = Flat(enemy.Position - player.Position).magnitude;
                bool observingRecovery = scenario == "down" && sawDown;
                bool canContinue = !observingRecovery && enemy.State == CBBodyState.Standing;
                float walk = canContinue ? Mathf.Clamp((distance - .76f) * 3f, -.25f, 1f) : 0f;
                bool left = accepted % 2 == 0;
                bool punch = canContinue && t >= nextPunch && !player.Attacking && !player.Stunned
                    && player.State == CBBodyState.Standing && distance < .93f;
                match.Drive(new Vector2(left ? -.8f : .8f, .4f), new Vector2(0f, walk), false, punch, false);
                if (punch)
                {
                    attempts++;
                    if (player.Attacking) accepted++;
                    nextPunch = t + .72f;
                }
                if (player.Landed != combatLastLanded)
                {
                    combatLastLanded = player.Landed;
                    File.AppendAllText(Path.Combine(Folder, "events.txt"), DateTime.UtcNow.ToString("O") + " " + scenario
                        + " " + t.ToString("F3") + " real hit=" + player.Landed + " enemy HP=" + enemy.Health.ToString("F1")
                        + " state=" + enemy.State + "\n");
                    samples.Add(Snapshot("contact-" + player.Landed));
                }
            }
            if (t >= 1.15f && !phaseShot) { phaseShot = true; Capture(scenario + "-contact"); }
            float limit = scenario == "ko" ? 30f : scenario == "ai" || scenario == "ai-match" || scenario == "down" ? 10f : 8f;
            if (t < limit && !(scenario == "down" && sawRecovery)
                && player.State != CBBodyState.KnockedOut && enemy.State != CBBodyState.KnockedOut) return;
            if (scenario == "guard-upper")
                Add("Low guard physically intercepts uppercut", player.Blocks > startBlocks && accepted > 0,
                    "accepted=" + accepted + ", blocks=" + player.Blocks + ", HP loss=" + (startHealth - player.Health).ToString("F1"));
            else if (scenario == "guard-wrong")
                Add("Low guard leaves head attack exposed", enemy.Landed > 0 && startHealth - player.Health > player.Blocks,
                    "enemy landed=" + enemy.Landed + ", player blocks=" + player.Blocks + ", HP loss=" + (startHealth - player.Health).ToString("F1"));
            else if (scenario == "down")
            {
                Add("Down prerequisite: real normal-punch contacts", player.Landed >= 4,
                    "accepted=" + accepted + ", landed=" + player.Landed + ", enemy health=" + enemy.Health.ToString("F1"));
                Add("Actual impacts cause down and recovery", sawDown && sawRising && sawRecovery,
                    "Down=" + sawDown + ", Rising=" + sawRising + ", Standing again=" + sawRecovery);
            }
            else if (scenario == "ai") Add("AI makes real damaging contact", enemy.Landed > 0 && player.Health < startHealth,
                "AI landed=" + enemy.Landed + ", player HP=" + player.Health.ToString("F1"));
            else if (scenario == "ko") Add("Normal player inputs reach KO with default health", enemy.State == CBBodyState.KnockedOut && enemy.Health <= 0f,
                "accepted=" + accepted + ", real landed=" + player.Landed + ", enemy health=" + enemy.Health.ToString("F1")
                + ", elapsed=" + t.ToString("F2") + "; opponent uses zero movement/guard. No damage or health values are injected.");
            else if (scenario == "ai-match") Add("AI match contact observed", player.Landed > 0 || enemy.Landed > 0,
                "player hits=" + player.Landed + ", AI hits=" + enemy.Landed + "; this short sparring window does not require KO.");
            else Add("Idle stays upright", player.State == CBBodyState.Standing && enemy.State == CBBodyState.Standing && maxTilt < 35f,
                "max tilt=" + maxTilt.ToString("F1"));
            Capture(scenario + "-end"); Complete();
        }

        static void ObserveStates(float time)
        {
            CBFighter observed = scenario == "down" || scenario == "ko" ? enemy : player;
            if (observed.State == CBBodyState.Down) sawDown = true;
            if (sawDown && observed.State == CBBodyState.Rising) sawRising = true;
            if (sawRising && observed.State == CBBodyState.Standing) sawRecovery = true;
            string states = player.State + "/" + enemy.State;
            if (lastStates == states) return;
            lastStates = states;
            File.AppendAllText(Path.Combine(Folder, "events.txt"), DateTime.UtcNow.ToString("O") + " " + scenario + " " + time.ToString("F3") + " " + states + "\n");
        }

        static void Complete()
        {
            bool okay = facts.Count > 0 && facts.TrueForAll(f => f.passed);
            Stop(okay ? "passed" : "failed", "These are bounded AI observations of input, poses, and live contacts. Human handfeel, prolonged stability, and player-build behavior remain unverified.");
        }

        static void Stop(string status, string note)
        {
            if (!running) return;
            try
            {
                if (player != null && enemy != null && player.Body != null && enemy.Body != null) samples.Add(Snapshot("final"));
                running = false; Write(scenario + ".json", status, note); Write("status.json", status, note);
                if (scenario == "recoil" && recoil != null) Write("recoil-" + recoil.runId + ".json", status, note);
            }
            finally
            {
                running = false;
                if (player != null) player.Command(Vector3.zero, false);
                if (enemy != null) enemy.Command(Vector3.zero, false);
                // A short quiet tail completes a six-second review clip without changing any measured facts.
                // No further scenario ticks run after this point; natural physics and rendering continue.
                bool keepTail = recording && (status == "passed" || status == "failed")
                    && Time.realtimeSinceStartupAsDouble - recordingBegan < 6d;
                recordingTail = keepTail;
                if (match != null) match.ExternalControl = keepTail ? true : previousExternal;
                if (recording && !keepTail) FinishRecording("Observation finished: " + status);
            }
        }

        static void BeginRecording()
        {
            recordingFolder = Path.Combine(Folder, "record-" + scenario + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
            Directory.CreateDirectory(recordingFolder);
            recordingInfo = new RecordingInfo { scenario = scenario, startedUtc = DateTime.UtcNow.ToString("O") };
            File.WriteAllText(Path.Combine(recordingFolder, "frames.csv"), "index,file,realSeconds,gameSeconds,frameCount,fixedTime\n");
            File.WriteAllText(Path.Combine(recordingFolder, "recording.json"), JsonUtility.ToJson(recordingInfo, true));
            File.WriteAllText(Path.Combine(Folder, "recording-path.txt"), recordingFolder);
            recordingBegan = recordingNextFrame = Time.realtimeSinceStartupAsDouble;
            recordingGameBegan = Time.time;
            recordingFrames = 0; recordingLastFrame = -1;
            recordingTail = false; recording = true;
        }

        static void RecordFrame()
        {
            if (!LiveScene() || match == null)
            { FinishRecording("Stopped because the exact live scene was unavailable."); return; }
            double now = Time.realtimeSinceStartupAsDouble;
            double duration = now - recordingBegan;
            if (duration >= 10d || recordingTail && duration >= 6d)
            { FinishRecording(recordingTail ? "Observation complete; quiet tail finished." : "Ten-second capture limit reached; scenario may continue."); return; }
            if (now < recordingNextFrame || Time.frameCount == recordingLastFrame) return;
            recordingNextFrame = now + .05d; recordingLastFrame = Time.frameCount;
            string file = "frame-" + recordingFrames.ToString("D4") + ".png";
            ScreenCapture.CaptureScreenshot(Path.Combine(recordingFolder, file));
            File.AppendAllText(Path.Combine(recordingFolder, "frames.csv"), FormattableString.Invariant(
                $"{recordingFrames},{file},{duration:F6},{Time.time - recordingGameBegan:F6},{Time.frameCount},{Time.fixedTime:F6}\n"));
            recordingFrames++;
        }

        static void FinishRecording(string reason)
        {
            if (!recording) return;
            recording = false;
            recordingInfo.endedUtc = DateTime.UtcNow.ToString("O");
            recordingInfo.reason = reason;
            recordingInfo.elapsedRealSeconds = Time.realtimeSinceStartupAsDouble - recordingBegan;
            recordingInfo.scheduledFrames = recordingFrames;
            recordingInfo.writtenFramesAtFinish = Directory.GetFiles(recordingFolder, "frame-*.png").Length;
            File.WriteAllText(Path.Combine(recordingFolder, "recording.json"), JsonUtility.ToJson(recordingInfo, true));
            if (recordingTail && match != null)
            {
                if (player != null) player.Command(Vector3.zero, false);
                if (enemy != null) enemy.Command(Vector3.zero, false);
                match.ExternalControl = previousExternal;
            }
            recordingTail = false;
        }

        static Sample Capture(string label)
        {
            Sample sample = Snapshot(label); samples.Add(sample);
            string file = scenario + "-" + label + ".png";
            ScreenCapture.CaptureScreenshot(Path.Combine(Folder, file)); screenshots.Add(file);
            return sample;
        }

        static Sample Snapshot(string label)
        {
            Camera camera = match.Camera;
            return new Sample
            {
                phase = label, elapsed = Time.time - began, intent = match.Control.Position,
                cameraPosition = camera.transform.position, cameraDistance = Vector3.Distance(camera.transform.position, player.Position),
                playerViewport = camera.WorldToViewportPoint(player.Head.position),
                enemyHeadViewport = camera.WorldToViewportPoint(enemy.Head.position),
                enemyBodyViewport = camera.WorldToViewportPoint(enemy.Position + Vector3.up * .12f),
                player = SampleFighter(player), enemy = SampleFighter(enemy)
            };
        }

        static FighterSample SampleFighter(CBFighter fighter)
        {
            float gloveAlpha = 1f; int gloveMaterials = 0;
            foreach (var renderer in fighter.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || !material.name.StartsWith("CB glove", StringComparison.Ordinal)) continue;
                    gloveMaterials++;
                    if (material.HasProperty("_BaseColor")) gloveAlpha = Mathf.Min(gloveAlpha, material.GetColor("_BaseColor").a);
                    else if (material.HasProperty("_Color")) gloveAlpha = Mathf.Min(gloveAlpha, material.GetColor("_Color").a);
                }
            return new FighterSample
            {
                state = fighter.State.ToString(), lastEvent = fighter.LastEvent, health = fighter.Health, stamina = fighter.Stamina,
                opacity = fighter.Visual.BodyOpacity, removedArmTriangles = fighter.Visual.RemovedArmTriangles,
                gloveMinAlpha = gloveAlpha, gloveMaterialCount = gloveMaterials,
                speed = fighter.Body.linearVelocity.magnitude, tilt = Tilt(fighter), landed = fighter.Landed, blocks = fighter.Blocks,
                guarding = fighter.Guarding, attacking = fighter.Attacking, attackLeft = fighter.AttackLeft,
                attackLow = fighter.AttackLow, uppercut = fighter.AttackUppercut,
                position = fighter.Position, head = fighter.Head.position, leftGlove = fighter.LeftHand.position, rightGlove = fighter.RightHand.position,
                bodyForward = fighter.Body.rotation * Vector3.forward, headForward = fighter.Head.rotation * Vector3.forward,
                leftGloveForward = fighter.LeftHand.rotation * Vector3.forward, rightGloveForward = fighter.RightHand.rotation * Vector3.forward
            };
        }

        static void Add(string name, bool passed, string evidence) => facts.Add(new Fact { name = name, passed = passed, evidence = evidence });
        static bool Inside(Vector3 viewport) => viewport.z > 0f && viewport.x > .04f && viewport.x < .96f && viewport.y > .04f && viewport.y < .96f;
        static float Tilt(CBFighter fighter) => Vector3.Angle(fighter.Body.rotation * Vector3.up, Vector3.up);
        static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);

        static void Write(string file, string status, string note)
        {
            Directory.CreateDirectory(Folder);
            var report = new Report
            {
                utc = DateTime.UtcNow.ToString("O"), scenario = scenario, status = status, note = note,
                running = running, passed = status == "passed", frames = frames, attempts = attempts, accepted = accepted,
                elapsed = EditorApplication.isPlaying ? Time.time - began : 0f, maxTilt = maxTilt, maxSpeed = maxSpeed,
                sawDown = sawDown, sawRising = sawRising, sawRecovery = sawRecovery,
                facts = facts.ToArray(), samples = samples.ToArray(), screenshots = screenshots.ToArray(),
                orbitSamples = orbitSamples.ToArray(), orbitShots = orbitShots.ToArray(), evadeTrials = evadeTrials.ToArray(),
                recoil = recoil, recoilSamples = recoilSamples.ToArray()
            };
            File.WriteAllText(Path.Combine(Folder, file), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(Folder, "status.txt"), status + ": " + note);
        }

        static void Status(string status, string note)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "status.txt"), status + ": " + note);
        }

        public static void RunRules() => Rules();

        static void Rules()
        {
            if (running) { Status("refused", "Stop the running scenario before checking pure input rules."); return; }
            scenario = "rules"; facts.Clear(); samples.Clear(); screenshots.Clear(); orbitSamples.Clear(); orbitShots.Clear(); evadeTrials.Clear(); recoil = null; recoilSamples.Clear(); frames = attempts = accepted = 0;
            var control = new CBControl();
            foreach (CBDirection direction in Enum.GetValues(typeof(CBDirection)))
            {
                Vector2 input = direction == CBDirection.Left ? Vector2.left : direction == CBDirection.Right ? Vector2.right
                    : direction == CBDirection.High ? Vector2.up : Vector2.down;
                control.Set(input); Add("Direction " + direction, control.Direction == direction, input.ToString());
            }
            control.Set(Vector2.left); control.Set(new Vector2(.17f, 0f));
            Add("Center band preserves left hand", control.LeftHand, "x=+0.17 after left selection");
            control.Set(Vector2.right); control.Set(new Vector2(-.17f, 0f));
            Add("Center band preserves right hand", !control.LeftHand, "x=-0.17 after right selection");
            control.Set(new Vector2(-.19f, 0f)); Add("Left selection threshold", control.LeftHand, "x=-0.19");
            control.Set(new Vector2(.19f, 0f)); Add("Right selection threshold", !control.LeftHand, "x=+0.19");
            control.Set(new Vector2(0f, -.30f)); Add("Body threshold boundary", !control.BodyPunch, "y=-0.30");
            control.Set(new Vector2(0f, -.31f)); Add("Body threshold below boundary", control.BodyPunch, "y=-0.31");
            control.Set(Vector2.zero); Add("Dead zone retains direction", control.Direction == CBDirection.Low, "zero after Low");
            control.Set(new Vector2(10f, 10f)); Add("Cursor magnitude bounded", control.Position.magnitude <= 1.00001f, control.Position.ToString());
            control.Reset(); Add("Reset chooses left hand and left direction", control.LeftHand && control.Direction == CBDirection.Left && !control.BodyPunch, control.Position.ToString());
            Write("rules.json", facts.TrueForAll(f => f.passed) ? "passed" : "failed", "Pure input mapping checks only. These do not verify physical contact or handfeel.");
        }
    }
}
