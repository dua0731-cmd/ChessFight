using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The skill pool's plan 3, its ultimates and their gauge (JY-skill-lab, 2026-11-20; the user: "응, 궁극기"). Each as its
    /// pool row writes it with its 검토 column's changes; no gauge or cooldown given back for a hit (the user ruled rewards
    /// for hitting out).
    ///
    ///   게이지              an ultimate's cooldown is its gauge: it starts empty when the loadout is set and fills over ultGauge
    ///                      (3 minutes: once or twice a match, the time part of the pool's X-판5); used, it starts again.
    ///   P-궁1 승격          the key draws the last rank 3 m ahead and the pawn steps over it: as its foot crosses, a 0.15 s
    ///                      hit stop, the enemies within 3 m are shoved off and it is a queen for 15 s (the queen's row of
    ///                      skills), then what it was again.
    ///   N-궁1 스머더드 메이트 only with an enemy within 18 m that two or more of its teammates crowd (2.5 m): the knight
    ///                      rises high and falls on it, the move keys chasing it up to 3 m; the middle one is squashed 3 s,
    ///                      the ones round it fly; the left click just before it lands squashes a second longer and holds.
    ///                      Down on nothing, it goes in head first ("??").
    ///   B-궁1 긴 대각선      the key aims, the left click lays a line to the end of the section (60 m, through people, short
    ///                      of a wall); 1.5 s on, it lies 8 s and nails every enemy on it or stepping onto it 2.5 s (the chess
    ///                      review's way: the long diagonal is held, not pulled).
    ///   R-궁6 활발한 룩      a chariot for 10 s: straight on at 6 m/s, A or D a right angle each, S one stop of a second, the
    ///                      key again ends it; enemies met go down to the side, the rook does not; a wall staggers it and ends it.
    ///   Q-궁1 퀸 희생        the key aims, the left click throws the queen at the spot (12 m) in 1.2 s, the move keys moving
    ///                      it up to 3 m; landing, every enemy within 9 m goes down and flies ("탁월한 수 !!" for four or
    ///                      more, "블런더 ??" for none), and the queen, taken, goes back (SkillSendBack; floored if none).
    ///   K-궁1 킹 행진        10 s: the king walks 1.3, 1.5, 1.7 times as fast for no, one, more teammates within 4 m; they and
    ///                      he are not floored, no enemy comes within 2 m; the key again is a march step that throws the
    ///                      enemies ahead off.
    ///
    /// Prototypes, lines and rings by PawnRushPlanMarks. Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        // ---------------------------------------------------------------- the gauge

        /// <summary>Sends a piece back to its last checkpoint (a queen given up); the scene sets it (the skill test bed: its
        /// respawn). Null: the piece is floored where it is.</summary>
        public static Action<RagdollPawn> SkillSendBack;

        /// <summary>The gauges start empty (ResetSkill): an ultimate in a slot waits its whole gauge first.</summary>
        void StartGauges()
        {
            var s = PawnRushSkills;
            var lo = Loadout;
            // (The kits' second version's gauge is its own, filled by crowns, time and knockdowns: RagdollPawn.UltGauge.cs.)
            if (s == null || lo == null || s.realGauge) return;
            int was = cdSlot;
            for (int k = 0; k < lo.SlotCount && k < slotCd.Length; k++)
            {
                if (!PawnRushSkillIds.Ultimate(lo.Skill(piece, k))) continue;
                cdSlot = k;
                StartCooldown(s.ultGauge);
            }
            cdSlot = was;
        }

        /// <summary>Before the run (PoolKeys): the ultimates that move the piece themselves take the keys; in the air the
        /// smothered mate's left click is its timing (not a dive).</summary>
        void UltKeys()
        {
            if (planPhase == PlanPhase.None) return;
            switch (activeSkill)
            {
                case PawnRushSkillIds.Smother:
                case PawnRushSkillIds.QueenSac:
                    if (planPhase != PlanPhase.Active) return;
                    if (activeSkill == PawnRushSkillIds.Smother && input.shove) smotherClickAt = Time.time;
                    input.move = Vector3.zero;
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
                case PawnRushSkillIds.Chariot:
                    input.move = Vector3.zero;
                    input.jump = false;
                    input.sprint = false;
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
            }
        }

        // ---------------------------------------------------------------- P-궁1: promotion

        float promoteLeft;
        PieceKind promoteFrom;

        /// <summary>Promoted for this long still (the host's).</summary>
        public float PromoteLeft => Mathf.Max(0f, promoteLeft);

        void UpdatePromote(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill() || promoteLeft > 0f) break;
                    SkillUses++;
                    planNote = 0;
                    skillDir = AimFlat();
                    planDir = skillDir;
                    planPoint = FeetPoint + skillDir * s.promoteStep;   // the last rank, ahead
                    skillHitSet.Clear();
                    StartDash(s.promoteStep / Mathf.Max(0.05f, s.promoteStepTime), s.promoteStepTime);
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = "승격: 마지막 한 칸";
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    if (WallAhead(dashBase * dt + 0.25f, out _)) dashLeft = 0f;
                    else
                    {
                        DriveDash(dt);
                        dashLeft -= dt;
                    }
                    if (dashLeft > 0f) break;
                    EndDash();
                    Promote(s);
                    EndPlanSkill();
                    StartCooldown(s.ultGauge);
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The foot on the last rank: the hit stop, the enemies round shoved off, and the queen it becomes.</summary>
        void Promote(PawnRushSkillParams s)
        {
            Vector3 c = FeetPoint;
            int n = 0;
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable) continue;
                Vector3 d = Flat(other.bodies[0].position - c);
                if (d.magnitude > s.promoteShoveRadius || Mathf.Abs(other.FeetPoint.y - c.y) > 1.5f) continue;
                Vector3 away = d.sqrMagnitude > 1e-4f ? d.normalized : planDir;
                SkillHit(other, away * s.promoteShove + Vector3.up * 1f, false, "승격 (끝줄)");
                n++;
            }
            FeelHit(null, c, planDir, FeelUltimate, Mathf.Max(1, n), false, s.promoteStop);
            Fx(SkillFxKind.QueenBlast, null, c, planDir, n, s.promoteShoveRadius);
            promoteFrom = piece;
            SetPiece(PieceKind.Queen);
            promoteLeft = s.promoteSeconds;
            Log($"{DisplayName}: 승격! {s.promoteSeconds:0}초 동안 퀸 (둘레 적 {n}명 밀림)");
        }

        /// <summary>Every step (UpdatePoolStates): the promotion runs out and the piece is what it was again.</summary>
        void UpdatePromotion(float dt)
        {
            if (promoteLeft <= 0f) return;
            promoteLeft -= dt;
            if (promoteLeft > 0f) return;
            if (SkillRunning) ClearSkills();   // (a queen's skill half way ends with the queen)
            SetPiece(promoteFrom);
            Log($"{DisplayName}: 승격이 끝남 → 원래 기물로");
        }

        // ---------------------------------------------------------------- N-궁1: the smothered mate

        RagdollPawn smotherCenter;
        float smotherClickAt = -10f;
        Vector3 smotherFirst;
        const byte SmotherBlunderBit = 1, SmotherSharpBit = 2;

        /// <summary>The smothered mate went in on nothing (any PC).</summary>
        public bool SmotherBlunder => PlanSkill == PawnRushSkillIds.Smother && planPhase == PlanPhase.Recovery && (planNote & SmotherBlunderBit) != 0;

        /// <summary>The enemy a smothered mate would fall on now (any PC): the nearest within smotherRange that two or more of
        /// its teammates crowd within smotherCluster.</summary>
        public RagdollPawn SmotherTarget()
        {
            var s = PawnRushSkills;
            if (s == null) return null;
            Vector3 me = bodies[0].position;
            RagdollPawn best = null;
            float bestAcross = s.smotherRange;
            foreach (var e in All)
            {
                if (e == null || e == this || !IsEnemyOf(e) || e.State != PawnState.Active || e.SkillUntargetable) continue;
                float across = Flat(e.bodies[0].position - me).magnitude;
                if (across > bestAcross || CrowdAround(e, s.smotherCluster) < 2) continue;
                best = e;
                bestAcross = across;
            }
            return best;
        }

        /// <summary>How many standing teammates of <paramref name="e"/> are within <paramref name="r"/> of it.</summary>
        static int CrowdAround(RagdollPawn e, float r)
        {
            int n = 0;
            foreach (var p in All)
                if (p != null && p != e && e.IsAllyOf(p) && p.State == PawnState.Active
                    && Flat(p.bodies[0].position - e.bodies[0].position).magnitude <= r) n++;
            return n;
        }

        void UpdateSmother(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill(true)) break;
                    var center = SmotherTarget();
                    if (center == null)
                    {
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Knight);
                        Log($"스머더드 메이트: {s.smotherRange:0} m 안에 둘 이상에게 둘러싸인 적이 없음");
                        break;
                    }
                    SkillUses++;
                    smotherCenter = center;
                    planTarget = center;
                    planNote = 0;
                    planPoint = center.FeetPoint;
                    smotherFirst = planPoint;
                    planDir = FlatDir(planPoint - FeetPoint);
                    smotherClickAt = -10f;
                    Launch(LeapVelocity(bodies[0].position, planPoint + Vector3.up * standHeight, s.smotherFlight, s.smotherHeight));
                    facing = planDir;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    Fx(SkillFxKind.KnightLeap, null, FeetPoint, planDir, 1);
                    SkillDetail = "스머더드 메이트: 떨어지는 중 (이동 키로 쫓기 · 닿기 직전 좌클릭)";
                    Log($"스머더드 메이트 → {center.DisplayName} (둘러싼 팀원 {CrowdAround(center, s.smotherCluster)}명)");
                    break;
                }
                case PlanPhase.Active:
                    planTime += dt;
                    SteerFall(s.smotherFlight, smotherFirst, s.smotherChase, s.smotherChaseSpeed, dt);
                    if (Climbing || rope != null || planTime > s.smotherFlight + 2f)
                    {
                        EndPlanSkill();
                        StartCooldown(s.ultGauge);
                    }
                    else if (planTime > 0.3f && Grounded)
                    {
                        SmotherLand(s, Time.time - smotherClickAt <= s.smotherClickEarly);
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.smotherRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.ultGauge);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>Falling onto planPoint: the move keys move it (at most <paramref name="most"/> from where it was first),
        /// and the body is steered so it comes down there after <paramref name="flight"/> seconds in all.</summary>
        void SteerFall(float flight, Vector3 first, float most, float speed, float dt)
        {
            Vector3 want = Flat(skillMoveRaw);
            if (want.sqrMagnitude > 0.04f)
            {
                Vector3 p = planPoint + want.normalized * speed * dt;
                Vector3 off = Flat(p - first);
                if (off.magnitude > most) p = new Vector3(first.x, p.y, first.z) + off.normalized * most;
                planPoint = p;
            }
            float left = Mathf.Max(0.15f, flight - planTime);
            Vector3 need = Flat(planPoint - bodies[0].position) / left;
            Vector3 have = Flat(bodies[0].linearVelocity);
            AddVelocity((need - have) * Mathf.Clamp01(dt * 6f));
        }

        /// <summary>Down on the middle one: it is squashed, the ones round it fly, the knight springs up. Down on nothing, it
        /// goes in head first.</summary>
        void SmotherLand(PawnRushSkillParams s, bool sharp)
        {
            Vector3 feet = FeetPoint;
            planPoint = feet;
            var center = smotherCenter;
            if (center == null || center.State != PawnState.Active || Flat(center.FeetPoint - feet).magnitude > s.smotherHit)
            {
                planNote |= SmotherBlunderBit;
                FeelMiss(feet, planDir);
                Knockdown("스머더드 메이트 헛침");   // head first (the knockdown ends the skill; its gauge runs again)
                Log("스머더드 메이트: 아무도 없는 곳에 떨어져 머리부터 박힘");
                return;
            }
            float flat = s.smotherSquash + (sharp ? s.smotherClickBonus : 0f);
            if (center.Squash(flat, 0f)) center.GaugeKnocked(this);   // squashed by an enemy: its gauge fills (KitTwo's gauge)
            center.LastSkillHit = $"스머더드 메이트: 납작 {flat:0.#}초";
            int n = 1;
            foreach (var other in All)
            {
                if (other == null || other == this || other == center || !IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable) continue;
                Vector3 d = Flat(other.bodies[0].position - feet);
                if (d.magnitude > s.smotherCluster + 0.5f) continue;
                Vector3 away = d.sqrMagnitude > 1e-4f ? d.normalized : planDir;
                SkillHit(other, away * s.smotherPush + Vector3.up * s.smotherLift, true, "스머더드 메이트");
                n++;
            }
            if (sharp) planNote |= SmotherSharpBit;
            FeelHit(center, feet, planDir, FeelUltimate, n, true, sharp ? s.smotherClickStop : 0f);
            Fx(SkillFxKind.KnightStomp, center, feet, planDir);
            Fx(SkillFxKind.KnightLand, null, feet, planDir, n, s.smotherCluster);
            Launch(Vector3.up * s.smotherBounce);
            Log($"스머더드 메이트 → {center.DisplayName}: 납작 {flat:0.#}초, 둘레 {n - 1}명 날아감" + (sharp ? " (때 맞춘 좌클릭)" : ""));
        }

        // ---------------------------------------------------------------- B-궁1: the long diagonal

        readonly HashSet<RagdollPawn> diagNailed = new HashSet<RagdollPawn>();

        /// <summary>Where the long diagonal starts (any PC): its end less its length (the small number, metres) back.</summary>
        public Vector3 DiagStart => planPoint - planDir * planNote;

        void UpdateLongDiagonal(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    planPhase = PlanPhase.Aim;
                    planTime = 0f;
                    skillGrabLatch = skillGrabRaw;
                    planDir = AimFlat();
                    DiagLay(s);
                    SkillDetail = "긴 대각선: 조준 (좌클릭 확정 · 우클릭/키 취소)";
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    planDir = AimFlat();
                    facing = planDir;
                    DiagLay(s);
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        Log("긴 대각선: 조준 취소");
                        break;
                    }
                    if (!confirm) break;
                    SkillUses++;
                    diagNailed.Clear();
                    Kick(-planDir, s.diagRecoil, 0.2f, true);
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    SkillDetail = "긴 대각선: 종이 울림";
                    Log($"긴 대각선: {planNote} m — {s.diagWarn:0.#}초 뒤 {s.diagSeconds:0}초 동안");
                    break;
                case PlanPhase.Windup:
                    // The warning everyone sees: the line and the bell.
                    planTime += dt;
                    if (planTime < s.diagWarn) break;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    Fx(SkillFxKind.BishopRopeThrow, null, LassoHands, planDir);   // (the rope's swish, borrowed)
                    break;
                case PlanPhase.Active:
                    // The diagonal is held: every enemy on it, or stepping onto it, is nailed (once a use).
                    planTime += dt;
                    DiagNail(s);
                    if (planTime >= s.diagSeconds)
                    {
                        EndPlanSkill();
                        StartCooldown(s.ultGauge);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The line from the hands along the aim, to the end of its range or short of a wall (people do not stop it);
        /// its length goes in the small number so every PC can draw it.</summary>
        void DiagLay(PawnRushSkillParams s)
        {
            Vector3 from = LassoHands;
            float reach = s.diagRange;
            if (SolidRay(from, planDir, reach, out var wall)) reach = wall.distance;
            reach = Mathf.Min(reach, 255f);
            planNote = (byte)Mathf.RoundToInt(reach);
            planPoint = from + planDir * planNote;
        }

        void DiagNail(PawnRushSkillParams s)
        {
            Vector3 a = DiagStart, u = FlatDir(planDir);
            float len = planNote;
            foreach (var e in All)
            {
                if (e == null || e == this || !IsEnemyOf(e) || e.State != PawnState.Active || e.SkillUntargetable || diagNailed.Contains(e)) continue;
                Vector3 p = e.LassoBelly;
                Vector3 flat = Flat(p - a);
                float t = Vector3.Dot(flat, u);
                if (t < 0f || t > len || (flat - u * t).magnitude > s.diagRadius || Mathf.Abs(p.y - a.y) > 1.5f) continue;
                diagNailed.Add(e);
                string result = SkillBind(e, s.diagNail, "긴 대각선");
                FeelHit(e, p, u, FeelWeak);
                Fx(SkillFxKind.PawnHit, e, p, u, 0);
                Log($"긴 대각선 → {e.DisplayName}: {result}");
            }
        }

        // ---------------------------------------------------------------- R-궁6: the chariot

        float chariotStopLeft;
        bool chariotStopped, chariotSideHeld;

        void UpdateChariot(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    skillDir = AimFlat();
                    planDir = skillDir;
                    planNote = 0;
                    chariotStopLeft = 0f;
                    chariotStopped = chariotSideHeld = false;
                    skillHitSet.Clear();
                    StartDash(s.chariotSpeed, s.chariotSeconds);
                    getUpGuardLeft = Mathf.Max(getUpGuardLeft, s.chariotSeconds + 0.3f);   // the chariot is not floored
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    Fx(SkillFxKind.RookSlam, null, FeetPoint, skillDir, 0);
                    SkillDetail = "활발한 룩: A · D 직각 · S 한 번 멈춤 · 키 끝내기";
                    Log($"{DisplayName}: 활발한 룩 — {s.chariotSeconds:0}초 동안 전차");
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    if (press)
                    {
                        EndChariot(s, "끝냄");
                        break;
                    }
                    // A or D: a right angle each time it is pressed; S: one stop of a second.
                    Vector3 fwd = FlatDir(skillDir), right = Vector3.Cross(Vector3.up, fwd), want = Flat(skillMoveRaw);
                    float side = Vector3.Dot(want, right), back = -Vector3.Dot(want, fwd);
                    bool sideNow = Mathf.Abs(side) > 0.5f;
                    if (sideNow && !chariotSideHeld)
                    {
                        skillDir = right * Mathf.Sign(side);
                        planDir = skillDir;
                        planNote = (byte)Mathf.Min(planNote + 1, 255);
                        Fx(SkillFxKind.RookSlam, null, FeetPoint, skillDir, 0);
                    }
                    chariotSideHeld = sideNow;
                    if (!chariotStopped && back > 0.5f)
                    {
                        chariotStopped = true;
                        chariotStopLeft = s.chariotStop;
                    }
                    if (chariotStopLeft > 0f)
                    {
                        chariotStopLeft -= dt;
                        Vector3 own = Flat(bodies[0].linearVelocity) - carryVel;
                        AddVelocity(-own);
                    }
                    else
                    {
                        if (WallAhead(dashBase * dt + 0.3f, out _))
                        {
                            HaltDash();
                            getUpGuardLeft = 0f;
                            Stagger(s.chariotWallStagger);
                            EndChariot(s, "벽에 박음");
                            break;
                        }
                        DriveDash(dt);
                        ChariotHits(s);
                    }
                    if (planTime >= s.chariotSeconds) EndChariot(s, $"{s.chariotSeconds:0}초");
                    break;
                }
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void ChariotHits(PawnRushSkillParams s)
        {
            Vector3 dir = FlatDir(skillDir), right = Vector3.Cross(Vector3.up, dir);
            Vector3 at = bodies[(int)BodyId.Chest].position + dir * 0.4f;
            int n = Physics.OverlapSphereNonAlloc(at, s.chariotRadius, skillOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!ColliderOwner.TryGetValue(skillOverlap[i], out var other) || other == null || other == this || skillHitSet.Contains(other)) continue;
                if (!IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable) continue;
                skillHitSet.Add(other);
                float sideSign = Vector3.Dot(other.bodies[0].position - bodies[0].position, right) >= 0f ? 1f : -1f;
                string result = SkillHit(other, right * sideSign * s.chariotSideways + dir * s.chariotForward + Vector3.up * s.chariotLift, true, "활발한 룩");
                Vector3 contact = SkillContact(other);
                Fx(SkillFxKind.RookHit, other, contact, dir, skillHitSet.Count);
                FeelHit(other, contact, dir, FeelHard);
                Log($"활발한 룩 → {other.DisplayName}: {result}");
            }
        }

        void EndChariot(PawnRushSkillParams s, string why)
        {
            if (dashing) EndDash();
            getUpGuardLeft = Mathf.Min(getUpGuardLeft, 0.3f);
            EndPlanSkill();
            StartCooldown(s.ultGauge);
            Log($"{DisplayName}: 활발한 룩 끝 ({why}, 꺾기 {planNote}번)");
        }

        // ---------------------------------------------------------------- Q-궁1: the queen's sacrifice

        bool sacAimValid;
        Vector3 sacFirst;
        const byte SacBrilliantBit = 1, SacBlunderBit = 2;

        /// <summary>The sacrifice's result while it is shown (any PC).</summary>
        public bool SacBrilliant => PlanSkill == PawnRushSkillIds.QueenSac && planPhase == PlanPhase.Recovery && (planNote & SacBrilliantBit) != 0;
        public bool SacBlunder => PlanSkill == PawnRushSkillIds.QueenSac && planPhase == PlanPhase.Recovery && (planNote & SacBlunderBit) != 0;

        void UpdateQueenSac(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    planPhase = PlanPhase.Aim;
                    planTime = 0f;
                    planNote = 0;
                    skillGrabLatch = skillGrabRaw;
                    SacAim(s);
                    SkillDetail = "퀸 희생: 떨어질 곳 겨눔 (좌클릭 몸 던지기 · 우클릭/키 취소)";
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    SacAim(s);
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        Log("퀸 희생: 취소");
                        break;
                    }
                    if (!confirm) break;
                    if (!sacAimValid)
                    {
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Queen);
                        Log("퀸 희생: 거기에는 못 떨어짐");
                        break;
                    }
                    SkillUses++;
                    sacFirst = planPoint;
                    Launch(LeapVelocity(bodies[0].position, planPoint + Vector3.up * standHeight, s.sacFlight, s.sacHeight));
                    facing = planDir;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = "퀸 희생: 몸을 던짐 (이동 키로 옮기기)";
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    SteerFall(s.sacFlight, sacFirst, s.sacChase, s.sacChaseSpeed, dt);
                    if (planTime > s.sacFlight + 2f || (planTime > 0.3f && Grounded)) SacBlast(s);
                    break;
                case PlanPhase.Recovery:
                    // The blast's result shown a moment, then the queen is taken.
                    planTime += dt;
                    if (planTime < s.sacShow) break;
                    EndPlanSkill();
                    StartCooldown(s.ultGauge);
                    if (SkillSendBack != null) SkillSendBack(this);
                    else Knockdown("퀸 희생 (잡힘)");
                    Log($"{DisplayName}: 퀸 희생 — 잡혀서 돌아감");
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void SacAim(PawnRushSkillParams s)
        {
            AimFloor(s.sacRange, s.sacRise, out Vector3 point, out bool valid, out _);
            planPoint = point;
            sacAimValid = valid;
            planDir = FlatDir(point - FeetPoint);
        }

        /// <summary>Down: every enemy within sacRadius goes down and flies, and the queen is taken a moment later.</summary>
        void SacBlast(PawnRushSkillParams s)
        {
            Vector3 c = FeetPoint;
            planPoint = c;
            int n = 0;
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable) continue;
                Vector3 d = Flat(other.bodies[0].position - c);
                if (d.magnitude > s.sacRadius || Mathf.Abs(other.FeetPoint.y - c.y) > 3f) continue;
                Vector3 away = d.sqrMagnitude > 1e-4f ? d.normalized : planDir;
                SkillHit(other, away * s.sacPush + Vector3.up * s.sacLift, true, "퀸 희생");
                n++;
            }
            planNote = (byte)(n >= s.sacBrilliant ? SacBrilliantBit : n == 0 ? SacBlunderBit : 0);
            FeelHit(null, c, planDir, FeelUltimate, Mathf.Max(1, n), false, s.sacStop);
            if (n == 0) FeelMiss(c, planDir);
            Fx(SkillFxKind.QueenBlast, null, c, planDir, n, s.sacRadius);
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            Log($"퀸 희생: 둘레 {s.sacRadius:0} m의 적 {n}명 날아감" + (n >= s.sacBrilliant ? " — 탁월한 수 !!" : n == 0 ? " — 블런더 ??" : ""));
        }

        // ---------------------------------------------------------------- K-궁1: the king's march

        float marchScale = 1f, marchShoveAt;

        /// <summary>The king's march's speed on the run now (RagdollPawn.TopSpeed, through SkillMoveScale).</summary>
        float MarchScale => PlanSkill == PawnRushSkillIds.KingMarch && planPhase == PlanPhase.Active ? marchScale : 1f;

        void UpdateKingMarch(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planNote = 0;
                    marchShoveAt = 0f;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    Fx(SkillFxKind.RookSlam, null, FeetPoint, FlatDir(facing), 0);   // (the horn: a heavy thud, borrowed)
                    SkillDetail = "킹 행진 (키 = 행진 걸음)";
                    Log($"{DisplayName}: 킹 행진 — {s.marchSeconds:0}초");
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    if (press) MarchStep(s);
                    MarchAura(s, dt);
                    if (planTime >= s.marchSeconds)
                    {
                        EndPlanSkill();
                        StartCooldown(s.ultGauge);
                        Log($"{DisplayName}: 킹 행진 끝 (행진 걸음 {planNote}번)");
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The march about the king: he and his teammates within marchGuard are not floored, no enemy stays within
        /// marchKeep (shoved off), and the more teammates march with him the faster he walks.</summary>
        void MarchAura(PawnRushSkillParams s, float dt)
        {
            getUpGuardLeft = Mathf.Max(getUpGuardLeft, 0.25f);
            int mates = 0;
            bool shove = (marchShoveAt -= dt) <= 0f;
            if (shove) marchShoveAt = 0.2f;
            Vector3 me = bodies[0].position;
            foreach (var p in All)
            {
                if (p == null || p == this || p.State != PawnState.Active) continue;
                float across = Flat(p.bodies[0].position - me).magnitude;
                if (IsAllyOf(p) && across <= s.marchGuard)
                {
                    mates++;
                    p.getUpGuardLeft = Mathf.Max(p.getUpGuardLeft, 0.25f);
                }
                else if (shove && IsEnemyOf(p) && !p.SkillUntargetable && across <= s.marchKeep)
                    p.Kick(FlatDir(p.bodies[0].position - me), Mathf.Max(0.3f, s.marchKeep + 0.3f - across), 0.15f, false);
            }
            marchScale = mates == 0 ? s.marchAlone : mates == 1 ? s.marchOne : s.marchMany;
        }

        /// <summary>A march step (the key again): a drum's thud, and the enemies ahead within marchStepReach are thrown off.</summary>
        void MarchStep(PawnRushSkillParams s)
        {
            Vector3 me = bodies[0].position, fwd = FlatDir(facing);
            int n = 0;
            foreach (var p in All)
            {
                if (p == null || p == this || !IsEnemyOf(p) || p.State != PawnState.Active || p.SkillUntargetable) continue;
                Vector3 d = Flat(p.bodies[0].position - me);
                if (d.magnitude > s.marchStepReach || Vector3.Angle(fwd, d) > 70f) continue;
                p.Kick(d.sqrMagnitude > 1e-4f ? d.normalized : fwd, s.marchStepShove, 0.25f, false);
                FeelHit(p, p.bodies[0].position, fwd, FeelWeak);
                n++;
            }
            planNote = (byte)Mathf.Min(planNote + 1, 255);
            Fx(SkillFxKind.RookSlam, null, FeetPoint, fwd, 0);
            Log($"킹 행진: 행진 걸음 — 앞의 적 {n}명 밀어냄");
        }
    }
}
