using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R725 ro-vfx2 (Royal Observatory R7, device VFX: the bowl's rolling planet, PLAN 5 "구르는 행성 발밑 먼지"): star dust
    // kicked off the two rails under the ball as it rolls - the same amount for every metre it goes, so more where it
    // runs fast - left behind it and drifting down into the abyss with a few gold glints, and a puff off the rails as
    // it comes to rest at a dock. Each crossing is laid out once along its way (RollingPlanet.Across, a pure function
    // of the shared clock) and drawn from its age with VFX_BurstParticles, so every PC shows the same instant and
    // nothing is sent. Only draws.
    [DisallowMultipleComponent]
    public sealed class RoyalObservatoryVfxPlanet : MonoBehaviour
    {
        // The rails' middles either side of the way and how far under the ball's middle it meets them
        // (ro_r2_rolling_model.py: 0.8 m either side, the ball's surface 2.05 m under its middle there).
        const float RailApart = .8f, RailUnder = 2.05f;

        public static bool Enabled = true;

        [Range(4, 80), Tooltip("Dust puffs over a crossing (each rail).")]
        public int dustCount = 22;
        [Range(0, 40), Tooltip("Gold glints over a crossing (each rail).")]
        public int glintCount = 9;
        [Range(0, 20), Tooltip("Dust puffs off each rail as it comes to rest.")]
        public int puffCount = 6;
        public float dustLight = 1.3f;
        public float glintLight = 4f;
        [Header("Cost")]
        public float farDistance = 60f;

        sealed class Crossing
        {
            public MeshRenderer dust, glints;
            public Mesh dustMesh, glintMesh;
            public float start, dustSpan, glintSpan;
        }

        RollingPlanet planet;
        readonly Crossing[] crossings = new Crossing[2];
        MaterialPropertyBlock block;
        bool ready;

        void Start()
        {
            planet = GetComponent<RollingPlanet>();
            ready = planet != null && transform.parent != null && RoyalObservatoryVfxSparks.Material != null;
            if (!ready)
            {
                enabled = false;
                return;
            }
            block = new MaterialPropertyBlock();
            // Where it stands at the near dock, in its parent's frame (where it is now, less how far along it is).
            var parent = transform.parent;
            var travel = planet.Travel;
            var near = parent.InverseTransformPoint(transform.position) - travel * planet.Across(planet.Now);
            float dwell = planet.Dwell, cross = planet.CrossSeconds;
            // There (starting at `dwell` into the cycle) and back (from `2 dwell + cross`).
            crossings[0] = Make("there", near, travel, dwell, cross, 81);
            crossings[1] = Make("back", near + travel, -travel, 2f * dwell + cross, cross, 83);
        }

        Crossing Make(string way, Vector3 from, Vector3 travel, float start, float cross, int seed)
        {
            var c = new Crossing { start = start };
            float across0 = planet.Across(start) > .5f ? 1f : 0f;   // where this crossing starts, 0 or 1 of the way
            float Along(float t)
            {
                float a = planet.Across(start + t);
                return across0 > .5f ? 1f - a : a;   // 0 -> 1 along this crossing
            }
            c.dustMesh = Kick("RO planet dust " + way, from, travel, cross, Along, dustCount, puffCount, seed, false, out c.dustSpan);
            c.glintMesh = Kick("RO planet glints " + way, from, travel, cross, Along, glintCount, 0, seed + 1, true, out c.glintSpan);
            c.dust = RoyalObservatoryVfxSparks.Part("Rail dust " + way, transform.parent, c.dustMesh);
            c.glints = RoyalObservatoryVfxSparks.Part("Rail glints " + way, transform.parent, c.glintMesh);
            DustLook(c.dust);
            GlintLook(c.glints);
            return c;
        }

        // The particles of one crossing, in the parent's frame: each rail's share spaced evenly along the way (so evenly
        // in metres), each let go when the ball is there - its delay the time it gets there.
        Mesh Kick(string name, Vector3 from, Vector3 travel, float cross, System.Func<float, float> along, int count, int puffs,
                  int seed, bool glints, out float span)
        {
            var rng = new System.Random(seed);
            float Next() => (float)rng.NextDouble();
            var flat = Vector3.ProjectOnPlane(travel, Vector3.up);
            var dir = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward;
            var side = Vector3.Cross(Vector3.up, dir);
            float length = travel.magnitude;
            var points = new List<Vector3>();
            var corners = new List<Vector2>();
            var particles = new List<Vector4>();
            var velocities = new List<Vector3>();
            var triangles = new List<int>();
            float last = 0f;

            // When the ball is `share` of the way along (Along rises from 0 to 1 over the crossing).
            float When(float share)
            {
                float lo = 0f, hi = cross;
                for (int k = 0; k < 24; k++)
                {
                    float mid = (lo + hi) * .5f;
                    if (along(mid) < share) lo = mid; else hi = mid;
                }
                return (lo + hi) * .5f;
            }

            void Add(Vector3 start, Vector3 velocity, float life, float delay)
            {
                last = Mathf.Max(last, delay + life);
                var particle = new Vector4(life, Mathf.Lerp(.7f, 1.35f, Next()), delay, Mathf.Lerp(-50f, 50f, Next()));
                int at = points.Count;
                for (int c = 0; c < 4; c++)
                {
                    points.Add(start);
                    corners.Add(new Vector2(c & 1, c >> 1));
                    particles.Add(particle);
                    velocities.Add(velocity);
                }
                triangles.AddRange(new[] { at, at + 2, at + 1, at + 2, at + 3, at + 1 });
            }

            foreach (float rail in new[] { -1f, 1f })
            {
                for (int i = 0; i < count; i++)
                {
                    // Not in the first and last few centimetres, where it hardly moves.
                    float share = Mathf.Lerp(.03f, .97f, (i + Next()) / count);
                    float t = When(share);
                    float speed = (along(Mathf.Min(cross, t + .05f)) - along(Mathf.Max(0f, t - .05f))) / .1f * length;
                    var start = from + travel * share + side * (rail * RailApart) + Vector3.down * RailUnder
                              + dir * Mathf.Lerp(-.25f, .1f, Next());
                    // Left behind it, a little out to its own side, and floating.
                    var velocity = -dir * speed * Mathf.Lerp(.12f, glints ? .5f : .35f, Next())
                                 + side * rail * Mathf.Lerp(.1f, glints ? 1.1f : .6f, Next())
                                 + Vector3.up * Mathf.Lerp(-.1f, glints ? 1.2f : .45f, Next());
                    float life = glints ? Mathf.Lerp(.5f, 1f, Next()) : Mathf.Lerp(1f, 1.7f, Next());
                    Add(start, velocity, life, t);
                }
                // Coming to rest: a puff off the rail at the end of the way, out along it and to the side.
                for (int i = 0; i < puffs; i++)
                {
                    var start = from + travel + side * (rail * RailApart) + Vector3.down * RailUnder;
                    float angle = Mathf.Lerp(-80f, 80f, Next());
                    var outward = Quaternion.AngleAxis(angle, Vector3.up) * (side * rail);
                    var velocity = outward * Mathf.Lerp(.6f, 1.6f, Next()) + dir * Mathf.Lerp(0f, .8f, Next()) + Vector3.up * Mathf.Lerp(.05f, .4f, Next());
                    Add(start, velocity, Mathf.Lerp(.9f, 1.5f, Next()), cross - Mathf.Lerp(.05f, .25f, Next()));
                }
            }

            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(points);
            mesh.SetUVs(0, corners);
            mesh.SetUVs(1, particles);
            mesh.SetUVs(2, velocities);
            mesh.SetTriangles(triangles, 0);
            var bounds = new Bounds(from, Vector3.zero);
            foreach (var p in points) bounds.Encapsulate(p);
            bounds.Expand(new Vector3(6f, 8f, 6f));
            bounds.center += Vector3.down * 2f;
            mesh.bounds = bounds;
            span = last;
            return mesh;
        }

        void LateUpdate()
        {
            if (!ready) return;
            var eye = RoyalObservatoryVfxSparks.Eye(transform);
            bool on = Enabled && (transform.position - eye).sqrMagnitude < farDistance * farDistance;
            double now = planet.Now;
            float cycle = planet.Cycle;
            foreach (var c in crossings)
            {
                if (!on)
                {
                    c.dust.enabled = c.glints.enabled = false;
                    continue;
                }
                double t = (now - c.start) % cycle;
                float age = (float)(t < 0 ? t + cycle : t);
                RoyalObservatoryVfxSparks.Seek(c.dust, block, age, c.dustSpan);
                RoyalObservatoryVfxSparks.Seek(c.glints, block, age, c.glintSpan);
            }
        }

        // Soft star dust off the rails: cream and amber from the ball, turning dusk violet as it sinks.
        void DustLook(MeshRenderer r)
        {
            r.GetPropertyBlock(block);
            block.SetColor("_ColorStart", new Color(1f, .82f, .58f));
            block.SetColor("_ColorEnd", new Color(.52f, .48f, .86f));
            block.SetFloat(RoyalObservatoryVfxSparks.LightId, dustLight);
            block.SetFloat("_AlphaStart", .45f);
            block.SetFloat("_AlphaEnd", 0f);
            block.SetFloat("_FadeIn", .12f);
            block.SetFloat("_SizeStart", .16f);
            block.SetFloat("_SizeEnd", .5f);
            block.SetFloat("_Stretch", 0f);
            block.SetFloat("_Soft", 1f);
            block.SetFloat("_Hard", 0f);
            block.SetFloat("_Additive", .5f);
            block.SetFloat("_DepthFade", .25f);
            block.SetFloat("_Gravity", 1.1f);
            block.SetFloat("_Drag", 1.6f);
            block.SetFloat("_FloorY", -1e5f);
            block.SetFloat(RoyalObservatoryVfxSparks.AgeId, -1f);
            r.SetPropertyBlock(block);
        }

        void GlintLook(MeshRenderer r)
        {
            r.GetPropertyBlock(block);
            block.SetColor("_ColorStart", new Color(1f, .95f, .8f));
            block.SetColor("_ColorEnd", new Color(1f, .58f, .22f));
            block.SetFloat(RoyalObservatoryVfxSparks.LightId, glintLight);
            block.SetFloat("_AlphaStart", 1f);
            block.SetFloat("_AlphaEnd", 0f);
            block.SetFloat("_FadeIn", .03f);
            block.SetFloat("_SizeStart", .06f);
            block.SetFloat("_SizeEnd", .02f);
            block.SetFloat("_Stretch", .015f);
            block.SetFloat("_Soft", .85f);
            block.SetFloat("_Hard", 0f);
            block.SetFloat("_Additive", 1f);
            block.SetFloat("_DepthFade", 0f);
            block.SetFloat("_Gravity", 3f);
            block.SetFloat("_Drag", 1f);
            block.SetFloat("_FloorY", -1e5f);
            block.SetFloat(RoyalObservatoryVfxSparks.AgeId, -1f);
            r.SetPropertyBlock(block);
        }

        void OnDisable()
        {
            foreach (var c in crossings)
                if (c != null) c.dust.enabled = c.glints.enabled = false;
        }

        void OnDestroy()
        {
            foreach (var c in crossings)
            {
                if (c == null) continue;
                if (c.dustMesh != null) Destroy(c.dustMesh);
                if (c.glintMesh != null) Destroy(c.glintMesh);
                if (c.dust != null) Destroy(c.dust.gameObject);
                if (c.glints != null) Destroy(c.glints.gameObject);
            }
        }
    }
}
