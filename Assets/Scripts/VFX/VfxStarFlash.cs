using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// A blinding star where blades meet (R185, 10-28, F2 of the king's block; reference
    /// Assets/VFX/Reference/king/F_02_parry.png): once each time it is turned on it blazes at <see cref="intensity"/>, its
    /// star shrinking from <see cref="startSize"/> to <see cref="endSize"/> and dying away over <see cref="duration"/>
    /// while its level streak lasts <see cref="streakTime"/>. In Play mode its rays are laid out afresh each time and its
    /// cross turned by up to <see cref="turnRange"/> either way; otherwise (the capture tool) they keep one layout. Its
    /// renderer (a quad with the VFX_StarFlashAdd material, which turns it to the camera) is on the same object; it sets
    /// the material's fades, intensity, turn and seed on it.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class VfxStarFlash : MonoBehaviour, IVfxSeekable
    {
        static readonly int FadeId = Shader.PropertyToID("_Fade"), StreakFadeId = Shader.PropertyToID("_StreakFade"),
            IntensityId = Shader.PropertyToID("_Intensity"), TurnId = Shader.PropertyToID("_Turn"), SeedId = Shader.PropertyToID("_Seed");

        [Min(.02f), Tooltip("Seconds the star lasts.")]
        public float duration = .14f;
        [Min(.02f), Tooltip("Seconds the level streak lasts.")]
        public float streakTime = .22f;
        [Tooltip("HDR multiplier at the start (Docs/VFX/README.md §6; the core is the material's Core Boost times it).")]
        public float intensity = 2.5f;
        [Tooltip("Height (m, ray tip to ray tip) at the start and at the end; the quad is the material's Aspect times as wide.")]
        public float startSize = 1.2f, endSize = .6f;
        [Tooltip("Degrees the cross may be turned either way, picked afresh each time in Play mode.")]
        public float turnRange = 12f;
        [Tooltip("Degrees it turns over the star's life.")]
        public float spin = 8f;

        MeshRenderer view;
        MaterialPropertyBlock block;
        float age, turn, seed;

        void OnEnable()
        {
            age = 0f;
            if (Application.isPlaying)
            {
                turn = Random.Range(-turnRange, turnRange);
                seed = Random.Range(0f, 10f);
            }
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
            view.enabled = age >= 0f && age < Mathf.Max(duration, streakTime);
            if (!view.enabled) return;
            float t = Mathf.Clamp01(age / duration), left = 1f - t, gone = Mathf.Clamp01(age / streakTime);
            transform.localScale = Vector3.one * Mathf.Lerp(startSize, endSize, 1f - left * left);
            view.GetPropertyBlock(block);
            block.SetFloat(IntensityId, intensity);
            // The star goes out fast, the streak more slowly.
            block.SetFloat(FadeId, left * left * left);
            block.SetFloat(StreakFadeId, (1f - gone) * (1f - gone));
            block.SetFloat(TurnId, turn + spin * t);
            block.SetFloat(SeedId, seed);
            view.SetPropertyBlock(block);
        }
    }
}
