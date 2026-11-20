using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The skill pool's plan 3, its mid row (JY-skill-lab, 2026-11-20; the user: "응, 중급기"). Each as its pool row writes it
    /// with its 검토 column's changes; no cooldown given back for a hit (the user ruled it out).
    ///
    ///   P-중4 쫓아내기   new. The key raises the spear (running on); the left click held draws it (0.2 to 0.8 s: a 3 to 5 m
    ///                   thrust), let go it thrusts. The first enemy is thrown as far as its value (a pawn 1 m, a knight or
    ///                   bishop 3, the king 4, a rook 5, a queen 9 - at most 6 m): a rook or a queen floored and flying,
    ///                   the cheaper ones pushed; the pawn bounces back off it. A miss falls flat on its face.
    ///   N-중1 포크       plan 1-나's fork, the pool's way. The key aims where it comes down (9 m, the floor the camera
    ///                   looks at), the left click leaps; landing, both hind legs kick every enemy within 1.6 m - two or
    ///                   more go down and fly apart (a 0.1 s hit stop, the knight springs up again; a king and a queen
    ///                   together: "로열 포크"), one alone is only shoved and the knight wobbles, none and it sits down
    ///                   in the splits. The left click just before it lands throws them 1.5 times as far.
    ///   B-중1 핀         plan 1-나's pin, the pool's way (B-중3's rule in, the rope tying two out). The key aims, the left
    ///                   click held draws the line (10 to 18 m), let go it shoots and the bishop is kicked 1 m back. The
    ///                   first enemy on it: with a teammate of its close behind on the line, if it moves in the next 3 s
    ///                   the one behind is struck; a wall or pillar behind, it is tied there 3 s; nothing, heavy feet 2 s.
    ///                   The key again within 3 s slides the bishop along the line up to 6 m to it.
    ///   Q-중5 연속 체크  new. The check's square, long toward the queen (2 x 3.5 m) so the nearest way out is away from her,
    ///                   blows in 0.9 s. Dodged, the key within 1 s slides her into the square it left and checks again, its
    ///                   way out the way the camera looks - three at most, in 0.7 and 0.5 s. Struck, floored and flying;
    ///                   the third holds long and is "최선수".
    ///
    /// Prototypes, lines and rings by PawnRushPlanMarks. Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        // ---------------------------------------------------------------- P-중4: chasing away

        float chaseReach;

        float ChaseReachFor(PawnRushSkillParams s, float charge) =>
            Mathf.Lerp(s.chaseNear, s.chaseFar, Mathf.InverseLerp(s.chaseChargeMin, s.chaseChargeMax, charge));

        /// <summary>How far the spear would thrust now (any PC): the longer the left click is held, the farther.</summary>
        public float ChaseReach
        {
            get
            {
                var s = PawnRushSkills;
                if (s == null) return 0f;
                return planPhase == PlanPhase.Windup ? ChaseReachFor(s, planTime) : planPhase == PlanPhase.Active ? chaseReach : s.chaseNear;
            }
        }

        void UpdateChaseAway(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
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
                    planPoint = FeetPoint + planDir * s.chaseNear;
                    SkillDetail = "쫓아내기: 창 겨눔 (좌클릭 누르고 모으기 · 떼면 찌름 · 우클릭/키 취소)";
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    planDir = AimFlat();
                    planPoint = FeetPoint + planDir * s.chaseNear;
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        Log("쫓아내기: 창을 거둠");
                        break;
                    }
                    if (!confirm) break;
                    SkillUses++;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    SkillDetail = "쫓아내기: 모으는 중 (떼면 찌름)";
                    if (!drawHeld) BeginChase(s);   // a click let go at once: the short thrust
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    planDir = AimFlat();
                    planPoint = FeetPoint + planDir * ChaseReachFor(s, planTime);
                    if (cancel || press)
                    {
                        EndPlanSkill();
                        Log("쫓아내기: 모으다 취소");
                        break;
                    }
                    if (!drawHeld || planTime > s.chaseHold) BeginChase(s);
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    if (WallAhead(dashBase * dt + 0.25f, out _)) dashLeft = 0f;
                    else
                    {
                        DriveDash(dt);
                        if (ChaseHits(s)) break;
                        dashLeft -= dt;
                    }
                    if (dashLeft > 0f) break;
                    EndDash();
                    // The pool's 검토: a miss falls flat on its face (the knockdown ends the skill, its cooldown runs).
                    FeelMiss(FeetPoint, skillDir);
                    Knockdown("쫓아내기 헛침");
                    AddVelocity(FlatDir(skillDir) * s.chaseMissLurch);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    Log("쫓아내기: 헛쳐서 앞으로 엎어짐");
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.chaseRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.chaseCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void BeginChase(PawnRushSkillParams s)
        {
            chaseReach = ChaseReachFor(s, planTime);
            skillDir = AimFlat();
            planDir = skillDir;
            planPoint = FeetPoint + skillDir * chaseReach;
            skillHitSet.Clear();
            StartDash(s.chaseSpeed, chaseReach / Mathf.Max(1f, s.chaseSpeed));
            planPhase = PlanPhase.Active;
            planTime = 0f;
            SkillDetail = "쫓아내기: 찌르기";
            Fx(SkillFxKind.PawnStep, null, FeetPoint, skillDir, 1);   // (the pawn's step, borrowed)
        }

        /// <summary>The spear's tip this step: the first enemy there takes it, and the thrust stops on it.</summary>
        bool ChaseHits(PawnRushSkillParams s)
        {
            Vector3 dir = FlatDir(skillDir);
            Vector3 at = bodies[(int)BodyId.Chest].position + dir * 0.4f;
            int n = Physics.OverlapSphereNonAlloc(at, s.chaseRadius, skillOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!ColliderOwner.TryGetValue(skillOverlap[i], out var other) || other == null || other == this || skillHitSet.Contains(other)) continue;
                if (!IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable) continue;
                skillHitSet.Add(other);
                HaltDash();
                planPhase = PlanPhase.Recovery;
                planTime = 0f;
                ChaseStrike(s, other, dir);
                return true;
            }
            return false;
        }

        /// <summary>Struck by the cheapest piece: thrown as far as its own value (at most chaseMaxThrow) - the dear ones
        /// floored and flying, the cheap ones pushed - and the pawn bounces back off it.</summary>
        void ChaseStrike(PawnRushSkillParams s, RagdollPawn other, Vector3 dir)
        {
            int value = other.piece == PieceKind.King ? 4 : PieceValue(other.piece);
            float metres = Mathf.Min(s.chaseMaxThrow, value);
            Vector3 contact = SkillContact(other);
            string result;
            if (value >= s.chaseKnockValue)
            {
                float k = metres / Mathf.Max(1f, s.chaseMaxThrow);
                result = SkillHit(other, dir * (s.chaseKnockPush * Mathf.Lerp(0.7f, 1f, k)) + Vector3.up * s.chaseKnockLift, true, "쫓아내기");
                Fx(SkillFxKind.QueenHit, other, contact, dir, 1);
                FeelHit(other, contact, dir, FeelHard);
            }
            else
            {
                other.Kick(dir, metres, 0.2f + 0.05f * metres, false);
                result = $"{metres:0.#} m 밀림";
                other.LastSkillHit = $"쫓아내기: {result}";
                Fx(SkillFxKind.PawnHit, other, contact, dir, 0);
                FeelHit(other, contact, dir, FeelWeak);
            }
            Kick(-dir, s.chaseBounce, 0.2f, true);
            Log($"쫓아내기 → {other.DisplayName} (값 {value}): {result}");
        }

        // ---------------------------------------------------------------- N-중1: the fork, aimed

        float forkClickAt = -10f;
        bool forkAimValid;
        const byte ForkDoubleBit = 1, ForkRoyalBit = 2, ForkMissBit = 4;
        readonly List<RagdollPawn> forkScratch = new List<RagdollPawn>();

        /// <summary>The fork just floored a king and a queen together (any PC).</summary>
        public bool RoyalFork => PlanSkill == PawnRushSkillIds.Fork && planPhase == PlanPhase.Recovery && (planNote & ForkRoyalBit) != 0;

        /// <summary>The enemies the fork would kick from where it is aimed to come down (any PC: the aim comes with the
        /// host's state), for the rings over their heads.</summary>
        public void ForkHelpTargets(List<RagdollPawn> into)
        {
            into.Clear();
            var s = PawnRushSkills;
            if (s == null) return;
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.State != PawnState.Active) continue;
                Vector3 d = other.FeetPoint - planPoint;
                if (Mathf.Abs(d.y) <= 1.2f && Flat(d).magnitude <= s.forkKickRadius) into.Add(other);
            }
        }

        /// <summary>Before the run (PoolKeys): in the air the left click is the kick's timing (not a dive); sat down after
        /// a miss, the keys do nothing.</summary>
        void ForkKeys()
        {
            if (planPhase == PlanPhase.Active && input.shove)
            {
                forkClickAt = Time.time;
                input.shove = false;
            }
            if (planPhase != PlanPhase.Recovery || (planNote & ForkMissBit) == 0) return;
            input.move = Vector3.zero;
            input.jump = false;
            input.sprint = false;
            input.shove = false;
        }

        void ForkAim(PawnRushSkillParams s)
        {
            AimFloor(s.forkAimRange, s.forkAimRise, out Vector3 point, out bool valid, out _);
            planPoint = point;
            forkAimValid = valid && Flat(point - FeetPoint).magnitude >= 1f;
            planDir = FlatDir(point - FeetPoint);
        }

        void UpdatePoolFork(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    planPhase = PlanPhase.Aim;
                    planTime = 0f;
                    planNote = 0;
                    skillGrabLatch = skillGrabRaw;
                    ForkAim(s);
                    SkillDetail = "포크: 내릴 곳 겨눔 (둘 사이로 · 좌클릭 도약 · 우클릭/키 취소)";
                    break;
                case PlanPhase.Aim:
                {
                    planTime += dt;
                    ForkAim(s);
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        Log("포크: 겨눔 취소");
                        break;
                    }
                    if (!confirm) break;
                    if (!forkAimValid)
                    {
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Knight);
                        Log("포크: 거기에는 못 내려섬");
                        break;
                    }
                    SkillUses++;
                    float flight = Mathf.Max(0.45f, Flat(planPoint - FeetPoint).magnitude / Mathf.Max(1f, s.forkLeapSpeed));
                    Launch(LeapVelocity(bodies[0].position, planPoint + Vector3.up * standHeight, flight, s.forkHeight));
                    facing = planDir;
                    forkClickAt = -10f;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = "포크 도약 (내리꽂는 순간 좌클릭)";
                    Fx(SkillFxKind.KnightLeap, null, FeetPoint, planDir, 0);
                    break;
                }
                case PlanPhase.Active:
                    planTime += dt;
                    if (Climbing || rope != null || planTime > 3f)
                    {
                        // Caught a ledge on the way, or still not down long after (it leapt off an edge): no kick.
                        EndPlanSkill();
                        StartCooldown(s.forkCooldown);
                    }
                    else if (planTime > 0.2f && Grounded)
                    {
                        ForkKick(s, Time.time - forkClickAt <= s.forkClickEarly);
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= ((planNote & ForkMissBit) != 0 ? s.forkMissSit : s.forkRecovery))
                    {
                        EndPlanSkill();
                        StartCooldown(s.forkCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>Both hind legs, from where it really came down: two or more enemies go down and fly apart, one alone
        /// is shoved (and the knight wobbles), none and the knight sits down. <paramref name="timed"/>: the left click met
        /// the landing, they fly farther.</summary>
        void ForkKick(PawnRushSkillParams s, bool timed)
        {
            Vector3 feet = FeetPoint;
            planPoint = feet;
            forkScratch.Clear();
            bool king = false, queen = false;
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable) continue;
                Vector3 d = other.FeetPoint - feet;
                if (Mathf.Abs(d.y) > 1.2f || Flat(d).magnitude > s.forkKickRadius) continue;
                forkScratch.Add(other);
                king |= other.piece == PieceKind.King;
                queen |= other.piece == PieceKind.Queen;
            }
            float boost = timed ? s.forkClickBoost : 1f;
            int n = forkScratch.Count;
            if (n >= 2)
            {
                foreach (var other in forkScratch)
                {
                    Vector3 away = FlatDir(other.bodies[0].position - bodies[0].position);
                    string result = SkillHit(other, away * s.forkPush * boost + Vector3.up * s.forkLift, true, "포크");
                    Log($"포크 → {other.DisplayName}: {result}");
                }
                planNote |= ForkDoubleBit;
                if (king && queen) planNote |= ForkRoyalBit;
                FeelHit(forkScratch[0], feet, planDir, FeelHard, n, true, s.forkDoubleStop);
                // Up into the air again off the two.
                Launch(Flat(bodies[0].linearVelocity) * 0.3f + Vector3.up * s.forkDoubleBounce);
            }
            else if (n == 1)
            {
                var other = forkScratch[0];
                Vector3 away = FlatDir(other.bodies[0].position - bodies[0].position);
                string result = SkillHit(other, away * s.forkSinglePush * boost, false, "포크 (한 명만)");
                FeelHit(other, SkillContact(other), away, FeelWeak);
                Stagger(s.forkSingleWobble);
                Log($"포크 → {other.DisplayName}: 한 명만 → {result}, 나이트 기우뚱");
            }
            else
            {
                planNote |= ForkMissBit;
                FeelMiss(feet, planDir);
                Log("포크: 아무도 없음 → 다리를 찢고 주저앉음");
            }
            Fx(SkillFxKind.KnightLand, null, feet, planDir, n, s.forkKickRadius);
            if (n >= 2) Log($"포크: {n}명 넘어짐" + (timed ? " (때 맞춘 좌클릭: 더 멀리)" : "") + ((planNote & ForkRoyalBit) != 0 ? " — 로열 포크!" : ""));
        }

        // ---------------------------------------------------------------- B-중1: the pin, the pool's way

        // What the pin did (its small number): the one behind is threatened, it is tied to a post, it walks on heavy feet.
        const byte PinThreatNote = 1, PinPostNote = 2, PinSlowNote = 3;
        RagdollPawn pinBehindPiece;
        Vector3 pinHeldAt;

        /// <summary>How far the pin's line reaches now (any PC): pinNear aiming, out to pinFar over pinCharge of drawing.</summary>
        public float PinReach
        {
            get
            {
                var s = PawnRushSkills;
                if (s == null) return 0f;
                if (planPhase != PlanPhase.Windup) return s.pinNear;
                return Mathf.Lerp(s.pinNear, s.pinFar, Mathf.Clamp01(planTime / Mathf.Max(0.05f, s.pinCharge)));
            }
        }

        /// <summary>A bishop whose pin has this piece with a teammate of its behind it: moving, that one is struck (any PC).</summary>
        public RagdollPawn PinThreatBy()
        {
            foreach (var p in All)
                if (p != null && p != this && p.PlanSkill == PawnRushSkillIds.Pin && p.planPhase == PlanPhase.Active
                    && p.planNote == PinThreatNote && p.planTarget == this) return p;
            return null;
        }

        /// <summary>The pin's line from its target to what is behind it (the threatened teammate), while that lasts.</summary>
        public bool PinThreatLine => PlanSkill == PawnRushSkillIds.Pin && planPhase == PlanPhase.Active && planNote == PinThreatNote && planTarget != null;

        void UpdatePoolPin(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    planPhase = PlanPhase.Aim;
                    planTime = 0f;
                    planNote = 0;
                    skillGrabLatch = skillGrabRaw;
                    planDir = LineAim(s.pinNear, s.pinAssist, 2f);
                    SkillDetail = "핀 조준 (좌클릭 누르고 모으기 · 떼면 쏨 · 우클릭/스킬 키 취소)";
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    planDir = LineAim(s.pinNear, s.pinAssist, 2f);
                    if (Flat(skillMoveRaw).sqrMagnitude < 0.04f) facing = FlatDir(planDir);
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        Log("핀: 조준 취소");
                        break;
                    }
                    if (!confirm) break;
                    SkillUses++;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    SkillDetail = "핀: 모으는 중 (떼면 쏨)";
                    if (!drawHeld) FirePoolPin(s);
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    planDir = LineAim(PinReach, s.pinAssist, 2f);
                    if (Flat(skillMoveRaw).sqrMagnitude < 0.04f) facing = FlatDir(planDir);
                    if (cancel || press)
                    {
                        EndPlanSkill();
                        Log("핀: 모으다 취소");
                        break;
                    }
                    if (!drawHeld || planTime > 3f) FirePoolPin(s);
                    break;
                case PlanPhase.Active:
                    // The pin holds: the one behind is watched, and the key slides the bishop along the line.
                    planTime += dt;
                    UpdatePinThreat(s);
                    if (press)
                    {
                        PinZip(s);
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        break;
                    }
                    if (planTime >= s.pinZipWindow || planTarget == null || planTarget.State != PawnState.Active)
                    {
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.pinRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.pinCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The line, at once: the first enemy on it (short of a wall) is pinned by what is behind it.</summary>
        void FirePoolPin(PawnRushSkillParams s)
        {
            float reach = PinReach;
            Vector3 from = LassoHands, dir = planDir.sqrMagnitude > 0.5f ? planDir.normalized : FlatDir(facing);
            if (SolidRay(from, dir, reach, out var ahead)) reach = ahead.distance;
            float along = reach;
            var first = FirstOnLine(from, dir, ref along, s.pinRadius + 0.2f, false);
            Vector3 flat = FlatDir(dir);
            Kick(-flat, s.pinRecoil, 0.2f, false);   // the shot's kick (a mid skill's: over an edge it goes)
            planTarget = first;
            planNote = 0;
            pinBehindPiece = null;
            if (first == null)
            {
                planPoint = from + dir * reach;
                planPhase = PlanPhase.Recovery;
                planTime = 0f;
                FeelMiss(planPoint, dir);
                Log("핀: 아무도 안 맞음");
                return;
            }
            Vector3 a = first.LassoBelly;
            float behind = s.pinBehind;
            bool post = SolidRay(a, flat, behind, out var wall) && Mathf.Abs(wall.normal.y) < 0.5f;
            if (post) behind = wall.distance;
            var second = PinFirst(s, a, flat, 0.3f, behind, first);
            Fx(SkillFxKind.PawnHit, first, a, flat, 0);
            FeelHit(first, a, flat, FeelWeak);
            planPhase = PlanPhase.Active;
            planTime = 0f;
            pinHeldAt = first.FeetPoint;
            if (second != null)
            {
                // The chess pin (B-중3's rule): move, and the piece behind is struck.
                planNote = PinThreatNote;
                pinBehindPiece = second;
                planPoint = second.LassoBelly;
                first.LastSkillHit = $"핀: 움직이면 뒤의 {second.DisplayName}가 맞음";
                Log($"핀: {first.DisplayName} — 움직이면 뒤의 {second.DisplayName}가 맞음 ({s.pinThreatSeconds:0.#}초)");
            }
            else if (post)
            {
                planNote = PinPostNote;
                first.BePinned(null, wall.point, true, s);
                first.pinLeft = s.pinPostSeconds;
                planPoint = wall.point;
                Log($"핀: {first.DisplayName}를 뒤의 벽 · 기둥에 꿰음 ({s.pinPostSeconds:0.#}초)");
            }
            else
            {
                planNote = PinSlowNote;
                string result = first.BeSlowed(s.pinSlowSeconds);
                planPoint = a + flat * 1.2f;
                Log($"핀: {first.DisplayName} 뒤가 빔 → {result}");
            }
        }

        /// <summary>The pin's threat this step (the host's): the pinned piece stepping off where it was pinned strikes the one
        /// behind it, once; the threat ends with its time or either piece going down.</summary>
        void UpdatePinThreat(PawnRushSkillParams s)
        {
            if (planNote != PinThreatNote) return;
            var first = planTarget;
            var behind = pinBehindPiece;
            if (first == null || behind == null || first.State != PawnState.Active || behind.State != PawnState.Active || planTime > s.pinThreatSeconds)
            {
                planNote = 0;
                pinBehindPiece = null;
                return;
            }
            planPoint = behind.LassoBelly;   // (the line follows the one behind)
            if (Flat(first.FeetPoint - pinHeldAt).magnitude <= s.pinThreatMove) return;
            Vector3 dir = FlatDir(behind.bodies[0].position - first.bodies[0].position);
            string result = SkillHit(behind, dir * s.pinThreatPush + Vector3.up * s.pinThreatLift, true, "핀 (앞이 움직임)");
            Fx(SkillFxKind.QueenHit, behind, behind.LassoBelly, dir, 1);
            FeelHit(behind, behind.LassoBelly, dir, FeelHard);
            Log($"핀: {first.DisplayName}가 움직임 → 뒤의 {behind.DisplayName}: {result}");
            planNote = 0;
            pinBehindPiece = null;
        }

        /// <summary>The key again while the pin holds: the bishop slides along the line toward the pinned piece, up to
        /// pinZipMax, stopping pinZipGap short of it (to grab it).</summary>
        void PinZip(PawnRushSkillParams s)
        {
            var first = planTarget;
            if (first == null) return;
            Vector3 to = Flat(first.bodies[0].position - bodies[0].position);
            float d = to.magnitude - s.pinZipGap;
            if (d <= 0.2f) return;
            float go = Mathf.Min(d, s.pinZipMax);
            Kick(to.normalized, go, Mathf.Max(0.2f, go / 12f), true);
            Log($"핀: 줄을 타고 {go:0.#} m 다가감");
        }

        // ---------------------------------------------------------------- Q-중5: the series of checks

        // The small number: how many checks so far (1..3) in the low bits, and "최선수".
        const byte SeriesCountMask = 3, SeriesBestBit = 8;

        /// <summary>How many checks of the series so far (1..3; any PC).</summary>
        public int SeriesCount => planNote & SeriesCountMask;

        /// <summary>The series' third check struck (any PC).</summary>
        public bool SeriesBest => PlanSkill == PawnRushSkillIds.CheckSeries && planPhase == PlanPhase.Recovery && (planNote & SeriesBestBit) != 0;

        /// <summary>The series has just been dodged and the key may follow (any PC).</summary>
        public bool SeriesFollowing => PlanSkill == PawnRushSkillIds.CheckSeries && planPhase == PlanPhase.Windup;

        public float SeriesFuse(int count)
        {
            var s = PawnRushSkills;
            if (s == null) return 1f;
            return count <= 1 ? s.seriesFuse1 : count == 2 ? s.seriesFuse2 : s.seriesFuse3;
        }

        /// <summary>A queen whose series of checks has this piece in its square now (any PC).</summary>
        public RagdollPawn SeriesCheckedBy()
        {
            foreach (var p in All)
                if (p != null && p != this && p.PlanSkill == PawnRushSkillIds.CheckSeries && p.planPhase == PlanPhase.Active && p.planTarget == this) return p;
            return null;
        }

        void UpdateCheckSeries(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    planPhase = PlanPhase.Aim;
                    planTime = 0f;
                    planNote = 0;
                    skillGrabLatch = skillGrabRaw;
                    planTarget = CheckTargetWithin(s.seriesRange, s.seriesAssist);
                    SkillDetail = "연속 체크 조준 (적을 보고 좌클릭 · 우클릭/스킬 키 취소)";
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    planTarget = CheckTargetWithin(s.seriesRange, s.seriesAssist);
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        Log("연속 체크: 조준 취소");
                        break;
                    }
                    if (!confirm) break;
                    if (planTarget == null)
                    {
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Queen);
                        Log("연속 체크: 조준에 적이 없음");
                        break;
                    }
                    SkillUses++;
                    // The first square's way out is away from the queen.
                    BeginSeriesCheck(s, 1, FlatDir(planTarget.bodies[0].position - bodies[0].position));
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    var t = planTarget;
                    if (Staggered || bindLeft > 0f || t == null || t.SkillUntargetable)
                    {
                        Log("연속 체크: 끊김");
                        EndPlanSkill();
                        StartCooldown(s.seriesCooldown);
                        break;
                    }
                    if (planTime < SeriesFuse(SeriesCount)) break;
                    ResolveSeries(s, t);
                    break;
                }
                case PlanPhase.Windup:
                    // Dodged: the key follows it (the square it left, then the next check).
                    planTime += dt;
                    if (press)
                    {
                        FollowSeries(s);
                        break;
                    }
                    if (planTime > s.seriesLink)
                    {
                        EndPlanSkill();
                        StartCooldown(s.seriesCooldown);
                        Log("연속 체크: 따라붙지 않음");
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.seriesRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.seriesCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>A square of the series under the target: long along <paramref name="way"/>, the target near its far end
        /// (seriesExit from it), so the nearest way out is on along <paramref name="way"/>.</summary>
        void BeginSeriesCheck(PawnRushSkillParams s, int count, Vector3 way)
        {
            way = FlatDir(way);
            planNote = (byte)Mathf.Clamp(count, 1, 3);
            planDir = way;
            planPoint = planTarget.FeetPoint - way * (s.seriesLength * 0.5f - s.seriesExit);
            planPhase = PlanPhase.Active;
            planTime = 0f;
            facing = FlatDir(planTarget.bodies[0].position - bodies[0].position);
            SkillDetail = $"연속 체크 {count} → {planTarget.DisplayName}";
            Log($"연속 체크 {count} → {planTarget.DisplayName} ({SeriesFuse(count):0.0}초)");
        }

        bool InSeriesSquare(PawnRushSkillParams s, Vector3 feet)
        {
            Vector3 d = feet - planPoint;
            if (Mathf.Abs(d.y) > 1.5f) return false;
            Vector3 u = FlatDir(planDir), w = Vector3.Cross(Vector3.up, u);
            Vector3 flat = Flat(d);
            return Mathf.Abs(Vector3.Dot(flat, u)) <= s.seriesLength * 0.5f && Mathf.Abs(Vector3.Dot(flat, w)) <= s.seriesWidth * 0.5f;
        }

        void ResolveSeries(PawnRushSkillParams s, RagdollPawn t)
        {
            int count = SeriesCount;
            if (!InSeriesSquare(s, t.FeetPoint))
            {
                if (count >= 3)
                {
                    FeelMiss(planPoint, planDir);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    Log("연속 체크: 세 번 다 비킴");
                    return;
                }
                planPhase = PlanPhase.Windup;
                planTime = 0f;
                SkillDetail = "연속 체크: 비켜 나감 → 1초 안에 F (카메라 쪽이 다음 칸의 나갈 곳)";
                Log($"연속 체크 {count}: {t.DisplayName}가 비킴 → 따라붙기");
                return;
            }
            var blocker = CheckBlocker(s, t);
            var hit = blocker != null ? blocker : t;
            Vector3 away = FlatDir(hit.bodies[0].position - bodies[0].position);
            string result = SkillHit(hit, away * s.seriesPush + Vector3.up * s.seriesLift, true, blocker != null ? "연속 체크 (대신 막음)" : "연속 체크");
            Fx(SkillFxKind.QueenHit, hit, hit.bodies[0].position, away, 1);
            bool best = count >= 3;
            FeelHit(hit, hit.bodies[0].position, away, FeelHard, 1, true, best ? s.seriesFinalStop : 0f);
            if (best) planNote |= SeriesBestBit;
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            Log($"연속 체크 {count} → {hit.DisplayName}: {result}" + (best ? " — 최선수!" : ""));
        }

        /// <summary>The key after a dodge: the queen slides into the square it left and checks again, if it is still near.</summary>
        void FollowSeries(PawnRushSkillParams s)
        {
            var t = planTarget;
            Vector3 square = planPoint;
            if (t == null || t.State != PawnState.Active || t.SkillUntargetable)
            {
                EndPlanSkill();
                StartCooldown(s.seriesCooldown);
                return;
            }
            Vector3 way = Flat(square - bodies[0].position);
            float d = Mathf.Min(way.magnitude, s.seriesSlideMax);
            if (d > 0.3f) Kick(way.normalized, d, Mathf.Max(0.2f, d / Mathf.Max(1f, s.seriesSlideSpeed)), true);
            if (Flat(t.bodies[0].position - square).magnitude > s.seriesFollowRange + s.seriesLength)
            {
                planPhase = PlanPhase.Recovery;
                planTime = 0f;
                Log($"연속 체크: {t.DisplayName}가 너무 멀어짐");
                return;
            }
            // The next square's way out: the way the camera looks.
            BeginSeriesCheck(s, SeriesCount + 1, AimFlat());
        }
    }
}
