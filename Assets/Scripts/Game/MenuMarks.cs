using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // Small vector marks the menu screens share, drawn with Painter2D since the
    // project has no image assets for them. Put one inside an element sized in USS.
    public static class MenuMarks
    {
        static readonly Vector2[] Crown =
        {
            new Vector2(-11, 8), new Vector2(-12, -5), new Vector2(-5, 0), new Vector2(0, -10),
            new Vector2(5, 0), new Vector2(12, -5), new Vector2(11, 8)
        };

        // The crown outline of the design (26 x 20 units), stroked or filled.
        public sealed class CrownMark : VisualElement
        {
            Color fill = Color.clear, stroke = PieceFigure.Hex(0xE7BE72);
            float width = 1.6f;

            public CrownMark(Color? fillColor = null, Color? strokeColor = null, float lineWidth = 1.6f)
            {
                pickingMode = PickingMode.Ignore;
                style.flexGrow = 1;
                if (fillColor.HasValue) fill = fillColor.Value;
                if (strokeColor.HasValue) stroke = strokeColor.Value;
                width = lineWidth;
                generateVisualContent += Draw;
            }

            public void Set(Color fillColor, Color strokeColor)
            {
                if (fill == fillColor && stroke == strokeColor) return;
                fill = fillColor;
                stroke = strokeColor;
                MarkDirtyRepaint();
            }

            void Draw(MeshGenerationContext context)
            {
                var rect = contentRect;
                if (rect.width <= 0 || rect.height <= 0) return;
                float scale = Mathf.Min((rect.width - width * 2) / 24f, (rect.height - width * 2) / 18f);
                var center = rect.center + new Vector2(0, scale);
                var painter = context.painter2D;
                painter.lineJoin = LineJoin.Round;
                painter.lineWidth = width;
                painter.BeginPath();
                for (int i = 0; i < Crown.Length; i++)
                {
                    var point = center + Crown[i] * scale;
                    if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
                }
                painter.ClosePath();
                if (fill.a > 0) { painter.fillColor = fill; painter.Fill(); }
                if (stroke.a > 0 && width > 0) { painter.strokeColor = stroke; painter.Stroke(); }
            }
        }

        // Three chevrons pointing right that light up one after another.
        public sealed class Chevrons : VisualElement
        {
            Color color;

            public Chevrons(Color tint)
            {
                pickingMode = PickingMode.Ignore;
                style.flexGrow = 1;
                color = tint;
                generateVisualContent += Draw;
                schedule.Execute(MarkDirtyRepaint).Every(33);
            }

            void Draw(MeshGenerationContext context)
            {
                var rect = contentRect;
                if (rect.width <= 0 || rect.height <= 0) return;
                float w = rect.width / 3.2f, h = rect.height, t = Time.unscaledTime;
                var painter = context.painter2D;
                for (int i = 0; i < 3; i++)
                {
                    // The samples' 1.2 s cycle, each chevron .15 s after the last.
                    float phase = (t - i * .15f) / 1.2f * Mathf.PI * 2f;
                    var c = color;
                    c.a = .25f + .75f * (.5f - .5f * Mathf.Cos(phase));
                    float x = rect.x + i * w * 1.05f;
                    painter.fillColor = c;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x, rect.y));
                    painter.LineTo(new Vector2(x + w * .45f, rect.y));
                    painter.LineTo(new Vector2(x + w, rect.y + h / 2));
                    painter.LineTo(new Vector2(x + w * .45f, rect.y + h));
                    painter.LineTo(new Vector2(x, rect.y + h));
                    painter.LineTo(new Vector2(x + w * .55f, rect.y + h / 2));
                    painter.ClosePath();
                    painter.Fill();
                }
            }
        }

        public enum Icon { People, Info, Hash, Lock, Copy, Exit, Arrow, Check }

        // The lobby's line icons (design A, R83), drawn in a 24-unit box with round
        // ends, in the element's own text colour unless a tint is given. Only
        // MoveTo, LineTo and clockwise Arc, the calls the Linux compile check's
        // Painter2D stand-in knows; the curves are short polylines.
        public sealed class IconMark : VisualElement
        {
            readonly Icon icon;
            readonly float width;
            readonly Color? tint;

            public IconMark(Icon kind, float lineWidth = 2f, Color? color = null)
            {
                icon = kind;
                width = lineWidth;
                tint = color;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext context)
            {
                var rect = contentRect;
                if (rect.width <= 0 || rect.height <= 0) return;
                float s = Mathf.Min(rect.width, rect.height) / 24f;
                var origin = rect.center - new Vector2(12f, 12f) * s;
                Vector2 P(float x, float y) => origin + new Vector2(x, y) * s;
                var p = context.painter2D;
                p.strokeColor = tint ?? resolvedStyle.color;
                p.lineWidth = width * s;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;
                p.BeginPath();
                void Line(float x0, float y0, float x1, float y1) { p.MoveTo(P(x0, y0)); p.LineTo(P(x1, y1)); }
                void Arc(float cx, float cy, float r, float from, float to) =>
                    p.Arc(P(cx, cy), r * s, new Angle(from, AngleUnit.Degree), new Angle(to, AngleUnit.Degree));
                void Circle(float cx, float cy, float r) { p.MoveTo(P(cx + r, cy)); Arc(cx, cy, r, 0f, 360f); }
                void Curve(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
                {
                    for (int i = 1; i <= 8; i++)
                    {
                        float t = i / 8f, u = 1f - t;
                        var q = u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
                        p.LineTo(P(q.x, q.y));
                    }
                }
                void Box(float x, float y, float w, float h, float r)
                {
                    p.MoveTo(P(x + r, y));
                    p.LineTo(P(x + w - r, y)); Arc(x + w - r, y + r, r, 270f, 360f);
                    p.LineTo(P(x + w, y + h - r)); Arc(x + w - r, y + h - r, r, 0f, 90f);
                    p.LineTo(P(x + r, y + h)); Arc(x + r, y + h - r, r, 90f, 180f);
                    p.LineTo(P(x, y + r)); Arc(x + r, y + r, r, 180f, 270f);
                }
                switch (icon)
                {
                    case Icon.People:
                        Circle(9f, 8f, 3.5f);
                        p.MoveTo(P(2.5f, 19f));
                        Curve(new Vector2(2.5f, 19f), new Vector2(3.4f, 16f), new Vector2(5.9f, 14.4f), new Vector2(9f, 14.4f));
                        Curve(new Vector2(9f, 14.4f), new Vector2(12.1f, 14.4f), new Vector2(14.6f, 16f), new Vector2(15.5f, 19f));
                        p.MoveTo(P(16f, 4.8f));
                        Arc(15.19f, 8f, 3.3f, -75.9f, 75.9f);
                        p.MoveTo(P(18.4f, 14.6f));
                        Curve(new Vector2(18.4f, 14.6f), new Vector2(20f, 15.2f), new Vector2(21.1f, 16.6f), new Vector2(21.5f, 19f));
                        break;
                    case Icon.Info:
                        Circle(12f, 12f, 9f);
                        Line(12f, 11f, 12f, 17f);
                        Line(12f, 7.5f, 12f, 8f);
                        break;
                    case Icon.Hash:
                        Line(9f, 3f, 7f, 21f); Line(17f, 3f, 15f, 21f);
                        Line(4f, 8.5f, 21f, 8.5f); Line(3f, 15.5f, 20f, 15.5f);
                        break;
                    case Icon.Lock:
                        Box(5f, 10.5f, 14f, 10f, 1.5f);
                        p.MoveTo(P(8f, 10.5f)); p.LineTo(P(8f, 8f));
                        Arc(12f, 8f, 4f, 180f, 360f);
                        p.LineTo(P(16f, 10.5f));
                        break;
                    case Icon.Copy:
                        Box(8f, 8f, 12f, 12f, 1.5f);
                        p.MoveTo(P(8f, 16f)); p.LineTo(P(5.5f, 16f)); Arc(5.5f, 14.5f, 1.5f, 90f, 180f);
                        p.LineTo(P(4f, 5.5f)); Arc(5.5f, 5.5f, 1.5f, 180f, 270f);
                        p.LineTo(P(14.5f, 4f)); Arc(14.5f, 5.5f, 1.5f, 270f, 360f);
                        p.LineTo(P(16f, 8f));
                        break;
                    case Icon.Exit:
                        p.MoveTo(P(14f, 4f)); p.LineTo(P(18.5f, 4f)); Arc(18.5f, 5.5f, 1.5f, 270f, 360f);
                        p.LineTo(P(20f, 18.5f)); Arc(18.5f, 18.5f, 1.5f, 0f, 90f);
                        p.LineTo(P(14f, 20f));
                        p.MoveTo(P(10f, 8f)); p.LineTo(P(6f, 12f)); p.LineTo(P(10f, 16f));
                        Line(6f, 12f, 16f, 12f);
                        break;
                    case Icon.Arrow:
                        Line(5f, 12f, 19f, 12f);
                        p.MoveTo(P(13f, 6f)); p.LineTo(P(19f, 12f)); p.LineTo(P(13f, 18f));
                        break;
                    case Icon.Check:
                        p.MoveTo(P(4f, 12.5f)); p.LineTo(P(9f, 17.5f)); p.LineTo(P(20f, 6.5f));
                        break;
                }
                p.Stroke();
            }
        }

        // Dark at the edges, clear in the middle (the samples' vignette).
        public static Texture2D Vignette(Color edge, float clear, float strength)
        {
            const int w = 128, h = 72;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Menu vignette"
            };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + .5f) / w - .5f, dy = (y + .5f) / h - .55f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / .5f / .9f;
                    float a = Mathf.Clamp01((r - clear) / (1f - clear));
                    edge.a = StageKit.Darkening(a * a * strength);
                    texture.SetPixel(x, y, edge);
                }
            texture.Apply();
            return texture;
        }
    }
}
