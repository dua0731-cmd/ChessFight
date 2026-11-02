using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The grand stair hall's moving stair (R150, the user: "해리포터의 움직이는 계단처럼 계단이 움직여도"): a whole
    // flight that turns about a vertical axis through one corner of its foot. Lined up, it joins the turning
    // landing to the window gallery; turned away, its top hangs a few metres short of the gallery over the
    // hall, and whoever is on it rides along and waits up there. It has no railings: a shove near its edge
    // drops a pawn to the hall floor (back to the turning landing's checkpoint). The foot corner on the axis
    // never leaves the landing, so the stair can be boarded at any time.
    //
    // Its turn is a function of the shared obstacle clock (DECISIONS G2) - the same on every PC, nothing sent;
    // riders stand on it through ImportedMovingSurface like on any moving floor. A shut wing stands it still
    // (GrandStairHall). The object's origin is the axis; its children are the flight.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SwingingStair : MonoBehaviour
    {
        [Tooltip("How far it turns away, degrees about +y (negative turns the other way).")]
        [SerializeField] float turn = 60f;
        [Tooltip("Seconds lined up, warning, turning away, away, then turning back in as long.")]
        [SerializeField] float joined = 5f, warning = 1f, swing = 3.5f, away = 3f;
        [Tooltip("Seconds added to the shared obstacle clock.")]
        [SerializeField] float phaseOffset;
        [Tooltip("Lit (flashing) while it warns, steady while it turns.")]
        [SerializeField] Renderer[] warningLights = new Renderer[0];

        Rigidbody body;
        Vector3 pivot;
        Quaternion rest;

        public float Cycle => joined + warning + 2f * swing + away;

        // For the builder: on an inactive object, before Awake.
        public void Configure(float degrees, float phase, Renderer[] lights)
        {
            turn = degrees;
            phaseOffset = phase;
            warningLights = lights ?? new Renderer[0];
        }

        double Into(double time)
        {
            double c = Cycle;
            return ((time + phaseOffset) % c + c) % c;
        }

        // 0 lined up with the gallery .. 1 turned away.
        public float Turned(double time)
        {
            double t = Into(time);
            if (t < joined + warning) return 0f;
            t -= joined + warning;
            if (t < swing) return Smooth((float)(t / swing));
            t -= swing;
            if (t < away) return 1f;
            return 1f - Smooth((float)((t - away) / swing));
        }

        public bool Warning(double time)
        {
            double t = Into(time);
            return t >= joined && t < joined + warning;
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        Pose Evaluate(double time) => new Pose(pivot, rest * Quaternion.AngleAxis(turn * Turned(time), Vector3.up));

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            pivot = transform.position;
            rest = transform.rotation;
            ImportedMovingSurface.Bind(body, Evaluate, () => isActiveAndEnabled);
            var pose = Evaluate(ObstacleClock.Now);
            body.position = pose.position;
            body.rotation = pose.rotation;
            Light(false);
        }

        void FixedUpdate()
        {
            double now = ObstacleClock.Now;
            body.MoveRotation(Evaluate(now).rotation);
            float turned = Turned(now);
            Light(Warning(now) ? Mathf.Repeat((float)(now * 4.0), 1f) < .5f : turned > 0f && turned < 1f);
        }

        void OnDisable() => Light(false);

        void Light(bool on)
        {
            if (warningLights == null) return;
            foreach (var light in warningLights) if (light != null) light.enabled = on;
        }
    }
}
