using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The ultimates' "!!" flow (JY-skill-lab, 2026-11-22; the user: "궁극기 자체가 체스의 탁월한 수(!!)라는 컨셉이니까 그걸 더
    /// 살려서 다시 이펙트를 디자인해줘"): one brilliant move played live on a giant chessboard laid over the course, read on every
    /// screen in five beats.
    ///
    ///   CLAIM       the key: the caster's cell lit like chess.com's from-square, the board unfolding round it, real light on
    ///               the caster; on its own screen time freezes and floats while the move is typed and stamped !!
    ///   THE MOVE    what it does drawn in thin teal neon (squares, arrows, files, rays), what it attacks in red
    ///   CAPTURE     each hit: red light from the sky, a red square and ring, the eval bar ticking, an impact frame
    ///   THE TACTIC  the piece's own chess idea once, slowed (each ultimate's own file)
    ///   REVIEW      the judgement: the from- and to-squares, one arrow for the whole ultimate, the badge filling with the
    ///               grade's chess.com colour; at !! the board ignites teal and the piece's own pattern rises as pillars
    ///
    /// This file only wires the parts together: the stage lights (PawnRushSkillFx.Stage.cs), the board (Brilliant.cs), the
    /// HUD (Hud.cs), the sounds (Sounds.cs), time and the impact frames (Impact.cs), the world and its grade (UltScreen.cs).
    /// </summary>
    public partial class PawnRushSkillFx
    {
        /// <summary>How loud a show's sounds are on this screen: the caster's full, its allies' 0.65, its enemies' 0.5, by the
        /// distance to <paramref name="at"/> on the others.</summary>
        float UltAudience(UltShow u, Vector3 at)
        {
            var me = Viewer;
            if (me != null && u.by != null && me == u.by) return 1f;
            float side = me == null || u.by == null ? 0.65f : Teams.AreEnemies(me.Team, u.by.Team) ? 0.5f : 0.65f;
            return side * UltHeard(at);
        }

        // ---------------------------------------------------------------- CLAIM: the key (UltCast)

        /// <summary>The key on every screen (the spec's §6): the stage lights on the caster, its trail and cage, the from-square
        /// and the board unfolding, the giant piece behind it (its own screen), the call and the eval bar, the key's sounds.
        /// Its slow motion and impact frames are UltCast's (Impact.cs); the curtain and the aurora burst are the previz's.</summary>
        void UltBrilliantCast(UltShow u)
        {
            if (u.by == null) return;
            var me = Viewer;
            bool caster = me != null && me == u.by, enemy = me != null && !caster && Teams.AreEnemies(me.Team, u.by.Team);
            UltKeySpot(u);
            UltRim(u);
            UltTrail(u);
            UltCage(u);
            FromSquare(u);
            BoardUnfold(u);
            if (caster) UltGiantAvatar(u, u.by.Piece == PieceKind.Queen ? 12f : 9f);
            UltHudCast(u);
            float heard = UltHeard(u.castAt);
            if (caster)
            {
                UltClip(UltClipId.ClockPress, 0.9f);
                UltPlay(sfxBoom, 1f, 0f, 0.8f);
                UltClip(UltClipId.SubDrop, 1f);
            }
            else if (!enemy)
            {
                UltClip(UltClipId.ClockPress, 0.6f * heard);
                UltPlay(sfxBoom, 0.7f * heard);
            }
            else
            {
                UltPlay(sfxBoom, 0.6f * heard);
                UltClip(UltClipId.Check, 0.5f * heard);
            }
        }

        // ---------------------------------------------------------------- CAPTURE (every UltStrike)

        /// <summary>A capture on every screen (§8): the struck cell red like chess.com's, a capture ring, red light from the
        /// sky and a hit light (merged in a frame), a few red sparks, the eval bar's tick, the capture's sound (a king struck:
        /// check). Its impact frames are the burst's (Impact.cs); its notation is the ultimate's own.</summary>
        void UltBrilliantStrike(UltShow u, SkillFxEvent e)
        {
            var t = e.target;
            if (t == null) return;
            Vector3 at = t.Hips.position;
            float floor = BrdGroundBelow(at, 6f);
            SquareMark(u, Cell(at), Blood, 0.9f, 0.30f, Blood, 2.4f, null, 0.6f, null, float.IsNaN(floor) ? at.y - t.standHeight : floor);
            CaptureRing(t, 0.6f);
            SkyBeam(at, Blood);
            UltHitLight(HeadOf(t) + Vector3.up * 1.5f);
            SparkBurst(ChestOf(t), 20, Blood, Vector3.up, 70f, 3f, 9f, 0.5f, 0.045f);
            UltEvalAdd(u, 1.3f);
            float lvl = UltAudience(u, at);
            UltClip(UltClipId.Capture, 0.85f * lvl);
            UltPlay(sfxChime[Mathf.Clamp(u.struck - 1, 0, sfxChime.Length - 1)], 0.6f * lvl);
            if (t.Piece == PieceKind.King)
            {
                CheckGlow(t);
                UltClip(UltClipId.Check, 0.8f * lvl);
            }
        }

        // ---------------------------------------------------------------- REVIEW: the judgement (UltHit)

        /// <summary>The judgement on every screen (§10): the from- and to-squares for 3 s, the one arrow of the whole ultimate
        /// (when both squares are within 80 m), the judgement spot, the window folding; the HUD's badge, grade word, card and
        /// eval; by the grade the board's ignition, the piece's own pattern rising as pillars, the floor's floating tiles and
        /// the chords. Its slow motion and frames are UltJudgeFrames' (Impact.cs).</summary>
        void UltBrilliantJudge(UltShow u, SkillFxEvent e)
        {
            Vector3 at = Ground(e.at);
            u.toCell = Cell(at);
            int g = u.grade;
            bool good = g <= RagdollPawn.GradeBest;
            ToSquare(u, u.toCell, good, 3f, g == RagdollPawn.GradeInaccuracy);
            if (u.fromCell != u.toCell)
                SquareMark(u, u.fromCell, good ? Teal : MissGreyTone, 1.15f, 0.42f, good ? TealPale : MissGreyTone, good ? 3.2f : 1.6f, UltCoord(u, u.fromCell), 3f);
            var cam = ViewCamera;
            Vector3 from = CellFlat(u.fromCell), to = CellFlat(u.toCell);
            from.y = BoardFloorNow(u, u.fromCell, BrdOf(u).refY);
            to.y = at.y;
            bool near = cam == null || (Vector3.Distance(cam.transform.position, from) <= 80f && Vector3.Distance(cam.transform.position, to) <= 80f);
            if (near && u.fromCell != u.toCell) UltArrow(() => from, () => to, good ? Teal : MissGreyTone, 1.2f, u.Knight, 2f, null, true);
            UltJudgeSpot(u, to, g);
            BoardWindowFold(u);
            UltHudJudge(u);
            float lvl = UltAudience(u, at);
            var piece = u.by != null ? u.by.Piece : PieceKind.Pawn;
            switch (g)
            {
                case RagdollPawn.GradeBrilliant:
                    BoardStamp(at, 6, 0.05f, 2.4f, Teal, 3f, 1.15f, true);   // the board's ignition, 13×13
                    PatternPillars(u, at, piece, 14f, 1f);
                    UltShatter(at, 11f, true);
                    UltJudgeBeams(u);
                    if (cam != null && Vector3.Distance(cam.transform.position, at) >= 12f) LensStreak(at + Vector3.up * 1.5f);
                    UltPlay(sfxGrade[0], 0.9f * lvl);
                    UltClip(UltClipId.Shimmer, 0.5f * lvl);
                    UltPlay(sfxImpactLow, 1f * lvl);
                    break;
                case RagdollPawn.GradeGreat:
                    BoardStamp(at, 4, 0.05f, 2.4f, Teal, 3f * 0.7f, 1.15f * 0.7f, true);   // 9×9 at 70 %
                    PatternPillars(u, at, piece, 8f, 0.7f);
                    UltShatter(at, 7f, true);
                    UltPlay(sfxGrade[1], 0.85f * lvl);
                    break;
                case RagdollPawn.GradeBest:
                    BoardStamp(at, 2, 0.05f, 2.4f, Teal, 3f * 0.5f, 1.15f * 0.5f, false);   // 5×5 at 50 %
                    UltPlay(sfxGrade[2], 0.8f * lvl);
                    break;
                case RagdollPawn.GradeInaccuracy:
                    UltPlay(sfxGrade[3], 0.6f * lvl);
                    break;
                default:
                    UltPlay(sfxGrade[4], 0.8f * lvl);
                    UltClip(UltClipId.Wah, 0.4f * lvl);
                    break;
            }
            if (good)
            {
                float d = cam != null ? Vector3.Distance(cam.transform.position, at) : 20f;
                Shake(Viewer != null && Viewer == u.by ? 0.22f : 0.16f * Mathf.Clamp01(1f - d / 35f), 0.5f);
            }
        }

        /// <summary>At a !!, red light from the sky on every enemy it struck, one after another (0.04 s apart).</summary>
        void UltJudgeBeams(UltShow u)
        {
            int i = 0;
            foreach (var p in u.struckSet)
            {
                if (p == null || i >= 8) continue;
                var target = p;
                float wait = 0.04f * i++;
                anims.Add(new Ongoing((age, dt) =>
                {
                    if (age < wait) return true;
                    if (target != null) SkyBeam(target.Hips.position, Blood);
                    return false;
                }) { tick = UltTick.Real });
            }
        }

        // ---------------------------------------------------------------- every frame

        /// <summary>A show each frame (UltStep): the board's window under the caster while it drives (not for the queen in the
        /// air: she has her rays).</summary>
        void UltBrilliantStep(UltShow u, Camera cam)
        {
            if (!u.Driven || u.by == null) return;
            bool flying = u.skill == PawnRushSkillIds.QueenDescent && u.Playing && ((u.by.PlanNote >> RagdollPawn.QdStageShift) & 3) != RagdollPawn.QdDown;
            BoardWindowStep(u, cam, u.Playing && !flying);
        }
    }
}
