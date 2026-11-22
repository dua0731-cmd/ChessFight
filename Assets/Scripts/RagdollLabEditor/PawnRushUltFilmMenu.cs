using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    /// <summary>
    /// Films the knight's ultimates as a player sees them (PawnRushUltFilm) and writes an H.264 .mp4 with the sounds mixed
    /// in (JY-skill-lab, 2026-11-21): opens PawnRush_SkillTest, sets the Game view to 1280 x 720, plays, films and stops.
    /// From the menu, or from the command line, which also quits Unity when it is done:
    ///   Unity.exe -projectPath &lt;project&gt; -executeMethod ChessFight.RagdollLab.Editor.PawnRushUltFilmMenu.FilmAndQuit
    /// The video: Temp/UltFilm/knight-ultimates.mp4 under the project. Play mode reloads the scripts, so what it is doing
    /// is kept in SessionState across that.
    /// </summary>
    [InitializeOnLoad]
    public static class PawnRushUltFilmMenu
    {
        const string Scene = "Assets/Scenes/SkillTest/PawnRush_SkillTest/PawnRush_SkillTest.unity";
        const string Pending = "ChessFight.UltFilm.Pending", Quit = "ChessFight.UltFilm.Quit", Leaving = "ChessFight.UltFilm.Leaving";
        const int Rate = 44100, Fps = 30;

        public static string OutPath => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Temp", "UltFilm", "drive-ultimates.mp4");

        static double startAt = -1d;
        static bool filming;

        static PawnRushUltFilmMenu()
        {
            PawnRushUltFilm.Began += Begin;
            PawnRushUltFilm.Frame += Add;
            PawnRushUltFilm.Ended += End;
            PawnRushSkillFx.UltSoundPlayed += CastSound;
            PawnRushSkillSfx.Played += SkillSound;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Watch;
            // Back in edit mode after a film started from the command line: quit.
            if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(Leaving, false))
            {
                SessionState.SetBool(Leaving, false);
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
            }
        }

        [MenuItem("ChessFight/Pawn Rush/Film Driven Ultimates (game view)", false, 41)]
        static void FilmFromMenu() => Start(false);

        public static void FilmAndQuit() => Start(true);

        static void Start(bool quit)
        {
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(Quit, quit);
            if (EditorApplication.isPlaying)
            {
                startAt = EditorApplication.timeSinceStartup + 1.0;
                return;
            }
            // The scene open already (an editor someone is working in): as it is. Another one: theirs to save first.
            if (EditorSceneManager.GetActiveScene().path != Scene)
            {
                if (!quit && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(Scene);
            }
            PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            PlayModeWindow.SetCustomRenderingResolution(1280, 720, "Ult film 1280x720");
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                startAt = EditorApplication.timeSinceStartup + 2.0;   // let the bed build its pieces first
            if (s == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Leaving, false))
            {
                SessionState.SetBool(Leaving, false);
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
            }
        }

        static double filmBegan = -1d;

        /// <summary>A film asked for from outside an open editor: this file under the project's Temp (deleted on start).</summary>
        static string AskFile => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Temp", "UltFilm", "film-now.txt");

        static void Watch()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && File.Exists(AskFile))
            {
                File.Delete(AskFile);
                Start(false);
                return;
            }
            // (Entering play mode reloads the scripts: the start time set before it is gone, the pending mark is not.)
            if (startAt < 0d && !filming && EditorApplication.isPlaying && SessionState.GetBool(Pending, false))
                startAt = EditorApplication.timeSinceStartup + 2.5;
            if (startAt > 0d && EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= startAt)
            {
                startAt = -1d;
                SessionState.SetBool(Pending, false);
                Debug.Log("[UltFilm] " + PawnRushUltFilm.Run(OutPath, 1280, 720, Fps));
                filming = true;
                filmBegan = EditorApplication.timeSinceStartup;
            }
            // A film that has not ended in 15 minutes is given up (what was written stays).
            if (filming && filmBegan > 0d && EditorApplication.timeSinceStartup - filmBegan > 900d && !PawnRushUltFilm.Status.StartsWith("done"))
            {
                Debug.Log("[UltFilm] given up: " + PawnRushUltFilm.Status);
                End();
                filming = false;
                if (SessionState.GetBool(Quit, false)) { SessionState.SetBool(Quit, false); SessionState.SetBool(Leaving, true); EditorApplication.ExitPlaymode(); }
            }
            if (filming && PawnRushUltFilm.Status.StartsWith("done"))
            {
                filming = false;
                Debug.Log("[UltFilm] " + PawnRushUltFilm.Status);
                if (SessionState.GetBool(Quit, false))
                {
                    SessionState.SetBool(Quit, false);
                    SessionState.SetBool(Leaving, true);
                    EditorApplication.ExitPlaymode();
                }
            }
        }

        // ---------------------------------------------------------------- the video and its sound

        static MediaEncoder encoder;
        static int fps;
        static long written, frames;

        sealed class Voice
        {
            public float[] data;
            public int channels, rate;
            public float gain, pitch;
            public double at;
        }

        static readonly List<Voice> playing = new List<Voice>();
        static readonly Dictionary<AudioClip, (float[] data, int channels, int rate)> samples = new Dictionary<AudioClip, (float[], int, int)>();

        static void Begin(string path, int width, int height, int rate)
        {
            End();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var video = new VideoTrackAttributes
            {
                frameRate = new MediaRational(rate),
                width = (uint)width,
                height = (uint)height,
                includeAlpha = false,
                bitRateMode = VideoBitrateMode.High,
            };
            var audio = new AudioTrackAttributes { sampleRate = new MediaRational(Rate), channelCount = 2, language = "ko" };
            encoder = new MediaEncoder(path, video, audio);
            fps = rate;
            written = frames = 0;
            playing.Clear();
            Debug.Log($"[UltFilm] writing {path}");
        }

        static void CastSound(AudioClip clip, float gain, float pitch, float delay) => AddVoice(clip, gain, pitch, delay);

        static void SkillSound(string name, float gain, float pan)
        {
            var sfx = PawnRushSkillSfx.Current;
            if (sfx == null || !sfx.Sounds.TryGetValue(name, out var sound) || sound.clip == null) return;
            AddVoice(sound.clip, gain, 1f, 0f);
        }

        static void AddVoice(AudioClip clip, float gain, float pitch, float delay)
        {
            if (encoder == null || clip == null) return;
            if (!samples.TryGetValue(clip, out var s))
            {
                var data = new float[clip.samples * clip.channels];
                if (!clip.GetData(data, 0)) return;
                samples[clip] = s = (data, clip.channels, clip.frequency);
            }
            playing.Add(new Voice { data = s.data, channels = s.channels, rate = s.rate, gain = gain, pitch = Mathf.Max(0.1f, pitch), at = written + delay * Rate });
        }

        static void Add(Texture2D frame)
        {
            if (encoder == null) return;
            encoder.AddFrame(frame);
            frames++;
            long upTo = frames * Rate / fps;
            int n = (int)(upTo - written);
            if (n <= 0) return;
            var mix = new float[n * 2];
            for (int v = playing.Count - 1; v >= 0; v--)
            {
                var p = playing[v];
                double step = p.rate / (double)Rate * p.pitch;
                int length = p.data.Length / p.channels;
                bool over = false;
                for (int i = 0; i < n; i++)
                {
                    double t = (written + i - p.at) * step;
                    if (t < 0d) continue;
                    int a = (int)t;
                    if (a >= length - 1) { over = true; break; }
                    float f = (float)(t - a);
                    float l = Mathf.Lerp(p.data[a * p.channels], p.data[(a + 1) * p.channels], f);
                    float r = p.channels > 1 ? Mathf.Lerp(p.data[a * p.channels + 1], p.data[(a + 1) * p.channels + 1], f) : l;
                    mix[i * 2] += l * p.gain;
                    mix[i * 2 + 1] += r * p.gain;
                }
                if (over) playing.RemoveAt(v);
            }
            // Soft limit: several sounds at once must not clip.
            for (int i = 0; i < mix.Length; i++)
            {
                float above = Mathf.Abs(mix[i]) - 0.7f;
                if (above > 0f) mix[i] = Mathf.Sign(mix[i]) * (0.7f + above / (1f + above * 3f));
            }
            using (var block = new NativeArray<float>(mix, Allocator.Temp)) encoder.AddSamples(block);
            written = upTo;
        }

        static void End()
        {
            if (encoder != null) Debug.Log($"[UltFilm] {frames} frames written");
            encoder?.Dispose();
            encoder = null;
            playing.Clear();
        }
    }
}
