using System;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // A promotion pedestal at the gate (Queen of the Hill M11, DESIGN §6.2): a
    // pawn in reach presses the interact key and becomes this pedestal's piece.
    // The rules are ChessPieces.CanPromote: only a pawn, never the queen, and one
    // king per team. A pedestal with `demote` turns any piece back into a pawn
    // (test beds only).
    //
    // Needs a collider the character's reach search can find (a trigger is fine).
    public sealed class PromotionPad : MonoBehaviour, IInteractable
    {
        [SerializeField] PieceKind piece = PieceKind.Rook;
        [Tooltip("Test beds only: any piece back to a pawn.")]
        [SerializeField] bool demote;

        public PieceKind Piece => piece;
        public bool Demotes => demote;
        public int Promotions { get; private set; }

        // Every promotion here: who, and into what.
        public event Action<ICharacterDriver, PieceKind> Promoted;

        public void Configure(PieceKind piece, bool demote = false)
        {
            this.piece = piece;
            this.demote = demote;
        }

        public bool Interact(ICharacterDriver who, int team)
        {
            if (!(who is IPromotable character)) return false;
            PieceKind to = demote ? PieceKind.Pawn : piece;
            if (demote ? character.Piece == PieceKind.Pawn : !ChessPieces.CanPromote(character.Piece, piece, KingsIn(team)))
                return false;
            character.Promote(to);
            Promotions++;
            Promoted?.Invoke(who, to);
            return true;
        }

        // Kings on `team` now. Promotions are rare, so a scene search is fine.
        static int KingsIn(int team)
        {
            int kings = 0;
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (behaviour is IPromotable p && p.Piece == PieceKind.King
                    && (behaviour is ITeamMember member ? member.Team : Teams.None) == team)
                    kings++;
            }
            return kings;
        }
    }
}
