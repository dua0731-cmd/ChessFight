using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The central landing's falling chandeliers (grand stair hall level design v0.3 §5 "샹들리에 낙하",
    // after 10 falling chess piece): every 0.7 ~ 1.4 s a chandelier falls somewhere random on the landing.
    // Its shadow on the floor darkens for 1.2 s while it sways overhead, then it falls in 0.4 s. Where and
    // when come from the section's seed (ChandelierSchedule: never on the king's base, never within 6 m of a
    // drop still warning or just fallen, so a way between the shadows is always open).
    //
    // Drawn from the event and ObstacleClock only - the same on every PC, nothing sent. Hits are the
    // authority's (ObstacleClock.Authority): inside 2.5 m a pawn is knocked flat (12 + up 4), out to 4.5 m it
    // staggers (7 + up 2). The king statue shields what is behind it, like the falling piece's cover. The
    // broken pieces lie 1.5 s and touch nothing.
    public sealed class ChandelierRain : MonoBehaviour, ISectionPart
    {
        [Header("Where (section frame)")]
        [SerializeField] Vector2 areaMin = new Vector2(-13f, 54f);
        [SerializeField] Vector2 areaMax = new Vector2(13f, 65f);
        [Tooltip("Left out: the king statue's base.")]
        [SerializeField] Vector2 keepOutMin = new Vector2(-2f, 61f);
        [SerializeField] Vector2 keepOutMax = new Vector2(2f, 65f);
        [SerializeField] float floorY = 8f;

        [Header("When")]
        [SerializeField] float minGap = .7f;
        [SerializeField] float maxGap = 1.4f;
        [SerializeField] float minDistance = 6f;
        [SerializeField] float warning = 1.2f;
        [SerializeField] float fall = .4f;
        [SerializeField] float debris = 1.5f;
        [Tooltip("How high over the floor it hangs while it warns.")]
        [SerializeField] float hang = 10f;
        [Tooltip("Mixed into the section's seed: this rain's own stream.")]
        [SerializeField] int salt = 2;

        [Header("Hits")]
        [SerializeField] float innerRadius = 2.5f;
        [SerializeField] float outerRadius = 4.5f;
        [SerializeField] float innerPush = 12f, innerLift = 4f, innerKnockdown = 1f;
        [SerializeField] float outerPush = 7f, outerLift = 2f;
        [SerializeField] float heightTolerance = 2f;

        [Header("Look")]
        [SerializeField] Material chandelierMaterial;

        // Someone was hit (who, "샹들리에" or "샹들리에 고리"): the playtest counts it.
        public static event Action<GameObject, string> Hit;

        sealed class View
        {
            public GameObject root, chandelier, chain, shadow, ring;
            public Transform[] pieces;
            public Material shadowMaterial;
            public long serial;
        }

        ChandelierSchedule schedule;
        double lastClock = double.NaN;
        readonly Dictionary<long, View> live = new Dictionary<long, View>();
        readonly List<View> free = new List<View>();
        readonly HashSet<long> seen = new HashSet<long>();
        readonly List<long> gone = new List<long>();
        readonly Collider[] overlaps = new Collider[64];
        readonly RaycastHit[] rays = new RaycastHit[32];

        public ChandelierSchedule Schedule => schedule;
        float Busy => warning + fall + .6f;

        public void Configure(Material chandelier) => chandelierMaterial = chandelier;

        // The landing it falls on and how often (the builder: a wider landing falls as often per square metre).
        public void Configure(Material chandelier, Vector2 min, Vector2 max, float gapMin, float gapMax)
        {
            chandelierMaterial = chandelier;
            areaMin = min;
            areaMax = max;
            minGap = gapMin;
            maxGap = gapMax;
        }

        public void OnSection(PawnRushSection section)
        {
            var rules = new ChandelierSchedule.Rules
            {
                MinGap = minGap, MaxGap = maxGap,
                XMin = areaMin.x, XMax = areaMax.x, ZMin = areaMin.y, ZMax = areaMax.y,
                KeepOutX0 = keepOutMin.x, KeepOutX1 = keepOutMax.x, KeepOutZ0 = keepOutMin.y, KeepOutZ1 = keepOutMax.y,
                MinDistance = minDistance, Busy = Busy,
            };
            schedule = new ChandelierSchedule(SeededSchedule.Hash(section.Seed, salt), section.StartClock, rules);
            lastClock = double.NaN;
            foreach (var view in live.Values) Release(view);
            live.Clear();
        }

        void FixedUpdate()
        {
            if (schedule == null) return;
            double now = ObstacleClock.Now;
            float life = warning + fall + debris;
            schedule.Advance(now + .1);
            schedule.Prune(now - life - 4.0);

            seen.Clear();
            for (int i = 0; i < schedule.Count; i++)
            {
                var d = schedule[i];
                double age = now - d.Time;
                if (age < 0) break;
                if (age > life) continue;
                seen.Add(d.Serial);
                if (!live.TryGetValue(d.Serial, out var view))
                {
                    view = Take();
                    view.serial = d.Serial;
                    live[d.Serial] = view;
                }
                Draw(view, d, (float)age);
                // The hit, once, the step the impact time is crossed (never for impacts before this PC started).
                double impact = d.Time + warning + fall;
                if (ObstacleClock.Authority && !double.IsNaN(lastClock) && impact > lastClock && impact <= now) Strike(d);
            }
            lastClock = now;

            gone.Clear();
            foreach (var kv in live) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (long serial in gone)
            {
                Release(live[serial]);
                live.Remove(serial);
            }
        }

        Vector3 Center(ChandelierSchedule.Drop d) => new Vector3((float)d.X, floorY, (float)d.Z);

        void Draw(View view, ChandelierSchedule.Drop d, float age)
        {
            var center = Center(d);
            view.root.transform.localPosition = center;
            bool falling = age >= warning && age < warning + fall;
            bool broken = age >= warning + fall;
            // Shadow and the outer ring: darkening through the warning, gone once it has fallen.
            float dark = broken ? 0f : Mathf.Clamp01(age / warning);
            view.shadow.SetActive(!broken);
            view.ring.SetActive(!broken);
            view.shadowMaterial.color = new Color(.05f, .03f, .08f, .15f + .6f * dark);
            // The chandelier: swaying and creaking overhead, then down.
            view.chandelier.SetActive(!broken);
            view.chain.SetActive(!falling && !broken);
            if (!broken)
            {
                float u = falling ? Mathf.Clamp01((age - warning) / fall) : 0f;
                float height = hang * (1f - u * u) + .6f;
                float sway = falling ? 0f : Mathf.Sin(age * 9f) * (4f + 10f * dark);
                view.chandelier.transform.localPosition = Vector3.up * height;
                view.chandelier.transform.localRotation = Quaternion.Euler(sway, age * 20f, sway * .6f);
            }
            // The pieces: out from the impact and down, then still.
            for (int i = 0; i < view.pieces.Length; i++)
            {
                var piece = view.pieces[i];
                piece.gameObject.SetActive(broken);
                if (!broken) continue;
                float t = Mathf.Min(age - warning - fall, .45f);
                float a = (i * 137.5f + d.Serial * 31f) * Mathf.Deg2Rad;
                float reach = 1.2f + (i % 3) * .7f;
                var outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float rise = Mathf.Max(0f, 2.2f * t - 5f * t * t);
                piece.localPosition = outward * (reach * Mathf.Clamp01(t / .45f)) + Vector3.up * (.12f + rise);
                piece.localRotation = Quaternion.Euler(i * 40f + t * 400f, i * 70f, t * 250f);
            }
        }

        void Strike(ChandelierSchedule.Drop d)
        {
            var center = transform.TransformPoint(Center(d));
            int n = Physics.OverlapSphereNonAlloc(center, outerRadius + 1f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            var struck = new HashSet<ImportedObstacleActor>();
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor == null || !actor.CanReceiveObstacle || !struck.Add(actor)) continue;
                var delta = actor.Position - center;
                if (Mathf.Abs(delta.y) > heightTolerance) continue;
                var flat = Vector3.ProjectOnPlane(delta, Vector3.up);
                float distance = flat.magnitude;
                if (distance > outerRadius || Shielded(center, actor)) continue;
                var outward = distance > .05f ? flat / distance : transform.forward;
                bool inner = distance <= innerRadius;
                actor.AddObstacleHit(outward * (inner ? innerPush : outerPush) + Vector3.up * (inner ? innerLift : outerLift),
                                     inner ? innerKnockdown : 0f);
                Hit?.Invoke(actor.gameObject, inner ? "샹들리에" : "샹들리에 고리");
            }
        }

        // Something solid between the impact and the pawn (the king statue) that is neither a pawn nor a moving obstacle.
        bool Shielded(Vector3 center, ImportedObstacleActor actor)
        {
            var from = center + Vector3.up;
            var to = actor.Position + Vector3.up * .5f;
            var dir = to - from;
            float length = dir.magnitude;
            if (length < .05f) return false;
            int n = Physics.RaycastNonAlloc(from, dir / length, rays, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = rays[i].collider;
                if (c.GetComponentInParent<ICharacterDriver>() != null || c.attachedRigidbody != null) continue;
                if (c.GetComponent<CourseFloor>() != null) continue;
                return true;
            }
            return false;
        }

        View Take()
        {
            View view;
            if (free.Count > 0)
            {
                view = free[free.Count - 1];
                free.RemoveAt(free.Count - 1);
            }
            else view = Make();
            view.root.SetActive(true);
            return view;
        }

        void Release(View view)
        {
            if (view == null || view.root == null) return;
            view.root.SetActive(false);
            free.Add(view);
        }

        // ------------------------------------------------------------------ the chandelier, its shadow and its pieces

        View Make()
        {
            var root = new GameObject("Falling chandelier");
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            var gold = chandelierMaterial != null ? chandelierMaterial : Arena.TrimGold;
            var candle = Arena.Lit("M_Candle", new Color(.98f, .95f, .86f), .2f, .5f);
            var flame = Arena.Glow(new Color(1f, .78f, .3f, .95f));

            var chandelier = new GameObject("Chandelier");
            chandelier.transform.SetParent(root.transform, false);
            var c = chandelier.transform;
            Shape(c, PrimitiveType.Cylinder, Vector3.zero, new Vector3(2.4f, .07f, 2.4f), gold);          // ring
            Shape(c, PrimitiveType.Cylinder, Vector3.up * .45f, new Vector3(.22f, .5f, .22f), gold);       // column
            Shape(c, PrimitiveType.Sphere, Vector3.up * -.25f, new Vector3(.45f, .45f, .45f), gold);       // finial
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var at = new Vector3(Mathf.Sin(a) * 1.1f, 0f, Mathf.Cos(a) * 1.1f);
                Shape(c, PrimitiveType.Cylinder, at + Vector3.up * .2f, new Vector3(.1f, .16f, .1f), candle);
                Shape(c, PrimitiveType.Sphere, at + Vector3.up * .42f, new Vector3(.1f, .14f, .1f), flame);
            }
            // The chain up into the dark (hidden while it falls).
            var chain = Shape(root.transform, PrimitiveType.Cylinder, Vector3.up * (hang + 6f), new Vector3(.08f, 5.4f, .08f), gold);

            // Shadow (darkening) and the outer ring's edge (a stagger out to 4.5 m), each with its own colour.
            var shadowMaterial = new Material(Arena.Glow(new Color(.05f, .03f, .08f, .2f)));
            var shadow = Shape(root.transform, PrimitiveType.Cylinder, Vector3.up * .03f,
                               new Vector3(innerRadius * 2f, .01f, innerRadius * 2f), shadowMaterial);
            var ring = Shape(root.transform, PrimitiveType.Cylinder, Vector3.up * .02f,
                             new Vector3(outerRadius * 2f, .005f, outerRadius * 2f), Arena.Glow(new Color(.05f, .03f, .08f, .16f)));

            var pieces = new Transform[7];
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = Shape(root.transform, i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Cylinder, Vector3.zero,
                                  new Vector3(.35f + .1f * (i % 3), .08f, .18f), i % 3 == 0 ? candle : gold);
                piece.SetActive(false);
                pieces[i] = piece.transform;
            }
            return new View { root = root, chandelier = chandelier, chain = chain, shadow = shadow, ring = ring, pieces = pieces, shadowMaterial = shadowMaterial };
        }

        static GameObject Shape(Transform parent, PrimitiveType type, Vector3 at, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        void OnDisable()
        {
            foreach (var view in live.Values) Release(view);
            live.Clear();
            lastClock = double.NaN;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, .8f, .2f, .8f);
            var mid = new Vector3((areaMin.x + areaMax.x) * .5f, floorY + .1f, (areaMin.y + areaMax.y) * .5f);
            Gizmos.DrawWireCube(mid, new Vector3(areaMax.x - areaMin.x, .2f, areaMax.y - areaMin.y));
        }
    }
}
