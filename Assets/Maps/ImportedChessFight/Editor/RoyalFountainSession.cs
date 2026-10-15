using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChessFight.Maps.Editor {
// Temporary task-scoped command runner. No automatic scene or asset changes.
[InitializeOnLoad] public static class RoyalFountainSession {
 const string Request="Temp/RoyalFountain.request", Result="Temp/RoyalFountain.result.txt";
 static double next;
 static RoyalFountainSession(){EditorApplication.update+=Poll;}
 static void Poll(){
  if(EditorApplication.timeSinceStartup<next || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
  next=EditorApplication.timeSinceStartup+.3;
  if(!File.Exists(Request))return;
  var command=File.ReadAllText(Request).Trim();File.Delete(Request);
  try{
   if(command=="inspect") {File.WriteAllText(Result,"project="+Application.dataPath+"\nscene="+SceneManager.GetActiveScene().path+"\ndirty="+SceneManager.GetActiveScene().isDirty+"\nplay="+EditorApplication.isPlaying+"\npipeline="+UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline+"\nstandard="+Shader.Find("Standard"));return;}
   if(command=="refresh"){AssetDatabase.Refresh();File.WriteAllText(Result,"refresh");return;}
   if(command=="stop"){EditorApplication.isPlaying=false;File.WriteAllText(Result,"stop");return;}
   if(command=="play"){EditorApplication.isPlaying=true;File.WriteAllText(Result,"play");return;}
   if(command=="cleanup"){AssetDatabase.DeleteAsset("Assets/Maps/ImportedChessFight/Editor/RoyalFountainSession.cs");File.WriteAllText(Result,"runner removed");return;}
   var t=typeof(RoyalFountainSession).Assembly.GetType("ChessFight.Maps.Editor.RoyalFountainHallBuilder",true);
   File.WriteAllText(Result,"running "+command);
   t.GetMethod(command,BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
   File.AppendAllText(Result,"\nfinished "+command);
  }catch(Exception e){File.WriteAllText(Result,"ERROR "+e);Debug.LogException(e);}
 }
}}
