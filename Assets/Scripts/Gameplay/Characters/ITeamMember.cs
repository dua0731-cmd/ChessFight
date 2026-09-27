namespace ChessFight.Gameplay
{
    // A character's side in a team mode: white or black, the colours of the chess
    // pieces the two teams are (Queen of the Hill M10). The ragdoll's RagdollDriver
    // implements it; team rules (en passant only on enemies, a bell's head start
    // for the team that rang it, team checkpoints) ask through it.
    public interface ITeamMember
    {
        int Team { get; }
    }

    // A character whose side is set by whoever spawns it (a playtest, the network
    // roster, a mode). Only the machine that simulates it needs this.
    public interface ITeamAssignable
    {
        void AssignTeam(int team);
    }

    // Stamina for a HUD, 0..1 (empty = the hands let go). Read only.
    public interface IStaminaReadout
    {
        float Stamina01 { get; }
    }

    public static class Teams
    {
        public const int None = -1, White = 0, Black = 1;

        // Different sides. A character with no side (a lab dummy, a test pawn) is
        // everyone's enemy, so a lab with no teams still has hooks to cut.
        public static bool AreEnemies(int a, int b) => a == None || b == None || a != b;

        public static string Name(int team) => team == White ? "백" : team == Black ? "흑" : "없음";
    }
}
