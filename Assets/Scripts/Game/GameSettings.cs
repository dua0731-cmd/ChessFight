using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Game
{
    // The actions the player can bind (settings → 조작 → 키 설정), two keys each.
    public enum GameKey { Forward, Back, Left, Right, Jump, Sprint, Shove, Grab, Skill, Skill2, Use, Respawn, Chat, Score, Talk }

    // What the settings window (R64, laid out again in R77) changes, kept on this
    // PC in PlayerPrefs and read by whoever it concerns: the movement code asks
    // for the bound keys, the cameras for the mouse, field of view, distance and
    // shake, the chat for its switches, the match HUD for the ping line,
    // ScreenOverlay for brightness and the frame counter.
    //
    // Every value lives under an id ("master", "fov", ...); the typed properties
    // below are the ones the game reads. Values the game cannot use yet (voice
    // chat, controller, name tags...) are saved all the same, under their ids,
    // so the window keeps them and the feature can read them when it comes.
    //
    // The display settings are applied at start only once the player has changed
    // them, so a PC that never opens the window keeps the project's defaults.
    public static class GameSettings
    {
        const string Prefix = "cf.s2.";

        // Bumped on every change, for views that redraw only when it moves.
        public static int Version { get; private set; }
        public static event Action Changed;

        static readonly Dictionary<string, float> cache = new Dictionary<string, float>();

        // ---------- the store ----------

        public static bool Has(string id) => cache.ContainsKey(id) || PlayerPrefs.HasKey(Prefix + id);

        public static float Float(string id, float fallback)
        {
            if (cache.TryGetValue(id, out var v)) return v;
            v = PlayerPrefs.HasKey(Prefix + id) ? PlayerPrefs.GetFloat(Prefix + id, fallback) : fallback;
            cache[id] = v;
            return v;
        }

        public static int Int(string id, int fallback) => Mathf.RoundToInt(Float(id, fallback));
        public static bool Bool(string id, bool fallback) => Int(id, fallback ? 1 : 0) != 0;

        public static void SetFloat(string id, float v)
        {
            cache[id] = v;
            PlayerPrefs.SetFloat(Prefix + id, v);
            Saved();
        }

        public static void SetInt(string id, int v) => SetFloat(id, v);
        public static void SetBool(string id, bool v) => SetFloat(id, v ? 1 : 0);

        public static void Forget(string id)
        {
            cache.Remove(id);
            PlayerPrefs.DeleteKey(Prefix + id);
            Saved();
        }

        static void Saved()
        {
            Version++;
            PlayerPrefs.Save();
            AudioListener.volume = Master;
            try { Changed?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ---------- what the game reads ----------

        public static float Master => Float("master", 1f);
        public static float Music => Float("music", .7f);
        public static float Effects => Float("sfx", .8f);
        public static float UiSound => Float("ui", .6f);
        public static float Voice => Float("vc", .8f);
        public static bool MuteInBackground => Bool("bgmute", true);

        // Vertical field of view of the match camera (the ragdoll camera's own default is 60).
        public static float Fov => Float("fov", 60f);
        public static float Brightness => Float("bright", 1f);
        public static bool ShowFps => Bool("showfps", false);
        public static bool ShowPing => Bool("showping", true);

        public static float MouseScale => Float("sens", 1f);
        public static bool InvertY => Bool("invert", false);
        // 0: hold to sprint, 1: press once to sprint until stopping.
        public static bool SprintToggle => Int("sprintmode", 0) == 1;

        public static float CameraDistance => Float("camdist", 3.6f);
        public static bool ReduceShake => Bool("reduceshake", false);
        public static bool ReduceFlash => Bool("reduceflash", false);
        // Camera shake is multiplied by this (settings → 게임플레이 → 카메라 흔들림, and 접근성).
        public static float ShakeScale => ReduceShake ? 0f : Mathf.Clamp01(Float("shake", 100f) / 100f);
        // The sprint's field-of-view kick.
        public static float SprintFovScale => !ReduceShake && Bool("sprintfov", true) ? 1f : 0f;

        public static bool ChatOn => Bool("chat", true);
        public static bool ChatFilter => Bool("filter", true);
        // 0 small, 1 normal, 2 large.
        public static int ChatSize => Mathf.Clamp(Int("chatsize", 1), 0, 2);

        // Menus drawn by RuntimePanels (the settings window keeps 1).
        public static float UiScale => Mathf.Clamp(Float("uiscale", 1f), .9f, 1.15f);

        // Loaded before the first scene, so the title screen already has them.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            LoadKeys();
            AudioListener.volume = Master;
            ApplyDisplay();
            ScreenOverlay.Ensure();
        }

        // ---------- keys ----------

        static readonly Dictionary<GameKey, KeyCode[]> Defaults = new Dictionary<GameKey, KeyCode[]>
        {
            { GameKey.Forward, new[] { KeyCode.W, KeyCode.UpArrow } },
            { GameKey.Back, new[] { KeyCode.S, KeyCode.DownArrow } },
            { GameKey.Left, new[] { KeyCode.A, KeyCode.LeftArrow } },
            { GameKey.Right, new[] { KeyCode.D, KeyCode.RightArrow } },
            { GameKey.Jump, new[] { KeyCode.Space, KeyCode.None } },
            { GameKey.Sprint, new[] { KeyCode.LeftShift, KeyCode.None } },
            { GameKey.Shove, new[] { KeyCode.Mouse0, KeyCode.None } },
            { GameKey.Grab, new[] { KeyCode.Mouse1, KeyCode.None } },
            { GameKey.Skill, new[] { KeyCode.E, KeyCode.None } },
            { GameKey.Skill2, new[] { KeyCode.Q, KeyCode.None } },
            { GameKey.Use, new[] { KeyCode.F, KeyCode.None } },
            { GameKey.Respawn, new[] { KeyCode.R, KeyCode.None } },
            { GameKey.Chat, new[] { KeyCode.Tab, KeyCode.None } },
            { GameKey.Score, new[] { KeyCode.BackQuote, KeyCode.None } },
            { GameKey.Talk, new[] { KeyCode.V, KeyCode.None } },
        };
        static readonly Dictionary<GameKey, KeyCode[]> keys = new Dictionary<GameKey, KeyCode[]>();

        static void LoadKeys()
        {
            foreach (var pair in Defaults)
            {
                var slots = new KeyCode[2];
                for (int s = 0; s < 2; s++)
                {
                    int code = Int($"key.{pair.Key}.{s}", (int)pair.Value[s]);
                    slots[s] = Enum.IsDefined(typeof(KeyCode), code) ? (KeyCode)code : pair.Value[s];
                }
                keys[pair.Key] = slots;
            }
        }

        public static KeyCode Key(GameKey key, int slot = 0)
        {
            if (keys.Count == 0) LoadKeys();
            return keys[key][slot];
        }

        // A key already used elsewhere moves to where this one was, so no two
        // places share it. KeyCode.None clears the slot.
        public static void SetKey(GameKey key, int slot, KeyCode code)
        {
            if (keys.Count == 0) LoadKeys();
            var old = keys[key][slot];
            if (code != KeyCode.None)
                foreach (var pair in keys)
                    for (int s = 0; s < 2; s++)
                        if (pair.Value[s] == code && !(pair.Key == key && s == slot))
                        {
                            pair.Value[s] = old;
                            cache[$"key.{pair.Key}.{s}"] = (int)old;
                            PlayerPrefs.SetFloat($"{Prefix}key.{pair.Key}.{s}", (int)old);
                        }
            keys[key][slot] = code;
            SetInt($"key.{key}.{slot}", (int)code);
        }

        public static void ResetKeys(IEnumerable<GameKey> which)
        {
            if (keys.Count == 0) LoadKeys();
            foreach (var key in which)
                for (int s = 0; s < 2; s++)
                {
                    keys[key][s] = Defaults[key][s];
                    cache[$"key.{key}.{s}"] = (int)Defaults[key][s];
                    PlayerPrefs.SetFloat($"{Prefix}key.{key}.{s}", (int)Defaults[key][s]);
                }
            Saved();
        }

        public static bool Held(GameKey key) => Get(Key(key, 0), false) || Get(Key(key, 1), false);
        public static bool Pressed(GameKey key) => Get(Key(key, 0), true) || Get(Key(key, 1), true);

        static bool Get(KeyCode code, bool down)
        {
            if (code == KeyCode.None) return false;
            try { return down ? Input.GetKeyDown(code) : Input.GetKey(code); }
            catch (InvalidOperationException) { return false; }
        }

        // The movement keys as a stick: x right, y forward.
        public static Vector2 Move =>
            new Vector2((Held(GameKey.Right) ? 1 : 0) - (Held(GameKey.Left) ? 1 : 0),
                        (Held(GameKey.Forward) ? 1 : 0) - (Held(GameKey.Back) ? 1 : 0));

        static bool sprintLatched;
        static int sprintFrame = -1;

        // Sprinting this frame: the key held, or, in toggle mode, latched by a
        // press until the player stops moving or presses it again.
        public static bool Sprint
        {
            get
            {
                if (!SprintToggle) return Held(GameKey.Sprint);
                if (sprintFrame != Time.frameCount)
                {
                    sprintFrame = Time.frameCount;
                    if (Pressed(GameKey.Sprint)) sprintLatched = !sprintLatched;
                    if (Move == Vector2.zero) sprintLatched = false;
                }
                return sprintLatched;
            }
        }

        // The mouse this frame with the sensitivity applied; LookY is positive
        // for "look up" (inverted when the player asked for it).
        public static float LookX => Axis("Mouse X") * MouseScale;
        public static float LookY => Axis("Mouse Y") * MouseScale * (InvertY ? -1f : 1f);

        static float Axis(string name)
        {
            try { return Input.GetAxisRaw(name); }
            catch (Exception) { return 0f; }
        }

        public static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.None: return "—";
                case KeyCode.Mouse0: return "좌클릭";
                case KeyCode.Mouse1: return "우클릭";
                case KeyCode.Mouse2: return "휠 클릭";
                case KeyCode.Mouse3: return "마우스 4";
                case KeyCode.Mouse4: return "마우스 5";
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
        public static readonly string[] FrameNames = { "30", "60", "120", "144", "240", "제한 없음" };

        public static int ScreenModeIndex
        {
            get
            {
                var mode = Has("mode") ? Modes[Mathf.Clamp(Int("mode", 1), 0, 2)] : Screen.fullScreenMode;
                if (mode == FullScreenMode.MaximizedWindow) mode = FullScreenMode.Windowed;
                return Mathf.Max(0, Array.IndexOf(Modes, mode));
            }
        }

        public static void SetScreenMode(int index)
        {
            index = Mathf.Clamp(index, 0, Modes.Length - 1);
            SetInt("mode", index);
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
            Has("width") ? new Vector2Int(Int("width", Screen.width), Int("height", Screen.height))
                         : new Vector2Int(Screen.width, Screen.height);

        public static void SetSize(Vector2Int size)
        {
            cache["width"] = size.x;
            PlayerPrefs.SetFloat(Prefix + "width", size.x);
            SetInt("height", size.y);
            Screen.SetResolution(size.x, size.y, Modes[ScreenModeIndex]);
        }

        // The monitors, by name; the game's window moves to the one picked.
        public static List<string> Monitors
        {
            get
            {
                var list = new List<string>();
                var layout = new List<DisplayInfo>();
                try { Screen.GetDisplayLayout(layout); } catch (Exception) { }
                for (int i = 0; i < layout.Count; i++)
                    list.Add($"모니터 {i + 1}" + (string.IsNullOrEmpty(layout[i].name) ? "" : $" ({layout[i].name})"));
                if (list.Count == 0) list.Add("모니터 1");
                return list;
            }
        }

        public static int MonitorIndex
        {
            get
            {
                var layout = new List<DisplayInfo>();
                try { Screen.GetDisplayLayout(layout); } catch (Exception) { return 0; }
                var main = Screen.mainWindowDisplayInfo;
                for (int i = 0; i < layout.Count; i++)
                    if (layout[i].name == main.name && layout[i].width == main.width && layout[i].height == main.height) return i;
                return 0;
            }
        }

        public static void SetMonitor(int index)
        {
            var layout = new List<DisplayInfo>();
            try { Screen.GetDisplayLayout(layout); } catch (Exception) { return; }
            if (index < 0 || index >= layout.Count) return;
            SetInt("monitor", index);
            Screen.MoveMainWindowTo(layout[index], Vector2Int.zero);
        }

        public static bool VSync => Has("vsync") ? Bool("vsync", true) : QualitySettings.vSyncCount > 0;

        public static void SetVSync(bool on)
        {
            SetBool("vsync", on);
            QualitySettings.vSyncCount = on ? 1 : 0;
        }

        // 0: no limit.
        public static int FrameLimit => Has("fps") ? Int("fps", 0) : Mathf.Max(0, Application.targetFrameRate);

        public static void SetFrameLimit(int fps)
        {
            SetInt("fps", fps);
            Application.targetFrameRate = fps > 0 ? fps : -1;
        }

        // ---------- graphics quality ----------
        // The built-in renderer's own switches: the quality level ("Mobile", "PC"),
        // shadows, MSAA and LOD bias. Water, effects and glow have no switch yet.

        public static readonly string[] Presets = { "낮음", "중간", "높음", "사용자 지정" };
        static readonly int[][] PresetValues = { new[] { 0, 0, 0 }, new[] { 2, 1, 1 }, new[] { 3, 2, 2 } };   // shadow, aa, detail

        public static int Preset => Int("preset", 2);
        public static int Shadow => Int("shadow", 3);
        public static int Antialias => Int("aa", 2);
        public static int Detail => Int("detail", 2);

        public static void SetPreset(int preset)
        {
            preset = Mathf.Clamp(preset, 0, 3);
            if (preset < 3)
            {
                var v = PresetValues[preset];
                cache["shadow"] = v[0]; PlayerPrefs.SetFloat(Prefix + "shadow", v[0]);
                cache["aa"] = v[1]; PlayerPrefs.SetFloat(Prefix + "aa", v[1]);
                cache["detail"] = v[2]; PlayerPrefs.SetFloat(Prefix + "detail", v[2]);
            }
            SetInt("preset", preset);
            ApplyGraphics();
        }

        // One of the parts: the preset becomes "사용자 지정".
        public static void SetGraphic(string id, int value)
        {
            cache["preset"] = 3; PlayerPrefs.SetFloat(Prefix + "preset", 3);
            SetInt(id, value);
            ApplyGraphics();
        }

        static void ApplyGraphics()
        {
            int levels = QualitySettings.names.Length;
            if (Has("preset") && levels > 0) QualitySettings.SetQualityLevel(Preset == 0 ? 0 : levels - 1, true);
            if (Has("shadow"))
            {
                int s = Mathf.Clamp(Shadow, 0, 3);
                QualitySettings.shadows = s == 0 ? ShadowQuality.Disable : s == 1 ? ShadowQuality.HardOnly : ShadowQuality.All;
                QualitySettings.shadowResolution = s <= 1 ? ShadowResolution.Low : s == 2 ? ShadowResolution.Medium : ShadowResolution.High;
                QualitySettings.shadowDistance = new[] { 0f, 25f, 50f, 90f }[s];
            }
            if (Has("aa")) QualitySettings.antiAliasing = new[] { 0, 2, 4, 8 }[Mathf.Clamp(Antialias, 0, 3)];
            if (Has("detail")) QualitySettings.lodBias = new[] { .6f, 1f, 1.6f }[Mathf.Clamp(Detail, 0, 2)];
            // A quality level carries its own vsync; keep the player's.
            if (Has("vsync")) QualitySettings.vSyncCount = Bool("vsync", true) ? 1 : 0;
        }

        // The 디스플레이 page's "기본값 복원": borderless at the monitor's size, vsync
        // on, no frame limit.
        public static void ResetDisplay()
        {
            var native = Screen.currentResolution;
            SetInt("mode", 1);
            SetSize(new Vector2Int(native.width, native.height));
            SetVSync(true);
            SetFrameLimit(0);
        }

        static void ApplyDisplay()
        {
            ApplyGraphics();
            if (Has("fps")) Application.targetFrameRate = FrameLimit > 0 ? FrameLimit : -1;
            // The editor's game view ignores both; a build takes them.
            if (!Application.isEditor && (Has("width") || Has("mode")))
            {
                var size = CurrentSize;
                Screen.SetResolution(size.x, size.y, Modes[ScreenModeIndex]);
            }
        }
    }
}
