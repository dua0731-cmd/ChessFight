using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    /// <summary>Opens the Sword Fight skill test scene (R91): a copy of the Sword Fight match scene (not the ragdoll lab)
    /// with the skill test bed on the match object. If the scene is not there yet, it is made: SwordFight copied, the bed
    /// added. The match scene itself is not touched.</summary>
    public static class SwordFightSkillTestMenu
    {
        public const string ScenePath = "Assets/Scenes/SkillTest/SwordFight_SkillTest/SwordFight_SkillTest.unity";

        [MenuItem("ChessFight/Sword Fight/Open Skill Test", false, 42)]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) Debug.Log(Make());
            else EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>Copy the match scene into the skill test folder and put the bed on its match object. Returns what it did
        /// (for the editor bridge).</summary>
        public static string Make()
        {
            if (File.Exists(ScenePath)) return "already there: " + ScenePath;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? ".");
            AssetDatabase.Refresh();
            if (!AssetDatabase.CopyAsset(SwordFightBuilder.ScenePath, ScenePath)) return "could not copy " + SwordFightBuilder.ScenePath;
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var game = Object.FindFirstObjectByType<SwordFightGame>();
            if (game == null) return "no SwordFightGame in the copy";
            if (game.GetComponent<SwordFightSkillBed>() == null) game.gameObject.AddComponent<SwordFightSkillBed>();
            game.gameObject.name = "Sword Fight · skill test";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "made " + ScenePath + " (bed on " + game.name + ")";
        }
    }

    /// <summary>Writes SwordFightSkillFilm's frames into H.264 .mp4 files with Unity's own encoder, one file per Began …
    /// Ended. R106: with the skills' sounds — every sound SwordFightSkillSfx starts is mixed into a 44.1 kHz stereo track
    /// from the frame it started on, at its own speed (also in the slow film), as QueenHillSkillFilmEncoder does. R116: a
    /// sound cut short (an edge skill called off) fades out in the track where it did in the game.</summary>
    [InitializeOnLoad]
    public static class SwordFightSkillFilmEncoder
    {
        const int Rate = 44100;

        static MediaEncoder encoder;
        static int fps;
        static long written, frames;

        sealed class Voice
        {
            public SwordFightSkillSfx.Sound sound;
            public float gain, pan;
            public double at;
            public double end = double.MaxValue, fade = 1;
        }

        static readonly List<Voice> playing = new List<Voice>();

        static SwordFightSkillFilmEncoder()
        {
            SwordFightSkillFilm.Began += Begin;
            SwordFightSkillFilm.Frame += Add;
            SwordFightSkillFilm.Ended += End;
            SwordFightSkillSfx.Played += Start;
            SwordFightSkillSfx.Cut += CutShort;
            EditorApplication.playModeStateChanged += _ => { if (!EditorApplication.isPlayingOrWillChangePlaymode) End(); };
        }

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
                bitRateMode = VideoBitrateMode.Medium,
            };
            var audio = new AudioTrackAttributes { sampleRate = new MediaRational(Rate), channelCount = 2, language = "ko" };
            encoder = new MediaEncoder(path, video, audio);
            fps = rate;
            written = frames = 0;
            playing.Clear();
        }

        static void Start(SwordFightSkillSfx.Sound sound, float gain, float pan)
        {
            if (encoder != null) playing.Add(new Voice { sound = sound, gain = gain, pan = pan, at = written });
        }

        static void CutShort(SwordFightSkillSfx.Sound sound, float seconds)
        {
            for (int v = playing.Count - 1; v >= 0; v--)
                if (playing[v].sound == sound && playing[v].end == double.MaxValue)
                {
                    playing[v].end = written;
                    playing[v].fade = Mathf.Max(1f, seconds * Rate);
                    return;
                }
        }

        static void Add(Texture2D frame)
        {
            if (encoder == null) return;
            encoder.AddFrame(frame);
            frames++;
            // The sound for this frame: up to where the frames have got to, so it never drifts.
            long upTo = frames * Rate / fps;
            int n = (int)(upTo - written);
            if (n <= 0) return;
            var mix = new float[n * 2];
            for (int v = playing.Count - 1; v >= 0; v--)
            {
                var p = playing[v];
                var s = p.sound;
                double step = s.rate / (double)Rate;
                int length = s.data.Length / s.channels;
                float left = p.gain * Mathf.Min(1f, 1f - p.pan), right = p.gain * Mathf.Min(1f, 1f + p.pan);
                bool over = false;
                for (int i = 0; i < n; i++)
                {
                    double t = (written + i - p.at) * step;
                    int a = (int)t;
                    if (a < 0) continue;
                    if (a >= length - 1) { over = true; break; }
                    float cut = (float)(1.0 - (written + i - p.end) / p.fade);
                    if (cut <= 0f) { over = true; break; }
                    float f = (float)(t - a);
                    float l = Mathf.Lerp(s.data[a * s.channels], s.data[(a + 1) * s.channels], f);
                    float r = s.channels > 1 ? Mathf.Lerp(s.data[a * s.channels + 1], s.data[(a + 1) * s.channels + 1], f) : l;
                    cut = Mathf.Min(1f, cut);
                    mix[i * 2] += l * left * cut;
                    mix[i * 2 + 1] += r * right * cut;
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
            encoder?.Dispose();
            encoder = null;
            playing.Clear();
        }
    }
}
