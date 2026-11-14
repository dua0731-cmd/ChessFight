using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    /// <summary>
    /// R652 (사용자 11-14 "지금까지 만든 소리들이야. 전부 적용시켜줘"): the Pawn Rush race music, the user's ElevenLabs Music
    /// v2.5 take from the comedic brass band chase prompt (150 BPM, F major). ArtSource/PawnRush/Music/bake.py cuts it into
    /// Resources/PawnRushMusic/RaceMusic.wav, the take's first 28.4 s, and RaceMusicLoop.wav, the 89.6 s (56 bars) that
    /// follow, whose end runs into its own start. Both go on the audio clock, the loop exactly where the opening ends, so
    /// that join is sample exact; then the loop repeats by itself (as the lobby music, Game/LobbyMusic.cs).
    ///
    /// It starts as the entrance screen goes (the screen's own music hands over to it), fades in over a second, and
    /// fades out over a second once a team has won; a round that starts again in the scene starts it from the top. The
    /// section test scene builds the same round, so it has the same music. It follows the music volume. (The R646 work
    /// that first played a made-in-code loop here was reset away before it was committed; this replaces it.)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PawnRushRoundMusic : MonoBehaviour
    {
        const string Folder = "PawnRushMusic/";
        // bake.py's RACE_GAIN: an RMS of -28 dBFS at the default music volume, under the course's sounds and 2 dB
        // below the lobby music. Bake another take and this changes with it.
        const float Gain = .3687f;
        const float FadeIn = 1f, FadeOut = 1f;
        const double Lead = .1;

        PawnRushRound round;
        AudioSource opening, loop;
        float fade;
        bool started;

        void Awake()
        {
            round = GetComponent<PawnRushRound>();
            var first = Resources.Load<AudioClip>(Folder + "RaceMusic");
            var again = Resources.Load<AudioClip>(Folder + "RaceMusicLoop");
            if (round == null || first == null || again == null)
            {
                enabled = false;
                return;
            }
            opening = Source(first, false);
            loop = Source(again, true);
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
            var finish = round.Finish;
            bool racing = round.HasBuilt && !LoadingScreenView.Showing
                          && (finish == null || finish.Winner == PawnRushFinish.None);
            if (racing && !started)
            {
                started = true;
                double at = AudioSettings.dspTime + Lead;
                opening.PlayScheduled(at);
                loop.PlayScheduled(at + (double)opening.clip.samples / opening.clip.frequency);
            }
            fade = Mathf.MoveTowards(fade, racing ? 1f : 0f, Time.unscaledDeltaTime / (racing ? FadeIn : FadeOut));
            opening.volume = loop.volume = GameSettings.Music * Gain * fade;
            if (!racing && started && fade <= 0f)
            {
                opening.Stop();
                loop.Stop();
                started = false;
            }
        }

        void OnDisable()
        {
            fade = 0f;
            started = false;
            if (opening != null) opening.Stop();
            if (loop != null) loop.Stop();
        }
    }
}
