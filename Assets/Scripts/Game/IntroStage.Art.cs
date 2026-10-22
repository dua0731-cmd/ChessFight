using UnityEngine;

namespace ChessFight.Game
{
    // The title hall's textures, made from code like the rest of the menu art:
    // stone blocks, the sunset glass of the lancet windows, and the pennants'
    // cloth with a gold border, a swallowtail and the piece glyph in gold.
    public sealed partial class IntroStage
    {
        static Texture2D stone;

        static Texture2D Make(string name, int w, int h, Color[] px, TextureWrapMode wrap)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, wrapMode = wrap, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            texture.SetPixels(px);
            texture.Apply(true);
            return texture;
        }

        // Rows of cream castle blocks (10-17: the lobby castle's stone, was grey-brown), every other row
        // offset by half a block, light mortar; each block its own shade, a lit top edge and a darker foot.
        static Texture2D StoneTexture()
        {
            if (stone != null) return stone;
            const int w = 512, h = 256, bw = 64, bh = 32;
            var px = new Color[w * h];
            Color a = MenuArt.Hex(0xE9DCC0), b = MenuArt.Hex(0xCDBB98), mortar = MenuArt.Hex(0xA8977A);
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
                    if (y % bh >= bh - 4) c = Color.Lerp(c, Color.white, .12f);
                    if (y % bh <= 3) c = Color.Lerp(c, MenuArt.Hex(0x8A7658), .25f);
                    px[y * w + x] = joint ? mortar : c;
                }
            }
            return stone = Make("Intro stone", w, h, px, TextureWrapMode.Repeat);
        }

        // The wall's stone material (the piers copy it with their own tiling).
        static Material StoneMaterial(Vector2 tiling) => LastSceneArt.Textured("intro stone", StoneTexture(), .12f, tiling);

        // Cloth in `cloth` with soft folds, a gold line inside each long edge and
        // along the swallowtail, a dark hem at the top, and the glyph in gold.
        internal static Texture2D PennantTexture(string glyph, int cloth)
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

        internal static float[] GlyphMask(string glyph, int width, int height)
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
