using UnityEngine;
using ChessFight.Gameplay;
using UnityEngine.Events;

namespace ChessFight.ProtectKing
{
    public enum CastlingBridgeState { Docked, Warning, Swapping, Settling }

    [RequireComponent(typeof(ObstacleContext))]
    public sealed class CastlingCrossingBridges : MonoBehaviour
    {
        float RuntimeTime => SampleTime(ObstacleClock.Now);
        float SampleTime(double t) => ObstacleContext.PeriodicTime(t,StartDelay,CycleDuration * 2,Repeat);
        public ObstacleContext context;
        public Rigidbody BridgeA, BridgeB;
        public BoxCollider DeckA, DeckB;
        public Transform LeftDock, RightDock, StaticStructure;
        public GameObject[] WarningVisuals;
        public bool Active = true, Repeat = true;
        [Min(0)] public float StartDelay = 1;
        [Min(.1f)] public float DockedDuration = 4, WarningDuration = 1.2f, SwapDuration = 10, SettlingDuration = .4f;
        [Min(.1f)] public float DetourDistance = 8;
        [Min(.05f)] public float MinimumClearance = .1f;
        [Tooltip("Monotonic 0..1 curve; applied separately to each of the three non-intersecting path legs.")]
        public AnimationCurve MovementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public UnityEvent OnWarning = new UnityEvent(), OnSwap = new UnityEvent(), OnDocked = new UnityEvent();
        public CastlingBridgeState State { get; private set; }
        public bool ConfigurationValid { get; private set; }
        public float CycleDuration => Mathf.Max(.1f, DockedDuration) + Mathf.Max(.1f, WarningDuration) + Mathf.Max(.1f, SwapDuration) + Mathf.Max(.1f, SettlingDuration);
        bool ready, wasDisabled;
        float previousClock, pausedTime, disabledAt;
        public struct Sample { public CastlingBridgeState state; public Vector3 a, b; public int cycle; public float progress; }
        Vector3 Left => transform.InverseTransformPoint(LeftDock.position);
        Vector3 Right => transform.InverseTransformPoint(RightDock.position);
        public Sample Evaluate(float elapsed)
        {
            float t = elapsed - Mathf.Max(0, StartDelay);
            var s = new Sample { state = CastlingBridgeState.Docked, a = Left, b = Right };
            if (t < 0) return s;
            if (!Repeat && t >= CycleDuration) { s.a = Right; s.b = Left; s.progress = 1; return s; }
            s.cycle = Repeat ? Mathf.FloorToInt(t / CycleDuration) : 0;
            t = Repeat ? Mathf.Repeat(t, CycleDuration) : t;
            bool returning = (s.cycle & 1) != 0;
            if (t < Mathf.Max(.1f, DockedDuration)) s.progress = returning ? 1 : 0;
            else if ((t -= Mathf.Max(.1f, DockedDuration)) < Mathf.Max(.1f, WarningDuration))
            { s.state = CastlingBridgeState.Warning; s.progress = returning ? 1 : 0; }
            else if ((t -= Mathf.Max(.1f, WarningDuration)) < Mathf.Max(.1f, SwapDuration))
            { s.state = CastlingBridgeState.Swapping; s.progress = returning ? 1 - t / Mathf.Max(.1f, SwapDuration) : t / Mathf.Max(.1f, SwapDuration); }
            else { s.state = CastlingBridgeState.Settling; s.progress = returning ? 0 : 1; }
            s.a = PositionAt(s.progress, true); s.b = PositionAt(s.progress, false); return s;
        }
        public Vector3 PositionAt(float progress, bool a)
        {
            var start = a ? Left : Right; var end = a ? Right : Left;
            var offset = Vector3.forward * (a ? DetourDistance : -DetourDistance);
            float p = Mathf.Clamp01(progress) * 3;
            if (p < 1) return Vector3.Lerp(start, start + offset, Ease(p));
            if (p < 2) return Vector3.Lerp(start + offset, end + offset, Ease(p - 1));
            return Vector3.Lerp(end + offset, end, Ease(p - 2));
        }
        float Ease(float value) => Mathf.Clamp01(MovementCurve != null && MovementCurve.length >= 2 ? MovementCurve.Evaluate(value) : Mathf.SmoothStep(0, 1, value));
        public bool ValidateConfiguration(out string reason)
        {
            reason = "";
            if (BridgeA == null || BridgeB == null || BridgeA == BridgeB || DeckA == null || DeckB == null || LeftDock == null || RightDock == null || StaticStructure == null)
            { reason = "Assign two distinct bridge bodies, deck colliders, docks and StaticStructure."; return false; }
            if (Vector3.Dot(transform.up, Vector3.up) < .999f || (transform.lossyScale - Vector3.one).sqrMagnitude > .0001f)
            { reason = "Use unit root scale and Y-axis rotation only."; return false; }
            if (Mathf.Abs(Left.y - Right.y) > .01f || Mathf.Abs(Left.z - Right.z) > .01f || Left.x >= Right.x)
            { reason = "Docks must be at the same local Y/Z with LeftDock left of RightDock."; return false; }
            if (DeckA.attachedRigidbody != BridgeA || DeckB.attachedRigidbody != BridgeB)
            { reason = "Each deck collider must belong to its own kinematic bridge body."; return false; }
            if (Quaternion.Angle(BridgeA.rotation, transform.rotation) > .01f || Quaternion.Angle(BridgeB.rotation, transform.rotation) > .01f)
            { reason = "Keep bridge bodies aligned with the root; rotate the prefab root instead."; return false; }
            float prior = -1;
            for (int i = 0; i <= 60; i++)
            {
                float v = MovementCurve == null ? -1 : MovementCurve.Evaluate(i / 60f);
                if (v < prior - .0001f || v < -.001f || v > 1.001f || (i == 0 && Mathf.Abs(v) > .001f) || (i == 60 && Mathf.Abs(v - 1) > .001f))
                { reason = "MovementCurve must increase from 0 to 1 without overshoot."; return false; }
                prior = v;
            }
            var ba = LocalBounds(DeckA); ba.center -= transform.InverseTransformPoint(BridgeA.position);
            var bb = LocalBounds(DeckB); bb.center -= transform.InverseTransformPoint(BridgeB.position);
            float clearance = Mathf.Max(.05f, MinimumClearance);
            if (Right.x - Left.x < (ba.size.x + bb.size.x) * .5f + Mathf.Abs(ba.center.x - bb.center.x) + clearance ||
                2 * DetourDistance < (ba.size.z + bb.size.z) * .5f + Mathf.Abs(ba.center.z - bb.center.z) + clearance)
            { reason = "Increase dock separation or detour distance: the two decks would intersect."; return false; }
            var fixedColliders = StaticStructure.GetComponentsInChildren<BoxCollider>(true);
            // Each leg is axis aligned and monotonic, so its union AABB covers the entire sweep, not just samples.
            for (int b = 0; b < 2; b++) for (int leg = 0; leg < 3; leg++)
            {
                var bounds = b == 0 ? ba : bb;
                var from = PositionAt(leg / 3f, b == 0); var to = PositionAt((leg + 1) / 3f, b == 0);
                bounds.center += (from + to) * .5f;
                bounds.size += new Vector3(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y), Mathf.Abs(to.z - from.z));
                bounds.Expand(clearance * 2);
                foreach (var fixedCollider in fixedColliders)
                    if (fixedCollider.enabled && !fixedCollider.isTrigger && bounds.Intersects(LocalBounds(fixedCollider)))
                    { reason = "Bridge sweep overlaps fixed structure: " + fixedCollider.name; return false; }
            }
            return true;
        }
        Bounds LocalBounds(BoxCollider box)
        {
            var bounds = new Bounds(transform.InverseTransformPoint(box.transform.TransformPoint(box.center)), Vector3.zero);
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                bounds.Encapsulate(transform.InverseTransformPoint(box.transform.TransformPoint(box.center + Vector3.Scale(box.size * .5f, new Vector3(x, y, z)))));
            return bounds;
        }
        void Awake()
        {
            if (context == null) context = GetComponent<ObstacleContext>();
            ConfigurationValid = ValidateConfiguration(out string reason);
            if (!ConfigurationValid) { Debug.LogError("CastlingCrossingBridges: " + reason, this); enabled = false; return; }
            foreach (var body in new[] { BridgeA, BridgeB })
            { body.isKinematic = true; body.useGravity = false; body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; }
            var sample = Evaluate(false ? -1 : RuntimeTime);
            BridgeA.position = transform.TransformPoint(sample.a); BridgeB.position = transform.TransformPoint(sample.b);
            ImportedMovingSurface.Bind(BridgeA,t=>new Pose(transform.TransformPoint(Evaluate(SampleTime(t)-pausedTime).a),BridgeA.rotation),()=>isActiveAndEnabled && Active && context.Running);
            ImportedMovingSurface.Bind(BridgeB,t=>new Pose(transform.TransformPoint(Evaluate(SampleTime(t)-pausedTime).b),BridgeB.rotation),()=>isActiveAndEnabled && Active && context.Running);
            previousClock = RuntimeTime; ready = true; SetWarning(false);
        }
        void OnEnable()
        {
            if (!ready || !wasDisabled) return;
            pausedTime += Mathf.Max(0, RuntimeTime - disabledAt); previousClock = RuntimeTime; wasDisabled = false;
        }
        void FixedUpdate()
        {
            if (!ready) return;
            float now = RuntimeTime;
            bool parked = false;
            if (now < previousClock || parked) pausedTime = 0;
            float step = Mathf.Max(0, now - previousClock); previousClock = now;
            if (!Active) { pausedTime += step; SetWarning(false); return; }
            if (!context.Running && !parked) { SetWarning(false); return; }
            var sample = Evaluate(parked ? -1 : now - pausedTime);
            if (sample.state != State)
            {
                State = sample.state;
                if (State == CastlingBridgeState.Warning) OnWarning.Invoke();
                if (State == CastlingBridgeState.Swapping) OnSwap.Invoke();
                if (State == CastlingBridgeState.Settling) OnDocked.Invoke();
            }
            BridgeA.MovePosition(transform.TransformPoint(sample.a)); BridgeB.MovePosition(transform.TransformPoint(sample.b));
            SetWarning(State == CastlingBridgeState.Warning && Mathf.Repeat(RuntimeTime * 4, 1) < .65f);
        }
        void SetWarning(bool visible)
        { if (WarningVisuals != null) foreach (var visual in WarningVisuals) if (visual != null) visual.SetActive(visible); }
        void OnDisable() { if (ready) { disabledAt = RuntimeTime; wasDisabled = true; } SetWarning(false); }
        void OnDrawGizmosSelected()
        {
            if (LeftDock == null || RightDock == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            for (int bridge = 0; bridge < 2; bridge++)
            {
                Gizmos.color = bridge == 0 ? Color.cyan : Color.yellow;
                for (int i = 0; i < 60; i++) Gizmos.DrawLine(PositionAt(i / 60f, bridge == 0), PositionAt((i + 1) / 60f, bridge == 0));
                var deck = bridge == 0 ? DeckA : DeckB;
                if (deck != null)
                {
                    var bounds = LocalBounds(deck);
                    for (int i = 0; i <= 3; i++) Gizmos.DrawWireCube(PositionAt(i / 3f, bridge == 0) + Vector3.down * bounds.extents.y, bounds.size + Vector3.one * MinimumClearance * 2);
                }
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
