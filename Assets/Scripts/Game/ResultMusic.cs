using UnityEngine;

namespace ChessFight.Game
{
    // R679 (the user after the 6 vs 6 test, 11-16: "승리,패배 BGM 추가"): the music of a match's result - one piece
    // for the team that won, another for the team that lost. It plays once (not looped) from the moment the result
    // is shown: the Pawn Rush result scenes (PawnRushResultDirector) and Sword Fight's result (SwordFightMatch's
    // end stamp; a draw has none). It ends with its scene, so the lobby's own music takes over there.
    //
    //   Resources/ResultMusic/Victory   the winners' piece
    //   Resources/ResultMusic/Defeat    the losers' piece
    //
    // Until a file is there nothing plays (one line in the log). No bake step is needed for a take dropped in: its
    // level is measured when it loads and set to the other music's - an RMS of -28 dBFS at the default music volume
    // (LobbyMusic, PawnRushRoundMusic) - and it follows the music volume. The prompts for the two takes are in
    // Docs/PawnRush/PAWN_RUSH_AUDIO.md.
    [DisallowMultipleComponent]
    public sealed class ResultMusic : MonoBehaviour
    {
        const string Folder = "ResultMusic/";
        const float TargetRms = .0398f;         // -28 dBFS
        const float DefaultMusic = .7f;         // GameSettings.Music's default, where the target holds
        const float FallbackGain = .37f;        // a clip whose samples cannot be read (a streamed one): the race music's
        const float FadeIn = .25f;
        static bool warned;

        AudioSource source;
        float gain, fade;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => warned = false;

        /// <summary>Plays the winners' or the losers' piece once on `host`; it stops when the host goes with its scene.
        /// Null (and silence) while the file does not exist.</summary>
        public static ResultMusic Play(GameObject host, bool won)
        {
            if (host == null || !Application.isPlaying) return null;
            string name = won ? "Victory" : "Defeat";
            var clip = Resources.Load<AudioClip>(Folder + name);
            if (clip == null)
            {
                if (!warned) Debug.Log($"[ChessFight] 결과 음악 파일이 아직 없습니다: Resources/{Folder}{name} (없으면 소리 없이 진행)");
                warned = true;
                return null;
            }
            var music = host.GetComponent<ResultMusic>();
            if (music == null) music = host.AddComponent<ResultMusic>();
            music.Begin(clip);
            return music;
        }

        void Begin(AudioClip clip)
        {
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.bypassReverbZones = true;
                source.priority = 0;
                source.dopplerLevel = 0f;
            }
            gain = Level(clip);
            fade = 0f;
            source.clip = clip;
            source.volume = 0f;
            source.Play();
        }

        // The code gain that plays this clip at the other music's level (the bake scripts' formula).
        static float Level(AudioClip clip)
        {
            try
            {
                int frames = Mathf.Min(clip.samples, clip.frequency * 30);
                var data = new float[frames * clip.channels];
                if (data.Length == 0 || !clip.GetData(data, 0)) return FallbackGain;
                double sum = 0;
                for (int i = 0; i < data.Length; i++) sum += (double)data[i] * data[i];
                float rms = Mathf.Sqrt((float)(sum / data.Length));
                return rms > 1e-4f ? Mathf.Clamp(TargetRms / (rms * DefaultMusic), .05f, 1.5f) : FallbackGain;
            }
            catch (System.Exception)
            {
                return FallbackGain;
            }
        }

        // Unscaled: Sword Fight's result runs in slow motion.
        void Update()
        {
            if (source == null) return;
            fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / FadeIn);
            source.volume = Mathf.Clamp01(GameSettings.Music * gain * fade);
        }
    }
}
