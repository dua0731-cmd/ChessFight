using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R486 (승규 11-06, a picture of the platform lake: "총 3개가 나올 수 있게 하고 각자 택스쳐가 다르게 해줘 중반 구간 물
    // 쪽 전체를 누릴 수 있게 해주고 물이랑 상호 작용 시켜"; R485's GardenWatergateBoat grown); R487 ("2개씩 나와야 하고 텀이
    // 길어 넓은 쪽으로 와야 해", "총 6척이 아니라 4척인데 그 주기 가 짧고 플레이어들이 볼 수 있어야 돼"): the lake's four
    // royal boats (GardenWatergate.fbx's "Lake boat south 1 / 2, north 1 / 2", watergate_sample_v1.py, each in its own
    // colours), two on each of two short routes in the wide waters the way runs beside, half a round apart. The lake
    // (-0.55) is waters no boat passes between - the platforms stand 1.2 m apart from the west landing to the east one,
    // the shortcut's posts 2.2 .. 2.5 m apart from P4 to the dyke. Each route is closed, checked against everything in
    // its water with a margin of 0.35 m and keeps its own lanes 5.7 m or more apart, so its two boats never meet
    // (lake_routes.py):
    //   0 south  west along the south lake under the platforms, round its west end, back east by the canal walk and
    //            the weirs, round its east end south of the east landing (117 m, 95 s round: a boat every 48 s) -
    //            seen from the canal walk, the ramp and the platforms
    //   1 north  north of the platforms east of the island: along the reservoir wall, down by the shortcut, back west
    //            past P4 and over the island's north (87 m, 71 s: a boat every 36 s) - seen from the platforms and
    //            the shortcut
    // Each goes 1.3 m/s on the straights and down to 0.65 in its tightest turns (eased over 2 m), floats as it goes -
    // rolling, pitching and heaving on the swell, heeling a little out of the turns - and heads along its route. A pure
    // function of the shared obstacle clock (HANDOFF rule 7), so every PC sees them in the same places; no collider
    // (nobody reaches the lake's open water but by falling in). Its hull goes to the water shader with the reservoir's
    // boats (GardenBoatWater: bow wave, Kelvin wake, rings, water heaped at the hull). This moves the pivot it rides
    // (section frame, its rest on its route with the bow north, as the FBX left the boat).
    [DefaultExecutionOrder(90)]
    public sealed class GardenLakeBoat : MonoBehaviour
    {
        // the boats: their FBX names (after "Lake boat "), the route each sails and its half (0 or 1) of the round
        public static readonly string[] Names = { "south 1", "south 2", "north 1", "north 2" };
        static readonly int[] RouteOf = { 0, 0, 1, 1 }, HalfOf = { 0, 1, 0, 1 };
        public const float HalfLength = 2.25f, HalfWidth = .9f;          // the royal boat at 0.75, for the water
        const float Fast = 1.3f, Slow = .65f;                            // m/s on the straights, in the tightest turn
        const float SlowRadius = 1.5f, FastRadius = 6f;                  // the turn radii those speeds hold at
        const float Roll = 1.6f, Pitch = .8f, Heave = .04f;              // degrees, degrees, metres (as GardenBoatRock)
        const float RollPeriod = 3.4f, PitchPeriod = 4.6f, HeavePeriod = 3.1f;
        const float TurnHeel = 1.8f;                                     // degrees at a 2.9 m turn

        // The routes' control points (section x, z), closed Catmull-Rom splines (lake_routes.py prints them); boat 1
        // rests at its route's first, boat 2 at its middle one.
        static readonly Vector2[][] Routes =
        {
            new[]
            {
                new Vector2(26.00f, -3.90f), new Vector2(14.00f, -3.80f), new Vector2(2.00f, -4.00f),
                new Vector2(-10.00f, -3.90f), new Vector2(-17.00f, -4.00f), new Vector2(-21.40f, -4.90f),
                new Vector2(-23.20f, -7.20f), new Vector2(-21.40f, -9.60f), new Vector2(-14.00f, -10.00f),
                new Vector2(-2.00f, -9.90f), new Vector2(10.00f, -11.00f), new Vector2(22.00f, -11.20f),
                new Vector2(29.50f, -10.20f), new Vector2(32.20f, -7.20f), new Vector2(30.60f, -4.60f),
            },
            new[]
            {
                new Vector2(-25.00f, 29.70f), new Vector2(-12.00f, 29.60f), new Vector2(-2.00f, 29.20f),
                new Vector2(2.60f, 26.60f), new Vector2(4.00f, 22.00f), new Vector2(3.60f, 17.00f),
                new Vector2(1.00f, 13.00f), new Vector2(-4.00f, 12.20f), new Vector2(-8.20f, 14.60f),
                new Vector2(-9.60f, 19.60f), new Vector2(-14.00f, 23.40f), new Vector2(-21.00f, 23.80f),
                new Vector2(-26.40f, 24.30f), new Vector2(-29.20f, 25.90f), new Vector2(-28.90f, 28.60f),
            },
        };

        // The boats sailing now, for the water shader (GardenBoatWater).
        public static readonly List<GardenLakeBoat> All = new List<GardenLakeBoat>();

        [SerializeField] int boat;
        [Tooltip("The lake's surface in the section's frame (RoyalWaterGardenBuilder.MiddleWaterY).")]
        [SerializeField] float waterY = -.55f;

        Vector2[] points;        // the route sampled
        float[] along;           // distance from the start at each sample
        float[] clock;           // time from the start at each sample (slower through the turns)
        float[] pace;            // the speed at each sample
        int samples;
        float length, period;

        // For the water: where its waterline's middle is, which way it heads (world), how fast it goes, how fast it rocks.
        public Vector3 Waterline { get; private set; }
        public Vector3 Heading { get; private set; }
        public float Speed { get; private set; }
        public float RockRate { get; private set; }

        int route => RouteOf[Mathf.Clamp(boat, 0, Names.Length - 1)];
        int half => HalfOf[Mathf.Clamp(boat, 0, Names.Length - 1)];

        // Where boat `boat`'s pivot rests (on its route, as the FBX left it), section frame.
        public static Vector3 Rest(int boat, float waterY)
        {
            var R = Routes[RouteOf[boat]];
            var at = R[HalfOf[boat] == 0 ? 0 : R.Length / 2];
            return new Vector3(at.x, waterY, at.y);
        }

        public void Configure(int boat, float waterY)
        {
            this.boat = boat;
            this.waterY = waterY;
            points = null;
            Build();
        }

        void OnEnable()
        {
            Build();
            if (!All.Contains(this)) All.Add(this);
        }

        void OnDisable() => All.Remove(this);

        void Build()
        {
            if (points != null) return;
            var R = Routes[route];
            int n = R.Length;
            // sample each span finely, then measure
            var raw = new List<Vector2>();
            for (int k = 0; k < n; k++)
                for (int i = 0; i < 60; i++)
                    raw.Add(CatmullRom(R[(k + n - 1) % n], R[k], R[(k + 1) % n], R[(k + 2) % n], i / 60f));
            samples = raw.Count;
            points = new Vector2[samples + 1];
            along = new float[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                points[i] = raw[i % samples];
                if (i > 0) along[i] = along[i - 1] + Vector2.Distance(points[i - 1], points[i]);
            }
            length = along[samples];
            // its speed at each sample from the turn's radius there (over 0.8 m either way), eased over 2 m
            float ds = length / samples;
            var speed = new float[samples];
            int reach = Mathf.Max(1, Mathf.RoundToInt(.8f / ds));
            for (int i = 0; i < samples; i++)
            {
                var a = points[Wrap(i - reach)];
                var b = points[i];
                var c = points[Wrap(i + reach)];
                float turn = Mathf.Abs(Vector2.SignedAngle(b - a, c - b)) * Mathf.Deg2Rad;
                float radius = turn > 1e-4f ? ((b - a).magnitude + (c - b).magnitude) * .5f / turn : 1e4f;
                speed[i] = Mathf.Lerp(Slow, Fast, Mathf.InverseLerp(SlowRadius, FastRadius, radius));
            }
            pace = new float[samples + 1];
            int ease = Mathf.Max(1, Mathf.RoundToInt(2f / ds));
            for (int i = 0; i < samples; i++)
            {
                float sum = 0f;
                for (int j = -ease; j <= ease; j++) sum += speed[Wrap(i + j)];
                pace[i] = sum / (2 * ease + 1);
            }
            pace[samples] = pace[0];
            clock = new float[samples + 1];
            for (int i = 1; i <= samples; i++)
                clock[i] = clock[i - 1] + (along[i] - along[i - 1]) * 2f / (pace[i - 1] + pace[i]);
            period = clock[samples];
        }

        int Wrap(int i) => ((i % samples) + samples) % samples;

        static Vector2 CatmullRom(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return .5f * (2f * b + (c - a) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (3f * b - a - 3f * c + d) * t3);
        }

        // The sample before time t (from the start) and how far on to the next.
        int AtTime(float t, out float f)
        {
            int lo = 0, hi = samples;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (clock[mid] <= t) lo = mid; else hi = mid;
            }
            float span = clock[hi] - clock[lo];
            f = span > 1e-5f ? (t - clock[lo]) / span : 0f;
            return lo;
        }

        // The route's point at distance s along it (wrapping).
        Vector2 At(float s)
        {
            s = Mathf.Repeat(s, length);
            int lo = 0, hi = samples;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (along[mid] <= s) lo = mid; else hi = mid;
            }
            float span = along[hi] - along[lo];
            return Vector2.Lerp(points[lo], points[hi], span > 1e-5f ? (s - along[lo]) / span : 0f);
        }

        void LateUpdate()
        {
            Build();
            double now = ObstacleClock.Now;
            // the second boat on a route half a round after the first
            int i = AtTime((float)((now + half * period * .5) % period), out float f);
            float s = Mathf.Lerp(along[i], along[i + 1], f);
            var p = At(s);
            var back = At(s - .8f);
            var ahead = At(s + .8f);
            var dir = ahead - back;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.up;
            // its heel out of the turn: the heading's change over the 1.6 m, as a share of a 2.9 m turn's, signed
            float turn = Vector2.SignedAngle(p - back, ahead - p) * Mathf.Deg2Rad / 1.6f * 2.9f;
            // each boat on its own swell (its number shifts the phases)
            float t = (float)(now % 3600.0) + boat * 1.37f;
            float wr = 2f * Mathf.PI / RollPeriod, wp = 2f * Mathf.PI / PitchPeriod, wh = 2f * Mathf.PI / HeavePeriod;
            float roll = Roll * (Mathf.Sin(wr * t) + .3f * Mathf.Sin(2.3f * wr * t + 1.1f)) - TurnHeel * Mathf.Clamp(turn, -1.3f, 1.3f);
            float pitch = Pitch * Mathf.Sin(wp * t + .7f);
            float heave = Heave * Mathf.Sin(wh * t + 2.1f);
            transform.localPosition = new Vector3(p.x, waterY + heave, p.y);
            // heading along the route (+z its bow at rest), then pitch about its beam (x) and roll about its length (z)
            var heading = new Vector3(dir.x, 0f, dir.y);
            transform.localRotation = Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(pitch, 0f, roll);

            var frame = transform.parent;
            Waterline = frame != null ? frame.TransformPoint(p.x, waterY, p.y) : new Vector3(p.x, waterY, p.y);
            Heading = (frame != null ? frame.TransformDirection(heading) : heading).normalized;
            Speed = Mathf.Lerp(pace[i], pace[i + 1], f);
            RockRate = Mathf.Deg2Rad * (Mathf.Abs(Roll * wr * Mathf.Cos(wr * t)) + Mathf.Abs(Pitch * wp * Mathf.Cos(wp * t + .7f)));
        }
    }
}
