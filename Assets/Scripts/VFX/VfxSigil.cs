using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// A pattern on the ground played once each time it is turned on (R168, 10-27, the king's landing sigil): it opens from
    /// the centre out in <see cref="reveal"/> with a bright edge, flashes and settles from <see cref="flashIntensity"/> to
    /// <see cref="intensity"/>, grows from <see cref="startScale"/> to <see cref="endScale"/>, turns at <see cref="spin"/>
    /// and dies away over the last <see cref="fadeOut"/> of <see cref="duration"/>. Its renderer (a flat quad with the
    /// VFX_SigilAdd material) is on the same object; every value is here to tune in the inspector.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class VfxSigil : MonoBehaviour, IVfxSeekable
    {
        static readonly int FadeId = Shader.PropertyToID("_Fade"), IntensityId = Shader.PropertyToID("_Intensity"),
            RevealId = Shader.PropertyToID("_Reveal");

        [Min(.1f)] public float duration = 1.6f;
        [Tooltip("Seconds to open from the centre to the rim.")]
        [Min(.01f)] public float reveal = .14f;
        [Tooltip("HDR multiplier as it opens, settling to Intensity over Flash Time.")]
        public float flashIntensity = 5f;
        public float intensity = 2.2f;
        [Min(.01f)] public float flashTime = .25f;
        [Tooltip("Size (m across) at the start and at the end.")]
        public float startScale = 3.4f, endScale = 4.2f;
        [Tooltip("Degrees a second.")]
        public float spin = 25f;
        [Range(.05f, 1f), Tooltip("Share of the duration it takes to die away at the end.")]
        public float fadeOut = .45f;

        MeshRenderer view;
        MaterialPropertyBlock block;
        Quaternion rest;
        float age;

        void Awake()
        {
            view = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
            rest = transform.localRotation;
        }

        void OnEnable()
        {
            age = 0f;
            view.enabled = true;
            Draw();
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= duration)
            {
                view.enabled = false;
                return;
            }
            Draw();
        }

        /// <summary>As it is <paramref name="at"/> seconds after it was turned on (R172; edit mode too, where Awake has not run).</summary>
        public void Seek(float at)
        {
            if (view == null) Awake();
            age = at;
            view.enabled = age >= 0f && age < duration;
            if (view.enabled) Draw();
        }

        void Draw()
        {
            float t = Mathf.Clamp01(age / duration);
            float grow = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, grow);
            transform.localRotation = rest * Quaternion.Euler(0f, spin * age, 0f);
            float open = Mathf.Clamp01(age / reveal);
            float fade = t < 1f - fadeOut ? 1f : (1f - t) / fadeOut;
            view.GetPropertyBlock(block);
            // Past 1 the reveal's bright edge has left the pattern.
            block.SetFloat(RevealId, open < 1f ? 1f - (1f - open) * (1f - open) : 1.2f);
            block.SetFloat(IntensityId, Mathf.Lerp(flashIntensity, intensity, Mathf.Clamp01(age / flashTime)));
            block.SetFloat(FadeId, fade * fade);
            view.SetPropertyBlock(block);
        }
    }
}
