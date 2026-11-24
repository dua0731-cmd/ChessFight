using System.Collections.Generic;
using System.Text;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    /// <summary>
    /// JY-gfx (2026-11-24): one section's baked indirect light. JY, after the bake test: "넣자. 저 사진만 봐도 그래픽 퀄리티
    /// 훨씬 좋고, 라이팅까지 제대로 하면 진짜 예쁘게 나오겠어". A round places its sections as prefabs, and they build much of
    /// their art at run time, so no scene bake ever reaches them. GfxBake (editor) bakes each section as the game built
    /// and dressed it (GfxShots -gfxFreeze: its models, the upgrade's materials, its light plan, the corridors round it)
    /// and keeps here the lightmaps, the meshes with the second UV set the bake laid them out by, and which part of the
    /// section gets which (by its path under the section). GfxBaked puts them on a section in a round.
    /// Resources/Gfx/Baked/&lt;section&gt;.asset, the section's prefab name without its zone ("MG_G_RoyalProcession").
    /// </summary>
    public sealed class GfxBakedLight : ScriptableObject
    {
        [System.Serializable]
        public struct Part
        {
            /// <summary>Under the section's root: "name~k/name~k" (k: which of the siblings of that name).</summary>
            public string path;
            /// <summary>The mesh the game builds there (its name and vertex count, a check before anything changes).</summary>
            public string mesh;
            public int vertices;
            /// <summary>That mesh with the bake's second UV set (null: the part's own mesh had one).</summary>
            public Mesh baked;
            public int lightmap;
            public Vector4 scaleOffset;
        }

        public string section;
        public Texture2D[] lightmaps;
        public Part[] parts;
        /// <summary>How it was baked (texels a metre, bounces, samples, when), for the notes.</summary>
        public string how;
    }

    /// <summary>
    /// JY-gfx: puts a section's baked light (GfxBakedLight) on it while the upgrade is on, and takes it off. The scene's own
    /// lightmaps (if any) stay first in LightmapSettings; each placed section's follow, re-indexed whenever one comes or goes.
    /// A part is changed only when its mesh is the one the bake saw (name and vertex count); any other keeps the live light.
    /// </summary>
    public static class GfxBaked
    {
        sealed class Placed
        {
            public Transform root;
            public GfxBakedLight data;
            public readonly List<(Renderer renderer, MeshFilter filter, Mesh original, Mesh baked, int lightmap, Vector4 scaleOffset)> parts =
                new List<(Renderer, MeshFilter, Mesh, Mesh, int, Vector4)>();
            public int first;
        }

        static readonly List<Placed> placed = new List<Placed>();
        static LightmapData[] sceneOwn;
        public static string Last = "";
        /// <summary>Off: no section gets its baked light (the picture tool's bake=0, to compare).</summary>
        public static bool Enabled = true;

        /// <summary>The section's prefab name without the round's slot number and its zone: the asset's name.</summary>
        public static string Key(string sectionName)
        {
            string n = sectionName;
            int space = n.IndexOf(' ');
            if (space > 0 && int.TryParse(n.Substring(0, space), out _)) n = n.Substring(space + 1);
            foreach (var zone in new[] { "Early_", "Mid_", "Late_", "Final_" })
                if (n.StartsWith(zone)) return n.Substring(zone.Length);
            return n;
        }

        public static GfxBakedLight Load(string sectionName) => Resources.Load<GfxBakedLight>("Gfx/Baked/" + Key(sectionName));

        public static bool IsOn(Transform section)
        {
            foreach (var p in placed) if (p.root == section) return true;
            return false;
        }

        /// <summary>The section's baked light on (true) or off.</summary>
        public static void Set(Transform section, bool on)
        {
            if (section == null) return;
            on &= Enabled;
            if (on == IsOn(section)) return;
            if (on) Apply(section);
            else Remove(section);
        }

        static void Apply(Transform section)
        {
            var data = Load(section.name);
            if (data == null || data.lightmaps == null || data.lightmaps.Length == 0) return;
            var where = Paths(section);
            var p = new Placed { root = section, data = data };
            int missing = 0, other = 0;
            foreach (var part in data.parts)
            {
                if (!where.TryGetValue(part.path, out var t)) { missing++; continue; }
                var renderer = t.GetComponent<MeshRenderer>();
                var filter = t.GetComponent<MeshFilter>();
                if (renderer == null || filter == null || filter.sharedMesh == null) { missing++; continue; }
                var mesh = filter.sharedMesh;
                if (mesh.vertexCount != part.vertices || Plain(mesh.name) != Plain(part.mesh)) { other++; continue; }
                if (part.baked != null && part.baked.vertexCount != mesh.vertexCount) { other++; continue; }
                p.parts.Add((renderer, filter, mesh, part.baked, part.lightmap, part.scaleOffset));
            }
            if (sceneOwn == null && placed.Count == 0) sceneOwn = LightmapSettings.lightmaps;
            placed.Add(p);
            Rebuild();
            Last = $"{Key(section.name)}: baked light on {p.parts.Count} of {data.parts.Length} parts ({missing} not found, {other} with another mesh)";
        }

        static void Remove(Transform section)
        {
            for (int i = placed.Count - 1; i >= 0; i--)
            {
                var p = placed[i];
                if (p.root != section) continue;
                foreach (var (renderer, filter, original, _, _, _) in p.parts)
                {
                    if (filter != null && original != null) filter.sharedMesh = original;
                    if (renderer != null) renderer.lightmapIndex = -1;
                }
                placed.RemoveAt(i);
            }
            Rebuild();
        }

        /// <summary>Sections gone with their scene or destroyed: their share of the lightmaps given back.</summary>
        public static void Tidy()
        {
            int before = placed.Count;
            placed.RemoveAll(p => p.root == null);
            if (placed.Count != before) Rebuild();
        }

        static void Rebuild()
        {
            var list = new List<LightmapData>();
            if (sceneOwn != null) list.AddRange(sceneOwn);
            foreach (var p in placed)
            {
                p.first = list.Count;
                foreach (var t in p.data.lightmaps) list.Add(new LightmapData { lightmapColor = t });
            }
            if (placed.Count == 0)
            {
                LightmapSettings.lightmaps = sceneOwn ?? new LightmapData[0];
                sceneOwn = null;
                return;
            }
            LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
            LightmapSettings.lightmaps = list.ToArray();
            foreach (var p in placed)
                foreach (var (renderer, filter, _, baked, lightmap, scaleOffset) in p.parts)
                {
                    if (renderer == null || filter == null) continue;
                    if (baked != null) filter.sharedMesh = baked;
                    renderer.lightmapIndex = p.first + lightmap;
                    renderer.lightmapScaleOffset = scaleOffset;
                }
        }

        static string Plain(string name) => (name ?? "").Replace(" (Instance)", "").Replace(" (uv2)", "").Trim();

        /// <summary>Every transform under the section by its path: "name~k/name~k", k counting the siblings of that name.</summary>
        public static Dictionary<string, Transform> Paths(Transform root)
        {
            var all = new Dictionary<string, Transform>();
            Walk(root, "", all);
            return all;
        }

        static void Walk(Transform t, string prefix, Dictionary<string, Transform> all)
        {
            var seen = new Dictionary<string, int>();
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                seen.TryGetValue(c.name, out int k);
                seen[c.name] = k + 1;
                string path = prefix.Length == 0 ? c.name + "~" + k : prefix + "/" + c.name + "~" + k;
                all[path] = c;
                Walk(c, path, all);
            }
        }

        /// <summary>A part's path under the section (as Paths names it).</summary>
        public static string PathOf(Transform root, Transform part)
        {
            var names = new List<string>();
            for (var t = part; t != null && t != root; t = t.parent)
            {
                int k = 0;
                var parent = t.parent;
                if (parent != null)
                    for (int i = 0; i < parent.childCount; i++)
                    {
                        var s = parent.GetChild(i);
                        if (s == t) break;
                        if (s.name == t.name) k++;
                    }
                names.Add(t.name + "~" + k);
            }
            names.Reverse();
            return string.Join("/", names);
        }

        public static string Notes()
        {
            var sb = new StringBuilder();
            foreach (var p in placed) sb.Append(Key(p.root != null ? p.root.name : "?")).Append(' ').Append(p.parts.Count).Append(" parts; ");
            return sb.ToString();
        }
    }
}
