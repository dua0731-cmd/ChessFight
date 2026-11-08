using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Carriage Yard's charging carriage (달리는 마차): waits where it stands, charges to its end point,
    // waits there, backs up to where it started, and again. Its pose is a pure function of ObstacleClock (HANDOFF
    // absolute rule 7), so every PC sees each carriage in the same place; carriages with different waits and phases
    // never settle into one rhythm. Running into a character knocks it away along the charge (YardImpact): harder
    // mid-charge than while it creeps back.
    //
    // Self-building: the object needs only this component (a Rigidbody comes with Obstacle). Its own position is
    // the start, `endOffset` (in its own axes; its nose is +z) the end. Built from code on an inactive object, call
    // Configure before switching it on.
    public sealed class ChargingCarriage : Obstacle
    {
        [Header("Path")]
        [Tooltip("Where the charge ends, from where it waits, in the carriage's own axes (its nose is +z)")]
        [SerializeField] Vector3 endOffset = new Vector3(0f, 0f, 18f);
        [Tooltip("Top speed of the charge (m/s)")]
        [SerializeField] float moveSpeed = 7f;
        [Tooltip("Top speed backing up to the start (m/s)")]
        [SerializeField] float returnSpeed = 3f;
        [Tooltip("Seconds it waits at the start before each charge")]
        [SerializeField] float waitAtStart = 1.5f;
        [Tooltip("Seconds it waits at the end before backing up")]
        [SerializeField] float returnDelay = .8f;
        [Tooltip("Seconds before the charge its lamps turn red")]
        [SerializeField] float warning = .8f;

        [Header("Hit")]
        [Tooltip("Push of a hit (m/s); the carriage's speed where it touches adds to it")]
        [SerializeField] float knockbackForce = 7f;
        [Tooltip("Knocked down for this long by a carriage moving 3 m/s or more (s)")]
        [SerializeField] float knockdownSeconds = 1f;
        [Tooltip("Body colour: 0 green, 1 red, 2 blue")]
        [SerializeField] int paint;

        Transform[] wheels;
        Renderer[] lamps;
        bool built;

        // Time fractions of the charge spent speeding up and slowing down (a quick start, a short stop).
        const float SpeedUp = .25f, SlowDown = .12f;

        public float CycleSeconds => waitAtStart + ChargeSeconds + returnDelay + ReturnSeconds;
        float Distance => endOffset.magnitude;
        float ChargeSeconds => Distance * PeakFactor(SpeedUp, SlowDown) / Mathf.Max(.1f, moveSpeed);
        float ReturnSeconds => Distance * PeakFactor(.3f, .3f) / Mathf.Max(.1f, returnSpeed);

        public void Configure(Vector3 endOffset, float moveSpeed, float returnSpeed, float waitAtStart, float returnDelay,
                              float knockbackForce, float knockdownSeconds, int paint = 0)
        {
            this.endOffset = endOffset;
            this.moveSpeed = moveSpeed;
            this.returnSpeed = returnSpeed;
            this.waitAtStart = waitAtStart;
            this.returnDelay = returnDelay;
            this.knockbackForce = knockbackForce;
            this.knockdownSeconds = knockdownSeconds;
            this.paint = paint;
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
            wheels = YardKit.Carriage(transform, paint == 1 ? YardKit.CarriageRed : paint == 2 ? YardKit.CarriageBlue : YardKit.CarriageGreen);
            var impact = YardKit.CarriageCollider(transform, knockbackForce);
            impact.knockdownSeconds = knockdownSeconds;
            var found = new System.Collections.Generic.List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>())
                if (r.name == "Lamp") found.Add(r);
            lamps = found.ToArray();
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = StartRotation;
            position = StartPosition + StartRotation * endOffset * Along(time);
        }

        // 0 at the start, 1 at the end.
        float Along(double time)
        {
            float cycle = CycleSeconds;
            if (cycle <= 0f) return 0f;
            float t = (float)(time % cycle);
            if (t < waitAtStart) return 0f;
            t -= waitAtStart;
            float charge = ChargeSeconds;
            if (t < charge) return Profile(t / charge, SpeedUp, SlowDown);
            t -= charge;
            if (t < returnDelay) return 1f;
            t -= returnDelay;
            return 1f - Profile(t / ReturnSeconds, .3f, .3f);
        }

        // Distance fraction for a speed that rises over the first `up` of the time, holds, and falls over the last
        // `down` (a trapezoid, its area 1).
        static float Profile(float u, float up, float down)
        {
            u = Mathf.Clamp01(u);
            float peak = PeakFactor(up, down);
            if (u < up) return peak * u * u / (2f * up);
            if (u < 1f - down) return peak * (up * .5f + (u - up));
            float left = 1f - u;
            return 1f - peak * left * left / (2f * down);
        }

        // Top speed over average speed for that trapezoid.
        static float PeakFactor(float up, float down) => 1f / (1f - (up + down) * .5f);

        // Pictures only: the wheels roll with the distance travelled, the lamps go red just before a charge.
        void LateUpdate()
        {
            if (wheels == null) return;
            float travelled = Vector3.Dot(transform.position - StartPosition, (StartRotation * endOffset).normalized);
            float angle = travelled / (YardKit.CarriageWheel * .5f) * Mathf.Rad2Deg;
            foreach (var wheel in wheels)
                if (wheel != null) wheel.localRotation = Quaternion.Euler(angle, 0f, 0f);
            if (lamps == null) return;
            float cycle = CycleSeconds;
            float t = cycle > 0f ? (float)((ObstacleClock.Now + phase) % cycle) : 0f;
            bool warn = t > waitAtStart - warning && t < waitAtStart + ChargeSeconds;
            var material = warn ? YardKit.Warning : YardKit.Lamp;
            foreach (var lamp in lamps)
                if (lamp != null && lamp.sharedMaterial != material) lamp.sharedMaterial = material;
        }

        void OnDrawGizmos()
        {
            var from = Application.isPlaying ? StartPosition : transform.position;
            var turn = Application.isPlaying ? StartRotation : transform.rotation;
            var to = from + turn * endOffset;
            Gizmos.color = new Color(1f, .55f, .2f, .8f);
            Gizmos.DrawLine(from + Vector3.up, to + Vector3.up);
            Gizmos.matrix = Matrix4x4.TRS(to, turn, Vector3.one);
            Gizmos.DrawWireCube(new Vector3(0f, YardKit.CarriageHeight * .5f, 0f),
                                new Vector3(YardKit.CarriageWidth, YardKit.CarriageHeight, YardKit.CarriageLength));
        }
    }
}
