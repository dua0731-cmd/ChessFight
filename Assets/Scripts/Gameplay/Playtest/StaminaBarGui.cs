using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // The stamina gauge at the bottom middle of the screen (11-11, the user's pick: mockup "C · 꺾쇠 모서리",
    // artifact 5mYx5mpUgmApfS38EAHiWF). Every piece has the same bar; only the square on its left changes, showing the
    // chess symbol of the piece the player is now (♟♞♝♜♛♚). Both boxes are navy with a faint cream edge and a thick
    // cream bracket on each corner. No numbers and no ticks: cream from 60 %, amber to 25 %, red and blinking below.
    // The offline playtest draws it for its own pawn (PlaytestSpawner); a match draws the same one for this PC's pawn
    // (MatchStaminaBar, R514), where the playtest is switched off.
    public static class StaminaBarGui
    {
        // Sizes at a 900 px tall screen; the gauge scales with the screen height.
        const float Badge = 44f, BarWidth = 266f, BarHeight = 20f, Gap = 8f, Inset = 3f, FromBottom = 30f;

        static readonly Color Navy = new Color(29 / 255f, 43 / 255f, 94 / 255f, .94f);
        static readonly Color Cream = new Color(246 / 255f, 233 / 255f, 204 / 255f);
        static readonly Color Edge = new Color(246 / 255f, 233 / 255f, 204 / 255f, .25f);
        static readonly Color Track = new Color(246 / 255f, 233 / 255f, 204 / 255f, .10f);
        static readonly Color Amber = new Color(242 / 255f, 169 / 255f, 59 / 255f);
        static readonly Color Red = new Color(224 / 255f, 65 / 255f, 47 / 255f);

        static Font symbols;
        static bool symbolsTried;
        static GUIStyle glyphStyle, fallbackStyle;

        /// <summary>Draws the gauge from OnGUI. <paramref name="stamina"/> is 0..1; <paramref name="label"/> gives the
        /// Korean font for a PC without a chess symbol font.</summary>
        public static void Draw(float stamina, GUIStyle label, PieceKind piece = PieceKind.Pawn)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            stamina = Mathf.Clamp01(stamina);
            float s = Mathf.Clamp(Screen.height / 900f, .8f, 2.2f);
            float badge = Badge * s, width = BarWidth * s, height = BarHeight * s, gap = Gap * s;
            float x = (Screen.width - (badge + gap + width)) * .5f;
            float bottom = Screen.height - FromBottom * s;
            var badgeRect = new Rect(x, bottom - badge, badge, badge);
            var barRect = new Rect(x + badge + gap, badgeRect.center.y - height * .5f, width, height);

            var color = stamina >= .6f ? Cream : stamina >= .25f ? Amber : Red;
            // Below a quarter the fill and the symbol blink (0.35 s on, 0.35 s dimmed), as in the mockup.
            if (stamina < .25f && Mathf.Repeat(Time.unscaledTime, .7f) >= .35f) color.a = .4f;

            var old = GUI.color;
            Box(badgeRect, s);
            Box(barRect, s);
            float inset = Inset * s;
            var inner = new Rect(barRect.x + inset, barRect.y + inset, barRect.width - 2f * inset, barRect.height - 2f * inset);
            Fill(inner, Track);
            Fill(new Rect(inner.x, inner.y, inner.width * stamina, inner.height), color);
            GUI.color = old;
            Glyph(badgeRect, piece, label, stamina < .25f ? color : Cream, s);
        }

        // A navy box, a faint cream edge, and a thick cream bracket on each corner reaching 2 px past the box.
        static void Box(Rect r, float s)
        {
            Fill(r, Navy);
            float f = Mathf.Max(1f, s);
            Fill(new Rect(r.x, r.y, r.width, f), Edge);
            Fill(new Rect(r.x, r.yMax - f, r.width, f), Edge);
            Fill(new Rect(r.x, r.y + f, f, r.height - 2f * f), Edge);
            Fill(new Rect(r.xMax - f, r.y + f, f, r.height - 2f * f), Edge);

            float o = 2f * s, t = Mathf.Max(2f, 3f * s);
            var b = new Rect(r.x - o, r.y - o, r.width + 2f * o, r.height + 2f * o);
            float len = Mathf.Min(9f * s, r.height / 2.4f) + o;
            Fill(new Rect(b.x, b.y, len, t), Cream); Fill(new Rect(b.x, b.y, t, len), Cream);
            Fill(new Rect(b.xMax - len, b.y, len, t), Cream); Fill(new Rect(b.xMax - t, b.y, t, len), Cream);
            Fill(new Rect(b.xMax - len, b.yMax - t, len, t), Cream); Fill(new Rect(b.xMax - t, b.yMax - len, t, len), Cream);
            Fill(new Rect(b.x, b.yMax - t, len, t), Cream); Fill(new Rect(b.x, b.yMax - len, t, len), Cream);
        }

        static void Fill(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        // The piece's chess symbol from Windows' symbol font; its Korean name where no such font is found.
        static void Glyph(Rect r, PieceKind piece, GUIStyle label, Color color, float s)
        {
            string symbol = Symbol(piece);
            if (!symbolsTried)
            {
                symbolsTried = true;
                try { symbols = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Symbol", "Arial Unicode MS", "DejaVu Sans" }, 64); }
                catch (System.Exception) { symbols = null; }
            }
            bool has = symbols != null && symbols.HasCharacter(symbol[0]);
            var style = has ? GlyphStyle() : FallbackStyle(label);
            style.fontSize = Mathf.RoundToInt((has ? 32f : 13f) * s);
            style.normal.textColor = color;
            // The symbol sits a little low in its line; lift it to the middle of the square.
            var place = has ? new Rect(r.x, r.y - 2f * s, r.width, r.height) : r;
            GUI.Label(place, has ? symbol : Name(piece), style);
        }

        static GUIStyle GlyphStyle()
        {
            if (glyphStyle == null)
                glyphStyle = new GUIStyle { font = symbols, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
            return glyphStyle;
        }

        static GUIStyle FallbackStyle(GUIStyle label)
        {
            if (fallbackStyle == null)
                fallbackStyle = new GUIStyle(label ?? GUIStyle.none) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = false };
            return fallbackStyle;
        }

        static string Symbol(PieceKind piece)
        {
            switch (piece)
            {
                case PieceKind.King: return "♚";
                case PieceKind.Queen: return "♛";
                case PieceKind.Rook: return "♜";
                case PieceKind.Bishop: return "♝";
                case PieceKind.Knight: return "♞";
                default: return "♟";
            }
        }

        static string Name(PieceKind piece)
        {
            switch (piece)
            {
                case PieceKind.King: return "킹";
                case PieceKind.Queen: return "퀸";
                case PieceKind.Rook: return "룩";
                case PieceKind.Bishop: return "비숍";
                case PieceKind.Knight: return "나이트";
                default: return "폰";
            }
        }
    }
}
