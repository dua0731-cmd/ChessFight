namespace ChessFight.Game
{
    // Scene names used by SceneManager.LoadScene. Every scene loaded at run time
    // must also be listed in File > Build Profiles (EditorBuildSettings.asset).
    public static class SceneNames
    {
        public const string Intro = "Intro";
        public const string Lobby = "Lobby";
        // The 폰 러쉬 match (GameModes.KingRush keeps its old key). Was the KingRush capsule scene until 10-08.
        public const string PawnRushCourse01 = "PawnRush_Course01";
        public const string SwordFight = "SwordFight";
        // Development only: opened directly in the Editor, never loaded at run time.
        public const string RagdollTest = "RagdollTest";
        // Queen of the Hill graybox: offline playtest only until the networked ragdoll
        // (MECHANICS_TODO M14) lets GameModes.QueenOfTheHill load it for a match.
        public const string QueenOfTheHill = "QueenOfTheHill";
        // The Pawn Rush result scenes (R76, in place of R62's LastScene): the winners' and the
        // losers'. Previewed on their own for now; the match flow does not load them until the
        // team decides where the result plays.
        public const string PawnRushVictory = "PawnRushVictory";
        public const string PawnRushLose = "PawnRushLose";
    }
}
