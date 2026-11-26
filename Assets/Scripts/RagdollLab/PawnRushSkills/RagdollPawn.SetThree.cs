using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The set 3판 (JY-skill-lab, 2026-11-23; Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6.12). The user: "이 게임은 귀여운 캐릭터들이
    /// 우당탕탕 싸우고 넘어지는게 웃긴게 제일 핵심인데 ... 스킬은 1개 아니면 최대 2개에, 궁극기는 빼고" - and the user wrote the
    /// skills. All numbers are first tries (PawnRushSkillParams, the "3판" block).
    ///
    ///   폰 앙파상 (F)            a bolas and rope thrown at the aim (12 m, 25 m/s): the first enemy it meets has its legs
    ///                           wrapped and is pulled in at 8 m/s for a second, the pawn lunging on by the recoil. Within
    ///                           1.5 m the enemy hangs a metre in front for 0.5 s (its head shines): the left click then
    ///                           chops its neck, stunned 1 s; missed, the two bump and the rope comes off.
    ///   나이트 하이점프 & 내려찍기 (F → F)  a 3 m jump (the run carries, 15% steering); a horseshoe marks the enemy within
    ///                           10 m below nearest the aim; F again dives on its head in 0.3 s (flat 1.5 s) and bounces
    ///                           up 1 m. No mark: F drops it straight down.
    ///   비숍 성검 소환 (F)        a holy sword beside every enemy, 5 m off on a diagonal at the waist; 1.5 s later each
    ///                           flies at where its enemy is then (30 m/s, not following); bound 1 s on a hit; three times.
    ///   비숍 돌멩이 던지기         up 1.5 m and hovering 0.6 s to aim; a 1.2 m stone at 10 m/s, 20 m, falling a little: every
    ///                           enemy on its way knocked 3 m off it (1 s down); a wall breaks it.
    ///   비숍 성수병 던지기         a bottle that breaks 8 m ahead: a 3 m puddle for 4 s, an enemy stepping in slips (1 s
    ///                           down) and slides out of it; its own team does not slip.
    ///   룩 룩앤롤 (F → F)        the driven ultimate's rolling column made a normal skill: 2.5 s, 8 to 18 m/s, 30°/s of
    ///                           steering; enemies are pins (10 m, 2 s, three a strike); F again stands it up with a
    ///                           thump (4 m round, 1 s); a wall stops it dizzy 0.5 s; off an edge it falls.
    ///   룩 탑 가두기 (E)          a stomp, and the ground splits straight at the aim (25 m/s, 20 m): the first enemy on it
    ///                           (on its feet: a jump clears it) is shut in a rook's tower 1.5 s (solid to the others), then
    ///                           rolls out of the ruin, 1 s down.
    ///   퀸 여왕의 위상 (F)        an 8 m octagram follows her; 0.5 s later, for each enemy in it, the blade nearest it lights
    ///                           and throws it 8 m along that blade (one of her eight lines), 2 s down.
    ///   퀸 연속 체크 (E)          the enemy within 12 m ahead nearest the aim is checked (red, 0.25 s), she flies at it (0.25 s)
    ///                           and knocks it 4 m; E within 1 s: the next within 8 m, three at most (the third 6 m); an
    ///                           enemy standing between takes it. No target at the first press: no cooldown.
    ///
    /// 11-24 (KITS 6.13): grades again, as numbers only (no name on the screen, nothing unlocked by section) - two a piece of
    /// different grades: the pawn and the knight a basic on E (the old two steps; the back kick, RagdollPawn.BookSkills.cs)
    /// and a mid on F; the bishop the swords (high) and a basic, the stone or the water, to be picked; the rook and the
    /// queen a mid on E and a high on F (the roll, the star: further, longer down, 30 s). A basic's push stops edgeGap short
    /// of an edge (the guard below); a mid's or a high's goes over.
    ///
    /// Three new hindrances: stunned (the pawn's chop: a stagger with stars), bound (the bishop's sword: the R138 bind), shut in
    /// (the rook's tower: the bind, a collider, then a fall). The host runs all of it; the marks come from the plan state
    /// (PawnRushPlanMarks.SetThree.cs) and the effects from the moments (PawnRushSkillFx.SetThree.cs). Not seen in Unity yet.
    /// </summary>
    public partial class RagdollPawn
    {
        // planNote's bits, by skill (any PC reads them).
        public const byte BolasPullBit = 1, BolasChopBit = 2;
        public const byte HopDiveBit = 1, HopDropBit = 2, HopStompBit = 4;
        public const byte RollingBit = 1;
        public const byte CcFlyBit = 1;   // (the checks done in the upper four bits)
        // The words over heads (KitPop), after the kits' second version's.
        public const int PopStrike = 109, PopCheckWord = 110, PopChop = 111, PopSlip = 112, PopTrap = 113, PopBind = 114;

        /// <summary>A push that throws a piece about <paramref name="metres"/> along <paramref name="dir"/> (the eight lines'
        /// 7 m/s and 3 up carry one about 4 m).</summary>
        static Vector3 S3Push(Vector3 dir, float metres) =>
            FlatDir(dir) * (Mathf.Max(0f, metres) * 1.75f) + Vector3.up * Mathf.Min(4f, 1.5f + metres * 0.35f);

        /// <summary>The camera's way (the flat facing with none).</summary>
        Vector3 AimRay()
        {
            Vector3 a = input.aim;
            return a.sqrMagnitude > 0.5f ? a.normalized : FlatDir(facing);
        }

        /// <summary>A way no steeper than <paramref name="limit"/> degrees up or down.</summary>
        static Vector3 Pitched(Vector3 dir, float limit)
        {
            Vector3 flat = FlatDir(dir);
            float p = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, -limit, limit) * Mathf.Deg2Rad;
            return (flat * Mathf.Cos(p) + Vector3.up * Mathf.Sin(p)).normalized;
        }

        /// <summary>Where the camera's line from the head meets the world (or <paramref name="far"/> along it).</summary>
        Vector3 AimPoint(float far)
        {
            Vector3 eye = bodies[(int)BodyId.Head].position, aim = AimRay();
            return SolidRay(eye, aim, 60f, out var hit) ? hit.point : eye + aim * far;
        }

        Vector3 Chest => bodies[(int)BodyId.Chest].position;

        /// <summary>Before the run (PlanKeys): what these skills let the keys do in their phases.</summary>
        void SetThreeKeys()
        {
            if (planPhase == PlanPhase.None) return;
            switch (activeSkill)
            {
                case PawnRushSkillIds.Bolas:
                    // Pulling: the left click is the chop (not a dive).
                    if (planPhase != PlanPhase.Active || (planNote & BolasPullBit) == 0) break;
                    bolasClick |= input.shove;
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
                case PawnRushSkillIds.HighStomp:
                    if (planPhase != PlanPhase.Active) break;
                    input.jump = false;
                    input.shove = false;
                    input.shoveHeld = false;
                    if ((planNote & HopDiveBit) != 0) input.move = Vector3.zero;
                    break;
                case PawnRushSkillIds.RollShort:
                case PawnRushSkillIds.TowerTrap:
                case PawnRushSkillIds.StoneThrow:
                    // Rolling, stamping, hovering: the skill moves it (the move keys are read raw where they steer).
                    if (planPhase == PlanPhase.Recovery) break;
                    input.move = Vector3.zero;
                    input.jump = false;
                    input.sprint = false;
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
                case PawnRushSkillIds.CheckChain:
                    if (planPhase != PlanPhase.Windup && planPhase != PlanPhase.Active) break;
                    input.move = Vector3.zero;
                    input.jump = false;
                    input.sprint = false;
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
            }
        }

        /// <summary>Every step for every piece (UpdateKitTwoStates; the host's): the pull's lunge, a tower round it, the
        /// bishop's swords and stones, the puddles under it.</summary>
        void UpdateSetThreeStates(PawnRushSkillParams s, float dt)
        {
            if (NetworkPuppet || s == null) return;
            if (bolasBoostLeft > 0f) bolasBoostLeft -= dt;
            if (trapLeft > 0f)
            {
                trapLeft -= dt;
                if (trapLeft <= 0f || State != PawnState.Active) TowerFall(s);
            }
            else if (trapTower != null)
            {
                Destroy(trapTower);
                trapTower = null;
            }
            if (swordVolley) SwordsStep(s, dt);
            if (stones.Count > 0) StonesStep(s, dt);
            PuddleStep(s);
            EdgeGuardStep(s, dt);
        }

        /// <summary>What these skills leave running when the skill ends (EndPlanSkill, before its target is let go).</summary>
        void ClearSetThreeSkill()
        {
            skillAirSteer = 0f;
            bolasClick = false;
            if (activeSkill == PawnRushSkillIds.Bolas && planTarget != null && planTarget.OuterFlying) planTarget.EndFlight();
            if (rsRolling)
            {
                rsRolling = false;
                rsAir = false;
                EndFlight();
            }
            if (OuterFlying && (activeSkill == PawnRushSkillIds.StoneThrow || activeSkill == PawnRushSkillIds.CheckChain || activeSkill == PawnRushSkillIds.HighStomp))
                EndFlight();
        }

        /// <summary>Everything of these skills on this piece gone (a reset): its swords, its stones, a tower round it.</summary>
        void ClearSetThreeStates()
        {
            swords.Clear();
            swordVolley = false;
            stones.Clear();
            bolasBoostLeft = 0f;
            edgeGuardLeft = 0f;
            trapLeft = 0f;
            trapBy = null;
            if (trapTower != null)
            {
                Destroy(trapTower);
                trapTower = null;
            }
        }

        /// <summary>A new round (the course's, the test bed's start): no puddle is left.</summary>
        public static void ResetSetThree() => puddles.Clear();

        /// <summary>The first enemy a ball of radius <paramref name="r"/> meets going <paramref name="len"/> along
        /// <paramref name="dir"/> (teammates let it by), or something solid (<paramref name="piece"/> null). A piece no skill
        /// takes, or one lying down, stops it as a wall does. <paramref name="floorFrom"/>: a floor grazed nearer than this
        /// is not a wall (it left from over it).</summary>
        bool S3Sweep(Vector3 p, Vector3 dir, float len, float r, float floorFrom, out Vector3 at, out RagdollPawn piece)
        {
            at = p;
            piece = null;
            if (len <= 1e-4f) return false;
            int n = Physics.SphereCastNonAlloc(p, r, dir, hits, len, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider == null || ownSet.Contains(h.collider) || h.distance >= best) continue;
                if (ColliderOwner.TryGetValue(h.collider, out var other))
                {
                    if (other == null || other == this || IsAllyOf(other)) continue;
                    best = h.distance;
                    found = true;
                    piece = !other.SkillUntargetable && !other.NetworkPuppet && other.State == PawnState.Active && IsEnemyOf(other) ? other : null;
                    at = h.distance > 0f ? h.point : other.Chest;
                    continue;
                }
                if (PassesThrough(h.collider) || h.distance <= 0f) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                if (h.normal.y > 0.7f && h.distance < floorFrom) continue;
                best = h.distance;
                found = true;
                piece = null;
                at = h.point;
            }
            return found;
        }

        // ---------------------------------------------------------------- 초급의 밀림: 낭떠러지 앞에서 멈춤 (6.13)

        // A piece a basic pushed (SkillHit's edgeSafe): for a while each step, the way its hips are going is looked along,
        // and with a drop edgeGap ahead its speed that way is taken away - every body of it. The system's safety rule
        // (PAWN_RUSH_SKILL_SYSTEM.md, 낙사): a basic's push stops 0.3 m short of an edge; from the mid grade on, over it goes.
        float edgeGuardLeft, edgeGuardAge;

        /// <summary>A basic skill just pushed this piece (the host's): it is kept from going over an edge for a while.</summary>
        void GuardEdge()
        {
            if (NetworkPuppet) return;
            edgeGuardLeft = 3f;
            edgeGuardAge = 0f;
        }

        /// <summary>Every step (UpdateSetThreeStates): while guarded, no speed out over a drop. The guard ends once the piece
        /// is on its feet and its own again (edgeHold after the hit), in the water, on a wall or carried: by itself it may
        /// still step off.</summary>
        void EdgeGuardStep(PawnRushSkillParams s, float dt)
        {
            if (edgeGuardLeft <= 0f) return;
            edgeGuardLeft -= dt;
            edgeGuardAge += dt;
            if (Floating || Climbing || rope != null || chainCarry || kingCarry || castleCarry || supCarry
                || (State == PawnState.Active && !Staggered && edgeGuardAge > s.edgeHold))
            {
                edgeGuardLeft = 0f;
                return;
            }
            var hips = bodies[0];
            Vector3 v = Flat(hips.linearVelocity);
            float speed = v.magnitude;
            if (speed < 0.3f) return;
            Vector3 dir = v / speed;
            float ahead = s.edgeGap + speed * Time.fixedDeltaTime * 2f + 0.15f;
            if (!EdgeAhead(s, hips.position, dir, ahead)) return;
            foreach (var rb in bodies)
            {
                Vector3 bv = rb.linearVelocity;
                float outward = bv.x * dir.x + bv.z * dir.z;
                if (outward > 0f) rb.linearVelocity = bv - dir * outward;
            }
            if (launched)
            {
                float c = Vector3.Dot(carryVel, dir);
                if (c > 0f) carryVel -= dir * c;
            }
        }

        /// <summary>A drop within <paramref name="ahead"/> along <paramref name="dir"/> from <paramref name="at"/> (the hips),
        /// looked for in 0.25 m steps: no floor, or one more than edgeDrop below the last at once (stairs going down are no
        /// edge). Over nothing already: too late, no edge.</summary>
        bool EdgeAhead(PawnRushSkillParams s, Vector3 at, Vector3 dir, float ahead)
        {
            if (!SolidRay(at + Vector3.up * 0.3f, Vector3.down, 6f, out var under)) return false;
            float floor = under.point.y;
            for (float d = 0.25f; ; d += 0.25f)
            {
                float k = Mathf.Min(d, ahead);
                Vector3 p = at + dir * k;
                // (From a little above the last floor down: a step up or a low ledge is floor too.)
                if (!SolidRay(new Vector3(p.x, floor + 1.2f, p.z), Vector3.down, 1.2f + s.edgeDrop, out var hit) || hit.normal.y < 0.3f)
                    return true;
                floor = hit.point.y;
                if (k >= ahead) return false;
            }
        }

        // ---------------------------------------------------------------- 폰: 앙파상 (the bolas)

        Vector3 bolasPos, bolasDir;
        float bolasOut, bolasChopLeft, bolasBoostLeft;
        bool bolasClick;

        /// <summary>The pull's lunge (SkillMoveScale): the pawn runs faster for the pull's second.</summary>
        float BolasScale => bolasBoostLeft > 0f && PawnRushSkills != null ? Mathf.Max(1f, PawnRushSkills.bolasRecoilScale) : 1f;

        /// <summary>The bolas is in the air (any PC: the plan point is where).</summary>
        public bool BolasFlying => PlanSkill == PawnRushSkillIds.Bolas && planPhase == PlanPhase.Active && (planNote & BolasPullBit) == 0;
        /// <summary>The rope is pulling the plan target in.</summary>
        public bool BolasPulling => PlanSkill == PawnRushSkillIds.Bolas && planPhase == PlanPhase.Active && (planNote & BolasPullBit) != 0;
        /// <summary>The chop's half second: the left click now strikes the neck.</summary>
        public bool BolasChopOpen => BolasPulling && (planNote & BolasChopBit) != 0;

        void UpdateBolas(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    bolasDir = Pitched(AimRay(), 20f);
                    bolasPos = Chest + FlatDir(bolasDir) * 0.25f;
                    bolasOut = 0f;
                    bolasClick = false;
                    bolasChopLeft = 0f;
                    skillHitSet.Clear();
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    planTarget = null;
                    planNote = 0;
                    planDir = FlatDir(bolasDir);
                    planPoint = bolasPos;
                    facing = planDir;
                    SkillDetail = "볼라스";
                    Fx(SkillFxKind.S3Bolas, null, bolasPos, bolasDir, 0, s.bolasSpeed);
                    Log($"{DisplayName}: 앙파상 — 볼라스를 던짐");
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    if ((planNote & BolasPullBit) == 0) BolasFlight(s, dt);
                    else BolasPullStep(s, dt);
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.2f)
                    {
                        EndPlanSkill();
                        StartCooldown(s.bolasCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void BolasFlight(PawnRushSkillParams s, float dt)
        {
            float step = s.bolasSpeed * dt, left = s.bolasRange - bolasOut;
            bool last = step >= left;
            if (last) step = Mathf.Max(0f, left);
            if (S3Sweep(bolasPos, bolasDir, step, s.bolasRadius, 1f - bolasOut, out var at, out var piece))
            {
                if (piece != null) BolasCatch(s, piece, at);
                else BolasOver(at, 0, "벽에 맞고 떨어짐");
                return;
            }
            bolasPos += bolasDir * step;
            bolasOut += step;
            planPoint = bolasPos;
            if (last) BolasOver(bolasPos, 0, "빗나가 떨어짐");
        }

        void BolasCatch(PawnRushSkillParams s, RagdollPawn e, Vector3 at)
        {
            planTarget = e;
            planNote = BolasPullBit;
            planTime = 0f;
            planPoint = at;
            e.Stagger(s.bolasTrip);
            e.LastSkillHit = "앙파상: 볼라스에 다리가 감김";
            // The recoil: the pawn goes on toward it, faster for the pull's second.
            Vector3 toward = FlatDir(e.bodies[0].position - bodies[0].position);
            AddVelocity(toward * s.bolasRecoil);
            bolasBoostLeft = s.bolasPull;
            Fx(SkillFxKind.S3BolasCatch, e, at, toward);
            FeelHit(e, at, toward, FeelWeak);
            Log($"앙파상 → {e.DisplayName}: 볼라스가 다리에 감김 → 끌어옴");
        }

        void BolasPullStep(PawnRushSkillParams s, float dt)
        {
            var e = planTarget;
            if (e == null || e.State != PawnState.Active || e.Floating || e.SkillUntargetable)
            {
                if (e != null) e.EndFlight();
                BolasOver(e != null ? e.bodies[0].position : bodies[0].position, 1, "밧줄이 풀림 (끌려오지 못함)");
                return;
            }
            Vector3 me = bodies[0].position, there = e.bodies[0].position, flat = Flat(there - me);
            float dist = flat.magnitude;
            Vector3 out_ = dist > 0.01f ? flat / dist : FlatDir(facing);   // from the pawn out to it
            if ((planNote & BolasChopBit) == 0 && dist <= s.bolasChopReach)
            {
                // In reach: it hangs a metre in front, its head shining, for the chop's half second.
                planNote |= BolasChopBit;
                bolasChopLeft = s.bolasChopWindow;
                bolasClick = false;   // (a click before the shine does not count)
            }
            float hold = 1f, nextDist = Mathf.Max(hold, dist - s.bolasPullSpeed * dt);
            Vector3 next = me + out_ * nextDist;
            var floor = DriveFloor(next, 1.2f, 3f);
            next.y = floor.HasValue ? floor.Value.y + e.standHeight : there.y;
            e.Fly(next, -out_ * (dist > hold + 0.05f ? s.bolasPullSpeed : 0f), 0f, default, 16f);
            e.Face(out_);   // (dragged in back first: its neck to the pawn)
            planPoint = e.Chest;
            if ((planNote & BolasChopBit) != 0)
            {
                bolasChopLeft -= dt;
                if (bolasClick)
                {
                    bolasClick = false;
                    Vector3 head = e.bodies[(int)BodyId.Head].position;
                    e.EndFlight(Vector3.zero);
                    bool stunned = e.Stagger(s.bolasStun);
                    e.LastSkillHit = stunned ? $"앙파상: 뒷목 치기 → 기절 {s.bolasStun:0.#}초" : "앙파상: 뒷목 치기 (안 걸림)";
                    if (stunned) Fx(SkillFxKind.S3Stun, e, head, out_, 0, s.bolasStun);
                    PopOver(e, PopChop);
                    FeelHit(e, head, out_, FeelHard);
                    BolasOver(head, 2, stunned ? "뒷목 치기! 기절" : "뒷목 치기 (안 걸림)");
                    return;
                }
                if (bolasChopLeft <= 0f)
                {
                    // Missed: the two bump into each other and the rope comes off.
                    e.EndFlight(out_ * 2f);
                    e.Stagger(s.bolasBump);
                    Stagger(s.bolasBump);
                    BolasOver((me + there) * 0.5f, 3, "뒷목 치기를 놓침 — 둘이 부딪혀 휘청");
                }
                return;
            }
            if (planTime >= s.bolasPull)
            {
                e.EndFlight();
                BolasOver(there, 1, "다 끌어오기 전에 밧줄이 풀림");
            }
        }

        /// <summary>The bolas is done with: dropped, let go, the chop, or the bump (S3BolasEnd's count).</summary>
        void BolasOver(Vector3 at, int how, string why)
        {
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            planNote = 0;
            planPoint = at;
            Fx(SkillFxKind.S3BolasEnd, planTarget, at, planDir, how);
            if (how == 0) FeelMiss(at, planDir);
            Log($"앙파상: {why}");
        }

        // ---------------------------------------------------------------- 나이트: 하이점프 & 내려찍기

        Vector3 hopFrom;
        float hopT, skillAirSteer;

        /// <summary>The share of the run's speed it may still steer by in the air now (RagdollPawn.cs, a launch's steer): the
        /// knight's high jump, hopSteer.</summary>
        float SkillAirSteer => skillAirSteer;

        void UpdateHighStomp(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    float g = Mathf.Max(0.01f, -Physics.gravity.y);
                    // (The ragdoll loses a little on the way up: the leap's own lift put back.)
                    float up = Mathf.Sqrt(2f * g * Mathf.Max(0.2f, s.hopHeight)) * s.leapLift;
                    Launch(Flat(bodies[0].linearVelocity) + Vector3.up * up);
                    skillAirSteer = s.hopSteer;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    planTarget = null;
                    planNote = 0;
                    planDir = AimFlat();
                    planPoint = FeetPoint;
                    hopT = 0f;
                    skillHitSet.Clear();
                    SkillDetail = "하이점프 (공중에서 F = 내려찍기)";
                    Fx(SkillFxKind.KnightLeap, null, FeetPoint, planDir, 0);
                    Log($"{DisplayName}: 하이점프 ({s.hopHeight:0.#} m)");
                    break;
                }
                case PlanPhase.Active:
                    planTime += dt;
                    if ((planNote & HopDiveBit) != 0)
                    {
                        HopDiveStep(s, dt);
                        break;
                    }
                    if ((planNote & (HopDropBit | HopStompBit)) == 0)
                    {
                        planTarget = HopMark(s);   // the horseshoe, kept up to date as it flies
                        if (press && planTime >= 0.1f)
                        {
                            if (planTarget != null) BeginHopDive(s);
                            else BeginHopDrop(s);
                            break;
                        }
                    }
                    if (planTime > 0.25f && Grounded) HopLand();
                    else if (planTime > 4f || Climbing || rope != null)
                    {
                        EndPlanSkill();
                        StartCooldown(s.hopCooldown);
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.hopRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.hopCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The enemy the horseshoe marks: within hopMarkReach across the floor and not above the knight, the one
        /// nearest where the camera's line meets the world.</summary>
        RagdollPawn HopMark(PawnRushSkillParams s)
        {
            Vector3 eye = bodies[(int)BodyId.Head].position, point = AimPoint(s.hopMarkReach);
            RagdollPawn best = null;
            float bestD = float.MaxValue;
            foreach (var e in All)
            {
                if (!UltFoe(e)) continue;
                Vector3 head = e.bodies[(int)BodyId.Head].position;
                if (Flat(head - eye).magnitude > s.hopMarkReach || head.y > eye.y + 0.3f) continue;
                float d = Flat(e.FeetPoint - point).magnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        void BeginHopDive(PawnRushSkillParams s)
        {
            planNote |= HopDiveBit;
            hopT = 0f;
            hopFrom = bodies[0].position;
            skillAirSteer = 0f;
            SkillDetail = $"내려찍기 → {planTarget.DisplayName}";
            Fx(SkillFxKind.S3HopDive, planTarget, hopFrom, FlatDir(planTarget.bodies[0].position - hopFrom));
            Log($"하이점프 → {planTarget.DisplayName}: 내려꽂힘");
        }

        void HopDiveStep(PawnRushSkillParams s, float dt)
        {
            var e = planTarget;
            if (e == null || e.State != PawnState.Active || e.SkillUntargetable)
            {
                // It got away mid-dive: the rest of the way straight down.
                EndFlight();
                planNote = (byte)(planNote & ~HopDiveBit);
                BeginHopDrop(s);
                return;
            }
            hopT += dt;
            float u = Mathf.Clamp01(hopT / Mathf.Max(0.05f, s.hopDiveTime));
            var headBody = e.bodies[(int)BodyId.Head];
            var headCollider = headBody.GetComponent<Collider>();
            Vector3 head = headBody.position;
            float top = headCollider != null ? headCollider.bounds.max.y : head.y + 0.12f;
            Vector3 onHead = new Vector3(head.x, top + standHeight, head.z);   // (its hips with its feet on the head)
            Fly(Vector3.Lerp(hopFrom, onHead, u * u), (onHead - hopFrom) / Mathf.Max(0.05f, s.hopDiveTime), 0f, default, 30f);
            Face(onHead - hopFrom);
            if (u >= 1f || (bodies[0].position - onHead).magnitude < 0.3f) HopStomp(s, e, new Vector3(head.x, top, head.z));
        }

        void HopStomp(PawnRushSkillParams s, RagdollPawn e, Vector3 at)
        {
            Vector3 dir = FlatDir(at - hopFrom);
            string result = SkillHit(e, dir * 1.5f + Vector3.down * 2f, true, "하이점프 내려찍기", false, s.hopKnockHold);
            Fx(SkillFxKind.KnightStomp, e, at, dir);
            FeelHit(e, at, dir, FeelHard);
            EndFlight();
            float g = Mathf.Max(0.01f, -Physics.gravity.y);
            Launch(Flat(bodies[0].linearVelocity) * 0.3f + Vector3.up * Mathf.Sqrt(2f * g * Mathf.Max(0.1f, s.hopBounce)));
            planNote = (byte)((planNote & ~HopDiveBit) | HopStompBit);
            planTarget = null;
            planTime = 0f;   // (the landing is looked for again after a moment)
            SkillDetail = "머리를 밟고 튀어 오름";
            Log($"하이점프 → {e.DisplayName}: 머리 밟기 → {result}");
        }

        void BeginHopDrop(PawnRushSkillParams s)
        {
            planNote |= HopDropBit;
            planTarget = null;
            skillAirSteer = 0f;
            Launch(Flat(bodies[0].linearVelocity) * 0.5f + Vector3.down * s.hopDropSpeed);
            Fx(SkillFxKind.S3HopDive, null, bodies[0].position, planDir);
            SkillDetail = "곧장 내려옴";
        }

        void HopLand()
        {
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            planTarget = null;
            skillAirSteer = 0f;
            planPoint = FeetPoint;
            SkillDetail = "착지";
            Fx(SkillFxKind.KnightLand, null, FeetPoint, planDir, 0, 0.5f);
        }

        // ---------------------------------------------------------------- 룩: 룩앤롤 (the normal one)

        // Its own column (the driven ultimate's way, RagdollPawn.RookRoll.cs, with its own numbers).
        bool rsRolling, rsAir;
        float rsSpeed, rsVy, rsStrikeAt;
        int rsStrikeRun;
        Vector3 rsCenter;

        /// <summary>The short roll is rolling now (any PC: the effects draw the column).</summary>
        public bool RollingShort => PlanSkill == PawnRushSkillIds.RollShort && planPhase == PlanPhase.Active && (planNote & RollingBit) != 0;

        void UpdateRollShort(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    DropHook();
                    DropRope();
                    EndClimbPose();
                    LetGo(0.3f);
                    rsRolling = true;
                    rsAir = false;
                    rsVy = 0f;
                    rsSpeed = s.rollSpeed0;
                    rsStrikeRun = 0;
                    rsStrikeAt = -10f;
                    skillHitSet.Clear();
                    planDir = MoveOrAim();
                    rsCenter = (DriveFloor(FeetPoint, 1f, 4f) ?? FeetPoint) + Vector3.up * s.rollRadius;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    planTarget = null;
                    planNote = RollingBit;
                    planPoint = FeetPoint;
                    SkillDetail = "룩앤롤 (구르는 중 F = 쿵 일어서기)";
                    Fx(SkillFxKind.S3Roll, null, FeetPoint, planDir, 0, s.rollRadius);
                    Log($"{DisplayName}: 룩앤롤 — 쓰러져 굴러감");
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    if (press && planTime >= 0.15f && !rsAir)
                    {
                        RollStand(s, 1);
                        break;
                    }
                    if (!RollStep(s, dt)) break;
                    if (planTime >= s.rollSeconds && !rsAir) RollStand(s, 0);
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.3f)
                    {
                        EndPlanSkill();
                        StartCooldown(s.rollCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>One step of the column; false if it stopped (a wall, too high a step).</summary>
        bool RollStep(PawnRushSkillParams s, float dt)
        {
            grabImmune = Mathf.Max(grabImmune, 0.25f);
            // Heavy: the move keys turn it a little only.
            Vector3 keys = Flat(skillMoveRaw), want = keys.sqrMagnitude > 0.04f ? keys.normalized : planDir;
            if (!rsAir) planDir = FlatDir(Vector3.RotateTowards(planDir, want, s.rollTurn * Mathf.Deg2Rad * dt, 0f));
            rsSpeed = Mathf.Lerp(s.rollSpeed0, s.rollSpeed1, Mathf.Clamp01(planTime / Mathf.Max(0.1f, s.rollSeconds)));
            float go = rsSpeed * dt;
            Vector3 move = planDir * go;
            if (RollBlocked(rsCenter, planDir, go + 0.2f, s.rollRadius, s.rollLength, out var hit))
            {
                var kitWall = hit.collider.GetComponentInParent<PawnRushKitWall>();
                var barricade = hit.collider.GetComponentInParent<SkillBarricade>();
                if (kitWall != null) kitWall.Break();
                else if (barricade != null) barricade.Break(s.rrRegrow);
                else
                {
                    // A wall: it stops there, dizzy.
                    Fx(SkillFxKind.S3RollEnd, null, hit.point, planDir, 2);
                    RollStand(s, 2);
                    Stagger(s.rollDizzy);
                    Log($"룩앤롤: 벽에 박음 → 어질 {s.rollDizzy:0.#}초");
                    return false;
                }
            }
            Vector3 center = rsCenter + move;
            var floor = DriveFloor(center, s.rollRadius, 30f);
            float rest = floor.HasValue ? floor.Value.y + s.rollRadius : float.NegativeInfinity;
            if (!rsAir && floor.HasValue && rest - center.y > s.rollRadius * 0.7f)
            {
                // Too high a step for it: as a wall.
                RollStand(s, 2);
                Stagger(s.rollDizzy);
                return false;
            }
            if (rsAir)
            {
                rsVy -= s.rrGravity * dt;
                center.y += rsVy * dt;
                if (rsVy <= 0f && center.y <= rest)
                {
                    center.y = rest;
                    rsAir = false;
                    rsVy = 0f;
                }
            }
            else if (center.y - rest > 0.8f)
            {
                rsAir = true;   // off an edge: it falls with it
                rsVy = 0f;
            }
            else if (floor.HasValue) center.y = rest;
            Fly(center, (center - bodies[0].position) / Mathf.Max(1e-4f, dt), 0f, default, 22f);
            rsCenter = center;
            RollHits(s);
            return true;
        }

        /// <summary>The first solid thing (not a piece) the column meets (its middle and both ends).</summary>
        static bool RollBlocked(Vector3 center, Vector3 way, float length, float radius, float span, out RaycastHit first)
        {
            first = default;
            float best = float.MaxValue;
            Vector3 right = new Vector3(way.z, 0f, -way.x);
            for (int i = -1; i <= 1; i++)
            {
                Vector3 from = center + right * (i * span * 0.4f) + Vector3.up * 0.15f;
                foreach (var h in Physics.SphereCastAll(from, radius * 0.6f, way, length, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h.distance >= best || h.distance <= 0f || h.collider.GetComponentInParent<RagdollPawn>() != null) continue;
                    if (h.normal.y >= 0.6f) continue;   // a floor or a slope, not a wall
                    if (h.rigidbody != null && !h.rigidbody.isKinematic) continue;
                    best = h.distance;
                    first = h;
                }
            }
            return best < float.MaxValue;
        }

        /// <summary>The enemies it rolls into: pins.</summary>
        void RollHits(PawnRushSkillParams s)
        {
            Vector3 right = new Vector3(planDir.z, 0f, -planDir.x);
            float half = s.rollLength * 0.5f, r = s.rollRadius;
            foreach (var e in All)
            {
                if (!UltFoe(e) || skillHitSet.Contains(e)) continue;
                Vector3 p = e.bodies[0].position, local = p - rsCenter;
                float along = Vector3.Dot(local, planDir), across = Vector3.Dot(local, right), up = local.y;
                if (Mathf.Abs(across) > half + 0.3f || Mathf.Abs(along) > r + 0.35f || up < -r - 0.5f || up > r + 0.5f) continue;
                skillHitSet.Add(e);
                float side = across >= 0f ? 1f : -1f;
                Vector3 away = FlatDir(planDir + right * side * Random.Range(0.4f, 1f));
                string result = SkillHit(e, S3Push(away, s.rollThrow), true, "룩앤롤", false, s.rollKnock);
                Fx(SkillFxKind.S3Hit, e, e.LassoBelly, away, skillHitSet.Count);
                FeelHit(e, p, planDir, FeelHard, skillHitSet.Count, true);
                Log($"룩앤롤 → {e.DisplayName}: 볼링 핀 → {result}");
                if (Time.time - rsStrikeAt > 0.6f) rsStrikeRun = 0;
                rsStrikeAt = Time.time;
                rsStrikeRun++;
                if (rsStrikeRun == s.rollStrike)
                {
                    PopOver(this, PopStrike);
                    Log($"룩앤롤: 스트라이크! ({rsStrikeRun})");
                }
            }
        }

        /// <summary>It stands up: at the end (0), with the thump (1, the key again), against a wall (2).</summary>
        void RollStand(PawnRushSkillParams s, int how)
        {
            if (!rsRolling) return;
            rsRolling = false;
            rsAir = false;
            EndFlight(how == 2 ? Vector3.zero : planDir * Mathf.Min(rsSpeed, 4f));
            KeepSkillLandingSafe();
            planNote = 0;
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            planPoint = FeetPoint;
            SkillDetail = how == 1 ? "쿵!" : "일어섬";
            if (how == 1) RollSlam(s);
            if (how != 2) Fx(SkillFxKind.S3RollEnd, null, FeetPoint, planDir, how);
        }

        void RollSlam(PawnRushSkillParams s)
        {
            Vector3 c = FeetPoint;
            int hit = 0;
            foreach (var e in All)
            {
                if (!UltFoe(e)) continue;
                Vector3 d = e.bodies[0].position - c;
                if (Flat(d).magnitude > s.rollSlamRadius || Mathf.Abs(d.y) > 1.5f) continue;
                hit++;
                string result = SkillHit(e, S3Push(Flat(d), s.rollSlamPush), true, "룩앤롤 쿵", false, s.rollSlamHold);
                Fx(SkillFxKind.S3Hit, e, e.LassoBelly, FlatDir(d), hit);
                Log($"룩앤롤 쿵 → {e.DisplayName}: {result}");
            }
            Fx(SkillFxKind.RookSlam, null, c, planDir, hit, 1f);
            if (hit > 0) FeelHit(null, c, planDir, FeelHard, hit, false);
            else FeelMiss(c, planDir);
        }

        // ---------------------------------------------------------------- 룩: 탑 가두기

        Vector3 crackFrom, crackTip;
        float crackOut;
        // A piece shut in a tower (the host's): its time, the rook that did it, the tower's collider.
        float trapLeft;
        RagdollPawn trapBy;
        GameObject trapTower;

        /// <summary>Shut in a rook's tower now (the host's).</summary>
        public bool Trapped => trapLeft > 0f;

        void UpdateTowerTrap(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planDir = AimFlat();
                    facing = planDir;
                    crackFrom = DriveFloor(FeetPoint + planDir * 0.4f, 0.6f, 2f) ?? FeetPoint;
                    crackTip = crackFrom;
                    crackOut = 0f;
                    planPoint = crackFrom;
                    planTarget = null;
                    planNote = 0;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    HaltDash();
                    SkillDetail = "탑 가두기 (땅이 갈라짐)";
                    Fx(SkillFxKind.RookSlam, null, FeetPoint, planDir, 0, 1f);
                    Fx(SkillFxKind.S3Crack, null, crackFrom, planDir, 0, s.crackSpeed);
                    Log($"{DisplayName}: 탑 가두기 — 땅을 가름");
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    CrackStep(s, dt);
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.3f)
                    {
                        EndPlanSkill();
                        StartCooldown(s.towerCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void CrackStep(PawnRushSkillParams s, float dt)
        {
            float step = s.crackSpeed * dt;
            bool last = crackOut + step >= s.crackRange;
            if (last) step = Mathf.Max(0f, s.crackRange - crackOut);
            var floor = DriveFloor(crackTip + planDir * step, 0.8f, 1.2f);
            // A wall ahead (knee high) or no floor for the next bit: it ends there.
            if (!floor.HasValue || SolidRay(crackTip + Vector3.up * 0.3f, planDir, step + 0.1f, out _))
            {
                CrackEnd(s, null);
                return;
            }
            // The first enemy on it, on its feet (a jump clears it).
            Vector3 right = new Vector3(planDir.z, 0f, -planDir.x);
            RagdollPawn first = null;
            float firstAlong = float.MaxValue;
            foreach (var e in All)
            {
                if (!UltFoe(e)) continue;
                Vector3 feet = e.FeetPoint, d = Flat(feet - crackTip);
                float along = Vector3.Dot(d, planDir);
                if (along < -0.3f || along > step + 0.3f || Mathf.Abs(Vector3.Dot(d, right)) > s.crackWidth * 0.5f + 0.3f) continue;
                if (feet.y - Mathf.Max(crackTip.y, floor.Value.y) > 0.4f) continue;
                if (along < firstAlong)
                {
                    firstAlong = along;
                    first = e;
                }
            }
            if (first != null)
            {
                float to = Mathf.Clamp(firstAlong, 0f, step);
                crackTip += planDir * to;
                crackOut += to;
                planPoint = crackTip;
                CrackEnd(s, first);
                return;
            }
            crackTip = floor.Value;
            crackOut += step;
            planPoint = crackTip;
            if (last) CrackEnd(s, null);
        }

        void CrackEnd(PawnRushSkillParams s, RagdollPawn caught)
        {
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            Fx(SkillFxKind.S3Crack, caught, crackTip, planDir, 1, crackOut);
            if (caught == null)
            {
                FeelMiss(crackTip, planDir);
                Log($"탑 가두기: 아무도 안 걸림 ({crackOut:0.#} m)");
                return;
            }
            planTarget = caught;
            string result = SkillBind(caught, s.towerSeconds, "탑 가두기");
            if (!caught.SkillBound)
            {
                Log($"탑 가두기 → {caught.DisplayName}: {result}");
                return;
            }
            caught.trapLeft = s.towerSeconds;
            caught.trapBy = this;
            caught.MakeTower(s);
            PopOver(caught, PopTrap);
            Fx(SkillFxKind.S3Tower, caught, caught.FeetPoint, planDir, 0, s.towerSeconds);
            FeelHit(caught, caught.FeetPoint, planDir, FeelHard);
            Log($"탑 가두기 → {caught.DisplayName}: 돌탑에 {s.towerSeconds:0.#}초 갇힘");
        }

        /// <summary>The tower round this piece: solid to everyone else (a capsule up to its neck), not to it.</summary>
        void MakeTower(PawnRushSkillParams s)
        {
            if (trapTower != null) Destroy(trapTower);
            trapTower = new GameObject("Rook tower (skill)");
            trapTower.transform.position = FeetPoint;
            var capsule = trapTower.AddComponent<CapsuleCollider>();
            float tall = Mathf.Max(0.6f, bodies[(int)BodyId.Head].position.y - FeetPoint.y);
            capsule.radius = s.towerWide * 0.5f;
            capsule.height = Mathf.Max(tall, s.towerWide);
            capsule.center = Vector3.up * capsule.height * 0.5f;
            IgnoreBody(capsule, true);
        }

        /// <summary>Its time is up (or it was floored inside): the tower crumbles and it rolls out of the ruin, down a while.</summary>
        void TowerFall(PawnRushSkillParams s)
        {
            trapLeft = 0f;
            if (trapTower != null)
            {
                Destroy(trapTower);
                trapTower = null;
            }
            bindLeft = 0f;   // (the bind goes with the tower)
            var by = trapBy != null ? trapBy : this;
            trapBy = null;
            Vector3 away = by != this ? FlatDir(bodies[0].position - by.bodies[0].position) : FlatDir(facing);
            Fx(SkillFxKind.S3TowerFall, this, FeetPoint, away);
            if (State != PawnState.Active) return;
            string result = by.SkillHit(this, S3Push(away, s.towerRollOut), true, "탑 가두기", false, s.towerKnockHold);
            Log($"{DisplayName}: 탑이 무너져 굴러 나옴 → {result}");
        }

        // ---------------------------------------------------------------- 퀸: 여왕의 위상

        /// <summary>Blade <paramref name="k"/> (0..7) of the queen's star as it lies (any PC: planDir is blade 0).</summary>
        public Vector3 StarBlade(int k) => Quaternion.AngleAxis(45f * k, Vector3.up) * FlatDir(planDir);

        void UpdateQueenStar(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planDir = AimFlat();
                    planPoint = FeetPoint;
                    planTarget = null;
                    planNote = 0;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    SkillDetail = "여왕의 위상";
                    Fx(SkillFxKind.S3Star, null, FeetPoint, planDir, 0, s.starRadius);
                    Log($"{DisplayName}: 여왕의 위상 — 팔각별");
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    planPoint = FeetPoint;   // (it follows her)
                    if (planTime >= s.starFuse) StarBlast(s);
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    planPoint = FeetPoint;
                    if (planTime >= s.starRecovery + 0.3f)
                    {
                        EndPlanSkill();
                        StartCooldown(s.starCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The star lights: for each enemy in it, the blade nearest it throws it along that blade.</summary>
        void StarBlast(PawnRushSkillParams s)
        {
            Vector3 c = FeetPoint;
            int hit = 0;
            byte lit = 0;
            RagdollPawn first = null;
            foreach (var e in All)
            {
                if (!UltFoe(e)) continue;
                Vector3 d = e.bodies[0].position - c;
                if (Flat(d).magnitude > s.starRadius || Mathf.Abs(d.y) > 2f) continue;
                float angle = Vector3.SignedAngle(FlatDir(planDir), FlatDir(d), Vector3.up);
                int k = ((Mathf.RoundToInt(angle / 45f) % 8) + 8) % 8;
                Vector3 blade = StarBlade(k);
                lit |= (byte)(1 << k);
                hit++;
                if (first == null) first = e;
                string result = SkillHit(e, S3Push(blade, s.starPush), true, "여왕의 위상", false, s.starKnockHold);
                Fx(SkillFxKind.S3StarBlade, e, c, blade, k);
                Fx(SkillFxKind.S3Hit, e, e.LassoBelly, blade, hit);
                Log($"여왕의 위상 → {e.DisplayName}: 날 {k} → {result}");
            }
            planNote = lit;
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            if (hit == 0)
            {
                Fx(SkillFxKind.S3StarBlade, null, c, planDir, 255);
                FeelMiss(c, planDir);
                Log("여왕의 위상: 별 안에 적 없음");
                return;
            }
            FeelHit(first, c, planDir, FeelHard, hit, true);
        }

        // ---------------------------------------------------------------- 퀸: 연속 체크 (3판)

        int ccDone;
        Vector3 ccFrom;
        float ccT;

        /// <summary>The checks given so far in this chain (any PC).</summary>
        public int CheckChainDone => PlanSkill == PawnRushSkillIds.CheckChain ? planNote >> 4 : 0;

        void UpdateCheckChain(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    skillHitSet.Clear();
                    var t = CcTarget(s, s.ccFirst, true);
                    if (t == null)
                    {
                        // Nothing to check: the key is not spent (no cooldown).
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, AimFlat(), (int)piece);
                        Log($"연속 체크: 앞 {s.ccFirst:0} m에 표적 없음 (쿨 안 씀)");
                        break;
                    }
                    SkillUses++;
                    ccDone = 0;
                    CcMark(t);
                    Log($"{DisplayName}: 연속 체크 — {t.DisplayName}");
                    break;
                }
                case PlanPhase.Windup:
                    planTime += dt;
                    if (!UltFoe(planTarget))
                    {
                        CcStop(s);
                        break;
                    }
                    planPoint = planTarget.FeetPoint;
                    planDir = FlatDir(planTarget.bodies[0].position - bodies[0].position);
                    if (planTime >= s.ccMark) CcFly();
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    CcFlyStep(s, dt);
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (press && ccDone < s.ccMax)
                    {
                        var t = CcTarget(s, s.ccNext, false);
                        if (t != null)
                        {
                            CcMark(t);
                            break;
                        }
                    }
                    if (planTime >= s.ccWait || ccDone >= s.ccMax) CcStop(s);
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The enemy to check: within <paramref name="reach"/> (ahead of her for the first), not checked already,
        /// nearest where the camera's line meets the world.</summary>
        RagdollPawn CcTarget(PawnRushSkillParams s, float reach, bool ahead)
        {
            Vector3 from = bodies[0].position, point = AimPoint(reach);
            RagdollPawn best = null;
            float bestD = float.MaxValue;
            foreach (var e in All)
            {
                if (!UltFoe(e) || skillHitSet.Contains(e)) continue;
                Vector3 d = e.bodies[0].position - from;
                if (Flat(d).magnitude > reach || Mathf.Abs(d.y) > 3f) continue;
                if (ahead && Vector3.Angle(AimFlat(), Flat(d)) > s.ccCone) continue;
                float score = Flat(e.FeetPoint - point).magnitude;
                if (score < bestD)
                {
                    bestD = score;
                    best = e;
                }
            }
            return best;
        }

        void CcMark(RagdollPawn t)
        {
            planTarget = t;
            planPoint = t.FeetPoint;
            planDir = FlatDir(t.bodies[0].position - bodies[0].position);
            planPhase = PlanPhase.Windup;
            planTime = 0f;
            planNote = (byte)(ccDone << 4);
            SkillDetail = $"체크 → {t.DisplayName}";
        }

        void CcFly()
        {
            var t = planTarget;
            Vector3 from = Chest, d = t.Chest - from;
            float along = d.magnitude;
            // A piece of its side standing between takes the check instead (a check is blocked by a piece put between).
            var between = along > 0.5f ? FirstOnLine(from, d / along, ref along, 0.5f, false) : null;
            if (between != null && between != t && IsEnemyOf(between) && !skillHitSet.Contains(between)) planTarget = t = between;
            ccFrom = bodies[0].position;
            ccT = 0f;
            planPhase = PlanPhase.Active;
            planTime = 0f;
            planNote = (byte)((ccDone << 4) | CcFlyBit);
            planDir = FlatDir(t.bodies[0].position - ccFrom);
        }

        void CcFlyStep(PawnRushSkillParams s, float dt)
        {
            var t = planTarget;
            if (t == null || t.State != PawnState.Active)
            {
                EndFlight();
                CcWaitNext(s);
                return;
            }
            ccT += dt;
            float u = Mathf.Clamp01(ccT / Mathf.Max(0.05f, s.ccFly));
            Vector3 there = t.bodies[0].position, goal = there - FlatDir(there - ccFrom) * 0.6f;
            Fly(Vector3.Lerp(ccFrom, goal, u), (goal - ccFrom) / Mathf.Max(0.05f, s.ccFly), 0f, default, 30f);
            Face(goal - ccFrom);
            if (u >= 1f || Flat(there - bodies[0].position).magnitude < 0.75f) CcStrike(s, t);
        }

        void CcStrike(PawnRushSkillParams s, RagdollPawn t)
        {
            ccDone++;
            skillHitSet.Add(t);
            Vector3 dir = FlatDir(t.bodies[0].position - ccFrom);
            float metres = ccDone >= s.ccMax ? s.ccLast : s.ccPush;
            string result = SkillHit(t, S3Push(dir, metres), true, "연속 체크", false, s.ccKnockHold);
            EndFlight(dir * 2f);
            Fx(SkillFxKind.S3Check, t, t.Chest, dir, ccDone);
            Fx(SkillFxKind.S3Hit, t, t.LassoBelly, dir, ccDone);
            PopOver(t, PopCheckWord);
            FeelHit(t, t.LassoBelly, dir, FeelHard);
            Log($"연속 체크 {ccDone} → {t.DisplayName}: {metres:0} m → {result}");
            CcWaitNext(s);
        }

        void CcWaitNext(PawnRushSkillParams s)
        {
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            planTarget = null;
            planNote = (byte)(ccDone << 4);
            SkillDetail = ccDone < s.ccMax ? $"체크 {ccDone} · 1초 안에 E = 다음 체크" : "체크 끝";
        }

        void CcStop(PawnRushSkillParams s)
        {
            EndFlight();
            EndPlanSkill();
            StartCooldown(s.ccCooldown);   // (from the last check: the chain was started)
        }

        // ---------------------------------------------------------------- 비숍: 성검 소환

        sealed class HolySword
        {
            public int id;
            public RagdollPawn foe;
            public Vector3 at, dir, to;
            public bool flying;
            public float flown, length;
        }

        readonly List<HolySword> swords = new List<HolySword>();
        int swordRound, swordNext;
        float swordT, swordGap = -1f;
        bool swordVolley;

        void UpdateHolySwords(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    planTarget = null;
                    planNote = 0;
                    planDir = AimFlat();
                    planPoint = FeetPoint;
                    SkillDetail = "성검 소환";
                    swords.Clear();
                    swordRound = 0;
                    swordGap = -1f;
                    swordVolley = true;
                    SwordRound(s);
                    Fx(SkillFxKind.S3Star, null, FeetPoint, planDir, 1, 1.5f);   // (count 1: the bishop's small sign of the cast)
                    Log($"{DisplayName}: 성검 소환 ({s.swordRounds}번)");
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    if (planTime >= 0.3f)
                    {
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.1f)
                    {
                        EndPlanSkill();
                        StartCooldown(s.swordCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>A sword beside every enemy: one of the four diagonals, turning from round to round and piece to piece.</summary>
        void SwordRound(PawnRushSkillParams s)
        {
            swordRound++;
            swordT = 0f;
            int i = 0;
            foreach (var e in All)
            {
                if (e == null || e == this || !IsEnemyOf(e) || e.SkillUntargetable || e.Floating) continue;
                int k = (swordRound + i++) % 4;
                Vector3 diag = new Vector3(k == 0 || k == 3 ? 1f : -1f, 0f, k < 2 ? 1f : -1f).normalized;
                Vector3 foot = e.FeetPoint, at = foot + diag * s.swordSide;
                var floor = DriveFloor(at, 2f, 6f);
                at = (floor ?? new Vector3(at.x, foot.y, at.z)) + Vector3.up * s.swordHigh;
                var sword = new HolySword { id = swordNext++ & 255, foe = e, at = at, dir = -diag };
                swords.Add(sword);
                Fx(SkillFxKind.S3SwordRise, e, at, -diag, sword.id, s.swordFuse);
            }
        }

        /// <summary>Every step while a volley lasts (the bishop's): each sword waits its fuse, then flies straight at where its
        /// enemy was; whoever of the other side it meets is bound; a wall stops it. Then a rest, and the next round.</summary>
        void SwordsStep(PawnRushSkillParams s, float dt)
        {
            swordT += dt;
            for (int i = swords.Count - 1; i >= 0; i--)
            {
                var sw = swords[i];
                if (!sw.flying)
                {
                    if (swordT < s.swordFuse) continue;
                    Vector3 to = sw.foe != null ? sw.foe.Chest : sw.at + sw.dir * s.swordSide;
                    Vector3 d = to - sw.at;
                    if (d.sqrMagnitude > 1e-4f) sw.dir = d.normalized;
                    sw.length = d.magnitude + 2f;
                    sw.to = sw.at + sw.dir * sw.length;
                    sw.flying = true;
                    Fx(SkillFxKind.S3SwordFly, sw.foe, sw.at, sw.dir, sw.id, s.swordSpeed, sw.to);
                    continue;
                }
                float step = s.swordSpeed * dt;
                Vector3 from = sw.at + sw.dir * sw.flown;
                sw.flown = Mathf.Min(sw.length, sw.flown + step);
                Vector3 tip = sw.at + sw.dir * sw.flown;
                RagdollPawn hit = null;
                foreach (var e in All)
                {
                    if (e == null || e == this || !IsEnemyOf(e) || e.State != PawnState.Active || e.SkillUntargetable) continue;
                    if (SegmentDistance(e.bodies[0].position + Vector3.up * 0.1f, from, tip) <= s.swordReach)
                    {
                        hit = e;
                        break;
                    }
                }
                if (hit != null)
                {
                    string result = SkillBind(hit, s.swordBind, "성검");
                    Fx(SkillFxKind.S3SwordEnd, hit, hit.Chest, sw.dir, sw.id);
                    if (hit.SkillBound)
                    {
                        Fx(SkillFxKind.S3Bound, hit, hit.FeetPoint, sw.dir, 0, s.swordBind);
                        PopOver(hit, PopBind);
                    }
                    FeelHit(hit, hit.Chest, sw.dir, FeelWeak);
                    Log($"성검 → {hit.DisplayName}: {result}");
                    swords.RemoveAt(i);
                    continue;
                }
                if (SolidRay(from, sw.dir, (tip - from).magnitude + 0.05f, out var wall))
                {
                    Fx(SkillFxKind.S3SwordEnd, null, wall.point, sw.dir, sw.id);
                    swords.RemoveAt(i);
                    continue;
                }
                if (sw.flown >= sw.length)
                {
                    Fx(SkillFxKind.S3SwordEnd, null, tip, sw.dir, sw.id);
                    swords.RemoveAt(i);
                }
            }
            if (swords.Count > 0) return;
            // The round is over: a rest, then the next (or the volley is done).
            if (swordGap < 0f) swordGap = s.swordRest;
            swordGap -= dt;
            if (swordGap > 0f) return;
            swordGap = -1f;
            if (swordRound < s.swordRounds) SwordRound(s);
            else swordVolley = false;
        }

        // ---------------------------------------------------------------- 비숍: 돌멩이 던지기

        sealed class FlyingStone
        {
            public int id;
            public Vector3 pos, vel;
            public float flown;
            public readonly HashSet<RagdollPawn> hit = new HashSet<RagdollPawn>();
        }

        readonly List<FlyingStone> stones = new List<FlyingStone>();
        int stoneNext;
        Vector3 stoneHoverAt;

        void UpdateStoneThrow(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    stoneHoverAt = bodies[0].position;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    planTarget = null;
                    planNote = 0;
                    planDir = AimFlat();
                    planPoint = FeetPoint;
                    SkillDetail = "떠올라 겨눔 (카메라로 조준)";
                    Log($"{DisplayName}: 돌멩이 — 떠올라 겨눔");
                    break;
                case PlanPhase.Windup:
                {
                    planTime += dt;
                    float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(planTime / 0.2f));
                    Fly(stoneHoverAt + Vector3.up * s.stoneRise * rise, Vector3.zero, 0f, default, 20f);
                    planDir = AimFlat();
                    facing = planDir;
                    if (planTime >= s.stoneHover) StoneShoot(s);
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.6f || (planTime > 0.15f && Grounded))
                    {
                        EndPlanSkill();
                        StartCooldown(s.stoneCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void StoneShoot(PawnRushSkillParams s)
        {
            Vector3 dir = Pitched(AimRay(), 25f);
            Vector3 start = Chest + dir * (s.stoneSize * 0.5f + 0.3f);
            var stone = new FlyingStone { id = stoneNext++ & 255, pos = start, vel = dir * s.stoneSpeed };
            stones.Add(stone);
            EndFlight(Vector3.zero);
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            Fx(SkillFxKind.S3StoneShot, null, start, dir, stone.id, s.stoneSpeed);
            Log($"돌멩이: 쏨 (초속 {s.stoneSpeed:0} m)");
        }

        /// <summary>Every step while a stone flies (the bishop's): it falls a little (and rolls on once it comes down on the
        /// floor), knocks every enemy on its way off it, and breaks on a wall or at the end of its range.</summary>
        void StonesStep(PawnRushSkillParams s, float dt)
        {
            float r = s.stoneSize * 0.5f;
            for (int i = stones.Count - 1; i >= 0; i--)
            {
                var st = stones[i];
                st.vel += Vector3.down * s.stoneDrop * dt;
                Vector3 step = st.vel * dt;
                float len = step.magnitude;
                if (len > 1e-4f && StoneSolid(st.pos, r * 0.8f, step / len, len, out var at))
                {
                    Fx(SkillFxKind.S3StoneBreak, null, at, st.vel.normalized, st.id);
                    stones.RemoveAt(i);
                    continue;
                }
                Vector3 next = st.pos + step;
                // Come down on the floor: it rolls on along it (the effects do the same with their own ground).
                var floor = DriveFloor(next + Vector3.up * r, r + 0.6f, 3f);
                if (floor.HasValue && next.y - r < floor.Value.y)
                {
                    next.y = floor.Value.y + r;
                    st.vel.y = 0f;
                }
                foreach (var e in All)
                {
                    if (!UltFoe(e) || st.hit.Contains(e)) continue;
                    if (SegmentDistance(e.bodies[0].position, st.pos, next) > r + 0.35f) continue;
                    st.hit.Add(e);
                    Vector3 side = Flat(e.bodies[0].position - next);
                    Vector3 away = FlatDir(FlatDir(st.vel) + (side.sqrMagnitude > 1e-4f ? side.normalized * 0.8f : Vector3.zero));
                    string result = SkillHit(e, S3Push(away, s.stonePush), true, "돌멩이", false, s.stoneKnockHold, true);
                    Fx(SkillFxKind.S3Hit, e, e.LassoBelly, away, st.hit.Count);
                    FeelHit(e, e.LassoBelly, away, FeelHard);
                    Log($"돌멩이 → {e.DisplayName}: {result}");
                }
                st.pos = next;
                st.flown += len;
                if (st.flown >= s.stoneRange)
                {
                    Fx(SkillFxKind.S3StoneBreak, null, st.pos, st.vel.normalized, st.id);
                    stones.RemoveAt(i);
                }
            }
        }

        /// <summary>A wall a stone meets (not the floor or a slope it rolls on, not a piece, not a loose box).</summary>
        bool StoneSolid(Vector3 p, float r, Vector3 dir, float len, out Vector3 at)
        {
            at = p;
            int n = Physics.SphereCastNonAlloc(p, r, dir, hits, len, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider == null || h.distance <= 0f || h.distance >= best || ownSet.Contains(h.collider)
                    || ColliderOwner.ContainsKey(h.collider) || PassesThrough(h.collider)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                if (h.normal.y > 0.6f) continue;   // (the floor: it rolls on it)
                best = h.distance;
                at = h.point;
            }
            return best < float.MaxValue;
        }

        // ---------------------------------------------------------------- 비숍: 성수병 던지기

        struct Puddle
        {
            public Vector3 at;
            public float radius, until;
            public int team;
            public RagdollPawn by;
        }

        // The puddles on the course (the host's; every PC draws them from S3Puddle).
        static readonly List<Puddle> puddles = new List<Puddle>();
        Vector3 waterAt;
        float slipReady;

        void UpdateHolyWater(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    Vector3 dir = AimFlat(), from = Chest;
                    facing = dir;
                    planDir = dir;
                    float reach = s.waterRange;
                    if (SolidRay(from, dir, reach, out var wall)) reach = Mathf.Max(1f, wall.distance - 0.3f);
                    Vector3 end = from + dir * reach;
                    waterAt = DriveFloor(end, 2f, 8f) ?? new Vector3(end.x, FeetPoint.y, end.z);
                    planPoint = waterAt;
                    planTarget = null;
                    planNote = 0;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = "성수병";
                    Fx(SkillFxKind.S3WaterThrow, null, from, dir, 0, s.waterFlight, waterAt);
                    Log($"{DisplayName}: 성수병 — {reach:0} m 앞");
                    break;
                }
                case PlanPhase.Active:
                    planTime += dt;
                    if (planTime >= s.waterFlight)
                    {
                        puddles.Add(new Puddle { at = waterAt, radius = s.waterSize * 0.5f, until = Time.time + s.waterSeconds, team = team, by = this });
                        Fx(SkillFxKind.S3Puddle, null, waterAt, planDir, team + 1, s.waterSize * 0.5f);
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.2f)
                    {
                        EndPlanSkill();
                        StartCooldown(s.waterCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>Every step for every piece (the host's): standing in an enemy bishop's puddle, it slips - its feet fly
        /// up, it lies a while and slides on out of it (once, then a moment before it can slip again).</summary>
        void PuddleStep(PawnRushSkillParams s)
        {
            if (puddles.Count == 0) return;
            for (int i = puddles.Count - 1; i >= 0; i--)
                if (Time.time > puddles[i].until) puddles.RemoveAt(i);
            if (State != PawnState.Active || !Grounded || Time.time < slipReady || SkillUntargetable || Floating) return;
            Vector3 feet = FeetPoint;
            foreach (var p in puddles)
            {
                if (p.by == this || !Teams.AreEnemies(team, p.team)) continue;
                if (Flat(feet - p.at).magnitude > p.radius || Mathf.Abs(feet.y - p.at.y) > 0.7f) continue;
                slipReady = Time.time + s.waterKnockHold + 1.5f;
                Vector3 run = Flat(bodies[0].linearVelocity);
                Vector3 way = run.sqrMagnitude > 0.5f ? run.normalized : FlatDir(feet - p.at);
                var by = p.by != null ? p.by : this;
                string result = by.SkillHit(this, way * (s.waterSlide * 1.75f) + Vector3.up * 3.5f, true, "성수 웅덩이", false, s.waterKnockHold, true);
                Fx(SkillFxKind.S3Slip, this, feet, way);
                PopOver(this, PopSlip);
                Log($"{DisplayName}: 성수 웅덩이에서 꽈당 → {result}");
                break;
            }
        }
    }
}
