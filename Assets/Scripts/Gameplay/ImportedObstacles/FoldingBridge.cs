using UnityEngine;
using ChessFight.Gameplay;
using UnityEngine.Events;

namespace ChessFight.ProtectKing
{
    public enum FoldingBridgeState { Open = 0, Folding = 2, Closed = 3 }

    // StateDrivenMover: external contact loads fold each panel downward. Offline physics.
    [DefaultExecutionOrder(-100)]
    public sealed class FoldingBridge : Obstacle
    {
        public ObstacleContext context;
        public Rigidbody LeftPivot, RightPivot;
        [SerializeField, Range(0f, 85f), Tooltip("Maximum downward angle, not a forced target. Reduce Return Spring if the panel stops before this limit.")] public float FoldAngle = 70f;
        [SerializeField, Min(1f)] float panelMass = 12f;
        [SerializeField, Min(0f), Tooltip("Returns an unloaded panel to horizontal. Lower values fold more easily under external load; zero disables return.")] float returnSpring = 150f;
        [SerializeField, Min(0f)] float returnDamper = 250f;
        [SerializeField, Min(1f)] float maximumFoldSpeed = 45f;
        public UnityEvent OnFolding = new UnityEvent(), OnClosed = new UnityEvent(), OnOpened = new UnityEvent();
        public FoldingBridgeState State { get; private set; }
        public float LeftAngle => leftJoint != null ? Mathf.Abs(leftJoint.angle) : 0f;
        public float RightAngle => rightJoint != null ? Mathf.Abs(rightJoint.angle) : 0f;
        float appliedFoldAngle;
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
            ApplySettings();
        }
        HingeJoint ConfigurePivot(Rigidbody body)
        {
            // The hinge supports the panel's own weight. Other bodies retain their
            // gravity and transmit their load through the normal collision solver.
            body.useGravity = false;
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
            appliedFoldAngle = FoldAngle;
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
            limits.bounciness = 0f; limits.contactDistance = Mathf.Min(.5f, Mathf.Clamp(FoldAngle, 0f, 85f) * .1f);
            joint.limits = limits; joint.useLimits = true;
            var spring = joint.spring;
            spring.spring = Mathf.Max(0f, returnSpring); spring.damper = Mathf.Max(0f, returnDamper); spring.targetPosition = 0f;
            joint.spring = spring; joint.useSpring = returnSpring > 0f;
            body.isKinematic = !Simulating;
            // Preserve the actual mass distribution and rotational inertia for contact loads.
            body.ResetCenterOfMass(); body.ResetInertiaTensor();
            if (!body.isKinematic) body.WakeUp();
        }
        void FixedUpdate()
        {
            if (!ready) return;
            if (dirty || appliedFoldAngle != FoldAngle || LeftPivot.isKinematic == Simulating || RightPivot.isKinematic == Simulating) ApplySettings();
            if (!Simulating) return;
            float angle = Mathf.Max(LeftAngle, RightAngle);
            var next = angle < .5f ? FoldingBridgeState.Open : angle >= Mathf.Max(0f, FoldAngle - 1f)
                ? FoldingBridgeState.Closed : FoldingBridgeState.Folding;
            if (State != next)
            {
                State = next;
                if (State == FoldingBridgeState.Folding) OnFolding.Invoke();
                else if (State == FoldingBridgeState.Closed) OnClosed.Invoke();
                else if (State == FoldingBridgeState.Open) OnOpened.Invoke();
            }
        }
        void OnEnable() { dirty = true; }
        void OnDisable()
        {
            dirty = true;
            if (ready) { LeftPivot.isKinematic = true; RightPivot.isKinematic = true; }
        }
    }
}
