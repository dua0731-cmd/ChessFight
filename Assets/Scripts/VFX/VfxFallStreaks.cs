using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// Speed lines left above a falling piece (R341, 11-04, W2 of the grand stair hall's falling knights): <see cref="count"/>
    /// thin upright bands of light (VFX_FallStreakAdd) round its body, their feet between <see cref="footLow"/> and
    /// <see cref="footHigh"/> m over its base and <see cref="ringInner"/> .. <see cref="ringOuter"/> m out from its upright,
    /// riding down with it. They grow with its speed, to <see cref="length"/> m as it lands, and are gone
    /// <see cref="outTime"/> after. Nothing while it hangs. The mesh is made here from <see cref="seed"/> (the same streaks
    /// every time; a change to the count or the spread shows the next time it is turned on). The game copies the
    /// obstacle's timing into <see cref="drop"/> and seeks it to the time into the obstacle's cycle every frame; on its own
    /// (the workbench) it runs from when it is turned on.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VfxFallStreaks : MonoBehaviour, IVfxSeekable
    {
        static readonly int LengthId = Shader.PropertyToID("_Length"), LightId = Shader.PropertyToID("_Light");

        public VfxDrop drop = VfxDrop.Default;
        [Min(1)] public int count = 10;
        [Tooltip("Metres out from the piece's upright the streaks stand, nearest and furthest.")]
        public float ringInner = .45f, ringOuter = .95f;
        [Tooltip("Metres over the piece's base their feet are, lowest and highest.")]
        public float footLow = .8f, footHigh = 2.3f;
        [Min(.1f), Tooltip("Metres long at full speed, as it lands.")]
        public float length = 3.2f;
        [Tooltip("HDR multiplier.")]
        public float brightness = 2f;
        [Min(.01f), Tooltip("Share of the fall over which they come in.")]
        public float fadeIn = .15f;
        [Min(.01f), Tooltip("Seconds after the landing they take to go.")]
        public float outTime = .05f;
        public int seed = 7;

        MeshRenderer view;
        MeshFilter filter;
        MaterialPropertyBlock block;
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
            Build();
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
            if (filter.sharedMesh == null) Build();
            age = at;
            Draw();
        }

        void Draw()
        {
            Ready();
            float d = drop.Progress(age);
            view.enabled = d > 0f && age < drop.lead + outTime;
            if (!view.enabled) return;
            transform.localPosition = new Vector3(transform.localPosition.x, drop.Height(Mathf.Min(age, drop.lead)), transform.localPosition.z);
            float fade = age < drop.lead ? Mathf.Clamp01(d / fadeIn) : 1f - Mathf.Clamp01((age - drop.lead) / outTime);
            view.GetPropertyBlock(block);
            // Its speed grows with the fall's progress (the height goes as its square).
            block.SetFloat(LengthId, length * d);
            block.SetFloat(LightId, brightness * fade);
            view.SetPropertyBlock(block);
        }

        // A quad a streak, its four corners at the streak's foot (the shader stands it up and turns it to the camera).
        void Build()
        {
            Ready();
            var rng = new System.Random(seed);
            float Next() => (float)rng.NextDouble();
            var points = new List<Vector3>();
            var uvs = new List<Vector2>();
            var shares = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Next() * .7f) / count * Mathf.PI * 2f, radius = Mathf.Lerp(ringInner, ringOuter, Next());
                var foot = new Vector3(Mathf.Sin(angle) * radius, Mathf.Lerp(footLow, footHigh, Next()), Mathf.Cos(angle) * radius);
                var share = new Vector2(Mathf.Lerp(.55f, 1f, Next()), Mathf.Lerp(.5f, 1f, Next()));
                int at = points.Count;
                for (int c = 0; c < 4; c++)
                {
                    points.Add(foot);
                    uvs.Add(new Vector2(c & 1, c >> 1));
                    shares.Add(share);
                }
                triangles.AddRange(new[] { at, at + 2, at + 1, at + 2, at + 3, at + 1 });
            }
            var mesh = new Mesh { name = "Fall streaks", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(points);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, shares);
            mesh.SetTriangles(triangles, 0);
            // Room for the streaks as the shader stands them up.
            float top = footHigh + length + .5f;
            mesh.bounds = new Bounds(new Vector3(0f, top * .5f, 0f), new Vector3(2f * ringOuter + .5f, top, 2f * ringOuter + .5f));
            if (filter.sharedMesh != null && filter.sharedMesh.hideFlags == HideFlags.DontSave)
            {
                if (Application.isPlaying) Destroy(filter.sharedMesh);
                else DestroyImmediate(filter.sharedMesh);
            }
            filter.sharedMesh = mesh;
        }
    }
}
