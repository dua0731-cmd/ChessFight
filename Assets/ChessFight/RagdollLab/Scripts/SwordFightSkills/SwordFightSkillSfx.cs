using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Sword Fight F skills' sounds in the test bed (R106). 승규 asked for them "in the feel of the Pawn Rush and Queen of
    /// the Hill sounds", so each piece keeps the direction it got in Queen of the Hill (R104): king D 묵직한 타격, queen C
    /// 만화 효과음, rook C 만화 효과음, bishop D 묵직한 타격, knight A 아케이드 팡. Where the moment is the same kind of thing
    /// the Queen of the Hill clip is used as it is (queen's hit, bishop's rising hum, knight's leap and landing);
    /// the others were made in the same direction (ElevenLabs Sound Effects v2). The edge skills (E, R103) have no sound yet.
    /// R108: the rook's four are stone now, not cartoon (승규 님: "룩 sfx 다 바꿔줘, 띠요오옹 최악이야") — the floor cracking
    /// open, the slam with the towers thrusting up, a stone thud on a body, a crunch on a wall; no boing, whistle or ring.
    ///
    /// One clip per moment in Resources/SwordFightSkillSfx, named after the <see cref="SfFxKind"/>; the cast is per piece
    /// ("Cast" + piece, the knight has none: its leap is its cast). Prepared once when loaded like QueenHillSkillSfx does:
    /// the quiet lead-in and the silent tail are cut and the loudest 0.3 s is brought to −15 dB (at most ×8, peaks under
    /// 0.98). Played flat (2D), panned a little by where the moment is on screen. The same moment twice in one frame (two
    /// pieces hit) sounds once.
    /// </summary>
    [DefaultExecutionOrder(211)]
    public class SwordFightSkillSfx : MonoBehaviour
    {
        public bool sound = true;
        [Range(0f, 1f)] public float volume = 1f;

        /// <summary>A prepared clip: what plays, and the samples the film mixes into its sound track.</summary>
        public sealed class Sound
        {
            public string name;
            public AudioClip clip;
            public float[] data;
            public int channels, rate;
            public float Length => data.Length / (float)(channels * rate);
        }

        /// <summary>Every sound started: the sound, its volume and its pan (−1 left .. 1 right). The film listens.</summary>
        public static event Action<Sound, float, float> Played;

        /// <summary>How loud each moment is next to the others (1 = the matched level). Times the settings' effects volume.</summary>
        static readonly Dictionary<string, float> Level = new Dictionary<string, float>
        {
            { "CastKing", 0.65f }, { "CastQueen", 0.7f }, { "CastRook", 0.65f }, { "CastBishop", 0.7f },
            { "KingParry", 0.95f }, { "KingCounterHit", 0.8f }, { "KingWhiff", 0.55f },
            { "QueenThrust", 0.8f }, { "QueenHit", 0.85f }, { "QueenStop", 0.55f },
            { "RookSlam", 0.9f }, { "RookHit", 0.8f }, { "RookBlocked", 0.7f },
            { "BishopFire", 0.75f }, { "BishopPin", 0.85f },
            { "KnightLeap", 0.75f }, { "KnightLand", 0.85f }, { "KnightHit", 0.8f },
            { "Interrupted", 0.55f },
        };

        public const string Folder = "SwordFightSkillSfx";
        const int Voices = 12;

        readonly Dictionary<string, Sound> sounds = new Dictionary<string, Sound>();
        readonly Dictionary<string, int> lastFrame = new Dictionary<string, int>();
        AudioSource[] voices;
        int next;
        SwordFightSkillFx fx;

        /// <summary>The bed's sounds, for the probe's report.</summary>
        public static SwordFightSkillSfx Current { get; private set; }
        public IReadOnlyDictionary<string, Sound> Sounds => sounds;
        /// <summary>How many sounds each clip started since the scene began (the probe checks the skills reach them).</summary>
        public readonly Dictionary<string, int> Count = new Dictionary<string, int>();

        /// <summary>The clip a moment plays: the cast is the piece's own, the rest are the moment's name.</summary>
        public static string ClipName(SfFxEvent e) =>
            e.kind == SfFxKind.Cast ? (e.by != null && e.by.Pawn != null ? "Cast" + e.by.Piece : null) : e.kind.ToString();

        void Awake()
        {
            Current = this;
            fx = GetComponent<SwordFightSkillFx>();
            foreach (var clip in Resources.LoadAll<AudioClip>(Folder))
            {
                var s = Prepare(clip);
                if (s != null) sounds[clip.name] = s;
            }
            var holder = new GameObject("Sword Fight skill sounds").transform;
            holder.SetParent(transform, false);
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var a = holder.gameObject.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 0f;
                voices[i] = a;
            }
            SwordFightSkills.Fx += OnFx;
        }

        void OnDestroy()
        {
            SwordFightSkills.Fx -= OnFx;
            if (Current == this) Current = null;
            foreach (var s in sounds.Values) Destroy(s.clip);
        }

        void OnFx(SfFxEvent e)
        {
            string name = ClipName(e);
            if (!sound || name == null || !sounds.TryGetValue(name, out var s)) return;
            if (lastFrame.TryGetValue(name, out int f) && f == Time.frameCount) return;
            lastFrame[name] = Time.frameCount;
            Count[name] = Count.TryGetValue(name, out int n) ? n + 1 : 1;
            float gain = Mathf.Clamp01(volume * (Level.TryGetValue(name, out float l) ? l : 1f) * ChessFight.Game.GameSettings.Effects);
            float pan = Pan(e.at);
            var a = voices[next];
            next = (next + 1) % voices.Length;
            a.Stop();
            a.clip = s.clip;
            a.volume = gain;
            a.panStereo = pan;
            a.Play();
            Played?.Invoke(s, gain, pan);
        }

        float Pan(Vector3 at)
        {
            var cam = fx != null ? fx.ViewCamera : Camera.main;
            if (cam == null) return 0f;
            var v = cam.WorldToViewportPoint(at);
            return v.z <= 0f ? 0f : Mathf.Clamp(v.x * 2f - 1f, -1f, 1f) * 0.4f;
        }

        /// <summary>Cut the quiet lead-in and the silent tail, match the loudness (see the class notes).</summary>
        static Sound Prepare(AudioClip clip)
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

            var made = AudioClip.Create(clip.name, count, ch, rate, false);
            made.SetData(data, 0);
            return new Sound { name = clip.name, clip = made, data = data, channels = ch, rate = rate };
        }
    }
}
