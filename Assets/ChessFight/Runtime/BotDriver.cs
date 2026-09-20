using UnityEngine;
namespace ChessFight.ProtectKing
{
    // Waypoint steering uses the same CharacterController, gravity and checkpoint rules as humans.
    // It grants no piece abilities and never teleports forward to bypass an obstacle.
    public sealed class BotDriver : MonoBehaviour
    {
        public PlayerMotor motor;
        public Transform[] route;
        int waypoint;
        int previousFalls=-1;
        Vector3 lastPosition;
        float stuckTime;
        public bool Interact { get; private set; }
        public void ResetRoute() { waypoint=0;previousFalls=-1;stuckTime=0;Interact=false; }
        public void Drive(float dt) {
            var p=motor.Identity;
            if(route==null||route.Length==0) { motor.SetInput(Vector2.zero,false);return; }
            var position=transform.position;
            if(previousFalls!=p.falls) {
                previousFalls=p.falls;waypoint=0;
                while(waypoint<route.Length-1 && route[waypoint].position.z<position.z-.5f)waypoint++;
                stuckTime=0;
            }
            while(waypoint<route.Length-1 && Vector2.Distance(new Vector2(position.x,position.z),
                new Vector2(route[waypoint].position.x,route[waypoint].position.z))<1.3f)waypoint++;
            var destination=route[waypoint].position;
            if(waypoint==route.Length-1 && p.piece!=PieceType.King) {
                float side=p.team==TeamId.Blue?-1:1;
                destination=motor.map.match.throne.transform.position+new Vector3(side*(5+(p.playerId%3)*1.7f),0,-2+(p.playerId%6/3)*2.4f);
            }
            var delta=destination-position;delta.y=0;
            Interact=motor.map.match.throne.IsEligible(p);
            if(Interact) { motor.SetInput(Vector2.zero,false);return; }
            if(delta.sqrMagnitude<1f && waypoint==route.Length-1) { motor.SetInput(Vector2.zero,false);return; }
            var forward=delta.normalized;
            bool blocked=Physics.SphereCast(position+Vector3.up*.9f,.45f,forward,out var hit,1.6f,1,QueryTriggerInteraction.Ignore);
            bool low=Physics.Raycast(position+Vector3.up*.4f,forward,1.5f,1,QueryTriggerInteraction.Ignore);
            bool head=Physics.Raycast(position+Vector3.up*2.3f,forward,1.5f,1,QueryTriggerInteraction.Ignore);
            bool groundAhead=Physics.Raycast(position+forward*1.05f+Vector3.up,Vector3.down,2.6f,1,QueryTriggerInteraction.Ignore);
            bool jump=motor.Grounded&&((low&&!head)||!groundAhead);
            if(blocked&&head) {
                // Gates are waited out; for columns choose a clear side that stays near the route.
                if(hit.collider.GetComponentInParent<ObstacleMotion>()==null) {
                    var left=Quaternion.Euler(0,-65,0)*forward;
                    var right=Quaternion.Euler(0,65,0)*forward;
                    bool l=Clear(position,left),r=Clear(position,right);
                    if(l||r)forward=l&&r?((p.playerId%2==0)?left:right):(l?left:right);
                    else forward=Vector3.zero;
                } else forward=Vector3.zero;
            }
            if((position-lastPosition).sqrMagnitude<.0004f)stuckTime+=dt;else stuckTime=0;
            lastPosition=position;
            if(stuckTime>7) { motor.map.Respawn(motor);ResetRoute();return; }
            motor.ViewYaw=0;
            motor.SetInput(new Vector2(forward.x,forward.z),jump);
        }
        static bool Clear(Vector3 p,Vector3 d)=>!Physics.SphereCast(p+Vector3.up,.5f,d,out _,2f,1,QueryTriggerInteraction.Ignore)
            &&Physics.Raycast(p+d*1.6f+Vector3.up,Vector3.down,3,1,QueryTriggerInteraction.Ignore);
    }
}