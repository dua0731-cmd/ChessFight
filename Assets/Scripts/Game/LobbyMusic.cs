using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.Game
{
    // R651 (11-14, 사용자 "이게 결과물인데 폰러시보단 로비 BGM으로 쓰면 딱일 것 같아. 대신 루프로 만들어야해"): the lobby's
    // music, an ElevenLabs Music v2.5 take first made for the Pawn Rush race (120 BPM, G minor, 164 s).
    // ArtSource/Lobby/Music/bake.py cuts it into Resources/Lobby/Music/LobbyMusic.wav, the take's first 28 s, and
    // LobbyMusicLoop.wav, the 96 s (48 bars) that follow, whose end runs into its own start. Both go on the audio clock,
    // the loop exactly where the opening ends (as the entrance screens' music does), so that join is sample exact; then
    // the loop repeats by itself.
    //
    // R654 (11-15, 사용자 "로비 BGM은 인트로씬의 체스파이트 제목 연출 이후 … 화면이 나왔을 때부터 게임 매칭 전까지가 재생
    // 범위야"): one piece of music from the title screen to the matching. It starts when the opening logo has played
    // (IntroController, LogoIntro's done) and carries on, not restarted, through the scene change into the lobby
    // (it outlives scene loads). It fades out while this PC is matching - searching, following its party leader's
    // search, or in a match room (Hold, set by LobbyBootstrap) - and back in if that is cancelled; once an entrance
    // screen's own music starts (LoadingScreenView.MusicPlaying) it is quiet too. A scene other than the title or the
    // lobby (a match) ends it; the lobby starts it from the top again when it comes back. It follows the music volume,
    // at the race music's level (-28 dB RMS at the default).
    [DisallowMultipleComponent]
    public sealed class LobbyMusic : MonoBehaviour
    {
        const string Folder = "Lobby/Music/";
        // R654: -28 dB RMS at the default music volume, the race music's level (bake.py's LOBBY_GAIN .5014 was -26).
        const float Gain = .3983f;
        const float FadeIn = 1.5f, FadeOut = .8f;
        // How far ahead the first start is put on the audio clock.
        const double Lead = .1;

        static LobbyMusic current;

        /// <summary>True while the music should be quiet in the lobby (this PC matching or in a match room); set by the
        /// lobby scene, null elsewhere.</summary>
        public static Func<bool> Hold;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;
            Hold = null;
        }

        /// <summary>Starts the music if it is not already playing (the title screen, or the lobby after a match).</summary>
        public static void Begin()
        {
            if (current != null) return;
            var go = new GameObject("Lobby music");
            DontDestroyOnLoad(go);
            current = go.AddComponent<LobbyMusic>();
        }

        AudioSource opening, loop;
        float fade;

        void Start()
        {
            var first = Resources.Load<AudioClip>(Folder + "LobbyMusic");
            var again = Resources.Load<AudioClip>(Folder + "LobbyMusicLoop");
            if (first == null || again == null)
            {
                Destroy(gameObject);
                return;
            }
            opening = Source(first, false);
            loop = Source(again, true);
            double at = AudioSettings.dspTime + Lead;
            opening.PlayScheduled(at);
            loop.PlayScheduled(at + (double)first.samples / first.frequency);
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

        void Update()
        {
            if (opening == null) return;
            string scene = SceneManager.GetActiveScene().name;
            bool menu = scene == SceneNames.Intro || scene == SceneNames.Lobby;
            bool held = Hold != null && Hold();
            bool wanted = menu && !held && !LoadingScreenView.MusicPlaying;
            fade = Mathf.MoveTowards(fade, wanted ? 1f : 0f, Time.unscaledDeltaTime / (wanted ? FadeIn : FadeOut));
            opening.volume = loop.volume = GameSettings.Music * Gain * fade;
            if (!menu && fade <= 0f) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }
    }
}
