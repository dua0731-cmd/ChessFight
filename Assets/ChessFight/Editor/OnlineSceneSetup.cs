using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChessFight.ProtectKing.Editor
{
    public static class OnlineSceneSetup
    {
        [MenuItem("CHESS FIGHT/Online/Install into Current Map")]
        public static void Install()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=GrayboxBuilder.ScenePath||scene.isDirty)
                throw new InvalidOperationException("Open the saved ProtectTheKing_Graybox scene in Edit Mode first.");
            Directory.CreateDirectory("Artifacts/ChessFight/BeforeOnline");
            string backup="Artifacts/ChessFight/BeforeOnline/ProtectTheKing_Graybox.unity";
            if(!File.Exists(backup))File.Copy(scene.path,backup);
            var local=UnityEngine.Object.FindFirstObjectByType<LocalPlaySession>();
            if(local==null||local.map.players.Length!=12)throw new InvalidOperationException("Expected twelve existing players.");
            var online=local.GetComponent<OnlineMatchSession>();
            if(online==null)online=Undo.AddComponent<OnlineMatchSession>(local.gameObject);
            var transport=local.GetComponent<SteamLobbyTransport>();
            if(transport==null)transport=Undo.AddComponent<SteamLobbyTransport>(local.gameObject);
            online.local=local;online.map=local.map;online.transport=transport;local.online=online;
            var root=GameObject.Find("AI Navigation");
            if(root==null) { root=new GameObject("AI Navigation");Undo.RegisterCreatedObjectUndo(root,"AI routes"); }
            for(int team=0;team<2;team++)
            {
                string name=team==0?"Blue Waypoints":"Red Waypoints";
                var routeRoot=root.transform.Find(name);
                Transform[] route;
                if(routeRoot!=null) {
                    route=new Transform[routeRoot.childCount];for(int i=0;i<route.Length;i++)route[i]=routeRoot.GetChild(i);
                } else {
                    routeRoot=new GameObject(name).transform;routeRoot.SetParent(root.transform);
                    float x=team==0?-16:16, end=team==0?-12:12;
                    var points=new List<Vector2> {
                        new Vector2(x,15),new Vector2(x,27),new Vector2(x,96),new Vector2(x,110),new Vector2(x,125),new Vector2(x,143),new Vector2(x,152),
                        new Vector2(0,158),new Vector2(0,177),new Vector2(0,195),new Vector2(0,220),new Vector2(0,252),
                        new Vector2(-18,274),new Vector2(-18,393),new Vector2(0,400),new Vector2(0,432),
                        new Vector2(0,440),new Vector2(0,450),new Vector2(0,505),new Vector2(0,523),new Vector2(0,542),
                        new Vector2(x,552),new Vector2(x,561),new Vector2(x,572),new Vector2(x-3.5f,582),new Vector2(x-3.5f,597),
                        new Vector2(x,603),new Vector2(x,630),new Vector2(x-3.5f,635),new Vector2(x-3.5f,648),new Vector2(x,655),new Vector2(x,682),new Vector2(x,692),
                        new Vector2(end,713),new Vector2(end,733),new Vector2(end,768),new Vector2(0,775)
                    };
                    route=new Transform[points.Count];
                    for(int i=0;i<points.Count;i++) {
                        var t=new GameObject("Waypoint "+i.ToString("00")).transform;t.SetParent(routeRoot);t.position=new Vector3(points[i].x,.1f,points[i].y);route[i]=t;
                    }
                }
                for(int i=team*6;i<team*6+6;i++) {
                    var player=local.map.players[i];
                    var bot=player.GetComponent<BotDriver>();if(bot==null)bot=Undo.AddComponent<BotDriver>(player.gameObject);
                    bot.motor=player.GetComponent<PlayerMotor>();bot.route=route;EditorUtility.SetDirty(bot);
                }
            }
            EditorUtility.SetDirty(local);EditorUtility.SetDirty(online);EditorUtility.SetDirty(transport);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Selection.activeGameObject=local.gameObject;
            Debug.Log("[CHESS FIGHT] Online session and 12 AI drivers saved in the actual map scene. Set Steam App ID here.");
        }
    }
}