namespace ChessFight.Gameplay
{
    // R7 giant's-desk props (ArtSource/PawnRush/RoyalLibraryModels/rl_r7_desk_props_model.py, PLAN.md R7, 11-18): the
    // far props of concept D standing on cliffs of giant leather books, round the course and never over a walking
    // face. Each spot is one FBX written in the course root's frame at its place (rl_r7 PLACES); nothing is covered
    // and the props have no colliders (a player who falls past them lands on the far floor as before).
    //   chess room  C1 ink bottle + quill, C2 pocket watch + chain, C3 teacup, C4 brass desk lamp (off), C5 magnifier,
    //               C6 parchment + fountain pen
    //   front course F1 ink bottle, F2 pocket watch, F3 teacup + magnifier, F4 parchment + pen (0.55 of the size)
    public sealed partial class RoyalLibraryDressing
    {
        static Hang DeskSpot(string key) => new Hang { zone = "Desk", model = "RlDesk" + key };

        static readonly Hang[] Desk =
        {
            DeskSpot("C1"), DeskSpot("C2"), DeskSpot("C3"), DeskSpot("C4"), DeskSpot("C5"), DeskSpot("C6"),
            DeskSpot("F1"), DeskSpot("F2"), DeskSpot("F3"), DeskSpot("F4"),
        };
    }
}
