using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R354 (승규 11-04 "이 부분 예고 동작도 vfx 해줘"): the royal balcony's two guard knights (17, GrandStairHallBuilder.Beats.cs
        // Knight) warn with CavalryWarnFx - a filling floor warning where they land and a lane down which they charge - in
        // place of the kit's flat orange and red marks.
        static void GuardKnightWarnings(Transform content)
        {
            int knights = 0;
            foreach (var knight in content.GetComponentsInChildren<ChessFight.ProtectKing.KnightCavalryCharge>(true))
            {
                if (knight.TryGetComponent<CavalryWarnFx>(out _)) continue;
                knight.gameObject.AddComponent<CavalryWarnFx>();
                knights++;
            }
            Debug.Log("[대계단 홀] 근위 기사 예고 효과 " + knights + "개");
        }
    }
}
