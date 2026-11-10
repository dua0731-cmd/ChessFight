using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R531 (승규 11-09 "이제 대관홀 차근차근 사운드 넣어 계단에 장애물 구르는 소리"): the grand stair's rolls are heard.
    //  roll   a loop on each roll while it comes down: the marble pawn a deep stone rumble, the carpet a soft fabric
    //         one; quieter while it is in the air off a nosing, fading out as it sinks into the drain.
    //  step   one hit at every landing of its path (Track.hits: where along the way and how fast it lands), louder the
    //         harder it lands: the pawn a heavy marble thud, the carpet a muffled thump.
    // Clips: Resources/PawnRushHallSfx (ArtSource/PawnRush/GrandStairHall/hall_roll_sounds.py). Every PC plays them from
    // the shared clock, as it draws the rolls. They ring in the stone hall and are heard only inside it (SectionSoundGate);
    // inside, the near ones loud and the far ones quiet.
    public sealed partial class RollingSpawner
    {
        const string HallSfx = "PawnRushHallSfx/";
        const float StatueLoopLevel = .75f, CarpetLoopLevel = .5f;
        // 승규 11-09 "지금 보다 조금만 더 작게": every roll sound at 80 % (about -2 dB),
        // then "여기서 20퍼 더 낮춰": 80 % of that, 64 % (about -3.9 dB)
        const float RollVolume = .64f;

        // 승규 11-09 "카펫 쪽에서 떨어지는 거랑 대리석에서 떨어지는 사운드 구별해": the stair's crimson runner is 9 m wide
        // (royal_stair_v45.py CARPET 4.5, the hall's frame), so the two middle lanes land on carpet - a dull padded
        // thud - and the two outer ones on bare marble - the hard knock.
        const float RunnerHalfWidth = 4.5f;

        // R548 (승규 11-10 "사탕 소리가 나 - 카펫 구간과 대리석 바닥 떨어지는 소리 구별해 주고, 계단 > 평지 > 계단 구간이라 그 소리도
        // 맞게"): the way down is stair, the middle landing (flat), stair, then the flat foot. On a flat stretch the roll
        // rumbles along (the loop at full); on a stair it does not roll, it drops a step at a time, so there the loop is
        // only a low undertone and each step is one heavy hit. The small after-bounces of a step (the carpet's, under
        // StepMinSpeed) make no sound - they were the light rattle. The runner (|x| < 4.5) runs from the stair's foot
        // (z 14) over both flights and the landing; on it the roll's rumble is muffled (a low-pass on its voices) and the
        // hits are the padded ones; off it (the outer lanes, and the marble foot below z 14) it is bare stone.
        const float StairLoopShare = .18f, StepMinSpeed = 1.6f, RunnerFromZ = 14f;
        const float RunnerCutoff = 900f, BareCutoff = 22000f;
        // R550 (승규 11-10 "카펫 구간에 있을 때 소리를 더 키워"): on the runner the roll and its hits are 1.5 times as loud
        // (about +3.5 dB; the runner's hit files are also denser) - the muffled sounds were lost there.
        const float RunnerBoost = 1.5f;

        static AudioClip statueLoopClip, statueStepClip, carpetLoopClip, carpetStepClip;
        static AudioClip statueOnRunnerClip, carpetOnRunnerClip;
        static AudioClip warnClip;
        static bool clipsLooked;

        // R532 (승규 11-09 "다음 작업": the hole warning, next on the hall's list): when a hole lights up 0.8 s before its
        // roll, a low stone rumble swells out of it and ends as the roll comes up - you hear which lane is next.
        const float WarnLevel = .7f;
        AudioSource[] warnVoices;
        long[] warnedSerial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSoundStatics()
        {
            statueLoopClip = statueStepClip = carpetLoopClip = carpetStepClip = null;
            statueOnRunnerClip = carpetOnRunnerClip = null;
            warnClip = null;
            clipsLooked = false;
        }

        static void LoadClips()
        {
            if (clipsLooked) return;
            clipsLooked = true;
            statueLoopClip = Clip("HallRollStatueLoop");
            statueStepClip = Clip("HallRollStatueStep");
            carpetLoopClip = Clip("HallRollCarpetLoop");
            carpetStepClip = Clip("HallRollCarpetStep");
            statueOnRunnerClip = Clip("HallRollStatueStepRunner");
            carpetOnRunnerClip = Clip("HallRollCarpetStepRunner");
            warnClip = Clip("HallRollHoleWarn");
        }

        // Once per roll, from the moment its hole lights (FixedUpdate). A PC that first sees the roll part way into its
        // warning starts the rumble that far in; one that sees it only in the last 0.3 s stays quiet.
        void Warn(RollingSchedule.Roll e, float age)
        {
            int lane = e.Lane;
            if (warnedSerial == null || warnedSerial.Length != laneX.Length)
            {
                warnedSerial = new long[laneX.Length];
                for (int i = 0; i < warnedSerial.Length; i++) warnedSerial[i] = long.MinValue;
                warnVoices = new AudioSource[laneX.Length];
            }
            if (warnedSerial[lane] == e.Serial) return;
            warnedSerial[lane] = e.Serial;
            LoadClips();
            float into = warning + age;
            if (warnClip == null || into > warning - .3f) return;
            var voice = warnVoices[lane];
            if (voice == null)
            {
                // its own object at the hole: the glow is switched off between rolls, and a sound on it would stop
                var at = new GameObject("Hole sound " + (lane + 1));
                at.transform.SetParent(transform, false);
                at.transform.localPosition = new Vector3(laneX[lane], Profile(holeZ) + .3f, holeZ);
                voice = warnVoices[lane] = Voice(at, 4f, 36f);
                voice.clip = warnClip;
                voice.volume = WarnLevel * RollVolume;
                Ring(at);
                SectionSoundGate.Gate(voice);
            }
            voice.pitch = Mathf.Lerp(.94f, 1.04f, Hash01(e.Serial, 0, 11)) * (e.Statue ? .94f : 1.04f);
            voice.time = Mathf.Min(into, warnClip.length - .01f);
            voice.Play();
        }

        static AudioClip Clip(string name)
        {
            var clip = Resources.Load<AudioClip>(HallSfx + name);
            if (clip == null) Debug.LogWarning("[대계단 홀] 소리 파일이 없어 재생하지 않습니다: Resources/" + HallSfx + name);
            return clip;
        }

        // In Update, not LateUpdate: SectionSoundGate multiplies the volumes set here in its LateUpdate.
        void Update()
        {
            if (live.Count == 0) return;
            LoadClips();
            double now = ObstacleClock.Now;
            foreach (var item in live.Values)
                if (item.root.activeSelf && item.picture != null) Sound(item, (float)(now - item.roll.Time));
        }

        void Sound(Item item, float age)
        {
            if (item.rollLoop == null) MakeVoices(item);
            float r = (item.statue ? statueDiameter : carpetDiameter) * .5f;
            float along = Mathf.Max(0f, age) * speed, z = holeZ - along;
            var track = TrackFor(item.statue);

            // the loop: on the ground full, in the air a quarter, into the drain to nothing
            float laneAt = item.roll.Lane < laneX.Length ? laneX[item.roll.Lane] : 0f;
            bool onRunner = Mathf.Abs(laneAt) < RunnerHalfWidth && z >= RunnerFromZ;
            float target = 0f;
            if (age >= 0f)
            {
                float level = item.statue ? StatueLoopLevel : CarpetLoopLevel;
                bool air = Sample(track, along) > Envelope(z, r) + .04f;
                float drain = Mathf.Clamp01((z - (drainZ - 1f)) / 1f);
                // R548: rolls on the flat, drops step by step on a stair (the hits carry it there)
                target = level * (OnStair(z) ? StairLoopShare : air ? .25f : 1f) * drain * RollVolume * (onRunner ? RunnerBoost : 1f);
            }
            if (item.muffle != null)
                item.muffle.cutoffFrequency = Mathf.MoveTowards(item.muffle.cutoffFrequency, onRunner ? RunnerCutoff : BareCutoff,
                    Time.deltaTime * 40000f);
            var loop = item.rollLoop;
            loop.volume = Mathf.MoveTowards(loop.volume, target, Time.deltaTime * 6f);
            if (loop.clip != null && !loop.isPlaying && target > 0f)
            {
                loop.time = Hash01(item.serial, 0, 9) * loop.clip.length;   // two rolls never share a seam
                loop.Play();
            }
            else if (loop.isPlaying && target <= 0f && loop.volume <= 0f) loop.Stop();

            // the steps: each landing passed since the last frame (a roll seen first half way down skips the old ones)
            var hits = track.hits;
            var stepClip = onRunner ? (item.statue ? statueOnRunnerClip : carpetOnRunnerClip)
                                    : (item.statue ? statueStepClip : carpetStepClip);
            while (item.nextHit < hits.Count && hits[item.nextHit].x <= along)
            {
                var h = hits[item.nextHit];
                if (stepClip != null && along - h.x < .6f && h.y >= StepMinSpeed)
                {
                    float gain = Mathf.Lerp(.6f, 1f, Mathf.InverseLerp(StepMinSpeed, 4f, h.y)) * RollVolume * (onRunner ? RunnerBoost : 1f);
                    item.rollHit.pitch = Mathf.Lerp(.95f, 1.03f, Hash01(item.serial, item.nextHit, 7));
                    item.rollHit.PlayOneShot(stepClip, gain);
                }
                item.nextHit++;
            }
        }

        void MakeVoices(Item item)
        {
            var at = item.picture.gameObject;
            bool statue = item.statue;
            item.rollLoop = Voice(at, statue ? 4f : 3f, statue ? 42f : 30f);
            item.rollLoop.clip = statue ? statueLoopClip : carpetLoopClip;
            item.rollLoop.loop = true;
            item.rollLoop.volume = 0f;
            item.rollLoop.pitch = Mathf.Lerp(.95f, 1.05f, Hash01(item.serial, 0, 8));
            item.rollHit = Voice(at, statue ? 5f : 3f, statue ? 48f : 32f);
            Ring(at);                                // one stone-hall ring for both
            item.muffle = at.AddComponent<AudioLowPassFilter>();   // R548: the runner muffles the roll
            item.muffle.cutoffFrequency = BareCutoff;
            SectionSoundGate.Gate(item.rollLoop);   // heard only in the hall
        }

        // On one of the stair's flights (not the middle landing or the foot): rolls drop a step at a time there.
        bool OnStair(float z)
        {
            foreach (var s in stairs)
                if (z >= s.x && z <= s.y) return true;
            return false;
        }

        // The stone hall's ring (every AudioSource on the GameObject goes through it). A bigger sound may ring longer
        // (decay, s) and louder (millibels over the usual reflections and tail): R570 the falling knight's landing.
        internal static void Ring(GameObject at, float decay = 2.2f, float louder = 0f)
        {
            var ring = at.AddComponent<AudioReverbFilter>();
            ring.reverbPreset = AudioReverbPreset.User;
            ring.dryLevel = 0f;
            ring.room = -200f;
            ring.roomHF = -400f;          // stone keeps the lows longer than the highs
            ring.decayTime = decay;
            ring.decayHFRatio = .6f;
            ring.reflectionsLevel = -500f + louder;
            ring.reverbLevel = -300f + louder;
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
            return a;
        }
    }
}
