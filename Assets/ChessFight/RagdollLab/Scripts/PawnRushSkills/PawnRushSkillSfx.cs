using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Pawn Rush skills' sounds in the test bed (R114). 승규 picked on the R107 page (each piece its own family, no
    /// coin-like rings, the moments run on into each other): pawn = the Queen of the Hill pawn's sounds as they are
    /// ("퀸 오브 더 힐 사운드랑 똑같게"), queen A 만화 효과음, rook A 만화 효과음, bishop B 마법 줄, knight E 묵직한 말.
    ///
    /// The clips are in Resources/PawnRushSkillSfx, named after the moment (Tools/Sfx/PawnRushSkills/export_unity.py
    /// wrote them); the pawn's come from Resources/QueenHillSkillSfx (PawnDash, PawnBump, PawnCrouch). Prepared once when
    /// loaded like QueenHillSkillSfx does: the quiet lead-in and the silent tail are cut and the loudest 0.3 s is brought
    /// to −15 dB (at most ×8, peaks under 0.98). Played flat (2D), panned a little by where the moment is on screen.
    ///
    /// What starts a sound: the effect signals (<see cref="RagdollPawn.SkillFx"/> — online a client raises them from the
    /// host's messages, so it hears the same) and the pieces' skill stages, which every PC sees: the queen's windup, the
    /// rook's aim, lock and charge, the bishop's throw, its wire being armed and going away. Three sounds are held while
    /// something lasts and faded out when it ends, so the moments join: the rook's charge, the bishop's taut rope (from
    /// armed until a trip, at most its own length) and the knight's flight (from the leap to the landing or the stomp).
    /// Repeats rise in pitch like on the mockup page: the rook's four aim ticks and its hits one, two, three, the pawn's
    /// second step; the rook's fourth (stopping) hit and the knight's dive onto a head are lower or higher variants.
    /// </summary>
    [DefaultExecutionOrder(211)]
    public class PawnRushSkillSfx : MonoBehaviour
    {
        public bool sound = true;
        [Range(0f, 1f)] public float volume = 1f;

        public sealed class Sound
        {
            public string name;
            public AudioClip clip;
            public float Length => clip != null ? clip.length : 0f;
        }

        /// <summary>Every sound started: its clip name, volume and pan (−1 left .. 1 right).</summary>
        public static event Action<string, float, float> Played;

        /// <summary>How loud each sound is next to the others (1 = the matched level). Times the settings' effects volume.</summary>
        static readonly Dictionary<string, float> Level = new Dictionary<string, float>
        {
            { "PawnDash", 0.6f }, { "PawnBump", 0.85f }, { "PawnCrouch", 0.65f },
            { "QueenWindup", 0.7f }, { "QueenBlast", 1f },
            { "RookTick", 0.35f }, { "RookLock", 0.6f }, { "RookDash", 0.6f }, { "RookHit", 0.85f }, { "RookWall", 1f },
            { "BishopThrow", 0.6f }, { "BishopLand", 0.7f }, { "BishopArm", 0.7f }, { "BishopHum", 0.35f },
            { "BishopTrip", 0.95f }, { "BishopEnd", 0.6f },
            { "KnightLeap", 0.7f }, { "KnightTurn", 0.7f }, { "KnightLand", 0.8f }, { "KnightStomp", 1f },
            { "KnightDaze", 0.4f }, { "KnightAir", 0.3f },
        };

        public const string Folder = "PawnRushSkillSfx";
        static readonly string[] PawnClips = { "PawnDash", "PawnBump", "PawnCrouch" };
        const int Voices = 16;
        const float HeldFade = 0.12f;
        static readonly float[] TickSteps = { 0f, 2f, 4f, 5f };

        readonly Dictionary<string, Sound> sounds = new Dictionary<string, Sound>();
        readonly Dictionary<string, int> lastFrame = new Dictionary<string, int>();
        AudioSource[] voices;
        int next;
        PawnRushSkillFx fx;

        /// <summary>A sound to start a little later (the rook's ticks, the knight's daze), in unscaled time.</summary>
        struct Later { public float at; public string name; public float semitones, level; public Vector3 where; }
        readonly List<Later> later = new List<Later>();

        /// <summary>A held sound and what it lasts as long as.</summary>
        sealed class Held { public AudioSource voice; public float fadeFrom = -1f, gain; }

        sealed class Watch
        {
            public SkillStage stage;
            public bool aiming, dashing;
            public Held charge, flight;
        }
        readonly Dictionary<RagdollPawn, Watch> watched = new Dictionary<RagdollPawn, Watch>();

        sealed class WireWatch { public bool armed; public Vector3 at; public Held hum; }
        readonly Dictionary<SkillTripwire, WireWatch> wires = new Dictionary<SkillTripwire, WireWatch>();
        readonly List<SkillTripwire> gone = new List<SkillTripwire>();

        public static PawnRushSkillSfx Current { get; private set; }
        public IReadOnlyDictionary<string, Sound> Sounds => sounds;
        /// <summary>How many times each clip started since the scene began.</summary>
        public readonly Dictionary<string, int> Count = new Dictionary<string, int>();

        void Awake()
        {
            Current = this;
            fx = GetComponent<PawnRushSkillFx>();
            foreach (var clip in Resources.LoadAll<AudioClip>(Folder)) Add(clip);
            foreach (var name in PawnClips) Add(Resources.Load<AudioClip>(QueenHillSkillSfx.Folder + "/" + name));
            var holder = new GameObject("Pawn Rush skill sounds").transform;
            holder.SetParent(transform, false);
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var a = holder.gameObject.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 0f;
                voices[i] = a;
            }
            RagdollPawn.SkillFx += OnFx;
        }

        void Add(AudioClip clip)
        {
            if (clip == null) return;
            var made = Prepare(clip);
            if (made != null) sounds[clip.name] = new Sound { name = clip.name, clip = made };
        }

        void OnDestroy()
        {
            RagdollPawn.SkillFx -= OnFx;
            if (Current == this) Current = null;
            foreach (var s in sounds.Values) Destroy(s.clip);
        }

        // ---------------------------------------------------------------- the effect signals

        void OnFx(SkillFxEvent e)
        {
            switch (e.kind)
            {
                case SkillFxKind.PawnStep: Play("PawnDash", e.at, e.count >= 2 ? 3f : 0f); break;
                // Straight shove and diagonal knock-over: the same bump, the knock-over lower and heavier.
                case SkillFxKind.PawnHit: Play("PawnBump", e.at, e.count == 1 ? -3f : 0f, e.count == 1 ? 1.1f : 1f); break;
                case SkillFxKind.PawnHelp: Play("PawnCrouch", e.at, 2f); break;
                case SkillFxKind.QueenBlast: Play("QueenBlast", e.at); break;
                // The charge keeps sounding under its hits (it ends with the dash).
                case SkillFxKind.RookHit: Play("RookHit", e.at, 2f * Mathf.Clamp(e.count - 1, 0, 3)); break;
                case SkillFxKind.RookStop: Play("RookHit", e.at, -3f, 1.1f); break;
                case SkillFxKind.RookWall: Play("RookWall", e.at); break;
                case SkillFxKind.RookBarricade: Play("RookWall", e.at, 3f, 0.8f); break;
                case SkillFxKind.RookSlam: Play("RookWall", e.at, -2f); break;
                case SkillFxKind.BishopWire:
                    Play("BishopLand", e.at);
                    if (e.source is SkillTripwire w && !wires.ContainsKey(w)) wires[w] = new WireWatch { armed = w.Armed, at = e.at };
                    break;
                case SkillFxKind.BishopTrip:
                    Play("BishopTrip", e.at);
                    if (e.source is SkillTripwire tw && wires.TryGetValue(tw, out var ww)) EndHeld(ww.hum, true);
                    break;
                case SkillFxKind.KnightLeap: Play("KnightLeap", e.at); break;
                case SkillFxKind.KnightTurn: Play("KnightTurn", e.at); break;
                case SkillFxKind.KnightHome: Play("KnightTurn", e.at, 2f); break;
                case SkillFxKind.KnightStomp:
                    Play("KnightStomp", e.at);
                    PlayLater(0.1f, "KnightDaze", e.at);
                    EndHeld(OwnerWatch(e.by)?.flight, true);
                    break;
                case SkillFxKind.KnightLand:
                    Play("KnightLand", e.at);
                    EndHeld(OwnerWatch(e.by)?.flight, true);
                    break;
            }
        }

        Watch OwnerWatch(RagdollPawn by) => by != null && watched.TryGetValue(by, out var w) ? w : null;

        // ---------------------------------------------------------------- the skill stages

        void Update()
        {
            float now = Time.unscaledTime;
            for (int i = later.Count - 1; i >= 0; i--)
                if (now >= later[i].at)
                {
                    var l = later[i];
                    later.RemoveAt(i);
                    Play(l.name, l.where, l.semitones, l.level);
                }

            foreach (var pawn in RagdollPawn.All)
            {
                if (pawn == null || pawn.PawnRushSkills == null) continue;
                if (!watched.TryGetValue(pawn, out var w)) watched[pawn] = w = new Watch();
                var stage = pawn.SkillStage;
                Vector3 at = pawn.FeetPoint;
                switch (pawn.Piece)
                {
                    case PieceKind.Queen:
                        if (stage == SkillStage.Windup && w.stage != SkillStage.Windup) Play("QueenWindup", at);
                        break;
                    case PieceKind.Rook:
                    {
                        // Aim: four ticks rising (its four squares). Lock: the aim ends with the rook still winding up.
                        bool aiming = pawn.SkillAiming;
                        if (aiming && !w.aiming)
                            for (int t = 0; t < 4; t++) PlayLater(0.1f * t, "RookTick", at, TickSteps[t]);
                        if (!aiming && w.aiming && stage == SkillStage.Windup) Play("RookLock", at);
                        bool dashing = pawn.SkillDashing;
                        if (dashing && !w.dashing) w.charge = StartHeld("RookDash", at);
                        if (!dashing && w.dashing) EndHeld(w.charge, false);
                        w.aiming = aiming;
                        w.dashing = dashing;
                        break;
                    }
                    case PieceKind.Bishop:
                        if (stage == SkillStage.Active && w.stage == SkillStage.Windup) Play("BishopThrow", at);
                        break;
                    case PieceKind.Knight:
                        if (stage == SkillStage.Active && w.stage != SkillStage.Active) w.flight = StartHeld("KnightAir", at);
                        if (stage != SkillStage.Active && w.stage == SkillStage.Active) EndHeld(w.flight, false);
                        break;
                }
                w.stage = stage;
            }

            // The bishop's wires: armed, then gone (tripped out, timed out, broken or replaced by a new throw).
            // A destroyed wire compares equal to null but stays a key under its old reference until removed here.
            foreach (var kv in wires)
            {
                var wire = kv.Key;
                var ww = kv.Value;
                if (!wire)
                {
                    EndHeld(ww.hum, true);
                    Play("BishopEnd", ww.at);
                    gone.Add(wire);
                    continue;
                }
                ww.at = wire.transform.position;
                if (wire.Armed && !ww.armed)
                {
                    Play("BishopArm", ww.at);
                    ww.hum = StartHeld("BishopHum", ww.at);
                }
                ww.armed = wire.Armed;
            }
            foreach (var wire in gone) wires.Remove(wire);
            gone.Clear();

            FadeHeld(now);
            if (watched.Count > RagdollPawn.All.Count + 8)
            {
                var dropped = new List<RagdollPawn>();
                foreach (var kv in watched) if (kv.Key == null) dropped.Add(kv.Key);
                foreach (var d in dropped) watched.Remove(d);
            }
        }

        // ---------------------------------------------------------------- playing

        void PlayLater(float delay, string name, Vector3 at, float semitones = 0f, float level = 1f)
            => later.Add(new Later { at = Time.unscaledTime + delay, name = name, where = at, semitones = semitones, level = level });

        AudioSource Play(string name, Vector3 at, float semitones = 0f, float level = 1f)
        {
            if (!sound || !sounds.TryGetValue(name, out var s)) return null;
            // The same sound twice in one frame (two pieces hit at once) plays once.
            string key = name + semitones;
            if (lastFrame.TryGetValue(key, out int f) && f == Time.frameCount) return null;
            lastFrame[key] = Time.frameCount;
            Count[name] = Count.TryGetValue(name, out int n) ? n + 1 : 1;
            float gain = Mathf.Clamp01(volume * level * (Level.TryGetValue(name, out float l) ? l : 1f) * ChessFight.Game.GameSettings.Effects);
            float pan = Pan(at);
            var a = FreeVoice();
            a.Stop();
            a.clip = s.clip;
            a.volume = gain;
            a.pitch = Mathf.Pow(2f, semitones / 12f);
            a.panStereo = pan;
            a.Play();
            Played?.Invoke(name, gain, pan);
            return a;
        }

        readonly List<Held> held = new List<Held>();

        Held StartHeld(string name, Vector3 at)
        {
            var a = Play(name, at);
            if (a == null) return null;
            var h = new Held { voice = a, gain = a.volume };
            held.Add(h);
            return h;
        }

        /// <summary>Fade a held sound out; <paramref name="soon"/> fades from now even if it was already fading.</summary>
        void EndHeld(Held h, bool soon)
        {
            if (h == null || (h.fadeFrom >= 0f && !soon)) return;
            h.fadeFrom = Time.unscaledTime;
        }

        void FadeHeld(float now)
        {
            for (int i = held.Count - 1; i >= 0; i--)
            {
                var h = held[i];
                if (h.voice == null || !h.voice.isPlaying) { held.RemoveAt(i); continue; }
                if (h.fadeFrom < 0f) continue;
                float k = 1f - (now - h.fadeFrom) / HeldFade;
                if (k <= 0f) { h.voice.Stop(); held.RemoveAt(i); continue; }
                h.voice.volume = h.gain * k;
            }
        }

        /// <summary>A voice not holding a sound that is still meant to last (the held ones are skipped while busy).</summary>
        AudioSource FreeVoice()
        {
            for (int tries = 0; tries < voices.Length; tries++)
            {
                var a = voices[next];
                next = (next + 1) % voices.Length;
                bool busyHeld = false;
                foreach (var h in held) if (h.voice == a && a.isPlaying) { busyHeld = true; break; }
                if (!busyHeld) return a;
            }
            var v = voices[next];
            next = (next + 1) % voices.Length;
            return v;
        }

        float Pan(Vector3 at)
        {
            var cam = fx != null ? fx.ViewCamera : Camera.main;
            if (cam == null) return 0f;
            var v = cam.WorldToViewportPoint(at);
            return v.z <= 0f ? 0f : Mathf.Clamp(v.x * 2f - 1f, -1f, 1f) * 0.4f;
        }

        /// <summary>Cut the quiet lead-in and the silent tail, match the loudness (see the class notes).</summary>
        static AudioClip Prepare(AudioClip clip)
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
            return made;
        }
    }
}
