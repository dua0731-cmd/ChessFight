using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The skills the user wrote (Docs/Skills/PAWN_RUSH_SET4_USER_SKILLS.md, 11-26: "지금 먼저 할 작업은 내가 기획한 스킬들을
    /// 너가 다듬어서 게임에 넣는거"), bundle 1: the pawn's two and the knight's one. Host side; every PC draws them from the
    /// moments (SkillFxKind.JyStep, JyRope, JyStomp; PawnRushSkillFx.SetFourJY.cs). The rule over all of them (2.1, the
    /// user: "너무 이동을 도와줄법한 스킬은 있어선 안됨"): the steps only from the floor and their speed gone when they end; the
    /// rope only catches enemies and the pawn goes on at its own speed after the chop; the knight rises straight up with
    /// no run speed and no steering in the air, and comes down where it rose unless it stomps. Not seen in Unity yet.
    ///
    ///   폰      첫 두걸음+대각선 공격 (초급): a crouch, two steps (3 m, 0.35 s) - an enemy met straight on is pushed a
    ///           little, a downed teammate helped up - then for 1 s the key again headbutts an enemy on a front diagonal
    ///           (25-65° to either side, 2.5 m): it flies a little and falls
    ///           앙파상-밧줄 (중급): the key takes the rope out (again puts it away); the left button held swings it (up to
    ///           1 s), let go throws it (4 m to 10 m, a lob); an enemy it catches is pulled in as the pawn is pulled on,
    ///           and when they meet the pawn chops its neck by itself: 콰당! 1.5 s
    ///   나이트  점프&밟기 (중급): up 3 m where it stands; a horseshoe over every enemy head within 6 m, the one nearest the
    ///           aim lit; the key again dives onto that head: 납작이, and the other enemies within 3 m of it are tied
    ///           (묶임 제자리형 1.5 s); with no marks the key again drops it fast
    /// </summary>
    public partial class RagdollPawn
    {
        // The two steps: the cells' last state told, the headbutt's target and its lunge.
        int jyDiagCells = -1;
        bool jyStepHit;
        const byte JyLungeNote = 1;

        // The rope: held, swinging (how long), thrown (the knot's way), caught.
        bool jyRopeClick, jyRopeHeld, jyRopeSwinging;
        int jyRopeLetGo;   // steps the button has been up while swinging (a throw takes two: a flicker is not one)
        float jyRopeCharge, jyRopeFlight;
        Vector3 jyKnotPos, jyKnotVel;
        const byte JyRopeFlyNote = 1, JyRopePullNote = 2;

        // The knight: where it rose from, the marks, the dive.
        Vector3 jyJumpFrom, jyDiveFrom;
        float jyDiveT;
        RagdollPawn jyLit;
        readonly System.Collections.Generic.List<RagdollPawn> jyMarked = new System.Collections.Generic.List<RagdollPawn>();
        const byte JyUpNote = 1, JyDiveNote = 2, JyDropNote = 4, JyStompedNote = 8;

        /// <summary>The rope is out in the hand (any PC that runs this piece; the marks draw its landing ring).</summary>
        public bool JyRopeOut => PlanSkill == PawnRushSkillIds.RopePassant && planPhase == PlanPhase.Windup;
        /// <summary>Where a throw now would come down (the pawn's own PC draws a cream ring there).</summary>
        public Vector3 JyRopeLanding { get; private set; }
        public float JyRopeChargeShare => Mathf.Clamp01(jyRopeCharge / Mathf.Max(0.05f, PawnRushSkills != null ? PawnRushSkills.jyRopeSpin : 1f));
        /// <summary>The two steps' second-press window is open (the screen says "F 한 번 더").</summary>
        public bool JyDiagOpen => PlanSkill == PawnRushSkillIds.StepsDiag && planPhase == PlanPhase.Recovery;

        // ---------------------------------------------------------------- 폰: 첫 두걸음+대각선 공격

        void UpdateStepsDiag(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;   // (on the floor only)
                    SkillUses++;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    planTarget = null;
                    planNote = 0;
                    planDir = MoveOrAim();
                    skillDir = planDir;
                    facing = planDir;
                    skillHitSet.Clear();
                    jyStepHit = false;
                    jyDiagCells = -1;
                    SkillDetail = "첫 두걸음";
                    Fx(SkillFxKind.JyStep, null, FeetPoint, planDir, 0, s.jyStepCrouch + s.jyStepTime);
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    HoldStill(0.05f);
                    if (planTime < s.jyStepCrouch) break;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    skillDir = planDir;
                    StartDash(s.jyStepDist / Mathf.Max(0.05f, s.jyStepTime), s.jyStepTime);
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    if (planNote == JyLungeNote)
                    {
                        JyLungeStep(s, dt);
                        break;
                    }
                    if (WallAhead(dashBase * dt + 0.25f, out _)) dashLeft = 0f;
                    else
                    {
                        DriveDash(dt);
                        JyStepHits(s);
                        dashLeft -= dt;
                    }
                    if (dashLeft > 0f) break;
                    HaltDash();   // (2.1: the steps' speed is gone when they end: it stands where they took it)
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    jyDiagCells = -1;
                    SkillDetail = "F 한 번 더 = 대각선 공격";
                    Fx(SkillFxKind.JyStep, null, FeetPoint, FlatDir(facing), 1, s.jyDiagWindow);
                    break;
                case PlanPhase.Recovery:
                {
                    planTime += dt;
                    int cells = JyDiagCells(s, out var left, out var right);
                    if (cells != jyDiagCells)
                    {
                        jyDiagCells = cells;
                        Fx(SkillFxKind.JyStep, null, FeetPoint, FlatDir(facing), 10 + cells);
                    }
                    if (press && State == PawnState.Active && cells != 0)
                    {
                        // Both taken: the one nearer the aim.
                        var t = left != null && right != null
                            ? (Vector3.Angle(AimFlat(), Flat(left.bodies[0].position - bodies[0].position)) <= Vector3.Angle(AimFlat(), Flat(right.bodies[0].position - bodies[0].position)) ? left : right)
                            : left ?? right;
                        planTarget = t;
                        planNote = JyLungeNote;
                        planPhase = PlanPhase.Active;
                        planTime = 0f;
                        Vector3 d = Flat(t.bodies[0].position - bodies[0].position);
                        skillDir = d.sqrMagnitude > 0.01f ? d.normalized : FlatDir(facing);
                        StartDash(Mathf.Max(2f, (d.magnitude - 0.3f) / Mathf.Max(0.05f, s.jyDiagLunge)), s.jyDiagLunge + 0.1f);
                        SkillDetail = $"대각선 공격 → {t.DisplayName}";
                        break;
                    }
                    if (planTime < s.jyDiagWindow) break;
                    Fx(SkillFxKind.JyStep, null, FeetPoint, FlatDir(facing), 3);
                    EndPlanSkill();
                    StartCooldown(SetFourCooldown(PawnRushSkillIds.StepsDiag, s));
                    break;
                }
            }
        }

        /// <summary>The two steps meet someone: a downed teammate is helped up; the first enemy straight on is pushed a
        /// little and the steps stop on it.</summary>
        void JyStepHits(PawnRushSkillParams s)
        {
            Vector3 at = bodies[(int)BodyId.Chest].position + skillDir * 0.3f;
            int n = Physics.OverlapSphereNonAlloc(at, s.pawnHitRadius, skillOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!ColliderOwner.TryGetValue(skillOverlap[i], out var other) || other == null || other == this || skillHitSet.Contains(other)) continue;
                if (IsAllyOf(other))
                {
                    if (!other.DownedForHelp) continue;
                    skillHitSet.Add(other);
                    HelpUp(s, other);
                    continue;
                }
                if (jyStepHit || !IsEnemyOf(other) || other.SkillUntargetable) continue;
                skillHitSet.Add(other);
                jyStepHit = true;
                SkillHit(other, skillDir * s.pawnFrontPush, false, "첫 두걸음", false, 0f, true);
                Fx(SkillFxKind.PawnHit, other, SkillContact(other), skillDir, 0);
                dashLeft = Mathf.Min(dashLeft, 0.02f);
            }
        }

        /// <summary>Enemies on the two diagonal cells in front (bit 1 left, 2 right).</summary>
        int JyDiagCells(PawnRushSkillParams s, out RagdollPawn left, out RagdollPawn right)
        {
            left = right = null;
            Vector3 me = bodies[0].position, face = FlatDir(facing);
            float bestL = float.MaxValue, bestR = float.MaxValue;
            foreach (var o in All)
            {
                if (!UltFoe(o) || o.State == PawnState.Ragdoll) continue;
                Vector3 d = o.bodies[0].position - me;
                if (Mathf.Abs(d.y) > 1.2f) continue;
                Vector3 f = Flat(d);
                float dist = f.magnitude;
                if (dist > s.jyDiagReach || dist < 0.2f) continue;
                float angle = Vector3.SignedAngle(face, f, Vector3.up);
                float a = Mathf.Abs(angle);
                if (a < s.jyDiagInner || a > s.jyDiagOuter) continue;
                if (angle < 0f && dist < bestL)
                {
                    bestL = dist;
                    left = o;
                }
                else if (angle > 0f && dist < bestR)
                {
                    bestR = dist;
                    right = o;
                }
            }
            return (left != null ? 1 : 0) | (right != null ? 2 : 0);
        }

        void JyLungeStep(PawnRushSkillParams s, float dt)
        {
            var t = planTarget;
            if (t != null)
            {
                Vector3 d = Flat(t.bodies[0].position - bodies[0].position);
                if (d.sqrMagnitude > 0.01f) skillDir = d.normalized;
                if (d.magnitude < 1.3f || JyTouches(t, Physics.OverlapSphereNonAlloc(Chest + skillDir * 0.35f, s.pawnHitRadius + 0.15f, skillOverlap, ~0, QueryTriggerInteraction.Ignore))
                    || (dashLeft <= dt && d.magnitude < 1.9f))
                {
                    // The headbutt: it flies a little and falls (physics, no hindrance).
                    Vector3 contact = Vector3.Lerp(bodies[(int)BodyId.Head].position, t.bodies[(int)BodyId.Head].position, 0.5f);
                    SkillHit(t, Throw4(skillDir, 1), true, "대각선 공격");
                    Fx(SkillFxKind.JyStep, t, contact, skillDir, 2);
                    FeelHit(t, contact, skillDir, FeelHard);
                    JyEndSteps(s);
                    return;
                }
            }
            DriveDash(dt);
            dashLeft -= dt;
            if (dashLeft <= 0f || t == null) JyEndSteps(s);
        }

        /// <summary>The first <paramref name="n"/> colliders of the last overlap include one of <paramref name="t"/>'s.</summary>
        bool JyTouches(RagdollPawn t, int n)
        {
            for (int i = 0; i < n && i < skillOverlap.Length; i++)
                if (skillOverlap[i] != null && ColliderOwner.TryGetValue(skillOverlap[i], out var o) && o == t) return true;
            return false;
        }

        void JyEndSteps(PawnRushSkillParams s)
        {
            HaltDash();
            EndPlanSkill();
            StartCooldown(SetFourCooldown(PawnRushSkillIds.StepsDiag, s));
        }

        /// <summary>No faster across the floor than its own run (2.1: a skill's speed does not carry into the jump after it).</summary>
        void JyCapToRun()
        {
            Vector3 own = Flat(bodies[0].linearVelocity) - carryVel;
            float top = Mathf.Max(1f, TopSpeed);
            if (own.magnitude > top) AddVelocity(own.normalized * top - own);
        }

        // ---------------------------------------------------------------- 폰: 앙파상-밧줄

        void UpdateRopePassant(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    planTarget = null;
                    planNote = 0;
                    planDir = AimFlat();
                    jyRopeCharge = 0f;
                    jyRopeSwinging = false;
                    jyRopeClick = jyRopeHeld = false;
                    SkillDetail = "밧줄 (좌클릭 꾹 = 돌리기, 떼기 = 던지기, F = 넣기)";
                    Fx(SkillFxKind.JyRope, null, Chest, FlatDir(facing), 0);
                    break;
                case PlanPhase.Windup:
                {
                    // In the hand, walking and jumping as ever.
                    planTime += dt;
                    planDir = AimFlat();
                    JyRopeAimLanding(s);
                    if (State != PawnState.Active || Climbing || Floating)
                    {
                        JyRopeAway(false);
                        break;
                    }
                    if (press && !jyRopeSwinging)
                    {
                        JyRopeAway(true);
                        break;
                    }
                    jyRopeLetGo = jyRopeHeld ? 0 : jyRopeLetGo + 1;
                    if (jyRopeHeld || (jyRopeSwinging && jyRopeLetGo < 2))
                    {
                        if (!jyRopeSwinging)
                        {
                            jyRopeSwinging = true;
                            jyRopeCharge = 0f;
                            Fx(SkillFxKind.JyRope, null, Chest, FlatDir(facing), 2, s.jyRopeSpin);
                        }
                        jyRopeCharge = Mathf.Min(s.jyRopeSpin, jyRopeCharge + dt);
                        SkillDetail = $"돌리는 중 {Mathf.RoundToInt(JyRopeChargeShare * 100f)}% (떼면 던짐)";
                    }
                    else if (jyRopeSwinging) JyRopeThrow(s);
                    break;
                }
                case PlanPhase.Active:
                    planTime += dt;
                    if (planNote == JyRopeFlyNote) JyKnotStep(s, dt);
                    else JyRopePullStep(s, dt);
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.4f) EndPlanSkill();
                    break;
            }
        }

        /// <summary>Put back (the key again, or knocked over while holding it): no cooldown, nothing thrown.</summary>
        void JyRopeAway(bool tell)
        {
            Fx(SkillFxKind.JyRope, null, Chest, FlatDir(facing), 1);
            jyRopeSwinging = false;
            EndPlanSkill();
            SkillDetail = tell ? "밧줄을 넣었어요" : "밧줄을 놓쳤어요";
        }

        /// <summary>How far a throw now would go: the short one at once, the long one after the whole swing.</summary>
        float JyRopeRange(PawnRushSkillParams s) => Mathf.Lerp(s.jyRopeShort, s.jyRopeLong, JyRopeChargeShare);

        /// <summary>The lob's first speed: up at 35° along the aim, so it comes down <paramref name="range"/> ahead on flat
        /// ground (from the hand's height).</summary>
        Vector3 JyRopeLob(float range, out Vector3 hand)
        {
            hand = Chest + Vector3.up * 0.55f;
            float angle = 35f * Mathf.Deg2Rad, g = Gravity4, h = hand.y - FeetPoint.y;
            // range = v cos a * t, 0 = h + v sin a t - g t^2 / 2  ->  v^2 = g R^2 / (2 cos^2 a (R tan a + h))
            float c = Mathf.Cos(angle), v = Mathf.Sqrt(g * range * range / Mathf.Max(0.1f, 2f * c * c * (range * Mathf.Tan(angle) + h)));
            return FlatDir(planDir) * (v * c) + Vector3.up * (v * Mathf.Sin(angle));
        }

        void JyRopeAimLanding(PawnRushSkillParams s)
        {
            Vector3 v = JyRopeLob(JyRopeRange(s), out var hand);
            float t = 0f, g = Gravity4;
            Vector3 p = hand;
            for (int i = 0; i < 40; i++)
            {
                t += 0.05f;
                Vector3 q = hand + v * t + Vector3.down * (0.5f * g * t * t);
                if (Physics.Linecast(p, q, out var hit, ~0, QueryTriggerInteraction.Ignore) && !ColliderOwner.ContainsKey(hit.collider))
                {
                    JyRopeLanding = hit.point;
                    return;
                }
                p = q;
            }
            JyRopeLanding = p;
        }

        void JyRopeThrow(PawnRushSkillParams s)
        {
            jyRopeSwinging = false;
            jyKnotVel = JyRopeLob(JyRopeRange(s), out var hand);
            jyKnotPos = hand;
            jyRopeFlight = 0f;
            planPhase = PlanPhase.Active;
            planNote = JyRopeFlyNote;
            planTime = 0f;
            planPoint = jyKnotPos;
            facing = FlatDir(planDir);
            skillHitSet.Clear();
            SkillDetail = $"밧줄을 던짐 ({JyRopeRange(s):0.#} m)";
            Log($"앙파상-밧줄: 던짐 (돌린 {jyRopeCharge:0.00}초, {JyRopeRange(s):0.#} m)");
            // (A moment's way is a unit on the wire: the first speed goes as its size.)
            Fx(SkillFxKind.JyRope, null, hand, jyKnotVel.normalized, 3, jyKnotVel.magnitude, JyRopeLanding);
            StartCooldown(SetFourCooldown(PawnRushSkillIds.RopePassant, s));   // (the cooldown runs from the throw)
        }

        /// <summary>The knot in flight, a lob under gravity: the first enemy it passes catches it; a wall or the floor ends it.</summary>
        void JyKnotStep(PawnRushSkillParams s, float dt)
        {
            Vector3 next = jyKnotPos + jyKnotVel * dt + Vector3.down * (0.5f * Gravity4 * dt * dt);
            jyKnotVel += Vector3.down * (Gravity4 * dt);
            jyRopeFlight += dt;
            Vector3 step = next - jyKnotPos;
            float len = step.magnitude;
            // (The floor ends it, but for the first moment out of the hand.)
            if (len > 1e-4f && S3Sweep(jyKnotPos, step / len, len, s.jyRopeCatch, jyRopeFlight < 0.12f ? 1f : 0f, out var at, out var piece))
            {
                if (piece != null && UltFoe(piece)) JyRopeCatch(s, piece, at);
                else JyRopeMiss(at);
                return;
            }
            jyKnotPos = next;
            planPoint = jyKnotPos;
            if (jyRopeFlight > 3f) JyRopeMiss(jyKnotPos);
        }

        void JyRopeCatch(PawnRushSkillParams s, RagdollPawn e, Vector3 at)
        {
            planTarget = e;
            planNote = JyRopePullNote;
            planTime = 0f;
            planPoint = at;
            // The recoil: the pawn is pulled on, a little faster than it ran.
            Vector3 toward = FlatDir(e.bodies[0].position - bodies[0].position);
            AddVelocity(toward * s.jyRopeLunge);
            e.LastSkillHit = "앙파상-밧줄: 허리에 감김";
            Fx(SkillFxKind.JyRope, e, at, toward, 4);
            FeelHit(e, at, toward, FeelWeak);
        }

        void JyRopeMiss(Vector3 at)
        {
            planPhase = PlanPhase.Recovery;
            planTime = -0.6f;   // (a while to coil it back in)
            planNote = 0;
            planPoint = at;
            Fx(SkillFxKind.JyRope, null, at, FlatDir(facing), 6);
            FeelMiss(at, FlatDir(facing));
            SkillDetail = "밧줄이 빗나감";
        }

        /// <summary>The second of pulling: the enemy comes in fast, the pawn goes on; when they meet, the neck chop.</summary>
        void JyRopePullStep(PawnRushSkillParams s, float dt)
        {
            var e = planTarget;
            if (e == null || e.SkillUntargetable || e.Floating || e.CurrentCc == Cc4.Frozen)
            {
                JyRopeMiss(e != null ? e.Chest : Chest);
                return;
            }
            Vector3 me = bodies[0].position, there = e.bodies[0].position, flat = Flat(there - me);
            float dist = flat.magnitude;
            Vector3 toMe = dist > 0.01f ? -flat / dist : -FlatDir(facing);
            planPoint = e.Chest;
            if (dist <= s.jyRopeChop)
            {
                // The chop, by itself: the neck, flat out.
                Vector3 neck = e.bodies[(int)BodyId.Head].position;
                e.GiveCc(Cc4.Flop, s.jyRopeFlop, this);
                Fx(SkillFxKind.JyRope, e, neck, -toMe, 5);
                FeelHit(e, neck, -toMe, FeelHard);
                JyCapToRun();   // (and on at its own speed)
                planPhase = PlanPhase.Recovery;
                planTime = 0f;
                planNote = 0;
                SkillDetail = $"뒷목 탁! → {e.DisplayName}";
                return;
            }
            if (e.State == PawnState.Active)
            {
                Vector3 own = Flat(e.bodies[0].linearVelocity);
                float inward = Vector3.Dot(own, toMe);
                if (inward < s.jyRopePull) e.AddVelocity(toMe * (s.jyRopePull - inward));
                e.Face(-toMe);   // (dragged in back first: its neck to the pawn)
            }
            // The recoil keeps the pawn going toward it: its own run and jyRopeLunge more.
            Vector3 mine = Flat(bodies[0].linearVelocity) - carryVel;
            float onward = Vector3.Dot(mine, -toMe), want = Mathf.Max(TopSpeed * 0.5f, 0f) + s.jyRopeLunge;
            if (onward < want) AddVelocity(-toMe * (want - onward));
            if (planTime >= s.jyRopeTime) JyRopeMiss(there);
        }

        // ---------------------------------------------------------------- 나이트: 점프&밟기

        void UpdateJumpStomp(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    planTarget = null;
                    planNote = 0;
                    planDir = AimFlat();
                    jyMarked.Clear();
                    jyLit = null;
                    HaltDash();
                    SkillDetail = "점프&밟기";
                    Fx(SkillFxKind.JyStomp, null, FeetPoint, FlatDir(facing), 0, s.jyJumpPrep);
                    break;
                case PlanPhase.Windup:
                {
                    planTime += dt;
                    HoldStill(0.05f);
                    if (planTime < s.jyJumpPrep) break;
                    // Up where it stands: no run speed, none to steer by on the way (2.1).
                    jyJumpFrom = FeetPoint;
                    float up = Mathf.Sqrt(2f * Gravity4 * Mathf.Max(0.3f, s.jyJumpHigh)) * (s.leapLift > 0f ? s.leapLift : 1f);
                    Launch(Vector3.up * up + carryVel);
                    skillAirSteer = 0f;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    planNote = JyUpNote;
                    planPoint = jyJumpFrom;
                    SkillDetail = "점프 (공중에서 F = 밟기)";
                    Fx(SkillFxKind.JyStomp, null, jyJumpFrom, FlatDir(facing), 1, up / Gravity4);
                    break;
                }
                case PlanPhase.Active:
                {
                    planTime += dt;
                    if ((planNote & JyDiveNote) != 0)
                    {
                        JyDiveStep(s, dt);
                        break;
                    }
                    if ((planNote & JyStompedNote) == 0 && (planNote & JyDropNote) == 0)
                    {
                        // Straight up and down: nothing across.
                        Vector3 own = Flat(bodies[0].linearVelocity) - carryVel;
                        if (own.sqrMagnitude > 0.01f) AddVelocity(-own);
                        JyMarks(s);
                        if (press && planTime >= 0.1f)
                        {
                            if (jyLit != null) JyBeginDive(s);
                            else
                            {
                                // No marks: the key again drops it fast.
                                planNote |= JyDropNote;
                                AddVelocity(Vector3.down * 10f);
                                SkillDetail = "빨리 내려옴";
                            }
                            break;
                        }
                    }
                    if (planTime > 0.25f && Grounded) JyLand(s);
                    else if (planTime > 4f || Climbing || rope != null) JyLand(s);
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime < 0.2f) break;
                    EndPlanSkill();
                    StartCooldown(SetFourCooldown(PawnRushSkillIds.JumpStomp, s));
                    break;
            }
        }

        /// <summary>The horseshoes: every enemy within jyMarkRange of where it rose and no more than jyMarkAbove over it; the
        /// one nearest the aim lit (every PC is told of each new one and of the lit one changing).</summary>
        void JyMarks(PawnRushSkillParams s)
        {
            Vector3 aim = AimPoint(s.jyMarkRange + 6f);
            RagdollPawn best = null;
            float bestD = float.MaxValue;
            foreach (var e in All)
            {
                if (!UltFoe(e)) continue;
                Vector3 f = e.FeetPoint;
                if (Flat(f - jyJumpFrom).magnitude > s.jyMarkRange || f.y > jyJumpFrom.y + s.jyMarkAbove) continue;
                if (!jyMarked.Contains(e))
                {
                    jyMarked.Add(e);
                    Fx(SkillFxKind.JyStomp, e, e.bodies[(int)BodyId.Head].position, Vector3.up, 2, 2f);
                }
                float d = Flat(f - aim).magnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            if (best != jyLit)
            {
                jyLit = best;
                planTarget = best;
                if (best != null) Fx(SkillFxKind.JyStomp, best, best.bodies[(int)BodyId.Head].position, Vector3.up, 2, 1f);
            }
        }

        void JyBeginDive(PawnRushSkillParams s)
        {
            planNote |= JyDiveNote;
            planTarget = jyLit;
            jyDiveT = 0f;
            jyDiveFrom = bodies[0].position;
            SkillDetail = $"밟기 → {jyLit.DisplayName}";
            Fx(SkillFxKind.JyStomp, jyLit, jyDiveFrom, FlatDir(jyLit.bodies[0].position - jyDiveFrom), 3, s.jyDiveTime);
        }

        void JyDiveStep(PawnRushSkillParams s, float dt)
        {
            var e = planTarget;
            if (e == null || e.SkillUntargetable || e.Floating)
            {
                // It got away: the rest of the way straight down.
                EndFlight();
                planNote = (byte)((planNote & ~JyDiveNote) | JyDropNote);
                AddVelocity(Vector3.down * 8f);
                return;
            }
            jyDiveT += dt;
            float u = Mathf.Clamp01(jyDiveT / Mathf.Max(0.05f, s.jyDiveTime));
            var headBody = e.bodies[(int)BodyId.Head];
            var headCollider = headBody.GetComponent<Collider>();
            Vector3 head = headBody.position;
            float top = headCollider != null ? headCollider.bounds.max.y : head.y + 0.12f;
            Vector3 onHead = new Vector3(head.x, top + standHeight, head.z);
            Fly(Vector3.Lerp(jyDiveFrom, onHead, u * u), (onHead - jyDiveFrom) / Mathf.Max(0.05f, s.jyDiveTime), 0f, default, 30f);
            Face(onHead - jyDiveFrom);
            if (u >= 1f || (bodies[0].position - onHead).magnitude < 0.3f) JyStomp(s, e, new Vector3(head.x, top, head.z));
        }

        void JyStomp(PawnRushSkillParams s, RagdollPawn e, Vector3 at)
        {
            Vector3 dir = FlatDir(at - jyDiveFrom);
            // Pressed flat: 납작이.
            e.GiveCc(Cc4.Flat, s.flatSeconds, this);
            Fx(SkillFxKind.JyStomp, e, at, dir, 4);
            FeelHit(e, at, dir, FeelHard);
            // The fork: the other enemies near it, their ankles shackled.
            Vector3 ground = e.FeetPoint;
            Fx(SkillFxKind.JyStomp, null, ground, dir, 5, s.jyForkRange);
            foreach (var o in All)
            {
                if (o == e || !UltFoe(o)) continue;
                if (Flat(o.bodies[0].position - ground).magnitude > s.jyForkRange || Mathf.Abs(o.FeetPoint.y - ground.y) > 1.5f) continue;
                o.GiveCc(Cc4.BoundRoot, s.jyForkBind, this, o.bodies[0].position);
                Fx(SkillFxKind.JyStomp, o, o.FeetPoint, FlatDir(o.bodies[0].position - ground), 6, s.jyForkBind);
            }
            // Off its head a metre up, to come down beside it.
            EndFlight();
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Launch(side * 1.6f + dir * 0.6f + Vector3.up * Mathf.Sqrt(2f * Gravity4 * Mathf.Max(0.1f, s.jyStompBounce)));
            planNote = (byte)((planNote & ~JyDiveNote) | JyStompedNote);
            planTime = 0f;
            SkillDetail = $"밟음! {e.DisplayName} 납작";
        }

        void JyLand(PawnRushSkillParams s)
        {
            Fx(SkillFxKind.JyStomp, null, FeetPoint, FlatDir(facing), 7, (planNote & JyStompedNote) != 0 ? 1f : 0f);
            jyMarked.Clear();
            jyLit = null;
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
        }

        // ---------------------------------------------------------------- keys and states

        /// <summary>Before the run (SetFourKeys): what the user's own skills do to the keys in their phases.</summary>
        void SetFourJYKeys()
        {
            if (planPhase == PlanPhase.None) return;
            switch (activeSkill)
            {
                case PawnRushSkillIds.RopePassant:
                    // The rope out: the left button swings and throws it (no dive).
                    if (planPhase != PlanPhase.Windup) break;
                    jyRopeHeld = rawShoveHeld || input.shove;   // (input's own is cleared below every step)
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
                case PawnRushSkillIds.JumpStomp:
                    if (planPhase == PlanPhase.Windup || (planPhase == PlanPhase.Active && (planNote & JyStompedNote) == 0))
                    {
                        // Straight up and down: no steering, no dive, no grab on the way.
                        input.move = Vector3.zero;
                        input.shove = false;
                        input.shoveHeld = false;
                        input.grab = false;
                        input.jump = false;
                    }
                    break;
                case PawnRushSkillIds.StepsDiag:
                    if (planPhase == PlanPhase.Windup || planPhase == PlanPhase.Active) input.jump = false;
                    break;
            }
            SetFourJYCKeys();   // the rook's: the strongman's buttons, the clone's keys (RagdollPawn.SetFourJYC.cs)
        }

        /// <summary>Every step (UpdateSetFourStates).</summary>
        void UpdateSetFourJYStates(PawnRushSkillParams s, float dt)
        {
            UpdateSetFourJYBStates(s, dt);
            UpdateSetFourJYCStates(s, dt);
        }

        void ClearSetFourJYStates()
        {
            jyRopeSwinging = false;
            jyMarked.Clear();
            jyLit = null;
            ClearSetFourJYBStates();
            ClearSetFourJYCStates();
        }
    }
}
