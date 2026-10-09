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
    /// The Pawn Rush piece skills (Docs/Skills/DESIGN.md §3-7): pawn "first two steps", knight "bent leap",
    /// bishop "crossed tripwire", rook "straight charge", queen "ring shove". The king's is not made yet (D1).
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

        internal static void RaiseSkillFx(SkillFxEvent e) => SkillFx?.Invoke(e);

        void Fx(SkillFxKind kind, RagdollPawn target, Vector3 at, Vector3 dir, int count = 0, float size = 0f, Vector3 normal = default)
            => SkillFx?.Invoke(new SkillFxEvent { kind = kind, by = this, target = target, at = at, dir = dir, count = count, size = size, normal = normal });

        Vector3 SkillContact(RagdollPawn other) =>
            Vector3.Lerp(bodies[(int)BodyId.Chest].position, other.bodies[(int)BodyId.Chest].position, 0.5f);

        SkillStage skillStage;
        float stageTime, skillCooldownLeft, cooldownTotal, skillGrace;
        bool skillPressed, skillConfirm, skillCancel, skillGrabRaw, skillGrabLatch, aimLocked, rookConfirmQueued;
        // The rook's charge in the air (R80): aimed up or down with the mouse, along skillDir (not flat then).
        bool rookAir;
        Vector3 skillMoveRaw;
        // A dash (the pawn's steps, the rook's charge): a fixed speed along a fixed line for a while.
        bool dashing;
        Vector3 skillDir = Vector3.forward;
        float dashBase, dashLeft;
        int dashHits, pawnStep;
        bool pawnBuffered, pawnStepHit, pawnHelped;
        readonly HashSet<RagdollPawn> skillHitSet = new HashSet<RagdollPawn>();
        readonly Collider[] skillOverlap = new Collider[48];
        // Getting up: skills only stagger for a moment (§2.4); the pawn's help makes it longer and faster.
        float getUpGuardLeft, pendingGuard, hasteLeft;
        PawnState skillSeenState = PawnState.Active;
        // Knight
        float knightAir, knightHomeLeft, knightKick;
        bool knightTurned, knightHoming, knightTapped;
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
        public float HasteLeft => Mathf.Max(0f, hasteLeft);
        public bool SkillDashing => dashing;
        public int SkillUses { get; private set; }
        public string SkillDetail { get; private set; } = "";
        public string LastSkillHit { get; private set; } = "-";

        /// <summary>A rook or a bishop between its key and the left click.</summary>
        public bool SkillAiming => Aiming;
        public bool BishopAiming => piece == PieceKind.Bishop && skillStage == SkillStage.Windup;
        public bool BishopAimValid => bishopValid;
        public Vector3 BishopAimPoint => bishopPoint;
        public Vector3 BishopAimYaw => bishopYaw;
        /// <summary>The enemy a knight's second press in the air would come down on (null: it would turn).</summary>
        public RagdollPawn KnightMarked => knightHoming ? knightTarget : knightCandidate;
        /// <summary>The rook's charge is in the air (R80): its aim is a line in 3D.</summary>
        public bool RookInAir => rookAir;
        /// <summary>On the floor, or just off it (the coyote time).</summary>
        public bool OnTheFloor => OnFloor;
        /// <summary>The knight's landing is to be shown: in its leap, not homing onto a head nor after the tap.</summary>
        public bool KnightShowsLanding => piece == PieceKind.Knight && skillStage == SkillStage.Active && !knightHoming && !knightTapped;
        public Vector3 KnightLandingSpot => knightSpot;
        public bool KnightHoming => knightHoming;
        /// <summary>The bishop's throw: where its X leaves from and how long it flies.</summary>
        public Vector3 BishopThrowFrom => bishopFrom;
        public float BishopThrowTime => bishopFlight;
        /// <summary>Set by the effects when they draw the skills' telegraphs themselves (design A, R90): the plain
        /// lines (SkillMarks) are not drawn then.</summary>
        public static bool SkillMarksHidden { get; set; }

        bool Aiming => skillStage == SkillStage.Windup && !aimLocked
                       && (QueenHillSkills != null ? QueenHillAims : piece == PieceKind.Rook || piece == PieceKind.Bishop);

        /// <summary>A skill is moving this pawn into others, or has just stopped doing so: contacts between
        /// pawns are the skill's to judge, not the collision knockdown's.</summary>
        bool SkillShielded => dashing || skillGrace > 0f;

        /// <summary>The pawn's help (진군): +15% run and sprint for a moment.</summary>
        float SkillSpeedScale => PawnRushSkills != null && hasteLeft > 0f ? PawnRushSkills.hasteScale : 1f;

        public static string SkillName(PieceKind kind) => kind switch
        {
            PieceKind.Pawn => "첫 두 걸음",
            PieceKind.Knight => "꺾어 도약",
            PieceKind.Bishop => "교차 밧줄",
            PieceKind.Rook => "직선 돌파",
            PieceKind.Queen => "팔방 밀치기",
            _ => "(킹 스킬은 아직 없음)",
        };

        /// <summary>The skill key went down this frame (kept until the next physics step reads it). The aim's
        /// left and right clicks come in with the pawn's own input (the dive and the grab).</summary>
        public void SetSkillInput(bool pressed) => skillPressed |= pressed;

        /// <summary>Drop whatever skill is under way and start the cooldown over (the test bed's piece switch).</summary>
        public void ResetSkill()
        {
            ClearSkills();
            skillCooldownLeft = cooldownTotal = 0f;
        }

        void Log(string text) => SkillLog?.Invoke(this, text);

        /// <summary>This bishop's wire tripped <paramref name="target"/> (SkillTripwire).</summary>
        public string SkillTrip(RagdollPawn target, Vector3 push) => SkillHit(target, push, true, "교차 밧줄");

        // ---------------------------------------------------------------- the step hooks

        /// <summary>Before the jump and the run: a skill that roots the piece takes the keys away.</summary>
        void PreSkills()
        {
            if (PawnRushSkills == null) return;
            skillMoveRaw = input.move;
            skillGrabRaw = input.grab;
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
            if (QueenHillSkills != null)
            {
                PreQueenHillSkills();   // the Queen of the Hill skills take it from here (R89)
                return;
            }
            if (knightHoming)
            {
                // The second press flies the knight onto the head by itself.
                input.move = Vector3.zero;
                input.jump = false;
                input.shove = false;
            }
            // Aiming, the rook walks (and jumps) as usual but cannot sprint; the click roots it (R75).
            if (Aiming && piece == PieceKind.Rook) input.sprint = false;
            bool rooted = dashing || skillStage == SkillStage.Recovery
                          || (skillStage == SkillStage.Windup && (piece == PieceKind.Queen || (piece == PieceKind.Rook && aimLocked)));
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
            hasteLeft -= dt;
            getUpGuardLeft -= dt;
            skillGrace -= dt;
            blastFlash -= dt;
            TrackGetUp(s);
            if (QueenHillSkills != null)
            {
                UpdateQueenHillSkills(QueenHillSkills, press, confirm, cancel, dt);   // Queen of the Hill skills (R89)
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
                case PieceKind.Rook: UpdateRookSkill(s, press, confirm, cancel, dt); break;
                case PieceKind.Queen: UpdateQueenSkill(s, press, dt); break;
                default:
                    if (press) Log("킹 스킬은 아직 만들지 않았어요 (D1)");
                    break;
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
            skillPressed = skillConfirm = skillCancel = false;
            aimLocked = rookConfirmQueued = rookAir = false;
            knightHoming = knightTapped = false;
            knightKick = 0f;
            knightTarget = knightCandidate = null;
            SkillDetail = "";
            HideSkillMarks();
            ClearQueenHill();
        }

        float PieceCooldown(PawnRushSkillParams s) => piece switch
        {
            PieceKind.Pawn => s.pawnCooldown,
            PieceKind.Knight => s.knightCooldown,
            PieceKind.Bishop => s.bishopCooldown,
            PieceKind.Rook => s.rookCooldown,
            PieceKind.Queen => s.queenCooldown,
            _ => 0f,
        };

        void StartCooldown(float seconds)
        {
            // The test bed's flat cooldown (2 s for quick tries) stands in for every piece's own.
            if (PawnRushSkills != null && PawnRushSkills.testCooldown > 0f) seconds = PawnRushSkills.testCooldown;
            cooldownTotal = Mathf.Max(0f, seconds);
            skillCooldownLeft = cooldownTotal;
        }

        /// <summary>Free to start a skill: on the ground, or anywhere for one that works in the air (the rook, R80).</summary>
        bool CanStartSkill(bool inAir = false) =>
            State == PawnState.Active && !Climbing && rope == null && !Floating && !BeingHeld && !Squashed && !Staggered
            && (hookPhase == HookPhase.None || hookPhase == HookPhase.Held) && skillCooldownLeft <= 0f
            && (inAir || Grounded || coyote > 0f);

        bool OnFloor => Grounded || coyote > 0f;

        /// <summary>The rook coming down out of an air charge (charging, or just slammed in): the floor is where the
        /// skill means it to land, not a fall that floors it (R80).</summary>
        bool SkillLandsOnFloor => PawnRushSkills != null && rookAir && (dashing || skillStage == SkillStage.Recovery);

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
            dashHits = 0;
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

        /// <summary>The rook's charge in the air (R80): straight along its line, up or down, gravity held off, the
        /// body leaning into it (upright going up, flat going level, head first diving down).</summary>
        void DriveAirDash(float dt)
        {
            Vector3 want = skillDir * dashBase;
            AddVelocity(want - bodies[0].linearVelocity);
            carryVel = Vector3.zero;
            anchorVel = Flat(want);
            Vector3 hp = bodies[0].position;
            anchorPos = hp + want * dt;
            anchor.MovePosition(anchorPos);
            facing = FlatDir(skillDir);
            freeFlight = Mathf.Max(freeFlight, 0.1f);
            airTimer = Mathf.Max(airTimer, 0.1f);
            LeanAlongLine(1f);
        }

        /// <summary>The air charge's warning: the rook stops in the air (its fall too) for rookAirLock, already
        /// leaning a little into the line.</summary>
        void HoverForCharge(float dt)
        {
            Vector3 v = bodies[0].linearVelocity;
            AddVelocity(-v * Mathf.Clamp01(14f * dt) - Physics.gravity * dt);
            freeFlight = Mathf.Max(freeFlight, 0.1f);
            airTimer = Mathf.Max(airTimer, 0.1f);
            LeanAlongLine(0.4f);
        }

        /// <summary>The hips' balance target tilted along the charge line like Superman (R81): going up the body lies
        /// along the line, head first (45° up = 45° lean, level = 80°); diving down it leans 50° (more and it went into
        /// the floor head first).</summary>
        void LeanAlongLine(float amount)
        {
            float up = Mathf.Asin(Mathf.Clamp(skillDir.y, -1f, 1f)) * Mathf.Rad2Deg;
            float lean = up >= 0f ? Mathf.Lerp(80f, 45f, up / 45f) : Mathf.Lerp(80f, 50f, -up / 60f);
            anchor.MoveRotation(Quaternion.LookRotation(FlatDir(skillDir), Vector3.up) * Quaternion.Euler(lean * amount, 0f, 0f));
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
                if (Mathf.Abs(h.normal.y) > 0.5f && h.collider.GetComponentInParent<SkillBarricade>() == null) continue;   // floor, ramp
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
                    if (press && pawnStep == 1) pawnBuffered = true;
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
                            SkillDetail = "연결 (G 한 번 더)";
                        }
                        else FinishPawn(s, true);
                    }
                    break;
                case SkillStage.Link:
                    stageTime += dt;
                    if (press) pawnBuffered = true;
                    if (pawnBuffered && stageTime >= s.pawnLinkMin && State == PawnState.Active && !Climbing) PawnStep(s, 2);
                    else if (stageTime > s.pawnLinkMax) FinishPawn(s, false);
                    break;
            }
        }

        void PawnStep(PawnRushSkillParams s, int n)
        {
            skillDir = MoveOrAim();
            pawnStep = n;
            pawnBuffered = pawnStepHit = false;
            skillHitSet.Clear();
            StartDash(s.pawnStepSpeed, s.pawnStepDistance / Mathf.Max(0.1f, s.pawnStepSpeed));
            skillStage = SkillStage.Active;
            stageTime = 0f;
            SkillDetail = n == 1 ? "첫 걸음" : "두 번째 걸음";
            Fx(SkillFxKind.PawnStep, null, FeetPoint, skillDir, n);
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
                    continue;
                }
                if (pawnStepHit || !IsEnemyOf(other)) continue;
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
            ally.hasteLeft = s.hasteTime;
            hasteLeft = s.hasteTime;
            pawnHelped = true;
            Fx(SkillFxKind.PawnHelp, ally, ally.bodies[0].position, skillDir);
            Log($"폰 → {ally.DisplayName}: 부축 — 바로 일어남, 기상 보호 {s.helpGuard:0.#}초, 둘 다 +{(s.hasteScale - 1f) * 100f:0}% {s.hasteTime:0.#}초, 내 쿨 −{s.helpCooldownCut:0.#}초");
        }

        // ---------------------------------------------------------------- knight: bent leap (§4)

        void UpdateKnightSkill(PawnRushSkillParams s, bool press, float dt)
        {
            switch (skillStage)
            {
                case SkillStage.None:
                    if (press && CanStartSkill(true))
                    {
                        SkillUses++;
                        if (!OnFloor)
                        {
                            // In the air too (R81): it leaps on from where it is, at once (no rearing up mid-air).
                            KnightLeap(s);
                            break;
                        }
                        skillStage = SkillStage.Windup;
                        stageTime = 0f;
                        SkillDetail = "준비 (뒷발로 섬)";
                    }
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
                    knightKick -= dt;
                    // One second press per leap: onto the head of an enemy close by, or else the kick on (turned or not).
                    bool second = !knightTurned && knightAir >= s.knightTurnAfter;
                    knightCandidate = second ? StompTarget(s) : null;
                    if (press && second)
                    {
                        if (knightCandidate != null) StartHoming(s, knightCandidate);
                        else KnightTurn(s);
                    }
                    if (knightHoming) SteerHoming(dt);
                    KnightStomp(s);
                    if (Climbing || rope != null)
                    {
                        // Caught a ledge on the way: the leap ends there, no landing shock.
                        skillStage = SkillStage.None;
                        SkillDetail = "";
                        StartCooldown(s.knightCooldown);
                    }
                    else if (knightAir > 0.2f && Grounded)
                    {
                        KnightLand(s);
                        skillStage = SkillStage.Recovery;
                        stageTime = 0f;
                        SkillDetail = "착지 후딜";
                        knightHoming = false;
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

        void KnightLeap(PawnRushSkillParams s)
        {
            bool fromFloor = OnFloor;   // (the launch below takes it off the floor)
            float g = Mathf.Max(0.01f, -Physics.gravity.y);
            // The textbook arc for the designed height and distance, then what the ragdoll loses in the air
            // put back (measured in the skill test scene).
            float up = Mathf.Sqrt(2f * g * Mathf.Max(0.05f, s.knightHeight));
            float on = s.knightDistance / (2f * up / g) * s.knightCarryCorrection;
            up *= s.knightLiftCorrection;
            skillDir = AimFlat();
            float along = Vector3.Dot(Flat(bodies[0].linearVelocity) - carryVel, skillDir);
            on = Mathf.Max(on, along);   // D2
            Launch(skillDir * on + Vector3.up * up);
            facing = skillDir;
            knightAir = 0f;
            knightTurned = knightTapped = false;
            skillHitSet.Clear();
            skillStage = SkillStage.Active;
            stageTime = 0f;
            SkillDetail = "도약 (공중에서 F = 다시 차고 나감)";
            knightSpot = PredictLanding(bodies[0].position, bodies[0].linearVelocity, g);
            Fx(SkillFxKind.KnightLeap, null, bodies[0].position - Vector3.up * standHeight, skillDir, fromFloor ? 0 : 1);
        }

        void KnightTurn(PawnRushSkillParams s)
        {
            Vector3 want = Flat(skillMoveRaw);
            if (want.sqrMagnitude < 0.04f) want = AimFlat();
            want.Normalize();
            Vector3 v = Flat(bodies[0].linearVelocity);
            float speed = v.magnitude;
            Vector3 dir = speed > 0.1f ? v / speed : skillDir;
            Vector3 turned = Vector3.RotateTowards(dir, want, s.knightTurnMax * Mathf.Deg2Rad, 0f);
            float angle = Vector3.Angle(dir, turned);
            // The second leg of the L kicks off the air (R80, "다~당"): at least knightTurnSpeed along the new line
            // and a fresh rise, straight on as much as round the corner. The plain turn before kept the speed and
            // the fall it had: the second leg was short, and going straight the press did nothing at all.
            float rise = Mathf.Max(bodies[0].linearVelocity.y, s.knightTurnLift);
            Launch(turned * Mathf.Max(speed, s.knightTurnSpeed) + Vector3.up * rise);
            facing = turned;
            skillDir = turned;
            knightTurned = true;
            knightKick = 0.3f;
            knightSpot = PredictLanding(bodies[0].position, bodies[0].linearVelocity, Mathf.Max(0.01f, -Physics.gravity.y));
            Fx(SkillFxKind.KnightTurn, null, bodies[0].position, turned, Mathf.RoundToInt(angle));
            SkillDetail = angle > 5f ? "꺾어 차고 나감" : "앞으로 차고 나감";
            Log($"나이트: 공중에서 다시 차고 나감 {angle:0}°");
        }

        void KnightStomp(PawnRushSkillParams s)
        {
            if (bodies[0].linearVelocity.y > -1f) return;
            Vector3 feet = bodies[0].position - Vector3.up * standHeight;
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.State == PawnState.Ragdoll || skillHitSet.Contains(other)) continue;
                var headCollider = other.bodies[(int)BodyId.Head].GetComponent<Collider>();
                Vector3 head = other.bodies[(int)BodyId.Head].position;
                float top = headCollider != null ? headCollider.bounds.max.y : head.y + 0.12f;
                if (Flat(feet - head).magnitude > s.knightStompReach || feet.y < top - 0.15f || feet.y > top + 0.35f) continue;
                skillHitSet.Add(other);
                string result = SkillHit(other, Flat(facing) * 1f + Vector3.down * 1f, true, "나이트 밟기");
                Fx(SkillFxKind.KnightStomp, other, new Vector3(head.x, top, head.z), Flat(facing));
                if (knightHoming || other == knightTarget)
                {
                    // The second press's stomp: a tap on the head, then down right beside it.
                    knightHoming = false;
                    knightTapped = true;
                    knightTarget = null;
                    Launch(Flat(facing) * s.knightHomingHop + Vector3.up * s.knightHomingBounce);
                    knightSpot = PredictLanding(bodies[0].position, bodies[0].linearVelocity, Mathf.Max(0.01f, -Physics.gravity.y));
                    SkillDetail = "머리 찍고 착지";
                    Log($"나이트 → {other.DisplayName}: 머리 찍기 → {result}, 바로 옆에 착지");
                    return;
                }
                // Off its head and up again; still the same leap (the turn, if unused, is still there).
                Vector3 v = bodies[0].linearVelocity;
                AddVelocity(new Vector3(0f, s.knightStompBounce - v.y, 0f));
                freeFlight = Mathf.Max(freeFlight, 0.4f);
                airTimer = Mathf.Max(airTimer, 0.15f);
                knightSpot = PredictLanding(bodies[0].position, bodies[0].linearVelocity, Mathf.Max(0.01f, -Physics.gravity.y));
                Log($"나이트 → {other.DisplayName}: 머리 밟기 → {result}, 다시 튀어 오름");
                return;
            }
        }

        /// <summary>The enemy a second press would come down on: the nearest one standing within knightLockRange
        /// across the ground, and not up above the knight.</summary>
        RagdollPawn StompTarget(PawnRushSkillParams s)
        {
            RagdollPawn best = null;
            float bestAcross = s.knightLockRange;
            Vector3 me = bodies[0].position;
            foreach (var other in All)
            {
                if (other == null || other == this || other.NetworkPuppet || !IsEnemyOf(other) || other.State != PawnState.Active) continue;
                Vector3 d = other.bodies[0].position - me;
                float across = Flat(d).magnitude;
                if (across > bestAcross || d.y > 0.5f) continue;
                best = other;
                bestAcross = across;
            }
            return best;
        }

        /// <summary>Where the hips must be for the feet to come down on top of <paramref name="target"/>'s head.</summary>
        Vector3 StompPoint(RagdollPawn target)
        {
            var head = target.bodies[(int)BodyId.Head];
            var col = head.GetComponent<Collider>();
            float top = col != null ? col.bounds.max.y : head.position.y + 0.12f;
            return new Vector3(head.position.x, top - 0.05f + standHeight, head.position.z);
        }

        /// <summary>The second press with an enemy marked: fly onto its head. The flight takes long enough to come
        /// down onto the head from above (knightHomingFall), not to fly flat into it, and the arc is worked out
        /// again every step, so a target that walks on is still met.</summary>
        void StartHoming(PawnRushSkillParams s, RagdollPawn target)
        {
            knightTurned = true;
            knightHoming = true;
            knightTarget = target;
            Vector3 p = bodies[0].position, aim = StompPoint(target);
            float g = Mathf.Max(0.01f, -Physics.gravity.y), fall = Mathf.Max(0.5f, s.knightHomingFall);
            float dy = aim.y - p.y;
            float tFall = (fall + Mathf.Sqrt(Mathf.Max(0f, fall * fall + 2f * g * dy))) / g;
            float tRun = Flat(aim - p).magnitude / Mathf.Max(0.5f, s.knightHomingSpeed);
            knightHomeLeft = Mathf.Clamp(Mathf.Max(tFall, tRun), 0.2f, 1.2f);
            Vector3 dir = Flat(aim - p);
            if (dir.sqrMagnitude > 1e-4f) facing = skillDir = dir.normalized;
            SteerHoming(0f);
            SkillDetail = "머리 찍기";
            Fx(SkillFxKind.KnightHome, target, aim, facing);
            Log($"나이트: {target.DisplayName} 감지 → 공중에서 F, 머리로 날아감");
        }

        void SteerHoming(float dt)
        {
            knightHomeLeft -= dt;
            if (knightTarget == null || knightTarget.State == PawnState.Ragdoll || knightHomeLeft <= 0.02f)
            {
                knightHoming = false;
                return;
            }
            float t = knightHomeLeft;
            Vector3 want = (StompPoint(knightTarget) - bodies[0].position) / t - 0.5f * t * Physics.gravity;
            Launch(want);
        }

        void KnightLand(PawnRushSkillParams s)
        {
            Vector3 feet = bodies[0].position - Vector3.up * standHeight;
            int staggered = 0;
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.State != PawnState.Active) continue;
                Vector3 d = other.bodies[0].position - bodies[0].position;
                if (Flat(d).magnitude > s.knightLandRadius || Mathf.Abs(d.y) > 1f) continue;
                if (other.Stagger(s.knightLandStagger))
                {
                    staggered++;
                    other.LastSkillHit = "나이트 착지: 휘청";
                }
            }
            int wires = SkillTripwire.BreakNear(feet, s.knightLandRadius, team);
            Fx(SkillFxKind.KnightLand, null, feet, Flat(facing), staggered, s.knightLandRadius);
            Log($"나이트: 착지 — 주변 {staggered}명 휘청 {s.knightLandStagger:0.##}초" + (wires > 0 ? $" · 밧줄 {wires}개 파괴" : ""));
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

        // ---------------------------------------------------------------- rook: straight charge (§6)

        void UpdateRookSkill(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (skillStage)
            {
                case SkillStage.None:
                    if (press && CanStartSkill(true))   // in the air too (R80)
                    {
                        skillStage = SkillStage.Windup;
                        stageTime = 0f;
                        aimLocked = false;
                        skillGrabLatch = skillGrabRaw;
                        skillDir = RookAim(s);
                        SkillDetail = "조준 (마우스로 방향 · 좌클릭 돌진 · 우클릭/F 취소)";
                    }
                    break;
                case SkillStage.Windup:
                    stageTime += dt;
                    if (!aimLocked)
                    {
                        // Aiming: the line follows the mouse (the camera's aim) until the left click. The rook walks
                        // meanwhile (no sprint, R75); standing still, it turns to the aim.
                        skillDir = RookAim(s);
                        if (Flat(skillMoveRaw).sqrMagnitude < 0.04f) facing = FlatDir(skillDir);
                        if (cancel || press)
                        {
                            if (cancel) skillGrabLatch = true;
                            ClearSkills();
                            Log("룩: 조준 취소");
                            break;
                        }
                        // On the ground the charge runs over the floor after rookLock; a click in the air (R80; it
                        // waited for the feet before) stops the rook in the air for rookAirLock and charges along
                        // the aim, up or down.
                        if (confirm)
                        {
                            SkillUses++;
                            aimLocked = true;
                            rookAir = !OnFloor;
                            if (!rookAir) skillDir = FlatDir(skillDir);
                            stageTime = 0f;
                            facing = FlatDir(skillDir);
                            SkillDetail = rookAir ? "공중 예고 (방향 고정, 멈춤)" : "예고 (방향 고정)";
                        }
                        break;
                    }
                    facing = FlatDir(skillDir);
                    if (rookAir) HoverForCharge(dt);
                    if (stageTime >= (rookAir ? s.rookAirLock : s.rookLock))
                    {
                        skillHitSet.Clear();
                        StartDash(s.rookSpeed, s.rookTime);
                        skillStage = SkillStage.Active;
                        stageTime = 0f;
                        SkillDetail = rookAir ? (skillDir.y > 0.1f ? "공중 돌진 (위로)" : skillDir.y < -0.1f ? "공중 돌진 (아래로)" : "공중 돌진") : "돌진";
                    }
                    break;
                case SkillStage.Active:
                    stageTime += dt;
                    if (!RookCharge(s, dt)) break;
                    if (rookAir) DriveAirDash(dt);
                    else DriveDash(dt);
                    dashLeft -= dt;
                    if (dashLeft <= 0f)
                    {
                        if (rookAir)
                        {
                            // Off the end of a charge in the air it flies on, slowing, and falls.
                            AddVelocity(-bodies[0].linearVelocity * 0.55f);
                        }
                        else
                        {
                            // The charge is four squares: it plants its feet at the end instead of sliding on
                            // (measured without this: 1.25 m more during the recovery).
                            Vector3 own = Flat(bodies[0].linearVelocity) - carryVel;
                            AddVelocity(-own * 0.75f);
                            anchorVel = carryVel + own * 0.25f;
                        }
                        RookRecovery();
                    }
                    break;
                case SkillStage.Recovery:
                    stageTime += dt;
                    if (stageTime >= s.rookRecovery)
                    {
                        skillStage = SkillStage.None;
                        SkillDetail = "";
                        StartCooldown(s.rookCooldown);
                    }
                    break;
            }
        }

        /// <summary>Where the rook would charge: over the floor where the mouse points; in the air (R80) tilted up or
        /// down too, by how far the camera looks above or below rookAirNeutralPitch, within rookAirMaxUp/Down.</summary>
        Vector3 RookAim(PawnRushSkillParams s)
        {
            Vector3 flat = AimFlat();
            if (OnFloor || input.aim.sqrMagnitude < 1e-4f) return flat;
            float lookDown = Mathf.Asin(Mathf.Clamp(-input.aim.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            float up = Mathf.Clamp(s.rookAirNeutralPitch - lookDown, -s.rookAirMaxDown, s.rookAirMaxUp) * Mathf.Deg2Rad;
            return (flat * Mathf.Cos(up) + Vector3.up * Mathf.Sin(up)).normalized;
        }

        void RookRecovery()
        {
            EndDash();
            skillStage = SkillStage.Recovery;
            stageTime = 0f;
            SkillDetail = "후딜";
        }

        /// <summary>One step of the charge's contacts. False = the charge has just ended.</summary>
        bool RookCharge(PawnRushSkillParams s, float dt)
        {
            // (Grounded is no use here: the charge keeps the air timer running, so it never turns true.)
            bool feetDown = groundFound && bodies[0].position.y - groundY < standHeight + 0.12f;
            if (rookAir && skillDir.y < -0.15f && stageTime > 0.04f && feetDown)
            {
                // A charge down out of the air meets the floor: it slams in where it lands (R80).
                EndDash();
                AddVelocity(-bodies[0].linearVelocity * 0.9f);
                anchorVel = carryVel = Vector3.zero;
                Vector3 hp = bodies[0].position;
                anchorPos = hp;
                anchor.MovePosition(anchorPos);
                Fx(SkillFxKind.RookSlam, null, hp - Vector3.up * standHeight, skillDir);
                RookRecovery();
                Log("룩: 공중에서 내리꽂아 바닥에 박힘");
                return false;
            }
            if (WallAhead(dashBase * dt + 0.3f, out var wall))
            {
                var barricade = wall.collider.GetComponentInParent<SkillBarricade>();
                if (barricade != null && barricade.Standing)
                {
                    barricade.Break(s.barricadeRegrow);
                    Fx(SkillFxKind.RookBarricade, null, wall.point, skillDir, normal: wall.normal);
                    dashBase *= 1f - s.rookBarricadeSlow;
                    Log($"룩: 바리케이드 파괴 → 속도 −{s.rookBarricadeSlow * 100f:0}% ({dashBase:0.0} m/s), 계속 전진 · {s.barricadeRegrow:0}초 뒤 다시 생김");
                }
                else
                {
                    HaltDash();
                    Fx(SkillFxKind.RookWall, null, wall.point, skillDir, normal: wall.normal);
                    Stagger(s.rookWallStagger);
                    RookRecovery();
                    Log($"룩: 벽에 박힘 → 그 자리에서 멈춤, 룩 휘청 {s.rookWallStagger:0.#}초");
                    return false;
                }
            }
            Vector3 center = bodies[(int)BodyId.Chest].position + skillDir * 0.45f;
            var half = new Vector3(s.rookWidth * 0.5f + 0.1f, 0.6f, 0.4f);
            int n = Physics.OverlapBoxNonAlloc(center, half, skillOverlap, Quaternion.LookRotation(skillDir, Vector3.up), ~0, QueryTriggerInteraction.Ignore);
            Vector3 right = Vector3.Cross(Vector3.up, FlatDir(skillDir));
            for (int i = 0; i < n; i++)
            {
                if (!ColliderOwner.TryGetValue(skillOverlap[i], out var other) || other == null || other == this
                    || skillHitSet.Contains(other)) continue;
                skillHitSet.Add(other);
                float sideDot = Vector3.Dot(Flat(other.bodies[0].position - bodies[0].position), right);
                float side = Mathf.Abs(sideDot) < 0.02f ? (skillHitSet.Count % 2 == 0 ? 1f : -1f) : Mathf.Sign(sideDot);
                if (IsAllyOf(other))
                {
                    other.TakeHit(right * (side * 2f), 0f, 0f, false);
                    Log($"룩 → {other.DisplayName}(아군): 옆으로 비켜 밀림");
                    continue;
                }
                if (!IsEnemyOf(other)) continue;
                if (dashHits >= s.rookMaxHits)
                {
                    string stop = SkillHit(other, skillDir * 3f, false, "룩 돌파");
                    Fx(SkillFxKind.RookStop, other, SkillContact(other), skillDir, s.rookMaxHits + 1);
                    Log($"룩 → {other.DisplayName}: {s.rookMaxHits + 1}번째 → {stop}, 돌진 끝");
                    HaltDash();
                    RookRecovery();
                    return false;
                }
                dashHits++;
                string result = SkillHit(other, right * (side * s.rookSidePush) + skillDir * 1.5f + Vector3.up * s.rookUpPush, true, "룩 돌파");
                dashBase *= 1f - s.rookSlowPerHit;
                Fx(SkillFxKind.RookHit, other, SkillContact(other), skillDir, dashHits);
                Log($"룩 → {other.DisplayName}: {dashHits}번째 옆으로 튕김 → {result}, 룩 속도 {dashBase:0.0} m/s");
            }
            return true;
        }

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
                if (other == null || other == this || IsAllyOf(other) || !IsEnemyOf(other)) continue;
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
                    // See-through white, not the side's blue (R80): faint while aiming, firmer once fixed.
                    var line = new Color(1f, 1f, 1f, aimLocked ? 0.6f : 0.38f);
                    float length = s.rookSpeed * s.rookTime;
                    if (!rookAir && (aimLocked || OnFloor))
                    {
                        Vector3 end = foot + FlatDir(skillDir) * length;
                        SkillMarks.Segment(Mark(ref markA, "Rook line"), foot, end, line, aimLocked ? s.rookWidth : s.rookWidth * 0.5f);
                        break;
                    }
                    // In the air the line leaves the chest along the aim, up or down; where it would meet the floor, a ring.
                    Vector3 from = bodies[(int)BodyId.Chest].position;
                    SkillMarks.Segment(Mark(ref markA, "Rook line"), from, from + skillDir * length, line, aimLocked ? 0.32f : 0.2f, false);
                    if (FloorAlong(from, skillDir, length, out var land))
                        SkillMarks.Circle(Mark(ref markB, "Rook landing"), land, 0.6f, line, 0.07f);
                    break;
                }
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
                case PieceKind.Knight when skillStage == SkillStage.Active:
                {
                    // Where it comes down: until a second F sends it at a head, or the tap on the head is made (then
                    // the head is where it comes down: one set of marks, not two, R75). R82: a white see-through mark
                    // turning on the floor ("원형 모형으로 회전") in place of the two blue circles; its inner ring draws
                    // in round the knight's shadow as it comes down.
                    if (!knightHoming && !knightTapped)
                    {
                        float high = Mathf.Clamp01((bodies[0].position.y - knightSpot.y - standHeight) / 2.5f);
                        SkillMarks.Reticle(Disc(ref markLanding, "Knight landing"), Disc(ref markLandingInner, "Knight landing inner"),
                            knightSpot, s.knightLandRadius, 0.55f * (1f + 0.35f * high), Time.time);
                    }
                    var marked = KnightMarked;
                    if (marked != null)
                    {
                        // The enemy a second F comes down on, in the target colour (not the knight's own): a ring at
                        // its feet and a pointer over its head, blinking while only found, steady once on the way.
                        float a = knightHoming ? 1f : 0.55f + 0.45f * Mathf.Sin(Time.time * 14f);
                        var lockColor = new Color(SkillMarks.Target.r, SkillMarks.Target.g, SkillMarks.Target.b, a);
                        Vector3 hip = marked.bodies[0].position;
                        Vector3 feet = new Vector3(hip.x, marked.groundFound ? marked.groundY : hip.y - marked.standHeight, hip.z);
                        SkillMarks.Circle(Mark(ref markC, "Knight lock"), feet, 0.5f, lockColor, knightHoming ? 0.12f : 0.07f);
                        Vector3 over = marked.bodies[(int)BodyId.Head].position + Vector3.up * 0.4f;
                        SkillMarks.Segment(Mark(ref markD, "Knight lock pointer"), over + Vector3.up * 0.35f, over, lockColor, 0.07f, false);
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
            if (QueenHillSkills != null)
            {
                QueenHillPose(ref armL, ref armR, ref chest, ref head, ref thighL, ref thighR, ref footL, ref footR);   // R89
                return;
            }
            if (piece == PieceKind.Rook && rookAir && dashing)
            {
                // Superman (R81): both fists out ahead over the head, legs straight behind, toes pointed, looking
                // where it flies.
                armL = Quaternion.Euler(0f, 0f, -80f);
                armR = Quaternion.Euler(0f, 0f, 80f);
                chest = Quaternion.identity;
                head = Quaternion.Euler(-35f, 0f, 0f);
                thighL = Quaternion.Euler(4f, 0f, 0f);
                thighR = Quaternion.Euler(-4f, 0f, 0f);
                footL = footR = Quaternion.Euler(30f, 0f, 0f);
                return;
            }
            if (piece == PieceKind.Rook && skillStage != SkillStage.None && (rookAir || !OnFloor))
            {
                // +1 = going up, -1 = going down: the charge's line while charging or holding, the rise or fall otherwise.
                float t = rookAir && (dashing || aimLocked)
                    ? Mathf.Clamp(skillDir.y * 1.6f, -1f, 1f)
                    : Mathf.Clamp(bodies[0].linearVelocity.y / 4f, -1f, 1f);
                Quaternion midL = Quaternion.Euler(0f, 90f, -10f), midR = Quaternion.Euler(0f, -90f, 10f);
                if (t >= 0f)
                {
                    armL = Quaternion.Slerp(midL, Quaternion.Euler(0f, 0f, -72f), t);
                    armR = Quaternion.Slerp(midR, Quaternion.Euler(0f, 0f, 72f), t);
                    chest = Quaternion.Slerp(Quaternion.Euler(10f, 0f, 0f), Quaternion.Euler(-10f, 0f, 0f), t);
                    head = Quaternion.Slerp(Quaternion.Euler(4f, 0f, 0f), Quaternion.Euler(-22f, 0f, 0f), t);
                    thighL = thighR = Quaternion.Slerp(Quaternion.Euler(12f, 0f, 0f), Quaternion.Euler(6f, 0f, 0f), t);
                }
                else
                {
                    float k = -t;
                    armL = Quaternion.Slerp(midL, Quaternion.Euler(0f, 90f, 38f), k);
                    armR = Quaternion.Slerp(midR, Quaternion.Euler(0f, -90f, -38f), k);
                    chest = Quaternion.Slerp(Quaternion.Euler(10f, 0f, 0f), Quaternion.Euler(28f, 0f, 0f), k);
                    head = Quaternion.Slerp(Quaternion.Euler(4f, 0f, 0f), Quaternion.Euler(32f, 0f, 0f), k);
                    thighL = thighR = Quaternion.Slerp(Quaternion.Euler(12f, 0f, 0f), Quaternion.Euler(38f, 0f, 0f), k);
                }
                footL = footR = Quaternion.Euler(12f, 0f, 0f);
                return;
            }
            if (piece == PieceKind.Knight && knightKick > 0f && skillStage == SkillStage.Active)
            {
                float k = Mathf.Clamp01(knightKick / 0.3f);
                thighL = Quaternion.Slerp(thighL, Quaternion.Euler(52f, 0f, 0f), k);
                thighR = Quaternion.Slerp(thighR, Quaternion.Euler(40f, 0f, 0f), k);
                footL = footR = Quaternion.Slerp(footL, Quaternion.Euler(20f, 0f, 0f), k);
                armL = Quaternion.Slerp(armL, Quaternion.Euler(0f, 90f, -25f), k);
                armR = Quaternion.Slerp(armR, Quaternion.Euler(0f, -90f, 25f), k);
                chest = Quaternion.Slerp(chest, Quaternion.Euler(20f, 0f, 0f), k);
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
