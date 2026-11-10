using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Carriage Yard's roaming carriage (R286, user 11-03: "넓은 공간 구역에서만 랜덤하게 돌진하며 돌아다니는
    // 마차"): it keeps charging to random points - turns on the spot to face the next point, stands with its lamps red,
    // charges, rests - and knocks away whoever it runs into while it charges (YardImpact: once a charge, mostly out to
    // the side, setting the speed - a hit is always a loss). Where and when come from a Core RoamingFleet plan, a
    // function of the plan's start and the shared obstacle clock (HANDOFF absolute rule 7), so PCs handed the same start
    // (StartAt) see it in the same place without a packet.
    //
    // It warns before it goes (R287, from the 6v6 review: about a third of the carriages that hit a walking pawn were off
    // screen when they set off): from the start of its turn a red strip on the floor shows the line it will charge
    // along, shrinking behind it as it passes; its lamps are big and red from the turn to the end of the charge; its
    // wheels rumble with its speed and a whip cracks as it sets off. All of it is read from the plan, so every PC
    // shows the same. While it charges its hit shell reaches up to 1.5 m ahead of its nose, so whoever it knocks aside
    // is clear before the nose gets there (otherwise the solid box caught most of them and shoved them on ahead at its
    // own speed), and the follow camera looks through it (CameraSeeThrough) instead of jumping in front of it.
    //
    // In a CarriageFleet (the yard) it is one of several sharing one area, which never touch; built by the fleet at
    // the fleet's origin. On its own (this prefab dropped anywhere) it makes a fleet of one, starting when it is switched
    // on: its area is halfWidth either side along its own x and halfDepth along its z round where it stands. Keep the
    // area BodyRadius inside any wall.
    public sealed class RoamingCarriage : Obstacle, IHitTurns
    {
        [Header("On its own (no fleet): its centre's area, metres either side")]
        [SerializeField] float halfWidth = 15f;
        [SerializeField] float halfDepth = 3f;
        [Tooltip("Top speed of a charge (m/s)")]
        [SerializeField] float moveSpeed = 9f;
        [Tooltip("Seconds a half turn on the spot takes; a smaller turn takes its share")]
        [SerializeField] float turnSeconds = 2.2f;
        [Tooltip("Shortest and longest wait with its lamps red before a charge (s)")]
        [SerializeField] float minAim = .6f, maxAim = 1.2f;
        [SerializeField] int seed = 1;

        [Header("Hit")]
        [Tooltip("Push of a hit (m/s) before the carriage's speed adds 0.25 per m/s (at most 7 in all); it sets the "
                 + "character's speed, mostly out to the side of the charge")]
        [SerializeField] float knockbackForce = 4f;
        [Tooltip("Knocked down for this long by a carriage moving 3 m/s or more (s)")]
        [SerializeField] float knockdownSeconds = 1f;
        [Tooltip("Body colour: 0 green, 2 blue (1 red is kept off roaming carriages: red is their warning)")]
        [SerializeField] int paint;
        [Tooltip("The wheels' rumble and a whip crack when a charge starts")]
        [SerializeField] bool sounds = true;

        // The body's reach from its centre whichever way it faces: half its diagonal.
        public static float BodyRadius => .5f * Mathf.Sqrt(YardKit.CarriageLength * YardKit.CarriageLength + YardKit.CarriageWidth * YardKit.CarriageWidth);

        // Width of the charge line on the floor (m): the body and a little either side.
        public const float LineWidth = 2.8f;
        // How far the hit shell reaches ahead of the nose while charging (m): a 7 m/s push sideways clears the body in
        // the ~0.2 s the nose takes to get there.
        public const float Lead = 1.5f;
        static readonly Color LineColor = new Color(1f, .22f, .16f, 1f);

        CarriageFleet fleet;
        RoamingFleet own, evaluated, previous;   // previous/evaluated: the plans of the last two poses
        int index;
        YardImpact impact;
        BoxCollider shell;
        Vector3 shellSize, shellCentre;
        CameraSeeThrough see;
        Transform[] wheels;
        Renderer[] lamps;
        Transform line;
        Renderer lineRenderer;
        Material lineMaterial;
        AudioSource rumble, crack;
        RoamingFleet.Stage shown;
        Vector3 lastPosition;
        float wheelAngle;
        bool built;

        RoamingFleet Plan => fleet != null ? fleet.Fleet : own;

        RoamingFleet.Pose Now => Plan != null ? Plan.At(index, ObstacleClock.Now + phase)
                                              : new RoamingFleet.Pose { Stage = RoamingFleet.Stage.Waiting, LegStart = double.NaN };

        public RoamingFleet.Stage Stage => Now.Stage;

        // One hit a charge: the leg's start.
        public double HitTurn => Now.LegStart;

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
        // The same start again changes nothing.
        public void StartAt(double clockTime)
        {
            if (fleet == null && (own == null || own.StartTime != clockTime)) own = Alone(clockTime);
        }

        protected override void Awake()
        {
            // The plan starts now and phase moves the carriage along it, as phase moves every obstacle. On a shared
            // match clock it waits at home for the match's start (StartAt) instead.
            if (fleet == null && own == null) own = Alone(ObstacleClock.Shared ? double.NaN : ObstacleClock.Now);
            Build();
            base.Awake();
            lastPosition = transform.position;
        }

        // The charge line is not the carriage's child: hide it with the carriage.
        void OnDisable()
        {
            if (lineRenderer != null) lineRenderer.enabled = false;
        }

        void OnDestroy()
        {
            if (line != null) Destroy(line.gameObject);
            if (lineMaterial != null) Destroy(lineMaterial);
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
            impact = YardKit.CarriageCollider(transform, knockbackForce, seeThrough: true);
            impact.knockdownSeconds = knockdownSeconds;
            shell = impact.GetComponent<BoxCollider>();
            shellSize = shell.size;
            shellCentre = shell.center;
            see = impact.solid != null ? impact.solid.GetComponent<CameraSeeThrough>() : null;
            // About 6.5 m/s at a full charge, mostly out to the side, the speed set rather than added, and the part of it
            // up the yard (its way on, the fleet's +z) cut to 60 %: a hit costs a second or two wherever the charge was
            // going, about a 6 m slide, instead of 10-20 m or a free ride up the yard. A short cooldown: a pawn staggered
            // as the charge sets off is knocked down properly as soon as the carriage is up to speed.
            impact.speedScale = .25f;
            impact.maxPush = 7f;
            impact.lift = 1.2f;
            impact.alongShare = .3f;
            impact.replaceSpeed = true;
            impact.course = transform.parent;
            impact.forwardShare = .6f;
            impact.cooldown = .2f;
            // Big lamps, seen from across the yard.
            var found = new System.Collections.Generic.List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>())
                if (r.name == "Lamp")
                {
                    r.transform.localScale *= 1.8f;
                    found.Add(r);
                }
            lamps = found.ToArray();
            // The charge line: one flat sheet on the floor beside the carriage, not under it (it must not move with it).
            lineMaterial = new Material(Arena.Glow(LineColor)) { name = "M_YardChargeLine" };
            var strip = GameObject.CreatePrimitive(PrimitiveType.Quad);
            strip.name = "Charge line (" + name + ")";
            DestroyImmediate(strip.GetComponent<Collider>());
            strip.transform.SetParent(transform.parent, false);
            line = strip.transform;
            lineRenderer = strip.GetComponent<Renderer>();
            lineRenderer.sharedMaterial = lineMaterial;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.enabled = false;
            if (sounds)
            {
                rumble = Voice(YardKit.RumbleSound, true);
                crack = Voice(YardKit.CrackSound, false);
            }
        }

        AudioSource Voice(AudioClip clip, bool loop)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.clip = clip;
            a.loop = loop;
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 4f;
            a.maxDistance = 35f;
            a.dopplerLevel = 0f;
            SectionSoundGate.Gate(a);   // R529
            a.volume = 0f;
            return a;
        }

        // The scripted knock-back belongs to the charge. Turning on the spot its ends swing (at most ~5 m/s with the
        // half turn at 2.2 s) and would throw whoever leant on a carriage that had just been standing still; then the
        // box only shoves with its body.
        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            var pose = Now;
            bool charging = pose.Stage == RoamingFleet.Stage.Charging;
            if (impact != null) impact.minSpeed = charging ? .4f : 99f;
            if (see != null && see.enabled != charging) see.enabled = charging;
            if (shell != null)
            {
                // Ahead of the nose by up to Lead while charging, never past where the nose will stop.
                float ahead = charging ? Mathf.Clamp(new Vector2((float)(pose.ToX - pose.X), (float)(pose.ToZ - pose.Z)).magnitude, 0f, Lead - .25f) : 0f;
                var size = new Vector3(shellSize.x, shellSize.y, shellSize.z + ahead);
                if (shell.size != size)
                {
                    shell.size = size;
                    shell.center = shellCentre + Vector3.forward * (ahead * .5f);
                }
            }
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

        // Pictures and sounds only: the wheels roll with the way it moves, the lamps go red from the turn to the end of
        // the charge, the charge line, the rumble and the crack.
        void LateUpdate()
        {
            var moved = transform.position - lastPosition;
            lastPosition = transform.position;
            wheelAngle = (wheelAngle + Vector3.Dot(moved, transform.forward) / (YardKit.CarriageWheel * .5f) * Mathf.Rad2Deg) % 360f;
            if (wheels != null)
                foreach (var wheel in wheels)
                    if (wheel != null) wheel.localRotation = Quaternion.Euler(wheelAngle, 0f, 0f);
            var pose = Now;
            bool warning = pose.Stage == RoamingFleet.Stage.Turning || pose.Stage == RoamingFleet.Stage.Aiming || pose.Stage == RoamingFleet.Stage.Charging;
            if (lamps != null)
            {
                var material = warning ? YardKit.Warning : YardKit.Lamp;
                foreach (var lamp in lamps)
                    if (lamp != null && lamp.sharedMaterial != material) lamp.sharedMaterial = material;
            }
            DrawLine(pose, warning);
            if (rumble != null)
            {
                // Kept going from here: switched off and on again, or after an audio reset, it starts once more.
                if (!rumble.isPlaying && rumble.isActiveAndEnabled) rumble.Play();
                float share = Mathf.Clamp01((float)pose.Speed / 9.5f);
                rumble.volume = .5f * share * GameSettings.Effects;
                rumble.pitch = .75f + .45f * share;
            }
            if (crack != null && pose.Stage == RoamingFleet.Stage.Charging && shown != RoamingFleet.Stage.Charging)
            {
                crack.volume = GameSettings.Effects;
                crack.PlayOneShot(crack.clip, .8f);
            }
            shown = pose.Stage;
        }

        // From the carriage's centre to where its nose stops, flat on the floor; brighter and pulsing while it aims.
        void DrawLine(RoamingFleet.Pose pose, bool warning)
        {
            if (lineRenderer == null) return;
            var from = new Vector3((float)pose.X, 0f, (float)pose.Z);
            var ahead = new Vector3((float)pose.ToX, 0f, (float)pose.ToZ) - from;
            float length = ahead.magnitude;
            if (!warning || length < .1f)
            {
                if (lineRenderer.enabled) lineRenderer.enabled = false;
                return;
            }
            var way = ahead / length;
            float reach = length + YardKit.CarriageLength * .5f;
            // The quad lies in its x-y plane: turned to lie flat with its y along the charge.
            line.SetPositionAndRotation(StartPosition + StartRotation * (from + way * (reach * .5f)) + Vector3.up * .03f,
                                        StartRotation * Quaternion.LookRotation(way) * Quaternion.Euler(90f, 0f, 0f));
            var scale = line.parent != null ? line.parent.lossyScale : Vector3.one;
            line.localScale = new Vector3(LineWidth / Mathf.Max(1e-4f, scale.x), reach / Mathf.Max(1e-4f, scale.z), 1f);
            // A pulse while it aims, on the shared clock like the rest.
            float pulse = (float)System.Math.Sin(ObstacleClock.Now * 14.0 % (2.0 * System.Math.PI));
            float alpha = pose.Stage == RoamingFleet.Stage.Turning ? .3f
                        : pose.Stage == RoamingFleet.Stage.Aiming ? .45f + .2f * pulse
                        : .4f;
            var color = LineColor;
            color.a = alpha;
            lineMaterial.color = color;
            if (!lineRenderer.enabled) lineRenderer.enabled = true;
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
