using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Queen of the Hill skills' sounds in the test bed (R104). 승규 picked one direction per skill on the R100
    /// mockup page: king D 묵직한 타격, queen C 만화 효과음, rook C 만화 효과음, bishop D 묵직한 타격, knight A 아케이드 팡,
    /// pawn C 만화 효과음; the other moments were made in the same direction (ElevenLabs Sound Effects v2).
    ///
    /// One clip per <see cref="QueenHillFxKind"/>, named after it, in Resources/QueenHillSkillSfx. The generated files
    /// differ a lot in level, so each is prepared once when loaded: the quiet lead-in is cut (the sound starts on the
    /// moment), the silent tail too, and the loudest 0.3 s is brought to −15 dB (at most ×8, peaks under 0.98) like
    /// the mockup page did. Played flat (2D), panned a little by where the moment is on screen. The same moment
    /// twice in one frame (three guarded allies, two pieces hit) sounds once.
    /// </summary>
    [DefaultExecutionOrder(211)]
    public class QueenHillSkillSfx : MonoBehaviour
    {
        public bool sound = true;
        [Range(0f, 1f)] public float volume = 1f;

        /// <summary>A prepared clip: what plays, and the samples the film mixes into its sound track.</summary>
        public sealed class Sound
        {
            public QueenHillFxKind kind;
            public AudioClip clip;
            public float[] data;
            public int channels, rate;
            public float Length => data.Length / (float)(channels * rate);
        }

        /// <summary>Every sound started: the sound, its volume and its pan (−1 left .. 1 right). The film listens.</summary>
        public static event Action<Sound, float, float> Played;

        /// <summary>How loud each moment is next to the others (1 = the matched level). Times the settings' effects volume.</summary>
        static readonly Dictionary<QueenHillFxKind, float> Level = new Dictionary<QueenHillFxKind, float>
        {
            { QueenHillFxKind.KingWindup, 0.7f }, { QueenHillFxKind.WardOn, 0.55f }, { QueenHillFxKind.WardBlock, 0.85f },
            { QueenHillFxKind.QueenLock, 0.7f }, { QueenHillFxKind.QueenHit, 0.85f },
            { QueenHillFxKind.RookRequest, 0.7f }, { QueenHillFxKind.RookAccept, 0.75f }, { QueenHillFxKind.RookCancel, 0.7f },
            { QueenHillFxKind.BishopRise, 0.75f }, { QueenHillFxKind.BishopHit, 0.8f }, { QueenHillFxKind.BishopDrop, 0.7f }, { QueenHillFxKind.BishopLand, 0.75f },
            { QueenHillFxKind.KnightLock, 0.65f }, { QueenHillFxKind.KnightFlatten, 0.85f }, { QueenHillFxKind.KnightFall, 0.8f },
            { QueenHillFxKind.PawnCrouch, 0.6f }, { QueenHillFxKind.PawnBump, 0.85f }, { QueenHillFxKind.PawnStop, 0.8f },
            { QueenHillFxKind.Down, 0.6f },
        };

        public const string Folder = "QueenHillSkillSfx";
        const int Voices = 12;

        readonly Dictionary<QueenHillFxKind, Sound> sounds = new Dictionary<QueenHillFxKind, Sound>();
        readonly Dictionary<QueenHillFxKind, int> lastFrame = new Dictionary<QueenHillFxKind, int>();
        AudioSource[] voices;
        int next;
        QueenHillSkillFx fx;

        /// <summary>The bed's sounds, for the probe's report.</summary>
        public static QueenHillSkillSfx Current { get; private set; }
        public IReadOnlyDictionary<QueenHillFxKind, Sound> Sounds => sounds;
        /// <summary>How many sounds each moment started since the scene began (the probe checks the skills reach them).</summary>
        public readonly Dictionary<QueenHillFxKind, int> Count = new Dictionary<QueenHillFxKind, int>();

        void Awake()
        {
            Current = this;
            fx = GetComponent<QueenHillSkillFx>();
            foreach (var clip in Resources.LoadAll<AudioClip>(Folder))
                if (Enum.TryParse(clip.name, out QueenHillFxKind kind))
                {
                    var s = Prepare(clip, kind);
                    if (s != null) sounds[kind] = s;
                }
            var holder = new GameObject("Queen of the Hill skill sounds").transform;
            holder.SetParent(transform, false);
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var a = holder.gameObject.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 0f;
                voices[i] = a;
            }
            RagdollPawn.QueenHillFx += OnFx;
        }

        void OnDestroy()
        {
            RagdollPawn.QueenHillFx -= OnFx;
            if (Current == this) Current = null;
            foreach (var s in sounds.Values) Destroy(s.clip);
        }

        void OnFx(QueenHillFxEvent e)
        {
            if (!sound || !sounds.TryGetValue(e.kind, out var s)) return;
            if (lastFrame.TryGetValue(e.kind, out int f) && f == Time.frameCount) return;
            lastFrame[e.kind] = Time.frameCount;
            Count[e.kind] = Count.TryGetValue(e.kind, out int n) ? n + 1 : 1;
            float gain = volume * (Level.TryGetValue(e.kind, out float l) ? l : 1f) * ChessFight.Game.GameSettings.Effects;
            float pan = Pan(e.at);
            var a = voices[next];
            next = (next + 1) % voices.Length;
            a.Stop();
            a.clip = s.clip;
            a.volume = Mathf.Clamp01(gain);
            a.panStereo = pan;
            a.Play();
            Played?.Invoke(s, Mathf.Clamp01(gain), pan);
        }

        float Pan(Vector3 at)
        {
            var cam = fx != null ? fx.ViewCamera : Camera.main;
            if (cam == null) return 0f;
            var v = cam.WorldToViewportPoint(at);
            return v.z <= 0f ? 0f : Mathf.Clamp(v.x * 2f - 1f, -1f, 1f) * 0.4f;
        }

        /// <summary>Cut the quiet lead-in and the silent tail, match the loudness (see the class notes).</summary>
        static Sound Prepare(AudioClip clip, QueenHillFxKind kind)
        {
            if (clip.loadState != AudioDataLoadState.Loaded && !clip.LoadAudioData()) return null;
            int ch = Mathf.Max(1, clip.channels), rate = clip.frequency, frames = clip.samples;
            var raw = new float[frames * ch];
            if (frames == 0 || !clip.GetData(raw, 0)) return null;

            float Peak(int frame)
            {
                float m = 0f;
                for (int c = 0; c < ch; c++) m = Mathf.Max(m, Mathf.Abs(raw[frame * ch + c]));
                return m;
            }
            float top = 0f;
            for (int i = 0; i < frames; i++) top = Mathf.Max(top, Peak(i));
            if (top <= 1e-5f) return null;
            // Lead-in: up to 5 ms before the first sample over 1/16 of the peak. Tail: 30 ms after the last over 1/300.
            int first = 0, last = frames - 1;
            while (first < frames && Peak(first) < top / 16f) first++;
            while (last > first && Peak(last) < top / 300f) last--;
            first = Mathf.Max(0, first - rate / 200);
            last = Mathf.Min(frames - 1, last + rate * 3 / 100);
            int count = last - first + 1;
            var data = new float[count * ch];
            Array.Copy(raw, first * ch, data, 0, data.Length);

            // Loudest 0.3 s (RMS over the channels) → −15 dB, at most ×8, peaks under 0.98.
            int win = Mathf.Min(count, Mathf.RoundToInt(rate * 0.3f));
            double sum = 0, best = 0;
            for (int i = 0; i < count; i++)
            {
                for (int c = 0; c < ch; c++) sum += data[i * ch + c] * (double)data[i * ch + c];
                if (i >= win)
                    for (int c = 0; c < ch; c++) sum -= data[(i - win) * ch + c] * (double)data[(i - win) * ch + c];
                if (i >= win - 1) best = Math.Max(best, sum);
            }
            float rms = (float)Math.Sqrt(best / Math.Max(1, win * ch));
            float gain = Mathf.Min(8f, Mathf.Pow(10f, -15f / 20f) / Mathf.Max(1e-5f, rms), 0.98f / top);
            int fade = Mathf.Min(count, rate * 3 / 100);
            for (int i = 0; i < count; i++)
            {
                float g = gain * (i >= count - fade ? (count - 1 - i) / (float)fade : 1f);
                for (int c = 0; c < ch; c++) data[i * ch + c] *= g;
            }

            var made = AudioClip.Create(kind.ToString(), count, ch, rate, false);
            made.SetData(data, 0);
            return new Sound { kind = kind, clip = made, data = data, channels = ch, rate = rate };
        }
    }
}
