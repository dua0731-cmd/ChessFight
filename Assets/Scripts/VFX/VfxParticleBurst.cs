using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// A burst of particles thrown out at <see cref="burstAt"/> seconds (R341, 11-04, I1 of the grand stair hall's falling
    /// knights: the flash, dust, stone chips and sparks as one lands). The particles start on a ring round the middle
    /// (<see cref="ringRadius"/>, <see cref="startHeight"/>) and fly outward and up at speeds in the given ranges; the
    /// shader (VFX_BurstParticles) works out where each is from its age alone, so any moment can be drawn straight away and
    /// every PC draws the same one. Ranges are x = least, y = most. The mesh is made here from <see cref="seed"/> (the same
    /// particles every time) and made again whenever a value that shapes it changes. The look (colours, sizes, stretch,
    /// blend) is here too, set per renderer, so one material serves every layer. The game sets <see cref="burstAt"/> to
    /// the falling piece's <see cref="moment"/> (its landing, or its going: I3) and seeks it to the time into the
    /// obstacle's cycle every frame; on its own (the workbench) it runs from when it is turned on.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VfxParticleBurst : MonoBehaviour, IVfxSeekable
    {
        /// <summary>The moment of a falling piece the game bursts it at: as it lands, or as it goes after lying there.</summary>
        public enum Moment { Landing, Gone }

        static readonly int ColorStartId = Shader.PropertyToID("_ColorStart"), ColorEndId = Shader.PropertyToID("_ColorEnd"),
            LightId = Shader.PropertyToID("_Light"), AlphaStartId = Shader.PropertyToID("_AlphaStart"),
            AlphaEndId = Shader.PropertyToID("_AlphaEnd"), FadeInId = Shader.PropertyToID("_FadeIn"),
            SizeStartId = Shader.PropertyToID("_SizeStart"), SizeEndId = Shader.PropertyToID("_SizeEnd"),
            StretchId = Shader.PropertyToID("_Stretch"), SoftId = Shader.PropertyToID("_Soft"), HardId = Shader.PropertyToID("_Hard"),
            AdditiveId = Shader.PropertyToID("_Additive"), DepthFadeId = Shader.PropertyToID("_DepthFade"),
            GravityId = Shader.PropertyToID("_Gravity"), DragId = Shader.PropertyToID("_Drag"), FloorId = Shader.PropertyToID("_FloorY"),
            AgeId = Shader.PropertyToID("_Age");

        [Header("Timing")]
        [Tooltip("Seconds after it is turned on that the burst happens (the game sets it from the moment below).")]
        public float burstAt = .1f;
        [Tooltip("The falling piece's moment the game bursts it at.")]
        public Moment moment;

        [Header("Particles")]
        [Min(1)] public int count = 20;
        public int seed = 11;
        [Tooltip("Metres out from the middle they start (least, most).")]
        public Vector2 ringRadius = new Vector2(.6f, .9f);
        [Tooltip("Metres over the floor they start (least, most).")]
        public Vector2 startHeight = new Vector2(.05f, .3f);
        [Tooltip("Metres a second outward at the start (least, most).")]
        public Vector2 outSpeed = new Vector2(2f, 5f);
        [Tooltip("Metres a second upward at the start (least, most).")]
        public Vector2 upSpeed = new Vector2(.5f, 2f);
        [Tooltip("Seconds each lives (least, most).")]
        public Vector2 life = new Vector2(.5f, .9f);
        [Tooltip("Each one's size, times the size below (least, most).")]
        public Vector2 sizeScale = new Vector2(.8f, 1.2f);
        [Min(0f), Tooltip("Most seconds one starts late.")]
        public float delay;
        [Min(0f), Tooltip("Most degrees a second one spins (when not stretched).")]
        public float spin;
        [Tooltip("Metres a second squared pulling down.")]
        public float gravity = 9.8f;
        [Min(0f), Tooltip("How fast its start speed dies away (1/s).")]
        public float drag = 1f;
        [Tooltip("Kept over the floor (its origin's height), sliding on it.")]
        public bool floor = true;

        [Header("Look")]
        [ColorUsage(false, true)] public Color colorStart = Color.white, colorEnd = Color.white;
        [Tooltip("HDR multiplier.")]
        public float brightness = 1f;
        [Range(0f, 1f)] public float alphaStart = 1f, alphaEnd;
        [Range(.001f, 1f), Tooltip("Share of its life it takes to show.")]
        public float fadeIn = .05f;
        [Tooltip("Metres across at birth and at death.")]
        public float sizeStart = .2f, sizeEnd = .4f;
        [Min(0f), Tooltip("Drawn this many seconds of travel long along its motion (0 = round, spun).")]
        public float stretch;
        [Range(.01f, 1f)] public float soft = .6f;
        [Range(0f, 1f), Tooltip("Round (0) to square (1), a chip.")]
        public float hard;
        [Range(0f, 1f), Tooltip("1 adds light (a flash, sparks), 0 lays it over (dust, chips).")]
        public float additive;
        [Range(0f, 1f), Tooltip("Metres over which it fades where it meets a surface (0 = off).")]
        public float depthFade;

        MeshRenderer view;
        MeshFilter filter;
        MaterialPropertyBlock block;
        int builtFor;
        float age;

        void Awake() => Ready();

        // Also after the scripts are reloaded in Play mode, which keeps the renderer but loses the block.
        void Ready()
        {
            if (view == null) view = GetComponent<MeshRenderer>();
            if (filter == null) filter = GetComponent<MeshFilter>();
            if (block == null) block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            age = 0f;
            Draw();
        }

        void Update()
        {
            age += Time.deltaTime;
            Draw();
        }

        /// <summary>As it is <paramref name="at"/> seconds after it was turned on (edit mode too, where Awake has not run).</summary>
        public void Seek(float at)
        {
            Ready();
            age = at;
            Draw();
        }

        void Draw()
        {
            Ready();
            float t = age - burstAt;
            view.enabled = t >= 0f && t < life.y + delay;
            if (!view.enabled) return;
            if (filter.sharedMesh == null || builtFor != Shape()) Build();
            view.GetPropertyBlock(block);
            block.SetColor(ColorStartId, colorStart);
            block.SetColor(ColorEndId, colorEnd);
            block.SetFloat(LightId, brightness);
            block.SetFloat(AlphaStartId, alphaStart);
            block.SetFloat(AlphaEndId, alphaEnd);
            block.SetFloat(FadeInId, fadeIn);
            block.SetFloat(SizeStartId, sizeStart);
            block.SetFloat(SizeEndId, sizeEnd);
            block.SetFloat(StretchId, stretch);
            block.SetFloat(SoftId, soft);
            block.SetFloat(HardId, hard);
            block.SetFloat(AdditiveId, additive);
            block.SetFloat(DepthFadeId, depthFade);
            block.SetFloat(GravityId, gravity);
            block.SetFloat(DragId, drag);
            block.SetFloat(FloorId, floor ? 0f : -1e5f);
            block.SetFloat(AgeId, t);
            view.SetPropertyBlock(block);
        }

        // Every value the mesh is made from, so a change in the inspector makes it again.
        int Shape()
        {
            unchecked
            {
                int h = count * 397 ^ seed;
                h = h * 31 + ringRadius.GetHashCode();
                h = h * 31 + startHeight.GetHashCode();
                h = h * 31 + outSpeed.GetHashCode();
                h = h * 31 + upSpeed.GetHashCode();
                h = h * 31 + life.GetHashCode();
                h = h * 31 + sizeScale.GetHashCode();
                h = h * 31 + delay.GetHashCode();
                h = h * 31 + spin.GetHashCode();
                h = h * 31 + gravity.GetHashCode();
                return h;
            }
        }

        // A quad a particle, its four corners at its start (the shader moves, sizes and turns it).
        void Build()
        {
            var rng = new System.Random(seed);
            float Range(Vector2 r) => Mathf.Lerp(r.x, r.y, (float)rng.NextDouble());
            var points = new List<Vector3>();
            var corners = new List<Vector2>();
            var particles = new List<Vector4>();
            var velocities = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < count; i++)
            {
                // Even round the ring with a little jitter, so no side is bare.
                float angle = (i + (float)rng.NextDouble() * .8f) / count * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                var start = outward * Range(ringRadius) + Vector3.up * Range(startHeight);
                var velocity = outward * Range(outSpeed) + Vector3.up * Range(upSpeed);
                var particle = new Vector4(Range(life), Range(sizeScale), (float)rng.NextDouble() * delay,
                    ((float)rng.NextDouble() * 2f - 1f) * spin);
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
            var mesh = new Mesh { name = "Burst particles", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(points);
            mesh.SetUVs(0, corners);
            mesh.SetUVs(1, particles);
            mesh.SetUVs(2, velocities);
            mesh.SetTriangles(triangles, 0);
            // Room for the furthest any can fly (no drag), up and out.
            float reach = ringRadius.y + outSpeed.y * (life.y + delay) + Mathf.Max(sizeStart, sizeEnd) * sizeScale.y + 1f;
            float rise = startHeight.y + upSpeed.y * upSpeed.y / (2f * Mathf.Max(gravity, 1f)) + upSpeed.y * .5f + 2f;
            mesh.bounds = new Bounds(new Vector3(0f, rise * .5f - .5f, 0f), new Vector3(2f * reach, rise + 1f, 2f * reach));
            if (filter.sharedMesh != null && filter.sharedMesh.hideFlags == HideFlags.DontSave)
            {
                if (Application.isPlaying) Destroy(filter.sharedMesh);
                else DestroyImmediate(filter.sharedMesh);
            }
            filter.sharedMesh = mesh;
            builtFor = Shape();
        }
    }
}
