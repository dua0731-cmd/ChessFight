using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// The shockwave a landing piece sends along the floor (R341, 11-04, I2 of the grand stair hall's falling knights): a
    /// bright band (VFX_ShockDecal on Unity's cube: only floor facing up inside the box) at exactly the reach the
    /// obstacle's hit has at that moment, out from the middle to <see cref="radius"/> evenly over the piece's
    /// <see cref="VfxDrop.hold"/> after <see cref="VfxDrop.lead"/>, as FallingChessPiece grows its hit, with a faint line at
    /// the most it will reach. Over the last <see cref="fadeOut"/> of it the light sinks to <see cref="endLight"/>; it is gone
    /// when the hit is. The game copies the obstacle's timing and reach in and seeks it to the time into the obstacle's
    /// cycle every frame; on its own (the workbench) it runs from when it is turned on.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class VfxShockDecal : MonoBehaviour, IVfxSeekable
    {
        static readonly int FrontId = Shader.PropertyToID("_Front"), RadiusId = Shader.PropertyToID("_Radius"),
            BoxId = Shader.PropertyToID("_Box"), FadeId = Shader.PropertyToID("_Fade"), AgeId = Shader.PropertyToID("_Age");

        public VfxDrop drop = VfxDrop.Default;
        [Min(.1f), Tooltip("The hit's most reach (m), the obstacle's shock radius.")]
        public float radius = 5f;
        [Min(.1f), Tooltip("The box's height (m): how much of a slope or a stair it can paint.")]
        public float height = 1.6f;
        [Tooltip("How far (m) the box reaches under its origin, the floor.")]
        public float sink = .7f;
        [Range(1f, 1.5f), Tooltip("The box's half-width over the radius.")]
        public float margin = 1.08f;
        [Range(.01f, 1f), Tooltip("Share of the shock at its end over which its light sinks.")]
        public float fadeOut = .3f;
        [Range(0f, 1f), Tooltip("Its light at the very end, against the start.")]
        public float endLight = .35f;

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
            float t = age - drop.lead, shock = Mathf.Max(drop.hold, .01f);
            view.enabled = t >= 0f && t < shock;
            if (!view.enabled) return;
            float f = t / shock;
            float side = 2f * radius * margin;
            transform.localScale = new Vector3(side, height, side);
            transform.localPosition = new Vector3(transform.localPosition.x, height * .5f - sink, transform.localPosition.z);
            view.GetPropertyBlock(block);
            block.SetFloat(FrontId, f * radius);
            block.SetFloat(RadiusId, radius);
            block.SetFloat(BoxId, side);
            block.SetFloat(FadeId, Mathf.Lerp(1f, endLight, Mathf.InverseLerp(1f - fadeOut, 1f, f)));
            block.SetFloat(AgeId, t);
            view.SetPropertyBlock(block);
        }
    }
}
