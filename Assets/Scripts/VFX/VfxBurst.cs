using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// Spikes of light thrown out over the ground from a point once each time it is turned on (R163, 10-27, the burst of an
    /// impact): <see cref="spikes"/> thin triangles round the centre at uneven angles and lengths, a little off the ground,
    /// out to full length in <see cref="grow"/> and gone by <see cref="duration"/>. A new spread every time. Drawn with
    /// <see cref="material"/> (VFX_ScrollDissolveAdd: u runs from the centre to the tip).
    /// </summary>
    public sealed class VfxBurst : MonoBehaviour
    {
        static readonly int FadeId = Shader.PropertyToID("_Fade"), DissolveId = Shader.PropertyToID("_Dissolve"),
            IntensityId = Shader.PropertyToID("_Intensity"), UFadeId = Shader.PropertyToID("_UFade");

        public Material material;
        [Min(.05f)] public float duration = .32f;
        [Min(3)] public int spikes = 20;
        [Tooltip("Shortest and longest spike (m).")]
        public Vector2 length = new Vector2(.9f, 2.1f);
        [Tooltip("Spike width at the base (m).")]
        public float width = .14f;
        [Tooltip("Seconds to reach full length.")]
        public float grow = .07f;
        [Tooltip("How far the tips rise off the ground, as a share of their length.")]
        public float rise = .12f;
        [Tooltip("HDR multiplier on the material's colour (see Docs/VFX §6).")]
        public float intensity = 3f;

        Mesh mesh;
        Vector3[] vertices, tips;
        float[] angles;
        MeshRenderer view;
        MaterialPropertyBlock block;
        float age;

        void Awake()
        {
            var filter = gameObject.AddComponent<MeshFilter>();
            view = gameObject.AddComponent<MeshRenderer>();
            view.sharedMaterial = material;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            vertices = new Vector3[spikes * 3];
            tips = new Vector3[spikes];
            angles = new float[spikes];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[spikes * 3];
            for (int i = 0; i < spikes; i++)
            {
                uv[i * 3] = new Vector2(0f, 0f);
                uv[i * 3 + 1] = new Vector2(0f, 1f);
                uv[i * 3 + 2] = new Vector2(1f, .5f);
                triangles[i * 3] = i * 3; triangles[i * 3 + 1] = i * 3 + 1; triangles[i * 3 + 2] = i * 3 + 2;
            }
            mesh = new Mesh { name = "VFX burst" };
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
            if (tips == null) return;
            for (int i = 0; i < spikes; i++)
            {
                angles[i] = (i + Random.Range(-.35f, .35f)) * 360f / spikes * Mathf.Deg2Rad;
                float l = Random.Range(length.x, length.y);
                tips[i] = new Vector3(Mathf.Sin(angles[i]) * l, l * rise * Random.Range(.3f, 1f), Mathf.Cos(angles[i]) * l);
            }
            view.enabled = true;
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
            float reach = grow > 0f ? 1f - Mathf.Pow(1f - Mathf.Clamp01(age / grow), 3f) : 1f;
            for (int i = 0; i < spikes; i++)
            {
                var side = new Vector3(Mathf.Cos(angles[i]), 0f, -Mathf.Sin(angles[i])) * (width * .5f * (1f - .6f * t));
                Vector3 baseAt = tips[i].normalized * .05f;
                vertices[i * 3] = baseAt - side;
                vertices[i * 3 + 1] = baseAt + side;
                vertices[i * 3 + 2] = tips[i] * reach;
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            float down = age < grow ? 1f : 1f - Mathf.Clamp01((age - grow) / Mathf.Max(.01f, duration - grow));
            view.GetPropertyBlock(block);
            block.SetFloat(FadeId, down * down);
            block.SetFloat(DissolveId, .5f * t);
            block.SetFloat(IntensityId, intensity);
            block.SetFloat(UFadeId, 0f);
            view.SetPropertyBlock(block);
        }
    }
}
