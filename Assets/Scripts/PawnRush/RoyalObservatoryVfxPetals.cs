using System.Collections.Generic;
using ChessFight.VFX;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R725 ro-vfx2 (Royal Observatory R7, device VFX: the variable stars, PLAN 5 "접힐 때 별가루 흩어짐, 펴질 때 빛 번짐"):
    // one petal's effects, beside its StarPetal. The petal decides when it folds and comes up (a pure function of the
    // shared obstacle clock and its phase); this works the same moment out from the same clock, so every PC shows the
    // same instant and nothing is sent. Nothing here moves a collider or decides anything; the red warning pulse before
    // the fold stays DeviceWarningGlow's.
    //   fold   star dust and gold glints shed off the petal's top as it drops away under them: each starts where the
    //          top was at the moment it lets go, with a little of the top's swing, and drifts down into the abyss
    //          glittering (VFX_BurstParticles, the meteor's sparks' shader, laid out here along the fold)
    //   rise   gold light bleeding out of the hinge and along the seams and rim as the petals come up together, a
    //          soft front running out to the rim with the rise, and a short hot flash along the edges as it locks
    //          level (Resources/RoyalObservatory/Vfx2/RoPetalBleed.shader, a fan a hair over the top that rides it)
    // Petals further than `farDistance` from the camera are not drawn.
    [DisallowMultipleComponent]
    public sealed class RoyalObservatoryVfxPetals : MonoBehaviour
    {
        const string BurstShader = "ChessFight/VFX/Burst Particles";
        const string BleedShaderPath = "RoyalObservatory/Vfx2/RoPetalBleed";
        // RoyalObservatoryBuilder's: the hub's radius and the half angle of a petal's wedge round the star's middle.
        const float CoreRadius = .35f, Half = 35.3f;

        static readonly int AgeId = Shader.PropertyToID("_Age"), LightId = Shader.PropertyToID("_Light"),
            FrontId = Shader.PropertyToID("_Front"), SettleId = Shader.PropertyToID("_Settle");

        // Off: the petals as modelling left them (a switch for comparing, as the dressing has).
        public static bool Enabled = true;

        [Header("Fold: star dust and glints shed off the top")]
        [Range(1, 60)] public int dustCount = 22;
        [Range(1, 60)] public int glintCount = 18;
        public float dustLight = 1.4f;
        public float glintLight = 4.5f;

        [Header("Rise: the gold bleed")]
        [ColorUsage(false, true)] public Color bleedColor = new Color(1f, .64f, .24f);
        [ColorUsage(false, true)] public Color bleedHot = new Color(1f, .9f, .66f);
        public float bleedLight = 1.6f;
        [Tooltip("Seconds the flash along the edges takes to die once the petal is level.")]
        public float settleSeconds = .5f;
        [Tooltip("Seconds the bleed takes to fade once the petal is level.")]
        public float lingerSeconds = .75f;

        [Header("Cost")]
        [Tooltip("Petals further than this from the camera (m) are not drawn.")]
        public float farDistance = 60f;

        StarPetal petal;
        Transform frame;
        MeshRenderer dust, glints, bleed;
        Mesh dustMesh, glintMesh, bleedMesh;
        Material burstMaterial, bleedMaterial;
        MaterialPropertyBlock block;
        float radius = 3.18f, hinge, dustSpan, glintSpan;
        bool ready;

        void Start()
        {
            petal = GetComponent<StarPetal>();
            block = new MaterialPropertyBlock();
            var burst = VfxShaderKeep.Find(BurstShader);
            var bleedShader = Resources.Load<Shader>(BleedShaderPath);
            ready = petal != null && burst != null && bleedShader != null && bleedShader.isSupported;
            if (!ready)
            {
                Debug.LogWarning("[천문대 VFX2] 변광성 꽃잎 효과 셰이더를 찾지 못해 효과를 끕니다");
                enabled = false;
                return;
            }
            burstMaterial = new Material(burst) { name = "RO petal dust" };
            bleedMaterial = new Material(bleedShader) { name = "RO petal bleed" };

            // The petal's size from its graybox wedge (its top at y 0, its rim's middle at z = radius - hinge).
            hinge = CoreRadius * Mathf.Cos(36f * Mathf.Deg2Rad);
            var deck = transform.Find("Petal");
            if (deck != null && deck.TryGetComponent<MeshFilter>(out var f) && f.sharedMesh != null)
                radius = f.sharedMesh.bounds.max.z + hinge;

            // The dust's frame: the petal's hinge as it lies level (it turns only about its own x, the hinge), upright,
            // so the particles' gravity is the world's.
            frame = new GameObject("Petal VFX (" + name + ")").transform;
            frame.SetParent(transform.parent, false);
            frame.position = transform.position;
            var level = Vector3.Cross(transform.right, Vector3.up);
            frame.rotation = Quaternion.LookRotation(level.sqrMagnitude > 1e-6f ? level.normalized : Vector3.forward, Vector3.up);

            dustMesh = ShedMesh(dustCount, 51, false, out dustSpan);
            glintMesh = ShedMesh(glintCount, 53, true, out glintSpan);
            dust = Part("Star dust", frame, dustMesh, burstMaterial);
            glints = Part("Gold glints", frame, glintMesh, burstMaterial);
            DustLook(dust);
            GlintLook(glints);

            bleedMesh = BleedMesh();
            bleed = Part("Gold bleed", transform, bleedMesh, bleedMaterial);
            bleedMaterial.SetColor("_Color", bleedColor);
            bleedMaterial.SetColor("_HotColor", bleedHot);
        }

        void LateUpdate()
        {
            if (!ready) return;
            var eye = Camera.main != null ? Camera.main.transform.position : transform.position;
            if (!Enabled || (transform.position - eye).sqrMagnitude > farDistance * farDistance)
            {
                Hide();
                return;
            }
            double now = petal.Now;
            float cycle = petal.Cycle, rise = petal.RiseSeconds;

            // Fold: the dust and glints seek the time since this petal began to fold (round the cycle, so the last
            // petal's dust still falls after the cycle has turned).
            float foldAge = Since(now, petal.FoldsAt, cycle);
            Seek(dust, foldAge, dustSpan, dustLight);
            Seek(glints, foldAge, glintSpan, glintLight);

            // Rise: the bleed runs out with the rise, flashes as it locks, then fades.
            float riseAge = Since(now, petal.RisesAt, cycle);
            bool bleeding = riseAge < rise + lingerSeconds;
            bleed.enabled = bleeding;
            if (bleeding)
            {
                float up = Mathf.Clamp01(riseAge / rise);
                float after = Mathf.Max(0f, riseAge - rise);
                float light = Mathf.Clamp01(riseAge / .12f) * (1f - Mathf.SmoothStep(0f, 1f, after / lingerSeconds));
                float settle = riseAge < rise ? 0f : Mathf.Pow(1f - Mathf.Clamp01(after / settleSeconds), 2f);
                bleed.GetPropertyBlock(block);
                block.SetFloat(FrontId, Mathf.SmoothStep(0f, 1f, up) * 1.08f);
                block.SetFloat(SettleId, settle);
                block.SetFloat(LightId, bleedLight * light);
                block.SetFloat(AgeId, riseAge);
                bleed.SetPropertyBlock(block);
            }
        }

        // Seconds since `at` in the cycle, 0 to cycle.
        static float Since(double now, float at, float cycle)
        {
            double t = (now - at) % cycle;
            return (float)(t < 0 ? t + cycle : t);
        }

        void Seek(MeshRenderer r, float age, float span, float light)
        {
            bool on = age < span;
            r.enabled = on;
            if (!on) return;
            r.GetPropertyBlock(block);
            block.SetFloat(AgeId, age);
            block.SetFloat(LightId, light);
            r.SetPropertyBlock(block);
        }

        void Hide()
        {
            if (dust != null) dust.enabled = false;
            if (glints != null) glints.enabled = false;
            if (bleed != null) bleed.enabled = false;
        }

        static MeshRenderer Part(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            r.enabled = false;
            return r;
        }

        // ------------------------------------------------------------------ looks (VFX_BurstParticles, per renderer)

        // Soft star dust: warm starlight turning dusk violet as it sinks, half laid over, half added.
        void DustLook(MeshRenderer r)
        {
            r.GetPropertyBlock(block);
            block.SetColor("_ColorStart", new Color(1f, .84f, .58f));
            block.SetColor("_ColorEnd", new Color(.56f, .5f, .9f));
            block.SetFloat(LightId, dustLight);
            block.SetFloat("_AlphaStart", .5f);
            block.SetFloat("_AlphaEnd", 0f);
            block.SetFloat("_FadeIn", .1f);
            block.SetFloat("_SizeStart", .14f);
            block.SetFloat("_SizeEnd", .4f);
            block.SetFloat("_Stretch", 0f);
            block.SetFloat("_Soft", 1f);
            block.SetFloat("_Hard", 0f);
            block.SetFloat("_Additive", .6f);
            block.SetFloat("_DepthFade", .2f);
            block.SetFloat("_Gravity", 1.2f);
            block.SetFloat("_Drag", 1.6f);
            block.SetFloat("_FloorY", -1e5f);   // nothing under a petal but the abyss
            block.SetFloat(AgeId, -1f);
            r.SetPropertyBlock(block);
        }

        // Bright gold glints off the rim and the seams' gold bands: small, added, quicker.
        void GlintLook(MeshRenderer r)
        {
            r.GetPropertyBlock(block);
            block.SetColor("_ColorStart", new Color(1f, .95f, .8f));
            block.SetColor("_ColorEnd", new Color(1f, .58f, .22f));
            block.SetFloat(LightId, glintLight);
            block.SetFloat("_AlphaStart", 1f);
            block.SetFloat("_AlphaEnd", 0f);
            block.SetFloat("_FadeIn", .03f);
            block.SetFloat("_SizeStart", .065f);
            block.SetFloat("_SizeEnd", .02f);
            block.SetFloat("_Stretch", .015f);
            block.SetFloat("_Soft", .85f);
            block.SetFloat("_Hard", 0f);
            block.SetFloat("_Additive", 1f);
            block.SetFloat("_DepthFade", 0f);
            block.SetFloat("_Gravity", 3f);
            block.SetFloat("_Drag", 1f);
            block.SetFloat("_FloorY", -1e5f);
            block.SetFloat(AgeId, -1f);
            r.SetPropertyBlock(block);
        }

        // ------------------------------------------------------------------ meshes

        // A point on the petal's top (the frame's level petal), `r` from the star's middle at `a` degrees off its axis.
        Vector3 Top(float r, float a) =>
            new Vector3(r * Mathf.Sin(a * Mathf.Deg2Rad), .02f, r * Mathf.Cos(a * Mathf.Deg2Rad) - hinge);

        // Where a point of the top is with the petal `share` of its fold time in (StarPetal.Folded: down = share²).
        Vector3 Folded(Vector3 top, float share) =>
            Quaternion.AngleAxis(petal.Angle * share * share, Vector3.right) * top;

        // VFX_BurstParticles laid out along the fold: particle i lets go of the top `share` of the fold time in, so its
        // delay is then; it starts where that point of the top is then, with a part of the top's own speed there.
        // Dust lifts off the whole top early (the top drops away under it); glints flick off the rim and the seams'
        // gold bands as the fold quickens.
        Mesh ShedMesh(int count, int seed, bool edges, out float span)
        {
            var rng = new System.Random(seed);
            float Next() => (float)rng.NextDouble();
            float fold = petal.FoldSeconds;
            float inner = CoreRadius + .15f, outer = radius - .05f;
            var points = new List<Vector3>();
            var corners = new List<Vector2>();
            var particles = new List<Vector4>();
            var velocities = new List<Vector3>();
            var triangles = new List<int>();
            span = 0f;
            for (int i = 0; i < count; i++)
            {
                float r, a, share, keep;
                if (!edges)
                {
                    // even over the top's area
                    r = Mathf.Sqrt(Mathf.Lerp(inner * inner, outer * outer, (i + Next()) / count));
                    a = Mathf.Lerp(-Half + 3f, Half - 3f, Next());
                    share = Mathf.Lerp(0f, .55f, Next());
                    keep = .25f;
                }
                else if (i % 3 == 0)
                {
                    // the rim's band
                    r = outer;
                    a = Mathf.Lerp(-Half + 2f, Half - 2f, (i / 3 + Next()) / Mathf.Max(1, (count + 2) / 3));
                    share = Mathf.Lerp(.2f, 1f, Next());
                    keep = .6f;
                }
                else
                {
                    // a seam's band, either side
                    r = Mathf.Lerp(inner + .4f, outer, Next());
                    a = (i % 2 == 0 ? 1f : -1f) * (Half - 1.5f);
                    share = Mathf.Lerp(.2f, 1f, Next());
                    keep = .6f;
                }
                var top = Top(r, a);
                const float dt = .01f;
                var start = Folded(top, share);
                var swing = (Folded(top, Mathf.Min(1f, share + dt / fold)) - start) / dt;
                var drift = new Vector3(Mathf.Lerp(-.35f, .35f, Next()), Mathf.Lerp(.1f, .55f, Next()), Mathf.Lerp(-.2f, .35f, Next()));
                var velocity = swing * keep + drift * (edges ? 1.6f : 1f);
                float life = edges ? Mathf.Lerp(.6f, 1.2f, Next()) : Mathf.Lerp(1.2f, 1.9f, Next());
                float delay = share * fold;
                span = Mathf.Max(span, delay + life);
                var particle = new Vector4(life, Mathf.Lerp(.7f, 1.35f, Next()), delay, Mathf.Lerp(-60f, 60f, Next()));
                int at = points.Count;
                for (int c = 0; c < 4; c++)
                {
                    points.Add(start);
                    corners.Add(new Vector2(c & 1, c >> 1));
                    particles.Add(particle);
                    velocities.Add(velocity);
                }
                triangles.AddRange(new[] { at, at + 2, at + 1, at + 2, at + 3, at + 1 });
            }
            var mesh = new Mesh { name = edges ? "RO petal glints" : "RO petal dust", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(points);
            mesh.SetUVs(0, corners);
            mesh.SetUVs(1, particles);
            mesh.SetUVs(2, velocities);
            mesh.SetTriangles(triangles, 0);
            // Room for the fold (down to the folded tip) and the drift after it.
            mesh.bounds = new Bounds(new Vector3(0f, -2.5f, radius * .5f), new Vector3(2f * radius + 4f, 9f, 2f * radius + 4f));
            return mesh;
        }

        // RoPetalBleed's fan: a grid over the top a hair above it, uv.x across, uv.y out from the hinge to the rim, uv2.x
        // metres to the nearest edge (the side seams are lines out from the star's middle, the rim an arc, the hinge z 0).
        Mesh BleedMesh()
        {
            const int across = 10, outward = 8;
            float inner = CoreRadius;
            var points = new List<Vector3>();
            var uvs = new List<Vector2>();
            var edges = new List<Vector2>();
            var triangles = new List<int>();
            for (int j = 0; j <= outward; j++)
                for (int i = 0; i <= across; i++)
                {
                    float r = Mathf.Lerp(inner, radius, j / (float)outward), a = Mathf.Lerp(-Half, Half, i / (float)across);
                    var p = Top(r, a);
                    p.y = .008f;
                    p.z = Mathf.Max(p.z, 0f);
                    points.Add(p);
                    uvs.Add(new Vector2(i / (float)across, j / (float)outward));
                    float side = r * Mathf.Sin((Half - Mathf.Abs(a)) * Mathf.Deg2Rad);
                    edges.Add(new Vector2(Mathf.Max(0f, Mathf.Min(side, Mathf.Min(radius - r, p.z))), 0f));
                }
            for (int j = 0; j < outward; j++)
                for (int i = 0; i < across; i++)
                {
                    int a = j * (across + 1) + i, b = a + 1, c = a + across + 1, d = c + 1;
                    triangles.AddRange(new[] { a, c, d, a, d, b });
                }
            var mesh = new Mesh { name = "RO petal bleed", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(points);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, edges);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        void OnDisable() => Hide();

        void OnDestroy()
        {
            foreach (var m in new[] { burstMaterial, bleedMaterial })
                if (m != null) Destroy(m);
            foreach (var m in new[] { dustMesh, glintMesh, bleedMesh })
                if (m != null) Destroy(m);
            if (frame != null) Destroy(frame.gameObject);
        }
    }
}
