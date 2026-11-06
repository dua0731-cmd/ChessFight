using System;
using UnityEngine;

namespace ChessFight.ChessBoxingPrototype
{
    // Owned by the test scene; never reads or writes RagdollTuning or PhysicsProfile.
    [Serializable]
    public sealed class CBSettings
    {
        [Header("Movement")]
        [Range(0.5f, 5f)] public float moveSpeed = 2.1f;
        [Range(2f, 60f)] public float acceleration = 20f;
        [Range(20f, 200f)] public float balanceStrength = 100f;
        [Range(8f, 30f)] public float bodyMass = 12f;
        [Header("Combat")]
        [Range(20f, 300f)] public float maxHealth = 100f;
        public float jabDamage = 9f;
        public float heavyDamage = 23f;
        public float jabCost = 7f;
        public float heavyCost = 22f;
        public float guardJabCost = 14f;
        public float guardHeavyCost = 33f;
        public float staminaRecovery = 22f;
        [Range(0.5f, 3f)] public float downSeconds = 1.2f;
        [Range(0.1f, 1f)] public float recoveryProtection = 0.55f;
        [Header("Opponent")]
        [Range(0.15f, 0.8f)] public float aiReaction = 0.32f;
        [Range(0f, 1f)] public float aiGuardChance = 0.6f;
    }

    public enum CBBodyState { Standing, Down, Rising, KnockedOut }
}
