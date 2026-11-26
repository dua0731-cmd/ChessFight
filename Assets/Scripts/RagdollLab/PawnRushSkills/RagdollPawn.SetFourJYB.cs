using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The skills the user wrote (USER_SKILLS 4.2), bundle 2: the bishop's three. Host side; every PC draws them from the
    /// moments (SkillFxKind.JySword, JyBow, JyBeam; PawnRushSkillFx.SetFourJYB.cs). None of them moves the bishop
    /// anywhere (2.1); the cross is a post no one climbs or stands on. Not seen in Unity yet.
    ///
    ///   성검 던지기 (중급): a holy sword appears in the hand (0.25 s) and is thrown spinning on a low arc (18 m/s, 15 m);
    ///              the first enemy it meets is bound at the foot of a big cross that comes down behind it (묶임 제자리형
    ///              2 s); the cross stands 4 s, a post enemies cannot pass (teammates pass); a miss sticks in the floor
    ///   케틀 궁 (상급): the other team's leader is aimed at by itself (1 s, a red line and a target over its head, the
    ///              bishop free to move), then a homing bullet (40 m/s, through walls, stopped only by bodies): the leader
    ///              is blown back along the course; any other enemy it meets first takes it instead and is pushed a little;
    ///              no leader, no shot (and no cooldown)
    ///   핀! (중급): a narrow beam at once (0.4 m wide, 25 m, stopped by walls): the first two enemies on it freeze where
    ///              they are (멈춤 2 s)
    /// </summary>
    public partial class RagdollPawn
    {
        Vector3 jySwordPos, jySwordVel, jyBowPos;
        float jySwordFlight;
        PawnRushKitWall jyCross;
        const byte JyBowStolenNote = 1;

        /// <summary>The leader the bow is on (its aim's second), or null.</summary>
        public RagdollPawn JyBowAimingAt => PlanSkill == PawnRushSkillIds.KettleBow && planPhase == PlanPhase.Windup ? planTarget : null;
        /// <summary>The bow's bullet was taken by another (the one in PlanTarget) - the notice says so.</summary>
        public bool JyBowStolen => PlanSkill == PawnRushSkillIds.KettleBow && planPhase == PlanPhase.Recovery && (planNote & JyBowStolenNote) != 0;

        // ---------------------------------------------------------------- 성검 던지기

        void UpdateSwordThrow(PawnRushSkillParams s, bool press, float dt)
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
                    SkillDetail = "성검 소환";
                    Fx(SkillFxKind.JySword, null, Chest, planDir, 0, s.jySwordSummon);
                    break;
                case PlanPhase.Windup:
                {
                    planTime += dt;
                    planDir = AimFlat();
                    if (planTime < s.jySwordSummon) break;
                    // Thrown: across at jySwordSpeed, lifted just enough to come down jySwordRange away on flat ground.
                    Vector3 hand = Chest + Vector3.up * 0.4f + planDir * 0.3f;
                    float T = Mathf.Max(0.2f, s.jySwordRange / Mathf.Max(1f, s.jySwordSpeed)), h = hand.y - FeetPoint.y;
                    float up = (0.5f * Gravity4 * T * T - h) / T;
                    jySwordVel = planDir * s.jySwordSpeed + Vector3.up * up;
                    jySwordPos = hand;
                    jySwordFlight = 0f;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    planPoint = hand;
                    facing = planDir;
                    SkillDetail = "성검 던지기";
                    Fx(SkillFxKind.JySword, null, hand, jySwordVel.normalized, 1, jySwordVel.magnitude);
                    break;
                }
                case PlanPhase.Active:
                {
                    planTime += dt;
                    Vector3 next = jySwordPos + jySwordVel * dt + Vector3.down * (0.5f * Gravity4 * dt * dt);
                    jySwordVel += Vector3.down * (Gravity4 * dt);
                    jySwordFlight += dt;
                    Vector3 step = next - jySwordPos;
                    float len = step.magnitude;
                    if (len > 1e-4f && S3Sweep(jySwordPos, step / len, len, s.jySwordCatch, jySwordFlight < 0.12f ? 1f : 0f, out var at, out var piece))
                    {
                        if (piece != null && UltFoe(piece)) JyCrossDown(s, piece);
                        else JySwordMiss(at);
                        break;
                    }
                    jySwordPos = next;
                    planPoint = next;
                    if (jySwordFlight > 2.5f) JySwordMiss(next);
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime < 0.3f) break;
                    EndPlanSkill();
                    StartCooldown(SetFourCooldown(PawnRushSkillIds.SwordThrow, s));
                    break;
            }
        }

        /// <summary>The cross comes down just behind the one hit (along the throw), a post enemies cannot pass; the one hit
        /// is bound at its foot.</summary>
        void JyCrossDown(PawnRushSkillParams s, RagdollPawn e)
        {
            Vector3 way = FlatDir(Flat(jySwordVel).sqrMagnitude > 0.01f ? jySwordVel : planDir);
            Vector3 feet = e.FeetPoint, foot = feet + way * 0.6f;
            if (SolidRay(foot + Vector3.up * 1.5f, Vector3.down, 4f, out var floor)) foot = floor.point;
            if (jyCross != null) jyCross.Break();
            jyCross = PawnRushKitWall.Raise(PawnRushKitWall.NextId(), this, foot - way * 0.25f, foot + way * 0.25f, s.jyCrossHigh, 0.6f, s.jyCrossSeconds, 0.2f, true);
            jyCross.Hidden = true;   // (every PC draws the cross from the moment)
            jyCross.NoTop = true;    // (no one stands on it)
            foreach (var o in All)
                if (o != null && o != this && IsAllyOf(o) && jyCross.Collider != null) o.IgnoreBody(jyCross.Collider, true);
            e.GiveCc(Cc4.BoundRoot, s.jyCrossBind, this, e.bodies[0].position);
            Fx(SkillFxKind.JySword, e, foot, way, 2, s.jyCrossSeconds);
            FeelHit(e, e.Chest, way, FeelHard);
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            SkillDetail = $"성검 → {e.DisplayName}: 십자가";
        }

        void JySwordMiss(Vector3 at)
        {
            Fx(SkillFxKind.JySword, null, at, FlatDir(Flat(jySwordVel).sqrMagnitude > 0.01f ? jySwordVel : planDir), 3);
            FeelMiss(at, planDir);
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            SkillDetail = "성검이 빗나감";
        }

        // ---------------------------------------------------------------- 케틀 궁

        void UpdateKettleBow(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    var leader = EnemyLeader();
                    if (leader == null)
                    {
                        SkipNoTarget4("케틀 궁");
                        break;
                    }
                    SkillUses++;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    planTarget = leader;
                    planNote = 0;
                    planDir = FlatDir(leader.Chest - Chest);
                    SkillDetail = $"케틀 궁 → {leader.DisplayName} (1등)";
                    Fx(SkillFxKind.JyBow, leader, Chest, planDir, 0, s.jyBowAim);
                    break;
                }
                case PlanPhase.Windup:
                {
                    // The aim's second: the bishop goes where it likes; the leader may change hands meanwhile (it stays on
                    // the one it named).
                    planTime += dt;
                    var t = planTarget;
                    if (t == null || t.SkillUntargetable)
                    {
                        EndPlanSkill();   // (gone before the shot: nothing spent)
                        break;
                    }
                    planDir = FlatDir(t.Chest - Chest);
                    if (planTime < s.jyBowAim) break;
                    jyBowPos = Chest + Vector3.up * 0.45f + planDir * 0.35f;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    planPoint = jyBowPos;
                    // The kick of the shot: back half a metre, a stagger.
                    AddVelocity(-planDir * 2.5f);
                    Fx(SkillFxKind.JyBow, t, jyBowPos, planDir, 1, s.jyBowSpeed);
                    break;
                }
                case PlanPhase.Active:
                {
                    planTime += dt;
                    var t = planTarget;
                    if (t == null || t.SkillUntargetable || planTime > 6f)
                    {
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        break;
                    }
                    // Homing on the leader's chest; through walls; the first body on its way takes it.
                    Vector3 to = t.Chest - jyBowPos;
                    float step = s.jyBowSpeed * dt, dist = to.magnitude;
                    Vector3 dir = dist > 1e-3f ? to / dist : planDir;
                    float go = Mathf.Min(step, dist);
                    RagdollPawn first = null;
                    float firstAlong = float.MaxValue;
                    foreach (var o in All)
                    {
                        if (!UltFoe(o) || o.CurrentCc == Cc4.Frozen) continue;
                        Vector3 rel = o.Chest - jyBowPos;
                        float along = Mathf.Clamp(Vector3.Dot(rel, dir), 0f, go + s.jyBowCatch);
                        float off = (rel - dir * along).magnitude;
                        if (off > s.jyBowCatch + 0.3f || along >= firstAlong) continue;
                        first = o;
                        firstAlong = along;
                    }
                    if (first != null && (first != t || dist <= go + s.jyBowCatch + 0.3f || firstAlong <= go))
                    {
                        JyBowHit(s, first, first == t, dir);
                        break;
                    }
                    jyBowPos += dir * go;
                    planPoint = jyBowPos;
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime < 0.7f) break;
                    EndPlanSkill();
                    StartCooldown(SetFourCooldown(PawnRushSkillIds.KettleBow, s));
                    break;
            }
        }

        void JyBowHit(PawnRushSkillParams s, RagdollPawn o, bool leader, Vector3 dir)
        {
            if (leader)
            {
                // Blown back along the course (the way the race runs, backwards; its own facing in a course).
                Vector3 back = RaceProgress == null ? Vector3.back : -FlatDir(o.Facing);
                SkillHit(o, Throw4(back, 3), true, "케틀 궁");
                Fx(SkillFxKind.JyBow, o, o.Chest, back, 3);
                FeelHit(o, o.Chest, back, FeelHard);
                SkillDetail = $"케틀 궁: {o.DisplayName} 맞음!";
            }
            else
            {
                // Someone in the way: it takes it, pushed a little.
                SkillHit(o, Throw4(FlatDir(dir), 1), false, "케틀 궁 (대신 맞음)");
                Fx(SkillFxKind.JyBow, o, o.Chest, FlatDir(dir), 2);
                planNote |= JyBowStolenNote;
                SkillDetail = $"케틀 궁: {o.DisplayName}이(가) 대신 맞음";
            }
            planTarget = o;
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            Fx(SkillFxKind.JyBow, null, Chest, FlatDir(facing), 4, 0.6f);
        }

        // ---------------------------------------------------------------- 핀!

        void UpdatePinBeam(PawnRushSkillParams s, bool press, float dt)
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
                    SkillDetail = "핀!";
                    Fx(SkillFxKind.JyBeam, null, Chest, planDir, 0, s.jyBeamPrep);
                    break;
                case PlanPhase.Windup:
                {
                    planTime += dt;
                    planDir = AimFlat();
                    if (planTime < s.jyBeamPrep) break;
                    Vector3 dir = Pitched(AimRay(), 10f);
                    Vector3 from = Chest + Vector3.up * 0.25f + FlatDir(dir) * 0.35f;
                    float len = JyBeamLength(from, dir, s.jyBeamRange);
                    // The first two enemies on its line, nearest first.
                    var on = new System.Collections.Generic.List<(float along, RagdollPawn who)>();
                    foreach (var o in All)
                    {
                        if (!UltFoe(o) || o.CurrentCc == Cc4.Frozen) continue;
                        Vector3 rel = o.Chest - from;
                        float along = Vector3.Dot(rel, dir);
                        if (along < 0f || along > len + 0.3f) continue;
                        if ((rel - dir * along).magnitude > s.jyBeamWide * 0.5f + 0.45f) continue;
                        on.Add((along, o));
                    }
                    on.Sort((a, b) => a.along.CompareTo(b.along));
                    Fx(SkillFxKind.JyBeam, null, from, dir, 1, len);
                    for (int i = 0; i < on.Count && i < Mathf.Max(1, s.jyBeamHits); i++)
                    {
                        var o = on[i].who;
                        o.GiveCc(Cc4.Frozen, s.jyBeamFreeze, this);
                        Fx(SkillFxKind.JyBeam, o, o.Chest, dir, 2 + i, s.jyBeamFreeze);
                    }
                    SkillDetail = on.Count > 0 ? $"핀! {Mathf.Min(on.Count, s.jyBeamHits)}명 멈춤" : "핀! (빗나감)";
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime < 0.25f) break;
                    EndPlanSkill();
                    StartCooldown(SetFourCooldown(PawnRushSkillIds.PinBeam, s));
                    break;
            }
        }

        /// <summary>How far the beam goes before a wall (pieces do not stop it).</summary>
        float JyBeamLength(Vector3 from, Vector3 dir, float range)
        {
            int n = Physics.RaycastNonAlloc(from, dir, hits, range, ~0, QueryTriggerInteraction.Ignore);
            float best = range;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider == null || ownSet.Contains(h.collider) || ColliderOwner.ContainsKey(h.collider) || PassesThrough(h.collider)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                if (h.distance < best) best = h.distance;
            }
            return best;
        }

        // ---------------------------------------------------------------- states

        void UpdateSetFourJYBStates(PawnRushSkillParams s, float dt)
        {
            if (jyCross != null && !jyCross.Standing) jyCross = null;
        }

        void ClearSetFourJYBStates()
        {
            jyCross = null;
        }
    }
}
