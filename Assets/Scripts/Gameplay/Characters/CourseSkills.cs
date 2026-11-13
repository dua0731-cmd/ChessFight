using UnityEngine;

namespace ChessFight.Gameplay
{
    // A scene that plays the Pawn Rush piece skills outside their test scene (R125: Pawn Rush course 01, offline
    // and online). The course sets it while it is loaded; the ragdoll side (ChessFight.RagdollLab,
    // PawnRushCourseSkills) then gives every pawn its skills, and the offline playtest sends F as the skill key
    // (CharacterCommand.Skill) instead of interact. Gameplay knows nothing of the skills themselves.
    public static class CourseSkills
    {
        public static bool PawnRush;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => PawnRush = false;
    }
}
