using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R350 (승규 11-04, an asset preview of a cream marble grand stair with a red runner, gold balustrades and crown
        // lanterns: "계단 부분도 고퀄리티로 모델링 해줘 텍스쳐 입혀주고"): the grand stair (beat 1) in its own model
        // (royal_stair_v45.py, the hall's frame, under its pivot): marble steps with bullnose nosings and gold inlays, a
        // crimson runner in the hall's carpet texture with gold bands and rods, palace-stone side walls with pilasters,
        // gold-beaded panels and medallions, gold balustrades on stone curbs, eight newels with glowing crown lanterns, and
        // an iron drain grille in a gold frame at the foot. The hall model's flights, middle landing and stair walls are
        // switched off (the stair balustrades are cut out of its railing mesh, OpenLanding); the kit's black drain picture
        // stops drawing. Every collider and the rolling pins' lanes are the blockout's as before.
        const string StairModel = "PawnRushGrandStairHall/RoyalStair";
        static readonly string[] HallStairNodes =
        {
            "HallV15_B1_GrandStair__Lower_flight", "HallV15_B1_GrandStair__Upper_flight", "HallV15_B1_GrandStair__Middle_landing",
            "HallV15_Stair_side_",
        };
        static readonly Dictionary<string, Material> stairMaterials = new Dictionary<string, Material>();

        static void RoyalStair(Transform pivot, Transform hallModel, Transform content, Material clay)
        {
            var model = Resources.Load<GameObject>(StairModel);
            if (model == null)
            {
                Debug.LogError("[대계단 홀] 대계단 모델을 불러오지 못했습니다: Resources/" + StairModel + ".fbx");
                return;
            }
            int off = 0;
            foreach (var t in hallModel.GetComponentsInChildren<Transform>(true))
            {
                foreach (var prefix in HallStairNodes)
                {
                    if (!t.name.StartsWith(prefix)) continue;
                    t.gameObject.SetActive(false);
                    off++;
                    break;
                }
            }
            // The hall's marble floor is one mesh from the porch to the middle landing; its landing tiles (top 4.0) would
            // flicker against the stair's landing tread, so they are cut out of it (the foyer's stay).
            foreach (var f in hallModel.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!f.name.StartsWith("HallV15_B1_GrandStair__Marble_floor")) continue;
                f.gameObject.AddComponent<MeshCutOut>().Configure(content, new Bounds(new Vector3(0f, 4f, 32.5f), new Vector3(16.4f, 2f, 7.2f)));
            }
            int drains = 0;
            foreach (var r in content.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name != "Drain grate" && r.name != "Drain bar") continue;
                r.enabled = false;
                drains++;
            }

            var view = Object.Instantiate(model, pivot, false);
            view.name = "Royal stair v45 model";
            int lamps = 0;
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("RoyalStair_light"))
                {
                    // A marker in a lantern's globe: a warm light there, the marker itself not drawn.
                    r.enabled = false;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var light = new GameObject("Lantern light").AddComponent<Light>();
                    light.transform.SetParent(r.transform, false);
                    if (r.TryGetComponent<MeshFilter>(out var f) && f.sharedMesh != null) light.transform.localPosition = f.sharedMesh.bounds.center;
                    Lamp(light, 9f, 4.5f);
                    lamps++;
                    continue;
                }
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string name = slots[i] != null ? slots[i].name : "";
                    slots[i] = StairMaterial(name) ?? PalaceMaterial(name) ?? clay;
                }
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var c in view.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            Debug.Log("[대계단 홀] 대계단 새 모델(대리석 계단 · 카펫 · 금 난간 · 왕관 등불 " + lamps + "개), 홀 모델의 옛 계단 " + off +
                      "개 끔, 키트 배수구 그림 " + drains + "개 끔");
        }

        // R363 (승규 11-04, the round hall's spiral way: "B로해" - a chessboard): the hall model's pieces of the way
        // (HallV15_B3_Spiral__*: the curving stair, the dive landings, the windy way, the north landing, the last stair)
        // in PR_SpiralChecker.shader, their tops a chessboard, their sides the walls' stone. R372 ("그럼 한번 미니맵에
        // 있는 걸로 해서 해봐 칸 사이즈는 줄이고"): the chessboard is the mini-games' floor (the kit's PRC_Floor_Checker,
        // light and near-black squares on the world's axes) in 1 m squares, in place of R363 ~ R370's ivory and black
        // marble cells laid round the hub, which kept dotted rings. The pieces under the marble (the spiral's foot, onto
        // the dais) keep the floors' marble (SeamlessFloors). The floating stones, jump pad, vents and hammers keep their
        // own models.
        const string SpiralCheckerMaterial = "PR_SpiralChecker";   // Assets/Art/PawnRushMap/Resources
        const string SpiralNode = "HallV15_B3_Spiral__";

        static void SpiralPaving(Transform hallModel)
        {
            var paving = Resources.Load<Material>(SpiralCheckerMaterial);
            if (paving == null)
            {
                Debug.LogError("[대계단 홀] 나선 체스판 재질(Resources/" + SpiralCheckerMaterial + ".mat)을 불러오지 못했습니다");
                return;
            }
            int paved = 0;
            foreach (var r in hallModel.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.StartsWith(SpiralNode) || r.name.Contains("under marble") || r.name.Contains("under_marble")) continue;
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = paving;
                r.sharedMaterials = slots;
                paved++;
            }
            Debug.Log("[대계단 홀] 나선 길 " + paved + "조각을 미니게임 바닥 체스판(" + paving.GetFloat("_Cell") + " m 칸)으로, 둥근 테는 금 · 옆은 크림 돌");
        }

        // R375 (승규 11-04, a shot of the dais drum's grey pilasters: "원기둥 쪽에서 회색 부분 아직 텍스쳐가 안 입혀졌고 나선
        // 모양 길도 테두리 부분 텍스쳐 입혀줘"): the hall model's last clay trims in PR_PalaceTrim.shader - the dais tower's
        // sixteen pilasters, base and cornice (cream limestone, gold feet and capitals, a gold base fillet and cornice
        // corona; its marble panels are the drum's, SeamlessFloors) and the mouldings round the marble floors' edges (the
        // spiral's foot, the landings, the balcony: cream limestone with a gold top fillet). The spiral way's own border is
        // its shader's (PR_SpiralChecker: a gold rounded edge, limestone sides).
        const string DaisTowerTrim = "PR_DaisTowerTrim", FloorMouldingTrim = "PR_FloorMoulding";   // Assets/Art/PawnRushMap/Resources

        static void PalaceTrims(Transform hallModel)
        {
            var tower = Resources.Load<Material>(DaisTowerTrim);
            var moulding = Resources.Load<Material>(FloorMouldingTrim);
            if (tower == null || moulding == null)
            {
                Debug.LogError("[대계단 홀] 장식 재질(Resources/" + DaisTowerTrim + ".mat, " + FloorMouldingTrim + ".mat)을 불러오지 못했습니다");
                return;
            }
            int towers = 0, mouldings = 0;
            foreach (var r in hallModel.GetComponentsInChildren<Renderer>(true))
            {
                string name = r.name.Replace('_', ' ');
                Material m;
                if (name.StartsWith("HallV15 Royal dais tower")) { m = tower; towers++; }
                else if (name.StartsWith("HallV15 ") && name.EndsWith(" moulding")) { m = moulding; mouldings++; }
                else continue;
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = m;
                r.sharedMaterials = slots;
            }
            Debug.Log("[대계단 홀] 회색 장식 칠함: 단상 탑 " + towers + "개(크림 돌 + 금 띠), 바닥 테 몰딩 " + mouldings + "개(크림 돌 + 금 윗선)");
        }

        // R353: the rolling carpet's model (RollingSpawner) and its materials: the body in the hall carpet, the spiral
        // ends in their own texture, the hubs in the stair's gold.
        internal const string RollingCarpetModel = "PawnRushGrandStairHall/RollingCarpet";
        // R354: the big roll, a faceted white marble pawn with gold rings (pawn_roll_v47.py); its marble the stair's
        // texture through its own UVs, so the veins turn with it.
        internal const string RollingStatueModel = "PawnRushGrandStairHall/RollingStatue";
        static Material rollCarpet, rollEnd, rollMarble;

        internal static Material RollMaterial(string name)
        {
            if (name.StartsWith("Roll_Marble"))
            {
                if (rollMarble == null)
                {
                    rollMarble = FanTextured("RoyalStair_Marble", .78f, 0f);
                    rollMarble.name = "Rolling statue marble";
                }
                return rollMarble;
            }
            if (name.StartsWith("Roll_Carpet"))
            {
                if (rollCarpet == null)
                    rollCarpet = new Material(PieceFigure.Lit(Color.white, .12f)) { name = "Rolling carpet", mainTexture = Resources.Load<Texture2D>(CarpetTexture) };
                return rollCarpet;
            }
            if (name.StartsWith("Roll_End"))
            {
                if (rollEnd == null)
                    rollEnd = new Material(PieceFigure.Lit(Color.white, .1f)) { name = "Rolling carpet end", mainTexture = Resources.Load<Texture2D>("PawnRushGrandStairHall/RollingCarpet_End") };
                return rollEnd;
            }
            return StairMaterial(name);
        }

        // The stair's own materials by their Blender names (the import may add "(Instance)" or a model prefix);
        // Palace_Stone is the walls' (PalaceMaterial), the gold the royal fan's textured gold.
        static Material StairMaterial(string name)
        {
            int cut = name.IndexOf(' ');
            if (cut >= 0) name = name.Substring(0, cut);
            int at = name.LastIndexOf("Stair_", System.StringComparison.Ordinal);
            if (at < 0) return null;
            string part = name.Substring(at + "Stair_".Length);
            if (stairMaterials.TryGetValue(part, out var cached) && cached != null) return cached;
            Material m;
            switch (part)
            {
                case "Marble": m = HallMarble(); break;   // R358: Calacatta Oro, as the hall's floors (was RoyalStair_Marble)
                case "Gold": return FanMaterial("RoyalFan_Gold");
                case "Carpet":
                    m = new Material(PieceFigure.Lit(Color.white, .12f)) { mainTexture = Resources.Load<Texture2D>(CarpetTexture) };
                    if (m.mainTexture == null) Debug.LogError("[대계단 홀] 계단 카펫 텍스처(Resources/" + CarpetTexture + ".png)를 불러오지 못했습니다");
                    break;   // the model's UVs are already in 2 m tiles, as the hall floor's carpet
                case "Velvet": m = new Material(PieceFigure.Lit(new Color(.42f, .03f, .06f), .32f)); break;
                case "Glass":
                    m = LitMaterials.NewLit(new Color(1f, .78f, .48f), .85f);
                    LitMaterials.SetEmission(m, CandleLight * 6f);   // HDR: over the bloom's threshold, as the hub bulbs
                    break;
                case "Iron": m = new Material(PieceFigure.Lit(new Color(.07f, .07f, .075f), .45f, .8f)); break;
                default: return null;
            }
            m.name = "Royal stair " + part;
            return stairMaterials[part] = m;
        }
    }
}
