using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Real models over the procedural props (R155, 승규 님 10-22: the king's sceptre and crown made with Tripo, polished in
    /// Blender like the bishop's mitre of R154): Resources/QueenHillSkillFx/KingModels. A model rides on its procedural prop
    /// (a child of the prop's transform), so the prop's motion is the model's; the prop draws itself instead while it flashes
    /// white, and wherever a model is missing.
    /// </summary>
    public partial class QueenHillSkillFx
    {
        static readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Material> modelMats = new Dictionary<string, Material>();

        /// <summary>The model at Resources/QueenHillSkillFx/<paramref name="path"/>, or null.</summary>
        static GameObject LoadModel(string path)
        {
            if (!models.TryGetValue(path, out var m))
            {
                m = Resources.Load<GameObject>("QueenHillSkillFx/" + path);
                if (m == null) Debug.LogWarning("[QueenHillSkillFx] No model at Resources/QueenHillSkillFx/" + path);
                models[path] = m;
            }
            return m;
        }

        /// <summary>The polished material a model's own (from Blender) stands for: gold, silver and gem as the mitre's, ebony
        /// and ivory as the king's.</summary>
        static Material ModelMaterial(Material from)
        {
            if (from == null) return null;
            string n = from.name, path =
                n.Contains("Gold") ? "MitreModel/Mitre_Gold" :
                n.Contains("Silver") ? "MitreModel/Mitre_Silver" :
                n.Contains("Gem") ? "MitreModel/Mitre_Gem" :
                n.Contains("Ebony") ? "KingModels/King_Ebony" :
                n.Contains("Ivory") ? "KingModels/King_Ivory" : null;
            if (path == null) return from;
            if (!modelMats.TryGetValue(path, out var m))
            {
                m = Resources.Load<Material>("QueenHillSkillFx/" + path);
                modelMats[path] = m;
            }
            return m != null ? m : from;
        }

        /// <summary>A copy of <paramref name="model"/> on <paramref name="on"/> (its foot at the prop's origin, its +y along
        /// the prop's), scaled by <paramref name="scale"/>, hidden until shown; null without a model.</summary>
        static GameObject ModelOn(GameObject model, Transform on, float scale)
        {
            if (model == null) return null;
            var root = new GameObject(model.name);
            root.transform.SetParent(on, false);
            root.transform.localScale = Vector3.one * scale;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var go = new GameObject(mf.name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = mf.transform.localPosition;
                go.transform.localRotation = mf.transform.localRotation;
                go.transform.localScale = mf.transform.localScale;
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var src = mf.GetComponent<MeshRenderer>().sharedMaterials;
                var mats = new Material[src.Length];
                for (int i = 0; i < src.Length; i++) mats[i] = ModelMaterial(src[i]);
                go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            }
            root.SetActive(false);
            return root;
        }

        /// <summary>The model drawn (and <paramref name="props"/> hidden) unless it flashes white or there is none.</summary>
        static void ShowModel(GameObject model, bool on, float flash, params Prop[] props)
        {
            bool real = model != null && flash < 0.5f;
            if (model != null) model.SetActive(on && real);
            foreach (var p in props) p.Show(on && !real);
        }
    }
}
