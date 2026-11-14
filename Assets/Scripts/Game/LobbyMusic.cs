using UnityEngine;

namespace ChessFight.Game
{
    // R651 (11-14, 사용자 "이게 결과물인데 폰러시보단 로비 BGM으로 쓰면 딱일 것 같아. 대신 루프로 만들어야해"): the lobby's
    // music, an ElevenLabs Music v2.5 take first made for the Pawn Rush race (120 BPM, G minor, 164 s).
    // ArtSource/Lobby/Music/bake.py cuts it into Resources/Lobby/Music/LobbyMusic.wav, the take's first 28 s, and
    // LobbyMusicLoop.wav, the 96 s (48 bars) that follow, whose end runs into its own start. Both go on the audio clock,
    // the loop exactly where the opening ends (as the entrance screens' music does), so that join is sample exact; then
    // the loop repeats by itself.
    //
    // It fades in as the lobby comes up and goes on under the gate walk and the matching screen. Once an entrance
    // screen's own music starts (LoadingScreenView.MusicPlaying: the course reveal's, Sword Fight's) it fades out, and
    // back in if that screen goes while the lobby is still here. Leaving the lobby scene ends it with the scene. It
    // follows the music volume, not the effects one.
    [DisallowMultipleComponent]
    public sealed class LobbyMusic : MonoBehaviour
    {
        const string Folder = "Lobby/Music/";
        // bake.py's LOBBY_GAIN: an RMS of -26 dBFS at the default music volume, about where the Sword Fight entrance
        // music plays. Bake another take and this changes with it.
        const float Gain = .5014f;
        const float FadeIn = 1.5f, FadeOut = .6f;
        // How far ahead the first start is put on the audio clock.
        const double Lead = .1;

        AudioSource opening, loop;
        float fade;

        void Start()
        {
            var first = Resources.Load<AudioClip>(Folder + "LobbyMusic");
            var again = Resources.Load<AudioClip>(Folder + "LobbyMusicLoop");
            if (first == null || again == null)
            {
                enabled = false;
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
            bool wanted = !LoadingScreenView.MusicPlaying;
            fade = Mathf.MoveTowards(fade, wanted ? 1f : 0f, Time.unscaledDeltaTime / (wanted ? FadeIn : FadeOut));
            opening.volume = loop.volume = GameSettings.Music * Gain * fade;
        }
    }
}
