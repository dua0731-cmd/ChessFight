using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R652 (사용자 11-14 "지금까지 만든 소리들이야. 전부 적용시켜줘. 점프와 착지의 경우 기능도 추가해서 넣어줘"): the
    /// moments of the Pawn Rush movement sounds. Each is raised as a skill effect (<see cref="SkillFxKind"/> CourseDive,
    /// CourseGrab, CourseJump, CourseLand) where the body is simulated - offline, or on the host, whose timed effect stream
    /// sends it on to the other PCs (a puppet raises nothing itself) - and PawnRushSkillSfx plays it there. Footsteps
    /// are not sent (three a second per pawn): every PC times them from what it sees (PawnRushStepSfx).
    ///
    /// A landing is the body coming down after a fall of its own, on its feet (count 0, '톡') or on its body (count 1,
    /// '털썩': limp - a dive, a knockdown, a tumble - or from very high). Not one: steps down a stair or off a low edge
    /// (under MinFall, or in the air under MinAir), anything held (a wall, a ledge, a rope, the hook, water), and a fall
    /// so long it ended in the respawn (MaxAir). Two in a row from one pawn are kept apart (Quiet), so a body that
    /// bounces does not rattle.
    /// </summary>
    public partial class RagdollPawn
    {
        // A fall that ends standing: at least this fast (m/s; a 0.25 m drop is 2.2) after this long in the air (s).
        const float MinFall = 2.2f, MinAir = 0.18f;
        // Limp, the body only has its own height to fall: from a dive's topple it comes down at about 1 m/s.
        const float MinFallLimp = 0.9f, MinAirLimp = 0.12f;
        // Landing on the feet this fast is a flop all the same (a 2.5 m drop).
        const float FlopFall = 7f;
        // Longer in the air than this was a fall off the course; the respawn is not a landing.
        const float MaxAir = 2.5f;
        // A limp body counts as down once its hips are this share of the standing height off the floor.
        const float LimpRest = 0.75f;
        const float QuietLight = 0.2f, QuietFlop = 0.4f;

        float landAir, landFall, landQuiet;

        /// <summary>Once per physics step after the floor is sensed (FixedUpdate).</summary>
        void SenseLanding(float dt)
        {
            if (PawnRushPhysics == null || NetworkPuppet) return;
            landQuiet -= dt;
            bool held = Climbing || Floating || rope != null || hookPhase == HookPhase.Pulling
                        || handL.HoldingLedge || handR.HoldingLedge;
            if (held)
            {
                landAir = landFall = 0f;
                return;
            }
            bool limp = State == PawnState.Ragdoll;
            float height = groundFound ? bodies[0].position.y - groundY : float.MaxValue;
            bool down = limp ? height < standHeight * LimpRest : Grounded;
            if (!down)
            {
                landAir += dt;
                landFall = Mathf.Max(landFall, -ComVelocity().y);
                return;
            }
            bool fell = limp ? landAir >= MinAirLimp && landFall >= MinFallLimp : landAir >= MinAir && landFall >= MinFall;
            if (fell && landAir <= MaxAir && landQuiet <= 0f)
            {
                bool flop = limp || Diving || landFall >= FlopFall;
                Vector3 at = bodies[0].position;
                if (groundFound) at.y = groundY;
                Fx(SkillFxKind.CourseLand, null, at, facing, flop ? 1 : 0, landFall);
                landQuiet = flop ? QuietFlop : QuietLight;
            }
            landAir = landFall = 0f;
        }

        /// <summary>A jump that left (Jump): off the floor, or (vault) off a ledge the hands held.</summary>
        void SoundJump(bool vault)
        {
            if (PawnRushPhysics == null || NetworkPuppet) return;
            Fx(SkillFxKind.CourseJump, null, bodies[0].position, facing, vault ? 1 : 0);
        }

        /// <summary>After the hands' step: a hand has just taken hold of something (a pawn, a thing, a wall, a rope).</summary>
        void SoundGrab(bool heldBefore)
        {
            if (PawnRushPhysics == null || NetworkPuppet || heldBefore) return;
            if (Grabbing || Climbing || OnRope) Fx(SkillFxKind.CourseGrab, null, bodies[0].position, facing);
        }
    }
}
