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

        // JY-skill-lab: a piece that stands on a platform without counting there. The ragdoll side sets it once (the
        // opposite-coloured bishop while enemy bodies pass through it) and CrownFinish asks; null: everyone counts.
        public static System.Func<ICharacterDriver, bool> OffTheCount;

        // JY-skill-lab (the set 2안-a): a team has cleared one of its mini-games (its team, where the games are judged and
        // where a client hears it); the ragdoll side counts them and turns the team's skills to their endgame shapes after
        // the second.
        public static System.Action<int> MiniGameDone;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            PawnRush = false;
            MiniGameDone = null;
        }
    }
}
