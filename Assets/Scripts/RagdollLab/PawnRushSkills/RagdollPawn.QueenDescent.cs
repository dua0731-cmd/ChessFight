using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Q-궁 여왕 강림 (JY-skill-lab, 2026-11-22, Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6.5): the queen goes anywhere on the
    /// board, and her sacrifice is the brilliant move itself. For 10 s:
    ///
    ///   the flight    she rises 20 m over the course (0.8 s) and flies at 30 m/s where the move keys take her (the
    ///                 camera's way is the keys' way), 20 m over whatever floor is under her (under a ceiling, below it).
    ///   a dive        the left click on a floor point the camera looks at (within 60 m, open sky over it): her shadow
    ///                 there for 1 s while she comes down on it; every enemy within 8 m is thrown 25 m and floored 2.5 s;
    ///                 then she rises again. Three (×1.5 four, ×2 six).
    ///   the sacrifice the right click instead, for her last dive or any before: 16 m round, and the queen is taken (the
    ///                 course sends her to her checkpoint; where there is none she is floored) - the ultimate ends there.
    ///   the end       her time out without it: she comes down where she is, and the judgement.
    ///
    /// planNote: the dives left (low four bits), the stage (QdStageShift: QdFly, QdDive, QdRise, QdDown), QdSacrifice.
    /// planPoint the dive's point. Not seen in Unity yet.
    /// </summary>
    public partial class RagdollPawn
    {
        public const byte QdDivesMask = 15, QdStageShift = 4, QdSacrifice = 64;
        public const int QdFly = 0, QdDive = 1, QdRise = 2, QdDown = 3;

        int qdStage;
        float qdT;
        bool qdSac, qdTaken, qdFlying;
        Vector3 qdVel, qdFrom;

        void UpdateQueenDescent(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill(true)) break;
                    BeginDrive(s, PawnRushSkillIds.QueenDescent, s.qdSeconds, s.qdDives, "여왕 강림 — 이동 키로 날기, 좌클릭 내리꽂기, 우클릭 = 퀸 희생");
                    DropHook();
                    DropRope();
                    EndClimbPose();
                    LetGo(0.3f);
                    qdStage = QdRise;
                    qdT = 0f;
                    qdSac = qdTaken = false;
                    qdFlying = true;
                    qdVel = Vector3.zero;
                    qdFrom = bodies[0].position;
                    QdNote();
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    qdT += dt;
                    grabImmune = Mathf.Max(grabImmune, 0.25f);
                    switch (qdStage)
                    {
                        case QdRise: QdFlyStep(s, dt, true); break;
                        case QdFly: QdFlyStep(s, dt, false); break;
                        case QdDive: QdDiveStep(s, dt); break;
                        default: QdDownStep(s, dt); break;
                    }
                    QdNote();
                    break;
                case PlanPhase.Recovery:
                    if (!qdTaken)
                    {
                        UpdateDriveRecovery(s, dt);
                        break;
                    }
                    // The sacrifice: the blast seen, then she is taken.
                    planTime += dt;
                    if (planTime < s.qdTakenShow) break;
                    Fx(SkillFxKind.UltEnd, null, FeetPoint, planDir, 0);
                    EndTeamUlt(s);
                    if (SkillSendBack != null) SkillSendBack(this);
                    else Knockdown("여왕 강림 (퀸 희생: 퀸이 잡힘)");
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void QdNote()
        {
            planNote = (byte)(Mathf.Clamp(driveUses, 0, 15) | (Mathf.Clamp(qdStage, 0, 3) << QdStageShift) | (qdSac ? QdSacrifice : 0));
        }

        /// <summary>The height she flies at over a point: 20 m over its floor, below a ceiling over it.</summary>
        float QdCruise(PawnRushSkillParams s, Vector3 at)
        {
            var floor = DriveFloor(at, 4f, 80f);
            float want = (floor.HasValue ? floor.Value.y : at.y - s.qdHigh) + s.qdHigh;
            foreach (var h in Physics.RaycastAll(at, Vector3.up, Mathf.Max(0f, want - at.y) + 3f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.GetComponentInParent<RagdollPawn>() == null) want = Mathf.Min(want, h.point.y - 2.5f);
            return want;
        }

        void QdFlyStep(PawnRushSkillParams s, float dt, bool rising)
        {
            Vector3 at = bodies[0].position, keys = Flat(skillMoveRaw);
            Vector3 wantVel = (keys.sqrMagnitude > 1f ? keys.normalized : keys) * s.qdSpeed;
            qdVel = Vector3.MoveTowards(qdVel, wantVel, s.qdAccel * dt);
            Vector3 move = qdVel * dt;
            if (move.sqrMagnitude > 1e-6f && GdWall(at, move.normalized, move.magnitude + 0.8f, out var wall) && wall.normal.y < 0.6f)
            {
                Vector3 n = Flat(wall.normal);
                if (n.sqrMagnitude > 1e-4f) qdVel = Vector3.ProjectOnPlane(qdVel, n.normalized);
                move = qdVel * dt;
            }
            if (qdVel.sqrMagnitude > 1f) planDir = FlatDir(qdVel);
            Vector3 next = at + move;
            float cruise = QdCruise(s, next);
            float y = rising ? Mathf.Lerp(qdFrom.y, cruise, Smooth01(qdT / s.qdRiseTime)) : Mathf.MoveTowards(at.y, cruise, s.qdClimb * dt);
            next.y = y;
            Fly(next, (next - at) / Mathf.Max(1e-4f, dt), 0f, default, 18f);
            if (rising)
            {
                if (qdT >= s.qdRiseTime)
                {
                    qdStage = QdFly;
                    qdT = 0f;
                }
                TakeClick();
                TakeRight();
                return;
            }
            if (DriveLeft <= 0f)
            {
                QdToDown();
                return;
            }
            bool click = TakeClick(), sacrifice = TakeRight();
            if ((!click && !sacrifice) || driveUses <= 0) return;
            Vector3 point = DriveAimPoint(this, AimDir(), s.qdReach, out _);
            if (!SkyOver(point, Mathf.Max(4f, at.y - point.y - 3f)))
            {
                Fx(SkillFxKind.SkillFail, null, point, planDir, (int)PieceKind.Queen);
                Log("여왕 강림: 지붕 밑에는 못 내리꽂음");
                return;
            }
            qdSac = sacrifice;
            qdStage = QdDive;
            qdT = 0f;
            qdFrom = at;
            planPoint = point;
            planDir = FlatDir(point - at);
            driveUses--;
            Fx(SkillFxKind.DriveWarn, null, point, planDir, qdSac ? 2 : 1, qdSac ? s.qdSacRadius : s.qdRadius);
            Log(qdSac ? "여왕 강림: 퀸 희생!" : $"여왕 강림: 내리꽂기 (남은 {driveUses}번)");
        }

        void QdDiveStep(PawnRushSkillParams s, float dt)
        {
            TakeClick();
            TakeRight();
            Vector3 at = bodies[0].position, to = planPoint + Vector3.up * standHeight;
            float u = Mathf.Clamp01((qdT + dt) / s.qdDiveTime);
            // A swoop: over the point first, then straight down on it, faster and faster.
            Vector3 over = new Vector3(to.x, Mathf.Max(qdFrom.y, to.y + 6f), to.z);
            float flat = Smooth01(Mathf.Clamp01(u / 0.6f)), down = u * u * u;
            Vector3 next = Vector3.Lerp(Vector3.Lerp(qdFrom, over, flat), to, down);
            if (qdT + dt < s.qdDiveTime)
            {
                Fly(next, (next - at) / Mathf.Max(1e-4f, dt), 0f, default, 20f);
                return;
            }
            // The impact.
            Fly(to, (to - at) / Mathf.Max(1e-4f, dt), 0f, default, 20f);
            float radius = qdSac ? s.qdSacRadius : s.qdRadius;
            Vector3 c = planPoint;
            int n = 0;
            foreach (var e in All)
            {
                if (!DriveFoe(e)) continue;
                Vector3 d = Flat(e.FeetPoint - c);
                if (d.magnitude > radius || Mathf.Abs(e.FeetPoint.y - c.y) > 4f) continue;
                driveHit.Add(e);
                n++;
                Vector3 away = d.sqrMagnitude > 0.04f ? d.normalized : planDir;
                var floor = DriveFloor(e.FeetPoint + away * s.qdThrow, 5f, 15f) ?? e.FeetPoint;
                Fx(SkillFxKind.UltStrike, e, e.bodies[0].position, away, driveHit.Count, qdSac ? 1f : 0f);
                e.UltThrow(floor, 0.75f, 7f, s.qdKnock, this);
                e.LastSkillHit = qdSac ? $"여왕 강림: 퀸 희생에 {s.qdThrow:0} m" : $"여왕 강림: 내리꽂기에 {s.qdThrow:0} m";
            }
            Fx(SkillFxKind.DriveLand, null, c, planDir, n, radius);
            FeelHit(null, c, planDir, FeelUltimate, Mathf.Max(1, n), false);
            Log($"여왕 강림: {(qdSac ? "퀸 희생" : "내리꽂기")} 둘레 {radius:0} m 적 {n}명");
            if (qdSac)
            {
                EndFlight(Vector3.zero);
                qdFlying = false;
                qdTaken = true;
                FinishDrive(s);
                return;
            }
            qdStage = QdRise;
            qdT = 0f;
            qdFrom = to;
            qdVel = Vector3.zero;
        }

        void QdToDown()
        {
            qdStage = QdDown;
            qdT = 0f;
            qdFrom = bodies[0].position;
            planPoint = DriveFloor(qdFrom, 2f, 80f) ?? qdFrom - Vector3.up * 20f;
        }

        void QdDownStep(PawnRushSkillParams s, float dt)
        {
            TakeClick();
            TakeRight();
            Vector3 at = bodies[0].position, to = planPoint + Vector3.up * standHeight;
            float u = Mathf.Clamp01((qdT + dt) / s.qdDownTime);
            Vector3 next = Vector3.Lerp(qdFrom, to, Smooth01(u));
            if (u < 1f)
            {
                Fly(next, (next - at) / Mathf.Max(1e-4f, dt), 0f, default, 18f);
                return;
            }
            EndFlight(Vector3.zero);
            KeepSkillLandingSafe();
            qdFlying = false;
            Fx(SkillFxKind.DriveState, null, planPoint, planDir, QdDown, 0f);
            FinishDrive(s);
        }

        static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        void ClearQueenDescent()
        {
            if (!qdFlying) return;
            qdFlying = false;
            EndFlight();
        }
    }
}
