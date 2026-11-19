using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The marks of the Pawn Rush 2.0 skill plan prototypes (plan 1-나, RagdollPawn.SkillPlans.cs; JY-skill-lab, 2026-11-19):
    /// plain lines and rings, no effects - the square a queen's check names, the knight's two fork spots, the bishop's
    /// line and the rope of a pin, the rook's castling line, the ring under the piece a pawn may trip. Drawn every frame
    /// from each piece's public plan state, which a client gets from the host, so every PC shows the same. In the
    /// middle of the screen it tells the piece played here what is being done to it: a castling offered, a check, a pin.
    /// Added by the skill test bed and by the course's skills.
    /// </summary>
    [DefaultExecutionOrder(250)]
    public class PawnRushPlanMarks : MonoBehaviour
    {
        /// <summary>The pawn played at this PC.</summary>
        public Func<RagdollPawn> Viewer;

        static readonly Color Red = new Color(1f, 0.22f, 0.18f);
        static readonly Color Violet = new Color(0.74f, 0.52f, 1f);
        static readonly Color White = Color.white;

        readonly List<LineRenderer> pool = new List<LineRenderer>();
        int used;
        GUIStyle big, small;

        LineRenderer Next()
        {
            if (used == pool.Count) pool.Add(SkillMarks.Line(transform, $"Plan mark {pool.Count}"));
            return pool[used++];
        }

        static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);

        static Vector3 Chest(RagdollPawn p) => p.Hips.position + Vector3.up * 0.15f;

        void LateUpdate()
        {
            used = 0;
            var me = Viewer != null ? Viewer() : null;
            foreach (var pawn in RagdollPawn.All)
            {
                if (pawn == null || !pawn.PlanB) continue;
                DrawPin(pawn);
                switch (pawn.Piece)
                {
                    case PieceKind.Pawn: DrawPass(pawn, me); break;
                    case PieceKind.Knight: DrawFork(pawn); break;
                    case PieceKind.Bishop: DrawPinLine(pawn); break;
                    case PieceKind.Rook: DrawCastle(pawn); break;
                    case PieceKind.Queen: DrawCheck(pawn); break;
                }
            }
            for (int i = used; i < pool.Count; i++) SkillMarks.Hide(pool[i]);
        }

        /// <summary>A pin's rope, from the piece's waist to what it is tied to (drawn once for a tied pair), blinking as it
        /// runs out.</summary>
        void DrawPin(RagdollPawn pawn)
        {
            if (!pawn.Pinned) return;
            var mate = pawn.PinMate;
            if (mate != null && mate.Pinned && mate.PinMate == pawn && pawn.GetInstanceID() > mate.GetInstanceID()) return;
            float a = pawn.PinLeft < 1f ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 18f)) : 1f;
            SkillMarks.Segment(Next(), pawn.LassoBelly, pawn.PinAnchor, Fade(Violet, a), 0.06f, false);
            SkillMarks.Circle(Next(), pawn.FeetPoint, 0.45f, Fade(Violet, a * 0.8f), 0.05f, 24);
            if (mate != null) SkillMarks.Circle(Next(), mate.FeetPoint, 0.45f, Fade(Violet, a * 0.8f), 0.05f, 24);
        }

        /// <summary>The pawn played here: a ring under the enemy that has just brushed past it (its key trips that one).</summary>
        void DrawPass(RagdollPawn pawn, RagdollPawn me)
        {
            if (pawn != me) return;
            var target = pawn.PassTarget;
            if (target == null) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 22f);
            SkillMarks.Circle(Next(), target.FeetPoint, 0.5f + 0.1f * pulse, Fade(SkillMarks.Orange, 0.6f + 0.4f * pulse), 0.09f, 24);
        }

        /// <summary>The knight's fork: where it comes down, and the two spots its hind legs kick.</summary>
        void DrawFork(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            if (phase != PlanPhase.Windup && phase != PlanPhase.Active) return;
            var s = pawn.PawnRushSkills;
            Color spot = Fade(SkillMarks.Target, phase == PlanPhase.Active ? 1f : 0.75f);
            SkillMarks.Circle(Next(), pawn.PlanPoint, 0.35f, Fade(White, 0.8f), 0.05f, 20);
            for (int side = 0; side < 2; side++)
            {
                Vector3 at = pawn.ForkSpot(side);
                SkillMarks.Circle(Next(), at, s.forkRadius, spot, 0.07f, 32);
                SkillMarks.Segment(Next(), pawn.PlanPoint + Vector3.up * 0.05f, at + Vector3.up * 0.05f, Fade(SkillMarks.Target, 0.5f), 0.04f);
            }
        }

        /// <summary>The bishop's pin: aiming, the line as far as it reaches (to the enemy it would take first); shot, the
        /// line it took for a moment.</summary>
        void DrawPinLine(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            var s = pawn.PawnRushSkills;
            Vector3 hands = pawn.LassoHands;
            if (phase == PlanPhase.Aim)
            {
                Vector3 end = hands + pawn.PlanDir * s.pinRange;
                SkillMarks.Segment(Next(), hands, end, Fade(Violet, 0.45f), 0.04f, false);
                var target = pawn.PlanTarget;
                if (target != null) SkillMarks.Circle(Next(), target.FeetPoint, 0.5f, Fade(Violet, 0.9f), 0.06f, 24);
            }
            else if (phase == PlanPhase.Recovery)
            {
                float a = 1f - Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, s.pinRecovery));
                SkillMarks.Segment(Next(), hands, pawn.PlanPoint, Fade(White, 0.4f + 0.6f * a), 0.09f, false);
            }
        }

        /// <summary>The rook's castling: a line to the piece it would swap with or is about to, and a ring under each.</summary>
        void DrawCastle(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            if (phase != PlanPhase.Aim && phase != PlanPhase.Windup) return;
            var mate = pawn.PlanTarget;
            var s = pawn.PawnRushSkills;
            if (mate == null)
            {
                if (phase == PlanPhase.Aim) SkillMarks.Circle(Next(), pawn.FeetPoint, 0.5f, Fade(White, 0.35f), 0.04f, 24);
                return;
            }
            bool ally = mate.Team == pawn.Team && pawn.Team != Teams.None;
            Color c = ally ? SkillMarks.Cyan : SkillMarks.Orange;
            float a = phase == PlanPhase.Aim ? 0.5f : 1f;
            SkillMarks.Segment(Next(), Chest(pawn), Chest(mate), Fade(c, a), phase == PlanPhase.Aim ? 0.04f : 0.07f, false);
            SkillMarks.Circle(Next(), pawn.FeetPoint, 0.5f, Fade(c, a), 0.06f, 24);
            SkillMarks.Circle(Next(), mate.FeetPoint, 0.5f, Fade(c, a), 0.06f, 24);
            if (phase != PlanPhase.Windup) return;
            // The time left: the teammate's to answer in, or the enemy's warning, as a ring closing on the other piece.
            float total = ally ? s.castleWindow : s.castleEnemyWindup;
            float left = 1f - Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, total));
            SkillMarks.Circle(Next(), mate.FeetPoint, 0.5f + 0.9f * left, Fade(c, 0.9f), 0.04f, 28);
        }

        /// <summary>The queen's check: aiming, a ring under the enemy she would name; named, its square in red with a ring
        /// closing on it as the time runs out, and the line from her (a piece standing on that line takes the blow).</summary>
        void DrawCheck(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            var s = pawn.PawnRushSkills;
            var target = pawn.PlanTarget;
            if (phase == PlanPhase.Aim)
            {
                if (target == null) return;
                float blink = 0.55f + 0.45f * Mathf.Sin(Time.time * 14f);
                SkillMarks.Circle(Next(), target.FeetPoint, 0.55f, Fade(Red, blink), 0.06f, 24);
                SkillMarks.Segment(Next(), Chest(pawn), Chest(target), Fade(Red, 0.3f), 0.03f, false);
                return;
            }
            if (phase != PlanPhase.Active) return;
            float left = 1f - Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, s.checkSeconds));
            Vector3 square = pawn.PlanPoint;
            SkillMarks.Circle(Next(), square, s.checkSquare, Red, 0.12f, 40);
            SkillMarks.Circle(Next(), square, Mathf.Max(0.1f, s.checkSquare * left), Fade(Red, 0.85f), 0.06f, 32);
            SkillMarks.Circle(Next(), square, s.checkSquare * 0.5f, Fade(Red, 0.35f), 0.05f, 28);
            if (target != null) SkillMarks.Segment(Next(), Chest(pawn), Chest(target), Fade(Red, 0.6f), 0.05f, false);
        }

        // ---------------------------------------------------------------- what is being done to the piece played here

        void OnGUI()
        {
            var me = Viewer != null ? Viewer() : null;
            if (me == null || !me.PlanB) return;
            string line = null, sub = null;
            float share = 0f;
            var rook = me.CastleOfferingMe();
            var queen = me.CheckedBy();
            if (rook != null && rook.PawnRushSkills != null)
            {
                float window = Mathf.Max(0.1f, rook.PawnRushSkills.castleWindow);
                line = "캐슬링!  스킬 키로 받기";
                sub = $"{rook.DisplayName}(룩)와 자리 바꾸기 · {rook.CastleOfferLeft:0.0}초";
                share = Mathf.Clamp01(rook.CastleOfferLeft / window);
            }
            else if (queen != null && queen.PawnRushSkills != null)
            {
                float window = Mathf.Max(0.1f, queen.PawnRushSkills.checkSeconds);
                line = "체크!  빨간 칸 밖으로";
                sub = $"{queen.DisplayName}(퀸) · {queen.CheckLeft:0.0}초";
                share = Mathf.Clamp01(queen.CheckLeft / window);
            }
            else if (me.Pinned && me.PawnRushSkills != null)
            {
                var mate = me.PinMate;
                line = "핀!  묶였어요";
                sub = (mate != null ? $"{mate.DisplayName}와 한 줄 · " : "벽에 묶임 · ") + $"{me.PinLeft:0.0}초";
                share = Mathf.Clamp01(me.PinLeft / Mathf.Max(0.1f, me.PawnRushSkills.pinSeconds));
            }
            if (line == null) return;
            if (big == null)
            {
                var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI" }, 32);
                big = new GUIStyle(GUI.skin.label) { font = font, fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                big.normal.textColor = new Color(0.98f, 0.96f, 0.9f);
                small = new GUIStyle(big) { fontSize = 17, fontStyle = FontStyle.Normal };
                small.normal.textColor = new Color(0.86f, 0.88f, 0.92f);
            }
            var white = Texture2D.whiteTexture;
            GUI.depth = -10;
            float w = 380f, h = 96f, x = (Screen.width - w) * 0.5f, y = Screen.height * 0.62f;
            GUI.color = new Color(0.05f, 0.08f, 0.16f, 0.78f);
            GUI.DrawTexture(new Rect(x, y, w, h), white);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 8f, w, 40f), line, big);
            GUI.Label(new Rect(x, y + 46f, w, 24f), sub, small);
            GUI.color = new Color(0.35f, 0.38f, 0.45f, 1f);
            GUI.DrawTexture(new Rect(x + 20f, y + h - 18f, w - 40f, 6f), white);
            GUI.color = new Color(0.86f, 0.9f, 0.96f, 1f);
            GUI.DrawTexture(new Rect(x + 20f, y + h - 18f, (w - 40f) * share, 6f), white);
            GUI.color = Color.white;
        }
    }
}
