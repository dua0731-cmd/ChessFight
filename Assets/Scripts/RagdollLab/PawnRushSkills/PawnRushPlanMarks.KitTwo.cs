using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The marks of the kits' second version (JY-skill-lab, 2026-11-21; RagdollPawn.KitTwo.cs): a light piece's pale ring
    /// bobbing at its chest, a heavy piece's dark ring at its feet; the queen's glide (its train and how far it may go),
    /// her eight lines (white turning gold as they are about to go), the rook's charge (its floor line, blinking before it
    /// sets off, then the wall's top as it rises); the words over heads ("+5", "체크 탈출"), and on a PC that is not the
    /// host the rook's walls themselves (PawnRushKitWall, a picture: the host's is the solid one).
    /// </summary>
    public partial class PawnRushPlanMarks
    {
        static readonly Color Pale = new Color(0.72f, 0.94f, 1f);
        static readonly Color Iron = new Color(0.32f, 0.34f, 0.4f);

        struct Pop
        {
            public RagdollPawn over;
            public int code;
            public float at;
        }

        readonly List<Pop> pops = new List<Pop>();
        GUIStyle popStyle;

        void OnEnable() => RagdollPawn.SkillFx += OnKitFx;

        void OnDisable() => RagdollPawn.SkillFx -= OnKitFx;

        void OnKitFx(SkillFxEvent e)
        {
            switch (e.kind)
            {
                case SkillFxKind.KitPop:
                    if (e.target != null) pops.Add(new Pop { over = e.target, code = e.count, at = Time.time });
                    break;
                case SkillFxKind.KitWall:
                    // The host's own wall is solid already; another PC stands a picture of it.
                    if (e.by == null || !e.by.NetworkPuppet) break;
                    var s = e.by.PawnRushSkills;
                    PawnRushKitWall.Raise(e.count, e.by, e.at, e.normal, s != null ? s.wallHeight : 1.2f, s != null ? s.wallThick : 0.45f,
                        Mathf.Max(0.1f, e.size), s != null ? s.wallRise : 0.2f, false);
                    break;
                case SkillFxKind.KitWallBreak:
                    if (e.by != null && e.by.NetworkPuppet) PawnRushKitWall.Find(e.count)?.Break();
                    break;
                case SkillFxKind.KitCrown:
                    // The host hid its own crown; another PC is told which.
                    if (e.by != null && e.by.NetworkPuppet) PawnRushGaugeCrown.Find(e.count)?.Take(e.size);
                    break;
            }
        }

        /// <summary>A full gauge: a small gold crown over the head, for everyone to see (and go for).</summary>
        void DrawReadyCrown(RagdollPawn pawn)
        {
            if (!pawn.UltReady || !pawn.HasUltimate) return;
            var cam = Camera.main;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            right.y = 0f;
            right = right.sqrMagnitude > 1e-4f ? right.normalized : Vector3.right;
            Vector3 b = pawn.Hips.position + Vector3.up * (1.05f + Mathf.Sin(Time.time * 3f) * 0.04f);
            crownPoints[0] = b - right * 0.2f;
            crownPoints[1] = b - right * 0.2f + Vector3.up * 0.2f;
            crownPoints[2] = b - right * 0.1f + Vector3.up * 0.09f;
            crownPoints[3] = b + Vector3.up * 0.24f;
            crownPoints[4] = b + right * 0.1f + Vector3.up * 0.09f;
            crownPoints[5] = b + right * 0.2f + Vector3.up * 0.2f;
            crownPoints[6] = b + right * 0.2f;
            crownPoints[7] = b - right * 0.2f;
            SkillMarks.Polyline(Next(), crownPoints, Fade(Gold, 1f), 0.045f);
        }

        readonly Vector3[] crownPoints = new Vector3[8];

        /// <summary>Light or heavy, on any piece.</summary>
        void DrawKitTwoStates(RagdollPawn pawn)
        {
            DrawReadyCrown(pawn);
            if (pawn.Light)
            {
                float bob = Mathf.Sin(Time.time * 6f) * 0.08f;
                float a = pawn.LightLeft < 0.6f ? 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(Time.time * 18f)) : 0.9f;
                SkillMarks.Circle(Next(), pawn.Hips.position + Vector3.up * (0.25f + bob), 0.42f, Fade(Pale, a), 0.04f, 20);
            }
            if (pawn.ChainHeavy)
            {
                float a = pawn.HeavyLeft < 0.6f ? 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(Time.time * 18f)) : 1f;
                SkillMarks.Circle(Next(), pawn.FeetPoint, 0.38f, Fade(Iron, a), 0.09f, 16);
            }
        }

        /// <summary>The queen's glide: the train from where she set off, and how far it may still go.</summary>
        void DrawGlide(RagdollPawn pawn)
        {
            var s = pawn.PawnRushSkills;
            if (s == null || pawn.PlanPhaseNow != PlanPhase.Active) return;
            Vector3 start = pawn.PlanPoint, way = pawn.PlanDir;
            way.y = 0f;
            if (way.sqrMagnitude < 1e-4f) return;
            way.Normalize();
            SkillMarks.Segment(Next(), start, pawn.FeetPoint, Fade(Gold, 0.85f), 0.12f);
            SkillMarks.Segment(Next(), pawn.FeetPoint, start + way * s.glideMax, Fade(White, 0.3f), 0.04f);
        }

        /// <summary>The eight lines: while she plants the staff, white to gold as they are about to go; the moment after, each
        /// line flashes.</summary>
        void DrawEightLines(RagdollPawn pawn)
        {
            var s = pawn.PawnRushSkills;
            if (s == null) return;
            var phase = pawn.PlanPhaseNow;
            if (phase != PlanPhase.Windup && phase != PlanPhase.Recovery) return;
            Vector3 c = pawn.PlanPoint + Vector3.up * 0.06f;
            float share = phase == PlanPhase.Windup ? Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, s.eightFuse)) : 1f;
            Color col = phase == PlanPhase.Windup ? Color.Lerp(White, Gold, share) : Gold;
            float a = phase == PlanPhase.Windup ? (share > 0.7f ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 30f)) : 0.75f)
                : Mathf.Clamp01(1f - pawn.PlanTime / 0.25f);
            if (a <= 0.01f) return;
            for (int k = 0; k < 8; k++)
                SkillMarks.Segment(Next(), c, c + pawn.EightLine(k) * s.eightLength, Fade(col, a), phase == PlanPhase.Windup ? 0.07f : 0.16f);
        }

        /// <summary>The rook's charge: aiming, its floor line; before it sets off, the line blinking for everyone; running, the
        /// wall's top behind it.</summary>
        void DrawWallCharge(RagdollPawn pawn)
        {
            var s = pawn.PawnRushSkills;
            if (s == null) return;
            switch (pawn.PlanPhaseNow)
            {
                case PlanPhase.Aim:
                    SkillMarks.Segment(Next(), pawn.FeetPoint + Vector3.up * 0.05f, pawn.PlanPoint + Vector3.up * 0.05f, Fade(White, 0.55f), 0.45f);
                    break;
                case PlanPhase.Windup:
                {
                    float blink = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * 24f));
                    SkillMarks.Segment(Next(), pawn.FeetPoint + Vector3.up * 0.05f, pawn.PlanPoint + Vector3.up * 0.05f, Fade(SkillMarks.Orange, blink), 0.45f);
                    break;
                }
                case PlanPhase.Active:
                {
                    // The wall's top as it rises (the solid wall is the host's; a picture comes once it stands).
                    Vector3 up = Vector3.up * s.wallHeight;
                    SkillMarks.Segment(Next(), pawn.PlanPoint + up, pawn.FeetPoint + up, Fade(Grey, 0.9f), 0.12f, false);
                    break;
                }
            }
        }

        /// <summary>The words over heads, rising and fading (OnGUI).</summary>
        void DrawPops()
        {
            if (pops.Count == 0) return;
            var cam = Camera.main;
            float life = 1.2f;
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var p = pops[i];
                if (p.over != null && p.over.PawnRushSkills != null) life = p.over.PawnRushSkills.popSeconds;
                if (p.over == null || Time.time - p.at > life) pops.RemoveAt(i);
            }
            if (cam == null || Event.current.type != EventType.Repaint) return;
            if (popStyle == null)
            {
                var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI" }, 32);
                popStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            foreach (var p in pops)
            {
                float age = Time.time - p.at, t = Mathf.Clamp01(age / life);
                Vector3 world = p.over.Hips.position + Vector3.up * (0.9f + 0.6f * t);
                Vector3 sp = cam.WorldToScreenPoint(world);
                if (sp.z <= 0f) continue;
                string text = p.code < 100 ? "+" + p.code
                    : p.code == RagdollPawn.PopCheckEscape ? "체크 탈출!"
                    : p.code == RagdollPawn.PopDefended ? "받쳐진 말"
                    : p.code == RagdollPawn.PopLight ? "가볍게!"
                    : p.code == RagdollPawn.PopHeavy ? "무겁게!"
                    : p.code == RagdollPawn.PopCrown ? $"왕관 +{(p.over.PawnRushSkills != null ? p.over.PawnRushSkills.gaugeCrown * 100f : 10f):0}%"
                    : p.code == RagdollPawn.PopUltCut ? "끊김! 게이지 절반"
                    : "";
                if (text.Length == 0) continue;
                Color c = p.code < 100 || p.code == RagdollPawn.PopCrown ? Gold : p.code == RagdollPawn.PopCheckEscape ? new Color(0.5f, 1f, 0.6f)
                    : p.code == RagdollPawn.PopLight ? Pale : p.code == RagdollPawn.PopHeavy ? new Color(0.75f, 0.78f, 0.86f) : White;
                float a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                float scale = p.code < 100 ? 1f + 0.4f * Mathf.Clamp01(1f - age / 0.15f) : 1f;   // a value pops in big
                var r = new Rect(sp.x - 120f, Screen.height - sp.y - 22f * scale, 240f, 44f * scale);
                popStyle.fontSize = Mathf.RoundToInt((p.code < 100 ? 34f : 24f) * scale);
                popStyle.normal.textColor = new Color(0f, 0f, 0f, 0.6f * a);
                GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, popStyle);
                popStyle.normal.textColor = new Color(c.r, c.g, c.b, a);
                GUI.Label(r, text, popStyle);
            }
        }
    }
}
