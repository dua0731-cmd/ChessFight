using System;
using ChessFight.Gameplay;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // One crate on a CraneDrop's beam (R532). Its whole trip is a pure function of ObstacleClock (HANDOFF absolute rule 7):
    // carried along the beam on its rope to the trip's place (MoveSeconds), held still over it (HoldSeconds), dropped
    // (falling at CraneDrop.Gravity), then sitting on the floor - a crate in the way, not climbed - until the trip ends.
    // The next trip starts with it back on its hook over the same place: that jump up is a cut (JumpsBetween), so it is
    // never swept up through whoever stands by it. The place is the trip hashed with the crane's seed and the hook, the
    // same on every PC.
    //
    // Only the fall knocks: a pawn under the crate as it comes down is pushed straight out from under it and knocked down
    // (YardImpact: the underside is the face coming at it; carried high along the beam it is out of everyone's reach, and
    // sitting still it is only a box). An orange disc on the floor shows where it will land from MarkLead before it does,
    // worked out from the clock like the crate; the trolley and the rope over it are pictures moved the same way.
    public sealed class CraneCrate : Obstacle
    {
        // How long before the crate lands the landing mark shows (s).
        public const float MarkLead = 1f;
        static readonly Color MarkColor = new Color(1f, .32f, .1f, 1f);

        [SerializeField] CraneDrop crane;
        [SerializeField] int index;
        [Tooltip("Lowest and highest place along the beam its centre comes down (m, the crane's local x)")]
        [SerializeField] float from, to;

        Transform mark, trolley, rope, hookBlock;
        Renderer markRenderer;
        Material markMaterial;
        ChessFight.Game.CameraSeeThrough see;
        bool built;

        public enum Stage { Carried, Held, Falling, Down }

        public void Configure(CraneDrop crane, int index, float from, float to)
        {
            this.crane = crane;
            this.index = index;
            this.from = from;
            this.to = to;
        }

        protected override void Awake()
        {
            if (crane == null) crane = GetComponentInParent<CraneDrop>();
            Build();
            base.Awake();
        }

        void Build()
        {
            if (built || crane == null) return;
            built = true;
            float s = crane.CrateSize;
            // The crate: planks with dark edges and a royal band, the collider one box over it (never climbed).
            YardKit.Box(transform, "Crate", Vector3.zero, Vector3.one * s, YardKit.WoodLight, Solid.None);
            foreach (float a in new[] { -1f, 1f })
                foreach (float b in new[] { -1f, 1f })
                {
                    YardKit.Box(transform, "Edge", new Vector3(a * s * .5f, 0f, b * s * .5f), new Vector3(.12f, s + .02f, .12f), YardKit.WoodDark, Solid.None);
                    YardKit.Box(transform, "Edge", new Vector3(0f, a * s * .5f, b * s * .5f), new Vector3(s + .02f, .12f, .12f), YardKit.WoodDark, Solid.None);
                    YardKit.Box(transform, "Edge", new Vector3(a * s * .5f, b * s * .5f, 0f), new Vector3(.12f, .12f, s + .02f), YardKit.WoodDark, Solid.None);
                }
            YardKit.Box(transform, "Band", Vector3.zero, new Vector3(s + .03f, s * .22f, s + .03f), YardKit.Banner, Solid.None);
            YardKit.Box(transform, "Band trim", Vector3.up * (s * .11f), new Vector3(s + .04f, .04f, s + .04f), YardKit.Gold, Solid.None);
            var body = new GameObject("Crate (collider)");
            body.transform.SetParent(transform, false);
            var box = body.AddComponent<BoxCollider>();
            box.size = Vector3.one * s;
            body.AddComponent<NoClimbSurface>();
            // The follow camera looks through it while it moves (a crate dropping behind a pawn would pull the view in);
            // down on the floor it is a wall like any other.
            see = body.AddComponent<ChessFight.Game.CameraSeeThrough>();
            see.enabled = false;

            // The hit shell, 0.15 m round the crate: judged by the crate's box, so only its underside comes at a pawn as
            // it falls (a pawn beside it is not hit) - and by the part it touches, not the hips, so a pawn lying with its
            // chest under the crate is hit too. Straight out from under it at one speed whatever the fall's, kept off the
            // course's way on as every yard hit, and knocked down; a knocked-down pawn is let through the box until the
            // crate goes back up (it sits where it fell), so nobody is left squeezed under it or against a wall.
            var impact = YardKit.Shell(transform, "Crate (hit)", Vector3.zero, Vector3.one * (s + .3f), crane.KnockbackForce);
            impact.solid = box;
            impact.onFloor = false;
            impact.judgePart = true;
            impact.ghostSeconds = crane.Period - crane.LandsAt + .5f;
            impact.fromCentre = true;
            impact.speedScale = 0f;
            impact.maxPush = 6f;
            impact.lift = 1.5f;
            impact.replaceSpeed = true;
            impact.knockdownSpeed = 3f;
            impact.knockdownSeconds = 1f;
            impact.cooldown = .5f;
            impact.course = crane.Course;
            impact.forwardShare = .6f;

            // The trolley on the beam, its rope and hook: pictures beside the crate (they must not fall with it).
            var c = crane.transform;
            trolley = YardKit.Box(c, "Trolley (" + name + ")", Vector3.zero, new Vector3(.9f, .4f, .7f), YardKit.Iron, Solid.None).transform;
            rope = YardKit.Box(c, "Rope (" + name + ")", Vector3.zero, new Vector3(.07f, 1f, .07f), YardKit.WoodDark, Solid.None).transform;
            hookBlock = YardKit.Box(c, "Hook (" + name + ")", Vector3.zero, new Vector3(.3f, .22f, .3f), YardKit.Gold, Solid.None).transform;

            // The landing mark: a flat disc on the floor 0.5 m wider than the crate all round - a pawn whose hips are
            // within ~0.3 m past its edge is still under it as it comes down (YardImpact's face test), and the disc
            // shows that whole place.
            markMaterial = new Material(Arena.Glow(MarkColor)) { name = "M_YardCrateMark" };
            var disc = YardKit.Cylinder(c, "Landing mark (" + name + ")", Vector3.zero, Vector3.up, s + 1f, .01f, markMaterial, Solid.None);
            markRenderer = disc.GetComponent<Renderer>();
            markRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            markRenderer.receiveShadows = false;
            markRenderer.enabled = false;
            mark = disc.transform;
        }

        // The mark, the trolley and the rope are not the crate's children: hide them with it, and take them away with it.
        void OnDisable()
        {
            if (markRenderer != null) markRenderer.enabled = false;
        }

        void OnDestroy()
        {
            foreach (var t in new[] { mark, trolley, rope, hookBlock })
                if (t != null) Destroy(t.gameObject);
            if (markMaterial != null) Destroy(markMaterial);
        }

        // ------------------------------------------------------------------ the clock

        // Which trip this is (it changes when the crate goes back up on its hook) and how far into it.
        void Trip(double time, out double trip, out float into)
        {
            double period = crane != null ? crane.Period : 4.0;
            trip = Math.Floor(time / period);
            into = (float)(time - trip * period);
        }

        protected override bool JumpsBetween(double from, double to)
        {
            Trip(from, out double a, out _);
            Trip(to, out double b, out _);
            return a != b;
        }

        // Where trip `trip`'s crate comes down along the beam, the same on every PC.
        float Place(double trip) => from + Hash(crane != null ? crane.Seed : 0, index, trip) * (to - from);

        // The crate's centre in the crane's frame: x along the beam, y up from the floor.
        Stage Local(double time, out float x, out float y)
        {
            Trip(time, out double trip, out float into);
            float s = crane.CrateSize;
            float hang = crane.DropHeight + s * .5f, rest = s * .5f;
            float here = Place(trip);
            const float move = CraneDrop.MoveSeconds, hold = CraneDrop.HoldSeconds;
            float fall = crane.FallSeconds;
            if (into < move)
            {
                float u = into / move;
                x = Mathf.Lerp(Place(trip - 1), here, u * u * (3f - 2f * u));
                y = hang;
                return Stage.Carried;
            }
            x = here;
            if (into < move + hold)
            {
                y = hang;
                return Stage.Held;
            }
            if (into < move + hold + fall)
            {
                float f = into - move - hold;
                y = Mathf.Max(rest, hang - .5f * CraneDrop.Gravity * f * f);
                return Stage.Falling;
            }
            y = rest;
            return Stage.Down;
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = StartRotation;
            if (crane == null)
            {
                position = StartPosition;
                return;
            }
            Local(time, out float x, out float y);
            position = StartPosition + StartRotation * new Vector3(x, y, 0f);
        }

        // The camera looks through the crate while it is carried, held or falling.
        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (see == null || crane == null) return;
            bool moving = Local(ObstacleClock.Now + phase, out _, out _) != Stage.Down;
            if (see.enabled != moving) see.enabled = moving;
        }

        // ------------------------------------------------------------------ pictures

        // The trolley over the crate, the rope down to it while it hangs (wound up once it is let go), and the landing
        // mark from MarkLead before it lands: brighter as it comes.
        void LateUpdate()
        {
            if (crane == null || trolley == null) return;
            double now = ObstacleClock.Now + phase;
            var stage = Local(now, out float x, out float y);
            // While it hangs, from where the crate is drawn (its body is interpolated a physics step behind the clock),
            // so the hook never runs ahead of a fast-carried crate.
            if (stage == Stage.Carried || stage == Stage.Held)
            {
                var drawn = crane.transform.InverseTransformPoint(transform.position);
                x = drawn.x;
                y = drawn.y;
            }
            float s = crane.CrateSize, beam = crane.BeamHeight;
            trolley.localPosition = new Vector3(x, beam - .2f, 0f);
            float ropeTop = beam - .4f;
            float ropeBottom = stage == Stage.Carried || stage == Stage.Held ? y + s * .5f + .11f : ropeTop - .5f;
            float length = Mathf.Max(.05f, ropeTop - ropeBottom);
            rope.localPosition = new Vector3(x, ropeTop - length * .5f, 0f);
            rope.localScale = new Vector3(.07f, length, .07f);
            hookBlock.localPosition = new Vector3(x, ropeBottom, 0f);

            Trip(now, out _, out float into);
            float until = crane.LandsAt - into;
            bool show = until >= 0f && until <= MarkLead;
            if (show != markRenderer.enabled) markRenderer.enabled = show;
            if (!show) return;
            mark.localPosition = new Vector3(x, .03f, 0f);
            var color = MarkColor;
            color.a = Mathf.Lerp(.7f, .2f, until / MarkLead);
            markMaterial.color = color;
        }

        // 0..1 from the crane's seed, the hook and the trip, the same on every PC.
        static float Hash(int seed, int hook, double trip)
        {
            unchecked
            {
                ulong h = (ulong)(long)trip * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)hook * 0xC2B2AE3D27D4EB4FUL ^ (ulong)(uint)seed * 0x165667B19E3779F9UL;
                h ^= h >> 33;
                h *= 0xFF51AFD7ED558CCDUL;
                h ^= h >> 33;
                return (h >> 40) / (float)(1UL << 24);
            }
        }
    }
}
