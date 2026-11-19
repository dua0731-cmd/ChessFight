using System;
using System.Collections.Generic;
using System.Globalization;
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
    // R725: the royal vault's own checks (왕실 금고실, build spec §5.4). Every timing and layout number comes from
    // RoyalVaultRules or from the built geometry; none is typed here twice. Not a play test, and nothing goes online.
    //
    // Edit (ChessFight > Pawn Rush > Validate > Royal Vault (Edit), RunEditBatch): both prefabs' stored values, then the
    // section prefab built by RoyalVaultBuilder in a scratch scene at (0, 0, 20000) without waking it (Awake does not run
    // in Edit mode, so every part stands in its rest pose: plates flat, die up, door shut, drawers and panels in), and
    //   E1  both prefabs' id, display name, kind, entry, exit and exit yaw equal RoyalVaultSection's (PITFALLS #68)
    //   E2  every model renderer wears the one grey-box material, every cue only the cue materials, signs are ink
    //   E3  every solid has NoClimbSurface on its own object
    //   E4  every walkable top of the layout is CourseFloor, the stairs' walking slopes and all ten plate decks too
    //   E5  floor all along every branch (both tower lanes on and 0.8 m off their line, the corridor, the walkway, both
    //       belts, the neck, the shelf, the plates' inner and outer lines, the door): steps under 0.31 m, gaps only
    //       between towers (TowerGapOffLine, lips of a tower step) and seams under 0.1 m
    //   E6  rails 1.15 m high on every regroup platform's edge over the pit but its listed openings
    //   E7  a kill volume under 1 m off every platform and tower edge, nothing solid on the way down
    //   E8  a pawn pushed off the shelf's open edge (5 m/s west, the Pawn Rush fall) lands on a belt or the landing's start
    //   E9  CP0..CP6 at RoyalVaultRules.CheckpointZ, CP0 with its reach box, CP3's trigger before the pad; the map test's
    //       Start
    //   E10 every floor sample of E5 inside a beat
    //   E11 no ISectionNetPart     E12 no FinishZone
    //   E13 the corridor joins: every solid between the entry and the exit, nothing in either corridor's way, the floors
    //       level 0.6 m in
    //   E14 every renderer and solid named "RV ", "Cue " or "Label ", every trigger a checkpoint, kill volume, launch pad
    //       or knock (or a knock's let-through zone, on its own object with no script), no "RV placeholder" left
    //   E15 the launch pad as RoyalVaultRules has it, its flight inside the IntendedGap
    // plus the section checks every section gets (SectionValidator.Run: spans, the route, checkpoint floors, no-climb,
    // the kill volume, entry and exit), and no error logged while building.
    //
    // Play (Validate > Royal Vault (Play), RunPlayBatch): the vault alone, its entry put on (100, 5, 200) and configured as
    // slot 5 before it wakes (-vaultRoundSeed N: a whole round instead, which must have drawn the vault), at 120 Hz. After
    // a second the shared clock is frozen at chosen times (ObstacleClock.Use) and every part is read once it has stood
    // there for three physics steps and two frames:
    //   P1  every mover's pose equals RoyalVaultRules, each against its own index (hammer heads, hurdle bars, bulkhead
    //       panels, the die, drawer faces, plate angles, the door's centre and roll, the shove on the disc's centre)
    //   P2  the hammer heads sweep the walkway's whole width, 0.1 m over it, moving -z at the bottom
    //   P3  the die down meets every point across the neck at 0.5 m; up, the 1.5 m under it is clear
    //   P4  one row of plates is flat at every sample; lifted decks tilt outward
    //   P5  the door shut closes the doorway; passable, a 2.5 m way is clear; the disc rolls without slipping; closing,
    //       the shove's leading face is never behind the rim
    //   P6  the belts carry riders at BeltSpeed along the section's +z
    //   P7  the bulkheads always leave 3.9 m clear
    //   P8  every cue shows its rule's state, the belt slats follow BeltTravel, the gold edges stay lit; a countdown copy is
    //       in view from the follow camera on the terrace
    //   P9  no error logged in Play
    //   P10 loose capsules on and round the launch pad are thrown and come down on the shelf inside the landing box
    // with pictures of the vault from above and of each beat beside the report (-vaultNoCapture: none).
    //
    // Report Logs/RoyalVaultValidation/vault.txt (the last run; vault_edit.txt and vault_play.txt keep each mode's last).
    // Exit 0 only when every check passed. Batch, on the copy (Tools/Run-RoyalVault.ps1 -What ValidateEdit | Validate),
    // without -quit (both exit by themselves):
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.RoyalVaultValidation.RunEditBatch -logFile <file>
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.RoyalVaultValidation.RunPlayBatch
    //         [-vaultRoundSeed 12] [-vaultNoCapture] -logFile <file>
    [InitializeOnLoad]
    public static class RoyalVaultValidation
    {
        const string ActiveKey = "ChessFight.R725.Vault.ValidatePlay";
        const string ExitKey = "ChessFight.R725.Vault.ValidateExit";
        const string CodeKey = "ChessFight.R725.Vault.ValidateCode";
        const string Folder = "Logs/RoyalVaultValidation";
        const string Report = "vault.txt";
        public const string SectionPrefab = "Assets/Prefabs/PawnRushSections/Late_3-3_RoyalVault.prefab";
        public const string MapPrefab = "Assets/Prefabs/PawnRushMaps/RoyalVault.prefab";
        const string SetPath = "Assets/Settings/PawnRushSections.asset";
        const float RuntimeLimit = 900f, Settle = 1f;
        // The layout's floor tops (spec §1): the forecourt, J1 to the neck, the shelf to the plates. The rules hold only the
        // top one, the terrace's and the room's (ExitY).
        const float FloorBottom = 0f, FloorLow = 4f, FloorHigh = 8f;
        static float FloorTop => (float)RoyalVaultRules.ExitY;
        // The Pawn Rush fall: gravity x1.4, x1.15 more on the way down (PawnRushPhysicsParams.cs:20-22, as
        // RoyalVaultRules.JumpAirSeconds and ForkTwoDropCost have it).
        const float FallGravity = 9.81f * 1.4f * 1.15f;
        // Where the Play check puts the vault's entry (spec §5.4), and the frozen clock's first time (the Core tests' large
        // clock, so the movers are read at the scale a match's shared clock runs at).
        static readonly Vector3 PlayEntry = new Vector3(100f, 5f, 200f);
        const double T0 = 1762000003.25;
        // P1, P4 and P6-P8 read 70 times 0.23 s apart: 16 s, a whole door period and more of everything else; the step
        // falls on both halves of the cues' 4 Hz blink. Nudge: half the gap of a finite difference.
        const int Samples = 70;
        const double SampleStep = .23, Nudge = .02;

        static readonly List<string> failures = new List<string>();
        static readonly List<string> errors = new List<string>();
        static readonly Dictionary<string, Tally> tallies = new Dictionary<string, Tally>();
        static readonly List<string> tallyOrder = new List<string>();
        static readonly Queue<Action> pictures = new Queue<Action>();
        static PawnRushSection section;
        static Transform frame, content;
        static Camera captureCamera;
        static int phase, roundSeed;
        static double beganAt, phaseAt, playRequestedAt, editorIdleAt;
        static bool stopping;

        static RoyalVaultValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        // ================================================================== tallies and the report

        // A check sampled many times: how many samples, how many broke it, the worst (or the first broken) one.
        sealed class Tally
        {
            public bool Flag, AtLeast;
            public int Samples, Bad;
            public double Limit, Worst = double.NaN;
            public string Unit = "", WorstAt = "", FirstBad = "";
        }

        static Tally Get(string name, double limit, string unit, bool flag, bool atLeast)
        {
            if (!tallies.TryGetValue(name, out var tally))
            {
                tallies[name] = tally = new Tally { Limit = limit, Unit = unit, Flag = flag, AtLeast = atLeast };
                tallyOrder.Add(name);
            }
            return tally;
        }

        // One sample of a measured check: it passes while `value` is at most `limit` (or at least it, `atLeast`).
        static void Measure(string name, double value, double limit, string at, string unit = " m", bool atLeast = false)
        {
            var tally = Get(name, limit, unit, false, atLeast);
            tally.Samples++;
            if (double.IsNaN(value)) value = atLeast ? double.NegativeInfinity : double.PositiveInfinity;
            if (double.IsNaN(tally.Worst) || (atLeast ? value < tally.Worst : value > tally.Worst))
            {
                tally.Worst = value;
                tally.WorstAt = at;
            }
            if (atLeast ? value >= limit : value <= limit) return;
            if (tally.Bad == 0) tally.FirstBad = F(value) + unit + " at " + at;
            tally.Bad++;
        }

        // One sample of a yes-or-no check.
        static void Expect(string name, bool ok, string at)
        {
            var tally = Get(name, 0, "", true, false);
            tally.Samples++;
            if (ok) return;
            if (tally.Bad == 0) tally.FirstBad = at;
            tally.Bad++;
        }

        static void Check(bool ok, string failure)
        {
            if (ok) return;
            failures.Add(failure);
            Log("FAIL: " + failure);
        }

        // Every tally into the report, a failure for each one broken.
        static void Flush()
        {
            foreach (var name in tallyOrder)
            {
                var t = tallies[name];
                string line = (t.Bad == 0 ? "PASS  " : "FAIL  ") + name + ": " + t.Samples + " sample" + (t.Samples == 1 ? "" : "s");
                if (!t.Flag)
                    line += ", " + (t.AtLeast ? "least " : "worst ") + F(t.Worst) + t.Unit + " (limit " + F(t.Limit) + t.Unit + ") at " + t.WorstAt;
                if (t.Bad > 0) line += "; " + t.Bad + " bad, the first " + t.FirstBad;
                Log(line);
                if (t.Bad > 0) failures.Add(name + ": " + t.Bad + " of " + t.Samples + ", the first " + t.FirstBad);
            }
            tallies.Clear();
            tallyOrder.Clear();
        }

        static void Header(string mode, string what)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, Report), "R725 royal vault, " + mode + " checks (" +
                DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "): " + what + "\n");
        }

        static void Log(string value)
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Path.Combine(Folder, Report), value + "\n");
            if (value.Length > 0) Debug.Log("[RoyalVault] " + value);
        }

        // The tallies, the result and every failure; the report is also kept as vault_<mode>.txt. The number of failures.
        static int End(string mode)
        {
            Flush();
            Log("");
            Log("Royal vault " + mode + " result: " + (failures.Count == 0 ? "PASS" : "FAIL (" + failures.Count + ")"));
            foreach (var f in failures) Log("  " + f);
            try { File.Copy(Path.Combine(Folder, Report), Path.Combine(Folder, "vault_" + mode.ToLowerInvariant() + ".txt"), true); }
            catch (IOException) { }
            return failures.Count;
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            // The render pipeline's editor package about its own ray-tracing shader on the first capture in batch mode (R516,
            // MiniGamePrefabValidation): not the game's.
            if (condition != null && condition.StartsWith("Host type is not matching any asset type") && condition.Contains(".urtshader")) return;
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                errors.Add(condition + "\n" + stack);
        }

        // ================================================================== the section frame

        static Vector3 L(Vector3 world) => frame.InverseTransformPoint(world);
        static Vector3 W(Vector3 local) => frame.TransformPoint(local);
        static Vector3 LDir(Vector3 world) => frame.InverseTransformDirection(world);
        static Quaternion LRot(Quaternion world) => Quaternion.Inverse(frame.rotation) * world;
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static string F(double v) => double.IsInfinity(v) ? (v > 0 ? "inf" : "-inf") : v.ToString("0.###", CultureInfo.InvariantCulture);
        static string V(Vector3 v) => "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")";
        static string Clock(double t) => "clock T0 + " + (t - T0).ToString("0.00", CultureInfo.InvariantCulture) + " s";
        static string Name(Transform t) => t.parent != null && t.parent != content && t.parent != frame ? t.parent.name + "/" + t.name : t.name;

        // A collider's box in the section frame: a box collider's own corners, anything else its world bounds' corners.
        static Bounds SectionBox(Collider c)
        {
            var corners = new Vector3[8];
            if (c is BoxCollider box)
            {
                var h = box.size * .5f;
                for (int i = 0; i < 8; i++)
                    corners[i] = L(box.transform.TransformPoint(box.center + new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z)));
            }
            else
            {
                var b = c.bounds;
                for (int i = 0; i < 8; i++)
                    corners[i] = L(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
            }
            var result = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < 8; i++) result.Encapsulate(corners[i]);
            return result;
        }

        static Vector3 Centre(Collider c) => c is BoxCollider box ? box.transform.TransformPoint(box.center) : c.bounds.center;

        // A moving part's collider, which the floor rays look past: an Obstacle's or an ObstacleSpan's child without CourseFloor
        // (Course01Validator.IsObstacle's rule, with the vault's movers; plate decks count as floor).
        static bool Moving(Collider c) =>
            c.GetComponent<CourseFloor>() == null && (c.GetComponentInParent<Obstacle>() != null || c.GetComponentInParent<ObstacleSpan>() != null);

        // The first solid under a section-frame point, `depth` down, that is not a moving part's.
        static bool Down(Vector3 local, float depth, out RaycastHit hit)
        {
            var hits = Physics.RaycastAll(W(local), -frame.up, depth, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (Moving(h.collider)) continue;
                hit = h;
                return true;
            }
            hit = default;
            return false;
        }

        // The top of the floor under a point expected near `local.y` (section frame), or NaN.
        static float FloorAt(Vector3 local) =>
            Down(new Vector3(local.x, local.y + 1.6f, local.z), 3.2f, out var hit) ? L(hit.point).y : float.NaN;

        static List<string> Solids(Vector3 centre, Vector3 half)
        {
            var found = new List<string>();
            foreach (var c in Physics.OverlapBox(W(centre), half, frame.rotation, ~0, QueryTriggerInteraction.Ignore))
                found.Add(Name(c.transform));
            return found;
        }

        static bool Overlaps(Vector3 centre, Vector3 half, Collider target)
        {
            foreach (var c in Physics.OverlapBox(W(centre), half, frame.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (c == target) return true;
            return false;
        }

        static List<Transform> Named(string name) =>
            content.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToList();

        static Collider NamedCollider(string name) =>
            Named(name).Select(t => t.GetComponent<Collider>()).FirstOrDefault(c => c != null && !c.isTrigger);

        // The collider called `name` under `root`, else (noted) the largest one of the same kind (trigger or solid).
        static Collider Part(Transform root, string name, bool trigger = false)
        {
            Collider best = null;
            float size = -1f;
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger != trigger) continue;
                if (c.name == name) return c;
                var b = c.bounds.size;
                if (b.x * b.y * b.z > size) { size = b.x * b.y * b.z; best = c; }
            }
            if (best != null) Log("note: " + Name(root) + " has no " + name + "; read " + Name(best.transform) + " instead");
            return best;
        }

        static bool IsCue(Transform t)
        {
            for (; t != null && t != content && t != frame; t = t.parent)
                if (t.name.StartsWith("Cue ")) return true;
            return false;
        }

        static bool Shown(Renderer r) => r.enabled && r.gameObject.activeInHierarchy;
        static string MaterialName(Renderer r) => r.sharedMaterial != null ? r.sharedMaterial.name : "none";
        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < .02f && Mathf.Abs(a.g - b.g) < .02f && Mathf.Abs(a.b - b.b) < .02f;
        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        static int Nearest(double[] values, float at)
        {
            int best = 0;
            for (int i = 1; i < values.Length; i++)
                if (Math.Abs(values[i] - at) < Math.Abs(values[best] - at)) best = i;
            return best;
        }

        // ================================================================== Edit

        [MenuItem("ChessFight/Pawn Rush/Validate/Royal Vault (Edit)", false, 206)]
        public static void RunEditMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            int bad = RunEdit(false);
            EditorUtility.DisplayDialog("Royal Vault (Edit)", bad == 0 ? "왕실 금고실 Edit 검사 통과 (오류 0)"
                : "왕실 금고실 Edit 검사 실패 " + bad + "개 - " + Folder + "/" + Report + "를 보세요", "확인");
        }

        public static void RunEditBatch()
        {
            int bad;
            try { bad = RunEdit(true); }
            catch (Exception e)
            {
                Log("FAIL: " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
                bad = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(bad == 0 ? 0 : 1);
        }

        static int RunEdit(bool batch)
        {
            failures.Clear(); errors.Clear(); tallies.Clear(); tallyOrder.Clear(); platforms.Clear();
            Header("Edit", "both prefabs' stored values; the vault built by RoyalVaultBuilder in a scratch scene at (0, 0, 20000) without waking " +
                   "(every part in its rest pose), E1-E15 and the section checks (SectionValidator).\nNot a play test and not online.");
            E1Prefabs();
            var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, batch ? NewSceneMode.Single : NewSceneMode.Additive);
            Application.logMessageReceived += OnLog;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SectionPrefab);
                if (prefab == null) { Check(false, "no section prefab at " + SectionPrefab); return End("Edit"); }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scratch);
                go.transform.position = new Vector3(0f, 0f, 20000f);
                var vault = go.GetComponent<RoyalVaultSection>();
                section = go.GetComponent<PawnRushSection>();
                if (vault == null || section == null) { Check(false, SectionPrefab + " lacks RoyalVaultSection or PawnRushSection"); return End("Edit"); }
                RoyalVaultBuilder.Build(vault);
                frame = section.transform;
                content = frame.Find(RoyalVaultSection.ContentName);
                if (content == null) { Check(false, "RoyalVaultBuilder.Build left no " + RoyalVaultSection.ContentName); return End("Edit"); }
                Physics.SyncTransforms();
                Inventory();
                E1Built();
                E2Materials();
                E3NoClimb();
                E4Floors();
                E5Branches();
                E6Rails();
                E7KillVolumes();
                E8DrawerDrops();
                E9Checkpoints();
                E10Beats();
                E11E12Parts();
                E13Corridor();
                E14Names();
                E15Pad();
                Sections();
                Check(errors.Count == 0, "E0 " + errors.Count + " error(s) logged while building and checking, the first: " + (errors.Count > 0 ? errors[0].Split('\n')[0] : ""));
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                if (!batch) EditorSceneManager.CloseScene(scratch, true);
            }
            return End("Edit");
        }

        static void Inventory()
        {
            Log("Built: " + content.GetComponentsInChildren<Spinner>(true).Length + " spinners, " + content.GetComponentsInChildren<VaultMover>(true).Length + " movers, " +
                content.GetComponentsInChildren<VaultPlate>(true).Length + " plates, " + content.GetComponentsInChildren<VaultBeltSurface>(true).Length + " belts, " +
                content.GetComponentsInChildren<VaultPressWave>(true).Length + " press wave, " + content.GetComponentsInChildren<VaultCue>(true).Length + " cues, " +
                content.GetComponentsInChildren<VaultSlats>(true).Length + " slat drivers, " + content.GetComponentsInChildren<YardImpact>(true).Length + " knocks, " +
                content.GetComponentsInChildren<ObstacleSpan>(true).Length + " spans, " + content.GetComponentsInChildren<CourseCheckpoint>(true).Length + " checkpoints, " +
                content.GetComponentsInChildren<KillVolume>(true).Length + " kill volumes, " + content.GetComponentsInChildren<Collider>(true).Length + " colliders, " +
                content.GetComponentsInChildren<Renderer>(true).Length + " renderers");
        }

        // ------------------------------------------------------------------ E1 the prefabs

        static void E1Prefabs()
        {
            foreach (var path in new[] { SectionPrefab, MapPrefab })
            {
                // Unity keeps a prefab's imported values until it is imported again (CarriageYardBotTest, R661).
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) { Check(false, "E1 no prefab at " + path); continue; }
                var s = asset.GetComponent<PawnRushSection>();
                var vault = asset.GetComponent<RoyalVaultSection>();
                Check(vault != null, "E1 " + path + ": no RoyalVaultSection");
                if (s == null) { Check(false, "E1 " + path + ": no PawnRushSection"); continue; }
                var stored = new SerializedObject(s);
                var yawProperty = stored.FindProperty("exitYaw");
                float yaw = yawProperty != null ? yawProperty.floatValue : float.NaN;
                Vector3 entry = s.EntryLocal.position, exit = s.ExitLocal.position;
                Check(s.Id == RoyalVaultSection.Id, "E1 " + path + ": id " + s.Id + ", not " + RoyalVaultSection.Id);
                Check(s.DisplayName == RoyalVaultSection.DisplayName, "E1 " + path + ": display name " + s.DisplayName + ", not " + RoyalVaultSection.DisplayName);
                Check(s.Kind == PawnRushSection.SlotKind.Obstacle, "E1 " + path + ": kind " + s.Kind + ", not Obstacle");
                Check((entry - RoyalVaultSection.Entry).sqrMagnitude < 1e-8f, "E1 " + path + ": entry " + V(entry) + ", not " + V(RoyalVaultSection.Entry));
                Check((exit - RoyalVaultSection.Exit).sqrMagnitude < 1e-8f, "E1 " + path + ": exit " + V(exit) + ", not " + V(RoyalVaultSection.Exit));
                Check(Mathf.Abs(yaw) < 1e-4f, "E1 " + path + ": exit yaw " + F(yaw) + ", not 0");
                if (vault != null)
                {
                    var build = new SerializedObject(vault).FindProperty("buildIfEmpty");
                    Check(build != null && build.boolValue, "E1 " + path + ": RoyalVaultSection does not build itself when empty (buildIfEmpty)");
                    Check(!vault.HasContent, "E1 " + path + ": baked Content in a root-only prefab (it would go stale)");
                }
                Log("E1 " + path + ": " + s.Id + " \"" + s.DisplayName + "\", " + s.Kind + ", entry " + V(entry) + ", exit " + V(exit) + ", exit yaw " + F(yaw));
            }
        }

        static void E1Built()
        {
            float yaw = Mathf.DeltaAngle(section.ExitLocal.rotation.eulerAngles.y, 0f);
            Check(section.Id == RoyalVaultSection.Id && section.DisplayName == RoyalVaultSection.DisplayName && section.Kind == PawnRushSection.SlotKind.Obstacle &&
                  (section.EntryLocal.position - RoyalVaultSection.Entry).sqrMagnitude < 1e-8f && (section.ExitLocal.position - RoyalVaultSection.Exit).sqrMagnitude < 1e-8f &&
                  Mathf.Abs(yaw) < 1e-3f,
                  "E1 the builder's Describe gives " + section.Id + ", " + section.Kind + ", entry " + V(section.EntryLocal.position) + ", exit " + V(section.ExitLocal.position) +
                  ", not the prefab's values");
        }

        // ------------------------------------------------------------------ E2 the grey box

        static void E2Materials()
        {
            string grey = RoyalVaultLook.Greybox.name;
            var cueMaterials = new[] { RoyalVaultLook.CueRed.name, RoyalVaultLook.CueGreen.name, RoyalVaultLook.CueGold.name, RoyalVaultLook.Ink.name };
            int models = 0, cueRenderers = 0;
            foreach (var r in content.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<TextMesh>() != null) continue;
                bool cue = IsCue(r.transform);
                if (cue) cueRenderers++;
                else models++;
                foreach (var m in r.sharedMaterials)
                {
                    string n = m != null ? m.name : "none";
                    if (cue) Expect("E2 cues wear only CueRed, CueGreen, CueGold or Ink", Array.IndexOf(cueMaterials, n) >= 0, Name(r.transform) + " wears " + n);
                    else Expect("E2 every model wears " + grey, n == grey, Name(r.transform) + " wears " + n);
                }
            }
            Check(RoyalVaultLook.Greybox.mainTexture == null, "E2 " + grey + " has a texture");
            var colours = new[] { (Color)RoyalVaultLook.InkColor, (Color)RoyalVaultLook.GreenColor, (Color)RoyalVaultLook.RedColor };
            foreach (var text in content.GetComponentsInChildren<TextMesh>(true))
            {
                if (text.name.StartsWith("Label ")) Expect("E2 signs in ink", Near(text.color, RoyalVaultLook.InkColor), Name(text.transform) + " in " + Hex(text.color));
                else if (IsCue(text.transform)) Expect("E2 cue words in ink, green or red", colours.Any(c => Near(text.color, c)), Name(text.transform) + " in " + Hex(text.color));
                else Expect("E2 every text a sign (\"Label ...\") or a cue", false, Name(text.transform));
            }
            foreach (string roof in new[] { "RV roof", "RV room roof" })
            {
                var r = Named(roof).Select(t => t.GetComponent<Renderer>()).FirstOrDefault(x => x != null);
                Check(r != null && r.shadowCastingMode == ShadowCastingMode.Off, "E2 " + roof + (r == null ? " is missing" : " casts shadows"));
            }
            Log("E2: " + models + " model renderers, " + cueRenderers + " cue renderers");
        }

        // ------------------------------------------------------------------ E3 nothing climbable

        static void E3NoClimb()
        {
            int solids = 0;
            foreach (var c in content.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) continue;
                solids++;
                Expect("E3 NoClimbSurface on every solid's own object", c.GetComponent<NoClimbSurface>() != null, Name(c.transform));
            }
            Log("E3: " + solids + " solids");
        }

        // ------------------------------------------------------------------ E4 walkable tops

        static readonly string[] WalkableTops =
        {
            "RV entrance sill", "RV forecourt", "RV tower rest", "RV corridor floor", "RV J1", "RV hammer walkway", "RV approach", "RV belt W", "RV belt E",
            "RV landing", "RV cabinet E", "RV neck", "RV shelf S", "RV shelf N", "RV J2", "RV terrace", "RV door sill", "RV room floor",
        };

        static void E4Floors()
        {
            var names = new List<string>(WalkableTops);
            for (int k = 1; k <= RoyalVaultRules.TowerCount; k++)
                if (k != RoyalVaultRules.TowerRest) { names.Add("RV tower W" + k); names.Add("RV tower E" + k); }
            foreach (var n in names)
            {
                var found = Named(n);
                Check(found.Count > 0, "E4 no " + n);
                foreach (var t in found)
                {
                    var c = t.GetComponent<Collider>();
                    Expect("E4 the layout's walkable tops are solid CourseFloor", c != null && !c.isTrigger && t.GetComponent<CourseFloor>() != null, Name(t));
                }
            }
            for (int s = 1; s <= 3; s++)
            {
                var slopes = content.GetComponentsInChildren<CourseFloor>(true).Where(f => f.sloped && f.name.StartsWith("RV stair " + s)).ToList();
                Check(slopes.Count == 1, "E4 RV stair " + s + ": " + slopes.Count + " walking slopes (one wanted)");
            }
            var plates = content.GetComponentsInChildren<VaultPlate>(true);
            int decks = 0;
            foreach (var plate in plates)
            {
                var floors = plate.GetComponentsInChildren<CourseFloor>(true).Where(f => f.GetComponent<Collider>() != null && !f.GetComponent<Collider>().isTrigger).ToList();
                Expect("E4 every plate has its deck (CourseFloor) before waking", floors.Count == 1, Name(plate.transform) + ": " + floors.Count + " decks");
                decks += floors.Count;
            }
            Check(plates.Length == 2 * RoyalVaultRules.PlateCount, "E4 " + plates.Length + " plates (" + 2 * RoyalVaultRules.PlateCount + " wanted)");
            Log("E4: " + names.Count + " named tops, 3 stairs, " + decks + " plate decks");
        }

        // ------------------------------------------------------------------ E5 floor along the branches

        // A line a pawn walks (section frame, its points' y the floor it expects there).
        sealed class Branch
        {
            public string Name;
            public Vector3[] Points;
            public bool Towers;
            public float MaxGap;
        }

        struct Spot
        {
            public Vector3 At;
            public int Kind;
            public float Y;
            public string What;
        }

        static readonly List<Vector3> floorSamples = new List<Vector3>();

        static List<Branch> Branches()
        {
            var list = new List<Branch>();
            float lane = (float)RoyalVaultRules.TowerLaneX, j1 = (float)RoyalVaultRules.TowerTop(RoyalVaultRules.TowerCount + 1);
            float forecourt = (float)RoyalVaultRules.ForecourtZ1, j1z = (float)RoyalVaultRules.J1Z0, walk = (float)RoyalVaultRules.WalkZ1;
            // Fork 1, risky: each lane on its line and 0.8 m either side of it, the forecourt to J1 (y rises 0 -> 4 over the field).
            foreach (float side in new[] { -1f, 1f })
                foreach (float off in new[] { -.8f, 0f, .8f })
                {
                    float x = side * lane + off;
                    list.Add(new Branch
                    {
                        Name = "towers x " + F(x), Towers = true, MaxGap = (float)RoyalVaultRules.TowerGapOffLine(Mathf.Abs(off)) + .05f,
                        Points = new[] { new Vector3(x, FloorBottom, 2f), new Vector3(x, FloorBottom, forecourt), new Vector3(x, j1, j1z), new Vector3(x, j1, j1z + 2f) },
                    });
                }
            // Fork 1, safe: in from the forecourt, along the corridor's middle and its halves' middles, up stair 1 (the 12 m before J1).
            float cx = (float)RoyalVaultRules.CorridorX, halfCorridor = (float)RoyalVaultRules.BulkheadHalfWidth / 2;
            foreach (float x in new[] { cx - halfCorridor, cx, cx + halfCorridor })
                list.Add(new Branch
                {
                    Name = "corridor x " + F(x), MaxGap = .1f,
                    Points = new[] { new Vector3(-8f, FloorBottom, 4f), new Vector3(x, FloorBottom, 4f), new Vector3(x, FloorBottom, j1z - 12f), new Vector3(x, FloorLow, j1z), new Vector3(x, FloorLow, j1z + 2f) },
                });
            foreach (float x in new[] { -3f, 0f, 3f })
                list.Add(new Branch
                {
                    Name = "J1, hammer walkway, approach x " + F(x), MaxGap = .1f,
                    Points = new[] { new Vector3(x, FloorLow, j1z + 2f), new Vector3(x, FloorLow, walk), new Vector3(x, FloorLow, walk + 3f) },
                });
            // Fork 2, safe: each belt's middle, the press neck at the die's x and its sides, stair 2 to J2.
            float bz1 = (float)RoyalVaultRules.BeltZ1, nz1 = (float)RoyalVaultRules.NeckZ1, s2 = (float)RoyalVaultRules.StairTwoZ1, px = (float)RoyalVaultRules.PressX;
            foreach (float x in new[] { (float)(RoyalVaultRules.BeltWestX0 + RoyalVaultRules.BeltSplitX) / 2, (float)(RoyalVaultRules.BeltSplitX + RoyalVaultRules.BeltEastX1) / 2 })
                list.Add(new Branch
                {
                    Name = "belt x " + F(x) + " to the press", MaxGap = .1f,
                    Points = new[] { new Vector3(x, FloorLow, walk + 3f), new Vector3(x, FloorLow, bz1 - .5f), new Vector3(px, FloorLow, bz1 + 2f), new Vector3(px, FloorLow, nz1), new Vector3(px, FloorHigh, s2), new Vector3(px, FloorHigh, s2 + 2f) },
                });
            foreach (float x in new[] { px - 3f, px + 3f })
                list.Add(new Branch
                {
                    Name = "press neck x " + F(x), MaxGap = .1f,
                    Points = new[] { new Vector3(x, FloorLow, bz1 + 1f), new Vector3(x, FloorLow, nz1), new Vector3(x, FloorHigh, s2), new Vector3(x, FloorHigh, s2 + 2f) },
                });
            // Fork 2, risky: the shelf from its landing zone (past the lip) to J2.
            float shelfMid = (float)(RoyalVaultRules.ShelfX0 + RoyalVaultRules.ShelfX1) / 2;
            foreach (float x in new[] { shelfMid - 1f, shelfMid, shelfMid + 1f })
                list.Add(new Branch
                {
                    Name = "shelf x " + F(x), MaxGap = .1f,
                    Points = new[] { new Vector3(x, FloorHigh, (float)RoyalVaultRules.ShelfZ0 + .5f), new Vector3(x, FloorHigh, s2), new Vector3(x, FloorHigh, s2 + 2f) },
                });
            // The plates' rows on their inner and outer lines, stair 3, the terrace; the door and the room to the exit.
            float pz0 = (float)RoyalVaultRules.PlateZ0, pz1 = (float)RoyalVaultRules.PlatesZ1, tz = (float)RoyalVaultRules.TerraceZ0;
            foreach (float x in new[] { -3.2f, -.8f, .8f, 3.2f })
                list.Add(new Branch
                {
                    Name = "plates x " + F(x), MaxGap = .1f,
                    Points = new[] { new Vector3(x, FloorHigh, pz0 - 2f), new Vector3(x, FloorHigh, pz1), new Vector3(x, FloorTop, tz), new Vector3(x, FloorTop, tz + 2f) },
                });
            foreach (float x in new[] { -2f, 0f, 2f })
                list.Add(new Branch
                {
                    Name = "terrace, door and room x " + F(x), MaxGap = .1f,
                    Points = new[] { new Vector3(x, FloorTop, tz + 1f), new Vector3(x, FloorTop, (float)RoyalVaultRules.ExitZ - .2f) },
                });
            return list;
        }

        // What a branch point stands over: 1 a floor (CourseFloor), 0 nothing within 1.6 m under it, 2 something else first.
        static int Ground(Vector3 p, out float y, out string what)
        {
            y = float.NaN;
            what = null;
            if (!Down(new Vector3(p.x, p.y + 1.6f, p.z), 3.2f, out var hit)) return 0;
            y = L(hit.point).y;
            if (hit.collider.GetComponent<CourseFloor>() != null) return 1;
            what = Name(hit.collider.transform) + " (top " + F(y) + ")";
            return 2;
        }

        // The edge between a floor point and one off it, to a millimetre or so.
        static Vector3 Refine(Vector3 floor, Vector3 off)
        {
            for (int k = 0; k < 12; k++)
            {
                var mid = (floor + off) * .5f;
                if (Ground(mid, out _, out _) == 1) floor = mid;
                else off = mid;
            }
            return (floor + off) * .5f;
        }

        static List<Spot> Walk(Branch branch)
        {
            var spots = new List<Spot>();
            for (int i = 1; i < branch.Points.Length; i++)
            {
                Vector3 a = branch.Points[i - 1], b = branch.Points[i];
                int n = Mathf.Max(1, Mathf.CeilToInt(Flat(b - a).magnitude / .25f));
                for (int k = i == 1 ? 0 : 1; k <= n; k++)
                {
                    var p = Vector3.Lerp(a, b, (float)k / n);
                    int kind = Ground(p, out float y, out string what);
                    spots.Add(new Spot { At = p, Kind = kind, Y = y, What = what });
                }
            }
            return spots;
        }

        static void E5Branches()
        {
            floorSamples.Clear();
            foreach (var branch in Branches())
            {
                var spots = Walk(branch);
                int floors = 0, gaps = 0, wrong = 0, last = -1;
                float step = 0f, widest = 0f, lip = 0f;
                string firstWrong = null, stepAt = "", widestAt = "";
                for (int i = 0; i < spots.Count; i++)
                {
                    var s = spots[i];
                    if (s.Kind == 2)
                    {
                        wrong++;
                        if (firstWrong == null) firstWrong = s.What + " at " + V(s.At);
                    }
                    if (s.Kind != 1) continue;
                    floors++;
                    floorSamples.Add(new Vector3(s.At.x, s.Y, s.At.z));
                    if (last >= 0 && last == i - 1)
                    {
                        float d = Mathf.Abs(s.Y - spots[last].Y);
                        if (d > step) { step = d; stepAt = V(s.At); }
                    }
                    else if (last >= 0)
                    {
                        gaps++;
                        Vector3 from = Refine(spots[last].At, spots[last + 1].At), to = Refine(s.At, spots[i - 1].At);
                        float gap = Flat(to - from).magnitude;
                        if (gap > widest) { widest = gap; widestAt = V(from) + " to " + V(to); }
                        lip = Mathf.Max(lip, Mathf.Abs(s.Y - spots[last].Y));
                    }
                    last = i;
                }
                float lipLimit = branch.Towers ? (float)RoyalVaultRules.TowerStep + .05f : .31f;
                Check(wrong == 0, "E5 " + branch.Name + ": " + wrong + " point(s) over something that is not floor, the first " + firstWrong);
                Check(spots.Count > 0 && spots[0].Kind == 1 && spots[spots.Count - 1].Kind == 1, "E5 " + branch.Name + ": does not start and end on floor");
                Check(step <= .31f, "E5 " + branch.Name + ": a step of " + F(step) + " m at " + stepAt);
                Check(widest <= branch.MaxGap, "E5 " + branch.Name + ": a gap of " + F(widest) + " m (" + widestAt + "), wider than " + F(branch.MaxGap) + " m");
                Check(lip <= lipLimit, "E5 " + branch.Name + ": a lip of " + F(lip) + " m over a gap (" + F(lipLimit) + " m allowed)");
                Log("E5 " + branch.Name + ": " + spots.Count + " points, " + floors + " on floor, " + gaps + " gap(s), widest " + F(widest) + " m (" + F(branch.MaxGap) +
                    " allowed), largest lip " + F(lip) + " m, largest step " + F(step) + " m");
            }
        }

        // ------------------------------------------------------------------ E6, E7 the pit's edges

        // A platform's edge: a line along x (a north or south edge, at z `At`) or along z (west or east, at x `At`) from `From` to
        // `To`, on a top at `Top`, `Out` the sign of the way off it; `Open` holds pairs of along-coordinates meant to stay open.
        sealed class Edge
        {
            public string Name;
            public bool AlongX;
            public float At, From, To, Top, Out;
            public float[] Open = new float[0];

            public Vector3 Point(float s) => AlongX ? new Vector3(s, Top, At) : new Vector3(At, Top, s);
            public Vector3 Outward => AlongX ? new Vector3(0f, 0f, Out) : new Vector3(Out, 0f, 0f);

            public bool Opening(float s, float margin)
            {
                for (int i = 0; i + 1 < Open.Length; i += 2)
                    if (s >= Open[i] - margin && s <= Open[i + 1] + margin) return true;
                return false;
            }
        }

        static readonly Dictionary<string, Bounds> platforms = new Dictionary<string, Bounds>();

        // A named floor's box (section frame); false, and one failure, when it is not there.
        static bool Platform(string name, out Bounds b)
        {
            if (platforms.TryGetValue(name, out b)) return b.size.sqrMagnitude > 0f;
            var c = NamedCollider(name);
            b = c != null ? SectionBox(c) : new Bounds();
            platforms[name] = b;
            Check(c != null, "E6/E7: no " + name);
            return c != null;
        }

        static Edge North(string name, Bounds b, params float[] open) => new Edge { Name = name + " N", AlongX = true, At = b.max.z, From = b.min.x, To = b.max.x, Top = b.max.y, Out = 1f, Open = open };
        static Edge South(string name, Bounds b, params float[] open) => new Edge { Name = name + " S", AlongX = true, At = b.min.z, From = b.min.x, To = b.max.x, Top = b.max.y, Out = -1f, Open = open };
        static Edge West(string name, Bounds b, params float[] open) => new Edge { Name = name + " W", AlongX = false, At = b.min.x, From = b.min.z, To = b.max.z, Top = b.max.y, Out = -1f, Open = open };
        static Edge East(string name, Bounds b, params float[] open) => new Edge { Name = name + " E", AlongX = false, At = b.max.x, From = b.min.z, To = b.max.z, Top = b.max.y, Out = 1f, Open = open };

        // The regroup platforms' edges over the pit and their openings (spec §1 and §5.4 E6): the tower take-offs (a lane's
        // middle +- TowerTakeoffHalf), the rest's landing edge (left out), J1's tower landings and stair 1, the walkway at J1
        // and the approach, the belts, stair 2 and the shelf at J2, the plates, stair 3.
        static List<Edge> RailedEdges()
        {
            var list = new List<Edge>();
            float lane = (float)RoyalVaultRules.TowerLaneX, half = (float)RoyalVaultRules.TowerTakeoffHalf;
            var takeoffs = new[] { -lane - half, -lane + half, lane - half, lane + half };
            if (Platform("RV forecourt", out var forecourt)) { list.Add(North("forecourt", forecourt, takeoffs)); list.Add(East("forecourt", forecourt)); }
            if (Platform("RV tower rest", out var rest)) { list.Add(North("tower rest", rest, takeoffs)); list.Add(West("tower rest", rest)); list.Add(East("tower rest", rest)); }
            if (Platform("RV J1", out var j1))
            {
                list.Add(South("J1", j1, -22.3f, -13.7f, -4f, -.4f, .4f, 4f));
                list.Add(West("J1", j1));
                list.Add(East("J1", j1));
                list.Add(North("J1", j1, -4f, 4f));
            }
            if (Platform("RV approach", out var approach))
            {
                list.Add(South("approach", approach, -4f, 4f));
                list.Add(West("approach", approach));
                list.Add(East("approach", approach));
                list.Add(North("approach", approach, -6.3f, 4.3f));
            }
            if (Platform("RV J2", out var j2))
            {
                list.Add(South("J2", j2, -6.3f, 2.3f, 2.7f, 7.3f));
                list.Add(West("J2", j2));
                list.Add(East("J2", j2));
                list.Add(North("J2", j2, -4f, 4f));
            }
            if (Platform("RV terrace", out var terrace)) { list.Add(South("terrace", terrace, -4.3f, 4.3f)); list.Add(West("terrace", terrace)); list.Add(East("terrace", terrace)); }
            return list;
        }

        // Every platform edge a pawn can fall off (the towers are walked round separately).
        static List<Edge> PitEdges()
        {
            var list = RailedEdges();
            if (Platform("RV tower rest", out var rest)) list.Add(South("tower rest", rest));
            if (Platform("RV corridor floor", out var corridor)) { list.Add(West("corridor", corridor)); list.Add(East("corridor", corridor)); }
            if (Platform("RV hammer walkway", out var walk)) { list.Add(West("hammer walkway", walk)); list.Add(East("hammer walkway", walk)); }
            if (Platform("RV shelf S", out var shelfS)) list.Add(East("shelf S", shelfS));
            if (Platform("RV shelf N", out var shelfN)) list.Add(East("shelf N", shelfN));
            var decks = content.GetComponentsInChildren<VaultPlate>(true).SelectMany(p => p.GetComponentsInChildren<CourseFloor>(true))
                               .Select(f => f.GetComponent<Collider>()).Where(c => c != null).Select(SectionBox).ToList();
            foreach (int side in new[] { -1, 1 })
            {
                var row = decks.Where(b => b.center.x * side > 0f).ToList();
                if (row.Count == 0) continue;
                var all = row[0];
                foreach (var b in row) all.Encapsulate(b);
                list.Add(side < 0 ? West("plates", all) : East("plates", all));
            }
            return list;
        }

        static IEnumerable<float> Along(Edge e, float step)
        {
            int n = Mathf.Max(1, Mathf.FloorToInt((e.To - e.From) / step));
            float used = (e.To - e.From) / n;
            for (int i = 0; i < n; i++) yield return e.From + used * (i + .5f);
        }

        // What lies under a point 1 m off an edge (section frame, at its floor's top): 0 the pit (a kill volume below and
        // nothing solid on the way), 1 something within 2 m under the top or the point inside a solid (a neighbouring floor,
        // a tower, a wall), 2 something solid further down before any kill volume, or no kill volume at all (`what`).
        static int Beyond(Vector3 at, out string what)
        {
            what = null;
            var origin = W(at + Vector3.up);
            if (Physics.CheckSphere(origin, .05f, ~0, QueryTriggerInteraction.Ignore)) return 1;
            var hits = Physics.RaycastAll(origin, -frame.up, 90f, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider.isTrigger)
                {
                    if (h.collider.GetComponent<KillVolume>() != null) return 0;
                    continue;
                }
                if (Moving(h.collider)) continue;
                if (h.distance <= 3f) return 1;
                what = "falls onto " + Name(h.collider.transform) + " at y " + F(L(h.point).y);
                return 2;
            }
            what = "no kill volume under it";
            return 2;
        }

        // A barrier on the edge line at height y: a thin probe from 0.4 m inside the edge to 0.4 m outside it meets a solid.
        static bool Barrier(Edge e, Vector3 p, float y)
        {
            var half = e.AlongX ? new Vector3(.05f, .005f, .4f) : new Vector3(.4f, .005f, .05f);
            foreach (var c in Physics.OverlapBox(W(new Vector3(p.x, y, p.z)), half, frame.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (!Moving(c)) return true;
            return false;
        }

        static void E6Rails()
        {
            foreach (var edge in RailedEdges())
            {
                int railed = 0, open = 0, beside = 0;
                foreach (float s in Along(edge, .25f))
                {
                    if (edge.Opening(s, .15f)) { open++; continue; }
                    var p = edge.Point(s);
                    if (Beyond(p + edge.Outward, out _) == 1) { beside++; continue; }
                    railed++;
                    bool low = Barrier(edge, p, edge.Top + .25f), high = Barrier(edge, p, edge.Top + 1.145f);
                    Expect("E6 rails 1.15 m high on the regroup platforms' edges over the pit", low && high,
                           edge.Name + " at " + V(p) + (low ? "" : ", open at the floor") + (high ? "" : ", lower than 1.15 m"));
                }
                Log("E6 " + edge.Name + ": " + railed + " points over the pit checked, " + open + " in its openings, " + beside + " beside other floor or a wall");
            }
        }

        static void E7KillVolumes()
        {
            int pit = 0, beside = 0;
            foreach (var edge in PitEdges())
                foreach (float s in Along(edge, .5f))
                {
                    var p = edge.Point(s) + edge.Outward;
                    int kind = Beyond(p, out string what);
                    if (kind == 1) { beside++; continue; }
                    pit++;
                    Expect("E7 a kill volume under 1 m off every platform edge", kind == 0, edge.Name + " at " + V(p) + ": " + what);
                }
            for (int k = 1; k <= RoyalVaultRules.TowerCount; k++)
            {
                if (k == RoyalVaultRules.TowerRest) continue;
                foreach (string lane in new[] { "W", "E" })
                {
                    var tower = NamedCollider("RV tower " + lane + k);
                    if (tower == null) continue;   // E4 says so
                    var b = SectionBox(tower);
                    float r = b.extents.x + 1f;
                    for (int a = 0; a < 24; a++)
                    {
                        float angle = a * 15f * Mathf.Deg2Rad;
                        var p = new Vector3(b.center.x + r * Mathf.Cos(angle), b.max.y, b.center.z + r * Mathf.Sin(angle));
                        int kind = Beyond(p, out string what);
                        if (kind == 1) { beside++; continue; }
                        pit++;
                        Expect("E7 a kill volume under 1 m off every tower", kind == 0, "RV tower " + lane + k + " at " + V(p) + ": " + what);
                    }
                }
            }
            Log("E7: " + pit + " points 1 m off platform and tower edges over the pit, " + beside + " beside other floor or a wall");
        }

        // ------------------------------------------------------------------ E8 drawer drops

        static void E8DrawerDrops()
        {
            int belt = 0, landing = 0, caught = 0;
            const float dt = .01f;
            for (double z = RoyalVaultRules.ShelfOpenZ0; z <= RoyalVaultRules.ShelfOpenZ1 + 1e-6; z += .25)
            {
                var p = new Vector3((float)RoyalVaultRules.ShelfX0 + .2f, FloorHigh + .5f, (float)z);
                var v = new Vector3(-5f, 0f, 0f);
                string fail = null;
                bool done = false;
                for (int k = 0; k < 300 && !done; k++)
                {
                    var next = p + v * dt + Vector3.down * (.5f * FallGravity * dt * dt);
                    var d = next - p;
                    if (Physics.SphereCast(W(p), (float)RoyalVaultRules.BodyHalf, frame.TransformDirection(d.normalized), out var hit, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    {
                        done = true;
                        var at = L(hit.point);
                        if (hit.collider.GetComponent<VaultBeltSurface>() != null) belt++;
                        else if (hit.collider.name == "RV landing" && at.z <= (float)RoyalVaultRules.ShelfOpenZ1 + .1f) landing++;
                        else if (hit.collider.name.StartsWith("RV rail shelf")) caught++;
                        else fail = "lands on " + Name(hit.collider.transform) + " at " + V(at);
                    }
                    p = next;
                    v += Vector3.down * (FallGravity * dt);
                }
                if (!done) fail = "nothing under it";
                Expect("E8 a pawn pushed off the shelf's open edge lands on a belt or the landing's start", fail == null, "z " + F(z) + ": " + fail);
            }
            Log("E8: pushed off from z " + F(RoyalVaultRules.ShelfOpenZ0) + " to " + F(RoyalVaultRules.ShelfOpenZ1) + " (5 m/s west, " + F(FallGravity) + " m/s² down): " +
                belt + " land on a belt, " + landing + " on the landing's start, " + caught + " against a rail end");
        }

        // ------------------------------------------------------------------ E9 checkpoints

        static void E9Checkpoints()
        {
            var cps = content.GetComponentsInChildren<CourseCheckpoint>(true).OrderBy(c => c.Number).ToArray();
            int count = RoyalVaultRules.CheckpointZ.Length;
            Check(cps.Select(c => c.Number).SequenceEqual(Enumerable.Range(0, count)),
                  "E9 checkpoints " + string.Join(", ", cps.Select(c => c.Number)) + " (0.." + (count - 1) + " wanted)");
            float last = float.NegativeInfinity;
            foreach (var cp in cps)
            {
                var at = L(cp.transform.position);
                Check(at.z > last, "E9 CP" + cp.Number + " (z " + F(at.z) + ") is not past the one before");
                last = at.z;
                if (cp.Number >= 0 && cp.Number < count)
                    Check(Mathf.Abs(at.z - (float)RoyalVaultRules.CheckpointZ[cp.Number]) < .05f,
                          "E9 CP" + cp.Number + " at z " + F(at.z) + ", RoyalVaultRules.CheckpointZ has " + F(RoyalVaultRules.CheckpointZ[cp.Number]));
                var boxes = cp.GetComponents<BoxCollider>();
                Check(boxes.Length > 0 && boxes.All(b => b.isTrigger), "E9 CP" + cp.Number + ": not all of its boxes are triggers");
                string first = boxes.Length > 0 ? ", trigger z " + F(SectionBox(boxes[0]).min.z) + ".." + F(SectionBox(boxes[0]).max.z) : "";
                Log("E9 CP" + cp.Number + " at " + V(at) + ", " + cp.SpotCount + " spots, " + boxes.Length + " box(es)" + first);
            }
            var cp0 = cps.FirstOrDefault(c => c.Number == 0);
            if (cp0 != null && Platform("RV forecourt", out var forecourt))
            {
                var boxes = cp0.GetComponents<BoxCollider>();
                Check(boxes.Length >= 2, "E9 CP0 has no reach box");
                if (boxes.Length >= 2)
                {
                    // The reach box spans the forecourt at its south end, before the corridor's mouth closes at z 8 (spec §1.2).
                    var reach = SectionBox(boxes[1]);
                    Check(reach.min.x <= forecourt.min.x + .05f && reach.max.x >= forecourt.max.x - .05f && reach.min.z <= forecourt.min.z + .05f && reach.max.z < 8f,
                          "E9 CP0's reach box x " + F(reach.min.x) + ".." + F(reach.max.x) + ", z " + F(reach.min.z) + ".." + F(reach.max.z) + " does not span the forecourt's entry end");
                }
            }
            var cp3 = cps.FirstOrDefault(c => c.Number == 3);
            if (cp3 != null)
            {
                var box = cp3.GetComponent<BoxCollider>();
                float padSouth = (float)(RoyalVaultRules.PadZ - RoyalVaultRules.PadSize / 2);
                Check(box != null && SectionBox(box).min.z < padSouth, "E9 CP3's trigger does not begin before the pad (z " + F(padSouth) + ")");
            }
            var starts = frame.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Start").ToArray();
            Check(starts.Length == 1, "E9 " + starts.Length + " Start objects (one wanted: the map test starts there)");
            if (starts.Length == 1)
            {
                var at = L(starts[0].position);
                float yaw = Mathf.DeltaAngle(LRot(starts[0].rotation).eulerAngles.y, 0f);
                Check(Vector3.Distance(at, new Vector3(0f, FloorBottom, 3f)) < .05f && Mathf.Abs(yaw) < .5f, "E9 Start at " + V(at) + ", yaw " + F(yaw) + " (spec: (0, 0, 3), yaw 0)");
            }
            Check(frame.GetComponentsInChildren<SpawnPoint>(true).Length == 0, "E9 the vault has spawn points: the map test would start there, not at Start");
        }

        // ------------------------------------------------------------------ E10 .. E15

        static void E10Beats()
        {
            foreach (var p in floorSamples) Expect("E10 every floor sample inside a beat", section.BeatAt(W(p)) != null, V(p));
            Log("E10: " + floorSamples.Count + " floor samples; beats " + string.Join(" | ", section.Beats.Select(b => b.name).Distinct()));
        }

        static void E11E12Parts()
        {
            int net = frame.GetComponentsInChildren<ISectionNetPart>(true).Length, parts = frame.GetComponentsInChildren<ISectionPart>(true).Length;
            Check(net == 0, "E11 " + net + " ISectionNetPart in the vault (every part runs on the shared clock)");
            int finish = frame.GetComponentsInChildren<FinishZone>(true).Length;
            Check(finish == 0, "E12 " + finish + " FinishZone in the vault (the round's finish is the ending's)");
            Log("E11, E12: " + net + " online parts, " + parts + " ISectionPart, " + finish + " finish zones");
        }

        static void E13Corridor()
        {
            float half = SectionCorridor.Half, exitZ = (float)RoyalVaultRules.ExitZ, length = SectionCorridor.Length;
            foreach (var c in content.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) continue;
                var b = SectionBox(c);
                bool inside = b.min.z >= -.02f && b.max.z <= exitZ + .02f;
                bool wing = b.max.z <= .62f && b.min.z >= -SectionCorridor.Wall - .02f && (b.min.x >= half - .02f || b.max.x <= -half + .02f);
                Expect("E13 every solid between the entry and the exit (or a south wall beside the entrance)", inside || wing,
                       Name(c.transform) + " z " + F(b.min.z) + ".." + F(b.max.z) + ", x " + F(b.min.x) + ".." + F(b.max.x));
            }
            foreach (float x in new[] { -5.5f, 0f, 5.5f })
            {
                Measure("E13 floor 0.6 m into the entry, level with the corridor (0.06 m)", Mathf.Abs(FloorAt(new Vector3(x, FloorBottom, .6f)) - FloorBottom), .06, "x " + F(x));
                Measure("E13 floor 0.6 m before the exit, level with the corridor (0.06 m)", Mathf.Abs(FloorAt(new Vector3(x, FloorTop, exitZ - .6f)) - FloorTop), .06, "x " + F(x));
            }
            // The corridors' walked box (SectionCorridorValidation's) before the entry and after the exit.
            var walked = new Vector3(half - .1f, 1.5f, length * .5f - .1f);
            var before = Solids(new Vector3(0f, FloorBottom + 1.65f, -length * .5f), walked);
            var after = Solids(new Vector3(0f, FloorTop + 1.65f, exitZ + length * .5f), walked);
            Check(before.Count == 0, "E13 vault solids in the corridor before the entry: " + string.Join(", ", before));
            Check(after.Count == 0, "E13 vault solids in the corridor after the exit: " + string.Join(", ", after));
        }

        static bool VaultName(string n) => n.StartsWith("RV ") || n.StartsWith("Cue ") || n.StartsWith("Label ");

        static void E14Names()
        {
            foreach (var r in content.GetComponentsInChildren<Renderer>(true))
                Expect("E14 every renderer named \"RV \", \"Cue \" or \"Label \"", VaultName(r.name), Name(r.transform));
            // R725 fun pass: a trigger a knock lists with its solids (YardImpact.alsoSolid) only widens the box that keeps a
            // knocked-down body let through (the hurdles' zones); it collides with nothing and nobody hears it.
            var rooms = new HashSet<Collider>(content.GetComponentsInChildren<YardImpact>(true).Where(k => k.alsoSolid != null)
                                                     .SelectMany(k => k.alsoSolid).Where(c => c != null && c.isTrigger));
            foreach (var c in content.GetComponentsInChildren<Collider>(true))
            {
                if (!c.isTrigger) Expect("E14 every solid named \"RV \", \"Cue \" or \"Label \"", VaultName(c.name), Name(c.transform));
                else Expect("E14 every trigger a Checkpoint, KillVolume, LaunchPad or YardImpact, or a knock's let-through zone",
                            c.GetComponent<Checkpoint>() != null || c.GetComponent<KillVolume>() != null || c.GetComponent<LaunchPad>() != null || c.GetComponent<YardImpact>() != null
                            || (rooms.Contains(c) && VaultName(c.name) && c.GetComponents<MonoBehaviour>().Length == 0),
                            Name(c.transform));
            }
            var placeholders = content.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("RV placeholder")).Select(t => t.name).ToArray();
            Check(placeholders.Length == 0, "E14 " + placeholders.Length + " \"RV placeholder\" object(s) left: " + string.Join(", ", placeholders.Take(10)));
        }

        static void E15Pad()
        {
            var pads = content.GetComponentsInChildren<LaunchPad>(true);
            Check(pads.Length == 1, "E15 " + pads.Length + " launch pads (one wanted)");
            if (pads.Length == 0) return;
            var lp = pads[0];
            var box = lp.GetComponent<BoxCollider>();
            if (box == null) { Check(false, "E15 the launch pad has no box"); return; }
            Check(box.isTrigger, "E15 the launch pad's box is not a trigger");
            var centre = L(lp.transform.TransformPoint(box.center));
            var size = Vector3.Scale(box.size, YardKit.Abs(lp.transform.lossyScale));
            float surface = L(lp.Surface).y;
            var offset = new Vector3((float)(RoyalVaultRules.PadLandX - RoyalVaultRules.PadX), (float)(RoyalVaultRules.PadLandY - RoyalVaultRules.PadFloor),
                                     (float)(RoyalVaultRules.PadLandZ - RoyalVaultRules.PadZ));
            var landing = new Vector3((float)RoyalVaultRules.PadLandX, (float)RoyalVaultRules.PadLandY, (float)RoyalVaultRules.PadLandZ);
            Check(Mathf.Abs(centre.x - (float)RoyalVaultRules.PadX) < .01f && Mathf.Abs(centre.z - (float)RoyalVaultRules.PadZ) < .01f,
                  "E15 the pad's box is centred " + V(centre) + ", not over (PadX, PadZ)");
            Check(Mathf.Abs(size.x - (float)RoyalVaultRules.PadSize) < .01f && Mathf.Abs(size.z - (float)RoyalVaultRules.PadSize) < .01f,
                  "E15 the pad's box is " + F(size.x) + " x " + F(size.z) + " m, not PadSize " + F(RoyalVaultRules.PadSize));
            Check(Mathf.Abs(surface - (float)RoyalVaultRules.PadFloor) < .01f, "E15 the pad throws from y " + F(surface) + ", not PadFloor " + F(RoyalVaultRules.PadFloor));
            Check((lp.LandingOffset - offset).magnitude < .01f, "E15 the pad's landing offset " + V(lp.LandingOffset) + ", not " + V(offset));
            Check((L(lp.Landing) - landing).magnitude < .02f, "E15 the pad lands at " + V(L(lp.Landing)) + ", not " + V(landing));
            Check(Mathf.Abs(lp.Clearance - (float)RoyalVaultRules.PadClearance) < 1e-3f, "E15 the pad's clearance " + F(lp.Clearance) + ", not " + F(RoyalVaultRules.PadClearance));
            Check(lp.SameThrow, "E15 the pad aims every throw at one spot (sameThrow off)");
            Check(lp.Period <= 0f, "E15 the pad is a spring (period " + F(lp.Period) + ")");
            Check(Quaternion.Angle(LRot(lp.transform.rotation), Quaternion.identity) < .1f, "E15 the pad is turned");
            // Its west face 1 m clear of the walkway's line (x 4: the walkway's east edge).
            float line = Platform("RV hammer walkway", out var walk) ? walk.max.x : 4f, west = centre.x - size.x / 2;
            Check(west >= line + 1f - 1e-3f, "E15 the pad's west face at x " + F(west) + ", less than 1 m east of x " + F(line));
            // The flight: wherever the feet are more than 5 m over anything, they are inside the pad flight's IntendedGap.
            var gaps = content.GetComponentsInChildren<IntendedGap>(true);
            RoyalVaultRules.PadThrow(out _, out _, out _, out double seconds);
            int open = 0, covered = 0;
            for (double s = 0; s <= seconds; s += .05)
            {
                RoyalVaultRules.PadFlight(RoyalVaultRules.PadX, RoyalVaultRules.PadZ, s, out double x, out double y, out double z);
                var feet = new Vector3((float)x, (float)y, (float)z);
                if (Down(feet, 5f, out _)) continue;
                open++;
                if (gaps.Any(g => g.Contains(W(feet)))) covered++;
                else Expect("E15 the pad's flight inside its IntendedGap", false, V(feet) + " " + F(s) + " s into the flight");
            }
            Log("E15: pad box " + V(centre) + " " + F(size.x) + " x " + F(size.y) + " x " + F(size.z) + " m, throws from y " + F(surface) + " to " + V(L(lp.Landing)) +
                ", clearance " + F(lp.Clearance) + ", sameThrow " + lp.SameThrow + "; " + gaps.Length + " IntendedGap, " + covered + " of " + open + " open flight points inside it");
        }

        static void Sections()
        {
            var notes = new List<string>();
            var found = SectionValidator.Run(section, notes);
            foreach (var n in notes) Log("SectionValidator: " + n);
            foreach (var e in found) Check(false, "SectionValidator: " + e);
            Log("SectionValidator: " + found.Count + " error(s)");
        }

        // ================================================================== Play

        [MenuItem("ChessFight/Pawn Rush/Validate/Royal Vault (Play)", false, 207)]
        public static void RunPlayMenu() => RunPlayBatch();

        public static void RunPlayBatch()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Header("Play", "the vault in Play Mode (its entry on " + V(PlayEntry) + ", slot 5), the shared clock frozen at chosen times, P1-P10, and pictures.\n" +
                   "Not a play test (no pawn runs it) and not online.");
            AssetDatabase.ImportAsset(SectionPrefab, ImportAssetOptions.ForceUpdate);
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

        static bool NoCapture => Array.IndexOf(Environment.GetCommandLineArgs(), "-vaultNoCapture") >= 0;

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                ResetPlay();
                roundSeed = Arg("-vaultRoundSeed", 0);
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
                captureCamera.nearClipPlane = .1f; captureCamera.farClipPlane = 500f;
                captureCamera.enabled = true;
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) CompleteExit();
        }

        static void ResetPlay()
        {
            failures.Clear(); errors.Clear(); tallies.Clear(); tallyOrder.Clear(); pictures.Clear(); platforms.Clear();
            phase = 0; stopping = false; section = null; frame = null; content = null;
            Array.Clear(hurdles, 0, 2); Array.Clear(hurdleBars, 0, 2); Array.Clear(hammers, 0, 2); Array.Clear(heads, 0, 2);
            Array.Clear(panels, 0, panels.Length); Array.Clear(panelBoxes, 0, panelBoxes.Length);
            Array.Clear(drawers, 0, 2); Array.Clear(drawerBoxes, 0, 2);
            Array.Clear(plates, 0, plates.Length); Array.Clear(decks, 0, decks.Length);
            Array.Clear(belts, 0, 2); Array.Clear(beltBoxes, 0, 2);
            Array.Clear(headRead, 0, headRead.Length); Array.Clear(discRead, 0, discRead.Length);
            die = disc = shove = null; dieBox = discBody = shoveShell = null; pad = null;
            cues.Clear(); gold.Clear(); slats[0].Clear(); slats[1].Clear(); slatZ[0].Clear(); slatZ[1].Clear(); slatClock = double.NaN;
            cueStates.Clear(); probes.Clear(); bodies.Clear();
            probeIndex = 0; probeWaiting = false;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || stopping) return;
            // R516: the Editor's Error Pause stops Play on a render pipeline message that is not the game's.
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            try
            {
                if (EditorApplication.timeSinceStartup - beganAt > RuntimeLimit)
                    throw new InvalidOperationException("the vault check exceeded " + RuntimeLimit + " seconds");
                if (pictures.Count > 0) { pictures.Dequeue()(); return; }   // one picture a frame, the clock still held
                double now = EditorApplication.timeSinceStartup;
                switch (phase)
                {
                    case 0:
                        Place();
                        phase = 1; phaseAt = now;
                        return;
                    case 1:
                        if (now - phaseAt < Settle) return;
                        Physics.SyncTransforms();
                        Collect();
                        Plan();
                        phase = 2;
                        return;
                    case 2:
                        if (Probing()) phase = 3;
                        return;
                    case 3:
                        Evaluate();
                        StartThrows();
                        phase = 4;
                        return;
                    case 4:
                        if (!Throwing()) phase = 5;
                        return;
                    case 5:
                        Finish();
                        return;
                }
            }
            catch (Exception e)
            {
                Log("FAIL: " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
                failures.Add("the check stopped: " + e.GetType().Name + ": " + e.Message);
                ObstacleClock.Use(null);
                End("Play");
                StopPlay(1);
            }
        }

        // ------------------------------------------------------------------ the vault in Play

        static void Place()
        {
            // The round scenes' physics (PhysicsProfile: 120 Hz, 24 iterations), so the thrown bodies fly as pawns would.
            new GameObject("Physics profile (vault check)").AddComponent<PhysicsProfile>();
            Log("Physics " + (1f / Time.fixedDeltaTime).ToString("0", CultureInfo.InvariantCulture) + " Hz, " + Physics.defaultSolverIterations + " solver iterations");
            if (roundSeed != 0) { PlaceRound(); return; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SectionPrefab);
            if (prefab == null) throw new InvalidOperationException("no section prefab at " + SectionPrefab);
            // Under an inactive holder, so it is placed and configured before anything in it wakes (PawnRushRound's way).
            var holder = new GameObject("Royal vault check");
            holder.SetActive(false);
            var go = Object.Instantiate(prefab, holder.transform);
            go.name = prefab.name;
            section = go.GetComponent<PawnRushSection>();
            if (section == null) throw new InvalidOperationException(SectionPrefab + " has no PawnRushSection");
            section.PlaceEntryOn(new Pose(PlayEntry, Quaternion.identity));
            section.Configure(4, 1, 6, ObstacleClock.Now);
            holder.SetActive(true);
            frame = section.transform;
            Log("The vault alone: entry on " + V(PlayEntry) + ", root at " + V(frame.position) + ", slot 5 (checkpoints 400..406)");
        }

        // Option B (-vaultRoundSeed): a whole round from the section set, which must have drawn the vault in slot 5.
        static void PlaceRound()
        {
            var go = new GameObject("Royal vault check round");
            go.SetActive(false);
            var round = go.AddComponent<PawnRushRound>();
            var so = new SerializedObject(round);
            so.FindProperty("set").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PawnRushSectionSet>(SetPath);
            so.FindProperty("buildOnAwake").boolValue = false;
            so.FindProperty("startSpots").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true);
            round.Build(roundSeed);
            Log("Round seed " + roundSeed + ": " + string.Join(" | ", round.Sections.Select(s => (s.Slot + 1) + " " + s.Id)));
            section = round.Sections.FirstOrDefault(s => s != null && s.GetComponent<RoyalVaultSection>() != null);
            if (section == null)
            {
                var late = round.Sections.FirstOrDefault(s => s != null && s.Slot == 4);
                throw new InvalidOperationException("slot 5 drew " + (late != null ? late.Id : "nothing") + ", not the royal vault (round seed " + roundSeed + ")");
            }
            frame = section.transform;
            Log("The vault in the round: slot " + (section.Slot + 1) + ", root at " + V(frame.position));
        }

        // The parts the Play checks read, each found by what it is and where it stands (its index from its place, so a
        // part driven by another index's rule shows).
        static readonly Transform[] hurdles = new Transform[2], hurdleBars = new Transform[2], hammers = new Transform[2], drawers = new Transform[2];
        static readonly Collider[] heads = new Collider[2], drawerBoxes = new Collider[2], beltBoxes = new Collider[2];
        static readonly Transform[,] panels = new Transform[2, 2];
        static readonly Collider[,] panelBoxes = new Collider[2, 2];
        static readonly float[,] panelY = new float[2, 2];
        static readonly Transform[,] plates = new Transform[2, RoyalVaultRules.PlateCount];
        static readonly Collider[,] decks = new Collider[2, RoyalVaultRules.PlateCount];
        static readonly IMovingSurface[] belts = new IMovingSurface[2];
        static Transform die, disc, shove;
        static Collider dieBox, discBody, shoveShell;
        static LaunchPad pad;
        static float neckTop, walkwayTop;
        static Bounds walkway, neck;

        enum CueKind { Bulkhead, Hammer, Press, Belt, Drawer, Plate, Door }

        sealed class CueRef
        {
            public string Name;
            public CueKind Kind;
            public int Index, Side;
            public Renderer[] Renderers;
            public TextMesh Text;
        }

        static readonly List<CueRef> cues = new List<CueRef>();
        static readonly List<Renderer> gold = new List<Renderer>();
        static readonly List<Transform>[] slats = { new List<Transform>(), new List<Transform>() };
        static int partsFound, partsWanted;

        // One part the Play checks need: counted, and a failure when it is missing.
        static void Need(bool ok, string what)
        {
            partsWanted++;
            if (ok) partsFound++;
            else Check(false, "P1 no " + what);
        }

        static void Collect()
        {
            content = frame.Find(RoyalVaultSection.ContentName);
            if (content == null) throw new InvalidOperationException("the vault woke without its " + RoyalVaultSection.ContentName);
            foreach (var spinner in content.GetComponentsInChildren<Spinner>(true))
            {
                var t = spinner.transform;
                var at = L(t.position);
                int h = Nearest(RoyalVaultRules.HurdleHubZ, at.z), i = Nearest(RoyalVaultRules.HammerAxleZ, at.z);
                if (Mathf.Abs(at.x - (float)RoyalVaultRules.HurdleHubX) < .5f && Mathf.Abs(at.z - (float)RoyalVaultRules.HurdleHubZ[h]) < .5f)
                {
                    Check(hurdles[h] == null, "P1 two hurdles at z " + F(RoyalVaultRules.HurdleHubZ[h]));
                    hurdles[h] = t;
                    var bar = t.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "RV hurdle bar");
                    hurdleBars[h] = bar != null ? bar : t;
                    Measure("P1 hurdle hubs where RoyalVaultRules has them (0.02 m)", Vector3.Distance(at, new Vector3((float)RoyalVaultRules.HurdleHubX, FloorBottom, (float)RoyalVaultRules.HurdleHubZ[h])), .02, Name(t));
                }
                else if (Mathf.Abs(at.x) < .5f && Mathf.Abs(at.z - (float)RoyalVaultRules.HammerAxleZ[i]) < .5f)
                {
                    Check(hammers[i] == null, "P1 two hammers at z " + F(RoyalVaultRules.HammerAxleZ[i]));
                    hammers[i] = t;
                    heads[i] = Part(t, "RV hammer head");
                }
                else Check(false, "P1 a spinner nobody expects: " + Name(t) + " at " + V(at));
            }
            foreach (var mover in content.GetComponentsInChildren<VaultMover>(true))
            {
                var t = mover.transform;
                var at = L(t.position);
                string n = t.name;
                if (n.StartsWith("RV bulkhead panel"))
                {
                    int b = Nearest(RoyalVaultRules.BulkheadZ, at.z), s = at.x < (float)RoyalVaultRules.CorridorX ? 0 : 1;
                    Check(panels[b, s] == null, "P1 two bulkhead panels for bulkhead " + (b + 1) + (s == 0 ? " W" : " E"));
                    panels[b, s] = t;
                    panelBoxes[b, s] = Part(t, "RV bulkhead panel");
                    panelY[b, s] = at.y;
                }
                else if (n.StartsWith("RV press die")) { die = t; dieBox = Part(t, "RV press die box"); }
                else if (n.StartsWith("RV drawer"))
                {
                    int k = Nearest(RoyalVaultRules.DrawerZ, at.z);
                    Check(drawers[k] == null, "P1 two drawers at z " + F(RoyalVaultRules.DrawerZ[k]));
                    drawers[k] = t;
                    drawerBoxes[k] = Part(t, "RV drawer box");
                }
                else if (n.StartsWith("RV door disc")) { disc = t; discBody = Part(t, "RV door disc body"); }
                else if (n.StartsWith("RV door shove")) { shove = t; shoveShell = Part(t, "RV door (hit)", true); }
                else Check(false, "P1 a mover nobody expects: " + Name(t));
            }
            foreach (var plate in content.GetComponentsInChildren<VaultPlate>(true))
            {
                var at = L(plate.transform.position);
                int row = at.x < 0f ? 0 : 1;
                var deck = plate.GetComponentsInChildren<CourseFloor>(true).Select(f => f.GetComponent<Collider>()).FirstOrDefault(c => c != null);
                float cz = deck != null ? SectionBox(deck).center.z : at.z;
                int i = Mathf.RoundToInt((cz - (float)(RoyalVaultRules.PlateZ0 + RoyalVaultRules.PlatePitch / 2)) / (float)RoyalVaultRules.PlatePitch);
                if (i < 0 || i >= RoyalVaultRules.PlateCount || plates[row, i] != null) { Check(false, "P1 a plate out of its place: " + Name(plate.transform) + " at " + V(at)); continue; }
                plates[row, i] = plate.transform;
                decks[row, i] = deck;
            }
            foreach (var surface in content.GetComponentsInChildren<VaultBeltSurface>(true))
            {
                var c = surface.GetComponent<Collider>();
                float x = c != null ? SectionBox(c).center.x : L(surface.transform.position).x;
                int b = x < (float)RoyalVaultRules.BeltSplitX ? 0 : 1;
                Check(belts[b] == null, "P6 two belts " + (b == 0 ? "W" : "E"));
                belts[b] = surface.GetComponent<IMovingSurface>();
                beltBoxes[b] = c;
            }
            foreach (var cue in content.GetComponentsInChildren<VaultCue>(true))
            {
                var t = cue.transform;
                string n = t.name;
                var renderers = cue.GetComponentsInChildren<Renderer>(true);
                var text = cue.GetComponent<TextMesh>();
                var at = renderers.Length > 0 && text == null ? L(renderers[0].bounds.center) : L(t.position);
                var c = new CueRef { Name = Name(t), Renderers = renderers, Text = text };
                if (n.StartsWith("Cue bulkhead")) { c.Kind = CueKind.Bulkhead; c.Index = Nearest(RoyalVaultRules.BulkheadZ, at.z); c.Side = at.x < (float)RoyalVaultRules.CorridorX ? -1 : 1; }
                else if (n.StartsWith("Cue hammer")) { c.Kind = CueKind.Hammer; c.Index = Nearest(RoyalVaultRules.HammerAxleZ, at.z); }
                else if (n.StartsWith("Cue press")) c.Kind = CueKind.Press;
                else if (n.StartsWith("Cue belt") && !n.StartsWith("Cue belt slat")) { c.Kind = CueKind.Belt; c.Index = at.x < (float)RoyalVaultRules.BeltSplitX ? 0 : 1; }
                else if (n.StartsWith("Cue drawer")) { c.Kind = CueKind.Drawer; c.Index = Nearest(RoyalVaultRules.DrawerZ, at.z); }
                else if (n.StartsWith("Cue plate"))
                {
                    var owner = cue.GetComponentInParent<VaultPlate>(true);
                    c.Kind = CueKind.Plate;
                    c.Index = (owner != null ? L(owner.transform.position).x : at.x) < 0f ? 0 : 1;
                }
                else if (n.StartsWith("Cue door countdown")) { c.Kind = CueKind.Door; Check(text != null, "P8 " + c.Name + " has no TextMesh"); }
                else { Check(false, "P8 a cue nobody reads: " + c.Name); continue; }
                cues.Add(c);
            }
            foreach (var r in content.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsCue(r.transform) || r.GetComponentInParent<VaultCue>(true) != null || r.GetComponent<TextMesh>() != null) continue;
                if (r.name.StartsWith("Cue belt slat")) slats[L(r.bounds.center).x < (float)RoyalVaultRules.BeltSplitX ? 0 : 1].Add(r.transform);
                else gold.Add(r);
            }
            pad = content.GetComponentInChildren<LaunchPad>(true);
            neckTop = FloorAt(new Vector3((float)RoyalVaultRules.PressX, FloorLow, (float)RoyalVaultRules.PressBandZ0 + .3f));
            walkwayTop = FloorAt(new Vector3(0f, FloorLow, (float)(RoyalVaultRules.HammerAxleZ[0] + RoyalVaultRules.HammerAxleZ[1]) / 2));
            var walk = NamedCollider("RV hammer walkway");
            var neckFloor = NamedCollider("RV neck");
            walkway = walk != null ? SectionBox(walk) : new Bounds(new Vector3(0f, FloorLow - .5f, 71.5f), new Vector3(8f, 1f, 23f));
            neck = neckFloor != null ? SectionBox(neckFloor) : new Bounds(new Vector3((float)RoyalVaultRules.PressX, FloorLow - .5f, 110.5f), new Vector3(8f, 1f, 7f));
            for (int i = 0; i < 2; i++)
                if (hammers[i] != null && !float.IsNaN(walkwayTop))
                    Measure("P1 hammer axles where RoyalVaultRules has them (0.02 m)",
                            Vector3.Distance(L(hammers[i].position), new Vector3(0f, walkwayTop + (float)RoyalVaultRules.HammerAxleAbove, (float)RoyalVaultRules.HammerAxleZ[i])), .02, Name(hammers[i]));

            partsFound = partsWanted = 0;
            for (int i = 0; i < 2; i++)
            {
                Need(hurdles[i] != null, "hurdle " + (i + 1));
                Need(hammers[i] != null && heads[i] != null, "hammer " + (i + 1) + " with its head");
                Need(drawers[i] != null && drawerBoxes[i] != null, "drawer " + (i + 1) + " with its box");
                Need(belts[i] != null && beltBoxes[i] != null, "belt " + (i == 0 ? "W" : "E"));
                for (int s = 0; s < 2; s++) Need(panels[i, s] != null && panelBoxes[i, s] != null, "bulkhead " + (i + 1) + (s == 0 ? " W" : " E") + " panel with its box");
                for (int k = 0; k < RoyalVaultRules.PlateCount; k++) Need(plates[i, k] != null && decks[i, k] != null, "plate " + (i == 0 ? "W" : "E") + (k + 1) + " with its deck");
            }
            Need(die != null && dieBox != null, "press die with its box");
            Need(disc != null && discBody != null, "door disc with its body");
            Need(shove != null && shoveShell != null, "door shove with its shell");
            Need(pad != null, "launch pad");
            Need(!float.IsNaN(neckTop) && !float.IsNaN(walkwayTop), "neck or walkway floor");
            var kinds = new[] { CueKind.Bulkhead, CueKind.Hammer, CueKind.Press, CueKind.Belt, CueKind.Drawer, CueKind.Plate, CueKind.Door };
            var counts = new[] { 4, 2, 1, 2, 2, 2 * RoyalVaultRules.PlateCount, 3 };
            for (int k = 0; k < kinds.Length; k++)
            {
                var kind = kinds[k];
                int n = cues.Count(c => c.Kind == kind);
                Check(n == counts[k], "P8 " + n + " " + kind.ToString().ToLowerInvariant() + " cues (" + counts[k] + " wanted)");
            }
            Check(slats[0].Count > 0 && slats[1].Count > 0, "P8 belt slats W " + slats[0].Count + ", E " + slats[1].Count + " (both belts need them)");
            Log("Found " + partsFound + " of " + partsWanted + " parts; " + cues.Count + " live cues, " + slats[0].Count + " + " + slats[1].Count + " belt slats, " + gold.Count +
                " static gold cue renderers; neck floor y " + F(neckTop) + ", walkway y " + F(walkwayTop) + "; clock " + F(ObstacleClock.Now) + " before freezing");
        }

        // ------------------------------------------------------------------ the frozen clock

        sealed class ProbeAt
        {
            public double Clock;
            public Action<double> Act;
        }

        static readonly List<ProbeAt> probes = new List<ProbeAt>();
        static int probeIndex, probeFrame;
        static bool probeWaiting;
        static double probeFixed;

        static void Add(double clock, Action<double> act) => probes.Add(new ProbeAt { Clock = clock, Act = act });

        // The first frozen time at or after T0 that lies `into` seconds into a cycle of `period` (RoyalVaultRules.Wrap's cycles).
        static double Next(double into, double period) => T0 + RoyalVaultRules.Wrap(into - T0, period);

        static void Plan()
        {
            probes.Clear();
            probeIndex = 0;
            probeWaiting = false;
            for (int k = 0; k < Samples; k++)
            {
                int n = k;
                Add(T0 + SampleStep * n, t => Sample(n, t));
            }
            // P2: each hammer just before, at and just after the bottom of its swing.
            double turn = 360 / RoyalVaultRules.HammerDegreesPerSecond;
            for (int i = 0; i < 2; i++)
            {
                int h = i;
                double bottom = Next(RoyalVaultRules.Wrap(-RoyalVaultRules.HammerPhase(h), turn), turn);
                Add(bottom - Nudge, t => HeadZ(h, 0));
                Add(bottom, t => HammerBottom(h, t));
                Add(bottom + Nudge, t => HeadZ(h, 1));
            }
            // P3: the die sitting on the neck, and in the middle of its time up.
            double press = RoyalVaultRules.PressPeriod, up = press - RoyalVaultRules.PressFall - RoyalVaultRules.PressWarnSeconds - RoyalVaultRules.PressSit - RoyalVaultRules.PressRise;
            Add(Next(RoyalVaultRules.PressSit / 2, press), PressDown);
            Add(Next(RoyalVaultRules.PressSit + RoyalVaultRules.PressRise + up / 2, press), PressUp);
            // P5: shut, passable (just after it opens, fully open, just before it closes), rolling both ways, the closing roll.
            double period = RoyalVaultRules.DoorPeriod, roll = RoyalVaultRules.DoorRoll, hold = RoyalVaultRules.DoorOpenHold;
            Add(Next(2 * roll + hold + RoyalVaultRules.DoorShutHold / 2, period), DoorShut);
            Add(Next(RoyalVaultRules.DoorPassableStart + Nudge, period), t => DoorPasses(t, true));
            Add(Next((RoyalVaultRules.DoorPassableStart + RoyalVaultRules.DoorPassableEnd) / 2, period), t => DoorPasses(t, false));
            Add(Next(RoyalVaultRules.DoorPassableEnd - Nudge, period), t => DoorPasses(t, true));
            var rolling = new[] { roll / 2, roll + hold + roll / 2 };
            for (int r = 0; r < 2; r++)
            {
                double mid = Next(rolling[r], period);
                int a = 2 * r, b = 2 * r + 1;
                Add(mid - Nudge, t => DiscPose(a, t));
                Add(mid + Nudge, t => DiscPose(b, t));
            }
            for (double u = roll + hold; u <= 2 * roll + hold + 1e-9; u += .1) Add(Next(u, period), ShoveLeads);
            // P8: the countdown at "2", seen from the terrace's follow camera; pictures of the door open and the plates lifted.
            Add(Next(RoyalVaultRules.DoorPassableStart - 1.5, period), CountdownInView);
            Add(Next(roll + hold / 2, period), t => Picture("door_open", new Vector3(0f, FloorTop + 2f, 172f), new Vector3(0f, FloorTop + 3f, 179f), 66f));
            Add(Next(RoyalVaultRules.PlateFlat + RoyalVaultRules.PlateRaise + RoyalVaultRules.PlateHold / 2, RoyalVaultRules.PlateCycle),
                t => Picture("plates_lifted", new Vector3(0f, FloorHigh + 4.5f, 128f), new Vector3(0f, FloorHigh, 147f), 70f));
            Log("Planned " + probes.Count + " frozen clock times from T0 = " + F(T0));
        }

        // Steps the probes: the clock frozen at the next one's time, then, once every part has stood there three physics steps
        // and two frames (cues and slats read the clock in Update), its reading. True once all are read.
        static bool Probing()
        {
            if (probeIndex >= probes.Count)
            {
                ObstacleClock.Use(null);
                return true;
            }
            var step = probes[probeIndex];
            if (!probeWaiting)
            {
                double at = step.Clock;
                ObstacleClock.Use(() => at);
                probeWaiting = true;
                probeFixed = Time.fixedTimeAsDouble;
                probeFrame = Time.frameCount;
                return false;
            }
            if (Time.frameCount < probeFrame + 2 || Time.fixedTimeAsDouble < probeFixed + 3 * Time.fixedDeltaTime - 1e-6) return false;
            Physics.SyncTransforms();
            step.Act(step.Clock);
            probeWaiting = false;
            probeIndex++;
            return false;
        }

        // ------------------------------------------------------------------ P1, P4, P6, P7, P8 at every sample

        static void Sample(int k, double t)
        {
            string at = Clock(t);
            Hammers(t, at);
            Hurdles(t, at);
            Bulkheads(t, at);
            Die(t, at);
            Drawers(t, at);
            Plates(t, at);
            Door(t, at);
            Belts(t, at);
            Cues(t, at);
            Slats(t, at);
            if (k == 0) Overviews();
        }

        static void Hammers(double t, string at)
        {
            double r = (RoyalVaultRules.HammerInner + RoyalVaultRules.HammerOuter) / 2;
            for (int i = 0; i < 2; i++)
            {
                if (hammers[i] == null || heads[i] == null) continue;
                double a = RoyalVaultRules.HammerAngle(i, t) * Math.PI / 180;
                Vector3 axle = L(hammers[i].position), head = L(Centre(heads[i]));
                var want = axle + new Vector3(0f, (float)(-r * Math.Cos(a)), (float)(-r * Math.Sin(a)));
                float off = Mathf.Max(Mathf.Abs(head.z - (float)RoyalVaultRules.HammerHeadZ(i, t)), (head - want).magnitude);
                Measure("P1 hammer " + (i + 1) + " head on HammerHeadZ and HammerAngle (0.05 m)", off, .05, at);
            }
        }

        static void Hurdles(double t, string at)
        {
            for (int h = 0; h < 2; h++)
            {
                if (hurdleBars[h] == null) continue;
                double a = RoyalVaultRules.HurdleAngle(h, t) * Math.PI / 180;
                var want = new Vector3((float)Math.Cos(a), 0f, (float)-Math.Sin(a));
                float off = Vector3.Angle(Flat(LDir(hurdleBars[h].right)), want);
                Measure("P1 hurdle " + (h + 1) + " bar along HurdleAngle (1°)", Mathf.Min(off, 180f - off), 1, at, "°");
            }
        }

        static void Bulkheads(double t, string at)
        {
            double cx = RoyalVaultRules.CorridorX, half = RoyalVaultRules.BulkheadHalfWidth, travel = RoyalVaultRules.BulkheadTravel;
            for (int b = 0; b < 2; b++)
            {
                for (int s = 0; s < 2; s++)
                {
                    if (panels[b, s] == null) continue;
                    int side = s == 0 ? -1 : 1;
                    // In its pocket it stands half its width beyond the corridor's side; it closes its half by BulkheadOut.
                    double x = cx + side * (half + travel / 2) - side * travel * RoyalVaultRules.BulkheadOut(b, side, t);
                    var p = L(panels[b, s].position);
                    float off = Mathf.Max(Mathf.Abs(p.x - (float)x), Mathf.Max(Mathf.Abs(p.z - (float)RoyalVaultRules.BulkheadZ[b]), Mathf.Abs(p.y - panelY[b, s])));
                    Measure("P1 bulkhead " + (b + 1) + (s == 0 ? " W" : " E") + " panel on BulkheadOut (0.02 m)", off, .02, at);
                }
                if (panelBoxes[b, 0] == null || panelBoxes[b, 1] == null) continue;
                float west = Mathf.Max(SectionBox(panelBoxes[b, 0]).max.x, (float)(cx - half)), east = Mathf.Min(SectionBox(panelBoxes[b, 1]).min.x, (float)(cx + half));
                Measure("P7 bulkhead " + (b + 1) + " leaves at least 3.9 m", east - west, 3.9, at, " m", true);
            }
        }

        static void Die(double t, string at)
        {
            if (dieBox == null || float.IsNaN(neckTop)) return;
            var box = SectionBox(dieBox);
            Measure("P1 press die bottom at PressDieBottom over the neck (0.02 m)", Mathf.Abs(box.min.y - (neckTop + (float)RoyalVaultRules.PressDieBottom(t))), .02, at);
            Measure("P1 press die over PressX, PressZ (0.02 m)", Mathf.Max(Mathf.Abs(box.center.x - (float)RoyalVaultRules.PressX), Mathf.Abs(box.center.z - (float)RoyalVaultRules.PressZ)), .02, at);
        }

        static void Drawers(double t, string at)
        {
            for (int k = 0; k < 2; k++)
            {
                if (drawerBoxes[k] == null) continue;
                var box = SectionBox(drawerBoxes[k]);
                float off = Mathf.Max(Mathf.Abs(box.min.x - (float)RoyalVaultRules.DrawerFaceX(k, t)), Mathf.Abs(box.center.z - (float)RoyalVaultRules.DrawerZ[k]));
                Measure("P1 drawer " + (k + 1) + " face at DrawerFaceX (0.02 m)", off, .02, at);
            }
        }

        static void Plates(double t, string at)
        {
            bool any = false;
            var flat = new[] { true, true };
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < RoyalVaultRules.PlateCount; i++)
                {
                    var deck = decks[row, i];
                    if (deck == null || plates[row, i] == null) continue;
                    any = true;
                    string which = (row == 0 ? "W" : "E") + (i + 1);
                    // The deck's local +x points inward on both rows (the east row is turned 180°): its rise is the lift.
                    Vector3 inner = LDir(deck.transform.right), up = LDir(deck.transform.up);
                    float angle = Mathf.Asin(Mathf.Clamp(inner.y, -1f, 1f)) * Mathf.Rad2Deg;
                    Measure("P1 plate angles at PlateAngle, each row its own (0.5°)", Mathf.Abs(angle - (float)RoyalVaultRules.PlateAngle(row, t)), .5, which + " at " + at, "°");
                    if (Mathf.Abs(angle) <= .5f) continue;
                    flat[row] = false;
                    float outward = Mathf.Sign(L(plates[row, i].position).x);
                    Expect("P4 a lifted deck tilts outward (its up away from the middle)", up.x * outward > 0f, which + " at " + at + ": up " + V(up));
                }
            if (any) Expect("P4 one row of plates flat at every sample", flat[0] || flat[1], at);
        }

        static void Door(double t, string at)
        {
            if (disc == null) return;
            var c = L(disc.position);
            var want = new Vector3((float)(RoyalVaultRules.DoorTravel * RoyalVaultRules.DoorOpen(t)), FloorTop + (float)RoyalVaultRules.DoorCentreAbove,
                                   (float)((RoyalVaultRules.DoorZ0 + RoyalVaultRules.DoorZ1) / 2));
            Measure("P1 door disc centre on DoorOpen (0.02 m)", (c - want).magnitude, .02, at);
            var turn = LRot(disc.rotation);
            // Rolling: as many radians as metres travelled over its radius, the negative way for +x (RoyalVaultBuilder.Door).
            float roll = Vector3.SignedAngle(Vector3.up, turn * Vector3.up, Vector3.forward);
            float wantRoll = (float)(-RoyalVaultRules.DoorOpen(t) * RoyalVaultRules.DoorTravel / RoyalVaultRules.DoorRadius * 180 / Math.PI);
            Measure("P1 door disc rolled as far as it travelled (0.5°)", Mathf.Abs(Mathf.DeltaAngle(roll, wantRoll)), .5, at, "°");
            Measure("P1 door disc's axle along z (0.5°)", Vector3.Angle(turn * Vector3.forward, Vector3.forward), .5, at, "°");
            if (shove == null) return;
            Measure("P1 door shove on the disc's centre (0.02 m)", (L(shove.position) - c).magnitude, .02, at);
            Measure("P1 door shove never turns (0.5°)", Quaternion.Angle(LRot(shove.rotation), Quaternion.identity), .5, at, "°");
        }

        static void Belts(double t, string at)
        {
            for (int b = 0; b < 2; b++)
            {
                if (belts[b] == null || beltBoxes[b] == null) continue;
                var bounds = beltBoxes[b].bounds;
                var v = LDir(belts[b].PointVelocity(bounds.center + frame.up * (bounds.extents.y + .1f)));
                var want = new Vector3(0f, 0f, (float)RoyalVaultRules.BeltSpeed(b, t));
                Measure("P6 belt " + (b == 0 ? "W" : "E") + " PointVelocity = BeltSpeed along +z (0.01 m/s)", (v - want).magnitude, .01, at, " m/s");
            }
        }

        static readonly Dictionary<string, int> cueStates = new Dictionary<string, int>();

        static RoyalVaultRules.Cue Want(CueRef c, double t, out string words)
        {
            words = null;
            switch (c.Kind)
            {
                case CueKind.Bulkhead: return RoyalVaultRules.BulkheadCue(c.Index, c.Side, t);
                case CueKind.Hammer: return RoyalVaultRules.HammerCue(c.Index, t);
                case CueKind.Press: return RoyalVaultRules.PressCue(t);
                case CueKind.Belt: return RoyalVaultRules.BeltCue(c.Index, t);
                case CueKind.Drawer: return RoyalVaultRules.DrawerCue(c.Index, t);
                case CueKind.Plate: return RoyalVaultRules.PlateCue(c.Index, t);
                default:
                    words = RoyalVaultRules.DoorSign(t, out var look);
                    return look;
            }
        }

        // The cue vocabulary (spec Conventions, §2.12): Off hidden, Go green, Warn red only while Blink, Stop red; the
        // countdown's words with their colour (ink, green, red). The gold edges are always lit.
        static void Cues(double t, string at)
        {
            bool lit = RoyalVaultRules.Blink(t);
            string red = RoyalVaultLook.CueRed.name, green = RoyalVaultLook.CueGreen.name, golden = RoyalVaultLook.CueGold.name;
            foreach (var c in cues)
            {
                var want = Want(c, t, out string words);
                string key = c.Kind + " " + want;
                cueStates.TryGetValue(key, out int seen);
                cueStates[key] = seen + 1;
                string name = "P8 " + c.Kind.ToString().ToLowerInvariant() + " cues show their rule's state";
                if (c.Text != null)
                {
                    var r = c.Renderers.Length > 0 ? c.Renderers[0] : null;
                    bool shown = r == null || Shown(r), ok = c.Text.text == words;
                    if (ok && words.Length > 0)
                    {
                        var colour = c.Text.color;
                        switch (want)
                        {
                            case RoyalVaultRules.Cue.Go: ok = shown && Near(colour, RoyalVaultLook.GreenColor); break;
                            case RoyalVaultRules.Cue.Stop: ok = shown && Near(colour, RoyalVaultLook.RedColor); break;
                            case RoyalVaultRules.Cue.Warn: ok = lit ? shown && Near(colour, RoyalVaultLook.RedColor) : !shown || Near(colour, RoyalVaultLook.RedColor); break;
                            default: ok = shown && Near(colour, RoyalVaultLook.InkColor); break;
                        }
                    }
                    Expect(name, ok, c.Name + " at " + at + ": wanted \"" + words + "\" " + want + ", shows \"" + c.Text.text + "\" in " + Hex(c.Text.color) + (shown ? "" : " (hidden)"));
                    continue;
                }
                bool all = c.Renderers.Length > 0 && c.Renderers.All(Shown), none = c.Renderers.All(x => !Shown(x));
                string material = all ? MaterialName(c.Renderers[0]) : "";
                bool good;
                switch (want)
                {
                    case RoyalVaultRules.Cue.Off: good = none; break;
                    case RoyalVaultRules.Cue.Go: good = all && material.StartsWith(green); break;
                    case RoyalVaultRules.Cue.Stop: good = all && material.StartsWith(red); break;
                    default: good = lit ? all && material.StartsWith(red) : none; break;
                }
                Expect(name, good, c.Name + " at " + at + ": wanted " + want + (want == RoyalVaultRules.Cue.Warn ? (lit ? " (lit)" : " (dark)") : "") +
                       ", shows " + (all ? material : none ? "nothing" : "some of it"));
            }
            foreach (var r in gold) Expect("P8 the static gold cues always lit in gold", Shown(r) && MaterialName(r).StartsWith(golden), Name(r.transform) + " at " + at);
        }

        static readonly List<float>[] slatZ = { new List<float>(), new List<float>() };
        static double slatClock = double.NaN;

        // The belt slats: ink, shown, and moved between two samples by the belt's own BeltTravel (modulo the belt's length).
        static void Slats(double t, string at)
        {
            string ink = RoyalVaultLook.Ink.name;
            double length = RoyalVaultRules.BeltZ1 - RoyalVaultRules.BeltZ0;
            for (int b = 0; b < 2; b++)
                for (int j = 0; j < slats[b].Count; j++)
                {
                    var slat = slats[b][j];
                    var r = slat.GetComponent<Renderer>();
                    Expect("P8 belt slats shown in ink", r != null && Shown(r) && MaterialName(r).StartsWith(ink), Name(slat) + " at " + at);
                    float z = L(slat.position).z;
                    if (!double.IsNaN(slatClock) && j < slatZ[b].Count)
                    {
                        double moved = z - slatZ[b][j], want = RoyalVaultRules.BeltTravel(b, t) - RoyalVaultRules.BeltTravel(b, slatClock);
                        double off = RoyalVaultRules.Wrap(moved - want + length / 2, length) - length / 2;
                        Measure("P8 belt slats move by BeltTravel (0.03 m)", Math.Abs(off), .03, Name(slat) + " at " + at);
                    }
                    if (j < slatZ[b].Count) slatZ[b][j] = z;
                    else slatZ[b].Add(z);
                }
            slatClock = t;
        }

        // ------------------------------------------------------------------ P2, P3, P5 at their own times

        static readonly float[,] headZ = new float[2, 2];
        static readonly bool[,] headRead = new bool[2, 2];

        static void HeadZ(int i, int which)
        {
            if (heads[i] == null) return;
            headZ[i, which] = L(Centre(heads[i])).z;
            headRead[i, which] = true;
        }

        static void HammerBottom(int i, double t)
        {
            if (heads[i] == null || float.IsNaN(walkwayTop)) return;
            string at = "hammer " + (i + 1) + " at " + Clock(t);
            var head = SectionBox(heads[i]);
            float gap = head.min.y - walkwayTop;
            Measure("P2 hammer heads pass HammerAxleAbove - HammerOuter over the walkway (0.03 m)", Mathf.Abs(gap - (float)(RoyalVaultRules.HammerAxleAbove - RoyalVaultRules.HammerOuter)), .03, at);
            float z = L(hammers[i].position).z, y = walkwayTop + .5f;
            int points = 0, hit = 0;
            for (float x = walkway.min.x + .1f; x <= walkway.max.x - .1f + 1e-3f; x += .4f)
            {
                points++;
                if (Overlaps(new Vector3(x, y, z), new Vector3(.05f, .05f, .05f), heads[i])) hit++;
            }
            Check(points > 0 && hit == points, "P2 " + at + ": its head meets " + hit + " of " + points + " points across the walkway at 0.5 m");
            Log("P2 " + at + " (angle " + F(RoyalVaultRules.HammerAngle(i, t)) + "°): the head meets " + hit + " of " + points + " points across the walkway's " +
                F(walkway.size.x) + " m at 0.5 m, its lowest point " + F(gap) + " m over it");
            if (i == 0) Picture("hammers", new Vector3(7f, FloorLow + 5f, 57f), new Vector3(0f, FloorLow + 1f, 72f), 70f);
        }

        static void PressDown(double t)
        {
            if (dieBox == null || float.IsNaN(neckTop)) return;
            string at = Clock(t);
            Check(RoyalVaultRules.PressLowered(t) >= .999, "P3 the die-down probe missed the landing (" + at + ")");
            int points = 0, hit = 0;
            for (float x = neck.min.x + .1f; x <= neck.max.x - .1f + 1e-3f; x += .5f)
            {
                points++;
                if (Overlaps(new Vector3(x, neckTop + .5f, (float)RoyalVaultRules.PressZ), new Vector3(.05f, .05f, .05f), dieBox)) hit++;
            }
            Check(points > 0 && hit == points, "P3 die down (" + at + "): " + hit + " of " + points + " points across the neck at 0.5 m meet it");
            Log("P3 die down (" + at + "): " + hit + " of " + points + " points across the neck's " + F(neck.size.x) + " m meet it at 0.5 m");
            Picture("press_down", new Vector3((float)RoyalVaultRules.PressX, FloorLow + 2.5f, 100f), new Vector3((float)RoyalVaultRules.PressX, FloorLow + 1.5f, 111f), 70f);
        }

        static void PressUp(double t)
        {
            if (dieBox == null || float.IsNaN(neckTop)) return;
            string at = Clock(t);
            Check(RoyalVaultRules.PressLowered(t) <= .001, "P3 the die-up probe missed the top (" + at + ")");
            float tall = (float)RoyalVaultRules.PressHeadroom;
            var under = Solids(new Vector3((float)RoyalVaultRules.PressX, neckTop + .05f + tall / 2, (float)RoyalVaultRules.PressZ),
                               new Vector3((float)RoyalVaultRules.PressDieAcross / 2 - .05f, tall / 2, (float)RoyalVaultRules.PressDieAlong / 2));
            Check(under.Count == 0, "P3 die up (" + at + "), something in the " + F(tall) + " m under it: " + string.Join(", ", under));
            Log("P3 die up (" + at + "): the " + F(tall) + " m under it is clear, its bottom " + F(SectionBox(dieBox).min.y - neckTop) + " m over the neck");
        }

        static void DoorShut(double t)
        {
            if (discBody == null) return;
            string at = Clock(t);
            Check(RoyalVaultRules.DoorOpen(t) <= 0, "P5 the shut probe missed the shut time (" + at + ")");
            float z = (float)((RoyalVaultRules.DoorZ0 + RoyalVaultRules.DoorZ1) / 2), depth = (float)(RoyalVaultRules.DoorZ1 - RoyalVaultRules.DoorZ0) / 2 + .3f;
            int points = 0, hit = 0;
            foreach (float x in new[] { -2.4f, -1.2f, 0f, 1.2f, 2.4f })
                foreach (float y in new[] { .3f, 1f, 2f, 3f })
                {
                    points++;
                    if (Overlaps(new Vector3(x, FloorTop + y, z), new Vector3(.05f, .05f, depth), discBody)) hit++;
                }
            Check(hit == points, "P5 shut (" + at + "): the disc misses " + (points - hit) + " of " + points + " points in the doorway");
            Log("P5 shut (" + at + "): the disc fills " + hit + " of " + points + " points in the doorway");
            Picture("door_shut", new Vector3(0f, FloorTop + 2f, 172f), new Vector3(0f, FloorTop + 3f, 179f), 66f);
        }

        // Passable: a 2.5 m way through the doorway is clear. At its first and last moments that is the way at 1 m over the
        // floor DoorClearWidth measures, from the octagon's west side to the rolling disc's rim (a 2.38 m box, 0.06 m off
        // either end); fully open, a 2.5 m box 1.5 m tall in the doorway's middle.
        static void DoorPasses(double t, bool tight)
        {
            string at = Clock(t);
            Check(RoyalVaultRules.DoorPassableFor(t) > 0, "P5 the passable probe missed the passable time (" + at + ")");
            float z = (float)((RoyalVaultRules.DoorZ0 + RoyalVaultRules.DoorZ1) / 2), depth = (float)(RoyalVaultRules.DoorZ1 - RoyalVaultRules.DoorZ0) / 2 + .3f;
            float pass = (float)RoyalVaultRules.DoorPassWidth;
            Vector3 centre, half;
            if (tight)
            {
                double y = 1 - RoyalVaultRules.DoorCentreAbove, tan = Math.Tan(Math.PI / 8), inradius = RoyalVaultRules.DoorWayInradius;
                double side = Math.Abs(y) <= inradius * tan ? inradius : inradius * (1 + tan) - Math.Abs(y);
                centre = new Vector3((float)-side + pass / 2, FloorTop + 1f, z);
                half = new Vector3(pass / 2 - .06f, .02f, depth);
            }
            else
            {
                centre = new Vector3(0f, FloorTop + 1.05f, z);
                half = new Vector3(pass / 2, .75f, depth);
            }
            var inWay = Solids(centre, half);
            Check(inWay.Count == 0, "P5 passable (" + at + "): a " + F(2 * half.x) + " m way through the doorway meets " + string.Join(", ", inWay));
            Log("P5 passable (" + at + ", DoorClearWidth " + F(RoyalVaultRules.DoorClearWidth(t)) + " m): a " + F(2 * half.x) + " x " + F(2 * half.y) + " m box at " + V(centre) +
                (inWay.Count == 0 ? " passes" : " is blocked"));
        }

        static readonly Vector3[] discPos = new Vector3[4];
        static readonly Quaternion[] discRot = new Quaternion[4];
        static readonly double[] discAt = new double[4];
        static readonly bool[] discRead = new bool[4];

        static void DiscPose(int slot, double t)
        {
            if (disc == null) return;
            discPos[slot] = L(disc.position);
            discRot[slot] = LRot(disc.rotation);
            discAt[slot] = t;
            discRead[slot] = true;
        }

        static void ShoveLeads(double t)
        {
            if (shoveShell == null || disc == null) return;
            var c = L(disc.position);
            float dy = c.y - (FloorTop + .3f), r = (float)RoyalVaultRules.DoorRadius;
            float rim = c.x - Mathf.Sqrt(Mathf.Max(0f, r * r - dy * dy));
            Measure("P5 closing, the shove's leading face never behind the disc's rim at 0.3 m (0.01 m)", SectionBox(shoveShell).min.x - rim, .01, Clock(t));
        }

        // P8: from the follow camera (6 m behind and 15° above a pawn on the terrace, 60°) at least one countdown is in view.
        static void CountdownInView(double t)
        {
            var texts = cues.Where(c => c.Kind == CueKind.Door && c.Text != null).Select(c => c.Text.transform).ToArray();
            var focus = new Vector3(0f, FloorTop + .6f, 175f);
            var eye = focus + Quaternion.Euler(15f, 0f, 0f) * new Vector3(0f, 0f, -6f);
            foreach (float aspect in new[] { 16f / 9f, 4f / 3f })
            {
                captureCamera.orthographic = false;
                captureCamera.fieldOfView = 60f;
                captureCamera.aspect = aspect;
                captureCamera.transform.SetPositionAndRotation(W(eye), Quaternion.LookRotation(W(focus) - W(eye), frame.up));
                var planes = GeometryUtility.CalculateFrustumPlanes(captureCamera);
                var inView = texts.Where(x => GeometryUtility.TestPlanesAABB(planes, new Bounds(x.position, Vector3.one * .3f))).Select(x => x.name).ToArray();
                Check(inView.Length > 0, "P8 no door countdown in view from the terrace's follow camera (aspect " + F(aspect) + ")");
                Log("P8 the follow camera 6 m behind and 15° above a pawn at " + V(focus) + ", 60°, aspect " + F(aspect) + " (" + Clock(t) + ", the sign reads \"" +
                    RoyalVaultRules.DoorSign(t, out _) + "\"): " + (inView.Length > 0 ? string.Join(", ", inView) : "no countdown") + " in view");
            }
            Picture("countdown_view", eye, focus, 60f);
        }

        // ------------------------------------------------------------------ after the probes

        static void Evaluate()
        {
            for (int i = 0; i < 2; i++)
            {
                if (!headRead[i, 0] || !headRead[i, 1]) continue;
                float v = (headZ[i, 1] - headZ[i, 0]) / (float)(2 * Nudge);
                Check(v < 0f, "P2 hammer " + (i + 1) + "'s head moves " + F(v) + " m/s along z at the bottom (-z wanted)");
                Log("P2 hammer " + (i + 1) + " at the bottom moves " + F(v) + " m/s along z");
            }
            for (int r = 0; r < 2; r++)
            {
                int a = 2 * r, b = a + 1;
                if (!discRead[a] || !discRead[b]) continue;
                float dt = (float)(discAt[b] - discAt[a]);
                // The point of the rim at the bottom halfway between the two readings, followed through both.
                var mid = Quaternion.Slerp(discRot[a], discRot[b], .5f);
                var bottom = Quaternion.Inverse(mid) * (Vector3.down * (float)RoyalVaultRules.DoorRadius);
                Vector3 pa = discPos[a] + discRot[a] * bottom, pb = discPos[b] + discRot[b] * bottom;
                float slip = (pb.x - pa.x) / dt, speed = (discPos[b].x - discPos[a].x) / dt;
                string way = r == 0 ? "rolling open" : "rolling shut";
                Measure("P5 the disc's lowest point stays put while it rolls (0.1 m/s)", Mathf.Abs(slip), .1, way + " at " + Clock((discAt[a] + discAt[b]) / 2), " m/s");
                Log("P5 " + way + ": the centre moves " + F(speed) + " m/s along x, the lowest point " + F(slip) + " m/s");
            }
            Log("P8 cue states seen: " + string.Join(", ", cueStates.OrderBy(k => k.Key).Select(k => k.Key + " " + k.Value)));
        }

        // ------------------------------------------------------------------ P10 the launch pad

        sealed class Body
        {
            public Rigidbody Rigidbody;
            public Vector3 Start, Launch, Touch, LastPosition, LastVelocity;
            public bool Launched, Down, Fell;
            public float DownAt, Peak = float.MinValue;
            public string On = "";
        }

        static readonly List<Body> bodies = new List<Body>();
        static float throwsAt;
        // A 0.6 m wide capsule, 1.2 m tall, standing with its axis 0.28 m outside the trigger's footprint, so 2 cm of it overlap
        // the trigger (spec §5.4 P10: 0.3 m outside every edge's middle and every corner, and the middle: 9 places).
        const float BodyRadius = .3f, BodyHeight = 1.2f, BodyOutside = .28f;

        static void StartThrows()
        {
            ObstacleClock.Use(null);
            bodies.Clear();
            if (pad == null) { Check(false, "P10 no launch pad"); return; }
            var box = pad.GetComponent<BoxCollider>();
            if (box == null) { Check(false, "P10 the launch pad has no box"); return; }
            var centre = L(pad.transform.TransformPoint(box.center));
            var half = Vector3.Scale(box.size, YardKit.Abs(pad.transform.lossyScale)) * .5f;
            var places = new List<Vector2> { Vector2.zero };
            for (int sx = -1; sx <= 1; sx++)
                for (int sz = -1; sz <= 1; sz++)
                {
                    if (sx == 0 && sz == 0) continue;
                    float d = sx != 0 && sz != 0 ? BodyOutside / Mathf.Sqrt(2f) : BodyOutside;
                    places.Add(new Vector2(sx * (half.x + d), sz * (half.z + d)));
                }
            foreach (var place in places)
            {
                var start = new Vector3(centre.x + place.x, (float)RoyalVaultRules.PadFloor, centre.z + place.y);
                var go = new GameObject("P10 body " + (bodies.Count + 1));
                go.transform.SetPositionAndRotation(W(start + Vector3.up * (BodyHeight * .5f + .005f)), frame.rotation);
                var shape = go.AddComponent<CapsuleCollider>();
                shape.radius = BodyRadius;
                shape.height = BodyHeight;
                shape.direction = 1;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 60f;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                bodies.Add(new Body { Rigidbody = rb, Start = start });
            }
            Physics.SyncTransforms();
            pad.Thrown += OnThrown;
            throwsAt = Time.time;
            Log("P10: " + bodies.Count + " loose capsules (" + F(2 * BodyRadius) + " m wide, " + F(BodyHeight) + " m tall) on the pad's middle and " + F(BodyOutside) +
                " m outside its edges' middles and corners, the clock running again");
        }

        static void OnThrown(Component who, Vector3 velocity)
        {
            foreach (var b in bodies)
                if (b.Rigidbody == who && !b.Launched)
                {
                    b.Launched = true;
                    b.Launch = LDir(velocity);
                }
        }

        // Where a body coming down from its last airborne reading meets the shelf's top, flying on under plain gravity.
        static Vector3 Touchdown(Vector3 p, Vector3 v)
        {
            float g = -Physics.gravity.y, bottom = p.y - BodyHeight * .5f, drop = Mathf.Max(0f, bottom - FloorHigh);
            float s = (v.y + Mathf.Sqrt(v.y * v.y + 2f * g * drop)) / g;
            return new Vector3(p.x + v.x * s, FloorHigh, p.z + v.z * s);
        }

        static string Under(Vector3 local)
        {
            var hits = Physics.RaycastAll(W(local + Vector3.up * .3f), -frame.up, 1f, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
                if (h.collider.attachedRigidbody == null || h.collider.attachedRigidbody.isKinematic) return h.collider.name;
            return "nothing";
        }

        // Follows the bodies until each has been down 0.6 s (or 6 s pass), then judges them. True while still following.
        static bool Throwing()
        {
            if (bodies.Count == 0) return false;
            bool settled = true;
            foreach (var b in bodies)
            {
                if (b.Rigidbody == null) continue;
                var p = L(b.Rigidbody.position);
                var v = LDir(b.Rigidbody.linearVelocity);
                float bottom = p.y - BodyHeight * .5f;
                if (b.Launched && !b.Down)
                {
                    b.Peak = Mathf.Max(b.Peak, bottom);
                    if (b.Peak > FloorHigh + .5f && bottom <= FloorHigh + .12f)
                    {
                        b.Down = true;
                        b.DownAt = Time.time;
                        b.Touch = Touchdown(b.LastPosition, b.LastVelocity);
                        b.On = Under(b.Touch);
                    }
                    else
                    {
                        b.LastPosition = p;
                        b.LastVelocity = v;
                    }
                }
                else if (b.Down && Time.time - b.DownAt < .6f && bottom < FloorHigh - .4f) b.Fell = true;
                if (!b.Down || Time.time - b.DownAt < .6f) settled = false;
            }
            if (!settled && Time.time - throwsAt < 6f) return true;

            RoyalVaultRules.PadThrow(out double up, out double vx, out double vz, out _);
            var thrown = new Vector3((float)vx, (float)up, (float)vz);
            float x0 = (float)(RoyalVaultRules.ShelfX0 + RoyalVaultRules.BodyHalf), x1 = (float)(RoyalVaultRules.ShelfX1 - RoyalVaultRules.BodyHalf);
            float z0 = (float)RoyalVaultRules.ShelfZ0 + .5f, z1 = (float)(RoyalVaultRules.PadLandZ + RoyalVaultRules.PadSize / 2 + RoyalVaultRules.PadCatch) + .1f;
            for (int i = 0; i < bodies.Count; i++)
            {
                var b = bodies[i];
                string who = "body " + (i + 1) + " from " + V(b.Start);
                Check(b.Launched, "P10 " + who + " was not thrown");
                if (!b.Launched) continue;
                Measure("P10 the pad throws at PadThrow (0.05 m/s)", (b.Launch - thrown).magnitude, .05, who, " m/s");
                Check(b.Down, "P10 " + who + " never came down on y " + F(FloorHigh) + " (highest " + F(b.Peak) + ")");
                if (!b.Down) continue;
                bool inside = b.Touch.x >= x0 && b.Touch.x <= x1 && b.Touch.z >= z0 && b.Touch.z <= z1;
                Expect("P10 thrown bodies land inside the landing box (x " + F(x0) + ".." + F(x1) + ", z " + F(z0) + ".." + F(z1) + ")", inside, who + " lands at " + V(b.Touch));
                Expect("P10 thrown bodies land on the shelf (RV shelf S)", b.On == "RV shelf S", who + " lands on " + b.On);
                Expect("P10 thrown bodies stay on the shelf", !b.Fell, who + " fell off within 0.6 s");
                Log("P10 " + who + ": thrown " + V(b.Launch) + " m/s, down at " + V(b.Touch) + " on " + b.On + (b.Fell ? ", then fell" : ""));
            }
            pad.Thrown -= OnThrown;
            foreach (var b in bodies)
                if (b.Rigidbody != null) Object.Destroy(b.Rigidbody.gameObject);
            return false;
        }

        // ------------------------------------------------------------------ pictures

        static void Overviews()
        {
            if (NoCapture) return;
            pictures.Enqueue(() => TopView("overview_top"));
            Picture("from_entrance", new Vector3(0f, 3.5f, 2f), new Vector3(0f, 3f, 40f), 70f);
            Picture("fork1", new Vector3(-4f, 9f, 4f), new Vector3(-8f, 1f, 32f), 70f);
            Picture("towers", new Vector3(9f, 6f, 14f), new Vector3(0f, 2.5f, 34f), 66f);
            Picture("corridor", new Vector3((float)RoyalVaultRules.CorridorX, 3f, 4f), new Vector3((float)RoyalVaultRules.CorridorX, 1f, 34f), 70f);
            Picture("fork2", new Vector3(-11f, FloorLow + 7f, 84f), new Vector3(0f, FloorLow + 1f, 100f), 70f);
            Picture("shelf", new Vector3(11f, FloorHigh + 5f, 89f), new Vector3(5f, FloorHigh + .5f, 101f), 66f);
            Picture("terrace", new Vector3(0f, FloorTop + 4f, 163f), new Vector3(0f, FloorTop + 2f, 179f), 70f);
            Picture("room_arch", new Vector3(0f, FloorTop + 2.5f, 181f), new Vector3(0f, FloorTop + 2f, 195f), 70f);
        }

        static void Picture(string name, Vector3 eye, Vector3 target, float fieldOfView)
        {
            if (NoCapture) return;
            pictures.Enqueue(() => CaptureView(name, eye, target, Vector3.up, fieldOfView, false, 1536, 1024));
        }

        // The whole vault from straight above, the way on up the picture, with its roofs (and anything else over 17 m) not drawn.
        static void TopView(string name)
        {
            var hidden = new List<Renderer>();
            foreach (var r in content.GetComponentsInChildren<Renderer>(false))
                if (r.enabled && SectionBoxOf(r).min.y > 17f) { r.enabled = false; hidden.Add(r); }
            try
            {
                CaptureView(name, new Vector3(-6f, 80f, 98f), new Vector3(-6f, 0f, 98f), Vector3.forward, 102f, true, 1024, 1536);
            }
            finally
            {
                foreach (var r in hidden) r.enabled = true;
            }
        }

        static Bounds SectionBoxOf(Renderer r)
        {
            var b = r.bounds;
            var result = new Bounds(L(b.min), Vector3.zero);
            result.Encapsulate(L(b.max));
            return result;
        }

        // One picture from `eye` toward `target` (the section's frame) into the report's folder; orthographic with
        // `fieldOfView` as its half height when `flat`.
        static void CaptureView(string name, Vector3 eye, Vector3 target, Vector3 up, float fieldOfView, bool flat, int width, int height)
        {
            if (frame == null || captureCamera == null) return;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Log("NOT RUN: capture " + name + " (null graphics device)"); return; }
            captureCamera.transform.position = W(eye);
            captureCamera.transform.rotation = Quaternion.LookRotation(W(target) - W(eye), frame.TransformDirection(up));
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
                Log("Captured " + path + " (" + Clock(ObstacleClock.Now) + ")");
            }
            finally
            {
                RenderTexture.active = before;
                if (picture != null) Object.DestroyImmediate(picture);
                texture.Release(); Object.DestroyImmediate(texture);
            }
        }

        // ------------------------------------------------------------------ the end

        static void Finish()
        {
            foreach (var e in errors) Log("ERROR logged in Play: " + e.Split('\n')[0]);
            Check(errors.Count == 0, "P9 " + errors.Count + " error(s) logged in Play");
            int bad = End("Play");
            StopPlay(bad == 0 ? 0 : 1);
        }

        static void StopPlay(int code)
        {
            stopping = true;
            ObstacleClock.Use(null);
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick;
            if (pad != null) pad.Thrown -= OnThrown;
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
