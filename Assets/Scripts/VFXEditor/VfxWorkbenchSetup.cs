#if CF_URP
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChessFight.VFX.Editor
{
    /// <summary>
    /// Adds the Neutral tonemapping comparison to the VFX workbench (R172, 10-27, 사용자: "Neutral이 괜찮아 보여. 게임
    /// 전체를 바꾸지 말고 작업대에서 Neutral로 비교할 수 있게만 해줘"): a global volume in the workbench scene that overrides
    /// only the tonemapping (Neutral), off by default, which N turns on and off in Play mode. The game's GameLook profile
    /// (ACES) is not touched. The scene is changed through the editor (opened additively if it is not open, then saved).
    /// URP only (CF_URP, as in <see cref="VfxCapture"/>).
    /// </summary>
    public static class VfxWorkbenchSetup
    {
        const string ScenePath = VfxAssets.Root + "/Scenes/VFX_Workbench.unity";
        const string ProfilePath = VfxAssets.Root + "/Settings/VFX_Workbench_Neutral.asset";
        const string VolumeName = "Bench Tonemap (Neutral)";

        [MenuItem("ChessFight/VFX/Workbench/Add Neutral Tonemap Compare")]
        public static void AddNeutral()
        {
            if (EditorApplication.isPlaying) return;
            var profile = Profile();
            var scene = EditorSceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                VfxWorkbench bench = null;
                foreach (var root in scene.GetRootGameObjects())
                    if (bench == null) bench = root.GetComponentInChildren<VfxWorkbench>(true);
                if (bench == null)
                {
                    Debug.LogError("[VFX] No VfxWorkbench in " + ScenePath);
                    return;
                }
                var go = bench.neutralVolume;
                if (go == null)
                {
                    go = new GameObject(VolumeName);
                    // Next to the bloom volume (B), so the two comparisons sit together.
                    var parent = bench.benchVolume != null ? bench.benchVolume.transform.parent : null;
                    if (parent != null) go.transform.SetParent(parent, false);
                    else EditorSceneManager.MoveGameObjectToScene(go, scene);
                    go.AddComponent<Volume>();
                }
                var volume = go.GetComponent<Volume>();
                volume.isGlobal = true;
                // Over GameLook's soft volume (10) and the bench bloom (50): only its tonemapping differs.
                volume.priority = 60f;
                volume.sharedProfile = profile;
                go.SetActive(false);
                bench.neutralVolume = go;
                EditorUtility.SetDirty(bench);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[VFX] Workbench: N compares Neutral tonemapping (" + go.name + ", off at start).");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [MenuItem("ChessFight/VFX/Workbench/Add Neutral Tonemap Compare", true)]
        static bool CanAdd() => !EditorApplication.isPlaying;

        static VolumeProfile Profile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile != null) return profile;
            VfxAssets.Folder(VfxAssets.Root + "/Settings");
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            var map = profile.Add<Tonemapping>(false);
            map.mode.Override(TonemappingMode.Neutral);
            AssetDatabase.AddObjectToAsset(map, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
#endif
