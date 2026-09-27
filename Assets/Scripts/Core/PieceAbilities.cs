using System;

namespace ChessFight.Network
{
    // What a piece is doing with its ability right now (Queen of the Hill M12). One
    // byte, so a network client can draw the warnings (the rook's line, the knight's
    // landing spot) of a piece it only sees.
    public enum PieceMove : byte
    {
        None = 0,
        KnightLeap = 1,     // up and over on the L, then falling; the landing spot is shown
        RookWindup = 2,     // 0.6 s: the charge's line is shown, the rook stands
        RookCharge = 3,     // flying along the line, through bodies, gravity ignored
        KingCheck = 4,      // 0.5 s: the sceptre is up, the shockwave follows
        BishopHover = 5,    // afloat, throwing stones
        BishopGlide = 6,    // sliding down a 45 degree line through the air
    }

    // The pieces' abilities (Docs/GameModes/QueenOfTheHill/DESIGN.md §6.2, the
    // starting values) as plain numbers and rules with no Unity in them, so they
    // are tested on their own and tuned in one place (M12). The ragdoll
    // (Assets/ChessFight/RagdollLab, RagdollPawn.Ability.cs) carries them out.
    //
    // Keys: E is a piece's main ability (the pawn's is the grappling hook), Q its
    // second (the king's castling). Every ability warns first, can be answered, and
    // waits a cooldown (§6.1): in the climb nothing is ever a lasting loss.
    public static class PieceAbilities
    {
        // Knight: the L-shaped leap, "two up, one on", drawn as the letter: straight up
        // KnightUp (and KnightLift more, to clear a ledge that high), then KnightOn along
        // the aim, then it lets go and comes down with a little of the speed on. Over a
        // wall or onto a ledge right in front of it. The landing spot is shown to
        // everyone from the start.
        public const float KnightCooldown = 5f;
        public const float KnightUp = 6f, KnightOn = 3f, KnightLift = 0.6f;
        public const float KnightUpSpeed = 16f, KnightOnSpeed = 12f, KnightLetGo = 4f;
        // Its passive, the stomp: coming down on an enemy's head squashes it (M13).
        public const float StompSquash = 1.2f, StompImmunity = 1f, StompBounce = 4f;

        // Rook: the straight charge. A warning line, then flat along one of the board's
        // four directions (or straight up a wall in front of it, or straight down),
        // gravity ignored, through bodies; enemies on its way are knocked down and
        // thrown aside. Enemies in the air are not hit.
        public const float RookCooldown = 8f;
        public const float RookWindup = 0.6f, RookSpeed = 18f, RookDistance = 14f, RookClimb = 8f;
        public const float RookReach = 0.75f;           // how close to the line a body is hit
        public const float RookAside = 5f, RookLift = 2.5f, RookKnockdown = 1f;   // about 3 m aside

        // King: "check!" - the sceptre up, then a shockwave that knocks enemies down
        // and away. Castling swaps it with a rook of its own team, however far.
        public const float CheckCooldown = 12f;
        public const float CheckWindup = 0.5f, CheckRadius = 4f;
        public const float CheckPush = 5f, CheckLift = 2.5f, CheckKnockdown = 1f;  // about 3 m
        public const float CastleCooldown = 20f;
        // Its passive, the king's grace: its teammates within AuraRadius are harder to
        // push and get their stamina back faster.
        public const float AuraRadius = 6f, AuraWeight = 1.3f, AuraStamina = 1.5f;

        // Bishop: the hover (floating, moving slowly, throwing stones), and the
        // diagonal glide down between floors (Space in the air).
        public const float HoverCooldown = 9f;
        public const float HoverSeconds = 5f, HoverSpeed = 3f, HoverRise = 3f;
        public const float HoverStartRise = 1f, HoverStep = 1f;   // up at the start, and per Space
        public const float ShardInterval = 0.7f, ShardSpeed = 11f, ShardLife = 3f;
        public const int ShardsAtOnce = 3;
        // A stone: on the ground a stagger and a push of about 1.5 m, on a wall a bite
        // of stamina, on a moving platform a second stone within ShardRideWindow
        // knocks it off.
        public const float ShardStagger = 0.4f, ShardPush = 3f, ShardStamina = 2.5f, ShardRideWindow = 2f;
        public const float ShardRideKnockdown = 0.8f;
        public const float GlideAngle = 45f, GlideSpeed = 10f;

