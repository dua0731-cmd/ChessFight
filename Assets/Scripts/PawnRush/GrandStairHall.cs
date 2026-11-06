using UnityEngine;

namespace ChessFight.PawnRush
{
    // The root of the mid-game obstacle section 2-1, the grand stair hall (Assets/Prefabs/PawnRushSections/
    // Mid_2-1_GrandStairHall.prefab; level design v0.3, Docs/PawnRush/PAWN_RUSH_GRAND_STAIR_HALL_DESIGN_v0.3.md,
    // reshaped into one spiral way in R303).
    //
    // The hall is built from code (GrandStairHallBuilder). The menu ChessFight > Pawn Rush > Build Mid 2-1 Grand
    // Stair Hall bakes it into the prefab; a prefab that does not hold it yet builds it when it wakes, so it can be
    // played straight from git. Since R303 there is one way for every team size (no wings to shut); the parts that
    // need the round's seed (the rolls, the chandeliers, the glass stones) are ISectionParts of their own.
    //
    // R309 (승규 11-05, the hall's art finished on seoungtest - its R308 ~ R413 there: "대계단 현관홀만 다른 사람이 이어서
    // 작업 할 수 있게 main에다가 넣어"): when it wakes it also puts on its art (Course01v4Builder.DressGrandStairHall:
    // the Blender hall, its marble, stone and gold, the stair, statues, balcony, entrance, fans, hammers, the hazards'
    // effects). The art makes its own materials and meshes, which a prefab asset cannot hold, so it is put on in Play,
    // never baked. A prefab baked by an older builder (no GrandStairHallBuilder.LayoutMark under its Content) is built
    // again first, as the art needs the current blockout (the spiral foot, the vents' ledges, the queen's blocks...).
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class GrandStairHall : MonoBehaviour
    {
        public const string Id = "Mid_2-1_GrandStairHall";
        public const string ContentName = "Content";

        [SerializeField] CourseKit kit;
        [Tooltip("Build the hall from code when the prefab does not hold it yet (the Build menu bakes it in).")]
        [SerializeField] bool buildIfEmpty = true;
        [Tooltip("Put the hall's art (models, materials, effects) on in Play (R309).")]
        [SerializeField] bool dressInPlay = true;

        public CourseKit Kit => kit;
        public bool HasContent => transform.Find(ContentName) != null;
        public bool HasCurrentLayout
        {
            get
            {
                var content = transform.Find(ContentName);
                return content != null && content.Find(GrandStairHallBuilder.LayoutMark) != null;
            }
        }

        void Awake()
        {
            if (kit == null) return;
            if (buildIfEmpty && !HasCurrentLayout) GrandStairHallBuilder.Build(this);
            if (dressInPlay && HasContent) Course01v4Builder.DressGrandStairHall(this);
        }
    }
}
