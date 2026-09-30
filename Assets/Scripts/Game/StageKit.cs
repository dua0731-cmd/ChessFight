using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    // The shared look of the menu backdrops (title screen and lobby): a pale sky
    // over an endless white tile plaza, soft sun, hazy distance, and a raised
    // chessboard to stand the pieces on. Built from code, so neither scene file
    // changes.
    public static class StageKit
    {
        static readonly Color SkyTop = PieceFigure.Hex(0x6DAEEA), SkyMid = PieceFigure.Hex(0xB5D7F5);
        static readonly Color Haze = PieceFigure.Hex(0xDCEBF9), SkyLow = PieceFigure.Hex(0xEAF3FC);
        static readonly Color TileLight = PieceFigure.Hex(0xEFE6D6), TileDark = PieceFigure.Hex(0x565D6E);
        static readonly Color Slab = PieceFigure.Hex(0xDDE1E8);

        static Material skybox, floor;

        // Sky, haze, ambient light and a sun with a weak fill, for `view`.
        public static void Environment(Transform parent, Camera view, float hazeStart, float hazeEnd)
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
                view.backgroundColor = SkyMid;
            }
            view.farClipPlane = 400f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Haze;
            RenderSettings.fogStartDistance = hazeStart;
            RenderSettings.fogEndDistance = hazeEnd;

            // A hemisphere light: white from above, cool grey bounce from below.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.78f, .80f, .84f);
            RenderSettings.ambientEquatorColor = new Color(.68f, .71f, .76f);
            RenderSettings.ambientGroundColor = new Color(.52f, .55f, .60f);

            var sun = AddLight(parent, "Sun", new Vector3(7f, -13f, 9f), Color.white, 1.05f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .55f;
            sun.shadowBias = .03f;
            sun.shadowNormalBias = .3f;
            AddLight(parent, "Fill", new Vector3(-6f, -4f, 6f), new Color(1f, .945f, .894f), .3f);
        }

        static Light AddLight(Transform parent, string name, Vector3 direction, Color color, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.LookRotation(direction.normalized);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        // The plaza: white tiles with grey grout, two units a tile, fading into the haze.
        public static void Floor(Transform parent)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Plaza";
            Object.Destroy(plane.GetComponent<Collider>());
            plane.transform.SetParent(parent, false);
            plane.transform.localScale = new Vector3(60f, 1f, 60f);   // 600 x 600
            var renderer = plane.GetComponent<Renderer>();
            renderer.sharedMaterial = FloorMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // A raised chessboard on a slab (pale unless `slab` says otherwise).
        // Returns the height of its top face, where pieces stand.
        public static float Board(Transform parent, string name, int columns, int rows, float tile, Vector3 center, float height, float border,
                                  Color? slab = null)
        {
            var board = new GameObject(name);
            board.transform.SetParent(parent, false);
            board.transform.localPosition = center;
            Box(board.transform, "Slab", new Vector3(0, height / 2 - .01f, 0),
                new Vector3(columns * tile + border, height, rows * tile + border), PieceFigure.Lit(slab ?? Slab, .08f));
            Material light = PieceFigure.Lit(TileLight, .1f), dark = PieceFigure.Lit(TileDark, .1f);
            for (int i = 0; i < columns; i++)
                for (int j = 0; j < rows; j++)
                    Box(board.transform, "Tile", new Vector3((i - (columns - 1) / 2f) * tile, height + .02f, (j - (rows - 1) / 2f) * tile),
                        new Vector3(tile, .06f, tile), (i + j) % 2 == 1 ? dark : light);
            return center.y + height + .05f;
        }

        // The project renders in linear space, where a dark overlay blended at
        // alpha a darkens far less than the same a does in a picture editor. This
        // returns the alpha that darkens a bright sky as much as `a` looks like it should.
        public static float Darkening(float a)
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear) return a;
            const float sky = .95f;
            return 1f - Mathf.GammaToLinearSpace((1f - a) * sky) / Mathf.GammaToLinearSpace(sky);
        }

        public static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = PieceFigure.Cube();
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        // ---------- generated textures ----------

        // Skybox/6 Sided with one vertical gradient on the four sides. The menus
        // only ever look slightly up, so the sides are all that shows.
        static Material Sky()
        {
            if (skybox != null) return skybox;
            var shader = Shader.Find("Skybox/6 Sided");
            if (shader == null) return null;
            var side = new Texture2D(4, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Menu Sky Side" };
            for (int y = 0; y < 256; y++)
            {
                // v .5 is the horizon; the gradient climbs from haze to deep blue.
                float up = Mathf.Clamp01((y / 255f - .5f) * 2f);
                Color c = y < 128 ? SkyLow : up < .18f ? Color.Lerp(Haze, SkyMid, up / .18f) : Color.Lerp(SkyMid, SkyTop, (up - .18f) / .82f);
                for (int x = 0; x < 4; x++) side.SetPixel(x, y, c);
            }
            side.Apply();
            skybox = new Material(shader) { name = "Menu Sky" };
            foreach (string face in new[] { "_FrontTex", "_BackTex", "_LeftTex", "_RightTex" }) skybox.SetTexture(face, side);
            skybox.SetTexture("_UpTex", Solid(SkyTop));
            skybox.SetTexture("_DownTex", Solid(SkyLow));
            return skybox;
        }

        static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return texture;
        }

        static Material FloorMaterial()
        {
            if (floor != null) return floor;
            const int size = 256, grout = 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8, name = "Plaza Tile"
            };
            Color fill = PieceFigure.Hex(0xF4F5F7), line = PieceFigure.Hex(0xCDD1D8);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = x < grout || y < grout ? line : fill;
            texture.SetPixels(pixels);
            texture.Apply(true);
            floor = new Material(PieceFigure.Lit(Color.white, .05f)) { name = "Plaza", mainTexture = texture };
            floor.color = Color.white;
            floor.mainTextureScale = new Vector2(300f, 300f);
            return floor;
        }
    }
}
