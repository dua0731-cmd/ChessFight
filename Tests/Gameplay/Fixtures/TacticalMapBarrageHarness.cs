// Executes the production TacticalMapBarrage against tiny engine/actor stand-ins.
// Physics returns a preselected overlap set; these checks do not simulate PhysX.
// Kept outside Assets so Unity never imports the stand-in UnityEngine namespace.
using System;
using System.Reflection;
using ChessFight.ProtectKing;
namespace UnityEngine {
 public class SerializeField:Attribute{} public class RangeAttribute:Attribute{public RangeAttribute(float a,float b){}} public class MinAttribute:Attribute{public MinAttribute(float a){}}
 public class GameObject { public bool activeSelf=true,activeInHierarchy=true; public void SetActive(bool b){activeSelf=b;} }
 public class MonoBehaviour {public bool enabled=true;public Transform transform=new Transform(); public T GetComponentInParent<T>() where T:class{return null;} }
 public class Transform {public Vector3 lossyScale=new Vector3(1,1,1); public object rotation; public Vector3 TransformPoint(Vector3 p){return p;} public Vector3 InverseTransformPoint(Vector3 p){return p;} public Vector3 TransformDirection(Vector3 p){return p;} }
 public class Collider {public ImportedObstacleActor actor; public Transform transform=new Transform();public GameObject gameObject=new GameObject();}
 public class BoxCollider:Collider {public Vector3 center,size;}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 back=>new Vector3(0,0,-1);public static Vector3 up=>new Vector3(0,1,0); public Vector3 normalized{get{float len=(float)Math.Sqrt(x*x+y*y+z*z);return len>0?this*(1/len):this;}} public static Vector3 operator*(Vector3 a,float s){return new Vector3(a.x*s,a.y*s,a.z*s);}public static Vector3 operator+(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}public static Vector3 operator-(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}public static Vector3 Scale(Vector3 a,Vector3 b){return new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);}}
 public static class Mathf {public static int Clamp(int i,int min,int max){return Math.Min(max,Math.Max(min,i));} public static float Max(float a,float b){return Math.Max(a,b);}public static float Abs(float a){return Math.Abs(a);} }
 public enum QueryTriggerInteraction{Ignore}
 public static class Physics {public static Collider[] found;public static int OverlapBoxNonAlloc(Vector3 a,Vector3 b,Collider[] o,object rotation,int mask,QueryTriggerInteraction q){if(found==null)return 0;Array.Copy(found,o,found.Length);return found.Length;}}
 public static class Debug {public static void LogError(string s,object o){}}
}
namespace ChessFight.Gameplay {public abstract class Obstacle:UnityEngine.MonoBehaviour{public static double Time;protected double SharedTime=>Time;}}
namespace ChessFight.ProtectKing {public class ObstacleContext{public bool Running=true,Authority=true;}public class ImportedObstacleActor{public bool CanReceiveObstacle=true;public UnityEngine.Vector3 Position;public int Hits;public static ImportedObstacleActor Find(UnityEngine.Collider c){return c.actor;}public void AddObstacleImpulse(UnityEngine.Vector3 v){Hits++;}}}
class Harness {
 static int checks;
 static void Check(bool b,string message){checks++;if(!b)throw new Exception(message);}
 static void Set(TacticalMapBarrage b,string n,object v){typeof(TacticalMapBarrage).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(b,v);}
 static void Call(TacticalMapBarrage b,string n){typeof(TacticalMapBarrage).GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(b,null);}
 static void Tick(TacticalMapBarrage b,double t){ChessFight.Gameplay.Obstacle.Time=t;Call(b,"Update");Call(b,"FixedUpdate");}
 static int Main(){
 var b=new TacticalMapBarrage();var context=new ObstacleContext();Set(b,"context",context);
 var cells=new UnityEngine.BoxCollider[9];var visuals=new UnityEngine.GameObject[9];var safe=new UnityEngine.GameObject[9];
 for(int i=0;i<9;i++){cells[i]=new UnityEngine.BoxCollider{center=new UnityEngine.Vector3((i%3)*4,1.5f,(i/3)*4),size=new UnityEngine.Vector3(4,3,4)};visuals[i]=new UnityEngine.GameObject();safe[i]=new UnityEngine.GameObject();}
 Set(b,"dangerAreas",cells);Set(b,"warningVisuals",visuals);Set(b,"safeVisuals",safe);Call(b,"Awake");Check(b.enabled,"Valid grid disabled");
 for(int cycle=-50;cycle<50;cycle++)for(int row=0;row<3;row++){var col=b.SafeColumn((cycle+.5)*b.Period,row);Check(col>=0&&col<3,"Safe column out of range");Check(col!=b.SafeColumn((cycle+1.5)*b.Period,row),"Pattern does not advance");}
 Tick(b,1.2);for(int row=0;row<3;row++){int s=0,w=0;for(int col=0;col<3;col++){if(safe[row*3+col].activeSelf)s++;if(visuals[row*3+col].activeSelf)w++;}Check(s==1&&w==2,"Unsafe telegraph count");}
 var actor=new ImportedObstacleActor{Position=new UnityEngine.Vector3(4,0,0)};UnityEngine.Physics.found=new[]{new UnityEngine.Collider{actor=actor},new UnityEngine.Collider{actor=actor}};
 Tick(b,3.3);Tick(b,3.35);Check(actor.Hits==1,"Repeated ragdoll collider/cycle hits");
 Tick(b,3.6);Check(actor.Hits==1,"Rest damage");foreach(var v in visuals)Check(!v.activeSelf,"Rest warning still active");
 Tick(b,6.8);Check(actor.Hits==1,"Safe cell actor hit by extended arm");
 actor.Position=new UnityEngine.Vector3(0,0,0);Tick(b,6.85);Check(actor.Hits==2,"Unsafe cell not hit");
 actor.Position=new UnityEngine.Vector3(0,-1,0);Tick(b,10.3);Check(actor.Hits==3,"Fallen body overlapping floor escaped barrage");
 actor.Position=new UnityEngine.Vector3(4,0,0);context.Authority=false;Tick(b,13.8);Check(actor.Hits==3,"Client dealt damage");context.Authority=true;context.Running=false;Tick(b,13.85);Check(actor.Hits==3,"Stopped context dealt damage");foreach(var v in safe)Check(!v.activeSelf,"Stopped safe marker active");
 Call(b,"OnDisable");foreach(var v in visuals)Check(!v.activeSelf,"Disabled warning active");
 var invalid=new TacticalMapBarrage();Call(invalid,"Awake");Check(!invalid.enabled,"Missing rows accepted");
 Console.WriteLine(checks+" TacticalMapBarrage checks passed (stubbed Unity, no PhysX).");return 0;
 }
}
