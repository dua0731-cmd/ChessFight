using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// The cracks a landing piece leaves in the floor (R341, 11-04, I3 of the grand stair hall's falling knights:
    /// VFX_CrackDecal on Unity's cube, only floor facing up inside the box). At the landing (<see cref="VfxDrop.lead"/>)
    /// they run out to <see cref="maxReach"/> m in <see cref="grow"/> seconds, glowing hot, cool over about
    /// <see cref="cool"/> seconds to dark scorched lines, and fade over the last <see cref="fadeOut"/> of
    /// <see cref="life"/>: keep the landing plus the life inside the obstacle's cycle (2.05 + 2.3 of 4.55 s), so they are
    /// gone before its next warning. <see cref="seed"/> picks the pattern (the game gives each piece its own). The game
    /// copies the obstacle's timing in and seeks it to the time into the obstacle's cycle every frame; on its own (the
    /// workbench) it runs from when it is turned on.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class VfxCrackDecal : MonoBehaviour, IVfxSeekable
    {
        static readonly int ReachId = Shader.PropertyToID("_Reach"), MaxReachId = Shader.PropertyToID("_MaxReach"),
            HeatId = Shader.PropertyToID("_Heat"), FadeId = Shader.PropertyToID("_Fade"), BoxId = Shader.PropertyToID("_Box"),
            SeedId = Shader.PropertyToID("_Seed");

        public VfxDrop drop = VfxDrop.Default;
        [Min(.2f), Tooltip("Metres the longest crack runs.")]
        public float maxReach = 2.8f;
        [Min(.01f), Tooltip("Seconds the cracks take to run out.")]
        public float grow = .12f;
        [Min(.05f), Tooltip("Seconds they take to cool from white-gold to dark.")]
        public float cool = .8f;
        [Min(.1f), Tooltip("Seconds after the landing they are gone.")]
        public float life = 2.3f;
        [Min(.01f), Tooltip("Seconds at the end of their life they fade over.")]
        public float fadeOut = .6f;
        [Tooltip("Picks the pattern.")]
        public float seed = 1f;
        [Min(.1f), Tooltip("The box's height (m): how much of a slope or a stair it can paint.")]
        public float height = 1.6f;
        [Tooltip("How far (m) the box reaches under its origin, the floor.")]
        public float sink = .7f;

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
            float t = age - drop.lead;
            view.enabled = t >= 0f && t < life;
            if (!view.enabled) return;
            float run = Mathf.Clamp01(t / grow);
            float side = 2.3f * maxReach;
            transform.localScale = new Vector3(side, height, side);
            transform.localPosition = new Vector3(transform.localPosition.x, height * .5f - sink, transform.localPosition.z);
            view.GetPropertyBlock(block);
            block.SetFloat(ReachId, maxReach * (1f - (1f - run) * (1f - run)));
            block.SetFloat(MaxReachId, maxReach);
            block.SetFloat(HeatId, Mathf.Exp(-3f * t / cool));
            block.SetFloat(FadeId, 1f - Mathf.Clamp01((t - (life - fadeOut)) / fadeOut));
            block.SetFloat(BoxId, side);
            block.SetFloat(SeedId, seed);
            view.SetPropertyBlock(block);
        }
    }
}
