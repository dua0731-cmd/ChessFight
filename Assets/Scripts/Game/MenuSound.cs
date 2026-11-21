using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // R738 (user 11-21: "설정창, 플레이, 기물도감, 전적 UI 사운드도 넣어야 해"): the menus' sounds - the lobby (its three tabs and
    // their pages, the party, friends and mode windows) and the settings window. Until now every menu was silent.
    // The clips are Resources/UiSound/Menu*.wav, ElevenLabs takes cut by ArtSource/Lobby/UiSound/bake.py (which take each
    // kind uses: its picks.json, then the numbers; every take is on the listening page). They follow the settings'
    // 소리 → 버튼 소리 volume (GameSettings.UiSound) under the master volume.
    //
    // Wire(root) hooks a whole panel at once, by bubbling events, so buttons made later (friend rows, the codex's list,
    // the settings' lines) sound too: the pointer coming onto an enabled button is Hover, a click is the button's kind -
    // a class on the button (SoundClass) or the chooser's answer, else Click. Opening and closing windows, switching
    // tabs and the like the views play themselves (Play).
    public static class MenuSound
    {
        public enum Kind { Hover, Click, Tab, Open, Close, Toggle, Confirm, Piece, Page, None }

        // A class on a button that says which sound its click makes ("snd-none": it plays its own, or none).
        public static string SoundClass(Kind kind) => "snd-" + kind.ToString().ToLowerInvariant();
        // A button that should not sound when the pointer comes onto it.
        public const string NoHover = "snd-nohover";

        const int Voices = 4;
        // The same kind again this soon is the same moment (a click that also opens a window, a drag over a slider).
        const float Repeat = .05f;
        // Kept small: the pitch moves a little so repeated clicks do not sound like a machine gun.
        const float Wobble = .025f;

        static GameObject host;
        static AudioSource[] voices;
        static int next;
        static AudioClip[] clips;
        static readonly float[] lastAt = new float[(int)Kind.None];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            host = null;
            voices = null;
            clips = null;
            next = 0;
            for (int i = 0; i < lastAt.Length; i++) lastAt[i] = -1f;
        }

        public static void Play(Kind kind)
        {
            if (kind == Kind.None || !Application.isPlaying) return;
            int k = (int)kind;
            float now = Time.unscaledTime;
            if (lastAt[k] >= 0f && now - lastAt[k] < Repeat) return;
            lastAt[k] = now;
            float volume = GameSettings.UiSound;
            if (volume <= 0f) return;
            var clip = Clip(kind);
            if (clip == null || !Ready()) return;
            var voice = voices[next];
            next = (next + 1) % voices.Length;
            voice.Stop();
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(volume);
            voice.pitch = 1f + Random.Range(-Wobble, Wobble);
            voice.Play();
        }

        static AudioClip Clip(Kind kind)
        {
            if (clips == null)
            {
                clips = new AudioClip[(int)Kind.None];
                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i] = Resources.Load<AudioClip>("UiSound/Menu" + (Kind)i);
                    if (clips[i] == null) Debug.LogWarning("[ChessFight] 메뉴 소리 파일 없음: Resources/UiSound/Menu" + (Kind)i);
                }
            }
            return clips[(int)kind];
        }

        static bool Ready()
        {
            if (voices != null && host != null) return true;
            host = new GameObject("Menu Sound");
            Object.DontDestroyOnLoad(host);
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
                source.priority = 32;
                voices[i] = source;
            }
            return true;
        }

        // The sound a click on this button makes: its class, else the chooser's, else a plain click.
        public static Kind KindOf(Button button, System.Func<Button, Kind?> choose = null)
        {
            if (button == null) return Kind.None;
            for (int i = 0; i < (int)Kind.None; i++)
                if (button.ClassListContains(SoundClass((Kind)i))) return (Kind)i;
            if (button.ClassListContains(SoundClass(Kind.None))) return Kind.None;
            var chosen = choose?.Invoke(button);
            return chosen ?? Kind.Click;
        }

        static Button ButtonOf(IEventHandler target)
        {
            for (var e = target as VisualElement; e != null; e = e.parent)
                if (e is Button b) return b;
            return null;
        }

        static readonly HashSet<VisualElement> wired = new HashSet<VisualElement>();

        // Hover and click sounds for every button under this root, now and later.
        public static void Wire(VisualElement root, System.Func<Button, Kind?> choose = null)
        {
            if (root == null) return;
            wired.RemoveWhere(e => e == null || e.panel == null);
            if (!wired.Add(root)) return;
            Button hovered = null;
            root.RegisterCallback<PointerOverEvent>(e =>
            {
                var button = ButtonOf(e.target);
                if (button == hovered) return;
                hovered = button;
                if (button != null && button.enabledInHierarchy && !button.ClassListContains(NoHover)) Play(Kind.Hover);
            });
            root.RegisterCallback<PointerLeaveEvent>(_ => hovered = null);
            root.RegisterCallback<ClickEvent>(e =>
            {
                var button = ButtonOf(e.target);
                if (button != null && button.enabledInHierarchy) Play(KindOf(button, choose));
            }, TrickleDown.TrickleDown);
        }
    }
}
