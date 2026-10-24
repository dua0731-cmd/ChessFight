using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R153 (Pawn Rush skill plan v0.3 §5.4): when a teammate rook holds its chain's handle out to the piece played here,
    /// the middle of the screen says so — "우클릭: 잡기" with the rook's name and the seconds left as a bar running down —
    /// until it is grabbed, wound back or the chain is gone. Read from the rook's skill state, so it works on every PC.
    /// </summary>
    [DefaultExecutionOrder(240)]
    public class PawnRushChainPrompt : MonoBehaviour
    {
        LabGame game;
        GUIStyle big, small;
        Texture2D white;

        void Awake()
        {
            game = GetComponent<LabGame>();
            if (game == null) game = FindFirstObjectByType<LabGame>();
        }

        void OnDestroy()
        {
            if (white != null) Destroy(white);
        }

        void OnGUI()
        {
            var me = game != null ? game.LocalPawn : null;
            if (me == null) return;
            var rook = me.RookOfferingMe();
            if (rook == null || rook.PawnRushSkills == null) return;
            if (big == null)
            {
                var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI" }, 32);
                big = new GUIStyle(GUI.skin.label) { font = font, fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                big.normal.textColor = new Color(0.98f, 0.96f, 0.9f);
                small = new GUIStyle(big) { fontSize = 17, fontStyle = FontStyle.Normal };
                small.normal.textColor = new Color(0.86f, 0.88f, 0.92f);
                white = Texture2D.whiteTexture;
            }
            GUI.depth = -10;
            float window = Mathf.Max(0.1f, rook.PawnRushSkills.rookAllyWindow);
            float left = rook.RookOfferLeft, share = Mathf.Clamp01(left / window);
            float w = 340f, h = 96f, x = (Screen.width - w) * 0.5f, y = Screen.height * 0.62f;
            GUI.color = new Color(0.05f, 0.08f, 0.16f, 0.78f);
            GUI.DrawTexture(new Rect(x, y, w, h), white);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 8f, w, 40f), "우클릭: 잡기", big);
            GUI.Label(new Rect(x, y + 46f, w, 24f), $"{rook.DisplayName}(룩)의 사슬 · {left:0.0}초", small);
            GUI.color = new Color(0.35f, 0.38f, 0.45f, 1f);
            GUI.DrawTexture(new Rect(x + 20f, y + h - 18f, w - 40f, 6f), white);
            GUI.color = new Color(0.86f, 0.9f, 0.96f, 1f);
            GUI.DrawTexture(new Rect(x + 20f, y + h - 18f, (w - 40f) * share, 6f), white);
            GUI.color = Color.white;
        }
    }
}
