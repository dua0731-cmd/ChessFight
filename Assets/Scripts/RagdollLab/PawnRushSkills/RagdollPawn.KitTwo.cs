using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The kits' second version (JY-skill-lab, 2026-11-21; Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6, "기물별 한 벌 2판"): each
    /// piece's own mix of normal slots (F, E, and the queen's R) and an ultimate on Q. Most of its skills are the plans'
    /// skills with this version's changes, made where those skills are; here are what it adds to them and its new ones.
    ///
    ///   P-초 첫 두 걸음 + 앙파상   the steps (81) watch for an enemy that dashes past within 2 m on a skill: a ring at its
    ///                             feet for 0.4 s, and the key then lunges at its ankles (down 1.2 s) instead of stepping.
    ///   P-중 쫓아내기              a rook or a queen struck pops its value over its head, "+5", "+9" (PopOver).
    ///   N-초 가벼운 발굽            a head the knight stomps is light for 3 s: pushed and thrown twice as far.
    ///   B-초 옆구리 사격            the one hit walks on heavy feet (0.7) for 1.5 s.
    ///   R-중 사슬                  a piece pulled in is heavy for 2 s: it jumps lower; the wheel reels too.
    ///   K-초 한 칸 잡기             an enemy with a teammate of its at its side is defended: the king's step stops at it.
    ///                             Walking out of a check's square, a snipe's light: "체크 탈출" over his head.
    ///   Q-초 여왕의 활주 (new)      move keys + key: a glide along one of eight ways at 7 m/s, the key again stops her
    ///                             dead (7 m at most); a piece or a wall stops her, an enemy touched is pushed 1.5 m.
    ///   Q-상 여덟 줄 (new, R)       the staff planted: eight lines of 7 m, 0.5 s, the move keys turn the star 22.5 degrees
    ///                             at most; then the first enemy on each line flies 4 m along it and lies 2.5 s. Three or
    ///                             more and the hits hold 0.15 s.
    ///   R-상 성벽 돌진 (new)        aim, left click: a floor line 0.4 s, then a 9 m charge with a chest-high wall rising
    ///                             behind it for 5 s (PawnRushKitWall); the left click again stops it; enemies it runs into
    ///                             are pushed aside. An enemy rook's charge breaks a wall.
    ///
    /// Prototypes, lines and rings by PawnRushPlanMarks (PawnRushPlanMarks.KitTwo.cs). Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        // ---------------------------------------------------------------- what a piece is left with: light, heavy

        float lightLeft, heavyLeft;
        // A flank shot's heavy feet go at this (0: the fianchetto's own, fianSlow).
        float slowScaleNow;
        // The king's step began in a check's square (or a snipe's light).
        bool stepFromWarning;
        // The wheel's notches, reeling the rook's chain a moment each.
        float chainWheelLeft;

        /// <summary>The skill test bed's: this piece counts as on a skill's dash (the dummy it sends running past, for the
        /// two steps' en passant).</summary>
        public bool TestDashing { get; set; }

        /// <summary>Light (a knight stomped its head): pushes and throws take it twice as far.</summary>
        public bool Light => lightLeft > 0f;
        public float LightLeft => Mathf.Max(0f, lightLeft);
        /// <summary>Heavy (a rook's chain wound round it): it jumps lower.</summary>
        public bool ChainHeavy => heavyLeft > 0f;
        public float HeavyLeft => Mathf.Max(0f, heavyLeft);

        /// <summary>How much farther pushes take this piece now (RagdollPawn.PushScale, Kick).</summary>
        float KitPushMul => lightLeft > 0f && PawnRushSkills != null ? Mathf.Max(1f, PawnRushSkills.lightPush) : 1f;
        /// <summary>How high its jumps go now (RagdollPawn.Jump).</summary>
        float SkillJumpScale => heavyLeft > 0f && PawnRushSkills != null ? Mathf.Clamp(PawnRushSkills.heavyJump, 0.2f, 1f) : 1f;

        // The words over a head (SkillFxKind.KitPop's count; under 100 a value shown as "+N").
        public const int PopCheckEscape = 100, PopDefended = 101, PopLight = 102, PopHeavy = 103, PopCrown = 104, PopUltCut = 105;

        void MakeLight(RagdollPawn other, PawnRushSkillParams s)
        {
            if (other == null || s == null) return;
            other.lightLeft = Mathf.Max(other.lightLeft, s.lightSeconds);
            PopOver(other, PopLight);
        }

        void MakeHeavy(PawnRushSkillParams s)
        {
            if (s == null) return;
            heavyLeft = Mathf.Max(heavyLeft, s.heavySeconds);
            PopOver(this, PopHeavy);
        }

        /// <summary>Heavy feet for a while at <paramref name="scale"/> of the run's speed (no sprint).</summary>
        void SlowFeet(float seconds, float scale)
        {
            if (seconds <= 0f) return;
            slowLeft = Mathf.Max(slowLeft, seconds);
            slowScaleNow = scale;
        }

        /// <summary>A word or a value over <paramref name="over"/>'s head, on every PC (PawnRushPlanMarks draws it).</summary>
        void PopOver(RagdollPawn over, int code)
        {
            if (over == null) return;
            Fx(SkillFxKind.KitPop, over, over.bodies[(int)BodyId.Head].position + Vector3.up * 0.35f, Vector3.up, code);
        }

        /// <summary>Every step (UpdateKitStates): light and heavy run out (and go at once in the water or on a respawn).</summary>
        void UpdateKitTwoStates(PawnRushSkillParams s, float dt)
        {
            bool gone = Floating || skillGuardLeft > 0f;
            if (lightLeft > 0f) lightLeft = gone ? 0f : lightLeft - dt;
            if (heavyLeft > 0f) heavyLeft = gone ? 0f : heavyLeft - dt;
            if (chainWheelLeft > 0f) chainWheelLeft -= dt;
            UpdateGauge(s, dt);   // the ultimate gauge (RagdollPawn.UltGauge.cs)
        }

        void ClearKitTwoStates()
        {
            lightLeft = heavyLeft = 0f;
            slowScaleNow = 0f;
            chainWheelLeft = 0f;
            stepFromWarning = false;
        }

        /// <summary>The rook's chain, reeling in: a wheel notch reels it fast for a moment (as the left click held does).</summary>
        bool ChainWheelReel(PawnRushSkillParams s, float dt)
        {
            if (Mathf.Abs(lassoWheel) > 0.01f && s != null) chainWheelLeft = s.behindWheelHold;
            lassoWheel = 0f;
            return chainWheelLeft > 0f;
        }

        /// <summary>A piece with a standing teammate of its at its side (the king cannot take a defended piece).</summary>
        bool Defended(RagdollPawn other, PawnRushSkillParams s)
        {
            foreach (var mate in All)
            {
                if (mate == null || mate == other || mate == this || mate.team != other.team || mate.State != PawnState.Active) continue;
                Vector3 d = mate.bodies[0].position - other.bodies[0].position;
                if (Mathf.Abs(d.y) <= 1.2f && Flat(d).magnitude <= s.stepDefended) return true;
            }
            return false;
        }

        /// <summary>In a warning now: a queen's check square, or a bishop's snipe light at its feet.</summary>
        bool InWarning() => SeriesCheckedBy() != null || CheckedBy() != null || SnipedBy() != null;

        /// <summary>Before the run (PlanKeys): what these skills let the keys do in their phases.</summary>
        void KitTwoKeys()
        {
            if (planPhase == PlanPhase.None) return;
            switch (activeSkill)
            {
                case PawnRushSkillIds.Promote:
                case PawnRushSkillIds.KingMarch:
                    // The ultimates' warning (the kits' second version): the piece stands while everyone is told.
                    if (planPhase != PlanPhase.Windup) break;
                    input.move = Vector3.zero;
                    input.jump = false;
                    input.sprint = false;
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
                case PawnRushSkillIds.SeventhRank:
                    // The wall rising: the rook stands. Pushing it: the left click held gathers a blow (not a dive).
                    if (planPhase == PlanPhase.Windup)
                    {
                        input.move = Vector3.zero;
                        input.jump = false;
                        input.sprint = false;
                    }
                    rankHeld = input.shoveHeld;
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
                case PawnRushSkillIds.EightLines:
                    // Planting the staff: she stands; the move keys only turn the star (read from skillMoveRaw).
                    input.move = Vector3.zero;
                    input.jump = false;
                    input.sprint = false;
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
                case PawnRushSkillIds.QueenGlide:
                    // Gliding: the dress carries her; no jump, no dive.
                    if (planPhase == PlanPhase.Active || planPhase == PlanPhase.Recovery)
                    {
                        input.jump = false;
                        input.shove = false;
                        input.shoveHeld = false;
                    }
                    break;
                case PawnRushSkillIds.WallCharge:
                    if (planPhase == PlanPhase.Aim) break;   // (walking while it aims; the left click is the aim's)
                    if (planPhase == PlanPhase.Active && input.shove) wallCut = true;   // the left click again: stop here
                    input.move = Vector3.zero;
                    input.jump = false;
                    input.sprint = false;
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
            }
        }

        // ---------------------------------------------------------------- P-초: en passant on the two steps

        /// <summary>Every step the two steps sit in a slot (RunOldSkill): it watches for an enemy dashing past; with one just
        /// gone by and the steps not under way, the key lunges at its ankles instead of stepping. True if it took the key.</summary>
        bool StartStepsPassant(PawnRushSkillParams s, bool press, float dt)
        {
            TrackPasses(s, dt, true);
            if (!press || skillStage != SkillStage.None || PassTarget == null || !CanStartSkill()) return false;
            SkillUses++;
            planTarget = passTarget;
            passTarget = null;
            passLeft = 0f;
            planPhase = PlanPhase.Active;
            planTime = 0f;
            planNote = 0;
            skillHitSet.Clear();
            skillDir = FlatDir(planTarget.bodies[0].position - bodies[0].position);
            planDir = skillDir;
            StartDash(s.passLungeSpeed, s.passLunge / Mathf.Max(0.1f, s.passLungeSpeed));
            SkillDetail = $"앙파상 → {planTarget.DisplayName}";
            Fx(SkillFxKind.PawnStep, planTarget, FeetPoint, skillDir, 2);
            Log($"첫 두 걸음: 앙파상 — {planTarget.DisplayName}의 발목으로 뛰어듦");
            return true;
        }

        /// <summary>The en passant lunge (RunNewSkill, the steps' number): it bends onto the piece and trips it on reaching it.</summary>
        void UpdateStepsPassant(PawnRushSkillParams s, float dt)
        {
            if (planPhase != PlanPhase.Active)
            {
                EndPlanSkill();
                return;
            }
            planTime += dt;
            var t = planTarget;
            if (t != null && t.State == PawnState.Active && !t.SkillUntargetable && !skillHitSet.Contains(t))
            {
                Vector3 d = t.bodies[0].position - bodies[0].position;
                if (Flat(d).sqrMagnitude > 0.01f) skillDir = Flat(d).normalized;
                if (Flat(d).magnitude <= s.passReach && Mathf.Abs(d.y) <= 1.2f)
                {
                    skillHitSet.Add(t);
                    TripEnPassant(s, t, s.stepKnockHold);
                    FeelHit(t, SkillContact(t), skillDir, FeelHard);
                    dashLeft = 0f;
                }
            }
            if (dashLeft > 0f)
            {
                if (WallAhead(dashBase * dt + 0.25f, out _)) dashLeft = 0f;
                else
                {
                    DriveDash(dt);
                    dashLeft -= dt;
                }
            }
            if (dashLeft > 0f) return;
            HaltDash();   // it stands where the piece was
            EndPlanSkill();
            StartCooldown(s.pawnCooldown);
        }

        // ---------------------------------------------------------------- Q-초: the queen's glide

        /// <summary>The way the glide goes: the move keys' way snapped to one of eight around the camera's (the camera's own
        /// with no key).</summary>
        Vector3 GlideWay()
        {
            Vector3 look = AimFlat();
            Vector3 m = Flat(skillMoveRaw);
            if (m.sqrMagnitude < 0.04f) return look;
            float a = Mathf.Round(Vector3.SignedAngle(look, m.normalized, Vector3.up) / 45f) * 45f;
            return FlatDir(Quaternion.AngleAxis(a, Vector3.up) * look);
        }

        void UpdateQueenGlide(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    skillDir = GlideWay();
                    planDir = skillDir;
                    planPoint = FeetPoint;   // where she set off (the marks draw the train from it)
                    planTarget = null;
                    planNote = 0;
                    skillHitSet.Clear();
                    StartDash(s.glideSpeed, 1f);
                    dashLeft = s.glideMax / Mathf.Max(0.5f, dashBase);   // never farther than glideMax, however fast she ran
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = "여왕의 활주 (키 = 그 자리에 딱 섬)";
                    Fx(SkillFxKind.PawnStep, null, FeetPoint, skillDir, 1);   // (the pawn's step sound, borrowed)
                    Log($"{DisplayName}: 여왕의 활주 ({Vector3.SignedAngle(AimFlat(), skillDir, Vector3.up):0}°)");
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    bool stop = press;   // the key again: she stops dead where she is
                    if (!stop && WallAhead(dashBase * dt + 0.25f, out _)) stop = true;
                    if (!stop)
                    {
                        DriveDash(dt);
                        if (GlideTouch(s) != null) stop = true;
                        dashLeft -= dt;
                    }
                    if (!stop && dashLeft > 0f) break;
                    HaltDash();
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    // The stop: a short thud (stopping on the edge of a platform is the skill's own art).
                    FeelHit(null, FeetPoint, skillDir, FeelWeak, 1, false, 0.04f);
                    Log($"{DisplayName}: 여왕의 활주 — {Flat(FeetPoint - planPoint).magnitude:0.0} m에서 멈춤" + (press ? " (키)" : ""));
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.glideRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.glideCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The glide meets a piece this step: an enemy is pushed glideShove along it, a teammate only stops her.</summary>
        RagdollPawn GlideTouch(PawnRushSkillParams s)
        {
            Vector3 dir = FlatDir(skillDir);
            Vector3 at = bodies[(int)BodyId.Chest].position + dir * 0.3f;
            int n = Physics.OverlapSphereNonAlloc(at, 0.45f, skillOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!ColliderOwner.TryGetValue(skillOverlap[i], out var other) || other == null || other == this || other.State != PawnState.Active) continue;
                if (IsEnemyOf(other) && !other.SkillUntargetable)
                {
                    other.Kick(dir, s.glideShove, 0.25f, false);
                    other.LastSkillHit = $"여왕의 활주: {s.glideShove:0.#} m 밀림";
                    Vector3 contact = SkillContact(other);
                    Fx(SkillFxKind.PawnHit, other, contact, dir, 0);
                    FeelHit(other, contact, dir, FeelWeak);
                    Log($"여왕의 활주 → {other.DisplayName}: 닿아서 {s.glideShove:0.#} m 밀림, 퀸은 멈춤");
                }
                return other;
            }
            return null;
        }

        // ---------------------------------------------------------------- Q-상: the eight lines

        // The star's first line before it was turned, and how far it has been turned (degrees).
        Vector3 eightBase = Vector3.forward;
        float eightTurnNow;

        /// <summary>The way of the star's line <paramref name="k"/> (0..7), as turned now (any PC: planDir is the first).</summary>
        public Vector3 EightLine(int k) => Quaternion.AngleAxis(45f * k, Vector3.up) * FlatDir(planDir);

        void UpdateEightLines(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    eightBase = AimFlat();
                    eightTurnNow = 0f;
                    planDir = eightBase;
                    planPoint = FeetPoint;
                    planTarget = null;
                    planNote = 0;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    HaltDash();
                    SkillDetail = "여덟 줄 (이동 키 = 줄 돌리기)";
                    Fx(SkillFxKind.RookSlam, null, FeetPoint, eightBase, 0, 1f);   // the staff on the floor (the rook's slam, borrowed)
                    Log($"{DisplayName}: 여덟 줄 — 홀을 찍음");
                    break;
                case PlanPhase.Windup:
                {
                    planTime += dt;
                    // The move keys turn the whole star, left or right of the way she planted it, eightTurn at most.
                    Vector3 m = Flat(skillMoveRaw);
                    if (m.sqrMagnitude > 0.04f)
                    {
                        float side = Vector3.Dot(m.normalized, Vector3.Cross(Vector3.up, eightBase));
                        if (Mathf.Abs(side) > 0.3f)
                            eightTurnNow = Mathf.Clamp(eightTurnNow + Mathf.Sign(side) * s.eightTurnSpeed * dt, -s.eightTurn, s.eightTurn);
                    }
                    planDir = Quaternion.AngleAxis(eightTurnNow, Vector3.up) * eightBase;
                    if (planTime >= s.eightFuse) EightBlast(s);
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.eightRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.eightCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>All eight at once: the first enemy on each (short of a wall) flies along it and lies eightHold; a piece
        /// between the lines is untouched. Several hits hold the moment and stack their sounds.</summary>
        void EightBlast(PawnRushSkillParams s)
        {
            Vector3 from = FeetPoint + Vector3.up * 0.9f;
            int hits = 0;
            RagdollPawn firstHit = null;
            for (int k = 0; k < 8; k++)
            {
                Vector3 dir = EightLine(k);
                float reach = s.eightLength;
                if (SolidRay(from, dir, reach, out var wall)) reach = wall.distance;
                float along = reach;
                var first = FirstOnLine(from, dir, ref along, s.eightRadius, false);
                if (first == null || !IsEnemyOf(first)) continue;
                hits++;
                if (firstHit == null) firstHit = first;
                string result = SkillHit(first, dir * s.eightPush + Vector3.up * s.eightLift, true, "여덟 줄", false, s.eightHold);
                Fx(SkillFxKind.QueenHit, first, first.LassoBelly, dir, hits);
                Log($"여덟 줄 → {first.DisplayName}: {along:0.#} m → {result}");
            }
            planNote = (byte)hits;
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            if (hits == 0)
            {
                FeelMiss(FeetPoint, planDir);
                Log("여덟 줄: 아무도 안 맞음 (줄 사이)");
                return;
            }
            FeelHit(firstHit, from, planDir, FeelHard, hits, true, hits >= s.eightMany ? s.eightStop : 0f);
            if (hits >= s.eightMany) Log($"여덟 줄: {hits}명 — 탕탕탕!");
        }

        // ---------------------------------------------------------------- R-상: the wall charge

        // The wall rising behind the run (the host's), where it began, and the left click again.
        PawnRushKitWall wallUp;
        Vector3 wallFrom;
        bool wallCut;

        /// <summary>Where the charge would end from here (a wall ahead cuts it).</summary>
        Vector3 WallChargeEnd(PawnRushSkillParams s, Vector3 dir)
        {
            float reach = s.wallRun;
            if (SolidRay(bodies[(int)BodyId.Chest].position, dir, reach, out var wall)) reach = Mathf.Max(0.5f, wall.distance - 0.4f);
            return FeetPoint + dir * reach;
        }

        void UpdateWallCharge(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
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
                    planPoint = WallChargeEnd(s, planDir);
                    SkillDetail = "성벽 돌진: 겨눔 (좌클릭 출발 · 우클릭/키 취소)";
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    planDir = AimFlat();
                    planPoint = WallChargeEnd(s, planDir);
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        Log("성벽 돌진: 겨눔 취소");
                        break;
                    }
                    if (!confirm) break;
                    SkillUses++;
                    // The floor line, for everyone to see first.
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    facing = FlatDir(planDir);
                    HaltDash();
                    SkillDetail = "성벽 돌진: 바닥 줄";
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    facing = FlatDir(planDir);
                    planPoint = WallChargeEnd(s, FlatDir(planDir));
                    if (planTime < s.wallWarn) break;
                    BeginWallCharge(s);
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    // An enemy rook's wall ahead breaks (the rook's charge is what breaks one).
                    PawnRushKitWall.BreakAhead(this, bodies[(int)BodyId.Chest].position, FlatDir(skillDir), dashBase * dt + 0.6f);
                    bool stop = wallCut || confirm;
                    if (!stop && WallAhead(dashBase * dt + 0.25f, out _)) stop = true;
                    if (!stop)
                    {
                        DriveDash(dt);
                        WallChargeShove(s);
                        dashLeft -= dt;
                        GrowWall(s, false);
                    }
                    if (!stop && dashLeft > 0f) break;
                    HaltDash();
                    GrowWall(s, true);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    Log($"{DisplayName}: 성벽 돌진 — {Flat(FeetPoint - wallFrom).magnitude:0.0} m, 성벽 {s.wallSeconds:0}초" + (wallCut || confirm ? " (좌클릭으로 끊음)" : ""));
                    wallCut = false;
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.wallRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.wallCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void BeginWallCharge(PawnRushSkillParams s)
        {
            skillDir = FlatDir(planDir);
            wallFrom = FeetPoint;
            wallCut = false;
            skillHitSet.Clear();
            StartDash(s.wallSpeed, s.wallRun / Mathf.Max(1f, s.wallSpeed));
            dashBase = s.wallSpeed;   // (its own speed: the wall goes up as fast as it runs)
            planPoint = wallFrom;     // (charging: the wall's start, for the marks)
            planPhase = PlanPhase.Active;
            planTime = 0f;
            SkillDetail = "성벽 돌진 (좌클릭 = 여기서 끊기)";
            wallUp = PawnRushKitWall.Raise(PawnRushKitWall.NextId(), this, wallFrom, wallFrom + skillDir * 0.3f, s.wallHeight, s.wallThick, s.wallSeconds, s.wallRise, true);
            Fx(SkillFxKind.RookThrow, null, FeetPoint, skillDir, 0);   // (the chain's throw sound, borrowed: the start)
        }

        /// <summary>The wall follows the run a little behind the rook; at the end (<paramref name="done"/>) it is told to every
        /// PC and stands its time from now.</summary>
        void GrowWall(PawnRushSkillParams s, bool done)
        {
            if (wallUp == null) return;
            Vector3 end = FeetPoint - FlatDir(skillDir) * (done ? 0.45f : 0.7f);
            if (Vector3.Dot(end - wallFrom, skillDir) < 0.3f) end = wallFrom + FlatDir(skillDir) * 0.3f;
            wallUp.SetLine(wallFrom, end);
            if (!done) return;
            wallUp.Stand(s.wallSeconds);
            Fx(SkillFxKind.KitWall, null, wallFrom, FlatDir(skillDir), wallUp.Id, s.wallSeconds, end);
            wallUp = null;
        }

        /// <summary>The running body meets an enemy: pushed off to the side of the line it stands on.</summary>
        void WallChargeShove(PawnRushSkillParams s)
        {
            Vector3 dir = FlatDir(skillDir), right = Vector3.Cross(Vector3.up, dir);
            Vector3 at = bodies[(int)BodyId.Chest].position + dir * 0.35f;
            int n = Physics.OverlapSphereNonAlloc(at, 0.55f, skillOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!ColliderOwner.TryGetValue(skillOverlap[i], out var other) || other == null || other == this || skillHitSet.Contains(other)) continue;
                if (!IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable) continue;
                skillHitSet.Add(other);
                float side = Vector3.Dot(other.bodies[0].position - bodies[0].position, right) >= 0f ? 1f : -1f;
                other.Kick(right * side, s.wallShove, 0.25f, false);
                other.LastSkillHit = $"성벽 돌진: 옆으로 {s.wallShove:0.#} m 밀림";
                Vector3 contact = SkillContact(other);
                Fx(SkillFxKind.RookHit, other, contact, right * side, skillHitSet.Count);
                FeelHit(other, contact, right * side, FeelWeak);
                Log($"성벽 돌진 → {other.DisplayName}: 옆으로 밀림");
            }
        }

        /// <summary>The wall under way goes with the skill if it ends early (a knockdown): what stood stays its time.</summary>
        void ClearKitTwoSkill()
        {
            ClearSeventhRank();   // the 7th rank's wall goes down with it (RagdollPawn.KitTwoUlts.cs)
            if (wallUp != null)
            {
                var s = PawnRushSkills;
                if (s != null) GrowWall(s, true);
                wallUp = null;
            }
            wallCut = false;
        }

        /// <summary>This piece's own bodies do not touch <paramref name="c"/> (its own wall) - or touch it again.</summary>
        public void IgnoreBody(Collider c, bool ignore)
        {
            if (c == null) return;
            foreach (var own in ownSet)
                if (own != null) Physics.IgnoreCollision(c, own, ignore);
        }

        /// <summary>Host: a wall of this piece's broke (an enemy rook charged through it): every PC takes it down.</summary>
        public void TellWallBroken(PawnRushKitWall wall) =>
            Fx(SkillFxKind.KitWallBreak, null, wall.Middle, Vector3.up, wall.Id);

        // ---------------------------------------------------------------- over the network (RagdollPawn.SkillNet.cs)

        // Flags3 bits (SkillWireState): light, heavy.
        const byte NetLight = 1, NetHeavy = 2;

        bool KitTwoNetBusy => lightLeft > 0f || heavyLeft > 0f;

        void CaptureKitTwoNet(ref SkillWireState s)
        {
            if (lightLeft > 0f) s.Flags3 |= NetLight;
            if (heavyLeft > 0f) s.Flags3 |= NetHeavy;
        }

        void ApplyKitTwoNet(in SkillWireState s)
        {
            var k = PawnRushSkills;
            SeeState(ref lightLeft, (s.Flags3 & NetLight) != 0, k != null ? k.lightSeconds : 3f);
            SeeState(ref heavyLeft, (s.Flags3 & NetHeavy) != 0, k != null ? k.heavySeconds : 2f);
        }

        void ClearKitTwoNet() => lightLeft = heavyLeft = 0f;

        void AdvanceKitTwoView(float dt)
        {
            if (lightLeft > 0f) lightLeft = Mathf.Max(0.05f, lightLeft - dt);
            if (heavyLeft > 0f) heavyLeft = Mathf.Max(0.05f, heavyLeft - dt);
        }
    }
}
