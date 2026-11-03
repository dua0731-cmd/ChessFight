using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Carriage Yard's carousel (회전 마차): carriages on beams round one fixed centre post, turning together
    // as one group - never each on its own - at a steady speed. The beams run overhead (armHeight), so a character
    // gets through between the carriages and under the beams by watching the gaps come round. A carriage that runs
    // into someone knocks it away along its path and out; the push grows with the speed at that radius (YardImpact),
    // so a faster carousel hits harder. Only its front hits (one driving away does not drag anyone along). The pose is
    // a pure function of ObstacleClock.
    //
    // Self-building from its settings (the object's position is the centre on the floor). Built from code on an
    // inactive object, call Configure before switching it on.
    public sealed class RotatingCarriageGroup : Obstacle
    {
        [Tooltip("Degrees per second")]
        [SerializeField] float rotationSpeed = 22f;
        [Tooltip("Clockwise seen from above")]
        [SerializeField] bool clockwise = true;
        [Tooltip("From the centre post to the middle of each carriage (m)")]
        [SerializeField] float rotationRadius = 7.5f;
        [SerializeField] int carriageCount = 4;
        [Tooltip("Height of the beams' underside (m): characters pass under them")]
        [SerializeField] float armHeight = 3.2f;
        [Tooltip("Push of a hit (m/s); the carriage's speed where it touches adds 0.6 per m/s. It sets the character's speed")]
        [SerializeField] float knockbackForce = 4.5f;

        bool built;

        public float RimSpeed => Mathf.Abs(rotationSpeed) * Mathf.Deg2Rad * (rotationRadius + YardKit.CarriageLength * .5f);

        public void Configure(float rotationSpeed, bool clockwise, float rotationRadius, int carriageCount, float knockbackForce, float armHeight = 3.2f)
        {
            this.rotationSpeed = rotationSpeed;
            this.clockwise = clockwise;
            this.rotationRadius = rotationRadius;
            this.carriageCount = carriageCount;
            this.knockbackForce = knockbackForce;
            this.armHeight = armHeight;
        }

        protected override void Awake()
        {
            Build();
            base.Awake();
        }

        void Build()
        {
            if (built) return;
            built = true;
            YardKit.Cylinder(transform, "Centre post", new Vector3(0f, (armHeight + 1.2f) * .5f, 0f), Vector3.up, 2.2f,
                             armHeight + 1.2f, YardKit.WallStone, YardKit.Solid.NoClimb);
            YardKit.Cylinder(transform, "Post band", new Vector3(0f, armHeight + .2f, 0f), Vector3.up, 2.4f, .4f, YardKit.Gold, YardKit.Solid.None);
            YardKit.Cone(transform, "Post cap", new Vector3(0f, armHeight + 1.2f, 0f), 2.6f, 1.6f, YardKit.Roof);
            YardKit.Ball(transform, "Post crown", new Vector3(0f, armHeight + 2.95f, 0f), .55f, YardKit.Gold, YardKit.Solid.None);
            int count = Mathf.Max(1, carriageCount);
            for (int i = 0; i < count; i++)
            {
                float angle = i * 360f / count;
                var arm = new GameObject("Carriage " + (i + 1)).transform;
                arm.SetParent(transform, false);
                arm.localRotation = Quaternion.Euler(0f, angle, 0f);
                // The beam overhead, from the post out over the carriage's roof.
                float length = rotationRadius;
                YardKit.Box(arm, "Beam", new Vector3(0f, armHeight + .2f, length * .5f), new Vector3(.35f, .4f, length), YardKit.WoodDark, YardKit.Solid.NoClimb);
                YardKit.Box(arm, "Beam trim", new Vector3(0f, armHeight + .42f, length * .5f), new Vector3(.38f, .05f, length), YardKit.Gold, YardKit.Solid.None);
                YardKit.Box(arm, "Hanger", new Vector3(0f, (armHeight + YardKit.CarriageHeight) * .5f, length), new Vector3(.18f, armHeight - YardKit.CarriageHeight + .1f, .18f),
                            YardKit.Iron, YardKit.Solid.None);
                // The carriage, nose along its travel.
                var seat = new GameObject("Seat").transform;
                seat.SetParent(arm, false);
                seat.localPosition = new Vector3(0f, 0f, length);
                seat.localRotation = Quaternion.Euler(0f, clockwise ? 90f : -90f, 0f);
                YardKit.Carriage(seat, i % 2 == 0 ? YardKit.CarriageGreen : YardKit.CarriageRed);
                var impact = YardKit.CarriageCollider(seat, knockbackForce);
                impact.knockdownSpeed = 2.5f;
                // R287: the speed set rather than added (a pawn walking into it and one waiting both go the same way),
                // along the carriage's travel and out from it, a small lift, and the part up the course (the +z of
                // whatever holds the carousel - not the carousel, which turns) cut to 60 %: a hit never throws anyone
                // past the carousel.
                impact.replaceSpeed = true;
                impact.alongShare = .6f;
                impact.lift = 1.5f;
                impact.course = transform.parent;
                impact.forwardShare = .6f;
            }
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            position = StartPosition;
            float signed = clockwise ? Mathf.Abs(rotationSpeed) : -Mathf.Abs(rotationSpeed);
            rotation = StartRotation * Quaternion.AngleAxis(WrapDegrees(signed * time), Vector3.up);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, .55f, .2f, .8f);
            var centre = transform.position;
            float outer = rotationRadius + YardKit.CarriageLength * .5f;
            for (int i = 0; i < 48; i++)
            {
                float a0 = i * Mathf.PI * 2f / 48f, a1 = (i + 1) * Mathf.PI * 2f / 48f;
                Gizmos.DrawLine(centre + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * outer, centre + new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * outer);
            }
        }
    }
}
