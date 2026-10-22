using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.EditorTools
{
    /// <summary>
    /// Plays each listed scene for a few seconds and saves stills from its cameras, for comparing the look
    /// before and after a rendering change (the URP move). Menu: ChessFight/Tools/Capture Scenes (before|after).
    /// Stills land in Temp/Captures/urp/&lt;label&gt;/&lt;scene&gt;_&lt;n&gt;.png. Survives the domain reload on entering Play.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneCompareCapture
    {
        const string QueueKey = "ChessFight.SceneCompareCapture.Queue";
        const string LabelKey = "ChessFight.SceneCompareCapture.Label";
        static readonly float[] Moments = { 3f, 7f };

        public static readonly string[] Scenes =
        {
            "Assets/Scenes/Intro.unity",
            "Assets/Scenes/Lobby.unity",
            "Assets/Scenes/PawnRushVictory.unity",
            "Assets/Scenes/PawnRushLose.unity",
            "Assets/Scenes/PawnRush_Course01.unity",
            "Assets/Scenes/SwordFight.unity",
            "Assets/Scenes/SkillTest/PawnRush_SkillTest/PawnRush_SkillTest.unity",
            "Assets/Scenes/SkillTest/QueenOfTheHill_SkillTest/QueenOfTheHill_SkillTest.unity",
            "Assets/Scenes/SkillTest/SwordFight_SkillTest/SwordFight_SkillTest.unity",
        };

        static double startedAt;
        static int taken;

        static SceneCompareCapture()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (EditorApplication.isPlaying && Queue.Count > 0) Begin();
        }

        [MenuItem("ChessFight/Tools/Capture Scenes (before)")]
        static void CaptureBefore() => Run("before");

        [MenuItem("ChessFight/Tools/Capture Scenes (after)")]
        static void CaptureAfter() => Run("after");

        public static void Run(string label, params string[] scenes)
        {
            if (scenes == null || scenes.Length == 0) scenes = Scenes;
            SessionState.SetString(LabelKey, label);
            SessionState.SetString(QueueKey, string.Join("|", scenes));
            PlayNext();
        }

        public static bool Busy => Queue.Count > 0;

        static List<string> Queue
        {
            get
            {
                string q = SessionState.GetString(QueueKey, "");
                return q.Length == 0 ? new List<string>() : new List<string>(q.Split('|'));
            }
        }

        static void PlayNext()
        {
            var queue = Queue;
            if (queue.Count == 0) return;
            EditorSceneManager.OpenScene(queue[0]);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (Queue.Count == 0) return;
            if (change == PlayModeStateChange.EnteredPlayMode) Begin();
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                var queue = Queue;
                queue.RemoveAt(0);
                SessionState.SetString(QueueKey, string.Join("|", queue));
                if (queue.Count == 0)
                {
                    Debug.Log("[SceneCompareCapture] done");
                    return;
                }
                // Straight after leaving Play the editor ignores a new isPlaying; start the next one a moment later.
                double at = EditorApplication.timeSinceStartup + 1.0;
                EditorApplication.CallbackFunction wait = null;
                wait = () =>
                {
                    if (EditorApplication.timeSinceStartup < at || EditorApplication.isPlayingOrWillChangePlaymode) return;
                    EditorApplication.update -= wait;
                    PlayNext();
                };
                EditorApplication.update += wait;
            }
        }

        static void Begin()
        {
            startedAt = EditorApplication.timeSinceStartup;
            taken = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
            if (EditorApplication.timeSinceStartup - startedAt < Moments[taken]) return;
            Save(SessionState.GetString(LabelKey, "capture"), taken);
            if (++taken < Moments.Length) return;
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
        }

        static void Save(string label, int index)
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string dir = Path.Combine("Temp/Captures/urp", label);
            Directory.CreateDirectory(dir);
            var cameras = new List<Camera>();
            foreach (var c in Camera.allCameras)
                if (c.targetTexture == null && !IsOverlay(c)) cameras.Add(c);
            cameras.Sort((a, b) => a.depth.CompareTo(b.depth));
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            foreach (var c in cameras) Render(c, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, $"{scene}_{index}.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Debug.Log($"[SceneCompareCapture] {label}/{scene}_{index} ({cameras.Count} cameras)");
        }

        // Under URP a camera is drawn through a render request (Camera.Render draws it the Built-in way: URP's shaders come
        // out magenta); under the Built-in pipeline straight into the texture.
        static void Render(Camera camera, RenderTexture rt)
        {
            var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                && UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera, request))
            {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
                return;
            }
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = null;
        }

        // A URP overlay camera only draws as part of its base camera's stack. Read by name so this compiles
        // with or without the URP package.
        static bool IsOverlay(Camera camera)
        {
            foreach (var c in camera.GetComponents<Component>())
            {
                if (c == null || c.GetType().Name != "UniversalAdditionalCameraData") continue;
                var renderType = c.GetType().GetProperty("renderType");
                return renderType != null && renderType.GetValue(c).ToString() == "Overlay";
            }
            return false;
        }
    }
}
