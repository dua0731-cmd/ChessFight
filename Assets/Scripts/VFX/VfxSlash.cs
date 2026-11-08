using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// A crescent slash of light hanging in the air (R175, 10-28, the bishop's X where his two swords cross): a LineRenderer
    /// on the same object, laid out in its local XY plane (X right, Y up) as a shallow arc through the object's origin at
    /// its middle, leaning <see cref="lean"/> degrees from level, both ends bent up by <see cref="bend"/> and back (-Z) by
    /// <see cref="depth"/>, widest in the middle and pointed at both ends. Once each time it is turned on it is drawn from
    /// its high end down to its low end over <see cref="sweep"/>, the brightest light at the head where the blade is;
    /// <see cref="preroll"/> of that is already done when it starts (the bishop's cue starts halfway through the cut, as
    /// the blades cross). It flashes and settles from <see cref="flashIntensity"/> to <see cref="intensity"/>, hangs, and
    /// thins and dies away over the last <see cref="fadeOut"/> of <see cref="duration"/>. Its material is VFX_SlashAdd,
    /// whose reveal, intensity and fade it sets on its renderer; every value is here to tune in the inspector.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class VfxSlash : MonoBehaviour, IVfxSeekable
    {
        static readonly int FadeId = Shader.PropertyToID("_Fade"), IntensityId = Shader.PropertyToID("_Intensity"),
            RevealId = Shader.PropertyToID("_Reveal");

        [Header("Shape")]
        [Min(.1f), Tooltip("Metres from end to end.")]
        public float length = 2.2f;
        [Range(0f, 90f), Tooltip("Degrees from level, down toward the end it is drawn to.")]
        public float lean = 38f;
        [Tooltip("Drawn from the right (+X) end to the left one, as his right sword cuts; else from the left to the right.")]
        public bool leftward;
        [Tooltip("Metres both ends bend up from the line through the middle: the crescent's curve.")]
        public float bend = .18f;
        [Tooltip("Metres both ends bend back (-Z) from the middle.")]
        public float depth = .08f;
        [Min(.005f), Tooltip("Metres across at the widest, in the middle.")]
        public float width = .13f;
        [Range(.3f, 3f), Tooltip("How sharply it narrows to its points: above 1 sharper, below 1 blunter.")]
        public float taper = 1.3f;
        [Range(4, 64)]
        public int segments = 32;

        [Header("Timing")]
        [Min(.01f), Tooltip("Seconds to draw it from end to end.")]
        public float sweep = .14f;
        [Min(0f), Tooltip("Seconds of the sweep already done when it is turned on.")]
        public float preroll = .07f;
        [Min(.05f)]
        public float duration = .6f;
        [Range(.05f, 1f), Tooltip("Share of the duration it takes to thin and die away at the end.")]
        public float fadeOut = .6f;
        [Range(0f, 1f), Tooltip("Share of its width left as it dies away.")]
        public float endWidth = .35f;

        [Header("Light")]
        [Tooltip("HDR multiplier as it is drawn, settling to Intensity over Flash Time (Docs/VFX/README.md §6).")]
        public float flashIntensity = 2.4f;
        public float intensity = 1.5f;
        [Min(.01f)]
        public float flashTime = .15f;

        LineRenderer view;
        MaterialPropertyBlock block;
        Vector3[] points;
        float age;

        void OnEnable()
        {
            age = 0f;
            Shape();
            Draw();
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
            Shape();
            Draw();
        }

        /// <summary>Lays the line out from the shape values (the builder calls it so the prefab shows the arc).</summary>
        public void Shape()
        {
            if (!Ready()) return;
            int n = Mathf.Max(segments, 4) + 1;
            if (points == null || points.Length != n) points = new Vector3[n];
            float c = Mathf.Cos(lean * Mathf.Deg2Rad), s = Mathf.Sin(lean * Mathf.Deg2Rad), x = leftward ? -1f : 1f;
            // Along the line from the high end down to the low one, and square to it on its upper side.
            var along = new Vector3(x * c, -s, 0f);
            var up = new Vector3(x * s, c, 0f);
            for (int i = 0; i < n; i++)
            {
                float u = i / (n - 1f), m = (2f * u - 1f) * (2f * u - 1f);
                points[i] = along * (length * (u - .5f)) + up * (bend * m) + Vector3.back * (depth * m);
            }
            view.positionCount = n;
            view.SetPositions(points);
            var crescent = new AnimationCurve();
            // Sin(PI) comes out a hair below 0 in floats, and a negative number to a power is NaN: no line past it.
            for (int i = 0; i <= 8; i++)
                crescent.AddKey(i / 8f, Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * i / 8f)), taper));
            for (int i = 0; i < crescent.length; i++) crescent.SmoothTangents(i, 0f);
            view.widthCurve = crescent;
        }

        bool Ready()
        {
            // Looked up here, not in Awake: a script reload in Play mode clears both (Docs/Environment/PITFALLS.md 53).
            if (view == null) view = GetComponent<LineRenderer>();
            if (block == null) block = new MaterialPropertyBlock();
            return view != null;
        }

        void Draw()
        {
            if (!Ready()) return;
            view.enabled = age >= 0f && age < duration;
            if (!view.enabled) return;
            float gone = Mathf.Clamp01((age / duration - (1f - fadeOut)) / fadeOut);
            view.widthMultiplier = width * Mathf.Lerp(1f, endWidth, gone);
            view.GetPropertyBlock(block);
            block.SetFloat(RevealId, (age + preroll) / sweep);
            block.SetFloat(IntensityId, Mathf.Lerp(flashIntensity, intensity, Mathf.Clamp01(age / flashTime)));
            block.SetFloat(FadeId, (1f - gone) * (1f - gone));
            view.SetPropertyBlock(block);
        }
    }
}
