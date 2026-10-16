using UnityEngine;

namespace ChessFight.Game
{
    // The title hall's textures, made from code like the rest of the menu art:
    // stone blocks, the sunset glass of the lancet windows, and the pennants'
    // cloth with a gold border, a swallowtail and the piece glyph in gold.
    public sealed partial class IntroStage
    {
        static Texture2D stone, window;

        static Texture2D Make(string name, int w, int h, Color[] px, TextureWrapMode wrap)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, wrapMode = wrap, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            texture.SetPixels(px);
            texture.Apply(true);
            return texture;
        }

        // Rows of grey-brown blocks, every other row offset by half a block, dark mortar.
        static Texture2D StoneTexture()
        {
            if (stone != null) return stone;
            const int w = 512, h = 256, bw = 64, bh = 32;
            var px = new Color[w * h];
            Color a = MenuArt.Hex(0x4A3A2C), b = MenuArt.Hex(0x3A2D22), mortar = MenuArt.Hex(0x1E160F);
            var random = new System.Random(150);
            var shade = new float[(w / bw + 1) * (h / bh)];
            for (int i = 0; i < shade.Length; i++) shade[i] = (float)random.NextDouble();
            for (int y = 0; y < h; y++)
            {
                int row = y / bh, off = row % 2 == 0 ? 0 : bw / 2;
                for (int x = 0; x < w; x++)
                {
                    int xs = (x + off) % w, col = xs / bw;
                    bool joint = y % bh < 2 || xs % bw < 2;
                    float n = Mathf.PerlinNoise(x * .09f, y * .09f) * .5f + Mathf.PerlinNoise(x * .31f, y * .31f) * .25f;
                    var c = Color.Lerp(a, b, shade[row * (w / bw + 1) + col] * .8f + n * .4f);
                    // A lighter top edge on each block, a darker bottom one.
                    if (y % bh == bh - 3) c = Color.Lerp(c, Color.white, .06f);
                    if (y % bh == 2) c = Color.Lerp(c, Color.black, .15f);
                    px[y * w + x] = joint ? mortar : c;
                }
            }
            return stone = Make("Intro stone", w, h, px, TextureWrapMode.Repeat);
        }

