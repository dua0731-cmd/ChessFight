using UnityEngine;

namespace ChessFight.Gameplay
{
    // The stamina bar at the bottom middle of the screen: green to red as it empties, "스테미나" over it. The offline
    // playtest draws it for its own pawn (PlaytestSpawner); a match draws the same one for this PC's pawn
    // (MatchStaminaBar, R514), where the playtest is switched off.
    public static class StaminaBarGui
    {
        public const float Width = 240f, Height = 12f, FromBottom = 44f;

        static readonly Color Back = new Color(0.08f, 0.1f, 0.14f, 0.7f);
        static readonly Color Empty = new Color(1f, 0.32f, 0.26f), Full = new Color(0.4f, 0.92f, 0.46f);

        /// <summary>Draws the bar from OnGUI. <paramref name="stamina"/> is 0..1.</summary>
        public static void Draw(float stamina, GUIStyle label)
        {
            var white = Texture2D.whiteTexture;
            var back = new Rect((Screen.width - Width) * 0.5f, Screen.height - FromBottom, Width, Height);
            var old = GUI.color;
            GUI.color = Back;
            GUI.DrawTexture(back, white);
            GUI.color = Color.Lerp(Empty, Full, stamina);
            GUI.DrawTexture(new Rect(back.x + 2f, back.y + 2f, (Width - 4f) * Mathf.Clamp01(stamina), Height - 4f), white);
            GUI.color = old;
            GUI.Label(new Rect(back.x, back.y - 20f, Width, 20f), "스테미나", label);
        }
    }
}
