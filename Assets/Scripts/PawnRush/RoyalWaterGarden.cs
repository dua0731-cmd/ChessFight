using UnityEngine;

namespace ChessFight.PawnRush
{
    // The root of the early obstacle section 1-1, the royal water garden v31 (Assets/Prefabs/PawnRushSections/
    // Early_1-1_RoyalWaterGarden.prefab; Docs/KingRush/PAWN_RUSH_ROYAL_GARDEN.md). The garden 승규 님 built in Blender
    // (seoungtest, v30) ran straight north in 112 m; the user asked for it longer and folded, the big spin bar cut into
    // small ones, a very hard shortcut off the platforms and boats crossing sideways. This is that layout as a graybox.
    //
    // The garden is built from code (RoyalWaterGardenBuilder). The menu ChessFight > Pawn Rush > Build Early 1-1 Royal
    // Water Garden bakes it into the prefab; a prefab that does not hold it yet builds it when it wakes, so it can be
    // played straight from git. The boats take their lanes from the round's seed (BoatLanes, an ISectionPart).
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class RoyalWaterGarden : MonoBehaviour
    {
        public const string Id = "Early_1-1_RoyalWaterGarden";
        public const string ContentName = "Content";

        [SerializeField] CourseKit kit;
        [Tooltip("Build the garden from code when the prefab does not hold it yet (the Build menu bakes it in).")]
        [SerializeField] bool buildIfEmpty = true;

        public CourseKit Kit => kit;
        public bool HasContent => transform.Find(ContentName) != null;

        void Awake()
        {
            if (buildIfEmpty && !HasContent && kit != null) RoyalWaterGardenBuilder.Build(this);
        }
    }
}
