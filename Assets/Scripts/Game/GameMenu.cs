using System;
using UnityEngine;

namespace ChessFight.Game
{
    // The in-game menu (R148, user: "ESC를 누르면 바로 나가져. 메뉴 UI창 - 게임으로 돌아가기, 설정, 게임 나가기(정말
    // 나가시겠습니까, 빨간색, 패널티 경고)"). Esc in a match scene opens it instead of leaving: back to the game, the
    // settings window (the same one as the title screen's), and leaving - which first asks "정말 나가시겠습니까?" with
    // a red leave button and the warning. The match does not pause (everyone else plays on); this PC's pawn stops
    // taking input and the mouse is free while it is open.
    //
    // A match scene installs it with what leaving means there (Use): the network match's cancel, Sword Fight's own
    // exit, the offline playtest's way back to the lobby. Esc that closes the chat or the voice sheet is theirs.
    // IMGUI on purpose: its clicks never depend on the UI Toolkit event path (Docs/Architecture/UI.md §2).
    public sealed class GameMenu : MonoBehaviour
    {
        static GameMenu current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        // Open: the game's own input (moving, skills, the camera's mouse) is off.
        public static bool IsOpen => current != null && current.open;
        // This scene has a menu (its Esc is the menu's: the camera does not free the mouse on it).
        public static bool Installed => current != null;

        Action leave;
        string warning;
        bool open, confirm, cursorTaken, savedVisible;
        CursorLockMode savedLock;
        int closedFrame = -10;
        GUIStyle title, sub, button, danger, red, plain;
        Texture2D white;

        // Esc was the menu's this frame or the last (a scene's own Esc handling skips it).
        public static bool EscapeHeld => current != null && (current.open || Time.frameCount - current.closedFrame <= 1);

        // This scene's menu: `leave` is what "나가기" does, `online` picks the warning (a match: leaving is desertion;
        // offline: only the practice ends). Replaces the menu a scene already has.
        public static GameMenu Use(GameObject host, Action leave, bool online)
        {
            if (host == null) return null;
            var menu = host.GetComponent<GameMenu>() ?? host.AddComponent<GameMenu>();
            menu.leave = leave;
            menu.warning = online
                ? "경기 도중 나가면 탈주로 처리되어 불이익(페널티)을 받을 수 있습니다.\n팀은 한 명이 빠진 채로 계속 싸우게 되고, 빈자리는 다른 플레이어가 채울 수 있습니다."
                : "연습을 끝내고 로비로 돌아갑니다. 지금까지의 진행은 저장되지 않습니다.";
            current = menu;
            return menu;
        }

        // A scene that has no menu yet gets this one (an offline playtest; a network match installs its own first).
        public static void UseIfNone(GameObject host, Action leave, bool online)
        {
            if (current == null) Use(host, leave, online);
        }

        public static void Close()
        {
            if (current != null) current.SetOpen(false);
        }

        void OnDestroy()
        {
            if (current == this)
            {
                GiveCursorBack();
                current = null;
            }
        }

        void Update()
        {
            if (current != this) return;
            if (ChatBox.KeysHeld || VoiceHud.EscapeHeld) return;
            if (!LegacyKeys.Down(KeyCode.Escape)) return;
            // The settings window over the menu: Esc steps back through it (its lists, then the window).
            if (SettingsWindow.IsOpen) { SettingsWindow.Back(); return; }
            if (confirm) { confirm = false; return; }
            SetOpen(!open);
        }

        void SetOpen(bool value)
        {
            if (open == value) return;
            open = value;
            confirm = false;
            if (open) TakeCursor();
            else
            {
                closedFrame = Time.frameCount;
                if (SettingsWindow.IsOpen) SettingsWindow.Close();
                GiveCursorBack();
            }
        }

        void TakeCursor()
        {
            if (cursorTaken) return;
            cursorTaken = true;
            savedLock = Cursor.lockState;
            savedVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameSettings.PointerTaken = true;
        }

        void GiveCursorBack()
        {
            if (!cursorTaken) return;
            cursorTaken = false;
            GameSettings.PointerTaken = false;
            if (savedLock == CursorLockMode.Locked && Application.isFocused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = savedLock;
                Cursor.visible = savedVisible;
            }
        }

