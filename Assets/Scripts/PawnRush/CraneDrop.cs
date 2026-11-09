using System;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // The Royal Carriage Yard's loading cranes (R532, the loading yard in the centre way; the user's "크레인으로 옮기던 짐이
    // 플레이어가 다니는 루트에 랜덤으로 떨어지고"): a wooden gantry across the way whose trolleys carry crates on ropes. Each
    // trolley keeps taking its crate to a new place along the beam, holds it still over that place, lets it drop, and picks
    // it up again a moment after it lands. Where comes from the trip, hashed with the crane's seed, so every PC sees the
    // same crate at the same place without a packet (CraneCrate, a pure function of ObstacleClock). An orange disc on the
    // floor shows where a crate will land a second before it does, and the crate hangs still over that place for longer.
    //
    // This object is the gantry: its position is the middle of the way on the floor, its local x across the way, its posts
    // on the walls either side (out of everyone's reach). Built from code on an inactive object: Configure builds the
    // gantry and its crates, so call it before switching it on.
    public sealed class CraneDrop : MonoBehaviour
    {
        [Serializable]
        public struct Hook
        {
            [Tooltip("Lowest and highest place along the beam a crate's centre comes down (m, local x)")]
            public float from, to;
            [Tooltip("Seconds added to the shared clock, so the hooks never drop together")]
            public float phase;

            public Hook(float from, float to, float phase)
            {
                this.from = from;
                this.to = to;
                this.phase = phase;
            }
        }

        // Seconds of each trip spent carrying the crate to its place and holding it still there; it then falls, and sits
        // on the floor for the rest of the trip.
        public const float MoveSeconds = .6f, HoldSeconds = .7f;
        // 1.6 g: a crate drops a little harder than plain gravity, as the Pawn Rush pieces fall.
        public const float Gravity = 15.7f;

        [Tooltip("The posts stand this far out either side (m)")]
        [SerializeField] float halfSpan = 13.4f;
        [Tooltip("Height the posts stand on (m): the tops of the walls either side")]
        [SerializeField] float postBase = 2.2f;
        [Tooltip("Height of the beam's underside (m)")]
        [SerializeField] float beamHeight = 7.6f;
        [Tooltip("Seconds of one trip: carry, hold, drop, sit")]
        [SerializeField] float period = 3f;
        [Tooltip("Height of a hanging crate's underside over the floor (m): how far it drops")]
        [SerializeField] float dropHeight = 5f;
        [Tooltip("Edge of a crate (m)")]
        [SerializeField] float crateSize = 1.4f;
        [Tooltip("Push of a hit (m/s), straight out from under the crate")]
        [SerializeField] float knockbackForce = 5f;
        [SerializeField] int seed = 532;

        Transform course;
        CraneCrate[] crates = new CraneCrate[0];
        bool built;

        public float Period => period;
        public float DropHeight => dropHeight;
        public float CrateSize => crateSize;
        public float BeamHeight => beamHeight;
        public float KnockbackForce => knockbackForce;
        public int Seed => seed;
        public Transform Course => course;
        public float FallSeconds => Mathf.Sqrt(2f * dropHeight / Gravity);
        public float LandsAt => MoveSeconds + HoldSeconds + FallSeconds;
        public CraneCrate[] Crates => crates;

        // `course`: the race's way on (its +z, flat): a hit's push towards it is cut, as for every yard obstacle.
        public void Configure(float halfSpan, float postBase, float beamHeight, float period, float dropHeight, float crateSize,
                              float knockbackForce, int seed, Hook[] hooks, Transform course)
        {
            this.halfSpan = halfSpan;
            this.postBase = postBase;
            this.beamHeight = beamHeight;
            this.dropHeight = Mathf.Clamp(dropHeight, 1f, beamHeight - crateSize - .8f);
            this.crateSize = crateSize;
            // At least half a second on the floor before the crate goes back up.
            this.period = Mathf.Max(period, MoveSeconds + HoldSeconds + FallSeconds + .5f);
            this.knockbackForce = knockbackForce;
            this.seed = seed;
            this.course = course;
            Build(hooks ?? new Hook[0]);
        }

        void Build(Hook[] hooks)
        {
            if (built) return;
            built = true;
            float top = beamHeight + .5f;
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * halfSpan;
                YardKit.Box(transform, "Crane post", new Vector3(x, (postBase + top) * .5f, 0f), new Vector3(.6f, top - postBase, .6f), YardKit.WoodDark, Solid.None);
                YardKit.Box(transform, "Post foot", new Vector3(x, postBase + .1f, 0f), new Vector3(.9f, .2f, .9f), YardKit.Iron, Solid.None);
                YardKit.Box(transform, "Post band", new Vector3(x, beamHeight - .6f, 0f), new Vector3(.66f, .14f, .66f), YardKit.Iron, Solid.None);
                // A knee brace from the post up under the beam, and a banner on the post's face towards the start (out of
                // the crates' way: they never come past 0.6 m off the walls the posts stand on).
                YardKit.Box(transform, "Brace", new Vector3(x - side * .75f, beamHeight - .55f, 0f), new Vector3(.22f, 1.9f, .22f), YardKit.WoodDark,
                            Solid.None, Quaternion.Euler(0f, 0f, side * 45f));
                Banner(new Vector3(x, beamHeight - .2f, -.33f));
            }
            YardKit.Box(transform, "Crane beam", new Vector3(0f, beamHeight + .25f, 0f), new Vector3(halfSpan * 2f + .9f, .5f, .6f), YardKit.WoodDark, Solid.None);
            YardKit.Box(transform, "Beam trim", new Vector3(0f, beamHeight + .25f, .31f), new Vector3(halfSpan * 2f + .9f, .08f, .02f), YardKit.Gold, Solid.None);
            YardKit.Box(transform, "Rail", new Vector3(0f, beamHeight - .04f, 0f), new Vector3(halfSpan * 2f - .8f, .08f, .3f), YardKit.Iron, Solid.None);

            crates = new CraneCrate[hooks.Length];
            for (int i = 0; i < hooks.Length; i++)
            {
                var go = new GameObject("Crate " + (i + 1));
                go.transform.SetParent(transform, false);
                var crate = go.AddComponent<CraneCrate>();
                float lo = Mathf.Min(hooks[i].from, hooks[i].to), hi = Mathf.Max(hooks[i].from, hooks[i].to);
                crate.Configure(this, i, lo, hi);
                crate.SetPhase(hooks[i].phase);
                crates[i] = crate;
            }
        }

        // A royal banner hanging on a post, its face to local -z (towards the start).
        void Banner(Vector3 at)
        {
            var b = new GameObject("Banner").transform;
            b.SetParent(transform, false);
            b.localPosition = at;
            YardKit.Box(b, "Cloth", new Vector3(0f, -1.1f, 0f), new Vector3(1.1f, 2.2f, .05f), YardKit.Banner, Solid.None);
            YardKit.Box(b, "Stripe", new Vector3(0f, -1.1f, -.03f), new Vector3(.2f, 2.2f, .02f), YardKit.Gold, Solid.None);
            YardKit.Box(b, "Crown", new Vector3(0f, -.65f, -.04f), new Vector3(.46f, .34f, .02f), YardKit.Gold, Solid.None);
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, .55f, .2f, .5f);
            Gizmos.DrawWireCube(new Vector3(0f, crateSize * .5f, 0f), new Vector3(halfSpan * 2f, crateSize, crateSize));
        }
    }
}
