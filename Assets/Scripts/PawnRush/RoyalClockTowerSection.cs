using UnityEngine;

namespace ChessFight.PawnRush
{
    // The root of the royal clock tower (왕실 시계탑, R775; Assets/Prefabs/PawnRushSections/Mid_2-2_RoyalClockTower.prefab): a
    // draft for the middle obstacle slot (slot 3's drafts), grey-box. The first section whose walls are the way: no stairs,
    // every climb ends on something to stand on. The prefab holds only this root and its PawnRushSection; Awake builds
    // Content from code (RoyalClockTowerBuilder), as the royal vault does. No kit field and no ISectionPart: every machine's
    // phase is a constant on the shared obstacle clock, so the section's seed is not used.
    // Design: Docs/PawnRush/PAWN_RUSH_ROYAL_CLOCK_TOWER_DESIGN_v0.1.md.
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class RoyalClockTowerSection : MonoBehaviour
    {
        public const string Id = "Mid_2-2_RoyalClockTower", DisplayName = "왕실 시계탑", ContentName = "Content";

        // Entry: the middle of the 12 m threshold, on the floor. Exit: the north end of the exit terrace.
        public static readonly Vector3 Entry = Vector3.zero,
            Exit = new Vector3(0f, RoyalClockTowerBuilder.ExitY, RoyalClockTowerBuilder.ExitZ);

        [Tooltip("Build the clock tower from code when it wakes (the prefab holds only its root).")]
        [SerializeField] bool buildIfEmpty = true;

        public bool HasContent => transform.Find(ContentName) != null;

        void Awake()
        {
            if (buildIfEmpty && !HasContent) RoyalClockTowerBuilder.Build(this);
        }
    }
}
