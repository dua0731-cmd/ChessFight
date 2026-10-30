using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChessFight.PawnRush.Editor
{
    // ChessFight > Pawn Rush > Endgame: Import Blender Models (R255): copies what
    // ArtSource/PawnRush/Endgame/build_endgame_v1.py exported (the part FBX files and PR_EG_manifest.json) and what
    // make_textures.py made (Textures/) into Assets/Art/PawnRushMap/Endgame, makes one URP Lit material per EG_ name
    // with its texture set (ArtSource/PawnRush/Endgame/README.md "Unity로 넘기기") and points the FBX materials at them.
    // The war room build (Course01v4Builder.WarRoomModels) then loads the models from Resources/PawnRushEndgame.
    // Run it again after building the Blender files again; the test scene picks the models up on its next build.
    public static class EndgameModelImport
    {
        public const string Folder = "Assets/Art/PawnRushMap/Endgame";
        public const string Models = Folder + "/Resources/PawnRushEndgame";
        const string Textures = Folder + "/Textures", Materials = Folder + "/Materials";
        static string Source => Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtSource/PawnRush/Endgame"));

        // 2048 sets tile once per UV (1 UV = 4 m), the 1024 sets twice: all 512 px/m.
        static readonly Dictionary<string, float> Tiling = new()
        {
            { "EG_Marble_Cream", 1f }, { "EG_MapPaper", 1f }, { "EG_Trim", 1f },
            { "EG_Gold", 2f }, { "EG_Teal", 2f }, { "EG_Walnut", 2f }, { "EG_Stone", 2f }, { "EG_Iron", 2f }, { "EG_BannerNavy", 2f },
            { "EG_Lamp_Emissive", 1f },
        };

        [MenuItem("ChessFight/Pawn Rush/Endgame: Import Blender Models (R255)", false, 30)]
        public static void Import()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(Path.Combine(Source, "PR_EG_manifest.json")) || !Directory.Exists(Path.Combine(Source, "Textures")))
            {
                EditorUtility.DisplayDialog("Endgame", "ArtSource/PawnRush/Endgame에 FBX · 텍스처가 없습니다.\n" +
                    "그 폴더에서 python make_textures.py, 그다음 Blender로 build_endgame_v1.py ... --export --parts . 를 먼저 실행하세요.", "확인");
                return;
            }
            int fbx = 0;
            foreach (var dir in Directory.GetDirectories(Source, "PR_EG_*"))
            {
                string part = Path.GetFileName(dir), file = Path.Combine(dir, part + ".fbx");
                if (!File.Exists(file)) continue;
                Copy(file, $"{Models}/{part}/{part}.fbx");
                fbx++;
            }
            Copy(Path.Combine(Source, "PR_EG_manifest.json"), Models + "/PR_EG_manifest.json");
            foreach (var png in Directory.GetFiles(Path.Combine(Source, "Textures"), "*.png"))
                Copy(png, Textures + "/" + Path.GetFileName(png));
            AssetDatabase.Refresh();

            var mats = MakeMaterials();
            int remapped = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Models }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;
                bool changed = false;
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(sub is Material m)) continue;
                    string name = m.name.Split('.')[0];
                    if (!mats.TryGetValue(name, out var mat)) continue;
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), mat);
                    changed = true;
                }
                if (changed) { importer.SaveAndReimport(); remapped++; }
            }
            Debug.Log($"[PawnRush] 엔드게임 모델 가져오기: FBX {fbx}개, 재질 {mats.Count}개, 재질 연결 {remapped}개 → {Folder}. " +
                      "시험 씬은 ChessFight > SeoungTest > Build Pawn Rush Test Scene으로 다시 지으면 모델이 붙습니다.");
        }

        static void Copy(string from, string assetPath)
        {
            string to = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            File.Copy(from, to, true);
        }

        // One URP Lit material per texture set (T_<name>_BaseColor ...); EG_Books and EG_Window_Emissive have none
        // and keep the FBX's own colour.
        static Dictionary<string, Material> MakeMaterials()
        {
            var made = new Dictionary<string, Material>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder(Folder, "Materials");
            foreach (var pair in Tiling)
            {
                string name = pair.Key, path = $"{Materials}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader) { name = name };
                    AssetDatabase.CreateAsset(mat, path);
                }
                var tile = new Vector2(pair.Value, pair.Value);
                var baseMap = Tex(name, "BaseColor");
                mat.SetTexture("_BaseMap", baseMap);
                mat.SetTextureScale("_BaseMap", tile);
                mat.SetColor("_BaseColor", Color.white);
                var metal = Tex(name, "MetallicSmoothness");
                mat.SetTexture("_MetallicGlossMap", metal);
                mat.SetFloat("_Smoothness", 1f);
                SetKeyword(mat, "_METALLICSPECGLOSSMAP", metal != null);
                var normal = Tex(name, "Normal");
                mat.SetTexture("_BumpMap", normal);
                SetKeyword(mat, "_NORMALMAP", normal != null);
                var occlusion = Tex(name, "Occlusion");
                mat.SetTexture("_OcclusionMap", occlusion);
                SetKeyword(mat, "_OCCLUSIONMAP", occlusion != null);
                if (name == "EG_Lamp_Emissive")
                {
                    var glow = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/T_EG_Lamp_Emission.png");
                    mat.SetTexture("_EmissionMap", glow);
                    mat.SetColor("_EmissionColor", Color.white * 2f);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    SetKeyword(mat, "_EMISSION", true);
                }
                EditorUtility.SetDirty(mat);
                made[name] = mat;
            }
            AssetDatabase.SaveAssets();
            return made;
        }

        static Texture2D Tex(string material, string channel) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{Textures}/T_{material}_{channel}.png");

        static void SetKeyword(Material m, string keyword, bool on)
        {
            if (on) m.EnableKeyword(keyword);
            else m.DisableKeyword(keyword);
        }

        // Import settings for the copied files: normal maps as normal maps, the data maps linear, the FBX without
        // cameras, lights, animation or colliders (the war room's grey boxes keep the colliders).
        sealed class Settings : AssetPostprocessor
        {
            void OnPreprocessTexture()
            {
                if (!assetPath.StartsWith(Textures + "/")) return;
                var t = (TextureImporter)assetImporter;
                if (assetPath.EndsWith("_Normal.png")) t.textureType = TextureImporterType.NormalMap;
                else if (assetPath.EndsWith("_MetallicSmoothness.png") || assetPath.EndsWith("_Occlusion.png")) t.sRGBTexture = false;
            }

            void OnPreprocessModel()
            {
                if (!assetPath.StartsWith(Models + "/")) return;
                var m = (ModelImporter)assetImporter;
                m.importCameras = false;
                m.importLights = false;
                m.importAnimation = false;
                m.animationType = ModelImporterAnimationType.None;
                m.addCollider = false;
            }
        }
    }
}
