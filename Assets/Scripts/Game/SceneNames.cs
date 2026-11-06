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
        // The Pawn Rush result scenes (R76, in place of R62's LastScene): the winners' and the
        // losers'. Since R125 the race loads them when a team has a majority on the crown
        // platform (PawnRushResultFlow), and they go back to the lobby.
        public const string PawnRushVictory = "PawnRushVictory";
        public const string PawnRushLose = "PawnRushLose";
    }
}
