using UnityEngine;
using ChessFight.Gameplay;
using UnityEngine.Events;

namespace ChessFight.ProtectKing
{
    public enum FloorPistonState { Idle, Warning, Rising, Hold, Falling }

    public sealed class FloorPiston : Obstacle
    {
        float RuntimeTime => SampleTime(SharedTime);
        float SampleTime(double t) => ObstacleContext.PeriodicTime(t,StartDelay,CycleDuration,Repeat);
        public ObstacleContext context;
        public Rigidbody Piston;
        public GameObject WarningArea;
        [Min(0)] [SerializeField] public float IdleDuration = 3;
        [Min(0)] [SerializeField] public float WarningDuration = .8f;
        [Min(.1f)] [SerializeField] public float RiseDuration = 2f;
        [Min(0)] [SerializeField] public float HoldDuration = .6f;
        [Min(.1f)] [SerializeField] public float FallDuration = 2f;
        [Min(0)] [SerializeField] public float RiseHeight = 2.5f;
        [SerializeField, Min(.1f), Tooltip("Acceleration cap in m/s squared, also capped below gravity to avoid throwing riders off at the top.")]
        float maximumAcceleration = 4f;
        float SafeAcceleration => Mathf.Min(Mathf.Max(.1f, maximumAcceleration), Mathf.Max(.1f, Mathf.Abs(Physics.gravity.y) * .5f));
        public float EffectiveRiseDuration => Mathf.Max(.1f, RiseDuration, Mathf.Sqrt(6f * Mathf.Max(0f, RiseHeight) / SafeAcceleration));
        public float EffectiveFallDuration => Mathf.Max(.1f, FallDuration, Mathf.Sqrt(6f * Mathf.Max(0f, RiseHeight) / SafeAcceleration));
        [Min(0)] [SerializeField] public float StartDelay = .5f;
        [SerializeField] public bool Repeat = true;
        public UnityEvent OnWarning = new UnityEvent();
        public UnityEvent OnRise = new UnityEvent();
        public FloorPistonState State { get; private set; }
        public float Height { get; private set; }
        public float CycleDuration => Mathf.Max(0, IdleDuration) + Mathf.Max(0, WarningDuration) +
            EffectiveRiseDuration + Mathf.Max(0, HoldDuration) + EffectiveFallDuration;
        Vector3 pistonRest;
        bool ready;
        public struct Sample { public FloorPistonState state; public float height; public int cycle; }
        public Sample Evaluate(float elapsed)
        {
            float t = elapsed - Mathf.Max(0, StartDelay);
            var sample = new Sample { state = FloorPistonState.Idle, cycle = t < 0 ? -1 : Repeat ? Mathf.FloorToInt(t / CycleDuration) : 0 };
            if (t < 0 || (!Repeat && t >= CycleDuration)) return sample;
            if (Repeat) t = Mathf.Repeat(t, CycleDuration);
            if (t < Mathf.Max(0, IdleDuration)) return sample;
            t -= Mathf.Max(0, IdleDuration);
            if (t < Mathf.Max(0, WarningDuration)) { sample.state = FloorPistonState.Warning; return sample; }
            t -= Mathf.Max(0, WarningDuration);
            if (t < EffectiveRiseDuration)
            { sample.state = FloorPistonState.Rising; sample.height = Mathf.Max(0, RiseHeight) * Smooth(t / EffectiveRiseDuration); return sample; }
            t -= EffectiveRiseDuration;
            if (t < Mathf.Max(0, HoldDuration)) { sample.state = FloorPistonState.Hold; sample.height = Mathf.Max(0, RiseHeight); return sample; }
            t -= Mathf.Max(0, HoldDuration);
            sample.state = FloorPistonState.Falling;
            sample.height = Mathf.Max(0, RiseHeight) * (1 - Smooth(t / EffectiveFallDuration));
            return sample;
        }
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        void Awake()
        {
            if (context == null) context = GetComponent<ObstacleContext>();
            if (Piston == null)
            { Debug.LogError("FloorPiston requires a Piston reference.", this); enabled = false; return; }
            Piston.isKinematic = true; Piston.useGravity = false; Piston.interpolation = RigidbodyInterpolation.Interpolate;
            Piston.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            pistonRest = transform.InverseTransformPoint(Piston.position);
            var sample = Evaluate(RuntimeTime); Height = sample.height;
            Piston.position = transform.TransformPoint(pistonRest + Vector3.up * Height);
            ImportedMovingSurface.Bind(Piston,t=>new Pose(transform.TransformPoint(pistonRest+Vector3.up*Evaluate(SampleTime(t)).height),Piston.rotation),()=>isActiveAndEnabled && (context==null || context.Running));
            ready = true; if (WarningArea != null) WarningArea.SetActive(false);
        }
        void FixedUpdate()
        {
            if (!ready) return;
            float time = RuntimeTime;
            if (context != null && !context.Running)
            { if (WarningArea != null) WarningArea.SetActive(false); return; }
            var sample = Evaluate(time);
            Height = sample.height;
            if (State != sample.state)
            {
                State = sample.state;
                if (State == FloorPistonState.Warning) OnWarning.Invoke();
                if (State == FloorPistonState.Rising) OnRise.Invoke();
            }
            Piston.MovePosition(transform.TransformPoint(pistonRest + Vector3.up * Height));
            if (WarningArea != null) WarningArea.SetActive(State == FloorPistonState.Warning);
        }
        void OnDisable() { if (WarningArea != null) WarningArea.SetActive(false); }
    }
}
