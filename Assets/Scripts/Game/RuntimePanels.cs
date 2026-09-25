using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // Runtime UI Toolkit plumbing shared by every scene's HUD: one place that
    // builds a panel and one Korean-capable font.
    public static class RuntimePanels
    {
        static Font korean;

        // The built-in LegacyRuntime font carries no Hangul, so take a Korean
        // capable OS font first and keep the built-in one as the fallback. Cached:
        // every panel and IMGUI overlay shares the same Font object.
        public static Font KoreanFont => korean != null ? korean : (korean = ResolveFont());

        public static VisualElement Create(GameObject host, VisualTreeAsset layout, ThemeStyleSheet theme,
                                           PanelSettings settings, Vector2Int referenceResolution, out PanelSettings owned)
        {
            owned = null;
            if (layout == null) { Debug.LogError("[ChessFight] HUD 레이아웃을 찾지 못했습니다."); return null; }
            var document = host.AddComponent<UIDocument>();
            if (settings == null)
            {
                settings = owned = ScriptableObject.CreateInstance<PanelSettings>();
                settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                settings.referenceResolution = referenceResolution;
                // A PanelSettings without a theme renders nothing at all, so say so
                // loudly rather than leaving an invisible HUD to explain.
                if (theme != null) settings.themeStyleSheet = theme;
                else Debug.LogError("[ChessFight] Resources/NetworkTheme (.tss)를 불러오지 못했습니다. " +
                                    "테마가 없으면 HUD가 아예 보이지 않습니다.");
            }
            document.panelSettings = settings;
            var root = document.rootVisualElement;
            // Unity's default runtime theme is what normally stretches a UIDocument
            // root over the whole screen. NetworkTheme.tss does not import it, so
            // without this the root is only as tall as its content: zero for a
            // layout of absolutely positioned cards, which pushes every card
            // anchored to the bottom edge off the top of the screen.
            root.StretchToParentSize();
            root.style.unityFont = KoreanFont;
            layout.CloneTree(root);
            return root;
        }

        static Font ResolveFont()
        {
            string[] candidates = { "Malgun Gothic", "맑은 고딕", "NanumGothic", "Noto Sans KR",
                                    "Gulim", "Dotum", "Arial Unicode MS" };
            try
            {
                var os = Font.CreateDynamicFontFromOSFont(candidates, 16);
                if (os != null) { Debug.Log("[ChessFight] HUD 폰트: " + os.name); return os; }
            }
            catch (Exception e) { Debug.LogWarning("[ChessFight] OS 폰트를 불러오지 못했습니다: " + e.Message); }
            Debug.LogWarning("[ChessFight] 한글 폰트를 찾지 못해 기본 폰트를 씁니다. 한글이 깨질 수 있습니다.");
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
