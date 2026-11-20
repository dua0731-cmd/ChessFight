using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    // R725 R7 (the user 11-20, a screenshot of the dome's Big Dipper: "천장에 이런 별자리들 효과좀 넣어"): the dome's own
    // constellations - the Big Dipper over the Dipper, Cassiopeia over Cassiopeia, the pole star at the crown - come alive.
    //   twinkle   each constellation star's gold ball gets a soft four-point flare that breathes, each at its own pace
    //   drawing   every few seconds a bright point runs star to star along the gold wires (the Dipper 1 -> 7 and its bowl
    //             closed back to 4, Cassiopeia 1 -> 5), the wire glowing behind it and each star flaring as it is reached,
    //             then the glow fades - the sky drawing the course's map
    // Looks only: one additive mesh per constellation (a glow ribbon just inside each wire + a flare quad at each star),
    // its vertex colours set each frame; no light, no shadow, nothing sent. The star positions are the hall model's own
    // (ArtSource/PawnRush/RoyalObservatory/ro_r6_hall_model.py: DOME, DIPPER, CASSIOPEIA, course_to_sky, dome_at), in the
    // section's frame (Blender x, y, z = Unity x, z, y).
    public sealed class RoyalObservatoryConstellations : MonoBehaviour
    {
        public static bool Enabled = true;

        [Tooltip("Seconds between two drawings of the same constellation")]
        public float period = 9f;
        [Tooltip("Seconds a drawing takes from the first star to the last")]
        public float drawSeconds = 3.2f;
        [Tooltip("Seconds the drawn line takes to fade after the last star")]
        public float fadeSeconds = 2.2f;
        public Color glowColor = new Color(1f, .82f, .5f);
        [Tooltip("HDR strength of the glow (over the bloom's threshold where it is brightest)")]
        public float strength = 2.4f;

        static readonly Vector3 Middle = new Vector3(0f, 14f, 64f);
        const float R = 50f, Crown = 84f;
        static readonly Vector2[] Dipper =
        {
            new Vector2(-2, 18), new Vector2(-10, 26), new Vector2(-19, 33), new Vector2(-27, 43), new Vector2(-38, 49),
            new Vector2(-37, 62), new Vector2(-25, 60),
        };
        static readonly Vector2[] Cassiopeia =
        {
            new Vector2(19.5f, 66.5f), new Vector2(23.7f, 72.1f), new Vector2(18.3f, 79.3f), new Vector2(23.7f, 86.5f),
            new Vector2(19.76f, 92.29f),
        };
        // The model's wires lie at R - .25 (radius .2), its star balls at R - .3 (radius .75): the glow sits just inside.
        const float RibbonR = R - .75f, RibbonWidth = 1.5f, FlareR = R - 1.3f, FlareSize = 4.2f, PoleFlareSize = 5.5f;

        sealed class Sky
        {
            public Mesh mesh;
            public Color[] colors;
            public float[] along;            // each ribbon pair's distance along the drawing path (m)
            public int ribbonPairs;
            public float[] starAt;           // each star's distance along the path (m); -1: not on the path (the pole)
            public float[] phase, rate;      // each star's twinkle
            public float length, offset;
        }

        readonly List<Sky> skies = new List<Sky>();
        readonly List<Object> made = new List<Object>();

        public static void AddTo(GameObject content)
        {
            if (!Enabled || content.GetComponent<RoyalObservatoryConstellations>() != null) return;
            content.AddComponent<RoyalObservatoryConstellations>();
        }

        void Start()
        {
            var tex = Atlas();
            made.Add(tex);
            var mat = LitMaterials.NewAdditiveParticles(tex, glowColor * strength);
            if (mat == null) { enabled = false; return; }
            mat.SetFloat("_Cull", 0f);
            made.Add(mat);
            var root = new GameObject("Dome constellations (glow)").transform;
            root.SetParent(transform, false);
            // The Dipper drawn 1 -> 7 and its bowl closed back to 4; Cassiopeia 1 -> 5, half a period later; the pole
            // star twinkles only.
            Build(root, mat, "Big Dipper", new List<Vector2>(Dipper) { Dipper[3] }, Dipper, 0f, false);
            Build(root, mat, "Cassiopeia", new List<Vector2>(Cassiopeia), Cassiopeia, period * .5f, false);
            Build(root, mat, "Pole star", null, new Vector2[0], 0f, true);
            Debug.Log("[천문대 별자리] 천장 북두칠성 · 카시오페이아 · 북극성 반짝임과 이어 그리기");
        }

        // A course point (x, z) -> its direction on the dome from the hall's middle: heading as on the course, elevation
        // from the crown (84 deg) at the middle down to 12 deg at 50 m (ro_r6_hall_model.course_to_sky).
        static Vector3 Dir(Vector2 course)
        {
            float dx = course.x - Middle.x, dz = course.y - Middle.z;
            float h = Mathf.Atan2(dx, dz), e = (Crown - (Crown - 12f) * Mathf.Min(1f, Mathf.Sqrt(dx * dx + dz * dz) / R)) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(h) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(h) * Mathf.Cos(e));
        }

        void Build(Transform root, Material mat, string name, List<Vector2> path, Vector2[] stars, float offset, bool pole)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            var sky = new Sky { offset = offset };
            var along = new List<float>();
            var mids = new List<Vector3>();
            // the ribbon: along each wire's great circle, every 2.5 deg as the model's wires, facing the hall's middle
            if (path != null)
            {
                var pts = new List<Vector3>();
                for (int i = 0; i + 1 < path.Count; i++)
                {
                    Vector3 a = Dir(path[i]), b = Dir(path[i + 1]);
                    int n = Mathf.Max(2, Mathf.CeilToInt(Vector3.Angle(a, b) / 2.5f));
                    for (int k = i == 0 ? 0 : 1; k <= n; k++) pts.Add(Middle + Vector3.Slerp(a, b, k / (float)n).normalized * RibbonR);
                }
                float s = 0f;
                for (int i = 0; i < pts.Count; i++)
                {
                    if (i > 0) s += Vector3.Distance(pts[i - 1], pts[i]);
                    var t = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                    var side = Vector3.Cross(t, (Middle - pts[i]).normalized).normalized * (RibbonWidth * .5f);
                    int k = v.Count;
                    v.Add(pts[i] - side); v.Add(pts[i] + side);
                    uv.Add(new Vector2(0f, .5f)); uv.Add(new Vector2(.5f, .5f));   // the atlas's left half: the line's profile
                    along.Add(s);
                    mids.Add(pts[i]);
                    if (i > 0) tri.AddRange(new[] { k - 2, k - 1, k + 1, k - 2, k + 1, k });
                }
                sky.length = s;
                sky.ribbonPairs = pts.Count;
            }
            // the flares: a quad at each star facing the hall's middle (the atlas's right half)
            var centres = new List<Vector3>();
            foreach (var st in stars) centres.Add(Middle + Dir(st) * FlareR);
            if (pole) centres.Add(Middle + Vector3.up * (R - 1.1f));
            var starAt = new List<float>();
            for (int f = 0; f < centres.Count; f++)
            {
                var c = centres[f];
                var inward = (Middle - c).normalized;
                var right = Vector3.Cross(inward, Mathf.Abs(inward.y) > .95f ? Vector3.forward : Vector3.up).normalized;
                var up = Vector3.Cross(right, inward).normalized;
                float half = (pole ? PoleFlareSize : FlareSize) * .5f;
                int k = v.Count;
                v.Add(c - right * half - up * half); v.Add(c + right * half - up * half);
                v.Add(c + right * half + up * half); v.Add(c - right * half + up * half);
                uv.Add(new Vector2(.5f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(.5f, 1f));
                tri.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
                // where the drawing first reaches this star
                float at = -1f;
                if (!pole && mids.Count > 0)
                {
                    float best = float.MaxValue;
                    var p = Middle + Dir(stars[f]) * RibbonR;
                    for (int i = 0; i < mids.Count; i++)
                    {
                        float d = Vector3.Distance(mids[i], p);
                        if (d < best - .01f) { best = d; at = along[i]; }
                    }
                }
                starAt.Add(at);
            }
            sky.along = along.ToArray();
            sky.starAt = starAt.ToArray();
            sky.phase = new float[starAt.Count];
            sky.rate = new float[starAt.Count];
            var rnd = new System.Random(725 + name.Length);
            for (int i = 0; i < starAt.Count; i++)
            {
                sky.phase[i] = (float)rnd.NextDouble() * 6.283f;
                sky.rate[i] = 1.1f + (float)rnd.NextDouble() * 1.2f;
            }
            var mesh = new Mesh { name = name + " glow" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            mesh.MarkDynamic();
            made.Add(mesh);
            sky.mesh = mesh;
            sky.colors = new Color[v.Count];
            var go = new GameObject(name + " (glow)");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            skies.Add(sky);
        }

        void LateUpdate()
        {
            float now = Time.time;
            foreach (var sky in skies)
            {
                // the drawing: t from 0 when it starts; the head runs the path in drawSeconds, then the line fades
                float t = Mathf.Repeat(now + sky.offset, period);
                bool drawing = t < drawSeconds;
                float head = sky.length > 0f ? Mathf.Clamp01(t / drawSeconds) * sky.length : 0f;
                float lit = drawing ? 1f : Mathf.Clamp01(1f - (t - drawSeconds) / fadeSeconds);
                for (int i = 0; i < sky.ribbonPairs; i++)
                {
                    float s = sky.along[i];
                    float a = .06f + .03f * Mathf.Sin(now * 1.3f + s * .12f);                  // the wire's faint resting glow
                    if (s <= head) a += lit * (.25f + .35f * Mathf.Exp(-(head - s) / 9f));     // behind the head: the drawn line
                    if (drawing) { float d = (s - head) / 1.8f; a += Mathf.Exp(-d * d); }      // the running point
                    var c = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                    sky.colors[i * 2] = c; sky.colors[i * 2 + 1] = c;
                }
                int baseV = sky.ribbonPairs * 2;
                float speed = sky.length / Mathf.Max(.01f, drawSeconds);
                for (int f = 0; f < sky.starAt.Length; f++)
                {
                    float a = .38f + .2f * Mathf.Sin(now * sky.rate[f] + sky.phase[f]) + .08f * Mathf.Sin(now * sky.rate[f] * 2.7f + sky.phase[f] * 1.7f);
                    float at = sky.starAt[f];
                    if (at >= 0f && head >= at && t < drawSeconds + fadeSeconds)
                    {
                        // reached: a flare that settles over a second
                        float since = (head - at) / Mathf.Max(.01f, speed) + Mathf.Max(0f, t - drawSeconds);
                        a += .9f * Mathf.Exp(-since / .9f);
                    }
                    var c = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                    for (int k = 0; k < 4; k++) sky.colors[baseV + f * 4 + k] = c;
                }
                sky.mesh.colors = sky.colors;
            }
        }

        // 128 x 64: left half the ribbon's profile across (u), soft-edged; right half a four-point flare with a round core.
        static Texture2D Atlas()
        {
            const int W = 128, H = 64, Half = W / 2;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Constellation glow" };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float a;
                    if (x < Half)
                    {
                        float across = Mathf.Abs((x + .5f) / Half - .5f) * 2f;   // 0 in the middle, 1 at the edges
                        a = Mathf.Exp(-across * across * 7f) * .7f + Mathf.Exp(-across * across * 60f) * .5f;
                        if (x == 0 || x == Half - 1) a = 0f;
                    }
                    else
                    {
                        float px = (x - Half + .5f) / Half * 2f - 1f, py = (y + .5f) / H * 2f - 1f;
                        float r = Mathf.Sqrt(px * px + py * py);
                        float core = Mathf.Exp(-r * r * 30f), halo = Mathf.Exp(-r * r * 6f) * .35f;
                        float rays = Mathf.Exp(-Mathf.Abs(px) * 40f) * Mathf.Exp(-Mathf.Abs(py) * 2.6f)
                                   + Mathf.Exp(-Mathf.Abs(py) * 40f) * Mathf.Exp(-Mathf.Abs(px) * 2.6f);
                        a = (core + halo + rays * .55f) * Mathf.Clamp01((1f - r) * 4f);   // nothing at the quad's edge
                    }
                    a = Mathf.Clamp01(a);
                    t.SetPixel(x, y, new Color(a, a, a, a));
                }
            t.Apply();
            return t;
        }

        void OnDestroy()
        {
            foreach (var o in made)
                if (o != null) Destroy(o);
        }
    }
}
