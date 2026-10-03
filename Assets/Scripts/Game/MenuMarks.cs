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
