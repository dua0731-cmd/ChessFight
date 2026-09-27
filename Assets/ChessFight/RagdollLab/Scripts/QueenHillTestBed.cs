using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// [7] The Queen of the Hill mechanics test bed, on a floor of its own south-east of the arena.
    /// Built from code when the lab starts, so the scene does not need rebuilding; LabAutoTest uses the
    /// same pieces.
    ///
    ///   7a  shuttle: a 3 m platform going 8 m back and forth at 2 m/s          (M1 riding)
    ///   7b  lift: 12 m up at 3 m/s to the top of a tower you can step onto     (M1, and jumping on it)
    ///   7c  turntable: 8 m disc at 20 degrees a second                          (M1, turning with it)
    ///   7d  sliding wall: 4 m wall going 8 m sideways at 1 m/s, to climb        (M1 on a wall)
    ///   7e  pool: water with a pier, a pillar to climb down into it, a crate    (M4: float, then respawn)
    ///   7f  hook range, east: a 10 m tower 20 m from the start line, and an arch (M5 grappling hook)
    ///   7g  pioneer tower, north of 7f: two sections with a bell on each landing and the light pillar's
    ///       lift cell beside each wall (M8), each landing a checkpoint (M9)
    ///   7h  launch pads, south of 7f: an L-shaped pad ("up 6 m, 3 m on") and a clockwork spring that
    ///       throws everyone on it 8 m up every 4 s (M7)
    ///   7i  ropes, south of 7h: a 6 m tower with a rope down its face, a swing across an 8 m gap between two
    ///       3 m platforms, and a thick pole to try the wall climb on (M6)
    ///   7j  promotion, south of 7i: pedestals for the rook, bishop, knight and king, and one back to a pawn (M11)
    ///
    /// Offline hotkeys for P1: F9 walk-in point, F8 the hook range's start line, F10 the pioneer tower (Shift+F10 the
    /// launch pads; F3 is the online panel), Shift+F8 the ropes, Shift+F9 the promotion pedestals, F11 silences the bells, F5 a 6 m/s hit with a
    /// 1 s knockdown, F6 stamina -2.5, F7 knocked off the wall (M3). A corner box shows what the pawn
    /// received from the new keys (M2) and what its hook is doing (M5).
    /// </summary>
    public class QueenHillTestBed : MonoBehaviour
    {
        public LabGame game;

        // ---------------------------------------------------------------- layout (LabAutoTest reads it)

        /// <summary>The test bed's floor: x 5..35, z -45..-15, top at y 0, joined to the arena's south edge.</summary>
        public static readonly Vector3 Entrance = new Vector3(15f, 0f, -16.5f);

        public const float PlatformThickness = 0.3f;
        public static readonly Vector3 PlatformSize = new Vector3(3f, PlatformThickness, 3f);
        public static readonly Vector3 ShuttleStart = new Vector3(8.5f, PlatformThickness * 0.5f, -19.5f);
        public static readonly Vector3 ShuttleTravel = new Vector3(8f, 0f, 0f);
        public const float ShuttleSpeed = 2f, ShuttlePause = 1.5f;

        public static readonly Vector3 LiftStart = new Vector3(24f, PlatformThickness * 0.5f, -19.5f);
        public const float LiftRise = 12f, LiftSpeed = 3f, LiftPause = 2f;
        public static readonly Vector3 TowerCenter = new Vector3(27.8f, (LiftRise + PlatformThickness) * 0.5f, -19.5f);
        public static readonly Vector3 TowerSize = new Vector3(4f, LiftRise + PlatformThickness, 4f);

        public static readonly Vector3 DiscCenter = new Vector3(11f, 0.2f, -28f);
        public const float DiscRadius = 4f, DiscHeight = 0.4f, DiscSpin = 20f;

        public static readonly Vector3 SlideWallStart = new Vector3(21f, 2.5f, -30f);
        public static readonly Vector3 SlideWallSize = new Vector3(4f, 5f, 1f);
        public static readonly Vector3 SlideWallTravel = new Vector3(8f, 0f, 0f);
        public const float SlideWallSpeed = 1f, SlideWallPause = 1.5f;

        // The pool: x 8..22, z -44..-36, bottom at y -2, water up to y -0.3.
        public const float PoolMinX = 8f, PoolMaxX = 22f, PoolMinZ = -44f, PoolMaxZ = -36f, PoolBottom = -2f, WaterTop = -0.3f;
        /// <summary>A 2 m pier runs out over the water to the pillar; the pillar's face (toward +Z) is at PillarFaceZ.</summary>
        public const float PierMinX = 14f, PierMaxX = 16f, PillarFaceZ = -39f;
        public static readonly Vector3 PierEnd = new Vector3(15f, 0f, PillarFaceZ + 0.45f);
        /// <summary>Where the water puts pawns back (the ground point), facing the pool.</summary>
        public static readonly Vector3 Checkpoint = new Vector3(15f, 0f, -33f);
        public static readonly Vector3 CrateSpot = new Vector3(18.5f, 0.25f, -34.5f);

        // 7f: the hook range, x 35..75 beside the test bed. Throw from the start line toward +X at a tower
        // whose face is 20 m away and whose top is 10 m up - the "20 m in about 4 s" of the plan - or
        // at the underside of the arch.
        public static readonly Vector3 HookStart = new Vector3(40f, 0f, -30f);
        public static readonly Vector3 HookTowerCenter = new Vector3(64f, 5f, -30f);
        public static readonly Vector3 HookTowerSize = new Vector3(8f, 10f, 10f);
        public static float HookTowerFaceX => HookTowerCenter.x - HookTowerSize.x * 0.5f;
        public static float HookTowerTop => HookTowerCenter.y + HookTowerSize.y * 0.5f;
        public static readonly Vector3 HookArchCenter = new Vector3(50f, 5.25f, -40f);
        public static readonly Vector3 HookArchSize = new Vector3(4f, 0.5f, 4f);

        // 7g: the pioneer tower. Two walls of SectionRise, the second set back PioneerStepBack behind the
        // first; the top of each is its section's landing, with the bell at its far side. Beside each wall
        // is the light pillar's lift cell for that section, hidden until the section's bell is rung.
        public static readonly Vector3 TowerStart = new Vector3(42f, 0f, -5f);
        public const float SectionRise = 5.3f, PioneerFaceX = 50f, PioneerStepBack = 4f;
        public static float SectionFaceX(int section) => PioneerFaceX + (section - 1) * PioneerStepBack;
        public static Vector3 LandingSpawn(int section) => new Vector3(SectionFaceX(section) + 1f, section * SectionRise, -5.5f);
        public static Vector3 BellSpot(int section) => new Vector3(SectionFaceX(section) + 3.2f, section * SectionRise, -8.5f);
        // 7h: launch pads, x 35..75, z -65..-45. Both throw toward +X. The L-shaped pad (S3, the knight's
        // move) throws whoever steps on it up PadThrow.y and PadThrow.z on, onto a ledge whose lip is
        // 0.8 m short of the spot. The clockwork spring (S5) winds up for SpringPeriod seconds and then
        // throws everyone on it the same way, SpringThrow up and on, onto a higher floor 3 m past it.
        public static readonly Vector3 LaunchStart = new Vector3(38f, 0f, -52f);
        public static readonly Vector3 PadCenter = new Vector3(42f, 0f, -52f);
        public const float PadSize = 1.6f, PadClearance = 1.2f, PadLedgeFaceX = 44.2f;
        public static readonly Vector3 PadThrow = new Vector3(0f, 6f, 3f);       // pad frame: up, forward (+X)
        public static Vector3 PadLanding => PadCenter + new Vector3(PadThrow.z, PadThrow.y, 0f);
        public static readonly Vector3 SpringCenter = new Vector3(56f, 0f, -52f); // the floor under it
        public const float SpringSize = 2.4f, SpringHeight = 0.3f, SpringPeriod = 4f, SpringClearance = 1.5f;
        public const float SpringLedgeFaceX = 60.2f;
        public static readonly Vector3 SpringThrow = new Vector3(0f, 8f, 6f);
        public static float SpringTop => SpringHeight;
        public static float SpringLedgeTop => SpringHeight + SpringThrow.y;

        // 7i: ropes, x 35..75, z -85..-65. A gold chain hangs down the west face of a 6 m tower from its
        // top edge; a swing hangs from 9.9 m over the middle of an 8 m gap between two 3 m platforms and
        // swings 40 degrees each way along X, its end reaching a metre onto each; a 0.5 m pole stands east.
        public static readonly Vector3 RopeStart = new Vector3(36.5f, 0f, -70f);
        public const float RopeTowerFaceX = 40f, RopeTowerTop = 6f, RopeOut = 0.35f;
        public static Vector3 ClimbRopeTop => new Vector3(RopeTowerFaceX - RopeOut, RopeTowerTop, -70f);
        public const float SwingGap = 8f, SwingPlatformTop = 3f, SwingWestEdgeX = 54f, SwingLength = 8f, SwingDegrees = 40f,
            SwingPeriod = 5f, SwingPivotHeight = 9.9f;
        public static float SwingEastEdgeX => SwingWestEdgeX + SwingGap;
        public static Vector3 SwingPivot => new Vector3(SwingWestEdgeX + SwingGap * 0.5f, SwingPivotHeight, -70f);
        public static Vector3 SwingStart => new Vector3(SwingWestEdgeX - 0.8f, SwingPlatformTop, -70f);
        public static readonly Vector3 PoleCenter = new Vector3(71f, 3f, -70f);
        public const float PoleRadius = 0.25f, PoleHeight = 6f;

        // 7j: promotion pedestals in a row along X at z -95, facing north (+Z) where the pawns come from.
        public static readonly Vector3 PromotionStart = new Vector3(46f, 0f, -91f);
        public const float PedestalZ = -95f;
        public static readonly PieceKind[] PedestalPieces = { PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight, PieceKind.King, PieceKind.Pawn };
        public static Vector3 Pedestal(int i) => new Vector3(40f + i * 3f, 0f, PedestalZ);

        public static Vector3 PillarCenter(int section) =>
            new Vector3(SectionFaceX(section) - 1.2f, (section - 1) * SectionRise + PlatformThickness * 0.5f, -2f);

        public MovingPlatform Shuttle { get; private set; }
        public MovingPlatform Lift { get; private set; }
        public MovingPlatform Disc { get; private set; }
        public MovingPlatform SlideWall { get; private set; }
        public WaterZone Water { get; private set; }
        public Rigidbody Crate { get; private set; }
        public QueenHillMatch Match { get; private set; }
        public readonly MovingPlatform[] PillarLifts = new MovingPlatform[3];   // by section, 1 and 2
        public LaunchPad Pad { get; private set; }
        public LaunchPad Spring { get; private set; }
        public RopeLine ClimbRope { get; private set; }
        public RopeLine Swing { get; private set; }
        public readonly PromotionPad[] Pedestals = new PromotionPad[5];

        /// <summary>Pawns put back on the checkpoint by the water so far, and the last one.</summary>
        public int WaterRespawns { get; private set; }
        public string LastWater { get; private set; } = "-";

        /// <summary>The last few water events ("12.3s 더미 1 물" / "부활"), for the automated checks.</summary>
        public readonly List<string> WaterLog = new List<string>();

        readonly Dictionary<RagdollPawn, float> drowning = new Dictionary<RagdollPawn, float>();
        readonly List<RagdollPawn> due = new List<RagdollPawn>();
        Transform root;
        Material floorMaterial, wallMaterial, platformMaterial, waterMaterial, crateMaterial, padMaterial, lightMaterial, bellMaterial,
            ropeMaterial;
        Font font;
        GUIStyle style;
        string lastAction = "";
        float lastActionAt = -10f;

        void Awake() => Build();

        void OnEnable() => WaterZone.Entered += OnWater;
        void OnDisable() => WaterZone.Entered -= OnWater;

        void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }

        // ---------------------------------------------------------------- building

        void Build()
        {
            if (root != null) return;
            root = new GameObject("[7] Queen of the Hill Test Bed").transform;
            MakeMaterials();

            // The floor, with a hole for the pool. Two metres thick so the pool has walls.
            const float t = 2f;
            Box("Floor North", new Vector3(20f, -t * 0.5f, (PoolMaxZ - 15f) * 0.5f), new Vector3(30f, t, -15f - PoolMaxZ), floorMaterial);
            Box("Floor South", new Vector3(20f, -t * 0.5f, (PoolMinZ - 45f) * 0.5f), new Vector3(30f, t, PoolMinZ + 45f), floorMaterial);
            Box("Floor West", new Vector3((5f + PoolMinX) * 0.5f, -t * 0.5f, (PoolMinZ + PoolMaxZ) * 0.5f),
                new Vector3(PoolMinX - 5f, t, PoolMaxZ - PoolMinZ), floorMaterial);
            Box("Floor East", new Vector3((PoolMaxX + 35f) * 0.5f, -t * 0.5f, (PoolMinZ + PoolMaxZ) * 0.5f),
                new Vector3(35f - PoolMaxX, t, PoolMaxZ - PoolMinZ), floorMaterial);
            Box("Pool Bottom", new Vector3((PoolMinX + PoolMaxX) * 0.5f, PoolBottom - 0.5f, (PoolMinZ + PoolMaxZ) * 0.5f),
                new Vector3(PoolMaxX - PoolMinX, 1f, PoolMaxZ - PoolMinZ), floorMaterial);
            Label("[7] 퀸 오브 더 힐 시험대", Entrance + new Vector3(0f, 0.02f, -1.2f), 0.8f);

            // 7a shuttle
            Shuttle = Platform("7a Shuttle", ShuttleStart, PlatformSize, ShuttleTravel, ShuttleSpeed, ShuttlePause, platformMaterial);
            Label("[7a] 왕복 발판", ShuttleStart + new Vector3(0f, -0.13f, 2.3f), 0.5f);

            // 7b lift and its tower (0.3 m gap to step across at the top)
            Lift = Platform("7b Lift", LiftStart, PlatformSize, Vector3.up * LiftRise, LiftSpeed, LiftPause, platformMaterial);
            Box("7b Tower", TowerCenter, TowerSize, wallMaterial);
            Label("[7b] 승강기 12m", LiftStart + new Vector3(0f, -0.13f, 2.3f), 0.5f);

            // 7c turntable
            Disc = MakeDisc();
            Label("[7c] 회전 원판 20°/초", DiscCenter + new Vector3(0f, -0.18f, DiscRadius + 0.8f), 0.5f);

            // 7d sliding wall, face toward +Z
            SlideWall = Platform("7d Sliding Wall", SlideWallStart, SlideWallSize, SlideWallTravel, SlideWallSpeed, SlideWallPause, wallMaterial);
            Label("[7d] 움직이는 벽 (W+우클릭 매달리기)", SlideWallStart + new Vector3(SlideWallTravel.x * 0.5f, -2.48f, 1.6f), 0.45f);

            // 7e pool: pier, pillar, water, crate, checkpoint pad
            float pierLength = PoolMaxZ - PillarFaceZ;
            Box("7e Pier", new Vector3((PierMinX + PierMaxX) * 0.5f, -t * 0.5f, PoolMaxZ - pierLength * 0.5f),
                new Vector3(PierMaxX - PierMinX, t, pierLength), floorMaterial);
            Box("7e Pillar", new Vector3((PoolMinX + PoolMaxX) * 0.5f, (PoolBottom + 3f) * 0.5f, PillarFaceZ - 1.25f),
                new Vector3(8f, 3f - PoolBottom, 2.5f), wallMaterial);
            var surface = Box("7e Water Surface", new Vector3((PoolMinX + PoolMaxX) * 0.5f, WaterTop - 0.01f, (PoolMinZ + PoolMaxZ) * 0.5f),
                new Vector3(PoolMaxX - PoolMinX, 0.02f, PoolMaxZ - PoolMinZ), waterMaterial);
            DestroyImmediate(surface.GetComponent<Collider>());
            var water = new GameObject("7e Water");
            water.transform.SetParent(root, false);
            water.transform.position = new Vector3((PoolMinX + PoolMaxX) * 0.5f, (PoolBottom + WaterTop) * 0.5f, (PoolMinZ + PoolMaxZ) * 0.5f);
            var box = water.AddComponent<BoxCollider>();
            box.size = new Vector3(PoolMaxX - PoolMinX, WaterTop - PoolBottom, PoolMaxZ - PoolMinZ);
            box.isTrigger = true;
            Water = water.AddComponent<WaterZone>();
            var pad = Box("7e Checkpoint Pad", Checkpoint + Vector3.up * 0.005f, new Vector3(2f, 0.01f, 2f), padMaterial);
            DestroyImmediate(pad.GetComponent<Collider>());
            Label($"[7e] 물 → {Water.RespawnDelay:0}초 둥둥 → 여기서 부활", Checkpoint + new Vector3(0f, 0.02f, 1.6f), 0.45f);
            Label("부두 끝에서 기둥에 매달려 옆으로 → 아래로", PierEnd + new Vector3(0f, 0.02f, 1.6f), 0.35f);
            var crate = Box("7e Crate", CrateSpot, Vector3.one * 0.5f, crateMaterial);
            Crate = crate.AddComponent<Rigidbody>();
            Crate.mass = 5f;
            Crate.interpolation = RigidbodyInterpolation.Interpolate;

            // 7f hook range
            Box("7f Floor", new Vector3(55f, -t * 0.5f, -30f), new Vector3(40f, t, 30f), floorMaterial);
            Box("7f Tower", HookTowerCenter, HookTowerSize, wallMaterial);
            Box("7f Arch Top", HookArchCenter, HookArchSize, platformMaterial);
            float pillarHeight = HookArchCenter.y - HookArchSize.y * 0.5f;
            foreach (float side in new[] { -1f, 1f })
                Box("7f Arch Pillar", new Vector3(HookArchCenter.x + side * (HookArchSize.x * 0.5f - 0.4f), pillarHeight * 0.5f, HookArchCenter.z),
                    new Vector3(0.8f, pillarHeight, 0.8f), wallMaterial);
            var line = Box("7f Start Line", HookStart + new Vector3(0f, 0.005f, 0f), new Vector3(0.3f, 0.01f, 6f), padMaterial);
            DestroyImmediate(line.GetComponent<Collider>());
            Label("[7f] 갈고리: E 꺼내기 · 좌클릭 꾹 = 돌리며 게이지 · 떼면 던짐", HookStart + new Vector3(-1.6f, 0.02f, 0f), 0.45f);
            Label("Space·E 밧줄 놓기 · 벽 앞에서 우클릭 = 벽 잡기 · 남의 갈고리 옆 F 꾹 = 앙파상", HookStart + new Vector3(-2.6f, 0.02f, 0f), 0.35f);
            Label("[7f] 탑 10 m (20 m 앞)", new Vector3(HookTowerFaceX - 1.2f, 0.02f, HookTowerCenter.z), 0.45f);
            Label("[7f] 아치 (천장 5 m)", HookArchCenter + new Vector3(0f, -HookArchCenter.y + 0.02f, 3f), 0.4f);

            // 7g pioneer tower
            Box("7g Floor", new Vector3(55f, -t * 0.5f, -5f), new Vector3(40f, t, 20f), floorMaterial);
            Match = gameObject.GetComponent<QueenHillMatch>();
            if (Match == null) Match = gameObject.AddComponent<QueenHillMatch>();
            Match.Configure(2, 20f);
            for (int s = 1; s <= 2; s++)
            {
                float faceX = SectionFaceX(s), baseY = (s - 1) * SectionRise;
                Box($"7g Wall S{s}", new Vector3(faceX + (s == 1 ? 4f : 2f), baseY + SectionRise * 0.5f, -5f),
                    new Vector3(s == 1 ? 8f : 4f, SectionRise, 10f), wallMaterial);
                // The landing on top of this section's wall: its checkpoint.
                var landing = new GameObject($"7g Landing S{s}");
                landing.transform.SetParent(root, false);
                landing.transform.position = new Vector3(faceX + PioneerStepBack * 0.5f, s * SectionRise + 1f, -5f);
                var volume = landing.AddComponent<BoxCollider>();
                volume.isTrigger = true;
                volume.size = new Vector3(PioneerStepBack, 2f, 10f);
                var spawnPoint = new GameObject("Spawn").transform;
                spawnPoint.SetParent(landing.transform, false);
                spawnPoint.SetPositionAndRotation(LandingSpawn(s), Quaternion.LookRotation(Vector3.right));
                landing.AddComponent<SectionCheckpoint>().Configure(s, spawnPoint);
                MakeBell(s, BellSpot(s));
                // The light pillar's cell: a lift up this wall, flush with the landing at the top.
                PillarLifts[s] = Platform($"7g Pillar S{s}", PillarCenter(s), new Vector3(2f, PlatformThickness, 2f),
                    Vector3.up * (SectionRise - PlatformThickness), 1f, 1.5f, lightMaterial);
                var path = new GameObject($"7g Path S{s}");
                path.transform.SetParent(root, false);
                path.AddComponent<OpenPath>().Configure(s, PillarLifts[s].gameObject);
            }
            Label("[7g] 개척의 탑: 벽을 올라 종 앞에서 F, 옆에 빛의 기둥 승강기 (20초는 친 팀만)", TowerStart + new Vector3(-1.6f, 0.02f, 0f), 0.4f);
            Label("S1 5.3 m, S2 10.6 m. F10 이동, F11 종 초기화. 물에 빠지면 체크포인트", TowerStart + new Vector3(-2.5f, 0.02f, 0f), 0.33f);

            // 7h launch pads
            Box("7h Floor", new Vector3(55f, -t * 0.5f, -55f), new Vector3(40f, t, 20f), floorMaterial);
            Quaternion east = Quaternion.LookRotation(Vector3.right);
            // The L-shaped pad, flush with the floor, and the ledge it throws onto.
            Visual("7h Pad", PadCenter + Vector3.up * 0.01f, new Vector3(PadSize, 0.02f, PadSize), platformMaterial);
            Visual("7h Pad L", PadCenter + new Vector3(0.3f, 0.025f, -0.45f), new Vector3(0.9f, 0.01f, 0.2f), wallMaterial);
            Visual("7h Pad L2", PadCenter + new Vector3(-0.05f, 0.025f, -0.05f), new Vector3(0.2f, 0.01f, 1f), wallMaterial);
            Pad = MakeLaunchPad("7h L Pad", PadCenter, east, new Vector3(PadSize, 0.6f, PadSize), PadThrow, PadClearance, 0f, false, null);
            float ledgeDepth = 5.8f;
            Box("7h Ledge", new Vector3(PadLedgeFaceX + ledgeDepth * 0.5f, PadThrow.y * 0.5f, PadCenter.z),
                new Vector3(ledgeDepth, PadThrow.y, 6f), wallMaterial);
            Visual("7h Pad Mark", PadLanding + Vector3.up * 0.01f, new Vector3(1f, 0.02f, 1f), padMaterial);
            // The clockwork spring: a plate on the floor, its key turning on the side, and the floor above.
            Box("7h Spring", SpringCenter + Vector3.up * (SpringHeight * 0.5f), new Vector3(SpringSize, SpringHeight, SpringSize), platformMaterial);
            var key = new GameObject("7h Spring Key").transform;
            key.SetParent(root, false);
            key.SetPositionAndRotation(SpringCenter + new Vector3(0f, SpringHeight * 0.5f, -SpringSize * 0.5f),
                Quaternion.FromToRotation(Vector3.up, Vector3.back));
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(shaft.GetComponent<Collider>());
            shaft.name = "Shaft";
            shaft.transform.SetParent(key, false);
            shaft.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            shaft.transform.localScale = new Vector3(0.08f, 0.2f, 0.08f);
            shaft.GetComponent<MeshRenderer>().sharedMaterial = bellMaterial;
            var handle = Visual("Handle", Vector3.zero, new Vector3(0.6f, 0.08f, 0.12f), bellMaterial);
            handle.transform.SetParent(key, false);
            handle.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            Spring = MakeLaunchPad("7h Spring Pad", SpringCenter + Vector3.up * SpringTop, east, new Vector3(SpringSize, 0.6f, SpringSize),
                SpringThrow, SpringClearance, SpringPeriod, true, key);
            float upperDepth = 7f;
            Box("7h Upper Floor", new Vector3(SpringLedgeFaceX + upperDepth * 0.5f, SpringLedgeTop * 0.5f, SpringCenter.z),
                new Vector3(upperDepth, SpringLedgeTop, 8f), wallMaterial);
            Label("[7h] L자 도약대: 밟으면 위 6 m, 앞 3 m (나이트처럼)", LaunchStart + new Vector3(-1.4f, 0.02f, 0f), 0.4f);
            BuildRopes(t);
            BuildPromotion(t);
            Label("[7h] 태엽 스프링: 4초마다 위에 선 모두를 8 m 위로. Shift+F10 이동", SpringCenter + new Vector3(-2.2f, 0.02f, 0f), 0.4f);
        }

        /// <summary>7i: the climbing chain, the swing over the gap and the thick pole (M6).</summary>
        void BuildRopes(float t)
        {
            Box("7i Floor", new Vector3(55f, -t * 0.5f, -75f), new Vector3(40f, t, 20f), floorMaterial);
            // The tower with the chain down its west face, from its top edge nearly to the floor.
            Box("7i Rope Tower", new Vector3(RopeTowerFaceX + 2f, RopeTowerTop * 0.5f, -70f), new Vector3(4f, RopeTowerTop, 4f), wallMaterial);
            ClimbRope = MakeRope("7i Chain", ClimbRopeTop, Vector3.right, RopeTowerTop - 0.3f, 0f, 0f, 0.1f, bellMaterial);
            // The swing: two 3 m platforms, an 8 m gap, the pivot over its middle on a beam.
            Box("7i Swing West", new Vector3(SwingWestEdgeX - 2f, SwingPlatformTop * 0.5f, -70f), new Vector3(4f, SwingPlatformTop, 6f), wallMaterial);
            Box("7i Swing East", new Vector3(SwingEastEdgeX + 2f, SwingPlatformTop * 0.5f, -70f), new Vector3(4f, SwingPlatformTop, 6f), wallMaterial);
            Box("7i Swing Beam", SwingPivot + Vector3.up * 0.15f, new Vector3(0.3f, 0.3f, 3f), wallMaterial);
            Swing = MakeRope("7i Swing", SwingPivot, Vector3.right, SwingLength, SwingDegrees, SwingPeriod, 0.07f, ropeMaterial);
            // The thick pole: can the wall climb take it? (MECHANICS_TODO M6 asks this first.)
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "7i Pole";
            pole.transform.SetParent(root, false);
            pole.transform.position = PoleCenter;
            pole.transform.localScale = new Vector3(PoleRadius * 2f, PoleHeight * 0.5f, PoleRadius * 2f);
            pole.GetComponent<MeshRenderer>().sharedMaterial = ropeMaterial;
            Label("[7i] 밧줄: 우클릭으로 잡고 W/S 오르내림 · A/D 돌기 · Space 뛰어내림 · 꼭대기에서 W = 올라섬", RopeStart + new Vector3(-1.4f, 0.02f, 0f), 0.4f);
            Label("그네: 8 m 틈. 끝에 왔을 때 우클릭, 건너편 끝에서 놓기. Shift+F8 이동", RopeStart + new Vector3(-2.3f, 0.02f, 0f), 0.35f);
        }

        /// <summary>7j: a pedestal per piece and one back to a pawn; F within reach promotes (M11).</summary>
        void BuildPromotion(float t)
        {
            Box("7j Floor", new Vector3(55f, -t * 0.5f, -95f), new Vector3(40f, t, 20f), floorMaterial);
            for (int i = 0; i < PedestalPieces.Length; i++)
            {
                Vector3 at = Pedestal(i);
                PieceKind kind = PedestalPieces[i];
                bool back = kind == PieceKind.Pawn;
                Box($"7j Pedestal {kind}", at + Vector3.up * 0.4f, new Vector3(0.6f, 0.8f, 0.6f), back ? wallMaterial : bellMaterial);
                var go = new GameObject($"7j Promote {kind}");
                go.transform.SetParent(root, false);
                go.transform.position = at;
                var reach = go.AddComponent<SphereCollider>();
                reach.isTrigger = true;
                reach.center = new Vector3(0f, 0.9f, 0f);
                reach.radius = 0.6f;
                Pedestals[i] = go.AddComponent<PromotionPad>();
                Pedestals[i].Configure(back ? PieceKind.Rook : kind, back);
                Label(back ? "폰으로" : ChessPieces.Name(kind), at + new Vector3(0f, 0.02f, 1.2f), 0.5f);
            }
            Label("[7j] 승격: 받침대 앞에서 F. 폰만, 킹은 팀에 하나. Shift+F9 이동", PromotionStart + new Vector3(0f, 0.02f, 1.5f), 0.4f);
        }

        /// <summary>A rope (M6) hanging from `top`, swinging along `forward`.</summary>
        RopeLine MakeRope(string name, Vector3 top, Vector3 forward, float length, float swing, float period, float thickness, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(top, Quaternion.LookRotation(forward));
            var line = go.AddComponent<RopeLine>();
            line.Configure(length, swing, period, 0f, thickness, material);
            return line;
        }

        /// <summary>A box you can see and walk through: only looks.</summary>
        GameObject Visual(string name, Vector3 center, Vector3 size, Material material)
        {
            var go = Box(name, center, size, material);
            DestroyImmediate(go.GetComponent<BoxCollider>());
            return go;
        }

        /// <summary>A launch pad (M7) whose volume rests on `surface`, throwing toward `facing`'s forward.</summary>
        LaunchPad MakeLaunchPad(string name, Vector3 surface, Quaternion facing, Vector3 volume, Vector3 throwTo, float clearance,
                                float period, bool sameThrow, Transform key)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(surface, facing);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, volume.y * 0.5f, 0f);
            box.size = volume;
            var pad = go.AddComponent<LaunchPad>();
            pad.Configure(throwTo, clearance, period, 0f, sameThrow, key);
            return pad;
        }

        /// <summary>A pioneer bell: a post, a cup that swings when rung, and a reach sphere for the interact key.</summary>
        void MakeBell(int section, Vector3 at)
        {
            Box($"7g Bell Post S{section}", at + Vector3.up * 0.8f, new Vector3(0.15f, 1.6f, 0.15f), wallMaterial);
            var go = new GameObject($"7g Bell S{section}");
            go.transform.SetParent(root, false);
            go.transform.position = at;
            var pivot = new GameObject("Swing").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(-0.3f, 1.55f, 0f);
            var cup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(cup.GetComponent<Collider>());
            cup.name = "Cup";
            cup.transform.SetParent(pivot, false);
            cup.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            cup.transform.localScale = new Vector3(0.45f, 0.22f, 0.45f);
            cup.GetComponent<MeshRenderer>().sharedMaterial = bellMaterial;
            var reach = go.AddComponent<SphereCollider>();
            reach.isTrigger = true;
            reach.center = new Vector3(-0.3f, 1.2f, 0f);
            reach.radius = 0.7f;
            go.AddComponent<Bell>().Configure(section, pivot);
        }

        void MakeMaterials()
        {
            // Borrow the arena's own look where it exists (the grid floor and the grey walls).
            floorMaterial = SceneMaterial("Floor 30x30") ?? NewMaterial(new Color(0.88f, 0.88f, 0.86f));
            wallMaterial = SceneMaterial("Wall 2m") ?? NewMaterial(new Color(0.62f, 0.64f, 0.7f));
            platformMaterial = Tinted(floorMaterial, new Color(0.95f, 0.78f, 0.35f));
            crateMaterial = Tinted(floorMaterial, new Color(0.72f, 0.5f, 0.3f));
            padMaterial = Tinted(floorMaterial, new Color(0.45f, 0.85f, 0.5f));
            waterMaterial = NewMaterial(new Color(0.2f, 0.45f, 0.9f));
            lightMaterial = NewMaterial(new Color(0.55f, 0.85f, 1f));
            bellMaterial = NewMaterial(new Color(0.95f, 0.75f, 0.2f));
            ropeMaterial = NewMaterial(new Color(0.55f, 0.38f, 0.2f));
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 64);
        }

        static Material SceneMaterial(string objectName)
        {
            var go = GameObject.Find(objectName);
            var renderer = go != null ? go.GetComponent<Renderer>() : null;
            return renderer != null ? renderer.sharedMaterial : null;
        }

        static Material NewMaterial(Color color)
        {
            var shader = Shader.Find("Standard");
            var m = new Material(shader != null ? shader : Shader.Find("Legacy Shaders/Diffuse")) { color = color };
            return m;
        }

        static Material Tinted(Material source, Color color)
        {
            var m = source != null ? new Material(source) : NewMaterial(color);
            m.color = color;
            return m;
        }

        GameObject Box(string name, Vector3 center, Vector3 size, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = center;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>A box that moves: the object is kept inactive until configured, because the platform
        /// evaluates its first pose in Awake.</summary>
        MovingPlatform Platform(string name, Vector3 center, Vector3 size, Vector3 travel, float speed, float pause, Material material)
        {
            var go = Box(name, center, size, material);
            go.SetActive(false);
            var platform = go.AddComponent<MovingPlatform>();
            platform.Configure(travel, speed, pause, Mathf.Max(0.5f, speed / 6f));
            go.SetActive(true);
            return platform;
        }

        MovingPlatform MakeDisc()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "7c Turntable";
            go.SetActive(false);
            go.transform.SetParent(root, false);
            go.transform.position = DiscCenter;
            go.transform.localScale = new Vector3(DiscRadius * 2f, DiscHeight * 0.5f, DiscRadius * 2f);
            // The primitive's capsule collider is rounded top and bottom; a disc needs a flat top.
            var capsule = go.GetComponent<Collider>();
            if (capsule != null) DestroyImmediate(capsule);
            var mesh = go.AddComponent<MeshCollider>();
            mesh.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mesh.convex = true;
            go.GetComponent<MeshRenderer>().sharedMaterial = platformMaterial;
            var platform = go.AddComponent<MovingPlatform>();
            platform.Configure(Vector3.zero, 0f, 0f, 0f, DiscSpin, Vector3.up);
            go.SetActive(true);
            // A marker stripe so the turning shows.
            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(stripe.GetComponent<Collider>());
            stripe.name = "Stripe";
            stripe.transform.SetParent(go.transform, false);
            stripe.transform.localPosition = new Vector3(0.25f, 1.01f, 0f);
            stripe.transform.localScale = new Vector3(0.5f, 0.02f, 0.06f);
            stripe.GetComponent<MeshRenderer>().sharedMaterial = crateMaterial;
            return platform;
        }

        void Label(string text, Vector3 position, float size)
        {
            var go = new GameObject("Label " + text);
            go.transform.SetParent(root, false);
            // Lying on the floor, readable walking south into the test bed.
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 180f, 0f));
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.fontSize = 64;
            mesh.characterSize = size * 0.12f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(0.16f, 0.18f, 0.24f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
        }

        /// <summary>Box mesh with UVs in world metres, like the arena's (the grid repeats every 2 m).</summary>
        static Mesh BoxMesh(Vector3 size)
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
            var mesh = new Mesh { name = "TestBedBox" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------------- water (M4)

        /// <summary>Stage-one rule: in the water, back on the checkpoint after the water's delay. Only the
        /// machine that simulates a pawn puts it back; a network puppet is moved by its host.</summary>
        void OnWater(ICharacterDriver who, WaterZone zone)
        {
            if (zone != Water || !(who is RagdollDriver driver)) return;
            var pawn = driver.Pawn;
            if (pawn == null || pawn.NetworkPuppet) return;
            LogWater(pawn, drowning.ContainsKey(pawn) ? "물(이미 빠짐)" : "물");
            if (drowning.ContainsKey(pawn)) return;
            drowning[pawn] = Time.time + zone.RespawnDelay;
        }

        void LogWater(RagdollPawn pawn, string what)
        {
            WaterLog.Add($"{Time.time:0.00}s {pawn.DisplayName} {what}");
            if (WaterLog.Count > 20) WaterLog.RemoveAt(0);
        }

        void Update()
        {
            RespawnFromWater();
            if (game == null || game.AutoTest || game.NetworkControlled || game.SuppressInput) return;
            var pawn = game.players.Length > 0 ? game.players[0].pawn : null;
            if (pawn == null) return;
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) GoToPromotion(pawn);
                else GoToEntrance(pawn);
            }
            if (Input.GetKeyDown(KeyCode.F8))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) GoToRopes(pawn);
                else GoToHookRange(pawn);
            }
            if (Input.GetKeyDown(KeyCode.F10))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) GoToLaunchPads(pawn);
                else GoToTower(pawn);
            }
            if (Input.GetKeyDown(KeyCode.F11) && Match != null)
            {
                Match.ResetRound();
                Note("F11 종 초기화 (모든 구간 닫힘, 체크포인트 지움)");
            }
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.F5) && shift) Status(pawn, true);
            else if (Input.GetKeyDown(KeyCode.F5))
                Hit(pawn, (-pawn.Facing * 0.87f + Vector3.up * 0.5f).normalized * 6f, 1f, 0f, false, "F5 피격: 6 m/s · 1초 넘어짐");
            if (Input.GetKeyDown(KeyCode.F6) && shift) Status(pawn, false);
            else if (Input.GetKeyDown(KeyCode.F6)) Hit(pawn, Vector3.zero, 0f, 2.5f, false, "F6 스테미나 -2.5");
            if (Input.GetKeyDown(KeyCode.F7))
                Hit(pawn, -pawn.Facing * 2f + Vector3.up, 0f, 0f, true, "F7 벽·탈것에서 떨어뜨리기");
        }

        void RespawnFromWater()
        {
            if (drowning.Count == 0) return;
            due.Clear();
            foreach (var pair in drowning)
                if (pair.Key == null || Time.time >= pair.Value) due.Add(pair.Key);
            foreach (var pawn in due)
            {
                drowning.Remove(pawn);
                if (pawn == null) continue;
                // Through the game's own contract, which has to let go of everything on the way out.
                var driver = pawn.GetComponent<RagdollDriver>();
                Quaternion face = Quaternion.LookRotation(Vector3.back);
                Vector3 to = Checkpoint;
                // Queen of the Hill (M9): the higher of its own landing and one below its team's best, if any.
                if (Match != null && driver != null && Match.TryRespawn(driver, pawn.Team, out var landing, out var facing))
                {
                    to = landing;
                    face = facing;
                }
                if (driver != null) driver.Teleport(to, face);
                else pawn.Teleport(to + Vector3.up * (pawn.standHeight + 0.02f), face * Vector3.forward);
                WaterRespawns++;
                LastWater = pawn.DisplayName;
                LogWater(pawn, "부활");
            }
        }

        /// <summary>True while the water is about to put this pawn back.</summary>
        public bool IsDrowning(RagdollPawn pawn) => pawn != null && drowning.ContainsKey(pawn);

        float DrowningLeft(RagdollPawn pawn) => drowning.TryGetValue(pawn, out float at) ? Mathf.Max(0f, at - Time.time) : 0f;

        public void GoToEntrance(RagdollPawn pawn)
        {
            var driver = pawn.GetComponent<RagdollDriver>();
            if (driver != null) driver.Teleport(Entrance, Quaternion.LookRotation(Vector3.back));
            else pawn.Teleport(Entrance + Vector3.up * (pawn.standHeight + 0.02f), Vector3.back);
            Note("F9 시험대로 이동");
        }

        public void GoToTower(RagdollPawn pawn)
        {
            var driver = pawn.GetComponent<RagdollDriver>();
            if (driver != null) driver.Teleport(TowerStart, Quaternion.LookRotation(Vector3.right));
            else pawn.Teleport(TowerStart + Vector3.up * (pawn.standHeight + 0.02f), Vector3.right);
            Note("F10 개척의 탑으로 이동");
        }

        public void GoToPromotion(RagdollPawn pawn)
        {
            var driver = pawn.GetComponent<RagdollDriver>();
            if (driver != null) driver.Teleport(PromotionStart, Quaternion.LookRotation(Vector3.back));
            else pawn.Teleport(PromotionStart + Vector3.up * (pawn.standHeight + 0.02f), Vector3.back);
            Note("Shift+F9 승격 받침대로 이동");
        }

        public void GoToRopes(RagdollPawn pawn)
        {
            var driver = pawn.GetComponent<RagdollDriver>();
            if (driver != null) driver.Teleport(RopeStart, Quaternion.LookRotation(Vector3.right));
            else pawn.Teleport(RopeStart + Vector3.up * (pawn.standHeight + 0.02f), Vector3.right);
            Note("Shift+F8 밧줄로 이동");
        }

        public void GoToLaunchPads(RagdollPawn pawn)
        {
            var driver = pawn.GetComponent<RagdollDriver>();
            if (driver != null) driver.Teleport(LaunchStart, Quaternion.LookRotation(Vector3.right));
            else pawn.Teleport(LaunchStart + Vector3.up * (pawn.standHeight + 0.02f), Vector3.right);
            Note("Shift+F10 도약대로 이동");
        }

        public void GoToHookRange(RagdollPawn pawn)
        {
            var driver = pawn.GetComponent<RagdollDriver>();
            if (driver != null) driver.Teleport(HookStart, Quaternion.LookRotation(Vector3.right));
            else pawn.Teleport(HookStart + Vector3.up * (pawn.standHeight + 0.02f), Vector3.right);
            Note("F8 갈고리 연습장으로 이동 (E로 갈고리 꺼내기)");
        }

        /// <summary>A hit through IHitReceiver, the way every attack will reach a character (M3).</summary>
        void Hit(RagdollPawn pawn, Vector3 push, float knockdown, float stamina, bool drop, string what)
        {
            var receiver = pawn.GetComponent<IHitReceiver>();
            if (receiver != null) receiver.ApplyHit(push, knockdown, stamina, drop);
            else pawn.TakeHit(push, knockdown, stamina, drop);
            Note(what);
        }

        /// <summary>A status effect through IStatusReceiver (M13): the knight's squash or the stones' stagger.</summary>
        void Status(RagdollPawn pawn, bool squash)
        {
            var receiver = pawn.GetComponent<IStatusReceiver>();
            bool took = receiver != null && (squash ? receiver.Squash(1.2f, 1f) : receiver.Stagger(0.4f));
            Note((squash ? "Shift+F5 찌그러짐 1.2초 (뒤 1초 면역)" : "Shift+F6 비틀 0.4초") + (took ? "" : " → 안 먹힘(면역)"));
        }

        static string StatusText(RagdollPawn pawn) =>
            pawn.Squashed ? $" · <b>찌그러짐 {pawn.SquashLeft:0.0}초</b>"
            : pawn.Staggered ? $" · <b>비틀 {pawn.StaggerLeft:0.0}초</b>"
            : pawn.SquashImmuneLeft > 0f ? $" · 찌그러짐 면역 {pawn.SquashImmuneLeft:0.0}초" : "";

        void Note(string what)
        {
            lastAction = what;
            lastActionAt = Time.unscaledTime;
        }

        // ---------------------------------------------------------------- readout (M2)

        string TowerText(RagdollPawn pawn)
        {
            if (Match == null) return "";
            string Section(int s) => !Match.IsOpen(s) ? "닫힘"
                : Teams.Name(Match.Pioneer(s)) + " 열음" + (Match.Exclusive(s) ? $" (독점 {Match.ExclusiveLeft(s):0}초)" : "");
            var driver = pawn.GetComponent<RagdollDriver>();
            int back = driver != null ? Match.Rules.RespawnSection(QueenHillMatch.Id(driver), pawn.Team) : 0;
            return $"[7g] 종 S1 {Section(1)} · S2 {Section(2)} · 물에 빠지면 {(back > 0 ? $"S{back} 착지대" : "7e 체크포인트")}로 "
                   + $"(내 최고 S{(driver != null ? Match.PersonalBest(driver) : 0)}, 팀 최고 S{Match.TeamBest(pawn.Team)})";
        }

        string LaunchText(RagdollPawn pawn)
        {
            if (Spring == null || Flat(pawn.Hips.position - SpringCenter).magnitude > 12f) return "";
            return $"\n[7h] 태엽 발사까지 {Spring.SecondsToFire:0.0}초 · 도약대 발사 {pawn.Launches}회{(pawn.Launched ? " (<b>날아가는 중</b>)" : "")}";
        }

        string RopeText(RagdollPawn pawn)
        {
            if (Swing == null || Mathf.Abs(pawn.Hips.position.z + 70f) > 10f || pawn.Hips.position.x < 30f) return "";
            string on = pawn.OnRope ? $"<b>매달림</b> (위에서 {pawn.RopeDown:0.0} m, 스테미나 {pawn.Stamina * 100f:0}%)" : "-";
            return $"\n[7i] 밧줄: {on} · 잡음 {pawn.RopeGrabs} · 점프로 놓음 {pawn.RopeJumps} · 놓음 {pawn.RopeDrops} · 올라섬 {pawn.RopeTopOuts}";
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static string HookText(HookPhase phase) => phase switch
        {
            HookPhase.Held => "손에 듦",
            HookPhase.Charging => "돌리는 중",
            HookPhase.Flying => "날아가는 중",
            HookPhase.Pulling => "<b>끌려가는 중</b>",
            HookPhase.Stuck => "박혀 있음",
            _ => "넣어 둠 (E)",
        };

        void OnGUI()
        {
            if (game == null || game.AutoTest || game.PanelOpen) return;
            var pawn = game.players.Length > 0 ? game.players[0].pawn : null;
            if (pawn == null) return;
            if (style == null)
            {
                var ui = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 13);
                style = new GUIStyle(GUI.skin.label) { font = ui, fontSize = 13, richText = true, wordWrap = false };
            }
            Vector3 aim = pawn.Aim;
            string text =
                "<b>[7] 퀸 오브 더 힐 시험대</b>  F9 이동 · F8 갈고리 연습장 · F10 개척의 탑 · F11 종 초기화 · Shift+F10 도약대 · Shift+F8 밧줄 · Shift+F9 승격 · F5 피격 6 m/s·1초 · F6 스테미나 -2.5 · F7 떨어뜨리기 · Shift+F5 찌그러짐 · Shift+F6 비틀\n" +
                $"받은 입력(P1): 능력 E <b>{pawn.AbilityPresses}</b>회 · 능력2 Q <b>{pawn.Ability2Presses}</b>회 · " +
                $"상호작용 F <b>{pawn.InteractPresses}</b>회{(pawn.InteractHeld ? " (누르는 중)" : "")} · " +
                $"전력질주 {(pawn.SprintHeld ? "●" : "○")} · 조준 ({aim.x:+0.00;-0.00}, {aim.y:+0.00;-0.00}, {aim.z:+0.00;-0.00})\n" +
                $"탈것: {(pawn.Riding ? "<b>타는 중</b>" : "-")} · 발밑 기준 {pawn.GroundSpeed:0.0} m/s (전체 {pawn.HorizontalSpeed:0.0}) · " +
                $"피격 {pawn.Hits}회: {pawn.LastHit}{StatusText(pawn)} · 물에서 부활 {WaterRespawns}회\n" +
                $"팀 {Teams.Name(pawn.Team)} · 기물 <b>{ChessPieces.Name(pawn.Piece)}</b> · 갈고리: {HookText(pawn.Hook)} · 던짐 {pawn.HookThrows} · 박힘 {pawn.HookHits} · 빗나감 {pawn.HookMisses} · " +
                $"도착 {pawn.HookArrivals} · 앙파상 성공 {pawn.EnPassantCuts}/당함 {pawn.HookCutOff} · 마지막: {pawn.LastHookEvent}\n" +
                TowerText(pawn) + LaunchText(pawn) + RopeText(pawn) +
                (IsDrowning(pawn) ? $" · <b>물에 빠짐! {DrowningLeft(pawn):0.0}초 뒤 체크포인트로</b> (좌클릭 버둥 {pawn.Thrashes}회)" : "") +
                (Time.unscaledTime - lastActionAt < 2.5f ? $"\n→ {lastAction}" : "");
            var size = style.CalcSize(new GUIContent(text));
            var rect = new Rect(8f, Screen.height - size.y - 20f, size.x + 16f, size.y + 12f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, size.x, size.y), text, style);
        }
    }
}
