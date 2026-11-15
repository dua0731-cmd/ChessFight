using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // One pallet of a PalletHoist (R662): an oak plate on four ropes that sits on the floor (walked onto), is hoisted up
    // to the loft deck, sits there (walked off), and is lowered again - SortingYardRules.Top, a pure function of
    // ObstacleClock. Riders are carried as on any obstacle (IMovingSurface). Two sacks lashed at its outer corners are the
    // goods it lifts.
    //
    // Only its underside hits, and only coming down (YardImpact judged by the plate's box: its top never comes at a rider,
    // its sides move up and down past whoever stands beside it): whoever is under it as it comes down is shoved straight
    // out of the well - south, away from the loft (lane), never sideways into the post - knocked down and let through the
    // plate until clear, so nobody is pressed under it. Whoever it still sets down on (deep in the well, or held against
    // the post) is lifted out on top of it once it rests (YardImpact.liftOut): on board, never inside the plate.
    //
    // This object is the plate's top in the middle, where it is when down.
    public sealed class HoistPallet : Obstacle
    {
        [SerializeField] PalletHoist hoist;
        [SerializeField] int index;
        bool built;

        public int Index => index;

        public void Configure(PalletHoist hoist, int index)
        {
            this.hoist = hoist;
            this.index = index;
        }

        protected override void Awake()
        {
            if (hoist == null) hoist = GetComponentInParent<PalletHoist>();
            Build();
            base.Awake();
        }

        void Build()
        {
            if (built || hoist == null) return;
            built = true;
            var size = hoist.PlateSize;   // across (x), thick (y), deep (z)
            // The plate: oak planks with iron corners, walked on (CourseFloor), never climbed from its sides.
            var plate = YardKit.Box(transform, "Pallet plate", new Vector3(0f, -size.y * .5f, 0f), size, YardKit.WoodLight, Solid.NoClimb);
            plate.AddComponent<CourseFloor>();
            for (float z = -size.z * .5f + .3f; z < size.z * .5f; z += .45f)
                YardKit.Box(transform, "Pallet plank", new Vector3(0f, .005f, z), new Vector3(size.x - .06f, .01f, .06f), YardKit.WoodDark, Solid.None);
            foreach (float a in new[] { -1f, 1f })
                foreach (float b in new[] { -1f, 1f })
                    YardKit.Box(transform, "Pallet corner", new Vector3(a * (size.x * .5f - .08f), -size.y * .5f, b * (size.z * .5f - .08f)),
                                new Vector3(.18f, size.y + .02f, .18f), YardKit.Iron, Solid.None);
            // Two sacks lashed at the outer corners (solid, never climbed): the goods going up.
            float outer = hoist.OuterSide(index) * (size.x * .5f - .3f);
            foreach (float z in new[] { -size.z * .5f + .35f, size.z * .5f - .35f })
            {
                var sack = YardKit.Box(transform, "Pallet sack", new Vector3(outer, .3f, z), new Vector3(.45f, .6f, .45f), Sack, Solid.NoClimb);
                YardKit.Box(sack.transform, "Sack cord", Vector3.zero, new Vector3(1.04f, .12f, 1.04f), YardKit.WoodDark, Solid.None);
            }

            // The underside's hit shell, 0.43 m deep under the plate, pushing south (the lane).
            var lane = new GameObject("Pallet lane (south)").transform;
            lane.SetParent(transform, false);
            lane.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            var box = plate.GetComponent<BoxCollider>();
            var impact = YardKit.Shell(transform, "Pallet underside (hit)", new Vector3(0f, -size.y - .215f, 0f),
                                       new Vector3(size.x - .04f, .43f, size.z - .04f), hoist.Push);
            impact.solid = box;
            impact.judgePart = true;
            impact.onFloor = false;
            impact.lane = lane;
            impact.alongShare = 1f;
            impact.speedScale = 0f;
            impact.maxPush = hoist.Push;
            impact.lift = 1f;
            impact.minSpeed = .1f;
            // Any contact coming down knocks down, and so lets the pawn through the plate until it is clear.
            impact.knockdownSpeed = .5f;
            impact.knockdownSeconds = 1f;
            impact.cooldown = .7f;
            impact.ghostSeconds = 3f;
            impact.liftOut = 3.2f;
            impact.replaceSpeed = true;
            impact.course = hoist.Course;
            impact.forwardShare = .6f;
        }

        static Material Sack => Arena.Lit("M_YardSack", Arena.Hex("#CDB48A"), 0f, .95f);

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = StartRotation;
            if (hoist == null) { position = StartPosition; return; }
            var h = hoist.Schedule(index);
            position = StartPosition + StartRotation * new Vector3(0f, (float)(SortingYardRules.Top(h, time) - h.Low), 0f);
        }
    }
}
