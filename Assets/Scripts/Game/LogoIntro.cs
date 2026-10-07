using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace ChessFight.Game
{
    // The opening logo (R84, Docs/Architecture/UI.md "시작 로고"): CHESS FIGHT's
    // letters slam in one by one, faster and faster ("팍! 팍팍 파파파팍"), the
    // checker strip between the words runs like a race track, and the crown drops
    // onto the C with a ding. It is the video rendered from the chosen design page,
    // with its sound (sample A) inside, played once per run of the game over the
    // title screen. A click or any key skips it; if the video cannot play, the
    // title shows at once.
    [DisallowMultipleComponent]
    public sealed class LogoIntro : MonoBehaviour
    {
        // Over every other panel (the settings window is 200).
        const float SortingOrder = 300f;
        // The motion settles by 4.5 s; the logo holds, then fades into the title.
        const float FadeAt = 5.5f, FadeSeconds = .6f, SkipFadeSeconds = .3f;
        // A video that has not started by then is given up on; input in the
        // first moment is not taken as a skip. Both count shown frames, each at
        // most MaxStep: the first frame after loading can take seconds (3.7 s in
        // the editor), and that wait used to give up on the video before it began.
        const float StartTimeout = 4f, SkipAfter = .25f, MaxStep = .05f;

        public static bool Played { get; private set; }
        public static bool Showing => current != null;
        static LogoIntro current;

        PanelSettings ownedPanel;
        VisualElement screen, picture;
        VideoPlayer player;
        AudioSource sound;
        RenderTexture frame;
        Action done;
        float shown, fadeFrom = -1, fadeLength = 1;
        bool playing, finished;

        // Once per run: later calls (or a second title screen) only call `whenDone`.
        public static void Play(Action whenDone)
        {
            if (Played || current != null) { whenDone?.Invoke(); return; }
            Played = true;
            var logo = new GameObject("Logo Intro").AddComponent<LogoIntro>();
            logo.done = whenDone;
            logo.Build();
        }

        void Build()
        {
            current = this;
            var root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("LogoHud"),
                                            Resources.Load<ThemeStyleSheet>("NetworkTheme"), null,
                                            new Vector2Int(1280, 720), out ownedPanel);
            if (ownedPanel != null) ownedPanel.sortingOrder = SortingOrder;
            IntroFlowStyle.Apply(root);
            screen = root?.Q<VisualElement>("logo-screen");
            picture = root?.Q<VisualElement>("logo-video");
            if (screen == null || picture == null) { Finish(); return; }

            string path = StreamingArt.PathOf(StreamingArt.LogoVideo);
            if (!File.Exists(path))
            {
                Debug.LogWarning("[ChessFight] 시작 로고 영상(StreamingAssets/" + StreamingArt.LogoVideo + ")이 없어 건너뜁니다.");
                Finish();
                return;
            }
            frame = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32) { name = "Logo Intro" };
            frame.Create();

            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 0f;
            sound.volume = GameSettings.Effects;
            // The title scene's camera normally hears it; without one nothing would.
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();

            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = false;
            player.skipOnDrop = true;
            player.source = VideoSource.Url;
            player.url = path;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = frame;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            // A URL's tracks are unknown until it is prepared: take the first.
            player.controlledAudioTrackCount = 1;
            player.EnableAudioTrack(0, true);
            player.SetTargetAudioSource(0, sound);
            player.errorReceived += (p, message) =>
            {
                Debug.LogWarning("[ChessFight] 시작 로고 영상을 재생하지 못했습니다: " + message);
                BeginFade(SkipFadeSeconds);
            };
            player.prepareCompleted += p =>
            {
                if (finished || fadeFrom >= 0) return;
                picture.style.backgroundImage = Background.FromRenderTexture(frame);
                p.Play();
                playing = true;
            };
            player.Prepare();
        }

        void Update()
        {
            if (finished) return;
            float now = Time.unscaledTime;
            shown += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            if (fadeFrom < 0)
            {
                if (!playing && shown > StartTimeout) BeginFade(SkipFadeSeconds);
                else if (playing && player.time >= FadeAt) BeginFade(FadeSeconds);
                else if (shown > SkipAfter && LegacyKeys.AnyDown()) BeginFade(SkipFadeSeconds);
            }
            float k = fadeFrom < 0 ? 0f : Mathf.Clamp01((now - fadeFrom) / fadeLength);
            if (screen != null) screen.style.opacity = 1f - k;
            if (sound != null) sound.volume = GameSettings.Effects * (1f - k);
            if (k >= 1f) Finish();
        }

        void BeginFade(float seconds)
        {
            if (fadeFrom >= 0 || finished) return;
            fadeFrom = Time.unscaledTime;
            fadeLength = Mathf.Max(.01f, seconds);
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            if (current == this) current = null;
            if (player != null) player.Stop();
            var callback = done;
            done = null;
            Destroy(gameObject);
            callback?.Invoke();
        }

        void OnDestroy()
        {
            if (current == this) current = null;
            if (frame != null) { frame.Release(); Destroy(frame); }
            if (ownedPanel != null) Destroy(ownedPanel);
        }
    }
}
