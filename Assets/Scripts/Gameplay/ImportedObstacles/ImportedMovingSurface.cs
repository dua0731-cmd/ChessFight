using System;
using ChessFight.Gameplay;
using UnityEngine;
namespace ChessFight.ProtectKing
{
    // Supplies analytical obstacle motion or delegates to a state-driven physical surface.
    public sealed class ImportedMovingSurface : MonoBehaviour, IMovingSurface
    {
        Func<double,Pose> evaluate;
        Func<bool> moving;
        Func<Vector3, Vector3> pointVelocity;
        Func<Quaternion> rotationDelta;
        public static void Bind(Rigidbody body,Func<double,Pose> sample,Func<bool> active)
        {
            var surface=body.GetComponent<ImportedMovingSurface>();
            if(surface==null) surface=body.gameObject.AddComponent<ImportedMovingSurface>();
            surface.pointVelocity=null; surface.rotationDelta=null;
            surface.evaluate=sample;
            surface.moving=active;
        }
        public static void BindDynamic(Rigidbody body, Func<Vector3, Vector3> velocity, Func<Quaternion> rotation, Func<bool> active)
        {
            var surface = body.GetComponent<ImportedMovingSurface>();
            if (surface == null) surface = body.gameObject.AddComponent<ImportedMovingSurface>();
            surface.evaluate = null;
            surface.pointVelocity = velocity; surface.rotationDelta = rotation; surface.moving = active;
        }
        public Quaternion DeltaRotation
        {
            get
            {
                if(moving!=null && !moving())return Quaternion.identity;
                if(rotationDelta!=null)return rotationDelta();
                if(evaluate==null)return Quaternion.identity;
                double t=ObstacleClock.Now;
                return evaluate(t).rotation * Quaternion.Inverse(evaluate(t-Time.fixedDeltaTime).rotation);
            }
        }
        public Vector3 PointVelocity(Vector3 point)
        {
            if(moving!=null && !moving())return Vector3.zero;
            if(pointVelocity!=null)return pointVelocity(point);
            if(evaluate==null)return Vector3.zero;
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
