using UnityEngine;

namespace ChessFight.Game
{
    // R863 (2026-11-27, the user's five tasks for 11-29: "소드 파이트 인게임 BGM"): Sword Fight's in-match music. Until now
    // the entrance screen's music (SwordMusicCarry) faded as 3·2·1 started and the fight itself had none.
    //
    //   Resources/SwordFightMusic/MatchMusic       the take's opening, played once (optional)
    //   Resources/SwordFightMusic/MatchMusicLoop   the bars that follow it and repeat seamlessly (required)
    //
    // Both go on the audio clock, the loop exactly where the opening ends, so the join is sample exact (as
    // PawnRushRoundMusic). Without MatchMusic the loop plays alone from its start; without MatchMusicLoop nothing plays
    // (one line in the log). The clip is the user's ElevenLabs take "Chessboard Corsairs" (11-27, 132 BPM, 80 s), cut by
    // ArtSource/SwordFight/Music/bake.py match into one 8-bar loop of 14.5 s (bars 32 -> 40) and no opening: the user,
    // after a match, "인게임도중에 구간 전환 구간이 있어서 뭔가 이어지는 느낌이 아니였어" - the take's 8-bar section changes
    // (bars 16, 24, 32, 40) broke the flow, and bars 32 -> 40 are its one stretch with none (every bar within 0.084 of the
    // two before it, the wrap too; the old 24-bar loop had 0.235). Gain is that script's GAIN: an RMS of -28 dBFS at the
    // default music volume, as the other music (Docs/PawnRush/PAWN_RUSH_AUDIO.md §19). Bake another take and Gain
    // changes with it.
    //
    // SwordFightMatch drives it: Play(true) from the start of 3·2·1 through sudden death, Play(false) for the result (the
    // winners' or the losers' ResultMusic takes over) and a new round; Duck() under the sword's hits, the skills' hits and
    // the points. It follows the music volume and runs on unscaled time (the result's slow motion). It is local audio:
    // every PC plays it from the match state it already has (no protocol change).
    [DisallowMultipleComponent]
    public sealed class SwordFightMusic : MonoBehaviour
    {
        const string Folder = "SwordFightMusic/";
        const float Gain = .4829f;              // bake.py's GAIN
        // R863 (user 11-27 after hearing it: "매칭 bgm이 끝나고 … 소리가 줄어들고 다시 나오게 하지 말고 그대로 쭉 긴장감
        // 있는 사운드로 시작"): no dip from the entrance music's peak into the match. The match music comes in as the
        // entrance music goes (3·2·1's start, the moment SwordMusicCarry releases: SwordFightMatch.Music), cross-fading
        // over the carry's 1.2 s, straight into the loud loop, at the entrance's peak level (-25 dB, Boost +3 dB over the
        // loop's -28) easing down to its own level over BoostEase seconds, too slowly to hear as a drop.
        // Then (user: "매칭에서 인게임으로 넘어갈 때 … 이질감 없게 자연스럽게"): the two songs are both 132 BPM, so with the
        // new entrance take this one starts on the entrance music's next downbeat (SwordMusicCarry.HandOverDsp), on its
        // own downbeat (the loop is cut LoopHit ahead of its first hit), and rises over the two bars the entrance music
        // takes to go (HandOverSeconds, timed on the audio clock), in step. A cut on the downbeat was tried first: the user
        // heard it "갑자기 튀어나오는 느낌".
        double crossFrom = -1, crossSeconds;
        const float Boost = 1.413f, BoostEase = 30f;
        const double LoopHit = .003;
        const float FadeIn = 1.2f, FadeOut = 1.2f;
        // A duck goes down fast, holds, then comes back slowly.
        const float DuckAttack = .06f, DuckRelease = .9f;
        const double Lead = .1;
        static bool warned;

        AudioSource opening, loop;
        float gain, fade, duck = 1f, duckTo = 1f, duckUntil, startedAt;
        bool wanted, started, missing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => warned = false;

        /// <summary>The music on `host` (added once).</summary>
        public static SwordFightMusic On(GameObject host)
        {
            if (host == null) return null;
            var m = host.GetComponent<SwordFightMusic>();
            return m != null ? m : host.AddComponent<SwordFightMusic>();
        }

