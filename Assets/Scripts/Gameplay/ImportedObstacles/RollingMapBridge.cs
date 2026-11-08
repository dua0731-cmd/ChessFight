using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;
namespace ChessFight.ProtectKing
{
    // StateDrivenMover: offline parchment bridge; a real hand joint slows its local cycle.
    public sealed class RollingMapBridge : Obstacle
    {
        [SerializeField] GameObject[] segments;
        [SerializeField] Transform roller;
        [SerializeField] Transform hangingParchment;
        [SerializeField, Min(1)] float mapWidth = 12;
        [SerializeField, Min(.1f)] float deploymentHeight = 6;
        [SerializeField, Min(0), Tooltip("Small sideways travel during the downward stage, in metres.")]
        float descentDrift = .6f;
        [SerializeField] bool unfoldFromRight;
        [SerializeField, Min(.1f), Tooltip("Time to descend before horizontal unfolding begins.")]
        float lowerSeconds = 1.5f;
        [SerializeField] Rigidbody handle;
        [SerializeField, Min(1)] float length = 16;
        [SerializeField, Min(1)] float openSeconds = 5;
        [SerializeField, Min(1)] float rollSeconds = 5;
        [SerializeField, Min(1)] float closedSeconds = 2;
        [SerializeField, Min(1)] float unfurlSeconds = 3;
        [SerializeField, Range(.1f,1)] float heldSpeed = .35f;
        [SerializeField] float phaseSeconds;
        readonly Collider[] nearby = new Collider[64];
        readonly List<Joint> joints = new List<Joint>();
        double last, clock;
        void OnEnable() { last = SharedTime; clock = phaseSeconds; ApplyCycle(); }
        // Progress is continuous at the corner: the roll reaches floor height before crossing the pit.
        public Vector3 DeploymentPosition(float progress, out float horizontal)
        {
            float split = Mathf.Max(.1f, lowerSeconds) / (Mathf.Max(.1f, lowerSeconds) + Mathf.Max(1, unfurlSeconds));
            float down = Mathf.Clamp01(progress / split);
            horizontal = Mathf.Clamp01((progress - split) / (1 - split));
            float width = Mathf.Max(1, mapWidth), drift = Mathf.Clamp(descentDrift, 0, width * .1f);
            float x = -width * .5f + drift * down + (width - drift) * horizontal;
            return new Vector3(unfoldFromRight ? -x : x, Mathf.Max(.1f, deploymentHeight) * (1 - down), length * .5f);
        }
        void ApplyCycle()
        {
            float a = Mathf.Max(1, openSeconds), b = Mathf.Max(1, rollSeconds), c = Mathf.Max(1, closedSeconds);
            float d = Mathf.Max(.1f, lowerSeconds) + Mathf.Max(1, unfurlSeconds);
            float t = (float)((clock % (a+b+c+d) + (a+b+c+d)) % (a+b+c+d));
            float deployment = t < a ? 1 : t < a+b ? 1-(t-a)/b : t < a+b+c ? 0 : (t-a-b-c)/d;
            Vector3 edge = DeploymentPosition(deployment, out float horizontal);
            if (segments != null) for (int i = 0; i < segments.Length; i++) if (segments[i] != null)
                segments[i].SetActive(horizontal >= (i + 1f) / segments.Length);
            if (roller != null)
            {
                roller.localPosition = edge + Vector3.up * .6f;
                roller.localRotation = Quaternion.Euler(90, 0, 0);
            }
            if (hangingParchment != null)
            {
                Vector3 anchor = new Vector3((unfoldFromRight ? 1 : -1) * Mathf.Max(1, mapWidth) * .5f,
                    Mathf.Max(.1f, deploymentHeight), length * .5f);
                Vector3 knee = DeploymentPosition(Mathf.Min(deployment,
                    Mathf.Max(.1f, lowerSeconds) / d), out _);
                Vector3 delta = knee - anchor;
                hangingParchment.gameObject.SetActive(delta.sqrMagnitude > .0001f);
                if (delta.sqrMagnitude > .0001f)
                {
                    hangingParchment.localPosition = (anchor + knee) * .5f;
                    hangingParchment.localRotation = Quaternion.FromToRotation(Vector3.up, delta);
                    hangingParchment.localScale = new Vector3(.12f, delta.magnitude, Mathf.Max(1, length));
                }
            }
        }
        bool Held()
        {
            if (handle == null) return false;
            int n=Physics.OverlapSphereNonAlloc(handle.position,2,nearby,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++) if(nearby[i].attachedRigidbody != null)
            {
                nearby[i].attachedRigidbody.GetComponents(joints);
                foreach(var joint in joints) if(joint != null && joint.connectedBody == handle) return true;
            }
            return false;
        }
        void FixedUpdate()
        {
            double now=SharedTime, dt=System.Math.Max(0,System.Math.Min(.1,now-last)); last=now;
            if(ObstacleClock.Shared) return;
            clock+=dt*(Held()?Mathf.Clamp(heldSpeed,.1f,1):1);
            ApplyCycle();
        }
    }
}
