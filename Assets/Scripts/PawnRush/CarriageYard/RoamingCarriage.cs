using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Carriage Yard's roaming carriage (R286, user 11-03: "넓은 공간 구역에서만 랜덤하게 돌진하며 돌아다니는
    // 마차"): it keeps charging to random points - turns on the spot to face the next point, stands with its lamps red,
    // charges, rests - and knocks away whoever it runs into while it charges (YardImpact: along the charge, harder the
    // faster it goes). Where and when come from a Core RoamingFleet plan, a function of the plan's start and the shared
    // obstacle clock (HANDOFF absolute rule 7), so PCs handed the same start (StartAt) see it in the same place without
    // a packet.
    //
    // In a CarriageFleet (the yard) it is one of several sharing one area, which never touch; built by the fleet at
    // the fleet's origin. On its own (this prefab dropped anywhere) it makes a fleet of one, starting when it is switched
    // on: its area is halfWidth either side along its own x and halfDepth along its z round where it stands. Keep the
    // area BodyRadius inside any wall.
    public sealed class RoamingCarriage : Obstacle
    {
        [Header("On its own (no fleet): its centre's area, metres either side")]
        [SerializeField] float halfWidth = 15f;
        [SerializeField] float halfDepth = 3f;
        [Tooltip("Top speed of a charge (m/s)")]
        [SerializeField] float moveSpeed = 9f;
        [Tooltip("Seconds it takes to turn to its next point")]
        [SerializeField] float turnSeconds = 1.2f;
        [Tooltip("Shortest and longest wait with its lamps red before a charge (s)")]
        [SerializeField] float minAim = .6f, maxAim = 1.2f;
        [SerializeField] int seed = 1;

        [Header("Hit")]
        [Tooltip("Push of a hit (m/s); the carriage's speed where it touches adds to it")]
        [SerializeField] float knockbackForce = 7f;
        [Tooltip("Knocked down for this long by a carriage moving 3 m/s or more (s)")]
        [SerializeField] float knockdownSeconds = 1f;
        [Tooltip("Body colour: 0 green, 1 red, 2 blue")]
        [SerializeField] int paint;

        // The body's reach from its centre whichever way it faces: half its diagonal.
        public static float BodyRadius => .5f * Mathf.Sqrt(YardKit.CarriageLength * YardKit.CarriageLength + YardKit.CarriageWidth * YardKit.CarriageWidth);

        CarriageFleet fleet;
        RoamingFleet own, evaluated, previous;   // previous/evaluated: the plans of the last two poses
        int index;
        YardImpact impact;
        Transform[] wheels;
        Renderer[] lamps;
        Vector3 lastPosition;
        float wheelAngle;
        bool built;

        RoamingFleet Plan => fleet != null ? fleet.Fleet : own;

        public RoamingFleet.Stage Stage => Plan != null ? Plan.At(index, ObstacleClock.Now + phase).Stage : RoamingFleet.Stage.Waiting;

        // On its own: the area, speeds and waits (before it is switched on).
        public void Configure(float halfWidth, float halfDepth, float moveSpeed, float turnSeconds, float minAim, float maxAim, int seed,
                              float knockbackForce, float knockdownSeconds, int paint = 0)
        {
            this.halfWidth = halfWidth;
            this.halfDepth = halfDepth;
            this.moveSpeed = moveSpeed;
            this.turnSeconds = turnSeconds;
            this.minAim = minAim;
            this.maxAim = maxAim;
            this.seed = seed;
            this.knockbackForce = knockbackForce;
            this.knockdownSeconds = knockdownSeconds;
            this.paint = paint;
        }

        // One of a fleet's carriages (the fleet calls it, before switching it on).
        public void Join(CarriageFleet fleet, int index, float knockbackForce, float knockdownSeconds, int paint)
        {
            this.fleet = fleet;
            this.index = index;
            this.knockbackForce = knockbackForce;
            this.knockdownSeconds = knockdownSeconds;
            this.paint = paint;
        }

        // On its own: a match's shared start (a clock time), after Configure; a fleet's carriages take the fleet's.
        public void StartAt(double clockTime)
        {
            if (fleet == null) own = Alone(clockTime);
        }

        protected override void Awake()
        {
            // The plan starts now and phase moves the carriage along it, as phase moves every obstacle.
            if (fleet == null && own == null) own = Alone(ObstacleClock.Now);
            Build();
            base.Awake();
            lastPosition = transform.position;
        }

        // A fleet of one over its own area, moving from `start`.
        RoamingFleet Alone(double start)
        {
            float w = Mathf.Abs(halfWidth), d = Mathf.Abs(halfDepth);
            var rules = new RoamingFleet.Rules
            {
                MinX = -w, MaxX = w, MinZ = -d, MaxZ = d, BodyRadius = BodyRadius, Turn = Mathf.Max(.05f, turnSeconds),
                MinAim = Mathf.Max(0f, minAim), MaxAim = Mathf.Max(Mathf.Max(0f, minAim), maxAim),
                // A charge crosses most of the area's length.
                MinCharge = Mathf.Max(1f, .4f * 2f * Mathf.Max(w, d)), MaxCharge = Mathf.Max(2f, 2.5f * Mathf.Max(w, d)),
            };
            return new RoamingFleet(unchecked((uint)seed), start, new double[] { Mathf.Max(.5f, moveSpeed) },
                                    new double[] { 0 }, new double[] { 0 }, new double[] { w >= d ? 90 : 0 }, rules);
        }

        void Build()
        {
            if (built) return;
            built = true;
            wheels = YardKit.Carriage(transform, paint == 1 ? YardKit.CarriageRed : paint == 2 ? YardKit.CarriageBlue : YardKit.CarriageGreen);
            impact = YardKit.CarriageCollider(transform, knockbackForce);
            impact.knockdownSeconds = knockdownSeconds;
            var found = new System.Collections.Generic.List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>())
                if (r.name == "Lamp") found.Add(r);
            lamps = found.ToArray();
        }

        // The scripted knock-back belongs to the charge. Turning on the spot its ends swing at several m/s and would
        // throw whoever leant on a carriage that had just been standing still; then the box only shoves with its body.
        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (impact != null) impact.minSpeed = Stage == RoamingFleet.Stage.Charging ? .4f : 99f;
            if (own != null) own.Prune(ObstacleClock.Now + phase - 30.0);
        }

        // A new plan (StartAt) puts the carriage where it says at once instead of sweeping it there through everyone.
        protected override bool JumpsBetween(double from, double to) => previous != null && previous != evaluated;

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            var plan = Plan;
            previous = evaluated;
            evaluated = plan;
            if (plan == null)
            {
                position = StartPosition;
                rotation = StartRotation;
                return;
            }
            var pose = plan.At(index, time);
            position = StartPosition + StartRotation * new Vector3((float)pose.X, 0f, (float)pose.Z);
            rotation = StartRotation * Quaternion.Euler(0f, (float)pose.Yaw, 0f);
        }

        // Pictures only: the wheels roll with the way it moves, the lamps go red while it turns and aims.
        void LateUpdate()
        {
            var moved = transform.position - lastPosition;
            lastPosition = transform.position;
            wheelAngle = (wheelAngle + Vector3.Dot(moved, transform.forward) / (YardKit.CarriageWheel * .5f) * Mathf.Rad2Deg) % 360f;
            if (wheels != null)
                foreach (var wheel in wheels)
                    if (wheel != null) wheel.localRotation = Quaternion.Euler(wheelAngle, 0f, 0f);
            if (lamps == null) return;
            var stage = Stage;
            var material = stage == RoamingFleet.Stage.Turning || stage == RoamingFleet.Stage.Aiming ? YardKit.Warning : YardKit.Lamp;
            foreach (var lamp in lamps)
                if (lamp != null && lamp.sharedMaterial != material) lamp.sharedMaterial = material;
        }

        void OnDrawGizmos()
        {
            if (fleet != null) return;   // the fleet draws the shared area
            var centre = Application.isPlaying ? StartPosition : transform.position;
            var turn = Application.isPlaying ? StartRotation : transform.rotation;
            Gizmos.matrix = Matrix4x4.TRS(centre, turn, Vector3.one);
            Gizmos.color = new Color(1f, .55f, .2f, .8f);
            Gizmos.DrawWireCube(new Vector3(0f, .05f, 0f), new Vector3(halfWidth * 2f, .1f, halfDepth * 2f));
            Gizmos.color = new Color(1f, .55f, .2f, .3f);
            float r = BodyRadius;
            Gizmos.DrawWireCube(new Vector3(0f, YardKit.CarriageHeight * .5f, 0f), new Vector3((halfWidth + r) * 2f, YardKit.CarriageHeight, (halfDepth + r) * 2f));
        }
    }
}
