using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    // R725 ro-vfx (Royal Observatory R7, device VFX 4 of 4 in this session; user 11-20 "확인 다음 작업"): one iris gate of
    // the great telescope, its two leaves (IrisLeaf) closing from left and right on the shared clock. This only draws,
    // from the same clock (IrisLeaf.CycleAt), so every PC shows the same moment and nothing is sent:
    //   closing  metal sparks scraped off the deck where each leaf's prow passes (the prow runs in at one speed, so each
    //            spark is shed as the prow reaches its place: its delay), flying back and up and sliding on the deck
    //   shut     as the prows meet on the deck's middle line, a short star of light at the seam and sparks spraying
    //            out along the seam, up and down the deck
    // The warning stays the leaves' own (DeviceWarningGlow: their gold pulses ember red). Sparks are the hazards'
    // particle shader (VFX_BurstParticles, drawn from age alone); the flash is the sun's corona shader (RoSunCorona).
    // This object sits where the builder's "Iris N warning" bar does (on the deck at the gate, +z up the deck, +y off
    // it, +x across it, the deck's middle line x 0) but beside it, so it is never switched off with the bar.
    [DisallowMultipleComponent]
    public sealed class RoyalObservatoryVfxIris : MonoBehaviour
    {
        const string BurstShader = "ChessFight/VFX/Burst Particles", FlashPath = "RoyalObservatory/Vfx/RoSunCorona";

        public static bool Enabled = true;

        static readonly int AgeId = Shader.PropertyToID("_Age"), LightId = Shader.PropertyToID("_Light"),
            SunRadiusId = Shader.PropertyToID("_SunRadius"), SizeId = Shader.PropertyToID("_Size"), CoronaReachId = Shader.PropertyToID("_CoronaReach"),
            FlamesId = Shader.PropertyToID("_Flames"), FlameReachId = Shader.PropertyToID("_FlameReach"), FlashId = Shader.PropertyToID("_Flash"),
            RaysId = Shader.PropertyToID("_Rays"), RayReachId = Shader.PropertyToID("_RayReach"), ShockLightId = Shader.PropertyToID("_ShockLight");

        [Header("Sparks as the leaves close")]
        [Range(1, 80)] public int scrapeCount = 26;
        public float scrapeLight = 5f;
        [Header("The clash as they shut")]
        [Range(1, 80)] public int clashCount = 36;
        public float clashLight = 6f;
        public float flashLight = 2.6f, flashSeconds = .28f;
        [Header("Cost")]
        public float farDistance = 50f;

        IrisLeaf leaf;
        float reach;          // how far out each prow starts (m from the middle line)
        Material burstMaterial, flashMaterial;
        MeshRenderer scrape, clash, flash;
        Mesh scrapeMesh, clashMesh, quad;
        MaterialPropertyBlock block;
        float scrapeLife, clashLife;
        bool ready;

        // `leaf` gives the timing (both leaves of a gate share it); `reach` how far from the middle line each prow starts.
        public void Configure(IrisLeaf leaf, float reach)
        {
            this.leaf = leaf;
            this.reach = reach;
        }

        void Start()
        {
            block = new MaterialPropertyBlock();
            var burst = ChessFight.VFX.VfxShaderKeep.Find(BurstShader);
            var flashShader = Resources.Load<Shader>(FlashPath);
            if (leaf == null || burst == null)
            {
                Debug.LogWarning("[천문대 VFX] 조리개 효과: 장치나 셰이더가 없어 끕니다");
                enabled = false;
                return;
            }
            burstMaterial = new Material(burst) { name = "RO iris sparks" };
            scrapeMesh = ScrapeMesh();
            clashMesh = ClashMesh();
            scrape = Part("Iris scrape sparks", scrapeMesh, burstMaterial);
            clash = Part("Iris clash sparks", clashMesh, burstMaterial);
            Look(scrape, scrapeLight, .06f, .025f, .035f, new Color(1f, .78f, .42f), new Color(1f, .4f, .1f));
            Look(clash, clashLight, .07f, .03f, .04f, new Color(1f, .9f, .66f), new Color(1f, .45f, .12f));
            if (flashShader != null && flashShader.isSupported)
            {
                flashMaterial = new Material(flashShader) { name = "RO iris clash flash" };
                quad = Quad();
                flash = Part("Iris clash flash", quad, flashMaterial);
                flash.transform.localPosition = new Vector3(0f, 1.2f, 0f);   // the seam's middle (the leaves stand 2.4 m)
            }
            ready = true;
        }

        MeshRenderer Part(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.enabled = false;
            return r;
        }

        void Look(MeshRenderer r, float light, float sizeStart, float sizeEnd, float stretch, Color start, Color end)
        {
            r.GetPropertyBlock(block);
            block.SetColor("_ColorStart", start);
            block.SetColor("_ColorEnd", end);
            block.SetFloat(LightId, light);
            block.SetFloat("_AlphaStart", 1f);
            block.SetFloat("_AlphaEnd", 0f);
            block.SetFloat("_FadeIn", .02f);
            block.SetFloat("_SizeStart", sizeStart);
            block.SetFloat("_SizeEnd", sizeEnd);
            block.SetFloat("_Stretch", stretch);
            block.SetFloat("_Soft", .8f);
            block.SetFloat("_Hard", 0f);
            block.SetFloat("_Additive", 1f);
            block.SetFloat("_DepthFade", 0f);
            block.SetFloat("_Gravity", 9f);
            block.SetFloat("_Drag", 1.6f);
            block.SetFloat("_FloorY", 0f);
            block.SetFloat(AgeId, -1f);
            r.SetPropertyBlock(block);
        }

        static Mesh Quad()
        {
            var mesh = new Mesh { name = "RO iris flash quad", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(new List<Vector3> { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 6f);
            return mesh;
        }

        // VFX_BurstParticles laid along both prows' runs over the deck: a spark is shed as its prow reaches its place
        // (the prow runs in at one speed, so its delay is how far in it is), thrown back the way the leaf came and up.
        Mesh ScrapeMesh()
        {
            var rng = new System.Random(71);
            float Next() => (float)rng.NextDouble();
            float close = Mathf.Max(leaf.CloseTime, .05f);
            var b = new Builder();
            scrapeLife = 0f;
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < scrapeCount; i++)
                {
                    float share = (i + Next()) / scrapeCount;   // of the way in, when it is shed
                    var start = new Vector3(side * reach * (1f - share), .04f + Next() * .12f, (Next() - .5f) * .35f);
                    var velocity = new Vector3(side * Mathf.Lerp(.6f, 2.8f, Next()), Mathf.Lerp(1.2f, 3.8f, Next()), (Next() - .5f) * 3.2f);
                    float life = Mathf.Lerp(.25f, .55f, Next());
                    scrapeLife = Mathf.Max(scrapeLife, share * close + life);
                    b.Add(start, velocity, life, Mathf.Lerp(.7f, 1.3f, Next()), share * close);
                }
            return b.Mesh("RO iris scrape sparks", new Bounds(new Vector3(0f, 1f, 0f), new Vector3(2f * reach + 4f, 4f, 6f)));
        }

        // VFX_BurstParticles along the seam where the prows meet (the middle line, from the deck up the leaves'
        // height), sprayed out up and down the deck and a little up.
        Mesh ClashMesh()
        {
            var rng = new System.Random(73);
            float Next() => (float)rng.NextDouble();
            var b = new Builder();
            clashLife = 0f;
            for (int i = 0; i < clashCount; i++)
            {
                var start = new Vector3((Next() - .5f) * .1f, Next() * 2.3f + .05f, 0f);
                float dir = Next() < .5f ? -1f : 1f;
                var velocity = new Vector3((Next() - .5f) * 2f, Mathf.Lerp(-.5f, 2.6f, Next()), dir * Mathf.Lerp(2.5f, 6.5f, Next()));
                float life = Mathf.Lerp(.3f, .65f, Next());
                clashLife = Mathf.Max(clashLife, life);
                b.Add(start, velocity, life, Mathf.Lerp(.7f, 1.4f, Next()), 0f);
            }
            return b.Mesh("RO iris clash sparks", new Bounds(new Vector3(0f, 1.5f, 0f), new Vector3(4f, 5f, 10f)));
        }

        sealed class Builder
        {
            readonly List<Vector3> points = new List<Vector3>(), velocities = new List<Vector3>();
            readonly List<Vector2> corners = new List<Vector2>();
            readonly List<Vector4> particles = new List<Vector4>();
            readonly List<int> triangles = new List<int>();

            public void Add(Vector3 start, Vector3 velocity, float life, float size, float delay)
            {
                int at = points.Count;
                for (int c = 0; c < 4; c++)
                {
                    points.Add(start);
                    corners.Add(new Vector2(c & 1, c >> 1));
                    particles.Add(new Vector4(life, size, delay, 0f));
                    velocities.Add(velocity);
                }
                triangles.AddRange(new[] { at, at + 2, at + 1, at + 2, at + 3, at + 1 });
            }

            public Mesh Mesh(string name, Bounds bounds)
            {
                var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
                mesh.SetVertices(points);
                mesh.SetUVs(0, corners);
                mesh.SetUVs(1, particles);
                mesh.SetUVs(2, velocities);
                mesh.SetTriangles(triangles, 0);
                mesh.bounds = bounds;
                return mesh;
            }
        }

        void LateUpdate()
        {
            if (!ready) return;
            var cam = Camera.main;
            bool near = Enabled && cam != null && (cam.transform.position - transform.position).sqrMagnitude < farDistance * farDistance;
            double now = ObstacleClock.Now;
            float t = leaf.CycleAt(now), closeAt = leaf.OpenTime + leaf.WarnTime, shutAt = closeAt + leaf.CloseTime;

            float scrapeAge = t - closeAt;
            Seek(scrape, near && scrapeAge >= 0f && scrapeAge < scrapeLife, scrapeAge);
            float clashAge = t - shutAt;
            Seek(clash, near && clashAge >= 0f && clashAge < clashLife, clashAge);

            if (flash != null)
            {
                bool on = near && clashAge >= 0f && clashAge < flashSeconds;
                if (flash.enabled != on) flash.enabled = on;
                if (on)
                {
                    float k = clashAge / Mathf.Max(flashSeconds, .01f);
                    flash.GetPropertyBlock(block);
                    block.SetFloat(SunRadiusId, .06f);
                    block.SetFloat(SizeId, 1.7f);
                    block.SetFloat(LightId, flashLight * (1f - k) * (1f - k));
                    block.SetFloat(CoronaReachId, 3f);
                    block.SetFloat(FlamesId, 0f);
                    block.SetFloat(FlameReachId, .1f);
                    block.SetFloat(FlashId, 2.5f);
                    block.SetFloat(ShockLightId, 0f);
                    block.SetFloat(RaysId, 3f);
                    block.SetFloat(RayReachId, Mathf.Lerp(6f, 16f, k));
                    block.SetFloat(AgeId, (float)(now % 600.0));
                    flash.SetPropertyBlock(block);
                }
            }
        }

        void Seek(MeshRenderer r, bool on, float age)
        {
            if (r.enabled != on) r.enabled = on;
            if (!on) return;
            r.GetPropertyBlock(block);
            block.SetFloat(AgeId, age);
            r.SetPropertyBlock(block);
        }

        void OnDisable()
        {
            if (!ready) return;
            scrape.enabled = false;
            clash.enabled = false;
            if (flash != null) flash.enabled = false;
        }

        void OnDestroy()
        {
            if (burstMaterial != null) Destroy(burstMaterial);
            if (flashMaterial != null) Destroy(flashMaterial);
            if (scrapeMesh != null) Destroy(scrapeMesh);
            if (clashMesh != null) Destroy(clashMesh);
            if (quad != null) Destroy(quad);
        }
    }
}
