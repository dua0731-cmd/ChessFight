using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // One player on the match sheet (the runtime fills these from the session).
    public struct SheetPlayer
    {
        public ulong Id;
        public string Name;
        public bool Me, Party, Bot, VoiceOff;
    }

    // Who is on the match sheet: my team on the left, the other on the right, best first.
    public sealed class SheetData
    {
        // 0 white, 1 black: my team's colour (the pieces and the headings follow it).
        public int MyTeam;
        public readonly List<SheetPlayer> Mine = new List<SheetPlayer>();
        public readonly List<SheetPlayer> Theirs = new List<SheetPlayer>();
    }

    // The voice chat in a match (Docs/Architecture/UI.md "음성 채팅", R131), one panel on the persistent
    // runtime object like the chat:
    //   - top left, only while someone talks: their name and a loudspeaker in their channel's colour
    //     (party green, team blue), so nothing covers the match the rest of the time;
    //   - P opens the match sheet (기보지, design 2-B) in the middle: a walnut box with a chessboard band,
    //     No. | 백 | 흑 like a scoresheet, my team always on the left. Party members carry a green edge,
    //     the other teammates a blue one; a piece icon appears only once that player is promoted; each
    //     teammate has a mute button; "내 목소리" picks the party or the whole team. P or Esc closes it.
    // While the sheet is open the mouse is the sheet's (GameSettings.PointerTaken): the camera stops
    // turning and the mouse buttons do nothing in the match, but the keys still move the character.
    // Steam-agnostic: the runtime hands over the sheet's players and the names.
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class VoiceHud : MonoBehaviour
    {
        public enum Layout { Hidden, Lobby, Match }

        // Over the scenes' HUDs, under the chat (50), the loading screen (100) and the settings (200).
        const float SortingOrder = 45f;
        const int TalkerRows = 8;
        const float FadeSeconds = .14f;

        static VoiceHud current;
        // True while the sheet is open and for a frame after Esc closed it: that Esc must not also leave
        // the match (Pawn Rush) or open a menu (Sword Fight).
        public static bool EscapeHeld => current != null && (current.open || Time.frameCount - current.closedFrame <= 1);
        public static bool SheetOpen => current != null && current.open;

        public Layout Where { get; set; }
        // How far down the talker list starts (a scene's own corner badge above it).
        public float TalkersTop { get; set; } = 18f;
        // The runtime's answers: who is on the sheet, and anyone's name.
        public Func<SheetData> Sheet;
        public Func<ulong, string> NameOf;

        PanelSettings ownedPanel;
        VisualElement root, talkersBox, sheetBox;
        readonly List<Talker> talkers = new List<Talker>();
        readonly List<Row> rows = new List<Row>();
        Label mineHead, theirsHead, keyCap, voiceNote;
        ChessGlyphs.PieceMark mineKing, theirsKing;
        VisualElement legend, picker;
        Button pickParty, pickTeam;
        bool open, cursorWasLocked;
        int closedFrame = -10;
        float sheetAlpha;

        sealed class Talker
        {
            public VisualElement Box, Tile;
            public ChessGlyphs.SpeakerMark Speaker;
            public ChessGlyphs.TalkBars Bars;
            public Label Name;
            public ulong Id;
            public float Alpha;
            public bool Wanted;
        }

        sealed class Side
        {
            public VisualElement Box, Edge, Tile;
            public ChessGlyphs.PieceMark Piece;
            public Label Name, Tag;
            public ChessGlyphs.TalkBars Bars;
            public Button Mute;
            public ChessGlyphs.SpeakerMark MuteMark;
            public ulong Id;
        }

        sealed class Row
        {
            public Label Number;
            public Side Mine, Theirs;
        }

        public void Build()
        {
            current = this;
            root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("VoiceHud"),
                                        Resources.Load<ThemeStyleSheet>("NetworkTheme"), null,
                                        new Vector2Int(1280, 720), out ownedPanel);
            if (root == null) return;
            if (ownedPanel != null) ownedPanel.sortingOrder = SortingOrder;
            // Not named after the UXML: Resources.Load<StyleSheet>("VoiceHud") returns the UXML's own
            // inline style sub-asset instead of a .uss of the same name.
            var sheetStyle = Resources.Load<StyleSheet>("VoiceChat");
            if (sheetStyle != null) root.styleSheets.Add(sheetStyle);
            else Debug.LogError("[ChessFight] Resources/VoiceChat.uss를 불러오지 못했습니다.");
            root.pickingMode = PickingMode.Ignore;
            talkersBox = root.Q<VisualElement>("vh-talkers");
            sheetBox = root.Q<VisualElement>("vh-sheet");
            if (talkersBox == null || sheetBox == null)
            { Debug.LogError("[ChessFight] VoiceHud.uxml의 이름이 VoiceHud와 맞지 않습니다."); root = null; return; }
            for (int i = 0; i < TalkerRows; i++) talkers.Add(NewTalker());
            BuildSheet();
            Show(sheetBox, false);
        }

        // ---------- the talker list ----------

        Talker NewTalker()
        {
            var t = new Talker { Box = Add(talkersBox, "vh-talker") };
            t.Tile = Add(t.Box, "vh-talker-tile");
            t.Speaker = new ChessGlyphs.SpeakerMark(VoiceBoard.TeamColor, 2.4f);
            t.Speaker.AddToClassList("vh-talker-icon");
            t.Tile.Add(t.Speaker);
            t.Name = Text(t.Box, "vh-talker-name", "");
            t.Bars = new ChessGlyphs.TalkBars(VoiceBoard.TeamColor, talkers.Count * .11f);
            t.Bars.AddToClassList("vh-talker-bars");
            t.Box.Add(t.Bars);
            t.Box.style.display = DisplayStyle.None;
            return t;
        }

        void DrawTalkers(float dt)
        {
            talkersBox.style.top = TalkersTop;
            bool show = Where == Layout.Match && VoiceBoard.Running;
            foreach (var t in talkers) t.Wanted = false;
            if (show)
                foreach (var talking in VoiceBoard.Talking)
                {
                    var slot = talkers.Find(t => t.Id == talking.Id && (t.Alpha > 0f || t.Wanted));
                    if (slot == null) slot = talkers.Find(t => t.Alpha <= 0f && !t.Wanted);
                    if (slot == null) break;
                    slot.Wanted = true;
                    if (slot.Id != talking.Id || slot.Alpha <= 0f)
                    {
                        slot.Id = talking.Id;
                        string name = NameOf?.Invoke(talking.Id) ?? "";
                        slot.Name.text = talking.Id == VoiceBoard.Self ? name + " (나)" : name;
                        // The newest speaker goes to the bottom of the list.
                        slot.Box.BringToFront();
                    }
                    var color = VoiceBoard.ColorOf(talking.Channel);
                    slot.Speaker.Set(ChessGlyphs.SpeakerMark.Look.Talking, color);
                    slot.Bars.SetColor(color);
                    slot.Tile.style.backgroundColor = Tile(color);
                    slot.Box.style.borderLeftColor = color;
                }
            foreach (var t in talkers)
            {
                t.Alpha = Mathf.MoveTowards(t.Alpha, t.Wanted ? 1f : 0f, dt / FadeSeconds);
                t.Box.style.opacity = t.Alpha;
                t.Box.style.display = t.Alpha > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // The speaker's tile: its channel colour at a fifth over the plaque's dark wood, composed in sRGB.
        static Color Tile(Color channel) => Color.Lerp(new Color(22 / 255f, 16 / 255f, 12 / 255f), channel, .22f);

        // ---------- the match sheet ----------

        void BuildSheet()
        {
            sheetBox.pickingMode = PickingMode.Position;
            sheetBox.style.backgroundImage = new StyleBackground(VoiceArt.Sheet());
            var paper = Add(sheetBox, "vh-paper");

            // The heading: 기보지, and on the right where my voice goes and the key that closes it.
            var left = Add(paper, "vh-head-left");
            var title = Text(left, "vh-title", "기 보 지");
            Serif(title);
            Text(left, "vh-subtitle", "경기 명단");
            var right = Add(paper, "vh-head-right");
            voiceNote = Text(right, "vh-voice-label", "내 목소리");
            picker = Add(right, "vh-picker");
            picker.pickingMode = PickingMode.Position;
            pickParty = Pick(picker, "파티", VoiceBoard.PartyColor, VoiceChannel.Party);
            pickTeam = Pick(picker, "팀 전체", VoiceBoard.TeamColor, VoiceChannel.Team);
            keyCap = Text(right, "vh-keycap", "P");

            // The column heads: No. | my team | theirs.
            var number = Text(paper, "vh-colhead vh-colhead-no", "No.");
            Serif(number);
            var mine = Add(paper, "vh-colhead vh-colhead-mine");
            mineKing = new ChessGlyphs.PieceMark(PieceKind.King, true, Brown);
            mineKing.AddToClassList("vh-colhead-king");
            mine.Add(mineKing);
            mineHead = Text(mine, "vh-colhead-text", "백 · 우리 팀");
            Serif(mineHead);
            Add(mine, "vh-grow");
            legend = Add(mine, "vh-legend");
            Swatch(legend, VoiceBoard.PartyColor, "파티");
            Swatch(legend, VoiceBoard.TeamColor, "팀원");
            var theirs = Add(paper, "vh-colhead vh-colhead-theirs");
            theirsKing = new ChessGlyphs.PieceMark(PieceKind.King, false, Brown);
            theirsKing.AddToClassList("vh-colhead-king");
            theirs.Add(theirsKing);
            theirsHead = Text(theirs, "vh-colhead-text", "흑 · 상대 팀");
            Serif(theirsHead);

            for (int i = 0; i < VoiceArt.Rows; i++)
            {
                float top = VoiceArt.RowsTop + i * VoiceArt.RowStep;
                var row = new Row { Number = Text(paper, "vh-number", (i + 1).ToString()) };
                Serif(row.Number);
                row.Number.style.top = top;
                row.Mine = NewSide(paper, top, true);
                row.Theirs = NewSide(paper, top, false);
                rows.Add(row);
            }
            RuntimePanels.ApplyDisplay(sheetBox);
        }

        static readonly Color Brown = new Color(156 / 255f, 122 / 255f, 85 / 255f);

        Button Pick(VisualElement parent, string label, Color color, VoiceChannel channel)
        {
            var button = new Button { focusable = false };
            button.AddToClassList("vh-pick");
            var dot = Add(button, "vh-pick-dot");
            dot.style.backgroundColor = color;
            Text(button, "vh-pick-text", label);
            button.clicked += () => { VoiceBoard.Picked = channel; Refresh(); };
            parent.Add(button);
            return button;
        }

        static void Swatch(VisualElement parent, Color color, string label)
        {
            var dot = Add(parent, "vh-swatch");
            dot.style.backgroundColor = color;
            Text(parent, "vh-swatch-text", label);
        }

        Side NewSide(VisualElement parent, float top, bool mine)
        {
            var side = new Side { Box = Add(parent, mine ? "vh-side vh-side-mine" : "vh-side vh-side-theirs") };
            side.Box.style.top = top;
            if (mine) side.Edge = Add(side.Box, "vh-edge");
            side.Tile = Add(side.Box, "vh-piece-tile");
            side.Piece = new ChessGlyphs.PieceMark(PieceKind.Pawn, mine, Brown);
            side.Piece.AddToClassList("vh-piece");
            side.Tile.Add(side.Piece);
            side.Name = Text(side.Box, "vh-name", "");
            side.Bars = new ChessGlyphs.TalkBars(VoiceBoard.TeamColor, top * .013f);
            side.Bars.AddToClassList("vh-name-bars");
            side.Box.Add(side.Bars);
            Add(side.Box, "vh-grow");
            side.Tag = Text(side.Box, "vh-tag", "");
            if (mine)
            {
                side.Mute = new Button { focusable = false };
                side.Mute.AddToClassList("vh-mute");
                side.MuteMark = new ChessGlyphs.SpeakerMark(Muted, 2.4f);
                side.MuteMark.AddToClassList("vh-mute-icon");
                side.Mute.Add(side.MuteMark);
                var who = side;
                side.Mute.clicked += () => { if (who.Id != 0) { VoiceBoard.ToggleMuted(who.Id); Refresh(); } };
                side.Box.Add(side.Mute);
            }
            return side;
        }

        static readonly Color Muted = new Color(191 / 255f, 174 / 255f, 142 / 255f);

        void SetOpen(bool value)
        {
            if (open == value) return;
            open = value;
            if (open)
            {
                cursorWasLocked = UnityEngine.Cursor.lockState == CursorLockMode.Locked;
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
                GameSettings.PointerTaken = true;
                Show(sheetBox, true);
                Refresh();
            }
            else
            {
                closedFrame = Time.frameCount;
                GameSettings.PointerTaken = false;
                if (cursorWasLocked && Application.isFocused)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }
        }

        void Refresh()
        {
            if (!open || root == null) return;
            var data = Sheet?.Invoke();
            int myTeam = data != null ? Mathf.Clamp(data.MyTeam, 0, 1) : 0;
            bool white = myTeam == 0;
            mineHead.text = (white ? "백" : "흑") + " · 우리 팀";
            theirsHead.text = (white ? "흑" : "백") + " · 상대 팀";
            mineKing.Set(PieceKind.King, white);
            theirsKing.Set(PieceKind.King, !white);
            keyCap.text = GameSettings.KeyName(GameSettings.Key(GameKey.Sheet));

            bool running = VoiceBoard.Running;
            Show(picker, running);
            Show(legend, true);
            voiceNote.text = running ? "내 목소리" : "음성 채팅 꺼짐";
            Choice(pickParty, VoiceBoard.Target == VoiceChannel.Party, VoiceBoard.PartyUsable);
            Choice(pickTeam, VoiceBoard.Target == VoiceChannel.Team, VoiceBoard.TeamUsable);

            for (int i = 0; i < rows.Count; i++)
            {
                DrawSide(rows[i].Mine, data != null && i < data.Mine.Count ? data.Mine[i] : (SheetPlayer?)null, white, true);
                DrawSide(rows[i].Theirs, data != null && i < data.Theirs.Count ? data.Theirs[i] : (SheetPlayer?)null, !white, false);
            }
        }

        static void Choice(Button button, bool chosen, bool usable)
        {
            button.EnableInClassList("vh-pick-on", chosen);
            button.SetEnabled(usable);
        }

        void DrawSide(Side side, SheetPlayer? who, bool whitePieces, bool mine)
        {
            if (who == null)
            {
                side.Id = 0;
                side.Box.EnableInClassList("vh-side-empty", true);
                side.Name.text = "";
                side.Tag.text = "";
                Show(side.Tile, false);
                Show(side.Bars, false);
                if (side.Edge != null) Show(side.Edge, false);
                if (side.Mute != null) Show(side.Mute, false);
                side.Box.style.backgroundColor = Color.clear;
                return;
            }
            var p = who.Value;
            side.Id = p.Id;
            side.Box.EnableInClassList("vh-side-empty", false);
            side.Box.EnableInClassList("vh-side-me", p.Me);
            side.Name.text = p.Name ?? "";
            bool promoted = MatchPieces.Promoted(p.Id, out var piece);
            Show(side.Tile, promoted);
            if (promoted) side.Piece.Set(piece, whitePieces);
            bool muted = !p.Me && VoiceBoard.IsMuted(p.Id);
            var channel = VoiceChannel.Team;
            bool talking = VoiceBoard.Running && !muted && mine && VoiceBoard.IsTalking(p.Id, out channel);
            Show(side.Bars, talking);
            if (talking)
            {
                var color = VoiceBoard.ColorOf(channel);
                side.Bars.SetColor(color);
                side.Box.style.backgroundColor = new Color(color.r, color.g, color.b, IntroFlowStyle.Light(.1f));
            }
            else side.Box.style.backgroundColor = Color.clear;
            side.Name.EnableInClassList("vh-name-quiet", muted || p.VoiceOff);
            side.Tag.text = p.Me ? "나" : p.Bot ? "AI" : mine && p.VoiceOff ? "음성 꺼짐" : "";
            if (side.Edge != null)
            {
                Show(side.Edge, true);
                side.Edge.style.backgroundColor = p.Party ? VoiceBoard.PartyColor : VoiceBoard.TeamColor;
            }
            if (side.Mute != null)
            {
                bool can = !p.Me && !p.Bot && !p.VoiceOff;
                Show(side.Mute, can);
                side.Mute.EnableInClassList("vh-mute-on", muted);
                side.MuteMark.Set(muted ? ChessGlyphs.SpeakerMark.Look.Muted : ChessGlyphs.SpeakerMark.Look.Quiet,
                                  muted ? VoiceBoard.MutedColor : Muted);
                side.Mute.tooltip = (muted ? "음소거 풀기: " : "음소거: ") + p.Name;
            }
        }

        // ---------- the frame ----------

        void Update()
        {
            if (root == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            bool match = Where == Layout.Match && !NameScreen.Showing;
            if (!match && open) SetOpen(false);
            if (match && !ChatBox.KeysHeld && !SettingsWindow.IsOpen)
            {
                if (GameSettings.Pressed(GameKey.Sheet)) SetOpen(!open);
                else if (open && LegacyKeys.Down(KeyCode.Escape)) SetOpen(false);
            }
            // The chat opened over it, or the settings: the sheet steps aside.
            if (open && (ChatBox.KeysHeld || SettingsWindow.IsOpen)) SetOpen(false);
            if (open) Refresh();

            sheetAlpha = Mathf.MoveTowards(sheetAlpha, open ? 1f : 0f, dt / FadeSeconds);
            sheetBox.style.opacity = sheetAlpha;
            float scale = .97f + .03f * sheetAlpha;
            sheetBox.style.scale = new Scale(new Vector3(scale, scale, 1f));
            Show(sheetBox, sheetAlpha > 0f);
            sheetBox.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
            DrawTalkers(dt);
        }

        // ---------- helpers ----------

        static Font serif;
        static bool serifTried;

        // The scoresheet's words in a serif face when Windows has one (바탕), else the display font.
        static void Serif(VisualElement element)
        {
            if (!serifTried)
            {
                serifTried = true;
                try
                {
                    var installed = Font.GetOSInstalledFontNames();
                    foreach (var name in new[] { "Batang", "바탕" })
                        if (Array.IndexOf(installed, name) >= 0) { serif = Font.CreateDynamicFontFromOSFont(name, 18); break; }
                }
                catch (Exception e) { Debug.LogWarning("[ChessFight] 기보지 글꼴: " + e.Message); }
            }
            if (serif != null)
            {
                element.style.unityFont = serif;
                element.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            else RuntimePanels.Display(element);
        }

        static Label Text(VisualElement parent, string classes, string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = false };
            foreach (var c in classes.Split(' ')) if (c.Length > 0) label.AddToClassList(c);
            parent.Add(label);
            return label;
        }

        static VisualElement Add(VisualElement parent, string classes)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (var c in classes.Split(' ')) if (c.Length > 0) element.AddToClassList(c);
            parent.Add(element);
            return element;
        }

        static void Show(VisualElement element, bool show)
        {
            if (element != null) element.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void OnDestroy()
        {
            if (open) GameSettings.PointerTaken = false;
            if (current == this) current = null;
            if (ownedPanel != null) Destroy(ownedPanel);
        }
    }
}
