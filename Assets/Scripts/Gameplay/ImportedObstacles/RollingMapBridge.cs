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
        void OnEnable() { last = SharedTime; clock = phaseSeconds; }
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
            float a=Mathf.Max(1,openSeconds), b=Mathf.Max(1,rollSeconds), c=Mathf.Max(1,closedSeconds), d=Mathf.Max(1,unfurlSeconds);
            float t=(float)((clock%(a+b+c+d)+(a+b+c+d))%(a+b+c+d));
            float rolled=t<a?0:t<a+b?(t-a)/b:t<a+b+c?1:1-(t-a-b-c)/d;
            if(segments!=null) for(int i=0;i<segments.Length;i++) if(segments[i]!=null)
                segments[i].SetActive(rolled<(i+1f)/segments.Length);
            if(roller!=null) { roller.localPosition=new Vector3(0,.6f,rolled*length); roller.localRotation=Quaternion.Euler(0,0,90); }
        }
    }
}
