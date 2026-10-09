using System;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>A moment of a Queen of the Hill skill, for the test bed's effects (QueenHillSkillFx).</summary>
    public enum QueenHillFxKind : byte
    {
        KingWindup, KingWard, WardOn, WardBlock,
        QueenLock, QueenSlash, QueenHit, QueenWall,
        RookRequest, RookAccept, RookCancel, RookSwap, RookLand,
        BishopRise, BishopThrow, BishopImpact, BishopHit, BishopDrop, BishopLand,
        KnightLock, KnightLeap, KnightLand, KnightFlatten, KnightFall,
        PawnCrouch, PawnDash, PawnBump, PawnStop,
        /// <summary>A skill floored <c>target</c> (the effects stamp the captured square under it once it lies).</summary>
        Down,
    }

    public struct QueenHillFxEvent
    {
        public QueenHillFxKind kind;
        public RagdollPawn by, target;
        /// <summary>Where it happened (the floor under a cast or a landing, the contact of a hit).</summary>
        public Vector3 at, dir, to;
        /// <summary>A radius, a length or a time.</summary>
        public float size;
        public int count;
    }

    /// <summary>
    /// The Queen of the Hill piece skills (design doc §7, R89: 승규 picked 1.B 2.A 3.B 4.B 5.B 6.B on the previz page).
    /// King "근접 호위": allies close by shrug off knockdowns and strong pushes for 2 s; the king himself does not (B).
    /// Queen "팔방 검격": a long slash down one aimed line, with a windup and a recovery, stopped by walls (A); R93: the
    /// sword comes up from low behind on the right to high on the left; R97: the slash runs along the floor like its
    /// warning squares, down drops and up steps of a tier, taking whoever stands on its way. Rook "캐슬링 교대": swap places with any ally close by (B: not only the
    /// king), once it agrees; the path and both spots must be clear. Bishop "교차 공중 포격": hover up, throw a shot at the
    /// aimed spot, an X of diagonals there knocks enemies down and off walls (B); R95: two shots a hover. Knight
    /// "도약 압착": leap to a chosen spot at most one tier up, enemies in the landing circle are flattened 0.7 s and
    /// pushed out, and fall if that takes them off a ledge (B); R93: a higher arc, and an enemy close to the aimed spot
    /// is caught by it: the knight comes down on its head. Pawn
    /// "비집고 돌파": crouch low and dash a short way, no invincibility, no passing walls (B: the crouch is a new pose:
    /// the legs split front and back so the hips come down while the feet stay on the floor).
    ///
    /// Off unless the Queen of the Hill skill test bed hands a pawn its numbers (<see cref="QueenHillSkills"/>). It runs
    /// on the Pawn Rush skills' machinery (stages, cooldown, dashes, the aim's clicks): the bed also gives the pawn a
    /// <see cref="PawnRushSkills"/> object, which switches those hooks on, and this branch takes over from there.
    /// All on F (temporary); the queen, the rook and the knight aim first: F starts the aim, the left click goes, the
    /// right click or F again calls it off. The bishop's F rises; it aims in the air and throws on the left click.
    /// Offline only: the network packets do not carry the skill key, the castling answer would go through the host.
    /// </summary>
    public partial class RagdollPawn
    {
        /// <summary>The Queen of the Hill skills' numbers; null = this pawn runs the Pawn Rush skills (or none).</summary>
        public QueenHillSkillParams QueenHillSkills { get; set; }

        /// <summary>Played by the test bed, not a person: answers a rook's castling request by itself.</summary>
        public bool SkillBot { get; set; }

        /// <summary>Every moment a Queen of the Hill skill lands, for the test bed's effects; nothing else listens.</summary>
        public static event Action<QueenHillFxEvent> QueenHillFx;

        void QhFx(QueenHillFxKind kind, RagdollPawn target, Vector3 at, Vector3 dir, float size = 0f, int count = 0, Vector3 to = default)
            => QueenHillFx?.Invoke(new QueenHillFxEvent { kind = kind, by = this, target = target, at = at, dir = dir, to = to, size = size, count = count });

        // The king's guard on this piece.
        float wardLeft, wardTotal;
        RagdollPawn wardBy;
        // Crouching (the pawn's dash): 0 standing .. 1 low.
        float qhCrouch, crouchAngle = 35f;
        // Flying an arc to a spot (the castling swap, the knight's leap).
        bool flying, flySoft;
        Vector3 flyFrom, flyTo;
        float flyT, flyTime, flyUp, flyGravity = 9.81f, partnerApart;
        RagdollPawn flyPartner, apartFrom;
        // The aim (queen, rook, bishop, knight).
        Vector3 qhPoint;
        bool qhValid;
        RagdollPawn qhTarget;
        string qhWhy = "";
        // Queen: the slash runs along the floor (R97), sampled every SlashPathStep metres from her feet.
        Vector3 slashFrom, slashDir = Vector3.forward, slashStop;
        float slashReach;
        bool slashBlocked;
        readonly System.Collections.Generic.List<Vector3> slashPath = new System.Collections.Generic.List<Vector3>();
        public const float SlashPathStep = 0.25f;
        // Bishop: shots still to throw in this hover (R95: two).
        int shotsLeft;
        // Rook: the request out, and one come in.
        RagdollPawn castleAsker;
        float castleAskAge, castleWait;
        // Bishop
        float hoverHipY, hoverLeft, hoverTotal, hoverLinger;
        Vector3 hoverMove, hoverVel;
        Vector3 shellFrom, shellTo, shotYaw = Vector3.forward;
        float shellT, shellTime;
        bool shellOut, shellLanded;
        // Knight: the spot it leaps to (or the enemy whose head it comes down on, R93); a flattened piece watching for
        // the fall off its ledge.
        Vector3 leapSpot, flattenSlide;
        float flattenWatch, flattenFloor, flattenSlideLeft;
        RagdollPawn flattenBy, leapTarget, flyHome, stompedOne;
        bool knightLanded;
        float stompedLeft;

        public float WardLeft => Mathf.Max(0f, wardLeft);
        public float WardTotal => wardTotal;
        public RagdollPawn WardBy => wardLeft > 0f ? wardBy : null;
        public float QhCrouch => qhCrouch;
        public bool QhFlying => flying;
        public Vector3 QhFlyFrom => flyFrom;
        public Vector3 QhFlyTo => flyTo;
        public float QhFlyProgress => flying && flyTime > 0f ? Mathf.Clamp01(flyT / flyTime) : 0f;
        public float QhFlyTime => flyTime;
        public RagdollPawn QhFlyPartner => flying ? flyPartner : null;
        /// <summary>Where the aim points (the knight's landing, the bishop's target), on the floor.</summary>
        public Vector3 QhAimPoint => qhPoint;
        public bool QhAimValid => qhValid;
        /// <summary>Why the aim cannot go there (empty if it can).</summary>
        public string QhAimWhy => qhWhy;
        /// <summary>The ally a rook's aim has picked, or asked.</summary>
        public RagdollPawn QhAimTarget => qhTarget;
        /// <summary>The rook that asked this piece to swap and waits for its answer.</summary>
        public RagdollPawn CastleAsker => castleAsker;
        public float CastleAskAge => castleAskAge;
        public bool QhAimLocked => aimLocked;
        public Vector3 SlashFrom => slashFrom;
        public Vector3 SlashDir => slashDir;
        public float SlashReach => slashReach;
        public bool SlashBlocked => slashBlocked;
        public Vector3 SlashStop => slashStop;
        /// <summary>The floor the queen's slash runs along (R97): a point every <see cref="SlashPathStep"/> m from her feet,
        /// down any drop and up steps of at most queenClimb, to where it ends or a wall stops it.</summary>
        public System.Collections.Generic.IReadOnlyList<Vector3> SlashPath => slashPath;

        /// <summary>The floor of a slash's way <paramref name="along"/> m from its start, between its points (a slope
        /// is a straight run between them).</summary>
        public static float SlashPathFloor(System.Collections.Generic.IReadOnlyList<Vector3> path, float along)
        {
            if (path == null || path.Count == 0) return 0f;
            float at = Mathf.Clamp(along / SlashPathStep, 0f, path.Count - 1);
            int a = Mathf.FloorToInt(at), b = Mathf.Min(a + 1, path.Count - 1);
            return Mathf.Lerp(path[a].y, path[b].y, at - a);
        }

        /// <summary>The floor under <paramref name="p"/> as the queen's slash finds its own (R98): the first one below a
        /// step's height (queenClimb) over <paramref name="near"/>, or <paramref name="near"/> if there is none. Her
        /// way, the squares that lie on it and who is standing on them are all found with it.</summary>
        public float SlashFloorAt(Vector3 p, float near)
        {
            float climb = Mathf.Max(0.1f, QueenHillSkills != null ? QueenHillSkills.queenClimb : 1.05f), probe = near + climb + 0.1f;
            return SolidRay(new Vector3(p.x, probe, p.z), Vector3.down, climb + 0.1f + 40f, out var floor) && floor.normal.y >= 0.5f
                ? floor.point.y : near;
        }

        public float HoverLeft => Mathf.Max(0f, hoverLeft);
        public float HoverTotal => hoverTotal;
        public bool ShellOut => shellOut;
        public Vector3 ShellFrom => shellFrom;
        public Vector3 ShellTo => shellTo;
        public float ShellProgress => shellTime > 0f ? Mathf.Clamp01(shellT / shellTime) : 0f;
        public Vector3 ShotYaw => shotYaw;
        public Vector3 LeapSpot => leapSpot;
        /// <summary>The enemy the knight is coming down on (its head), or null for a plain spot.</summary>
        public RagdollPawn LeapTarget => leapTarget;
        public Vector3 SkillDirection => skillDir;
        /// <summary>Which way the queen's sword points (world), for its effects (R93: the swing comes up from below).</summary>
        public Vector3 QueenBladeDir => FacingFrame(QueenBladeLocal(QueenHillSkills, out _));
        /// <summary>How far along the queen's swing is: -1 aiming, 0 the low guard, 0.5 straight ahead (the slash
        /// goes), 1 the top of the follow-through.</summary>
        public float QueenSwingAlong
        {
            get
            {
                QueenBladeLocal(QueenHillSkills, out float along);
                return along;
            }
        }
        /// <summary>The floor under the feet (the ground probe's, or under the hips).</summary>
        public Vector3 FeetPoint
        {
            get
            {
                Vector3 h = bodies[0].position;
                return new Vector3(h.x, groundFound ? groundY : h.y - standHeight, h.z);
            }
        }

        /// <summary>The Queen of the Hill skill's name.</summary>
        public static string QueenHillSkillName(PieceKind kind) => kind switch
        {
            PieceKind.King => "근접 호위",
            PieceKind.Queen => "팔방 검격",
            PieceKind.Rook => "캐슬링 교대",
            PieceKind.Bishop => "교차 공중 포격",
            PieceKind.Knight => "도약 압착",
            _ => "비집고 돌파",
        };

        /// <summary>Pieces that aim before they go: F starts the aim, the left click fires, the right click calls it off.</summary>
        bool QueenHillAims => piece == PieceKind.Queen || piece == PieceKind.Rook || piece == PieceKind.Knight || piece == PieceKind.Bishop;

        /// <summary>The crouch's part of the hips' height (Locomotion): the legs split front and back by crouchAngle,
        /// so the hips come down by exactly what a straight leg swung that far loses in height.</summary>
        float SkillCrouchDrop => qhCrouch > 0f ? legReach * (1f - Mathf.Cos(crouchAngle * qhCrouch * Mathf.Deg2Rad)) : 0f;

        /// <summary>0..1: how much of the run's lift and bob the crouch takes away (the hips stay down while dashing).</summary>
        float SkillCrouch => qhCrouch;

        float QueenHillCooldown(QueenHillSkillParams s) => piece switch
        {
            PieceKind.King => s.kingCooldown,
            PieceKind.Queen => s.queenCooldown,
            PieceKind.Rook => s.rookCooldown,
            PieceKind.Bishop => s.bishopCooldown,
            PieceKind.Knight => s.knightCooldown,
            _ => s.pawnCooldown,
        };

        // ---------------------------------------------------------------- hooks

        /// <summary>Before the jump and the run (PreSkills): what each skill's stage lets the keys do.</summary>
        void PreQueenHillSkills()
        {
            if (Aiming) input.sprint = false;   // aiming, the queen, the rook and the knight walk, no sprint
            bool rooted = flying || dashing || castleAsker != null && SkillBot
                          || skillStage == SkillStage.Recovery
                          || skillStage == SkillStage.Windup && (piece == PieceKind.King || piece == PieceKind.Pawn
                                                                 || piece == PieceKind.Bishop || aimLocked);
            // R105: the hovering bishop does not walk, but its move keys slide the hover (Hover).
            hoverMove = piece == PieceKind.Bishop && skillStage == SkillStage.Windup ? input.move : Vector3.zero;
            if (!rooted) return;
            input.move = Vector3.zero;
            input.jump = false;
            input.sprint = false;
            input.shove = false;
        }

        /// <summary>After the run (UpdateSkills): the Queen of the Hill skills.</summary>
        void UpdateQueenHillSkills(QueenHillSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            wardLeft -= dt;
            if (wardLeft <= 0f) wardBy = null;
            UpdateFlattenFall(dt);
            if (partnerApart > 0f)
            {
                partnerApart -= dt;
                if (partnerApart <= 0f) IgnorePartner(apartFrom, false);
            }
            if (stompedOne != null)
            {
                // The piece the knight came down on: the two collide again once they are apart (together, the solver
                // threw the squashed one into the air), or after a while whatever happens.
                stompedLeft -= dt;
                if (stompedLeft <= 0f || Flat(stompedOne.bodies[0].position - bodies[0].position).magnitude > 0.62f)
                {
                    IgnorePartner(stompedOne, false);
                    stompedOne = null;
                }
            }
            // A castling request to this piece: its own skill key answers yes (a test dummy answers by itself).
            if (castleAsker != null) press = AnswerCastle(s, press);
            if (flying) Fly(dt);

            if (skillStage != SkillStage.None && (State == PawnState.Ragdoll || Floating))
            {
                // Floored (or in the water) mid-skill: it ends here, and the cooldown runs if it had begun.
                Log($"{DisplayName}: {QueenHillSkillName(piece)} 끊김 ({(Floating ? "물" : "넘어짐")})");
                bool begun = !(skillStage == SkillStage.Windup && !aimLocked && QueenHillAims && piece != PieceKind.Bishop)
                             && !(piece == PieceKind.Rook && skillStage == SkillStage.Windup);
                ClearSkills();
                if (begun) StartCooldown(QueenHillCooldown(s));
            }

            switch (piece)
            {
                case PieceKind.King: UpdateKingWard(s, press, dt); break;
                case PieceKind.Queen: UpdateQueenSlash(s, press, confirm, cancel, dt); break;
                case PieceKind.Rook: UpdateRookCastle(s, press, confirm, cancel, dt); break;
                case PieceKind.Bishop: UpdateBishopVolley(s, press, confirm, cancel, dt); break;
                case PieceKind.Knight: UpdateKnightPress(s, press, confirm, cancel, dt); break;
                default: UpdatePawnSqueeze(s, press, dt); break;
            }
            UpdateCrouch(s);
        }

        /// <summary>Everything a Queen of the Hill skill leaves running (ClearSkills: a respawn, a piece switch, a fall).</summary>
        void ClearQueenHill()
        {
            if (flying) EndFly(false);
            if (castleAsker != null) castleAsker = null;
            qhTarget = null;
            qhValid = false;
            qhWhy = "";
            qhCrouch = 0f;
            slashBlocked = false;
            shellOut = shellLanded = false;
            hoverLeft = hoverLinger = 0f;
            leapTarget = flyHome = null;
            knightLanded = false;
        }

        /// <summary>What a Queen of the Hill skill does to the piece it hits: a knockdown is the skill's to give; the
        /// king's guard turns it into a short stagger and a much smaller push (근접 호위).</summary>
        string QhHit(RagdollPawn target, Vector3 push, bool knockdown, string cause, QueenHillFxKind kind, Vector3 at)
        {
            if (target == null || target.NetworkPuppet) return "-";
            if (target.wardLeft > 0f && target.State != PawnState.Ragdoll)
            {
                // (TakeHit itself scales a guarded piece's push down: WardPushScale.)
                target.TakeHit(Flat(push), 0f, 0f, false);
                target.Stagger(target.QueenHillSkills != null ? target.QueenHillSkills.wardStagger : 0.2f);
                target.LastSkillHit = $"{cause}: 호위로 버팀";
                QhFx(kind, target, at, Flat(push).normalized, 0f, 1);
                QhFx(QueenHillFxKind.WardBlock, target, at, Flat(push).normalized);
                return "호위로 버팀 (밀림만 조금)";
            }
            string result = SkillHit(target, push, knockdown, cause);
            QhFx(kind, target, at, Flat(push).normalized);
            if (result == "넘어짐")
            {
                // Down for a while, long enough to read (the ragdoll's own get-up was a third of a second for the king).
                var s = QueenHillSkills;
                if (s != null && s.downHold > 0f) target.Knockdown(cause, s.downHold);
                QhFx(QueenHillFxKind.Down, target, target.FeetPoint, Flat(push).normalized);
            }
            return result;
        }

        /// <summary>Guarded by the king: knockdowns from anything (a tackle, a bump) become a short stagger.</summary>
        bool WardCatchesKnockdown(string cause)
        {
            if (wardLeft <= 0f || State == PawnState.Ragdoll || Floating) return false;
            Stagger(QueenHillSkills != null ? QueenHillSkills.wardStagger : 0.2f);
            LastSkillHit = $"{cause}: 호위로 버팀";
            if (wardBy != null) wardBy.QhFx(QueenHillFxKind.WardBlock, this, bodies[(int)BodyId.Chest].position, Flat(facing));
            return true;
        }

        /// <summary>Guarded by the king: pushes (TakeHit) are this much smaller.</summary>
        float WardPushScale => wardLeft > 0f ? (QueenHillSkills != null ? QueenHillSkills.wardPush : 0.25f) : 1f;

        // ---------------------------------------------------------------- king: close guard (B)

        void UpdateKingWard(QueenHillSkillParams s, bool press, float dt)
        {
            switch (skillStage)
            {
                case SkillStage.None:
                    if (press && CanStartSkill())
                    {
                        SkillUses++;
                        skillStage = SkillStage.Windup;
                        stageTime = 0f;
                        SkillDetail = "호위 준비 (홀을 듦)";
                        QhFx(QueenHillFxKind.KingWindup, null, FeetPoint, Flat(facing), s.kingRadius, 0, Vector3.up * s.kingWindup);
                    }
                    break;
                case SkillStage.Windup:
                    stageTime += dt;
                    if (stageTime >= s.kingWindup)
                    {
                        KingWard(s);
                        skillStage = SkillStage.Recovery;
                        stageTime = 0f;
                        SkillDetail = "후딜";
                    }
                    break;
                case SkillStage.Recovery:
                    stageTime += dt;
                    if (stageTime >= s.kingRecovery)
                    {
                        skillStage = SkillStage.None;
                        SkillDetail = "";
                        StartCooldown(s.kingCooldown);
                    }
                    break;
            }
        }

        void KingWard(QueenHillSkillParams s)
        {
            Vector3 c = bodies[0].position;
            int n = 0;
            string names = "";
            foreach (var other in All)
            {
                // B (R89): the king's own body is left out, so in the second half the target can still be beaten.
                if (other == null || other == this || other.NetworkPuppet || !IsAllyOf(other) || other.State == PawnState.Ragdoll) continue;
                Vector3 d = other.bodies[0].position - c;
                if (Flat(d).magnitude > s.kingRadius || Mathf.Abs(d.y) > s.kingHeight) continue;
                other.wardLeft = other.wardTotal = s.wardTime;
                other.wardBy = this;
                n++;
                names += (names.Length > 0 ? ", " : "") + other.DisplayName;
                QhFx(QueenHillFxKind.WardOn, other, other.FeetPoint, Flat(d).normalized, s.wardTime);
            }
            QhFx(QueenHillFxKind.KingWard, null, FeetPoint, Flat(facing), s.kingRadius, n);
            Log($"킹: 근접 호위 — 아군 {n}명 {s.wardTime:0.#}초 ({(n > 0 ? names : "범위 안에 없음")}) · 킹 자신은 빠짐");
        }

        // ---------------------------------------------------------------- queen: one long slash (A)

        void UpdateQueenSlash(QueenHillSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (skillStage)
            {
                case SkillStage.None:
                    if (press && CanStartSkill())
                    {
                        skillStage = SkillStage.Windup;
                        stageTime = 0f;
                        aimLocked = false;
                        skillGrabLatch = skillGrabRaw;
                        slashDir = AimFlat();
                        SlashLine(s);
                        SkillDetail = "조준 (마우스로 방향 · 좌클릭 베기 · 우클릭/F 취소)";
                    }
                    break;
                case SkillStage.Windup:
                    stageTime += dt;
                    if (!aimLocked)
                    {
                        slashDir = AimFlat();
                        if (Flat(skillMoveRaw).sqrMagnitude < 0.04f) facing = slashDir;
                        SlashLine(s);
                        if (cancel || press)
                        {
                            if (cancel) skillGrabLatch = true;
                            ClearSkills();
                            Log("퀸: 조준 취소");
                            break;
                        }
                        if (confirm)
                        {
                            SkillUses++;
                            aimLocked = true;
                            stageTime = 0f;
                            facing = slashDir;
                            SkillDetail = "준비 동작 (칼을 듦)";
                            QhFx(QueenHillFxKind.QueenLock, null, slashFrom, slashDir, slashReach, 0, Vector3.up * s.queenWindup);
                        }
                        break;
                    }
                    facing = slashDir;
                    if (stageTime >= s.queenWindup)
                    {
                        SlashLine(s);
                        skillHitSet.Clear();
                        skillStage = SkillStage.Active;
                        stageTime = 0f;
                        SkillDetail = "검격";
                        QhFx(QueenHillFxKind.QueenSlash, null, slashFrom, slashDir, slashReach, slashBlocked ? 1 : 0, slashStop);
                    }
                    break;
                case SkillStage.Active:
                {
                    stageTime += dt;
                    float k = Mathf.Clamp01(stageTime / Mathf.Max(0.01f, s.queenTravel));
                    QueenSlashHits(s, slashReach * k);
                    if (k >= 1f)
                    {
                        if (slashBlocked)
                        {
                            QhFx(QueenHillFxKind.QueenWall, null, slashStop, slashDir);
                            Log($"퀸: 검기가 {slashReach:0.0} m에서 벽에 막힘");
                        }
                        skillStage = SkillStage.Recovery;
                        stageTime = 0f;
                        SkillDetail = "회복 (빈틈)";
                    }
                    break;
                }
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

        /// <summary>The slash's way along the aim (R97, 승규 님: "위에 있고 적이 아래에 있으면 아래 적이 맞아야, 마지막 칸에 있는
        /// 사람 쪽으로 보내져야 돼, 그 반대도"): it runs along the floor the way its warning squares lie — down any drop, up
        /// steps of at most queenClimb (a tier of the hill) — and a face taller than that (the stone wall) stops it.
        /// (R95 flew it straight at her height instead; that was not what was asked and is undone.)</summary>
        void SlashLine(QueenHillSkillParams s)
        {
            Vector3 chest = bodies[(int)BodyId.Chest].position;
            slashFrom = chest;
            Vector3 feet = FeetPoint;
            slashPath.Clear();
            slashPath.Add(feet);
            slashReach = s.queenLength;
            slashBlocked = false;
            float level = feet.y, climb = Mathf.Max(0.1f, s.queenClimb);
            int n = Mathf.CeilToInt(s.queenLength / SlashPathStep);
            for (int i = 1; i <= n; i++)
            {
                float was = (i - 1) * SlashPathStep, d = Mathf.Min(i * SlashPathStep, s.queenLength);
                float probe = level + climb + 0.1f;
                Vector3 prev = feet + slashDir * was, here = feet + slashDir * d;
                // A face taller than a step between the last point and this one: a wall, the slash stops at it.
                if (SolidRay(new Vector3(prev.x, probe, prev.z), slashDir, d - was, out var wall) && Mathf.Abs(wall.normal.y) < 0.6f)
                {
                    slashReach = Mathf.Max(0.3f, was + wall.distance - 0.1f);
                    slashBlocked = true;
                    slashStop = wall.point;
                    if (wall.distance > 0.15f) slashPath.Add(new Vector3(prev.x, level, prev.z) + slashDir * (wall.distance - 0.1f));
                    return;
                }
                // The floor here: the first one below a step's height over the slash's floor (up a step or a slope, down a
                // drop).
                level = SlashFloorAt(here, level);
                slashPath.Add(new Vector3(here.x, level, here.z));
            }
            slashStop = slashPath[slashPath.Count - 1] + Vector3.up * Mathf.Max(0.3f, chest.y - feet.y);
        }

        /// <summary>The lowest and highest floor of the slash's way around <paramref name="along"/> m from her feet (the two
        /// points either side: a piece on a step's edge counts on either level).</summary>
        void SlashFloorAround(float along, out float low, out float high)
        {
            if (slashPath.Count == 0) { low = high = FeetPoint.y; return; }
            int a = Mathf.Clamp(Mathf.FloorToInt(along / SlashPathStep), 0, slashPath.Count - 1);
            int b = Mathf.Clamp(a + 1, 0, slashPath.Count - 1);
            low = Mathf.Min(slashPath[a].y, slashPath[b].y);
            high = Mathf.Max(slashPath[a].y, slashPath[b].y);
        }

        void QueenSlashHits(QueenHillSkillParams s, float front)
        {
            foreach (var other in All)
            {
                if (other == null || other == this || other.NetworkPuppet || !IsEnemyOf(other) || skillHitSet.Contains(other)) continue;
                Vector3 start = slashPath.Count > 0 ? slashPath[0] : FeetPoint;
                Vector3 rel = other.bodies[0].position - start;
                float along = Vector3.Dot(Flat(rel), slashDir);
                if (along < -0.3f || along > front + 0.25f || along > slashReach + 0.2f) continue;
                float across = (Flat(rel) - slashDir * along).magnitude;
                if (across > s.queenWidth * 0.5f + 0.3f) continue;
                // On the slash's way (R97): standing on the floor it runs along there — a tier below or above her included —
                // or jumping over it. R98: or on the floor right under the piece, found the same way (a slope tilted
                // across the line, the lower floor beside a step's edge): its square lies there too.
                SlashFloorAround(Mathf.Max(0f, along), out float low, out float high);
                float under = SlashFloorAt(other.bodies[0].position, SlashPathFloor(slashPath, Mathf.Max(0f, along)));
                low = Mathf.Min(low, under);
                high = Mathf.Max(high, under);
                float feet = other.FeetPoint.y;
                if (feet < low - s.queenDrop || feet > high + s.queenHeight) continue;
                skillHitSet.Add(other);
                string result = QhHit(other, slashDir * s.queenPush + Vector3.up * s.queenLift, true, "퀸 팔방 검격",
                    QueenHillFxKind.QueenHit, other.bodies[(int)BodyId.Chest].position);
                Log($"퀸 → {other.DisplayName}: {along:0.0} m → {result}");
            }
        }

        // ----- the queen's sword: one swing from below (R93, 승규 님: "검을 아래에서 위로, 동작도 자연스럽게")
        //
        // The sword is no longer the forearm carried on (hanging, that went through the floor): its direction is worked
        // out here, in a plane tilted 35° left of upright through her front, low behind on the right (SwingLow) →
        // straight ahead (0°, the moment the slash goes) → high in front on the left (SwingHigh). The arm reaches along
        // it, the chest turns from right to left with it, and the effects draw the blade the same way.

        public const float SwingLow = -150f, SwingHigh = 70f;

        /// <summary>A direction of the swing in the queen's own frame (x right, y up, z ahead).</summary>
        public static Vector3 SwingDir(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector3(0f, 0f, 1f) * Mathf.Cos(a) + new Vector3(-0.57f, 0.82f, 0f) * Mathf.Sin(a);
        }

        /// <summary>The swing's angle at <paramref name="u"/> (0 low guard, 0.5 straight ahead, 1 top): speeding up
        /// to the release, slowing into the top.</summary>
        static float SwingAngle(float u)
        {
            if (u <= 0.5f)
            {
                float x = Mathf.Clamp01(u / 0.5f);
                return Mathf.Lerp(SwingLow, 0f, x * x);
            }
            float y = Mathf.Clamp01((u - 0.5f) / 0.5f);
            return Mathf.Lerp(0f, SwingHigh, 1f - (1f - y) * (1f - y));
        }

        static float Ease3(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        /// <summary>Where the queen's sword points in her own frame (x right, y up, z ahead), and how far along the swing
        /// she is (-1 aiming, 0 low guard .. 0.5 the release .. 1 the top).</summary>
        Vector3 QueenBladeLocal(QueenHillSkillParams s, out float along)
        {
            // Aiming: held up in front, a little to the sword side.
            var ready = new Vector3(0.35f, 0.55f, 0.76f).normalized;
            along = -1f;
            if (s == null || piece != PieceKind.Queen || skillStage == SkillStage.None || skillStage == SkillStage.Windup && !aimLocked)
                return ready;
            float swing = Mathf.Max(0.04f, s.queenSwing);
            if (skillStage == SkillStage.Windup)
            {
                // The swing starts half its time before the slash goes, so the blade passes straight ahead right then.
                float u = (stageTime - s.queenWindup) / swing + 0.5f;
                if (u < 0f)
                {
                    // Down into the low guard behind on the right in 0.2 s, then trembling a little with the charge.
                    float tremble = 4f * Mathf.Sin(stageTime * 45f) * Mathf.Clamp01((stageTime - 0.2f) / 0.1f);
                    along = 0f;
                    return Vector3.Slerp(ready, SwingDir(SwingLow + tremble), Ease3(stageTime / 0.2f)).normalized;
                }
                along = Mathf.Clamp01(u);
                return SwingDir(SwingAngle(along));
            }
            if (skillStage == SkillStage.Active)
            {
                along = Mathf.Clamp01(0.5f + stageTime / swing);
                return SwingDir(SwingAngle(along));
            }
            // Recovery: held at the top a moment, then let down a little.
            along = 1f;
            float k = Ease3((stageTime - 0.15f) / Mathf.Max(0.1f, s.queenRecovery - 0.15f));
            return SwingDir(Mathf.Lerp(SwingHigh, 40f, k));
        }

        /// <summary>A direction in this piece's own frame (x right, y up, z ahead) turned into the world.</summary>
        Vector3 FacingFrame(Vector3 local)
        {
            Vector3 f = FlatDir(facing), r = Vector3.Cross(Vector3.up, f);
            return (r * local.x + Vector3.up * local.y + f * local.z).normalized;
        }

        /// <summary>The queen's body with the sword (R93): the chest turns from the right (low guard) to the left (top) and
        /// leans back as it goes up, the head keeps looking ahead, the sword arm reaches along the blade (the hand a
        /// little out and down from it), the other arm balances ahead on the left, then swings back.</summary>
        void QueenPose(ref Quaternion armL, ref Quaternion armR, ref Quaternion chest, ref Quaternion head)
        {
            Vector3 blade = QueenBladeLocal(QueenHillSkills, out float along);
            bool aiming = along < 0f;
            float t = aiming ? 0f : along;
            float yaw = aiming ? 10f : Mathf.Lerp(32f, -30f, t), pitch = aiming ? 0f : Mathf.Lerp(10f, -10f, t);
            Quaternion c = Quaternion.Euler(pitch, yaw, 0f);
            // While aiming she walks: half the chest's turn, and the free arm left to the walk.
            float w = aiming ? 0.5f : 1f;
            chest = Quaternion.Slerp(chest, c, w);
            head = Quaternion.Slerp(head, Quaternion.Euler(-pitch * 0.5f, -yaw * 0.8f, 0f), w);
            Quaternion inv = Quaternion.Inverse(Quaternion.Slerp(Quaternion.identity, c, w));
            armR = ArmToward(inv * (blade + new Vector3(0.35f, -0.45f, 0f)), false);
            if (aiming) return;
            Vector3 off = Vector3.Slerp(new Vector3(-0.55f, -0.1f, 0.83f), new Vector3(-0.6f, -0.45f, -0.66f), t);
            armL = ArmToward(inv * off, true);
        }

        /// <summary>The joint target that points an arm along <paramref name="d"/> (in the chest's frame: x right, y up,
        /// z ahead): Euler(0, yaw, raise) on the right arm's rest (pointing right), mirrored for the left.</summary>
        static Quaternion ArmToward(Vector3 d, bool left)
        {
            d = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
            float raise = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (left) return Quaternion.Euler(0f, Mathf.Atan2(d.z, -d.x) * Mathf.Rad2Deg, -raise);
            return Quaternion.Euler(0f, Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg, raise);
        }

        // ---------------------------------------------------------------- rook: castling swap (B)

        void UpdateRookCastle(QueenHillSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (skillStage)
            {
                case SkillStage.None:
                    if (press && CanStartSkill())
                    {
                        skillStage = SkillStage.Windup;
                        stageTime = 0f;
                        aimLocked = false;
                        skillGrabLatch = skillGrabRaw;
                        RookPick(s);
                        SkillDetail = "조준 (마우스로 아군 고르기 · 좌클릭 요청 · 우클릭/F 취소)";
                    }
                    break;
                case SkillStage.Windup:
                    stageTime += dt;
                    if (!aimLocked)
                    {
                        RookPick(s);
                        if (cancel || press)
                        {
                            if (cancel) skillGrabLatch = true;
                            ClearSkills();
                            Log("룩: 조준 취소");
                            break;
                        }
                        if (confirm)
                        {
                            if (qhTarget == null) { Log("룩: 조준선 근처에 아군이 없어요"); break; }
                            if (!qhValid) { Log($"룩: {qhTarget.DisplayName}와 바꿀 수 없어요 ({qhWhy})"); break; }
                            aimLocked = true;
                            stageTime = 0f;
                            castleWait = 0f;
                            qhTarget.castleAsker = this;
                            qhTarget.castleAskAge = 0f;
                            SkillDetail = $"{qhTarget.DisplayName}의 수락 기다림";
                            QhFx(QueenHillFxKind.RookRequest, qhTarget, qhTarget.FeetPoint, Flat(qhTarget.bodies[0].position - bodies[0].position).normalized, s.rookWait);
                            Log($"룩 → {qhTarget.DisplayName}: 교대 요청 (상대 PC → 방장 판정)");
                        }
                        break;
                    }
                    // Waiting for the ally's yes. It may walk off, fall or say nothing: then the request lapses (no cooldown).
                    castleWait += dt;
                    if (qhTarget != null) facing = FlatDir(qhTarget.bodies[0].position - bodies[0].position);
                    if (qhTarget == null || qhTarget.State != PawnState.Active || qhTarget.castleAsker != this || castleWait > s.rookWait + 0.1f
                        || Flat(qhTarget.bodies[0].position - bodies[0].position).magnitude > s.rookRange * 1.3f)
                    {
                        string why = qhTarget == null ? "아군 없음" : qhTarget.State != PawnState.Active ? "아군이 넘어짐" : castleWait > s.rookWait ? "수락 없음" : "멀어짐";
                        if (qhTarget != null && qhTarget.castleAsker == this) qhTarget.castleAsker = null;
                        QhFx(QueenHillFxKind.RookCancel, qhTarget, qhTarget != null ? qhTarget.FeetPoint : FeetPoint, Vector3.zero);
                        ClearSkills();
                        Log($"룩: 교대 취소 ({why}) — 쿨타임 없음");
                    }
                    break;
                case SkillStage.Active:
                    stageTime += dt;
                    if (!flying && (OnFloor || stageTime > flyTime + 0.8f))
                    {
                        skillStage = SkillStage.Recovery;
                        stageTime = 0f;
                        SkillDetail = "착지 후딜";
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

        /// <summary>The ally the aim points at: within rookRange and rookCone of the aim, the closest to the line.</summary>
        void RookPick(QueenHillSkillParams s)
        {
            Vector3 aim = AimFlat(), me = bodies[0].position;
            if (Flat(skillMoveRaw).sqrMagnitude < 0.04f) facing = aim;
            RagdollPawn best = null;
            float bestScore = float.MaxValue;
            foreach (var other in All)
            {
                if (other == null || other == this || other.NetworkPuppet || !IsAllyOf(other) || other.State != PawnState.Active || other.flying
                    || other.QueenHillSkills == null) continue;
                Vector3 d = Flat(other.bodies[0].position - me);
                float dist = d.magnitude;
                if (dist > s.rookRange || dist < 0.5f) continue;
                float angle = Vector3.Angle(d, aim);
                if (angle > s.rookCone) continue;
                float score = angle + dist * 3f;
                if (score >= bestScore) continue;
                best = other;
                bestScore = score;
            }
            qhTarget = best;
            qhValid = best != null && CastleClear(s, best, out qhWhy);
            if (best == null) qhWhy = "아군 없음";
        }

        /// <summary>Both spots must have room to stand in and the arc between them must be open (the castling's path check).</summary>
        bool CastleClear(QueenHillSkillParams s, RagdollPawn ally, out string why)
        {
            why = "";
            Vector3 a = FeetPoint, b = ally.FeetPoint;
            if (!RoomToStand(a, ally) || !RoomToStand(b, ally)) { why = "설 자리가 막힘"; return false; }
            float top = Mathf.Max(a.y, b.y) + s.rookArc;
            Vector3 prev = a + Vector3.up * (standHeight + 0.35f);
            for (int i = 1; i <= 12; i++)
            {
                float t = i / 12f;
                Vector3 p = Vector3.Lerp(a, b, t);
                p.y = Mathf.Lerp(a.y, b.y, t) + 4f * (top - Mathf.Lerp(a.y, b.y, t)) * t * (1f - t) * 0.92f + standHeight + 0.35f;
                if (i == 12) p = b + Vector3.up * (standHeight + 0.35f);
                Vector3 d = p - prev;
                if (BlockedBetween(prev, d, ally)) { why = "가는 길이 막힘"; return false; }
                prev = p;
            }
            return true;
        }

        bool BlockedBetween(Vector3 from, Vector3 d, RagdollPawn other)
        {
            float len = d.magnitude;
            if (len < 1e-3f) return false;
            int n = Physics.SphereCastNonAlloc(from, 0.2f, d / len, hits, len, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.distance <= 0f || ownSet.Contains(h.collider) || ColliderOwner.ContainsKey(h.collider) || PassesThrough(h.collider)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                return true;
            }
            return false;
        }

        bool RoomToStand(Vector3 feet, RagdollPawn other)
        {
            int n = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * 0.32f, feet + Vector3.up * 0.75f, 0.24f, skillOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = skillOverlap[i];
                if (ownSet.Contains(c) || ColliderOwner.ContainsKey(c) || PassesThrough(c)) continue;
                var rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                return false;
            }
            return true;
        }

        /// <summary>This piece has a castling request: yes on its own skill key (a person) or by itself after a moment
        /// (a test dummy: standing in for the other PC's answer going through the host). Returns the key press if it was
        /// not used for the answer.</summary>
        bool AnswerCastle(QueenHillSkillParams s, bool press)
        {
            var rook = castleAsker;
            castleAskAge += Time.fixedDeltaTime;
            if (rook == null || rook.skillStage != SkillStage.Windup || !rook.aimLocked || rook.qhTarget != this || State != PawnState.Active)
            {
                castleAsker = null;
                return press;
            }
            bool yes = SkillBot ? castleAskAge >= s.dummyAccept : press;
            if (!yes) return press;
            castleAsker = null;
            rook.StartCastleSwap(s, this);
            return false;
        }

        void StartCastleSwap(QueenHillSkillParams s, RagdollPawn ally)
        {
            Vector3 mine = FeetPoint, theirs = ally.FeetPoint;
            float top = Mathf.Max(mine.y, theirs.y) + s.rookArc;
            QhFx(QueenHillFxKind.RookAccept, ally, theirs, Flat(theirs - mine).normalized);
            ally.ClearSkills();
            ally.facing = FlatDir(mine - theirs);
            facing = FlatDir(theirs - mine);
            BeginFly(theirs + Vector3.up * (standHeight + 0.03f), top + standHeight, ally, true, s.rookSwapGravity);
            ally.BeginFly(mine + Vector3.up * (ally.standHeight + 0.03f), top + ally.standHeight, this, true, s.rookSwapGravity);
            SkillUses++;
            skillStage = SkillStage.Active;
            stageTime = 0f;
            SkillDetail = "자리 바꾸는 중";
            QhFx(QueenHillFxKind.RookSwap, ally, mine, Flat(theirs - mine).normalized, Mathf.Max(flyTime, ally.flyTime), 0, theirs);
            Log($"룩 ↔ {ally.DisplayName}: 수락 → 캐슬링 교대 ({Flat(theirs - mine).magnitude:0.0} m, 높이 차 {theirs.y - mine.y:+0.0;-0.0} m)");
        }

        // ---------------------------------------------------------------- flying an arc (castling, the knight)

        /// <summary>Fly the hips from where they are to <paramref name="toHips"/> on a thrown arc that peaks at
        /// <paramref name="apexY"/>, at gravity's own pace. The arc is flown exactly: every step sets the body's speed
        /// for where the arc will be at the end of that step (as the knight's head stomp does), so the ragdoll's drag
        /// and swinging limbs do not leave it short.</summary>
        void BeginFly(Vector3 toHips, float apexY, RagdollPawn partner, bool soft, float gravityScale = 1f)
        {
            Vector3 from = bodies[0].position;
            // A heavier "gravity" for the arc makes the same arc quicker (a snappier swap or leap); the steering below
            // flies whatever arc it is given.
            flyGravity = Mathf.Max(0.01f, -Physics.gravity.y) * Mathf.Max(0.2f, gravityScale);
            float g = flyGravity;
            float top = Mathf.Max(apexY, Mathf.Max(from.y, toHips.y) + 0.2f);
            float up = Mathf.Sqrt(2f * g * (top - from.y));
            float down = Mathf.Sqrt(2f * g * (top - toHips.y));
            flyTime = (up + down) / g;
            flyUp = up;
            flyFrom = from;
            flyTo = toHips;
            flyT = 0f;
            flying = true;
            flySoft = soft;
            flyPartner = partner;
            if (partner != null) IgnorePartner(partner, true);
            Launch(FlyVelocity(Time.fixedDeltaTime));
        }

        Vector3 FlyPoint(float t)
        {
            float g = flyGravity;
            t = Mathf.Clamp(t, 0f, flyTime);
            float k = flyTime > 0f ? t / flyTime : 1f;
            return new Vector3(Mathf.Lerp(flyFrom.x, flyTo.x, k), flyFrom.y + flyUp * t - 0.5f * g * t * t, Mathf.Lerp(flyFrom.z, flyTo.z, k));
        }

        /// <summary>The speed that puts the hips on the arc at flyT + dt (gravity's share of the step taken off).</summary>
        Vector3 FlyVelocity(float dt) => (FlyPoint(flyT + dt) - bodies[0].position) / Mathf.Max(1e-4f, dt) - Physics.gravity * dt;

        void Fly(float dt)
        {
            if (State == PawnState.Ragdoll) { EndFly(false); return; }
            flyT += dt;
            if (flyT >= flyTime)
            {
                EndFly(true);
                return;
            }
            if (flyHome != null && flyHome.State == PawnState.Active)
            {
                // Coming down on a head: the end of the arc follows the head if the piece moves (a little at a time).
                Vector3 head = flyHome.bodies[(int)BodyId.Head].position;
                flyTo = Vector3.MoveTowards(flyTo, new Vector3(head.x, flyTo.y, head.z), 3f * dt);
            }
            Launch(FlyVelocity(dt));
            facing = FlatDir(flyTo - flyFrom);
        }

        void EndFly(bool arrived)
        {
            if (!flying) return;
            flying = false;
            flyHome = null;
            if (flyPartner != null)
            {
                // Apart again a moment after landing (they cross in the air).
                apartFrom = flyPartner;
                partnerApart = 0.25f;
            }
            flyPartner = null;
            if (!arrived) return;
            Vector3 v = bodies[0].linearVelocity;
            // The castling sets both down gently on their new spots; the knight comes down with its weight.
            if (flySoft) Launch(new Vector3(0f, Mathf.Max(v.y, -1.5f), 0f));
        }

        void IgnorePartner(RagdollPawn other, bool ignore)
        {
            if (other == null || ownColliders == null || other.ownColliders == null) return;
            foreach (var a in ownColliders)
            {
                if (a == null) continue;
                foreach (var b in other.ownColliders)
                    if (b != null) Physics.IgnoreCollision(a, b, ignore);
            }
        }

        // ---------------------------------------------------------------- bishop: crossed air barrage (B)

        void UpdateBishopVolley(QueenHillSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (skillStage)
            {
                case SkillStage.None:
                    if (press && CanStartSkill())
                    {
                        SkillUses++;
                        skillStage = SkillStage.Windup;
                        stageTime = 0f;
                        aimLocked = false;
                        skillGrabLatch = skillGrabRaw;
                        hoverHipY = FeetPoint.y + s.bishopHover + standHeight;
                        hoverLeft = hoverTotal = s.bishopHoverTime;
                        hoverLinger = 0f;
                        hoverVel = Vector3.zero;
                        shellOut = shellLanded = false;
                        shotsLeft = Mathf.Max(1, s.bishopShots);
                        BishopAimQh(s);
                        SkillDetail = $"떠오름 (이동 키로 천천히 · 마우스로 조준 · 좌클릭 발사 {shotsLeft}발 · {s.bishopHoverTime:0.#}초 안에 안 쏘면 취소)";
                        QhFx(QueenHillFxKind.BishopRise, null, FeetPoint, Flat(facing), s.bishopHover, 0, Vector3.up * s.bishopHoverTime);
                        Log($"비숍: 떠오름 {s.bishopHover:0.0} m · {s.bishopHoverTime:0.#}초 (더 오를 수 없음)");
                    }
                    break;
                case SkillStage.Windup:
                    stageTime += dt;
                    Hover(s, dt);
                    if (!aimLocked)
                    {
                        BishopAimQh(s);
                        // R105: the hover's time only runs before the first shot; once one is out it waits for the next.
                        bool fired = shotsLeft < Mathf.Max(1, s.bishopShots);
                        if (!fired) hoverLeft -= dt;
                        if (cancel || press)
                        {
                            if (cancel) skillGrabLatch = true;
                            Log(fired ? "비숍: 남은 발 없이 내려옴" : "비숍: 쏘지 않고 내려옴");
                            DropHover();
                        }
                        else if (confirm && qhValid) FireShell(s);
                        else if (confirm) Log($"비숍: 거기는 쏠 수 없어요 ({qhWhy})");
                        else if (!fired && hoverLeft <= 0f)
                        {
                            // R105: a hover with no shot at all is called off (it used to throw where it aimed).
                            Log($"비숍: {s.bishopHoverTime:0.#}초 동안 안 쏴서 취소");
                            DropHover();
                        }
                        break;
                    }
                    if (!shellLanded)
                    {
                        shellT += dt;
                        if (shellT >= shellTime)
                        {
                            shellLanded = true;
                            shellOut = false;
                            BishopImpact(s);
                            hoverLinger = 0.3f;
                        }
                        break;
                    }
                    if (shotsLeft > 0)
                    {
                        // R95: another shot in this hover: it aims again; R105: for as long as it takes.
                        aimLocked = false;
                        shellLanded = false;
                        SkillDetail = $"다시 조준 (좌클릭 발사, {shotsLeft}발 남음 · 우클릭/F 내려옴)";
                        break;
                    }
                    hoverLinger -= dt;
                    if (hoverLinger <= 0f) DropHover();
                    break;
                case SkillStage.Recovery:
                    stageTime += dt;
                    if ((OnFloor && stageTime > 0.12f) || stageTime > 3f)
                    {
                        QhFx(QueenHillFxKind.BishopLand, null, FeetPoint, Flat(facing));
                        skillStage = SkillStage.None;
                        SkillDetail = "";
                        StartCooldown(s.bishopCooldown);
                    }
                    break;
            }
        }

        /// <summary>Held up at the hover height (no higher: it cannot keep climbing), bobbing a little. R105: the move
        /// keys slide it very slowly, like a magic carpet: it eases up to <c>bishopDrift</c> and eases to a stop.</summary>
        void Hover(QueenHillSkillParams s, float dt)
        {
            Vector3 v = bodies[0].linearVelocity;
            float y = bodies[0].position.y;
            float bob = stageTime > s.bishopRise ? Mathf.Sin((stageTime - s.bishopRise) * Mathf.PI * 3f) * 0.04f : 0f;
            float wantUp = Mathf.Clamp((hoverHipY + bob - y) * 7f, -2.5f, 4.5f);
            Vector3 drift = stageTime > s.bishopRise ? Vector3.ClampMagnitude(Flat(hoverMove), 1f) * s.bishopDrift : Vector3.zero;
            // The carpet's speed eases toward the drift (still air: it carries on a moment when let go); the body is
            // held to it every step (a nudge lost to the joints' damping and crept at a quarter of it). Rising: stopped.
            hoverVel = stageTime > s.bishopRise
                ? Vector3.Lerp(hoverVel, drift, Mathf.Clamp01(dt * 3f / Mathf.Max(0.05f, s.bishopDriftEase)))
                : Vector3.zero;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            Vector3 toward = (hoverVel - flat) * (stageTime > s.bishopRise ? 1f : Mathf.Clamp01(10f * dt));
            AddVelocity(new Vector3(toward.x, wantUp - v.y, toward.z) - Physics.gravity * dt);
            carryVel = Vector3.zero;
            // The standing anchor goes along with the hover (R105): left where it was, its spring pulled the
            // drifting body back and it barely crept.
            anchorVel = flat + new Vector3(toward.x, 0f, toward.z);
            Vector3 hips = bodies[0].position;
            anchorPos = new Vector3(hips.x, anchorPos.y, hips.z);
            freeFlight = Mathf.Max(freeFlight, 0.1f);
            airTimer = Mathf.Max(airTimer, 0.1f);
            Vector3 aim = Flat(qhPoint - bodies[0].position);
            if (aim.sqrMagnitude > 0.04f) facing = aim.normalized;
            else facing = AimFlat();
            anchor.MoveRotation(Quaternion.LookRotation(facing, Vector3.up));
        }

        void DropHover()
        {
            shellOut = false;
            skillStage = SkillStage.Recovery;
            stageTime = 0f;
            SkillDetail = "내려옴";
            QhFx(QueenHillFxKind.BishopDrop, null, FeetPoint, Flat(facing));
        }

        /// <summary>Where the camera's aim meets a floor within range; looking at a face, the top behind it; past the
        /// range, the floor under the range's end.</summary>
        void BishopAimQh(QueenHillSkillParams s) => AimFloor(s.bishopRange, 99f, out qhPoint, out qhValid, out qhWhy);

        void FireShell(QueenHillSkillParams s)
        {
            shotsLeft--;
            aimLocked = true;
            shellOut = true;
            shellLanded = false;
            shellT = 0f;
            shellTime = s.bishopFlight;
            Vector3 hand = bodies[(int)BodyId.HandR].position;
            shellFrom = hand + Vector3.up * 0.1f;
            shellTo = qhPoint;
            shotYaw = FlatDir(shellTo - bodies[0].position);
            SkillDetail = "견제탄 날아가는 중";
            QhFx(QueenHillFxKind.BishopThrow, null, shellFrom, shotYaw, shellTime, 0, shellTo);
        }

        /// <summary>The shot lands: an X of diagonals (±45° to the throw) and the spot itself. Enemies on it are pushed
        /// out from the middle and knocked down: off a wall or a ledge they fall (B).</summary>
        void BishopImpact(QueenHillSkillParams s)
        {
            Vector3 c = shellTo;
            Vector3 d1 = Quaternion.Euler(0f, 45f, 0f) * shotYaw, d2 = Quaternion.Euler(0f, -45f, 0f) * shotYaw;
            int down = 0;
            foreach (var other in All)
            {
                if (other == null || other == this || other.NetworkPuppet || !IsEnemyOf(other)) continue;
                Vector3 hip = other.bodies[0].position;
                Vector3 rel = Flat(hip - c);
                float dy = hip.y - other.standHeight - c.y;
                if (dy < -1.2f || dy > 2.2f) continue;
                Vector3 away = Vector3.zero;
                bool hit = rel.magnitude <= s.bishopCenter;
                if (hit) away = rel.sqrMagnitude > 0.01f ? rel.normalized : shotYaw;
                foreach (var d in new[] { d1, d2 })
                {
                    if (hit) break;
                    float along = Vector3.Dot(rel, d);
                    float across = (rel - d * along).magnitude;
                    if (Mathf.Abs(along) > s.bishopArm || across > s.bishopArmWidth * 0.5f + 0.2f) continue;
                    hit = true;
                    away = (d * Mathf.Sign(along) * 0.7f + (rel - d * along).normalized * 0.3f).normalized;
                }
                if (!hit) continue;
                bool climbing = other.Climbing;
                string result = QhHit(other, away * s.bishopPush + Vector3.up * s.bishopLift, true, "비숍 교차 포격",
                    QueenHillFxKind.BishopHit, other.bodies[(int)BodyId.Chest].position);
                if (other.State == PawnState.Ragdoll) down++;
                Log($"비숍 → {other.DisplayName}: {rel.magnitude:0.0} m" + (climbing ? " (벽에 매달림)" : "") + $" → {result}");
            }
            QhFx(QueenHillFxKind.BishopImpact, null, c, shotYaw, s.bishopArm, down);
            Log($"비숍: 교차 파동 — {down}명 넘어짐");
        }

        /// <summary>Where a floor aim's ray starts: 1.6 m over the head and 1.2 m back along the aim (about the
        /// third-person camera). The probe aims from the same spot.</summary>
        public Vector3 AimEye(Vector3 aim)
        {
            Vector3 flat = new Vector3(aim.x, 0f, aim.z);
            flat = flat.sqrMagnitude > 1e-4f ? flat.normalized : Flat(facing).normalized;
            return bodies[(int)BodyId.Head].position + Vector3.up * 1.6f - flat * 1.2f;
        }

        /// <summary>The floor the camera's aim points at within <paramref name="range"/> across the ground, at most
        /// <paramref name="maxRise"/> above this piece's floor. Looking at a wall's face, the top just behind it.</summary>
        void AimFloor(float range, float maxRise, out Vector3 point, out bool valid, out string why)
        {
            Vector3 head = bodies[(int)BodyId.Head].position;
            Vector3 aim = input.aim.sqrMagnitude > 1e-4f ? input.aim.normalized : facing;
            Vector3 flat = AimFlat();
            Vector3 me = FeetPoint;
            point = me + flat * Mathf.Min(3f, range);
            valid = false;
            why = "바닥이 없음";
            bool found = false;
            // From about where the third-person camera is (above and behind the head), so a ledge's top can be aimed at
            // from below it.
            if (SolidRay(AimEye(aim), aim, range + 10f, out var hit))
            {
                if (hit.normal.y >= 0.6f) { point = hit.point; found = true; }
                else if (SolidRay(hit.point + flat * 0.45f + Vector3.up * (maxRise < 50f ? maxRise + 1.6f : 4f), Vector3.down, 6f, out var top) && top.normal.y >= 0.6f)
                {
                    point = top.point;   // a ledge: the top just behind the face it looks at
                    found = true;
                }
            }
            if (found && Flat(point - me).magnitude > range)
            {
                // Past the range: the floor under the range's end.
                found = false;
            }
            if (!found)
            {
                Vector3 far = new Vector3(me.x, Mathf.Max(me.y, head.y), me.z) + flat * range;
                if (SolidRay(far + Vector3.up * (maxRise < 50f ? maxRise + 1.2f : 3f), Vector3.down, 12f, out var floor) && floor.normal.y >= 0.6f)
                {
                    point = floor.point;
                    found = true;
                }
            }
            if (!found) return;
            if (Flat(point - me).magnitude < 0.8f) { why = "너무 가까움"; return; }
            if (point.y - me.y > maxRise) { why = $"너무 높음 ({point.y - me.y:0.0} m, 한 층까지)"; return; }
            valid = true;
            why = "";
        }

        // ---------------------------------------------------------------- knight: press leap (B)

        void UpdateKnightPress(QueenHillSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (skillStage)
            {
                case SkillStage.None:
                    if (press && CanStartSkill())
                    {
                        skillStage = SkillStage.Windup;
                        stageTime = 0f;
                        aimLocked = false;
                        skillGrabLatch = skillGrabRaw;
                        KnightAimQh(s);
                        SkillDetail = "착지점 조준 (마우스 · 좌클릭 도약 · 우클릭/F 취소)";
                    }
                    break;
                case SkillStage.Windup:
                    stageTime += dt;
                    if (!aimLocked)
                    {
                        KnightAimQh(s);
                        if (Flat(skillMoveRaw).sqrMagnitude < 0.04f) facing = AimFlat();
                        if (cancel || press)
                        {
                            if (cancel) skillGrabLatch = true;
                            ClearSkills();
                            Log("나이트: 조준 취소");
                            break;
                        }
                        if (confirm)
                        {
                            if (!qhValid) { Log($"나이트: 거기로는 못 뛰어요 ({qhWhy})"); break; }
                            SkillUses++;
                            aimLocked = true;
                            stageTime = 0f;
                            leapSpot = qhPoint;
                            leapTarget = qhTarget;
                            facing = FlatDir(leapSpot - bodies[0].position);
                            SkillDetail = leapTarget != null ? $"도약 준비 ({leapTarget.DisplayName} 머리 위)" : "도약 준비";
                            QhFx(QueenHillFxKind.KnightLock, leapTarget, FeetPoint, facing, s.knightRadius, 0, leapSpot);
                        }
                        break;
                    }
                    if (stageTime >= s.knightWindup)
                    {
                        Vector3 from = FeetPoint;
                        if (leapTarget != null && leapTarget.State != PawnState.Active) leapTarget = null;
                        Vector3 toHips = leapSpot + Vector3.up * (standHeight + 0.02f);
                        if (leapTarget != null)
                        {
                            // Onto the head (R93): the feet come down on top of it, where it stands now.
                            Vector3 head = leapTarget.bodies[(int)BodyId.Head].position;
                            leapSpot = new Vector3(head.x, leapTarget.FeetPoint.y, head.z);
                            toHips = new Vector3(head.x, HeadTop(leapTarget) + standHeight, head.z);
                        }
                        // R93: a higher arc (knightArc 1.6), and always well over a head it comes down on.
                        float top = Mathf.Max(Mathf.Max(from.y, leapSpot.y) + s.knightArc + standHeight, toHips.y + 0.5f);
                        skillHitSet.Clear();
                        knightLanded = false;
                        BeginFly(toHips, top, null, false, s.knightGravity);
                        flyHome = leapTarget;
                        skillStage = SkillStage.Active;
                        stageTime = 0f;
                        SkillDetail = leapTarget != null ? "도약 (머리 밟기)" : "도약";
                        QhFx(QueenHillFxKind.KnightLeap, leapTarget, from, FlatDir(leapSpot - from), flyTime, 0, leapSpot);
                        Log($"나이트: 도약 {Flat(leapSpot - from).magnitude:0.0} m, 높이 차 {leapSpot.y - from.y:+0.0;-0.0} m"
                            + (leapTarget != null ? $", {leapTarget.DisplayName} 머리 위로" : ""));
                    }
                    break;
                case SkillStage.Active:
                    stageTime += dt;
                    // The landing is judged the moment the arc ends: on the floor, or on the head it came down on.
                    if (!flying && !knightLanded)
                    {
                        knightLanded = true;
                        KnightLandQh(s);
                    }
                    if (knightLanded && (OnFloor || stageTime > flyTime + 0.6f))
                    {
                        skillStage = SkillStage.Recovery;
                        stageTime = 0f;
                        SkillDetail = "착지 후딜";
                    }
                    break;
                case SkillStage.Recovery:
                    stageTime += dt;
                    if (stageTime >= s.knightRecovery)
                    {
                        skillStage = SkillStage.None;
                        SkillDetail = "";
                        StartCooldown(s.knightCooldown);
                    }
                    break;
            }
        }

        void KnightAimQh(QueenHillSkillParams s)
        {
            AimFloor(s.knightRange, s.knightMaxRise, out qhPoint, out qhValid, out qhWhy);
            if (qhValid && !RoomToStand(qhPoint, null)) { qhValid = false; qhWhy = "설 자리가 없음"; }
            // R93 (승규 님): an enemy close to where the circle is gets caught by it, and the leap goes onto its head.
            qhTarget = KnightHeadPick(s);
            if (qhTarget == null) return;
            qhPoint = qhTarget.FeetPoint;
            qhValid = true;
            qhWhy = "";
        }

        /// <summary>The enemy whose head the knight's aim catches: standing within knightSnap of the aimed spot, in
        /// reach and at most one tier up (as for a floor); the closest to the spot.</summary>
        RagdollPawn KnightHeadPick(QueenHillSkillParams s)
        {
            Vector3 me = FeetPoint;
            RagdollPawn best = null;
            float bestD = s.knightSnap;
            foreach (var other in All)
            {
                if (other == null || other == this || other.NetworkPuppet || !IsEnemyOf(other) || other.State != PawnState.Active || other.flying) continue;
                Vector3 feet = other.FeetPoint;
                if (feet.y - me.y > s.knightMaxRise) continue;
                float reach = Flat(feet - me).magnitude;
                if (reach > s.knightRange + 0.5f || reach < 0.8f) continue;
                float d = Flat(feet - qhPoint).magnitude;
                if (d > bestD) continue;
                best = other;
                bestD = d;
            }
            return best;
        }

        /// <summary>The first way <paramref name="p"/> can slide a metre along the floor without hitting a wall: the given
        /// way, then 45° either side of it, and so on round (other pieces do not count).</summary>
        Vector3 OpenWay(RagdollPawn p, Vector3 first)
        {
            if (first.sqrMagnitude < 1e-4f) first = Vector3.forward;
            Vector3 from = p.bodies[0].position;
            foreach (float a in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f })
            {
                Vector3 d = Quaternion.Euler(0f, a, 0f) * first;
                if (!BlockedBetween(from, d * 1.1f, p)) return d;
            }
            return first;
        }

        /// <summary>The top of a piece's head (its colliders' highest point; a little over the head body otherwise).</summary>
        static float HeadTop(RagdollPawn p)
        {
            var head = p.bodies[(int)BodyId.Head];
            float top = head.position.y + 0.12f;
            foreach (var c in head.GetComponentsInChildren<Collider>())
                if (!c.isTrigger) top = Mathf.Max(top, c.bounds.max.y);
            return top;
        }

        void KnightLandQh(QueenHillSkillParams s)
        {
            Vector3 feet = FeetPoint;
            int n = 0;
            foreach (var other in All)
            {
                if (other == null || other == this || other.NetworkPuppet || !IsEnemyOf(other) || other.State != PawnState.Active) continue;
                Vector3 d = other.bodies[0].position - bodies[0].position;
                // The piece it came down on is under its feet: caught whatever the circle says.
                bool stomped = other == leapTarget && Flat(other.bodies[(int)BodyId.Head].position - bodies[0].position).magnitude < 0.8f;
                if (!stomped && (Flat(d).magnitude > s.knightRadius || Mathf.Abs(other.FeetPoint.y - feet.y) > 1f)) continue;
                Vector3 out_ = Flat(d).sqrMagnitude > 0.01f ? Flat(d).normalized : Flat(facing).normalized;
                if (other.wardLeft > 0f)
                {
                    QhHit(other, out_ * s.knightSlide, false, "나이트 압착", QueenHillFxKind.KnightFlatten, other.bodies[(int)BodyId.Head].position);
                    continue;
                }
                if (!other.Squash(s.knightFlatten, 0.5f))
                {
                    other.Stagger(0.3f);
                    continue;
                }
                float slide = s.knightSlide;
                if (stomped)
                {
                    // Squashed under its feet: it squirts out the way the knight was going if that is open (else the
                    // nearest open way), a little faster, and the knight goes down through it to the floor; the two
                    // collide again once they are apart.
                    out_ = OpenWay(other, Flat(facing).normalized);
                    slide *= 1.25f;
                    IgnorePartner(other, true);
                    stompedOne = other;
                    stompedLeft = 2.5f;
                }
                n++;
                // Slid out of the circle while flat: on a ledge's edge that is over it (B: flattened up high, it falls).
                other.flattenSlide = out_ * slide;
                other.flattenSlideLeft = s.knightSlideTime;
                other.flattenWatch = s.knightFlatten + 0.4f;
                other.flattenFloor = other.FeetPoint.y;
                other.flattenBy = this;
                other.LastSkillHit = stomped ? "나이트 압착: 머리 밟혀 납작" : "나이트 압착: 납작";
                QhFx(QueenHillFxKind.KnightFlatten, other, other.bodies[(int)BodyId.Head].position, out_, s.knightFlatten, stomped ? 1 : 0);
                Log($"나이트 → {other.DisplayName}: {(stomped ? "머리 밟기 → " : "")}납작 {s.knightFlatten:0.0#}초" + (other.flattenFloor > 0.3f ? $" (높이 {other.flattenFloor:0.0} m)" : ""));
            }
            QhFx(QueenHillFxKind.KnightLand, null, feet, Flat(facing), s.knightRadius, n);
        }

        /// <summary>Flattened and pushed out: if that takes it off a ledge it does not land on its feet (B).</summary>
        void UpdateFlattenFall(float dt)
        {
            if (flattenWatch <= 0f) return;
            flattenWatch -= dt;
            if (State == PawnState.Ragdoll) { flattenWatch = flattenSlideLeft = 0f; return; }
            if (flattenSlideLeft > 0f)
            {
                // Flat on the floor it cannot brace: it skids out at the slide's speed for a moment (a plain push was
                // braked by the standing spring within a few centimetres).
                flattenSlideLeft -= dt;
                Vector3 own = Flat(bodies[0].linearVelocity) - carryVel;
                AddVelocity(flattenSlide - own);
                anchorVel = flattenSlide + carryVel;
                Vector3 hp = bodies[0].position;
                anchorPos = new Vector3(hp.x + anchorVel.x * dt, anchorPos.y, hp.z + anchorVel.z * dt);
                anchor.MovePosition(anchorPos);
            }
            bool off = !Grounded && coyote <= 0f && bodies[0].linearVelocity.y < -1f
                       && bodies[0].position.y < flattenFloor + standHeight - 0.2f;
            if (!off) return;
            flattenWatch = 0f;
            squashTimer = 0f;
            Knockdown("나이트 압착: 납작해져 떨어짐", QueenHillSkills != null ? QueenHillSkills.downHold : 0f);
            var by = flattenBy != null ? flattenBy : this;
            by.QhFx(QueenHillFxKind.KnightFall, this, bodies[0].position, Flat(bodies[0].linearVelocity).normalized);
            by.QhFx(QueenHillFxKind.Down, this, FeetPoint, Flat(bodies[0].linearVelocity).normalized);
            Log($"{DisplayName}: 납작해진 채 가장자리에서 떨어짐");
        }

        // ---------------------------------------------------------------- pawn: squeeze through (B)

        void UpdatePawnSqueeze(QueenHillSkillParams s, bool press, float dt)
        {
            switch (skillStage)
            {
                case SkillStage.None:
                    if (press && CanStartSkill())
                    {
                        SkillUses++;
                        skillDir = MoveOrAim();
                        facing = skillDir;
                        skillStage = SkillStage.Windup;
                        stageTime = 0f;
                        SkillDetail = "몸 낮춤";
                        QhFx(QueenHillFxKind.PawnCrouch, null, FeetPoint, skillDir, s.pawnDistance, 0, Vector3.up * s.pawnCrouch);
                    }
                    break;
                case SkillStage.Windup:
                    stageTime += dt;
                    facing = skillDir;
                    if (stageTime >= s.pawnCrouch)
                    {
                        skillHitSet.Clear();
                        StartDash(s.pawnSpeed, s.pawnDistance / Mathf.Max(0.5f, s.pawnSpeed));
                        skillStage = SkillStage.Active;
                        stageTime = 0f;
                        SkillDetail = "비집고 돌파";
                        QhFx(QueenHillFxKind.PawnDash, null, FeetPoint, skillDir, s.pawnDistance);
                    }
                    break;
                case SkillStage.Active:
                    stageTime += dt;
                    if (WallAhead(dashBase * dt + 0.22f, out var wall))
                    {
                        // A wall ends the dash where it stands: no passing through, no stagger.
                        if (dashLeft > 0f) QhFx(QueenHillFxKind.PawnStop, null, wall.point, skillDir);
                        dashLeft = 0f;
                        HaltDash();
                    }
                    else
                    {
                        DriveDash(dt);
                        PawnBumps(s);
                        dashLeft -= dt;
                    }
                    if (dashLeft <= 0f)
                    {
                        EndDash();
                        skillStage = SkillStage.Recovery;
                        stageTime = 0f;
                        SkillDetail = "일어남";
                    }
                    break;
                case SkillStage.Recovery:
                    stageTime += dt;
                    if (stageTime >= s.pawnRise)
                    {
                        skillStage = SkillStage.None;
                        SkillDetail = "";
                        StartCooldown(s.pawnCooldown);
                    }
                    break;
            }
        }

        /// <summary>Pieces it squeezes past are nudged aside (no knockdown); the pawn itself is not protected: the bodies
        /// still meet, a skill still hits it.</summary>
        void PawnBumps(QueenHillSkillParams s)
        {
            Vector3 at = bodies[(int)BodyId.Chest].position + skillDir * 0.3f;
            Vector3 right = Vector3.Cross(Vector3.up, skillDir);
            int n = Physics.OverlapSphereNonAlloc(at, 0.42f, skillOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!ColliderOwner.TryGetValue(skillOverlap[i], out var other) || other == null || other == this || skillHitSet.Contains(other)) continue;
                if (other.State == PawnState.Ragdoll) continue;
                skillHitSet.Add(other);
                float side = Vector3.Dot(Flat(other.bodies[0].position - bodies[0].position), right);
                Vector3 push = right * (Mathf.Abs(side) < 0.02f ? 1f : Mathf.Sign(side)) * s.pawnNudge;
                other.TakeHit(push, 0f, 0f, false);
                QhFx(QueenHillFxKind.PawnBump, other, SkillContact(other), push.normalized);
                Log($"폰 → {other.DisplayName}: 비집고 지나감 (옆으로 비킴)");
            }
        }

        void UpdateCrouch(QueenHillSkillParams s)
        {
            if (legReach > 0.02f)
                crouchAngle = Mathf.Min(48f, Mathf.Acos(Mathf.Clamp(1f - s.pawnDrop / legReach, -1f, 1f)) * Mathf.Rad2Deg);
            float want = 0f;
            if (piece == PieceKind.Pawn && State == PawnState.Active)
            {
                if (skillStage == SkillStage.Windup) want = Mathf.Clamp01(stageTime / Mathf.Max(0.01f, s.pawnCrouch));
                else if (skillStage == SkillStage.Active) want = 1f;
                else if (skillStage == SkillStage.Recovery) want = 1f - Mathf.Clamp01(stageTime / Mathf.Max(0.01f, s.pawnRise));
            }
            qhCrouch = want;
        }

        // ---------------------------------------------------------------- poses

        /// <summary>The Queen of the Hill skills' part of the body pose (from SkillPose). The pawn's crouch: legs split
        /// front and back (the hips come down by what that costs, SkillCrouchDrop), chest pitched low, head up, arms swept
        /// back. The king raises his sceptre and brings it down; the queen takes her sword down behind on the right and
        /// brings it up across her front (QueenPose, R93); the bishop hovers with its arms out and its feet together; the
        /// knight crouches before it leaps.</summary>
        void QueenHillPose(ref Quaternion armL, ref Quaternion armR, ref Quaternion chest, ref Quaternion head,
                           ref Quaternion thighL, ref Quaternion thighR, ref Quaternion footL, ref Quaternion footR)
        {
            if (State != PawnState.Active) return;
            if (qhCrouch > 0.001f)
            {
                float c = qhCrouch, a = crouchAngle * c;
                thighL = Quaternion.Euler(-a, 0f, -6f * c);
                thighR = Quaternion.Euler(a, 0f, 6f * c);
                footL = Quaternion.Euler(a * 0.9f, 0f, 0f);
                footR = Quaternion.Euler(-a * 0.9f, 0f, 0f);
                chest = Quaternion.Slerp(chest, Quaternion.Euler(44f, 0f, 0f), c);
                head = Quaternion.Slerp(head, Quaternion.Euler(-36f, 0f, 0f), c);
                armL = Quaternion.Slerp(armL, Quaternion.Euler(0f, -70f, 40f), c);
                armR = Quaternion.Slerp(armR, Quaternion.Euler(0f, 70f, -40f), c);
                return;
            }
            switch (piece)
            {
                case PieceKind.King when skillStage == SkillStage.Windup:
                {
                    float k = Mathf.Clamp01(stageTime / 0.12f);
                    armR = Quaternion.Slerp(armR, Quaternion.Euler(0f, -20f, 78f), k);
                    armL = Quaternion.Slerp(armL, Quaternion.Euler(0f, 0f, -30f), k);
                    chest = Quaternion.Slerp(chest, Quaternion.Euler(-10f, 0f, 0f), k);
                    head = Quaternion.Slerp(head, Quaternion.Euler(-14f, 0f, 0f), k);
                    break;
                }
                case PieceKind.King when skillStage == SkillStage.Recovery:
                    armR = Quaternion.Euler(0f, -80f, -10f);
                    armL = Quaternion.Euler(0f, 0f, 30f);
                    chest = Quaternion.Euler(16f, 0f, 0f);
                    break;
                case PieceKind.Queen when skillStage != SkillStage.None:
                    QueenPose(ref armL, ref armR, ref chest, ref head);
                    break;
                case PieceKind.Rook when skillStage == SkillStage.Windup && aimLocked:
                    // Pointing at the ally it asked.
                    armR = Quaternion.Euler(0f, -85f, -5f);
                    break;
                case PieceKind.Bishop when skillStage == SkillStage.Windup:
                {
                    // Hovering: arms out and a little up, legs together and pointed down; the throw swings the right arm.
                    armL = Quaternion.Euler(0f, 10f, -28f);
                    armR = shellOut || shellLanded ? Quaternion.Euler(0f, -95f, 10f) : Quaternion.Euler(0f, -10f, 28f);
                    thighL = Quaternion.Euler(4f, 0f, 3f);
                    thighR = Quaternion.Euler(4f, 0f, -3f);
                    footL = footR = Quaternion.Euler(28f, 0f, 0f);
                    chest = Quaternion.Euler(shellOut ? 12f : -4f, 0f, 0f);
                    break;
                }
                case PieceKind.Knight when skillStage == SkillStage.Windup && aimLocked:
                    thighL = Quaternion.Euler(-24f, 0f, -6f);
                    thighR = Quaternion.Euler(-24f, 0f, 6f);
                    footL = footR = Quaternion.Euler(24f, 0f, 0f);
                    chest = Quaternion.Euler(28f, 0f, 0f);
                    armL = Quaternion.Euler(0f, -50f, 30f);
                    armR = Quaternion.Euler(0f, 50f, -30f);
                    break;
            }
        }
    }
}
