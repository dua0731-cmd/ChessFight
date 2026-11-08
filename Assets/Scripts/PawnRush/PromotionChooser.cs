using System;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The promotion choice (R130, user: "로그라이크 게임처럼 기물 목록이 화면에 UI로 보이고, 그 중에서 선택"): when this
    // PC's pawn has held a promotion pad (StationPromotion), four cards slide up - queen, rook, bishop, knight, each
    // with its Pawn Rush skill - and the player picks one with 1-4 or a click. The pawn keeps moving meanwhile;
    // after ChooseSeconds the host picks at random.
    //
    // Offline and on the host the answer goes straight to the pad (StationPromotion.Choose); a client hands it to
    // the match link (Asked), which sends it in its input to the host.
    public sealed class PromotionChooser : MonoBehaviour
    {
        // A client's answer, for the match link to send (the piece). Null offline and on the host.
        public static Action<PieceKind> Asked;

        static PromotionChooser current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Asked = null;
            current = null;
        }

        public static void Ensure()
        {
            if (current != null || !Application.isPlaying) return;
            current = new GameObject("Promotion chooser").AddComponent<PromotionChooser>();
        }

        StationPromotion pad;
        float openedAt, askedAt = -10f;
        PieceKind asked;
        bool cursorTaken;
        CursorLockMode savedLock;
        bool savedVisible;
        GUIStyle title, name, skill, line, key;
        Texture2D white;

        // The pad on which this PC's own pawn chooses (the host's word on a client).
        static StationPromotion Mine() => StationPromotion.ChoosingFor(MiniGameBase.LocalDriver);

        void Update()
        {
            var now = Mine();
            // A client's answer is on its way: hide the cards until the host's state catches up.
            if (now != null && Time.time - askedAt < 1.5f) now = null;
            if (now != pad)
            {
                pad = now;
                openedAt = Time.time;
                if (pad != null) ArenaFx.Sfx("sfx_mg_quarter", pad.transform.position, twoD: true, pitch: 1.5f);
            }
            if (pad == null) { GiveCursorBack(); return; }
            TakeCursor();
            for (int i = 0; i < PawnRushPromotion.Pieces.Length; i++)
                if (LegacyKeys.Down(KeyCode.Alpha1 + i) || LegacyKeys.Down(KeyCode.Keypad1 + i)) Pick(PawnRushPromotion.Pieces[i]);
        }

        void Pick(PieceKind piece)
        {
            if (pad == null) return;
            if (MiniGameBase.Remote)
            {
                Asked?.Invoke(piece);
                askedAt = Time.time;
                asked = piece;
            }
            else StationPromotion.Choose(MiniGameBase.LocalDriver, piece);
            ArenaFx.Sfx("sfx_mg_complete", pad.transform.position, twoD: true, volume: .6f);
            pad = null;
            GiveCursorBack();
        }

        // A free mouse while the cards are up (so they can be clicked), given back as it was.
        void TakeCursor()
        {
            if (cursorTaken) return;
            cursorTaken = true;
            savedLock = Cursor.lockState;
            savedVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void GiveCursorBack()
        {
            if (!cursorTaken) return;
            cursorTaken = false;
            Cursor.lockState = savedLock;
            Cursor.visible = savedVisible;
        }

        void OnDisable() => GiveCursorBack();

        void OnGUI()
        {
            if (pad == null) return;
            if (title == null)
            {
                var font = RuntimePanels.KoreanFont;
                title = new GUIStyle(GUI.skin.label) { font = font, fontSize = 26, alignment = TextAnchor.MiddleCenter, richText = true };
                name = new GUIStyle(title) { fontSize = 30 };
                skill = new GUIStyle(title) { fontSize = 18 };
                line = new GUIStyle(title) { fontSize = 14, wordWrap = true };
                key = new GUIStyle(title) { fontSize = 20 };
                white = Texture2D.whiteTexture;
            }
            float t = Mathf.Clamp01((Time.time - openedAt) / .25f);
            float rise = (1f - t) * 60f;
            int n = PawnRushPromotion.Pieces.Length;
            float w = Mathf.Min(220f, (Screen.width - 80f) / n - 16f), h = 290f, gap = 16f;
            float x0 = (Screen.width - (n * w + (n - 1) * gap)) * .5f, y = Screen.height * .5f - h * .5f + rise;
            var old = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, .45f * t);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
            GUI.color = new Color(1f, 1f, 1f, t);
            float left = Mathf.Max(0f, PawnRushPromotion.ChooseSeconds - (Time.time - openedAt));
            GUI.Label(new Rect(0, y - 70, Screen.width, 40), "<b>승격! 원하는 기물을 고르세요</b>", title);
            GUI.Label(new Rect(0, y - 36, Screen.width, 28), $"1~4 키 또는 클릭 · {Mathf.CeilToInt(left)}초 뒤 무작위", line);

            var mouse = Event.current.mousePosition;
            for (int i = 0; i < n; i++)
            {
                var piece = PawnRushPromotion.Pieces[i];
                var r = new Rect(x0 + i * (w + gap), y, w, h);
                bool hover = r.Contains(mouse);
                if (hover) r.y -= 8f;
                // The card: a dark face, a gold rim (brighter under the mouse).
                GUI.color = new Color(.85f, .66f, .2f, t * (hover ? 1f : .75f));
                GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), white);
                GUI.color = new Color(.11f, .12f, .15f, .96f * t);
                GUI.DrawTexture(r, white);
                GUI.color = new Color(1f, 1f, 1f, t);
                GUI.Label(new Rect(r.x, r.y + 10, r.width, 30), $"<color=#E2B23C>{i + 1}</color>", key);
                GUI.Label(new Rect(r.x, r.y + 40, r.width, 80), $"<size=64>{Symbol(piece)}</size>", title);
                GUI.Label(new Rect(r.x, r.y + 126, r.width, 40), $"<b>{ChessPieces.Name(piece)}</b>", name);
                GUI.Label(new Rect(r.x, r.y + 166, r.width, 28), $"<color=#E2B23C>{PawnRushPromotion.SkillName(piece)}</color>", skill);
                GUI.Label(new Rect(r.x + 12, r.y + 198, r.width - 24, 80), PawnRushPromotion.SkillLine(piece), line);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Pick(piece);
            }
            GUI.color = old;
        }

        static string Symbol(PieceKind piece)
        {
            switch (piece)
            {
                case PieceKind.Queen: return "♛";
                case PieceKind.Rook: return "♜";
                case PieceKind.Bishop: return "♝";
                case PieceKind.Knight: return "♞";
                default: return "♟";
            }
        }
    }
}
