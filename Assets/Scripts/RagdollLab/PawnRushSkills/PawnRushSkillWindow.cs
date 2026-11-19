using System;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The developers' skill window (JY-skill-lab, 2026-11-19; the user: "스킬 창을 따로 만들고, 거기에 각 칸에 드래그 앤 드롭으로
    /// 스킬들이 ... 자유롭게 넣었다 뺐다 할 수 있는 거야. 막 바꿔보면서 테스트할 수 있게 ... 개발자용 테스트", "창은 k키 같은 걸로
    /// 열리게", "아무 기물에다 넣을 수 있게"). K opens it. On the left the skill book (PawnRushSkillBook), each skill with the
    /// colours of its tags and filtered by tag; on the right every piece's slots. A skill is dragged onto a slot (or
    /// clicked, then the slot clicked), any skill on any piece; the cross on a slot empties it. The skill system at the
    /// top: 1 a slot a piece, 2 the same with a stage 1..3 set here by hand, 3 three slots a piece (the skill key and the
    /// two ability keys). What it edits is PawnRushSkillParams.loadout, kept on this PC between plays.
    ///
    /// For now it edits only where this PC runs the skills itself (offline); a room still plays one of the two loadouts
    /// its rules name. Everyone opening it and the room's manager locking it come with the loadout going over the network.
    /// </summary>
    [DefaultExecutionOrder(260)]
    public class PawnRushSkillWindow : MonoBehaviour
    {
        public KeyCode toggleKey = KeyCode.K;

        /// <summary>The numbers whose loadout this window edits.</summary>
        public PawnRushSkillParams Skills;
        /// <summary>False: the window shows the loadout but changes nothing (with <see cref="LockedNote"/> saying why).</summary>
        public Func<bool> CanEdit;
        public string LockedNote = "";
        /// <summary>The loadout was changed here.</summary>
        public Action Changed;
        /// <summary>The names of the three slots' keys, as this scene binds them.</summary>
        public Func<int, string> KeyName;

        public bool Open { get; private set; }
        /// <summary>The window's place on the screen while it is open (for the cursor: clicks on it are its own).</summary>
        public Rect Area { get; private set; }

        const string Pref = "ChessFight.PawnRushLoadout";

        SkillTag filter = SkillTag.None;
        int dragId, pickId;
        string message = "";
        float messageUntil;
        GUIStyle label, small, title, cardName, center;
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

        void Update()
        {
            if (Input.GetKeyDown(toggleKey) && !ChessFight.Game.ChatBox.KeysHeld) Toggle();
        }

        public void Toggle()
        {
            Open = !Open;
            dragId = pickId = 0;
        }

        bool Editable => Skills != null && (CanEdit == null || CanEdit());

        void Say(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 2.5f;
        }

        void Apply()
        {
            PlayerPrefs.SetString(Pref, Skills.loadout.Encode());
            Changed?.Invoke();
        }

        void Put(int row, int slot, int id)
        {
            if (!Editable)
            {
                Say(string.IsNullOrEmpty(LockedNote) ? "지금은 바꿀 수 없어요" : LockedNote);
                return;
            }
            var entry = PawnRushSkillBook.Find(id);
            if (id != 0 && (entry == null || !entry.built))
            {
                Say("아직 안 만든 스킬이에요");
                return;
            }
            Skills.loadout.Set(row, slot, id);
            pickId = 0;
            if (entry != null && entry.Old && entry.home != PawnRushLoadout.RowPieces[row])
                Say($"'{entry.name}'은 {ChessPieces.Name(entry.home)}용으로 만든 스킬이라, 다른 기물에서는 이펙트와 소리가 안 나올 수 있어요");
            Apply();
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

        void DrawCard(Rect r, SkillEntry entry, bool picked, bool ghost)
        {
            Fill(r, entry.built ? (ghost ? new Color(0.2f, 0.25f, 0.36f, 0.92f) : Card) : CardDim);
            Frame(r, picked ? Lit : Edge, picked ? 2f : 1f);
            var old = cardName.normal.textColor;
            cardName.normal.textColor = entry.built ? Ink : Faint;
            GUI.Label(new Rect(r.x + 7f, r.y + 2f, r.width - 10f, 20f), entry.name, cardName);
            cardName.normal.textColor = old;
            Dots(r.x + 8f, r.y + 26f, entry.tags);
            string under = ChessPieces.Name(entry.home) + (entry.built ? "" : " · 아직");
            var oldAlign = small.alignment;
            small.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(r.x + 4f, r.y + 20f, r.width - 10f, 18f), under, small);
            small.alignment = oldAlign;
        }

        void OnGUI()
        {
            if (!Open || Skills == null) return;
            if (Skills.loadout == null) Skills.loadout = PawnRushLoadout.Preset(0);
            EnsureStyles();
            GUI.depth = -20;
            var lo = Skills.loadout;
            var e = Event.current;

            float w = Mathf.Clamp(Screen.width - 400f, 640f, 900f), h = Mathf.Clamp(Screen.height - 110f, 430f, 640f);
            var area = new Rect(Mathf.Max(8f, Screen.width - w - 12f), 44f, w, h);
            Area = area;
            Fill(area, Panel);
            Frame(area, Edge);
            float x0 = area.x + 14f, y = area.y + 10f, right = area.xMax - 14f;

            GUI.Label(new Rect(x0, y, 320f, 24f), "스킬 창 (개발자용)", title);
            var oldAlign = small.alignment;
            small.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(right - 330f, y, 330f, 24f), Editable ? $"끌어다 놓기 · 스킬을 누른 뒤 칸을 눌러도 됨 · {toggleKey} 닫기" : LockedNote, small);
            small.alignment = oldAlign;
            y += 30f;

            // The skill system, and the loadouts that come with the game.
            GUI.Label(new Rect(x0, y, 64f, 24f), "스킬 방식", label);
            float x = x0 + 68f;
            string[] systems = { "1안 칸 하나", "2안 하나가 강해짐", "3안 칸 셋" };
            for (int i = 0; i < 3; i++)
            {
                float bw = i == 1 ? 126f : 92f;
                if (Chip(new Rect(x, y, bw, 24f), systems[i], lo.system == i + 1) && Editable && lo.system != i + 1)
                {
                    lo.system = i + 1;
                    Apply();
                }
                x += bw + 6f;
            }
            x += 14f;
            string[] presets = { "지금 스킬", "1-나", "비우기" };
            for (int i = 0; i < 3; i++)
            {
                if (Chip(new Rect(x, y, 70f, 24f), presets[i], false) && Editable)
                {
                    int system = lo.system;
                    lo.CopyFrom(PawnRushLoadout.Preset(i));
                    lo.system = system;
                    Say(i == 2 ? "모든 칸을 비웠어요" : $"첫 칸을 '{presets[i]}' 구성으로 채웠어요");
                    Apply();
                }
                x += 76f;
            }
            y += 32f;

            // The tags.
            x = x0;
            if (Chip(new Rect(x, y, 52f, 22f), "전체", filter == SkillTag.None)) filter = SkillTag.None;
            x += 58f;
            foreach (var tag in PawnRushSkillBook.Tags)
            {
                string name = PawnRushSkillBook.TagName(tag);
                float bw = 30f + name.Length * 12f;
                if (Chip(new Rect(x, y, bw, 22f), name, filter == tag, PawnRushSkillBook.TagColor(tag))) filter = filter == tag ? SkillTag.None : tag;
                x += bw + 6f;
            }
            y += 30f;

            // Left: the book. Right: the slots.
            float boardW = 356f, listW = area.width - 28f - boardW - 14f;
            float listX = x0, boardX = x0 + listW + 14f, top = y;
            int made = 0, shown = 0;
            foreach (var entry in PawnRushSkillBook.All) if (entry.built) made++;
            int cols = Mathf.Max(1, Mathf.FloorToInt((listW + 6f) / 150f));
            float cw = (listW - (cols - 1) * 6f) / cols, ch = 40f;
            float ly = top + 20f;
            foreach (var entry in PawnRushSkillBook.All)
            {
                if (filter != SkillTag.None && (entry.tags & filter) == 0) continue;
                var r = new Rect(listX + (shown % cols) * (cw + 6f), ly + (shown / cols) * (ch + 6f), cw, ch);
                shown++;
                if (r.yMax > area.yMax - 30f) continue;
                DrawCard(r, entry, pickId == entry.id, false);
                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
                {
                    if (!entry.built) Say("아직 안 만든 스킬이에요");
                    else
                    {
                        dragId = entry.id;
                        pickId = pickId == entry.id ? 0 : entry.id;
                    }
                    e.Use();
                }
            }
            GUI.Label(new Rect(listX, top, listW, 18f), $"스킬 목록 {shown}개 (만든 것 {made}개) · 색 점 = 종류", small);

            GUI.Label(new Rect(boardX, top, boardW, 18f), "기물별 칸", small);
            float by = top + 20f, sw = lo.system == PawnRushLoadout.Many ? 96f : 150f, sh = 46f;
            for (int row = 0; row < PawnRushLoadout.Rows; row++)
            {
                var kind = PawnRushLoadout.RowPieces[row];
                GUI.Label(new Rect(boardX, by, 52f, sh), ChessPieces.Name(kind), label);
                float sx = boardX + 54f;
                for (int k = 0; k < lo.SlotCount; k++)
                {
                    var r = new Rect(sx, by, sw, sh);
                    int id = lo.Raw(row, k);
                    var entry = PawnRushSkillBook.Find(id);
                    bool over = dragId != 0 && r.Contains(e.mousePosition);
                    Fill(r, entry != null ? Card : SlotEmpty);
                    Frame(r, over ? Lit : Edge, over ? 2f : 1f);
                    string key = KeyName != null ? KeyName(k) : k == 0 ? "F" : k == 1 ? "E" : "Q";
                    if (entry != null)
                    {
                        GUI.Label(new Rect(r.x + 6f, r.y + 2f, r.width - 22f, 20f), entry.name, cardName);
                        Dots(r.x + 7f, r.y + 28f, entry.tags);
                        var a = small.alignment;
                        small.alignment = TextAnchor.MiddleRight;
                        GUI.Label(new Rect(r.x, r.y + 22f, r.width - 6f, 18f), key, small);
                        small.alignment = a;
                        var cross = new Rect(r.xMax - 18f, r.y + 2f, 16f, 16f);
                        GUI.Label(cross, "×", center);
                        if (e.type == EventType.MouseDown && e.button == 0 && cross.Contains(e.mousePosition))
                        {
                            Put(row, k, 0);
                            e.Use();
                        }
                    }
                    else GUI.Label(r, key, center);
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
                    sx += sw + 6f;
                }
                if (lo.system == PawnRushLoadout.Grow)
                {
                    // The growing system: the stage, set by hand for now.
                    GUI.Label(new Rect(sx + 4f, by, 34f, sh), "단계", small);
                    for (int stage = 1; stage <= 3; stage++)
                        if (Chip(new Rect(sx + 34f + (stage - 1) * 34f, by + 10f, 30f, 24f), " " + stage, lo.stages[row] == stage) && Editable && lo.stages[row] != stage)
                        {
                            lo.SetStage(row, stage);
                            Apply();
                        }
                }
                by += sh + 8f;
            }
            string note = lo.system == PawnRushLoadout.One ? "1안: 기물마다 스킬 하나 (스킬 키)."
                : lo.system == PawnRushLoadout.Grow ? "2안: 스킬 하나가 단계마다 강해져요. 지금은 단계를 여기서 직접 올리고, 쿨타임이 줄고 미는 힘이 세져요 (임시)."
                : "3안: 기물마다 세 칸. 한 번에 스킬 하나만 돌고, 쿨타임은 칸마다 따로예요.";
            GUI.Label(new Rect(boardX, by, boardW, 34f), note, small);
            GUI.Label(new Rect(boardX, by + 36f, boardW, 34f), "구성을 바꾸면 쓰던 스킬과 쿨타임이 처음으로 돌아가요.", small);

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
                    DrawCard(new Rect(e.mousePosition.x - 60f, e.mousePosition.y - 18f, 130f, 40f), entry, true, true);
                if (e.type == EventType.MouseUp) dragId = 0;
            }
            // The window's own clicks go no further.
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseUp) && area.Contains(e.mousePosition)) e.Use();
        }
    }
}
