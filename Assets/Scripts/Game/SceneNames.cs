namespace ChessFight.Game
{
    // Scene names used by SceneManager.LoadScene. Every scene loaded at run time
    // must also be listed in File > Build Profiles (EditorBuildSettings.asset).
    public static class SceneNames
    {
        public const string Intro = "Intro";
        public const string Lobby = "Lobby";
        public const string KingRush = "KingRush";
        // Development only: opened directly in the Editor, never loaded at run time.
        public const string RagdollTest = "RagdollTest";
        // Queen of the Hill graybox: offline playtest only until the networked ragdoll
        // (MECHANICS_TODO M14) lets GameModes.QueenOfTheHill load it for a match.
        public const string QueenOfTheHill = "QueenOfTheHill";
    }
}
