using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Carriage Yard's crossing waterwheel (가로 회전 물레방아): a big wheel standing across the walkway, its
    // axle along the way to go (local +z), turning steadily. Its blades sweep down across the walkway one after
    // another; the open sector between two blades comes round to the walkway and goes again, and a character gets
    // through then - or jumps the low tip of a blade as it passes at the bottom, or dives under one that is rising
    // away. A blade that catches someone knocks it sideways off the walkway (YardImpact), into the water if there is
    // water there. The pose is a pure function of ObstacleClock.
    //
    // The object's position is the wheel's centre (put it wheelRadius + the tip's clearance over the walkway).
    // Self-building; built from code on an inactive object, call Configure before switching it on.
    public sealed class HorizontalWaterwheel : Obstacle
    {
        [Tooltip("Degrees per second")]
        [SerializeField] float rotationSpeed = 25f;
        [Tooltip("Clockwise as seen by someone walking towards it")]
        [SerializeField] bool clockwise = true;
        [Tooltip("Centre to the blade tips (m)")]
        [SerializeField] float wheelRadius = 4.6f;
        [SerializeField] int bladeCount = 4;
        [Tooltip("Width of the walkway through the wheel the gaps are timed for (m); the console reports the time a gap stays open over it")]
        [SerializeField] float passageWidth = 3.2f;
        [Tooltip("Gap between a blade's tip at the bottom and the walkway (m): low enough to jump")]
        [SerializeField] float tipClearance = .35f;
        [Tooltip("Radial length of each blade's paddle (m)")]
        [SerializeField] float paddleLength = 2.4f;
        [Tooltip("Thickness of a paddle across its sweep (m)")]
        [SerializeField] float paddleThickness = .35f;
        [Tooltip("Depth of the wheel along its axle (m)")]
        [SerializeField] float wheelDepth = 1.4f;
        [Tooltip("Push of a hit (m/s); the blade's speed where it touches adds to it")]
        [SerializeField] float knockbackForce = 5f;

        bool built;

        public void Configure(float rotationSpeed, bool clockwise, float wheelRadius, int bladeCount, float passageWidth,
                              float tipClearance, float knockbackForce)
        {
            this.rotationSpeed = rotationSpeed;
            this.clockwise = clockwise;
            this.wheelRadius = wheelRadius;
            this.bladeCount = bladeCount;
            this.passageWidth = passageWidth;
            this.tipClearance = tipClearance;
            this.knockbackForce = knockbackForce;
        }

        protected override void Awake()
        {
            Build();
            base.Awake();
            Debug.Log($"[RoyalCarriageYard] 가로 물레방아 '{name}': 날개 {bladeCount}개, {Mathf.Abs(rotationSpeed):0.#}도/초 → " +
                      $"{PassEverySeconds:0.0}초마다 틈이 오고, 폭 {passageWidth:0.0} m 통로가 약 {OpenSeconds:0.00}초 열린다");
        }

        // A blade comes past every this many seconds.
        public float PassEverySeconds => 360f / Mathf.Max(1, bladeCount) / Mathf.Max(.01f, Mathf.Abs(rotationSpeed));

        // Roughly how long the walkway (passageWidth wide, a body 1.9 m high) is clear of blades each time: the
        // blade covers it while its paddle crosses that box.
        public float OpenSeconds
        {
            get
            {
                float centre = wheelRadius + tipClearance;          // the axle over the walkway
                float inner = Mathf.Max(.1f, centre - 1.9f);           // the paddle's radius at head height
                float blocked = 2f * Mathf.Atan2(passageWidth * .5f + paddleThickness * .5f, inner) * Mathf.Rad2Deg;
                if (inner > wheelRadius) return PassEverySeconds;     // the blades never come down that low
                float open = 360f / Mathf.Max(1, bladeCount) - blocked;
                return Mathf.Max(0f, open / Mathf.Max(.01f, Mathf.Abs(rotationSpeed)));
            }
        }

        void Build()
        {
            if (built) return;
            built = true;
            var axis = Vector3.forward;
            float hub = .8f, rimRadius = wheelRadius - paddleLength;
            YardKit.Cylinder(transform, "Hub", Vector3.zero, axis, hub * 2f, wheelDepth + .3f, YardKit.WoodDark, YardKit.Solid.NoClimb);
            YardKit.Cylinder(transform, "Hub band", new Vector3(0f, 0f, wheelDepth * .5f + .16f), axis, hub * 1.4f, .08f, YardKit.Gold, YardKit.Solid.None);
            YardKit.Cylinder(transform, "Hub band", new Vector3(0f, 0f, -wheelDepth * .5f - .16f), axis, hub * 1.4f, .08f, YardKit.Gold, YardKit.Solid.None);
            // The inner rim joins the blades over head height; it is round, so it carries nobody anywhere.
            YardKit.Ring(transform, "Inner rim", Vector3.zero, axis, rimRadius, .3f, wheelDepth * .6f, 32, YardKit.WoodDark, YardKit.Solid.NoClimb);
            int count = Mathf.Max(1, bladeCount);
            for (int i = 0; i < count; i++)
            {
                var blade = new GameObject("Blade " + (i + 1)).transform;
                blade.SetParent(transform, false);
                blade.localRotation = Quaternion.AngleAxis(i * 360f / count, axis);
                // Along the blade's local -y, from the hub out to the tip.
                float spoke = rimRadius - hub;
                YardKit.Box(blade, "Spoke", new Vector3(0f, -(hub + spoke * .5f), 0f), new Vector3(.24f, spoke, wheelDepth * .5f), YardKit.WoodDark, YardKit.Solid.NoClimb);
                var paddle = YardKit.Box(blade, "Paddle", new Vector3(0f, -(rimRadius + paddleLength * .5f), 0f),
                                         new Vector3(paddleThickness, paddleLength, wheelDepth), YardKit.WoodLight, YardKit.Solid.NoClimb);
                YardKit.Box(blade, "Paddle edge", new Vector3(0f, -(wheelRadius - .06f), 0f), new Vector3(paddleThickness + .04f, .12f, wheelDepth + .04f),
                            YardKit.Gold, YardKit.Solid.None);
                var impact = YardKit.Shell(blade, "Blade (hit)", new Vector3(0f, -(hub + (wheelRadius - hub) * .5f), 0f),
                                           new Vector3(paddleThickness + .5f, wheelRadius - hub + .2f, wheelDepth + .4f), knockbackForce);
                impact.knockdownSpeed = 99f;   // a blade shoves; only the water ends a run
                impact.lift = 1.5f;
                paddle.name = "Paddle";
            }
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            position = StartPosition;
            float signed = clockwise ? -Mathf.Abs(rotationSpeed) : Mathf.Abs(rotationSpeed);
            rotation = StartRotation * Quaternion.AngleAxis(WrapDegrees(signed * time), Vector3.forward);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(.3f, .7f, 1f, .8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            for (int i = 0; i < 48; i++)
            {
                float a0 = i * Mathf.PI * 2f / 48f, a1 = (i + 1) * Mathf.PI * 2f / 48f;
                Gizmos.DrawLine(new Vector3(Mathf.Sin(a0), Mathf.Cos(a0), 0f) * wheelRadius, new Vector3(Mathf.Sin(a1), Mathf.Cos(a1), 0f) * wheelRadius);
            }
            float walk = -(wheelRadius + tipClearance);
            Gizmos.DrawWireCube(new Vector3(0f, walk + .95f, 0f), new Vector3(passageWidth, 1.9f, wheelDepth));
        }
    }
}
