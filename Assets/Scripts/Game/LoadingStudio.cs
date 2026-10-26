using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    // 10-18 (승규 님, background B "성문 앞 광장"): behind the stage now stands the chess castle rendered in
    // Blender (ArtSource/Lobby/GateCastle, Resources/Loading/GateCastle.png) on a card that fills the camera's
    // view; the wooden hall's floor, the coloured light shafts and their glows are gone.
    //
    // The entrance screen's moving picture (design C revised, 2026-10-03): a round
    // maple and walnut stage in the wooden hall where each player walks in from
    // the side as they join the match, hops onto their spot with a flash in the
    // team colour, and waits; when everyone is in and ready they all cheer. White comes
    // from the left (cyan light), black from the right (red light). Numbers are
    // the design sample's (MenuArt.Web).
    //
    // It lives under the loading screen's persistent object, far below the
    // scenes, and its own camera draws it into a texture every frame. The match
    // scene's haze, ambient light and sun would change it as it loads, so Render
    // swaps the hall's settings in around its own draw and puts them back.
    public sealed partial class LoadingStudio
    {
        public const int PerTeam = 6;
        const float StageRadius = 5.6f, StageHeight = .5f, Walk = 6.4f, Hop = .45f, Stagger = .25f;
        static readonly Vector3 Origin = new Vector3(0f, -600f, 0f);
        // Each team's spots (white; black mirrored), front row first.
        static readonly Vector2[] Spots =
            { new Vector2(-1.05f, 1.7f), new Vector2(-2.1f, 1.7f), new Vector2(-3.15f, 1.7f), new Vector2(-1.55f, -1.2f), new Vector2(-2.6f, -1.2f), new Vector2(-3.65f, -1.2f) };
        // Who stands on each spot: a pawn, as everyone starts the match (R148, as in the lobby lineup).
        public static readonly PieceKind[] Kinds =
            { PieceKind.Pawn, PieceKind.Pawn, PieceKind.Pawn, PieceKind.Pawn, PieceKind.Pawn, PieceKind.Pawn };

        sealed class Player
        {
            public LastSceneFigure Figure;
            public int Team, Spot;
            public float Side, SpotYaw, StartX;
            public Vector3 At;
            public float EnterAt = -1;   // when the walk began, studio time
            public bool Queued, Bot;
            public Material Disc, Ring, Flash;
            public Transform RingT, FlashT;
        }

        public RenderTexture Texture { get; private set; }
        // Everyone listed has walked on and landed.
        public bool AllIn { get; private set; }
        // Everyone is here and ready: the whole stage cheers.
        public bool Cheer { get; set; }

        readonly Player[] players = new Player[PerTeam * 2];
        readonly Queue<Player> waiting = new Queue<Player>();
        readonly List<Light> hidden = new List<Light>();
        GameObject root;
        Camera camera;
        float lastStart = -10f;
        int sceneHandle = -1;

        public void Build(Transform parent, int width, int height)
        {
            root = new GameObject("Loading Studio");
            root.transform.SetParent(parent, false);
            root.transform.position = Origin;
            var t = root.transform;

            Texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Loading Studio" };
            Texture.Create();
            var rig = new GameObject("Loading Studio Camera");
            rig.transform.SetParent(t, false);
            camera = rig.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = MenuArt.Hex(MenuArt.HallColor);
            camera.fieldOfView = 30f;
            // 10-22 (승규 님: "체스판을 아예 빼주고 돌계단 쪽에서"): with the castle picture the camera is the Blender
            // render's own (at (0, -18, 5) looking at (0, 46.5, 23), 72 degrees across a 16:9 frame) and the players
            // stand on the stone steps in it; the round stage and its coloured lights are gone.
            // 10-22: the castle backdrop was dropped (승규 님 chose background A, the study); the stair path stays unused.
            onStairs = false;
            if (onStairs)
            {
                float aspect = width / Mathf.Max(1f, height), across = Mathf.Tan(36f * Mathf.Deg2Rad);
                camera.fieldOfView = 2f * Mathf.Atan(aspect >= 16f / 9f ? across / aspect : across * 9f / 16f) * Mathf.Rad2Deg;
            }
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 90f;
            camera.allowHDR = false;
            camera.targetTexture = Texture;
            Aim(0f);

            camera.backgroundColor = MenuArt.Hex(0x0E0906);
            if (onStairs) StairLights(t);
            else Study(t);

            // Every spot glows faintly in its team colour, waiting for its player.
            for (int team = 0; team < 2; team++)
                for (int spot = 0; spot < PerTeam; spot++)
                {
                    var p = new Player { Team = team, Spot = spot, Side = team == 0 ? 1f : -1f };
                    players[team * PerTeam + spot] = p;
                    // 10-26 (승규 님: "플레이어가 들어오는 건 넣지마"): nobody walks onto the course model, so no spots
                    if (courseModel) continue;
                    var s = Spots[spot];
                    float x = team == 0 ? s.x : -s.x;
                    p.At = onStairs ? StairSpot(team, spot) : MenuArt.Web(x, StageHeight, s.y);
                    p.StartX = onStairs ? p.Side * -6.6f : p.Side * -10.5f;
                    int color = team == 0 ? MenuArt.Cyan : MenuArt.Red, light = team == 0 ? 0x9BE8FF : 0xFF9AA6;
                    var disc = Blob(t, p.At + Vector3.up * .03f);
                    p.Disc = disc.GetComponent<Renderer>().sharedMaterial;
                    p.RingT = LastSceneArt.Part(t, "Spot Ring", MenuArt.Torus(.5f, .035f, 48), MenuArt.Glow("ring", null, MenuArt.Hex(light), false),
                                                p.At + Vector3.up * .02f, null, null, false).transform;
                    p.Ring = p.RingT.GetComponent<Renderer>().sharedMaterial;
                    p.FlashT = MenuArt.Halo(t, p.At + Vector3.up * .9f, light, 1f, 0f, camera);
                    p.Flash = p.FlashT.GetComponent<Renderer>().sharedMaterial;
                    SetAlpha(p.Ring, 0);
                }
        }


        // Background A (10-22, 승규 님): a dark study at night. A big walnut table, on it a raised wooden chessboard
        // (maple and walnut squares, mahogany frame, brass inlay) the players walk onto, lit from straight above by
        // one brass reading lamp in a warm pool that falls off into the dark; behind, the shelves of books and a
        // window with a little cold moonlight, barely there. No coloured stage lights.
        // 10-25/26: the chessboard and the players walking onto it gave way to the course hologram over its LED plate
        // (LoadingStudio.Course.cs).
        void Study(Transform t)
        {
            var tableWood = MenuArt.Wood(MenuArt.DarkWood, .55f, new Vector2(4f, 2f));
            MenuArt.Slab(t, "Table", new Vector3(0, -.3f, 1f), new Vector3(30f, .6f, 18f), tableWood);
            // 10-25: the course model on its plate instead of the chessboard (LoadingStudio.Course.cs)
            courseModel = true;
            CourseModel(t);
            // the shelves behind: dark wood carcass, rows of books in muted leather colours
            var shelfWood = MenuArt.Wood(MenuArt.DarkWood, .35f, new Vector2(3f, 1f), true);
            var rng = new System.Random(22);
            int[] leather = { 0x4A1E18, 0x2E2A22, 0x5A3A1E, 0x1E2A26, 0x6B4A2A, 0x3A1A22, 0x2A2420 };
            for (int col = -3; col <= 3; col++)
            {
                float cx = col * 3.2f, z = 11f;
                MenuArt.Slab(t, "Shelf Side", new Vector3(cx - 1.55f, 4f, z), new Vector3(.12f, 9f, 1.2f), shelfWood);
                for (int row = 0; row < 6; row++)
                {
                    float y = .4f + row * 1.45f;
                    MenuArt.Slab(t, "Shelf", new Vector3(cx, y, z), new Vector3(3.1f, .08f, 1.2f), shelfWood);
                    float x = cx - 1.45f;
                    while (x < cx + 1.4f)
                    {
                        float w = .12f + (float)rng.NextDouble() * .12f, h = .9f + (float)rng.NextDouble() * .4f;
                        if (rng.NextDouble() < .08) { x += .3f; continue; }
                        MenuArt.Slab(t, "Book", new Vector3(x + w / 2, y + .04f + h / 2, z - .1f), new Vector3(w - .01f, h, .8f),
                                     LastSceneArt.Lit(leather[rng.Next(leather.Length)], .25f));
                        x += w;
                    }
                }
            }
            // a tall window between the shelves with a cold glow
            MenuArt.Slab(t, "Window", new Vector3(0, 5.2f, 11.4f), new Vector3(2.4f, 5.2f, .05f), MenuArt.Glow("study window", null, MenuArt.Hex(0x3A4A6A), false));
            // the reading lamp: a brass shade hanging over the board on a rod, its bulb and the pool of light
            var brass = MenuArt.BrassMetal();
            LastSceneArt.Part(t, "Lamp Rod", LastSceneArt.Cylinder(.03f, .03f, 6f, 12), brass, new Vector3(0, 12.2f, 0), null, null, false);
            LastSceneArt.Part(t, "Lamp Shade", LastSceneArt.Cone(1.2f, 1.0f, 48), LastSceneArt.Lit(0x1E3A2A, .5f, .3f), new Vector3(0, 9.0f, 0), null, null, false);
            LastSceneArt.Part(t, "Lamp Bulb", LastSceneArt.Sphere(.28f), MenuArt.Lamp(0xFFE6B0), new Vector3(0, 8.55f, 0), null, null, false);
            // 10-26: 60 % of what it was, so the course hologram and its LEDs stand out on the plate
            MenuArt.Spot(t, new Vector3(0, 8.5f, 0), new Vector3(0, 0, .3f), 0xFFD9A0, 1.9f, .95f, .8f, true);
            // faint warm bounce from the table and cold moonlight from behind, so the shelves just read
            MenuArt.Spot(t, new Vector3(0, 2f, -12f), new Vector3(0, 2f, 0), 0xC08A5A, .5f, .8f, .9f);
            MenuArt.Spot(t, new Vector3(0, 6f, 10f), new Vector3(0, 1f, -2f), 0x6A86B8, .7f, .7f, .9f);
        }

        Material backdrop, blob;
        bool onStairs;
        Light sun;

        // Blender's castle coordinates (x right, y away from the camera, z up) in the studio's.
        static Vector3 Stair(float x, float y, float z) => new Vector3(x, z, y);

        // The steps the players stand on (build_gate_castle.py: 16 steps of .3 m by 1 m from y 0): white on the
        // left of the middle, black on the right, a front row on the 8th step and a back row on the 11th.
        static Vector3 StairSpot(int team, int spot)
        {
            // on the landing between the two flights (y 16..23, 4.8 m up): clear of the progress row and the cards
            int row = spot < 3 ? 0 : 1;
            float x = (1.6f + (spot % 3) * 1.9f + row * .9f) * (team == 0 ? -1f : 1f);
            return Stair(x, row == 0 ? 17.6f : 20.4f, 4.8f);
        }

        // The render's sun (from the left and a little in front); the sky's light is the ambient.
        void StairLights(Transform t)
        {
            sun = new GameObject("Stair Sun").AddComponent<Light>();
            sun.transform.SetParent(t, false);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.LookRotation(Stair(.55f, .55f, -.62f));
            sun.color = MenuArt.Hex(0xFFEACC);
            sun.intensity = 2.4f;
            sun.shadows = LightShadows.None;
            sun.enabled = false;
        }

        // A soft dark patch under a player on the steps, as if they cast a shadow on the stone.
        Transform Blob(Transform t, Vector3 at)
        {
            if (blob == null)
                blob = new Material(Shader.Find("Sprites/Default")) { name = "Stair contact shadow", mainTexture = MenuArt.RadialTexture(), color = new Color(0, 0, 0, 1), renderQueue = 2400 };
            return LastSceneArt.Part(t, "Contact Shadow", LastSceneArt.Panel(1.3f, 1.0f), new Material(blob), at, Quaternion.Euler(90f, 0, 0), null, false).transform;
        }

        // The castle picture on a card far behind the stage, held by the camera so it always fills the view
        // (covering it, cropped to the picture's own shape). Unlit, drawn first, no fog.
        void Backdrop(Transform rig, int width, int height)
        {
            var image = Resources.Load<Texture2D>("Loading/GateCastle");
            var shader = Shader.Find("Sprites/Default");
            if (image == null || shader == null || camera == null) return;
            backdrop = new Material(shader) { name = "Loading castle", mainTexture = image, renderQueue = 1000 };
            const float distance = 70f;
            float h = 2f * distance * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad), w = h * width / Mathf.Max(1f, height);
            float aspect = image.width / (float)image.height;
            if (w / h > aspect) h = w / aspect; else w = h * aspect;
            LastSceneArt.Part(rig, "Castle Backdrop", LastSceneArt.Panel(w, h), backdrop, new Vector3(0, 0, distance), null, null, false);
        }

        // Hands over who is in the match and who has loaded. Lists are in roster
        // order sorted by team; each team fills its spots in that order.
        public void Sync(IReadOnlyList<int> teams, IReadOnlyList<bool> joined, IReadOnlyList<bool> bots, float now)
        {
            int[] next = { 0, 0 };
            int total = 0, entered = 0;
            var listed = new bool[players.Length];
            for (int i = 0; i < teams.Count; i++)
            {
                int team = teams[i] == 0 ? 0 : 1;
                int spot = next[team]++;
                if (spot >= PerTeam) continue;
                var p = players[team * PerTeam + spot];
                bool bot = i < bots.Count && bots[i];
                bool isIn = i < joined.Count && joined[i];
                // Someone else took this spot (a player left and the list moved up).
                if (p.Figure != null && p.Bot != bot) Reset(p);
                if (!isIn) { if (p.Queued) Reset(p); continue; }
                listed[team * PerTeam + spot] = true;
                total++;
                // nobody walks onto the course model: everyone listed is in at once
                if (courseModel)
                {
                    entered++;
                    continue;
                }
                if (p.Figure == null)
                {
                    p.Bot = bot;
                    var kind = bot ? PieceKind.Pawn : Kinds[spot];
                    // Facing the camera, turned towards the middle.
                    var s = Spots[spot];
                    float x = team == 0 ? s.x : -s.x;
                    var look = onStairs ? p.At + new Vector3(p.Side * 1.6f, 0, -6f) : MenuArt.Web(x + 3 * p.Side, StageHeight, s.y + 6);
                    p.Figure = MenuArt.Figure(root.transform, kind, team, p.At, look, onStairs ? 1.7f : 1.05f);
                    p.SpotYaw = Mathf.Atan2(look.x - p.At.x, look.z - p.At.z);
                    p.Figure.Root.SetActive(false);
                }
                if (!p.Queued) { p.Queued = true; waiting.Enqueue(p); }
                if (p.EnterAt >= 0 && now - p.EnterAt >= WalkTime(p)) entered++;
            }
            // Spots nobody is on any more.
            for (int k = 0; k < players.Length; k++)
                if (!listed[k] && players[k].Queued) Reset(players[k]);
            // One player starts walking at a time, a quarter second apart.
            while (waiting.Count > 0 && !waiting.Peek().Queued) waiting.Dequeue();
            if (waiting.Count > 0 && now - lastStart >= Stagger)
            {
                var p = waiting.Dequeue();
                p.EnterAt = now;
                p.Figure.Root.SetActive(true);
                lastStart = now;
            }
            AllIn = total > 0 && entered == total && waiting.Count == 0;
            routeTarget = Mathf.Clamp01(total / (float)(PerTeam * 2));
        }

        // Clears a spot: the figure goes (a new one is built for whoever comes).
        static void Reset(Player p)
        {
            if (p.Figure != null) Object.Destroy(p.Figure.Root);
            p.Figure = null;
            p.Queued = false;
            p.EnterAt = -1;
            SetAlpha(p.Ring, 0);
            SetAlpha(p.Flash, 0);
        }

        // How long a player is out of sight from the stage edge until the hop ends.
        float WalkTime(Player p) => Mathf.Abs(p.StartX - p.At.x) / Walk + Hop;

        public void Animate(float now)
        {
            TickRoute(Time.unscaledDeltaTime);
            Aim(now);
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p.Figure == null || p.EnterAt < 0)
                {
                    SetAlpha(p.Disc, 0f);
                    continue;
                }
                float age = now - p.EnterAt, walk = Mathf.Abs(p.StartX - p.At.x) / Walk;
                float walkYaw = p.Side * Mathf.PI / 2 - p.SpotYaw;
                if (age < walk)
                {
                    var run = MenuArt.Run(now, i);
                    run.Dx = Mathf.Lerp(p.StartX, p.At.x, age / walk) - p.At.x;
                    run.Yaw = walkYaw;
                    run.Lean = .24f;
                    p.Figure.Pose(run);
                    SetAlpha(p.Disc, .35f * (age / walk));
                    continue;
                }
                float a = age - walk;
                if (a < Hop)
                {
                    float h = Mathf.Sin(Mathf.Min(1, a / .38f) * Mathf.PI), turn = Mathf.SmoothStep(0, 1, a / Hop);
                    p.Figure.Pose(new FigurePose
                    {
                        Jump = h * .55f, Squash = a < .05f ? .12f : a > .36f ? .1f : -.06f, Yaw = walkYaw * (1 - turn),
                        ArmLeft = .3f + 1.8f * h, ArmRight = .3f + 1.8f * h
                    });
                }
                else if (Cheer) MenuArt.Cheer(p.Figure, now, 0);
                else MenuArt.Idle(p.Figure, now, i, .7f);
                SetAlpha(p.Flash, a < .7f ? Mathf.Max(0, .95f - a * 1.35f) : 0);
                p.FlashT.localScale = Vector3.one * (a < .7f ? .5f + a * 4.2f : .01f);
                SetAlpha(p.Ring, a < .8f ? Mathf.Max(0, 1 - a * 1.25f) : 0);
                p.RingT.localScale = Vector3.one * (1 + Mathf.Min(a, .8f) * 2.6f);
                SetAlpha(p.Disc, .45f);
            }
        }

        void Aim(float now)
        {
            if (camera == null) return;
            // 10-22 (승규 님: the board hid the scene): further back and a little higher, so the round stage sits
            // in the lower part of the frame in front of the castle's stair and the castle shows above it.
            if (onStairs)
            {
                camera.transform.localPosition = Stair(0f, -18f, 5f);
                camera.transform.LookAt(Origin + Stair(0f, 46.5f, 23f));
                return;
            }
            camera.transform.localPosition = MenuArt.Web(Mathf.Sin(now * .18f) * .5f, 6.2f, 17.5f);
            camera.transform.LookAt(Origin + MenuArt.Web(0, 1.6f, -1f));
        }

        // Draws one frame into Texture with the hall's settings, then restores the
        // scene's: haze, ambient light, and its suns (switched off for the draw).
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

            MenuArt.HallLighting(24f, 80f);
            if (onStairs)
            {
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = MenuArt.Hex(0xB9CDE6) * 1.0f;
                RenderSettings.ambientEquatorColor = MenuArt.Hex(0xCDBFA6) * .85f;
                RenderSettings.ambientGroundColor = MenuArt.Hex(0x6B5E4E) * .5f;
            }
            if (sun != null) sun.enabled = true;
            camera.Render();
            if (sun != null) sun.enabled = false;

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
            if (backdrop != null) Object.Destroy(backdrop);
            if (blob != null) Object.Destroy(blob);
            DisposeCourse();
            if (root != null) Object.Destroy(root);
            root = null;
            camera = null;
            if (Texture != null) { Texture.Release(); Object.Destroy(Texture); Texture = null; }
        }

        static void SetAlpha(Material material, float a)
        {
            if (material == null) return;
            var c = material.color;
            c.a = a;
            material.color = c;
        }
    }
}
