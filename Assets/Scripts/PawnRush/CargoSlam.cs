using System;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // The Royal Carriage Yard's giant cargo (R659, zone 2 "왕실 하역 창고"; the user's "거대한 짐을 옮기는 구간의 특정
    // 범위내에 있던 플레이어들은 충격파로 넘어지게"): a great gantry over the yard whose trolley carries a royal chest the size
    // of a cart. Each trip it carries the chest to one of its places, holds it high there while a red circle on the floor
    // shows how far the shockwave will run, lets it drop, and the floor shakes: whoever stands or lies inside the circle
    // as the wave runs out past them is knocked down and pushed out from the chest; whoever is in the air then (a jump
    // timed to the slam) or up on something is spared. Under the chest itself it hits as a crane's crate does. The chest
    // then sits as a box in the way and is hoisted up slowly. Every PC computes the same (SlamChest is a pure function of
    // ObstacleClock; the places are the trip hashed with the seed), no packet.
    //
    // This object is the gantry: its position the middle of the floor under the beam, its local x along the beam, the
    // posts `halfSpan` out either side. Built from code on an inactive object: Configure, then switch it on.
    public sealed class CargoSlam : MonoBehaviour
    {
        [Header("Gantry")]
        [Tooltip("The posts stand this far out either side (m)")]
        [SerializeField] float halfSpan = 26.5f;
        [Tooltip("Height the posts stand on (m)")]
        [SerializeField] float postBase;
        [Tooltip("Height of the beam's underside (m)")]
        [SerializeField] float beamHeight = 11f;
        [Header("Chest")]
        [Tooltip("The chest's size: across the beam (x), up (y), along the way (z) (m)")]
        [SerializeField] Vector3 chestSize = new Vector3(3.2f, 2.4f, 2.4f);
        [Tooltip("Height of the hanging chest's underside (m): how far it drops")]
        [SerializeField] float hangHeight = 6f;
        [Tooltip("Where the chest comes down, along the beam (m, local x)")]
        [SerializeField] float[] places = { -12f, 0f, 12f };
        [Tooltip("Places in a hashed order (never the same twice running); off, in turn")]
        [SerializeField] bool shuffled = true;
        [Tooltip("Hashed, the next place is at most this far from the last (m; 0: any place)")]
        [SerializeField] float maxStep;
        [SerializeField] int seed = 626;
        [Header("Timing (s)")]
        [SerializeField] float carrySeconds = 1.8f;
        [SerializeField] float holdSeconds = 1.6f;
        [SerializeField] float fallSeconds = .55f;
        [SerializeField] float sitSeconds = 1.6f;
        [SerializeField] float hoistSeconds = 1.8f;
        [Header("Shockwave")]
        [Tooltip("How far the wave runs from the chest's centre (m): the red circle")]
        [SerializeField] float shockRadius = 6f;
        [Tooltip("Seconds the wave takes to run from the chest's edge to the circle's")]
        [SerializeField] float waveSeconds = .35f;
        [Tooltip("Push straight out from the chest (m/s) and up, and how long it keeps them down")]
        [SerializeField] float shockPush = 4f;
        [SerializeField] float shockLift = 2f;
        [SerializeField] float shockKnockdown = 1.3f;
        [Tooltip("A character whose lowest part is this far over the floor or more is off it (jumping, up on something) and spared")]
        [SerializeField] float groundTolerance = .4f;
        [Header("Under the chest")]
        [SerializeField] float crushPush = 6f;

        Transform course;
        SlamChest chest;
        bool built;

        public float HalfSpan => halfSpan;
        public float BeamHeight => beamHeight;
        public Vector3 ChestSize => chestSize;
        public float HangHeight => hangHeight;
        public int PlaceCount => places != null ? places.Length : 0;
        public float PlaceAt(int i) => places[Mathf.Clamp(i, 0, places.Length - 1)];
        public bool Shuffled => shuffled;
        public int Seed => seed;
        public float CarrySeconds => carrySeconds;
        public float HoldSeconds => holdSeconds;
        public float FallSeconds => fallSeconds;
        public float SitSeconds => sitSeconds;
        public float HoistSeconds => hoistSeconds;
        public float Period => carrySeconds + holdSeconds + fallSeconds + sitSeconds + hoistSeconds;
        public float LandsAt => carrySeconds + holdSeconds + fallSeconds;
        public float ShockRadius => shockRadius;
        public float WaveSeconds => waveSeconds;
        public float ShockPush => shockPush;
        public float ShockLift => shockLift;
        public float ShockKnockdown => shockKnockdown;
        public float GroundTolerance => groundTolerance;
        public float CrushPush => crushPush;
        public Transform Course => course;
        public SlamChest Chest => chest;

        public void Configure(float halfSpan, float postBase, float beamHeight, Vector3 chestSize, float hangHeight, float[] places, bool shuffled,
                              int seed, float carrySeconds, float holdSeconds, float fallSeconds, float sitSeconds, float hoistSeconds,
                              float shockRadius, float waveSeconds, float shockPush, float shockLift, float shockKnockdown, float crushPush,
                              Transform course)
        {
            this.halfSpan = halfSpan;
            this.postBase = postBase;
            this.beamHeight = beamHeight;
            this.chestSize = chestSize;
            this.hangHeight = Mathf.Clamp(hangHeight, 1f, beamHeight - chestSize.y - 1f);
            this.places = places != null && places.Length > 0 ? (float[])places.Clone() : new[] { 0f };
            this.shuffled = shuffled && this.places.Length > 1;
            this.seed = seed;
            this.carrySeconds = Mathf.Max(.2f, carrySeconds);
            // The hold is the circle's last warning before the drop: never under a second.
            this.holdSeconds = Mathf.Max(1f, holdSeconds);
            this.fallSeconds = Mathf.Max(.2f, fallSeconds);
            this.sitSeconds = Mathf.Max(waveSeconds + .2f, sitSeconds);
            this.hoistSeconds = Mathf.Max(.5f, hoistSeconds);
            this.shockRadius = Mathf.Max(chestSize.x * .5f + .5f, shockRadius);
            this.waveSeconds = Mathf.Max(.05f, waveSeconds);
            this.shockPush = shockPush;
            this.shockLift = shockLift;
            this.shockKnockdown = shockKnockdown;
            this.crushPush = crushPush;
            this.course = course;
            Build();
        }

        // Hashed places at most `metres` apart from one trip to the next (before it is switched on).
        public void LimitStep(float metres) => maxStep = Mathf.Max(0f, metres);

        void Build()
        {
            if (built) return;
            built = true;
            float top = beamHeight + .9f;
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * halfSpan;
                // Two posts either side with a cross tie: a frame the size of a gatehouse.
                foreach (float z in new[] { -1.2f, 1.2f })
                {
                    YardKit.Box(transform, "Slam crane post", new Vector3(x, (postBase + top) * .5f, z), new Vector3(.8f, top - postBase, .8f), YardKit.WoodDark, Solid.NoClimb);
                    YardKit.Box(transform, "Post foot", new Vector3(x, postBase + .15f, z), new Vector3(1.2f, .3f, 1.2f), YardKit.Iron, Solid.None);
                    YardKit.Box(transform, "Post band", new Vector3(x, beamHeight - 1f, z), new Vector3(.86f, .18f, .86f), YardKit.Iron, Solid.None);
                }
                YardKit.Box(transform, "Cross tie", new Vector3(x, beamHeight - 2.2f, 0f), new Vector3(.4f, .4f, 2.8f), YardKit.WoodDark, Solid.None);
                YardKit.Box(transform, "Knee brace", new Vector3(x - side * 1.1f, beamHeight - .9f, 0f), new Vector3(.3f, 2.6f, .3f), YardKit.WoodDark,
                            Solid.None, Quaternion.Euler(0f, 0f, side * 45f));
                // A gear wheel on the post: the winch that lets the chest go.
                YardKit.Cylinder(transform, "Winch wheel", new Vector3(x - side * .6f, beamHeight - 3.2f, -1.7f), Vector3.forward, 2.2f, .2f, YardKit.WoodDark, Solid.None);
                YardKit.Cylinder(transform, "Winch hub", new Vector3(x - side * .6f, beamHeight - 3.2f, -1.82f), Vector3.forward, .5f, .1f, YardKit.Gold, Solid.None);
            }
            foreach (float z in new[] { -1.2f, 1.2f })
            {
                YardKit.Box(transform, "Slam crane beam", new Vector3(0f, beamHeight + .4f, z), new Vector3(halfSpan * 2f + 1.2f, .8f, .7f), YardKit.WoodDark, Solid.None);
                YardKit.Box(transform, "Beam band", new Vector3(0f, beamHeight + .4f, z + Mathf.Sign(z) * .36f), new Vector3(halfSpan * 2f + 1.2f, .12f, .02f), YardKit.Gold, Solid.None);
            }
            YardKit.Box(transform, "Rail", new Vector3(0f, beamHeight - .05f, 0f), new Vector3(halfSpan * 2f - 1f, .1f, 1.9f), YardKit.Iron, Solid.None);

            var go = new GameObject("Giant chest");
            go.transform.SetParent(transform, false);
            chest = go.AddComponent<SlamChest>();
            chest.Configure(this);
        }

        // Where trip `trip` brings the chest down: its place's index, the same on every PC.
        //
        // With maxStep (m, 0: any place) the next place is never further than that from the last, so the chest is never
        // flung across the yard in one carry: the trips go in runs of StepRun, each run starting at the place nearest the
        // middle (keep every place within maxStep of it, so that step keeps the rule too; it may be where the last run
        // ended) and each next place hashed among those within maxStep of the last, never the same twice running - a
        // pure function of the trip all the same, at most StepRun steps worked out.
        public const int StepRun = 8;

        public int PlaceOf(double trip)
        {
            int n = PlaceCount;
            if (n <= 1) return 0;
            if (!shuffled) return (int)(((long)trip % n + n) % n);
            if (maxStep <= 0f)
            {
                int here = (int)(Hash(seed, trip) % (uint)n);
                int before = (int)(Hash(seed, trip - 1) % (uint)n);
                return here == before ? (here + 1) % n : here;
            }
            double first = Math.Floor(trip / StepRun) * StepRun;
            int at = Middle();
            for (double t = first + 1; t <= trip; t++)
            {
                int near = 0;
                for (int i = 0; i < n; i++)
                    if (i != at && Mathf.Abs(places[i] - places[at]) <= maxStep + 1e-3f) near++;
                if (near == 0) continue;
                int pick = (int)(Hash(seed, t) % (uint)near);
                for (int i = 0; i < n; i++)
                    if (i != at && Mathf.Abs(places[i] - places[at]) <= maxStep + 1e-3f && pick-- == 0)
                    {
                        at = i;
                        break;
                    }
            }
            return at;
        }

        // The place nearest the middle of the beam (the first if two are as near).
        int Middle()
        {
            int best = 0;
            for (int i = 1; i < places.Length; i++)
                if (Mathf.Abs(places[i]) < Mathf.Abs(places[best]) - 1e-3f) best = i;
            return best;
        }

        static uint Hash(int seed, double trip)
        {
            unchecked
            {
                ulong h = (ulong)(long)trip * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)seed * 0xC2B2AE3D27D4EB4FUL;
                h ^= h >> 33;
                h *= 0xFF51AFD7ED558CCDUL;
                h ^= h >> 33;
                return (uint)(h >> 32);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, .3f, .2f, .6f);
            if (places == null) return;
            foreach (float x in places)
            {
                for (int i = 0; i < 32; i++)
                {
                    float a0 = i * Mathf.PI * 2f / 32f, a1 = (i + 1) * Mathf.PI * 2f / 32f;
                    Gizmos.DrawLine(new Vector3(x + Mathf.Cos(a0) * shockRadius, .05f, Mathf.Sin(a0) * shockRadius),
                                    new Vector3(x + Mathf.Cos(a1) * shockRadius, .05f, Mathf.Sin(a1) * shockRadius));
                }
                Gizmos.DrawWireCube(new Vector3(x, chestSize.y * .5f, 0f), chestSize);
            }
        }
    }
}
