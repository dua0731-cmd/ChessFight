using System.Collections.Generic;
using System.Text;
using ChessFight.Game;
using UnityEditor;
using UnityEngine;

namespace ChessFight.PawnRush.Editor
{
    /// <summary>
    /// JY-gfx (2026-11-24, the graphics upgrade): the URP Lit material files on ChessFight/Gfx Lit for good, for a
    /// build. In Play, GameSurfaces gives URP Lit materials Gfx Lit twins (the editor finds the shader then); a build
    /// only has Gfx Lit, with the variants its materials use, if material files use it. These menus change the files
    /// themselves, the set picked the same way (GameSurfaces.Pick, from the name): everything else on the material
    /// stays (Gfx Lit is URP Lit with two more numbers and the same inspector). Materials that wear no set are left
    /// on URP Lit. "Back" puts every Gfx Lit material file on URP Lit again. Nothing runs by itself.
    /// </summary>
    public static class GfxConvert
    {
        const string LitName = "Universal Render Pipeline/Lit", GfxName = "ChessFight/Gfx Lit";

        [MenuItem("ChessFight/Graphics/URP Lit materials -> Gfx Lit (for a build)")]
        public static void ToGfx()
        {
            var lit = Shader.Find(LitName);
            var gfx = Shader.Find(GfxName);
            if (lit == null || gfx == null)
            {
                Debug.LogError($"[GfxConvert] shaders: URP Lit {(lit != null)}, Gfx Lit {(gfx != null)}");
                return;
            }
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m != null && m.shader == lit && GameSurfaces.Pick(m, out _) >= 0) paths.Add(path);
            }
            if (paths.Count == 0 || !EditorUtility.DisplayDialog("Gfx Lit",
                    $"{paths.Count} URP Lit material files will be put on ChessFight/Gfx Lit, each with its surface set " +
                    "(their files change; \"Gfx Lit materials -> URP Lit\" puts them back).", "Change them", "Cancel"))
                return;
            var log = new StringBuilder();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in paths)
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                    int set = GameSurfaces.Pick(m, out float scale);
                    int queue = m.renderQueue;
                    var keywords = m.shaderKeywords;
                    Undo.RecordObject(m, "Gfx Lit");
                    m.shader = gfx;
                    m.shaderKeywords = keywords;
                    m.renderQueue = queue;
                    m.SetFloat("_GfxSet", set);
                    m.SetFloat("_GfxSetScale", scale);
                    EditorUtility.SetDirty(m);
                    log.AppendLine($"{GameSurfaces.Sets[set],-10} {scale:0.00}  {path}");
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[GfxConvert] {paths.Count} materials on Gfx Lit:\n{log}");
        }

        [MenuItem("ChessFight/Graphics/Gfx Lit materials -> URP Lit")]
        public static void ToLit()
        {
            var lit = Shader.Find(LitName);
            var gfx = Shader.Find(GfxName);
            if (lit == null || gfx == null) return;
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null || m.shader != gfx) continue;
                int queue = m.renderQueue;
                var keywords = m.shaderKeywords;
                Undo.RecordObject(m, "URP Lit");
                m.shader = lit;
                m.shaderKeywords = keywords;
                m.renderQueue = queue;
                EditorUtility.SetDirty(m);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[GfxConvert] {count} materials back on URP Lit");
        }
    }
}
