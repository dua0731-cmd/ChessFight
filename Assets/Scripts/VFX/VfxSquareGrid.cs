using System;
using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// The king's eight squares lit round him through his guard (R190, 10-28, F1 of his counter; reference
    /// F_01_guard_b.png): the board's lines round the square he stands on and the eight round it (his moves) run out
    /// from his square over <see cref="build"/> with a bright head and a flash that settles over <see cref="flashTime"/>,
    /// a soft band going slowly round them, the eight squares faintly filled. Through the last <see cref="blinkTime"/> of
    /// the guard they blink at <see cref="blinkRate"/>, dimming to <see cref="blinkDim"/>, and when it ends (a block, a
    /// whiff or the guard running out) they go out over <see cref="stopTime"/>. The squares keep the way he faced as it
    /// started; when he steps off the middle one they slide a square after him over <see cref="stepTime"/>. A square with
    /// no floor under it is not drawn. Its renderer (the floor quad with the VFX_SquareGridAdd material) is on the same
    /// object, which it scales to the nine squares and their run on. The game drives it (<see cref="Begin"/>,
    /// <see cref="Follow"/>, <see cref="Stop"/>); without that (the workbench and the capture tool) it lies where the
    /// prefab put it, all nine squares on floor, and plays a guard of <see cref="guard"/> seconds.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [DefaultExecutionOrder(120)]
    public sealed class VfxSquareGrid : MonoBehaviour, IVfxSeekable
    {
        static readonly int CellId = Shader.PropertyToID("_Cell"), SpanId = Shader.PropertyToID("_Span"),
            RevealId = Shader.PropertyToID("_Reveal"), PhaseId = Shader.PropertyToID("_Phase"), FloorId = Shader.PropertyToID("_Floor"),
            OverrunId = Shader.PropertyToID("_Overrun"), IntensityId = Shader.PropertyToID("_Intensity"), FadeId = Shader.PropertyToID("_Fade");

        // Without a Follow this long the game has let go of it (the king gone): it goes out.
        const float Orphan = .3f;

        [Header("Squares (the game sets the size)")]
        [Min(.2f), Tooltip("Metres a square (SwordFightSkillParams.knightCell, the knight's squares).")]
        public float cell = 1.25f;
        [Range(0f, 1f), Tooltip("Squares the lines run on past the outer edge, dying away (set on the material from here).")]
        public float overrun = .3f;
        [Min(0f), Tooltip("Metres over the floor.")]
        public float lift = .015f;

        [Header("Timing")]
        [Min(.01f), Tooltip("Seconds for the lines to run out from his square to their ends.")]
        public float build = .1f;
        [Min(.01f), Tooltip("Seconds the flash takes to settle once they are out.")]
        public float flashTime = .15f;
        [Tooltip("Radians a second the soft band goes round.")]
        public float shimmerSpeed = 2.5f;
        [Min(.05f), Tooltip("Seconds of the guard without the game (the workbench and the captures; SwordFightSkillParams.kingGuard).")]
        public float guard = .9f;
        [Min(0f), Tooltip("Seconds before the guard ends that they start to blink.")]
        public float blinkTime = .2f;
        [Min(1f), Tooltip("Blinks a second.")]
        public float blinkRate = 15f;
        [Range(0f, 1f), Tooltip("Share of their light at the bottom of a blink.")]
        public float blinkDim = .55f;
        [Min(.01f), Tooltip("Seconds they take to go out when the guard ends.")]
        public float stopTime = .06f;
        [Min(.01f), Tooltip("Seconds they take to slide a square after him.")]
        public float stepTime = .08f;

        [Header("Light (HDR multipliers, Docs/VFX/README.md §6; the cores are the material's Core Boost times these)")]
        [Tooltip("As the lines come out, settling to Intensity over Flash Time.")]
        public float flash = 2.5f;
        public float intensity = 1f;

        MeshRenderer view;
        MaterialPropertyBlock block;
        Func<Vector3, bool> floorAt;
        Vector3 from, to;
        float age, stepAt = float.NegativeInfinity, guardLeft, followedAt, stoppedAt = float.PositiveInfinity;
        int mask = 511;
        bool live;

        void OnEnable()
        {
            age = 0f;
            Draw();
        }

        void Update()
        {
            age += Time.deltaTime;
            if (live && float.IsPositiveInfinity(stoppedAt) && age - followedAt > Orphan) stoppedAt = age;
            Draw();
        }

        /// <summary>As it is <paramref name="at"/> seconds after it was turned on (the capture tool; edit mode too).</summary>
        public void Seek(float at)
        {
            age = at;
            Draw();
        }

        /// <summary>
        /// The game takes it over: squares <paramref name="cellSize"/> metres, round where it lies and turned the way it
        /// faces, with <paramref name="floor"/> saying whether there is floor at a world point (null: everywhere).
        /// </summary>
        public void Begin(float cellSize, Func<Vector3, bool> floor)
        {
            Ready();
            cell = Mathf.Max(.2f, cellSize);
            floorAt = floor;
            live = true;
            followedAt = age;
            // Round the cue's root, on the floor (this part sits Lift over it).
            from = to = transform.parent != null ? transform.parent.position : transform.position;
            mask = Mask(to);
            Draw();
        }

        /// <summary>Each frame of the guard: the floor under him (<paramref name="feet"/>) and the seconds of it left
        /// (<paramref name="left"/>; it blinks through the last of them).</summary>
        public void Follow(Vector3 feet, float left)
        {
            if (!live) return;
            followedAt = age;
            guardLeft = left;
            Vector3 fwd = transform.forward, right = transform.right, off = feet - to;
            float across = Vector3.Dot(off, right) / cell, along = Vector3.Dot(off, fwd) / cell;
            if (Mathf.Abs(across) <= .5f && Mathf.Abs(along) <= .5f) return;
            // He has stepped off the middle square: the squares slide after him, one whole square each way he went.
            from = transform.position;
            to += (right * Mathf.Round(across) + fwd * Mathf.Round(along)) * cell;
            to.y = feet.y;
            stepAt = age;
            mask = Mask(to);
        }

        /// <summary>The guard is over (a block, a whiff, or it ran out): the squares go out.</summary>
        public void Stop()
        {
            if (float.IsPositiveInfinity(stoppedAt)) stoppedAt = age;
        }

        bool Ready()
        {
            // Looked up here, not in Awake: a script reload in Play mode clears both (Docs/Environment/PITFALLS.md 53).
            if (view == null) view = GetComponent<MeshRenderer>();
            if (block == null) block = new MaterialPropertyBlock();
            return view != null;
        }

        /// <summary>Which of the nine squares round <paramref name="middle"/> have floor (bit (row + 1) * 3 + column + 1).</summary>
        int Mask(Vector3 middle)
        {
            if (floorAt == null) return 511;
            int bits = 0;
            Vector3 fwd = transform.forward, right = transform.right;
            for (int r = -1; r <= 1; r++)
                for (int c = -1; c <= 1; c++)
                    if (floorAt(middle + (right * c + fwd * r) * cell)) bits |= 1 << ((r + 1) * 3 + c + 1);
            return bits;
        }

        void Draw()
        {
            if (!Ready()) return;
            float end = live ? stoppedAt : guard, left = live ? guardLeft : guard - age;
            float fade = age < end ? 1f : 1f - (age - end) / stopTime;
            view.enabled = age >= 0f && fade > 0f;
            if (!view.enabled) return;
            float span = (3f + 2f * overrun) * cell + .2f;
            transform.localScale = Vector3.one * span;
            if (live)
            {
                float k = Mathf.Clamp01((age - stepAt) / stepTime);
                transform.position = Vector3.Lerp(from, to, 1f - (1f - k) * (1f - k)) + Vector3.up * lift;
            }
            float light = Mathf.Lerp(flash, intensity, Mathf.Clamp01((age - build) / flashTime));
            if (left < blinkTime)
                light *= Mathf.Lerp(blinkDim, 1f, .5f + .5f * Mathf.Cos(2f * Mathf.PI * blinkRate * (blinkTime - left)));
            float k0 = Mathf.Clamp01(age / build);
            view.GetPropertyBlock(block);
            block.SetFloat(CellId, cell);
            block.SetFloat(SpanId, span);
            block.SetFloat(OverrunId, overrun);
            block.SetFloat(RevealId, (1.6f + overrun) * (1f - (1f - k0) * (1f - k0)));
            block.SetFloat(PhaseId, age * shimmerSpeed);
            block.SetFloat(FloorId, mask);
            block.SetFloat(IntensityId, light);
            block.SetFloat(FadeId, Mathf.Clamp01(fade));
            view.SetPropertyBlock(block);
        }
    }
}
