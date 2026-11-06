using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R400 (승규 11-04, "맵을 다 리모델링 해야 하니 물 그래픽을 제외하고 완전 초기 상태"): zone G is the section prefab
        // Early_1-1_RoyalWaterGarden as it stands in Assets/Prefabs/PawnRushSections - the v31 graybox with the garden's
        // water (RoyalWaterGardenBuilder), remodelled by hand there. The Blender garden model (R307-R309) is no longer
        // laid over it.
        static void RoyalGarden(CourseBuilder b)
        {
            var prefab = (b.kit as Course01Kit)?.royalWaterGarden;
            if (prefab == null) throw new System.Exception("Course01Kit.royalWaterGarden이 비어 있습니다");
            // Built inactive, so nothing inside wakes up before it is in place.
            var holder = new GameObject("Royal garden layout");
            holder.transform.SetParent(b.parent, false);
            holder.SetActive(false);
            try
            {
                var section = CourseBuilder.Spawn(prefab, holder.transform);
                section.transform.localPosition = Vector3.zero;
                section.transform.localRotation = Quaternion.identity;
                var garden = section.GetComponent<RoyalWaterGarden>();
                if (garden != null && !garden.HasContent) RoyalWaterGardenBuilder.Build(garden);
            }
            finally { holder.SetActive(true); }
        }
    }
}
