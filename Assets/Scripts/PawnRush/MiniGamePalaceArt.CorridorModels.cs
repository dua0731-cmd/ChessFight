using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush
{
    public static partial class MiniGamePalaceArt
    {
        // 2026-11-22 (승규: "복도만의 모양으로 따로 만들어야 하고 ... 들어가는 구역에 그 컨셉에 맞게"): modelled corridor kits.
        // A corridor takes the look of the place it leads into (the first one, out of the garden or the carriage yard, the
        // look of the place it leaves), each kit a set of Blender modules in Resources/PawnRushCorridors/<kit>/
        // (ArtSource/PawnRush/Corridors, concepts in Docs/PawnRush/ConceptArt/SectionCorridors):
        //   CR01_Garden, CR02_CarriageYard   out of the first obstacle section (by the section left)
        //   CR03_HallPorch                   into the grand stair hall
        //   CR04_LowerRooms, CR05_KingsAntechamber   into the second gate's room (B E F J / G H I)
        //   CR06_WarRoom, CR07_Library       into the late obstacle section (CR08_Observatory: into the observatory)
        // Anything else (the observatory, the vault, the ending) keeps the code picture. The bodies never change
        // (SectionCorridor): a kit only replaces pictures, so nothing goes on the wire and no protocol moves.
        //
        // Modules (all optional, each replaces only its own code part): Bay / BayMid / BayWest / BayEast (a wall bay
        // between two piers, authored for the west wall at a 1.9 m clear span and scaled along the way to the real one),
        // Pier, CeilingBay, CeilingBeam, Header (over a mini-game's arch), Portal (round an obstacle section's end),
        // EndWall (the war room's wide start floor), Floor (replaces the paving) and FloorInlay (replaces the runner).
        // <id>_Layout.json lists them and the ornaments hung per pier or bay. Markers inside a model: LIGHT_* (a warm
        // point light and a flame), FLAME_* (a flame), BANNER (a banner, hung as the code banners are).
        // Every model hangs as the mini-game models do (MiniGameModels.Hang): under a pivot turned 180 degrees about y,
        // so Blender x, y, z = the corridor's x, z, y; the east wall and the exit end are the same model turned back.
        public sealed class CorridorKit
        {
            public string Id;
            public Layout Data;
            readonly Dictionary<string, GameObject> modules = new Dictionary<string, GameObject>();

            public bool Has(string module) => Module(module) != null;

            public GameObject Module(string module)
            {
                if (modules.TryGetValue(module, out var model)) return model;
                model = null;
                if (Data.modules != null && System.Array.IndexOf(Data.modules, module) >= 0)
                {
                    model = Resources.Load<GameObject>(KitFolder + Id + "/" + Prefix(Id) + "_" + module);
                    if (model == null) Debug.LogWarning("[복도 모델] 불러오지 못했습니다: Resources/" + KitFolder + Id + "/" + Prefix(Id) + "_" + module + ".fbx — 그 부분은 코드 그림을 씁니다");
                }
                modules[module] = model;
                return model;
            }

            public GameObject Ornament(string name)
            {
                if (modules.TryGetValue("#" + name, out var model)) return model;
                model = Resources.Load<GameObject>(KitFolder + Id + "/" + name);
                if (model == null) Debug.LogWarning("[복도 모델] 장식을 불러오지 못했습니다: Resources/" + KitFolder + Id + "/" + name + ".fbx");
                modules["#" + name] = model;
                return model;
            }

            public bool Walls => Has("Pier") && (Has("Bay") || Has("BayWest") || Has("BayEast"));
            public bool Ceiling => Has("CeilingBay");
        }

        [System.Serializable]
        public sealed class Layout
        {
            public string id;
            public float height = 6.5f;
            public float z_from, z_to = 10f;
            public float bay_span_authored = 1.9f;
            public float pier_depth = .9f;
            public bool open_sky;
            // Ceiling pieces (CeilingBay, CeilingBeam, Header) are authored with their origin at the ceiling (y = height), as
            // agreed between the two kit sessions; a kit authored at absolute heights says so here (CR03).
            public bool ceiling_absolute;
            public string[] modules = new string[0];
            public Ornament[] ornaments = new Ornament[0];
            public BannerSpec banner;        // the BANNER markers' cloth (else the kit's default)
            public BannerSpec banner_room;   // BANNER_ROOM* markers: hung facing the way (+z), into the next room
        }

        [System.Serializable]
        public sealed class BannerSpec
        {
            public float width, height;
            public string fabric;            // a Unity material name (looked up as the models' are)
        }

        [System.Serializable]
        public sealed class Ornament
        {
            public string name;
            public string per = "pier";       // "pier" or "bay"
            public int[] index = new int[0];
            public float[] offset = new float[3];
            public bool mirror = true;        // false: one copy on the corridor's centre line (a chandelier)
        }

        public const string KitFolder = "PawnRushCorridors/";
        const float AuthoredSpan = 1.9f, PierDepth = .9f;
        const int MaxCorridorLights = 6;
        static readonly Dictionary<string, CorridorKit> kits = new Dictionary<string, CorridorKit>();
        static readonly Dictionary<string, Material> kitMaterials = new Dictionary<string, Material>();
        static readonly string[] KitMaterialFolders =
        {
            KitFolder + "Materials/", "PawnRushMiniGames/Materials/", "PawnRushGardenConcept/Materials/",
            "PawnRushCarriageYard/Materials/", ""
        };

        static string Prefix(string id) { int i = id.IndexOf('_'); return i > 0 ? id.Substring(0, i) : id; }

        // The kit a corridor between `before` and `after` takes, or null for the code picture.
        public static string CorridorKitId(PawnRushSection before, PawnRushSection after)
        {
            if (before == null || after == null) return null;
            if (before.Id == RoyalWaterGarden.Id) return "CR01_Garden";
            if (before.Id == RoyalCarriageYard.SectionId) return "CR02_CarriageYard";
            if (after.Id == GrandStairHall.Id) return "CR03_HallPorch";
            if (after.GetComponent<WarRoomSection>() != null) return "CR06_WarRoom";
            if (after.Id == RoyalLibrarySection.Id) return "CR07_Library";
            if (after.Id == RoyalObservatorySection.Id) return "CR08_Observatory";   // its own entry tunnel follows (승규 11-22)
            if (after.Kind == PawnRushSection.SlotKind.MiniGame)
            {
                var room = after.GetComponent<MiniGameSection>();
                if (room != null && !Early(room.GameIndex)) return Late(room.GameIndex) ? "CR05_KingsAntechamber" : "CR04_LowerRooms";
            }
            return null;
        }

        // The kit's layout and models, or null when its layout is not in Resources (then the code picture stays).
        public static CorridorKit LoadCorridorKit(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (kits.TryGetValue(id, out var kit)) return kit;
            kit = null;
            var text = Resources.Load<TextAsset>(KitFolder + id + "/" + Prefix(id) + "_Layout");
            if (text != null)
            {
                var data = JsonUtility.FromJson<Layout>(text.text);
                if (data != null) kit = new CorridorKit { Id = id, Data = data };
            }
            kits[id] = kit;
            return kit;
        }

        // Hangs the kit's models in a corridor (frame as Corridor: the way +z from 0 to `length`, the walls from `from`
        // to `to`). `portalStart` / `portalEnd`: the ends that open onto an obstacle section; `endHalf` > 0: the war room.
        static void CorridorModels(Transform art, CorridorKit kit, float length, float from, float to, float endHalf,
                                   bool portalStart, bool portalEnd, bool early)
        {
            var root = Group(art, "MODEL_Corridor_" + kit.Id);
            int lights = 0;
            var piers = new float[CorridorBays + 1];
            for (int i = 0; i <= CorridorBays; i++) piers[i] = CorridorPier(from, to, i);
            float span = piers[1] - piers[0] - PierDepth;
            float stretch = span / Mathf.Max(.1f, kit.Data.bay_span_authored > 0f ? kit.Data.bay_span_authored : AuthoredSpan);
            if (kit.Walls)
                foreach (float side in new[] { -1f, 1f })
                {
                    var pier = kit.Module("Pier");
                    for (int i = 0; i <= CorridorBays; i++)
                    {
                        // a pier wholly inside a gate frame would only share its faces (flicker, 승규 11-22)
                        if ((i == 0 && portalStart || i == CorridorBays && portalEnd) && kit.Has("Portal")) continue;
                        HangKit(root, "Pier " + i + (side < 0f ? " W" : " E"), pier, new Vector3(side * 6f, 0f, piers[i]), side > 0f, 1f, kit, ref lights, i);
                    }
                    for (int b = 0; b < CorridorBays; b++)
                    {
                        var bay = (b == 1 ? kit.Module("BayMid") : null) ?? kit.Module(side < 0f ? "BayWest" : "BayEast") ?? kit.Module("Bay")
                                  ?? kit.Module(side < 0f ? "BayEast" : "BayWest");
                        HangKit(root, "Bay " + b + (side < 0f ? " W" : " E"), bay, new Vector3(side * 6f, 0f, (piers[b] + piers[b + 1]) * .5f), side > 0f, stretch, kit, ref lights, b);
                    }
                }
            // 승규 11-22 (walls across the way in 04 ~ 07): the ceiling pieces hang at the kit's height, not on the floor
            float ceilY = kit.Data.ceiling_absolute ? 0f : kit.Data.height;
            if (kit.Ceiling)
            {
                for (int b = 0; b < CorridorBays; b++)
                    HangKit(root, "Ceiling bay " + b, kit.Module("CeilingBay"), new Vector3(0f, ceilY, (piers[b] + piers[b + 1]) * .5f), false, stretch, kit, ref lights, b);
                for (int i = 0; i <= CorridorBays; i++)
                    HangKit(root, "Ceiling beam " + i, kit.Module("CeilingBeam"), new Vector3(0f, ceilY, piers[i]), false, 1f, kit, ref lights, i);
            }
            if (from > 0f) HangKit(root, "Header (entry)", kit.Module("Header"), new Vector3(0f, kit.Data.ceiling_absolute ? 0f : kit.Data.height, from), false, 1f, kit, ref lights, 0);
            if (to < length) HangKit(root, "Header (exit)", kit.Module("Header"), new Vector3(0f, kit.Data.ceiling_absolute ? 0f : kit.Data.height, to), true, 1f, kit, ref lights, 0);
            if (portalStart) HangKit(root, "Portal (entry)", kit.Module("Portal"), Vector3.zero, false, 1f, kit, ref lights, 0);
            if (portalEnd) HangKit(root, "Portal (exit)", kit.Module("Portal"), new Vector3(0f, 0f, length), true, 1f, kit, ref lights, 0);
            if (endHalf > 0f) HangKit(root, "End wall", kit.Module("EndWall"), new Vector3(0f, 0f, length), false, 1f, kit, ref lights, 0);
            HangKit(root, "Floor", kit.Module("Floor"), Vector3.zero, false, 1f, kit, ref lights, 0);
            HangKit(root, "Floor inlay", kit.Module("FloorInlay"), Vector3.zero, false, 1f, kit, ref lights, 0);
            if (kit.Data.ornaments != null)
                // the centred ones (chandeliers) first, so the light budget goes to them before the wall sconces
                foreach (var o in System.Linq.Enumerable.OrderBy(kit.Data.ornaments, x => x != null && x.mirror ? 1 : 0))
                {
                    var model = o != null && !string.IsNullOrEmpty(o.name) ? kit.Ornament(o.name) : null;
                    if (model == null || o.index == null) continue;
                    var off = o.offset != null && o.offset.Length >= 3 ? new Vector3(o.offset[0], o.offset[1], o.offset[2]) : Vector3.zero;
                    bool perBay = o.per == "bay";
                    foreach (int i in o.index)
                    {
                        if (i < 0 || i > (perBay ? CorridorBays - 1 : CorridorBays)) continue;
                        float z = perBay ? (piers[i] + piers[i + 1]) * .5f : piers[i];
                        if (!o.mirror)
                        {
                            HangKit(root, o.name + " " + i, model, new Vector3(off.x, off.y, z + off.z), false, 1f, kit, ref lights, i);
                            continue;
                        }
                        HangKit(root, o.name + " " + i + " W", model, new Vector3(-6f + off.x, off.y, z + off.z), false, 1f, kit, ref lights, i);
                        HangKit(root, o.name + " " + i + " E", model, new Vector3(6f - off.x, off.y, z - off.z), true, 1f, kit, ref lights, i);
                    }
                }
        }

        // One model under a pivot at `at` in the corridor's frame; `turned` = the east wall / the exit end (the model
        // turned 180 degrees about up from how it was authored); `stretch` scales it along the way.
        static void HangKit(Transform root, string name, GameObject model, Vector3 at, bool turned, float stretch,
                            CorridorKit kit, ref int lights, int index)
        {
            if (model == null) return;
            var pivot = new GameObject(name).transform;
            pivot.SetParent(root, false);
            pivot.localPosition = at;
            pivot.localRotation = Quaternion.Euler(0f, turned ? 0f : 180f, 0f);
            pivot.localScale = new Vector3(1f, 1f, stretch);
            var copy = Object.Instantiate(model, pivot, false);
            copy.name = model.name;
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] != null) slots[i] = CorridorKitMaterial(slots[i]);
                renderer.sharedMaterials = slots;
            }
            var corridor = root.parent != null ? root.parent : root;
            foreach (var marker in copy.GetComponentsInChildren<Transform>(true))
            {
                string n = marker.name;
                bool light = n.StartsWith("LIGHT_"), flame = light || n.StartsWith("FLAME_");
                if (flame) KitFlame(root, marker, light && lights < MaxCorridorLights, kit.Data.open_sky, ref lights);
                else if (n == "BANNER" || n.StartsWith("BANNER_")) KitBanner(corridor, marker, kit, index);
            }
        }

        // The flame hangs under the corridor's kit root at the marker's place, never under the marker itself: Unity imports
        // a Blender FBX's nodes with a scale of 100 (centimetres), so a flame parented to the marker came out metres wide
        // (승규 11-22, the cream balls in the corridors).
        static void KitFlame(Transform root, Transform marker, bool withLight, bool daylight, ref int lights)
        {
            var at = Group(root, marker.name, root.InverseTransformPoint(marker.position));
            var flame = Ball(at, "Candle flame", Vector3.zero, .045f, Lamp);
            flame.transform.localScale = new Vector3(.035f, .06f, .035f);
            if (!withLight) return;
            var light = at.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, .8f, .55f);
            light.range = daylight ? 3.5f : 7f;
            light.intensity = daylight ? .6f : 1.4f;
            light.shadows = LightShadows.None;
            lights++;
        }

        // A banner as the code corridors hang theirs (back to the wall, MiniGamePalaceArt.Banner), at a model's marker.
        static void KitBanner(Transform corridor, Transform marker, CorridorKit kit, int index)
        {
            var art = corridor;
            var local = art.InverseTransformPoint(marker.position);
            float side = local.x < 0f ? -1f : 1f;
            var banner = Group(art, "Corridor banner (" + kit.Id + ")", local);
            bool room = marker.name.StartsWith("BANNER_ROOM");
            // the cloth's front faces its local -z: a wall banner turns its back to the wall, a room banner its front to +z
            banner.localRotation = Quaternion.Euler(0f, room ? 180f : side * 90f, 0f);
            Material fabric;
            float width = .7f, height = 2.0f;
            var spec = room ? kit.Data.banner_room : kit.Data.banner;
            if (kit.Id == "CR01_Garden") fabric = index % 2 == 0 ? KitMaterialFile("GC_royal_blue") : KitMaterialFile("GC_velvet_crimson");
            else if (kit.Id == "CR03_HallPorch") { fabric = Cloth; width = .8f; height = 2.3f; }
            else fabric = Cloth;
            if (spec != null && spec.width > 0f && spec.height > 0f)
            {
                width = spec.width;
                height = spec.height;
                if (!string.IsNullOrEmpty(spec.fabric)) fabric = KitMaterialFile(spec.fabric) ?? fabric;
            }
            Banner(banner, Vector3.zero, width, height, fabric != null ? fabric : Cloth, true);
        }

        // "GC_cream_stone" (Blender's copy "GC_cream_stone.001" arrives as "..._001") -> the Unity .mat of that name in
        // the corridor, mini-game, garden-concept or carriage-yard materials, or at the Resources root (the hall's).
        static Material CorridorKitMaterial(Material imported)
        {
            string name = Regex.Replace(imported.name.Replace(" (Instance)", ""), "[^A-Za-z0-9]+", "_").Trim('_');
            var copy = Regex.Match(name, "^(.+)_[0-9]{3}$");
            var found = KitMaterialFile(name) ?? (copy.Success ? KitMaterialFile(copy.Groups[1].Value) : null);
            if (found == null && !kitMaterials.ContainsKey("!" + name))
            {
                kitMaterials["!" + name] = null;
                Debug.LogWarning("[복도 모델] 재질이 없습니다: " + name + " — FBX의 재질을 그대로 씁니다");
            }
            return found != null ? found : imported;
        }

        static Material KitMaterialFile(string name)
        {
            if (kitMaterials.TryGetValue(name, out var m)) return m;
            m = null;
            foreach (var folder in KitMaterialFolders)
            {
                m = Resources.Load<Material>(folder + name);
                if (m != null) break;
            }
            kitMaterials[name] = m;
            return m;
        }
    }
}
