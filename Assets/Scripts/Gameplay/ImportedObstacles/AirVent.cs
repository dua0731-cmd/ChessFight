using System.Collections.Generic;
using UnityEngine;
using ChessFight.Gameplay;
using UnityEngine.Events;

namespace ChessFight.ProtectKing
{
    public enum AirVentShape { Box, Cylinder }
    public enum AirVentState { Idle, Warning, Blowing }
    public sealed class AirVent : Obstacle
    {
        [SerializeField, HideInInspector] AirVentShape shape = AirVentShape.Box;
        public ObstacleContext context;
        public BoxCollider WindTrigger;
        public GameObject WarningVisual, WindVisual;
        public Transform VFXPoint;
        [Min(0)] [SerializeField] public float IdleDuration = 3;
        [Min(0)] [SerializeField] public float WarningDuration = .8f;
        [Min(.1f)] [SerializeField] public float BlowDuration = 3;
        [Min(0)] [SerializeField] public float WindStrength = 20;
        [SerializeField] public Vector3 WindDirection = Vector3.forward;
        [Min(0)] [SerializeField] public float VerticalForce;
        [Min(0)] [SerializeField] public float AirborneMultiplier = 1.3f;
        [Min(0)] [SerializeField] public float GroundedMultiplier = 1;
        [Range(0, 22)] [SerializeField] public float MaxPushSpeed = 10;
        [Min(0)] [SerializeField] public float StartDelay = .5f;
        [SerializeField] public bool Repeat = true;
        public UnityEvent OnWarning = new UnityEvent();
        public UnityEvent OnBlowing = new UnityEvent();
        public UnityEvent OnStopped = new UnityEvent();
        public AirVentState State { get; private set; }
        public float CycleDuration => Mathf.Max(0, IdleDuration) + Mathf.Max(0, WarningDuration) + Mathf.Max(.1f, BlowDuration);
        public float PeakWindContribution { get; private set; }
        readonly HashSet<ImportedObstacleActor> visited = new HashSet<ImportedObstacleActor>();
        readonly Collider[] buffer = new Collider[512];
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
        // Continuous bounded acceleration through existing dynamic bodies; never repeated hit/stagger.
        void FixedUpdate()
        {
            float clock = context != null ? context.Elapsed : Time.time;
            if (clock < previousClock || (context != null && !context.Running)) visited.Clear();
            previousClock = clock;
            if (context != null && !context.Running) { State = AirVentState.Idle; Visuals(false, false); return; }
            var next = Evaluate(ObstacleContext.PeriodicTime(SharedTime,StartDelay,CycleDuration,Repeat));
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
                if (shape == AirVentShape.Cylinder && !OverlapsCylinder(buffer[i])) continue;
                var motor = ImportedObstacleActor.Find(buffer[i]);
                if (motor == null || !motor.CanReceiveObstacle || !visited.Add(motor)) continue;
                float multiplier = motor.Grounded ? Mathf.Max(0,GroundedMultiplier) : Mathf.Max(0,AirborneMultiplier);
                Vector3 acceleration = (transform.TransformDirection(WindDirection.normalized)*Mathf.Max(0,WindStrength)
                    + Vector3.up*Mathf.Max(0,VerticalForce))*multiplier;
                var change = motor.Accelerate(acceleration,Mathf.Max(0,MaxPushSpeed),Time.fixedDeltaTime);
                PeakWindContribution=Mathf.Max(PeakWindContribution,change.magnitude);

            }
        }
        bool OverlapsCylinder(Collider other)
        {
            // Box broad phase, then test actual body contact against the round cross-section.
            var volume = WindTrigger.transform;
            Vector3 half = WindTrigger.size * .5f;
            Vector3 local = volume.InverseTransformPoint(other.bounds.center) - WindTrigger.center;
            Vector3 axis = WindTrigger.center + Vector3.forward * Mathf.Clamp(local.z, -half.z, half.z);
            Vector3 closest = volume.InverseTransformPoint(other.ClosestPoint(volume.TransformPoint(axis))) - WindTrigger.center;
            float x = closest.x / Mathf.Max(.001f, half.x), y = closest.y / Mathf.Max(.001f, half.y);
            return x * x + y * y <= 1.0001f && Mathf.Abs(closest.z) <= half.z + .001f;
        }
        void OnDrawGizmosSelected()
        {
            if (WindTrigger == null) return;
            var saved = Gizmos.matrix; Gizmos.matrix = WindTrigger.transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            if (shape == AirVentShape.Box) Gizmos.DrawWireCube(WindTrigger.center, WindTrigger.size);
            else
            {
                Vector3 half = WindTrigger.size * .5f, centre = WindTrigger.center;
                for (int i = 0; i < 32; i++)
                {
                    float a = i * 2f * Mathf.PI / 32f, b = (i + 1) * 2f * Mathf.PI / 32f;
                    Vector3 p = new Vector3(Mathf.Cos(a) * half.x, Mathf.Sin(a) * half.y, 0f);
                    Vector3 q = new Vector3(Mathf.Cos(b) * half.x, Mathf.Sin(b) * half.y, 0f);
                    Gizmos.DrawLine(centre + p - Vector3.forward * half.z, centre + q - Vector3.forward * half.z);
                    Gizmos.DrawLine(centre + p + Vector3.forward * half.z, centre + q + Vector3.forward * half.z);
                    if (i % 8 == 0) Gizmos.DrawLine(centre + p - Vector3.forward * half.z, centre + p + Vector3.forward * half.z);
                }
            }
            Gizmos.matrix = saved;
        }
        void Visuals(bool warning, bool wind)
        {
            if (WarningVisual != null) WarningVisual.SetActive(warning);
            if (WindVisual != null) WindVisual.SetActive(wind);
        }
        void OnDisable() { Visuals(false, false); }
    }
}
