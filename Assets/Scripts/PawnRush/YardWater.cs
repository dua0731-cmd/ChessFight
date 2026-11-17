using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    // R684 (승규 11-16 "물 그래픽도 수학적으로 계산해서 ... 그래픽 좋게 넣어"): the Royal Carriage Yard's water look, made by
    // RoyalCarriageYard.Water.cs under "Water look" beside "Built". It owns
    //  - the surfaces: each code water box (the canal, the flume's trough, the mill pond) stops drawing and a grid of
    //    up faces over its top takes its place in PR_YardWater (Gerstner swell moved in the vertex shader, so it needs
    //    vertices: 0.5 / 0.8 / 1.6 m apart by quality tier), each with its own current;
    //  - the falling water (YardWaterfall, one per place), which it shows and hides;
    //  - the shader's globals: every fall's landing lines (_RcyFallLines, up to 16) and the last 8 splashes of pawns
    //    falling in (_RcySplashes, from YardWaterSplash), set once a frame after the falls have moved.
    // Switching this object off puts the old flat water and scrolling sheets back at once; on again, the new ones.
    // Quality: the same tiers as the yard's light (YardAtmosphere: the settings window's 낮음 / 중간 / 높음, a custom
    // preset by its shadow step, the 'Mobile' quality level always low), capped by maxTier. Tier 0 also switches the
    // shader to _RCY_WATER_LOW. A picture only: nothing here collides or is sent.
    [DefaultExecutionOrder(120)]
    public sealed class YardWater : MonoBehaviour
    {
        public const int MaxLines = 16, MaxSplashes = 8;
        const string LowKeyword = "_RCY_WATER_LOW";
        static readonly int LinesId = Shader.PropertyToID("_RcyFallLines");
        static readonly int LinesInfoId = Shader.PropertyToID("_RcyFallLinesInfo");
        static readonly int LinesCountId = Shader.PropertyToID("_RcyFallLinesCount");
        static readonly int SplashId = Shader.PropertyToID("_RcySplashes");
        static readonly int SplashInfoId = Shader.PropertyToID("_RcySplashesInfo");
        static readonly int SplashCountId = Shader.PropertyToID("_RcySplashesCount");
        static readonly int FlowId = Shader.PropertyToID("_Flow");
        static readonly int HeadingId = Shader.PropertyToID("_WaveHeading");

        sealed class Surface
        {
            public Renderer old;
            public Transform frame;            // the water box's parent: its flow is in this frame
            public Vector3 flowLocal;
            public WaterCurrent current;        // a WaterCurrent over this water, when there is one: its flow wins
            public Vector2 size;               // the box's top, x and z (m)
            public Material material;
            public Mesh mesh;
            public GameObject go;
            public int builtTier = -1;
        }

        [SerializeField, Range(0, 2)] int maxTier = 2;

        readonly List<Surface> surfaces = new List<Surface>();
        readonly List<YardWaterfall> falls = new List<YardWaterfall>();
        readonly List<Renderer> hidden = new List<Renderer>();
        readonly List<GameObject> extras = new List<GameObject>();
        readonly Vector4[] lines = new Vector4[MaxLines], lineInfo = new Vector4[MaxLines];
        readonly Vector4[] splashes = new Vector4[MaxSplashes], splashInfo = new Vector4[MaxSplashes];
        int splashCount, splashNext, tier = -1;

        public int Tier => Mathf.Max(0, tier);
        public int SurfaceCount => surfaces.Count;
        public int FallCount => falls.Count;

        public void Configure(int maxQualityTier) => maxTier = Mathf.Clamp(maxQualityTier, 0, 2);

        // A code water box replaced by a moving surface: `flowLocal` (m/s) in the box's parent's frame, the swell's
        // height and main wavelength, the water's depth for the dispersion relation, the edge foam's depth.
        public void AddSurface(string name, Renderer old, Material shared, Vector3 flowLocal, float waveHeight, float waveLength,
                               float depth, float edgeFoam, Transform yardRoot)
        {
            if (old == null || shared == null) return;
            var t = old.transform;
            var s = new Surface
            {
                old = old, frame = t.parent, flowLocal = flowLocal, size = new Vector2(Mathf.Abs(t.localScale.x), Mathf.Abs(t.localScale.z)),
                material = new Material(shared) { name = "Yard water (" + name + ")" },
            };
            s.material.SetFloat("_WaveHeight", waveHeight);
            s.material.SetFloat("_WaveLength", waveLength);
            s.material.SetFloat("_WaterDepth", depth);
            s.material.SetFloat("_EdgeFoam", edgeFoam);
            // The game's own current, if a WaterCurrent box lies over this water (none in the yard today).
            if (yardRoot != null)
                foreach (var current in yardRoot.GetComponentsInChildren<WaterCurrent>(true))
                    if (current.TryGetComponent<BoxCollider>(out var box) && box.bounds.Intersects(old.bounds)) { s.current = current; break; }
            s.go = new GameObject("Water surface (" + name + ")");
            s.go.SetActive(false);
            s.go.transform.SetParent(t.parent, false);
            s.go.transform.localPosition = t.localPosition + t.localRotation * new Vector3(0f, Mathf.Abs(t.localScale.y) * .5f, 0f);
            s.go.transform.localRotation = t.localRotation;
            s.mesh = new Mesh { name = "Yard water grid (" + name + ")" };
            s.go.AddComponent<MeshFilter>().sharedMesh = s.mesh;
            var view = s.go.AddComponent<MeshRenderer>();
            view.sharedMaterial = s.material;
            view.shadowCastingMode = ShadowCastingMode.Off;
            view.receiveShadows = true;
            view.lightProbeUsage = LightProbeUsage.Off;
            view.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            surfaces.Add(s);
            Hide(old);
        }

        public void AddFall(YardWaterfall fall)
        {
            if (fall != null && !falls.Contains(fall)) falls.Add(fall);
        }

        // A code picture the new water draws instead (shown again when this is off).
        public void Hide(Renderer r)
        {
            if (r != null && !hidden.Contains(r)) hidden.Add(r);
        }

        // Something of the new water's own that is shown and hidden with it (the splash triggers).
        public void Own(GameObject go)
        {
            if (go != null) extras.Add(go);
        }

        // A pawn went in at `at` (on the surface) this hard (0.4 .. 1.4): rings and a moment of churn in the shader.
        public void Splash(Vector3 at, float strength)
        {
            splashes[splashNext] = new Vector4(at.x, at.z, Time.timeSinceLevelLoad, Mathf.Clamp(strength, .2f, 1.5f));
            splashInfo[splashNext] = new Vector4(at.y, 0f, 0f, 0f);
            splashNext = (splashNext + 1) % MaxSplashes;
            splashCount = Mathf.Min(splashCount + 1, MaxSplashes);
        }

        void OnEnable()
        {
            GameSettings.Changed -= UpdateTier;
            GameSettings.Changed += UpdateTier;
            tier = -1;
            UpdateTier();
            Show(true);
        }

        void OnDisable()
        {
            GameSettings.Changed -= UpdateTier;
            Show(false);
            Shader.SetGlobalFloat(LinesCountId, 0f);
            Shader.SetGlobalFloat(SplashCountId, 0f);
        }

        void OnDestroy()
        {
            foreach (var s in surfaces)
            {
                if (s.mesh != null) Destroy(s.mesh);
                if (s.material != null) Destroy(s.material);
            }
        }

        void Show(bool on)
        {
            foreach (var r in hidden) if (r != null) r.enabled = !on;
            foreach (var s in surfaces) if (s.go != null) s.go.SetActive(on);
            foreach (var f in falls) if (f != null) f.gameObject.SetActive(on);
            foreach (var go in extras) if (go != null) go.SetActive(on);
        }

        // The yard light's tiers (YardAtmosphere.SettingsTier, same rules).
        static int SettingsTier()
        {
            int q = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            if (q >= 0 && q < names.Length && names[q].Contains("Mobile")) return 0;
            int preset = GameSettings.Preset;
            if (preset <= 2) return Mathf.Max(0, preset);
            int shadow = GameSettings.Shadow;
            return shadow >= 3 ? 2 : shadow >= 2 ? 1 : 0;
        }

        void UpdateTier()
        {
            int now = Mathf.Min(SettingsTier(), maxTier);
            if (now == tier) return;
            tier = now;
            foreach (var s in surfaces)
            {
                if (tier == 0) s.material.EnableKeyword(LowKeyword); else s.material.DisableKeyword(LowKeyword);
                Grid(s);
            }
            Debug.Log("[마차뜰 물] 품질 단계 " + tier + ": 물 표면 " + surfaces.Count + "개(격자 " + Cell(tier) + " m), 떨어지는 물 "
                      + falls.Count + "곳" + (tier == 0 ? ", 셰이더 낮음(물결 2개 · 고리 없음)" : ""));
        }

        static float Cell(int tier) => tier >= 2 ? .5f : tier == 1 ? .8f : 1.6f;

        // The surface's grid of up faces at its tier's spacing (in the surface object's frame, centred, y 0).
        void Grid(Surface s)
        {
            if (s.builtTier == tier || s.mesh == null) return;
            s.builtTier = tier;
            float cell = Cell(tier);
            int nx = Mathf.Clamp(Mathf.CeilToInt(s.size.x / cell), 1, 240), nz = Mathf.Clamp(Mathf.CeilToInt(s.size.y / cell), 1, 240);
            while ((nx + 1) * (nz + 1) > 60000) { nx = Mathf.Max(1, nx * 3 / 4); nz = Mathf.Max(1, nz * 3 / 4); }
            var v = new Vector3[(nx + 1) * (nz + 1)];
            var n = new Vector3[v.Length];
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    v[j * (nx + 1) + i] = new Vector3((i / (float)nx - .5f) * s.size.x, 0f, (j / (float)nz - .5f) * s.size.y);
                    n[j * (nx + 1) + i] = Vector3.up;
                }
            var tris = new int[nx * nz * 6];
            int k = 0;
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = j * (nx + 1) + i;
                    tris[k++] = a; tris[k++] = a + nx + 1; tris[k++] = a + 1;
                    tris[k++] = a + 1; tris[k++] = a + nx + 1; tris[k++] = a + nx + 2;
                }
            s.mesh.Clear();
            s.mesh.vertices = v;
            s.mesh.normals = n;
            s.mesh.triangles = tris;
            // Room for the swell (a few cm) and the Gerstner's sideways shift.
            s.mesh.bounds = new Bounds(Vector3.zero, new Vector3(s.size.x + 1f, 1f, s.size.y + 1f));
        }

        void LateUpdate()
        {
            // Each surface's current, in the world (a WaterCurrent's own when there is one), and the swell along it.
            foreach (var s in surfaces)
            {
                if (s.material == null) continue;
                var flow = s.current != null ? s.current.transform.TransformDirection(s.current.Flow)
                         : s.frame != null ? s.frame.TransformDirection(s.flowLocal) : s.flowLocal;
                s.material.SetVector(FlowId, new Vector4(flow.x, flow.z, 0f, 0f));
                if (flow.x * flow.x + flow.z * flow.z > 1e-4f)
                    s.material.SetFloat(HeadingId, Mathf.Atan2(flow.z, flow.x) * Mathf.Rad2Deg);
            }
            // Every fall's landing lines, in the world.
            int n = 0;
            foreach (var f in falls)
            {
                if (f == null || !f.isActiveAndEnabled) continue;
                var t = f.transform;
                foreach (var line in f.Lines)
                {
                    if (n == MaxLines) break;
                    if (line.strength <= 0f) continue;
                    var a = t.TransformPoint(line.a);
                    var b = t.TransformPoint(line.b);
                    lines[n] = new Vector4(a.x, a.z, b.x, b.z);
                    lineInfo[n] = new Vector4(t.TransformPoint(new Vector3(0f, f.WaterY, 0f)).y, line.strength, line.reach, line.scale);
                    n++;
                }
            }
            Shader.SetGlobalVectorArray(LinesId, lines);
            Shader.SetGlobalVectorArray(LinesInfoId, lineInfo);
            Shader.SetGlobalFloat(LinesCountId, n);
            Shader.SetGlobalVectorArray(SplashId, splashes);
            Shader.SetGlobalVectorArray(SplashInfoId, splashInfo);
            Shader.SetGlobalFloat(SplashCountId, splashCount);
        }
    }
}
