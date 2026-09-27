using System;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.Gameplay
{
    // The Queen of the Hill map, "sky palace" (DESIGN §3, 7th revision, R49), as a
    // GRAYBOX: plain shapes in the signal colours of §3.9, at the real scale. Seven
    // floors, one per rank of the chessboard, and the summit, rank 8, where a pawn
    // promotes (QueenHillCourse).
    //
    // The map is DATA. Tools/QueenHill/build_layout.py writes
    // Assets/Resources/QueenHill/QueenHillLayout.json and this component builds it
    // when the scene starts: a list of pieces, each a box, a cylinder, a group, a
    // trigger or a sign, optionally moving on the shared clock (MovingPlatform,
    // OrbitPlatform, Oscillator, Pendulum, Spinner, PhaseToggle) and optionally doing
    // a job (a bell, a light-column path, a checkpoint, a spawn point, the hook start
    // zone, the sea, a launch pad, a rope, a promotion pedestal). Change the map in
    // the script, never in the JSON; Tools/QueenHill/preview renders it without Unity.
    //
    // Keep the level object at the origin and unrotated: water and hook zones are
    // read as axis-aligned boxes.
    [DefaultExecutionOrder(-500)]
    public sealed class QueenHillLevel : MonoBehaviour
    {
        [Tooltip("The map (Tools/QueenHill/build_layout.py). Empty: Resources/QueenHill/QueenHillLayout.")]
        [SerializeField] TextAsset layout;
        [Tooltip("Seconds an opened path belongs to the team that opened it.")]
        [SerializeField] float exclusiveSeconds = (float)QueenHillRules.DefaultExclusiveSeconds;
        [Tooltip("Words on the floor naming each floor and its ways up.")]
        [SerializeField] bool signs = true;

        public static QueenHillLevel Current { get; private set; }

        public QueenHillMatch Match { get; private set; }
        public int Pieces { get; private set; }

        Transform root;
        readonly Dictionary<string, GameObject> named = new Dictionary<string, GameObject>();
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        readonly List<GameObject> movers = new List<GameObject>();
        readonly List<(GameObject group, int section)> paths = new List<(GameObject, int)>();
        readonly List<Object> owned = new List<Object>();
        Texture2D checkerTexture;

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

        // ---------------------------------------------------------------- data

        [Serializable]
        public sealed class LayoutFile
        {
            public int version;
            public float[] ranks;
            public int floors;
            public LayoutPiece[] pieces;
        }

        // One piece of the map. Short field names keep the file small; see build_layout.py.
        [Serializable]
        public sealed class LayoutPiece
        {
            public string n;    // name (unique)
            public string k;    // box, vbox (only looks), ibox (only collides), cyl, vcyl, group, trig, sign
            public string g;    // parent piece's name; position and rotation are then local to it
            public float[] p;   // position
            public float[] s;   // size (a cylinder: diameter, height, diameter)
            public float[] r;   // rotation, Euler degrees
            public string c;    // colour
            public string m;    // motion: move, orbit, osc, pend, spin, toggle
            public float[] a;   // motion arguments
            public string f;    // job: bell, path, checkpoint, spawn, hookzone, water, launch, rope, promote, sign
            public float[] b;   // job arguments
            public string t;    // text (a sign) or a colour (a rope)
        }

        static Vector3 V(float[] v, int i = 0, float fallback = 0f) =>
            v != null && v.Length >= i + 3 ? new Vector3(v[i], v[i + 1], v[i + 2]) : new Vector3(fallback, fallback, fallback);

        static float F(float[] v, int i, float fallback = 0f) => v != null && v.Length > i ? v[i] : fallback;

        // ---------------------------------------------------------------- building

        void Build()
        {
            if (root != null) return;
            var asset = layout != null ? layout : Resources.Load<TextAsset>("QueenHill/QueenHillLayout");
            if (asset == null)
            {
                Debug.LogError("[ChessFight] 퀸 오브 더 힐 맵 데이터가 없습니다: Assets/Resources/QueenHill/QueenHillLayout.json " +
                               "(python3 Tools/QueenHill/build_layout.py 로 만든다).");
                return;
            }
            var data = JsonUtility.FromJson<LayoutFile>(asset.text);
            if (data == null || data.pieces == null)
            {
                Debug.LogError("[ChessFight] 퀸 오브 더 힐 맵 데이터를 읽지 못했습니다.");
                return;
            }
            CheckHeights(data);

            root = new GameObject("Built Level").transform;
            root.SetParent(transform, false);
            Atmosphere();

            Match = GetComponent<QueenHillMatch>();
            if (Match == null) Match = gameObject.AddComponent<QueenHillMatch>();
            Match.Configure(QueenHillCourse.Sections, exclusiveSeconds);

            foreach (var piece in data.pieces)
            {
                try { Make(piece); }
                catch (Exception e) { Debug.LogError("[ChessFight] 맵 조각 '" + piece.n + "': " + e.Message); }
            }
            // Moving pieces were kept inactive until all their children were in place:
            // an obstacle takes its start pose in Awake.
            foreach (var go in movers) go.SetActive(true);
            foreach (var (group, section) in paths)
            {
                var controller = new GameObject(group.name + " (Open Path)");
                controller.transform.SetParent(root, false);
                controller.AddComponent<OpenPath>().Configure(section, group);
            }
            Pieces = data.pieces.Length;
        }

        static void CheckHeights(LayoutFile data)
        {
            if (data.ranks == null || data.ranks.Length != QueenHillCourse.Ranks)
            {
                Debug.LogWarning("[ChessFight] 맵 데이터의 랭크 수가 QueenHillCourse와 다릅니다.");
                return;
            }
            for (int r = 1; r <= QueenHillCourse.Ranks; r++)
                if (Mathf.Abs(data.ranks[r - 1] - QueenHillCourse.RankHeight(r)) > 0.01f)
                    Debug.LogWarning($"[ChessFight] 맵 데이터의 {r}랭크 높이 {data.ranks[r - 1]}가 QueenHillCourse({QueenHillCourse.RankHeight(r)})와 다릅니다.");
        }

        void Make(LayoutPiece piece)
        {
            Transform parent = root;
            if (!string.IsNullOrEmpty(piece.g) && named.TryGetValue(piece.g, out var p)) parent = p.transform;
            GameObject go;
            switch (piece.k)
            {
                case "box": go = Box(piece, collide: true, show: true); break;
                case "vbox": go = Box(piece, collide: false, show: true); break;
                case "ibox": go = Box(piece, collide: true, show: false); break;
                case "cyl": go = Cylinder(piece, collide: true); break;
                case "vcyl": go = Cylinder(piece, collide: false); break;
                default: go = new GameObject(piece.n); break;    // group, trig, sign
            }
            go.name = piece.n;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = V(piece.p);
            go.transform.localRotation = Quaternion.Euler(V(piece.r));
            named[piece.n] = go;
            if (!string.IsNullOrEmpty(piece.m)) Move(go, piece);
            if (!string.IsNullOrEmpty(piece.f)) Job(go, piece);
        }

        void Move(GameObject go, LayoutPiece piece)
        {
            float[] a = piece.a;
            switch (piece.m)
            {
                case "move":
                    go.SetActive(false);
                    go.AddComponent<MovingPlatform>().Configure(V(a), F(a, 3), F(a, 4), F(a, 5, 0.5f), F(a, 6), V(a, 7), F(a, 10));
                    movers.Add(go);
                    break;
                case "orbit":
                    go.SetActive(false);
                    go.AddComponent<OrbitPlatform>().Configure(F(a, 0), F(a, 1, 30f), V(a, 3), F(a, 2));
                    movers.Add(go);
                    break;
                case "osc":
                {
                    go.SetActive(false);
                    var osc = go.AddComponent<Oscillator>();
                    osc.Configure(V(a), F(a, 3, 4f));
                    osc.SetPhase(F(a, 4));
                    movers.Add(go);
                    break;
                }
                case "pend":
                {
                    go.SetActive(false);
                    var pendulum = go.AddComponent<Pendulum>();
                    pendulum.Configure(V(a), F(a, 3, 30f), F(a, 4, 4f));
                    pendulum.SetPhase(F(a, 5));
                    movers.Add(go);
                    break;
                }
                case "spin":
                {
                    go.SetActive(false);
                    var spinner = go.AddComponent<Spinner>();
                    spinner.Configure(V(a), F(a, 3, 20f));
                    spinner.SetPhase(F(a, 4));
                    movers.Add(go);
                    break;
                }
                case "toggle":
                    go.AddComponent<PhaseToggle>().Configure(F(a, 0, 4f), F(a, 1), F(a, 2, 0.7f), F(a, 3));
                    break;
            }
        }

        void Job(GameObject go, LayoutPiece piece)
        {
            float[] b = piece.b;
            switch (piece.f)
            {
                case "path":
                    paths.Add((go, Mathf.RoundToInt(F(b, 0, 1))));
                    break;
                case "bell":
                    MakeBell(go, Mathf.RoundToInt(F(b, 0, 1)));
                    break;
                case "checkpoint":
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.size = V(piece.s, 0, 1f);
                    var spawn = new GameObject("Spawn").transform;
                    spawn.SetParent(go.transform, false);
                    spawn.position = root.TransformPoint(V(b, 1));
                    spawn.rotation = root.rotation * Quaternion.Euler(0f, F(b, 4), 0f);
                    go.AddComponent<SectionCheckpoint>().Configure(Mathf.RoundToInt(F(b, 0, 1)), spawn);
                    break;
                }
                case "spawn":
                    go.AddComponent<SpawnPoint>().Configure(Mathf.RoundToInt(F(b, 0)), Mathf.RoundToInt(F(b, 1)));
                    break;
                case "hookzone":
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.size = V(piece.s, 0, 1f);
                    go.AddComponent<HookStartZone>();
                    break;
                }
                case "water":
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.size = V(piece.s, 0, 1f);
                    go.AddComponent<WaterZone>();
                    break;
                }
                case "launch":
                {
                    Vector3 size = V(piece.s, 0, 1f);
                    var box = go.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.center = new Vector3(0f, size.y * 0.5f, 0f);
                    box.size = size;
                    go.AddComponent<LaunchPad>().Configure(V(b), F(b, 3, 1.2f), F(b, 4), F(b, 5), F(b, 6) > 0.5f);
                    break;
                }
                case "rope":
                {
                    go.SetActive(false);
                    go.AddComponent<RopeLine>().Configure(F(b, 0, 6f), F(b, 1), F(b, 2, 5f), F(b, 3), F(b, 4, 0.1f),
                        Material(string.IsNullOrEmpty(piece.t) ? "chain" : piece.t));
                    go.SetActive(true);
                    break;
                }
                case "promote":
                {
                    var reach = go.AddComponent<SphereCollider>();
                    reach.isTrigger = true;
                    reach.center = new Vector3(0f, 0.9f, 0f);
                    reach.radius = 0.7f;
                    go.AddComponent<PromotionPad>().Configure((PieceKind)Mathf.RoundToInt(F(b, 0, 1)));
                    break;
                }
                case "sign":
                    if (signs) MakeSign(go, piece.t, F(b, 0, 0.5f));
                    break;
            }
        }

        void MakeBell(GameObject go, int section)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "Post";
            post.transform.SetParent(go.transform, false);
            post.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            post.transform.localScale = new Vector3(0.15f, 1.6f, 0.15f);
            post.GetComponent<MeshRenderer>().sharedMaterial = Material("marble");
            var pivot = new GameObject("Swing").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(-0.3f, 1.55f, 0f);
            var cup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(cup.GetComponent<Collider>());
            cup.name = "Cup";
            cup.transform.SetParent(pivot, false);
            cup.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            cup.transform.localScale = new Vector3(0.45f, 0.22f, 0.45f);
            cup.GetComponent<MeshRenderer>().sharedMaterial = Material("gold");
            var reach = go.AddComponent<SphereCollider>();
            reach.isTrigger = true;
            reach.center = new Vector3(-0.3f, 1.2f, 0f);
            reach.radius = 0.7f;
            go.AddComponent<Bell>().Configure(section, pivot);
        }

        void MakeSign(GameObject go, string text, float size)
        {
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text ?? "";
            mesh.font = RuntimePanels.KoreanFont != null ? RuntimePanels.KoreanFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.fontSize = 64;
            mesh.characterSize = size * 0.12f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(0.2f, 0.16f, 0.1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
        }

        GameObject Box(LayoutPiece piece, bool collide, bool show)
        {
            Vector3 size = V(piece.s, 0, 1f);
            var go = new GameObject(piece.n);
            if (show)
            {
                go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
                go.AddComponent<MeshRenderer>().sharedMaterial = Material(piece.c);
            }
            if (collide) go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        GameObject Cylinder(LayoutPiece piece, bool collide)
        {
            Vector3 size = V(piece.s, 0, 1f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            // The primitive's capsule collider is rounded top and bottom; a disc needs a flat top.
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(size.x, size.y * 0.5f, size.z);
            go.GetComponent<MeshRenderer>().sharedMaterial = Material(piece.c);
            if (collide)
            {
                var mesh = go.AddComponent<MeshCollider>();
                mesh.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                mesh.convex = true;
            }
            return go;
        }

        // ---------------------------------------------------------------- look

        void Atmosphere()
        {
            var sky = new Color(0.64f, 0.79f, 0.93f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sky;
            RenderSettings.fogStartDistance = 180f;
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

        // The signal colours (DESIGN §3.9): green rests, gold moves, lapis-and-gold chequer is
        // exposed, cream marble is a wall to climb, pale blue light is an opened path, ruby hits.
        // Tools/QueenHill/preview/index.html uses the same table.
        static readonly Dictionary<string, Color> Colours = new Dictionary<string, Color>
        {
            { "marble", new Color(0.93f, 0.88f, 0.78f) }, { "honey", new Color(0.88f, 0.76f, 0.55f) },
            { "cliff", new Color(0.84f, 0.77f, 0.64f) }, { "garden", new Color(0.42f, 0.66f, 0.44f) },
            { "cypress", new Color(0.18f, 0.4f, 0.26f) }, { "gold", new Color(0.93f, 0.74f, 0.3f) },
            { "lapis", new Color(0.2f, 0.33f, 0.66f) }, { "palegold", new Color(0.93f, 0.85f, 0.6f) },
            { "light", new Color(0.72f, 0.93f, 1f) }, { "sea", new Color(0.12f, 0.56f, 0.62f) },
            { "stone", new Color(0.72f, 0.67f, 0.6f) }, { "rock", new Color(0.56f, 0.52f, 0.48f) },
            { "waterfall", new Color(0.84f, 0.95f, 1f) }, { "chain", new Color(0.85f, 0.66f, 0.25f) },
            { "niche", new Color(0.78f, 0.7f, 0.58f) }, { "ruby", new Color(0.72f, 0.2f, 0.26f) },
            { "cloud", new Color(0.97f, 0.98f, 1f) }, { "rope", new Color(0.6f, 0.45f, 0.3f) },
        };

        Material Material(string key)
        {
            if (string.IsNullOrEmpty(key)) key = "marble";
            if (materials.TryGetValue(key, out var m)) return m;
            var shader = Shader.Find("Standard");
            m = new Material(shader != null ? shader : Shader.Find("Legacy Shaders/Diffuse")) { name = "QueenHill " + key };
            if (key == "checker")
            {
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
                m.mainTexture = checkerTexture;
                m.color = Color.white;
            }
            else m.color = Colours.TryGetValue(key, out var c) ? c : Color.magenta;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.12f);
            owned.Add(m);
            materials[key] = m;
            return m;
        }

        // Box mesh with UVs in world metres (one unit per two metres).
        Mesh BoxMesh(Vector3 size)
        {
            Vector3 h = size * 0.5f;
            var vertices = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var tris = new List<int>(36);
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
