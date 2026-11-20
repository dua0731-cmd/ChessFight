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
    ///   R-중2, K-중1              the rook's chain and the king's call as they are, for now.
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
        }

        // ---------------------------------------------------------------- B-중7: the long diagonal snipe

        // The left click held this step (taken from the input before the run, so it does not dive); the small number's bits.
        bool snipeHeld;
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
                return Mathf.Lerp(s.snipeNear, s.snipeFar, Mathf.Clamp01(planTime / Mathf.Max(0.05f, s.snipeCharge)));
            }
        }

        /// <summary>The shot just fired took someone from beyond snipeKnockUpTo: "최선수" (any PC).</summary>
        public bool SnipeBest => PlanSkill == PawnRushSkillIds.LongSnipe && planPhase == PlanPhase.Recovery && (planNote & SnipeBestBit) != 0;

        /// <summary>A bishop that has drawn on this piece long enough for the light at its feet to show (any PC).</summary>
        public RagdollPawn SnipedBy()
        {
            foreach (var p in All)
                if (p != null && p != this && p.PlanSkill == PawnRushSkillIds.LongSnipe && p.planPhase == PlanPhase.Windup && p.planTarget == this
                    && p.PawnRushSkills != null && p.planTime >= p.PawnRushSkills.snipeWarnAfter) return p;
            return null;
        }

        /// <summary>Before the run (PlanKeys): kneeling from the key to the end of the sit after the shot, the bishop goes
        /// nowhere; the left click held is the draw's (not a dive), and while drawing the right click calls it off.</summary>
        void PoolKeys()
        {
            if (planPhase == PlanPhase.None || activeSkill != PawnRushSkillIds.LongSnipe) return;
            input.move = Vector3.zero;
            input.jump = false;
            input.sprint = false;
            snipeHeld = input.shoveHeld;
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
                    if (!snipeHeld) FireSnipe(s);   // a click let go at once: the short shot
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
                    if (!snipeHeld || planTime > s.snipeHold) FireSnipe(s);
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.snipeRecoilTime + s.snipeSit)
                    {
                        EndPlanSkill();
                        StartCooldown(s.snipeCooldown);
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
                result = SkillHit(first, push, true, far ? "긴 대각선 저격 (멀리서)" : "긴 대각선 저격");
                Fx(SkillFxKind.QueenHit, first, at, dir, 1);
                FeelHit(first, at, dir, FeelHard);
                if (far) planNote |= SnipeBestBit;
            }
            Log($"긴 대각선 저격 → {first.DisplayName}: {along:0.#} m → {result}" + ((planNote & SnipeBestBit) != 0 ? " — 최선수!" : ""));
        }
    }
}
