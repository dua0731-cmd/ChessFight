using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// A warning painted on the floor where something is about to strike (R341, 11-04, W1 of the grand stair hall's
    /// falling knights: 승규 "기본 데칼 말고 고급 데칼 넣어서 차오르는 애니메이션"). Its renderer is a box (Unity's cube) with
    /// the VFX_GroundWarnDecal material, which paints only the floor inside the box that faces up: a step, a slope or the
    /// stair's edge as they are, never the air past them. The danger disc (<see cref="radius"/>) shows at once in a faint
    /// shade; its fill runs from the middle to the rim over <see cref="lead"/> seconds, so it is full the moment the blow
    /// lands, while the ticks round the rim light one by one; the last <see cref="urgent"/> seconds it blinks, and it is
    /// gone <see cref="outTime"/> after <see cref="lead"/>. The game sets the radius and the lead from the obstacle and
    /// seeks it to the time into the obstacle's cycle every frame; on its own (the workbench) it runs from when it is
    /// turned on.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class VfxGroundWarn : MonoBehaviour, IVfxSeekable
    {
        static readonly int FillId = Shader.PropertyToID("_Fill"), SweepId = Shader.PropertyToID("_Sweep"),
            UrgencyId = Shader.PropertyToID("_Urgency"), FadeId = Shader.PropertyToID("_Fade"), AgeId = Shader.PropertyToID("_Age"),
            RadiusId = Shader.PropertyToID("_Radius"), MarginId = Shader.PropertyToID("_Margin");

        [Min(.1f), Tooltip("The danger disc's radius (m): the strike's reach.")]
        public float radius = 5f;
        [Min(.1f), Tooltip("Seconds from when it shows to the strike; the fill reaches the rim then.")]
        public float lead = 2.05f;
        [Min(.1f), Tooltip("The box's height (m): how much of a slope or a stair it can paint.")]
        public float height = 1.6f;
        [Tooltip("How far (m) the box reaches under its origin, the floor.")]
        public float sink = .7f;
        [Range(1f, 1.5f), Tooltip("The box's half-width over the radius, room for the rim's glow.")]
        public float margin = 1.12f;
        [Min(.01f), Tooltip("Seconds it takes to show.")]
        public float fadeIn = .12f;
        [Min(.01f), Tooltip("Seconds before the strike it starts to blink.")]
        public float urgent = .35f;
        [Min(.01f), Tooltip("Seconds after the strike it takes to go.")]
        public float outTime = .06f;
        [Range(.2f, 1f), Tooltip("How the fill runs: 1 = even, less = quick at first and slow to the rim.")]
        public float fillCurve = .85f;

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
            view.enabled = age >= 0f && age < lead + outTime;
            if (!view.enabled) return;
            float side = 2f * radius * margin;
            transform.localScale = new Vector3(side, height, side);
            transform.localPosition = new Vector3(transform.localPosition.x, height * .5f - sink, transform.localPosition.z);
            float run = Mathf.Clamp01(age / lead);
            float fade = Mathf.Clamp01(age / fadeIn) * (age < lead ? 1f : 1f - Mathf.Clamp01((age - lead) / outTime));
            view.GetPropertyBlock(block);
            block.SetFloat(FillId, Mathf.Pow(run, fillCurve));
            block.SetFloat(SweepId, run);
            block.SetFloat(UrgencyId, Mathf.Clamp01((age - lead + urgent) / .05f));
            block.SetFloat(FadeId, fade);
            block.SetFloat(AgeId, age);
            block.SetFloat(RadiusId, radius);
            block.SetFloat(MarginId, margin);
            view.SetPropertyBlock(block);
        }
    }
}
