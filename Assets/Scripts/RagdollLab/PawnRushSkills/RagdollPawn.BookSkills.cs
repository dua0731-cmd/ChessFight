using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Bundle A of the skill idea book (Docs/Skills/PAWN_RUSH_2_SKILL_IDEAS.md 13장; JY-skill-lab, 2026-11-20, the user:
    /// "A 묶음부터"): one skill a piece, none of which knocks anyone down - they take places away and let hits pass. On
    /// the slots' machinery like the rest of the new skills (RagdollPawn.SkillPlans.cs).
    ///
    ///   터치 무브 · 자두브 (pawn, P5)  the palm out at arm's length. An enemy it touches cannot stand still for a while
    ///                                 (touch-move: a touched piece must move): it may turn, its feet keep going. A
    ///                                 teammate that is down, staggered or tied stands at once ("j'adoube").
    ///   뒷발차기 (knight, N4)          remade on 11-24 as the set 3판's basic (KITS 6.13): it no longer turns round - the
    ///                                 rear bobs up and the hind legs kick out behind the way it faces; an enemy there
    ///                                 flies back and falls, and the knight springs on ahead. (It was: turn the back, a
    ///                                 strip, the hit lets go of what it holds and its hands go numb, no knockdown.)
    ///   반대색 비숍 (bishop, B1)        for a while enemy bodies and skills pass through it, and it takes hold of and
    ///                                 pushes no enemy itself; standing on the crown platform it does not count
    ///                                 (bishops of opposite colours never meet). Still in an enemy when it ends: the
    ///                                 two are thrown apart.
    ///   뒤에서 밀기 (rook, R1)          the piece right in front of its hands is sent straight ahead on a low arc after
    ///                                 a warning (Tarrasch: rooks belong behind passed pawns). Whoever stands nearest
    ///                                 is the one sent - an enemy that steps in between takes the ride - and a piece
    ///                                 that jumps is not sent. It lands short of a gap: nothing is jumped over.
    ///   두 마리 토끼 (queen, Q4)        the two enemies nearest her aim are tied waist to waist by an elastic (a double
    ///                                 attack): a pin's rope (RagdollPawn.SkillPlans.cs) with its own numbers, which
    ///                                 does not break when stretched but snaps the two back at each other.
    ///
    /// Prototypes: the rules and the physics, lines and rings for marks (PawnRushPlanMarks), no effects, sounds or
    /// poses of their own. A ghost is drawn as rings only (its body is not see-through yet), and while it lasts the
    /// bishop's own team's skills do not take on it either (SkillUntargetable). Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        // ---------------------------------------------------------------- what they leave on a piece

        // Touched: it cannot stand still. Numb: its hands hold nothing. A ghost: enemy bodies and skills pass through
        // it. Shot: flying where a rook sent it. The host's; a client's puppet is told the first three.
        float touchLeft, numbLeft, ghostLeft, shotLeft, shotAge;
        Vector3 touchDir = Vector3.forward, shotDir = Vector3.forward;
        bool ghostOff;   // its collisions with the enemy pieces are switched off now (the host's)
        readonly HashSet<RagdollPawn> shotBumped = new HashSet<RagdollPawn>();
        // A pin that is the queen's elastic: its own pull, and the wait before it may snap the pair together again.
        bool pinIsTie;
        float pinStiff, pinPullMax, tieSnapWait;
        // The second enemy of the queen's two (the first is the plan's target; the plan's point is this one's waist,
        // which every PC has for her marks).
        RagdollPawn hareOther;

        public bool Touched => touchLeft > 0f;
        public float TouchLeft => Mathf.Max(0f, touchLeft);
        public bool Numb => numbLeft > 0f;
        public float NumbLeft => Mathf.Max(0f, numbLeft);
        /// <summary>Enemy bodies and skills pass through it now (the opposite-coloured bishop).</summary>
        public bool Ghosted => ghostLeft > 0f;
        public float GhostLeft => Mathf.Max(0f, ghostLeft);
        /// <summary>Tied to another piece by the queen's elastic (a pin with its own numbers).</summary>
        public bool Tied => pinLeft > 0f && pinIsTie;

        bool HoldsSomething => Grabbing || Climbing || OnRope || rope != null || hookPhase != HookPhase.None
                               || lassoPhase != LassoPhase.None || chainPhase != ChainPhase.None;

        /// <summary>A teammate's touch would set it straight: down, staggered, squashed, tied up, pinned, touched or numb.</summary>
        bool NeedsAdjust => (State == PawnState.Ragdoll && !Diving) || Staggered || Squashed || bindLeft > 0f || pinLeft > 0f
                            || touchLeft > 0f || numbLeft > 0f;

        /// <summary>PawnHand: a ghost takes hold of no enemy.</summary>
        public bool SkillCannotGrab(RagdollPawn other) => other != null && ghostLeft > 0f && Teams.AreEnemies(team, other.team);

        // The crown platform does not count a ghost standing on it (CrownFinish asks through Gameplay, which knows
        // nothing of the skills).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void HookPlatformCount() =>
            CourseSkills.OffTheCount = driver => driver is RagdollDriver ragdoll && ragdoll.Pawn != null && ragdoll.Pawn.Ghosted;

        /// <summary>Before the run (PlanKeys): a touched piece's feet keep going and numb hands take hold of nothing; then
        /// what each of these skills lets the keys do in its phase (a skill that roots the piece holds a touched one too).</summary>
        void BookKeys()
        {
            if (numbLeft > 0f) input.grab = false;
            if (touchLeft > 0f && State == PawnState.Active && !Staggered && !Squashed && bindLeft <= 0f && !Floating && !Climbing
                && rope == null && !castleCarry && !chainCarry && !kingCarry)
            {
                // It may turn, not stop: with no key down, the way it last went.
                Vector3 m = Flat(input.move);
                if (m.sqrMagnitude > 0.04f) touchDir = m.normalized;
                input.move = touchDir;
            }
            if (planPhase == PlanPhase.None) return;
            switch (activeSkill)
            {
                case PawnRushSkillIds.TouchMove:
                    input.sprint = false;   // it walks through its reach
                    break;
                case PawnRushSkillIds.BackKick:
                case PawnRushSkillIds.BackKick4:
                    // It runs on through the bob: only the jump and the dive wait for the kick.
                    if (planPhase != PlanPhase.Windup) break;
                    input.jump = false;
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
                case PawnRushSkillIds.BackPush:
                case PawnRushSkillIds.TwoHares:
                    input.move = Vector3.zero;
                    input.jump = false;
                    input.sprint = false;
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
            }
        }

        /// <summary>Every step (UpdateSkills): the states these skills leave on a piece run out.</summary>
        void UpdateBookStates(PawnRushSkillParams s, float dt)
        {
            // (In the water, or just back from a respawn: nothing of it is left.)
            bool gone = Floating || skillGuardLeft > 0f;
            if (touchLeft > 0f)
            {
                touchLeft = gone ? 0f : touchLeft - dt;
                // (Over: the move it was made to make is not left standing in a piece nobody is steering, a test dummy.)
                if (touchLeft <= 0f) input.move = Vector3.zero;
            }
            if (numbLeft > 0f) numbLeft = gone ? 0f : numbLeft - dt;
            if (ghostOff || ghostLeft > 0f)
            {
                // (A ghost this PC was only told of - it has just taken the match over as host - runs out here too.)
                ghostLeft -= dt;
                if (ghostOff) grabImmune = Mathf.Max(grabImmune, 0.2f);   // no hand takes hold of it
                if (ghostLeft <= 0f || Floating) EndGhost(s);
            }
            if (shotLeft > 0f) UpdateShot(s, dt);
        }

        /// <summary>What these skills leave running on the piece that casts them, when its skill ends.</summary>
        void ClearBookSkills()
        {
            hareOther = null;
        }

        // ---------------------------------------------------------------- 터치 무브 · 자두브 (the pawn's)

        /// <summary>The piece the palm would touch: the nearest within reach and the cone of the aim - an enemy, or a teammate
        /// that needs setting straight (one standing free is left alone).</summary>
        RagdollPawn TouchPick(PawnRushSkillParams s, Vector3 dir)
        {
            Vector3 from = bodies[(int)BodyId.Chest].position;
            RagdollPawn best = null;
            float bestD = s.touchReach;
            foreach (var other in All)
            {
                if (other == null || other == this || other.NetworkPuppet || other.Floating || other.chainCarry || other.kingCarry
                    || other.castleCarry) continue;
                if (IsAllyOf(other) ? !other.NeedsAdjust : !IsEnemyOf(other) || other.SkillUntargetable) continue;
                Vector3 to = other.bodies[(int)BodyId.Chest].position - from;
                float across = Flat(to).magnitude;
                if (across > bestD || Mathf.Abs(to.y) > 1.2f) continue;
                if (across > 0.35f && Vector3.Angle(dir, Flat(to)) > s.touchCone) continue;
                best = other;
                bestD = across;
            }
            return best;
        }

        void UpdateTouchMove(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planDir = AimFlat();
                    planTarget = TouchPick(s, planDir);
                    planPoint = bodies[(int)BodyId.Chest].position + planDir * s.touchReach;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    SkillDetail = "손 내밀기";
                    break;
                case PlanPhase.Windup:
                    // The palm follows the aim until it lands; the pawn walks on meanwhile (no sprint).
                    planTime += dt;
                    planDir = AimFlat();
                    if (Flat(skillMoveRaw).sqrMagnitude < 0.04f) facing = planDir;
                    planTarget = TouchPick(s, planDir);
                    planPoint = bodies[(int)BodyId.Chest].position + planDir * s.touchReach;
                    if (planTime >= s.touchWindup)
                    {
                        planNote = (byte)(Touch(s, planTarget) ? 1 : 0);
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        SkillDetail = "터치 무브";
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.touchRecovery)
                    {
                        bool touched = (planNote & 1) != 0;
                        EndPlanSkill();
                        StartCooldown(touched ? s.touchCooldown : s.touchMissCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The palm lands: an enemy cannot stand still for a while, a teammate is set straight. False: on nobody.</summary>
        bool Touch(PawnRushSkillParams s, RagdollPawn t)
        {
            if (t == null)
            {
                Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Pawn);
                Log("터치 무브: 손이 닿는 곳에 아무도 없음");
                return false;
            }
            Vector3 way = FlatDir(t.bodies[0].position - bodies[0].position);
            if (IsAllyOf(t))
            {
                t.BeAdjusted(s);
                Fx(SkillFxKind.PawnHelp, t, t.bodies[0].position, way);
                Log($"자두브 → {t.DisplayName}: 바로 섬 · 기상 보호 {s.touchGuard:0.#}초");
                return true;
            }
            string result = t.BeTouched(s.touchSeconds);
            Fx(SkillFxKind.PawnHit, t, SkillContact(t), way, 0);
            Log($"터치 무브 → {t.DisplayName}: {result}");
            return true;
        }

        string BeTouched(float seconds)
        {
            if (NetworkPuppet) return "-";
            if (SkillUntargetable)
            {
                LastSkillHit = "터치 무브: 스킬 대상 아님";
                return "대상 아님";
            }
            touchLeft = Mathf.Max(touchLeft, seconds);
            // The way it was going; standing, the way it faces.
            Vector3 going = Flat(bodies[0].linearVelocity) - carryVel;
            touchDir = going.sqrMagnitude > 0.6f ? going.normalized : FlatDir(facing);
            LastSkillHit = $"터치 무브: {seconds:0.#}초 동안 멈출 수 없음";
            return $"{seconds:0.#}초 동안 멈출 수 없음";
        }

        /// <summary>"J'adoube": set straight by a teammate's touch. Down, it gets up at once; whatever held it (a stagger, a
        /// squash, a bishop's rope, a pin, a touch, numb hands) is gone, and no skill floors it for touchGuard seconds.</summary>
        void BeAdjusted(PawnRushSkillParams s)
        {
            if (NetworkPuppet) return;
            staggerTimer = 0f;
            squashTimer = 0f;
            bindLeft = 0f;
            touchLeft = numbLeft = 0f;
            EndPin();
            if (State == PawnState.Ragdoll && !Diving)
            {
                BeginGetUp(P);
                pendingGuard = s.touchGuard;
            }
            else getUpGuardLeft = Mathf.Max(getUpGuardLeft, s.touchGuard);
            LastSkillHit = "자두브: 바로 섬";
        }

        // ---------------------------------------------------------------- 뒷발차기 (the knight's)

        // 11-24 (KITS 6.13, the set 3판's basic, the user: "추천 둘로"): it does not turn round. Its rear bobs up for
        // kickWindup (the warning), then the hind legs kick out behind the way it faces: every enemy in the fan there
        // (kickReach, kickCone) flies back kickThrow metres and falls for kickKnockHold - a basic's push, so short of an
        // edge - and the knight springs kickBounce metres on ahead, hit or miss. planDir is the way it faces (the kick goes
        // the other way); the marks draw the fan behind from it.
        void UpdateBackKick(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planDir = FlatDir(facing);
                    planPoint = FeetPoint;
                    planTarget = null;
                    planNote = 0;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    KickBob(s);
                    SkillDetail = "엉덩이 들썩 (뒷발 준비)";
                    Fx(SkillFxKind.S3Kick, null, FeetPoint, planDir, 0);
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    // (Its back as it runs on: the kick goes behind whichever way it faces at the moment it kicks.)
                    planDir = FlatDir(facing);
                    planPoint = FeetPoint;
                    if (planTime >= s.kickWindup)
                    {
                        HindKick(s);
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        SkillDetail = "뒷발차기!";
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    // (It waits to come down from the spring, a second at most.)
                    if (planTime >= s.kickRecovery && (Grounded || planTime >= 1f))
                    {
                        EndPlanSkill();
                        StartCooldown(activeSkill == PawnRushSkillIds.BackKick4 ? s.s4Basic : s.kickCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The warning: the rear bobs up and the head dips (a horse about to kick).</summary>
        void KickBob(PawnRushSkillParams s)
        {
            bodies[(int)BodyId.Hips].linearVelocity += Vector3.up * s.kickBob;
            bodies[(int)BodyId.Head].linearVelocity += Vector3.down * (s.kickBob * 0.5f);
        }

        /// <summary>The hind legs: every enemy standing in the fan behind flies back and falls; the knight springs on ahead
        /// and its feet shoot out behind.</summary>
        void HindKick(PawnRushSkillParams s)
        {
            Vector3 me = bodies[0].position, back = -planDir;
            float half = s.kickCone * 0.5f;
            int n = 0;
            foreach (var other in All)
            {
                if (!UltFoe(other) || other.NetworkPuppet) continue;
                Vector3 to = other.bodies[0].position - me;
                if (Mathf.Abs(to.y) > 1.2f) continue;
                Vector3 flat = Flat(to);
                float d = flat.magnitude;
                if (d > s.kickReach + 0.3f) continue;   // (to its middle: the reach and half a body)
                if (d > 0.05f && Vector3.Angle(back, flat) > half) continue;
                n++;
                Vector3 away = d > 0.05f ? flat / d : back;
                // The set 4판's (11-26, ROUND1 4.2): a bang over no edge guard, and 헤롱헤롱 on top (RagdollPawn.SetFour.cs).
                string result = activeSkill == PawnRushSkillIds.BackKick4
                    ? Hit4(other, away, 2, Cc4.Dizzy, s.kick4Dizzy, "뒷발차기", n)
                    : SkillHit(other, S3Push(away, s.kickThrow), true, "뒷발차기", false, s.kickKnockHold, true);
                Fx(SkillFxKind.PawnHit, other, SkillContact(other), away, 1);
                FeelHit(other, SkillContact(other), away, FeelHard);
                Log($"뒷발차기 → {other.DisplayName}: {result}");
            }
            if (n == 0) Log("뒷발차기: 빗나감 (반동만)");
            KickSpring(s);
            // The pose of the kick: the feet go out behind (after the spring, which sets every body's speed).
            bodies[(int)BodyId.FootL].linearVelocity += back * s.kickLegs + Vector3.up * 1.5f;
            bodies[(int)BodyId.FootR].linearVelocity += back * s.kickLegs + Vector3.up * 1.5f;
            Fx(SkillFxKind.S3Kick, null, FeetPoint, planDir, Mathf.Min(254, n) + 1);
        }

        /// <summary>The recoil: a low hop that goes kickBounce metres further than the run alone would carry it, short of an
        /// edge (a basic's push, the system's safety rule; the run itself may still carry it over).</summary>
        void KickSpring(PawnRushSkillParams s)
        {
            float g = Mathf.Max(0.01f, -Physics.gravity.y);
            float up = Mathf.Max(0.5f, s.kickHop);
            float air = 2f * up / g;
            Vector3 run = Flat(bodies[0].linearVelocity);
            float along = Mathf.Max(0f, Vector3.Dot(run, planDir));
            Vector3 feet = FeetPoint;
            float room = s.kickBounce;
            while (room > 0.05f && !FloorUnder(feet + planDir * (along * air + room + s.edgeGap))) room -= 0.1f;
            Launch(run + planDir * (Mathf.Max(0f, room) / air) + Vector3.up * up);
        }

        void BeNumbed(float seconds)
        {
            if (NetworkPuppet || seconds <= 0f) return;
            numbLeft = Mathf.Max(numbLeft, seconds);
            LetGo(0.3f);   // the wall, a ledge, a rope, the hook, whatever is in its hands
            if (PawnRushSkills != null && (lassoPhase != LassoPhase.None || chainPhase != ChainPhase.None))
            {
                // A bishop's rope or a rook's chain is held in the hands too: it drops, and its cooldown runs.
                Log($"{DisplayName}: {SkillName(skillPiece)} 놓침 (손 저림)");
                float cooldown = PieceCooldown(PawnRushSkills);
                ClearSkills();
                StartCooldown(cooldown);
            }
            LastSkillHit = $"뒷발차기: 손 저림 {seconds:0.#}초";
        }

        // ---------------------------------------------------------------- 반대색 비숍 (the bishop's)

        void UpdateGhost(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    // (An answer to what is coming: it may be started in the air too.)
                    if (!press || !CanStartSkill(true)) break;
                    SkillUses++;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    planPoint = FeetPoint;
                    SkillDetail = "색 바꾸는 중";
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    planPoint = FeetPoint;
                    if (planTime >= s.ghostWindup)
                    {
                        BeginGhost(s);
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        SkillDetail = "반대색";
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.ghostRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.ghostCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void BeginGhost(PawnRushSkillParams s)
        {
            ghostLeft = s.ghostSeconds;
            ghostOff = true;
            foreach (var other in All)
                if (other != null && other != this && IsEnemyOf(other)) IgnorePiece(other, true);
            // Nothing of an enemy's holds a ghost: a hand on it lets go, a pin or an elastic on it falls off (a bishop's
            // rope lets go of a piece no skill can target by itself).
            ReleaseHolders(0.5f);
            heldTimer = struggleTimer = escapeProgress = 0f;
            holder = null;
            heldCollider = null;
            EndPin();
            // And it holds no enemy: a grip it has on one opens (PawnHand takes no new one: SkillCannotGrab).
            foreach (var hand in new[] { handL, handR })
            {
                if (hand == null || hand.HeldCollider == null) continue;
                if (ColliderOwner.TryGetValue(hand.HeldCollider, out var held) && held != this && IsEnemyOf(held)) hand.Release(0.3f);
            }
            Log($"반대색 비숍: {s.ghostSeconds:0.#}초 동안 적의 몸과 스킬이 통과");
        }

        /// <summary>The ghost ends: it collides with the enemy pieces again, and an enemy it is still standing in is thrown
        /// apart from it.</summary>
        void EndGhost(PawnRushSkillParams s)
        {
            ghostLeft = 0f;
            if (!ghostOff) return;
            ghostOff = false;
            Vector3 me = bodies[0].position;
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other)) continue;
                if (other.ghostOff) continue;   // a ghost itself: the two stay through each other until that one ends too
                IgnorePiece(other, false);
                if (s == null || other.NetworkPuppet) continue;
                Vector3 d = other.bodies[0].position - me;
                if (Flat(d).magnitude > s.ghostPopRadius || Mathf.Abs(d.y) > 1f) continue;
                Vector3 away = Flat(d).sqrMagnitude > 1e-4f ? Flat(d).normalized : FlatDir(facing);
                other.TakeHit(away * s.ghostPop + Vector3.up, 0f, 0f, false);
                TakeHit(-away * s.ghostPop + Vector3.up, 0f, 0f, false);
                other.LastSkillHit = "반대색 비숍: 겹친 채 풀려 튕김";
                Log($"반대색 비숍: {other.DisplayName}와 겹친 채 풀려 서로 튕김");
            }
        }

        // ---------------------------------------------------------------- 뒤에서 밀기 (the rook's)

        /// <summary>A rook about to send this piece (any PC), and the seconds left before it does.</summary>
        public RagdollPawn PushedBy()
        {
            foreach (var p in All)
                if (p != null && p != this && p.PlanSkill == PawnRushSkillIds.BackPush && p.planPhase == PlanPhase.Windup && p.planTarget == this) return p;
            return null;
        }

        public float PushLeft =>
            PlanSkill == PawnRushSkillIds.BackPush && planPhase == PlanPhase.Windup && PawnRushSkills != null
                ? Mathf.Max(0f, PawnRushSkills.pushWindup - planTime) : 0f;

        /// <summary>The piece right in front of its hands, which the push would send: any standing piece, a teammate or an
        /// enemy that has stepped in between - the nearest.</summary>
        RagdollPawn PushPick(PawnRushSkillParams s, Vector3 dir)
        {
            Vector3 from = bodies[0].position;
            RagdollPawn best = null;
            float bestD = s.pushReach;
            foreach (var other in All)
            {
                if (other == null || other == this || other.NetworkPuppet || other.State != PawnState.Active || other.SkillUntargetable
                    || other.Floating || other.Climbing || other.chainCarry || other.kingCarry || other.castleCarry) continue;
                Vector3 to = other.bodies[0].position - from;
                float across = Flat(to).magnitude;
                if (across < 0.2f || across > bestD || Mathf.Abs(to.y) > 1f) continue;
                if (Vector3.Angle(dir, Flat(to)) > s.pushCone) continue;
                best = other;
                bestD = across;
            }
            return best;
        }

        /// <summary>Where the piece comes down: pushDistance along the line while the floor goes on unbroken. It stops
        /// short of a gap (nothing is jumped over: a missing bridge stays missing) and, unless pushIntoWalls, of a wall.</summary>
        Vector3 PushLanding(PawnRushSkillParams s, RagdollPawn t, Vector3 dir)
        {
            Vector3 feet = t.FeetPoint, at = feet;
            const float step = 0.4f;
            int n = Mathf.CeilToInt(s.pushDistance / step);
            for (int i = 1; i <= n; i++)
            {
                Vector3 here = feet + dir * Mathf.Min(i * step, s.pushDistance);
                Vector3 knee = new Vector3(at.x, at.y + 0.45f, at.z);
                if (!s.pushIntoWalls && SolidRay(knee, dir, Flat(here - at).magnitude + 0.35f, out var wall) && Mathf.Abs(wall.normal.y) < 0.5f) break;
                if (!SolidRay(new Vector3(here.x, at.y + 1f, here.z), Vector3.down, 2.2f, out var floor) || floor.normal.y < 0.5f) break;
                at = floor.point;
            }
            return at;
        }

        void UpdateBackPush(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    var t = PushPick(s, AimFlat());
                    if (t == null)
                    {
                        // Nobody right in front: the key is ignored (a short "no" sound), no cooldown.
                        if (Time.time - pawnFailAt > 0.25f)
                        {
                            pawnFailAt = Time.time;
                            Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Rook);
                            Log("뒤에서 밀기: 바로 앞에 받칠 기물이 없음 (무시)");
                        }
                        break;
                    }
                    SkillUses++;
                    planTarget = t;
                    planDir = FlatDir(t.bodies[0].position - bodies[0].position);
                    planPoint = PushLanding(s, t, planDir);
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    facing = planDir;
                    SkillDetail = $"받침 → {t.DisplayName}";
                    Log($"뒤에서 밀기 → {t.DisplayName}{(IsAllyOf(t) ? "(팀원)" : "(적)")}: {s.pushWindup:0.#}초 뒤 발사 (점프하면 빠짐)");
                    break;
                }
                case PlanPhase.Windup:
                {
                    planTime += dt;
                    if (Staggered || bindLeft > 0f)
                    {
                        Log("뒤에서 밀기: 맞아서 끊김");
                        EndPlanSkill();
                        StartCooldown(s.pushMissCooldown);
                        break;
                    }
                    // Whoever stands nearest in front of its hands now is the one it sends: a piece that steps in between
                    // takes the ride.
                    var now = PushPick(s, planDir);
                    if (now != null) planTarget = now;
                    var t = planTarget;
                    if (t != null)
                    {
                        Vector3 to = Flat(t.bodies[0].position - bodies[0].position);
                        if (to.sqrMagnitude > 0.04f) planDir = to.normalized;
                        planPoint = PushLanding(s, t, planDir);
                    }
                    facing = planDir;
                    if (planTime < s.pushWindup) break;
                    bool sent = Send(s, t);
                    planNote = (byte)(sent ? 1 : 0);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    SkillDetail = sent ? "발사!" : "빈손";
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.pushRecovery)
                    {
                        bool sent = (planNote & 1) != 0;
                        EndPlanSkill();
                        StartCooldown(sent ? s.pushCooldown : s.pushMissCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The push: the piece flies to the plan's point on a low arc and lands standing. One that has jumped, or
        /// got out of reach, is not sent.</summary>
        bool Send(PawnRushSkillParams s, RagdollPawn t)
        {
            if (t == null || t.State != PawnState.Active || t.SkillUntargetable || t.Floating)
            {
                Log("뒤에서 밀기: 받칠 기물이 없어짐");
                return false;
            }
            Vector3 to = t.bodies[0].position - bodies[0].position;
            if (Flat(to).magnitude > s.pushReach * 1.25f || Mathf.Abs(to.y) > 1.2f || !t.OnFloor)
            {
                Log($"뒤에서 밀기: {t.DisplayName}가 빠짐 ({(!t.OnFloor ? "점프" : "멀어짐")})");
                return false;
            }
            Vector3 from = t.bodies[0].position, land = planPoint + Vector3.up * t.standHeight;
            float far = Flat(land - from).magnitude;
            // (A shorter way - a gap or a wall ahead - at the same speed.)
            float time = Mathf.Max(0.25f, s.pushFlight * Mathf.Clamp01(far / Mathf.Max(0.5f, s.pushDistance)));
            t.BeShot(LeapVelocity(from, land, time, s.pushArc), time, planDir);
            Fx(SkillFxKind.PawnHit, t, SkillContact(t), planDir, 0);
            Log($"뒤에서 밀기 → {t.DisplayName}{(IsAllyOf(t) ? "(팀원)" : "(적)")}: {far:0.0} m 발사 ({time:0.00}초)");
            return true;
        }

        void BeShot(Vector3 velocity, float time, Vector3 dir)
        {
            if (NetworkPuppet) return;
            ClearSkills();   // its own skill stops; the cooldowns it has keep running
            Launch(velocity);
            // Pieces it meets on the way are the push's to move aside, not an impact's to floor; and it lands standing.
            skillGrace = Mathf.Max(skillGrace, time + 0.3f);
            KeepSkillLandingSafe(time + SkillLandingGrace);
            shotLeft = time + 0.15f;
            shotAge = 0f;
            shotDir = dir;
            shotBumped.Clear();
            LastSkillHit = "뒤에서 밀기: 발사됨";
        }

        /// <summary>Flying where a rook sent it: enemies of the flying piece that are in its way are nudged aside (no
        /// knockdown). It ends on landing.</summary>
        void UpdateShot(PawnRushSkillParams s, float dt)
        {
            shotLeft -= dt;
            shotAge += dt;
            if (State != PawnState.Active || Floating || (shotAge > 0.2f && OnFloor))
            {
                shotLeft = 0f;
                return;
            }
            Vector3 at = bodies[(int)BodyId.Chest].position + shotDir * 0.3f;
            Vector3 right = Vector3.Cross(Vector3.up, shotDir);
            int n = Physics.OverlapSphereNonAlloc(at, 0.5f, skillOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!ColliderOwner.TryGetValue(skillOverlap[i], out var other) || other == null || other == this || shotBumped.Contains(other)) continue;
                if (other.State == PawnState.Ragdoll || other.NetworkPuppet || !IsEnemyOf(other) || other.SkillUntargetable) continue;
                shotBumped.Add(other);
                float side = Vector3.Dot(Flat(other.bodies[0].position - bodies[0].position), right);
                other.TakeHit(right * (Mathf.Abs(side) < 0.02f ? 1f : Mathf.Sign(side)) * s.pushBump, 0f, 0f, false);
                other.LastSkillHit = "뒤에서 밀기: 날아온 기물에 옆으로 튕김";
                Log($"{DisplayName}(발사됨) → {other.DisplayName}: 옆으로 튕김");
            }
        }

        // ---------------------------------------------------------------- 두 마리 토끼 (the queen's)

        /// <summary>The two enemies she would tie: the two nearest to her aim, in reach and in sight, free of a pin.</summary>
        void HarePick(PawnRushSkillParams s, out RagdollPawn first, out RagdollPawn second)
        {
            first = second = null;
            float a1 = s.hareCone, a2 = s.hareCone;
            Vector3 flat = AimFlat(), eye = bodies[(int)BodyId.Head].position;
            foreach (var other in All)
            {
                if (other == null || other == this || other.NetworkPuppet || !IsEnemyOf(other) || other.State != PawnState.Active
                    || other.SkillUntargetable || other.pinLeft > 0f || other.chainCarry || other.kingCarry || other.castleCarry) continue;
                Vector3 to = other.bodies[0].position - bodies[0].position;
                float across = Flat(to).magnitude;
                if (across < 0.8f || across > s.hareRange || Mathf.Abs(to.y) > 2f) continue;
                float angle = Vector3.Angle(flat, Flat(to));
                if (angle > a2 || !ClearSight(eye, other.bodies[(int)BodyId.Head].position)) continue;
                if (angle <= a1)
                {
                    second = first;
                    a2 = a1;
                    first = other;
                    a1 = angle;
                }
                else
                {
                    second = other;
                    a2 = angle;
                }
            }
        }

        void UpdateTwoHares(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    HarePick(s, out var a, out var b);
                    if (a == null || b == null)
                    {
                        // Not two enemies in her aim: the key is ignored (a short "no" sound), no cooldown.
                        if (Time.time - pawnFailAt > 0.25f)
                        {
                            pawnFailAt = Time.time;
                            Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Queen);
                            Log("두 마리 토끼: 조준 안에 적이 둘 있어야 함 (무시)");
                        }
                        break;
                    }
                    SkillUses++;
                    planTarget = a;
                    hareOther = b;
                    planPoint = b.LassoBelly;
                    planDir = AimFlat();
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    facing = planDir;
                    SkillDetail = $"두 손 뻗기 → {a.DisplayName}, {b.DisplayName}";
                    break;
                }
                case PlanPhase.Windup:
                {
                    // The two she reached for at the start: both lines show until the elastic goes on.
                    planTime += dt;
                    if (Staggered || bindLeft > 0f)
                    {
                        Log("두 마리 토끼: 맞아서 끊김");
                        EndPlanSkill();
                        StartCooldown(s.hareMissCooldown);
                        break;
                    }
                    if (hareOther != null) planPoint = hareOther.LassoBelly;
                    if (planTime < s.hareWindup) break;
                    bool tied = TieHares(s, planTarget, hareOther);
                    planNote = (byte)(tied ? 1 : 0);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    SkillDetail = tied ? "묶음!" : "놓침";
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.hareRecovery)
                    {
                        bool tied = (planNote & 1) != 0;
                        EndPlanSkill();
                        StartCooldown(tied ? s.hareCooldown : s.hareMissCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The elastic goes on if both are still there: standing, in reach and in sight, free of a pin.</summary>
        bool TieHares(PawnRushSkillParams s, RagdollPawn a, RagdollPawn b)
        {
            Vector3 eye = bodies[(int)BodyId.Head].position;
            foreach (var t in new[] { a, b })
            {
                if (t != null && t.State == PawnState.Active && !t.SkillUntargetable && t.pinLeft <= 0f && !t.Floating
                    && Flat(t.bodies[0].position - bodies[0].position).magnitude <= s.hareRange * 1.15f
                    && ClearSight(eye, t.bodies[(int)BodyId.Head].position)) continue;
                Log($"두 마리 토끼: {(t != null ? t.DisplayName : "?")}를 놓침 (멀어지거나 가려지거나 넘어짐)");
                return false;
            }
            a.BeTied(b, s);
            b.BeTied(a, s);
            Fx(SkillFxKind.PawnHit, a, a.LassoBelly, FlatDir(b.LassoBelly - a.LassoBelly), 0);
            Log($"두 마리 토끼: {a.DisplayName}와 {b.DisplayName}를 {s.hareRest:0.#} m 고무줄로 묶음 ({s.hareSeconds:0.#}초)");
            return true;
        }

        void BeTied(RagdollPawn mate, PawnRushSkillParams s)
        {
            pinMate = mate;
            pinPost = Vector3.zero;
            pinToPost = false;
            pinLeft = s.hareSeconds;
            pinRest = s.hareRest;
            pinIsTie = true;
            pinStiff = s.hareStiffness;
            pinPullMax = s.harePull;
            tieSnapWait = 0.5f;
            LastSkillHit = $"두 마리 토끼: {(mate != null ? mate.DisplayName : "?")}와 묶임";
        }

        /// <summary>A tied piece's step (the host's; UpdatePin hands it over). The elastic does not break when stretched:
        /// past hareSnap, with both on their feet, it snaps the two back at each other (once for the pair). It ends
        /// when its time runs out, the other end is gone, or the piece is in the water or out of every skill's reach.</summary>
        void UpdateTie(PawnRushSkillParams s, float dt)
        {
            pinLeft -= dt;
            tieSnapWait -= dt;
            var mate = pinMate;
            if (pinLeft <= 0f || mate == null || mate.pinMate != this || mate.pinLeft <= 0f || Floating || SkillUntargetable)
            {
                EndPin();
                return;
            }
            float stretch = Vector3.Distance(PinAnchor, LassoBelly) - pinRest;
            if (stretch <= s.hareSnap || tieSnapWait > 0f || mate.tieSnapWait > 0f || GetInstanceID() > mate.GetInstanceID()) return;
            if (State != PawnState.Active || mate.State != PawnState.Active || !OnFloor || !mate.OnFloor) return;
            Vector3 toMate = FlatDir(mate.LassoBelly - LassoBelly);
            TakeHit(toMate * s.hareSnapPush + Vector3.up, 0f, 0f, false);
            mate.TakeHit(-toMate * s.hareSnapPush + Vector3.up, 0f, 0f, false);
            Stagger(s.hareSnapStagger);
            mate.Stagger(s.hareSnapStagger);
            tieSnapWait = mate.tieSnapWait = s.hareSnapEvery;
            LastSkillHit = mate.LastSkillHit = "두 마리 토끼: 줄이 튕겨 서로에게 날아감";
            Fx(SkillFxKind.PawnHit, mate, Vector3.Lerp(LassoBelly, mate.LassoBelly, 0.5f), toMate, 0);
            Log($"두 마리 토끼: 줄이 {stretch:0.0} m 늘어나 {DisplayName}와 {mate.DisplayName}가 서로에게 튕김");
        }

        // ---------------------------------------------------------------- over the network (RagdollPawn.SkillNet.cs)

        // More bits of a piece's skill state: Flags2 for what these skills leave on it, and one of the first byte's
        // free bits (RagdollPawn.SkillNet.cs: 2, 16 and 64) for "the pin on this piece is the queen's elastic".
        const byte NetTouched = 16, NetNumb = 32, NetGhost = 64;
        const byte NetTied = 64;

        bool BookNetBusy => touchLeft > 0f || numbLeft > 0f || ghostLeft > 0f;

        /// <summary>Host: what these skills have left on this piece.</summary>
        void CaptureBookNet(ref SkillWireState s)
        {
            if (touchLeft > 0f) s.Flags2 |= NetTouched;
            if (numbLeft > 0f) s.Flags2 |= NetNumb;
            if (ghostLeft > 0f) s.Flags2 |= NetGhost;
            if (pinLeft > 0f && pinIsTie) s.Flags |= NetTied;
        }

        /// <summary>Client: the same on a puppet. (The time left of each is this screen's own count from when it first saw
        /// it, as a pin's is.)</summary>
        void ApplyBookNet(in SkillWireState s)
        {
            var k = PawnRushSkills;
            SeeState(ref touchLeft, (s.Flags2 & NetTouched) != 0, k != null ? k.touchSeconds : 2f);
            SeeState(ref numbLeft, (s.Flags2 & NetNumb) != 0, k != null ? k.kickNumb : 3f);
            SeeState(ref ghostLeft, (s.Flags2 & NetGhost) != 0, k != null ? k.ghostSeconds : 3f);
            pinIsTie = (s.Flags & NetTied) != 0;
        }

        static void SeeState(ref float left, bool on, float seconds)
        {
            if (!on) left = 0f;
            else if (left <= 0f) left = seconds;
        }

        void ClearBookNet()
        {
            touchLeft = numbLeft = ghostLeft = 0f;
            pinIsTie = false;
        }

        void AdvanceBookView(float dt)
        {
            if (touchLeft > 0f) touchLeft = Mathf.Max(0.05f, touchLeft - dt);
            if (numbLeft > 0f) numbLeft = Mathf.Max(0.05f, numbLeft - dt);
            if (ghostLeft > 0f) ghostLeft = Mathf.Max(0.05f, ghostLeft - dt);
        }
    }
}
