using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// A disc of light on the ground that cracks and breaks apart once each time it is turned on (R163, 10-27, the
    /// aftermath of an impact): it comes up to full size in <see cref="grow"/>, glows whole with thin cracks for
    /// <see cref="whole"/>, then its pieces (rings of uneven wedges, a new pattern every time) open apart, slide out, lift a
    /// little, turn and fade, eaten away by the noise, by <see cref="duration"/>. Each piece is brightest along its edges.
    /// Flat in the XZ plane, drawn with <see cref="material"/> (VFX_ScrollDissolveAdd; its vertex colour carries the edges).
    /// </summary>
    public sealed class VfxShatter : MonoBehaviour
    {
        const int ArcPoints = 3;
        static readonly int FadeId = Shader.PropertyToID("_Fade"), DissolveId = Shader.PropertyToID("_Dissolve"),
            IntensityId = Shader.PropertyToID("_Intensity"), UFadeId = Shader.PropertyToID("_UFade");

        public Material material;
        [Tooltip("Radius of the disc (m).")]
        public float radius = 2f;
        [Min(3)] public int sectors = 10;
        [Min(1)] public int rings = 3;
        [Tooltip("Seconds to come up to full size.")]
        public float grow = .08f;
        [Tooltip("Seconds it glows whole before it breaks.")]
        public float whole = .16f;
        [Min(.2f)] public float duration = 1.7f;
        [Range(0f, .3f), Tooltip("How much each piece is drawn in from its neighbours while whole, and once broken.")]
        public float gap = .04f, gapOpen = .14f;
        [Tooltip("How far the pieces slide out (m) and rise (m), and turn (degrees).")]
        public float drift = .45f, lift = .22f, turn = 45f;
        [Tooltip("HDR multipliers on the material's colour: whole, then the pieces (see Docs/VFX §6).")]
        public float intensity = 2.6f, pieceIntensity = 1.2f;

        struct Piece
        {
            public Vector3 centre, outward, axis;
            public int first, count;
            public float drift, lift, turn;
        }

        Mesh mesh;
        Vector3[] vertices, offsets;
        Piece[] pieces;
        MeshRenderer view;
        MaterialPropertyBlock block;
        float age;

        void Awake()
        {
            gameObject.AddComponent<MeshFilter>();
            view = gameObject.AddComponent<MeshRenderer>();
            view.sharedMaterial = material;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            mesh = new Mesh { name = "VFX shatter" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            age = 0f;
            if (mesh == null) return;
            Cut();
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

        /// <summary>A new crack pattern: ring boundaries a little uneven, each ring cut into wedges at uneven angles.</summary>
        void Cut()
        {
            var bounds = new float[rings + 1];
            for (int k = 1; k < rings; k++) bounds[k] = radius * Mathf.Pow((float)k / rings, .75f) * Random.Range(.92f, 1.06f);
            bounds[rings] = radius;
            int total = 0;
            var counts = new int[rings];
            for (int k = 0; k < rings; k++)
            {
                counts[k] = k == 0 ? Mathf.Max(3, sectors / 2) : sectors;
                total += counts[k];
            }
            pieces = new Piece[total];
            // Each piece: its centre and its outline (the outer arc, then the inner arc back, or the centre point).
            var points = new System.Collections.Generic.List<Vector3>();
            var colours = new System.Collections.Generic.List<Color>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();
            var outline = new System.Collections.Generic.List<Vector3>();
            int p = 0;
            for (int k = 0; k < rings; k++)
            {
                float start = Random.Range(0f, 360f), step = 360f / counts[k];
                var cuts = new float[counts[k] + 1];
                for (int j = 0; j < counts[k]; j++) cuts[j] = start + (j + Random.Range(-.3f, .3f)) * step;
                cuts[counts[k]] = cuts[0] + 360f;
                for (int j = 0; j < counts[k]; j++)
                {
                    outline.Clear();
                    for (int i = 0; i < ArcPoints; i++) outline.Add(Round(Mathf.Lerp(cuts[j], cuts[j + 1], i / (ArcPoints - 1f)), bounds[k + 1]));
                    if (k == 0) outline.Add(Vector3.zero);
                    else
                        for (int i = ArcPoints - 1; i >= 0; i--) outline.Add(Round(Mathf.Lerp(cuts[j], cuts[j + 1], i / (ArcPoints - 1f)), bounds[k]));
                    var centre = Vector3.zero;
                    foreach (var o in outline) centre += o;
                    centre /= outline.Count;
                    int first = points.Count;
                    points.Add(centre);
                    colours.Add(new Color(1f, 1f, 1f, .4f));
                    foreach (var o in outline)
                    {
                        points.Add(o);
                        colours.Add(Color.white);
                    }
                    for (int i = 0; i < outline.Count; i++)
                    {
                        triangles.Add(first);
                        triangles.Add(first + 1 + i);
                        triangles.Add(first + 1 + (i + 1) % outline.Count);
                    }
                    var flat = new Vector3(centre.x, 0f, centre.z);
                    pieces[p++] = new Piece
                    {
                        centre = centre,
                        outward = flat.sqrMagnitude > 1e-4f ? flat.normalized : Random.insideUnitSphere.normalized,
                        axis = Random.onUnitSphere,
                        first = first,
                        count = outline.Count + 1,
                        drift = Random.Range(.5f, 1.2f) * (.4f + flat.magnitude / radius),
                        lift = Random.Range(.4f, 1.2f),
                        turn = Random.Range(-1f, 1f),
                    };
                }
            }
            for (int i = 0; i < points.Count; i++)
                uv.Add(new Vector2(Mathf.Repeat(Mathf.Atan2(points[i].x, points[i].z) / (2f * Mathf.PI), 1f), .5f));
            vertices = points.ToArray();
            offsets = new Vector3[vertices.Length];
            foreach (var piece in pieces)
                for (int i = 0; i < piece.count; i++) offsets[piece.first + i] = vertices[piece.first + i] - piece.centre;
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colours.ToArray();
            mesh.uv = uv.ToArray();
            mesh.triangles = triangles.ToArray();
        }

        static Vector3 Round(float degrees, float r)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
        }

        void Draw()
        {
            if (pieces == null) return;
            float size = grow > 0f ? Mathf.Lerp(.55f, 1f, 1f - Mathf.Pow(1f - Mathf.Clamp01(age / grow), 3f)) : 1f;
            float broken = Mathf.Clamp01((age - whole) / Mathf.Max(.05f, duration - whole));
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(broken * 1.6f), 3f);
            float open = 1f - Mathf.Lerp(gap, gapOpen, e);
            foreach (var piece in pieces)
            {
                var spin = Quaternion.AngleAxis(turn * piece.turn * e, piece.axis);
                Vector3 at = piece.centre * size + piece.outward * (drift * piece.drift * e)
                    + Vector3.up * (lift * piece.lift * Mathf.Sin(Mathf.PI * Mathf.Min(1f, e)) * .8f + .002f);
                for (int i = 0; i < piece.count; i++)
                    vertices[piece.first + i] = at + spin * (offsets[piece.first + i] * size * open);
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            // Bright while whole, settling to the pieces' glow as it breaks, then dying away over the last 40%.
            float glow = age < whole ? intensity : Mathf.Lerp(intensity, pieceIntensity, Mathf.Clamp01((age - whole) / .2f));
            float t = Mathf.Clamp01(age / duration);
            float fade = t < .6f ? 1f : 1f - (t - .6f) / .4f;
            view.GetPropertyBlock(block);
            block.SetFloat(FadeId, fade * fade);
            block.SetFloat(DissolveId, .55f * broken);
            block.SetFloat(IntensityId, glow);
            block.SetFloat(UFadeId, 0f);
            view.SetPropertyBlock(block);
        }
    }
}
