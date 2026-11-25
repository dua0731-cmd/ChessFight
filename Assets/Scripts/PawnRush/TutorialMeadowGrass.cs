using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    // R834 (승규 11-25 "성 밖에 있는 잔디 바닥 좀 더 그래픽 좋게"): grass tufts on the tutorial's meadow, drawn GPU-instanced
    // (Graphics.RenderMeshInstanced, 1023 a call) at the places ArtSource/Tutorial wrote (TU_GrassTufts.txt: x, y, z, yaw,
    // scale; dense by the walls, thinning out to 140 m, none on the road). A tuft is three crossed cards of the blade
    // texture (TU_GrassBlades, alpha-clipped, both faces), normals straight up so it shades like the ground it stands on.
    // A picture only: no colliders, nothing sent. Built at Start.
    public sealed class TutorialMeadowGrass : MonoBehaviour
    {
        [SerializeField] TextAsset tufts;
        [SerializeField] Material material;
        [SerializeField] float width = .95f, height = .6f;

        readonly List<Matrix4x4[]> batches = new List<Matrix4x4[]>();
        Mesh mesh;
        RenderParams draw;

        public void Configure(TextAsset places, Material blades)
        {
            tufts = places; material = blades;
        }

        void Start()
        {
            if (tufts == null || material == null) return;
            mesh = Tuft(width, height);
            var all = new List<Matrix4x4>();
            var inv = CultureInfo.InvariantCulture;
            foreach (var raw in tufts.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split(',');
                if (p.Length < 5) continue;
                var at = new Vector3(float.Parse(p[0], inv), float.Parse(p[1], inv), float.Parse(p[2], inv));
                float s = float.Parse(p[4], inv);
                all.Add(Matrix4x4.TRS(at, Quaternion.Euler(0f, float.Parse(p[3], inv), 0f), new Vector3(s, s * Random.Range(.85f, 1.2f), s)));
            }
            for (int i = 0; i < all.Count; i += 1023)
                batches.Add(all.GetRange(i, Mathf.Min(1023, all.Count - i)).ToArray());
            draw = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                worldBounds = new Bounds(Vector3.zero, new Vector3(500f, 120f, 500f)),
            };
            Debug.Log("[튜토리얼 풀] 풀포기 " + all.Count + "개(" + batches.Count + "번에 나눠 그림)");
        }

        void Update()
        {
            if (mesh == null) return;
            foreach (var b in batches) Graphics.RenderMeshInstanced(draw, mesh, 0, b);
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        static Mesh Tuft(float w, float h)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI / 3f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (w * .5f);
                int o = v.Count;
                v.Add(-d); v.Add(d); v.Add(d + Vector3.up * h); v.Add(-d + Vector3.up * h);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                for (int q = 0; q < 4; q++) n.Add(Vector3.up);
                t.AddRange(new[] { o, o + 2, o + 1, o, o + 3, o + 2 });
            }
            var m = new Mesh { name = "Tutorial grass tuft" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetNormals(n); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
