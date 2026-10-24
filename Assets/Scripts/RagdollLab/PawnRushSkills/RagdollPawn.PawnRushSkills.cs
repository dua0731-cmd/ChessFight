using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public enum SkillStage : byte
    {
        None = 0,
        Windup = 1,     // the warning the others see (the bishop: aiming while the key is held)
        Active = 2,     // the move itself: a step, the charge, the leap, the thread in flight
        Link = 3,       // the pawn only: between its two steps
        Recovery = 4,   // stuck for a moment after it
    }

    /// <summary>A moment a skill lands, for the test bed's hit effects (PawnRushSkillFx).</summary>
    public enum SkillFxKind : byte
    {
        QueenBlast, QueenHit,
        RookHit, RookStop, RookWall, RookBarricade,
        BishopWire, BishopTrip,
        KnightStomp, KnightLand,
        KnightTurn, KnightHome,
        KnightLeap, RookSlam,
        PawnStep, PawnHit, PawnHelp,
        // R146: the king's call, by the king on the one called (count: its place; size: the seconds it has, or takes).
        KingCall, KingCallGo, KingCallLand, KingCallEnd,
        // R153: the rook's chain (thrown; on a piece: count 0 an enemy hooked, 1 a teammate offered the handle, 2 a teammate
        // that grabbed it; the pull's end: count 1 a teammate; broken), and a skill key pressed with nothing to aim at (the
        // pawn's second step, the knight's leap: count = the piece).
        RookThrow, RookHook, RookPullEnd, RookChainBreak, SkillFail,
    }

    public struct SkillFxEvent
    {
        public SkillFxKind kind;
        public RagdollPawn by, target;
        /// <summary>Where it happened: the ground under a blast or a landing, the contact of a hit.</summary>
        public Vector3 at, dir;
        /// <summary>The face it happened on, pointing out of it: the wall or barricade a charge met (zero if none).</summary>
        public Vector3 normal;
        /// <summary>The rook's hit number, the queen's pieces hit, a trip's number.</summary>
        public int count;
        /// <summary>A radius or a length (the queen's ring, the bishop's line).</summary>
        public float size;
        /// <summary>The thing the effect lasts as long as (the bishop's wire).</summary>
        public UnityEngine.Object source;
    }

    /// <summary>
    /// The Pawn Rush piece skills (Docs/Skills/DESIGN.md §3-7; R153 the skill plan v0.3, 승규 님 10-16): pawn "first two
    /// steps" (the second homes onto a downed teammate or an enemy), knight "fork leap" (only at an enemy, onto its head),
    /// bishop "guarded X trap" (only while the bishop stays near it), rook "straight chain pull" (RagdollPawn.RookChain.cs),
    /// queen "ring shove". The king (R146 "왕의 부름", RagdollPawn.KingCall.cs) is out of Pawn Rush since R153; its code
    /// stays, and the rook's pull moves a piece its way.
    ///
    /// Off unless something hands the pawn its numbers (<see cref="PawnRushSkills"/>): only the Pawn Rush skill
    /// test scene does, so Queen of the Hill, Sword Fight and the network match run exactly as before. The skill
    /// key is fed in separately (<see cref="SetSkillInput"/>; F in the test scene, R74). The rook and the bishop aim
    /// first: the key starts the aim, the left click (the dive's button) fires, the right click or the key again
    /// calls it off.
    /// The previz numbers stay as they are; a step, a charge or a leap only never makes the piece slower than it
    /// was going (D2). Offline only for now: the network packets do not carry the skill key.
    /// </summary>
    public partial class RagdollPawn
    {
        /// <summary>The skills' numbers; null = this pawn has no Pawn Rush skills (every other scene).</summary>
        public PawnRushSkillParams PawnRushSkills { get; set; }

        /// <summary>Every result a skill produces, for the test bed's log.</summary>
        public static event Action<RagdollPawn, string> SkillLog;

        /// <summary>Every moment a skill lands, for the test bed's hit effects; nothing else listens.</summary>
        public static event Action<SkillFxEvent> SkillFx;

        /// <summary>Raise a skill's moment from outside a pawn: a tripwire's, or (R111) one a network client was sent.</summary>
        public static void RaiseSkillFx(SkillFxEvent e) => SkillFx?.Invoke(e);

        void Fx(SkillFxKind kind, RagdollPawn target, Vector3 at, Vector3 dir, int count = 0, float size = 0f, Vector3 normal = default)
            => SkillFx?.Invoke(new SkillFxEvent { kind = kind, by = this, target = target, at = at, dir = dir, count = count, size = size, normal = normal });

        Vector3 SkillContact(RagdollPawn other) =>
            Vector3.Lerp(bodies[(int)BodyId.Chest].position, other.bodies[(int)BodyId.Chest].position, 0.5f);

        SkillStage skillStage;
        float stageTime, skillCooldownLeft, cooldownTotal, skillGrace;
        bool skillPressed, skillConfirm, skillCancel, skillGrabRaw, skillGrabLatch, aimLocked;
        Vector3 skillMoveRaw;
        // A dash (the pawn's steps): a fixed speed along a fixed line for a while.
        bool dashing;
        Vector3 skillDir = Vector3.forward;
        float dashBase, dashLeft;
        int pawnStep;
        bool pawnBuffered, pawnStepHit, pawnHelped;
        // R153: the one the second step goes for (a downed teammate or a standing enemy in front), found during the first.
        RagdollPawn pawnTarget;
        float pawnFailAt = -10f;
        readonly HashSet<RagdollPawn> skillHitSet = new HashSet<RagdollPawn>();
        readonly Collider[] skillOverlap = new Collider[48];
        // Getting up: skills only stagger for a moment (§2.4); the pawn's help makes it longer (R153: no more haste).
        float getUpGuardLeft, pendingGuard;
        PawnState skillSeenState = PawnState.Active;
        // Knight (R153): the enemy it would leap at (found every step while idle), the one it flies at, where it comes down
        float knightAir, knightFlight, knightAcross;
        bool knightTapped;
        Vector3 knightSpot;
        RagdollPawn knightCandidate, knightTarget;
        // Bishop
        Vector3 bishopPoint, bishopYaw = Vector3.forward, bishopFrom;
        bool bishopValid;
        float bishopFlight;
        // Telegraphs
        LineRenderer markA, markB, markC, markD, markE;
        readonly LineRenderer[] markPegs = new LineRenderer[4];
        MeshRenderer markLanding, markLandingInner;
        float blastFlash;

        public SkillStage SkillStage => skillStage;
        public float SkillStageTime => stageTime;
        public float SkillCooldown => Mathf.Max(0f, skillCooldownLeft);
        public float SkillCooldownTotal => cooldownTotal;
        public float GetUpGuardLeft => Mathf.Max(0f, getUpGuardLeft);
        public bool SkillDashing => dashing;
        public int SkillUses { get; private set; }
        public string SkillDetail { get; private set; } = "";
        public string LastSkillHit { get; internal set; } = "-";

        /// <summary>A rook or a bishop between its key and the left click.</summary>
        public bool SkillAiming => Aiming;
        public bool BishopAiming => piece == PieceKind.Bishop && skillStage == SkillStage.Windup;
        public bool BishopAimValid => bishopValid;
        public Vector3 BishopAimPoint => bishopPoint;
        public Vector3 BishopAimYaw => bishopYaw;
        /// <summary>The enemy the knight flies at, or (idle) the one its key would leap at; null: its key does nothing.</summary>
        public RagdollPawn KnightMarked => knightTarget != null ? knightTarget : knightCandidate;
        /// <summary>The knight is in its leap at an enemy (every PC: the host sends the target).</summary>
        public RagdollPawn KnightTarget => knightTarget;
        /// <summary>The one the pawn's second step would go for (null: the second press does nothing).</summary>
        public RagdollPawn PawnTarget => pawnTarget;
        /// <summary>The pawn's step under way (1 or 2; every PC: the host sends it).</summary>
        public int PawnStepNumber => pawnStep;
        /// <summary>On the floor, or just off it (the coyote time).</summary>
        public bool OnTheFloor => OnFloor;
        /// <summary>The knight's landing is to be shown: in its leap, until it has come down on the head.</summary>
        public bool KnightShowsLanding => piece == PieceKind.Knight && skillStage == SkillStage.Active && !knightTapped;
        public Vector3 KnightLandingSpot => knightSpot;
        /// <summary>The bishop's throw: where its X leaves from and how long it flies.</summary>
        public Vector3 BishopThrowFrom => bishopFrom;
        public float BishopThrowTime => bishopFlight;
        /// <summary>Set by the effects when they draw the skills' telegraphs themselves (design A, R90): the plain
        /// lines (SkillMarks) are not drawn then.</summary>
        public static bool SkillMarksHidden { get; set; }

        bool Aiming => skillStage == SkillStage.Windup && !aimLocked
                       && (QueenHillSkills != null ? QueenHillAims : piece == PieceKind.Rook || piece == PieceKind.Bishop
                           || piece == PieceKind.King);   // R146: the king's list, its clicks the list's

        /// <summary>A skill is moving this pawn into others, or has just stopped doing so: contacts between
        /// pawns are the skill's to judge, not the collision knockdown's.</summary>
        bool SkillShielded => dashing || skillGrace > 0f;


        public static string SkillName(PieceKind kind) => kind switch
        {
            PieceKind.Pawn => "첫 두 걸음",
            PieceKind.Knight => "포크 도약",
            PieceKind.Bishop => "지키는 X자 덫",
            PieceKind.Rook => "직선 사슬 견인",
            PieceKind.Queen => "팔방 밀치기",
            PieceKind.King => "왕의 부름",
            _ => "",
        };

        /// <summary>The skill key went down this frame (kept until the next physics step reads it). The aim's
        /// left and right clicks come in with the pawn's own input (the dive and the grab).</summary>
        public void SetSkillInput(bool pressed) => skillPressed |= pressed;

        /// <summary>Drop whatever skill is under way and start the cooldown over (the test bed's piece switch).</summary>
        public void ResetSkill()
        {
            ClearSkills();
            skillCooldownLeft = cooldownTotal = 0f;
            bindLeft = 0f;
        }

        void Log(string text) => SkillLog?.Invoke(this, text);

        /// <summary>This bishop's wire tripped <paramref name="target"/> (SkillTripwire).</summary>
        public string SkillTrip(RagdollPawn target, Vector3 push) => SkillHit(target, push, true, "교차 밧줄");

        // ---------------------------------------------------------------- tied up (R138)

        // R138 (승규 님 10-14: "비숍이 스킬을 사용 할 때 밧줄에 걸려서 넘어 지는 게 아니라 밧줄에 묶이게 해줘 (실제 밧줄에 묶이는게
        // 잘 보여야 돼)"): a piece the bishop's wire catches is tied up where it stands, not tripped: held on the spot, its
        // arms bound to its sides, its keys doing nothing, until the rope lets go (or a knockdown frees it).
        float bindLeft, bindTotal;
        Vector3 bindAt;

        /// <summary>Tied up by a bishop's rope now (R138; the host's own pieces: a client's puppet does not know).</summary>
        public bool SkillBound => bindLeft > 0f;
        public float SkillBindLeft => Mathf.Max(0f, bindLeft);
        public float SkillBindTotal => bindTotal;

        /// <summary>This bishop's wire ties <paramref name="target"/> up for <paramref name="seconds"/> (R138, SkillTripwire): it
        /// stands where it was caught. A piece already down is not tied; one just up from a fall only staggers.</summary>
        public string SkillBind(RagdollPawn target, float seconds, string cause)
        {
            if (target == null || target.NetworkPuppet) return "-";
            if (target.SkillUntargetable)
            {
                // On its way in to a king (R146), just respawned or on the finish pad (R153): nothing takes.
                target.LastSkillHit = $"{cause}: 스킬 대상 아님";
                return "대상 아님";
            }
            string result;
            if (target.State != PawnState.Active)
                result = "넘어져 있어 안 묶임";
            else if (target.getUpGuardLeft > 0f)
            {
                target.Stagger(PawnRushSkills != null ? PawnRushSkills.guardStagger : 0.3f);
                result = "기상 보호 → 휘청";
            }
            else
            {
                target.BeBound(seconds);
                result = $"묶임 {seconds:0.0}초";
            }
            target.LastSkillHit = $"{cause}: {result}";
            return result;
        }

        /// <summary>The rope that tied it is gone (its wire broken or replaced): free at once (R138).</summary>
        public void SkillUnbind() => bindLeft = 0f;

        void BeBound(float seconds)
        {
            bindLeft = bindTotal = Mathf.Max(0.1f, seconds);
            bindAt = bodies[0].position;
            LetGo(0.3f);
            if (hookPhase == HookPhase.Charging) DropHook();
            if (PawnRushSkills != null && skillStage != SkillStage.None)
            {
                // Tied mid-skill: it ends here, and the cooldown runs as if it had finished.
                Log($"{DisplayName}: {SkillName(piece)} 끊김 (묶임)");
                bool firedAlready = piece == PieceKind.Bishop && skillStage == SkillStage.Active;
                ClearSkills();
                if (!firedAlready) StartCooldown(PieceCooldown(PawnRushSkills));
            }
        }

        /// <summary>Tied up (R138): pulled back to where the rope caught it and held there, standing.</summary>
        void HoldBound(float dt)
        {
            Vector3 own = Flat(bodies[0].linearVelocity) - carryVel;
            Vector3 want = Vector3.ClampMagnitude(Flat(bindAt - bodies[0].position) * 8f, 2f);
            AddVelocity(want - own);
            anchorVel = want + carryVel;
            Vector3 hp = bodies[0].position;
            anchorPos = new Vector3(hp.x + anchorVel.x * dt, anchorPos.y, hp.z + anchorVel.z * dt);
            anchor.MovePosition(anchorPos);
        }

        // ---------------------------------------------------------------- the step hooks

        /// <summary>Before the jump and the run: a skill that roots the piece takes the keys away.</summary>
        void PreSkills()
        {
            if (PawnRushSkills == null) return;
            skillMoveRaw = input.move;
            skillGrabRaw = input.grab;
            HoldKeysForKingCall();   // R146: on its way in to its king
            ChainGrabKey();          // R153: a rook's chain handle held out to it (the right click), or riding the chain
            if (Aiming)
            {
                // Aiming: the left click (the dive) fires the skill and the right click (the grab) calls it off.
                skillConfirm |= input.shove;
                skillCancel |= input.grab && !skillGrabLatch;
                input.shove = false;
            }
            // A right click that was down when the aim began, or that called it off, grabs nothing until let go.
            if (skillGrabLatch && !input.grab) skillGrabLatch = false;
            if (Aiming || skillGrabLatch) input.grab = false;
            if (bindLeft > 0f)
            {
                // Tied up (R138): the keys still arrive, they just do nothing.
                input.move = Vector3.zero;
                input.jump = false;
                input.sprint = false;
                input.shove = false;
                input.shoveHeld = false;
                input.grab = false;
            }
            if (QueenHillSkills != null)
            {
                PreQueenHillSkills();   // the Queen of the Hill skills take it from here (R89)
                return;
            }
            // Aiming, the rook walks (and jumps) as usual but cannot sprint; from the click to the end it stays put (R153).
            if (Aiming && piece == PieceKind.Rook) input.sprint = false;
            bool rooted = dashing || skillStage == SkillStage.Recovery
                          || (skillStage == SkillStage.Windup && (piece == PieceKind.Queen || (piece == PieceKind.Rook && aimLocked)))
                          || (piece == PieceKind.Rook && skillStage == SkillStage.Active);
            if (!rooted) return;
            input.move = Vector3.zero;
            input.jump = false;
            input.sprint = false;
            input.shove = false;
        }

        /// <summary>After the run: the skills set the body's speed on top of what the run asked for.</summary>
        void UpdateSkills(float dt)
        {
            var s = PawnRushSkills;
            if (s == null) return;
            bool press = skillPressed, confirm = skillConfirm, cancel = skillCancel;
            skillPressed = skillConfirm = skillCancel = false;
            skillCooldownLeft -= dt;
            getUpGuardLeft -= dt;
            skillGuardLeft -= dt;
            skillGrace -= dt;
            blastFlash -= dt;
            if (bindLeft > 0f)
            {
                // Tied up (R138): held on the spot until the rope lets go; a knockdown (or the water) frees it at once.
                bindLeft -= dt;
                if (State != PawnState.Active || Floating) bindLeft = 0f;
                else HoldBound(dt);
            }
            TrackGetUp(s);
            if (QueenHillSkills != null)
            {
                UpdateQueenHillSkills(QueenHillSkills, press, confirm, cancel, dt);   // Queen of the Hill skills (R89)
                return;
            }
            // R146: a king's call answered (its own key), or the trip in to him.
            press = UpdateKingCallMate(s, press, dt);
            // R153: pulled in on a rook's chain.
            if (kingCarry || UpdateChainCarry(s, dt))
            {
                DrawSkillMarks(s);
                return;
            }

            if (skillStage != SkillStage.None && (State == PawnState.Ragdoll || Floating))
            {
                // Floored (or in the water) mid-skill: it ends here, and the cooldown runs as if it had finished.
                Log($"{DisplayName}: {SkillName(piece)} 끊김 ({(Floating ? "물" : "넘어짐")})");
                bool firedAlready = piece == PieceKind.Bishop && skillStage == SkillStage.Active;
                ClearSkills();
                if (!firedAlready) StartCooldown(PieceCooldown(s));
            }

            switch (piece)
            {
                case PieceKind.Pawn: UpdatePawnSkill(s, press, dt); break;
                case PieceKind.Knight: UpdateKnightSkill(s, press, dt); break;
                case PieceKind.Bishop: UpdateBishopSkill(s, press, confirm, cancel, dt); break;
                case PieceKind.Rook: UpdateRookChain(s, press, confirm, cancel, dt); break;
                case PieceKind.Queen: UpdateQueenSkill(s, press, dt); break;
                case PieceKind.King: UpdateKingCall(s, press, cancel, dt); break;
            }
            DrawSkillMarks(s);
        }

        void ClearSkills()
        {
            skillStage = SkillStage.None;
            stageTime = 0f;
            dashing = false;
            skillGrace = 0f;
            pawnBuffered = pawnStepHit = false;
            pawnStep = 0;
            pawnTarget = null;
            skillPressed = skillConfirm = skillCancel = false;
            aimLocked = false;
            knightTapped = false;
            knightTarget = knightCandidate = null;
            SkillDetail = "";
            HideSkillMarks();
            ClearQueenHill();
            ClearKingCall();
            ClearRookChain();
        }

        float PieceCooldown(PawnRushSkillParams s) => piece switch
        {
            PieceKind.Pawn => s.pawnCooldown,
            PieceKind.Knight => s.knightCooldown,
            PieceKind.Bishop => s.bishopCooldown,
            PieceKind.Rook => s.rookCooldown,
            PieceKind.Queen => s.queenCooldown,
            PieceKind.King => s.kingCooldown,
            _ => 0f,
        };

        void StartCooldown(float seconds)
        {
            // The test bed's flat cooldown (2 s for quick tries) stands in for every piece's own.
            if (PawnRushSkills != null && PawnRushSkills.testCooldown > 0f) seconds = PawnRushSkills.testCooldown;
            cooldownTotal = Mathf.Max(0f, seconds);
            skillCooldownLeft = cooldownTotal;
        }

        /// <summary>Free to start a skill: on the ground, or anywhere for one that works in the air (the rook, the knight).</summary>
        bool CanStartSkill(bool inAir = false) =>
            State == PawnState.Active && !Climbing && rope == null && !Floating && !BeingHeld && !Squashed && !Staggered && bindLeft <= 0f
            && !chainCarry
            && (hookPhase == HookPhase.None || hookPhase == HookPhase.Held) && skillCooldownLeft <= 0f
            && (inAir || Grounded || coyote > 0f);

        bool OnFloor => Grounded || coyote > 0f;

        static Vector3 FlatDir(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        void TrackGetUp(PawnRushSkillParams s)
        {
            if (skillSeenState == PawnState.GettingUp && State == PawnState.Active)
            {
                getUpGuardLeft = pendingGuard > 0f ? pendingGuard : s.getUpGuard;
                pendingGuard = 0f;
            }
            skillSeenState = State;
        }

        Vector3 AimFlat()
        {
            Vector3 a = Flat(input.aim);
            return a.sqrMagnitude > 1e-4f ? a.normalized : Flat(facing).normalized;
        }

        Vector3 MoveOrAim()
        {
            Vector3 m = Flat(skillMoveRaw);
            return m.sqrMagnitude > 0.04f ? m.normalized : AimFlat();
        }

        bool IsAllyOf(RagdollPawn other) => team != Teams.None && other.team == team;
        bool IsEnemyOf(RagdollPawn other) => Teams.AreEnemies(team, other.team);
        bool DownedForHelp => State == PawnState.Ragdoll && !Diving;

        /// <summary>Angle between the line the other piece faces along (front or back alike) and the way this
        /// one came in, 0..90 degrees (DESIGN §3.2).</summary>
        static float AxisAngle(RagdollPawn other, Vector3 dir)
        {
            Vector3 f = Flat(other.facing);
            if (f.sqrMagnitude < 1e-4f) f = Flat(other.bodies[0].transform.forward);
            float a = Vector3.Angle(f, dir);
            return a > 90f ? 180f - a : a;
        }

        /// <summary>What a skill does to the piece it hits (DESIGN §2.3-2.4): a knockdown is the skill's to
        /// give, not the impact's; a piece that is down is only pushed, one just up only staggers.</summary>
        string SkillHit(RagdollPawn target, Vector3 push, bool knockdown, string cause)
        {
            if (target == null || target.NetworkPuppet) return "-";
            if (target.SkillUntargetable)
            {
                // On its way in to a king (R146), just respawned or on the finish pad (R153): nothing takes.
                target.LastSkillHit = $"{cause}: 스킬 대상 아님";
                return "대상 아님";
            }
            var cover = target.Cover();
            if (cover != null)
            {
                // A Queen of the Hill king its guards stand round: the nearest one takes the hit for it.
                target.LastSkillHit = $"{cause}: {cover.DisplayName}이(가) 대신 막음";
                SkillHit(cover, push, knockdown, cause);
                return $"{cover.DisplayName}이(가) 대신 막음";
            }
            if (target.wardLeft > 0f && target.State != PawnState.Ragdoll)
            {
                // Guarded by a Queen of the Hill king (R89): no knockdown, a much smaller push (TakeHit scales it).
                target.TakeHit(Flat(push), 0f, 0f, false);
                target.Stagger(target.QueenHillSkills != null ? target.QueenHillSkills.wardStagger : 0.2f);
                target.LastSkillHit = $"{cause}: 호위로 버팀";
                return "호위로 버팀";
            }
            float guardStagger = PawnRushSkills != null ? PawnRushSkills.guardStagger : 0.3f;
            string result;
            if (knockdown && target.State == PawnState.Ragdoll)
            {
                target.AddVelocity(push * target.PushScale);
                result = "이미 넘어져 있어 밀림만";
            }
            else if (knockdown && target.getUpGuardLeft > 0f)
            {
                target.TakeHit(Flat(push), 0f, 0f, false);
                target.Stagger(guardStagger);
                result = "기상 보호 → 휘청";
            }
            else if (knockdown)
            {
                target.Knockdown(cause);
                target.AddVelocity(push * target.PushScale);
                result = "넘어짐";
            }
            else
            {
                target.TakeHit(push, 0f, 0f, false);
                result = "밀림";
            }
            // A piece in the air on a thrown arc (a knight's leap) flies on from the new speed.
            if (target.launched) target.carryVel += Flat(push * target.PushScale);
            target.LastSkillHit = $"{cause}: {result}";
            return result;
        }

        // ---------------------------------------------------------------- dashes

        void StartDash(float speed, float seconds)
        {
            // D2: never slower than it was going along the line.
            float along = Vector3.Dot(Flat(bodies[0].linearVelocity) - carryVel, skillDir);
            dashBase = Mathf.Max(speed, along);
            dashLeft = seconds;
            dashing = true;
            facing = FlatDir(skillDir);
        }

        void DriveDash(float dt)
        {
            Vector3 own = Flat(bodies[0].linearVelocity) - carryVel;
            Vector3 want = skillDir * dashBase;
            AddVelocity(want - own);
            anchorVel = want + carryVel;
            Vector3 hp = bodies[0].position;
            anchorPos = new Vector3(hp.x + anchorVel.x * dt, anchorPos.y, hp.z + anchorVel.z * dt);
            anchor.MovePosition(anchorPos);
            facing = skillDir;
        }

        void EndDash()
        {
            if (!dashing) return;
            dashing = false;
            skillGrace = 0.3f;
        }

        /// <summary>Stopped dead (a wall): the speed goes, the anchor stays on the body.</summary>
        void HaltDash()
        {
            EndDash();
            Vector3 own = Flat(bodies[0].linearVelocity) - carryVel;
            AddVelocity(-own);
            anchorVel = carryVel;
            Vector3 hp = bodies[0].position;
            anchorPos = new Vector3(hp.x, anchorPos.y, hp.z);
            anchor.MovePosition(anchorPos);
        }

        /// <summary>Something solid and upright right ahead of the chest (pieces and loose bodies do not count).</summary>
        bool WallAhead(float distance, out RaycastHit wall)
        {
            wall = default;
            Vector3 from = bodies[(int)BodyId.Chest].position - skillDir * 0.1f;
            int n = Physics.SphereCastNonAlloc(from, 0.28f, skillDir, hits, distance + 0.1f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.distance <= 0f || ownSet.Contains(h.collider) || ColliderOwner.ContainsKey(h.collider) || PassesThrough(h.collider)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                if (Mathf.Abs(h.normal.y) > 0.5f) continue;   // floor, ramp
                if (h.distance >= best) continue;
                best = h.distance;
                wall = h;
                found = true;
            }
            return found;
        }

        // ---------------------------------------------------------------- pawn: first two steps (§3)

        void UpdatePawnSkill(PawnRushSkillParams s, bool press, float dt)
        {
            switch (skillStage)
            {
                case SkillStage.None:
                    if (press && CanStartSkill())
                    {
                        SkillUses++;
                        pawnHelped = false;
                        PawnStep(s, 1);
                    }
                    break;
                case SkillStage.Active:
                    if (pawnStep == 1)
                    {
                        if (press) pawnBuffered = true;
                        pawnTarget = SeekTarget(s);
                    }
                    else SteerToTarget();
                    if (WallAhead(dashBase * dt + 0.25f, out _)) dashLeft = 0f;   // a wall ends the step, no stagger
                    else
                    {
                        DriveDash(dt);
                        PawnStepHits(s);
                        dashLeft -= dt;
                    }
                    if (dashLeft <= 0f)
                    {
                        EndDash();
                        if (pawnStep == 1)
                        {
                            skillStage = SkillStage.Link;
                            stageTime = 0f;
                            SkillDetail = pawnTarget != null ? $"연결 (F 한 번 더 → {pawnTarget.DisplayName})" : "연결 (대상 없음)";
                        }
                        else FinishPawn(s, true);
                    }
                    break;
                case SkillStage.Link:
                    stageTime += dt;
                    // R153 (plan v0.3 §3): the second step only goes for someone: a downed teammate or a standing enemy in front.
                    pawnTarget = SeekTarget(s);
                    SkillDetail = pawnTarget != null ? $"연결 (F 한 번 더 → {pawnTarget.DisplayName})" : "연결 (대상 없음)";
                    if (press) pawnBuffered = true;
                    if (pawnBuffered && stageTime >= s.pawnLinkMin && State == PawnState.Active && !Climbing)
                    {
                        if (pawnTarget != null) PawnStep(s, 2);
                        else
                        {
                            // Nothing to go for: the press is ignored (a short "no" sound), the link keeps waiting.
                            pawnBuffered = false;
                            if (Time.time - pawnFailAt > 0.25f)
                            {
                                pawnFailAt = Time.time;
                                Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Pawn);
                                Log("폰: 두 번째 걸음 — 대상 없음 (무시)");
                            }
                        }
                    }
                    else if (stageTime > s.pawnLinkMax) FinishPawn(s, false);
                    break;
            }
        }

        /// <summary>The one the second step would go for: within pawnSeekAngle either side of the aim and pawnSeekRange, a
        /// downed teammate or a standing enemy, the nearest to the aim.</summary>
        RagdollPawn SeekTarget(PawnRushSkillParams s)
        {
            Vector3 aim = AimFlat(), me = bodies[0].position;
            RagdollPawn best = null;
            float bestAngle = s.pawnSeekAngle;
            foreach (var other in All)
            {
                if (other == null || other == this || other.SkillUntargetable || other.chainCarry) continue;
                bool ally = IsAllyOf(other);
                if (ally ? !other.DownedForHelp : !IsEnemyOf(other) || other.State != PawnState.Active) continue;
                Vector3 d = other.bodies[0].position - me;
                if (Mathf.Abs(d.y) > 1.2f || Flat(d).magnitude > s.pawnSeekRange) continue;
                float angle = Vector3.Angle(aim, Flat(d));
                if (angle > bestAngle) continue;
                best = other;
                bestAngle = angle;
            }
            return best;
        }

        /// <summary>The second step bends onto its target as it goes.</summary>
        void SteerToTarget()
        {
            if (pawnTarget == null || pawnStepHit) return;
            Vector3 d = Flat(pawnTarget.bodies[0].position - bodies[0].position);
            if (d.sqrMagnitude > 0.01f) skillDir = d.normalized;
        }

        void PawnStep(PawnRushSkillParams s, int n)
        {
            pawnStep = n;
            pawnBuffered = pawnStepHit = false;
            skillHitSet.Clear();
            float distance = s.pawnStepDistance;
            if (n == 2 && pawnTarget != null)
            {
                // Toward the target, at most pawnSeekStep; it stops on touching it.
                Vector3 d = Flat(pawnTarget.bodies[0].position - bodies[0].position);
                skillDir = d.sqrMagnitude > 0.01f ? d.normalized : MoveOrAim();
                distance = Mathf.Min(s.pawnSeekStep, d.magnitude + 0.3f);
            }
            else skillDir = MoveOrAim();
            StartDash(s.pawnStepSpeed, distance / Mathf.Max(0.1f, s.pawnStepSpeed));
            skillStage = SkillStage.Active;
            stageTime = 0f;
            SkillDetail = n == 1 ? "첫 걸음" : $"두 번째 걸음 → {(pawnTarget != null ? pawnTarget.DisplayName : "?")}";
            Fx(SkillFxKind.PawnStep, pawnTarget, FeetPoint, skillDir, n);
        }

        void FinishPawn(PawnRushSkillParams s, bool bothSteps)
        {
            skillStage = SkillStage.None;
            SkillDetail = "";
            float cooldown = bothSteps ? s.pawnCooldown : s.pawnCooldownOneStep;
            if (pawnHelped) cooldown -= s.helpCooldownCut;
            StartCooldown(cooldown);
        }

        void PawnStepHits(PawnRushSkillParams s)
        {
            Vector3 at = bodies[(int)BodyId.Chest].position + skillDir * 0.3f;
            int n = Physics.OverlapSphereNonAlloc(at, s.pawnHitRadius, skillOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!ColliderOwner.TryGetValue(skillOverlap[i], out var other) || other == null || other == this
                    || skillHitSet.Contains(other)) continue;
                if (IsAllyOf(other))
                {
                    if (!other.DownedForHelp) continue;
                    skillHitSet.Add(other);
                    HelpUp(s, other);
                    // The second step stops on the teammate it went for.
                    if (pawnStep == 2 && other == pawnTarget) dashLeft = Mathf.Min(dashLeft, 0.02f);
                    continue;
                }
                if (pawnStepHit || !IsEnemyOf(other) || other.SkillUntargetable) continue;
                skillHitSet.Add(other);
                pawnStepHit = true;
                float angle = AxisAngle(other, skillDir);
                bool diagonal = angle > s.pawnFrontAngle && angle <= s.pawnDiagonalAngle;
                string result = diagonal
                    ? SkillHit(other, skillDir * s.pawnFrontPush + Vector3.up * 1.5f, true, "폰 대각")
                    : SkillHit(other, skillDir * s.pawnFrontPush, false, "폰 정면");
                string kind = diagonal ? "대각" : angle > s.pawnDiagonalAngle ? "바로 옆" : "곧게";
                Fx(SkillFxKind.PawnHit, other, SkillContact(other), skillDir, diagonal ? 1 : 0);
                Log($"폰 {pawnStep}걸음 → {other.DisplayName}: {kind} {angle:0}° → {result}");
                // The step stops on the piece it hit; the pawn itself takes nothing back (폰 불이익 없음).
                dashLeft = Mathf.Min(dashLeft, 0.02f);
            }
        }

        void HelpUp(PawnRushSkillParams s, RagdollPawn ally)
        {
            if (ally.State == PawnState.Ragdoll) ally.BeginGetUp(ally.P);
            ally.pendingGuard = s.helpGuard;
            pawnHelped = true;
            Fx(SkillFxKind.PawnHelp, ally, ally.bodies[0].position, skillDir);
            Log($"폰 → {ally.DisplayName}: 부축 — 바로 일어남, 기상 보호 {s.helpGuard:0.#}초, 내 쿨 −{s.helpCooldownCut:0.#}초");
        }

        // ---------------------------------------------------------------- knight: fork leap (R153, plan v0.3 §7)

        void UpdateKnightSkill(PawnRushSkillParams s, bool press, float dt)
        {
            switch (skillStage)
            {
                case SkillStage.None:
                    // The enemy its key would leap at, every step (its icon lights only with one, its ring shows over it).
                    knightCandidate = State == PawnState.Active ? FindKnightTarget(s, input.aim) : null;
                    if (!press) break;
                    if (!CanStartSkill(true)) break;
                    if (knightCandidate == null)
                    {
                        // Nothing to leap at: the key is ignored (a short "no" sound), no cooldown.
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Knight);
                        Log("나이트: 대상 없음 (무시)");
                        break;
                    }
                    SkillUses++;
                    knightTarget = knightCandidate;
                    knightCandidate = null;
                    if (!OnFloor)
                    {
                        // In the air too, with a target: it leaps on from where it is, at once.
                        KnightLeap(s);
                        break;
                    }
                    skillStage = SkillStage.Windup;
                    stageTime = 0f;
                    SkillDetail = $"준비 (뒷발로 섬) → {knightTarget.DisplayName}";
                    break;
                case SkillStage.Windup:
                    // Not rooted: a running knight keeps its speed into the leap (D2).
                    stageTime += dt;
                    if (stageTime >= s.knightWindup) KnightLeap(s);
                    break;
                case SkillStage.Active:
                {
                    stageTime += dt;
                    knightAir += dt;
                    if (knightTarget != null && !knightTapped) SteerKnight(s, dt);
                    KnightStomp(s);
                    if (Climbing || rope != null)
                    {
                        // Caught a ledge on the way: the leap ends there, no landing shock.
                        skillStage = SkillStage.None;
                        SkillDetail = "";
                        knightTarget = null;
                        StartCooldown(s.knightCooldown);
                    }
                    else if (knightAir > 0.2f && Grounded)
                    {
                        KnightLand(s);
                        skillStage = SkillStage.Recovery;
                        stageTime = 0f;
                        SkillDetail = "착지 후딜";
                        knightCandidate = knightTarget = null;
                    }
                    break;
                }
                case SkillStage.Recovery:
                    stageTime += dt;
                    if (stageTime >= s.knightLandRecovery)
                    {
                        skillStage = SkillStage.None;
                        SkillDetail = "";
                        StartCooldown(s.knightCooldown);
                    }
                    break;
            }
        }

        /// <summary>The enemy a knight's key would leap at, looking along <paramref name="aim"/>: standing, within
        /// knightTargetAngle either side of the aim, knightTargetMin..Max across the ground, knightTargetHeight up or down, not
        /// hidden behind a wall; the one nearest the aim (the middle of the screen). Any PC can ask (the knight's own screen
        /// shows it before the host answers).</summary>
        public RagdollPawn FindKnightTarget(PawnRushSkillParams s, Vector3 aim)
        {
            if (s == null) return null;
            Vector3 flatAim = Flat(aim);
            flatAim = flatAim.sqrMagnitude > 1e-4f ? flatAim.normalized : Flat(facing).normalized;
            Vector3 me = bodies[0].position, eye = bodies[(int)BodyId.Head].position;
            RagdollPawn best = null;
            float bestAngle = s.knightTargetAngle;
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable
                    || other.chainCarry || other.kingCarry) continue;
                Vector3 d = other.bodies[0].position - me;
                float across = Flat(d).magnitude;
                if (across < s.knightTargetMin || across > s.knightTargetMax || Mathf.Abs(d.y) > s.knightTargetHeight) continue;
                float angle = Vector3.Angle(flatAim, Flat(d));
                if (angle > bestAngle) continue;
                if (!ClearSight(eye, other.bodies[(int)BodyId.Head].position)) continue;
                best = other;
                bestAngle = angle;
            }
            return best;
        }

        /// <summary>Nothing solid (pieces and loose things do not count) between two points.</summary>
        bool ClearSight(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float length = d.magnitude;
            if (length < 0.05f) return true;
            int n = Physics.RaycastNonAlloc(from, d / length, hits, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || ownSet.Contains(c) || ColliderOwner.ContainsKey(c) || PassesThrough(c)) continue;
                var rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                return false;
            }
            return true;
        }

        /// <summary>The leap at the target's head: the time it takes from the distance (knightFlightNear..Far), the speed to
        /// come down on its head at the end of it.</summary>
        void KnightLeap(PawnRushSkillParams s)
        {
            bool fromFloor = OnFloor;   // (the launch below takes it off the floor)
            var target = knightTarget;
            float g = Mathf.Max(0.01f, -Physics.gravity.y);
            Vector3 p = bodies[0].position;
            knightSpot = target != null ? StompPoint(target) : p + AimFlat() * s.knightTargetMax;
            float across = Flat(knightSpot - p).magnitude;
            float u = Mathf.InverseLerp(s.knightTargetMin, s.knightTargetMax, across);
            knightFlight = Mathf.Lerp(s.knightFlightNear, s.knightFlightFar, u);
            skillDir = Flat(knightSpot - p).sqrMagnitude > 1e-4f ? Flat(knightSpot - p).normalized : AimFlat();
            knightAcross = across / Mathf.Max(0.2f, knightFlight);
            Launch(LeapVelocity(p, knightSpot, knightFlight, s.knightHeight));
            facing = skillDir;
            knightAir = 0f;
            knightTapped = false;
            skillHitSet.Clear();
            skillStage = SkillStage.Active;
            stageTime = 0f;
            SkillDetail = target != null ? $"도약 → {target.DisplayName}" : "도약";
            Fx(SkillFxKind.KnightLeap, target, p - Vector3.up * standHeight, skillDir, fromFloor ? 0 : 1);
            if (target != null) Fx(SkillFxKind.KnightHome, target, knightSpot, skillDir);
            Log($"나이트: {(target != null ? target.DisplayName : "?")}에게 도약 ({across:0.0} m, 체공 {knightFlight:0.00}초)");
        }

        /// <summary>The speed that takes the hips from <paramref name="from"/> to <paramref name="to"/> in <paramref name="time"/>
        /// seconds under gravity, the top kept within <paramref name="height"/> over the higher end.</summary>
        static Vector3 LeapVelocity(Vector3 from, Vector3 to, float time, float height)
        {
            float g = Mathf.Max(0.01f, -Physics.gravity.y);
            time = Mathf.Max(0.2f, time);
            Vector3 across = Flat(to - from) / time;
            float vy = (to.y - from.y + 0.5f * g * time * time) / time;
            float top = Mathf.Max(from.y, to.y) + height;
            float most = Mathf.Sqrt(2f * g * Mathf.Max(0.05f, top - from.y));
            return across + Vector3.up * Mathf.Min(vy, most);
        }

        /// <summary>In the air the spot it comes down on moves toward where its target is now, at most knightCorrection m/s;
        /// the flight is worked out again each step for the time it has left (a target that dodges far enough is missed).</summary>
        void SteerKnight(PawnRushSkillParams s, float dt)
        {
            var target = knightTarget;
            if (target != null && target.State == PawnState.Active && !target.SkillUntargetable && !target.chainCarry)
            {
                Vector3 want = StompPoint(target);
                Vector3 move = Flat(want - knightSpot);
                float step = s.knightCorrection * dt;
                if (move.sqrMagnitude > step * step) move = move.normalized * step;
                knightSpot += move;
                knightSpot.y = Mathf.MoveTowards(knightSpot.y, want.y, s.knightCorrection * dt);
            }
            float left = knightFlight - knightAir;
            float most = knightAcross + s.knightCorrection;
            if (left < 0.12f)
            {
                // The last moment, and past its time: across the ground only, onto its spot (a miss comes down where it
                // was going, not past it).
                // (Not a fresh launch: that would keep the feet from ever counting as landed.)
                if (OnFloor) return;
                Vector3 onto = Flat(knightSpot - bodies[0].position) / Mathf.Max(0.08f, left);
                if (onto.sqrMagnitude > most * most) onto = onto.normalized * most;
                AddVelocity(onto - Flat(bodies[0].linearVelocity));
                if (launched) carryVel = anchorVel = onto;
                return;
            }
            // Never faster across the ground than the leap's own speed plus the correction (a far dodge is missed).
            Vector3 v = LeapVelocity(bodies[0].position, knightSpot, left, s.knightHeight);
            Vector3 flat = Flat(v);
            if (flat.sqrMagnitude > most * most) v = flat.normalized * most + Vector3.up * v.y;
            Launch(v);
        }

        void KnightStomp(PawnRushSkillParams s)
        {
            if (knightTapped) return;
            Vector3 feet = bodies[0].position - Vector3.up * standHeight;
            float vy = bodies[0].linearVelocity.y;
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.State == PawnState.Ragdoll || skillHitSet.Contains(other)) continue;
                // R129: judged against the head, closing on each other counts; the target a little more readily.
                bool locked = other == knightTarget;
                float closing = vy - other.bodies[0].linearVelocity.y;
                if (locked ? closing > 1.5f : vy > 1f || closing > -1f) continue;
                var headCollider = other.bodies[(int)BodyId.Head].GetComponent<Collider>();
                Vector3 head = other.bodies[(int)BodyId.Head].position;
                float top = headCollider != null ? headCollider.bounds.max.y : head.y + 0.12f;
                float reach = s.knightStompReach + (locked ? 0.25f : 0.1f);
                if (Flat(feet - head).magnitude > reach || feet.y < top - (locked ? 0.45f : 0.25f) || feet.y > top + (locked ? 0.5f : 0.35f)) continue;
                skillHitSet.Add(other);
                string result = SkillHit(other, Flat(facing) * 1f + Vector3.down * 1f, true, "나이트 머리 찍기");
                Fx(SkillFxKind.KnightStomp, other, new Vector3(head.x, top, head.z), Flat(facing));
                // A tap on the head, up off it, then down right beside it.
                knightTapped = true;
                knightTarget = null;
                Launch(Flat(facing) * s.knightStompHop + Vector3.up * s.knightStompBounce);
                knightSpot = PredictLanding(bodies[0].position, bodies[0].linearVelocity, Mathf.Max(0.01f, -Physics.gravity.y));
                SkillDetail = "머리 찍고 착지";
                Log($"나이트 → {other.DisplayName}: 머리 찍기 → {result}, 바로 옆에 착지");
                return;
            }
        }

        /// <summary>Where the hips must be for the feet to come down on top of <paramref name="target"/>'s head.</summary>
        Vector3 StompPoint(RagdollPawn target)
        {
            var head = target.bodies[(int)BodyId.Head];
            var col = head.GetComponent<Collider>();
            float top = col != null ? col.bounds.max.y : head.position.y + 0.12f;
            return new Vector3(head.position.x, top - 0.05f + standHeight, head.position.z);
        }

        /// <summary>The landing: only after a head was taken, the other enemies close by stagger (the fork); a miss comes down
        /// on its last spot with nothing (plan v0.3 §7.2). Either way a bishop's wire under it snaps.</summary>
        void KnightLand(PawnRushSkillParams s)
        {
            Vector3 feet = bodies[0].position - Vector3.up * standHeight;
            int staggered = 0;
            if (knightTapped)
                foreach (var other in All)
                {
                    if (other == null || other == this || !IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable) continue;
                    Vector3 d = other.bodies[0].position - bodies[0].position;
                    if (Flat(d).magnitude > s.knightLandRadius || Mathf.Abs(d.y) > 1f) continue;
                    if (other.Stagger(s.knightLandStagger))
                    {
                        staggered++;
                        other.LastSkillHit = "나이트 포크: 휘청";
                    }
                }
            int wires = SkillTripwire.BreakNear(feet, s.knightLandRadius, team);
            Fx(SkillFxKind.KnightLand, null, feet, Flat(facing), staggered, s.knightLandRadius);
            Log(knightTapped
                ? $"나이트: 착지 — 포크 {staggered}명 휘청 {s.knightLandStagger:0.##}초" + (wires > 0 ? $" · 밧줄 {wires}개 파괴" : "")
                : "나이트: 빗나감 — 마지막 착지점에 착지 (휘청 없음)" + (wires > 0 ? $" · 밧줄 {wires}개 파괴" : ""));
        }

        // ---------------------------------------------------------------- bishop: crossed tripwire (§5)

        void UpdateBishopSkill(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (skillStage)
            {
                case SkillStage.None:
                    if (press && CanStartSkill())
                    {
                        skillStage = SkillStage.Windup;
                        stageTime = 0f;
                        skillGrabLatch = skillGrabRaw;
                        SkillDetail = "조준 (마우스로 위치 · 좌클릭 설치 · 우클릭/F 취소)";
                        BishopAim(s);
                    }
                    break;
                case SkillStage.Windup:
                    // The see-through X follows the mouse within the range until the left click.
                    stageTime += dt;
                    BishopAim(s);
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        ClearSkills();
                        Log("비숍: 조준 취소");
                    }
                    else if (confirm)
                    {
                        if (!bishopValid)
                        {
                            Log("비숍: 여기에는 깔 수 없어요 (바닥이 없거나 너무 가파름)");
                            break;
                        }
                        SkillUses++;
                        StartCooldown(s.bishopCooldown);
                        bishopFrom = bodies[(int)BodyId.Head].position + Vector3.up * 0.2f;
                        float range = Flat(bishopPoint - bodies[0].position).magnitude;
                        bishopFlight = Mathf.Max(0.08f, s.bishopFlight * Mathf.Clamp01(range / Mathf.Max(0.1f, s.bishopRange)));
                        skillStage = SkillStage.Active;
                        stageTime = 0f;
                        SkillDetail = "실이 날아가는 중";
                    }
                    break;
                case SkillStage.Active:
                    stageTime += dt;
                    if (stageTime >= bishopFlight)
                    {
                        SkillTripwire.Spawn(this, bishopPoint, bishopYaw, s);
                        Log($"비숍: X자 밧줄 설치 ({Flat(bishopPoint - bodies[0].position).magnitude:0.0} m) — {s.bishopArm:0.#}초 뒤 무장");
                        skillStage = SkillStage.None;
                        SkillDetail = "";
                    }
                    break;
            }
        }

        /// <summary>Where the camera's aim meets the floor within range; past the range, the floor under the
        /// range's end. The X lies along the way the bishop looks (no turning it, v0.1).</summary>
        void BishopAim(PawnRushSkillParams s)
        {
            Vector3 head = bodies[(int)BodyId.Head].position;
            Vector3 aim = input.aim.sqrMagnitude > 1e-4f ? input.aim.normalized : facing;
            bishopYaw = AimFlat();
            float minUp = Mathf.Cos(s.bishopMaxSlope * Mathf.Deg2Rad);
            bishopValid = false;
            if (SolidRay(head, aim, s.bishopRange + 4f, out var hit) && hit.normal.y >= minUp
                && Flat(hit.point - bodies[0].position).magnitude <= s.bishopRange)
            {
                bishopPoint = hit.point;
                bishopValid = true;
                return;
            }
            // Looking level or up: the end of the range, dropped to the floor.
            float reach = s.bishopRange;
            if (SolidRay(head, bishopYaw, s.bishopRange, out var wall)) reach = Mathf.Max(0.5f, wall.distance - 0.3f);
            Vector3 far = head + bishopYaw * reach;
            if (SolidRay(far + Vector3.up * 1.5f, Vector3.down, 8f, out var floor) && floor.normal.y >= minUp)
            {
                bishopPoint = floor.point;
                bishopValid = true;
            }
        }

        // ---------------------------------------------------------------- rook: straight chain pull (R153) — RagdollPawn.RookChain.cs

        // ---------------------------------------------------------------- queen: ring shove (§7)

        void UpdateQueenSkill(PawnRushSkillParams s, bool press, float dt)
        {
            switch (skillStage)
            {
                case SkillStage.None:
                    if (press && CanStartSkill())
                    {
                        SkillUses++;
                        skillStage = SkillStage.Windup;
                        stageTime = 0f;
                        SkillDetail = "예고 (빛이 조여듦)";
                    }
                    break;
                case SkillStage.Windup:
                    stageTime += dt;
                    if (stageTime >= s.queenWindup)
                    {
                        QueenBlast(s);
                        skillStage = SkillStage.Recovery;
                        stageTime = 0f;
                        SkillDetail = "후딜 (검에 끌려 돎)";
                    }
                    break;
                case SkillStage.Recovery:
                    stageTime += dt;
                    if (stageTime >= s.queenRecovery)
                    {
                        skillStage = SkillStage.None;
                        SkillDetail = "";
                        StartCooldown(s.queenCooldown);
                    }
                    break;
            }
        }

        void QueenBlast(PawnRushSkillParams s)
        {
            Vector3 c = bodies[0].position;
            int down = 0, pushed = 0, freed = 0;
            foreach (var other in All)
            {
                if (other == null || other == this || IsAllyOf(other) || !IsEnemyOf(other) || other.SkillUntargetable) continue;
                Vector3 d = Flat(other.bodies[0].position - c);
                float dist = d.magnitude;
                if (dist > s.queenRadius || Mathf.Abs(other.bodies[0].position.y - c.y) > s.queenHeight) continue;
                freed += ReleaseGripOnAllies(other);
                Vector3 dir = dist > 0.05f ? d / dist : Flat(facing).normalized;
                string result;
                if (dist <= s.queenInner)
                {
                    result = SkillHit(other, dir * s.queenInnerPush + Vector3.up * 1.5f, true, "퀸 안쪽 원");
                    down++;
                }
                else
                {
                    result = SkillHit(other, dir * s.queenOuterPush, false, "퀸 바깥 원");
                    pushed++;
                }
                Fx(SkillFxKind.QueenHit, other, other.bodies[0].position, dir, dist <= s.queenInner ? 1 : 0);
                Log($"퀸 → {other.DisplayName}: {dist:0.0} m ({(dist <= s.queenInner ? "안쪽" : "바깥")}) → {result}");
            }
            int wires = SkillTripwire.BreakNear(c, s.queenRadius, team);
            blastFlash = 0.25f;
            Fx(SkillFxKind.QueenBlast, null, c - Vector3.up * standHeight, Flat(facing), down + pushed, s.queenRadius);
            Log($"퀸: 팔방 밀치기 — 안쪽 {down}명 · 바깥 {pushed}명" + (freed > 0 ? $" · 잡힌 아군 구출 {freed}" : "") + (wires > 0 ? $" · 밧줄 {wires}개 파괴" : ""));
        }

        /// <summary>The enemy lets go of any of this queen's allies it holds (the queen's "rescue").</summary>
        int ReleaseGripOnAllies(RagdollPawn enemy)
        {
            int n = 0;
            foreach (var hand in new[] { enemy.handL, enemy.handR })
            {
                if (hand == null || !hand.IsHolding || hand.HeldCollider == null) continue;
                if (!ColliderOwner.TryGetValue(hand.HeldCollider, out var held) || held == null || !IsAllyOf(held)) continue;
                hand.Release(0.5f);
                n++;
            }
            return n;
        }

        // ---------------------------------------------------------------- telegraphs

        void DrawSkillMarks(PawnRushSkillParams s)
        {
            float floorY = groundFound ? groundY : bodies[0].position.y - standHeight;
            Vector3 foot = new Vector3(bodies[0].position.x, floorY + 0.05f, bodies[0].position.z);
            HideSkillMarks();
            if (SkillMarksHidden) return;   // the effects draw them (design A, R90)
            switch (piece)
            {
                case PieceKind.Rook when skillStage == SkillStage.Windup:
                {
                    // R153: the chain's aim, a see-through white line from the chest (everyone sees it), to a wall if one is nearer.
                    var line = new Color(1f, 1f, 1f, aimLocked ? 0.6f : 0.38f);
                    Vector3 from = ChainOrigin;
                    float length = s.rookChainRange;
                    if (SolidRay(from, skillDir, length, out var wall)) length = wall.distance;
                    SkillMarks.Segment(Mark(ref markA, "Rook chain aim"), from, from + skillDir * length, line, aimLocked ? 0.06f : 0.04f, false);
                    break;
                }
                case PieceKind.Rook when chainPhase != ChainPhase.None:
                    SkillMarks.Segment(Mark(ref markA, "Rook chain"), RookChainHand, chainTip, new Color(0.75f, 0.77f, 0.8f, 1f), 0.05f, false);
                    break;
                case PieceKind.Queen when skillStage == SkillStage.Windup:
                {
                    // In the gold of the blast's light, not the side's blue (R82: "불빛 색이랑 똑같은 색으로"); the
                    // effects send small lights up off these rings.
                    float t = Mathf.Clamp01(stageTime / Mathf.Max(0.01f, s.queenWindup));
                    var gold = SkillMarks.Gold;
                    SkillMarks.Circle(Mark(ref markA, "Queen outer"), foot, s.queenRadius, gold, 0.05f);
                    SkillMarks.Circle(Mark(ref markB, "Queen inner"), foot, s.queenInner, gold, 0.05f);
                    SkillMarks.Circle(Mark(ref markC, "Queen squeeze"), foot, Mathf.Lerp(s.queenRadius, 0.2f, t), gold, 0.14f);
                    break;
                }
                case PieceKind.Queen when blastFlash > 0f:
                {
                    float t = 1f - blastFlash / 0.25f;
                    SkillMarks.Circle(Mark(ref markD, "Queen blast"), foot, Mathf.Lerp(0.5f, s.queenRadius, t), SkillMarks.Gold, 0.18f);
                    break;
                }
                case PieceKind.Knight when skillStage == SkillStage.Active || knightCandidate != null:
                {
                    // Where it comes down (R82's turning white mark), until the tap on the head.
                    if (skillStage == SkillStage.Active && !knightTapped)
                    {
                        float high = Mathf.Clamp01((bodies[0].position.y - knightSpot.y - standHeight) / 2.5f);
                        SkillMarks.Reticle(Disc(ref markLanding, "Knight landing"), Disc(ref markLandingInner, "Knight landing inner"),
                            knightSpot, s.knightLandRadius, 0.55f * (1f + 0.35f * high), Time.time);
                    }
                    var marked = KnightMarked;
                    if (marked != null)
                    {
                        // The enemy it leaps at (R153 plan: a red dashed ring over its head), blinking while only found.
                        float a = knightTarget != null ? 1f : 0.55f + 0.45f * Mathf.Sin(Time.time * 14f);
                        var lockColor = new Color(SkillMarks.Target.r, SkillMarks.Target.g, SkillMarks.Target.b, a);
                        Vector3 over = marked.bodies[(int)BodyId.Head].position + Vector3.up * 0.45f;
                        SkillMarks.Circle(Mark(ref markC, "Knight lock"), over, 0.35f, lockColor, 0.06f);
                    }
                    break;
                }
                case PieceKind.Bishop when skillStage == SkillStage.Windup:
                {
                    // The reach (close range) around the bishop, and the X where it would go: the two lines at shin
                    // height and the four pegs. R80: no blue (the reach and the floor shadow of the lines were the
                    // side's colour; the shadow is gone), the white lines thicker and stronger, still see-through.
                    SkillMarks.Circle(Mark(ref markE, "Bishop range"), foot, s.bishopRange, new Color(1f, 1f, 1f, 0.22f), 0.05f, 64);
                    if (!bishopValid) break;
                    Vector3 p = bishopPoint + Vector3.up * 0.04f, up = Vector3.up * s.bishopHeight;
                    float half = s.bishopLineLength * 0.5f;
                    Vector3 d1 = Quaternion.Euler(0f, 45f, 0f) * bishopYaw, d2 = Quaternion.Euler(0f, -45f, 0f) * bishopYaw;
                    var ghost = new Color(1f, 1f, 1f, 0.75f);
                    // A faint dark edge under the white lines: white on a light floor is hard to see otherwise.
                    var edge = new Color(0.05f, 0.08f, 0.19f, 0.2f);
                    SkillMarks.Segment(Mark(ref markC, "Bishop preview edge 1"), p - d1 * half + up, p + d1 * half + up, edge, 0.11f, false);
                    SkillMarks.Segment(Mark(ref markD, "Bishop preview edge 2"), p - d2 * half + up, p + d2 * half + up, edge, 0.11f, false);
                    SkillMarks.Segment(Mark(ref markA, "Bishop preview 1"), p - d1 * half + up, p + d1 * half + up, ghost, 0.06f, false);
                    SkillMarks.Segment(Mark(ref markB, "Bishop preview 2"), p - d2 * half + up, p + d2 * half + up, ghost, 0.06f, false);
                    markC.sortingOrder = markD.sortingOrder = -1;
                    markA.sortingOrder = markB.sortingOrder = 1;
                    Vector3[] ends = { p - d1 * half, p + d1 * half, p - d2 * half, p + d2 * half };
                    for (int i = 0; i < ends.Length; i++)
                        SkillMarks.Segment(Mark(ref markPegs[i], "Bishop preview peg"), ends[i], ends[i] + up + Vector3.up * 0.04f, ghost, 0.09f, false);
                    break;
                }
                case PieceKind.Bishop when skillStage == SkillStage.Active:
                {
                    float t = Mathf.Clamp01(stageTime / Mathf.Max(0.01f, bishopFlight));
                    Vector3 tip = Vector3.Lerp(bishopFrom, bishopPoint, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.8f);
                    SkillMarks.Segment(Mark(ref markC, "Bishop thread"), bishopFrom, tip, Color.white, 0.03f, false);
                    break;
                }
            }
        }

        /// <summary>The first floor (not a piece, not this rook) along a line, for the air charge's landing ring.</summary>
        bool FloorAlong(Vector3 from, Vector3 dir, float length, out Vector3 point)
        {
            point = default;
            int n = Physics.RaycastNonAlloc(from, dir, hits, length, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.distance >= best || ownSet.Contains(h.collider) || ColliderOwner.ContainsKey(h.collider) || h.normal.y < 0.5f) continue;
                best = h.distance;
                point = h.point;
            }
            return best < float.MaxValue;
        }

        /// <summary>
        /// The skills' part of the body pose (R80), from Pose before the hook's and the status' parts. The rook in the
        /// air (aiming, holding, charging): reaching up as it rises or charges upward, arms forward and head down
        /// diving as it falls or charges down, fists out in between. The knight kicking off the air on its second
        /// press: legs flung back, arms out ahead.
        /// </summary>
        void SkillPose(ref Quaternion armL, ref Quaternion armR, ref Quaternion chest, ref Quaternion head,
                       ref Quaternion thighL, ref Quaternion thighR, ref Quaternion footL, ref Quaternion footR)
        {
            if (PawnRushSkills == null || State != PawnState.Active) return;
            if (bindLeft > 0f)
            {
                // Tied up (R138): the arms bound tight to its sides, the legs together, wriggling against the rope.
                float t = Time.time;
                float w = Mathf.Sin(t * 11f), w2 = Mathf.Sin(t * 7.3f + 1.1f);
                armL = Quaternion.Euler(0f, 4f * w2, 74f + 4f * w);
                armR = Quaternion.Euler(0f, -4f * w2, -74f + 4f * w);
                chest = Quaternion.Euler(3f * w2, 9f * w, 7f * w2);
                head = Quaternion.Euler(-6f + 4f * w, -10f * w, -6f * w2);
                thighL = Quaternion.Euler(2f * w, 0f, 3f);
                thighR = Quaternion.Euler(-2f * w, 0f, -3f);
                footL = footR = Quaternion.identity;
                return;
            }
            if (QueenHillSkills != null)
            {
                QueenHillPose(ref armL, ref armR, ref chest, ref head, ref thighL, ref thighR, ref footL, ref footR);   // R89
                return;
            }
            if (piece == PieceKind.Knight && skillStage == SkillStage.Windup)
            {
                // R138 (승규 님: "좀 더 나이트다! 라는 연출"): it rears up like a horse before it springs: leaning back, head up,
                // the arms (its front legs) up in front pawing the air, then drops into the crouch.
                float w = Mathf.Max(0.05f, PawnRushSkills.knightWindup);
                float rear = Mathf.Clamp01(stageTime / (w * 0.6f)), gather = Mathf.Clamp01((stageTime - w * 0.6f) / (w * 0.4f));
                float r = rear * rear * (3f - 2f * rear), paw = Mathf.Sin(stageTime * 40f);
                chest = Quaternion.Slerp(Quaternion.Slerp(chest, Quaternion.Euler(-30f, 0f, 0f), r), Quaternion.Euler(24f, 0f, 0f), gather);
                head = Quaternion.Slerp(Quaternion.Slerp(head, Quaternion.Euler(-24f, 0f, 0f), r), Quaternion.Euler(-10f, 0f, 0f), gather);
                armR = Quaternion.Slerp(Quaternion.Slerp(armR, Quaternion.Euler(0f, -80f, 40f + 28f * paw), r), Quaternion.Euler(0f, 50f, -30f), gather);
                armL = Quaternion.Slerp(Quaternion.Slerp(armL, Quaternion.Euler(0f, 80f, -(40f - 28f * paw)), r), Quaternion.Euler(0f, -50f, 30f), gather);
                thighL = Quaternion.Slerp(Quaternion.Euler(-8f * r, 0f, -4f), Quaternion.Euler(-22f, 0f, -6f), gather);
                thighR = Quaternion.Slerp(Quaternion.Euler(-8f * r, 0f, 4f), Quaternion.Euler(-22f, 0f, 6f), gather);
                footL = footR = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(22f, 0f, 0f), gather);
                return;
            }
            if (piece == PieceKind.Knight && skillStage == SkillStage.Active && !OnFloor)
            {
                // R138: a jumping horse. Going up the front legs fold under the chest and it looks up; over the top it
                // stretches out level; coming down the front legs reach for the ground and it looks at where it lands.
                float vy = bodies[0].linearVelocity.y;
                float up = Mathf.Clamp01(vy / 3f), down = Mathf.Clamp01(-vy / 3f);
                chest = Quaternion.Slerp(Quaternion.Slerp(Quaternion.Euler(14f, 0f, 0f), Quaternion.Euler(-14f, 0f, 0f), up), Quaternion.Euler(28f, 0f, 0f), down);
                head = Quaternion.Slerp(Quaternion.Slerp(Quaternion.Euler(-6f, 0f, 0f), Quaternion.Euler(-22f, 0f, 0f), up), Quaternion.Euler(14f, 0f, 0f), down);
                armR = Quaternion.Slerp(Quaternion.Slerp(Quaternion.Euler(0f, -70f, -48f), Quaternion.Euler(0f, -85f, -4f), up), Quaternion.Euler(0f, -84f, -38f), down);
                armL = Quaternion.Slerp(Quaternion.Slerp(Quaternion.Euler(0f, 70f, 48f), Quaternion.Euler(0f, 85f, 4f), up), Quaternion.Euler(0f, 84f, 38f), down);
                thighL = Quaternion.Slerp(Quaternion.Slerp(Quaternion.Euler(-6f, 0f, -4f), Quaternion.Euler(22f, 0f, -4f), up), Quaternion.Euler(-20f, 0f, -5f), down);
                thighR = Quaternion.Slerp(Quaternion.Slerp(Quaternion.Euler(-6f, 0f, 4f), Quaternion.Euler(22f, 0f, 4f), up), Quaternion.Euler(-20f, 0f, 5f), down);
                footL = footR = Quaternion.Euler(16f, 0f, 0f);
                return;
            }
            if (chainCarry && chainCarryAlly)
            {
                // R153 (plan §5.4): riding the rook's chain like a water ski handle: both arms out ahead holding it, the
                // body leaning back, the legs trailing.
                armL = Quaternion.Euler(0f, 84f, -8f);
                armR = Quaternion.Euler(0f, -84f, 8f);
                chest = Quaternion.Euler(-16f, 0f, 0f);
                head = Quaternion.Euler(-8f, 0f, 0f);
                thighL = Quaternion.Euler(-26f, 0f, -4f);
                thighR = Quaternion.Euler(-34f, 0f, 4f);
                footL = footR = Quaternion.Euler(24f, 0f, 0f);
                return;
            }
            if (chainCarry)
            {
                // An enemy dragged in: pulled off its feet chest first, arms trailing back, legs dangling.
                armL = Quaternion.Euler(0f, -60f, 30f);
                armR = Quaternion.Euler(0f, 60f, -30f);
                chest = Quaternion.Euler(18f, 0f, 0f);
                head = Quaternion.Euler(-14f, 0f, 0f);
                thighL = Quaternion.Euler(-18f, 0f, -6f);
                thighR = Quaternion.Euler(-10f, 0f, 6f);
                footL = footR = Quaternion.Euler(14f, 0f, 0f);
                return;
            }
            if (piece == PieceKind.Rook && (aimLocked || chainPhase != ChainPhase.None) && skillStage != SkillStage.None)
            {
                // R153: the throw: the right arm swung out along the chain's line, the left braced back, leaning back
                // against the pull while it hauls in.
                bool hauling = chainPhase == ChainPhase.Pull || chainPhase == ChainPhase.Rewind;
                armR = Quaternion.Euler(0f, -88f, hauling ? 6f : -4f);
                armL = Quaternion.Euler(0f, 30f, 40f);
                chest = Quaternion.Euler(hauling ? -14f : 6f, hauling ? 0f : -10f, 0f);
                head = Quaternion.Euler(hauling ? 4f : -6f, 0f, 0f);
                thighL = Quaternion.Euler(hauling ? -12f : 0f, 0f, -6f);
                thighR = Quaternion.Euler(hauling ? 10f : 0f, 0f, 6f);
                return;
            }
        }

        LineRenderer Mark(ref LineRenderer mark, string label)
        {
            if (mark == null) mark = SkillMarks.Line(transform, $"{name} {label}");
            return mark;
        }

        MeshRenderer Disc(ref MeshRenderer disc, string label)
        {
            if (disc == null) disc = SkillMarks.Disc(transform, $"{name} {label}");
            return disc;
        }

        void HideSkillMarks()
        {
            SkillMarks.Hide(markA);
            SkillMarks.Hide(markB);
            SkillMarks.Hide(markC);
            SkillMarks.Hide(markD);
            SkillMarks.Hide(markE);
            foreach (var peg in markPegs) SkillMarks.Hide(peg);
            SkillMarks.Hide(markLanding);
            SkillMarks.Hide(markLandingInner);
        }
    }
}
