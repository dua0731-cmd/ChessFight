using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The king's call on screen (R146): 승규 님's pick from the planning chat's mockups, D "체스 시계", with the lines he
    /// struck out left out (no "F 누르면 출발", no "날아가는 동안 무적 · 킹 뒤 빈 칸에 내려요"; the king's chips show the name and
    /// the piece only, no distance, no state, no section name).
    /// - The king with his list open: "왕의 부름 · 두 자리를 채우세요" over a clock face with two places, his teammates as pills
    ///   under it (a left click fills a place, again empties it, two at most), and the keys at the bottom. The cursor is free
    ///   while it is open. F sends the picks (the host calls them; RagdollPawn.AskKingCall, online LabGame.AskKingCall), the
    ///   right click closes it.
    /// - The king after calling: the two places with each one's time left, then "출발!", "도착", "대답 없음" or "취소".
    /// - The one called: a chess clock at the top right, the king (a real chess king icon and his name; 승규 님 10-15: "부른 사람
    ///   글 지워주고 왕관 표시도 지워주고 실제 체스 아이콘 넣어줘") and the time left, its F button on top, "왕의 부름을 받았습니다"
    ///   under it; once it has answered, the button goes down and the clock says "출발!".
    /// Drawn from the skill moments (KingCall, KingCallGo, KingCallLand, KingCallEnd) and the king's stage, the same on every PC.
    /// The game's display face (Black Han Sans) stands in for the mockup's Hahmlet; Malgun Gothic for the small lines.
    /// </summary>
    [DefaultExecutionOrder(320)]
    public class PawnRushKingCallUi : MonoBehaviour
    {
        // The mockup's colours.
        static readonly Color Ink = Hex("#1A1614"), Cream = Hex("#F4EEDC"), Gold = Hex("#C99A3A"), GoldLight = Hex("#F0CF7A"),
                              Cell = Hex("#2B2522"), Muted = Hex("#8F857C"), Edge = Hex("#5A4F48"), Brown = Hex("#7A5A1A"),
                              Bronze = Hex("#8A6A2A"), Rim = Hex("#5A4318"), Sub = Hex("#E8DCC0");

        LabGame game;
        readonly List<RagdollPawn> candidates = new List<RagdollPawn>();
        readonly List<RagdollPawn> picks = new List<RagdollPawn>();
        bool openBefore, freedCursor;

        class Slot
        {
            public RagdollPawn mate;
            public string name = "", glyph = "";
            public RagdollPawn.KingSlotState state;
            public float endsAt, changedAt;

            public void Clear()
            {
                mate = null;
                name = glyph = "";
                state = RagdollPawn.KingSlotState.None;
            }
        }

        // The calls this PC's king made, place by place.
        readonly Slot[] mine = { new Slot(), new Slot() };
        // A call to this PC's own piece.
        RagdollPawn caller;
        string callerName = "";
        float callEndsAt, callChangedAt;
        RagdollPawn.KingSlotState callState;

        Font display, label, glyphs;
        // One style per face, size and alignment: one style changed from call to call within a frame drew stale text
        // (another line's words, a word three times over).
        readonly Dictionary<(Font, int, TextAnchor, bool), GUIStyle> styles = new Dictionary<(Font, int, TextAnchor, bool), GUIStyle>();
        bool styled;
        Texture2D crown, shade;

        void OnEnable() => RagdollPawn.SkillFx += OnFx;

        void OnDisable()
        {
            RagdollPawn.SkillFx -= OnFx;
            if (freedCursor && game != null) game.SkillListOpen = false;
            freedCursor = false;
        }

        void OnDestroy()
        {
            if (crown != null) Destroy(crown);
            if (shade != null) Destroy(shade);
        }

        // ---------------------------------------------------------------- the moments

        void OnFx(SkillFxEvent e)
        {
            var me = game != null ? game.LocalPawn : null;
            int slot = Mathf.Clamp(e.count % 2, 0, 1);
            switch (e.kind)
            {
                case SkillFxKind.KingCall:
                    if (e.target != null) e.target.NoteKingCallSeen(e.size);
                    if (e.by != null && e.by == me)
                    {
                        if (slot == 0)
                        {
                            mine[0].Clear();
                            mine[1].Clear();
                        }
                        var s = mine[slot];
                        s.mate = e.target;
                        s.name = NameOf(e.target);
                        s.glyph = GlyphOf(e.target);
                        s.state = RagdollPawn.KingSlotState.Waiting;
                        s.endsAt = Time.time + e.size;
                        s.changedAt = Time.time;
                        picks.Clear();
                    }
                    if (e.target != null && e.target == me)
                    {
                        caller = e.by;
                        callerName = NameOf(e.by);
                        callEndsAt = Time.time + e.size;
                        callState = RagdollPawn.KingSlotState.Waiting;
                        callChangedAt = Time.time;
                    }
                    break;
                case SkillFxKind.KingCallGo:
                    if (e.target != null) e.target.NoteKingCallSeen(0f);
                    Mark(e.by, e.target, slot, RagdollPawn.KingSlotState.Going);
                    break;
                case SkillFxKind.KingCallLand:
                    Mark(e.by, e.target, slot, RagdollPawn.KingSlotState.Arrived);
                    break;
                case SkillFxKind.KingCallEnd:
                    if (e.target != null) e.target.NoteKingCallSeen(0f);
                    Mark(e.by, e.target, slot, e.count >= 2 ? RagdollPawn.KingSlotState.Cancelled : RagdollPawn.KingSlotState.Missed);
                    break;
            }
        }

        void Mark(RagdollPawn king, RagdollPawn mate, int slot, RagdollPawn.KingSlotState state)
        {
            var me = game != null ? game.LocalPawn : null;
            if (king != null && king == me && mine[slot].mate == mate)
            {
                mine[slot].state = state;
                mine[slot].changedAt = Time.time;
            }
            if (mate != null && mate == me && caller == king)
            {
                callState = state;
                callChangedAt = Time.time;
            }
        }

        static string NameOf(RagdollPawn p) => p != null ? p.DisplayName : "";

        static string GlyphOf(RagdollPawn p)
        {
            if (p == null) return "";
            switch (p.Piece)
            {
                case PieceKind.King: return "♔︎";
                case PieceKind.Queen: return "♕︎";
                case PieceKind.Rook: return "♖︎";
                case PieceKind.Bishop: return "♗︎";
                case PieceKind.Knight: return "♘︎";
                default: return "♙︎";
            }
        }

        // ---------------------------------------------------------------- the keys

        void Update()
        {
            if (game == null) game = FindFirstObjectByType<LabGame>();
            var me = game != null ? game.LocalPawn : null;
            bool open = me != null && me.KingListOpen && !game.AutoTest;
            if (open)
            {
                me.KingCallList(candidates);
                picks.RemoveAll(p => p == null || !candidates.Contains(p));
                // Not on the press that opened it (a client draws its own press at once).
                if (openBefore && !game.SuppressInput)
                {
                    if (game.SkillKey != KeyCode.None && Input.GetKeyDown(game.SkillKey))
                        Ask(me, picks.Count > 0 ? picks[0] : null, picks.Count > 1 ? picks[1] : null);
                    else if (Input.GetMouseButtonDown(1))
                    {
                        picks.Clear();
                        Ask(me, null, null);
                    }
                }
            }
            else picks.Clear();
            openBefore = open;
        }

        void Ask(RagdollPawn king, RagdollPawn a, RagdollPawn b)
        {
            if (king.NetworkPuppet) game.AskKingCall(a, b);
            else king.AskKingCall(a, b);
        }

        void LateUpdate()
        {
            if (game == null) return;
            // The cursor is free while the list is open (the pills are clicked), back to the view once it closes.
            if (openBefore && !freedCursor)
            {
                game.SkillListOpen = true;
                freedCursor = true;
            }
            else if (!openBefore && freedCursor)
            {
                game.SkillListOpen = false;
                freedCursor = false;
                if (!game.PanelOpen && !game.UiWantsCursor)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        void Toggle(RagdollPawn p)
        {
            if (picks.Contains(p)) picks.Remove(p);
            else if (picks.Count < 2) picks.Add(p);
        }

        // ---------------------------------------------------------------- drawing

        bool ShowMine
        {
            get
            {
                bool any = false, busy = false;
                float last = 0f;
                foreach (var s in mine)
                {
                    if (s.state == RagdollPawn.KingSlotState.None) continue;
                    any = true;
                    if (s.state == RagdollPawn.KingSlotState.Waiting || s.state == RagdollPawn.KingSlotState.Going) busy = true;
                    last = Mathf.Max(last, s.changedAt);
                }
                return any && (busy || Time.time - last < 1.2f);
            }
        }

        bool ShowCall
        {
            get
            {
                if (callState == RagdollPawn.KingSlotState.None) return false;
                if (callState == RagdollPawn.KingSlotState.Waiting) return Time.time < callEndsAt + 0.4f;
                if (callState == RagdollPawn.KingSlotState.Going) return Time.time - callChangedAt < 2f;
                return Time.time - callChangedAt < 0.9f;
            }
        }

        void OnGUI()
        {
            if (game == null || game.AutoTest || game.PanelOpen) return;
            var me = game.LocalPawn;
            if (me == null) return;
            Styles();
            // Over the test screen's own panels (the network box at the top right sat on the clock's F button).
            GUI.depth = -10;
            float s = Screen.height / 720f;
            if (me.KingListOpen) DrawList(me, s);
            else if (ShowMine) DrawWaiting(s);
            if (ShowCall) DrawNotice(s);
        }

        void Styles()
        {
            if (styled) return;
            styled = true;
            display = Resources.Load<Font>("Fonts/BlackHanSans-Regular");
            // The OS font's family name exactly (Unity 6 draws IMGUI text through TextCore: a name it cannot load, such as
            // "Malgun Gothic Bold", logged a warning on every label every frame and drew other glyphs in its place).
            label = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 16);
            glyphs = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Symbol", "Segoe UI Emoji", "Arial Unicode MS" }, 32);
            if (display == null) display = label;
            crown = CrownTexture();
            shade = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < 64; y++) shade.SetPixel(0, y, new Color(Ink.r, Ink.g, Ink.b, 0.55f * (y / 63f)));
            shade.Apply();
        }

        /// <summary>D's king screen: the list.</summary>
        void DrawList(RagdollPawn me, float s)
        {
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect(0f, 0f, Screen.width, 330f * s), shade, ScaleMode.StretchToFill);
            float y = Title(s, "두 자리를 채우세요");
            var places = new Slot[2];
            for (int i = 0; i < 2; i++)
            {
                var p = i < picks.Count ? picks[i] : null;
                places[i] = p == null ? null : new Slot { mate = p, name = NameOf(p), glyph = GlyphOf(p) };
            }
            ClockFace(s, y + 14f * s, places, null);

            // The teammates, as pills.
            const float H = 64f, Pad = 16f, Gap = 12f;
            var widths = new List<float>();
            float total = 0f;
            foreach (var p in candidates)
            {
                float w = (Pad * 2f + 30f + 10f) * s + Mathf.Max(Measure(p.DisplayName, label, 18f * s, true), Measure(ChessPieces.Name(p.Piece), label, 13f * s));
                widths.Add(w);
                total += w + (widths.Count > 1 ? Gap * s : 0f);
            }
            float top = 300f * s;
            if (candidates.Count == 0)
            {
                const string none = "같은 구간에 팀원이 없어요";
                float w = Measure(none, label, 18f * s) + 2f * Pad * s;
                var r = new Rect((Screen.width - w) * 0.5f, top, w, H * s);
                Fill(r, new Color(Ink.r, Ink.g, Ink.b, 0.9f), H * 0.5f * s);
                Line(r, Edge, 3f * s, H * 0.5f * s);
                Write(r, none, label, 18f * s, Cream, TextAnchor.MiddleCenter);
            }
            else
            {
                float x = (Screen.width - total) * 0.5f;
                for (int i = 0; i < candidates.Count; i++)
                {
                    var p = candidates[i];
                    var r = new Rect(x, top, widths[i], H * s);
                    bool on = picks.Contains(p);
                    Fill(r, on ? Gold : new Color(Ink.r, Ink.g, Ink.b, 0.9f), H * 0.5f * s);
                    Line(r, on ? Cream : Edge, 3f * s, H * 0.5f * s);
                    Color ink = on ? Ink : Cream;
                    Write(new Rect(r.x + Pad * s, r.y, 30f * s, r.height), GlyphOf(p), glyphs, 30f * s, ink, TextAnchor.MiddleCenter);
                    float tx = r.x + (Pad + 30f + 10f) * s;
                    Write(new Rect(tx, r.y + 12f * s, r.width, 22f * s), p.DisplayName, label, 18f * s, ink, TextAnchor.UpperLeft, true);
                    Write(new Rect(tx, r.y + 36f * s, r.width, 16f * s), ChessPieces.Name(p.Piece), label, 13f * s, ink, TextAnchor.UpperLeft);
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Toggle(p);
                    x += widths[i] + Gap * s;
                }
            }

            // The keys.
            Chips(s, ("좌클릭 · 고르기", false), ("F · 부르기", true), ("우클릭 · 취소", false));
        }

        /// <summary>D's king screen after the call (Flow 1 and 4): each place's time left, then how it went.</summary>
        void DrawWaiting(float s)
        {
            bool waiting = mine[0].state == RagdollPawn.KingSlotState.Waiting || mine[1].state == RagdollPawn.KingSlotState.Waiting;
            // The same shade as the list's: the crown's shaft of light stands right behind the title.
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect(0f, 0f, Screen.width, 330f * s), shade, ScaleMode.StretchToFill);
            float y = Title(s, waiting ? "대답을 기다려요" : null);
            ClockFace(s, y + 14f * s, new[] { mine[0].state != RagdollPawn.KingSlotState.None ? mine[0] : null, mine[1].state != RagdollPawn.KingSlotState.None ? mine[1] : null }, mine);
        }

        /// <summary>The crown, "왕의 부름" and a line beside it, centred at the top; returns the row's foot.</summary>
        float Title(float s, string line)
        {
            float top = 28f * s, h = 40f * s;
            float wName = Measure("왕의 부름", display, 30f * s), wLine = line != null ? Measure(line, label, 16f * s) : 0f;
            float w = 32f * s + 12f * s + wName + (line != null ? 12f * s + wLine : 0f);
            float x = (Screen.width - w) * 0.5f;
            Icon(new Rect(x, top + (h - 24f * s) * 0.5f, 32f * s, 24f * s), Gold);
            x += 44f * s;
            Write(new Rect(x, top, wName, h), "왕의 부름", display, 30f * s, Cream, TextAnchor.MiddleLeft);
            if (line != null) Write(new Rect(x + wName + 12f * s, top, wLine, h), line, label, 16f * s, Sub, TextAnchor.MiddleLeft);
            return top + h;
        }

        /// <summary>The clock face with its two places: empty (dashed), picked (cream, the piece and the name), or after the
        /// call (<paramref name="called"/>) with the time left or how it went.</summary>
        void ClockFace(float s, float top, Slot[] places, Slot[] called)
        {
            float w = 520f * s, pad = 12f * s, gap = 12f * s, h = 112f * s;
            var face = new Rect((Screen.width - w) * 0.5f, top, w, h + pad * 2f);
            Fill(face, Ink, 14f * s);
            Line(face, Gold, 3f * s, 14f * s);
            float cw = (w - pad * 2f - gap) * 0.5f;
            for (int i = 0; i < 2; i++)
            {
                var r = new Rect(face.x + pad + i * (cw + gap), face.y + pad, cw, h);
                var p = places[i];
                string place = $"{i + 1}번 자리";
                if (p == null)
                {
                    Fill(r, Cell, 10f * s);
                    Dashed(r, Edge, 2f * s, 10f * s, 8f * s);
                    Write(new Rect(r.x, r.y + 8f * s, r.width, 18f * s), place, label, 13f * s, Gold, TextAnchor.UpperCenter);
                    Write(new Rect(r.x, r.y + 28f * s, r.width, 46f * s), "—", glyphs, 40f * s, Muted, TextAnchor.MiddleCenter);
                    Write(new Rect(r.x, r.y + 78f * s, r.width, 24f * s), "비어 있음", label, 18f * s, Muted, TextAnchor.UpperCenter, true);
                    continue;
                }
                var state = called != null ? p.state : RagdollPawn.KingSlotState.None;
                bool gold = state == RagdollPawn.KingSlotState.Going || state == RagdollPawn.KingSlotState.Arrived;
                bool dark = state == RagdollPawn.KingSlotState.Missed || state == RagdollPawn.KingSlotState.Cancelled;
                Fill(r, gold ? Gold : dark ? Cell : Cream, 10f * s);
                Color ink = dark ? Muted : Ink;
                Write(new Rect(r.x, r.y + 8f * s, r.width, 18f * s), place, label, 13f * s, gold ? Ink : dark ? Gold : Brown, TextAnchor.UpperCenter);
                if (state == RagdollPawn.KingSlotState.None || state == RagdollPawn.KingSlotState.Waiting)
                {
                    Write(new Rect(r.x, r.y + 24f * s, r.width, 44f * s), p.glyph, glyphs, state == RagdollPawn.KingSlotState.Waiting ? 30f * s : 40f * s, ink, TextAnchor.MiddleCenter);
                    if (state == RagdollPawn.KingSlotState.Waiting)
                    {
                        Write(new Rect(r.x, r.y + 62f * s, r.width, 22f * s), p.name, label, 16f * s, ink, TextAnchor.UpperCenter, true);
                        Write(new Rect(r.x, r.y + 82f * s, r.width, 28f * s), Clock(p.endsAt - Time.time), display, 22f * s, ink, TextAnchor.UpperCenter);
                    }
                    else Write(new Rect(r.x, r.y + 76f * s, r.width, 24f * s), p.name, label, 18f * s, ink, TextAnchor.UpperCenter, true);
                    continue;
                }
                string how = state == RagdollPawn.KingSlotState.Going ? "출발!" : state == RagdollPawn.KingSlotState.Arrived ? "도착"
                           : state == RagdollPawn.KingSlotState.Cancelled ? "취소" : "대답 없음";
                Write(new Rect(r.x, r.y + 34f * s, r.width, 24f * s), p.name, label, 18f * s, ink, TextAnchor.UpperCenter, true);
                Write(new Rect(r.x, r.y + 60f * s, r.width, 40f * s), how, display, 26f * s, ink, TextAnchor.UpperCenter);
            }
        }

        /// <summary>D's teammate screen: the chess clock at the top right.</summary>
        void DrawNotice(float s)
        {
            bool going = callState == RagdollPawn.KingSlotState.Going || callState == RagdollPawn.KingSlotState.Arrived;
            bool off = callState == RagdollPawn.KingSlotState.Missed || callState == RagdollPawn.KingSlotState.Cancelled;
            float w = 440f * s, x = Screen.width - 40f * s - w, y = 36f * s;
            // Its two buttons on top: the right one is this player's F; it goes down once answered.
            float bw = 60f * s, inner = 70f * s, row = 26f * s;
            float leftH = (going ? 22f : 14f) * s, rightH = (going ? 10f : 26f) * s;
            var left = new Rect(x + inner, y + row - leftH, bw, leftH);
            var right = new Rect(x + w - inner - bw, y + row - rightH, bw, rightH);
            Fill(left, Bronze, 6f * s, true);
            Fill(right, going ? GoldLight : Gold, 6f * s, true);
            Line(right, Rim, 2f * s, 6f * s, true);
            if (!going) Write(right, "F", display, 16f * s, Ink, TextAnchor.MiddleCenter);
            // The clock.
            float pad = 16f * s, gap = 14f * s, ch = 118f * s;
            var body = new Rect(x, y + row, w, ch + pad * 2f);
            Fill(body, Ink, 12f * s);
            Line(body, Gold, 3f * s, 12f * s);
            float cw = (w - pad * 2f - gap) * 0.5f;
            var who = new Rect(body.x + pad, body.y + pad, cw, ch);
            var time = new Rect(who.xMax + gap, who.y, cw, ch);
            Fill(who, Cell, 10f * s);
            // Who called: the chess king (no label, no crown, R146).
            Write(new Rect(who.x, who.y + 4f * s, who.width, 74f * s), KingIcon, glyphs, 62f * s, Cream, TextAnchor.MiddleCenter);
            Write(new Rect(who.x, who.y + 80f * s, who.width, 26f * s), $"{callerName} · 킹", label, 20f * s, Cream, TextAnchor.UpperCenter, true);
            if (going)
            {
                Fill(time, Gold, 10f * s);
                Write(time, callState == RagdollPawn.KingSlotState.Arrived ? "도착" : "출발!", display, 46f * s, Ink, TextAnchor.MiddleCenter);
            }
            else if (off)
            {
                Fill(time, Cell, 10f * s);
                Write(time, callState == RagdollPawn.KingSlotState.Cancelled ? "취소" : "놓침", display, 40f * s, Muted, TextAnchor.MiddleCenter);
            }
            else
            {
                Fill(time, Cream, 10f * s);
                Write(new Rect(time.x, time.y + 10f * s, time.width, 18f * s), "남은 시간", label, 14f * s, Brown, TextAnchor.UpperCenter);
                Write(new Rect(time.x, time.y + 34f * s, time.width, 64f * s), Clock(callEndsAt - Time.time), display, 46f * s, Ink, TextAnchor.MiddleCenter);
            }
            // The band under it.
            var band = new Rect(body.x + 14f * s, body.yMax, w - 28f * s, 54f * s);
            Fill(band, Gold, 10f * s, false, true);
            Write(new Rect(band.x + 18f * s, band.y, band.width - 36f * s, band.height), "왕의 부름을 받았습니다", display, 28f * s, Ink, TextAnchor.MiddleLeft);
        }

        /// <summary>The keys at the bottom: dark chips, the call's in gold.</summary>
        void Chips(float s, params (string text, bool gold)[] chips)
        {
            float pad = 16f * s, gap = 10f * s, h = 36f * s, total = 0f;
            var widths = new float[chips.Length];
            for (int i = 0; i < chips.Length; i++)
            {
                widths[i] = Measure(chips[i].text, label, 16f * s, true) + pad * 2f;
                total += widths[i] + (i > 0 ? gap : 0f);
            }
            float x = (Screen.width - total) * 0.5f, y = Screen.height - 30f * s - h;
            for (int i = 0; i < chips.Length; i++)
            {
                var r = new Rect(x, y, widths[i], h);
                Fill(r, chips[i].gold ? Gold : new Color(Ink.r, Ink.g, Ink.b, 0.88f), 8f * s);
                Write(r, chips[i].text, label, 16f * s, chips[i].gold ? Ink : Cream, TextAnchor.MiddleCenter, true);
                x += widths[i] + gap;
            }
        }

        /// <summary>The chess king, filled (Segoe UI Symbol's).</summary>
        const string KingIcon = "♚︎";

        static string Clock(float seconds) => $"0:{Mathf.Max(0f, seconds):00.0}";

        // ---------------------------------------------------------------- primitives

        static void Fill(Rect r, Color c, float radius, bool topOnly = false, bool bottomOnly = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            var radii = topOnly ? new Vector4(radius, radius, 0f, 0f) : bottomOnly ? new Vector4(0f, 0f, radius, radius) : Vector4.one * radius;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero, radii);
        }

        static void Line(Rect r, Color c, float width, float radius, bool topOnly = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            var radii = topOnly ? new Vector4(radius, radius, 0f, 0f) : Vector4.one * radius;
            var widths = topOnly ? new Vector4(width, width, width, 0f) : Vector4.one * width;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, widths, radii);
        }

        /// <summary>A dashed edge along the straight sides (the empty place).</summary>
        static void Dashed(Rect r, Color c, float width, float radius, float dash)
        {
            if (Event.current.type != EventType.Repaint) return;
            for (float x = r.x + radius; x < r.xMax - radius; x += dash * 2f)
            {
                float len = Mathf.Min(dash, r.xMax - radius - x);
                GUI.DrawTexture(new Rect(x, r.y, len, width), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, 0f);
                GUI.DrawTexture(new Rect(x, r.yMax - width, len, width), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, 0f);
            }
            for (float y = r.y + radius; y < r.yMax - radius; y += dash * 2f)
            {
                float len = Mathf.Min(dash, r.yMax - radius - y);
                GUI.DrawTexture(new Rect(r.x, y, width, len), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, 0f);
                GUI.DrawTexture(new Rect(r.xMax - width, y, width, len), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, 0f);
            }
        }

        void Icon(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint || crown == null) return;
            GUI.DrawTexture(r, crown, ScaleMode.ScaleToFit, true, 0f, c, 0f, 0f);
        }

        GUIStyle StyleFor(Font font, float size, TextAnchor anchor, bool bold = false)
        {
            var key = (font, Mathf.Max(6, Mathf.RoundToInt(size)), anchor, bold);
            if (!styles.TryGetValue(key, out var style))
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    font = font, fontSize = key.Item2, alignment = anchor, richText = false, wordWrap = false,
                    clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
                    fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                };
                style.normal.textColor = Color.white;
                styles[key] = style;
            }
            return style;
        }

        void Write(Rect r, string s, Font font, float size, Color c, TextAnchor anchor, bool bold = false)
        {
            var old = GUI.contentColor;
            GUI.contentColor = c;
            GUI.Label(r, s, StyleFor(font, size, anchor, bold));
            GUI.contentColor = old;
        }

        float Measure(string s, Font font, float size, bool bold = false) => StyleFor(font, size, TextAnchor.UpperLeft, bold).CalcSize(new GUIContent(s)).x;

        /// <summary>The mockup's crown (54 x 40: five points and a band), white, to be tinted.</summary>
        static Texture2D CrownTexture()
        {
            const int W = 108, H = 80, S = 4;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            Vector2[] points = { new Vector2(4, 34), new Vector2(8, 10), new Vector2(18, 22), new Vector2(27, 4), new Vector2(36, 22), new Vector2(46, 10), new Vector2(50, 34) };
            var pixels = new Color[W * H];
            for (int py = 0; py < H; py++)
                for (int px = 0; px < W; px++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < S; sy++)
                        for (int sx = 0; sx < S; sx++)
                        {
                            // Texture rows go up; the mockup's y goes down.
                            float x = (px + (sx + 0.5f) / S) * 54f / W, y = 40f - (py + (sy + 0.5f) / S) * 40f / H;
                            bool band = x >= 4f && x <= 50f && y >= 32f && y <= 38f;
                            if (band || InPolygon(points, x, y)) inside++;
                        }
                    pixels[py * W + px] = new Color(1f, 1f, 1f, inside / (float)(S * S));
                }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static bool InPolygon(Vector2[] p, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x) inside = !inside;
            return inside;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
