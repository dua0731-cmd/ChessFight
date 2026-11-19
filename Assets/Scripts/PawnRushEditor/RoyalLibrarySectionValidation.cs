using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.PawnRush.Editor
{
    // seoungtest R689: the Royal Library section (Late_3-1_RoyalLibrary, the late obstacle slot) checked in Play Mode in the
    // section test scene: the round builds it in slot 5 with its entry and exit, checkpoints, kill volume and threshold
    // floors; the giant chess runs as a round section (no pawns or camera of its own) with the section's seed; the books
    // fold on the shared clock; the typewriter's letters are the seed's; the chess starts when the playtest pawn is put at the
    // chess room's door and its moves go on; the section's state is the bytes it says. Not people, not online.
    //   Menu: ChessFight > Pawn Rush > Validate > Royal Library Section (Play)
    //   Batch (on a copy of the project while the Editor keeps this one open):
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.RoyalLibrarySectionValidation.RunPlayBatch
    // Report: Logs/RoyalLibrarySection/report.txt.
    [InitializeOnLoad]
    public static class RoyalLibrarySectionValidation
    {
        const string ScenePath = "Assets/Scenes/PawnRush_Sections.unity";
        const string ActiveKey = "RoyalLibrarySectionValidation.Active", ExitKey = "RoyalLibrarySectionValidation.Exit",
            CodeKey = "RoyalLibrarySectionValidation.Code";
        static string Folder => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "RoyalLibrarySection");
        static readonly List<string> errors = new List<string>();
        static int failures, step, redraws;
        static double began, stepAt, requested, idleAt;
        static PawnRushSection section;
        static RoyalLibraryMatch match;
        static MonoBehaviour chess;
        static RoyalLibraryLighting lighting;
        static int plyAtStart;

        static RoyalLibrarySectionValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        [MenuItem("ChessFight/Pawn Rush/Validate/Royal Library Section (Play)", false, 206)]
        public static void RunMenu() => RunPlayBatch();

        public static void RunPlayBatch()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "report.txt"),
                "Royal Library section (R689): built in a round of the section test scene, in Play Mode. Not people, not online.\n");
            AssetDatabase.ImportAsset("Assets/Prefabs/PawnRushSections/Late_3-1_RoyalLibrary.prefab", ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitKey, false);
            SessionState.SetInt(CodeKey, 1);
            requested = idleAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= BeginWhenIdle;
            EditorApplication.update += BeginWhenIdle;
        }

        static void BeginWhenIdle()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - requested > 180)
            {
                EditorApplication.update -= BeginWhenIdle;
                Log("FAIL: the Editor did not settle before Play Mode");
                SessionState.SetBool(ActiveKey, false);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { idleAt = now; return; }
            if (now - idleAt < 2) return;
            EditorApplication.update -= BeginWhenIdle;
            EditorSceneManager.playModeStartScene = null;
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                errors.Clear(); failures = 0; step = 0; redraws = 0; section = null; match = null; chess = null;
                began = stepAt = EditorApplication.timeSinceStartup;
                Application.logMessageReceived += OnLog;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) CompleteExit();
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (errors.Count < 40) errors.Add(type + ": " + condition + (type == LogType.Exception ? "\n" + stack : ""));
        }

        static void Check(bool ok, string what)
        {
            if (!ok) failures++;
            Log((ok ? "PASS " : "FAIL ") + what);
        }

        static T Field<T>(object o, string name)
        {
            var f = o?.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return f != null && f.GetValue(o) is T v ? v : default;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            double now = EditorApplication.timeSinceStartup, inStep = now - stepAt;
            if (now - began > 240) { Log("FAIL: timed out at step " + step); failures++; Finish(); return; }
            switch (step)
            {
                case 0:   // the round is built
                    var round = PawnRushRound.Current;
                    if (round == null || !round.HasBuilt || inStep < 3) return;
                    var ids = new List<string>();
                    foreach (var s in round.Sections) { ids.Add(s != null ? s.Id : "-"); if (s != null && s.Id == RoyalLibrarySection.Id) section = s; }
                    if (section == null && redraws < 12)
                    {
                        // main (R706): the late obstacle slot also has the war room - draw again until the library comes up
                        // (the previous round's pick is left out while the slot has another).
                        redraws++;
                        Log("redraw " + redraws + ": " + string.Join(", ", ids));
                        round.Build(round.Seed + 7919);
                        stepAt = now;
                        return;
                    }
                    Check(section != null, "the round has the Royal Library section (" + string.Join(", ", ids) + ")");
                    if (section == null) { Finish(); return; }
                    Check(section.Slot == 4, "in the late obstacle slot (5): slot index " + section.Slot);
                    Check(section.EntryLocal.position == RoyalLibrarySection.Entry && section.ExitLocal.position == RoyalLibrarySection.Exit,
                        $"entry {section.EntryLocal.position} exit {section.ExitLocal.position}");
                    var group = section.transform.Find(RoyalLibrarySection.GroupName);
                    Check(group != null, "its round parts (Section)");
                    var cps = section.GetComponentsInChildren<CourseCheckpoint>(true);
                    var orders = new List<string>();
                    foreach (var cp in cps) orders.Add(cp.GetComponent<Checkpoint>().Order + "@" + section.transform.InverseTransformPoint(cp.transform.position).z.ToString("0"));
                    Check(cps.Length == 6, "six checkpoints: " + string.Join(" ", orders));
                    Check(section.GetComponentsInChildren<KillVolume>(true).Length >= 1, "a kill volume");
                    Check(group != null && group.Find("Entry threshold") != null && group.Find("Exit threshold") != null, "threshold floors");
                    match = section.GetComponent<RoyalLibraryMatch>();
                    Check(match != null && match.Configured && match.Seed == section.Seed, $"the match bridge has the section's seed ({(match != null ? match.Seed : 0)} / {section.Seed})");
                    foreach (var b in section.GetComponentsInChildren<MonoBehaviour>(true))
                        if (b != null && b.GetType().Name == "GiantChessCourse") chess = b;
                    Check(chess != null && chess.enabled && Field<RoyalLibraryMatch>(chess, "match") == match, "the giant chess runs as a round section");
                    Check(Field<System.Collections.IList>(chess, "players") is System.Collections.IList list && list.Count == 0, "no pawns of its own");
                    var books = section.GetComponentsInChildren<ClosingBook>(true);
                    Check(books.Length == 2 && Array.TrueForAll(books, b => b.SharedClock), "both books on the shared clock");
                    var typewriter = section.GetComponentInChildren<TypewriterStairs>(true);
                    string expected = TypewriterMemory.Generate(new System.Random(unchecked((int)SeededSchedule.Hash(section.Seed, 0x7E1)))).Sequence;
                    Check(typewriter != null && typewriter.Sequence == expected, $"the typewriter's clues are the seed's ({(typewriter != null ? typewriter.Sequence : "-")} / {expected})");
                    var part = section.GetComponent<RoyalLibrarySection>();
                    var buffer = new byte[96];
                    var block = new NetBlock(buffer, 0, buffer.Length);
                    part.WriteNet(ref block);
                    Check(block.Written == part.NetBytes && part.NetBytes == 33, "the section's state: " + block.Written + " bytes");
                    Check(!match.ChessStarted, "the chess waits for the first pawn");
                    lighting = section.GetComponentInChildren<RoyalLibraryLighting>(true);
                    Check(lighting != null && Field<bool>(lighting, "zoned"), "the library's light is zoned in a round");
                    Check(lighting != null && Field<float>(lighting, "weight") < .01f && !Field<bool>(lighting, "applied"),
                        "away from the library the round keeps its own light (weight " + (lighting != null ? Field<float>(lighting, "weight") : -1f).ToString("0.00") + ")");
                    var game = Field<object>(chess, "<CurrentGame>k__BackingField");
                    Log("game: " + game?.GetType().GetProperty("Title")?.GetValue(game));
                    step = 1; stepAt = now;
                    return;
                case 1:   // the playtest pawn at the chess room's door
                    var driver = MiniGameBase.LocalDriver;
                    if (driver == null) { if (inStep > 10) { Check(false, "the playtest pawn"); Finish(); } return; }
                    driver.Teleport(section.transform.TransformPoint(new Vector3(0f, 0f, -4f)), section.transform.rotation);
                    step = 2; stepAt = now;
                    return;
                case 2:
                    if (!match.ChessStarted && inStep < 5) return;
                    Check(match.ChessStarted, "the chess starts when a pawn reaches the room");
                    plyAtStart = Field<int>(chess, "ply");
                    step = 3; stepAt = now;
                    return;
                case 3:   // ten real seconds: about five moves at replay speed 5
                    if (inStep < 10) return;
                    int ply = Field<int>(chess, "ply"), phase = Field<int>(chess, "phase");
                    Check(ply >= plyAtStart + 3, $"the moves go on: ply {plyAtStart} -> {ply}, phase {phase}");
                    var block2 = new NetBlock(new byte[96], 0, 96);
                    section.GetComponent<RoyalLibrarySection>().WriteNet(ref block2);
                    int wire = block2.Buffer[0] | block2.Buffer[1] << 8;
                    Check(wire > 0 && Math.Abs(GiantChessReplay.Decode(wire, match.Origin) - match.ChessStart) < 1e-6, "the chess start on the wire: " + wire);
                    if (lighting != null)
                    {
                        float w = Field<float>(lighting, "weight");
                        var sun = RenderSettings.sun;
                        var cam = Camera.main;
                        string where = cam != null ? section.transform.InverseTransformPoint(cam.transform.position).ToString("0") : "no camera";
                        Check(w > .9f && sun != null && Vector4.Distance(sun.color, lighting.sunColor) < .05f,
                            $"in the library its light is on: weight {w:0.00}, sun {(sun != null ? sun.color.ToString() : "-")}, camera at {where}");
                    }
                    Finish();
                    return;
            }
        }

        static void Finish()
        {
            foreach (var e in errors) Log("console " + e);
            Check(errors.Count == 0, "no errors or exceptions in the console (" + errors.Count + ")");
            Log(failures == 0 ? "RESULT PASS" : "RESULT FAIL (" + failures + ")");
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick;
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(ExitKey, true);
            SessionState.SetInt(CodeKey, failures == 0 ? 0 : 1);
            EditorApplication.isPlaying = false;
        }

        static void Log(string value)
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Path.Combine(Folder, "report.txt"), value + "\n");
            Debug.Log("[RoyalLibrarySection] " + value);
        }

        static void CompleteExit()
        {
            if (!SessionState.GetBool(ExitKey, false)) return;
            SessionState.SetBool(ExitKey, false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(CodeKey, 1));
        }
    }
}
