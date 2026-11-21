using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // Runtime UI Toolkit plumbing shared by every scene's HUD: one place that
    // builds a panel, one Korean-capable body font and the display font.
    public static class RuntimePanels
    {
        // Elements with this class draw in the display font (titles, buttons, big numbers).
        public const string DisplayClass = "disp";

        static Font display;
        static bool displayTried;
        static readonly Dictionary<Script, Font> bodies = new Dictionary<Script, Font>();

        // The built-in LegacyRuntime font carries no Hangul, so take a Korean
        // capable OS font first and keep the built-in one as the fallback. Cached:
        // every panel and IMGUI overlay shares the same Font object.
        // R747: the body font of the language shown (Loc.Lang) - KoreanFont is the older name every caller uses.
        // Korean and the Latin and Cyrillic languages keep 맑은 고딕 first (player names and chat stay Hangul);
        // Chinese and Japanese put their own Windows font first.
        public static Font KoreanFont => BodyFont;

        public static Font BodyFont
        {
            get
            {
                var script = Loc.Current.Script;
                Font font;
                if (bodies.TryGetValue(script, out font) && font != null) return font;
                return bodies[script] = ResolveFont(script);
            }
        }

        // Black Han Sans (SIL OFL 1.1, Resources/Fonts with its licence), the menu
        // design's title face. It carries the 2,350 common Hangul syllables; null
        // if the file is missing, and then titles keep the body font.
        public static Font DisplayFont
        {
            get
            {
                if (display == null && !displayTried)
                {
                    displayTried = true;
                    display = Resources.Load<Font>("Fonts/BlackHanSans-Regular");
                    if (display == null) Debug.LogWarning("[ChessFight] 제목 글꼴(Resources/Fonts/BlackHanSans-Regular)을 찾지 못했습니다.");
                }
                return display;
            }
        }

        // Gives every element under `root` that has DisplayClass the display font.
        // Call again for elements added later (or use Display on each).
        // R747: the display font for the language shown, null when Black Han Sans has not its letters (it has Hangul
        // and plain Latin only): those titles keep the body font until each script has a display face of its own.
        public static Font DisplayFace
        {
            get
            {
                var script = Loc.Current.Script;
                return script == Script.Hangul || script == Script.Latin ? DisplayFont : null;
            }
        }

        public static void ApplyDisplay(VisualElement root)
        {
            if (root == null || DisplayFace == null) return;
            root.Query<VisualElement>(className: DisplayClass).ForEach(Display);
        }

        // Exo 2 Black Italic (SIL OFL 1.1, Resources/Fonts with its licence), the
        // title screen's CHESS FIGHT since R150. Latin only; null if missing.
        static Font title;
        static bool titleTried;

        public static Font TitleFont
        {
            get
            {
                if (title == null && !titleTried)
                {
                    titleTried = true;
                    title = Resources.Load<Font>("Fonts/Exo2-BlackItalic");
                    if (title == null) Debug.LogWarning("[ChessFight] 시작 화면 제목 글꼴(Resources/Fonts/Exo2-BlackItalic)을 찾지 못했습니다.");
                }
                return title;
            }
        }

        public static void Display(VisualElement element)
        {
            if (element == null || DisplayFace == null) return;
            element.AddToClassList(DisplayClass);
            element.style.unityFont = DisplayFace;
            element.style.unityFontStyleAndWeight = FontStyle.Normal;
        }

        // R625: editor captures only (PawnRushEditor/VersusSceneCapture): while set, a panel made here draws into this texture
        // instead of the screen (batch mode has no screen). Set before the panel is made: a panel that already draws to
        // the screen does not move to a texture set afterwards.
        public static RenderTexture CaptureTarget;

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
                if (CaptureTarget != null)
                {
                    settings.targetTexture = CaptureTarget;
                    settings.clearColor = true;
                    settings.colorClearValue = Color.black;
                }
                // A PanelSettings without a theme renders nothing at all, so say so
                // loudly rather than leaving an invisible HUD to explain.
                if (theme != null) settings.themeStyleSheet = theme;
                else Debug.LogError("[ChessFight] Resources/NetworkTheme (.tss)를 불러오지 못했습니다. " +
                                    "테마가 없으면 HUD가 아예 보이지 않습니다.");
            }
            document.panelSettings = settings;
            // Settings → 접근성 → 메뉴 크기 (R77): every menu this makes, not the
            // settings window itself, follows it, also when it changes.
            if (owned != null && host.GetComponent<SettingsWindow>() == null) Scale(owned);
            var root = document.rootVisualElement;
            // Unity's default runtime theme is what normally stretches a UIDocument
            // root over the whole screen. NetworkTheme.tss does not import it, so
            // without this the root is only as tall as its content: zero for a
            // layout of absolutely positioned cards, which pushes every card
            // anchored to the bottom edge off the top of the screen.
            root.StretchToParentSize();
            root.style.unityFont = KoreanFont;
            layout.CloneTree(root);
            Translate(root);
            ApplyDisplay(root);
            Follow(root);
            return root;
        }

        // R747: a layout's own words (the UXML's text) through the string table, once as it is made; a line the
        // table has not translated stays Korean. Text the code sets later goes through Loc.T at the call site.
        public static void Translate(VisualElement root)
        {
            if (root == null || Loc.Lang == Loc.Source) return;
            root.Query<TextElement>().ForEach(t => { if (!string.IsNullOrEmpty(t.text)) t.text = Loc.T(t.text); });
        }

        // R747: panels made here take the new body and display fonts when the language changes (words already shown
        // change when their view draws them again; the settings window builds itself again).
        static readonly List<VisualElement> roots = new List<VisualElement>();
        static bool following;

        static void Follow(VisualElement root)
        {
            if (!following) { following = true; Loc.Changed += Refont; }
            roots.RemoveAll(r => r == null || r.panel == null);
            roots.Add(root);
        }

        static void Refont()
        {
            roots.RemoveAll(r => r == null || r.panel == null);
            var face = DisplayFace;
            foreach (var root in roots)
            {
                root.style.unityFont = BodyFont;
                root.Query<VisualElement>(className: DisplayClass).ForEach(e =>
                {
                    if (face != null) e.style.unityFont = face;
                    else e.style.unityFont = StyleKeyword.Null;
                });
            }
        }

        static readonly List<PanelSettings> scaled = new List<PanelSettings>();
        static bool rescaling;

        static void Scale(PanelSettings settings)
        {
            if (!rescaling) { rescaling = true; GameSettings.Changed += Rescale; }
            scaled.Add(settings);
            settings.scale = GameSettings.UiScale;
        }

        static void Rescale()
        {
            scaled.RemoveAll(s => s == null);
            foreach (var s in scaled) s.scale = GameSettings.UiScale;
        }

        static readonly string[] KoreanFaces = { "Malgun Gothic", "맑은 고딕", "NanumGothic", "Noto Sans KR", "Gulim", "Dotum" };

        // R747: the Windows fonts tried for each script, in order (the ones every Windows 10/11 has first). The
        // Korean faces follow the others' own so Hangul names can still find a glyph - whether Unity's UI Toolkit
        // falls back along this list for a single missing letter is still to be seen in Unity.
        static string[] Candidates(Script script)
        {
            string[] own;
            switch (script)
            {
                case Script.SimplifiedChinese: own = new[] { "Microsoft YaHei", "微软雅黑", "DengXian", "SimHei" }; break;
                case Script.TraditionalChinese: own = new[] { "Microsoft JhengHei", "微軟正黑體", "PMingLiU" }; break;
                case Script.Japanese: own = new[] { "Yu Gothic UI", "Yu Gothic", "Meiryo", "MS Gothic" }; break;
                default: own = new string[0]; break;
            }
            var list = new List<string>(own);
            list.AddRange(KoreanFaces);
            // 맑은 고딕's Latin and Cyrillic cover the European languages; Segoe UI is there for a letter it lacks.
            if (script == Script.Latin || script == Script.LatinExtended || script == Script.Cyrillic) list.Add("Segoe UI");
            // R749: names may be Chinese now. 맑은 고딕 has some traditional Hanja only, so the simplified ones come
            // from Microsoft YaHei (every Windows 10/11 has it) in every language's list.
            if (script != Script.SimplifiedChinese) list.AddRange(new[] { "Microsoft YaHei", "微软雅黑" });
            list.Add("Arial Unicode MS");
            return list.ToArray();
        }

        static Font ResolveFont(Script script)
        {
            var candidates = Candidates(script);
            try
            {
                var os = Font.CreateDynamicFontFromOSFont(candidates, 16);
                if (os != null) { Debug.Log("[ChessFight] HUD 폰트(" + script + "): " + os.name); return os; }
            }
            catch (Exception e) { Debug.LogWarning("[ChessFight] OS 폰트를 불러오지 못했습니다: " + e.Message); }
            Debug.LogWarning("[ChessFight] 한글 폰트를 찾지 못해 기본 폰트를 씁니다. 한글이 깨질 수 있습니다.");
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
