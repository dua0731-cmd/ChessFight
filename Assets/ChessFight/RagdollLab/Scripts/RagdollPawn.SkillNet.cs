using System;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The piece skills over the network (R111, Docs/Network/SKILLS_ONLINE.md). The host runs every skill; each
    /// snapshot it sends what the others need to draw them (stage, timers, aim, warnings) as one SkillWireState per
    /// busy piece. A client writes that into its puppets' own skill fields, so the effects, which read those fields,
    /// draw the same warnings and the same cooldown on every screen without knowing about the network.
    ///
    /// The state is written the moment it arrives, not 70 ms later with the bodies (D-S4): a warning shows as early as
    /// it can, so there is that much more time to see it and step away. The local player's own aim is drawn from this
    /// PC's camera at once and a press starts its warning at once (D-S3, <see cref="ViewAim"/>, <see cref="PredictPress"/>);
    /// the host's state takes over when it arrives. The bodies always follow the host.
    ///
    /// The wire slots mean different things per mode (Pawn Rush / Queen of the Hill); the maps are below and in
    /// SKILLS_ONLINE §8.
    /// </summary>
    public partial class RagdollPawn
    {
        // Pawn Rush flags
        const byte NetLocked = 1, NetRookAir = 2, NetDashing = 4, NetValid = 8, NetHoming = 16, NetTapped = 32, NetTurned = 64;
        // Queen of the Hill flags (NetLocked, NetDashing and NetValid as above)
        const byte NetFlying = 2, NetBlocked = 16, NetShell = 32;
        // Flags2
        const byte NetQueenHill = 128, NetAsked = 1;

        // ---------------------------------------------------------------- the visual hit stop (D-S2)

        float visualHoldUntil = -1f;

        /// <summary>The skin is held where it is (a per-screen hit stop): RagdollVisualSync leaves it alone. Online a
        /// hit stop must not stop the game (the host's physics and everyone's inputs), so only these two pieces' picture
        /// stops, on each screen by itself; the bodies go on and the skin catches up when it lets go.</summary>
        public bool VisualHeld => Time.unscaledTime < visualHoldUntil;

        public void HoldVisual(float seconds)
        {
            if (seconds > 0f) visualHoldUntil = Mathf.Max(visualHoldUntil, Time.unscaledTime + seconds);
        }

        // ---------------------------------------------------------------- host: what the others need

        /// <summary>Nothing about this piece's skill is worth sending (an idle piece is left out of the packet, D-S5).</summary>
        public bool SkillNetIdle =>
            PawnRushSkills == null
            || (skillStage == SkillStage.None && skillCooldownLeft <= 0f && hasteLeft <= 0f && getUpGuardLeft <= 0f
                && wardLeft <= 0f && castleAsker == null && !flying && qhCrouch <= 0.001f);

        /// <summary>This piece's skill as the others need to draw it. <paramref name="refOf"/> turns a piece into its
        /// roster place.</summary>
        public void CaptureSkillNet(ref SkillWireState s, Func<RagdollPawn, byte> refOf)
        {
            s.Stage = (byte)skillStage;
            s.StageTime = stageTime;
            s.Cooldown = Mathf.Max(0f, skillCooldownLeft);
            s.CooldownTotal = Mathf.Max(0f, cooldownTotal);
            s.Flags = 0;
            s.Flags2 = 0;
            s.Target = s.Other = SkillWire.None;
            Vector3 dir, p = Vector3.zero, q = Vector3.zero;
            if (QueenHillSkills != null)
            {
                s.Flags2 |= NetQueenHill;
                if (aimLocked) s.Flags |= NetLocked;
                if (flying) s.Flags |= NetFlying;
                if (dashing) s.Flags |= NetDashing;
                if (qhValid) s.Flags |= NetValid;
                if (slashBlocked) s.Flags |= NetBlocked;
                if (shellOut) s.Flags |= NetShell;
                if (castleAsker != null) s.Flags2 |= NetAsked;
                dir = piece == PieceKind.Queen ? slashDir : skillDir;
                p = qhPoint;
                q = piece == PieceKind.Knight ? leapSpot : piece == PieceKind.Bishop ? shellTo : Vector3.zero;
                var target = piece == PieceKind.Knight && aimLocked ? leapTarget : qhTarget;
                if (target != null) s.Target = refOf(target);
                if (castleAsker != null) s.Other = refOf(castleAsker);
                s.A = Mathf.Max(0f, wardLeft);
                s.B = castleAsker != null ? castleAskAge : 0f;
                s.C = (byte)Mathf.Clamp(Mathf.RoundToInt(qhCrouch * 255f), 0, 255);
                s.D = (byte)Mathf.Clamp(Mathf.RoundToInt(QhFlyProgress * 255f), 0, 255);
            }
            else
            {
                if (aimLocked) s.Flags |= NetLocked;
                if (rookAir) s.Flags |= NetRookAir;
                if (dashing) s.Flags |= NetDashing;
                if (bishopValid) s.Flags |= NetValid;
                if (knightHoming) s.Flags |= NetHoming;
                if (knightTapped) s.Flags |= NetTapped;
                if (knightTurned) s.Flags |= NetTurned;
                dir = piece == PieceKind.Bishop ? bishopYaw : skillDir;
                if (piece == PieceKind.Bishop)
                {
                    p = bishopPoint;
                    q = bishopFrom;
                }
                else if (piece == PieceKind.Knight) p = knightSpot;
                var marked = KnightMarked;
                if (piece == PieceKind.Knight && marked != null) s.Target = refOf(marked);
                s.A = Mathf.Max(0f, hasteLeft);
                s.B = Mathf.Max(0f, getUpGuardLeft);
                s.C = (byte)Mathf.Clamp(Mathf.RoundToInt(bishopFlight * 100f), 0, 255);
                s.D = (byte)Mathf.Clamp(pawnStep, 0, 255);
            }
            s.Dx = dir.x; s.Dy = dir.y; s.Dz = dir.z;
            s.Px = p.x; s.Py = p.y; s.Pz = p.z;
            s.Qx = q.x; s.Qy = q.y; s.Qz = q.z;
        }

        // ---------------------------------------------------------------- client: the puppet's skill fields

        // The host's state as it last arrived (the prediction draws over it for a moment).
        SkillStage netStage;
        bool netLocked;
        // The local player's own press, drawn before the host has answered (D-S3), with its own clock.
        float predictLeft, predictTime;
        SkillStage predictStage;
        bool predictLocked;

        /// <summary>The host's state for this piece arrived (a client's puppet). <paramref name="pawnOf"/> turns a roster
        /// place back into a piece.</summary>
        public void ApplySkillNet(in SkillWireState s, Func<byte, RagdollPawn> pawnOf)
        {
            netStage = (SkillStage)s.Stage;
            netLocked = (s.Flags & NetLocked) != 0;
            skillStage = netStage;
            aimLocked = netLocked;
            stageTime = s.StageTime;
            skillCooldownLeft = s.Cooldown;
            cooldownTotal = s.CooldownTotal;
            var dir = new Vector3(s.Dx, s.Dy, s.Dz);
            var p = new Vector3(s.Px, s.Py, s.Pz);
            var q = new Vector3(s.Qx, s.Qy, s.Qz);
            var target = s.Target != SkillWire.None ? pawnOf(s.Target) : null;
            if ((s.Flags2 & NetQueenHill) != 0)
            {
                flying = (s.Flags & NetFlying) != 0;
                dashing = (s.Flags & NetDashing) != 0;
                qhValid = (s.Flags & NetValid) != 0;
                slashBlocked = (s.Flags & NetBlocked) != 0;
                shellOut = (s.Flags & NetShell) != 0;
                if (piece == PieceKind.Queen) slashDir = FlatDir(dir, slashDir);
                else if (dir.sqrMagnitude > 0.5f) skillDir = dir;
                qhPoint = p;
                if (piece == PieceKind.Knight) leapSpot = q;
                if (piece == PieceKind.Bishop) shellTo = q;
                qhTarget = target;
                leapTarget = piece == PieceKind.Knight && netLocked ? target : null;
                castleAsker = (s.Flags2 & NetAsked) != 0 && s.Other != SkillWire.None ? pawnOf(s.Other) : null;
                castleAskAge = s.B;
                wardLeft = s.A;
                wardTotal = Mathf.Max(wardLeft, QueenHillSkills != null ? QueenHillSkills.wardTime : wardLeft);
                qhCrouch = s.C / 255f;
                // QhFlyProgress is flyT / flyTime: the share of the flight, as the host has it.
                flyTime = 1f;
                flyT = s.D / 255f;
            }
            else
            {
                rookAir = (s.Flags & NetRookAir) != 0;
                dashing = (s.Flags & NetDashing) != 0;
                bishopValid = (s.Flags & NetValid) != 0;
                knightHoming = (s.Flags & NetHoming) != 0;
                knightTapped = (s.Flags & NetTapped) != 0;
                knightTurned = (s.Flags & NetTurned) != 0;
                if (piece == PieceKind.Bishop)
                {
                    bishopYaw = FlatDir(dir, bishopYaw);
                    bishopPoint = p;
                    bishopFrom = q;
                    bishopFlight = s.C / 100f;
                }
                else if (dir.sqrMagnitude > 0.5f) skillDir = dir;
                if (piece == PieceKind.Knight) knightSpot = p;
                knightTarget = knightHoming ? target : null;
                knightCandidate = knightHoming ? null : target;
                hasteLeft = s.A;
                getUpGuardLeft = s.B;
                pawnStep = s.D;
            }
            // A piece doing its skill faces the skill's way (the effects lean on it: the queen's blade, a fallback aim).
            if (skillStage != SkillStage.None)
            {
                Vector3 face = QueenHillSkills != null && piece == PieceKind.Queen ? slashDir : piece == PieceKind.Bishop ? bishopYaw : skillDir;
                facing = FlatDir(face, facing);
            }
            ApplyPrediction();
        }

        /// <summary>This piece is not in the host's packet: its skill is idle (a client's puppet).</summary>
        public void ClearSkillNet()
        {
            if (netStage == SkillStage.None && skillStage == SkillStage.None && skillCooldownLeft <= 0f && !flying && castleAsker == null
                && wardLeft <= 0f && hasteLeft <= 0f && qhCrouch <= 0f) return;
            netStage = SkillStage.None;
            netLocked = false;
            skillStage = SkillStage.None;
            aimLocked = rookAir = dashing = knightHoming = knightTapped = knightTurned = false;
            flying = slashBlocked = shellOut = false;
            skillCooldownLeft = cooldownTotal = 0f;
            hasteLeft = getUpGuardLeft = wardLeft = 0f;
            qhCrouch = 0f;
            castleAsker = null;
            knightTarget = knightCandidate = qhTarget = leapTarget = null;
            stageTime = 0f;
            ApplyPrediction();
        }

        /// <summary>Between two of the host's packets a client runs the timers on itself, so a warning fills smoothly
        /// at the screen's rate rather than in 30 steps a second.</summary>
        public void AdvanceSkillView(float dt)
        {
            if (PawnRushSkills == null) return;
            if (skillStage != SkillStage.None) stageTime += dt;
            skillCooldownLeft -= dt;
            hasteLeft -= dt;
            getUpGuardLeft -= dt;
            wardLeft -= dt;
            if (castleAsker != null) castleAskAge += dt;
            if (predictLeft > 0f)
            {
                predictLeft -= dt;
                predictTime += dt;
                if (predictLeft <= 0f)
                {
                    // The host never took the press (on its own cooldown, knocked down meanwhile): back to what it says.
                    skillStage = netStage;
                    aimLocked = netLocked;
                }
            }
            // The queen's slash runs along the floor (R97): the way is the same arithmetic on every PC.
            if (QueenHillSkills != null && piece == PieceKind.Queen && skillStage != SkillStage.None) SlashLine(QueenHillSkills);
        }

        /// <summary>The local player's own piece on a client: while it aims, the aim follows this PC's camera at once
        /// instead of a round trip later (D-S3). What the host decides (the lock, the target) still comes from it.</summary>
        public void ViewAim(Vector3 aim)
        {
            if (PawnRushSkills == null || skillStage != SkillStage.Windup || aimLocked) return;
            input.aim = aim;
            if (QueenHillSkills != null)
            {
                var s = QueenHillSkills;
                switch (piece)
                {
                    case PieceKind.Queen:
                        slashDir = AimFlat();
                        break;
                    case PieceKind.Bishop:
                        BishopAimQh(s);
                        break;
                    case PieceKind.Knight:
                        // A head the host's aim has caught stays (the host judges pieces); a floor spot is aimed here.
                        if (qhTarget == null) AimFloor(s.knightRange, s.knightMaxRise, out qhPoint, out qhValid, out qhWhy);
                        break;
                }
                return;
            }
            if (piece == PieceKind.Rook) skillDir = RookAim(PawnRushSkills);
            else if (piece == PieceKind.Bishop) BishopAim(PawnRushSkills);
        }

        /// <summary>
        /// The local player pressed something on a client (D-S3): draw what the host will most likely do with it at once,
        /// for at most <paramref name="hold"/> seconds (about a round trip) until the host's own state says the same.
        /// <paramref name="key"/> 0 = the skill key, 1 = the left click (go), 2 = the right click (call it off).
        /// </summary>
        public void PredictPress(int key, float hold)
        {
            if (PawnRushSkills == null || State != PawnState.Active) return;
            bool qh = QueenHillSkills != null;
            bool aims = qh ? QueenHillAims : piece == PieceKind.Rook || piece == PieceKind.Bishop;
            bool aiming = skillStage == SkillStage.Windup && !aimLocked && aims;
            switch (key)
            {
                case 0:
                    if (skillStage == SkillStage.None && skillCooldownLeft <= 0.05f)
                    {
                        // The pieces whose skill opens with a warning or an aim; a dash or a leap is the body's to show.
                        bool windup = qh || piece == PieceKind.Queen || piece == PieceKind.Rook || piece == PieceKind.Bishop
                                      || (piece == PieceKind.Knight && Grounded);
                        if (windup) Predict(SkillStage.Windup, false, hold, true);
                    }
                    else if (aiming && !(qh && piece == PieceKind.Bishop)) Predict(SkillStage.None, false, hold, false);
                    break;
                case 1:
                    if (!aiming || (qh && piece == PieceKind.Bishop)) break;
                    bool valid = qh ? qhValid : piece != PieceKind.Bishop || bishopValid;
                    if (!valid) break;
                    // The Pawn Rush bishop throws at once (its X in flight); the others lock their aim and wind up.
                    if (!qh && piece == PieceKind.Bishop) Predict(SkillStage.Active, false, hold, true);
                    else Predict(SkillStage.Windup, true, hold, true);
                    break;
                case 2:
                    if (aiming && !(qh && piece == PieceKind.Bishop)) Predict(SkillStage.None, false, hold, false);
                    break;
            }
        }

        void Predict(SkillStage stage, bool locked, float hold, bool restartClock)
        {
            predictStage = stage;
            predictLocked = locked;
            predictLeft = Mathf.Max(0.1f, hold);
            predictTime = restartClock ? 0f : stageTime;
            skillStage = stage;
            aimLocked = locked;
            stageTime = predictTime;
        }

        /// <summary>The host's state has caught up with the guess: the same step, or one past it (a quick skill can be a
        /// step further by the time its state arrives).</summary>
        bool PredictionConfirmed()
        {
            if (predictStage == SkillStage.None) return netStage == SkillStage.None;
            if (netStage == SkillStage.None) return false;
            if ((int)netStage > (int)predictStage) return true;
            return netStage == predictStage && (!predictLocked || netLocked);
        }

        void ApplyPrediction()
        {
            if (predictLeft <= 0f) return;
            if (PredictionConfirmed())
            {
                predictLeft = 0f;   // the host did it: its state from now on
                return;
            }
            // Until then the guess holds, on its own clock (the host's state still says the step before).
            skillStage = predictStage;
            aimLocked = predictLocked;
            stageTime = predictTime;
        }

        /// <summary>A client becomes the host mid-match (D-S6): what it was drawing becomes what it runs. Cooldowns, the
        /// pawn's haste, the guard after getting up and the king's guard carry on; a skill caught half way (a dash, a leap,
        /// an aim) is called off and its cooldown starts, as if it had ended there.</summary>
        public void AdoptSkillNet()
        {
            predictLeft = 0f;
            if (PawnRushSkills == null) return;
            bool midway = skillStage != SkillStage.None;
            float cooldown = skillCooldownLeft, total = cooldownTotal, haste = hasteLeft, guard = getUpGuardLeft, ward = wardLeft;
            var wardFrom = wardBy;
            ClearSkills();
            flying = false;
            castleAsker = null;
            skillCooldownLeft = Mathf.Max(0f, cooldown);
            cooldownTotal = total;
            if (midway && skillCooldownLeft <= 0f)
                StartCooldown(QueenHillSkills != null ? QueenHillCooldown(QueenHillSkills) : PieceCooldown(PawnRushSkills));
            hasteLeft = Mathf.Max(0f, haste);
            getUpGuardLeft = Mathf.Max(0f, guard);
            wardLeft = Mathf.Max(0f, ward);
            wardBy = wardFrom;
        }

        /// <summary>The way a puppet faces, from its hips (the host's facing is not sent; the effects lean on it). A piece
        /// lying down keeps the way it last faced.</summary>
        void FaceFromPose(RagdollPose pose)
        {
            if (pose.state != 0 || skillStage != SkillStage.None) return;
            Vector3 f = pose.rotations[0] * Vector3.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 1e-4f) facing = f.normalized;
        }
    }
}
