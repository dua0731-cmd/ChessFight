using UnityEngine;

namespace ChessFight.PawnRush
{
    // The root of the rooks' rampart walk (룩 성벽길, R843 design v0.1, R859 2D explainer, R860 grey box;
    // Assets/Prefabs/PawnRushSections/Early_1-5_RookRampart.prefab): a draft for the early obstacle slot (slot 1's drafts,
    // after the water garden and the carriage yard), grey-box. The royal castle's outer defences on a rock spur - three
    // curtain walls climbed in switchbacks on the garrison's siege drill day: the siege ladders, the murder holes, the siege
    // towers, the rooks' drill square, the rook and knight cranes over the ditch, the king's banner, the keep bridge.
    // The prefab holds only this root and its PawnRushSection; Awake builds Content from code (RookRampartBuilder), as the
    // grand banquet hall does. Every device's phase is a constant on the shared obstacle clock, so the section's seed is not
    // used. Design: Docs/PawnRush/PAWN_RUSH_ROOK_RAMPART_DESIGN_v0.1.md; numbers: ArtSource/PawnRush/RookRampart/rampart_layout.py.
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class RookRampartSection : MonoBehaviour
    {
        public const string Id = "Early_1-5_RookRampart", DisplayName = "룩 성벽길", ContentName = "Content";

        // Entry: the glacis in front of the outer wall. Exit: the keep's door at the north end of the keep bridge.
        public static readonly Vector3 Entry = Vector3.zero, Exit = new Vector3(0f, RookRampartBuilder.GateY, RookRampartBuilder.KeepZ);

        [Tooltip("Build the rampart from code when it wakes (the prefab holds only its root).")]
        [SerializeField] bool buildIfEmpty = true;

        public bool HasContent => transform.Find(ContentName) != null;

        void Awake()
        {
            if (buildIfEmpty && !HasContent) RookRampartBuilder.Build(this);
        }
    }
}
