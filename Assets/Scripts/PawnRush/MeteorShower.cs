using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Observatory's meteor shower (R720, obstacle plan v0.1 #2): meteors fall on the constellation lines -
    // the 3 m bridges between the star platforms - one about every `interval` seconds. A gold ring shows where for
    // `warning` seconds, a streak comes down in the last of them, and it lands: inside `innerRadius` a pawn is
    // knocked flat, out to `outerRadius` it is shoved. Never on a star platform (the lines' ends are left out), never
    // twice running on the same line.
    //
    // Which line, where along it and when are a pure function of the section's seed and the meteor's number: the
    // nth meteor is the same on every PC, whenever it joined, and nothing is sent. Drawn from that and ObstacleClock
    // only. Hits are the authority's (ObstacleClock.Authority), as the grand stair hall's chandeliers'.
    public sealed class MeteorShower : MonoBehaviour, ISectionPart
    {
        [Serializable]
        public struct Line
        {
            public Vector3 from, to;   // the bridge's top, end to end, in this object's frame

            public Line(Vector3 from, Vector3 to)
            {
                this.from = from;
                this.to = to;
            }
        }

        public struct Meteor
        {
            public long Serial;   // 0, 1, 2 ... from the section's start
            public double Time;   // when it lands, on the shared obstacle clock
            public int Line;
            public float Along;   // 0..1 of the line's usable length
        }

        [SerializeField] Line[] lines = new Line[0];
        [Tooltip("Seconds from one meteor to the next, on average.")]
        [SerializeField] float interval = 1.25f;
        [SerializeField] float warning = 1f;
        [Tooltip("The last of the warning, when the streak comes down.")]
        [SerializeField] float streak = .4f;
        [SerializeField] float flash = .3f;
        [Tooltip("A meteor's middle stays this far from a line's ends (the star platforms are the rests).")]
        [SerializeField] float endMargin = 1.5f;
        [Tooltip("Mixed into the section's seed: this shower's own stream.")]
        [SerializeField] int salt = 31;

        [Header("Hits")]
        [SerializeField] float innerRadius = 1.4f;
        [SerializeField] float outerRadius = 2.2f;
        [SerializeField] float innerPush = 8f, innerLift = 3.5f, innerKnockdown = .9f;
        [SerializeField] float outerPush = 5f, outerLift = 1.5f;
        [SerializeField] float heightTolerance = 2f;

        sealed class View
        {
            public GameObject root;
            public Transform rim, fill, streak, flash;
            public Material fillMaterial, flashMaterial;
        }

        const float StreakFrom = 26f, StreakLength = 6f;
        static readonly Color Gold = new Color(1f, .78f, .3f);

        uint seed;
        double start = double.NaN, lastClock = double.NaN;
        int[] order = new int[0], before = new int[0];
        readonly List<View> views = new List<View>();
        readonly Collider[] overlaps = new Collider[64];
        readonly HashSet<ImportedObstacleActor> struck = new HashSet<ImportedObstacleActor>();

        public int LineCount => lines.Length;
        public float Interval => interval;
        public float Warning => warning;
        // How many meteors are showing now (warning, falling or just landed).
        public int Showing { get; private set; }
        // How many have landed since this PC began to watch.
        public int Landed { get; private set; }

        public void Configure(Line[] lines, float interval, int salt)
        {
            this.lines = lines ?? new Line[0];
            this.interval = Mathf.Max(.2f, interval);
            this.salt = salt;
        }

        public void OnSection(PawnRushSection section)
        {
            seed = SeededSchedule.Hash(section.Seed, salt);
            start = section.StartClock;
            lastClock = double.NaN;
            Landed = 0;
        }

        // The nth meteor (n >= 0): a pure function of the seed and n.
        public Meteor At(long n)
        {
            var random = new SeededRandom(SeededSchedule.Hash(seed, unchecked((uint)n)));
            // A quarter of the interval either side of its slot: two never land closer than half an interval.
            double jitter = random.Range(-.25, .25);
            float along = (float)random.NextDouble();
            return new Meteor { Serial = n, Time = start + (n + .5 + jitter) * interval, Line = LineOf(n), Along = along };
        }

        // Which line the nth falls on. The lines go round in blocks, each block a shuffle of them all; when a block
        // would start on the line the block before ended on, its first two change places - so never the same line
        // twice running, and the answer for n needs only its own block and the one before. Two lines take turns.
        int LineOf(long n)
        {
            int count = lines.Length;
            if (count <= 1) return 0;
            if (count == 2) return (int)((n + (seed & 1u)) % 2);
            if (order.Length != count)
            {
                order = new int[count];
                before = new int[count];
            }
            long block = n / count;
            Shuffle(block, order);
            if (block > 0)
            {
                Shuffle(block - 1, before);
                if (order[0] == before[count - 1])
                {
                    int first = order[0];
                    order[0] = order[1];
                    order[1] = first;
                }
            }
            return order[(int)(n % count)];
        }

        void Shuffle(long block, int[] into)
        {
            var random = new SeededRandom(SeededSchedule.Hash(seed ^ 0x9E3779B9u, unchecked((uint)block)));
            for (int i = 0; i < into.Length; i++) into[i] = i;
            for (int i = into.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int t = into[i];
                into[i] = into[j];
                into[j] = t;
            }
        }

        // Where a meteor lands, in this object's frame: on its line's top, clear of the line's ends.
        public Vector3 Point(Meteor m)
        {
            var line = lines[m.Line];
            float length = Vector3.Distance(line.from, line.to);
            if (length < .01f) return line.from;
            float margin = Mathf.Min(endMargin, length * .3f);
            return Vector3.Lerp(line.from, line.to, (margin + (length - 2f * margin) * m.Along) / length);
        }

        void FixedUpdate()
        {
            if (double.IsNaN(start) || lines.Length == 0) return;
            double now = ObstacleClock.Now;
            long current = (long)Math.Floor((now - start) / interval);
            int used = 0;
            for (long n = Math.Max(0L, current - 2); n <= current + 2; n++)
            {
                var m = At(n);
                double age = now - (m.Time - warning);   // since its warning began
                if (age < 0 || age > warning + flash) continue;
                if (used == views.Count) views.Add(Make());
                Draw(views[used++], m, (float)age);
                // The hit, once, the step its landing is crossed (never for landings before this PC began to watch).
                if (!double.IsNaN(lastClock) && m.Time > lastClock && m.Time <= now)
                {
                    Landed++;
                    if (ObstacleClock.Authority) Strike(m);
                }
            }
            for (int i = used; i < views.Count; i++)
                if (views[i].root.activeSelf) views[i].root.SetActive(false);
            Showing = used;
            lastClock = now;
        }

        void Draw(View view, Meteor m, float age)
        {
            var line = lines[m.Line];
            var along = (line.to - line.from).normalized;
            var side = Vector3.Cross(Vector3.up, along);
            // Flat on the line, which may slope.
            var facing = side.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(along, Vector3.Cross(along, side.normalized)) : Quaternion.identity;
            view.root.transform.localPosition = Point(m);
            view.root.transform.localRotation = facing;
            if (!view.root.activeSelf) view.root.SetActive(true);

            bool landed = age >= warning;
            float k = Mathf.Clamp01(age / warning);
            view.rim.gameObject.SetActive(!landed);
            view.fill.gameObject.SetActive(!landed);
            // The ring fills from its middle to the knock-down's edge as the meteor comes.
            view.fill.localScale = new Vector3(innerRadius * 2f * k, .006f, innerRadius * 2f * k);
            view.fillMaterial.color = new Color(Gold.r, Gold.g, Gold.b, .25f + .5f * k);

            bool falling = !landed && age >= warning - streak;
            view.streak.gameObject.SetActive(falling);
            if (falling)
            {
                float u = (age - (warning - streak)) / streak;
                view.streak.localPosition = Vector3.up * (StreakLength * .5f + StreakFrom * (1f - u));
            }

            view.flash.gameObject.SetActive(landed);
            if (landed)
            {
                float f = Mathf.Clamp01((age - warning) / flash), e = 1f - (1f - f) * (1f - f);
                float size = Mathf.Lerp(.8f, innerRadius * 2.4f, e);
                view.flash.localScale = new Vector3(size, size * .5f, size);
                view.flashMaterial.color = new Color(1f, .9f, .6f, .75f * (1f - f) * (1f - f));
            }
        }

        void Strike(Meteor m)
        {
            var line = lines[m.Line];
            var center = transform.TransformPoint(Point(m));
            var along = transform.TransformDirection(line.to - line.from);
            along.y = 0f;
            int n = Physics.OverlapSphereNonAlloc(center, outerRadius + 1f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            struck.Clear();
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor == null || !actor.CanReceiveObstacle || !struck.Add(actor)) continue;
                var delta = actor.Position - center;
                if (Mathf.Abs(delta.y) > heightTolerance) continue;
                var flat = Vector3.ProjectOnPlane(delta, Vector3.up);
                float distance = flat.magnitude;
                if (distance > outerRadius) continue;
                // Right under it: along the line, not off it.
                var outward = distance > .05f ? flat / distance : along.sqrMagnitude > 1e-6f ? along.normalized : transform.forward;
                bool inner = distance <= innerRadius;
                actor.AddObstacleHit(outward * (inner ? innerPush : outerPush) + Vector3.up * (inner ? innerLift : outerLift),
                                     inner ? innerKnockdown : 0f);
            }
        }

        // ------------------------------------------------------------------ the ring, the streak and the flash

        View Make()
        {
            var root = new GameObject("Meteor");
            root.transform.SetParent(transform, false);
            var view = new View { root = root };
            view.rim = Shape(root.transform, PrimitiveType.Cylinder, Vector3.up * .02f,
                             new Vector3(outerRadius * 2f, .004f, outerRadius * 2f), Arena.Glow(new Color(Gold.r, Gold.g, Gold.b, .22f)));
            view.rim.name = "Warning ring (shove)";
            view.fillMaterial = new Material(Arena.Glow(new Color(Gold.r, Gold.g, Gold.b, .3f)));
            view.fill = Shape(root.transform, PrimitiveType.Cylinder, Vector3.up * .03f, Vector3.one, view.fillMaterial);
            view.fill.name = "Warning fill (knock-down)";
            view.streak = Shape(root.transform, PrimitiveType.Cylinder, Vector3.up * StreakFrom,
                                new Vector3(.35f, StreakLength * .5f, .35f), Arena.Glow(new Color(1f, .92f, .7f, .8f)));
            view.streak.name = "Streak";
            view.flashMaterial = new Material(Arena.Glow(new Color(1f, .9f, .6f, .7f)));
            view.flash = Shape(root.transform, PrimitiveType.Sphere, Vector3.up * .2f, Vector3.one, view.flashMaterial);
            view.flash.name = "Flash";
            return view;
        }

        static Transform Shape(Transform parent, PrimitiveType type, Vector3 at, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void OnDisable()
        {
            foreach (var view in views)
                if (view.root != null) view.root.SetActive(false);
            lastClock = double.NaN;
            Showing = 0;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, .8f, .2f, .8f);
            foreach (var line in lines) Gizmos.DrawLine(line.from + Vector3.up * .1f, line.to + Vector3.up * .1f);
        }
    }
}
