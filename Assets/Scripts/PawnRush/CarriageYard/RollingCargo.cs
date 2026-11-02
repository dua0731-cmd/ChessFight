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
    public sealed class RollingCargo : Obstacle
    {
        public enum Shape { Barrel, Bale }

        [SerializeField] CargoChute chute;
        [SerializeField] int index;
        [SerializeField] float size = 1.2f;
        [SerializeField] Shape shape;
        [SerializeField] float knockbackForce = 6f;

        Vector3 top, down, normal, across;
        Quaternion lane;
        bool built;

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
            float r = size * .5f;
            if (shape == Shape.Barrel)
            {
                // Lying across the lane (its axle on local x), so it rolls straight down.
                float length = Mathf.Min(size * 1.25f, chute.LaneWidth / 3f);
                YardKit.Cylinder(transform, "Barrel", Vector3.zero, Vector3.right, size, length, YardKit.WoodLight, YardKit.Solid.NoClimb);
                foreach (float x in new[] { -.36f, 0f, .36f })
                    YardKit.Cylinder(transform, "Hoop", new Vector3(x * length, 0f, 0f), Vector3.right, size + .04f, .07f, YardKit.Iron, YardKit.Solid.None);
                YardKit.Shell(transform, "Barrel (hit)", Vector3.zero, new Vector3(length + .3f, size + .3f, size + .3f), knockbackForce);
            }
            else
            {
                YardKit.Ball(transform, "Bale", Vector3.zero, size, BaleStraw, YardKit.Solid.NoClimb);
                YardKit.Cylinder(transform, "Band", Vector3.zero, Vector3.right, size + .03f, .12f, YardKit.WoodDark, YardKit.Solid.None);
                YardKit.Cylinder(transform, "Band", Vector3.zero, Vector3.forward, size + .03f, .12f, YardKit.WoodDark, YardKit.Solid.None);
                YardKit.Shell(transform, "Bale (hit)", Vector3.zero, Vector3.one * (size + .3f), knockbackForce);
            }
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
            float room = Mathf.Max(0f, chute.LaneWidth - size * 1.25f - .4f);
            var landing = top + across * ((Hash(chute.Seed, index, trip) - .5f) * room) + normal * r;
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
