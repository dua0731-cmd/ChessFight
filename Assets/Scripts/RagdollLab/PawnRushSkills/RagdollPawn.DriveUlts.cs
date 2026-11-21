using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The six driven ultimates' common frame (JY-skill-lab, 2026-11-22; Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6.1-6.6,
    /// Docs/Skills/PAWN_RUSH_SKILL_SYSTEM.md 4). The user: "걍 쎄고 강하고 적들이 훨씬 앞서 있다 그래도 뒤엎을 수 있는 강력하고
    /// 멋진 글로벌 스킬인데 너무 짧게 끝나거나 뻔하진 않은 그런 거. 쓰는 재미가 있는 걸로", and of the knight's first ones: "시전
    /// 중에도 계속 유저랑 상호작용할 수 있고, 유저의 실력에 따라 더 좋은 결과를 낼 수도 있고, 덜 좋은 결과를 낼 수도 있어야지".
    ///
    ///   the key       Q goes at once (on a full team gauge), on the run or in the air; the caster keeps moving
    ///   the time      10-12 s the caster drives (×1.5 15 s, ×2 20 s, ×0.5 half: the team's comeback); the presses it has
    ///                 (dashes, shots, dives, waves) scale the same way
    ///   the keys      the left click, the right click and the jump key are the ultimate's while it runs (DriveKeys): they
    ///                 do not dive, grab or jump
    ///   the end       its time out: the judgement (SkillFxKind.UltHit) by how many of the other team it hit, as a chess move
    ///                 is judged: !! brilliant, ! great, ★ best, ?! inaccuracy, ?? blunder; 0.6 s later it is over
    ///   the screens   the common cast (PawnRushSkillFx.Ult*.cs) at the key - night, the aura, the banner, every enemy outlined
    ///                 in red through walls for the caster - and each piece's own (PawnRushSkillFx.Drive*.cs)
    ///
    /// planNote carries each one's small state to every PC (its uses left, a level, flags); planTime its time so far.
    /// Not seen in Unity yet.
    /// </summary>
    public partial class RagdollPawn
    {
        // The keys while one runs (DriveKeys, before the run): the left click this step and held, the right click this step,
        // the jump key this step. Taken by the update that reads them.
        bool driveClick, driveHeld, driveRight, driveRightWas, driveJump;
        float driveSeconds;
        int driveUses;
        readonly HashSet<RagdollPawn> driveHit = new HashSet<RagdollPawn>();

        /// <summary>A driven ultimate's whole time (s; the host's: any PC has it from the cast's moment).</summary>
        public float DriveSeconds => driveSeconds;

        /// <summary>Before the run (KitTwoKeys): the clicks and the jump are the ultimate's. The bishop keeps his jump (he walks
        /// and shoots).</summary>
        void DriveKeys()
        {
            driveClick |= input.shove;
            driveHeld = input.shoveHeld;
            bool right = input.grab;
            driveRight |= right && !driveRightWas;
            driveRightWas = right;
            input.shove = false;
            input.shoveHeld = false;
            input.grab = false;
            if (activeSkill == PawnRushSkillIds.GreatDiagonal)
            {
                if (driveHeld) input.sprint = false;   // gathering the light: walking
                return;
            }
            driveJump |= input.jump;
            input.jump = false;
        }

        bool TakeClick()
        {
            bool c = driveClick;
            driveClick = false;
            return c;
        }

        bool TakeRight()
        {
            bool c = driveRight;
            driveRight = false;
            return c;
        }

        bool TakeJump()
        {
            bool c = driveJump;
            driveJump = false;
            return c;
        }

        /// <summary>The key: the ultimate starts at once. <paramref name="seconds"/> and <paramref name="uses"/> at ×1; the
        /// team's comeback scales both (<paramref name="fixedSeconds"/>, a part that does not scale, is added: the pawn's run).</summary>
        void BeginDrive(PawnRushSkillParams s, int id, float seconds, int uses, string detail, float fixedSeconds = 0f)
        {
            pressedSkill = id;
            SkillUses++;
            ultF = Comeback(team);
            ultTaken.Clear();
            driveHit.Clear();
            driveSeconds = Mathf.Clamp(fixedSeconds + ByComeback(ultF, seconds, seconds * 2f), 2f, 25f);
            driveUses = uses > 0 ? Mathf.Clamp(Mathf.RoundToInt(ByComeback(ultF, uses, uses * 2f)), 1, 15) : 0;
            driveClick = driveRight = driveJump = false;
            driveRightWas = input.grab;
            planPhase = PlanPhase.Active;
            planTime = 0f;
            planTarget = null;
            planDir = AimFlat();
            planPoint = FeetPoint;
            planNote = (byte)driveUses;
            SkillDetail = detail;
            Fx(SkillFxKind.UltCast, null, FeetPoint, planDir, id, driveSeconds);
            Log($"{DisplayName}: {PawnRushSkillBook.Name(id)} — {driveSeconds:0.#}초" + (driveUses > 0 ? $", {driveUses}번" : "") + $" (역전 배율 ×{ultF:0.#})");
        }

        /// <summary>Time left in the ultimate (the host's).</summary>
        float DriveLeft => driveSeconds - planTime;

        /// <summary>An enemy this ultimate may still hit: standing, not shielded, not hit by it already (a piece loses at most
        /// about 3 s of control to one ultimate).</summary>
        bool DriveFoe(RagdollPawn e) => UltFoe(e) && !driveHit.Contains(e);

        /// <summary>Its time is out: the judgement, then a moment to see it.</summary>
        void FinishDrive(PawnRushSkillParams s)
        {
            int hit = driveHit.Count, grade = DriveGrade(s, hit);
            Fx(SkillFxKind.UltHit, null, FeetPoint, planDir, hit, grade);
            Log($"{PawnRushSkillBook.Name(activeSkill)}: 적 {hit}명 → {GradeName(grade)}");
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
        }

        /// <summary>The recovery after the judgement; then it is over.</summary>
        void UpdateDriveRecovery(PawnRushSkillParams s, float dt)
        {
            planTime += dt;
            if (planTime < 0.6f) return;
            Fx(SkillFxKind.UltEnd, null, FeetPoint, planDir, 0);
            EndTeamUlt(s);
        }

        /// <summary>The judgement: how many of the other team it hit (six of six is brilliant).</summary>
        int DriveGrade(PawnRushSkillParams s, int hit)
        {
            int enemies = 0;
            foreach (var p in All)
                if (p != null && p != this && IsEnemyOf(p)) enemies++;
            float share = hit / (float)Mathf.Max(1, enemies);
            return hit >= 6 || share >= s.driveBrilliant ? GradeBrilliant
                 : share >= s.driveGreat ? GradeGreat
                 : share >= s.driveBest ? GradeBest
                 : hit > 0 ? GradeInaccuracy : GradeBlunder;
        }

        /// <summary>What a driven ultimate leaves running when it ends early or as it should (EndPlanSkill): its flight, its
        /// body (the queen it became, the column it rolled as, the king's size), the procession.</summary>
        void ClearDrive()
        {
            driveClick = driveHeld = driveRight = driveJump = false;
            if (ultFlying)
            {
                ultFlying = false;
                EndFlight();
            }
            ClearNightmare();
            ClearPromotionRun();   // (a queen again a pawn)
            ClearRookRoll();
            ClearQueenDescent();
        }

        // ---------------------------------------------------------------- where the camera looks

        /// <summary>The way the camera looks (the aim, with its pitch).</summary>
        Vector3 AimDir()
        {
            Vector3 a = input.aim;
            return a.sqrMagnitude > 1e-4f ? a.normalized : FlatDir(facing);
        }

        /// <summary>About where the third-person camera stands for <paramref name="p"/> (its head, up and back along the aim):
        /// the host has the aim, not the camera; the piece's own PC works from the same point, so both find the same spot.</summary>
        public static Vector3 DriveEye(RagdollPawn p, Vector3 aim) => p.bodies[(int)BodyId.Head].position + Vector3.up * 1.1f - aim.normalized * 3.5f;

        /// <summary>The floor at <paramref name="at"/>: the highest one under a point <paramref name="above"/> m over it,
        /// down to <paramref name="below"/> m under it (pieces and triggers do not count), or none.</summary>
        public static Vector3? DriveFloor(Vector3 at, float above = 20f, float below = 40f)
        {
            Vector3? found = null;
            float best = float.MaxValue;
            foreach (var h in Physics.RaycastAll(at + Vector3.up * above, Vector3.down, above + below, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.distance >= best || h.normal.y < 0.4f || h.collider.GetComponentInParent<RagdollPawn>() != null) continue;
                best = h.distance;
                found = h.point;
            }
            return found;
        }

        /// <summary>Open sky over a floor point (nothing solid within <paramref name="up"/> m over a piece standing there):
        /// what the knight and the queen can come down on.</summary>
        public static bool SkyOver(Vector3 floor, float up)
        {
            foreach (var h in Physics.RaycastAll(floor + Vector3.up * 2.4f, Vector3.up, up, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.GetComponentInParent<RagdollPawn>() == null) return false;
            return true;
        }

        /// <summary>The floor point the camera looks at, at most <paramref name="reach"/> m away along the ground: where the
        /// look meets a floor; a wall, the floor at its foot; the sky or further, the floor <paramref name="reach"/> m along
        /// the look's way. <paramref name="far"/>: it was cut to the reach.</summary>
        public static Vector3 DriveAimPoint(RagdollPawn p, Vector3 aim, float reach, out bool far)
        {
            far = false;
            Vector3 me = p.FeetPoint, flat = FlatDir(aim);
            Vector3 eye = DriveEye(p, aim);
            float best = float.MaxValue;
            RaycastHit hit = default;
            foreach (var h in Physics.RaycastAll(eye, aim.normalized, reach * 2.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.distance >= best || h.collider.GetComponentInParent<RagdollPawn>() != null) continue;
                best = h.distance;
                hit = h;
            }
            if (best < float.MaxValue)
            {
                Vector3 at = hit.point;
                if (hit.normal.y < 0.5f) at -= flat * 0.6f;   // a wall: the floor at its foot, this side
                if (Flat(at - me).magnitude <= reach)
                {
                    var f = hit.normal.y >= 0.5f ? at : DriveFloor(at);
                    if (f.HasValue) return f.Value;
                }
            }
            far = true;
            Vector3 point = me + flat * reach;
            return DriveFloor(point) ?? point;
        }
    }
}
