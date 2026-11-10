using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R532 (승규 11-09, the spin bar plaza: "벽에 물줄기가 사진 같이 되 있어 실제로 물줄기가 나와야 돼"): the six fountains
    // that pour from the kerb's planters into the moat, drawn as running water instead of the concept model's chain of
    // glass tubes (plaza_sample_v4.py "Plaza fountain jets"; the builder takes the model's water and foam out and keeps its
    // gold nozzles).
    //
    // Each jet is a projectile: water leaves the nozzle at (u out, w up) and falls freely, x = u a, y = w a - g a^2 / 2,
    // the arc the model drew (2.2 m out from the nozzle's mouth, 1 m up at the top of a unit curve, landing on the moat).
    // A steady jet stands on that arc; what moves is the water along it:
    //   - streaks and air ride down the stream at its speed (the falls' shader, UV0.y = metres along, _Speed);
    //   - continuity thins it as it speeds up (r ~ 1 / sqrt(speed)) while it takes in air and frays a little;
    //   - the nozzle's pressure surges, so swellings travel down the stream and its tip sways;
    //   - near the water it breaks into drops, and where it lands the moat foams, rings spread and drops jump back up.
    // Nothing here touches the game; everything is drawn from the local clock (the jets never change).
    public sealed class GardenSpout : MonoBehaviour
    {
        const float G = 9.81f;
        const int Around = 12, Along = 40;
        const float R0 = .045f, Spread = .55f;

        [SerializeField] Vector3[] nozzles = new Vector3[0];   // the mouth of each nozzle, in this object's frame
        [SerializeField] Vector3[] outs = new Vector3[0];      // each jet's horizontal direction
        [SerializeField] float waterY = -1.9f;
        [SerializeField] Material water;
        [Tooltip("The concept model's old jets (glass tubes and still foam), taken out on waking; its gold nozzles stay.")]
        [SerializeField] MeshRenderer oldJets;

        struct Jet { public Vector3 p0, dir; public float u, w, aLand, seed; }
        Jet[] jets;
        Mesh mesh, nozzlesOnly;
        Material drawn;
        float peakAbove;
        readonly List<Vector3> verts = new List<Vector3>(), normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> tris = new List<int>();
        readonly Vector3[] centre = new Vector3[Along + 1];
        readonly float[] radius = new float[Along + 1], metres = new float[Along + 1];

        // For the builder: on an inactive object, before it wakes.
        public void Configure(Vector3[] nozzleMouths, Vector3[] directions, float waterHeight, Material waterMaterial, MeshRenderer oldJetModel)
        {
            nozzles = nozzleMouths;
            outs = directions;
            waterY = waterHeight;
            water = waterMaterial;
            oldJets = oldJetModel;
        }

        void Awake()
        {
            // In Play only, so the copy made here is never saved into a scene or a prefab.
            if (oldJets != null) nozzlesOnly = KeepOnly(oldJets, m => !m.name.Contains("fountain_jet") && !m.name.Contains("foam"));
            jets = new Jet[nozzles.Length];
            peakAbove = 0f;
            for (int i = 0; i < jets.Length; i++)
            {
                // The model's arc: from the mouth, 2.2 m out while the unit curve s - (1 + drop) s^2 takes it from 1 m up
                // at its top down to the moat. So the flight lasts T with w T = 1, g T^2 / 2 = 1 + drop, u T = 2.2.
                float drop = nozzles[i].y - waterY;
                float T = Mathf.Sqrt(2f * (1f + drop) / G);
                var jet = new Jet
                {
                    p0 = nozzles[i], dir = new Vector3(outs[i].x, 0f, outs[i].z).normalized,
                    u = 2.2f / T, w = 1f / T, aLand = T, seed = (i * .618034f) % 1f,
                };
                jets[i] = jet;
                peakAbove = Mathf.Max(peakAbove, drop + jet.w * jet.w / (2f * G));
            }
            // A copy of the falls' water that runs: its foot blends into the moat over the last half metre.
            drawn = water != null ? new Material(water) { name = "Garden spout water" } : null;
            if (drawn != null)
            {
                drawn.SetFloat("_Speed", 2.6f);
                drawn.SetFloat("_Wobble", .012f);
                drawn.SetFloat("_Aeration", .8f);
                drawn.SetFloat("_FallHeight", peakAbove);
                drawn.SetFloat("_FootHeight", .35f);
            }
            mesh = new Mesh { name = "Garden spouts" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var view = gameObject.AddComponent<MeshRenderer>();
            view.sharedMaterial = drawn;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (drawn != null) Destroy(drawn);
            if (nozzlesOnly != null) Destroy(nozzlesOnly);
        }

        // A copy of the renderer's mesh with only the submeshes whose material passes (null: nothing to take out).
        static Mesh KeepOnly(MeshRenderer renderer, System.Func<Material, bool> keep)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            var source = filter != null ? filter.sharedMesh : null;
            if (source == null || !source.isReadable) { renderer.enabled = false; return null; }
            var slots = renderer.sharedMaterials;
            var kept = new List<Material>();
            var lists = new List<int[]>();
            for (int i = 0; i < source.subMeshCount && i < slots.Length; i++)
                if (slots[i] != null && keep(slots[i])) { kept.Add(slots[i]); lists.Add(source.GetTriangles(i)); }
            if (kept.Count == slots.Length) return null;
            var copy = Instantiate(source);
            copy.name = source.name + " (nozzles)";
            copy.subMeshCount = lists.Count;
            for (int i = 0; i < lists.Count; i++) copy.SetTriangles(lists[i], i);
            filter.sharedMesh = copy;
            renderer.sharedMaterials = kept.ToArray();
            return copy;
        }

        void LateUpdate()
        {
            verts.Clear(); normals.Clear(); uvs.Clear(); colors.Clear(); tris.Clear();
            float t = Time.time;
            for (int i = 0; i < jets.Length; i++)
            {
                Stream(jets[i], t);
                Landing(jets[i], t);
                Drops(jets[i], i, t);
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0, false);
            mesh.RecalculateBounds();
        }

        // ------------------------------------------------------------------ the jet

        Vector3 At(in Jet j, float a) => j.p0 + j.dir * (j.u * a) + Vector3.up * (j.w * a - .5f * G * a * a);
        Vector3 Velocity(in Jet j, float a) => j.dir * j.u + Vector3.up * (j.w - G * a);

        // How far down the jet has fallen towards the moat, 0 at its top, 1 on the water: the shader's air and foot.
        float Fall(float y) => Mathf.Clamp01(1f - (y - waterY) / Mathf.Max(peakAbove, .1f));

        void Stream(in Jet j, float t)
        {
            float speed0 = Velocity(j, 0f).magnitude, run = 0f;
            for (int k = 0; k <= Along; k++)
            {
                float s = (float)k / Along, a = s * j.aLand;
                var p = At(j, a);
                // The pressure surges: the nozzle sends a little more, then less, so swellings ride down the stream (each
                // part of the water left the nozzle a seconds ago) and the falling end sways from side to side.
                float leftAt = t - a, surge = Mathf.Sin(leftAt * 7.3f + j.seed * 6.3f) * .5f + Mathf.Sin(leftAt * 12.9f + j.seed * 2.1f) * .3f;
                var side = Vector3.Cross(Vector3.up, j.dir);
                p += side * (s * s * .05f * Mathf.Sin(leftAt * 2.3f + j.seed * 9f));
                if (k > 0) run += Vector3.Distance(p, centre[k - 1]);
                centre[k] = p;
                metres[k] = run;
                float speed = Velocity(j, a).magnitude;
                radius[k] = R0 * Mathf.Sqrt(speed0 / speed) * (1f + Spread * s * s) * (1f + .12f * surge * (.3f + s))
                            * Mathf.Min(1f, s * 40f + .55f);   // a slight neck as it leaves the mouth
            }
            int start = verts.Count;
            for (int k = 0; k <= Along; k++)
            {
                var forward = (centre[Mathf.Min(Along, k + 1)] - centre[Mathf.Max(0, k - 1)]).normalized;
                var ax = Vector3.Cross(forward, Vector3.up).normalized;
                var ay = Vector3.Cross(ax, forward);
                float fall = Fall(centre[k].y);
                for (int q = 0; q <= Around; q++)
                {
                    float ph = 2f * Mathf.PI * q / Around;
                    var n = ax * Mathf.Cos(ph) + ay * Mathf.Sin(ph);
                    verts.Add(centre[k] + n * radius[k]);
                    normals.Add(n);
                    uvs.Add(new Vector2(ph * R0 * 6f, metres[k]));
                    colors.Add(new Color(.5f, fall, j.seed, 1f));
                }
            }
            int row = Around + 1;
            for (int k = 0; k < Along; k++)
                for (int q = 0; q < Around; q++)
                {
                    int i = start + k * row + q;
                    tris.Add(i); tris.Add(i + row + 1); tris.Add(i + row);
                    tris.Add(i); tris.Add(i + 1); tris.Add(i + row + 1);
                }
        }

        // Where the jet lands: a dome of white water that churns, and rings that spread out on the moat and fade.
        void Landing(in Jet j, float t)
        {
            var land = At(j, j.aLand);
            land.y = waterY;
            float churn = .5f + .5f * Mathf.Sin(t * 9.1f + j.seed * 5f);
            Dome(land, .17f + .03f * churn, .07f + .04f * churn, j.seed, t);
            for (int r = 0; r < 3; r++)
            {
                float age = Mathf.Repeat(t * .9f + r / 3f + j.seed, 1f);
                float rad = .2f + .75f * age, h = .012f * (1f - age);
                Ring(land, rad, .04f + .05f * age, h, .96f + .03f * age, j.seed, t);
            }
        }

        void Dome(Vector3 c, float r, float h, float seed, float t)
        {
            const int K = 5;
            int start = verts.Count;
            for (int k = 0; k <= K; k++)
            {
                float e = k * Mathf.PI / 2f / K;
                for (int q = 0; q <= Around; q++)
                {
                    float ph = 2f * Mathf.PI * q / Around;
                    float bump = 1f + .18f * Mathf.Sin(ph * 3f + t * 6f + seed * 7f) * Mathf.Cos(e);
                    var n = new Vector3(Mathf.Cos(e) * Mathf.Cos(ph), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Sin(ph));
                    verts.Add(c + new Vector3(n.x * r * bump, n.y * h - .01f, n.z * r * bump));
                    normals.Add(n);
                    uvs.Add(new Vector2(ph * r, t * 2f + e * r));
                    colors.Add(new Color(.5f, .9f, seed, 1f));
                }
            }
            Grid(start, K);
        }

        void Ring(Vector3 c, float rad, float width, float h, float fall, float seed, float t)
        {
            int start = verts.Count;
            for (int k = 0; k <= 2; k++)
            {
                float rr = rad - width * .5f + width * k * .5f, y = k == 1 ? h : 0f;
                for (int q = 0; q <= Around * 2; q++)
                {
                    float ph = 2f * Mathf.PI * q / (Around * 2);
                    verts.Add(c + new Vector3(rr * Mathf.Cos(ph), y + .004f, rr * Mathf.Sin(ph)));
                    normals.Add(Vector3.up);
                    uvs.Add(new Vector2(ph * rr, t + rr));
                    colors.Add(new Color(.5f, fall, seed, 1f));
                }
            }
            Grid(start, 2, Around * 2);
        }

        void Grid(int start, int rows, int around = Around)
        {
            int row = around + 1;
            for (int k = 0; k < rows; k++)
                for (int q = 0; q < around; q++)
                {
                    int i = start + k * row + q;
                    tris.Add(i); tris.Add(i + row + 1); tris.Add(i + row);
                    tris.Add(i); tris.Add(i + 1); tris.Add(i + row + 1);
                }
        }

        // Drops: some peel off the falling end before it lands, more jump back up from where it lands.
        void Drops(in Jet j, int index, float t)
        {
            var land = At(j, j.aLand);
            for (int d = 0; d < 14; d++)
            {
                const float Life = .55f;
                float phase = Hash(index, d, 0), cycleT = t / Life + phase;
                int cycle = Mathf.FloorToInt(cycleT);
                float age = (cycleT - cycle) * Life;
                float r1 = Hash(index, d, cycle * 3 + 1), r2 = Hash(index, d, cycle * 3 + 2), r3 = Hash(index, d, cycle * 3 + 3);
                float ph = r1 * 2f * Mathf.PI, out_ = .25f + .7f * r2;
                var from = new Vector3(land.x, waterY + .02f, land.z) + new Vector3(Mathf.Cos(ph), 0f, Mathf.Sin(ph)) * (.08f + .1f * r3);
                var v = new Vector3(Mathf.Cos(ph) * out_, .9f + 1.4f * r3, Mathf.Sin(ph) * out_) + j.dir * .4f;
                var p = from + v * age + .5f * G * age * age * Vector3.down;
                if (p.y > waterY) Droplet(p, v + G * age * Vector3.down, .012f + .016f * r2, j.seed);
            }
            for (int d = 0; d < 6; d++)
            {
                // Peeled off the jet in its last third, falling a little apart from it.
                float phase = Hash(index, d + 50, 0), lane = (t * 1.7f + phase);
                int cycle = Mathf.FloorToInt(lane);
                float frac = lane - cycle;
                float r1 = Hash(index, d + 50, cycle * 2 + 1), r2 = Hash(index, d + 50, cycle * 2 + 2);
                float a = j.aLand * (.68f + .32f * frac);
                var side = Vector3.Cross(Vector3.up, j.dir);
                var p = At(j, a) + side * ((r1 - .5f) * .16f * frac) + j.dir * (r2 * .06f * frac) + Vector3.down * (.05f * frac);
                if (p.y > waterY) Droplet(p, Velocity(j, a), .014f + .01f * r2, j.seed);
            }
        }

        // A drop: an octahedron drawn out along its motion.
        void Droplet(Vector3 p, Vector3 velocity, float size, float seed)
        {
            float speed = velocity.magnitude;
            var d = speed > 1e-4f ? velocity / speed : Vector3.up;
            float stretch = 1f + .06f * speed;
            var ax = Mathf.Abs(d.y) < .95f ? Vector3.Cross(d, Vector3.up).normalized : Vector3.right;
            var ay = Vector3.Cross(d, ax);
            int start = verts.Count;
            Point(p + d * size * stretch, d, seed);
            Point(p - d * size * .8f, -d, seed);
            for (int q = 0; q < 4; q++)
            {
                float a = q * Mathf.PI / 2f;
                var o = ax * Mathf.Cos(a) + ay * Mathf.Sin(a);
                Point(p + o * size, o, seed);
            }
            for (int q = 0; q < 4; q++)
            {
                int a = start + 2 + q, b = start + 2 + (q + 1) % 4;
                tris.Add(start); tris.Add(a); tris.Add(b);
                tris.Add(start + 1); tris.Add(b); tris.Add(a);
            }
        }

        void Point(Vector3 p, Vector3 n, float seed)
        {
            verts.Add(p);
            normals.Add(n);
            uvs.Add(Vector2.zero);
            colors.Add(new Color(.5f, .7f, seed, 1f));
        }

        static float Hash(int a, int b, int c)
        {
            unchecked
            {
                uint h = 2166136261;
                h = (h ^ (uint)a) * 16777619;
                h = (h ^ (uint)b) * 16777619;
                h = (h ^ (uint)c) * 16777619;
                h ^= h >> 13; h *= 1274126177; h ^= h >> 16;
                return (h & 0xffffff) / (float)0x1000000;
            }
        }
    }
}
