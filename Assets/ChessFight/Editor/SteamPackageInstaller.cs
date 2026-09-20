using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
namespace ChessFight.ProtectKing.Editor {
 public static class SteamPackageInstaller {
  static AddRequest request;
  [MenuItem("CHESS FIGHT/Online/Install Steamworks")]
  public static void Install() {
   request=Client.Add("https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1");
   EditorApplication.update+=Poll;
  }
  static void Poll() {
   if(request==null || !request.IsCompleted)return;
   EditorApplication.update-=Poll;
   if(request.Status==StatusCode.Success) Debug.Log("[CHESS FIGHT] Steamworks installed: "+request.Result.version);
   else Debug.LogError(request.Error.message);
  }
 }
}