using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// What the user's own skills show on the screen of the piece played here (USER_SKILLS 5.3 and 5.4's "화면" column):
    /// never over the middle - only the top, the bottom and the edges (5.2). From each piece's public state, so a client
    /// draws them too. Not seen in Unity yet.
    ///
    ///   납작이    "Space × 5" at the bottom, a dot filling for each press
    ///   멈춤      ⏸ and the time left at the top, the edges greyed
    ///   점프&밟기  a horseshoe at the top while a knight's jump can mark me
    ///   첫 두걸음  "F 한 번 더" at the bottom while the diagonal window is open (my own)
    /// </summary>
    public partial class PawnRushPlanMarks
    {
        GUIStyle jyBig, jySmall;
        float jyHeldSeen = -1f;

        void JyStyles()
        {
            if (jyBig != null) return;
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI" }, 32);
            jyBig = new GUIStyle(GUI.skin.label) { font = font, fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            jyBig.normal.textColor = new Color(0.98f, 0.96f, 0.9f);
            jySmall = new GUIStyle(jyBig) { fontSize = 16, fontStyle = FontStyle.Normal };
        }

        /// <summary>The JY hints for the piece played here (OnGUI, before the middle panel).</summary>
        void DrawJyScreen(RagdollPawn me)
        {
            if (me == null || me.PawnRushSkills == null) return;
            JyStyles();
            var white = Texture2D.whiteTexture;
            var s = me.PawnRushSkills;
            // 납작이: the presses at the bottom.
            if (me.CurrentCc == Cc4.Flat)
            {
                int need = Mathf.Max(1, s.flatPresses), got = Mathf.Clamp(me.FlatPresses, 0, need);
                float w = 300f, h = 64f, x = (Screen.width - w) * 0.5f, y = Screen.height - h - 24f;
                Panel(white, new Rect(x, y, w, h));
                GUI.Label(new Rect(x, y + 4f, w, 30f), $"Space × {need}", jyBig);
                float dot = 14f, gap = 10f, dx = x + (w - (need * dot + (need - 1) * gap)) * 0.5f;
                for (int i = 0; i < need; i++)
                {
                    GUI.color = i < got ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 1f, 1f, 0.25f);
                    GUI.DrawTexture(new Rect(dx + i * (dot + gap), y + 40f, dot, dot), white);
                }
                GUI.color = Color.white;
            }
            // 멈춤: ⏸ and the time at the top; the edges greyed.
            if (me.CurrentCc == Cc4.Frozen)
            {
                float edge = Mathf.Min(Screen.width, Screen.height) * 0.06f;
                GUI.color = new Color(0.5f, 0.5f, 0.55f, 0.45f);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, edge), white);
                GUI.DrawTexture(new Rect(0f, Screen.height - edge, Screen.width, edge), white);
                GUI.DrawTexture(new Rect(0f, edge, edge, Screen.height - 2f * edge), white);
                GUI.DrawTexture(new Rect(Screen.width - edge, edge, edge, Screen.height - 2f * edge), white);
                GUI.color = Color.white;
                float w = 220f, h = 54f, x = (Screen.width - w) * 0.5f, y = edge + 8f;
                Panel(white, new Rect(x, y, w, h));
                GUI.Label(new Rect(x, y + 4f, w, h - 8f), $"⏸  {me.CcLeft:0.0}초", jyBig);
            }
            // 점프&밟기: a knight in the air whose jump reaches me.
            foreach (var k in RagdollPawn.All)
            {
                if (k == null || k == me || k.PlanSkill != PawnRushSkillIds.JumpStomp || k.PlanPhaseNow != PlanPhase.Active) continue;
                if (me.Team != Teams.None && k.Team == me.Team) continue;
                Vector3 from = k.PlanPoint, feet = me.FeetPoint;
                if (new Vector2(feet.x - from.x, feet.z - from.z).magnitude > s.jyMarkRange || feet.y > from.y + s.jyMarkAbove) continue;
                bool lit = k.PlanTarget == me;
                float w = lit ? 250f : 200f, h = 46f, x = (Screen.width - w) * 0.5f, y = 18f;
                Panel(white, new Rect(x, y, w, h));
                var old = jySmall.normal.textColor;
                jySmall.normal.textColor = lit ? new Color(1f, 0.4f, 0.32f) : new Color(0.95f, 0.85f, 0.55f);
                GUI.Label(new Rect(x, y + 4f, w, h - 8f), lit ? "∩  편자! 밟힌다" : "∩  편자 무늬", jySmall);
                jySmall.normal.textColor = old;
                break;
            }
            // 케틀 궁: everyone is told who is aimed at; the one aimed at gets a red edge; a bullet taken by another is told.
            foreach (var b in RagdollPawn.All)
            {
                if (b == null) continue;
                var aimed = b.JyBowAimingAt;
                string notice = aimed != null ? $"{b.DisplayName} → {aimed.DisplayName} 저격!"
                              : b.JyBowStolen && b.PlanTarget != null ? $"{b.PlanTarget.DisplayName} 대신 맞음!" : null;
                if (notice == null) continue;
                float w = 340f, h = 40f, x = (Screen.width - w) * 0.5f, y = 72f;
                Panel(white, new Rect(x, y, w, h));
                var old = jySmall.normal.textColor;
                jySmall.normal.textColor = aimed != null ? new Color(1f, 0.45f, 0.38f) : new Color(1f, 0.85f, 0.4f);
                GUI.Label(new Rect(x, y + 2f, w, h - 4f), notice, jySmall);
                jySmall.normal.textColor = old;
                if (aimed == me)
                {
                    float edge = Mathf.Min(Screen.width, Screen.height) * 0.025f, beat = 0.35f + 0.25f * Mathf.Sin(Time.time * 12f);
                    GUI.color = new Color(1f, 0.15f, 0.12f, beat);
                    GUI.DrawTexture(new Rect(0f, 0f, Screen.width, edge), white);
                    GUI.DrawTexture(new Rect(0f, Screen.height - edge, Screen.width, edge), white);
                    GUI.DrawTexture(new Rect(0f, edge, edge, Screen.height - 2f * edge), white);
                    GUI.DrawTexture(new Rect(Screen.width - edge, edge, edge, Screen.height - 2f * edge), white);
                    GUI.color = Color.white;
                }
                break;
            }
            // 성벽 짓기: my own build - the wall left, the turns left, W A S D.
            if (me.JyBuilding)
            {
                float built = Mathf.Min(s.jyBuildLength, me.PlanTime * s.jyBuildSpeed), share = 1f - built / Mathf.Max(1f, s.jyBuildLength);
                int turnsLeft = Mathf.Max(0, s.jyBuildTurns - me.PlanNote);
                float w = 320f, h = 70f, x = (Screen.width - w) * 0.5f, y = Screen.height - h - 24f;
                Panel(white, new Rect(x, y, w, h));
                GUI.Label(new Rect(x, y + 2f, w, 24f), $"성벽 짓기 · W A S D = 꺾기 · F = 끝", jySmall);
                GUI.color = new Color(0.35f, 0.38f, 0.45f, 1f);
                GUI.DrawTexture(new Rect(x + 20f, y + 32f, w - 40f, 8f), white);
                GUI.color = new Color(0.95f, 0.8f, 0.45f, 1f);
                GUI.DrawTexture(new Rect(x + 20f, y + 32f, (w - 40f) * share, 8f), white);
                for (int i = 0; i < s.jyBuildTurns; i++)
                {
                    GUI.color = i < turnsLeft ? new Color(0.3f, 0.85f, 0.78f) : new Color(1f, 1f, 1f, 0.2f);
                    GUI.DrawTexture(new Rect(x + w * 0.5f - s.jyBuildTurns * 11f + i * 22f, y + 48f, 14f, 14f), white);
                }
                GUI.color = Color.white;
            }
            // 힘세지기: lifted - "잡혔다" and the 3 s it takes to slip out.
            RagdollPawn holder = null;
            foreach (var r in RagdollPawn.All)
                if (r != null && r != me && r.JyHolding == me) holder = r;
            if (holder != null)
            {
                if (jyHeldSeen < 0f) jyHeldSeen = Time.time;
                float left = Mathf.Max(0f, s.jyStrongEscape - (Time.time - jyHeldSeen));
                float w = 260f, h = 52f, x = (Screen.width - w) * 0.5f, y = 18f;
                Panel(white, new Rect(x, y, w, h));
                GUI.Label(new Rect(x, y + 2f, w, 28f), $"잡혔다!  {left:0.0}초", jyBig);
                GUI.color = new Color(1f, 0.6f, 0.3f, 1f);
                GUI.DrawTexture(new Rect(x + 20f, y + h - 12f, (w - 40f) * Mathf.Clamp01(left / Mathf.Max(0.1f, s.jyStrongEscape)), 5f), white);
                GUI.color = Color.white;
            }
            else jyHeldSeen = -1f;
            // 첫 두걸음: my own window for the headbutt.
            if (me.JyDiagOpen)
            {
                float w = 220f, h = 40f, x = (Screen.width - w) * 0.5f, y = Screen.height - h - 96f;
                Panel(white, new Rect(x, y, w, h));
                GUI.Label(new Rect(x, y + 2f, w, h - 4f), "F 한 번 더", jySmall);
            }
        }

        static void Panel(Texture2D white, Rect r)
        {
            GUI.color = new Color(0.05f, 0.08f, 0.16f, 0.72f);
            GUI.DrawTexture(r, white);
            GUI.color = Color.white;
        }
    }
}
