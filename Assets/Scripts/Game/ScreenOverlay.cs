using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // The settings that act on the whole screen rather than one scene (R77):
    // brightness (a dark veil below 1, a faint light one above), the frame counter
    // top right, and muting the game while its window is in the background. One
    // object for the whole run, above every panel, never taking a click.
    [DisallowMultipleComponent]
    public sealed class ScreenOverlay : MonoBehaviour
    {
        const float SortingOrder = 1000f;

        static ScreenOverlay instance;
        PanelSettings panel;
        VisualElement veil;
        Label fps;
        float frames, elapsed;
        bool focused = true;

        public static void Ensure()
        {
            if (instance != null) return;
            var host = new GameObject("Screen Overlay");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<ScreenOverlay>();
        }

        void Awake()
        {
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            panel.sortingOrder = SortingOrder;
            var theme = Resources.Load<ThemeStyleSheet>("NetworkTheme");
            if (theme != null) panel.themeStyleSheet = theme;
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            var root = document.rootVisualElement;
            root.StretchToParentSize();
            root.pickingMode = PickingMode.Ignore;

            veil = new VisualElement { pickingMode = PickingMode.Ignore };
            veil.StretchToParentSize();
            root.Add(veil);
            fps = new Label { pickingMode = PickingMode.Ignore };
            fps.AddToClassList("overlay-fps");
            fps.style.unityFont = RuntimePanels.KoreanFont;
            root.Add(fps);

            GameSettings.Changed += Apply;
            Apply();
        }

        void Apply()
        {
            if (veil == null) return;
            float b = GameSettings.Brightness;
            veil.style.backgroundColor = b < 1f ? new Color(0f, 0f, 0f, Mathf.Clamp01((1f - b) * .9f))
                                                : new Color(1f, .96f, .88f, Mathf.Clamp01((b - 1f) * .18f));
            veil.style.display = Mathf.Abs(b - 1f) < .01f ? DisplayStyle.None : DisplayStyle.Flex;
            fps.style.display = GameSettings.ShowFps ? DisplayStyle.Flex : DisplayStyle.None;
            AudioListener.volume = focused || !GameSettings.MuteInBackground ? GameSettings.Master : 0f;
        }

        void Update()
        {
            if (!GameSettings.ShowFps) return;
            frames++;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < .5f) return;
            fps.text = Mathf.RoundToInt(frames / elapsed) + " FPS";
            frames = elapsed = 0;
        }

        void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            Apply();
        }

        void OnDestroy()
        {
            GameSettings.Changed -= Apply;
            if (instance == this) instance = null;
            if (panel != null) Destroy(panel);
        }
    }
}
