using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.ChessBoxingPrototype.Editor
{
    /// <summary>Explicit menu only: creates new prototype assets without editing shared game assets.</summary>
    public static class CBPrototypeBuilder
    {
        const string Root = "Assets/ChessBoxingPrototype";
        const string ScenePath = Root + "/Scenes/ChessBoxingPrototype.unity";
        const string VisualPath = Root + "/Generated/PawnVisual.prefab";
        const string SourcePath = "Assets/Prefabs/RagdollPawn.prefab";

        [MenuItem("ChessFight/Chess Boxing Prototype/Create or Open Test Scene")]
        public static void CreateOrOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Stop Play mode before opening the Chess Boxing Prototype.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                return;
            }
            if (File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("Chess Boxing Prototype", "An existing scene could not be loaded. It was not overwritten:\n" + ScenePath, "OK");
                return;
            }
            try
            {
                EnsureDirectory(Root + "/Generated");
                EnsureDirectory(Root + "/Scenes");
                GameObject visual = GetOrCreateVisual();
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var controller = new GameObject("Chess Boxing Prototype — Stage 1");
                var match = controller.AddComponent<CBMatch>();
                match.pawnModel = visual;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save " + ScenePath);
                AssetDatabase.SaveAssets();
                Selection.activeGameObject = controller;
                Debug.Log("Chess Boxing Prototype created. Press Play. All added assets are under " + Root + ".");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorUtility.DisplayDialog("Chess Boxing Prototype", "Could not create the prototype. Existing game assets were not edited.\n\n" + error.Message, "OK");
            }
        }

        static GameObject GetOrCreateVisual()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
            if (existing != null) return existing;
            if (File.Exists(VisualPath)) throw new IOException("Existing visual could not be loaded; not overwriting " + VisualPath);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null) throw new FileNotFoundException("The source pawn prefab is missing: " + SourcePath);
            GameObject contents = null, copy = null;
            var created = new List<string>();
            try
            {
                // Loading the prefab in an editor preview scene does not run its gameplay behaviours.
                contents = PrefabUtility.LoadPrefabContents(SourcePath);
                var visual = contents.transform.Find("Visual");
                if (visual == null) throw new InvalidOperationException("The source pawn prefab has no Visual child.");
                copy = UnityEngine.Object.Instantiate(visual.gameObject);
                copy.name = "CB Pawn Visual";
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                // Retain only transform/mesh data. No existing ragdoll, network or controller is copied.
                foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour != null) UnityEngine.Object.DestroyImmediate(behaviour);
                foreach (var component in copy.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(component);
                foreach (var component in copy.GetComponentsInChildren<Joint>(true)) UnityEngine.Object.DestroyImmediate(component);
                foreach (var component in copy.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(component);
                foreach (var component in copy.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(component);

                foreach (var skin in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.rootBone == null || !skin.rootBone.IsChildOf(copy.transform))
                        throw new InvalidOperationException("The pawn skin root is outside its Visual hierarchy.");
                    foreach (Transform bone in skin.bones)
                        if (bone != null && !bone.IsChildOf(copy.transform))
                            throw new InvalidOperationException("The copied pawn skin references an external bone: " + bone.name);
                }

                int meshNumber = 0, materialNumber = 0;
                foreach (var skin in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.sharedMesh == null) continue;
                    string path = Root + "/Generated/PawnMesh_" + meshNumber++ + ".asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh == null)
                    {
                        mesh = UnityEngine.Object.Instantiate(skin.sharedMesh);
                        mesh.name = "CB Pawn Mesh";
                        AssetDatabase.CreateAsset(mesh, path);
                        created.Add(path);
                    }
                    skin.sharedMesh = mesh;
                }
                foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
                {
                    var sourceMaterials = renderer.sharedMaterials;
                    var materials = new Material[sourceMaterials.Length];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (sourceMaterials[i] == null) continue;
                        string path = Root + "/Generated/PawnMaterial_" + materialNumber++ + ".mat";
                        materials[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (materials[i] != null) continue;
                        materials[i] = new Material(sourceMaterials[i]) { name = "CB Pawn Material" };
                        AssetDatabase.CreateAsset(materials[i], path);
                        created.Add(path);
                    }
                    renderer.sharedMaterials = materials;
                }
                if (copy.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 || copy.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                    throw new InvalidOperationException("The pawn visual was not stripped successfully.");
                var saved = PrefabUtility.SaveAsPrefabAsset(copy, VisualPath);
                if (saved == null) throw new IOException("Could not save " + VisualPath);
                created.Add(VisualPath);
                return saved;
            }
            catch
            {
                // Remove only assets made by this failed operation; never delete a pre-existing copy.
                for (int i = created.Count - 1; i >= 0; i--) AssetDatabase.DeleteAsset(created[i]);
                throw;
            }
            finally
            {
                if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
                if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
