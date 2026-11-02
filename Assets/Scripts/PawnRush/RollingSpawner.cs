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
    public sealed class RollingSpawner : MonoBehaviour, ISectionPart
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
        [SerializeField] float bounce = .15f;

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
            public long serial;
            public readonly HashSet<ImportedObstacleActor> struck = new HashSet<ImportedObstacleActor>();
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
                if (age < 0 && e.Lane < lit.Length) lit[e.Lane] = true;
                if (age < -emerge || age > duration) continue;
                seen.Add(e.Serial);
                if (!live.TryGetValue(e.Serial, out var item))
                {
                    item = Take(e.Statue);
                    item.serial = e.Serial;
                    item.fresh = true;
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

        bool OnStairs(float z)
        {
            foreach (var s in stairs)
                if (z >= s.x && z <= s.y) return true;
            return false;
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
            // The picture rolls (a turn per 2 pi r) and bumps a step at a time; the collider does neither.
            float angle = travelled / Mathf.Max(.05f, r) * Mathf.Rad2Deg;
            float hop = OnStairs(z) ? bounce * Mathf.Abs(Mathf.Sin(Mathf.PI * (z - Mathf.Floor(z)))) : 0f;
            item.picture.localPosition = Vector3.up * hop;
            item.picture.localRotation = Quaternion.AngleAxis(-angle, Vector3.right);
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
            var mat = carpetMaterial != null ? carpetMaterial : Arena.Lit("M_RollingCarpet", new Color(.72f, .1f, .14f));
            Part(item.picture, PrimitiveType.Cylinder, Vector3.zero, carpetDiameter, carpetLength, mat);
            // Gold bands so the roll can be seen turning.
            var gold = Arena.TrimGold;
            foreach (float x in new[] { -1.2f, 0f, 1.2f })
                Part(item.picture, PrimitiveType.Cylinder, new Vector3(x, 0f, 0f), carpetDiameter * 1.04f, .12f, gold);
            Part(item.picture, PrimitiveType.Cube, new Vector3(0f, carpetDiameter * .45f, 0f), .06f, carpetLength * .96f, gold);
            return item;
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
