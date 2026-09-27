using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ChessFight.ProtectKing
{
    public enum AirVentState { Idle, Warning, Blowing }
    public sealed class AirVent : MonoBehaviour
    {
        public ObstacleContext context;
        public BoxCollider WindTrigger;
        public GameObject WarningVisual, WindVisual;
        public Transform VFXPoint;
        [Min(0)] public float IdleDuration = 3;
        [Min(0)] public float WarningDuration = .8f;
        [Min(.1f)] public float BlowDuration = 3;
        [Min(0)] public float WindStrength = 20;
        public Vector3 WindDirection = Vector3.forward;
        [Min(0)] public float VerticalForce;
        [Min(0)] public float AirborneMultiplier = 1.3f;
        [Min(0)] public float GroundedMultiplier = 1;
        [Range(0, 22)] public float MaxPushSpeed = 10;
        [Min(0)] public float StartDelay = .5f;
        public bool Repeat = true;
        public UnityEvent OnWarning = new UnityEvent();
        public UnityEvent OnBlowing = new UnityEvent();
        public UnityEvent OnStopped = new UnityEvent();
        public AirVentState State { get; private set; }
        public float CycleDuration => Mathf.Max(0, IdleDuration) + Mathf.Max(0, WarningDuration) + Mathf.Max(.1f, BlowDuration);
        public float PeakWindContribution { get; private set; }
        struct Contribution { public Vector3 velocity; public float time; public int falls; }
        readonly Dictionary<PlayerMotor, Contribution> contributions = new Dictionary<PlayerMotor, Contribution>();
        readonly HashSet<PlayerMotor> visited = new HashSet<PlayerMotor>();
        readonly Collider[] buffer = new Collider[64];
        float previousClock;
        public AirVentState Evaluate(float elapsed)
        {
            float t = elapsed - Mathf.Max(0, StartDelay);
            if (t < 0 || (!Repeat && t >= CycleDuration)) return AirVentState.Idle;
            if (Repeat) t = Mathf.Repeat(t, CycleDuration);
            if (t < Mathf.Max(0, IdleDuration)) return AirVentState.Idle;
            return t < Mathf.Max(0, IdleDuration) + Mathf.Max(0, WarningDuration) ? AirVentState.Warning : AirVentState.Blowing;
        }
        void Awake()
        {
            if (context == null) context = GetComponent<ObstacleContext>();
            if (WindTrigger == null) { Debug.LogError("AirVent requires WindTrigger", this); enabled = false; }
            Visuals(false, false);
        }
        // Apply small velocity increments after PlayerMotor's Update, using its existing damping and collision movement.
        void LateUpdate()
        {
            float clock = context != null ? context.Elapsed : Time.time;
            if (clock < previousClock || (context != null && !context.Running)) contributions.Clear();
            previousClock = clock;
            if (context != null && !context.Running) { State = AirVentState.Idle; Visuals(false, false); return; }
            var next = Evaluate(clock);
            if (next != State)
            {
                if (State == AirVentState.Blowing) OnStopped.Invoke();
                State = next;
                if (State == AirVentState.Warning) OnWarning.Invoke();
                if (State == AirVentState.Blowing) OnBlowing.Invoke();
            }
            Visuals(State == AirVentState.Warning, State == AirVentState.Blowing);
            if (State != AirVentState.Blowing || (context != null && !context.Authority)) return;
            var s = WindTrigger.transform.lossyScale;
            int count = Physics.OverlapBoxNonAlloc(WindTrigger.transform.TransformPoint(WindTrigger.center),
                Vector3.Scale(WindTrigger.size * .5f, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z))), buffer,
                WindTrigger.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            visited.Clear();
            for (int i = 0; i < count; i++)
            {
                var motor = buffer[i].GetComponentInParent<PlayerMotor>();
                if (motor == null || !motor.CanReceiveObstacle || !visited.Add(motor)) continue;
                var controller = motor.GetComponent<CharacterController>();
                float multiplier = motor.Grounded ? Mathf.Max(0, GroundedMultiplier) : Mathf.Max(0, AirborneMultiplier);
                var direction = transform.TransformDirection(WindDirection.normalized);
                var acceleration = direction * Mathf.Max(0, WindStrength) * multiplier;
                float limit = Mathf.Clamp(MaxPushSpeed, 0, 22), dt = Mathf.Min(Time.deltaTime, .1f);
                contributions.TryGetValue(motor, out var contribution);
                if (contribution.falls != motor.Identity.falls) contribution.velocity = Vector3.zero;
                // Track only this vent's contribution, including the existing motor's 8 m/s² impulse damping.
                var old = Vector3.MoveTowards(contribution.velocity, Vector3.zero, 8 * Mathf.Max(0, Time.time - contribution.time));
                var target = Vector3.ClampMagnitude(old + Vector3.ProjectOnPlane(acceleration, Vector3.up) * dt, limit);
                var impulse = target - old;
                float up = Mathf.Max(0, acceleration.y + Mathf.Max(0, VerticalForce) * multiplier);
                if (up > 0 && limit > 0) impulse.y = Mathf.Min(limit, Mathf.Max(0, controller.velocity.y) + up * dt);
                if (impulse.sqrMagnitude > .0000001f) motor.AddObstacleImpulse(impulse);
                contributions[motor] = new Contribution { velocity = target, time = Time.time, falls = motor.Identity.falls };
                PeakWindContribution = Mathf.Max(PeakWindContribution, target.magnitude, impulse.y);
            }
        }
        void Visuals(bool warning, bool wind)
        {
            if (WarningVisual != null) WarningVisual.SetActive(warning);
            if (WindVisual != null) WindVisual.SetActive(wind);
        }
        void OnDisable() { Visuals(false, false); }
    }
}
