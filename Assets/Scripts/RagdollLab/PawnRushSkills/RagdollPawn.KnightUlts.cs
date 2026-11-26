using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The knight's two ultimates, played from start to end (JY-skill-lab, 2026-11-22). The first version stood the knight
    /// still for 2.1 s and picked its targets for it; the user: "이동하다가 스킬을 시전하면 멈추는것도 불편하고, 적 타겟팅이나
    /// 그런것도 제대로 안되서", and "엄연히 스킬인데 왜 조작이 안되게 하려는거야? 시전 중에도 계속 유저랑 상호작용할 수 있고, 유저의
    /// 실력에 따라 더 좋은 결과를 낼 수도 있고, 덜 좋은 결과를 낼 수도 있어야지". Now Q goes at once, from a run or from the air;
    /// the player steers, picks and times every part; the result is judged like a chess move.
    ///
    ///   패밀리 포크  Q: the knight leaps high (1.6 s in the air). Its landing square starts where it forks the most enemies
    ///              the camera looks at (within 60°) and the move keys move it (9 m/s, 30 m from the take-off at most); the eight knight's squares
    ///              round it show who it would fork. Q again in the last 0.22 s of the fall (the closing ring) is on the
    ///              beat: the squares catch wider and the forked fly further. On landing every enemy on a knight's square is
    ///              forked (thrown high, 20 m back onto a floor it ran over, 2 s down); one landed on is floored 1.5 s; the
    ///              rest within 7 m sway. One press: an early one is spent.
    ///   나이트 투어  before the key, a red ring shows the head it would take (the enemy nearest the camera's way within
    ///              22 m). Q: onto that head. On each head (squashed 2.5 s, then sliding back) the knight rides it 0.8 s
    ///              while the camera picks the next within 12 m; Q hops there, and on the beat (0.3 to
    ///              0.55 s after it landed) the next is squashed a second longer. No press, or no head: the last leap,
    ///              aimed 10 m ahead of the enemy leader (30 m at most) and steered like the fork's. Six heads at most
    ///              (×0.5 three, ×2 eight). Nobody in reach at the key: the last leap alone.
    ///
    /// The judgement (SkillFxKind.UltHit's size): 0 !! brilliant, 1 ! great, 2 ★ best, 3 ?! inaccuracy, 4 ?? blunder.
    /// What the screens show is told by moments (SkillFxKind.Ult*) and the plan's point (the fork's landing square) and
    /// target (the tour's next head). Not seen in Unity yet.
    /// </summary>
    public partial class RagdollPawn
    {
        public const int GradeBrilliant = 0, GradeGreat = 1, GradeBest = 2, GradeInaccuracy = 3, GradeBlunder = 4;

        /// <summary>The knight's squares from a landing, in squares (the board's own axes).</summary>
        public static readonly Vector2Int[] KnightMoves =
        {
            new Vector2Int(1, 2), new Vector2Int(2, 1), new Vector2Int(2, -1), new Vector2Int(1, -2),
            new Vector2Int(-1, -2), new Vector2Int(-2, -1), new Vector2Int(-2, 1), new Vector2Int(-1, 2),
        };

        // The steered flight (the fork's leap, the tour's hops and last leap): where it left, where it comes down (moved by the
        // keys, or following a head), its time and height.
        Vector3 ultFrom, ultTo, ultStart;
        float ultFlightT, ultFlightTime, ultFlightTop, ultStand;
        RagdollPawn ultHead;
        bool ultFlying, ultPerfect, ultPressed, ultFinal;
        int ultPerfects;

        /// <summary>An enemy an ultimate may aim at or hit: standing (not down, not shielded).</summary>
        bool UltFoe(RagdollPawn e) =>
            e != null && e != this && IsEnemyOf(e) && e.State == PawnState.Active && !e.SkillUntargetable && !e.Floating;

        /// <summary>The key: it goes at once (no warning: the flight is the play).</summary>
        void BeginKnightUlt(PawnRushSkillParams s, int id, string detail, float seconds)
        {
            pressedSkill = id;
            SkillUses++;
            ultF = Comeback(team);
            ultTaken.Clear();
            ultPerfects = 0;
            ultFinal = ultPressed = ultPerfect = ultFlying = false;
            ultHead = null;
            planPhase = PlanPhase.Active;
            planTime = 0f;
            planTarget = null;
            planNote = 0;
            planDir = AimFlat();
            ultStart = FeetPoint;
            SkillDetail = detail;
            Fx(SkillFxKind.UltCast, null, FeetPoint, planDir, id, seconds);
            Log($"{DisplayName}: {PawnRushSkillBook.Name(id)} (역전 배율 ×{ultF:0.#})");
        }

        void EndKnightUlt(PawnRushSkillParams s)
        {
            if (ultFlying)
            {
                ultFlying = false;
                EndFlight();
            }
            Fx(SkillFxKind.UltEnd, null, FeetPoint, planDir, 0);
            EndTeamUlt(s);
        }

        static int Judge(int score) => Mathf.Clamp(score, GradeBrilliant, GradeBlunder);

        // ---------------------------------------------------------------- the steered flight

        void UltLeap(Vector3 hipsTo, float seconds, float height, RagdollPawn head)
        {
            if (!ultFlying)
            {
                DropHook();
                DropRope();
                EndClimbPose();
                LetGo(0.3f);
            }
            ultFrom = bodies[0].position;
            ultTo = hipsTo;
            ultHead = head;
            ultFlightT = 0f;
            ultFlightTime = Mathf.Max(0.2f, seconds);
            ultFlightTop = Mathf.Max(0.3f, height);
            ultFlying = true;
            ultPressed = false;
        }

        Vector3 UltArc(float u)
        {
            u = Mathf.Clamp01(u);
            float e = u * 0.65f + u * u * (3f - 2f * u) * 0.35f;
            return Vector3.Lerp(ultFrom, ultTo, e) + Vector3.up * (4f * ultFlightTop * u * (1f - u));
        }

        /// <summary>The flight this step (the hips carried along the arc, its end where it is now); true once it is down.</summary>
        bool UltFly(float dt)
        {
            if (!ultFlying) return true;
            ultFlightT += dt;
            grabImmune = Mathf.Max(grabImmune, 0.25f);
            if (ultHead != null && ultHead.State == PawnState.Active)
            {
                Vector3 head = ultHead.bodies[(int)BodyId.Head].position + Vector3.up * 0.35f;
                ultTo = Vector3.MoveTowards(ultTo, head, 9f * dt);
            }
            if (ultFlightT >= ultFlightTime)
            {
                ultFlying = false;
                EndFlight(ultHead != null ? (Vector3?)null : Vector3.zero);
                if (ultHead == null) KeepSkillLandingSafe();
                return true;
            }
            Vector3 at = bodies[0].position, next = UltArc((ultFlightT + dt) / ultFlightTime);
            Fly(next, (next - at) / Mathf.Max(1e-4f, dt), 0f, default, 18f);
            return false;
        }

        /// <summary>The move keys move a landing point (the camera's way is the keys' way), onto floors only, within
        /// <paramref name="far"/> of where the ultimate began; the last tenth of a second it holds.</summary>
        void UltSteer(PawnRushSkillParams s, float dt, float far)
        {
            if (ultFlightTime - ultFlightT < 0.12f) return;
            Vector3 keys = Flat(skillMoveRaw);
            if (keys.sqrMagnitude < 0.01f) return;
            if (keys.sqrMagnitude > 1f) keys.Normalize();
            Vector3 want = planPoint + keys * (s.famSteer * dt);
            Vector3 off = Flat(want - ultStart);
            if (off.magnitude > far) want = new Vector3(ultStart.x, want.y, ultStart.z) + off.normalized * far;
            var floor = UltFloor(want);
            if (!floor.HasValue || Mathf.Abs(floor.Value.y - planPoint.y) > 3f) return;
            planPoint = floor.Value;
            ultTo = planPoint + Vector3.up * standHeight;
        }

        // ---------------------------------------------------------------- 패밀리 포크 (Nf7+!!)

        void UpdateFamilyFork(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill(true)) break;
                    BeginKnightUlt(s, PawnRushSkillIds.FamilyFork, "패밀리 포크 — 이동 키로 착지 칸, 내려올 때 Q", s.famAir);
                    planPoint = FamFirstAim(s);
                    UltLeap(planPoint + Vector3.up * standHeight, s.famAir, s.famHigh, null);
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    UltSteer(s, dt, s.famFar);
                    if (press && !ultPressed)
                    {
                        // One press: in the last moment of the fall it is on the beat; earlier it is spent.
                        ultPressed = true;
                        ultPerfect = ultFlightTime - ultFlightT <= s.famBeat;
                        Fx(SkillFxKind.UltTiming, null, planPoint, planDir, ultPerfect ? 1 : 0);
                    }
                    if (!UltFly(dt)) break;
                    FamLand(s);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.5f) EndKnightUlt(s);
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>Where the fork first aims: of the squares a knight's move from an enemy (within famAim, inside 60° of
        /// the camera's way, on a floor), the one that forks the most enemies now, then the one nearest the camera's way;
        /// nobody there: 14 m straight ahead. The move keys take it from there (they move against the enemies' running).</summary>
        Vector3 FamFirstAim(PawnRushSkillParams s)
        {
            Vector3 me = FeetPoint, way = AimFlat(), best = default;
            float bestScore = float.NegativeInfinity;
            bool found = false;
            foreach (var e in All)
            {
                if (!UltFoe(e)) continue;
                Vector3 at = e.FeetPoint;
                if (Mathf.Abs(at.y - me.y) > 6f) continue;
                foreach (var m in KnightMoves)
                {
                    Vector3 c = at - new Vector3(m.x * s.famSquare, 0f, m.y * s.famSquare), d = Flat(c - me);
                    float far = d.magnitude;
                    if (far > s.famAim || far < 3f) continue;
                    float look = Vector3.Dot(d / far, way);
                    if (look < 0.5f) continue;
                    int forks = 0;
                    foreach (var o in All)
                        if (UltFoe(o) && Mathf.Abs(o.FeetPoint.y - at.y) <= 4f && OnKnightSquare(Flat(o.FeetPoint - c), s.famSquare, s.famCatch)) forks++;
                    float score = forks * 3f + look * 1.5f - far / s.famAim;
                    if (score <= bestScore) continue;
                    var floor = UltFloor(c);
                    if (!floor.HasValue || Mathf.Abs(floor.Value.y - me.y) > 6f) continue;
                    best = floor.Value;
                    bestScore = score;
                    found = true;
                }
            }
            return found ? best : LandAhead(me, way, 14f);
        }

        /// <summary>Whether a point (flat, from the landing) is on one of the knight's squares.</summary>
        public static bool OnKnightSquare(Vector3 flatFromLanding, float square, float reach)
        {
            foreach (var m in KnightMoves)
                if (new Vector2(flatFromLanding.x - m.x * square, flatFromLanding.z - m.y * square).magnitude <= reach) return true;
            return false;
        }

        void FamLand(PawnRushSkillParams s)
        {
            Vector3 at = FeetPoint;
            planPoint = at;
            float reach = s.famCatch + (ultPerfect ? s.famCatchBeat : 0f);
            float back = s.famBack + (ultPerfect ? 5f : 0f);
            int forked = 0, landed = 0, swayed = 0;
            ultTaken.Clear();
            foreach (var e in All)
            {
                if (!UltFoe(e)) continue;
                Vector3 d = Flat(e.FeetPoint - at);
                if (Mathf.Abs(e.FeetPoint.y - at.y) > 4f) continue;
                if (OnKnightSquare(d, s.famSquare, reach))
                {
                    forked++;
                    ultTaken.Add(e);
                    Vector3 to = e.BackAlong(back, at);
                    Fx(SkillFxKind.UltStrike, e, e.bodies[(int)BodyId.Head].position, FlatDir(to - e.FeetPoint), forked);
                    e.UltThrow(to, s.famThrowTime, s.famThrowTop, s.famKnock, this);
                    e.LastSkillHit = $"패밀리 포크: 포크! {Flat(to - e.FeetPoint).magnitude:0} m 뒤로";
                }
                else if (d.magnitude <= s.famStomp)
                {
                    landed++;
                    e.Knockdown("패밀리 포크", s.famStompKnock);
                    e.GaugeKnocked(this);
                    e.LastSkillHit = "패밀리 포크: 깔림";
                }
                else if (d.magnitude <= s.famSway)
                {
                    swayed++;
                    e.Stagger(0.6f);
                }
            }
            int grade = forked >= 3 || (forked >= 2 && ultPerfect) ? GradeBrilliant
                      : forked == 2 || (forked == 1 && ultPerfect) ? GradeGreat
                      : forked == 1 ? GradeBest
                      : landed + swayed > 0 ? GradeInaccuracy : GradeBlunder;
            Fx(SkillFxKind.UltHit, null, at, planDir, forked, grade);
            FeelHit(null, at, planDir, FeelUltimate, Mathf.Max(1, forked + landed), false);
            Log($"패밀리 포크: 포크 {forked}명, 깔림 {landed}, 흔들림 {swayed}" + (ultPerfect ? " · 박자 맞음" : "") + $" → {GradeName(grade)}");
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

        public static string GradeName(int grade) => grade switch
        {
            GradeBrilliant => "탁월한 수 !!",
            GradeGreat => "좋은 수 !",
            GradeBest => "최선의 수 ★",
            GradeInaccuracy => "부정확한 수 ?!",
            _ => "블런더 ??",
        };

        // ---------------------------------------------------------------- 나이트 투어 (Nf5–d6–e8!!)

        void UpdateTourUlt(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill(true)) break;
                    var first = TourAim(s, FeetPoint, s.ultTourStart);
                    BeginKnightUlt(s, PawnRushSkillIds.KnightTour, "나이트 투어 — 카메라로 다음 머리, Q로 건너뛰기", s.ultTourHop);
                    if (first == null) TourFinalLeap(s);
                    else TourHopTo(s, first);
                    break;
                }
                case PlanPhase.Active:
                    planTime += dt;
                    if (ultFlying)
                    {
                        if (ultFinal) UltSteer(s, dt, s.ultTourFinalMax + 10f);
                        if (!UltFly(dt)) break;
                        if (ultFinal)
                        {
                            TourLanded(s);
                            planPhase = PlanPhase.Recovery;
                            planTime = 0f;
                            break;
                        }
                        if (ultHead == null || ultHead.State != PawnState.Active || ultHead.Floating)
                        {
                            TourFinalLeap(s);
                            break;
                        }
                        TourStompHead(s, ultHead);
                        ultStand = 0f;
                    }
                    // On a head: ride it while the next is picked.
                    ultStand += dt;
                    TourRide(dt);
                    planTarget = ultTaken.Count < TourHops(s) ? TourAim(s, bodies[0].position, s.ultTourReach) : null;
                    if (press)
                    {
                        if (planTarget == null)
                        {
                            TourFinalLeap(s);
                            break;
                        }
                        bool beat = ultStand >= s.ultTourBeat0 && ultStand <= s.ultTourBeat1;
                        Fx(SkillFxKind.UltTiming, planTarget, planTarget.FeetPoint, planDir, beat ? 1 : 0);
                        TourHopTo(s, planTarget);
                        ultPerfect = beat;
                        if (beat) ultPerfects++;
                        break;
                    }
                    if (ultStand >= s.ultTourWait || planTarget == null && ultStand >= 0.25f) TourFinalLeap(s);
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.5f) EndKnightUlt(s);
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        int TourHops(PawnRushSkillParams s) => Mathf.Max(1, Mathf.RoundToInt(ByComeback(ultF, s.ultTourHops1, s.ultTourHops2)));

        /// <summary>The head the camera points at within <paramref name="reach"/>.</summary>
        RagdollPawn TourAim(PawnRushSkillParams s, Vector3 from, float reach) => TourPick(this, from, AimFlat(), reach, ultTaken);

        /// <summary>The head a knight's tour takes from <paramref name="from"/>: a standing enemy within
        /// <paramref name="reach"/>, the one most along <paramref name="way"/> (the camera's), then the nearest. The host's
        /// pick, and the red ring the knight's own screen shows before the key (PawnRushSkillFx.Ult.cs): one rule.</summary>
        public static RagdollPawn TourPick(RagdollPawn by, Vector3 from, Vector3 way, float reach, ICollection<RagdollPawn> skip)
        {
            if (by == null) return null;
            way = Flat(way);
            if (way.sqrMagnitude < 1e-4f) return null;
            way.Normalize();
            RagdollPawn best = null;
            float bestScore = float.NegativeInfinity;
            foreach (var e in All)
            {
                if (!by.UltFoe(e) || skip != null && skip.Contains(e)) continue;
                Vector3 d = Flat(e.FeetPoint - from);
                float far = d.magnitude;
                if (far > reach || far < 0.5f || Mathf.Abs(e.FeetPoint.y - from.y) > 5f) continue;
                float score = Vector3.Dot(d / far, way) * 2f - far / reach;
                if (score <= bestScore) continue;
                best = e;
                bestScore = score;
            }
            return best;
        }

        void TourHopTo(PawnRushSkillParams s, RagdollPawn head)
        {
            planTarget = head;
            planDir = FlatDir(head.bodies[0].position - bodies[0].position);
            Vector3 top = head.bodies[(int)BodyId.Head].position + Vector3.up * 0.35f;
            UltLeap(top, s.ultTourHop, 2.2f + 0.08f * Flat(top - bodies[0].position).magnitude, head);
            Fx(SkillFxKind.UltGo, head, head.FeetPoint, planDir, PawnRushSkillIds.KnightTour, 0f);
        }

        /// <summary>Standing on the squashed head: carried with it.</summary>
        void TourRide(float dt)
        {
            var head = ultHead;
            if (head == null) return;
            Vector3 top = head.bodies[(int)BodyId.Head].position + Vector3.up * 0.4f, at = bodies[0].position;
            Fly(top, (top - at) / Mathf.Max(1e-4f, dt) * 0.5f + Flat(head.bodies[0].linearVelocity), 0f, default, 18f);
            grabImmune = Mathf.Max(grabImmune, 0.25f);
        }

        void TourStompHead(PawnRushSkillParams s, RagdollPawn e)
        {
            ultTaken.Add(e);
            if (e.State != PawnState.Active) return;
            float squash = s.ultTourSquash + (ultPerfect ? s.ultTourBeatSquash : 0f);
            if (e.Squash(squash, 0f)) e.GaugeKnocked(this);
            e.slideAt = Time.time + squash;
            e.slideDir = -RunWay(e);
            e.slideMetres = s.ultTourSlide;
            e.LastSkillHit = $"나이트 투어: 납작 {squash:0.#}초, 뒤로 {s.ultTourSlide:0} m";
            Vector3 head = e.bodies[(int)BodyId.Head].position;
            Fx(SkillFxKind.UltStrike, e, head, planDir, ultTaken.Count, ultPerfect ? 1f : 0f);
            FeelHit(e, head, planDir, FeelHard, ultTaken.Count, true);
            Log($"나이트 투어 → {e.DisplayName}: {ultTaken.Count}번째 머리" + (ultPerfect ? " (박자)" : ""));
        }

        /// <summary>The last leap: aimed 10 m ahead of the enemy leader (30 m at most), or straight ahead; steered like the fork.</summary>
        void TourFinalLeap(PawnRushSkillParams s)
        {
            ultFinal = true;
            if (ultFlying)
            {
                ultFlying = false;
                EndFlight();
            }
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
                Vector3 want = leader.FeetPoint + RunWay(leader) * s.ultTourFinal, d = Flat(want - me);
                var floor = d.magnitude <= s.ultTourFinalMax ? UltFloor(want) : null;
                to = floor ?? LandAhead(me, FlatDir(d), Mathf.Min(d.magnitude, s.ultTourFinalMax));
            }
            else to = LandAhead(me, AimFlat(), s.ultTourEmpty);
            ultStart = me;
            planPoint = to;
            planTarget = null;
            planDir = FlatDir(to - me);
            float across = Flat(to - me).magnitude;
            UltLeap(to + Vector3.up * standHeight, Mathf.Clamp(across / 18f, 0.8f, 1.5f), 4f + across * 0.12f, null);
            Fx(SkillFxKind.UltGo, leader, to, planDir, PawnRushSkillIds.KnightTour, 1f);
        }

        void TourLanded(PawnRushSkillParams s)
        {
            Vector3 at = FeetPoint;
            planPoint = at;
            // (The first head has no beat before it: h heads carry h - 1 beats at most.)
            int h = ultTaken.Count, p = ultPerfects;
            int grade = h >= 4 && p >= h - 1 ? GradeBrilliant
                      : h >= 3 || (h >= 2 && p >= 1) ? GradeGreat
                      : h >= 2 ? GradeBest
                      : h == 1 ? GradeInaccuracy : GradeBlunder;
            Fx(SkillFxKind.UltHit, null, at, planDir, h, grade);
            FeelHit(null, at, planDir, FeelUltimate, Mathf.Max(1, h), false);
            Log($"나이트 투어: {h}명 (박자 {p}) → {GradeName(grade)}");
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
            if (State != PawnState.Active || !OnFloor || Floating || castleCarry || OuterFlying) return;
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
