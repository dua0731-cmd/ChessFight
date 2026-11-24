using ChessFight.Game;
using UnityEditor;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    // The tutorial map's look (R834, palace drill yard, ArtSource/Tutorial): the Blender map over the grey box.
    // The grey boxes keep their colliders and only stop drawing; every number the game checks stays the builder's.
    //   Assets/Art/Tutorial/Models/TU_Map.fbx      TU_Course, TU_Castle, TU_Grounds (already at the game's coordinates)
    //   Assets/Art/Tutorial/Models/TU_Sweeper.fbx  the spinning bar's look, hung under the Spinning Bar
    //   Assets/Art/Tutorial/Materials/TU_*.mat     made here (URP Lit) from Assets/Art/Tutorial/Textures
    // Without the FBX the scene stays the grey box (a warning says so).
    public static partial class RagdollLabBuilder
    {
        const string TutorialArtDir = "Assets/Art/Tutorial";
        const string TutorialMapModel = TutorialArtDir + "/Models/TU_Map.fbx";
        const string TutorialSweeperModel = TutorialArtDir + "/Models/TU_Sweeper.fbx";
        const string TutorialMaterialDir = TutorialArtDir + "/Materials";
        const string TutorialTextureDir = TutorialArtDir + "/Textures";
        const string MiniGameMaterialDir = "Assets/Resources/PawnRushMiniGames/Materials";   // the reused flower pot and cypress

        // name, base map (null = plain colour), colour, smoothness, metallic, normal map
        static readonly (string name, string tex, Color color, float smooth, float metal, string normal)[] TutorialLooks =
        {
            ("TU_CreamBlock", "TU_CreamBlock_BaseColor", Color.white, .2f, 0f, null),
            ("TU_Paving", "TU_Paving_BaseColor", Color.white, .25f, 0f, null),
            ("TU_MarbleCream", "TU_MarbleCream_BaseColor", Color.white, .6f, 0f, null),
            ("TU_MarbleWalnut", "TU_MarbleWalnut_BaseColor", Color.white, .6f, 0f, null),
            ("TU_Walnut", "TU_Walnut_BaseColor", Color.white, .45f, 0f, null),
            ("TU_MoatFloor", "TU_MoatFloor_BaseColor", Color.white, .1f, 0f, null),
            ("TU_Gold", null, new Color(.93f, .66f, .12f), .65f, .6f, null),
            ("TU_Purple", null, new Color(.38f, .08f, .45f), .5f, 0f, null),
            ("TU_CreamPaint", null, new Color(.95f, .88f, .78f), .5f, 0f, null),
            ("TU_Grass", null, new Color(.27f, .48f, .2f), .1f, 0f, null),
            ("TU_Kit_CastleKit", "TU_Kit_CastleKit_BaseColor", Color.white, .3f, 0f, "TU_Kit_CastleKit_Normal"),
            ("TU_Kit_GateKit", "TU_Kit_GateKit_BaseColor", Color.white, .3f, 0f, "TU_Kit_GateKit_Normal"),
        };

        static readonly string[] ReusedLooks = { "PR_A_TripoProps", "PR_Early_TripoProps", "PR_Early_Soil", "PR_Early_Leaf", "PR_Early_Petal", "PR_Early_FlowerHeart" };

        /// <summary>Stops the lab map's boxes (all of <paramref name="env"/>) and the hurdles drawing, and puts the map's look in their place.</summary>
        static void DressTutorial(Transform env, Transform props)
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-tutorialNoDress") >= 0)
            {
                Debug.Log("[Tutorial] -tutorialNoDress: 회색 상자 그대로.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TutorialMapModel) == null)
            {
                Debug.LogWarning("[Tutorial] " + TutorialMapModel + "이 없어 회색 상자 그대로 둡니다 (ArtSource/Tutorial/tu_export_unity_v1.py).");
                return;
            }
            PrepareTutorialArt();
            var map = AssetDatabase.LoadAssetAtPath<GameObject>(TutorialMapModel);
            var sweeper = AssetDatabase.LoadAssetAtPath<GameObject>(TutorialSweeperModel);

            foreach (var r in env.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            foreach (var r in props.GetComponentsInChildren<MeshRenderer>(true))
                if (r.name.StartsWith("Hurdle")) r.enabled = false;
            var bar = env.GetComponentInChildren<SpinningBar>(true);

            var look = (GameObject)PrefabUtility.InstantiatePrefab(map);
            look.name = "Map look (ArtSource/Tutorial)";
            look.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var c in look.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            GameObjectUtility.SetStaticEditorFlags(look, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            if (bar != null && sweeper != null)
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(sweeper, bar.transform);
                s.name = "Sweeper look";
                s.transform.localPosition = Vector3.zero;
                s.transform.localRotation = Quaternion.identity;
                foreach (var c in s.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            }

            // The FBX is made at the game's coordinates: the dressed course spans the floors (x -40..35, z -37..32).
            var course = look.transform.Find("TU_Course");
            if (course != null && course.TryGetComponent<Renderer>(out var cr))
            {
                var b = cr.bounds;
                bool ok = Mathf.Abs(b.min.x + 40f) < 1.5f && Mathf.Abs(b.max.x - 35f) < 1.5f && Mathf.Abs(b.min.z + 37f) < 1.5f && Mathf.Abs(b.max.z - 32f) < 2f;
                if (ok) Debug.Log($"[Tutorial] 맵 겉모습 위치 맞음: {b.min} ~ {b.max}");
                else Debug.LogError($"[Tutorial] 맵 겉모습이 판정 상자와 어긋남(뒤집힘?): {b.min} ~ {b.max}");
            }
        }

        static void PrepareTutorialArt()
        {
            EnsureTutorialFolder(TutorialMaterialDir);
            foreach (var look in TutorialLooks)
            {
                if (look.normal != null) SetTextureKind(look.normal, true);
                if (look.tex != null) SetTextureKind(look.tex, false);
            }

            var remap = new System.Collections.Generic.Dictionary<string, Material>();
            foreach (var look in TutorialLooks)
            {
                string path = $"{TutorialMaterialDir}/{look.name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(LitMaterials.Lit);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = LitMaterials.Lit;
                mat.color = look.color;
                mat.mainTexture = look.tex == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{TutorialTextureDir}/{look.tex}.png");
                if (look.tex != null && mat.mainTexture == null) Debug.LogWarning("[Tutorial] 텍스처 없음: " + look.tex);
                LitMaterials.SetSmoothness(mat, look.smooth);
                LitMaterials.SetMetallic(mat, look.metal);
                var normal = look.normal == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{TutorialTextureDir}/{look.normal}.png");
                if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", normal);
                if (normal != null) mat.EnableKeyword("_NORMALMAP"); else mat.DisableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(mat);
                remap[look.name] = mat;
            }
            foreach (var name in ReusedLooks)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MiniGameMaterialDir}/{name}.mat");
                if (mat != null) remap[name] = mat;
                else Debug.LogWarning("[Tutorial] 재사용 재질 없음: " + name);
            }
            AssetDatabase.SaveAssets();

            foreach (var model in new[] { TutorialMapModel, TutorialSweeperModel })
            {
                if (!(AssetImporter.GetAtPath(model) is ModelImporter mi)) continue;
                bool changed = false;
                if (mi.importCameras || mi.importLights || mi.addCollider || mi.importAnimation || mi.isReadable)
                {
                    mi.importCameras = false; mi.importLights = false; mi.addCollider = false; mi.importAnimation = false; mi.isReadable = false;
                    changed = true;
                }
                if (mi.indexFormat != ModelImporterIndexFormat.UInt32) { mi.indexFormat = ModelImporterIndexFormat.UInt32; changed = true; }
                var have = mi.GetExternalObjectMap();
                foreach (var kv in remap)
                {
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key);
                    if (have.TryGetValue(id, out var cur) && cur == kv.Value) continue;
                    mi.AddRemap(id, kv.Value); changed = true;
                }
                if (changed) mi.SaveAndReimport();
            }
        }

        static void SetTextureKind(string name, bool normal)
        {
            string path = $"{TutorialTextureDir}/{name}.png";
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) { Debug.LogWarning("[Tutorial] 텍스처 없음: " + path); return; }
            var want = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (ti.textureType == want && ti.maxTextureSize == 2048) return;
            ti.textureType = want;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
        }

        static void EnsureTutorialFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureTutorialFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