        // A lancet window: a round arched top, a dark frame and two columns of amber
        // panes in a checker of light and deep, with lead between them.
        static Texture2D WindowTexture()
        {
            if (window != null) return window;
            const int w = 64, h = 320, frame = 5, cols = 2, rows = 9;
            var px = new Color[w * h];
            Color light = MenuArt.Hex(0xFFE2AA), deep = MenuArt.Hex(0xF0AA64), lead = MenuArt.Hex(0x2A1E15);
            float r = w / 2f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fx = x + .5f - r, fy = y + .5f - (h - r);
                    bool inside = y < h - r || fx * fx + fy * fy <= r * r;
                    bool glass = x >= frame && x < w - frame && y >= frame && (y < h - r || fx * fx + fy * fy <= (r - frame) * (r - frame));
                    Color c;
                    if (!inside) c = new Color(0, 0, 0, 0);
                    else if (!glass) c = lead;
                    else
                    {
                        float gx = (x - frame) / (float)(w - 2 * frame), gy = (y - frame) / (float)(h - 2 * frame);
                        int col = Mathf.Clamp((int)(gx * cols), 0, cols - 1), row = Mathf.Clamp((int)(gy * rows), 0, rows - 1);
                        c = (col + row) % 2 == 0 ? light : deep;
                        // Lead cames between the panes.
                        float ex = gx * cols % 1f, ey = gy * rows % 1f;
                        if (ex < .04f || ex > .96f || ey < .03f || ey > .97f) c = lead;
                        // Brighter low in the window, as the sun sets behind it.
                        c = Color.Lerp(c, Color.white, .12f * (1f - gy));
                    }
                    px[y * w + x] = c;
                }
            return window = Make("Intro window", w, h, px, TextureWrapMode.Clamp);
        }

        // Cloth in `cloth` with soft folds, a gold line inside each long edge and
        // along the swallowtail, a dark hem at the top, and the glyph in gold.
        static Texture2D PennantTexture(string glyph, int cloth)
        {
            const int w = 128, h = 256;
            const float notch = .2f;
            var px = new Color[w * h];
            Color baseColor = MenuArt.Hex(cloth), gold = MenuArt.Hex(0xF2C14E), hem = Color.Lerp(baseColor, Color.black, .45f);
            var mask = GlyphMask(glyph, 96, 96);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + .5f) / w, v = (y + .5f) / h;   // v 0 at the tail, 1 at the rod
                    float edge = notch * (1f - Mathf.Abs(u * 2f - 1f));
                    if (v < edge) { px[y * w + x] = new Color(0, 0, 0, 0); continue; }
                    // Folds: soft vertical light and shade, and a faint weave.
                    float fold = Mathf.Sin(u * Mathf.PI * 3f) * .08f + (u - .5f) * -.1f;
                    var c = Color.Lerp(baseColor, fold > 0 ? Color.white : Color.black, Mathf.Abs(fold));
                    if ((x + y) % 4 == 0) c = Color.Lerp(c, Color.black, .04f);
                    // Gold lines: 9 px in from each long edge, and the same distance above the tail.
                    float inX = Mathf.Min(x, w - 1 - x);
                    float aboveTail = (v - edge) * h;
                    bool line = (inX >= 8 && inX < 11 && aboveTail >= 8) || (aboveTail >= 8 && aboveTail < 11 && inX >= 8);
                    if (line) c = gold;
                    if (y >= h - 10) c = hem;
                    // The glyph, centred across, a little above the middle.
                    int gx = x - (w - 96) / 2, gy = y - (int)(h * .5f);
                    if (mask != null && gx >= 0 && gx < 96 && gy >= 0 && gy < 96)
                    {
                        float m = mask[gy * 96 + gx];
                        c = Color.Lerp(c, gold, m);
                    }
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            return Make("Intro pennant", w, h, px, TextureWrapMode.Clamp);
        }

        // The glyph's coverage (0..1, row 0 at the bottom) in a size x size square,
        // drawn once with a TextMesh in an OS symbol font; null where no such font is.
        static Font symbols;

        static float[] GlyphMask(string glyph, int width, int height)
        {
            if (symbols == null)
            {
                try { symbols = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Symbol", "Segoe UI Emoji", "Arial Unicode MS", "DejaVu Sans" }, 128); }
                catch (System.Exception) { symbols = null; }
            }
            if (symbols == null) return null;
            symbols.RequestCharactersInTexture(glyph, 128);

            var studio = new GameObject("Intro Glyph");
            studio.transform.position = new Vector3(0, -2400f, 0);
            var text = new GameObject("Glyph").AddComponent<TextMesh>();
            text.transform.SetParent(studio.transform, false);
            text.font = symbols;
            text.text = glyph;
            text.fontSize = 128;
            text.characterSize = .1f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;
            var renderer = text.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = symbols.material;
            var bounds = renderer.bounds;

            var rig = new GameObject("Glyph Camera");
            rig.transform.SetParent(studio.transform, false);
            var camera = rig.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x * height / width) * 1.06f;
            camera.transform.position = bounds.center - new Vector3(0, 0, 5f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0, 0, 0, 0);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 10f;
            camera.allowHDR = false;
            var target = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = target;
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            camera.Render();
            RenderSettings.fog = fog;

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var read = new Texture2D(width, height, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            read.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;

            var pixels = read.GetPixels();
            var mask = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                mask[i] = Mathf.Clamp01(Mathf.Max(pixels[i].r, Mathf.Max(pixels[i].g, pixels[i].b)) * pixels[i].a * 1.2f);
            Destroy(read);
            target.Release();
            Destroy(target);
            studio.SetActive(false);
            Destroy(studio);
            return mask;
        }
    }
}
