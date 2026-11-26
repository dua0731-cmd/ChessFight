using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The kits by piece, the basic row (JY-skill-lab, 2026-11-20; Docs/Skills/PAWN_RUSH_SKILL_KITS.md §6). The user: a
    /// team has one of each piece, and "약한 스킬 강한 스킬 모티브는 오프닝 > 엔드게임으로 갈 때 나오는 각 기물의 플레이":
    /// a piece's basic skill is what it does in a chess opening. Three of the six basics were there already (the pawn's
    /// two steps, the rook's castling, the queen's check); these are the other three, on the slots' machinery
    /// (RagdollPawn.SkillPlans.cs: one skill at a time, a phase, a time, a point, a target, the same on every PC).
    ///
    ///   ㄱ자 도약     the knight is the first piece out, over its own pawns: a leap 1.6 m high and 4.5 m long over what
    ///   (bent leap)   rolls, swings or stands in the way; one more press in the air kicks off again, turned up to a right
    ///                 angle (the L). Coming down on an enemy's head staggers it and bounces the knight up once more. This
    ///                 is the first form of 승규 님's leap (10-07; R153 made it the leap at an enemy's head it is now), made
    ///                 again for the slots with that form's numbers; it does not home on a head by itself.
    ///   피앙케토      the bishop takes a seat and aims down the long diagonal: its key seats it (it goes nowhere), the line
    ///   (fianchetto)  grows from 8 m to 25 m over a second, the left click shoots. The first piece on the line takes it - an
    ///                 enemy walks on heavy feet for two seconds (no sprint), a teammate or a wall just stops it.
    ///   왕의 한 걸음  the king steps one square any way. Held by someone, the step shakes them off first; lying knocked
    ///   (king's step) down, the key gets him up at once instead (no step). 승규 님's first king skill as a plan (DESIGN §8),
    ///                 never code until now.
    ///
    /// And one thing more for the rook's basic: a castling between the king and the rook (either asking) leaves both
    /// unfloorable for a moment after they land (castleRoyalGuard).
    ///
    /// Prototypes: the rules and the physics only, lines and rings by PawnRushPlanMarks. Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        // ---------------------------------------------------------------- what they share

        // What a fianchettoed bishop's line leaves on a piece: heavy feet.
        float slowLeft;
        // The bent leap's own: its time in the air, its one turn used, no second head right after one; the pool's N-기1: when
        // the jump key was last pressed coming down and when it landed (a roll), and its small number's bits.
        float leapAir, leapBounceWait, leapJumpAt = -10f, leapLandAt;
        bool leapTurned;
        const byte LeapRolled = 1, LeapBlunderBit = 2;
        // How long this piece has lain knocked down (the king's key gets him up only after a moment).
        float downFor;
        // The castling under way is the king's and the rook's.
        bool castleRoyal;

        /// <summary>Walking on heavy feet (a bishop's fianchetto took it): slower, and no sprint.</summary>
        public bool Slowed => slowLeft > 0f;
        public float SlowLeft => Mathf.Max(0f, slowLeft);
        /// <summary>The king plays the skills of his row (it holds one); otherwise his call, as it always was.</summary>
        public bool KingPlaysSlots => piece == PieceKind.King && Loadout != null && Loadout.KingSlots;
        /// <summary>The bent leap is in the air and its one turn is still there.</summary>
        public bool LeapCanTurn => planPhase == PlanPhase.Active && activeSkill == PawnRushSkillIds.BentLeap && !leapTurned;
        /// <summary>The bent leap is in the air with no floor where it will come down (the pool's "블런더"; any PC).</summary>
        public bool LeapBlunder => PlanSkill == PawnRushSkillIds.BentLeap && planPhase == PlanPhase.Active && (planNote & LeapBlunderBit) != 0;

        /// <summary>The run's top speed under these skills (RagdollPawn.TopSpeed): heavy feet.</summary>
        float SkillMoveScale => (slowLeft > 0f && PawnRushSkills != null ? Mathf.Clamp(slowScaleNow > 0f ? slowScaleNow : PawnRushSkills.fianSlow, 0.2f, 1f) : 1f)
                                * MarchScale    // the pool's king's march (RagdollPawn.PoolUltimates.cs); a flank shot's own slow (KitTwo)
                                * KingWalkScale    // the king's walk, a driven ultimate (RagdollPawn.KingWalk.cs)
                                * SupportScale     // a support's reward: both fast for a while (RagdollPawn.Support.cs)
                                * BolasScale       // the bolas' pull: the pawn lunges on (RagdollPawn.SetThree.cs)
                                * CcMoveScale      // the set 4판's hindrances: heavy, tiny (RagdollPawn.Cc.cs)
                                * MowerScale;      // the set 4판's 잔디깎이: 30% faster (RagdollPawn.SetFourB.cs)

        /// <summary>How far the seated bishop's line reaches now: fianNear at the key, fianFar once it has aimed for
        /// fianCharge seconds (any PC: the time comes with the host's skill state).</summary>
        public float FianReach
        {
            get
            {
                var s = PawnRushSkills;
                if (s == null) return 0f;
                return Mathf.Lerp(s.fianNear, s.fianFar, Mathf.Clamp01(planTime / Mathf.Max(0.05f, s.fianCharge)));
            }
        }

        /// <summary>Before the run (PlanKeys): heavy feet do not sprint; then what each of these skills lets the keys do in
        /// its phase. (The clicks of an aim are read after this, so an aiming phase leaves them alone.)</summary>
        void KitKeys()
        {
            if (slowLeft > 0f) input.sprint = false;
            // The pool's K-기1: a jump or a dive just after a king's step goes farther along it (UpdateKitStates).
            if ((input.jump || input.shove) && PawnRushSkills != null && Time.time - stepEndAt <= PawnRushSkills.stepChainWindow
                && (planPhase == PlanPhase.None || (activeSkill == PawnRushSkillIds.KingStep && planPhase == PlanPhase.Recovery)))
                stepBoost = true;
            if (planPhase == PlanPhase.None) return;
            switch (activeSkill)
            {
                case PawnRushSkillIds.Fianchetto:
                    // Seated from the key to the end of the shot: it turns with its aim and goes nowhere.
                    input.move = Vector3.zero;
                    input.jump = false;
                    input.sprint = false;
                    if (planPhase == PlanPhase.Aim) break;
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
                case PawnRushSkillIds.KingStep:
                    input.move = Vector3.zero;
                    input.sprint = false;
                    if (planPhase == PlanPhase.Recovery) break;   // (a jump or a dive may follow the step: K-기1)
                    input.jump = false;
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
                case PawnRushSkillIds.BentLeap:
                    // The pool's N-기1: a jump pressed coming down (or just after landing) rolls the knight on
                    // (UpdateBentLeap); taken here so it does not jump as well.
                    if (input.jump && ((planPhase == PlanPhase.Active && bodies[0].linearVelocity.y < 0f) || planPhase == PlanPhase.Recovery))
                    {
                        leapJumpAt = Time.time;
                        input.jump = false;
                    }
                    // (Rearing up it still runs; in the air the keys only say which way the turn goes.)
                    if (planPhase != PlanPhase.Recovery) break;
                    input.move = Vector3.zero;
                    input.jump = false;
                    input.sprint = false;
                    break;
            }
        }

        /// <summary>Every step (UpdateSkills): what these skills leave on a piece runs out.</summary>
        void UpdateKitStates(PawnRushSkillParams s, float dt)
        {
            UpdatePoolStates(s, dt);   // the skill pool's: the moment to check again (RagdollPawn.PoolSkills.cs)
            if (stepBoost)
            {
                // The pool's K-기1: the jump or dive just taken goes on along the step (and the step's stop is over).
                stepBoost = false;
                AddVelocity(FlatDir(stepEndDir) * s.stepChainBoost);
                if (PlanSkill == PawnRushSkillIds.KingStep && planPhase == PlanPhase.Recovery)
                {
                    float cooldown = StepCooldownNow(s);
                    EndPlanSkill();
                    StartCooldown(cooldown);
                }
                Log($"{DisplayName}: 왕의 한 걸음에 이어 점프 · 다이빙 → 더 멀리");
            }
            downFor = State == PawnState.Ragdoll && !Diving ? downFor + dt : 0f;
            UpdateKitTwoStates(s, dt);   // the kits' second version: light, heavy (RagdollPawn.KitTwo.cs)
            if (slowLeft <= 0f) return;
            // (In the water, or just back from a respawn: nothing of it is left.)
            slowLeft = Floating || skillGuardLeft > 0f ? 0f : slowLeft - dt;
            if (slowLeft <= 0f) slowScaleNow = 0f;
        }

        /// <summary>What these skills leave running on the piece that casts them, when its skill ends.</summary>
        void ClearKitSkills()
        {
            leapAir = leapBounceWait = 0f;
            leapTurned = false;
            ClearKitTwoSkill();   // the kits' second version: a wall half raised stands what it is (RagdollPawn.KitTwo.cs)
        }

        // ---------------------------------------------------------------- ㄱ자 도약 (the knight's bent leap)

        void UpdateBentLeap(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill(true)) break;
                    SkillUses++;
                    planNote = 0;
                    planDir = AimFlat();
                    facing = planDir;
                    if (!OnFloor)
                    {
                        // In the air too: it leaps on from where it is, at once (no rearing up mid-air).
                        BeginBentLeap(s);
                        break;
                    }
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    planPoint = FeetPoint + planDir * s.leapDistance;
                    SkillDetail = "준비 (뒷발로 섬)";
                    break;
                case PlanPhase.Windup:
                    // Not rooted: a running knight keeps its speed into the leap, and the leap goes where it looks then.
                    planTime += dt;
                    planDir = AimFlat();
                    planPoint = FeetPoint + planDir * s.leapDistance;
                    if (planTime >= s.leapWindup) BeginBentLeap(s);
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    leapAir += dt;
                    leapBounceWait -= dt;
                    // One more press per leap: the kick on, turned toward the move keys (or straight on).
                    if (press && !leapTurned && leapAir >= s.leapTurnAfter) BentTurn(s);
                    BentStomp(s);
                    if (Climbing || rope != null || leapAir > s.leapLongest)
                    {
                        // Caught a ledge on the way, or still not down long after (it leapt off an edge): it ends there.
                        EndPlanSkill();
                        StartCooldown(s.leapCooldown);
                    }
                    else if (leapAir > 0.2f && Grounded)
                    {
                        planPoint = FeetPoint;
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        planNote = (byte)(planNote & ~LeapBlunderBit);
                        leapLandAt = Time.time;
                        SkillDetail = "착지 후딜";
                        Fx(SkillFxKind.KnightLand, null, FeetPoint, planDir, 0, 0.5f);
                        if (VariantNow == PawnRushSkillIds.LeapFork) LeapForkLand(s);   // the set 1안-a's leap fork (SetSkills)
                        // The pool's N-기1: the jump pressed just before touching down rolls it on.
                        if (Time.time - leapJumpAt <= s.leapRollEarly) BeginLeapRoll(s);
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if ((planNote & LeapRolled) != 0)
                    {
                        // Rolling on, with no stop after it: the roll runs out (or meets a wall) and the leap is over.
                        if (WallAhead(dashBase * dt + 0.22f, out _))
                        {
                            dashLeft = 0f;
                            HaltDash();
                        }
                        else
                        {
                            DriveDash(dt);
                            dashLeft -= dt;
                        }
                        if (dashLeft <= 0f)
                        {
                            EndDash();
                            EndPlanSkill();
                            StartCooldown(s.leapCooldown);
                        }
                        break;
                    }
                    // ...or pressed just after.
                    if (leapJumpAt >= leapLandAt && planTime <= s.leapRollLate && BeginLeapRoll(s)) break;
                    if (planTime >= s.leapRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.leapCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void BeginBentLeap(PawnRushSkillParams s)
        {
            bool fromFloor = OnFloor;   // (the launch below takes it off the floor)
            float g = Mathf.Max(0.01f, -Physics.gravity.y);
            // The textbook arc for the height and the distance, then what the ragdoll loses in the air put back (the first
            // form's numbers, measured in the skill test scene then); never slower along the line than it was running.
            float up = Mathf.Sqrt(2f * g * Mathf.Max(0.05f, s.leapHeight));
            float on = s.leapDistance / (2f * up / g) * s.leapCarry;
            up *= s.leapLift;
            float along = Vector3.Dot(Flat(bodies[0].linearVelocity) - carryVel, planDir);
            on = Mathf.Max(on, along);
            Launch(planDir * on + Vector3.up * up);
            facing = planDir;
            leapAir = 0f;
            leapBounceWait = 0f;
            leapTurned = false;
            skillHitSet.Clear();
            planPhase = PlanPhase.Active;
            planTime = 0f;
            planPoint = PredictLanding(bodies[0].position, bodies[0].linearVelocity, g);
            MarkLeapBlunder(s);
            SkillDetail = "도약 (공중에서 한 번 더 = 꺾어 차고 나감)";
            Fx(SkillFxKind.KnightLeap, null, bodies[0].position - Vector3.up * standHeight, planDir, fromFloor ? 0 : 1);
            Log($"{DisplayName}: ㄱ자 도약 ({s.leapDistance:0.#} m, 높이 {s.leapHeight:0.#} m)");
        }

        /// <summary>The second leg of the L: it kicks off the air along the move keys (no key: the aim), turned at most
        /// leapTurnMax from the way it flies, at leapTurnSpeed or its own speed if that is more, with a fresh rise.</summary>
        void BentTurn(PawnRushSkillParams s)
        {
            Vector3 want = Flat(skillMoveRaw);
            if (want.sqrMagnitude < 0.04f) want = AimFlat();
            want.Normalize();
            Vector3 v = Flat(bodies[0].linearVelocity);
            float speed = v.magnitude;
            Vector3 dir = speed > 0.1f ? v / speed : planDir;
            Vector3 turned = Vector3.RotateTowards(dir, want, s.leapTurnMax * Mathf.Deg2Rad, 0f);
            float angle = Vector3.Angle(dir, turned);
            float rise = Mathf.Max(bodies[0].linearVelocity.y, s.leapTurnLift);
            Launch(turned * Mathf.Max(speed, s.leapTurnSpeed) + Vector3.up * rise);
            facing = turned;
            planDir = turned;
            leapTurned = true;
            planPoint = PredictLanding(bodies[0].position, bodies[0].linearVelocity, Mathf.Max(0.01f, -Physics.gravity.y));
            MarkLeapBlunder(s);
            Fx(SkillFxKind.KnightTurn, null, bodies[0].position, turned, Mathf.RoundToInt(angle));
            SkillDetail = angle > 5f ? "꺾어 차고 나감" : "앞으로 차고 나감";
            Log($"{DisplayName}: 공중에서 다시 차고 나감 {angle:0}°");
        }

        /// <summary>Coming down on a standing enemy's head: that piece staggers (it is not floored), and the knight bounces
        /// up off it - the same leap goes on, its turn still there if unused. Judged against the head, the two closing on
        /// each other, as the leap at a head is.</summary>
        void BentStomp(PawnRushSkillParams s)
        {
            if (leapBounceWait > 0f) return;
            Vector3 feet = bodies[0].position - Vector3.up * standHeight;
            float vy = bodies[0].linearVelocity.y;
            foreach (var other in All)
            {
                if (other == null || other == this || other.NetworkPuppet || !IsEnemyOf(other) || other.State != PawnState.Active
                    || other.SkillUntargetable || skillHitSet.Contains(other)) continue;
                float closing = vy - other.bodies[0].linearVelocity.y;
                if (vy > 1f || closing > -1f) continue;
                var headBody = other.bodies[(int)BodyId.Head];
                var headCollider = headBody.GetComponent<Collider>();
                Vector3 head = headBody.position;
                float top = headCollider != null ? headCollider.bounds.max.y : head.y + 0.12f;
                if (Flat(feet - head).magnitude > s.leapStompReach + 0.1f || feet.y < top - 0.25f || feet.y > top + 0.35f) continue;
                skillHitSet.Add(other);
                bool swayed = other.Stagger(s.leapStompStagger);
                other.LastSkillHit = swayed ? $"ㄱ자 도약: 머리를 밟힘 → 휘청 {s.leapStompStagger:0.#}초" : "ㄱ자 도약: 머리를 밟힘 (휘청 안 걸림)";
                // The kits' second version (가벼운 발굽): light for a while - pushed and thrown twice as far (KitTwo).
                MakeLight(other, s);
                Fx(SkillFxKind.KnightStomp, other, new Vector3(head.x, top, head.z), Flat(facing));
                // The hand feel's hit stop, the pool's N-기1 length (a stagger otherwise holds 0.06 s).
                FeelHit(other, new Vector3(head.x, top, head.z), Flat(facing), FeelWeak, baseStop: s.leapStompStop);
                // Up off the head: what it had along the ground stays, the fall becomes a rise.
                Launch(Flat(bodies[0].linearVelocity) + Vector3.up * s.leapStompBounce);
                leapBounceWait = 0.25f;
                planPoint = PredictLanding(bodies[0].position, bodies[0].linearVelocity, Mathf.Max(0.01f, -Physics.gravity.y));
                MarkLeapBlunder(s);
                SkillDetail = "머리 밟고 다시 튐";
                Log($"ㄱ자 도약 → {other.DisplayName}: 머리 밟기 → {(swayed ? "휘청" : "휘청 안 걸림")}, 다시 튀어 오름");
                return;
            }
        }

        // ---------------------------------------------------------------- 피앙케토 (the bishop's fianchetto)

        /// <summary>The way the line would go: flat along the aim (the camera looks down at the piece, so its own tilt is not
        /// used); an enemy within fianAssist of that way seen from above, in reach and in sight, is aimed at wherever it
        /// stands up or down a slope (fianTall), and the line then goes straight to it. The line's end goes into planPoint
        /// (short of a wall).</summary>
        Vector3 FianAim(PawnRushSkillParams s, float reach) => LineAim(reach, s.fianAssist, s.fianTall);

        /// <summary>A line from the hands, aimed (the fianchetto, the pool's long snipe): flat along the aim; an enemy within
        /// <paramref name="assist"/>° of that way seen from above, in reach and in sight, is aimed at wherever it stands up
        /// or down a slope (<paramref name="tall"/>), and the line then goes straight to it (planTarget). The line's end goes
        /// into planPoint (short of a wall).</summary>
        Vector3 LineAim(float reach, float assist, float tall)
        {
            Vector3 from = LassoHands;
            Vector3 flat = AimFlat();
            planTarget = null;
            float best = assist;
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable) continue;
                Vector3 to = other.LassoBelly - from;
                if (Flat(to).magnitude < 0.6f || to.magnitude > reach || Mathf.Abs(to.y) > tall) continue;
                float angle = Vector3.Angle(flat, Flat(to));
                if (angle > best || !ClearSight(from, other.LassoBelly)) continue;
                planTarget = other;
                best = angle;
            }
            Vector3 dir = planTarget != null ? (planTarget.LassoBelly - from).normalized : flat;
            float open = reach;
            if (SolidRay(from, dir, reach, out var wall)) open = wall.distance;
            planPoint = from + dir * open;
            return dir;
        }

        /// <summary>The first standing piece on a line from <paramref name="from"/>, within <paramref name="radius"/> of it and
        /// short of <paramref name="along"/> (which comes back as how far along it is): an enemy, or a teammate when
        /// <paramref name="alliesBlock"/>. Pieces being carried by a skill are not on it.</summary>
        RagdollPawn FirstOnLine(Vector3 from, Vector3 dir, ref float along, float radius, bool alliesBlock)
        {
            RagdollPawn first = null;
            foreach (var other in All)
            {
                if (other == null || other == this || other.State != PawnState.Active || other.chainCarry || other.kingCarry || other.castleCarry) continue;
                bool enemy = IsEnemyOf(other);
                if (enemy ? other.SkillUntargetable : !(alliesBlock && IsAllyOf(other))) continue;
                Vector3 to = other.LassoBelly - from;
                float at = Vector3.Dot(to, dir);
                if (at < 0.5f || at > along) continue;
                if ((to - dir * at).magnitude > radius) continue;
                first = other;
                along = at;
            }
            return first;
        }

        void UpdateFianchetto(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (press && CanStartSkill())
                    {
                        planPhase = PlanPhase.Aim;
                        planTime = 0f;
                        skillGrabLatch = skillGrabRaw;
                        planDir = FianAim(s, s.fianNear);
                        facing = FlatDir(planDir);
                        SkillDetail = "피앙케토: 자리 잡음 (오래 겨눌수록 멀리 · 좌클릭 쏘기 · 우클릭/스킬 키 취소)";
                    }
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    planDir = FianAim(s, FianReach);
                    facing = FlatDir(planDir);
                    if (cancel || press || planTime > s.fianHold)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        Log("피앙케토: 자리에서 일어남 (안 쏨)");
                        break;
                    }
                    if (confirm)
                    {
                        SkillUses++;
                        FireFianchetto(s, FianReach);
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        SkillDetail = "피앙케토";
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.fianRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.fianCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The line, at once: the first piece on it short of a wall takes it. An enemy walks on heavy feet for a
        /// while; a teammate only stops it (so does a wall) - the bishop's line is blocked by its own side too.</summary>
        void FireFianchetto(PawnRushSkillParams s, float reach)
        {
            Vector3 from = LassoHands, dir = planDir.sqrMagnitude > 0.5f ? planDir.normalized : FlatDir(facing);
            if (SolidRay(from, dir, reach, out var wall)) reach = wall.distance;
            FeelKick(-dir, s.feelKickShot);   // the hand feel: the shot slides the seated bishop back (RagdollPawn.HandFeel.cs)
            float firstAlong = reach;
            var first = FirstOnLine(from, dir, ref firstAlong, s.fianRadius, s.fianAlliesBlock);
            planTarget = first;
            planPoint = from + dir * firstAlong;
            if (first == null)
            {
                FeelMiss(planPoint, dir);
                Log($"피앙케토: 아무도 안 맞음 ({reach:0.#} m)");
                return;
            }
            if (!IsEnemyOf(first))
            {
                Fx(SkillFxKind.SkillFail, null, first.LassoBelly, dir, (int)PieceKind.Bishop);
                Log($"피앙케토: 팀원 {first.DisplayName}에게 막힘 ({firstAlong:0.#} m)");
                return;
            }
            string result = first.BeSlowed(s.fianSlowSeconds);
            Fx(SkillFxKind.PawnHit, first, first.LassoBelly, dir, 0);
            FeelHit(first, first.LassoBelly, dir, FeelWeak);   // heavy feet: a weak hit stop
            Log($"피앙케토 → {first.DisplayName}: {result} ({firstAlong:0.#} m)");
        }

        string BeSlowed(float seconds)
        {
            if (NetworkPuppet) return "-";
            if (SkillUntargetable)
            {
                LastSkillHit = "피앙케토: 스킬 대상 아님";
                return "대상 아님";
            }
            slowLeft = Mathf.Max(slowLeft, seconds);
            LastSkillHit = $"피앙케토: {seconds:0.#}초 동안 발이 무거움";
            return $"{seconds:0.#}초 동안 발이 무거움";
        }

        // ---------------------------------------------------------------- 왕의 한 걸음 (the king's step)

        // What a step was started from, kept in the skill's small number for its cooldown: held by someone, in the air.
        const byte StepShookOff = 1, StepInAir = 2;

        /// <summary>Free to step but for a hand on it, the air and the cooldown (the step sees to those itself).</summary>
        bool StepFree =>
            State == PawnState.Active && !Climbing && rope == null && !Floating && !Squashed && !Staggered && bindLeft <= 0f
            && !chainCarry && !kingCarry && !castleCarry && ghostLeft <= 0f
            && (hookPhase == HookPhase.None || hookPhase == HookPhase.Held);

        void UpdateKingStep(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || skillCooldownLeft > 0f) break;
                    if (State == PawnState.Ragdoll && !Diving && !Floating)
                    {
                        // Knocked down: the key gets him up at once instead (after a moment on the floor), and no skill
                        // floors him again for a while. No step with it.
                        if (downFor < s.stepDownAfter) break;
                        SkillUses++;
                        BeginGetUp(P);
                        // The pool's K-기1: up with a roll toward the move keys.
                        Vector3 roll = Flat(skillMoveRaw);
                        if (roll.sqrMagnitude > 0.04f) AddVelocity(roll.normalized * s.stepRollSpeed);
                        pendingGuard = Mathf.Max(pendingGuard, s.stepGuard);
                        StartCooldown(s.stepCooldown + s.stepDownExtra);
                        LastSkillHit = "왕의 한 걸음: 바로 일어남";
                        Fx(SkillFxKind.PawnHelp, this, bodies[0].position, FlatDir(facing));
                        Log($"{DisplayName}: 왕의 한 걸음 — 넘어진 채로 → 바로 일어남 (기상 보호 {s.stepGuard:0.#}초)");
                        break;
                    }
                    if (!StepFree) break;
                    bool held = BeingHeld;
                    if (held)
                    {
                        // A hand on him: shaken off first (as a launch does), then the step.
                        ReleaseHolders(s.stepShakeOff);
                        heldTimer = struggleTimer = escapeProgress = 0f;
                        holder = null;
                        heldCollider = null;
                    }
                    SkillUses++;
                    skillDir = MoveOrAim();
                    planDir = skillDir;
                    facing = skillDir;
                    planNote = (byte)((held ? StepShookOff : 0) | (OnFloor ? 0 : StepInAir));
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    planPoint = FeetPoint + skillDir * s.stepDistance;
                    stepFromWarning = InWarning();   // the kits' second version: walking out of one is "체크 탈출" (KitTwo)
                    SkillDetail = held ? "뿌리치고 한 걸음" : "왕의 한 걸음";
                    if (held) Log($"{DisplayName}: 왕의 한 걸음 — 잡은 손을 뿌리침");
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    facing = skillDir;
                    if (planTime >= s.stepWindup)
                    {
                        skillHitSet.Clear();
                        StartDash(s.stepSpeed, s.stepDistance / Mathf.Max(0.5f, s.stepSpeed));
                        if (VariantNow == PawnRushSkillIds.KingEscort) EscortStep(s);   // the set 2안-a's: two teammates come along
                        planPhase = PlanPhase.Active;
                        planTime = 0f;
                    }
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    if (WallAhead(dashBase * dt + 0.22f, out _))
                    {
                        // A wall ends the step where he stands.
                        dashLeft = 0f;
                        HaltDash();
                    }
                    else
                    {
                        DriveDash(dt);
                        // The pool's K-기1: an enemy the step meets is shoved 3 m, and the king stands where he is.
                        if (KingStepShove(s))
                        {
                            dashLeft = 0f;
                            HaltDash();
                        }
                        else dashLeft -= dt;
                    }
                    if (dashLeft <= 0f)
                    {
                        EndDash();
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        // The pool's K-기1: the foot comes down with a thud (a short hit stop), and a jump or dive may follow.
                        stepEndAt = Time.time;
                        stepEndDir = skillDir;
                        FeelHit(null, FeetPoint, skillDir, FeelWeak, 1, false, s.stepStompStop);
                        if (stepFromWarning && !InWarning())
                        {
                            PopOver(this, PopCheckEscape);
                            Log($"{DisplayName}: 왕의 한 걸음 — 체크 탈출!");
                        }
                        stepFromWarning = false;
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.stepRecovery)
                    {
                        float cooldown = StepCooldownNow(s);
                        EndPlanSkill();
                        StartCooldown(cooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        // The pool's K-기1: when and which way the last step ended; a jump or dive taken in the moment after it.
        float stepEndAt = -10f;
        Vector3 stepEndDir = Vector3.forward;
        bool stepBoost;

        /// <summary>The step's cooldown: more if it shook a hand off or was taken in the air.</summary>
        float StepCooldownNow(PawnRushSkillParams s) =>
            s.stepCooldown + ((planNote & StepShookOff) != 0 ? s.stepDownExtra : 0f) + ((planNote & StepInAir) != 0 ? s.stepAirExtra : 0f);

        /// <summary>The step this physics step meets an enemy (the first one): shoved stepShove along the step. True if so.</summary>
        bool KingStepShove(PawnRushSkillParams s)
        {
            Vector3 dir = FlatDir(skillDir);
            Vector3 at = bodies[(int)BodyId.Chest].position + dir * 0.3f;
            int n = Physics.OverlapSphereNonAlloc(at, 0.45f, skillOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!ColliderOwner.TryGetValue(skillOverlap[i], out var other) || other == null || other == this || skillHitSet.Contains(other)) continue;
                if (!IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable) continue;
                skillHitSet.Add(other);
                Vector3 contact = SkillContact(other);
                // The kits' second version (한 칸 잡기): a piece with a teammate of its at its side is defended - the king
                // cannot take it, and stops where he is (RagdollPawn.KitTwo.cs).
                if (Defended(other, s))
                {
                    other.LastSkillHit = "왕의 한 걸음: 받쳐진 말이라 안 밀림";
                    PopOver(other, PopDefended);
                    FeelHit(other, contact, dir, FeelWeak, 1, false);
                    Log($"왕의 한 걸음 → {other.DisplayName}: 받쳐진 말 (곁에 그 팀원) → 못 밀고 섬");
                    return true;
                }
                other.Kick(dir, s.stepShove, 0.3f, false);
                other.LastSkillHit = $"왕의 한 걸음: {s.stepShove:0.#} m 밀림";
                Fx(SkillFxKind.PawnHit, other, contact, dir, 0);
                FeelHit(other, contact, dir, FeelWeak);
                Log($"왕의 한 걸음 → {other.DisplayName}: 닿아서 {s.stepShove:0.#} m 밀어냄, 킹은 그 자리에 섬");
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- castling: the king and the rook

        /// <summary>A castling begins (StartCastle): between the king and the rook it is the real thing, and both stand
        /// unfloorable for castleRoyalGuard seconds once they land.</summary>
        void MarkRoyalCastle(RagdollPawn mate, bool forced)
        {
            bool royal = !forced && mate != null
                         && ((piece == PieceKind.King && mate.piece == PieceKind.Rook) || (piece == PieceKind.Rook && mate.piece == PieceKind.King));
            castleRoyal = royal;
            if (mate != null) mate.castleRoyal = royal;
            if (royal) Log($"캐슬링: 킹과 룩 — 내려앉은 뒤 {(PawnRushSkills != null ? PawnRushSkills.castleRoyalGuard : 0f):0.#}초 동안 넘어지지 않음");
        }

        /// <summary>A castling's flight ended (EndCastleCarry).</summary>
        void LandRoyalCastle(bool arrived)
        {
            bool royal = castleRoyal;
            castleRoyal = false;
            if (!royal || !arrived || PawnRushSkills == null) return;
            getUpGuardLeft = Mathf.Max(getUpGuardLeft, PawnRushSkills.castleRoyalGuard);
            LastSkillHit = "캐슬링: 킹과 룩 → 넘어지지 않음";
        }

        // ---------------------------------------------------------------- over the network (RagdollPawn.SkillNet.cs)

        // One of the first byte's free bits (RagdollPawn.SkillNet.cs: 2 and 16 were free; 64 is the queen's elastic): heavy
        // feet. The skills themselves go as every new skill does (its phase, time, point, way and target).
        // And 16, the last free one: the queen may check once more (the skill pool's Q-기1, RagdollPawn.PoolSkills.cs).
        const byte NetSlowed = 2, NetCheckAgain = 16;

        bool KitNetBusy => slowLeft > 0f || checkAgainLeft > 0f || KitTwoNetBusy || CcNetBusy;

        /// <summary>Host: what these skills have left on this piece.</summary>
        void CaptureKitNet(ref SkillWireState s)
        {
            if (slowLeft > 0f) s.Flags |= NetSlowed;
            if (checkAgainLeft > 0f) s.Flags |= NetCheckAgain;
            CaptureKitTwoNet(ref s);   // the kits' second version: light, heavy (RagdollPawn.KitTwo.cs)
            CaptureCcNet(ref s);       // the set 4판's hindrance (RagdollPawn.Cc.cs)
        }

        /// <summary>Client: the same on a puppet. (The time left is this screen's own count from when it first saw it.)</summary>
        void ApplyKitNet(in SkillWireState s)
        {
            var k = PawnRushSkills;
            SeeState(ref slowLeft, (s.Flags & NetSlowed) != 0, k != null ? k.fianSlowSeconds : 2f);
            SeeState(ref checkAgainLeft, (s.Flags & NetCheckAgain) != 0, k != null ? k.checkAgainWindow : 2f);
            ApplyKitTwoNet(in s);
            ApplyCcNet(in s);
        }

        void ClearKitNet()
        {
            slowLeft = 0f;
            checkAgainLeft = 0f;
            ClearKitTwoNet();
            ClearCc();
        }

        void AdvanceKitView(float dt)
        {
            if (slowLeft > 0f) slowLeft = Mathf.Max(0.05f, slowLeft - dt);
            if (checkAgainLeft > 0f) checkAgainLeft = Mathf.Max(0.05f, checkAgainLeft - dt);
            AdvanceKitTwoView(dt);
            AdvanceCcView(dt);
        }
    }
}
