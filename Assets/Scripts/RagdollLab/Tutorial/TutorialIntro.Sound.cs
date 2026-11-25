using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The opening's sounds (user 11-25 "1 4 6 7 해서 자연스럽게 편집해"): four takes from the ElevenLabs flow "튜토리얼 독수리
    /// 오프닝 소리", cut and faded by ArtSource/Tutorial/bake_eagle_sfx.py into Resources/TutorialSfx/ - EagleWing (one wing
    /// beat), EagleCry, EagleCrash (the feet, the hops and the belly slide) and EagleRuffle (the shake after getting up).
    /// They sound from the eagle itself (an AudioSource on it), timed from the same clock as the pose: a wing beat at the
    /// top of each power stroke (Pose leaves its phase and weight here), the cry before the flare and again as it leaves,
    /// the crash at touchdown, the ruffle when it shakes itself. A jump of the clock plays nothing.
    /// </summary>
    public sealed partial class TutorialIntro
    {
        const string SfxFolder = "TutorialSfx/";
        // The first cry sounds as the scene opens (user 11-25 "튜토리얼 씬에 들어가고 울 수 있게"), not before the landing.
        const float CryAtStart = .3f;
        // Once more halfway round the lap (user 11-25 "중간에 한 번 더"), a little higher and softer.
        const float CryMidway = 5f;
        const float WingVolume = .6f, CryVolume = .7f, CrashVolume = .9f, RuffleVolume = .6f;
        // The Pose methods leave the wing phase here each frame (NaN: not beating): approach 0, leaving 1.
        float beatPhase = float.NaN, beatWeight;
        int beatStage;
        int lastBeat = -1;
        float soundT = -1f;
        bool soundReady;
        AudioSource voice;
        AudioClip wingClip, cryClip, crashClip, ruffleClip;

        void LoadSounds()
        {
            soundReady = true;
            if (Application.isBatchMode) return;
            wingClip = Resources.Load<AudioClip>(SfxFolder + "EagleWing");
            cryClip = Resources.Load<AudioClip>(SfxFolder + "EagleCry");
            crashClip = Resources.Load<AudioClip>(SfxFolder + "EagleCrash");
            ruffleClip = Resources.Load<AudioClip>(SfxFolder + "EagleRuffle");
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = .5f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = 8f;
            voice.maxDistance = 70f;
        }

        void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (voice == null || clip == null) return;
            voice.pitch = pitch * Random.Range(.97f, 1.03f);
            voice.PlayOneShot(clip, volume);
        }

        // True when the clock passed `at` this frame (and did not jump over it).
        bool Crossed(float prev, float t, float at) => prev < at && t >= at && t - prev < .5f;

        /// <summary>Once a frame after Pose(t).</summary>
        void SoundStep(float t)
        {
            if (!soundReady) LoadSounds();
            float prev = soundT;
            soundT = t;
            if (voice == null || prev < 0f) { beatPhase = float.NaN; return; }

            // A wing beat at the start of each power stroke, harder and quicker as it brakes.
            if (!float.IsNaN(beatPhase) && beatWeight > .35f && t - prev < .5f)
            {
                int beat = Mathf.FloorToInt(beatPhase) + beatStage * 1000;
                if (beat != lastBeat)
                {
                    lastBeat = beat;
                    bool braking = beatStage == 0 && t > FlareFrom;
                    Play(wingClip, WingVolume * Mathf.Clamp01(beatWeight) * (braking ? 1.2f : 1f), braking ? 1.1f : beatStage == 1 ? .95f : 1f);
                }
            }
            beatPhase = float.NaN;

            float landed = Approach;
            if (Crossed(prev, t, CryAtStart)) Play(cryClip, CryVolume);
            if (Crossed(prev, t, CryMidway)) Play(cryClip, CryVolume * .85f, 1.08f);
            if (Crossed(prev, t, landed)) Play(crashClip, CrashVolume);
            if (Crossed(prev, t, landed + Landing + Dazed + 1.05f)) Play(ruffleClip, RuffleVolume);
            if (Crossed(prev, t, landed + Landing + Dazed + Rise + .3f)) Play(cryClip, CryVolume * .8f, .92f);
        }
    }
}
