using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The grand banquet hall's devices that are not one moving body (대연회장, R815 design v0.4, R816 grey box): the
    // magician's tablecloth trick, the chocolate fountain's flow, the lamps and the chandeliers' look. (R825: the champagne
    // tower's overflow went with the tower - the user: "마지막 샴페인 구간은 빼자. 샹들리에를 넘어가면 끝".)
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
    //    climber on its chocolate is carried down at the flow's speed. R825: the chocolate flows all the way down every tier
    //    (`stillBand` 0) and faster than a pawn climbs - it is not a way up; the gilded ladders on the fountain are (the
    //    user: "한 단에서 반은 초코가 흐르고 반은 안흐른다는게 상식선에서 이해가 안 가", "사다리처럼 하든 뭘 하든 인식 가능하게").
    //    `stillBand` (R817) leaves the last metres under the rim still; `outward` pushes whoever stands on the top off it.
    //    This object is the tier's cylinder (YardKit.Cylinder: local y -1 .. 1).
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
