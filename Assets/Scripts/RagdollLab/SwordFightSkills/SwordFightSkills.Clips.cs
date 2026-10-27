using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R190 (사용자 10-28: "B. Mixamo 클립 해주고 리깅 작업 클립에 맞게 바꿔줘", 1단계부터): a skill with a Mixamo clip in
    /// <see cref="SwordFightClipSet"/> plays it on the piece's body; one without keeps its code motion (Motion.cs).
    /// <list type="bullet">
    /// <item>The host only, like the code motion: the clip is read onto the eleven bodies (<see cref="SkillClipRig"/>)
    /// and laid over the code pose per region (the entry's chest, head, free arm, legs and turn shares), so where the code
    /// aims the head or the free hand at the skill's target that aim can be kept. Nothing new goes on the wire; other
    /// machines see the bodies they are sent.</item>
    /// <item>The clip's time follows the skill's stages through the entry's four marks (<see cref="SkillClipTiming"/>).</item>
    /// <item>The sword arm stays with the sword's pose (SwordFightPawn.SwordPose): the blade's line is what cuts, so the
    /// code keeps it. A skill whose sword has left the hand (the bishop's) lets the clip have that arm too.</item>
    /// <item>The same limits as the code motion: grounded legs no wider than the drawn feet allow, soles flat on the
    /// board, the turn and lean held to what the balance takes.</item>
    /// </list>
    /// </summary>
    public partial class SwordFightSkills
    {
        /// <summary>The clips on (off: the code motion only, to compare).</summary>
        public static bool ClipMotion = true;

        SkillClipRig clipRig;
        bool clipRigTried;
        readonly Quaternion[] clipLocal = new Quaternion[RagdollPawn.Count];
        SwordFightClipSet.Entry clipEntry;
        float clipW, clipAge, clipFade, clipTurn, clipLean;

        /// <summary>The clip entry for the skill as it stands now (any machine, from replicated state), with its stage
        /// (<see cref="SkillClipTiming"/>), the time into it and its length (0: not set), or null.</summary>
        SwordFightClipSet.Entry ClipNow(out int stage, out float time, out float length)
        {
            stage = 0; time = length = 0f;
            if (!ClipMotion || !BodyMotion || Pawn == null) return null;
            var set = SwordFightClipSet.Current;
            if (set == null) return null;
            if (EdgeStage != SfEdge.None)
            {
                if (EdgeStage == SfEdge.Windup) stage = SkillClipTiming.Windup;
                else if (EdgeStage == SfEdge.Active) stage = SkillClipTiming.Active;
                else stage = SkillClipTiming.Recovery;
                time = EdgeTime;
                length = EdgeStageLength(EdgeStage);
                return set.Find(Piece, true);
            }
            if (Stage == SfStage.Windup) stage = SkillClipTiming.Windup;
            else if (Stage == SfStage.Active) stage = SkillClipTiming.Active;
            else if (Stage == SfStage.Recovery) stage = SkillClipTiming.Recovery;
            else return null;
            time = StageTime;
            length = StageLength(Stage);
            return set.Find(Piece, false);
        }

        /// <summary>How long an F stage lasts when the parameters say (0: it runs until something happens).</summary>
        float StageLength(SfStage stage)
        {
            if (S == null) return 0f;
            switch (Piece)
            {
                case PieceKind.King:
                    if (stage == SfStage.Active) return S.kingCounterBeat + S.kingCounterTime;
                    if (stage == SfStage.Recovery) return KingCountered ? S.kingRecovery : S.kingWhiff;
                    return 0f;   // the guard waits for a blow
                case PieceKind.Queen:
                    return stage == SfStage.Windup ? S.queenWindup : stage == SfStage.Recovery ? S.queenRecovery : 0f;
                case PieceKind.Rook:
                    return stage == SfStage.Windup ? S.rookWindup : stage == SfStage.Recovery ? S.rookRecovery : 0f;
                case PieceKind.Bishop:
                    return stage == SfStage.Windup ? S.bishopWindup : stage == SfStage.Active ? 0.1f : stage == SfStage.Recovery ? S.bishopRecovery : 0f;
                case PieceKind.Knight:
                    if (stage == SfStage.Active) return KnightGather + S.knightRise + S.knightHover + S.knightDive;
                    return stage == SfStage.Recovery ? S.knightRecovery : 0f;
            }
            return 0f;
        }

        /// <summary>How long an edge stage lasts when the parameters say (0: not set).</summary>
        float EdgeStageLength(SfEdge stage)
        {
            var X = EdgeParams;
            if (X == null) return 0f;
            switch (Piece)
            {
                case PieceKind.King: return stage == SfEdge.Active ? X.kingCatch + X.kingHang + X.kingVault : 0f;
                case PieceKind.Queen: return stage == SfEdge.Windup ? X.queenLine : 0f;
                case PieceKind.Bishop:
                    return stage == SfEdge.Active ? X.bishopReach + X.bishopPull : stage == SfEdge.Recovery ? X.bishopRecovery : 0f;
                case PieceKind.Knight:
                    return stage == SfEdge.Windup ? X.knightCrouch : stage == SfEdge.Active ? X.knightFlip : stage == SfEdge.Recovery ? X.knightRecovery : 0f;
            }
            return 0f;
        }

        /// <summary>Any machine: the skill's clip moves the free arm now, so the drawn piece lets it (LeftArmPosed).</summary>
        bool ClipMovesFreeArm => ClipNow(out _, out _, out _) is SwordFightClipSet.Entry e && e.freeArm > 0f;

        /// <summary>The host's step: the clip read onto the bodies, its weight eased in and let go, the whole-body turn,
        /// lean and stance mixed toward it.</summary>
        void StepClip(float dt, bool active)
        {
            int stage = 0;
            float time = 0f, length = 0f;
            var e = NetPuppet || !active || Fighter == null || !Fighter.Alive || !Fighter.Authority ? null : ClipNow(out stage, out time, out length);
            if (e != null && Rig() is SkillClipRig rig)
            {
                if (e != clipEntry) clipAge = 0f;
                clipEntry = e;
                clipAge += dt;
                float at = SkillClipTiming.At(e.marks, stage, time, length, e.clip.length);
                if (rig.Sample(e.clip, at))
                {
                    rig.PoseBodies(clipLocal, out clipTurn, out clipLean);
                    clipW = SkillClipTiming.In(clipAge, e.blendIn, e.weight) * bodyRamp;
                    clipFade = 0.12f;
                }
            }
            else if (clipFade > 0f && active && clipEntry != null)
            {
                // A skill cut short or over lets the clip go over a moment, held where it was.
                clipFade -= dt;
                clipW = clipEntry.weight * Mathf.Clamp01(clipFade / 0.12f);
                clipAge = 0f;
            }
            else
            {
                clipW = clipFade = clipAge = 0f;
                clipEntry = null;
            }
            if (clipW <= 0f || clipEntry == null) return;
            float t = clipW * clipEntry.turn;
            Pawn.SkillTurn = Mathf.Lerp(Pawn.SkillTurn, Mathf.Clamp(clipTurn, -40f, 40f), t);
            Pawn.SkillLean = Mathf.Lerp(Pawn.SkillLean, Mathf.Clamp(clipLean, -12f, 15f), t);
            if (Pawn.Grounded)
            {
                float split = Mathf.Max(Quaternion.Angle(Quaternion.identity, clipLocal[(int)BodyId.ThighL]),
                    Quaternion.Angle(Quaternion.identity, clipLocal[(int)BodyId.ThighR]));
                Pawn.SkillStance = Mathf.Lerp(Pawn.SkillStance, StanceDrop(Mathf.Min(split, MaxGroundSplit)), clipW * clipEntry.legs);
            }
        }

        SkillClipRig Rig()
        {
            if (clipRig != null) return clipRig.Ready ? clipRig : null;
            if (clipRigTried) return null;
            clipRigTried = true;
            clipRig = new SkillClipRig(Pawn);
            if (!clipRig.Ready) Debug.LogWarning($"[SwordFightSkills] {name}: no skill clips, the code motion stays ({clipRig.Problem}).");
            return clipRig.Ready ? clipRig : null;
        }

        void DropClipRig()
        {
            clipRig?.Dispose();
            clipRig = null;
            clipRigTried = false;
        }

        /// <summary>The share of the clip in <paramref name="part"/> now (host).</summary>
        float ClipShare(int part)
        {
            if (clipW <= 0f || clipEntry == null) return 0f;
            switch ((BodyId)part)
            {
                case BodyId.Chest: return clipW * clipEntry.chest;
                case BodyId.Head: return clipW * clipEntry.head;
                case BodyId.ArmL:
                case BodyId.HandL: return CarriedByBishop ? 0f : clipW * clipEntry.freeArm;
                // The sword hand: the sword's own pose aims it while the skill holds the blade.
                case BodyId.ArmR:
                case BodyId.HandR: return Blade != null ? 0f : clipW * clipEntry.freeArm;
                case BodyId.ThighL:
                case BodyId.ThighR:
                case BodyId.FootL:
                case BodyId.FootR: return clipW * clipEntry.legs;
            }
            return 0f;
        }

        /// <summary>The clip's rotation for <paramref name="part"/> inside the body's limits.</summary>
        Quaternion ClipPart(int part)
        {
            var id = (BodyId)part;
            Quaternion q = clipLocal[part];
            switch (id)
            {
                case BodyId.Chest: return Quaternion.RotateTowards(Quaternion.identity, q, 32f);
                case BodyId.Head: return Quaternion.RotateTowards(Quaternion.identity, q, 42f);
                case BodyId.ThighL:
                case BodyId.ThighR:
                    return Quaternion.RotateTowards(Quaternion.identity, q, Pawn.Grounded ? MaxGroundSplit : 50f);
                case BodyId.FootL:
                case BodyId.FootR:
                {
                    // On the board the sole stays flat (whatever the clip's thigh did); in the air the clip's own.
                    if (!Pawn.Grounded) return Quaternion.RotateTowards(Quaternion.identity, q, 38f);
                    var thigh = id == BodyId.FootL ? BodyId.ThighL : BodyId.ThighR;
                    return Foot(thigh, ClipPart((int)thigh), 0f);
                }
            }
            return q;
        }

        /// <summary>The part's local rotation in its parent body for the skill now (host only): the code motion, with
        /// the skill's clip laid over it where there is one (R190). Null: the puppet's own.</summary>
        public Quaternion? SkillBodyPose(int part)
        {
            Quaternion? code = CodeBodyPose(part);
            if (NetPuppet || Pawn == null || Pawn.State != PawnState.Active) return code;
            // A piece the queen's edge cut went through is held as it stood, clip or none.
            if (struckLeft > 0f && part >= (int)BodyId.Chest && part <= (int)BodyId.HandR) return code;
            float w = ClipShare(part);
            if (w <= 0f) return code;
            Quaternion from = code ?? Pawn.puppet[part].localRotation;
            return Quaternion.Slerp(from, ClipPart(part), Mathf.Clamp01(w));
        }
    }
}
