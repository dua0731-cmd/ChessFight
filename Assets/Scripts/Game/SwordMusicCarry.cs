using UnityEngine;

namespace ChessFight.Game
{
    // R645 (11-14, 사용자 "브금도 카메라 돌아가는 연출을 할 때 까지 유지"): Sword Fight's entrance music (O1, the loading
    // screen's SwordMusic and SwordMusicLoop) goes on after the entrance screen has gone, over the camera's flight round
    // the map (SwordFightMatch.FlySeconds), and fades as 3·2·1 starts. The screen hands over the object its two music
    // sources live on (still playing, so nothing restarts or skips); the match calls Release once the flight is over
    // (or when it ends). One that is never released fades by itself after MaxLife.
    //
    // R863 (11-27, 사용자 "매칭에서 인게임으로 넘어갈 때 … 이질감 없게 자연스럽게"): with a beat grid (the new entrance take,
    // 132 BPM like the match music) Release does not fade over 1.2 s across the match music - two songs out of step for a
    // second. It picks the entrance music's next downbeat at least HandLead ahead (HandOverDsp, on the audio clock); the
    // match music (SwordFightMusic) starts there on its own downbeat and the two cross over the HandBars bars after it
    // (equal power: this one on a cosine, that one on a sine), then the audio clock stops this one - two songs in step,
    // one rising as the other goes. (A cut on the downbeat alone was tried first: the user heard the match music "갑자기
    // 튀어나오는 느낌".) Without a grid (O1) it is as before.
    public sealed class SwordMusicCarry : MonoBehaviour
    {
        const float FadeSeconds = 1.2f, MaxLife = 20f;
        const double HandLead = .15, HandBars = 2;

        static SwordMusicCarry current;
        AudioSource[] sources;
        float gain, since, fadeFrom = -1f;
        double gridAt, bar, handOver = -1;

        public static bool Active => current != null;

        /// <summary>The audio-clock time the match music should start at (a downbeat of the entrance music), or below 0
        /// when there is none (no grid, or not released yet). Set by Release; cleared by the next Carry.</summary>
        public static double HandOverDsp { get; private set; } = -1;
        /// <summary>How long the two songs cross from HandOverDsp (two bars of the grid).</summary>
        public static double HandOverSeconds { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { current = null; HandOverDsp = -1; }

        /// <summary>Takes over `holder` (its parent is cut, it survives scene loads) and its playing sources, kept at
        /// `gain` times the music volume until Release. `downbeatDsp` and `barSeconds` (R863), when known, are one of
        /// the music's downbeats on the audio clock and its bar's length.</summary>
        public static void Carry(GameObject holder, float gain, double downbeatDsp = 0, double barSeconds = 0)
        {
            if (holder == null) return;
            if (current != null) Destroy(current.gameObject);
            HandOverDsp = -1;
            holder.transform.SetParent(null, false);
            DontDestroyOnLoad(holder);
            current = holder.AddComponent<SwordMusicCarry>();
            current.sources = holder.GetComponents<AudioSource>();
            current.gain = gain;
            current.since = Time.unscaledTime;
            current.gridAt = downbeatDsp;
            current.bar = barSeconds;
        }

        /// <summary>The flight is over: fade out (and go) - on the next downbeat when the beat grid is known. Nothing if
        /// no music is being carried.</summary>
        public static void Release()
        {
            if (current == null || current.fadeFrom >= 0f || current.handOver >= 0) return;
            if (current.bar > .1)
            {
                double earliest = AudioSettings.dspTime + HandLead;
                double k = System.Math.Ceiling((earliest - current.gridAt) / current.bar);
                current.handOver = current.gridAt + k * current.bar;
                HandOverSeconds = HandBars * current.bar;
                HandOverDsp = current.handOver;
                foreach (var s in current.sources)
                    if (s != null) s.SetScheduledEndTime(current.handOver + HandOverSeconds + .05);
                return;
            }
            current.fadeFrom = Time.unscaledTime;
        }

        void Update()
        {
            float fade;
            if (handOver >= 0)
            {
                double after = AudioSettings.dspTime - handOver;
                if (after > HandOverSeconds + .1) { Destroy(gameObject); return; }
                fade = Mathf.Cos(Mathf.Clamp01((float)(after / HandOverSeconds)) * Mathf.PI / 2f);   // equal power, against SwordFightMusic's sine
            }
            else
            {
                if (fadeFrom < 0f && Time.unscaledTime - since > MaxLife) fadeFrom = Time.unscaledTime;
                fade = fadeFrom < 0f ? 1f : 1f - (Time.unscaledTime - fadeFrom) / FadeSeconds;
                if (fade <= 0f) { Destroy(gameObject); return; }
            }
            float volume = GameSettings.Music * gain * fade;
            foreach (var s in sources) if (s != null) s.volume = volume;
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }
    }
}
