using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // Design A of the menus (10-27, 승규 님 chose "연기 유리 + 가는 금선"): smoked glass with a hairline gold edge,
    // drawn with the vector painter behind the element's children. The glass is a warm smoke, lighter at the top;
    // the edge is gold, brighter along the top as if lit from above. A plate can be bright (picked: a fuller edge
    // and a faint gold glow round it), an empty seat (no glass, a faint dashed edge only) or a button (a gold edge
    // running dark-light-dark across; the glass lighter while the pointer is on it).
    // 10-27 (승규 님's recording): the glint that swept across a button on hover went (it drew as a hard white slash,
    // past the button), and every edge is thinner.
    public sealed class GlassPlate
    {
        public static readonly Color Gold = new Color(233 / 255f, 198 / 255f, 122 / 255f);

        public float Radius = 12f;
        bool bright, empty, hover, down, hidden;
        readonly bool button;
        readonly VisualElement owner;

        GlassPlate(VisualElement owner, bool button)
        {
            this.owner = owner;
            this.button = button;
            owner.generateVisualContent += Draw;
        }

        public static GlassPlate On(VisualElement element, float radius = 12f) => new GlassPlate(element, false) { Radius = radius };

        // A button's face: the glass and the gold edge, and the hover and press looks.
        public static GlassPlate Button(VisualElement element, float radius = 14f)
        {
            var plate = new GlassPlate(element, true) { Radius = radius };
            element.RegisterCallback<PointerEnterEvent>(_ => plate.Hover(true));
            element.RegisterCallback<PointerLeaveEvent>(_ => { plate.down = false; plate.Hover(false); });
            element.RegisterCallback<PointerDownEvent>(_ => { plate.down = true; element.MarkDirtyRepaint(); }, TrickleDown.TrickleDown);
            element.RegisterCallback<PointerUpEvent>(_ => { plate.down = false; element.MarkDirtyRepaint(); }, TrickleDown.TrickleDown);
            element.RegisterCallback<PointerCaptureOutEvent>(_ => { plate.down = false; element.MarkDirtyRepaint(); });
            return plate;
        }

        // Off: nothing is drawn (the settings window's tabs: only the chosen one has a plate).
        public void Show(bool on)
        {
            if (hidden == !on) return;
            hidden = !on;
            owner.MarkDirtyRepaint();
        }

        public void Set(bool bright, bool empty)
        {
            if (this.bright == bright && this.empty == empty) return;
            this.bright = bright;
            this.empty = empty;
            owner.MarkDirtyRepaint();
        }

        void Hover(bool on)
        {
            hover = on;
            owner.MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            float w = owner.layout.width, h = owner.layout.height;
            if (hidden || float.IsNaN(w) || float.IsNaN(h) || w <= 2f || h <= 2f) return;
            var p = context.painter2D;
            float r = Mathf.Min(Radius, h / 2f, w / 2f);
            var top = new Vector2(0, 0);
            var bottom = new Vector2(0, h);
            if (!empty)
            {
                // a faint gold glow round the picked plate
                if (bright)
                {
                    Path(p, -1.5f, w, h, r + 1.5f);
                    p.lineWidth = 1.5f;
                    p.strokeFillGradient = Flat(new Color(Gold.r, Gold.g, Gold.b, .12f));
                    p.Stroke();
                }
                // the glass
                Path(p, 0f, w, h, r);
                float lift = button ? (down ? .1f : hover ? .06f : 0f) : bright ? .05f : 0f;
                p.fillGradient = Linear(new Color(.24f + lift, .18f + lift, .11f + lift * .6f, button ? .86f : .62f),
                                        new Color(.08f, .06f, .04f, button ? .92f : .74f), top, bottom);
                p.Fill();
                // a thin light along the top inside, the glass catching the light
                p.BeginPath();
                p.MoveTo(new Vector2(r, 1.5f));
                p.LineTo(new Vector2(w - r, 1.5f));
                p.lineWidth = .6f;
                p.strokeFillGradient = Flat(new Color(1f, .95f, .85f, .14f));
                p.Stroke();
            }
            // the gold edge
            if (empty)
            {
                // an empty seat: faint ivory dashes along the sides, the corners whole, no gold (10-27, 승규 님: the
                // empty seats were "painted gold"); drawn by hand, as the Linux compile check's Painter2D stand-in
                // has no dash pattern
                p.strokeFillGradient = Flat(new Color(1f, .95f, .86f, .16f));
                p.lineWidth = .7f;
                p.BeginPath();
                Dashes(p, new Vector2(r, .5f), new Vector2(w - r, .5f));
                Dashes(p, new Vector2(w - .5f, r), new Vector2(w - .5f, h - r));
                Dashes(p, new Vector2(w - r, h - .5f), new Vector2(r, h - .5f));
                Dashes(p, new Vector2(.5f, h - r), new Vector2(.5f, r));
                p.MoveTo(new Vector2(w - r, .5f)); p.ArcTo(new Vector2(w - .5f, .5f), new Vector2(w - .5f, r), r - .5f);
                p.MoveTo(new Vector2(w - .5f, h - r)); p.ArcTo(new Vector2(w - .5f, h - .5f), new Vector2(w - r, h - .5f), r - .5f);
                p.MoveTo(new Vector2(r, h - .5f)); p.ArcTo(new Vector2(.5f, h - .5f), new Vector2(.5f, h - r), r - .5f);
                p.MoveTo(new Vector2(.5f, r)); p.ArcTo(new Vector2(.5f, .5f), new Vector2(r, .5f), r - .5f);
                p.Stroke();
                return;
            }
            Path(p, .5f, w, h, r - .5f);
            if (button)
            {
                // dark-light-dark across, as polished gold catches a light
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Hex(0x7A5A2C), 0f), new GradientColorKey(Hex(0xFFE6A6), .42f), new GradientColorKey(Hex(0x9B7334), .7f), new GradientColorKey(Hex(0xFFE6A6), 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                p.strokeFillGradient = FillGradient.MakeLinearGradient(g, new Vector2(0, 0), new Vector2(w, 0), AddressMode.Clamp);
                p.lineWidth = .8f;
                p.Stroke();
                return;
            }
            float edge = bright ? .95f : .32f;
            p.strokeFillGradient = Linear(new Color(1f, .9f, .7f, Mathf.Min(1f, edge * 1.5f)), new Color(Gold.r, Gold.g, Gold.b, edge * .8f), top, bottom);
            p.lineWidth = bright ? 1f : .7f;
            p.Stroke();
        }

        // 5 px dashes 4 px apart from a towards b, as sub-paths of the current path.
        static void Dashes(Painter2D p, Vector2 a, Vector2 b)
        {
            float length = Vector2.Distance(a, b);
            var dir = (b - a) / Mathf.Max(length, .001f);
            for (float s = 2f; s < length - 2f; s += 9f)
            {
                p.MoveTo(a + dir * s);
                p.LineTo(a + dir * Mathf.Min(s + 5f, length - 2f));
            }
        }

        // From one colour to the other between two points. Through a Gradient: the two-colour MakeLinearGradient
        // drops the colours' alpha (its alpha keys come out 1), which drew every faint line and the glass opaque.
        static FillGradient Linear(Color a, Color b, Vector2 from, Vector2 to)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                      new[] { new GradientAlphaKey(a.a, 0f), new GradientAlphaKey(b.a, 1f) });
            return FillGradient.MakeLinearGradient(g, from, to, AddressMode.Clamp);
        }

        static FillGradient Flat(Color c) => Linear(c, c, Vector2.zero, Vector2.right);

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        // A rounded rectangle inset by `inset` (negative: outset) from the w x h box.
        static void Path(Painter2D p, float inset, float w, float h, float r)
        {
            float x0 = inset, y0 = inset, x1 = w - inset, y1 = h - inset;
            r = Mathf.Max(0f, r);
            p.BeginPath();
            p.MoveTo(new Vector2(x0 + r, y0));
            p.LineTo(new Vector2(x1 - r, y0));
            p.ArcTo(new Vector2(x1, y0), new Vector2(x1, y0 + r), r);
            p.LineTo(new Vector2(x1, y1 - r));
            p.ArcTo(new Vector2(x1, y1), new Vector2(x1 - r, y1), r);
            p.LineTo(new Vector2(x0 + r, y1));
            p.ArcTo(new Vector2(x0, y1), new Vector2(x0, y1 - r), r);
            p.LineTo(new Vector2(x0, y0 + r));
            p.ArcTo(new Vector2(x0, y0), new Vector2(x0 + r, y0), r);
            p.ClosePath();
        }
    }
}
