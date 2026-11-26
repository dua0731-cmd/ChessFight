using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The skills the user wrote (USER_SKILLS 4.4), bundle 3: the rook's four. Host side; every PC draws them from the
    /// moments (SkillFxKind.JyGiant, JyStrong, JyTWall, JyBuild, JyWallLeg, JyWallEnd; PawnRushSkillFx.SetFourJYC.cs) and
    /// stands the walls' pictures (PawnRushKitWall). The rule over them (2.1): the giant goes back where it grew; the
    /// strongman lifts enemies only; no wall is a step up (NoTop) and the rook is where it was after building. Not seen in
    /// Unity yet.
    ///
    ///   거대화 (상급): 0.5 s to grow four times as big, 8 s a giant (it walks and jumps as ever): any enemy inside its drawn
    ///          body is kicked away (뻥), nothing pushes it; then it shrinks back to where it grew. With no room over its
    ///          head it does not grow (no cooldown). Its body's collision: the enemies inside the giant's size are kicked
    ///          out (no physical collider: the small body still walks the level)
    ///   힘세지기 (상급): 15 s big (drawn 1.5x) and heavy (pushed 30%, runs at 90%); the right button held lifts the enemy in
    ///          front (2 m, nearest the aim) over its head; the left button throws it 4 m ahead (1.5 m up), down it goes and
    ///          whoever it lands on too; a lifted one slips out after 3 s; teammates are never lifted
    ///   벨코즈 q 성벽 (중급): a wall runs out from its feet along the aim (20 m/s, 10 m), then - the key again or at 10 m - it
    ///          splits both ways at its head (4 m each): a T, 2 m high, 5 s
    ///   성벽 짓기 (상급): a clone slides along the aim (10 m/s, keeping its height) raising a wall behind it; the rook stays
    ///          put and the camera follows the clone; W, A, S or D turns it to that side (the nearest right angle; not ahead
    ///          or back), at most four times, 0.5 s apart; 24 m of wall, a wall in its way or the key again ends it; the
    ///          rook knocked over or hindered meanwhile ends it too; no wall over a drop
    /// </summary>
    public partial class RagdollPawn
    {
        sealed class JyLeg { public PawnRushKitWall wall; public Vector3 a, dir; public float len, max; public bool done; }

        // The giant: where it grew, the enemies it kicked lately.
        Vector3 jyGiantHome, jyGiantFace;
        readonly Dictionary<RagdollPawn, float> jyKicked = new Dictionary<RagdollPawn, float>();
        // The strongman: the one held, how long, the one thrown and its flight.
        RagdollPawn jyHeld, jyThrown, jyUnignore;
        float jyHeldFor, jyThrownFor;
        bool jyGrabKey, jyThrowKey;
        readonly HashSet<RagdollPawn> jyThrownHit = new HashSet<RagdollPawn>();
        // The walls: the T's legs, the clone's legs, every one standing (for the bumps).
        readonly List<JyLeg> jyLegs = new List<JyLeg>();
        readonly List<(PawnRushKitWall wall, HashSet<RagdollPawn> bumped)> jyWalls = new List<(PawnRushKitWall, HashSet<RagdollPawn>)>();
        Vector3 jyClone, jyCloneDir, jyBuildMove;
        float jyBuildTurnWait, jyBuildBuilt;
        int jyBuildTurns;
        bool jyBuildMoveWas, jyBuildGap;
        const byte JyHoldNote = 1, JyWingsNote = 1;

        /// <summary>A giant now (nothing pushes it; its camera stands back).</summary>
        public bool JyGiantNow => PlanSkill == PawnRushSkillIds.Giant && planPhase == PlanPhase.Active;
        /// <summary>A strongman now (heavy and slow).</summary>
        public bool JyStrongNow => PlanSkill == PawnRushSkillIds.Strongman && planPhase == PlanPhase.Active;
        /// <summary>The enemy a strongman holds over its head (its PlanTarget while the hold note is up), or null.</summary>
        public RagdollPawn JyHolding => JyStrongNow && (planNote & JyHoldNote) != 0 ? planTarget : null;
        /// <summary>Building walls: the camera follows the clone (any PC: the plan point is the clone).</summary>
        public bool JyBuilding => PlanSkill == PawnRushSkillIds.WallBuild && planPhase == PlanPhase.Active;

        /// <summary>Where the camera follows instead of the body (LabCamera): the clone while building.</summary>
        public Vector3? CameraFollowOverride => JyBuilding ? planPoint + Vector3.up * 0.9f : (Vector3?)null;
        /// <summary>How far back the camera stands (LabCamera): a giant's 2.5 times.</summary>
        public float CameraReachScale => JyGiantNow ? 2.5f : 1f;

        /// <summary>How far pushes take it (RagdollPawn.PushScale): a strongman 30%.</summary>
        float JyPushScale => JyStrongNow && PawnRushSkills != null ? PawnRushSkills.jyStrongPush : 1f;
        /// <summary>Its run's top speed (SkillMoveScale): a strongman 90%.</summary>
        float JyMoveScale => JyStrongNow && PawnRushSkills != null ? PawnRushSkills.jyStrongMove : 1f;

        // ---------------------------------------------------------------- 거대화

        void UpdateGiant(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    // Room to grow: nothing over its head up to its giant height.
                    float tall = Mathf.Max(1.2f, bodies[(int)BodyId.Head].position.y - FeetPoint.y + 0.3f) * s.jyGiantScale;
                    if (SolidRay(bodies[(int)BodyId.Head].position, Vector3.up, tall, out _))
                    {
                        SkillDetail = "거대화: 커질 자리가 없어요 (쿨 없음)";
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, AimFlat(), (int)piece);
                        break;
                    }
                    SkillUses++;
                    jyGiantHome = FeetPoint;
                    jyGiantFace = FlatDir(facing);
                    jyKicked.Clear();
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    planPoint = jyGiantHome;
                    SkillDetail = "거대화 (흐읍—)";
                    Fx(SkillFxKind.JyGiant, null, jyGiantHome, jyGiantFace, 0, s.jyGiantPrep);
                    break;
                }
                case PlanPhase.Windup:
                    planTime += dt;
                    if (planTime < s.jyGiantPrep) break;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = "거대화!";
                    Fx(SkillFxKind.JyGiant, null, jyGiantHome, jyGiantFace, 1, s.jyGiantSeconds);
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    // Every enemy inside its giant body is kicked away (each once in a while).
                    float k = Mathf.Clamp01(planTime / Mathf.Max(0.05f, s.jyGiantGrow)) * (s.jyGiantScale - 1f) + 1f;
                    float reach = 0.55f * k, high = 1.7f * k;
                    Vector3 me = bodies[0].position;
                    foreach (var o in All)
                    {
                        if (!UltFoe(o)) continue;
                        Vector3 d = o.bodies[0].position - me;
                        if (Flat(d).magnitude > reach + 0.35f || d.y > high || d.y < -1.5f) continue;
                        if (jyKicked.TryGetValue(o, out var at) && Time.time - at < 0.7f) continue;
                        jyKicked[o] = Time.time;
                        Vector3 away = Flat(d).sqrMagnitude > 0.01f ? Flat(d).normalized : FlatDir(facing);
                        SkillHit(o, Throw4(away, 2), true, "거대화");
                        Fx(SkillFxKind.JyGiant, o, o.Chest, away, 2);
                    }
                    if (planTime < s.jyGiantSeconds) break;
                    // Over: it shrinks back to where it grew.
                    Vector3 was = FeetPoint;
                    Teleport(jyGiantHome + Vector3.up * standHeight, jyGiantFace);
                    Fx(SkillFxKind.JyGiant, null, jyGiantHome, jyGiantFace, 3, 0.5f, was);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime < 0.3f) break;
                    EndPlanSkill();
                    StartCooldown(SetFourCooldown(PawnRushSkillIds.Giant, s));
                    break;
            }
        }

        // ---------------------------------------------------------------- 힘세지기

        void UpdateStrongman(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    jyHeld = jyThrown = null;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    planTarget = null;
                    planNote = 0;
                    SkillDetail = "힘세지기 (으랏차!)";
                    Fx(SkillFxKind.JyStrong, null, FeetPoint, FlatDir(facing), 0, s.jyStrongPrep);
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    HoldStill(0.05f);
                    if (planTime < s.jyStrongPrep) break;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = "힘세지기 (우클릭 꾹 = 들기, 좌클릭 = 던지기)";
                    Fx(SkillFxKind.JyStrong, null, FeetPoint, FlatDir(facing), 1, s.jyStrongSeconds);
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    if (jyHeld != null) JyHoldStep(s, dt);
                    else if (jyGrabKey)
                    {
                        var o = JyLiftable(s);
                        if (o != null)
                        {
                            jyHeld = o;
                            jyHeldFor = 0f;
                            IgnorePiece(o, true);   // (held over its head: the two pass through each other)
                            planTarget = o;
                            planNote |= JyHoldNote;
                            o.LastSkillHit = "힘세지기: 들림";
                            Fx(SkillFxKind.JyStrong, o, o.Chest, FlatDir(facing), 2, s.jyStrongEscape);
                        }
                    }
                    JyThrownStep(dt);
                    jyThrowKey = false;
                    if (planTime < s.jyStrongSeconds) break;
                    if (jyHeld != null) JyDropHeld(false);
                    Fx(SkillFxKind.JyStrong, null, FeetPoint, FlatDir(facing), 5);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    JyThrownStep(dt);
                    if (planTime < 0.5f) break;
                    EndPlanSkill();
                    StartCooldown(SetFourCooldown(PawnRushSkillIds.Strongman, s));
                    break;
            }
        }

        /// <summary>The enemy in front it can lift: within jyStrongReach, in front (60° either side), nearest the aim.</summary>
        RagdollPawn JyLiftable(PawnRushSkillParams s)
        {
            Vector3 me = bodies[0].position, face = FlatDir(facing), aim = AimFlat();
            RagdollPawn best = null;
            float bestA = float.MaxValue;
            foreach (var o in All)
            {
                if (!UltFoe(o) || o.CurrentCc == Cc4.Frozen || o.JyGiantNow) continue;
                Vector3 d = o.bodies[0].position - me;
                if (Flat(d).magnitude > s.jyStrongReach + 0.3f || Mathf.Abs(d.y) > 1.3f || Vector3.Angle(face, Flat(d)) > 60f) continue;
                float a = Vector3.Angle(aim, Flat(d));
                if (a < bestA)
                {
                    bestA = a;
                    best = o;
                }
            }
            return best;
        }

        /// <summary>Held over its head, kicking: thrown on the left button, slipping out after jyStrongEscape.</summary>
        void JyHoldStep(PawnRushSkillParams s, float dt)
        {
            var o = jyHeld;
            if (o == null || o.SkillUntargetable || o.Floating)
            {
                JyDropHeld(false);
                return;
            }
            jyHeldFor += dt;
            Vector3 over = bodies[(int)BodyId.Head].position + Vector3.up * 0.9f;
            o.Fly(over, Flat(bodies[0].linearVelocity), 0f, default, 20f);
            o.Face(Vector3.Cross(Vector3.up, FlatDir(facing)));
            if (jyThrowKey)
            {
                // Thrown: 4 m ahead along the aim, 1.5 m up, down it goes where it lands.
                Vector3 to = FeetPoint + AimFlat() * s.jyStrongThrow;
                o.EndFlight();
                Vector3 v = ArcTo4(o.FeetPoint, to, s.jyStrongThrowHigh, out _);
                SkillHit(o, v, true, "힘세지기 (던지기)");
                Fx(SkillFxKind.JyStrong, o, o.Chest, AimFlat(), 3);
                FeelHit(o, o.Chest, AimFlat(), FeelHard);
                jyThrown = o;
                jyThrownFor = 0f;
                jyThrownHit.Clear();
                jyHeld = null;
                jyUnignore = o;   // (touching again once it is clear of the head)
                planNote = (byte)(planNote & ~JyHoldNote);
                return;
            }
            if (jyHeldFor >= s.jyStrongEscape) JyDropHeld(true);
        }

        void JyDropHeld(bool slipped)
        {
            var o = jyHeld;
            jyHeld = null;
            planNote = (byte)(planNote & ~JyHoldNote);
            if (o == null) return;
            o.EndFlight(Vector3.down * 1f);
            jyUnignore = o;
            if (slipped) Fx(SkillFxKind.JyStrong, o, o.Chest, FlatDir(facing), 4);
        }

        /// <summary>A thrown enemy for a second: whoever it flies into goes down too.</summary>
        void JyThrownStep(float dt)
        {
            if (jyUnignore != null && (jyUnignore.bodies[0].position - bodies[0].position).magnitude > 1.6f)
            {
                IgnorePiece(jyUnignore, false);
                jyUnignore = null;
            }
            if (jyThrown == null) return;
            jyThrownFor += dt;
            if (jyThrownFor > 1.2f)
            {
                jyThrown = null;
                return;
            }
            Vector3 at = jyThrown.bodies[0].position;
            foreach (var o in All)
            {
                if (o == jyThrown || !UltFoe(o) || jyThrownHit.Contains(o)) continue;
                if ((o.bodies[0].position - at).magnitude > 0.95f) continue;
                jyThrownHit.Add(o);
                Vector3 away = FlatDir(o.bodies[0].position - at);
                SkillHit(o, Throw4(away, 1), true, "힘세지기 (날아온 적)");
                Fx(SkillFxKind.S3Hit, o, o.bodies[(int)BodyId.Head].position, away, 1);
            }
        }

        // ---------------------------------------------------------------- the walls (the T and the clone's)

        /// <summary>A wall leg starting at <paramref name="a"/> along <paramref name="dir"/>, growing as it is told; solid on
        /// the host, a picture elsewhere (JyWallLeg tells them); nobody stands on it; teammates pass.</summary>
        JyLeg JyStartLeg(PawnRushSkillParams s, Vector3 a, Vector3 dir, float max, float speed)
        {
            if (SolidRay(a + Vector3.up * 1.5f, Vector3.down, 4f, out var floor)) a = floor.point;
            var wall = PawnRushKitWall.Raise(PawnRushKitWall.NextId(), this, a, a + dir * 0.05f, s.jyWallHigh, s.jyWallThick, 60f, 0.15f, true);
            wall.NoTop = true;
            foreach (var o in All)
                if (o != null && o != this && IsAllyOf(o) && wall.Collider != null) o.IgnoreBody(wall.Collider, true);
            var leg = new JyLeg { wall = wall, a = a, dir = dir, len = 0.05f, max = max };
            jyLegs.Add(leg);
            jyWalls.Add((wall, new HashSet<RagdollPawn>()));
            Fx(SkillFxKind.JyWallLeg, null, a, dir, wall.Id, speed, new Vector3(max, s.jyWallHigh, s.jyWallThick));
            return leg;
        }

        /// <summary>The leg grows <paramref name="step"/> (to its most); an enemy standing where it rises is nudged aside.</summary>
        void JyGrowLeg(PawnRushSkillParams s, JyLeg leg, float step)
        {
            if (leg.done || leg.wall == null) return;
            float was = leg.len;
            leg.len = Mathf.Min(leg.max, leg.len + step);
            Vector3 end = leg.a + leg.dir * leg.len;
            leg.wall.SetLine(leg.a, end);
            Vector3 side = Vector3.Cross(Vector3.up, leg.dir);
            foreach (var o in All)
            {
                if (!UltFoe(o)) continue;
                Vector3 d = o.bodies[0].position - leg.a;
                float along = Vector3.Dot(Flat(d), leg.dir), across = Vector3.Dot(Flat(d), side);
                if (along < was - 0.3f || along > leg.len + 0.3f || Mathf.Abs(across) > s.jyWallThick * 0.5f + 0.45f || Mathf.Abs(d.y) > s.jyWallHigh + 0.5f) continue;
                o.AddVelocity(side * (across >= 0f ? 3.2f : -3.2f) + Vector3.up * 1f);   // (a bump on the bottom, aside)
            }
        }

        /// <summary>The leg stops growing and stands <paramref name="seconds"/> (JyWallEnd tells the other PCs).</summary>
        void JyEndLeg(JyLeg leg, float seconds)
        {
            if (leg == null || leg.done) return;
            leg.done = true;
            if (leg.wall == null) return;
            leg.wall.StandFor(seconds);
            Fx(SkillFxKind.JyWallEnd, null, leg.a + leg.dir * leg.len, leg.dir, leg.wall.Id, seconds);
        }

        // ---------------------------------------------------------------- 벨코즈 q 성벽

        void UpdateTWall(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    jyLegs.Clear();
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    planNote = 0;
                    planDir = AimFlat();
                    planPoint = FeetPoint;
                    SkillDetail = "벨코즈 q 성벽";
                    Fx(SkillFxKind.JyTWall, null, FeetPoint, planDir, 0, s.jyTWallPrep);
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    HoldStill(0.05f);
                    if (planTime < s.jyTWallPrep) break;
                    JyStartLeg(s, FeetPoint + planDir * 0.8f, planDir, s.jyTWallLength, s.jyTWallSpeed);
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = "성벽이 뻗어요 (F = 가르기)";
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    if ((planNote & JyWingsNote) == 0)
                    {
                        var stem = jyLegs.Count > 0 ? jyLegs[0] : null;
                        if (stem == null)
                        {
                            EndPlanSkill();
                            break;
                        }
                        // A wall or a drop ahead of its head stops it there.
                        Vector3 head = stem.a + stem.dir * stem.len;
                        bool blocked = SolidRay(head + Vector3.up * 1f, stem.dir, s.jyTWallSpeed * dt + 0.4f, out _)
                                       || !SolidRay(head + stem.dir * 0.6f + Vector3.up * 1.5f, Vector3.down, 3.5f, out _);
                        if (!blocked) JyGrowLeg(s, stem, s.jyTWallSpeed * dt);
                        if (press || blocked || stem.len >= stem.max - 0.01f)
                        {
                            // The split: both ways at its head.
                            JyEndLeg(stem, s.jyTWallSeconds + 0.3f);
                            Vector3 at = stem.a + stem.dir * stem.len, side = Vector3.Cross(Vector3.up, stem.dir);
                            JyStartLeg(s, at, side, s.jyTWallWing, s.jyTWallSpeed);
                            JyStartLeg(s, at, -side, s.jyTWallWing, s.jyTWallSpeed);
                            planNote |= JyWingsNote;
                            planPoint = at;
                            Fx(SkillFxKind.JyTWall, null, at, stem.dir, 1);
                        }
                        break;
                    }
                    bool growing = false;
                    for (int i = 1; i < jyLegs.Count; i++)
                    {
                        var w = jyLegs[i];
                        if (w.done) continue;
                        JyGrowLeg(s, w, s.jyTWallSpeed * dt);
                        if (w.len >= w.max - 0.01f) JyEndLeg(w, s.jyTWallSeconds);
                        else growing = true;
                    }
                    if (growing) break;
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime < 0.2f) break;
                    jyLegs.Clear();
                    EndPlanSkill();
                    StartCooldown(SetFourCooldown(PawnRushSkillIds.TWall, s));
                    break;
            }
        }

        // ---------------------------------------------------------------- 성벽 짓기

        void UpdateWallBuild(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    jyLegs.Clear();
                    jyClone = FeetPoint;
                    jyCloneDir = AimFlat();
                    jyBuildTurns = 0;
                    jyBuildTurnWait = 0f;
                    jyBuildBuilt = 0f;
                    jyBuildGap = false;
                    jyBuildMoveWas = true;   // (a key already down does not turn it)
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    planNote = 0;
                    planDir = jyCloneDir;
                    planPoint = jyClone;
                    HaltDash();
                    JyStartLeg(s, jyClone, jyCloneDir, s.jyBuildLength, s.jyBuildSpeed);
                    SkillDetail = "성벽 짓기 (W A S D = 꺾기, F = 끝내기)";
                    Fx(SkillFxKind.JyBuild, null, jyClone, jyCloneDir, 0, s.jyBuildLength);
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;   // (the rook sits still, a statue: its keys go to the clone, SetFourJYCKeys)
                    if (State != PawnState.Active || CurrentCc != Cc4.None)
                    {
                        JyBuildOver(s, false);
                        break;
                    }
                    if (press && planTime > 0.2f)
                    {
                        JyBuildOver(s, true);
                        break;
                    }
                    // A turn: the move key's side of the way it goes (the nearest right angle), a fresh press, 0.5 s apart.
                    jyBuildTurnWait -= dt;
                    Vector3 m = Flat(jyBuildMove);
                    bool held = m.sqrMagnitude > 0.25f;
                    if (held && !jyBuildMoveWas && jyBuildTurnWait <= 0f && jyBuildTurns < s.jyBuildTurns)
                    {
                        Vector3 right = Vector3.Cross(Vector3.up, jyCloneDir);
                        float across = Vector3.Dot(m.normalized, right), ahead = Vector3.Dot(m.normalized, jyCloneDir);
                        if (Mathf.Abs(across) > Mathf.Abs(ahead))
                        {
                            var leg = jyLegs.Count > 0 ? jyLegs[jyLegs.Count - 1] : null;
                            JyEndLeg(leg, 60f);   // (stands until the whole build is over)
                            jyCloneDir = right * Mathf.Sign(across);
                            planDir = jyCloneDir;
                            jyBuildTurns++;
                            planNote = (byte)jyBuildTurns;   // (every PC's screen shows the turns left)
                            jyBuildTurnWait = s.jyBuildTurnCool;
                            Fx(SkillFxKind.JyBuild, null, jyClone, jyCloneDir, 1, jyBuildTurns);
                            if (!jyBuildGap) JyStartLeg(s, jyClone, jyCloneDir, s.jyBuildLength - jyBuildBuilt, s.jyBuildSpeed);
                        }
                    }
                    jyBuildMoveWas = held;
                    // On along its way, keeping its height; a wall ahead ends it.
                    float step = s.jyBuildSpeed * dt;
                    if (SolidRay(jyClone + Vector3.up * 1f, jyCloneDir, step + 0.35f, out var hit) && !JyOwnWall(hit.collider))
                    {
                        JyBuildOver(s, true);
                        break;
                    }
                    jyClone += jyCloneDir * step;
                    planPoint = jyClone;
                    // No floor under it: no wall there (the leg stops; a new one starts where the floor comes back).
                    bool floor = SolidRay(jyClone + Vector3.up * 1.2f, Vector3.down, 3.2f, out _);
                    var cur = jyLegs.Count > 0 ? jyLegs[jyLegs.Count - 1] : null;
                    if (!floor && !jyBuildGap)
                    {
                        JyEndLeg(cur, 60f);
                        jyBuildGap = true;
                    }
                    else if (floor && jyBuildGap)
                    {
                        jyBuildGap = false;
                        cur = JyStartLeg(s, jyClone, jyCloneDir, s.jyBuildLength - jyBuildBuilt, s.jyBuildSpeed);
                    }
                    if (!jyBuildGap && cur != null && !cur.done)
                    {
                        JyGrowLeg(s, cur, step);
                        jyBuildBuilt += step;
                    }
                    if (jyBuildBuilt >= s.jyBuildLength) JyBuildOver(s, true);
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime < 0.3f) break;
                    jyLegs.Clear();
                    EndPlanSkill();
                    StartCooldown(SetFourCooldown(PawnRushSkillIds.WallBuild, s));
                    break;
            }
        }

        bool JyOwnWall(Collider c)
        {
            foreach (var leg in jyLegs)
                if (leg.wall != null && leg.wall.Collider == c) return true;
            return false;
        }

        /// <summary>The build ends: every leg stands jyBuildSeconds; enemies shut in on all four sides are told ("!").</summary>
        void JyBuildOver(PawnRushSkillParams s, bool finished)
        {
            foreach (var leg in jyLegs)
            {
                if (leg.done && leg.wall != null) leg.wall.StandFor(s.jyBuildSeconds);
                JyEndLeg(leg, s.jyBuildSeconds);
            }
            // (Every PC: the legs ended above stood 60 s; tell them the real time.)
            foreach (var leg in jyLegs)
                if (leg.wall != null) Fx(SkillFxKind.JyWallEnd, null, leg.a + leg.dir * leg.len, leg.dir, leg.wall.Id, s.jyBuildSeconds);
            if (finished)
            {
                foreach (var o in All)
                {
                    if (!UltFoe(o) || Flat(o.bodies[0].position - jyClone).magnitude > 20f) continue;
                    if (JyShutIn(o)) Fx(SkillFxKind.JyBuild, o, o.Chest, Vector3.up, 2);
                }
            }
            Fx(SkillFxKind.JyBuild, null, jyClone, jyCloneDir, finished ? 3 : 4);
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
        }

        /// <summary>Walls of this build on four sides of <paramref name="o"/> (flat rays along the first leg's way and across).</summary>
        bool JyShutIn(RagdollPawn o)
        {
            if (jyLegs.Count == 0) return false;
            Vector3 f = jyLegs[0].dir, r = Vector3.Cross(Vector3.up, f), c = o.bodies[0].position;
            foreach (var way in new[] { f, -f, r, -r })
            {
                bool hitOwn = false;
                int n = Physics.RaycastNonAlloc(c, way, hits, 16f, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < n && !hitOwn; i++) hitOwn = JyOwnWall(hits[i].collider);
                if (!hitOwn) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- the walls' bumps, the keys, the states

        /// <summary>An enemy running into one of these walls falls over (thrown back off it).</summary>
        void JyWallBumps(PawnRushSkillParams s)
        {
            for (int i = jyWalls.Count - 1; i >= 0; i--)
            {
                var (wall, bumped) = jyWalls[i];
                if (wall == null || !wall.Standing)
                {
                    jyWalls.RemoveAt(i);
                    continue;
                }
                Vector3 a = wall.From, b = wall.To, line = Flat(b - a);
                if (line.sqrMagnitude < 0.04f) continue;
                Vector3 along = line.normalized, face = Vector3.Cross(Vector3.up, along);
                foreach (var o in All)
                {
                    if (!UltFoe(o) || bumped.Contains(o) || o.State != PawnState.Active) continue;
                    Vector3 d = Flat(o.bodies[0].position - a);
                    float t = Vector3.Dot(d, along), off = Vector3.Dot(d, face);
                    if (t < -0.2f || t > line.magnitude + 0.2f || Mathf.Abs(off) > s.jyWallThick * 0.5f + 0.55f) continue;
                    Vector3 v = Flat(o.bodies[0].linearVelocity);
                    Vector3 out_ = off >= 0f ? face : -face;
                    if (-Vector3.Dot(v, out_) < (s.rampartBump > 0f ? s.rampartBump : 3f)) continue;
                    bumped.Add(o);
                    SkillHit(o, out_ * 2.5f + Vector3.up * 1.5f, true, "성벽");
                    Fx(SkillFxKind.S3Hit, o, o.bodies[(int)BodyId.Head].position, out_, 1);
                }
            }
        }

        /// <summary>Before the run (SetFourJYKeys): the strongman's buttons, the clone's keys.</summary>
        void SetFourJYCKeys()
        {
            switch (activeSkill)
            {
                case PawnRushSkillIds.Strongman:
                    if (planPhase != PlanPhase.Active) break;
                    // The right button lifts (no plain grab meanwhile); with one held, the left button throws (no dive).
                    jyGrabKey = input.grab;
                    input.grab = false;
                    if (jyHeld != null)
                    {
                        jyThrowKey |= input.shove;
                        input.shove = false;
                        input.shoveHeld = false;
                    }
                    break;
                case PawnRushSkillIds.WallBuild:
                    if (planPhase != PlanPhase.Active) break;
                    // The keys steer the clone: the rook itself stands.
                    jyBuildMove = input.move;
                    input.move = Vector3.zero;
                    input.jump = false;
                    input.sprint = false;
                    input.shove = false;
                    input.shoveHeld = false;
                    input.grab = false;
                    break;
            }
        }

        void UpdateSetFourJYCStates(PawnRushSkillParams s, float dt)
        {
            if (jyWalls.Count > 0) JyWallBumps(s);
        }

        void ClearSetFourJYCStates()
        {
            if (jyHeld != null) JyDropHeld(false);
            if (jyUnignore != null) IgnorePiece(jyUnignore, false);
            jyUnignore = null;
            jyThrown = null;
            jyLegs.Clear();
            jyKicked.Clear();
        }
    }
}
