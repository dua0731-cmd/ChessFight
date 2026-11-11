using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R533 (승규 11-09, from the chandelier warning mockups: "A로 해주고 샹들리에 깨지는 소리도 넣어"):
    //  warning  "째깍 원" on the floor circle's clock (VfxGroundWarn, 1.6 s): a soft tick each time three of its 24 rim
    //           ticks light, rising a little, then a clearer beep on each of the last 0.35 s's blinks (7 a second).
    //           Made for warning + fall = 1.6 s; it starts with the circle, so a PC that first sees the drop part way
    //           in starts it that far in (not at all past 0.3 s).
    //  break    the crash where it lands (warning + fall): the frame's heavy thud, its glass and metal scattering.
    // Clips: Resources/PawnRushHallSfx (ArtSource/PawnRush/GrandStairHall/hall_chandelier_sounds.py). From the shared
    // clock like the picture, so every PC hears the same; on the drop's spot, ringing in the stone hall and heard only
    // inside it (SectionSoundGate).
    public sealed partial class ChandelierRain
    {
        const string HallSfx = "PawnRushHallSfx/";
        // 승규 11-09 "깨지는 소리 줄여": the crash at 60 % (about -4.4 dB), then "더 줄여줘": 35 % (about -9 dB)
        // R573 (승규 11-10 "샹들리에 예고 살짝만 키워"): warning .55 -> .7 (about +2 dB).
        const float WarnVolume = .7f, BreakVolume = .35f;

        static AudioClip warnClip, breakClip;
        static bool clipsLooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSoundStatics()
        {
            warnClip = breakClip = null;
            clipsLooked = false;
        }

        static void LoadClips()
        {
            if (clipsLooked) return;
            clipsLooked = true;
            warnClip = Clip("HallChandelierWarn");
            breakClip = Clip("HallChandelierBreak");
        }

        static AudioClip Clip(string name)
        {
            var clip = Resources.Load<AudioClip>(HallSfx + name);
            if (clip == null) Debug.LogWarning("[대계단 홀] 소리 파일이 없어 재생하지 않습니다: Resources/" + HallSfx + name);
            return clip;
        }

        void Sound(View view, ChandelierSchedule.Drop d, float age)
        {
            LoadClips();
            if (view.warnVoice == null) MakeVoices(view);

            if (view.warnedFor != d.Serial)
            {
                view.warnedFor = d.Serial;
                if (warnClip != null && age < .3f && age < warnClip.length)
                {
                    view.warnVoice.clip = warnClip;
                    view.warnVoice.time = age;
                    view.warnVoice.Play();
                }
            }

            float since = age - (warning + fall);
            if (since >= 0f && view.brokeFor != d.Serial)
            {
                view.brokeFor = d.Serial;
                if (breakClip != null && since < .3f && since < breakClip.length)
                {
                    view.breakVoice.clip = breakClip;
                    view.breakVoice.pitch = Mathf.Lerp(.93f, 1.05f, Mathf.Repeat(d.Serial * .618034f, 1f));
                    view.breakVoice.time = since;
                    view.breakVoice.Play();
                }
            }
        }

        // Both on the pooled root, which sits on the drop's spot and lives through the warning, the fall and the debris.
        static void MakeVoices(View view)
        {
            var at = view.root;
            // R538 (승규 11-09 "해머 장애물 코스에 도달하면 샹들리에 깨지는 소리랑 예고 소리 때문에 묵직한 바람 소리가 안
            // 들려 - 샹들리에 쪽 소리 범위를 조절해"): heard round the drops only, gone before the hammers (was 4 -> 32 m
            // and 6 -> 55 m).
            view.warnVoice = Voice(at, 3f, 14f, WarnVolume);
            view.breakVoice = Voice(at, 4f, 22f, BreakVolume);
            RollingSpawner.Ring(at);
            SectionSoundGate.Gate(view.warnVoice);   // heard only in the hall
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
