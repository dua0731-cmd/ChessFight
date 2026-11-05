using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R351 (승규 11-04 "선풍기 vfx 적용 시켜줘 데칼 말고" + "위치도 길 쪽에 자연스럽게 배치해주고"): the windy way's two royal
        // fans. Their stone ledges and consoles (GrandStairHallBuilder.Beats.cs: out of the way's inner side, the fan's face
        // on its edge) in the walls' cream limestone with a gold fillet along the ledge's inner top edge; each vent its wind
        // on its own clock (VentWindFx), in place of the kit's gold rods.
        static void WindyWay(Transform content)
        {
            var stone = PalaceMaterial("Palace_Stone");
            var gold = PalaceMaterial("Palace_Gold");
            int ledges = 0, winds = 0;
            foreach (var t in content.GetComponentsInChildren<Transform>(true))
            {
                bool ledge = t.name.StartsWith("Wind vent ledge ");
                if (!ledge && !t.name.StartsWith("Wind vent console ")) continue;
                if (t.TryGetComponent<Renderer>(out var drawn)) drawn.sharedMaterial = stone;
                if (!ledge || t.Find("Gold fillet") != null) continue;
                // In the ledge's unit cube: z from the hub (-) to the way's edge (+), y up, x along the way.
                var fillet = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fillet.name = "Gold fillet";
                Object.DestroyImmediate(fillet.GetComponent<Collider>());
                fillet.transform.SetParent(t, false);
                fillet.transform.localPosition = new Vector3(0f, .5f, -.5f);
                fillet.transform.localScale = new Vector3(1.01f, .1f, .08f);
                fillet.GetComponent<Renderer>().sharedMaterial = gold;
                ledges++;
            }
            foreach (var vent in content.GetComponentsInChildren<ChessFight.ProtectKing.AirVent>(true))
            {
                if (vent.TryGetComponent<VentWindFx>(out _)) continue;
                vent.gameObject.AddComponent<VentWindFx>();
                winds++;
            }
            Debug.Log("[대계단 홀] 환풍기 받침 " + ledges + "개, 바람 효과 " + winds + "개");
        }
    }
}
