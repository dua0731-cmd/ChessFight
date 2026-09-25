using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Lets the ragdoll be driven by anything that speaks ICharacterDriver: the offline
    /// playtest now, the network host and bots later. The lab's own LabGame keeps calling
    /// RagdollPawn.SetInput directly; both paths end in the same latched input.
    /// </summary>
    [RequireComponent(typeof(RagdollPawn))]
    public sealed class RagdollDriver : MonoBehaviour, ICharacterDriver
    {
        RagdollPawn pawn;

        RagdollPawn Pawn => pawn != null ? pawn : (pawn = GetComponent<RagdollPawn>());

        // CharacterCommand has the same shape as PawnInput on purpose.
        public void SetCommand(in CharacterCommand command) => Pawn.SetInput(new PawnInput
        {
            move = command.Move, jump = command.Jump, shove = command.Shove, grab = command.Grab
        });

        public Transform FollowTarget => Pawn.Hips.transform;

        // `position` is the ground point; the hips go standHeight above it, as LabGame.Respawn does.
        public void Teleport(Vector3 position, Quaternion rotation) =>
            Pawn.Teleport(position + Vector3.up * (Pawn.standHeight + 0.02f), rotation * Vector3.forward);
    }
}
