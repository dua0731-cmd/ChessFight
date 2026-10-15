using ChessFight.Gameplay;
using ChessFight.Network;
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
    public sealed class RagdollDriver : MonoBehaviour, ICharacterDriver, IHitReceiver, ITeamMember, ITeamAssignable, IStaminaReadout, ILaunchable, IStatusReceiver, IPromotable, IMiniGamePawn
    {
        RagdollPawn pawn;

        public RagdollPawn Pawn => pawn != null ? pawn : (pawn = GetComponent<RagdollPawn>());

        // CharacterCommand has the same shape as PawnInput on purpose.
        public void SetCommand(in CharacterCommand command) => Pawn.SetInput(new PawnInput
        {
            move = command.Move, jump = command.Jump, shove = command.Shove, grab = command.Grab,
            sprint = command.Sprint, ability = command.Ability, ability2 = command.Ability2,
            interact = command.Interact, aim = command.Aim, shoveHeld = command.ShoveHeld,
            skill = command.Skill,
        });

        public Transform FollowTarget => Pawn.Hips.transform;

        public int Team => Pawn.Team;

        // Set by whoever spawns the pawn outside the lab (the offline playtest of a mode scene).
        public void AssignTeam(int team) => Pawn.Team = team;

        public float Stamina01 => Pawn.Stamina;

        // `position` is the ground point; the hips go standHeight above it, as LabGame.Respawn does.
        // RagdollPawn.Teleport lets go of everything first (wall, hands, anyone holding it).
        public void Teleport(Vector3 position, Quaternion rotation) =>
            Pawn.Teleport(position + Vector3.up * (Pawn.standHeight + 0.02f), rotation * Vector3.forward);

        public void ApplyHit(Vector3 push, float knockdownSeconds, float staminaDamage, bool dropFromWallOrRide) =>
            Pawn.TakeHit(push, knockdownSeconds, staminaDamage, dropFromWallOrRide);

        // A launch pad or a spring lift (M7), worked out from where the feet would be standing.
        public Vector3 LaunchFrom => Pawn.Hips.position - Vector3.up * Pawn.standHeight;

        public void Launch(Vector3 velocity) => Pawn.Launch(velocity);

        // Status effects (M13).
        public bool Squash(float seconds, float immunity) => Pawn.Squash(seconds, immunity);
        public bool Stagger(float seconds) => Pawn.Stagger(seconds);

        // Pawn Rush mini-games v1.0 (R126).
        public bool Down => Pawn.State != PawnState.Active && !Pawn.Diving;
        public bool Diving => Pawn.Diving;
        public Vector3 MoveInput => Pawn.GivenMove;
        public bool SprintHeld => Pawn.SprintHeld;
        public bool Sprinting => Pawn.Sprinting;
        public void SpendStamina(float amount) => Pawn.SpendStamina(amount);
        public Vector3 HipsVelocity => Pawn.Hips.linearVelocity;
        public Collider Held(int hand) => hand == 0 ? (Pawn.handL != null ? Pawn.handL.HeldCollider : null)
                                                   : (Pawn.handR != null ? Pawn.handR.HeldCollider : null);
        public Vector3 HandPosition(int hand) => hand == 0 ? (Pawn.handL != null ? Pawn.handL.Center : Pawn.Hips.position)
                                                           : (Pawn.handR != null ? Pawn.handR.Center : Pawn.Hips.position);
        public bool SkillDashing => Pawn.SkillDashing;
        public int SkillUses => Pawn.SkillUses;
        public float KnightLandedAt { get; private set; } = -1f;
        public Vector3 KnightLandedWhere { get; private set; }
        public void AddVelocity(Vector3 change) => Pawn.AddVelocity(change);
        public void HoldStill(float seconds) => Pawn.HoldStill(seconds);

        void OnEnable() => RagdollPawn.SkillFx += OnSkillFx;
        void OnDisable() => RagdollPawn.SkillFx -= OnSkillFx;

        void OnSkillFx(SkillFxEvent e)
        {
            if (e.by != Pawn || (e.kind != SkillFxKind.KnightLand && e.kind != SkillFxKind.KnightStomp && e.kind != SkillFxKind.KnightHome)) return;
            KnightLandedAt = Time.time;
            KnightLandedWhere = Pawn.Hips.position;
        }

        // Promotion (M11).
        public PieceKind Piece => Pawn.Piece;
        public void Promote(PieceKind piece)
        {
            Pawn.SetPiece(piece);
            // With the Pawn Rush skills (course 01's pads, R125) the new piece starts its own skill fresh.
            if (Pawn.PawnRushSkills != null) Pawn.ResetSkill();
        }
    }
}
