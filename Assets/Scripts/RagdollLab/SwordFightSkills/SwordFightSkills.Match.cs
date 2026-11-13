using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// What the Sword Fight prototype match (R149, <see cref="SwordFightMatch"/>, spec §7) adds to the piece skills; the
    /// skill test scenes keep them as they were (both switches are off there):
    /// <list type="bullet">
    /// <item>the king's guard takes only a sword's cut (<see cref="GuardTakesSwordOnly"/>; skills land on him as on anyone);</item>
    /// <item>the pawn's F: its first two steps (<see cref="PawnSteps"/>, spec §7.2): 1.5 m in 0.2 s, a 0.22 s gap, a second
    /// step that may turn. An enemy met diagonally (25–75° off the step) is knocked down; one met head-on (under 25°)
    /// bounces both apart, the pawn staggers and its steps are over. No aim, 5 s cooldown. Nothing stops it stepping off
    /// the board.</item>
    /// <item>cooldowns set as the match deals them (<see cref="Ready"/>: a stolen piece's F ready, its E half spent).</item>
    /// </list>
    /// </summary>
    public partial class SwordFightSkills
    {
        /// <summary>The king's guard blocks only swords (the prototype match).</summary>
        public static bool GuardTakesSwordOnly;
        /// <summary>The pawn has its two steps on F (the prototype match).</summary>
        public static bool PawnSteps;

        Vector3 rawMove;
        int pawnStep;          // 0 none, 1 the first step, 2 the gap, 3 the second step
        Vector3 pawnDir;

        /// <summary>Every running skill called off, the F skill ready and the E skill <paramref name="edgeShare"/> of its
        /// cooldown away (0 = ready): the start of the match (0), a stolen piece (0.5, spec §7.3), a respawn as a pawn.</summary>
        public void Ready(float edgeShare)
        {
            ResetSkill();
            pawnStep = 0;
            if (edgeShare > 0f && EdgeParams != null && HasSkill) StartEdgeCooldown(edgeShare);
        }

        /// <summary>Development (F10): every cooldown over at once.</summary>
        public void ClearCooldowns()
        {
            cooldownLeft = 0f;
            edgeCooldownLeft = 0f;
        }

        void StepPawn(bool press, float dt)
        {
            if (Stage == SfStage.None)
            {
                if (!press) return;
                if (cooldownLeft > 0f) { Say($"{Pawn.DisplayName}: 첫 두 걸음 쿨타임 {cooldownLeft:0.0}초"); return; }
                if (Pawn.State != PawnState.Active) return;
                SetStage(SfStage.Active, "첫 두 걸음");
                hitSet.Clear();
                pawnStep = 1;
                pawnDir = StepDirection();
                Dir = pawnDir;
                Pawn.BeginDash(pawnDir, S.pawnStepSpeed);
                Raise(SfFxKind.PawnStep, null, Pawn.Hips.position, pawnDir, 1f);
                return;
            }
            if (Pawn.State != PawnState.Active) { EndPawn(); return; }
            float t = StageTime;
            if (pawnStep == 1 && t >= S.pawnStepTime)
            {
                Pawn.StopDash(0.2f);
                pawnStep = 2;
            }
            else if (pawnStep == 2 && t >= S.pawnStepTime + S.pawnStepGap)
            {
                // The second step goes where the keys say now (or straight on).
                pawnDir = StepDirection();
                Dir = pawnDir;
                pawnStep = 3;
                Pawn.BeginDash(pawnDir, S.pawnStepSpeed);
                Raise(SfFxKind.PawnStep, null, Pawn.Hips.position, pawnDir, 2f);
            }
            else if (pawnStep == 3 && t >= 2f * S.pawnStepTime + S.pawnStepGap)
            {
                EndPawn();
                return;
            }
            if (pawnStep == 1 || pawnStep == 3) PawnContacts();
        }

        Vector3 StepDirection()
        {
            Vector3 d = Flat(rawMove);
            if (d.sqrMagnitude < 0.01f) d = Flat(rawAim);
            if (d.sqrMagnitude < 0.01f) d = Flat(Pawn.Facing);
            return d.normalized;
        }

        void EndPawn()
        {
            pawnStep = 0;
            Finish();
        }

        void PawnContacts()
        {
            Vector3 me = Flat(Pawn.Hips.position);
            foreach (var other in All)
            {
                if (!IsEnemy(other) || hitSet.Contains(other.Pawn)) continue;
                Vector3 to = Flat(other.Pawn.Hips.position) - me;
                if (to.magnitude > S.pawnReach) continue;
                float angle = Vector3.Angle(pawnDir, to);
                if (angle > S.pawnDiagonal) continue;
                hitSet.Add(other.Pawn);
                Vector3 at = Vector3.Lerp(Pawn.bodies[(int)BodyId.Chest].position, other.Pawn.bodies[(int)BodyId.Chest].position, 0.5f);
                if (angle < S.pawnHeadOn)
                {
                    // Head-on: both bounce, the pawn staggers (a 0-second knockdown is a stagger) and its steps are over.
                    Pawn.StopDash(0f);
                    other.Pawn.TakeHit(pawnDir * S.pawnBounce, 0f, 0f, false);
                    Pawn.TakeHit(-pawnDir * S.pawnBounce, 0f, 0f, false);
                    Raise(SfFxKind.PawnBounce, other.Pawn, at, pawnDir, S.pawnBounce);
                    Say($"{Pawn.DisplayName} ↔ {other.Pawn.DisplayName}: 정면으로 부딪혀 튕김");
                    EndPawn();
                    return;
                }
                Vector3 away = to.sqrMagnitude > 1e-4f ? to.normalized : pawnDir;
                other.Pawn.TakeHit(away * S.pawnPush + Vector3.up * S.pawnPushUp, S.knockdownSeconds, 0f, true);
                other.Fighter.StopCombat();
                if (other.Stage != SfStage.None) { other.Clear(); other.StartCooldown(); }
                Raise(SfFxKind.PawnHit, other.Pawn, at, away, S.pawnPush);
                Say($"{Pawn.DisplayName} → {other.Pawn.DisplayName}: 폰 걸음 (대각선 {angle:0}°)");
            }
        }
    }
}
