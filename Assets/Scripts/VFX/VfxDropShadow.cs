using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// The shadow a falling piece throws straight down (R341, 11-04, W2 of the grand stair hall's falling knights): a soft
    /// dark blob on the floor right under it (VFX_GroundShadowDecal on Unity's cube: only floor facing up inside the box),
    /// <see cref="radiusHigh"/> wide and <see cref="darkHigh"/> dark while the piece hangs, shrinking and darkening with
    /// its height to <see cref="radiusLow"/> and <see cref="darkLow"/> as it lands, then lying under it until it goes. It
    /// shows as the warning starts and is gone <see cref="outTime"/> after the piece's <see cref="VfxDrop.hold"/>. The game
    /// copies the obstacle's timing into <see cref="drop"/> and seeks it to the time into the obstacle's cycle every frame;
    /// on its own (the workbench) it runs from when it is turned on.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class VfxDropShadow : MonoBehaviour, IVfxSeekable
    {
        static readonly int RadiusId = Shader.PropertyToID("_Radius"), DarknessId = Shader.PropertyToID("_Darkness"),
            BoxId = Shader.PropertyToID("_Box");

        public VfxDrop drop = VfxDrop.Default;
        [Tooltip("Radius (m) while the piece hangs at its height, and as it lands.")]
        public float radiusHigh = 1.9f, radiusLow = .95f;
        [Range(0f, 1f), Tooltip("Darkness while the piece hangs at its height, and as it lands.")]
        public float darkHigh = .2f, darkLow = .65f;
        [Min(.1f), Tooltip("The box's height (m): how much of a slope or a stair it can paint.")]
        public float height = 1.6f;
        [Tooltip("How far (m) the box reaches under its origin, the floor.")]
        public float sink = .7f;
        [Min(.01f), Tooltip("Seconds it takes to show, and to go.")]
        public float fadeIn = .12f, outTime = .06f;

        MeshRenderer view;
        MaterialPropertyBlock block;
        float age;

        void Awake() => Ready();

        // Also after the scripts are reloaded in Play mode, which keeps the renderer but loses the block.
        void Ready()
        {
            if (view == null) view = GetComponent<MeshRenderer>();
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
            float end = drop.lead + drop.hold;
            view.enabled = age >= 0f && age < end + outTime;
            if (!view.enabled) return;
            // 0 while it hangs, 1 once it is down (the height's own curve: slow at first, fast at the end).
            float low = 1f - Mathf.InverseLerp(VfxDrop.Rest, drop.height, drop.Height(age));
            float fade = Mathf.Clamp01(age / fadeIn) * (age < end ? 1f : 1f - Mathf.Clamp01((age - end) / outTime));
            float side = 2.1f * Mathf.Max(radiusHigh, radiusLow);
            transform.localScale = new Vector3(side, height, side);
            transform.localPosition = new Vector3(transform.localPosition.x, height * .5f - sink, transform.localPosition.z);
            view.GetPropertyBlock(block);
            block.SetFloat(RadiusId, Mathf.Lerp(radiusHigh, radiusLow, low));
            block.SetFloat(DarknessId, Mathf.Lerp(darkHigh, darkLow, low) * fade);
            block.SetFloat(BoxId, side);
            view.SetPropertyBlock(block);
        }
    }
}
