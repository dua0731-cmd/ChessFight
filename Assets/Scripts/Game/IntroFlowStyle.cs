using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // The opening logo, the name screen and the scene transition (R84) keep their
    // classes in Resources/IntroFlow.uss, added next to the shared theme, so the
    // lobby's stylesheet (NetworkHud.uss) is not touched by them.
    public static class IntroFlowStyle
    {
        static StyleSheet sheet;
        static bool tried;

        public static void Apply(VisualElement root)
        {
            if (root == null) return;
            if (!tried)
            {
                tried = true;
                sheet = Resources.Load<StyleSheet>("IntroFlow");
                if (sheet == null) Debug.LogError("[ChessFight] Resources/IntroFlow.uss를 불러오지 못했습니다.");
            }
            if (sheet != null) root.styleSheets.Add(sheet);
        }

        // A light colour's alpha from the design page, for drawing over a dark ground
        // in this project's linear colour space (PITFALLS 30): the page mixed in
        // sRGB, so the same alpha here would look far stronger (.38 → .12).
        public static float Light(float designAlpha) =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? Mathf.GammaToLinearSpace(Mathf.Clamp01(designAlpha)) : designAlpha;
    }
}
