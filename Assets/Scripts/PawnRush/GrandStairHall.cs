using UnityEngine;

namespace ChessFight.PawnRush
{
    // The root of the mid-game obstacle section 2-1, the grand stair hall (Assets/Prefabs/PawnRushSections/
    // Mid_2-1_GrandStairHall.prefab; level design v0.3, Docs/KingRush/PAWN_RUSH_GRAND_STAIR_HALL_DESIGN_v0.3.md,
    // reshaped into one spiral way in R303).
    //
    // The hall is built from code (GrandStairHallBuilder). The menu ChessFight > Pawn Rush > Build Mid 2-1 Grand
    // Stair Hall bakes it into the prefab; a prefab that does not hold it yet builds it when it wakes, so it can be
    // played straight from git. Since R303 there is one way for every team size (no wings to shut); the parts that
    // need the round's seed (the rolls, the chandeliers, the glass stones) are ISectionParts of their own.
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class GrandStairHall : MonoBehaviour
    {
        public const string Id = "Mid_2-1_GrandStairHall";
        public const string ContentName = "Content";

        [SerializeField] CourseKit kit;
        [Tooltip("Build the hall from code when the prefab does not hold it yet (the Build menu bakes it in).")]
        [SerializeField] bool buildIfEmpty = true;

        public CourseKit Kit => kit;
        public bool HasContent => transform.Find(ContentName) != null;

        void Awake()
        {
            if (buildIfEmpty && !HasContent && kit != null) GrandStairHallBuilder.Build(this);
        }
    }
}
