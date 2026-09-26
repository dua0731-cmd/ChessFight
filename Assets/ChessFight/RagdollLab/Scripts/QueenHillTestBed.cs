using System.Collections.Generic;
using ChessFight.Gameplay;
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
    ///
    /// Offline hotkeys for P1: F9 walk-in point, F5 a 6 m/s hit with a 1 s knockdown, F6 stamina -2.5,
    /// F7 knocked off the wall (M3). A corner box shows what the pawn received from the new keys (M2).
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

        public MovingPlatform Shuttle { get; private set; }
        public MovingPlatform Lift { get; private set; }
        public MovingPlatform Disc { get; private set; }
        public MovingPlatform SlideWall { get; private set; }
        public WaterZone Water { get; private set; }
        public Rigidbody Crate { get; private set; }

        /// <summary>Pawns put back on the checkpoint by the water so far, and the last one.</summary>
        public int WaterRespawns { get; private set; }
        public string LastWater { get; private set; } = "-";

        /// <summary>The last few water events ("12.3s 더미 1 물" / "부활"), for the automated checks.</summary>
        public readonly List<string> WaterLog = new List<string>();

        readonly Dictionary<RagdollPawn, float> drowning = new Dictionary<RagdollPawn, float>();
        readonly List<RagdollPawn> due = new List<RagdollPawn>();
        Transform root;
        Material floorMaterial, wallMaterial, platformMaterial, waterMaterial, crateMaterial, padMaterial;
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
            if (Input.GetKeyDown(KeyCode.F9)) GoToEntrance(pawn);
            if (Input.GetKeyDown(KeyCode.F5))
                Hit(pawn, (-pawn.Facing * 0.87f + Vector3.up * 0.5f).normalized * 6f, 1f, 0f, false, "F5 피격: 6 m/s · 1초 넘어짐");
            if (Input.GetKeyDown(KeyCode.F6)) Hit(pawn, Vector3.zero, 0f, 2.5f, false, "F6 스테미나 -2.5");
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
                if (driver != null) driver.Teleport(Checkpoint, face);
                else pawn.Teleport(Checkpoint + Vector3.up * (pawn.standHeight + 0.02f), Vector3.back);
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

        /// <summary>A hit through IHitReceiver, the way every attack will reach a character (M3).</summary>
        void Hit(RagdollPawn pawn, Vector3 push, float knockdown, float stamina, bool drop, string what)
        {
            var receiver = pawn.GetComponent<IHitReceiver>();
            if (receiver != null) receiver.ApplyHit(push, knockdown, stamina, drop);
            else pawn.TakeHit(push, knockdown, stamina, drop);
            Note(what);
        }

        void Note(string what)
        {
            lastAction = what;
            lastActionAt = Time.unscaledTime;
        }

        // ---------------------------------------------------------------- readout (M2)

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
                "<b>[7] 퀸 오브 더 힐 시험대</b>  F9 이동 · F5 피격 6 m/s·1초 · F6 스테미나 -2.5 · F7 떨어뜨리기\n" +
                $"받은 입력(P1): 능력 E <b>{pawn.AbilityPresses}</b>회 · 능력2 Q <b>{pawn.Ability2Presses}</b>회 · " +
                $"상호작용 F <b>{pawn.InteractPresses}</b>회{(pawn.InteractHeld ? " (누르는 중)" : "")} · " +
                $"전력질주 {(pawn.SprintHeld ? "●" : "○")} · 조준 ({aim.x:+0.00;-0.00}, {aim.y:+0.00;-0.00}, {aim.z:+0.00;-0.00})\n" +
                $"탈것: {(pawn.Riding ? "<b>타는 중</b>" : "-")} · 발밑 기준 {pawn.GroundSpeed:0.0} m/s (전체 {pawn.HorizontalSpeed:0.0}) · " +
                $"피격 {pawn.Hits}회: {pawn.LastHit} · 물에서 부활 {WaterRespawns}회" +
                (IsDrowning(pawn) ? $" · <b>물에 빠짐! {DrowningLeft(pawn):0.0}초 뒤 체크포인트로</b> (좌클릭 버둥 {pawn.Thrashes}회)" : "") +
                (Time.unscaledTime - lastActionAt < 2.5f ? $"\n→ {lastAction}" : "");
            var size = style.CalcSize(new GUIContent(text));
            var rect = new Rect(8f, Screen.height - size.y - 20f, size.x + 16f, size.y + 12f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, size.x, size.y), text, style);
        }
    }
}
