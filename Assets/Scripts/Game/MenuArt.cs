using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    // The look of the menus since design C "그랜드 아레나" (revised 2026-10-03,
    // Docs/Architecture/UI.md): a dark warm hall with a wooden floor, maple and
    // walnut chessboards in mahogany frames, brass trim, warm spotlights with
    // visible beams, and the PAWN RUSH figures (LastSceneFigure). Every texture is
    // drawn here from code, the way the design samples drew theirs, so the
    // project gains no image files. Used by IntroStage, LobbyStage and the
    // loading screen's entrance stage.
    public static class MenuArt
    {
        // Wood palettes: base, dark grain, light grain (the samples' WOOD table).
        public static readonly int[] Maple = { 0xE7C28C, 0xB9864E, 0xF7DDB0 };
        public static readonly int[] Walnut = { 0x5C3920, 0x3A2212, 0x7E5232 };
        public static readonly int[] Mahogany = { 0x5E2B1A, 0x3C170C, 0x7E3E26 };
        public static readonly int[] DarkWood = { 0x2E1C11, 0x1A0F08, 0x46301E };
        static readonly int[] FloorA = { 0x2C1B10, 0x170D07, 0x43291A };
        static readonly int[] FloorB = { 0x22150C, 0x120A05, 0x38230F };

        public const int Brass = 0xD9AE62, Gold = 0xFFC93D, Led = 0xFFC66B, Warm = 0xFFB45A;
        public const int Cyan = 0x3CD0FF, Red = 0xFF4D62;
        public const int HallColor = 0x140D08;

        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, RenderTexture> captions = new Dictionary<string, RenderTexture>();

        public static Color Hex(int rgb) => PieceFigure.Hex(rgb);

        // ---------- the hall ----------

        // Warm dark sky and haze, dim warm ambient light and the plank floor.
        // The spotlights are each stage's own (Spot).
        public static void Hall(Transform parent, Camera view, float hazeStart, float hazeEnd)
        {
            var sky = Sky();
            if (sky != null)
            {
                RenderSettings.skybox = sky;
                view.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                view.clearFlags = CameraClearFlags.SolidColor;
                view.backgroundColor = Hex(HallColor);
            }
            view.farClipPlane = 300f;
            view.allowHDR = false;
            HallLighting(hazeStart, hazeEnd);
            // Scenes may carry a sun of their own; the hall is lit by spots only.
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional && light.transform.parent == null) light.enabled = false;

            Floor(parent);
        }

        // The hall's haze and dim warm ambient light (RenderSettings are global:
        // the loading screen sets them only around its own render).
        public static void HallLighting(float hazeStart, float hazeEnd)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex(HallColor);
            RenderSettings.fogStartDistance = hazeStart;
            RenderSettings.fogEndDistance = hazeEnd;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex(0x6A4A30) * .8f;
            RenderSettings.ambientEquatorColor = Hex(0x4A3220) * .7f;
            RenderSettings.ambientGroundColor = Hex(0x120A06);
            RenderSettings.reflectionIntensity = .6f;
        }

        // The hall's plank floor, 240 units square, on its own (the loading
        // screen's stage lives under the scene and keeps the scene's settings).
        public static GameObject Floor(Transform parent)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Hall Floor";
            Object.Destroy(floor.GetComponent<Collider>());
            floor.transform.SetParent(parent, false);
            floor.transform.localScale = new Vector3(24f, 1f, 24f);   // 240 x 240
            var renderer = floor.GetComponent<Renderer>();
            renderer.sharedMaterial = FloorMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return floor;
        }

        // A spotlight from `from` onto `to`. `angle` is the half angle in radians and
        // `penumbra` the soft share of it, as on the samples. The range is long so the
        // light barely fades over the stage, like the samples' lights without decay.
        public static Light Spot(Transform parent, Vector3 from, Vector3 to, int rgb, float intensity, float angle, float penumbra, bool shadow = false)
        {
            var go = new GameObject("Spot");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = from;
            go.transform.localRotation = Quaternion.LookRotation(to - from);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = Hex(rgb);
            light.intensity = intensity;
            light.range = (to - from).magnitude * 6f;
            light.spotAngle = angle * 2f * Mathf.Rad2Deg;
            light.innerSpotAngle = light.spotAngle * (1f - penumbra);
            light.renderMode = LightRenderMode.ForcePixel;
            light.shadows = shadow ? LightShadows.Soft : LightShadows.None;
            if (shadow)
            {
                light.shadowStrength = .75f;
                light.shadowBias = .02f;
                light.shadowNormalBias = .3f;
            }
            return light;
        }

        // A soft beam of light: an open cone from `from` (narrow) to `to` (wide),
        // fading along its length. Returns the transform to re-aim with Aim.
        public static Transform Beam(Transform parent, Vector3 from, Vector3 to, int rgb, float radius, float opacity)
        {
            var color = Hex(rgb);
            color.a = opacity;
            var go = LastSceneArt.Part(parent, "Beam", BeamMesh(radius), Glow("beam", BeamTexture(), color, false), Vector3.zero, null, null, false);
            go.GetComponent<MeshRenderer>().receiveShadows = false;
            Aim(go.transform, from, to);
            return go.transform;
        }

        public static void Aim(Transform beam, Vector3 from, Vector3 to)
        {
            var d = to - from;
            beam.localPosition = from + d * .5f;
            beam.localRotation = Quaternion.FromToRotation(Vector3.down, d.normalized);
            beam.localScale = new Vector3(1f, d.magnitude, 1f);
        }

        // A round glow facing `view` (for lamps and flashes). Its material is its own.
        public static Transform Halo(Transform parent, Vector3 at, int rgb, float size, float opacity, Camera view)
        {
            var color = Hex(rgb);
            color.a = opacity;
            var go = LastSceneArt.Part(parent, "Halo", LastSceneArt.Panel(1f, 1f), Glow("halo", RadialTexture(), color, false), at,
                                       null, Vector3.one * size, false);
            if (view != null) go.transform.rotation = Quaternion.LookRotation(go.transform.position - view.transform.position);
            return go.transform;
        }

        // A glowing disc lying on the floor.
        public static Transform FloorGlow(Transform parent, Vector3 at, int rgb, float radius, float opacity)
        {
            var color = Hex(rgb);
            color.a = opacity;
            return LastSceneArt.Part(parent, "Floor Glow", LastSceneArt.Panel(1f, 1f), Glow("halo", RadialTexture(), color, false), at,
                                     Quaternion.Euler(90f, 0, 0), Vector3.one * radius * 2f, false).transform;
        }

        // ---------- materials ----------

        public static Material Wood(int[] palette, float gloss, Vector2 tiling, bool vertical = false)
        {
            string key = "W" + palette[0] + "_" + gloss + "_" + tiling + vertical;
            if (materials.TryGetValue(key, out var cached) && cached != null) return cached;
            var material = new Material(PieceFigure.Lit(Color.white, gloss, .05f))
            {
                name = "Menu wood " + palette[0].ToString("X6"), mainTexture = WoodTexture(palette, vertical), mainTextureScale = tiling
            };
            return materials[key] = material;
        }

        public static Material Board(int squares)
        {
            string key = "B" + squares;
            if (materials.TryGetValue(key, out var cached) && cached != null) return cached;
            var material = new Material(PieceFigure.Lit(Color.white, .45f, .05f)) { name = "Menu board " + squares, mainTexture = BoardTexture(squares) };
            return materials[key] = material;
        }

        public static Material BrassMetal() => PieceFigure.Lit(Hex(Brass), .72f, .85f);

        // Unlit, with no fog: LED strips and lamp faces.
        public static Material Lamp(int rgb) => LastSceneArt.Blended("lamp " + rgb.ToString("X6"), null, Hex(rgb));

        // Unlit and blended (Sprites/Default is always in the build). `shared`
        // false hands back a copy whose colour can be faded on its own.
        public static Material Glow(string key, Texture texture, Color tint, bool shared = true)
        {
            var material = LastSceneArt.Blended("menu " + key + ColorUtility.ToHtmlStringRGBA(tint), texture, tint, shared);
            // Over every opaque thing and after the other blended parts.
            material.renderQueue = (int)RenderQueue.Transparent + 10;
            return material;
        }

        static Material Sky()
        {
            if (materials.TryGetValue("sky", out var cached) && cached != null) return cached;
            var shader = Shader.Find("Skybox/6 Sided");
            if (shader == null) return null;
            Color low = Hex(HallColor), mid = Hex(0x26180E), top = Hex(0x3E2716);
            var side = new Texture2D(4, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Menu hall sky" };
            for (int y = 0; y < 256; y++)
            {
                float up = Mathf.Clamp01((y / 255f - .5f) * 2f);
                Color c = y < 128 ? low : up < .3f ? Color.Lerp(low, mid, up / .3f) : Color.Lerp(mid, top, (up - .3f) / .7f);
                for (int x = 0; x < 4; x++) side.SetPixel(x, y, c);
            }
            side.Apply();
            var material = new Material(shader) { name = "Menu hall sky" };
            foreach (string face in new[] { "_FrontTex", "_BackTex", "_LeftTex", "_RightTex" }) material.SetTexture(face, side);
            material.SetTexture("_UpTex", Solid(top));
            material.SetTexture("_DownTex", Solid(low));
            return materials["sky"] = material;
        }

        static Material FloorMaterial()
        {
            if (materials.TryGetValue("floor", out var cached) && cached != null) return cached;
            var material = new Material(PieceFigure.Lit(Color.white, .62f, .1f)) { name = "Menu floor", mainTexture = FloorTexture(), mainTextureScale = new Vector2(60f, 60f) };
            return materials["floor"] = material;
        }

        // ---------- textures ----------

        // Park-Miller, as the samples seeded their grain.
        sealed class Seeded
        {
            long s;
            public Seeded(int seed) { s = System.Math.Abs(seed) % 2147483646 + 1; }
            public float Next() { s = s * 16807 % 2147483647; return (s - 1) / 2147483646f; }
        }

        // Wood grain into `px` (width `stride`) inside the rectangle: a base colour,
        // a few broad soft bands, then fine wavy lines along the grain.
        static void Grain(Color[] px, int stride, int x0, int y0, int w, int h, int[] palette, bool vertical, int seed, float density)
        {
            var random = new Seeded(seed);
            Color baseColor = Hex(palette[0]), dark = Hex(palette[1]), light = Hex(palette[2]);
            int across = vertical ? w : h, along = vertical ? h : w;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) px[(y0 + y) * stride + x0 + x] = baseColor;
            for (int i = 0; i < 6; i++)
            {
                float o = random.Next() * across, bw = 8 + random.Next() * across * .3f, a = .06f + random.Next() * .08f;
                var c = random.Next() < .5f ? dark : light;
                for (int k = Mathf.Max(0, (int)o); k < Mathf.Min(across, (int)(o + bw)); k++)
                    for (int t = 0; t < along; t++) Blend(px, stride, x0, y0, vertical ? k : t, vertical ? t : k, c, a);
            }
            int lines = Mathf.RoundToInt(across / density);
            for (int i = 0; i < lines; i++)
            {
                float off = (i + random.Next()) * across / lines, amp = .6f + random.Next() * 2.4f, fq = .008f + random.Next() * .03f, ph = random.Next() * 6.3f;
                float a = .07f + random.Next() * .2f;
                var c = random.Next() < .62f ? dark : light;
                float width = .5f + random.Next() * 1.4f;
                for (int t = 0; t < along; t++)
                {
                    float center = off + Mathf.Sin(t * fq + ph) * amp + Mathf.Sin(t * fq * 2.7f + ph * 1.7f) * amp * .35f;
                    float lo = center - width / 2f, hi = center + width / 2f;
                    for (int k = Mathf.FloorToInt(lo); k <= Mathf.FloorToInt(hi); k++)
                    {
                        if (k < 0 || k >= across) continue;
                        float cover = Mathf.Clamp01(Mathf.Min(hi, k + 1) - Mathf.Max(lo, k));
                        Blend(px, stride, x0, y0, vertical ? k : t, vertical ? t : k, c, a * cover);
                    }
                }
            }
        }

        static void Blend(Color[] px, int stride, int x0, int y0, int x, int y, Color c, float a)
        {
            int i = (y0 + y) * stride + x0 + x;
            px[i] = Color.LerpUnclamped(px[i], c, a);
        }

        static Texture2D Finish(string name, int w, int h, Color[] px, TextureWrapMode wrap)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                wrapMode = wrap, filterMode = FilterMode.Trilinear, anisoLevel = 8, name = name
            };
            texture.SetPixels(px);
            texture.Apply(true);
            return texture;
        }

        public static Texture2D WoodTexture(int[] palette, bool vertical)
        {
            string key = "wood" + palette[0] + vertical;
            if (textures.TryGetValue(key, out var cached) && cached != null) return cached;
            const int w = 512, h = 128;
            var px = new Color[w * h];
            Grain(px, w, 0, 0, w, h, palette, vertical, palette[0] % 977 + (vertical ? 13 : 0), 3f);
            return textures[key] = Finish("Menu wood", w, h, px, TextureWrapMode.Repeat);
        }

        // `squares` x `squares` maple and walnut squares with dark seams and a
        // faint sheen from one corner. a1 (bottom left) is walnut.
        public static Texture2D BoardTexture(int squares)
        {
            string key = "board" + squares;
            if (textures.TryGetValue(key, out var cached) && cached != null) return cached;
            const int s = 96;
            int size = squares * s;
            var px = new Color[size * size];
            for (int i = 0; i < squares; i++)
                for (int j = 0; j < squares; j++)
                {
                    bool dark = (i + j) % 2 == 0;
                    Grain(px, size, i * s, j * s, s, s, dark ? Walnut : Maple, !dark, i * 31 + j * 17 + 5, 2.6f);
                }
            var seam = new Color(30 / 255f, 14 / 255f, 5 / 255f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    if (x % s < 1 || x % s >= s - 1 || y % s < 1 || y % s >= s - 1) px[i] = Color.Lerp(px[i], seam, .55f);
                    float u = (x + (size - y)) / (2f * size);   // 0 at the top left, 1 at the bottom right
                    px[i] = u < .5f ? Color.Lerp(px[i], Color.white, .1f * (1 - u * 2)) : Color.Lerp(px[i], Color.black, .1f * (u * 2 - 1));
                }
            return textures[key] = Finish("Menu board", size, size, px, TextureWrapMode.Clamp);
        }

        static Texture2D FloorTexture()
        {
            if (textures.TryGetValue("floor", out var cached) && cached != null) return cached;
            const int size = 512, s = 128;
            var px = new Color[size * size];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    Grain(px, size, i * s, j * s, s, s, (i + j) % 2 == 1 ? FloorA : FloorB, (i + j) % 2 == 0, i * 13 + j * 7 + 3, 3f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (x % s < 2 || y % s < 2) px[y * size + x] = Color.Lerp(px[y * size + x], Color.black, .45f);
            return textures["floor"] = Finish("Menu floor", size, size, px, TextureWrapMode.Repeat);
        }

        // White with a soft round alpha: halos, floor glows, flashes.
        public static Texture2D RadialTexture()
        {
            if (textures.TryGetValue("radial", out var cached) && cached != null) return cached;
            const int size = 128;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Min(1f, new Vector2(x + .5f - 64f, y + .5f - 64f).magnitude / 64f);
                    float a = r < .25f ? Mathf.Lerp(1f, .55f, r / .25f) : Mathf.Lerp(.55f, 0f, (r - .25f) / .75f);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            var texture = Finish("Menu radial", size, size, px, TextureWrapMode.Clamp);
            return textures["radial"] = texture;
        }

        // Bright at the lamp (v = 1), fading towards the lit spot (v = 0).
        static Texture2D BeamTexture()
        {
            if (textures.TryGetValue("beam", out var cached) && cached != null) return cached;
            const int h = 128;
            var px = new Color[4 * h];
            for (int y = 0; y < h; y++)
            {
                float t = 1f - y / (h - 1f);   // 0 at the lamp
                float a = t < .35f ? Mathf.Lerp(1f, .53f, t / .35f) : Mathf.Lerp(.53f, .1f, (t - .35f) / .65f);
                for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color(1, 1, 1, a);
            }
            return textures["beam"] = Finish("Menu beam", 4, h, px, TextureWrapMode.Clamp);
        }

        // ---------- HUD textures (USS has no gradients) ----------

        // Lacquered walnut for the HUD panels: warm grain under a dark glaze with a
        // soft sheen along the top.
        public static Texture2D PanelTexture()
        {
            if (textures.TryGetValue("panel", out var cached) && cached != null) return cached;
            const int w = 256, h = 128;
            var px = new Color[w * h];
            Grain(px, w, 0, 0, w, h, new[] { 0x4E321E, 0x2C1A0E, 0x6A4630 }, false, 4242, 4f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = 1f - y / (h - 1f);   // 0 at the top
                    int i = y * w + x;
                    var c = Color.Lerp(px[i], Hex(0x281A10), .35f + .3f * v);
                    if (v < .38f) c = Color.Lerp(c, new Color(1f, .9f, .75f), .07f * (1 - v / .38f));
                    c.a = .96f;
                    px[i] = c;
                }
            return textures["panel"] = Finish("Menu panel", w, h, px, TextureWrapMode.Clamp);
        }

        // A mode card: walnut, darker to the right, with the mode's colour glowing
        // behind where its piece stands (right of centre).
        public static Texture2D CardTexture(Color glow)
        {
            string key = "card" + ColorUtility.ToHtmlStringRGB(glow);
            if (textures.TryGetValue(key, out var cached) && cached != null) return cached;
            const int w = 396, h = 116;
            var px = new Color[w * h];
            Grain(px, w, 0, 0, w, h, new[] { 0x4D311D, 0x2C1A0E, 0x66432A }, false, 977, 4f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    float u = x / (w - 1f), v = 1f - y / (h - 1f);
                    var c = Color.Lerp(px[i], Hex(0x2C1C11), .25f + .45f * u);
                    float r = new Vector2((u - .86f) / .55f, (v - .62f) / 1.2f).magnitude;
                    c = Color.Lerp(c, glow, .36f * Mathf.Clamp01(1f - r / .72f));
                    c.a = 1f;
                    px[i] = c;
                }
            return textures[key] = Finish("Menu card", w, h, px, TextureWrapMode.Clamp);
        }

        // Top to bottom, `from` fading to `to` (alpha included): the lobby's top bar shade.
        public static Texture2D VerticalFade(Color from, Color to)
        {
            string key = "fade" + ColorUtility.ToHtmlStringRGBA(from) + ColorUtility.ToHtmlStringRGBA(to);
            if (textures.TryGetValue(key, out var cached) && cached != null) return cached;
            const int h = 64;
            var px = new Color[2 * h];
            for (int y = 0; y < h; y++)
            {
                var c = Color.Lerp(to, from, y / (h - 1f));
                px[y * 2] = px[y * 2 + 1] = c;
            }
            var texture = new Texture2D(2, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Menu fade" };
            texture.SetPixels(px);
            texture.Apply();
            return textures[key] = texture;
        }

        // Left to right through the given colours, transparent at both ends: the
        // matchmaking banner's top line.
        public static Texture2D Line(params Color[] stops)
        {
            string key = "line";
            foreach (var c in stops) key += ColorUtility.ToHtmlStringRGBA(c);
            if (textures.TryGetValue(key, out var cached) && cached != null) return cached;
            const int w = 128;
            var px = new Color[w];
            for (int x = 0; x < w; x++)
            {
                float t = x / (w - 1f) * (stops.Length - 1);
                int i = Mathf.Min(stops.Length - 2, (int)t);
                px[x] = Color.Lerp(stops[i], stops[i + 1], t - i);
            }
            var texture = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Menu line" };
            texture.SetPixels(px);
            texture.Apply();
            return textures[key] = texture;
        }

        // The lobby wall: dark warm wood tone, a faint large checker, a warm glow in
        // the middle and fine scan lines, like an old lit sign board.
        public static Texture2D WallTexture()
        {
            if (textures.TryGetValue("wall", out var cached) && cached != null) return cached;
            const int w = 768, h = 280, square = 28;
            Color top = Hex(0x2A1A0E), bottom = Hex(0x110904), glow = new Color(1f, .745f, .392f);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = 1f - y / (h - 1f);   // 0 at the top
                    var c = Color.Lerp(top, bottom, v);
                    if ((x / square + y / square) % 2 == 1) c = Color.Lerp(c, new Color(1f, .82f, .59f), .035f);
                    float r = new Vector2((x - w / 2f) / (w * .42f), (y - h * .54f) / (w * .42f)).magnitude;
                    c = Color.Lerp(c, glow, .3f * Mathf.Clamp01(1f - r));
                    if (y % 2 == 0) c = Color.Lerp(c, Color.black, .18f);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            return textures["wall"] = Finish("Menu wall", w, h, px, TextureWrapMode.Clamp);
        }

        // One line in the display font, drawn once into a transparent texture
        // `width` x `height` by a throwaway camera far below the scene (the way
        // PiecePortraits takes its pictures). `repeat` fills the width with copies
        // that tile seamlessly, for a scrolling sign. Null without the font.
        public static RenderTexture Caption(string text, int width, int height, Color color, bool repeat)
        {
            var font = RuntimePanels.DisplayFont;
            if (font == null || string.IsNullOrEmpty(text)) return null;
            string key = text + width + "x" + height + ColorUtility.ToHtmlStringRGB(color) + repeat;
            if (captions.TryGetValue(key, out var cached) && cached != null && cached.IsCreated()) return cached;
            var studio = new Vector3(0f, -900f, 0f);
            var go = new GameObject("Menu Caption");
            go.transform.position = studio;
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.fontSize = 96;
            mesh.characterSize = .1f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            mesh.text = text;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            float aspect = width / (float)height;
            var size = renderer.bounds.size;
            // Where the glyphs sit against the line box; the same for any copy count.
            float rise = renderer.bounds.center.y - studio.y;
            if (size.x <= 0 || size.y <= 0) { size = new Vector3(text.Length * .55f, 1f, 0); rise = 0; }
            float viewHeight;
            if (repeat)
            {
                int copies = Mathf.Max(1, Mathf.RoundToInt(size.y * aspect / (.62f * size.x)));
                var line = new System.Text.StringBuilder();
                for (int i = 0; i < copies; i++) line.Append(text);
                mesh.text = line.ToString();
                size = renderer.bounds.size;
                if (size.x <= 0) size.x = copies * text.Length * .55f;
                viewHeight = size.x / aspect;
            }
            else viewHeight = Mathf.Max(size.y / .78f, size.x / .96f / aspect);

            var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "Menu caption", wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, antiAliasing = 1
            };
            texture.Create();
            var rig = new GameObject("Menu Caption Camera");
            var camera = rig.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = viewHeight / 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(color.r, color.g, color.b, 0f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 20f;
            camera.allowHDR = false;
            camera.targetTexture = texture;
            rig.transform.position = studio + new Vector3(0, rise, -5f);
            rig.transform.rotation = Quaternion.identity;
            camera.Render();
            camera.targetTexture = null;
            go.SetActive(false);
            Object.Destroy(rig);
            Object.Destroy(go);
            return captions[key] = texture;
        }

        static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return texture;
        }

        // ---------- meshes ----------

        // An open cone one unit tall round Y, narrow at the top; v runs 0 (bottom) to 1 (top).
        static Mesh BeamMesh(float radius)
        {
            string key = "beam" + radius;
            if (meshes.TryGetValue(key, out var cached) && cached != null) return cached;
            const int segments = 40;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int j = 0; j <= segments; j++)
            {
                float a = Mathf.PI * 2 * j / segments, c = Mathf.Cos(a), s = Mathf.Sin(a);
                v.Add(new Vector3(c * radius, -.5f, s * radius)); uv.Add(new Vector2((float)j / segments, 0));
                v.Add(new Vector3(c * .06f, .5f, s * .06f)); uv.Add(new Vector2((float)j / segments, 1));
            }
            for (int j = 0; j < segments; j++)
            {
                int a = j * 2;
                t.Add(a); t.Add(a + 1); t.Add(a + 2);
                t.Add(a + 2); t.Add(a + 1); t.Add(a + 3);
            }
            var mesh = new Mesh { name = "Menu beam" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return meshes[key] = mesh;
        }

        // A ring tube lying flat (in XZ) of radius `major`, tube radius `minor`.
        public static Mesh Torus(float major, float minor, int segments = 72, int sides = 8)
        {
            string key = "torus" + major + "_" + minor + "_" + segments;
            if (meshes.TryGetValue(key, out var cached) && cached != null) return cached;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.PI * 2 * i / segments;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                for (int j = 0; j <= sides; j++)
                {
                    float b = Mathf.PI * 2 * j / sides;
                    var normal = dir * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    v.Add(dir * major + normal * minor);
                    n.Add(normal);
                }
            }
            int row = sides + 1;
            for (int i = 0; i < segments; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = i * row + j, b = a + row;
                    t.Add(a); t.Add(a + 1); t.Add(b);
                    t.Add(b); t.Add(a + 1); t.Add(b + 1);
                }
            var mesh = new Mesh { name = "Menu torus" };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return meshes[key] = mesh;
        }

        // A flat disc facing up with planar UVs (0..1 across it), for a round board top.
        public static Mesh Disc(float radius, int segments = 96)
        {
            string key = "disc" + radius;
            if (meshes.TryGetValue(key, out var cached) && cached != null) return cached;
            var v = new List<Vector3> { Vector3.zero }; var uv = new List<Vector2> { new Vector2(.5f, .5f) }; var t = new List<int>();
            for (int j = 0; j <= segments; j++)
            {
                float a = Mathf.PI * 2 * j / segments, c = Mathf.Cos(a), s = Mathf.Sin(a);
                v.Add(new Vector3(c * radius, 0, s * radius));
                uv.Add(new Vector2(.5f + c * .5f, .5f + s * .5f));
                if (j > 0) { t.Add(0); t.Add(j + 1); t.Add(j); }
            }
            var mesh = new Mesh { name = "Menu disc" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return meshes[key] = mesh;
        }

        // A box with UVs on every face (wood on the sides of a slab).
        public static GameObject Slab(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        // ---------- portraits ----------

        static readonly Dictionary<string, RenderTexture> portraits = new Dictionary<string, RenderTexture>();

        // A PAWN RUSH figure in a pose on a transparent square, lit by its own
        // three lights (a warm studio, as on the samples' cards), rendered once far
        // below the scene. `head` frames the face only, for avatars. `turn` is where
        // the figure looks, in sample coordinates around its feet.
        public static RenderTexture Portrait(string key, PieceKind kind, int team, FigurePose pose, Vector2 turn, bool head, float distance = 6.2f, float lookY = 1.05f)
        {
            key = key + kind + team + head;
            if (portraits.TryGetValue(key, out var cached) && cached != null && cached.IsCreated()) return cached;
            int size = head ? 128 : 256;
            var texture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Menu portrait " + key };
            texture.Create();

            var studio = new GameObject("Menu Portrait");
            studio.transform.position = new Vector3(0f, -1200f, 0f);
            var figure = LastSceneFigure.Build(kind, team, studio.transform);
            figure.Place(Vector3.zero, Web(turn.x, 0, turn.y));
            figure.Pose(pose);
            foreach (var part in figure.Root.GetComponentsInChildren<Renderer>())
            {
                part.shadowCastingMode = ShadowCastingMode.Off;
                part.receiveShadows = false;
            }
            var lights = new[]
            {
                StudioLight(studio.transform, Web(-3, 5, 6), 0xFFEFD8, 1.25f),
                StudioLight(studio.transform, Web(4, 3, -4), 0xFFD9A8, .8f),
                StudioLight(studio.transform, Web(0, -2, 5), 0xE8D9C0, .35f)
            };

            var rig = new GameObject("Menu Portrait Camera");
            rig.transform.SetParent(studio.transform, false);
            var camera = rig.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.2f, .12f, .07f, 0f);
            camera.fieldOfView = 28f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 30f;
            camera.allowHDR = false;
            camera.targetTexture = texture;
            if (head) { rig.transform.localPosition = Web(0, 1.55f, 3.1f); rig.transform.LookAt(studio.transform.position + Vector3.up * 1.5f); }
            else { rig.transform.localPosition = Web(0, 1.25f, distance); rig.transform.LookAt(studio.transform.position + Vector3.up * lookY); }
            var ambient = RenderSettings.ambientMode;
            Color sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor;
            bool fog = RenderSettings.fog;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex(0xFFF0DD) * .55f;
            RenderSettings.ambientEquatorColor = Hex(0xE8D9C0) * .45f;
            RenderSettings.ambientGroundColor = Hex(0xE8D9C0) * .35f;
            RenderSettings.fog = false;
            camera.Render();
            RenderSettings.ambientMode = ambient;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.fog = fog;
            camera.targetTexture = null;
            foreach (var light in lights) light.enabled = false;
            studio.SetActive(false);
            Object.Destroy(studio);
            return portraits[key] = texture;
        }

        static Light StudioLight(Transform parent, Vector3 from, int rgb, float intensity)
        {
            var go = new GameObject("Studio Light");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.LookRotation(-from.normalized);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Hex(rgb);
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            return light;
        }

        // ---------- figures ----------

        // Web samples' coordinates have +Z towards the camera; Unity's menus put the
        // camera at -Z. These take the samples' numbers as they are.
        public static Vector3 Web(float x, float y, float z) => new Vector3(x, y, -z);

        public static LastSceneFigure Figure(Transform parent, PieceKind kind, int team, Vector3 at, Vector3 lookAt, float scale = 1f)
        {
            var figure = LastSceneFigure.Build(kind, team, parent);
            figure.Place(at, lookAt);
            if (scale != 1f) figure.Root.transform.localScale = Vector3.one * scale;
            return figure;
        }

        // The samples' idle bounce, run cycle and cheer.
        public static void Idle(LastSceneFigure f, float t, int i, float amp)
        {
            float b = Mathf.Max(0, Mathf.Sin(t * 2.4f + i * 1.3f));
            f.Pose(new FigurePose { Jump = b * .08f * amp, Squash = -.04f * b, Arm = .25f + .2f * Mathf.Sin(t * 2.4f + i) });
        }

        public static FigurePose Run(float t, int i)
        {
            float ph = t * 9f + i * 1.7f, s = Mathf.Sin(ph);
            return new FigurePose { Jump = Mathf.Abs(s) * .18f, Leg = s * .75f, ArmLeft = .5f + s * .6f, ArmRight = .5f - s * .6f, Lean = .2f };
        }

        public static void Cheer(LastSceneFigure f, float t, int i)
        {
            float b = Mathf.Max(0, Mathf.Sin(t * 3.2f + i * 1.1f));
            f.Pose(new FigurePose { Jump = b * .22f, Squash = -.06f * b, ArmLeft = 1.6f + .9f * b, ArmRight = 1.6f + .9f * b });
        }
    }
}
