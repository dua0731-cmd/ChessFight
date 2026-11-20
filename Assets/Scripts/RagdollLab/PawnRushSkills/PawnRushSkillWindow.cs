using System;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 개발자 폰러쉬 스킬트리 - the developers' skill tree (JY-skill-lab, 2026-11-19; the user: "스킬 창을 따로 만들고, 거기에 각 칸에
    /// 드래그 앤 드롭으로 스킬들이 ... 자유롭게 넣었다 뺐다 할 수 있는 거야. 막 바꿔보면서 테스트할 수 있게 ... 개발자용 테스트",
    /// "창은 k키 같은 걸로 열리게", "아무 기물에다 넣을 수 있게", and its name: "이걸 '개발자 폰러쉬 스킬트리'라 이름짓고"). K opens
    /// it. On the left the skill book (PawnRushSkillBook), each skill with the colours of its tags and filtered by tag; on
    /// the right every piece's slots. A skill is dragged onto a slot (or clicked, then the slot clicked), any skill on any
    /// piece; the cross on a slot empties it. The skill system at the top: 1 a slot a piece, 2 the same with a stage 1..3
    /// set here by hand, 3 three slots a piece (the skill key and the two ability keys). What it edits is
    /// PawnRushSkillParams.loadout, kept on this PC between plays.
    ///
    /// Every change is two numbers (PawnRushLoadout.Apply). Where this PC runs the skills itself (offline, a match's host)
    /// the window makes the change; elsewhere it is asked of the host (<see cref="Send"/>) and shows when the host's
    /// loadout comes back. In a room everyone opens it ("테스트하는 우리 모두 개발자이기 때문에 모두가 열 수도 있게"), and the room's
    /// manager can keep the changes to himself with the switch at the top ("방장이 직접 지정하는 것(다른 인원들은 스킬 못 바꾸는
    /// 것)도 가능하게").
    /// </summary>
    [DefaultExecutionOrder(260)]
    public class PawnRushSkillWindow : MonoBehaviour
    {
        public const string Title = "개발자 폰러쉬 스킬트리";

        public KeyCode toggleKey = KeyCode.K;

        /// <summary>The numbers whose loadout this window edits.</summary>
        public PawnRushSkillParams Skills;
        /// <summary>May this change (PawnRushLoadout's Op numbers) be made here now? Unset: yes.</summary>
        public Func<int, bool> CanEdit;
        /// <summary>Why not, for the player.</summary>
        public Func<int, string> LockedNote;
        /// <summary>Set where another PC runs the skills (a match's host): a change is asked of it instead of made here.</summary>
        public Action<int, int> Send;
        /// <summary>A change was made here (not one sent on).</summary>
        public Action Changed;
        /// <summary>The names of the three slots' keys, as this scene binds them.</summary>
        public Func<int, string> KeyName;
        /// <summary>A room: the "only the manager changes it" switch is shown.</summary>
        public Func<bool> ShowLock;
        /// <summary>The window may not be opened now (the entrance screen still up).</summary>
        public Func<bool> Blocked;
        /// <summary>The window frees the mouse itself while it is open (the course: GameSettings.PointerTaken, as the
        /// promotion cards do). The lab's scenes keep their own cursor rule (LabGame.UiWantsCursor).</summary>
        public bool OwnCursor;

        public bool Open { get; private set; }
        /// <summary>A skill tree is open somewhere in this scene: the mouse is its own (the lab's cursor rule reads this).</summary>
        public static bool AnyOpen { get; private set; }
        /// <summary>The window's place on the screen while it is open (for the cursor: clicks on it are its own).</summary>
        public Rect Area { get; private set; }

        const string Pref = "ChessFight.PawnRushLoadout";

        SkillTag filter = SkillTag.None;
        int originFilter = -1;   // SkillOrigin, or -1 for every one
        int dragId, pickId, hoverId;
        float scroll;
        string message = "";
        float messageUntil;
        bool cursorTaken, savedVisible;
        CursorLockMode savedLock;
        GUIStyle label, small, title, cardName, center, glyph, tileName;
        Texture2D white;

        static readonly Color Panel = new Color(0.05f, 0.07f, 0.12f, 0.94f), Card = new Color(0.13f, 0.16f, 0.24f, 1f),
            CardDim = new Color(0.09f, 0.11f, 0.16f, 1f), Edge = new Color(1f, 1f, 1f, 0.16f), Lit = new Color(1f, 0.85f, 0.3f, 1f),
            SlotEmpty = new Color(1f, 1f, 1f, 0.05f), Ink = new Color(0.95f, 0.96f, 0.98f), Faint = new Color(0.62f, 0.66f, 0.74f);

        /// <summary>The loadout this PC last had, into <paramref name="skills"/> (call once the numbers are set).</summary>
        public static void LoadSaved(PawnRushSkillParams skills)
        {
            if (skills == null) return;
            if (skills.loadout == null) skills.loadout = PawnRushLoadout.Preset(0);
            skills.loadout.CopyFrom(PawnRushLoadout.Decode(PlayerPrefs.GetString(Pref, "")));
        }

        /// <summary>Keeps a loadout as this PC's own for the next play.</summary>
        public static void Save(PawnRushLoadout loadout)
        {
            if (loadout != null) PlayerPrefs.SetString(Pref, loadout.Encode());
        }

        void Update()
        {
            if (Open && Blocked != null && Blocked()) Toggle();
            if (LegacyKeys.Down(toggleKey) && !ChatBox.KeysHeld && !GameMenu.IsOpen && !SettingsWindow.IsOpen
                && (Open || Blocked == null || !Blocked())) Toggle();
            if (!OwnCursor) return;
            if (Open) TakeCursor();
            else GiveCursorBack();
        }

        void OnDisable()
        {
            if (Open) AnyOpen = false;
            GiveCursorBack();
        }

        public void Toggle()
        {
            Open = !Open;
            AnyOpen = Open;
            dragId = pickId = 0;
        }

        // The mouse is the window's while it is open (every frame: whatever locks the cursor under it would leave it
        // unclickable), and goes back as it was.
        void TakeCursor()
        {
            if (!cursorTaken)
            {
                cursorTaken = true;
                savedLock = Cursor.lockState;
                savedVisible = Cursor.visible;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameSettings.PointerTaken = true;
        }

        void GiveCursorBack()
        {
            if (!cursorTaken) return;
            cursorTaken = false;
            // The menu or the match sheet, opened over the window, holds the mouse on.
            if (GameMenu.IsOpen || VoiceHud.SheetOpen) return;
            GameSettings.PointerTaken = false;
            if (savedLock == CursorLockMode.Locked && !Application.isFocused) return;
            Cursor.lockState = savedLock;
            Cursor.visible = savedVisible;
        }

        bool Allowed(int op) => Skills != null && (CanEdit == null || CanEdit(op));

        void Say(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 2.5f;
        }

        /// <summary>One change: made here, or asked of the PC that runs the skills. False when it is not allowed.</summary>
        bool Change(int op, int value)
        {
            if (Skills == null) return false;
            if (!Allowed(op))
            {
                string why = LockedNote != null ? LockedNote(op) : "";
                Say(string.IsNullOrEmpty(why) ? "지금은 바꿀 수 없어요" : why);
                return false;
            }
            if (Send != null)
            {
                Send(op, value);   // the host's loadout coming back shows it
                return true;
            }
            if (!Skills.loadout.Apply(op, value)) return true;
            Save(Skills.loadout);
            Changed?.Invoke();
            return true;
        }

        void Put(int row, int slot, int id)
        {
            var entry = PawnRushSkillBook.Find(id);
            if (id != 0 && (entry == null || !entry.built))
            {
                Say("아직 칸에 넣을 수 없는 스킬이에요");
                return;
            }
            pickId = 0;
            if (!Change(row * PawnRushLoadout.Slots + slot, id)) return;
            if (entry != null && entry.Old && entry.home != PawnRushLoadout.RowPieces[row])
                Say($"'{entry.name}'은 {ChessPieces.Name(entry.home)}용으로 만든 스킬이라, 다른 기물에서는 이펙트와 소리가 안 나올 수 있어요");
        }

        void EnsureStyles()
        {
            if (label != null) return;
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 13);
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 13, richText = true, wordWrap = false, alignment = TextAnchor.MiddleLeft };
            label.normal.textColor = Ink;
            small = new GUIStyle(label) { fontSize = 11, wordWrap = true, alignment = TextAnchor.UpperLeft };
            small.normal.textColor = Faint;
            title = new GUIStyle(label) { fontSize = 16, fontStyle = FontStyle.Bold };
            cardName = new GUIStyle(label) { fontSize = 13, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip };
            center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            var symbols = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Symbol", "Malgun Gothic", "Arial Unicode MS" }, 34);
            glyph = new GUIStyle(GUI.skin.label) { font = symbols, fontSize = 34, alignment = TextAnchor.MiddleCenter };
            glyph.normal.textColor = Ink;
            tileName = new GUIStyle(label) { fontSize = 11, wordWrap = true, alignment = TextAnchor.UpperCenter, clipping = TextClipping.Clip };
            white = Texture2D.whiteTexture;
        }

        void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        void Frame(Rect r, Color c, float t = 1f)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        /// <summary>A button drawn by hand (the window takes its own mouse events for the drag).</summary>
        bool Chip(Rect r, string text, bool on, Color? dot = null)
        {
            Fill(r, on ? new Color(1f, 1f, 1f, 0.16f) : new Color(1f, 1f, 1f, 0.06f));
            Frame(r, on ? Lit : Edge);
            float x = r.x + 8f;
            if (dot.HasValue)
            {
                Fill(new Rect(x, r.y + r.height * 0.5f - 4f, 8f, 8f), dot.Value);
                x += 13f;
            }
            var old = label.normal.textColor;
            label.normal.textColor = on ? Lit : Ink;
            GUI.Label(new Rect(x, r.y, r.xMax - x - 4f, r.height), text, label);
            label.normal.textColor = old;
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                e.Use();
                return true;
            }
            return false;
        }

        void Dots(float x, float y, SkillTag tags)
        {
            foreach (var tag in PawnRushSkillBook.Tags)
            {
                if ((tags & tag) == 0) continue;
                Fill(new Rect(x, y, 8f, 8f), PawnRushSkillBook.TagColor(tag));
                x += 11f;
            }
        }

        static string Glyph(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.King: return "\u265A";
                case PieceKind.Queen: return "\u265B";
                case PieceKind.Rook: return "\u265C";
                case PieceKind.Bishop: return "\u265D";
                case PieceKind.Knight: return "\u265E";
                default: return "\u265F";
            }
        }

        static Color FirstTagColor(SkillTag tags)
        {
            foreach (var tag in PawnRushSkillBook.Tags)
                if ((tags & tag) != 0) return PawnRushSkillBook.TagColor(tag);
            return PawnRushSkillBook.TagColor(SkillTag.None);
        }

        /// <summary>A skill's square icon: its kind's colour, the piece it was made for, its kinds as dots; dim when it
        /// cannot be put in a slot yet.</summary>
        void DrawTile(Rect r, SkillEntry entry, bool picked, float alpha = 1f)
        {
            Color c = FirstTagColor(entry.tags);
            float k = entry.built ? 1f : 0.35f;
            Fill(r, new Color(c.r * 0.34f * k + 0.04f, c.g * 0.34f * k + 0.05f, c.b * 0.34f * k + 0.08f, alpha));
            var inner = new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f);
            Fill(inner, new Color(c.r * 0.6f * k, c.g * 0.6f * k, c.b * 0.6f * k, 0.55f * alpha));
            var old = glyph.normal.textColor;
            glyph.normal.textColor = new Color(1f, 1f, 1f, (entry.built ? 0.95f : 0.4f) * alpha);
            GUI.Label(r, Glyph(entry.home), glyph);
            glyph.normal.textColor = old;
            Dots(r.x + 5f, r.yMax - 12f, entry.tags);
            Frame(r, picked ? Lit : entry.built ? new Color(c.r, c.g, c.b, 0.9f * alpha) : Edge, picked ? 3f : 2f);
        }

        bool Shown(SkillEntry entry) =>
            (filter == SkillTag.None || (entry.tags & filter) != 0) && (originFilter < 0 || (int)entry.origin == originFilter);

        static string TagNames(SkillTag tags)
        {
            string names = "";
            foreach (var tag in PawnRushSkillBook.Tags)
                if ((tags & tag) != 0) names += (names.Length > 0 ? " · " : "") + PawnRushSkillBook.TagName(tag);
            return names;
        }

        void OnGUI()
        {
            if (!Open || Skills == null) return;
            if (Skills.loadout == null) Skills.loadout = PawnRushLoadout.Preset(0);
            EnsureStyles();
            GUI.depth = -20;
            var lo = Skills.loadout;
            var e = Event.current;

            float w = Mathf.Clamp(Screen.width - 120f, 760f, 1240f), h = Mathf.Clamp(Screen.height - 90f, 470f, 760f);
            var area = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Area = area;
            Fill(area, Panel);
            Frame(area, Edge);
            float x0 = area.x + 14f, y = area.y + 10f, right = area.xMax - 14f;

            GUI.Label(new Rect(x0, y, 320f, 24f), Title, title);
            var oldAlign = small.alignment;
            small.alignment = TextAnchor.MiddleRight;
            string hint = Allowed(0) ? $"끌어다 놓기 · 스킬을 누른 뒤 칸을 눌러도 됨 · {toggleKey} 닫기"
                : (LockedNote != null ? LockedNote(0) : "지금은 바꿀 수 없어요") + $" · {toggleKey} 닫기";
            GUI.Label(new Rect(right - 400f, y, 400f, 24f), hint, small);
            small.alignment = oldAlign;
            y += 30f;

            // The skill system, the loadouts that come with the game, and (a room) who may change it.
            GUI.Label(new Rect(x0, y, 64f, 24f), "스킬 방식", label);
            float x = x0 + 68f;
            string[] systems = { "1안 칸 하나", "2안 하나가 강해짐", "3안 칸 셋" };
            for (int i = 0; i < 3; i++)
            {
                float bw = i == 1 ? 126f : 92f;
                if (Chip(new Rect(x, y, bw, 24f), systems[i], lo.system == i + 1) && lo.system != i + 1) Change(PawnRushLoadout.OpSystem, i + 1);
                x += bw + 6f;
            }
            x += 10f;
            // (In the order shown; each one's number is PawnRushLoadout.Preset's.)
            string[] presets = { "지금 스킬", "1-나", "A 묶음", "기본기", "1안", "비우기" };
            int[] presetIds = { 0, 1, 3, 4, 5, 2 };
            for (int i = 0; i < presets.Length; i++)
            {
                if (Chip(new Rect(x, y, 58f, 24f), presets[i], false) && Change(PawnRushLoadout.OpPreset, presetIds[i]))
                    Say(presetIds[i] == 2 ? "모든 칸을 비웠어요"
                        : presetIds[i] == 4 ? "첫 칸을 기물별 한 벌의 기본기(오프닝)로 채웠어요. 킹도 한 걸음을 받아요"
                        : presetIds[i] == 5 ? "첫 칸을 후보 문서의 1안(기물당 하나)으로 채웠어요. 킹도 왕의 부름(앞으로)을 받아요"
                        : $"첫 칸을 '{presets[i]}' 구성으로 채웠어요");
                x += 62f;
            }
            if (ShowLock != null && ShowLock())
            {
                var lr = new Rect(right - 132f, y, 132f, 24f);
                if (Chip(lr, lo.hostOnly ? "방장만 바꾸기: 켬" : "방장만 바꾸기: 끔", lo.hostOnly)) Change(PawnRushLoadout.OpHostOnly, lo.hostOnly ? 0 : 1);
            }
            y += 32f;

            // The tags.
            x = x0;
            if (Chip(new Rect(x, y, 52f, 22f), "전체", filter == SkillTag.None)) { filter = SkillTag.None; scroll = 0f; }
            x += 58f;
            foreach (var tag in PawnRushSkillBook.Tags)
            {
                string name = PawnRushSkillBook.TagName(tag);
                float bw = 30f + name.Length * 12f;
                if (Chip(new Rect(x, y, bw, 22f), name, filter == tag, PawnRushSkillBook.TagColor(tag)))
                {
                    filter = filter == tag ? SkillTag.None : tag;
                    scroll = 0f;
                }
                x += bw + 6f;
            }
            y += 28f;

            // Where the skills come from.
            x = x0;
            if (Chip(new Rect(x, y, 76f, 22f), "출처 전체", originFilter < 0)) { originFilter = -1; scroll = 0f; }
            x += 82f;
            foreach (var origin in PawnRushSkillBook.Origins)
            {
                string name = PawnRushSkillBook.OriginName(origin);
                float bw = 18f + name.Length * 12f;
                if (Chip(new Rect(x, y, bw, 22f), name, originFilter == (int)origin))
                {
                    originFilter = originFilter == (int)origin ? -1 : (int)origin;
                    scroll = 0f;
                }
                x += bw + 6f;
            }
            y += 30f;

            // Middle: the skills as square icons, a row group for each place they come from, scrolled by the wheel.
            // Right: every piece's slots as square sockets. Under the skills: what the one under the mouse does.
            const float tile = 62f, gapX = 10f, cellH = tile + 30f, detailH = 86f, boardW = 330f;
            float listW = area.width - 28f - boardW - 16f;
            float listX = x0, boardX = x0 + listW + 16f, top = y;
            var view = new Rect(listX, top, listW, area.yMax - 34f - detailH - top);
            int cols = Mathf.Max(1, Mathf.FloorToInt((listW - 8f + gapX) / (tile + gapX)));
            // (Lay the groups out once to know the height.)
            float content = 0f;
            foreach (var origin in PawnRushSkillBook.Origins)
            {
                int n = 0;
                foreach (var entry in PawnRushSkillBook.All) if (entry.origin == origin && Shown(entry)) n++;
                if (n > 0) content += 24f + Mathf.Ceil(n / (float)cols) * cellH + 6f;
            }
            float maxScroll = Mathf.Max(0f, content - view.height);
            if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition))
            {
                scroll += e.delta.y * 24f;
                e.Use();
            }
            scroll = Mathf.Clamp(scroll, 0f, maxScroll);
            bool mouseInView = view.Contains(e.mousePosition);
            int hovered = 0;
            GUI.BeginGroup(view);
            float gy = -scroll;
            foreach (var origin in PawnRushSkillBook.Origins)
            {
                int n = 0, slotable = 0;
                foreach (var entry in PawnRushSkillBook.All)
                    if (entry.origin == origin && Shown(entry)) { n++; if (entry.built) slotable++; }
                if (n == 0) continue;
                GUI.Label(new Rect(0f, gy, listW, 20f), $"<b>{PawnRushSkillBook.OriginName(origin)}</b>  <size=11><color=#9EA9BD>{n}개 · 넣을 수 있는 것 {slotable}개</color></size>", label);
                Fill(new Rect(0f, gy + 21f, listW - 8f, 1f), Edge);
                gy += 24f;
                int i = 0;
                foreach (var entry in PawnRushSkillBook.All)
                {
                    if (entry.origin != origin || !Shown(entry)) continue;
                    var r = new Rect((i % cols) * (tile + gapX), gy + (i / cols) * cellH, tile, tile);
                    i++;
                    if (r.yMax + 28f < 0f || r.y > view.height) continue;
                    DrawTile(r, entry, pickId == entry.id);
                    var oldName = tileName.normal.textColor;
                    tileName.normal.textColor = entry.built ? Ink : Faint;
                    GUI.Label(new Rect(r.x - 4f, r.yMax + 1f, tile + 8f, 28f), entry.name, tileName);
                    tileName.normal.textColor = oldName;
                    if (!mouseInView || !r.Contains(e.mousePosition)) continue;
                    hovered = entry.id;
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        if (!entry.built) Say("아직 칸에 넣을 수 없는 스킬이에요");
                        else
                        {
                            dragId = entry.id;
                            pickId = pickId == entry.id ? 0 : entry.id;
                        }
                        e.Use();
                    }
                }
                gy += Mathf.Ceil(n / (float)cols) * cellH + 6f;
            }
            GUI.EndGroup();
            if (e.type == EventType.Repaint) hoverId = hovered;
            if (maxScroll > 0f)
            {
                float barH = Mathf.Max(24f, view.height * view.height / content);
                Fill(new Rect(view.xMax - 4f, view.y + (view.height - barH) * (scroll / maxScroll), 3f, barH), Edge);
            }

            // What the skill under the mouse (or the one picked) does.
            var about = PawnRushSkillBook.Find(hoverId != 0 ? hoverId : pickId);
            var detail = new Rect(listX, view.yMax + 6f, listW, detailH);
            Fill(detail, SlotEmpty);
            Frame(detail, Edge);
            if (about != null)
            {
                DrawTile(new Rect(detail.x + 8f, detail.y + 8f, 44f, 44f), about, false);
                GUI.Label(new Rect(detail.x + 62f, detail.y + 3f, detail.width - 70f, 20f),
                    $"<b>{about.name}</b>  <size=11><color=#9EA9BD>{ChessPieces.Name(about.home)} · {PawnRushSkillBook.OriginName(about.origin)} · {TagNames(about.tags)}{(about.built ? "" : " · 아직 못 넣음")}</color></size>", label);
                GUI.Label(new Rect(detail.x + 62f, detail.y + 24f, detail.width - 70f, detail.height - 26f), about.how, small);
            }
            else GUI.Label(new Rect(detail.x + 8f, detail.y + 6f, detail.width - 16f, 40f), "스킬 아이콘에 마우스를 올리면 여기에 설명이 나와요. 아이콘을 끌어서 오른쪽 칸에 놓으세요.", small);

            GUI.Label(new Rect(boardX, top, boardW, 20f), "<b>기물별 칸</b>", label);
            // Six rows (the king's is the last): they share the height there is under the heading.
            float rowsRoom = area.yMax - 96f - (top + 28f);
            float by = top + 28f, sock = Mathf.Clamp(rowsRoom / PawnRushLoadout.Rows, 70f, 96f) - 34f;
            for (int row = 0; row < PawnRushLoadout.Rows; row++)
            {
                var kind = PawnRushLoadout.RowPieces[row];
                var old2 = glyph.normal.textColor;
                glyph.normal.textColor = Ink;
                GUI.Label(new Rect(boardX, by, 40f, sock), Glyph(kind), glyph);
                glyph.normal.textColor = old2;
                GUI.Label(new Rect(boardX - 2f, by + sock - 4f, 44f, 16f), ChessPieces.Name(kind), tileName);
                float sx = boardX + 50f;
                for (int k = 0; k < lo.SlotCount; k++)
                {
                    var r = new Rect(sx, by, sock, sock);
                    int id = lo.Raw(row, k);
                    var entry = PawnRushSkillBook.Find(id);
                    bool over = dragId != 0 && r.Contains(e.mousePosition);
                    string key = KeyName != null ? KeyName(k) : k == 0 ? "F" : k == 1 ? "E" : "Q";
                    if (entry != null)
                    {
                        DrawTile(r, entry, over);
                        GUI.Label(new Rect(r.x - 6f, r.yMax + 1f, sock + 12f, 28f), entry.name, tileName);
                        if (r.Contains(e.mousePosition) && e.type == EventType.Repaint && dragId == 0) hoverId = entry.id;
                        var cross = new Rect(r.xMax - 17f, r.y + 1f, 16f, 16f);
                        Fill(cross, new Color(0f, 0f, 0f, 0.55f));
                        GUI.Label(cross, "×", center);
                        if (e.type == EventType.MouseDown && e.button == 0 && cross.Contains(e.mousePosition))
                        {
                            Put(row, k, 0);
                            e.Use();
                        }
                    }
                    else
                    {
                        Fill(r, SlotEmpty);
                        Frame(r, over ? Lit : Edge, over ? 3f : 1f);
                    }
                    // The slot's key, on a small plate at its foot.
                    var plate = new Rect(r.x + 2f, r.yMax - 16f, 26f, 14f);
                    if (entry == null) plate = new Rect(r.x, r.y, r.width, r.height);
                    else Fill(plate, new Color(0f, 0f, 0f, 0.6f));
                    GUI.Label(plate, key, center);
                    // A skill let go over the slot, or one picked and the slot clicked.
                    if (e.type == EventType.MouseUp && e.button == 0 && dragId != 0 && r.Contains(e.mousePosition))
                    {
                        Put(row, k, dragId);
                        dragId = 0;
                        e.Use();
                    }
                    else if (e.type == EventType.MouseDown && e.button == 0 && pickId != 0 && r.Contains(e.mousePosition))
                    {
                        Put(row, k, pickId);
                        e.Use();
                    }
                    sx += sock + 14f;
                }
                // The king's row left empty: he plays his call, as he always did.
                if (kind == PieceKind.King && !lo.KingSlots && lo.system != PawnRushLoadout.Grow)
                    GUI.Label(new Rect(sx + 2f, by + sock * 0.5f - 10f, 130f, 20f), "비우면 왕의 부름", small);
                if (lo.system == PawnRushLoadout.Grow)
                {
                    // The growing system: the stage, set by hand for now.
                    GUI.Label(new Rect(sx + 4f, by, 34f, 20f), "단계", small);
                    for (int stage = 1; stage <= 3; stage++)
                        if (Chip(new Rect(sx + (stage - 1) * 34f, by + 22f, 30f, 24f), " " + stage, lo.stages[row] == stage) && lo.stages[row] != stage)
                            Change(PawnRushLoadout.OpStage + row, stage);
                }
                by += sock + 34f;
            }
            string note = lo.system == PawnRushLoadout.One ? "1안: 기물마다 스킬 하나 (스킬 키)."
                : lo.system == PawnRushLoadout.Grow ? "2안: 스킬 하나가 단계마다 강해져요. 지금은 단계를 여기서 직접 올려요 (임시)."
                : "3안: 기물마다 세 칸. 한 번에 스킬 하나만 돌고, 쿨타임은 칸마다 따로예요.";
            GUI.Label(new Rect(boardX, by, boardW, 32f), note, small);
            if (ShowLock != null && ShowLock())
                GUI.Label(new Rect(boardX, by + 32f, boardW, 46f), lo.hostOnly
                    ? "방: 방장만 바꿀 수 있어요. 다른 사람은 보기만 해요."
                    : "방: 누구나 바꿀 수 있고, 바꾸면 모두에게 똑같이 적용돼요.", small);

            if (Time.unscaledTime < messageUntil)
            {
                var m = new Rect(x0, area.yMax - 28f, area.width - 28f, 22f);
                var old = label.normal.textColor;
                label.normal.textColor = new Color(1f, 0.62f, 0.5f);
                GUI.Label(m, message, label);
                label.normal.textColor = old;
            }

            // The skill being dragged, under the mouse.
            if (dragId != 0)
            {
                var entry = PawnRushSkillBook.Find(dragId);
                if (entry != null && e.type == EventType.Repaint)
                    DrawTile(new Rect(e.mousePosition.x - 31f, e.mousePosition.y - 31f, 62f, 62f), entry, true, 0.9f);
                if (e.type == EventType.MouseUp) dragId = 0;
            }
            // The window's own clicks go no further.
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseUp) && area.Contains(e.mousePosition)) e.Use();
        }
    }
}
