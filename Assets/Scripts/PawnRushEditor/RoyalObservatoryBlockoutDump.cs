using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ChessFight.PawnRush.Editor
{
    // The Royal Observatory's graybox written out for Blender (R725 modelling, ArtSource/PawnRush/RoyalObservatory/PLAN.md R0).
    // The section prefab is an empty shell; RoyalObservatoryBuilder builds the graybox from code when it wakes. This
    // builds it once in edit mode (the prefab's contents loaded apart, never saved), walks what was built and writes
    //   ArtSource/PawnRush/RoyalObservatory/ro_blockout.json
    // one entry an object: name, path, parent, active, its matrix in the section's frame (Unity: +z north, y up,
    // metres, the entry's threshold at 0), what it draws (Unity's cube / cylinder / sphere, or a built mesh written
    // once under "meshes"), its material, its collider and its components. ro_blockout.py stands it up in Blender and
    // ro_kit.frame() reads the device frames from it. Moving devices are written at rest (the clock not applied).
    // Written again by itself after a compile when the builder is newer than the file (not in Play, not in batch
    // runs), or from ChessFight > Pawn Rush > Royal Observatory > Dump Blockout For Blender.
    [InitializeOnLoad]
    static class RoyalObservatoryBlockoutDump
    {
        const string Prefab = "Assets/Prefabs/PawnRushSections/" + RoyalObservatorySection.Id + ".prefab";
        const string Builder = "Assets/Scripts/PawnRush/RoyalObservatoryBuilder.cs";
        const string Out = "ArtSource/PawnRush/RoyalObservatory/ro_blockout.json";

        static RoyalObservatoryBlockoutDump()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var root = Directory.GetCurrentDirectory();
                string json = Path.Combine(root, Out), builder = Path.Combine(root, Builder);
                if (!File.Exists(builder)) return;
                if (File.Exists(json) && File.GetLastWriteTimeUtc(json) >= File.GetLastWriteTimeUtc(builder)) return;
                Dump();
            };
        }

        [MenuItem("ChessFight/Pawn Rush/Royal Observatory/Dump Blockout For Blender")]
        static void Dump()
        {
            var shell = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var observatory = shell.GetComponent<RoyalObservatorySection>();
                var built = observatory != null && !observatory.HasContent ? RoyalObservatoryBuilder.Build(observatory) : null;
                if (built == null)
                {
                    Debug.LogWarning("[천문대 블록아웃] " + Prefab + "에서 회색 상자를 짓지 못해 덤프하지 않았습니다.");
                    return;
                }
                var text = Write(shell.transform);
                var path = Path.Combine(Directory.GetCurrentDirectory(), Out);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text, new UTF8Encoding(false));
                Debug.Log("[천문대 블록아웃] " + Out + " 를 썼습니다 (Blender ro_blockout.py가 읽음).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(shell);
            }
        }

        static string F(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);

        static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        static string Write(Transform section)
        {
            var nodes = new List<Transform>();
            var index = new Dictionary<Transform, int>();
            var meshes = new List<Mesh>();
            var meshIndex = new Dictionary<Mesh, int>();
            void Walk(Transform t)
            {
                index[t] = nodes.Count;
                nodes.Add(t);
                foreach (Transform c in t) Walk(c);
            }
            foreach (Transform c in section) Walk(c);

            var toSection = section.worldToLocalMatrix;
            var sb = new StringBuilder();
            sb.Append("{\n \"section\": \"").Append(RoyalObservatorySection.Id).Append("\",\n \"frame\": \"Unity section-local: x east, y up, z north, metres\",\n \"nodes\": [\n");
            for (int i = 0; i < nodes.Count; i++)
            {
                var t = nodes[i];
                var m = toSection * t.localToWorldMatrix;
                var path = new StringBuilder(t.name);
                for (var p = t.parent; p != null && p != section; p = p.parent) path.Insert(0, p.name + "/");
                sb.Append("  {\"name\": \"").Append(Esc(t.name)).Append("\", \"path\": \"").Append(Esc(path.ToString())).Append('"');
                sb.Append(", \"parent\": ").Append(t.parent != null && index.TryGetValue(t.parent, out int pi) ? pi : -1);
                sb.Append(", \"active\": ").Append(t.gameObject.activeSelf ? "true" : "false");
                sb.Append(", \"local\": [").Append(F(t.localPosition.x)).Append(',').Append(F(t.localPosition.y)).Append(',').Append(F(t.localPosition.z))
                  .Append(',').Append(F(t.localRotation.x)).Append(',').Append(F(t.localRotation.y)).Append(',').Append(F(t.localRotation.z)).Append(',').Append(F(t.localRotation.w))
                  .Append(',').Append(F(t.localScale.x)).Append(',').Append(F(t.localScale.y)).Append(',').Append(F(t.localScale.z)).Append(']');
                sb.Append(", \"matrix\": [");
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 4; c++)
                        sb.Append(r + c > 0 ? "," : "").Append(F(m[r, c]));
                sb.Append(']');
                var filter = t.GetComponent<MeshFilter>();
                var renderer = t.GetComponent<MeshRenderer>();
                if (filter != null && filter.sharedMesh != null)
                {
                    var mesh = filter.sharedMesh;
                    string kind = mesh.name == "Cube" ? "cube" : mesh.name == "Cylinder" ? "cylinder" : mesh.name == "Sphere" ? "sphere" : null;
                    if (kind == null)
                    {
                        if (!meshIndex.TryGetValue(mesh, out int mi)) { mi = meshes.Count; meshes.Add(mesh); meshIndex[mesh] = mi; }
                        kind = "mesh:" + mi;
                    }
                    sb.Append(", \"draws\": \"").Append(kind).Append('"');
                    if (renderer != null)
                    {
                        var mat = renderer.sharedMaterial;
                        sb.Append(", \"material\": \"").Append(mat != null ? Esc(mat.name) : "").Append('"');
                        if (mat != null && mat.HasProperty("_BaseColor"))
                        {
                            var col = mat.GetColor("_BaseColor");
                            sb.Append(", \"colour\": [").Append(F(col.r)).Append(',').Append(F(col.g)).Append(',').Append(F(col.b)).Append(',').Append(F(col.a)).Append(']');
                        }
                        var bounds = renderer.bounds;   // world AABB; the section prefab sits at the origin while loaded apart
                        sb.Append(", \"bounds\": [").Append(F(bounds.min.x)).Append(',').Append(F(bounds.min.y)).Append(',').Append(F(bounds.min.z))
                          .Append(',').Append(F(bounds.max.x)).Append(',').Append(F(bounds.max.y)).Append(',').Append(F(bounds.max.z)).Append(']');
                    }
                }
                var collider = t.GetComponent<Collider>();
                if (collider != null)
                    sb.Append(", \"collider\": \"").Append(collider.GetType().Name).Append(collider.isTrigger ? " trigger" : "").Append('"');
                var kinds = new List<string>();
                foreach (var comp in t.GetComponents<Component>())
                {
                    if (comp == null || comp is Transform || comp is MeshFilter || comp is MeshRenderer || comp is Collider) continue;
                    kinds.Add(comp.GetType().Name);
                }
                if (kinds.Count > 0) sb.Append(", \"components\": [\"").Append(string.Join("\", \"", kinds)).Append("\"]");
                sb.Append(i + 1 < nodes.Count ? "},\n" : "}\n");
            }
            sb.Append(" ],\n \"meshes\": [\n");
            for (int i = 0; i < meshes.Count; i++)
            {
                var mesh = meshes[i];
                sb.Append("  {\"name\": \"").Append(Esc(mesh.name)).Append("\", \"vertices\": [");
                var v = mesh.vertices;
                for (int k = 0; k < v.Length; k++)
                    sb.Append(k > 0 ? "," : "").Append(F(v[k].x)).Append(',').Append(F(v[k].y)).Append(',').Append(F(v[k].z));
                sb.Append("], \"triangles\": [");
                var tris = mesh.triangles;
                for (int k = 0; k < tris.Length; k++) sb.Append(k > 0 ? "," : "").Append(tris[k]);
                sb.Append(i + 1 < meshes.Count ? "]},\n" : "]}\n");
            }
            sb.Append(" ]\n}\n");
            return sb.ToString();
        }
    }
}
