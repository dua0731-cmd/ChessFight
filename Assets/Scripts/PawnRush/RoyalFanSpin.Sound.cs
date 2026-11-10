using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R537 (승규 11-09 "바람 소리 시작"): the grand stair hall's royal fans sound with their vent (AirVent: idle 3 s,
    // warning 0.8 s, blowing 3 s on the shared obstacle clock).
    //  hum    the rotor's heavy chop over its motor (R552: a machine, not air), always running, its pitch and loudness following the rotor's own speed: a low murmur
    //         at rest, rising as it spins up through the warning (so the warning is heard as well as seen), full while
    //         it blows, sinking as it runs down.
    //  wind   a heavy steady rush of air while it blows: in over 0.15 s, out over 0.5 s.
    //  gust   one deep whomp of air as each blow starts.
    // Clips: Resources/PawnRushHallSfx (ArtSource/PawnRush/GrandStairHall/hall_fan_sounds.py). On the fan, ringing in
    // the stone hall and heard only inside it (SectionSoundGate).
    public sealed partial class RoyalFanSpin
    {
        const string HallSfx = "PawnRushHallSfx/";
        // R552 (승규 11-10 "이 소리 너무 바람 소리 같아", mockup C "묵직한 로터" https://claude.ai/artifact/WDPb4Vb92Txyxb5NKA3qMz):
        // the fan is heard as a machine - the hum is a heavy rotor chopping over a low motor, rising with the rotor
        // (pitch .55 -> 1.05, loudness .06 -> .9 along speed^1.3) - with the wind only a little under it while it blows
        // (20 % of before) and no gust (was .55 and .7).
        const float HumQuiet = .06f, HumLoud = .9f, HumLow = .55f, HumHigh = 1.05f, HumCurve = 1.3f;
        const float WindVolume = .11f, GustVolume = 0f;
        const float WindIn = .15f, WindOut = .5f;

        static AudioClip humClip, windClip, gustClip;
        static bool clipsLooked;

        AudioSource humVoice, windVoice, gustVoice;
        AirVentState heard = AirVentState.Idle;
        float wind;
        bool soundReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSoundStatics()
        {
            humClip = windClip = gustClip = null;
            clipsLooked = false;
        }

        static AudioClip Clip(string name)
        {
            var clip = Resources.Load<AudioClip>(HallSfx + name);
            if (clip == null) Debug.LogWarning("[대계단 홀] 소리 파일이 없어 재생하지 않습니다: Resources/" + HallSfx + name);
            return clip;
        }

        void Sound(AirVentState state)
        {
            if (!soundReady)
            {
                soundReady = true;
                if (!clipsLooked)
                {
                    clipsLooked = true;
                    humClip = Clip("HallFanHum");
                    windClip = Clip("HallFanWind");
                    gustClip = Clip("HallFanGust");
                }
                var at = new GameObject("Fan sound");
                at.transform.SetParent(transform, false);
                humVoice = Voice(at, 4f, 30f);
                windVoice = Voice(at, 6f, 45f);
                gustVoice = Voice(at, 6f, 45f);
                RollingSpawner.Ring(at);
                SectionSoundGate.Gate(humVoice);   // heard only in the hall
                Loop(humVoice, humClip);
                Loop(windVoice, windClip);
                heard = state;
            }

            // The hum from the rotor's speed (0 at rest .. 1 at full blow).
            float s = Mathf.InverseLerp(idle, blowing, speed);
            humVoice.volume = Mathf.Lerp(HumQuiet, HumLoud, Mathf.Pow(s, HumCurve));
            humVoice.pitch = Mathf.Lerp(HumLow, HumHigh, s);

            bool blowingNow = state == AirVentState.Blowing;
            wind = Mathf.MoveTowards(wind, blowingNow ? 1f : 0f, Time.deltaTime / (blowingNow ? WindIn : WindOut));
            windVoice.volume = WindVolume * wind;

            if (blowingNow && heard != AirVentState.Blowing && gustClip != null && GustVolume > 0f)
            {
                gustVoice.clip = gustClip;
                gustVoice.volume = GustVolume;
                gustVoice.Play();
            }
            heard = state;
        }

        static void Loop(AudioSource voice, AudioClip clip)
        {
            if (clip == null) return;
            voice.clip = clip;
            voice.loop = true;
            voice.volume = 0f;
            voice.time = Random.Range(0f, clip.length);   // fans side by side don't beat in step
            voice.Play();
        }

        static AudioSource Voice(GameObject at, float near, float far)
        {
            var a = at.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = near;
            a.maxDistance = far;
            a.dopplerLevel = 0f;
            a.volume = 0f;
            return a;
        }
    }
}
