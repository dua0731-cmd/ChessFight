using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // R624 (11-13, 사용자 "vs 문구에 코팅처럼 되어있으면 좋겠어 이건 폰러쉬 vs문구도 같이"): a lacquer coat over a label's
    // text - Sword Fight's VS (LoadingScreenView.SwordPick.cs) and Pawn Rush's V and S (LoadingScreenView.Versus.cs).
    // The upper part of the letters is lighter, brightest at the top and fading to nothing by their middle (the gloss),
    // and now and then a soft light glints across them from left to right.
    //
    // UI Toolkit cannot fill text with a gradient or mask by glyphs, so the coat is copies of the label's own text, laid
    // as the label's children (drawn over its text, in the same place, font and size) and each seen only through a
    // clipping box. So the coat keeps to the glyphs exactly and moves, scales and fades with the label.
    // R624 11-13 (사용자 "vs 문구에 층 처럼 보여 자연스럽게 이어지게"): the gloss was three overlapping steps and the glint
    // two bands, which showed as stripes. Now the gloss is thin horizontal slices (Slice px each), every one a little
    // fainter than the one above, and the glint is Bands thin vertical slices under a bell, so both read as smooth.
    // Text set on the label later is not copied.
    public sealed class TextCoat
    {
        // the gloss: slices of Slice px from the top of the box down to GlossReach of its height (the letters are centred,
        // so .5 is their middle), each as strong as (1 - its depth)^GlossFall of the gloss colour's alpha
        const float Slice = 2f, GlossReach = .56f, GlossFall = 1.7f;
        // the glint: Bands slices of BandWide px, the middle one at the glint colour's alpha, the rest on a bell
        const int Bands = 13;
        const float BandWide = 7f;

        readonly VisualElement[] bands = new VisualElement[Bands];
        readonly Label[] bandTexts = new Label[Bands];
        readonly float from, to;
        // a glint every Period seconds, crossing in Sweep seconds
        public float Period = 2.6f, Sweep = .55f;

        TextCoat(float from, float to)
        {
            this.from = from;
            this.to = to;
        }

        /// <summary>Coats the label (width x height, its text centred). The glint crosses from x `from` to `to` (the
        /// letters' span in the label's own pixels).</summary>
        public static TextCoat Apply(Label label, float width, float height, Color gloss, Color glint, float from, float to)
        {
            float reach = height * GlossReach;
            int slices = Mathf.Max(1, Mathf.CeilToInt(reach / Slice));
            for (int k = 0; k < slices; k++)
            {
                float y = k * Slice, depth = (y + Slice / 2f) / reach;
                var c = gloss;
                c.a *= Mathf.Pow(Mathf.Clamp01(1f - depth), GlossFall);
                if (c.a <= .004f) continue;
                var clip = Clip(label, 0f, y, width, Slice);
                var text = Copy(label, clip, width, height, c);
                text.style.top = -y;
            }
            var coat = new TextCoat(from, to);
            float mid = (Bands - 1) / 2f;
            for (int k = 0; k < Bands; k++)
            {
                float d = (k - mid) / (Bands / 4.2f);
                var c = glint;
                c.a *= Mathf.Exp(-d * d);
                coat.bands[k] = Clip(label, 0f, 0f, BandWide, height);
                coat.bandTexts[k] = Copy(label, coat.bands[k], width, height, c);
            }
            coat.Sync(-1f);
            return coat;
        }

        /// <summary>`t`: seconds since the label landed (below 0: no glint yet).</summary>
        public void Sync(float t)
        {
            float p = t < 0f ? -1f : (t % Period) / Sweep;
            bool on = p >= 0f && p <= 1f;
            var shown = on ? Visibility.Visible : Visibility.Hidden;
            for (int k = 0; k < Bands; k++) bands[k].style.visibility = shown;
            if (!on) return;
            float e = p * p * (3f - 2f * p), span = Bands * BandWide;
            float x = Mathf.Lerp(from - span, to, e);
            for (int k = 0; k < Bands; k++)
            {
                float at = x + k * BandWide;
                bands[k].style.left = at;
                bandTexts[k].style.left = -at;
            }
        }

        static VisualElement Clip(VisualElement parent, float x, float y, float w, float h)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = x; e.style.top = y; e.style.width = w; e.style.height = h;
            e.style.overflow = Overflow.Hidden;
            parent.Add(e);
            return e;
        }

        // the label's text again, in its place, font and size, in another colour
        static Label Copy(Label source, VisualElement parent, float width, float height, Color color)
        {
            var copy = new Label(source.text) { pickingMode = PickingMode.Ignore };
            foreach (var c in source.GetClasses()) copy.AddToClassList(c);
            var s = copy.style;
            var from = source.style;
            s.position = Position.Absolute;
            s.left = 0f; s.top = 0f; s.width = width; s.height = height;
            s.fontSize = from.fontSize;
            s.unityFont = from.unityFont;
            s.unityFontDefinition = from.unityFontDefinition;
            s.unityFontStyleAndWeight = from.unityFontStyleAndWeight;
            s.unityTextAlign = from.unityTextAlign;
            s.whiteSpace = from.whiteSpace;
            s.letterSpacing = from.letterSpacing;
            s.marginLeft = s.marginRight = s.marginTop = s.marginBottom = 0f;
            s.paddingLeft = from.paddingLeft; s.paddingRight = from.paddingRight;
            s.paddingTop = from.paddingTop; s.paddingBottom = from.paddingBottom;
            s.color = color;
            parent.Add(copy);
            return copy;
        }
    }
}
