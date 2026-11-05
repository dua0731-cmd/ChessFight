using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// The swirl of air at a vent's mouth (R351, 11-04, the grand stair hall's royal fans): a disc on the fan's face
    /// (VFX_WindSwirl on a quad, <see cref="radius"/> m) of faint curling arms turning with the blades - stirring through
    /// the <see cref="warn"/> seconds before the blow, full for its <see cref="blow"/> seconds, dying away over
    /// <see cref="fadeOut"/> after. It takes the time since the blow started; a time within the warning before the next
    /// blow (<see cref="cycle"/> seconds on) counts as that warning. The game seeks it so every frame; on its own (the
    /// workbench) it runs from when it is turned on, its blow <see cref="warn"/> seconds in.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class VfxWindSwirl : MonoBehaviour, IVfxSeekable
    {
        static readonly int StrengthId = Shader.PropertyToID("_Strength"), TurnId = Shader.PropertyToID("_Turn");

        [Min(0f), Tooltip("Seconds of warning before the blow.")]
        public float warn = .8f;
        [Min(.1f), Tooltip("Seconds it blows.")]
        public float blow = 3f;
        [Min(.1f), Tooltip("Seconds from one blow's start to the next's.")]
        public float cycle = 6.8f;
        [Min(.01f), Tooltip("Seconds it takes to die away after the blow.")]
        public float fadeOut = .5f;
        [Range(0f, 1f), Tooltip("How strong it gets by the end of the warning.")]
        public float warnStrength = .45f;
        [Min(.1f), Tooltip("Metres from the axis to its rim.")]
        public float radius = .95f;
        [Tooltip("Turns a second while it blows, and while it warns.")]
        public float spin = 3f, warnSpin = 1.2f;

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
            Draw(age - warn);
        }

        void Update()
        {
            age += Time.deltaTime;
            Draw(age - warn);
        }

        /// <summary>As it is <paramref name="at"/> seconds after the blow started (edit mode too, where Awake has not run).</summary>
        public void Seek(float at)
        {
            Ready();
            age = at + warn;
            Draw(at);
        }

        void Draw(float tau)
        {
            Ready();
            if (tau >= cycle - warn) tau -= cycle;                 // the warning before the next blow
            float strength, turn;
            if (tau < -warn) strength = turn = 0f;
            else if (tau < 0f)
            {
                float w = (tau + warn) / Mathf.Max(warn, .01f);
                strength = warnStrength * w * w;
                turn = warnSpin * (tau + warn);
            }
            else
            {
                strength = Mathf.Clamp01(tau / .15f) * (1f - Mathf.Clamp01((tau - blow) / fadeOut)) * Mathf.Lerp(warnStrength, 1f, Mathf.Clamp01(tau / .3f));
                turn = warnSpin * warn + spin * tau;
            }
            view.enabled = strength > .001f;
            if (!view.enabled) return;
            transform.localScale = new Vector3(2f * radius, 2f * radius, 1f);
            view.GetPropertyBlock(block);
            block.SetFloat(StrengthId, strength);
            block.SetFloat(TurnId, turn * Mathf.PI * 2f);
            view.SetPropertyBlock(block);
        }
    }
}
