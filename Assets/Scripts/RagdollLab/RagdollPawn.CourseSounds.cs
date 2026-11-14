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
    /// A landing on the feet (count 0, '톡') is the pawn coming down standing after a fall of its own: Grounded again
    /// after MinAir in the air at MinFall or faster. Not one: steps down a stair or off a low edge, anything held (a
    /// wall, a ledge, a rope, the hook, water), a fall so long it ended in the respawn (MaxAir). Landing on the feet
    /// from very high (FlopFall) is a flop all the same.
    ///
    /// R656 (사용자 11-15 "특히 털썩착지는 들리지도 않아"): a flop (count 1, '털썩') is the limp body - a dive, a
    /// knockdown, a tumble - hitting a floor with its hips, chest or head at MinFlopImpact or faster, taken from the
    /// physics contact itself. R652 waited instead for the hips to
    /// come down under three quarters of the standing height, which a pawn lying on its front never reaches, so the
    /// flop hardly ever sounded. Two landings in a row from one pawn are kept apart (Quiet), so a body that bounces or
    /// rolls does not rattle.
    ///
    /// R658 (사용자 11-15 "털썩 착지 사운드가 아주 살짝 늦어"): the flop is raised in the contact's own callback, not on
    /// the next physics step (one step, 20 ms, sooner; raising it only plays a sound and logs it for the network -
    /// nothing moves or is destroyed there, PITFALLS §19), and on a softer first touch (0.7 m/s, was 1.2): a dive comes
    /// down sliding, its first touch is soft, and the flop waited for a harder bump after it.
    /// </summary>
    public partial class RagdollPawn
    {
        // A fall that ends standing: at least this fast (m/s; a 0.25 m drop is 2.2) after this long in the air (s).
        const float MinFall = 2.2f, MinAir = 0.18f;
        // Landing on the feet this fast is a flop all the same (a 2.5 m drop).
        const float FlopFall = 7f;
        // Longer in the air than this was a fall off the course; the respawn is not a landing.
        const float MaxAir = 2.5f;
        // R656: a limp body's hips, chest or head meeting a floor (its face within about 53 degrees of level) at least
        // this fast along the contact (m/s). R658: 0.7 (was 1.2), so a dive's sliding first touch counts.
        const float MinFlopImpact = .7f, FloorFacing = .6f;
        const float QuietLight = 0.2f, QuietFlop = 0.4f;

        float landAir, landFall, landQuiet;

        /// <summary>R657: the legs' phase, radians 0..2π (Pose: the left leg forward at π/2, the right at 3π/2), for the
        /// footsteps (PawnRushStepSfx), which sound as each leg reaches the front.</summary>
        public float GaitAngle => gait;

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
            Vector3 at = bodies[0].position;
            if (groundFound) at.y = groundY;
            if (State == PawnState.Ragdoll)
            {
                landAir = landFall = 0f;   // limp, the flop comes from the contact (NoteFlop)
                return;
            }
            if (!Grounded)
            {
                landAir += dt;
                landFall = Mathf.Max(landFall, -ComVelocity().y);
                return;
            }
            if (landAir >= MinAir && landFall >= MinFall && landAir <= MaxAir && landQuiet <= 0f)
            {
                bool hard = landFall >= FlopFall;
                Fx(SkillFxKind.CourseLand, null, at, facing, hard ? 1 : 0, landFall);
                landQuiet = hard ? QuietFlop : QuietLight;
            }
            landAir = landFall = 0f;
        }

        /// <summary>R656, from OnPartCollision: the limp body's hips, chest or head meeting a floor-like face at `impact`
        /// m/s along the contact. R658: the flop is raised right here, at the contact (a sound and a network log only).</summary>
        void NoteFlop(RagdollBodyPart part, float impact, Vector3 normal)
        {
            if (PawnRushPhysics == null || NetworkPuppet || State != PawnState.Ragdoll) return;
            if (part.id != BodyId.Hips && part.id != BodyId.Chest && part.id != BodyId.Head) return;
            if (Mathf.Abs(normal.y) < FloorFacing || impact < MinFlopImpact || landQuiet > 0f) return;
            Vector3 at = bodies[0].position;
            if (groundFound) at.y = groundY;
            Fx(SkillFxKind.CourseLand, null, at, facing, 1, impact);
            landQuiet = QuietFlop;
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
