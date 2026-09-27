using ChessFight.Network;

namespace ChessFight.Gameplay
{
    // A character that can be promoted (Queen of the Hill M11): a pawn that
    // reaches the gate becomes a rook, a bishop, a knight or its team's king
    // (ChessPieces.CanPromote). The piece changes the character's multipliers and
    // look, never its body. Looked up next to the driver; only the machine that
    // simulates the character promotes it, the others are told by the snapshot.
    public interface IPromotable
    {
        PieceKind Piece { get; }

        void Promote(PieceKind piece);
    }
}
