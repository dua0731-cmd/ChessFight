using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.ProtectKing
{
    // StateDrivenMover: tilted by the weight on its deck, not by ObstacleClock. Offline only
    // until the host sends its angle (Docs/KingRush/MAP_IMPORT.md).
    [RequireComponent(typeof(Rigidbody))]
    public sealed class WeightedBridge : MonoBehaviour
    {
        public ObstacleContext context;
        public Vector3 deckSize = new Vector3(6, .4f, 12);
        [Range(0, 40)] public float maximumTilt = 24;
        [Min(0)] public float degreesPerPlayer = 12;
        [Min(1)] public float tiltSpeed = 30;
        [Min(0)] public float slideSpeed = 3;
        public float Angle { get; private set; }
        public uint NetworkId { get; private set; }
        public int RiderCount { get; private set; }
        public Vector3 SlideVelocity => Vector3.ProjectOnPlane(Vector3.down, transform.up) * slideSpeed;
        static readonly List<WeightedBridge> instances = new List<WeightedBridge>();
        readonly Collider[] overlaps = new Collider[512];
        readonly HashSet<ImportedObstacleActor> riders = new HashSet<ImportedObstacleActor>();
        Rigidbody body;
        Quaternion restRotation;
        float remoteAngle, previousTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() { instances.Clear(); }
        void Awake()
        {
            if (context == null) context = GetComponentInParent<ObstacleContext>();
            body = GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            restRotation = transform.localRotation;
            // Identical saved hierarchy on host and clients yields an identical ID for each instance.
            uint hash = 2166136261;
            for (var t = transform; t != null; t = t.parent)
                foreach (char c in t.name + ":" + t.GetSiblingIndex()) hash = (hash ^ c) * 16777619;
            NetworkId = hash;
        }
        void OnEnable() { if (!instances.Contains(this)) instances.Add(this); }
        void OnDisable() { instances.Remove(this); riders.Clear(); }
        public float CalculateTarget()
        {
            riders.Clear();
            var scale = transform.lossyScale;
            var half = Vector3.Scale(new Vector3(deckSize.x * .5f, 1.15f, deckSize.z * .5f), new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            int count = Physics.OverlapBoxNonAlloc(transform.TransformPoint(new Vector3(0, deckSize.y * .5f + 1, 0)), half,
                overlaps, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            float weight = 0;
            for (int i = 0; i < count; i++)
            {
                var motor = ImportedObstacleActor.Find(overlaps[i]);
                if (motor == null || !motor.CanReceiveObstacle || !motor.Grounded || !riders.Add(motor)) continue;
                var feet = transform.InverseTransformPoint(motor.Position);
                if (feet.y < deckSize.y * .5f - .2f || feet.y > deckSize.y * .5f + .6f ||
                    Mathf.Abs(feet.x) > deckSize.x * .5f || Mathf.Abs(feet.z) > deckSize.z * .5f)
                { riders.Remove(motor); continue; }
                weight += Mathf.Clamp(feet.x / Mathf.Max(.1f, deckSize.x * .5f), -1, 1);
            }
            RiderCount = riders.Count;
            return Mathf.Clamp(-weight * degreesPerPlayer, -maximumTilt, maximumTilt);
        }
        void FixedUpdate()
        {
            float time = context != null ? context.Elapsed : Time.time;
            if (time < previousTime || (context != null && !context.Running)) { Angle = 0; remoteAngle = 0; }
            else Angle = Mathf.MoveTowards(Angle, context == null || context.Authority ? CalculateTarget() : remoteAngle, tiltSpeed * Time.fixedDeltaTime);
            previousTime = time;
            var localRotation = restRotation * Quaternion.AngleAxis(Angle, Vector3.forward);
            body.MoveRotation(transform.parent != null ? transform.parent.rotation * localRotation : localRotation);
        }
        public static void Capture(Scene scene, out uint[] ids, out float[] angles)
        {
            int count = 0;
            foreach (var bridge in instances) if (bridge != null && bridge.gameObject.scene == scene) count++;
            // A separate bridge-state message would be needed for levels exceeding this budget.
            if (count > 64) throw new System.InvalidOperationException("At most 64 weighted bridges per online scene are supported.");
            ids = new uint[count]; angles = new float[count]; int i = 0;
            foreach (var bridge in instances)
                if (bridge != null && bridge.gameObject.scene == scene) { ids[i] = bridge.NetworkId; angles[i++] = bridge.Angle; }
        }
        public static void ApplyRemote(Scene scene, uint[] ids, float[] angles)
        {
            foreach (var bridge in instances)
            {
                if (bridge == null || bridge.gameObject.scene != scene || bridge.context == null || bridge.context.Authority) continue;
                for (int i = 0; i < ids.Length; i++) if (ids[i] == bridge.NetworkId)
                { bridge.remoteAngle = Mathf.Clamp(angles[i], -bridge.maximumTilt, bridge.maximumTilt); break; }
            }
        }
    }
}
