#if CF_URP
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ChessFight.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChessFight.PawnRush.Editor
{
    /// <summary>
    /// JY-gfx (2026-11-24): the sections' baked indirect light. JY: "너 왜 간접광 베이크를 안했던거야? 그거 하면 퀄리티 확
    /// 올라갈거 같은데" and, after the test, "넣자". A round places its sections as prefabs and they build much of their art at
    /// run time, so no scene bake reaches them. Each section is baked alone in a scene of its own under the game's morning
    /// (GameSky's sky, sun and haze), as the game built and dressed it (a frozen copy, GfxShots -gfxFreeze: its models, the
    /// upgrade's materials, its light plan, the corridors round it): every still part static, a second UV set made where a
    /// mesh has none (copies), the lights mixed, the indirect light baked (Progressive GPU, baked indirect). Bake keeps the
    /// result for the game (Resources/Gfx/Baked/&lt;section&gt;.asset, GfxBakedLight; GfxBaked puts it on in a round); Test only
    /// takes pictures from the game's camera spots with the bake and without it.
    ///   1. Unity -batchmode -projectPath copy -executeMethod ChessFight.PawnRush.Editor.GfxShots.Run -gfxFreeze
    ///          -gfxSections a,b,c -gfxSpots 1 -gfxOut folder          (Play: Assets/GfxBakeTest/Frozen/&lt;section&gt;_frozen.prefab)
    ///   2. Unity -batchmode -projectPath copy -executeMethod ChessFight.PawnRush.Editor.GfxBake.Bake
    ///          [-bakeSections a,b,c (default: every frozen one)] [-bakeTexels 10] [-bakeBounces 3] [-bakeSamples 512]
    ///          [-bakeOut folder (pictures with and without the bake, and notes)]
    /// </summary>
    public static class GfxBake
    {
        const string PrefabFolder = "Assets/Prefabs/PawnRushSections/";
        const string TestFolder = "Assets/GfxBakeTest";
        const string FrozenFolder = TestFolder + "/Frozen";
        const string OutFolder = "Assets/Resources/Gfx/Baked";
        const int Width = 1600, Height = 900;

        static string Arg(string name, string fallback)
        {
            var args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : fallback;
        }

        static float F(string name, float fallback) =>
            float.TryParse(Arg(name, null), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        [MenuItem("ChessFight/Graphics/Bake the sections' indirect light")]
        public static void Bake() => Main(keep: true);

        [MenuItem("ChessFight/Graphics/Bake test (pictures only)")]
        public static void Test() => Main(keep: false);

        static void Main(bool keep)
        {
            var notes = new StringBuilder();
            string folder = Arg("-bakeOut", Path.Combine(Directory.GetCurrentDirectory(), "Logs", "GfxBake"));
            Directory.CreateDirectory(folder);
            int failed = 0, done = 0;
            var names = (Arg("-bakeSections", null) ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            if (names.Count == 0 && AssetDatabase.IsValidFolder(FrozenFolder))
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { FrozenFolder }))
                    names.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)).Replace("_frozen", ""));
            if (names.Count == 0) names.Add(Arg("-bakeSection", "Late_MG_G_RoyalProcession"));
            foreach (var name in names)
            {
                try
                {
                    if (One(name, folder, keep, notes)) done++;
                    else failed++;
                }
                catch (System.Exception e)
                {
                    failed++;
                    notes.AppendLine($"FAIL {name}: {e}");
                    Debug.LogException(e);
                }
            }
            notes.AppendLine($"sections baked {done}, failed {failed}");
            File.WriteAllText(Path.Combine(folder, "bake_notes.txt"), notes.ToString());
            Debug.Log("[GfxBake]\n" + notes);
            if (Application.isBatchMode) EditorApplication.Exit(failed == 0 && done > 0 ? 0 : 1);
        }

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // One section: its scene, its bake, then what is kept (keep) and its pictures.
        static bool One(string name, string folder, bool keep, StringBuilder notes)
        {
            float texels = F("-bakeTexels", 10f);
            int bounces = Mathf.RoundToInt(F("-bakeBounces", 3f));
            int samples = Mathf.RoundToInt(F("-bakeSamples", 512f));
            var clock = System.Diagnostics.Stopwatch.StartNew();
            string key = GfxBaked.Key(name);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Folder(TestFolder);

            // The game's morning (GameSky's numbers): its sky, its sun, its haze.
            var source = Resources.Load<Material>("Gfx/GfxSky");
            if (source == null) { notes.AppendLine("FAIL no Gfx/GfxSky"); return false; }
            string skyPath = TestFolder + "/GfxSkyBake.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(source) { name = "Gfx sky (bake)" };
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetFloat("_Exposure", GameSky.SkyExposure);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = GameSky.AmbientIntensity;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.fog = GameSky.HazeOn;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = GameSky.HazeStart;
            RenderSettings.fogEndDistance = GameSky.HazeEnd;
            RenderSettings.fogColor = GameSky.HazeColor;
            var sunObject = new GameObject("Sun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = GameSky.SunColor;
            sun.intensity = GameSky.SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            sunObject.transform.rotation = Quaternion.LookRotation(GameSky.SunWay.normalized);
            RenderSettings.sun = sun;

            // The section as the game built and dressed it (frozen), or the bare prefab (with the corridors) for a test.
            GameObject section;
            GameObject[] roots;
            var frozen = AssetDatabase.LoadAssetAtPath<GameObject>(FrozenFolder + "/" + key + "_frozen.prefab");
            if (frozen != null)
            {
                var placedRoot = (GameObject)PrefabUtility.InstantiatePrefab(frozen, scene);
                var rush = placedRoot.GetComponentInChildren<PawnRushSection>(true);
                section = rush != null ? rush.gameObject : placedRoot;
                roots = new[] { placedRoot };
            }
            else
            {
                if (keep) { notes.AppendLine($"FAIL {name}: no frozen copy (GfxShots -gfxFreeze first)"); return false; }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab");
                if (prefab == null) { notes.AppendLine("FAIL no prefab " + name); return false; }
                section = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                section.name = name;
                GameObject joins = null;
                try { joins = GfxShots.Joins(section, name); }
                catch (System.Exception e) { notes.AppendLine("joins failed (baked without them): " + e.Message); }
                roots = new[] { section, joins };
            }

            // A section that lights itself is baked under its own light.
            OwnLight(section, sun, notes);

            // Every still part static and lit by the bake; a mesh with no second UV set gets one (a copy).
            var copies = new Dictionary<Mesh, Mesh>();
            var originalOf = new Dictionary<Mesh, Mesh>();
            int still = 0, moving = 0, unwrapped = 0, unreadable = 0;
            foreach (var root in roots)
            {
                if (root == null) continue;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    var filter = r.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    if (Moving(r, root.transform)) { moving++; continue; }
                    // See-through things (glass, beams, glows) neither hold nor block the baked light, nor do the upgrade's
                    // own shadowless pictures (the moon); its shadow-only lid does block.
                    bool clear = r.shadowCastingMode == ShadowCastingMode.Off && r.name.EndsWith("(JY-gfx)");
                    foreach (var m in r.sharedMaterials) if (m != null && m.renderQueue >= 2500) { clear = true; break; }
                    if (clear) continue;
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, GameObjectUtility.GetStaticEditorFlags(r.gameObject) | StaticEditorFlags.ContributeGI);
                    r.receiveGI = ReceiveGI.Lightmaps;
                    var mesh = filter.sharedMesh;
                    bool hasUv2;
                    try { hasUv2 = mesh.uv2 != null && mesh.uv2.Length == mesh.vertexCount; }
                    catch { unreadable++; continue; }
                    if (!hasUv2)
                    {
                        if (!copies.TryGetValue(mesh, out var copy))
                        {
                            copy = Object.Instantiate(mesh);
                            copy.name = mesh.name + " (uv2)";
                            Unwrapping.GenerateSecondaryUVSet(copy);
                            copies[mesh] = copy;
                            originalOf[copy] = mesh;
                            unwrapped++;
                        }
                        filter.sharedMesh = copy;
                    }
                    still++;
                }
            }
            int lights = 0;
            foreach (var root in roots)
            {
                if (root == null) continue;
                foreach (var l in root.GetComponentsInChildren<Light>(true))
                {
                    if (l == sun || !l.enabled || l.intensity <= 0f) continue;
                    l.lightmapBakeType = LightmapBakeType.Mixed;
                    lights++;
                }
            }
            notes.AppendLine($"{name} ({(frozen != null ? "frozen" : "bare prefab")}): {still} still parts static, {moving} moving left live, {unwrapped} meshes given a second UV set, {unreadable} unreadable, {lights} lights mixed; set up in {clock.Elapsed.TotalSeconds:0} s");

            // Baked indirect: the sky's and the lights' bounces in lightmaps, their direct light still live.
            var settings = new LightingSettings { name = "GfxBake" };
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
            settings.lightmapResolution = texels;
            settings.lightmapPadding = 2;
            settings.lightmapMaxSize = 2048;
            settings.maxBounces = bounces;
            settings.directSampleCount = 32;
            settings.indirectSampleCount = samples;
            settings.environmentSampleCount = samples;
            settings.lightmapCompression = LightmapCompression.HighQuality;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.filteringMode = LightingSettings.FilterMode.Auto;
            string settingsPath = TestFolder + "/GfxBake.lighting";
            AssetDatabase.DeleteAsset(settingsPath);
            AssetDatabase.CreateAsset(settings, settingsPath);
            Lightmapping.lightingSettings = settings;
            EditorSceneManager.SaveScene(scene, TestFolder + "/" + key + ".unity");
            clock.Restart();
            bool baked = Lightmapping.Bake();
            string how = $"{texels} texels a metre, {bounces} bounces, {samples} samples, {System.DateTime.Now:yyyy-MM-dd HH:mm}";
            notes.AppendLine($"  bake {(baked ? "done" : "FAILED")} in {clock.Elapsed.TotalSeconds:0} s: {LightmapSettings.lightmaps.Length} lightmaps, {how}");
            if (!baked) return false;

            if (keep) Keep(section, key, how, originalOf, notes);
            Pictures(section, folder, notes);
            return true;
        }

        // The Royal Observatory's dome sky as a night sky glows: only its stars and the Milky Way, not the dark between
        // (JY: "실제 밤하늘은 푸르수름하긴 하지만 별이 많은부분이 빛나고, 별이 없는부분은 빛이 안나잖아"). Stars: brighter than
        // their own few pixels round them; the band: brighter than its row's mean sky. Resources/Gfx/Glow/RO_DomeSky_Glow.png.
        [MenuItem("ChessFight/Graphics/Make the observatory dome's star glow")]
        public static void StarGlow()
        {
            const string From = "Assets/Art/RoyalObservatory/Textures/RO_DomeSky_Albedo.png", Dir = "Assets/Resources/Gfx/Glow";
            const string To = Dir + "/RO_DomeSky_Glow.png";
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            source.LoadImage(File.ReadAllBytes(From));
            int w = source.width, h = source.height;
            var px = source.GetPixels32();
            var lum = new float[w * h];
            for (int i = 0; i < lum.Length; i++) lum[i] = (.2126f * px[i].r + .7152f * px[i].g + .0722f * px[i].b) / 255f;
            var near = Blur(lum, w, h, 6);
            var wide = Blur(lum, w, h, 20);
            var row = new float[h];
            for (int y = 0; y < h; y++)
            {
                float sum = 0f;
                for (int x = 0; x < w; x++) sum += lum[y * w + x];
                row[y] = sum / w;
            }
            var glow = new Color32[w * h];
            float lit = 0f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    float star = Mathf.Clamp01((lum[i] - near[i] - .03f) * 7f);
                    float band = Mathf.Clamp01((wide[i] - row[y] - .015f) * 6f) * .55f;
                    float m = Mathf.Max(star, band);
                    lit += m;
                    glow[i] = new Color32((byte)(px[i].r * m), (byte)(px[i].g * m), (byte)(px[i].b * m), 255);
                }
            var made = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            made.SetPixels32(glow);
            made.Apply();
            Folder(Dir);
            File.WriteAllBytes(To, made.EncodeToPNG());
            Object.DestroyImmediate(made);
            Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(To);
            var importer = (TextureImporter)AssetImporter.GetAtPath(To);
            var from = (TextureImporter)AssetImporter.GetAtPath(From);
            importer.sRGBTexture = true;
            importer.maxTextureSize = 4096;
            importer.wrapModeU = from != null ? from.wrapModeU : TextureWrapMode.Repeat;
            importer.wrapModeV = from != null ? from.wrapModeV : TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            Debug.Log($"[GfxBake] star glow {w}x{h}: {100f * lit / lum.Length:0.0}% of the sky glows -> {To}");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // A box blur of radius r, wrapping across (a sky panorama) and clamped up and down.
        static float[] Blur(float[] src, int w, int h, int r)
        {
            var across = new float[src.Length];
            var result = new float[src.Length];
            float n = 2 * r + 1;
            for (int y = 0; y < h; y++)
            {
                float sum = 0f;
                for (int k = -r; k <= r; k++) sum += src[y * w + ((k % w) + w) % w];
                for (int x = 0; x < w; x++)
                {
                    across[y * w + x] = sum / n;
                    sum += src[y * w + (x + r + 1) % w] - src[y * w + ((x - r) % w + w) % w];
                }
            }
            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                for (int k = -r; k <= r; k++) sum += across[Mathf.Clamp(k, 0, h - 1) * w + x];
                for (int y = 0; y < h; y++)
                {
                    result[y * w + x] = sum / n;
                    sum += across[Mathf.Clamp(y + r + 1, 0, h - 1) * w + x] - across[Mathf.Clamp(y - r, 0, h - 1) * w + x];
                }
            }
            return result;
        }

        // The light of a section that lights itself, for its bake (read from its own component, by name: the editor does not
        // reference the gameplay code): the Royal Observatory's night (moon, ambient, haze; JY: "천문대는 밤 맞아"), and the
        // Royal Library's own sun and ambient when its map keeps them (GfxMapLights.Map.OwnLook).
        static void OwnLight(GameObject section, Light sun, StringBuilder notes)
        {
            const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            T Get<T>(object o, string field) => (T)o.GetType().GetField(field, Any).GetValue(o);
            foreach (var mb in section.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var type = mb.GetType();
                bool observatory = type.Name == "RoyalObservatoryLighting";
                bool library = type.Name == "RoyalLibraryLighting" && GfxMapLights.Has(section.name) && GfxMapLights.For(section.name, 3).OwnLook;
                if (!observatory && !library) continue;
                if (observatory)
                {
                    var way = (Vector3)type.GetMethod("MoonDirection", Any).Invoke(mb, null);
                    sun.transform.rotation = Quaternion.LookRotation(way);
                    sun.color = Get<Color>(mb, "moonColor");
                    sun.intensity = Get<float>(mb, "moonIntensity");
                    if (Get<bool>(mb, "haze"))
                    {
                        RenderSettings.fogColor = Get<Color>(mb, "hazeColor");
                        RenderSettings.fogStartDistance = Get<float>(mb, "hazeStart");
                        RenderSettings.fogEndDistance = Get<float>(mb, "hazeEnd");
                    }
                }
                else
                {
                    sun.transform.rotation = mb.transform.rotation * Quaternion.Euler(Get<float>(mb, "sunHeight"), Get<float>(mb, "sunYaw"), 0f);
                    sun.color = Get<Color>(mb, "sunColor");
                    sun.intensity = Get<float>(mb, "sunIntensity");
                }
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = Get<Color>(mb, "ambientSky");
                RenderSettings.ambientEquatorColor = Get<Color>(mb, "ambientEquator");
                RenderSettings.ambientGroundColor = Get<Color>(mb, "ambientGround");
                notes.AppendLine($"  baked under its own light ({type.Name}): sun {sun.intensity:0.00} at {sun.transform.eulerAngles}, ambient a gradient");
                return;
            }
        }

        // What moves in play keeps the live light: anything with a body, an animator or an animation over it, the pieces.
        static bool Moving(Renderer r, Transform root)
        {
            for (var t = r.transform; t != null && t != root.parent; t = t.parent)
            {
                if (t.GetComponent<Rigidbody>() != null || t.GetComponent<Animator>() != null || t.GetComponent<Animation>() != null) return true;
                string n = t.name;
                if (n.Contains("Rook") || n.Contains("Pawn") || n.Contains("Knight") || n.Contains("Bishop") || n.Contains("Queen") || n.StartsWith("Piece") || n.Contains(" piece")) return true;
                // Devices that turn about a pivot (the observatory's meteor rings and orbit tracks).
                if (n.StartsWith("Device_") || n == "pivot" || n.EndsWith(" pivot")) return true;
            }
            foreach (var m in r.sharedMaterials) if (m != null && m.name.StartsWith("M_Piece")) return true;
            return false;
        }

        // The bake as the game uses it: the lightmaps (copies), the meshes with their second UV set, each part's place.
        static void Keep(GameObject section, string key, string how, Dictionary<Mesh, Mesh> originalOf, StringBuilder notes)
        {
            Folder(OutFolder + "/" + key);
            string dataPath = OutFolder + "/" + key + ".asset";
            AssetDatabase.DeleteAsset(dataPath);
            // The bake's lightmaps are float EXRs of 30 MB and more each: kept as half floats, ZIP-compressed (the game's copy
            // is compressed on import, BC6H, whatever the file).
            var maps = new List<Texture2D>();
            for (int i = 0; i < LightmapSettings.lightmaps.Length; i++)
            {
                var t = LightmapSettings.lightmaps[i].lightmapColor;
                string from = AssetDatabase.GetAssetPath(t);
                string to = OutFolder + "/" + key + "/Lightmap-" + i + ".exr";
                AssetDatabase.DeleteAsset(to);
                var source = (TextureImporter)AssetImporter.GetAtPath(from);
                source.isReadable = true;
                source.textureCompression = TextureImporterCompression.Uncompressed;
                source.SaveAndReimport();
                var read = AssetDatabase.LoadAssetAtPath<Texture2D>(from);
                var half = new Texture2D(read.width, read.height, TextureFormat.RGBAHalf, false, true);
                half.SetPixels(read.GetPixels());
                half.Apply();
                File.WriteAllBytes(to, half.EncodeToEXR(Texture2D.EXRFlags.CompressZIP));
                Object.DestroyImmediate(half);
                AssetDatabase.ImportAsset(to);
                var importer = (TextureImporter)AssetImporter.GetAtPath(to);
                importer.textureType = TextureImporterType.Lightmap;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.isReadable = false;
                importer.SaveAndReimport();
                maps.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(to));
            }
            var data = ScriptableObject.CreateInstance<GfxBakedLight>();
            data.section = section.name;
            data.lightmaps = maps.ToArray();
            data.how = how;
            AssetDatabase.CreateAsset(data, dataPath);
            var parts = new List<GfxBakedLight.Part>();
            var kept = new HashSet<Mesh>();
            foreach (var r in section.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.lightmapIndex < 0 || r.lightmapIndex >= 65534) continue;
                var filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var mesh = filter.sharedMesh;
                bool copy = originalOf.TryGetValue(mesh, out var original);
                if (!copy) original = mesh;
                if (copy && kept.Add(mesh)) AssetDatabase.AddObjectToAsset(mesh, data);
                parts.Add(new GfxBakedLight.Part
                {
                    path = GfxBaked.PathOf(section.transform, r.transform),
                    mesh = original.name,
                    vertices = original.vertexCount,
                    baked = copy ? mesh : null,
                    lightmap = r.lightmapIndex,
                    scaleOffset = r.lightmapScaleOffset,
                });
            }
            data.parts = parts.ToArray();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            long bytes = 0;
            foreach (var t in maps) if (t != null) bytes += new FileInfo(AssetDatabase.GetAssetPath(t)).Length;
            notes.AppendLine($"  kept {dataPath}: {parts.Count} parts, {kept.Count} meshes with a second UV set, {maps.Count} lightmaps ({bytes / 1048576f:0.0} MB on disk)");
        }

        // Pictures from the game's camera spots with the bake and without it, at the game's exposure and one stop over.
        static void Pictures(GameObject section, string folder, StringBuilder notes)
        {
            var cameraObject = new GameObject("Camera");
            var cam = cameraObject.AddComponent<Camera>();
            cam.nearClipPlane = .1f;
            cam.farClipPlane = 3000f;
            cam.fieldOfView = 60f;
            cam.allowHDR = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.renderShadows = true;
            var volumeObject = new GameObject("Grade");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
            var adjust = profile.Add<ColorAdjustments>(true);
            adjust.contrast.Override(GameGrade.Contrast);
            adjust.saturation.Override(GameGrade.Saturation);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(.6f);
            bloom.threshold.Override(.95f);
            volume.sharedProfile = profile;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            target.Create();
            var spots = Spots(section, notes);
            var lightmapped = new List<(Renderer renderer, int index, Vector4 offset)>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (r.lightmapIndex >= 0 && r.lightmapIndex < 65534) lightmapped.Add((r, r.lightmapIndex, r.lightmapScaleOffset));
            foreach (var (look, live) in new[] { ("bake", false), ("live", true) })
            {
                foreach (var (r, index, offset) in lightmapped)
                {
                    r.lightmapIndex = live ? -1 : index;
                    r.lightmapScaleOffset = offset;
                }
                foreach (float stops in new[] { 0f, 1f })
                {
                    adjust.postExposure.Override(GameGrade.Exposure + stops);
                    foreach (var (spotName, eye, at) in spots)
                    {
                        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye, Vector3.up));
                        for (int i = 0; i < 3; i++) Draw(cam, target);
                        Save(target, Path.Combine(folder, $"{spotName}_{look}_e{stops:0}.png"));
                    }
                }
            }
            foreach (var (r, index, offset) in lightmapped)
            {
                r.lightmapIndex = index;
                r.lightmapScaleOffset = offset;
            }
            target.Release();
            notes.AppendLine($"  pictures: {spots.Count * 4}");
        }

        static void Draw(Camera cam, RenderTexture target)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else
            {
                cam.targetTexture = target;
                cam.Render();
                cam.targetTexture = null;
            }
        }

        static void Save(RenderTexture target, string path)
        {
            var picture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var active = RenderTexture.active;
            RenderTexture.active = target;
            picture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            picture.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(path, picture.EncodeToPNG());
            Object.DestroyImmediate(picture);
        }

        // The game's camera at 10, 40 and 72 % of white's route (as GfxShots places it: 6 m behind the pawn, 15 degrees
        // down, 0.6 m over its feet, 0.45 m over the right shoulder, pulled in before a wall).
        static List<(string name, Vector3 eye, Vector3 at)> Spots(GameObject section, StringBuilder notes)
        {
            var list = new List<(string, Vector3, Vector3)>();
            Physics.SyncTransforms();
            var path = section.GetComponentInChildren<ProgressPath>(true);
            var route = path != null ? path.World(0) : null;
            if (route == null || route.Count < 2)
            {
                var s = section.GetComponent<PawnRushSection>();
                if (s == null) { notes.AppendLine("  no route"); return list; }
                route = new List<Vector3> { s.EntryWorld.position, s.ExitWorld.position };
            }
            float length = 0f;
            for (int i = 1; i < route.Count; i++) length += Vector3.Distance(route[i - 1], route[i]);
            string shortName = GfxBaked.Key(section.name);
            var at = new[] { .1f, .4f, .72f };
            for (int i = 0; i < at.Length; i++)
            {
                var p = Ground(PointAt(route, at[i] * length));
                var a = PointAt(route, Mathf.Max(0f, at[i] * length - 3f));
                var b = PointAt(route, Mathf.Min(length, at[i] * length + 3f));
                var way = b - a;
                way.y = 0f;
                way = way.sqrMagnitude > 1e-4f ? way.normalized : section.transform.forward;
                var look = Quaternion.LookRotation(way, Vector3.up) * Quaternion.Euler(15f, 0f, 0f);
                var pivot = p + Vector3.up * .6f + Vector3.Cross(Vector3.up, way).normalized * .45f;
                float back = 6f;
                if (Physics.SphereCast(pivot, .2f, -(look * Vector3.forward), out var hit, back, ~0, QueryTriggerInteraction.Ignore))
                    back = Mathf.Max(1.5f, hit.distance - .05f);
                var eye = pivot - look * Vector3.forward * back;
                list.Add(($"{shortName}_{i + 1}", eye, eye + look * Vector3.forward * 10f));
            }
            return list;
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
    }
}
#endif
