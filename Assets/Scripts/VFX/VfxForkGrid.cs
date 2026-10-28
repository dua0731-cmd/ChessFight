using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// The knight's fork drawn out on the floor where it lands (R179, 10-28, F5 of the fork dive; reference
    /// F_04_fork.png): a square plate of light flashes under it, four lines run out along the floor from the middles of its
    /// sides, two squares each way, over <see cref="armTime"/>, a T at their ends runs on to the squares either side over
    /// <see cref="teeTime"/>, and then the eight squares a knight attacks from there flash, an outline of light faintly
    /// filled, and a shaft of light rises off each over <see cref="riseTime"/>. The shafts die away first (gone by
    /// <see cref="shaftEnd"/>), the lines, the squares and the plate after (from <see cref="fadeFrom"/> to
    /// <see cref="duration"/>). A square with an enemy on it has a thicker red edge and is <see cref="foeBoost"/> times as
    /// bright, so it is plain at a glance whom the fork struck. One mesh on the same object draws it all, the floor in its
    /// first material (VFX_ForkGridAdd) and the shafts in its second (VFX_ForkShaftAdd), one object unit a square: the
    /// object is scaled to <see cref="cell"/> metres. The game sets the square's size, which squares have floor under them
    /// and which have an enemy on them (<see cref="Configure"/>, squares in the order of SwordFightSkills.KnightMoves);
    /// every other value is here to tune in the inspector, the sizes and colours on the two materials.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VfxForkGrid : MonoBehaviour, IVfxSeekable
    {
        /// <summary>The squares a knight attacks.</summary>
        public const int Squares = 8;

        static readonly int CellId = Shader.PropertyToID("_Cell"), ArmId = Shader.PropertyToID("_ArmReveal"),
            TeeId = Shader.PropertyToID("_TeeReveal"), LitId = Shader.PropertyToID("_SquaresLit"),
            PlateId = Shader.PropertyToID("_PlateLight"), LineId = Shader.PropertyToID("_LineLight"),
            HeadId = Shader.PropertyToID("_HeadLight"), SquareId = Shader.PropertyToID("_SquareLight"),
            FoeBoostId = Shader.PropertyToID("_FoeBoost"), PlateFoeId = Shader.PropertyToID("_PlateFoe"),
            OnAId = Shader.PropertyToID("_SquareOnA"), OnBId = Shader.PropertyToID("_SquareOnB"),
            FoeAId = Shader.PropertyToID("_SquareFoeA"), FoeBId = Shader.PropertyToID("_SquareFoeB"),
            FadeId = Shader.PropertyToID("_Fade"), RiseId = Shader.PropertyToID("_Rise"),
            ShaftId = Shader.PropertyToID("_ShaftLight"), ShaftFadeId = Shader.PropertyToID("_ShaftFade");

        [Header("Squares (the game sets these)")]
        [Min(.2f), Tooltip("Metres a square (SwordFightSkillParams.knightCell).")]
        public float cell = 1.25f;
        [Tooltip("Which of the eight squares have floor under them, in SwordFightSkills.KnightMoves order.")]
        public bool[] squareOn = { true, true, true, true, true, true, true, true };
        [Tooltip("Which have an enemy on them (the prefab shows one, ahead to the right, for the workbench and the captures).")]
        public bool[] squareFoe = new bool[Squares];
        [Tooltip("An enemy on the square it lands on (stamped).")]
        public bool plateFoe;

        [Header("Timing")]
        [Min(.01f), Tooltip("Seconds for the four lines to run their two squares out.")]
        public float armTime = .08f;
        [Min(.01f), Tooltip("Seconds for the T at their ends to reach the squares.")]
        public float teeTime = .04f;
        [Min(.01f), Tooltip("Seconds the plate's flash, and each square's, takes to settle.")]
        public float flashTime = .1f;
        [Min(.01f), Tooltip("Seconds the shafts take to rise once their squares are lit.")]
        public float riseTime = .1f;
        [Min(0f), Tooltip("Seconds after the start the shafts have died away by.")]
        public float shaftEnd = .6f;
        [Min(0f), Tooltip("Seconds after the start the lines, the squares and the plate begin to die away.")]
        public float fadeFrom = .3f;
        [Min(.05f), Tooltip("Seconds after the start all of it is gone.")]
        public float duration = .8f;

        [Header("Light (HDR multipliers, Docs/VFX/README.md §6; the cores are Core Brightness times these)")]
        [Tooltip("The plate as it flashes, settling to Plate Light over Flash Time.")]
        public float plateFlash = 5f;
        public float plateLight = 1.8f;
        public float lineLight = 2f;
        [Tooltip("The bright heads of the lines as they run.")]
        public float headLight = 3f;
        [Tooltip("Each square as it is reached, settling to Square Light over Flash Time.")]
        public float squareFlash = 4f;
        public float squareLight = 2f;
        [Tooltip("Times as bright for a square with an enemy on it (its edge red).")]
        public float foeBoost = 1.5f;
        public float shaftLight = 1.5f;

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

        /// <summary>
        /// Its square's size in metres, which of the eight squares have floor under them and which have an enemy on them
        /// (SwordFightSkills.KnightMoves order; null: all on, none), and whether one is on the square it lands on.
        /// </summary>
        public void Configure(float cellSize, bool[] on, bool[] foe, bool foeOnPlate)
        {
            Ready();
            cell = Mathf.Max(.2f, cellSize);
            for (int i = 0; i < Squares; i++)
            {
                squareOn[i] = on == null || i >= on.Length || on[i];
                squareFoe[i] = foe != null && i < foe.Length && foe[i];
            }
            plateFoe = foeOnPlate;
            Draw();
        }

        bool Ready()
        {
            // Looked up here, not in Awake: a script reload in Play mode clears both (Docs/Environment/PITFALLS.md 53).
            if (view == null) view = GetComponent<MeshRenderer>();
            if (block == null) block = new MaterialPropertyBlock();
            if (squareOn == null || squareOn.Length != Squares) squareOn = Sized(squareOn, true);
            if (squareFoe == null || squareFoe.Length != Squares) squareFoe = Sized(squareFoe, false);
            return view != null;
        }

        void Draw()
        {
            if (!Ready()) return;
            view.enabled = age >= 0f && age < duration;
            if (!view.enabled) return;
            transform.localScale = Vector3.one * cell;
            // The squares light as the T reaches them; the shafts rise from then and die away first.
            float lit = armTime + teeTime, since = age - lit;
            float gone = Mathf.Clamp01((age - fadeFrom) / Mathf.Max(.01f, duration - fadeFrom));
            float shaftGone = Mathf.Clamp01((since - riseTime) / Mathf.Max(.01f, shaftEnd - lit - riseTime));
            view.GetPropertyBlock(block);
            block.SetFloat(CellId, cell);
            block.SetFloat(ArmId, Out(age / armTime));
            block.SetFloat(TeeId, Out((age - armTime) / teeTime));
            block.SetFloat(LitId, since >= 0f ? 1f : 0f);
            block.SetFloat(PlateId, Mathf.Lerp(plateFlash, plateLight, Mathf.Clamp01(age / flashTime)));
            block.SetFloat(LineId, lineLight);
            block.SetFloat(HeadId, since < 0f ? headLight : 0f);
            block.SetFloat(SquareId, Mathf.Lerp(squareFlash, squareLight, Mathf.Clamp01(since / flashTime)));
            block.SetFloat(FoeBoostId, foeBoost);
            block.SetFloat(PlateFoeId, plateFoe ? 1f : 0f);
            block.SetVector(OnAId, Flags(squareOn, 0));
            block.SetVector(OnBId, Flags(squareOn, 4));
            block.SetVector(FoeAId, Flags(squareFoe, 0));
            block.SetVector(FoeBId, Flags(squareFoe, 4));
            block.SetFloat(FadeId, (1f - gone) * (1f - gone));
            block.SetFloat(RiseId, Out(since / riseTime));
            block.SetFloat(ShaftId, shaftLight);
            block.SetFloat(ShaftFadeId, (1f - shaftGone) * (1f - shaftGone));
            view.SetPropertyBlock(block);
        }

        /// <summary>Eased out from 0 to 1: fast first, settling (0 below 0, 1 past 1).</summary>
        static float Out(float k)
        {
            k = Mathf.Clamp01(k);
            return 1f - (1f - k) * (1f - k);
        }

        static Vector4 Flags(bool[] flags, int from) =>
            new Vector4(flags[from] ? 1f : 0f, flags[from + 1] ? 1f : 0f, flags[from + 2] ? 1f : 0f, flags[from + 3] ? 1f : 0f);

        static bool[] Sized(bool[] flags, bool fill)
        {
            var sized = new bool[Squares];
            for (int i = 0; i < Squares; i++) sized[i] = flags != null && i < flags.Length ? flags[i] : fill;
            return sized;
        }
    }
}
