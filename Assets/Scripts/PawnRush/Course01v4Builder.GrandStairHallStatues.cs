using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R345 (승규 11-04, "4개동상 전부 받침대는 다른 텍스쳐로", "흰 대리석 + 짙은 녹색 대리석", "시선을 … 계단 쪽으로 …
        // 아주 조금"): the four statues beside the grand stair painted by their shape (stair_statues_v36.py) and turned
        // 10 degrees further towards the stair, on new octagonal plinths with gold fillets, frames and rosettes. They
        // leave the hall model for their own FBX (same frame, under the hall's pivot); the hall's statue and plinth
        // nodes are switched off. The meshes have no UVs, so both stones are PR_PalaceStone.shader's world-space stone
        // in other colours: Statue_Marble white with grey veins, Plinth_Marble (the pedestals) in PR_Travertine - R347
        // (승규 11-04, "받침대 색이 마음에 안들어 시안 줘" -> six options, pedestal_colors_v37.py -> "c로"): a warm honed
        // cream travertine with honey veins, a shade deeper than the walls, in place of R345's deep green.
        const string StatuesModel = "PawnRushGrandStairHall/StairStatuesV36";
        const string HallStatueNode = "HallV15_Stair_statue_";

        static void StairStatues(Transform pivot, Transform hallModel, Material clay)
        {
            var model = Resources.Load<GameObject>(StatuesModel);
            if (model == null)
            {
                Debug.LogError("[대계단 홀] 계단 옆 동상 모델을 불러오지 못했습니다: Resources/" + StatuesModel + ".fbx");
                return;
            }
            int off = 0;
            foreach (var t in hallModel.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith(HallStatueNode)) continue;
                t.gameObject.SetActive(false);
                off++;
            }
            var view = Object.Instantiate(model, pivot, false);
            view.name = "Stair statues v36 model";
            var white = StatueMarble("PR_StatueMarble", new Color(.95f, .94f, .92f), .72f);
            var pedestal = StatueMarble("PR_Travertine", new Color(.88f, .79f, .64f), .45f);
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string name = slots[i] != null ? slots[i].name : "";
                    slots[i] = name.StartsWith("Statue_Marble") ? white : name.StartsWith("Plinth_Marble") ? pedestal
                             : PalaceMaterial(name) ?? clay;
                }
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            Debug.Log("[대계단 홀] 계단 옆 동상 " + view.transform.childCount + "개 조각(흰 대리석 · 트래버틴 받침), 홀 모델의 옛 동상 " + off + "개 끔");
        }

        static Material StatueMarble(string resource, Color fallback, float smoothness)
        {
            var m = Resources.Load<Material>(resource);
            if (m != null) return m;
            Debug.LogError("[대계단 홀] 동상 재질(Resources/" + resource + ".mat)을 불러오지 못했습니다");
            return PieceFigure.Lit(fallback, smoothness);
        }
    }
}
