using System;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>Where a Pawn Rush rook's chain is (R153): thrown out, its handle held out to a teammate, pulling someone in,
    /// or winding back.</summary>
    public enum ChainPhase : byte { None = 0, Fly = 1, Offer = 2, Pull = 3, Rewind = 4 }

    /// <summary>
    /// The Pawn Rush rook's "직선 사슬 견인" (R153, Pawn Rush piece skill plan v0.3 §5, 승규 님 10-16), in place of the straight
    /// charge. F aims a 6 m straight line from the chest (walking allowed, no sprint, ±45° up or down); the left click swings
    /// the arm for 0.1 s and the chain flies at 24 m/s. The first piece it meets on the way: an enemy is hooked at once and
    /// pulled in 0.35 s to 1.5 m in front of the rook along a low path 0.3 m over the floor (a queen, weight 2, takes 1.3
    /// times as long to the same spot), then staggers 0.3 s; a teammate is offered the chain's handle for 2 s and comes only
    /// if it grabs it with its right click, then flies in 0.5 s on a low arc (0.6 m at most) to 1 m beside the rook and
    /// lands standing. A wall, the ground or the end of the range winds the chain back in 0.2 s. The rook stays where it is
    /// from the throw to the end; it being knocked down, staggered or tied breaks the chain and drops whoever was on it
    /// where it is. Something solid in the pulled piece's way drops it there; a cliff does not stop it.
    ///
    /// The pull is a path, not a joint (§5.5): the body is moved along a set path for a set time (the king's call's way of
    /// moving a piece, R146, without its walls or its shield), so it reads the same on the host and every client.
    /// The host runs all of it; a client draws it from the rook's skill state (the phase, the tip, the piece on it).
    /// </summary>
    public partial class RagdollPawn
    {
        /// <summary>Pieces no skill may take as its target now, set by a course: one standing on the finish pad, one in the
        /// other team's own section (plan v0.3 §2.5). Null: none (the skill test bed).</summary>
        public static Func<RagdollPawn, bool> SkillUntouchable;

        /// <summary>Test dummies (<see cref="SkillBot"/>) grab a rook's chain handle by themselves after rookAllyDummyGrab
        /// seconds (false: they never do, to see it wind back).</summary>
        public static bool RookChainBotsGrab = true;

        // Any piece: the guard after a respawn (plan v0.3 §2.5).
        float skillGuardLeft;

        // The rook's side.
        ChainPhase chainPhase;
        float chainT, chainOut;
        Vector3 chainTip, chainFrom;
        RagdollPawn chainOn;

        // The piece on the chain (the host's).
        bool chainCarry, chainCarryAlly, chainGrabEdge, chainGrabPrev;
        RagdollPawn chainBy, chainOfferBy;
        float chainCarryT, chainCarryTime, chainFromFloor;
        Vector3 chainCarryFrom, chainCarryTo;

        /// <summary>Guarded from every skill for <paramref name="seconds"/> (a respawn: plan v0.3 §2.5).</summary>
        public void GuardFromSkills(float seconds) => skillGuardLeft = Mathf.Max(skillGuardLeft, seconds);
        public float SkillGuardLeft => Mathf.Max(0f, skillGuardLeft);

        /// <summary>No skill may take this piece as its target now: on its way in to a king (R146), just respawned, or on a
        /// spot the course rules out (the finish pad, the other team's section).</summary>
        public bool SkillUntargetable => KingCallShielded || skillGuardLeft > 0f || (SkillUntouchable != null && SkillUntouchable(this));

        public ChainPhase RookChainPhase => chainPhase;
        public Vector3 RookChainTip => chainTip;
        /// <summary>The piece the chain has: hooked, offered its handle, or being pulled.</summary>
        public RagdollPawn RookChainOn => chainOn;
        public float RookChainTime => chainT;
        /// <summary>The seconds a teammate still has to grab the handle (0 unless the chain is held out to it).</summary>
        public float RookOfferLeft => chainPhase == ChainPhase.Offer && PawnRushSkills != null ? Mathf.Max(0f, PawnRushSkills.rookAllyWindow - chainT) : 0f;
        /// <summary>Being pulled in on a rook's chain now (the host's), and by whom; a teammate rides it like a water ski.</summary>
        public bool ChainCarried => chainCarry;
        public bool ChainCarryAlly => chainCarryAlly;
        public RagdollPawn ChainCarriedBy => chainBy;

        /// <summary>Where the chain leaves the rook: its right hand.</summary>
        public Vector3 RookChainHand => bodies[(int)BodyId.HandR].position;

        /// <summary>A rook whose chain handle is held out to this piece now (any PC: the rook's state comes from the host).</summary>
        public RagdollPawn RookOfferingMe()
        {
            foreach (var p in All)
                if (p != null && p != this && p.piece == PieceKind.Rook && p.chainPhase == ChainPhase.Offer && p.chainOn == this) return p;
            return null;
        }

        // ---------------------------------------------------------------- the rook

        void UpdateRookChain(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (skillStage)
            {
                case SkillStage.None:
                    if (press && CanStartSkill(true))
                    {
                        skillStage = SkillStage.Windup;
                        stageTime = 0f;
                        aimLocked = false;
                        skillGrabLatch = skillGrabRaw;
                        skillDir = ChainAim(s);
                        SkillDetail = "조준 (마우스로 방향 · 좌클릭 던지기 · 우클릭/F 취소)";
                    }
                    break;
                case SkillStage.Windup:
                    stageTime += dt;
                    if (!aimLocked)
                    {
                        // Aiming: the line follows the mouse; the rook walks meanwhile (no sprint); standing still, it turns.
                        skillDir = ChainAim(s);
                        if (Flat(skillMoveRaw).sqrMagnitude < 0.04f) facing = FlatDir(skillDir);
                        if (cancel || press)
                        {
                            if (cancel) skillGrabLatch = true;
                            ClearSkills();
                            Log("룩: 조준 취소");
                            break;
                        }
                        if (confirm)
                        {
                            SkillUses++;
                            aimLocked = true;
                            stageTime = 0f;
                            facing = FlatDir(skillDir);
                            SkillDetail = "팔 휘두르기";
                        }
                        break;
                    }
                    facing = FlatDir(skillDir);
                    if (stageTime >= s.rookSwing) ThrowChain(s);
                    break;
                case SkillStage.Active:
                    stageTime += dt;
                    if (Staggered || bindLeft > 0f)
                    {
                        // Hit mid-throw (a knockdown ends every skill on its own): the chain breaks, its piece drops.
                        BreakChain(Staggered ? "룩이 휘청" : "룩이 묶임");
                        StartCooldown(s.rookCooldown);
                        break;
                    }
                    facing = FlatDir(skillDir);
                    StepChain(s, dt);
                    break;
                default:
                    ClearSkills();
                    break;
            }
        }

        /// <summary>The way the chain would go: where the mouse points, up or down within rookChainPitch.</summary>
        Vector3 ChainAim(PawnRushSkillParams s)
        {
            Vector3 flat = AimFlat();
            // R153 (승규 님 10-22: "마우스를 좀 많이 내려야 걸려"): a piece the chain can take within rookAimAssist either side of
            // the aim, in reach and in sight, is aimed at (its chest), up or down by itself; the nearest to the aim wins.
            chainAimTarget = ChainAssistTarget(s, flat);
            if (chainAimTarget != null)
            {
                Vector3 to = chainAimTarget.bodies[(int)BodyId.Chest].position - (bodies[(int)BodyId.Chest].position + flat * 0.2f);
                float pitch = Mathf.Asin(Mathf.Clamp(to.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                pitch = Mathf.Clamp(pitch, -s.rookChainPitch, s.rookChainPitch) * Mathf.Deg2Rad;
                Vector3 across = Flat(to).sqrMagnitude > 1e-4f ? Flat(to).normalized : flat;
                return (across * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch)).normalized;
            }
            if (input.aim.sqrMagnitude < 1e-4f) return flat;
            // No one to take: the camera's own downward look counts as level (the camera looks rookAimNeutralPitch down).
            float up = Mathf.Asin(Mathf.Clamp(input.aim.normalized.y, -1f, 1f)) * Mathf.Rad2Deg + s.rookAimNeutralPitch;
            up = Mathf.Clamp(up, -s.rookChainPitch, s.rookChainPitch) * Mathf.Deg2Rad;
            return (flat * Mathf.Cos(up) + Vector3.up * Mathf.Sin(up)).normalized;
        }

        RagdollPawn chainAimTarget;

        /// <summary>The piece the chain aims itself at while the rook aims (null: none near the aim).</summary>
        public RagdollPawn RookChainAimTarget => chainAimTarget;

        RagdollPawn ChainAssistTarget(PawnRushSkillParams s, Vector3 flat)
        {
            Vector3 from = bodies[(int)BodyId.Chest].position;
            RagdollPawn best = null;
            float bestAngle = s.rookAimAssist;
            foreach (var other in All)
            {
                if (other == null || other == this || other.State != PawnState.Active || other.SkillUntargetable || other.chainCarry || other.kingCarry) continue;
                if (!IsEnemyOf(other) && !IsAllyOf(other)) continue;
                Vector3 to = other.bodies[(int)BodyId.Chest].position - from;
                float across = Flat(to).magnitude;
                if (across < 0.6f || across > s.rookChainRange) continue;
                float pitch = Mathf.Abs(Mathf.Asin(Mathf.Clamp(to.normalized.y, -1f, 1f)) * Mathf.Rad2Deg);
                if (pitch > s.rookChainPitch) continue;
                float angle = Vector3.Angle(flat, Flat(to));
                if (angle > bestAngle) continue;
                if (!ClearSight(from, other.bodies[(int)BodyId.Chest].position)) continue;
                best = other;
                bestAngle = angle;
            }
            return best;
        }

        /// <summary>Where the chain's line starts: the chest, a little forward (the aim line and the flight are measured from here).</summary>
        Vector3 ChainOrigin => bodies[(int)BodyId.Chest].position + FlatDir(skillDir) * 0.2f;

        void ThrowChain(PawnRushSkillParams s)
        {
            skillStage = SkillStage.Active;
            stageTime = 0f;
            chainPhase = ChainPhase.Fly;
            chainT = chainOut = 0f;
            chainFrom = ChainOrigin;
            chainTip = chainFrom;
            chainOn = null;
            SkillDetail = "사슬이 날아감";
            Fx(SkillFxKind.RookThrow, null, chainFrom, skillDir, 0, s.rookChainRange);
        }

        void StepChain(PawnRushSkillParams s, float dt)
        {
            chainT += dt;
            switch (chainPhase)
            {
                case ChainPhase.Fly:
                {
                    float step = s.rookChainSpeed * dt;
                    float left = s.rookChainRange - chainOut;
                    bool last = step >= left;
                    if (last) step = Mathf.Max(0f, left);
                    Vector3 from = chainFrom + skillDir * chainOut;
                    var hit = ChainSweep(s, from, step, out var at, out var other, out bool solid);
                    if (other != null)
                    {
                        chainTip = at;
                        if (IsEnemyOf(other)) StartChainPull(s, other, false);
                        else OfferChain(s, other);
                        return;
                    }
                    if (hit)
                    {
                        chainTip = at;
                        Rewind(solid ? "벽·지형에 닿음" : "걸리지 않는 상대에 닿음");
                        return;
                    }
                    chainOut += step;
                    chainTip = chainFrom + skillDir * chainOut;
                    if (last) Rewind("6 m 끝까지 감");
                    break;
                }
                case ChainPhase.Offer:
                {
                    var ally = chainOn;
                    if (ally == null || ally.State != PawnState.Active || ally.SkillUntargetable || ally.kingCarry || ally.chainCarry)
                    {
                        if (ally != null) ally.chainOfferBy = null;
                        Rewind("팀원이 잡을 수 없음");
                        return;
                    }
                    chainTip = ally.bodies[(int)BodyId.Chest].position + FlatDir(chainFrom - ally.bodies[0].position) * 0.35f;
                    bool grabbed = ally.SkillBot ? RookChainBotsGrab && chainT >= s.rookAllyDummyGrab : ally.chainGrabEdge;
                    ally.chainGrabEdge = false;
                    if (grabbed)
                    {
                        ally.chainOfferBy = null;
                        ally.skillGrabLatch = true;   // the right click held on grabs nothing after the ride
                        StartChainPull(s, ally, true);
                        return;
                    }
                    if (chainT >= s.rookAllyWindow)
                    {
                        ally.chainOfferBy = null;
                        Rewind("팀원이 안 잡음");
                        chainCooldown = s.rookUngrabbedCooldown;
                    }
                    break;
                }
                case ChainPhase.Pull:
                {
                    var on = chainOn;
                    if (on == null || !on.chainCarry || on.chainBy != this)
                    {
                        FinishChain(s);
                        return;
                    }
                    chainTip = on.bodies[(int)BodyId.Chest].position;
                    break;
                }
                case ChainPhase.Rewind:
                {
                    float t = Mathf.Clamp01(chainT / Mathf.Max(0.02f, s.rookRewind));
                    chainTip = Vector3.Lerp(rewindFrom, RookChainHand, t * t);
                    if (t >= 1f) FinishChain(s);
                    break;
                }
            }
        }

        Vector3 rewindFrom;
        float chainCooldown = -1f;

        void Rewind(string why)
        {
            chainPhase = ChainPhase.Rewind;
            chainT = 0f;
            rewindFrom = chainTip;
            chainOn = null;
            SkillDetail = "사슬 되감기";
            Log($"룩 사슬: {why} → 되감기");
        }

        void FinishChain(PawnRushSkillParams s)
        {
            float cooldown = chainCooldown > 0f ? chainCooldown : s.rookCooldown;
            chainCooldown = -1f;
            chainPhase = ChainPhase.None;
            chainOn = null;
            chainT = 0f;
            skillStage = SkillStage.None;
            stageTime = 0f;
            SkillDetail = "";
            StartCooldown(cooldown);
        }

        /// <summary>One step of the chain's flight: the first piece or solid thing in the way. A piece that no skill may take
        /// (just respawned, on the finish pad) or a downed one stops it like a wall; a loose box does not.</summary>
        bool ChainSweep(PawnRushSkillParams s, Vector3 from, float distance, out Vector3 at, out RagdollPawn piece, out bool solid)
        {
            at = from;
            piece = null;
            solid = false;
            if (distance <= 1e-4f) return false;
            int n = Physics.SphereCastNonAlloc(from, s.rookChainRadius, skillDir, hits, distance, ~0, QueryTriggerInteraction.Ignore);
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
                    bool takes = !other.SkillUntargetable && !other.NetworkPuppet && other.State == PawnState.Active && !other.chainCarry
                                 && (IsEnemyOf(other) || IsAllyOf(other));
                    piece = takes ? other : null;
                    solid = false;
                    at = h.distance > 0f ? h.point : other.bodies[(int)BodyId.Chest].position;
                    continue;
                }
                if (PassesThrough(h.collider)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                // The floor the rook stands on, grazed as the chain leaves it aimed down (off a ledge): not a wall.
                if (chainOut + h.distance < 0.8f && (h.normal.y > 0.6f || h.point.y < FeetPoint.y + 0.1f)) continue;
                best = h.distance;
                found = true;
                piece = null;
                solid = true;
                at = h.distance > 0f ? h.point : from;
            }
            return found;
        }

        void OfferChain(PawnRushSkillParams s, RagdollPawn ally)
        {
            chainPhase = ChainPhase.Offer;
            chainT = 0f;
            chainOn = ally;
            ally.chainOfferBy = this;
            ally.chainGrabEdge = false;
            SkillDetail = "팀원에게 손잡이 (우클릭으로 잡기)";
            Fx(SkillFxKind.RookHook, ally, chainTip, skillDir, 1, s.rookAllyWindow);
            Log($"룩 사슬 → {ally.DisplayName}(팀원): 손잡이 {s.rookAllyWindow:0.#}초 — 우클릭으로 잡기");
        }

        void StartChainPull(PawnRushSkillParams s, RagdollPawn target, bool ally)
        {
            chainPhase = ChainPhase.Pull;
            chainT = 0f;
            chainOn = target;
            Vector3 to = ChainSpot(s, target, ally);
            float time = ally ? s.rookAllyPull : s.rookPullTime * (target.Weight >= s.rookHeavyWeight - 0.01f ? s.rookHeavySlow : 1f);
            target.StartChainCarry(this, ally, to, time);
            SkillDetail = ally ? "팀원 끌어올림" : "적 끌어옴";
            Fx(SkillFxKind.RookHook, target, chainTip, skillDir, ally ? 2 : 0, time);
            Log($"룩 사슬 → {target.DisplayName}{(ally ? "(팀원): 잡음" : ": 걸림")} → {time:0.00}초 동안 {Flat(to - target.bodies[0].position).magnitude:0.0} m 끌려옴");
        }

        /// <summary>Where a piece on the chain is set down, on the floor: an enemy 1.5 m in front of the rook along the line
        /// to it, a teammate 1 m beside it (on the side it comes from). Something solid there: the nearest free spot round it.</summary>
        Vector3 ChainSpot(PawnRushSkillParams s, RagdollPawn target, bool ally)
        {
            Vector3 feet = FeetPoint;
            Vector3 toward = Flat(target.bodies[0].position - bodies[0].position);
            Vector3 fwd = toward.sqrMagnitude > 0.01f ? toward.normalized : FlatDir(facing);
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 offset = ally
                ? right * (Vector3.Dot(toward, right) >= 0f ? 1f : -1f) * s.rookAllyBeside + fwd * 0.2f
                : fwd * s.rookPullFront;
            foreach (float turn in KingSpotTurns)
            {
                Vector3 at = feet + Quaternion.AngleAxis(turn, Vector3.up) * offset;
                float? y = KingFloorAt(at, feet.y);
                if (y == null || Mathf.Abs(y.Value - feet.y) > 1.2f) continue;
                at.y = y.Value;
                if (KingSpotFree(at)) return at;
            }
            return feet + offset;
        }

        /// <summary>The chain breaks (the rook hit, its skill cleared): whoever is on it drops where it is.</summary>
        void BreakChain(string why)
        {
            if (chainPhase == ChainPhase.None) return;
            var on = chainOn;
            if (on != null)
            {
                if (on.chainOfferBy == this) on.chainOfferBy = null;
                if (on.chainCarry && on.chainBy == this) on.EndChainCarry(false);
            }
            Fx(SkillFxKind.RookChainBreak, on, chainTip, skillDir);
            Log($"룩 사슬 끊김 ({why})" + (on != null ? $" — {on.DisplayName} 그 자리에 놓임" : ""));
            chainPhase = ChainPhase.None;
            chainOn = null;
            chainT = 0f;
            chainCooldown = -1f;
            skillStage = SkillStage.None;
            SkillDetail = "";
        }

        /// <summary>Everything the chain leaves running when the skills are cleared (a knockdown, a piece switch).</summary>
        void ClearRookChain()
        {
            BreakChain("스킬 끝");
            chainOfferBy = null;
            chainGrabEdge = false;
            if (chainCarry) EndChainCarry(false);
        }

        // ---------------------------------------------------------------- the piece on the chain

        /// <summary>The right click while a rook holds its chain's handle out to this piece is the grab, nothing else.</summary>
        void ChainGrabKey()
        {
            bool offered = chainOfferBy != null && chainOfferBy.chainPhase == ChainPhase.Offer && chainOfferBy.chainOn == this;
            if (!offered) chainOfferBy = null;
            if (offered)
            {
                if (skillGrabRaw && !chainGrabPrev) chainGrabEdge = true;
                input.grab = false;
            }
            chainGrabPrev = skillGrabRaw;
            if (!chainCarry) return;
            input.move = Vector3.zero;
            input.jump = false;
            input.sprint = false;
            input.shove = false;
            input.shoveHeld = false;
            input.grab = false;
        }

        void StartChainCarry(RagdollPawn rook, bool ally, Vector3 to, float seconds)
        {
            ClearSkills();   // its own skill stops (a knight's leap too); the cooldown it has keeps running
            DropHook();
            DropRope();
            EndClimbPose();
            LetGo(0.3f);
            chainCarry = true;
            chainCarryAlly = ally;
            chainBy = rook;
            chainCarryT = 0f;
            chainCarryTime = Mathf.Max(0.05f, seconds);
            chainCarryFrom = bodies[0].position;
            chainFromFloor = FeetPoint.y;
            chainCarryTo = to;
        }

        /// <summary>Where the hips are <paramref name="t"/> seconds into the pull: an enemy along a low line 0.3 m over the floor
        /// (eased: slow off, fast midway, slow in), a teammate on a low arc (rookAllyArc at its top).</summary>
        Vector3 ChainPath(float t, PawnRushSkillParams s)
        {
            float u = Mathf.Clamp01(t / chainCarryTime);
            Vector3 a = chainCarryFrom, b = chainCarryTo;
            if (chainCarryAlly)
            {
                float floorA = Mathf.Lerp(chainFromFloor, b.y, u);
                return new Vector3(Mathf.Lerp(a.x, b.x, u), floorA + standHeight + 4f * s.rookAllyArc * u * (1f - u), Mathf.Lerp(a.z, b.z, u));
            }
            float e = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * u);
            float floor = Mathf.Lerp(chainFromFloor, b.y, e);
            float hover = s.rookPullHover * Mathf.Clamp01(u * 6f) * Mathf.Clamp01((1f - u) * 6f + 0.3f);
            return new Vector3(Mathf.Lerp(a.x, b.x, e), floor + standHeight + hover, Mathf.Lerp(a.z, b.z, e));
        }

        /// <summary>The pull this step (the host's). True while it runs: the rest of the skills wait.</summary>
        bool UpdateChainCarry(PawnRushSkillParams s, float dt)
        {
            if (!chainCarry) return false;
            var rook = chainBy;
            if (rook == null || rook.chainOn != this || rook.chainPhase != ChainPhase.Pull || State != PawnState.Active)
            {
                EndChainCarry(false);
                return false;
            }
            chainCarryT += dt;
            grabImmune = Mathf.Max(grabImmune, 0.25f);
            if (chainCarryT >= chainCarryTime)
            {
                EndChainCarry(true);
                return false;
            }
            Vector3 at = bodies[0].position, next = ChainPath(chainCarryT + dt, s);
            // Something solid on the way drops it there (§5.6); a cliff does not stop it.
            Vector3 step = next - at;
            if (step.sqrMagnitude > 1e-6f && ChainBlocked(at, step))
            {
                Log($"{DisplayName}: 끌려오다 장애물에 걸려 놓임");
                EndChainCarry(false);
                return false;
            }
            Vector3 toRook = Flat(rook.bodies[0].position - at);
            if (toRook.sqrMagnitude > 0.01f) Face(toRook);
            Fly(next, step / Mathf.Max(1e-4f, dt), 0f, default, 18f);
            return true;
        }

        /// <summary>Something solid (not a piece, not a loose thing) right in the way of the body's next step.</summary>
        bool ChainBlocked(Vector3 from, Vector3 step)
        {
            float d = step.magnitude;
            int n = Physics.SphereCastNonAlloc(from, 0.22f, step / d, hits, d + 0.1f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider == null || h.distance <= 0f || ownSet.Contains(h.collider) || ColliderOwner.ContainsKey(h.collider) || PassesThrough(h.collider)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                if (h.normal.y > 0.6f) continue;   // the floor under a low path
                return true;
            }
            return false;
        }

        void EndChainCarry(bool arrived)
        {
            if (!chainCarry) return;
            chainCarry = false;
            var rook = chainBy;
            chainBy = null;
            var s = PawnRushSkills ?? rook?.PawnRushSkills;
            if (!arrived)
            {
                // Let go mid-way: it carries on from the speed it had and falls where it is.
                EndFlight(bodies[0].linearVelocity * 0.5f);
                return;
            }
            EndFlight(Vector3.zero);
            KeepSkillLandingSafe();
            if (rook != null) Face(chainCarryAlly ? rook.facing : Flat(rook.bodies[0].position - bodies[0].position));
            if (!chainCarryAlly && s != null)
            {
                Stagger(s.rookPullStagger);
                LastSkillHit = $"룩 사슬: 끌려와 휘청 {s.rookPullStagger:0.0}초";
            }
            if (rook != null) rook.Fx(SkillFxKind.RookPullEnd, this, FeetPoint, Flat(bodies[0].position - rook.bodies[0].position), chainCarryAlly ? 1 : 0);
            Log($"{DisplayName}: 룩 {(chainCarryAlly ? "옆에 착지" : "앞에 놓임 → 휘청")} ({chainCarryT:0.00}초)");
        }
    }
}
