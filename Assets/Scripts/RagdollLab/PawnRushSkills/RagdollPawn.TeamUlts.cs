using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The ultimates as the team's comeback move (JY-skill-lab, 2026-11-21; Docs/Skills/PAWN_RUSH_SKILL_SYSTEM.md 7.4 and
    /// Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6's ultimate rows; the user: "훨씬훨씬 더 강력하고, 체스 모티브도 살아 있고 아예 팀이
    /// 역전할 수 있는"). Each piece's most famous move, on the team's gauge (RagdollPawn.UltGauge.cs), with a 2 s warning
    /// every screen is told of; floored in it, the ultimate is cut and half the gauge is left. The further the team's fourth
    /// runner is behind, the bigger (Comeback: x0.5 to x2; what grows is each one's own).
    ///
    ///   앙파상 !!   the pawn shouts (2 s, it walks on). Every enemy that overtook it in the last 20 s (40 s at x2; four at
    ///               most, the farthest ahead first) is pulled back through the air to where it passed the pawn, and lies
    ///               1.5 s.
    ///   나이트 투어 onto the nearest enemy's head within 12 m, then from enemy to enemy (a hop each 0.5 s, the next within
    ///               12 m: the move keys pick it, or the one farthest ahead): each squashed 2 s, then sliding 5 m back. Four
    ///               hops (six at x2); the knight stands where the last one stood.
    ///   그리스 선물 hooks over the other team's front 1 (3 at x2) for 2 s; then the bishop's light goes to each in turn
    ///               (0.4 s each: the first body on the way takes it, a wall stops it) and the hooked are pulled back to
    ///               where the bishop stood, dazed 2 s. The bishop is taken (back to its checkpoint).
    ///   7번째 줄    5 m ahead of the other team's first runner (followed through the 2 s), a 3 m wall across the way:
    ///               its team cannot pass, ours walks through, and the ones before it are pushed back 1 m a second. It
    ///               stands 4 s (12 s at x2).
    ///   퀸 희생     onto where most enemies stand in the section (the left click: the next crowd); a crown's shadow grows
    ///               2 s there, then the queen falls and every enemy within 10 m (20 m at x2) flies about 10 m and lies
    ///               2.5 s. The queen is taken.
    ///   총동원      the king's trumpet (2 s, he walks on); then he flies beside the team's first runner, and two of the
    ///               rearmost teammates (four at x2) fly to him. The gathered are not floored for 5 s; enemies within
    ///               3 m are shoved off.
    ///
    /// Prototypes, lines and rings by PawnRushPlanMarks (PawnRushPlanMarks.KitTwo.cs). Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        // The multiplier the ultimate under way was cast at; the pieces it took; a step of its sequence.
        float ultF = 1f;
        readonly List<RagdollPawn> ultTaken = new List<RagdollPawn>();
        int ultStep;
        float ultStepAt;

        // A carried flight's landing (BeginTeamCarry): lie, sway or stand fast when it ends, and whose skill it was.
        float carryKnock, carryStagger, carryGuard;
        RagdollPawn carryBy;
        // A slide back some time after a squash (the knight's tour).
        float slideAt, slideMetres;
        Vector3 slideDir;

        // ---------------------------------------------------------------- shared

        /// <summary>The skill's small number (any PC): the queen's sacrifice's round (m), the 7th rank's width (tenths).</summary>
        public byte PlanNote => planNote;

        /// <summary>Carried through the air to <paramref name="floor"/> over about <paramref name="seconds"/> (the castling's
        /// flight, nothing on the way drops it), then: lying <paramref name="knock"/> s, swaying <paramref name="stagger"/> s,
        /// or not floored for <paramref name="guard"/> s. Its own skill stops.</summary>
        void BeginTeamCarry(Vector3 floor, float seconds, float knock, float stagger, float guard, RagdollPawn by)
        {
            if (State != PawnState.Active || Floating) return;
            if (SkillRunning && by != this) ClearSkills();
            Vector3 to = floor + Vector3.up * (standHeight + 0.05f);
            float top = Mathf.Max(bodies[0].position.y, to.y) + 2.5f + 0.06f * Flat(to - bodies[0].position).magnitude;
            BeginArcCarry(to, Mathf.Max(0.4f, seconds), top, null);
            carryKnock = knock;
            carryStagger = stagger;
            carryGuard = guard;
            carryBy = by;
        }

        /// <summary>A carried flight ended (EndCastleCarry): what it was to leave on landing.</summary>
        void LandTeamCarry(bool arrived)
        {
            float knock = carryKnock, stagger = carryStagger, guard = carryGuard;
            var by = carryBy;
            carryKnock = carryStagger = carryGuard = 0f;
            carryBy = null;
            if (!arrived) return;
            if (knock > 0f)
            {
                Knockdown(by != null ? PawnRushSkillBook.Name(by.activeSkill) : "궁극기", knock);
                if (by != null && IsEnemyOf(by)) GaugeKnocked(by);
            }
            else if (stagger > 0f) Stagger(stagger);
            if (guard > 0f) getUpGuardLeft = Mathf.Max(getUpGuardLeft, guard);
        }

        /// <summary>Every step (UpdateKitTwoStates): a slide back that was waiting goes.</summary>
        void UpdateSlideBack()
        {
            if (slideAt <= 0f || Time.time < slideAt) return;
            slideAt = 0f;
            if (State == PawnState.Active) Kick(slideDir, slideMetres, 0.5f, false);
        }

        /// <summary>The way a piece runs (its run, or the way it faces).</summary>
        static Vector3 RunWay(RagdollPawn p)
        {
            Vector3 v = Flat(p.bodies[0].linearVelocity);
            return v.sqrMagnitude > 1f ? v.normalized : FlatDir(p.facing);
        }

        /// <summary>Before an ultimate's key starts it: the warning (2 s) everyone is told of.</summary>
        void BeginTeamUlt(PawnRushSkillParams s, string detail)
        {
            SkillUses++;
            ultF = Comeback(team);
            ultTaken.Clear();
            ultStep = 0;
            planPhase = PlanPhase.Windup;
            planTime = 0f;
            planTarget = null;
            SkillDetail = detail;
            Log($"{DisplayName}: {PawnRushSkillBook.Name(activeSkillOrPressed)} — {s.teamUltWarn:0.#}초 예고 (역전 배율 ×{ultF:0.#})");
        }

        // (Set by RunNewSkill's caller for the log line: the skill a press is for.)
        int activeSkillOrPressed => activeSkill != 0 ? activeSkill : pressedSkill;
        int pressedSkill;

        void EndTeamUlt(PawnRushSkillParams s)
        {
            EndPlanSkill();
            StartCooldown(UltCooldown(pressedSkill, s));
        }

        // ---------------------------------------------------------------- P-궁: en passant !!

        // Every enemy's place against this pawn's in the race a step ago, and where and when each last overtook it.
        readonly Dictionary<RagdollPawn, bool> passantBehind = new Dictionary<RagdollPawn, bool>();
        readonly Dictionary<RagdollPawn, (float at, Vector3 where)> passantOvertook = new Dictionary<RagdollPawn, (float, Vector3)>();

        /// <summary>Every step while en passant !! sits in a slot (the host's): who overtakes this pawn, where, when.</summary>
        void TrackOvertakes()
        {
            float mine = Progress(this);
            foreach (var e in All)
            {
                if (e == null || e == this || !IsEnemyOf(e)) continue;
                bool behind = Progress(e) < mine;
                if (passantBehind.TryGetValue(e, out bool was) && was && !behind) passantOvertook[e] = (Time.time, e.FeetPoint);
                passantBehind[e] = behind;
            }
        }

        void UpdatePassantBang(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    pressedSkill = PawnRushSkillIds.PassantBang;
                    BeginTeamUlt(s, "앙파상! (외치는 2초, 걸을 수 있어요)");
                    planPoint = FeetPoint;
                    PopOver(this, PopPassant);
                    Fx(SkillFxKind.RookSlam, null, FeetPoint, FlatDir(facing), 0);   // (the shout: a heavy thud, borrowed)
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    planPoint = FeetPoint;
                    if (planTime < s.teamUltWarn) break;
                    PassantPull(s);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.3f) EndTeamUlt(s);
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The shout's end: every enemy that overtook the pawn within the window and is still ahead is pulled back
        /// to where it passed it (the farthest ahead first, passantMax at most).</summary>
        void PassantPull(PawnRushSkillParams s)
        {
            float window = ByComeback(ultF, s.passantWindow1, s.passantWindow2), mine = Progress(this);
            var pulled = new List<(RagdollPawn e, Vector3 where)>();
            foreach (var kv in passantOvertook)
            {
                var e = kv.Key;
                if (e == null || !IsEnemyOf(e) || e.State != PawnState.Active || e.SkillUntargetable) continue;
                if (Time.time - kv.Value.at > window || Progress(e) <= mine) continue;
                pulled.Add((e, kv.Value.where));
            }
            pulled.Sort((a, b) => Progress(b.e).CompareTo(Progress(a.e)));
            int n = Mathf.Min(pulled.Count, Mathf.Max(1, s.passantMax));
            for (int i = 0; i < n; i++)
            {
                var (e, where) = pulled[i];
                float across = Flat(where - e.FeetPoint).magnitude;
                e.BeginTeamCarry(where, Mathf.Clamp(across / Mathf.Max(1f, s.passantSpeed), 0.8f, 2.2f), s.passantKnock, 0f, 0f, this);
                e.LastSkillHit = $"앙파상 !!: {across:0} m 뒤로 끌려 돌아감";
                ultTaken.Add(e);
                Log($"앙파상 !! → {e.DisplayName}: {across:0} m 뒤, 앞질러 간 자리로");
            }
            FeelHit(null, FeetPoint, FlatDir(facing), FeelUltimate, Mathf.Max(1, n), false);
            if (n == 0)
            {
                FeelMiss(FeetPoint, FlatDir(facing));
                Log($"앙파상 !!: 지난 {window:0}초 동안 앞질러 간 적이 없음");
            }
            else Log($"앙파상 !!: {n}명 (지난 {window:0}초, ×{ultF:0.#})");
        }

        // ---------------------------------------------------------------- N-궁: the knight's tour

        /// <summary>The tour's next head from here: within tourReach, not stomped yet; the one the move keys point at, or the
        /// one farthest ahead.</summary>
        RagdollPawn TourNext(PawnRushSkillParams s, Vector3 from)
        {
            Vector3 want = Flat(skillMoveRaw);
            bool keyed = want.sqrMagnitude > 0.04f;
            RagdollPawn best = null;
            float bestScore = float.NegativeInfinity;
            foreach (var e in All)
            {
                if (e == null || e == this || !IsEnemyOf(e) || e.State != PawnState.Active || e.SkillUntargetable || ultTaken.Contains(e)) continue;
                Vector3 d = Flat(e.bodies[0].position - from);
                if (d.magnitude > s.tourReach || Mathf.Abs(e.bodies[0].position.y - from.y) > 4f) continue;
                float score = keyed ? Vector3.Dot(d.normalized, want.normalized) : Progress(e);
                if (score <= bestScore) continue;
                best = e;
                bestScore = score;
            }
            return best;
        }

        RagdollPawn TourNearest(PawnRushSkillParams s)
        {
            RagdollPawn best = null;
            float bestD = s.tourReach;
            foreach (var e in All)
            {
                if (e == null || e == this || !IsEnemyOf(e) || e.State != PawnState.Active || e.SkillUntargetable) continue;
                float d = Flat(e.bodies[0].position - bodies[0].position).magnitude;
                if (d > bestD) continue;
                best = e;
                bestD = d;
            }
            return best;
        }

        void UpdateKnightTour(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill(true)) break;
                    var first = TourNearest(s);
                    if (first == null)
                    {
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Knight);
                        Log($"나이트 투어: {s.tourReach:0} m 안에 적이 없음");
                        break;
                    }
                    pressedSkill = PawnRushSkillIds.KnightTour;
                    BeginTeamUlt(s, "나이트 투어 (말 울음)");
                    planTarget = first;
                    Fx(SkillFxKind.KnightLeap, null, FeetPoint, FlatDir(facing), 1);
                    break;
                }
                case PlanPhase.Windup:
                    planTime += dt;
                    // The first head: the nearest still, while the neigh lasts.
                    if (planTarget == null || planTarget.State != PawnState.Active) planTarget = TourNearest(s);
                    if (planTime < s.teamUltWarn) break;
                    if (planTarget == null || Flat(planTarget.bodies[0].position - bodies[0].position).magnitude > s.tourReach)
                    {
                        planTarget = TourNearest(s);
                        if (planTarget == null)
                        {
                            FeelMiss(FeetPoint, FlatDir(facing));
                            Log("나이트 투어: 첫 머리가 12 m 밖으로 흩어짐 — 끊김");
                            planPhase = PlanPhase.Recovery;
                            planTime = 0f;
                            break;
                        }
                    }
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    TourHop(s, planTarget);
                    break;
                case PlanPhase.Active:
                {
                    // (Between hops: the flight onto a head has just ended - UpdatePlanCarry ran it.)
                    planTime += dt;
                    var landed = planTarget;
                    if (landed != null && !ultTaken.Contains(landed)) TourStomp(s, landed);
                    int hops = Mathf.RoundToInt(ByComeback(ultF, s.tourHops1, s.tourHops2));
                    var next = ultTaken.Count < Mathf.Max(1, hops) ? TourNext(s, bodies[0].position) : null;
                    if (next == null)
                    {
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        Log($"나이트 투어: {ultTaken.Count}명 밟음 (×{ultF:0.#})");
                        break;
                    }
                    TourHop(s, next);
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.3f) EndTeamUlt(s);
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>A hop onto <paramref name="head"/>'s head (the carried flight, following it).</summary>
        void TourHop(PawnRushSkillParams s, RagdollPawn head)
        {
            planTarget = head;
            planDir = FlatDir(head.bodies[0].position - bodies[0].position);
            Vector3 top = head.bodies[(int)BodyId.Head].position + Vector3.up * 0.35f;
            BeginArcCarry(top, s.tourHop, Mathf.Max(bodies[0].position.y, top.y) + 2f, head);
            Fx(SkillFxKind.KnightLeap, null, FeetPoint, planDir, 0);
        }

        /// <summary>On a head: squashed, and sliding back a moment later.</summary>
        void TourStomp(PawnRushSkillParams s, RagdollPawn e)
        {
            ultTaken.Add(e);
            if (e.State != PawnState.Active) return;
            if (e.Squash(s.tourSquash, 0f)) e.GaugeKnocked(this);
            e.slideAt = Time.time + s.tourSquash;
            e.slideDir = -RunWay(e);
            e.slideMetres = s.tourSlide;
            e.LastSkillHit = $"나이트 투어: 납작 {s.tourSquash:0.#}초, 뒤로 {s.tourSlide:0} m";
            Vector3 head = e.bodies[(int)BodyId.Head].position;
            Fx(SkillFxKind.KnightStomp, e, head, planDir);
            FeelHit(e, head, planDir, FeelHard, ultTaken.Count, true);
            Log($"나이트 투어 → {e.DisplayName}: {ultTaken.Count}번째 머리");
        }

        // ---------------------------------------------------------------- B-궁: the Greek gift

        readonly List<RagdollPawn> giftMarked = new List<RagdollPawn>();

        /// <summary>The other team's front pieces, the farthest ahead first.</summary>
        void FrontEnemies(List<RagdollPawn> into, int n)
        {
            into.Clear();
            foreach (var e in All)
                if (e != null && e != this && IsEnemyOf(e) && e.State != PawnState.Ragdoll && !e.SkillUntargetable) into.Add(e);
            into.Sort((a, b) => Progress(b).CompareTo(Progress(a)));
            if (into.Count > n) into.RemoveRange(n, into.Count - n);
        }

        void UpdateGreekGift(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    int n = Mathf.Clamp(Mathf.RoundToInt(ByComeback(Comeback(team), s.giftCount1, s.giftCount2)), 1, 3);
                    FrontEnemies(giftMarked, n);
                    if (giftMarked.Count == 0)
                    {
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Bishop);
                        Log("그리스 선물: 상대가 없음");
                        break;
                    }
                    pressedSkill = PawnRushSkillIds.GreekGift;
                    BeginTeamUlt(s, "그리스 선물 (갈고리 표시 2초)");
                    foreach (var e in giftMarked) PopOver(e, PopHook);
                    Fx(SkillFxKind.BishopRopeThrow, null, LassoHands, FlatDir(facing));   // (the bell: the rope's swish, borrowed)
                    break;
                }
                case PlanPhase.Windup:
                    planTime += dt;
                    planPoint = FeetPoint;   // where the bishop leaves from
                    if (planTime < s.teamUltWarn) break;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    ultStep = 0;
                    ultStepAt = 0f;
                    SkillDetail = "그리스 선물: 빛이 되어 날아감";
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    // The light reaches one marked piece every giftPer seconds, in turn.
                    while (ultStep < giftMarked.Count && planTime >= ultStepAt + s.giftPer)
                    {
                        ultStepAt += s.giftPer;
                        GiftHook(s, giftMarked[ultStep]);
                        ultStep++;
                    }
                    if (ultStep < giftMarked.Count) break;
                    GiftPull(s);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    planTarget = null;
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime < s.giftTaken) break;
                    // The bishop is taken: back to its checkpoint (floored where it is, where nothing sends it back).
                    int pulled = ultTaken.Count;
                    EndTeamUlt(s);
                    if (SkillSendBack != null) SkillSendBack(this);
                    else Knockdown("그리스 선물 (비숍이 잡힘)");
                    Log($"그리스 선물: {pulled}명 끌어옴, 비숍은 잡힘");
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The light goes for a marked piece: the first body on the way takes the hook instead (a teammate of its
        /// may take it), a wall stops it.</summary>
        void GiftHook(PawnRushSkillParams s, RagdollPawn mark)
        {
            if (mark == null) return;
            Vector3 from = planPoint + Vector3.up * 1f, to = mark.LassoBelly;
            Vector3 d = to - from;
            float reach = d.magnitude;
            Vector3 dir = reach > 1e-3f ? d / reach : FlatDir(facing);
            planTarget = mark;
            if (SolidRay(from, dir, reach, out _))
            {
                Log($"그리스 선물 → {mark.DisplayName}: 벽에 막힘");
                return;
            }
            float along = reach + 0.5f;
            var first = FirstOnLine(from, dir, ref along, 0.5f, false);
            var hooked = first != null && IsEnemyOf(first) ? first : mark;
            if (ultTaken.Contains(hooked) || hooked.State != PawnState.Active) return;
            ultTaken.Add(hooked);
            planTarget = hooked;
            Fx(SkillFxKind.BishopRopeHook, hooked, hooked.LassoBelly, dir, 0, reach);
            Log($"그리스 선물 → {hooked.DisplayName}: 갈고리" + (hooked != mark ? $" ({mark.DisplayName} 대신)" : ""));
        }

        /// <summary>The hooked are pulled back to where the bishop left from, in a ring round it, dazed when they land.</summary>
        void GiftPull(PawnRushSkillParams s)
        {
            int n = ultTaken.Count;
            for (int i = 0; i < n; i++)
            {
                var e = ultTaken[i];
                if (e == null || e.State != PawnState.Active) continue;
                float a = i * Mathf.PI * 2f / Mathf.Max(1, n);
                Vector3 spot = planPoint + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.4f;
                e.BeginTeamCarry(spot, s.giftFlight, 0f, s.giftDaze, 0f, this);
                e.LastSkillHit = "그리스 선물: 비숍 자리로 끌려옴";
            }
            FeelHit(null, planPoint, FlatDir(facing), FeelUltimate, Mathf.Max(1, n), false);
            if (n == 0) FeelMiss(planPoint, FlatDir(facing));
        }

        // ---------------------------------------------------------------- R-궁: the 7th rank, across the way

        /// <summary>The other team's first runner.</summary>
        RagdollPawn EnemyLeader()
        {
            RagdollPawn best = null;
            float bestP = float.NegativeInfinity;
            foreach (var e in All)
            {
                if (e == null || e == this || !IsEnemyOf(e) || e.SkillUntargetable) continue;
                float p = Progress(e);
                if (p <= bestP) continue;
                best = e;
                bestP = p;
            }
            return best;
        }

        /// <summary>Where the wall would stand now: teamWallAhead before the leader, across its run (planPoint, planDir), the
        /// way's width short of the side walls (planNote, tenths of a metre).</summary>
        void PlaceRankWall(PawnRushSkillParams s, RagdollPawn leader)
        {
            Vector3 way = RunWay(leader);
            Vector3 middle = leader.FeetPoint + way * s.teamWallAhead;
            Vector3 side = Vector3.Cross(Vector3.up, way), from = middle + Vector3.up * 1f;
            float half = s.teamWallWidth * 0.5f, left = half, right = half;
            if (SolidRay(from, -side, half, out var hl)) left = Mathf.Max(0.5f, hl.distance - 0.1f);
            if (SolidRay(from, side, half, out var hr)) right = Mathf.Max(0.5f, hr.distance - 0.1f);
            // (Off the middle by the difference, so it reaches both side walls.)
            middle += side * ((right - left) * 0.5f);
            planPoint = middle;
            planDir = way;
            planNote = (byte)Mathf.Clamp(Mathf.RoundToInt((left + right) * 10f), 10, 255);
        }

        void UpdateRankWall(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    var leader = EnemyLeader();
                    if (leader == null)
                    {
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Rook);
                        Log("7번째 줄: 상대가 없음");
                        break;
                    }
                    pressedSkill = PawnRushSkillIds.RankWall;
                    BeginTeamUlt(s, "7번째 줄 (쇠창살 소리)");
                    planTarget = leader;
                    PlaceRankWall(s, leader);
                    Fx(SkillFxKind.RookSlam, null, FeetPoint, FlatDir(facing), 0);
                    break;
                }
                case PlanPhase.Windup:
                {
                    planTime += dt;
                    // The floor line goes with the first runner (its 5 m ahead) until the wall rises.
                    var leader = planTarget != null && planTarget.State != PawnState.Ragdoll ? planTarget : EnemyLeader();
                    if (leader != null)
                    {
                        planTarget = leader;
                        PlaceRankWall(s, leader);
                    }
                    if (planTime < s.teamUltWarn) break;
                    RaiseRankWall(s);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.3f) EndTeamUlt(s);
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void RaiseRankWall(PawnRushSkillParams s)
        {
            float seconds = ByComeback(ultF, s.teamWallStand1, s.teamWallStand2);
            Vector3 side = Vector3.Cross(Vector3.up, FlatDir(planDir));
            float half = planNote * 0.05f;
            Vector3 l = planPoint - side * half, r = planPoint + side * half;
            var wall = PawnRushKitWall.Raise(PawnRushKitWall.NextId(), this, l, r, s.teamWallHeight, s.teamWallThick, seconds, 0.3f, true);
            // Its own team walks through it; the other team is pushed back off its face.
            foreach (var mate in All)
                if (mate != null && mate != this && mate.team == team && team != Teams.None) mate.IgnoreBody(wall.Collider, true);
            wall.PushBack(team, FlatDir(planDir), s.teamWallBack);
            Fx(SkillFxKind.KitTeamWall, planTarget, l, FlatDir(planDir), wall.Id, seconds, r);
            FeelHit(null, planPoint, FlatDir(planDir), FeelUltimate, 1, false);
            Log($"7번째 줄: {(planTarget != null ? planTarget.DisplayName : "?")} 앞에 폭 {half * 2f:0.#} m 성벽, {seconds:0.#}초 (×{ultF:0.#})");
        }

        // ---------------------------------------------------------------- Q-궁: the queen's sacrifice, onto the crowd

        readonly List<Vector3> dropSpots = new List<Vector3>();
        int dropPick;

        /// <summary>The crowds to fall on, the most enemies within <paramref name="radius"/> first (each at least a radius
        /// from the ones before it).</summary>
        void FindCrowds(float radius)
        {
            dropSpots.Clear();
            var scored = new List<(Vector3 at, int n, float p)>();
            foreach (var e in All)
            {
                if (e == null || !IsEnemyOf(e) || e.SkillUntargetable) continue;
                int n = 0;
                foreach (var o in All)
                    if (o != null && IsEnemyOf(o) && !o.SkillUntargetable && Flat(o.bodies[0].position - e.bodies[0].position).magnitude <= radius) n++;
                scored.Add((e.FeetPoint, n, Progress(e)));
            }
            scored.Sort((a, b) => a.n != b.n ? b.n.CompareTo(a.n) : b.p.CompareTo(a.p));
            foreach (var c in scored)
            {
                bool near = false;
                foreach (var d in dropSpots)
                    if (Flat(d - c.at).magnitude < radius * 0.5f) near = true;
                if (!near) dropSpots.Add(c.at);
            }
        }

        void UpdateQueenDrop(PawnRushSkillParams s, bool press, bool confirm, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    float radius = ByComeback(Comeback(team), s.dropRadius1, s.dropRadius2);
                    FindCrowds(radius);
                    if (dropSpots.Count == 0)
                    {
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Queen);
                        Log("퀸 희생: 적이 없음");
                        break;
                    }
                    pressedSkill = PawnRushSkillIds.QueenDrop;
                    BeginTeamUlt(s, "퀸 희생 (좌클릭 = 다음 무리)");
                    dropPick = 0;
                    planPoint = dropSpots[0];
                    planNote = (byte)Mathf.Clamp(Mathf.RoundToInt(radius), 1, 255);
                    break;
                }
                case PlanPhase.Windup:
                    planTime += dt;
                    if (confirm || dropShoveNext)
                    {
                        dropShoveNext = false;
                        if (dropSpots.Count > 1)
                        {
                            dropPick = (dropPick + 1) % dropSpots.Count;
                            planPoint = dropSpots[dropPick];
                        }
                    }
                    if (planTime < s.teamUltWarn) break;
                    // From the sky: the queen comes down onto the spot as the shadow closes (at once: a fall of her own
                    // could floor her first and lose the blast).
                    Teleport(planPoint + Vector3.up * (standHeight + s.dropHeight), FlatDir(facing));
                    DropBlast(s);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime < s.sacShow) break;
                    EndTeamUlt(s);
                    if (SkillSendBack != null) SkillSendBack(this);
                    else Knockdown("퀸 희생 (퀸이 잡힘)");
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        // The left click while the shadow grows: the next crowd (taken from the input before the run: TeamUltKeys).
        bool dropShoveNext;

        void DropBlast(PawnRushSkillParams s)
        {
            float radius = planNote;
            Vector3 c = planPoint;
            int n = 0;
            foreach (var e in All)
            {
                if (e == null || e == this || !IsEnemyOf(e) || e.SkillUntargetable) continue;
                Vector3 d = Flat(e.bodies[0].position - c);
                if (d.magnitude > radius || Mathf.Abs(e.FeetPoint.y - c.y) > 4f) continue;
                Vector3 away = d.sqrMagnitude > 1e-4f ? d.normalized : FlatDir(facing);
                SkillHit(e, away * s.dropPush + Vector3.up * s.dropLift, true, "퀸 희생", false, s.dropHold);
                n++;
            }
            FeelHit(null, c, FlatDir(facing), FeelUltimate, Mathf.Max(1, n), false, s.sacStop);
            Fx(SkillFxKind.QueenBlast, null, c, FlatDir(facing), n, radius);
            Log($"퀸 희생: 둘레 {radius:0} m의 적 {n}명 날아감 (×{ultF:0.#}), 퀸은 잡힘");
        }

        // ---------------------------------------------------------------- K-궁: the general call

        Vector3 callPoint;

        /// <summary>This team's first runner (the king among them).</summary>
        RagdollPawn TeamLeader()
        {
            RagdollPawn best = null;
            float bestP = float.NegativeInfinity;
            foreach (var p in All)
            {
                if (p == null || p.team != team || p.State == PawnState.Ragdoll) continue;
                float pr = Progress(p);
                if (pr <= bestP) continue;
                best = p;
                bestP = pr;
            }
            return best;
        }

        void UpdateGeneralCall(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    pressedSkill = PawnRushSkillIds.GeneralCall;
                    BeginTeamUlt(s, "총동원 (나팔 2초, 걸을 수 있어요)");
                    PopOver(this, PopCall);
                    Fx(SkillFxKind.RookSlam, null, FeetPoint, FlatDir(facing), 0);   // (the trumpet: a heavy thud, borrowed)
                    break;
                case PlanPhase.Windup:
                {
                    planTime += dt;
                    if (planTime < s.teamUltWarn) break;
                    var leader = TeamLeader();
                    planTarget = leader;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    if (leader == null || leader == this)
                    {
                        // He leads already: the team comes to him where he stands.
                        callPoint = FeetPoint;
                        CallTeam(s);
                        break;
                    }
                    // Beside the first runner (castling: the king goes two squares at once).
                    Vector3 way = RunWay(leader), side = Vector3.Cross(Vector3.up, way);
                    callPoint = FloorNear(leader.FeetPoint + side * s.callSide);
                    float across = Flat(callPoint - FeetPoint).magnitude;
                    planPoint = callPoint;
                    BeginTeamCarry(callPoint, Mathf.Clamp(across / 20f, 0.8f, 2.5f), 0f, 0f, s.callGuard, this);
                    Log($"총동원: 킹이 {leader.DisplayName} 곁으로 ({across:0} m)");
                    break;
                }
                case PlanPhase.Active:
                    // (Landed beside the first runner: UpdatePlanCarry ran the flight.) The team comes once.
                    planTime += dt;
                    if (ultStep == 0) CallTeam(s);
                    if (planTime < 2.5f) break;
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.3f) EndTeamUlt(s);
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The rearmost teammates (2, 4 at x2) fly to the king; the gathered stand fast; enemies near are shoved off.</summary>
        void CallTeam(PawnRushSkillParams s)
        {
            ultStep = 1;
            int n = Mathf.Clamp(Mathf.RoundToInt(ByComeback(ultF, s.callCount1, s.callCount2)), 1, 5);
            var mates = new List<RagdollPawn>();
            foreach (var p in All)
                if (p != null && p != this && p != planTarget && p.team == team && team != Teams.None && p.State == PawnState.Active) mates.Add(p);
            mates.Sort((a, b) => Progress(a).CompareTo(Progress(b)));   // the rearmost first
            if (mates.Count > n) mates.RemoveRange(n, mates.Count - n);
            for (int i = 0; i < mates.Count; i++)
            {
                float a = (i + 1) * Mathf.PI * 2f / (mates.Count + 1);
                Vector3 spot = FloorNear(callPoint + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.6f);
                float across = Flat(spot - mates[i].FeetPoint).magnitude;
                mates[i].BeginTeamCarry(spot, Mathf.Clamp(across / 20f, 0.8f, 2.5f), 0f, 0f, s.callGuard, this);
                ultTaken.Add(mates[i]);
                Log($"총동원 → {mates[i].DisplayName}: {across:0} m 날아옴");
            }
            getUpGuardLeft = Mathf.Max(getUpGuardLeft, s.callGuard);
            if (planTarget != null && planTarget != this) planTarget.getUpGuardLeft = Mathf.Max(planTarget.getUpGuardLeft, s.callGuard);
            // Enemies in the way of the gathering are shoved off.
            foreach (var e in All)
            {
                if (e == null || !IsEnemyOf(e) || e.State != PawnState.Active || e.SkillUntargetable) continue;
                Vector3 d = Flat(e.bodies[0].position - callPoint);
                if (d.magnitude > s.callShove) continue;
                e.Kick(d.sqrMagnitude > 1e-4f ? d.normalized : FlatDir(facing), s.callShove + 0.5f - d.magnitude, 0.3f, false);
            }
            FeelHit(null, callPoint, FlatDir(facing), FeelUltimate, Mathf.Max(1, mates.Count), false);
            Log($"총동원: 팀원 {mates.Count}명이 킹 곁으로 (×{ultF:0.#}), {s.callGuard:0}초 동안 넘어지지 않음");
        }

        /// <summary>The floor under a point (or the point itself).</summary>
        Vector3 FloorNear(Vector3 p)
        {
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 3f, Vector3.down, hits, 8f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 at = p;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || ownSet.Contains(c) || ColliderOwner.ContainsKey(c) || PassesThrough(c)) continue;
                if (hits[i].distance >= best) continue;
                best = hits[i].distance;
                at = hits[i].point;
            }
            return at;
        }

        /// <summary>Before the run (PlanKeys): the queen's left click while the crown's shadow grows picks the next crowd.</summary>
        void TeamUltKeys()
        {
            if (planPhase != PlanPhase.Windup || activeSkill != PawnRushSkillIds.QueenDrop) return;
            if (input.shove) dropShoveNext = true;
            input.shove = false;
            input.shoveHeld = false;
        }

        void ClearTeamUlts()
        {
            ultTaken.Clear();
            ultStep = 0;
            dropShoveNext = false;
        }
    }
}
