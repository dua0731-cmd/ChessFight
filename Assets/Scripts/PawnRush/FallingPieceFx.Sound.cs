using UnityEngine;

namespace ChessFight.PawnRush
{
    // R543 (승규 11-09 "기사 소리 시작"): the grand stair hall's falling knights sound on their own cycle
    // (FallingChessPiece: warning 1.4 s, fall 0.65 s, then the slam's shock 0.7 s and rest 1.8 s).
    //  warn  ticks on the floor circle's clock (W1, VfxGroundWarn: it fills over warning + fall = 2.05 s, the last
    //        0.35 s blinking): a low tick each time three of its rim ticks light, rising, then a beep on each blink -
    //        the chandelier's "째깍 원" (R533) a fourth lower, so the heavier hazard sounds heavier.
    //  fall  the knight's air rush as it comes down, loudest just before it lands. On the knight.
    //  land  the slam: a deep boom, the shockwave's burst of air, a little stone. Where it lands.
    // All three are made to end (warn, fall) or begin (land) at the impact, warning + fall into the cycle, and play
    // from where the cycle already is, so a PC that first sees a drop part way in hears the rest of it, in step.
    // Clips: Resources/PawnRushHallSfx (ArtSource/PawnRush/GrandStairHall/hall_knight_sounds.py). On the shared
    // clock like the picture, so every PC hears the same; ringing in the stone hall and heard only inside it.
    public sealed partial class FallingPieceFx
    {
        const string HallSfx = "PawnRushHallSfx/";
        // R570 (승규 11-10 "A1로 해주고 범위는 그대로 하돼 울림을 크게 하고 바닥 떨어지는 바람 크게 해"): the fall's
        // rush is louder (0.7 -> 1, and lifted in its file); the landing (take A1) rings in a bigger hall, baked into
        // its file and on its own GameObject with a longer, louder ring. The distances stay as they were.
        // R571 (승규 11-10 "내려오는 기사가 바닥을 칠 때 내는 소리 훨씬 크게"): the landing's file is driven hotter
        // (about +6 dB, hall_knight_sounds.py punch) and it plays on two voices started on the same sample - a source
        // cannot go over volume 1, two in step are twice as loud (+6 dB) - at full volume: about +13 dB in all.
        // R572 (승규 11-10 "쿵 소리 좀 줄여"): landing 1 -> 0.7 (about -3 dB), still on two voices.
        const float WarnVolume = .5f, FallVolume = 1f, LandVolume = .7f;
        const float LandRingDecay = 3.8f, LandRingLouder = 600f;
        const float Late = .3f;   // a cue first seen later than this past its start is skipped

        static AudioClip warnClip, fallClip, landClip;
        static bool clipsLooked;

        AudioSource warnVoice, fallVoice, landVoice, landDouble;
        float lastT = -1f;
        bool warned, fell, landed, soundReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSoundStatics()
        {
            warnClip = fallClip = landClip = null;
            clipsLooked = false;
        }

        static AudioClip Clip(string name)
        {
            var clip = Resources.Load<AudioClip>(HallSfx + name);
            if (clip == null) Debug.LogWarning("[대계단 홀] 소리 파일이 없어 재생하지 않습니다: Resources/" + HallSfx + name);
            return clip;
        }

        void Sound(float t, float lead)
        {
            if (!soundReady)
            {
                soundReady = true;
                if (!clipsLooked)
                {
                    clipsLooked = true;
                    warnClip = Clip("HallKnightWarn");
                    fallClip = Clip("HallKnightFall");
                    landClip = Clip("HallKnightLand");
                }
                var ground = new GameObject("Knight sound");
                ground.transform.SetParent(transform, false);
                warnVoice = Voice(ground, 4f, 22f, WarnVolume);
                RollingSpawner.Ring(ground);
                SectionSoundGate.Gate(warnVoice);   // heard only in the hall

                var slam = new GameObject("Knight land sound");
                slam.transform.SetParent(transform, false);
                landVoice = Voice(slam, 6f, 40f, LandVolume);
                landDouble = Voice(slam, 6f, 40f, LandVolume);
                RollingSpawner.Ring(slam, LandRingDecay, LandRingLouder);
                SectionSoundGate.Gate(landVoice);   // gates both: every source on the object

                var air = new GameObject("Knight fall sound");
                air.transform.SetParent(piece.piece != null ? piece.piece : transform, false);
                fallVoice = Voice(air, 4f, 25f, FallVolume);
                RollingSpawner.Ring(air);
                SectionSoundGate.Gate(fallVoice);
            }

            // A new cycle: the time into it went back round.
            if (t < lastT) warned = fell = landed = false;
            lastT = t;

            // Warn and fall end at the impact: a clip of length L starts L before it.
            if (!warned && warnClip != null && t >= lead - warnClip.length)
            {
                warned = true;
                Cue(warnVoice, warnClip, t - (lead - warnClip.length), lead - t);
            }
            if (!fell && fallClip != null && t >= lead - fallClip.length)
            {
                fell = true;
                Cue(fallVoice, fallClip, t - (lead - fallClip.length), lead - t);
            }
            if (!landed && landClip != null && t >= lead)
            {
                landed = true;
                float since = t - lead;
                if (since < Late)
                {
                    // both on one DSP time: a buffer apart, the low boom would cancel itself instead of adding up
                    double at = AudioSettings.dspTime + .02;
                    Cue(landVoice, landClip, since, landClip.length - since, at);
                    Cue(landDouble, landClip, since, landClip.length - since, at);
                }
            }
        }

        // Plays `clip` from `from` s, unless that is past the clip's end or (for a cue not yet at the impact) the cue
        // began more than Late ago and only a stub of it is left.
        // `at` (a DSP time) starts it exactly then instead of at the next mix.
        static void Cue(AudioSource voice, AudioClip clip, float from, float left, double at = 0d)
        {
            if (from < 0f || from >= clip.length || left <= 0f) return;
            if (from > Late && left < .5f) return;
            voice.clip = clip;
            voice.time = from;
            if (at > 0d) voice.PlayScheduled(at);
            else voice.Play();
        }

        static AudioSource Voice(GameObject at, float near, float far, float volume)
        {
            var a = at.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = near;
            a.maxDistance = far;
            a.dopplerLevel = 0f;
            a.volume = volume;
            return a;
        }
    }
}
