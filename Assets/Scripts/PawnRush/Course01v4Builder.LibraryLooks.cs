using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R371 (승규 11-04 "Unity에 적용해", after R368 / R370): the library's hand-built blower cannons and the textured
        // conveyor. ArtSource/PawnRush/Library/cannon_conveyor_unity.py wrote them into the two FBX files with material
        // names this matches: the flat parts "Library gold / teal / cream / steel / dark / grout / stone / wood"
        // (Blender's linear colours as sRGB here) and the conveyor's painted maps in Resources/PawnRushLibrary/Conveyor
        // ("Library conveyor deck", "Library conveyor chassis": <name> base colour, <name>_MS metallic RGB +
        // smoothness A). Any other material is left as it is.
        const string ConveyorMaps = "PawnRushLibrary/Conveyor/", CannonMaps = "PawnRushLibrary/Cannon/",
            WarPropMaps = "PawnRushLibrary/WarProps/";
        static readonly Dictionary<string, Material> libraryLooks = new Dictionary<string, Material>();

        static void LibraryLooks(GameObject root, string what)
        {
            int slots = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.StartsWith("Library ")) continue;
                    var m = LibraryLook(mats[i].name);
                    if (m == null) continue;
                    mats[i] = m;
                    changed = true;
                    slots++;
                }
                if (changed) r.sharedMaterials = mats;
            }
            if (slots == 0) Debug.LogError("[작전 회의실] " + what + "에 \"Library …\" 재질이 없습니다 (cannon_conveyor_unity.py를 돌렸는지 확인)");
        }

        static Material LibraryLook(string fbxName)
        {
            string name = fbxName.Replace(" (Instance)", "");
            if (libraryLooks.TryGetValue(name, out var cached)) return cached;
            Material m;
            switch (name)
            {
                // R383 (승규 11-04 "완전 빛나는 골드 색이면 안돼 ... 초록색 부분이 검은색으로 나와"): worn gold, not a mirror;
                // the teal as paint (a metal reflects the dim hall and read black); the cannon body is its baked maps.
                case "Library gold": m = FlatLook(new Color(.75f, .61f, .32f), .42f, .6f); break;
                case "Library teal": m = FlatLook(new Color(.30f, .44f, .47f), .34f, .05f); break;
                case "Library cream": m = FlatLook(new Color(.88f, .84f, .78f), .3f, 0f); break;
                case "Library steel": m = FlatLook(new Color(.27f, .28f, .30f), .4f, .55f); break;
                case "Library dark": m = FlatLook(new Color(.19f, .19f, .21f), .3f, .2f); break;
                case "Library cannon worn": m = MappedLook("LibraryCannon", CannonMaps); break;
                case "Library grout": m = FlatLook(new Color(.54f, .50f, .45f), .2f, 0f); break;
                case "Library stone": m = FlatLook(new Color(.63f, .63f, .64f), .25f, 0f); break;
                case "Library wood": m = FlatLook(new Color(.53f, .37f, .23f), .4f, 0f); break;
                case "Library conveyor deck": m = MappedLook("LibraryConveyorDeck"); break;
                case "Library conveyor chassis": m = MappedLook("LibraryConveyorChassis"); break;
                // R376: the conveyor worn and dusty like its reference; its small parts share one baked atlas.
                case "Library conveyor parts": m = MappedLook("LibraryConveyorParts"); break;
                // R402 (승규 11-04, ArtSource/PawnRush/Library/war_room_props.py): the new bookcases (tiled warm wood, the
                // back boards and plinth the same wood darker, the books from one painted atlas), the pier banners, the
                // scroll crate's weathered wood and its aged parchment rolls.
                case "Library bookcase wood": m = MappedLook("LibraryBookcaseWood", WarPropMaps); break;
                case "Library bookcase wood dark":
                    m = MappedLook("LibraryBookcaseWood", WarPropMaps);
                    if (m != null) m.color = new Color(.55f, .47f, .42f);
                    break;
                case "Library bookcase books": m = MappedLook("LibraryBookcaseBooks", WarPropMaps); break;
                case "Library banner": m = MappedLook("LibraryBanner", WarPropMaps); break;
                case "Library crate wood": m = MappedLook("LibraryCrateWood", WarPropMaps); break;
                case "Library parchment": m = MappedLook("LibraryParchment", WarPropMaps); break;
                // R427 (승규 11-05 "지하실 구간이 살짝 밋밋해", basement_dressing.py): the lower floor's campaign maps (on the
                // walls and the sand table) and its lanterns' glass, lit from inside.
                case "Library campaign map": m = MappedLook("LibraryCampaignMap", WarPropMaps); break;
                case "Library lantern glass":
                    m = FlatLook(new Color(1f, .8f, .5f), .6f, 0f);
                    LitMaterials.SetEmission(m, new Color(2.4f, 1.5f, .6f));
                    break;
                // R448 (goal_rooks.py): the goal rooks' polished marble, their pedestals' darker steps, and the socket
                // glow under each piece (dim at rest; LibraryRookTurret brightens it through a charge).
                case "Library marble": m = MappedLook("LibraryMarble", WarPropMaps); break;
                case "Library marble dark":
                    m = MappedLook("LibraryMarble", WarPropMaps);
                    if (m != null) m.color = new Color(.62f, .6f, .58f);
                    break;
                case "Library rook glow":
                    m = FlatLook(new Color(1f, .78f, .42f), .7f, 0f);
                    LitMaterials.SetEmission(m, new Color(.25f, .18f, .07f));
                    break;
                default: m = null; break;   // the start props' own (LibraryStartProps) or an unknown name
            }
            if (m != null) m.name = name;
            return libraryLooks[name] = m;
        }

        static Material FlatLook(Color color, float smoothness, float metallic) =>
            new Material(PieceFigure.Lit(color, smoothness, metallic));

        static Material MappedLook(string map, string folder = ConveyorMaps)
        {
            var albedo = Resources.Load<Texture2D>(folder + map);
            if (albedo == null)
            {
                Debug.LogError("[작전 회의실] 텍스처를 불러오지 못했습니다: Resources/" + folder + map + ".png");
                return null;
            }
            var m = new Material(PieceFigure.Lit(Color.white, .5f)) { mainTexture = albedo };
            var ms = Resources.Load<Texture2D>(folder + map + "_MS");
            if (ms != null)
            {
                m.SetTexture("_MetallicGlossMap", ms);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                LitMaterials.SetMetallic(m, 1f);
                LitMaterials.SetSmoothness(m, 1f);
            }
            return m;
        }
    }
}
