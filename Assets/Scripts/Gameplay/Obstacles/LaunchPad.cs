using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // A launch pad (Queen of the Hill M7). It throws whatever stands on it along a
    // set arc to one landing spot: the L-shaped pad of S3 ("up 6 m, 3 m on", the
    // knight's move). With a period it is instead a wound spring lift (S5): it
    // winds up and throws everyone on it at once every `period` seconds on the
    // shared clock.
    //
    // The arc peaks `clearance` above the landing and comes down onto it. A pad
    // aims it from where each character actually stands, so it lands on the spot
    // wherever on the pad it stepped. A spring full of people gives everyone the
    // same throw instead (sameThrow), so they land as far apart as they stood
    // rather than all on one spot. Either way it SETS the velocity (ILaunchable)
    // instead of adding to it: running on or standing still, the throw is the
    // same. A loose body (a crate) is thrown the same way.
    //
    // Level design: the arc only clears an edge it flies over late in its flight.
    // Leave the lip of the landing well short of the thrower: rising 6 m with a
    // 1.2 m clearance the body is above the lip only after 42% of the flight, so
    // the lip must be less than 58% of the throw back from the landing (the gizmo
    // draws the arc).
    //
    // The BoxCollider on this object is the pad's volume, a trigger resting on the
    // surface it throws from. Only the machine that simulates the characters
    // throws anyone; a network client's puppets ignore the call and move as the
    // host sends them.
    [RequireComponent(typeof(BoxCollider))]
    public abstract class LaunchPad : Obstacle
    {
        [Tooltip("Where it throws to, from the middle of the pad's surface, in the pad's frame (x right, y up, z forward). Metres, not scaled.")]
        [SerializeField] Vector3 landing = new Vector3(0f, 6f, 3f);
        [Tooltip("How far above the landing the arc peaks, so it comes down onto the spot instead of skimming the edge.")]
        [SerializeField] float clearance = 1.2f;
        [Tooltip("Everyone gets the same throw and lands as far apart as they stood (a spring lift). Off: every throw aims at the landing spot (a pad).")]
        [SerializeField] bool sameThrow;
        // Once thrown, a body is left alone this long: it is still inside the volume for a few steps
        // on its way up, and aimed again from there it would fly a different arc.
        const double Rethrow = 0.6;

        readonly Collider[] overlap = new Collider[64];
        readonly Dictionary<Collider, Component> owners = new Dictionary<Collider, Component>();
        readonly Dictionary<Component, double> thrownAt = new Dictionary<Component, double>();
        readonly HashSet<Component> thisStep = new HashSet<Component>();
        BoxCollider box;

        public Vector3 LandingOffset => landing;
        public float Clearance => clearance;
        public virtual float Period => 0f;
        public bool SameThrow => sameThrow;
        public int Throws { get; private set; }

        // Every thrown character or body, with the velocity it was given.
        public event Action<Component, Vector3> Thrown;

        BoxCollider Box => box != null ? box : (box = GetComponent<BoxCollider>());

        // The middle of the surface it throws from: the bottom face of its volume.
        public Vector3 Surface => transform.TransformPoint(Box.center - new Vector3(0f, Box.size.y * 0.5f, 0f));

        public Vector3 Landing => Surface + transform.rotation * landing;

        // A spring: seconds until it next fires. A pad: 0.
        public virtual float SecondsToFire => 0f;

        // For pads built from code (test beds).
        public void Configure(Vector3 landing, float clearance, float period = 0f, float phase = 0f, bool sameThrow = false,
                              Transform windKey = null)
        {
            this.landing = landing;
            this.clearance = clearance;
            this.sameThrow = sameThrow;
            ConfigureSchedule(period, phase, windKey);
        }

        // Where a body standing over `ground` (a point on the pad's surface) is thrown to.
        public Vector3 LandingFor(Vector3 ground)
        {
            if (!sameThrow) return Landing;
            Vector3 offset = ground - Surface;
            offset.y = 0f;
            return Landing + offset;
        }

        // The launch velocity that carries a body from `from` to `to`, peaking `clearance` above the
        // higher of the two: straight up to the peak, then down onto the spot.
        public static Vector3 ArcVelocity(Vector3 from, Vector3 to, float clearance, float gravity)
        {
            gravity = Mathf.Max(0.01f, gravity);
            float rise = to.y - from.y;
            float apex = Mathf.Max(rise, 0f) + Mathf.Max(0.05f, clearance);
            float up = Mathf.Sqrt(2f * gravity * apex);
            Vector3 across = to - from;
            across.y = 0f;
            return across / ArcSeconds(rise, clearance, gravity) + Vector3.up * up;
        }

        // Seconds in the air on that arc.
        public static float ArcSeconds(float rise, float clearance, float gravity)
        {
            gravity = Mathf.Max(0.01f, gravity);
            float apex = Mathf.Max(rise, 0f) + Mathf.Max(0.05f, clearance);
            return Mathf.Sqrt(2f * apex / gravity) + Mathf.Sqrt(2f * (apex - rise) / gravity);
        }

        protected void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        protected virtual void ConfigureSchedule(float period, float phase, Transform windKey) { }

        protected void ThrowAllOn()
        {
            var b = Box;
            Vector3 half = Vector3.Scale(b.size, transform.lossyScale) * 0.5f;
            int n = Physics.OverlapBoxNonAlloc(transform.TransformPoint(b.center), half, overlap, transform.rotation, ~0,
                QueryTriggerInteraction.Ignore);
            double now = ObstacleClock.Now;
            thisStep.Clear();
            for (int i = 0; i < n; i++)
            {
                Component who = Owner(overlap[i]);
                if (who == null || !thisStep.Add(who)) continue;
                if (thrownAt.TryGetValue(who, out double last) && now - last < Rethrow && now >= last) continue;
                Throw(who, overlap[i], now);
            }
        }

        // What a collider belongs to: a character (ILaunchable), or a loose body; null for scenery.
        Component Owner(Collider c)
        {
            if (owners.TryGetValue(c, out var known)) return known;
            if (owners.Count > 512) owners.Clear();   // bodies that came and went
            Component who = c.GetComponentInParent<ILaunchable>() as Component;
            if (who == null)
            {
                var rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic) who = rb;
            }
            owners[c] = who;
            return who;
        }

        void Throw(Component who, Collider touching, double now)
        {
            // The arc is worked out from the ground point under it to the landing: a character's feet, a
            // loose body's underside.
            Vector3 from;
            if (who is ILaunchable character) from = character.LaunchFrom;
            else
            {
                Vector3 at = who is Rigidbody body ? body.worldCenterOfMass : who.transform.position;
                from = new Vector3(at.x, touching.bounds.min.y, at.z);
            }
            Vector3 velocity = ArcVelocity(from, LandingFor(from), clearance, -Physics.gravity.y);
            if (who is ILaunchable launchable) launchable.Launch(velocity);
            else if (who is Rigidbody rb)
            {
                rb.linearVelocity = velocity;
                rb.angularVelocity = Vector3.zero;
            }
            thrownAt[who] = now;
            Throws++;
            Thrown?.Invoke(who, velocity);
        }

        protected void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f);
            Vector3 from = Surface, to = Landing;
            float g = Mathf.Max(0.01f, -Physics.gravity.y);
            Vector3 v = ArcVelocity(from, to, clearance, g);
            float flight = ArcSeconds(to.y - from.y, clearance, g);
            Vector3 last = from;
            for (int i = 1; i <= 24; i++)
            {
                float t = flight * i / 24f;
                Vector3 p = from + v * t + 0.5f * Physics.gravity * t * t;
                Gizmos.DrawLine(last, p);
                last = p;
            }
            Gizmos.DrawWireSphere(to, 0.3f);
        }
    }
}
