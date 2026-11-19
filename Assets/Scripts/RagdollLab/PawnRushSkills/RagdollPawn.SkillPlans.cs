using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>Where a plan 1-나 skill is: aiming (its key pressed, before the left click), its warning, the move itself,
    /// stuck for a moment after it.</summary>
    public enum PlanPhase : byte { None = 0, Aim = 1, Windup = 2, Active = 3, Recovery = 4 }

    /// <summary>
    /// Pawn Rush 2.0 skill plans, prototypes (JY-skill-lab, 2026-11-19; Docs/Skills/PAWN_RUSH_2_SKILL_PLANS.md). The user:
    /// a skill is "각 기물마다, 체스의 컨셉을 살린, 게임플레이에 변수를 창출해내는 재미있는 무언가", and the plans are to be made
    /// as prototypes and judged by playing them, switched by the room's manager (PawnRushRules.Skills) or the skill test
    /// bed. PawnRushSkillParams.plan picks the plan every pawn plays by: 0 the skills as they are (plan 1-가), 1 plan 1-나,
    /// each piece's own famous chess move:
    ///
    ///   pawn    en passant  an enemy that has just brushed past it (the closest point of their passing, the enemy the
    ///                       faster) can be tripped for a moment: the pawn lunges at its ankles, it goes down sliding on
    ///                       at the speed it ran, the pawn stands where it was.
    ///   knight  fork        a short leap, and a kick out of both hind legs on landing at two spots ahead left and right
    ///                       (shown from the start): enemies on both spots at once go down and fly apart; on one only,
    ///                       a shove.
    ///   bishop  pin         a straight line through the first enemy on it; another enemy or a wall or pillar close behind
    ///                       that one on the same line, and the two are tied by a short rope for a while (the X rope's
    ///                       elastic pull, RagdollPawn.BishopLasso.cs). Nothing behind: a short stagger.
    ///   rook    castling    it aims at a teammate; the teammate answers with its own skill key (as a king's call is
    ///                       answered, RagdollPawn.KingCall.cs) and the two fly to each other's places. A variant to
    ///                       try (castleEnemies): an enemy too, without asking, after a warning.
    ///   queen   check       she names an enemy: the square it stands on turns red, and unless it is off that square in
    ///                       time it is knocked flying. As in chess it can step away, a piece of its side standing
    ///                       between it and the queen takes the blow instead, or the queen is knocked over first.
    ///
    /// Prototypes: the rules and the physics only. The plan's skills keep their own phase (<see cref="PlanPhase"/>) and
    /// leave the plan 1-가 stage (SkillStage) at None, so the effects and sounds made for those skills stay quiet; lines
    /// and rings are drawn by PawnRushPlanMarks from what is public here, the same on every PC (the host sends the phase,
    /// time, point and target in the piece's skill state, RagdollPawn.SkillNet.cs). A few moments reuse an old skill's
    /// sound and hit effect (the pawn's hit, the queen's hit, the knight's take-off and landing, the "no" sound).
    /// Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        PlanPhase planPhase;
        float planTime;
        RagdollPawn planTarget;
        Vector3 planPoint, planDir = Vector3.forward;

        /// <summary>This pawn plays by plan 1-나 (the room's choice, or the test bed's).</summary>
        public bool PlanB => PawnRushSkills != null && PawnRushSkills.plan == PawnRushSkillParams.PlanChessMoves;
        public PlanPhase PlanPhaseNow => planPhase;
        public float PlanTime => planTime;
        public bool PlanBusy => planPhase != PlanPhase.None;
        /// <summary>The piece the skill is about: the one the queen checks, the rook swaps with, the pawn lunges at, the
        /// bishop's line met first (aiming: the one it would).</summary>
        public RagdollPawn PlanTarget => planTarget;
        /// <summary>The checked square's middle, the knight's landing, the end of the bishop's line.</summary>
        public Vector3 PlanPoint => planPoint;
        public Vector3 PlanDir => planDir;

        public static string PlanSkillName(PieceKind kind) => kind switch
        {
            PieceKind.Pawn => "앙파상",
            PieceKind.Knight => "포크",
            PieceKind.Bishop => "핀",
            PieceKind.Rook => "캐슬링",
            PieceKind.Queen => "체크!",
            _ => "",
        };

        /// <summary>The skill's name under the plan this pawn plays by.</summary>
        public string SkillNameNow => PlanB ? PlanSkillName(piece) : SkillName(piece);

        float PlanCooldown(PawnRushSkillParams s) => piece switch
        {
            PieceKind.Pawn => s.passCooldown,
            PieceKind.Knight => s.forkCooldown,
            PieceKind.Bishop => s.pinCooldown,
            PieceKind.Rook => s.castleCooldown,
            PieceKind.Queen => s.checkCooldown,
            _ => 0f,
        };

        // ---------------------------------------------------------------- the step

        /// <summary>The plan's skill this step (the host's), in place of the plan 1-가 skill.</summary>
        void UpdatePlanSkill(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            if (planPhase != PlanPhase.None && (State == PawnState.Ragdoll || Floating))
            {
                // Floored (or in the water) mid-skill: it ends here; a skill already spent runs its cooldown.
                Log($"{DisplayName}: {PlanSkillName(piece)} 끊김 ({(Floating ? "물" : "넘어짐")})");
                bool spent = planPhase != PlanPhase.Aim;
                EndPlanSkill();
                if (spent) StartCooldown(PlanCooldown(s));
            }
            switch (piece)
            {
                case PieceKind.Pawn: UpdatePawnPass(s, press, dt); break;
                case PieceKind.Knight: UpdateKnightFork(s, press, dt); break;
                case PieceKind.Bishop: UpdateBishopPin(s, press, confirm, cancel, dt); break;
                case PieceKind.Rook: UpdateRookCastle(s, press, confirm, cancel, dt); break;
                case PieceKind.Queen: UpdateQueenCheck(s, press, confirm, cancel, dt); break;
            }
        }

        void EndPlanSkill()
        {
            var mate = planTarget;
            if (mate != null && mate.castleOfferBy == this) mate.castleOfferBy = null;
            planPhase = PlanPhase.None;
            planTime = 0f;
            planTarget = null;
            if (dashing) EndDash();
            SkillDetail = "";
        }

        /// <summary>Everything a plan skill leaves running when the skills are cleared (a knockdown, a piece switch, being
        /// carried off). A pin stays: it is a rope between two pieces, not this piece's skill.</summary>
        void ClearPlanSkills()
        {
            EndPlanSkill();
            passTarget = null;
            passLeft = 0f;
            castleOfferBy = null;
            castleAccepted = false;
            if (castleCarry) EndCastleCarry(false);
        }

        /// <summary>Before the run: a piece in a castling's flight has no keys.</summary>
        void PlanKeys()
        {
            if (!castleCarry) return;
            input.move = Vector3.zero;
            input.jump = false;
            input.sprint = false;
            input.shove = false;
            input.shoveHeld = false;
            input.grab = false;
        }

        // ---------------------------------------------------------------- pawn: en passant

        struct PassTrack
        {
            public Vector3 pos;
            public float closing;
        }

        readonly Dictionary<RagdollPawn, PassTrack> passSeen = new Dictionary<RagdollPawn, PassTrack>();
        Vector3 passSelfPos;
        bool passSelfSeen;
        RagdollPawn passTarget;
        float passLeft;

        /// <summary>The enemy that has just brushed past this pawn and can still be tripped (null: none).</summary>
        public RagdollPawn PassTarget => passLeft > 0f ? passTarget : null;
        public float PassLeft => Mathf.Max(0f, passLeft);

        /// <summary>Watches every enemy go by: the moment one stops coming nearer and starts going away (the closest point of
        /// their passing) within passRange, the two passing each other at passSpeed or more and the enemy the faster of the
        /// two, it can be tripped for passWindow seconds. Speeds are taken from the places seen a step ago, so a client can
        /// run it on what it is shown (its own pawn's ring) as the host runs it on the bodies.</summary>
        void TrackPasses(PawnRushSkillParams s, float dt)
        {
            if (dt <= 1e-5f) return;
            Vector3 me = bodies[0].position;
            Vector3 myVel = passSelfSeen ? Flat(me - passSelfPos) / dt : Vector3.zero;
            passSelfPos = me;
            passSelfSeen = true;
            if (passLeft > 0f)
            {
                passLeft -= dt;
                if (passLeft <= 0f || passTarget == null || passTarget.State != PawnState.Active)
                {
                    passTarget = null;
                    passLeft = 0f;
                }
            }
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other)) continue;
                Vector3 op = other.bodies[0].position;
                if (!passSeen.TryGetValue(other, out var seen))
                {
                    passSeen[other] = new PassTrack { pos = op };
                    continue;
                }
                Vector3 ov = Flat(op - seen.pos) / dt;
                Vector3 r = Flat(op - me), v = ov - myVel;
                float closing = Vector3.Dot(r, v);   // under 0 coming nearer, over 0 going away
                bool passed = seen.closing < 0f && closing >= 0f;
                passSeen[other] = new PassTrack { pos = op, closing = closing };
                if (!passed || other.State != PawnState.Active || other.SkillUntargetable) continue;
                if (r.magnitude > s.passRange || Mathf.Abs(op.y - me.y) > 1.2f) continue;
                // (Over 20 m/s is no run: a respawn, a teleport of the test bed.)
                if (v.magnitude < s.passSpeed || ov.magnitude > 20f || ov.magnitude < myVel.magnitude + 0.2f) continue;
                passTarget = other;
                passLeft = s.passWindow;
            }
        }

        void UpdatePawnPass(PawnRushSkillParams s, bool press, float dt)
        {
            TrackPasses(s, dt);
            switch (planPhase)
            {
                case PlanPhase.None:
                    SkillDetail = PassTarget != null ? $"앙파상 가능 → {passTarget.DisplayName} ({passLeft:0.0}초)" : "";
                    if (!press || !CanStartSkill()) break;
                    if (PassTarget == null)
                    {
                        // Nobody has just gone by: the key is ignored (a short "no" sound), no cooldown.
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Pawn);
                        Log("폰 앙파상: 방금 스쳐 지나간 적이 없음 (무시)");
                        break;
                    }
                    SkillUses++;
                    planTarget = passTarget;
                    passTarget = null;
                    passLeft = 0f;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    skillHitSet.Clear();
                    skillDir = FlatDir(planTarget.bodies[0].position - bodies[0].position);
                    StartDash(s.passLungeSpeed, s.passLunge / Mathf.Max(0.1f, s.passLungeSpeed));
                    SkillDetail = $"앙파상 → {planTarget.DisplayName}";
                    Log($"폰 앙파상: {planTarget.DisplayName}의 발목으로 뛰어듦");
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    var t = planTarget;
                    if (t != null && t.State == PawnState.Active && !t.SkillUntargetable)
                    {
                        // It bends onto the piece as it goes, and stops on reaching it.
                        Vector3 d = t.bodies[0].position - bodies[0].position;
                        if (Flat(d).sqrMagnitude > 0.01f) skillDir = Flat(d).normalized;
                        if (Flat(d).magnitude <= s.passReach && Mathf.Abs(d.y) <= 1.2f)
                        {
                            TripEnPassant(s, t);
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
                    if (dashLeft <= 0f)
                    {
                        HaltDash();   // it stands where the piece was
                        planPhase = PlanPhase.None;
                        planTime = 0f;
                        planTarget = null;
                        SkillDetail = "";
                        StartCooldown(s.passCooldown);
                    }
                    break;
                }
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The trip: the piece goes down the way it was running, at passTripSpeed at least (the faster it ran the
        /// further it slides).</summary>
        void TripEnPassant(PawnRushSkillParams s, RagdollPawn t)
        {
            Vector3 tv = Flat(t.bodies[0].linearVelocity);
            Vector3 way = tv.sqrMagnitude > 0.25f ? tv.normalized : skillDir;
            float extra = Mathf.Max(1f, s.passTripSpeed - tv.magnitude);
            string result = SkillHit(t, way * extra + Vector3.up * s.passTripLift, true, "앙파상");
            Fx(SkillFxKind.PawnHit, t, SkillContact(t), skillDir, 1);
            Log($"폰 앙파상 → {t.DisplayName}: 발 걸기 (달리던 속도 {tv.magnitude:0.0} m/s) → {result}");
        }

        // ---------------------------------------------------------------- knight: fork

        /// <summary>One of the fork's two spots (0 left, 1 right of the way it leaps), from the landing point.</summary>
        public Vector3 ForkSpot(int side)
        {
            var s = PawnRushSkills;
            if (s == null) return planPoint;
            return planPoint + Quaternion.Euler(0f, side == 0 ? -s.forkAngle : s.forkAngle, 0f) * planDir * s.forkAhead;
        }

        /// <summary>Where the leap comes down: forkLeap ahead on the floor there, short of a wall in the way.</summary>
        Vector3 ForkLanding(PawnRushSkillParams s, Vector3 dir)
        {
            Vector3 feet = FeetPoint;
            float reach = s.forkLeap;
            if (SolidRay(feet + Vector3.up * 0.35f, dir, reach + 0.3f, out var wall) && Mathf.Abs(wall.normal.y) < 0.5f)
                reach = Mathf.Max(0.4f, wall.distance - 0.4f);
            Vector3 at = feet + dir * reach;
            if (SolidRay(at + Vector3.up * 1.5f, Vector3.down, 5f, out var floor) && floor.normal.y > 0.5f) at.y = floor.point.y;
            return at;
        }

        void UpdateKnightFork(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planDir = AimFlat();
                    planPoint = ForkLanding(s, planDir);
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    facing = planDir;
                    SkillDetail = "포크 준비 (두 자리가 보임)";
                    break;
                case PlanPhase.Windup:
                    // Not rooted: a running knight keeps its speed into the leap, the spots go with it.
                    planTime += dt;
                    planPoint = ForkLanding(s, planDir);
                    if (planTime >= s.forkWindup)
                    {
                        Vector3 p = bodies[0].position;
                        Launch(LeapVelocity(p, planPoint + Vector3.up * standHeight, s.forkFlight, s.forkHeight));
                        facing = planDir;
                        planPhase = PlanPhase.Active;
                        planTime = 0f;
                        SkillDetail = "포크 도약";
                    }
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    if (Climbing || rope != null || planTime > s.forkFlight + 1.2f)
                    {
                        // Caught a ledge on the way, or still not down long after (it leapt off an edge): no kick.
                        EndPlanSkill();
                        StartCooldown(s.forkCooldown);
                    }
                    else if (planTime > 0.2f && Grounded)
                    {
                        ForkLand(s);
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        SkillDetail = "착지 후딜";
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.forkRecovery)
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

        /// <summary>The kick, judged from where it really came down: enemies standing on both spots at once all go down and
        /// fly outward; on one spot only, they are shoved.</summary>
        void ForkLand(PawnRushSkillParams s)
        {
            Vector3 feet = FeetPoint;
            planPoint = feet;
            Vector3 left = ForkSpot(0), right = ForkSpot(1);
            var onLeft = new List<RagdollPawn>();
            var onRight = new List<RagdollPawn>();
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.State == PawnState.Ragdoll || other.SkillUntargetable) continue;
                Vector3 op = other.FeetPoint;
                if (Mathf.Abs(op.y - feet.y) > 1.2f) continue;
                float dl = Flat(op - left).magnitude, dr = Flat(op - right).magnitude;
                if (dl <= s.forkRadius && dl <= dr) onLeft.Add(other);
                else if (dr <= s.forkRadius) onRight.Add(other);
            }
            bool both = onLeft.Count > 0 && onRight.Count > 0;
            int hit = 0;
            foreach (var list in new[] { onLeft, onRight })
                foreach (var other in list)
                {
                    Vector3 away = FlatDir(other.bodies[0].position - bodies[0].position);
                    string result = both
                        ? SkillHit(other, away * s.forkPush + Vector3.up * s.forkLift, true, "포크")
                        : SkillHit(other, away * s.forkSinglePush, false, "포크 (한 자리만)");
                    hit++;
                    Log($"나이트 포크 → {other.DisplayName}: {result}");
                }
            Fx(SkillFxKind.KnightLand, null, feet, planDir, hit, s.forkRadius);
            Log(both ? $"나이트 포크: 두 자리 모두 맞힘 — {hit}명 넘어짐" : hit > 0 ? "나이트 포크: 한 자리만 맞힘 — 밀기만" : "나이트 포크: 빗나감");
        }

        // ---------------------------------------------------------------- bishop: pin

        // The piece a pin holds (the host's; a client's puppet is told): what it is tied to, for how long.
        RagdollPawn pinMate;
        Vector3 pinPost;
        bool pinToPost;
        float pinLeft, pinRest;

        /// <summary>Tied by a bishop's pin now: to another piece (<see cref="PinMate"/>) or to a wall or pillar.</summary>
        public bool Pinned => pinLeft > 0f;
        public float PinLeft => Mathf.Max(0f, pinLeft);
        public RagdollPawn PinMate => pinLeft > 0f && !pinToPost ? pinMate : null;
        public bool PinnedToPost => pinLeft > 0f && pinToPost;
        /// <summary>The other end of the rope: the piece it is tied to (its waist), or the post.</summary>
        public Vector3 PinAnchor => pinToPost || pinMate == null ? pinPost : pinMate.LassoBelly;

        /// <summary>The way the line would go: flat along the aim; an enemy within pinAssist of it, in reach and in sight, is
        /// aimed at.</summary>
        Vector3 PinAim(PawnRushSkillParams s)
        {
            Vector3 flat = AimFlat(), from = LassoHands;
            planTarget = null;
            float best = s.pinAssist;
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable
                    || other.pinLeft > 0f) continue;
                Vector3 to = other.LassoBelly - from;
                float across = Flat(to).magnitude;
                if (across < 0.6f || across > s.pinRange || Mathf.Abs(to.y) > 2f) continue;
                float angle = Vector3.Angle(flat, Flat(to));
                if (angle > best || !ClearSight(from, other.LassoBelly)) continue;
                planTarget = other;
                best = angle;
            }
            return planTarget != null ? FlatDir(planTarget.LassoBelly - from) : flat;
        }

        void UpdateBishopPin(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (press && CanStartSkill())
                    {
                        planPhase = PlanPhase.Aim;
                        planTime = 0f;
                        skillGrabLatch = skillGrabRaw;
                        planDir = PinAim(s);
                        SkillDetail = "핀 조준 (좌클릭 쏘기 · 우클릭/F 취소)";
                    }
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    planDir = PinAim(s);
                    if (Flat(skillMoveRaw).sqrMagnitude < 0.04f) facing = FlatDir(planDir);
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        Log("비숍 핀: 조준 취소");
                        break;
                    }
                    if (confirm)
                    {
                        SkillUses++;
                        FirePin(s);
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        SkillDetail = "핀";
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

        /// <summary>The line, at once: the first enemy on it (short of a wall), then what is close behind that one on the same
        /// line - another enemy, or an upright wall or pillar - and the two are tied. Nothing behind: a short stagger.</summary>
        void FirePin(PawnRushSkillParams s)
        {
            Vector3 from = LassoHands, dir = FlatDir(planDir);
            float reach = s.pinRange;
            if (SolidRay(from, dir, reach, out var ahead)) reach = ahead.distance;
            var first = PinFirst(s, from, dir, 0.5f, reach, null);
            planTarget = first;
            if (first == null)
            {
                planPoint = from + dir * reach;
                Log("비숍 핀: 아무도 안 맞음");
                return;
            }
            Vector3 a = first.LassoBelly;
            float behind = s.pinBehind;
            bool post = SolidRay(a, dir, behind, out var wall) && Mathf.Abs(wall.normal.y) < 0.5f;
            if (post) behind = wall.distance;
            var second = PinFirst(s, a, dir, 0.3f, behind, first);
            if (second != null)
            {
                first.BePinned(second, Vector3.zero, false, s);
                second.BePinned(first, Vector3.zero, false, s);
                planPoint = second.LassoBelly;
                Fx(SkillFxKind.PawnHit, first, first.LassoBelly, dir, 0);
                Log($"비숍 핀: {first.DisplayName}와 {second.DisplayName}를 꿰어 묶음 ({s.pinSeconds:0.#}초, 줄 {s.pinRest:0.#} m)");
            }
            else if (post)
            {
                first.BePinned(null, wall.point, true, s);
                planPoint = wall.point;
                Fx(SkillFxKind.PawnHit, first, first.LassoBelly, dir, 0);
                Log($"비숍 핀: {first.DisplayName}를 뒤의 벽 · 기둥에 묶음 ({s.pinSeconds:0.#}초)");
            }
            else
            {
                planPoint = a + dir * 1.2f;
                first.Stagger(s.pinMissStagger);
                first.LastSkillHit = "핀: 뒤에 아무것도 없음 → 휘청";
                Log($"비숍 핀: {first.DisplayName} 뒤에 아무것도 없음 → 휘청 {s.pinMissStagger:0.#}초");
            }
        }

        /// <summary>The nearest enemy the line takes between <paramref name="min"/> and <paramref name="max"/> along it.</summary>
        RagdollPawn PinFirst(PawnRushSkillParams s, Vector3 from, Vector3 dir, float min, float max, RagdollPawn skip)
        {
            RagdollPawn best = null;
            float bestAlong = max;
            foreach (var other in All)
            {
                if (other == null || other == this || other == skip || !IsEnemyOf(other) || other.State != PawnState.Active
                    || other.SkillUntargetable || other.NetworkPuppet || other.pinLeft > 0f || other.chainCarry || other.kingCarry
                    || other.castleCarry) continue;
                Vector3 to = other.LassoBelly - from;
                if (Mathf.Abs(to.y) > 2f) continue;
                Vector3 flat = Flat(to);
                float along = Vector3.Dot(flat, dir);
                if (along < min || along > bestAlong) continue;
                if ((flat - dir * along).magnitude > s.pinRadius + 0.3f) continue;
                best = other;
                bestAlong = along;
            }
            return best;
        }

        void BePinned(RagdollPawn mate, Vector3 post, bool toPost, PawnRushSkillParams s)
        {
            pinMate = toPost ? null : mate;
            pinPost = post;
            pinToPost = toPost;
            pinLeft = s.pinSeconds;
            pinRest = s.pinRest;
            LastSkillHit = toPost ? "핀: 벽 · 기둥에 묶임" : $"핀: {(mate != null ? mate.DisplayName : "?")}와 묶임";
        }

        /// <summary>The rope is gone, at both ends.</summary>
        void EndPin()
        {
            var mate = pinMate;
            pinLeft = 0f;
            pinMate = null;
            pinToPost = false;
            if (mate != null && mate.pinMate == this) mate.EndPin();
        }

        /// <summary>How fast the pin's rope drags this piece this step (m/s): toward its other end as far as it is stretched
        /// past its length (added to a bishop's X rope's drag, RagdollPawn.UpdateLassoDrag).</summary>
        Vector3 PinPull(PawnRushSkillParams s)
        {
            if (pinLeft <= 0f || s == null) return Vector3.zero;
            Vector3 to = PinAnchor - LassoBelly;
            float d = to.magnitude, stretch = d - pinRest;
            if (stretch <= 0f || d <= 1e-3f) return Vector3.zero;
            return to / d * Mathf.Min(s.pinStiffness * stretch, s.pinPull);
        }

        /// <summary>A pinned piece's step (the host's): the time runs out, the other end is gone, it is in the water or out of
        /// every skill's reach (respawned, on the finish), or the rope is stretched past pinBreak and snaps.</summary>
        void UpdatePin(PawnRushSkillParams s, float dt)
        {
            if (pinLeft <= 0f) return;
            pinLeft -= dt;
            bool lost = !pinToPost && (pinMate == null || pinMate.pinMate != this || pinMate.pinLeft <= 0f);
            float stretch = Vector3.Distance(PinAnchor, LassoBelly) - pinRest;
            if (pinLeft > 0f && !lost && !Floating && !SkillUntargetable && stretch <= s.pinBreak) return;
            if (stretch > s.pinBreak) Log($"{DisplayName}: 핀 줄이 너무 늘어나 끊어짐 ({stretch:0.0} m)");
            EndPin();
        }

        // ---------------------------------------------------------------- rook: castling

        // The piece a rook offers a castling to (the host's): the rook, how long ago, its answer.
        RagdollPawn castleOfferBy;
        float castleOfferAge;
        bool castleAccepted;
        // The swap's flight, either piece.
        bool castleCarry, castleForced;
        Vector3 castleFrom, castleTo;
        float castleT, castleTime, castleArcHeight, castleSideWidth;

        /// <summary>In a castling's flight now (the host's).</summary>
        public bool CastleFlying => castleCarry;

        /// <summary>A teammate rook that offers this piece a castling now (any PC: the rook's state comes from the host).</summary>
        public RagdollPawn CastleOfferingMe()
        {
            foreach (var p in All)
                if (p != null && p != this && p.piece == PieceKind.Rook && p.planPhase == PlanPhase.Windup && p.planTarget == this
                    && p.IsAllyOf(this)) return p;
            return null;
        }

        /// <summary>The seconds a teammate still has to answer (0 unless the rook waits for one).</summary>
        public float CastleOfferLeft =>
            piece == PieceKind.Rook && planPhase == PlanPhase.Windup && PawnRushSkills != null ? Mathf.Max(0f, PawnRushSkills.castleWindow - planTime) : 0f;

        /// <summary>The piece the rook would castle with: a teammate (an enemy too with castleEnemies) standing within
        /// castleAssist of the aim, in reach and in sight; the nearest to the aim.</summary>
        RagdollPawn FindCastleMate(PawnRushSkillParams s)
        {
            Vector3 flat = AimFlat(), from = bodies[(int)BodyId.Chest].position;
            RagdollPawn best = null;
            float bestAngle = s.castleAssist;
            foreach (var other in All)
            {
                if (other == null || other == this || other.State != PawnState.Active || other.SkillUntargetable || other.Floating
                    || other.chainCarry || other.kingCarry || other.castleCarry) continue;
                if (!IsAllyOf(other) && !(s.castleEnemies && IsEnemyOf(other))) continue;
                Vector3 to = other.bodies[(int)BodyId.Chest].position - from;
                float across = Flat(to).magnitude;
                if (across < 1.5f || across > s.castleRange) continue;
                float angle = Vector3.Angle(flat, Flat(to));
                if (angle > bestAngle || !ClearSight(from, other.bodies[(int)BodyId.Chest].position)) continue;
                best = other;
                bestAngle = angle;
            }
            return best;
        }

        void UpdateRookCastle(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (press && CanStartSkill())
                    {
                        planPhase = PlanPhase.Aim;
                        planTime = 0f;
                        skillGrabLatch = skillGrabRaw;
                        planTarget = FindCastleMate(s);
                        SkillDetail = "캐슬링 조준 (팀원을 보고 좌클릭 · 우클릭/F 취소)";
                    }
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    planTarget = FindCastleMate(s);
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        Log("룩 캐슬링: 조준 취소");
                        break;
                    }
                    if (!confirm) break;
                    if (planTarget == null)
                    {
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Rook);
                        Log("룩 캐슬링: 바꿀 기물이 조준에 없음");
                        break;
                    }
                    SkillUses++;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    if (IsAllyOf(planTarget))
                    {
                        planTarget.castleOfferBy = this;
                        planTarget.castleOfferAge = 0f;
                        planTarget.castleAccepted = false;
                        SkillDetail = $"캐슬링 제안 → {planTarget.DisplayName} (스킬 키로 받기)";
                        Log($"룩 캐슬링 → {planTarget.DisplayName}(팀원): {s.castleWindow:0.#}초 안에 스킬 키로 받기");
                    }
                    else
                    {
                        SkillDetail = $"캐슬링 예고 → {planTarget.DisplayName}";
                        Log($"룩 캐슬링 → {planTarget.DisplayName}(적): {s.castleEnemyWindup:0.#}초 뒤 자리 바꿈");
                    }
                    break;
                case PlanPhase.Windup:
                {
                    planTime += dt;
                    var mate = planTarget;
                    if (Staggered || bindLeft > 0f || mate == null || mate.SkillUntargetable || mate.Floating || mate.castleCarry)
                    {
                        Log("룩 캐슬링: 끊김");
                        EndPlanSkill();
                        StartCooldown(s.castleCooldown);
                        break;
                    }
                    if (IsAllyOf(mate))
                    {
                        if (mate.castleAccepted)
                        {
                            mate.castleAccepted = false;
                            StartCastle(s, mate, false);
                        }
                        else if (planTime >= s.castleWindow)
                        {
                            Log($"룩 캐슬링: {mate.DisplayName}가 받지 않음");
                            EndPlanSkill();
                            StartCooldown(s.castleRefusedCooldown);
                        }
                        break;
                    }
                    // The variant: an enemy, without asking. It gets out of it behind a wall or in the air at the end.
                    if (planTime < s.castleEnemyWindup) break;
                    bool seen = ClearSight(bodies[(int)BodyId.Chest].position, mate.bodies[(int)BodyId.Chest].position);
                    if (!seen || !mate.OnTheFloor || mate.State != PawnState.Active)
                    {
                        Log($"룩 캐슬링: {mate.DisplayName}가 피함 ({(!seen ? "가려짐" : "공중")})");
                        EndPlanSkill();
                        StartCooldown(s.castleCooldown);
                        break;
                    }
                    StartCastle(s, mate, true);
                    break;
                }
                case PlanPhase.Active:
                    // (Its own flight holds the step back; this runs once it has come down or been dropped.)
                    planTime += dt;
                    if (!castleCarry)
                    {
                        EndPlanSkill();
                        StartCooldown(s.castleCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>A castling offered to this piece this step (the host's): its own skill key answers yes (a test dummy answers
        /// by itself). Returns the key press if it was not used.</summary>
        bool UpdatePlanMate(PawnRushSkillParams s, bool press, float dt)
        {
            var rook = castleOfferBy;
            if (rook == null) return press;
            if (rook.piece != PieceKind.Rook || rook.planPhase != PlanPhase.Windup || rook.planTarget != this)
            {
                castleOfferBy = null;
                return press;
            }
            castleOfferAge += dt;
            bool yes = SkillBot ? castleOfferAge >= s.castleDummyAnswer : press;
            if (!yes) return press;
            // Down, tied, held or in the water: the answer waits (the key is used up).
            if (State != PawnState.Active || bindLeft > 0f || BeingHeld || Floating) return false;
            castleAccepted = true;
            castleOfferBy = null;
            Log($"{DisplayName}: 캐슬링을 받음");
            return false;
        }

        void StartCastle(PawnRushSkillParams s, RagdollPawn mate, bool forced)
        {
            Vector3 a = bodies[0].position, b = mate.bodies[0].position;
            float aFloor = FeetPoint.y, bFloor = mate.FeetPoint.y;
            // Each lands standing on the other's floor.
            mate.BeginCastleCarry(new Vector3(a.x, aFloor + mate.standHeight, a.z), s, forced, false);
            BeginCastleCarry(new Vector3(b.x, bFloor + standHeight, b.z), s, false, true);
            planPhase = PlanPhase.Active;
            planTime = 0f;
            planTarget = mate;
            SkillDetail = "캐슬링!";
            Log($"룩 캐슬링: {DisplayName} ↔ {mate.DisplayName} 자리 바꿈 ({Flat(b - a).magnitude:0.0} m, {s.castleFlight:0.0#}초)");
        }

        void BeginCastleCarry(Vector3 to, PawnRushSkillParams s, bool forced, bool own)
        {
            if (!own) ClearSkills();   // the other piece's own skill stops; the cooldown it has keeps running
            DropHook();
            DropRope();
            EndClimbPose();
            LetGo(0.3f);
            castleCarry = true;
            castleForced = forced;
            castleFrom = bodies[0].position;
            castleTo = to;
            castleT = 0f;
            castleTime = Mathf.Max(0.1f, s.castleFlight);
            // The rook's arc is the higher, and each bows out to its own right: they pass each other, not through.
            castleArcHeight = own ? s.castleArc : s.castleArc * 0.7f;
            castleSideWidth = s.castleSide;
        }

        Vector3 CastlePath(float t)
        {
            float u = Mathf.Clamp01(t / castleTime);
            Vector3 along = Flat(castleTo - castleFrom);
            Vector3 side = along.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, along.normalized) : Vector3.zero;
            float bow = 4f * u * (1f - u);
            return Vector3.Lerp(castleFrom, castleTo, u) + Vector3.up * (castleArcHeight * bow) + side * (castleSideWidth * bow);
        }

        /// <summary>The swap's flight this step (the host's). True while it runs: the rest of the skills wait.</summary>
        bool UpdatePlanCarry(PawnRushSkillParams s, float dt)
        {
            if (!castleCarry) return false;
            if (State != PawnState.Active || Floating)
            {
                EndCastleCarry(false);
                return false;
            }
            castleT += dt;
            grabImmune = Mathf.Max(grabImmune, 0.25f);
            if (castleT >= castleTime)
            {
                EndCastleCarry(true);
                return false;
            }
            Vector3 at = bodies[0].position, next = CastlePath(castleT + dt);
            Vector3 step = next - at;
            // Something solid on the way drops it there (as a rook's chain does).
            if (castleT > 0.1f && step.sqrMagnitude > 1e-6f && ChainBlocked(at, step))
            {
                Log($"{DisplayName}: 캐슬링 도중 장애물에 걸려 떨어짐");
                EndCastleCarry(false);
                return false;
            }
            Fly(next, step / Mathf.Max(1e-4f, dt), 0f, default, 18f);
            return true;
        }

        void EndCastleCarry(bool arrived)
        {
            if (!castleCarry) return;
            castleCarry = false;
            if (!arrived)
            {
                EndFlight(bodies[0].linearVelocity * 0.5f);
                return;
            }
            EndFlight(Vector3.zero);
            KeepSkillLandingSafe();
            if (castleForced && PawnRushSkills != null)
            {
                Stagger(PawnRushSkills.castleEnemyStagger);
                LastSkillHit = "캐슬링: 자리가 바뀜 → 휘청";
            }
        }

        // ---------------------------------------------------------------- queen: check

        /// <summary>A queen that has this piece in check now (any PC).</summary>
        public RagdollPawn CheckedBy()
        {
            foreach (var p in All)
                if (p != null && p != this && p.piece == PieceKind.Queen && p.planPhase == PlanPhase.Active && p.planTarget == this) return p;
            return null;
        }

        /// <summary>The seconds the checked piece still has to leave the square.</summary>
        public float CheckLeft =>
            piece == PieceKind.Queen && planPhase == PlanPhase.Active && PawnRushSkills != null ? Mathf.Max(0f, PawnRushSkills.checkSeconds - planTime) : 0f;

        /// <summary>The enemy the queen would check: standing within checkAssist of the aim, in reach and in sight.</summary>
        RagdollPawn FindCheckTarget(PawnRushSkillParams s)
        {
            Vector3 flat = AimFlat(), eye = bodies[(int)BodyId.Head].position;
            RagdollPawn best = null;
            float bestAngle = s.checkAssist;
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable
                    || other.chainCarry || other.kingCarry || other.castleCarry) continue;
                Vector3 to = other.bodies[0].position - bodies[0].position;
                float across = Flat(to).magnitude;
                if (across < 0.8f || across > s.checkRange || Mathf.Abs(to.y) > 2.5f) continue;
                float angle = Vector3.Angle(flat, Flat(to));
                if (angle > bestAngle || !ClearSight(eye, other.bodies[(int)BodyId.Head].position)) continue;
                best = other;
                bestAngle = angle;
            }
            return best;
        }

        void UpdateQueenCheck(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (press && CanStartSkill())
                    {
                        planPhase = PlanPhase.Aim;
                        planTime = 0f;
                        skillGrabLatch = skillGrabRaw;
                        planTarget = FindCheckTarget(s);
                        SkillDetail = "체크 조준 (적을 보고 좌클릭 · 우클릭/F 취소)";
                    }
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    planTarget = FindCheckTarget(s);
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        Log("퀸 체크: 조준 취소");
                        break;
                    }
                    if (!confirm) break;
                    if (planTarget == null)
                    {
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Queen);
                        Log("퀸 체크: 조준에 적이 없음");
                        break;
                    }
                    SkillUses++;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    planPoint = planTarget.FeetPoint;   // the square: where it stands now
                    facing = FlatDir(planTarget.bodies[0].position - bodies[0].position);
                    SkillDetail = $"체크! → {planTarget.DisplayName}";
                    Log($"퀸: 체크! → {planTarget.DisplayName} ({s.checkSeconds:0.#}초 안에 칸 밖으로)");
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    var t = planTarget;
                    if (Staggered || bindLeft > 0f)
                    {
                        // "Taken": the queen was hit before her check came down.
                        Log("퀸 체크: 퀸이 맞아 취소됨");
                        EndPlanSkill();
                        StartCooldown(s.checkCooldown);
                        break;
                    }
                    if (t == null || t.SkillUntargetable)
                    {
                        EndPlanSkill();
                        StartCooldown(s.checkCooldown);
                        break;
                    }
                    if (planTime < s.checkSeconds) break;
                    ResolveCheck(s, t);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    SkillDetail = "체크 후딜";
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.checkRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.checkCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The check comes down: a piece still on its square is knocked flying away from the queen - unless a piece of
        /// its side stands between it and her, which takes the blow instead (the block).</summary>
        void ResolveCheck(PawnRushSkillParams s, RagdollPawn t)
        {
            Vector3 feet = t.FeetPoint;
            bool onSquare = Flat(feet - planPoint).magnitude <= s.checkSquare && Mathf.Abs(feet.y - planPoint.y) <= 1.5f;
            if (!onSquare)
            {
                Log($"퀸 체크: {t.DisplayName}가 칸을 벗어남 (피함)");
                return;
            }
            var blocker = CheckBlocker(s, t);
            var hit = blocker != null ? blocker : t;
            Vector3 away = FlatDir(hit.bodies[0].position - bodies[0].position);
            string result = SkillHit(hit, away * s.checkPush + Vector3.up * s.checkLift, true, blocker != null ? "체크 (대신 막음)" : "체크");
            Fx(SkillFxKind.QueenHit, hit, hit.bodies[0].position, away, 1);
            Log(blocker != null
                ? $"퀸 체크: {blocker.DisplayName}가 몸으로 막음 → {result}"
                : $"퀸 체크 → {t.DisplayName}: 칸에 남아 있음 → {result}");
        }

        /// <summary>A standing piece of the checked one's side between it and the queen, within checkBlock of the line (the
        /// nearest to the queen).</summary>
        RagdollPawn CheckBlocker(PawnRushSkillParams s, RagdollPawn t)
        {
            Vector3 q = bodies[0].position, line = Flat(t.bodies[0].position - q);
            float length = line.magnitude;
            if (length < 1.2f) return null;
            Vector3 dir = line / length;
            RagdollPawn best = null;
            float bestAlong = length - 0.5f;
            foreach (var other in All)
            {
                if (other == null || other == this || other == t || !IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable) continue;
                Vector3 to = other.bodies[0].position - q;
                if (Mathf.Abs(to.y) > 1.5f) continue;
                Vector3 flat = Flat(to);
                float along = Vector3.Dot(flat, dir);
                if (along < 0.5f || along > bestAlong) continue;
                if ((flat - dir * along).magnitude > s.checkBlock) continue;
                best = other;
                bestAlong = along;
            }
            return best;
        }

        // ---------------------------------------------------------------- over the network (RagdollPawn.SkillNet.cs)

        // Flags2 bits of a piece's skill state (128 is a retired mode's): the plan it plays by, and a pin on it.
        const byte NetPlanB = 1, NetPinned = 2, NetPinPost = 4;

        /// <summary>Something of the plan's is worth sending: its skill under way, or a pin on this piece.</summary>
        bool PlanNetBusy => planPhase != PlanPhase.None || pinLeft > 0f;

        /// <summary>Host: the plan's part of this piece's skill state. D its phase, A its time, P its point, the direction its
        /// way, Target its target; a pinned piece: Other the piece it is tied to, Q the post, C the seconds left (1/10 s).</summary>
        void CapturePlanNet(ref SkillWireState s, Func<RagdollPawn, byte> refOf, ref Vector3 dir, ref Vector3 p, ref Vector3 q, ref RagdollPawn target)
        {
            s.Flags2 |= NetPlanB;
            s.D = (byte)planPhase;
            s.A = planTime;
            dir = planDir;
            p = planPoint;
            target = planTarget;
            if (pinLeft <= 0f) return;
            s.Flags2 |= NetPinned;
            if (pinToPost) s.Flags2 |= NetPinPost;
            q = pinPost;
            s.C = (byte)Mathf.Clamp(Mathf.RoundToInt(pinLeft * 10f), 0, 255);
            if (pinMate != null) s.Other = refOf(pinMate);
        }

        /// <summary>Client: the host's state for this puppet says which plan the match plays by and what its skill is at.</summary>
        void ApplyPlanNet(in SkillWireState s, Vector3 dir, Vector3 p, Vector3 q, RagdollPawn target, Func<byte, RagdollPawn> pawnOf)
        {
            planPhase = (PlanPhase)s.D;
            planTime = s.A;
            if (dir.sqrMagnitude > 0.5f) planDir = dir.normalized;
            planPoint = p;
            planTarget = target;
            bool pinned = (s.Flags2 & NetPinned) != 0;
            pinLeft = pinned ? Mathf.Max(0.05f, s.C / 10f) : 0f;
            pinToPost = pinned && (s.Flags2 & NetPinPost) != 0;
            pinPost = q;
            pinMate = pinned && s.Other != SkillWire.None ? pawnOf(s.Other) : null;
            if (pinned && PawnRushSkills != null) pinRest = PawnRushSkills.pinRest;
        }

        /// <summary>Client: this piece is not in the host's packet, or plays by the other plan.</summary>
        void ClearPlanNet()
        {
            planPhase = PlanPhase.None;
            planTime = 0f;
            planTarget = null;
            pinLeft = 0f;
            pinMate = null;
            pinToPost = false;
        }

        /// <summary>Client: between two of the host's packets the plan's clocks run on.</summary>
        void AdvancePlanView(float dt)
        {
            if (planPhase != PlanPhase.None) planTime += dt;
            if (pinLeft > 0f) pinLeft = Mathf.Max(0.01f, pinLeft - dt);
        }
    }
}
