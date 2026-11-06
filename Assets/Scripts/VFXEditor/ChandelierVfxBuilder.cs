using UnityEditor;
using UnityEngine;

namespace ChessFight.VFX.Editor
{
    /// <summary>
    /// The grand stair hall's falling chandeliers' floor warning (R340, 11-04, 승규 "샹들리에 예고 vfx 만들어"): the same
    /// projected decal as the falling knights' W1 (HazardVfxBuilder, VFX_GroundWarnDecal), so every hazard in the hall warns
    /// the same way, on its own material: the rim at the stagger's reach (4.5 m, set by the game), a brighter inner ring at
    /// the knock-down's (2.5 m), 24 ticks for its shorter count (1.6 s) and a wider landing mark for the chandelier's
    /// ring. Menu ChessFight/VFX/Hazard/Build W1 chandelier → Prefabs/Common/21_Hazard_Chandelier_Warn, listed in
    /// Resources/VfxSets/Hazards as Chandelier_Warn, which PawnRush/ChandelierRain plays. Values tuned in the prefab's part
    /// are kept when it is built again; the material is only filled in when it is first made.
    /// </summary>
    public static class ChandelierVfxBuilder
    {
        const string SetPath = "Assets/Resources/VfxSets/Hazards.asset";
        const string WarnPrefab = VfxAssets.Root + "/Prefabs/Common/21_Hazard_Chandelier_Warn.prefab";
        const string WarnMaterial = VfxAssets.Root + "/Materials/M_Hazard_ChandelierWarnDecal.mat";
        const string WarnShader = "ChessFight/VFX/Ground Warn Decal";
        const string WarnPart = "W1 Decal";

        // The chandelier's look over the shader's own (the knights'); ChandelierRain uses the same when the prefab is missing.
        public const float InnerRing = 2.5f / 4.5f, Ticks = 24f, Mark = 1.3f, InnerLight = 1.8f;

        [MenuItem("ChessFight/Development/VFX/Hazard/Build W1 chandelier (falling chandelier warning)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the hazard effects are built in edit mode.");
                return;
            }
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(WarnPrefab);
            var root = new GameObject("21_Hazard_Chandelier_Warn");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = 1.9f;
            // Just shown, a third, two thirds, blinking, full as it lands, gone.
            cue.captureTimes = new[] { .1f, .55f, 1.05f, 1.35f, 1.58f, 1.7f };

            var decal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            decal.name = WarnPart;
            Object.DestroyImmediate(decal.GetComponent<Collider>());
            decal.transform.SetParent(root.transform, false);
            var view = decal.GetComponent<MeshRenderer>();
            var mat = VfxAssets.Material(WarnMaterial, WarnShader, out bool made);
            if (made) Look(mat);
            EditorUtility.SetDirty(mat);
            view.sharedMaterial = mat;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            view.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            var part = decal.AddComponent<VfxGroundWarn>();
            part.radius = 4.5f;
            part.lead = 1.6f;
            var tuned = old != null && old.transform.Find(WarnPart) is Transform t ? t.GetComponent<VfxGroundWarn>() : null;
            if (tuned != null) EditorUtility.CopySerialized(tuned, part);

            var prefab = VfxAssets.Prefab(WarnPrefab, root);
            Register("Chandelier_Warn", prefab);
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            Debug.Log("[VFX] Hazard W1 chandelier built: the falling chandelier's floor warning (Chandelier_Warn). Workbench: any piece, " + prefab.name);
        }

        [MenuItem("ChessFight/Development/VFX/Hazard/Build W1 chandelier (falling chandelier warning)", true)]
        static bool CanBuild() => !EditorApplication.isPlaying;

        static void Look(Material m)
        {
            m.SetFloat("_InnerRing", InnerRing);
            m.SetFloat("_InnerLight", InnerLight);
            m.SetFloat("_Ticks", Ticks);
            m.SetFloat("_Mark", Mark);
        }

        static void Register(string key, GameObject prefab)
        {
            var set = AssetDatabase.LoadAssetAtPath<VfxSet>(SetPath);
            if (set == null)
            {
                VfxAssets.Folder("Assets/Resources/VfxSets");
                set = ScriptableObject.CreateInstance<VfxSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }
            var entry = new VfxSet.Entry { key = key, prefab = prefab };
            int i = set.entries.FindIndex(e => e.key == key);
            if (i >= 0) set.entries[i] = entry;
            else set.entries.Add(entry);
            EditorUtility.SetDirty(set);
        }
    }
}
