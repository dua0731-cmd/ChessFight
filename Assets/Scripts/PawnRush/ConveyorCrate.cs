using System;
using ChessFight.Gameplay;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // One load on a CargoConveyor (R661): a single crate (1.2 m) or a two-high stack (2.4 m), carried along the belt at the
    // belt's speed from where it starts round the loop, into the east stall's hatch and out of sight, and back out of the
    // west one - a pure function of ObstacleClock (HANDOFF absolute rule 7); going out of sight and coming back are cuts
    // (JumpsBetween), never swept through anyone.
    //
    // Solid and never climbed. Its front face running into a pawn on the belt shoves it off the belt - mostly out to the
    // side, at one speed (YardImpact) - and knocks it down; the pawn is let through the load until clear, so nobody is
    // ever carried along into the hatch or squeezed against the stall.
    public sealed class ConveyorCrate : Obstacle
    {
        [SerializeField] CargoConveyor conveyor;
        [Tooltip("Metres round the loop at clock 0")]
        [SerializeField] float start;
        [Tooltip("A two-high stack, too high to jump")]
        [SerializeField] bool tall;
        bool built;

        public bool Tall => tall;
        float Size => CargoConveyor.LoadSize;
        float Height => tall ? CargoConveyor.LoadTier * 2f : CargoConveyor.LoadTier;

        public void Configure(CargoConveyor conveyor, float start, bool tall)
        {
            this.conveyor = conveyor;
            this.start = start;
            this.tall = tall;
        }

        protected override void Awake()
        {
            if (conveyor == null) conveyor = GetComponentInParent<CargoConveyor>();
            Build();
            base.Awake();
        }

        void Build()
        {
            if (built || conveyor == null) return;
            built = true;
            float s = Size, t = CargoConveyor.LoadTier;
            int tiers = tall ? 2 : 1;
            for (int i = 0; i < tiers; i++)
            {
                float y = -Height * .5f + t * (i + .5f), w = s - i * .12f;
                YardKit.Box(transform, "Crate", new Vector3(0f, y, 0f), new Vector3(w, t - .04f, w), i == 0 ? YardKit.WoodLight : YardKit.WoodDark, Solid.None);
                YardKit.Box(transform, "Crate band", new Vector3(0f, y, 0f), new Vector3(w + .03f, .14f, w + .03f), YardKit.Banner, Solid.None);
                foreach (float a in new[] { -1f, 1f })
                    YardKit.Box(transform, "Crate edge", new Vector3(a * w * .5f, y, 0f), new Vector3(.08f, t - .02f, w + .02f), YardKit.WoodDark, Solid.None);
            }
            var body = new GameObject("Conveyor load (collider)");
            body.transform.SetParent(transform, false);
            var box = body.AddComponent<BoxCollider>();
            box.size = new Vector3(s, Height, s);
            body.AddComponent<NoClimbSurface>();

            // Only its front face (local +x, the way the belt runs) comes at anyone; out to the side, at one speed.
            var impact = YardKit.Shell(transform, "Conveyor load (hit)", new Vector3(.15f, 0f, 0f), new Vector3(s + .3f, Height + .1f, s + .2f), conveyor.KnockbackForce);
            impact.solid = box;
            impact.speedScale = 0f;
            impact.maxPush = conveyor.KnockbackForce;
            impact.alongShare = .15f;
            impact.lift = 1.2f;
            impact.replaceSpeed = true;
            impact.knockdownSpeed = 3f;
            impact.knockdownSeconds = 1f;
            impact.cooldown = .6f;
            impact.ghostSeconds = 1f;
            impact.course = conveyor.Course;
            impact.forwardShare = .6f;
        }

        // ------------------------------------------------------------------ the clock

        // Metres round the loop at `time`: 0 .. length + size on the belt (from inside the west hatch to inside the east
        // one), the rest out of sight.
        float Along(double time)
        {
            double loop = conveyor != null ? Math.Max(1.0, conveyor.Loop) : 1.0;
            double s = (start + (conveyor != null ? conveyor.Speed : 0f) * time) % loop;
            return (float)(s < 0 ? s + loop : s);
        }

        bool Seen(float along) => conveyor != null && along <= conveyor.Length + Size;

        // The load's centre along the belt (local x) for a place round the loop on the belt.
        float X(float along) => -conveyor.Length * .5f - Size * .5f + along;

        protected override bool JumpsBetween(double from, double to)
        {
            float a = Along(from), b = Along(to);
            return b < a || Seen(a) != Seen(b);
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = StartRotation;
            if (conveyor == null) { position = StartPosition; return; }
            float along = Along(time);
            var local = Seen(along)
                ? new Vector3(X(along), conveyor.Height + Height * .5f, 0f)
                : new Vector3(-conveyor.Length * .5f - Size, -4f, 0f);   // out of sight under the floor, waiting at the west end
            position = StartPosition + StartRotation * local;
        }

        // Will this load's footprint come within `margin` of local x between `from` and `to` seconds from now (on the belt)?
        // For the test bots.
        public bool Covers(float localX, float from, float to, float margin)
        {
            if (conveyor == null) return false;
            double now = ObstacleClock.Now + phase;
            float a = Along(now + from), b = Along(now + to);
            if (!Seen(a) && !Seen(b)) return false;
            // From where it is (or comes out of the west hatch) to where it will be (or goes into the east one).
            float x0 = Seen(a) ? X(a) : X(0f), x1 = Seen(b) && (b >= a || !Seen(a)) ? X(b) : X(conveyor.Length + Size);
            return localX >= x0 - Size * .5f - margin && localX <= x1 + Size * .5f + margin;
        }
    }
}
