using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The skill pool's plan 3 (basic, mid, ultimate a piece; Docs/Skills/PAWN_RUSH_SKILL_POOL.md), its basic row's new
    /// skills (JY-skill-lab, 2026-11-20; the user, after plan 1: "다음 바로 진행해줘"). Each as its pool row writes it, the
    /// 검토 column's changes included; no cooldown given back for a hit (the user ruled it out).
    ///
    ///   B-기6 옆구리 사격        running on, the key fires a 12 m beam the way the camera looks (0.15 s): the first enemy
    ///                           on it is pushed 2 m along the beam, the bishop 1 m back (no edge stops either). Shot from
    ///                           behind, it only helps the enemy on ("블런더"); a dash hit from the side is broken off.
    ///   R-기2 넘어오는 캐슬링     the key crouches 0.15 s and the rook flies low over the nearest teammate ahead (6 m) to
    ///                           land 1.5 m beyond it: the enemies within 2 m are shoved, the teammate behind is guarded
    ///                           1.5 s (a knockdown becomes a stagger). The key as it lands shoves out to 3 m. An enemy
    ///                           in the way stops it on that enemy's back. Not while in check.
    ///   Q-기2 스콜라 찌르기       the key (with a side key) slides the queen 2.5 m out at a slant; the key again within
    ///                           0.6 s thrusts 5 m the way the camera looks. An enemy struck in the back or side is pushed
    ///                           3 m on and staggers 0.5 s - floored if it was standing with no teammate within 4 m; one
    ///                           that faces her is only pushed, and she bounces off and staggers 0.6 s.
    ///
    /// The pawn's (P-기1), the knight's (N-기1) and the king's (K-기1) basics are RagdollPawn.PoolSkills.cs's and
    /// RagdollPawn.KitSkills.cs's. Prototypes, lines and rings by PawnRushPlanMarks. Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        // ---------------------------------------------------------------- B-기6: the flank shot

        const byte FlankBlunderBit = 1;

        /// <summary>The flank shot just pushed an enemy on the way it was running (any PC).</summary>
        public bool FlankBlunder => PlanSkill == PawnRushSkillIds.FlankShot && planPhase == PlanPhase.Recovery && (planNote & FlankBlunderBit) != 0;

        void UpdateFlankShot(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planNote = 0;
                    // The way the camera looks at the press (an enemy close to it is aimed at); the bishop runs on.
                    planDir = LineAim(s.flankRange, s.flankAssist, s.flankTall);
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    SkillDetail = "옆구리 사격";
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    planPoint = LassoHands + planDir * s.flankRange;
                    if (planTime >= s.flankWindup) FireFlank(s);
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.flankShow)
                    {
                        EndPlanSkill();
                        StartCooldown(s.flankCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void FireFlank(PawnRushSkillParams s)
        {
            Vector3 from = LassoHands, dir = planDir.sqrMagnitude > 0.5f ? planDir.normalized : FlatDir(facing);
            float reach = s.flankRange;
            if (SolidRay(from, dir, reach, out var wall)) reach = wall.distance;
            float along = reach;
            var first = FirstOnLine(from, dir, ref along, s.flankRadius, true);
            planTarget = first;
            planPoint = from + dir * along;
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            Vector3 push = FlatDir(dir);
            Kick(-push, s.flankRecoil, 0.2f, false);   // the shot's kick: over an edge it goes (the pool: into the water behind)
            if (first == null)
            {
                FeelMiss(planPoint, dir);
                Log($"옆구리 사격: 아무도 안 맞음 ({reach:0.#} m)");
                return;
            }
            if (!IsEnemyOf(first))
            {
                Fx(SkillFxKind.SkillFail, null, first.LassoBelly, dir, (int)PieceKind.Bishop);
                Log($"옆구리 사격: 팀원 {first.DisplayName}에게 막힘");
                return;
            }
            // Shot from behind along its run, it is only pushed on its way: a blunder. A dash hit is broken off.
            Vector3 run = Flat(first.bodies[0].linearVelocity);
            bool helped = run.magnitude > s.flankBlunderSpeed && Vector3.Angle(run, push) < s.flankBlunderAngle;
            bool broke = first.dashing;
            if (broke) first.dashLeft = 0f;
            first.Kick(push, s.flankPush, s.flankPushTime, false);
            first.LastSkillHit = helped ? "옆구리 사격: 등 뒤에서 맞아 앞으로 밀림" : $"옆구리 사격: {s.flankPush:0.#} m 밀림" + (broke ? " (돌진 끊김)" : "");
            Fx(SkillFxKind.PawnHit, first, first.LassoBelly, push, 0);
            FeelHit(first, first.LassoBelly, push, FeelWeak);
            if (helped) planNote |= FlankBlunderBit;
            Log($"옆구리 사격 → {first.DisplayName}: {along:0.#} m, {s.flankPush:0.#} m 밀림" + (broke ? ", 돌진 끊김" : "") + (helped ? " — 블런더 (앞으로 밀어 줌)" : ""));
        }

        // ---------------------------------------------------------------- R-기2: castling over a teammate

        // The flight: from, to (the hips), its time so far and in all; the floor it left; when the key was last pressed in
        // the air; whether the wide shove has been given.
        Vector3 overFrom, overTo;
        float overT, overTime, overPressAt = -10f;
        bool overWide;
        static readonly float[] OverTries = { 1f, 0.66f, 0.4f };

        void UpdateCastleOver(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    // As in chess, no castling out of check.
                    bool checkedNow = CheckedBy() != null;
                    var mate = checkedNow ? null : OverMate(s);
                    Vector3? land = mate != null ? OverLanding(s, mate) : null;
                    if (land == null)
                    {
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Rook);
                        Log(checkedNow ? "넘어오는 캐슬링: 체크 중에는 못 함" : mate == null ? $"넘어오는 캐슬링: 앞쪽 {s.overRange:0.#} m 안에 팀원이 없음" : "넘어오는 캐슬링: 팀원 너머에 내려설 곳이 없음");
                        break;
                    }
                    SkillUses++;
                    planNote = 0;
                    planTarget = mate;
                    planDir = FlatDir(mate.bodies[0].position - bodies[0].position);
                    planPoint = land.Value;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    overPressAt = -10f;
                    overWide = false;
                    facing = planDir;
                    SkillDetail = $"넘어오는 캐슬링 → {mate.DisplayName} (내려서는 순간 F = 더 넓게)";
                    break;
                }
                case PlanPhase.Windup:
                    planTime += dt;
                    if (planTime >= s.overCrouch) BeginOverFlight(s);
                    break;
                case PlanPhase.Active:
                    if (press) overPressAt = Time.time;
                    StepOverFlight(s, dt);
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    // The key just after landing: the shove reaches farther (the ones already shoved are not again).
                    if (press && !overWide && (planNote & OverLandedBit) != 0 && planTime <= s.overPressLate) OverShove(s, true);
                    if (planTime >= s.overRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.overCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        const byte OverLandedBit = 1, OverBlockedBit = 2;

        /// <summary>The nearest standing teammate ahead (within overAngle of the aim) and within overRange.</summary>
        RagdollPawn OverMate(PawnRushSkillParams s)
        {
            Vector3 aim = AimFlat(), me = bodies[0].position;
            RagdollPawn best = null;
            float bestAcross = s.overRange;
            foreach (var p in All)
            {
                if (p == null || p == this || !IsAllyOf(p) || p.State != PawnState.Active || p.chainCarry || p.kingCarry || p.castleCarry) continue;
                Vector3 d = p.bodies[0].position - me;
                float across = Flat(d).magnitude;
                if (across < 0.8f || across > bestAcross || Mathf.Abs(d.y) > 1.5f || Vector3.Angle(aim, Flat(d)) > s.overAngle) continue;
                best = p;
                bestAcross = across;
            }
            return best;
        }

        /// <summary>Where the rook comes down: overBeyond past the teammate along the way to it, on a free floor near the
        /// teammate's height (nearer if there is none that far); null if there is nowhere.</summary>
        Vector3? OverLanding(PawnRushSkillParams s, RagdollPawn mate)
        {
            Vector3 dir = FlatDir(mate.bodies[0].position - bodies[0].position);
            Vector3 feet = mate.FeetPoint;
            foreach (float k in OverTries)
            {
                Vector3 at = feet + dir * (s.overBeyond * k);
                float? y = KingFloorAt(at, feet.y);
                if (y == null || Mathf.Abs(y.Value - feet.y) > 1.2f) continue;
                at.y = y.Value;
                if (KingSpotFree(at)) return at;
            }
            return null;
        }

        void BeginOverFlight(PawnRushSkillParams s)
        {
            overFrom = bodies[0].position;
            overTo = planPoint + Vector3.up * standHeight;
            overTime = Mathf.Max(0.25f, Flat(overTo - overFrom).magnitude / Mathf.Max(1f, s.overSpeed));
            overT = 0f;
            skillHitSet.Clear();
            planPhase = PlanPhase.Active;
            planTime = 0f;
            Fx(SkillFxKind.KnightLeap, null, FeetPoint, planDir, 1);   // (the knight's take-off, borrowed)
        }

        /// <summary>Low and straight: the line from where it rose to where it lands, lifted by an arc overArc high in the middle
        /// (over the teammate's head).</summary>
        Vector3 OverPath(float t, PawnRushSkillParams s)
        {
            float u = Mathf.Clamp01(t / overTime);
            Vector3 p = Vector3.Lerp(overFrom, overTo, u);
            p.y += 4f * s.overArc * u * (1f - u);
            return p;
        }

        void StepOverFlight(PawnRushSkillParams s, float dt)
        {
            overT += dt;
            planTime = overT;
            if (overT >= overTime)
            {
                LandOver(s, null, true);
                return;
            }
            Vector3 at = bodies[0].position, next = OverPath(overT + dt, s);
            Vector3 step = next - at;
            var blocker = OverBlocker(at, step);
            if (blocker != null || (step.sqrMagnitude > 1e-6f && ChainBlocked(at, step)))
            {
                LandOver(s, blocker, false);
                return;
            }
            facing = planDir;
            Fly(next, step / Mathf.Max(1e-4f, dt), 0f, default, 18f);
        }

        /// <summary>An enemy right in the way of the flight's next step (the rook cannot leap an enemy).</summary>
        RagdollPawn OverBlocker(Vector3 from, Vector3 step)
        {
            float d = step.magnitude;
            if (d < 1e-4f) return null;
            int n = Physics.SphereCastNonAlloc(from, 0.25f, step / d, hits, d + 0.15f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || ownSet.Contains(c) || !ColliderOwner.TryGetValue(c, out var other)) continue;
                if (other != null && other != this && IsEnemyOf(other) && other.State == PawnState.Active) return other;
            }
            return null;
        }

        /// <summary>Down (<paramref name="arrived"/>) beyond the teammate: the teammate behind is guarded and the enemies round
        /// are shoved (wide if the key met the landing). Stopped on the way: it drops there - on an enemy's back, the pool's
        /// funny miss.</summary>
        void LandOver(PawnRushSkillParams s, RagdollPawn blocker, bool arrived)
        {
            EndFlight(arrived ? Vector3.zero : bodies[0].linearVelocity * 0.3f);
            KeepSkillLandingSafe();
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            if (!arrived)
            {
                planNote |= OverBlockedBit;
                FeelMiss(FeetPoint, planDir);
                Log(blocker != null ? $"넘어오는 캐슬링: {blocker.DisplayName}의 등에 박고 멈춤" : "넘어오는 캐슬링: 장애물에 걸려 멈춤");
                return;
            }
            planNote |= OverLandedBit;
            var mate = planTarget;
            if (mate != null && mate.State == PawnState.Active && Flat(mate.bodies[0].position - bodies[0].position).magnitude < s.overBeyond + 2f)
            {
                mate.getUpGuardLeft = Mathf.Max(mate.getUpGuardLeft, s.overGuard);
                mate.LastSkillHit = $"넘어오는 캐슬링: 룩 뒤 — {s.overGuard:0.#}초 동안 넘어지지 않음";
            }
            Fx(SkillFxKind.RookSlam, null, FeetPoint, planDir, 0);
            OverShove(s, Time.time - overPressAt <= s.overPressEarly);
        }

        void OverShove(PawnRushSkillParams s, bool wide)
        {
            if (wide) overWide = true;
            float radius = wide ? s.overBigRadius : s.overRadius, shove = wide ? s.overShove * 1.5f : s.overShove;
            Vector3 c = FeetPoint;
            int n = 0;
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable || skillHitSet.Contains(other)) continue;
                Vector3 d = Flat(other.bodies[0].position - c);
                if (d.magnitude > radius || Mathf.Abs(other.FeetPoint.y - c.y) > 1.2f) continue;
                skillHitSet.Add(other);
                Vector3 away = d.sqrMagnitude > 1e-4f ? d.normalized : planDir;
                SkillHit(other, away * shove + Vector3.up * 0.8f, false, "넘어오는 캐슬링 (내려섬)");
                FeelHit(other, other.bodies[0].position, away, FeelWeak);
                n++;
            }
            Log($"넘어오는 캐슬링: 쿵 — 둘레 {radius:0.#} m의 적 {n}명 밀림" + (wide ? " (내려서는 순간 키)" : ""));
        }

        // ---------------------------------------------------------------- Q-기2: the scholar's thrust

        const byte ScholarBlockedBit = 1;
        bool scholarAgain;

        /// <summary>The thrust met an enemy face to face and the queen bounced off (any PC).</summary>
        public bool ScholarBlocked => PlanSkill == PawnRushSkillIds.ScholarThrust && planPhase == PlanPhase.Recovery && (planNote & ScholarBlockedBit) != 0;

        void UpdateScholarThrust(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planNote = 0;
                    scholarAgain = false;
                    // Out to the side the side key says (no side key: the right), at a slant forward.
                    Vector3 aim = AimFlat(), right = Vector3.Cross(Vector3.up, aim);
                    float sign = Vector3.Dot(Flat(skillMoveRaw), right) < -0.1f ? -1f : 1f;
                    skillDir = Quaternion.AngleAxis(sign * s.scholarSideAngle, Vector3.up) * aim;
                    StartDash(s.scholarSide / Mathf.Max(0.05f, s.scholarSideTime), s.scholarSideTime);
                    planDir = aim;
                    planPoint = bodies[0].position + aim * s.scholarReach;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    SkillDetail = "스콜라: 옆걸음 (0.6초 안에 F = 찌르기)";
                    break;
                }
                case PlanPhase.Windup:
                    // The slide out (its 0.25 s is the warning the others see).
                    planTime += dt;
                    if (press || confirm) scholarAgain = true;
                    planDir = AimFlat();
                    planPoint = bodies[0].position + planDir * s.scholarReach;
                    if (WallAhead(dashBase * dt + 0.25f, out _)) dashLeft = 0f;
                    else
                    {
                        DriveDash(dt);
                        dashLeft -= dt;
                    }
                    if (dashLeft > 0f) break;
                    EndDash();
                    if (scholarAgain) BeginScholarThrust(s);
                    else planPhase = PlanPhase.Aim;   // waiting for the second key (the clock runs on from the first)
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    planDir = AimFlat();
                    planPoint = bodies[0].position + planDir * s.scholarReach;
                    if (press || confirm)
                    {
                        BeginScholarThrust(s);
                        break;
                    }
                    if (cancel || planTime > s.scholarWindow)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        StartCooldown(s.scholarStepCooldown);
                        Log("스콜라 찌르기: 옆걸음만");
                    }
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    if (WallAhead(dashBase * dt + 0.25f, out _)) dashLeft = 0f;
                    else
                    {
                        DriveDash(dt);
                        if (ScholarHits(s)) break;
                        dashLeft -= dt;
                    }
                    if (dashLeft > 0f) break;
                    EndDash();
                    FeelMiss(FeetPoint, skillDir);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    Log("스콜라 찌르기: 아무도 안 찔림");
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.scholarRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.scholarCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void BeginScholarThrust(PawnRushSkillParams s)
        {
            skillDir = AimFlat();
            planDir = skillDir;
            skillHitSet.Clear();
            StartDash(s.scholarSpeed, s.scholarReach / Mathf.Max(1f, s.scholarSpeed));
            planPhase = PlanPhase.Active;
            planTime = 0f;
            SkillDetail = "스콜라: 찌르기";
        }

        /// <summary>The thrust's sceptre this step: the first enemy at its tip takes it, and the thrust stops there.</summary>
        bool ScholarHits(PawnRushSkillParams s)
        {
            Vector3 dir = FlatDir(skillDir);
            Vector3 at = bodies[(int)BodyId.Chest].position + dir * 0.35f;
            int n = Physics.OverlapSphereNonAlloc(at, s.scholarRadius, skillOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!ColliderOwner.TryGetValue(skillOverlap[i], out var other) || other == null || other == this || skillHitSet.Contains(other)) continue;
                if (!IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable) continue;
                skillHitSet.Add(other);
                HaltDash();
                planPhase = PlanPhase.Recovery;
                planTime = 0f;
                ScholarStrike(s, other, dir);
                return true;
            }
            return false;
        }

        void ScholarStrike(PawnRushSkillParams s, RagdollPawn other, Vector3 dir)
        {
            Vector3 contact = SkillContact(other);
            Vector3 toQueen = Flat(bodies[0].position - other.bodies[0].position);
            bool facesHer = toQueen.sqrMagnitude > 1e-4f && Vector3.Angle(FlatDir(other.facing), toQueen) < s.scholarFacing;
            if (facesHer)
            {
                // Met by one who knows the defence: it is only pushed, and the queen bounces off and staggers ("??").
                SkillHit(other, dir * s.scholarShove, false, "스콜라 찌르기 (막음)");
                Kick(-dir, s.scholarBounce, 0.2f, true);
                Stagger(s.scholarBlockedStagger);
                planNote |= ScholarBlockedBit;
                Fx(SkillFxKind.PawnHit, other, contact, dir, 0);
                FeelMiss(contact, dir);
                Log($"스콜라 찌르기 → {other.DisplayName}: 마주 봐서 막힘 → 퀸이 튕겨 휘청 {s.scholarBlockedStagger:0.#}초");
                return;
            }
            bool standing = Flat(other.bodies[0].linearVelocity).magnitude < s.scholarStillSpeed;
            bool alone = !MateWithin(other, s.scholarMateGap);
            string result;
            if (standing && alone)
            {
                result = SkillHit(other, dir * s.scholarKnockPush + Vector3.up * s.scholarKnockLift, true, "스콜라 찌르기");
                Fx(SkillFxKind.QueenHit, other, contact, dir, 1);
                FeelHit(other, contact, dir, FeelHard);
            }
            else
            {
                other.Kick(dir, s.scholarPush, 0.3f, false);
                other.Stagger(s.scholarStagger);
                result = $"{s.scholarPush:0.#} m 밀림 + 휘청 {s.scholarStagger:0.#}초";
                other.LastSkillHit = $"스콜라 찌르기: {result}";
                Fx(SkillFxKind.PawnHit, other, contact, dir, 0);
                FeelHit(other, contact, dir, FeelWeak);
            }
            Log($"스콜라 찌르기 → {other.DisplayName}: 등 · 옆 → {result}" + (standing ? "" : " (달리던 중)") + (alone ? "" : " (팀원이 곁에)"));
        }

        /// <summary>A standing teammate of <paramref name="other"/> within <paramref name="gap"/> of it.</summary>
        bool MateWithin(RagdollPawn other, float gap)
        {
            foreach (var p in All)
                if (p != null && p != other && other.IsAllyOf(p) && p.State == PawnState.Active
                    && Flat(p.bodies[0].position - other.bodies[0].position).magnitude <= gap) return true;
            return false;
        }

        /// <summary>Before the run (PoolKeys): the castling over crouches and flies (no keys); the scholar's thrust after its
        /// slide waits without a dive.</summary>
        void PoolBasicKeys()
        {
            if (planPhase == PlanPhase.None) return;
            if (activeSkill == PawnRushSkillIds.CastleOver)
            {
                input.move = Vector3.zero;
                input.jump = false;
                input.sprint = false;
                input.shove = false;
                input.shoveHeld = false;
            }
        }
    }
}
