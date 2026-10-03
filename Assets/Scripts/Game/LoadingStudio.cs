using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
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
    public sealed class LoadingStudio
    {
        public const int PerTeam = 6;
        const float StageRadius = 5.6f, StageHeight = .5f, Walk = 6.4f, Hop = .45f, Stagger = .25f;
        static readonly Vector3 Origin = new Vector3(0f, -600f, 0f);
        // Each team's spots (white; black mirrored), front row first.
        static readonly Vector2[] Spots =
            { new Vector2(-1.05f, 1.7f), new Vector2(-2.1f, 1.7f), new Vector2(-3.15f, 1.7f), new Vector2(-1.55f, -1.2f), new Vector2(-2.6f, -1.2f), new Vector2(-3.65f, -1.2f) };
        // Who stands on each spot, as in the lobby lineup; bots are pawns.
        public static readonly PieceKind[] Kinds =
            { PieceKind.Knight, PieceKind.Queen, PieceKind.King, PieceKind.Rook, PieceKind.Bishop, PieceKind.Pawn };

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
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 90f;
            camera.allowHDR = false;
            camera.targetTexture = Texture;
            Aim(0f);

            MenuArt.Floor(t);
            // The stage: mahogany sides, the board on top, a brass rim and an LED ring.
            LastSceneArt.Part(t, "Stage", LastSceneArt.Cylinder(StageRadius + .15f, StageRadius, StageHeight, 96),
                              MenuArt.Wood(MenuArt.Mahogany, .6f, new Vector2(6f, 1f)), new Vector3(0, StageHeight / 2, 0));
            LastSceneArt.Part(t, "Stage Top", MenuArt.Disc(StageRadius), MenuArt.Board(8), new Vector3(0, StageHeight + .004f, 0), null, null, false)
                        .GetComponent<MeshRenderer>().receiveShadows = true;
            LastSceneArt.Part(t, "Stage Rim", MenuArt.Torus(StageRadius + .02f, .06f, 128, 10), MenuArt.BrassMetal(), new Vector3(0, StageHeight, 0));
            LastSceneArt.Part(t, "Stage Light", MenuArt.Torus(StageRadius + .2f, .04f, 128), MenuArt.Lamp(MenuArt.Led), new Vector3(0, .05f, 0), null, null, false);
            MenuArt.Slab(t, "Centre Line", new Vector3(0, StageHeight + .01f, 0), new Vector3(.05f, .012f, StageRadius * 1.85f), MenuArt.Lamp(0xFFE2A8))
                   .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            MenuArt.Spot(t, MenuArt.Web(0, 12, 6), MenuArt.Web(0, .5f, 0), 0xFFE6C2, 1.9f, .42f, .6f, true);
            MenuArt.Spot(t, MenuArt.Web(-9, 7, 4), MenuArt.Web(-2.4f, .5f, .6f), MenuArt.Cyan, 2f, .4f, .6f);
            MenuArt.Spot(t, MenuArt.Web(9, 7, 4), MenuArt.Web(2.4f, .5f, .6f), MenuArt.Red, 2f, .4f, .6f);
            MenuArt.Spot(t, MenuArt.Web(0, 8, -8), MenuArt.Web(0, 1.5f, 0), 0xFFE2B8, 1.4f, .5f, .6f);
            MenuArt.Beam(t, MenuArt.Web(-10, 0, -12), MenuArt.Web(-1, 12, -3), MenuArt.Cyan, 2.2f, .06f);
            MenuArt.Beam(t, MenuArt.Web(10, 0, -12), MenuArt.Web(1, 12, -3), MenuArt.Red, 2.2f, .06f);
            MenuArt.Beam(t, MenuArt.Web(0, 14, 2), MenuArt.Web(0, StageHeight, .4f), 0xFFE8C8, 3.4f, .06f);
            MenuArt.Halo(t, MenuArt.Web(-10, .3f, -12), MenuArt.Cyan, 3f, .8f, camera);
            MenuArt.Halo(t, MenuArt.Web(10, .3f, -12), MenuArt.Red, 3f, .8f, camera);

            // Every spot glows faintly in its team colour, waiting for its player.
            for (int team = 0; team < 2; team++)
                for (int spot = 0; spot < PerTeam; spot++)
                {
                    var p = new Player { Team = team, Spot = spot, Side = team == 0 ? 1f : -1f };
                    var s = Spots[spot];
                    float x = team == 0 ? s.x : -s.x;
                    p.At = MenuArt.Web(x, StageHeight, s.y);
                    p.StartX = p.Side * -10.5f;
                    int color = team == 0 ? MenuArt.Cyan : MenuArt.Red, light = team == 0 ? 0x9BE8FF : 0xFF9AA6;
                    var disc = MenuArt.FloorGlow(t, p.At + Vector3.up * .012f, color, .52f, .22f);
                    p.Disc = disc.GetComponent<Renderer>().sharedMaterial;
                    p.RingT = LastSceneArt.Part(t, "Spot Ring", MenuArt.Torus(.5f, .035f, 48), MenuArt.Glow("ring", null, MenuArt.Hex(light), false),
                                                p.At + Vector3.up * .02f, null, null, false).transform;
                    p.Ring = p.RingT.GetComponent<Renderer>().sharedMaterial;
                    p.FlashT = MenuArt.Halo(t, p.At + Vector3.up * .9f, light, 1f, 0f, camera);
                    p.Flash = p.FlashT.GetComponent<Renderer>().sharedMaterial;
                    SetAlpha(p.Ring, 0);
                    players[team * PerTeam + spot] = p;
                }
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
                if (p.Figure == null)
                {
                    p.Bot = bot;
                    var kind = bot ? PieceKind.Pawn : Kinds[spot];
                    // Facing the camera, turned towards the middle.
                    var s = Spots[spot];
                    float x = team == 0 ? s.x : -s.x;
                    var look = MenuArt.Web(x + 3 * p.Side, StageHeight, s.y + 6);
                    p.Figure = MenuArt.Figure(root.transform, kind, team, p.At, look, 1.05f);
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
            Aim(now);
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p.Figure == null || p.EnterAt < 0)
                {
                    SetAlpha(p.Disc, .18f + .06f * Mathf.Sin(now * 3 + i));
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
                    SetAlpha(p.Disc, .2f + .25f * (age / walk));
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
                SetAlpha(p.Disc, .55f + .1f * Mathf.Sin(now * 3 + i));
            }
        }

        void Aim(float now)
        {
            if (camera == null) return;
            camera.transform.localPosition = MenuArt.Web(Mathf.Sin(now * .18f) * .5f, 5.6f, 15.5f);
            camera.transform.LookAt(Origin + MenuArt.Web(0, 1.4f, 0));
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
            camera.Render();

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
