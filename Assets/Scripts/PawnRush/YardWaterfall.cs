using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    // R684 (승규 11-16 "물 그래픽도 수학적으로 계산해서 물래방아에서 떨어지는 물 등등 해서 그래픽 좋게 넣어"): the Royal Carriage
    // Yard's falling water, worked out every frame and drawn as one dynamic mesh per place (the giant wheel; the lifting
    // wheel with the cliff's fall; each sluice) in a copy of the garden falls' shader (PR_GardenFallSheet: UV0 = (metres
    // across or round, the parcel's own flow coordinate), vertex colour r across, g how far it has fallen, b a seed,
    // a 1 for a round stream and 0 for a curtain). A picture only: nothing collides, nothing is sent, nothing moves the
    // wheels - their pose is read off their transforms, which ObstacleClock drives, so the water turns with them on
    // every PC. The formulas, with the symbols used below:
    //
    //  g = 9.81 m/s^2 down. A wheel turns at omega (rad/s, signed about its axle A, measured from its transform each frame
    //  and smoothed, so a wheel that stops stops pouring); a point r out from its centre c moves at v = omega A x r.
    //  phi is a blade's angle from the top, counted the way the top moves (0 top, pi bottom); it is under water while
    //  cos(phi) < (y_water - c_y) / R, i.e. for phi in (phi_sub, 2 pi - phi_sub).
    //
    //  1 The giant wheel's buckets (overshot: the flume fills them at the top, they empty down the falling side). A
    //    bucket holds V(phi) = V0 until phi_s (spillFrom, 65 deg: earlier, gravity's pull towards the axle beats the
    //    lip's speed and the stream falls back into the buckets below - worked out, one leaving at 40 deg passes 1.3 m
    //    inside the rim, one at 65 deg stays outside it), then tips it out as
    //      V(phi) = V0 (1 + cos(pi x)) / 2,  x = (phi - phi_s) / (phi_e - phi_s),  phi_e = min(132 deg, phi_sub - 6 deg),
    //    so it pours at Q = -dV/dt = V0 omega pi sin(pi x) / (2 (phi_e - phi_s)): most when it lies on its side.
    //    Water leaves the bucket's lip (radius R_lip) with the lip's velocity plus a little out of the bucket,
    //      v_e = omega A x r_e + u_out r_e / |r_e|,
    //    and falls freely. The stream seen now is a streakline: the parcel that left tau seconds ago left when the bucket
    //    was omega tau further back, from r_e(tau) = Rot(A, -omega tau) r(now), and is at
    //      P(tau) = c + r_e(tau) + v_e(tau) tau + g tau^2 / 2
    //    until P_y(tau_land) = y_water (found by marching tau in 0.04 s steps, then bisection). Its flow is
    //    Q(phi - |omega| tau): the stream starts in mid-air before the bucket tips and its tail falls clear after.
    //  2 Continuity (A v = Q along a stream): a stream leaving at |v_e| with lip radius r_0 sqrt(Q / Q_max) is
    //      r(tau) = r_0 sqrt(Q / Q_max) sqrt(|v_e| / |v(tau)|),  |v(tau)| = |v_e + g tau|:
    //    it thins as it speeds up.
    //  3 Break-up (Rayleigh-Plateau): a falling stream necks and breaks after about 4.5 capillary times,
    //      t_b = 4.5 sqrt(rho r^3 / sigma) (rho 1000 kg/m^3, sigma 0.072 N/m; jittered +-25 % per stream),
    //    into drops one fastest-growing wavelength apart, lambda = 9.02 r, each of radius (3/4 r^2 lambda)^(1/3) = 1.89 r.
    //    A drop is the parcel that left at an emission time on a fixed grid (dt_e = lambda / |v_e|), so drops travel down
    //    the streakline with the water instead of flickering; a thinner flow drops fewer (kept with probability
    //    min(1, 1.5 Q / Q_max)). A full pour (r 7 cm: t_b = 4.5 x 2.2 s) outlasts its 1.4 s fall and arrives whole, as
    //    real ones do; the thin start and end of each pour break up. A stream still whole sheds spray off its surface,
    //    the more the faster it falls ((tau / tau_land)^2 of its flow).
    //  4 Draining blades (both wheels' rising side, 승규's "물레방아에서 떨어지는 물"): a blade that comes up out of the
    //    water carries a film that runs off its lip: a short sheet at first (flow e^(-s / 0.3), s seconds since the lip
    //    came out), then drips whose rate falls as e^(-s / T_drain) (a Poisson process thinned on the emission grid). They
    //    leave with the blade's velocity - upward on the rising side - so they climb a little before they fall.
    //  5 A blade going in throws a crown: drops leave at (1.2 .. 3.2 m/s up, +-0.75 m/s along the axle, back from the
    //    blade's way) and fly as P = E + v t + g t^2 / 2; the white water is the pool shader's churn.
    //  6 Sheets over a lip (the cliff, the sluices, the flume's mouth): a nappe leaving horizontally at u falls as
    //      P(tau) = lip + n u tau + g tau^2 / 2,  tau_land = sqrt(2 h / g):
    //    it curves out from the wall and lands n u tau_land out. The sheet's streaks ride with the parcels (UV.y is the
    //    parcel's age times the shader's streak speed), so they stretch as the water speeds up, as real streaks do. A
    //    few round jets break out of it (u + 0.08 out: still clear of the lifting wheel and the barges; continuity as in 2).
    //    The sheet is drawn as its face and its back, 7 cm apart, and a veil of mist rises where it plunges.
    //  7 Where water lands: no meshes of white water (11-17: they read as plastic) - a crown of drops jumping back up (age = t mod 0.55 s per
    //    drop, v = (out 0.25 .. 0.95, up 0.9 .. 2.3) m/s), and a landing line handed to the water's shader (YardWater ->
    //    PR_YardWater's _RcyFallLines) for its churn, white foam and rings.
    // Every PC draws its own from its own clock; the quality tier (YardWater.Tier) sets the detail (table in
    // Docs/PawnRush/RCY_MODELS.md "R684 물").
    [DisallowMultipleComponent]
    public sealed class YardWaterfall : MonoBehaviour
    {
        public const float G = 9.81f;
        const float Rho = 1000f, Sigma = .072f;   // water: density (kg/m^3), surface tension (N/m)
        const float StreakSpeed = 4f;            // the shader's _Speed: streak metres per second of a parcel's age
        const float FootHeight = .45f;            // the shader's foot blend into the pool (m)
        const float Span = 2.4f;                  // the longest a parcel is followed (s): 28 m of fall
        const int MaxVerts = 60000;

        // A landing line in this object's frame, for YardWater.
        public struct Line
        {
            public Vector3 a, b;
            public float strength, reach, scale;
        }

        // Detail by quality tier (0 low, 1 medium, 2 high).
        struct Detail
        {
            public int around, along, drops, crown, spillEmitters, dripEmitters;
            public float column, drip;
            public bool jets;
        }

        static readonly Detail[] Details =
        {
            // R686 (승규 11-17 "물 떨어지는 거 더 사실적으로"): a bucket 1.2 m wide pours a broad sheet, not a thread or two -
            // 2 / 3 / 5 overlapping streams over 85 % of its width (were 1 / 2 / 2 over 55 %), so they read as one.
            new Detail { around = 4, along = 10, drops = 5, crown = 0, spillEmitters = 2, dripEmitters = 2, column = .8f, drip = .45f, jets = false },
            new Detail { around = 5, along = 16, drops = 9, crown = 3, spillEmitters = 3, dripEmitters = 3, column = .45f, drip = .7f, jets = true },
            new Detail { around = 7, along = 22, drops = 14, crown = 6, spillEmitters = 5, dripEmitters = 5, column = .3f, drip = 1f, jets = true },
        };

        abstract class Part
        {
            public float top;   // highest the water starts over its pool (m), for the shader's foot
            public abstract void Build(YardWaterfall f, float t, float dt);
            public abstract void Extend(Transform frame, ref Bounds bounds);
        }

        // ------------------------------------------------------------------ a turning wheel's water (1 - 5)

        sealed class Wheel : Part
        {
            public Transform wheel;
            public Vector3 axisLocal, zeroLocal;   // in the wheel's own frame: the axle, blade 0's way out
            public float lip;                      // centre to where water leaves a blade (m)
            public int blades;
            public float halfWidth;                // half the blades' width along the axle (m)
            public bool spills;                    // the giant wheel's buckets (1); else only draining blades (4)
            public float spillFrom, spillTo, spillOut, streamRadius;
            public float drainTime, dripRate, filmRadius;
            public float omega;                    // signed rad/s about the axle, measured
            public int spillLine = -1, inLine = -1, outLine = -1;
            Quaternion last;
            bool seen;
            Vector3 landMin, landMax;
            bool landed;

            public override void Extend(Transform frame, ref Bounds bounds)
            {
                bounds.Encapsulate(new Bounds(frame.InverseTransformPoint(wheel.position), Vector3.one * (lip + 4f) * 2f));
            }

            // omega from the turn since last frame (the transform is the rigidbody's interpolated pose).
            public void Measure(YardWaterfall f, float dt)
            {
                var axisWorld = wheel.TransformDirection(axisLocal);
                if (seen && dt > 0f)
                {
                    (wheel.rotation * Quaternion.Inverse(last)).ToAngleAxis(out float deg, out var ax);
                    if (!float.IsNaN(ax.x) && !float.IsInfinity(ax.x))
                    {
                        if (deg > 180f) deg -= 360f;
                        float rate = deg * Mathf.Deg2Rad * Mathf.Sign(Vector3.Dot(ax, axisWorld)) / dt;
                        if (Mathf.Abs(deg) < 1e-4f) rate = 0f;
                        omega = Mathf.Lerp(omega, rate, 1f - Mathf.Exp(-dt * 4f));
                    }
                }
                last = wheel.rotation;
                seen = true;
            }

            public override void Build(YardWaterfall f, float t, float dt)
            {
                var tf = f.transform;
                var c = tf.InverseTransformPoint(wheel.position);
                var A = tf.InverseTransformDirection(wheel.TransformDirection(axisLocal)).normalized;
                float w = Mathf.Abs(omega);
                if (w < .02f) return;
                var up = tf.InverseTransformDirection(Vector3.up);
                var ahead = Vector3.Cross(A * omega, up).normalized;   // the way the top moves
                float cosSub = Mathf.Clamp((f.waterY - Vector3.Dot(c, up)) / lip, -1f, 1f);
                float sub = Mathf.Acos(cosSub), emerge = 2f * Mathf.PI - sub;
                var d = f.detail;
                landed = false;
                for (int i = 0; i < blades; i++)
                {
                    var dir = tf.InverseTransformDirection(wheel.TransformDirection(Quaternion.AngleAxis(i * 360f / blades, axisLocal) * zeroLocal));
                    float phi = Mathf.Atan2(Vector3.Dot(dir, ahead), Vector3.Dot(dir, up));
                    if (phi < 0f) phi += 2f * Mathf.PI;
                    if (spills && phi > spillFrom && phi < spillTo + w * Span)
                        for (int k = 0; k < d.spillEmitters; k++)
                            f.Stream(this, c, A, dir, phi, 0f, Across(k, d.spillEmitters, .85f), i * 31 + k, t, 0);
                    // Seconds since the blade's lip came out of the water (phi past 2 pi - phi_sub; the top wraps to 0).
                    float since = (phi >= emerge ? phi - emerge : phi + 2f * Mathf.PI - emerge) / w;
                    if (since < drainTime * 3f + 1.6f)
                        for (int k = 0; k < d.dripEmitters; k++)
                        {
                            float a = Across(k, d.dripEmitters, .85f);
                            if (since < .9f) f.Stream(this, c, A, dir, phi, since, a, i * 31 + k + 900, t, 1);
                            f.Drips(this, c, A, dir, since, a, i * 31 + k, t);
                        }
                    float into = (phi - sub) / w;
                    if (into > 0f && into < .6f) f.Crown(this, c, A, up, ahead, sub, into, i, t);
                }
                if (spillLine >= 0 && landed)
                    f.lines[spillLine] = new Line { a = landMin, b = landMax, strength = 1f, reach = 1.1f, scale = 1f };
            }

            // Emitter k of n along the axle, spread over `share` of the width.
            float Across(int k, int n, float share) => n <= 1 ? 0f : halfWidth * share * (2f * k / (n - 1) - 1f);

            // A bucket's pour (1): sin(pi x) over its tipping angles.
            public float Spill(float phi)
            {
                float x = (phi - spillFrom) / Mathf.Max(spillTo - spillFrom, 1e-3f);
                return x > 0f && x < 1f ? Mathf.Sin(Mathf.PI * x) : 0f;
            }

            public void Landed(Vector3 p)
            {
                if (!landed) { landMin = landMax = p; landed = true; return; }
                // The line runs between the two landings furthest apart.
                if ((p - landMin).sqrMagnitude > (landMax - landMin).sqrMagnitude) landMax = p;
                else if ((p - landMax).sqrMagnitude > (landMax - landMin).sqrMagnitude) landMin = p;
            }
        }

        // ------------------------------------------------------------------ a sheet over a lip (6)

        sealed class Sheet : Part
        {
            public Vector3 lip0, lip1, outward;
            public float u, landY, seed;
            public float wallGap;   // how far the lip stands off the wall behind it (m): the back never reaches the wall
            public bool onWater;
            public int jets;

            public float TauLand => Mathf.Sqrt(2f * Mathf.Max(.01f, lip0.y - landY) / G);

            public override void Extend(Transform frame, ref Bounds bounds)
            {
                bounds.Encapsulate(lip0 + Vector3.up * .3f);
                bounds.Encapsulate(lip1 + Vector3.up * .3f);
                float far = (u + .08f) * TauLand + 1.5f;
                bounds.Encapsulate(new Vector3(lip0.x, landY - .3f, lip0.z) + outward * far + (lip0 - lip1).normalized * 1.5f);
                bounds.Encapsulate(new Vector3(lip1.x, landY - .3f, lip1.z) + outward * far + (lip1 - lip0).normalized * 1.5f);
                bounds.Encapsulate(new Vector3(lip0.x, landY + 2.5f, lip0.z) - outward * 1.5f);
            }

            public override void Build(YardWaterfall f, float t, float dt) => f.Nappe(this, t);
        }

        // ------------------------------------------------------------------ the overflow over a wheel's rim (8)

        // R686 (승규 11-17 "물 표현이 부자연스러워"; the concept U2: the flume's water running over the wheel's upper falling
        // side and off it): the water the flume gives the top buckets more of than they hold, riding over the outside of
        // the rim with them and leaving it where gravity can no longer turn it round the axle. Drawn as a body of water
        // (face, back, sides) like a nappe (6). The formulas:
        //  - It moves with the rim, v = |omega| R. A layer on the outside of a turning drum stays on it while gravity's
        //    pull towards the axle can supply the turn, g cos(phi) >= v^2 / R, so it leaves at
        //      phi_d = acos(v^2 / (g R))      (the giant wheel: 15 deg/s, R 9.3 m -> v 2.43 m/s, phi_d 86 deg),
        //    and then falls freely from there with the rim's velocity: P(tau) = P_d + v t_d tau + g tau^2 / 2, landing
        //    at tau_land = (v_y + sqrt(v_y^2 + 2 g h)) / g (v_y its downward speed, h its height over the water).
        //  - Its depth over the rim grows from 2.5 cm at the top to `thick` a third of the way round (the buckets
        //    overflow more as they fill), then the falling sheet takes in air and bulks: thick + 0.3 m (tau / tau_land).
        //  - Its streaks ride with the water: UV.y = the shader's streak speed times the water's age (arc / v on the
        //    rim, then the fall's tau), so they slide round the rim and stretch as the sheet speeds up.
        sealed class RimFilm : Part
        {
            public Wheel wheel;          // the wheel whose rim it runs over (its axle, its turn)
            public float radius;         // the rim's outside (m)
            public float halfWidth;      // half its width along the axle (m)
            public float thick;          // its depth over the rim once it has filled (m)
            public float seed;
            public int line = -1;

            public override void Extend(Transform frame, ref Bounds bounds)
            {
                var c = frame.InverseTransformPoint(wheel.wheel.position);
                bounds.Encapsulate(new Bounds(c, Vector3.one * (radius + 7f) * 2f));
            }

            public override void Build(YardWaterfall f, float t, float dt) => f.Film(this, t);
        }

        YardWater owner;
        Material source, drawn;
        Mesh mesh;
        MeshRenderer view;
        float waterY, fallHeight = 1f, lastTime = -1f;
        Detail detail;
        Bounds bounds;
        bool finished, everBuilt;
        readonly List<Part> parts = new List<Part>();
        readonly List<Line> lines = new List<Line>();
        readonly List<Vector3> verts = new List<Vector3>(), normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> tris = new List<int>();
        // One stream's samples (reused).
        const int MaxAlong = 40;
        readonly Vector3[] centre = new Vector3[MaxAlong + 1], velocity = new Vector3[MaxAlong + 1];
        readonly float[] radius = new float[MaxAlong + 1], flowCoord = new float[MaxAlong + 1];
        // A thick sheet's face and back (reused).
        Vector3[] face, back;

        // Landing lines, this object's frame.
        public List<Line> Lines => lines;
        public float WaterY => waterY;

        // An inactive object under `group` (the pool's frame); add its parts, then Finish. YardWater shows it.
        public static YardWaterfall Create(Transform group, string name, YardWater owner, Material fallSheet, float waterHeight)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(group, false);
            var f = go.AddComponent<YardWaterfall>();
            f.owner = owner;
            f.source = fallSheet;
            f.waterY = waterHeight;
            return f;
        }

        // A wheel turning in this pool: `axisLocal` its axle and `zeroLocal` blade 0's way out, both in the wheel's frame.
        // spills: the buckets pour down the falling side (1); else its blades only drain as they come up (4).
        public void AddWheel(Transform wheel, Vector3 axisLocal, Vector3 zeroLocal, float lipRadius, int blades, float halfWidth,
                             float degreesPerSecond, bool spills, float spillFromDeg, float spillOut, float streamRadius,
                             float drainTime, float dripRate, bool crossingLines)
        {
            if (wheel == null) return;
            var c = transform.InverseTransformPoint(wheel.position);
            float cosSub = Mathf.Clamp((waterY - c.y) / lipRadius, -1f, 1f), sub = Mathf.Acos(cosSub);
            var part = new Wheel
            {
                wheel = wheel, axisLocal = axisLocal.normalized, zeroLocal = zeroLocal.normalized, lip = lipRadius,
                blades = Mathf.Max(1, blades), halfWidth = halfWidth, spills = spills,
                spillFrom = spillFromDeg * Mathf.Deg2Rad, spillTo = Mathf.Min(132f * Mathf.Deg2Rad, sub - 6f * Mathf.Deg2Rad),
                spillOut = spillOut, streamRadius = streamRadius, drainTime = drainTime, dripRate = dripRate,
                filmRadius = .03f, omega = degreesPerSecond * Mathf.Deg2Rad,
                top = c.y + lipRadius - waterY,
            };
            if (spills)
            {
                part.spillLine = lines.Count;
                lines.Add(new Line());   // filled once the buckets pour
            }
            if (crossingLines)
            {
                // Where the blades cut the surface, either side of the bottom: churn and rings all the time (5), the
                // falling side (into the water) harder.
                var A = transform.InverseTransformDirection(wheel.TransformDirection(axisLocal)).normalized;
                var ahead = Vector3.Cross(A * part.omega, Vector3.up).normalized;
                float side = Mathf.Sin(sub) * lipRadius;
                foreach (float s in new[] { 1f, -1f })
                {
                    var at = new Vector3(c.x, waterY, c.z) + ahead * (s * side);
                    lines.Add(new Line { a = at - A * halfWidth, b = at + A * halfWidth, strength = s > 0f ? .85f : .55f, reach = .9f, scale = .8f });
                }
            }
            parts.Add(part);
        }

        // A sheet leaving the lip lip0..lip1 (this frame, horizontal) at u m/s along `outward`, landing at landY (6).
        public void AddSheet(Vector3 lip0, Vector3 lip1, Vector3 outward, float u, float landY, bool onWater, int jets, float seed,
                             float wallGap = .05f)
        {
            outward.y = 0f;
            var part = new Sheet
            {
                lip0 = lip0, lip1 = lip1, outward = outward.normalized, u = u, landY = landY, onWater = onWater, jets = jets, seed = seed,
                wallGap = Mathf.Max(.05f, wallGap),
                top = lip0.y - waterY,
            };
            parts.Add(part);
            if (!onWater) return;
            float far = u * part.TauLand;
            lines.Add(new Line
            {
                a = new Vector3(lip0.x, landY, lip0.z) + part.outward * far, b = new Vector3(lip1.x, landY, lip1.z) + part.outward * far,
                strength = 1.6f, reach = 1.3f, scale = 1.1f,
            });
        }

        // R686: the overflow over the rim of the wheel added last (8): `radius` the rim's outside, `halfWidth` half its
        // width along the axle, `thick` the layer's depth once it has filled.
        public void AddRimFilm(float radius, float halfWidth, float thick, float seed)
        {
            Wheel w = null;
            for (int i = parts.Count - 1; i >= 0 && w == null; i--) w = parts[i] as Wheel;
            if (w == null) return;
            var c = transform.InverseTransformPoint(w.wheel.position);
            var part = new RimFilm { wheel = w, radius = radius, halfWidth = halfWidth, thick = thick, seed = seed, top = c.y + radius - waterY };
            part.line = lines.Count;
            lines.Add(new Line());   // filled once it lands
            parts.Add(part);
        }

        // The material and the mesh once every part is in; registers with YardWater.
        public void Finish()
        {
            if (finished) return;
            finished = true;
            fallHeight = .5f;
            bool first = true;
            foreach (var p in parts)
            {
                fallHeight = Mathf.Max(fallHeight, p.top + .3f);
                var b = p is Sheet sheet ? new Bounds(sheet.lip0, Vector3.zero)
                      : p is Wheel wheel ? new Bounds(transform.InverseTransformPoint(wheel.wheel.position), Vector3.zero)
                      : p is RimFilm film ? new Bounds(transform.InverseTransformPoint(film.wheel.wheel.position), Vector3.zero) : new Bounds();
                p.Extend(transform, ref b);
                if (first) bounds = b; else bounds.Encapsulate(b);
                first = false;
            }
            if (source != null)
            {
                drawn = new Material(source) { name = "Yard falling water (" + name + ")" };
                drawn.SetFloat("_Speed", StreakSpeed);
                drawn.SetFloat("_Wobble", .018f);
                drawn.SetFloat("_Aeration", .92f);
                drawn.SetFloat("_FallHeight", fallHeight);
                drawn.SetFloat("_FootHeight", FootHeight);
                // The concept's falling water: a white-blue streaked sheet #DBF0FF.
                drawn.SetColor("_Tint", new Color(.86f, .94f, 1f));
                drawn.SetColor("_FoamColor", new Color(.95f, .98f, 1f));
                drawn.SetColor("_PoolTint", new Color(.62f, .88f, .92f));
            }
            mesh = new Mesh { name = "Yard falling water" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            view = gameObject.AddComponent<MeshRenderer>();
            view.sharedMaterial = drawn;
            view.shadowCastingMode = ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.lightProbeUsage = LightProbeUsage.Off;
            view.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mesh.bounds = bounds;
            if (owner != null) owner.AddFall(this);
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (drawn != null) Destroy(drawn);
        }

        void LateUpdate()
        {
            if (!finished || mesh == null) return;
            float t = Time.time, dt = lastTime < 0f ? 0f : t - lastTime;
            lastTime = t;
            // The wheels' omega is followed every frame; the mesh is rebuilt only when it was on screen last frame and
            // the camera is within 110 m (the haze has it by then).
            foreach (var p in parts)
                if (p is Wheel w) w.Measure(this, dt);
            var cam = Camera.main;
            bool far = false;
            if (cam != null)
            {
                var eye = transform.InverseTransformPoint(cam.transform.position);
                far = (bounds.ClosestPoint(eye) - eye).sqrMagnitude > 110f * 110f;
            }
            if (everBuilt && (far || !view.isVisible)) return;
            detail = Details[Mathf.Clamp(owner != null ? owner.Tier : 2, 0, Details.Length - 1)];
            verts.Clear(); normals.Clear(); uvs.Clear(); colors.Clear(); tris.Clear();
            foreach (var p in parts) p.Build(this, t, dt);
            everBuilt = true;
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0, false);
            mesh.bounds = bounds;
        }

        bool Full => verts.Count > MaxVerts;

        // The shader's g: how far below its start the water is, 0 .. 1, so the last FootHeight over the pool blends in.
        float Fallen(float y) => Mathf.Clamp01(1f - (y - waterY) / fallHeight);

        // White water standing on the pool: g just over the foot blend, so it stays white and whole.
        float FoamFallen => Mathf.Clamp01(1f - FootHeight * 1.3f / fallHeight);

        // ------------------------------------------------------------------ streams (1 - 4)

        // The parcel of age tau of the stream that blade `dir` (now at phi) pours from `a` along the axle: where it is,
        // and its velocity. `out_` m/s leave out of the blade on top of the blade's own velocity.
        static Vector3 Parcel(Vector3 c, Vector3 A, Vector3 dir, float lip, float omega, float a, float out_, float tau, Vector3 g, out Vector3 v)
        {
            var r = Quaternion.AngleAxis(-omega * tau * Mathf.Rad2Deg, A) * (dir * lip);
            var ve = Vector3.Cross(A * omega, r) + r.normalized * out_;
            v = ve + g * tau;
            return c + r + A * a + ve * tau + .5f * tau * tau * g;
        }

        // Mode 0: a bucket's pour (1-3); mode 1: the film off a blade that has just come out (4), `since` s ago.
        void Stream(Wheel w, Vector3 c, Vector3 A, Vector3 dir, float phi, float since, float a, int id, float t, int mode)
        {
            if (Full) return;
            var g = Vector3.down * G;
            float abs = Mathf.Abs(w.omega), out_ = mode == 0 ? w.spillOut : 0f;
            // tau_land: march, then bisect.
            float lo = 0f, hi = -1f;
            for (float tau = .04f; tau <= Span; tau += .04f)
            {
                if (Parcel(c, A, dir, w.lip, w.omega, a, out_, tau, g, out _).y <= waterY) { hi = tau; break; }
                lo = tau;
            }
            if (hi < 0f) hi = Span;
            for (int k = 0; k < 5; k++)
            {
                float mid = (lo + hi) * .5f;
                if (Parcel(c, A, dir, w.lip, w.omega, a, out_, mid, g, out _).y <= waterY) hi = mid; else lo = mid;
            }
            float tauLand = hi;
            int along = Mathf.Min(detail.along, MaxAlong);
            // The flow each parcel left with; the biggest sets the stream's break-up.
            float peak = 0f;
            for (int k = 0; k <= along; k++) peak = Mathf.Max(peak, Flow(w, mode, phi, since, tauLand * k / along, abs));
            if (peak < .02f) return;
            float r0 = (mode == 0 ? w.streamRadius : w.filmRadius) * Mathf.Sqrt(peak);
            float seed = Hash(id, 7, 0);
            // 3: t_b = 4.5 sqrt(rho r^3 / sigma), jittered.
            float tauB = Mathf.Clamp(4.5f * Mathf.Sqrt(Rho * r0 * r0 * r0 / Sigma) * (.75f + .5f * seed), .15f, tauLand);
            float speed0 = 0f;
            int rows = Mathf.Max(3, Mathf.CeilToInt(along * tauB / Mathf.Max(tauLand, .01f)));
            for (int k = 0; k <= rows; k++)
            {
                float tau = tauB * k / rows;
                centre[k] = Parcel(c, A, dir, w.lip, w.omega, a, out_, tau, g, out velocity[k]);
                if (k == 0) speed0 = velocity[0].magnitude;
                float q = Flow(w, mode, phi, since, tau, abs);
                // 2: continuity; a neck as it breaks up. R686: a bucket's pour (mode 0) is a broad sheet that spreads
                // sideways as it speeds up, so its thickness falls as (v0 / v)^0.3 rather than a round jet's ^0.5 (the
                // pours read as white threads 4 cm thick at the bottom).
                float neck = Mathf.Min(1f, (tauB - tau) / .12f + .3f);
                float thin = speed0 / Mathf.Max(velocity[k].magnitude, .05f);
                radius[k] = (mode == 0 ? w.streamRadius : w.filmRadius) * Mathf.Sqrt(q) * (mode == 0 ? Mathf.Pow(thin, .3f) : Mathf.Sqrt(thin)) * neck;
                flowCoord[k] = StreakSpeed * (.85f + .3f * seed) * tau;
            }
            Tube(rows, seed);
            // 3: the drops past the break, on a fixed emission grid so each one travels.
            float lambda = 9.02f * r0, dtE = Mathf.Max(lambda / Mathf.Max(speed0, .5f), (tauLand - tauB) / detail.drops);
            int n0 = Mathf.CeilToInt((t - tauLand) / dtE), n1 = Mathf.FloorToInt((t - tauB) / dtE);
            for (int n = n0; n <= n1 && !Full; n++)
            {
                float tau = t - n * dtE;
                float q = Flow(w, mode, phi, since, tau, abs);
                if (q <= 0f || Hash(id, n, 1) > 1.5f * q) continue;
                var p = Parcel(c, A, dir, w.lip, w.omega, a, out_, tau, g, out var v);
                float spread = .14f * (tau - tauB);
                p += A * ((Hash(id, n, 2) - .5f) * spread) + Vector3.Cross(A, v.normalized) * ((Hash(id, n, 3) - .5f) * spread);
                if (p.y <= waterY) continue;
                Droplet(p, v, 1.89f * r0 * Mathf.Sqrt(q) * (.7f + .6f * Hash(id, n, 4)), Fallen(p.y), seed);
            }
            // A stream still whole: spray torn off its surface by the air, more as it speeds up ((tau / tau_land)^2 of
            // its flow), each drop drifting out from the stream as it falls.
            if (tauB >= tauLand * .98f && detail.crown > 0)
            {
                float dtS = tauLand / detail.drops;
                int s0 = Mathf.CeilToInt((t - tauLand) / dtS), s1 = Mathf.FloorToInt((t - .35f * tauLand) / dtS);
                for (int n = s0; n <= s1 && !Full; n++)
                {
                    float tau = t - n * dtS, share = tau / tauLand;
                    float q = Flow(w, mode, phi, since, tau, abs);
                    if (q <= 0f || Hash(id, n, 8) > share * share * q) continue;
                    var p = Parcel(c, A, dir, w.lip, w.omega, a, out_, tau, g, out var v);
                    float turn = Hash(id, n, 9) * 2f * Mathf.PI;
                    var side = Vector3.Cross(v.normalized, A).normalized;
                    var away = A * Mathf.Cos(turn) + side * Mathf.Sin(turn);
                    p += away * (r0 * Mathf.Sqrt(q) + .3f * (tau - .35f * tauLand));
                    if (p.y > waterY) Droplet(p, v, .01f + .014f * Hash(id, n, 10), Fallen(p.y), seed);
                }
            }
            // 7: where it lands.
            float qLand = Flow(w, mode, phi, since, tauLand, abs);
            if (qLand < .12f) return;
            var land = Parcel(c, A, dir, w.lip, w.omega, a, out_, tauLand, g, out var vLand);
            land.y = waterY;
            if (mode == 0) w.Landed(land);
            // The white water itself is the pool's (the shader's churn under the landing line); only the drops here.
            Crown(land, new Vector3(vLand.x, 0f, vLand.z) * .15f, Mathf.Sqrt(qLand), id, t, detail.crown);
        }

        // The relative flow (0 .. 1) a parcel of age tau left with.
        static float Flow(Wheel w, int mode, float phi, float since, float tau, float abs)
        {
            if (mode == 0) return w.Spill(phi - abs * tau);
            float s = since - tau;   // seconds the blade had been out when the parcel left
            return s < 0f ? 0f : Mathf.Exp(-s / .3f);
        }

        // 4: drips off a draining blade's lip, `since` s after it came out.
        void Drips(Wheel w, Vector3 c, Vector3 A, Vector3 dir, float since, float a, int id, float t)
        {
            var g = Vector3.down * G;
            float rate = w.dripRate * detail.drip;
            if (rate <= 0f) return;
            float dtE = 1f / rate;
            int n0 = Mathf.CeilToInt((t - Span) / dtE), n1 = Mathf.FloorToInt(t / dtE);
            for (int n = n0; n <= n1 && !Full; n++)
            {
                float tau = t - n * dtE, left = since - tau;   // the blade had been out `left` s when this one left
                if (left < 0f || Hash(id, n, 5) > Mathf.Exp(-left / w.drainTime)) continue;
                float off = (Hash(id, n, 6) - .5f) * .22f;
                var p = Parcel(c, A, dir, w.lip, w.omega, a + off, 0f, tau, g, out var v);
                if (p.y <= waterY) continue;
                Droplet(p, v, .011f + .014f * Hash(id, n, 7), Fallen(p.y), Hash(id, 7, 0));
            }
        }

        // 5: a blade going in throws a crown, `into` s ago, along the whole width of the blade.
        void Crown(Wheel w, Vector3 c, Vector3 A, Vector3 up, Vector3 ahead, float sub, float into, int blade, float t)
        {
            if (Full) return;
            var entry = c + (up * Mathf.Cos(sub) + ahead * Mathf.Sin(sub)) * w.lip;
            entry.y = waterY;
            int spots = Mathf.Max(1, detail.dripEmitters);
            int cycle = Mathf.FloorToInt((t - into) * 4f);   // a new set of drops each time a blade goes in
            for (int k = 0; k < spots; k++)
            {
                float a = spots <= 1 ? 0f : w.halfWidth * .85f * (2f * k / (spots - 1) - 1f);
                var at = entry + A * a;
                for (int j = 0; j < detail.crown; j++)
                {
                    float h1 = Hash(blade * 13 + k, j, cycle), h2 = Hash(blade * 13 + k, j + 40, cycle), h3 = Hash(blade * 13 + k, j + 80, cycle);
                    var v = Vector3.up * (1.2f + 2f * h1) + A * ((h2 - .5f) * 1.5f) - ahead * (.6f * h3);
                    var p = at + v * into + .5f * into * into * Vector3.down * G;
                    if (p.y > waterY) Droplet(p, v + Vector3.down * G * into, .014f + .016f * h2, Fallen(p.y), h3);
                }
            }
        }

        // ------------------------------------------------------------------ sheets (6)

        void Nappe(Sheet s, float t)
        {
            if (Full) return;
            var g = Vector3.down * G;
            float tauLand = s.TauLand;
            var span = s.lip1 - s.lip0;
            float width = span.magnitude;
            var across = span / Mathf.Max(width, 1e-4f);
            int cols = Mathf.Max(2, Mathf.CeilToInt(width / detail.column)), rows = Mathf.Min(detail.along, MaxAlong);
            Volume(s, t, tauLand, span, width, across, cols, rows);
            // Round jets breaking out of the sheet (2, 6).
            if (detail.jets && s.jets > 0)
                for (int j = 0; j < s.jets && !Full; j++)
                {
                    float where = Mathf.Clamp01((j + .5f) / s.jets + (Hash(j, 3, 11) - .5f) * .6f / s.jets);
                    var from = s.lip0 + span * where - s.outward * .02f;
                    float u = s.u + .08f, seed = Hash(j, 5, 11);
                    float tl = Mathf.Sqrt(2f * Mathf.Max(.01f, from.y - s.landY) / G);
                    int jr = Mathf.Min(detail.along, MaxAlong);
                    for (int k = 0; k <= jr; k++)
                    {
                        float tau = tl * k / jr;
                        velocity[k] = s.outward * u + g * tau;
                        centre[k] = from + s.outward * (u * tau) + .5f * tau * tau * g;
                        // Continuity from 0.085 m at the lip; its surge rides down with the water.
                        float surge = 1f + .12f * Mathf.Sin((t - tau) * 7.3f + seed * 6.3f);
                        radius[k] = .085f * Mathf.Sqrt(u / Mathf.Max(velocity[k].magnitude, .05f)) * surge * Mathf.Min(1f, k * .5f + .6f);
                        flowCoord[k] = StreakSpeed * (.85f + .3f * seed) * tau;
                    }
                    Tube(jr, seed);
                }
            // 7: where it lands.
            var landA = new Vector3(s.lip0.x, s.landY + (s.onWater ? 0f : .05f), s.lip0.z) + s.outward * (s.u * tauLand);
            // 승규 11-17 ("하얀색 물 방울 더미 같은게 ... 자연스럽지 않아"): no heaped white meshes - the falls' shader cannot
            // fade a mound's edge into the pool, so they read as crumpled plastic. The white water is the pool's own
            // (PR_YardWater's churn and foam under the landing line, boiling and fading out); here only the drops that
            // jump back up along the line, and the veil of mist.
            int spots = Mathf.Max(1, Mathf.CeilToInt(width / (detail.column * 1.3f)));
            for (int k = 0; k < spots && !Full; k++)
            {
                float u = (k + .5f) / spots, h = Hash(k, 1, 13);
                var at = landA + span * u + s.outward * ((h - .4f) * .3f);
                Crown(at, s.outward * .6f, 1.3f, k * 7 + 300, t, detail.crown / (s.onWater ? 1 : 2));
            }
            if (s.onWater && detail.jets) Veil(landA, span, s.outward, t, s.seed);
        }

        // 승규 11-16 ("폭포수가 좀 더 강하게", 11-17 "두께를 좀 더 두껍게 해서 입체감을 살려"): the nappe as a body of water, closed
        // all round - a face, a back and two sides - so the sun, the sky and the shading see a thickness.
        //  - Thickness: water off a sill is h = q / u thick, then takes in air as it falls and bulks:
        //      thick(tau) = h0 + bulk (tau / tau_land),  h0 0.1 m, bulk 0.45 m,
        //    grown backwards, towards the wall: the face keeps its parabola (the lifting wheel's paddles pass 0.8 m off
        //    the marble at its axle's height, the face 0.68), and the back stops 4 cm short of the wall, whose gap opens
        //    as the water is thrown out (gap(tau) = lip's gap + u tau).
        //  - Lumps on the face that ride down with the water: two travelling waves of the parcel's leaving time
        //    (t - tau) across the lip, 0 .. 6 cm out, more as it falls; the face's normals are worked out from its own
        //    shape (finite differences), so the lumps catch the light.
        //  - Vertex a: face 0.62, back 0.5, sides 0.75 (between the shader's curtain and stream: whole, white, little
        //    seen through).
        void Volume(Sheet s, float t, float tauLand, Vector3 span, float width, Vector3 across, int cols, int rows)
        {
            const float H0 = .1f, Bulk = .45f, Lump = .06f;
            var g = Vector3.down * G;
            float streak = StreakSpeed * (.85f + .3f * s.seed), backSeed = (s.seed + .29f) % 1f;
            int row = cols + 1;
            if (face == null || face.Length < (rows + 1) * row) { face = new Vector3[(rows + 1) * row]; back = new Vector3[face.Length]; }
            for (int r = 0; r <= rows; r++)
            {
                float tau = tauLand * r / rows, share = tau / Mathf.Max(tauLand, 1e-3f), left = t - tau;
                float thick = Mathf.Min(H0 + Bulk * share, s.wallGap + s.u * tau - .04f);
                for (int k = 0; k <= cols; k++)
                {
                    float u = (float)k / cols, x = u * width;
                    var p = s.lip0 + span * u + s.outward * (s.u * tau) + .5f * tau * tau * g;
                    float lump = Lump * (.25f + .75f * share)
                                 * (.5f + .3f * Mathf.Sin(x * 2.3f + left * 5.1f + s.seed * 13f) + .2f * Mathf.Sin(x * 5.7f - left * 8.3f + s.seed * 29f));
                    face[r * row + k] = p + s.outward * lump;
                    back[r * row + k] = p - s.outward * Mathf.Max(.02f, thick);
                }
            }
            // The face: its normals from its shape, turned outwards.
            int start = verts.Count;
            for (int r = 0; r <= rows; r++)
            {
                float tau = tauLand * r / rows;
                for (int k = 0; k <= cols; k++)
                {
                    var down = face[Mathf.Min(rows, r + 1) * row + k] - face[Mathf.Max(0, r - 1) * row + k];
                    var side = face[r * row + Mathf.Min(cols, k + 1)] - face[r * row + Mathf.Max(0, k - 1)];
                    var n = Vector3.Cross(side, down).normalized;
                    if (Vector3.Dot(n, s.outward) < 0f) n = -n;
                    var p = face[r * row + k];
                    verts.Add(p);
                    normals.Add(n);
                    uvs.Add(new Vector2((float)k / cols * width, streak * tau));
                    colors.Add(new Color((float)k / cols, Fallen(p.y), s.seed, .62f));
                }
            }
            Grid(start, rows, cols);
            // The back, facing the wall.
            start = verts.Count;
            for (int r = 0; r <= rows; r++)
            {
                float tau = tauLand * r / rows;
                var n = -Vector3.Cross(across, s.outward * s.u + g * tau).normalized;
                if (Vector3.Dot(n, s.outward) > 0f) n = -n;
                for (int k = 0; k <= cols; k++)
                {
                    var p = back[r * row + k];
                    verts.Add(p);
                    normals.Add(n);
                    uvs.Add(new Vector2((float)k / cols * width + 3.7f, streak * tau));
                    colors.Add(new Color((float)k / cols, Fallen(p.y), backSeed, .5f));
                }
            }
            Grid(start, rows, cols);
            // The two sides, closing it (each a strip from the back to the face, down the fall).
            foreach (int k in new[] { 0, cols })
            {
                var n = k == 0 ? -across : across;
                start = verts.Count;
                for (int r = 0; r <= rows; r++)
                {
                    float tau = tauLand * r / rows;
                    var a = back[r * row + k];
                    var b = face[r * row + k];
                    float th = (b - a).magnitude;
                    verts.Add(a); normals.Add(n); uvs.Add(new Vector2(0f, streak * tau)); colors.Add(new Color(k == 0 ? 0f : 1f, Fallen(a.y), s.seed, .75f));
                    verts.Add(b); normals.Add(n); uvs.Add(new Vector2(th, streak * tau)); colors.Add(new Color(k == 0 ? 0f : 1f, Fallen(b.y), s.seed, .75f));
                }
                Grid(start, rows, 1);
            }
        }

        // 8: the overflow riding the outside of a wheel's rim from its top, then falling off it (RimFilm, formulas there).
        readonly float[] rowAge = new float[MaxAlong + 16];
        readonly Vector3[] rowOut = new Vector3[MaxAlong + 16];

        void Film(RimFilm film, float t)
        {
            var w = film.wheel;
            float om = Mathf.Abs(w.omega);
            if (om < .02f || Full) return;
            var tf = transform;
            var c = tf.InverseTransformPoint(w.wheel.position);
            var A = tf.InverseTransformDirection(w.wheel.TransformDirection(w.axisLocal)).normalized;
            var up = tf.InverseTransformDirection(Vector3.up);
            var ahead = Vector3.Cross(A * w.omega, up).normalized;   // the way the top moves: the falling side
            var g = Vector3.down * G;
            const float Phi0 = .05f, Bulk = .3f, Lump = .04f;
            const int ArcRows = 12;
            float R = film.radius, v = om * R;
            float phiD = Mathf.Acos(Mathf.Clamp01(v * v / (G * R)));
            var nD = up * Mathf.Cos(phiD) + ahead * Mathf.Sin(phiD);
            var vD = (ahead * Mathf.Cos(phiD) - up * Mathf.Sin(phiD)) * v;
            var pD = c + nD * (R + .006f + film.thick * .5f);
            float vy = -vD.y, h = pD.y - waterY;
            if (h <= .05f) return;
            float tauLand = (vy + Mathf.Sqrt(vy * vy + 2f * G * h)) / G;
            float ageD = R * (phiD - Phi0) / Mathf.Max(v, .01f);
            int fallRows = Mathf.Min(detail.along, MaxAlong), rows = ArcRows + fallRows;
            float width = 2f * film.halfWidth;
            int cols = Mathf.Max(2, Mathf.CeilToInt(width / detail.column)), row = cols + 1;
            if (face == null || face.Length < (rows + 1) * row) { face = new Vector3[(rows + 1) * row]; back = new Vector3[face.Length]; }
            for (int r = 0; r <= rows; r++)
            {
                Vector3 mid, outN;
                float th, age;
                if (r <= ArcRows)
                {
                    float f = (float)r / ArcRows, phi = Phi0 + (phiD - Phi0) * f;
                    outN = up * Mathf.Cos(phi) + ahead * Mathf.Sin(phi);
                    th = Mathf.Lerp(.025f, film.thick, Mathf.Clamp01(f * 3f));
                    mid = c + outN * (R + .006f + th * .5f);
                    age = R * (phi - Phi0) / Mathf.Max(v, .01f);
                }
                else
                {
                    float tau = tauLand * (r - ArcRows) / fallRows;
                    var vel = vD + g * tau;
                    mid = pD + vD * tau + .5f * tau * tau * g;
                    outN = Vector3.Cross(A, vel).normalized;
                    if (Vector3.Dot(outN, mid - c) < 0f) outN = -outN;
                    th = film.thick + Bulk * tau / tauLand;
                    age = ageD + tau;
                }
                rowAge[r] = age;
                rowOut[r] = outN;
                float left = t - age, grown = (float)r / rows;
                for (int k = 0; k <= cols; k++)
                {
                    float a = ((float)k / cols - .5f) * width;
                    float lump = Lump * (.3f + .7f * grown)
                                 * (.5f + .3f * Mathf.Sin(a * 2.3f + left * 5.1f + film.seed * 13f) + .2f * Mathf.Sin(a * 5.7f - left * 8.3f + film.seed * 29f));
                    var p = mid + A * a;
                    face[r * row + k] = p + outN * (th * .5f + lump);
                    back[r * row + k] = p - outN * (th * .5f);
                }
            }
            float streak = StreakSpeed * (.85f + .3f * film.seed), backSeed = (film.seed + .29f) % 1f;
            // The face: its normals from its shape, turned outwards.
            int start = verts.Count;
            for (int r = 0; r <= rows; r++)
                for (int k = 0; k <= cols; k++)
                {
                    var down = face[Mathf.Min(rows, r + 1) * row + k] - face[Mathf.Max(0, r - 1) * row + k];
                    var side = face[r * row + Mathf.Min(cols, k + 1)] - face[r * row + Mathf.Max(0, k - 1)];
                    var n = Vector3.Cross(side, down).normalized;
                    if (Vector3.Dot(n, rowOut[r]) < 0f) n = -n;
                    var p = face[r * row + k];
                    verts.Add(p);
                    normals.Add(n);
                    uvs.Add(new Vector2((float)k / cols * width, streak * rowAge[r]));
                    colors.Add(new Color((float)k / cols, Fallen(p.y), film.seed, .62f));
                }
            Grid(start, rows, cols);
            // The back, towards the rim and then the wheel.
            start = verts.Count;
            for (int r = 0; r <= rows; r++)
                for (int k = 0; k <= cols; k++)
                {
                    var p = back[r * row + k];
                    verts.Add(p);
                    normals.Add(-rowOut[r]);
                    uvs.Add(new Vector2((float)k / cols * width + 3.7f, streak * rowAge[r]));
                    colors.Add(new Color((float)k / cols, Fallen(p.y), backSeed, .5f));
                }
            Grid(start, rows, cols);
            // The two sides, closing it.
            foreach (int k in new[] { 0, cols })
            {
                var n = k == 0 ? -A : A;
                start = verts.Count;
                for (int r = 0; r <= rows; r++)
                {
                    var a = back[r * row + k];
                    var b = face[r * row + k];
                    verts.Add(a); normals.Add(n); uvs.Add(new Vector2(0f, streak * rowAge[r])); colors.Add(new Color(k == 0 ? 0f : 1f, Fallen(a.y), film.seed, .75f));
                    verts.Add(b); normals.Add(n); uvs.Add(new Vector2((b - a).magnitude, streak * rowAge[r])); colors.Add(new Color(k == 0 ? 0f : 1f, Fallen(b.y), film.seed, .75f));
                }
                Grid(start, rows, 1);
            }
            // 7: where it lands - the pool's churn and foam under a landing line, drops jumping back up, the mist.
            var land = pD + vD * tauLand + .5f * tauLand * tauLand * g;
            land.y = waterY;
            var la = land - A * film.halfWidth;
            var lb = land + A * film.halfWidth;
            if (film.line >= 0) lines[film.line] = new Line { a = la, b = lb, strength = 1.5f, reach = 1.2f, scale = 1.1f };
            var vl = vD + g * tauLand;
            var outH = new Vector3(vl.x, 0f, vl.z);
            outH = outH.sqrMagnitude > 1e-4f ? outH.normalized : ahead;
            int spots = Mathf.Max(1, Mathf.CeilToInt(width / (detail.column * 1.3f)));
            for (int k = 0; k < spots && !Full; k++)
                Crown(la + (lb - la) * ((k + .5f) / spots), outH * .5f, 1.4f, k * 7 + 500, t, detail.crown);
            if (detail.jets && !Full) Veil(la, lb - la, outH, t, film.seed);
        }

        // The mist a plunging sheet throws up where it lands: a curtain along the landing line rising 0.9 m and leaning
        // out over the pool, all air (g past the foot, so it is white), thinning into wisps as it rises (the shader's
        // strands), its features moving up (UV.y grows with height) and swaying.
        void Veil(Vector3 landA, Vector3 span, Vector3 outward, float t, float seed)
        {
            const int Rows = 4;
            const float Height = .9f;
            float width = span.magnitude;
            int cols = Mathf.Max(2, Mathf.CeilToInt(width / detail.column));
            var across = span / Mathf.Max(width, 1e-4f);
            var normal = Vector3.Cross(across, Vector3.up).normalized;
            float g = FoamFallen, s2 = (seed + .53f) % 1f;
            int start = verts.Count;
            for (int r = 0; r <= Rows; r++)
            {
                float rise = (float)r / Rows, hgt = Height * rise;
                for (int k = 0; k <= cols; k++)
                {
                    float u = (float)k / cols;
                    float sway = Mathf.Sin(u * width * 1.7f + t * 2.3f + seed * 9f) * .12f * rise;
                    verts.Add(landA + span * u + Vector3.up * (hgt - .05f) + outward * (.15f + .45f * rise + sway));
                    normals.Add(normal);
                    uvs.Add(new Vector2(u * width, hgt * 2.5f));
                    colors.Add(new Color(u, g, s2, 0f));
                }
            }
            Grid(start, Rows, cols);
        }

        // ------------------------------------------------------------------ shapes

        // A round stream through centre[0..rows] with radius[], UV.y = flowCoord[].
        void Tube(int rows, float seed)
        {
            int around = detail.around, start = verts.Count;
            for (int k = 0; k <= rows; k++)
            {
                var forward = (centre[Mathf.Min(rows, k + 1)] - centre[Mathf.Max(0, k - 1)]).normalized;
                var ax = Vector3.Cross(forward, Vector3.up);
                ax = ax.sqrMagnitude > 1e-6f ? ax.normalized : Vector3.right;
                var ay = Vector3.Cross(ax, forward);
                float fallen = Fallen(centre[k].y);
                for (int q = 0; q <= around; q++)
                {
                    float ph = 2f * Mathf.PI * q / around;
                    var n = ax * Mathf.Cos(ph) + ay * Mathf.Sin(ph);
                    verts.Add(centre[k] + n * radius[k]);
                    normals.Add(n);
                    uvs.Add(new Vector2(ph * .12f, flowCoord[k]));
                    colors.Add(new Color(.5f, fallen, seed, 1f));
                }
            }
            Grid(start, rows, around);
        }

        // 7: drops jumping back up where water lands (each lives 0.55 s and comes again).
        void Crown(Vector3 land, Vector3 carry, float strength, int id, float t, int count)
        {
            for (int d = 0; d < count && !Full; d++)
            {
                const float Life = .55f;
                float phase = Hash(id, d, 0), cycleT = t / Life + phase;
                int cycle = Mathf.FloorToInt(cycleT);
                float age = (cycleT - cycle) * Life;
                float r1 = Hash(id, d, cycle * 3 + 1), r2 = Hash(id, d, cycle * 3 + 2), r3 = Hash(id, d, cycle * 3 + 3);
                if (r3 > .4f + .6f * strength) continue;
                float ph = r1 * 2f * Mathf.PI, out_ = .25f + .7f * r2;
                // From where it lands (the pool, or the wheel's top under the flume's pour), back down to it.
                var from = land + Vector3.up * .02f + new Vector3(Mathf.Cos(ph), 0f, Mathf.Sin(ph)) * (.08f + .14f * r3);
                var v = new Vector3(Mathf.Cos(ph) * out_, .9f + 1.4f * r3 * strength, Mathf.Sin(ph) * out_) + carry;
                var p = from + v * age + .5f * G * age * age * Vector3.down;
                if (p.y > land.y) Droplet(p, v + G * age * Vector3.down, .012f + .016f * r2, Fallen(p.y), r1);
            }
        }

        // A drop: an octahedron drawn out along its motion (GardenSpout's).
        void Droplet(Vector3 p, Vector3 v, float size, float fallen, float seed)
        {
            float speed = v.magnitude;
            var d = speed > 1e-4f ? v / speed : Vector3.up;
            float stretch = 1f + .06f * speed;
            var ax = Mathf.Abs(d.y) < .95f ? Vector3.Cross(d, Vector3.up).normalized : Vector3.right;
            var ay = Vector3.Cross(d, ax);
            int start = verts.Count;
            Point(p + d * size * stretch, d, fallen, seed);
            Point(p - d * size * .8f, -d, fallen, seed);
            for (int q = 0; q < 4; q++)
            {
                float a = q * Mathf.PI / 2f;
                var o = ax * Mathf.Cos(a) + ay * Mathf.Sin(a);
                Point(p + o * size, o, fallen, seed);
            }
            for (int q = 0; q < 4; q++)
            {
                int a = start + 2 + q, b = start + 2 + (q + 1) % 4;
                tris.Add(start); tris.Add(a); tris.Add(b);
                tris.Add(start + 1); tris.Add(b); tris.Add(a);
            }
        }

        void Point(Vector3 p, Vector3 n, float fallen, float seed)
        {
            verts.Add(p);
            normals.Add(n);
            uvs.Add(Vector2.zero);
            colors.Add(new Color(.5f, fallen, seed, 1f));
        }

        void Grid(int start, int rows, int cols)
        {
            int row = cols + 1;
            for (int k = 0; k < rows; k++)
                for (int q = 0; q < cols; q++)
                {
                    int i = start + k * row + q;
                    tris.Add(i); tris.Add(i + row + 1); tris.Add(i + row);
                    tris.Add(i); tris.Add(i + 1); tris.Add(i + row + 1);
                }
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
