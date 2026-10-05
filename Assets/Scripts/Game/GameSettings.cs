using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Game
{
    // The keys the player can move (settings → 조작).
    public enum GameKey { Forward, Back, Left, Right, Jump, Sprint, Chat }

    // What the settings window (R64) changes, kept on this PC in PlayerPrefs and
    // read by whoever it concerns: the movement code asks for the bound keys, the
    // cameras for the mouse, the chat for its switches, the match HUD for the
    // ping line.
    //
    // The display settings (screen mode, resolution, vsync, frame limit, quality)
    // are applied at start only once the player has changed them, so a PC that
    // never opens the window keeps the project's own defaults. The game has no
    // sound yet: the master volume drives AudioListener.volume, the other three
    // wait for the sounds that will read them.
    public static class GameSettings
    {
        const string Prefix = "cf.settings.";

        // Bumped on every change, for views that redraw only when it moves.
        public static int Version { get; private set; }

        // ---------- sound (0..1) ----------
        public static float Master { get; private set; } = 1f;
        public static float Music { get; private set; } = .8f;
        public static float Effects { get; private set; } = .8f;
        public static float Voice { get; private set; } = .8f;

        // ---------- mouse ----------
        public static float MouseScale { get; private set; } = 1f;
        public static bool InvertY { get; private set; }

        // ---------- chat ----------
        public static bool ChatOn { get; private set; } = true;
        public static bool ChatFilter { get; private set; } = true;
        // 0 small, 1 normal, 2 large.
        public static int ChatSize { get; private set; } = 1;

        // ---------- other ----------
        public static bool ShowPing { get; private set; } = true;
        public static bool ReduceShake { get; private set; }

        static readonly Dictionary<GameKey, KeyCode> Defaults = new Dictionary<GameKey, KeyCode>
        {
            { GameKey.Forward, KeyCode.W }, { GameKey.Back, KeyCode.S }, { GameKey.Left, KeyCode.A }, { GameKey.Right, KeyCode.D },
            { GameKey.Jump, KeyCode.Space }, { GameKey.Sprint, KeyCode.LeftShift }, { GameKey.Chat, KeyCode.Tab },
        };
        static readonly Dictionary<GameKey, KeyCode> keys = new Dictionary<GameKey, KeyCode>(Defaults);

        // Loaded before the first scene, so the title screen already has them.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            Master = GetFloat("master", Master);
            Music = GetFloat("music", Music);
            Effects = GetFloat("effects", Effects);
            Voice = GetFloat("voice", Voice);
            MouseScale = GetFloat("mouse", MouseScale);
            InvertY = GetBool("invertY", InvertY);
            ChatOn = GetBool("chatOn", ChatOn);
            ChatFilter = GetBool("chatFilter", ChatFilter);
            ChatSize = Mathf.Clamp(GetInt("chatSize", ChatSize), 0, 2);
            ShowPing = GetBool("ping", ShowPing);
            ReduceShake = GetBool("shake", ReduceShake);
            foreach (var bind in Defaults.Keys)
            {
                int code = GetInt("key." + bind, (int)Defaults[bind]);
                keys[bind] = Enum.IsDefined(typeof(KeyCode), code) ? (KeyCode)code : Defaults[bind];
            }
            AudioListener.volume = Master;
            ApplyDisplay();
        }

        // ---------- setters (save at once) ----------

        public static void SetMaster(float v) { Master = Mathf.Clamp01(v); SetFloat("master", Master); AudioListener.volume = Master; }
        public static void SetMusic(float v) { Music = Mathf.Clamp01(v); SetFloat("music", Music); }
        public static void SetEffects(float v) { Effects = Mathf.Clamp01(v); SetFloat("effects", Effects); }
        public static void SetVoice(float v) { Voice = Mathf.Clamp01(v); SetFloat("voice", Voice); }
        public static void SetMouseScale(float v) { MouseScale = Mathf.Clamp(v, .1f, 3f); SetFloat("mouse", MouseScale); }
        public static void SetInvertY(bool v) { InvertY = v; SetBool("invertY", v); }
        public static void SetChatOn(bool v) { ChatOn = v; SetBool("chatOn", v); }
        public static void SetChatFilter(bool v) { ChatFilter = v; SetBool("chatFilter", v); }
        public static void SetChatSize(int v) { ChatSize = Mathf.Clamp(v, 0, 2); SetInt("chatSize", ChatSize); }
        public static void SetShowPing(bool v) { ShowPing = v; SetBool("ping", v); }
        public static void SetReduceShake(bool v) { ReduceShake = v; SetBool("shake", v); }

        // ---------- keys ----------

        public static KeyCode Key(GameKey bind) => keys[bind];

        // A key already on another action swaps with it, so no two actions share one.
        public static void SetKey(GameKey bind, KeyCode key)
        {
            foreach (var other in Defaults.Keys)
                if (other != bind && keys[other] == key)
                {
                    keys[other] = keys[bind];
                    SetInt("key." + other, (int)keys[other]);
                }
            keys[bind] = key;
            SetInt("key." + bind, (int)key);
        }

        public static void ResetKeys()
        {
            foreach (var pair in Defaults) { keys[pair.Key] = pair.Value; SetInt("key." + pair.Key, (int)pair.Value); }
        }

        public static bool Held(GameKey bind)
        {
            try { return Input.GetKey(keys[bind]); }
            catch (InvalidOperationException) { return false; }
        }

        public static bool Pressed(GameKey bind)
        {
            try { return Input.GetKeyDown(keys[bind]); }
            catch (InvalidOperationException) { return false; }
        }

        // The movement keys as a stick: x right, y forward.
        public static Vector2 Move =>
            new Vector2((Held(GameKey.Right) ? 1 : 0) - (Held(GameKey.Left) ? 1 : 0),
                        (Held(GameKey.Forward) ? 1 : 0) - (Held(GameKey.Back) ? 1 : 0));

        // The mouse this frame with the sensitivity applied; LookY is positive
        // for "look up" (inverted when the player asked for it).
        public static float LookX => Axis("Mouse X") * MouseScale;
        public static float LookY => Axis("Mouse Y") * MouseScale * (InvertY ? -1f : 1f);

        // Camera shake and the sprint field-of-view kick are multiplied by this.
        public static float ShakeScale => ReduceShake ? 0f : 1f;

        static float Axis(string name)
        {
            try { return Input.GetAxisRaw(name); }
            catch (Exception) { return 0f; }
        }

        public static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Space: return "Space";
                case KeyCode.LeftShift: return "왼쪽 Shift";
                case KeyCode.RightShift: return "오른쪽 Shift";
                case KeyCode.LeftControl: return "왼쪽 Ctrl";
                case KeyCode.RightControl: return "오른쪽 Ctrl";
                case KeyCode.LeftAlt: return "왼쪽 Alt";
                case KeyCode.RightAlt: return "오른쪽 Alt";
                case KeyCode.Return: return "Enter";
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
                case KeyCode.BackQuote: return "`";
            }
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)(key - KeyCode.Alpha0)).ToString();
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return "숫자판 " + (int)(key - KeyCode.Keypad0);
            return key.ToString();
        }

        // ---------- chat filter ----------

        // Common Korean and English swear words, matched inside words too and
        // drawn as stars. A short list on purpose: it covers the usual ones, it is
        // not a moderation system.
        static readonly string[] Swears =
        {
            "씨발", "시발", "씨빨", "ㅅㅂ", "ㅆㅂ", "병신", "븅신", "ㅂㅅ", "개새끼", "개새", "새끼", "ㅅㄲ",
            "좆", "존나", "졸라", "ㅈㄴ", "지랄", "ㅈㄹ", "닥쳐", "꺼져", "미친놈", "미친년", "염병", "엠창", "느금",
            "fuck", "shit", "bitch", "asshole", "bastard",
        };

        public static string Filter(string text)
        {
            if (!ChatFilter || string.IsNullOrEmpty(text)) return text;
            foreach (var word in Swears)
            {
                int at = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
                while (at >= 0)
                {
                    text = text.Substring(0, at) + new string('*', word.Length) + text.Substring(at + word.Length);
                    at = text.IndexOf(word, at + word.Length, StringComparison.OrdinalIgnoreCase);
                }
            }
            return text;
        }

        // ---------- display ----------

        static readonly FullScreenMode[] Modes = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
        public static readonly string[] ModeNames = { "전체 화면", "테두리 없는 창", "창 모드" };
        public static readonly int[] FrameLimits = { 30, 60, 120, 144, 240, 0 };

        public static int ScreenModeIndex
        {
            get
            {
                var mode = HasKey("screenMode") ? (FullScreenMode)GetInt("screenMode", 1) : Screen.fullScreenMode;
                if (mode == FullScreenMode.MaximizedWindow) mode = FullScreenMode.Windowed;
                return Mathf.Max(0, Array.IndexOf(Modes, mode));
            }
        }

        public static void SetScreenMode(int index)
        {
            index = Mathf.Clamp(index, 0, Modes.Length - 1);
            SetInt("screenMode", (int)Modes[index]);
            var size = CurrentSize;
            Screen.SetResolution(size.x, size.y, Modes[index]);
        }

        // Every width × height the monitor offers, smallest first, once each.
        public static List<Vector2Int> Sizes
        {
            get
            {
                var list = new List<Vector2Int>();
                foreach (var r in Screen.resolutions)
                {
                    var size = new Vector2Int(r.width, r.height);
                    if (!list.Contains(size)) list.Add(size);
                }
                var now = CurrentSize;
                if (!list.Contains(now)) list.Add(now);
                list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
                return list;
            }
        }

        public static Vector2Int CurrentSize =>
            HasKey("width") ? new Vector2Int(GetInt("width", Screen.width), GetInt("height", Screen.height))
                            : new Vector2Int(Screen.width, Screen.height);

        public static void SetSize(Vector2Int size)
        {
            SetInt("width", size.x);
            SetInt("height", size.y);
            Screen.SetResolution(size.x, size.y, Modes[ScreenModeIndex]);
        }

        public static bool VSync => HasKey("vsync") ? GetBool("vsync", true) : QualitySettings.vSyncCount > 0;

        public static void SetVSync(bool on)
        {
            SetBool("vsync", on);
            QualitySettings.vSyncCount = on ? 1 : 0;
        }

        // 0: no limit.
        public static int FrameLimit => HasKey("fps") ? GetInt("fps", 0) : Mathf.Max(0, Application.targetFrameRate);

        public static void SetFrameLimit(int fps)
        {
            SetInt("fps", fps);
            Application.targetFrameRate = fps > 0 ? fps : -1;
        }

        public static int Quality => HasKey("quality") ? Mathf.Clamp(GetInt("quality", 0), 0, QualitySettings.names.Length - 1)
                                                       : QualitySettings.GetQualityLevel();

        public static string QualityName(int level)
        {
            var names = QualitySettings.names;
            // The project has two levels, "Mobile" and "PC".
            if (names.Length == 2) return level == 0 ? "낮음" : "높음";
            if (names.Length == 3) return level == 0 ? "낮음" : level == 1 ? "중간" : "높음";
            return level >= 0 && level < names.Length ? names[level] : "";
        }

        public static void SetQuality(int level)
        {
            level = Mathf.Clamp(level, 0, QualitySettings.names.Length - 1);
            SetInt("quality", level);
            QualitySettings.SetQualityLevel(level, true);
            // A quality level carries its own vsync; keep the player's.
            if (HasKey("vsync")) QualitySettings.vSyncCount = GetBool("vsync", true) ? 1 : 0;
        }

        // The window's "기본값" on the 화면 tab: borderless at the monitor's size, vsync on,
        // no frame limit, the highest quality.
        public static void ResetDisplay()
        {
            var native = Screen.currentResolution;
            SetInt("screenMode", (int)FullScreenMode.FullScreenWindow);
            SetSize(new Vector2Int(native.width, native.height));
            SetQuality(QualitySettings.names.Length - 1);
            SetVSync(true);
            SetFrameLimit(0);
        }

        static void ApplyDisplay()
        {
            if (HasKey("quality")) QualitySettings.SetQualityLevel(Quality, true);
            if (HasKey("vsync")) QualitySettings.vSyncCount = VSync ? 1 : 0;
            if (HasKey("fps")) Application.targetFrameRate = FrameLimit > 0 ? FrameLimit : -1;
            // The editor's game view ignores both; a build takes them.
            if (!Application.isEditor && (HasKey("width") || HasKey("screenMode")))
            {
                var size = CurrentSize;
                Screen.SetResolution(size.x, size.y, Modes[ScreenModeIndex]);
            }
        }

        // ---------- PlayerPrefs ----------

        static bool HasKey(string key) => PlayerPrefs.HasKey(Prefix + key);
        static float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(Prefix + key, fallback);
        static int GetInt(string key, int fallback) => PlayerPrefs.GetInt(Prefix + key, fallback);
        static bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) != 0;
        static void SetFloat(string key, float v) { PlayerPrefs.SetFloat(Prefix + key, v); Saved(); }
        static void SetInt(string key, int v) { PlayerPrefs.SetInt(Prefix + key, v); Saved(); }
        static void SetBool(string key, bool v) => SetInt(key, v ? 1 : 0);

        static void Saved()
        {
            Version++;
            PlayerPrefs.Save();
        }
    }
}
