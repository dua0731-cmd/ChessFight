using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Lets the ragdoll be driven by anything that speaks ICharacterDriver: the offline
    /// playtest now, the network host and bots later. The lab's own LabGame keeps calling
    /// RagdollPawn.SetInput directly; both paths end in the same latched input. Attacks reach
    /// the pawn the same way, through IHitReceiver.
    /// </summary>
    [RequireComponent(typeof(RagdollPawn))]
    public sealed class RagdollDriver : MonoBehaviour, ICharacterDriver, IHitReceiver
    {
        RagdollPawn pawn;

        public RagdollPawn Pawn => pawn != null ? pawn : (pawn = GetComponent<RagdollPawn>());

        // CharacterCommand has the same shape as PawnInput on purpose.
        public void SetCommand(in CharacterCommand command) => Pawn.SetInput(new PawnInput
        {
            move = command.Move, jump = command.Jump, shove = command.Shove, grab = command.Grab,
            sprint = command.Sprint, ability = command.Ability, ability2 = command.Ability2,
            interact = command.Interact, aim = command.Aim
        });

        public Transform FollowTarget => Pawn.Hips.transform;

        // `position` is the ground point; the hips go standHeight above it, as LabGame.Respawn does.
        // RagdollPawn.Teleport lets go of everything first (wall, hands, anyone holding it).
        public void Teleport(Vector3 position, Quaternion rotation) =>
            Pawn.Teleport(position + Vector3.up * (Pawn.standHeight + 0.02f), rotation * Vector3.forward);

        public void ApplyHit(Vector3 push, float knockdownSeconds, float staminaDamage, bool dropFromWallOrRide) =>
            Pawn.TakeHit(push, knockdownSeconds, staminaDamage, dropFromWallOrRide);
    }
}
