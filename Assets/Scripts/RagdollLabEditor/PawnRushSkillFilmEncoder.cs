using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    /// <summary>Writes PawnRushSkillFilm's frames into an H.264 .mp4 with Unity's own encoder (no Recorder
    /// package, no ffmpeg). Hooked up whenever scripts load, so a film started in Play mode is written.
    /// R153: with the skills' sounds, as the Queen of the Hill film has them (R104) — every sound PawnRushSkillSfx starts is
    /// mixed into a 44.1 kHz stereo track from the frame it started on, at its own speed (also in the slow takes).</summary>
    [InitializeOnLoad]
    public static class PawnRushSkillFilmEncoder
    {
        const int Rate = 44100;

        static MediaEncoder encoder;
        static int fps;
        static long written, frames;

        sealed class Voice
        {
            public float[] data;
            public int channels, rate;
            public float gain, pan;
            public double at;
        }

        static readonly List<Voice> playing = new List<Voice>();
        static readonly Dictionary<AudioClip, (float[] data, int channels, int rate)> samples = new Dictionary<AudioClip, (float[], int, int)>();

        static PawnRushSkillFilmEncoder()
        {
            PawnRushSkillFilm.Began += Begin;
            PawnRushSkillFilm.Frame += Add;
            PawnRushSkillFilm.Ended += End;
            PawnRushSkillSfx.Played += Start;
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
                bitRateMode = VideoBitrateMode.High,
            };
            var audio = new AudioTrackAttributes { sampleRate = new MediaRational(Rate), channelCount = 2, language = "ko" };
            encoder = new MediaEncoder(path, video, audio);
            fps = rate;
            written = frames = 0;
            playing.Clear();
        }

        static void Start(string name, float gain, float pan)
        {
            if (encoder == null) return;
            var sfx = PawnRushSkillSfx.Current;
            if (sfx == null || !sfx.Sounds.TryGetValue(name, out var sound) || sound.clip == null) return;
            if (!samples.TryGetValue(sound.clip, out var s))
            {
                var clip = sound.clip;
                var data = new float[clip.samples * clip.channels];
                if (!clip.GetData(data, 0)) return;
                samples[clip] = s = (data, clip.channels, clip.frequency);
            }
            playing.Add(new Voice { data = s.data, channels = s.channels, rate = s.rate, gain = gain, pan = pan, at = written });
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
            using (var block = new NativeArray<float>(n * 2, Allocator.Temp))
            {
                var mix = new float[n * 2];
                for (int v = playing.Count - 1; v >= 0; v--)
                {
                    var p = playing[v];
                    double step = p.rate / (double)Rate;
                    int length = p.data.Length / p.channels;
                    float left = p.gain * Mathf.Min(1f, 1f - p.pan), right = p.gain * Mathf.Min(1f, 1f + p.pan);
                    bool over = false;
                    for (int i = 0; i < n; i++)
                    {
                        double t = (written + i - p.at) * step;
                        int a = (int)t;
                        if (a < 0) continue;
                        if (a >= length - 1) { over = true; break; }
                        float f = (float)(t - a);
                        float l = Mathf.Lerp(p.data[a * p.channels], p.data[(a + 1) * p.channels], f);
                        float r = p.channels > 1 ? Mathf.Lerp(p.data[a * p.channels + 1], p.data[(a + 1) * p.channels + 1], f) : l;
                        mix[i * 2] += l * left;
                        mix[i * 2 + 1] += r * right;
                    }
                    if (over) playing.RemoveAt(v);
                }
                // Soft limit: several sounds at once must not clip.
                for (int i = 0; i < mix.Length; i++)
                {
                    float over = Mathf.Abs(mix[i]) - 0.7f;
                    if (over > 0f) mix[i] = Mathf.Sign(mix[i]) * (0.7f + over / (1f + over * 3f));
                }
                block.CopyFrom(mix);
                encoder.AddSamples(block);
            }
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
