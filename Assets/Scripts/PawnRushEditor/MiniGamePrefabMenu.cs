using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush.Editor
{
    // Authoring is repeatable from code. Geometry and surface assets are persisted before saving the prefab.
    public static class MiniGamePrefabMenu
    {
        public const string TestScene = "Assets/Scenes/PawnRush_MiniGames.unity";
        const string PrefabFolder = "Assets/Prefabs/PawnRushSections";
        const string MaterialFolder = "Assets/Materials/PawnRushMiniGames";
        const string GeometryFolder = "Assets/Art/PawnRushMiniGames";
        const string KitPath = "Assets/Settings/Course01Kit.asset";

        public static readonly int[] Games =
        {
            PawnRushMissions.Drawbridge, PawnRushMissions.Capstan, PawnRushMissions.Paint,
            PawnRushMissions.Planks, PawnRushMissions.Bells, PawnRushMissions.EnPassant, PawnRushMissions.KnightPads,
            PawnRushMissions.KingEscort, PawnRushMissions.Checkmate, PawnRushMissions.CheckBlock
        };

        static readonly string[] FileNames =
        {
            "Early_MG_A_ParadeCourt", "Mid_MG_B_RepairWing", "Early_MG_C_MoatDrawbridge", "Early_MG_D_PortcullisCourt",
            "Mid_MG_E_RoyalLibrary", "Mid_MG_F_ClockworkBallroom", "Late_MG_G_RoyalProcession",
            "Late_MG_H_KingsChessHall", "Late_MG_I_KingsBedchamber", "Mid_MG_J_PalaceLaundry"
        };

        public static string PrefabPath(int game) => PrefabFolder + "/" + FileNames[game] + ".prefab";
        public static int Zone(int game) => MiniGamePalaceArt.Early(game) ? 0 : MiniGamePalaceArt.Late(game) ? 2 : 1;

        [MenuItem("ChessFight/Pawn Rush/Build All Mini-game Prefabs", false, 23)]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var kit = AssetDatabase.LoadAssetAtPath<Course01Kit>(KitPath);
            if (kit == null) throw new InvalidOperationException("Missing course kit: " + KitPath);
            EnsureFolder(MaterialFolder);
            EnsureFolder(GeometryFolder);
            var assets = new PersistentAssets();
            var scratch = EditorSceneManager.NewPreviewScene();
            var choices = new GameObject[Games.Length];
            try
            {
                for (int i = 0; i < Games.Length; i++)
                {
                    int game = Games[i];
                    var root = new GameObject(FileNames[game]);
                    SceneManager.MoveGameObjectToScene(root, scratch);
                    root.SetActive(false);
                    var section = root.AddComponent<MiniGameSection>();
                    section.Configure(kit, game, Zone(game));
                    MiniGameSectionBuilder.Build(section);
                    root.SetActive(true);
                    assets.Persist(root);
                    choices[i] = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(game));
                    if (choices[i] == null) throw new InvalidOperationException("Could not save " + PrefabPath(game));
                    Object.DestroyImmediate(root);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scratch); }
            RegisterPools(choices);
            BuildTestScene(choices);
            AssetDatabase.SaveAssets();
            Debug.Log("[PawnRush] 미니게임 프리팹 10개 저장: 초반 3 · 중반 4 · 후반 3. 시험 장면: " + TestScene);
        }

        // Entry point for an isolated Editor import/build. A failure never reports a successful bake.
        public static void BuildBatch()
        {
            try
            {
                BuildAll();
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        [MenuItem("ChessFight/Pawn Rush/Open Mini-game Prefab Test", false, 24)]
        public static void OpenTest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScene) == null) BuildAll();
            EditorSceneManager.OpenScene(TestScene);
        }

        static void RegisterPools(GameObject[] choices)
        {
            var set = AssetDatabase.LoadAssetAtPath<PawnRushSectionSet>(SectionPrefabMenu.SectionSet);
            if (set == null) throw new InvalidOperationException("Missing section set: " + SectionPrefabMenu.SectionSet);
            var serialized = new SerializedObject(set);
            var slots = serialized.FindProperty("slots");
            if (slots.arraySize < 7) throw new InvalidOperationException("The section set must contain seven slots.");
            for (int zone = 0; zone < 3; zone++)
            {
                var slot = slots.GetArrayElementAtIndex(zone * 2 + 1);
                slot.FindPropertyRelative("kind").intValue = (int)PawnRushSection.SlotKind.MiniGame;
                var list = slot.FindPropertyRelative("prefabs");
                var desired = new List<GameObject>();
                // Keep user-authored alternatives; only refresh the ten prefabs owned by this generator.
                for (int i = 0; i < list.arraySize; i++)
                {
                    var previous = list.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                    if (previous != null && Array.IndexOf(choices, previous) < 0) desired.Add(previous);
                }
                for (int i = 0; i < choices.Length; i++) if (Zone(Games[i]) == zone) desired.Add(choices[i]);
                list.arraySize = desired.Count;
                for (int i = 0; i < desired.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = desired[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
        }

        static void BuildTestScene(GameObject[] choices)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScene) == null && !AssetDatabase.CopyAsset(SectionPrefabMenu.TestScene, TestScene))
                throw new InvalidOperationException("Could not copy the existing offline section test.");
            var scene = SceneManager.GetSceneByPath(TestScene);
            var previousActive = SceneManager.GetActiveScene();
            bool close = !scene.isLoaded;
            if (close) scene = EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                PlaytestSpawner spawner = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var round in root.GetComponentsInChildren<PawnRushRound>(true)) Object.DestroyImmediate(round);
                    foreach (var test in root.GetComponentsInChildren<SectionPlaytest>(true)) Object.DestroyImmediate(test);
                    if (spawner == null) spawner = root.GetComponentInChildren<PlaytestSpawner>(true);
                }
                if (spawner == null) throw new InvalidOperationException("The offline test has no PlaytestSpawner.");
                var viewer = spawner.GetComponent<MiniGamePrefabTest>();
                if (viewer == null) viewer = spawner.gameObject.AddComponent<MiniGamePrefabTest>();
                viewer.Configure(choices, spawner);
                var settings = new SerializedObject(spawner);
                settings.FindProperty("showHelp").boolValue = false;
                settings.FindProperty("fallLimit").floatValue = -12f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(.72f, .77f, .84f);
                RenderSettings.ambientEquatorColor = new Color(.57f, .58f, .60f);
                RenderSettings.ambientGroundColor = new Color(.32f, .30f, .28f);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, TestScene);
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                if (close) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(parent.Length + 1));
        }

        // Never add an Arena/MapLook cache object itself to an asset: other prefabs and runtime builders
        // still own it. Save a clone, then replace only the baked hierarchy's reference.
        sealed class PersistentAssets
        {
            readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
            readonly Dictionary<Mesh, Mesh> meshes = new Dictionary<Mesh, Mesh>();
            readonly Dictionary<Texture, Texture> textures = new Dictionary<Texture, Texture>();

            public void Persist(GameObject root)
            {
                var labels = root.GetComponentsInChildren<TextMesh>(true);
                if (labels.Length > 0)
                {
                    var binder = root.GetComponent<PalaceTextBinding>();
                    if (binder == null) binder = root.AddComponent<PalaceTextBinding>();
                    binder.Configure(labels);
                    var placeholder = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    foreach (var label in labels)
                    {
                        label.font = placeholder;
                        label.GetComponent<Renderer>().sharedMaterial = placeholder.material;
                    }
                }
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true)) filter.sharedMesh = MeshAsset(filter.sharedMesh);
                foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true)) collider.sharedMesh = MeshAsset(collider.sharedMesh);
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.GetComponent<TextMesh>() != null) continue;
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = MaterialAsset(slots[i]);
                    renderer.sharedMaterials = slots;
                }
            }

            Material MaterialAsset(Material source)
            {
                if (source == null || EditorUtility.IsPersistent(source)) return source;
                if (materials.TryGetValue(source, out var previous)) return previous;
                var fingerprint = new StringBuilder(source.shader.name).Append('|').Append(source.renderQueue);
                foreach (var keyword in source.shaderKeywords) fingerprint.Append('|').Append(keyword);
                for (int i = 0; i < ShaderUtil.GetPropertyCount(source.shader); i++)
                {
                    string property = ShaderUtil.GetPropertyName(source.shader, i);
                    fingerprint.Append('|').Append(property).Append('=');
                    switch (ShaderUtil.GetPropertyType(source.shader, i))
                    {
                        case ShaderUtil.ShaderPropertyType.Color: fingerprint.Append(source.GetColor(property).ToString("R")); break;
                        case ShaderUtil.ShaderPropertyType.Vector: fingerprint.Append(source.GetVector(property).ToString("R")); break;
                        case ShaderUtil.ShaderPropertyType.TexEnv:
                            var texture = source.GetTexture(property);
                            fingerprint.Append(texture != null ? AssetDatabase.GetAssetPath(texture) + texture.name + texture.imageContentsHash : "none");
                            fingerprint.Append(source.GetTextureScale(property).ToString("R")).Append(source.GetTextureOffset(property).ToString("R"));
                            break;
                        default: fingerprint.Append(source.GetFloat(property).ToString("R", CultureInfo.InvariantCulture)); break;
                    }
                }
                string path = MaterialFolder + "/" + Safe(source.name) + "_" + Hash(fingerprint.ToString()) + ".mat";
                var clone = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (clone == null) { clone = new Material(source); AssetDatabase.CreateAsset(clone, path); }
                else EditorUtility.CopySerialized(source, clone);
                materials[source] = clone;
                foreach (var property in clone.GetTexturePropertyNames()) clone.SetTexture(property, TextureAsset(source.GetTexture(property)));
                EditorUtility.SetDirty(clone);
                return clone;
            }

            Mesh MeshAsset(Mesh source)
            {
                if (source == null || EditorUtility.IsPersistent(source)) return source;
                if (meshes.TryGetValue(source, out var previous)) return previous;
                var fingerprint = new StringBuilder();
                foreach (var vertex in source.vertices) fingerprint.Append(vertex.ToString("R"));
                foreach (var normal in source.normals) fingerprint.Append(normal.ToString("R"));
                foreach (var uv in source.uv) fingerprint.Append(uv.ToString("R"));
                foreach (var color in source.colors32) fingerprint.Append(color.ToString());
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    fingerprint.Append('|').Append(source.GetTopology(sub));
                    foreach (var index in source.GetIndices(sub)) fingerprint.Append(index).Append(',');
                }
                string path = GeometryFolder + "/" + Safe(source.name) + "_" + Hash(fingerprint.ToString()) + ".asset";
                var clone = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (clone == null) { clone = Object.Instantiate(source); AssetDatabase.CreateAsset(clone, path); }
                meshes[source] = clone;
                return clone;
            }

            Texture TextureAsset(Texture source)
            {
                if (source == null || EditorUtility.IsPersistent(source)) return source;
                if (textures.TryGetValue(source, out var previous)) return previous;
                string path = GeometryFolder + "/" + Safe(source.name) + "_" + Hash(source.width + "/" + source.height + "/" + source.imageContentsHash) + ".asset";
                var clone = AssetDatabase.LoadAssetAtPath<Texture>(path);
                if (clone == null) { clone = Object.Instantiate(source); AssetDatabase.CreateAsset(clone, path); }
                textures[source] = clone;
                return clone;
            }

            static string Safe(string name)
            {
                var value = new StringBuilder("MG_");
                foreach (char letter in name) value.Append(char.IsLetterOrDigit(letter) || letter == '_' ? letter : '_');
                return value.ToString();
            }

            static string Hash(string value)
            {
                using (var hash = SHA256.Create())
                {
                    var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(value));
                    return BitConverter.ToString(bytes, 0, 6).Replace("-", "").ToLowerInvariant();
                }
            }
        }
    }
}
