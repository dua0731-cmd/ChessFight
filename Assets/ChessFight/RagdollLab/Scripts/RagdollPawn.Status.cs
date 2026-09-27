using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Status effects (Queen of the Hill M13; RagdollDriver forwards IStatusReceiver here). Squashed: no
    /// moving, jumping, grabbing, diving or sprinting, off any wall, rope or hook, bowed over (the one-piece
    /// legs cannot crouch: pulling the hips down only pressed them into the floor); then a while of
    /// immunity to the next squash. Staggered: the same loss of control for a moment, standing,
    /// swaying. A pawn with StatusImmune (the king, M11) shrugs both off.
    /// </summary>
    public partial class RagdollPawn
    {
        float squashTimer, squashImmune, staggerTimer;

        public bool Squashed => squashTimer > 0f;
        public bool Staggered => staggerTimer > 0f;
        public float SquashLeft => Mathf.Max(0f, squashTimer);
        public float SquashImmuneLeft => Mathf.Max(0f, squashImmune);
        public float StaggerLeft => Mathf.Max(0f, staggerTimer);
        public int Squashes { get; private set; }
        public int Staggers { get; private set; }

        /// <summary>Squashes and staggers do not take (the king's passive, M11).</summary>
        public bool StatusImmune { get; set; }

        public bool Squash(float seconds, float immunity)
        {
            if (NetworkPuppet || StatusImmune || seconds <= 0f || squashImmune > 0f || State == PawnState.Ragdoll) return false;
            LetGo(0.3f);
            squashTimer = seconds;
            squashImmune = seconds + Mathf.Max(0f, immunity);
            Squashes++;
            return true;
        }

        public bool Stagger(float seconds)
        {
            if (NetworkPuppet || StatusImmune || seconds <= 0f || State == PawnState.Ragdoll) return false;
            staggerTimer = Mathf.Max(staggerTimer, seconds);
            // A swing in progress goes back in the hand: with the button forced up it would be thrown.
            if (hookPhase == HookPhase.Charging) DropHook();
            Staggers++;
            return true;
        }

        void UpdateStatus(float dt)
        {
            squashTimer -= dt;
            squashImmune -= dt;
            staggerTimer -= dt;
            if (!Squashed && !Staggered) return;
            // No control: the keys still arrive, they just do nothing. E, Q and F still work.
            input.move = Vector3.zero;
            input.jump = false;
            input.shove = false;
            input.shoveHeld = false;
            input.grab = false;
            input.sprint = false;
        }

        /// <summary>Squashed: bowed and flattened, arms out. Staggered: swaying on its feet.</summary>
        void StatusPose(ref Quaternion armL, ref Quaternion armR, ref Quaternion chest, ref Quaternion head)
        {
            if (Squashed)
            {
                chest = Quaternion.Euler(22f, 0f, 0f);
                head = Quaternion.Euler(18f, 0f, 0f);
                armL = Quaternion.Euler(0f, 0f, 10f);
                armR = Quaternion.Euler(0f, 0f, -10f);
            }
            else if (Staggered)
            {
                float sway = Mathf.Sin(staggerTimer * Mathf.PI * 2f * 3f);
                chest = chest * Quaternion.Euler(-8f, 0f, 14f * sway);
                head = head * Quaternion.Euler(0f, 0f, -10f * sway);
            }
        }
    }
}
