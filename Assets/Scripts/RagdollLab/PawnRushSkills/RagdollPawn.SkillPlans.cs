using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>Where a new (plan) skill is: aiming (its key pressed, before the left click), its warning, the move itself,
    /// stuck for a moment after it.</summary>
    public enum PlanPhase : byte { None = 0, Aim = 1, Windup = 2, Active = 3, Recovery = 4 }

    /// <summary>
    /// Pawn Rush 2.0 skill prototypes and the slots they run from (JY-skill-lab, 2026-11-19;
    /// Docs/Skills/PAWN_RUSH_2_SKILL_PLANS.md). The user: a skill is "각 기물마다, 체스의 컨셉을 살린, 게임플레이에 변수를
    /// 창출해내는 재미있는 무언가", the plans are to be made as prototypes and judged by playing them, and (the skill window)
    /// "드래그 앤 드롭으로 ... 자유롭게 넣었다 뺐다 ... 막 바꿔보면서 테스트", any skill on any piece: "어차피 이건 개발자 테스트니까".
    ///
    /// The slots. PawnRushSkillParams.loadout (PawnRushLoadout) says which skill sits in which of a piece's slots: one slot
    /// (the skill key), or three with the several-skills system (the skill key and the two ability keys, E and Q). One
    /// skill runs at a time; every slot has its own cooldown. A skill is a number (PawnRushSkillIds): 1..5 the skills as
    /// they are, whose code runs as it always did but for <see cref="skillPiece"/>, the piece that code believes it is
    /// (so the rook's chain works on a pawn; the effects and sounds made for those skills still look at the pawn's real
    /// piece and may not show on another); 11..15 the new ones below; 44..49 Queen of the Hill's six
    /// (RagdollPawn.HillSkills.cs); 61..65 the skill idea book's bundle A (RagdollPawn.BookSkills.cs); 43, 71 and 72 the
    /// kits' basic row (RagdollPawn.KitSkills.cs: the knight's bent leap, the bishop's fianchetto, the king's step). The
    /// king has a row of his own since then; while it is empty he plays his call as he always did.
    ///
    ///   en passant  an enemy that has just brushed past (the closest point of their passing, the enemy the faster) can
    ///               be tripped for a moment: a lunge at its ankles, it goes down sliding on at the speed it ran.
    ///   fork        a short leap, and a kick out of both hind legs on landing at two spots ahead left and right (shown
    ///               from the start): enemies on both spots at once go down and fly apart; on one only, a shove.
    ///   pin         a straight line through the first enemy on it; another enemy or a wall or pillar close behind that
    ///               one on the same line, and the two are tied by a short rope for a while (the X rope's elastic pull,
    ///               RagdollPawn.BishopLasso.cs). Nothing behind: a short stagger.
    ///   castling    it aims at a teammate; the teammate answers with its own skill key (as a king's call is answered)
    ///               and the two fly to each other's places. A variant to try (castleEnemies): an enemy too, without
    ///               asking, after a warning.
    ///   check       it names an enemy: the square it stands on turns red, and unless it is off that square in time it is
    ///               knocked flying. As in chess it can step away, a piece of its side standing between takes the blow
    ///               instead, or the checker is knocked over first.
    ///
    /// Prototypes: the rules and the physics only. The new skills keep their own phase (<see cref="PlanPhase"/>) and leave
    /// the old skills' stage (SkillStage) at None, so the effects and sounds made for the old skills stay quiet; lines and
    /// rings are drawn by PawnRushPlanMarks from what is public here, the same on every PC (the host sends the skill, its
    /// phase, time, point and target in the piece's skill state, RagdollPawn.SkillNet.cs). A few moments reuse an old
    /// skill's sound and hit effect. Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        // ---------------------------------------------------------------- the slots

        // Each slot's cooldown. The old skills' code reads and writes the one in cdSlot, as skillCooldownLeft and
        // cooldownTotal: the slot of the skill being run.
        readonly float[] slotCd = new float[PawnRushLoadout.Slots], slotCdTotal = new float[PawnRushLoadout.Slots];
        readonly bool[] slotPress = new bool[PawnRushLoadout.Slots];
        int cdSlot;
        // The slot and the skill under way, and the two ability keys taken this step for the second and third slots.
        int activeSlot = -1, activeSkill;
        bool slotKey1, slotKey2;
        // The piece whose old skill is being run or was last looked at (-1: this pawn's own piece).
        int oldHome = -1;

        float skillCooldownLeft { get => slotCd[cdSlot]; set => slotCd[cdSlot] = value; }
        float cooldownTotal { get => slotCdTotal[cdSlot]; set => slotCdTotal[cdSlot] = value; }

        /// <summary>The piece the old skills' code takes this pawn for: its own, unless the loadout put another piece's
        /// skill in the slot at work.</summary>
        PieceKind skillPiece => oldHome >= 0 ? (PieceKind)oldHome : piece;
        public PieceKind SkillPiece => skillPiece;

        PawnRushLoadout Loadout => PawnRushSkills != null ? PawnRushSkills.loadout : null;

        /// <summary>How many slots this piece has now, and the skill in one (0: empty).</summary>
        public int SlotCount => Loadout != null && PawnRushLoadout.Row(piece) >= 0 && (piece != PieceKind.King || Loadout.KingSlots)
            ? Loadout.SlotCount : 0;
        public int SlotSkill(int slot) => Loadout != null ? Loadout.Skill(piece, slot) : 0;
        public bool HasSkill(int id) => Loadout != null && Loadout.SlotOf(piece, id) >= 0;
        public float SlotCooldown(int slot) => slot >= 0 && slot < slotCd.Length ? Mathf.Max(0f, slotCd[slot]) : 0f;
        public float SlotCooldownTotal(int slot) => slot >= 0 && slot < slotCdTotal.Length ? slotCdTotal[slot] : 0f;

        bool OldRunning => skillStage != SkillStage.None || chainPhase != ChainPhase.None || lassoPhase != LassoPhase.None;
        /// <summary>A skill is under way, old or new; the slot it runs from (-1) and its number (0).</summary>
        public bool SkillRunning => planPhase != PlanPhase.None || OldRunning;
        public int ActiveSlot => SkillRunning ? activeSlot : -1;
        public int ActiveSkill => SkillRunning ? activeSkill : 0;
        /// <summary>The new skill under way (0: none, or an old one).</summary>
        public int PlanSkill => planPhase != PlanPhase.None ? activeSkill : 0;

        /// <summary>The growing system's stage of this piece's skill (1..3; 1 under the other systems).</summary>
        public int SkillStageLevel => Loadout != null ? Loadout.Stage(piece) : 1;

        float StageFactor(float[] table)
        {
            if (table == null || table.Length == 0) return 1f;
            return table[Mathf.Clamp(SkillStageLevel - 1, 0, table.Length - 1)];
        }

        float StageCooldown => PawnRushSkills != null ? StageFactor(PawnRushSkills.stageCooldown) : 1f;
        float StagePower => PawnRushSkills != null ? StageFactor(PawnRushSkills.stagePower) : 1f;

        static int OldSkillOf(PieceKind kind) => kind switch
        {
            PieceKind.Knight => PawnRushSkillIds.TurnLeap,
            PieceKind.Bishop => PawnRushSkillIds.CrossRope,
            PieceKind.Rook => PawnRushSkillIds.ChainPull,
            PieceKind.Queen => PawnRushSkillIds.RingShove,
            _ => PawnRushSkillIds.TwoSteps,
        };

        /// <summary>The skills of this piece's slots this step (the host's): the one under way gets its key and the clicks
        /// and the others wait; with none under way each sees its own key and the first to start takes the step.</summary>
        void RunSlots(PawnRushSkillParams s, bool press, bool key1, bool key2, bool confirm, bool cancel, float dt)
        {
            if (piece == PieceKind.King && (s.loadout == null || !s.loadout.KingSlots))
            {
                // The king's row is empty (he was given one on 2026-11-20: a team has one of each piece): his call, as it
                // was. With a skill in his row he plays his slots like any piece.
                oldHome = -1;
                cdSlot = 0;
                kingPool = false;
                UpdateKingCall(s, press, cancel, dt);
                return;
            }
            var lo = s.loadout;
            slotPress[0] = press;
            slotPress[1] = key1;
            slotPress[2] = key2;
            FeelSendKept(s, lo, dt);   // the hand feel: a key pressed a moment early goes now (RagdollPawn.HandFeel.cs)

            if (planPhase != PlanPhase.None && (State == PawnState.Ragdoll || Floating))
            {
                // A new skill floored (or in the water) mid-way: it ends here; one already spent runs its cooldown.
                Log($"{DisplayName}: {PawnRushSkillBook.Name(activeSkill)} 끊김 ({(Floating ? "물" : "넘어짐")})");
                // (The air barrage is spent from the moment it rises: its aim is the hover.)
                bool spent = planPhase != PlanPhase.Aim || activeSkill == PawnRushSkillIds.AirVolley;
                float cooldown = NewSkillCooldown(activeSkill, s);
                EndPlanSkill();
                if (spent) StartCooldown(cooldown);
            }

            if (SkillRunning)
            {
                int k = Mathf.Clamp(activeSlot, 0, slotCd.Length - 1);
                cdSlot = k;
                if (planPhase != PlanPhase.None) RunNewSkill(activeSkill, s, slotPress[k], confirm, cancel, dt);
                else RunOldSkill(activeSkill, s, slotPress[k], confirm, cancel, dt);
                return;
            }

            activeSlot = -1;
            activeSkill = 0;
            bool anyOld = false;
            for (int k = 0; k < lo.SlotCount; k++)
            {
                int id = lo.Skill(piece, k);
                if (id == 0) continue;
                cdSlot = k;
                float cdBefore = slotCd[k];
                if (PawnRushSkillIds.Old(id))
                {
                    anyOld = true;
                    RunOldSkill(id, s, slotPress[k], confirm, cancel, dt);
                }
                else RunNewSkill(id, s, slotPress[k], false, false, dt);
                // The key started a use (under way, or its cooldown begun at once): the hand feel's press.
                if (slotPress[k] && (SkillRunning || (cdBefore <= 0f && slotCd[k] > 0f))) FeelUsed(k);
                if (!SkillRunning) continue;
                activeSlot = k;
                activeSkill = id;
                return;
            }
            FeelKeepPresses(s, lo);   // a key that could not go yet is kept a moment (RagdollPawn.HandFeel.cs)
            if (!anyOld) oldHome = -1;
            cdSlot = 0;
        }

        /// <summary>One of the skills as they are, by its own code, as the piece it was made for.</summary>
        void RunOldSkill(int id, PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            // (Under way with no number: a loadout changed mid-skill. It runs out as the piece it believes it is.)
            if (!PawnRushSkillIds.Old(id)) id = OldSkillOf(skillPiece);
            oldHome = (int)PawnRushSkillIds.OldPiece(id);
            switch (id)
            {
                case PawnRushSkillIds.TwoSteps: pawnSteer = false; UpdatePawnSkill(s, press, dt); break;
                case PawnRushSkillIds.SteeredSteps: pawnSteer = true; UpdatePawnSkill(s, press, dt); break;   // the pool's P-기1
                case PawnRushSkillIds.TurnLeap: UpdateKnightSkill(s, press, dt); break;
                case PawnRushSkillIds.CrossRope: UpdateBishopSkill(s, press, confirm, cancel, dt); break;
                case PawnRushSkillIds.ChainPull: chainPool = false; UpdateRookChain(s, press, confirm, cancel, dt); break;
                // The skill pool's R-중2 and K-중1 on the chain's and the call's code (RagdollPawn.PoolSkills.cs).
                case PawnRushSkillIds.ChainBehind: chainPool = true; UpdateRookChain(s, press, confirm, cancel, dt); break;
                case PawnRushSkillIds.KingSummon:
                    kingPool = true;
                    if (SummonPushKey(s, press)) press = false;   // the moment after a teammate set down: push on together
                    UpdateKingCall(s, press, cancel, dt);
                    break;
                case PawnRushSkillIds.RingShove: UpdateQueenSkill(s, press, dt); break;
            }
        }

        void RunNewSkill(int id, PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (id)
            {
                case PawnRushSkillIds.EnPassant: UpdatePawnPass(s, press, dt); break;
                case PawnRushSkillIds.Fork: UpdateKnightFork(s, press, dt); break;
                case PawnRushSkillIds.Pin: UpdateBishopPin(s, press, confirm, cancel, dt); break;
                case PawnRushSkillIds.Castling: UpdateRookCastle(s, press, confirm, cancel, dt, id); break;
                case PawnRushSkillIds.Check: UpdateQueenCheck(s, press, confirm, cancel, dt); break;
                // Queen of the Hill's six (RagdollPawn.HillSkills.cs).
                case PawnRushSkillIds.Ward: UpdateWard(s, press, dt); break;
                case PawnRushSkillIds.Slash: UpdateSlash(s, press, confirm, cancel, dt); break;
                case PawnRushSkillIds.CastleSwap: UpdateRookCastle(s, press, confirm, cancel, dt, id); break;
                case PawnRushSkillIds.AirVolley: UpdateVolley(s, press, confirm, cancel, dt); break;
                case PawnRushSkillIds.PressLeap: UpdatePressLeap(s, press, confirm, cancel, dt); break;
                case PawnRushSkillIds.Squeeze: UpdateSqueeze(s, press, dt); break;
                // The skill idea book's bundle A (RagdollPawn.BookSkills.cs).
                case PawnRushSkillIds.TouchMove: UpdateTouchMove(s, press, dt); break;
                case PawnRushSkillIds.BackKick: UpdateBackKick(s, press, dt); break;
                case PawnRushSkillIds.Ghost: UpdateGhost(s, press, dt); break;
                case PawnRushSkillIds.BackPush: UpdateBackPush(s, press, dt); break;
                case PawnRushSkillIds.TwoHares: UpdateTwoHares(s, press, dt); break;
                // The kits' basic row (RagdollPawn.KitSkills.cs).
                case PawnRushSkillIds.BentLeap: UpdateBentLeap(s, press, dt); break;
                case PawnRushSkillIds.Fianchetto: UpdateFianchetto(s, press, confirm, cancel, dt); break;
                case PawnRushSkillIds.KingStep: UpdateKingStep(s, press, dt); break;
                // The skill pool's plan 1 (RagdollPawn.PoolSkills.cs).
                case PawnRushSkillIds.LongSnipe: UpdateLongSnipe(s, press, confirm, cancel, dt); break;
            }
        }

        static float NewSkillCooldown(int id, PawnRushSkillParams s)
        {
            switch (id)
            {
                case PawnRushSkillIds.EnPassant: return s.passCooldown;
                case PawnRushSkillIds.Fork: return s.forkCooldown;
                case PawnRushSkillIds.Pin: return s.pinCooldown;
                case PawnRushSkillIds.Castling: return s.castleCooldown;
                case PawnRushSkillIds.Check: return s.checkCooldown;
                case PawnRushSkillIds.Ward: return s.wardCooldown;
                case PawnRushSkillIds.Slash: return s.slashCooldown;
                case PawnRushSkillIds.CastleSwap: return s.swapCooldown;
                case PawnRushSkillIds.AirVolley: return s.volleyCooldown;
                case PawnRushSkillIds.PressLeap: return s.pressCooldown;
                case PawnRushSkillIds.Squeeze: return s.squeezeCooldown;
                case PawnRushSkillIds.TouchMove: return s.touchCooldown;
                case PawnRushSkillIds.BackKick: return s.kickCooldown;
                case PawnRushSkillIds.Ghost: return s.ghostCooldown;
                case PawnRushSkillIds.BackPush: return s.pushCooldown;
                case PawnRushSkillIds.TwoHares: return s.hareCooldown;
                case PawnRushSkillIds.BentLeap: return s.leapCooldown;
                case PawnRushSkillIds.Fianchetto: return s.fianCooldown;
                case PawnRushSkillIds.KingStep: return s.stepCooldown;
                case PawnRushSkillIds.LongSnipe: return s.snipeCooldown;
                default: return 0f;
            }
        }

        /// <summary>Before the run: the second and third slots' keys are the two ability keys (E, Q), theirs when a skill
        /// sits there (the pawn's hook and the like do not get them then); a piece in a castling's flight has no keys.</summary>
        void PlanKeys()
        {
            var lo = Loadout;
            if (lo != null && lo.SlotCount > 1 && (piece != PieceKind.King || lo.KingSlots))
            {
                if (input.ability && lo.Skill(piece, 1) != 0)
                {
                    slotKey1 = true;
                    input.ability = false;
                }
                if (input.ability2 && lo.Skill(piece, 2) != 0)
                {
                    slotKey2 = true;
                    input.ability2 = false;
                }
            }
            HillKeys();   // Queen of the Hill's six: what each lets the keys do in its phase
            BookKeys();   // the idea book's bundle A: a touched piece's feet, numb hands, and the same for its five
            KitKeys();    // the kits' basic row: heavy feet, a seated bishop, a stepping king
            PoolKeys();   // the skill pool's plan 1: the kneeling, drawing bishop (RagdollPawn.PoolSkills.cs)
            if (!castleCarry) return;
            input.move = Vector3.zero;
            input.jump = false;
            input.sprint = false;
            input.shove = false;
            input.shoveHeld = false;
            input.grab = false;
        }

        // ---------------------------------------------------------------- the new skills: what they share

        PlanPhase planPhase;
        float planTime;
        RagdollPawn planTarget;
        Vector3 planPoint, planDir = Vector3.forward;

        public PlanPhase PlanPhaseNow => planPhase;
        public float PlanTime => planTime;
        public bool PlanBusy => planPhase != PlanPhase.None;
        /// <summary>The piece the skill is about: the one checked, swapped with, lunged at, the pin's line met first (aiming:
        /// the one it would).</summary>
        public RagdollPawn PlanTarget => planTarget;
        /// <summary>The checked square's middle, the fork's landing, the end of the pin's line.</summary>
        public Vector3 PlanPoint => planPoint;
        public Vector3 PlanDir => planDir;

        void EndPlanSkill()
        {
            var mate = planTarget;
            if (mate != null && mate.castleOfferBy == this) mate.castleOfferBy = null;
            planPhase = PlanPhase.None;
            planTime = 0f;
            planTarget = null;
            if (dashing) EndDash();
            SkillDetail = "";
            ClearHillSkills();
            ClearBookSkills();
            ClearKitSkills();
        }

        /// <summary>Everything a new skill leaves running when the skills are cleared (a knockdown, a piece switch, being
        /// carried off). A pin stays: it is a rope between two pieces, not this piece's skill.</summary>
        void ClearPlanSkills()
        {
            EndPlanSkill();
            passTarget = null;
            passLeft = 0f;
            castleOfferBy = null;
            castleAccepted = false;
            if (castleCarry) EndCastleCarry(false);
        }

        // ---------------------------------------------------------------- en passant

        struct PassTrack
        {
            public Vector3 pos;
            public float closing;
        }

        readonly Dictionary<RagdollPawn, PassTrack> passSeen = new Dictionary<RagdollPawn, PassTrack>();
        Vector3 passSelfPos;
        bool passSelfSeen;
        RagdollPawn passTarget;
        float passLeft;

        /// <summary>The enemy that has just brushed past this pawn and can still be tripped (null: none).</summary>
        public RagdollPawn PassTarget => passLeft > 0f ? passTarget : null;
        public float PassLeft => Mathf.Max(0f, passLeft);

        /// <summary>Watches every enemy go by: the moment one stops coming nearer and starts going away (the closest point of
        /// their passing) within passRange, the two passing each other at passSpeed or more and the enemy the faster of the
        /// two, it can be tripped for passWindow seconds. Speeds are taken from the places seen a step ago, so a client can
        /// run it on what it is shown (its own pawn's ring) as the host runs it on the bodies.</summary>
        void TrackPasses(PawnRushSkillParams s, float dt)
        {
            if (dt <= 1e-5f) return;
            Vector3 me = bodies[0].position;
            Vector3 myVel = passSelfSeen ? Flat(me - passSelfPos) / dt : Vector3.zero;
            passSelfPos = me;
            passSelfSeen = true;
            if (passLeft > 0f)
            {
                passLeft -= dt;
                if (passLeft <= 0f || passTarget == null || passTarget.State != PawnState.Active)
                {
                    passTarget = null;
                    passLeft = 0f;
                }
            }
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other)) continue;
                Vector3 op = other.bodies[0].position;
                if (!passSeen.TryGetValue(other, out var seen))
                {
                    passSeen[other] = new PassTrack { pos = op };
                    continue;
                }
                Vector3 ov = Flat(op - seen.pos) / dt;
                Vector3 r = Flat(op - me), v = ov - myVel;
                float closing = Vector3.Dot(r, v);   // under 0 coming nearer, over 0 going away
                bool passed = seen.closing < 0f && closing >= 0f;
                passSeen[other] = new PassTrack { pos = op, closing = closing };
                if (!passed || other.State != PawnState.Active || other.SkillUntargetable) continue;
                if (r.magnitude > s.passRange || Mathf.Abs(op.y - me.y) > 1.2f) continue;
                // (Over 20 m/s is no run: a respawn, a teleport of the test bed.)
                if (v.magnitude < s.passSpeed || ov.magnitude > 20f || ov.magnitude < myVel.magnitude + 0.2f) continue;
                passTarget = other;
                passLeft = s.passWindow;
            }
        }

        void UpdatePawnPass(PawnRushSkillParams s, bool press, float dt)
        {
            TrackPasses(s, dt);
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    if (PassTarget == null)
                    {
                        // Nobody has just gone by: the key is ignored (a short "no" sound), no cooldown.
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Pawn);
                        Log("앙파상: 방금 스쳐 지나간 적이 없음 (무시)");
                        break;
                    }
                    SkillUses++;
                    planTarget = passTarget;
                    passTarget = null;
                    passLeft = 0f;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    skillHitSet.Clear();
                    skillDir = FlatDir(planTarget.bodies[0].position - bodies[0].position);
                    StartDash(s.passLungeSpeed, s.passLunge / Mathf.Max(0.1f, s.passLungeSpeed));
                    SkillDetail = $"앙파상 → {planTarget.DisplayName}";
                    Log($"앙파상: {planTarget.DisplayName}의 발목으로 뛰어듦");
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    var t = planTarget;
                    if (t != null && t.State == PawnState.Active && !t.SkillUntargetable)
                    {
                        // It bends onto the piece as it goes, and stops on reaching it.
                        Vector3 d = t.bodies[0].position - bodies[0].position;
                        if (Flat(d).sqrMagnitude > 0.01f) skillDir = Flat(d).normalized;
                        if (Flat(d).magnitude <= s.passReach && Mathf.Abs(d.y) <= 1.2f)
                        {
                            TripEnPassant(s, t);
                            dashLeft = 0f;
                        }
                    }
                    if (dashLeft > 0f)
                    {
                        if (WallAhead(dashBase * dt + 0.25f, out _)) dashLeft = 0f;
                        else
                        {
                            DriveDash(dt);
                            dashLeft -= dt;
                        }
                    }
                    if (dashLeft <= 0f)
                    {
                        HaltDash();   // it stands where the piece was
                        EndPlanSkill();
                        StartCooldown(s.passCooldown);
                    }
                    break;
                }
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The trip: the piece goes down the way it was running, at passTripSpeed at least (the faster it ran the
        /// further it slides).</summary>
        void TripEnPassant(PawnRushSkillParams s, RagdollPawn t)
        {
            Vector3 tv = Flat(t.bodies[0].linearVelocity);
            Vector3 way = tv.sqrMagnitude > 0.25f ? tv.normalized : skillDir;
            float extra = Mathf.Max(1f, s.passTripSpeed - tv.magnitude);
            string result = SkillHit(t, way * extra + Vector3.up * s.passTripLift, true, "앙파상");
            Fx(SkillFxKind.PawnHit, t, SkillContact(t), skillDir, 1);
            Log($"앙파상 → {t.DisplayName}: 발 걸기 (달리던 속도 {tv.magnitude:0.0} m/s) → {result}");
        }

        // ---------------------------------------------------------------- fork

        /// <summary>One of the fork's two spots (0 left, 1 right of the way it leaps), from the landing point.</summary>
        public Vector3 ForkSpot(int side)
        {
            var s = PawnRushSkills;
            if (s == null) return planPoint;
            return planPoint + Quaternion.Euler(0f, side == 0 ? -s.forkAngle : s.forkAngle, 0f) * planDir * s.forkAhead;
        }

        /// <summary>Where the leap comes down: forkLeap ahead on the floor there, short of a wall in the way.</summary>
        Vector3 ForkLanding(PawnRushSkillParams s, Vector3 dir)
        {
            Vector3 feet = FeetPoint;
            float reach = s.forkLeap;
            if (SolidRay(feet + Vector3.up * 0.35f, dir, reach + 0.3f, out var wall) && Mathf.Abs(wall.normal.y) < 0.5f)
                reach = Mathf.Max(0.4f, wall.distance - 0.4f);
            Vector3 at = feet + dir * reach;
            if (SolidRay(at + Vector3.up * 1.5f, Vector3.down, 5f, out var floor) && floor.normal.y > 0.5f) at.y = floor.point.y;
            return at;
        }

        void UpdateKnightFork(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planDir = AimFlat();
                    planPoint = ForkLanding(s, planDir);
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    facing = planDir;
                    SkillDetail = "포크 준비 (두 자리가 보임)";
                    break;
                case PlanPhase.Windup:
                    // Not rooted: a running piece keeps its speed into the leap, the spots go with it.
                    planTime += dt;
                    planPoint = ForkLanding(s, planDir);
                    if (planTime >= s.forkWindup)
                    {
                        Vector3 p = bodies[0].position;
                        Launch(LeapVelocity(p, planPoint + Vector3.up * standHeight, s.forkFlight, s.forkHeight));
                        facing = planDir;
                        planPhase = PlanPhase.Active;
                        planTime = 0f;
                        SkillDetail = "포크 도약";
                    }
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    if (Climbing || rope != null || planTime > s.forkFlight + 1.2f)
                    {
                        // Caught a ledge on the way, or still not down long after (it leapt off an edge): no kick.
                        EndPlanSkill();
                        StartCooldown(s.forkCooldown);
                    }
                    else if (planTime > 0.2f && Grounded)
                    {
                        ForkLand(s);
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        SkillDetail = "착지 후딜";
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.forkRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.forkCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The kick, judged from where it really came down: enemies standing on both spots at once all go down and
        /// fly outward; on one spot only, they are shoved.</summary>
        void ForkLand(PawnRushSkillParams s)
        {
            Vector3 feet = FeetPoint;
            planPoint = feet;
            Vector3 left = ForkSpot(0), right = ForkSpot(1);
            var onLeft = new List<RagdollPawn>();
            var onRight = new List<RagdollPawn>();
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.State == PawnState.Ragdoll || other.SkillUntargetable) continue;
                Vector3 op = other.FeetPoint;
                if (Mathf.Abs(op.y - feet.y) > 1.2f) continue;
                float dl = Flat(op - left).magnitude, dr = Flat(op - right).magnitude;
                if (dl <= s.forkRadius && dl <= dr) onLeft.Add(other);
                else if (dr <= s.forkRadius) onRight.Add(other);
            }
            bool both = onLeft.Count > 0 && onRight.Count > 0;
            int hit = 0;
            foreach (var list in new[] { onLeft, onRight })
                foreach (var other in list)
                {
                    Vector3 away = FlatDir(other.bodies[0].position - bodies[0].position);
                    string result = both
                        ? SkillHit(other, away * s.forkPush + Vector3.up * s.forkLift, true, "포크")
                        : SkillHit(other, away * s.forkSinglePush, false, "포크 (한 자리만)");
                    hit++;
                    Log($"포크 → {other.DisplayName}: {result}");
                }
            Fx(SkillFxKind.KnightLand, null, feet, planDir, hit, s.forkRadius);
            Log(both ? $"포크: 두 자리 모두 맞힘 — {hit}명 넘어짐" : hit > 0 ? "포크: 한 자리만 맞힘 — 밀기만" : "포크: 빗나감");
        }

        // ---------------------------------------------------------------- pin

        // The piece a pin holds (the host's; a client's puppet is told): what it is tied to, for how long.
        RagdollPawn pinMate;
        Vector3 pinPost;
        bool pinToPost;
        float pinLeft, pinRest;

        /// <summary>Tied by a pin now: to another piece (<see cref="PinMate"/>) or to a wall or pillar.</summary>
        public bool Pinned => pinLeft > 0f;
        public float PinLeft => Mathf.Max(0f, pinLeft);
        public RagdollPawn PinMate => pinLeft > 0f && !pinToPost ? pinMate : null;
        public bool PinnedToPost => pinLeft > 0f && pinToPost;
        /// <summary>The other end of the rope: the piece it is tied to (its waist), or the post.</summary>
        public Vector3 PinAnchor => pinToPost || pinMate == null ? pinPost : pinMate.LassoBelly;

        /// <summary>The way the line would go: flat along the aim; an enemy within pinAssist of it, in reach and in sight, is
        /// aimed at.</summary>
        Vector3 PinAim(PawnRushSkillParams s)
        {
            Vector3 flat = AimFlat(), from = LassoHands;
            planTarget = null;
            float best = s.pinAssist;
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable
                    || other.pinLeft > 0f) continue;
                Vector3 to = other.LassoBelly - from;
                float across = Flat(to).magnitude;
                if (across < 0.6f || across > s.pinRange || Mathf.Abs(to.y) > 2f) continue;
                float angle = Vector3.Angle(flat, Flat(to));
                if (angle > best || !ClearSight(from, other.LassoBelly)) continue;
                planTarget = other;
                best = angle;
            }
            return planTarget != null ? FlatDir(planTarget.LassoBelly - from) : flat;
        }

        void UpdateBishopPin(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (press && CanStartSkill())
                    {
                        planPhase = PlanPhase.Aim;
                        planTime = 0f;
                        skillGrabLatch = skillGrabRaw;
                        planDir = PinAim(s);
                        SkillDetail = "핀 조준 (좌클릭 쏘기 · 우클릭/스킬 키 취소)";
                    }
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    planDir = PinAim(s);
                    if (Flat(skillMoveRaw).sqrMagnitude < 0.04f) facing = FlatDir(planDir);
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        Log("핀: 조준 취소");
                        break;
                    }
                    if (confirm)
                    {
                        SkillUses++;
                        FirePin(s);
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        SkillDetail = "핀";
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.pinRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.pinCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The line, at once: the first enemy on it (short of a wall), then what is close behind that one on the same
        /// line - another enemy, or an upright wall or pillar - and the two are tied. Nothing behind: a short stagger.</summary>
        void FirePin(PawnRushSkillParams s)
        {
            Vector3 from = LassoHands, dir = FlatDir(planDir);
            float reach = s.pinRange;
            if (SolidRay(from, dir, reach, out var ahead)) reach = ahead.distance;
            var first = PinFirst(s, from, dir, 0.5f, reach, null);
            planTarget = first;
            if (first == null)
            {
                planPoint = from + dir * reach;
                Log("핀: 아무도 안 맞음");
                return;
            }
            Vector3 a = first.LassoBelly;
            float behind = s.pinBehind;
            bool post = SolidRay(a, dir, behind, out var wall) && Mathf.Abs(wall.normal.y) < 0.5f;
            if (post) behind = wall.distance;
            var second = PinFirst(s, a, dir, 0.3f, behind, first);
            if (second != null)
            {
                first.BePinned(second, Vector3.zero, false, s);
                second.BePinned(first, Vector3.zero, false, s);
                planPoint = second.LassoBelly;
                Fx(SkillFxKind.PawnHit, first, first.LassoBelly, dir, 0);
                Log($"핀: {first.DisplayName}와 {second.DisplayName}를 꿰어 묶음 ({s.pinSeconds:0.#}초, 줄 {s.pinRest:0.#} m)");
            }
            else if (post)
            {
                first.BePinned(null, wall.point, true, s);
                planPoint = wall.point;
                Fx(SkillFxKind.PawnHit, first, first.LassoBelly, dir, 0);
                Log($"핀: {first.DisplayName}를 뒤의 벽 · 기둥에 묶음 ({s.pinSeconds:0.#}초)");
            }
            else
            {
                planPoint = a + dir * 1.2f;
                first.Stagger(s.pinMissStagger);
                first.LastSkillHit = "핀: 뒤에 아무것도 없음 → 휘청";
                Log($"핀: {first.DisplayName} 뒤에 아무것도 없음 → 휘청 {s.pinMissStagger:0.#}초");
            }
        }

        /// <summary>The nearest enemy the line takes between <paramref name="min"/> and <paramref name="max"/> along it.</summary>
        RagdollPawn PinFirst(PawnRushSkillParams s, Vector3 from, Vector3 dir, float min, float max, RagdollPawn skip)
        {
            RagdollPawn best = null;
            float bestAlong = max;
            foreach (var other in All)
            {
                if (other == null || other == this || other == skip || !IsEnemyOf(other) || other.State != PawnState.Active
                    || other.SkillUntargetable || other.NetworkPuppet || other.pinLeft > 0f || other.chainCarry || other.kingCarry
                    || other.castleCarry) continue;
                Vector3 to = other.LassoBelly - from;
                if (Mathf.Abs(to.y) > 2f) continue;
                Vector3 flat = Flat(to);
                float along = Vector3.Dot(flat, dir);
                if (along < min || along > bestAlong) continue;
                if ((flat - dir * along).magnitude > s.pinRadius + 0.3f) continue;
                best = other;
                bestAlong = along;
            }
            return best;
        }

        void BePinned(RagdollPawn mate, Vector3 post, bool toPost, PawnRushSkillParams s)
        {
            pinMate = toPost ? null : mate;
            pinPost = post;
            pinToPost = toPost;
            pinLeft = s.pinSeconds;
            pinRest = s.pinRest;
            pinIsTie = false;   // (a pin, not the queen's elastic: RagdollPawn.BookSkills.cs)
            pinStiff = s.pinStiffness;
            pinPullMax = s.pinPull;
            LastSkillHit = toPost ? "핀: 벽 · 기둥에 묶임" : $"핀: {(mate != null ? mate.DisplayName : "?")}와 묶임";
        }

        /// <summary>The rope is gone, at both ends.</summary>
        void EndPin()
        {
            var mate = pinMate;
            pinLeft = 0f;
            pinMate = null;
            pinToPost = false;
            pinIsTie = false;
            if (mate != null && mate.pinMate == this) mate.EndPin();
        }

        /// <summary>How fast the pin's rope drags this piece this step (m/s): toward its other end as far as it is stretched
        /// past its length (added to a bishop's X rope's drag, RagdollPawn.UpdateLassoDrag).</summary>
        Vector3 PinPull(PawnRushSkillParams s)
        {
            if (pinLeft <= 0f || s == null) return Vector3.zero;
            Vector3 to = PinAnchor - LassoBelly;
            float d = to.magnitude, stretch = d - pinRest;
            if (stretch <= 0f || d <= 1e-3f) return Vector3.zero;
            // (The pin's own numbers, or the ones of the queen's elastic: RagdollPawn.BookSkills.cs.)
            float stiff = pinStiff > 0f ? pinStiff : s.pinStiffness, most = pinPullMax > 0f ? pinPullMax : s.pinPull;
            return to / d * Mathf.Min(stiff * stretch, most);
        }

        /// <summary>A pinned piece's step (the host's): the time runs out, the other end is gone, it is in the water or out of
        /// every skill's reach (respawned, on the finish), or the rope is stretched past pinBreak and snaps.</summary>
        void UpdatePin(PawnRushSkillParams s, float dt)
        {
            if (pinLeft <= 0f) return;
            if (pinIsTie)
            {
                UpdateTie(s, dt);   // the queen's elastic: it does not break, it snaps the two together
                return;
            }
            pinLeft -= dt;
            bool lost = !pinToPost && (pinMate == null || pinMate.pinMate != this || pinMate.pinLeft <= 0f);
            float stretch = Vector3.Distance(PinAnchor, LassoBelly) - pinRest;
            if (pinLeft > 0f && !lost && !Floating && !SkillUntargetable && stretch <= s.pinBreak) return;
            if (stretch > s.pinBreak) Log($"{DisplayName}: 핀 줄이 너무 늘어나 끊어짐 ({stretch:0.0} m)");
            EndPin();
        }

        // ---------------------------------------------------------------- castling

        // The piece a castling is offered to (the host's): by whom, how long ago, its answer.
        RagdollPawn castleOfferBy;
        float castleOfferAge;
        bool castleAccepted;
        // The swap's flight, either piece (and the knight's press leap, RagdollPawn.HillSkills.cs: an arc of its own
        // to a spot, or onto a head it follows; nothing on the way drops it).
        bool castleCarry, castleForced, carryFree;
        Vector3 castleFrom, castleTo;
        float castleT, castleTime, castleArcHeight, castleSideWidth;
        RagdollPawn carryHome;

        /// <summary>A castling's numbers: plan 1-나's, or the ones of Queen of the Hill's swap (the same skill made twice:
        /// that one reaches less far, waits longer for the answer, costs nothing when refused, and flies lower and quicker).</summary>
        struct CastleNumbers
        {
            public float range, assist, window, dummy, flight, arc, cooldown, refused;
            public bool enemies;
            public string name;
        }

        static CastleNumbers CastleOf(PawnRushSkillParams s, int id) => id == PawnRushSkillIds.CastleSwap
            ? new CastleNumbers { range = s.swapRange, assist = s.swapCone, window = s.swapWait, dummy = s.swapDummyAnswer, flight = s.swapTime,
                                  arc = s.swapArc, cooldown = s.swapCooldown, refused = 0f, enemies = false, name = "캐슬링 교대" }
            : new CastleNumbers { range = s.castleRange, assist = s.castleAssist, window = s.castleWindow, dummy = s.castleDummyAnswer,
                                  flight = s.castleFlight, arc = s.castleArc, cooldown = s.castleCooldown, refused = s.castleRefusedCooldown,
                                  enemies = s.castleEnemies, name = "캐슬링" };

        /// <summary>The seconds a teammate has to answer this piece's castling (the one it is casting, or would).</summary>
        public float CastleWindow => PawnRushSkills != null ? CastleOf(PawnRushSkills, PlanSkill).window : 2f;

        /// <summary>In a castling's flight now (the host's).</summary>
        public bool CastleFlying => castleCarry;

        /// <summary>A teammate that offers this piece a castling now (any PC: its state comes from the host).</summary>
        public RagdollPawn CastleOfferingMe()
        {
            foreach (var p in All)
                if (p != null && p != this && PawnRushSkillIds.Castle(p.PlanSkill) && p.planPhase == PlanPhase.Windup
                    && p.planTarget == this && p.IsAllyOf(this)) return p;
            return null;
        }

        /// <summary>The seconds a teammate still has to answer (0 unless this piece waits for one).</summary>
        public float CastleOfferLeft =>
            PawnRushSkillIds.Castle(PlanSkill) && planPhase == PlanPhase.Windup && PawnRushSkills != null
                ? Mathf.Max(0f, CastleWindow - planTime) : 0f;

        /// <summary>The piece it would castle with: a teammate (an enemy too with castleEnemies) standing within
        /// castleAssist of the aim, in reach and in sight; the nearest to the aim.</summary>
        RagdollPawn FindCastleMate(PawnRushSkillParams s, CastleNumbers c)
        {
            Vector3 flat = AimFlat(), from = bodies[(int)BodyId.Chest].position;
            RagdollPawn best = null;
            float bestAngle = c.assist;
            foreach (var other in All)
            {
                if (other == null || other == this || other.State != PawnState.Active || other.SkillUntargetable || other.Floating
                    || other.chainCarry || other.kingCarry || other.castleCarry) continue;
                if (!IsAllyOf(other) && !(c.enemies && IsEnemyOf(other))) continue;
                Vector3 to = other.bodies[(int)BodyId.Chest].position - from;
                float across = Flat(to).magnitude;
                if (across < 1.5f || across > c.range) continue;
                float angle = Vector3.Angle(flat, Flat(to));
                if (angle > bestAngle || !ClearSight(from, other.bodies[(int)BodyId.Chest].position)) continue;
                best = other;
                bestAngle = angle;
            }
            return best;
        }

        void UpdateRookCastle(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt, int id)
        {
            var c = CastleOf(s, id);
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (press && CanStartSkill())
                    {
                        planPhase = PlanPhase.Aim;
                        planTime = 0f;
                        skillGrabLatch = skillGrabRaw;
                        planTarget = FindCastleMate(s, c);
                        SkillDetail = $"{c.name} 조준 (팀원을 보고 좌클릭 · 우클릭/스킬 키 취소)";
                    }
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    planTarget = FindCastleMate(s, c);
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        Log($"{c.name}: 조준 취소");
                        break;
                    }
                    if (!confirm) break;
                    if (planTarget == null)
                    {
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Rook);
                        Log("캐슬링: 바꿀 기물이 조준에 없음");
                        break;
                    }
                    SkillUses++;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    if (IsAllyOf(planTarget))
                    {
                        planTarget.castleOfferBy = this;
                        planTarget.castleOfferAge = 0f;
                        planTarget.castleAccepted = false;
                        SkillDetail = $"캐슬링 제안 → {planTarget.DisplayName} (스킬 키로 받기)";
                        Log($"{c.name} → {planTarget.DisplayName}(팀원): {c.window:0.#}초 안에 스킬 키로 받기");
                    }
                    else
                    {
                        SkillDetail = $"캐슬링 예고 → {planTarget.DisplayName}";
                        Log($"캐슬링 → {planTarget.DisplayName}(적): {s.castleEnemyWindup:0.#}초 뒤 자리 바꿈");
                    }
                    break;
                case PlanPhase.Windup:
                {
                    planTime += dt;
                    var mate = planTarget;
                    if (Staggered || bindLeft > 0f || mate == null || mate.SkillUntargetable || mate.Floating || mate.castleCarry)
                    {
                        Log($"{c.name}: 끊김");
                        EndPlanSkill();
                        StartCooldown(c.cooldown);
                        break;
                    }
                    if (IsAllyOf(mate))
                    {
                        if (mate.castleAccepted)
                        {
                            mate.castleAccepted = false;
                            StartCastle(s, mate, false, c);
                        }
                        else if (planTime >= c.window)
                        {
                            Log($"{c.name}: {mate.DisplayName}가 받지 않음");
                            EndPlanSkill();
                            if (c.refused > 0f) StartCooldown(c.refused);
                        }
                        break;
                    }
                    // The variant: an enemy, without asking. It gets out of it behind a wall or in the air at the end.
                    if (planTime < s.castleEnemyWindup) break;
                    bool seen = ClearSight(bodies[(int)BodyId.Chest].position, mate.bodies[(int)BodyId.Chest].position);
                    if (!seen || !mate.OnTheFloor || mate.State != PawnState.Active)
                    {
                        Log($"캐슬링: {mate.DisplayName}가 피함 ({(!seen ? "가려짐" : "공중")})");
                        EndPlanSkill();
                        StartCooldown(c.cooldown);
                        break;
                    }
                    StartCastle(s, mate, true, c);
                    break;
                }
                case PlanPhase.Active:
                    // (Its own flight holds the step back; this runs once it has come down or been dropped.)
                    planTime += dt;
                    if (!castleCarry)
                    {
                        EndPlanSkill();
                        StartCooldown(c.cooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>A castling offered to this piece this step (the host's): its own skill key answers yes (a test dummy answers
        /// by itself). Returns the key press if it was not used.</summary>
        bool UpdatePlanMate(PawnRushSkillParams s, bool press, float dt)
        {
            var by = castleOfferBy;
            if (by == null) return press;
            if (!PawnRushSkillIds.Castle(by.PlanSkill) || by.planPhase != PlanPhase.Windup || by.planTarget != this)
            {
                castleOfferBy = null;
                return press;
            }
            castleOfferAge += dt;
            bool yes = SkillBot ? castleOfferAge >= CastleOf(s, by.PlanSkill).dummy : press;
            if (!yes) return press;
            // Down, tied, held or in the water: the answer waits (the key is used up).
            if (State != PawnState.Active || bindLeft > 0f || BeingHeld || Floating) return false;
            castleAccepted = true;
            castleOfferBy = null;
            Log($"{DisplayName}: 캐슬링을 받음");
            return false;
        }

        void StartCastle(PawnRushSkillParams s, RagdollPawn mate, bool forced, CastleNumbers c)
        {
            Vector3 a = bodies[0].position, b = mate.bodies[0].position;
            float aFloor = FeetPoint.y, bFloor = mate.FeetPoint.y;
            // Each lands standing on the other's floor.
            mate.BeginCastleCarry(new Vector3(a.x, aFloor + mate.standHeight, a.z), s, forced, false, c);
            BeginCastleCarry(new Vector3(b.x, bFloor + standHeight, b.z), s, false, true, c);
            MarkRoyalCastle(mate, forced);   // the king and the rook: the real thing (RagdollPawn.KitSkills.cs)
            // The hand feel: the two hold a moment as they go (the values count only for an enemy swapped without asking).
            FeelHit(mate, SkillContact(mate), FlatDir(b - a), FeelWeak, 1, forced);
            planPhase = PlanPhase.Active;
            planTime = 0f;
            planTarget = mate;
            SkillDetail = c.name + "!";
            Log($"{c.name}: {DisplayName} ↔ {mate.DisplayName} 자리 바꿈 ({Flat(b - a).magnitude:0.0} m, {c.flight:0.0#}초)");
        }

        void BeginCastleCarry(Vector3 to, PawnRushSkillParams s, bool forced, bool own, CastleNumbers c)
        {
            if (!own) ClearSkills();   // the other piece's own skill stops; the cooldowns it has keep running
            DropHook();
            DropRope();
            EndClimbPose();
            LetGo(0.3f);
            castleCarry = true;
            castleForced = forced;
            carryFree = false;
            carryHome = null;
            castleFrom = bodies[0].position;
            castleTo = to;
            castleT = 0f;
            castleTime = Mathf.Max(0.1f, c.flight);
            // The caster's arc is the higher, and each bows out to its own right: they pass each other, not through.
            castleArcHeight = own ? c.arc : c.arc * 0.7f;
            castleSideWidth = s.castleSide;
        }

        /// <summary>The knight's press leap (RagdollPawn.HillSkills.cs): the same carried flight on an arc of its own - over
        /// <paramref name="time"/> seconds, its top at <paramref name="topY"/> - to a spot, or onto the head of
        /// <paramref name="home"/>, which the arc's end follows. Nothing on the way drops it.</summary>
        void BeginArcCarry(Vector3 to, float time, float topY, RagdollPawn home)
        {
            DropHook();
            DropRope();
            EndClimbPose();
            LetGo(0.3f);
            castleCarry = true;
            castleForced = false;
            carryFree = true;
            carryHome = home;
            castleFrom = bodies[0].position;
            castleTo = to;
            castleT = 0f;
            castleTime = Mathf.Max(0.1f, time);
            castleArcHeight = Mathf.Max(0.2f, topY - (castleFrom.y + to.y) * 0.5f);
            castleSideWidth = 0f;
        }

        Vector3 CastlePath(float t)
        {
            float u = Mathf.Clamp01(t / castleTime);
            Vector3 along = Flat(castleTo - castleFrom);
            Vector3 side = along.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, along.normalized) : Vector3.zero;
            float bow = 4f * u * (1f - u);
            return Vector3.Lerp(castleFrom, castleTo, u) + Vector3.up * (castleArcHeight * bow) + side * (castleSideWidth * bow);
        }

        /// <summary>The swap's flight this step (the host's). True while it runs: the rest of the skills wait.</summary>
        bool UpdatePlanCarry(PawnRushSkillParams s, float dt)
        {
            if (!castleCarry) return false;
            if (State != PawnState.Active || Floating)
            {
                EndCastleCarry(false);
                return false;
            }
            castleT += dt;
            grabImmune = Mathf.Max(grabImmune, 0.25f);
            if (castleT >= castleTime)
            {
                EndCastleCarry(true);
                return false;
            }
            if (carryHome != null && carryHome.State == PawnState.Active)
            {
                // Coming down on a head: the end of the arc follows it if the piece moves (5 m/s, as the mode had it).
                Vector3 head = carryHome.bodies[(int)BodyId.Head].position;
                castleTo = Vector3.MoveTowards(castleTo, new Vector3(head.x, castleTo.y, head.z), 5f * dt);
            }
            Vector3 at = bodies[0].position, next = CastlePath(castleT + dt);
            Vector3 step = next - at;
            // Something solid on the way drops it there (as a rook's chain does); a leap goes its own way.
            if (!carryFree && castleT > 0.1f && step.sqrMagnitude > 1e-6f && ChainBlocked(at, step))
            {
                Log($"{DisplayName}: 캐슬링 도중 장애물에 걸려 떨어짐");
                EndCastleCarry(false);
                return false;
            }
            Fly(next, step / Mathf.Max(1e-4f, dt), 0f, default, 18f);
            return true;
        }

        void EndCastleCarry(bool arrived)
        {
            if (!castleCarry) return;
            castleCarry = false;
            carryFree = false;
            carryHome = null;
            LandRoyalCastle(arrived);
            if (!arrived)
            {
                EndFlight(bodies[0].linearVelocity * 0.5f);
                return;
            }
            EndFlight(Vector3.zero);
            KeepSkillLandingSafe();
            if (castleForced && PawnRushSkills != null)
            {
                Stagger(PawnRushSkills.castleEnemyStagger);
                LastSkillHit = "캐슬링: 자리가 바뀜 → 휘청";
            }
        }

        // ---------------------------------------------------------------- check

        /// <summary>A piece that has this one in check now (any PC).</summary>
        public RagdollPawn CheckedBy()
        {
            foreach (var p in All)
                if (p != null && p != this && p.PlanSkill == PawnRushSkillIds.Check && p.planPhase == PlanPhase.Active && p.planTarget == this) return p;
            return null;
        }

        /// <summary>The seconds the checked piece still has to leave the square.</summary>
        public float CheckLeft =>
            PlanSkill == PawnRushSkillIds.Check && planPhase == PlanPhase.Active && PawnRushSkills != null
                ? Mathf.Max(0f, PawnRushSkills.checkSeconds - planTime) : 0f;

        /// <summary>The enemy it would check: standing within checkAssist of the aim, in reach and in sight.</summary>
        RagdollPawn FindCheckTarget(PawnRushSkillParams s)
        {
            Vector3 flat = AimFlat(), eye = bodies[(int)BodyId.Head].position;
            RagdollPawn best = null;
            float bestAngle = s.checkAssist;
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable
                    || other.chainCarry || other.kingCarry || other.castleCarry) continue;
                Vector3 to = other.bodies[0].position - bodies[0].position;
                float across = Flat(to).magnitude;
                if (across < 0.8f || across > s.checkRange || Mathf.Abs(to.y) > 2.5f) continue;
                float angle = Vector3.Angle(flat, Flat(to));
                if (angle > bestAngle || !ClearSight(eye, other.bodies[(int)BodyId.Head].position)) continue;
                best = other;
                bestAngle = angle;
            }
            return best;
        }

        void UpdateQueenCheck(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (press && CanStartSkill())
                    {
                        // The pool's Q-기1: one more check is free within a moment of a dodged one (RagdollPawn.PoolSkills.cs).
                        checkFollowUp = checkAgainLeft > 0f;
                        checkAgainLeft = 0f;
                        checkAgainNext = false;
                        planPhase = PlanPhase.Aim;
                        planTime = 0f;
                        skillGrabLatch = skillGrabRaw;
                        planTarget = FindCheckTarget(s);
                        SkillDetail = checkFollowUp ? "체크를 잇는다 (적을 보고 좌클릭)" : "체크 조준 (적을 보고 좌클릭 · 우클릭/스킬 키 취소)";
                    }
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    planTarget = FindCheckTarget(s);
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        // (The free one called off: its cooldown runs, so it cannot be held back.)
                        if (checkFollowUp) StartCooldown(s.checkCooldown);
                        checkFollowUp = false;
                        Log("체크: 조준 취소");
                        break;
                    }
                    if (!confirm) break;
                    if (planTarget == null)
                    {
                        Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)PieceKind.Queen);
                        Log("체크: 조준에 적이 없음");
                        break;
                    }
                    SkillUses++;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    planPoint = planTarget.FeetPoint;   // the square: where it stands now
                    facing = FlatDir(planTarget.bodies[0].position - bodies[0].position);
                    // The pool's Q-기1: she thrusts her sceptre and slides that way (RagdollPawn.HandFeel.cs, Kick).
                    Kick(facing, s.checkLunge, s.checkLungeTime, true);
                    SkillDetail = $"체크! → {planTarget.DisplayName}";
                    Log($"체크! → {planTarget.DisplayName} ({s.checkSeconds:0.#}초 안에 칸 밖으로)");
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    var t = planTarget;
                    if (Staggered || bindLeft > 0f)
                    {
                        // "Taken": the checker was hit before its check came down.
                        Log("체크: 맞아서 취소됨");
                        EndPlanSkill();
                        StartCooldown(s.checkCooldown);
                        break;
                    }
                    if (t == null || t.SkillUntargetable)
                    {
                        EndPlanSkill();
                        StartCooldown(s.checkCooldown);
                        break;
                    }
                    if (planTime < s.checkSeconds) break;
                    ResolveCheck(s, t);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    SkillDetail = "체크 후딜";
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.checkRecovery)
                    {
                        bool again = checkAgainNext;
                        checkAgainNext = checkFollowUp = false;
                        EndPlanSkill();
                        // The pool's Q-기1: dodged, the check may be played once more for a moment, no cooldown yet.
                        if (again) OpenCheckAgain(s);
                        else StartCooldown(s.checkCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>The check comes down: a piece still on its square is knocked flying away from the checker - unless a piece
        /// of its side stands between the two, which takes the blow instead (the block).</summary>
        void ResolveCheck(PawnRushSkillParams s, RagdollPawn t)
        {
            Vector3 feet = t.FeetPoint;
            bool onSquare = Flat(feet - planPoint).magnitude <= s.checkSquare && Mathf.Abs(feet.y - planPoint.y) <= 1.5f;
            if (!onSquare)
            {
                FeelMiss(planPoint, FlatDir(feet - planPoint));   // the hand feel: the queen's miss
                checkAgainNext = !checkFollowUp;   // the pool's Q-기1: once more (not after the free one)
                Log($"체크: {t.DisplayName}가 칸을 벗어남 (피함)" + (checkAgainNext ? $" → {s.checkAgainWindow:0.#}초 안에 한 번 더" : ""));
                return;
            }
            var blocker = CheckBlocker(s, t);
            var hit = blocker != null ? blocker : t;
            Vector3 away = FlatDir(hit.bodies[0].position - bodies[0].position);
            string result = SkillHit(hit, away * s.checkPush + Vector3.up * s.checkLift, true, blocker != null ? "체크 (대신 막음)" : "체크");
            Fx(SkillFxKind.QueenHit, hit, hit.bodies[0].position, away, 1);
            // The pool's Q-기1: the blow holds 0.08 s, and the queen bounces a step back with it.
            FeelHit(hit, hit.bodies[0].position, away, FeelHard, baseStop: s.checkStop);
            Kick(-away, s.checkBounce, 0.15f, true);
            Log(blocker != null
                ? $"체크: {blocker.DisplayName}가 몸으로 막음 → {result}"
                : $"체크 → {t.DisplayName}: 칸에 남아 있음 → {result}");
        }

        /// <summary>A standing piece of the checked one's side between it and the checker, within checkBlock of the line (the
        /// nearest to the checker).</summary>
        RagdollPawn CheckBlocker(PawnRushSkillParams s, RagdollPawn t)
        {
            Vector3 q = bodies[0].position, line = Flat(t.bodies[0].position - q);
            float length = line.magnitude;
            if (length < 1.2f) return null;
            Vector3 dir = line / length;
            RagdollPawn best = null;
            float bestAlong = length - 0.5f;
            foreach (var other in All)
            {
                if (other == null || other == this || other == t || !IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable) continue;
                Vector3 to = other.bodies[0].position - q;
                if (Mathf.Abs(to.y) > 1.5f) continue;
                Vector3 flat = Flat(to);
                float along = Vector3.Dot(flat, dir);
                if (along < 0.5f || along > bestAlong) continue;
                if ((flat - dir * along).magnitude > s.checkBlock) continue;
                best = other;
                bestAlong = along;
            }
            return best;
        }

        // ---------------------------------------------------------------- over the network (RagdollPawn.SkillNet.cs)

        // Flags2 bits of a piece's skill state (128 is a retired mode's): a new skill's phase is in it; a pin is on the piece.
        // (16, 32 and 64 are the idea book's bundle A's: RagdollPawn.BookSkills.cs.)
        const byte NetNewSkill = 1, NetPinned = 2, NetPinPost = 4, NetWarded = 8;

        /// <summary>Something of the slots' beyond the first slot's cooldown is worth sending: a new skill under way, a pin on
        /// this piece, another slot's cooldown.</summary>
        bool SlotNetBusy => planPhase != PlanPhase.None || pinLeft > 0f || wardLeft > 0f || slotCd[1] > 0f || slotCd[2] > 0f || BookNetBusy
                            || KitNetBusy;

        /// <summary>Host: the slots' part of this piece's skill state - the skill under way and each slot's cooldown.</summary>
        void CaptureSlotNet(ref SkillWireState s)
        {
            s.Skill = (byte)Mathf.Clamp(ActiveSkill, 0, 255);
            s.Cooldown = Mathf.Max(0f, slotCd[0]);
            s.CooldownTotal = Mathf.Max(0f, slotCdTotal[0]);
            s.Cooldown2 = Mathf.Max(0f, slotCd[1]);
            s.Cooldown3 = Mathf.Max(0f, slotCd[2]);
        }

        /// <summary>Host: a new skill's part of the state. D its phase, A its time, P its point, the direction its way,
        /// Target its target.</summary>
        void CapturePlanNet(ref SkillWireState s, ref Vector3 dir, ref Vector3 p, ref RagdollPawn target)
        {
            s.Flags2 |= NetNewSkill;
            s.D = (byte)planPhase;
            s.C = planNote;   // the skill's own small number (the aim can go there, the shots left)
            s.A = planTime;
            dir = planDir;
            p = planPoint;
            target = planTarget;
        }

        /// <summary>Host: a pin on this piece. Other the piece it is tied to, Q the post.</summary>
        void CapturePinNet(ref SkillWireState s, Func<RagdollPawn, byte> refOf, ref Vector3 q)
        {
            CaptureBookNet(ref s);   // touched, numb, a ghost, the queen's elastic (RagdollPawn.BookSkills.cs)
            CaptureKitNet(ref s);    // heavy feet (RagdollPawn.KitSkills.cs)
            if (wardLeft > 0f) s.Flags2 |= NetWarded;   // a close guard on this piece (RagdollPawn.HillSkills.cs)
            if (pinLeft <= 0f) return;
            s.Flags2 |= NetPinned;
            if (pinToPost)
            {
                s.Flags2 |= NetPinPost;
                q = pinPost;
            }
            else if (pinMate != null) s.Other = refOf(pinMate);
        }

        /// <summary>Client: the skill under way and the slots' cooldowns of this puppet.</summary>
        void ApplySlotNet(in SkillWireState s)
        {
            activeSkill = s.Skill;
            activeSlot = Loadout != null ? Loadout.SlotOf(piece, s.Skill) : -1;
            oldHome = PawnRushSkillIds.Old(s.Skill) ? (int)PawnRushSkillIds.OldPiece(s.Skill) : -1;
            SeeSlotCooldown(1, s.Cooldown2);
            SeeSlotCooldown(2, s.Cooldown3);
        }

        void SeeSlotCooldown(int slot, float left)
        {
            slotCd[slot] = left;
            if (left <= 0f) slotCdTotal[slot] = 0f;
            else if (left > slotCdTotal[slot]) slotCdTotal[slot] = left;
        }

        /// <summary>Client: a new skill's phase, point and target, and a pin on the puppet.</summary>
        void ApplyPlanNet(in SkillWireState s, Vector3 dir, Vector3 p, Vector3 q, RagdollPawn target, Func<byte, RagdollPawn> pawnOf)
        {
            if ((s.Flags2 & NetNewSkill) != 0)
            {
                planPhase = (PlanPhase)s.D;
                planNote = s.C;
                planTime = s.A;
                if (dir.sqrMagnitude > 0.5f) planDir = dir.normalized;
                planPoint = p;
                planTarget = target;
            }
            else
            {
                planPhase = PlanPhase.None;
                planNote = 0;
                planTime = 0f;
                planTarget = null;
            }
            ApplyBookNet(s);   // touched, numb, a ghost, and whether a pin on it is the queen's elastic
            ApplyKitNet(s);    // heavy feet
            // (A guard's time left is this screen's own count from when it first saw it, as a pin's is.)
            if ((s.Flags2 & NetWarded) == 0) wardLeft = 0f;
            else if (wardLeft <= 0f) wardLeft = PawnRushSkills != null ? PawnRushSkills.wardTime : 2.9f;
            bool pinned = (s.Flags2 & NetPinned) != 0;
            if (!pinned)
            {
                pinLeft = 0f;
                pinMate = null;
                pinToPost = false;
                return;
            }
            // (The time left is this screen's own count from when it first saw the pin.)
            if (pinLeft <= 0f) pinLeft = PawnRushSkills == null ? 5f : pinIsTie ? PawnRushSkills.hareSeconds : PawnRushSkills.pinSeconds;
            pinToPost = (s.Flags2 & NetPinPost) != 0;
            if (pinToPost) pinPost = q;
            pinMate = !pinToPost && s.Other != SkillWire.None ? pawnOf(s.Other) : null;
            if (PawnRushSkills != null) pinRest = pinIsTie ? PawnRushSkills.hareRest : PawnRushSkills.pinRest;
        }

        /// <summary>Client: this piece is not in the host's packet.</summary>
        void ClearSlotNet()
        {
            planPhase = PlanPhase.None;
            planTime = 0f;
            planTarget = null;
            pinLeft = 0f;
            pinMate = null;
            pinToPost = false;
            wardLeft = 0f;
            ClearBookNet();
            ClearKitNet();
            planNote = 0;
            activeSkill = 0;
            activeSlot = -1;
            oldHome = -1;
            slotCd[1] = slotCd[2] = 0f;
            slotCdTotal[1] = slotCdTotal[2] = 0f;
        }

        /// <summary>Client: between two of the host's packets the slots' clocks run on.</summary>
        void AdvanceSlotView(float dt)
        {
            if (planPhase != PlanPhase.None) planTime += dt;
            if (pinLeft > 0f) pinLeft = Mathf.Max(0.05f, pinLeft - dt);
            if (wardLeft > 0f) wardLeft = Mathf.Max(0.05f, wardLeft - dt);
            AdvanceBookView(dt);
            AdvanceKitView(dt);
            slotCd[1] -= dt;
            slotCd[2] -= dt;
        }
    }
}
