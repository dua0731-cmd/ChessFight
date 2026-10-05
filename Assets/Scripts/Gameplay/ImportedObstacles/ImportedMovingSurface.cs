using System;
using ChessFight.Gameplay;
using UnityEngine;
namespace ChessFight.ProtectKing
{
    // Supplies the same analytical motion to the existing ragdoll rider before
    // its physics step, regardless of MonoBehaviour execution order.
    public sealed class ImportedMovingSurface : MonoBehaviour, IMovingSurface
    {
        Func<double,Pose> evaluate;
        Func<bool> moving;
        public static void Bind(Rigidbody body,Func<double,Pose> sample,Func<bool> active)
        {
            var surface=body.GetComponent<ImportedMovingSurface>();
            if(surface==null) surface=body.gameObject.AddComponent<ImportedMovingSurface>();
            surface.evaluate=sample;
            surface.moving=active;
        }
        public Quaternion DeltaRotation
        {
            get
            {
                if(evaluate==null || (moving!=null && !moving()))return Quaternion.identity;
                double t=ObstacleClock.Now;
                return evaluate(t).rotation * Quaternion.Inverse(evaluate(t-Time.fixedDeltaTime).rotation);
            }
        }
        public Vector3 PointVelocity(Vector3 point)
        {
            if(evaluate==null || (moving!=null && !moving()))return Vector3.zero;
            double t=ObstacleClock.Now; float dt=Time.fixedDeltaTime;
            Pose a=evaluate(t-dt),b=evaluate(t);
            Quaternion turn=b.rotation*Quaternion.Inverse(a.rotation);
            turn.ToAngleAxis(out float degrees,out Vector3 axis);
            if(degrees>180)degrees-=360;
            Vector3 angular= !float.IsNaN(axis.x) && Mathf.Abs(degrees)>.0001f ? axis*(degrees*Mathf.Deg2Rad/dt) : Vector3.zero;
            return (b.position-a.position)/dt + Vector3.Cross(angular,point-a.position);
        }
    }
}
