using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The grand stair's random rolls (grand stair hall level design v0.3 §5 "무작위 굴림"): out of one of
    // four holes at the top of the stair comes a lying pawn statue (big: step aside) or a rolled carpet
    // (small: jump it) and rolls down its lane to the drain at the foot. Which lane, which kind and when
    // come from the section's seed (RollingSchedule: a roll every 1.6 ~ 2.6 s, two statues to three carpets,
    // the three rules that always leave a lane to step aside into). The hole glows 0.8 s before.
    //
    // A roll's place is a function of its event and ObstacleClock only - every PC draws it in the same place
    // and nothing is sent. Hits are the authority's (ObstacleClock.Authority: offline, or the host): a statue
    // knocks a pawn down and sends it down the stair (10 + up 3), a carpet staggers it (6 + up 1). The pawn is
    // pushed, the roll never is: the bodies are kinematic.
    //
    // Everything is in the section's frame: x across the stair, z up it (the roll goes -z), y up.
    public sealed partial class RollingSpawner : MonoBehaviour, ISectionPart
    {
        [Header("Lanes (section frame)")]
        [SerializeField] float[] laneX = { -6f, -2f, 2f, 6f };
        [SerializeField] float laneWidth = 3.6f;
        [Tooltip("Where the rolls come out: the holes' middle, z.")]
        [SerializeField] float holeZ = 51.5f;
        [Tooltip("Where they sink into the drain grate, z.")]
        [SerializeField] float drainZ = 13.5f;
        [Tooltip("The surface the rolls ride, as (z, y) corners from the top down: the stair's walking slope.")]
        [SerializeField] Vector2[] profile =
        {
            new Vector2(52f, 8f), new Vector2(51.5f, 8f), new Vector2(35.5f, 4f), new Vector2(29.5f, 4f),
            new Vector2(13.5f, 0f), new Vector2(10f, 0f),
        };
        [Tooltip("Stretches (z low, z high) of steps 1 m deep where the picture bumps down a step at a time.")]
        [SerializeField] Vector2[] stairs = { new Vector2(13.5f, 29.5f), new Vector2(35.5f, 51.5f) };
        [SerializeField] float speed = 4f;
        [Tooltip("The hole glows this long before the roll comes out.")]
        [SerializeField] float warning = .8f;
        [Tooltip("The roll rises out of its hole over the last part of the warning.")]
        [SerializeField] float emerge = .35f;

        [Header("Lively picture (R356: gravity, bounce, sway - the collider is unchanged)")]
        [SerializeField] float rollGravity = 14f;
        [Tooltip("How much of a landing's speed bounces back: stone little, a rolled carpet more.")]
        [SerializeField, Range(0f, .9f)] float statueRestitution = .28f, carpetRestitution = .45f;
        [Tooltip("The most a landing sways the roll, degrees.")]
        [SerializeField] float statueSway = 4f, carpetSway = 10f;

        [Header("Schedule")]
        [SerializeField] float minGap = 1.6f;
        [SerializeField] float maxGap = 2.6f;
        [Tooltip("Mixed into the section's seed: this spawner's own stream.")]
        [SerializeField] int salt = 1;

        [Header("Statue (big: step aside)")]
        [SerializeField] float statueDiameter = 1.6f;
        [SerializeField] float statueLength = 2.6f;
        [SerializeField] float statuePush = 10f, statueLift = 3f, statueKnockdown = 1.2f;

        [Header("Carpet roll (small: jump it)")]
        [SerializeField] float carpetDiameter = .5f;
        [SerializeField] float carpetLength = 3.4f;
        [SerializeField] float carpetPush = 6f, carpetLift = 1f, carpetKnockdown;

        [Header("Look")]
        [SerializeField] Material statueMaterial;
        [SerializeField] Material carpetMaterial;
        [SerializeField] Color holeGlow = new Color(1f, .55f, .12f, .85f);

        // Someone was hit (who, "석상" or "카펫"): the playtest counts it.
        public static event Action<GameObject, string> Hit;

        sealed class Item
        {
            public GameObject root;
            public Rigidbody body;
            public Transform picture;
            public bool statue, fresh;
            public RollingSchedule.Roll roll;     // R356: what PosePicture draws from
            public long serial;
            public readonly HashSet<ImportedObstacleActor> struck = new HashSet<ImportedObstacleActor>();
            public AudioSource rollLoop, rollHit;  // R531: its sounds (RollingSpawner.Sound.cs)
            public AudioLowPassFilter muffle;      // R548: muffles them on the runner
            public int nextHit;
        }

        RollingSchedule schedule;
        readonly Dictionary<long, Item> live = new Dictionary<long, Item>();
        readonly List<Item> freeStatues = new List<Item>(), freeCarpets = new List<Item>();
        readonly HashSet<long> seen = new HashSet<long>();
        readonly List<long> gone = new List<long>();
        readonly Collider[] overlaps = new Collider[64];
        GameObject[] glows;
        bool[] lit = new bool[0];

        public float Duration => (holeZ - (drainZ - 1f)) / Mathf.Max(.1f, speed);
        public RollingSchedule Schedule => schedule;

        public void Configure(Material statue, Material carpet)
        {
            statueMaterial = statue;
            carpetMaterial = carpet;
        }

        public void OnSection(PawnRushSection section)
        {
            var rules = new RollingSchedule.Rules { Lanes = laneX.Length, MinGap = minGap, MaxGap = maxGap };
            schedule = new RollingSchedule(SeededSchedule.Hash(section.Seed, salt), section.StartClock, rules);
            foreach (var item in live.Values) Release(item);
            live.Clear();
        }

        void Awake()
        {
            glows = new GameObject[laneX.Length];
            var glow = Arena.Glow(holeGlow);
            for (int i = 0; i < laneX.Length; i++)
            {
                float y = Profile(holeZ);
                glows[i] = Arena.Picture(transform, "Hole glow " + (i + 1),
                                         new Vector3(laneX[i] - laneWidth * .5f, y + .02f, holeZ - .5f),
                                         new Vector3(laneX[i] + laneWidth * .5f, y + .05f, holeZ + .5f), glow);
                glows[i].SetActive(false);
            }
        }

        void FixedUpdate()
        {
            if (schedule == null) return;
            double now = ObstacleClock.Now;
            float duration = Duration;
            schedule.Advance(now + warning + .1);
            schedule.Prune(now - duration - 2.0);

            seen.Clear();
            if (lit.Length != laneX.Length) lit = new bool[laneX.Length];
            Array.Clear(lit, 0, lit.Length);
            for (int i = 0; i < schedule.Count; i++)
            {
                var e = schedule[i];
                double age = now - e.Time;
                if (age < -warning) break;
                if (age < 0 && e.Lane < lit.Length)
                {
                    lit[e.Lane] = true;
                    // R574 (승규 11-10 "계단 장애물 나올 때 사운드 지워줘"): the hole no longer rumbles before a roll comes out.
                    // Warn(e, (float)age);   // R532: the hole rumbled as it glowed (RollingSpawner.Sound.cs)
                }
                if (age < -emerge || age > duration) continue;
                seen.Add(e.Serial);
                if (!live.TryGetValue(e.Serial, out var item))
                {
                    item = Take(e.Statue);
                    item.serial = e.Serial;
                    item.roll = e;
                    item.fresh = true;
                    item.nextHit = 0;
                    item.struck.Clear();
                    live[e.Serial] = item;
                }
                Pose(item, e, (float)age);
                if (age >= 0 && ObstacleClock.Authority) Strike(item);
            }
            for (int i = 0; i < glows.Length; i++)
                if (glows[i] != null && glows[i].activeSelf != lit[i]) glows[i].SetActive(lit[i]);

            gone.Clear();
            foreach (var kv in live) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (long serial in gone)
            {
                Release(live[serial]);
                live.Remove(serial);
            }
        }

        // Height of the riding surface at z (linear between the profile's corners).
        float Profile(float z)
        {
            if (profile == null || profile.Length == 0) return 0f;
            if (z >= profile[0].x) return profile[0].y;
            for (int i = 1; i < profile.Length; i++)
                if (z >= profile[i].x)
                {
                    var a = profile[i - 1];
                    var b = profile[i];
                    float t = Mathf.Approximately(a.x, b.x) ? 0f : (z - b.x) / (a.x - b.x);
                    return Mathf.Lerp(b.y, a.y, t);
                }
            return profile[profile.Length - 1].y;
        }

        void Pose(Item item, RollingSchedule.Roll e, float age)
        {
            float r = (item.statue ? statueDiameter : carpetDiameter) * .5f;
            float x = e.Lane < laneX.Length ? laneX[e.Lane] : 0f;
            float travelled = Mathf.Max(0f, age) * speed;
            float z = holeZ - travelled, y;
            if (age < 0f)
            {
                // Rising out of the hole.
                float u = Mathf.Clamp01(1f + age / Mathf.Max(.01f, emerge));
                y = Profile(holeZ) - r - .1f + u * (2f * r + .1f);
            }
            else
            {
                y = Profile(z) + r;
                if (z < drainZ) y -= (drainZ - z) * (2f * r + .3f);   // into the drain grate over its last metre
            }
            var world = transform.TransformPoint(new Vector3(x, y, z));
            if (item.fresh)
            {
                item.body.position = world;
                item.body.rotation = transform.rotation;
                item.root.transform.SetPositionAndRotation(world, transform.rotation);
                item.fresh = false;
            }
            else
            {
                item.body.MovePosition(world);
                item.body.MoveRotation(transform.rotation);
            }
            // The picture is posed every frame in LateUpdate (PosePicture); the collider keeps to the slope.
        }

        // ------------------------------------------------------------------ the lively picture (R356)

        // R356 (승규 11-04 "계단 내려갈 때 물리엔진을 더 생동감 있게 해"): the picture comes down the steps as a body
        // would - it rolls off each nosing, flies, lands on the tread below and bounces (gravity, restitution by kind),
        // never sinks into a step (its centre stays r clear of every tread and nosing), settles on the landing, and
        // every landing knocks it into a damped sway and tilt and a little sideways wander. The path of each kind is
        // simulated once, at a fixed step along the way, so it is the same on every PC; the sway's directions come from
        // the roll's serial. Posed every frame from the clock, so it is smooth between physics steps. The collider,
        // the timing and the hits are as before (the body keeps to the walking slope).
        const float PathStep = .01f;   // metres along the way between the path's samples

        sealed class Track
        {
            public float[] y;                                                // the centre's height, PathStep apart
            public readonly List<Vector2> hits = new List<Vector2>();        // (metres along, landing speed m/s)
        }

        Track statueTrack, carpetTrack;

        Track TrackFor(bool statue) => statue
            ? statueTrack ??= BuildTrack(statueDiameter * .5f, statueRestitution)
            : carpetTrack ??= BuildTrack(carpetDiameter * .5f, carpetRestitution);

        // The ground under z: the treads of the stair stretches (risers 1 m apart from half a metre in), else the profile.
        float Ground(float z)
        {
            foreach (var s in stairs)
            {
                if (z < s.x || z > s.y + .5f) continue;
                float first = s.x + .5f, y0 = Profile(s.x);
                if (z < first) return y0;
                int n = Mathf.Max(1, Mathf.RoundToInt(s.y - s.x));
                float rise = (Profile(s.y) - y0) / n;
                return y0 + rise * (Mathf.Min(Mathf.FloorToInt(z - first), n - 1) + 1);
            }
            return Profile(z);
        }

        // The lowest a round body of radius r can sit with its centre over z: r over the ground, and r clear of every
        // nosing (a tread's front edge) within reach.
        float Envelope(float z, float r)
        {
            float best = Ground(z) + r;
            foreach (var s in stairs)
            {
                float first = s.x + .5f, y0 = Profile(s.x);
                int n = Mathf.Max(1, Mathf.RoundToInt(s.y - s.x));
                float rise = (Profile(s.y) - y0) / n;
                int lo = Mathf.Max(0, Mathf.FloorToInt(z - r - first)), hi = Mathf.Min(n - 1, Mathf.CeilToInt(z + r - first));
                for (int i = lo; i <= hi; i++)
                {
                    float d = z - (first + i);
                    if (Mathf.Abs(d) < r) best = Mathf.Max(best, y0 + rise * (i + 1) + Mathf.Sqrt(r * r - d * d));
                }
            }
            return best;
        }

        // The way down at the rolls' speed: in the air under gravity, on the ground riding its envelope, a landing
        // faster than the ground's own fall bouncing back by the restitution (small ones just stick).
        Track BuildTrack(float r, float restitution)
        {
            float total = holeZ - (drainZ - 1f) + .5f;
            int n = Mathf.CeilToInt(total / PathStep) + 2;
            var track = new Track { y = new float[n] };
            float dt = PathStep / Mathf.Max(.1f, speed);
            float y = Envelope(holeZ, r), vy = 0f;
            for (int j = 0; j < n; j++)
            {
                float z = holeZ - j * PathStep, c = Envelope(z, r);
                if (y <= c)
                {
                    float surface = (Envelope(z - PathStep, r) - c) / dt;   // how fast the ground ahead drops (or climbs)
                    float hit = surface - vy;
                    if (hit > .35f)
                    {
                        if (hit > .8f) track.hits.Add(new Vector2(j * PathStep, hit));
                        vy = surface + restitution * hit;
                    }
                    else if (vy < surface) vy = surface;
                    y = c;
                }
                track.y[j] = y;
                vy -= rollGravity * dt;
                y += vy * dt;
            }
            return track;
        }

        static float Sample(Track track, float along)
        {
            float f = Mathf.Max(0f, along) / PathStep;
            int i = Mathf.Min((int)f, track.y.Length - 2);
            return Mathf.Lerp(track.y[i], track.y[i + 1], Mathf.Clamp01(f - i));
        }

        void LateUpdate()
        {
            if (live.Count == 0) return;
            double now = ObstacleClock.Now;
            foreach (var item in live.Values)
                if (item.root.activeSelf) PosePicture(item, (float)(now - item.roll.Time));
        }

        void PosePicture(Item item, float age)
        {
            var e = item.roll;
            float r = (item.statue ? statueDiameter : carpetDiameter) * .5f;
            float x = e.Lane < laneX.Length ? laneX[e.Lane] : 0f;
            float along = Mathf.Max(0f, age) * speed, z = holeZ - along, y;
            var track = TrackFor(item.statue);
            if (age < 0f)
            {
                // Out of the hole, easing out with a little pop at the top.
                float u = Mathf.Clamp01(1f + age / Mathf.Max(.01f, emerge));
                float eased = 1f - (1f - u) * (1f - u) + .18f * Mathf.Sin(Mathf.PI * u);
                y = Profile(holeZ) - r - .1f + eased * (2f * r + .1f);
            }
            else
            {
                y = Sample(track, along);
                if (z < drainZ) y -= (drainZ - z) * (2f * r + .3f);   // into the drain grate over its last metre
            }
            // Each landing's sway (about up), tilt (one end dipping) and sideways shove, from the serial.
            float yaw = 0f, tilt = 0f, drift = 0f;
            float swayMax = item.statue ? statueSway : carpetSway, swayPer = item.statue ? 1.2f : 2.6f;
            for (int i = 0; i < track.hits.Count; i++)
            {
                var h = track.hits[i];
                if (h.x > along) break;
                float since = (along - h.x) / Mathf.Max(.1f, speed);
                float a = Mathf.Min(swayMax, h.y * swayPer);
                drift += (Hash01(e.Serial, i, 3) - .5f) * .05f * h.y * (1f - Mathf.Exp(-since / .35f));
                if (since > 2.5f) continue;
                float fade = Mathf.Exp(-since / .45f);
                yaw += (Hash01(e.Serial, i, 1) < .5f ? -a : a) * fade * Mathf.Sin(2f * Mathf.PI * 2.2f * since);
                tilt += (Hash01(e.Serial, i, 2) < .5f ? -a : a) * .7f * fade * Mathf.Sin(2f * Mathf.PI * 3.1f * since);
            }
            float room = Mathf.Max(0f, laneWidth * .5f - (item.statue ? statueLength : carpetLength) * .5f);
            drift = Mathf.Clamp(drift, -room, room);
            float angle = along / Mathf.Max(.05f, r) * Mathf.Rad2Deg;          // rolling: a turn per 2 pi r
            item.picture.SetPositionAndRotation(transform.TransformPoint(new Vector3(x + drift, y, z)),
                transform.rotation * Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(tilt, Vector3.forward)
                * Quaternion.AngleAxis(-angle, Vector3.right));
        }

        static float Hash01(long serial, int i, int salt)
        {
            uint h = (uint)(serial * 73856093L) ^ (uint)(i * 19349663) ^ (uint)(salt * 83492791);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }

        void Strike(Item item)
        {
            float r = (item.statue ? statueDiameter : carpetDiameter) * .5f;
            float half = Mathf.Max(0f, (item.statue ? statueLength : carpetLength) * .5f - r);
            var center = item.body.position;
            var axis = transform.right * half;
            int n = Physics.OverlapCapsuleNonAlloc(center - axis, center + axis, r + .12f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor == null || !actor.CanReceiveObstacle || !item.struck.Add(actor)) continue;
                // Down the stair (the way the roll goes), and up a little.
                var push = -transform.forward * (item.statue ? statuePush : carpetPush) + Vector3.up * (item.statue ? statueLift : carpetLift);
                actor.AddObstacleHit(push, item.statue ? statueKnockdown : carpetKnockdown);
                Hit?.Invoke(actor.gameObject, item.statue ? "석상" : "카펫");
            }
        }

        Item Take(bool statue)
        {
            var free = statue ? freeStatues : freeCarpets;
            Item item;
            if (free.Count > 0)
            {
                item = free[free.Count - 1];
                free.RemoveAt(free.Count - 1);
            }
            else item = statue ? MakeStatue() : MakeCarpet();
            item.root.SetActive(true);
            return item;
        }

        void Release(Item item)
        {
            if (item == null || item.root == null) return;
            item.root.SetActive(false);
            if (item.rollLoop != null) item.rollLoop.volume = 0f;
            (item.statue ? freeStatues : freeCarpets).Add(item);
        }

        // ------------------------------------------------------------------ the rolls (made once, then reused)

        Item MakeBody(string name, bool statue, float diameter, float length)
        {
            var root = new GameObject(name);
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 0;   // along x, across the lane
            capsule.radius = diameter * .5f;
            capsule.height = Mathf.Max(length, diameter);
            // Rolled, not climbed.
            root.AddComponent<NoClimbSurface>();
            var picture = new GameObject("Picture").transform;
            picture.SetParent(root.transform, false);
            return new Item { root = root, body = body, picture = picture, statue = statue };
        }

        Item MakeStatue()
        {
            var item = MakeBody("Rolling statue", true, statueDiameter, statueLength);
            // R354 (승규 11-04, a picture of a faceted white marble pawn with gold rings): the modelled pawn
            // (pawn_roll_v47.py, 2.6 m, base end to -x as below), else the primitives.
            if (ModelPicture(item, Course01v4Builder.RollingStatueModel, "Rolling statue model")) return item;
            var mat = statueMaterial != null ? statueMaterial : Arena.Lit("M_RollingStatue", new Color(.95f, .55f, .2f));
            float l = statueLength, d = statueDiameter;
            // A pawn lying across the lane: base, body, collar, head, knob (base end to -x).
            Part(item.picture, PrimitiveType.Cylinder, new Vector3(-l * .5f + .15f, 0f, 0f), d, .3f, mat);
            Part(item.picture, PrimitiveType.Cylinder, new Vector3(-l * .5f + .85f, 0f, 0f), d * .72f, 1.1f, mat);
            Part(item.picture, PrimitiveType.Cylinder, new Vector3(-l * .5f + 1.5f, 0f, 0f), d * .88f, .18f, mat);
            Part(item.picture, PrimitiveType.Sphere, new Vector3(l * .5f - .55f, 0f, 0f), d * .62f, d * .62f, mat);
            Part(item.picture, PrimitiveType.Sphere, new Vector3(l * .5f - .12f, 0f, 0f), .26f, .26f, mat);
            return item;
        }

        Item MakeCarpet()
        {
            var item = MakeBody("Rolling carpet", false, carpetDiameter, carpetLength);
            // R353 (승규 11-04 "계단 장애물 모델링 … 일단 1개만"): the modelled roll (carpet_roll_v46.py: 3.2 m of rolled
            // carpet, r 0.24, gold borders, spiral ends, gold hubs - inside the 0.5 x 3.4 capsule), else the primitives.
            if (ModelPicture(item, Course01v4Builder.RollingCarpetModel, "Rolling carpet model")) return item;
            var mat = carpetMaterial != null ? carpetMaterial : Arena.Lit("M_RollingCarpet", new Color(.72f, .1f, .14f));
            Part(item.picture, PrimitiveType.Cylinder, Vector3.zero, carpetDiameter, carpetLength, mat);
            // Gold bands so the roll can be seen turning.
            var gold = Arena.TrimGold;
            foreach (float x in new[] { -1.2f, 0f, 1.2f })
                Part(item.picture, PrimitiveType.Cylinder, new Vector3(x, 0f, 0f), carpetDiameter * 1.04f, .12f, gold);
            Part(item.picture, PrimitiveType.Cube, new Vector3(0f, carpetDiameter * .45f, 0f), .06f, carpetLength * .96f, gold);
            return item;
        }

        // R353 / R354: a modelled roll (Resources) as the item's picture, its materials the grand stair hall's; false when
        // the model is missing (the primitives draw instead).
        static bool ModelPicture(Item item, string resource, string name)
        {
            var model = Resources.Load<GameObject>(resource);
            if (model == null) return false;
            var view = Instantiate(model, item.picture, false);
            view.name = name;
            foreach (var c in view.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = Course01v4Builder.RollMaterial(slots[i] != null ? slots[i].name : "") ?? slots[i];
                r.sharedMaterials = slots;
            }
            return true;
        }

        // A primitive picture lying along x (no collider): `size` across, `length` along.
        static void Part(Transform parent, PrimitiveType type, Vector3 at, float size, float length, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            if (type == PrimitiveType.Cylinder)
            {
                go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                go.transform.localScale = new Vector3(size, length * .5f, size);
            }
            else go.transform.localScale = new Vector3(length, size, size);
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        void OnDisable()
        {
            foreach (var item in live.Values) Release(item);
            live.Clear();
            if (glows != null)
                foreach (var g in glows) if (g != null) g.SetActive(false);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, .55f, .1f, .8f);
            foreach (float x in laneX)
                for (int i = 1; i < profile.Length; i++)
                    Gizmos.DrawLine(new Vector3(x, profile[i - 1].y + .8f, profile[i - 1].x), new Vector3(x, profile[i].y + .8f, profile[i].x));
        }
    }
}
