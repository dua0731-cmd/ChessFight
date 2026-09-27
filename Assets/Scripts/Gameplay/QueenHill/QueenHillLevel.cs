using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // The Queen of the Hill map, "sky palace" (DESIGN §3, 6th revision), as a
    // GRAYBOX: plain boxes in the signal colours of §3.9, built from code when the
    // scene starts, at the real scale (160 m). It exists to measure the climb - does
    // a section take 25-35 s, does the whole tower take about four minutes - before
    // any art is made. All numbers come from QueenHillCourse (Core).
    //
    //   the sea (WaterZone) and a cliff base up to 16 m, the hook terrace
    //   two broken stone bridges at +12 m, white from the south (-Z), black from
    //   the north (+Z); a pawn throws its hook from the broken end (HookStartZone)
    //   eight sections of 18 m. Each team climbs its own half; the black half is
    //   the white half turned 180 degrees. Per section and half:
    //     landing (green garden) -> ride (gold) up 13 m -> exposed ledge (lapis and
    //     gold checker) -> 5 m wall (cream marble; or the S3 launch pad, or a chain)
    //     -> walkway back to the next landing
    //   the spine in the middle: at every landing a ring both teams walk to, the
    //   section's pioneer bell, and the light pillar's cell for that section (a fast
    //   lift, hidden until the bell is rung: Bell, OpenPath, QueenHillMatch)
    //   every landing is a checkpoint (SectionCheckpoint); S4 and S7 join the two
    //   halves all round; S8 is the gate at 160 m with the promotion pedestals
    //
    // Signal colours: green = rest, gold = moves, lapis/gold checker = exposed,
    // cream marble = climbable wall, pale blue light = opened path.
    //
    // Keep the level object at the origin and unrotated: water and hook zones are
    // read as axis-aligned boxes.
    [DefaultExecutionOrder(-500)]
    public sealed class QueenHillLevel : MonoBehaviour
    {
        [Tooltip("Seconds an opened path belongs to the team that opened it.")]
        [SerializeField] float exclusiveSeconds = (float)QueenHillRules.DefaultExclusiveSeconds;
        [Tooltip("Signs on the floor naming each section and its ride.")]
        [SerializeField] bool signs = true;

        public const float CliffHalf = 28f, SeaFloor = -8f;
        public const float BridgeEndZ = -(CliffHalf + QueenHillCourse.BridgeGap);   // -40, white side
        public const float BridgeHalfWidth = 6f, BridgeLength = 60f;
        // Each team's half, in the white frame (black = turned 180 degrees).
        public const float LandingHalfX = 10f, LandingNearZ = -12f, LandingFarZ = -22f;
        public const float SpineHalfX = 7f, SpineHalfZ = 5f, CellHole = 3.6f, CellSize = 3.4f;
        // A section's route, in route coordinates (x' = side * x): ride column, ledge, wall, walkway.
        public const float ColumnX0 = 10f, ColumnX1 = 14f, LedgeX1 = 22f, WallX1 = 26f, WalkwayZ = -15f;

        public static QueenHillLevel Current { get; private set; }

        public QueenHillMatch Match { get; private set; }
        public IReadOnlyList<SectionCheckpoint> Landings => landings;
        public IReadOnlyList<MovingPlatform> Cells => cells;

        readonly List<SectionCheckpoint> landings = new List<SectionCheckpoint>();
        readonly List<MovingPlatform> cells = new List<MovingPlatform>();
        Transform root;
        Material marble, honey, cliff, garden, cypress, gold, checker, lightMat, sea, stone, rock, waterfall, chain, lapis;
        Texture2D checkerTexture;
        readonly List<Object> owned = new List<Object>();

        // The ground point a team's pawn number `slot` (0..5) starts on: the broken end of its bridge.
        public static Vector3 BridgeSpawn(int team, int slot)
        {
            var at = new Vector3(-5f + 2f * Mathf.Clamp(slot, 0, 5), QueenHillCourse.BridgeTop, BridgeEndZ - 4f);
            return team == Teams.Black ? Half(team) * at : at;
        }

        static Quaternion Half(int team) => team == Teams.Black ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

        void Awake()
        {
            Current = this;
            Build();
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            foreach (var o in owned)
                if (o != null) Destroy(o);
        }

        // ---------------------------------------------------------------- building

        void Build()
        {
            if (root != null) return;
            root = new GameObject("Built Level").transform;
            root.SetParent(transform, false);
            MakeMaterials();
            Atmosphere();

            Match = GetComponent<QueenHillMatch>();
            if (Match == null) Match = gameObject.AddComponent<QueenHillMatch>();
            Match.Configure(QueenHillCourse.Sections, exclusiveSeconds);

            BuildSeaAndCliff();
            for (int team = Teams.White; team <= Teams.Black; team++) BuildBridge(team);
            for (int level = 1; level <= QueenHillCourse.Sections; level++) BuildLevel(level);
            for (int section = 1; section <= QueenHillCourse.Sections; section++)
            {
                BuildCell(section);
                for (int team = Teams.White; team <= Teams.Black; team++) BuildRoute(section, Half(team));
            }
            BuildGate();
            BuildScenery();
        }

        void Atmosphere()
        {
            var sky = new Color(0.64f, 0.79f, 0.93f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sky;
            RenderSettings.fogStartDistance = 140f;
            RenderSettings.fogEndDistance = 900f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.66f, 0.66f, 0.7f);
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = sky;
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 1500f);
            }
        }

        void BuildSeaAndCliff()
        {
            // The sea: a surface to look at and the water volume below it.
            Visual("Sea Surface", new Vector3(0f, -0.05f, 0f), new Vector3(1600f, 0.1f, 1600f), sea);
            var water = new GameObject("Sea (WaterZone)");
            water.transform.SetParent(root, false);
            water.transform.localPosition = new Vector3(0f, (SeaFloor + QueenHillCourse.SeaLevel) * 0.5f, 0f);
            var box = water.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1600f, QueenHillCourse.SeaLevel - SeaFloor, 1600f);
            water.AddComponent<WaterZone>();

            // The cliff base: smooth sheer walls up to the hook terrace. No way up but the hook.
            float top = QueenHillCourse.StartTop;
            Solid("Cliff Base (hook terrace on top)", new Vector3(0f, (SeaFloor + top) * 0.5f, 0f),
                new Vector3(CliffHalf * 2f, top - SeaFloor, CliffHalf * 2f), cliff);
            // Waterfalls down the east and west faces (only looks).
            foreach (float x in new[] { -1f, 1f })
                for (int i = 0; i < 3; i++)
                    Visual("Waterfall", new Vector3(x * (CliffHalf + 0.15f), top * 0.5f - 0.5f, -14f + 14f * i),
                        new Vector3(0.3f, top + 1f, 3f + i), waterfall);
            Sign("0 갈고리 착지대 (+16 m)", new Vector3(0f, top + 0.02f, -CliffHalf + 3f), Quaternion.identity, 1.1f);
            Sign("0 갈고리 착지대 (+16 m)", new Vector3(0f, top + 0.02f, CliffHalf - 3f), Quaternion.Euler(0f, 180f, 0f), 1.1f);
        }

        void BuildBridge(int team)
        {
            Quaternion half = Half(team);
            float top = QueenHillCourse.BridgeTop;
            string who = Teams.Name(team) + "팀";
            float far = BridgeEndZ - BridgeLength;
            Solid($"{who} Bridge Deck", half * new Vector3(0f, top - 1f, (BridgeEndZ + far) * 0.5f),
                new Vector3(BridgeHalfWidth * 2f, 2f, BridgeLength), stone, half);
            // The broken end: jagged stubs sticking out over the gap.
            Solid($"{who} Bridge Stub", half * new Vector3(-4.5f, top - 1f, BridgeEndZ + 0.8f), new Vector3(3f, 2f, 1.6f), stone, half);
            Solid($"{who} Bridge Stub", half * new Vector3(4f, top - 1.2f, BridgeEndZ + 0.5f), new Vector3(4f, 1.6f, 1f), stone, half);
            Solid($"{who} Rubble", half * new Vector3(1.5f, top + 0.2f, BridgeEndZ - 1.5f), new Vector3(1.2f, 0.4f, 0.9f), stone, half);
            // Low parapets along the sides, open at the end where the pawns spread out to throw.
            foreach (float x in new[] { -1f, 1f })
                Solid($"{who} Parapet", half * new Vector3(x * (BridgeHalfWidth - 0.3f), top + 0.45f, (BridgeEndZ - 10f + far) * 0.5f),
                    new Vector3(0.6f, 0.9f, BridgeLength - 10f), stone, half);
            // Piers down to the sea floor, and the island the bridge comes from.
            for (int i = 1; i <= 3; i++)
                Solid($"{who} Pier", half * new Vector3(0f, (SeaFloor + top - 2f) * 0.5f, BridgeEndZ - 10f - 16f * i),
                    new Vector3(5f, top - 2f - SeaFloor, 4f), stone, half);
            Solid($"{who} Island", half * new Vector3(0f, (SeaFloor + top) * 0.5f, far - 14f), new Vector3(34f, top - SeaFloor, 28f), rock, half);

            // Where the hook may be thrown from: the last stretch of the bridge.
            var zone = new GameObject($"{who} Hook Start Zone");
            zone.transform.SetParent(root, false);
            zone.transform.localPosition = half * new Vector3(0f, top + 1.5f, BridgeEndZ - 5f);
            var box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(BridgeHalfWidth * 2f + 2f, 4f, 14f);
            zone.AddComponent<HookStartZone>();

            for (int slot = 0; slot < 6; slot++)
            {
                var point = new GameObject($"{who} Spawn {slot}");
                point.transform.SetParent(root, false);
                point.transform.localPosition = BridgeSpawn(team, slot);
                point.transform.localRotation = half;   // facing the tower
                point.AddComponent<SpawnPoint>().Configure(team * 6 + slot, team);
            }
            Sign($"{who} 다리 (+12 m) — E 갈고리, 좌클릭 꾹 게이지, 떼면 던짐. 가장자리는 12 m 앞 4 m 위",
                half * new Vector3(0f, top + 0.02f, BridgeEndZ - 8f), half, 0.55f);
        }

        // The landings of one level (both halves), the spine ring with its bell, the posts
        // under it, and the level's checkpoint.
        void BuildLevel(int level)
        {
            float y = QueenHillCourse.LevelTop(level);
            float below = QueenHillCourse.LevelTop(level - 1);
            var section = QueenHillCourse.Section(level);
            bool joined = section != null && section.JoinedAbove;
            for (int team = Teams.White; team <= Teams.Black; team++)
            {
                Quaternion half = Half(team);
                Span($"L{level} Landing {Teams.Name(team)}", half, -LandingHalfX, LandingHalfX, y - 1.5f, y, LandingFarZ, LandingNearZ, garden);
                if (!joined) Span($"L{level} Walkway {Teams.Name(team)}", half, -1.5f, 1.5f, y - 1f, y, LandingNearZ, -SpineHalfZ, marble);
                foreach (float x in new[] { -1f, 1f })
                    Visual("Cypress", half * new Vector3(x * (LandingHalfX - 1.2f), y + 1.5f, LandingFarZ + 1.2f), new Vector3(0.9f, 3f, 0.9f), cypress, half);
            }
            if (joined) BuildPlaza(level, y);

            // The spine ring, with the hole the cell of this section arrives through.
            float hx = QueenHillCourse.CellX(level), h = CellHole * 0.5f;
            Span($"L{level} Spine West", Quaternion.identity, -SpineHalfX, hx - h, y - 1f, y, -SpineHalfZ, SpineHalfZ, honey);
            Span($"L{level} Spine East", Quaternion.identity, hx + h, SpineHalfX, y - 1f, y, -SpineHalfZ, SpineHalfZ, honey);
            Span($"L{level} Spine South", Quaternion.identity, hx - h, hx + h, y - 1f, y, -SpineHalfZ, -h, honey);
            Span($"L{level} Spine North", Quaternion.identity, hx - h, hx + h, y - 1f, y, h, SpineHalfZ, honey);
            // A rim of light round the hole, so nobody walks into it by surprise.
            foreach (float z in new[] { -h, h })
                Visual("Hole Rim", new Vector3(hx, y + 0.03f, z), new Vector3(CellHole, 0.06f, 0.12f), lightMat);
            foreach (float x in new[] { -h, h })
                Visual("Hole Rim", new Vector3(hx + x, y + 0.03f, 0f), new Vector3(0.12f, 0.06f, CellHole), lightMat);
            // Four posts carrying the ring.
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1f, 1f })
                    Solid("Spine Post", new Vector3(x * (SpineHalfX - 0.6f), (below + y - 1f) * 0.5f, z * (SpineHalfZ - 0.6f)),
                        new Vector3(0.8f, y - 1f - below, 0.8f), marble);

            // The pioneer bell of this section, on the solid side of the ring.
            MakeBell(level, new Vector3(-Mathf.Sign(hx) * (SpineHalfX - 0.9f), y, 0f), hx > 0f ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity);

            // Everything at this level counts as having reached it; characters come back
            // on the middle strip of the ring.
            var landing = new GameObject($"L{level} Checkpoint");
            landing.transform.SetParent(root, false);
            landing.transform.localPosition = new Vector3(0f, y + 1.5f, 0f);
            var volume = landing.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = new Vector3(CliffHalf * 2f, 3f, CliffHalf * 2f);
            var spawn = new GameObject("Spawn").transform;
            spawn.SetParent(landing.transform, false);
            spawn.localPosition = new Vector3(0f, -1.5f, 0f);
            spawn.localRotation = Quaternion.LookRotation(Vector3.right);
            var checkpoint = landing.AddComponent<SectionCheckpoint>();
            checkpoint.Configure(level, spawn);
            landings.Add(checkpoint);

            if (section != null)
                Sign($"S{level} 종 — F로 치면 S{level} 빛의 기둥이 열린다 (20초는 친 팀만)",
                    new Vector3(-Mathf.Sign(hx) * 2.5f, y + 0.02f, 3.6f), Quaternion.Euler(0f, 180f, 0f), 0.32f);
        }

        // S4, S7, S8: the two halves joined all round (the first clash, the full merge).
        void BuildPlaza(int level, float y)
        {
            foreach (float z in new[] { -1f, 1f })
                Span($"L{level} Plaza", Quaternion.identity, -LandingHalfX, LandingHalfX, y - 1f, y,
                    z < 0f ? LandingNearZ : SpineHalfZ, z < 0f ? -SpineHalfZ : -LandingNearZ, checker);
            foreach (float x in new[] { -1f, 1f })
            {
                Span($"L{level} Plaza", Quaternion.identity, x < 0f ? -LandingHalfX : SpineHalfX, x < 0f ? -SpineHalfX : LandingHalfX,
                    y - 1f, y, -SpineHalfZ, SpineHalfZ, checker);
                Span($"L{level} Side Terrace", Quaternion.identity, x < 0f ? -CliffHalf : LandingHalfX, x < 0f ? -LandingHalfX : CliffHalf,
                    y - 1f, y, LandingNearZ, -LandingNearZ, checker);
            }
        }

        // The light pillar's cell for a section: a lift resting on the ring below, rising
        // through the hole in this section's ring. Hidden until the section's bell is rung.
        void BuildCell(int section)
        {
            float from = QueenHillCourse.LevelTop(section - 1), t = QueenHillCourse.CellThickness;
            var cell = Platform($"S{section} Light Cell", new Vector3(QueenHillCourse.CellX(section), from + t * 0.5f, 0f),
                new Vector3(CellSize, t, CellSize), Vector3.up * (QueenHillCourse.Rise - t), QueenHillCourse.CellSpeed,
                QueenHillCourse.CellPause, lightMat);
            cells.Add(cell);
            var path = new GameObject($"S{section} Open Path");
            path.transform.SetParent(root, false);
            path.AddComponent<OpenPath>().Configure(section, cell.gameObject);
        }

        // One team's route up a section, in that half's frame.
        void BuildRoute(int number, Quaternion half)
        {
            var section = QueenHillCourse.Section(number);
            float side = QueenHillCourse.Side(number);
            float y0 = QueenHillCourse.LevelTop(number - 1), y1 = QueenHillCourse.LevelTop(number);
            float ledge = y0 + QueenHillCourse.RideRise;
            string tag = $"S{number} ";
            Vector3 P(float xr, float y, float z) => half * new Vector3(side * xr, y, z);
            void Route(string name, float x0, float x1, float b, float t, float z0, float z1, Material m) =>
                Span(tag + name, half, Mathf.Min(side * x0, side * x1), Mathf.Max(side * x0, side * x1), b, t, z0, z1, m);

            // The exposed ledge the ride arrives at, the wall above it, the walkway back.
            Route("Ledge", ColumnX1, LedgeX1, ledge - 1f, ledge, LandingFarZ, LandingNearZ, checker);
            Route("Wall", LedgeX1, WallX1, ledge - 1f, y1, LandingFarZ, WalkwayZ, marble);
            Route("Walkway", ColumnX0, WallX1, y1 - 1f, y1, WalkwayZ, LandingNearZ, honey);

            BuildRide(section, half, side, y0, P);
            switch (section.Challenge)
            {
                case QueenHillChallenge.LaunchPad:
                {
                    // The knight's move: up the wall's height, 3.5 m on, onto the wall's top.
                    Vector3 at = P(LedgeX1 - 2f, ledge, -18.5f);
                    Quaternion facing = half * Quaternion.LookRotation(new Vector3(side, 0f, 0f));
                    Visual(tag + "L Pad", at + Vector3.up * 0.01f, new Vector3(1.6f, 0.02f, 1.6f), gold, facing);
                    MakeLaunchPad(tag + "L Pad", at, facing, new Vector3(1.6f, 0.6f, 1.6f),
                        new Vector3(0f, QueenHillCourse.ClimbRise, 3.5f), 1.2f, 0f, false);
                    break;
                }
                case QueenHillChallenge.Chain:
                    MakeRope(tag + "Chain", P(LedgeX1 - 0.3f, y1, -18.5f), half * new Vector3(side, 0f, 0f),
                        QueenHillCourse.ClimbRise - 0.3f, 0f, 5f, 0.1f, chain);
                    break;
            }

            Sign($"S{number} {section.Name}\n{QueenHillCourse.RideName(section.Ride)} → {QueenHillCourse.ChallengeName(section.Challenge)}",
                P(ColumnX0 - 3.5f, y0 + 0.02f, -17f), half * Quaternion.Euler(0f, side > 0f ? 90f : -90f, 0f), 0.4f);
            Sign($"{QueenHillCourse.ClimbRise:0} m 벽: 우클릭 꾹 + W, Space 벽 점프",
                P(LedgeX1 - 2.5f, ledge + 0.02f, -13.5f), half * Quaternion.Euler(0f, side > 0f ? 90f : -90f, 0f), 0.3f);
        }

        delegate Vector3 RoutePoint(float xr, float y, float z);

        void BuildRide(QueenHillSection section, Quaternion half, float side, float y0, RoutePoint P)
        {
            var timing = QueenHillCourse.Timing(section.Ride);
            string tag = $"S{section.Number} ";
            float mid = (ColumnX0 + ColumnX1) * 0.5f;
            switch (section.Ride)
            {
                case QueenHillRide.Lift:
                case QueenHillRide.LightLift:
                    Platform(tag + QueenHillCourse.RideName(section.Ride), P(mid, y0 - 0.2f, -18.5f), new Vector3(3.6f, 0.4f, 3.6f),
                        Vector3.up * QueenHillCourse.RideRise, timing.Speed, timing.Pause,
                        section.Ride == QueenHillRide.LightLift ? lightMat : gold, half);
                    break;
                case QueenHillRide.Ring:
                    Disc(tag + "Orbit Ring", P(mid, y0 - 0.2f, -18.5f), 1.85f, 0.4f,
                        Vector3.up * QueenHillCourse.RideRise, timing.Speed, timing.Pause, 25f * side);
                    break;
                case QueenHillRide.Gondolas:
                    Stack(tag + "Gondola", 3f, 3f, new[] { -13.7f, -17f, -20.3f }, timing, y0, P, lapis, true);
                    break;
                case QueenHillRide.Slabs:
                    Stack(tag + "Slab", 0.5f, 2.2f, new[] { -13.3f, -15.8f, -18.3f, -20.8f }, timing, y0, P, gold, false);
                    break;
                case QueenHillRide.Islands:
                    Stack(tag + "Island", 1.2f, 4.4f, new[] { -14.5f, -19.6f }, timing, y0, P, garden, false);
                    break;
                case QueenHillRide.Spring:
                {
                    // A plate flush with the landing that throws everyone on it up onto the ledge.
                    Vector3 surface = P(mid, y0, -18.5f);
                    Solid(tag + "Spring Plate", surface - Vector3.up * 0.15f, new Vector3(3.6f, 0.3f, 3.6f), gold, half);
                    Quaternion facing = half * Quaternion.LookRotation(new Vector3(side, 0f, 0f));
                    MakeLaunchPad(tag + "Clockwork Spring", surface, facing, new Vector3(3.4f, 0.6f, 3.4f),
                        new Vector3(0f, QueenHillCourse.RideRise, 4.5f), 1.5f, timing.Period, true);
                    // A great gear turning on the wall behind (only looks).
                    var gear = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    gear.name = tag + "Gear";
                    DestroyImmediate(gear.GetComponent<Collider>());
                    gear.SetActive(false);
                    gear.transform.SetParent(root, false);
                    gear.transform.localPosition = P(WallX1 + 0.4f, y0 + 9f, -17f);
                    gear.transform.localRotation = half * Quaternion.Euler(0f, 0f, 90f);
                    gear.transform.localScale = new Vector3(7f, 0.3f, 7f);
                    gear.GetComponent<MeshRenderer>().sharedMaterial = gold;
                    gear.AddComponent<MovingPlatform>().Configure(Vector3.zero, 0f, 0f, 0.5f, 12f * side, Vector3.up);
                    gear.SetActive(true);
                    break;
                }
            }
        }

        // Platforms of a chain ride that rise in turn: platform k travels `step` metres
        // from 3k... below the next one's range by the one metre they share, and every
        // other one starts at its top, so one is at its top when the next is at its bottom.
        void Stack(string name, float height, float depth, float[] zs, QueenHillCourse.RideTiming timing, float y0,
                   RoutePoint P, Material material, bool chains)
        {
            float mid = (ColumnX0 + ColumnX1) * 0.5f, width = ColumnX1 - ColumnX0 - 0.6f;
            for (int k = 0; k < zs.Length; k++)
            {
                float bottom = y0 + (timing.Step - 1f) * k;
                bool fromTop = k % 2 == 1;
                float top = fromTop ? bottom + timing.Step : bottom;
                Vector3 at = P(mid, top - height * 0.5f, zs[k]);
                var p = Platform($"{name} {k + 1}", at, new Vector3(width, height, depth),
                    Vector3.up * (fromTop ? -timing.Step : timing.Step), timing.Speed, timing.Pause, material);
                if (chains)
                {
                    // Gold chains up from the cube (only look; they ride along).
                    var link = Visual("Chain", Vector3.zero, new Vector3(0.12f, 8f, 0.12f), chain);
                    link.transform.SetParent(p.transform, false);
                    link.transform.localPosition = new Vector3(0f, height * 0.5f + 4f, 0f);
                }
            }
        }

        void BuildGate()
        {
            float y = QueenHillCourse.TopHeight;
            // The gate: two pillars and a lintel over the middle, and the crown floating inside.
            foreach (float x in new[] { -1f, 1f })
                Solid("Gate Pillar", new Vector3(x * (SpineHalfX + 1.5f), y + 5f, 0f), new Vector3(1.2f, 10f, 1.2f), marble);
            Visual("Gate Lintel", new Vector3(0f, y + 10.5f, 0f), new Vector3(2f * SpineHalfX + 4.2f, 1f, 1.2f), marble);
            Visual("Crown", new Vector3(0f, y + 7f, 0f), new Vector3(1.6f, 1.6f, 1.6f), gold, Quaternion.Euler(45f, 45f, 0f));
            Visual("Queen Jewel Plate (not built yet)", new Vector3(-(SpineHalfX + 1.5f), y + 0.03f, 3f), new Vector3(2f, 0.06f, 2f), lightMat);
            Sign("퀸 보석판 (아직 없음)", new Vector3(-(SpineHalfX + 1.5f), y + 0.05f, 4.8f), Quaternion.identity, 0.3f);

            // Promotion pedestals on each team's top landing (M11): F in reach.
            var pieces = new[] { PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight, PieceKind.King };
            for (int team = Teams.White; team <= Teams.Black; team++)
            {
                Quaternion half = Half(team);
                for (int i = 0; i < pieces.Length; i++)
                {
                    Vector3 at = half * new Vector3(-6f + 4f * i, y, LandingFarZ + 4f);
                    Solid($"Pedestal {pieces[i]}", at + Vector3.up * 0.4f, new Vector3(0.6f, 0.8f, 0.6f), gold, half);
                    var go = new GameObject($"Promote {pieces[i]} ({Teams.Name(team)})");
                    go.transform.SetParent(root, false);
                    go.transform.localPosition = at;
                    var reach = go.AddComponent<SphereCollider>();
                    reach.isTrigger = true;
                    reach.center = new Vector3(0f, 0.9f, 0f);
                    reach.radius = 0.6f;
                    go.AddComponent<PromotionPad>().Configure(pieces[i]);
                    Sign(ChessPieces.Name(pieces[i]), at + half * new Vector3(0f, 0.02f, 1.2f), half, 0.45f);
                }
                Sign("하늘의 문 (160 m) — 받침대 앞에서 F = 승격", half * new Vector3(0f, y + 0.02f, LandingFarZ + 7.5f), half, 0.5f);
            }
        }

        // Far islands and floating rocks, for scale (only look).
        void BuildScenery()
        {
            var rocks = new[]
            {
                new Vector4(-140f, 10f, 60f, 30f), new Vector4(160f, 18f, -40f, 40f), new Vector4(90f, 6f, 170f, 26f),
                new Vector4(-110f, 4f, -150f, 22f), new Vector4(-60f, 70f, 45f, 9f), new Vector4(55f, 110f, -35f, 11f),
                new Vector4(48f, 40f, 50f, 8f), new Vector4(-52f, 135f, -30f, 7f),
            };
            foreach (var r in rocks)
                Visual("Floating Rock", new Vector3(r.x, r.y, r.z), new Vector3(r.w, r.w * 0.8f, r.w * 1.1f), rock, Quaternion.Euler(0f, r.x, 0f));
        }

        // ---------------------------------------------------------------- pieces

        void MakeBell(int section, Vector3 at, Quaternion facing)
        {
            Solid($"S{section} Bell Post", at + Vector3.up * 0.8f, new Vector3(0.15f, 1.6f, 0.15f), marble);
            var go = new GameObject($"S{section} Pioneer Bell");
            go.transform.SetParent(root, false);
            go.transform.localPosition = at;
            go.transform.localRotation = facing;
            var pivot = new GameObject("Swing").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(-0.3f, 1.55f, 0f);
            var cup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(cup.GetComponent<Collider>());
            cup.name = "Cup";
            cup.transform.SetParent(pivot, false);
            cup.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            cup.transform.localScale = new Vector3(0.45f, 0.22f, 0.45f);
            cup.GetComponent<MeshRenderer>().sharedMaterial = gold;
            var reach = go.AddComponent<SphereCollider>();
            reach.isTrigger = true;
            reach.center = new Vector3(-0.3f, 1.2f, 0f);
            reach.radius = 0.7f;
            go.AddComponent<Bell>().Configure(section, pivot);
        }

        void MakeLaunchPad(string name, Vector3 surface, Quaternion facing, Vector3 volume, Vector3 throwTo, float clearance,
                           float period, bool sameThrow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = surface;
            go.transform.localRotation = facing;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, volume.y * 0.5f, 0f);
            box.size = volume;
            go.AddComponent<LaunchPad>().Configure(throwTo, clearance, period, 0f, sameThrow);
        }

        void MakeRope(string name, Vector3 top, Vector3 forward, float length, float swing, float period, float thickness, Material material)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(root, false);
            go.transform.localPosition = top;
            go.transform.localRotation = Quaternion.LookRotation(forward);
            go.AddComponent<RopeLine>().Configure(length, swing, period, 0f, thickness, material);
            go.SetActive(true);
        }

        // A box that moves. Kept inactive until configured: the platform evaluates its
        // first pose in Awake.
        MovingPlatform Platform(string name, Vector3 center, Vector3 size, Vector3 travel, float speed, float pause,
                                Material material, Quaternion? rotation = null)
        {
            var go = Solid(name, center, size, material, rotation ?? Quaternion.identity);
            go.SetActive(false);
            var platform = go.AddComponent<MovingPlatform>();
            platform.Configure(travel, speed, pause, QueenHillCourse.RampFor(speed));
            go.SetActive(true);
            return platform;
        }

        void Disc(string name, Vector3 center, float radius, float height, Vector3 travel, float speed, float pause, float spin)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.SetActive(false);
            go.transform.SetParent(root, false);
            go.transform.localPosition = center;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            // The primitive's capsule is rounded top and bottom; a disc needs a flat top.
            DestroyImmediate(go.GetComponent<Collider>());
            var mesh = go.AddComponent<MeshCollider>();
            mesh.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mesh.convex = true;
            go.GetComponent<MeshRenderer>().sharedMaterial = gold;
            go.AddComponent<MovingPlatform>().Configure(travel, speed, pause, QueenHillCourse.RampFor(speed), spin, Vector3.up);
            go.SetActive(true);
            // A stripe so the turning shows.
            var stripe = Visual("Stripe", Vector3.zero, Vector3.one, lapis);
            stripe.transform.SetParent(go.transform, false);
            stripe.transform.localPosition = new Vector3(0.25f, 1.01f, 0f);
            stripe.transform.localScale = new Vector3(0.5f, 0.02f, 0.08f);
        }

        // An axis-aligned box from its extents, turned into a team's half.
        GameObject Span(string name, Quaternion half, float x0, float x1, float y0, float y1, float z0, float z1, Material material) =>
            Solid(name, half * new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f),
                new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0)), material, half);

        GameObject Solid(string name, Vector3 center, Vector3 size, Material material, Quaternion? rotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        // A box you can see but not touch.
        GameObject Visual(string name, Vector3 center, Vector3 size, Material material, Quaternion? rotation = null)
        {
            var go = Solid(name, center, size, material, rotation);
            DestroyImmediate(go.GetComponent<BoxCollider>());
            return go;
        }

        void Sign(string text, Vector3 position, Quaternion facing, float size)
        {
            if (!signs) return;
            var go = new GameObject("Sign " + text.Replace('\n', ' '));
            go.transform.SetParent(root, false);
            // Lying on the floor, readable walking along the frame's forward.
            go.transform.localPosition = position;
            go.transform.localRotation = facing * Quaternion.Euler(90f, 0f, 0f);
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = RuntimePanels.KoreanFont != null ? RuntimePanels.KoreanFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.fontSize = 64;
            mesh.characterSize = size * 0.12f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(0.2f, 0.16f, 0.1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
        }

        // ---------------------------------------------------------------- look

        void MakeMaterials()
        {
            marble = NewMaterial(new Color(0.93f, 0.88f, 0.78f));
            honey = NewMaterial(new Color(0.88f, 0.76f, 0.55f));
            cliff = NewMaterial(new Color(0.84f, 0.77f, 0.64f));
            garden = NewMaterial(new Color(0.42f, 0.66f, 0.44f));
            cypress = NewMaterial(new Color(0.18f, 0.4f, 0.26f));
            gold = NewMaterial(new Color(0.93f, 0.74f, 0.3f));
            lapis = NewMaterial(new Color(0.2f, 0.33f, 0.66f));
            lightMat = NewMaterial(new Color(0.72f, 0.93f, 1f));
            sea = NewMaterial(new Color(0.12f, 0.56f, 0.62f));
            stone = NewMaterial(new Color(0.72f, 0.67f, 0.6f));
            rock = NewMaterial(new Color(0.56f, 0.52f, 0.48f));
            waterfall = NewMaterial(new Color(0.84f, 0.95f, 1f));
            chain = NewMaterial(new Color(0.85f, 0.66f, 0.25f));
            // Lapis and pale gold, one-metre squares (box UVs run one unit per two metres).
            checkerTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "QueenHillChecker"
            };
            var a = new Color(0.22f, 0.35f, 0.68f);
            var b = new Color(0.93f, 0.85f, 0.6f);
            checkerTexture.SetPixels(new[] { a, b, b, a });
            checkerTexture.Apply();
            owned.Add(checkerTexture);
            checker = NewMaterial(Color.white);
            checker.mainTexture = checkerTexture;
        }

        Material NewMaterial(Color color)
        {
            var shader = Shader.Find("Standard");
            var m = new Material(shader != null ? shader : Shader.Find("Legacy Shaders/Diffuse")) { color = color };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.12f);
            owned.Add(m);
            return m;
        }

        // Box mesh with UVs in world metres (one unit per two metres), like the lab's.
        Mesh BoxMesh(Vector3 size)
        {
            Vector3 h = size * 0.5f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            void Face(Vector3 normal, Vector3 u, Vector3 w, float su, float sw)
            {
                int b = vertices.Count;
                Vector3 c = Vector3.Scale(normal, h);
                Vector3 du = u * (su * 0.5f), dw = w * (sw * 0.5f);
                vertices.Add(c - du - dw);
                vertices.Add(c + du - dw);
                vertices.Add(c + du + dw);
                vertices.Add(c - du + dw);
                for (int i = 0; i < 4; i++) normals.Add(normal);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(su * 0.5f, 0f));
                uvs.Add(new Vector2(su * 0.5f, sw * 0.5f));
                uvs.Add(new Vector2(0f, sw * 0.5f));
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }
            Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
            Face(Vector3.down, Vector3.left, Vector3.forward, size.x, size.z);
            Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
            Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
            Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
            var mesh = new Mesh { name = "QueenHillBox" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            owned.Add(mesh);
            return mesh;
        }
    }
}
