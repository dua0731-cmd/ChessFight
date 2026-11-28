using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // Royal Library course (seoungtest R687, ArtSource/PawnRush/RoyalLibraryModels/PLAN.md R0): the Blender models laid
    // over WarRoomComplete's blockout in Play, the carriage yard's way (RoyalCarriageYard.Dressing.cs). Every position,
    // collider, pivot and timing stays the prefab's: a covered blockout piece only stops drawing.
    //
    // Each zone adds its models in its own partial file (RoyalLibraryDressing.<Zone>.cs) as a static Hang[] field; this
    // file finds them all. A Hang names an FBX under Resources/RoyalLibrary/Models/<zone>/ (rl_kit.export), the prefab
    // object it hangs on (null = the course root; else every object of that name, e.g. both books' covers or all 23
    // keys - the FBX was written in that object's frame, so it follows the piece as it moves) and the blockout pieces it
    // covers (a name, or a name's start then '*'; searched under `under`, or the whole course).
    //
    // Materials: each FBX slot "RL | <slot>" (Unity imports it as "RL _ <slot>") is drawn in the library material
    // Resources/RoyalLibrary/Materials/RL_<Name> (rl_textures.py / rl_unity_materials.py, seamless tiles in metres); an
    // unknown slot keeps the FBX's own material, so a gap shows.
    public sealed partial class RoyalLibraryDressing : MonoBehaviour
    {
        const string ModelFolder = "RoyalLibrary/Models/";
        const string MaterialFolder = "RoyalLibrary/Materials/";

        [Tooltip("Lay the Blender models over the blockout in Play (off: the blockout as pr/2 built it).")]
        [SerializeField] bool dress = true;
        [Tooltip("Draw the models in the baked library materials (off: their own Blender colours).")]
        [SerializeField] bool bakedMaterials = true;

        public sealed class Hang
        {
            public string zone, model;
            public string on;          // prefab object the model rides (null: the course root)
            // Which one of the objects named `on` (R5 11-18, the two door leaves share a name): -1 every one, else the
            // index-th in prefab order (depth first, children in order - the same count as rl_kit.frame(name, index)).
            public int index = -1;
            public string under;       // where `covers` is looked for (null: the whole course)
            public string[] covers = Array.Empty<string>();
            // R3 11-18: keep the host's own renderer (the game recolours it with a property block - a wrong typewriter
            // key) and only swap its mesh and materials for the FBX's first mesh, baked into the host's frame.
            public bool reskin;
        }

        // R2 11-18: anything a zone must do once the models hang (a ribbon that follows the blockout's warning colour):
        // a static Action<RoyalLibraryDressing> field in any partial file is called after dressing, in no set order.
        static IEnumerable<Action<RoyalLibraryDressing>> AfterDressActions()
        {
            foreach (var f in typeof(RoyalLibraryDressing).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                if (f.FieldType == typeof(Action<RoyalLibraryDressing>) && f.GetValue(null) is Action<RoyalLibraryDressing> a)
                    yield return a;
        }

        static readonly Dictionary<string, string> Slots = new Dictionary<string, string>
        {
            { "stone", "RL_CreamStone" }, { "walnut", "RL_Walnut" }, { "varnished", "RL_VarnishedWalnut" },
            { "brass", "RL_Brass" }, { "leather", "RL_RedLeather" }, { "parchment", "RL_Parchment" },
            { "ink", "RL_InkGlass" }, { "cloth", "RL_CreamCloth" }, { "wax", "RL_RedWax" }, { "marble", "RL_Marble" },
            { "ivory", "RL_Ivory" }, { "ebony", "RL_Ebony" }, { "glass", "RL_Glass" },
            { "leaf", "RL_Leaf" },
            { "leaf_light", "RL_LeafLight" },
            { "stem", "RL_Stem" },
            { "soil", "RL_Soil" },
            { "petal_cream", "RL_PetalCream" },
            { "petal_pink", "RL_PetalPink" },
            { "ceramic", "RL_Ceramic" },
            { "pollen", "RL_Pollen" },
            { "bark", "RL_Bark" }, { "painting", "RL_Portrait" }, { "tea", "RL_Tea" }, { "veil", "RL_Veil" },
            { "leather_brown", "RL_LeatherBrown" }, { "leather_dark", "RL_LeatherDark" },
            { "leather_tan", "RL_LeatherTan" }, { "leather_slate", "RL_LeatherSlate" },
        };

        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        static IEnumerable<Hang> AllHangs()
        {
            foreach (var f in typeof(RoyalLibraryDressing).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                if (f.FieldType == typeof(Hang[]) && f.GetValue(null) is Hang[] list)
                    foreach (var h in list) yield return h;
        }

        void Start()
        {
            if (!dress) return;
            int models = 0, covered = 0, missing = 0;
            foreach (var hang in AllHangs())
            {
                // No model: only stop drawing the covered pieces (RoyalLibraryDressing.Cleanup.cs, 11-18).
                var fbx = string.IsNullOrEmpty(hang.model) ? null : Resources.Load<GameObject>(ModelFolder + hang.zone + "/" + hang.model);
                if (fbx == null && !string.IsNullOrEmpty(hang.model)) { missing++; continue; }
                var hosts = new List<Transform>();
                if (string.IsNullOrEmpty(hang.on)) hosts.Add(transform);
                else Find(transform, hang.on, hosts);
                if (hang.index >= 0)
                    hosts = hang.index < hosts.Count ? new List<Transform> { hosts[hang.index] } : new List<Transform>();
                if (fbx != null)
                    foreach (var host in hosts)
                    {
                        if (hang.reskin ? Reskin(host, fbx) : HangOn(host, fbx)) models++;
                    }
                var scope = string.IsNullOrEmpty(hang.under) ? new List<Transform> { transform } : Under(hang.under);
                foreach (var s in scope)
                    foreach (var pattern in hang.covers)
                        covered += Cover(s, pattern);
            }
            Debug.Log("[RoyalLibraryDressing] 모델 " + models + "개 · 덮은 블록아웃 " + covered + "개"
                      + (missing > 0 ? " · 아직 없는 FBX " + missing + "개" : ""));
            foreach (var after in AfterDressActions())
            {
                try { after(this); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
            // R904: faces lying on other faces lifted a few mm (CoplanarNudges, CoplanarScan's bake), last
            CoplanarNudges.Apply(transform, "RoyalLibrary/coplanar_nudges");
        }

        readonly Dictionary<(GameObject, Vector3), Mesh> reskinned = new Dictionary<(GameObject, Vector3), Mesh>();

        // The FBX's first mesh into the host's own MeshFilter, in the host's frame: the same half turn about y as
        // HangOn's pivot, the mesh node's place inside the FBX, and the host's scale undone (a 2.42 x 0.012 x 2.42 disk
        // must not squash the model). One baked mesh per FBX and host scale. Renderer, colliders, place: the host's.
        bool Reskin(Transform host, GameObject fbx)
        {
            var mf = host.GetComponent<MeshFilter>();
            var mr = host.GetComponent<MeshRenderer>();
            var srcFilter = fbx.GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mr == null || srcFilter == null || srcFilter.sharedMesh == null) return false;
            var s = host.lossyScale;
            var key = (fbx, s);
            if (!reskinned.TryGetValue(key, out var mesh))
            {
                var node = fbx.transform.worldToLocalMatrix * srcFilter.transform.localToWorldMatrix;
                var m = Matrix4x4.Scale(new Vector3(1 / Safe(s.x), 1 / Safe(s.y), 1 / Safe(s.z)))
                        * Matrix4x4.Rotate(Quaternion.Euler(0, 180, 0)) * node;
                var n = m.inverse.transpose;
                mesh = Instantiate(srcFilter.sharedMesh);
                mesh.name = srcFilter.sharedMesh.name + " (reskin)";
                var v = mesh.vertices; var nr = mesh.normals; var t = mesh.tangents;
                for (int i = 0; i < v.Length; i++) v[i] = m.MultiplyPoint3x4(v[i]);
                for (int i = 0; i < nr.Length; i++) nr[i] = n.MultiplyVector(nr[i]).normalized;
                for (int i = 0; i < t.Length; i++)
                {
                    var d = m.MultiplyVector(new Vector3(t[i].x, t[i].y, t[i].z)).normalized;
                    t[i] = new Vector4(d.x, d.y, d.z, t[i].w);
                }
                mesh.vertices = v; mesh.normals = nr;
                if (t.Length == v.Length) mesh.tangents = t;
                mesh.RecalculateBounds();
                reskinned[key] = mesh;
            }
            mf.sharedMesh = mesh;
            var src = srcFilter.GetComponent<Renderer>();
            if (src != null)
            {
                mr.sharedMaterials = src.sharedMaterials;
                if (bakedMaterials) Paint(mr);
            }
            return true;
        }

        bool HangOn(Transform host, GameObject fbx)
        {
            // The FBX's axes (rl_kit.export: -Z forward, Y up) need a half turn about y to land Blender x, y, z on
            // Unity x, z, y - the carriage yard's pivot. A scaled host is undone so the model keeps metres.
            var pivot = new GameObject(fbx.name + " (model)").transform;
            pivot.SetParent(host, false);
            pivot.localRotation = Quaternion.Euler(0, 180, 0);
            var s = host.lossyScale;
            pivot.localScale = new Vector3(1 / Safe(s.x), 1 / Safe(s.y), 1 / Safe(s.z));
            var model = Instantiate(fbx, pivot, false);
            model.name = fbx.name;
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);   // the prefab keeps the colliders
            if (bakedMaterials)
                foreach (var r in model.GetComponentsInChildren<Renderer>(true)) Paint(r);
            return true;
        }

        static float Safe(float v) => Mathf.Abs(v) < 1e-5f ? 1 : v;

        void Paint(Renderer r)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var lib = mats[i] != null ? Library(mats[i].name) : null;
                if (lib != null) mats[i] = lib;
            }
            r.sharedMaterials = mats;
        }

        Material Library(string fbxName)
        {
            // "RL _ walnut", "RL | walnut", "RL_walnut" (an instance may add " (Instance)")
            string n = fbxName.Replace(" (Instance)", "").Trim();
            if (!n.StartsWith("RL", StringComparison.Ordinal)) return null;
            string key = n.Substring(2).TrimStart(' ', '_', '|').Trim().ToLowerInvariant();
            if (!Slots.TryGetValue(key, out var asset)) return null;
            if (!materials.TryGetValue(asset, out var m))
                materials[asset] = m = Resources.Load<Material>(MaterialFolder + asset);
            return m;
        }

        List<Transform> Under(string name)
        {
            var list = new List<Transform>();
            Find(transform, name, list);
            return list;
        }

        static void Find(Transform t, string name, List<Transform> into)
        {
            if (t.name == name) into.Add(t);
            for (int i = 0; i < t.childCount; i++) Find(t.GetChild(i), name, into);
        }

        // A matching piece stops drawing with everything under it (a group name covers the group); hung models
        // ("<model> (model)") are never touched.
        static int Cover(Transform t, string pattern)
        {
            if (IsModel(t)) return 0;
            bool prefix = pattern.EndsWith("*", StringComparison.Ordinal);
            string p = prefix ? pattern.Substring(0, pattern.Length - 1) : pattern;
            if (prefix ? t.name.StartsWith(p, StringComparison.Ordinal) : t.name == p) return HideAll(t);
            int n = 0;
            for (int i = 0; i < t.childCount; i++) n += Cover(t.GetChild(i), pattern);
            return n;
        }

        static bool IsModel(Transform t) => t.name.EndsWith(" (model)", StringComparison.Ordinal);

        static int HideAll(Transform t)
        {
            if (IsModel(t)) return 0;
            int n = 0;
            if (t.TryGetComponent<MeshRenderer>(out var mr) && mr.enabled) { mr.enabled = false; n++; }
            for (int i = 0; i < t.childCount; i++) n += HideAll(t.GetChild(i));
            return n;
        }
    }
}
