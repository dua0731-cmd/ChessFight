using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The grand banquet hall's devices that are not one moving body (대연회장, R815 design v0.4, R816 grey box): the
    // magician's tablecloth trick, the chocolate fountain's flow and its ripples (R828), the cloche lifts' hoist lines (R828),
    // the lamps and the chandeliers' look. (R825: the champagne tower's overflow went with the tower - the user: "마지막
    // 샴페인 구간은 빼자. 샹들리에를 넘어가면 끝".)
    // Every one is timed by the shared obstacle clock; nothing here is sent.

    // 2. The magician's tablecloth trick. Every `period` seconds: the cloth lies, the drum rolls (the cloth turns red), the
    //    magician pulls it off southward in an instant and holds it while it is gone, then lays it again from his end.
    //    At the pull everyone standing on the cloth - not on a plate - is thrown off the table's south end into the servery
    //    (the throw is LaunchPad's arc, from where each one stands to the servery floor), and walks back to the waiters.
    //    Only the PC that simulates a character throws it (ILaunchable.Launch is ignored on a network puppet), as LaunchPad.
    //
    // This object sits on the section's frame (local zero): the cloth's area, the plates and the landing are in that frame.
    public sealed class TableclothTrick : MonoBehaviour
    {
        [SerializeField] Vector3 clothMin, clothMax;           // the cloth's top area, y = the table top
        [SerializeField] Vector2[] plates = new Vector2[0];    // (x, z) of each plate on the runner
        [SerializeField] float plateRadius = 1.2f;
        [SerializeField] float landingZ = 9f, landingX0 = 15f, landingX1 = 25f, clearance = 1.5f;
        [SerializeField] float lie = 3.5f, roll = 2f, pull = .3f, gone = 2.2f, lay = 2f, pullDistance = 30f;
        [SerializeField] Transform cloth;                       // the picture, pivoted on its south end; scaled along z to lay it
        [SerializeField] Material lying, rolling;

        readonly Collider[] overlap = new Collider[64];
        readonly HashSet<ILaunchable> seen = new HashSet<ILaunchable>();
        Renderer[] clothRenderers = new Renderer[0];
        Vector3 clothRest;
        long lastPull = long.MinValue;

        public float Period => lie + roll + pull + gone + lay;
        // Seconds into the cycle: the drum roll's start, the pull, the laying's start, and how long the laying takes
        // (read by the dressed table's show, BanquetTableShow, and the hall's sounds, GrandBanquetHallSound); and each
        // throw's spot (R844 sound: on every PC that runs the pull - a puppet's throw is ignored by Launch but still
        // counted, as Throws is).
        public float RollAt => lie;
        public float PullAt => lie + roll;
        public float LayAt => lie + roll + pull + gone;
        public float LayTime => lay;
        public event Action<Vector3> Threw;
        public int Throws { get; private set; }
        public int Pulls { get; private set; }

        public void Configure(Vector3 areaMin, Vector3 areaMax, Vector2[] plateCentres, float plateRadiusMetres, float landingAtZ,
                              Transform clothPicture, Material lyingMaterial, Material rollingMaterial)
        {
            clothMin = areaMin;
            clothMax = areaMax;
            plates = plateCentres;
            plateRadius = plateRadiusMetres;
            landingZ = landingAtZ;
            landingX0 = areaMin.x + 1f;
            landingX1 = areaMax.x - 1f;
            cloth = clothPicture;
            lying = lyingMaterial;
            rolling = rollingMaterial;
        }

        // Seconds into the trick's cycle at `time`.
        public float Phase(double time)
        {
            double u = time % Period;
            return (float)(u < 0.0 ? u + Period : u);
        }

        public bool DrumRolling(double time)
        {
            float u = Phase(time);
            return u >= lie && u < lie + roll;
        }

        void Awake()
        {
            if (cloth != null)
            {
                clothRest = cloth.localScale;
                clothRenderers = cloth.GetComponentsInChildren<Renderer>(true);
            }
        }

        void FixedUpdate()
        {
            // The pull's start, counted like LaunchPad's spring: the first step only learns which cycle it is in.
            long pullIndex = (long)Math.Floor((ObstacleClock.Now - (lie + roll)) / Period);
            if (lastPull != long.MinValue && pullIndex != lastPull) Pull();
            lastPull = pullIndex;
        }

        void Pull()
        {
            Pulls++;
            var frame = transform;
            Vector3 lo = new Vector3(clothMin.x, clothMin.y - .2f, clothMin.z), hi = new Vector3(clothMax.x, clothMin.y + 3f, clothMax.z);
            int n = Physics.OverlapBoxNonAlloc(frame.TransformPoint((lo + hi) * .5f), (hi - lo) * .5f, overlap, frame.rotation, ~0,
                                               QueryTriggerInteraction.Ignore);
            seen.Clear();
            float gravity = -Physics.gravity.y;
            for (int i = 0; i < n; i++)
            {
                var who = overlap[i].GetComponentInParent<ILaunchable>();
                if (who == null || !seen.Add(who)) continue;
                Vector3 from = who.LaunchFrom, local = frame.InverseTransformPoint(from);
                // On the cloth: over its area, at the table top (a plate's top is 0.12 m higher), not over a plate.
                if (local.x < clothMin.x || local.x > clothMax.x || local.z < clothMin.z || local.z > clothMax.z) continue;
                if (local.y > clothMin.y + .08f || local.y < clothMin.y - .3f) continue;
                bool onPlate = false;
                foreach (var p in plates)
                    if ((new Vector2(local.x, local.z) - p).sqrMagnitude < (plateRadius + .05f) * (plateRadius + .05f)) { onPlate = true; break; }
                if (onPlate) continue;
                var to = frame.TransformPoint(new Vector3(Mathf.Clamp(local.x, landingX0, landingX1), 0f, landingZ));
                who.Launch(LaunchPad.ArcVelocity(from, to, clearance, gravity));
                Throws++;
                Threw?.Invoke(from);
            }
        }

        // The picture: red while the drum rolls, gone south in the pull, away while the magician holds it, laid again from
        // the south end.
        void LateUpdate()
        {
            if (cloth == null) return;
            float u = Phase(ObstacleClock.Now);
            float a = lie, b = a + roll, c = b + pull, d = c + gone;
            Vector3 at = Vector3.zero, scale = clothRest;
            bool shown = true;
            if (u >= b && u < c) { float k = (u - b) / pull; at.z = -pullDistance * k * k; }
            else if (u >= c && u < d) shown = false;
            else if (u >= d) { float k = Mathf.Clamp01((u - d) / lay); scale.z = clothRest.z * Mathf.Max(.02f, k * k * (3f - 2f * k)); }
            cloth.localPosition = new Vector3(cloth.localPosition.x, cloth.localPosition.y, clothMin.z) + at;
            cloth.localScale = scale;
            var m = u >= a && u < b ? rolling : lying;
            foreach (var r in clothRenderers)
            {
                r.enabled = shown;
                if (m != null && r.name.EndsWith("cloth")) r.sharedMaterial = m;
            }
        }
    }

    // 6. A tier of the chocolate fountain: it does not move, but says it does (IMovingSurface, as ConveyorBeltSurface), so a
    //    climber on its chocolate is carried down at the flow's speed. R828: the chocolate flows down the whole of every tier
    //    and is climbed against (0.3 m/s down, a pawn climbs at 1.2), as the design had it; ChocolateRipples shows it. R825
    //    had made it faster than a climb and put ladders on the fountain (the user: "사다리를 초코분수에 넣으면 어떡해").
    //    `stillBand` leaves the last bit under the rim still, where the hands are on the lip and the climb over it begins:
    //    a hanging pawn is carried by its anchor, and the climb over starts with the anchor at most 0.25 m under the top
    //    (RagdollPawn.TryStartTopOut), so a band of 0.45 m keeps the whole climb over from being carried back down (R817's
    //    slip at the rim) and shows nothing (R817's 1.3 m pale band did: "반은 초코가 흐르고 반은 안흐른다").
    //    `outward` pushes whoever stands on the top off it. This object is the tier's cylinder (YardKit.Cylinder: local y -1 .. 1).
    public sealed class ChocolateFlow : MonoBehaviour, IMovingSurface
    {
        [SerializeField] float down = .4f, outward, stillBand = 1.3f;

        // The top's flow is for what stands on it: a point this far over the top (a standing pawn's hips).
        const float StandingAbove = .5f;

        public float Down => down;
        public float Outward => outward;
        public float StillBand => stillBand;

        public void Configure(float downSpeed, float outwardSpeed, float stillUnderTheRim)
        {
            down = downSpeed;
            outward = outwardSpeed;
            stillBand = stillUnderTheRim;
        }

        public Vector3 PointVelocity(Vector3 worldPoint)
        {
            Vector3 up = transform.up, top = transform.TransformPoint(Vector3.up);
            float over = Vector3.Dot(worldPoint - top, up);
            if (over < -stillBand) return -up * down;
            if (over < StandingAbove || outward <= 0f) return Vector3.zero;
            Vector3 d = worldPoint - transform.position;
            d -= up * Vector3.Dot(d, up);
            return d.sqrMagnitude < 1e-4f ? Vector3.zero : d.normalized * outward;
        }

        public Quaternion DeltaRotation => Quaternion.identity;
    }

    // 6b. The flow's picture (R828): ripples that well up out of a tier's rim, run down its side at the flow's speed and
    //     sink into the tier (or the bowl) under it - on the whole of every tier, so the chocolate is seen to flow everywhere
    //     it is climbed. Only looks, on the clock (the same on every PC); the pull is ChocolateFlow's. This object sits on
    //     the tier's axis at its foot; its rings are cylinders 0.14 m high (YardKit.Cylinder).
    public sealed class ChocolateRipples : MonoBehaviour
    {
        [SerializeField] float height = 2.8f, radius = 3.3f, speed = .3f;
        [SerializeField] Transform[] rings = new Transform[0];

        // A ring is 0.14 m high, comes out of the rim over its first 0.3 m and then stands 5 cm proud of the chocolate.
        public const float Half = .07f;
        const float Proud = .05f, Swell = .3f;

        public float Height => height;
        public int Count => rings.Length;
        public Transform Ring(int i) => rings[i];

        // Call on an INACTIVE object, the rings its children.
        public void Configure(float tierHeight, float tierRadius, float metresPerSecond, Transform[] ringPictures)
        {
            height = tierHeight;
            radius = tierRadius;
            speed = metresPerSecond;
            rings = ringPictures;
            Pose(ObstacleClock.Now);
        }

        // How far under the rim ring `i` is at `time`: 0 .. height, the rings evenly apart.
        public float Below(int i, double time)
        {
            double d = (speed * time + (double)i * height / Mathf.Max(1, rings.Length)) % height;
            return (float)(d < 0.0 ? d + height : d);
        }

        void LateUpdate() => Pose(ObstacleClock.Now);

        void Pose(double time)
        {
            for (int i = 0; i < rings.Length; i++)
            {
                if (rings[i] == null) continue;
                float below = Below(i, time), d = 2f * (radius + Mathf.Lerp(-Proud, Proud, below / Swell));
                // At 0 under the rim it is inside the tier; a whole tier on, inside the one under it: the wrap is not seen.
                rings[i].localPosition = new Vector3(0f, height - Half - .01f - below, 0f);
                rings[i].localScale = new Vector3(d, Half, d);
            }
        }
    }

    // 3b. A hoist line's picture (R828, the user: "은뚜껑을 들어올렸는데 전체가 올라오는건 이상하잖아" - the glove on the
    //     cloche's handle seemed to lift the whole dish): a cable from a lug on a cloche lift's platter, or the chain from its
    //     cloche's handle, up to a pulley under the dumbwaiter's beam, stretched every frame - the platter is seen hung from
    //     the frame and the cloche lifted off it by its own chain. Only looks. This object is the line, a cylinder
    //     (YardKit.Cylinder) built at its pulley as a child of the section's content.
    public sealed class HoistLine : MonoBehaviour
    {
        [SerializeField] Transform load;
        [SerializeField] Vector3 hook, pulley;
        [SerializeField] float thickness = .07f;

        public Vector3 HookPoint => load != null ? load.TransformPoint(hook) : transform.position;
        public Vector3 PulleyPoint => transform.parent != null ? transform.parent.TransformPoint(pulley) : pulley;

        public void Configure(Transform loadBody, Vector3 hookOnLoad, float lineThickness)
        {
            load = loadBody;
            hook = hookOnLoad;
            pulley = transform.localPosition;
            thickness = lineThickness;
            Stretch();
        }

        // After the lift's body has moved (and been interpolated) for this frame.
        void LateUpdate() => Stretch();

        void Stretch()
        {
            if (load == null) return;
            var frame = transform.parent;
            Vector3 from = frame != null ? frame.InverseTransformPoint(HookPoint) : HookPoint, d = pulley - from;
            float length = d.magnitude;
            transform.localPosition = (from + pulley) * .5f;
            transform.localRotation = length > 1e-4f ? Quaternion.FromToRotation(Vector3.up, d / length) : Quaternion.identity;
            transform.localScale = new Vector3(thickness, length * .5f, thickness);
        }
    }

    // A lamp lit by the clock (R817): in a window of every `period` seconds, `from` .. `to` seconds round `at`. The
    // boarding place on the dais lights it when a queen is about to stand still in front of it, the stage's alighting
    // place when one is about to stop there. Only looks.
    public sealed class ClockLamp : MonoBehaviour
    {
        [SerializeField] float period = 4.775f, at, from = -1f, to = .5f;
        [SerializeField] Material lit, unlit;

        Renderer[] lamps = new Renderer[0];

        public void Configure(float periodSeconds, float atSeconds, float fromSeconds, float toSeconds, Material litMaterial, Material unlitMaterial)
        {
            period = periodSeconds;
            at = atSeconds;
            from = fromSeconds;
            to = toSeconds;
            lit = litMaterial;
            unlit = unlitMaterial;
        }

        public bool Lit(double time)
        {
            double u = (time - at) % period;
            if (u < 0.0) u += period;
            if (u > period * .5) u -= period;   // -period/2 .. period/2 round the moment
            return u >= from && u < to;
        }

        void Awake() => lamps = GetComponentsInChildren<Renderer>(true);

        void Update()
        {
            var m = Lit(ObstacleClock.Now) ? lit : unlit;
            foreach (var r in lamps) if (r != null && m != null) r.sharedMaterial = m;
        }
    }

    // 7. A chandelier's picture (the wheel of candles and the drop under it) swung with its rope: the RopeLine is the thing
    //    held, this only turns the picture to the rope's angle. No collider.
    public sealed class ChandelierLook : MonoBehaviour
    {
        [SerializeField] RopeLine rope;

        public void Configure(RopeLine line) => rope = line;

        void LateUpdate()
        {
            if (rope == null) return;
            var rest = rope.transform.rotation;
            transform.rotation = Quaternion.FromToRotation(rest * Vector3.down, rope.Direction(ObstacleClock.Now)) * rest;
        }
    }
}
