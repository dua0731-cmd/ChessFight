using UnityEngine;
using ChessFight.Gameplay;
using UnityEngine.Events;

namespace ChessFight.ProtectKing
{
    public enum FoldingBridgeState { Open, Warning, Folding, Closed, Unfolding }

    // StateDrivenMover: gravity and actual contact torque fold each panel downward. Offline physics.
    [DefaultExecutionOrder(-100)]
    public sealed class FoldingBridge : Obstacle
    {
        public ObstacleContext context;
        public Rigidbody LeftPivot, RightPivot;
        [SerializeField, Range(0f, 85f), Tooltip("Maximum downward folding angle of each panel.")] public float FoldAngle = 70f;
        [SerializeField, Min(1f)] float panelMass = 12f;
        [SerializeField, Min(0f), Tooltip("Return spring balancing the panel and rider weight. Lower values fold more easily.")] float returnSpring = 1500f;
        [SerializeField, Min(0f)] float returnDamper = 250f;
        [SerializeField, Min(1f)] float maximumFoldSpeed = 45f;
        [SerializeField, Range(0f, 1f)] float warningFraction = .75f;
        public Renderer[] WarningVisuals;
        [SerializeField, Min(.1f)] public float WarningFlashRate = 4f;
        public AudioSource WarningAudio;
        public AudioClip WarningClip;
        public UnityEvent OnWarning = new UnityEvent(), OnFolding = new UnityEvent(), OnClosed = new UnityEvent(), OnOpened = new UnityEvent();
        public FoldingBridgeState State { get; private set; }
        public float LeftAngle => leftJoint != null ? Mathf.Abs(leftJoint.angle) : 0f;
        public float RightAngle => rightJoint != null ? Mathf.Abs(rightJoint.angle) : 0f;
        public bool WarningLit { get; private set; }
        HingeJoint leftJoint, rightJoint;
        bool ready, dirty = true;
        bool Simulating => isActiveAndEnabled && (context == null || (context.Running && context.Authority));

        void Awake()
        {
            if (context == null) context = GetComponent<ObstacleContext>();
            if (LeftPivot == null || RightPivot == null || LeftPivot == RightPivot)
            { Debug.LogError("FoldingBridge requires two distinct pivot Rigidbodies.", this); enabled = false; return; }
            leftJoint = ConfigurePivot(LeftPivot);
            rightJoint = ConfigurePivot(RightPivot);
            ready = true;
            ApplySettings(); SetWarning(false);
        }
        HingeJoint ConfigurePivot(Rigidbody body)
        {
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations = 24; body.solverVelocityIterations = 12;
            var joint = body.GetComponent<HingeJoint>();
            if (joint == null) joint = body.gameObject.AddComponent<HingeJoint>();
            joint.connectedBody = null; joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero; joint.axis = Vector3.forward;
            joint.connectedAnchor = body.position; joint.useMotor = false;
            ImportedMovingSurface.BindDynamic(body, point => SurfaceVelocity(body, point),
                () => AngularStep(body), () => Simulating);
            return joint;
        }
        static Vector3 SurfaceVelocity(Rigidbody body, Vector3 point)
        {
            Vector3 normal = body.rotation * Vector3.up, offset = point - body.position;
            offset -= normal * Vector3.Dot(offset, normal); // The authored top is at the hinge's local y=0.
            return body.GetPointVelocity(body.position + offset);
        }
        static Quaternion AngularStep(Rigidbody body) => body.angularVelocity.sqrMagnitude > .000001f
            ? Quaternion.AngleAxis(body.angularVelocity.magnitude * Time.fixedDeltaTime * Mathf.Rad2Deg, body.angularVelocity.normalized)
            : Quaternion.identity;
        void OnValidate() { dirty = true; }
        void ApplySettings()
        {
            if (!ready) return;
            ApplyPanel(LeftPivot, leftJoint, true); ApplyPanel(RightPivot, rightJoint, false);
            dirty = false;
        }
        void ApplyPanel(Rigidbody body, HingeJoint joint, bool left)
        {
            body.mass = Mathf.Max(1f, panelMass);
            body.maxAngularVelocity = Mathf.Max(1f, maximumFoldSpeed) * Mathf.Deg2Rad;
            // Left panel extends +X, right panel -X: only downward rotation is allowed.
            var limits = joint.limits;
            limits.min = left ? -Mathf.Clamp(FoldAngle, 0f, 85f) : 0f;
            limits.max = left ? 0f : Mathf.Clamp(FoldAngle, 0f, 85f);
            limits.bounciness = 0f; limits.contactDistance = .5f;
            joint.limits = limits; joint.useLimits = true;
            var spring = joint.spring;
            spring.spring = Mathf.Max(0f, returnSpring); spring.damper = Mathf.Max(0f, returnDamper); spring.targetPosition = 0f;
            joint.spring = spring; joint.useSpring = returnSpring > 0f;
            body.isKinematic = !Simulating;
            // Unlike the balanced bridge, each panel's centre of mass must remain away from its hinge.
            body.ResetCenterOfMass(); body.ResetInertiaTensor();
            if (!body.isKinematic) body.WakeUp();
        }
        void FixedUpdate()
        {
            if (!ready) return;
            if (dirty || LeftPivot.isKinematic == Simulating || RightPivot.isKinematic == Simulating) ApplySettings();
            if (!Simulating) { SetWarning(false); return; }
            float angle = Mathf.Max(LeftAngle, RightAngle);
            var next = angle < .5f ? FoldingBridgeState.Open : angle >= Mathf.Max(0f, FoldAngle - 1f)
                ? FoldingBridgeState.Closed : angle >= FoldAngle * warningFraction ? FoldingBridgeState.Warning : FoldingBridgeState.Folding;
            if (State != next)
            {
                State = next;
                if (State == FoldingBridgeState.Warning)
                {
                    OnWarning.Invoke();
                    if (WarningAudio != null && WarningClip != null) WarningAudio.PlayOneShot(WarningClip);
                }
                else if (State == FoldingBridgeState.Folding) OnFolding.Invoke();
                else if (State == FoldingBridgeState.Closed) OnClosed.Invoke();
                else if (State == FoldingBridgeState.Open) OnOpened.Invoke();
            }
            SetWarning(FoldAngle > .5f && angle >= FoldAngle * warningFraction && Mathf.Repeat(Time.time * Mathf.Max(.1f, WarningFlashRate), 1f) < .5f);
        }
        void SetWarning(bool lit)
        {
            WarningLit = lit;
            if (WarningVisuals != null) foreach (var visual in WarningVisuals) if (visual != null) visual.enabled = lit;
        }
        void OnEnable() { dirty = true; }
        void OnDisable()
        {
            SetWarning(false); dirty = true;
            if (ready) { LeftPivot.isKinematic = true; RightPivot.isKinematic = true; }
        }
    }
}
