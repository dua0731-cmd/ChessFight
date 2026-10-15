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
        // R130, the king's counter: the spin cut a beat after the block, and an attacker thrown off by the block.
        KingCounterSpin, KingRepel,
    }

    /// <summary>R130: what the king's guard took (the block's effects answer each one: a cut, the queen's thrust or her
    /// edge cut, the rook's wave, the bishop's hands, the knight's dive or its kick).</summary>
    public enum SfBlock : byte { None, Sword, Queen, Rook, Bishop, Knight, QueenEdge, KnightEdge }

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
    /// Online in this scene since R121: the host runs every piece's skills, the clients draw them
    /// (<c>SwordFightSkills.Net.cs</c>, LabNetLink.Sword.cs). The match scene's own link still carries no skills.
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
        /// <summary>The knight (R129, the fork dive): the eight squares it attacks from the square it comes down on, and
        /// whether each has floor under it (one off the board is not attacked).</summary>
        public Vector3[] KnightSquares { get; } = new Vector3[8];
        public bool[] KnightSquareOn { get; } = new bool[8];
        public bool KingCountered { get; private set; }
        /// <summary>R130: what the king's guard took this time (None before it took anything).</summary>
        public SfBlock Blocked { get; private set; }
        /// <summary>R130: how far round the king's counter spin is (0..1; 0 until the beat after the block is over).</summary>
        public float CounterSpin => Piece == PieceKind.King && Stage == SfStage.Active && KingCountered && S != null
            ? Mathf.Clamp01((StageTime - S.kingCounterBeat) / Mathf.Max(0.05f, S.kingCounterTime)) : 0f;
        bool counterDone;
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
        public bool Parry(SwordFightPawn attacker, Vector3 at) =>
            attacker != null && Block(attacker.Skills, attacker.Pawn, at, SfBlock.Sword);

        /// <summary>The king is in his guard (the stance after F): the next attack on him is blocked and answered.</summary>
        public bool Guarding => Piece == PieceKind.King && Stage == SfStage.Windup && S != null && Fighter != null && Fighter.Alive;

        /// <summary>
        /// R130 (승규 님 10-13: "킹이 왕의 반격을 할 때 … 아 막고 반격한다! 라는 연출을 그리고 cc 기들을 실제로 막아내는
        /// 장면을 연출 해줘 … 각종 기물이 공격 할 때 반격 하는 걸 상호작용 하듯"): the king's guard takes any attack, not only a
        /// sword's cut: the queen's thrust and her edge cut, the bishop's hands, the knight's dive and its kick (R132, 승규 님:
        /// "왕은 룩의 F 스킬을 막지 못해" — not the rook's wave: it throws him as anyone). The attack does nothing to him; whoever came at him close is thrown off it at once (<see cref="Repel"/>);
        /// he turns to face it, holds a beat (<see cref="SwordFightSkillParams.kingCounterBeat"/>), then spins round once
        /// cutting everyone near him away (<see cref="Counter"/>). False when this piece is not a king in his guard.
        /// </summary>
        public bool Block(SwordFightSkills attacker, RagdollPawn attackerPawn, Vector3 at, SfBlock kind)
        {
            if (!Guarding) return false;
            KingCountered = true;
            Blocked = kind;
            counterDone = false;
            Vector3 from = attackerPawn != null ? attackerPawn.Hips.position : at;
            Vector3 toward = Flat(from - Pawn.Hips.position);
            if (toward.sqrMagnitude > 1e-4f) Dir = toward.normalized;
            Pawn.Face(Dir);
            Raise(SfFxKind.KingParry, attackerPawn, at, Dir, 0f, (int)kind);
            Say($"{Pawn.DisplayName}: 왕의 반격 — {(attackerPawn != null ? attackerPawn.DisplayName : "?")}의 {BlockName(kind)}을(를) 막음");
            SetStage(SfStage.Active, "반격");
            if (attacker != null && attackerPawn != null) Repel(attacker, attackerPawn, kind);
            return true;
        }

        static string BlockName(SfBlock kind) => kind switch
        {
            SfBlock.Sword => "칼",
            SfBlock.Queen => "꼬치 베기",
            SfBlock.Rook => "충격파",
            SfBlock.Bishop => "관통 핀(손)",
            SfBlock.Knight => "포크 강하",
            SfBlock.QueenEdge => "체크메이트 일섬",
            SfBlock.KnightEdge => "뒷발차기",
            _ => "공격",
        };

        /// <summary>The attacker thrown off the king's block where it came at him (R130): the queen's thrust stops dead and
        /// she is flung back (her edge cut much harder: she flies), the knight bounces off its dive or its kick. The bishop's
        /// hands come from afar: nothing comes back at them.</summary>
        void Repel(SwordFightSkills attacker, RagdollPawn pawn, SfBlock kind)
        {
            float metres, up;
            switch (kind)
            {
                // (The counter a beat later pushes it again if it is still near: these are kept short.)
                case SfBlock.Queen: metres = 1.6f; up = 1.4f; break;
                case SfBlock.QueenEdge: metres = 3.6f; up = 3.6f; break;
                case SfBlock.Knight: metres = 1.4f; up = 2.4f; break;
                case SfBlock.KnightEdge: metres = 1.6f; up = 1.8f; break;
                default: return;
            }
            Vector3 away = Flat(pawn.Hips.position - Pawn.Hips.position);
            away = away.sqrMagnitude > 1e-4f ? away.normalized : -Dir;
            pawn.StopDash(0f);
            if (pawn.OuterFlying) pawn.EndFlight();
            pawn.TakeHit(away * (metres * S.pushPerMetre) + Vector3.up * up, S.knockdownSeconds, 0f, true);
            attacker.Fighter.StopCombat();
            attacker.pinLeft = 0f;
            if (kind == SfBlock.Queen && attacker.Stage != SfStage.None)
            {
                attacker.PassThrough(false);
                attacker.SetStage(SfStage.Recovery, "막힘");
            }
            Raise(SfFxKind.KingRepel, pawn, pawn.bodies[(int)BodyId.Chest].position, away, metres, (int)kind);
            Say($"{pawn.DisplayName}: 왕의 반격에 튕겨 나감 ({metres:0.0} m)");
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
            // Online a client's copy only draws what the host sends (R121, SwordFightSkills.Net.cs).
            if (NetPuppet) return;
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
            // A leap, a dash or an edge flight lands its piece standing (RagdollPawn.SkillLanding, 10-11).
            if ((Stage != SfStage.None && Stage != SfStage.Aim) || EdgeStage != SfEdge.None) Pawn.KeepSkillLandingSafe();

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
            Blocked = SfBlock.None;
            counterDone = false;
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
            else if (Piece == PieceKind.Rook) AimReach = RookReach(Origin, AimDir, out _);
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

            if (Piece == PieceKind.Knight) KnightSquaresAt(AimPoint, along);
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
                    Reach = RookReach(Origin, Dir, out bool wall);
                    WaveBlocked = wall;
                    Say($"{Pawn.DisplayName}: 열린 파일 포격 예고" + (WaveBlocked ? $" (장애물 {Reach:0.0} m)" : ""));
                    break;
                case PieceKind.Bishop:
                    SetStage(SfStage.Windup, PinCandidate != null ? "예고 · 적 조준" : "예고");
                    Say($"{Pawn.DisplayName}: 관통 핀 예고" + (PinCandidate != null ? $" ({PinCandidate.DisplayName})" : ""));
                    break;
                case PieceKind.Knight:
                    KnightGo();
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
                {
                    // R130: the block holds a beat, then he spins round once, cutting everyone near him away.
                    if (!counterDone && StageTime >= S.kingCounterBeat)
                    {
                        counterDone = true;
                        Raise(SfFxKind.KingCounterSpin, null, Floor(Pawn.Hips.position), Dir, S.kingCounterRadius, (int)Blocked);
                        Counter();
                    }
                    float k = CounterSpin;
                    if (counterDone) Pawn.Face(Quaternion.AngleAxis(-360f * (k * k * (3f - 2f * k)), Vector3.up) * Dir);
                    if (StageTime >= S.kingCounterBeat + S.kingCounterTime + 0.04f)
                    {
                        Pawn.Face(Dir);
                        SetStage(SfStage.Recovery, "후딜");
                    }
                    break;
                }
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
                    // R129 (승규 님 10-13: "플레이어가 거리 조절을 실패하면 장 밖을 떨어져 나갈 수 있게"): past the floor's
                    // edge she does not plant her feet: the dash lets go with most of its speed and she sails out and falls.
                    if (DashProgress > 0.3f && !Pawn.Grounded && !HasFloor(Flat(Pawn.Hips.position)))
                    {
                        Pawn.StopDash(S.queenFallCarry);
                        Say($"{Pawn.DisplayName}: 꼬치 베기 — 가장자리 밖으로 달려 나감 ({DashProgress:0.0} m)");
                        SetStage(SfStage.Recovery, "후딜");
                        break;
                    }
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
                        // R130: a king in his guard stops her thrust dead (she is thrown back, her skill is over).
                        if (!Hit(other, dir, S.queenPush[Mathf.Min(n, S.queenPush.Length - 1)], "꼬치 베기", SfFxKind.QueenHit, n)) return;
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

        /// <summary>How far the queen dashes down a line: to its end, or short of a wall. Not short of the floor's edge
        /// (R129): aimed past it, she runs off it (<see cref="FloorDistance"/> says where the floor ends).</summary>
        float DashDistance(Vector3 from, Vector3 dir, float length)
        {
            float best = Mathf.Min(length, WallDistance(from, dir, length) - 0.45f);
            return Mathf.Max(0f, best);
        }

        /// <summary>How far the floor goes on along a line (to <paramref name="length"/> at most): where it first ends.</summary>
        public static float FloorDistance(Vector3 from, Vector3 dir, float length, float start = 0.25f)
        {
            for (float s = start; s <= length + 1e-3f; s += 0.25f)
                if (!HasFloor(Flat(from) + dir * s)) return Mathf.Max(0f, s - 0.25f);
            return length;
        }

        /// <summary>How far the rook's wave runs: to its end, a wall, or the floor's edge (R129: its squares and towers
        /// no longer hang out over the void past the edge). <paramref name="wall"/>: a wall stops it first.</summary>
        float RookReach(Vector3 from, Vector3 dir, out bool wall)
        {
            float walls = WallDistance(from, dir, S.rookLength), floor = FloorDistance(from, dir, S.rookLength, 0.6f);
            wall = walls < S.rookLength - 0.01f && walls <= floor;
            return Mathf.Max(1.2f, Mathf.Min(walls, floor - 0.1f));
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
                        // R132: a king's guard does not stop it (BlockOf).
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
                        // R130: a king in his guard cuts the hands that come for him (no pin, no slow: he counters).
                        taken.RemoveAll(o => o.Block(this, Pawn, Floor(o.Pawn.Hips.position), SfBlock.Bishop));
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

        // R129 (승규 님 10-13: "나이트의 F 스킬을 아예 다른 연출과 액션으로 해줘 (포크 강하라는 이름에 맞게)"): the knight
        // springs high, hangs a beat at the top (the squares it forks show), then dives straight down onto the aimed square.
        // A chess fork: from the square it lands on, every enemy on one of the eight squares a knight attacks (an L: two
        // squares one way, one the other; the squares next to it are jumped over) is struck at once, and whoever stands on
        // the square itself is stamped down. Its flight is the skill's own (RagdollPawn.Fly), not a throw: the same arc on
        // every PC and every time, landing exactly on the square.

        /// <summary>The eight knight moves (across to the right, along the aim) in board squares.</summary>
        public static readonly Vector2Int[] KnightMoves =
        {
            new Vector2Int(1, 2), new Vector2Int(2, 1), new Vector2Int(2, -1), new Vector2Int(1, -2),
            new Vector2Int(-1, -2), new Vector2Int(-2, -1), new Vector2Int(-2, 1), new Vector2Int(-1, 2),
        };

        Vector3 knightFrom, knightLand, knightApex;

        /// <summary>The eight squares a knight on <paramref name="point"/> facing <paramref name="along"/> attacks.</summary>
        public void KnightSquaresAt(Vector3 point, Vector3 along)
        {
            if (S == null) return;
            along = Flat(along).sqrMagnitude > 1e-4f ? Flat(along).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, along);
            for (int i = 0; i < KnightMoves.Length; i++)
            {
                Vector3 p = point + (right * KnightMoves[i].x + along * KnightMoves[i].y) * S.knightCell;
                KnightSquareOn[i] = HasFloor(Flat(p));
                KnightSquares[i] = Floor(p);
            }
        }

        /// <summary>
        /// Where the knight's hips are <paramref name="t"/> s into its dive, and how far it is tipped forward (degrees):
        /// a spring up and on to the top (fast off the floor, slowing into it), a beat at the top tipping over, then a dive
        /// gathering speed straight down onto the square. The same arithmetic draws its path while it aims.
        /// </summary>
        public static Vector3 KnightFlight(SwordFightSkillParams S, Vector3 from, Vector3 land, Vector3 dir, float t, out float tip)
        {
            Vector3 mid = Vector3.Lerp(Flat(from), Flat(land), S.knightApexAt);
            var apex = new Vector3(mid.x, Mathf.Max(from.y, land.y) + S.knightApex, mid.z);
            Vector3 drift = apex + Flat(dir).normalized * 0.12f;
            float up = Mathf.Max(0.05f, S.knightRise), hold = Mathf.Max(0f, S.knightHover), dive = Mathf.Max(0.05f, S.knightDive);
            if (t < up)
            {
                float u = t / up, eh = 1f - (1f - u) * (1f - u), ev = 1f - (1f - u) * (1f - u) * (1f - u);
                tip = -18f * u;   // rearing back as it springs
                return new Vector3(Mathf.Lerp(from.x, apex.x, eh), Mathf.Lerp(from.y, apex.y, ev), Mathf.Lerp(from.z, apex.z, eh));
            }
            if (t < up + hold)
            {
                float h = (t - up) / Mathf.Max(1e-4f, hold);
                tip = Mathf.Lerp(-18f, 38f, h * h * (3f - 2f * h));   // tipping over into the dive
                return Vector3.Lerp(apex, drift, h) + Vector3.up * (0.05f * Mathf.Sin(h * Mathf.PI));
            }
            float k = Mathf.Clamp01((t - up - hold) / dive);
            tip = Mathf.Lerp(38f, 6f, k);
            return Vector3.Lerp(drift, land, k * k);   // gathering speed all the way down
        }

        /// <summary>The whole flight, take-off to impact (s).</summary>
        public float KnightTime => S != null ? S.knightRise + S.knightHover + S.knightDive : 0.6f;

        void KnightGo()
        {
            SetStage(SfStage.Active, "도약");
            knightFrom = Pawn.Hips.position;
            knightLand = Point + Vector3.up * (Pawn.standHeight + 0.03f);
            KnightSquaresAt(Point, Dir);
            Pawn.Face(Dir);
            // It comes down through the pieces under it instead of onto their heads (they are thrown off as it lands).
            PassThrough(true);
            int on = 0;
            foreach (bool b in KnightSquareOn) on += b ? 1 : 0;
            Raise(SfFxKind.KnightLeap, null, Origin, Dir, Flat(Point - Origin).magnitude);
            Say($"{Pawn.DisplayName}: 포크 강하 — {Flat(Point - Origin).magnitude:0.0} m 앞 칸으로 (노리는 칸 {on}개)");
        }

        void StepKnight()
        {
            switch (Stage)
            {
                case SfStage.Active:
                {
                    float dt = Time.fixedDeltaTime;
                    if (StageTime < KnightTime)
                    {
                        Vector3 pos = KnightFlight(S, knightFrom, knightLand, Dir, StageTime, out float tip);
                        Vector3 next = KnightFlight(S, knightFrom, knightLand, Dir, StageTime + dt, out _);
                        Pawn.Fly(pos, (next - pos) / dt, tip, default, 26f);
                        break;
                    }
                    Pawn.EndFlight(Vector3.down * 3.5f);
                    KnightImpact();
                    SetStage(SfStage.Recovery, "후딜");
                    break;
                }
                case SfStage.Recovery:
                    if (StageTime >= S.knightRecovery) Finish();
                    break;
            }
        }

        /// <summary>The landing: the square under it stamped, then the fork: the eight squares at once.</summary>
        void KnightImpact()
        {
            Vector3 land = Floor(Pawn.Hips.position);
            Raise(SfFxKind.KnightLand, null, land, Dir, S.knightCell);
            int n = 0, stamped = 0;
            foreach (var other in All)
            {
                if (!IsEnemy(other)) continue;
                Vector3 d = Flat(other.Pawn.Hips.position - land);
                if (d.magnitude > S.knightStomp || Mathf.Abs(other.Pawn.Hips.position.y - Pawn.Hips.position.y) > 1.2f) continue;
                Hit(other, d.sqrMagnitude > 1e-4f ? d.normalized : Dir, S.knightStompPush, "포크 강하 · 내리찍기", SfFxKind.KnightHit, n++);
                stamped++;
            }
            int forked = 0;
            foreach (var other in All)
            {
                if (!IsEnemy(other) || hitSet.Contains(other.Pawn)) continue;
                Vector3 o = Flat(other.Pawn.Hips.position);
                for (int i = 0; i < KnightMoves.Length; i++)
                {
                    if (!KnightSquareOn[i] || Flat(o - KnightSquares[i]).magnitude > S.knightSquare + 0.15f) continue;
                    if (Mathf.Abs(other.Pawn.Hips.position.y - Pawn.standHeight - KnightSquares[i].y) > 1.4f) continue;
                    Vector3 d = o - Flat(land);
                    Hit(other, d.sqrMagnitude > 1e-4f ? d.normalized : Dir, S.knightPush, "포크 강하 · 포크", SfFxKind.KnightHit, n++);
                    forked++;
                    break;
                }
            }
            Say($"{Pawn.DisplayName}: 내리꽂음 — 착지 칸 {stamped}명 · L자 칸 {forked}명" + (forked >= 2 ? " (포크!)" : ""));
        }

        // ---------------------------------------------------------------- hits

        bool IsEnemy(SwordFightSkills other) =>
            other != null && other != this && other.Fighter != null && other.Fighter.Alive && other.Pawn != null
            && other.Pawn.Team != Pawn.Team && other.Fighter.Protection <= 0f;

        /// <summary>A skill's hit on <paramref name="other"/>. False if a king's guard blocked it (R130: nothing happens to
        /// him, the attacker may be thrown off; the king counters).</summary>
        bool Hit(SwordFightSkills other, Vector3 dir, float metres, string cause, SfFxKind kind, int order, float lift = -1f, float carry = 1f)
        {
            var target = other.Pawn;
            hitSet.Add(target);
            var block = BlockOf(kind);
            if (block != SfBlock.None && other.Block(this, Pawn, Vector3.Lerp(Pawn.bodies[(int)BodyId.Chest].position, target.bodies[(int)BodyId.Chest].position, 0.75f), block))
                return false;
            dir = Flat(dir).normalized;
            Vector3 push = dir * (metres * S.pushPerMetre * carry) + Vector3.up * (lift >= 0f ? lift : S.pushLift);
            target.TakeHit(push, S.knockdownSeconds, 0f, true);
            other.Fighter.StopCombat();
            other.pinLeft = 0f;
            if (other.Stage != SfStage.None && other.Stage != SfStage.Aim) other.StartCooldown();
            if (other.Stage != SfStage.None) other.Clear();
            Raise(kind, target, Vector3.Lerp(Pawn.bodies[(int)BodyId.Chest].position, target.bodies[(int)BodyId.Chest].position, 0.7f), dir, metres, order);
            Say($"{Pawn.DisplayName} → {target.DisplayName}: {cause} {metres:0.0} m 밀림");
            return true;
        }

        /// <summary>Which of a king's blocks a skill's hit is (R130); the king's own counter is never blocked, nor (R132,
        /// 승규 님: "왕은 룩의 F 스킬을 막지 못해") the rook's wave.</summary>
        static SfBlock BlockOf(SfFxKind kind) => kind switch
        {
            SfFxKind.QueenHit => SfBlock.Queen,
            SfFxKind.RookHit => SfBlock.None,
            SfFxKind.KnightHit => SfBlock.Knight,
            SfFxKind.KnightKickHit => SfBlock.KnightEdge,
            SfFxKind.KingCounterHit => SfBlock.None,
            _ => SfBlock.Sword,
        };

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
                    case PieceKind.King when Stage == SfStage.Active:
                    {
                        // R130: held up across his front through the block's beat, then swept once round with the spin.
                        float k = CounterSpin;
                        if (k <= 0f) return (side + Vector3.up * 0.55f + f * 0.25f).normalized;
                        return Quaternion.AngleAxis(-360f * (k * k * (3f - 2f * k)), Vector3.up) * (f + side * 0.6f).normalized;
                    }
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
                    var e = SwordFightSkillFx.CardColors(Piece);
                    return Color.Lerp(e.main, Color.white, 0.25f + 0.25f * Mathf.Sin(Time.time * 36f));
                }
                if (!HasSkill || (Stage != SfStage.Windup && Stage != SfStage.Active)) return null;
                var p = SwordFightSkillFx.CardColors(Piece);
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
