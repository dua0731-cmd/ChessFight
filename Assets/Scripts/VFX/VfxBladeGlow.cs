using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// The king's blade alight through his guard (R190, 10-28, F1 of his counter; reference F_01_guard_b.png): after
    /// <see cref="delay"/> a run of gold light goes up the blade from the hilt to the point over <see cref="run"/>, the
    /// point flashes as it gets there and keeps a small steady sparkle, and a soft glow stays on the blade, brighter toward
    /// the point. Through the last <see cref="blinkTime"/> of the guard it blinks with the squares
    /// (<see cref="VfxSquareGrid"/>), and when the guard ends it goes out over <see cref="stopTime"/>. The blade is a
    /// LineRenderer on the same object (the VFX_SlashAdd material, whose reveal, intensity and fade it sets; its points in
    /// world space, a hair toward the camera so the light lies on the steel's face); the point is a child named
    /// <see cref="TipName"/> with the VFX_GlintAdd material on a quad. The game hands it the blade each frame
    /// (<see cref="Follow"/>, <see cref="Hold"/>, <see cref="Stop"/>); without that (the workbench and the capture tool)
    /// the blade lies where <see cref="previewGuard"/> and <see cref="previewTip"/> put it and the guard lasts
    /// <see cref="previewTime"/>.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [DefaultExecutionOrder(120)]
    public sealed class VfxBladeGlow : MonoBehaviour, IVfxSeekable
    {
        /// <summary>Where the blade is now, from the hilt end of the steel to its point; false while there is none.</summary>
        public delegate bool Source(out Vector3 guard, out Vector3 tip);

        public const string TipName = "Tip";

        static readonly int FadeId = Shader.PropertyToID("_Fade"), IntensityId = Shader.PropertyToID("_Intensity"),
            RevealId = Shader.PropertyToID("_Reveal"), TurnId = Shader.PropertyToID("_Turn");

        // Without a Hold this long the game has let go of it (the king gone): it goes out.
        const float Orphan = .3f;

        [Header("Without the game (the workbench and the captures), in the cue's space")]
        [Tooltip("The hilt end of the steel in his guard (SwordFightPawn.BladeRoot).")]
        public Vector3 previewGuard = new Vector3(.18f, .95f, .32f);
        [Tooltip("The point (SwordFightPawn.BladeTip): his guard holds the blade across to his left, a little up.")]
        public Vector3 previewTip = new Vector3(-.55f, 1.06f, .4f);
        [Min(.05f), Tooltip("Seconds of the guard (SwordFightSkillParams.kingGuard).")]
        public float previewTime = .9f;

        [Header("Timing")]
        [Min(0f), Tooltip("Seconds before the light starts up the blade (he turns it into the guard over the first frames).")]
        public float delay = .05f;
        [Min(.01f), Tooltip("Seconds for the light to run from the hilt to the point.")]
        public float run = .1f;
        [Min(.01f), Tooltip("Seconds the point's flash, and the run's light, take to settle.")]
        public float settle = .15f;
        [Min(0f), Tooltip("Seconds before the guard ends that it starts to blink.")]
        public float blinkTime = .2f;
        [Min(1f), Tooltip("Blinks a second.")]
        public float blinkRate = 15f;
        [Range(0f, 1f), Tooltip("Share of its light at the bottom of a blink.")]
        public float blinkDim = .55f;
        [Min(.01f), Tooltip("Seconds it takes to go out when the guard ends.")]
        public float stopTime = .06f;

        [Header("Shape")]
        [Min(.005f), Tooltip("Metres across.")]
        public float width = .05f;
        [Min(0f), Tooltip("Metres toward the camera off the steel.")]
        public float toCamera = .03f;
        [Range(2, 32)]
        public int segments = 8;

        [Header("Light (HDR multipliers, Docs/VFX/README.md §6)")]
        [Tooltip("As the light runs up, settling to Intensity.")]
        public float runIntensity = 2.5f;
        public float intensity = 1.1f;
        [Min(1f), Tooltip("How far past the point the run ends: past 1 the whole blade shows, brighter toward the point.")]
        public float hold = 1.1f;
        [Tooltip("The point as the light gets there, settling to Sparkle.")]
        public float glint = 4f;
        public float sparkle = 1.4f;
        [Tooltip("Metres from ray tip to ray tip as it flashes, and as it settles.")]
        public float glintSize = .4f, sparkleSize = .16f;
        [Tooltip("Degrees a second the sparkle turns.")]
        public float spin = 40f;

        LineRenderer view;
        MeshRenderer tip;
        MaterialPropertyBlock block, tipBlock;
        Vector3[] points;
        Source source;
        float age, guardLeft, heldAt, stoppedAt = float.PositiveInfinity;
        bool live;

        void OnEnable()
        {
            age = 0f;
            Draw();
        }

        void LateUpdate()
        {
            // After the ragdoll has moved the sword this frame, so the light keeps to it.
            age += Time.deltaTime;
            if (live && float.IsPositiveInfinity(stoppedAt) && age - heldAt > Orphan) stoppedAt = age;
            Draw();
        }

        /// <summary>As it is <paramref name="at"/> seconds after it was turned on (the capture tool; edit mode too).</summary>
        public void Seek(float at)
        {
            age = at;
            Draw();
        }

        /// <summary>The game takes it over: the blade from <paramref name="blade"/> each frame.</summary>
        public void Follow(Source blade)
        {
            Ready();
            source = blade;
            live = true;
            heldAt = age;
            guardLeft = previewTime;
        }

        /// <summary>Each frame of the guard: the seconds of it left (it blinks through the last of them).</summary>
        public void Hold(float left)
        {
            heldAt = age;
            guardLeft = left;
        }

        /// <summary>The guard is over (a block, a whiff, or it ran out): the light goes out.</summary>
        public void Stop()
        {
            if (float.IsPositiveInfinity(stoppedAt)) stoppedAt = age;
        }

        bool Ready()
        {
            // Looked up here, not in Awake: a script reload in Play mode clears them (Docs/Environment/PITFALLS.md 53).
            if (view == null) view = GetComponent<LineRenderer>();
            if (tip == null)
            {
                var t = transform.Find(TipName);
                if (t != null) tip = t.GetComponent<MeshRenderer>();
            }
            if (block == null) block = new MaterialPropertyBlock();
            if (tipBlock == null) tipBlock = new MaterialPropertyBlock();
            return view != null;
        }

        bool Blade(out Vector3 guard, out Vector3 point)
        {
            if (live) return source(out guard, out point);
            var space = transform.parent != null ? transform.parent : transform;
            guard = space.TransformPoint(previewGuard);
            point = space.TransformPoint(previewTip);
            return true;
        }

        void Draw()
        {
            if (!Ready()) return;
            float end = live ? stoppedAt : previewTime, left = live ? guardLeft : previewTime - age;
            float fade = age < end ? 1f : 1f - (age - end) / stopTime, since = age - delay;
            Vector3 guard = default, point = default;
            bool on = age >= 0f && fade > 0f && since >= 0f && (!live || source != null) && Blade(out guard, out point);
            view.enabled = on;
            if (tip != null) tip.enabled = on;
            if (!on) return;

            var eye = Camera.main;
            Vector3 toEye = eye != null ? (eye.transform.position - Vector3.Lerp(guard, point, .5f)).normalized * toCamera : Vector3.zero;
            int n = Mathf.Max(segments, 2) + 1;
            if (points == null || points.Length != n) points = new Vector3[n];
            for (int i = 0; i < n; i++) points[i] = Vector3.Lerp(guard, point, i / (n - 1f)) + toEye;
            view.useWorldSpace = true;
            view.positionCount = n;
            view.SetPositions(points);
            view.widthMultiplier = width;

            float ran = Mathf.Clamp01(since / run), settled = Mathf.Clamp01((since - run) / settle);
            float blink = left < blinkTime
                ? Mathf.Lerp(blinkDim, 1f, .5f + .5f * Mathf.Cos(2f * Mathf.PI * blinkRate * (blinkTime - left))) : 1f;
            fade = Mathf.Clamp01(fade);
            view.GetPropertyBlock(block);
            block.SetFloat(RevealId, hold * (1f - (1f - ran) * (1f - ran)));
            block.SetFloat(IntensityId, Mathf.Lerp(runIntensity, intensity, settled) * blink);
            block.SetFloat(FadeId, fade);
            view.SetPropertyBlock(block);

            if (tip == null) return;
            // The point lights as the run gets there.
            tip.enabled = ran >= 1f;
            if (!tip.enabled) return;
            tip.transform.position = point + toEye;
            tip.transform.localScale = Vector3.one * Mathf.Lerp(glintSize, sparkleSize, 1f - (1f - settled) * (1f - settled));
            tip.GetPropertyBlock(tipBlock);
            tipBlock.SetFloat(IntensityId, Mathf.Lerp(glint, sparkle, settled) * blink);
            tipBlock.SetFloat(FadeId, fade);
            tipBlock.SetFloat(TurnId, spin * since);
            tip.SetPropertyBlock(tipBlock);
        }
    }
}
