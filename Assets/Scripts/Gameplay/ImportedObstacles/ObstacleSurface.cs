using System.Collections.Generic;
using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.ProtectKing
{
    public enum ObstacleSurfaceKind { SpinningDisc, JumpPad, Conveyor }

    public sealed class ObstacleSurface : MonoBehaviour, ISurfaceTransport
    {
        public ObstacleSurfaceKind kind;
        public ObstacleContext context;
        [Header("Spinning Disc")]
        [SerializeField, Tooltip("Degrees per second. Negative reverses rotation; zero stops it.")]
        float rotationSpeed = 45f;
        [Header("Jump Pad")]
        [SerializeField, Min(0), Tooltip("Target rise in metres above takeoff, using current gravity. Collisions can shorten the jump.")]
        float jumpHeight = 10f;
        [SerializeField, Tooltip("Forward launch speed in metres per second, independent of jump height.")]
        float forwardSpeed = 8f;
        [Min(.1f)] public float launchCooldown = .7f;
        [Header("Conveyor")]
        public Vector3 direction = Vector3.forward;
        [Min(0)] public float conveyorSpeed = 3;
        [Header("Visuals")]
        public Transform[] rotors;
        public Transform[] beltMarkers;
        [Min(.1f)] public float beltLength = 6;
        readonly Dictionary<ImportedObstacleActor, float> lastLaunch = new Dictionary<ImportedObstacleActor, float>();
        Vector3[] markerOrigins;
        Quaternion[] rotorOrigins;

        public Vector3 PointVelocity(Vector3 point) => !isActiveAndEnabled || (context!=null && !context.Running) || kind == ObstacleSurfaceKind.JumpPad ? Vector3.zero : VelocityAt(point);
        bool Running => isActiveAndEnabled && (context == null || context.Running);
        public bool TransportActive => Running && kind == ObstacleSurfaceKind.SpinningDisc;
        public Quaternion DeltaRotation => Running && kind == ObstacleSurfaceKind.SpinningDisc
            ? Quaternion.AngleAxis(rotationSpeed * Time.fixedDeltaTime, transform.up) : Quaternion.identity;

        public Vector3 PointDisplacement(Vector3 point)
        {
            if (!Running) return Vector3.zero;
            if (kind != ObstacleSurfaceKind.SpinningDisc) return PointVelocity(point) * Time.fixedDeltaTime;
            Vector3 offset = point - transform.position;
            return DeltaRotation * offset - offset;
        }
        readonly Collider[] occupants = new Collider[512];
        readonly HashSet<ImportedObstacleActor> visited = new HashSet<ImportedObstacleActor>();
        BoxCollider pad;
        void FixedUpdate()
        {
            if(kind!=ObstacleSurfaceKind.JumpPad || (context!=null && !context.Authority))return;
            if(pad==null) pad=GetComponentInChildren<BoxCollider>();
            if(pad==null)return;
            var box=pad.bounds;
            int n=Physics.OverlapBoxNonAlloc(box.center+Vector3.up*(box.extents.y+.25f),new Vector3(box.extents.x,.35f,box.extents.z),occupants,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            visited.Clear();
            for(int i=0;i<n;i++)
            {
                var actor=ImportedObstacleActor.Find(occupants[i]);
                if(actor!=null && actor.CanReceiveObstacle && actor.Position.y>=box.max.y-.2f && actor.Position.y<=box.max.y+.4f && visited.Add(actor)) Apply(actor);
            }
        }
        void Awake()
        {
            if (context == null) context = GetComponentInParent<ObstacleContext>();
            markerOrigins = new Vector3[beltMarkers == null ? 0 : beltMarkers.Length];
            for (int i = 0; i < markerOrigins.Length; i++) markerOrigins[i] = beltMarkers[i].localPosition;
            rotorOrigins = new Quaternion[rotors == null ? 0 : rotors.Length];
            for (int i = 0; i < rotorOrigins.Length; i++) rotorOrigins[i] = rotors[i].localRotation;
        }

        public Vector3 VelocityAt(Vector3 worldPosition)
        {
            if (kind == ObstacleSurfaceKind.Conveyor) return transform.TransformDirection(direction.normalized) * conveyorSpeed;
            if (kind != ObstacleSurfaceKind.SpinningDisc) return Vector3.zero;
            // Tangential takeoff velocity, with no artificial outward push. World-space
            // radius also keeps scaled platforms consistent with their visual rotation.
            return Vector3.Cross(transform.up * (rotationSpeed * Mathf.Deg2Rad), worldPosition - transform.position);
        }

        public Vector3 Apply(ImportedObstacleActor motor)
        {
            if (!isActiveAndEnabled || !motor.CanReceiveObstacle || (context != null && (!context.Running || !context.Authority))) return Vector3.zero;
            if (kind != ObstacleSurfaceKind.JumpPad) return VelocityAt(motor.Position);
            float time = context != null ? context.Elapsed : Time.time;
            if (lastLaunch.TryGetValue(motor, out float previous) && time >= previous && time - previous < launchCooldown) return Vector3.zero;
            lastLaunch[motor] = time;
            float rise = Mathf.Sqrt(2f * Mathf.Max(0f, -Physics.gravity.y) * Mathf.Max(0f, jumpHeight));
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            motor.LaunchFromObstacle(forward * forwardSpeed + Vector3.up * rise);
            return Vector3.zero;
        }

        void Update()
        {
            if (!Running) return;
            double clock = ObstacleClock.Now;
            float t = (float)clock;
            for (int i = 0; i < rotorOrigins.Length; i++)
            {
                rotors[i].localRotation = rotorOrigins[i] * Quaternion.Euler(0, (float)(clock * rotationSpeed % 360.0), 0);
            }
            var axis = direction.normalized;
            for (int i = 0; i < markerOrigins.Length; i++)
            {
                float initial = Vector3.Dot(markerOrigins[i], axis);
                float offset = Mathf.Repeat(initial + t * conveyorSpeed + beltLength * .5f, beltLength) - beltLength * .5f;
                beltMarkers[i].localPosition = markerOrigins[i] + axis * (offset - initial);
            }
        }
        void OnDisable() { lastLaunch.Clear(); }
    }
}
