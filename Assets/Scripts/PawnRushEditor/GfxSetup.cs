using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChessFight.PawnRush.Editor
{
    /// <summary>
    /// JY-gfx (2026-11-24, the graphics upgrade): how the upgrade's own files are imported, and the sky's material.
    ///   Resources/Gfx/Sets/*.png   the surface sets (ChessFight.Game.GameSurfaces): data, not colour; mipmapped,
    ///                              tiled, BC7, all alike (they are copied into texture arrays on the GPU)
    ///   Resources/Gfx/Height/*.png the floors' relief: R height, G ambient occlusion; data, mipmapped, tiled, BC5
    ///   Resources/Gfx/Sky/*.exr    the sky picture: HDR, no mips (read at mip 0), wraps round, BC6H
    ///   Resources/Gfx/GfxSky.mat   ChessFight/Gfx Sky with that picture (ChessFight.Game.GameSky)
    /// </summary>
    public sealed class GfxSetup : AssetPostprocessor
    {
        const string Root = "Assets/Resources/Gfx";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root + "/")) return;
            var importer = (TextureImporter)assetImporter;
            if (assetPath.StartsWith(Root + "/Sets/"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = true;
                importer.isReadable = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                foreach (var platform in new[] { "Standalone", "Windows Store Apps" })
                {
                    var settings = importer.GetPlatformTextureSettings(platform);
                    settings.overridden = true;
                    settings.maxTextureSize = 1024;
                    settings.format = TextureImporterFormat.BC7;
                    importer.SetPlatformTextureSettings(settings);
                }
            }
            else if (assetPath.StartsWith(Root + "/Height/"))
            {
                // The floors' relief (GameSurfaces gives a Gfx Lit twin the one named after its base map): R height,
                // G ambient occlusion, baked from the swatch scripts' geometry. Data; BC5 keeps the two channels apart.
                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = true;
                importer.isReadable = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                foreach (var platform in new[] { "Standalone", "Windows Store Apps" })
                {
                    var settings = importer.GetPlatformTextureSettings(platform);
                    settings.overridden = true;
                    settings.maxTextureSize = 1024;
                    settings.format = TextureImporterFormat.BC5;
                    importer.SetPlatformTextureSettings(settings);
                }
            }
            else if (assetPath.StartsWith(Root + "/Sky/"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                var settings = importer.GetPlatformTextureSettings("Standalone");
                settings.overridden = true;
                settings.maxTextureSize = 4096;
                settings.format = TextureImporterFormat.BC6H;
                importer.SetPlatformTextureSettings(settings);
            }
        }

        /// <summary>Makes (or refreshes) the sky's material. Batch: -executeMethod ChessFight.PawnRush.Editor.GfxSetup.Build</summary>
        [MenuItem("ChessFight/Development/Graphics/Set up the sky material")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            string path = Root + "/GfxSky.mat";
            var shader = Shader.Find("ChessFight/Gfx Sky");
            var picture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Sky/sky.exr");
            if (shader == null || picture == null)
            {
                Debug.LogError($"[GfxSetup] shader {(shader != null)}, sky picture {(picture != null)}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "GfxSky" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_Panorama", picture);
            material.SetFloat("_Exposure", 1f);
            EditorUtility.SetDirty(material);
            // The grass's look (GameGrass makes one per lawn from it; being in Resources keeps its shader in a build).
            var grassShader = Shader.Find("ChessFight/Gfx Grass");
            if (grassShader != null)
            {
                string grassPath = Root + "/GfxGrass.mat";
                var grass = AssetDatabase.LoadAssetAtPath<Material>(grassPath);
                if (grass == null)
                {
                    grass = new Material(grassShader) { name = "GfxGrass" };
                    AssetDatabase.CreateAsset(grass, grassPath);
                }
                grass.shader = grassShader;
                EditorUtility.SetDirty(grass);
            }
            else Debug.LogError("[GfxSetup] no ChessFight/Gfx Grass shader");
            AssetDatabase.SaveAssets();
            Debug.Log($"[GfxSetup] sky material {path}: {picture.width}x{picture.height} {picture.format}");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
