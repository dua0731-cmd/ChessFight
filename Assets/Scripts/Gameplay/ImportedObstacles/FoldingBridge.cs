using UnityEngine;
using ChessFight.Gameplay;
using UnityEngine.Events;

namespace ChessFight.ProtectKing
{
    public enum FoldingBridgeState { Open, Warning, Folding, Closed, Unfolding }

    // Two kinematic panels reuse ImportedObstacleActor's existing moving-support handling.
    public sealed class FoldingBridge : MonoBehaviour
    {
        float RuntimeTime => SampleTime(ObstacleClock.Now);
        float SampleTime(double t) => ObstacleContext.PeriodicTime(t+phaseOffset,StartDelay,CycleDuration,Repeat);
        public ObstacleContext context;
        public Rigidbody LeftPivot;
        public Rigidbody RightPivot;
        [Min(0)] public float OpenDuration = 5;
        [Min(0)] public float WarningDuration = 1.5f;
        [Min(.25f)] public float FoldDuration = 2.5f;
        [Min(0)] public float ClosedDuration = 2;
        [Range(0, 80)] public float FoldAngle = 70;
        [Min(0)] public float StartDelay = 1;
        public bool Repeat = true;
        [Tooltip("Seconds added to the shared obstacle clock: the same cycle, earlier or later.")]
        public float phaseOffset;
        public bool Simultaneous = true;
        [Min(0), Tooltip("Right panel delay when Simultaneous is disabled.")]
        public float RightDelay = .6f;
        public Renderer[] WarningVisuals;
        [Min(.1f)] public float WarningFlashRate = 4;
        public AudioSource WarningAudio;
        public AudioClip WarningClip;
        public UnityEvent OnWarning = new UnityEvent();
        public UnityEvent OnFolding = new UnityEvent();
        public UnityEvent OnClosed = new UnityEvent();
        public UnityEvent OnOpened = new UnityEvent();
        public FoldingBridgeState State { get; private set; }
        public float LeftAngle { get; private set; }
        public float RightAngle { get; private set; }
        public bool WarningLit { get; private set; }
        const float MaxDegreesPerSecond = 60;
        public float EffectiveFoldDuration => Mathf.Max(.25f, FoldDuration, Mathf.Clamp(FoldAngle, 0, 80) * 1.5f / MaxDegreesPerSecond);
        float Delay => Simultaneous ? 0 : Mathf.Max(0, RightDelay);
        public float CycleDuration => Mathf.Max(0, OpenDuration) + Mathf.Max(0, WarningDuration) +
            2 * (EffectiveFoldDuration + Delay) + Mathf.Max(0, ClosedDuration);
        Quaternion leftRest, rightRest;
        bool ready;
        float previousTime;

        public struct Sample
        {
            public FoldingBridgeState state;
            public float left, right;
        }
        public Sample Evaluate(float elapsed)
        {
            float t = elapsed - Mathf.Max(0, StartDelay);
            if (t < 0 || (!Repeat && t >= CycleDuration)) return new Sample { state = FoldingBridgeState.Open };
            if (Repeat) t = Mathf.Repeat(t, CycleDuration);
            if (t < Mathf.Max(0, OpenDuration)) return new Sample { state = FoldingBridgeState.Open };
            t -= Mathf.Max(0, OpenDuration);
            if (t < Mathf.Max(0, WarningDuration)) return new Sample { state = FoldingBridgeState.Warning };
            t -= Mathf.Max(0, WarningDuration);
            float duration = EffectiveFoldDuration, angle = Mathf.Clamp(FoldAngle, 0, 80);
            if (t < duration + Delay) return new Sample { state = FoldingBridgeState.Folding,
                left = angle * Smooth(t / duration), right = angle * Smooth((t - Delay) / duration) };
            t -= duration + Delay;
            if (t < Mathf.Max(0, ClosedDuration)) return new Sample { state = FoldingBridgeState.Closed, left = angle, right = angle };
            t -= Mathf.Max(0, ClosedDuration);
            return new Sample { state = FoldingBridgeState.Unfolding,
                left = angle * (1 - Smooth(t / duration)), right = angle * (1 - Smooth((t - Delay) / duration)) };
        }
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        void Awake()
        {
            if (context == null) context = GetComponent<ObstacleContext>();
            if (LeftPivot == null || RightPivot == null || LeftPivot == RightPivot)
            { Debug.LogError("FoldingBridge requires two distinct pivot Rigidbodies.", this); enabled = false; return; }
            leftRest = LeftPivot.transform.localRotation; rightRest = RightPivot.transform.localRotation;
            Configure(LeftPivot); Configure(RightPivot);
            ImportedMovingSurface.Bind(LeftPivot,t=>new Pose(LeftPivot.position,Rotation(LeftPivot,leftRest,Evaluate(SampleTime(t)).left)),()=>isActiveAndEnabled && (context==null || context.Running));
            ImportedMovingSurface.Bind(RightPivot,t=>new Pose(RightPivot.position,Rotation(RightPivot,rightRest,-Evaluate(SampleTime(t)).right)),()=>isActiveAndEnabled && (context==null || context.Running));
            previousTime = RuntimeTime;
            var pose = Evaluate(previousTime); LeftAngle = pose.left; RightAngle = pose.right;
            LeftPivot.rotation = Rotation(LeftPivot, leftRest, LeftAngle);
            RightPivot.rotation = Rotation(RightPivot, rightRest, -RightAngle);
            ready = true; SetWarning(false);
        }
        static void Configure(Rigidbody body)
        {
            body.isKinematic = true; body.useGravity = false; body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
        static Quaternion Rotation(Rigidbody body, Quaternion rest, float angle) =>
            (body.transform.parent != null ? body.transform.parent.rotation : Quaternion.identity) * rest * Quaternion.AngleAxis(angle, Vector3.forward);
        void FixedUpdate()
        {
            if (!ready) return;
            float time = RuntimeTime;
            bool reset = time < previousTime; previousTime = time;
            bool parked = false;
            if (context != null && !context.Running && !reset && !parked) { SetWarning(false); return; }
            var sample = parked ? new Sample { state = FoldingBridgeState.Open } : Evaluate(time);
            LeftAngle = sample.left;
            RightAngle = sample.right;
            LeftPivot.MoveRotation(Rotation(LeftPivot, leftRest, LeftAngle));
            RightPivot.MoveRotation(Rotation(RightPivot, rightRest, -RightAngle));
            if (State != sample.state)
            {
                State = sample.state;
                if (State == FoldingBridgeState.Warning)
                {
                    OnWarning.Invoke();
                    if (WarningAudio != null && WarningClip != null) WarningAudio.PlayOneShot(WarningClip);
                }
                else if (State == FoldingBridgeState.Folding) OnFolding.Invoke();
                else if (State == FoldingBridgeState.Closed) OnClosed.Invoke();
                else if (State == FoldingBridgeState.Open) OnOpened.Invoke();
            }
            SetWarning(State == FoldingBridgeState.Warning && Mathf.Repeat(time * Mathf.Max(.1f, WarningFlashRate), 1) < .5f);
        }
        void SetWarning(bool lit)
        {
            WarningLit = lit;
            if (WarningVisuals == null) return;
            foreach (var visual in WarningVisuals) if (visual != null) visual.enabled = lit;
        }
        void OnDisable() { SetWarning(false); }
    }
}
