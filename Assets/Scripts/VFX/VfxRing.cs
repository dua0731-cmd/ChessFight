using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// A ring that grows and fades once each time it is turned on (R159, 10-27): flat on the ground (Floor: a band
    /// <see cref="width"/> wide in the XZ plane, a shock or a stance mark), standing up (Band: a wall <see cref="width"/>
    /// high, an arc of air in front of a piece) or a straight strip on the ground (Strip: along X, its length growing from
    /// startRadius to endRadius, a crack's line of light). An arc under 360 degrees is centred on +Z. It builds its own
    /// mesh and draws with <see cref="material"/> (VFX_ScrollDissolveAdd), whose fade, dissolve and intensity it sets on
    /// its renderer as it goes.
    /// </summary>
    public sealed class VfxRing : MonoBehaviour
    {
        public enum Shape { Floor, Band, Strip }

        const int Segments = 64;
        static readonly int FadeId = Shader.PropertyToID("_Fade"), DissolveId = Shader.PropertyToID("_Dissolve"),
            IntensityId = Shader.PropertyToID("_Intensity"), UFadeId = Shader.PropertyToID("_UFade");

        public Shape shape = Shape.Floor;
        public Material material;
        [Min(.02f)] public float duration = .5f;
        public float startRadius = .3f, endRadius = 1.5f;
        [Tooltip("Floor and Strip: how wide the band is. Band: how high the wall is.")]
        public float width = .08f;
        [Range(10f, 360f)] public float arc = 360f;
        [Tooltip("Seconds to come up to full strength.")]
        public float fadeIn = .05f;
        [Tooltip("HDR multiplier on the material's colour (see Docs/VFX §6).")]
        public float intensity = 1.5f;
        [Range(0f, 1f), Tooltip("How far the noise has eaten it away by the end.")]
        public float dissolveEnd = .6f;

        Mesh mesh;
        Vector3[] vertices;
        MeshRenderer view;
        MaterialPropertyBlock block;
        float age;

        void Awake()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            view = GetComponent<MeshRenderer>();
            if (view == null) view = gameObject.AddComponent<MeshRenderer>();
            view.sharedMaterial = material;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            mesh = new Mesh { name = "VFX ring" };
            mesh.MarkDynamic();
            vertices = new Vector3[(Segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[Segments * 6];
            for (int i = 0; i <= Segments; i++)
            {
                uv[i * 2] = new Vector2((float)i / Segments, 0f);
                uv[i * 2 + 1] = new Vector2((float)i / Segments, 1f);
                if (i == Segments) continue;
                int t = i * 6, a = i * 2;
                triangles[t] = a; triangles[t + 1] = a + 1; triangles[t + 2] = a + 2;
                triangles[t + 3] = a + 2; triangles[t + 4] = a + 1; triangles[t + 5] = a + 3;
            }
            Build(startRadius);
            mesh.uv = uv;
            mesh.triangles = triangles;
            filter.sharedMesh = mesh;
            block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            age = 0f;
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
            float grow = 1f - (1f - t) * (1f - t) * (1f - t);
            Build(Mathf.Lerp(startRadius, endRadius, grow));
            float up = fadeIn > 0f ? Mathf.Clamp01(age / fadeIn) : 1f;
            view.GetPropertyBlock(block);
            block.SetFloat(FadeId, up * (1f - t * t));
            block.SetFloat(DissolveId, dissolveEnd * t);
            block.SetFloat(IntensityId, intensity);
            block.SetFloat(UFadeId, shape == Shape.Strip || arc < 359.5f ? .15f : 0f);
            view.SetPropertyBlock(block);
        }

        void Build(float radius)
        {
            float half = arc * .5f * Mathf.Deg2Rad;
            for (int i = 0; i <= Segments; i++)
            {
                if (shape == Shape.Strip)
                {
                    float x = (i / (float)Segments - .5f) * radius;
                    vertices[i * 2] = new Vector3(x, 0f, -width * .5f);
                    vertices[i * 2 + 1] = new Vector3(x, 0f, width * .5f);
                    continue;
                }
                float a = Mathf.Lerp(-half, half, (float)i / Segments);
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                if (shape == Shape.Floor)
                {
                    vertices[i * 2] = dir * Mathf.Max(0f, radius - width * .5f);
                    vertices[i * 2 + 1] = dir * (radius + width * .5f);
                }
                else
                {
                    vertices[i * 2] = dir * radius;
                    vertices[i * 2 + 1] = dir * radius + Vector3.up * width;
                }
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }
    }
}
