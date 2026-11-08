using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush.Editor
{
    // Runs the production effect in Play Mode: its collider cleanup uses Object.Destroy.
    // No scene or asset is saved, and there is deliberately no additional menu item.
    [InitializeOnLoad]
    public static class AirCannonFxValidation
    {
        const string ActiveKey = "ChessFight.R498.AirCannon.Active";
        const string ExitKey = "ChessFight.R498.AirCannon.Exit";
        const string CodeKey = "ChessFight.R498.AirCannon.Code";
        const string ReportPath = "Logs/AirCannonFxValidation/R498.txt";
        const string OrbitalDiagnostic = "Particle Orbital Velocity curves must all be in the same mode";
        static readonly List<string> issues = new List<string>();
        static readonly List<string> report = new List<string>();
        static GameObject holder;
        static double idleAt;
        static string currentCase;

        static AirCannonFxValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("AirCannonFxValidation.RunBatch is an isolated batch-only check.");
            SessionState.SetBool(ActiveKey, true);
            idleAt = 0;
            EditorApplication.update += RequestPlay;
        }

        static void RequestPlay()
        {
            // Let import and search initialization settle before the Play Mode domain reload.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { idleAt = 0; return; }
            if (idleAt == 0) idleAt = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - idleAt < 2) return;
            EditorApplication.update -= RequestPlay;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
                EditorApplication.delayCall += RunChecks;
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false))
                EditorApplication.delayCall += CompleteExit;
        }

        static void RunChecks()
        {
            issues.Clear();
            report.Clear();
            report.Add("R498 air cannon orbital velocity regression");
            report.Add("Production Build, charge/fire, explicit particle emission and native simulation in Play Mode.");
            report.Add("Not visual approval, a complete map run, multiplayer, or a performance check.");
            Application.logMessageReceived += OnLog;
            try
            {
                holder = new GameObject("R498 temporary effect validation");
                CheckIndependentGroups();
                var directions = new[] { Vector3.forward, Vector3.right, new Vector3(1, .3f, .5f).normalized };
                for (int i = 0; i < directions.Length; i++) CheckCannon(i, directions[i]);
            }
            catch (Exception exception) { issues.Add(currentCase + ": " + exception); }
            finally
            {
                if (holder != null)
                {
                    foreach (var particle in holder.GetComponentsInChildren<ParticleSystem>(true))
                        particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    Object.Destroy(holder);
                }
                // Keep collecting until the following editor update, including deferred destruction diagnostics.
                EditorApplication.delayCall += Finish;
            }
        }

        static void CheckIndependentGroups()
        {
            currentCase = "control: random linear axes, constant orbital axes";
            int before = issues.Count;
            var go = new GameObject("R498 independent curve group control");
            go.transform.SetParent(holder.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.startSpeed = 0;
            main.startLifetime = 1;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = false;
            velocity.x = velocity.z = new ParticleSystem.MinMaxCurve(0, 0);
            velocity.y = new ParticleSystem.MinMaxCurve(1, 1);
            velocity.orbitalX = velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(.8f);
            velocity.enabled = true;
            Simulate(ps);
            Need(velocity.x.mode == ParticleSystemCurveMode.TwoConstants
                && velocity.orbitalX.mode == ParticleSystemCurveMode.Constant, "control groups changed mode");
            report.Add(currentCase + ": " + (issues.Count == before ? "PASS" : "FAIL"));
        }

        static void CheckCannon(int index, Vector3 direction)
        {
            currentCase = "cannon " + (index + 1);
            int before = issues.Count;
            var parent = new GameObject("R498 cannon parent " + index);
            parent.transform.SetParent(holder.transform, false);
            parent.transform.SetPositionAndRotation(new Vector3(11 + 7 * index, 3, 29), Quaternion.Euler(0, index * 63, 0));
            var fan = new GameObject("R498 dummy fan");
            fan.transform.SetParent(parent.transform, false);
            fan.transform.localPosition = new Vector3(.3f, .2f, .4f);
            var fx = LibraryAirCannonFx.Build(fan.transform, direction, parent.transform);
            var intake = fx.transform.Find("Air cannon intake").GetComponent<ParticleSystem>();
            var velocity = intake.velocityOverLifetime;
            report.Add(currentCase + " orbital modes: " + velocity.orbitalX.mode + "/"
                + velocity.orbitalY.mode + "/" + velocity.orbitalZ.mode);
            Need(velocity.orbitalX.mode == ParticleSystemCurveMode.TwoConstants
                && velocity.orbitalY.mode == ParticleSystemCurveMode.TwoConstants
                && velocity.orbitalZ.mode == ParticleSystemCurveMode.TwoConstants, "orbital modes must be TwoConstants on all axes");
            Need(ZeroRange(velocity.orbitalX) && ZeroRange(velocity.orbitalY), "unused orbital axes must remain zero");
            Need(Mathf.Approximately(velocity.orbitalZ.constantMin, 2.5f)
                && Mathf.Approximately(velocity.orbitalZ.constantMax, 5f), "muzzle orbit speed range changed");
            Need(velocity.enabled && velocity.space == ParticleSystemSimulationSpace.Local
                && intake.main.simulationSpace == ParticleSystemSimulationSpace.Local, "intake must orbit in muzzle-local space");
            Need(Vector3.Distance(fx.transform.position, fan.transform.position) < .0001f
                && Vector3.Dot(fx.transform.forward, direction) > .9999f, "translated or rotated muzzle placement changed");

            // Assertions above collect failures without throwing: the broken baseline still reaches native simulation.
            fx.Charge(.85f);
            Simulate(intake);
            fx.Fire();
            foreach (string name in new[] { "Air cannon streaks", "Air cannon puffs", "Air cannon flecks" })
            {
                var shot = fx.transform.Find(name).GetComponent<ParticleSystem>();
                Need(shot.particleCount > 0, name + " did not emit during Fire");
                shot.Simulate(.025f, false, false, true);
                Need(shot.particleCount > 0, name + " shot particles did not survive simulation");
            }
            foreach (var particle in fx.GetComponentsInChildren<ParticleSystem>(true)) Simulate(particle);
            fx.Charge(-1);
            report.Add(currentCase + ": " + (issues.Count == before ? "PASS" : "FAIL"));
        }

        static bool ZeroRange(ParticleSystem.MinMaxCurve curve) =>
            Mathf.Approximately(curve.constantMin, 0) && Mathf.Approximately(curve.constantMax, 0);

        static void Simulate(ParticleSystem ps)
        {
            // Production effects are already playing; Unity requires a stopped system before changing seeds.
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false;
            ps.randomSeed = 498;
            ps.Play(false);
            ps.Emit(new ParticleSystem.EmitParams
            {
                position = new Vector3(1, .2f, .4f), velocity = Vector3.zero,
                startLifetime = 1, startSize = .05f, startColor = Color.white
            }, 8);
            for (int i = 0; i < 4; i++) ps.Simulate(.025f, false, false, true);
            Need(ps.particleCount > 0, ps.name + " emitted particles did not survive simulation");
        }

        static void Need(bool condition, string message)
        {
            if (!condition) issues.Add(currentCase + ": " + message);
        }

        static void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert
                || message.Contains(OrbitalDiagnostic)) issues.Add(currentCase + " [" + type + "]: " + message);
        }

        static void Finish()
        {
            Application.logMessageReceived -= OnLog;
            report.AddRange(issues);
            report.Add("Result: " + (issues.Count == 0 ? "PASS" : "FAIL") + ", issues=" + issues.Count);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllLines(ReportPath, report);
            Debug.Log("[AirCannonFxValidation] " + report[report.Count - 1] + ". Report: " + Path.GetFullPath(ReportPath));
            SessionState.SetInt(CodeKey, issues.Count == 0 ? 0 : 1);
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(ExitKey, true);
            EditorApplication.isPlaying = false;
        }

        static void CompleteExit()
        {
            if (!SessionState.GetBool(ExitKey, false)) return;
            int code = SessionState.GetInt(CodeKey, 1);
            SessionState.EraseBool(ExitKey);
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseInt(CodeKey);
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
