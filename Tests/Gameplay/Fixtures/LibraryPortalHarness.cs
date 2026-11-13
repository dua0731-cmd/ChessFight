using System;
using System.Reflection;
using UnityEngine;
using ChessFight.RagdollLab;
namespace UnityEngine {
 public class SerializeField:Attribute{} public class MinAttribute:Attribute{public MinAttribute(float f){}} public class RangeAttribute:Attribute{public RangeAttribute(float a,float b){}}
 public class MonoBehaviour {public T FindFirstObjectByType<T>() where T:class=>LabCamera.Active as T;}
 public class GameObject {public bool active=true;public void SetActive(bool b){active=b;}}
 public class Transform {public Vector3 position;public Vector3 InverseTransformPoint(Vector3 p)=>p;}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
 public struct Color {public Color(float a,float b,float c,float d){}}
 public struct Rect {public Rect(float a,float b,float c,float d){}}
 public static class GUI {public static Color color;public static void DrawTexture(Rect r,object t){}}
 public static class Screen {public static int width=100,height=100;}
 public static class Texture2D {public static object whiteTexture;}
 public static class Time {public static float deltaTime=.1f;}
 public static class Mathf {public const float PI=(float)Math.PI;public static float Abs(float f)=>Math.Abs(f);public static float Max(float a,float b)=>Math.Max(a,b);public static float Sin(float f)=>(float)Math.Sin(f);public static float Clamp01(float f)=>Math.Max(0,Math.Min(1,f));}
}
namespace ChessFight.RagdollLab {
 public class LabCamera {public static LabCamera Active;public RagdollPawn soloTarget;public float baseFov=60,distance=12;}
 public class RagdollPawn {public Transform Hips=new Transform();}
}
class Harness {
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
 static void Call(object o,string n)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main(){
  var camera=new LabCamera{soloTarget=new RagdollPawn()};LabCamera.Active=camera;
  var portal=new RoyalLibraryPortal();var veil=new GameObject();Set(portal,"veil",veil);Set(portal,"threshold",new Transform());Call(portal,"Awake");
  camera.soloTarget.Hips.position=new Vector3(0,2,-1);Call(portal,"Update");
  camera.soloTarget.Hips.position=new Vector3(0,2,1);Call(portal,"Update");
  Check(!veil.active&&camera.baseFov>60&&camera.distance>12,"Forward crossing reveals and expands");
  for(int i=0;i<20;i++)Call(portal,"Update");Check(camera.baseFov==60&&camera.distance==12,"Natural completion restores camera");
  portal.ResetPresentation();Check(veil.active,"Reset restores veil");
  camera.soloTarget.Hips.position=new Vector3(10,2,-1);Call(portal,"Update");camera.soloTarget.Hips.position=new Vector3(10,2,1);Call(portal,"Update");Check(veil.active,"Outside aperture does not reveal");
  camera.soloTarget.Hips.position=new Vector3(0,2,-1);Call(portal,"Update");camera.soloTarget.Hips.position=new Vector3(0,2,1);Call(portal,"Update");
  Call(portal,"OnDisable");Check(camera.baseFov==60&&camera.distance==12,"Disable restores camera");
  portal.ResetPresentation();camera.soloTarget.Hips.position=new Vector3(0,2,-1);Call(portal,"Update");camera.soloTarget.Hips.position=new Vector3(0,2,1);Call(portal,"Update");
  camera.soloTarget=new RagdollPawn();Call(portal,"Update");Check(camera.baseFov==60&&camera.distance==12,"Target switch cancels cinematic");
  Console.WriteLine("PASS: portal crossing, aperture, reset, completion, disable and target change");
 }
}
