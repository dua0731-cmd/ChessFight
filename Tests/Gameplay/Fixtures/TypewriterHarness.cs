using System;
using System.Collections.Generic;
using System.Reflection;
using ChessFight.ProtectKing;
using UnityEngine;
namespace UnityEngine {
 public class SerializeField:Attribute {} public class MinAttribute:Attribute {public MinAttribute(float f){}}
 public class MonoBehaviour {public bool enabled=true;public Transform transform=new Transform();public T GetComponentInParent<T>() where T:class=>null;public T GetComponent<T>() where T:new()=>new T();}
 public class GameObject {public bool activeInHierarchy=true;public Transform transform=new Transform();public GameObject(){}public GameObject(string n){}public T AddComponent<T>() where T:new()=>new T();public T GetComponent<T>() where T:new()=>new T();}
 public class Transform {public Vector3 position,localScale,lossyScale=Vector3.one;public object rotation;public void SetParent(Transform t,bool b){}public Vector3 TransformPoint(Vector3 v)=>position+v;public Vector3 InverseTransformPoint(Vector3 v)=>v-position;public Vector3 TransformDirection(Vector3 v)=>v;}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 one=>new Vector3(1,1,1);public static Vector3 up=>new Vector3(0,1,0);public static Vector3 back=>new Vector3(0,0,-1);public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);public static Vector3 operator*(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);public static Vector3 Scale(Vector3 a,Vector3 b)=>new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);}
 public struct Color {public static Color black=>default(Color);public static Color red=>default(Color);}
 public class Font {public object material;}
 public enum TextAnchor {MiddleCenter} public enum TextAlignment {Center}
 public class TextMesh {public string text;public TextAnchor anchor;public TextAlignment alignment;public Font font;public int fontSize;public float characterSize;public Color color;}
 public struct Bounds {public Vector3 size;}
 public class Renderer {public void SetPropertyBlock(MaterialPropertyBlock m){}}
 public class MeshRenderer:Renderer {public object sharedMaterial;public Bounds localBounds;}
 public class MaterialPropertyBlock {public void Clear(){}}
 public class Collider {public ImportedObstacleActor actor;public Transform transform=new Transform();public GameObject gameObject=new GameObject();}
 public class BoxCollider:Collider {public Vector3 center,size;}
 public static class Time {public static float time;}
 public static class Mathf {public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static float Abs(float a)=>Math.Abs(a);}
 public enum QueryTriggerInteraction {Ignore}
 public static class Physics {public static Collider[] found=new Collider[0];public static int OverlapBoxNonAlloc(Vector3 p,Vector3 s,Collider[] target,object r,int mask,QueryTriggerInteraction q){Array.Copy(found,target,found.Length);return found.Length;}}
 public static class Debug {public static void LogError(string m,object o){throw new Exception(m);}}
}
namespace ChessFight.Game {public static class RuntimePanels {public static Font KoreanFont;}public static class LitMaterials {public static void SetColor(MaterialPropertyBlock m,Color c){}}}
namespace ChessFight.Gameplay {public class Obstacle:MonoBehaviour{}}
namespace ChessFight.ProtectKing {public class ObstacleContext {public bool Running=true,Authority=true;}public class ImportedObstacleActor {public bool CanReceiveObstacle=true,Grounded=true;public Vector3 Position,Impulse;public int hits;public static ImportedObstacleActor Find(Collider c)=>c.actor;public void AddObstacleHit(Vector3 v,float stun){hits++;Impulse=v;}}}
class Harness {
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static void Set(object o,string field,object v)=>o.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,v);
 static void Tick(TypewriterStairs s,float time){Time.time=time;typeof(TypewriterStairs).GetMethod("FixedUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,null);}
 static void Main(){
  var stairs=new TypewriterStairs();foreach(char c in "CHECKcheck")Check(stairs.IsSafe(c),"Printed letter must be safe");foreach(char c in "BNXP !")Check(!stairs.IsSafe(c),"Unprinted key must be unsafe");
  Set(stairs,"printedWord","ROOK");Check(stairs.IsSafe('r')&&!stairs.IsSafe('C'),"Rules follow printed document");Set(stairs,"printedWord","CHECK");
  var safe=new BoxCollider {center=new Vector3(0,.15f,0),size=new Vector3(2,.7f,2)};
  var wrong=new BoxCollider {center=new Vector3(3,.15f,0),size=new Vector3(2,.7f,2)};
  Set(stairs,"keyAreas",new[]{safe,wrong});Set(stairs,"keyLetters","CX");Set(stairs,"flashUntil",new float[2]);
  var context=new ObstacleContext();Set(stairs,"context",context);
  var actor=new ImportedObstacleActor {Position=new Vector3(0,.25f,0)};
  Physics.found=new[]{new Collider {actor=actor},new Collider {actor=actor}};
  Tick(stairs,0);Check(actor.hits==0,"Safe key and hand overlapping wrong key must not launch");
  actor.Position=new Vector3(3,.25f,0);actor.Grounded=false;Tick(stairs,.1f);Check(actor.hits==0,"Airborne crossing must not launch");
  actor.Grounded=true;actor.Position=new Vector3(3,-1,0);Tick(stairs,.2f);Check(actor.hits==0,"Below-key contact must not launch");
  actor.Position=new Vector3(3,.25f,0);Tick(stairs,.3f);Check(actor.hits==1&&actor.Impulse.z==-12&&actor.Impulse.y==6,"Wrong grounded key launches backwards/up once per pawn");
  Tick(stairs,.4f);Check(actor.hits==1,"Cooldown stops repeated ragdoll impulses");
  context.Authority=false;Tick(stairs,2);Check(actor.hits==1,"Remote peer cannot apply hits");
  context.Authority=true;context.Running=false;Tick(stairs,3);Check(actor.hits==1,"Disabled context cannot apply hits");
  context.Running=true;Tick(stairs,4);Check(actor.hits==2,"Landing again after cooldown can launch");
  Console.WriteLine("PASS: document letters, safe/unsafe footing, airborne/limb filtering, per-pawn cooldown and authority");
 }
}
