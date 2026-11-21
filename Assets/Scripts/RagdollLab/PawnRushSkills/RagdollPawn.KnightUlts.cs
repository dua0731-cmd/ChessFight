using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The knight's two ultimates (JY-skill-lab, 2026-11-21: the user, after the previz "16-공통시전-나이트-퀸": "나이트의
    /// 궁극기 2개만 먼저 만들어줘 ... 진짜진짜 멋있게 연출해서 유니티에서 구현해줘", and "그냥 키 누르면 발동되도록 해줘"), on the
    /// team's gauge like the other comeback moves, and the first played on the ultimates' shared cast (PawnRushSkillFx.Ult.cs):
    /// Q, 2.1 s of gathering while the knight stands and the camera aims (what it aims at and every enemy it will hit is named
    /// for everyone, one heartbeat at a time), 0.3 s of silence, the call - then the knight's own move. The key always goes:
    /// with no enemy near, the knight still makes its big leap ahead.
    ///
    ///   패밀리 포크  Nf7+!!  onto where the enemies stand thickest within 32 m (the camera's way counts most), a 1.15 s leap
    ///                     16 m high, aimed ahead of the crowd's run; every enemy within 14 m (×0.5 7 m, ×2 20 m) is
    ///                     thrown high and comes down on a floor it ran over 20 m back, lying 2 s. Nobody near: a 16 m leap
    ///                     the way the camera looks.
    ///   나이트 투어  Nf5–d6–e8!!  onto the enemy nearest the camera's aim within 26 m, then head to head (a hop each
    ///                     0.42 s, the next within 12 m, the one furthest ahead first): each squashed 3 s, then sliding 4 m
    ///                     back. Five heads (×0.5 two, ×2 seven). Then one last leap to 10 m ahead of the enemy leader (30 m
    ///                     at most). Nobody near: that last leap alone, 18 m ahead.
    ///
    /// Everything the screens show is told by moments (SkillFxKind.Ult*): the cast, each enemy named, the call, each enemy
    /// struck, the landing and the end; online a client is sent them as any skill's moments. Not seen in Unity yet.
    /// </summary>
    public partial class RagdollPawn
    {
        // The tour's line of heads as named (in order), how many were told, the camera's way kept, the last leap under way.
        readonly List<RagdollPawn> ultRoute = new List<RagdollPawn>();
        bool ultTold, ultFinal;
        Vector3 ultAim = Vector3.forward;

        /// <summary>The knight's ultimates' cast: gathering, then a beat of silence (the shared cast's seconds).</summary>
        static float UltCastTime(PawnRushSkillParams s) => Mathf.Max(0.5f, s.ultGather) + Mathf.Max(0f, s.ultHush);

        /// <summary>When the screens start naming the targets (s into the cast, the shared cast's third heartbeat).</summary>
        static float UltMarkAt(PawnRushSkillParams s) => Mathf.Max(0.3f, s.ultGather) * 0.45f;

        /// <summary>An enemy an ultimate may aim at or hit: standing (not down, not shielded).</summary>
        bool UltFoe(RagdollPawn e) =>
            e != null && e != this && IsEnemyOf(e) && e.State == PawnState.Active && !e.SkillUntargetable && !e.Floating;

        /// <summary>The key: the cast begins. It always does; what it goes at is found while it gathers.</summary>
        void BeginKnightUlt(PawnRushSkillParams s, int id, string detail)
        {
            pressedSkill = id;
            BeginTeamUlt(s, detail);
            ultRoute.Clear();
            ultTold = ultFinal = false;
            ultAim = AimFlat();
            planDir = ultAim;
            planPoint = FeetPoint;
            planNote = 0;
            Fx(SkillFxKind.UltCast, null, FeetPoint, ultAim, id, UltCastTime(s));
        }

        void EndKnightUlt(PawnRushSkillParams s)
        {
            Fx(SkillFxKind.UltEnd, null, FeetPoint, planDir, 0);
            EndTeamUlt(s);
        }

        // ---------------------------------------------------------------- 패밀리 포크 (Nf7+!!)

        void UpdateFamilyFork(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill(true)) break;
                    BeginKnightUlt(s, PawnRushSkillIds.FamilyFork, "패밀리 포크 — 모으는 중 (카메라로 착지할 무리 고르기)");
                    FamAim(s);
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    if (planTime < s.ultGather)
                    {
                        ultAim = AimFlat();
                        planDir = ultAim;
                        FamAim(s);
                    }
                    if (planTime < UltCastTime(s)) break;
                    FamLeap(s);
                    break;
                case PlanPhase.Active:
                    // (The leap's carried flight holds the step back: this runs once it has come down.)
                    planTime += dt;
                    if (castleCarry) break;
                    FamLand(s);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.6f) EndKnightUlt(s);
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        float FamRadius(PawnRushSkillParams s) => ByComeback(ultF, s.famRadius1, s.famRadius2);

        /// <summary>The fork's landing (every step while it gathers): the crowd that is thickest, counting the camera's way
        /// most, within reach; its middle, on the floor. Nobody in reach: straight ahead. The round goes to every screen as
        /// the plan's small number (m).</summary>
        void FamAim(PawnRushSkillParams s)
        {
            float r = FamRadius(s), near = r * 0.7f;
            planNote = (byte)Mathf.Clamp(Mathf.RoundToInt(r), 1, 255);
            Vector3 me = FeetPoint;
            RagdollPawn best = null;
            float bestScore = 0f;
            foreach (var e in All)
            {
                if (!UltFoe(e)) continue;
                Vector3 d = Flat(e.FeetPoint - me);
                float far = d.magnitude;
                if (far > s.famReach || Mathf.Abs(e.FeetPoint.y - me.y) > 6f) continue;
                int crowd = 0;
                foreach (var o in All)
                    if (UltFoe(o) && Flat(o.FeetPoint - e.FeetPoint).magnitude <= near) crowd++;
                float way = far > 0.5f ? Vector3.Dot(d / far, ultAim) : 1f;
                float score = crowd * (1.6f + way);
                if (score <= bestScore) continue;
                best = e;
                bestScore = score;
            }
            planTarget = best;
            if (best == null)
            {
                planPoint = LandAhead(me, ultAim, s.famEmpty);
                return;
            }
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var o in All)
            {
                if (!UltFoe(o) || Flat(o.FeetPoint - best.FeetPoint).magnitude > near) continue;
                sum += o.FeetPoint;
                n++;
            }
            Vector3 middle = n > 0 ? sum / n : best.FeetPoint;
            var floor = UltFloor(middle);
            planPoint = floor ?? middle;
        }

        /// <summary>The call: the leap, aimed where the crowd will be when it comes down.</summary>
        void FamLeap(PawnRushSkillParams s)
        {
            float r = FamRadius(s);
            Vector3 to = planPoint;
            if (planTarget != null)
            {
                // Lead the crowd: its middle's run over the flight (what it runs in a second, as the leap is about that).
                Vector3 run = Vector3.zero;
                int n = 0;
                foreach (var o in All)
                {
                    if (!UltFoe(o) || Flat(o.FeetPoint - to).magnitude > r) continue;
                    run += Flat(o.bodies[0].linearVelocity);
                    n++;
                }
                if (n > 0)
                {
                    Vector3 ahead = to + run / n * (s.famFlight * 0.85f);
                    var floor = UltFloor(ahead);
                    if (floor.HasValue && Mathf.Abs(floor.Value.y - to.y) < 3f) to = floor.Value;
                }
            }
            planPoint = to;
            planPhase = PlanPhase.Active;
            planTime = 0f;
            SkillDetail = "패밀리 포크!!";
            Fx(SkillFxKind.UltGo, planTarget, to, planDir, PawnRushSkillIds.FamilyFork, 0f);
            float top = Mathf.Max(bodies[0].position.y, to.y) + s.famTop;
            BeginArcCarry(to + Vector3.up * standHeight, s.famFlight, top, null);
            Log($"패밀리 포크: 도약 → {Flat(to - FeetPoint).magnitude:0} m 앞 (둘레 {r:0} m, ×{ultF:0.#})");
        }

        /// <summary>The landing: every enemy within the round is thrown high and comes down on a floor it ran over, about
        /// forkBack metres back in the race, lying forkKnock seconds.</summary>
        void FamLand(PawnRushSkillParams s)
        {
            float r = FamRadius(s);
            Vector3 at = FeetPoint;
            planPoint = at;
            ultTaken.Clear();
            foreach (var e in All)
            {
                if (!UltFoe(e)) continue;
                if (Flat(e.FeetPoint - at).magnitude > r || Mathf.Abs(e.FeetPoint.y - at.y) > 5f) continue;
                Vector3 back = e.BackAlong(s.famBack, at);
                ultTaken.Add(e);
                Fx(SkillFxKind.UltStrike, e, e.bodies[(int)BodyId.Head].position, FlatDir(back - e.FeetPoint), ultTaken.Count);
                e.UltThrow(back, s.famThrowTime, s.famThrowTop, s.famKnock, this);
                e.LastSkillHit = $"패밀리 포크: {Flat(back - e.FeetPoint).magnitude:0} m 뒤로";
            }
            int n = ultTaken.Count;
            Fx(SkillFxKind.UltHit, null, at, planDir, n, r);
            FeelHit(null, at, planDir, FeelUltimate, Mathf.Max(1, n), false);
            if (n == 0)
            {
                FeelMiss(at, planDir);
                Log("패밀리 포크: 착지 둘레에 적이 없음 (앞으로 크게 뛰기만)");
            }
            else Log($"패밀리 포크: {n}명 하늘로, {s.famBack:0} m 뒤로 (둘레 {r:0} m, ×{ultF:0.#})");
        }

        /// <summary>Carried through the air on a high arc to <paramref name="floor"/>, then lying <paramref name="knock"/> s
        /// (the team ultimates' landing, RagdollPawn.TeamUlts.cs). Its own skill stops.</summary>
        void UltThrow(Vector3 floor, float seconds, float above, float knock, RagdollPawn by)
        {
            if (State != PawnState.Active || Floating) return;
            if (SkillRunning && by != this) ClearSkills();
            Vector3 to = floor + Vector3.up * (standHeight + 0.05f);
            float top = Mathf.Max(bodies[0].position.y, to.y) + Mathf.Max(1f, above);
            BeginArcCarry(to, Mathf.Max(0.4f, seconds), top, null);
            carryKnock = knock;
            carryStagger = 0f;
            carryGuard = 0f;
            carryBy = by;
        }

        // ---------------------------------------------------------------- 나이트 투어 (Nf5–d6–e8!!)

        void UpdateTourUlt(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill(true)) break;
                    BeginKnightUlt(s, PawnRushSkillIds.KnightTour, "나이트 투어 — 모으는 중 (카메라로 첫 머리 고르기)");
                    TourPlan(s);
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    if (planTime < UltMarkAt(s))
                    {
                        ultAim = AimFlat();
                        planDir = ultAim;
                        TourPlan(s);
                    }
                    else if (!ultTold) TourTell();
                    if (planTime < UltCastTime(s)) break;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = "나이트 투어!!";
                    ultTaken.Clear();
                    Fx(SkillFxKind.UltGo, planTarget, planTarget != null ? planTarget.FeetPoint : FeetPoint, planDir, PawnRushSkillIds.KnightTour, 0f);
                    TourNextHop(s);
                    break;
                case PlanPhase.Active:
                {
                    // (Between hops: the flight onto a head, or the last leap, has just ended - UpdatePlanCarry ran it.)
                    planTime += dt;
                    if (castleCarry) break;
                    if (ultFinal)
                    {
                        TourLanded(s);
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        break;
                    }
                    var landed = planTarget;
                    if (landed != null && !ultTaken.Contains(landed)) TourStompHead(s, landed);
                    TourNextHop(s);
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.6f) EndKnightUlt(s);
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        int TourHops(PawnRushSkillParams s) => Mathf.Max(1, Mathf.RoundToInt(ByComeback(ultF, s.ultTourHops1, s.ultTourHops2)));

        /// <summary>The line of heads (while it gathers, until they are named): the first the enemy nearest the camera's aim
        /// within ultTourStart, then each next within ultTourReach of the last, the one furthest ahead first.</summary>
        void TourPlan(PawnRushSkillParams s)
        {
            ultRoute.Clear();
            int hops = TourHops(s);
            Vector3 me = FeetPoint;
            RagdollPawn first = null;
            float bestScore = float.NegativeInfinity;
            foreach (var e in All)
            {
                if (!UltFoe(e)) continue;
                Vector3 d = Flat(e.FeetPoint - me);
                float far = d.magnitude;
                if (far > s.ultTourStart || Mathf.Abs(e.FeetPoint.y - me.y) > 5f) continue;
                float score = (far > 0.5f ? Vector3.Dot(d / far, ultAim) : 1f) * 2f - far / s.ultTourStart;
                if (score <= bestScore) continue;
                first = e;
                bestScore = score;
            }
            if (first != null)
            {
                ultRoute.Add(first);
                while (ultRoute.Count < hops)
                {
                    var next = TourPick(s, ultRoute[ultRoute.Count - 1].FeetPoint, s.ultTourReach);
                    if (next == null) break;
                    ultRoute.Add(next);
                }
            }
            planTarget = first;
            planPoint = first != null ? first.FeetPoint : LandAhead(me, ultAim, s.ultTourEmpty);
            planNote = (byte)ultRoute.Count;
        }

        /// <summary>A head not in the line yet within <paramref name="reach"/> of <paramref name="from"/>: the one furthest ahead.</summary>
        RagdollPawn TourPick(PawnRushSkillParams s, Vector3 from, float reach)
        {
            RagdollPawn best = null;
            float bestAhead = float.NegativeInfinity;
            foreach (var e in All)
            {
                if (!UltFoe(e) || ultRoute.Contains(e) || ultTaken.Contains(e)) continue;
                if (Flat(e.FeetPoint - from).magnitude > reach || Mathf.Abs(e.FeetPoint.y - from.y) > 4f) continue;
                float ahead = Progress(e);
                if (ahead <= bestAhead) continue;
                best = e;
                bestAhead = ahead;
            }
            return best;
        }

        /// <summary>The line is named, every head of it, for every screen (they show them one heartbeat at a time).</summary>
        void TourTell()
        {
            ultTold = true;
            for (int i = 0; i < ultRoute.Count; i++)
            {
                var e = ultRoute[i];
                if (e != null) Fx(SkillFxKind.UltMark, e, e.FeetPoint, planDir, i + 1);
            }
            if (ultRoute.Count == 0) Log("나이트 투어: 닿는 곳에 적이 없음 (마지막 도약만)");
        }

        /// <summary>The next hop: the next of the line still standing and in reach, or any other in reach (named now), or,
        /// with none, or the heads all taken, the last leap.</summary>
        void TourNextHop(PawnRushSkillParams s)
        {
            RagdollPawn next = null;
            if (ultTaken.Count < TourHops(s))
            {
                Vector3 from = bodies[0].position;
                float reach = ultTaken.Count == 0 ? s.ultTourStart : s.ultTourReach;
                foreach (var e in ultRoute)
                {
                    if (!UltFoe(e) || ultTaken.Contains(e) || Flat(e.bodies[0].position - from).magnitude > reach) continue;
                    next = e;
                    break;
                }
                if (next == null)
                {
                    next = TourPick(s, from, reach);
                    if (next != null)
                    {
                        ultRoute.Add(next);
                        Fx(SkillFxKind.UltMark, next, next.FeetPoint, planDir, ultRoute.Count);
                    }
                }
            }
            if (next != null)
            {
                planTarget = next;
                planDir = FlatDir(next.bodies[0].position - bodies[0].position);
                Vector3 top = next.bodies[(int)BodyId.Head].position + Vector3.up * 0.35f;
                BeginArcCarry(top, s.ultTourHop, Mathf.Max(bodies[0].position.y, top.y) + 2.2f, next);
                return;
            }
            TourFinalLeap(s);
        }

        /// <summary>On a head: squashed ultTourSquash seconds, then sliding back.</summary>
        void TourStompHead(PawnRushSkillParams s, RagdollPawn e)
        {
            ultTaken.Add(e);
            if (e.State != PawnState.Active) return;
            if (e.Squash(s.ultTourSquash, 0f)) e.GaugeKnocked(this);
            e.slideAt = Time.time + s.ultTourSquash;
            e.slideDir = -RunWay(e);
            e.slideMetres = s.ultTourSlide;
            e.LastSkillHit = $"나이트 투어: 납작 {s.ultTourSquash:0.#}초, 뒤로 {s.ultTourSlide:0} m";
            Vector3 head = e.bodies[(int)BodyId.Head].position;
            Fx(SkillFxKind.UltStrike, e, head, planDir, ultTaken.Count);
            FeelHit(e, head, planDir, FeelHard, ultTaken.Count, true);
            Log($"나이트 투어 → {e.DisplayName}: {ultTaken.Count}번째 머리");
        }

        /// <summary>The last leap: 10 m ahead of the enemy leader (30 m at most from here), or straight ahead with nobody.</summary>
        void TourFinalLeap(PawnRushSkillParams s)
        {
            ultFinal = true;
            Vector3 me = FeetPoint;
            RagdollPawn leader = null;
            float lead = float.NegativeInfinity;
            foreach (var e in All)
            {
                if (e == null || e == this || !IsEnemyOf(e)) continue;
                float p = Progress(e);
                if (p <= lead) continue;
                leader = e;
                lead = p;
            }
            Vector3 to;
            if (leader != null && lead > Progress(this) - 2f)
            {
                Vector3 want = leader.FeetPoint + RunWay(leader) * s.ultTourFinal;
                Vector3 d = Flat(want - me);
                float far = Mathf.Min(d.magnitude, s.ultTourFinalMax);
                to = d.magnitude > s.ultTourFinalMax || !UltFloor(want).HasValue ? LandAhead(me, FlatDir(d), far) : UltFloor(want).Value;
            }
            else to = LandAhead(me, ultAim, s.ultTourEmpty);
            planPoint = to;
            planTarget = null;
            planDir = FlatDir(to - me);
            float across = Flat(to - me).magnitude;
            Fx(SkillFxKind.UltGo, leader, to, planDir, PawnRushSkillIds.KnightTour, 1f);
            BeginArcCarry(to + Vector3.up * standHeight, Mathf.Clamp(across / 20f, 0.7f, 1.4f), Mathf.Max(bodies[0].position.y, to.y) + 5f + across * 0.15f, null);
            Log($"나이트 투어: {ultTaken.Count}명 밟고 마지막 도약 {across:0} m" + (leader != null ? $" ({leader.DisplayName} 앞)" : ""));
        }

        void TourLanded(PawnRushSkillParams s)
        {
            Vector3 at = FeetPoint;
            planPoint = at;
            Fx(SkillFxKind.UltHit, null, at, planDir, ultTaken.Count, 0f);
            FeelHit(null, at, planDir, FeelUltimate, Mathf.Max(1, ultTaken.Count), false);
            Log($"나이트 투어: {ultTaken.Count}명 (×{ultF:0.#})");
        }

        // ---------------------------------------------------------------- where to come down

        /// <summary>The floor at <paramref name="at"/> (looking down from 4 m over it; pieces do not count), or none.</summary>
        static Vector3? UltFloor(Vector3 at)
        {
            Vector3? found = null;
            float best = float.MaxValue;
            foreach (var h in Physics.RaycastAll(at + Vector3.up * 4f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.distance >= best || h.collider.GetComponentInParent<RagdollPawn>() != null) continue;
                best = h.distance;
                found = h.point;
            }
            return found;
        }

        /// <summary>A floor about <paramref name="dist"/> m along <paramref name="way"/> that is seen from here (the furthest
        /// such, at least 3 m), on about this level.</summary>
        Vector3 LandAhead(Vector3 from, Vector3 way, float dist)
        {
            way = FlatDir(way);
            for (float d = dist; d >= 3f; d -= 1.5f)
            {
                var f = UltFloor(from + way * d);
                if (!f.HasValue || Mathf.Abs(f.Value.y - from.y) > 5f) continue;
                if (!ClearSight(from + Vector3.up * 1.2f, f.Value + Vector3.up * 1.2f)) continue;
                return f.Value;
            }
            return from + way * 2f;
        }

        // ---------------------------------------------------------------- breadcrumbs (the host's)

        // Where this piece stood on the floor lately and how far along the race it was then: every 0.25 s for 12 s. A fork
        // throws its victims back onto a floor they really ran over.
        const int CrumbCount = 48;
        readonly Vector3[] crumbAt = new Vector3[CrumbCount];
        readonly float[] crumbProgress = new float[CrumbCount];
        int crumbNext, crumbFilled;
        float crumbClock;

        /// <summary>Every step (UpdateKitTwoStates): a crumb where it stands, every quarter second.</summary>
        void TrackCrumbs(float dt)
        {
            crumbClock += dt;
            if (crumbClock < 0.25f) return;
            crumbClock = 0f;
            if (State != PawnState.Active || !OnFloor || Floating || castleCarry) return;
            crumbAt[crumbNext] = FeetPoint;
            crumbProgress[crumbNext] = Progress(this);
            crumbNext = (crumbNext + 1) % CrumbCount;
            crumbFilled = Mathf.Min(CrumbCount, crumbFilled + 1);
        }

        /// <summary>A floor this piece stood on about <paramref name="metres"/> back in the race; if it has not run that far
        /// lately (a test dummy), a seen floor that far away from <paramref name="from"/> and back from its run.</summary>
        Vector3 BackAlong(float metres, Vector3 from)
        {
            float want = Progress(this) - metres;
            Vector3 best = FeetPoint;
            float bestGap = float.MaxValue;
            for (int i = 0; i < crumbFilled; i++)
            {
                int k = (crumbNext - 1 - i + CrumbCount) % CrumbCount;
                float gap = Mathf.Abs(crumbProgress[k] - want);
                if (gap >= bestGap) continue;
                bestGap = gap;
                best = crumbAt[k];
            }
            if (Flat(best - FeetPoint).magnitude >= metres * 0.5f) return best;
            Vector3 away = FlatDir(FeetPoint - from) + FlatDir(-RunWay(this));
            return LandAhead(FeetPoint, away.sqrMagnitude > 1e-4f ? away.normalized : -RunWay(this), metres);
        }
    }
}
