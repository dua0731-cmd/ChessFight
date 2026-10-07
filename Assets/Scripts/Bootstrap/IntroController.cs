using System;
using ChessFight.Network;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // Title screen. NetworkRuntime starts Steam while this is on screen, and a
    // click on the start plate moves on (R64: no longer any key, so Esc can open
    // the settings window). The chess set behind the title is IntroStage, built
    // here at run time so the scene file stays untouched.
    //
    // R84: the opening logo plays over it once per run of the game; the first
    // time (no name saved yet, or Shift held on the plate) the plate leads to the
    // name screen, and from there, or straight from the plate, the scene
    // transition (the board closing through a king, opening through a knight)
    // carries the player to the lobby.
    [DisallowMultipleComponent]
    public sealed class IntroController : MonoBehaviour
    {
        const float MinimumShow = 1f;
        // The start plate's release plays before the transition starts.
        const float LeaveDelay = .3f;
        // The lobby's own player stands here (feet on the board); the knight-shaped
        // hole opens a little above the feet.
        const float KnightHoleHeight = .95f;

        static readonly Color Ready = new Color(.36f, .89f, .54f);
        static readonly Color Waiting = new Color(1f, .78f, .31f);
        static readonly Color Failed = new Color(1f, .47f, .39f);

        PanelSettings ownedPanel;
        Texture2D shade;
        Label status;
        VisualElement root, press, chip, dot;
        NameScreen nameScreen;
        float shownAt, leaveAt = -1;
        bool armed, renaming, logoDone, left;

        void Awake()
        {
            new GameObject("Intro Stage").AddComponent<IntroStage>().Build(Camera.main);

            root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("IntroHud"),
                                        Resources.Load<ThemeStyleSheet>("NetworkTheme"), null,
                                        new Vector2Int(1280, 720), out ownedPanel);
            status = root?.Q<Label>("intro-status");
            press = root?.Q<VisualElement>("intro-press");
            chip = root?.Q<VisualElement>("intro-chip");
            dot = root?.Q<VisualElement>("intro-dot");
            var shadeElement = root?.Q<VisualElement>("intro-shade");
            if (shadeElement != null)
            {
                shade = Shade();
                shadeElement.style.backgroundImage = new StyleBackground(shade);
            }
            // Lacquered walnut under the lines below the title and the Steam chip.
            var walnut = new StyleBackground(MenuArt.PanelTexture());
            var subs = root?.Q<VisualElement>("intro-subs");
            if (subs != null) subs.style.backgroundImage = walnut;
            if (chip != null) chip.style.backgroundImage = walnut;
            if (press != null) ChunkyButtons.Make(press);
            shownAt = Time.unscaledTime;
            // The opening logo, once per run; the title counts its minimum time
            // from when the logo is gone.
            LogoIntro.Play(() => { logoDone = true; shownAt = Time.unscaledTime; });
        }

        void Update()
        {
            var session = NetworkRuntime.Instance != null ? NetworkRuntime.Instance.Session : null;
            string text = Describe(session);
            if (status != null) status.text = text;
            if (chip != null) chip.style.display = text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (dot != null && session != null)
                dot.style.backgroundColor = session.Online ? Ready : string.IsNullOrEmpty(session.Error) ? Waiting : Failed;

            bool ready = logoDone && Time.unscaledTime - shownAt >= MinimumShow;
            if (press != null) press.style.opacity = ready ? 1f : .55f;
            // Nothing here takes input under the logo or the transition, or once the
            // player has moved on.
            if (!logoDone || SceneTransition.Busy || left) { armed = false; return; }
            if (LegacyKeys.Down(KeyCode.Escape)) SettingsWindow.Toggle();
            if (nameScreen != null) return;

            if (leaveAt >= 0)
            {
                if (Time.unscaledTime - leaveAt >= LeaveDelay) Leave();
                return;
            }
            // A press that started under the logo must not start the game when it is let go.
            if (MouseDown()) armed = true;
            // A failed Steam start still continues: the lobby has the retry button.
            if (ready && armed && !SettingsWindow.IsOpen && Released(press))
            {
                leaveAt = Time.unscaledTime;
                renaming = ShiftHeld();
            }
        }

        // From the start plate: the name screen the first time, else the lobby.
        void Leave()
        {
            leaveAt = -1;
            var from = Fraction(press);
            if (PlayerProfile.HasName && !renaming) { GoToLobby(from); return; }
            SceneTransition.Run("이름 설정", from, ShowNameScreen, () => nameScreen != null && nameScreen.Ready,
                                () => nameScreen != null ? nameScreen.PawnAt : new Vector2(.5f, .5f));
        }

        void ShowNameScreen()
        {
            if (nameScreen != null) return;
            nameScreen = new GameObject("Name Screen").AddComponent<NameScreen>();
            nameScreen.Build(Prefill());
            nameScreen.Confirmed += name =>
            {
                PlayerProfile.Set(name);
                GoToLobby(nameScreen.PawnAt);
            };
        }

        // The saved name when there is one (renaming), else the Steam name if it is
        // a name the screen would take.
        static string Prefill()
        {
            if (PlayerProfile.HasName) return PlayerProfile.Name;
            string steam = "";
            try
            {
                var session = NetworkRuntime.Instance != null ? NetworkRuntime.Instance.Session : null;
                if (session != null && session.Online) steam = PlayerNames.Tidy(SteamFriends.GetPersonaName());
            }
            catch (InvalidOperationException) { steam = ""; }
            return PlayerNames.Check(steam, false).Ok ? steam : "";
        }

        // The lobby loads behind the board; the board opens on the player's own
        // piece once the lobby has built its stage.
        void GoToLobby(Vector2 from)
        {
            if (left) return;
            left = true;
            AsyncOperation load = null;
            int loadedFrame = -1;
            SceneTransition.Run("로비", from,
                () => load = SceneManager.LoadSceneAsync(SceneNames.Lobby),
                () =>
                {
                    if (load == null || !load.isDone || UnityEngine.Object.FindAnyObjectByType<LobbyStage>() == null) return false;
                    if (loadedFrame < 0) loadedFrame = Time.frameCount;
                    // A couple of frames for the lobby's HUD to lay out and its stage to draw.
                    return Time.frameCount > loadedFrame + 2;
                },
                LobbyPlayerAt);
        }

        static Vector2 LobbyPlayerAt()
        {
            var stage = UnityEngine.Object.FindAnyObjectByType<LobbyStage>();
            var view = stage != null ? stage.View : null;
            if (view == null || Screen.width <= 0 || Screen.height <= 0) return new Vector2(.5f, .62f);
            var at = view.WorldToScreenPoint(LobbyStage.SpotPosition(0) + Vector3.up * KnightHoleHeight);
            if (at.z <= 0) return new Vector2(.5f, .62f);
            return new Vector2(at.x / Screen.width, 1f - at.y / Screen.height);
        }

        // An element's centre as a fraction of the screen (0..1 from the top left).
        Vector2 Fraction(VisualElement element)
        {
            if (element?.panel == null || root == null || root.worldBound.width <= 0) return new Vector2(.5f, .5f);
            var c = element.worldBound.center;
            var r = root.worldBound;
            return new Vector2((c.x - r.x) / r.width, (c.y - r.y) / r.height);
        }

        // The mouse button let go over the plate this frame. Read from the mouse
        // rather than a UI event so the title still works where pointer events do
        // not arrive (NetworkHudView's fallback click is for the same case); the
        // plate's own press and spring still come from ChunkyButtons.
        static bool Released(VisualElement plate)
        {
            if (plate?.panel == null) return false;
            bool up;
            Vector2 screen;
            try { up = Input.GetMouseButtonUp(0); screen = Input.mousePosition; }
            catch (InvalidOperationException) { return false; }
            if (!up) return false;
            var point = RuntimePanelUtils.ScreenToPanel(plate.panel, new Vector2(screen.x, Screen.height - screen.y));
            return plate.worldBound.Contains(point);
        }

        static bool MouseDown()
        {
            try { return Input.GetMouseButtonDown(0); }
            catch (InvalidOperationException) { return false; }
        }

        static bool ShiftHeld()
        {
            try { return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift); }
            catch (InvalidOperationException) { return false; }
        }

        static string Describe(SteamSession session)
        {
            if (session == null) return "";
            if (session.Online)
            {
                string name = session.Name(session.Self);
                return string.IsNullOrEmpty(name) ? "STEAM 연결됨" : "STEAM 연결됨 · " + name;
            }
            return string.IsNullOrEmpty(session.Error) ? "Steam 연결 중..." : session.Error;
        }

        // Walnut fading out left to right: strong behind the title, gone by the board.
        static Texture2D Shade()
        {
            const int width = 256;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Intro Shade"
            };
            var walnut = new Color(44 / 255f, 26 / 255f, 14 / 255f);
            for (int x = 0; x < width; x++)
            {
                float u = x / (width - 1f);
                walnut.a = StageKit.Darkening(u < .48f ? Mathf.Lerp(.66f, .18f, u / .48f) : Mathf.Lerp(.18f, 0f, (u - .48f) / .52f));
                texture.SetPixel(x, 0, walnut);
            }
            texture.Apply();
            return texture;
        }

        void OnDestroy()
        {
            if (ownedPanel != null) Destroy(ownedPanel);
            if (shade != null) Destroy(shade);
        }
    }
}