        // The cooldown after using a piece's ability: slot 0 is E, slot 1 is Q.
        // Zero when the piece has no such ability.
        public static float Cooldown(PieceKind piece, int slot)
        {
            switch (piece)
            {
                case PieceKind.Knight: return slot == 0 ? KnightCooldown : 0f;
                case PieceKind.Rook: return slot == 0 ? RookCooldown : 0f;
                case PieceKind.King: return slot == 0 ? CheckCooldown : CastleCooldown;
                case PieceKind.Bishop: return slot == 0 ? HoverCooldown : 0f;
                default: return 0f;
            }
        }

        public static bool Has(PieceKind piece, int slot) => Cooldown(piece, slot) > 0f;

        // The Korean names shown on the lab's panel.
        public static string Name(PieceKind piece, int slot)
        {
            switch (piece)
            {
                case PieceKind.Knight: return slot == 0 ? "L자 도약" : "";
                case PieceKind.Rook: return slot == 0 ? "직선 돌진" : "";
                case PieceKind.King: return slot == 0 ? "체크!" : "캐슬링";
                case PieceKind.Bishop: return slot == 0 ? "호버 + 돌조각" : "";
                case PieceKind.Pawn: return slot == 0 ? "갈고리" : "";
                default: return "";
            }
        }

        // The rook moves along the board: a flat direction (x, z) snaps to the nearest
        // of the four axes. A zero direction gives +z.
        public static void RookAxis(float x, float z, out float ax, out float az)
        {
            if (Math.Abs(x) > Math.Abs(z))
            {
                ax = x > 0f ? 1f : -1f;
                az = 0f;
            }
            else
            {
                ax = 0f;
                az = z < 0f ? -1f : 1f;
            }
        }

        // Up the wall when the aim is steeper than 45 degrees up and there is a wall to
        // run up; straight down when it is steeper than 45 degrees down; else flat.
        // 1 up, -1 down, 0 flat.
        public static int RookVertical(float aimY, bool wallAhead)
        {
            const float Steep = 0.7071f;
            if (aimY > Steep && wallAhead) return 1;
            if (aimY < -Steep) return -1;
            return 0;
        }

        // How far the charge may still go: along the board RookDistance, up a wall
        // RookClimb.
        public static float RookLength(int vertical) => vertical > 0 ? RookClimb : RookDistance;

        // Seconds the leap's L takes, up and on, before it lets go.
        public static float KnightSeconds => (KnightUp + KnightLift) / KnightUpSpeed + KnightOn / KnightOnSpeed;

        // A teammate this far away (metres, any direction) is in the king's grace.
        public static bool InAura(float dx, float dy, float dz) => dx * dx + dy * dy + dz * dz <= AuraRadius * AuraRadius;
    }

    // One piece's cooldowns, E and Q, counting down in seconds. Plain numbers so the
    // rule "used, then waits" is tested without Unity.
    public sealed class PieceCooldowns
    {
        readonly float[] left = new float[2];

        public float Left(int slot) => slot == 0 || slot == 1 ? Math.Max(0f, left[slot]) : 0f;
        public bool Ready(int slot) => Left(slot) <= 0f;

        // Used the ability in `slot`: it waits `seconds`.
        public void Start(int slot, float seconds)
        {
            if (slot == 0 || slot == 1) left[slot] = Math.Max(0f, seconds);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            left[0] -= dt;
            left[1] -= dt;
        }

        public void Clear() => left[0] = left[1] = 0f;
    }

    // The bishop's stones on a rider (DESIGN §6.2): a second stone within the window
    // knocks it off its platform. Times are seconds of any steady clock.
    public sealed class ShardTally
    {
        double last = double.NegativeInfinity;

        // A stone hit a rider at `now`: true when it is the second within the window.
        public bool Hit(double now, float window)
        {
            bool second = now - last <= window;
            last = second ? double.NegativeInfinity : now;
            return second;
        }
    }
}
