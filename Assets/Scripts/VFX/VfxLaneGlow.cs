using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// A charge's lane warned in standing light, not painted on the floor (R354, 11-04, the grand stair hall's guard
    /// knights: 승규 "데칼 말고 다른 기술로 해줘"): VFX_WarnLight's two low curtains of light along the lane's sides and
    /// <see cref="count"/> arrowheads floating over it, running down it toward where the charge stops. Its +z runs from
    /// where the knight lands to there, <see cref="length"/> by <see cref="width"/> m. It shows <see cref="showAt"/> seconds
    /// after the warning starts (승규 "이제 순서를 둬": the landing ring first, the lane from mid-leap); the curtains stand
    /// taller and the arrowheads brighter where the fill has reached, the fill reaching the far end at <see cref="lead"/>
    /// seconds (as the charge starts), blinking the last <see cref="urgent"/>; through the
    /// <see cref="charge"/> everything behind the knight goes out (eased as its move), and it is gone as the charge ends.
    /// The game sets the lane and the timing from the obstacle and seeks it to the time since its warning started every
    /// frame; on its own (the workbench) it runs from when it is turned on.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VfxLaneGlow : MonoBehaviour, IVfxSeekable
    {
        static readonly int FillId = Shader.PropertyToID("_Fill"), PassedId = Shader.PropertyToID("_Passed"),
            ScrollId = Shader.PropertyToID("_Scroll"), UrgencyId = Shader.PropertyToID("_Urgency"), FadeId = Shader.PropertyToID("_Fade"),
            AgeId = Shader.PropertyToID("_Age"), LengthId = Shader.PropertyToID("_Length"), WidthId = Shader.PropertyToID("_Width"),
            HeightId = Shader.PropertyToID("_Height"), HoverId = Shader.PropertyToID("_Hover"), SizeId = Shader.PropertyToID("_ChevronSize");

        [Min(.5f), Tooltip("Metres from where the knight lands to where it stops.")]
        public float length = 8f;
        [Min(.1f), Tooltip("Metres across: the knight's body.")]
        public float width = 1.4f;
        [Min(.1f), Tooltip("Seconds from the warning to the charge.")]
        public float lead = 3.02f;
        [Min(0f), Tooltip("Seconds after the warning starts it shows (the game: mid-leap, after the landing ring).")]
        public float showAt = 1.8f;
        [Min(.05f), Tooltip("Seconds the charge takes.")]
        public float charge = 1.1f;
        [Min(.01f), Tooltip("Seconds before the charge it starts to blink.")]
        public float urgent = .6f;
        [Min(1), Tooltip("Arrowheads over the lane.")]
        public int count = 6;
        [Tooltip("Metres a second the arrowheads run down the lane.")]
        public float speed = 3.5f;
        [Min(.1f), Tooltip("Metres the side curtains stand.")]
        public float height = .9f;
        [Min(0f), Tooltip("Metres over the floor the arrowheads float.")]
        public float hover = .35f;
        [Min(.05f), Tooltip("Metres across an arrowhead.")]
        public float chevronSize = .6f;
        [Min(.01f)] public float fadeIn = .15f;

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
            float from = Mathf.Min(showAt, lead - .1f), shown = age - from;
            view.enabled = shown >= 0f && age < lead + charge;
            if (!view.enabled) return;
            if (filter.sharedMesh == null || builtFor != count) Build();
            filter.sharedMesh.bounds = new Bounds(new Vector3(0f, height * .5f, length * .5f), new Vector3(width + 2f, height + 1f, length + 2f));
            view.GetPropertyBlock(block);
            block.SetFloat(FillId, Mathf.Clamp01(shown / (lead - from)));
            block.SetFloat(PassedId, age < lead ? 0f : Mathf.SmoothStep(0f, 1f, (age - lead) / charge));
            block.SetFloat(ScrollId, shown * speed / Mathf.Max(length, .1f));
            block.SetFloat(UrgencyId, age < lead ? Mathf.Clamp01((age - lead + urgent) / .05f) : 0f);
            block.SetFloat(FadeId, Mathf.Clamp01(shown / fadeIn));
            block.SetFloat(AgeId, age);
            block.SetFloat(LengthId, length);
            block.SetFloat(WidthId, width);
            block.SetFloat(HeightId, height);
            block.SetFloat(HoverId, hover);
            block.SetFloat(SizeId, chevronSize);
            view.SetPropertyBlock(block);
        }

        // Two side curtains (columns along the lane, the shader places them) and the arrowheads (their shape, tip at the
        // origin pointing +z, a unit wide, tilted up at the back; the shader carries them down the lane).
        void Build()
        {
            var points = new List<Vector3>();
            var uvs = new List<Vector2>();
            var shapes = new List<Vector4>();
            var triangles = new List<int>();
            const int columns = 24;
            for (int side = -1; side <= 1; side += 2)
            {
                int at = points.Count;
                for (int c = 0; c <= columns; c++)
                    for (int r = 0; r < 2; r++)
                    {
                        points.Add(Vector3.zero);
                        uvs.Add(new Vector2(c / (float)columns, r));
                        shapes.Add(new Vector4(0f, side, 0f, 0f));
                    }
                for (int c = 0; c < columns; c++)
                {
                    int a = at + c * 2;
                    triangles.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
                }
            }
            for (int i = 0; i < count; i++)
            {
                var shape = new Vector4(1f, i / (float)count, 0f, 0f);
                foreach (var tail in new[] { new Vector3(-.5f, .18f, -.45f), new Vector3(.5f, .18f, -.45f) })
                {
                    var arm = (Vector3.zero - tail).normalized;
                    var across = Vector3.Cross(Vector3.up, arm).normalized * .07f;
                    int at = points.Count;
                    foreach (var p in new[] { tail - across, tail + across, -across, across })
                    {
                        points.Add(p);
                        uvs.Add(Vector2.zero);
                        shapes.Add(shape);
                    }
                    triangles.AddRange(new[] { at, at + 1, at + 2, at + 1, at + 3, at + 2 });
                }
            }
            var mesh = new Mesh { name = "Lane glow", hideFlags = HideFlags.DontSave };
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
            builtFor = count;
        }
    }
}
