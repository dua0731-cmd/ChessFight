using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R426 (승규 11-05, "물기둥을 실제 물로 해서 자연스럽게 나오게 해 수학적으로 계산해"): a canal geyser's water, computed
    // every frame from the shared obstacle clock (so every PC draws the same water; nothing here touches the game - the
    // hit stays GardenGeyser's). The same model, line for line, as ArtSource/PawnRush/GardenConcept/jet_math.py, which
    // renders it in Blender (jet_preview.py).
    //
    // s = seconds since the geyser began to warn (warn 0.8 s, jet 1 s, cycle 5 s); t = s - 0.8 since the valve opened.
    //   Water leaves the nozzle at v0 = sqrt(2 g H) (H = GardenGeyser.height, 5 m). A parcel that left a seconds ago is at
    //   z(a) = v0 a - g a^2 / 2, rising at v(a) = v0 - g a: the column's front climbs as a ballistic head and the column
    //   stands whole only after a_top = v0 / g (1.01 s).
    //   Continuity (A v = const through every section) widens it as the water slows: r = (r0 + k z) sqrt(v0 / v), k z
    //   the aerated jet's spread, capped at RCap near the top.
    //   Past the top each parcel spreads outward at Us and falls freely: rho = RCap + Us dt, z = H - g dt^2 / 2, an
    //   umbrella landing 2 a_top after the parcel left. When the valve shuts the last parcel obeys the same law: the
    //   column's foot leaves the nozzle and climbs after it.
    //   Droplets fly ballistically: shed from the rising head, broken off the umbrella, splashed up where it lands, spat
    //   from the nozzle while it warns. A dome of white water churns at the foot while the valve is open (where the
    //   jet throws pawns), a ring of foam where the umbrella lands, ripples on the basin while it warns.
    // Drawn with GC_jet (the garden falls' shader, its flow speed 0; vertex alpha 1 = a whole stream, 0 = the umbrella's
    // curtain that thins into strands): UV0 = (metres round, the parcel's own flow
    // coordinate), so streaks ride on the water; vertex colour g = how much air it holds (white; over 0.94 it thins out).
    [DisallowMultipleComponent]
    public sealed class GardenJet : MonoBehaviour
    {
        const float G = 9.81f;
        const float TWarn = .8f, TOn = 1f;
        const float R0 = .26f, KAir = .05f, VMin = 1.3f, RCap = .62f, Us = .45f;
        const float JetZ0 = .025f, FloorZ = -.02f, PoolZ = -.032f;
        const int Seg = 20;

        [SerializeField] int order;
        [SerializeField] float height = 5f;
        [Tooltip("A basin of water round the nozzle (ripples while it warns); off for the dry-deck ones on the ramp.")]
        [SerializeField] bool pool = true;
        [SerializeField] Material water;

        float v0, aTop, aLand, end, seed;
        Mesh mesh;
        MeshRenderer view;
        readonly List<Vector3> verts = new List<Vector3>(), normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> tris = new List<int>();
        readonly List<Vector2> prof = new List<Vector2>();   // (rho, height) along a surface
        readonly List<float> flows = new List<float>(), airs = new List<float>();

        // For the builder: on an inactive object, before it wakes.
        public void Configure(int order, float height, bool pool, Material water)
        {
            this.order = order;
            this.height = height;
            this.pool = pool;
            this.water = water;
        }

        void Awake()
        {
            v0 = Mathf.Sqrt(2f * G * height);
            aTop = v0 / G;
            aLand = 2f * aTop;
            end = TWarn + TOn + aLand + .45f;
            seed = Hash01(order, 9, 0, 0, 0);
            var go = new GameObject("Jet water");
            go.transform.SetParent(transform, false);
            mesh = new Mesh { name = "Garden jet" };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            view = go.AddComponent<MeshRenderer>();
            view.sharedMaterial = water;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.enabled = false;
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        void LateUpdate()
        {
            double now = ObstacleClock.Now, into;
            var state = RoyalWaterGardenRules.GeyserState(order, now, out into);
            float s = state == RoyalWaterGardenRules.Jet.Warn ? (float)into
                : state == RoyalWaterGardenRules.Jet.Jet ? TWarn + (float)into : TWarn + TOn + (float)into;
            Draw(s, RoyalWaterGardenRules.GeyserCycleIndex(order, now));
        }

        // ------------------------------------------------------------------ the model

        void Draw(float s, long cycle)
        {
            verts.Clear(); normals.Clear(); uvs.Clear(); colors.Clear(); tris.Clear();
            if (s >= 0f && s <= end)
            {
                float t = s - TWarn;
                if (t < 0f) Warn(s);
                else { Column(t); Umbrella(t); Boil(t); Landing(t); }
                Droplets(t, cycle);
            }
            view.enabled = verts.Count > 0;
            if (!view.enabled) return;
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0, false);
            mesh.bounds = new Bounds(new Vector3(0f, height * .6f, 0f), new Vector3(6f, height * 1.4f, 6f));
        }

        float ColumnRadius(float z, float v) => Mathf.Min(RCap, (R0 + KAir * Mathf.Max(z, 0f)) * Mathf.Sqrt(v0 / Mathf.Max(v, VMin)));

        void Begin() { prof.Clear(); flows.Clear(); airs.Clear(); }
        void Add(float rho, float z, float flow, float air) { prof.Add(new Vector2(rho, z)); flows.Add(flow); airs.Add(air); }

        // The basin bulges over the nozzle and ripples; spits fly (Droplets).
        void Warn(float tw)
        {
            float w = tw / TWarn;
            float hb = .02f + .1f * w * w + .025f * Mathf.Sin(31f * tw) * w;
            float rb = .3f + .15f * w;
            Begin();
            for (int i = 0; i <= 6; i++)
            {
                float q = i * Mathf.PI / 2f / 6f;
                Add(rb * Mathf.Cos(q), JetZ0 - .01f + hb * Mathf.Sin(q), tw * 2f, .5f);
            }
            Strip();
            if (!pool) return;
            for (int k = 0; k < 3; k++)
            {
                float p = Mathf.Repeat(tw * 1.6f + k / 3f, 1f), rho = .35f + .6f * p, air = .62f + .35f * p;
                Begin();
                Add(rho - .03f, PoolZ + .003f, k, air);
                Add(rho + .03f, PoolZ + .003f, k, air);
                Strip();
            }
        }

        // The rising water: parcels a_lo..a_hi seconds old, from the nozzle (or the climbing foot) to the head.
        void Column(float t)
        {
            float aLo = Mathf.Max(0f, t - TOn), aHi = Mathf.Min(t, aTop);
            if (aHi - aLo < 1e-3f) return;
            Begin();
            if (aLo <= 0f) Add(R0 * .9f, JetZ0 - .02f, t * v0 * .5f, .5f);
            const int K = 24;
            for (int k = 0; k < K; k++)
            {
                float a = aLo + (aHi - aLo) * k / (K - 1);
                float z = v0 * a - .5f * G * a * a;
                Add(ColumnRadius(z, v0 - G * a), JetZ0 + z, (t - a) * v0 * .5f, .5f + .3f * a / aTop);
            }
            if (aLo > 0f)
            {
                // The valve has shut: the column's foot is a rounded tail climbing after it.
                var foot = prof[0];
                float f0 = flows[0], g0 = airs[0];
                prof.Insert(0, new Vector2(foot.x * .7f, foot.y - .14f * foot.x)); flows.Insert(0, f0); airs.Insert(0, g0);
                prof.Insert(0, new Vector2(0f, foot.y - .3f * foot.x)); flows.Insert(0, f0); airs.Insert(0, g0);
            }
            var top = prof[prof.Count - 1];
            float f = flows[flows.Count - 1], g = Mathf.Max(airs[airs.Count - 1], .78f);
            if (t < aTop)
            {
                // The starting jet's head: a mushroom wider than the column.
                Add(top.x * 1.12f, top.y + .12f * top.x, f, g);
                Add(top.x * .95f, top.y + .35f * top.x, f, g);
                Add(top.x * .6f, top.y + .55f * top.x, f, g);
                Add(0f, top.y + .62f * top.x, f, g);
            }
            else
            {
                // Water still reaching the top: the crown it turns over at.
                Add(top.x * .7f, top.y + .25f, f, g);
                Add(0f, top.y + .32f, f, g);
            }
            Strip();
        }

        // Past the top: parcels spread at Us and fall freely, thinning out (air over 0.94) as they near the ground.
        void Umbrella(float t)
        {
            float lo = Mathf.Max(aTop, t - TOn), hi = Mathf.Min(t, aLand);
            if (hi - lo < 1e-3f) return;
            Begin();
            const int K = 18;
            for (int k = 0; k < K; k++)
            {
                float a = lo + (hi - lo) * k / (K - 1), dt = a - aTop;
                Add(RCap + Us * dt, JetZ0 + height - .5f * G * dt * dt, (t - a) * v0 * .5f, .6f + .36f * dt / aTop);
            }
            Strip(0f);  // a curtain (vertex alpha 0): the falls' shader thins it into strands as it falls
        }

        // White water churning at the jet's foot while the valve is open: as wide as the hit (GardenGeyser.radius 0.9).
        void Boil(float t)
        {
            if (t > TOn + .45f) return;
            float w = t < TOn ? 1f : 1f - (t - TOn) / .45f;
            float hb = (.4f * Mathf.Exp(-3f * t) + .14f) * w, rb = .85f * (.75f + .25f * w);
            Begin();
            for (int i = 0; i <= 6; i++)
            {
                float q = i * Mathf.PI / 2f / 6f;
                Add(rb * Mathf.Cos(q), FloorZ + .01f + hb * Mathf.Sin(q), t * 2f, .74f);
            }
            Strip();
        }

        // A ring of foam where the umbrella lands, while it lands.
        void Landing(float t)
        {
            float w = Smooth((t - (aLand - .05f)) / .1f) * (1f - Smooth((t - (aLand + TOn)) / .4f));
            if (w <= .01f) return;
            float rl = RCap + Us * aTop;
            Begin();
            Add(rl - .3f, FloorZ + .005f, t * 1.5f, .76f);
            Add(rl - .15f, FloorZ + .12f * w, t * 1.5f, .76f);
            Add(rl + .1f, FloorZ + .14f * w, t * 1.5f, .76f);
            Add(rl + .3f, FloorZ + .005f, t * 1.5f, .76f);
            Strip();
        }

        void Droplets(float t, long cycle)
        {
            for (int j = 0; j < 60; j++)   // shed from the rising head
            {
                float r0 = Hash01(j, 1, cycle, order, 0), r1 = Hash01(j, 1, cycle, order, 1), r2 = Hash01(j, 1, cycle, order, 2),
                      r3 = Hash01(j, 1, cycle, order, 3), r4 = Hash01(j, 1, cycle, order, 4);
                float tb = .06f + r0 * (Mathf.Min(TOn, aTop) - .06f);
                float zf = v0 * tb - .5f * G * tb * tb, vf = v0 - G * tb;
                float rho = ColumnRadius(zf, vf) * .85f, ph = r1 * 2f * Mathf.PI, u = .5f + 1.4f * r3;
                Fly(t, tb, new Vector3(rho * Mathf.Cos(ph), JetZ0 + zf, rho * Mathf.Sin(ph)),
                    new Vector3(u * Mathf.Cos(ph), vf * (.55f + .45f * r2), u * Mathf.Sin(ph)), .035f + .04f * r4);
            }
            for (int j = 0; j < 50; j++)   // broken off the umbrella
            {
                float r0 = Hash01(j, 2, cycle, order, 0), r1 = Hash01(j, 2, cycle, order, 1), r2 = Hash01(j, 2, cycle, order, 2),
                      r3 = Hash01(j, 2, cycle, order, 3), r4 = Hash01(j, 2, cycle, order, 4);
                float a = aTop + r0 * aTop * .85f, tb = r2 * TOn + a, dt = a - aTop;
                float rho = RCap + Us * dt, ph = r1 * 2f * Mathf.PI, u = Us + .2f + .7f * r3;
                Fly(t, tb, new Vector3(rho * Mathf.Cos(ph), JetZ0 + height - .5f * G * dt * dt, rho * Mathf.Sin(ph)),
                    new Vector3(u * Mathf.Cos(ph), -G * dt * (1f + .1f * r4), u * Mathf.Sin(ph)), .03f + .035f * r4);
            }
            float rl = RCap + Us * aTop;
            for (int j = 0; j < 50; j++)   // splashed up where the umbrella lands
            {
                float r0 = Hash01(j, 3, cycle, order, 0), r1 = Hash01(j, 3, cycle, order, 1), r2 = Hash01(j, 3, cycle, order, 2),
                      r3 = Hash01(j, 3, cycle, order, 3), r4 = Hash01(j, 3, cycle, order, 4);
                float tb = r0 * TOn + aLand, ph = r1 * 2f * Mathf.PI, rho = rl * (.9f + .2f * r2), u = .3f + r3;
                Fly(t, tb, new Vector3(rho * Mathf.Cos(ph), FloorZ + .05f, rho * Mathf.Sin(ph)),
                    new Vector3(u * Mathf.Cos(ph), .9f + 1.6f * r4, u * Mathf.Sin(ph)), .03f + .03f * r1);
            }
            for (int j = 0; j < 24; j++)   // spat out of the nozzle while it warns
            {
                float r0 = Hash01(j, 4, cycle, order, 0), r1 = Hash01(j, 4, cycle, order, 1), r2 = Hash01(j, 4, cycle, order, 2),
                      r3 = Hash01(j, 4, cycle, order, 3), r4 = Hash01(j, 4, cycle, order, 4);
                float tb = -.5f + r0 * .48f, ph = r1 * 2f * Mathf.PI, u = .1f + .5f * r3;
                Fly(t, tb, new Vector3(.12f * Mathf.Cos(ph), JetZ0, .12f * Mathf.Sin(ph)),
                    new Vector3(u * Mathf.Cos(ph), 1.2f + 2.2f * r2, u * Mathf.Sin(ph)), .03f + .025f * r4);
            }
        }

        // A droplet born at tb at p0 with velocity u0: where gravity has carried it by t (none before tb or once down).
        void Fly(float t, float tb, Vector3 p0, Vector3 u0, float size)
        {
            float age = t - tb;
            if (age < 0f) return;
            var p = p0 + u0 * age + .5f * G * age * age * Vector3.down;
            if (p.y < FloorZ) return;
            Droplet(p, u0 + G * age * Vector3.down, size);
        }

        // ------------------------------------------------------------------ the mesh

        // One ring of Seg + 1 vertices per profile point (the seam on -x), quads between them.
        void Strip(float stream = 1f)
        {
            int n = prof.Count;
            if (n < 2) return;
            int start = verts.Count;
            for (int k = 0; k < n; k++)
            {
                var a = prof[Mathf.Max(0, k - 1)];
                var b = prof[Mathf.Min(n - 1, k + 1)];
                var tangent = b - a;
                var side = tangent.sqrMagnitude > 1e-10f ? new Vector2(tangent.y, -tangent.x).normalized : Vector2.right;
                for (int s = 0; s <= Seg; s++)
                {
                    float ph = Mathf.PI + 2f * Mathf.PI * s / Seg, c = Mathf.Cos(ph), sn = Mathf.Sin(ph);
                    verts.Add(new Vector3(prof[k].x * c, prof[k].y, prof[k].x * sn));
                    normals.Add(new Vector3(side.x * c, side.y, side.x * sn));
                    uvs.Add(new Vector2(Mathf.PI * s / Seg, flows[k]));
                    colors.Add(new Color(.5f, airs[k], seed, stream));
                }
            }
            int row = Seg + 1;
            for (int k = 0; k < n - 1; k++)
                for (int s = 0; s < Seg; s++)
                {
                    int i = start + k * row + s;
                    tris.Add(i); tris.Add(i + row); tris.Add(i + row + 1);
                    tris.Add(i); tris.Add(i + row + 1); tris.Add(i + 1);
                }
        }

        // A drop: an octahedron drawn out along its motion.
        void Droplet(Vector3 p, Vector3 velocity, float size)
        {
            float speed = velocity.magnitude;
            var d = speed > 1e-4f ? velocity / speed : Vector3.up;
            float stretch = 1f + .045f * speed;
            var ax = Mathf.Abs(d.y) < .95f ? Vector3.Cross(d, Vector3.up).normalized : Vector3.right;
            var ay = Vector3.Cross(d, ax);
            int start = verts.Count;
            Point(p + d * size * stretch, d);
            Point(p - d * size * .8f, -d);
            for (int q = 0; q < 4; q++)
            {
                float a = q * Mathf.PI / 2f;
                var o = ax * Mathf.Cos(a) + ay * Mathf.Sin(a);
                Point(p + o * size, o);
            }
            for (int q = 0; q < 4; q++)
            {
                int a = start + 2 + q, b = start + 2 + (q + 1) % 4;
                tris.Add(start); tris.Add(a); tris.Add(b);
                tris.Add(start + 1); tris.Add(b); tris.Add(a);
            }
        }

        void Point(Vector3 p, Vector3 n)
        {
            verts.Add(p);
            normals.Add(n);
            uvs.Add(Vector2.zero);
            colors.Add(new Color(.5f, .66f, seed, 1f));
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // jet_math.hash01: the same numbers in Blender and here.
        static float Hash01(int a, int b, long c, int d, int e)
        {
            uint h = 2166136261;
            Mix(ref h, (uint)a);
            Mix(ref h, (uint)b);
            Mix(ref h, (uint)c);
            Mix(ref h, (uint)d);
            Mix(ref h, (uint)e);
            h ^= h >> 16;
            return (h & 0xffffff) / (float)0x1000000;
        }

        static void Mix(ref uint h, uint k)
        {
            unchecked
            {
                h ^= k;
                h *= 16777619;
                h ^= h >> 13;
                h *= 1274126177;
            }
        }
    }
}
