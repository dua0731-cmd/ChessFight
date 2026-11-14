using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // The sorting yard's roller conveyor (R661, zone 2 "왕실 하역 창고"; the user: "크레인으로 짐을 옮기고 정리" and "플레이어들을
    // 확실히 방해할 수 있을만한 동선과 장애물 형태"): a conveyor across the whole yard carrying single crates and two-high stacks
    // out of one sorting stall's hatch and into the other's. Every pawn has to cross it: the belt stands 0.45 m up - over
    // the 0.25 m a pawn walks up by itself, under the 0.2 m the ledge assist lifts a jumping pawn's feet over a lip
    // (PawnRushPhysicsParams.stepUpHeight / ledgeAssistHeight), so it takes a jump at the belt, and a jump there always gets
    // on (R661: at 0.55 m the bots' jumps fell back off the side, a pawn's jump carries it on 0.2 - 0.4 m) - and carries
    // whoever stands on it along at its speed; a stack is too high to jump, a single crate only with the F step; so a pawn
    // waits for a gap in the loads and goes through it. A load running into a pawn on the belt shoves it off the belt and
    // knocks it down (ConveyorCrate), never along into the hatch.
    //
    // This object is the conveyor's middle on the floor, its local x along the belt (the way the loads go), local z
    // across. Built from code on an inactive object: Configure, then switch it on.
    public sealed class CargoConveyor : MonoBehaviour
    {
        [Tooltip("Length of the belt (m), centred on this object along its local x")]
        [SerializeField] float length = 47f;
        [Tooltip("Width of the belt (m)")]
        [SerializeField] float width = 1.6f;
        [Tooltip("Height of the belt's top (m)")]
        [SerializeField] float height = .45f;
        [Tooltip("Speed of the belt and its loads (m/s)")]
        [SerializeField] float speed = 3.2f;
        [Tooltip("Loads on the loop")]
        [SerializeField] int loads = 9;
        [Tooltip("Metres of the loop out of sight (in the hatches, under the floor)")]
        [SerializeField] float hidden = 9f;
        [Tooltip("Shortest gap between two loads (m)")]
        [SerializeField] float minGap = 2.6f;
        [SerializeField] int seed = 661;
        [Tooltip("Push of a load running into a pawn on the belt (m/s), off the belt")]
        [SerializeField] float knockbackForce = 4.5f;

        Transform course;
        ConveyorCrate[] crates = new ConveyorCrate[0];
        bool built;

        public const float LoadSize = 1.3f, LoadTier = 1.2f;
        public float Length => length;
        public float Width => width;
        public float Height => height;
        public float Speed => speed;
        // Round the loop: the belt, a load's length to leave it, and the stretch out of sight.
        public float Loop => length + LoadSize + hidden;
        public float KnockbackForce => knockbackForce;
        public Transform Course => course;
        public ConveyorCrate[] Crates => crates;

        public void Configure(float length, float width, float height, float speed, int loads, float hidden, float minGap, int seed,
                              float knockbackForce, Transform course)
        {
            this.length = length;
            this.width = width;
            this.height = height;
            this.speed = Mathf.Max(.5f, speed);
            this.loads = Mathf.Max(1, loads);
            this.hidden = Mathf.Max(LoadSize + .5f, hidden);
            this.minGap = minGap;
            this.seed = seed;
            this.knockbackForce = knockbackForce;
            this.course = course;
            Build();
        }

        void Build()
        {
            if (built) return;
            built = true;
            float half = length * .5f, w = width * .5f;
            // The belt: one walkable box from the floor to the belt's top (nothing to lie under or be caught in), never
            // climbed, carrying whoever stands on it (ConveyorBeltSurface).
            var belt = YardKit.Box(transform, "Conveyor belt", new Vector3(0f, height * .5f, 0f), new Vector3(length, height, width), YardKit.Iron, Solid.NoClimb);
            belt.AddComponent<ConveyorBeltSurface>().Configure(speed);
            // Pictures on it: the slats of its top, wooden side rails with gold along their tops, legs.
            for (float x = -half + .25f; x < half; x += .45f)
                YardKit.Box(transform, "Slat", new Vector3(x, height - .055f, 0f), new Vector3(.2f, .1f, width - .1f), YardKit.WoodLight, Solid.None);
            foreach (float side in new[] { -1f, 1f })
            {
                YardKit.Box(transform, "Conveyor rail", new Vector3(0f, height - .2f, side * (w + .04f)), new Vector3(length, .4f, .08f), YardKit.WoodDark, Solid.None);
                YardKit.Box(transform, "Conveyor trim", new Vector3(0f, height + .005f, side * (w + .04f)), new Vector3(length, .03f, .1f), YardKit.Gold, Solid.None);
                for (float x = -half + .8f; x < half; x += 2.4f)
                    YardKit.Box(transform, "Conveyor leg", new Vector3(x, (height - .4f) * .5f, side * (w + .05f)), new Vector3(.16f, height - .4f, .1f), YardKit.WoodDark, Solid.None);
            }
            // The loads round the loop: irregular gaps, never under minGap, from the seed.
            crates = new ConveyorCrate[loads];
            float[] at = Spacing();
            for (int i = 0; i < loads; i++)
            {
                var go = new GameObject("Conveyor load " + (i + 1));
                go.transform.SetParent(transform, false);
                var crate = go.AddComponent<ConveyorCrate>();
                crate.Configure(this, at[i], Hash(seed, i, 7) < .45f);
                crates[i] = crate;
            }
        }

        // Where each load starts round the loop (m from the west end): every gap minGap and a share of the rest of the loop
        // drawn from the seed, so the loads come irregularly, never closer than minGap, and the gaps fill the loop exactly
        // (the last load is as far from the first round the loop as its own gap). A loop too short for that spaces them
        // evenly.
        float[] Spacing()
        {
            int n = Mathf.Max(1, loads);
            var at = new float[n];
            float spare = Loop - n * (minGap + LoadSize);
            if (spare <= 0f)
            {
                for (int i = 0; i < n; i++) at[i] = Loop * i / n;
                return at;
            }
            var share = new float[n];
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                share[i] = .15f + Hash(seed, i, 3);
                sum += share[i];
            }
            float s = 0f;
            for (int i = 0; i < n; i++)
            {
                at[i] = s;
                s += LoadSize + minGap + spare * share[i] / sum;
            }
            return at;
        }

        // Is the belt clear at local x from `from` to `to` seconds from now: no load's footprint within `margin` of it (for
        // the test bots, which cross through a gap as a player does).
        public bool ClearAt(float localX, float from, float to, float margin)
        {
            foreach (var crate in crates)
                if (crate != null && crate.Covers(localX, from, to, margin)) return false;
            return true;
        }

        static float Hash(int seed, int index, int salt)
        {
            unchecked
            {
                ulong h = (ulong)(uint)index * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)seed * 0xC2B2AE3D27D4EB4FUL ^ (ulong)(uint)salt * 0x165667B19E3779F9UL;
                h ^= h >> 33;
                h *= 0xFF51AFD7ED558CCDUL;
                h ^= h >> 33;
                return (h >> 40) / (float)(1UL << 24);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, .55f, .2f, .6f);
            Gizmos.DrawWireCube(new Vector3(0f, height * .5f, 0f), new Vector3(length, height, width));
        }
    }
}
