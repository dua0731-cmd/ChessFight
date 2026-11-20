using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Observatory's Blender models laid over the graybox in Play (R725, ArtSource/PawnRush/RoyalObservatory/PLAN.md
    // R0), the way the royal library is dressed (RoyalLibraryDressing). RoyalObservatoryBuilder adds this to the built
    // Content once it is built (Play only); every position, collider, pivot and timing stays the builder's: a covered
    // graybox piece only stops drawing (its renderer off, its collider kept).
    //
    // Each zone adds its models in its own partial file - .Dipper.cs (R1), .Bowl.cs (R2), .Globe.cs (R3),
    // .Cassiopeia.cs (R4), .Telescope.cs (R5), .Hall.cs (R6) - as a static Hang[] field; this file finds them all. A Hang
    // names an FBX under Resources/RoyalObservatory/Models/<zone>/ (ro_kit.export), the built object it hangs under
    // (null = Content, the section's frame; else every object of that name, e.g. "Device_OrbitRing 1" - the FBX was
    // written in that object's frame, so it turns with the device) and the graybox pieces it covers (a name, or a
    // name's start then '*'; searched under `under`, or the whole of Content).
    //
    // Materials: an FBX slot "RO | <slot>" (Unity imports it as "RO _ <slot>") is drawn with
    // Resources/RoyalObservatory/Materials/RO_<Name> (ro_textures.py / ro_unity_materials.py); "planet_<name>" with
    // RO_Planet<Name>; an unknown slot keeps the FBX's own material, so a gap shows.
    // Lamps: "<model>_lamps.txt" beside an FBX (ro_kit.write_lamps: one "x y z" a line, in the model's frame, Unity axes)
    // puts a warm point light at each globe and lights the globe's pearl - light is Unity's, never baked into a model.
    public sealed partial class RoyalObservatoryDressing : MonoBehaviour
    {
        const string ModelFolder = "RoyalObservatory/Models/";
        const string MaterialFolder = "RoyalObservatory/Materials/";

        // Off: the graybox as the builder made it (a switch for comparing, as the library has).
        public static bool Enabled = true;

        public sealed class Hang
        {
            public string zone, model;
            public string on;           // built object the model rides (null: Content)
            public int index = -1;      // which one of the objects named `on`: -1 every one
            public string under;        // where `covers` is looked for (null: all of Content)
            public string[] covers = Array.Empty<string>();
        }

        static readonly Dictionary<string, string> Slots = new Dictionary<string, string>
        {
            { "marble", "RO_Marble" }, { "gold", "RO_Gold" }, { "brass", "RO_Brass" }, { "navy", "RO_NavyEnamel" },
            { "violet", "RO_VioletEnamel" }, { "pearl", "RO_Pearl" }, { "glass", "RO_Glass" }, { "hallstone", "RO_HallStone" },
            { "velvet", "RO_Velvet" }, { "motif", "RO_Motif" }, { "sky", "RO_DomeSky" }, { "night", "RO_NightSky" }, { "black", "RO_BlackEnamel" },
        };

        // The lamps' light: warm, short-reaching (a globe lights its own platform), no shadows (many lamps).
        public static Color LampColor = new Color(1f, .78f, .52f);
        public static float LampIntensity = 2.2f, LampRange = 5.5f;

        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        Material lampGlobe;
        int lights;

        static IEnumerable<Hang> AllHangs()
        {
            foreach (var f in typeof(RoyalObservatoryDressing).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
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
                {
                    var lamps = Resources.Load<TextAsset>(ModelFolder + hang.zone + "/" + hang.model + "_lamps");
                    foreach (var host in hosts)
                        if (HangOn(host, fbx, lamps)) models++;
                }
                var scope = new List<Transform>();
                if (string.IsNullOrEmpty(hang.under)) scope.Add(transform);
                else Find(transform, hang.under, scope);
                foreach (var s in scope)
                    foreach (var pattern in hang.covers)
                        covered += Cover(s, pattern);
            }
            Debug.Log("[천문대 입히기] 모델 " + models + "개 · 덮은 회색 상자 " + covered + "개 · 등 " + lights + "개"
                      + (missing > 0 ? " · 아직 없는 FBX " + missing + "개" : ""));
            foreach (var after in AfterDressActions())
            {
                try { after(this); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
        }

        // What a zone does once the models hang (a device's warning drawn on the model instead of the graybox's
        // overlay, ...): a static Action<RoyalObservatoryDressing> field in any partial file, called after dressing.
        static IEnumerable<Action<RoyalObservatoryDressing>> AfterDressActions()
        {
            foreach (var f in typeof(RoyalObservatoryDressing).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                if (f.FieldType == typeof(Action<RoyalObservatoryDressing>) && f.GetValue(null) is Action<RoyalObservatoryDressing> a)
                    yield return a;
        }

        // Library materials on a model made after dressing (a device's runtime parts, e.g. MeteorShowerDress).
        public void PaintModel(GameObject model)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) Paint(r);
        }

        // The objects whose names start with `prefix` under this content.
        public List<Transform> FindStarting(string prefix)
        {
            var list = new List<Transform>();
            void Walk(Transform t)
            {
                if (t.name.StartsWith(prefix, StringComparison.Ordinal)) list.Add(t);
                for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i));
            }
            Walk(transform);
            return list;
        }

        // The objects named `name` under this content.
        public List<Transform> FindAll(string name)
        {
            var list = new List<Transform>();
            Find(transform, name, list);
            return list;
        }

        bool HangOn(Transform host, GameObject fbx, TextAsset lamps)
        {
            // ro_kit.export writes -Z forward, Y up: a half turn about y lands Blender x, y, z on Unity x, z, y (the
            // library's and the carriage yard's pivot). A scaled host is undone so the model keeps metres.
            var pivot = new GameObject(fbx.name + " (model)").transform;
            pivot.SetParent(host, false);
            pivot.localRotation = Quaternion.Euler(0, 180, 0);
            var s = host.lossyScale;
            pivot.localScale = new Vector3(1 / Safe(s.x), 1 / Safe(s.y), 1 / Safe(s.z));
            var model = Instantiate(fbx, pivot, false);
            model.name = fbx.name;
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);   // the builder keeps the colliders
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) Paint(r);
            if (lamps != null) Lamps(host, lamps.text);
            return true;
        }

        // The lamps' positions are in the model's frame in Unity axes: the host's frame, its scale undone.
        void Lamps(Transform host, string text)
        {
            var s = host.lossyScale;
            foreach (var line in text.Split('\n'))
            {
                var p = line.Trim().Split(' ');
                if (p.Length < 3) continue;
                var at = new Vector3(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture),
                                     float.Parse(p[2], CultureInfo.InvariantCulture));
                var go = new GameObject("Lamp light (model)");
                go.transform.SetParent(host, false);
                go.transform.localPosition = new Vector3(at.x / Safe(s.x), at.y / Safe(s.y), at.z / Safe(s.z));
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = LampColor;
                light.intensity = LampIntensity;
                light.range = LampRange;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.Auto;
                lights++;
            }
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
            // "RO _ gold", "RO | gold", "RO_gold" (an instance may add " (Instance)")
            string n = fbxName.Replace(" (Instance)", "").Trim();
            if (!n.StartsWith("RO", StringComparison.Ordinal)) return null;
            string key = n.Substring(2).TrimStart(' ', '_', '|').Trim().ToLowerInvariant();
            string asset;
            if (key == "pearl") return LampGlobe();
            if (key.StartsWith("planet_", StringComparison.Ordinal))
            {
                var parts = key.Substring(7).Split('_');
                asset = "RO_Planet";
                foreach (var w in parts) if (w.Length > 0) asset += char.ToUpperInvariant(w[0]) + w.Substring(1);
            }
            else if (!Slots.TryGetValue(key, out asset)) return null;
            if (!materials.TryGetValue(asset, out var m))
                materials[asset] = m = Resources.Load<Material>(MaterialFolder + asset);
            return m;
        }

        // The pearl: the lamp globes and the pearl trims share it. A copy with a soft warm glow (the lamps are lit by
        // the code's lights; the glow only keeps the globes from reading as dull stone in the dark hall).
        Material LampGlobe()
        {
            if (lampGlobe != null) return lampGlobe;
            var src = Resources.Load<Material>(MaterialFolder + "RO_Pearl");
            if (src == null) return null;
            lampGlobe = new Material(src) { name = "RO_Pearl (lit)" };
            lampGlobe.EnableKeyword("_EMISSION");
            lampGlobe.SetColor("_EmissionColor", LampColor * .9f);
            lampGlobe.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return lampGlobe;
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
