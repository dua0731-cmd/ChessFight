using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// Where a knight will land warned in standing light, not painted on the floor (R354, 11-04, the grand stair hall's
    /// guard knights: 승규 "데칼 말고 다른 기술로 해줘"): VFX_WarnLight's ring curtain, a wall of light <see cref="radius"/> m
    /// round the landing, <see cref="height"/> m at its tallest, dying away upward with a bright top rim. It shows as the
    /// warning starts and grows taller and brighter until the landing, <see cref="lead"/> seconds on, blinking the last
    /// <see cref="urgent"/>, gone <see cref="outTime"/> after. The game sets the radius and the lead from the obstacle and
    /// seeks it to the time since its warning started every frame; on its own (the workbench) it runs from when it is
    /// turned on. In Play, where a wall stands nearer than the radius (승규 "원vfx 가 생성 될때 구조물에 짤려": the guard
    /// knights' alcoves and plinths cut it), the curtain follows the wall's face <see cref="wallGap"/> m in front of it
    /// instead of running into it (<see cref="fitToWalls"/>): rays out from the landing at two heights, only colliders
    /// that do not move (no rigidbody), once - the walls stay where they are.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VfxRingGlow : MonoBehaviour, IVfxSeekable
    {
        static readonly int FillId = Shader.PropertyToID("_Fill"), UrgencyId = Shader.PropertyToID("_Urgency"),
            FadeId = Shader.PropertyToID("_Fade"), AgeId = Shader.PropertyToID("_Age"), RadiusId = Shader.PropertyToID("_Radius"),
            HeightId = Shader.PropertyToID("_Height");
        static readonly RaycastHit[] hits = new RaycastHit[32];

        [Min(.1f), Tooltip("Metres from the landing to the curtain: the landing's reach.")]
        public float radius = 2f;
        [Min(.1f), Tooltip("Seconds from the warning to the landing.")]
        public float lead = 2.3f;
        [Min(.1f), Tooltip("Metres the curtain stands at the landing.")]
        public float height = 1.2f;
        [Min(.01f), Tooltip("Seconds before the landing it starts to blink.")]
        public float urgent = .35f;
        [Min(.01f)] public float fadeIn = .15f, outTime = .08f;
        [Range(16, 128)] public int segments = 64;
        [Tooltip("In Play, follow the face of a wall nearer than the radius instead of running into it.")]
        public bool fitToWalls = true;
        [Min(0f), Tooltip("Metres in front of a wall the curtain stands.")]
        public float wallGap = .06f;

        MeshRenderer view;
        MeshFilter filter;
        MaterialPropertyBlock block;
        int builtFor;
        float age;
        float[] reach;                  // each column's share of the radius (1 where no wall is nearer)
        Vector3 fittedAt;
        float fittedRadius;

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

        /// <summary>As it is <paramref name="at"/> seconds after its warning started (edit mode too, where Awake has not run).</summary>
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
            bool refit = Application.isPlaying && fitToWalls &&
                         (reach == null || reach.Length != segments + 1 || (transform.position - fittedAt).sqrMagnitude > 1e-4f || fittedRadius != radius);
            if (refit) Fit();
            if (refit || filter.sharedMesh == null || builtFor != segments) Build();
            filter.sharedMesh.bounds = new Bounds(new Vector3(0f, height * .5f, 0f), new Vector3(2f * radius + 1f, height + 1f, 2f * radius + 1f));
            float fade = Mathf.Clamp01(age / fadeIn) * (age < lead ? 1f : 1f - Mathf.Clamp01((age - lead) / outTime));
            view.GetPropertyBlock(block);
            block.SetFloat(FillId, Mathf.Clamp01(age / lead));
            block.SetFloat(UrgencyId, Mathf.Clamp01((age - lead + urgent) / .05f));
            block.SetFloat(FadeId, fade);
            block.SetFloat(AgeId, age);
            block.SetFloat(RadiusId, radius);
            block.SetFloat(HeightId, height);
            view.SetPropertyBlock(block);
        }

        // Each column's reach: out from the landing at two heights the curtain stands at, the nearest wall that does not
        // move (no rigidbody: not a pawn), wallGap in front of it, as a share of the radius.
        void Fit()
        {
            reach = new float[segments + 1];
            fittedAt = transform.position;
            fittedRadius = radius;
            for (int c = 0; c <= segments; c++)
            {
                float turn = c / (float)segments * Mathf.PI * 2f;
                var out_ = transform.TransformDirection(new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)));
                float near = radius;
                foreach (float up in new[] { .3f, .9f })
                {
                    int n = Physics.RaycastNonAlloc(transform.position + Vector3.up * up, out_, hits, radius, ~0, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n; i++)
                        if (hits[i].rigidbody == null && hits[i].distance < near) near = hits[i].distance;
                }
                reach[c] = Mathf.Clamp(near < radius ? near - wallGap : radius, .1f, radius) / radius;
            }
        }

        // A ring of columns (the shader sets its radius and height): uv round 0..1 and up 0..1, uv2.y each column's share
        // of the radius.
        void Build()
        {
            var points = new List<Vector3>();
            var uvs = new List<Vector2>();
            var shapes = new List<Vector4>();
            var triangles = new List<int>();
            for (int c = 0; c <= segments; c++)
                for (int r = 0; r < 2; r++)
                {
                    points.Add(Vector3.zero);
                    uvs.Add(new Vector2(c / (float)segments, r));
                    shapes.Add(new Vector4(2f, reach != null && reach.Length == segments + 1 ? reach[c] : 1f, 0f, 0f));
                }
            for (int c = 0; c < segments; c++)
            {
                int a = c * 2;
                triangles.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }
            var mesh = new Mesh { name = "Ring glow", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(points);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, shapes);
            mesh.SetTriangles(triangles, 0);
            if (filter.sharedMesh != null && filter.sharedMesh.hideFlags == HideFlags.DontSave)
            {
                if (Application.isPlaying) Destroy(filter.sharedMesh);
                else DestroyImmediate(filter.sharedMesh);
            }
            filter.sharedMesh = mesh;
            builtFor = segments;
        }
    }
}
