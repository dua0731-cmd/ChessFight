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
        const string TuneKey = "GfxShots.Tune", SpotsKey = "GfxShots.Spots", MsaaKey = "GfxShots.Msaa";
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
            // -gfxSpots 1,0wide: only these spots of each section.
            SessionState.SetString(SpotsKey, Arg("-gfxSpots") ?? "");
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
        }

        sealed class Runner : MonoBehaviour
        {
            readonly StringBuilder notes = new StringBuilder();
            Camera cam;
            int width, height, pictures, failures;
            string folder, tag;

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
                    // Awake and Start dress the section; the first frames settle what moves.
                    for (int i = 0; i < 40; i++) yield return null;
                    Time.timeScale = 0f;
                    Inventory(section, name);
                    // The URP Lit materials on Gfx Lit now (GameSurfaces' watch does it every two seconds anyway).
                    GameSurfaces.Dress(GameSurfaces.LitOn);
                    GameGrass.Grow();
                    var spots = Spots(section, name);
                    var pawns = Pawns(spots);
                    string only = SessionState.GetString(SpotsKey, "");
                    var tunes = Tunes(SessionState.GetString(TuneKey, ""));
                    foreach (var spot in spots)
                    {
                        if (only.Length > 0 && !only.Split(',').Any(s => spot.name.EndsWith("_" + s.Trim()))) continue;
                        cam.transform.SetPositionAndRotation(spot.eye, Quaternion.LookRotation(spot.target - spot.eye, Vector3.up));
                        cam.fieldOfView = spot.fov;
                        foreach (var (tuneName, settings) in tunes)
                        {
                            Look(settings);
                            Reflect();
                            // The place's own reflections where this picture is (GameProbes; probes=0 to compare).
                            GameProbes.Now(cam);
                            // A few frames at the spot: the zone lights, the shadow cascades, the exposure settle.
                            for (int i = 0; i < 8; i++)
                            {
                                Draw(target);
                                yield return null;
                            }
                            Capture(target, tuneName.Length > 0 ? $"{spot.name}_{tag}_{tuneName}" : $"{spot.name}_{tag}");
                        }
                    }
                    var sun = ZoneLight.Sun != null ? ZoneLight.Sun : RenderSettings.sun;
                    notes.AppendLine($"{name}: {spots.Count} pictures; sun {(sun != null ? $"{sun.intensity:0.00} ({sun.color.r:0.00}, {sun.color.g:0.00}, {sun.color.b:0.00}) {sun.transform.eulerAngles}" : "none")}, "
                        + $"sky {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none")}, ambient {RenderSettings.ambientMode}, fog {RenderSettings.fog} {RenderSettings.fogMode}");
                    if (pawns != null) Destroy(pawns);
                    Destroy(section);
                    yield return null;
                    yield return null;
                }
                // Everything a look changed back as the run found it (the renderer asset above all).
                Look(new Dictionary<string, string>());
                var ssao = Ssao();
                if (ssao != null) notes.AppendLine($"ssao: intensity {Field(ssao, "Intensity"):0.00}, radius {Field(ssao, "Radius"):0.00}, direct {Field(ssao, "DirectLightingStrength"):0.00}");
                var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (pipeline != null)
                    notes.AppendLine($"pipeline {pipeline.name}: HDR {pipeline.supportsHDR}, shadows {pipeline.shadowDistance} m in {pipeline.shadowCascadeCount}, "
                        + $"grading {pipeline.colorGradingMode}, MSAA {pipeline.msaaSampleCount}");
                notes.AppendLine($"pictures {pictures}, failed {failures}");
                File.WriteAllText(Path.Combine(folder, $"notes_{tag}.txt"), notes.ToString());
                Debug.Log("[GfxShots]\n" + notes);
                ZoneLight.Instant = false;
                Time.timeScale = 1f;
                target.Release();
                if (pipe != null) pipe.msaaSampleCount = msaa0;
                SessionState.SetInt(CodeKey, failures == 0 && pictures > 0 ? 0 : 1);
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
                GameCharacters.Ambient = F("cama", GameCharacters.Ambient);
                GameCharacters.Gloss = F("camg", GameCharacters.Gloss);
                GameCharacters.Rim = F("camr", GameCharacters.Rim);
                GameCharacters.Reflection = F("camw", GameCharacters.Reflection);
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
                // The scene's haze (linear fog): distances and colour.
                RenderSettings.fogStartDistance = F("fogs", RenderSettings.fogStartDistance);
                RenderSettings.fogEndDistance = F("foge", RenderSettings.fogEndDistance);
                var fog = RenderSettings.fogColor;
                RenderSettings.fogColor = new Color(F("fogr", fog.r), F("fogg", fog.g), F("fogb", fog.b), 1f);
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
