using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChessFight.VFX.Editor
{
    /// <summary>
    /// What the VFX builders share (R168, 10-27, DECISIONS FX7): saving generated textures, meshes, materials and prefabs
    /// under Assets/VFX, replacing what a previous build saved at the same path so references to it hold.
    /// </summary>
    public static class VfxAssets
    {
        public const string Root = "Assets/VFX";

        public static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>A grey mask (0..1 per pixel, row by row from the bottom) saved as a PNG: linear, clamped, with mips.</summary>
        public static Texture2D Mask(string path, int size, float[] values)
        {
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte v = (byte)Mathf.RoundToInt(Mathf.Clamp01(values[i]) * 255f);
                pixels[i] = new Color32(v, v, v, 255);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Saves a mesh, or copies it over the one already at the path.</summary>
        public static Mesh SaveMesh(string path, Mesh mesh)
        {
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, old);
            old.name = Path.GetFileNameWithoutExtension(path);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(old);
            return old;
        }

        /// <summary>The material at the path on <paramref name="shader"/>, made if missing; its values are left as tuned.</summary>
        public static Material Material(string path, string shader, out bool made)
        {
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            made = mat == null;
            if (made)
            {
                mat = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader == null || mat.shader.name != shader) mat.shader = Shader.Find(shader);
            return mat;
        }

        /// <summary>A flat 1 x 1 m quad on the ground (XZ), facing up, UVs 0..1.</summary>
        public static Mesh FloorQuad()
        {
            var mesh = new Mesh { name = "VFX_FloorQuad" };
            mesh.vertices = new[] { new Vector3(-.5f, 0f, -.5f), new Vector3(.5f, 0f, -.5f), new Vector3(-.5f, 0f, .5f), new Vector3(.5f, 0f, .5f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Saves <paramref name="root"/> as the prefab at the path (replacing it, so its GUID holds) and removes it from the scene.</summary>
        public static GameObject Prefab(string path, GameObject root)
        {
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
