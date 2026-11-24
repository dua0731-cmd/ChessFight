using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush.Editor
{
    // R809 (the 11-23 build test, Docs/Network/BUILD_TEST_2026-11-23.md: the carriage yard drew at 52 fps where the grand
    // stair hall drew at 77, and a round with the palace laundry logs "Reduced additional punctual light shadows
    // resolution by 4 to make 96 shadow maps fit"). Lives in Tools/SectionLightProbe, outside Assets:
    // Tools/SectionLightProbe/Run-SectionLightProbe.ps1 puts it into a COPY of the project (its own editor assembly) and
    // runs it in batch mode with a graphics device, so the open Editor is left alone:
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.SectionLightProbe.RunPlayBatch
    // In Play, the course prefab test builds the carriage yard, the grand stair hall, the war room and the audience hall
    // one at a time, and the mini-game test the moat drawbridge and the palace laundry, each with its own runtime dressing
    // (lights, water, models), as a round has them. For each: every light there is (kind, shadows), the shadow-casting
    // punctual ones by name (URP gives a shadowed point light six maps of the atlas, a spot one), and every enabled
    // renderer (triangles, shadow casters, materials, transparent ones, particle systems). Then at the entry and at
    // every beat a camera 4 m behind and 2.5 m over the floor looking on: what its frustum holds (renderers, triangles,
    // casters, lights, shadowed lights) and the camera drawn into 1920x1080 (CPU until Render returns; total with the GPU
    // finished) - the Editor's batch mode on one thread, not a player's frame rate: for comparing sections, not for fps.
    // Report: <copy>/Logs/SectionLightProbe/report.txt, lights.csv, views.csv.
    [InitializeOnLoad]
    public static class SectionLightProbe
    {
        const string K = "SectionLightProbe.";
        static readonly string[] CourseNames = { "RoyalCarriageYard", "GrandStairHall", "WarRoom", "AudienceHall" };
        static readonly string[] RoomNames = { "MoatDrawbridge", "PalaceLaundry" };

        static string Out => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "SectionLightProbe");

        static SectionLightProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(K + "Exit", false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        public static void RunPlayBatch()
        {
            Directory.CreateDirectory(Out);
            File.WriteAllText(Path.Combine(Out, "report.txt"), "Section light probe (R809) " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n");
            File.WriteAllText(Path.Combine(Out, "lights.csv"), "section,path,type,shadows,range,intensity,render_mode,maps\n");
            File.WriteAllText(Path.Combine(Out, "views.csv"), "section,view,renderers,triangles,casters,lights,shadowed_lights,shadow_maps,cpu_ms,total_ms\n");
            File.WriteAllText(Path.Combine(Out, "meshes.csv"), "section,asset,mesh,readable,instances,triangles,largest_m,first_renderer\n");
            Log("device: " + SystemInfo.graphicsDeviceName + " | " + SystemInfo.graphicsDeviceType + " | quality " + QualitySettings.GetQualityLevel() + " " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
            SessionState.SetInt(K + "Job", 0);
            SessionState.SetInt(K + "Failures", 0);
            OpenJob();
        }

        // -probeOnly <name part>: one course only (the mini-game job is skipped then).
        static string Only
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args, "-probeOnly");
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : "";
            }
        }

        static string[] Jobs => Only != "" ? new[] { "courses" } : new[] { "courses", "minigames" };

        static void OpenJob()
        {
            int index = SessionState.GetInt(K + "Job", 0);
            if (index >= Jobs.Length) { Finish(); return; }
            var path = Jobs[index] == "courses" ? CoursePrefabMenu.TestScene : MiniGamePrefabMenu.TestScene;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(K + "Active", true);
            SessionState.SetBool(K + "Exit", false);
            requested = idleAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= BeginWhenIdle;
            EditorApplication.update += BeginWhenIdle;
        }

        static double requested, idleAt;

        static void BeginWhenIdle()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - requested > 300) { EditorApplication.update -= BeginWhenIdle; Fail("the Editor did not settle"); Finish(); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { idleAt = now; return; }
            if (now - idleAt < 2) return;
            EditorApplication.update -= BeginWhenIdle;
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(K + "Active", false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                int index = SessionState.GetInt(K + "Job", 0);
                Application.logMessageReceived -= OnLog;
                Application.logMessageReceived += OnLog;
                stack.Clear();
                stack.Push(index < Jobs.Length && Jobs[index] == "courses" ? CoursesJob() : RoomsJob());
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetInt(K + "Job", SessionState.GetInt(K + "Job", 0) + 1);
                if (SessionState.GetBool(K + "Exit", false)) { CompleteExit(); return; }
                EditorApplication.delayCall += OpenJob;
            }
        }

        // Shadow-atlas and other warnings while a section stands, with the section they came under.
        static string current = "-";
        static void OnLog(string message, string trace, LogType type)
        {
            if (!message.StartsWith("[SectionLightProbe]") && (message.Contains("shadow maps") || message.Contains("shadow atlas")))
                Log("  WARNING under " + current + ": " + message.Replace('\n', ' '));
        }

        // ---- coroutine runner on EditorApplication.update ----

        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();

        static void Tick()
        {
            try
            {
                while (stack.Count > 0)
                {
                    var top = stack.Peek();
                    if (!top.MoveNext()) { stack.Pop(); continue; }
                    if (top.Current is IEnumerator inner) { stack.Push(inner); continue; }
                    return;
                }
                EndJob();
            }
            catch (Exception e) { Fail("exception: " + e); EndJob(); }
        }

        static IEnumerator Wait(double seconds)
        {
            double end = EditorApplication.timeSinceStartup + seconds;
            while (EditorApplication.timeSinceStartup < end) yield return null;
        }

        static void EndJob()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            EditorApplication.isPlaying = false;
        }

        static void Finish()
        {
            int failures = SessionState.GetInt(K + "Failures", 0);
            Log(failures == 0 ? "RESULT PASS (probe ran; see the numbers)" : "RESULT FAIL (" + failures + ")");
            SessionState.SetBool(K + "Active", false);
            SessionState.SetBool(K + "Exit", true);
            if (!EditorApplication.isPlaying) CompleteExit();
        }

        static void CompleteExit()
        {
            if (!SessionState.GetBool(K + "Exit", false)) return;
            SessionState.SetBool(K + "Exit", false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(K + "Failures", 0) == 0 ? 0 : 1);
        }

        // ---- jobs ----

        static IEnumerator CoursesJob()
        {
            yield return Wait(3);
            var test = Object.FindFirstObjectByType<CoursePrefabTest>();
            if (test == null) { Fail("no CoursePrefabTest"); yield break; }
            var list = typeof(CoursePrefabTest).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(test) as IList;
            foreach (var want in CourseNames)
            {
                if (Only != "" && !want.Contains(Only)) continue;
                int found = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    var prefab = list[i].GetType().GetField("prefab").GetValue(list[i]) as GameObject;
                    if (prefab != null && prefab.name.Contains(want)) { found = i; break; }
                }
                if (found < 0) { Log("SKIP " + want + ": not in the course test"); continue; }
                test.Select(found);
                yield return Section(want, () => test.Current);
            }
        }

        static IEnumerator RoomsJob()
        {
            yield return Wait(3);
            var test = Object.FindFirstObjectByType<MiniGamePrefabTest>();
            if (test == null) { Fail("no MiniGamePrefabTest"); yield break; }
            var prefabs = typeof(MiniGamePrefabTest).GetField("prefabs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(test) as GameObject[];
            foreach (var want in RoomNames)
            {
                int found = prefabs == null ? -1 : Array.FindIndex(prefabs, p => p != null && p.name.Contains(want));
                if (found < 0) { Log("SKIP " + want + ": not in the mini-game test"); continue; }
                test.Select(found);
                yield return Section(want, () =>
                {
                    foreach (var s in Object.FindObjectsByType<PawnRushSection>(FindObjectsSortMode.None))
                        if (s.name.Contains(want)) return s;
                    return null;
                });
            }
        }

        // ---- one section ----

        static IEnumerator Section(string name, Func<PawnRushSection> find)
        {
            current = name;
            Log("== " + name);
            // Its runtime dressing (models, lights, water) goes on over the first seconds.
            yield return Wait(8);
            var section = find();
            if (section == null) { Fail(name + ": no section"); yield break; }
            Census(name, section);
            var cam = Camera.main;
            if (cam == null) { Fail(name + ": no main camera"); yield break; }
            // The follow camera and anything else on it would put it back.
            var held = new List<Behaviour>();
            foreach (var b in cam.GetComponents<Behaviour>())
                if (b.enabled && !(b is Camera) && b.GetType().Namespace != null && b.GetType().Namespace.StartsWith("ChessFight")) { b.enabled = false; held.Add(b); }
            var views = new List<KeyValuePair<string, Pose>> { new KeyValuePair<string, Pose>("entry", View(section, section.EntryWorld.position)) };
            foreach (var beat in section.Beats)
            {
                var middle = section.transform.TransformPoint((beat.min + beat.max) * .5f);
                views.Add(new KeyValuePair<string, Pose>(beat.name, View(section, middle)));
            }
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { name = "Section light probe 1080p" };
            var sync = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            double sumTotal = 0; int counted = 0; string worst = ""; double worstMs = 0;
            foreach (var v in views)
            {
                cam.transform.SetPositionAndRotation(v.Value.position, v.Value.rotation);
                // Places' light (ZoneLight) eases over about two seconds.
                yield return Wait(2.5);
                var seen = InView(cam, section);
                var was = cam.targetTexture;
                cam.targetTexture = target;
                var cpu = new List<double>(); var total = new List<double>();
                for (int i = 0; i < 18; i++)
                {
                    var watch = Stopwatch.StartNew();
                    cam.Render();
                    double c = watch.Elapsed.TotalMilliseconds;
                    var active = RenderTexture.active;
                    RenderTexture.active = target;
                    sync.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
                    RenderTexture.active = active;
                    double t = watch.Elapsed.TotalMilliseconds;
                    if (i >= 3) { cpu.Add(c); total.Add(t); }
                }
                cam.targetTexture = was;
                double mc = Median(cpu), mt = Median(total);
                sumTotal += mt; counted++;
                if (mt > worstMs) { worstMs = mt; worst = v.Key; }
                Csv("views.csv", name, v.Key, seen.renderers, seen.triangles, seen.casters, seen.lights, seen.shadowed, seen.maps, F(mc), F(mt));
                Log($"  view {v.Key}: renderers {seen.renderers} · triangles {seen.triangles:N0} · casters {seen.casters} · lights {seen.lights} (shadowed {seen.shadowed}, maps {seen.maps}) · {F(mc)} / {F(mt)} ms");
                yield return null;
            }
            foreach (var b in held) if (b != null) b.enabled = true;
            Object.Destroy(target);
            Object.Destroy(sync);
            if (counted > 0) Log($"  views: mean {F(sumTotal / counted)} ms, slowest '{worst}' {F(worstMs)} ms");
        }

        // A pawn's camera at a place: 4 m behind it along the section's way on, 2.5 m over the floor, looking on.
        static Pose View(PawnRushSection section, Vector3 at)
        {
            var on = section.transform.forward;
            float floor = at.y;
            if (Physics.Raycast(at + Vector3.up * 4f, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore)) floor = hit.point.y;
            var eye = new Vector3(at.x, floor + 2.5f, at.z) - on * 4f;
            var look = new Vector3(at.x, floor + 1f, at.z) + on * 6f;
            return new Pose(eye, Quaternion.LookRotation(look - eye, Vector3.up));
        }

        struct Seen { public int renderers, casters, lights, shadowed, maps; public long triangles; }

        static Seen InView(Camera cam, PawnRushSection section)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            var s = new Seen();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if ((cam.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                if (Vector3.Distance(cam.transform.position, r.bounds.ClosestPoint(cam.transform.position)) > cam.farClipPlane) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                s.renderers++;
                s.triangles += Triangles(r);
                if (r.shadowCastingMode != ShadowCastingMode.Off) s.casters++;
            }
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (!l.enabled || !l.gameObject.activeInHierarchy || l.type == LightType.Directional) continue;
                var b = new Bounds(l.transform.position, Vector3.one * (l.range * 2f));
                if (!GeometryUtility.TestPlanesAABB(planes, b)) continue;
                s.lights++;
                if (l.shadows != LightShadows.None) { s.shadowed++; s.maps += Maps(l); }
            }
            return s;
        }

        static int Maps(Light l) => l.type == LightType.Point ? 6 : 1;

        static long Triangles(Renderer r)
        {
            Mesh mesh = null;
            // A MeshRenderer may have no MeshFilter (a TextMesh's label).
            if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
            else if (r is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
            if (mesh == null) return 0;
            long n = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) n += (long)mesh.GetIndexCount(i) / 3;
            return n;
        }

        // Everything in the scene as it stands with this section built (the test scene holds one at a time).
        static void Census(string name, PawnRushSection section)
        {
            var kinds = new Dictionary<string, int>();
            int shadowed = 0, maps = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (!l.enabled || !l.gameObject.activeInHierarchy) continue;
                string kind = l.type + (l.shadows != LightShadows.None ? " shadowed" : "");
                kinds[kind] = kinds.TryGetValue(kind, out int n) ? n + 1 : 1;
                if (l.type != LightType.Directional && l.shadows != LightShadows.None)
                {
                    shadowed++; maps += Maps(l);
                    Csv("lights.csv", name, PathOf(l.transform), l.type, l.shadows, F(l.range), F(l.intensity), l.renderMode, Maps(l));
                }
            }
            int renderers = 0, casters = 0, transparent = 0, particles = 0;
            long triangles = 0;
            var materials = new HashSet<Material>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r is ParticleSystemRenderer) { particles++; continue; }
                renderers++;
                triangles += Triangles(r);
                if (r.shadowCastingMode != ShadowCastingMode.Off) casters++;
                foreach (var m in r.sharedMaterials)
                    if (m != null && materials.Add(m) && m.renderQueue >= 3000) transparent++;
            }
            // Which meshes hold the triangles: per mesh asset, its instances and their sum (meshes.csv).
            var byMesh = new Dictionary<Mesh, (int count, long tris, float size, string renderer)>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer) continue;
                Mesh mesh = null;
                if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf)) mesh = mf.sharedMesh;
                else if (r is SkinnedMeshRenderer sk) mesh = sk.sharedMesh;
                if (mesh == null) continue;
                long t = Triangles(r);
                float size = r.bounds.size.magnitude;
                byMesh[mesh] = byMesh.TryGetValue(mesh, out var e) ? (e.count + 1, e.tris + t, Mathf.Max(e.size, size), e.renderer) : (1, t, size, PathOf(r.transform));
            }
            foreach (var pair in byMesh.OrderByDescending(p => p.Value.tris).Take(60))
                Csv("meshes.csv", name, AssetDatabase.GetAssetPath(pair.Key), pair.Key.name, pair.Key.isReadable, pair.Value.count, pair.Value.tris, F(pair.Value.size), pair.Value.renderer);
            int probes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Count(p => p.isActiveAndEnabled);
            Log($"  lights: {string.Join(", ", kinds.OrderBy(k => k.Key).Select(k => k.Key + " " + k.Value))}");
            Log($"  shadowed punctual lights: {shadowed} (shadow maps {maps}; the atlas warning comes when one frame needs more than it holds)");
            Log($"  renderers {renderers:N0} · triangles {triangles:N0} · shadow casters {casters:N0} · materials {materials.Count} (transparent {transparent}) · particle systems {particles} · reflection probes {probes}");
        }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (int i = 0; t != null && i < 6; i++, t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static double Median(List<double> values)
        {
            if (values.Count == 0) return 0;
            var s = values.OrderBy(x => x).ToList();
            return s[s.Count / 2];
        }

        static string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        static void Csv(string file, params object[] cells) =>
            File.AppendAllText(Path.Combine(Out, file), string.Join(",", cells.Select(c => "\"" + Convert.ToString(c, CultureInfo.InvariantCulture).Replace("\"", "'") + "\"")) + "\n");

        static void Log(string line)
        {
            Directory.CreateDirectory(Out);
            File.AppendAllText(Path.Combine(Out, "report.txt"), line + "\n");
            Debug.Log("[SectionLightProbe] " + line);
        }

        static void Fail(string why)
        {
            SessionState.SetInt(K + "Failures", SessionState.GetInt(K + "Failures", 0) + 1);
            Log("FAIL " + why);
        }
    }
}
