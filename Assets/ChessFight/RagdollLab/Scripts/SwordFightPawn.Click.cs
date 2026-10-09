using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public sealed partial class SwordFightPawn
    {
        // R48 click-swing timings and arc, kept separately for live A/B testing.
        public const float Windup = .16f, ActiveTime = .20f, Recovery = .34f;
        public const float SwingDuration = Windup + ActiveTime + Recovery;
        public float SwingAge { get; private set; } = SwingDuration;
        Vector3 swingDirection = Vector3.forward, bufferedAim, previousGrip, previousClickTip;
        readonly HashSet<SwordFightPawn> hitThisSwing = new HashSet<SwordFightPawn>();
        float inputBuffer;
        bool wasActive;

        void CancelClickSwing()
        { inputBuffer = 0; SwingAge = SwingDuration; wasActive = false; hitThisSwing.Clear(); }

        void StepClickSwing(float dt)
        {
            if (Pawn.State != PawnState.Active) { CancelClickSwing(); return; }
            if (inputBuffer > 0 && !Attacking)
            {
                bufferedAim.y = 0;
                swingDirection = bufferedAim.sqrMagnitude > .01f ? bufferedAim.normalized : Pawn.Facing;
                SwingAge = 0; Swings++; inputBuffer = 0; Protection = 0;
                hitThisSwing.Clear(); wasActive = false;
            }
            inputBuffer = Mathf.Max(0, inputBuffer - dt);
            if (!Attacking) return;
            SwingAge += dt;
            bool active = SwingAge >= Windup && SwingAge <= Windup + ActiveTime;
            Vector3 grip = Hand.position, tip = grip + ClickBladeDirection() * .88f;
            if (active)
            {
                if (!wasActive) { previousGrip = grip; previousClickTip = tip; }
                for (int s = 0; s <= 3; s++)
                    DetectClickHit(Vector3.Lerp(previousGrip, grip, s / 3f), Vector3.Lerp(previousClickTip, tip, s / 3f));
            }
            previousGrip = grip; previousClickTip = tip; wasActive = active;
        }

        void DetectClickHit(Vector3 start, Vector3 end)
        {
            if (!Attacking) return;   // stopped this step (a parried cut)
            int count = Physics.OverlapCapsuleNonAlloc(start, end, .15f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var c = overlaps[i];
                if (!RagdollPawn.ColliderOwner.TryGetValue(c, out var target) || target == Pawn ||
                    target.Team == Pawn.Team || c.GetComponent<SwordBladeContact>() != null) continue;
                var victim = target.GetComponent<SwordFightPawn>();
                if (victim == null || !victim.Alive || victim.Protection > 0 || !hitThisSwing.Add(victim)) continue;
                // A king in its guard (the skill test scene) takes the cut and answers it.
                if (victim.Skills != null && victim.Skills.Parry(this, c.ClosestPoint(Hand.position))) { HitFlash = .1f; CancelClickSwing(); return; }
                Vector3 away = target.Hips.position - Pawn.Hips.position; away.y = 0;
                Vector3 push = (swingDirection * .6f + away.normalized * .4f).normalized;
                target.TakeHit(push * 3.4f + Vector3.up * 1.05f, .65f, 0, true);
                victim.StopCombat(); victim.HitFlash = .14f; HitFlash = .1f; HitsLanded++;
            }
        }

        Vector3 ClickBladeDirection()
        {
            if (!Alive || Pawn.State != PawnState.Active) return Hand.rotation * Vector3.right;
            float angle = -25f;
            if (Attacking)
            {
                if (SwingAge < Windup) angle = Mathf.Lerp(-25, -85, SwingAge / Windup);
                else if (SwingAge < Windup + ActiveTime) angle = Mathf.Lerp(-85, 85, (SwingAge - Windup) / ActiveTime);
                else angle = Mathf.Lerp(85, -25, (SwingAge - Windup - ActiveTime) / Recovery);
            }
            Vector3 forward = Attacking ? swingDirection : Pawn.Facing;
            // The posed blade has no collider: it is kept over the floor, not swept through it.
            return AboveFloor(Hand.position, (Quaternion.AngleAxis(angle, Vector3.up) * forward + Vector3.up * (Attacking ? -.32f : .35f)).normalized);
        }
        Quaternion? ClickSwordPose(int part)
        {
            if (!Alive || Pawn.State != PawnState.Active) return null;
            if (part == (int)BodyId.Chest && Attacking)
            {
                float turn = Mathf.Clamp(Vector3.SignedAngle(Pawn.Facing, swingDirection, Vector3.up), -65, 65);
                float sweep = Mathf.Sin(Mathf.Clamp01((SwingAge - Windup) / ActiveTime) * Mathf.PI);
                return Quaternion.Euler(-8f * sweep, turn * .65f, -9f * sweep);
            }
            if (part != (int)BodyId.ArmR) return null;
            return Quaternion.FromToRotation(Vector3.right, Pawn.bodies[(int)BodyId.Chest].transform.InverseTransformDirection(ClickBladeDirection()));
        }
        void PoseClickSword() => sword.SetPositionAndRotation(Hand.position, Quaternion.LookRotation(ClickBladeDirection()));
    }
}
