using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ChessFight.ProtectKing
{
    public enum SpringPillarState { Ready, Warning, Extend, Hold, Retract, Cooldown }
    // StateDrivenMover: fired when a player comes close, not by ObstacleClock. Offline only
    // until the host sends its state (Docs/KingRush/MAP_IMPORT.md).
    public sealed class SpringPillar : MonoBehaviour
    {
        public ObstacleContext context;
        public Rigidbody Pillar;
        public BoxCollider TriggerArea, HitArea;
        public GameObject WarningVisual;
        [Min(0)] public float WarningDuration = .5f;
        [Min(0)] public float ExtendDistance = 3;
        [Min(.1f)] public float ExtendDuration = .2f;
        [Min(0)] public float HoldDuration = .6f;
        [Min(.1f)] public float RetractDuration = .8f;
        [Min(.1f)] public float Cooldown = 2;
        [Min(0)] public float KnockbackForce = 12;
        [Min(0)] public float UpwardForce = 1.5f;
        public bool AutoTrigger = true, Repeat = true, RequireReentry = true;
        public UnityEvent OnWarning = new UnityEvent();
        public UnityEvent OnExtend = new UnityEvent();
        public SpringPillarState State { get; private set; }
        public int ActivationCount { get; private set; }
        public int HitsThisActivation => struck.Count;
        public float Extension { get; private set; }
        readonly Collider[] buffer = new Collider[512];
        readonly HashSet<ImportedObstacleActor> previous = new HashSet<ImportedObstacleActor>();
        readonly HashSet<ImportedObstacleActor> present = new HashSet<ImportedObstacleActor>();
        readonly HashSet<ImportedObstacleActor> struck = new HashSet<ImportedObstacleActor>();
        Vector3 rest, hitCenter, hitSize;
        float started, previousTime;
        bool running, ready;
        float Clock => context != null ? context.Elapsed : Time.time;
        void Awake()
        {
            if (context == null) context = GetComponent<ObstacleContext>();
            if (Pillar == null || TriggerArea == null || HitArea == null) { Debug.LogError("SpringPillar references missing", this); enabled = false; return; }
            Pillar.isKinematic = true; Pillar.useGravity = false; Pillar.interpolation = RigidbodyInterpolation.Interpolate;
            Pillar.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rest = transform.InverseTransformPoint(Pillar.position);
            hitCenter = transform.InverseTransformPoint(HitArea.transform.TransformPoint(HitArea.center));
            hitSize = Vector3.Scale(HitArea.size, HitArea.transform.localScale);
            previousTime = Clock; ready = true; SetWarning(false);
        }
        public bool Trigger()
        {
            if (context != null && !context.Authority) return false;
            if (!ready || !isActiveAndEnabled || running || (!Repeat && ActivationCount > 0) || (context != null && !context.Running)) return false;
            running = true; started = Clock; ActivationCount++; struck.Clear(); State = SpringPillarState.Warning;
            SetWarning(true); OnWarning.Invoke(); return true;
        }
        void FixedUpdate()
        {
            if (!ready) return;
            float time = Clock;
            bool reset = time < previousTime || (false);
            previousTime = time;
            if (reset)
            {
                running = false; ActivationCount = 0; State = SpringPillarState.Ready; Extension = 0;
                previous.Clear(); struck.Clear(); SetWarning(false); Pillar.MovePosition(transform.TransformPoint(rest));
            }
            if (context != null && !context.Running) { SetWarning(false); return; }
            present.Clear(); bool entered = false;
            int count = Overlap(TriggerArea.transform.TransformPoint(TriggerArea.center), TriggerArea.size, TriggerArea.transform);
            for (int i = 0; i < count; i++)
            {
                var motor = ImportedObstacleActor.Find(buffer[i]);
                if (motor == null || !motor.isActiveAndEnabled || !motor.CanReceiveObstacle) continue;
                present.Add(motor); if (!previous.Contains(motor)) entered = true;
            }
            if (!running && AutoTrigger && present.Count > 0 && (!RequireReentry || entered)) Trigger();
            previous.Clear(); foreach (var motor in present) previous.Add(motor);
            if (!running) return;
            float t = Mathf.Max(0, time - started), next = 0;
            SpringPillarState state;
            if (t < Mathf.Max(0, WarningDuration)) state = SpringPillarState.Warning;
            else if ((t -= Mathf.Max(0, WarningDuration)) < Mathf.Max(.1f, ExtendDuration))
            { state = SpringPillarState.Extend; next = Mathf.Max(0, ExtendDistance) * Smooth(t / Mathf.Max(.1f, ExtendDuration)); }
            else if ((t -= Mathf.Max(.1f, ExtendDuration)) < Mathf.Max(0, HoldDuration))
            { state = SpringPillarState.Hold; next = Mathf.Max(0, ExtendDistance); }
            else if ((t -= Mathf.Max(0, HoldDuration)) < Mathf.Max(.1f, RetractDuration))
            { state = SpringPillarState.Retract; next = Mathf.Max(0, ExtendDistance) * (1 - Smooth(t / Mathf.Max(.1f, RetractDuration))); }
            else if ((t -= Mathf.Max(.1f, RetractDuration)) < Mathf.Max(.1f, Cooldown)) state = SpringPillarState.Cooldown;
            else { state = SpringPillarState.Ready; running = false; }
            if (state != State) { State = state; if (state == SpringPillarState.Extend) OnExtend.Invoke(); }
            if (State == SpringPillarState.Extend && next > Extension && (context == null || context.Authority)) Hit(Extension, next);
            Extension = next; Pillar.MovePosition(transform.TransformPoint(rest + Vector3.forward * Extension)); SetWarning(State == SpringPillarState.Warning);
        }
        void Hit(float before, float after)
        {
            var center = transform.TransformPoint(hitCenter + Vector3.forward * ((before + after) * .5f));
            int count = Overlap(center, hitSize + Vector3.forward * (after - before), transform);
            for (int i = 0; i < count; i++)
            {
                var motor = ImportedObstacleActor.Find(buffer[i]);
                if (motor == null || !motor.CanReceiveObstacle || !struck.Add(motor)) continue;
                var direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                motor.AddObstacleImpulse(direction * Mathf.Max(0, KnockbackForce) + Vector3.up * Mathf.Max(0, UpwardForce));
            }
        }
        int Overlap(Vector3 center, Vector3 size, Transform frame)
        {
            var s = frame.lossyScale;
            return Physics.OverlapBoxNonAlloc(center, Vector3.Scale(size * .5f, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z))),
                buffer, frame.rotation, ~0, QueryTriggerInteraction.Ignore);
        }
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        void SetWarning(bool on) { if (WarningVisual != null) WarningVisual.SetActive(on); }
        void OnDisable() { SetWarning(false); }
    }
}
