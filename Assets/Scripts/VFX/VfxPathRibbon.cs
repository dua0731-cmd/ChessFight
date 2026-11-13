using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// A ribbon of light drawn along a path over time (R182, 10-28, E3 of the knight's reversal: the long crescent his
    /// hind hooves draw over the somersault; reference E_03_flip.png). Each point of the path has the second after it
    /// was turned on that it is drawn at; the ribbon reaches as far as the latest of them, brightest at that head, and
    /// each part of it narrows to nothing and dies away over <see cref="life"/> seconds from when it was drawn, so it is a
    /// crescent that dies away from its tail and never closes into a ring. The path is in this object's space: the
    /// builder saves a typical one in the prefab (the workbench and the captures draw that); the game clears it with
    /// <see cref="Begin"/> and lays down where the hooves really go with <see cref="Trace"/>, a point a frame, so it is
    /// where they went on every screen. A smooth curve runs through the points (<see cref="smooth"/> more put in between
    /// two). One mesh on the same object, made here and not saved, drawn with VFX_PathRibbonAdd, whose clock, width,
    /// life and intensity it sets on its renderer; every value is here to tune in the inspector, the colours and the
    /// look of the light on the material.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VfxPathRibbon : MonoBehaviour, IVfxSeekable
    {
        static readonly int NowId = Shader.PropertyToID("_Now"), WidthId = Shader.PropertyToID("_Width"),
            TaperId = Shader.PropertyToID("_Taper"), LifeId = Shader.PropertyToID("_Life"),
            IntensityId = Shader.PropertyToID("_Intensity");

        [Header("Path (the game lays its own down)")]
        [Tooltip("Points along the way in this object's space, in the order they are drawn.")]
        public List<Vector3> points = new List<Vector3>();
        [Tooltip("Seconds after it is turned on that each point is drawn at (one a point, rising).")]
        public List<float> times = new List<float>();
        [Range(1, 8), Tooltip("Points put in on a smooth curve between two of the path's.")]
        public int smooth = 4;
        [Min(0f), Tooltip("Metres a traced point must be from the last one to be kept.")]
        public float minStep = .015f;

        [Header("Shape")]
        [Min(.005f), Tooltip("Metres across where it was drawn last.")]
        public float width = .14f;
        [Range(.1f, 4f), Tooltip("How it narrows toward the tail as it ages: above 1 sooner, below 1 later.")]
        public float taper = .8f;

        [Header("Timing")]
        [Min(.05f), Tooltip("Seconds each part of it stays once drawn, narrowing and dying away.")]
        public float life = .55f;

        [Header("Light")]
        [Tooltip("HDR multiplier (Docs/VFX/README.md §6; the core is the material's Core Brightness times this).")]
        public float intensity = 1.5f;

        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> ways = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> triangles = new List<int>();
        MeshFilter filter;
        MeshRenderer view;
        MaterialPropertyBlock block;
        Mesh mesh;
        bool dirty = true;
        float age;

        void OnEnable()
        {
            age = 0f;
            dirty = true;
            Draw();
        }

        void Update()
        {
            age += Time.deltaTime;
            Draw();
        }

        // A change in the inspector (the curve's points in between) is drawn from the next frame.
        void OnValidate() => dirty = true;

        void OnDestroy()
        {
            // Its mesh is its own (made here, not saved).
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }

        /// <summary>As it is <paramref name="at"/> seconds after it was turned on (the capture tool; edit mode too).</summary>
        public void Seek(float at)
        {
            age = at;
            Draw();
        }

        /// <summary>Clears the path (the prefab's typical one) for the game to lay its own down with <see cref="Trace"/>.</summary>
        public void Begin()
        {
            points.Clear();
            times.Clear();
            dirty = true;
            Draw();
        }

        /// <summary>Adds <paramref name="world"/> to the path, drawn now (skipped closer than <see cref="minStep"/> to the
        /// last point).</summary>
        public void Trace(Vector3 world)
        {
            Vector3 at = transform.InverseTransformPoint(world);
            int n = Mathf.Min(points.Count, times.Count);
            if (n > 0 && ((at - points[n - 1]).sqrMagnitude < minStep * minStep || age <= times[n - 1])) return;
            if (points.Count != n) points.RemoveRange(n, points.Count - n);
            if (times.Count != n) times.RemoveRange(n, times.Count - n);
            points.Add(at);
            times.Add(age);
            dirty = true;
            Draw();
        }

        bool Ready()
        {
            // Looked up here, not in Awake: a script reload in Play mode clears all of these (Docs/Environment/PITFALLS.md 53).
            if (filter == null) filter = GetComponent<MeshFilter>();
            if (view == null) view = GetComponent<MeshRenderer>();
            if (block == null) block = new MaterialPropertyBlock();
            if (mesh == null)
            {
                mesh = new Mesh { name = "Path ribbon", hideFlags = HideFlags.HideAndDontSave };
                mesh.MarkDynamic();
                dirty = true;
            }
            return filter != null && view != null;
        }

        void Draw()
        {
            if (!Ready()) return;
            if (dirty) Build();
            int n = Mathf.Min(points.Count, times.Count);
            view.enabled = n > 1 && age >= times[0] && age < times[n - 1] + life;
            if (!view.enabled) return;
            view.GetPropertyBlock(block);
            block.SetFloat(NowId, age);
            block.SetFloat(WidthId, width);
            block.SetFloat(TaperId, taper);
            block.SetFloat(LifeId, life);
            block.SetFloat(IntensityId, intensity);
            view.SetPropertyBlock(block);
        }

        /// <summary>The mesh: a smooth curve through the points, two vertices across each spot on it (the side in uv.y,
        /// spread by the shader), the second it is drawn at in uv.x and the way along it in the normal.</summary>
        void Build()
        {
            dirty = false;
            filter.sharedMesh = mesh;
            vertices.Clear();
            ways.Clear();
            uvs.Clear();
            triangles.Clear();
            int n = Mathf.Min(points.Count, times.Count), s = Mathf.Max(1, smooth);
            if (n > 1)
            {
                Vector3 lastWay = points[1] - points[0];
                for (int i = 0; i < n - 1; i++)
                {
                    Vector3 p0 = points[Mathf.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(i + 2, n - 1)];
                    // The last stretch ends on its last point.
                    int steps = i == n - 2 ? s + 1 : s;
                    for (int j = 0; j < steps; j++)
                    {
                        float u = j / (float)s;
                        Vector3 way = CurveWay(p0, p1, p2, p3, u);
                        if (way.sqrMagnitude < 1e-10f) way = lastWay;
                        lastWay = way;
                        Vector3 at = Curve(p0, p1, p2, p3, u);
                        float time = Mathf.Lerp(times[i], times[i + 1], u);
                        vertices.Add(at);
                        vertices.Add(at);
                        ways.Add(way.normalized);
                        ways.Add(way.normalized);
                        uvs.Add(new Vector2(time, -1f));
                        uvs.Add(new Vector2(time, 1f));
                    }
                }
                for (int k = 0; k + 3 < vertices.Count; k += 2)
                    triangles.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(ways);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, false);
            // The shader spreads it up to half its width each way off the curve.
            if (vertices.Count > 0)
            {
                var bounds = new Bounds(vertices[0], Vector3.zero);
                foreach (var v in vertices) bounds.Encapsulate(v);
                bounds.Expand(width * 2f);
                mesh.bounds = bounds;
            }
        }

        /// <summary>A point on the Catmull-Rom curve through p1 to p2 (p0 and p3 their neighbours).</summary>
        static Vector3 Curve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float u2 = u * u, u3 = u2 * u;
            return .5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (3f * p1 - p0 - 3f * p2 + p3) * u3);
        }

        /// <summary>The way along that curve there.</summary>
        static Vector3 CurveWay(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            return .5f * ((p2 - p0) + 2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * u + 3f * (3f * p1 - p0 - 3f * p2 + p3) * u * u);
        }
    }
}
