using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Gameplay
{
    public interface IAbilityUser
    {
        bool AbilitiesEnabled { get; set; }
        float Cooldown01 { get; }
        bool AbilityActive { get; }
        void CancelAbility();
    }
    public interface IBodyState
    {
        Vector3 BodyPosition { get; }
        bool CountsAsBody { get; }
    }
    public interface IKingRushCharacter : ITeamMember, IBodyState, IAbilityUser
    {
        ulong Id { get; }
        KingRushPiece Piece { get; }
        bool FixedKing { get; }
        bool StandingOnPad { get; }
        KingRushSection Section { get; set; }
        Collider[] BodyColliders { get; }
        IHitReceiver HitReceiver { get; }
        bool Promote(KingRushPiece piece);
        void LeaveBlue();
        void ReleaseHoldOn(IKingRushCharacter target);
        Vector3 LaunchOrigin { get; }
        bool LaunchFromCannon(Vector3 velocity, bool enemy);
    }
}
