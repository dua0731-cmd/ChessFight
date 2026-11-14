using System;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // The giant chest on a CargoSlam's gantry (R626). Its whole trip is a pure function of ObstacleClock (HANDOFF absolute
    // rule 7): carried along the beam high over everyone to the trip's place (smoothly from the last one), held still
    // there, dropped, sitting on the floor, hoisted back up slowly - and the next trip carries it on from there. The
    // place is the trip hashed with the gantry's seed (CargoSlam.PlaceOf), the same on every PC.
    //
    // Warned twice over: from the start of the carry a dim red circle on the floor shows where it is going and how far
    // its shockwave will run; held over it, the circle burns bright and pulses until the drop. On landing the wave runs
    // out from the chest's edge to the circle's in WaveSeconds; whoever it reaches with a foot on the floor (their lowest
    // part within GroundTolerance of it: standing, running, lying) is knocked down and pushed straight out from the
    // chest, the part towards the race's way on cut to 60 % as every yard hit; whoever it reaches in the air - a jump
    // timed to the slam - or standing up on something is spared. Judged on the PC that moves the characters (offline,
    // the host: YardKit.Movable), once per character per slam, in FixedUpdate (not a physics callback: a hit lets go of
    // what the hands hold, HANDOFF absolute rule 14). Under the chest itself only its falling underside hits, as a
    // crane's crate does (YardImpact, judged by the part it touches), and the knocked-down are let through the chest
    // until it is hoisted, so nobody is left under it.
    public sealed class SlamChest : Obstacle
    {
        public enum Stage { Carried, Held, Falling, Down, Hoisted }

        static readonly Color CircleColor = new Color(1f, .2f, .12f, 1f), WaveColor = new Color(1f, .86f, .62f, 1f);
        const int CircleSegments = 64;

        [SerializeField] CargoSlam slam;

        Transform trolley, area;
        Transform[] chains;
        LineRenderer rim, wave;
        Renderer areaRenderer;
        Material areaMaterial, rimMaterial, waveMaterial;
        Transform[] dust;
        Material dustMaterial;
        ChessFight.Game.CameraSeeThrough see;
        AudioSource thud;
        bool built;
        double judgedTrip = double.NaN, thudTrip = double.NaN;
        readonly HashSet<ICharacterDriver> judged = new HashSet<ICharacterDriver>();
        readonly List<Collider> parts = new List<Collider>();

        public int Knocked { get; private set; }
        public int Spared { get; private set; }

        public void Configure(CargoSlam slam) => this.slam = slam;

        protected override void Awake()
        {
            if (slam == null) slam = GetComponentInParent<CargoSlam>();
            Build();
            base.Awake();
        }

        void Build()
        {
            if (built || slam == null) return;
            built = true;
            var s = slam.ChestSize;
            // The chest: oak planks, iron corners, gold bands round it, a royal blue lid with a crown on top.
            YardKit.Box(transform, "Chest", Vector3.zero, s, YardKit.WoodLight, Solid.None);
            YardKit.Box(transform, "Lid", new Vector3(0f, s.y * .36f, 0f), new Vector3(s.x + .06f, s.y * .3f, s.z + .06f), YardKit.Banner, Solid.None);
            foreach (float x in new[] { -.3f, .3f })
                YardKit.Box(transform, "Band", new Vector3(x * s.x, 0f, 0f), new Vector3(.2f, s.y + .05f, s.z + .08f), YardKit.Gold, Solid.None);
            YardKit.Box(transform, "Band", new Vector3(0f, -s.y * .28f, 0f), new Vector3(s.x + .08f, .16f, s.z + .08f), YardKit.Gold, Solid.None);
            foreach (float a in new[] { -1f, 1f })
                foreach (float b in new[] { -1f, 1f })
                    YardKit.Box(transform, "Corner", new Vector3(a * s.x * .5f, 0f, b * s.z * .5f), new Vector3(.24f, s.y + .04f, .24f), YardKit.Iron, Solid.None);
            YardKit.Box(transform, "Crown base", new Vector3(0f, s.y * .5f + .1f, 0f), new Vector3(.9f, .2f, .9f), YardKit.Gold, Solid.None);
            YardKit.Ball(transform, "Crown", new Vector3(0f, s.y * .5f + .45f, 0f), .55f, YardKit.Gold, Solid.None);
            YardKit.Box(transform, "Lock", new Vector3(0f, s.y * .12f, -s.z * .5f - .05f), new Vector3(.5f, .6f, .1f), YardKit.Gold, Solid.None);

            var body = new GameObject("Giant chest (collider)");
            body.transform.SetParent(transform, false);
            var box = body.AddComponent<BoxCollider>();
            box.size = s;
            body.AddComponent<NoClimbSurface>();
            see = body.AddComponent<ChessFight.Game.CameraSeeThrough>();
            see.enabled = false;

            // Its falling underside, as a crane's crate (CraneCrate): straight out from under it at one speed, knocked
            // down, let through the chest until it goes back up.
            var impact = YardKit.Shell(transform, "Giant chest (hit)", Vector3.zero, s + Vector3.one * .3f, slam.CrushPush);
            impact.solid = box;
            impact.onFloor = false;
            impact.judgePart = true;
            impact.ghostSeconds = slam.SitSeconds + .5f;
            impact.fromCentre = true;
            impact.speedScale = 0f;
            impact.maxPush = slam.CrushPush;
            impact.lift = 1.5f;
            impact.replaceSpeed = true;
            impact.knockdownSpeed = 3f;
            impact.knockdownSeconds = slam.ShockKnockdown;
            impact.cooldown = .5f;
            impact.course = slam.Course;
            impact.forwardShare = .6f;

            // Pictures beside the chest (they must not move with it): the trolley, four chains, the circle, the wave.
            var c = slam.transform;
            trolley = YardKit.Box(c, "Slam trolley", Vector3.zero, new Vector3(s.x + .6f, .7f, 2.6f), YardKit.Iron, Solid.None).transform;
            YardKit.Box(trolley, "Trolley drum", new Vector3(0f, -.55f, 0f), new Vector3(.9f, .5f, .9f), YardKit.WoodDark, Solid.None);
            chains = new Transform[4];
            for (int i = 0; i < 4; i++)
                chains[i] = YardKit.Box(c, "Slam chain", Vector3.zero, new Vector3(.09f, 1f, .09f), YardKit.Iron, Solid.None).transform;

            areaMaterial = new Material(Arena.Glow(CircleColor)) { name = "M_YardSlamArea" };
            var disc = YardKit.Cylinder(c, "Slam circle", Vector3.zero, Vector3.up, slam.ShockRadius * 2f, .01f, areaMaterial, Solid.None);
            areaRenderer = disc.GetComponent<Renderer>();
            areaRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            areaRenderer.receiveShadows = false;
            areaRenderer.enabled = false;
            area = disc.transform;
            rimMaterial = new Material(Arena.Glow(CircleColor)) { name = "M_YardSlamRim" };
            rim = Circle(c, "Slam circle rim", rimMaterial, .35f);
            waveMaterial = new Material(Arena.Glow(WaveColor)) { name = "M_YardSlamWave" };
            wave = Circle(c, "Slam wave", waveMaterial, .6f);
            dustMaterial = new Material(Arena.Glow(new Color(.86f, .76f, .58f, .7f))) { name = "M_YardSlamDust" };
            dust = new Transform[10];
            for (int i = 0; i < dust.Length; i++)
            {
                var puff = YardKit.Ball(c, "Slam dust", Vector3.zero, 1f, dustMaterial, Solid.None);
                var r = puff.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.enabled = false;
                dust[i] = puff.transform;
            }

            thud = gameObject.AddComponent<AudioSource>();
            thud.clip = ThudSound;
            thud.playOnAwake = false;
            thud.spatialBlend = 1f;
            thud.rolloffMode = AudioRolloffMode.Linear;
            thud.minDistance = 6f;
            thud.maxDistance = 60f;
            thud.dopplerLevel = 0f;
            SectionSoundGate.Gate(thud);
        }

        static LineRenderer Circle(Transform parent, string name, Material material, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = CircleSegments;
            line.widthMultiplier = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.TransformZ;
            // Lying flat: the line's own frame turned so its z is up.
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            line.enabled = false;
            return line;
        }

        static void Ring(LineRenderer line, Vector3 centre, float radius)
        {
            // In the line's frame (turned flat): x across, y along the floor's z.
            line.transform.localPosition = centre;
            for (int i = 0; i < CircleSegments; i++)
            {
                float a = i * Mathf.PI * 2f / CircleSegments;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
        }

        // The pictures are not the chest's children: hide them with it, and take them away with it.
        void OnDisable()
        {
            if (areaRenderer != null) areaRenderer.enabled = false;
            if (rim != null) rim.enabled = false;
            if (wave != null) wave.enabled = false;
            if (dust != null)
                foreach (var d in dust)
                    if (d != null) d.GetComponent<Renderer>().enabled = false;
        }

        void OnDestroy()
        {
            var gone = new List<Transform> { trolley, area };
            if (chains != null) gone.AddRange(chains);
            if (dust != null) gone.AddRange(dust);
            if (rim != null) gone.Add(rim.transform);
            if (wave != null) gone.Add(wave.transform);
            foreach (var t in gone)
                if (t != null) Destroy(t.gameObject);
            foreach (var m in new[] { areaMaterial, rimMaterial, waveMaterial, dustMaterial })
                if (m != null) Destroy(m);
        }

        // ------------------------------------------------------------------ the clock

        void Trip(double time, out double trip, out float into)
        {
            double period = slam != null ? Math.Max(.5, slam.Period) : 8.0;
            trip = Math.Floor(time / period);
            into = (float)(time - trip * period);
        }

        // The chest's centre in the gantry's frame (x along the beam, y up from the floor), and the stage.
        public Stage Local(double time, out float x, out float y)
        {
            Trip(time, out double trip, out float into);
            float half = slam.ChestSize.y * .5f, hang = slam.HangHeight + half, rest = half;
            float here = slam.PlaceAt(slam.PlaceOf(trip));
            float carry = slam.CarrySeconds, hold = slam.HoldSeconds, fall = slam.FallSeconds, sit = slam.SitSeconds;
            if (into < carry)
            {
                float u = into / carry;
                x = Mathf.Lerp(slam.PlaceAt(slam.PlaceOf(trip - 1)), here, u * u * (3f - 2f * u));
                y = hang;
                return Stage.Carried;
            }
            x = here;
            into -= carry;
            if (into < hold)
            {
                y = hang;
                return Stage.Held;
            }
            into -= hold;
            if (into < fall)
            {
                // Falling from rest at a steady pull that lands it at the end of the fall.
                float u = into / fall;
                y = hang - (hang - rest) * u * u;
                return Stage.Falling;
            }
            into -= fall;
            if (into < sit)
            {
                y = rest;
                return Stage.Down;
            }
            into -= sit;
            float v = Mathf.Clamp01(into / slam.HoistSeconds);
            y = Mathf.Lerp(rest, hang, v * v * (3f - 2f * v));
            return Stage.Hoisted;
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = StartRotation;
            if (slam == null)
            {
                position = StartPosition;
                return;
            }
            Local(time, out float x, out float y);
            position = StartPosition + StartRotation * new Vector3(x, y, 0f);
        }

        // ------------------------------------------------------------------ the shockwave

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (slam == null) return;
            double now = ObstacleClock.Now + phase;
            var stage = Local(now, out _, out _);
            bool moving = stage != Stage.Down;
            if (see != null && see.enabled != moving) see.enabled = moving;
            if (!ObstacleClock.Authority) return;
            Trip(now, out double trip, out float into);
            float since = into - slam.LandsAt;
            // A frame or two of slack after the wave's end, so a long step does not skip its rim.
            if (since < 0f || since > slam.WaveSeconds + .1f) return;
            if (trip != judgedTrip)
            {
                judgedTrip = trip;
                judged.Clear();
            }
            Shock(Mathf.Clamp01(since / slam.WaveSeconds));
        }

        // Everyone the wave has reached by now (`front` 0 at the chest's edge .. 1 at the circle) and not judged this slam.
        void Shock(float front)
        {
            var size = slam.ChestSize;
            float edge = Mathf.Max(size.x, size.z) * .5f, radius = slam.ShockRadius;
            float reach = Mathf.Lerp(edge, radius, front);
            var centre = transform.position;
            float floor = slam.transform.position.y;
            foreach (var driver in YardKit.CharactersNear(centre, radius + 1.5f))
            {
                if (driver == null || judged.Contains(driver) || driver.FollowTarget == null) continue;
                var hips = driver.FollowTarget.position;
                var off = new Vector3(hips.x - centre.x, 0f, hips.z - centre.z);
                float d = off.magnitude;
                if (d > reach || d > radius) continue;
                judged.Add(driver);
                // Under the chest's own footprint the falling underside has had them (YardImpact).
                var local = Quaternion.Inverse(transform.rotation) * off;
                if (Mathf.Abs(local.x) < size.x * .5f + .35f && Mathf.Abs(local.z) < size.z * .5f + .35f) continue;
                if (!(driver is Component owner) || !YardKit.Movable(owner.GetComponentInChildren<Collider>(), out _, out var hits)) continue;
                if (Lowest(owner) > floor + slam.GroundTolerance)
                {
                    Spared++;
                    continue;
                }
                var way = d > .05f ? off / d : transform.forward;
                var push = way * slam.ShockPush;
                var course = slam.Course;
                if (course != null)
                {
                    var on = new Vector3(course.forward.x, 0f, course.forward.z).normalized;
                    float ahead = Vector3.Dot(push, on);
                    if (ahead > 0f) push -= on * (ahead * .4f);
                }
                // The push sets the speed across the floor, whatever the character was doing (YardImpact.replaceSpeed).
                var body = driver.FollowTarget.GetComponent<Rigidbody>();
                var piece = owner.GetComponentInParent<IPromotable>();
                float weight = ChessPieces.Stats(piece != null ? piece.Piece : PieceKind.Pawn).Weight;
                if (body != null) push -= new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z) * weight;
                hits.ApplyHit(push + Vector3.up * slam.ShockLift, slam.ShockKnockdown, 0f, true);
                Knocked++;
            }
        }

        // The lowest point of a character's solid parts (its feet standing, its back lying).
        float Lowest(Component owner)
        {
            owner.GetComponentsInChildren(parts);
            float low = float.MaxValue;
            foreach (var part in parts)
                if (part != null && !part.isTrigger && part.enabled) low = Mathf.Min(low, part.bounds.min.y);
            parts.Clear();
            return low == float.MaxValue ? owner.transform.position.y : low;
        }

        // ------------------------------------------------------------------ pictures and sound

        void LateUpdate()
        {
            if (slam == null || trolley == null) return;
            double now = ObstacleClock.Now + phase;
            var stage = Local(now, out float x, out float y);
            Trip(now, out double trip, out float into);
            var size = slam.ChestSize;
            // While it hangs, from where the chest is drawn (its body is interpolated a physics step behind the clock).
            if (stage != Stage.Falling)
            {
                var drawn = slam.transform.InverseTransformPoint(transform.position);
                x = drawn.x;
                y = drawn.y;
            }
            float beam = slam.BeamHeight;
            trolley.localPosition = new Vector3(x, beam - .35f, 0f);
            // Chains from the trolley's drum to the lid's corners while it hangs or is hooked; let go, they hang short.
            float top = y + size.y * .5f, from = beam - .8f;
            bool hooked = stage != Stage.Falling;
            for (int i = 0; i < chains.Length; i++)
            {
                float cx = (i % 2 == 0 ? -1f : 1f) * size.x * .35f, cz = (i < 2 ? -1f : 1f) * size.z * .35f;
                var a = new Vector3(x + cx * .3f, from, cz * .3f);
                var b = hooked ? new Vector3(x + cx, top, cz) : a + Vector3.down * 1.2f;
                var mid = (a + b) * .5f;
                var along = b - a;
                chains[i].localPosition = mid;
                chains[i].localRotation = Quaternion.FromToRotation(Vector3.up, along.sqrMagnitude > 1e-4f ? along.normalized : Vector3.up);
                chains[i].localScale = new Vector3(.09f, Mathf.Max(.05f, along.magnitude), .09f);
            }

            // The circle: dim while the chest is carried towards it, bright and pulsing once it is held over it.
            float target = slam.PlaceAt(slam.PlaceOf(trip)), radius = slam.ShockRadius;
            bool warn = stage == Stage.Carried || stage == Stage.Held || stage == Stage.Falling;
            if (areaRenderer.enabled != warn) areaRenderer.enabled = warn;
            if (rim.enabled != warn) rim.enabled = warn;
            if (warn)
            {
                area.localPosition = new Vector3(target, .03f, 0f);
                Ring(rim, new Vector3(target, .05f, 0f), radius - .18f);
                float pulse = .5f + .5f * (float)Math.Sin(ObstacleClock.Now * 12.0 % (2.0 * Math.PI));
                var fill = CircleColor;
                fill.a = stage == Stage.Carried ? .1f : .22f + .14f * pulse;
                areaMaterial.color = fill;
                var edge = CircleColor;
                edge.a = stage == Stage.Carried ? .35f : .7f + .3f * pulse;
                rimMaterial.color = edge;
            }

            // The wave and the dust from the landing.
            float since = into - slam.LandsAt;
            bool waving = since >= 0f && since < slam.WaveSeconds + .35f;
            if (wave.enabled != waving) wave.enabled = waving;
            if (waving)
            {
                float u = Mathf.Clamp01(since / slam.WaveSeconds);
                Ring(wave, new Vector3(target, .08f, 0f), Mathf.Lerp(Mathf.Max(size.x, size.z) * .5f, radius, u));
                var c = WaveColor;
                c.a = since < slam.WaveSeconds ? .9f : .9f * (1f - (since - slam.WaveSeconds) / .35f);
                waveMaterial.color = c;
            }
            float puffs = since >= 0f && since < 1.4f ? since / 1.4f : -1f;
            for (int i = 0; i < dust.Length; i++)
            {
                var r = dust[i].GetComponent<Renderer>();
                if (r.enabled != puffs >= 0f) r.enabled = puffs >= 0f;
                if (puffs < 0f) continue;
                float a = i * Mathf.PI * 2f / dust.Length;
                float out1 = Mathf.Max(size.x, size.z) * .5f + puffs * radius * .55f;
                dust[i].localPosition = new Vector3(target + Mathf.Cos(a) * out1, .4f + puffs * .9f, Mathf.Sin(a) * out1);
                dust[i].localScale = Vector3.one * (.8f + puffs * 2.2f);
            }
            if (puffs >= 0f)
            {
                var d = dustMaterial.color;
                d.a = .65f * (1f - puffs);
                dustMaterial.color = d;
            }
            if (since >= 0f && since < .2f && trip != thudTrip)
            {
                thudTrip = trip;
                thud.volume = GameSettings.Effects;
                thud.PlayOneShot(thud.clip, 1f);
            }
        }

        static AudioClip thudClip;

        // The chest hitting the cobbles: a deep falling boom with a crack of wood in it, under a second. Made in code.
        static AudioClip ThudSound
        {
            get
            {
                if (thudClip != null) return thudClip;
                const int rate = 22050, length = rate * 9 / 10;
                var random = new System.Random(626);
                var data = new float[length];
                float low = 0f, peak = 1e-4f, phaseA = 0f;
                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / rate;
                    // A boom sliding from 70 Hz to 38 Hz, the noise of the floor shaking, a short crack of wood.
                    float freq = Mathf.Lerp(70f, 38f, Mathf.Clamp01(t / .5f));
                    phaseA += 2f * Mathf.PI * freq / rate;
                    float boom = Mathf.Sin(phaseA) * Mathf.Exp(-t / .28f);
                    float white = (float)(random.NextDouble() * 2.0 - 1.0);
                    low += .06f * (white - low);
                    float rumble = low * 3f * Mathf.Exp(-t / .35f);
                    float crack = white * Mathf.Exp(-t / .018f) * .6f;
                    data[i] = boom + rumble + crack;
                    peak = Mathf.Max(peak, Mathf.Abs(data[i]));
                }
                for (int i = 0; i < length; i++) data[i] *= .85f / peak;
                thudClip = AudioClip.Create("YardSlamThud", length, 1, rate, false);
                thudClip.SetData(data, 0);
                return thudClip;
            }
        }
    }
}
