using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Carriage Yard's crossing waterwheel (가로 회전 물레방아, R286 user 11-03: the photo's wooden water
    // wheel, "원형 틀과 그 안쪽 6개의 작대기 사이마다 구멍"): a giant wheel standing across the way, its axle along the
    // way (local +z), turning steadily - a round frame of two rims with boards between them, and six spokes from the
    // hub to the frame on each face. The walkways run THROUGH the wheel, either side of its axle, inside the frame:
    // between two spokes there is a hole, and a character gets through when a hole comes round to its walkway - with a
    // jump over the gap the walkway leaves for the wheel, a dive to go low. A spoke that catches someone knocks it
    // aside (YardImpact), off the walkway if it is not careful. The pose is a pure function of ObstacleClock.
    //
    // The object's position is the axle's centre. The walkways are described by passageOffset, passageWidth and
    // deckBelowAxle only for the timing report (the console line on Play and OpenSeconds); the map builds them.
    // Self-building; built from code on an inactive object, call Configure before switching it on.
    public sealed class HorizontalWaterwheel : Obstacle
    {
        // The frame's width and the wheel's depth a map lays its walkways and their gap by (the defaults below).
        public const float DefaultRim = .6f, DefaultDepth = 1f;

        [Tooltip("Degrees per second")]
        [SerializeField] float rotationSpeed = 15f;
        [Tooltip("Clockwise as seen walking towards it")]
        [SerializeField] bool clockwise = true;
        [Tooltip("Centre to the outside of the round frame (m)")]
        [SerializeField] float wheelRadius = 9f;
        [Tooltip("Width of the round frame across its radius (m); the walkways pass inside it")]
        [SerializeField] float rimWidth = DefaultRim;
        [Tooltip("Spokes on each face; the holes between them are the way through")]
        [SerializeField] int spokeCount = 6;
        [Tooltip("Width of a spoke across its sweep (m)")]
        [SerializeField] float spokeWidth = .45f;
        [Tooltip("Depth of the wheel along its axle (m): the two faces and the boards between them")]
        [SerializeField] float wheelDepth = DefaultDepth;
        [SerializeField] float hubRadius = 1.1f;
        [Tooltip("Boards round the frame between the two rims")]
        [SerializeField] int bucketCount = 28;
        [Tooltip("Push of a hit (m/s); the spoke's speed where it touches adds to it")]
        [SerializeField] float knockbackForce = 4f;

        [Header("The walkways through it (timing report only)")]
        [Tooltip("From the axle's line to the middle of each walkway, sideways (m)")]
        [SerializeField] float passageOffset = 4.6f;
        [SerializeField] float passageWidth = 3.2f;
        [Tooltip("How far the walkways' surface is under the axle (m)")]
        [SerializeField] float deckBelowAxle = 5.27f;

        bool built;

        // Height a character takes going through, jumping (m).
        public const float BodyHeight = 1.9f;

        public float WheelDepth => wheelDepth;
        public float PassEverySeconds => 360f / Mathf.Max(1, spokeCount) / Mathf.Max(.01f, Mathf.Abs(rotationSpeed));

        // How long each walkway's way through (passageWidth wide, BodyHeight high) is clear of spokes each time a hole
        // comes round: the angle between two spokes less the angle the box takes up as seen from the axle and a spoke's
        // own width there.
        public float OpenSeconds
        {
            get
            {
                float inner = Mathf.Max(0f, passageOffset - passageWidth * .5f), outer = passageOffset + passageWidth * .5f;
                float low = deckBelowAxle, high = deckBelowAxle - BodyHeight;   // below the axle: positive downwards
                // Angles from straight down, towards the walkway's side.
                float first = Mathf.Atan2(inner, low) * Mathf.Rad2Deg;
                float last = Mathf.Atan2(outer, Mathf.Max(.01f, high)) * Mathf.Rad2Deg;
                if (high <= 0f) last = 90f + Mathf.Atan2(-high, outer) * Mathf.Rad2Deg;
                float nearest = Mathf.Sqrt(inner * inner + Mathf.Max(0f, high) * Mathf.Max(0f, high));
                float spoke = 2f * Mathf.Atan2(spokeWidth * .5f, Mathf.Max(.5f, nearest)) * Mathf.Rad2Deg;
                float open = 360f / Mathf.Max(1, spokeCount) - (last - first) - spoke;
                return Mathf.Max(0f, open / Mathf.Max(.01f, Mathf.Abs(rotationSpeed)));
            }
        }

        public void Configure(float rotationSpeed, bool clockwise, float wheelRadius, int spokeCount, float knockbackForce,
                              float passageOffset, float passageWidth, float deckBelowAxle)
        {
            this.rotationSpeed = rotationSpeed;
            this.clockwise = clockwise;
            this.wheelRadius = wheelRadius;
            this.spokeCount = spokeCount;
            this.knockbackForce = knockbackForce;
            this.passageOffset = passageOffset;
            this.passageWidth = passageWidth;
            this.deckBelowAxle = deckBelowAxle;
        }

        protected override void Awake()
        {
            Build();
            base.Awake();
            Debug.Log($"[RoyalCarriageYard] 가로 물레방아 '{name}': 바퀴살 {spokeCount}개, {Mathf.Abs(rotationSpeed):0.#}도/초 → 길마다 " +
                      $"{PassEverySeconds:0.0}초마다 구멍이 오고, 폭 {passageWidth:0.0} m 길이 약 {OpenSeconds:0.00}초 열린다");
        }

        void Build()
        {
            if (built) return;
            built = true;
            var axis = Vector3.forward;
            float half = wheelDepth * .5f, rimMiddle = wheelRadius - rimWidth * .5f, inside = wheelRadius - rimWidth;
            const YardKit.Solid none = YardKit.Solid.None, solid = YardKit.Solid.NoClimb;

            // The hub: a wooden drum with iron caps and a gold ring, a little deeper than the wheel.
            YardKit.Cylinder(transform, "Hub", Vector3.zero, axis, hubRadius * 2f, wheelDepth + .4f, YardKit.WoodDark, solid);
            foreach (float side in new[] { -1f, 1f })
            {
                YardKit.Cylinder(transform, "Hub cap", new Vector3(0f, 0f, side * (half + .24f)), axis, hubRadius * 1.15f, .1f, YardKit.Iron, none);
                YardKit.Cylinder(transform, "Hub ring", new Vector3(0f, 0f, side * (half + .3f)), axis, hubRadius * .5f, .08f, YardKit.Gold, none);
            }

            // The round frame: a rim on each face and boards between them all the way round (the buckets).
            foreach (float side in new[] { -1f, 1f })
                YardKit.Ring(transform, "Rim", new Vector3(0f, 0f, side * (half - .1f)), axis, rimMiddle, rimWidth, .2f, 56, YardKit.WoodDark, solid);
            int buckets = Mathf.Max(8, bucketCount);
            float chord = 2f * wheelRadius * Mathf.Sin(Mathf.PI / buckets);
            for (int i = 0; i < buckets; i++)
            {
                var r = Quaternion.AngleAxis(i * 360f / buckets, axis);
                // The outer board between the rims, and a bucket wall across the frame.
                YardKit.Box(transform, "Board", r * (Vector3.up * (wheelRadius - .05f)), new Vector3(chord * .92f, .1f, wheelDepth - .4f),
                            i % 2 == 0 ? YardKit.WoodLight : YardKit.WoodDark, solid, r);
                YardKit.Box(transform, "Bucket wall", r * (Vector3.up * rimMiddle), new Vector3(.08f, rimWidth - .05f, wheelDepth - .4f), YardKit.WoodDark, none, r);
            }

            // Six spokes on each face, joined through the wheel by ties so each pair reads as one blade.
            int count = Mathf.Max(1, spokeCount);
            float length = inside - hubRadius + .2f;
            float middle = hubRadius - .1f + length * .5f;
            for (int i = 0; i < count; i++)
            {
                var blade = new GameObject("Spoke " + (i + 1)).transform;
                blade.SetParent(transform, false);
                blade.localRotation = Quaternion.AngleAxis(i * 360f / count, axis);
                // Along the blade's local +y, from the hub out to the frame.
                foreach (float side in new[] { -1f, 1f })
                {
                    YardKit.Box(blade, "Spoke", new Vector3(0f, middle, side * (half - .11f)), new Vector3(spokeWidth, length, .22f), YardKit.WoodLight, solid);
                    YardKit.Box(blade, "Spoke iron", new Vector3(0f, inside - .35f, side * (half + .01f)), new Vector3(spokeWidth + .04f, .14f, .04f), YardKit.Iron, none);
                }
                foreach (float at in new[] { .45f, .8f })
                    YardKit.Box(blade, "Tie", new Vector3(0f, hubRadius + (inside - hubRadius) * at, 0f), new Vector3(spokeWidth * .7f, .22f, wheelDepth - .3f),
                                YardKit.WoodDark, solid);
                // Inside the wheel's depth (+0.1 a side), so it never reaches over a walkway's end.
                var impact = YardKit.Shell(blade, "Spoke (hit)", new Vector3(0f, middle, 0f), new Vector3(spokeWidth + .5f, length + .2f, wheelDepth + .2f), knockbackForce);
                impact.knockdownSpeed = 99f;   // a spoke shoves; only the water ends a run
                impact.lift = 1.5f;
            }
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            position = StartPosition;
            // Seen walking towards it (looking along +z), a positive turn about +z is anticlockwise.
            float signed = clockwise ? -Mathf.Abs(rotationSpeed) : Mathf.Abs(rotationSpeed);
            rotation = StartRotation * Quaternion.AngleAxis(WrapDegrees(signed * time), Vector3.forward);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(.3f, .7f, 1f, .8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            for (int i = 0; i < 64; i++)
            {
                float a0 = i * Mathf.PI * 2f / 64f, a1 = (i + 1) * Mathf.PI * 2f / 64f;
                Gizmos.DrawLine(new Vector3(Mathf.Sin(a0), Mathf.Cos(a0), 0f) * wheelRadius, new Vector3(Mathf.Sin(a1), Mathf.Cos(a1), 0f) * wheelRadius);
                Gizmos.DrawLine(new Vector3(Mathf.Sin(a0), Mathf.Cos(a0), 0f) * (wheelRadius - rimWidth), new Vector3(Mathf.Sin(a1), Mathf.Cos(a1), 0f) * (wheelRadius - rimWidth));
            }
            foreach (float side in new[] { -1f, 1f })
                Gizmos.DrawWireCube(new Vector3(side * passageOffset, -deckBelowAxle + BodyHeight * .5f, 0f), new Vector3(passageWidth, BodyHeight, wheelDepth));
        }
    }
}
