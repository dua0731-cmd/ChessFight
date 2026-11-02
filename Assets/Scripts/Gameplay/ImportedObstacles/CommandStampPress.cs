using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.ProtectKing
{
    // The visual press has no solid crushing collider. One bounded impulse per actor/cycle
    // avoids trapping ragdoll joints between a kinematic head and the floor.
    public sealed class CommandStampPress : ObstacleMotion
    {
        [SerializeField] ObstacleContext context;
        [SerializeField] BoxCollider impactArea;
        [SerializeField] GameObject warningVisual;
        [SerializeField, Min(1)] float raisedHeight = 6;
        [SerializeField, Min(.5f)] float restSeconds = 2;
        [SerializeField, Min(.5f)] float warningSeconds = 1.4f;
        [SerializeField, Min(.1f)] float slamSeconds = .35f;
        [SerializeField, Min(.1f)] float holdSeconds = .6f;
        [SerializeField, Min(.2f)] float retractSeconds = 1;
        [SerializeField] float phaseOffset;
        [SerializeField, Min(0)] float pushForce = 10;
        [SerializeField, Min(0)] float upwardForce = 2;
        readonly Collider[] overlaps = new Collider[512];
        readonly HashSet<ImportedObstacleActor> struck = new HashSet<ImportedObstacleActor>();
        double lastCycle = double.NaN;
        float Rest => Mathf.Max(.5f, restSeconds);
        float Warn => Mathf.Max(.5f, warningSeconds);
        float Slam => Mathf.Max(.1f, slamSeconds);
        float Hold => Mathf.Max(.1f, holdSeconds);
        float Retract => Mathf.Max(.2f, retractSeconds);
        public float Period => Rest + Warn + Slam + Hold + Retract;
        float Phase(double time) => (float)(((time + phaseOffset) % Period + Period) % Period);
        protected override Pose Evaluate(double time)
        {
            float t = Phase(time), height = Mathf.Max(1, raisedHeight);
            if (t >= Rest + Warn && t < Rest + Warn + Slam)
                height *= 1 - Mathf.Pow((t - Rest - Warn) / Slam, 2);
            else if (t >= Rest + Warn + Slam && t < Rest + Warn + Slam + Hold) height = 0;
            else if (t >= Rest + Warn + Slam + Hold)
                height *= Mathf.SmoothStep(0, 1, (t - Rest - Warn - Slam - Hold) / Retract);
            return new Pose(Origin + RestRotation * Vector3.up * height, RestRotation);
        }
        void Update()
        {
            float t = Phase(SharedTime);
            if (warningVisual != null) warningVisual.SetActive(t >= Rest && t < Rest + Warn + Slam);
        }
        new void FixedUpdate()
        {
            base.FixedUpdate();
            double now = SharedTime;
            double cycle = System.Math.Floor((now + phaseOffset) / Period);
            if (cycle != lastCycle) { struck.Clear(); lastCycle = cycle; }
            float t = Phase(now);
            if (impactArea == null || (context != null && (!context.Running || !context.Authority)) ||
                t < Rest + Warn + Slam || t >= Rest + Warn + Slam + Hold) return;
            var scale = impactArea.transform.lossyScale;
            int count = Physics.OverlapBoxNonAlloc(impactArea.transform.TransformPoint(impactArea.center),
                Vector3.Scale(impactArea.size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))),
                overlaps, impactArea.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor == null || !actor.CanReceiveObstacle || !struck.Add(actor)) continue;
                actor.AddObstacleImpulse(RestRotation * Vector3.back * Mathf.Max(0, pushForce) + Vector3.up * Mathf.Max(0, upwardForce));
            }
        }
        void OnDisable()
        {
            struck.Clear(); lastCycle = double.NaN;
            if (warningVisual != null) warningVisual.SetActive(false);
        }
    }
}
