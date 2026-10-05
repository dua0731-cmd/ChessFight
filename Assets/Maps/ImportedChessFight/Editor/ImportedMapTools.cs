using System;
using System.IO;
using System.Linq;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.Maps.Editor
{
    public static class ImportedMapTools
    {
        const string ScenePath="Assets/Scenes/ImportedChessFightMap.unity";
        const string Prefabs="Assets/Maps/ImportedChessFight/Prefabs/Obstacles";
        [MenuItem("ChessFight/Imported Map/Open Map")]
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var map=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Map");
            if(map!=null){Selection.activeGameObject=map; SceneView.lastActiveSceneView?.FrameSelected();}
        }
        [MenuItem("ChessFight/Imported Map/Select Obstacle Prefabs")]
        public static void SelectPrefabs()=>Selection.activeObject=AssetDatabase.LoadAssetAtPath<DefaultAsset>(Prefabs);

        // Explicit batch entry point; never runs on project load or regenerates the map.
        public static void ValidateBatch()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var roots=scene.GetRootGameObjects();
            var all=roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach(var t in all)
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)!=0)
                    throw new Exception("Missing script: "+t.name);
            foreach(var renderer in roots.SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)))
                foreach(var material in renderer.sharedMaterials)
                    if(material==null || material.shader==null || !material.shader.isSupported)
                        throw new Exception("Invalid material: "+renderer.name);
            int prefabs=0;
            foreach(var asset in AssetDatabase.FindAssets("t:Prefab",new[]{Prefabs}))
            {
                var p=AssetDatabase.GUIDToAssetPath(asset);
                var go=PrefabUtility.LoadPrefabContents(p);
                try
                {
                    foreach(var t in go.GetComponentsInChildren<Transform>(true))
                        if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)!=0)
                            throw new Exception("Missing prefab script: "+p+"/"+t.name);
                    prefabs++;
                }
                finally{PrefabUtility.UnloadPrefabContents(go);}
            }
            if(prefabs!=19)throw new Exception("Expected 19 obstacle prefabs, found "+prefabs);
            // Reuse the destination's existing offline driver and ragdoll prefab.
            // It is a scene fixture, not an imported player implementation.
            var fixture=roots.FirstOrDefault(g=>g.name=="Core Playtest");
            if(fixture==null)
            {
                fixture=new GameObject("Core Playtest");
                fixture.AddComponent<PhysicsProfile>();
                var camera=roots.SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).First();
                var rig=fixture.AddComponent<CameraRig>();
                var rigData=new SerializedObject(rig);
                rigData.FindProperty("target").objectReferenceValue=camera;
                rigData.FindProperty("followOffset").vector3Value=new Vector3(0,6,-8);
                rigData.FindProperty("pitch").floatValue=35;
                rigData.ApplyModifiedPropertiesWithoutUndo();
                var spawner=fixture.AddComponent<PlaytestSpawner>();
                var data=new SerializedObject(spawner);
                var pawn=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChessFight/RagdollLab/Prefabs/RagdollPawn.prefab");
                if(pawn==null)throw new Exception("Destination ragdoll prefab missing");
                data.FindProperty("characterPrefab").objectReferenceValue=pawn;
                // These are the source map's existing spawn anchors.
                var start=all.FirstOrDefault(t=>t.name=="Blue_King_Start");
                if(start==null)start=all.FirstOrDefault(t=>t.name.Contains("Spawn") && t.position.z<15);
                if(start==null)
                {
                    var marker=new GameObject("Core Playtest Spawn");marker.transform.SetParent(fixture.transform);
                    marker.transform.position=new Vector3(-13,0,2);start=marker.transform;
                }
                data.FindProperty("spawnPoint").objectReferenceValue=start;
                data.FindProperty("cameraRig").objectReferenceValue=rig;
                data.FindProperty("fallLimit").floatValue=-7;
                data.FindProperty("showHelp").boolValue=true;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene);
            }
            Directory.CreateDirectory("Logs/ImportedMapValidation");
            File.WriteAllText("Logs/ImportedMapValidation/Editor.txt",$"PASS: Unity imported scene; {all.Length} transforms, {prefabs} obstacle prefabs; no missing scripts or unsupported materials.\nCore Playtest reuses destination ragdoll and PlaytestSpawner.\nHuman Unity/Steam confirmation pending.\n");
            Debug.Log("Imported map asset checks passed.");
        }
    }
}
