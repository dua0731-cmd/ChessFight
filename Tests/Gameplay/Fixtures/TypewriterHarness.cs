using System;
using System.Collections.Generic;
using System.Reflection;
using ChessFight.ProtectKing;
using ChessFight.Network;
using UnityEngine;
namespace UnityEngine {
 public class SerializeField:Attribute {} public class DefaultExecutionOrder:Attribute { public DefaultExecutionOrder(int n){} } public class RequireComponent:Attribute {public RequireComponent(Type t){}} public class MinAttribute:Attribute {public MinAttribute(float f){}}
 public class MonoBehaviour {public bool enabled=true;public bool isActiveAndEnabled=true;public Transform transform=new Transform();public T GetComponentInParent<T>() where T:class=>null;public T GetComponent<T>() where T:new()=>new T();}
 public class GameObject {public bool activeInHierarchy=true;public Transform transform=new Transform();public GameObject(){}public GameObject(string n){}public T AddComponent<T>() where T:new()=>new T();public T GetComponent<T>() where T:new()=>new T();}
 public class Transform {public Vector3 position,localScale,lossyScale=Vector3.one;public object rotation;public Transform parent;public Vector3 localPosition,up=Vector3.up;public bool IsChildOf(Transform t)=>this==t||parent==t;public void SetParent(Transform t,bool b){}public Vector3 TransformPoint(Vector3 v)=>position+v;public Vector3 InverseTransformPoint(Vector3 v)=>v-position;public Vector3 TransformDirection(Vector3 v)=>v;}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 one=>new Vector3(1,1,1);public static Vector3 up=>new Vector3(0,1,0);public static Vector3 back=>new Vector3(0,0,-1);public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);public static Vector3 operator*(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);public static Vector3 Scale(Vector3 a,Vector3 b)=>new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);}
 public struct Color {public static Color white=>default(Color);public static Color black=>default(Color);public static Color red=>default(Color);}
 public class Font {public object material;}
 public enum TextAnchor {MiddleCenter} public enum TextAlignment {Center}
 public class TextMesh {public string text;public TextAnchor anchor;public TextAlignment alignment;public Font font;public int fontSize;public float characterSize;public Color color;}
 public struct Quaternion {public static Quaternion identity=>default(Quaternion);}
 public struct Bounds {public Vector3 size,center;public Vector3 extents=>size*.5f;public bool Contains(Vector3 p)=>Math.Abs(p.x-center.x)<=size.x*.5f&&Math.Abs(p.y-center.y)<=size.y*.5f&&Math.Abs(p.z-center.z)<=size.z*.5f;}
 public enum RigidbodyInterpolation {Interpolate}
 public class Rigidbody {public Vector3 position;public bool isKinematic,useGravity;public RigidbodyInterpolation interpolation;public void MovePosition(Vector3 p){position=p;}}
 public static class Random {static System.Random r=new System.Random(912);public static int Range(int a,int b)=>r.Next(a,b);}
 public class Renderer {public void SetPropertyBlock(MaterialPropertyBlock m){}}
 public class MeshRenderer:Renderer {public object sharedMaterial;public Bounds localBounds;}
 public class MaterialPropertyBlock {public void Clear(){}}
 public class Collider {public TypewriterKey key;public T GetComponentInParent<T>() where T:class=>key as T;public ImportedObstacleActor actor;public Transform transform=new Transform();public GameObject gameObject=new GameObject();}
 public class BoxCollider:Collider {public Vector3 center,size;public Bounds bounds=>new Bounds {center=transform.TransformPoint(center),size=size};}
 public static class Time {public static float time,fixedDeltaTime=.02f;}
 public static class Mathf {public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static float Abs(float a)=>Math.Abs(a);public static float Clamp(float n,float a,float b)=>Math.Max(a,Math.Min(b,n));public static float MoveTowards(float a,float b,float d)=>Math.Abs(b-a)<=d?b:a+Math.Sign(b-a)*d;}
 public enum QueryTriggerInteraction {Ignore}
 public static class Physics {public static Collider[] found=new Collider[0];public static int OverlapBoxNonAlloc(Vector3 p,Vector3 s,Collider[] target,object r,int mask,QueryTriggerInteraction q){Array.Copy(found,target,found.Length);return found.Length;}}
 public static class Debug {public static void LogError(string m,object o){throw new Exception(m);}}
}
namespace ChessFight.Game {public static class RuntimePanels {public static Font KoreanFont;}public static class LitMaterials {public static void SetColor(MaterialPropertyBlock m,Color c){}}}
namespace ChessFight.Gameplay {public class Obstacle:MonoBehaviour{} public interface IMovingSurface {Quaternion DeltaRotation{get;}Vector3 PointVelocity(Vector3 p);}}
namespace ChessFight.ProtectKing {public class ObstacleContext {public bool Running=true,Authority=true;}public class ImportedObstacleActor {public bool CanReceiveObstacle=true,Grounded=true;public Vector3 Position,Impulse;public Collider support;public bool TryGetGroundCollider(out Collider c){c=support;return Grounded&&c!=null;}public int hits;public static ImportedObstacleActor Find(Collider c)=>c.actor;public void AddObstacleHit(Vector3 v,float stun){hits++;Impulse=v;}}}
class Harness {
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static void Set(object o,string field,object v)=>o.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,v);
 static void Tick(TypewriterStairs s,float time){Time.time=time;typeof(TypewriterStairs).GetMethod("FixedUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,null);}
 static void Main(){
  var random=new System.Random(51);string previous=null;int changes=0;
  for(int seed=0;seed<2000;seed++) {
   var layout=TypewriterMemory.Generate(new System.Random(seed));Check(layout.Sequence.Length==5&&layout.Keys.Length==20,"Layout size");
   Check(new HashSet<char>(layout.Sequence).Count==5,"Distinct random letters");int last=-1;
   for(int row=0;row<5;row++) {int safe=-1,count=0;var unique=new HashSet<char>();
    for(int col=0;col<4;col++){unique.Add(layout.Keys[row*4+col]);if(layout.Safe(row*4+col)){safe=col;count++;}}
    Check(count==1&&unique.Count==4,"One safe distinct key per row");Check(last<0||Math.Abs(safe-last)<=1,"Reachable adjacent columns");last=safe;
   }
   Check(!layout.Safe(-1)&&!layout.Safe(20),"Invalid indices");if(previous!=layout.Sequence)changes++;previous=layout.Sequence;
   Check(layout.Keys==TypewriterMemory.Generate(new System.Random(seed)).Keys,"Repeatable seed");
  }
  Check(changes>1950,"Random rounds differ");
  var puzzle=TypewriterMemory.Generate(random);var stairs=new TypewriterStairs();Set(stairs,"layout",puzzle);
  int safeIndex=0;while(!puzzle.Safe(safeIndex))safeIndex++;int wrongIndex=safeIndex==0?1:0;
  var areas=new BoxCollider[20];var keys=new TypewriterKey[20];
  for(int i=0;i<20;i++){areas[i]=new BoxCollider{center=new Vector3(i*3,.15f,0),size=new Vector3(2.8f,.7f,2.8f)};keys[i]=new TypewriterKey();}
  Set(stairs,"keyAreas",areas);Set(stairs,"keys",keys);Set(stairs,"flashUntil",new float[20]);
  Set(stairs,"keyboardWell",new BoxCollider{center=new Vector3(30,.15f,0),size=new Vector3(65,2,5)});
  var context=new ObstacleContext();Set(stairs,"context",context);
  var actor=new ImportedObstacleActor{Position=new Vector3(safeIndex*3,.25f,0)};Physics.found=new[]{new Collider{actor=actor},new Collider{actor=actor}};
  actor.support=new Collider{key=keys[safeIndex]};
  Tick(stairs,0);Check(actor.hits==0,"Safe row key and reaching hands ignored");
  actor.Position=new Vector3(wrongIndex*3,.25f,0);actor.Grounded=false;Tick(stairs,.1f);Check(actor.hits==0,"Airborne crossing");
  actor.Grounded=true;actor.support=null;actor.Position=new Vector3(wrongIndex*3,-2,0);Tick(stairs,.2f);Check(actor.hits==0,"Below-key contact");
  actor.support=new Collider{key=keys[wrongIndex]};actor.Position=new Vector3(wrongIndex*3,.25f,0);Tick(stairs,.3f);Check(actor.hits==1&&actor.Impulse.z==-12&&actor.Impulse.y==6,"Wrong grounded key");
  Tick(stairs,.4f);Check(actor.hits==1,"No multiple impulses");context.Authority=false;Tick(stairs,2);Check(actor.hits==1,"Authority guard");context.Authority=true;
  actor.support=new Collider();actor.support.transform.parent=((BoxCollider)typeof(TypewriterStairs).GetField("keyboardWell",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(stairs)).transform;
  actor.Position=new Vector3(1.5f,0,1.5f);Tick(stairs,3);Check(actor.hits==2,"Keyboard gaps cannot bypass the puzzle");
  // Every physical row has a safe support, including a hip projection outside the circular key.
  int priorHits=actor.hits;
  for(int row=0;row<5;row++) {
   int index=row*4;while(!puzzle.Safe(index))index++;
   actor.support=new Collider{key=keys[index]};actor.Position=new Vector3(1.5f,0,1.5f);
   Tick(stairs,10+row*2);Check(actor.hits==priorHits,"Safe support edge must not become a gap hit");
  }
  actor.support=new Collider();Tick(stairs,21);Check(actor.hits==priorHits,"Unrelated supporting floor is not keyboard bed");
  var spring=new TypewriterKey();Set(spring,"body",new Rigidbody());spring.SetPressed(true);
  var step=typeof(TypewriterKey).GetMethod("FixedUpdate",BindingFlags.NonPublic|BindingFlags.Instance);
  for(int i=0;i<20;i++)step.Invoke(spring,null);
  float depth=(float)typeof(TypewriterKey).GetField("depth",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(spring);
  Check(Math.Abs(depth-.14f)<.001f,"Key depresses under load");
  Check(Math.Abs(spring.TakeJumpBonus(Vector3.one)-1.2f)<.001f,"Small bonus on explicit jump");Check(spring.TakeJumpBonus(Vector3.one)==0,"Bonus cannot stack");
  spring.SetPressed(false);Time.time=4;for(int i=0;i<20;i++)step.Invoke(spring,null);
  Check(spring.TakeJumpBonus(Vector3.one)==0,"Walking off does not launch");
  var text=new TextMesh[20];for(int i=0;i<20;i++)text[i]=new TextMesh();var clues=new TextMesh[5];for(int i=0;i<5;i++)clues[i]=new TextMesh();Set(stairs,"keyText",text);Set(stairs,"clueText",clues);
  string before=puzzle.Sequence;stairs.NewRound();var next=(TypewriterMemory)typeof(TypewriterStairs).GetField("layout",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(stairs);
  Check(next.Sequence!=before,"New round changes clues");for(int i=0;i<5;i++)Check(clues[i].text==(i+1)+"\n"+next.Sequence[i],"Clue order");for(int i=0;i<20;i++)Check(text[i].text==next.Keys[i].ToString(),"Keyboard matches clues");
  Console.WriteLine("PASS: 2000 memory layouts, row rules, hits, key depression, jump-only rebound and new-round clues");
 }
}
