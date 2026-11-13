using System;
using System.Collections.Generic;
using System.Reflection;
using ChessFight.ProtectKing;
using UnityEngine;
namespace UnityEngine
{
 public class SerializeField:Attribute {} public class MinAttribute:Attribute { public MinAttribute(float n){} }
 public class RangeAttribute:Attribute { public RangeAttribute(float a,float b){} }
 public class TooltipAttribute:Attribute { public TooltipAttribute(string s){} }
 public class MonoBehaviour { public bool isActiveAndEnabled=true; }
 public class GameObject { public bool activeSelf=true; public Transform transform; public GameObject(){transform=new Transform(this);} public void SetActive(bool b){activeSelf=b;} }
 public class Transform { public GameObject gameObject; public Vector3 localPosition,localScale,lossyScale=new Vector3(1,1,1);public Quaternion localRotation,rotation;public Transform(GameObject g){gameObject=g;}public Vector3 TransformPoint(Vector3 v)=>v; }
 public class Rigidbody { public Vector3 position; public void GetComponents(List<Joint> j){j.Clear();} }
 public class Joint { public Rigidbody connectedBody; }
 public class Collider { public Rigidbody attachedRigidbody;public Transform transform=new GameObject().transform; }
 public class BoxCollider:Collider { public Vector3 center,size; }
 public struct Pose { public Vector3 position;public Quaternion rotation;public Pose(Vector3 p,Quaternion r){position=p;rotation=r;} }
 public struct Quaternion { public static Quaternion identity=>default(Quaternion); public static Quaternion Euler(float a,float b,float c)=>default(Quaternion);public static Quaternion FromToRotation(Vector3 a,Vector3 b)=>default(Quaternion);public static Vector3 operator*(Quaternion q,Vector3 v)=>v; }
 public struct Vector3 {
  public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}
  public static Vector3 up=>new Vector3(0,1,0);public static Vector3 back=>new Vector3(0,0,-1);
  public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
  public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator*(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
  public static Vector3 Scale(Vector3 a,Vector3 b)=>new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);
 }
 public static class Mathf {
  public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);
  public static float Abs(float a)=>Math.Abs(a);public static float Pow(float a,float b)=>(float)Math.Pow(a,b);
  public static float Clamp(float n,float a,float b)=>Math.Min(b,Math.Max(a,n));public static float Clamp01(float n)=>Clamp(n,0,1);
  public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return a+(b-a)*t*t*(3-2*t);}
 }
 public enum QueryTriggerInteraction {Ignore}
 public static class Physics {
  public static int OverlapSphereNonAlloc(Vector3 p,float r,Collider[] c,int m,QueryTriggerInteraction q)=>0;
  public static int OverlapBoxNonAlloc(Vector3 p,Vector3 s,Collider[] c,Quaternion r,int m,QueryTriggerInteraction q)=>0;
 }
}
namespace ChessFight.Gameplay {
 public abstract class Obstacle:MonoBehaviour { public static double Time;protected double SharedTime=>Time; }
 public static class ObstacleClock { public static bool Shared; }
}
namespace ChessFight.ProtectKing {
 public abstract class ObstacleMotion:ChessFight.Gameplay.Obstacle {protected Vector3 Origin;protected Quaternion RestRotation;protected abstract Pose Evaluate(double t);protected void FixedUpdate(){} }
 public class ObstacleContext { public bool Running=true,Authority=true; }
 public class ImportedObstacleActor {public bool CanReceiveObstacle=true;public static ImportedObstacleActor Find(Collider c)=>null;public void AddObstacleImpulse(Vector3 v){} }
}
class WarRoomMotionHarness {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Set(object o,string name,object v){o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,v);}
 static void Call(object o,string name){o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,null);}
 static float Height(CommandStampPress p,double t)=>( (Pose)typeof(CommandStampPress).GetMethod("Evaluate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(p,new object[]{t})).position.y;
 static void Main(){
  var press=new CommandStampPress();float slam=.18f,retract=2.5f;
  Set(press,"restSeconds",2f);Set(press,"warningSeconds",1.4f);Set(press,"slamSeconds",slam);Set(press,"holdSeconds",.6f);Set(press,"retractSeconds",retract);
  Check(Height(press,3.4) > 5.99f,"Raised before strike");
  Check(Height(press,3.4+slam)<.001f,"Fast strike reaches floor");
  Check(Math.Abs(Height(press,3.4+slam+.6+retract/2)-3)<.001f,"Slow rise halfway");
  Check(Height(press,press.Period-.0001)>5.99f,"Rise joins rest without jump");
  Check(6/slam>10*(6/retract),"Strike much faster than rise");
  var map=new RollingMapBridge();float h;var start=map.DeploymentPosition(0,out h);
  for(int i=1;i<100;i++){
   var p=map.DeploymentPosition(i/300f,out h);
   Check(h==0 && p.y>0,"No horizontal floor before descent completes");
   Check(Math.Abs(p.x-start.x)<.61f,"Small sideways drift while descending");
   Check((start.y-p.y)>9*Math.Abs(p.x-start.x),"Steep diagonal descent");
  }
  var corner=map.DeploymentPosition(1f/3,out h);Check(Math.Abs(corner.y)<.001f && h<.001f,"At floor before horizontal stage");
  var before=map.DeploymentPosition(1f/3-.00001f,out h);var after=map.DeploymentPosition(1f/3+.00001f,out h);
  Check((before-after).magnitude<.002f,"Continuous corner");
  for(int i=34;i<=100;i++){var p=map.DeploymentPosition(i/100f,out h);Check(p.y==0 && h>0,"Horizontal travel stays at floor");}
  var end=map.DeploymentPosition(1,out h);Check(Math.Abs(end.x-6)<.001f && h==1,"Full width reached");
  Set(map,"unfoldFromRight",true);var mirrored=map.DeploymentPosition(.2f,out h);Set(map,"unfoldFromRight",false);var original=map.DeploymentPosition(.2f,out h);
  Check(mirrored.x==-original.x && mirrored.y==original.y,"Mirrored sides share height");
  var strips=new GameObject[8];for(int i=0;i<8;i++)strips[i]=new GameObject();Set(map,"segments",strips);
  Set(map,"clock",12.75);Call(map,"ApplyCycle");foreach(var s in strips)Check(!s.activeSelf,"Descending sheet is not walkable");
  Set(map,"clock",0.0);Call(map,"ApplyCycle");foreach(var s in strips)Check(s.activeSelf,"Fully open bridge is walkable");
  Set(map,"clock",11.0);Call(map,"ApplyCycle");foreach(var s in strips)Check(!s.activeSelf,"Closed bridge has no floor");
  foreach(bool right in new[]{false,true}) {
   Set(map,"unfoldFromRight",right);
   for(int step=1;step<=100;step++) {
    double progress=step/100.0;
    Set(map,"clock",13.5+3*progress);Call(map,"ApplyCycle");
    float direction=right?-1:1, next=-5.4f;
    foreach(var strip in strips) if(strip.activeSelf) {
     float half=1.425f*strip.transform.localScale.x*.5f;
     float centre=direction*strip.transform.localPosition.x;
     Check(Math.Abs(centre-half-next)<.0001f,"No gaps in horizontal floor");
     Check(strip.transform.localPosition.y==0 && strip.transform.localScale.y==1,"Floor stays level at target Y");
     next=centre+half;
    }
    Check(Math.Abs(next-(-5.4+11.4*progress))<.0001f,"Walkable floor reaches roller continuously");
    Check(strips[0].activeSelf,"Floor appears immediately when horizontal unfolding starts");
   }
  }
  Console.WriteLine("PASS: slow rise / fast slam; steep descent, continuous corner, horizontal spread, mirrored sides and floor activation");
 }
}
