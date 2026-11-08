using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Carriage Yard's lifting waterwheel (세로 회전 물레방아): an upright wheel in front of the waterfall, its
    // axle across the way (local x), turning so its near side (local -z) rises. It is not a thing to dodge but a lift:
    // a character holds the right mouse button by a paddle or one of its gold handles as it comes up past the landing,
    // and the ragdoll's own grab - a FixedJoint from the hand to this wheel's body - carries it up with the wheel's
    // real turning, round over the top. Nothing is teleported or snapped: the hand stays where it took hold and the
    // body hangs from it.
    //
    // Over the top, once the grip is releaseAngle past the highest point, the wheel lets the rider go with a push
    // forward and up onto the platform beyond (IHitReceiver.ApplyHit with "let go", the same call a hit uses). A rider
    // who lets go earlier falls; the water under the wheel sends it back to the landing. Every wheel collider is
    // GrabOnlySurface: held and carried, never climbed. The pose is a pure function of ObstacleClock.
    //
    // The object's position is the axle's centre. Self-building; built from code on an inactive object, call
    // Configure before switching it on.
    public sealed class VerticalWaterwheel : Obstacle
    {
        [Tooltip("Degrees per second")]
        [SerializeField] float rotationSpeed = 28f;
        [Tooltip("Off: the near side (local -z) rises and riders go over the top forwards. On: the other way round")]
        [SerializeField] bool reverse;
        [Tooltip("Centre to the paddle tips (m)")]
        [SerializeField] float wheelRadius = 5.2f;
        [SerializeField] int bladeCount = 8;
        [Tooltip("Width of the wheel along its axle (m)")]
        [SerializeField] float wheelWidth = 3f;
        [Tooltip("Thickness of the gold handles (m): thicker ones are caught from a little further")]
        [SerializeField] float handleThickness = .16f;

        [Header("Letting riders off at the top")]
        [Tooltip("Degrees past the top at which a rider is let go")]
        [SerializeField] float releaseAngle = 30f;
        [Tooltip("Push forward (m/s, along the way the top moves) when let go")]
        [SerializeField] float releaseForward = 4f;
        [Tooltip("Push up (m/s) when let go")]
        [SerializeField] float releaseUp = 4.5f;

        Rigidbody body;
        bool built;
        readonly Dictionary<ICharacterDriver, float> letGo = new Dictionary<ICharacterDriver, float>();

        public int Riders { get; private set; }
        public int Released { get; private set; }

        public void Configure(float rotationSpeed, float wheelRadius, int bladeCount, float releaseAngle, float releaseForward, float releaseUp,
                              bool reverse = false)
        {
            this.rotationSpeed = rotationSpeed;
            this.wheelRadius = wheelRadius;
            this.bladeCount = bladeCount;
            this.releaseAngle = releaseAngle;
            this.releaseForward = releaseForward;
            this.releaseUp = releaseUp;
            this.reverse = reverse;
        }

        protected override void Awake()
        {
            Build();
            base.Awake();
            body = GetComponent<Rigidbody>();
        }

        void Build()
        {
            if (built) return;
            built = true;
            var axis = Vector3.right;
            const YardKit.Solid grab = YardKit.Solid.GrabOnly;
            float half = wheelWidth * .5f, rim = wheelRadius - .35f;
            YardKit.Cylinder(transform, "Hub", Vector3.zero, axis, 1.3f, wheelWidth + .2f, YardKit.WoodDark, grab);
            YardKit.Cylinder(transform, "Hub cap", new Vector3(half + .14f, 0f, 0f), axis, .9f, .1f, YardKit.Gold, YardKit.Solid.None);
            YardKit.Cylinder(transform, "Hub cap", new Vector3(-half - .14f, 0f, 0f), axis, .9f, .1f, YardKit.Gold, YardKit.Solid.None);
            foreach (float side in new[] { -1f, 1f })
                YardKit.Ring(transform, "Rim", new Vector3(side * (half - .1f), 0f, 0f), axis, rim, .3f, .2f, 40, YardKit.WoodDark, grab);
            int count = Mathf.Max(1, bladeCount);
            for (int i = 0; i < count; i++)
            {
                var blade = new GameObject("Paddle " + (i + 1)).transform;
                blade.SetParent(transform, false);
                blade.localRotation = Quaternion.AngleAxis(i * 360f / count, axis);
                // Along the blade's local +y from the hub to the tip; it moves towards its local +z.
                foreach (float side in new[] { -1f, 1f })
                    YardKit.Box(blade, "Spoke", new Vector3(side * (half - .1f), (rim + .65f) * .5f, 0f), new Vector3(.2f, rim - .65f, .22f), YardKit.WoodDark, grab);
                YardKit.Box(blade, "Paddle", new Vector3(0f, wheelRadius - .5f, 0f), new Vector3(wheelWidth - .1f, 1f, .16f), YardKit.WoodLight, grab);
                // The handle: a gold bar across the wheel on the paddle's leading face, where a hand closes round it.
                YardKit.Cylinder(blade, "Handle", new Vector3(0f, wheelRadius - .55f, .32f), axis, handleThickness, wheelWidth - .5f, YardKit.Gold, grab);
                foreach (float side in new[] { -1f, 1f })
                    YardKit.Box(blade, "Handle post", new Vector3(side * (half - .3f), wheelRadius - .55f, .2f), new Vector3(.08f, .08f, .26f), YardKit.Gold, YardKit.Solid.None);
            }
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            position = StartPosition;
            // Positive about +x lifts the -z side and carries the top towards +z.
            float signed = reverse ? -Mathf.Abs(rotationSpeed) : Mathf.Abs(rotationSpeed);
            rotation = StartRotation * Quaternion.AngleAxis(WrapDegrees(signed * time), Vector3.right);
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!ObstacleClock.Authority || body == null) return;
            Riders = 0;
            float now = Time.time;
            foreach (var driver in YardKit.CharactersNear(transform.position, wheelRadius + 2.5f))
            {
                var grip = YardKit.GripOn(driver, body);
                if (grip == null) continue;
                Riders++;
                if (letGo.TryGetValue(driver, out float last) && now - last < 1f) continue;
                if (PastTop(grip.transform.position) < releaseAngle) continue;
                var owner = driver as Component;
                var hits = owner != null ? owner.GetComponentInParent<IHitReceiver>() : null;
                if (hits == null) continue;
                var forward = Vector3.ProjectOnPlane(StartRotation * (reverse ? Vector3.back : Vector3.forward), Vector3.up).normalized;
                hits.ApplyHit(forward * releaseForward + Vector3.up * releaseUp, 0f, 0f, true);
                letGo[driver] = now;
                Released++;
            }
        }

        // Degrees a grip is past the top, towards the side the top moves to (negative: still coming up).
        float PastTop(Vector3 grip)
        {
            var local = Quaternion.Inverse(StartRotation) * (grip - StartPosition);
            float ahead = reverse ? -local.z : local.z;
            return Mathf.Atan2(ahead, local.y) * Mathf.Rad2Deg;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(.3f, .7f, 1f, .8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            for (int i = 0; i < 48; i++)
            {
                float a0 = i * Mathf.PI * 2f / 48f, a1 = (i + 1) * Mathf.PI * 2f / 48f;
                Gizmos.DrawLine(new Vector3(0f, Mathf.Cos(a0), Mathf.Sin(a0)) * wheelRadius, new Vector3(0f, Mathf.Cos(a1), Mathf.Sin(a1)) * wheelRadius);
            }
            if (Application.isPlaying) return;
            float r = releaseAngle * Mathf.Deg2Rad * (reverse ? -1f : 1f);
            Gizmos.color = new Color(1f, .84f, .2f, .9f);
            Gizmos.DrawLine(Vector3.zero, new Vector3(0f, Mathf.Cos(r), Mathf.Sin(r)) * (wheelRadius + 1f));
        }
    }
}
