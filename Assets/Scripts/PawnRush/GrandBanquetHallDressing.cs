using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The grand banquet hall's Blender models laid over the graybox in Play (B0, seoungtest R837,
    // ArtSource/PawnRush/BanquetHall/PLAN.md), made from RoyalObservatoryDressing. GrandBanquetHallBuilder adds this to the
    // built Content once it is built (Play only); every position, collider, pivot and timing stays the builder's: a covered
    // graybox piece only stops drawing (its renderer off, its collider kept).
    //
    // Each zone adds its models in its own partial file - .Servery.cs (B1), .Table.cs (B2), .Dais.cs (B3), .Waltz.cs (B4),
    // .Stage.cs (B5), .Chandeliers.cs (B6), .Hall.cs (B7), .Lighting.cs (B8) - as a static Hang[] field; this file finds
    // them all. A Hang names an FBX under Resources/BanquetHall/Models/<zone>/ (bh_kit.export), the built object it hangs
    // under (null = Content, the section's frame; else every object of that name, e.g. "BH waiter 1" - the FBX was
    // written in that object's frame, so it moves with it) and the graybox pieces it covers (a name, or a name's start
    // then '*'; searched under `under`, or the whole of Content). `blank` pieces are emptied instead of switched off
    // (their mesh taken away): for renderers a device switches on and off itself (the tablecloth trick's).
    //
    // Materials: an FBX slot "BH | <slot>" (Unity imports it as "BH _ <slot>") is drawn with
    // Resources/BanquetHall/Materials/BH_<Name> (bh_textures.py / bh_unity_materials.py); an unknown slot keeps the FBX's
    // own material, so a gap shows.
    public sealed partial class GrandBanquetHallDressing : MonoBehaviour
    {
        const string ModelFolder = "BanquetHall/Models/";
        const string MaterialFolder = "BanquetHall/Materials/";

        // Off: the graybox as the builder made it (a switch for comparing).
        public static bool Enabled = true;

        public sealed class Hang
        {
            public string zone, model;
            public string on;           // built object the model rides (null: Content)
            public int index = -1;      // which one of the objects named `on`: -1 every one
            public string under;        // where `covers` / `blank` are looked for (null: all of Content)
            public string[] covers = Array.Empty<string>();
            public string[] blank = Array.Empty<string>();
        }

        // bh_kit.SLOTS: slot -> library material.
        static readonly Dictionary<string, string> Slots = new Dictionary<string, string>
        {
            { "stone", "BH_Stone" }, { "walnut", "BH_Walnut" }, { "gold", "BH_Gold" }, { "damask", "BH_GoldDamask" },
            { "silver", "BH_Silver" }, { "linen", "BH_Linen" }, { "runner", "BH_Runner" }, { "velvet", "BH_Velvet" },
            { "marble", "BH_Marble" }, { "floor_light", "BH_FloorLight" }, { "floor_dark", "BH_FloorDark" },
            { "chocolate", "BH_Chocolate" }, { "crystal", "BH_Crystal" }, { "wax", "BH_Wax" }, { "piece_white", "BH_Ivory" },
            { "piece_black", "BH_Ebony" }, { "olive", "BH_Olive" }, { "china", "BH_China" }, { "emerald", "BH_Emerald" },
            { "figures_royal", "BH_FiguresRoyal" }, { "figures_staff", "BH_FiguresStaff" },   // the user's figurines (bh_figures.py)
        };

        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        static IEnumerable<Hang> AllHangs()
        {
            foreach (var f in typeof(GrandBanquetHallDressing).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                if (f.FieldType == typeof(Hang[]) && f.GetValue(null) is Hang[] list)
                    foreach (var h in list) yield return h;
        }

        void Start()
        {
            if (!Enabled) return;
            int models = 0, covered = 0, missing = 0;
            foreach (var hang in AllHangs())
            {
                var fbx = string.IsNullOrEmpty(hang.model) ? null : Resources.Load<GameObject>(ModelFolder + hang.zone + "/" + hang.model);
                if (fbx == null && !string.IsNullOrEmpty(hang.model)) { missing++; continue; }
                var hosts = new List<Transform>();
                if (string.IsNullOrEmpty(hang.on)) hosts.Add(transform);
                else Find(transform, hang.on, hosts);
                if (hang.index >= 0)
                    hosts = hang.index < hosts.Count ? new List<Transform> { hosts[hang.index] } : new List<Transform>();
                if (fbx != null)
                    foreach (var host in hosts)
                        if (HangOn(host, fbx) != null) models++;
                var scope = new List<Transform>();
                if (string.IsNullOrEmpty(hang.under)) scope.Add(transform);
                else Find(transform, hang.under, scope);
                foreach (var s in scope)
                {
                    foreach (var pattern in hang.covers) covered += Cover(s, pattern, false);
                    foreach (var pattern in hang.blank) covered += Cover(s, pattern, true);
                }
            }
            Debug.Log("[대연회장 입히기] 모델 " + models + "개 · 덮은 회색 상자 " + covered + "개"
                      + (missing > 0 ? " · 아직 없는 FBX " + missing + "개" : ""));
            foreach (var after in AfterDressActions())
            {
                try { after(this); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
        }

        // What a zone does once the models hang: a static Action<GrandBanquetHallDressing> field in any partial file.
        static IEnumerable<Action<GrandBanquetHallDressing>> AfterDressActions()
        {
            foreach (var f in typeof(GrandBanquetHallDressing).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                if (f.FieldType == typeof(Action<GrandBanquetHallDressing>) && f.GetValue(null) is Action<GrandBanquetHallDressing> a)
                    yield return a;
        }

        // Library materials on a model made after dressing.
        public void PaintModel(GameObject model)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) Paint(r);
        }

        // The objects named `name` under this content.
        public List<Transform> FindAll(string name)
        {
            var list = new List<Transform>();
            Find(transform, name, list);
            return list;
        }

        // The hung model on `host` ("<fbx> (model)"), or null.
        public static Transform ModelOn(Transform host, string fbx) => host.Find(fbx + " (model)");

        GameObject HangOn(Transform host, GameObject fbx)
        {
            // bh_kit.export writes -Z forward, Y up: a half turn about y lands Blender x, y, z on Unity x, z, y (the
            // observatory's pivot). A scaled host is undone so the model keeps metres.
            var pivot = new GameObject(fbx.name + " (model)").transform;
            pivot.SetParent(host, false);
            pivot.localRotation = Quaternion.Euler(0, 180, 0);
            var s = host.lossyScale;
            pivot.localScale = new Vector3(1 / Safe(s.x), 1 / Safe(s.y), 1 / Safe(s.z));
            var model = Instantiate(fbx, pivot, false);
            model.name = fbx.name;
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);   // the builder keeps the colliders
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) Paint(r);
            return model;
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
            // "BH _ gold", "BH | gold", "BH_gold" (an instance may add " (Instance)")
            string n = fbxName.Replace(" (Instance)", "").Trim();
            if (!n.StartsWith("BH", StringComparison.Ordinal)) return null;
            string key = n.Substring(2).TrimStart(' ', '_', '|').Trim().ToLowerInvariant();
            if (!Slots.TryGetValue(key, out var asset)) return null;
            if (!materials.TryGetValue(asset, out var m))
                materials[asset] = m = Resources.Load<Material>(MaterialFolder + asset);
            return m;
        }

        static void Find(Transform t, string name, List<Transform> into)
        {
            if (t.name == name) into.Add(t);
            for (int i = 0; i < t.childCount; i++) Find(t.GetChild(i), name, into);
        }

        // A matching piece stops drawing with everything under it (a group name covers the group); hung models
        // ("<model> (model)") are never touched. blank: the mesh is taken away instead (the renderer stays on).
        static int Cover(Transform t, string pattern, bool blank)
        {
            if (IsModel(t)) return 0;
            bool prefix = pattern.EndsWith("*", StringComparison.Ordinal);
            string p = prefix ? pattern.Substring(0, pattern.Length - 1) : pattern;
            if (prefix ? t.name.StartsWith(p, StringComparison.Ordinal) : t.name == p) return HideAll(t, blank);
            int n = 0;
            for (int i = 0; i < t.childCount; i++) n += Cover(t.GetChild(i), pattern, blank);
            return n;
        }

        static bool IsModel(Transform t) => t.name.EndsWith(" (model)", StringComparison.Ordinal);

        static int HideAll(Transform t, bool blank)
        {
            if (IsModel(t)) return 0;
            int n = 0;
            if (t.TryGetComponent<MeshRenderer>(out var mr))
            {
                if (blank && t.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null) { mf.sharedMesh = null; n++; }
                else if (!blank && mr.enabled) { mr.enabled = false; n++; }
            }
            for (int i = 0; i < t.childCount; i++) n += HideAll(t.GetChild(i), blank);
            return n;
        }
    }
}
