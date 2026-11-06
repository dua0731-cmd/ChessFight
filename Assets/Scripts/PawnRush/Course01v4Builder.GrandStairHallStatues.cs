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

        // R413 (승규 11-05, a shot of the central landing's grey king statue and a Tripo queen statue: "교체 해주고 발판
        // 지우고 텍스쳐 입혀"): the landing's statue is the queen (queen_statue_v52.py: her square pedestal cut off, 6 m,
        // foot on the landing at (0, 8, 63), facing the stair), in the stair statues' white marble with her crown, orb
        // and crossed maces in the walls' gold. The hall model's king statue is switched off; the blockout under it
        // (GrandStairHallBuilder.Beats CentralLanding) is her shape.
        const string QueenModel = "PawnRushGrandStairHall/QueenStatue";

        static void LandingQueen(Transform hallModel, Transform content, Material clay)
        {
            var model = Resources.Load<GameObject>(QueenModel);
            var body = FindUnder(content, "King statue body");
            if (model == null || body == null)
            {
                Debug.LogError("[대계단 홀] 퀸 석상을 놓지 못했습니다: Resources/" + QueenModel + ".fbx 또는 블록아웃 King statue body 없음");
                return;
            }
            foreach (var t in hallModel.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("HallV15_King statue")) t.gameObject.SetActive(false);
            var view = Object.Instantiate(model, body.parent, false);
            view.name = "Queen statue v52 model";
            view.transform.localPosition = new Vector3(0f, 8f, 63f);
            view.transform.localRotation = Quaternion.identity;   // her front (Blender -Y) on the section's -z: the stair
            var white = StatueMarble("PR_StatueMarble", new Color(.95f, .94f, .92f), .72f);
            var gold = PalaceMaterial("Palace_Gold");
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string name = slots[i] != null ? slots[i].name : "";
                    slots[i] = name.StartsWith("Queen_Gold") ? gold : name.StartsWith("Queen_Marble") ? white : clay;
                }
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var c in view.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            Debug.Log("[대계단 홀] 계단참 석상 = 퀸(흰 대리석 + 금 왕관 · 홀), 옛 왕 석상 끔");
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
