using UnityEngine;

namespace ChessFight.PawnRush
{
    // Course 01's kit (Assets/Settings/Course01Kit.asset): the course materials and the imported obstacle
    // prefabs. The fields live in CourseKit, which the section prefabs' kit shares.
    [CreateAssetMenu(menuName = "ChessFight/Pawn Rush/Course Kit", fileName = "Course01Kit")]
    public sealed class Course01Kit : CourseKit
    {
        // seoungtest (R297): the grand stair hall the test scene stands beside the garden (zone H).
        public GameObject grandStairHall;
        // seoungtest (R307): main's royal water garden v31 (Early_1-1_RoyalWaterGarden), the layout zone G's models sit on.
        public GameObject royalWaterGarden;
    }
}
