#if CF_URP
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ChessFight.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush.Editor
{
    /// <summary>
    /// JY-gfx (2026-11-24): pictures of the real game for the graphics upgrade's before and after. The sections of a
    /// round, one at a time, as a round places them (the prefab, dressed in Play), seen from where the game's camera
    /// sits (OrbitCamera in Pawn Rush: 6 m behind the pawn, 15 degrees down, 0.6 m over its feet, 0.45 m over the
    /// right shoulder, 60 degrees of view, pulled in when a wall is in the way) at a few points of each section's
    /// route, with white and black pawns standing there, plus one wider view over each section. Time stands still,
    /// so a before and an after show the same moment.
    ///   batch: Unity -batchmode -projectPath copy -executeMethod ChessFight.PawnRush.Editor.GfxShots.Run
    ///          -gfxOut folder -gfxTag before [-gfxSections a,b,c] [-gfxWidth 1600 -gfxHeight 900]
    /// Pictures: folder/section_spot_tag.png, and notes_tag.txt (the sun, the sky, the settings each was taken with).
    /// </summary>
    [InitializeOnLoad]
    public static class GfxShots
    {
        const string ScenePath = "Assets/Scenes/PawnRush_Sections.unity";
        const string PrefabFolder = "Assets/Prefabs/PawnRushSections/";
        static readonly string[] DefaultSections =
        {
            "Early_1-1_RoyalWaterGarden", "Early_1-2_RoyalCarriageYard", "Early_MG_A_ParadeCourt", "Mid_2-1_GrandStairHall", "Late_3-1_RoyalLibrary",
        };
        const string ActiveKey = "GfxShots.Active", CodeKey = "GfxShots.Code", StartKey = "GfxShots.Start";
        const string OutKey = "GfxShots.Out", TagKey = "GfxShots.Tag", SectionsKey = "GfxShots.Sections", SizeKey = "GfxShots.Size";
        const string TuneKey = "GfxShots.Tune", SpotsKey = "GfxShots.Spots", MsaaKey = "GfxShots.Msaa", LineupKey = "GfxShots.Lineup";
        const string TimeKey = "GfxShots.Time", ScanKey = "GfxShots.Scan", FloorsKey = "GfxShots.Floors", AloneKey = "GfxShots.Alone";
        const string DumpKey = "GfxShots.Dump", FreezeKey = "GfxShots.Freeze";
        const float GiveUpSeconds = 1500f;

        static GfxShots()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Watchdog;
        }

        static string Arg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        // The corridors a round builds at a section's entry and exit (PawnRushRound, SectionCorridor). The neighbours are
        // stand-ins: a corridor asks only their slot and kind. The section's slot comes from its zone and kind, set as the
        // round sets it (before the section starts); the corridors' look follows it.
        internal static GameObject Joins(GameObject sectionObject, string name)
        {
            var section = sectionObject.GetComponent<PawnRushSection>();
            if (section == null) return null;
            int slot = name.StartsWith("Early_") ? 0 : name.StartsWith("Mid_") ? 2 : name.StartsWith("Late_") ? 4 : name.StartsWith("Final_") ? 6 : -1;
            if (slot < 0) return null;
            if (slot < 6 && section.Kind == PawnRushSection.SlotKind.MiniGame) slot++;
            section.Configure(slot, section.Seed, section.TeamSize, double.NaN);
            var holder = new GameObject("Joins (GfxShots)");
            if (slot > 0)
            {
                var entry = section.EntryWorld;
                var at = new Pose(entry.position - entry.rotation * Vector3.forward * SectionCorridor.Length, entry.rotation);
                SectionCorridor.Build(holder.transform, at, StandIn(holder.transform, slot - 1), section, 1);
            }
            if (slot < 6) SectionCorridor.Build(holder.transform, section.ExitWorld, section, StandIn(holder.transform, slot + 1), 2);
            return holder;
        }

        static PawnRushSection StandIn(Transform holder, int slot)
        {
            var go = new GameObject("Neighbour in slot " + slot + " (stand-in)");
            go.SetActive(false);
            go.transform.SetParent(holder, false);
            var s = go.AddComponent<PawnRushSection>();
            var kind = slot % 2 == 1 && slot < 6 ? PawnRushSection.SlotKind.MiniGame : PawnRushSection.SlotKind.Obstacle;
            s.Describe("", "", kind, Vector3.zero, Vector3.zero, 0f, null);
            s.Configure(slot, 1, 1, double.NaN);
            // The last slot is the war room: its corridor is wider at the far end and has its own kit.
            if (slot == 6) go.AddComponent<WarRoomSection>();
            return s;
        }

        [MenuItem("ChessFight/Graphics/Pictures of the sections (Play)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string folder = Arg("-gfxOut") ?? Path.Combine(Directory.GetCurrentDirectory(), "Logs", "GfxShots");
            Directory.CreateDirectory(folder);
            SessionState.SetString(OutKey, folder);
            SessionState.SetString(TagKey, Arg("-gfxTag") ?? "now");
            SessionState.SetString(SectionsKey, Arg("-gfxSections") ?? string.Join(",", DefaultSections));
            SessionState.SetString(SizeKey, (Arg("-gfxWidth") ?? "1600") + "x" + (Arg("-gfxHeight") ?? "900"));
            // -gfxTune "a:exp=0.2,con=14;b:tone=aces": looks to compare at every spot (pictures spot_tag_a, spot_tag_b).
            SessionState.SetString(TuneKey, Arg("-gfxTune") ?? "");
            // -gfxMsaa 4: the pipeline's MSAA for this run (put back at the end), the pictures drawn with it.
            SessionState.SetInt(MsaaKey, int.TryParse(Arg("-gfxMsaa"), out var msaa) ? msaa : 0);
            // -gfxLineup: the six white pieces in a row ahead of the first spot, a picture of them from the front (_lu).
            SessionState.SetBool(LineupKey, System.Environment.GetCommandLineArgs().Contains("-gfxLineup"));
            // -gfxSpots 1,0wide: only these spots of each section.
            SessionState.SetString(SpotsKey, Arg("-gfxSpots") ?? "");
            // -gfxTime 60: the cost instead of pictures. Every spot and look drawn 60 more times, each waited out on the
            // GPU, in times_tag.csv; and the frame a new GameProbes probe is laid in, where probes are on.
            SessionState.SetInt(TimeKey, int.TryParse(Arg("-gfxTime"), out var frames) ? frames : 0);
            // -gfxScan all (or a,b,c): those sections in at once before the pictures, and what the upgrade's runtime
            // look-rounds over every renderer cost then (scan_tag.txt).
            SessionState.SetString(ScanKey, Arg("-gfxScan") ?? "");
            // -gfxFloors: no pictures; at every route spot, rays through a 96x54 grid of the game camera's view find the
            // floor under each (a surface facing up) and its material: floors_<section>.txt and floors_all_tag.txt.
            SessionState.SetBool(FloorsKey, System.Environment.GetCommandLineArgs().Contains("-gfxFloors"));
            // -gfxAlone: each section by itself, open at its ends (without it, the corridors a round joins it with).
            SessionState.SetBool(AloneKey, System.Environment.GetCommandLineArgs().Contains("-gfxAlone"));
            // -gfxDump: what each section is made of and where, and every light on it (anatomy_<section>.txt).
            SessionState.SetBool(DumpKey, System.Environment.GetCommandLineArgs().Contains("-gfxDump"));
            // -gfxFreeze: each section as the game built and dressed it (its models, the upgrade's materials, the rig's
            // lights, the corridors) saved as a prefab with the meshes, materials and textures it made, for GfxBake to bake.
            SessionState.SetBool(FreezeKey, System.Environment.GetCommandLineArgs().Contains("-gfxFreeze"));
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // The scene's own round, playtest and camera off: the sections come in one at a time.
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "Pawn Rush Round" || go.name == "Playtest" || go.name == "Main Camera") go.SetActive(false);
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(CodeKey, 1);
            SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode) new GameObject("Gfx shots").AddComponent<Runner>();
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseBool(ActiveKey);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(CodeKey, 1));
            }
        }

        // A run that hangs must not keep a batch Unity open for ever.
        static void Watchdog()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartKey, 0f) < GiveUpSeconds) return;
            SessionState.EraseBool(ActiveKey);
            Debug.LogError("[GfxShots] gave up after " + GiveUpSeconds + " s");
            if (Application.isBatchMode) EditorApplication.Exit(3);
            else EditorApplication.isPlaying = false;
        }

        sealed class Spot
        {
            public string name;
            public Vector3 eye, target, at, way;
            public float fov = 60f;
            // The view's up (a straight-down view needs one) and, above 0, an orthographic view this many metres half high.
            public Vector3 up = Vector3.up;
            public float ortho;
        }

        sealed class Runner : MonoBehaviour
        {
            readonly StringBuilder notes = new StringBuilder(), times = new StringBuilder();
            readonly Dictionary<int, RenderTexture> targets = new Dictionary<int, RenderTexture>();
            Camera cam;
            int width, height, pictures, failures, runMsaa, timed;
            string folder, tag;
            RenderTexture tiny;
            Texture2D tinyRead;

            IEnumerator Start()
            {
                folder = SessionState.GetString(OutKey, ".");
                tag = SessionState.GetString(TagKey, "now");
                var size = SessionState.GetString(SizeKey, "1600x900").Split('x');
                width = int.Parse(size[0]);
                height = int.Parse(size[1]);
                Application.targetFrameRate = 60;
                notes.AppendLine($"GfxShots '{tag}' {System.DateTime.Now:yyyy-MM-dd HH:mm}, Unity {Application.unityVersion}, {width}x{height}");

                // The round's far fog (PawnRushRound.FarFog), as a round sets it.
                if (!RenderSettings.fog)
                {
                    RenderSettings.fogMode = FogMode.Linear;
                    RenderSettings.fogStartDistance = PawnRushRound.FarFogStart;
                    RenderSettings.fogEndDistance = PawnRushRound.FarFogEnd;
                    RenderSettings.fog = true;
                }

                cam = new GameObject("Gfx shots camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                // The light shafts are drawn for this camera too, though it draws into a texture (GameShafts).
                GameShafts.CaptureCamera = cam;
                cam.nearClipPlane = .1f;
                cam.farClipPlane = 3000f;
                cam.fieldOfView = 60f;
                cam.allowHDR = true;
                var data = cam.GetUniversalAdditionalCameraData();
                // As GameLook.Prepare sets every game camera (it skips cameras that draw into a texture).
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                data.renderShadows = true;
                ZoneLight.Instant = true;
                var pipe = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                int msaa = SessionState.GetInt(MsaaKey, 0), msaa0 = pipe != null ? pipe.msaaSampleCount : 1;
                if (pipe != null && msaa > 0) pipe.msaaSampleCount = msaa;
                // A camera that draws into a texture takes its MSAA from the texture.
                var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = pipe != null ? pipe.msaaSampleCount : 1 };
                target.Create();
                runMsaa = target.antiAliasing;
                targets[runMsaa] = target;
                float shadows0 = pipe != null ? pipe.shadowDistance : 0f;
                int cascades0 = pipe != null ? pipe.shadowCascadeCount : 1;
                int timeFrames = SessionState.GetInt(TimeKey, 0);
                if (timeFrames > 0)
                {
                    tiny = new RenderTexture(4, 4, 0, RenderTextureFormat.ARGB32);
                    tiny.Create();
                    tinyRead = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    times.AppendLine("section,spot,look,msaa,shadow_m,cascades,frames,total_median_ms,total_p90_ms,cpu_median_ms");
                    notes.AppendLine($"timing on {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), {width}x{height}, {timeFrames} frames a look, each waited out on the GPU");
                }
                string scan = SessionState.GetString(ScanKey, "");
                if (scan.Length > 0) yield return Scan(scan);

                foreach (var name in SessionState.GetString(SectionsKey, "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab");
                    if (prefab == null)
                    {
                        notes.AppendLine($"FAIL {name}: no prefab");
                        failures++;
                        continue;
                    }
                    Time.timeScale = 1f;
                    var section = Instantiate(prefab);
                    section.name = name;
                    // JY 11-24 ("실제 게임상에선 통로와 연결되어 막혀있는 부분이니까. 다른 프리팹들도 마찬가지고"): a
                    // round joins every section to its neighbours by corridors (SectionCorridor: walls, a ceiling, a
                    // header over a mini-game's arch), so its ends are closed there. The pictures have them too: what the
                    // game's camera sees at the ends, and no sun or sky through them.
                    var joins = SessionState.GetBool(AloneKey, false) ? null : Joins(section, name);
                    // Awake and Start dress the section; the first frames settle what moves.
                    for (int i = 0; i < 40; i++) yield return null;
                    Time.timeScale = 0f;
                    Inventory(section, name);
                    // The URP Lit materials on Gfx Lit now (GameSurfaces' watch does it every two seconds anyway).
                    GameSurfaces.Dress(GameSurfaces.LitOn);
                    GameGrass.Grow();
                    // The section's course light and the rest of the upgrade's look at it (GfxSections' watch looks only
                    // every two seconds).
                    GfxSections.Find();
                    // The small things on its paving, now (the watch spreads them over frames).
                    var rushSection = section.GetComponent<PawnRushSection>();
                    if (rushSection != null) GfxGround.Now(rushSection);
                    if (SessionState.GetBool(DumpKey, false)) Anatomy(section, name);
                    var spots = Spots(section, name);
                    var pawns = Pawns(spots);
                    var lineup = SessionState.GetBool(LineupKey, false) ? Lineup(spots, name) : null;
                    string only = SessionState.GetString(SpotsKey, "");
                    var tunes = Tunes(SessionState.GetString(TuneKey, ""));
                    bool census = SessionState.GetBool(FloorsKey, false);
                    var floorTable = new Dictionary<string, FloorEntry>();
                    int floorPixels = 0, allPixels = 0;
                    var floorDistances = new List<float>();
                    foreach (var spot in spots)
                    {
                        if (only.Length > 0 && !only.Split(',').Any(s => spot.name.EndsWith("_" + s.Trim()))) continue;
                        cam.transform.SetPositionAndRotation(spot.eye, Quaternion.LookRotation(spot.target - spot.eye, spot.up));
                        cam.fieldOfView = spot.fov;
                        cam.orthographic = spot.ortho > 0f;
                        if (cam.orthographic) cam.orthographicSize = spot.ortho;
                        if (census)
                        {
                            // Route spots only (the game's camera); the close-ups and the wide view are not what a player sees.
                            if (spot.way == Vector3.zero) continue;
                            for (int i = 0; i < 4; i++) yield return null;
                            FloorCensus(section, floorTable, ref floorPixels, ref allPixels, floorDistances);
                            continue;
                        }
                        foreach (var (tuneName, settings) in tunes)
                        {
                            Look(settings);
                            // msaa=1, shd=40, casc=2: the pipeline's MSAA and shadows for this look (the game before the
                            // upgrade had MSAA off and 40 m of shadows in 2 cascades; the switch does not touch them).
                            int samples = settings.TryGetValue("msaa", out var ms) && int.TryParse(ms, out var m) ? Mathf.Max(1, m) : runMsaa;
                            if (pipe != null)
                            {
                                pipe.msaaSampleCount = samples;
                                pipe.shadowDistance = settings.TryGetValue("shd", out var sd) && float.TryParse(sd, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : shadows0;
                                pipe.shadowCascadeCount = settings.TryGetValue("casc", out var sc) && int.TryParse(sc, out var c) ? c : cascades0;
                            }
                            var rt = Target(samples);
                            Reflect();
                            // The place's own reflections where this picture is (GameProbes; probes=0 to compare).
                            GameProbes.Now(cam);
                            // A few frames at the spot: the zone lights, the shadow cascades, the exposure settle.
                            for (int i = 0; i < 8; i++)
                            {
                                Draw(rt);
                                yield return null;
                            }
                            if (spot == spots[0]) Ambient(name, tuneName);
                            if (timeFrames > 0)
                            {
                                yield return Clock(rt, name, spot.name, tuneName, samples, timeFrames);
                                if (spot == spots[0] && GameProbes.On && GameGfx.Upgrade) yield return ProbeSpike(rt, name, tuneName);
                            }
                            else Capture(rt, tuneName.Length > 0 ? $"{spot.name}_{tag}_{tuneName}" : $"{spot.name}_{tag}");
                        }
                    }
                    if (census) WriteFloors(name, floorTable, floorPixels, allPixels, floorDistances);
                    var sun = ZoneLight.Sun != null ? ZoneLight.Sun : RenderSettings.sun;
                    notes.AppendLine($"{name}: {spots.Count} pictures; sun {(sun != null ? $"{sun.intensity:0.00} ({sun.color.r:0.00}, {sun.color.g:0.00}, {sun.color.b:0.00}) {sun.transform.eulerAngles}" : "none")}, "
                        + $"sky {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none")}, ambient {RenderSettings.ambientMode}, fog {RenderSettings.fog} {RenderSettings.fogMode}");
                    if (GfxBaked.Last.Length > 0) notes.AppendLine("  " + GfxBaked.Last);
                    if (SessionState.GetBool(FreezeKey, false)) Freeze(section, joins, name);
                    if (pawns != null) Destroy(pawns);
                    if (lineup != null) Destroy(lineup);
                    if (joins != null) Destroy(joins);
                    Destroy(section);
                    yield return null;
                    yield return null;
                }
                // Everything a look changed back as the run found it (the renderer asset above all).
                Look(new Dictionary<string, string>());
                if (pipe != null)
                {
                    pipe.msaaSampleCount = msaa0;
                    pipe.shadowDistance = shadows0;
                    pipe.shadowCascadeCount = cascades0;
                }
                var ssao = Ssao();
                if (ssao != null) notes.AppendLine($"ssao: intensity {Field(ssao, "Intensity"):0.00}, radius {Field(ssao, "Radius"):0.00}, direct {Field(ssao, "DirectLightingStrength"):0.00}");
                var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (pipeline != null)
                    notes.AppendLine($"pipeline {pipeline.name}: HDR {pipeline.supportsHDR}, shadows {pipeline.shadowDistance} m in {pipeline.shadowCascadeCount}, "
                        + $"grading {pipeline.colorGradingMode}, MSAA {pipeline.msaaSampleCount}");
                if (censused > 0) WriteAllFloors();
                notes.AppendLine($"zone light mixer: the scene's ambient light noted again {ZoneLight.Renotes} times (it changed under the mix)");
                notes.AppendLine($"pictures {pictures}, timed looks {timed}, floor censuses {censused}, failed {failures}");
                File.WriteAllText(Path.Combine(folder, $"notes_{tag}.txt"), notes.ToString());
                if (timeFrames > 0) File.WriteAllText(Path.Combine(folder, $"times_{tag}.csv"), times.ToString());
                Debug.Log("[GfxShots]\n" + notes);
                ZoneLight.Instant = false;
                Time.timeScale = 1f;
                foreach (var rt in targets.Values) if (rt != null) rt.Release();
                if (tiny != null) tiny.Release();
                if (idTarget != null) idTarget.Release();
                foreach (var id in idMaterialOf.Values) if (id != null) Destroy(id);
                SessionState.SetInt(CodeKey, failures == 0 && pictures + timed + censused > 0 ? 0 : 1);
                EditorApplication.isPlaying = false;
            }

            // Points along the section's route (white's), the game's camera behind each, and one view over the section.
            List<Spot> Spots(GameObject section, string name)
            {
                var list = new List<Spot>();
                var path = section.GetComponentInChildren<ProgressPath>(true);
                List<Vector3> route = path != null ? path.World(0) : null;
                if (route == null || route.Count < 2)
                {
                    // No route: the line from the entry to the exit.
                    var s = section.GetComponent<PawnRushSection>();
                    if (s == null)
                    {
                        notes.AppendLine($"FAIL {name}: no route and no section");
                        failures++;
                        return list;
                    }
                    route = new List<Vector3> { s.EntryWorld.position, s.ExitWorld.position };
                    notes.AppendLine($"{name}: no route, entry to exit");
                }
                float length = ProgressPath.Length(route);
                Physics.SyncTransforms();
                var at = new[] { .1f, .4f, .72f };
                for (int i = 0; i < at.Length; i++)
                {
                    var p = Ground(PointAt(route, at[i] * length));
                    var a = PointAt(route, Mathf.Max(0f, at[i] * length - 3f));
                    var b = PointAt(route, Mathf.Min(length, at[i] * length + 3f));
                    var way = b - a;
                    way.y = 0f;
                    way = way.sqrMagnitude > 1e-4f ? way.normalized : section.transform.forward;
                    // OrbitCamera: yaw along the way, pitch 15 down, 6 m back from a pivot 0.6 m over the feet, over the right shoulder.
                    var look = Quaternion.LookRotation(way, Vector3.up) * Quaternion.Euler(15f, 0f, 0f);
                    var pivot = p + Vector3.up * .6f + Vector3.Cross(Vector3.up, way).normalized * .45f;
                    float back = 6f;
                    if (Physics.SphereCast(pivot, .2f, -(look * Vector3.forward), out var hit, back, ~0, QueryTriggerInteraction.Ignore))
                        back = Mathf.Max(1.5f, hit.distance - .05f);
                    var eye = pivot - look * Vector3.forward * back;
                    list.Add(new Spot { name = $"{Short(name)}_{i + 1}", at = p, way = way, eye = eye, target = eye + look * Vector3.forward * 10f });
                }
                // Close to the first spot's white pawn (the character's look): from behind three quarters, as the game's
                // camera sees it, and from the front three quarters, as the lobby shows it.
                if (list.Count > 0)
                {
                    var s1 = list[0];
                    var side = Vector3.Cross(Vector3.up, s1.way).normalized;
                    var centre = Ground(s1.at + side * .3f + s1.way * .2f) + Vector3.up * .5f;
                    list.Add(new Spot { name = $"{Short(name)}_pcb", eye = centre - s1.way * 2.2f + side * .9f + Vector3.up * .5f, target = centre, fov = 40f });
                    list.Add(new Spot { name = $"{Short(name)}_pcf", eye = centre + s1.way * 2.2f + side * .9f + Vector3.up * .4f, target = centre, fov = 40f });
                }
                // Over the section: from behind and above its start, along its first stretch.
                {
                    var p0 = PointAt(route, 0f);
                    var p1 = PointAt(route, Mathf.Min(length, 40f));
                    var way = p1 - p0;
                    way.y = 0f;
                    way = way.sqrMagnitude > 1e-4f ? way.normalized : section.transform.forward;
                    list.Add(new Spot { name = $"{Short(name)}_0wide", eye = p0 - way * 10f + Vector3.up * 9f, target = PointAt(route, Mathf.Min(length, 30f)) + Vector3.up * 1f, fov = 62f });
                }
                // Straight down over the hall from just under its lid, the way on upward, orthographic: where the sun's
                // pools and the lamps' light fall. Inside the place, so its light is on; what is above is not drawn.
                // Only when asked (-gfxSpots ...,top).
                if (SessionState.GetString(SpotsKey, "").Split(',').Any(s => s.Trim() == "top") && GfxMapLights.Box(section.transform, out var hall))
                {
                    var frame = section.transform;
                    float y = hall.max.y - .1f;
                    var lid = section.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name.StartsWith("Sun lid"));
                    if (lid != null && lid.sharedMesh != null) y = Mathf.Min(y, lid.sharedMesh.bounds.min.y - .4f);
                    var eye = frame.TransformPoint(new Vector3(hall.center.x, y, hall.center.z));
                    float aspect = 16f / 9f;
                    list.Add(new Spot { name = $"{Short(name)}_top", eye = eye, target = eye - frame.up * 10f, up = frame.forward, ortho = Mathf.Max(hall.size.z * .52f, hall.size.x * .52f / aspect) });
                }
                return list;
            }

            // The looks to compare: (name, key=value pairs). None asked: one, as the game sets it.
            static List<(string, Dictionary<string, string>)> Tunes(string text)
            {
                var list = new List<(string, Dictionary<string, string>)>();
                foreach (var part in text.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0))
                {
                    int colon = part.IndexOf(':');
                    string name = colon > 0 ? part.Substring(0, colon) : part;
                    var settings = new Dictionary<string, string>();
                    foreach (var kv in (colon > 0 ? part.Substring(colon + 1) : "").Split(',').Select(s => s.Trim()).Where(s => s.Contains('=')))
                        settings[kv.Substring(0, kv.IndexOf('=')).Trim()] = kv.Substring(kv.IndexOf('=') + 1).Trim();
                    list.Add((name, settings));
                }
                if (list.Count == 0) list.Add(("", new Dictionary<string, string>()));
                return list;
            }

            static readonly Dictionary<string, float> baseline = new Dictionary<string, float>();

            // Sets one look: the grade (GameGrade), the sun, the sky and its light, the surface sets' strength. Keys not
            // given go back to how the run found them.
            void Look(Dictionary<string, string> s)
            {
                float F(string key, float now)
                {
                    if (!baseline.ContainsKey(key)) baseline[key] = now;
                    return s.TryGetValue(key, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : baseline[key];
                }
                if (!baseline.ContainsKey("tone")) baseline["tone"] = (float)GameGrade.Tone;
                GameGrade.Tone = s.TryGetValue("tone", out var tone) ? (tone == "aces" ? TonemappingMode.ACES : TonemappingMode.Neutral) : (TonemappingMode)(int)baseline["tone"];
                GameGrade.Exposure = F("exp", GameGrade.Exposure);
                GameGrade.Contrast = F("con", GameGrade.Contrast);
                GameGrade.Saturation = F("sat", GameGrade.Saturation);
                GameGrade.Temperature = F("temp", GameGrade.Temperature);
                GameGrade.BloomIntensity = F("bloom", GameGrade.BloomIntensity);
                GameGrade.Vignette = F("vig", GameGrade.Vignette);
                GameGrade.ShaftIntensity = F("shaft", GameGrade.ShaftIntensity);
                GameGrade.ShaftDensity = F("shaftd", GameGrade.ShaftDensity);
                GameGrade.ShaftAnisotropy = F("shaftg", GameGrade.ShaftAnisotropy);
                GameProbes.On = F("probes", 1f) > .5f;
                // course=0: the indoor maps without their own light (GameCourses, GfxMapLights); <map>.<Field>=v sets
                // one number of a map's look or rig (colours and ways as r|g|b), every other as the run found it.
                GameCourses.On = F("course", 1f) > .5f;
                MapKeys(s);
                // ground=0: the paving without its tufts and pebbles (GfxGround).
                GfxGround.On = F("ground", 1f) > .5f;
                GameCharacters.Ambient = F("cama", GameCharacters.Ambient);
                GameCharacters.Gloss = F("camg", GameCharacters.Gloss);
                GameCharacters.Rim = F("camr", GameCharacters.Rim);
                GameCharacters.Reflection = F("camw", GameCharacters.Reflection);
                GameCharacters.Desaturate = F("ctd", GameCharacters.Desaturate);
                var tint = GameCharacters.Tint;
                GameCharacters.Tint = new Color(F("ctr", tint.r), F("ctg", tint.g), F("ctb", tint.b), 1f);
                var st = GameGrade.ShadowTone;
                GameGrade.ShadowTone = new Color(F("shr", st.r), F("shg", st.g), F("shb", st.b), 1f);
                var ht = GameGrade.HighlightTone;
                GameGrade.HighlightTone = new Color(F("hir", ht.r), F("hig", ht.g), F("hib", ht.b), 1f);
                foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
                    if (v.name == "Game grade (JY-gfx)") v.sharedProfile = GameGrade.Profile();
                RenderSettings.ambientIntensity = F("amb", RenderSettings.ambientIntensity);
                var sun = RenderSettings.sun;
                if (sun != null)
                {
                    sun.intensity = F("sun", sun.intensity);
                    var sc = sun.color;
                    sun.color = new Color(F("sunr", sc.r), F("sung", sc.g), F("sunb", sc.b), 1f);
                }
                if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Exposure"))
                    RenderSettings.skybox.SetFloat("_Exposure", F("sky", RenderSettings.skybox.GetFloat("_Exposure")));
                // The sky's light follows its brightness and the ambient strength.
                DynamicGI.UpdateEnvironment();
                GameSurfaces.Strength = F("surf", GameSurfaces.Strength);
                GameSurfaces.Apply();
                probeOn = F("probe", 0f) > .5f;
                GameGrass.Show(F("grass", 1f) > .5f);
                GameSurfaces.Macro = F("macro", GameSurfaces.Macro);
                GameSurfaces.Foliage = F("fol", GameSurfaces.Foliage);
                GameSurfaces.Apply();
                GameSurfaces.LitOn = F("lit", GameSurfaces.LitOn ? 1f : 0f) > .5f;
                GameSurfaces.Dress(GameSurfaces.LitOn);
                // relief=0: the floors flat as before; rdepth=1.5: every relief deeper.
                GameSurfaces.ReliefOn = F("relief", 1f) > .5f;
                GameSurfaces.ReliefDepth = F("rdepth", GameSurfaces.ReliefDepth);
                GameSurfaces.ReliefShadow = F("rshadow", GameSurfaces.ReliefShadow);
                GameSurfaces.ApplyRelief();
                // The scene's haze (linear fog): distances and colour.
                RenderSettings.fogStartDistance = F("fogs", RenderSettings.fogStartDistance);
                RenderSettings.fogEndDistance = F("foge", RenderSettings.fogEndDistance);
                var fog = RenderSettings.fogColor;
                RenderSettings.fogColor = new Color(F("fogr", fog.r), F("fogg", fog.g), F("fogb", fog.b), 1f);
                // bake=0: no section gets its baked indirect light (GfxBaked), to compare.
                GfxBaked.Enabled = F("bake", 1f) > .5f;
                // The renderer's ambient occlusion (SSAO): strength, reach in metres, share on the direct light.
                var ao = Ssao();
                if (ao != null)
                {
                    Field(ao, "Intensity", F("aoi", Field(ao, "Intensity")));
                    Field(ao, "Radius", F("aor", Field(ao, "Radius")));
                    Field(ao, "DirectLightingStrength", F("aod", Field(ao, "DirectLightingStrength")));
                }
                // gfx=0: the whole upgrade off (GameGfx), last so it wins over the keys above.
                GameGfx.Set(F("gfx", 1f) > .5f);
                // char=0: the pieces flat and in Look A while the rest stays upgraded (after the switch, which sets them too).
                if (F("gfx", 1f) > .5f) GameCharacters.Show(F("char", 1f) > .5f);
            }

            static Dictionary<string, object> mapBase;

            // MG_F_ClockworkBallroom.Indoor=.7 or WarRoom.GoalColor=1|.8|.5: one number of a map's look
            // (GameCourses.Look) or rig (GfxMapLights.Rig) for this look; every number not given goes back to how the run
            // found it. The map is GfxMapLights.Maps' Key (Course2 / Course3 for the defaults).
            static void MapKeys(Dictionary<string, string> s)
            {
                var maps = new List<(string key, object look, object rig)> { ("Course2", GfxMapLights.Course2Default.Look, GfxMapLights.Course2Default.Rig), ("Course3", GfxMapLights.Course3Default.Look, GfxMapLights.Course3Default.Rig) };
                foreach (var m in GfxMapLights.Maps) maps.Add((m.Key, m.Look, m.Rig));
                if (mapBase == null)
                {
                    mapBase = new Dictionary<string, object>();
                    foreach (var (key, look, rig) in maps)
                        foreach (var o in new[] { look, rig })
                            foreach (var f in o.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)) mapBase[key + "." + f.Name] = f.GetValue(o);
                }
                foreach (var (key, look, rig) in maps)
                    foreach (var o in new[] { look, rig })
                        foreach (var f in o.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                        {
                            object value = mapBase[key + "." + f.Name];
                            if (s.TryGetValue(key + "." + f.Name, out var text))
                            {
                                var parts = text.Split('|');
                                float P(int i) => float.Parse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture);
                                if (f.FieldType == typeof(float)) value = P(0);
                                else if (f.FieldType == typeof(int)) value = (int)P(0);
                                else if (f.FieldType == typeof(bool)) value = P(0) > .5f;
                                else if (f.FieldType == typeof(Color)) value = new Color(P(0), P(1), P(2), 1f);
                                else if (f.FieldType == typeof(Vector3)) value = new Vector3(P(0), P(1), P(2));
                                else if (f.FieldType == typeof(string)) value = text;
                            }
                            f.SetValue(o, value);
                        }
                GameCourses.Refresh();
            }

            bool probeOn;
            ReflectionProbe probe;

            // probe=1: a reflection probe at the camera, rendered for this picture (what a probe that follows the game's
            // camera would show: the place around, not the open sky, in every shiny thing).
            void Reflect()
            {
                if (!probeOn)
                {
                    if (probe != null) probe.enabled = false;
                    return;
                }
                if (probe == null)
                {
                    probe = new GameObject("Gfx shots probe").AddComponent<ReflectionProbe>();
                    probe.mode = ReflectionProbeMode.Realtime;
                    probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                    probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                    probe.size = new Vector3(400f, 200f, 400f);
                    probe.resolution = 256;
                    probe.hdr = true;
                    probe.importance = 10;
                }
                probe.enabled = true;
                probe.transform.position = cam.transform.position;
                probe.RenderProbe();
            }

            // The SSAO feature's settings in the pipeline's renderers (URP keeps them internal: read by reflection). The
            // asset is changed in memory only, and put back by the last Look of the run.
            static object Ssao()
            {
                var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                var list = pipeline != null ? typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(pipeline) as ScriptableRendererData[] : null;
                if (list == null) return null;
                foreach (var data in list)
                    if (data != null)
                        foreach (var feature in data.rendererFeatures)
                            if (feature != null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                                return feature.GetType().GetField("m_Settings", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(feature);
                return null;
            }

            static float Field(object o, string name)
            {
                var f = o.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return f != null && f.FieldType == typeof(float) ? (float)f.GetValue(o) : 0f;
            }

            static void Field(object o, string name, float value)
            {
                var f = o.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(float)) f.SetValue(o, value);
            }

            // Which shaders the section's renderers wear, with the share of renderers each (what an upgrade reaches).
            void Inventory(GameObject section, string name)
            {
                var counts = new Dictionary<string, int>();
                var sets = new SortedSet<string>();
                int total = 0;
                foreach (var r in section.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        string key = m.shader != null ? m.shader.name : "?";
                        // The set each URP Lit material wears on Gfx Lit (GameSurfaces.Pick).
                        if (key == "Universal Render Pipeline/Lit")
                        {
                            int set = GameSurfaces.Pick(m, out float scale);
                            sets.Add($"{(set < 0 ? "none" : GameSurfaces.Sets[set]),-10} {scale:0.00}  {m.name}");
                        }
                        else if (key == "ChessFight/Gfx Lit")
                            sets.Add($"{GameSurfaces.Sets[Mathf.Clamp((int)m.GetFloat("_GfxSet"), 0, 8)],-10} {m.GetFloat("_GfxSetScale"):0.00}  {m.name}");
                        counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
                        total++;
                    }
                }
                var sb = new StringBuilder();
                foreach (var kv in counts.OrderByDescending(k => k.Value)) sb.AppendLine($"{kv.Value,6}  {100f * kv.Value / Mathf.Max(1, total),5:0.0}%  {kv.Key}");
                File.WriteAllText(Path.Combine(folder, $"shaders_{Short(name)}.txt"), sb.ToString());
                File.WriteAllLines(Path.Combine(folder, $"litsets_{Short(name)}.txt"), sets);
            }

            // The section and its corridors as built and dressed now, saved for a bake in edit mode (GfxBake -bakePrefab):
            // Assets/GfxBakeTest/Frozen/<section>_frozen.prefab, and everything it made at run time (meshes, materials,
            // readable textures) in <section>_made.asset beside it. The batch copy only.
            void Freeze(GameObject section, GameObject joins, string name)
            {
                const string Root = "Assets/GfxBakeTest", Dir = Root + "/Frozen";
                if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "GfxBakeTest");
                if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder(Root, "Frozen");
                string store = Dir + "/" + Short(name) + "_made.asset", prefabPath = Dir + "/" + Short(name) + "_frozen.prefab";
                AssetDatabase.DeleteAsset(store);
                AssetDatabase.DeleteAsset(prefabPath);
                var holder = new GameObject(Short(name) + " (frozen)");
                var parents = new List<(Transform t, Transform parent)>();
                foreach (var go in new[] { section, joins })
                {
                    if (go == null) continue;
                    parents.Add((go.transform, go.transform.parent));
                    go.transform.SetParent(holder.transform, true);
                }
                Object main = null;
                int kept = 0;
                var done = new HashSet<Object>();
                void Keep(Object o)
                {
                    if (o == null || o is RenderTexture || !done.Add(o) || AssetDatabase.Contains(o)) return;
                    if (o is Texture2D t && !t.isReadable) return;
                    o.hideFlags = HideFlags.None;
                    if (main == null)
                    {
                        AssetDatabase.CreateAsset(o, store);
                        main = o;
                    }
                    else AssetDatabase.AddObjectToAsset(o, main);
                    kept++;
                }
                foreach (var filter in holder.GetComponentsInChildren<MeshFilter>(true)) Keep(filter.sharedMesh);
                foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        foreach (var id in m.GetTexturePropertyNameIDs()) Keep(m.GetTexture(id));
                        Keep(m);
                    }
                AssetDatabase.SaveAssets();
                PrefabUtility.SaveAsPrefabAsset(holder, prefabPath, out bool saved);
                foreach (var (t, parent) in parents) t.SetParent(parent, true);
                Destroy(holder);
                notes.AppendLine($"{name}: frozen as {prefabPath} ({(saved ? "saved" : "FAILED")}), {kept} made things kept");
            }

            // What the section is made of and where, in its own frame (metres: x right, y up, z along the way), and every
            // light in the scene: renderers grouped by their top part under the section and their name without its number.
            void Anatomy(GameObject section, string name)
            {
                var frame = section.transform;
                var inv = CultureInfo.InvariantCulture;
                string V(Vector3 v) => string.Format(inv, "({0:0.0} {1:0.0} {2:0.0})", v.x, v.y, v.z);
                Bounds Local(Bounds b)
                {
                    var l = new Bounds(frame.InverseTransformPoint(b.center), Vector3.zero);
                    for (int i = 0; i < 8; i++)
                        l.Encapsulate(frame.InverseTransformPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
                    return l;
                }
                var sb = new StringBuilder();
                sb.AppendLine($"{name}: frame at {V(frame.position)}, yaw {frame.eulerAngles.y:0}");
                if (GfxMapLights.Box(frame, out var box)) sb.AppendLine($"box {V(box.min)} .. {V(box.max)}");
                var rush = section.GetComponent<PawnRushSection>();
                if (rush != null) sb.AppendLine($"entry {V(rush.EntryLocal.position)}, exit {V(rush.ExitLocal.position)} yaw {rush.ExitLocal.rotation.eulerAngles.y:0}, kind {rush.Kind}, slot {rush.Slot}");
                var path = section.GetComponentInChildren<ProgressPath>(true);
                var route = path != null ? path.World(0) : null;
                if (route != null) sb.AppendLine("white's route: " + string.Join(" ", route.Select(p => V(frame.InverseTransformPoint(p)))));
                var groups = new SortedDictionary<string, (int n, Bounds b, HashSet<string> mats, string shadow, bool glow)>();
                foreach (var r in section.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    var top = r.transform;
                    while (top.parent != null && top.parent != frame) top = top.parent;
                    string own = System.Text.RegularExpressions.Regex.Replace(r.name, @"\s*\(\d+\)$", "");
                    string key = (top == r.transform ? "" : top.name + " / ") + own + (r.enabled && r.gameObject.activeInHierarchy ? "" : " [off]");
                    var lb = Local(r.bounds);
                    bool glow = r.sharedMaterials.Any(m => m != null && m.IsKeywordEnabled("_EMISSION") && m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent >= .5f);
                    if (groups.TryGetValue(key, out var g))
                    {
                        g.b.Encapsulate(lb);
                        foreach (var m in r.sharedMaterials) if (m != null) g.mats.Add(m.name);
                        groups[key] = (g.n + 1, g.b, g.mats, g.shadow, g.glow || glow);
                    }
                    else groups[key] = (1, lb, new HashSet<string>(r.sharedMaterials.Where(m => m != null).Select(m => m.name)), r.shadowCastingMode.ToString(), glow);
                }
                sb.AppendLine($"renderers in {groups.Count} groups (count, box min .. max, shadows, glows, materials):");
                foreach (var kv in groups)
                    sb.AppendLine($"  {kv.Key} x{kv.Value.n}: {V(kv.Value.b.min)} .. {V(kv.Value.b.max)} {kv.Value.shadow}{(kv.Value.glow ? " GLOWS" : "")} [{string.Join(", ", kv.Value.mats.Take(4))}]");
                sb.AppendLine("lights (type, where, way, intensity, range, angle, colour, shadows, parent):");
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var way = frame.InverseTransformDirection(l.transform.forward);
                    sb.AppendLine(string.Format(inv, "  {0} {1}{2} at {3} way {4} i {5:0.00} r {6:0.0} a {7:0} c ({8:0.00} {9:0.00} {10:0.00}) {11} under {12}",
                        l.name, l.type, l.isActiveAndEnabled ? "" : " [off]", V(frame.InverseTransformPoint(l.transform.position)), V(way), l.intensity, l.range,
                        l.spotAngle, l.color.r, l.color.g, l.color.b, l.shadows, l.transform.parent != null ? l.transform.parent.name : "-"));
                }
                File.WriteAllText(Path.Combine(folder, $"anatomy_{Short(name)}.txt"), sb.ToString());
            }

            static string Short(string name)
            {
                int under = name.IndexOf('_');
                return under >= 0 ? name.Substring(under + 1) : name;
            }

            static Vector3 Ground(Vector3 p)
            {
                if (Physics.Raycast(p + Vector3.up * 2.5f, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
                return p;
            }

            static Vector3 PointAt(List<Vector3> route, float distance)
            {
                for (int i = 1; i < route.Count; i++)
                {
                    float leg = Vector3.Distance(route[i - 1], route[i]);
                    if (distance <= leg) return Vector3.Lerp(route[i - 1], route[i], leg > 0f ? distance / leg : 0f);
                    distance -= leg;
                }
                return route[route.Count - 1];
            }

            // White and black pawns (the game's Pawn Rush pawn, its mesh as modelled) round each route spot.
            // The six white pieces in a row in a plain grey studio far from every section (a floor, a wall behind), in
            // the round's own sun and sky light, seen from the front: the character's look as the design sheet shows it.
            // Heights as a set's.
            GameObject Lineup(List<Spot> spots, string name)
            {
                var root = new GameObject("Stand-in lineup studio (pictures only)");
                var centre = new Vector3(3000f, 0f, 3000f);
                var grey = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Studio grey" };
                grey.SetColor("_BaseColor", new Color(.6f, .62f, .66f));
                grey.SetFloat("_Smoothness", .15f);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.transform.SetParent(root.transform, false);
                floor.transform.position = centre;
                floor.transform.localScale = new Vector3(6f, 1f, 6f);
                floor.GetComponent<Renderer>().sharedMaterial = grey;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
                wall.transform.SetParent(root.transform, false);
                wall.transform.SetPositionAndRotation(centre + new Vector3(0f, 6f, -5f), Quaternion.Euler(0f, 180f, 0f));
                wall.transform.localScale = new Vector3(40f, 12f, 1f);
                wall.GetComponent<Renderer>().sharedMaterial = grey;
                // As the design sheet: pawn, rook, knight, bishop from the left.
                string[] names = { "Pawn", "Rook", "Knight", "Bishop" };
                float[] heights = { 1f, 1.2f, 1.3f, 1.4f };
                for (int i = 0; i < names.Length; i++)
                {
                    var prefab = Resources.Load<GameObject>("PieceModels/PawnRush/" + names[i]);
                    var skin = prefab != null ? prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
                    if (skin == null || skin.sharedMesh == null) continue;
                    var mesh = skin.sharedMesh;
                    var size = mesh.bounds.size;
                    var upright = size.z > size.y * 1.3f ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
                    float scale = heights[i] / Mathf.Max(.01f, upright == Quaternion.identity ? size.y : size.z);
                    var foot = centre + new Vector3((1.5f - i) * 1.45f, 0f, 0f);
                    var go = new GameObject(names[i]);
                    go.transform.SetParent(root.transform, false);
                    // Facing the camera (on +z), so the round's sun, coming from +z, lights their fronts.
                    go.transform.SetPositionAndRotation(foot, Quaternion.LookRotation(Vector3.forward, Vector3.up) * upright);
                    go.transform.localScale = Vector3.one * scale;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterials = skin.sharedMaterials;
                    var bb = r.bounds;
                    go.transform.position += Vector3.up * (foot.y - bb.min.y);
                }
                var look = centre + Vector3.up * .75f;
                spots.Add(new Spot { name = $"{Short(name)}_lu", eye = centre + new Vector3(0f, 1.05f, 5.4f), target = look, fov = 36f });
                return root;
            }

            GameObject Pawns(List<Spot> spots)
            {
                var prefab = Resources.Load<GameObject>("PieceModels/PawnRush/Pawn");
                var skin = prefab != null ? prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
                var black = Resources.Load<Material>("PieceModels/Black/Pawn");
                if (skin == null || skin.sharedMesh == null) return null;
                var mesh = skin.sharedMesh;
                var white = skin.sharedMaterials;
                var size = mesh.bounds.size;
                var upright = size.z > size.y * 1.3f ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
                float scale = 1f / Mathf.Max(.01f, upright == Quaternion.identity ? size.y : size.z);
                var root = new GameObject("Stand-in pawns (pictures only)");
                foreach (var s in spots)
                {
                    if (s.way == Vector3.zero) continue;
                    var side = Vector3.Cross(Vector3.up, s.way).normalized;
                    var places = new[] { (s.at + side * .3f + s.way * .2f, false), (s.at - side * 1.1f + s.way * 1.8f, true), (s.at + side * 1.3f + s.way * 3.2f, false) };
                    foreach (var (p, isBlack) in places)
                    {
                        var foot = Ground(p);
                        var go = new GameObject("Pawn");
                        go.transform.SetParent(root.transform, false);
                        go.transform.SetPositionAndRotation(foot, Quaternion.LookRotation(s.way, Vector3.up) * upright);
                        go.transform.localScale = Vector3.one * scale;
                        go.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var r = go.AddComponent<MeshRenderer>();
                        r.sharedMaterials = isBlack && black != null ? Enumerable.Repeat(black, white.Length).ToArray() : white;
                        // Stand on the floor: the mesh's lowest point at the foot.
                        var b = r.bounds;
                        go.transform.position += Vector3.up * (foot.y - b.min.y);
                    }
                }
                return root;
            }

            sealed class FloorEntry
            {
                public int pixels;
                public float distance;
                public string materials, details;
                public readonly HashSet<string> sections = new HashSet<string>();
            }

            readonly Dictionary<string, FloorEntry> allFloors = new Dictionary<string, FloorEntry>();
            int allFloorPixels, allScreenPixels, censused;

            // What floor the game's camera sees. The view is drawn once with every material slot of every mesh in a flat
            // colour of its own (an id: unlit, no fog, no post, no shafts) and read back, so each pixel names the
            // material actually drawn there (art meshes over grey-box colliders included); a ray through the pixel says
            // whether the surface there faces up (floor) and how far it is.
            const int CensusW = 192, CensusH = 108;
            RenderTexture idTarget;
            Texture2D idRead;
            Shader idShader;
            readonly Dictionary<Material, Material> idMaterialOf = new Dictionary<Material, Material>();
            readonly List<Material> idOriginals = new List<Material>();

            Material IdMaterial(Material original)
            {
                if (original == null) return null;
                if (idMaterialOf.TryGetValue(original, out var id)) return id;
                idOriginals.Add(original);
                int n = idOriginals.Count;   // 1-based; 0 is the background
                id = new Material(idShader) { name = "id " + n, hideFlags = HideFlags.DontSave };
                // Sixteen levels a channel, 13 apart and none darker than 40: an 8-bit target, linear or sRGB, keeps them.
                id.SetColor("_BaseColor", new Color(Level(n % 16) / 255f, Level(n / 16 % 16) / 255f, Level(n / 256 % 16) / 255f, 1f));
                idMaterialOf[original] = id;
                return id;
            }

            static int Level(int k) => 40 + 13 * k;

            // The id a pixel's colour encodes: 0 the background, -1 a colour no id has (a sprite, text, particles).
            static int IdOf(Color32 c)
            {
                if (c.r < 20 && c.g < 20 && c.b < 20) return 0;
                int K(byte v)
                {
                    int k = Mathf.RoundToInt((v - 40) / 13f);
                    return k >= 0 && k <= 15 && Mathf.Abs(v - Level(k)) <= 5 ? k : -1;
                }
                int r = K(c.r), g = K(c.g), b = K(c.b);
                return r < 0 || g < 0 || b < 0 ? -1 : r + 16 * g + 256 * b;
            }

            void FloorCensus(GameObject section, Dictionary<string, FloorEntry> table, ref int floorPixels, ref int allPixels, List<float> distances)
            {
                if (idShader == null) idShader = Shader.Find("Universal Render Pipeline/Unlit");
                // Ids afresh for every view (4095 at most).
                foreach (var old in idMaterialOf.Values) if (old != null) Destroy(old);
                idMaterialOf.Clear();
                idOriginals.Clear();
                if (idTarget == null)
                {
                    idTarget = new RenderTexture(CensusW, CensusH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                    idTarget.Create();
                    idRead = new Texture2D(CensusW, CensusH, TextureFormat.RGBA32, false);
                }
                // Every mesh's materials swapped for their ids, its property block (a tint a script sets) lifted, both
                // put back after.
                var swapped = new List<(Renderer, Material[], MaterialPropertyBlock)>();
                foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                    var mats = r.sharedMaterials;
                    var ids = new Material[mats.Length];
                    for (int k = 0; k < mats.Length; k++) ids[k] = IdMaterial(mats[k]);
                    MaterialPropertyBlock block = null;
                    if (r.HasPropertyBlock())
                    {
                        block = new MaterialPropertyBlock();
                        r.GetPropertyBlock(block);
                        r.SetPropertyBlock(null);
                    }
                    swapped.Add((r, mats, block));
                    r.sharedMaterials = ids;
                }
                var data = cam.GetUniversalAdditionalCameraData();
                bool post0 = data.renderPostProcessing, hdr0 = cam.allowHDR, fog0 = RenderSettings.fog;
                var aa0 = data.antialiasing;
                var clear0 = cam.clearFlags;
                var background0 = cam.backgroundColor;
                var capture0 = GameShafts.CaptureCamera;
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                cam.allowHDR = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                RenderSettings.fog = false;
                GameShafts.CaptureCamera = null;
                Color32[] pixels;
                try
                {
                    Draw(idTarget);
                    var active = RenderTexture.active;
                    RenderTexture.active = idTarget;
                    idRead.ReadPixels(new Rect(0f, 0f, CensusW, CensusH), 0, 0, false);
                    idRead.Apply(false);
                    RenderTexture.active = active;
                    pixels = idRead.GetPixels32();
                }
                finally
                {
                    foreach (var (r, mats, block) in swapped)
                    {
                        if (r == null) continue;
                        r.sharedMaterials = mats;
                        if (block != null) r.SetPropertyBlock(block);
                    }
                    data.renderPostProcessing = post0;
                    data.antialiasing = aa0;
                    cam.allowHDR = hdr0;
                    cam.clearFlags = clear0;
                    cam.backgroundColor = background0;
                    RenderSettings.fog = fog0;
                    GameShafts.CaptureCamera = capture0;
                }
                for (int y = 0; y < CensusH; y++)
                    for (int x = 0; x < CensusW; x++)
                    {
                        allPixels++;
                        allScreenPixels++;
                        var ray = cam.ViewportPointToRay(new Vector3((x + .5f) / CensusW, (y + .5f) / CensusH, 0f));
                        if (!Physics.Raycast(ray, out var hit, 800f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        if (hit.normal.y < .6f) continue;
                        floorPixels++;
                        allFloorPixels++;
                        distances.Add(hit.distance);
                        int id = IdOf(pixels[y * CensusW + x]);
                        string key, details = "";
                        if (id >= 1 && id <= idOriginals.Count && idOriginals[id - 1] != null)
                        {
                            var m = idOriginals[id - 1];
                            key = $"{m.name} [{(m.shader != null ? m.shader.name : "?")}]";
                            details = Textures(m);
                        }
                        else key = id == 0 ? "(nothing drawn)" : id < 0 ? "(not a mesh: sprite, text, particles)" : "(unknown id)";
                        foreach (var t in new[] { table, allFloors })
                        {
                            if (!t.TryGetValue(key, out var e)) t[key] = e = new FloorEntry { materials = key, details = details };
                            e.pixels++;
                            e.distance += hit.distance;
                            e.sections.Add(section.name);
                        }
                    }
                censused++;
            }

            // A material's textures: property=texture name, and its tiling.
            static string Textures(Material m)
            {
                var parts = new List<string>();
                foreach (var p in m.GetTexturePropertyNames())
                {
                    var t = m.GetTexture(p);
                    if (t == null) continue;
                    var s = m.GetTextureScale(p);
                    parts.Add($"{p}={t.name}{(s != Vector2.one ? $" x{s.x:0.##},{s.y:0.##}" : "")}");
                }
                return parts.Count > 0 ? string.Join(", ", parts) : "(no textures)";
            }

            void WriteFloors(string section, Dictionary<string, FloorEntry> table, int floorPixels, int allPixels, List<float> distances)
            {
                distances.Sort();
                var sb = new StringBuilder();
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}: floor is {1:0.0}% of the game camera's view ({2} of {3} rays); floor distance median {4:0.0} m, 90% within {5:0.0} m",
                    section, 100f * floorPixels / Mathf.Max(1, allPixels), floorPixels, allPixels,
                    distances.Count > 0 ? distances[distances.Count / 2] : 0f, distances.Count > 0 ? distances[Mathf.Min(distances.Count - 1, (int)(distances.Count * .9f))] : 0f));
                foreach (var e in table.Values.OrderByDescending(v => v.pixels))
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,5:0.0}% of floor  {1,5:0.0}% of view  {2,5:0.0} m  {3}\n        {4}",
                        100f * e.pixels / Mathf.Max(1, floorPixels), 100f * e.pixels / Mathf.Max(1, allPixels), e.distance / Mathf.Max(1, e.pixels), e.materials, e.details));
                File.WriteAllText(Path.Combine(folder, $"floors_{Short(section)}.txt"), sb.ToString());
            }

            void WriteAllFloors()
            {
                var sb = new StringBuilder();
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "every section: floor is {0:0.0}% of the game camera's view", 100f * allFloorPixels / Mathf.Max(1, allScreenPixels)));
                // By shader: how much of all the floor each shader draws.
                var byShader = new Dictionary<string, int>();
                foreach (var e in allFloors.Values)
                {
                    var shaders = string.Join(" + ", e.materials.Split('|').Select(s => s.Trim()).Select(s => { int a = s.LastIndexOf('['); return a >= 0 ? s.Substring(a) : s; }).Distinct());
                    byShader[shaders] = byShader.TryGetValue(shaders, out var n) ? n + e.pixels : e.pixels;
                }
                sb.AppendLine("by shader:");
                foreach (var kv in byShader.OrderByDescending(k => k.Value))
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,5:0.0}%  {1}", 100f * kv.Value / Mathf.Max(1, allFloorPixels), kv.Key));
                sb.AppendLine("by material:");
                foreach (var e in allFloors.Values.OrderByDescending(v => v.pixels).Take(80))
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,5:0.0}%  {1,5:0.0} m  {2}  ({3})\n          {4}",
                        100f * e.pixels / Mathf.Max(1, allFloorPixels), e.distance / Mathf.Max(1, e.pixels), e.materials, string.Join(", ", e.sections.Select(Short)), e.details));
                File.WriteAllText(Path.Combine(folder, $"floors_all_{tag}.txt"), sb.ToString());
            }

            // The texture a look draws into: one per MSAA sample count.
            RenderTexture Target(int samples)
            {
                if (targets.TryGetValue(samples, out var rt) && rt != null) return rt;
                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = samples };
                rt.Create();
                targets[samples] = rt;
                return rt;
            }

            // Waits until the GPU has drawn everything asked of it so far (a tiny read back of the picture).
            void Sync(RenderTexture rt)
            {
                Graphics.Blit(rt, tiny);
                var active = RenderTexture.active;
                RenderTexture.active = tiny;
                tinyRead.ReadPixels(new Rect(0f, 0f, 4f, 4f), 0, 0, false);
                RenderTexture.active = active;
            }

            static double Median(List<double> sorted) => sorted.Count > 0 ? sorted[sorted.Count / 2] : 0.0;
            static double P90(List<double> sorted) => sorted.Count > 0 ? sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * .9f))] : 0.0;

            // What one frame of this look costs at this spot: the camera drawn `frames` times, each waited out (CPU and
            // GPU one after the other, so more than a game frame that overlaps them; the differences between looks are
            // what to read). cpu: until the draw was handed to the GPU.
            IEnumerator Clock(RenderTexture rt, string section, string spot, string look, int samples, int frames)
            {
                var total = new List<double>(frames);
                var cpu = new List<double>(frames);
                Sync(rt);
                for (int i = 0; i < frames; i++)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    Draw(rt);
                    cpu.Add(watch.Elapsed.TotalMilliseconds);
                    Sync(rt);
                    total.Add(watch.Elapsed.TotalMilliseconds);
                    yield return null;
                }
                total.Sort();
                cpu.Sort();
                var pipe = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                times.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5},{6},{7:0.00},{8:0.00},{9:0.00}", section, spot,
                    look.Length > 0 ? look : "-", samples, pipe != null ? pipe.shadowDistance : 0f, pipe != null ? pipe.shadowCascadeCount : 0, frames,
                    Median(total), P90(total), Median(cpu)));
                timed++;
            }

            // The frame a new probe is laid in, as GameProbes lays one every 26 m of the camera's way (all six faces in
            // one frame): the frame times around it, with nothing else drawn, against the frames before.
            IEnumerator ProbeSpike(RenderTexture rt, string section, string look)
            {
                int rate0 = Application.targetFrameRate;
                Application.targetFrameRate = -1;
                var before = new List<double>();
                for (int i = 0; i < 6; i++)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    yield return null;
                    Sync(rt);
                    before.Add(watch.Elapsed.TotalMilliseconds);
                }
                var probe = new GameObject("Gfx shots timed probe").AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
                probe.resolution = 256;
                probe.hdr = true;
                probe.boxProjection = true;
                probe.size = new Vector3(64f, 36f, 64f);
                probe.nearClipPlane = .3f;
                probe.farClipPlane = 400f;
                probe.importance = 2;
                var forward = cam.transform.forward;
                forward.y = 0f;
                probe.transform.position = cam.transform.position + (forward.sqrMagnitude > 1e-4f ? forward.normalized * 10f : Vector3.zero);
                probe.RenderProbe();
                var after = new List<double>();
                for (int i = 0; i < 6; i++)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    yield return null;
                    Sync(rt);
                    after.Add(watch.Elapsed.TotalMilliseconds);
                }
                Destroy(probe.gameObject);
                Application.targetFrameRate = rate0;
                before.Sort();
                notes.AppendLine(string.Format(CultureInfo.InvariantCulture, "probe {0} {1}: frames before median {2:0.00} ms; after laying one: {3}", section,
                    look.Length > 0 ? look : "-", Median(before), string.Join(" ", after.Select(a => a.ToString("0.00", CultureInfo.InvariantCulture)))));
            }

            // The ambient light (the harmonics every unlit side gets) at the first spot: up, sideways, down.
            void Ambient(string section, string look)
            {
                var dirs = new[] { Vector3.up, Vector3.forward, Vector3.down };
                var cols = new Color[3];
                RenderSettings.ambientProbe.Evaluate(dirs, cols);
                string Rgb(Color c) => string.Format(CultureInfo.InvariantCulture, "({0:0.000} {1:0.000} {2:0.000})", c.r, c.g, c.b);
                notes.AppendLine($"ambient {section} {(look.Length > 0 ? look : "-")}: up {Rgb(cols[0])} side {Rgb(cols[1])} down {Rgb(cols[2])}, "
                    + $"mode {RenderSettings.ambientMode}, intensity {RenderSettings.ambientIntensity:0.00}, sky {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none")}"
                    + $"; camera at {cam.transform.position}; map light {GameCourses.Current} {GameCourses.Weight:0.00}");
            }

            // The sections in at once (as many as a round has, and more), then what the upgrade's look-rounds cost: each
            // runs over every renderer of the scene on a timer (GameSurfaces and GameGrass every 2 s, GameCharacters every 1 s).
            IEnumerator Scan(string which)
            {
                var names = which == "all"
                    ? Directory.GetFiles(PrefabFolder, "*.prefab").Select(p => Path.GetFileNameWithoutExtension(p)).ToList()
                    : which.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                var made = new List<GameObject>();
                Time.timeScale = 1f;
                foreach (var n in names)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + n + ".prefab");
                    if (prefab != null) made.Add(Instantiate(prefab, new Vector3(1200f * (made.Count + 1), 0f, 0f), Quaternion.identity));
                }
                for (int i = 0; i < 60; i++) yield return null;
                Time.timeScale = 0f;
                var sb = new StringBuilder();
                int renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                int meshRenderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length;
                sb.AppendLine($"scan: {made.Count} sections in at once, {renderers} renderers ({meshRenderers} active mesh renderers)");
                Measure(sb, "GameSurfaces.Dress (every 2 s)", () => GameSurfaces.Dress(GameSurfaces.LitOn));
                Measure(sb, "GameGrass.Grow (every 2 s)", () => GameGrass.Grow());
                Measure(sb, "GameCharacters.Sweep (every 1 s)", GameCharacters.Sweep);
                Measure(sb, "  of which FindObjectsByType<MeshRenderer>(Include)", () => FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None));
                Measure(sb, "  of which FindObjectsByType<SkinnedMeshRenderer>(Include)", () => FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None));
                Measure(sb, "GfxSections.Find (every 2 s)", GfxSections.Find);
                Measure(sb, "  of which FindObjectsByType<PawnRushSection>", () => FindObjectsByType<PawnRushSection>(FindObjectsSortMode.None));
                File.WriteAllText(Path.Combine(folder, $"scan_{tag}.txt"), sb.ToString());
                notes.Append(sb);
                foreach (var go in made) Destroy(go);
                for (int i = 0; i < 4; i++) yield return null;
            }

            static void Measure(StringBuilder sb, string what, System.Action act)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                long a0 = System.GC.GetAllocatedBytesForCurrentThread();
                act();
                double first = watch.Elapsed.TotalMilliseconds;
                long a1 = System.GC.GetAllocatedBytesForCurrentThread();
                var list = new List<double>();
                for (int i = 0; i < 10; i++)
                {
                    watch.Restart();
                    act();
                    list.Add(watch.Elapsed.TotalMilliseconds);
                }
                long a2 = System.GC.GetAllocatedBytesForCurrentThread();
                list.Sort();
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}: first {1:0.00} ms ({2} KB), then median {3:0.00} ms, max {4:0.00} ms, {5} KB a call",
                    what, first, (a1 - a0) / 1024, Median(list), list[list.Count - 1], (a2 - a1) / 10 / 1024));
            }

            void Draw(RenderTexture target)
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            }

            void Capture(RenderTexture target, string name)
            {
                try
                {
                    Draw(target);
                    var picture = new Texture2D(width, height, TextureFormat.RGB24, false);
                    var active = RenderTexture.active;
                    RenderTexture.active = target;
                    picture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                    picture.Apply();
                    RenderTexture.active = active;
                    File.WriteAllBytes(Path.Combine(folder, name + ".png"), picture.EncodeToPNG());
                    Destroy(picture);
                    pictures++;
                }
                catch (System.Exception e)
                {
                    failures++;
                    notes.AppendLine($"FAIL {name}: {e.Message}");
                }
            }
        }
    }
}
#endif
