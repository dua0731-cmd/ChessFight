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
    // R616: builds a section round in Play Mode and checks its last section, the war room, as the user's level design of
    // 11-12 has it (Course01v4Builder.WarRoomEnding.cs):
    //   the turrets   four of them on their towers at the corners of the conveyor's well, the middle rook put away; over
    //                 the next volleys exactly one fires every TurretVolleys.Period seconds, never the one before, each
    //                 shot from its own turret through the rider band above the moving deck, with nothing
    //                 solid on the way; how much of the carriage's travel each turret can reach
    //   the way out   the great door's leaves hang on a PushGate and shut the opening; pushed open, the passage through
    //                 the rear wall is clear and its floor runs level on to the ending's floor; the round's finish and a
    //                 second checkpoint stand beyond the door; the gate's progress is one of the round's online parts
    // and takes pictures (Logs/WarRoomEnding): the room from above and from the start, each turret, a volley charging and
    // in flight, the door shut, half open and open, the ending's floor. Not a play test: no pawn rides the conveyor, is
    // hit or pushes the door (the door is opened by its progress, as a match's client is handed it), nothing goes online.
    //
    // Batch (do not pass -quit; it exits after Play Mode; with a graphics device for the pictures):
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.WarRoomEndingValidation.RunPlayBatch
    //         [-warRoomSeed 11] [-warRoomNoCapture] -logFile <file>
    [InitializeOnLoad]
    public static class WarRoomEndingValidation
    {
        const string ActiveKey = "ChessFight.R616.WarRoom.ValidatePlay";
        const string ExitKey = "ChessFight.R616.WarRoom.ValidateExit";
        const string CodeKey = "ChessFight.R616.WarRoom.ValidateCode";
        const string Folder = "Logs/WarRoomEnding";
        const string Report = "warroom.txt";
        const string SetPath = "Assets/Settings/PawnRushSections.asset";
        const float RuntimeLimit = 900f, Settle = 2.5f;
        const int Volleys = 400;
        static readonly List<string> errors = new List<string>();
        static readonly List<string> failures = new List<string>();
        static readonly Queue<Action> pictures = new Queue<Action>();
        static Camera captureCamera;
        static PawnRushRound round;
        static PawnRushSection room;
        static LibraryRookTurret[] turrets;
        static PushGate gate;
        static int phase, seed, charged, shot;
        static long pictured = long.MinValue;
        static double beganAt, phaseAt, playRequestedAt, editorIdleAt;
        static bool stopping;

        static WarRoomEndingValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        [MenuItem("ChessFight/Pawn Rush/Validate/War Room Ending (Play)", false, 204)]
        public static void RunMenu() => RunPlayBatch();

        public static void RunPlayBatch()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, Report),
                "War room check: a section round built in Play Mode, its last section's four turrets, their volleys, the exit door and the ending's floor, and pictures.\n" +
                "Not a play test (no pawn rides, is hit or pushes the door) and not online.\n");
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
                seed = 11;
                var args = Environment.GetCommandLineArgs();
                int at = Array.IndexOf(args, "-warRoomSeed");
                if (at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], out int given) && given != 0) seed = given;
                phase = charged = shot = 0;
                pictured = long.MinValue;
                round = null; room = null; turrets = null; gate = null;
                errors.Clear(); failures.Clear(); pictures.Clear(); stopping = false;
                beganAt = phaseAt = EditorApplication.timeSinceStartup;
                Application.logMessageReceived += OnLog;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
                RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 1f;
                DynamicGI.UpdateEnvironment();
                var sun = new GameObject("Validation sun").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.2f; sun.color = Color.white; sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                RenderSettings.sun = sun;
                captureCamera = new GameObject("Validation camera").AddComponent<Camera>();
                captureCamera.clearFlags = CameraClearFlags.Skybox;
                captureCamera.nearClipPlane = .1f; captureCamera.farClipPlane = 400f;
                captureCamera.enabled = true;
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) CompleteExit();
        }

        static bool NoCapture => Array.IndexOf(Environment.GetCommandLineArgs(), "-warRoomNoCapture") >= 0;

        static void OnLog(string condition, string stack, LogType type)
        {
            // The render pipeline's editor package logs this about its own ray-tracing shader the first time a capture asks
            // for a render request in batch mode (R516, MiniGamePrefabValidation): not the game's.
            if (condition != null && condition.StartsWith("Host type is not matching any asset type") && condition.Contains(".urtshader")) return;
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                errors.Add(condition + "\n" + stack);
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || stopping) return;
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            try
            {
                if (EditorApplication.timeSinceStartup - beganAt > RuntimeLimit)
                    throw new InvalidOperationException("War room check exceeded " + RuntimeLimit + " seconds");
                if (pictures.Count > 0) { pictures.Dequeue()(); return; }   // one picture a frame
                double now = EditorApplication.timeSinceStartup;
                switch (phase)
                {
                    case 0:
                        BuildRound();
                        phase = 1; phaseAt = now;
                        return;
                    case 1:
                        if (now - phaseAt < Settle) return;
                        Physics.SyncTransforms();
                        if (!Find()) { Finish(); return; }
                        CheckTurrets();
                        CheckBalconies();
                        CheckVolleys();
                        CheckReach();
                        CheckExitShut();
                        Overviews();
                        phase = 2; phaseAt = now;
                        return;
                    case 2:
                        // live: a volley charging (its laser on the carriage) and one in flight, of two different turrets
                        if (NoCapture || now - phaseAt > 40f || (charged >= 2 && shot >= 2)) { phase = 3; phaseAt = now; return; }
                        WatchVolley();
                        return;
                    case 3:
                        if (gate != null) gate.SetProgress(.45f);
                        phase = 4; phaseAt = now;
                        return;
                    case 4:
                        if (now - phaseAt < .8f) return;
                        Picture("door_half_open", new Vector3(0f, 3f, 17.2f), new Vector3(0f, 3.5f, 24.2f), 62f);
                        if (gate != null) gate.SetProgress(1f);
                        phase = 5; phaseAt = now;
                        return;
                    case 5:
                        if (now - phaseAt < 1f) return;
                        Physics.SyncTransforms();
                        CheckExitOpen();
                        Picture("door_open", new Vector3(0f, 3f, 17.2f), new Vector3(0f, 3.5f, 24.2f), 62f);
                        Picture("door_open_near", new Vector3(-1.2f, 2.6f, 21.2f), new Vector3(.4f, 2.6f, 27f), 70f);
                        Picture("door_from_ending", new Vector3(0f, 3.2f, 32.5f), new Vector3(0f, 3.4f, 24.6f), 62f);
                        Picture("ending_floor", new Vector3(-5.2f, 7f, 26f), new Vector3(1f, 1.2f, 34f), 66f);
                        phase = 6;
                        return;
                    case 6:
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

        static void BuildRound()
        {
            var go = new GameObject("War room validation round");
            go.SetActive(false);
            round = go.AddComponent<PawnRushRound>();
            var so = new SerializedObject(round);
            so.FindProperty("set").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PawnRushSectionSet>(SetPath);
            so.FindProperty("buildOnAwake").boolValue = false;
            so.FindProperty("startSpots").boolValue = false;
            so.FindProperty("finishRace").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true);
            round.Build(seed);
            Log("");
            Log("Round seed " + seed + ": " + string.Join(" | ", round.Sections.Select(s => s.name)));
        }

        static bool Find()
        {
            room = round.Sections.LastOrDefault();
            if (room == null || room.GetComponent<WarRoomSection>() == null)
            {
                Check(false, "the round's last section is not the war room: " + (room != null ? room.name : "none"));
                return false;
            }
            turrets = room.GetComponentsInChildren<LibraryRookTurret>(false).Where(t => t.Volley).OrderBy(t => t.TurretIndex).ToArray();
            gate = room.GetComponentInChildren<PushGate>(true);
            Log("War room at " + V(room.transform.position) + ", section seed " + room.Seed + ", clock " + ObstacleClock.Now.ToString("0.00"));
            return true;
        }

        // ------------------------------------------------------------------ the turrets

        static Vector3 Local(Vector3 world) => room.transform.InverseTransformPoint(world);
        static Vector3 World(Vector3 local) => room.transform.TransformPoint(local);

        static void CheckTurrets()
        {
            Check(turrets.Length == Course01v4Builder.WarRoomTurretCount, "turrets firing as a battery: " + turrets.Length + " (4 wanted)");
            var loose = room.GetComponentsInChildren<LibraryRookTurret>(false).Count(t => !t.Volley);
            Check(loose == 0, loose + " rook(s) still fire on their own rounds");
            foreach (var t in room.GetComponentsInChildren<Transform>(false))
                if (t.name == "Goal_rook_0") Check(false, "the middle rook (Goal_rook_0) still stands");
            for (int i = 0; i < turrets.Length && i < Course01v4Builder.WarRoomTurretSpots.Length; i++)
            {
                var want = Course01v4Builder.WarRoomTurretSpots[i] + Vector3.up * Course01v4Builder.WarRoomTurretTower;
                var at = Local(turrets[i].transform.position);
                var gap = Local(turrets[i].GapPoint);
                Check(turrets[i].TurretIndex == i, "turret " + i + " has index " + turrets[i].TurretIndex);
                Check(Vector3.Distance(at, want) < .03f, "turret " + Course01v4Builder.WarRoomTurretNames[i] + " stands at " + V(at) + ", wanted " + V(want));
                // the floor under the tower's four corners: it stands on something everywhere
                float worst = 0f;
                foreach (var corner in new[] { new Vector3(-.7f, 0f, -.7f), new Vector3(.7f, 0f, -.7f), new Vector3(-.7f, 0f, .7f), new Vector3(.7f, 0f, .7f) })
                {
                    var foot = Course01v4Builder.WarRoomTurretSpots[i] + corner;
                    float drop = FloorUnder(foot + Vector3.up * .1f, t => !t.name.StartsWith("Goal_rook_"), out _);
                    worst = Mathf.Max(worst, drop - .1f);
                }
                Check(worst < .2f, "turret " + Course01v4Builder.WarRoomTurretNames[i] + ": a corner of its tower hangs " + worst.ToString("0.00") + " m over the floor");
                Log("turret " + i + " " + Course01v4Builder.WarRoomTurretNames[i] + ": rook at " + V(at) + ", shots leave from " + V(gap) +
                    ", floor under the tower's corners within " + worst.ToString("0.00") + " m");
            }
        }

        static void CheckBalconies()
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var screen = room.transform.Find("Content/" + (side < 0 ? "West" : "East") + " balcony enclosure");
                var solid = screen != null ? screen.GetComponent<BoxCollider>() : null;
                Check(solid != null && solid.enabled && !solid.isTrigger, "missing solid balcony enclosure on side " + side);
                if (solid == null) continue;
                foreach (float x in new[] { 7.95f, 11f, 14.9f })
                    foreach (float height in new[] { .6f, 2.5f, 5f })
                        Check(solid.Raycast(new Ray(World(new Vector3(side * x, height, -12f)), room.transform.forward), out _, 24f),
                            "open balcony lane at x " + side * x + ", height " + height);
            }
            var rails = room.GetComponentsInChildren<Collider>(true).Where(c => c.name.StartsWith("Fixed_guide_")).ToArray();
            Check(rails.Length > 0 && rails.All(c => !c.enabled), "the static guides under the carriage can be ridden");
        }

        // How far down the first solid thing is under a point (section frame), triggers and what `count` leaves out aside.
        static float FloorUnder(Vector3 local, Func<Transform, bool> count, out Collider floor)
        {
            floor = null;
            float best = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(World(local), -room.transform.up, 30f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.distance < best && (count == null || count(hit.collider.transform))) { best = hit.distance; floor = hit.collider; }
            return best;
        }

        static BoxCollider Deck(out LibraryShuttleDriver shuttle, out Vector3 fromCarriage)
        {
            fromCarriage = default;
            shuttle = room.GetComponentsInChildren<LibraryShuttleDriver>(true).FirstOrDefault();
            if (shuttle == null || shuttle.Carriage == null) return null;
            var deck = shuttle.Carriage.GetComponentsInChildren<BoxCollider>(true).FirstOrDefault(b => b.name == "Conveyor collision");
            if (deck != null) fromCarriage = deck.transform.TransformPoint(deck.center) - shuttle.Carriage.position;
            return deck;
        }

        // The deck's level axes: x across it, z along it (a jolt's nod left out).
        static Quaternion DeckFrame(BoxCollider deck)
        {
            var along = deck.transform.forward;
            along.y = 0f;
            return Quaternion.LookRotation(along.normalized, Vector3.up);
        }

        // Something of the still model on the way from a to b (the turrets' own rule: nothing on a body, no pawn, no rook).
        static bool Blocked(Vector3 a, Vector3 b, out string what)
        {
            what = null;
            float length = Vector3.Distance(a, b);
            float best = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(a, (b - a).normalized, length - .4f, ~0, QueryTriggerInteraction.Ignore))
            {
                var c = hit.collider;
                if (c.attachedRigidbody != null || c.GetComponentInParent<ICharacterDriver>() != null || c.name.StartsWith("Goal_rook_")) continue;
                if (hit.distance < best) { best = hit.distance; what = c.name + " at " + V(Local(hit.point)); }
            }
            return what != null;
        }

        static void CheckVolleys()
        {
            if (turrets.Length == 0) return;
            var deck = Deck(out var shuttle, out var fromCarriage);
            Check(deck != null, "no conveyor deck (Conveyor collision) in the war room");
            if (deck == null) return;
            var size = Vector3.Scale(deck.size, deck.transform.lossyScale);
            var count = new int[turrets.Length];
            int last = -1, offDeck = 0, blocked = 0, twice = 0, none = 0, repeats = 0;
            float longest = 0f, lowAngle = 180f, highAngle = -180f, ahead = float.MinValue, behind = float.MaxValue;
            string firstBlock = null;
            long first = TurretVolleys.Volley(ObstacleClock.Now) + 2;
            var order = new System.Text.StringBuilder();
            for (long n = first; n < first + Volleys; n++)
            {
                double clock = TurretVolleys.Start(n) + .1;
                int who = -1;
                Vector3 from = default, to = default;
                double fire = 0, impact = 0;
                for (int i = 0; i < turrets.Length; i++)
                {
                    if (!turrets[i].FiresAt(clock, out var f, out var t, out double fi, out double im)) continue;
                    if (who >= 0) twice++;
                    who = i; from = f; to = t; fire = fi; impact = im;
                }
                if (who < 0) { none++; continue; }
                if (who == last) repeats++;
                last = who;
                count[who]++;
                if (n < first + 40) order.Append(Course01v4Builder.WarRoomTurretNames[who]).Append(' ');
                Check(who == TurretVolleys.Turret(room.Seed, n, turrets.Length), "volley " + n + ": turret " + who + " fires, the rule says " + TurretVolleys.Turret(room.Seed, n, turrets.Length));
                Check(Math.Abs(fire - (TurretVolleys.Start(n) + .55)) < 1e-3, "volley " + n + " fires " + (fire - TurretVolleys.Start(n)).ToString("0.000") + " s into its two seconds (0.55 wanted)");
                Check(Vector3.Distance(from, turrets[who].GapPoint) < .8f, "volley " + n + " leaves from " + V(Local(from)) + ", not from turret " + who);
                longest = Mathf.Max(longest, (float)(impact - fire));
                // Intersect the actual flight segment with the deck's longitudinal centre plane and evaluate
                // the carriage at that crossing time, independently of the aiming implementation.
                var axes = DeckFrame(deck);
                var across = axes * Vector3.right;
                if (!shuttle.CarriageAt(fire, out var carriage)) { offDeck++; continue; }
                float dx = Vector3.Dot(to - from, across);
                if (Mathf.Abs(dx) < .01f) { offDeck++; continue; }
                float fraction = Vector3.Dot(carriage + fromCarriage - from, across) / dx;
                if (fraction <= 0f || fraction >= 1f || !shuttle.CarriageAt(fire + (impact - fire) * fraction, out carriage)) { offDeck++; continue; }
                var crossing = Vector3.Lerp(from, to, fraction);
                var middle = carriage + fromCarriage;
                var local = Quaternion.Inverse(axes) * (crossing - middle);
                float height = local.y - size.y * .5f;
                if (height < LibraryRookTurret.VolleyMinHeight - .06f || height > LibraryRookTurret.VolleyMaxHeight + .06f
                    || Mathf.Abs(local.z) > size.z * .5f || Vector3.Distance(to, crossing) < .5f) offDeck++;
                var centreBearing = Vector3.ProjectOnPlane(middle - turrets[who].GapPoint, Vector3.up);
                float angle = Vector3.SignedAngle(centreBearing, Vector3.ProjectOnPlane(to - from, Vector3.up), Vector3.up);
                Check(Mathf.Abs(angle) <= 15.5f, "volley " + n + " spread outside its cone: " + angle);
                lowAngle = Mathf.Min(lowAngle, angle);
                highAngle = Mathf.Max(highAngle, angle);
                ahead = Mathf.Max(ahead, local.z);
                behind = Mathf.Min(behind, local.z);
                if (Blocked(from, crossing, out string what)) { blocked++; if (firstBlock == null) firstBlock = "volley " + n + " from turret " + who + ": " + what; }
            }
            Check(lowAngle < -5f && highAngle > 5f, "volleys do not cover both sides of the bearing to the rider");
            Check(ahead > 1f && behind < -1f, "volleys do not reach both halves of the riding deck");
            Check(none == 0, none + " of " + Volleys + " volleys have no turret firing");
            Check(twice == 0, twice + " volleys have two turrets firing");
            Check(repeats == 0, repeats + " volleys are fired by the turret that fired the one before");
            Check(offDeck == 0, offDeck + " of " + Volleys + " shots miss the rider-height band or end before passing through it");
            Check(blocked == 0, blocked + " of " + Volleys + " shots have something solid on the way (" + firstBlock + ")");
            for (int i = 0; i < count.Length; i++)
                Check(count[i] > Volleys / count.Length * .7f && count[i] < Volleys / count.Length * 1.3f, "turret " + i + " fires " + count[i] + " of " + Volleys + " volleys");
            Log("volleys " + first + " .. " + (first + Volleys - 1) + " (" + TurretVolleys.Period + " s apart): one turret each, never the one before; shares " +
                string.Join(" / ", count.Select((c, i) => Course01v4Builder.WarRoomTurretNames[i] + " " + c)) + "; longest flight " + longest.ToString("0.00") + " s");
            Log("Rider-height crossings: bearing offset " + lowAngle.ToString("0.0") + " to " + highAngle.ToString("0.0") +
                " degrees, from " + behind.ToString("0.00") + " to " + ahead.ToString("0.00") + " m along the deck");
            Log("the first forty: " + order.ToString().Trim());
        }

        // How much of the carriage's deck, over its whole travel, each turret reaches with nothing in the way.
        static void CheckReach()
        {
            var deck = Deck(out var shuttle, out var fromCarriage);
            if (deck == null) return;
            var size = Vector3.Scale(deck.size, deck.transform.lossyScale);
            foreach (var turret in turrets)
            {
                int clear = 0, all = 0;
                string firstBlock = null;
                for (float time = 0f; time < 16f; time += .5f)
                {
                    if (!shuttle.CarriageAt(time, out var carriage)) continue;
                    foreach (float u in new[] { -1f, -.5f, 0f, .5f, 1f })
                        foreach (float v in new[] { -1f, 0f, 1f })
                        {
                            var point = carriage + fromCarriage + DeckFrame(deck) * new Vector3(v * (size.x * .5f - .35f), size.y * .5f + .95f, u * (size.z * .5f - .6f));
                            var flat = point - turret.GapPoint;
                            flat.y = 0f;
                            var from = turret.GapPoint + flat.normalized * .56f + Vector3.up * .06f;
                            all++;
                            if (!Blocked(from, point, out string what)) clear++;
                            else if (firstBlock == null) firstBlock = what + " (deck point " + V(Local(point)) + ")";
                        }
                }
                float share = all > 0 ? clear * 100f / all : 0f;
                Check(share >= 90f, "turret " + Course01v4Builder.WarRoomTurretNames[turret.TurretIndex] + " reaches only " + share.ToString("0") + " % of the deck over its travel (" + firstBlock + ")");
                Log("turret " + Course01v4Builder.WarRoomTurretNames[turret.TurretIndex] + " reaches " + share.ToString("0.0") + " % of the deck over the carriage's travel (" + clear + " of " + all +
                    " points)" + (firstBlock != null ? "; in the way: " + firstBlock : ""));
            }
        }

        // ------------------------------------------------------------------ the way out

        static List<string> Solids(Vector3 centre, Vector3 half)
        {
            var found = new List<string>();
            foreach (var c in Physics.OverlapBox(World(centre), half, room.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                found.Add(c.name);
            return found;
        }

        static void CheckExitShut()
        {
            Check(gate != null, "no exit gate (PushGate) in the war room");
            if (gate == null) return;
            float floor = Course01v4Builder.WarRoomGoalFloor, z = Course01v4Builder.WarRoomExitGate;
            Check(Vector3.Distance(Local(gate.transform.position), new Vector3(0f, floor, z)) < .02f, "the exit gate stands at " + V(Local(gate.transform.position)));
            Check(gate.Progress == 0f && !gate.IsOpen, "the exit gate starts at " + gate.Progress.ToString("0.00"));
            foreach (string leaf in new[] { "Exit door leaf (left)", "Exit door leaf (right)" })
            {
                var model = room.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == leaf);
                Check(model != null && model.sharedMesh != null && model.sharedMesh.vertexCount > 300, leaf + ": no model cut from the door");
                if (model == null) continue;
                Check(model.transform.parent != null && model.transform.parent.GetComponent<Rigidbody>() != null, leaf + " does not ride a leaf of the gate (its parent: " +
                      (model.transform.parent != null ? model.transform.parent.name : "none") + ")");
                var b = model.GetComponent<Renderer>().bounds;
                Vector3 lo = Local(b.min), hi = Local(b.max);
                Log(leaf + ": " + model.sharedMesh.vertexCount + " vertices, " + model.sharedMesh.triangles.Length / 3 + " triangles, x " + Mathf.Min(lo.x, hi.x).ToString("0.00") + " .. " +
                    Mathf.Max(lo.x, hi.x).ToString("0.00") + ", y " + lo.y.ToString("0.00") + " .. " + hi.y.ToString("0.00") + ", z " + Mathf.Min(lo.z, hi.z).ToString("0.00") + " .. " + Mathf.Max(lo.z, hi.z).ToString("0.00"));
            }
            // shut: a pawn-high slab across the opening meets the leaves
            var across = Solids(new Vector3(0f, floor + 1.2f, z), new Vector3(1.4f, .8f, .05f));
            Check(across.Count > 0, "shut, nothing stands in the opening");
            // the finish and the second checkpoint are beyond the door
            var finish = room.GetComponentsInChildren<FinishZone>(true);
            Check(finish.Length == 1, finish.Length + " finish zones in the war room");
            foreach (var f in finish)
            {
                var at = Local(f.GetComponent<Collider>().bounds.center);
                Check(at.z > Course01v4Builder.WarRoomWallBack + 2f && Mathf.Abs(at.x) < .1f, "the finish is at " + V(at) + ", not beyond the door");
                Log("finish at " + V(at) + " (the rear wall's back is at z " + Course01v4Builder.WarRoomWallBack + ")");
            }
            Check(round.Finish != null, "the round has no crown finish bound");
            var numbers = room.GetComponentsInChildren<CourseCheckpoint>(true).Select(c => c.Number).OrderBy(x => x).ToArray();
            Check(numbers.SequenceEqual(new[] { 0, 1 }), "checkpoints: " + string.Join(", ", numbers) + " (0 and 1 wanted)");
            var parts = round.GetComponentsInChildren<ISectionNetPart>(true);
            Check(parts.Contains(gate), "the exit gate is not among the round's online parts");
            Log("exit gate shut: in the opening " + string.Join(", ", across.Distinct()) + "; online parts in the round " + parts.Length + " (" + parts.Sum(p => p.NetBytes) + " bytes)");
            var exit = Local(room.ExitWorld.position);
            Check(Vector3.Distance(exit, Course01v4Builder.WarRoomExit) < .02f, "the section's exit is " + V(exit) + ", wanted " + V(Course01v4Builder.WarRoomExit));
        }

        static void CheckExitOpen()
        {
            if (gate == null) return;
            float floor = Course01v4Builder.WarRoomGoalFloor;
            Check(gate.IsOpen, "the exit gate is at " + gate.Progress.ToString("0.00") + " after it was opened");
            // open: the passage, a pawn high and 3.2 m wide, from in front of the door to beyond the wall
            var inWay = Solids(new Vector3(0f, floor + 1.25f, 24.6f), new Vector3(1.6f, 1.1f, 1.3f));
            Check(inWay.Count == 0, "open, the passage still holds: " + string.Join(", ", inWay.Distinct()));
            // and nothing a pawn would walk into on the way from the star platform to the finish
            foreach (float x in new[] { -1.2f, 0f, 1.2f })
            {
                Vector3 a = World(new Vector3(x, floor + 1.2f, 22.9f)), b = World(new Vector3(x, floor + 1.2f, Course01v4Builder.WarRoomFinishAt));
                bool hit = Physics.SphereCast(a, .4f, (b - a).normalized, out var wall, Vector3.Distance(a, b), ~0, QueryTriggerInteraction.Ignore);
                Check(!hit, "open, walking x " + x + " from the star platform to the finish meets " + (hit ? wall.collider.name + " at " + V(Local(wall.point)) : ""));
            }
            // the floor from the star platform through the door to the ending's far end: no hole, no step over 0.31 m
            float before = float.NaN, step = 0f, low = float.MaxValue, high = float.MinValue;
            for (float z = 17f; z <= Course01v4Builder.WarRoomWallBack + Course01v4Builder.WarRoomEndingLength - .3f; z += .1f)
            {
                float drop = FloorUnder(new Vector3(0f, floor + 2f, z), t => t.GetComponentInParent<Rigidbody>() == null, out var under);
                float y = floor + 2f - drop;
                Check(drop < 4f, "no floor at z " + z.ToString("0.0") + " on the way out");
                if (!float.IsNaN(before)) step = Mathf.Max(step, Mathf.Abs(y - before));
                before = y; low = Mathf.Min(low, y); high = Mathf.Max(high, y);
            }
            Check(step < .31f, "a step of " + step.ToString("0.00") + " m on the way out");
            Log("exit gate open: the passage is clear; floor from the star platform to the ending between y " + low.ToString("0.00") + " and " + high.ToString("0.00") +
                ", largest step " + step.ToString("0.00") + " m");
        }

        // ------------------------------------------------------------------ pictures

        static void Overviews()
        {
            if (NoCapture) return;
            pictures.Enqueue(() => TopView("overview_top"));
            Picture("overview_from_start", new Vector3(0f, 6.5f, -22f), new Vector3(0f, .5f, 8f), 72f);
            Picture("overview_from_goal", new Vector3(0f, 7.5f, 21.5f), new Vector3(0f, 0f, -6f), 72f);
            for (int i = 0; i < Course01v4Builder.WarRoomTurretSpots.Length; i++)
            {
                var spot = Course01v4Builder.WarRoomTurretSpots[i];
                Picture("turret_" + Course01v4Builder.WarRoomTurretNames[i], new Vector3(spot.x * .42f, spot.y + 3.1f, spot.z * .62f), spot + Vector3.up * 2f, 60f);
            }
            Picture("door_shut", new Vector3(0f, 3f, 17.2f), new Vector3(0f, 3.5f, 24.2f), 62f);
            Picture("star_platform", new Vector3(-6.5f, 5.5f, 14.5f), new Vector3(1f, 2f, 22f), 66f);
        }

        // A volley's turret charging (the laser on the carriage) and its shot in flight, seen over the turret's shoulder
        // and across the well.
        static void WatchVolley()
        {
            double clock = ObstacleClock.Now;
            long n = TurretVolleys.Volley(clock);
            foreach (var turret in turrets)
            {
                if (!turret.FiresAt(clock, out var from, out var to, out double fire, out double impact)) continue;
                // from over the well on the far side, level with the lanterns: the turret, its line and where it lands
                var spot = Course01v4Builder.WarRoomTurretSpots[turret.TurretIndex];
                Vector3 eye = new Vector3(-spot.x * .55f, 6f, spot.z * .35f), target = Local(Vector3.Lerp(from, to, .45f));
                string name = Course01v4Builder.WarRoomTurretNames[turret.TurretIndex];
                if (clock > fire - .2 && clock < fire - .03 && pictured != n && charged < 2)
                {
                    pictured = n;
                    charged++;
                    CaptureView("volley_charge_" + charged + "_" + name, eye, target, Vector3.up, 62f, false, 1536, 1024);
                    CaptureView("volley_charge_" + charged + "_" + name + "_wide", new Vector3(0f, 9.5f, -21f), new Vector3(0f, 0f, 3f), Vector3.up, 70f, false, 1536, 1024);
                }
                else if (clock >= fire && clock < impact + .12 && pictured != -n && shot < 2 && charged > shot)
                {
                    pictured = -n;
                    shot++;
                    CaptureView("volley_shot_" + shot + "_" + name, eye, target, Vector3.up, 62f, false, 1536, 1024);
                }
            }
        }

        static void Picture(string name, Vector3 eye, Vector3 target, float fieldOfView)
        {
            if (NoCapture) return;
            pictures.Enqueue(() => CaptureView(name, eye, target, Vector3.up, fieldOfView, false, 1536, 1024));
        }

        // The whole room from straight above, north up as in the user's sketch, with everything over 7 m (the vault, its
        // beams, the upper lanterns) not drawn.
        static void TopView(string name)
        {
            var hidden = new List<Renderer>();
            foreach (var r in room.GetComponentsInChildren<Renderer>(false))
                if (r.enabled && Local(r.bounds.min).y > 7f) { r.enabled = false; hidden.Add(r); }
            try
            {
                CaptureView(name, new Vector3(0f, 60f, 8f), new Vector3(0f, 0f, 8f), Vector3.forward, 33f, true, 1024, 1536);
            }
            finally
            {
                foreach (var r in hidden) r.enabled = true;
            }
        }

        // One picture from `eye` toward `target` (the section's frame) into the validation folder; orthographic with
        // `fieldOfView` as its half height when `flat`.
        static void CaptureView(string name, Vector3 eye, Vector3 target, Vector3 up, float fieldOfView, bool flat, int width, int height)
        {
            if (room == null) return;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Log("NOT RUN: capture " + name + " (null graphics device)"); return; }
            captureCamera.transform.position = World(eye);
            captureCamera.transform.rotation = Quaternion.LookRotation(World(target) - World(eye), room.transform.TransformDirection(up));
            captureCamera.orthographic = flat;
            if (flat) captureCamera.orthographicSize = fieldOfView;
            else captureCamera.fieldOfView = fieldOfView;
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

        // ------------------------------------------------------------------ the run

        static string V(Vector3 v) => "(" + v.x.ToString("0.00") + ", " + v.y.ToString("0.00") + ", " + v.z.ToString("0.00") + ")";

        static void Check(bool ok, string failure)
        {
            if (ok) return;
            failures.Add(failure);
            Log("FAIL: " + failure);
        }

        static void Finish()
        {
            foreach (var e in errors) Log("ERROR logged in Play: " + e.Split('\n')[0]);
            int code = failures.Count == 0 && errors.Count == 0 ? 0 : 1;
            Log("War room result: " + (code == 0 ? "PASS" : "FAIL (" + failures.Count + " checks, " + errors.Count + " errors)"));
            StopPlay(code);
        }

        static void Log(string value)
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Path.Combine(Folder, Report), value + "\n");
            if (value.Length > 0) Debug.Log("[WarRoomEnding] " + value);
        }

        static void StopPlay(int code)
        {
            stopping = true;
            Application.logMessageReceived -= OnLog;
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
