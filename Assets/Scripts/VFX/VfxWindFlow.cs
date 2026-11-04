using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// The air a vent blows (R351, 11-04, the grand stair hall's royal fans: 승규 "선풍기 vfx 적용 시켜줘 데칼 말고"):
    /// <see cref="count"/> streaks of air (or, <see cref="round"/>, motes of dust) out of the fan along its +z, wound round
    /// its axis near it, spreading and wavering, gone by <see cref="reach"/> m (VFX_WindFlow). Each runs out again and again
    /// at its own phase for the <see cref="blow"/> seconds after <see cref="blowAt"/>, and its last run finishes its way
    /// after; the shader works out where each is from the time alone. Ranges are x = least, y = most. The mesh is made
    /// here from <see cref="seed"/> and made again whenever a value that shapes it changes; the look is set per renderer,
    /// so one material serves streaks and motes. The game seeks it to the time since the vent's last blow started every
    /// frame (blowAt 0); on its own (the workbench) it runs from when it is turned on.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VfxWindFlow : MonoBehaviour, IVfxSeekable
    {
        static readonly int ColorId = Shader.PropertyToID("_Color"), LightId = Shader.PropertyToID("_Light"),
            AlphaId = Shader.PropertyToID("_Alpha"), AdditiveId = Shader.PropertyToID("_Additive"), SpeedId = Shader.PropertyToID("_Speed"),
            ReachId = Shader.PropertyToID("_Reach"), GapId = Shader.PropertyToID("_Gap"), LengthId = Shader.PropertyToID("_Length"),
            WidthId = Shader.PropertyToID("_Width"), TwistId = Shader.PropertyToID("_Twist"), TwistFadeId = Shader.PropertyToID("_TwistFade"),
            SpreadId = Shader.PropertyToID("_Spread"), WobbleId = Shader.PropertyToID("_Wobble"), RoundId = Shader.PropertyToID("_Round"),
            TauId = Shader.PropertyToID("_Tau"), BlowId = Shader.PropertyToID("_Blow");

        [Header("Timing")]
        [Tooltip("Seconds after it is turned on that the blow starts (the game seeks it from the blow's start).")]
        public float blowAt = .8f;
        [Min(.1f), Tooltip("Seconds it blows.")]
        public float blow = 3f;

        [Header("Streaks")]
        [Min(1)] public int count = 26;
        public int seed = 21;
        [Range(1, 24), Tooltip("Rows along each streak (more bend round the twist smoothly).")]
        public int segments = 8;
        [Tooltip("Metres out from the fan's axis they leave it (least, most).")]
        public Vector2 laneRadius = new Vector2(.2f, .85f);
        [Tooltip("Their speed, times Speed (least, most).")]
        public Vector2 speedScale = new Vector2(.8f, 1.2f);
        [Tooltip("Their length, times Length (least, most).")]
        public Vector2 lengthScale = new Vector2(.75f, 1.35f);
        [Tooltip("Their width, times Width (least, most).")]
        public Vector2 widthScale = new Vector2(.7f, 1.4f);
        [Tooltip("Their light, times Brightness (least, most).")]
        public Vector2 lightScale = new Vector2(.5f, 1f);

        [Header("Flow")]
        [Min(.1f), Tooltip("Metres a second.")]
        public float speed = 9f;
        [Min(.1f), Tooltip("Metres out from the fan they are gone.")]
        public float reach = 6f;
        [Min(0f), Tooltip("Seconds each rests between its runs.")]
        public float gap = .15f;
        [Min(.01f)] public float length = 1.6f;
        [Min(.005f)] public float width = .05f;
        [Tooltip("Radians a metre they wind round the axis near the fan.")]
        public float twist = 1.6f;
        [Min(.01f), Tooltip("Metres over which the winding dies away.")]
        public float twistFade = 2.2f;
        [Tooltip("How much wider the flow gets a metre out.")]
        public float spread = .1f;
        [Min(0f)] public float wobble = .06f;

        [Header("Look")]
        [ColorUsage(false, true)] public Color color = new Color(.86f, .93f, 1f);
        [Tooltip("HDR multiplier.")]
        public float brightness = 1.5f;
        [Range(0f, 1f)] public float alpha = .35f;
        [Range(0f, 1f), Tooltip("1 adds light, 0 lays it over.")]
        public float additive = .5f;
        [Tooltip("Round motes of dust instead of streaks.")]
        public bool round;

        MeshRenderer view;
        MeshFilter filter;
        MaterialPropertyBlock block;
        int builtFor;
        float age;

        void Awake() => Ready();

        // Also after the scripts are reloaded in Play mode, which keeps the renderer but loses the block.
        void Ready()
        {
            if (view == null) view = GetComponent<MeshRenderer>();
            if (filter == null) filter = GetComponent<MeshFilter>();
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
            float tau = age - blowAt;
            // Until the slowest last run is out of reach.
            float done = blow + (reach + length * lengthScale.y) / Mathf.Max(.1f, speed * speedScale.x);
            view.enabled = tau >= 0f && tau < done;
            if (!view.enabled) return;
            if (filter.sharedMesh == null || builtFor != Shape()) Build();
            view.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            block.SetFloat(LightId, brightness);
            block.SetFloat(AlphaId, alpha);
            block.SetFloat(AdditiveId, additive);
            block.SetFloat(SpeedId, speed);
            block.SetFloat(ReachId, reach);
            block.SetFloat(GapId, gap);
            block.SetFloat(LengthId, length);
            block.SetFloat(WidthId, width);
            block.SetFloat(TwistId, twist);
            block.SetFloat(TwistFadeId, twistFade);
            block.SetFloat(SpreadId, spread);
            block.SetFloat(WobbleId, wobble);
            block.SetFloat(RoundId, round ? 1f : 0f);
            block.SetFloat(TauId, tau);
            block.SetFloat(BlowId, blow);
            view.SetPropertyBlock(block);
        }

        // Every value the mesh is made from, so a change in the inspector makes it again.
        int Shape()
        {
            unchecked
            {
                int h = count * 397 ^ seed;
                h = h * 31 + segments;
                h = h * 31 + laneRadius.GetHashCode();
                h = h * 31 + speedScale.GetHashCode();
                h = h * 31 + lengthScale.GetHashCode();
                h = h * 31 + widthScale.GetHashCode();
                h = h * 31 + lightScale.GetHashCode();
                h = h * 31 + reach.GetHashCode();
                h = h * 31 + spread.GetHashCode();
                return h;
            }
        }

        // A ribbon of rows a streak, every vertex at the fan (the shader lays it along its lane).
        void Build()
        {
            var rng = new System.Random(seed);
            float Range(Vector2 r) => Mathf.Lerp(r.x, r.y, (float)rng.NextDouble());
            int rows = round ? 1 : Mathf.Max(1, segments);
            var points = new List<Vector3>();
            var corners = new List<Vector2>();
            var lanes = new List<Vector4>();
            var looks = new List<Vector4>();
            var triangles = new List<int>();
            for (int i = 0; i < count; i++)
            {
                // Even round the axis with a little jitter, the radius even over the disc's area.
                float angle = (i + (float)rng.NextDouble() * .8f) / count * Mathf.PI * 2f;
                float u = (float)rng.NextDouble();
                float radius = Mathf.Sqrt(Mathf.Lerp(laneRadius.x * laneRadius.x, laneRadius.y * laneRadius.y, u));
                var lane = new Vector4(angle, radius, (float)rng.NextDouble(), Range(speedScale));
                var look = new Vector4(Range(lengthScale), Range(widthScale), Range(lightScale), (float)rng.NextDouble());
                int at = points.Count;
                for (int r = 0; r <= rows; r++)
                    for (int c = 0; c < 2; c++)
                    {
                        points.Add(Vector3.zero);
                        corners.Add(new Vector2(c, r / (float)rows));
                        lanes.Add(lane);
                        looks.Add(look);
                    }
                for (int r = 0; r < rows; r++)
                {
                    int a = at + r * 2;
                    triangles.AddRange(new[] { a, a + 2, a + 1, a + 2, a + 3, a + 1 });
                }
            }
            var mesh = new Mesh { name = "Wind flow", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(points);
            mesh.SetUVs(0, corners);
            mesh.SetUVs(1, lanes);
            mesh.SetUVs(2, looks);
            mesh.SetTriangles(triangles, 0);
            // Room for the flow as the shader lays it out.
            float wide = laneRadius.y * (1f + spread * reach) + wobble + 1f;
            mesh.bounds = new Bounds(new Vector3(0f, 0f, reach * .5f), new Vector3(2f * wide, 2f * wide, reach + 1f));
            if (filter.sharedMesh != null && filter.sharedMesh.hideFlags == HideFlags.DontSave)
            {
                if (Application.isPlaying) Destroy(filter.sharedMesh);
                else DestroyImmediate(filter.sharedMesh);
            }
            filter.sharedMesh = mesh;
            builtFor = Shape();
        }
    }
}