        void Leave()
        {
            var action = leave;
            SetOpen(false);
            try { action?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ---------- the look ----------

        static readonly Color Ink = new Color(.07f, .06f, .05f, .94f), Gold = new Color(.89f, .70f, .24f), Cream = new Color(.95f, .92f, .86f);
        static readonly Color Red = new Color(.88f, .22f, .26f), RedDark = new Color(.55f, .08f, .12f);

        void Styles()
        {
            if (title != null) return;
            var font = RuntimePanels.KoreanFont;
            white = Texture2D.whiteTexture;
            title = new GUIStyle(GUI.skin.label) { font = font, fontSize = 30, alignment = TextAnchor.MiddleCenter, richText = true, fontStyle = FontStyle.Bold };
            title.normal.textColor = Gold;
            sub = new GUIStyle(title) { fontSize = 14, fontStyle = FontStyle.Normal, wordWrap = true };
            sub.normal.textColor = new Color(.78f, .73f, .64f);
            red = new GUIStyle(sub) { fontSize = 15 };
            red.normal.textColor = new Color(1f, .45f, .45f);
            plain = new GUIStyle(GUI.skin.label) { font = font, fontSize = 19, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            button = new GUIStyle(plain);
            danger = new GUIStyle(plain);
        }

        // A slab button: a darker edge under a face, brighter under the mouse.
        bool Slab(Rect r, string text, Color face, Color edge, Color ink)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            var old = GUI.color;
            GUI.color = edge;
            GUI.DrawTexture(new Rect(r.x, r.y + 4, r.width, r.height), white);
            GUI.color = hover ? Color.Lerp(face, Color.white, .18f) : face;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height - 2), white);
            GUI.color = old;
            plain.normal.textColor = ink;
            GUI.Label(new Rect(r.x, r.y - 1, r.width, r.height), text, plain);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        void OnGUI()
        {
            if (!open || current != this || SettingsWindow.IsOpen) return;
            Styles();
            GUI.depth = -100;
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
            float w = 440f, h = confirm ? 330f : 360f;
            var panel = new Rect((Screen.width - w) * .5f, (Screen.height - h) * .5f, w, h);
            GUI.color = Gold;
            GUI.DrawTexture(new Rect(panel.x - 3, panel.y - 3, panel.width + 6, panel.height + 6), white);
            GUI.color = Ink;
            GUI.DrawTexture(panel, white);
            GUI.color = old;

            float bx = panel.x + 50, bw = w - 100, bh = 52;
            if (!confirm)
            {
                GUI.Label(new Rect(panel.x, panel.y + 24, w, 40), "메뉴", title);
                GUI.Label(new Rect(panel.x, panel.y + 64, w, 22), "경기는 멈추지 않고 계속됩니다 · Esc 닫기", sub);
                float y = panel.y + 110;
                if (Slab(new Rect(bx, y, bw, bh), "게임으로 돌아가기", new Color(1f, .78f, .3f), new Color(.43f, .26f, 0f), new Color(.11f, .07f, 0f))) SetOpen(false);
                y += bh + 20;
                if (Slab(new Rect(bx, y, bw, bh), "설정", new Color(.42f, .28f, .17f), new Color(.12f, .07f, .04f), Cream)) SettingsWindow.Open();
                y += bh + 20;
                if (Slab(new Rect(bx, y, bw, bh), "게임 나가기", new Color(.25f, .23f, .22f), new Color(.08f, .07f, .07f), new Color(1f, .6f, .6f))) confirm = true;
            }
            else
            {
                GUI.Label(new Rect(panel.x, panel.y + 26, w, 40), "정말 나가시겠습니까?", title);
                GUI.Label(new Rect(panel.x + 30, panel.y + 80, w - 60, 110), warning, red);
                float y = panel.y + h - 2 * bh - 50;
                if (Slab(new Rect(bx, y, bw, bh), "나가기", Red, RedDark, Color.white)) { Leave(); return; }
                y += bh + 18;
                if (Slab(new Rect(bx, y, bw, bh), "취소", new Color(.42f, .28f, .17f), new Color(.12f, .07f, .04f), Cream)) confirm = false;
            }
        }
    }
}
