using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.ProtectKing
{
    public enum ObstacleSurfaceKind { SpinningDisc, JumpPad, Conveyor }

    public sealed class ObstacleSurface : MonoBehaviour
    {
        public ObstacleSurfaceKind kind;
        public ObstacleContext context;
        [Min(.1f)] public float radius = 4;
        [Min(0)] public float centerSpeed = 5;
        [Min(0)] public float edgeSpeed = 1;
        [Min(0)] public float outwardSpeed = 1.5f;
        public Vector3 direction = Vector3.forward;
        [Min(0)] public float conveyorSpeed = 3;
        public Vector3 launchVelocity = new Vector3(0, 14, 8);
        [Min(.1f)] public float launchCooldown = .7f;
        public Transform[] rotors;
        public Transform[] beltMarkers;
        [Min(.1f)] public float beltLength = 6;
        readonly Dictionary<PlayerMotor, float> lastLaunch = new Dictionary<PlayerMotor, float>();
        Vector3[] markerOrigins;
        Quaternion[] rotorOrigins;

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
            var local = transform.InverseTransformPoint(worldPosition); local.y = 0;
            float distance = local.magnitude;
            var radial = distance > .05f ? local / distance : Vector3.right;
            float speed = Mathf.Lerp(centerSpeed, edgeSpeed, Mathf.Clamp01(distance / Mathf.Max(.1f, radius)));
            var tangent = Vector3.Cross(Vector3.up, radial);
            return transform.TransformDirection(tangent * speed + radial * outwardSpeed);
        }

        public Vector3 Apply(PlayerMotor motor)
        {
            if (!isActiveAndEnabled || !motor.CanReceiveObstacle || (context != null && (!context.Running || !context.Authority))) return Vector3.zero;
            if (kind != ObstacleSurfaceKind.JumpPad) return VelocityAt(motor.transform.position);
            float time = context != null ? context.Elapsed : Time.time;
            if (lastLaunch.TryGetValue(motor, out float previous) && time >= previous && time - previous < launchCooldown) return Vector3.zero;
            lastLaunch[motor] = time;
            motor.LaunchFromObstacle(transform.TransformDirection(launchVelocity));
            return Vector3.zero;
        }

        void Update()
        {
            float t = context != null ? context.Elapsed : Time.time;
            for (int i = 0; i < rotorOrigins.Length; i++)
            {
                float fraction = rotorOrigins.Length <= 1 ? 0 : (float)i / (rotorOrigins.Length - 1);
                rotors[i].localRotation = rotorOrigins[i] * Quaternion.Euler(0, t * Mathf.Lerp(150, 30, fraction), 0);
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
