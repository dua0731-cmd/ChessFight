using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public enum SfStage : byte { None, Aim, Windup, Active, Recovery }

    public enum SfFxKind : byte
    {
        Cast,
        KingParry, KingCounterHit, KingWhiff,
        QueenThrust, QueenHit, QueenStop,
        RookSlam, RookHit, RookBlocked,
        BishopFire, BishopSlow, BishopPin,
        KnightLeap, KnightLand, KnightHit,
        Interrupted,
        // The edge skills (R103, E).
        EdgeCast, EdgeCancel,
        KingStab, KingVault, KingLand, KingLandHit, KingDrop,
        QueenMark, QueenBlink, QueenCut, QueenMiss,
        RookBreak,
        BishopReach, BishopCatch, BishopSet, BishopDrop,
        KnightCrouch, KnightFlip, KnightKick, KnightKickHit,
    }

    public struct SfFxEvent
    {
        public SfFxKind kind;
        public SwordFightSkills by;
        public RagdollPawn target;
        public Vector3 at, dir;
        public float size;
        public int order;
    }

    /// <summary>
    /// One fighter's Sword Fight piece skill (R91, the skill test scene only: the bed adds it; the match scene never has
    /// it). Design doc chapter 8 on the repo's rules (ring-out = 1 point, no health, the pawn has no skill):
    /// 킹 왕의 반격 (a short guard that takes one sword hit and shoves everyone round), 퀸 꼬치 베기 (a long free-aimed
    /// thrust that pushes a line of pieces, less the further back), 룩 열린 파일 포격 (a ground shockwave along a line,
    /// stopped by solid things, no turning once it goes), 비숍 관통 핀 (two hands reaching on the diagonals to a point:
    /// every piece there is pinned by the ankles, then slowed; anywhere on the floor since R102, before only with the
    /// floor ending right behind it), 나이트 포크 강하 (a leap to a chosen spot that hits two spots ahead).
    /// The skill key (F in the test scene) starts the aim, the left click uses it, the right click (or the key again) calls it
    /// off, as in the other skill test scenes; the king has no aim: the key is his guard. A skill knocked out of its windup still starts the cooldown.
    /// Each piece also has an edge skill on E (R103, <c>SwordFightSkills.Edge.cs</c>).
    /// Offline only: the network packets do not carry skills.
    /// </summary>
    [DefaultExecutionOrder(-75)]   // after the match's step (-90) and the bed (-80), before the sword (-60)
    public partial class SwordFightSkills : MonoBehaviour
    {
        public static event Action<SfFxEvent> Fx;
        public static event Action<SwordFightSkills, string> Log;
        public static readonly List<SwordFightSkills> All = new List<SwordFightSkills>();

        public SwordFightSkillParams S;
        public SwordFightPawn Fighter { get; private set; }
        public RagdollPawn Pawn => Fighter != null ? Fighter.Pawn : null;
        public PieceKind Piece => Pawn != null ? Pawn.Piece : PieceKind.Pawn;
        public bool HasSkill => Piece != PieceKind.Pawn;

        public SfStage Stage { get; private set; }
        public float StageTime { get; private set; }
        public float Cooldown => Mathf.Max(0f, cooldownLeft);
        public float CooldownTotal { get; private set; }
        public string Detail { get; private set; } = "";
        public int Uses { get; private set; }

        // aim
        public Vector3 AimDir { get; private set; } = Vector3.forward;
        public Vector3 AimPoint { get; private set; }
        public bool AimValid { get; private set; }
        /// <summary>The bishop: the enemy on the aimed point (the one the hands will take).</summary>
        public RagdollPawn PinCandidate { get; private set; }
        public Vector3 Origin { get; private set; }

        // the skill once it goes
        public Vector3 Dir { get; private set; } = Vector3.forward;
        public Vector3 Point { get; private set; }
        public float Reach { get; private set; }
        /// <summary>The rook's wave front, metres from where it started (Active).</summary>
        public float WaveFront { get; private set; }
        public bool WaveBlocked { get; private set; }
        public Vector3[] KnightSpots { get; } = new Vector3[2];
        public bool KingCountered { get; private set; }
        /// <summary>The queen's and the rook's line as aimed now: how far it runs before an edge (the queen) or a wall.</summary>
        public float AimReach { get; private set; }
        /// <summary>The queen's dash: where her hips started and how far along the line they are now.</summary>
        public Vector3 DashStart { get; private set; }
        public float DashProgress { get; private set; }

        // what is done to this piece
        public float SlowLeft => Mathf.Max(0f, slowLeft);
        public float PinLeft => Mathf.Max(0f, pinLeft);

        bool pressEdge, confirmEdge, cancelEdge;
        Vector3? aimOverride;
        Vector3? pointOverride;
        Vector3 rawAim = Vector3.forward;
        float cooldownLeft, slowLeft, pinLeft, guardUntil;
        readonly HashSet<RagdollPawn> hitSet = new HashSet<RagdollPawn>();
        Camera aimCamera;

        public void Init(SwordFightPawn fighter, SwordFightSkillParams s, SwordFightEdgeParams edge = null)
        {
            Fighter = fighter;
            S = s;
            EdgeParams = edge;
            fighter.Skills = this;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); PassThrough(false); Clearance(null, false); }
        void OnDestroy()
        {
            All.Remove(this);
            if (Fighter != null && Fighter.Skills == this) Fighter.Skills = null;
        }

        // ---------------------------------------------------------------- input

        /// <summary>The skill key went down (starts the aim; again while aiming calls it off). Kept until the next step.</summary>
        public void PressKey() => pressEdge = true;

        /// <summary>Use the aimed skill (the left click while aiming; it also comes in with the match's input).</summary>
        public void Confirm() => confirmEdge = true;

        /// <summary>Call the aim off (the right click while aiming).</summary>
        public void CancelAim() => cancelEdge = true;

        /// <summary>The camera the point aims (the bishop's and the knight's) are taken from: its centre ray onto the floor.</summary>
        public void SetAimCamera(Camera cam) => aimCamera = cam;

        /// <summary>Scripted runs: aim this way / at this point instead of the camera.</summary>
        public void OverrideAim(Vector3? dir, Vector3? point = null)
        {
            aimOverride = dir;
            pointOverride = point;
        }

        /// <summary>What the match hands the sword this step, with the skill's say: rooted pieces do not walk, aiming and
        /// skills keep the sword in, a slowed piece walks at 60%, a pinned one not at all.</summary>
        public PawnInput Filter(PawnInput raw)
        {
            if (raw.aim.sqrMagnitude > 0.01f) rawAim = raw.aim;
            if (Stage == SfStage.Aim && raw.shove)
            {
                confirmEdge = true;
                raw.shove = raw.shoveHeld = false;
            }
            if (Stage != SfStage.None || EdgeStage != SfEdge.None)
            {
                raw.shove = raw.shoveHeld = false;
                raw.sprint = false;
            }
            bool rooted = Stage == SfStage.Windup || Stage == SfStage.Recovery || (Stage == SfStage.Active && Piece != PieceKind.Knight)
                          || EdgeStage != SfEdge.None;   // an edge skill holds the piece where it is (or carries it)
            if (rooted || pinLeft > 0f)
            {
                raw.move = Vector3.zero;
                raw.jump = false;
                raw.sprint = false;
            }
            else if (slowLeft > 0f)
            {
                raw.move *= S != null ? S.bishopSlow : 0.6f;
                raw.sprint = false;
            }
            return raw;
        }

        /// <summary>Called by the sword when an enemy's cut is about to land on this piece: the king's guard takes it.</summary>
        public bool Parry(SwordFightPawn attacker, Vector3 at)
        {
            if (Piece != PieceKind.King || Stage != SfStage.Windup || S == null) return false;
            KingCountered = true;
            Raise(SfFxKind.KingParry, null, at, Flat(at - Pawn.Hips.position), 0f);
            Say($"{Pawn.DisplayName}: 왕의 반격 — {attacker.Pawn.DisplayName}의 칼을 받아냄");
            SetStage(SfStage.Active, "반격");
            Counter();
            return true;
        }

        /// <summary>The test bed's F8 while the king is in his guard: a dummy's cut is on its way, so the guard waits for it
        /// (at most <paramref name="seconds"/> more). Only the test helper; a real cut has to come within the guard.</summary>
        public void HoldGuard(float seconds)
        {
            if (Piece == PieceKind.King && Stage == SfStage.Windup) guardUntil = Mathf.Max(guardUntil, StageTime + seconds);
        }

        public void ResetSkill()
        {
            Clear();
            ClearEdge();
            Clearance(null, false);
            cooldownLeft = CooldownTotal = 0f;
            edgeCooldownLeft = EdgeCooldownTotal = 0f;
            slowLeft = pinLeft = 0f;
            FallTime = -1f;
        }

        // ---------------------------------------------------------------- the step

        void FixedUpdate()
        {
            if (Fighter == null || S == null) return;
            float dt = Time.fixedDeltaTime;
            slowLeft -= dt;
            pinLeft -= dt;
            if (!Fighter.Alive || !Fighter.Authority)
            {
                if (Stage != SfStage.None) Clear();
                if (EdgeStage != SfEdge.None) ClearEdge();
                if (clearedFrom != null) Clearance(null, false);
                pressEdge = confirmEdge = cancelEdge = edgeKey = false;
                FallTime = -1f;
                EdgeCondition = false;
                return;
            }
            cooldownLeft -= dt;
            StageTime += dt;
            bool press = pressEdge, confirm = confirmEdge, cancel = cancelEdge, edge = edgeKey;
            pressEdge = confirmEdge = cancelEdge = edgeKey = false;
            TrackFall(dt);

            if (!HasSkill)
            {
                if (press || edge) Say($"{Pawn.DisplayName}: 폰은 스킬이 없어요");
                return;
            }
            // The edge skill (its own key and cooldown, R103) first: it is used falling, knocked down. While it runs the
            // F skill waits.
            if (StepEdge(edge, dt)) return;
            // Knocked down mid-skill: it ends here (the knight's leap goes on: it is already thrown).
            if (Stage != SfStage.None && Pawn.State != PawnState.Active && !(Piece == PieceKind.Knight && Stage == SfStage.Active))
            {
                bool started = Stage != SfStage.Aim;
                Raise(SfFxKind.Interrupted, null, Pawn.Hips.position, Vector3.zero, 0f);
                Say($"{Pawn.DisplayName}: {Name(Piece)} 끊김 (넘어짐)");
                Clear();
                if (started) StartCooldown();
                return;
            }
            // The aim follows the mouse until the skill goes; from then on its line, point and spots stay put.
            if (Stage == SfStage.None || Stage == SfStage.Aim) UpdateAim();

            switch (Stage)
            {
                case SfStage.None:
                    if (!press) break;
                    if (cooldownLeft > 0f) { Say($"{Pawn.DisplayName}: 쿨타임 {cooldownLeft:0.0}초"); break; }
                    if (Pawn.State != PawnState.Active) break;
                    if (Piece == PieceKind.King) { BeginKing(); break; }
                    SetStage(SfStage.Aim, "조준 (좌클릭 발동 · 우클릭 취소)");
                    break;
                case SfStage.Aim:
                    if (cancel || press) { Say($"{Pawn.DisplayName}: 조준 취소"); Clear(); break; }
                    if (confirm) Go();
                    break;
                default:
                    StepSkill(dt);
                    break;
            }
        }

        void SetStage(SfStage s, string detail)
        {
            Stage = s;
            StageTime = 0f;
            Detail = detail;
        }

        void Clear()
        {
            if (Pawn != null) Pawn.StopDash();
            PassThrough(false);
            Stage = SfStage.None;
            StageTime = 0f;
            Detail = "";
            hitSet.Clear();
            WaveFront = 0f;
            WaveBlocked = false;
            KingCountered = false;
            guardUntil = 0f;
        }

        void StartCooldown()
        {
            float c = S.testCooldown > 0f ? S.testCooldown : S.Cooldown(Piece);
            CooldownTotal = cooldownLeft = c;
        }

        void Finish()
        {
            Clear();
            StartCooldown();
        }

        // ---------------------------------------------------------------- aiming

        void UpdateAim()
        {
            Origin = Floor(Pawn.Hips.position);
            Vector3 dir = aimOverride ?? rawAim;
            AimDir = Flat(dir).sqrMagnitude > 1e-4f ? Flat(dir).normalized : Flat(Pawn.Facing).normalized;
            if (Piece == PieceKind.Queen) AimReach = DashDistance(Origin, AimDir, S.queenLength);
            else if (Piece == PieceKind.Rook) AimReach = WallDistance(Origin, AimDir, S.rookLength);
            if (Piece != PieceKind.Bishop && Piece != PieceKind.Knight) { AimValid = true; return; }

            float range = Piece == PieceKind.Bishop ? S.bishopRange : S.knightRange;
            Vector3 p;
            if (pointOverride.HasValue) p = pointOverride.Value;
            else
            {
                p = Origin + AimDir * range * 0.6f;
                var cam = aimCamera != null ? aimCamera : Camera.main;
                if (cam != null)
                {
                    var ray = new Ray(cam.transform.position, cam.transform.forward);
                    var plane = new Plane(Vector3.up, Origin);
                    if (plane.Raycast(ray, out float d) && d > 0f) p = ray.GetPoint(d);
                }
            }
            Vector3 off = Flat(p - Origin);
            float dist = Mathf.Clamp(off.magnitude, Piece == PieceKind.Knight ? S.knightMinRange : 0.5f, range);
            Vector3 along = off.sqrMagnitude > 1e-4f ? off.normalized : AimDir;
            AimPoint = Floor(Origin + along * dist);
            AimDir = along;
            AimValid = true;

            if (Piece == PieceKind.Knight)
            {
                float a = S.knightSpotAngle;
                KnightSpots[0] = Floor(AimPoint + Quaternion.AngleAxis(-a, Vector3.up) * along * S.knightSpotOffset);
                KnightSpots[1] = Floor(AimPoint + Quaternion.AngleAxis(a, Vector3.up) * along * S.knightSpotOffset);
            }
            else
            {
                // The enemy on the point is shown before the hands go (to both sides).
                PinCandidate = Nearest(AimPoint, S.bishopRadius);
            }
        }

        static bool HasFloor(Vector3 flatPoint)
        {
            var hits = Physics.RaycastAll(flatPoint + Vector3.up * 2f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (RagdollPawn.ColliderOwner.ContainsKey(h.collider)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                if (h.normal.y > 0.5f) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- using it

        void Go()
        {
            Dir = AimDir;
            Point = AimPoint;
            Uses++;
            Raise(SfFxKind.Cast, null, Origin, Dir, 0f);
            switch (Piece)
            {
                case PieceKind.Queen:
                    SetStage(SfStage.Windup, "예고");
                    Reach = DashDistance(Origin, Dir, S.queenLength);
                    Say($"{Pawn.DisplayName}: 꼬치 베기 예고 (돌진 {Reach:0.0} m)");
                    break;
                case PieceKind.Rook:
                    SetStage(SfStage.Windup, "예고 · 고정");
                    Reach = WallDistance(Origin, Dir, S.rookLength);
                    WaveBlocked = Reach < S.rookLength - 0.01f;
                    Say($"{Pawn.DisplayName}: 열린 파일 포격 예고" + (WaveBlocked ? $" (장애물 {Reach:0.0} m)" : ""));
                    break;
                case PieceKind.Bishop:
                    SetStage(SfStage.Windup, PinCandidate != null ? "예고 · 적 조준" : "예고");
                    Say($"{Pawn.DisplayName}: 관통 핀 예고" + (PinCandidate != null ? $" ({PinCandidate.DisplayName})" : ""));
                    break;
                case PieceKind.Knight:
                    KnightLeap();
                    break;
            }
            // A rooted piece cannot turn by walking: face the way it goes (standing, where it stands). Not the knight: it
            // is already thrown.
            if (Piece != PieceKind.Knight && Vector3.Angle(Flat(Pawn.Facing), Dir) > 35f && Pawn.Grounded)
                Pawn.Teleport(Pawn.Hips.position, Dir);
        }

        void BeginKing()
        {
            Uses++;
            Dir = Flat(Pawn.Facing).normalized;
            Raise(SfFxKind.Cast, null, Floor(Pawn.Hips.position), Dir, 0f);
            SetStage(SfStage.Windup, "받아내기");
            guardUntil = 0f;
            Say($"{Pawn.DisplayName}: 왕의 반격 자세");
        }

        void StepSkill(float dt)
        {
            switch (Piece)
            {
                case PieceKind.King: StepKing(); break;
                case PieceKind.Queen: StepQueen(); break;
                case PieceKind.Rook: StepRook(dt); break;
                case PieceKind.Bishop: StepBishop(); break;
                case PieceKind.Knight: StepKnight(); break;
            }
        }

        // ---------------------------------------------------------------- king

        void StepKing()
        {
            switch (Stage)
            {
                case SfStage.Windup:
                    if (StageTime >= Mathf.Max(S.kingGuard, guardUntil))
                    {
                        SetStage(SfStage.Recovery, "빈틈");
                        Raise(SfFxKind.KingWhiff, null, Pawn.Hips.position, Dir, 0f);
                        Say($"{Pawn.DisplayName}: 받아낼 칼이 없었음 → 빈틈 {S.kingWhiff:0.0}초");
                    }
                    break;
                case SfStage.Active:
                    if (StageTime >= S.kingCounterTime) SetStage(SfStage.Recovery, "후딜");
                    break;
                case SfStage.Recovery:
                    if (StageTime >= (KingCountered ? S.kingRecovery : S.kingWhiff)) Finish();
                    break;
            }
        }

        void Counter()
        {
            Vector3 c = Flat(Pawn.Hips.position);
            int n = 0;
            foreach (var other in All)
            {
                if (!IsEnemy(other)) continue;
                Vector3 d = Flat(other.Pawn.Hips.position) - c;
                if (d.magnitude > S.kingCounterRadius) continue;
                Vector3 dir = d.sqrMagnitude > 1e-4f ? d.normalized : Dir;
                Hit(other, dir, S.kingCounterPush, "왕의 반격", SfFxKind.KingCounterHit, n++);
            }
            Say($"{Pawn.DisplayName}: 반격 — 반경 {S.kingCounterRadius:0.0} m 안 적 {n}명 밀어냄");
        }

        // ---------------------------------------------------------------- queen

        void StepQueen()
        {
            switch (Stage)
            {
                case SfStage.Windup:
                    if (StageTime >= S.queenWindup)
                    {
                        // She goes herself: a dash on her feet down her line (thrown and landing at that speed she fell
                        // over), cutting every enemy her sword's tip reaches on the way, nearest first.
                        SetStage(SfStage.Active, "돌진");
                        DashStart = Pawn.Hips.position;
                        DashProgress = 0f;
                        PassThrough(true);
                        Pawn.BeginDash(Dir, S.queenDashSpeed);
                        Raise(SfFxKind.QueenThrust, null, Origin, Dir, Reach);
                        Say($"{Pawn.DisplayName}: 꼬치 베기 돌진 {Reach:0.0} m");
                    }
                    break;
                case SfStage.Active:
                {
                    DashProgress = Mathf.Max(DashProgress, Vector3.Dot(Flat(Pawn.Hips.position - DashStart), Dir));
                    Vector3 perp = Vector3.Cross(Vector3.up, Dir);
                    foreach (var (p, along) in InLine(Origin, Dir, 0.3f, Reach + S.queenHitAhead, S.queenWidth * 0.5f + 0.25f))
                    {
                        if (hitSet.Contains(p) || along > DashProgress + S.queenHitAhead) continue;
                        var other = p.GetComponent<SwordFightSkills>();
                        if (other == null) continue;
                        // Thrown on along her line and out to the side it stood on: she cuts through, they part.
                        float side = Vector3.Dot(Flat(p.Hips.position - Origin), perp) >= 0f ? 1f : -1f;
                        Vector3 dir = (Dir + perp * (side * S.queenSideShare)).normalized;
                        int n = hitSet.Count;
                        Hit(other, dir, S.queenPush[Mathf.Min(n, S.queenPush.Length - 1)], "꼬치 베기", SfFxKind.QueenHit, n);
                    }
                    // She plants her feet where the line ends (short of an edge); a wall or a fall ends it sooner. The
                    // pieces she passed stay passable until her recovery is over (they are flying off by then).
                    float brake = S.queenDashSpeed * Time.fixedDeltaTime * 1.5f;
                    if (DashProgress >= Reach - brake || !Pawn.OuterDashing || StageTime >= Reach / Mathf.Max(1f, S.queenDashSpeed) + 0.25f)
                    {
                        Pawn.StopDash(0.15f);
                        Raise(SfFxKind.QueenStop, null, Floor(Pawn.Hips.position), Dir, DashProgress);
                        Say($"{Pawn.DisplayName}: 돌진 끝 {DashProgress:0.0} m");
                        SetStage(SfStage.Recovery, "후딜");
                    }
                    break;
                }
                case SfStage.Recovery:
                    if (StageTime >= S.queenRecovery) Finish();
                    break;
            }
        }

        /// <summary>How far the queen can dash down a line: to its end, or short of the floor's edge or a wall.</summary>
        float DashDistance(Vector3 from, Vector3 dir, float length)
        {
            float best = Mathf.Min(length, WallDistance(from, dir, length) - 0.45f);
            for (float s = 0.25f; s <= length + 1e-3f; s += 0.25f)
                if (!HasFloor(Flat(from) + dir * s)) { best = Mathf.Min(best, s - S.queenEdgeMargin); break; }
            return Mathf.Max(0f, best);
        }

        // The dashing queen goes through the pieces she cuts instead of running into them.
        readonly List<(Collider, Collider)> ignored = new List<(Collider, Collider)>();

        void PassThrough(bool on)
        {
            foreach (var (a, b) in ignored) if (a != null && b != null) Physics.IgnoreCollision(a, b, false);
            ignored.Clear();
            if (!on || Pawn == null) return;
            var mine = Pawn.GetComponentsInChildren<Collider>();
            foreach (var other in All)
            {
                if (other == this || other.Pawn == null || other.Pawn.Team == Pawn.Team) continue;
                foreach (var c in other.Pawn.GetComponentsInChildren<Collider>())
                    foreach (var m in mine)
                    {
                        if (c == null || m == null || c.isTrigger || m.isTrigger) continue;
                        Physics.IgnoreCollision(m, c, true);
                        ignored.Add((m, c));
                    }
            }
        }

        // ---------------------------------------------------------------- rook

        void StepRook(float dt)
        {
            switch (Stage)
            {
                case SfStage.Windup:
                    if (StageTime >= S.rookWindup)
                    {
                        SetStage(SfStage.Active, "충격파");
                        WaveFront = 0.6f;
                        Raise(SfFxKind.RookSlam, null, Origin + Dir * 0.6f, Dir, Reach);
                    }
                    break;
                case SfStage.Active:
                {
                    float from = WaveFront;
                    WaveFront = Mathf.Min(Reach, WaveFront + S.rookWaveSpeed * dt);
                    foreach (var (p, along) in InLine(Origin, Dir, from, WaveFront, S.rookWidth * 0.5f + 0.25f))
                    {
                        var other = p.GetComponent<SwordFightSkills>();
                        if (other == null || hitSet.Contains(p)) continue;
                        // A tower comes up under it: thrown up as well as back (the queen's cut only slides them).
                        Hit(other, Dir, along <= S.rookNear ? S.rookPushNear : S.rookPushFar, "열린 파일 포격", SfFxKind.RookHit, hitSet.Count, S.rookLift, S.rookLiftCarry);
                    }
                    if (WaveFront >= Reach - 1e-3f)
                    {
                        if (WaveBlocked) Raise(SfFxKind.RookBlocked, null, Origin + Dir * Reach, Dir, 0f);
                        SetStage(SfStage.Recovery, "후딜");
                    }
                    break;
                }
                case SfStage.Recovery:
                    if (StageTime >= S.rookRecovery) Finish();
                    break;
            }
        }

        /// <summary>How far a line along the floor runs before something solid and upright stands in it.</summary>
        float WallDistance(Vector3 from, Vector3 dir, float length)
        {
            var hits = Physics.SphereCastAll(Flat(from) + Vector3.up * (from.y + 0.5f), 0.35f, dir, length, ~0, QueryTriggerInteraction.Ignore);
            float best = length;
            foreach (var h in hits)
            {
                if (h.distance <= 0f || RagdollPawn.ColliderOwner.ContainsKey(h.collider)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                if (Mathf.Abs(h.normal.y) > 0.5f) continue;
                best = Mathf.Min(best, h.distance);
            }
            return best;
        }

        // ---------------------------------------------------------------- bishop

        void StepBishop()
        {
            switch (Stage)
            {
                case SfStage.Windup:
                    if (StageTime >= S.bishopWindup)
                    {
                        // The hands go out on the diagonals: every enemy on the point is held by the ankles, wherever it
                        // stands (승규 님 10-10: "맵 전체에 가능하게"), and slowed for a while after.
                        SetStage(SfStage.Active, "손");
                        var taken = new List<SwordFightSkills>();
                        foreach (var other in All)
                            if (IsEnemy(other) && Flat(other.Pawn.Hips.position - Point).magnitude <= S.bishopRadius + 0.25f) taken.Add(other);
                        Raise(SfFxKind.BishopFire, null, Point, Dir, S.bishopRadius, taken.Count);
                        foreach (var other in taken)
                        {
                            other.slowLeft = S.bishopSlowTime;
                            other.pinLeft = S.bishopPinTime;
                            other.Fighter.StopCombat();
                            Raise(SfFxKind.BishopSlow, other.Pawn, other.Pawn.Hips.position, Dir, S.bishopSlowTime);
                            Raise(SfFxKind.BishopPin, other.Pawn, Floor(other.Pawn.Hips.position), Dir, S.bishopPinTime);
                            Say($"{Pawn.DisplayName} → {other.Pawn.DisplayName}: 묶임 {S.bishopPinTime:0.0}초 + 감속 {(1f - S.bishopSlow) * 100f:0}% {S.bishopSlowTime:0.0}초");
                        }
                        if (taken.Count == 0) Say($"{Pawn.DisplayName}: 관통 핀 — 잡힌 적 없음");
                    }
                    break;
                case SfStage.Active:
                    if (StageTime >= 0.1f) SetStage(SfStage.Recovery, "후딜");
                    break;
                case SfStage.Recovery:
                    if (StageTime >= S.bishopRecovery) Finish();
                    break;
            }
        }

        // ---------------------------------------------------------------- knight

        void KnightLeap()
        {
            SetStage(SfStage.Active, "도약");
            float t = S.knightAir, g = -Physics.gravity.y;
            Vector3 from = Pawn.Hips.position;
            Vector3 flat = Flat(Point - from);
            // Measured (probe "knight"): the standing body adds its own push to the throw and flies about 15% long.
            Vector3 v = flat / t * 0.875f + Vector3.up * ((Point.y - Origin.y) / t + 0.5f * g * t) * 1.02f;
            Pawn.Launch(v);
            Raise(SfFxKind.KnightLeap, null, Origin, Dir, flat.magnitude);
            Say($"{Pawn.DisplayName}: 포크 강하 {flat.magnitude:0.0} m");
        }

        void StepKnight()
        {
            switch (Stage)
            {
                case SfStage.Active:
                    bool landed = StageTime > 0.25f && (Pawn.Grounded || Pawn.State != PawnState.Active);
                    if (landed || StageTime > S.knightAir + 0.35f)
                    {
                        Vector3 land = Floor(Pawn.Hips.position);
                        Raise(SfFxKind.KnightLand, null, land, Dir, S.knightSpotRadius);
                        int n = 0;
                        foreach (var other in All)
                        {
                            if (!IsEnemy(other)) continue;
                            Vector3 o = Flat(other.Pawn.Hips.position);
                            bool inSpot = false;
                            foreach (var spot in KnightSpots) inSpot |= Flat(o - spot).magnitude <= S.knightSpotRadius + 0.2f;
                            if (!inSpot) continue;
                            Vector3 d = o - Flat(land);
                            Hit(other, d.sqrMagnitude > 1e-4f ? d.normalized : Dir, S.knightPush, "포크 강하", SfFxKind.KnightHit, n++);
                        }
                        Say($"{Pawn.DisplayName}: 착지 — 두 자리에서 적 {n}명");
                        SetStage(SfStage.Recovery, "후딜");
                    }
                    break;
                case SfStage.Recovery:
                    if (StageTime >= S.knightRecovery) Finish();
                    break;
            }
        }

        // ---------------------------------------------------------------- hits

        bool IsEnemy(SwordFightSkills other) =>
            other != null && other != this && other.Fighter != null && other.Fighter.Alive && other.Pawn != null
            && other.Pawn.Team != Pawn.Team && other.Fighter.Protection <= 0f;

        void Hit(SwordFightSkills other, Vector3 dir, float metres, string cause, SfFxKind kind, int order, float lift = -1f, float carry = 1f)
        {
            var target = other.Pawn;
            hitSet.Add(target);
            dir = Flat(dir).normalized;
            Vector3 push = dir * (metres * S.pushPerMetre * carry) + Vector3.up * (lift >= 0f ? lift : S.pushLift);
            target.TakeHit(push, S.knockdownSeconds, 0f, true);
            other.Fighter.StopCombat();
            other.pinLeft = 0f;
            if (other.Stage != SfStage.None && other.Stage != SfStage.Aim) other.StartCooldown();
            if (other.Stage != SfStage.None) other.Clear();
            Raise(kind, target, Vector3.Lerp(Pawn.bodies[(int)BodyId.Chest].position, target.bodies[(int)BodyId.Chest].position, 0.7f), dir, metres, order);
            Say($"{Pawn.DisplayName} → {target.DisplayName}: {cause} {metres:0.0} m 밀림");
        }

        /// <summary>Enemies along a line on the floor: from <paramref name="from"/> to <paramref name="to"/> metres ahead,
        /// within <paramref name="half"/> to the side, nearest first.</summary>
        List<(RagdollPawn p, float along)> InLine(Vector3 origin, Vector3 dir, float from, float to, float half)
        {
            var list = new List<(RagdollPawn, float)>();
            foreach (var other in All)
            {
                if (!IsEnemy(other)) continue;
                Vector3 d = Flat(other.Pawn.Hips.position - origin);
                float along = Vector3.Dot(d, dir);
                if (along <= from || along > to) continue;
                if ((d - dir * along).magnitude > half) continue;
                list.Add((other.Pawn, along));
            }
            list.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            return list;
        }

        RagdollPawn Nearest(Vector3 at, float radius)
        {
            RagdollPawn best = null;
            float bd = radius + 0.25f;
            foreach (var other in All)
            {
                if (!IsEnemy(other)) continue;
                float d = Flat(other.Pawn.Hips.position - at).magnitude;
                if (d < bd) { bd = d; best = other.Pawn; }
            }
            return best;
        }

        // ---------------------------------------------------------------- the sword's pose while a skill runs

        /// <summary>Where the blade points while a skill holds it (null = the sword is the match's).</summary>
        public Vector3? Blade
        {
            get
            {
                if (Pawn == null) return null;
                // An edge skill's own pose (the king's sword stays in the stone even while he is still getting up).
                if (EdgeStage != SfEdge.None && (Pawn.State == PawnState.Active || KingHanging)) return EdgeBlade();
                if (Pawn.State != PawnState.Active) return null;
                Vector3 f = Stage == SfStage.Aim ? AimDir : Dir;
                Vector3 side = Vector3.Cross(Vector3.up, f);   // the piece's right
                switch (Piece)
                {
                    case PieceKind.King when Stage == SfStage.Windup: return (-side + Vector3.up * 0.15f).normalized;
                    case PieceKind.King when Stage == SfStage.Active: return (f + side * 0.6f).normalized;
                    case PieceKind.Queen when Stage == SfStage.Windup: return (f + Vector3.up * 0.1f).normalized;
                    case PieceKind.Queen when Stage == SfStage.Active || Stage == SfStage.Recovery: return f;
                    case PieceKind.Rook when Stage == SfStage.Windup: return (Vector3.up - f * 0.35f).normalized;
                    case PieceKind.Rook when Stage == SfStage.Active || Stage == SfStage.Recovery: return (f - Vector3.up * 0.8f).normalized;
                    case PieceKind.Bishop when Stage == SfStage.Windup || Stage == SfStage.Active: return (f + Vector3.up * 0.3f).normalized;
                    case PieceKind.Knight when Stage == SfStage.Active: return (Vector3.up - f * 0.2f).normalized;
                    case PieceKind.Knight when Stage == SfStage.Recovery: return (f - Vector3.up * 0.8f).normalized;
                }
                return null;
            }
        }

        /// <summary>The blade in the piece's own colour while its skill winds up and goes (null = the sword's own).</summary>
        public Color? BladeTint
        {
            get
            {
                if (HasSkill && EdgeStage != SfEdge.None)
                {
                    var e = Colors(Piece);
                    return Color.Lerp(e.main, Color.white, 0.25f + 0.25f * Mathf.Sin(Time.time * 36f));
                }
                if (!HasSkill || (Stage != SfStage.Windup && Stage != SfStage.Active)) return null;
                var p = Colors(Piece);
                // Brighter toward the release, white in the windup's last frames (it is about to go).
                float k = Stage == SfStage.Active ? 1f : Mathf.Clamp01(StageTime / 0.25f);
                return Color.Lerp(p.main, p.light, 0.5f + 0.5f * Mathf.Sin(Time.time * 30f) * k);
            }
        }

        /// <summary>A piece's own colours in design A.</summary>
        public static SkillInkKit.Palette Colors(PieceKind kind) => kind switch
        {
            PieceKind.King => SkillInkKit.King,
            PieceKind.Queen => SkillInkKit.Queen,
            PieceKind.Rook => SkillInkKit.Rook,
            PieceKind.Bishop => SkillInkKit.Bishop,
            PieceKind.Knight => SkillInkKit.Knight,
            _ => SkillInkKit.Grey,
        };

        /// <summary>How far the hand is pulled back (−) or thrust out (+) along the skill's line.</summary>
        public float Reach01 => Piece == PieceKind.Queen ? Stage == SfStage.Windup ? -0.25f : Stage == SfStage.Active ? 0.35f : Stage == SfStage.Recovery ? 0.3f : 0f : 0f;

        // ---------------------------------------------------------------- small things

        public static string Name(PieceKind kind) => kind switch
        {
            PieceKind.King => "왕의 반격",
            PieceKind.Queen => "꼬치 베기",
            PieceKind.Rook => "열린 파일 포격",
            PieceKind.Bishop => "관통 핀",
            PieceKind.Knight => "포크 강하",
            _ => "(폰은 스킬 없음)",
        };

        void Raise(SfFxKind kind, RagdollPawn target, Vector3 at, Vector3 dir, float size, int order = 0)
            => Fx?.Invoke(new SfFxEvent { kind = kind, by = this, target = target, at = at, dir = dir, size = size, order = order });

        void Say(string text) => Log?.Invoke(this, text);

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>The floor under a point (not a piece), or the point at the pawn's feet height.</summary>
        Vector3 Floor(Vector3 p)
        {
            var hits = Physics.RaycastAll(Flat(p) + Vector3.up * (p.y + 1.5f), Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 found = new Vector3(p.x, Pawn != null ? Pawn.Hips.position.y - Pawn.standHeight : 0f, p.z);
            foreach (var h in hits)
            {
                if (RagdollPawn.ColliderOwner.ContainsKey(h.collider)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                if (h.normal.y < 0.5f || h.distance >= best) continue;
                best = h.distance;
                found = h.point;
            }
            return found;
        }
    }
}
