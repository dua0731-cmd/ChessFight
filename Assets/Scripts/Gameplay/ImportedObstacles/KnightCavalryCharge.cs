using System.Collections.Generic;
using UnityEngine;
using ChessFight.Gameplay;
using UnityEngine.Events;

namespace ChessFight.ProtectKing
{
    public enum CavalryState { Idle, Warning, Leaping, Landing, ChargeWindup, Charging, Recovering, Cooldown }

    [RequireComponent(typeof(ObstacleContext))]
    public sealed class KnightCavalryCharge : MonoBehaviour
    {
        float RuntimeTime => SampleTime(ObstacleClock.Now);
        float SampleTime(double t) => ObstacleContext.PeriodicTime(t,StartDelay,CycleDuration,Repeat);
        public ObstacleContext context;
        public Rigidbody KnightRoot;
        public Transform Visual, StartPoint, LandingPoint, ChargeEndPoint;
        [Tooltip("Trigger-only body: explicit existing motor impulse owns all push, not solver depenetration.")]
        public BoxCollider BodyCollider;
        public GameObject LandingWarning, ChargeWarning;
        public Transform LandingVFXPoint, ChargeVFXPoint;
        public bool Active = true, Repeat = true;
        [Min(0)] public float StartDelay = 1, IdleDuration = 1;
        [Min(.05f)] public float WarningDuration = 1.3f, LeapDuration = 1;
        [Min(0)] public float LeapHeight = 4;
        [Min(.05f)] public float LandingPauseDuration = .12f, ChargeWindupDuration = .6f, ChargeDuration = 1.1f;
        [Min(.05f)] public float RecoveryDuration = .5f, CooldownDuration = 2;
        [Min(.1f)] public float LandingRadius = 2;
        [Min(0)] public float LandingKnockbackForce = 9, LandingUpwardForce = 3;
        [Min(0)] public float ChargeKnockbackForce = 13, ChargeUpwardForce = 2;
        public LayerMask PlayerLayerMask = ~0;
        public UnityEvent OnWarning = new UnityEvent(), OnLanding = new UnityEvent(), OnCharge = new UnityEvent();
        public CavalryState State { get; private set; }
        public int HitsThisCycle => struck.Count;
        public float CycleDuration => Idle + Warn + Leap + Land + Windup + Charge + Recovery + Cooldown;
        float Idle => Mathf.Max(0, IdleDuration);
        float Warn => Mathf.Max(.05f, WarningDuration);
        float Leap => Mathf.Max(.05f, LeapDuration);
        float Land => Mathf.Max(.05f, LandingPauseDuration);
        float Windup => Mathf.Max(.05f, ChargeWindupDuration);
        float Charge => Mathf.Max(.05f, ChargeDuration);
        float Recovery => Mathf.Max(.05f, RecoveryDuration);
        float Cooldown => Mathf.Max(.05f, CooldownDuration);
        readonly HashSet<ImportedObstacleActor> struck = new HashSet<ImportedObstacleActor>();
        readonly Collider[] overlaps = new Collider[512];
        readonly RaycastHit[] casts = new RaycastHit[512];
        int cycle = -2;
        float previousTime;
        float previousClock, pausedTime, disabledAt;
        bool ready, landed, wasDisabled;
        Vector3 previousLocal;
        public struct Sample { public CavalryState state; public Vector3 position; public int cycle; public float phase; }
        public Sample Evaluate(float elapsed)
        {
            Vector3 start = transform.InverseTransformPoint(StartPoint.position), landing = transform.InverseTransformPoint(LandingPoint.position);
            Vector3 end = transform.InverseTransformPoint(ChargeEndPoint.position);
            float t = elapsed - Mathf.Max(0, StartDelay);
            var s = new Sample { state = CavalryState.Idle, position = start, cycle = -1 };
            if (t < 0 || (!Repeat && t >= CycleDuration)) return s;
            s.cycle = Repeat ? Mathf.FloorToInt(t / CycleDuration) : 0;
            t = Repeat ? Mathf.Repeat(t, CycleDuration) : t; s.phase = t;
            if (t < Idle) return s;
            if ((t -= Idle) < Warn) { s.state = CavalryState.Warning; return s; }
            if ((t -= Warn) < Leap)
            {
                float u = t / Leap; s.state = CavalryState.Leaping;
                s.position = Vector3.Lerp(start, landing, u) + Vector3.up * (4 * Mathf.Max(0, LeapHeight) * u * (1 - u)); return s;
            }
            s.position = landing;
            if ((t -= Leap) < Land) { s.state = CavalryState.Landing; return s; }
            if ((t -= Land) < Windup) { s.state = CavalryState.ChargeWindup; return s; }
            if ((t -= Windup) < Charge)
            { s.state = CavalryState.Charging; s.position = Vector3.Lerp(landing, end, Mathf.SmoothStep(0, 1, t / Charge)); return s; }
            s.position = end;
            if ((t -= Charge) < Recovery) { s.state = CavalryState.Recovering; return s; }
            s.state = CavalryState.Cooldown; s.position = start; return s;
        }
        public bool ValidateConfiguration(out string reason)
        {
            reason = "";
            if (KnightRoot == null || Visual == null || BodyCollider == null || StartPoint == null || LandingPoint == null || ChargeEndPoint == null)
                reason = "Assign KnightRoot, Visual, BodyCollider and all three path points.";
            else if (Vector3.Distance(LandingPoint.position, ChargeEndPoint.position) < .5f) reason = "Charge path must be at least 0.5 m.";
            else if (Vector3.Dot(transform.up, Vector3.up) < .999f || (transform.lossyScale - Vector3.one).sqrMagnitude > .0001f)
                reason = "Use unit root scale and Y-axis rotation only; edit the path points to change the route.";
            else if (Mathf.Abs(transform.InverseTransformPoint(LandingPoint.position).y - transform.InverseTransformPoint(ChargeEndPoint.position).y) > .01f)
                reason = "Landing and charge end must have the same height.";
            return reason.Length == 0;
        }
        void Awake()
        {
            if (context == null) context = GetComponent<ObstacleContext>();
            if (!ValidateConfiguration(out string reason)) { Debug.LogError("KnightCavalryCharge: " + reason, this); enabled = false; return; }
            KnightRoot.isKinematic = true; KnightRoot.useGravity = false; KnightRoot.interpolation = RigidbodyInterpolation.Interpolate;
            BodyCollider.isTrigger = true; BodyCollider.enabled = false;
            previousTime = RuntimeTime; previousClock = previousTime; var s = Evaluate(previousTime); previousLocal = s.position;
            KnightRoot.position = transform.TransformPoint(s.position); ready = true; HideWarnings();
        }
        void OnEnable()
        {
            if (!ready || !wasDisabled) return;
            pausedTime += Mathf.Max(0, RuntimeTime - disabledAt); previousClock = RuntimeTime; wasDisabled = false;
        }
        void FixedUpdate()
        {
            if (!ready) return;
            float clock = RuntimeTime;
            bool parked = false;
            if (clock < previousClock || parked) pausedTime = 0;
            float step = Mathf.Max(0, clock - previousClock); previousClock = clock;
            if (!Active) { pausedTime += step; BodyCollider.enabled = false; HideWarnings(); return; }
            if (!context.Running && !parked) { BodyCollider.enabled = false; HideWarnings(); return; }
            float now = clock - pausedTime;
            var s = Evaluate(parked ? -1 : now);
            bool continuous = s.cycle == cycle && now >= previousTime && now - previousTime < .25f;
            if (s.cycle != cycle || now < previousTime) { struck.Clear(); landed = false; cycle = s.cycle; }
            var before = previousLocal;
            if (State != s.state)
            {
                State = s.state;
                if (State == CavalryState.Warning) OnWarning.Invoke();
                if (State == CavalryState.Charging) OnCharge.Invoke();
            }
            // Landing is a one-shot contact; include a step that crosses its short phase.
            float landAt = Idle + Warn + Leap;
            if (!parked && !landed && s.cycle >= 0 && s.phase >= landAt && s.phase < landAt + Land + Windup + Charge)
            {
                landed = true; OnLanding.Invoke();
                if (context.Authority) HitLanding();
            }
            if (context.Authority && !parked && (s.state == CavalryState.Charging ||
                (continuous && StateAtPrevious() == CavalryState.Charging && s.state == CavalryState.Recovering)))
            {
                var from = continuous ? before : transform.InverseTransformPoint(LandingPoint.position);
                SweepCharge(transform.TransformPoint(from), transform.TransformPoint(s.position));
            }
            BodyCollider.enabled = !parked && (s.state == CavalryState.Landing || s.state == CavalryState.Charging);
            Visual.gameObject.SetActive(s.state != CavalryState.Recovering && s.state != CavalryState.Cooldown);
            var direction = ChargeEndPoint.position - LandingPoint.position;
            KnightRoot.MoveRotation(Quaternion.LookRotation(direction.normalized, Vector3.up));
            if (!continuous && (parked || s.state == CavalryState.Idle || s.state == CavalryState.Cooldown)) KnightRoot.position = transform.TransformPoint(s.position);
            else KnightRoot.MovePosition(transform.TransformPoint(s.position));
            AlignWarnings();
            if (LandingWarning != null) LandingWarning.SetActive(s.state == CavalryState.Warning || s.state == CavalryState.Leaping);
            if (ChargeWarning != null) ChargeWarning.SetActive(s.state == CavalryState.Warning || s.state == CavalryState.Leaping || s.state == CavalryState.Landing || s.state == CavalryState.ChargeWindup || s.state == CavalryState.Charging);
            previousLocal = s.position; previousTime = now;
        }
        CavalryState StateAtPrevious() => Evaluate(previousTime).state;
        void HitLanding()
        {
            // A shallow vertical cylinder, not a spherical shockwave through the lower recovery route.
            int count = Physics.OverlapSphereNonAlloc(LandingPoint.position + Vector3.up * .5f, LandingRadius + .6f, overlaps, PlayerLayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var motor = ImportedObstacleActor.Find(overlaps[i]); if (motor == null) continue;
                var offset = motor.Position - LandingPoint.position;
                if (offset.y < -.2f || offset.y > 1 || Vector3.ProjectOnPlane(offset, Vector3.up).magnitude > LandingRadius) continue;
                ApplyHit(motor, offset, LandingKnockbackForce, LandingUpwardForce);
            }
        }
        void SweepCharge(Vector3 from, Vector3 to)
        {
            Quaternion rotation = Quaternion.LookRotation((ChargeEndPoint.position - LandingPoint.position).normalized, Vector3.up);
            var half = Vector3.Scale(BodyCollider.size * .5f, BodyCollider.transform.lossyScale);
            var offset = rotation * Vector3.Scale(BodyCollider.center, BodyCollider.transform.lossyScale);
            var movement = to - from; var direction = ChargeEndPoint.position - LandingPoint.position;
            int count = Physics.OverlapBoxNonAlloc(from + offset, half, overlaps, rotation, PlayerLayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) ApplyHit(ImportedObstacleActor.Find(overlaps[i]), direction, ChargeKnockbackForce, ChargeUpwardForce);
            if (movement.sqrMagnitude > .000001f)
            {
                count = Physics.BoxCastNonAlloc(from + offset, half, movement.normalized, casts, rotation, movement.magnitude, PlayerLayerMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++) ApplyHit(ImportedObstacleActor.Find(casts[i].collider), direction, ChargeKnockbackForce, ChargeUpwardForce);
            }
            count = Physics.OverlapBoxNonAlloc(to + offset, half, overlaps, rotation, PlayerLayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) ApplyHit(ImportedObstacleActor.Find(overlaps[i]), direction, ChargeKnockbackForce, ChargeUpwardForce);
        }
        void ApplyHit(ImportedObstacleActor motor, Vector3 direction, float force, float lift)
        {
            if (motor == null || motor.gameObject.scene != gameObject.scene || !motor.CanReceiveObstacle || !struck.Add(motor)) return;
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (direction.sqrMagnitude < .001f) direction = Vector3.ProjectOnPlane(ChargeEndPoint.position - LandingPoint.position, Vector3.up);
            motor.AddObstacleImpulse(direction.normalized * Mathf.Max(0, force) + Vector3.up * Mathf.Max(0, lift));
        }
        void AlignWarnings()
        {
            if (LandingVFXPoint != null) LandingVFXPoint.position = LandingPoint.position;
            if (LandingWarning != null)
            { LandingWarning.transform.position = LandingPoint.position + Vector3.up * .025f; LandingWarning.transform.localScale = new Vector3(LandingRadius * 2, .02f, LandingRadius * 2); }
            if (ChargeWarning != null)
            {
                var delta = ChargeEndPoint.position - LandingPoint.position;
                ChargeWarning.transform.position = (LandingPoint.position + ChargeEndPoint.position) * .5f + Vector3.up * .04f;
                ChargeWarning.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                ChargeWarning.transform.localScale = new Vector3(BodyCollider.size.x, .02f, delta.magnitude + BodyCollider.size.z);
            }
        }
        void HideWarnings() { if (LandingWarning != null) LandingWarning.SetActive(false); if (ChargeWarning != null) ChargeWarning.SetActive(false); }
        void OnDisable() { if (ready) { disabledAt = RuntimeTime; wasDisabled = true; } HideWarnings(); if (BodyCollider != null) BodyCollider.enabled = false; }
        void OnDrawGizmosSelected()
        {
            if (StartPoint == null || LandingPoint == null || ChargeEndPoint == null) return;
            Gizmos.color = Color.yellow; Vector3 previous = StartPoint.position;
            for (int i = 1; i <= 24; i++) { float t = i / 24f; var p = Vector3.Lerp(StartPoint.position, LandingPoint.position, t) + transform.up * (4 * LeapHeight * t * (1 - t)); Gizmos.DrawLine(previous, p); previous = p; }
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(LandingPoint.position, LandingRadius); Gizmos.DrawLine(LandingPoint.position, ChargeEndPoint.position);
        }
    }
}
