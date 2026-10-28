using System;
using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>10-28: the board square under <paramref name="at"/> (x / z): its middle (at the height of <paramref name="at"/>)
    /// and its side (m), the board's own; false where there is none.</summary>
    public delegate bool BoardSquareFinder(Vector3 at, out Vector3 centre, out float size);

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
            OverrunId = Shader.PropertyToID("_Overrun"), IntensityId = Shader.PropertyToID("_Intensity"), FadeId = Shader.PropertyToID("_Fade"),
            OwnId = Shader.PropertyToID("_Own"), RiseId = Shader.PropertyToID("_Rise"), WaveTimeId = Shader.PropertyToID("_WaveTime");

        // Without a Follow this long the game has let go of it (the king gone): it goes out.
        const float Orphan = .3f;

        [Header("Squares (the game sets the size)")]
        [Min(.2f), Tooltip("Metres a square (SwordFightSkillParams.knightCell, the knight's squares).")]
        public float cell = 1.25f;
        [Range(0f, 1f), Tooltip("Squares the lines run on past the outer edge, dying away (set on the material from here).")]
        public float overrun = .3f;
        [Min(0f), Tooltip("Metres over the floor.")]
        public float lift = .015f;
        [Tooltip("10-28: only the four sides of the square he stands on (no lines round the eight, none running on, no fill).")]
        public bool ownSquareOnly = true;

        [Header("Motes (gold specks lifting off the sides of his square)")]
        [Tooltip("The emitter (a system with no emission of its own, KingVfxBuilder's F1 Motes); empty: no motes.")]
        public ParticleSystem motes;
        [Min(0f), Tooltip("Motes a second along the four sides.")]
        public float moteRate = 22f;
        [Tooltip("Metres a second they lift (least, most) and the metres a second they wander sideways.")]
        public Vector2 moteLift = new Vector2(.12f, .4f);
        public float moteDrift = .05f;
        [Tooltip("Seconds a mote lives (least, most) and its size in metres (least, most).")]
        public Vector2 moteLife = new Vector2(.6f, 1.1f), moteSize = new Vector2(.024f, .046f);

        [Header("Wave (a ripple of gold light rising softly off the sides as far as his belly)")]
        [Tooltip("The wall of the wave (an open box 1 m on a side and 1 m high, KingVfxBuilder's F1 Wave); empty: no wave.")]
        public MeshRenderer wave;
        [Min(.1f), Tooltip("Metres high it rises: his belly (the game sets it from his hips).")]
        public float waveHeight = .9f;
        [Min(.01f), Tooltip("Seconds it takes to climb to the top.")]
        public float waveRise = .45f;
        [Min(0f), Tooltip("Its light (an HDR multiplier; it is meant to be faint).")]
        public float waveLight = .55f;

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
        MaterialPropertyBlock block, waveBlock;
        Func<Vector3, bool> floorAt;
        // 10-28: where the game has a board, his square is the board's own (its middle and side), the world's way up the page.
        BoardSquareFinder boardSquare;
        Vector3 from, to;
        float age, stepAt = float.NegativeInfinity, guardLeft, followedAt, stoppedAt = float.PositiveInfinity;
        int mask = 511;
        bool live;
        float moteDebt;

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
            EmitMotes();
        }

        /// <summary>Gold specks lifting off the four sides of his square while it is lit (not when it is only being seeked
        /// to a time for a capture).</summary>
        void EmitMotes()
        {
            if (motes == null || moteRate <= 0f || view == null || !view.enabled) return;
            // More along a longer side (the rate is for a 1.25 m square).
            moteDebt += moteRate * Mathf.Max(.5f, cell / 1.25f) * Time.deltaTime;
            int n = (int)moteDebt;
            moteDebt -= n;
            if (n <= 0) return;
            if (!motes.isPlaying) motes.Play(true);
            // The square under him: its middle is this object's, its sides along the way he faces (a direction, whatever the scale).
            Vector3 right = transform.right, fwd = transform.forward, centre = transform.position;
            var e = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                // Anywhere along the four sides, as one run of four squares' lengths.
                float u = UnityEngine.Random.value * 4f;
                int side = Mathf.Min((int)u, 3);
                float t = u - side - .5f;
                Vector2 at = side == 0 ? new Vector2(t, .5f) : side == 1 ? new Vector2(.5f, -t) : side == 2 ? new Vector2(-t, -.5f) : new Vector2(-.5f, t);
                e.position = centre + (right * at.x + fwd * at.y) * cell;
                e.velocity = new Vector3(UnityEngine.Random.Range(-1f, 1f) * moteDrift, UnityEngine.Random.Range(moteLift.x, moteLift.y),
                    UnityEngine.Random.Range(-1f, 1f) * moteDrift);
                e.startLifetime = UnityEngine.Random.Range(moteLife.x, moteLife.y);
                e.startSize = UnityEngine.Random.Range(moteSize.x, moteSize.y);
                e.startColor = Color.white;
                motes.Emit(e, 1);
            }
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
        public void Begin(float cellSize, Func<Vector3, bool> floor, BoardSquareFinder board = null)
        {
            Ready();
            cell = Mathf.Max(.2f, cellSize);
            floorAt = floor;
            live = true;
            followedAt = age;
            // Round the cue's root, on the floor (this part sits Lift over it).
            from = to = transform.parent != null ? transform.parent.position : transform.position;
            boardSquare = null;
            // 10-28 (the user: "the border of the one board square the king stands on"): on a board, the square he stands on
            // as the board lies - its middle, its side, the board's axes - not a square of his own measuring.
            if (board != null && board(to, out Vector3 centre, out float side))
            {
                boardSquare = board;
                cell = Mathf.Max(.2f, side);
                from = to = new Vector3(centre.x, to.y, centre.z);
                transform.rotation = Quaternion.identity;
            }
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
            if (boardSquare != null)
            {
                // He keeps to the square he started on until he is clearly (a hand's breadth) over the edge of another.
                Vector3 away = feet - to;
                float reach = cell * .5f + .12f;
                if ((Mathf.Abs(away.x) > reach || Mathf.Abs(away.z) > reach) && boardSquare(feet, out Vector3 centre, out float side))
                {
                    from = transform.position;
                    to = new Vector3(centre.x, feet.y, centre.z);
                    cell = Mathf.Max(.2f, side);
                    stepAt = age;
                }
                return;
            }
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
            if (waveBlock == null) waveBlock = new MaterialPropertyBlock();
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
            if (wave != null) wave.enabled = view.enabled;
            if (!view.enabled) return;
            // His own square alone needs a quad of just that and the glow round its sides.
            float span = ownSquareOnly ? cell + .4f : (3f + 2f * overrun) * cell + .2f;
            transform.localScale = Vector3.one * span;
            if (live)
            {
                float k = Mathf.Clamp01((age - stepAt) / stepTime);
                transform.position = Vector3.Lerp(from, to, 1f - (1f - k) * (1f - k)) + Vector3.up * lift;
            }
            float light = Mathf.Lerp(flash, intensity, Mathf.Clamp01((age - build) / flashTime));
            float blink = 1f;
            if (left < blinkTime)
                blink = Mathf.Lerp(blinkDim, 1f, .5f + .5f * Mathf.Cos(2f * Mathf.PI * blinkRate * (blinkTime - left)));
            light *= blink;
            DrawWave(fade, blink);
            float k0 = Mathf.Clamp01(age / build);
            view.GetPropertyBlock(block);
            block.SetFloat(CellId, cell);
            block.SetFloat(SpanId, span);
            block.SetFloat(OverrunId, overrun);
            block.SetFloat(RevealId, (1.6f + overrun) * (1f - (1f - k0) * (1f - k0)));
            block.SetFloat(PhaseId, age * shimmerSpeed);
            block.SetFloat(FloorId, mask);
            block.SetFloat(OwnId, ownSquareOnly ? 1f : 0f);
            block.SetFloat(IntensityId, light);
            block.SetFloat(FadeId, Mathf.Clamp01(fade));
            view.SetPropertyBlock(block);
        }

        /// <summary>The wave: a wall on the four sides of his square (its middle and axes the grid's, its side the cell) climbing
        /// from the floor to his belly over <see cref="waveRise"/>, then moving on while the guard holds; it blinks and goes
        /// out with the lines.</summary>
        void DrawWave(float fade, float blink)
        {
            if (wave == null) return;
            float climb = Mathf.Clamp01(age / waveRise);
            var t = wave.transform;
            t.SetPositionAndRotation(transform.position - Vector3.up * lift, transform.rotation);
            t.localScale = new Vector3(cell, waveHeight, cell);
            wave.GetPropertyBlock(waveBlock);
            // The front runs a little past the top (1.2) so the whole height is lit by the time it has climbed.
            waveBlock.SetFloat(RiseId, 1.2f * (1f - (1f - climb) * (1f - climb)));
            waveBlock.SetFloat(WaveTimeId, age);
            waveBlock.SetFloat(IntensityId, waveLight * blink);
            waveBlock.SetFloat(FadeId, Mathf.Clamp01(fade));
            wave.SetPropertyBlock(waveBlock);
        }
    }
}
