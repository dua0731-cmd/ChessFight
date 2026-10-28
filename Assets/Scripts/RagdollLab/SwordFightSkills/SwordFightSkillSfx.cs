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
    /// the others were made in the same direction (ElevenLabs Sound Effects v2).
    /// R108: the rook's four are stone now, not cartoon (승규 님: "룩 sfx 다 바꿔줘, 띠요오옹 최악이야") — the floor cracking
    /// open, the slam with the towers thrusting up, a stone thud on a body, a crunch on a wall; no boing, whistle or ring.
    /// R116: the edge skills (E, R103) have one sound each, at their one dramatic moment, as 승규 picked on the R115 page:
    /// king D 애니 액션 (the sword into the stone), queen B 시네마틱 (from the gold line: the sucked-in whoosh peaks on the
    /// cut), rook B 시네마틱 (the chunk falling away), bishop E 원소 에너지 (the hands reaching, lifting the ally), knight D
    /// 애니 액션 (started <see cref="Delay"/> into the somersault so its hit is the kick). A sound that leads up to something
    /// that then does not happen (the skill called off or cut, the bishop letting go) fades out at once.
    /// R120: the bishop's F sounds are in the family of its edge sound (승규 님: "비숍도 잘 어울리는 소리로") — no more heavy
    /// hum and metal latch: a swirling energy gathering on the cast, a rushing wind streak as the hands shoot out, a gust
    /// bursting up from the floor ending in a soft deep thump as they grab the ankles; and the ally set down on the edge
    /// (<see cref="SfFxKind.BishopSet"/>) settles with a soft airy thump.
    ///
    /// One clip per moment in Resources/SwordFightSkillSfx, named after the <see cref="SfFxKind"/>; the cast is per piece
    /// ("Cast" + piece, the knight has none: its leap is its cast). Prepared once when loaded like QueenHillSkillSfx does:
    /// the quiet lead-in and the silent tail are cut and the loudest 0.3 s is brought to −21 dB (R119, was −15; at most ×8, peaks under
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
        /// <summary>A sound faded out early (an edge skill called off): the sound and the fade's seconds. The film listens.</summary>
        public static event Action<Sound, float> Cut;

        const float CutFade = 0.06f;

        /// <summary>How loud each moment is next to the others (1 = the matched level). Times the settings' effects volume.</summary>
        static readonly Dictionary<string, float> Level = new Dictionary<string, float>
        {
            { "CastKing", 0.65f }, { "CastQueen", 0.7f }, { "CastRook", 0.65f }, { "CastBishop", 0.7f },
            { "KingParry", 0.95f }, { "KingCounterHit", 0.8f }, { "KingWhiff", 0.55f },
            { "QueenThrust", 0.8f }, { "QueenHit", 0.85f }, { "QueenStop", 0.55f },
            { "RookSlam", 0.9f }, { "RookHit", 0.8f }, { "RookBlocked", 0.7f },
            // 10-28 (사용자 "비숍 f 기존에 있던 사운드는 지우고 아예 새롭게"): the old F's BishopFire and BishopPin clips are
            // gone and CastBishop is a new take - the X cut's moments below are one family of airy steel swishes.
            { "BishopSet", 0.7f },
            { "KnightLeap", 1f }, { "KnightLand", 0.85f }, { "KnightHit", 0.8f },
            { "Interrupted", 0.55f },
            { "KingStab", 0.9f }, { "QueenMark", 0.9f }, { "RookCollapse", 0.95f }, { "BishopReach", 0.85f }, { "KnightFlip", 1f },
            // 10-25 (승규 님: "칼집에서 뽑는 소리, 베인 뒤 한 박자 늦게 반응"): the queen's draw as she goes through, and the cut a beat later.
            { "QueenBlink", 0.9f }, { "QueenCut", 0.9f },
            // 10-26 later (승규 님): KnightFlip, QueenThrust, KingVault (stronger, cinematic), BishopReach and BishopSet are
            // new realistic takes in place of the old ones.
            // 10-26, the realistic effects' own sounds (ElevenLabs): the knight's kick landing,
            // the queen's sparks on the thrust and on the hit, the king's vault off the sword and its landing, the bishop's catch.
            { "KnightKickHit", 0.85f }, { "QueenThrustSparks", 0.55f }, { "QueenHitSparks", 0.6f },
            { "KingVault", 0.75f }, { "KingLand", 0.85f }, { "BishopCatch", 0.75f }, { "RookTremble", 0.85f },
            // 10-28 (the king's edge effects E2, E5, E6): the crown drawn on his landing spot, the enemies his landing hits
            // (under KingLand), the fall when his sword is cut out.
            { "KingMark", 0.55f }, { "KingLandHit", 0.6f }, { "KingDrop", 0.6f },
            // 10-28, the moments the new effects (R172 - R203) drew without a sound: the bishop's X cut (the second sword
            // forming, the throw, the cross, a piece it took), the king's counter cut, the knight's crouch and kick on the
            // edge, the queen's edge cut missing.
            { "BishopDraw", 0.6f }, { "BishopLeap", 0.75f }, { "BishopCross", 0.85f }, { "BishopHit", 0.8f },
            { "KingCounterSpin", 0.8f }, { "KnightCrouch", 0.6f }, { "KnightKick", 0.75f }, { "QueenMiss", 0.5f },
        };

        /// <summary>Moments that play another moment's clip (10-28): the king's landing hit is his counter's hit, his fall
        /// when the sword is cut out is his whiff (the gold falling from the blade) - one family of sounds for the king.</summary>
        static readonly Dictionary<string, string> Alias = new Dictionary<string, string>
        {
            { "KingLandHit", "KingCounterHit" }, { "KingDrop", "KingWhiff" },
            // The queen's edge cut finding no one ends like her thrust stopping short.
            { "QueenMiss", "QueenStop" },
        };

        /// <summary>Sounds laid over a moment's own, started with it and never delayed (10-26): the queen's sparks.</summary>
        static readonly Dictionary<string, string[]> Layers = new Dictionary<string, string[]>
        {
            { "QueenThrust", new[] { "QueenThrustSparks" } },
            { "QueenHit", new[] { "QueenHitSparks" } },
            // 10-28: the king's sword going in starts the swell of the crown drawn where he will land (E2).
            { "KingStab", new[] { "KingMark" } },
        };

        /// <summary>The edge skills' sounds (R116): each belongs to its skill while it runs and stops if that is called off.
        /// The rook's plays when its chunk falls (the floor's news, not a skill moment).</summary>
        static readonly HashSet<string> Edge = new HashSet<string> { "KingStab", "KingMark", "QueenMark", "RookCollapse", "BishopReach", "KnightCrouch", "KnightFlip", "KnightKick" };

        /// <summary>Seconds (game time, the skill's own clock) after its moment a sound starts. 10-26: none now - the knight's
        /// somersault sound is its spin from the start (승규 님: "기존 소리 말고 다른 소리"), and its kick has its own
        /// KnightKickHit; it was 0.45 s for the old clip whose hit was the kick.</summary>
        static readonly Dictionary<string, float> Delay = new Dictionary<string, float>();

        public const string Folder = "SwordFightSkillSfx";
        const int Voices = 12;

        readonly Dictionary<string, Sound> sounds = new Dictionary<string, Sound>();
        readonly Dictionary<string, int> lastFrame = new Dictionary<string, int>();
        AudioSource[] voices;
        float[] fading;
        int next;

        struct Waiting { public string name; public Vector3 at; public SwordFightSkills by; public float left; }
        readonly List<Waiting> waiting = new List<Waiting>();
        // The edge skill sounds each piece has going (the moment's own and its layers): their voices and sounds.
        readonly Dictionary<SwordFightSkills, List<(int voice, Sound sound)>> edgeVoice = new Dictionary<SwordFightSkills, List<(int, Sound)>>();
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
            fading = new float[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var a = holder.gameObject.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 0f;
                voices[i] = a;
            }
            SwordFightSkills.Fx += OnFx;
            SwordFightEdgeFloor.Changed += OnFloor;
        }

        void OnDestroy()
        {
            SwordFightSkills.Fx -= OnFx;
            SwordFightEdgeFloor.Changed -= OnFloor;
            if (Current == this) Current = null;
            foreach (var s in sounds.Values) Destroy(s.clip);
        }

        void OnFx(SfFxEvent e)
        {
            if (e.kind == SfFxKind.EdgeCancel || e.kind == SfFxKind.BishopDrop) { CutEdge(e.by); return; }
            // 10-28: the king's sword cut out - the crown's swell stops, then his fall plays.
            if (e.kind == SfFxKind.KingDrop) CutEdge(e.by);
            string name = ClipName(e);
            if (name != null && Layers.TryGetValue(name, out var layers))
                foreach (var layer in layers) Play(layer, e.at, e.by);
            if (name != null && Delay.TryGetValue(name, out float wait) && wait > 0f)
            {
                if (sound && sounds.ContainsKey(name)) waiting.Add(new Waiting { name = name, at = e.at, by = e.by, left = wait });
                return;
            }
            Play(name, e.at, e.by);
        }

        void OnFloor(SwordFightEdgeFloor.Collapse c)
        {
            // 10-26 (승규 님: "그 특정 땅이 흔들리는데 그 소리가 없어"): the chunk's tremble before it falls has its rumble.
            if (c.phase == SwordFightEdgeFloor.Phase.Crack) Play("RookTremble", c.lip, c.by);
            if (c.phase == SwordFightEdgeFloor.Phase.Open) Play("RookCollapse", c.lip, c.by);
        }

        void Update()
        {
            for (int i = waiting.Count - 1; i >= 0; i--)
            {
                var w = waiting[i];
                // The skill's clock: held in a hit stop, slowed in a slow motion.
                w.left -= Time.deltaTime;
                if (w.left > 0f) { waiting[i] = w; continue; }
                waiting.RemoveAt(i);
                if (w.by != null && w.by.EdgeStage != SfEdge.None) Play(w.name, w.at, w.by);
            }
            for (int v = 0; v < voices.Length; v++)
            {
                if (fading[v] <= 0f) continue;
                var a = voices[v];
                a.volume = Mathf.Max(0f, a.volume - fading[v] * Time.unscaledDeltaTime);
                if (a.volume <= 0f) { a.Stop(); fading[v] = 0f; }
            }
        }

        /// <summary>An edge skill called off or let go: its sound (waiting or playing) fades out.</summary>
        void CutEdge(SwordFightSkills by)
        {
            if (by == null) return;
            waiting.RemoveAll(w => w.by == by);
            if (!edgeVoice.TryGetValue(by, out var held)) return;
            edgeVoice.Remove(by);
            foreach (var (voice, s) in held)
            {
                var a = voices[voice];
                if (!a.isPlaying || a.clip != s.clip) continue;
                fading[voice] = a.volume / CutFade;
                Cut?.Invoke(s, CutFade);
            }
        }

        /// <summary>A moment that is no <see cref="SfFxKind"/> (the effects see it themselves, like the bishop's second
        /// sword forming): its sound, if a bed's sounds are up.</summary>
        public static void Moment(string name, Vector3 at, SwordFightSkills by)
        {
            if (Current != null) Current.Play(name, at, by);
        }

        void Play(string name, Vector3 at, SwordFightSkills by)
        {
            if (!sound || name == null) return;
            if (!sounds.TryGetValue(Alias.TryGetValue(name, out string other) ? other : name, out var s)) return;
            if (lastFrame.TryGetValue(name, out int f) && f == Time.frameCount) return;
            lastFrame[name] = Time.frameCount;
            Count[name] = Count.TryGetValue(name, out int n) ? n + 1 : 1;
            float gain = Mathf.Clamp01(volume * (Level.TryGetValue(name, out float l) ? l : 1f) * ChessFight.Game.GameSettings.Effects);
            float pan = Pan(at);
            int voice = next;
            var a = voices[voice];
            next = (next + 1) % voices.Length;
            fading[voice] = 0f;
            a.Stop();
            a.clip = s.clip;
            a.volume = gain;
            a.panStereo = pan;
            a.Play();
            if (by != null && Edge.Contains(name))
            {
                if (!edgeVoice.TryGetValue(by, out var held)) edgeVoice[by] = held = new List<(int, Sound)>();
                held.RemoveAll(h => !voices[h.voice].isPlaying || voices[h.voice].clip != h.sound.clip || h.voice == voice);
                held.Add((voice, s));
            }
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

            // Loudest 0.3 s (RMS over the channels) → −21 dB, at most ×8, peaks under 0.98. R119: it was −15 dB; the online
            // test found the sounds loud ("기본 사운드가 크다"), so everything sits 6 dB (half as loud) lower.
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
            float gain = Mathf.Min(8f, Mathf.Pow(10f, -21f / 20f) / Mathf.Max(1e-5f, rms), 0.98f / top);
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
