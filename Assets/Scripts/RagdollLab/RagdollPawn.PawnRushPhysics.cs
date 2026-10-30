using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Pawn Rush movement (seoungtest, 10-30): a quicker jump, stepping up low lips, being carried over a ledge a jump
    /// just reaches, and stepping off an edge without the hips being pulled over it. Off unless the pawn holds
    /// <see cref="PawnRushPhysics"/>; Sword Fight and every other mode never hand it out, so they move exactly as before.
    /// </summary>
    public partial class RagdollPawn
    {
        /// <summary>Pawn Rush movement numbers; null everywhere else (PawnRushCourseSkills hands them out).</summary>
        public PawnRushPhysicsParams PawnRushPhysics
        {
            get => pawnRushPhysics;
            set
            {
                if (value == pawnRushPhysics) return;
                pawnRushPhysics = value;
                // The Pawn Rush models (R251) go on, or come off, at once.
                if (skin != null && bodies != null && bodies.Length > 0 && bodies[0] != null) ShowPiece();
            }
        }
        PawnRushPhysicsParams pawnRushPhysics;

        /// <summary>Times this pawn hopped onto a lip or was carried over a ledge (Pawn Rush).</summary>
        public int StepOvers { get; private set; }

        float stepOverCooldown;
        bool jumpLeadLeft, airPoseLast;
        float skidTimer;   // Pawn Rush: just up from a dive, the feet still skid (R248)
        float diveLand;    // 0 flying, 1 down on the floor catching itself (R250)
        float landPose, lastAirVy;   // the landing pose after a jump: 1 just down, fades to 0 (R250)

        float JumpSpeed(RagdollParams p) => PawnRushPhysics != null ? PawnRushPhysics.jumpSpeed : p.jumpImpulse;

        /// <summary>Gravity on a rising body, as a share of the world's (GuardJumpRise measures the climb with it).</summary>
        float RiseGravityScale => PawnRushPhysics != null ? PawnRushPhysics.gravityScale : 1f;

        /// <summary>Something else is flying the body (a pad's arc, a skill's leap or dash): its own gravity rules.</summary>
        bool BodyCarried => launched || dashing || flight || flying || skillStage != SkillStage.None;

        /// <summary>The extra pull in the air. Standing it is left alone: the joints hold the body up against plain gravity.</summary>
        void PawnRushGravity()
        {
            var q = PawnRushPhysics;
            if (q == null || Grounded || BodyCarried || Climbing || rope != null || Floating || BeingHeld
                || hookPhase == HookPhase.Pulling) return;
            float scale = q.gravityScale * (ComVelocity().y < 0f ? q.fallGravity : 1f);
            if (scale <= 1.001f) return;
            Vector3 extra = Physics.gravity * (scale - 1f);
            foreach (var rb in bodies)
                if (rb.useGravity) rb.AddForce(extra, ForceMode.Acceleration);
        }

        /// <summary>
        /// A floor hit lands as hard as the fall was long under plain gravity, so the stronger pull does not floor the
        /// pawn from a drop it used to walk away from. Pawns and walls count as they are.
        /// </summary>
        float PawnRushLandingImpact(float impact, Vector3 normal, RagdollPawn other)
        {
            var q = PawnRushPhysics;
            if (q == null || other != null || normal.y < 0.6f) return impact;
            return impact / Mathf.Sqrt(Mathf.Max(1f, q.gravityScale * q.fallGravity));
        }

        /// <summary>
        /// The floor the hips stand at: the highest one under the hips or either foot. At an edge the hips are over the
        /// low floor while a foot is still on the top; standing at the low one pulled the body over the lip.
        /// </summary>
        float PawnRushStandY(float underHips)
        {
            var q = PawnRushPhysics;
            if (q == null || !q.standOnFeet) return underHips;
            // Only over a flat floor (an edge, a step). On a slope the front foot is always on higher ground, and taking
            // its floor lifted the hips up every slope and bobbed them with each step (R249).
            if (groundNormal.y < 0.985f) return underHips;
            float best = underHips;
            if (FootFloor(footL, out float y)) best = Mathf.Max(best, y);
            if (FootFloor(footR, out y)) best = Mathf.Max(best, y);
            return best;
        }

        bool FootFloor(BoxCollider foot, out float y)
        {
            y = 0f;
            Vector3 at = foot.bounds.center + Vector3.up * 0.1f;
            if (!SolidRay(at, Vector3.down, 0.3f, out RaycastHit h) || h.normal.y < 0.6f) return false;
            y = h.point.y;
            return true;
        }

        /// <summary>
        /// Walking into a lip no higher than stepUpHeight, the pawn hops onto it; in the air with the feet just under a
        /// ledge top (ledgeAssistHeight), it is carried over instead of catching the lip and dropping back.
        /// </summary>
        void PawnRushStepOver(float dt)
        {
            var q = PawnRushPhysics;
            if (q == null) return;
            skidTimer -= dt;
            stepOverCooldown -= dt;
            if (stepOverCooldown > 0f || State != PawnState.Active || Climbing || rope != null || Floating || BeingHeld
                || Diving || BodyCarried || hookPhase == HookPhase.Pulling || HoldingEnvironment()) return;
            Vector3 move = Flat(input.move);
            if (move.sqrMagnitude < 0.04f) return;
            Vector3 dir = move.normalized;
            bool planted = Grounded;
            float most = planted ? q.stepUpHeight : q.ledgeAssistHeight;
            if (most < 0.02f) return;
            Rigidbody hips = bodies[0];
            if (!planted && ComVelocity().y > q.ledgeAssistMaxRise) return;   // still on the way up: let the jump climb first

            float feetY = Mathf.Min(footL.bounds.min.y, footR.bounds.min.y);
            Vector3 hp = hips.position;
            float ahead = Mathf.Max(0f, Vector3.Dot(Flat(hips.linearVelocity), dir));
            Vector3 low = new Vector3(hp.x, feetY + 0.04f, hp.z);
            // Something steep right ahead at the feet (a slope is walked up, not hopped).
            if (!SolidRay(low, dir, q.stepProbe + ahead * dt * 2f, out RaycastHit face) || face.normal.y > 0.6f) return;
            // Its top, looked for from just above the most the pawn may step. Starting inside a taller block finds
            // nothing on it, so a wall is never taken for a step.
            Vector3 over = face.point + dir * 0.1f;
            over.y = feetY + most + 0.05f;
            if (!SolidRay(over, Vector3.down, most + 0.05f, out RaycastHit top) || top.normal.y < 0.7f) return;
            float rise = top.point.y - feetY;
            if (rise < 0.03f || rise > most) return;
            // Room for the body over it: nothing solid at knee height above the top between here and there.
            if (SolidRay(new Vector3(hp.x, top.point.y + 0.15f, hp.z), dir, face.distance + 0.25f, out _)) return;

            float g = -Physics.gravity.y * q.gravityScale;
            float up = Mathf.Sqrt(2f * Mathf.Max(0.1f, g) * (rise + q.stepClearance));
            foreach (var rb in bodies)
            {
                Vector3 v = rb.linearVelocity;
                v.y = Mathf.Max(v.y, up);   // set, never add (Jump's rule)
                float along = Vector3.Dot(Flat(v), dir);
                if (along < q.stepForward) v += dir * (q.stepForward - along);
                rb.linearVelocity = v;
            }
            anchorVel = carryVel + dir * Mathf.Max(q.stepForward, Vector3.Dot(anchorVel - carryVel, dir));
            // The launch guard and the standing spring leave the hop alone until it comes down.
            freeFlight = Mathf.Max(freeFlight, up / Mathf.Max(0.1f, g) + 0.05f);
            airTimer = Mathf.Max(airTimer, 0.1f);
            stepOverCooldown = 0.3f;
            StepOvers++;
        }

        // ---------------------------------------------------------------- gait and skid (R248)

        /// <summary>
        /// Steps a second: slow steps at a walk rising to the run's and the sprint's, instead of the shared fixed hop
        /// rate that only shrinks toward a stop (it shuffled in slow motion at low speed and buzzed at full speed).
        /// </summary>
        float PawnRushCadence(float shared, float speedN)
        {
            var q = PawnRushPhysics;
            if (q == null) return shared;
            float full = Mathf.Lerp(q.runCadence, q.sprintCadence, sprintBlend);
            return Mathf.Lerp(q.walkCadence, full, speedN) * Mathf.Clamp01(speedN * 4f);
        }

        /// <summary>How far the legs swing: short at a walk, longer at the run, longest at the sprint (the hip allows 60).</summary>
        float PawnRushLegSwing(float shared, float speedN)
        {
            var q = PawnRushPhysics;
            if (q == null) return shared;
            float full = Mathf.Lerp(q.runLegSwing, q.sprintLegSwing, sprintBlend);
            return Mathf.Min(Mathf.Lerp(q.walkLegSwing, full, speedN), HipSwingLimit);
        }

        /// <summary>The foot in front lands heel first (toes up), the one behind pushes off on its toes.</summary>
        void PawnRushFootRoll(float s, float speedN, ref Quaternion footL, ref Quaternion footR)
        {
            var q = PawnRushPhysics;
            if (q == null) return;
            float roll = q.footRoll * Mathf.Clamp01(speedN * 1.5f) * s;
            // s > 0: the left leg is in front. A negative turn about x lifts the toes.
            footL *= Quaternion.Euler(-roll, 0f, 0f);
            footR *= Quaternion.Euler(roll, 0f, 0f);
        }

        /// <summary>Up from a dive at speed the pawn skids on a little before its feet catch (R248).</summary>
        void PawnRushSkid(Vector3 speed)
        {
            var q = PawnRushPhysics;
            if (q == null || speed.sqrMagnitude < 1f) return;
            anchorVel = speed * q.skidKeep;
            skidTimer = q.skidTime;
        }

        /// <summary>How fast a pawn with no key held comes to rest; slower while skidding after a dive.</summary>
        float StopDeceleration(RagdollParams p) =>
            PawnRushPhysics != null && skidTimer > 0f ? PawnRushPhysics.skidDeceleration : p.stopDeceleration;

        // ---------------------------------------------------------------- getting up and slopes (R249)

        float GetUpDelay(RagdollParams p) => PawnRushPhysics != null ? PawnRushPhysics.getUpDelay : p.getUpDelay;
        float GetUpBlendTime(RagdollParams p) => PawnRushPhysics != null ? PawnRushPhysics.getUpBlendTime : p.getUpBlendTime;
        float DiveMinTime(RagdollParams p) => PawnRushPhysics != null ? PawnRushPhysics.diveMinTime : p.diveMinTime;
        float DiveLieTime(RagdollParams p) => PawnRushPhysics != null ? PawnRushPhysics.diveLieTime : p.diveLieTime;
        /// <summary>Below this the dive may end; above it, it keeps sliding (R252: higher on Pawn Rush, up while still sliding).</summary>
        float DiveGetUpSpeed(RagdollParams p) => PawnRushPhysics != null ? PawnRushPhysics.diveGetUpSpeed : p.diveGetUpSpeed;
        float DiveMaxTime(RagdollParams p) => PawnRushPhysics != null ? PawnRushPhysics.diveMaxTime : p.diveMaxTime;
        /// <summary>
        /// A Pawn Rush dive down on the floor (its catch pose fully in, R250). Propped on its hands its hips stay above the
        /// shared "lying" height, so the dive never counted as down and waited out 1.5 s (3 s at worst) before getting up.
        /// </summary>
        bool PawnRushDiveDown => PawnRushPhysics != null && diveLand >= 0.99f;
        /// <summary>Seconds after getting up that the run waits for an upright body.</summary>
        float UpSettle => PawnRushPhysics != null ? Mathf.Max(0.01f, PawnRushPhysics.upSettleTime) : UpSettleTime;
        float SlopeStiffness(RagdollParams p) => PawnRushPhysics != null ? PawnRushPhysics.slopeStiffness : p.slopeStiffnessMultiplier;

        /// <summary>The slope along the way the pawn faces, in degrees: positive climbing, negative going down. 0 off the ground.</summary>
        float SlopePitch(bool planted)
        {
            if (!planted || !groundFound || groundNormal.y < 0.5f) return 0f;
            return Mathf.Asin(Mathf.Clamp(-Vector3.Dot(groundNormal, facing), -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>Leaning into a climb and back a little going down, as a runner does (the shared run stood bolt upright).</summary>
        float PawnRushSlopeLean(bool planted)
        {
            var q = PawnRushPhysics;
            if (q == null) return 0f;
            float pitch = SlopePitch(planted);
            return pitch * (pitch > 0f ? q.slopeLeanUp : q.slopeLeanDown);
        }

        /// <summary>
        /// On a slope the legs swing about a line tipped with it (forward into a climb, back down a descent) and the
        /// soles lie along it, instead of swinging as on the flat with the uphill toe in the ground and the downhill one
        /// in the air.
        /// </summary>
        void PawnRushSlopeLegs(bool air, ref Quaternion thighL, ref Quaternion thighR, ref Quaternion footL, ref Quaternion footR)
        {
            var q = PawnRushPhysics;
            if (q == null || air || State != PawnState.Active) return;
            float pitch = SlopePitch(Grounded);
            if (Mathf.Abs(pitch) < 2f) return;
            // The hips already lean by part of the slope; the legs take the rest of it.
            float relative = pitch * (1f - (pitch > 0f ? q.slopeLeanUp : q.slopeLeanDown));
            Quaternion swing = Quaternion.Euler(-relative * q.slopeLegFollow, 0f, 0f);   // a negative turn swings forward
            thighL = swing * thighL;
            thighR = swing * thighR;
            // Each foot turns back by what its leg took, then on to the slope: toes up a climb, down a descent.
            Quaternion sole = Quaternion.Euler(relative * q.slopeLegFollow - relative, 0f, 0f);
            footL = sole * footL;
            footR = sole * footR;
        }

        // ---------------------------------------------------------------- poses (R246)

        /// <summary>An arm pointed along a direction in the chest's frame (x = the body's right, y = up, z = forward).</summary>
        static Quaternion ArmAlong(bool left, Vector3 chestLocal) =>
            Quaternion.FromToRotation(left ? Vector3.left : Vector3.right, chestLocal.normalized);

        /// <summary>
        /// Pawn Rush arms and jump. Running, the arms hang by the sides and swing forward and back against the legs (the
        /// right arm comes forward with the left leg), further at a sprint. In the air the pose follows the jump: rising,
        /// the leading knee drives up with the opposite arm (from standing, both arms and knees); falling, the arms open
        /// for balance and the legs reach down for the floor.
        /// </summary>
        void PawnRushRunPose(bool air, float speedN, float s, ref Quaternion armL, ref Quaternion armR,
                             ref Quaternion thighL, ref Quaternion thighR, ref Quaternion footL, ref Quaternion footR, ref Quaternion head)
        {
            var q = PawnRushPhysics;
            if (q == null) return;
            if (!Diving) diveLand = 0f;
            if (State != PawnState.Active) return;
            if (!air)
            {
                float dt = Time.fixedDeltaTime;
                // Just down from the air: the landing pose, longer the harder it came down (R250).
                if (airPoseLast)
                    landPose = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(-lastAirVy / 6f));
                landPose -= dt / Mathf.Max(0.01f, q.landPoseTime);
                airPoseLast = false;
                float amp = Mathf.Lerp(q.runArmSwing, q.sprintArmSwing, sprintBlend) * speedN;
                // The pawn's skirt is wider than the other pieces' bodies: its arms open further out (R253).
                float down = (q.runArmDown - (piece == PieceKind.Pawn ? q.pawnArmSpread : 0f)) * Mathf.Deg2Rad;
                // s > 0: the left leg is forward (thighL turns by -legAmp * s), so the right arm is.
                float swingR = (q.runArmForward * speedN + amp * s) * Mathf.Deg2Rad;
                float swingL = (q.runArmForward * speedN - amp * s) * Mathf.Deg2Rad;
                armL = ArmAlong(true, new Vector3(-Mathf.Cos(down), -Mathf.Sin(down) * Mathf.Cos(swingL), Mathf.Sin(down) * Mathf.Sin(swingL)));
                armR = ArmAlong(false, new Vector3(Mathf.Cos(down), -Mathf.Sin(down) * Mathf.Cos(swingR), Mathf.Sin(down) * Mathf.Sin(swingR)));
                PawnRushLandPose(speedN, ref armL, ref armR, ref head, ref thighL, ref thighR);
                return;
            }

            // The leg that was behind at take-off is the one that drives up, as in a running leap.
            if (!airPoseLast) jumpLeadLeft = s < 0f;
            airPoseLast = true;
            float vy = ComVelocity().y;
            lastAirVy = vy;
            landPose = 0f;
            float rising = Mathf.Clamp01(0.5f + 0.5f * vy / Mathf.Max(0.1f, q.jumpPoseSpeed));
            float moving = Mathf.Clamp01(HorizontalSpeed / Mathf.Max(0.1f, P.moveSpeed));

            // Rising from a run: lead knee up, trail leg back, the opposite arm up and forward, the other back.
            Quaternion leadThigh = Quaternion.Euler(-55f, 0f, 0f), trailThigh = Quaternion.Euler(20f, 0f, 0f);
            Vector3 upArm = new Vector3(0.25f, 0.55f, 0.8f), backArm = new Vector3(0.35f, -0.6f, -0.7f);
            Quaternion runL = jumpLeadLeft ? leadThigh : trailThigh, runR = jumpLeadLeft ? trailThigh : leadThigh;
            Quaternion runArmL = ArmAlong(true, jumpLeadLeft ? Mirror(backArm) : Mirror(upArm));
            Quaternion runArmR = ArmAlong(false, jumpLeadLeft ? upArm : backArm);
            // Rising from standing: both knees tucked, both arms thrown up and forward.
            Quaternion tuck = Quaternion.Euler(-35f, 0f, 0f);
            Vector3 bothUp = new Vector3(0.3f, 0.65f, 0.7f);
            Quaternion upL = Quaternion.Slerp(tuck, runL, moving), upR = Quaternion.Slerp(tuck, runR, moving);
            Quaternion upArmL = Quaternion.Slerp(ArmAlong(true, Mirror(bothUp)), runArmL, moving);
            Quaternion upArmR = Quaternion.Slerp(ArmAlong(false, bothUp), runArmR, moving);
            // Falling: arms open out a little above the shoulders, legs together reaching down and a touch forward.
            Vector3 open = new Vector3(0.9f, 0.3f, 0.25f);
            Quaternion reach = Quaternion.Euler(-12f, 0f, 0f);

            thighL = Quaternion.Slerp(reach, upL, rising);
            thighR = Quaternion.Slerp(reach, upR, rising);
            footL = footR = Quaternion.Euler(Mathf.Lerp(5f, 25f, rising), 0f, 0f);
            armL = Quaternion.Slerp(ArmAlong(true, Mirror(open)), upArmL, rising);
            armR = Quaternion.Slerp(ArmAlong(false, open), upArmR, rising);
            // Looking up the jump, then down at where it lands.
            head = Quaternion.Euler(Mathf.Lerp(8f, -10f, rising), 0f, 0f);
        }

        static Vector3 Mirror(Vector3 right) => new Vector3(-right.x, right.y, right.z);

        /// <summary>
        /// Pawn Rush dive: a straight body, arms out ahead along the dive (a superman), head up, legs together and pointed
        /// behind. Drives hold it at diveBodyStiffness instead of letting everything but the arms go limp.
        /// </summary>
        void PawnRushDivePose(ref Quaternion armL, ref Quaternion armR, ref Quaternion chest, ref Quaternion head,
                              ref Quaternion thighL, ref Quaternion thighR, ref Quaternion footL, ref Quaternion footR)
        {
            var q = PawnRushPhysics;
            if (q == null) return;
            // In the air (R250, 승규 님: "고개 들고 … 손은 다이빙 하듯이 위로"): both arms straight up past the head along
            // the body, as a diver goes into the water, so they lead as the body tips over; the head held up.
            Vector3 overhead = new Vector3(0.12f, 1f, 0.2f);
            Quaternion flyL = ArmAlong(true, Mirror(overhead)), flyR = ArmAlong(false, overhead);
            // Down on the floor ("착지 할 때 착지 동작"): the hands come down ahead to catch it, as in a push-up, chest
            // and head up off the floor, legs lifted a little behind.
            Transform chestT = bodies[(int)BodyId.Chest].transform;
            Vector3 side = Vector3.Cross(Vector3.up, diveDirection);
            Vector3 catchAt = diveDirection * 0.55f + Vector3.down * 0.8f;
            Quaternion catchL = ArmAlong(true, chestT.InverseTransformDirection(catchAt - side * 0.35f));
            Quaternion catchR = ArmAlong(false, chestT.InverseTransformDirection(catchAt + side * 0.35f));
            bool down = groundFound && bodies[0].position.y - groundY < standHeight + 0.1f && stateTimer > 0.1f;
            diveLand = Mathf.MoveTowards(diveLand, down ? 1f : 0f, Time.fixedDeltaTime / Mathf.Max(0.01f, q.diveLandBlend));

            armL = Quaternion.Slerp(flyL, catchL, diveLand);
            armR = Quaternion.Slerp(flyR, catchR, diveLand);
            chest = Quaternion.Euler(Mathf.Lerp(-10f, -18f, diveLand), 0f, 0f);   // arched, chest up
            head = Quaternion.Euler(-q.diveHeadUp, 0f, 0f);                        // looking ahead, not at the floor
            thighL = thighR = Quaternion.Euler(Mathf.Lerp(10f, 22f, diveLand), 0f, 0f);
            footL = footR = Quaternion.Euler(30f, 0f, 0f);
        }

        /// <summary>
        /// Touching down from a jump or a fall (R250): for a moment the arms come forward and down to take it and the
        /// head dips, then the run's pose takes over again. Harder landings hold it a little longer.
        /// </summary>
        void PawnRushLandPose(float speedN, ref Quaternion armL, ref Quaternion armR, ref Quaternion head, ref Quaternion thighL, ref Quaternion thighR)
        {
            var q = PawnRushPhysics;
            if (q == null || landPose <= 0f) return;
            float k = Smooth(Mathf.Clamp01(landPose));
            Vector3 take = new Vector3(0.55f, -0.35f, 0.75f);
            armL = Quaternion.Slerp(armL, ArmAlong(true, Mirror(take)), k);
            armR = Quaternion.Slerp(armR, ArmAlong(false, take), k);
            head = Quaternion.Slerp(head, Quaternion.Euler(14f, 0f, 0f), k);
            // Standing still the legs plant apart a touch; running they are left to the stride.
            Quaternion plant = Quaternion.Euler(-8f, 0f, 0f);
            float still = k * (1f - Mathf.Clamp01(speedN * 2f));
            thighL = Quaternion.Slerp(thighL, plant * Quaternion.Euler(0f, 0f, -6f), still);
            thighR = Quaternion.Slerp(thighR, plant * Quaternion.Euler(0f, 0f, 6f), still);
        }
    }
}
