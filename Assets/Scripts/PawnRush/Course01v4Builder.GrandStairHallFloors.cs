using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R358 (승규 11-04, floor options floor_marble_options_v38.py -> "A로 해주고 계단도 바꿔줘. 문양 의미도 모델 다체
        // 이음새 지워"): the hall's marble - its floors and the grand stair (GrandStairHallStair's "Marble") - in Calacatta
        // Oro (PR_VeinedMarble.shader: bold grey veins haloed in gold, fine veins, polished, drawn in world space so it
        // needs no UVs and never repeats), and the floors without their joints.
        const string FloorMarble = "PR_CalacattaOro";   // Assets/Art/PawnRushMap/Resources
        static Material hallMarble;

        static Material HallMarble()
        {
            if (hallMarble != null) return hallMarble;
            hallMarble = Resources.Load<Material>(FloorMarble);
            if (hallMarble == null)
            {
                Debug.LogError("[대계단 홀] 대리석 재질(Resources/" + FloorMarble + ".mat)을 불러오지 못했습니다");
                hallMarble = PieceFigure.Lit(new Color(.92f, .91f, .88f), .8f);
            }
            return hallMarble;
        }

        // The model's marble floors are 1 m slabs with 6 mm joints (and rings of curved slabs on the dais) laid on the
        // blockout box lowered by their 3 cm (floor_marble_v12.py). Without the joints: the slab meshes are switched
        // off and each box under them is drawn in the marble and raised the 3 cm back to the walked top (its collider,
        // the blockout's, is where it was). The middle landing's box stays: the grand stair's own model covers it (R350).
        const string SlabFloor = "__Marble_floor", UnderMarble = "(under marble)";
        const float SlabThick = .03f;

        static void SeamlessFloors(Transform model, Material marble)
        {
            int slabs = 0, boxes = 0;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith(SlabFloor))
                {
                    t.gameObject.SetActive(false);
                    slabs++;
                }
                else if (t.name.Contains(UnderMarble) && !t.name.Contains("Middle_landing") && t.TryGetComponent<Renderer>(out var r))
                {
                    var slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = marble;
                    r.sharedMaterials = slots;
                    t.localPosition += Vector3.up * SlabThick;   // the pivot only turns about y: up is up
                    boxes++;
                }
            }
            Debug.Log("[대계단 홀] 대리석 바닥 이음새 없앰: 판석 모델 " + slabs + "개 끔, 아래 상자 " + boxes + "개를 대리석으로 3 cm 올림");
        }
    }
}
