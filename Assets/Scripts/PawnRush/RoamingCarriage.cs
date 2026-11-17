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
    //
    // R659 (the user's zone 1: "말과 말이 이끄는 마차들이 돌아다니며"): it can also be a team - the carriage with a
    // pair of harnessed horses in front, one long body - or a loose horse, smaller and faster, which rears through its
    // aim instead of lighting lamps. The fleet gives each its own reach and turn (RoamingFleet radii / turns).
    public sealed partial class RoamingCarriage : Obstacle, IHitTurns
    {
        public enum Look { Carriage, Team, Horse }

        // A team's horses stand this far in front of the carriage's nose (m), the body's box over both.
        public const float TeamGap = .15f;

        public static float LengthOf(Look look) =>
            look == Look.Horse ? YardHorse.Length : look == Look.Team ? YardKit.CarriageLength + TeamGap + YardHorse.Length : YardKit.CarriageLength;
        public static float WidthOf(Look look) => look == Look.Horse ? YardHorse.Width : YardKit.CarriageWidth;
        public static float HeightOf(Look look) => look == Look.Horse ? YardHorse.Height : YardKit.CarriageHeight;
        // A body's reach from its centre whichever way it faces: half its diagonal.
        public static float Reach(Look look) => .5f * Mathf.Sqrt(LengthOf(look) * LengthOf(look) + WidthOf(look) * WidthOf(look));
        // R686: how far behind its middle a body turns about (RoamingFleet's pivot): a team about its coach's rear axle (the
        // coach at the back of the body, its rear wheels RearAxle behind the coach's middle), the rest on the spot.
        public const float RearAxle = 1.3f;
        public static float PivotOf(Look look) =>
            look == Look.Team ? LengthOf(Look.Team) * .5f - YardKit.CarriageLength * .5f + RearAxle : 0f;

        [Tooltip("A carriage on its own, a team (a carriage drawn by a pair of horses) or a loose horse")]
        [SerializeField] Look look;
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

        // A carriage's reach from its centre whichever way it faces: half its diagonal.
        public static float BodyRadius => Reach(Look.Carriage);

        // Width of the charge line on the floor (m): the body and a little either side.
        public const float LineWidth = 2.8f;
        float Line => look == Look.Horse ? WidthOf(look) + .5f : LineWidth;
        // How far the hit shell reaches ahead of the nose while charging (m): a 7 m/s push sideways clears the body in
        // the ~0.2 s the nose takes to get there.
        public const float Lead = 1.5f;
        // R685 (승규 "말 예고는 지워주고 나머지 예고도 실사로"): the charge line is the dark track of the way it will run (a
        // shadow on the paving that deepens through the aim), not a red neon strip; a loose horse shows none.
        static readonly Color LineColor = YardWarn.Stop;   // R685: the signal lamps' red (승규 11-17: not black)

        CarriageFleet fleet;
        RoamingFleet own, evaluated, previous;   // previous/evaluated: the plans of the last two poses
        int index;
        YardImpact impact;
        BoxCollider shell;
        Vector3 shellSize, shellCentre;
        CameraSeeThrough see;
        Transform[] wheels;
        YardHorse[] horses;
        Renderer[] lamps;
        Transform line;
        Renderer lineRenderer;
        Material lineMaterial;
        AudioSource rumble, crack;
        RoamingFleet.Stage shown;
        bool built;
        // R683 (승규 11-16 "말이 뛰는 느낌 돌아가는 거 그에따라 마차가 회전 하는 것 등 부자연스러워서 엄청 자연스럽게"): pictures
        // only, the body's box and its path untouched. A team bends at its front axle as it turns on the spot - the
        // forecarriage (the pair, the pole and the front wheels) leads into the turn, the carriage stays square in the
        // box (R684, the collider audit: trailing, it swung up to 0.6-1.1 m out of the box and pawns walked through it) - the
        // carriage rides on its springs (back as it sets off, forward as it pulls up, out of a turn, a shake at speed)
        // and each wheel rolls by how far its own rim goes along the ground (turning on the spot they used to slide).
        // Everything is read off the plan at the shared clock, so every PC draws the same.
        // Degrees, and degrees per degree a second of turn: at 4 degrees about the king pin the pair's outer sides and
        // noses stay within ~10 cm of the body's box (they stand 15 cm inside it).
        const float BendMost = 4f, BendPerTurn = .04f;
        // R686 (승규 11-17 "말 도는 것을 진짜 자연스럽게" ... "더 나아가"): a team drives its turns as a coach does, round a
        // point on its rear axle's line (the plan's turning point: RoamingFleet.Circles to the inside, or the rear axle
        // itself, PivotOf), its rear wheels rolling round it and the pair walking ahead on the forecarriage, swung out so
        // they head round the circle: the lock that points the pair's middle (PairReach ahead of the rear axle) along it -
        // atan(PairReach / radius), 50 degrees on a 4 m circle, up to LockMost about the axle itself - or its share of a
        // small turn (LockPerTurn a degree), eased in over the first LockEase of the turn and out over the last. Swung out,
        // the pair stand outside the body's box: their own box (on the forecarriage) takes them and the body's box shrinks
        // to the coach and its pole (straight, as through every charge, the one long box as before - a hit is judged by it).
        const float LockMost = 85f, LockPerTurn = .65f, LockEase = .45f;
        BoxCollider pairBox, bodyBox;
        CameraSeeThrough pairSee;
        Vector3 bodySize, bodyCentre, coachSize, coachCentre;
        static readonly Vector3 SpringAt = new Vector3(0f, .7f, 0f);
        Transform fore, cab, frame, hitchModel;
        Vector3 kingpin, cabAt, frameAt;
        Quaternion frameTurn = Quaternion.identity;
        Vector3[] wheelAt, wheelWas;
        float[] wheelSpin;
        bool[] wheelFront;
        bool wheelsPlaced;

        RoamingFleet Plan => fleet != null ? fleet.Fleet : own;

        RoamingFleet.Pose Now => Plan != null ? Plan.At(index, ObstacleClock.Now + phase)
                                              : new RoamingFleet.Pose { Stage = RoamingFleet.Stage.Waiting, LegStart = double.NaN };

        public RoamingFleet.Stage Stage => Now.Stage;
        public Look Kind => look;
        // Its top speed in the plan (for the rumble and the stride).
        float TopSpeed => Plan != null ? (float)Plan.TopSpeed(index) : moveSpeed;

        // One hit a charge: the leg's start.
        public double HitTurn => Now.LegStart;

        // On its own: the area, speeds and waits (before it is switched on).
        public void Configure(float halfWidth, float halfDepth, float moveSpeed, float turnSeconds, float minAim, float maxAim, int seed,
                              float knockbackForce, float knockdownSeconds, int paint = 0, Look look = Look.Carriage)
        {
            this.look = look;
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

        // One of a fleet's bodies (the fleet calls it, before switching it on).
        public void Join(CarriageFleet fleet, int index, Look look, float knockbackForce, float knockdownSeconds, int paint)
        {
            this.look = look;
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
                MinX = -w, MaxX = w, MinZ = -d, MaxZ = d, BodyRadius = Reach(look), Turn = Mathf.Max(.05f, turnSeconds),
                MinAim = Mathf.Max(0f, minAim), MaxAim = Mathf.Max(Mathf.Max(0f, minAim), maxAim),
                // A charge crosses most of the area's length.
                MinCharge = Mathf.Max(1f, .4f * 2f * Mathf.Max(w, d)), MaxCharge = Mathf.Max(2f, 2.5f * Mathf.Max(w, d)),
            };
            return new RoamingFleet(unchecked((uint)seed), start, new double[] { Mathf.Max(.5f, moveSpeed) },
                                    new double[] { 0 }, new double[] { 0 }, new double[] { w >= d ? 90 : 0 }, rules,
                                    pivots: new double[] { PivotOf(look) });
        }

        void Build()
        {
            if (built) return;
            built = true;
            var body = paint == 1 ? YardKit.CarriageRed : paint == 2 ? YardKit.CarriageBlue : YardKit.CarriageGreen;
            if (look == Look.Horse)
                horses = new[] { YardHorse.Build(transform, Vector3.zero, Coat(index), true) };
            else if (look == Look.Team)
                BuildTeam(body);
            else
                wheels = YardKit.Carriage(transform, body);
            if (wheels != null && wheels.Length > 0 && wheels[0] != null)
            {
                frame = wheels[0].parent;
                frameAt = frame.localPosition;
                frameTurn = frame.localRotation;
                wheelAt = new Vector3[wheels.Length];
                wheelWas = new Vector3[wheels.Length];
                wheelSpin = new float[wheels.Length];
                wheelFront = new bool[wheels.Length];
                for (int i = 0; i < wheels.Length; i++)
                {
                    wheelAt[i] = wheels[i].localPosition;
                    wheelFront[i] = wheels[i].localPosition.z > 0f;
                }
            }
            impact = YardKit.BodyCollider(transform, new Vector3(WidthOf(look), HeightOf(look), LengthOf(look)), knockbackForce, seeThrough: true);
            impact.knockdownSeconds = knockdownSeconds;
            shell = impact.GetComponent<BoxCollider>();
            shellSize = shell.size;
            shellCentre = shell.center;
            see = impact.solid != null ? impact.solid.GetComponent<CameraSeeThrough>() : null;
            if (look == Look.Team && fore != null && impact.solid != null) BuildPairBox();
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
                HorseSounds();   // 2026-11-17: hooves and the call before a charge (RoamingCarriage.Sound.cs; its hits: YardSoundscape.Hits.cs)
            }
        }

        // R686: the team's box split at the pair's tails - the coach and its pole on the body, the pair on the forecarriage
        // (switched on only while it swings out).
        void BuildPairBox()
        {
            bodyBox = impact.solid;
            bodySize = bodyBox.size;
            bodyCentre = bodyBox.center;
            float half = LengthOf(Look.Team) * .5f, tails = half - YardHorse.Length;
            coachSize = new Vector3(bodySize.x, bodySize.y, tails + half);
            coachCentre = new Vector3(bodyCentre.x, bodyCentre.y, (tails - half) * .5f);
            var go = new GameObject("Pair (collider)");
            go.transform.SetParent(fore, false);
            pairBox = go.AddComponent<BoxCollider>();
            float height = YardHorse.Height;
            pairBox.center = new Vector3(0f, height * .5f, (tails + half) * .5f - kingpin.z);
            pairBox.size = new Vector3(bodySize.x, height, half - tails);
            go.AddComponent<NoClimbSurface>();
            pairSee = go.AddComponent<CameraSeeThrough>();
            pairSee.enabled = false;
            pairBox.enabled = false;
        }

        // Loose horses and team pairs take turns of coat, so the yard is not all one horse.
        static YardHorse.Coat Coat(int i) => (YardHorse.Coat)(((i % 3) + 3) % 3);

        // A team: the carriage at the back of the body's length, a pair of harnessed horses in front of it, the pole
        // and traces between them (pictures; the body's one box is over all of it).
        void BuildTeam(Material body)
        {
            float length = LengthOf(Look.Team), back = -length * .5f + YardKit.CarriageLength * .5f;
            cab = new GameObject("Team carriage").transform;
            cab.SetParent(transform, false);
            cab.localPosition = new Vector3(0f, 0f, back);
            cabAt = cab.localPosition;
            wheels = YardKit.Carriage(cab, body);
            // R683: the forecarriage turns about the front axle's king pin; the pair and the pole ride it.
            kingpin = new Vector3(0f, 0f, back + 1.3f);
            fore = new GameObject("Forecarriage").transform;
            fore.SetParent(transform, false);
            fore.localPosition = kingpin;
            float front = length * .5f - YardHorse.Length * .5f;
            var coat = Coat(index);
            horses = new[]
            {
                YardHorse.Build(fore, new Vector3(-.55f, 0f, front) - kingpin, coat, false, "Horse W"),
                YardHorse.Build(fore, new Vector3(.55f, 0f, front) - kingpin, coat, false, "Horse E"),
            };
            const YardKit.Solid none = YardKit.Solid.None;
            float nose = back + YardKit.CarriageLength * .5f, chest = front + 1.1f;
            // The pole between the pair from the carriage's nose to their chests, a swingle-tree, the traces.
            YardKit.Box(fore, "Pole", new Vector3(0f, .95f, (nose + chest) * .5f) - kingpin, new Vector3(.12f, .12f, chest - nose), YardKit.WoodDark, none);
            YardKit.Box(fore, "Swingle tree", new Vector3(0f, .95f, nose + .1f) - kingpin, new Vector3(1.7f, .1f, .1f), YardKit.WoodDark, none);
            YardKit.Box(fore, "Pole tip", new Vector3(0f, .95f, chest) - kingpin, new Vector3(.16f, .16f, .16f), YardKit.Gold, none);
            foreach (float x in new[] { -.85f, -.25f, .25f, .85f })
                YardKit.Box(fore, "Trace", new Vector3(x, 1.05f, (nose + front + .5f) * .5f) - kingpin, new Vector3(.05f, .05f, front + .5f - nose), YardKit.WoodDark, none);
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
            if (pairSee != null && pairSee.enabled != charging) pairSee.enabled = charging;
            Swing(LockAt(pose, ObstacleClock.Now + phase));
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
            var pose = Now;
            double clock = ObstacleClock.Now + phase;
            Motion(clock, out float turn, out float accel, out float ground);
            float lockNow = LockAt(pose, clock);
            float steer = lockNow != 0f ? Swing(lockNow) : Bend(turn);
            // R686: driving its turn round a circle it moves too (the plan's charge speed is 0 then): the springs and the
            // wheels' rumble go by how fast it really goes.
            float going = Mathf.Max((float)pose.Speed, ground);
            Quaternion sway = Sway(clock, turn, accel, Mathf.Clamp01(going / Mathf.Max(1f, TopSpeed)));
            Roll(sway, steer);
            if (horses != null)
            {
                // R686 (승규 11-17 "말 도는 것을 진짜 자연스럽게"): each horse is handed its own way over the ground and how
                // fast it turns, read off the plan either side of now (where its middle is: the body's pose, the
                // forecarriage's lock or bend, its place on it) - a team's pair walking round the rear axle, swung out on
                // the lock, or side-stepping round a turn on the spot; a loose horse turning on its middle - and how far
                // it still has to turn to face the charge (turning, then aiming), which its head leads. Through its aim a
                // loose horse rears (a team's pair toss their heads), read from the plan, so every PC draws the same (kept:
                // 승규 11-17 "말 빨간 램프랑 말 동작은 유지 해야지").
                float warned = pose.Stage == RoamingFleet.Stage.Aiming && pose.StageEnd > pose.StageStart
                    ? (float)((ObstacleClock.Now + phase - pose.StageStart) / (pose.StageEnd - pose.StageStart)) : -1f;
                float rear = YardHorse.Rear(warned) * (look == Look.Horse ? 1f : .3f);
                bool facing = pose.Stage == RoamingFleet.Stage.Turning || pose.Stage == RoamingFleet.Stage.Aiming;
                foreach (var horse in horses)
                {
                    if (horse == null || horse.Root == null) continue;
                    HorseWay(clock, horse.Root.localPosition, out var way, out float horseTurn, out float horseYaw);
                    float ahead = facing ? Mathf.Clamp(Mathf.DeltaAngle(horseYaw, (float)pose.Heading), -40f, 40f) : 0f;
                    horse.Move(ObstacleClock.Now, rear, way, Mathf.Max(1f, TopSpeed), horseTurn, ahead);
                }
            }
            bool warning = pose.Stage == RoamingFleet.Stage.Turning || pose.Stage == RoamingFleet.Stage.Aiming || pose.Stage == RoamingFleet.Stage.Charging;
            if (lamps != null)
            {
                var material = warning ? YardKit.Warning : YardKit.Lamp;   // red from the turn to the end of a charge (kept)
                foreach (var lamp in lamps)
                    if (lamp != null && lamp.sharedMaterial != material) lamp.sharedMaterial = material;
            }
            DrawLine(pose, warning);
            if (rumble != null)
            {
                // Kept going from here: switched off and on again, or after an audio reset, it starts once more.
                if (!rumble.isPlaying && rumble.isActiveAndEnabled) rumble.Play();
                float share = Mathf.Clamp01(going / Mathf.Max(1f, TopSpeed));
                rumble.volume = .5f * share;   // R654: the effects volume is the gate's (SectionSoundGate)
                rumble.pitch = (look == Look.Horse ? 1.25f : .75f) + .45f * share;
            }
            if (crack != null && pose.Stage == RoamingFleet.Stage.Charging && shown != RoamingFleet.Stage.Charging)
            {
                crack.volume = 1f;
                crack.PlayOneShot(crack.clip, .8f);
            }
            Calls(pose);   // 2026-11-17: a snort or a neigh as its aim starts (RoamingCarriage.Sound.cs)
            shown = pose.Stage;
        }

        // R683: how fast it turns (degrees a second, + to the right) and speeds up (m/s a second), off the plan either
        // side of `time` (smooth: the plan eases every turn and every charge in and out); R686: and how fast its middle
        // goes over the ground (m/s: a charge, or a turn driven round a circle).
        void Motion(double time, out float turn, out float accel, out float ground)
        {
            turn = accel = ground = 0f;
            var plan = Plan;
            if (plan == null) return;
            const double h = .06;
            var a = plan.At(index, time - h);
            var b = plan.At(index, time + h);
            turn = Mathf.DeltaAngle((float)a.Yaw, (float)b.Yaw) / (float)(2.0 * h);
            accel = (float)((b.Speed - a.Speed) / (2.0 * h));
            // A cut to a new plan (a jump of metres) is no speed.
            ground = (float)(System.Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z)) / (2.0 * h));
            if (float.IsNaN(turn) || float.IsInfinity(turn)) turn = 0f;
            if (float.IsNaN(accel) || float.IsInfinity(accel)) accel = 0f;
            if (float.IsNaN(ground) || float.IsInfinity(ground) || ground > 2f * TopSpeed) ground = 0f;
        }

        // R686: how far a team's forecarriage swings out at `time` in a turn round its turning point (degrees, + to the
        // right; 0 off such a turn - turning on the spot it bends, Bend - and for a loose horse or a lone carriage).
        float LockAt(in RoamingFleet.Pose pose, double time)
        {
            if (look != Look.Team || fore == null || pose.Stage != RoamingFleet.Stage.Turning || (pose.Pivot <= 0 && pose.Side == 0)
                || !(pose.StageEnd > pose.StageStart)) return 0f;
            float u = Mathf.Clamp01((float)((time - pose.StageStart) / (pose.StageEnd - pose.StageStart)));
            float delta = Mathf.DeltaAngle((float)pose.HeadingFrom, (float)pose.Heading);
            float round = Mathf.Min(LockMost, Mathf.Atan2(PairReach, Mathf.Abs((float)pose.Side)) * Mathf.Rad2Deg);
            float most = Mathf.Sign(delta) * Mathf.Min(round, Mathf.Abs(delta) * LockPerTurn);
            return most * Ease(u / LockEase) * Ease((1f - u) / LockEase);
        }

        // From the rear axle to the pair's middle along the body (m): the wheelbase, then the king pin to the pair.
        float PairReach => 2f * RearAxle + (LengthOf(Look.Team) * .5f - YardHorse.Length * .5f - kingpin.z);

        static float Ease(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        // R686: the forecarriage swung to `lockDeg` about the king pin, and the boxes for it: swung out, the pair's own box
        // on the forecarriage and the body's box round the coach only; straight, the one long box. Returns the steer.
        float Swing(float lockDeg)
        {
            if (fore == null || cab == null) return 0f;
            Hitch();
            fore.localRotation = Quaternion.Euler(0f, lockDeg, 0f);
            cab.localRotation = Quaternion.identity;
            cab.localPosition = cabAt;
            if (pairBox != null && bodyBox != null)
            {
                bool split = Mathf.Abs(lockDeg) > 5f;   // (the on-the-spot bend, 4 degrees at most, keeps the one box)
                if (pairBox.enabled != split) pairBox.enabled = split;
                var size = split ? coachSize : bodySize;
                if (bodyBox.size != size)
                {
                    bodyBox.size = size;
                    bodyBox.center = split ? coachCentre : bodyCentre;
                }
            }
            return lockDeg;
        }

        // The dressed pole and traces (RcyHitch, hung on the team by the dressing) move onto the forecarriage the first
        // time they are seen.
        void Hitch()
        {
            if (hitchModel != null) return;
            var hung = transform.Find("RcyHitch (model)");
            if (hung == null) return;
            var turn = fore.localRotation;
            fore.localRotation = Quaternion.identity;
            hung.SetParent(fore, true);
            hitchModel = hung;
            fore.localRotation = turn;
        }

        // R686: a horse's way over the ground (its own frame, m/s), how fast it turns (degrees a second) and which way it
        // faces (the plan's frame), off the plan either side of `time`: the body's pose, the forecarriage's lock (or its
        // bend turning on the spot), and the horse's place on the forecarriage (`local`; a loose horse: at the middle).
        void HorseWay(double time, Vector3 local, out Vector3 way, out float turnRate, out float yaw)
        {
            way = Vector3.zero;
            turnRate = yaw = 0f;
            var plan = Plan;
            if (plan == null) return;
            const double h = .04;
            HorseAt(plan, time - h, local, out var a, out float ya);
            HorseAt(plan, time + h, local, out var b, out float yb);
            HorseAt(plan, time, local, out _, out yaw);
            var v = (b - a) / (float)(2.0 * h);
            way = Quaternion.Euler(0f, -yaw, 0f) * new Vector3(v.x, 0f, v.y);
            turnRate = Mathf.DeltaAngle(ya, yb) / (float)(2.0 * h);
            if (float.IsNaN(way.x) || float.IsNaN(way.z) || float.IsNaN(turnRate)) { way = Vector3.zero; turnRate = 0f; }
        }

        void HorseAt(RoamingFleet plan, double time, Vector3 local, out Vector2 at, out float yaw)
        {
            var p = plan.At(index, time);
            float swing = 0f;
            if (fore != null)
            {
                swing = LockAt(p, time);
                if (swing == 0f && p.Stage == RoamingFleet.Stage.Turning)
                {
                    // On the spot: the bend the turn's rate gives (Bend).
                    var a = plan.At(index, time - .06);
                    var b = plan.At(index, time + .06);
                    swing = Mathf.Clamp(Mathf.DeltaAngle((float)a.Yaw, (float)b.Yaw) / .12f * BendPerTurn, -BendMost, BendMost);
                }
            }
            var inBody = fore != null ? kingpin + Quaternion.Euler(0f, swing, 0f) * local : local;
            var w = Quaternion.Euler(0f, (float)p.Yaw, 0f) * inBody;
            at = new Vector2((float)p.X + w.x, (float)p.Z + w.z);
            yaw = (float)p.Yaw + swing;
        }

        // R683: a team bends at its king pin, the forecarriage leading into the turn (R684: the carriage no longer
        // trails, at most 4 degrees: see BendMost). The dressed pole and traces
        // (RcyHitch, hung on the team by the dressing) move onto the forecarriage the first time they are seen. Returns
        // the front wheels' steer against the carriage.
        float Bend(float turn) => Swing(Mathf.Clamp(turn * BendPerTurn, -BendMost, BendMost));

        // R683: the carriage (its frame, body and lamps - not its wheels) on its springs, tipped about a point at spring
        // height: back as it speeds up and forward as it slows, out of a turn, and a little shake that grows with
        // speed. Returns the tip, which the wheels take back out.
        Quaternion Sway(double clock, float turn, float accel, float share)
        {
            if (frame == null) return Quaternion.identity;
            float t = (float)(clock % 1000.0);
            // R684: at most ~2.5 degrees of pitch and 1.5 of roll, so the roof's ends and edges move under 10 cm
            float pitch = Mathf.Clamp(-accel * .4f, -2.2f, 2.2f) + .3f * share * Mathf.Sin(t * 17f);
            float roll = Mathf.Clamp(turn * .012f, -1.2f, 1.2f) + .25f * share * Mathf.Sin(t * 13f + 1.3f);
            var tip = Quaternion.Euler(pitch, 0f, roll);
            frame.localRotation = frameTurn * tip;
            frame.localPosition = frameAt + frameTurn * (SpringAt - tip * SpringAt) + Vector3.up * (.012f * share * Mathf.Sin(t * 23f));
            return tip;
        }

        // R683: each wheel back where it stands unsprung, the front pair steered with the forecarriage, and turned by
        // how far its own hub went along its own forward since the last frame (a jump to a new plan turns nothing).
        void Roll(Quaternion tip, float steer)
        {
            if (wheels == null || frame == null || wheelAt == null) return;
            var untip = Quaternion.Inverse(tip);
            var frameRot = frameTurn * tip;
            float radius = YardKit.CarriageWheel * .5f;
            for (int i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];
                if (wheel == null) continue;
                var turnTo = Quaternion.Euler(0f, wheelFront[i] ? steer : 0f, 0f);
                wheel.localPosition = Quaternion.Inverse(frameRot) * (frameAt + frameTurn * wheelAt[i] - frame.localPosition);
                var now = wheel.position;
                if (wheelsPlaced)
                {
                    var forward = frame.rotation * untip * turnTo * Vector3.forward;
                    var d = now - wheelWas[i];
                    if (d.sqrMagnitude < 4f)
                        wheelSpin[i] = (wheelSpin[i] + Vector3.Dot(d, forward) / radius * Mathf.Rad2Deg) % 360f;
                }
                wheelWas[i] = now;
                wheel.localRotation = untip * turnTo * Quaternion.Euler(wheelSpin[i], 0f, 0f);
            }
            wheelsPlaced = true;
        }

        // From the carriage's centre to where its nose stops, flat on the floor; brighter and pulsing while it aims.
        void DrawLine(RoamingFleet.Pose pose, bool warning)
        {
            if (lineRenderer == null) return;
            var from = new Vector3((float)pose.X, 0f, (float)pose.Z);
            var ahead = new Vector3((float)pose.ToX, 0f, (float)pose.ToZ) - from;
            float length = ahead.magnitude;
            if (!warning || length < .1f || look != Look.Carriage)   // R685: no line under a team or a loose horse
            {
                if (lineRenderer.enabled) lineRenderer.enabled = false;
                return;
            }
            var way = ahead / length;
            float reach = length + LengthOf(look) * .5f;
            // The quad lies in its x-y plane: turned to lie flat with its y along the charge.
            line.SetPositionAndRotation(StartPosition + StartRotation * (from + way * (reach * .5f)) + Vector3.up * .03f,
                                        StartRotation * Quaternion.LookRotation(way) * Quaternion.Euler(90f, 0f, 0f));
            var scale = line.parent != null ? line.parent.lossyScale : Vector3.one;
            line.localScale = new Vector3(Line / Mathf.Max(1e-4f, scale.x), reach / Mathf.Max(1e-4f, scale.z), 1f);
            // Faint through the turn, deepening through the aim (no flashing), on the shared clock like the rest.
            float aimed = pose.Stage == RoamingFleet.Stage.Aiming && pose.StageEnd > pose.StageStart
                ? (float)((ObstacleClock.Now + phase - pose.StageStart) / (pose.StageEnd - pose.StageStart)) : 0f;
            float alpha = pose.Stage == RoamingFleet.Stage.Turning ? .12f
                        : pose.Stage == RoamingFleet.Stage.Aiming ? .16f + .2f * YardWarn.Deepen(aimed)
                        : .3f;
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
            float r = Reach(look);
            Gizmos.DrawWireCube(new Vector3(0f, HeightOf(look) * .5f, 0f), new Vector3((halfWidth + r) * 2f, HeightOf(look), (halfDepth + r) * 2f));
        }
    }
}
