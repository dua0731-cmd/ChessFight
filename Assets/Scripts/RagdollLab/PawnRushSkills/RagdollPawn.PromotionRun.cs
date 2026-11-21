using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// P-궁 승격, e8=Q!! (JY-skill-lab, 2026-11-22, Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6.1): the weakest piece reaches the
    /// last rank and becomes the strongest. Three acts:
    ///
    ///   the run      3 s at 25 m/s that nobody stops, steered with the move keys (150°/s); an enemy in the way is knocked
    ///                aside (5 m, a sway). A wall turns it along the wall; an edge with no floor near under it stops it.
    ///   the queen    at the end it leaps (0.55 s) and comes down a 6 m queen (the piece is a queen: every PC draws it
    ///                6 m tall, PawnRushSkillFx.PromotionRun.cs); every enemy within 10 m is thrown 12 m.
    ///   the dashes   9 s a queen (×1.5 13.5, ×2 18): the left click shows a 30 m line on the floor for 0.3 s and then
    ///                dashes along it (the camera's way) through everyone on it (thrown 20 m, floored 2 s); 0.6 s between
    ///                dashes, four (×1.5 six, ×2 eight). Each one hit makes the queen 10% bigger (1.5x at most, its reach
    ///                too). A roof lower than the queen stops a dash short: under a low roof is safe.
    ///   the end      the queen's time out, or a second after its last dash: a pawn again, and the judgement.
    ///
    /// planNote: the dashes left (low four bits), the act (PrStageShift: 0 the run, 1 the leap, 2 the queen), PrWindup,
    /// PrDashing. planDir the run's or the dash's way, planPoint the dash's end. Not seen in Unity yet.
    /// </summary>
    public partial class RagdollPawn
    {
        public const byte PrDashMask = 15, PrStageShift = 4, PrWindup = 64, PrDashing = 128;
        public const int PrRun = 0, PrLeap = 1, PrQueen = 2;

        int prStage, prDashHits;
        float prClock, prGap, prDashT;
        bool prWindup, prDashing, prWasQueen;
        Vector3 prDashFrom, prDashTo;
        PieceKind prFrom = PieceKind.Pawn;
        readonly HashSet<RagdollPawn> prShoved = new HashSet<RagdollPawn>();

        /// <summary>How big the queen is now against her 6 m (1 .. 1.5: 10% a dash's hit).</summary>
        float PrGrowth(PawnRushSkillParams s) => Mathf.Min(s.prGrowMax, 1f + s.prGrow * prDashHits);

        void UpdatePromotionRun(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill(true)) break;
                    BeginDrive(s, PawnRushSkillIds.PromotionRun, s.prQueen, s.prDashes, "승격 — 이동 키로 질주, 퀸이 되면 좌클릭 돌진", s.prRun + s.prLeap);
                    prStage = PrRun;
                    prClock = 0f;
                    prGap = 0f;
                    prDashHits = 0;
                    prWindup = prDashing = false;
                    prShoved.Clear();
                    planDir = MoveOrAim();
                    PrNote();
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    prClock += dt;
                    switch (prStage)
                    {
                        case PrRun: PrRunStep(s, dt); break;
                        case PrLeap: PrLeapStep(s, dt); break;
                        default: PrQueenStep(s, dt); break;
                    }
                    PrNote();
                    break;
                case PlanPhase.Recovery:
                    UpdateDriveRecovery(s, dt);
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void PrNote()
        {
            planNote = (byte)(Mathf.Clamp(driveUses, 0, 15) | (Mathf.Clamp(prStage, 0, 3) << PrStageShift) | (prWindup ? PrWindup : 0) | (prDashing ? PrDashing : 0));
        }

        // ---------------------------------------------------------------- the run

        void PrRunStep(PawnRushSkillParams s, float dt)
        {
            TakeClick();
            grabImmune = Mathf.Max(grabImmune, 0.25f);
            Vector3 keys = Flat(skillMoveRaw);
            if (keys.sqrMagnitude > 0.04f)
                planDir = Vector3.RotateTowards(planDir, keys.normalized, s.prTurn * Mathf.Deg2Rad * dt, 0f);
            planDir = FlatDir(planDir);
            Vector3 at = bodies[0].position, step = planDir * (s.prSpeed * dt);
            // A wall: along it.
            if (GdWall(at, planDir, s.prSpeed * dt + 0.6f, out var wall) && wall.normal.y < 0.6f)
            {
                Vector3 along = Vector3.ProjectOnPlane(planDir, Flat(wall.normal).normalized);
                if (along.sqrMagnitude > 0.04f) planDir = FlatDir(along);
                step = planDir * (s.prSpeed * dt * Mathf.Clamp01(along.magnitude));
            }
            Vector3 next = at + step;
            var floor = DriveFloor(next, 1.5f, 6f);
            if (!floor.HasValue)
            {
                // No floor near under the next step: it stops at the edge (the run does not throw itself off).
                PrToLeap(s);
                return;
            }
            Vector3 hips = floor.Value + Vector3.up * standHeight;
            Fly(hips, (hips - at) / Mathf.Max(1e-4f, dt), 0f, default, 20f);
            planPoint = floor.Value + planDir * 20f;
            foreach (var e in All)
            {
                if (!UltFoe(e) || prShoved.Contains(e)) continue;
                Vector3 d = Flat(e.bodies[0].position - at);
                if (d.magnitude > s.prShoveReach || Mathf.Abs(e.bodies[0].position.y - at.y) > 2f) continue;
                prShoved.Add(e);
                Vector3 right = new Vector3(planDir.z, 0f, -planDir.x), side = Vector3.Dot(d, right) >= 0f ? right : -right;
                e.Kick(FlatDir(side + planDir * 0.4f), s.prShove, 0.3f, true);
                e.Stagger(0.4f);
                e.LastSkillHit = "승격: 질주에 옆으로 튕김";
                Fx(SkillFxKind.PawnHit, e, e.bodies[0].position, side, 0);
                FeelHit(e, e.bodies[0].position, side, FeelWeak);
            }
            if (prClock >= s.prRun) PrToLeap(s);
        }

        // ---------------------------------------------------------------- the leap to the queen

        void PrToLeap(PawnRushSkillParams s)
        {
            prStage = PrLeap;
            prClock = 0f;
            ultFrom = bodies[0].position;
            Vector3 land = DriveFloor(FeetPoint + planDir * s.prLeapAhead, 3f, 10f) ?? FeetPoint;
            planPoint = land;
            ultTo = land + Vector3.up * standHeight;
            ultFlightT = 0f;
            ultFlightTime = s.prLeap;
            ultFlightTop = s.prLeapTop;
            prFrom = piece;
            prWasQueen = piece == PieceKind.Queen;
            if (!prWasQueen) SetPiece(PieceKind.Queen);
            Fx(SkillFxKind.DriveState, null, FeetPoint, planDir, PrLeap, s.prLeap);
            Log("승격: 끝줄! 퀸으로");
        }

        void PrLeapStep(PawnRushSkillParams s, float dt)
        {
            TakeClick();
            ultFlightT += dt;
            grabImmune = Mathf.Max(grabImmune, 0.25f);
            if (ultFlightT < ultFlightTime)
            {
                Vector3 at = bodies[0].position, next = UltArc((ultFlightT + dt) / ultFlightTime);
                Fly(next, (next - at) / Mathf.Max(1e-4f, dt), 0f, default, 18f);
                return;
            }
            EndFlight(Vector3.zero);
            KeepSkillLandingSafe();
            prStage = PrQueen;
            prClock = 0f;
            prGap = 0f;
            Vector3 c = FeetPoint;
            int n = 0;
            foreach (var e in All)
            {
                if (!DriveFoe(e)) continue;
                Vector3 d = Flat(e.FeetPoint - c);
                if (d.magnitude > s.prLand || Mathf.Abs(e.FeetPoint.y - c.y) > 3f) continue;
                driveHit.Add(e);
                n++;
                Vector3 away = d.sqrMagnitude > 0.04f ? d.normalized : planDir;
                var to = DriveFloor(e.FeetPoint + away * s.prLandThrow, 4f, 12f) ?? e.FeetPoint;
                Fx(SkillFxKind.UltStrike, e, e.bodies[0].position, away, driveHit.Count, 0f);
                e.UltThrow(to, 0.8f, 4f, s.prLandKnock, this);
                e.LastSkillHit = $"승격: 퀸이 내려서며 {s.prLandThrow:0} m 날림";
            }
            Fx(SkillFxKind.DriveLand, null, c, planDir, n, s.prLand);
            FeelHit(null, c, planDir, FeelUltimate, Mathf.Max(1, n), false);
            Log($"승격: e8=Q!! 둘레 {s.prLand:0} m 적 {n}명 날아감");
        }

        // ---------------------------------------------------------------- the queen's dashes

        void PrQueenStep(PawnRushSkillParams s, float dt)
        {
            if (prDashing)
            {
                PrDashStep(s, dt);
                return;
            }
            if (prWindup)
            {
                prDashT += dt;
                if (prDashT >= s.prWarn) PrDashGo(s);
                return;
            }
            prGap -= dt;
            if (DriveLeft <= 0f || driveUses <= 0 && prGap <= -1f)
            {
                PrBackToPawn();
                FinishDrive(s);
                return;
            }
            // (A click during a dash or the gap after it waits for the gap to end: dashes chain.)
            if (prGap > 0f || driveUses <= 0 || !TakeClick()) return;
            // The line it will dash along: the camera's way, cut short by a wall or a roof under the queen's height.
            planDir = AimFlat();
            prDashFrom = FeetPoint;
            prDashTo = PrDashEnd(s, prDashFrom, planDir);
            planPoint = prDashTo;
            prWindup = true;
            prDashT = 0f;
            Fx(SkillFxKind.DriveWarn, null, prDashFrom, planDir, 1, Flat(prDashTo - prDashFrom).magnitude, prDashTo);
        }

        /// <summary>Where a dash from <paramref name="from"/> stops: its length, or short of a wall or of a roof lower than
        /// the queen.</summary>
        Vector3 PrDashEnd(PawnRushSkillParams s, Vector3 from, Vector3 way)
        {
            float tall = s.prTall * PrGrowth(s), reach = s.prDash;
            Vector3 chest = from + Vector3.up * 1.2f;
            if (GdWall(chest, way, reach, out var wall)) reach = Mathf.Max(0f, wall.distance - 1f);
            Vector3 last = from;
            for (float d = 2f; d <= reach; d += 2f)
            {
                Vector3 p = from + way * d;
                var floor = DriveFloor(p, 3f, 6f);
                if (!floor.HasValue) break;
                if (!SkyOver(floor.Value, tall - 2.4f)) break;
                last = floor.Value;
            }
            return last;
        }

        void PrDashGo(PawnRushSkillParams s)
        {
            prWindup = false;
            prDashing = true;
            prDashT = 0f;
            driveUses--;
            ultFrom = bodies[0].position;
            Fx(SkillFxKind.DriveLine, null, prDashFrom, planDir, driveUses, s.prRadius * PrGrowth(s), prDashTo);
        }

        void PrDashStep(PawnRushSkillParams s, float dt)
        {
            prDashT += dt;
            grabImmune = Mathf.Max(grabImmune, 0.25f);
            float k = Mathf.Clamp01(prDashT / s.prDashTime);
            Vector3 hipsTo = prDashTo + Vector3.up * standHeight, at = bodies[0].position;
            Vector3 next = Vector3.Lerp(ultFrom, hipsTo, Mathf.Clamp01((prDashT + dt) / s.prDashTime));
            float radius = s.prRadius * PrGrowth(s);
            foreach (var e in All)
            {
                if (!DriveFoe(e)) continue;
                Vector3 p = e.bodies[0].position;
                if (DistanceToSegmentFlat(p, at, next) > radius || Mathf.Abs(p.y - at.y) > 3f) continue;
                driveHit.Add(e);
                prDashHits++;
                Vector3 side = Flat(p - at);
                side = side.sqrMagnitude > 0.04f ? side.normalized : planDir;
                Vector3 away = FlatDir(planDir + side * 0.6f);
                var to = DriveFloor(e.FeetPoint + away * s.prDashThrow, 4f, 14f) ?? e.FeetPoint;
                Fx(SkillFxKind.UltStrike, e, p, away, driveHit.Count, 1f);
                e.UltThrow(to, 0.75f, 5f, s.prDashKnock, this);
                e.LastSkillHit = $"승격: 퀸의 돌진에 {s.prDashThrow:0} m 날림";
                FeelHit(e, p, planDir, FeelHard, driveHit.Count, true);
            }
            if (k >= 1f)
            {
                prDashing = false;
                prGap = s.prDashGap;
                EndFlight(planDir * 4f);
                KeepSkillLandingSafe();
                return;
            }
            Fly(next, (next - at) / Mathf.Max(1e-4f, dt), 0f, default, 22f);
        }

        static float DistanceToSegmentFlat(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = Flat(b - a), ap = Flat(p - a);
            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            return (ap - ab * t).magnitude;
        }

        void PrBackToPawn()
        {
            if (prStage >= PrLeap && !prWasQueen && piece == PieceKind.Queen) SetPiece(prFrom);
            prStage = PrRun;
        }

        void ClearPromotionRun()
        {
            if (activeSkill != PawnRushSkillIds.PromotionRun) return;
            if (prDashing || prStage == PrLeap) EndFlight();
            prDashing = prWindup = false;
            PrBackToPawn();
        }
    }
}
