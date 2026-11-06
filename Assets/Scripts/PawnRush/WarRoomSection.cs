using UnityEngine;

namespace ChessFight.PawnRush
{
    // The root of the final section, the war room (작전 회의실, Assets/Prefabs/PawnRushSections/Final_WarRoom.prefab).
    //
    // R493 (승규 11-06, "맵은 open section test에 넣어주면 되고 작전 회의실은 어디 갔어"): 승규 님's library conveyor course
    // was only zone E of the SeoungTest scene (Course01v4Builder.LibraryReplacement); it is now a section of the round
    // too, in slot 7 (마지막 구간). Its far end is the goal - the capture zone in front of the three rooks on the star
    // platform; the arched door behind them is a picture on the solid rear wall - so nothing can follow it.
    //
    // The room is zone E's own build, under Content (Course01v4Builder.BuildWarRoomSection), made when the section wakes:
    // its look makes materials and moving parts that a prefab asset cannot hold (as the grand stair hall's art), so the
    // prefab holds only this root and is never baked. The SeoungTest scene's zone E is unchanged.
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class WarRoomSection : MonoBehaviour
    {
        public const string Id = "Final_WarRoom";
        public const string ContentName = "Content";

        [SerializeField] CourseKit kit;
        [Tooltip("Build the war room from code when it wakes (the prefab holds only its root).")]
        [SerializeField] bool buildIfEmpty = true;

        public CourseKit Kit => kit;
        public bool HasContent => transform.Find(ContentName) != null;

        void Awake()
        {
            if (buildIfEmpty && !HasContent) Course01v4Builder.BuildWarRoomSection(this);
        }
    }
}
