using System;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // What a Pawn Rush mini-game may ask of a character and do to it (mini-games v1.0 §1.1, §2.2). Before this
    // the course could only see where a character's hips were, so "holding a rope" had to be read as walking
    // backwards in a lane and planks were picked up by standing. The ragdoll (RagdollDriver) implements it.
    //
    // Everything here is for the PC that simulates the characters (offline, or the host): online the others
    // draw puppets, and a game never judges there.
    public interface IMiniGamePawn
    {
        // Knocked over or getting up (a dive is not "down": it is how F, H and I catch things).
        bool Down { get; }
        bool Diving { get; }
        // The latest move input: world, flat, length 0..1 (zero when there is none).
        Vector3 MoveInput { get; }
        bool SprintHeld { get; }
        // 0..1 of the stamina pool.
        float Stamina01 { get; }
        // Spends stamina in the pool's own unit (a second of sprinting costs about 1).
        void SpendStamina(float amount);
        Vector3 HipsVelocity { get; }
        // The collider a hand grips (0 left, 1 right), or null.
        Collider Held(int hand);
        // Where a hand is (0 left, 1 right). On every PC: a puppet's hands are where the host's were drawn.
        Vector3 HandPosition(int hand);
        // A piece skill moving the body along at speed (a rook's charge, a pawn's steps), and how many skills it
        // has used (a charge is one use: F counts one capture per charge).
        bool SkillDashing { get; }
        int SkillUses { get; }
        // When (Time.time) a knight's leap last landed, and where; -1 never.
        float KnightLandedAt { get; }
        Vector3 KnightLandedWhere { get; }
        // Adds a velocity to the whole body (a rope that is too short pulls the pawn back).
        void AddVelocity(Vector3 change);
        // No moving, jumping or sprinting for this long (J's pad winding up).
        void HoldStill(float seconds);
    }

    // Hooks the mini-games set on the character side.
    public static class MiniGameHooks
    {
        // A jump press a game takes instead (J's pads: Space on a pad winds up a launch). Called on the PC that
        // simulates the character, with its driver; true eats the jump.
        public static Func<ICharacterDriver, bool> JumpIntercept;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => JumpIntercept = null;
    }
}
