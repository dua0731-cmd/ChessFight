using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The skill pool's plan 1, one skill a piece (JY-skill-lab, 2026-11-20; Docs/Skills/PAWN_RUSH_SKILL_POOL.md, the user:
    /// "1안부터", and before it "이전까지 들어가있는 스킬들은 샘플에 가깝고, 우리가 기획서에서 만들었던 스킬들을 구현해야해").
    /// Each skill as its row in the pool writes it, with the fixes of its 손맛 and 검토 columns - except the cooldown given
    /// back for a hit that several rows have, which the user ruled out ("맞히면 쿨 감소는 밸런스를 너무 파괴해").
    ///
    ///   P-기1 첫 두 걸음 (꺾기)   the old two steps' code (RagdollPawn.PawnRushSkills.cs) with pawnSteer: the second press
    ///                            must come within 0.5 s and the move keys bend the second step up to 45° (it no longer goes
    ///                            for a target); one step alone, 3 s.
    ///   N-기1 ㄱ자 도약           the kits' bent leap (RagdollPawn.KitSkills.cs) with a 0.08 s hit stop on a head, a roll on
    ///                            if the jump key meets the landing (no stop after it), and "블런더" when it will come down
    ///                            on nothing.
    ///   B-중7 긴 대각선 저격      new, below: kneel, hold the left click to draw the line out to 30 m, let go to shoot; the
    ///                            farther the hit the harder (a stagger, a knockdown, a knockdown that throws); the shot
    ///                            throws the bishop 2 m back onto its seat.
    ///   Q-기1 체크!               plan 1-나's check (RagdollPawn.SkillPlans.cs) with 12 m, a 1.5 m lunge on the click, a
    ///                            0.08 s hit stop and a bounce when it comes down, and once more within 2 s after a dodge.
    ///   R-중2 뒤에서 잡기         the rook's chain (RagdollPawn.RookChain.cs) with chainPool: it catches on the first thing
    ///                            it meets; an enemy lighter than the rook is pulled in (to 0.9 m, close enough to grab at
    ///                            once), a heavier one (the queen, the king) or a wall, pillar or ledge pulls the rook to
    ///                            it; the left click held reels faster (the pool's mouse wheel does not travel to the
    ///                            host); the one pulled shakes it off half way with three left clicks; 16 s.
    ///   K-중1 왕의 부름           the king's call (RagdollPawn.KingCall.cs) with kingPool: the teammates come to the squares
    ///                            in front of him, left and right, the way he looked; the horn sets him half a step back;
    ///                            each landing shoves the enemies within 2 m; the key once more just after pushes the king
    ///                            and those set down 3 m on together; 20 s.
    ///
    /// Prototypes: lines and rings by PawnRushPlanMarks. Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        // ---------------------------------------------------------------- P-기1: the two steps, bent

        // The pawn's two steps run as the pool's (the second step bent by the move keys) rather than R153's (it seeks a target).
        bool pawnSteer;

        /// <summary>The second step's way: the first's, turned toward the move keys by at most steerTurn (straight on with no
        /// key).</summary>
        Vector3 SteeredSecond(PawnRushSkillParams s)
        {
            Vector3 was = FlatDir(skillDir);
            Vector3 want = Flat(skillMoveRaw);
            if (want.sqrMagnitude < 0.04f) return was;
            return Vector3.RotateTowards(was, want.normalized, s.steerTurn * Mathf.Deg2Rad, 0f);
        }

        // ---------------------------------------------------------------- N-기1: the leap's roll and its blunder

        /// <summary>The jump key met the landing: the knight rolls on along its leap instead of standing a moment (as far as
        /// there is floor ahead). False if there is no room to roll.</summary>
        bool BeginLeapRoll(PawnRushSkillParams s)
        {
            Vector3 dir = FlatDir(planDir);
            float room = s.leapRollDistance;
            Vector3 feet = FeetPoint;
            while (room > 0.3f && !FloorUnder(feet + dir * room)) room -= 0.25f;
            if (room <= 0.3f) return false;
            skillDir = dir;
            StartDash(s.leapRollSpeed, room / Mathf.Max(0.5f, s.leapRollSpeed));
            planNote |= LeapRolled;
            planPoint = feet + dir * room;
            SkillDetail = "착지 구르기";
            Fx(SkillFxKind.KnightTurn, null, bodies[0].position, dir, 0);   // (the kick-off's swish, borrowed)
            Log($"{DisplayName}: 착지에 맞춰 점프 → {room:0.#} m 구르기 (후딜 없음)");
            return true;
        }

        /// <summary>Where the leap will come down has no floor under it within leapBlunderDepth: a blunder (its small number
        /// says so to every PC).</summary>
        void MarkLeapBlunder(PawnRushSkillParams s)
        {
            bool blunder = !FloorUnder(planPoint, s.leapBlunderDepth);
            planNote = (byte)(blunder ? planNote | LeapBlunderBit : planNote & ~LeapBlunderBit);
            if (blunder) Log($"{DisplayName}: ㄱ자 도약 — 블런더 (내려설 곳이 없음)");
        }

        // ---------------------------------------------------------------- Q-기1: the check, once more

        // A dodged check opens a moment to check again (checkAgainLeft, on the host; a client's count from the flag); the next
        // check is that one (checkFollowUp), which opens none. checkAgainNext: the check under way was dodged.
        float checkAgainLeft;
        int checkAgainSlot;
        bool checkFollowUp, checkAgainNext;

        /// <summary>The queen may check once more, free, for this long (any PC).</summary>
        public float CheckAgainLeft => Mathf.Max(0f, checkAgainLeft);

        void OpenCheckAgain(PawnRushSkillParams s)
        {
            checkAgainLeft = Mathf.Max(0.1f, s.checkAgainWindow);
            checkAgainSlot = cdSlot;
        }

        /// <summary>Every step (UpdateKitStates, the host): the moment to check again runs out, and then the cooldown starts
        /// on the slot the check was in.</summary>
        void UpdatePoolStates(PawnRushSkillParams s, float dt)
        {
            if (summonPushLeft > 0f && (summonPushLeft -= dt) <= 0f) summonArrived[0] = summonArrived[1] = null;
            UpdatePromotion(dt);   // plan 3's promotion runs out (RagdollPawn.PoolUltimates.cs)
            if (checkAgainLeft <= 0f || PlanSkill == PawnRushSkillIds.Check) return;
            checkAgainLeft -= dt;
            if (checkAgainLeft > 0f) return;
            int was = cdSlot;
            cdSlot = Mathf.Clamp(checkAgainSlot, 0, slotCd.Length - 1);
            StartCooldown(s.checkCooldown);
            cdSlot = was;
        }

        void ClearPoolStates()
        {
            checkAgainLeft = 0f;
            checkFollowUp = checkAgainNext = false;
            summonPushLeft = 0f;
            summonArrived[0] = summonArrived[1] = null;
            promoteLeft = 0f;   // (a reset keeps the piece it is given: no going back)
            ClearKitTwoStates();   // the kits' second version: light, heavy (RagdollPawn.KitTwo.cs)
        }

        // ---------------------------------------------------------------- B-중7: the long diagonal snipe

        // The left click held this step, for the skills drawn with it (the snipe, the pawn's spear, the pin: taken from the
        // input before the run, so it does not dive); the snipe's small number's bits.
        bool drawHeld;
        const byte SnipeHitBit = 1, SnipeBestBit = 2;

        /// <summary>How far the kneeling bishop's line reaches now: snipeNear kneeling, out to snipeFar over snipeCharge of
        /// drawing (any PC: the time comes with the host's skill state).</summary>
        public float SnipeReach
        {
            get
            {
                var s = PawnRushSkills;
                if (s == null) return 0f;
                if (planPhase != PlanPhase.Windup) return s.snipeNear;
                // (The set 1안-a's mid snipe reaches less far: snipeMidFar. Any PC: the slot under way says which.)
                float far = ActiveVariant == PawnRushSkillIds.SnipeMid ? s.snipeMidFar : s.snipeFar;
                return Mathf.Lerp(s.snipeNear, far, Mathf.Clamp01(planTime / Mathf.Max(0.05f, s.snipeCharge)));
            }
        }

        /// <summary>The shot just fired took someone from beyond snipeKnockUpTo: "최선수" (any PC).</summary>
        public bool SnipeBest => PawnRushSkillIds.Snipe(PlanSkill) && planPhase == PlanPhase.Recovery && (planNote & SnipeBestBit) != 0;

        /// <summary>A bishop that has drawn on this piece long enough for the light at its feet to show (any PC).</summary>
        public RagdollPawn SnipedBy()
        {
            foreach (var p in All)
                if (p != null && p != this && PawnRushSkillIds.Snipe(p.PlanSkill) && p.planPhase == PlanPhase.Windup && p.planTarget == this
                    && p.PawnRushSkills != null && p.planTime >= p.PawnRushSkills.snipeWarnAfter) return p;
            return null;
        }

        /// <summary>Before the run (PlanKeys): the skills drawn with the left click - the kneeling bishop's snipe (it goes
        /// nowhere from the key to the end of its sit), the pawn's spear and the pin (a walk while drawing): the left click
        /// held is the draw's (not a dive), and while drawing the right click calls it off. The fork has its own.</summary>
        void PoolKeys()
        {
            PoolBasicKeys();   // plan 3's basics (RagdollPawn.PoolBasics.cs)
            Plan2Keys();       // plan 2's (RagdollPawn.PoolPlan2.cs)
            UltKeys();         // plan 3's ultimates (RagdollPawn.PoolUltimates.cs)
            if (planPhase == PlanPhase.None) return;
            if (activeSkill == PawnRushSkillIds.Fork)
            {
                ForkKeys();
                return;
            }
            bool snipe = PawnRushSkillIds.Snipe(activeSkill);
            if (!snipe && activeSkill != PawnRushSkillIds.ChaseAway && activeSkill != PawnRushSkillIds.Pin
                && activeSkill != PawnRushSkillIds.FileCharge) return;
            if (snipe)
            {
                input.move = Vector3.zero;
                input.jump = false;
                input.sprint = false;
            }
            else if (planPhase != PlanPhase.Aim && planPhase != PlanPhase.Windup) return;   // (thrusting, or the pin held: free)
            else if (planPhase == PlanPhase.Windup) input.sprint = false;
            drawHeld = input.shoveHeld;
            input.shoveHeld = false;
            if (planPhase == PlanPhase.Aim) return;   // (the aim's clicks are read after this: the left starts the draw)
            input.shove = false;
            // Drawing, the right click calls it off; held on, it grabs nothing until let go (PreSkills clears it by the latch).
            if (planPhase == PlanPhase.Windup && input.grab && !skillGrabLatch)
            {
                skillCancel = true;
                skillGrabLatch = true;
            }
        }

        void UpdateLongSnipe(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    planPhase = PlanPhase.Aim;
                    planTime = 0f;
                    planNote = 0;
                    skillGrabLatch = skillGrabRaw;
                    planDir = LineAim(s.snipeNear, s.snipeAssist, s.snipeTall);
                    facing = FlatDir(planDir);
                    SkillDetail = "긴 대각선 저격: 무릎 꿇음 (좌클릭 누르고 모으기 · 떼면 쏨 · 우클릭/스킬 키 일어남)";
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    planDir = LineAim(s.snipeNear, s.snipeAssist, s.snipeTall);
                    facing = FlatDir(planDir);
                    if (cancel || press || planTime > s.snipeHold)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        Log("긴 대각선 저격: 일어남 (안 쏨)");
                        break;
                    }
                    if (!confirm) break;
                    SkillUses++;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    SkillDetail = "긴 대각선 저격: 모으는 중 (떼면 쏨 · 우클릭 취소)";
                    if (!drawHeld) FireSnipe(s);   // a click let go at once: the short shot
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    planDir = LineAim(SnipeReach, s.snipeAssist, s.snipeTall);
                    facing = FlatDir(planDir);
                    if (cancel || press)
                    {
                        // Let go unshot (no cooldown, as an aim called off).
                        EndPlanSkill();
                        Log("긴 대각선 저격: 모으다 취소");
                        break;
                    }
                    if (!drawHeld || planTime > s.snipeHold) FireSnipe(s);
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.snipeRecoilTime + s.snipeSit)
                    {
                        // (The kits' second version's high snipe is the same shot on a high skill's cooldown.)
                        float cooldown = activeSkill == PawnRushSkillIds.SnipeHigh ? s.snipeHighCooldown : s.snipeCooldown;
                        EndPlanSkill();
                        StartCooldown(cooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The shot, at once along the drawn line: the first piece on it short of a wall takes it, harder the farther
        /// it stands (a teammate or a wall only stops it). The shot throws the bishop back onto its seat - a mid skill's
        /// recoil, so over an edge it goes.</summary>
        void FireSnipe(PawnRushSkillParams s)
        {
            float reach = SnipeReach;
            Vector3 from = LassoHands, dir = planDir.sqrMagnitude > 0.5f ? planDir.normalized : FlatDir(facing);
            if (SolidRay(from, dir, reach, out var wall)) reach = wall.distance;
            float along = reach;
            var first = FirstOnLine(from, dir, ref along, s.snipeRadius, s.snipeAlliesBlock);
            planTarget = first;
            planPoint = from + dir * along;
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            planNote = 0;
            SkillDetail = "긴 대각선 저격 (엉덩방아)";
            Kick(-dir, s.snipeRecoil, s.snipeRecoilTime, false);
            if (first == null)
            {
                FeelMiss(planPoint, dir);
                Log($"긴 대각선 저격: 아무도 안 맞음 ({reach:0.#} m)");
                return;
            }
            if (!IsEnemyOf(first))
            {
                Fx(SkillFxKind.SkillFail, null, first.LassoBelly, dir, (int)PieceKind.Bishop);
                Log($"긴 대각선 저격: 팀원 {first.DisplayName}에게 막힘 ({along:0.#} m)");
                return;
            }
            planNote |= SnipeHitBit;
            string result;
            Vector3 at = first.LassoBelly;
            if (along <= s.snipeStaggerUpTo)
            {
                bool swayed = first.Stagger(s.snipeStagger);
                result = swayed ? $"휘청 {s.snipeStagger:0.#}초" : "휘청 안 걸림";
                first.LastSkillHit = $"긴 대각선 저격: {result}";
                Fx(SkillFxKind.PawnHit, first, at, dir, 0);
                FeelHit(first, at, dir, FeelWeak);
            }
            else
            {
                bool far = along > s.snipeKnockUpTo;
                Vector3 push = far ? dir * s.snipeFlyPush + Vector3.up * s.snipeFlyLift : dir * s.snipePush + Vector3.up * s.snipeLift;
                result = SkillHit(first, push, true, far ? "긴 대각선 저격 (멀리서)" : "긴 대각선 저격", false, far ? s.snipeFlyHold : s.snipeKnockHold);
                Fx(SkillFxKind.QueenHit, first, at, dir, 1);
                FeelHit(first, at, dir, FeelHard);
                if (far) planNote |= SnipeBestBit;
            }
            Log($"긴 대각선 저격 → {first.DisplayName}: {along:0.#} m → {result}" + ((planNote & SnipeBestBit) != 0 ? " — 최선수!" : ""));
        }

        // ---------------------------------------------------------------- R-중2: the chain from behind

        // The rook's chain runs as the pool's; the rook flying to what it caught (a client's from the skill state); its left
        // click held this step. The piece on the chain: its left clicks so far.
        bool chainPool, chainSelf, chainReel;
        int chainShakes;
        float chainSelfT, chainSelfTime, chainSelfFloor;
        Vector3 chainSelfFrom, chainSelfTo;

        float ChainCooldown(PawnRushSkillParams s) => chainPool ? s.behindCooldown : s.rookCooldown;

        /// <summary>The chain's weights are the pieces' values, the king heaviest of all.</summary>
        static int ChainWeight(PieceKind k) => k == PieceKind.King ? 100 : PieceValue(k);

        bool LighterThanMe(RagdollPawn other) => ChainWeight(other.piece) < ChainWeight(piece);

        /// <summary>An enemy rook's chain pulling this piece in now (any PC), so it knows to shake it off.</summary>
        public RagdollPawn ChainPulledBy()
        {
            foreach (var p in All)
                if (p != null && p != this && p.chainPhase == ChainPhase.Pull && p.chainOn == this && !p.chainSelf
                    && p.ActiveSkill == PawnRushSkillIds.ChainBehind && IsEnemyOf(p)) return p;
            return null;
        }

        /// <summary>The chain caught something the rook cannot pull (a heavier enemy, or <paramref name="heavy"/> null: a wall,
        /// pillar or ledge at <paramref name="anchor"/>): the rook goes to it instead, straight, staying short of it.</summary>
        void StartSelfPull(PawnRushSkillParams s, RagdollPawn heavy, Vector3 anchor)
        {
            chainPhase = ChainPhase.Pull;
            chainT = 0f;
            chainOn = heavy;
            chainSelf = true;
            chainSelfT = 0f;
            chainSelfFrom = bodies[0].position;
            chainSelfFloor = FeetPoint.y;
            chainSelfTo = SelfPullEnd(s, heavy, anchor);
            chainSelfTime = Mathf.Max(0.25f, (chainSelfTo - chainSelfFrom).magnitude / Mathf.Max(1f, s.behindPullSpeed));
            SkillDetail = heavy != null ? "무거운 적에게 끌려감" : "벽 · 턱으로 끌려감";
            Fx(SkillFxKind.RookHook, heavy, chainTip, skillDir, 0, chainSelfTime);
            FeelHit(heavy, anchor, skillDir, FeelWeak, 1, heavy != null);
            Log($"뒤에서 잡기 → {(heavy != null ? heavy.DisplayName + "(나보다 무거움)" : "벽 · 턱")}: 룩이 끌려감 {(chainSelfTo - chainSelfFrom).magnitude:0.0} m");
        }

        /// <summary>Where the rook's hips end up: short of what it caught along the flat way to it - a heavy piece's standing
        /// height, or a wall's or ledge's catch point (never under standing on its own floor).</summary>
        Vector3 SelfPullEnd(PawnRushSkillParams s, RagdollPawn heavy, Vector3 anchor)
        {
            Vector3 from = bodies[0].position;
            Vector3 way = Flat(anchor - from);
            way = way.sqrMagnitude > 1e-4f ? way.normalized : FlatDir(facing);
            if (heavy != null)
            {
                Vector3 end = heavy.bodies[0].position - way * s.behindHeavyGap;
                end.y = heavy.FeetPoint.y + standHeight;
                return end;
            }
            Vector3 at = anchor - way * s.behindWallGap;
            at.y = Mathf.Max(anchor.y, chainSelfFloor + standHeight);
            return at;
        }

        void StepSelfPull(PawnRushSkillParams s, float dt)
        {
            float run = dt * (chainReel ? s.behindReel : 1f);
            chainSelfT += run;
            var heavy = chainOn;
            if (heavy != null && heavy.State == PawnState.Active)
            {
                // A heavy piece that moves is followed.
                chainSelfTo = SelfPullEnd(s, heavy, heavy.bodies[(int)BodyId.Chest].position);
                chainTip = heavy.bodies[(int)BodyId.Chest].position;
            }
            if (chainSelfT >= chainSelfTime)
            {
                EndSelfPull(s, true);
                return;
            }
            float u = Mathf.Clamp01((chainSelfT + run) / chainSelfTime);
            float e = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * u);
            Vector3 at = bodies[0].position, next = Vector3.Lerp(chainSelfFrom, chainSelfTo, e);
            Vector3 step = next - at;
            if (step.sqrMagnitude > 1e-6f && ChainBlocked(at, step))
            {
                Log($"{DisplayName}: 끌려가다 장애물에 걸림");
                EndSelfPull(s, false);
                return;
            }
            Fly(next, step / Mathf.Max(1e-4f, dt), 0f, default, 18f);
        }

        void EndSelfPull(PawnRushSkillParams s, bool arrived)
        {
            chainSelf = false;
            EndFlight(arrived ? Vector3.zero : bodies[0].linearVelocity * 0.5f);
            KeepSkillLandingSafe();
            Fx(SkillFxKind.RookPullEnd, chainOn, FeetPoint, FlatDir(skillDir), 0);
            FinishChain(s);
        }

        // ---------------------------------------------------------------- K-중1: the king's call, in front of him

        // The king's call runs as the pool's; the way he looked as he called; the teammates set down, and how long the key
        // may still push them on.
        bool kingPool;
        Vector3 summonFwd;
        readonly RagdollPawn[] summonArrived = new RagdollPawn[2];
        float summonPushLeft;

        /// <summary>The king may push on with those set down for this long (the host's).</summary>
        public float SummonPushLeft => Mathf.Max(0f, summonPushLeft);

        /// <summary>A called teammate set down in front of this king: the enemies round its square are shoved off it, and the
        /// moment to push on together opens.</summary>
        void SummonLanded(RagdollPawn mate, int slot, Vector3 at)
        {
            var s = PawnRushSkills;
            if (s == null) return;
            if (slot >= 0 && slot < summonArrived.Length) summonArrived[slot] = mate;
            summonPushLeft = Mathf.Max(0.1f, s.summonPushWindow);
            int shoved = 0;
            foreach (var other in All)
            {
                if (other == null || other == this || other == mate || !IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable) continue;
                Vector3 d = Flat(other.bodies[0].position - at);
                if (d.magnitude > s.summonPushRadius || Mathf.Abs(other.FeetPoint.y - at.y) > 1.2f) continue;
                Vector3 away = d.sqrMagnitude > 1e-4f ? d.normalized : FlatDir(summonFwd);
                SkillHit(other, away * s.summonShove + Vector3.up * 0.8f, false, "왕의 부름 (내려섬)");
                FeelHit(other, other.bodies[0].position, away, FeelWeak);
                shoved++;
            }
            Log($"왕의 부름: {(mate != null ? mate.DisplayName : "팀원")} 내려섬 → 둘레 적 {shoved}명 밀림 · {s.summonPushWindow:0.#}초 안에 스킬 키 = 함께 밀고 나가기");
        }

        /// <summary>The king's key in the moment after a landing: he and those set down (still near) push on together along
        /// the way he called. True if the key was used for it.</summary>
        bool SummonPushKey(PawnRushSkillParams s, bool press)
        {
            if (!press || summonPushLeft <= 0f) return false;
            summonPushLeft = 0f;
            Vector3 dir = summonFwd.sqrMagnitude > 0.5f ? summonFwd : FlatDir(facing);
            Kick(dir, s.summonPushDistance, s.summonPushTime, true);
            int n = 0;
            for (int i = 0; i < summonArrived.Length; i++)
            {
                var mate = summonArrived[i];
                summonArrived[i] = null;
                if (mate == null || mate.State != PawnState.Active || Flat(mate.bodies[0].position - bodies[0].position).magnitude > 4f) continue;
                mate.Kick(dir, s.summonPushDistance, s.summonPushTime, true);
                n++;
            }
            Log($"왕의 부름: 킹과 팀원 {n}명이 함께 {s.summonPushDistance:0.#} m 밀고 나감");
            return true;
        }
    }
}
