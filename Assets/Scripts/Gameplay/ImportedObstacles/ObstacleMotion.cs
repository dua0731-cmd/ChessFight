using System;
using ChessFight.Gameplay;
using UnityEngine;
namespace ChessFight.ProtectKing
{
    public enum MotionKind { Translate, Rotate, Gate }
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ObstacleMotion : MonoBehaviour
    {
        public MotionKind kind;
        public Vector3 axis=Vector3.right;
        public float distance=4,period=5,phase,degreesPerSecond=60;
        public bool shove=true,useLocalTranslationAxes;
        Rigidbody body; ImportedMovingSurface surface;
        Vector3 origin; Quaternion rotation;
        void Awake()
        {
            body=GetComponent<Rigidbody>(); body.isKinematic=true; body.useGravity=false;
            body.interpolation=RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            origin=transform.position; rotation=transform.rotation;
            ImportedMovingSurface.Bind(body,Evaluate,()=>isActiveAndEnabled);
            surface=GetComponent<ImportedMovingSurface>();
            var first=Evaluate(ObstacleClock.Now);body.position=first.position;body.rotation=first.rotation;
        }
        Pose Evaluate(double time)
        {
            var unit=axis.sqrMagnitude>.001f?axis.normalized:Vector3.right;
            if(kind!=MotionKind.Rotate && useLocalTranslationAxes)unit=rotation*unit;
            if(kind==MotionKind.Rotate)return new Pose(origin,rotation*Quaternion.AngleAxis((float)((time*degreesPerSecond+phase*360)%360),unit));
            float wave=(float)Math.Sin((time/Math.Max(.5f,period)+phase)%1 * Math.PI*2);
            float fraction=kind==MotionKind.Gate?Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.35f,.35f,wave)):wave;
            return new Pose(origin+unit*(distance*fraction),rotation);
        }
        void FixedUpdate(){var pose=Evaluate(ObstacleClock.Now);body.MovePosition(pose.position);body.MoveRotation(pose.rotation);}
        public Vector3 PointVelocity(Vector3 point)=>surface!=null?surface.PointVelocity(point):Vector3.zero;
    }
}
