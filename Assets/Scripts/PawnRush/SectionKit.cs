using UnityEngine;

namespace ChessFight.PawnRush
{
    // The section prefabs' kit (Assets/Settings/PawnRushSectionKit.asset): the section level designs' blockout
    // look ("1 m 격자. 걷는 바닥은 파란 격자, 오를 수 없는 면은 회색 격자 + NoClimbSurface, 장애물은 주황,
    // 체크포인트·쉼터는 하늘색") and the same imported obstacle prefabs as course 01.
    [CreateAssetMenu(menuName = "ChessFight/Pawn Rush/Section Kit", fileName = "PawnRushSectionKit")]
    public sealed class SectionKit : CourseKit
    {
    }
}
