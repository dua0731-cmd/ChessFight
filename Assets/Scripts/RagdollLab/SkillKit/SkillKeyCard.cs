using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The skill key cards at the bottom right of the view, shared by the three skill test beds (Pawn Rush, Queen of the
    /// Hill, Sword Fight): a key square in the piece's colour (grey while it can't be used, the cooldown draining it from
    /// the top), the skill's name and one line under it. Row 0 is the lowest card; more rows stack upward.
    /// </summary>
    public static class SkillKeyCard
    {
        public const float Width = 320f, Height = 56f, Gap = 8f;
        static readonly Color Panel = new Color(0.05f, 0.07f, 0.12f, 0.8f), Off = new Color(0.42f, 0.46f, 0.55f);
        static Texture2D white;
        static GUIStyle keyStyle, nameStyle, lineStyle;

        /// <summary>Where card <paramref name="row"/> goes: clear of the help at the bottom left and the gauges in the
        /// bottom middle.</summary>
        public static Rect Slot(int row) =>
            new Rect(Screen.width - Width - 24f, Screen.height - Height - 120f - row * (Height + Gap), Width, Height);

        /// <param name="key">Letter on the key square ("F", "E").</param>
        /// <param name="lit">Usable now (or running): the key square takes the piece's colour.</param>
        /// <param name="blink">Pulse the lit square (for a skill that only turns on under a condition).</param>
        /// <param name="cooldown01">Share of the cooldown still left, 0 when ready.</param>
        /// <param name="board">The Pawn Rush board look (its pads' colours): the panel in the pads' navy with a strip of the
        /// piece's pad colour along its top, the key letter navy on the light pad colour, the text cream.</param>
        public static void Draw(Rect r, string key, string name, string line, SkillInkKit.Palette pal, bool lit, bool blink, float cooldown01, bool board = false)
        {
            Styles();
            var old = GUI.color;
            GUI.color = board ? BoardPanel : Panel;
            GUI.DrawTexture(r, white);
            if (board)
            {
                Color strip = lit ? pal.main : Off;
                GUI.color = new Color(strip.r, strip.g, strip.b, 1f);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3f), white);
            }
            var sq = new Rect(r.x + 6f, r.y + 6f, r.height - 12f, r.height - 12f);
            GUI.color = new Color(pal.ink.r, pal.ink.g, pal.ink.b, 1f);
            GUI.DrawTexture(new Rect(sq.x - 2f, sq.y - 2f, sq.width + 4f, sq.height + 4f), white);
            Color c = lit ? pal.main : Off;
            GUI.color = new Color(c.r, c.g, c.b, lit && blink ? 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 9f) : 1f);
            GUI.DrawTexture(sq, white);
            if (cooldown01 > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(sq.x, sq.y, sq.width, sq.height * Mathf.Clamp01(cooldown01)), white);
            }
            GUI.color = board && lit ? BoardInk : Color.white;
            GUI.Label(sq, key, keyStyle);
            float tx = sq.xMax + 10f, tw = r.width - sq.width - 22f;
            GUI.color = board ? BoardText : Color.white;
            GUI.Label(new Rect(tx, r.y + 5f, tw, 22f), $"<b>{name}</b>", nameStyle);
            GUI.Label(new Rect(tx, r.y + 25f, tw, 22f), line, lineStyle);
            GUI.color = old;
        }

        // The Pawn Rush board look's card (R126): the pads' navy, their ink and the board's cream.
        static readonly Color BoardPanel = new Color(0.141f, 0.141f, 0.184f, 0.88f), BoardInk = new Color(0.141f, 0.141f, 0.184f), BoardText = new Color(0.95f, 0.85f, 0.74f);

        /// <summary>The usual second line: what it is doing, the cooldown left, or ready.</summary>
        public static string Line(string running, float cooldown, string ready) =>
            !string.IsNullOrEmpty(running) ? running : cooldown > 0f ? $"쿨타임 {cooldown:0.0}초" : ready;

        static void Styles()
        {
            if (white == null)
            {
                white = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                white.SetPixel(0, 0, Color.white);
                white.Apply();
            }
            if (keyStyle != null) return;
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 13);
            nameStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 13, richText = true, wordWrap = false, clipping = TextClipping.Clip };
            nameStyle.normal.textColor = Color.white;
            lineStyle = new GUIStyle(nameStyle) { fontSize = 12 };
            lineStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
            keyStyle = new GUIStyle(nameStyle) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        /// <summary>A piece's own colours (design A); the pawn has none, so it wears my side's teal.</summary>
        public static SkillInkKit.Palette Colors(PieceKind kind) => kind switch
        {
            PieceKind.King => SkillInkKit.King,
            PieceKind.Queen => SkillInkKit.Queen,
            PieceKind.Rook => SkillInkKit.Rook,
            PieceKind.Bishop => SkillInkKit.Bishop,
            PieceKind.Knight => SkillInkKit.Knight,
            _ => SkillInkKit.TeamAlly,
        };
    }
}
