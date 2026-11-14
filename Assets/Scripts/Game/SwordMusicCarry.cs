using UnityEngine;

namespace ChessFight.Game
{
    // R645 (11-14, 사용자 "브금도 카메라 돌아가는 연출을 할 때 까지 유지"): Sword Fight's entrance music (O1, the loading
    // screen's SwordMusic and SwordMusicLoop) goes on after the entrance screen has gone, over the camera's flight round
    // the map (SwordFightMatch.FlySeconds), and fades as 3·2·1 starts. The screen hands over the object its two music
    // sources live on (still playing, so nothing restarts or skips); the match calls Release once the flight is over
    // (or when it ends). One that is never released fades by itself after MaxLife.
    public sealed class SwordMusicCarry : MonoBehaviour
    {
        const float FadeSeconds = 1.2f, MaxLife = 20f;

        static SwordMusicCarry current;
        AudioSource[] sources;
        float gain, since, fadeFrom = -1f;

        public static bool Active => current != null;

        /// <summary>Takes over `holder` (its parent is cut, it survives scene loads) and its playing sources, kept at
        /// `gain` times the music volume until Release.</summary>
        public static void Carry(GameObject holder, float gain)
        {
            if (holder == null) return;
            if (current != null) Destroy(current.gameObject);
            holder.transform.SetParent(null, false);
            DontDestroyOnLoad(holder);
            current = holder.AddComponent<SwordMusicCarry>();
            current.sources = holder.GetComponents<AudioSource>();
            current.gain = gain;
            current.since = Time.unscaledTime;
        }

        /// <summary>The flight is over: fade out (and go). Nothing if no music is being carried.</summary>
        public static void Release()
        {
            if (current != null && current.fadeFrom < 0f) current.fadeFrom = Time.unscaledTime;
        }

        void Update()
        {
            if (fadeFrom < 0f && Time.unscaledTime - since > MaxLife) fadeFrom = Time.unscaledTime;
            float fade = fadeFrom < 0f ? 1f : 1f - (Time.unscaledTime - fadeFrom) / FadeSeconds;
            if (fade <= 0f) { Destroy(gameObject); return; }
            float volume = GameSettings.Music * gain * fade;
            foreach (var s in sources) if (s != null) s.volume = volume;
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }
    }
}
