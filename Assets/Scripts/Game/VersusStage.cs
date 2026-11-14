using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    // R625, second look (11-13; the user on the first, flat panels with a drawn pawn icon: "뭔가 사진으로 그냥 가져다 붙여
    // 놓은 거 같은 느낌이야 퀄리티가 부족해"): the versus scene's 3D stage. The players' own pawns (the match's model and
    // colours, LastSceneFigure.WearPawnRushLook) stand in one row on a maple and walnut board, white's turned toward black's,
    // under a warm key light that throws their shadows on the board, each with a rim light in its team's colour behind it
    // (cool for white, amber for black). One camera draws the row into Texture, laid out as the versus scene's 1280 x 720
    // stage: Stand puts a pawn's feet at a stage point, so each slanted panel shows its own player through its part of the
    // picture (LoadingScreenView.Versus.cs), and the whole picture, dimmed, fills the screen behind the panels.
    //
    // Like LoadingStudio it lives far below the scenes and draws only when asked (Render), with its own light, haze and
    // ambient swapped in around the draw and the scene's directional lights switched off for it.
    public sealed class VersusStage
    {
        public const int PerTeam = 6;
        static readonly Vector3 Origin = new Vector3(0f, -1500f, 0f);
        const float StageWide = 1280f, StageTall = 720f, Fov = 24f;
        // A pawn this many stage pixels tall at scale 1 (the camera's distance follows the model's height): with its arms
        // down it is about as wide as a six-a-side panel's face (82 px), so each panel shows its whole player and none of
        // the next.
        public const float PawnPixels = 160f;
        // The camera's eye and aim heights, in pawn heights.
        const float EyeHeight = .7f, AimHeight = .5f;
        static readonly Color Clear = new Color(.043f, .031f, .024f);
        // The rim behind each pawn, stronger behind black's (their dark slate would sink into the dark otherwise).
        static readonly Color RimWhite = new Color(.72f, .82f, 1f), RimBlack = new Color(1f, .68f, .32f);
        static readonly float[] RimIntensity = { 6f, 13f }, PoolIntensity = { 7f, 10.5f };
        const float RimRange = 2.1f;
        // A warm spot over each pawn: a pool of light on the board around its feet and light on its head.
        static readonly Color PoolColor = new Color(1f, .87f, .66f);
        const float PoolRange = 6f, PoolAngle = 38f;
        // The pawn's height as it shows, over the renderers' bounds (the skinned model stands a little taller than its
        // bounds say): names clear its head by it (HeadTop).
        const float ShownOverBounds = 1.16f;
        public const float ShownPawnPixels = PawnPixels * ShownOverBounds;

        public RenderTexture Texture { get; private set; }
        public bool Built => root != null;

        GameObject root;
        Camera camera;
        Light key;
        readonly LastSceneFigure[] figures = new LastSceneFigure[PerTeam * 2];
        readonly Light[] rims = new Light[PerTeam * 2], pools = new Light[PerTeam * 2];
        readonly List<Light> hidden = new List<Light>();
        Texture2D boardTexture;
        Material boardMaterial;
        float pawnHeight = 2f;
        int sceneHandle = -1;

        public void Build(Transform parent, int width, int height)
        {
            root = new GameObject("Versus Stage");
            root.transform.SetParent(parent, false);
            root.transform.position = Origin;
            var t = root.transform;

            // the players: built now (while matching) so the start does not wait for twelve models
            for (int team = 0; team < 2; team++)
                for (int i = 0; i < PerTeam; i++)
                {
                    var figure = LastSceneFigure.Build(PieceKind.Pawn, team, t);
                    figure.WearPawnRushLook();
                    figure.Root.SetActive(false);
                    figures[team * PerTeam + i] = figure;
                    var rim = new GameObject("Rim").AddComponent<Light>();
                    rim.transform.SetParent(t, false);
                    rim.type = LightType.Point;
                    rim.color = team == 0 ? RimWhite : RimBlack;
                    rim.range = RimRange;
                    rim.intensity = RimIntensity[team];
                    rim.shadows = LightShadows.None;
                    rim.enabled = false;
                    rims[team * PerTeam + i] = rim;
                    var pool = new GameObject("Pool").AddComponent<Light>();
                    pool.transform.SetParent(t, false);
                    pool.type = LightType.Spot;
                    pool.color = PoolColor;
                    pool.range = PoolRange;
                    pool.spotAngle = PoolAngle;
                    pool.innerSpotAngle = PoolAngle * .45f;
                    pool.intensity = PoolIntensity[team];
                    pool.shadows = LightShadows.None;
                    pool.enabled = false;
                    pools[team * PerTeam + i] = pool;
                }
            pawnHeight = Height(figures[0]);

            // the board: maple and walnut squares of one unit, fading into the dark behind
            boardTexture = BoardTexture();
            boardMaterial = LastSceneArt.Textured("versus board", boardTexture, .55f, new Vector2(30f, 30f));
            var board = GameObject.CreatePrimitive(PrimitiveType.Plane);
            board.name = "Board";
            Object.Destroy(board.GetComponent<Collider>());
            board.transform.SetParent(t, false);
            // 60 x 60 units: from far behind the row to under the camera, so the screen's foot is board too
            board.transform.localPosition = Vector3.zero;
            board.transform.localScale = new Vector3(6f, 1f, 6f);
            var boardRenderer = board.GetComponent<MeshRenderer>();
            boardRenderer.sharedMaterial = boardMaterial;
            boardRenderer.shadowCastingMode = ShadowCastingMode.Off;
            boardRenderer.receiveShadows = true;

            // the key: warm, from the upper left in front, with soft shadows
            key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(t, false);
            key.type = LightType.Directional;
            key.color = new Color(1f, .9f, .77f);
            key.intensity = 1.05f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = .8f;
            key.transform.rotation = Quaternion.LookRotation(new Vector3(-.45f, -.75f, -.5f));
            key.enabled = false;

            Texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Versus Stage" };
            Texture.Create();
            var rig = new GameObject("Versus Camera");
            rig.transform.SetParent(t, false);
            camera = rig.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Clear;
            camera.fieldOfView = Fov;
            camera.aspect = StageWide / StageTall;
            camera.nearClipPlane = .5f;
            camera.farClipPlane = 80f;
            camera.allowHDR = false;
            camera.targetTexture = Texture;
            // far enough that a pawn is PawnPixels tall on the 720 px stage
            float distance = pawnHeight * StageTall / (2f * Mathf.Tan(Fov * .5f * Mathf.Deg2Rad) * PawnPixels);
            rig.transform.localPosition = new Vector3(0f, pawnHeight * EyeHeight, distance);
            rig.transform.LookAt(t.TransformPoint(new Vector3(0f, pawnHeight * AimHeight, 0f)));
        }

        // The model's height from its renderers (the camera is placed by it).
        static float Height(LastSceneFigure figure)
        {
            bool was = figure.Root.activeSelf;
            figure.Root.SetActive(true);
            figure.Place(Vector3.zero, Vector3.forward);
            var renderers = figure.Root.GetComponentsInChildren<Renderer>();
            float top = 0f, bottom = 0f;
            bool any = false;
            foreach (var r in renderers)
            {
                var b = r.bounds;
                if (!any) { top = b.max.y; bottom = b.min.y; any = true; }
                else { top = Mathf.Max(top, b.max.y); bottom = Mathf.Min(bottom, b.min.y); }
            }
            figure.Root.SetActive(was);
            float h = top - bottom;
            return any && h > .3f && h < 10f ? h : 2f;
        }

        public LastSceneFigure Figure(int team, int index) => figures[Mathf.Clamp(team, 0, 1) * PerTeam + Mathf.Clamp(index, 0, PerTeam - 1)];

        public void HideAll()
        {
            for (int i = 0; i < figures.Length; i++)
            {
                if (figures[i] != null) figures[i].Root.SetActive(false);
                if (rims[i] != null) rims[i].gameObject.SetActive(false);
                if (pools[i] != null) pools[i].gameObject.SetActive(false);
            }
        }

        // Stands a team's `index`-th pawn with its feet on the board at stage point `feet` (pixels of the 1280 x 720
        // stage, y down), turned `yaw` (radians) from facing the camera, `scale` times its size.
        public void Stand(int team, int index, Vector2 feet, float yaw, float scale)
        {
            if (camera == null) return;
            int k = Mathf.Clamp(team, 0, 1) * PerTeam + Mathf.Clamp(index, 0, PerTeam - 1);
            var figure = figures[k];
            if (figure == null) return;
            var ray = camera.ViewportPointToRay(new Vector3(feet.x / StageWide, 1f - feet.y / StageTall, 0f));
            var t = root.transform;
            // where the ray meets the board (y = 0 in the stage's space)
            Vector3 from = t.InverseTransformPoint(ray.origin), along = t.InverseTransformDirection(ray.direction);
            float s = along.y < -1e-4f ? -from.y / along.y : 0f;
            var spot = from + along * s;
            figure.Root.SetActive(true);
            figure.Root.transform.localScale = Vector3.one * scale;
            var eye = t.InverseTransformPoint(camera.transform.position);
            figure.Place(spot, new Vector3(eye.x, spot.y, eye.z));
            figure.Pose(new FigurePose { Yaw = yaw });
            var rim = rims[k];
            rim.gameObject.SetActive(true);
            // behind the pawn, a little to the side it turns from, at its head's height
            rim.transform.localPosition = spot + new Vector3(-Mathf.Sign(yaw) * .3f * scale, pawnHeight * 1.05f * scale, -.75f * scale);
            rim.range = RimRange * scale;
            // high over the pawn, a little in front, pointing at its feet
            var pool = pools[k];
            pool.gameObject.SetActive(true);
            pool.transform.localPosition = spot + new Vector3(0f, pawnHeight * 2.3f * scale, .5f * scale);
            pool.transform.LookAt(root.transform.TransformPoint(spot));
            pool.range = PoolRange * scale;
        }

        public void Pose(int team, int index, in FigurePose pose) => Figure(team, index).Pose(pose);

        // Draws one frame into Texture with the stage's own light, haze and ambient, then restores the scene's.
        public void Render()
        {
            if (camera == null || Texture == null) return;
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            if (active != sceneHandle)
            {
                sceneHandle = active;
                hidden.Clear();
                foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.type == LightType.Directional && !light.transform.IsChildOf(root.transform)) hidden.Add(light);
            }
            bool fog = RenderSettings.fog;
            var fogMode = RenderSettings.fogMode;
            var fogColor = RenderSettings.fogColor;
            float fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance;
            var ambientMode = RenderSettings.ambientMode;
            Color sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor;
            var ambientLight = RenderSettings.ambientLight;
            float reflection = RenderSettings.reflectionIntensity;
            var on = new List<Light>();
            foreach (var light in hidden)
                if (light != null && light.enabled) { light.enabled = false; on.Add(light); }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Clear;
            float distance = camera.transform.localPosition.z;
            RenderSettings.fogStartDistance = distance + 2f;
            RenderSettings.fogEndDistance = distance + 20f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.30f, .25f, .21f);
            RenderSettings.ambientEquatorColor = new Color(.17f, .13f, .10f);
            RenderSettings.ambientGroundColor = new Color(.05f, .04f, .03f);
            RenderSettings.reflectionIntensity = .35f;
            key.enabled = true;
            foreach (var rim in rims) if (rim != null && rim.gameObject.activeSelf) rim.enabled = true;
            foreach (var pool in pools) if (pool != null && pool.gameObject.activeSelf) pool.enabled = true;
            camera.Render();
            foreach (var pool in pools) if (pool != null) pool.enabled = false;
            foreach (var rim in rims) if (rim != null) rim.enabled = false;
            key.enabled = false;

            foreach (var light in on) light.enabled = true;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            if (ambientMode == AmbientMode.Flat) RenderSettings.ambientLight = ambientLight;
            RenderSettings.reflectionIntensity = reflection;
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root);
            root = null;
            camera = null;
            if (Texture != null) { Texture.Release(); Object.Destroy(Texture); Texture = null; }
            if (boardTexture != null) Object.Destroy(boardTexture);
            boardTexture = null;
        }

        // Two squares of maple and walnut a side, each with a fine seam and a faint grain, for a board tiled under the row.
        static Texture2D BoardTexture()
        {
            const int size = 256, half = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8, name = "Versus board"
            };
            Color maple = LastSceneArt.Hex(0xC9A57A), walnut = LastSceneArt.Hex(0x4E3220), seam = LastSceneArt.Hex(0x241710);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int cx = x / half, cy = y / half, lx = x % half, ly = y % half;
                    bool light = (cx + cy) % 2 == 0;
                    Color c = light ? maple : walnut;
                    // grain: soft streaks along the square, turned square by square like an inlaid board
                    float along = light ? lx : ly, across = light ? ly : lx;
                    float grain = Mathf.Sin(across * .35f + Mathf.Sin(along * .05f + across * .02f) * 2.2f) * .5f + .5f;
                    c = Color.Lerp(c, c * .82f, grain * .35f);
                    if (lx < 2 || ly < 2 || lx > half - 3 || ly > half - 3) c = Color.Lerp(c, seam, .6f);
                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }
    }
}
