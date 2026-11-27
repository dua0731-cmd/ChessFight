using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // The settings window (R64; laid out again in R77, Docs/Architecture/UI.md §12):
    // Esc on the title screen and in the lobby opens it full screen over a still
    // chess background (SettingsBackdrop). Categories across the top, each
    // category's pages down the left with a description box under them, one line
    // per setting (name left, value right: ‹ ›, a drop-down list or a slider),
    // key hints along the bottom. Every change is saved at once (GameSettings).
    // Design sample: https://claude.ai/artifact/C7WFSCyXfVj6wLX4zDRw4g (Settings2).
    //
    // Every line acts on the game (R674 went through them against main). R786 (user 11-23, "지금 필요 없는 것
    // 지우고"): the lines for features the game does not have (controller, colour-blind modes, streamer mode...),
    // drawn dim as "준비 중" since R674, are gone; a feature gets its line when it comes. The 음성 채팅 page (R131)
    // uses the same lines, plus GateLine (the voice-activated threshold over the live level) and MicLine.
    //
    // The screens own the Esc key: IntroController and NetworkHudView call
    // Toggle/Back. Back closes an open list, then calls off a key being bound,
    // then closes the window. While it is open those screens ignore their other
    // keys and clicks, and the chat stays shut.
    [DisallowMultipleComponent]
    public sealed class SettingsWindow : MonoBehaviour
    {
        // Above the entrance screen (LoadingScreenView, 100).
        const float SortingOrder = 200f;
        const float ScrollStep = 48f;

        static SettingsWindow current;
        static KeyCode[] capturable;
        static int tabIndex, pageIndex;

        public static bool IsOpen => current != null;
        // The Steam name for the top bar; the runtime sets it (null: offline).
        public static Func<string> Identity;
        // Voice chat (R131): the runtime's microphone level (0..1, null without Steam) and why it is
        // silent ("" when fine). While the 음성 채팅 page is open the voice keeps the microphone running
        // for the meters (VoicePageOpen), and plays my own voice back while VoiceEcho is on.
        public static Func<float> VoiceLevel;
        public static Func<string> VoiceProblem;
        public static bool VoicePageOpen { get; private set; }
        public static bool VoiceEcho { get; private set; }

        PanelSettings ownedPanel;
        VisualElement root, tabsBox, subsBox, view, content, bar, thumb, layer;
        Label descTitle, descText, descNote, resetText;
        readonly List<Action> refreshers = new List<Action>();
        // Built in Build, not as a field initializer: Unity refuses Screen calls in a constructor.
        List<Tab> tabs;
        float scroll;
        // A key being bound: which action and slot, and the frame it started.
        GameKey? waitingKey;
        int waitingSlot, waitingFrame;
        VisualElement openMenu;
        // The microphone test (소리 → 음성 채팅).
        AudioClip micClip;
        string micDevice;
        float micLevel;

        public static void Toggle()
        {
            if (IsOpen) Back();
            else Open();
        }

        public static void Open()
        {
            if (IsOpen) return;
            MenuSound.Play(MenuSound.Kind.Open);
            current = new GameObject("Settings Window").AddComponent<SettingsWindow>();
            current.Build();
        }

        public static void Back()
        {
            if (!IsOpen) return;
            if (current.openMenu != null) { MenuSound.Play(MenuSound.Kind.Close); current.CloseMenu(); return; }
            if (current.waitingKey != null) { current.waitingKey = null; current.Refresh(); return; }
            Close();
        }

        public static void Close()
        {
            if (!IsOpen) return;
            MenuSound.Play(MenuSound.Kind.Close);
            Destroy(current.gameObject);
            current = null;
        }

        // ---------- what the window holds ----------

        abstract class Item
        {
            // Note: a side remark under the description, below a thin line (the editor-only screen lines).
            public string Label, Desc, Note;
            public bool Indent;
            public Action Reset;
            // Shown only while this holds (null: always).
            public Func<bool> When;
        }

        // ‹ value › (Drop: a drop-down list instead).
        sealed class Choice : Item
        {
            public Func<string[]> Options;
            public Func<int> Get;
            public Action<int> Set;
            public bool Drop;
        }

        sealed class Slide : Item
        {
            public float Min, Max, Step;
            public Func<float> Get;
            public Action<float> Set;
            public Func<float, string> Show;
        }

        sealed class Keys : Item { public GameKey Key; }
        // R842: a line with one button that does something (튜토리얼 다시 하기). Run says whether it could; when it
        // could not, the description panel shows Refused.
        sealed class Act : Item { public string Button, Refused; public Func<bool> Run; }

        /// <summary>R842: starts the tutorial again from the lobby (set by the game's runtime, which knows the scene and the
        /// party); false when it cannot just now. Null: the line is not shown.</summary>
        public static Func<bool> TutorialReplay;
        sealed class MicTest : Item { }

        // The voice-activated threshold (R131): a slider whose rail also shows the live microphone level.
        sealed class Gate : Item { }

        sealed class Section { public string Title, Note; public Item[] Items; }
        sealed class Page { public string Name; public Section[] Sections; }
        sealed class Tab { public string Name; public Page[] Pages; }

        static readonly string[] OffOn = { "끔", "켬" };

        // A value saved under an id (GameSettings), which the game reads.
        static Choice Pick(string id, string label, string[] options, int fallback, string desc, bool drop = false,
                           bool indent = false) =>
            new Choice
            {
                Label = label, Desc = desc, Indent = indent, Drop = drop, Options = () => options,
                Get = () => Mathf.Clamp(GameSettings.Int(id, fallback), 0, options.Length - 1),
                Set = v => GameSettings.SetInt(id, v), Reset = () => GameSettings.Forget(id),
            };

        // R747: the languages the string table has, each by its own name; one not marked ready says it is still being
        // translated. Picking one rebuilds the window in it (Update sees Loc.Lang move); the page's reset goes back to
        // following Steam's language.
        static Choice LanguageChoice()
        {
            var offered = GameLanguage.Offered();
            var names = offered.ConvertAll(l => l.Code == Loc.Source || Loc.Table.Ready(l.Code) ? l.Native : l.Native + " · " + Loc.T("번역 중")).ToArray();
            return new Choice
            {
                Label = "언어", Drop = true, Options = () => names,
                Desc = "게임에 표시할 언어를 선택합니다. 선택하지 않으면 Steam 언어 설정을 따릅니다.",
                Get = () => Mathf.Max(0, offered.FindIndex(l => l.Code == Loc.Lang)),
                Set = v => { if (v >= 0 && v < offered.Count) GameLanguage.Pick(offered[v].Code); },
                Reset = GameLanguage.Forget,
            };
        }

        static Choice Switch(string id, string label, bool fallback, string desc, bool indent = false) =>
            Pick(id, label, OffOn, fallback ? 1 : 0, desc, false, indent);

        static Slide Number(string id, string label, float min, float max, float step, float fallback,
                            Func<float, string> show, string desc, bool indent = false) =>
            new Slide
            {
                Label = label, Desc = desc, Indent = indent, Min = min, Max = max, Step = step, Show = show,
                Get = () => GameSettings.Float(id, fallback), Set = v => GameSettings.SetFloat(id, v),
                Reset = () => GameSettings.Forget(id),
            };

        static Slide Volume(string id, string label, float fallback, string desc, bool indent = false) =>
            Number(id, label, 0f, 1f, .05f, fallback, v => Mathf.RoundToInt(v * 100).ToString(), desc, indent);

        static Keys Bind(GameKey key, string label, string desc = "") =>
            new Keys { Key = key, Label = label, Desc = desc, Reset = () => GameSettings.ResetKeys(new[] { key }) };

        static string Percent(float v) => Mathf.RoundToInt(v) + "%";

        // R792: a line shown only while `when` holds (the streamer and colour-blind lines under their switch).
        static T ShownWhen<T>(T item, Func<bool> when) where T : Item
        {
            item.When = when;
            return item;
        }

        static Item[] VoiceItems(out string note)
        {
            note = VoiceLevel == null ? "Steam 연결 시 사용 가능" : "Steam 음성 · 파티와 팀";
            return new Item[]
            {
                Switch("vcon", "음성 채팅", true, "음성 채팅을 켜거나 끕니다."),
                Switch("vcparty", "파티 음성 자동 참여", true, "파티에 참가하면 파티 음성 채팅에 자동으로 연결됩니다."),
                Switch("vcteam", "팀 음성 자동 참여", true,
                       "경기가 시작되면 팀 음성 채팅에 자동으로 연결됩니다. 대화 대상은 경기 중 Tab 키로 바꿀 수 있습니다."),
            };
        }

        static Item[] TalkItems() => new Item[]
        {
            Pick("vcmode", "입력 방식", new[] { "누르고 말하기", "음성 감지" }, 0,
                 "마이크를 켜는 방식을 선택합니다."),
            new Keys
            {
                Key = GameKey.Talk, Label = "누르고 말하기 키", Desc = "누르고 있는 동안 음성이 전송됩니다.",
                Reset = () => GameSettings.ResetKeys(new[] { GameKey.Talk }), When = () => GameSettings.PushToTalk,
            },
            new Gate
            {
                Label = "음성 감지 기준", When = () => !GameSettings.PushToTalk, Reset = () => GameSettings.Forget("vcthresh"),
                Desc = "이 기준보다 큰 소리만 전송됩니다. 키보드 소리 같은 잡음이 들어가지 않도록 조절하세요.",
            },
            new MicTest
            {
                Label = "마이크 테스트",
                Desc = "마이크 입력을 확인합니다. \"들어 보기\"를 누르면 내 목소리를 들어 볼 수 있습니다.",
            },
        };

        const string VoicePage = "음성 채팅";

        static List<Tab> Schema()
        {
            var voiceItems = VoiceItems(out string voiceNote);
            string editorNote = Application.isEditor ? "Unity 에디터에서는 안 바뀌고, 빌드한 게임에서만 바뀌어요." : "";
            var res = Screen.currentResolution;
            string screenNote = $"{res.width} × {res.height} · {Mathf.RoundToInt((float)res.refreshRateRatio.value)}Hz";

            var display = new Item[]
            {
                new Choice
                {
                    Label = "화면 모드", Drop = true, Options = () => GameSettings.ModeNames,
                    Desc = "게임 화면의 표시 방식을 선택합니다.", Note = editorNote,
                    Get = () => GameSettings.ScreenModeIndex, Set = GameSettings.SetScreenMode, Reset = () => GameSettings.SetScreenMode(1),
                },
                new Choice
                {
                    Label = "모니터", Drop = true, Desc = "게임을 표시할 모니터를 선택합니다.", Note = editorNote,
                    Options = () => GameSettings.Monitors.ToArray(), Get = () => GameSettings.MonitorIndex,
                    Set = GameSettings.SetMonitor, Reset = () => GameSettings.SetMonitor(0),
                },
                new Choice
                {
                    Label = "해상도", Drop = true,
                    Desc = "화면 해상도를 선택합니다. 테두리 없는 창에서는 모니터 해상도를 사용합니다.", Note = editorNote,
                    Options = () => GameSettings.Sizes.ConvertAll(s => s.x + " × " + s.y).ToArray(),
                    Get = () => Mathf.Max(0, GameSettings.Sizes.IndexOf(GameSettings.CurrentSize)),
                    Set = i => GameSettings.SetSize(GameSettings.Sizes[i]),
                    Reset = () => GameSettings.SetSize(new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height)),
                },
                Number("fov", "시야 범위", 50f, 80f, 1f, 60f, v => Mathf.RoundToInt(v) + "°",
                       "경기 중 카메라의 시야 범위를 조절합니다."),
                new Choice
                {
                    Label = "프레임 제한", Drop = true, Options = () => GameSettings.FrameNames,
                    Desc = "초당 최대 프레임 수를 제한합니다.",
                    Get = () => Mathf.Max(0, Array.IndexOf(GameSettings.FrameLimits, GameSettings.FrameLimit)),
                    Set = i => GameSettings.SetFrameLimit(GameSettings.FrameLimits[i]), Reset = () => GameSettings.SetFrameLimit(0),
                },
                new Choice
                {
                    Label = "수직 동기화", Options = () => OffOn, Desc = "화면 찢어짐을 방지합니다. 입력 지연이 약간 늘어날 수 있습니다.",
                    Get = () => GameSettings.VSync ? 1 : 0, Set = v => GameSettings.SetVSync(v == 1), Reset = () => GameSettings.SetVSync(true),
                },
                Number("bright", "밝기", .5f, 1.5f, .05f, 1f, v => v.ToString("0.00"), "화면 밝기를 조절합니다."),
            };

            string[] levels4 = { "끔", "낮음", "중간", "높음" };
            var quality = new Item[]
            {
                new Choice
                {
                    Label = "전체 품질", Drop = true, Options = () => GameSettings.Presets,
                    Desc = "그래픽 설정을 한 번에 조절합니다.",
                    Get = () => GameSettings.Preset, Set = GameSettings.SetPreset, Reset = () => GameSettings.SetPreset(2),
                },
                new Choice
                {
                    Label = "그림자", Indent = true, Options = () => levels4, Desc = "그림자 품질을 설정합니다. 성능에 가장 큰 영향을 줍니다.",
                    Get = () => GameSettings.Shadow, Set = v => GameSettings.SetGraphic("shadow", v), Reset = () => GameSettings.SetPreset(2),
                },
                new Choice
                {
                    Label = "안티 에일리어싱", Indent = true, Options = () => new[] { "끔", "2x", "4x", "8x" },
                    Desc = "물체 가장자리의 계단 현상을 줄입니다.",
                    Get = () => GameSettings.Antialias, Set = v => GameSettings.SetGraphic("aa", v), Reset = () => GameSettings.SetPreset(2),
                },
                new Choice
                {
                    Label = "배경 디테일", Indent = true, Options = () => new[] { "낮음", "중간", "높음" },
                    Desc = "멀리 있는 배경의 디테일을 설정합니다.",
                    Get = () => GameSettings.Detail, Set = v => GameSettings.SetGraphic("detail", v), Reset = () => GameSettings.SetPreset(2),
                },
                Switch("bloom", "빛 번짐", true, "밝은 빛 주변에 빛 번짐 효과를 적용합니다.", indent: true),
                // R792: on its own, not part of the presets (GameSettings.RenderScale).
                new Slide
                {
                    Label = "렌더 해상도", Min = GameSettings.MinRenderScale, Max = 1f, Step = .05f, Show = v => Mathf.RoundToInt(v * 100) + "%",
                    Desc = "3D 화면을 그리는 해상도의 비율입니다. 낮추면 성능이 향상되지만 화면이 흐려질 수 있습니다.",
                    Get = () => GameSettings.RenderScale, Set = GameSettings.SetRenderScale, Reset = GameSettings.ResetRenderScale,
                },
            };

            return new List<Tab>
            {
                new Tab { Name = "화면", Pages = new[]
                {
                    new Page { Name = "디스플레이", Sections = new[] { new Section { Title = "디스플레이", Note = screenNote, Items = display } } },
                    new Page { Name = "그래픽 품질", Sections = new[] { new Section { Title = "그래픽 품질", Note = "사양이 낮은 PC는 \"낮음\"을 권장합니다", Items = quality } } },
                    new Page { Name = "정보 표시", Sections = new[] { new Section { Title = "정보 표시", Note = "경기 화면 구석에 표시됩니다", Items = new Item[]
                    {
                        Switch("showfps", "FPS 표시", false, "화면에 초당 프레임 수를 표시합니다."),
                        Switch("showping", "핑 표시", true, "화면에 네트워크 지연 시간을 표시합니다."),
                    } } } },
                } },
                new Tab { Name = "소리", Pages = new[]
                {
                    new Page { Name = "볼륨", Sections = new[]
                    {
                        new Section { Title = "볼륨", Items = new Item[]
                        {
                            Volume("master", "전체 볼륨", 1f, "게임 전체 볼륨을 조절합니다."),
                            Volume("music", "배경 음악", .7f, "배경 음악 볼륨을 조절합니다.", indent: true),
                            Volume("sfx", "효과음", .8f, "효과음 볼륨을 조절합니다.", indent: true),
                            Volume("ui", "메뉴 효과음", .6f, "메뉴와 버튼 효과음 볼륨을 조절합니다.", indent: true),
                            Volume("vc", "음성 채팅", .8f, "다른 플레이어 목소리의 볼륨을 조절합니다.", indent: true),
                        } },
                        new Section { Title = "기타", Items = new Item[]
                        {
                            Switch("bgmute", "백그라운드에서 소리 끄기", true, "게임 창이 비활성 상태일 때 소리를 끕니다."),
                            Switch("readyding", "매칭 완료 알림음", true,
                                   "매칭이 완료되면 게임 창이 비활성 상태여도 알림음을 재생합니다."),
                        } },
                    } },
                    new Page { Name = VoicePage, Sections = new[]
                    {
                        new Section { Title = "음성 채팅", Note = voiceNote, Items = voiceItems },
                        new Section { Title = "말하는 방식", Items = TalkItems() },
                        // R786: the others' voice volume is on the 볼륨 page only (it was on both, one value).
                        new Section { Title = "볼륨", Items = new Item[]
                        {
                            Number("micgain", "마이크 볼륨", 0f, 2f, .05f, 1f, v => Mathf.RoundToInt(v * 100).ToString(),
                                   "다른 플레이어에게 전달되는 내 목소리 볼륨을 조절합니다."),
                        } },
                    } },
                } },
                new Tab { Name = "조작", Pages = new[]
                {
                    new Page { Name = "마우스", Sections = new[] { new Section { Title = "마우스", Items = new Item[]
                    {
                        Number("sens", "마우스 감도", .1f, 3f, .1f, 1f, v => Loc.F("{0}배", v.ToString("0.0")), "마우스로 카메라를 돌리는 속도를 조절합니다."),
                        Switch("invert", "마우스 상하 반전", false, "마우스 상하 움직임을 반전합니다."),
                        Pick("sprintmode", "질주 방식", new[] { "누르고 있기", "눌러서 전환" }, 0,
                             "질주 키를 누르고 있어야 달릴지, 한 번 눌러 전환할지 선택합니다.", drop: true),
                    } } } },
                    new Page { Name = "키 설정", Sections = new[]
                    {
                        new Section { Title = "이동", Note = "칸을 클릭한 뒤 새 키를 누르세요 · 중복된 키는 서로 교체됩니다 · Delete로 지우기", Items = new Item[]
                        {
                            Bind(GameKey.Forward, "앞으로 이동", "캐릭터를 앞으로 이동합니다."),
                            Bind(GameKey.Back, "뒤로 이동", "캐릭터를 뒤로 이동합니다."),
                            Bind(GameKey.Left, "왼쪽으로 이동", "캐릭터를 왼쪽으로 이동합니다."),
                            Bind(GameKey.Right, "오른쪽으로 이동", "캐릭터를 오른쪽으로 이동합니다."),
                            Bind(GameKey.Jump, "점프", "점프합니다."),
                            Bind(GameKey.Sprint, "질주", "빠르게 달립니다. 질주 방식은 마우스 설정에서 바꿀 수 있습니다."),
                        } },
                        new Section { Title = "행동", Items = new Item[]
                        {
                            // R674: named after what each key does in the two modes now (Queen of the Hill's bells,
                            // levers and second abilities are gone); the skill test scenes keep their own keys.
                            Bind(GameKey.Shove, "다이빙 · 칼 베기", "폰 러쉬에서는 앞으로 몸을 날리고, 소드 파이트에서는 칼을 휘두릅니다."),
                            Bind(GameKey.Grab, "잡기 · 조준 취소", "폰 러쉬에서는 상대나 물체를 붙잡고, 소드 파이트에서는 스킬 조준을 취소합니다."),
                            Bind(GameKey.Use, "기물 스킬", "기물 고유 스킬을 사용합니다."),
                            Bind(GameKey.Skill, "두 번째 스킬", "두 번째 스킬을 사용합니다. 폰 러쉬에서 폰은 갈고리를 꺼내거나 넣습니다."),
                            // R786: held while the match says the pawn is stuck (SteamPawnRushLink.Unstuck).
                            Bind(GameKey.Unstuck, "끼었을 때 되돌아가기",
                                 "폰 러쉬에서 지형에 끼었을 때 길게 눌러 지나온 체크포인트로 돌아갑니다. (30초마다 사용 가능)"),
                        } },
                        new Section { Title = "기타", Items = new Item[]
                        {
                            Bind(GameKey.Chat, "채팅 열기", "채팅 입력창을 엽니다."),
                            Bind(GameKey.Sheet, "경기 명단 (기보지)", "경기 중 누르고 있으면 경기 명단이 열립니다. 팀 음성 채팅과 음소거를 관리할 수 있습니다."),
                            Bind(GameKey.Talk, "누르고 말하기", "누르고 있는 동안 음성이 전송됩니다."),
                        } },
                        new Section { Title = "튜토리얼", Items = new Item[]
                        {
                            ShownWhen(new Act
                            {
                                Label = "튜토리얼 다시 하기", Button = "다시 하기",
                                Desc = "조작 튜토리얼을 다시 플레이합니다. 로비에서 혼자일 때만 시작할 수 있습니다.",
                                Refused = "지금은 시작할 수 없습니다. 로비에서 혼자이고 매칭 중이 아닐 때 다시 시도해 주세요.",
                                Run = () => TutorialReplay != null && TutorialReplay(),
                            }, () => TutorialReplay != null),
                        } },
                    } },
                } },
                new Tab { Name = "게임플레이", Pages = new[]
                {
                    new Page { Name = "카메라", Sections = new[] { new Section { Title = "카메라", Items = new Item[]
                    {
                        Number("camdist", "카메라 거리", 2f, 8f, .2f, 3.6f, v => v.ToString("0.0") + "m",
                               "경기 시작 시 카메라 거리를 설정합니다. 경기 중에는 마우스 휠로 조절할 수 있습니다."),
                        Switch("sprintfov", "질주 시 시야 확장", true, "질주할 때 시야를 넓혀 속도감을 높입니다."),
                        Number("shake", "카메라 흔들림", 0f, 100f, 10f, 100f, Percent, "충돌과 스킬 등으로 인한 카메라 흔들림의 세기를 조절합니다."),
                    } } } },
                    new Page { Name = "화면 표시", Sections = new[]
                    {
                        new Section { Title = "이름과 표시", Items = new Item[]
                        {
                            Pick("names", "팀원 이름표", new[] { "항상", "가까울 때만", "끔" }, 0,
                                 "팀원 머리 위에 이름표를 표시합니다.", drop: true),
                            Pick("enemy", "상대 이름표", new[] { "항상", "가까울 때만", "끔" }, 1,
                                 "상대 머리 위에 이름표를 표시합니다.", drop: true),
                            Switch("myname", "내 이름표", false, "내 머리 위에 이름표를 표시합니다."),
                        } },
                        new Section { Title = "일반", Items = new Item[]
                        {
                            LanguageChoice(),
                        } },
                    } },
                } },
                new Tab { Name = "소셜", Pages = new[]
                {
                    new Page { Name = "채팅", Sections = new[] { new Section { Title = "채팅", Items = new Item[]
                    {
                        Switch("chat", "채팅", true, "채팅을 켜거나 끕니다."),
                        Switch("filter", "욕설 필터", true, "채팅의 욕설을 **로 가립니다."),
                        Pick("chatsize", "글자 크기", new[] { "작게", "보통", "크게" }, 1, "채팅 글자 크기를 설정합니다.", drop: true),
                        Number("chatalpha", "채팅 배경 불투명도", 0f, 100f, 10f, 0f, Percent,
                               "경기 중 채팅창 배경의 불투명도를 조절합니다."),
                        Pick("chatfrom", "표시할 채팅", new[] { "모두", "파티와 팀만", "파티만" }, 0,
                             "다른 플레이어의 채팅 중 표시할 채널을 선택합니다.", drop: true),
                    } } } },
                    new Page { Name = "초대", Sections = new[]
                    {
                        new Section { Title = "초대", Items = new Item[]
                        {
                            Pick("invites", "파티 초대 허용", new[] { "모두", "Steam 친구만", "허용 안 함" }, 1,
                                 "게임 안에서 파티 초대를 받을 대상을 선택합니다.", drop: true),
                        } },
                    } },
                    // R792: what this PC's screens keep off a stream (Core StreamerMode).
                    new Page { Name = "방송", Sections = new[] { new Section { Title = "방송", Items = new Item[]
                    {
                        Switch("streamer", "스트리머 모드", false,
                               "방송 중 개인 정보가 화면에 노출되지 않도록 가립니다."),
                        ShownWhen(Switch("hidename", "다른 플레이어 이름 숨기기", true,
                               "다른 플레이어의 이름과 프로필 사진을 숨깁니다.",
                               indent: true), () => GameSettings.Streamer),
                        ShownWhen(Switch("hidecode", "방 번호 · 파티 코드 숨기기", true,
                               "방 번호와 파티 코드를 숨깁니다.", indent: true), () => GameSettings.Streamer),
                    } } } },
                } },
                new Tab { Name = "접근성", Pages = new[]
                {
                    new Page { Name = "보기 편하게", Sections = new[]
                    {
                        new Section { Title = "보기 편하게", Items = new Item[]
                        {
                            // R792: GameLook's channel mixer volume, Core ColourAid's correction.
                            Pick("cb", "색약 모드", new[] { "끔", "적색약", "녹색약", "청색약" }, 0,
                                 "색각 이상이 있는 플레이어를 위해 화면 색을 보정합니다.",
                                 drop: true),
                            ShownWhen(Number("cbs", "보정 강도", 0f, 100f, 10f, 100f, Percent, "색약 모드의 보정 강도를 조절합니다.", indent: true),
                                      () => GameSettings.ColourAidKind != 0),
                            Number("uiscale", "메뉴 크기", .9f, 1.15f, .05f, 1f, v => Mathf.RoundToInt(v * 100) + "%",
                                   "메뉴 화면의 크기를 조절합니다."),
                            Switch("reduceshake", "화면 흔들림 줄이기", false, "카메라 흔들림과 시야 확장 효과를 끕니다."),
                            Switch("reduceflash", "번쩍임 줄이기", false,
                                   "번쩍이는 효과를 약하게 합니다. 빛에 민감한 경우 켜는 것을 권장합니다."),
                        } },
                    } },
                } },
            };
        }

        // ---------- building ----------

        // R747: the language the window was built in; when Loc.Lang moves (picked here, or Steam's arriving) Update
        // builds it again.
        string builtIn;

        void Build()
        {
            builtIn = Loc.Lang;
            tabs = Schema();
            root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("SettingsHud"),
                                        Resources.Load<ThemeStyleSheet>("NetworkTheme"), null,
                                        new Vector2Int(1280, 720), out ownedPanel);
            if (root == null) return;
            if (ownedPanel != null) { ownedPanel.sortingOrder = SortingOrder; ownedPanel.scale = 1f; }

            var bg = root.Q<VisualElement>("s2-bg");
            if (bg != null)
            {
                // Drawn on a worker thread since the game started; if it is not
                // done yet the plain dark ground shows until it is.
                IVisualElementScheduledItem wait = null;
                bool Show()
                {
                    var picture = SettingsBackdrop.TryGet();
                    if (picture == null) return false;
                    bg.style.backgroundImage = new StyleBackground(picture);
                    wait?.Pause();
                    return true;
                }
                if (!Show()) wait = bg.schedule.Execute(() => Show()).Every(100);
            }
            tabsBox = root.Q<VisualElement>("s2-tabs");
            subsBox = root.Q<VisualElement>("s2-subs");
            view = root.Q<VisualElement>("s2-view");
            content = root.Q<VisualElement>("s2-content");
            bar = root.Q<VisualElement>("s2-bar");
            thumb = root.Q<VisualElement>("s2-thumb");
            layer = root.Q<VisualElement>("s2-layer");
            descTitle = root.Q<Label>("s2-desc-title");
            descText = root.Q<Label>("s2-desc-text");
            descNote = root.Q<Label>("s2-desc-note");
            resetText = root.Q<Label>("s2-reset-text");

            string name = null;
            try { name = Identity?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            var nameLabel = root.Q<Label>("s2-name");
            var steam = root.Q<Label>("s2-steam");
            var mark = root.Q<Label>(className: "s2-avatar-mark");
            if (nameLabel != null) nameLabel.text = string.IsNullOrEmpty(name) ? Loc.T("플레이어") : name;
            if (mark != null) mark.text = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
            if (steam != null)
            {
                steam.text = name != null ? Loc.T("STEAM 연결됨") : Loc.T("STEAM 오프라인");
                steam.EnableInClassList("s2-steam-off", name == null);
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                int index = i;
                var tab = new Button { focusable = false };
                tab.AddToClassList("s2-tab");
                var text = new Label(Loc.T(tabs[i].Name)) { pickingMode = PickingMode.Ignore };
                text.AddToClassList("disp");
                text.AddToClassList("s2-tab-text");
                tab.Add(text);
                // 10-30 design A: the chosen tab is underlined in gold (USS), no longer on a glass plate (10-27)
                tab.clicked += () => { if (tabIndex != index) { tabIndex = index; pageIndex = 0; ShowTab(); } };
                ChunkyButtons.Springy(tab);
                tabsBox?.Add(tab);
            }

            Wire(root.Q<Button>("s2-back"), Back);
            Wire(root.Q<Button>("s2-reset"), ResetPage);
            Wire(root.Q<Button>("s2-quit"), AskQuit);
            // R738: the window's sounds. 뒤로 and 게임 나가기 sound as the window closes or the question opens.
            MenuSound.Wire(root, b =>
                b.ClassListContains("s2-tab") || b.ClassListContains("s2-sub") ? MenuSound.Kind.Tab
                : b.ClassListContains("s2-switch") || b.ClassListContains("s2-ar") || b.ClassListContains("s2-opt") || b.ClassListContains("s2-dd") ? MenuSound.Kind.Toggle
                : b.name == "s2-back" || b.name == "s2-quit" ? MenuSound.Kind.None
                : (MenuSound.Kind?)null);
            view?.RegisterCallback<WheelEvent>(e => { ScrollTo(scroll + Mathf.Sign(e.delta.y) * ScrollStep); e.StopPropagation(); });
            view?.RegisterCallback<GeometryChangedEvent>(_ => ScrollTo(scroll));
            content?.RegisterCallback<GeometryChangedEvent>(_ => ScrollTo(scroll));
            DragThumb();
            ShowTab();
        }

        static void Wire(Button button, Action action)
        {
            if (button == null) return;
            button.focusable = false;
            button.clicked += action;
            ChunkyButtons.Springy(button);
        }

        void ShowTab()
        {
            CloseMenu();
            waitingKey = null;
            StopMic();
            if (tabsBox != null)
            {
                int i = 0;
                foreach (var tab in tabsBox.Children())
                    tab.EnableInClassList("s2-tab-on", i++ == tabIndex);
            }
            if (subsBox == null) return;
            subsBox.Clear();
            var pages = tabs[tabIndex].Pages;
            pageIndex = Mathf.Clamp(pageIndex, 0, pages.Length - 1);
            for (int i = 0; i < pages.Length; i++)
            {
                int index = i;
                var sub = new Button { text = Loc.T(pages[i].Name), focusable = false };
                sub.AddToClassList("s2-sub");
                sub.EnableInClassList("s2-sub-on", i == pageIndex);
                sub.clicked += () => { if (pageIndex != index) { pageIndex = index; ShowTab(); } };
                ChunkyButtons.Springy(sub);
                subsBox.Add(sub);
            }
            ShowPage();
        }

        void ShowPage()
        {
            if (content == null) return;
            content.Clear();
            refreshers.Clear();
            var page = tabs[tabIndex].Pages[pageIndex];
            // R747: every name, title and description below goes through Loc.T as it is drawn, so the whole window
            // follows the string table without each line in Schema being wrapped (the Korean stays the key).
            if (resetText != null) resetText.text = Loc.F("{0} 기본값 복원", Loc.T(page.Name));
            Describe(Loc.T(page.Name), Loc.T("항목에 마우스를 올리면 설명이 표시됩니다."), null);
            // The voice page keeps the microphone running for its meters: Steam's when the game is
            // online, else Unity's (no one to talk to then, but the meter still shows the microphone).
            VoicePageOpen = page.Name == VoicePage;
            VoiceEcho = false;
            if (VoicePageOpen && VoiceLevel == null) StartMic();
            foreach (var section in page.Sections)
            {
                var head = Add(content, "s2-section");
                Add(head, "disp s2-section-title", Loc.T(section.Title));
                if (!string.IsNullOrEmpty(section.Note)) Add(head, "s2-section-note", Loc.T(section.Note));
                var group = Add(content, "s2-group");
                foreach (var item in section.Items) Line(group, item);
            }
            RuntimePanels.ApplyDisplay(content);
            ChunkyButtons.Attach(content);
            scroll = 0;
            ScrollTo(0);
            Refresh();
        }

        void Refresh()
        {
            foreach (var refresh in refreshers) refresh();
        }

        void Describe(string title, string text, string note)
        {
            if (descTitle != null) descTitle.text = title;
            if (descText != null) descText.text = text;
            if (descNote == null) return;
            descNote.text = note ?? "";
            descNote.style.display = string.IsNullOrEmpty(note) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // One line: the name, then the value.
        void Line(VisualElement parent, Item item)
        {
            var row = Add(parent, "s2-row");
            row.pickingMode = PickingMode.Position;
            var lab = Add(row, "s2-lab");
            if (item.Indent) lab.style.paddingLeft = 26;
            Add(lab, "s2-lab-text", Loc.T(item.Label));
            var val = Add(row, "s2-val");
            row.RegisterCallback<PointerEnterEvent>(_ => Describe(Loc.T(item.Label), Loc.T(string.IsNullOrEmpty(item.Desc) ? "이 항목에는 설명이 없습니다." : item.Desc),
                                                                 string.IsNullOrEmpty(item.Note) ? item.Note : Loc.T(item.Note)));

            switch (item)
            {
                case Choice c when c.Drop: DropLine(val, c); break;
                case Choice c when ReferenceEquals(c.Options(), OffOn): ToggleLine(val, c); break;
                case Choice c: StepLine(val, c); break;
                case Slide s: SlideLine(val, s); break;
                case Keys k: KeysLine(val, k); break;
                case Act a: ActLine(val, a); break;
                case MicTest _: MicLine(val); break;
                case Gate _: GateLine(val); break;
            }
            if (item.When != null)
                refreshers.Add(() => row.style.display = item.When() ? DisplayStyle.Flex : DisplayStyle.None);
        }

        // ---------- the voice page (R131) ----------

        // The voice-activated threshold as the usual slider: the knob is the threshold, and the rail fills
        // with the live microphone level, dim below the knob and gold once it passes (that voice goes out).
        void GateLine(VisualElement val)
        {
            val.AddToClassList("s2-slide");
            var number = Add(val, "s2-num", "");
            var track = Add(val, "s2-track");
            track.pickingMode = PickingMode.Position;
            var rail = Add(track, "s2-rail");
            var level = Add(rail, "s2-fill s2-level");
            var knob = Add(rail, "s2-knob");
            void Show()
            {
                float threshold = GameSettings.VoiceThreshold;
                knob.style.left = new Length(threshold * 100f, LengthUnit.Percent);
                number.text = Mathf.RoundToInt(threshold * 100f).ToString();
            }
            void At(Vector2 local)
            {
                float width = track.layout.width;
                if (width <= 0) return;
                GameSettings.SetFloat("vcthresh", Mathf.Round(Mathf.Clamp01(local.x / width) * 20f) / 20f);
                Show();
            }
            Drag(track, At);
            refreshers.Add(Show);
            rail.schedule.Execute(() =>
            {
                float now = Mathf.Clamp01(Level);
                level.style.width = new Length(now * 100f, LengthUnit.Percent);
                level.EnableInClassList("s2-level-go", now >= GameSettings.VoiceThreshold);
            }).Every(50);
        }

        static void Drag(VisualElement track, Action<Vector2> at)
        {
            track.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                track.CapturePointer(e.pointerId);
                at(e.localPosition);
            });
            track.RegisterCallback<PointerMoveEvent>(e => { if (track.HasPointerCapture(e.pointerId)) at(e.localPosition); });
            track.RegisterCallback<PointerUpEvent>(e => { if (track.HasPointerCapture(e.pointerId)) track.ReleasePointer(e.pointerId); });
        }

        // The microphone level the meters show: Steam's voice when online, else Unity's microphone.
        float Level
        {
            get
            {
                if (VoiceLevel == null) return micLevel;
                try { return VoiceLevel(); }
                catch (Exception e) { Debug.LogException(e); VoiceLevel = null; return 0f; }
            }
        }

        void StepLine(VisualElement val, Choice c)
        {
            var prev = Arrow(val, "‹");
            var text = Add(val, "s2-vt", "");
            var next = Arrow(val, "›");
            void Step(int by)
            {
                int n = c.Options().Length;
                if (n <= 1) return;
                c.Set((c.Get() + by + n) % n);
                Changed();
            }
            prev.clicked += () => Step(-1);
            next.clicked += () => Step(1);
            refreshers.Add(() =>
            {
                var options = c.Options();
                text.text = options.Length > 0 ? Loc.T(options[Mathf.Clamp(c.Get(), 0, options.Length - 1)]) : "";
                prev.SetEnabled(options.Length > 1);
                next.SetEnabled(options.Length > 1);
            });
        }

        // 끔 / 켬 (10-30 design A): the word, then a switch that slides gold when on.
        void ToggleLine(VisualElement val, Choice c)
        {
            val.AddToClassList("s2-val-toggle");
            var text = Add(val, "s2-toggle-text", "");
            var button = new Button { focusable = false };
            button.AddToClassList("s2-switch");
            Add(button, "s2-switch-knob");
            val.Add(button);
            button.clicked += () => { c.Set(c.Get() == 1 ? 0 : 1); Changed(); };
            ChunkyButtons.Springy(button);
            refreshers.Add(() =>
            {
                bool on = c.Get() == 1;
                text.text = Loc.T(OffOn[on ? 1 : 0]);
                button.EnableInClassList("s2-switch-on", on);
                val.EnableInClassList("s2-on", on);
            });
        }

        void DropLine(VisualElement val, Choice c)
        {
            var button = new Button { focusable = false };
            button.AddToClassList("s2-dd");
            var text = Add(button, "s2-dd-text", "");
            Add(button, "s2-dd-mark", "▼");
            val.Add(button);
            button.clicked += () => { if (openMenu != null) CloseMenu(); else OpenMenu(val, c); };
            refreshers.Add(() =>
            {
                var options = c.Options();
                text.text = options.Length > 0 ? Loc.T(options[Mathf.Clamp(c.Get(), 0, options.Length - 1)]) : "";
            });
        }

        // The list opens under the line (above it when it would run off the bottom),
        // over a click catcher that closes it.
        void OpenMenu(VisualElement anchor, Choice c)
        {
            CloseMenu();
            if (layer == null) return;
            var catcher = Add(layer, "s2-catch");
            catcher.pickingMode = PickingMode.Position;
            catcher.RegisterCallback<PointerDownEvent>(e => { CloseMenu(); e.StopPropagation(); });
            var menu = Add(layer, "s2-menu");
            menu.pickingMode = PickingMode.Position;
            var options = c.Options();
            int chosen = c.Get();
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                var option = new Button { text = Loc.T(options[i]), focusable = false };
                option.AddToClassList("s2-opt");
                option.EnableInClassList("s2-opt-on", i == chosen);
                option.clicked += () => { CloseMenu(); if (c.Get() != index) c.Set(index); Changed(); };
                ChunkyButtons.Springy(option);
                menu.Add(option);
            }
            var box = anchor.worldBound;
            var topLeft = layer.WorldToLocal(new Vector2(box.xMin, box.yMax));
            float height = options.Length * 32f + 10f;
            float limit = layer.layout.height - 50f;
            menu.style.left = topLeft.x;
            menu.style.width = box.width;
            menu.style.top = topLeft.y + height > limit ? Mathf.Max(80f, topLeft.y - box.height - height) : topLeft.y;
            openMenu = layer;
        }

        void CloseMenu()
        {
            layer?.Clear();
            openMenu = null;
        }

        // ---------- 게임 나가기 (R738) ----------

        // What closing the game does. Leaving a match under way this way is a desertion like the in-game menu's 나가기:
        // the session counts it as it shuts down (SteamSession.Dispose), and the match record writes "나감".
        public static Action QuitGame = () =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        };

        // The 게임 나가기 question over the open window (as its button asks it; the lobby capture shows it this way).
        public static void AskQuitNow()
        {
            if (IsOpen) current.AskQuit();
        }

        // R738 (user 11-21, "설정창에 게임 나가기도 넣어주고"): a card over the window that asks first, as the
        // drop-down lists do (on the layer, Esc or a click beside it closes it).
        void AskQuit()
        {
            CloseMenu();
            waitingKey = null;
            if (layer == null) return;
            MenuSound.Play(MenuSound.Kind.Open);
            var catcher = Add(layer, "s2-catch s2-ask-dim");
            catcher.pickingMode = PickingMode.Position;
            catcher.RegisterCallback<PointerDownEvent>(e => { MenuSound.Play(MenuSound.Kind.Close); CloseMenu(); e.StopPropagation(); });
            var card = Add(layer, "s2-ask");
            card.pickingMode = PickingMode.Position;
            Add(card, "disp s2-ask-title", Loc.T("게임을 종료할까요?"));
            string cost = null;
            try { cost = GameMenu.OnlineCost?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            Add(card, "s2-ask-text", Loc.T("게임 창이 닫혀요."));
            if (!string.IsNullOrEmpty(cost)) Add(card, "s2-ask-warn", cost);
            var row = Add(card, "row s2-ask-row");
            var stay = new Button { text = Loc.T("취소"), focusable = false };
            stay.AddToClassList("s2-ask-btn");
            stay.AddToClassList(MenuSound.SoundClass(MenuSound.Kind.Close));
            stay.clicked += CloseMenu;
            ChunkyButtons.Springy(stay);
            row.Add(stay);
            var quit = new Button { text = Loc.T("게임 나가기"), focusable = false };
            quit.AddToClassList("s2-ask-btn");
            quit.AddToClassList("s2-ask-quit");
            quit.AddToClassList(MenuSound.SoundClass(MenuSound.Kind.Confirm));
            quit.clicked += () =>
            {
                CloseMenu();
                try { QuitGame?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            };
            ChunkyButtons.Springy(quit);
            row.Add(quit);
            RuntimePanels.ApplyDisplay(card);
            openMenu = layer;
        }

        void SlideLine(VisualElement val, Slide s)
        {
            // the value right of the rail (s2-slide reverses the row)
            val.AddToClassList("s2-slide");
            var number = Add(val, "s2-num", "");
            var track = Add(val, "s2-track");
            track.pickingMode = PickingMode.Position;
            var rail = Add(track, "s2-rail");
            var fill = Add(rail, "s2-fill");
            var knob = Add(rail, "s2-knob");
            float Snap(float v) => Mathf.Clamp(Mathf.Round((v - s.Min) / s.Step) * s.Step + s.Min, s.Min, s.Max);
            void Show()
            {
                float v = s.Get();
                float t = Mathf.InverseLerp(s.Min, s.Max, v) * 100f;
                fill.style.width = new Length(t, LengthUnit.Percent);
                knob.style.left = new Length(t, LengthUnit.Percent);
                number.text = s.Show(v);
            }
            void At(Vector2 local)
            {
                float width = track.layout.width;
                if (width <= 0) return;
                float v = Snap(Mathf.Lerp(s.Min, s.Max, Mathf.Clamp01(local.x / width)));
                if (Mathf.Abs(v - s.Get()) > 1e-4f)
                {
                    s.Set(v);
                    MenuSound.Play(MenuSound.Kind.Toggle);   // R738: a tick a step (after the set: 버튼 소리 ticks at its new volume)
                }
                Show();
            }
            track.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                track.CapturePointer(e.pointerId);
                At(e.localPosition);
            });
            track.RegisterCallback<PointerMoveEvent>(e => { if (track.HasPointerCapture(e.pointerId)) At(e.localPosition); });
            track.RegisterCallback<PointerUpEvent>(e => { if (track.HasPointerCapture(e.pointerId)) track.ReleasePointer(e.pointerId); });
            refreshers.Add(Show);
        }

        // R842: one button, in the key buttons' look.
        void ActLine(VisualElement val, Act a)
        {
            var box = Add(val, "s2-keys");
            var button = new Button { text = Loc.T(a.Button), focusable = false };
            button.AddToClassList("chunky");
            button.AddToClassList("ch-ivory");
            button.AddToClassList("ch-xs");
            button.AddToClassList("s2-key");
            button.AddToClassList(MenuSound.SoundClass(MenuSound.Kind.Confirm));
            button.clicked += () =>
            {
                if (a.Run != null && a.Run()) return;
                Describe(Loc.T(a.Label), Loc.T(a.Refused), null);
            };
            ChunkyButtons.Springy(button);
            box.Add(button);
        }

        void KeysLine(VisualElement val, Keys k)
        {
            var box = Add(val, "s2-keys");
            for (int slot = 0; slot < 2; slot++)
            {
                int s = slot;
                var button = new Button { focusable = false };
                button.AddToClassList("chunky");
                button.AddToClassList("ch-ivory");
                button.AddToClassList("ch-xs");
                button.AddToClassList("s2-key");
                button.clicked += () =>
                {
                    bool same = waitingKey == k.Key && waitingSlot == s;
                    waitingKey = same ? (GameKey?)null : k.Key;
                    waitingSlot = s;
                    waitingFrame = Time.frameCount;
                    Refresh();
                };
                box.Add(button);
                refreshers.Add(() =>
                {
                    bool asking = waitingKey == k.Key && waitingSlot == s;
                    // Gold while it waits for a key; ivory otherwise.
                    button.EnableInClassList("ch-ivory", !asking);
                    SetText(button, asking ? Loc.T("키를 누르세요…") : GameSettings.KeyName(GameSettings.Key(k.Key, s)));
                    button.MarkDirtyRepaint();
                });
            }
        }

        // The microphone check (R131): the level meter, and "들어 보기" (my own voice played back to me only)
        // as a gold word like the ‹ › arrows. A microphone problem replaces the meter with its note.
        void MicLine(VisualElement val)
        {
            var meter = Add(val, "s2-meter");
            var cells = new VisualElement[16];
            for (int i = 0; i < cells.Length; i++) cells[i] = Add(meter, "s2-meter-cell");
            var note = Add(val, "s2-mic-note", "");
            var listen = Arrow(val, Loc.T("들어 보기"));
            listen.AddToClassList("s2-test");
            listen.clicked += () => { if (VoiceLevel != null) { VoiceEcho = !VoiceEcho; Refresh(); } };
            refreshers.Add(() =>
            {
                bool steam = VoiceLevel != null;
                string problem = "";
                try { problem = VoiceProblem?.Invoke() ?? ""; } catch (Exception e) { Debug.LogException(e); }
                if (problem == "" && !steam && Microphone.devices.Length == 0) problem = Loc.T("마이크가 없어요");
                note.text = problem;
                note.style.display = problem != "" ? DisplayStyle.Flex : DisplayStyle.None;
                meter.style.display = problem == "" ? DisplayStyle.Flex : DisplayStyle.None;
                listen.SetEnabled(steam);
                SetText(listen, Loc.T(!steam ? "Steam 필요" : VoiceEcho ? "그만 듣기" : "들어 보기"));
            });
            meter.schedule.Execute(() =>
            {
                int lit = Mathf.RoundToInt(Mathf.Clamp01(Level) * cells.Length);
                for (int i = 0; i < cells.Length; i++)
                {
                    cells[i].EnableInClassList("s2-meter-on", i < lit && i < 12);
                    cells[i].EnableInClassList("s2-meter-hot", i < lit && i >= 12);
                }
            }).Every(50);
        }

        void StartMic()
        {
            if (Microphone.devices.Length == 0) return;
            micDevice = Microphone.devices[0];
            micClip = Microphone.Start(micDevice, true, 1, 16000);
        }

        void StopMic()
        {
            if (micClip == null) return;
            Microphone.End(micDevice);
            Destroy(micClip);
            micClip = null;
            micLevel = 0;
        }

        readonly float[] micSamples = new float[512];

        void ReadMic()
        {
            if (micClip == null) return;
            int at = Microphone.GetPosition(micDevice) - micSamples.Length;
            if (at < 0) return;
            micClip.GetData(micSamples, at);
            float sum = 0;
            foreach (var v in micSamples) sum += v * v;
            float rms = Mathf.Sqrt(sum / micSamples.Length);
            micLevel = Mathf.Lerp(micLevel, ChessFight.Network.VoiceLevel.FromRms(rms), .5f);
        }

        Button Arrow(VisualElement parent, string text)
        {
            var button = new Button { text = text, focusable = false };
            button.AddToClassList("s2-ar");
            ChunkyButtons.Springy(button);
            parent.Add(button);
            return button;
        }

        void Changed() => Refresh();

        // ---------- scrolling ----------

        void ScrollTo(float y)
        {
            if (view == null || content == null) return;
            float max = Mathf.Max(0, content.layout.height - view.layout.height);
            scroll = Mathf.Clamp(y, 0, max);
            content.style.top = -scroll;
            if (bar == null || thumb == null) return;
            bar.style.display = max > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            float barHeight = bar.layout.height;
            float size = Mathf.Max(30f, barHeight * view.layout.height / Mathf.Max(1, content.layout.height));
            thumb.style.height = size;
            thumb.style.top = max > 0 ? (barHeight - size) * scroll / max : 0;
        }

        void DragThumb()
        {
            if (thumb == null || bar == null) return;
            float grabY = 0, grabScroll = 0;
            thumb.RegisterCallback<PointerDownEvent>(e =>
            {
                thumb.CapturePointer(e.pointerId);
                grabY = e.position.y; grabScroll = scroll;
                e.StopPropagation();
            });
            thumb.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!thumb.HasPointerCapture(e.pointerId)) return;
                float max = Mathf.Max(0, content.layout.height - view.layout.height);
                float travel = Mathf.Max(1, bar.layout.height - thumb.layout.height);
                ScrollTo(grabScroll + (e.position.y - grabY) / travel * max);
            });
            thumb.RegisterCallback<PointerUpEvent>(e => { if (thumb.HasPointerCapture(e.pointerId)) thumb.ReleasePointer(e.pointerId); });
            bar.RegisterCallback<PointerDownEvent>(e =>
            {
                float max = Mathf.Max(0, content.layout.height - view.layout.height);
                ScrollTo(e.localPosition.y / Mathf.Max(1, bar.layout.height) * max);
            });
        }

        // ---------- keys, reset ----------

        void Update()
        {
            if (builtIn != Loc.Lang) { Rebuild(); return; }
            ReadMic();
            if (waitingKey != null)
            {
                foreach (var key in Capturable())
                {
                    if (!LegacyKeys.Down(key)) continue;
                    // The click that started the wait is not the answer, nor a click
                    // on another button (that button handles it).
                    if (key >= KeyCode.Mouse0 && (Time.frameCount <= waitingFrame || OverButton())) continue;
                    GameSettings.SetKey(waitingKey.Value, waitingSlot, key == KeyCode.Delete ? KeyCode.None : key);
                    waitingKey = null;
                    Refresh();
                    return;
                }
                return;
            }
            if (openMenu == null && LegacyKeys.Down(KeyCode.Delete)) ResetPage();
        }

        bool OverButton()
        {
            if (root?.panel == null) return false;
            Vector2 mouse;
            try { mouse = Input.mousePosition; }
            catch (InvalidOperationException) { return false; }
            var point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(mouse.x, Screen.height - mouse.y));
            for (var e = root.panel.Pick(point); e != null; e = e.parent)
                if (e is Button) return true;
            return false;
        }

        // Every key a player could bind: keyboard and mouse buttons 1-5, not Esc (it cancels).
        static KeyCode[] Capturable()
        {
            if (capturable != null) return capturable;
            var list = new List<KeyCode>();
            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (key == KeyCode.None || key == KeyCode.Escape || list.Contains(key)) continue;
                if (key > KeyCode.Mouse4) continue;   // Mouse5, Mouse6 and every joystick button follow
                list.Add(key);
            }
            return capturable = list.ToArray();
        }

        // R747: the language changed - a new window in it on the same tab and page (both static), without the open
        // and close sounds.
        void Rebuild()
        {
            if (current != this) return;
            // The microphone goes first: the new window starts its own, and this one's OnDestroy comes at the end of
            // the frame, after it (and leaves the voice page flags to the new window).
            StopMic();
            current = new GameObject("Settings Window").AddComponent<SettingsWindow>();
            current.Build();
            Destroy(gameObject);
        }

        void ResetPage()
        {
            CloseMenu();
            waitingKey = null;
            foreach (var section in tabs[tabIndex].Pages[pageIndex].Sections)
                foreach (var item in section.Items)
                    item.Reset?.Invoke();
            Refresh();
        }

        // ---------- helpers ----------

        // A slab button keeps its words on a child label (ChunkyButtons); write
        // there so the change shows this frame, not on the next poll.
        static void SetText(Button button, string text)
        {
            if (button == null) return;
            var label = button.Q<Label>(className: "chunky-text");
            if (label != null) label.text = text;
            else button.text = text;
        }

        static Label Add(VisualElement parent, string classes, string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (var c in classes.Split(' ')) if (c.Length > 0) label.AddToClassList(c);
            parent.Add(label);
            return label;
        }

        static VisualElement Add(VisualElement parent, string classes)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (var c in classes.Split(' ')) if (c.Length > 0) element.AddToClassList(c);
            parent.Add(element);
            return element;
        }

        void OnDestroy()
        {
            StopMic();
            if (current == this || current == null) VoicePageOpen = VoiceEcho = false;
            if (current == this) current = null;
            if (ownedPanel != null) Destroy(ownedPanel);
        }
    }
}
