using System.Collections.Generic;
using UnityEngine;
using ChessFight.Gameplay;
using UnityEngine.Events;

namespace ChessFight.ProtectKing
{
    public enum FloorPistonState { Idle, Warning, Rising, Hold, Falling }

    public sealed class FloorPiston : MonoBehaviour
    {
        float RuntimeTime => SampleTime(ObstacleClock.Now);
        float SampleTime(double t) => ObstacleContext.PeriodicTime(t,StartDelay,CycleDuration,Repeat);
        public ObstacleContext context;
        public Rigidbody Piston;
        public GameObject WarningArea;
        [Tooltip("Query volume above the retracted top. Kept separate from the solid piston collider.")]
        public BoxCollider HitArea;
        [Min(0)] public float IdleDuration = 3;
        [Min(0)] public float WarningDuration = .8f;
        [Min(.1f)] public float RiseDuration = .25f;
        [Min(0)] public float HoldDuration = .6f;
        [Min(.1f)] public float FallDuration = 1;
        [Min(0)] public float RiseHeight = 2.5f;
        [Min(0)] public float LaunchForce = 18;
        [Min(0)] public float HorizontalForce = 4;
        [Min(0)] public float StartDelay = .5f;
        public bool Repeat = true;
        public UnityEvent OnWarning = new UnityEvent();
        public UnityEvent OnRise = new UnityEvent();
        public FloorPistonState State { get; private set; }
        public float Height { get; private set; }
        public int LaunchesThisCycle => launched.Count;
        public float CycleDuration => Mathf.Max(0, IdleDuration) + Mathf.Max(0, WarningDuration) +
            Mathf.Max(.1f, RiseDuration) + Mathf.Max(0, HoldDuration) + Mathf.Max(.1f, FallDuration);
        readonly HashSet<ImportedObstacleActor> launched = new HashSet<ImportedObstacleActor>();
        readonly Collider[] overlaps = new Collider[512];
        Vector3 pistonRest, hitRest, hitCenter, hitSize;
        float previousTime;
        int cycle = -1;
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
            if (t < Mathf.Max(.1f, RiseDuration))
            { sample.state = FloorPistonState.Rising; sample.height = Mathf.Max(0, RiseHeight) * Smooth(t / Mathf.Max(.1f, RiseDuration)); return sample; }
            t -= Mathf.Max(.1f, RiseDuration);
            if (t < Mathf.Max(0, HoldDuration)) { sample.state = FloorPistonState.Hold; sample.height = Mathf.Max(0, RiseHeight); return sample; }
            t -= Mathf.Max(0, HoldDuration);
            sample.state = FloorPistonState.Falling;
            sample.height = Mathf.Max(0, RiseHeight) * (1 - Smooth(t / Mathf.Max(.1f, FallDuration)));
            return sample;
        }
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        void Awake()
        {
            if (context == null) context = GetComponent<ObstacleContext>();
            if (Piston == null || HitArea == null)
            { Debug.LogError("FloorPiston requires Piston and HitArea references.", this); enabled = false; return; }
            Piston.isKinematic = true; Piston.useGravity = false; Piston.interpolation = RigidbodyInterpolation.Interpolate;
            Piston.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            pistonRest = transform.InverseTransformPoint(Piston.position);
            hitRest = HitArea.transform.localPosition;
            hitCenter = transform.InverseTransformPoint(HitArea.transform.TransformPoint(HitArea.center));
            hitSize = Vector3.Scale(HitArea.size, HitArea.transform.localScale);
            previousTime = RuntimeTime;
            var sample = Evaluate(previousTime); Height = sample.height; cycle = sample.cycle;
            Piston.position = transform.TransformPoint(pistonRest + Vector3.up * Height);
            HitArea.transform.localPosition = hitRest + Vector3.up * Height;
            ImportedMovingSurface.Bind(Piston,t=>new Pose(transform.TransformPoint(pistonRest+Vector3.up*Evaluate(SampleTime(t)).height),Piston.rotation),()=>isActiveAndEnabled && (context==null || context.Running));
            ready = true; if (WarningArea != null) WarningArea.SetActive(false);
        }
        void FixedUpdate()
        {
            if (!ready) return;
            float time = RuntimeTime;
            bool reset = time < previousTime; previousTime = time;
            bool parked = false;
            if (context != null && !context.Running && !parked)
            { if (WarningArea != null) WarningArea.SetActive(false); return; }
            var sample = parked ? new Sample { state = FloorPistonState.Idle, cycle = -1 } : Evaluate(time);
            if (reset || sample.cycle != cycle) { launched.Clear(); cycle = sample.cycle; }
            float previousHeight = Height;
            Height = sample.height;
            if (State != sample.state)
            {
                State = sample.state;
                if (State == FloorPistonState.Warning) OnWarning.Invoke();
                if (State == FloorPistonState.Rising) OnRise.Invoke();
            }
            // Sweep the top over this physics step instead of relying on a fast trigger entering a stationary controller.
            if (State == FloorPistonState.Rising && Height > previousHeight && (context == null || context.Authority))
                LaunchAbove(previousHeight, Height);
            Piston.MovePosition(transform.TransformPoint(pistonRest + Vector3.up * Height));
            HitArea.transform.localPosition = hitRest + Vector3.up * Height;
            if (WarningArea != null) WarningArea.SetActive(State == FloorPistonState.Warning);
        }
        void LaunchAbove(float previousHeight, float nextHeight)
        {
            var center = hitCenter + Vector3.up * ((previousHeight + nextHeight) * .5f);
            var size = hitSize + Vector3.up * (nextHeight - previousHeight + .15f);
            var scale = transform.lossyScale;
            var half = Vector3.Scale(size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            int count = Physics.OverlapBoxNonAlloc(transform.TransformPoint(center), half, overlaps, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var motor = ImportedObstacleActor.Find(overlaps[i]);
                if (motor == null || !motor.CanReceiveObstacle || launched.Contains(motor)) continue;
                var feet = transform.InverseTransformPoint(motor.Position);
                float bottom = hitCenter.y - hitSize.y * .5f + previousHeight;
                float top = hitCenter.y + hitSize.y * .5f + nextHeight;
                var radial = new Vector3(feet.x - hitCenter.x, 0, feet.z - hitCenter.z);
                // A side contact with a player's torso must never count as standing on the top.
                if (feet.y < bottom - .12f || feet.y > top || Mathf.Abs(radial.x) > hitSize.x * .5f || Mathf.Abs(radial.z) > hitSize.z * .5f) continue;
                float edge = Mathf.Clamp01(Mathf.Max(Mathf.Abs(radial.x) / Mathf.Max(.01f, hitSize.x * .5f), Mathf.Abs(radial.z) / Mathf.Max(.01f, hitSize.z * .5f)));
                var horizontal = Vector3.ProjectOnPlane(transform.TransformDirection(radial), Vector3.up).normalized;
                launched.Add(motor);
                motor.LaunchFromObstacle(Vector3.up * Mathf.Max(0, LaunchForce) + horizontal * (Mathf.Max(0, HorizontalForce) * edge));
            }
        }
        void OnDisable() { if (WarningArea != null) WarningArea.SetActive(false); }
        void OnDrawGizmosSelected()
        {
            if (HitArea == null) return;
            Gizmos.color = Color.red; Gizmos.matrix = HitArea.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(HitArea.center, HitArea.size); Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
