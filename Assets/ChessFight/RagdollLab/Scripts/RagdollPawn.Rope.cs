using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Ropes, chains and swings (Queen of the Hill M6): hanging on a Gameplay RopeLine. The right button
    /// takes hold of a rope within reach of the hands. On it the body is placed like on the wall (kinematic),
    /// the hands on the rope and the body hanging along it, carried by the rope's own swing. W and S climb
    /// it (stamina while moving, none while still), A and D turn round it, Space jumps off and letting go of
    /// the button drops off - both with the speed of the rope where the pawn was. W at the top steps onto a
    /// floor beside the rope's top; S at the bottom lets go.
    /// </summary>
    public partial class RagdollPawn
    {
        RopeLine rope;              // the rope the hands are on, or null
        float ropeS;                // how far down the rope the hands are
        float hangTime;             // on this rope, for the legs and the sway
        float ropeBlend;            // 0..1: easing from where the body was onto the rope
        Vector3 ropeFrom;           // the hips when it took hold
        float ropeRegrab;           // no taking hold again (of anything) for this long after letting go
        bool ropeTopping;           // stepping off the top onto a floor
        Vector3 ropeTopTarget, ropeTopDir;
        float ropeClimbing;         // -1..1, how it climbs this step (for the legs)

        const float RopeTopGap = 0.35f;     // the hands stop this far below the top
        const float RopeBack = 0.22f;       // the body hangs this far behind the rope, facing it, hands in front
        const float RopeGrabEase = 0.15f;

        public bool OnRope => rope != null;
        public RopeLine HeldRope => rope;
        public float RopeDown => ropeS;
        public int RopeGrabs { get; private set; }
        public int RopeJumps { get; private set; }
        public int RopeTopOuts { get; private set; }
        public int RopeDrops { get; private set; }

        static double RopeClock => ObstacleClock.Now;

        void UpdateRope(RagdollParams p, float dt)
        {
            ropeRegrab -= dt;
            if (rope == null)
            {
                TryTakeRope(p);
                return;
            }
            if (ropeTopping)
            {
                RopeTopOut(dt);
                return;
            }
            if (input.jump)
            {
                LeaveRope(p, true);
                return;
            }
            if (!input.grab || stamina <= 0f || State != PawnState.Active || Floating || BeingHeld)
            {
                LeaveRope(p, false);
                return;
            }
            hangTime += dt;
            // W and S up and down the rope whatever the camera's angle, A and D round it (read through the
            // camera's own axes, as on the wall).
            Vector3 move = Flat(input.move);
            Vector3 view = Flat(input.aim);
            if (view.sqrMagnitude < 1e-4f) view = facing;
            view.Normalize();
            float up = Mathf.Clamp(Vector3.Dot(move, view), -1f, 1f);
            float side = Mathf.Clamp(Vector3.Dot(move, Vector3.Cross(Vector3.up, view)), -1f, 1f);
            if (Mathf.Abs(side) > 0.1f) facing = Quaternion.AngleAxis(side * p.ropeTurnRate * dt, Vector3.up) * facing;

            float before = ropeS;
            ropeClimbing = Mathf.Abs(up) > 0.1f ? up : 0f;
            if (ropeClimbing > 0f && ropeS <= RopeTopGap + 0.01f && ropeBlend >= 1f && TryRopeTopOut())
                return;
            if (ropeClimbing < 0f && ropeS >= rope.Length - 0.01f)
            {
                // Climbed off the bottom end.
                LeaveRope(p, false);
                return;
            }
            ropeS = Mathf.Clamp(ropeS - ropeClimbing * p.climbSpeed * ClimbScale * dt, RopeTopGap, rope.Length);
            if (Mathf.Abs(ropeS - before) > 1e-4f) UseStamina(p, p.climbDrainMove * dt);
            else if (p.climbDrainHold > 0f) UseStamina(p, p.climbDrainHold * dt);
            PlaceOnRope(dt);
        }

        /// <summary>The right button near a rope: take hold of it where the hands reach it.</summary>
        void TryTakeRope(RagdollParams p)
        {
            if (RopeLine.All.Count == 0 || !input.grab || climbGrabLatch || ropeRegrab > 0f || stamina <= 0f) return;
            if (State != PawnState.Active || Climbing || Floating || BeingHeld || Grabbing || topOutTimer > 0f) return;
            if (hookPhase == HookPhase.Charging || hookPhase == HookPhase.Pulling || hookPhase == HookPhase.Flying) return;
            double now = RopeClock;
            Vector3 hands = bodies[0].position + Vector3.up * RopeHang + Flat(facing).normalized * RopeBack;
            RopeLine best = null;
            float bestDistance = p.ropeReach, bestS = 0f;
            foreach (var line in RopeLine.All)
            {
                if (line == null || !line.isActiveAndEnabled) continue;
                float d = line.Distance(hands, now, out float s);
                if (s < RopeTopGap) d = Vector3.Distance(hands, line.Point(RopeTopGap, now));
                if (d < bestDistance)
                {
                    best = line;
                    bestDistance = d;
                    bestS = Mathf.Max(s, RopeTopGap);
                }
            }
            if (best == null) return;
            rope = best;
            ropeS = bestS;
            hangTime = 0f;
            ropeBlend = 0f;
            ropeFrom = bodies[0].position;
            ropeTopping = false;
            ropeClimbing = 0f;
            RopeGrabs++;
            // Nothing else in the hands, off the wall; the hook stays on the belt.
            handL.Release();
            handR.Release();
            PlaceOnRope(Time.fixedDeltaTime);
        }

        /// <summary>The hips hang along the rope under the hands, eased on from where the body was.</summary>
        void PlaceOnRope(float dt)
        {
            double now = RopeClock;
            Vector3 down = rope.Direction(now);
            // Behind the rope, facing it: the rope runs in front of the chest where the hands hold it.
            Vector3 back = Vector3.ProjectOnPlane(-facing, down);
            back = back.sqrMagnitude > 1e-4f ? back.normalized * RopeBack : Vector3.zero;
            Vector3 target = rope.Point(ropeS, now) + down * RopeHang + back;
            ropeBlend = Mathf.Min(1f, ropeBlend + dt / RopeGrabEase);
            float k = ropeBlend * ropeBlend * (3f - 2f * ropeBlend);
            Vector3 next = Vector3.Lerp(ropeFrom, target, k);
            // Where it is going: the rope's own speed there plus the climb, for letting go.
            anchorVel = rope.PointVelocity(ropeS + RopeHang, now) - down * (ropeClimbing * P.climbSpeed * ClimbScale);
            anchorPos = next;
            anchor.MovePosition(anchorPos);
            Vector3 face = Vector3.ProjectOnPlane(facing, down);
            anchor.MoveRotation(Quaternion.LookRotation(face.sqrMagnitude > 1e-4f ? face.normalized : facing, -down));
            Grounded = false;
        }

        /// <summary>W at the top of the rope: a floor within a step of its top is stood on.</summary>
        bool TryRopeTopOut()
        {
            Vector3 top = rope.Top;
            float[] turns = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };
            foreach (float turn in turns)
            {
                Vector3 dir = Quaternion.AngleAxis(turn, Vector3.up) * Flat(facing).normalized;
                Vector3 probe = top + dir * 0.8f + Vector3.up * 1.2f;
                if (!Physics.Raycast(probe, Vector3.down, out var hit, 2.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (ownSet.Contains(hit.collider) || PassesThrough(hit.collider) || hit.normal.y < 0.7f) continue;
                if (hit.point.y < top.y - 0.8f || hit.point.y > top.y + 0.6f) continue;
                ropeTopping = true;
                ropeTopTarget = hit.point + Vector3.up * (standHeight + 0.03f);
                ropeTopDir = dir;
                facing = dir;
                return true;
            }
            return false;
        }

        /// <summary>Stepping off the top: straight up past the floor's height, then over onto it.</summary>
        void RopeTopOut(float dt)
        {
            const float speed = 2.5f;
            Vector3 next = anchorPos;
            if (next.y < ropeTopTarget.y - 0.001f) next.y = Mathf.MoveTowards(next.y, ropeTopTarget.y, speed * dt);
            else
            {
                Vector3 flat = Flat(ropeTopTarget - next);
                next += Vector3.ClampMagnitude(flat, speed * dt);
            }
            anchorVel = (next - anchorPos) / Mathf.Max(dt, 1e-4f);
            anchorPos = next;
            anchor.MovePosition(anchorPos);
            anchor.MoveRotation(Quaternion.LookRotation(ropeTopDir, Vector3.up));
            if ((ropeTopTarget - anchorPos).sqrMagnitude < 0.0004f)
            {
                RopeTopOuts++;
                rope = null;
                ropeTopping = false;
                ropeRegrab = 0.5f;
                anchorVel = ropeTopDir * 0.5f;
                carryVel = Vector3.zero;
                climbGrabLatch = true;   // standing on top with the button held: not straight back onto the rope
                wallPoint = ropeTopTarget - Vector3.up * (standHeight + 0.03f);
                wallNormal = Vector3.up;
                // FixedUpdate hands the body back to physics (EndClimbPose), standing on the floor.
            }
        }

        /// <summary>Off the rope, with the rope's speed where the pawn was (and a hop up if Space let go).</summary>
        void LeaveRope(RagdollParams p, bool jumped)
        {
            if (rope == null) return;
            Vector3 v = rope.PointVelocity(ropeS + RopeHang, RopeClock);
            if (jumped)
            {
                v += Vector3.up * p.ropeJumpUp;
                RopeJumps++;
                jumpTimer = 0.4f;
            }
            else RopeDrops++;
            rope = null;
            ropeTopping = false;
            ropeRegrab = 0.5f;
            // Dropped off with the button still held (out of stamina, hit): nothing else is taken hold of
            // until it comes up. A jump may catch the next rope in the air.
            if (!jumped && input.grab) climbGrabLatch = true;
            anchorVel = v;
            // Through the air it keeps the swing's speed, the way a jump off a moving platform keeps the
            // platform's, instead of the anchor braking it to a standstill.
            carryVel = Flat(v);
            freeFlight = Mathf.Max(freeFlight, 0.5f);
            airTimer = Mathf.Max(airTimer, 0.15f);
            wallPoint = bodies[0].position;
            wallNormal = Vector3.up;
            // FixedUpdate hands the body back to physics after the pose step (EndClimbPose), at anchorVel.
        }

        /// <summary>Knocked off, hit off, respawned: off the rope without a jump.</summary>
        void DropRope()
        {
            if (rope == null) return;
            LeaveRope(P, false);
        }

        /// <summary>The body on the rope: kinematic, hands on it, hanging along it.</summary>
        void ApplyHangPose(RagdollParams p)
        {
            Vector3 hands, along;
            if (ropeTopping)
            {
                hands = anchorPos + Vector3.up * RopeHang;
                along = Vector3.up;
            }
            else
            {
                double now = RopeClock;
                along = -rope.Direction(now);
                hands = rope.Point(ropeS, now);
                // Easing on, the hands reach for the rope from where the body is.
                if (ropeBlend < 1f) hands = Vector3.Lerp(anchorPos + Vector3.up * RopeHang, hands, ropeBlend);
            }
            float sway = 4f * Mathf.Sin(hangTime * Mathf.PI * 2f * 0.7f);
            PlaceHanging(p, hands, along, along, sway, !ropeTopping);
        }

        /// <summary>The legs on the rope: gripping it, climbing hand over hand when it moves.</summary>
        void RopeHangPose(ref Quaternion armL, ref Quaternion armR, ref Quaternion chest, ref Quaternion head,
                          ref Quaternion thighL, ref Quaternion thighR, ref Quaternion footL, ref Quaternion footR)
        {
            if (rope == null) return;
            float step = ropeClimbing != 0f ? Mathf.Sin(hangTime * Mathf.PI * 2f * 2.2f) : 0f;
            thighL = Quaternion.Euler(-32f + 18f * step, 0f, 0f);
            thighR = Quaternion.Euler(-18f - 18f * step, 0f, 0f);
            footL = footR = Quaternion.Euler(24f, 0f, 0f);
            chest = Quaternion.Euler(-6f, 0f, 0f);
            head = Quaternion.Euler(-16f, 0f, 0f);
        }
    }
}
