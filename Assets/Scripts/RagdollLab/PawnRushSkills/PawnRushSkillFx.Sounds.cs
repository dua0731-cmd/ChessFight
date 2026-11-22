using System;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The sounds of the ultimates' "!!" presentation (JY-skill-lab, 2026-11-22; the spec's §12 and §2's audio lines), made
    /// in code once while no show runs and played by every PC for its own listener. What is heard is the chess of the move:
    ///
    ///   the board    TOCK (a wooden piece set down), CAPTURE (the capture's clack with a low thump), CHECK (a tock and a
    ///                higher ping), CASTLE (two tocks), PROMOTE (a tock and a chime climbing C6, E6, G6)
    ///   the clock    CLOCK PRESS (the clock's button: two clicks on a plastic body), TICK (the last seconds; as a bed it
    ///                ticks four times a second), TAP (one letter of the call typed)
    ///   the weight   SUB DROP (a sine falling under the key), SWELL (noise growing into the stamp, cut dead), HEART (two
    ///                low thumps), CRACK (a blow's snap over a sub), RISER (noise climbing into a landing), WHOOSH (the world
    ///                let go after the stamp)
    ///   the verdict  SHIMMER (glints on the notes of the brilliant chord), WAH (the blunder's muted horn falling)
    ///   the beds     WIND, MARCH and HUM are loops; SWELL, RISER and TICK have loops of their own. The caller asks for a
    ///                bed every frame; one not asked for again within 0.1 s fades out in 0.12 s, the swell stops at once
    ///   the caps     at most five ultimate one-shots at once, their levels summing to 1.8 at most (the spec's §12): a
    ///                newcomer is scaled into what is left, takes the tally place of a quieter one, or is dropped
    ///
    /// One-shots go through UltPlay (the films record them, the slow motion lowers them); the beds have their own looping
    /// sources and are not on the films' track. Every clip peaks at 0.9 or less and starts and ends on silence.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        // ---------------------------------------------------------------- the state

        /// <summary>A bed: its looping source, what was asked of it last (level, pitch, real time, frame), how far it has
        /// faded in or out, its age (for its own rise) and the slow motion's pitch eased into it.</summary>
        sealed class SndBed
        {
            public AudioSource src;
            public bool on;
            public float level, pitch, gain, age, slow = 1f, askedAt;
            public int askedFrame;
        }

        /// <summary>A one-shot in the caps' tally: when it is heard (real time), its level and how its loudness falls as it
        /// plays (0: held to its end, else the power of the share left).</summary>
        struct SndVoice
        {
            public float start, end, level, bend;
        }

        static readonly int SndIds = Enum.GetValues(typeof(UltClipId)).Length;
        const int SndVoiceMax = 32, SndAtOnce = 5;
        const float SndSumMax = 1.8f, SndFaint = 0.05f, SndBedHold = 0.1f, SndBedFade = 0.12f;

        readonly AudioClip[] sndClips = new AudioClip[SndIds];
        readonly AudioClip[] sndLoops = new AudioClip[SndIds];
        readonly SndBed[] sndBeds = new SndBed[SndIds];
        readonly SndVoice[] sndVoices = new SndVoice[SndVoiceMax];
        int sndVoiceCount, sndPumpFrame = -100;
        bool sndBuilt, sndHooked;
        Action<AudioClip, float, float, float> sndHook;
        Ongoing sndPump;

        // ---------------------------------------------------------------- building

        /// <summary>Makes every clip of the presentation once, and the older ultimate clips (UltAudioBuild) with them. Call
        /// it while no show runs, never at a key: it takes a few milliseconds.</summary>
        public void UltSoundsBuild()
        {
            if (root == null) return;
            UltAudioBuild();
            if (!sndHooked)
            {
                if (sndHook == null) sndHook = SndHeard;
                UltSoundPlayed += sndHook;
                sndHooked = true;
            }
            if (sndBuilt) return;
            sndBuilt = true;
            // CAPTURE is the older clack with a thump under it: its samples, if it is there.
            float[] clack = null;
            if (sfxClack != null && sfxClack.samples > 0)
            {
                clack = new float[sfxClack.samples];
                if (!sfxClack.GetData(clack, 0)) clack = null;
            }
            for (int i = 0; i < SndIds; i++)
            {
                var id = (UltClipId)i;
                var loop = SndLoop(id);
                var one = SndOneShot(id, clack, loop);
                if (loop != null) sndLoops[i] = SndClip($"Ult {id} loop", loop);
                if (one != null) sndClips[i] = ReferenceEquals(one, loop) ? sndLoops[i] : SndClip($"Ult {id}", one);
                if (sndLoops[i] == null) sndLoops[i] = sndClips[i];
            }
        }

        static AudioClip SndClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }

        partial void UltSoundsDestroy()
        {
            if (sndHooked && sndHook != null) UltSoundPlayed -= sndHook;
            sndHooked = false;
            sndPump = null;
            for (int i = 0; i < SndIds; i++)
            {
                var b = sndBeds[i];
                if (b != null && b.src != null) Destroy(b.src);
                sndBeds[i] = null;
            }
            for (int i = 0; i < SndIds; i++)
            {
                if (sndLoops[i] != null && !ReferenceEquals(sndLoops[i], sndClips[i])) Destroy(sndLoops[i]);
                if (sndClips[i] != null) Destroy(sndClips[i]);
                sndLoops[i] = null;
                sndClips[i] = null;
            }
            sndVoiceCount = 0;
            sndBuilt = false;
        }

        // ---------------------------------------------------------------- one-shots and the caps

        partial void UltClipPlay(UltClipId id, float level, float pitch, float delay)
        {
            int i = (int)id;
            if (level <= 0f || root == null || i < 0 || i >= SndIds) return;
            UltSoundsBuild();
            var clip = sndClips[i];
            if (clip == null) return;
            delay = Mathf.Max(0f, delay);
            level = SndFit(level, RealClock + delay);
            if (level > 0f) UltPlay(clip, level, delay, Mathf.Clamp(pitch, 0.05f, 3f));
        }

        /// <summary>The caps on a newcomer heard from <paramref name="onset"/> (real time). The one-shots sounding then are
        /// tallied by how loud they still are (faint tails not at all). While five are sounding, or less than half the
        /// newcomer's level is left of the 1.8, the quietest one gives the louder newcomer its place in the tally (a one-shot
        /// cannot be stopped through UltPlay, so it plays out underneath, already the softest). Then the newcomer is scaled
        /// into what is left, or dropped when five louder ones sound or almost nothing is left.</summary>
        float SndFit(float level, float onset)
        {
            float sum = 0f;
            int count = 0;
            for (int guard = 0; guard <= SndVoiceMax; guard++)
            {
                sum = SndTally(onset, out count, out int quiet, out float quietLevel);
                if (count < SndAtOnce && SndSumMax - sum >= 0.5f * level) break;
                if (quiet < 0 || quietLevel >= level) break;
                sndVoices[quiet].level = 0f;
            }
            if (count >= SndAtOnce) return 0f;
            level = Mathf.Min(level, SndSumMax - sum);
            return level >= 0.04f ? level : 0f;
        }

        /// <summary>The one-shots sounding at <paramref name="at"/> (or starting within 50 ms of it): how many, their summed
        /// loudness now, and the quietest of them.</summary>
        float SndTally(float at, out int count, out int quiet, out float quietLevel)
        {
            float sum = 0f;
            count = 0;
            quiet = -1;
            quietLevel = float.MaxValue;
            for (int i = 0; i < sndVoiceCount; i++)
            {
                ref var v = ref sndVoices[i];
                if (v.level <= 0f || v.end <= at || v.start > at + 0.05f) continue;
                float f = Mathf.Clamp01((at - v.start) / Mathf.Max(1e-3f, v.end - v.start));
                float w = v.level * (v.bend <= 0f ? (f < 0.85f ? 1f : (1f - f) / 0.15f) : Mathf.Pow(1f - f, v.bend));
                if (w < SndFaint) continue;
                sum += w;
                count++;
                if (w < quietLevel)
                {
                    quietLevel = w;
                    quiet = i;
                }
            }
            return sum;
        }

        /// <summary>Every ultimate one-shot UltPlay starts, this file's and the older ones' (the boom, the chords, the
        /// knight's clips) on any PawnRushSkillFx: they all reach one listener, so they all count.</summary>
        void SndHeard(AudioClip clip, float gain, float pitch, float delay)
        {
            if (clip == null || gain <= 0f) return;
            float fx = ChessFight.Game.GameSettings.Effects;
            float level = fx > 1e-3f ? gain / fx : gain;
            float start = RealClock + Mathf.Max(0f, delay);
            SndVoiceAdd(start, start + clip.length / Mathf.Max(0.05f, Mathf.Abs(pitch)), level, SndBendOf(clip));
        }

        void SndVoiceAdd(float start, float end, float level, float bend)
        {
            float now = RealClock;
            int kept = 0;
            for (int r = 0; r < sndVoiceCount; r++)
                if (sndVoices[r].end > now) sndVoices[kept++] = sndVoices[r];
            sndVoiceCount = kept;
            int at = sndVoiceCount;
            if (at >= SndVoiceMax)
            {
                // full: the one that ends first makes way
                at = 0;
                for (int i = 1; i < sndVoiceCount; i++)
                    if (sndVoices[i].end < sndVoices[at].end) at = i;
            }
            else sndVoiceCount++;
            sndVoices[at] = new SndVoice { start = start, end = end, level = level, bend = bend };
        }

        float SndBendOf(AudioClip clip)
        {
            for (int i = 0; i < SndIds; i++)
                if (ReferenceEquals(sndClips[i], clip) || ReferenceEquals(sndLoops[i], clip)) return SndBend((UltClipId)i);
            return 2f;
        }

        /// <summary>How fast a clip's loudness falls in the tally: 0 held (the swells, risers and loops), 0.6 a long fall
        /// (the sub drop, the wah), 1 a ringing one (the chimes, glints, the heart), 2 a knock (the rest and every older
        /// clip).</summary>
        static float SndBend(UltClipId id)
        {
            switch (id)
            {
                case UltClipId.Swell:
                case UltClipId.Riser:
                case UltClipId.Whoosh:
                case UltClipId.Wind:
                case UltClipId.March:
                case UltClipId.Hum:
                    return 0f;
                case UltClipId.SubDrop:
                case UltClipId.Wah:
                    return 0.6f;
                case UltClipId.Promote:
                case UltClipId.Shimmer:
                case UltClipId.Heart:
                    return 1f;
                default:
                    return 2f;
            }
        }

        // ---------------------------------------------------------------- beds

        partial void UltBedPlay(UltClipId id, float level, float pitch)
        {
            int i = (int)id;
            if (level <= 0f || root == null || i < 0 || i >= SndIds) return;
            UltSoundsBuild();
            var clip = sndLoops[i];
            if (clip == null) return;
            var b = sndBeds[i];
            if (b == null || b.src == null)
            {
                var src = root.gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.loop = true;
                src.volume = 0f;
                src.clip = clip;
                b = sndBeds[i] = new SndBed { src = src };
            }
            // Two shows asking for one bed in a frame: the louder ask is heard.
            int frame = Time.frameCount;
            if (!b.on || b.askedFrame != frame || level > b.level)
            {
                b.level = level;
                b.pitch = pitch;
            }
            if (!b.on)
            {
                b.on = true;
                b.age = 0f;
                b.gain = SndBedAttack(id) > 0f ? 0f : 1f;
                b.slow = UltSfxPitch;
                b.src.clip = clip;
                b.src.time = 0f;
                b.src.Play();
            }
            b.askedAt = RealClock;
            b.askedFrame = frame;
            SndBedApply(id, b);
            SndPumpOn();
        }

        /// <summary>Stops a bed at once, on this frame (the swell's cut at the key's stamp, without the frame the bed waits
        /// to see it is no longer asked for). Stop asking for it first, or the next ask starts it again.</summary>
        void SndBedCut(UltClipId id)
        {
            int i = (int)id;
            if (i < 0 || i >= SndIds) return;
            var b = sndBeds[i];
            if (b != null && b.on && b.src != null) SndBedStop(b);
        }

        void SndBedApply(UltClipId id, SndBed b)
        {
            b.src.volume = Mathf.Clamp01(b.level * ChessFight.Game.GameSettings.Effects * b.gain * SndBedRise(id, b.age));
            b.src.pitch = Mathf.Clamp(b.pitch * b.slow * SndBedBend(id, b.age), 0.05f, 3f);
        }

        static void SndBedStop(SndBed b)
        {
            b.src.Stop();
            b.src.volume = 0f;
            b.on = false;
            b.gain = 0f;
        }

        /// <summary>Starts the beds' step (an anim on real time) unless it runs; one that has not stepped for three frames
        /// was dropped with the anims and is replaced.</summary>
        void SndPumpOn()
        {
            if (sndPump != null && Time.frameCount - sndPumpFrame <= 3) return;
            Ongoing pump = null;
            pump = new Ongoing((age, dt) => sndPump == pump && SndBedsStep(dt), () =>
            {
                if (sndPump == pump) sndPump = null;
            }) { tick = UltTick.Real };
            sndPump = pump;
            sndPumpFrame = Time.frameCount;
            anims.Add(pump);
        }

        /// <summary>One frame of the beds: a bed asked for within 0.1 s comes in (or back) and follows its asks; one that is
        /// not fades out in 0.12 s and stops; the swell stops the first frame it was not asked for. The slow motion's lower
        /// pitch (UltSfxPitch, as the one-shots get it from UltPlay) is eased in over 0.08 s. False once no bed plays.</summary>
        bool SndBedsStep(float dt)
        {
            sndPumpFrame = Time.frameCount;
            float slow = UltSfxPitch;
            bool any = false;
            for (int i = 0; i < SndIds; i++)
            {
                var b = sndBeds[i];
                if (b == null || !b.on) continue;
                if (b.src == null)
                {
                    b.on = false;
                    continue;
                }
                var id = (UltClipId)i;
                if (id == UltClipId.Swell)
                {
                    if (Time.frameCount - b.askedFrame >= 2)
                    {
                        SndBedStop(b);
                        continue;
                    }
                }
                else if (RealClock - b.askedAt > SndBedHold)
                {
                    b.gain -= dt / SndBedFade;
                    if (b.gain <= 0f)
                    {
                        SndBedStop(b);
                        continue;
                    }
                }
                else b.gain = Mathf.Min(1f, b.gain + dt / Mathf.Max(0.015f, SndBedAttack(id)));
                b.age += dt;
                b.slow = Mathf.MoveTowards(b.slow, slow, 2f * dt);
                SndBedApply(id, b);
                any = true;
            }
            return any;
        }

        /// <summary>How long a bed takes to come in (0: at once; its clip starts on silence or the bed rises by itself).</summary>
        static float SndBedAttack(UltClipId id)
        {
            switch (id)
            {
                case UltClipId.Wind: return 0.4f;
                case UltClipId.Hum: return 0.08f;
                default: return 0f;
            }
        }

        /// <summary>A bed's own rise from its start: the swell grows as t^2.5 over 0.6 s (the spec's SWELL), the riser as t^2
        /// over 0.4 s.</summary>
        static float SndBedRise(UltClipId id, float age)
        {
            switch (id)
            {
                case UltClipId.Swell: return Mathf.Pow(Mathf.Clamp01(age / 0.6f), 2.5f);
                case UltClipId.Riser: return Mathf.Pow(Mathf.Clamp01(age / 0.4f), 2f);
                default: return 1f;
            }
        }

        /// <summary>A bed's own pitch: the swell brightens as it grows (x0.78 to x1.12); the riser climbs while it is held
        /// (x0.85, about x1.13 after 3 s, x1.3 at most), so a run's riser rises without the caller's help.</summary>
        static float SndBedBend(UltClipId id, float age)
        {
            switch (id)
            {
                case UltClipId.Swell: return Mathf.Lerp(0.78f, 1.12f, Mathf.Pow(Mathf.Clamp01(age / 0.6f), 1.5f));
                case UltClipId.Riser: return 0.85f + 0.45f * (1f - Mathf.Exp(-age / 3f));
                default: return 1f;
            }
        }

        #region Snd synthesis: pure (Mathf, Math and Rate only)

        const float SndTau = Mathf.PI * 2f;

        /// <summary>The samples of an id's one-shot (null: none); <paramref name="loop"/> is its bed's loop if it has one.</summary>
        static float[] SndOneShot(UltClipId id, float[] clack, float[] loop)
        {
            float[] b;
            switch (id)
            {
                // TOCK: a wooden piece set down - 620 and 1240 Hz ringing out in about 35 ms over a click of bright noise.
                case UltClipId.Tock:
                    b = SndBuf(0.09f);
                    SndTockAdd(b, 0f, 1f, 1f, 0x2F6B1D3Bu);
                    return SndPeak(b, 0.85f);
                // CAPTURE: the older clack (sfxClack) with a 90 Hz thump under it for 80 ms.
                case UltClipId.Capture:
                    return SndCapture(clack);
                // CHECK: a tock, then a tock half again as high 70 ms later (the ping of a king hit).
                case UltClipId.Check:
                    b = SndBuf(0.135f);
                    SndTockAdd(b, 0f, 1f, 1f, 0x5BD1E995u);
                    SndTockAdd(b, 0.07f, 1.5f, 0.85f, 0x1B873593u);
                    return SndPeak(b, 0.85f);
                // CASTLE: two tocks 70 ms apart, the second a shade lower (two pieces set down).
                case UltClipId.Castle:
                    b = SndBuf(0.165f);
                    SndTockAdd(b, 0f, 1f, 1f, 0xCC9E2D51u);
                    SndTockAdd(b, 0.07f, 0.95f, 0.9f, 0xE6546B64u);
                    return SndPeak(b, 0.85f);
                case UltClipId.Promote: return SndPromote();
                case UltClipId.ClockPress: return SndClockPress();
                case UltClipId.SubDrop: return SndSubDrop();
                case UltClipId.Swell: return SndSwell();
                case UltClipId.Heart: return SndHeart();
                case UltClipId.Tap: return SndTap();
                case UltClipId.Whoosh: return SndWhoosh();
                case UltClipId.Crack: return SndCrack();
                case UltClipId.Riser: return SndRiser();
                case UltClipId.Tick: return SndTick();
                case UltClipId.Wah: return SndWah();
                case UltClipId.Shimmer: return SndShimmer();
                // WIND and HUM played once: their loop faded in and out (the loop itself starts mid-sound).
                case UltClipId.Wind: return loop != null ? SndEnds((float[])loop.Clone(), 0.3f, 0.4f) : null;
                case UltClipId.Hum: return loop != null ? SndEnds((float[])loop.Clone(), 0.05f, 0.1f) : null;
                // MARCH played once is its loop (it starts and ends on silence).
                case UltClipId.March: return loop;
                default: return null;
            }
        }

        /// <summary>The samples of an id's bed loop (null: the bed loops its one-shot).</summary>
        static float[] SndLoop(UltClipId id)
        {
            switch (id)
            {
                // SWELL as a bed: airy band noise; the bed grows it as t^2.5 over 0.6 s and lifts its pitch itself.
                case UltClipId.Swell: return SndNoiseLoop(1.5f, 2600f, 0.55f, 6500f, 1.5f, 0.5f, 0x6C8E9CF5u);
                // RISER as a bed: a narrower band with a thin whistle above it; the bed climbs it while it is held.
                case UltClipId.Riser: return SndNoiseLoop(1.5f, 1700f, 1.2f, 3900f, 4f, 0.6f, 0x1B56C4E9u);
                // TICK as a bed: one tick every 0.25 s (a locked warning's 4 Hz).
                case UltClipId.Tick: return SndTickBed();
                case UltClipId.Wind: return SndWind();
                case UltClipId.March: return SndMarch();
                case UltClipId.Hum: return SndHum();
                default: return null;
            }
        }

        /// <summary>Adds one tock: 620 and 1240 Hz (and a faint wooden third mode) ringing out in about 35 ms, over 2 ms of
        /// high-passed noise for the contact; <paramref name="pitch"/> raises it the way a faster playback would.</summary>
        static void SndTockAdd(float[] b, float at, float pitch, float gain, uint seed)
        {
            int start = Mathf.RoundToInt(at * Rate);
            int n = Mathf.Min(b.Length - start, Mathf.CeilToInt(0.085f / pitch * Rate));
            var o1 = new SndOsc(620.0 * pitch);
            var o2 = new SndOsc(1240.0 * pitch);
            var o3 = new SndOsc(1975.0 * pitch);
            float d1 = SndDecay(0.012f / pitch), d2 = SndDecay(0.0075f / pitch), d3 = SndDecay(0.004f / pitch), dc = SndDecay(0.0007f / pitch);
            float e1 = 1f, e2 = 0.45f, e3 = 0.12f, ec = 0.6f, hp = 0f, last = 0f;
            uint s = seed;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate * pitch;   // the tock's own time
                float x = SndRand(ref s);
                hp = 0.75f * (hp + x - last);
                last = x;
                float tone = e1 * o1.Next() + e2 * o2.Next() + e3 * o3.Next();
                float v = tone * SndStep(t / 0.0004f) + ec * hp * SndStep(t / 0.0001f);
                b[start + i] += gain * v * (1f - SndStep((t - 0.07f) / 0.015f));
                e1 *= d1;
                e2 *= d2;
                e3 *= d3;
                ec *= dc;
            }
        }

        /// <summary>CAPTURE: the older clack (sfxClack's samples, or its formula when the clip is missing) and a 90 Hz thump
        /// falling into it from 150 Hz, gone by 80 ms; the clack's faint tail trimmed at 0.16 s.</summary>
        static float[] SndCapture(float[] clack)
        {
            var b = SndBuf(0.16f);
            uint s = 0x94D049BBu;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] = 0.8f * (clack != null && i < clack.Length ? clack[i]
                    : 0.55f * (Mathf.Sin(SndTau * 950f * t) + 0.6f * Mathf.Sin(SndTau * 1430f * t)) * Mathf.Exp(-t * 38f) + 0.4f * SndRand(ref s) * Mathf.Exp(-t * 90f));
            }
            int n = Mathf.CeilToInt(0.08f * Rate);
            double p = 0.0;
            float e = 0.9f, d = SndDecay(0.022f), bend = 0.65f, db = SndDecay(0.006f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                b[i] += e * SndStep(t / 0.0015f) * (1f - SndStep((t - 0.06f) / 0.02f)) * (float)Math.Sin(2.0 * Math.PI * p);
                p += 90.0 * (1.0 + bend) / Rate;
                p -= Math.Floor(p);
                e *= d;
                bend *= db;
            }
            return SndPeak(SndEnds(b, 0.0002f, 0.02f), 0.9f);   // (the clack's noise starts at full: 0.2 ms keeps its snap)
        }

        /// <summary>PROMOTE: a tock, then the chime ladder's chime climbing C6, E6, G6 over 0.25 s, G6 left ringing.</summary>
        static float[] SndPromote()
        {
            var b = SndBuf(0.9f);
            SndTockAdd(b, 0f, 1f, 0.75f, 0x2545F491u);
            SndChimeAdd(b, 0.02f, 1046.5f, 0.5f);
            SndChimeAdd(b, 0.135f, 1318.5f, 0.5f);
            SndChimeAdd(b, 0.25f, 1568f, 0.62f);
            return SndPeak(SndEnds(b, 0f, 0.15f), 0.85f);
        }

        /// <summary>Adds the chime of UltScreen's chime ladder: a tone, its octave and a slightly sharp twelfth, 3 ms in,
        /// falling as e^-4.5t.</summary>
        static void SndChimeAdd(float[] b, float at, float hz, float gain)
        {
            int start = Mathf.RoundToInt(at * Rate);
            var o1 = new SndOsc(hz);
            var o2 = new SndOsc(2.0 * hz);
            var o3 = new SndOsc(3.01 * hz);
            float d = SndDecay(1f / 4.5f), e = gain;
            for (int i = start; i < b.Length; i++)
            {
                float t = (i - start) / (float)Rate;
                b[i] += e * Mathf.Clamp01(t * 300f) * (o1.Next() + 0.35f * o2.Next() + 0.15f * o3.Next());
                e *= d;
            }
        }

        /// <summary>CLOCK PRESS: the chess clock's button - two clicks 6 ms apart (2.6 kHz, then a little higher and softer),
        /// each on a 0.9 kHz plastic body, 0.12 s.</summary>
        static float[] SndClockPress()
        {
            var b = SndBuf(0.12f);
            SndClickAdd(b, 0f, 2600f, 1f, 0x68E31DA4u);
            SndClickAdd(b, 0.006f, 2750f, 0.7f, 0xB5297A4Du);
            return SndPeak(SndEnds(b, 0f, 0.01f), 0.8f);
        }

        static void SndClickAdd(float[] b, float at, float hz, float gain, uint seed)
        {
            int start = Mathf.RoundToInt(at * Rate);
            var tick = new SndOsc(hz);
            var body = new SndOsc(900.0);
            float dk = SndDecay(0.0025f), db = SndDecay(0.011f), dn = SndDecay(0.0004f);
            float ek = 1f, eb = 0.8f, en = 0.5f, hp = 0f, last = 0f;
            uint s = seed;
            for (int i = start; i < b.Length; i++)
            {
                float t = (i - start) / (float)Rate;
                float x = SndRand(ref s);
                hp = 0.7f * (hp + x - last);
                last = x;
                b[i] += gain * SndStep(t / 0.00015f) * (ek * tick.Next() + eb * body.Next() + en * hp);
                ek *= dk;
                eb *= db;
                en *= dn;
            }
        }

        /// <summary>SUB DROP: a sine falling from 70 to 28 Hz over 0.9 s (with its octave, for small speakers) under a 30 ms
        /// burst of low noise - the weight under the key.</summary>
        static float[] SndSubDrop()
        {
            var b = SndBuf(0.95f);
            double p = 0.0, hz = 70.0, fall = Math.Pow(0.4, 1.0 / (0.9 * Rate));
            float e = 1f, d = SndDecay(0.55f), e2 = 0.3f, d2 = SndDecay(0.25f), lp = 0f, a = SndPole(1400f);
            uint s = 0x9E3779B9u;
            int burst = Mathf.RoundToInt(0.03f * Rate), glide = Mathf.RoundToInt(0.9f * Rate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                double th = 2.0 * Math.PI * p;
                float tone = (float)Math.Sin(th) + e2 * (float)Math.Sin(2.0 * th);
                lp += a * (SndRand(ref s) - lp);
                float u = i < burst ? 1f - i / (float)burst : 0f;
                b[i] = 0.85f * SndStep(t / 0.006f) * e * tone + 1.4f * lp * SndStep(t / 0.001f) * u * u;
                p += hz / Rate;
                p -= Math.Floor(p);
                if (i < glide) hz *= fall;
                e *= d;
                e2 *= d2;
            }
            return SndPeak(SndEnds(b, 0f, 0.15f), 0.9f);
        }

        /// <summary>SWELL: noise growing as t^2.5 over 0.6 s and brightening (700 Hz to 9 kHz), ending on a 3 ms cut: the held
        /// breath before the stamp.</summary>
        static float[] SndSwell()
        {
            var b = SndBuf(0.6f);
            var f = new SndSvf();
            uint s = 0x7F4A7C15u;
            float hp = 0f, last = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float u = i / (float)(b.Length - 1);
                if ((i & 15) == 0) f.Set(700f * Mathf.Pow(13f, u), 0.7f);
                f.Step(SndRand(ref s), out float low, out _);
                hp = 0.985f * (hp + low - last);
                last = low;
                b[i] = hp * Mathf.Pow(u, 2.5f);
            }
            return SndPeak(SndEnds(b, 0f, 0.003f), 0.85f);
        }

        /// <summary>HEART: two 55 Hz thumps 0.18 s apart (lub, then a softer, slightly higher dub), each soft at its start.</summary>
        static float[] SndHeart()
        {
            var b = SndBuf(0.42f);
            SndThumpAdd(b, 0f, 1f, 1f, 0.06f, 0xA2F9836Eu);
            SndThumpAdd(b, 0.18f, 0.72f, 1.06f, 0.05f, 0x4F1BBCDCu);
            return SndPeak(SndEnds(b, 0f, 0.02f), 0.9f);
        }

        /// <summary>Adds one heart thump: 55 Hz falling into it from 80 Hz, its octave for small speakers, a breath of low
        /// noise, 6 ms in and gone by 0.17 s.</summary>
        static void SndThumpAdd(float[] b, float at, float gain, float tune, float tau, uint seed)
        {
            int start = Mathf.RoundToInt(at * Rate);
            int n = Mathf.Min(b.Length - start, Mathf.CeilToInt(0.17f * Rate));
            double p = 0.0;
            float e = 1f, d = SndDecay(tau), bend = 0.45f, db = SndDecay(0.015f), e2 = 0.35f, d2 = SndDecay(0.03f);
            float en = 1.5f, dn = SndDecay(0.012f), lp = 0f, a = SndPole(300f);
            uint s = seed;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                double th = 2.0 * Math.PI * p;
                lp += a * (SndRand(ref s) - lp);
                float v = (float)Math.Sin(th) + e2 * (float)Math.Sin(2.0 * th) + en * lp;
                b[start + i] += gain * SndStep(t / 0.006f) * (1f - SndStep((t - 0.12f) / 0.05f)) * e * v;
                p += 55.0 * tune * (1.0 + bend) / Rate;
                p -= Math.Floor(p);
                bend *= db;
                e *= d;
                e2 *= d2;
                en *= dn;
            }
        }

        /// <summary>TAP: a 1.15 kHz blip of 25 ms (one per letter of the call; the caller raises it a semitone a letter).</summary>
        static float[] SndTap()
        {
            var b = SndBuf(0.03f);
            var o1 = new SndOsc(1150.0);
            var o2 = new SndOsc(2300.0);
            float e = 1f, d = SndDecay(0.007f), e2 = 0.18f, d2 = SndDecay(0.004f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] = SndStep(t / 0.0008f) * (1f - SndStep((t - 0.018f) / 0.007f)) * (e * o1.Next() + e2 * o2.Next());
                e *= d;
                e2 *= d2;
            }
            return SndPeak(b, 0.7f);
        }

        /// <summary>WHOOSH: a band of noise swept from 300 Hz to 3 kHz over 0.35 s, swelling past the middle and gone: the
        /// world let go after the stamp.</summary>
        static float[] SndWhoosh()
        {
            var b = SndBuf(0.36f);
            var f = new SndSvf();
            uint s = 0x85EBCA6Bu;
            float lp = 0f, a = SndPole(500f);
            for (int i = 0; i < b.Length; i++)
            {
                float u = Mathf.Clamp01(i / (0.35f * Rate));
                if ((i & 15) == 0) f.Set(300f * Mathf.Pow(10f, u), 1.1f);
                float x = SndRand(ref s);
                float band = f.Step(x, out _, out _);
                lp += a * (x - lp);
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(u, 1.4f));
                b[i] = env * env * (band + 0.6f * lp);
            }
            return SndPeak(SndEnds(b, 0.01f, 0.01f), 0.8f);
        }

        /// <summary>CRACK: 20 ms of bright noise (the snap is the point) over a 50 Hz sub falling into it from 110 Hz, 150 ms:
        /// a predictable blow landing.</summary>
        static float[] SndCrack()
        {
            var b = SndBuf(0.17f);
            double p = 0.0;
            float e = 1f, d = SndDecay(0.045f), bend = 1.2f, db = SndDecay(0.008f), e2 = 0.25f, d2 = SndDecay(0.03f);
            float en = 1f, dn = SndDecay(0.004f), hp = 0f, last = 0f;
            uint s = 0xC2B2AE35u;
            int snap = Mathf.RoundToInt(0.02f * Rate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float x = SndRand(ref s);
                hp = 0.86f * (hp + x - last);
                last = x;
                float noise = i < snap ? hp * SndStep(t / 0.0002f) * Mathf.Pow(1f - i / (float)snap, 1.5f) * (0.35f + 0.65f * en) : 0f;
                double th = 2.0 * Math.PI * p;
                float sub = (float)Math.Sin(th) + e2 * (float)Math.Sin(2.0 * th);
                b[i] = 1.1f * noise + 0.85f * SndStep(t / 0.001f) * (1f - SndStep((t - 0.12f) / 0.03f)) * e * sub;
                p += 50.0 * (1.0 + bend) / Rate;
                p -= Math.Floor(p);
                bend *= db;
                e *= d;
                e2 *= d2;
                en *= dn;
            }
            return SndPeak(SndEnds(b, 0f, 0.015f), 0.9f);
        }

        /// <summary>RISER: noise climbing over 0.4 s (a band from 500 Hz to 6 kHz growing as t^2) with a faint tone sliding
        /// two octaves up, ending on its peak (4 ms) into what lands.</summary>
        static float[] SndRiser()
        {
            var b = SndBuf(0.4f);
            var f = new SndSvf();
            uint s = 0x27D4EB2Fu;
            double p = 0.0, hz = 220.0, climb = Math.Pow(4.0, 1.0 / (0.4 * Rate));
            for (int i = 0; i < b.Length; i++)
            {
                float u = i / (float)(b.Length - 1);
                if ((i & 15) == 0) f.Set(500f * Mathf.Pow(12f, u), 1.8f);
                float band = f.Step(SndRand(ref s), out _, out _);
                float tone = (float)Math.Sin(2.0 * Math.PI * p);
                p += hz / Rate;
                p -= Math.Floor(p);
                hz *= climb;
                b[i] = u * u * (band + 0.2f * tone);
            }
            return SndPeak(SndEnds(b, 0.01f, 0.004f), 0.8f);
        }

        /// <summary>TICK: a 2 kHz tick of 15 ms (the clock's last seconds, a locked warning).</summary>
        static float[] SndTick()
        {
            var b = SndBuf(0.02f);
            var o1 = new SndOsc(2000.0);
            var o2 = new SndOsc(4000.0);
            float e = 1f, d = SndDecay(0.0035f), e2 = 0.22f, d2 = SndDecay(0.002f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] = SndStep(t / 0.0003f) * (1f - SndStep((t - 0.011f) / 0.004f)) * (e * o1.Next() + e2 * o2.Next());
                e *= d;
                e2 *= d2;
            }
            return SndPeak(b, 0.65f);
        }

        /// <summary>The tick as a bed's loop: one tick, then silence to 0.25 s.</summary>
        static float[] SndTickBed()
        {
            var tick = SndTick();
            var b = SndBuf(0.25f);
            Array.Copy(tick, b, tick.Length);
            return b;
        }

        /// <summary>WAH: a muted horn falling from 300 to 150 Hz over 0.6 s, its mute opening and closing six times a second
        /// (the tremolo): the blunder's sad note.</summary>
        static float[] SndWah()
        {
            var b = SndBuf(0.62f);
            var f = new SndSvf();
            double p = 0.0, hz = 300.0, fall = Math.Pow(0.5, 1.0 / (0.6 * Rate));
            int glide = Mathf.RoundToInt(0.6f * Rate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                // the horn's first six harmonics (1/n) by the sine recurrence: one sine and one cosine a sample
                double th = 2.0 * Math.PI * p, c2 = 2.0 * Math.Cos(th), s0 = 0.0, s1 = Math.Sin(th), horn = s1;
                for (int h = 2; h <= 6; h++)
                {
                    double sh = c2 * s1 - s0;
                    s0 = s1;
                    s1 = sh;
                    horn += sh / h;
                }
                p += hz / Rate;
                p -= Math.Floor(p);
                if (i < glide) hz *= fall;
                float wob = 0.5f - 0.5f * Mathf.Cos(SndTau * 6f * t);   // the 6 Hz tremolo, from shut
                if ((i & 15) == 0) f.Set(380f + 1100f * wob, 2.2f);
                f.Step((float)horn, out float low, out _);
                b[i] = SndStep(t / 0.025f) * (1f - SndStep((t - 0.5f) / 0.12f)) * (0.6f + 0.4f * wob) * low;
            }
            return SndPeak(SndEnds(b, 0f, 0.005f), 0.75f);
        }

        /// <summary>SHIMMER: 12 glints over 1 s on E7, F#7, G#7 and B7 (2.6-4.2 kHz, the brilliant chord's key, so they ring
        /// with it), each 1.5 ms in and falling in about a tenth of a second, the later ones softer.</summary>
        static float[] SndShimmer()
        {
            var b = SndBuf(1.25f);
            float[] notes = { 2637f, 2960f, 3322.4f, 3951.1f };
            int[] order = { 0, 2, 3, 1, 2, 0, 3, 2, 1, 3, 0, 2 };
            uint s = 0x165667B1u;
            for (int k = 0; k < order.Length; k++)
            {
                float t0 = Mathf.Max(0f, 0.015f + 0.078f * k + 0.018f * SndRand(ref s));
                double hz = notes[order[k]] * (1.0 + 0.002 * SndRand(ref s));
                float gain = (1f - 0.45f * k / 11f) * (0.8f + 0.2f * SndRand(ref s));
                float tau = 0.08f + 0.03f * (1f + SndRand(ref s));
                int start = Mathf.RoundToInt(t0 * Rate), n = Mathf.Min(b.Length - start, Mathf.CeilToInt(4f * tau * Rate));
                var o1 = new SndOsc(hz);
                var o2 = new SndOsc(2.76 * hz);
                float e = 1f, d = SndDecay(tau), e2 = 0.12f, d2 = SndDecay(0.4f * tau);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    b[start + i] += gain * SndStep(t / 0.0015f) * (1f - SndStep((t - 3f * tau) / tau)) * (e * o1.Next() + e2 * o2.Next());
                    e *= d;
                    e2 *= d2;
                }
            }
            return SndPeak(SndEnds(b, 0f, 0.1f), 0.75f);
        }

        /// <summary>WIND (a loop): noise in a band wandering between about 280 Hz and 1 kHz, a faint howl above it and a low
        /// rush under it, in slow gusts; every slow wave has whole cycles in the 4 s, and the take's end is cross-faded into
        /// its start.</summary>
        static float[] SndWind()
        {
            const float L = 4f;
            int loop = Mathf.RoundToInt(L * Rate), x = Mathf.RoundToInt(0.35f * Rate), pre = Rate / 2;
            var take = new float[loop + x];
            var band = new SndSvf();
            var howl = new SndSvf();
            uint s = 0x3C6EF372u;
            float lp = 0f, a = SndPole(220f), m = 0f, gust = 1f;
            for (int i = -pre; i < take.Length; i++)
            {
                if (((i + pre) & 31) == 0)
                {
                    float t = i / (float)Rate;
                    m = 0.6f * Mathf.Sin(SndTau * t / L) + 0.4f * Mathf.Sin(SndTau * 2f * t / L + 1.3f);
                    gust = 0.6f + 0.2f * (1f + 0.65f * Mathf.Sin(SndTau * t / L + 0.4f) + 0.35f * Mathf.Sin(SndTau * 3f * t / L + 2.1f));
                    float hz = 520f * Mathf.Pow(2f, 0.9f * m);
                    band.Set(hz, 0.8f);
                    howl.Set(2.2f * hz, 6f);
                }
                float n = SndRand(ref s);
                float v = band.Step(n, out _, out _) + 0.5f * (0.5f + 0.5f * m) * howl.Step(n, out _, out _);
                lp += a * (n - lp);
                if (i >= 0) take[i] = gust * (v + 1.2f * lp);
            }
            return SndPeak(SndSeal(take, loop, x), 0.6f);
        }

        /// <summary>MARCH (a loop): a 60 Hz kick every 0.5 s (falling into it from 120 Hz, a soft beater on top, the bar's
        /// beats a little uneven), each gone before the next.</summary>
        static float[] SndMarch()
        {
            var b = new float[2 * Rate];
            var kick = SndKick();
            float[] accent = { 1f, 0.8f, 0.92f, 0.8f };
            for (int k = 0; k < accent.Length; k++)
            {
                int start = k * Rate / 2;
                for (int i = 0; i < kick.Length && start + i < b.Length; i++) b[start + i] += accent[k] * kick[i];
            }
            return SndPeak(b, 0.85f);
        }

        /// <summary>One march kick (0.46 s, starting and ending on silence).</summary>
        static float[] SndKick()
        {
            var b = SndBuf(0.46f);
            double p = 0.0;
            float e = 1f, d = SndDecay(0.13f), bend = 1f, db = SndDecay(0.018f), e2 = 0.22f, d2 = SndDecay(0.06f);
            float ec = 0.3f, dc = SndDecay(0.0015f), lp = 0f, a = SndPole(3000f);
            uint s = 0xD1B54A32u;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                double th = 2.0 * Math.PI * p;
                lp += a * (SndRand(ref s) - lp);
                float body = SndStep(t / 0.0015f) * (1f - SndStep((t - 0.38f) / 0.08f)) * e * ((float)Math.Sin(th) + e2 * (float)Math.Sin(2.0 * th));
                b[i] = body + ec * lp * SndStep(t / 0.0001f);
                p += 60.0 * (1.0 + bend) / Rate;
                p -= Math.Floor(p);
                bend *= db;
                e *= d;
                e2 *= d2;
                ec *= dc;
            }
            return b;
        }

        /// <summary>HUM (a loop): 110 and 220 Hz with a few quiet harmonics and a second 220 half a hertz off (a slow beat),
        /// breathing at 1.5 Hz. 2 s holds whole cycles of every one of them, so it loops exactly (the bishop's charge; its
        /// caller raises the pitch to 1.5).</summary>
        static float[] SndHum()
        {
            var b = new float[2 * Rate];
            var o = new[] { new SndOsc(110.0), new SndOsc(220.0), new SndOsc(220.5), new SndOsc(330.0), new SndOsc(440.0), new SndOsc(550.0) };
            float[] k = { 1f, 0.5f, 0.18f, 0.12f, 0.05f, 0.03f };
            var breath = new SndOsc(1.5);
            for (int i = 0; i < b.Length; i++)
            {
                float v = 0f;
                for (int j = 0; j < o.Length; j++) v += k[j] * o[j].Next();
                b[i] = v * (0.9f + 0.1f * breath.Next());
            }
            return SndPeak(b, 0.6f);
        }

        /// <summary>A loop of band noise for a bed: a band at <paramref name="hz"/> and a narrower one above it at
        /// <paramref name="k2"/>, the filters run in for 0.2 s first, the take's end cross-faded into its start.</summary>
        static float[] SndNoiseLoop(float seconds, float hz, float q, float hz2, float q2, float k2, uint seed)
        {
            int loop = Mathf.RoundToInt(seconds * Rate), x = Mathf.RoundToInt(0.2f * Rate), pre = Rate / 5;
            var take = new float[loop + x];
            var f1 = new SndSvf();
            var f2 = new SndSvf();
            f1.Set(hz, q);
            f2.Set(hz2, q2);
            uint s = seed;
            for (int i = -pre; i < take.Length; i++)
            {
                float n = SndRand(ref s);
                float v = f1.Step(n, out _, out _) + k2 * f2.Step(n, out _, out _);
                if (i >= 0) take[i] = v;
            }
            return SndPeak(SndSeal(take, loop, x), 0.7f);
        }

        // -------------------------------- the tools

        static float[] SndBuf(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        /// <summary>A raised-cosine step from 0 (x at 0 or below) to 1 (x at 1 or above).</summary>
        static float SndStep(float x) => x <= 0f ? 0f : x >= 1f ? 1f : 0.5f - 0.5f * Mathf.Cos(Mathf.PI * x);

        /// <summary>The factor per sample of a fall with time constant <paramref name="tau"/> s.</summary>
        static float SndDecay(float tau) => Mathf.Exp(-1f / (tau * Rate));

        /// <summary>A one-pole low-pass's coefficient for <paramref name="hz"/> (y += a (x - y)).</summary>
        static float SndPole(float hz) => 1f - Mathf.Exp(-SndTau * hz / Rate);

        /// <summary>White noise in -1..1 (xorshift; the same clips every run).</summary>
        static float SndRand(ref uint s)
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return (s & 0xFFFFFF) / 8388607.5f - 1f;
        }

        /// <summary>Raised-cosine fades at both ends (the first and last samples exactly 0).</summary>
        static float[] SndEnds(float[] b, float inSeconds, float outSeconds)
        {
            int nIn = Mathf.Min(b.Length, Mathf.RoundToInt(inSeconds * Rate)), nOut = Mathf.Min(b.Length, Mathf.RoundToInt(outSeconds * Rate));
            for (int i = 0; i < nIn; i++) b[i] *= SndStep(i / (float)nIn);
            for (int i = 0; i < nOut; i++) b[b.Length - 1 - i] *= SndStep(i / (float)nOut);
            return b;
        }

        /// <summary>Scaled so its loudest sample is <paramref name="peak"/>.</summary>
        static float[] SndPeak(float[] b, float peak)
        {
            float m = 0f;
            for (int i = 0; i < b.Length; i++) m = Mathf.Max(m, Mathf.Abs(b[i]));
            if (m > 1e-6f)
            {
                float k = peak / m;
                for (int i = 0; i < b.Length; i++) b[i] *= k;
            }
            return b;
        }

        /// <summary>A seamless loop of <paramref name="loop"/> samples from a longer take: the take's last
        /// <paramref name="fade"/> samples are cross-faded (equal power, for noise) into its start.</summary>
        static float[] SndSeal(float[] take, int loop, int fade)
        {
            var b = new float[loop];
            Array.Copy(take, b, loop);
            for (int i = 0; i < fade; i++)
            {
                float x = 0.5f * Mathf.PI * i / fade;
                b[i] = take[i] * Mathf.Sin(x) + take[loop + i] * Mathf.Cos(x);
            }
            return b;
        }

        /// <summary>A sine at a fixed pitch by rotation (no sine per sample), from phase 0.</summary>
        struct SndOsc
        {
            readonly double c, s;
            double x, y;

            public SndOsc(double hz)
            {
                double w = 2.0 * Math.PI * hz / Rate;
                c = Math.Cos(w);
                s = Math.Sin(w);
                x = 1.0;
                y = 0.0;
            }

            public float Next()
            {
                float v = (float)y;
                double nx = x * c - y * s;
                y = x * s + y * c;
                x = nx;
                return v;
            }
        }

        /// <summary>A state-variable filter in its trapezoidal form (stable while its cutoff moves).</summary>
        struct SndSvf
        {
            float ic1, ic2, a1, a2, a3, k;

            public void Set(float hz, float q)
            {
                float g = Mathf.Tan(Mathf.PI * Mathf.Clamp(hz, 10f, 0.45f * Rate) / Rate);
                k = 1f / Mathf.Max(0.05f, q);
                a1 = 1f / (1f + g * (g + k));
                a2 = g * a1;
                a3 = g * a2;
            }

            /// <summary>One sample in: the band out (peak gain 1), the low and the high parts beside it.</summary>
            public float Step(float x, out float low, out float high)
            {
                float v3 = x - ic2;
                float v1 = a1 * ic1 + a2 * v3;
                float v2 = ic2 + a2 * ic1 + a3 * v3;
                ic1 = 2f * v1 - ic1;
                ic2 = 2f * v2 - ic2;
                low = v2;
                high = x - k * v1 - v2;
                return k * v1;
            }
        }

        #endregion
    }
}
