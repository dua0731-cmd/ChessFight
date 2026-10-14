using UnityEngine;

namespace ChessFight.Game
{
    // The match sheet's art (R131, design 2-B "호두나무 체스 상자", thinner and see-through): a walnut box
    // with a chessboard band along the top and an ivory inlay round a dark glass sheet ruled like a
    // scoresheet. Painted once into one texture, composed as the design page does (in sRGB, PITFALLS 30)
    // and only then bent for linear blending, so it looks like the design over the match.
    public static class VoiceArt
    {
        // The sheet in menu pixels (the 1280 × 720 reference), its shadow margin, and where the parts sit.
        public const int Width = 640, Height = 296, Margin = 12;
        public const float GlassLeft = 5, GlassTop = 13, GlassRight = 635, GlassBottom = 291;
        public const float HeadingBottom = 58, ColumnsTop = 60, ColumnsBottom = 90, RowsTop = 91, RowStep = 33, RowHeight = 32;
        public const float NumberRight = 40, MineRight = 338;
        public const int Rows = 6;

        static Texture2D sheet;

        static readonly int[] Walnut = { 0x5A3A22, 0x3A2416, 0x7A5634 };

        // Twice the menu resolution, so the rules stay sharp on a 1440p screen.
        public static Texture2D Sheet()
        {
            if (sheet != null) return sheet;
            const int k = 2;
            int tw = (Width + 2 * Margin) * k, th = (Height + 2 * Margin) * k;
            var wood = MenuArt.WoodTexture(Walnut, false);
            Color[] grain = null;
            int gw = 1, gh = 1;
            try { grain = wood.GetPixels(); gw = wood.width; gh = wood.height; }
            catch (UnityException) { grain = null; }

            Color woodBase = Hex(0x553620), ivory = Hex(0xEFE4CC), glass = Hex(0x17100B), band = Hex(0x3A2416);
            var px = new Color[tw * th];
            for (int py = 0; py < th; py++)
                for (int x = 0; x < tw; x++)
                {
                    float rx = (x + .5f) / k - Margin, ry = (py + .5f) / k - Margin;   // menu pixels, y down
                    var c = new Color(0, 0, 0, 0);

                    // A soft shadow under the box, a little lower than it.
                    float ds = RoundRect(rx, ry - 4f, 0, 0, Width, Height, 8f);
                    if (ds > -8f) c = new Color(0, 0, 0, .3f * Mathf.Exp(-Mathf.Max(0f, ds + 2f) * Mathf.Max(0f, ds + 2f) / 40f));

                    float d = RoundRect(rx, ry, 0, 0, Width, Height, 7f);
                    float frame = Mathf.Clamp01(.5f - d * k);
                    if (frame > 0f)
                    {
                        // Walnut, lighter at the top, a sheen along the upper edge, darker at the bottom.
                        Color w = grain != null ? grain[(int)(ry * 2.2f + 9) % gh * gw + (int)(rx * 2.2f) % gw] : woodBase;
                        float t = Mathf.Clamp01(ry / Height);
                        w = Color.Lerp(w, Hex(0x2C1A0E), .15f + .3f * t);
                        if (ry < 2.2f) w = Color.Lerp(w, new Color(1f, .92f, .78f), .28f * (1f - ry / 2.2f));
                        if (ry > Height - 1.6f || rx > Width - 1.2f) w = Color.Lerp(w, Color.black, .35f);
                        if (rx < 1.2f) w = Color.Lerp(w, new Color(1f, .9f, .75f), .12f);
                        w.a = .9f;

                        // The chessboard band along the top.
                        if (ry >= 4f && ry < 9f && rx >= 6f && rx < Width - 6f)
                        {
                            int square = (int)((rx - 6f) / 6f);
                            var sq = square % 2 == 0 ? Hex(0xE6D3AE) : band;
                            w = Over(w, sq, .68f);
                            if (ry < 5f) w = Over(w, Color.white, square % 2 == 0 ? .18f : .06f);
                            if (ry >= 8.4f) w = Over(w, Color.black, .2f);
                        }

                        float dg = RoundRect(rx, ry, GlassLeft, GlassTop, GlassRight, GlassBottom, 3f);
                        // A dark groove round the glass, and an ivory inlay just outside it.
                        if (dg > 0f && dg < 1.1f) w = Over(w, Color.black, .5f);
                        else if (dg >= 1.1f && dg < 2.1f) w = Over(w, ivory, .3f);
                        float inside = Mathf.Clamp01(.5f - dg * k);
                        if (inside > 0f) w = Color.Lerp(w, Glass(rx, ry, glass, ivory), inside);
                        w.a *= frame;
                        c = Over(c, w, w.a);
                    }
                    // Linear blending shows a dark sheet thinner than the page did; bend the alpha back.
                    c.a = StageKit.Darkening(c.a);
                    px[(th - 1 - py) * tw + x] = c;
                }
            sheet = new Texture2D(tw, th, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Voice sheet"
            };
            sheet.SetPixels(px);
            sheet.Apply(false, true);
            return sheet;
        }

        // The glass and its ruling at one point: dark glass, the number column's walnut band, the rules.
        static Color Glass(float x, float y, Color glass, Color ivory)
        {
            float t = Mathf.Clamp01((y - GlassTop) / (GlassBottom - GlassTop));
            var c = Color.Lerp(glass, Hex(0x0E0905), t);
            c = Over(c, ivory, .035f * (1f - t));   // a faint light at the top
            c.a = .72f;
            if (y >= ColumnsTop && x < NumberRight) c = Over(c, Hex(0x3A2416), .35f);
            // The heading's double rule, the column heads' rule, the rows' rules and the two upright rules.
            if (y >= HeadingBottom && y < HeadingBottom + 2f) c = Over(c, ivory, .3f);
            if (y >= ColumnsBottom && y < ColumnsBottom + 1f) c = Over(c, ivory, .26f);
            for (int r = 0; r < Rows - 1; r++)
            {
                float rule = RowsTop + r * RowStep + RowHeight;
                if (y >= rule && y < rule + 1f) c = Over(c, ivory, .09f);
            }
            if (y >= ColumnsTop && ((x >= NumberRight && x < NumberRight + 1f) || (x >= MineRight && x < MineRight + 1f)))
                c = Over(c, ivory, .2f);
            return c;
        }

        // Signed distance to a rounded rectangle (negative inside), in menu pixels.
        static float RoundRect(float px, float py, float x0, float y0, float x1, float y1, float r)
        {
            float cx = (x0 + x1) * .5f, cy = (y0 + y1) * .5f;
            float dx = Mathf.Abs(px - cx) - ((x1 - x0) * .5f - r), dy = Mathf.Abs(py - cy) - ((y1 - y0) * .5f - r);
            float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
        }

        // `top` laid over `under` at opacity `a` (straight alpha), as a page composes.
        static Color Over(Color under, Color top, float a)
        {
            float outA = a + under.a * (1f - a);
            if (outA <= 0f) return new Color(0f, 0f, 0f, 0f);
            float k = under.a * (1f - a);
            return new Color((top.r * a + under.r * k) / outA, (top.g * a + under.g * k) / outA, (top.b * a + under.b * k) / outA, outA);
        }

        public static Color Hex(int rgb) => new Color((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f);
    }
}
