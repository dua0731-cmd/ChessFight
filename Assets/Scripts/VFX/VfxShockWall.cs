using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// The low wall of light riding a landing piece's shockwave (R341, 11-04, I2 of the grand stair hall's falling knights):
    /// an open ring (VFX_ShockWallAdd) at the hit's reach, the same as <see cref="VfxShockDecal"/>'s front, sinking from
    /// <see cref="heightStart"/> to <see cref="heightEnd"/> m as it spreads, its light sinking over the last
    /// <see cref="fadeOut"/> of the shock, gone when the hit is. The ring's mesh (radius 1, height 1) is made here. The game
    /// copies the obstacle's timing and reach in and seeks it to the time into the obstacle's cycle every frame; on its own
    /// (the workbench) it runs from when it is turned on.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VfxShockWall : MonoBehaviour, IVfxSeekable
    {
        static readonly int LightId = Shader.PropertyToID("_Light");

        public VfxDrop drop = VfxDrop.Default;
        [Min(.1f), Tooltip("The hit's most reach (m), the obstacle's shock radius.")]
        public float radius = 5f;
        [Tooltip("Metres high as it starts, and at its most reach.")]
        public float heightStart = .9f, heightEnd = .35f;
        [Tooltip("HDR multiplier.")]
        public float brightness = 1.5f;
        [Range(.01f, 1f), Tooltip("Share of the shock at its end over which its light sinks to nothing.")]
        public float fadeOut = .35f;
        [Min(.01f), Tooltip("Metres the reach must have before it shows (a ring of nothing is a dot).")]
        public float minReach = .15f;
        [Range(8, 256)] public int segments = 72;

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
            float t = age - drop.lead, shock = Mathf.Max(drop.hold, .01f);
            float f = t / shock, reach = f * radius;
            view.enabled = t >= 0f && t < shock && reach >= minReach;
            if (!view.enabled) return;
            if (filter.sharedMesh == null || builtFor != segments) Build();
            transform.localScale = new Vector3(reach, Mathf.Lerp(heightStart, heightEnd, f), reach);
            view.GetPropertyBlock(block);
            block.SetFloat(LightId, brightness * (1f - Mathf.InverseLerp(1f - fadeOut, 1f, f)));
            view.SetPropertyBlock(block);
        }

        // An open ring of radius 1 from the floor up to 1: uv.x round it, uv.y up, normals outward.
        void Build()
        {
            var points = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments, angle = u * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                for (int y = 0; y < 2; y++)
                {
                    points.Add(outward + Vector3.up * y);
                    normals.Add(outward);
                    uvs.Add(new Vector2(u, y));
                }
                if (i == segments) continue;
                int at = i * 2;
                triangles.AddRange(new[] { at, at + 1, at + 2, at + 1, at + 3, at + 2 });
            }
            var mesh = new Mesh { name = "Shock wall", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(points);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
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
