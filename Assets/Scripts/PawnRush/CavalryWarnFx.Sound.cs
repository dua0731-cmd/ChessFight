using UnityEngine;

namespace ChessFight.PawnRush
{
    // R564 (승규 11-10 "B1으로 해줘", the R563 mockups' set B "전투마" take 1): the royal balcony's guard knights sound
    // on their own cycle (KnightCavalryCharge: warning 1.3 s, leap 1.0 s, landing 0.12 s, windup 0.6 s, charge 1.1 s).
    //   leap    a snort and hoof push-off, the rush through the air and four heavy hooves slamming down - its landing
    //           1.0 s into the clip, so it is cued 1.0 s before the landing (the leap's start)
    //   charge  a hoof pawing the floor and a snort, then a short gallop thundering down the lane and skidding to a stop,
    //           from the windup's start
    // Both ride on the knight (it moves, so the sound comes from where it is), play from where the cycle already is, so
    // a PC that first sees a move part way in hears the rest of it in step. Clips: Resources/PawnRushHallSfx
    // (ArtSource/PawnRush/GrandStairHall/hall_guard_sounds.py); ringing in the stone hall and heard only inside it.
    public sealed partial class CavalryWarnFx
    {
        const string HallSfx = "PawnRushHallSfx/";
        const float LeapVolume = .9f, ChargeVolume = .85f;
        const float LeapLead = 1f;   // s from the clip's start to its landing (hall_guard_sounds.py LEAP)
        const float Late = .3f;      // a cue first seen later than this past its start, with little left, is skipped

        static AudioClip leapClip, chargeClip;
        static bool clipsLooked;

        AudioSource leapVoice, chargeVoice;
        int soundCycle = int.MinValue;
        float soundAge = -1f;
        bool leapt, charged, soundReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSoundStatics()
        {
            leapClip = chargeClip = null;
            clipsLooked = false;
        }

        static AudioClip Clip(string name)
        {
            var clip = Resources.Load<AudioClip>(HallSfx + name);
            if (clip == null) Debug.LogWarning("[대계단 홀] 소리 파일이 없어 재생하지 않습니다: Resources/" + HallSfx + name);
            return clip;
        }

        // age: s since the warning began (negative when the knight is not running); landAt / windupAt: its landing and
        // its windup's start on that same clock.
        void Sound(float age, int cycle, float landAt, float windupAt)
        {
            if (!soundReady)
            {
                soundReady = true;
                if (!clipsLooked)
                {
                    clipsLooked = true;
                    leapClip = Clip("HallGuardLeap");
                    chargeClip = Clip("HallGuardCharge");
                }
                var go = new GameObject("Guard knight sound");
                var body = knight.KnightRoot != null ? knight.KnightRoot.transform : transform;
                go.transform.SetParent(body, false);
                go.transform.localPosition = Vector3.up * 1f;
                // Two voices: the charge starts 0.12 s after the landing, over the landing's tail.
                leapVoice = Voice(go, LeapVolume);
                chargeVoice = Voice(go, ChargeVolume);
                RollingSpawner.Ring(go);
            }
            // R565: the shared clock is wrapped to one cycle (ObstacleContext.PeriodicTime), so the cycle number stays 0;
            // a new run is seen by the age going back (or the knight stopping), not by the number.
            if (cycle != soundCycle || age < soundAge - .5f || (age < 0f && soundAge >= 0f))
            {
                soundCycle = cycle;
                leapt = charged = false;
            }
            soundAge = age;
            if (age < 0f || leapVoice == null) return;

            float leapAt = landAt - LeapLead;
            if (!leapt && leapClip != null && age >= leapAt)
            {
                leapt = true;
                Cue(leapVoice, leapClip, age - leapAt);
            }
            if (!charged && chargeClip != null && age >= windupAt)
            {
                charged = true;
                Cue(chargeVoice, chargeClip, age - windupAt);
            }
        }

        static void Cue(AudioSource voice, AudioClip clip, float from)
        {
            float left = clip.length - from;
            if (from < 0f || left <= 0f) return;
            if (from > Late && left < .5f) return;
            voice.clip = clip;
            voice.time = from;
            voice.Play();
        }

        static AudioSource Voice(GameObject at, float volume)
        {
            var a = at.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 5f;
            a.maxDistance = 40f;
            a.dopplerLevel = 0f;
            a.volume = volume;
            SectionSoundGate.Gate(a);   // heard only in the hall
            return a;
        }
    }
}
