using UnityEngine;

namespace ChessFight.PawnRush
{
    // The root of the grand banquet hall (대연회장, R815 design v0.4, R816 grey box;
    // Assets/Prefabs/PawnRushSections/Mid_2-4_GrandBanquetHall.prefab): a draft for the middle obstacle slot (slot 3's drafts,
    // after the royal clock tower), grey-box. A royal banquet-and-ball hall in which the devices are what the place does -
    // the waiters' parade, the magician's tablecloth trick, the silver cloches, the waltz, the stage curtain, the chocolate
    // fountain, the chandeliers and the champagne tower - on one way that crosses the hall three times at three heights.
    // The prefab holds only this root and its PawnRushSection; Awake builds Content from code (GrandBanquetHallBuilder), as
    // the clock tower does. Every device's phase is a constant on the shared obstacle clock, so the section's seed is not
    // used. Design: Docs/PawnRush/PAWN_RUSH_BANQUET_HALL_DESIGN_v0.4.md.
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class GrandBanquetHallSection : MonoBehaviour
    {
        public const string Id = "Mid_2-4_GrandBanquetHall", DisplayName = "대연회장", ContentName = "Content";

        // Entry: the middle of the south doorway, on the floor. Exit: the north end of the king's balcony.
        public static readonly Vector3 Entry = Vector3.zero,
            Exit = new Vector3(0f, GrandBanquetHallBuilder.BalconyY, GrandBanquetHallBuilder.HallZ1);

        [Tooltip("Build the banquet hall from code when it wakes (the prefab holds only its root).")]
        [SerializeField] bool buildIfEmpty = true;

        public bool HasContent => transform.Find(ContentName) != null;

        void Awake()
        {
            if (buildIfEmpty && !HasContent) GrandBanquetHallBuilder.Build(this);
        }
    }
}
