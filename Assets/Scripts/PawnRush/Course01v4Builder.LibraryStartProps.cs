using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R364 (승규 11-04 "Unity에 적용해", after the Blender looks R359 - R361): the library's start props in their
        // textured looks. ArtSource/PawnRush/Library/start_props_unity.py put the new nodes into the environment FBX and
        // baked their Blender shaders into Resources/PawnRushLibrary/StartProps: <name> (base colour) and <name>_MS
        // (metallic in RGB, smoothness in A).
        //   Start_compass_rose    the bronze compass rose on the start disc (START_spawn_zone)
        //   War_map_frame         the war map's rebuilt wooden frame with iron bands, gold caps, studs and nodes
        //   War_map_parchment     its two painted parchment faces (no metal map)
        //   Start_armillary|...   the armillary's table and gold stand; its rings plain gold, its globe the star map
        const string StartProps = "PawnRushLibrary/StartProps/";
        static readonly Dictionary<string, Material> startPropMaterials = new Dictionary<string, Material>();

        static void LibraryStartProps(GameObject hall)
        {
            int dressed = 0;
            foreach (var r in hall.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.name;
                Material m = null;
                if (n == "Start_compass_rose") m = StartPropMaterial("StartCompass", true, .7f);
                else if (n == "War_map_frame") m = StartPropMaterial("WarMapFrame", true, .6f);
                else if (n == "War_map_parchment") m = StartPropMaterial("WarMapParchment", false, .18f);
                else if (n.StartsWith("Start_armillary|")) m = StartPropMaterial("ArmillaryStand", true, .55f);
                else if (n == "Start_armillary_Sphere") m = StartPropMaterial("ArmillaryGlobe", true, .5f);
                else if (n.StartsWith("Start_armillary_Ring")) m = ArmillaryGold();
                if (m == null) continue;
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = m;
                r.sharedMaterials = slots;
                dressed++;
            }
            if (dressed < 8) Debug.LogError("[작전 회의실] 출발 소품 " + dressed + "/8개에만 재질을 입혔습니다 (start_props_unity.py를 돌렸는지 확인)");
        }

        // Base colour, and with metalMap the _MS map (both scaled by 1, as the hall's pads); without it a flat
        // non-metal of the given smoothness.
        static Material StartPropMaterial(string name, bool metalMap, float smoothness)
        {
            if (startPropMaterials.TryGetValue(name, out var cached) && cached != null) return cached;
            var albedo = Resources.Load<Texture2D>(StartProps + name);
            if (albedo == null)
            {
                Debug.LogError("[작전 회의실] 출발 소품 텍스처를 불러오지 못했습니다: Resources/" + StartProps + name + ".png");
                return null;
            }
            var m = new Material(PieceFigure.Lit(Color.white, smoothness)) { name = "Library " + name, mainTexture = albedo };
            var ms = metalMap ? Resources.Load<Texture2D>(StartProps + name + "_MS") : null;
            if (ms != null)
            {
                m.SetTexture("_MetallicGlossMap", ms);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                LitMaterials.SetMetallic(m, 1f);
                LitMaterials.SetSmoothness(m, 1f);
            }
            return startPropMaterials[name] = m;
        }

        // The armillary's rings: Blender's "Armillary gold" (linear 1, .62, .20) as sRGB, without its faint mottling.
        static Material armillaryGold;
        static Material ArmillaryGold()
        {
            if (armillaryGold == null)
            {
                armillaryGold = new Material(PieceFigure.Lit(new Color(1f, .81f, .49f), .74f, .9f)) { name = "Library armillary gold" };
            }
            return armillaryGold;
        }
    }
}
