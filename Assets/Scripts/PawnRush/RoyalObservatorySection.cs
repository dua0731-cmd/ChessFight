using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Observatory, a late obstacle course (R711 level design v0.1, R712 graybox): one way round the great
    // orrery under a dome - the ring court, the star-chart belts and shutter shields, the vent grating bridge, the
    // orrery's turntable, the planet lifts, the telescope bridge, the constellation wall, the exit terrace.
    // Design: Docs/PawnRush/PAWN_RUSH_ROYAL_OBSERVATORY_DESIGN_v0.1.md.
    //
    //   Late_3-2_RoyalObservatory (RoyalObservatorySection, PawnRushSection)
    //     Content        built from code when the prefab wakes (RoyalObservatoryBuilder), as the war room is
    //
    // A graybox: boxes and the existing devices, no art. It is a DRAFT of the section set's late obstacle slot
    // (PawnRushSectionSet.Slot.drafts): the course prefab test lists it, a round does not draw it.
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class RoyalObservatorySection : MonoBehaviour
    {
        public const string Id = "Late_3-2_RoyalObservatory";
        public const string DisplayName = "왕실 천문대 (회색 상자)";
        public const string ContentName = "Content";

        [SerializeField] CourseKit kit;

        public CourseKit Kit => kit;
        public bool HasContent => transform.Find(ContentName) != null;

        void Awake()
        {
            if (!HasContent) RoyalObservatoryBuilder.Build(this);
        }
    }
}
