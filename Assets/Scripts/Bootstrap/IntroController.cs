using ChessFight.Network;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // Title screen. NetworkRuntime starts Steam while this is on screen, and any
    // key moves on to the lobby. Deliberately needs no clickable UI, only a key.
    // The chess set behind the title is IntroStage, built here at run time so the
    // scene file stays untouched.
    [DisallowMultipleComponent]
    public sealed class IntroController : MonoBehaviour
    {
        const float MinimumShow = 1f;
        // The start plate's release plays before the lobby loads.
        const float LeaveDelay = .3f;

        static readonly Color Ready = new Color(.36f, .89f, .54f);
        static readonly Color Waiting = new Color(1f, .78f, .31f);
        static readonly Color Failed = new Color(1f, .47f, .39f);

        PanelSettings ownedPanel;
        Texture2D shade;
        Label status, hint;
        VisualElement press, chip, dot;
        float shownAt, leaveAt = -1;

        void Awake()
        {
            new GameObject("Intro Stage").AddComponent<IntroStage>().Build(Camera.main);

            var root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("IntroHud"),
                                            Resources.Load<ThemeStyleSheet>("NetworkTheme"), null,
                                            new Vector2Int(1280, 720), out ownedPanel);
            status = root?.Q<Label>("intro-status");
            hint = root?.Q<Label>("intro-hint");
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
        }

        void Update()
        {
            var session = NetworkRuntime.Instance != null ? NetworkRuntime.Instance.Session : null;
            bool ready = Time.unscaledTime - shownAt >= MinimumShow;

            string text = Describe(session);
            if (status != null) status.text = text;
            if (chip != null) chip.style.display = text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (dot != null && session != null)
                dot.style.backgroundColor = session.Online ? Ready : string.IsNullOrEmpty(session.Error) ? Waiting : Failed;

            if (hint != null) hint.text = "아무 키나 눌러 시작";
            if (press != null) press.style.opacity = ready ? 1f : .55f;
            if (leaveAt >= 0)
            {
                if (Time.unscaledTime - leaveAt >= LeaveDelay) SceneManager.LoadScene(SceneNames.Lobby);
                return;
            }
            // A failed Steam start still continues: the lobby has the retry button.
            if (ready && LegacyKeys.AnyDown())
            {
                leaveAt = Time.unscaledTime;
                ChunkyButtons.Pulse(press);
            }
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
