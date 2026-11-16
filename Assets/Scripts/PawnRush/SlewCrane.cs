using System;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // A slewing jib crane in the sorting yard (R662, the user: "지금처럼 좌우로 한칸씩 이동하며 플레이어를 방해하는 장애물과
    // 말고도 다른 매커니즘의 장애물이 한두개 정도는 더"): a treadwheel crane on a stone drum whose jib swings a slung load -
    // a bundle of squared timber, carried low over the cobbles as heavy loads are - round the drum from one rest to the
    // other and back (or on round the same way, turnOn). Nothing is dropped and nothing hammers: the load is lifted off its
    // rest chocks, slewed, set down on the other, and taken back. The load is the danger. It sweeps an arc, slow by the drum
    // and fast at its far end, so where you cross decides how it comes at you: jump it as it passes under you (its ridge is
    // under a jump's reach), follow it out as it leaves, or keep to the ring by the drum that it never sweeps.
    //
    // Only its leading side comes at anyone (YardImpact judged by the load's lower box: its ends and its ridge never do),
    // so a jumper over it and a pawn past its end are not hit; the push is along the sweep (alongShare), set to one speed
    // that grows with the load's speed there, the part up the course cut to 60 %; fast enough it knocks down, and whoever
    // it knocks down it lets through until clear (its roof as well, YardImpact.alsoSolid), so it never bulldozes anyone
    // lying. Its top is a ridge (faces steeper than anyone stands on), so nobody rides it across the field.
    //
    // Warned three ways: the lantern at the jib's end shows green glass while the load rests and red from WarnSeconds
    // before it lifts until it is set down (a flame's flicker, R685); a bell strikes three times in the last second before
    // a lift; and the load's shadow (R685; was a red sector) on the
    // floor shows where the load will be in the next second (deepening over the first second of the coming slew while it
    // still rests). The motion is SortingYardRules.Heading, a pure function of ObstacleClock - the same on every PC without
    // a packet, and checked by the Core tests (the yard's two cranes never meet, keep clear of everything round them).
    //
    // This object is the drum's middle on the floor. Headings are degrees of Unity yaw in this object's frame: 0 points the
    // jib along local +x, -90 along local +z. Built from code on an inactive object: Configure, then switch it on.
    public sealed class SlewCrane : MonoBehaviour
    {
        [Header("Crane")]
        [SerializeField] float drumRadius = 1.2f;
        [Tooltip("Height of the drum (m): 2.4, out of any jump's reach from the load's ridge")]
        [SerializeField] float drumHeight = 2.4f;
        [Tooltip("Height of the jib (m): over every beam and gantry below it")]
        [SerializeField] float jibHeight = 7.6f;
        [Header("Load")]
        [Tooltip("The load reaches from here (m from the drum's middle; outside the drum)")]
        [SerializeField] float innerRadius = 2f;
        [SerializeField] float outerRadius = 10f;
        [Tooltip("The load's thickness across the sweep and its height to the ridge (m)")]
        [SerializeField] Vector2 loadSection = new Vector2(.42f, .42f);
        [Tooltip("Height of the load's underside over the floor (m)")]
        [SerializeField] float loadBottom = .13f;
        [Header("Sweep (Unity yaw degrees in this object's frame: 0 = local +x, -90 = local +z)")]
        [SerializeField] float headingA = -180f;
        [SerializeField] float headingB;
        [Tooltip("Each slew goes on round the same way (A to B, then on by as much again) instead of back")]
        [SerializeField] bool turnOn;
        [SerializeField] float restSeconds = 1.4f;
        [SerializeField] float slewSeconds = 6f;
        [Tooltip("Seconds a slew takes to reach its full turning speed, and to stop")]
        [SerializeField] float rampSeconds = 1f;
        [Tooltip("The lantern turns red, the bell strikes and the load's shadow shows the coming slew from this long before the load lifts (s)")]
        [SerializeField] float warnSeconds = 1f;
        [SerializeField] float phase;
        [Header("Hit")]
        [Tooltip("Push (m/s) of the load barely moving, and per m/s of its speed where it touches, up to maxPush")]
        [SerializeField] float knockbackForce = 2.5f;
        [SerializeField] float speedScale = .5f;
        [SerializeField] float maxPush = 5.5f;
        [Tooltip("Coming at a pawn this fast or faster (m/s), the load knocks it down")]
        [SerializeField] float knockdownSpeed = 2f;
        [Tooltip("Push (m/s) of a knockdown: under the load's speed, so the pawn tips over the load and it passes on (a knocked "
                 + "pawn driven ahead of the sweep was hit again and again, R662 bots)")]
        [SerializeField] float downPush = 1f;
        [Tooltip("Share of the push along the sweep (1); the rest out from the load's line")]
        [Range(0f, 1f)] [SerializeField] float alongShare = 1f;

        // The floor sector shows the load's next AheadSeconds; the bell strikes this long before a lift.
        const float AheadSeconds = 1f;
        static readonly float[] Strikes = { 1f, .65f, .3f };
        const int SectorSegments = 24;

        Transform course;
        SlewJib jib;
        Material lampMaterial, reachMaterial, sectorMaterial;
        LineRenderer reach;
        Mesh sectorMesh;
        MeshRenderer sectorRenderer;
        // The sector's vertices, refilled each frame, and its two windings, made once (no garbage per frame).
        Vector3[] sectorVertices;
        int[] sectorUp, sectorDown;
        int sectorWound = -1;
        AudioSource bell;
        double struckRest = double.NaN;
        int struck;
        bool built;

        // R685 (실사 예고): a coloured-glass lantern with a flame's flicker; the floor sector is the load's shadow sweeping
        // ahead of it, deepening before a lift, and the reach a worn sand line (was neon red, flashing, and an amber glow).
        static readonly Color Rest = YardWarn.Go, Moving = YardWarn.Stop;

        public float InnerRadius => innerRadius;
        public float OuterRadius => outerRadius;
        public Vector2 LoadSection => loadSection;
        public float LoadBottom => loadBottom;
        public float DrumRadius => drumRadius;
        public float DrumHeight => drumHeight;
        public float JibHeight => jibHeight;
        public float RestSeconds => restSeconds;
        public float SlewSeconds => slewSeconds;
        public float Period => 2f * (restSeconds + slewSeconds);
        public float KnockbackForce => knockbackForce;
        public float SpeedScale => speedScale;
        public float MaxPush => maxPush;
        public float KnockdownSpeed => knockdownSpeed;
        public float DownPush => downPush;
        public float AlongShare => alongShare;
        public Transform Course => course;
        public SlewJib Jib => jib;

        SortingYardRules.Slew Schedule => new SortingYardRules.Slew
        {
            HeadingA = headingA, HeadingB = headingB, Rest = restSeconds, Seconds = slewSeconds, Ramp = rampSeconds, Phase = phase, TurnOn = turnOn,
        };

        public void Configure(float drumRadius, float drumHeight, float jibHeight, float innerRadius, float outerRadius, Vector2 loadSection,
                              float loadBottom, float headingA, float headingB, bool turnOn, float restSeconds, float slewSeconds,
                              float rampSeconds, float warnSeconds, float phase, float knockbackForce, float speedScale, float maxPush,
                              float knockdownSpeed, float alongShare, Transform course)
        {
            this.drumRadius = drumRadius;
            this.drumHeight = drumHeight;
            this.jibHeight = jibHeight;
            this.innerRadius = Mathf.Max(drumRadius + .6f, innerRadius);
            this.outerRadius = Mathf.Max(this.innerRadius + .5f, outerRadius);
            this.loadSection = loadSection;
            this.loadBottom = loadBottom;
            this.headingA = headingA;
            this.headingB = headingB;
            this.turnOn = turnOn;
            this.restSeconds = Mathf.Max(warnSeconds + .1f, restSeconds);
            this.slewSeconds = Mathf.Max(.5f, slewSeconds);
            this.rampSeconds = Mathf.Clamp(rampSeconds, 0f, this.slewSeconds * .5f);
            this.warnSeconds = Mathf.Max(.3f, warnSeconds);
            this.phase = phase;
            this.knockbackForce = knockbackForce;
            this.speedScale = speedScale;
            this.maxPush = maxPush;
            this.knockdownSpeed = knockdownSpeed;
            this.alongShare = alongShare;
            this.course = course;
            Build();
        }

        // ------------------------------------------------------------------ the heading, from the clock

        // The jib's heading at `time` (this crane's phase added) and whether the load is moving then (SortingYardRules).
        public float HeadingAt(double time, out bool moving) => (float)SortingYardRules.Heading(Schedule, time, out moving);

        public float HeadingAt(double time) => HeadingAt(time, out _);

        // Seconds until the load next lifts off a rest (0 while it moves).
        public float UntilLift(double time) => (float)SortingYardRules.UntilLift(Schedule, time);

        // The load's direction at a heading, in this object's frame.
        static Vector3 Along(float heading)
        {
            float a = heading * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
        }

        // ------------------------------------------------------------------ for the test bots

        // Will the load's footprint come within `margin` of this world point between `from` and `to` seconds from now?
        public bool Sweeps(Vector3 world, float from, float to, float margin)
        {
            var local = transform.InverseTransformPoint(world);
            float r = new Vector2(local.x, local.z).magnitude;
            if (r < innerRadius - margin || r > outerRadius + margin) return false;
            float at = Mathf.Atan2(-local.z, local.x) * Mathf.Rad2Deg;
            float half = loadSection.x * .5f + margin;
            double now = ObstacleClock.Now;
            for (float t = from; t <= to + 1e-4f; t += .05f)
                if (Mathf.Abs(Mathf.DeltaAngle(HeadingAt(now + t), at)) * Mathf.Deg2Rad * r <= half) return true;
            return false;
        }

        // Is the load resting across this world point now (within `margin`), and how long until it lifts? For the bots,
        // which hop a resting load rather than wait for it.
        public bool RestingAt(Vector3 world, float margin, out float restLeft)
        {
            restLeft = 0f;
            double now = ObstacleClock.Now;
            float heading = HeadingAt(now, out bool moving);
            if (moving) return false;
            var local = transform.InverseTransformPoint(world);
            float r = new Vector2(local.x, local.z).magnitude;
            if (r < innerRadius - margin || r > outerRadius + margin) return false;
            float at = Mathf.Atan2(-local.z, local.x) * Mathf.Rad2Deg;
            if (Mathf.Abs(Mathf.DeltaAngle(heading, at)) * Mathf.Deg2Rad * r > loadSection.x * .5f + margin) return false;
            restLeft = UntilLift(now);
            return true;
        }

        // Is this world point within the load's reach (between its radii, `margin` either way, and inside the arc it sweeps)?
        public bool InSweep(Vector3 world, float margin)
        {
            var local = transform.InverseTransformPoint(world);
            float r = new Vector2(local.x, local.z).magnitude;
            if (r < innerRadius - margin || r > outerRadius + margin) return false;
            if (turnOn) return true;
            float at = Mathf.Atan2(-local.z, local.x) * Mathf.Rad2Deg;
            float lo = Mathf.Min(headingA, headingB), hi = Mathf.Max(headingA, headingB);
            float slack = r > .1f ? (loadSection.x * .5f + margin) / r * Mathf.Rad2Deg : 180f;
            for (int k = -1; k <= 1; k++)
            {
                float a = at + 360f * k;
                if (a >= lo - slack && a <= hi + slack) return true;
            }
            return false;
        }

        // Seconds until the load's leading side reaches this world point (within `within` seconds), and how fast it comes
        // then (m/s); -1 if it does not. For the test bots' jumps.
        public float ArrivesIn(Vector3 world, float within, out float speed)
        {
            speed = 0f;
            var local = transform.InverseTransformPoint(world);
            float r = new Vector2(local.x, local.z).magnitude;
            if (r < innerRadius || r > outerRadius) return -1f;
            float at = Mathf.Atan2(-local.z, local.x) * Mathf.Rad2Deg;
            float reach = loadSection.x * .5f + .15f;
            double now = ObstacleClock.Now;
            float before = HeadingAt(now);
            for (float t = .02f; t <= within; t += .02f)
            {
                float h = HeadingAt(now + t);
                if (Mathf.Abs(Mathf.DeltaAngle(h, at)) * Mathf.Deg2Rad * r <= reach)
                {
                    speed = Mathf.Abs(Mathf.DeltaAngle(before, h)) * Mathf.Deg2Rad * r / .02f;
                    return t;
                }
                before = h;
            }
            return -1f;
        }

        // ------------------------------------------------------------------ building

        void Build()
        {
            if (built) return;
            built = true;
            // The stone drum the crane stands on (never climbed), a gold band, the mast up to the jib.
            YardKit.Cylinder(transform, "Crane drum", new Vector3(0f, drumHeight * .5f, 0f), Vector3.up, drumRadius * 2f, drumHeight, YardKit.WallStone, Solid.NoClimb);
            YardKit.Cylinder(transform, "Drum band", new Vector3(0f, drumHeight - .15f, 0f), Vector3.up, drumRadius * 2f + .06f, .14f, YardKit.Gold, Solid.None);
            YardKit.Box(transform, "Crane mast", new Vector3(0f, (drumHeight + jibHeight) * .5f, 0f), new Vector3(.45f, jibHeight - drumHeight, .45f), YardKit.WoodDark, Solid.None);

            // The turning part (SlewJib), which carries the load.
            var go = new GameObject("Slewing jib");
            go.transform.SetParent(transform, false);
            jib = go.AddComponent<SlewJib>();
            jib.Configure(this);

            // How far the load reaches: a dim arc on the floor over the sweep, and the rest chocks it is set down on.
            reachMaterial = new Material(Arena.Glow(YardWarn.With(YardWarn.Sand, .3f))) { name = "M_YardSlewReach" };
            reach = Arc(transform, "Slew reach", reachMaterial, .12f);
            DrawArc(reach, outerRadius + .05f);
            foreach (float heading in turnOn ? new[] { headingA, headingA + 180f } : new[] { headingA, headingB })
            {
                var d = Along(heading);
                float length = outerRadius - innerRadius + .4f, mid = (innerRadius + outerRadius) * .5f;
                var rot = Quaternion.Euler(0f, heading, 0f);
                YardKit.Box(transform, "Rest board", d * mid + Vector3.up * .01f, new Vector3(length, .02f, loadSection.x + .5f), YardKit.WoodDark, Solid.None, rot);
                for (float r = innerRadius + .6f; r < outerRadius; r += 2f)
                    YardKit.Box(transform, "Rest chock", d * r + Vector3.up * .06f, new Vector3(.25f, .1f, loadSection.x + .3f), YardKit.Iron, Solid.None, rot);
            }

            // The load's shadow over the next second of its way (a mesh rebuilt each frame).
            sectorMaterial = new Material(Arena.Glow(YardWarn.With(YardWarn.Stop, .4f))) { name = "M_YardSlewSector" };
            var sector = new GameObject("Slew sector");
            sector.transform.SetParent(transform, false);
            sector.transform.localPosition = Vector3.up * .035f;
            sectorMesh = new Mesh { name = "SlewSector" };
            sectorMesh.MarkDynamic();
            sector.AddComponent<MeshFilter>().sharedMesh = sectorMesh;
            sectorRenderer = sector.AddComponent<MeshRenderer>();
            sectorRenderer.sharedMaterial = sectorMaterial;
            sectorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sectorRenderer.receiveShadows = false;
            sectorRenderer.enabled = false;

            // The bell on the crane's house.
            bell = gameObject.AddComponent<AudioSource>();
            bell.clip = BellSound;
            bell.playOnAwake = false;
            bell.spatialBlend = 1f;
            bell.rolloffMode = AudioRolloffMode.Linear;
            bell.minDistance = 4f;
            bell.maxDistance = 30f;
            bell.dopplerLevel = 0f;
            SectionSoundGate.Gate(bell);
        }

        static LineRenderer Arc(Transform parent, string name, Material material, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * .03f;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.widthMultiplier = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.TransformZ;
            return line;
        }

        // The arc the load's far end sweeps (in the line's frame, turned flat: x across, y along the floor's -z).
        void DrawArc(LineRenderer line, float radius)
        {
            float from = headingA, to = turnOn ? headingA + 360f : headingB;
            int n = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(to - from) / 5f));
            line.positionCount = n + 1;
            for (int i = 0; i <= n; i++)
            {
                var d = Along(Mathf.Lerp(from, to, i / (float)n));
                line.SetPosition(i, new Vector3(d.x * radius, d.z * radius, 0f));
            }
        }

        // The lamp at the jib's end (built by the jib, handed back here so the colour is set in one place).
        public void SetLamp(Transform lamp, Material material) => lampMaterial = material;

        void LateUpdate()
        {
            if (!built) return;
            double now = ObstacleClock.Now;
            float heading = HeadingAt(now, out bool moving);
            float until = UntilLift(now);
            bool warn = moving || until < warnSeconds;
            if (lampMaterial != null)
            {
                var c = warn ? Moving : Rest;
                float flame = YardWarn.Flicker(now, transform.position.x * .37f + transform.position.z * .11f);
                lampMaterial.color = new Color(c.r * flame, c.g * flame, c.b * flame, .92f);
            }

            // The floor sector: while it moves, where the load will be in the next second; in the last WarnSeconds of a
            // rest, the first second of the coming slew, pulsing.
            bool show = moving || until < warnSeconds;
            if (sectorRenderer.enabled != show) sectorRenderer.enabled = show;
            if (show)
            {
                double from = moving ? now : now + until;
                float a0 = moving ? heading : HeadingAt(from), a1 = HeadingAt(from + AheadSeconds);
                DrawSector(a0, a1);
                var s = sectorMaterial.color;
                s.a = moving ? .4f : .12f + .28f * YardWarn.Deepen(1f - until / warnSeconds);
                sectorMaterial.color = s;
            }

            // Three bell strikes in the last second before a lift, once each per rest.
            if (!moving && bell != null)
            {
                double restStart = Math.Round(now + until, 2);
                if (restStart != struckRest)
                {
                    struckRest = restStart;
                    struck = 0;
                }
                while (struck < Strikes.Length && until <= Strikes[struck])
                {
                    bool late = until < Strikes[struck] - .2f;   // skipped a frame by more than a strike's slack: stay quiet
                    struck++;
                    if (!late) bell.PlayOneShot(bell.clip, 1f);
                }
            }
        }

        // An annulus sector from innerRadius to outerRadius between two headings, in this object's frame. The arrays are
        // made once and refilled: it is redrawn every frame of a slew.
        void DrawSector(float from, float to)
        {
            if (sectorVertices == null)
            {
                sectorVertices = new Vector3[(SectorSegments + 1) * 2];
                sectorUp = new int[SectorSegments * 6];
                sectorDown = new int[SectorSegments * 6];
                for (int i = 0; i < SectorSegments; i++)
                {
                    int a = i * 2, b = a + 1, c = a + 2, d = a + 3, t = i * 6;
                    sectorUp[t] = a; sectorUp[t + 1] = c; sectorUp[t + 2] = b; sectorUp[t + 3] = b; sectorUp[t + 4] = c; sectorUp[t + 5] = d;
                    sectorDown[t] = a; sectorDown[t + 1] = b; sectorDown[t + 2] = c; sectorDown[t + 3] = b; sectorDown[t + 4] = d; sectorDown[t + 5] = c;
                }
            }
            for (int i = 0; i <= SectorSegments; i++)
            {
                var d = Along(Mathf.Lerp(from, to, i / (float)SectorSegments));
                sectorVertices[i * 2] = d * innerRadius;
                sectorVertices[i * 2 + 1] = d * outerRadius;
            }
            sectorMesh.vertices = sectorVertices;
            // Wound to face up whichever way the sector turns.
            int wound = to < from ? 1 : 0;
            if (wound != sectorWound)
            {
                sectorWound = wound;
                sectorMesh.triangles = wound == 1 ? sectorDown : sectorUp;
            }
            sectorMesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            foreach (var m in new[] { lampMaterial, reachMaterial, sectorMaterial })
                if (m != null) Destroy(m);
            if (sectorMesh != null) Destroy(sectorMesh);
        }

        static AudioClip bellClip;

        // A yard bell: a bright strike with a few inharmonic partials ringing out, under a second. Made in code.
        static AudioClip BellSound
        {
            get
            {
                if (bellClip != null) return bellClip;
                const int rate = 22050, length = rate * 4 / 5;
                var data = new float[length];
                float peak = 1e-4f;
                float[] partials = { 1f, 2.1f, 2.76f, 4.07f }, weights = { 1f, .55f, .35f, .2f };
                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / rate, v = 0f;
                    for (int k = 0; k < partials.Length; k++)
                        v += weights[k] * Mathf.Sin(2f * Mathf.PI * 880f * partials[k] * t) * Mathf.Exp(-t / (.32f / (1f + k * .6f)));
                    v *= Mathf.Min(1f, t / .002f);
                    data[i] = v;
                    peak = Mathf.Max(peak, Mathf.Abs(v));
                }
                for (int i = 0; i < length; i++) data[i] *= .7f / peak;
                bellClip = AudioClip.Create("YardCraneBell", length, 1, rate, false);
                bellClip.SetData(data, 0);
                return bellClip;
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, .55f, .2f, .6f);
            float from = headingA, to = turnOn ? headingA + 360f : headingB;
            for (int i = 0; i < 24; i++)
            {
                var a = Along(Mathf.Lerp(from, to, i / 24f)) * outerRadius;
                var b = Along(Mathf.Lerp(from, to, (i + 1) / 24f)) * outerRadius;
                Gizmos.DrawLine(a, b);
            }
        }
    }
}
