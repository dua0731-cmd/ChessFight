using System;
using System.Collections.Generic;
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
    // Lines marked "준비 중" are saved but nothing in the game reads them yet
    // (voice chat, controller, name tags...).
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

        PanelSettings ownedPanel;
        VisualElement root, tabsBox, subsBox, view, content, bar, thumb, layer;
        Label descTitle, descText, resetText;
        readonly List<Action> refreshers = new List<Action>();
        readonly List<Tab> tabs = Schema();
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
            current = new GameObject("Settings Window").AddComponent<SettingsWindow>();
            current.Build();
        }

        public static void Back()
        {
            if (!IsOpen) return;
            if (current.openMenu != null) { current.CloseMenu(); return; }
            if (current.waitingKey != null) { current.waitingKey = null; current.Refresh(); return; }
            Close();
        }

        public static void Close()
        {
            if (!IsOpen) return;
            Destroy(current.gameObject);
            current = null;
        }

        // ---------- what the window holds ----------

        abstract class Item
        {
            public string Label, Desc;
            public bool Indent, Soon;
            public Action Reset;
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
        sealed class MicTest : Item { }

        sealed class Section { public string Title, Note; public Item[] Items; }
        sealed class Page { public string Name; public Section[] Sections; }
        sealed class Tab { public string Name; public Page[] Pages; }

        static readonly string[] OffOn = { "끔", "켬" };
        const string Soon = "아직 게임에 이 기능이 없어 값만 저장돼요.";

        // A value saved under an id; the game may not read it yet.
        static Choice Pick(string id, string label, string[] options, int fallback, string desc, bool drop = false,
                           bool soon = false, bool indent = false) =>
            new Choice
            {
                Label = label, Desc = desc, Soon = soon, Indent = indent, Drop = drop, Options = () => options,
                Get = () => Mathf.Clamp(GameSettings.Int(id, fallback), 0, options.Length - 1),
                Set = v => GameSettings.SetInt(id, v), Reset = () => GameSettings.Forget(id),
            };

        static Choice Switch(string id, string label, bool fallback, string desc, bool soon = false, bool indent = false) =>
            Pick(id, label, OffOn, fallback ? 1 : 0, desc, false, soon, indent);

        static Slide Number(string id, string label, float min, float max, float step, float fallback,
                            Func<float, string> show, string desc, bool soon = false, bool indent = false) =>
            new Slide
            {
                Label = label, Desc = desc, Soon = soon, Indent = indent, Min = min, Max = max, Step = step, Show = show,
                Get = () => GameSettings.Float(id, fallback), Set = v => GameSettings.SetFloat(id, v),
                Reset = () => GameSettings.Forget(id),
            };

        static Slide Volume(string id, string label, float fallback, string desc, bool soon = false, bool indent = false) =>
            Number(id, label, 0f, 1f, .05f, fallback, v => Mathf.RoundToInt(v * 100).ToString(), desc, soon, indent);

        static Keys Bind(GameKey key, string label, string desc = "", bool soon = false) =>
            new Keys { Key = key, Label = label, Desc = desc, Soon = soon, Reset = () => GameSettings.ResetKeys(new[] { key }) };

        static string Percent(float v) => Mathf.RoundToInt(v) + "%";

        static List<Tab> Schema()
        {
            string editorNote = Application.isEditor ? " (Unity 에디터에서는 안 바뀌고, 빌드한 게임에서 바뀌어요.)" : "";
            var res = Screen.currentResolution;
            string screenNote = $"{res.width} × {res.height} · {Mathf.RoundToInt((float)res.refreshRateRatio.value)}Hz";

            var display = new Item[]
            {
                new Choice
                {
                    Label = "화면 모드", Drop = true, Options = () => GameSettings.ModeNames,
                    Desc = "테두리 없는 창은 다른 창으로 빨리 넘어갈 수 있고, 전체 화면은 입력 지연이 조금 더 적어요." + editorNote,
                    Get = () => GameSettings.ScreenModeIndex, Set = GameSettings.SetScreenMode, Reset = () => GameSettings.SetScreenMode(1),
                },
                new Choice
                {
                    Label = "모니터", Drop = true, Desc = "게임을 띄울 모니터." + editorNote,
                    Options = () => GameSettings.Monitors.ToArray(), Get = () => GameSettings.MonitorIndex,
                    Set = GameSettings.SetMonitor, Reset = () => GameSettings.SetMonitor(0),
                },
                new Choice
                {
                    Label = "해상도", Drop = true,
                    Desc = "창 모드와 전체 화면에서 쓰는 크기. 테두리 없는 창은 모니터 크기를 따라가요." + editorNote,
                    Options = () => GameSettings.Sizes.ConvertAll(s => s.x + " × " + s.y).ToArray(),
                    Get = () => Mathf.Max(0, GameSettings.Sizes.IndexOf(GameSettings.CurrentSize)),
                    Set = i => GameSettings.SetSize(GameSettings.Sizes[i]),
                    Reset = () => GameSettings.SetSize(new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height)),
                },
                Number("fov", "시야 범위", 50f, 80f, 1f, 60f, v => Mathf.RoundToInt(v) + "°",
                       "경기 카메라에 보이는 넓이. 넓을수록 주변이 잘 보이지만 멀미가 날 수 있어요."),
                new Choice
                {
                    Label = "프레임 제한", Drop = true, Options = () => GameSettings.FrameNames,
                    Desc = "1초에 그릴 최대 장면 수. 모니터 주사율에 맞추면 부드럽고 전기도 덜 써요. 수직 동기화가 켜져 있으면 주사율을 따라가요.",
                    Get = () => Mathf.Max(0, Array.IndexOf(GameSettings.FrameLimits, GameSettings.FrameLimit)),
                    Set = i => GameSettings.SetFrameLimit(GameSettings.FrameLimits[i]), Reset = () => GameSettings.SetFrameLimit(0),
                },
                new Choice
                {
                    Label = "수직 동기화", Options = () => OffOn, Desc = "화면이 가로로 찢어지는 것을 막아요. 켜면 입력이 조금 늦게 느껴질 수 있어요.",
                    Get = () => GameSettings.VSync ? 1 : 0, Set = v => GameSettings.SetVSync(v == 1), Reset = () => GameSettings.SetVSync(true),
                },
                Number("bright", "밝기", .5f, 1.5f, .05f, 1f, v => v.ToString("0.00"), "전체 밝기. 어두운 곳이 안 보이면 올리세요."),
            };

            string[] levels4 = { "끔", "낮음", "중간", "높음" };
            var quality = new Item[]
            {
                new Choice
                {
                    Label = "전체 품질", Drop = true, Options = () => GameSettings.Presets,
                    Desc = "아래 항목을 한 번에 맞춰요. 아래를 하나라도 바꾸면 \"사용자 지정\"이 돼요. 낮음은 사양 낮은 PC용(Mobile 단계).",
                    Get = () => GameSettings.Preset, Set = GameSettings.SetPreset, Reset = () => GameSettings.SetPreset(2),
                },
                new Choice
                {
                    Label = "그림자", Indent = true, Options = () => levels4, Desc = "말과 장애물의 그림자. 프레임에 가장 크게 영향을 줘요.",
                    Get = () => GameSettings.Shadow, Set = v => GameSettings.SetGraphic("shadow", v), Reset = () => GameSettings.SetPreset(2),
                },
                new Choice
                {
                    Label = "계단 현상 줄이기", Indent = true, Options = () => new[] { "끔", "2x", "4x", "8x" },
                    Desc = "말과 체스판 가장자리의 지글거림을 줄여요(MSAA).",
                    Get = () => GameSettings.Antialias, Set = v => GameSettings.SetGraphic("aa", v), Reset = () => GameSettings.SetPreset(2),
                },
                new Choice
                {
                    Label = "배경 디테일", Indent = true, Options = () => new[] { "낮음", "중간", "높음" },
                    Desc = "멀리 있는 성벽과 장식을 얼마나 자세히 그릴지.",
                    Get = () => GameSettings.Detail, Set = v => GameSettings.SetGraphic("detail", v), Reset = () => GameSettings.SetPreset(2),
                },
                Pick("water", "물 표현", new[] { "낮음", "높음" }, 1, "해자와 물웅덩이의 반사와 물결.", soon: true, indent: true),
                Pick("particle", "효과 양", new[] { "적게", "보통", "많이" }, 1, "폭죽, 불꽃, 먼지 같은 효과의 양.", soon: true, indent: true),
                Switch("bloom", "빛 번짐", true, "금색 장식과 조명 주위가 은은하게 번져요.", soon: true, indent: true),
            };

            return new List<Tab>
            {
                new Tab { Name = "화면", Pages = new[]
                {
                    new Page { Name = "디스플레이", Sections = new[] { new Section { Title = "디스플레이", Note = screenNote, Items = display } } },
                    new Page { Name = "그래픽 품질", Sections = new[] { new Section { Title = "그래픽 품질", Note = "사양이 낮은 PC는 \"낮음\"부터", Items = quality } } },
                    new Page { Name = "정보 표시", Sections = new[] { new Section { Title = "정보 표시", Note = "경기 화면 구석에 작게", Items = new Item[]
                    {
                        Switch("showfps", "프레임 표시", false, "오른쪽 위에 초당 프레임 수."),
                        Switch("showping", "핑 표시", true, "방장과 주고받는 데 걸리는 시간. 끊김 경고는 끄더라도 그대로 떠요."),
                        Switch("netgraph", "연결 상태 그래프", false, "핑과 끊김을 작은 그래프로.", soon: true),
                    } } } },
                } },
                new Tab { Name = "소리", Pages = new[]
                {
                    new Page { Name = "볼륨", Sections = new[]
                    {
                        new Section { Title = "볼륨", Items = new Item[]
                        {
                            Volume("master", "전체 소리", 1f, "모든 소리의 크기."),
                            Volume("music", "배경 음악", .7f, "로비와 경기 음악.", soon: true, indent: true),
                            Volume("sfx", "효과음", .8f, "부딪힘, 점프, 대포 같은 경기 소리.", soon: true, indent: true),
                            Volume("ui", "버튼 소리", .6f, "메뉴 버튼과 알림 소리.", soon: true, indent: true),
                            Volume("vc", "음성 채팅", .8f, "다른 사람 목소리 크기.", soon: true, indent: true),
                        } },
                        new Section { Title = "기타", Items = new Item[]
                        {
                            Switch("bgmute", "창이 뒤에 있으면 소리 끄기", true, "다른 창을 보고 있을 때 게임 소리를 꺼요."),
                            Switch("readyding", "경기 잡힘 알림음", true, "매칭이 잡히면 다른 창에 있어도 들리게 울려요.", soon: true),
                        } },
                    } },
                    new Page { Name = "음성 채팅", Sections = new[]
                    {
                        new Section { Title = "음성 채팅", Note = "음성 채팅이 들어가면 적용", Items = new Item[]
                        {
                            Switch("vcon", "음성 채팅", true, "끄면 듣지도 말하지도 않아요.", soon: true),
                            Pick("vcmode", "말하는 방식", new[] { "누르고 말하기", "항상 켜기", "소리 나면 켜기" }, 0,
                                 "누르고 말하기는 아래 키를 누르는 동안만 내 목소리가 나가요.", drop: true, soon: true),
                            Bind(GameKey.Talk, "누르고 말하기 키", "누르는 동안 말해요.", soon: true),
                            Volume("mic", "마이크 크기", .8f, "내 목소리가 나가는 크기.", soon: true),
                            new MicTest { Label = "마이크 테스트", Desc = "\"테스트\"를 누르고 말해 보세요. 금색 칸이 움직이면 마이크가 잡혀요." },
                            Pick("vcchan", "기본 채널", new[] { "파티", "팀" }, 0, "경기 중에는 팀 채널로 바뀌어요.", drop: true, soon: true),
                        } },
                    } },
                } },
                new Tab { Name = "조작", Pages = new[]
                {
                    new Page { Name = "마우스", Sections = new[] { new Section { Title = "마우스", Items = new Item[]
                    {
                        Number("sens", "마우스 감도", .1f, 3f, .1f, 1f, v => v.ToString("0.0") + "배", "시점이 도는 빠르기."),
                        Switch("invert", "상하 반전", false, "마우스를 올리면 아래를 봐요."),
                        Pick("sprintmode", "질주 방식", new[] { "누르고 있기", "한 번 눌러 켜기" }, 0,
                             "한 번 눌러 켜기는 다시 누르거나 멈출 때까지 달려요.", drop: true),
                    } } } },
                    new Page { Name = "키 설정", Sections = new[]
                    {
                        new Section { Title = "이동", Note = "칸을 누르고 바꿀 키를 누르세요 · 겹치면 서로 바뀌어요 · Delete로 비우기", Items = new Item[]
                        {
                            Bind(GameKey.Forward, "앞으로"), Bind(GameKey.Back, "뒤로"), Bind(GameKey.Left, "왼쪽"), Bind(GameKey.Right, "오른쪽"),
                            Bind(GameKey.Jump, "점프"), Bind(GameKey.Sprint, "질주"),
                        } },
                        new Section { Title = "행동", Items = new Item[]
                        {
                            Bind(GameKey.Shove, "밀치기", "앞 상대를 밀어요. 누르고 있으면 갈고리를 휘둘러요."),
                            Bind(GameKey.Grab, "잡기", "누르는 동안 잡아요."),
                            Bind(GameKey.Skill, "스킬", "말마다 다른 주 스킬. (스킬 시험 씬은 아직 따로 F를 써요.)"),
                            Bind(GameKey.Skill2, "보조 스킬", "퀸과 킹의 두 번째 스킬."),
                            Bind(GameKey.Use, "상호작용", "종, 레버, 앙파상(0.4초 누르기)."),
                            Bind(GameKey.Respawn, "구간 처음으로", "현재 구간 입구에서 다시 시작."),
                        } },
                        new Section { Title = "기타", Items = new Item[]
                        {
                            Bind(GameKey.Chat, "채팅 열기"),
                            Bind(GameKey.Score, "점수판 보기", "누르는 동안 양 팀 점수와 핑.", soon: true),
                        } },
                    } },
                    new Page { Name = "컨트롤러", Sections = new[] { new Section { Title = "컨트롤러", Note = "Xbox · PlayStation 패드", Items = new Item[]
                    {
                        Number("padsens", "스틱 감도", .1f, 3f, .1f, 1f, v => v.ToString("0.0") + "배", "오른쪽 스틱으로 시점이 도는 빠르기.", soon: true),
                        Number("dead", "스틱 데드존", 0f, 30f, 1f, 10f, Percent, "스틱이 이만큼 움직이기 전에는 무시해요.", soon: true),
                        Switch("rumble", "진동", true, "부딪히거나 밀려날 때 패드가 떨려요.", soon: true),
                        Pick("padicons", "버튼 그림", new[] { "자동", "Xbox", "PlayStation" }, 0, "화면에 나오는 버튼 모양.", drop: true, soon: true),
                    } } } },
                } },
                new Tab { Name = "게임플레이", Pages = new[]
                {
                    new Page { Name = "카메라", Sections = new[] { new Section { Title = "카메라", Items = new Item[]
                    {
                        Number("camdist", "카메라 거리", 2f, 8f, .2f, 3.6f, v => v.ToString("0.0") + "m",
                               "경기를 시작할 때 말과 카메라 사이 거리. 경기 중에는 휠로 바꿔요."),
                        Switch("sprintfov", "질주할 때 시야 넓어짐", true, "달릴 때 화면이 살짝 넓어져 빨라 보여요."),
                        Number("shake", "카메라 흔들림", 0f, 100f, 10f, 100f, Percent, "부딪힘, 스킬, 결과 화면의 흔들림 세기."),
                    } } } },
                    new Page { Name = "화면 표시", Sections = new[]
                    {
                        new Section { Title = "이름과 표시", Items = new Item[]
                        {
                            Pick("names", "팀원 이름", new[] { "항상", "가까울 때만", "끔" }, 0, "팀원 머리 위 이름.", drop: true, soon: true),
                            Pick("enemy", "상대 이름", new[] { "항상", "가까울 때만", "끔" }, 1, "상대 머리 위 이름.", drop: true, soon: true),
                            Switch("myname", "내 이름", false, "내 머리 위에도 이름 표시.", soon: true),
                            Switch("hints", "조작 도움말", true, "처음 보는 장애물 옆에 짧은 설명.", soon: true),
                            Number("hud", "HUD 크기", 80f, 130f, 5f, 100f, Percent, "점수, 시간, 스태미나 같은 경기 화면 크기.", soon: true),
                        } },
                        new Section { Title = "일반", Items = new Item[]
                        {
                            Pick("lang", "언어", new[] { "한국어" }, 0, "다른 언어는 준비 중이에요.", drop: true),
                            Switch("skipresult", "결과 화면 건너뛰기", false, "켜면 결과 연출을 클릭으로 건너뛸 수 있어요.", soon: true),
                        } },
                    } },
                } },
                new Tab { Name = "소셜", Pages = new[]
                {
                    new Page { Name = "채팅", Sections = new[] { new Section { Title = "채팅", Items = new Item[]
                    {
                        Switch("chat", "채팅", true, "끄면 채팅이 숨고 열리지 않아요."),
                        Switch("filter", "욕설 가리기", true, "흔한 욕을 ** 로 가려요. 내 화면에서만."),
                        Pick("chatsize", "글자 크기", new[] { "작게", "보통", "크게" }, 1, "채팅 글자 크기.", drop: true),
                        Number("chatalpha", "채팅 배경 진하기", 0f, 100f, 10f, 60f, Percent, "경기 중 채팅 뒤 배경.", soon: true),
                        Pick("chatfrom", "채팅 받기", new[] { "모두", "파티와 팀만", "파티만" }, 0, "전체 채널을 끄고 싶을 때.", drop: true, soon: true),
                    } } } },
                    new Page { Name = "초대와 개인정보", Sections = new[]
                    {
                        new Section { Title = "초대", Items = new Item[]
                        {
                            Pick("invites", "파티 초대 받기", new[] { "모두", "Steam 친구만", "받지 않기" }, 1, "누가 나를 파티에 부를 수 있는지.", drop: true, soon: true),
                            Switch("invitesound", "초대 알림음", true, "초대가 오면 소리로 알려요.", soon: true),
                        } },
                        new Section { Title = "방송할 때", Items = new Item[]
                        {
                            Switch("streamer", "스트리머 모드", false, "켜면 아래 둘이 함께 켜져요.", soon: true),
                            Switch("hidename", "다른 사람 Steam 이름 가리기", false, "\"플레이어 3\"처럼 보여요.", soon: true, indent: true),
                            Switch("hidecode", "방 번호·파티 코드 가리기", false, "화면에 ●●●●로 보이고, 복사 버튼은 그대로 돼요.", soon: true, indent: true),
                        } },
                    } },
                } },
                new Tab { Name = "접근성", Pages = new[]
                {
                    new Page { Name = "보기 편하게", Sections = new[]
                    {
                        new Section { Title = "색과 크기", Items = new Item[]
                        {
                            Pick("cb", "색약 모드", new[] { "끔", "적색약", "녹색약", "청색약" }, 0, "백팀·흑팀과 경고 색을 구분하기 쉬운 색으로 바꿔요.", drop: true, soon: true),
                            Number("cbs", "색 보정 세기", 0f, 100f, 10f, 100f, Percent, "색약 모드의 세기.", soon: true, indent: true),
                            Number("uiscale", "메뉴 크기", .9f, 1.15f, .05f, 1f, v => Mathf.RoundToInt(v * 100) + "%",
                                   "인트로, 로비, 채팅 같은 메뉴 화면의 크기. 이 설정 화면은 그대로예요."),
                            Switch("outline", "팀 테두리", false, "말에 팀 색 테두리를 둘러요.", soon: true),
                        } },
                        new Section { Title = "움직임과 번쩍임", Items = new Item[]
                        {
                            Switch("reduceshake", "화면 흔들림 줄이기", false, "카메라 흔들림과 질주 시야 넓어짐을 한 번에 꺼요."),
                            Switch("reduceflash", "번쩍임 줄이기", false, "버튼을 누를 때의 불꽃과 하얀 번쩍임을 약하게 해요."),
                            Switch("subtitles", "소리 알림 글자", false, "대포, 종, 경고음 같은 소리가 날 때 화면에 짧게 글자로 알려요.", soon: true),
                        } },
                    } },
                } },
            };
        }

        // ---------- building ----------

        void Build()
        {
            root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("SettingsHud"),
                                        Resources.Load<ThemeStyleSheet>("NetworkTheme"), null,
                                        new Vector2Int(1280, 720), out ownedPanel);
            if (root == null) return;
            if (ownedPanel != null) { ownedPanel.sortingOrder = SortingOrder; ownedPanel.scale = 1f; }

            var bg = root.Q<VisualElement>("s2-bg");
            if (bg != null) bg.style.backgroundImage = new StyleBackground(SettingsBackdrop.Texture);
            tabsBox = root.Q<VisualElement>("s2-tabs");
            subsBox = root.Q<VisualElement>("s2-subs");
            view = root.Q<VisualElement>("s2-view");
            content = root.Q<VisualElement>("s2-content");
            bar = root.Q<VisualElement>("s2-bar");
            thumb = root.Q<VisualElement>("s2-thumb");
            layer = root.Q<VisualElement>("s2-layer");
            descTitle = root.Q<Label>("s2-desc-title");
            descText = root.Q<Label>("s2-desc-text");
            resetText = root.Q<Label>("s2-reset-text");

            string name = null;
            try { name = Identity?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            var nameLabel = root.Q<Label>("s2-name");
            var steam = root.Q<Label>("s2-steam");
            var mark = root.Q<Label>(className: "s2-avatar-mark");
            if (nameLabel != null) nameLabel.text = string.IsNullOrEmpty(name) ? "플레이어" : name;
            if (mark != null) mark.text = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
            if (steam != null)
            {
                steam.text = name != null ? "STEAM 연결됨" : "STEAM 오프라인";
                steam.EnableInClassList("s2-steam-off", name == null);
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                int index = i;
                var tab = new Button { focusable = false };
                tab.AddToClassList("s2-tab");
                var text = new Label(tabs[i].Name) { pickingMode = PickingMode.Ignore };
                text.AddToClassList("disp");
                text.AddToClassList("s2-tab-text");
                tab.Add(text);
                tab.generateVisualContent += DrawTabPlate;
                tab.clicked += () => { if (tabIndex != index) { tabIndex = index; pageIndex = 0; ShowTab(); } };
                ChunkyButtons.Springy(tab);
                tabsBox?.Add(tab);
            }

            Wire(root.Q<Button>("s2-back"), Back);
            Wire(root.Q<Button>("s2-reset"), ResetPage);
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

        // The chosen tab sits on a gold slab with clipped corners.
        static void DrawTabPlate(MeshGenerationContext context)
        {
            var e = context.visualElement;
            if (!e.ClassListContains("s2-tab-on")) return;
            float w = e.layout.width, h = e.layout.height, d = 4f, cut = 7f;
            var painter = context.painter2D;
            void Plate(float y0, float y1, Color color)
            {
                painter.fillColor = color;
                painter.BeginPath();
                painter.MoveTo(new Vector2(cut, y0));
                painter.LineTo(new Vector2(w - cut, y0));
                painter.LineTo(new Vector2(w, y0 + cut));
                painter.LineTo(new Vector2(w, y1 - cut));
                painter.LineTo(new Vector2(w - cut, y1));
                painter.LineTo(new Vector2(cut, y1));
                painter.LineTo(new Vector2(0, y1 - cut));
                painter.LineTo(new Vector2(0, y0 + cut));
                painter.ClosePath();
                painter.Fill();
            }
            Plate(d, h, PieceFigure.Hex(0x6E4300));
            Plate(0, h - d, PieceFigure.Hex(0xFFC93D));
            Plate(0, (h - d) * .45f, PieceFigure.Hex(0xFFE07A));
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
                {
                    tab.EnableInClassList("s2-tab-on", i++ == tabIndex);
                    tab.MarkDirtyRepaint();
                }
            }
            if (subsBox == null) return;
            subsBox.Clear();
            var pages = tabs[tabIndex].Pages;
            pageIndex = Mathf.Clamp(pageIndex, 0, pages.Length - 1);
            for (int i = 0; i < pages.Length; i++)
            {
                int index = i;
                var sub = new Button { text = pages[i].Name, focusable = false };
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
            if (resetText != null) resetText.text = page.Name + " 기본값 복원";
            Describe(page.Name, "줄 위에 마우스를 올리면 여기에 설명이 나와요.", false);
            foreach (var section in page.Sections)
            {
                var head = Add(content, "s2-section");
                Add(head, "disp s2-section-title", section.Title);
                if (!string.IsNullOrEmpty(section.Note)) Add(head, "s2-section-note", section.Note);
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

        void Describe(string title, string text, bool soon)
        {
            if (descTitle != null) descTitle.text = title;
            if (descText != null) descText.text = soon ? text + "\n\n[준비 중] " + Soon : text;
        }

        // One line: the name (with "준비 중" when nothing reads it yet), then the value.
        void Line(VisualElement parent, Item item)
        {
            var row = Add(parent, "s2-row");
            row.pickingMode = PickingMode.Position;
            var lab = Add(row, "s2-lab");
            if (item.Indent) lab.style.paddingLeft = 44;
            Add(lab, "s2-lab-text", item.Label);
            if (item.Soon) Add(lab, "s2-soon", "준비 중");
            var val = Add(row, "s2-val");
            row.RegisterCallback<PointerEnterEvent>(_ => Describe(item.Label, string.IsNullOrEmpty(item.Desc) ? "설명이 필요 없는 항목이에요." : item.Desc, item.Soon));

            switch (item)
            {
                case Choice c when c.Drop: DropLine(val, c); break;
                case Choice c: StepLine(val, c); break;
                case Slide s: SlideLine(val, s); break;
                case Keys k: KeysLine(val, k); break;
                case MicTest _: MicLine(val); break;
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
                text.text = options.Length > 0 ? options[Mathf.Clamp(c.Get(), 0, options.Length - 1)] : "";
                prev.SetEnabled(options.Length > 1);
                next.SetEnabled(options.Length > 1);
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
                text.text = options.Length > 0 ? options[Mathf.Clamp(c.Get(), 0, options.Length - 1)] : "";
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
                var option = new Button { text = options[i], focusable = false };
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

        void SlideLine(VisualElement val, Slide s)
        {
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
                if (Mathf.Abs(v - s.Get()) > 1e-4f) s.Set(v);
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
                    SetText(button, asking ? "키를 누르세요…" : GameSettings.KeyName(GameSettings.Key(k.Key, s)));
                    button.MarkDirtyRepaint();
                });
            }
        }

        void MicLine(VisualElement val)
        {
            var meter = Add(val, "s2-meter");
            var cells = new VisualElement[16];
            for (int i = 0; i < cells.Length; i++) cells[i] = Add(meter, "s2-meter-cell");
            var test = Arrow(val, "테스트");
            test.AddToClassList("s2-test");
            test.clicked += () => { if (micClip != null) StopMic(); else StartMic(); Refresh(); };
            refreshers.Add(() => SetText(test, micClip != null ? "테스트 끄기" : (Microphone.devices.Length == 0 ? "마이크 없음" : "테스트")));
            meter.schedule.Execute(() =>
            {
                int lit = micClip != null ? Mathf.RoundToInt(Mathf.Clamp01(micLevel) * cells.Length) : 0;
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
            micLevel = Mathf.Lerp(micLevel, Mathf.Clamp01(rms * 12f), .5f);
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
            if (current == this) current = null;
            if (ownedPanel != null) Destroy(ownedPanel);
        }
    }
}
