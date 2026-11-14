using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Carriage Yard's stable chargers (R532, the loading yard in the centre way; the user's "갑자기 돌진하는 마차"
    // and "흥분해서 빠르게 돌진하는 말"): a carriage or a horse that stands in its stable facing the way, bolts out across it
    // to endOffset, stops, and goes home again - a carriage backing up, a horse turning round and trotting back, or with
    // returnBolts galloping back as hard as it came. Its pose is a pure function of ObstacleClock (HANDOFF absolute rule 7),
    // so every PC sees it in the same place without a packet.
    //
    // It warns before every bolt, read from the same clock: for `warning` seconds a red line on the floor shows where it
    // will run, a carriage's lamps go big and red, a horse rears. Only a bolt knocks, and once a bolt (IHitTurns; YardImpact:
    // mostly out to the side, the speed set, the part up the course cut to 60 %, as the roaming carriages); standing,
    // turning or going home slowly it is only a body that shoves. While it bolts its hit shell reaches up to Lead ahead of
    // its nose, so whoever it knocks aside is clear before the nose gets there, and the follow camera looks through it.
    // A horse turns round slowly (turnSeconds, 1.6 s on the map: its ends swing at about 4 m/s, as R287 slowed the roaming
    // carriages' turns), since it only shoves while it turns.
    //
    // Self-building: its own position is the stable (the body's centre on the floor), its nose local +z. Built from code
    // on an inactive object: call Configure (and SetPhase) before switching it on. The stable around it is the caller's:
    // leave at least a pawn's room (0.6 m) behind where it comes home, so nobody is ever pinned there.
    //
    // R659 (zone 2, the sorting yard's machines): a cart is a loaded cargo cart on a track - it runs out to the end, holds,
    // and with returnBolts runs back as hard as it came without turning (a cart has two noses: its shell reaches ahead of
    // whichever end leads), warned by its red beacon and the line both ways.
    public sealed class StableCharger : Obstacle, IHitTurns
    {
        public enum Look { Carriage, Horse, Cart }

        public const float CartLength = 2.6f, CartWidth = 1.7f, CartHeight = 1.9f;
        public enum Stage { Waiting, Bolting, Holding, Returning }

        // How far the hit shell reaches ahead of the nose while bolting (m): a 7 m/s push sideways clears the body in the
        // ~0.2 s the nose takes to get there (the roaming carriages' Lead).
        public const float Lead = 1.5f;
        // Time shares of the bolt spent speeding up and slowing down, and of the way home.
        const float SpeedUp = .25f, SlowDown = .15f, HomeEase = .3f;
        public const float HorseLength = YardHorse.Length, HorseWidth = YardHorse.Width, HorseHeight = YardHorse.Height;
        static readonly Color LineColor = new Color(1f, .22f, .16f, 1f);

        [SerializeField] Look look;
        [Header("Path")]
        [Tooltip("Where the bolt ends, from the stable, in its own axes (its nose is +z)")]
        [SerializeField] Vector3 endOffset = new Vector3(0f, 0f, 15f);
        [Tooltip("Top speed of the bolt (m/s)")]
        [SerializeField] float moveSpeed = 9f;
        [Tooltip("Top speed going home (m/s)")]
        [SerializeField] float returnSpeed = 4f;
        [Tooltip("Seconds it stands in its stable between bolts (the warning is the last of them)")]
        [SerializeField] float waitAtStart = 2.6f;
        [Tooltip("Seconds it stands at the end of the bolt before going home (a horse turns round in them)")]
        [SerializeField] float holdAtEnd = .8f;
        [Tooltip("Seconds of red line, red lamps or rearing before the bolt")]
        [SerializeField] float warning = 1f;
        [Tooltip("A horse's half turn at either end (s)")]
        [SerializeField] float turnSeconds = 1.6f;
        [Tooltip("Goes home as hard as it came, warned and knocking the same way (a horse: it turns round first)")]
        [SerializeField] bool returnBolts;
        [Header("Hit")]
        [Tooltip("Push of a hit (m/s) before the speed adds 0.25 per m/s, at most 7; it sets the character's speed")]
        [SerializeField] float knockbackForce = 4f;
        [SerializeField] float knockdownSeconds = 1f;
        [Tooltip("Carriage body colour: 0 green, 1 red, 2 blue")]
        [SerializeField] int paint = 2;
        [SerializeField] bool sounds = true;

        Transform[] wheels;
        Renderer[] lamps;
        Material lampLit, lampWarn;
        YardHorse horse;
        YardImpact impact;
        BoxCollider shell;
        Vector3 shellSize, shellCentre;
        CameraSeeThrough see;
        Transform line;
        Renderer lineRenderer;
        Material lineMaterial;
        AudioSource rumble, crack;
        Stage shown;
        bool built;

        public Look Kind => look;
        public float CycleSeconds => waitAtStart + BoltSeconds + holdAtEnd + HomeSeconds;
        public float Distance => endOffset.magnitude;
        public float BoltSeconds => Distance * PeakFactor(SpeedUp, SlowDown) / Mathf.Max(.1f, moveSpeed);
        float HomeSeconds => returnBolts ? BoltSeconds : Distance * PeakFactor(HomeEase, HomeEase) / Mathf.Max(.1f, returnSpeed);
        float HalfLength => look == Look.Horse ? HorseLength * .5f : look == Look.Cart ? CartLength * .5f : YardKit.CarriageLength * .5f;
        float BodyWidth => look == Look.Horse ? HorseWidth : look == Look.Cart ? CartWidth : YardKit.CarriageWidth;
        float BodyHeight => look == Look.Horse ? HorseHeight : look == Look.Cart ? CartHeight : YardKit.CarriageHeight;
        // Time it stands at home, and at the end, before it goes: a horse turns round first, then the warning.
        float Lull => warning + (look == Look.Horse ? turnSeconds : 0f);

        // One turn per bolt (IHitTurns): a character is knocked down once a bolt, as by the roaming carriages (R287). The
        // way out is the first turn of a cycle, the way home (with returnBolts) the second.
        public double HitTurn
        {
            get
            {
                double now = ObstacleClock.Now + phase, cycle = Mathf.Max(.01f, CycleSeconds);
                var stage = At(now, out _);
                return System.Math.Floor(now / cycle) * 2.0 + (stage == Stage.Holding || stage == Stage.Returning ? 1.0 : 0.0);
            }
        }

        public void Configure(Look look, Vector3 endOffset, float moveSpeed, float returnSpeed, float waitAtStart, float holdAtEnd,
                              float warning, float knockbackForce, float knockdownSeconds, int paint = 2, float turnSeconds = 1.6f,
                              bool returnBolts = false)
        {
            this.look = look;
            this.endOffset = endOffset;
            this.moveSpeed = moveSpeed;
            this.returnSpeed = returnSpeed;
            this.warning = warning;
            this.knockbackForce = knockbackForce;
            this.knockdownSeconds = knockdownSeconds;
            this.paint = paint;
            this.turnSeconds = turnSeconds;
            // Only a horse turns round to run home nose first; a carriage backs up; a cart runs back either way (R659).
            this.returnBolts = returnBolts && look != Look.Carriage;
            this.waitAtStart = Mathf.Max(Lull, waitAtStart);
            this.holdAtEnd = this.returnBolts ? Mathf.Max(Lull, holdAtEnd) : look == Look.Horse ? Mathf.Max(turnSeconds, holdAtEnd) : holdAtEnd;
        }

        protected override void Awake()
        {
            Build();
            base.Awake();
            BuildLine();
        }

        void Build()
        {
            if (built) return;
            built = true;
            if (look == Look.Horse)
            {
                horse = YardHorse.Build(transform, Vector3.zero);
                var body = new GameObject("Horse (collider)");
                body.transform.SetParent(transform, false);
                var box = body.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, (HorseHeight + .05f) * .5f, 0f);
                box.size = new Vector3(HorseWidth, HorseHeight - .05f, HorseLength);
                body.AddComponent<NoClimbSurface>();
                body.AddComponent<CameraSeeThrough>().enabled = false;
                impact = YardKit.Shell(transform, "Horse (hit)", box.center, box.size + new Vector3(.5f, .2f, .5f), knockbackForce);
                impact.solid = box;
                impact.onFloor = true;
            }
            else if (look == Look.Cart)
            {
                wheels = BuildCart();
                impact = YardKit.BodyCollider(transform, new Vector3(CartWidth, CartHeight, CartLength), knockbackForce, seeThrough: true);
                var found = new System.Collections.Generic.List<Renderer>();
                foreach (var r in GetComponentsInChildren<Renderer>())
                    if (r.name == "Lamp") found.Add(r);
                lamps = found.ToArray();
                lampLit = YardKit.Lamp;
                lampWarn = YardKit.Warning;
            }
            else
            {
                wheels = YardKit.Carriage(transform, paint == 1 ? YardKit.CarriageRed : paint == 2 ? YardKit.CarriageBlue : YardKit.CarriageGreen);
                impact = YardKit.CarriageCollider(transform, knockbackForce, seeThrough: true);
                // Big lamps, seen from across the way.
                var found = new System.Collections.Generic.List<Renderer>();
                foreach (var r in GetComponentsInChildren<Renderer>())
                    if (r.name == "Lamp")
                    {
                        r.transform.localScale *= 1.8f;
                        found.Add(r);
                    }
                lamps = found.ToArray();
                // Fetched once: the materials are looked up by name, which builds a string each time.
                lampLit = YardKit.Lamp;
                lampWarn = YardKit.Warning;
            }
            impact.knockdownSeconds = knockdownSeconds;
            // About 6.5 m/s at a full bolt, mostly out to the side, the speed set, the part up the course (the +z of
            // whatever holds the stable) cut to 60 %: a hit costs a second or two, never a free ride on (the roaming
            // carriages' numbers, R287).
            impact.speedScale = .25f;
            impact.maxPush = 7f;
            impact.lift = 1.2f;
            impact.alongShare = .3f;
            impact.replaceSpeed = true;
            impact.course = transform.parent;
            impact.forwardShare = .6f;
            impact.cooldown = .2f;
            impact.minSpeed = 99f;
            shell = impact.GetComponent<BoxCollider>();
            shellSize = shell.size;
            shellCentre = shell.center;
            see = impact.solid != null ? impact.solid.GetComponent<CameraSeeThrough>() : null;
            if (sounds)
            {
                rumble = Voice(YardKit.RumbleSound, true);
                crack = Voice(YardKit.CrackSound, false);
            }
        }

        // The bolt's line: one flat sheet on the floor from the stable to where the nose stops, beside the body (it must
        // not move with it), shown only while it warns and bolts.
        void BuildLine()
        {
            lineMaterial = new Material(Arena.Glow(LineColor)) { name = "M_YardChargeLine" };
            var strip = GameObject.CreatePrimitive(PrimitiveType.Quad);
            strip.name = "Bolt line (" + name + ")";
            DestroyImmediate(strip.GetComponent<Collider>());
            strip.transform.SetParent(transform.parent, false);
            line = strip.transform;
            lineRenderer = strip.GetComponent<Renderer>();
            lineRenderer.sharedMaterial = lineMaterial;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.enabled = false;
            var way = (StartRotation * endOffset).normalized;
            float reach = Distance + HalfLength;
            // The quad lies in its x-y plane: turned to lie flat with its y along the bolt.
            line.SetPositionAndRotation(StartPosition + way * (reach * .5f) + Vector3.up * .03f,
                                        Quaternion.LookRotation(way) * Quaternion.Euler(90f, 0f, 0f));
            var scale = line.parent != null ? line.parent.lossyScale : Vector3.one;
            line.localScale = new Vector3((BodyWidth + .5f) / Mathf.Max(1e-4f, scale.x), reach / Mathf.Max(1e-4f, scale.z), 1f);
        }

        // A loaded cargo cart on a track, its length along local z, standing on y 0: a low bed on four iron wheels, crates
        // and a sack under a strap, buffers at both ends and a red beacon on a post (its warning). Pictures only; the
        // collider is one box over all of it. Returns the wheel pivots.
        Transform[] BuildCart()
        {
            var c = new GameObject("Cart").transform;
            c.SetParent(transform, false);
            const YardKit.Solid none = YardKit.Solid.None;
            YardKit.Box(c, "Cart bed", new Vector3(0f, .62f, 0f), new Vector3(CartWidth - .1f, .26f, CartLength), YardKit.WoodDark, none);
            YardKit.Box(c, "Cart side", new Vector3(0f, .9f, 0f), new Vector3(CartWidth, .3f, CartLength - .2f), YardKit.WoodLight, none);
            foreach (float z in new[] { -1f, 1f })
            {
                YardKit.Box(c, "Buffer", new Vector3(0f, .6f, z * (CartLength * .5f - .02f)), new Vector3(1.1f, .22f, .1f), YardKit.Iron, none);
                YardKit.Box(c, "Buffer trim", new Vector3(0f, .74f, z * (CartLength * .5f - .02f)), new Vector3(1.12f, .05f, .11f), YardKit.Gold, none);
            }
            YardKit.Box(c, "Crate", new Vector3(-.3f, 1.42f, -.5f), new Vector3(.9f, .9f, .9f), YardKit.WoodLight, none);
            YardKit.Box(c, "Crate band", new Vector3(-.3f, 1.42f, -.5f), new Vector3(.93f, .12f, .93f), YardKit.Banner, none);
            YardKit.Box(c, "Crate", new Vector3(.35f, 1.3f, .55f), new Vector3(.8f, .66f, .8f), YardKit.WoodDark, none);
            var sack = YardKit.Ball(c, "Sack", new Vector3(.4f, 1.22f, -.45f), .7f, Arena.Lit("M_YardSack", Arena.Hex("#CDB48A"), 0f, .95f), none);
            sack.transform.localScale = new Vector3(.6f, .5f, .8f);
            YardKit.Box(c, "Strap", new Vector3(0f, 1.45f, 0f), new Vector3(CartWidth + .02f, .05f, .12f), YardKit.WoodDark, none);
            YardKit.Box(c, "Beacon post", new Vector3(-.7f, 1.4f, .95f), new Vector3(.1f, .9f, .1f), YardKit.Iron, none);
            YardKit.Box(c, "Lamp", new Vector3(-.7f, 1.9f, .95f), new Vector3(.3f, .3f, .3f), YardKit.Lamp, none);
            var wheels = new Transform[4];
            int n = 0;
            foreach (float z in new[] { -.8f, .8f })
                foreach (float side in new[] { -1f, 1f })
                {
                    var pivot = new GameObject("Wheel").transform;
                    pivot.SetParent(c, false);
                    pivot.localPosition = new Vector3(.62f * side, .32f, z);
                    YardKit.Cylinder(pivot, "Tyre", Vector3.zero, Vector3.right, .64f, .12f, YardKit.Iron, none);
                    YardKit.Box(pivot, "Spoke", Vector3.zero, new Vector3(.05f, .56f, .06f), YardKit.WoodDark, none);
                    YardKit.Box(pivot, "Spoke", Vector3.zero, new Vector3(.05f, .06f, .56f), YardKit.WoodDark, none);
                    wheels[n++] = pivot;
                }
            return wheels;
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
            SectionSoundGate.Gate(a);   // R529 / R654: the effects volume is the gate's
            a.volume = 0f;
            return a;
        }

        // The line is not the body's child: hide it with the body, and take it away with it.
        void OnDisable()
        {
            if (lineRenderer != null) lineRenderer.enabled = false;
        }

        void OnDestroy()
        {
            if (line != null) Destroy(line.gameObject);
            if (lineMaterial != null) Destroy(lineMaterial);
        }

        // ------------------------------------------------------------------ the clock

        // Which stage the cycle is in at `time` and how far into it (s).
        Stage At(double time, out float into)
        {
            float cycle = CycleSeconds;
            float t = cycle > 0f ? (float)(time - System.Math.Floor(time / cycle) * cycle) : 0f;
            if (t < waitAtStart) { into = t; return Stage.Waiting; }
            t -= waitAtStart;
            float bolt = BoltSeconds;
            if (t < bolt) { into = t; return Stage.Bolting; }
            t -= bolt;
            if (t < holdAtEnd) { into = t; return Stage.Holding; }
            into = t - holdAtEnd;
            return Stage.Returning;
        }

        // 0 in the stable, 1 at the end of the bolt.
        float Along(Stage stage, float into)
        {
            switch (stage)
            {
                case Stage.Bolting: return Profile(into / BoltSeconds, SpeedUp, SlowDown);
                case Stage.Holding: return 1f;
                case Stage.Returning:
                    return returnBolts ? 1f - Profile(into / HomeSeconds, SpeedUp, SlowDown) : 1f - Profile(into / HomeSeconds, HomeEase, HomeEase);
                default: return 0f;
            }
        }

        // Running hard (knocking): the bolt out, and with returnBolts the run home.
        bool Running(Stage stage) => stage == Stage.Bolting || (returnBolts && stage == Stage.Returning);

        // How far into the warning before a run (0..1), or -1 outside one: the last `warning` seconds at home, and with
        // returnBolts at the end.
        float Warned(Stage stage, float into)
        {
            float w = Mathf.Max(.05f, warning);
            if (stage == Stage.Waiting && into > waitAtStart - w) return (into - (waitAtStart - w)) / w;
            if (returnBolts && stage == Stage.Holding && into > holdAtEnd - w) return (into - (holdAtEnd - w)) / w;
            return -1f;
        }

        // A horse's heading about y from facing out (0) to facing home (180): it turns round at the end of the bolt and
        // again as it gets home, the same way both times (a whole turn a cycle). A carriage always faces out and backs up.
        float Heading(Stage stage, float into)
        {
            if (look != Look.Horse) return 0f;
            float turn = Mathf.Max(.05f, turnSeconds);
            switch (stage)
            {
                case Stage.Waiting: return into < turn ? 180f + 180f * Smooth(into / turn) : 0f;
                case Stage.Holding: return 180f * Smooth(into / turn);
                case Stage.Returning: return 180f;
                default: return 0f;
            }
        }

        static float Smooth(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            var stage = At(time, out float into);
            position = StartPosition + StartRotation * endOffset * Along(stage, into);
            rotation = StartRotation * Quaternion.Euler(0f, Heading(stage, into), 0f);
        }

        // Distance fraction for a speed that rises over the first `up` of the time, holds, and falls over the last
        // `down` (a trapezoid, its area 1).
        static float Profile(float u, float up, float down)
        {
            u = Mathf.Clamp01(u);
            float peak = PeakFactor(up, down);
            if (u < up) return peak * u * u / (2f * up);
            if (u < 1f - down) return peak * (up * .5f + (u - up));
            float left = 1f - u;
            return 1f - peak * left * left / (2f * down);
        }

        // Top speed over average speed for that trapezoid.
        static float PeakFactor(float up, float down) => 1f / (1f - (up + down) * .5f);

        // ------------------------------------------------------------------ the knock

        // Only a run knocks; the shell reaches ahead of the nose while it runs, never past where the nose will stop (a
        // horse running home has turned round, so its nose - local +z - leads that way too).
        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            var stage = At(ObstacleClock.Now + phase, out float into);
            bool running = Running(stage);
            if (impact != null) impact.minSpeed = running ? .4f : 99f;
            if (see != null && see.enabled != running) see.enabled = running;
            if (shell == null) return;
            float left = stage == Stage.Bolting ? 1f - Along(stage, into) : Along(stage, into);
            float ahead = running ? Mathf.Clamp(left * Distance, 0f, Lead - .25f) : 0f;
            // A cart runs back without turning: its lead goes out of its back end then (R659).
            float lead = look == Look.Cart && stage == Stage.Returning ? -1f : 1f;
            var size = new Vector3(shellSize.x, shellSize.y, shellSize.z + ahead);
            var centre = shellCentre + Vector3.forward * (lead * ahead * .5f);
            if (shell.size != size || shell.center != centre)
            {
                shell.size = size;
                shell.center = centre;
            }
        }

        // ------------------------------------------------------------------ pictures and sound

        void LateUpdate()
        {
            double now = ObstacleClock.Now + phase;
            var stage = At(now, out float into);
            float along = Along(stage, into);
            float travelled = along * Distance;
            float warned = Warned(stage, into);
            bool running = Running(stage);
            bool warn = warned >= 0f || running;
            // How fast it is going now, from the same clock (for the wheels' sound and the horse's stride).
            var before = At(now - .05, out float intoBefore);
            float speed = Mathf.Abs(travelled - Along(before, intoBefore) * Distance) / .05f;

            if (wheels != null)
            {
                float angle = travelled / ((look == Look.Cart ? .64f : YardKit.CarriageWheel) * .5f) * Mathf.Rad2Deg;
                foreach (var wheel in wheels)
                    if (wheel != null) wheel.localRotation = Quaternion.Euler(angle, 0f, 0f);
            }
            if (lamps != null)
            {
                var material = warn ? lampWarn : lampLit;
                foreach (var lamp in lamps)
                    if (lamp != null && lamp.sharedMaterial != material) lamp.sharedMaterial = material;
            }
            if (horse != null) horse.Pose(now, YardHorse.Rear(warned), speed, moveSpeed, travelled);

            if (lineRenderer != null)
            {
                if (warn != lineRenderer.enabled) lineRenderer.enabled = warn;
                if (warn)
                {
                    float pulse = (float)System.Math.Sin(ObstacleClock.Now * 14.0 % (2.0 * System.Math.PI));
                    var color = LineColor;
                    color.a = !running ? .45f + .2f * pulse : .4f;
                    lineMaterial.color = color;
                }
            }

            if (rumble != null)
            {
                float share = Mathf.Clamp01(speed / Mathf.Max(.1f, moveSpeed));
                if (share > .02f)
                {
                    if (!rumble.isPlaying && rumble.isActiveAndEnabled) rumble.Play();
                    rumble.volume = .5f * share;
                    rumble.pitch = (look == Look.Horse ? 1.25f : look == Look.Cart ? .95f : .75f) + .45f * share;
                }
                else if (rumble.isPlaying) rumble.Stop();
            }
            if (crack != null && running && stage != shown)
            {
                crack.volume = 1f;
                crack.PlayOneShot(crack.clip, .8f);
            }
            shown = stage;
        }

        void OnDrawGizmos()
        {
            var from = Application.isPlaying ? StartPosition : transform.position;
            var turn = Application.isPlaying ? StartRotation : transform.rotation;
            var to = from + turn * endOffset;
            Gizmos.color = new Color(1f, .55f, .2f, .8f);
            Gizmos.DrawLine(from + Vector3.up, to + Vector3.up);
            Gizmos.matrix = Matrix4x4.TRS(to, turn, Vector3.one);
            float h = BodyHeight;
            Gizmos.DrawWireCube(new Vector3(0f, h * .5f, 0f), new Vector3(BodyWidth, h, HalfLength * 2f));
        }
    }
}
