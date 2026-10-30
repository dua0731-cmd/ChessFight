using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// A streak left behind a fast move (R162; on the floor R164, 10-27): ribbons from where the move started (the tail:
    /// this object's place when it is turned on) to where the piece is now (the head) - two camera-facing ones in the air
    /// (a core inside a glow, the Fire Beam look), or one lying on the floor (<see cref="onFloor"/>: the queen's burn
    /// mark, drawn with VFX_ScorchPremul; no <see cref="coreMaterial"/>, no core). Game code moves the head each frame
    /// (<see cref="Reach"/>) and lets the beam go when the move ends (<see cref="Release"/>); left alone (the workbench,
    /// whose target stands 6 m ahead) it runs
    /// <see cref="autoLength"/> along +Z in <see cref="growTime"/>, holds <see cref="hold"/> and lets go by itself. Once
    /// let go it stays a moment (<see cref="afterglow"/>), then burns out from the tail: the tail chases the head, the
    /// noise eats it away, the ribbons thin, and <see cref="emberCount"/> embers shed off it and drift up. The older part
    /// of the beam (toward the tail) is cooler: dimmer and thinner (<see cref="tailTint"/>). The noise of its material is
    /// laid along it in metres from the tail, so it stays put in the world while the head runs on.
    /// </summary>
    public sealed class VfxBeam : MonoBehaviour
    {
        const int Segments = 24;
        // Noise cells run along u; a quarter of a unit a metre keeps them the same size however long the beam is.
        const float UPerMetre = .25f;
        static readonly int FadeId = Shader.PropertyToID("_Fade"), DissolveId = Shader.PropertyToID("_Dissolve"),
            IntensityId = Shader.PropertyToID("_Intensity");

        [Tooltip("Lie flat on the floor (the points given are floor points) instead of facing the camera.")]
        public bool onFloor;
        [Tooltip("On the floor: how far above it the strip lies.")]
        public float floorLift = .012f;
        [Tooltip("The thin bright strip inside the wide one (none: no core).")]
        public Material coreMaterial;
        [Tooltip("The wide strip (on the floor, the only one).")]
        public Material glowMaterial;
        [Tooltip("Width of the core at the head (m).")]
        public float coreWidth = .05f;
        [Tooltip("Width of the wide strip at the head (m).")]
        public float glowWidth = .24f;
        [Range(0f, 1f), Tooltip("Width at the tail as a share of the head's.")]
        public float tailWidth = .45f;
        [Tooltip("HDR multipliers on the materials' colours (see Docs/VFX §6).")]
        public float coreIntensity = 5f, glowIntensity = 2f;
        [Tooltip("Multiplies the colour at the tail (the head is as the material has it): the older part is cooler.")]
        public Color tailTint = new Color(1f, .45f, .3f, .55f);
        [Tooltip("Metres over which the tail and the head come up from nothing.")]
        public float tailSoft = .4f, headSoft = .12f;
        [Range(0f, .5f), Tooltip("How much the brightness flickers.")]
        public float flicker = .12f;

        [Header("By itself (the workbench)")]
        [Min(0f)] public float growTime = .4f;
        public float autoLength = 6f;
        [Min(0f)] public float hold = .05f;

        [Header("Burning out")]
        [Min(0f), Tooltip("Seconds it stays whole once let go.")]
        public float afterglow = .08f;
        [Min(.02f)] public float burnTime = .35f;
        [Range(0f, 1f), Tooltip("How far the tail chases the head while it burns out.")]
        public float burnChase = .7f;
        [Range(0f, 1f), Tooltip("How far the noise has eaten it away by the end.")]
        public float dissolveEnd = .85f;
        [Tooltip("Drawn with a soft glow material (VFX_SoftGlowAdd); none: no embers.")]
        public Material emberMaterial;
        [Min(0)] public int emberCount = 10;
        public float emberIntensity = 2.5f;

        Ribbon core, glow;
        ParticleSystem embers;
        Vector3 tail, head;
        bool driven, released;
        float age, releasedAt, lastReach, seed;

        /// <summary>Puts the head at a world point (the piece as it runs). The first call takes the beam out of its own
        /// timing; if the calls stop for a while (the piece is gone) it lets go by itself.</summary>
        public void Reach(Vector3 world)
        {
            if (released) return;
            driven = true;
            head = world;
            lastReach = age;
        }

        /// <summary>The move is over: the beam stays a moment and burns out.</summary>
        public void Release()
        {
            if (released || !isActiveAndEnabled) return;
            released = true;
            releasedAt = age;
            Shed();
        }

        void Awake()
        {
            glow = new Ribbon(transform, "Beam glow", glowMaterial);
            if (coreMaterial != null) core = new Ribbon(transform, "Beam core", coreMaterial);
            if (emberMaterial != null && emberCount > 0) embers = Embers();
        }

        /// <summary>Embers: small glowing dots that drift up and fade (a built-in particle system set up in code).</summary>
        ParticleSystem Embers()
        {
            var go = new GameObject("Beam embers");
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = .1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.4f, .9f);
            main.startSize = new ParticleSystem.MinMaxCurve(.015f, .03f);
            main.gravityModifier = -.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = emberCount;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            var shape = system.shape;
            shape.enabled = false;
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.8f, .4f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var view = go.GetComponent<ParticleSystemRenderer>();
            view.sharedMaterial = emberMaterial;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetFloat(IntensityId, emberIntensity);
            view.SetPropertyBlock(block);
            return system;
        }

        /// <summary>The embers go from along the beam (not its very tail), each off a random way, slowly.</summary>
        void Shed()
        {
            if (embers == null) return;
            embers.Play(false);
            var p = new ParticleSystem.EmitParams();
            for (int i = 0, n = Mathf.RoundToInt(emberCount * VfxCue.Density); i < n; i++)
            {
                p.position = Vector3.Lerp(tail, head, Random.Range(.15f, 1f));
                var v = Random.onUnitSphere;
                if (onFloor) v.y = Mathf.Abs(v.y);
                p.velocity = v * Random.Range(.2f, .8f) + Vector3.up * .3f;
                embers.Emit(p, 1);
            }
        }

        void OnEnable()
        {
            age = 0f;
            driven = released = false;
            tail = head = transform.position;
            seed = Random.value * 100f;
            Show(false);
        }

        void OnDestroy()
        {
            glow?.Destroy();
            core?.Destroy();
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            if (!released)
            {
                if (!driven)
                {
                    float k = growTime > 0f ? Mathf.Clamp01(age / growTime) : 1f;
                    head = Vector3.Lerp(tail, AutoEnd(), k);
                    if (age >= growTime + hold) Release();
                }
                else if (age - lastReach > .3f) Release();
            }
            float burn = released ? Mathf.Clamp01((age - releasedAt - afterglow) / burnTime) : 0f;
            if (released && burn >= 1f)
            {
                Show(false);
                return;
            }
            Draw(burn);
        }

        Vector3 AutoEnd() => tail + transform.forward * autoLength;

        void Show(bool on)
        {
            if (glow != null) glow.view.enabled = on;
            if (core != null) core.view.enabled = on;
        }

        void Draw(float burn)
        {
            float chase = burnChase * burn * burn * (3f - 2f * burn);
            Vector3 from = Vector3.Lerp(tail, head, chase), way = head - from;
            float length = way.magnitude;
            if (length < .02f)
            {
                Show(false);
                return;
            }
            Show(true);
            var cam = Camera.main;
            Vector3 eye = cam != null ? cam.transform.position : head + Vector3.up * 5f;
            Vector3 along = way / length, lift = onFloor ? Vector3.up * floorLift : Vector3.zero;
            float thin = 1f - .5f * burn, startMetres = Vector3.Distance(tail, from);
            for (int i = 0; i <= Segments; i++)
            {
                float s = (float)i / Segments;
                Vector3 p = from + way * s + lift;
                Vector3 side = Vector3.Cross(along, onFloor ? Vector3.up : eye - p);
                if (side.sqrMagnitude < 1e-8f) side = Vector3.Cross(along, Vector3.up);
                if (side.sqrMagnitude < 1e-8f) side = Vector3.right;
                side.Normalize();
                // Cooler and dimmer toward the tail, coming up softly from nothing at both ends.
                Color tint = Color.Lerp(tailTint, Color.white, s);
                tint.a *= Mathf.Clamp01(length * s / Mathf.Max(.01f, tailSoft)) * Mathf.Clamp01(length * (1f - s) / Mathf.Max(.01f, headSoft));
                float widen = Mathf.Lerp(tailWidth, 1f, s) * thin, u = (startMetres + length * s) * UPerMetre;
                glow.Set(transform, i, p, side * (glowWidth * .5f * widen), u, tint);
                core?.Set(transform, i, p, side * (coreWidth * .5f * widen), u, tint);
            }
            float up = Mathf.Clamp01(age / .03f), fade = up * Mathf.Pow(1f - burn, 1.5f), dissolve = dissolveEnd * burn;
            float flick = 1f + flicker * (Mathf.PerlinNoise(age * 18f, seed) * 2f - 1f);
            glow.Apply(fade, dissolve, glowIntensity * flick);
            core?.Apply(fade, dissolve, coreIntensity * flick);
        }

        /// <summary>One camera-facing strip of the beam: its own mesh and renderer, a child of the beam.</summary>
        sealed class Ribbon
        {
            public readonly MeshRenderer view;
            readonly Mesh mesh;
            readonly Vector3[] vertices = new Vector3[(Segments + 1) * 2];
            readonly Color[] colors = new Color[(Segments + 1) * 2];
            readonly Vector2[] uv = new Vector2[(Segments + 1) * 2];
            readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

            public Ribbon(Transform parent, string name, Material material)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                mesh = new Mesh { name = "VFX beam" };
                mesh.MarkDynamic();
                var triangles = new int[Segments * 6];
                for (int i = 0; i < Segments; i++)
                {
                    int t = i * 6, a = i * 2;
                    triangles[t] = a; triangles[t + 1] = a + 1; triangles[t + 2] = a + 2;
                    triangles[t + 3] = a + 2; triangles[t + 4] = a + 1; triangles[t + 5] = a + 3;
                }
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                view = go.AddComponent<MeshRenderer>();
                view.sharedMaterial = material;
                view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                view.receiveShadows = false;
                view.enabled = false;
            }

            /// <summary>Cross-section <paramref name="i"/>: a world point and half its width (also in the world).</summary>
            public void Set(Transform space, int i, Vector3 p, Vector3 half, float u, Color tint)
            {
                vertices[i * 2] = space.InverseTransformPoint(p - half);
                vertices[i * 2 + 1] = space.InverseTransformPoint(p + half);
                uv[i * 2] = new Vector2(u, 0f);
                uv[i * 2 + 1] = new Vector2(u, 1f);
                colors[i * 2] = colors[i * 2 + 1] = tint;
            }

            public void Apply(float fade, float dissolve, float intensity)
            {
                mesh.vertices = vertices;
                mesh.uv = uv;
                mesh.colors = colors;
                mesh.RecalculateBounds();
                view.GetPropertyBlock(block);
                block.SetFloat(FadeId, fade);
                block.SetFloat(DissolveId, dissolve);
                block.SetFloat(IntensityId, intensity);
                view.SetPropertyBlock(block);
            }

            public void Destroy()
            {
                if (mesh != null) Object.Destroy(mesh);
            }
        }
    }
}
