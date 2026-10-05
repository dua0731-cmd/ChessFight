using System.Collections.Generic;
using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.ProtectKing
{
    public sealed class FallingChessPiece : MonoBehaviour
    {
        float RuntimeTime => SampleTime(ObstacleClock.Now);
        float SampleTime(double t) => ObstacleContext.PeriodicTime(t,0,Period,true);
        public ObstacleContext context;
        public Transform piece;
        public Transform warning;
        public Transform shockRing;
        [Min(1)] public float dropHeight = 9;
        [Min(.1f)] public float warningSeconds = 1.4f;
        [Min(.1f)] public float fallSeconds = .65f;
        [Min(.1f)] public float shockSeconds = .7f;
        [Min(.1f)] public float restSeconds = 1.8f;
        [Min(0)] public float phaseOffset;
        [Min(.1f)] public float shockRadius = 5;
        [Min(0)] public float knockback = 10;
        [Min(0)] public float lift = 4;
        [Min(.1f)] public float heightTolerance = 2;
        public bool coverBlocksShock = true;
        public float Period => warningSeconds + fallSeconds + shockSeconds + restSeconds;
        public float CycleTime => Mathf.Repeat((RuntimeTime) + phaseOffset, Period);
        readonly Collider[] overlaps = new Collider[512];
        readonly HashSet<ImportedObstacleActor> struck = new HashSet<ImportedObstacleActor>();
        int lastCycle = -1;
        float lastTime = -1;
        void Awake() { if (context == null) context = GetComponent<ObstacleContext>(); }
        void Update()
        {
            float t = CycleTime;
            bool falling = t >= warningSeconds && t < warningSeconds + fallSeconds;
            bool impact = t >= warningSeconds + fallSeconds && t < warningSeconds + fallSeconds + shockSeconds;
            float drop = Mathf.Clamp01((t - warningSeconds) / fallSeconds);
            if (piece != null) {
                piece.gameObject.SetActive(t < warningSeconds + fallSeconds + shockSeconds);
                piece.localPosition = Vector3.up * Mathf.Lerp(dropHeight, .15f, drop * drop);
            }
            if (warning != null) {
                warning.gameObject.SetActive(t < warningSeconds || falling);
                float pulse = .88f + .12f * Mathf.Sin(t * 18);
                warning.localScale = new Vector3(shockRadius * 2 * pulse, .04f, shockRadius * 2 * pulse);
            }
            if (shockRing != null) {
                shockRing.gameObject.SetActive(impact);
                float size = Mathf.Max(.03f, Mathf.Clamp01((t - warningSeconds - fallSeconds) / shockSeconds) * shockRadius * 2);
                shockRing.localScale = new Vector3(size, 1, size);
            }
        }
        void FixedUpdate()
        {
            if (context != null && (!context.Running || !context.Authority)) return;
            float time = (RuntimeTime) + phaseOffset;
            int cycle = Mathf.FloorToInt(time / Period);
            if (cycle != lastCycle || time < lastTime) { struck.Clear(); lastCycle = cycle; }
            float t = Mathf.Repeat(time, Period);
            float impactAt = warningSeconds + fallSeconds;
            // Include a just-crossed end boundary so a long frame cannot skip the outer rim.
            bool crossedEnd = lastTime >= 0 && Mathf.FloorToInt(lastTime / Period) == cycle &&
                Mathf.Repeat(lastTime, Period) < impactAt + shockSeconds && t >= impactAt + shockSeconds;
            lastTime = time;
            if (t < impactAt || (t > impactAt + shockSeconds && !crossedEnd)) return;
            float radius = Mathf.Clamp01((t - impactAt) / shockSeconds) * shockRadius;
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius + 1, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var motor = ImportedObstacleActor.Find(overlaps[i]);
                if (motor == null || !motor.CanReceiveObstacle || struck.Contains(motor)) continue;
                var delta = motor.Position - transform.position;
                if (Mathf.Abs(delta.y) > heightTolerance) continue;
                var horizontal = Vector3.ProjectOnPlane(delta, Vector3.up);
                if (horizontal.magnitude > radius) continue;
                if (coverBlocksShock && Physics.Linecast(transform.position + Vector3.up,
                    motor.Position + Vector3.up, 1, QueryTriggerInteraction.Ignore)) continue;
                struck.Add(motor);
                motor.AddObstacleImpulse((horizontal.sqrMagnitude > .01f ? horizontal.normalized : transform.forward) * knockback + Vector3.up * lift);
            }
        }
        void OnDisable() { struck.Clear(); lastCycle = -1; lastTime = -1; }
    }
}
