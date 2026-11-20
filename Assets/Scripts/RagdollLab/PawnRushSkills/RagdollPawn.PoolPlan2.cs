using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The skill pool's plan 2 (a skill that grows into another; JY-skill-lab, 2026-11-20; the user: "2안 남은 셋"): the three
    /// its kits need that the other plans did not make. Each as its pool row writes it, with its 검토 column's changes; no
    /// cooldown given back for a hit (the user ruled it out).
    ///
    ///   R-기5 뒷줄 한 수   the side or back key with the skill key: the rook slides 3.5 m straight left, right or back (as
    ///                     on its back rank, never forward) and stops dead on the first thing it meets - an enemy is shoved
    ///                     3 m on and the rook stands where it is. No edge stops it (the pool's funny fall into the water).
    ///   R-중1 직선 돌파    the key takes the stance, the left click held draws the line on the floor from 4 to 9 m, let go
    ///                     the rook charges straight along it: up to three enemies on the way go down to either side, each
    ///                     with a 0.06 s hit stop and a metre more to the charge; into a wall the rook staggers; off a cliff
    ///                     it falls. The skill is over the moment the charge is, so a dive can follow.
    ///   K-중7 킹 런        the key steps, and each press in time steps again: five steps in 2.5 s, each its own way, 2.2 m
    ///                     on the beat, 1.8 near it, 1.4 off it. Each foot down sheds what holds the king (hands, a tie, a
    ///                     pin, heavy feet); a step out of a check's square earns one more step. A breath of 0.5 s after.
    ///
    /// Prototypes, lines and rings by PawnRushPlanMarks. Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        // ---------------------------------------------------------------- R-기5: the back-rank move

        void UpdateBackRank(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    // Left, right or back of the way the camera looks, whichever the move keys are nearest; never forward.
                    Vector3 aim = AimFlat(), right = Vector3.Cross(Vector3.up, aim), want = Flat(skillMoveRaw);
                    float side = Vector3.Dot(want, right), back = -Vector3.Dot(want, aim);
                    if (want.sqrMagnitude < 0.04f || (Mathf.Abs(side) < 0.3f && back < 0.3f))
                    {
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, aim, (int)PieceKind.Rook);
                        Log("뒷줄 한 수: 옆이나 뒤 키를 누른 채 (앞으로는 안 됨)");
                        break;
                    }
                    Vector3 dir = Mathf.Abs(side) >= back ? right * Mathf.Sign(side) : -aim;
                    SkillUses++;
                    skillDir = dir;
                    planDir = dir;
                    planNote = 0;
                    planPoint = FeetPoint + dir * s.rankSlide;
                    skillHitSet.Clear();
                    Kick(dir, s.rankSlide, s.rankSlideTime, false);
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = "뒷줄 한 수";
                    break;
                }
                case PlanPhase.Active:
                    planTime += dt;
                    if (RankStop(s) || kickLeft <= 0f)
                    {
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.rankRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.rankCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The slide this step meets something - a wall or pillar, a piece: it stops dead there; an enemy is shoved
        /// rankShove on. True if it stopped.</summary>
        bool RankStop(PawnRushSkillParams s)
        {
            Vector3 dir = FlatDir(skillDir);
            if (WallAhead(kickSpeed * Time.fixedDeltaTime + 0.25f, out _))
            {
                kickLeft = 0f;
                HaltDash();
                Log("뒷줄 한 수: 벽에서 딱 멈춤");
                return true;
            }
            Vector3 at = bodies[(int)BodyId.Chest].position + dir * 0.35f;
            int n = Physics.OverlapSphereNonAlloc(at, 0.45f, skillOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!ColliderOwner.TryGetValue(skillOverlap[i], out var other) || other == null || other == this || other.State != PawnState.Active) continue;
                if (!IsEnemyOf(other) && !IsAllyOf(other)) continue;
                kickLeft = 0f;
                HaltDash();
                if (IsEnemyOf(other) && !other.SkillUntargetable)
                {
                    other.Kick(dir, s.rankShove, 0.25f, false);
                    other.LastSkillHit = $"뒷줄 한 수: {s.rankShove:0.#} m 밀림";
                    Vector3 contact = SkillContact(other);
                    Fx(SkillFxKind.RookHit, other, contact, dir, 1);
                    FeelHit(other, contact, dir, FeelWeak);
                    Log($"뒷줄 한 수 → {other.DisplayName}: {s.rankShove:0.#} m 밀어냄, 룩은 그 자리에 섬");
                }
                else Log($"뒷줄 한 수: {other.DisplayName}에 닿아 멈춤");
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- R-중1: the straight charge down an open file

        int fileHits;

        /// <summary>How far the charge's line reaches now (any PC): fileNear in the stance, out to fileFar over fileCharge of
        /// drawing.</summary>
        public float FileReach
        {
            get
            {
                var s = PawnRushSkills;
                if (s == null) return 0f;
                return planPhase == PlanPhase.Windup ? Mathf.Lerp(s.fileNear, s.fileFar, Mathf.Clamp01(planTime / Mathf.Max(0.05f, s.fileCharge))) : s.fileNear;
            }
        }

        void UpdateFileCharge(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    planPhase = PlanPhase.Aim;
                    planTime = 0f;
                    planNote = 0;
                    skillGrabLatch = skillGrabRaw;
                    planDir = AimFlat();
                    planPoint = FeetPoint + planDir * s.fileNear;
                    SkillDetail = "직선 돌파: 자세 (좌클릭 누르고 모으기 · 떼면 출발 · 우클릭/키 취소)";
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    planDir = AimFlat();
                    facing = planDir;
                    planPoint = FeetPoint + planDir * s.fileNear;
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        Log("직선 돌파: 자세를 풂");
                        break;
                    }
                    if (!confirm) break;
                    SkillUses++;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    SkillDetail = "직선 돌파: 줄을 늘리는 중 (떼면 출발)";
                    if (!drawHeld) BeginFileCharge(s);
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    planDir = AimFlat();
                    facing = planDir;
                    planPoint = FeetPoint + planDir * FileReach;
                    if (cancel || press)
                    {
                        EndPlanSkill();
                        Log("직선 돌파: 모으다 취소");
                        break;
                    }
                    if (!drawHeld || planTime > s.fileHold) BeginFileCharge(s);
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    if (WallAhead(dashBase * dt + 0.25f, out _))
                    {
                        // Into a wall: the rook staggers.
                        HaltDash();
                        Stagger(s.fileWallStagger);
                        FeelMiss(FeetPoint, skillDir);
                        EndPlanSkill();
                        StartCooldown(s.fileCooldown);
                        Log("직선 돌파: 벽에 박아 휘청");
                        break;
                    }
                    DriveDash(dt);
                    FileHits(s);
                    dashLeft -= dt;
                    if (dashLeft > 0f) break;
                    // Over the moment the charge is, so a dive can follow.
                    EndDash();
                    EndPlanSkill();
                    StartCooldown(s.fileCooldown);
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void BeginFileCharge(PawnRushSkillParams s)
        {
            float reach = FileReach;
            skillDir = AimFlat();
            planDir = skillDir;
            planPoint = FeetPoint + skillDir * reach;
            skillHitSet.Clear();
            fileHits = 0;
            StartDash(s.fileSpeed, reach / Mathf.Max(1f, s.fileSpeed));
            planPhase = PlanPhase.Active;
            planTime = 0f;
            SkillDetail = "직선 돌파";
            Log($"직선 돌파: {reach:0.#} m");
        }

        /// <summary>The charge's front this step: enemies on the way (fileMaxHits at most) go down to whichever side of the line
        /// they stood, each holding a moment and lengthening the charge by fileExtra.</summary>
        void FileHits(PawnRushSkillParams s)
        {
            if (fileHits >= s.fileMaxHits) return;
            Vector3 dir = FlatDir(skillDir), right = Vector3.Cross(Vector3.up, dir);
            Vector3 at = bodies[(int)BodyId.Chest].position + dir * 0.4f;
            int n = Physics.OverlapSphereNonAlloc(at, s.fileRadius, skillOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n && fileHits < s.fileMaxHits; i++)
            {
                if (!ColliderOwner.TryGetValue(skillOverlap[i], out var other) || other == null || other == this || skillHitSet.Contains(other)) continue;
                if (!IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable) continue;
                skillHitSet.Add(other);
                fileHits++;
                float side = Vector3.Dot(other.bodies[0].position - bodies[0].position, right) >= 0f ? 1f : -1f;
                Vector3 push = right * side * s.fileSideways + dir * s.fileForward + Vector3.up * s.fileLift;
                string result = SkillHit(other, push, true, "직선 돌파");
                Vector3 contact = SkillContact(other);
                Fx(SkillFxKind.RookHit, other, contact, dir, fileHits);
                FeelHit(other, contact, dir, FeelHard, 1, true, s.fileStop);
                dashLeft += s.fileExtra / Mathf.Max(1f, dashBase);
                Log($"직선 돌파 → {other.DisplayName}: {result} ({fileHits}번째, 1 m 더)");
            }
        }

        // ---------------------------------------------------------------- K-중7: the king's run

        int runStepsLeft;
        float runStartAt, runStepAt = -10f, runQueuedAt;
        bool runQueued;

        void UpdateKingRun(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    runStepsLeft = Mathf.Max(1, s.runSteps);
                    runStartAt = Time.time;
                    runQueued = false;
                    planNote = 0;
                    BeginRunStep(s, s.runStep);
                    break;
                case PlanPhase.Active:
                    // A step under way; a press now goes as the next step (off the beat if it is early).
                    planTime += dt;
                    if (press)
                    {
                        runQueued = true;
                        runQueuedAt = Time.time;
                    }
                    if (WallAhead(dashBase * dt + 0.22f, out _))
                    {
                        dashLeft = 0f;
                        HaltDash();
                    }
                    else
                    {
                        DriveDash(dt);
                        dashLeft -= dt;
                    }
                    if (dashLeft > 0f) break;
                    EndDash();
                    RunFootDown(s);
                    if (runStepsLeft <= 0 || Time.time - runStartAt >= s.runTime)
                    {
                        EndRun();
                        break;
                    }
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    if (runQueued)
                    {
                        runQueued = false;
                        NextRunStep(s, runQueuedAt);
                    }
                    break;
                case PlanPhase.Windup:
                    // Between steps: the key steps again, in time with the beat or not.
                    planTime += dt;
                    if (press)
                    {
                        NextRunStep(s, Time.time);
                        break;
                    }
                    if (Time.time - runStartAt >= s.runTime) EndRun();
                    break;
                case PlanPhase.Recovery:
                    // The breath after.
                    planTime += dt;
                    if (planTime >= s.runBreath)
                    {
                        EndPlanSkill();
                        StartCooldown(s.runCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The next step, its length by how near the press came to the beat (runBeat after the last step began).</summary>
        void NextRunStep(PawnRushSkillParams s, float pressedAt)
        {
            float off = Mathf.Abs(pressedAt - runStepAt - s.runBeat);
            BeginRunStep(s, off <= s.runBeatTight ? s.runLong : off <= s.runBeatLoose ? s.runStep : s.runShort);
        }

        void BeginRunStep(PawnRushSkillParams s, float length)
        {
            // A step taken while a check is on the king earns one more (the pool: out of a warned square, a step more).
            if (CheckedBy() != null || SeriesCheckedBy() != null) runStepsLeft++;
            runStepsLeft--;
            runStepAt = Time.time;
            skillDir = MoveOrAim();
            planDir = skillDir;
            planPoint = FeetPoint + skillDir * length;
            skillHitSet.Clear();
            StartDash(s.runSpeed, length / Mathf.Max(1f, s.runSpeed));
            planPhase = PlanPhase.Active;
            planTime = 0f;
            planNote = (byte)Mathf.Clamp(planNote + 1, 0, 15);   // steps taken
            Fx(SkillFxKind.PawnStep, null, FeetPoint, skillDir, planNote);   // (the pawn's step sound, rising: 탁-탁-탁)
            SkillDetail = $"킹 런 {planNote}걸음 ({length:0.0} m)";
        }

        /// <summary>A foot down: it sheds what holds the king - hands on him, a tie, a pin, heavy feet.</summary>
        void RunFootDown(PawnRushSkillParams s)
        {
            if (BeingHeld)
            {
                ReleaseHolders(s.stepShakeOff);
                heldTimer = struggleTimer = escapeProgress = 0f;
                holder = null;
                heldCollider = null;
                Log($"{DisplayName}: 킹 런 — 잡은 손을 떨쳐 냄");
            }
            bindLeft = 0f;
            slowLeft = 0f;
            if (pinLeft > 0f) EndPin();
        }

        void EndRun()
        {
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            SkillDetail = "킹 런: 숨 고르기";
            Log($"{DisplayName}: 킹 런 {planNote}걸음");
        }

        /// <summary>Before the run (PoolKeys): the back-rank slide and the king's run move the piece themselves (the move
        /// keys only say which way); the charge's stance is still.</summary>
        void Plan2Keys()
        {
            if (planPhase == PlanPhase.None) return;
            bool still = activeSkill == PawnRushSkillIds.BackRank || activeSkill == PawnRushSkillIds.KingRun
                         || (activeSkill == PawnRushSkillIds.FileCharge && (planPhase == PlanPhase.Aim || planPhase == PlanPhase.Windup));
            if (!still) return;
            input.move = Vector3.zero;
            input.jump = false;
            input.sprint = false;
        }
    }
}
