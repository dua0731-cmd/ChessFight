using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// The cut of light a big sword stroke leaves (R183, 10-28, F3 of the king's counter; reference
    /// Assets/VFX/Reference/king/F_03_counter.png): a crescent band in the object's local XY plane round its origin (where
    /// the stroke turns, his sword shoulder), from <see cref="from"/> degrees (0 = +X, turning toward +Y) on through
    /// <see cref="arc"/>, its outer edge (where the blade's edge ran) <see cref="radius"/> out, widest <see cref="width"/>
    /// at <see cref="widest"/> of its length and pointed at both ends, with a soft glow <see cref="halo"/> past the edge.
    /// Once each time it is turned on it is drawn from its start to its end as the blade goes: <see cref="strikeShare"/> of
    /// it by <see cref="strikeAt"/> of <see cref="sweep"/>, speeding up, where the blow lands and it flares to
    /// <see cref="flashIntensity"/>, then on to its end slowing down (the king's swing, In2 then Out2); it settles to
    /// <see cref="intensity"/> and burns away from its start and its inside toward the edge near its end over the last
    /// <see cref="fadeOut"/> of <see cref="duration"/>. Its material is VFX_CrescentAdd, whose reveal, intensity, burn and
    /// fade it sets on its renderer (the shader turns it partly toward the camera). In Play mode it makes its own mesh from
    /// these values; the builder saves one of the same shape for the prefab and the capture tool (rebuild after changing
    /// the shape). Every value is here to tune in the inspector.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VfxCrescent : MonoBehaviour, IVfxSeekable
    {
        static readonly int RevealId = Shader.PropertyToID("_Reveal"), IntensityId = Shader.PropertyToID("_Intensity"),
            BurnId = Shader.PropertyToID("_Burn"), FadeId = Shader.PropertyToID("_Fade");

        [Header("Shape")]
        [Min(.1f), Tooltip("Metres from the middle out to the edge, where the blade's edge ran.")]
        public float radius = 1.45f;
        [Min(.01f), Tooltip("Metres across at the widest; it comes to a point at both ends.")]
        public float width = .5f;
        [Range(.1f, .9f), Tooltip("Where along it the widest part is (0.5 the middle, higher nearer its end).")]
        public float widest = .55f;
        [Range(.3f, 3f), Tooltip("How sharply it narrows to its points: above 1 sharper, below 1 blunter.")]
        public float taper = 1.1f;
        [Tooltip("Degrees where it starts, from +X turning toward +Y.")]
        public float from = -6f;
        [Range(10f, 330f), Tooltip("Degrees it runs, from +X toward +Y.")]
        public float arc = 152f;
        [Min(0f), Tooltip("Metres of soft glow past the edge at the widest.")]
        public float halo = .08f;
        [Range(8, 160)]
        public int segments = 72;

        [Header("Timing")]
        [Min(.01f), Tooltip("Seconds to draw it from end to end (the swing).")]
        public float sweep = .34f;
        [Range(.05f, .95f), Tooltip("Share of the sweep's time at which the blow lands.")]
        public float strikeAt = .25f;
        [Range(.05f, .95f), Tooltip("Share of its length drawn by then.")]
        public float strikeShare = .7f;
        [Min(0f), Tooltip("How far past its end the head light runs on once the swing is done, per sweep (it dims as it goes).")]
        public float runOn = .6f;
        [Min(.05f)]
        public float duration = .75f;
        [Range(.05f, 1f), Tooltip("Share of the duration it takes to burn away at the end.")]
        public float fadeOut = .5f;

        [Header("Light")]
        [Tooltip("HDR multiplier where the blow lands, settling to Intensity over Flash Time (Docs/VFX/README.md §6).")]
        public float flashIntensity = 1.8f;
        public float intensity = 1f;
        [Min(.01f)]
        public float flashTime = .12f;

        MeshRenderer view;
        MaterialPropertyBlock block;
        Mesh own;
        float age;

        void OnEnable()
        {
            age = 0f;
            if (Application.isPlaying && Ready() && own == null)
            {
                own = MakeMesh();
                own.hideFlags = HideFlags.DontSave;
                GetComponent<MeshFilter>().sharedMesh = own;
            }
            Draw();
        }

        void OnDestroy()
        {
            if (own != null) Destroy(own);
        }

        void Update()
        {
            age += Time.deltaTime;
            Draw();
        }

        /// <summary>As it is <paramref name="at"/> seconds after it was turned on (the capture tool; edit mode too).</summary>
        public void Seek(float at)
        {
            age = at;
            Draw();
        }

        /// <summary>The band from the shape values, one ring of three a step along it: the inner edge, the outer edge and
        /// the end of the glow past it. uv = (along, across 0 inner .. 1 edge, metres past the edge, metres across here).</summary>
        public Mesh MakeMesh()
        {
            int n = Mathf.Max(segments, 8);
            var vertices = new List<Vector3>((n + 1) * 3);
            var uvs = new List<Vector4>((n + 1) * 3);
            var triangles = new List<int>(n * 12);
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, a = (from + arc * u) * Mathf.Deg2Rad;
                var out1 = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                // Widest at `widest`, coming to a point at both ends.
                float x = u < widest ? .5f * u / widest : .5f + .5f * (u - widest) / (1f - widest);
                // Sin(PI) comes out a hair below 0 in floats, and a negative number to a power is NaN (PITFALLS 54).
                float shape = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * x)), taper);
                float across = Mathf.Max(width * shape, 1e-4f), glow = halo * Mathf.Sqrt(shape);
                vertices.Add(out1 * (radius - across));
                vertices.Add(out1 * radius);
                vertices.Add(out1 * (radius + glow));
                uvs.Add(new Vector4(u, 0f, 0f, across));
                uvs.Add(new Vector4(u, 1f, 0f, across));
                uvs.Add(new Vector4(u, 1f, glow, across));
                if (i == n) continue;
                int v = i * 3;
                for (int k = 0; k < 2; k++)
                    triangles.AddRange(new[] { v + k, v + k + 1, v + k + 3, v + k + 1, v + k + 4, v + k + 3 });
            }
            var mesh = new Mesh { name = "SM_Crescent" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            // The shader turns it toward the camera about its middle: any way round it stays in this ball.
            float reach = radius + halo;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (2f * reach));
            return mesh;
        }

        bool Ready()
        {
            // Looked up here, not in Awake: a script reload in Play mode clears both (Docs/Environment/PITFALLS.md 53).
            if (view == null) view = GetComponent<MeshRenderer>();
            if (block == null) block = new MaterialPropertyBlock();
            return view != null;
        }

        static float In2(float x) => x * x;
        static float Out2(float x) => 1f - (1f - x) * (1f - x);

        void Draw()
        {
            if (!Ready()) return;
            view.enabled = age >= 0f && age < duration;
            if (!view.enabled) return;
            float k = age / sweep, hit = strikeAt * sweep;
            float head = k < strikeAt ? strikeShare * In2(k / strikeAt)
                : strikeShare + (1f - strikeShare) * Out2(Mathf.Clamp01((k - strikeAt) / (1f - strikeAt)));
            // Once the swing is done the head light runs on off its end and dims.
            if (k > 1f) head += (k - 1f) * runOn;
            float light = age < hit ? Mathf.Lerp(intensity, flashIntensity, In2(age / hit))
                : Mathf.Lerp(intensity, flashIntensity, Mathf.Exp(-(age - hit) / flashTime));
            float gone = Mathf.Clamp01((age / duration - (1f - fadeOut)) / fadeOut);
            view.GetPropertyBlock(block);
            block.SetFloat(RevealId, head);
            block.SetFloat(IntensityId, light);
            block.SetFloat(BurnId, 1.2f * gone);
            block.SetFloat(FadeId, 1f - .4f * gone * gone);
            view.SetPropertyBlock(block);
        }
    }
}
