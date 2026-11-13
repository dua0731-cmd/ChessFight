using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// A star glint (R175, 10-28, where the bishop's two swords cross): once each time it is turned on it flashes at
    /// <see cref="intensity"/>, shrinks from <see cref="startSize"/> to <see cref="endSize"/>, turns by <see cref="spin"/>
    /// and dies away over <see cref="duration"/>. Its renderer (a quad with the VFX_GlintAdd material, which turns it to
    /// the camera) is on the same object; it sets the material's fade, intensity and turn on it.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class VfxGlint : MonoBehaviour, IVfxSeekable
    {
        static readonly int FadeId = Shader.PropertyToID("_Fade"), IntensityId = Shader.PropertyToID("_Intensity"),
            TurnId = Shader.PropertyToID("_Turn");

        [Min(.02f)] public float duration = .3f;
        [Tooltip("HDR multiplier at the start (Docs/VFX/README.md §6; the middle is about three times it).")]
        public float intensity = 5f;
        [Tooltip("Size (m from ray tip to ray tip) at the start and at the end.")]
        public float startSize = .9f, endSize = .45f;
        [Tooltip("Degrees the rays are turned at the start, and how far they turn over its life.")]
        public float turn, spin = 25f;

        MeshRenderer view;
        MaterialPropertyBlock block;
        float age;

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

        /// <summary>As it is <paramref name="at"/> seconds after it was turned on (the capture tool; edit mode too).</summary>
        public void Seek(float at)
        {
            age = at;
            Draw();
        }

        void Draw()
        {
            // Looked up here, not in Awake: a script reload in Play mode clears both (Docs/Environment/PITFALLS.md 53).
            if (view == null) view = GetComponent<MeshRenderer>();
            if (block == null) block = new MaterialPropertyBlock();
            view.enabled = age >= 0f && age < duration;
            if (!view.enabled) return;
            float t = age / duration, left = 1f - t;
            transform.localScale = Vector3.one * Mathf.Lerp(startSize, endSize, 1f - left * left);
            view.GetPropertyBlock(block);
            block.SetFloat(IntensityId, intensity);
            block.SetFloat(FadeId, left * left);
            block.SetFloat(TurnId, turn + spin * t);
            view.SetPropertyBlock(block);
        }
    }
}