        public bool Playing => started;

        void Awake()
        {
            var again = Resources.Load<AudioClip>(Folder + "MatchMusicLoop");
            var first = Resources.Load<AudioClip>(Folder + "MatchMusic");
            if (Application.isBatchMode || again == null)
            {
                missing = true;
                if (again == null && !warned && !Application.isBatchMode)
                    Debug.Log($"[ChessFight] 소드 파이트 경기 음악 파일이 아직 없습니다: Resources/{Folder}MatchMusicLoop (없으면 소리 없이 진행)");
                warned = true;
                enabled = false;
                return;
            }
            if (first != null) opening = Source(first, false);
            loop = Source(again, true);
            gain = Gain;
        }

        AudioSource Source(AudioClip clip, bool repeat)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.clip = clip;
            a.playOnAwake = false;
            a.loop = repeat;
            a.spatialBlend = 0f;
            a.bypassReverbZones = true;
            a.priority = 0;
            a.dopplerLevel = 0f;
            a.volume = 0f;
            return a;
        }

        /// <summary>Whether the music should be on: it starts from the top when it goes on, fades out when it goes off.</summary>
        public void Play(bool on) => wanted = on && !missing;

        /// <summary>The music drops to `to` (0..1 of its level) at once and stays there `hold` seconds before it comes
        /// back; a deeper or longer duck already running wins.</summary>
        public void Duck(float to, float hold)
        {
            if (!started) return;
            float now = Time.unscaledTime;
            bool running = now < duckUntil;
            duckTo = running ? Mathf.Min(duckTo, to) : to;
            duckUntil = running ? Mathf.Max(duckUntil, now + hold) : now + hold;
        }

        void Update()
        {
            if (wanted && !started)
            {
                started = true;
                fade = 0f;
                duck = duckTo = 1f;
                duckUntil = 0f;
                startedAt = Time.unscaledTime;
                double at = AudioSettings.dspTime + Lead;
                double handOver = SwordMusicCarry.HandOverDsp;
                if (handOver - LoopHit > AudioSettings.dspTime + .02)
                {
                    at = handOver - LoopHit;
                    crossFrom = handOver;
                    crossSeconds = System.Math.Max(.05, SwordMusicCarry.HandOverSeconds);
                }
                loop.timeSamples = 0;
                if (opening != null)
                {
                    opening.timeSamples = 0;
                    opening.PlayScheduled(at);
                    loop.PlayScheduled(at + (double)opening.clip.samples / opening.clip.frequency);
                }
                else loop.PlayScheduled(at);
            }
            if (!started) return;
            if (wanted && crossFrom >= 0)
            {
                fade = Mathf.Clamp01((float)((AudioSettings.dspTime - crossFrom) / crossSeconds));
                if (fade >= 1f) crossFrom = -1;
            }
            else
            {
                crossFrom = -1;
                fade = Mathf.MoveTowards(fade, wanted ? 1f : 0f, Time.unscaledDeltaTime / (wanted ? FadeIn : FadeOut));
            }
            if (Time.unscaledTime >= duckUntil) duckTo = 1f;
            duck = Mathf.MoveTowards(duck, duckTo, Time.unscaledDeltaTime / (duckTo < duck ? DuckAttack : DuckRelease));
            // In: an equal-power curve, so the cross-fade with the entrance music's linear fade has no hole. Out: linear.
            float shape = wanted ? Mathf.Sin(fade * Mathf.PI / 2f) : fade;
            float boost = Mathf.Lerp(Boost, 1f, Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - startedAt) / BoostEase));
            float volume = Mathf.Clamp01(GameSettings.Music * gain * boost * shape * duck);
            if (opening != null) opening.volume = volume;
            loop.volume = volume;
            if (!wanted && fade <= 0f) Stop();
        }

        void Stop()
        {
            started = false;
            if (opening != null) opening.Stop();
            if (loop != null) loop.Stop();
        }

        void OnDisable()
        {
            fade = 0f;
            wanted = false;
            Stop();
        }
    }
}
