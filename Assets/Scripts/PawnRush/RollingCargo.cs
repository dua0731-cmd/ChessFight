using System;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // One piece of cargo on a CargoChute's lane: a barrel rolling on its side or a round bale. Its whole trip is a
    // pure function of ObstacleClock - out of the hatch over the top of the lane (falling), down the lane (rolling,
    // its turn worked out from the distance), into the drain at the bottom (sinking), a moment out of sight under the
    // floor - and then the same again from the top, at a new place across the lane. The jump from under the floor back
    // into the hatch is a cut (JumpsBetween), so it is never swept through the lane or anyone on it.
    //
    // A hit always sends the character down the ramp and out of the piece's way at the same speed and keeps it on its
    // feet (R287: the 6v6 review found the hit taken at the hips, where the roll adds to the piece's speed, throwing it
    // up or down the slope by the piece's size). The drop starts inside the gatehouse's lintel, so an orange disc on the lane shows where the
    // piece will land from MarkLead before it falls, worked out from the clock like the piece.
    public sealed class RollingCargo : Obstacle
    {
        // How long before the drop the landing mark shows (s).
        public const float MarkLead = 1f;
        public enum Shape { Barrel, Bale }

        [SerializeField] CargoChute chute;
        [SerializeField] int index;
        [SerializeField] float size = 1.2f;
        [SerializeField] Shape shape;
        [SerializeField] float knockbackForce = 6f;

        Vector3 top, down, normal, across;
        Quaternion lane;
        Transform mark;
        Renderer markRenderer;
        Material markMaterial;
        bool built;

        static readonly Color MarkColor = new Color(1f, .32f, .1f, 1f);

        public float Size => size;

        public void Configure(CargoChute chute, int index, float size, Shape shape, float knockbackForce)
        {
            this.chute = chute;
            this.index = index;
            this.size = size;
            this.shape = shape;
            this.knockbackForce = knockbackForce;
        }

        protected override void Awake()
        {
            if (chute == null) chute = GetComponentInParent<CargoChute>();
            var t = chute.transform;
            top = t.position;
            down = t.forward;
            normal = t.up;
            across = t.right;
            lane = Quaternion.LookRotation(down, normal);
            Build();
            base.Awake();
        }

        void Build()
        {
            if (built) return;
            built = true;
            // The hit shells are round like the pieces, 0.15 m over them, so they reach as far whatever the roll (a box
            // turning with the piece reached up to 0.58 m beyond it at its corners, and hit pawns passing its ends).
            var shell = new GameObject(shape == Shape.Barrel ? "Barrel (hit)" : "Bale (hit)");
            shell.transform.SetParent(transform, false);
            if (shape == Shape.Barrel)
            {
                // Lying across the lane (its axle on local x), so it rolls straight down.
                float length = Mathf.Min(size * 1.25f, chute.LaneWidth / 3f);
                YardKit.Cylinder(transform, "Barrel", Vector3.zero, Vector3.right, size, length, YardKit.WoodLight, YardKit.Solid.NoClimb);
                foreach (float x in new[] { -.36f, 0f, .36f })
                    YardKit.Cylinder(transform, "Hoop", new Vector3(x * length, 0f, 0f), Vector3.right, size + .04f, .07f, YardKit.Iron, YardKit.Solid.None);
                // A capsule along the axle: its round ends reach past the barrel's ends, which are never a front (Drum).
                var capsule = shell.AddComponent<CapsuleCollider>();
                capsule.direction = 0;
                capsule.radius = size * .5f + .15f;
                capsule.height = length + size + .3f;
                capsule.isTrigger = true;
            }
            else
            {
                YardKit.Ball(transform, "Bale", Vector3.zero, size, BaleStraw, YardKit.Solid.NoClimb);
                YardKit.Cylinder(transform, "Band", Vector3.zero, Vector3.right, size + .03f, .12f, YardKit.WoodDark, YardKit.Solid.None);
                YardKit.Cylinder(transform, "Band", Vector3.zero, Vector3.forward, size + .03f, .12f, YardKit.WoodDark, YardKit.Solid.None);
                var ball = shell.AddComponent<SphereCollider>();
                ball.radius = size * .5f + .15f;
                ball.isTrigger = true;
            }
            var hit = shell.AddComponent<YardImpact>();
            hit.knockbackForce = knockbackForce;
            hit.body = shape == Shape.Barrel ? YardImpact.Body.Drum : YardImpact.Body.Ball;
            // Down the ramp and out of the piece's line at one speed (3 + 0.6 x 5 m/s rolling = 6) whatever was hit and
            // however the piece spins, and kept on its feet: a short hop ~1.5 m down and ~1.5 m aside, clear of the piece
            // (R287: straight down at 9 m/s the pawn turned back up into the same piece and was hit again and again;
            // knocked flat on a slope, a body slid on down out of all control). Dead in a piece's line it goes towards
            // the ramp's outer wall.
            hit.fromCentre = true;
            hit.lane = chute.transform;
            hit.alongShare = .5f;
            hit.centreSide = chute.CentreSide;
            hit.replaceSpeed = true;
            hit.maxPush = 6f;
            hit.lift = 1f;
            hit.knockdownSpeed = 99f;

            // The landing mark: a flat disc on the lane, the piece's width, beside the piece (it must not move with it).
            markMaterial = new Material(Arena.Glow(MarkColor)) { name = "M_YardCargoMark" };
            var disc = YardKit.Cylinder(chute.transform, "Landing mark (" + name + ")", Vector3.zero, Vector3.up, size * 1.1f, .01f, markMaterial, YardKit.Solid.None);
            markRenderer = disc.GetComponent<Renderer>();
            markRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            markRenderer.receiveShadows = false;
            markRenderer.enabled = false;
            mark = disc.transform;
        }

        // The mark is not the piece's child: hide it with the piece.
        void OnDisable()
        {
            if (markRenderer != null) markRenderer.enabled = false;
        }

        void OnDestroy()
        {
            if (mark != null) Destroy(mark.gameObject);
            if (markMaterial != null) Destroy(markMaterial);
        }

        // Where the trip's piece comes down, across the lane, the same on every PC.
        Vector3 Landing(double trip)
        {
            float room = Mathf.Max(0f, chute.LaneWidth - size * 1.25f - .4f);
            return top + across * ((Hash(chute.Seed, index, trip) - .5f) * room);
        }

        // The landing mark, from MarkLead before the drop until the piece is down: brighter as it comes.
        void LateUpdate()
        {
            if (mark == null || markRenderer == null) return;
            Trip(ObstacleClock.Now + phase, out double trip, out float into);
            float drop = chute.DropSeconds, left = (float)chute.LoopSeconds - into;
            float until = into < drop ? drop - into : left < MarkLead ? left + drop : -1f;
            if (until < 0f)
            {
                if (markRenderer.enabled) markRenderer.enabled = false;
                return;
            }
            mark.SetPositionAndRotation(Landing(into < drop ? trip : trip + 1) + normal * .03f, Quaternion.FromToRotation(Vector3.up, normal));
            var color = MarkColor;
            color.a = Mathf.Lerp(.65f, .15f, until / (MarkLead + drop));
            markMaterial.color = color;
            if (!markRenderer.enabled) markRenderer.enabled = true;
        }

        static Material BaleStraw => Arena.Lit("M_YardStraw", Arena.Hex("#D9B65E"), 0f, .9f);

        // Which trip this is (it changes when the piece goes back to the top) and how far into it.
        void Trip(double time, out double trip, out float into)
        {
            double loop = chute.LoopSeconds;
            double t = time + index * (double)chute.SpawnInterval;
            trip = Math.Floor(t / loop);
            into = (float)(t - trip * loop);
        }

        protected override bool JumpsBetween(double from, double to)
        {
            Trip(from, out double a, out _);
            Trip(to, out double b, out _);
            return a != b;
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            Trip(time, out double trip, out float into);
            float r = size * .5f;
            var landing = Landing(trip) + normal * r;
            float drop = chute.DropSeconds, roll = chute.RollSeconds, sink = CargoChute.SinkSeconds;
            float length = chute.LaneLength;
            float travelled;
            if (into < drop)
            {
                float f = into / drop;
                position = landing + Vector3.up * (chute.DropHeight * (1f - f * f));
                travelled = 0f;
            }
            else if (into < drop + roll)
            {
                travelled = (into - drop) * chute.RollingSpeed;
                position = landing + down * travelled;
            }
            else
            {
                // Down through the drain grate, out of sight under the floor, until the next trip.
                float f = Mathf.Clamp01((into - drop - roll) / sink);
                travelled = length;
                position = landing + down * length + Vector3.down * ((size + 1.3f) * f);
            }
            rotation = lane * Quaternion.AngleAxis(travelled / Mathf.Max(.05f, r) * Mathf.Rad2Deg, Vector3.right);
        }

        // 0..1 from the chute's seed, the piece and the trip, the same on every PC.
        static float Hash(int seed, int piece, double trip)
        {
            unchecked
            {
                ulong h = (ulong)(long)trip * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)piece * 0xC2B2AE3D27D4EB4FUL ^ (ulong)(uint)seed * 0x165667B19E3779F9UL;
                h ^= h >> 33;
                h *= 0xFF51AFD7ED558CCDUL;
                h ^= h >> 33;
                return (h >> 40) / (float)(1UL << 24);
            }
        }
    }
}
