using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The piece emotes (skill network test branch). A press of 1..4 plays that slot's pose for a couple of seconds:
    /// arms, chest and head only, so the legs keep standing. Only the machine that simulates the pawn runs it (the
    /// host online); the others see it through the replicated bone rotations, so it needs no packet of its own beyond
    /// the request. Any move, jump, click, grab, skill or status effect ends it at once. Slots 3 and 4 are locked
    /// until their poses are made (PieceEmotes.Unlocked); the pawn has none yet.
    /// </summary>
    public partial class RagdollPawn
    {
        const float EmoteIn = 0.18f, EmoteOut = 0.25f, EmoteCooldown = 0.4f;

        int emoteSlot;
        float emoteTimer, emoteTotal, emoteLockout;

        /// <summary>The slot being played (1..4), 0 when none.</summary>
        public int EmoteSlot => emoteTimer > 0f ? emoteSlot : 0;
        public int EmotesPlayed { get; private set; }

        bool EmoteCanStart =>
            State == PawnState.Active && Grounded && !Climbing && !Floating && !Diving && rope == null && !Squashed && !Staggered
            && skillStage == SkillStage.None && !input.grab && !input.jump && !input.shove && input.move.sqrMagnitude < 0.04f;

        /// <summary>Plays the piece's emote in this slot. False for a slot that is locked, a pawn that is busy, or a copy
        /// of the pawn that another machine simulates.</summary>
        public bool PlayEmote(int slot)
        {
            if (NetworkPuppet || emoteLockout > 0f || !PieceEmotes.Available(piece, slot) || !EmoteCanStart) return false;
            emoteSlot = slot;
            emoteTotal = emoteTimer = PieceEmotes.Duration(slot);
            EmotesPlayed++;
            return true;
        }

        public void StopEmote() => emoteTimer = 0f;

        void UpdateEmote(float dt)
        {
            emoteLockout -= dt;
            if (emoteTimer <= 0f) return;
            emoteTimer -= dt;
            bool busy = State != PawnState.Active || Climbing || Floating || Diving || rope != null || Squashed || Staggered
                        || skillStage != SkillStage.None || input.grab || input.jump || input.shove || input.move.sqrMagnitude > 0.04f;
            if (busy || emoteTimer <= 0f)
            {
                emoteTimer = 0f;
                emoteLockout = EmoteCooldown;
            }
        }

        /// <summary>An arm aimed by how far it swings to the front (0 = out to the side, 90 = straight ahead, past it
        /// crosses the body) and how far it is raised (0 = level, 90 = straight up, negative = down).</summary>
        static Quaternion ArmAim(bool left, float forward, float up) =>
            left ? Quaternion.Euler(0f, forward, -up) : Quaternion.Euler(0f, -forward, up);

        void EmotePose(ref Quaternion armL, ref Quaternion armR, ref Quaternion chest, ref Quaternion head)
        {
            if (emoteTimer <= 0f) return;
            float t = emoteTotal - emoteTimer;
            float k = Smooth(Mathf.Clamp01(Mathf.Min(t / EmoteIn, emoteTimer / EmoteOut)));
            Quaternion tArmL = armL, tArmR = armR, tChest = chest, tHead = head;
            float rest = P.armRestDown;
            float s6 = Mathf.Sin(t * 6f), s10 = Mathf.Sin(t * 10f), s20 = Mathf.Sin(t * 20f);
            switch (piece)
            {
                case PieceKind.Rook:
                    if (emoteSlot == 1)
                    {
                        // Arms folded, chin up, planted.
                        tArmL = ArmAim(true, 118f, -12f);
                        tArmR = ArmAim(false, 118f, -22f);
                        tChest = Quaternion.Euler(-6f, 0f, 2f * s6);
                        tHead = Quaternion.Euler(-14f, 0f, 0f);
                    }
                    else
                    {
                        // Hands on the belly, the whole wall shaking with laughter.
                        tArmL = ArmAim(true, 100f, -30f);
                        tArmR = ArmAim(false, 100f, -30f);
                        tChest = Quaternion.Euler(6f + 5f * s20, 0f, 0f);
                        tHead = Quaternion.Euler(-18f + 4f * s20, 0f, 0f);
                    }
                    break;
                case PieceKind.Bishop:
                    if (emoteSlot == 1)
                    {
                        // A hand up to the grin, the other arm behind it, a sly tilt.
                        tArmR = ArmAim(false, 40f, 112f);
                        tArmL = ArmAim(true, 75f, -35f);
                        tChest = Quaternion.Euler(2f, 0f, -4f);
                        tHead = Quaternion.Euler(6f, -10f, 8f + 2f * s20);
                    }
                    else
                    {
                        // Hands joined in front, a slow bow.
                        tArmL = ArmAim(true, 104f, 18f);
                        tArmR = ArmAim(false, 104f, 18f);
                        tChest = Quaternion.Euler(8f + 3f * s6, 0f, 0f);
                        tHead = Quaternion.Euler(14f + 4f * s6, 0f, 0f);
                    }
                    break;
                case PieceKind.Knight:
                    if (emoteSlot == 1)
                    {
                        // Rearing back, one arm levelled ahead like a lance, the other flung up behind.
                        tArmR = ArmAim(false, 86f, 24f + 3f * s10);
                        tArmL = ArmAim(true, 12f, 82f);
                        tChest = Quaternion.Euler(-20f, -6f, 0f);
                        tHead = Quaternion.Euler(-12f, 0f, 0f);
                    }
                    else
                    {
                        // A fist up in victory, the other arm out, hopping on the spot.
                        tArmR = ArmAim(false, 8f, 100f + 8f * s10);
                        tArmL = ArmAim(true, 30f, 30f);
                        tChest = Quaternion.Euler(-10f + 4f * Mathf.Abs(s10), 0f, 0f);
                        tHead = Quaternion.Euler(-16f, 0f, 0f);
                    }
                    break;
                case PieceKind.King:
                    if (emoteSlot == 1)
                    {
                        // An arm held out to command, the other fist on the hip, a lifted head.
                        tArmR = ArmAim(false, 88f, 12f);
                        tArmL = Quaternion.Euler(0f, 0f, rest + 38f);
                        tChest = Quaternion.Euler(-8f, 0f, 0f);
                        tHead = Quaternion.Euler(-10f, 0f, 0f);
                    }
                    else
                    {
                        // Both arms thrown wide, a big slow laugh.
                        tArmL = ArmAim(true, 6f, 82f + 6f * s6);
                        tArmR = ArmAim(false, 6f, 82f - 6f * s6);
                        tChest = Quaternion.Euler(-8f + 3f * s6, 0f, 0f);
                        tHead = Quaternion.Euler(-16f, 0f, 0f);
                    }
                    break;
                case PieceKind.Queen:
                    if (emoteSlot == 1)
                    {
                        // Chin on one hand, a wink of a head tilt, the other arm resting.
                        tArmR = ArmAim(false, 45f, 104f);
                        tArmL = Quaternion.Euler(0f, 0f, rest + 12f);
                        tChest = Quaternion.Euler(-3f, 0f, -5f);
                        tHead = Quaternion.Euler(-4f, 8f, 12f);
                    }
                    else
                    {
                        // Clapping: the hands come together and apart in front.
                        float clap = 0.5f + 0.5f * Mathf.Sin(t * 22f);
                        float swing = Mathf.Lerp(58f, 102f, clap);
                        tArmL = ArmAim(true, swing, 8f);
                        tArmR = ArmAim(false, swing, 8f);
                        tChest = Quaternion.Euler(-4f + 2f * s10, 0f, 0f);
                        tHead = Quaternion.Euler(-8f, 0f, 6f * s6);
                    }
                    break;
                default:
                    return;
            }
            armL = Quaternion.Slerp(armL, tArmL, k);
            armR = Quaternion.Slerp(armR, tArmR, k);
            chest = Quaternion.Slerp(chest, tChest, k);
            head = Quaternion.Slerp(head, tHead, k);
        }
    }
}
