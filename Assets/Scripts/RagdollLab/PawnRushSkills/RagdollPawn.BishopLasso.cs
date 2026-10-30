using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>Where the bishop's rope is (10-26): out of its hands, flying; on a piece's waist, held; coming back.</summary>
    public enum LassoPhase : byte { None = 0, Fly = 1, Hold = 2, Rewind = 3, Snap = 4 }

    /// <summary>
    /// The Pawn Rush bishop's X rope (10-26, 승규 님: "소드파이트에서 비숍이 구원의 손 할 때 밧줄 그걸로 비숍 손에서 x 자로 상대
    /// 배에다가 걸고 실제로 내가 wasd나 마우스 휠로 밧줄 탄력을 늘렸다 줄였다 할 수 있게"; it was the guarded X trap, kept behind
    /// <see cref="PawnRushSkillParams.bishopRope"/>). F aims (an enemy near the aim is aimed at, as the rook's chain does), the
    /// left click throws: a rope from each hand, crossing on their way (the X), flies out and wraps round the enemy's waist.
    /// Second take (10-26, 승규 님: "wasd로 움직이면서 컨트롤 할 수 있는 물리엔진을 다시 해줘. 마우스 휠을 올리면 올릴 수록 느슨해지고
    /// 내리면 내릴 수록 줄이 팽팽해져"; braced on the spot with W/S hauling, the wheel zoomed the camera instead): the bishop walks
    /// and runs with WASD as ever (no sprint) and the rope goes with it - an elastic tether between its hands and the piece's
    /// waist. The wheel sets the rope's length: up lets it out (looser), down takes it in (tighter), bishopRopeWheel a notch
    /// within bishopRopeMin..Max, the length following at bishopRopeReel m/s. Stretched past its length the rope drags the
    /// piece toward the bishop, harder the further it is stretched - walking away tows it along, turning swings it round -
    /// and the piece can lean away and run against it (a tug of war; the bishop is dragged its way a little, bishopRopeBrace);
    /// too far and it snaps. F or the right click lets go, and a taut rope then throws the piece toward the bishop. It lets go
    /// by itself (slack, nothing thrown) after bishopRopeHold seconds, and drops when the bishop is knocked down or staggered.
    /// The host runs it; the others draw it from the bishop's rope phase, length, tip and piece in the skill packet.
    /// </summary>
    public partial class RagdollPawn
    {
        LassoPhase lassoPhase;
        RagdollPawn lassoOn, lassoAim, lassoHoming;
        /// <summary>The bishop whose rope is round this piece's waist (the host's).</summary>
        RagdollPawn lassoBy;
        Vector3 lassoTip, lassoFrom, lassoDir = Vector3.forward, lassoRewindFrom, lassoDrag;
        float lassoOut, lassoT, lassoRest, lassoRestWant, lassoStretch, lassoWheel;
        // The body on the rope (10-26): how much it leans back hauling against a piece pulling away, how much it takes the
        // rope in hand over hand as a piece comes toward it, and the clock of the hands.
        float lassoPull, lassoReel, lassoHands;

        /// <summary>How far (degrees) the whole body leans back hauling on the rope (the chest leans back further on top).</summary>
        public float LassoLeanBack => piece == PieceKind.Bishop && lassoPhase == LassoPhase.Hold ? 20f * lassoPull * (1f - lassoReel) : 0f;

        /// <summary>Holding its rope it keeps facing the piece on it, walking any way (the run does not turn it).</summary>
        public bool LassoFacesTarget => piece == PieceKind.Bishop && lassoPhase == LassoPhase.Hold && lassoOn != null;

        public LassoPhase BishopLassoPhase => lassoPhase;
        /// <summary>The piece its rope is round (held), or none.</summary>
        public RagdollPawn BishopLassoOn => lassoOn;
        /// <summary>The enemy it would throw at while it aims (null: none near the aim).</summary>
        public RagdollPawn BishopLassoAim => lassoAim;
        public Vector3 BishopLassoTip => lassoTip;
        public Vector3 BishopLassoDir => lassoDir;
        public float BishopLassoTime => lassoT;
        /// <summary>The rope's own length now (m): pulled in it is shorter, let out longer.</summary>
        public float BishopLassoRest => lassoRest;
        /// <summary>How far past its length the rope is stretched now (m, 0 or less: slack).</summary>
        public float BishopLassoStretch => lassoOn != null ? Vector3.Distance(LassoHands, lassoOn.LassoBelly) - lassoRest : lassoStretch;
        /// <summary>A bishop's rope is round this piece's waist (the host's).</summary>
        public RagdollPawn LassoedBy => lassoBy;
        /// <summary>The mouse wheel works the rope now, not the camera: from the aim until the rope is back (the camera
        /// zoomed whenever the rope was not held, and the view ended up inside the bishop's back).</summary>
        public bool WheelOnSkill => piece == PieceKind.Bishop && PawnRushSkills != null && PawnRushSkills.bishopRope && skillStage != SkillStage.None;

        /// <summary>Between the bishop's hands, where the two ropes leave from.</summary>
        public Vector3 LassoHands => (bodies[(int)BodyId.HandL].position + bodies[(int)BodyId.HandR].position) * 0.5f;
        /// <summary>The middle of a piece's waist, where the rope goes round.</summary>
        public Vector3 LassoBelly => bodies[0].position + Vector3.up * 0.02f;

        /// <summary>The mouse wheel's notches since the last physics step (up: looser), the local player's only.</summary>
        public void AddSkillWheel(float notches) => lassoWheel += notches;

        void UpdateBishopLasso(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            if (lassoPhase != LassoPhase.Hold) lassoWheel = 0f;
            switch (skillStage)
            {
                case SkillStage.None:
                    if (press && CanStartSkill())
                    {
                        skillStage = SkillStage.Windup;
                        stageTime = 0f;
                        skillGrabLatch = skillGrabRaw;
                        lassoDir = LassoAim(s);
                        SkillDetail = "조준 (마우스로 방향 · 좌클릭 밧줄 던지기 · 우클릭/F 취소)";
                    }
                    break;
                case SkillStage.Windup:
                    stageTime += dt;
                    lassoDir = LassoAim(s);
                    if (Flat(skillMoveRaw).sqrMagnitude < 0.04f) facing = FlatDir(lassoDir);
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        ClearSkills();
                        Log("비숍: 조준 취소");
                        break;
                    }
                    if (confirm)
                    {
                        SkillUses++;
                        ThrowLasso(s);
                    }
                    break;
                case SkillStage.Active:
                    stageTime += dt;
                    if (Staggered || bindLeft > 0f)
                    {
                        // Hit while it throws or holds: the rope drops from its hands.
                        LetGoLasso(s, false, Staggered ? "비숍이 휘청" : "비숍이 묶임");
                        break;
                    }
                    StepLasso(s, press, cancel, dt);
                    break;
                default:
                    ClearSkills();
                    break;
            }
        }

        /// <summary>The way the rope would go: an enemy within bishopRopeAssist of the aim, in reach and in sight, is aimed at
        /// (its waist); otherwise level along the aim.</summary>
        Vector3 LassoAim(PawnRushSkillParams s)
        {
            Vector3 flat = AimFlat();
            Vector3 from = LassoHands;
            lassoAim = null;
            float best = s.bishopRopeAssist;
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable
                    || other.chainCarry || other.kingCarry || other.lassoBy != null) continue;
                Vector3 to = other.LassoBelly - from;
                float across = Flat(to).magnitude;
                if (across < 0.6f || across > s.bishopRopeRange) continue;
                if (Mathf.Abs(to.y) > 2f) continue;
                float angle = Vector3.Angle(flat, Flat(to));
                if (angle > best || !ClearSight(from, other.LassoBelly)) continue;
                lassoAim = other;
                best = angle;
            }
            if (lassoAim != null) return (lassoAim.LassoBelly - from).normalized;
            return flat;
        }

        void ThrowLasso(PawnRushSkillParams s)
        {
            skillStage = SkillStage.Active;
            stageTime = 0f;
            lassoPhase = LassoPhase.Fly;
            lassoT = lassoOut = 0f;
            lassoFrom = LassoHands;
            lassoTip = lassoFrom;
            lassoOn = null;
            lassoHoming = lassoAim;
            facing = FlatDir(lassoDir);
            SkillDetail = "밧줄이 날아감";
            Fx(SkillFxKind.BishopRopeThrow, lassoHoming, lassoFrom, lassoDir, 0, s.bishopRopeRange);
            Log($"비숍: 밧줄 던짐{(lassoHoming != null ? $" → {lassoHoming.DisplayName}" : "")}");
        }

        void StepLasso(PawnRushSkillParams s, bool press, bool cancel, float dt)
        {
            lassoT += dt;
            switch (lassoPhase)
            {
                case LassoPhase.Fly:
                {
                    // Thrown at a piece, it curves after it a little (a thrown rope is aimed, not a bullet).
                    var homing = lassoHoming;
                    if (homing != null && homing.State == PawnState.Active && !homing.SkillUntargetable)
                    {
                        Vector3 to = homing.LassoBelly - lassoTip;
                        if (to.sqrMagnitude > 1e-4f) lassoDir = Vector3.Slerp(lassoDir, to.normalized, Mathf.Clamp01(12f * dt)).normalized;
                    }
                    float step = s.bishopRopeSpeed * dt;
                    float left = s.bishopRopeRange - lassoOut;
                    bool last = step >= left;
                    if (last) step = Mathf.Max(0f, left);
                    if (LassoSweep(lassoTip, lassoDir, step, out var at, out var other, out bool solid))
                    {
                        lassoTip = at;
                        if (other != null && IsEnemyOf(other) && other.lassoBy == null) HookLasso(s, other);
                        else LassoRewind(other != null ? "걸 수 없는 기물에 닿음" : "벽·지형에 닿음");
                        return;
                    }
                    lassoTip += lassoDir * step;
                    lassoOut += step;
                    if (last) LassoRewind("끝까지 감");
                    break;
                }
                case LassoPhase.Hold:
                    HoldLasso(s, press, cancel, dt);
                    break;
                case LassoPhase.Rewind:
                case LassoPhase.Snap:
                {
                    float t = Mathf.Clamp01(lassoT / 0.28f);
                    lassoTip = Vector3.Lerp(lassoRewindFrom, LassoHands, t * t);
                    if (t >= 1f) FinishLasso(s);
                    break;
                }
            }
        }

        /// <summary>One step of the rope's flight: the first piece or solid thing in its way.</summary>
        bool LassoSweep(Vector3 from, Vector3 dir, float distance, out Vector3 at, out RagdollPawn piece, out bool solid)
        {
            at = from;
            piece = null;
            solid = false;
            if (distance <= 1e-4f) return false;
            int n = Physics.SphereCastNonAlloc(from, 0.16f, dir, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider == null || ownSet.Contains(h.collider) || h.distance >= best) continue;
                if (ColliderOwner.TryGetValue(h.collider, out var other))
                {
                    if (other == null || other == this) continue;
                    best = h.distance;
                    found = true;
                    bool takes = !other.SkillUntargetable && !other.NetworkPuppet && other.State == PawnState.Active && !other.chainCarry && !other.kingCarry;
                    piece = takes ? other : null;
                    solid = false;
                    at = h.distance > 0f ? h.point : other.LassoBelly;
                    continue;
                }
                if (PassesThrough(h.collider)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                // The floor it leaves from, grazed as it is thrown down off a ledge: not a wall.
                if (lassoOut + h.distance < 0.8f && (h.normal.y > 0.6f || h.point.y < FeetPoint.y + 0.1f)) continue;
                best = h.distance;
                found = true;
                piece = null;
                solid = true;
                at = h.distance > 0f ? h.point : from;
            }
            return found;
        }

        void HookLasso(PawnRushSkillParams s, RagdollPawn target)
        {
            lassoPhase = LassoPhase.Hold;
            lassoT = 0f;
            lassoOn = target;
            lassoHoming = null;
            target.lassoBy = this;
            target.LetGo(0.3f);
            // It starts as long as the way to the piece is: no jolt as it closes, the bishop takes it in from there.
            // It closes a little shorter than the way to the piece (bishopRopeTighten), within its range: taut from the start,
            // taken in from further away no faster than the piece gives (bishopRopeHaul).
            float dist = Vector3.Distance(LassoHands, target.LassoBelly);
            lassoRestWant = Mathf.Clamp(dist - s.bishopRopeTighten, s.bishopRopeMin, s.bishopRopeMax);
            lassoRest = Mathf.Max(lassoRestWant, dist - s.bishopRopeHaul);
            lassoStretch = 0f;
            lassoWheel = 0f;
            lassoTip = target.LassoBelly;
            SkillDetail = "밧줄 걸림 — WASD로 끌고 다니기 · 휠↑ 느슨하게 · 휠↓ 팽팽하게 · F/우클릭 놓기";
            Fx(SkillFxKind.BishopRopeHook, target, target.LassoBelly, Flat(target.bodies[0].position - bodies[0].position), 0, lassoRest);
            Log($"비숍 밧줄 → {target.DisplayName}: 허리에 걸림 (길이 {lassoRest:0.0} m, {s.bishopRopeHold:0.#}초)");
        }

        /// <summary>The bishop holds the rope (the host's step): its length from the wheel, then whether it snaps, is let go or
        /// runs out of time. The bishop walks meanwhile; the rope's pull is in <see cref="UpdateLassoDrag"/>.</summary>
        void HoldLasso(PawnRushSkillParams s, bool press, bool cancel, float dt)
        {
            var on = lassoOn;
            if (on == null || on.lassoBy != this || on.SkillUntargetable || on.Floating || on.chainCarry || on.kingCarry)
            {
                LetGoLasso(s, false, "상대를 놓침");
                return;
            }
            Vector3 hands = LassoHands, belly = on.LassoBelly;
            // The wheel: up lets it out (looser), down takes it in (tighter), a notch at a time, within its range; the length
            // follows at bishopRopeReel m/s (hand over hand, not in a jump).
            lassoRestWant = Mathf.Clamp(lassoRestWant + lassoWheel * s.bishopRopeWheel, s.bishopRopeMin, s.bishopRopeMax);
            lassoWheel = 0f;
            lassoRest = Mathf.MoveTowards(lassoRest, lassoRestWant, s.bishopRopeReel * dt);
            float dist = Vector3.Distance(hands, belly);
            // It cannot be taken in further than the piece gives: no shorter than bishopRopeHaul under the way to it.
            lassoRest = Mathf.Max(lassoRest, dist - s.bishopRopeHaul);
            lassoTip = belly;
            lassoStretch = dist - lassoRest;
            // It faces the piece; its body answers the rope (LassoPose): leaning back against a piece pulling away (the rope
            // taut), hand over hand as one comes toward it (or as the wheel takes the rope in).
            Vector3 toward = Flat(belly - hands);
            if (toward.sqrMagnitude > 1e-4f) facing = toward.normalized;
            Vector3 back = toward.sqrMagnitude > 1e-4f ? -toward.normalized : -facing;
            float coming = Vector3.Dot(Flat(on.bodies[0].linearVelocity - bodies[0].linearVelocity), back);
            float pullWant = Mathf.Clamp01((lassoStretch + 0.05f) / 0.45f) * (coming > 0.8f ? 0.3f : 1f);
            float reelWant = Mathf.Max(Mathf.Clamp01((coming - 0.5f) / 1.2f), lassoRest < lassoRestWant - 0.01f || lassoRestWant < lassoRest - 0.01f ? 0.8f : 0f);
            lassoPull = Mathf.MoveTowards(lassoPull, pullWant, 4f * dt);
            lassoReel = Mathf.MoveTowards(lassoReel, reelWant, 5f * dt);
            lassoHands += dt * Mathf.Lerp(3f, 11f, lassoReel);
            if (lassoStretch > s.bishopRopeBreak)
            {
                SnapLasso(s);
                return;
            }
            if (cancel || press)
            {
                LetGoLasso(s, true, cancel ? "우클릭" : "F");
                return;
            }
            if (lassoT >= s.bishopRopeHold) LetGoLasso(s, false, "시간 끝");
        }

        /// <summary>How fast the rope drags <paramref name="target"/> this step (m/s): toward the bishop's hands as far as it
        /// is stretched past its length.</summary>
        Vector3 LassoPullOn(RagdollPawn target, PawnRushSkillParams s)
        {
            Vector3 to = LassoHands - target.LassoBelly;
            float d = to.magnitude, stretch = d - lassoRest;
            if (stretch <= 0f || d <= 1e-3f) return Vector3.zero;
            return to / d * Mathf.Min(s.bishopRopeStiffness * stretch, s.bishopRopePull);
        }

        /// <summary>Every piece's step (the host's): a bishop's rope round its waist drags it; a bishop holding a taut rope is
        /// dragged a little toward what it holds. Added to the run like a moving floor, taken off again before the next run.</summary>
        void UpdateLassoDrag(PawnRushSkillParams s, float dt)
        {
            Vector3 want = Vector3.zero;
            var by = lassoBy;
            if (by != null && (by.lassoOn != this || by.lassoPhase != LassoPhase.Hold))
            {
                lassoBy = null;
                by = null;
            }
            if (by != null) want = by.LassoPullOn(this, by.PawnRushSkills ?? s);
            else if (lassoPhase == LassoPhase.Hold && lassoOn != null)
            {
                Vector3 to = lassoOn.LassoBelly - LassoHands;
                float d = to.magnitude, stretch = d - lassoRest;
                if (stretch > 0f && d > 1e-3f) want = to / d * (Mathf.Min(s.bishopRopeStiffness * stretch, s.bishopRopePull) * s.bishopRopeBrace);
            }
            ApplyLassoDrag(want, dt);
        }

        void ApplyLassoDrag(Vector3 want, float dt)
        {
            if (NetworkPuppet) return;
            bool standing = State == PawnState.Active && !Climbing && rope == null && hookPhase != HookPhase.Pulling && !Floating
                            && !chainCarry && !kingCarry && bindLeft <= 0f;
            if (standing)
            {
                Vector3 flat = Flat(want);
                if (flat.sqrMagnitude < 1e-8f && lassoDrag.sqrMagnitude < 1e-8f) return;
                anchorVel += flat;
                anchorPos.x += flat.x * dt;
                anchorPos.z += flat.z * dt;
                anchor.MovePosition(anchorPos);
                AddVelocity(flat - lassoDrag);
                lassoDrag = flat;
                return;
            }
            lassoDrag = Vector3.zero;
            if (want.sqrMagnitude < 1e-6f) return;
            // Down or in the air: the body is pulled along the rope up to the rope's speed.
            Vector3 dir = want.normalized;
            float need = want.magnitude - Vector3.Dot(bodies[0].linearVelocity, dir);
            if (need > 0f) AddVelocity(dir * Mathf.Min(need, 25f * dt));
        }

        /// <summary>Before the run: the rope's drag of the last step is taken off again (the run works on its own speed).</summary>
        void PreLassoDrag()
        {
            if (lassoDrag.sqrMagnitude > 0f) anchorVel -= lassoDrag;
        }

        void LassoRewind(string why)
        {
            lassoPhase = LassoPhase.Rewind;
            lassoT = 0f;
            lassoRewindFrom = lassoTip;
            lassoOn = null;
            lassoHoming = null;
            SkillDetail = "밧줄 거둠";
            Fx(SkillFxKind.BishopRopeMiss, null, lassoTip, lassoDir);
            Log($"비숍 밧줄: {why} → 거둠");
        }

        /// <summary>The bishop lets go (<paramref name="fling"/>: on purpose; a taut rope throws the piece its way).</summary>
        void LetGoLasso(PawnRushSkillParams s, bool fling, string why)
        {
            var on = lassoOn;
            float stretch = on != null ? Vector3.Distance(LassoHands, on.LassoBelly) - lassoRest : 0f;
            int flung = 0;
            Vector3 at = on != null ? on.LassoBelly : lassoTip;
            if (on != null)
            {
                if (fling && stretch > 0.15f && on.State == PawnState.Active && !on.NetworkPuppet)
                {
                    Vector3 back = Flat(LassoHands - on.LassoBelly);
                    float speed = Mathf.Min(stretch * s.bishopRopeFling, s.bishopRopeFlingMax);
                    if (back.sqrMagnitude > 1e-4f)
                    {
                        on.Launch(back.normalized * speed + Vector3.up * Mathf.Min(2.5f, 1f + speed * 0.25f));
                        flung = 1;
                    }
                }
                if (on.lassoBy == this) on.lassoBy = null;
            }
            if (lassoPhase == LassoPhase.Fly || lassoPhase == LassoPhase.Hold)
                Fx(SkillFxKind.BishopRopeRelease, on, at, Flat(LassoHands - at), flung, Mathf.Max(0f, stretch));
            Log($"비숍 밧줄: 놓음 ({why}){(flung == 1 ? $" — 팽팽해서 {on.DisplayName} 튕겨 옴" : "")}");
            lassoRewindFrom = at;
            lassoTip = at;
            lassoPhase = LassoPhase.Rewind;
            lassoT = 0f;
            lassoOn = null;
            lassoHoming = null;
            lassoStretch = 0f;
            SkillDetail = "밧줄 거둠";
        }

        void SnapLasso(PawnRushSkillParams s)
        {
            var on = lassoOn;
            Vector3 at = on != null ? on.LassoBelly : lassoTip;
            if (on != null && on.lassoBy == this) on.lassoBy = null;
            Fx(SkillFxKind.BishopRopeSnap, on, Vector3.Lerp(LassoHands, at, 0.5f), Flat(at - LassoHands), 0, lassoStretch);
            Log($"비숍 밧줄: 너무 늘어나 끊어짐 ({lassoStretch:0.0} m)");
            lassoRewindFrom = at;
            lassoPhase = LassoPhase.Snap;
            lassoT = 0f;
            lassoOn = null;
            lassoHoming = null;
            SkillDetail = "밧줄 끊어짐";
        }

        /// <summary>
        /// The bishop's body on its rope (10-26, 승규 님: "상대가 비숍 플레이어한테 멀어지려고 하면 나는 뒤로 몸을 져치면서 잡아당기는
        /// 몸 연출이랑 나랑 가까워지려고 하면 그에 맞는 동작"): aiming, the coiled rope held out in both hands; throwing, both
        /// arms flung forward; holding, the rope in both hands out ahead - leaning back with the arms straight along the rope
        /// and the legs braced, straining, as the piece pulls away; taking it in hand over hand, upright, as the piece comes
        /// toward it. False when its rope is not out (the other poses go on).
        /// </summary>
        bool LassoPose(ref Quaternion armL, ref Quaternion armR, ref Quaternion chest, ref Quaternion head,
                       ref Quaternion thighL, ref Quaternion thighR, ref Quaternion footL, ref Quaternion footR)
        {
            if (piece != PieceKind.Bishop || PawnRushSkills == null || !PawnRushSkills.bishopRope) return false;
            if (skillStage == SkillStage.Windup)
            {
                // The coils held out in front, ready.
                armL = Quaternion.Euler(0f, 62f, 34f);
                armR = Quaternion.Euler(0f, -62f, -34f);
                chest = Quaternion.Euler(6f, 0f, 0f);
                head = Quaternion.Euler(-4f, 0f, 0f);
                return true;
            }
            if (lassoPhase == LassoPhase.Fly)
            {
                // Thrown from overhead: both arms up over the head, then flung forward and down after the rope.
                float k = Mathf.Clamp01(lassoT / 0.16f);
                k = k * k * (3f - 2f * k);
                armL = Quaternion.Slerp(Quaternion.Euler(0f, 30f, -72f), Quaternion.Euler(0f, 88f, -6f), k);
                armR = Quaternion.Slerp(Quaternion.Euler(0f, -30f, 72f), Quaternion.Euler(0f, -88f, 6f), k);
                chest = Quaternion.Slerp(Quaternion.Euler(-12f, 0f, 0f), Quaternion.Euler(16f, 0f, 0f), k);
                head = Quaternion.Slerp(Quaternion.Euler(-10f, 0f, 0f), Quaternion.Euler(4f, 0f, 0f), k);
                return true;
            }
            if (lassoPhase != LassoPhase.Hold) return false;
            float pull = lassoPull, reel = lassoReel, c = lassoHands;
            float strain = Mathf.Sin(Time.time * 23f) * 2.5f * pull;
            // Held: the rope in both hands out ahead at the waist.
            Quaternion holdL = Quaternion.Euler(0f, 70f, 28f), holdR = Quaternion.Euler(0f, -70f, -28f);
            // Hauling against it: arms straight out along the rope, straining.
            Quaternion pullL = Quaternion.Euler(strain, 86f, 6f), pullR = Quaternion.Euler(-strain, -86f, -6f);
            // Hand over hand: one hand reaches out along the rope while the other draws in to the chest.
            float a = Mathf.Sin(c), b = -a;
            Quaternion reelL = Quaternion.Euler(0f, 66f + 20f * a, 26f - 22f * a), reelR = Quaternion.Euler(0f, -66f - 20f * b, -26f + 22f * b);
            armL = Quaternion.Slerp(Quaternion.Slerp(holdL, pullL, pull), reelL, reel);
            armR = Quaternion.Slerp(Quaternion.Slerp(holdR, pullR, pull), reelR, reel);
            chest = Quaternion.Slerp(Quaternion.Slerp(Quaternion.Euler(4f, 0f, 0f), Quaternion.Euler(-28f, 0f, strain), pull), Quaternion.Euler(8f, 7f * a, 0f), reel);
            head = Quaternion.Slerp(Quaternion.Slerp(Quaternion.Euler(-2f, 0f, 0f), Quaternion.Euler(-12f, 0f, 0f), pull), Quaternion.Euler(4f, 0f, 0f), reel);
            // Standing to haul, the legs brace ahead (walking, the steps go on).
            float brace = pull * (1f - reel) * Mathf.Clamp01(1f - GroundSpeed / 1.5f);
            if (brace > 0.001f)
            {
                thighL = Quaternion.Slerp(thighL, Quaternion.Euler(-24f, 0f, -6f), brace);
                thighR = Quaternion.Slerp(thighR, Quaternion.Euler(-12f, 0f, 6f), brace);
                footL = Quaternion.Slerp(footL, Quaternion.Euler(22f, 0f, 0f), brace);
                footR = Quaternion.Slerp(footR, Quaternion.Euler(14f, 0f, 0f), brace);
            }
            return true;
        }

        void FinishLasso(PawnRushSkillParams s)
        {
            lassoPhase = LassoPhase.None;
            lassoOn = null;
            lassoT = 0f;
            lassoStretch = 0f;
            skillStage = SkillStage.None;
            stageTime = 0f;
            SkillDetail = "";
            StartCooldown(s.bishopCooldown);
        }

        /// <summary>The skills are cleared (a knockdown, a piece switch): a rope out drops, and the cooldown runs.</summary>
        void ClearLasso()
        {
            lassoPull = lassoReel = 0f;
            lassoAim = null;
            lassoWheel = 0f;
            if (lassoPhase == LassoPhase.None) return;
            var on = lassoOn;
            if (on != null && on.lassoBy == this) on.lassoBy = null;
            if (lassoPhase == LassoPhase.Fly || lassoPhase == LassoPhase.Hold)
                Fx(SkillFxKind.BishopRopeRelease, on, on != null ? on.LassoBelly : lassoTip, lassoDir, 0, 0f);
            lassoPhase = LassoPhase.None;
            lassoOn = null;
            lassoHoming = null;
            lassoStretch = 0f;
            if (PawnRushSkills != null && !NetworkPuppet) StartCooldown(PawnRushSkills.bishopCooldown);
        }
    }
}
