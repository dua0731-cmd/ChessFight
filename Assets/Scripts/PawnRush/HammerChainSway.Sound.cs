using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R535 (승규 11-09 "아재 해머 소리"): the grand stair hall's hammers sound as they swing.
    //  whoosh  each swing, the whole of it (end to end, R542; 1.4 s since R547, the sound set the swing): rising as the ball leaves, loudest as it passes the
    //          middle, gone as it stops (a weapon swing, not wind: R539). On the ball, so it sweeps across as it passes.
    //  creak   each time it reaches an end and hangs there (the 0.2 s pause): the chain snapping tight, a dull low
    //          clunk (R536). At the pin.
    // Clips: Resources/PawnRushHallSfx (ArtSource/PawnRush/GrandStairHall/hall_hammer_sounds.py). Timed from the
    // pendulum's own angle (PendulumSwing, the shared obstacle clock), so every PC hears the same swing; ringing in
    // the stone hall and heard only inside it (SectionSoundGate).
    public sealed partial class HammerChainSway
    {
        const string HallSfx = "PawnRushHallSfx/";
        // R540 (승규 11-09 "해머 소리 훨씬 키워"): the swing at full (was 0.6), heard full out to 8 m (was 5), and its
        // file made denser with a soft limiter (hall_hammer_sounds.py; R541: one smooth swell, not two swishes).
        const float WhooshVolume = 1f, CreakVolume = .45f;
        const float AtEnd = .05f;   // degrees short of the swing's end that count as hanging there

        static AudioClip whooshClip, creakClip;
        static bool clipsLooked;

        // R544 (승규 11-09 "해머 구간 통과하면 다른 소리가 묻혀 - 입장하기 전에는 유지하고 통과하면 소리를 더 줄여"):
        // as loud as before while a player comes up to a hammer, much quieter once they have gone through its swing.
        // The players run through the swing's plane (the ball swings along the Pivot's x, about its z), so which side
        // of that plane the listener is on says before or past. The side counts as "before" where the listener first
        // comes within ApproachRange (players come up the course), and the before/past state changes only when the
        // listener crosses the plane close to the hammer (CrossRange), so the spiral coming round above or below it
        // later does not flip it.
        const float ApproachRange = 30f, CrossRange = 9f, PlaneSlack = .6f;
        const float PassedGain = .25f, PassedFar = 18f, FadeRate = 3f;   // about -12 dB, heard to 18 m (was 45)
        static AudioListener ear;

        PendulumSwing swing;
        AudioSource whooshVoice, creakVoice;
        bool soundReady, wasAtEnd, passed;
        int approachSide;   // +1 / -1 along the Pivot's z, 0 until the listener first comes near
        float gain = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSoundStatics()
        {
            ear = null;
            whooshClip = creakClip = null;
            clipsLooked = false;
        }

        static AudioClip Clip(string name)
        {
            var clip = Resources.Load<AudioClip>(HallSfx + name);
            if (clip == null) Debug.LogWarning("[대계단 홀] 소리 파일이 없어 재생하지 않습니다: Resources/" + HallSfx + name);
            return clip;
        }

        void Sound()
        {
            if (!soundReady)
            {
                soundReady = true;
                if (!clipsLooked)
                {
                    clipsLooked = true;
                    whooshClip = Clip("HallHammerWhoosh");
                    creakClip = Clip("HallHammerCreak");
                }
                swing = pivot != null ? pivot.GetComponent<PendulumSwing>() : null;
                if (swing == null) return;
                whooshVoice = Voice("Hammer whoosh", ball != null ? ball : pivot, 8f, 45f, WhooshVolume);
                creakVoice = Voice("Hammer creak", pivot, 4f, 30f, CreakVolume);
                wasAtEnd = AtSwingEnd();
                return;
            }
            if (swing == null || !swing.isActiveAndEnabled) return;

            bool atEnd = AtSwingEnd();
            if (atEnd && !wasAtEnd) Play(creakVoice, creakClip, Random.Range(.96f, 1.03f), 0f);   // reached an end
            else if (!atEnd && wasAtEnd) Play(whooshVoice, whooshClip, 1f, SinceLeft());          // left it: a swing
            wasAtEnd = atEnd;
        }

        // Volume in Update, before SectionSoundGate scales it in LateUpdate (Sound runs in LateUpdate, too late for that).
        void Update()
        {
            if (whooshVoice == null || swing == null) return;
            if (ear == null || !ear.isActiveAndEnabled) ear = FindAnyObjectByType<AudioListener>();
            if (ear != null)
            {
                var at = swing.transform.position;
                var axis = swing.transform.forward;   // the swing turns about this, so it stays put as it swings
                var to = ear.transform.position - at;
                float along = Vector3.Dot(to, axis);
                var flat = to - axis * along;
                flat.y = 0f;
                if (approachSide == 0 && to.magnitude < ApproachRange && Mathf.Abs(along) > PlaneSlack)
                    approachSide = along > 0f ? 1 : -1;
                if (approachSide != 0 && flat.magnitude < CrossRange && Mathf.Abs(along) > PlaneSlack)
                    passed = (along > 0f ? 1 : -1) != approachSide;
            }
            gain = Mathf.MoveTowards(gain, passed ? PassedGain : 1f, FadeRate * Time.deltaTime);
            float far = Mathf.Lerp(PassedFar, 45f, Mathf.InverseLerp(PassedGain, 1f, gain));
            whooshVoice.volume = WhooshVolume * gain;
            whooshVoice.maxDistance = far;
            creakVoice.volume = CreakVolume * gain;
            creakVoice.maxDistance = Mathf.Min(30f, far);
        }

        bool AtSwingEnd() => Mathf.Abs(swing.Angle) >= Mathf.Clamp(swing.SwingAngle, 0f, 85f) - AtEnd;

        // R542 (승규 11-09 "한 번 스윙하고 멈춤 지점까지 소리가 이어져야 돼"): the whoosh is one whole swing long
        // (SwingDuration, start to stop: 1.4 s in the hall since R547), so it plays at its own pitch (a pitch is a speed) and from how far the
        // swing already is: the angle has to move AtEnd degrees before the start is seen, and the swing goes
        // angle = -A cos(pi t / T), so t = T / pi * acos(1 - |moved| / A).
        float SinceLeft()
        {
            float amp = Mathf.Max(.01f, Mathf.Clamp(swing.SwingAngle, 0f, 85f));
            float moved = Mathf.Clamp01(1f - Mathf.Abs(swing.Angle) / amp);
            return Mathf.Max(.3f, swing.SwingDuration) / Mathf.PI * Mathf.Acos(1f - moved);
        }

        void Play(AudioSource voice, AudioClip clip, float pitch, float from)
        {
            if (voice == null || clip == null || from >= clip.length) return;
            voice.clip = clip;
            voice.pitch = pitch;
            voice.time = from;
            voice.Play();
        }

        // Its own child so the hall's ring and gate cover only the hammer's sounds.
        static AudioSource Voice(string name, Transform under, float near, float far, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(under, false);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = near;
            a.maxDistance = far;
            a.dopplerLevel = 0f;
            a.volume = volume;
            RollingSpawner.Ring(go);
            SectionSoundGate.Gate(a);   // heard only in the hall
            return a;
        }
    }
}
