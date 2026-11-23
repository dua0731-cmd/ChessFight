using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The set 3판's lines and rings (RagdollPawn.SetThree.cs; JY-skill-lab 11-23), drawn every frame from each piece's
    /// public plan state, the same on every PC: the bolas' rope and the shine over the head it pulls in, the knight's
    /// horseshoe over the enemy it will stomp, the stone's way and where the holy water will break, the rook's path ahead
    /// and the ground splitting, the queen's octagram and her check. The solid things (the swords, the stone, the tower,
    /// the rolling column, the puddle) are the effects' (PawnRushSkillFx.SetThree.cs).
    /// </summary>
    public partial class PawnRushPlanMarks
    {
        static readonly Color Teal = new Color(0.2f, 0.95f, 0.85f);
        static readonly Color Water = new Color(0.35f, 0.7f, 1f);
        readonly Vector3[] arc = new Vector3[13];
        readonly Vector3[] zig = new Vector3[24];

        void DrawSetThree(RagdollPawn pawn, RagdollPawn me)
        {
            var s = pawn.PawnRushSkills;
            if (s == null) return;
            var phase = pawn.PlanPhaseNow;
            var target = pawn.PlanTarget;
            switch (pawn.PlanSkill)
            {
                case PawnRushSkillIds.Bolas:
                {
                    if (phase != PlanPhase.Active) return;
                    Vector3 hands = pawn.LassoHands;
                    if (pawn.BolasFlying)
                    {
                        SkillMarks.Segment(Next(), hands, pawn.PlanPoint, Fade(Gold, 0.8f), 0.025f, false);
                        SkillMarks.Circle(Next(), pawn.PlanPoint, 0.18f, Fade(Gold, 0.9f), 0.03f, 10);
                        return;
                    }
                    if (target == null) return;
                    SkillMarks.Segment(Next(), hands, target.FeetPoint + Vector3.up * 0.12f, Fade(Gold, 0.9f), 0.03f, false);
                    SkillMarks.Circle(Next(), target.FeetPoint + Vector3.up * 0.1f, 0.2f, Fade(Gold, 0.9f), 0.03f, 12);
                    if (!pawn.BolasChopOpen) return;
                    // The shine over its head: the chop's half second (the pawn played here sees it bigger).
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 30f);
                    Vector3 head = target.Hips.position + Vector3.up * 0.45f;
                    float r = (pawn == me ? 0.32f : 0.22f) + 0.06f * pulse;
                    SkillMarks.Circle(Next(), head, r, Fade(White, 0.7f + 0.3f * pulse), 0.06f, 20);
                    SkillMarks.Circle(Next(), head, r * 0.6f, Fade(Gold, 0.9f), 0.04f, 16);
                    return;
                }
                case PawnRushSkillIds.HighStomp:
                {
                    if (phase != PlanPhase.Active || target == null) return;
                    // The horseshoe over the enemy it will come down on (that enemy sees it too).
                    Vector3 over = target.Hips.position + Vector3.up * 0.75f;
                    Vector3 face = Flat(pawn.FeetPoint - target.FeetPoint);
                    Horseshoe(over, face.sqrMagnitude > 1e-4f ? face.normalized : Vector3.forward, 0.3f, Fade(SkillMarks.Orange, 0.95f), 0.07f);
                    SkillMarks.Circle(Next(), target.FeetPoint, 0.55f, Fade(SkillMarks.Orange, 0.7f), 0.05f, 24);
                    bool diving = (pawn.PlanNote & RagdollPawn.HopDiveBit) != 0;
                    SkillMarks.Segment(Next(), pawn.Hips.position, over, Fade(diving ? Red : SkillMarks.Orange, diving ? 0.9f : 0.3f), diving ? 0.06f : 0.02f, false);
                    return;
                }
                case PawnRushSkillIds.StoneThrow:
                {
                    if (phase != PlanPhase.Windup) return;
                    // Hovering: the way the stone will go (slow: everyone sees it coming).
                    Vector3 from = Chest(pawn), dir = Flat(pawn.PlanDir).normalized;
                    if (dir.sqrMagnitude < 0.5f) return;
                    float charge = Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, s.stoneHover));
                    SkillMarks.Segment(Next(), from, from + dir * s.stoneRange, Fade(Violet, 0.25f + 0.5f * charge), Mathf.Lerp(0.03f, s.stoneSize * 0.25f, charge), false);
                    return;
                }
                case PawnRushSkillIds.HolyWater:
                {
                    if (phase != PlanPhase.Active) return;
                    SkillMarks.Circle(Next(), pawn.PlanPoint + Vector3.up * 0.04f, s.waterSize * 0.5f, Fade(Water, 0.8f), 0.05f, 32);
                    return;
                }
                case PawnRushSkillIds.RollShort:
                {
                    if (!pawn.RollingShort) return;
                    // The path ahead: get out of its line.
                    Vector3 dir = Flat(pawn.PlanDir).normalized, feet = pawn.FeetPoint + Vector3.up * 0.05f;
                    float left = Mathf.Max(0f, s.rollSeconds - pawn.PlanTime) * s.rollSpeed1;
                    Vector3 side = Vector3.Cross(Vector3.up, dir) * (s.rollLength * 0.5f);
                    SkillMarks.Segment(Next(), feet + side, feet + side + dir * left, Fade(SkillMarks.Orange, 0.45f), 0.05f, false);
                    SkillMarks.Segment(Next(), feet - side, feet - side + dir * left, Fade(SkillMarks.Orange, 0.45f), 0.05f, false);
                    return;
                }
                case PawnRushSkillIds.TowerTrap:
                {
                    if (phase != PlanPhase.Active && phase != PlanPhase.Recovery) return;
                    // The ground splitting from the rook's feet to its tip, a jagged line on the floor.
                    float a = phase == PlanPhase.Active ? 1f : 1f - Mathf.Clamp01(pawn.PlanTime / 0.3f);
                    if (a <= 0.01f) return;
                    Crack(pawn.FeetPoint, pawn.PlanPoint, s.crackWidth, Fade(new Color(0.85f, 0.6f, 0.35f), a), 0.07f);
                    return;
                }
                case PawnRushSkillIds.QueenStar:
                {
                    if (phase == PlanPhase.Windup)
                    {
                        float charge = Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, s.starFuse));
                        Octagram(pawn, s.starRadius, 0, Fade(Color.Lerp(White, Teal, charge), 0.35f + 0.5f * charge), 0.04f + 0.03f * charge);
                    }
                    else if (phase == PlanPhase.Recovery)
                    {
                        float a = 1f - Mathf.Clamp01(pawn.PlanTime / 0.4f);
                        if (a > 0.01f) Octagram(pawn, s.starRadius, pawn.PlanNote, Fade(Gold, a), 0.09f);
                    }
                    return;
                }
                case PawnRushSkillIds.CheckChain:
                {
                    if ((phase != PlanPhase.Windup && phase != PlanPhase.Active) || target == null) return;
                    float blink = 0.55f + 0.45f * Mathf.Sin(Time.time * 26f);
                    SkillMarks.Circle(Next(), target.FeetPoint, 0.6f, Fade(Red, phase == PlanPhase.Windup ? blink : 1f), 0.08f, 24);
                    SkillMarks.Segment(Next(), Chest(pawn), Chest(target), Fade(Red, phase == PlanPhase.Windup ? 0.5f : 0.9f), phase == PlanPhase.Windup ? 0.03f : 0.07f, false);
                    return;
                }
            }
        }

        /// <summary>A horseshoe (an open ring, its gap toward <paramref name="open"/>) standing upright at <paramref name="at"/>.</summary>
        void Horseshoe(Vector3 at, Vector3 open, float r, Color c, float width)
        {
            Vector3 side = Vector3.Cross(Vector3.up, open).normalized;
            for (int i = 0; i < arc.Length; i++)
            {
                // From one heel round the top to the other (the gap at the bottom).
                float a = Mathf.Lerp(-140f, 140f, i / (float)(arc.Length - 1)) * Mathf.Deg2Rad;
                arc[i] = at + side * (Mathf.Sin(a) * r) + Vector3.up * (Mathf.Cos(a) * r);
            }
            SkillMarks.Polyline(Next(), arc, c, width, false);
        }

        /// <summary>The ground split: a jagged line from <paramref name="a"/> to <paramref name="b"/> on the floor.</summary>
        void Crack(Vector3 a, Vector3 b, float width, Color c, float lineWidth)
        {
            Vector3 d = Flat(b - a);
            float len = d.magnitude;
            if (len < 0.1f) return;
            Vector3 dir = d / len, side = Vector3.Cross(Vector3.up, dir);
            int n = Mathf.Clamp(Mathf.CeilToInt(len / 0.8f) + 1, 2, zig.Length);
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                // (A fixed zigzag from the line's own length: the same each frame.)
                float wobble = i == 0 || i == n - 1 ? 0f : Mathf.Sin(i * 2.7f + len * 0.3f) * width * 0.35f;
                Vector3 p = Vector3.Lerp(a, b, t) + side * wobble;
                p.y = Mathf.Lerp(a.y, b.y, t) + 0.05f;
                pts[i] = p;
            }
            SkillMarks.Polyline(Next(), pts, c, lineWidth, false);
        }

        /// <summary>The queen's octagram on the floor round her feet: eight thin blades, each folded down its middle
        /// (Docs/Skills/img/queen-octagram.svg); the blades in <paramref name="lit"/> (bits 0..7) drawn brighter and thicker.</summary>
        void Octagram(RagdollPawn queen, float radius, int lit, Color c, float width)
        {
            Vector3 o = queen.FeetPoint + Vector3.up * 0.06f;
            for (int k = 0; k < 8; k++)
            {
                Vector3 along = queen.StarBlade(k), side = Vector3.Cross(Vector3.up, along);
                bool on = (lit & (1 << k)) != 0;
                Color cc = lit == 0 || on ? c : Fade(c, c.a * 0.3f);
                float w = on ? width * 1.6f : width;
                Vector3 inner = o + along * (radius * 0.064f), tip = o + along * radius;
                Vector3 l = o + along * (radius * 0.33f) - side * (radius * 0.1f), r = o + along * (radius * 0.33f) + side * (radius * 0.1f);
                SkillMarks.Segment(Next(), inner, l, cc, w, false);
                SkillMarks.Segment(Next(), l, tip, cc, w, false);
                SkillMarks.Segment(Next(), tip, r, cc, w, false);
                SkillMarks.Segment(Next(), r, inner, cc, w, false);
                SkillMarks.Segment(Next(), inner, tip, Fade(cc, cc.a * 0.6f), w * 0.6f, false);   // the fold
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
