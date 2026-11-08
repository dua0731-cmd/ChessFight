using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// A swirl drawn in on the ground once each time it is turned on (R163, 10-27, the gathering before an impact): spiral
    /// arms winding from the rim to the centre and a few crescents riding outside the rim, all turning faster and drawing
    /// in to a point over <see cref="duration"/>. Flat in the XZ plane, drawn with <see cref="material"/>
    /// (VFX_ScrollDissolveAdd: u runs along each arm, rim to centre, so its noise flows inward).
    /// </summary>
    public sealed class VfxSwirl : MonoBehaviour
    {
        const int ArmSteps = 24, ShardSteps = 12;
        static readonly int FadeId = Shader.PropertyToID("_Fade"), DissolveId = Shader.PropertyToID("_Dissolve"),
            IntensityId = Shader.PropertyToID("_Intensity"), UFadeId = Shader.PropertyToID("_UFade");

        public Material material;
        [Min(.05f)] public float duration = .5f;
        [Min(1)] public int arms = 5;
        [Tooltip("Outer radius at the start (m).")]
        public float radius = 1.8f;
        [Range(0f, 1f), Tooltip("Its size at the end, as a share of the start.")]
        public float endScale = .06f;
        [Tooltip("How far round each arm winds from the rim to the centre (turns).")]
        public float turns = .7f;
        [Tooltip("Arm width at the rim (m); the arms thin toward the centre.")]
        public float width = .14f;
        [Tooltip("Degrees a second at the start; it turns three times as fast by the end.")]
        public float spin = 200f;
        [Min(0)] public int shards = 4;
        [Tooltip("How long each crescent is (degrees).")]
        public float shardArc = 32f;
        public float fadeIn = .1f;
        [Tooltip("HDR multiplier on the material's colour (see Docs/VFX §6).")]
        public float intensity = 1.6f;
        [Range(0f, 1f)] public float dissolveEnd = .3f;

        Mesh mesh;
        Vector3[] vertices;
        MeshRenderer view;
        MaterialPropertyBlock block;
        float age, turned;

        void Awake()
        {
            var filter = gameObject.AddComponent<MeshFilter>();
            view = gameObject.AddComponent<MeshRenderer>();
            view.sharedMaterial = material;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            int strips = arms + shards;
            int perArm = (ArmSteps + 1) * 2, perShard = (ShardSteps + 1) * 2;
            vertices = new Vector3[arms * perArm + shards * perShard];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(arms * ArmSteps + shards * ShardSteps) * 6];
            int v = 0, t = 0;
            for (int s = 0; s < strips; s++)
            {
                int steps = s < arms ? ArmSteps : ShardSteps;
                for (int i = 0; i <= steps; i++)
                {
                    uv[v + i * 2] = new Vector2((float)i / steps, 0f);
                    uv[v + i * 2 + 1] = new Vector2((float)i / steps, 1f);
                    if (i == steps) continue;
                    int a = v + i * 2;
                    triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = a + 2;
                    triangles[t++] = a + 2; triangles[t++] = a + 1; triangles[t++] = a + 3;
                }
                v += (steps + 1) * 2;
            }
            mesh = new Mesh { name = "VFX swirl" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            filter.sharedMesh = mesh;
            block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            age = 0f;
            turned = Random.Range(0f, 360f);
            if (view != null) view.enabled = true;
            Draw();
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= duration)
            {
                view.enabled = false;
                return;
            }
            turned += spin * (1f + 2f * Mathf.Clamp01(age / duration)) * Time.deltaTime;
            Draw();
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        void Draw()
        {
            if (mesh == null) return;
            float t = Mathf.Clamp01(age / duration);
            // Drawn in slowly at first, then fast into the point.
            float scale = Mathf.Lerp(1f, endScale, t * t);
            float r = radius * scale, w = width * Mathf.Lerp(1f, .5f, t);
            int v = 0;
            for (int a = 0; a < arms; a++)
            {
                float from = turned + a * 360f / arms;
                for (int i = 0; i <= ArmSteps; i++)
                {
                    float k = (float)i / ArmSteps;
                    Vector3 p = Arm(from, k, r), q = Arm(from, Mathf.Min(1f, k + .02f), r) - Arm(from, Mathf.Max(0f, k - .02f), r);
                    Vector3 side = new Vector3(-q.z, 0f, q.x).normalized * (w * (1f - .8f * k) * .5f);
                    vertices[v++] = p - side;
                    vertices[v++] = p + side;
                }
            }
            for (int s = 0; s < shards; s++)
            {
                // The crescents ride outside the rim, faster than the arms.
                float centre = turned * 1.4f + s * 360f / shards + 20f;
                for (int i = 0; i <= ShardSteps; i++)
                {
                    float k = (float)i / ShardSteps;
                    float ang = (centre + (k - .5f) * shardArc) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                    float half = w * .45f * Mathf.Sin(Mathf.PI * k);
                    vertices[v++] = dir * (r * 1.15f - half);
                    vertices[v++] = dir * (r * 1.15f + half);
                }
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            float up = fadeIn > 0f ? Mathf.Clamp01(age / fadeIn) : 1f;
            float down = t < .85f ? 1f : (1f - t) / .15f;
            view.GetPropertyBlock(block);
            block.SetFloat(FadeId, up * down);
            block.SetFloat(DissolveId, dissolveEnd * t);
            block.SetFloat(IntensityId, intensity);
            block.SetFloat(UFadeId, .15f);
            view.SetPropertyBlock(block);
        }

        /// <summary>A point on an arm starting at <paramref name="from"/> degrees: k 0 at the rim, 1 at the centre.</summary>
        Vector3 Arm(float from, float k, float r)
        {
            float ang = (from + k * turns * 360f) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * (r * (1f - .92f * k));
        }
    }
}
