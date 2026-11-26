using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R854 (user 11-26: "첫번째 음악을 비행으로 두번 째 음악을 튜토리얼 조작으로 해서 자연스럽게"): the tutorial's music, the
    /// user's two ElevenLabs Music takes. ArtSource/Tutorial/Music/bake.py joins them into Resources/TutorialMusic/:
    /// TutorialMusic, the opening's flight with the lessons' theme (brought down into the flight's key) fading in under
    /// its tail; and TutorialMusicLoop, the theme's bars that repeat. Both go on the audio clock, so the join is sample exact.
    ///
    /// It starts with the eagle's clock (not while the scene transition covers the screen); without an opening it starts
    /// at the lessons' theme. It fades out when the tutorial is finished or left, and follows the music volume.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialMusic : MonoBehaviour
    {
        const string Folder = "TutorialMusic/";
        // Where the first lesson begins in TutorialMusic (the theme is fading in by then), and bake.py's GAIN (-28 dBFS
        // at the default music volume, as the race music).
        const double LessonsAt = 12.14;
        const float Gain = .5767f;
        const float FadeOut = 1f;
        const double Lead = .05;

        TutorialCourse course;
        AudioSource opening, loop;
        float fade = 1f;
        bool started;

        void Awake()
        {
            course = GetComponent<TutorialCourse>();
            var first = Resources.Load<AudioClip>(Folder + "TutorialMusic");
            var again = Resources.Load<AudioClip>(Folder + "TutorialMusicLoop");
            if (Application.isBatchMode || course == null || first == null || again == null)
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
            bool over = course.Finished || SceneTransition.Busy;
            if (!started)
            {
                if (over) return;
                // With the opening: from its first moving frame, at the same time into it.
                double from = TutorialIntro.Playing ? TutorialIntro.Clock : LessonsAt;
                if (from <= 0d) return;
                started = true;
                opening.time = (float)from;
                double at = AudioSettings.dspTime + Lead;
                opening.PlayScheduled(at);
                loop.PlayScheduled(at + (double)opening.clip.samples / opening.clip.frequency - from);
            }
            if (over) fade = Mathf.MoveTowards(fade, 0f, Time.unscaledDeltaTime / FadeOut);
            opening.volume = loop.volume = GameSettings.Music * Gain * fade;
            if (fade <= 0f)
            {
                opening.Stop();
                loop.Stop();
                enabled = false;
            }
        }
    }
}
