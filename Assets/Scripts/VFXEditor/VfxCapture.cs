#if CF_URP
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChessFight.VFX.Editor
{
    /// <summary>
    /// Frame captures of one effect for checking it against its reference (R172, 10-27): the effect prefab is put into the
    /// capture scene (Assets/VFX/Scenes/VFX_Capture.unity, dark, opened as a preview scene so the open scene is not touched),
    /// set to each capture time with <see cref="VfxCue.Seek"/> and rendered at 1280 x 720 through the game's post-processing
    /// (the pipeline's GameLook profile: ACES, bloom 1.2 / 0.3, plus the scene's soft grade as GameLook puts it on every
    /// scene in Play), so a capture looks as the Game view does. Frames and a contact sheet of them go to
    /// VFXCaptures/&lt;target&gt;/&lt;layer&gt;/ at the project root (outside Assets, not in git). Menu ChessFight/VFX/Capture;
    /// <see cref="Capture"/> is the same from code (the Unity MCP). URP only: CF_URP comes from the asmdef's versionDefines,
    /// so the compile checks that build without URP (Tools/Test-NetworkCompile.ps1, run-tests-linux.sh) leave this file out.
    /// </summary>
    public static class VfxCapture
    {
        public enum Tone { Game, ACES, Neutral, None }

        public const int Width = 1280, Height = 720;
        public const string ScenePath = VfxAssets.Root + "/Scenes/VFX_Capture.unity";
        const string LookPath = VfxAssets.Root + "/Settings/VFX_Capture_Look.asset";
        const string FloorMaterial = VfxAssets.Root + "/Materials/M_CaptureFloor.mat";
        const string ChartPrefab = VfxAssets.Root + "/Prefabs/Common/00_ColorChart.prefab";
        const string ChartMaterial = VfxAssets.Root + "/Materials/M_ColorChart.mat";
        const int SheetCell = 640, SheetGap = 6, SheetColumns = 3;
        static readonly Color Backdrop = new Color(.015f, .016f, .02f);

        public static string CaptureRoot => Path.Combine(Path.GetDirectoryName(Application.dataPath), "VFXCaptures");

        // ---------------------------------------------------------------- menus

        [MenuItem("ChessFight/VFX/Capture/Window")]
        static void OpenWindow() => EditorWindow.GetWindow<VfxCaptureWindow>("VFX Capture");

        [MenuItem("ChessFight/VFX/Capture/Build Capture Scene")]
        public static void BuildScene()
        {
            if (EditorApplication.isPlaying) return;
            VfxAssets.Folder(VfxAssets.Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                // The camera: about where the sword fight's third-person camera stands, the effect's start at the origin.
                var camGo = new GameObject("Capture Camera");
                SceneManager.MoveGameObjectToScene(camGo, scene);
                camGo.transform.position = new Vector3(0f, 2.2f, -3.6f);
                camGo.transform.LookAt(new Vector3(0f, .4f, 0f));
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Backdrop;
                cam.fieldOfView = 40f;
                cam.nearClipPlane = .05f;
                cam.farClipPlane = 200f;
                cam.allowHDR = true;
                var data = camGo.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;

                // A dark floor for effects on the ground, unlit so the scene needs no light.
                var floor = new GameObject("Floor");
                SceneManager.MoveGameObjectToScene(floor, scene);
                floor.transform.localScale = new Vector3(14f, 1f, 14f);
                floor.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(VfxAssets.Root + "/Meshes/VFX_FloorQuad.asset")
                    ?? VfxAssets.SaveMesh(VfxAssets.Root + "/Meshes/VFX_FloorQuad.asset", VfxAssets.FloorQuad());
                var mat = VfxAssets.Material(FloorMaterial, "Universal Render Pipeline/Unlit", out bool made);
                if (made) mat.SetColor("_BaseColor", new Color(.03f, .031f, .035f));
                EditorUtility.SetDirty(mat);
                var view = floor.AddComponent<MeshRenderer>();
                view.sharedMaterial = mat;
                view.shadowCastingMode = ShadowCastingMode.Off;

                // The soft grade GameLook puts over every scene but the intro in Play (contrast 4, saturation 5, vignette 0.08).
                var lookGo = new GameObject("Game Look (soft)");
                SceneManager.MoveGameObjectToScene(lookGo, scene);
                var volume = lookGo.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10f;
                volume.sharedProfile = Look();

                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[VFX] Capture scene built: " + ScenePath);
        }

        [MenuItem("ChessFight/VFX/Capture/Build Color Chart")]
        public static void BuildChart()
        {
            if (EditorApplication.isPlaying) return;
            var mat = VfxAssets.Material(ChartMaterial, "ChessFight/VFX/Color Chart", out _);
            EditorUtility.SetDirty(mat);
            var root = new GameObject("00_ColorChart");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Chest;
            cue.length = 1f;
            cue.captureTimes = new[] { 0f };
            var chart = new GameObject("L1 Chart");
            chart.transform.SetParent(root.transform, false);
            // On the capture camera's axis 3.2 m out and square to it (it looks down 26.6 degrees), clear of the floor.
            chart.transform.localPosition = new Vector3(0f, .77f, -.74f);
            chart.transform.localRotation = Quaternion.Euler(26.57f, 0f, 0f);
            chart.transform.localScale = new Vector3(3.6f, 1.6f, 1f);
            chart.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var view = chart.AddComponent<MeshRenderer>();
            view.sharedMaterial = mat;
            view.shadowCastingMode = ShadowCastingMode.Off;
            VfxAssets.Prefab(ChartPrefab, root);
            AssetDatabase.SaveAssets();
            Debug.Log("[VFX] Color chart built: " + ChartPrefab);
        }

        /// <summary>
        /// The chart with the game's look (ACES) and with only its tonemapping swapped for Neutral and for none, and the
        /// three stacked in one image (1 ACES, 2 Neutral, 3 none): which tonemapping to keep is the user's call.
        /// </summary>
        [MenuItem("ChessFight/VFX/Capture/Color Chart (ACES, Neutral, None)")]
        public static string CaptureChart()
        {
            BuildChart();
            var tones = new[] { Tone.Game, Tone.Neutral, Tone.None };
            var shots = new List<Texture2D>();
            try
            {
                foreach (var tone in tones)
                {
                    string dir = Capture(ChartPrefab, "color_chart", "L1", tone, new[] { 0f });
                    shots.Add(Load(Path.Combine(dir, FrameName(0f))));
                }
                string path = Path.Combine(CaptureRoot, "color_chart", "compare_aces_neutral_none.png");
                var sheet = Sheet(shots, new[] { "1", "2", "3" }, 1);
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
                Debug.Log("[VFX] Color chart compared (top to bottom: 1 game ACES, 2 Neutral, 3 none): " + path);
                return path;
            }
            finally
            {
                foreach (var shot in shots) Object.DestroyImmediate(shot);
            }
        }

        static bool NotPlaying() => !EditorApplication.isPlaying;
        [MenuItem("ChessFight/VFX/Capture/Build Capture Scene", true)] static bool CanBuildScene() => NotPlaying();
        [MenuItem("ChessFight/VFX/Capture/Build Color Chart", true)] static bool CanBuildChart() => NotPlaying();
        [MenuItem("ChessFight/VFX/Capture/Color Chart (ACES, Neutral, None)", true)] static bool CanCaptureChart() => NotPlaying();

        // ---------------------------------------------------------------- capture

        /// <summary>
        /// Captures the effect prefab at <paramref name="prefabPath"/> at each of <paramref name="times"/> (null: the cue's
        /// capture times) into VFXCaptures/<paramref name="target"/>/<paramref name="layer"/>/ (a suffix for a tone other
        /// than the game's), one PNG a time and sheet.png of them all, replacing the last capture there. Edit mode only.
        /// <paramref name="turn"/> turns the effect that many degrees about the vertical, to see it from the side as a
        /// side-on reference shows it (0: from behind, as the player sees it; 90: its +Z to the right). <paramref name="eye"/>
        /// and <paramref name="look"/>, when given, put the camera there and turn it to that point instead (R181: an effect
        /// that crosses the arena seen from above, as its reference is). Returns the folder.
        /// </summary>
        public static string Capture(string prefabPath, string target, string layer, Tone tone = Tone.Game, float[] times = null, float turn = 0f,
            Vector3? eye = null, Vector3? look = null)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first: captures are taken in edit mode.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new ArgumentException("No prefab at " + prefabPath);
            if (!File.Exists(ScenePath)) BuildScene();

            string dir = Path.Combine(CaptureRoot, Safe(target), Safe(layer) + (tone == Tone.Game ? "" : "_" + tone.ToString().ToLowerInvariant()));
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "*.png")) File.Delete(old);

            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            Camera cam = null;
            RenderTexture rt = null;
            VolumeProfile toneProfile = null;
            string note = "";
            var frames = new List<Texture2D>();
            try
            {
                foreach (var go in scene.GetRootGameObjects())
                    if (cam == null) cam = go.GetComponentInChildren<Camera>(true);
                if (cam == null) throw new InvalidOperationException("No camera in " + ScenePath);
                cam.scene = scene;
                // Somewhere else, when asked (R181); the preview scene is a copy, so the saved camera stays where it is.
                if (eye.HasValue) cam.transform.position = eye.Value;
                if (eye.HasValue || look.HasValue) cam.transform.LookAt(look ?? new Vector3(0f, .4f, 0f));

                var fx = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                fx.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var cue = fx.GetComponent<VfxCue>();
                if (cue == null) cue = fx.AddComponent<VfxCue>();
                // An effect that plays at a chest is lifted off the floor to about where it would be (R178).
                fx.transform.position = Vector3.up * cue.captureHeight;
                // Turned to be seen from the side, when asked (R178).
                fx.transform.rotation = Quaternion.Euler(0f, turn, 0f);
                // Parts that animate themselves but cannot be seeked stay as they were saved (usually off) in every frame.
                var unseekable = new List<string>();
                foreach (var part in fx.GetComponentsInChildren<MonoBehaviour>(true))
                    if (part != null && part.GetType().Namespace == typeof(VfxCue).Namespace && !(part is VfxCue) && !(part is IVfxSeekable))
                        unseekable.Add(part.name + " (" + part.GetType().Name + ")");
                if (unseekable.Count > 0)
                    Debug.LogWarning("[VFX] Not seekable, so missing or frozen in the capture of " + prefab.name + ": " + string.Join(", ", unseekable));
                note = unseekable.Count > 0 ? "not seekable: " + string.Join(", ", unseekable) + "\n" : "";
                if (times == null || times.Length == 0) times = cue.captureTimes != null && cue.captureTimes.Length > 0 ? cue.captureTimes : new[] { .05f, .15f, .3f, .6f, 1f };

                if (tone != Tone.Game)
                {
                    var toneGo = new GameObject("Tone");
                    SceneManager.MoveGameObjectToScene(toneGo, scene);
                    var volume = toneGo.AddComponent<Volume>();
                    volume.isGlobal = true;
                    volume.priority = 1000f;
                    toneProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                    toneProfile.hideFlags = HideFlags.DontSave;
                    var map = toneProfile.Add<Tonemapping>(false);
                    map.mode.Override(tone == Tone.ACES ? TonemappingMode.ACES : tone == Tone.Neutral ? TonemappingMode.Neutral : TonemappingMode.None);
                    volume.sharedProfile = toneProfile;
                }

                // A float target: URP sizes its camera colour buffer by the target's format, and an 8-bit one would clip
                // everything over 1 before bloom and tonemapping (the Game view's buffer is HDR). Read back as linear.
                rt = new RenderTexture(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGBHalf, 24) { sRGB = false, msaaSamples = 1 });
                cam.targetTexture = rt;
                foreach (float t in times)
                {
                    cue.Seek(t);
                    cam.Render();
                    var frame = Read(rt);
                    File.WriteAllBytes(Path.Combine(dir, FrameName(t)), frame.EncodeToPNG());
                    frames.Add(frame);
                }

                var labels = new string[times.Length];
                for (int i = 0; i < times.Length; i++) labels[i] = times[i].ToString("0.00", CultureInfo.InvariantCulture);
                var sheet = Sheet(frames, labels, Mathf.Min(SheetColumns, frames.Count));
                File.WriteAllBytes(Path.Combine(dir, "sheet.png"), sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
                File.WriteAllText(Path.Combine(dir, "capture.txt"),
                    "prefab: " + prefabPath + "\ntone: " + tone + (turn != 0f ? "\nturn: " + turn.ToString("0.#", CultureInfo.InvariantCulture) : "")
                    + (eye.HasValue || look.HasValue ? "\ncamera: " + cam.transform.position.ToString("0.##") + " looking at " + (look ?? new Vector3(0f, .4f, 0f)).ToString("0.##") : "") + "\ntimes: " + string.Join(", ", labels) + "\ntaken: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" + note);
            }
            finally
            {
                if (cam != null) cam.targetTexture = null;
                if (rt != null) Object.DestroyImmediate(rt);
                foreach (var frame in frames) Object.DestroyImmediate(frame);
                EditorSceneManager.ClosePreviewScene(scene);
                if (toneProfile != null) Object.DestroyImmediate(toneProfile);
            }
            Debug.Log("[VFX] Captured " + prefab.name + " (" + tone + ", " + times.Length + " frames): " + dir);
            return dir;
        }

        public static string FrameName(float t) => "t" + t.ToString("0.000", CultureInfo.InvariantCulture) + ".png";

        static string Safe(string name) => string.IsNullOrWhiteSpace(name) ? "unnamed" : string.Join("_", name.Trim().Split(Path.GetInvalidFileNameChars()));

        /// <summary>The look volume's profile: GameLook's soft grade for scenes past the intro.</summary>
        static VolumeProfile Look()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LookPath);
            if (profile != null) return profile;
            VfxAssets.Folder(VfxAssets.Root + "/Settings");
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, LookPath);
            var grade = profile.Add<ColorAdjustments>(false);
            grade.contrast.Override(4f);
            grade.saturation.Override(5f);
            var vignette = profile.Add<Vignette>(false);
            vignette.intensity.Override(.08f);
            foreach (var part in profile.components) AssetDatabase.AddObjectToAsset(part, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static Texture2D Read(RenderTexture rt)
        {
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            var linear = new Texture2D(rt.width, rt.height, TextureFormat.RGBAHalf, false, true);
            linear.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = was;
            // Post-processing has already tonemapped it: only the screen's sRGB encoding is left to do.
            var src = linear.GetPixels();
            var pixels = new Color32[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var c = src[i];
                pixels[i] = new Color32(Encode(c.r), Encode(c.g), Encode(c.b), 255);
            }
            Object.DestroyImmediate(linear);
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        static byte Encode(float linear) => (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.LinearToGammaSpace(Mathf.Max(0f, linear))) * 255f);

        static Texture2D Load(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            tex.LoadImage(File.ReadAllBytes(path));
            return tex;
        }

        // ---------------------------------------------------------------- contact sheet

        /// <summary>Frames at half size in a grid, left to right and down, each with its label (a time) in its corner.</summary>
        static Texture2D Sheet(List<Texture2D> frames, string[] labels, int columns)
        {
            int cellH = SheetCell * Height / Width;
            int rows = (frames.Count + columns - 1) / columns;
            int w = columns * SheetCell + (columns + 1) * SheetGap, h = rows * cellH + (rows + 1) * SheetGap;
            var pixels = new Color32[w * h];
            var gap = new Color32(40, 40, 44, 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = gap;
            for (int f = 0; f < frames.Count; f++)
            {
                int col = f % columns, row = f / columns;
                int x0 = SheetGap + col * (SheetCell + SheetGap);
                int y0 = h - (row + 1) * (cellH + SheetGap);   // rows go down the image; textures count from the bottom
                var src = frames[f];
                for (int y = 0; y < cellH; y++)
                    for (int x = 0; x < SheetCell; x++)
                        pixels[(y0 + y) * w + x0 + x] = src.GetPixelBilinear((x + .5f) / SheetCell, (y + .5f) / cellH);
                Label(pixels, w, x0 + 8, y0 + cellH - 8, f < labels.Length ? labels[f] : "");
            }
            var sheet = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            sheet.SetPixels32(pixels);
            sheet.Apply();
            return sheet;
        }

        /// <summary>3 x 5 pixel digits and a point, rows from the top.</summary>
        static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            { '0', new[] { "111", "101", "101", "101", "111" } }, { '1', new[] { "010", "110", "010", "010", "111" } },
            { '2', new[] { "111", "001", "111", "100", "111" } }, { '3', new[] { "111", "001", "111", "001", "111" } },
            { '4', new[] { "101", "101", "111", "001", "001" } }, { '5', new[] { "111", "100", "111", "001", "111" } },
            { '6', new[] { "111", "100", "111", "101", "111" } }, { '7', new[] { "111", "001", "001", "001", "001" } },
            { '8', new[] { "111", "101", "111", "101", "111" } }, { '9', new[] { "111", "101", "111", "001", "111" } },
            { '.', new[] { "000", "000", "000", "000", "010" } },
        };

        /// <summary>Writes <paramref name="text"/> white on a black box, its top left at (x, top), 4 pixels a dot.</summary>
        static void Label(Color32[] pixels, int w, int x, int top, string text)
        {
            const int Dot = 4, Pad = 4;
            int boxW = text.Length * 4 * Dot + Pad * 2 - Dot, boxH = 5 * Dot + Pad * 2;
            var black = new Color32(0, 0, 0, 255);
            var white = new Color32(255, 255, 255, 255);
            for (int y = 0; y < boxH; y++)
                for (int i = 0; i < boxW; i++)
                    pixels[(top - y) * w + x + i] = black;
            for (int c = 0; c < text.Length; c++)
            {
                if (!Glyphs.TryGetValue(text[c], out var glyph)) continue;
                for (int gy = 0; gy < 5; gy++)
                    for (int gx = 0; gx < 3; gx++)
                    {
                        if (glyph[gy][gx] != '1') continue;
                        for (int dy = 0; dy < Dot; dy++)
                            for (int dx = 0; dx < Dot; dx++)
                                pixels[(top - Pad - gy * Dot - dy) * w + x + Pad + c * 4 * Dot + gx * Dot + dx] = white;
                    }
            }
        }
    }

    /// <summary>The capture menu's window: which prefab, under what target and layer name, with which tone.</summary>
    public sealed class VfxCaptureWindow : EditorWindow
    {
        GameObject prefab;
        string target = "bishop", layer = "L1", times = "";
        VfxCapture.Tone tone = VfxCapture.Tone.Game;

        void OnEnable()
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EditorPrefs.GetString("VfxCapture.Prefab", ""));
            target = EditorPrefs.GetString("VfxCapture.Target", target);
            layer = EditorPrefs.GetString("VfxCapture.Layer", layer);
        }

        void OnGUI()
        {
            prefab = (GameObject)EditorGUILayout.ObjectField("Effect prefab", prefab, typeof(GameObject), false);
            target = EditorGUILayout.TextField("Target (folder)", target);
            layer = EditorGUILayout.TextField("Layer (folder)", layer);
            tone = (VfxCapture.Tone)EditorGUILayout.EnumPopup("Tone", tone);
            times = EditorGUILayout.TextField(new GUIContent("Times (s)", "Comma separated; empty uses the cue's capture times."), times);
            using (new EditorGUI.DisabledScope(prefab == null || EditorApplication.isPlaying))
                if (GUILayout.Button("Capture"))
                {
                    string path = AssetDatabase.GetAssetPath(prefab);
                    EditorPrefs.SetString("VfxCapture.Prefab", path);
                    EditorPrefs.SetString("VfxCapture.Target", target);
                    EditorPrefs.SetString("VfxCapture.Layer", layer);
                    string dir = VfxCapture.Capture(path, target, layer, tone, Parse(times));
                    EditorUtility.RevealInFinder(Path.Combine(dir, "sheet.png"));
                }
            if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("Stop Play mode to capture.", MessageType.Info);
        }

        static float[] Parse(string text)
        {
            var list = new List<float>();
            foreach (var part in text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out float t)) list.Add(t);
            return list.Count > 0 ? list.ToArray() : null;
        }
    }
}
#endif
