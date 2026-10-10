using ChessFight.Network;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public enum SfEdge : byte { None, Windup, Active, Recovery }

    /// <summary>
    /// The edge skills (R103, 승규 님 2026-10-10 "가장자리 스킬"): a second skill per piece on its own key (E, the settings'
    /// skill key), one press, no aim, only where a fight is won or lost - at the platform's edge (within
    /// <see cref="SwordFightEdgeParams.edgeZone"/> of it). When its condition holds the HUD's E lights up and the nearest
    /// edge glows in the piece's colour. Each has one dramatic beat (slow motion, the view pulled in, the edge flashing;
    /// see <see cref="SwordFightSkillFx"/>). A telegraph whose condition breaks calls the skill off at half the cooldown.
    /// <list type="bullet">
    /// <item>킹 · 왕의 귀환: pushed off and falling (within <see cref="SwordFightEdgeParams.fallWindow"/>, not 2 m down yet),
    /// he drives his sword into the edge and hangs from it, then springs back up onto the platform; landing shoves every
    /// enemy round him. The landing spot shows while he hangs; a cut at the hilt (or any hit) drops him.</item>
    /// <item>퀸 · 체크메이트 일섬: an enemy in the edge zone within 5 m: a gold line to it for 0.5 s, then she is beside it
    /// and cuts it out and up off the platform. It escapes by stepping in out of the zone (cancels), jumping, or a king's
    /// guard.</item>
    /// <item>룩 · 성벽 붕괴: standing in the edge zone he hacks the edge he aims at: a 3 × 2 m chunk cracks for 0.8 s and
    /// falls away with whoever is on it (him too); it rises back after 5 s (<see cref="SwordFightEdgeFloor"/>).</item>
    /// <item>비숍 · 구원의 손: in the edge zone, an ally falling within 6 m (in its first second): two hands reach out of
    /// a hole in the edge, catch it, pull it in over 0.6 s and set it down; hitting the bishop meanwhile makes them let go.</item>
    /// <item>나이트 · 벼랑 끝 역전: its back to the edge (1.5 m) and an enemy within 3 m ahead: it crouches 0.35 s (the
    /// landing mark shows behind the enemy), springs out over the cliff and somersaults over the enemy's head, lands
    /// behind it and kicks it back toward the cliff 3.5 m. Backing off past 3 m calls it off; a hit stops it.</item>
    /// </list>
    /// The edge skill and the F skill do not run together. Offline only, like the rest.
    /// </summary>
    public partial class SwordFightSkills
    {
        public SwordFightEdgeParams EdgeParams { get; private set; }
        public SfEdge EdgeStage { get; private set; }
        public float EdgeTime { get; private set; }
        public string EdgeDetail { get; private set; } = "";
        public float EdgeCooldown => Mathf.Max(0f, edgeCooldownLeft);
        public float EdgeCooldownTotal { get; private set; }
        public int EdgeUses { get; private set; }

        /// <summary>Seconds this piece has been off the floor over the void (pushed off, falling); -1 on the floor.</summary>
        public float FallTime { get; private set; } = -1f;

        // ---------------------------------------------------------------- the condition, as it stands now

        public bool EdgeCondition { get; private set; }
        /// <summary>Why it cannot be used now (empty when it can).</summary>
        public string EdgeWhy { get; private set; } = "";
        public bool EdgeReady => EdgeParams != null && EdgeCondition && EdgeStage == SfEdge.None && edgeCooldownLeft <= 0f
                                 && (Stage == SfStage.None || Stage == SfStage.Aim);
        /// <summary>The edge that glows: the one this skill would use.</summary>
        public bool HasEdgeGlow { get; private set; }
        public EdgeHit EdgeGlow { get; private set; }
        /// <summary>The queen's and the knight's enemy, the bishop's falling ally.</summary>
        public RagdollPawn EdgeTarget { get; private set; }
        /// <summary>The rook's chunk as he aims now (x / z on the floor).</summary>
        public Rect EdgeChunk { get; private set; }

        // ---------------------------------------------------------------- the skill once it goes

        /// <summary>King: the lip his sword goes in under; rook: the middle of the chunk's outer edge; bishop: the hole
        /// on the lip; knight: the edge at its back; queen: where her enemy stood.</summary>
        public Vector3 EdgeAt { get; private set; }
        /// <summary>Flat, toward the void at <see cref="EdgeAt"/>.</summary>
        public Vector3 EdgeOut { get; private set; } = Vector3.forward;
        /// <summary>King, knight: where it lands; bishop: where the ally is set down.</summary>
        public Vector3 EdgeLand { get; private set; }
        /// <summary>The hips where it set off (the queen's blink, the king's catch, the knight's flip).</summary>
        public Vector3 EdgeStart { get; private set; }
        public SwordFightEdgeFloor.Collapse EdgeCollapse { get; private set; }
        /// <summary>The bishop's ally in its hands.</summary>
        public RagdollPawn Carried => carried;
        public bool CarriedHeld => held;
        /// <summary>The king's sword is in the stone (catching and hanging), not yet pulled out for the vault.</summary>
        public bool KingHanging => Piece == PieceKind.King && EdgeStage == SfEdge.Active && !vaulted;
        /// <summary>The king's hilt sticking out of the edge's face.</summary>
        public Vector3 Hilt => EdgeAt + EdgeOut * 0.1f + Vector3.down * 0.12f;

        bool edgeKey, held, vaulted, knightAir;
        float edgeCooldownLeft;
        int edgeHits;
        Vector3 hangHips, catchFrom, flipFrom, flipOver, rookLip, rookOut, rookAlong;
        RagdollPawn carried;

        /// <summary>The edge skill's key went down (E). Kept until the next step.</summary>
        public void PressEdge() => edgeKey = true;

        public static string EdgeName(PieceKind kind) => kind switch
        {
            PieceKind.King => "왕의 귀환",
            PieceKind.Queen => "체크메이트 일섬",
            PieceKind.Rook => "성벽 붕괴",
            PieceKind.Bishop => "구원의 손",
            PieceKind.Knight => "벼랑 끝 역전",
            _ => "(폰은 스킬 없음)",
        };

        // ---------------------------------------------------------------- the step

        /// <summary>Over the void and off its feet: the fall's clock runs (the king's and the bishop's window).</summary>
        void TrackFall(float dt)
        {
            var floor = SwordFightEdgeFloor.Current;
            if (floor == null || !floor.Find() || Pawn == null) { FallTime = -1f; return; }
            bool over = !floor.OnFloor(Pawn.Hips.position) && !Pawn.Grounded;
            FallTime = over ? (FallTime < 0f ? 0f : FallTime + dt) : -1f;
        }

        /// <summary>The edge skill's part of the step; true while one runs (the F skill waits).</summary>
        bool StepEdge(bool press, float dt)
        {
            var X = EdgeParams;
            if (X == null) return false;
            edgeCooldownLeft -= dt;
            StepClearance(dt);
            if (EdgeStage == SfEdge.None)
            {
                CheckEdge();
                if (!press) return false;
                if (edgeCooldownLeft > 0f) { Say($"{Pawn.DisplayName}: {EdgeName(Piece)} 쿨타임 {edgeCooldownLeft:0.0}초"); return false; }
                if (Stage != SfStage.None && Stage != SfStage.Aim) { Say($"{Pawn.DisplayName}: 다른 스킬을 쓰는 중"); return false; }
                if (!EdgeCondition) { Say($"{Pawn.DisplayName}: {EdgeName(Piece)} — {EdgeWhy}"); return false; }
                if (Stage == SfStage.Aim) Clear();
                BeginEdge();
                return EdgeStage != SfEdge.None;
            }
            EdgeTime += dt;
            switch (Piece)
            {
                case PieceKind.King: StepKingEdge(); break;
                case PieceKind.Queen: StepQueenEdge(); break;
                case PieceKind.Rook: StepRookEdge(); break;
                case PieceKind.Bishop: StepBishopEdge(); break;
                case PieceKind.Knight: StepKnightEdge(); break;
                default: ClearEdge(); break;
            }
            return EdgeStage != SfEdge.None;
        }

        void SetEdge(SfEdge stage, string detail)
        {
            EdgeStage = stage;
            EdgeTime = 0f;
            EdgeDetail = detail;
        }

        void ClearEdge()
        {
            if (Pawn != null && Pawn.OuterFlying) Pawn.EndFlight();
            if (carried != null && carried.OuterFlying) carried.EndFlight();
            ReleaseClearance(0.3f);
            carried = null;
            held = vaulted = knightAir = false;
            EdgeStage = SfEdge.None;
            EdgeTime = 0f;
            EdgeDetail = "";
            EdgeTarget = null;
        }

        void StartEdgeCooldown(float share)
        {
            var X = EdgeParams;
            float c = X.testCooldown > 0f ? X.testCooldown : X.Cooldown(Piece);
            EdgeCooldownTotal = c;
            edgeCooldownLeft = c * share;
        }

        void FinishEdge()
        {
            ClearEdge();
            StartEdgeCooldown(1f);
        }

        /// <summary>The telegraph's condition broke: called off, half the cooldown.</summary>
        void CancelEdge(string why)
        {
            Raise(SfFxKind.EdgeCancel, EdgeTarget, Pawn.Hips.position, Vector3.zero, 0f);
            Say($"{Pawn.DisplayName}: {EdgeName(Piece)} 취소 ({why}) — 쿨타임 반");
            ClearEdge();
            StartEdgeCooldown(0.5f);
        }

        /// <summary>Stopped by a hit: the whole cooldown.</summary>
        void InterruptEdge(string why)
        {
            Raise(SfFxKind.EdgeCancel, EdgeTarget, Pawn.Hips.position, Vector3.zero, 1f);
            Say($"{Pawn.DisplayName}: {EdgeName(Piece)} 끊김 ({why})");
            ClearEdge();
            StartEdgeCooldown(1f);
        }

        // ---------------------------------------------------------------- the condition

        void CheckEdge()
        {
            EdgeCondition = false;
            HasEdgeGlow = false;
            EdgeTarget = null;
            EdgeWhy = "";
            var X = EdgeParams;
            var floor = SwordFightEdgeFloor.Current;
            if (floor == null || !floor.Find()) { EdgeWhy = "발판을 못 찾음"; return; }
            Vector3 hips = Pawn.Hips.position;
            switch (Piece)
            {
                case PieceKind.King:
                {
                    if (FallTime < 0f) { EdgeWhy = "밀려 떨어지는 중에만"; return; }
                    if (FallTime > X.fallWindow || hips.y < floor.Top - X.fallDepth) { EdgeWhy = "너무 늦음"; return; }
                    if (!floor.Nearest(hips, out var e) || e.distance > X.kingReach) { EdgeWhy = "가장자리가 멂"; return; }
                    Glow(e);
                    EdgeCondition = true;
                    return;
                }
                case PieceKind.Queen:
                {
                    if (!Standing(floor)) return;
                    var target = QueenPick(floor, out var e);
                    if (target == null) { EdgeWhy = $"가장자리 구역({X.edgeZone:0.#} m)에 있는 적이 {X.queenRange:0.#} m 안에 없음"; return; }
                    EdgeTarget = target;
                    Glow(e);
                    EdgeCondition = true;
                    return;
                }
                case PieceKind.Rook:
                {
                    if (!Standing(floor)) return;
                    if (!floor.Nearest(hips, out var near) || near.distance > X.edgeZone) { EdgeWhy = $"가장자리 구역({X.edgeZone:0.#} m)에 서야 함"; return; }
                    // The edge he looks at (if it is close enough), else the one he stands by.
                    var plan = floor.Cast(hips, AimFlat(), X.rookAimReach, out var aimed) ? aimed : near;
                    EdgeChunk = floor.ChunkAt(plan, X.rookChunkWidth, X.rookChunkDepth, out rookLip);
                    rookOut = plan.outward;
                    rookAlong = plan.along;
                    Glow(plan);
                    if (floor.Overlaps(EdgeChunk)) { EdgeWhy = "그 자리는 이미 무너지는 중"; return; }
                    EdgeCondition = true;
                    return;
                }
                case PieceKind.Bishop:
                {
                    if (!Standing(floor)) return;
                    if (!floor.Nearest(hips, out var near) || near.distance > X.edgeZone) { EdgeWhy = $"가장자리 구역({X.edgeZone:0.#} m)에 서야 함"; return; }
                    var ally = BishopPick(floor, out var e);
                    if (ally == null) { EdgeWhy = $"{X.bishopRange:0.#} m 안에 떨어지는 아군이 없음"; return; }
                    EdgeTarget = ally;
                    Glow(e);
                    EdgeCondition = true;
                    return;
                }
                case PieceKind.Knight:
                {
                    // R109 (승규 님: "링 밖을 나가면 스킬이 켜져야 하는데 안 켜질 때가 있어"): it lit only standing with the
                    // hips over the floor and the camera looking inward. Now also with the hips a little past the lip, with
                    // the camera any way (then "ahead" is straight in from the edge), and in the first moment of a fall
                    // off it (pushed or stepped off: the knight catches itself in the air, then flips back over).
                    bool air = FallTime >= 0f;
                    if (!floor.Nearest(hips, out var back) || back.distance > X.knightBackEdge)
                    { EdgeWhy = $"가장자리를 등지고({X.knightBackEdge:0.#} m 안) 서야 함"; return; }
                    if (air)
                    {
                        if (FallTime > X.fallWindow || hips.y < floor.Top - X.fallDepth * 0.5f) { EdgeWhy = "떨어진 지 너무 오래됨"; return; }
                    }
                    else
                    {
                        if (Pawn.State != PawnState.Active) { EdgeWhy = "넘어져 있음"; return; }
                        if (!floor.OnFloor(hips) && !floor.OnFloor(hips - back.outward * KnightLipSlack)) { EdgeWhy = "발판 위에서만"; return; }
                    }
                    Vector3 inward = -back.outward, aim = AimFlat();
                    bool looking = !air && Vector3.Dot(aim, inward) > 0.2f;
                    Vector3 front = looking ? aim : inward;
                    var foe = KnightPick(floor, front, looking ? 0.5f : 0.35f, X.knightFront + (air ? KnightAirReach : 0f));
                    if (foe == null) { EdgeWhy = $"앞 {X.knightFront:0.#} m 안에 적이 없음"; return; }
                    EdgeTarget = foe;
                    Glow(back);
                    EdgeCondition = true;
                    return;
                }
                default:
                    EdgeWhy = "폰은 스킬이 없어요";
                    return;
            }
        }

        bool Standing(SwordFightEdgeFloor floor)
        {
            if (Pawn.State != PawnState.Active) { EdgeWhy = "넘어져 있음"; return false; }
            if (!floor.OnFloor(Pawn.Hips.position)) { EdgeWhy = "발판 위에서만"; return false; }
            return true;
        }

        void Glow(EdgeHit e)
        {
            HasEdgeGlow = true;
            EdgeGlow = e;
        }

        /// <summary>Where the camera looks, flat (the scripted runs' override first); the facing if none.</summary>
        Vector3 AimFlat()
        {
            Vector3 a = Flat(aimOverride ?? rawAim);
            return a.sqrMagnitude > 1e-4f ? a.normalized : Flat(Pawn.Facing).normalized;
        }

        /// <summary>The queen's enemy: in the edge zone, within range; the one the camera looks at most, then the nearest.</summary>
        RagdollPawn QueenPick(SwordFightEdgeFloor floor, out EdgeHit edge)
        {
            var X = EdgeParams;
            edge = default;
            RagdollPawn best = null;
            float bestScore = float.MaxValue;
            Vector3 me = Flat(Pawn.Hips.position), aim = AimFlat();
            foreach (var other in All)
            {
                if (!IsEnemy(other)) continue;
                Vector3 h = other.Pawn.Hips.position;
                if (!floor.OnFloor(h) || !floor.Nearest(h, out var e) || e.distance > X.edgeZone) continue;
                Vector3 d = Flat(h) - me;
                if (d.magnitude > X.queenRange) continue;
                float score = Vector3.Angle(aim, d) + d.magnitude * 4f;
                if (score >= bestScore) continue;
                bestScore = score;
                best = other.Pawn;
                edge = e;
            }
            return best;
        }

        /// <summary>The bishop's ally: falling (its first second, not too far down yet), within range, the nearest.</summary>
        RagdollPawn BishopPick(SwordFightEdgeFloor floor, out EdgeHit edge)
        {
            var X = EdgeParams;
            edge = default;
            RagdollPawn best = null;
            float bestDistance = float.MaxValue;
            foreach (var other in All)
            {
                if (other == this || other.Fighter == null || !other.Fighter.Alive || other.Pawn == null || other.Pawn.Team != Pawn.Team) continue;
                if (other.FallTime < 0f || other.FallTime > X.bishopFallWindow || other.EdgeStage != SfEdge.None) continue;
                Vector3 h = other.Pawn.Hips.position;
                if (h.y < floor.Top - X.fallDepth - 0.8f) continue;
                float d = Flat(h - Pawn.Hips.position).magnitude;
                if (d > X.bishopRange || d >= bestDistance || !floor.Nearest(h, out var e)) continue;
                bestDistance = d;
                best = other.Pawn;
                edge = e;
            }
            return best;
        }

        /// <summary>The knight's enemy: standing ahead of it (within 60° of where it looks), within range, the nearest.</summary>
        /// <summary>How far past the lip the knight's hips may be and still count as standing at the edge (R109).</summary>
        const float KnightLipSlack = 0.6f;
        /// <summary>Falling, the knight is already out past the edge: the enemy may be this much further (R109).</summary>
        const float KnightAirReach = 1.0f;

        /// <summary>The knight's enemy: on the floor, within <paramref name="range"/>, within the cone
        /// (<paramref name="cone"/> = the least cosine) round <paramref name="front"/>; the nearest.</summary>
        RagdollPawn KnightPick(SwordFightEdgeFloor floor, Vector3 front, float cone, float range)
        {
            RagdollPawn best = null;
            float bestDistance = float.MaxValue;
            foreach (var other in All)
            {
                if (!IsEnemy(other)) continue;
                Vector3 h = other.Pawn.Hips.position;
                if (!floor.OnFloor(h)) continue;
                Vector3 d = Flat(h - Pawn.Hips.position);
                float m = d.magnitude;
                if (m > range || m >= bestDistance || m < 0.3f || Vector3.Dot(d / m, front) < cone) continue;
                bestDistance = m;
                best = other.Pawn;
            }
            return best;
        }

        // ---------------------------------------------------------------- using it

        void BeginEdge()
        {
            var X = EdgeParams;
            var floor = SwordFightEdgeFloor.Current;
            edgeHits = Pawn.Hits;
            held = vaulted = knightAir = false;
            carried = null;
            EdgeStart = Pawn.Hips.position;
            Vector3 hips = Pawn.Hips.position;
            switch (Piece)
            {
                case PieceKind.King:
                {
                    var e = EdgeGlow;
                    EdgeAt = e.point;
                    EdgeOut = e.outward;
                    // The sword goes into the face just under the lip; he hangs from its hilt, his hips under his raised hand.
                    hangHips = e.point + e.outward * 0.22f + Vector3.down * 0.42f;
                    EdgeLand = Inward(floor, e, X.kingLandIn);
                    Pawn.StandUp();
                    Pawn.Face(-e.outward);
                    SetEdge(SfEdge.Active, "칼 박기");
                    Raise(SfFxKind.EdgeCast, null, Hilt, e.outward, 0f);
                    Raise(SfFxKind.KingStab, null, Hilt, e.outward, X.kingHang);
                    Say($"{Pawn.DisplayName}: 왕의 귀환 — 칼을 가장자리에 박고 매달림 (떨어진 지 {Mathf.Max(0f, FallTime):0.00}초)");
                    break;
                }
                case PieceKind.Queen:
                {
                    var target = EdgeTarget;
                    EdgeAt = target.Hips.position;
                    EdgeOut = EdgeGlow.outward;
                    Pawn.Face(target.Hips.position - hips);
                    SetEdge(SfEdge.Windup, "금색 선");
                    Raise(SfFxKind.EdgeCast, target, Floor(hips), Flat(target.Hips.position - hips).normalized, 0f);
                    Raise(SfFxKind.QueenMark, target, target.Hips.position, Flat(target.Hips.position - hips).normalized, X.queenLine);
                    Say($"{Pawn.DisplayName}: 체크메이트 일섬 예고 → {target.DisplayName} ({Flat(target.Hips.position - hips).magnitude:0.0} m)");
                    break;
                }
                case PieceKind.Rook:
                {
                    EdgeCollapse = floor.Begin(EdgeChunk, rookOut, rookAlong, rookLip, X.rookCrack, X.rookRestore, this);
                    if (EdgeCollapse == null) { Say($"{Pawn.DisplayName}: 성벽 붕괴 — 발판을 나눌 수 없음"); return; }
                    EdgeAt = rookLip;
                    EdgeOut = rookOut;
                    Pawn.Face(rookLip - hips);
                    SetEdge(SfEdge.Windup, "내려치기");
                    Raise(SfFxKind.EdgeCast, null, Floor(hips), rookOut, 0f);
                    Raise(SfFxKind.RookBreak, null, rookLip, rookOut, X.rookChunkWidth);
                    Say($"{Pawn.DisplayName}: 성벽 붕괴 — {X.rookChunkWidth:0.#} × {X.rookChunkDepth:0.#} m에 금이 감, {X.rookCrack:0.0}초 뒤 무너짐");
                    break;
                }
                case PieceKind.Bishop:
                {
                    carried = EdgeTarget;
                    EdgeAt = EdgeGlow.point;
                    EdgeOut = EdgeGlow.outward;
                    EdgeLand = RescueLanding(floor, EdgeGlow, X.bishopSetIn);
                    Clearance(carried, true);
                    SetEdge(SfEdge.Active, "손");
                    Raise(SfFxKind.EdgeCast, carried, Floor(hips), EdgeOut, 0f);
                    Raise(SfFxKind.BishopReach, carried, carried.Hips.position, EdgeOut, X.bishopReach);
                    Say($"{Pawn.DisplayName}: 구원의 손 → {carried.DisplayName}");
                    break;
                }
                case PieceKind.Knight:
                {
                    var target = EdgeTarget;
                    EdgeAt = EdgeGlow.point;
                    EdgeOut = EdgeGlow.outward;
                    EdgeLand = KnightLanding(floor, target);
                    // Off the edge already (R109): it stops in the air where it is (a little up to the lip if it has
                    // dropped below it) and crouches there, then the flip starts from the air.
                    knightAir = FallTime >= 0f;
                    if (knightAir)
                    {
                        hangHips = new Vector3(hips.x, Mathf.Max(hips.y, floor.Top - 0.25f), hips.z);
                        if (Pawn.State != PawnState.Active) Pawn.StandUp();
                    }
                    Pawn.Face(target.Hips.position - hips);
                    SetEdge(SfEdge.Windup, "웅크림");
                    Raise(SfFxKind.EdgeCast, target, Floor(hips), Flat(target.Hips.position - hips).normalized, 0f);
                    Raise(SfFxKind.KnightCrouch, target, EdgeLand, Flat(EdgeLand - hips).normalized, X.knightCrouch);
                    Say($"{Pawn.DisplayName}: 벼랑 끝 역전 — 웅크림 ({target.DisplayName} 뒤에 착지 예정)");
                    break;
                }
            }
            if (EdgeStage != SfEdge.None) EdgeUses++;
        }

        /// <summary>A spot on the floor <paramref name="inward"/> in from an edge (less if the floor is not there).</summary>
        Vector3 Inward(SwordFightEdgeFloor floor, EdgeHit e, float inward)
        {
            for (float d = inward; d >= 0.3f; d -= 0.2f)
            {
                Vector3 p = e.point - e.outward * d;
                if (floor.OnFloor(p)) return Floor(p);
            }
            return Floor(e.point - e.outward * 0.3f);
        }

        /// <summary>Where the bishop sets the ally down (R109): <paramref name="inward"/> inside the edge, but never on the
        /// bishop — the ally set on him was shoved back over the edge (승규 님: "아군이 올라오면 나 때문에 다시 떨어져").
        /// If the spot is within <see cref="RescueRoom"/> of the bishop it slides along the edge, away from him first.</summary>
        Vector3 RescueLanding(SwordFightEdgeFloor floor, EdgeHit e, float inward)
        {
            Vector3 me = Flat(Pawn.Hips.position);
            Vector3 along = Vector3.Cross(Vector3.up, e.outward).normalized;
            float away = Vector3.Dot(Flat(e.point) - me, along) >= 0f ? 1f : -1f;
            foreach (float side in new[] { 0f, 0.5f, 1f, 1.5f, 2f })
                foreach (float sign in new[] { away, -away })
                {
                    if (side == 0f && sign != away) continue;
                    var shifted = e;
                    shifted.point = e.point + along * (side * sign);
                    if (!floor.OnFloor(shifted.point - e.outward * 0.3f)) continue;
                    Vector3 p = Inward(floor, shifted, inward);
                    if ((Flat(p) - me).magnitude >= RescueRoom) return p;
                }
            return Inward(floor, e, inward);
        }

        const float RescueRoom = 1.0f;

        // While the bishop's hands pull an ally up, and a moment after it is set down, the two do not collide (bodies and
        // swords): the ally comes up past him and lands by him, not on him.
        readonly List<(Collider, Collider)> clearance = new List<(Collider, Collider)>();
        RagdollPawn clearedFrom;
        float clearanceLeft;
        bool clearanceEnding;

        void Clearance(RagdollPawn other, bool on)
        {
            foreach (var (a, b) in clearance) if (a != null && b != null) Physics.IgnoreCollision(a, b, false);
            clearance.Clear();
            clearedFrom = null;
            clearanceEnding = false;
            if (!on || other == null || Pawn == null) return;
            var mine = Pawn.GetComponentsInChildren<Collider>();
            foreach (var c in other.GetComponentsInChildren<Collider>())
                foreach (var m in mine)
                {
                    if (c == null || m == null || c.isTrigger || m.isTrigger) continue;
                    Physics.IgnoreCollision(m, c, true);
                    clearance.Add((m, c));
                }
            clearedFrom = other;
        }

        /// <summary>After the set-down the pair stays apart for <paramref name="seconds"/>, and then until they are clear of
        /// each other.</summary>
        void ReleaseClearance(float seconds) { if (clearedFrom != null && !clearanceEnding) { clearanceLeft = seconds; clearanceEnding = true; } }

        void StepClearance(float dt)
        {
            if (clearedFrom == null || !clearanceEnding) return;
            clearanceLeft -= dt;
            bool apart = Pawn == null || (Flat(clearedFrom.Hips.position) - Flat(Pawn.Hips.position)).magnitude > 0.9f;
            if (clearanceLeft <= 0f && (apart || clearanceLeft < -2f)) Clearance(null, false);
        }

        Vector3 KnightLanding(SwordFightEdgeFloor floor, RagdollPawn target)
        {
            var X = EdgeParams;
            Vector3 t = Flat(target.Hips.position), d = t - Flat(Pawn.Hips.position);
            d = d.sqrMagnitude > 1e-4f ? d.normalized : -EdgeOut;
            for (float b = X.knightLandBehind; b >= 0.3f; b -= 0.15f)
            {
                Vector3 p = t + d * b + Vector3.up * floor.Top;
                if (floor.OnFloor(p) && floor.Nearest(p, out var e) && e.distance > 0.35f) return Floor(p);
            }
            return Floor(t + d * 0.3f + Vector3.up * floor.Top);
        }

        // ---------------------------------------------------------------- king

        void StepKingEdge()
        {
            var X = EdgeParams;
            float t = EdgeTime, c = X.kingCatch, h = X.kingHang, v = X.kingVault;
            if (EdgeStage == SfEdge.Recovery)
            {
                if (t >= 0.35f) FinishEdge();
                return;
            }
            if (t < c + h)
            {
                // A hit, or a cut at the hilt sticking out of the stone, and he falls.
                if (Pawn.Hits != edgeHits) { KingDrops(null); return; }
                if (HiltStruck(out var by)) { KingDrops(by); return; }
                if (t < c)
                {
                    float k = Mathf.Clamp01(t / c), e = 1f - (1f - k) * (1f - k);
                    Pawn.Fly(Vector3.Lerp(EdgeStart, hangHips, e), (hangHips - EdgeStart) * (2f * (1f - k) / c), 0f, default, 24f);
                }
                else
                {
                    if (EdgeDetail != "매달림") EdgeDetail = "매달림";
                    // A short swing on the hilt as the fall is caught.
                    float s = Mathf.Sin((t - c) * 15f) * Mathf.Exp(-(t - c) * 6f) * 0.07f;
                    Pawn.Fly(hangHips + EdgeOut * s, Vector3.zero, 0f, default, 18f);
                }
                return;
            }
            if (!vaulted)
            {
                vaulted = true;
                EdgeDetail = "귀환";
                Raise(SfFxKind.KingVault, null, Pawn.Hips.position, -EdgeOut, v);
                Say($"{Pawn.DisplayName}: 몸을 튕겨 발판 위로");
            }
            // An arc up over the lip onto the landing spot.
            float u = Mathf.Clamp01((t - c - h) / v);
            const float Rise = 0.95f;
            Vector3 a = hangHips, b = EdgeLand + Vector3.up * (Pawn.standHeight + 0.03f);
            if (u < 1f)
            {
                Vector3 pos = a + (b - a) * u + Vector3.up * (4f * Rise * u * (1f - u));
                Vector3 vel = ((b - a) + Vector3.up * (4f * Rise * (1f - 2f * u))) / v;
                Pawn.Fly(pos, vel, 0f, default, 20f);
                return;
            }
            Pawn.EndFlight(Vector3.down * 0.8f);
            KingLands();
        }

        void KingLands()
        {
            var X = EdgeParams;
            Raise(SfFxKind.KingLand, null, EdgeLand, -EdgeOut, X.kingLandRadius);
            int n = 0;
            foreach (var other in All)
            {
                if (!IsEnemy(other)) continue;
                Vector3 d = Flat(other.Pawn.Hips.position - EdgeLand);
                if (d.magnitude > X.kingLandRadius) continue;
                Hit(other, d.sqrMagnitude > 1e-4f ? d.normalized : -EdgeOut, X.kingLandPush, "왕의 귀환 착지", SfFxKind.KingLandHit, n++);
            }
            Say($"{Pawn.DisplayName}: 착지 — 반경 {X.kingLandRadius:0.#} m 안 적 {n}명 밀어냄");
            SetEdge(SfEdge.Recovery, "착지");
        }

        void KingDrops(SwordFightSkills by)
        {
            Pawn.EndFlight();
            Raise(SfFxKind.KingDrop, by != null ? by.Pawn : null, Hilt, EdgeOut, 0f);
            Say($"{Pawn.DisplayName}: 왕의 귀환 실패 — " + (by != null ? $"박힌 칼을 맞아 떨어짐 ({by.Pawn.DisplayName})" : "맞아서 떨어짐"));
            ClearEdge();
            StartEdgeCooldown(1f);
        }

        /// <summary>An enemy's cut passing the hilt (its blade within 0.4 m of it).</summary>
        bool HiltStruck(out SwordFightSkills by)
        {
            by = null;
            Vector3 hilt = Hilt;
            foreach (var other in All)
            {
                if (!IsEnemy(other) || !other.Fighter.Attacking) continue;
                Vector3 a = other.Fighter.BladeRoot, b = other.Fighter.BladeTip, ab = b - a;
                float k = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(hilt - a, ab) / ab.sqrMagnitude) : 0f;
                if ((a + ab * k - hilt).magnitude > 0.4f) continue;
                by = other;
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- queen

        void StepQueenEdge()
        {
            var X = EdgeParams;
            var floor = SwordFightEdgeFloor.Current;
            if (EdgeStage == SfEdge.Windup)
            {
                if (Pawn.State != PawnState.Active) { InterruptEdge("넘어짐"); return; }
                if (!QueenHolds(floor, EdgeTarget, out string why)) { CancelEdge(why); return; }
                if (EdgeTime >= X.queenLine) QueenStrikes(floor);
                return;
            }
            if (EdgeStage == SfEdge.Recovery && EdgeTime >= X.queenRecovery) FinishEdge();
        }

        bool QueenHolds(SwordFightEdgeFloor floor, RagdollPawn target, out string why)
        {
            var X = EdgeParams;
            var ts = target != null ? target.GetComponent<SwordFightSkills>() : null;
            why = "";
            if (ts == null || ts.Fighter == null || !ts.Fighter.Alive) { why = "적이 사라짐"; return false; }
            Vector3 h = target.Hips.position;
            if (!floor.OnFloor(h)) return true;   // already going over: let the cut land
            if (!floor.Nearest(h, out var e) || e.distance > X.edgeZone + 0.1f) { why = "적이 가장자리 구역 밖으로 들어옴"; return false; }
            if (Flat(h - Pawn.Hips.position).magnitude > X.queenRange + 0.5f) { why = "적이 멀어짐"; return false; }
            return true;
        }

        /// <summary>She is beside it in an instant, on its inner side, and cuts it out and up off the platform - unless it
        /// is in the air (a jump: the cut goes under it) or a king in his guard takes it.</summary>
        void QueenStrikes(SwordFightEdgeFloor floor)
        {
            var X = EdgeParams;
            var target = EdgeTarget;
            var ts = target.GetComponent<SwordFightSkills>();
            if (!floor.Nearest(target.Hips.position, out var e)) e = new EdgeHit { outward = EdgeOut };
            Vector3 tf = Flat(target.Hips.position), from = Flat(Pawn.Hips.position);
            Vector3 spot = tf - e.outward * 0.8f;
            if (!floor.OnFloor(spot + Vector3.up * floor.Top)) spot = tf + (from - tf).normalized * 0.8f;
            Vector3 ground = Floor(spot + Vector3.up * floor.Top);
            Vector3 face = Flat(tf - spot);
            face = face.sqrMagnitude > 1e-4f ? face.normalized : e.outward;
            EdgeStart = Pawn.Hips.position;
            EdgeAt = target.Hips.position;
            EdgeOut = e.outward;
            Pawn.Teleport(ground + Vector3.up * (Pawn.standHeight + 0.02f), face);
            Raise(SfFxKind.QueenBlink, target, ground, face, Vector3.Distance(from, spot));
            if (ts != null && ts.Piece == PieceKind.King && ts.Stage == SfStage.Windup && ts.Parry(Fighter, target.bodies[(int)BodyId.Chest].position))
            {
                Say($"{Pawn.DisplayName}: 체크메이트 일섬 — {target.DisplayName}의 받아내기에 막힘");
                SetEdge(SfEdge.Recovery, "막힘");
                return;
            }
            float air = target.Hips.position.y - (floor.Top + target.standHeight);
            if (!target.Grounded && air > 0.2f)
            {
                Raise(SfFxKind.QueenMiss, target, target.Hips.position, face, 0f);
                Say($"{Pawn.DisplayName}: 체크메이트 일섬 — 점프로 피함 ({target.DisplayName})");
                SetEdge(SfEdge.Recovery, "헛벰");
                return;
            }
            Vector3 dir = Flat(e.outward * 0.85f + face * 0.15f).normalized;
            Fling(ts, dir * (X.queenFling / 0.9f) + Vector3.up * X.queenFlingUp, "체크메이트 일섬", SfFxKind.QueenCut, X.queenFling);
            SetEdge(SfEdge.Recovery, "후딜");
        }

        /// <summary>A hit that throws a piece at a set speed (not a slide: it goes off the platform).</summary>
        void Fling(SwordFightSkills other, Vector3 push, string cause, SfFxKind kind, float size)
        {
            var target = other.Pawn;
            target.TakeHit(push, S.knockdownSeconds, 0f, true);
            other.Fighter.StopCombat();
            other.pinLeft = 0f;
            if (other.Stage != SfStage.None && other.Stage != SfStage.Aim) other.StartCooldown();
            if (other.Stage != SfStage.None) other.Clear();
            Raise(kind, target, Vector3.Lerp(Pawn.bodies[(int)BodyId.Chest].position, target.bodies[(int)BodyId.Chest].position, 0.7f), Flat(push).normalized, size);
            Say($"{Pawn.DisplayName} → {target.DisplayName}: {cause} (바깥 {Flat(push).magnitude:0.0} · 위 {push.y:0.0} m/s)");
        }

        // ---------------------------------------------------------------- rook

        void StepRookEdge()
        {
            if (EdgeStage == SfEdge.Windup && EdgeTime >= EdgeParams.rookSlam) FinishEdge();
        }

        // ---------------------------------------------------------------- bishop

        void StepBishopEdge()
        {
            var X = EdgeParams;
            if (EdgeStage == SfEdge.Recovery)
            {
                if (EdgeTime >= X.bishopRecovery) FinishEdge();
                return;
            }
            var cs = carried != null ? carried.GetComponent<SwordFightSkills>() : null;
            if (Pawn.Hits != edgeHits || Pawn.State != PawnState.Active) { BishopDrops("비숍이 맞아서 손을 놓침"); return; }
            if (cs == null || cs.Fighter == null || !cs.Fighter.Alive) { BishopDrops("아군이 먼저 떨어짐"); return; }
            float t = EdgeTime;
            if (t < X.bishopReach) return;   // the hands are on their way (the effects)
            if (!held)
            {
                held = true;
                catchFrom = carried.Hips.position;
                carried.StandUp();
                cs.FallTime = -1f;
                Raise(SfFxKind.BishopCatch, carried, catchFrom, EdgeOut, X.bishopPull);
                Say($"{Pawn.DisplayName} → {carried.DisplayName}: 구원의 손이 잡음");
            }
            float k = Mathf.Clamp01((t - X.bishopReach) / X.bishopPull);
            // Up first, still outside the lip, and only over it toward the end: hit the bishop before then and the ally
            // goes back down into the void.
            Vector3 p0 = catchFrom, p3 = EdgeLand + Vector3.up * (carried.standHeight + 0.03f);
            Vector3 p1 = p0 + Vector3.up * 1.1f, p2 = new Vector3(EdgeAt.x, p3.y, EdgeAt.z) + EdgeOut * 0.4f + Vector3.up * 0.9f;
            if (k < 1f)
            {
                float s = k * k * (3f - 2f * k), ds = 6f * k * (1f - k) / X.bishopPull;
                carried.Fly(Bezier(p0, p1, p2, p3, s), BezierTangent(p0, p1, p2, p3, s) * ds, 0f, default, 20f);
                carried.Face(-EdgeOut);
                return;
            }
            carried.EndFlight(Vector3.down * 0.5f);
            ReleaseClearance(0.8f);
            Raise(SfFxKind.BishopSet, carried, EdgeLand, -EdgeOut, 0f);
            Say($"{Pawn.DisplayName} → {carried.DisplayName}: 구원 — 발판 위에 내려놓음");
            held = false;
            carried = null;
            SetEdge(SfEdge.Recovery, "후딜");
        }

        void BishopDrops(string why)
        {
            if (carried != null && carried.OuterFlying)
            {
                // Let go: it drops back the way it came, limp (not carried on over the lip by the pull's speed).
                float vy = Mathf.Min(carried.Hips.linearVelocity.y, 0.5f);
                carried.EndFlight(new Vector3(EdgeOut.x * 0.8f, vy, EdgeOut.z * 0.8f));
                carried.Knockdown("구원의 손 놓침", 0.65f);
            }
            ReleaseClearance(0.3f);
            Raise(SfFxKind.BishopDrop, carried, carried != null ? carried.Hips.position : EdgeAt, EdgeOut, held ? 1f : 0f);
            Say($"{Pawn.DisplayName}: 구원의 손 놓침 ({why})");
            ClearEdge();
            StartEdgeCooldown(1f);
        }

        // ---------------------------------------------------------------- knight

        void StepKnightEdge()
        {
            var X = EdgeParams;
            var floor = SwordFightEdgeFloor.Current;
            switch (EdgeStage)
            {
                case SfEdge.Windup:
                {
                    // In the air it is held where it caught itself (and is getting up from a knock meanwhile).
                    if (Pawn.Hits != edgeHits || (!knightAir && Pawn.State != PawnState.Active)) { InterruptEdge("웅크린 채 맞음"); return; }
                    if (knightAir) Pawn.Fly(hangHips, Vector3.zero);
                    var target = EdgeTarget;
                    var ts = target != null ? target.GetComponent<SwordFightSkills>() : null;
                    float reach = X.knightFront + (knightAir ? KnightAirReach : 0f);
                    if (ts == null || !ts.Fighter.Alive || !floor.OnFloor(target.Hips.position)
                        || Flat(target.Hips.position - Pawn.Hips.position).magnitude > reach + 0.05f)
                    { CancelEdge($"적이 {reach:0.#} m 밖으로 물러남"); return; }
                    // The mark behind it follows it until the spring.
                    EdgeLand = KnightLanding(floor, target);
                    Pawn.Face(target.Hips.position - Pawn.Hips.position);
                    if (EdgeTime < X.knightCrouch) return;
                    flipFrom = Pawn.Hips.position;
                    flipOver = target.Hips.position;
                    EdgeStart = flipFrom;
                    SetEdge(SfEdge.Active, "공중제비");
                    Raise(SfFxKind.KnightFlip, target, Floor(flipFrom), Flat(EdgeLand - flipFrom).normalized, X.knightFlip);
                    Say($"{Pawn.DisplayName}: 벼랑 밖으로 뛰어 공중제비");
                    return;
                }
                case SfEdge.Active:
                {
                    if (Pawn.Hits != edgeHits) { InterruptEdge("공중에서 맞음"); return; }
                    float k = Mathf.Clamp01(EdgeTime / X.knightFlip);
                    Vector3 dir = Flat(EdgeLand - flipFrom).normalized;
                    if (k < 1f)
                    {
                        FlipPath(floor, k, out var pos, out var vel);
                        // A forward somersault over the top (nose down about its right).
                        float turn = 360f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FlipOut + 0.05f, 0.86f, k));
                        Pawn.Fly(pos, vel, turn, Vector3.Cross(Vector3.up, dir), 24f);
                        return;
                    }
                    KnightKicks(dir);
                    SetEdge(SfEdge.Recovery, "뒷발차기");
                    return;
                }
                case SfEdge.Recovery:
                {
                    // The kick: head down, hind legs up behind it for a moment.
                    Vector3 dir = Flat(EdgeLand - flipFrom).normalized;
                    if (EdgeTime < 0.18f)
                        Pawn.Fly(EdgeLand + Vector3.up * (Pawn.standHeight + 0.03f), Vector3.zero, 55f * Mathf.Sin(EdgeTime / 0.18f * Mathf.PI), Vector3.Cross(Vector3.up, dir), 16f);
                    else if (Pawn.OuterFlying) Pawn.EndFlight(Vector3.zero);
                    if (EdgeTime >= X.knightRecovery) FinishEdge();
                    return;
                }
            }
        }

        /// <summary>The share of the knight's flight spent springing out over the cliff.</summary>
        public const float FlipOut = 0.3f;

        /// <summary>The knight's way (0..1 of its flight): back out over the cliff and up (arriving there going straight
        /// up), then up and over the enemy's head and down behind it. Also used to draw it.</summary>
        public void FlipPath(SwordFightEdgeFloor floor, float k, out Vector3 pos, out Vector3 vel)
        {
            var X = EdgeParams;
            float top = floor != null ? floor.Top : 0f;
            Vector3 s = flipFrom;
            Vector3 q = new Vector3(EdgeAt.x, top, EdgeAt.z) + EdgeOut * 0.7f + Vector3.up * 0.95f;
            Vector3 c = q - Vector3.up * 0.5f;
            Vector3 dir = Flat(EdgeLand - flipFrom).normalized;
            Vector3 apex = Flat(flipOver) + dir * 0.2f + Vector3.up * (top + X.knightFlipHeight + 0.35f);
            Vector3 land = EdgeLand + Vector3.up * (Pawn.standHeight + 0.03f);
            if (k < FlipOut)
            {
                float u = k / FlipOut, w = 1f - u;
                pos = w * w * s + 2f * w * u * c + u * u * q;
                vel = (2f * w * (c - s) + 2f * u * (q - c)) / (FlipOut * X.knightFlip);
                return;
            }
            float v = (k - FlipOut) / (1f - FlipOut);
            Vector3 q2 = q + Vector3.up * 0.6f;
            pos = Bezier(q, q2, apex, land, v);
            vel = BezierTangent(q, q2, apex, land, v) / ((1f - FlipOut) * X.knightFlip);
        }

        void KnightKicks(Vector3 dir)
        {
            var X = EdgeParams;
            var target = EdgeTarget;
            var ts = target != null ? target.GetComponent<SwordFightSkills>() : null;
            Raise(SfFxKind.KnightKick, target, EdgeLand, -dir, X.knightKick);
            if (ts != null && IsEnemy(ts) && Flat(target.Hips.position - EdgeLand).magnitude <= 1.9f)
            {
                // Back toward the cliff where the knight stood.
                Vector3 back = Flat(flipFrom - target.Hips.position);
                back = back.sqrMagnitude > 1e-4f ? back.normalized : -dir;
                Hit(ts, back, X.knightKick, "벼랑 끝 역전 뒷발차기", SfFxKind.KnightKickHit, 0, 1.6f);
            }
            else Say($"{Pawn.DisplayName}: 뒷발차기 — 적이 없음");
        }

        // ---------------------------------------------------------------- the sword while it runs

        /// <summary>Where the blade points during an edge skill (null: none).</summary>
        Vector3? EdgeBlade()
        {
            if (EdgeStage == SfEdge.None || Pawn == null) return null;
            Vector3 f = Flat(Pawn.Facing).normalized, side = Vector3.Cross(Vector3.up, f);
            switch (Piece)
            {
                case PieceKind.King:
                    if (KingHanging) return (-EdgeOut + Vector3.down * 0.22f).normalized;   // in the stone
                    if (EdgeStage == SfEdge.Active) return (Vector3.up + f * 0.3f).normalized;   // the vault: held high
                    return (f - Vector3.up * 0.75f).normalized;
                case PieceKind.Queen:
                    return EdgeStage == SfEdge.Windup ? (f + Vector3.up * 0.35f - side * 0.4f).normalized : (f - Vector3.up * 0.1f + side * 0.7f).normalized;
                case PieceKind.Rook:
                    return (Flat(EdgeAt - Pawn.Hips.position).normalized - Vector3.up * 0.9f).normalized;
                case PieceKind.Bishop:
                    return (Vector3.up + f * 0.4f).normalized;
                case PieceKind.Knight:
                    return EdgeStage == SfEdge.Windup ? (-f + Vector3.up * 0.25f).normalized
                         : EdgeStage == SfEdge.Active ? (Vector3.up + f * 0.25f).normalized
                         : (-f - Vector3.up * 0.3f).normalized;
            }
            return null;
        }

        /// <summary>The sword arm reaching somewhere else than along the blade: the king's hand on his hilt in the stone.</summary>
        public Vector3? Arm => KingHanging && Pawn != null ? (Hilt - Pawn.bodies[(int)BodyId.Chest].position).normalized : (Vector3?)null;

        /// <summary>The sword stays put where its hilt is (the king's, stuck in the stone), not in the hand.</summary>
        public Vector3? SwordAt => KingHanging ? Hilt : (Vector3?)null;

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        static Vector3 BezierTangent(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return 3f * u * u * (b - a) + 6f * u * t * (c - b) + 3f * t * t * (d - c);
        }
    }
}
