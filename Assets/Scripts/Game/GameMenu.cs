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

        // 10-30 design A (사용자, https://claude.ai/artifact/GA6MaACNmS8iCFmLj5StYu "경기 메뉴 A"): the settings window's
        // look - smoked dark glass with a thin gold edge, rounded. Back to the game is the gold button (with its ESC cap),
        // settings a plain one, leaving a red-edged one under a line; the leave question is red-edged, the warning in a
        // red box, "남아서 계속하기" beside the red "나가기".
        static readonly Color Gold = new Color(.95f, .71f, .20f), Cream = new Color(.94f, .89f, .82f), Bright = new Color(1f, .96f, .86f),
            Muted = new Color(.66f, .58f, .49f), Dark = new Color(.08f, .05f, .03f, .95f), Plate = new Color(.17f, .13f, .09f),
            PlateEdge = new Color(.29f, .22f, .15f), Red = new Color(.85f, .27f, .23f), RedText = new Color(1f, .54f, .48f),
            RedSoft = new Color(1f, .71f, .66f);

        void Styles()
        {
            if (title != null) return;
            var font = RuntimePanels.KoreanFont;
            white = Texture2D.whiteTexture;
            title = new GUIStyle(GUI.skin.label) { font = font, fontSize = 30, alignment = TextAnchor.MiddleLeft, richText = true, fontStyle = FontStyle.Bold };
            sub = new GUIStyle(title) { fontSize = 14, fontStyle = FontStyle.Normal, wordWrap = true };
            red = new GUIStyle(sub) { fontSize = 15 };
            plain = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            button = new GUIStyle(plain);
            danger = new GUIStyle(plain);
        }

        // A rounded box: the fill, then (when edge.a > 0) an outline of `line` px.
        void Box(Rect r, Color fill, Color edge, float radius, float line = 1f)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, white, ScaleMode.StretchToFill, true, 0f, fill, Vector4.zero, new Vector4(radius, radius, radius, radius));
            if (edge.a > 0f)
                GUI.DrawTexture(r, white, ScaleMode.StretchToFill, true, 0f, edge, new Vector4(line, line, line, line), new Vector4(radius, radius, radius, radius));
        }

        void Say(Rect r, string text, GUIStyle style, Color color, TextAnchor anchor)
        {
            style.normal.textColor = color;
            style.alignment = anchor;
            GUI.Label(r, text, style);
        }

        // A flat rounded button, a little brighter under the mouse; `cap` is a key hint on its right.
        bool Flat(Rect r, string text, Color fill, Color edge, Color ink, string cap = null, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Box(r, hover ? Color.Lerp(fill, Color.white, .12f) : fill, hover && edge.a > 0f ? Color.Lerp(edge, Gold, .4f) : edge, 10f);
            Say(new Rect(r.x + 18, r.y, r.width - 36, r.height), text, plain, ink, anchor);
            if (cap != null)
            {
                var c = new Rect(r.xMax - 18 - 40, r.center.y - 11, 40, 22);
                Box(c, new Color(ink.r, ink.g, ink.b, .16f), Color.clear, 4f);
                sub.fontSize = 12;
                sub.fontStyle = FontStyle.Bold;
                Say(c, cap, sub, ink, TextAnchor.MiddleCenter);
                sub.fontSize = 14;
                sub.fontStyle = FontStyle.Normal;
            }
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
            GUI.color = old;
            // The same size on any screen as at 1080p (the design's 460 px panel).
            float scale = Mathf.Clamp(Screen.height / 1080f, .75f, 2f);
            var keep = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f)) * keep;
            float sw = Screen.width / scale, sh = Screen.height / scale;

            string[] lines = warning.Split('\n');
            float w = 460f, h = confirm ? 196f + lines.Length * 46f : 352f;
            var panel = new Rect((sw - w) * .5f, (sh - h) * .5f, w, h);
            Box(panel, Dark, confirm ? new Color(Red.r, Red.g, Red.b, .6f) : new Color(Gold.r, Gold.g, Gold.b, .4f), 16f);

            float x = panel.x + 30, iw = w - 60;
            if (!confirm)
            {
                Say(new Rect(x, panel.y + 26, iw, 40), "메뉴", title, Gold, TextAnchor.MiddleLeft);
                // "경기 진행 중": a red dot pill, the match does not stop.
                var pill = new Rect(panel.xMax - 30 - 118, panel.y + 34, 118, 26);
                Box(pill, new Color(Red.r, Red.g, Red.b, .16f), Color.clear, 13f);
                Box(new Rect(pill.x + 12, pill.center.y - 4, 8, 8), new Color(1f, .42f, .35f), Color.clear, 4f);
                sub.fontStyle = FontStyle.Bold;
                sub.fontSize = 13;
                Say(new Rect(pill.x + 26, pill.y, pill.width - 30, pill.height), "경기 진행 중", sub, RedText, TextAnchor.MiddleLeft);
                sub.fontStyle = FontStyle.Normal;
                sub.fontSize = 14;
                Say(new Rect(x, panel.y + 74, iw, 22), "메뉴를 열어도 경기는 멈추지 않아요", sub, Muted, TextAnchor.MiddleLeft);
                float y = panel.y + 112;
                if (Flat(new Rect(x, y, iw, 56), "게임으로 돌아가기", Gold, Color.clear, new Color(.1f, .07f, .04f), "ESC")) SetOpen(false);
                y += 64;
                if (Flat(new Rect(x, y, iw, 52), "설정", Plate, PlateEdge, Cream, "›")) SettingsWindow.Open();
                y += 52 + 14;
                GUI.color = new Color(1f, 1f, 1f, .07f);
                GUI.DrawTexture(new Rect(x, y, iw, 1), white);
                GUI.color = old;
                y += 14;
                plain.fontSize = 16;
                if (Flat(new Rect(x, y, iw, 48), "게임 나가기", Color.clear, new Color(Red.r, Red.g, Red.b, .45f), RedText)) confirm = true;
                plain.fontSize = 18;
            }
            else
            {
                Box(new Rect(x, panel.y + 28, 44, 44), new Color(Red.r, Red.g, Red.b, .16f), Color.clear, 22f);
                title.fontSize = 26;
                Say(new Rect(x, panel.y + 28, 44, 44), "!", title, new Color(1f, .42f, .35f), TextAnchor.MiddleCenter);
                Say(new Rect(x + 58, panel.y + 28, iw - 58, 44), "정말 나가시겠습니까?", title, Bright, TextAnchor.MiddleLeft);
                title.fontSize = 30;
                var note = new Rect(x, panel.y + 90, iw, lines.Length * 46f + 16f);
                Box(note, new Color(Red.r, Red.g, Red.b, .08f), new Color(Red.r, Red.g, Red.b, .3f), 10f);
                for (int i = 0; i < lines.Length; i++)
                    Say(new Rect(note.x + 16, note.y + 8 + i * 46f, note.width - 32, 46), "• " + lines[i], red, i == 0 ? RedSoft : Cream, TextAnchor.MiddleLeft);
                float y = note.yMax + 18, bw = (iw - 10) / 2f;
                if (Flat(new Rect(x, y, bw, 52), "남아서 계속하기", Plate, PlateEdge, Cream, null, TextAnchor.MiddleCenter)) confirm = false;
                if (Flat(new Rect(x + bw + 10, y, bw, 52), "나가기", Red, Color.clear, Color.white, null, TextAnchor.MiddleCenter)) { GUI.matrix = keep; Leave(); return; }
            }
            GUI.matrix = keep;
        }
    }
}
