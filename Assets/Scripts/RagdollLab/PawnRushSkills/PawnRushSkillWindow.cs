using System;
using System.Collections.Generic;
using System.Reflection;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 개발자 폰러쉬 스킬트리 - the developers' skill tree (JY-skill-lab, 2026-11-19; the user: "스킬 창을 따로 만들고, 거기에 각 칸에
    /// 드래그 앤 드롭으로 스킬들이 ... 자유롭게 넣었다 뺐다 할 수 있는 거야. 막 바꿔보면서 테스트할 수 있게 ... 개발자용 테스트",
    /// "창은 k키 같은 걸로 열리게", "아무 기물에다 넣을 수 있게", and its name: "이걸 '개발자 폰러쉬 스킬트리'라 이름짓고"). K opens
    /// it. What it edits is PawnRushSkillParams.loadout, kept on this PC between plays.
    ///
    /// Laid out again on 2026-11-24 (the user: "스킬트리 UI좀 직관적으로 만들어줘. 지금은 너무 난잡하고 뭐가 뭔지 모르겠어"):
    ///   - the sets on one line, the one in use ticked and told in a line under it (a set under the mouse is told there
    ///     instead); the earlier ones behind "옛 세트"; the skill system goes with the set and hides behind its own switch;
    ///   - on the left every piece's slots as cards with the skill's name and one line, under their keys (F, E, R, Q); the
    ///     king's row only while it holds a skill (no king in Pawn Rush);
    ///   - on the right the skills as a list, a name and one line each: the ones on the board first (with where they are),
    ///     then by where they come from; a tab a piece; the ones that are only plans behind a switch; under it, what the
    ///     skill under the mouse does.
    /// Click a slot, then a skill: it goes in, and the slot stays picked to try the next. Dragging a skill onto a slot, or
    /// clicking a skill and then a slot, still works; the cross on a slot under the mouse empties it. The tags' colours and
    /// the two rows of filters are gone (the tags are words in the description).
    ///
    /// Every skill has a picture of what it does (the user, the same day: "스킬 이름만 나오지 말고 이미지도 같이", "스킬이 뭔지
    /// 한눈에 보여주는 이미지"): on its slot card, in the list and in the description. They are drawn by Tools/SkillIcons
    /// into Resources/PawnRushSkillIcons, one a PawnRushSkillIds constant's name (a variant shows its base's; a skill that
    /// is only a plan, a question mark).
    ///
    /// Two tabs since 2026-11-26 (the user: "지금부터 작업하는 스킬들이 실제로 게임에 넣는 걸 고려해 볼 후보들이니까 이전에 있던건
    /// '폐기된 스킬'탭을 만들어서 거기로 넘기고, 이것들만 바로 보이도록 해야돼"): "4판 후보" shows only the set 4판's thirty-five
    /// (Docs/Skills/PAWN_RUSH_SET4_ROUND1.md) by piece - the ones built first, the rest faint as "준비 중" - with the 4판's
    /// sets; "폐기된 스킬" holds every skill and set from before, still to be put in any slot. The same day the rules the
    /// user settled (Docs/Skills/PAWN_RUSH_SKILL_SET4.md 1.1: "각 캐릭터당 스킬 슬롯은 2개고", grades mixed freely in them,
    /// "초급,중급만 넣어서 빠르게 스킬을 돌려쓰는 대신 각각의 위력은 약하게 할 수도 있고, 중급-상급이나 초급-상급"): the 4판
    /// tab shows a piece's two slots (F, E; R and Q only while they hold something), every 4판 skill carries its grade
    /// and cooldown in its colour (초급 green, 중급 blue, 상급 violet), and under each piece's name its two grades.
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
        /// <summary>The names of the slots' keys, as this scene binds them.</summary>
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

        // The 4판 tab's sets (11-26); under "폐기된 스킬" the sets before them, newest first, and the earliest behind "옛 세트"
        // (PawnRushLoadout.Preset's numbers; 2 is "모두 비우기").
        static readonly int[] NewIds = { 13 };
        static readonly string[] NewNames = { "4판 가" };
        static readonly int[] SetIds = { 12, 11, 8, 10, 9, 0 };
        static readonly string[] SetNames = { "3판", "3안-b", "3안-a", "2안-a", "1안-a", "지금 게임" };
        static readonly int[] OldIds = { 1, 3, 4, 5, 7, 6 };
        static readonly string[] OldNames = { "1-나", "A 묶음", "기본기", "옛 1안", "옛 2안", "옛 3안" };
        // The list's groups after the skills on the board, in this order.
        static readonly SkillOrigin[] GroupOrder =
            { SkillOrigin.Kits, SkillOrigin.Pool, SkillOrigin.PawnRush, SkillOrigin.IdeaBook, SkillOrigin.QueenHill, SkillOrigin.SwordFight, SkillOrigin.Plan2 };

        int selRow = -1, selSlot = -1;   // the slot picked on the board: a skill clicked in the list goes there
        int listPiece = -1;              // the list's tab: a board row (its piece), or -1 for every piece
        int dragId, pickId, hoverId, hoverNow, hoverSet = -1, hoverSetNow = -1;
        float scroll;
        bool showOld, showSystems, showIdeas;
        bool discarded;                  // the "폐기된 스킬" tab (else "4판 후보")
        int ideasHidden;
        // The set the slots are now (-1: changed by hand), and where each skill sits, kept until the loadout changes.
        int activeSet = -1, activeHash = int.MinValue;
        readonly Dictionary<int, string> placedAt = new Dictionary<int, string>();
        readonly Dictionary<int, int> placedRows = new Dictionary<int, int>();
        string message = "";
        bool messageWarn;
        float messageUntil;
        bool cursorTaken, savedVisible;
        CursorLockMode savedLock;
        GUIStyle label, small, smallOne, smallRight, title, cardName, cardNameWrap, pieceName, center, emptyText, keyHead, glyph, glyphList;
        GUIContent measure;
        Texture2D white;
        readonly Dictionary<(string, int, int), string> fitted = new Dictionary<(string, int, int), string>();
        readonly int[] colIndex = new int[PawnRushLoadout.Slots];
        readonly float[] colX = new float[PawnRushLoadout.Slots], colW = new float[PawnRushLoadout.Slots];
        readonly int[] rowIndex = new int[PawnRushLoadout.Rows];

        // The list's lines this frame: a group's heading (entry null) or a skill.
        struct Line
        {
            public SkillEntry entry;
            public string head;
        }

        readonly List<Line> lines = new List<Line>();
        readonly HashSet<int> listed = new HashSet<int>();

        const float HeadH = 24f, SkillH = 50f;

        // The skills' pictures (Tools/SkillIcons), by skill number, loaded when first shown.
        const string IconFolder = "PawnRushSkillIcons/";
        static readonly Dictionary<int, Texture2D> icons = new Dictionary<int, Texture2D>();
        static Dictionary<int, string> idNames;
        static Texture2D unknownIcon;
        static bool unknownTried;

        static readonly Color Panel = new Color(0.05f, 0.07f, 0.12f, 0.96f), Card = new Color(0.14f, 0.17f, 0.25f, 1f),
            CardHover = new Color(0.2f, 0.24f, 0.34f, 1f), CardEdge = new Color(1f, 1f, 1f, 0.2f), Edge = new Color(1f, 1f, 1f, 0.14f),
            Lit = new Color(1f, 0.85f, 0.3f, 1f), PickFill = new Color(1f, 0.85f, 0.3f, 0.14f), SlotEmpty = new Color(1f, 1f, 1f, 0.04f),
            RowTint = new Color(1f, 1f, 1f, 0.025f), Ink = new Color(0.95f, 0.96f, 0.98f), Faint = new Color(0.62f, 0.66f, 0.74f),
            Warn = new Color(1f, 0.62f, 0.5f), Good = new Color(0.62f, 0.9f, 0.68f);

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

        void Say(string text, bool warn = true)
        {
            message = text;
            messageWarn = warn;
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
                Say("아직 칸에 넣을 수 없는 스킬이에요 (계획만 있어요)");
                return;
            }
            pickId = 0;
            if (!Change(row * PawnRushLoadout.Slots + slot, id)) return;
            var piece = PawnRushLoadout.RowPieces[row];
            string where = $"{ChessPieces.Name(piece)} {KeyOf(slot)}";
            if (entry == null) Say($"{where} 칸을 비웠어요", false);
            else if (entry.Old && entry.home != piece)
                Say($"{where} ← {entry.name}. {ChessPieces.Name(entry.home)}용으로 만든 옛 스킬이라 이펙트와 소리가 안 나올 수 있어요");
            else Say($"{where} ← {entry.name}", false);
        }

        // ---------------------------------------------------------------- drawing parts

        void EnsureStyles()
        {
            if (label != null) return;
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 13);
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 13, richText = true, wordWrap = false, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            label.normal.textColor = Ink;
            small = new GUIStyle(label) { fontSize = 11, wordWrap = true, alignment = TextAnchor.UpperLeft };
            small.normal.textColor = Faint;
            smallOne = new GUIStyle(small) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
            smallRight = new GUIStyle(smallOne) { alignment = TextAnchor.MiddleRight };
            title = new GUIStyle(label) { fontSize = 17, fontStyle = FontStyle.Bold };
            cardName = new GUIStyle(label) { fontSize = 13, fontStyle = FontStyle.Bold };
            cardNameWrap = new GUIStyle(cardName) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            pieceName = new GUIStyle(cardName) { fontSize = 14 };
            center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            emptyText = new GUIStyle(smallOne) { alignment = TextAnchor.MiddleCenter };
            keyHead = new GUIStyle(label) { fontSize = 18, fontStyle = FontStyle.Bold };
            keyHead.normal.textColor = Lit;
            var symbols = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Symbol", "Malgun Gothic", "Arial Unicode MS" }, 34);
            glyph = new GUIStyle(GUI.skin.label) { font = symbols, fontSize = 28, alignment = TextAnchor.MiddleCenter };
            glyph.normal.textColor = Ink;
            glyphList = new GUIStyle(glyph) { fontSize = 22 };
            measure = new GUIContent();
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

        float TextW(string text, GUIStyle style)
        {
            measure.text = text;
            return style.CalcSize(measure).x;
        }

        /// <summary>The text as it fits <paramref name="width"/> in <paramref name="style"/>, cut with "…" (kept: every frame
        /// asks for the same ones).</summary>
        string Fit(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text) || width <= 8f) return "";
            var key = (text, Mathf.RoundToInt(width), style.fontSize * 2 + (style.fontStyle == FontStyle.Bold ? 1 : 0));
            if (fitted.TryGetValue(key, out var done)) return done;
            string result = text;
            if (TextW(text, style) > width)
            {
                int lo = 0, hi = text.Length - 1;
                while (lo < hi)
                {
                    int mid = (lo + hi + 1) / 2;
                    if (TextW(text.Substring(0, mid) + "…", style) <= width) lo = mid;
                    else hi = mid - 1;
                }
                result = text.Substring(0, lo).TrimEnd() + "…";
            }
            if (fitted.Count > 4000) fitted.Clear();
            fitted[key] = result;
            return result;
        }

        /// <summary>A button drawn by hand (the window takes its own mouse events for the drag).</summary>
        bool Chip(Rect r, string text, bool on)
        {
            bool mouse = r.Contains(Event.current.mousePosition);
            Fill(r, on ? new Color(1f, 0.85f, 0.3f, 0.18f) : mouse ? new Color(1f, 1f, 1f, 0.11f) : new Color(1f, 1f, 1f, 0.05f));
            Frame(r, on ? Lit : Edge);
            var old = label.normal.textColor;
            label.normal.textColor = on ? Lit : Ink;
            var oldAlign = label.alignment;
            label.alignment = TextAnchor.MiddleCenter;
            GUI.Label(r, text, label);
            label.alignment = oldAlign;
            label.normal.textColor = old;
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && mouse)
            {
                e.Use();
                return true;
            }
            return false;
        }

        static string Glyph(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.King: return "♚";
                case PieceKind.Queen: return "♛";
                case PieceKind.Rook: return "♜";
                case PieceKind.Bishop: return "♝";
                case PieceKind.Knight: return "♞";
                default: return "♟";
            }
        }

        string KeyOf(int slot) => KeyName != null ? KeyName(slot) : slot == 0 ? "F" : slot == 1 ? "E" : slot == 2 ? "R" : "Q";

        static Texture2D UnknownIcon()
        {
            if (!unknownTried)
            {
                unknownTried = true;
                unknownIcon = Resources.Load<Texture2D>(IconFolder + "Unknown");
            }
            return unknownIcon;
        }

        /// <summary>A skill's picture: the one named after its PawnRushSkillIds constant, else its base skill's (a set's
        /// variant), else the question mark.</summary>
        static Texture2D IconOf(int id)
        {
            if (icons.TryGetValue(id, out var tex)) return tex;
            if (idNames == null)
            {
                idNames = new Dictionary<int, string>();
                foreach (var field in typeof(PawnRushSkillIds).GetFields(BindingFlags.Public | BindingFlags.Static))
                    if (field.IsLiteral && field.FieldType == typeof(int)) idNames[(int)field.GetRawConstantValue()] = field.Name;
            }
            if (idNames.TryGetValue(id, out string name)) tex = Resources.Load<Texture2D>(IconFolder + name);
            int baseId = PawnRushSkillIds.BaseOf(id);
            if (tex == null && baseId != id && idNames.TryGetValue(baseId, out string baseName))
                tex = Resources.Load<Texture2D>(IconFolder + baseName);
            if (tex == null) tex = UnknownIcon();
            icons[id] = tex;
            return tex;
        }

        /// <summary>The skill's picture in <paramref name="r"/> (a skill that is only a plan: the question mark; no
        /// pictures at all: its piece's glyph).</summary>
        void DrawIcon(Rect r, SkillEntry entry, float alpha = 1f)
        {
            var tex = entry.built ? IconOf(entry.id) : UnknownIcon();
            var old = GUI.color;
            GUI.color = new Color(old.r, old.g, old.b, old.a * alpha);
            if (tex != null) GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
            else GUI.Label(r, Glyph(entry.home), r.height > 34f ? glyph : glyphList);
            GUI.color = old;
        }

        static string TagNames(SkillTag tags)
        {
            string names = "";
            foreach (var tag in PawnRushSkillBook.Tags)
                if ((tags & tag) != 0) names += (names.Length > 0 ? " · " : "") + PawnRushSkillBook.TagName(tag);
            return names;
        }

        // ---------------------------------------------------------------- the sets

        static string SetName(int id)
        {
            for (int i = 0; i < NewIds.Length; i++) if (NewIds[i] == id) return NewNames[i];
            for (int i = 0; i < SetIds.Length; i++) if (SetIds[i] == id) return SetNames[i];
            for (int i = 0; i < OldIds.Length; i++) if (OldIds[i] == id) return OldNames[i];
            return id == 2 ? "모두 비우기" : "";
        }

        static string SetAbout(int id)
        {
            switch (id)
            {
                case 13: return "4판 1차 묶음 가 · 기물마다 두 칸 (F · E) · 먼저 만든 열하나 가운데 열 (폰의 구덩이는 목록에서 넣어요) · 궁극기 없음";
                case 12: return "사용자가 짠 스킬 · 기물마다 F · E 둘 · 궁극기 없음 · 비숍은 E 돌멩이와 R 성수병 가운데 하나를 고르는 중";
                case 11: return "기물마다 F · E 둘 · Q는 10~12초 동안 직접 모는 궁극기 여섯";
                case 8: return "기물마다 칸 조합이 달라요 (퀸은 R까지 셋) · Q는 팀 게이지 궁극기";
                case 10: return "F 하나가 미들게임 모양에서 엔드게임 모양(→)으로 자라요 · Q는 궁극기";
                case 9: return "기물마다 F 하나 (중급 세기)와 Q 궁극기 (팀 게이지)";
                case 0: return "지금 게임에 들어 있는 스킬 (기물마다 F 하나)";
                case 1: return "기획안 1-나 (11-19)";
                case 3: return "스킬 기획서의 A 묶음";
                case 4: return "기물별 한 벌의 기본기 (오프닝) · 킹도 한 걸음";
                case 5: return "후보 문서 1안 (기물당 하나)";
                case 7: return "후보 문서 2안 (단계 1은 첫 칸, 단계 2 · 3은 둘째 칸)";
                case 6: return "후보 문서 3안 (기본기 · 중급기 · 처음 궁극기)";
                case 2: return "모든 칸을 비워요";
                default: return "";
            }
        }

        /// <summary>The set the slots are now (and where each skill sits), worked out again only when the loadout changed.</summary>
        void Refresh(PawnRushLoadout lo)
        {
            int hash = lo.system;
            for (int row = 0; row < PawnRushLoadout.Rows; row++)
                for (int k = 0; k < PawnRushLoadout.Slots; k++)
                    hash = unchecked(hash * 31 + lo.Raw(row, k));
            if (hash == activeHash) return;
            activeHash = hash;
            activeSet = -1;
            for (int n = 0; n < NewIds.Length + SetIds.Length + OldIds.Length && activeSet < 0; n++)
            {
                int id = n < NewIds.Length ? NewIds[n] : n < NewIds.Length + SetIds.Length ? SetIds[n - NewIds.Length] : OldIds[n - NewIds.Length - SetIds.Length];
                if (Matches(lo, id)) activeSet = id;
            }
            placedAt.Clear();
            placedRows.Clear();
            for (int row = 0; row < PawnRushLoadout.Rows; row++)
                for (int k = 0; k < PawnRushLoadout.Slots; k++)
                {
                    int id = lo.Raw(row, k);
                    if (id == 0) continue;
                    placedRows[id] = (placedRows.TryGetValue(id, out int mask) ? mask : 0) | (1 << row);
                    string here = $"{ChessPieces.Name(PawnRushLoadout.RowPieces[row])} {KeyOf(k)}";
                    placedAt[id] = placedAt.TryGetValue(id, out string before) ? (before.EndsWith("…") ? before : before + " …") : here;
                }
        }

        static bool Matches(PawnRushLoadout lo, int id)
        {
            int system = PawnRushLoadout.SystemOf(id);
            if (system != 0 && system != lo.system) return false;
            var p = PawnRushLoadout.Preset(id);
            for (int row = 0; row < PawnRushLoadout.Rows; row++)
                for (int k = 0; k < PawnRushLoadout.Slots; k++)
                    if (p.Raw(row, k) != lo.Raw(row, k)) return false;
            return true;
        }

        /// <summary>A set comes with its skill system (one skill a piece, the growing one, several).</summary>
        void PickSet(PawnRushLoadout lo, int id)
        {
            bool slots = Change(PawnRushLoadout.OpPreset, id);
            int system = PawnRushLoadout.SystemOf(id);
            bool switched = system != 0 && lo.system != system && Change(PawnRushLoadout.OpSystem, system);
            if (!slots && !switched) return;
            selRow = selSlot = -1;
            pickId = 0;
            Say(id == 2 ? "모든 칸을 비웠어요" : $"'{SetName(id)}' 세트로 바꿨어요", false);
        }

        float SetChip(PawnRushLoadout lo, float x, float y, float height, int id, string name)
        {
            bool on = activeSet == id;
            string text = on ? "✓ " + name : name;
            var r = new Rect(x, y, TextW(text, label) + 26f, height);
            if (r.Contains(Event.current.mousePosition)) hoverSetNow = id;
            if (Chip(r, text, on)) PickSet(lo, id);
            return r.xMax;
        }

        void SwitchTab(bool toDiscarded)
        {
            discarded = toDiscarded;
            scroll = 0f;
            showOld = false;
            pickId = 0;
        }

        float DrawSets(PawnRushLoadout lo, float x0, float right, float y)
        {
            // The two tabs (11-26): the 4판's candidates, and under "폐기된 스킬" everything before them (still usable).
            float x = x0;
            const string NewTab = "4판 후보", OldTab = "폐기된 스킬";
            float tw = TextW(NewTab, label) + 26f;
            if (Chip(new Rect(x, y, tw, 28f), NewTab, !discarded) && discarded) SwitchTab(false);
            x += tw + 6f;
            tw = TextW(OldTab, label) + 26f;
            if (Chip(new Rect(x, y, tw, 28f), OldTab, discarded) && !discarded) SwitchTab(true);
            x += tw + 16f;
            Fill(new Rect(x - 9f, y + 4f, 1f, 20f), Edge);
            GUI.Label(new Rect(x, y, 40f, 28f), "<b>세트</b>", label);
            x += 44f;
            float setX = x;
            if (!discarded)
                for (int i = 0; i < NewIds.Length; i++) x = SetChip(lo, x, y, 28f, NewIds[i], NewNames[i]) + 6f;
            else
            {
                for (int i = 0; i < SetIds.Length; i++) x = SetChip(lo, x, y, 28f, SetIds[i], SetNames[i]) + 6f;
                string oldText = showOld ? "옛 세트 ▾" : "옛 세트 ▸";
                float ow = TextW(oldText, label) + 24f;
                if (Chip(new Rect(x + 6f, y, ow, 28f), oldText, showOld)) showOld = !showOld;
            }
            float cw = TextW("모두 비우기", label) + 24f;
            var clear = new Rect(right - cw, y, cw, 28f);
            if (clear.Contains(Event.current.mousePosition)) hoverSetNow = 2;
            if (Chip(clear, "모두 비우기", false)) PickSet(lo, 2);
            y += 34f;
            if (discarded && showOld)
            {
                x = setX;
                for (int i = 0; i < OldIds.Length; i++) x = SetChip(lo, x, y, 24f, OldIds[i], OldNames[i]) + 6f;
                y += 30f;
            }

            // The set in use (or the one under the mouse) in a line; the skill system's switch at its end.
            string sys = lo.system == PawnRushLoadout.One ? "1안 · 칸 하나" : lo.system == PawnRushLoadout.Grow ? "2안 · 하나가 자람" : "3안 · 칸 여럿";
            string sysText = $"방식: {sys} {(showSystems ? "▾" : "▸")}";
            float sw = TextW(sysText, label) + 24f;
            string about = hoverSet >= 0
                ? $"<color=#F2F4F8><b>{SetName(hoverSet)}</b></color>  {SetAbout(hoverSet)}{(hoverSet == 2 ? "" : hoverSet == activeSet ? "  (지금 이 세트)" : "  · 누르면 이 세트로")}"
                : activeSet >= 0 ? $"<color=#FFD94D>지금: <b>{SetName(activeSet)}</b></color>  {SetAbout(activeSet)}"
                : "<color=#FFD94D>지금: <b>직접 바꾼 구성</b></color>  어느 세트와도 달라요 (세트를 누르면 그대로 돌아가요)";
            var aboutRect = new Rect(x0, y, right - x0 - sw - 12f, 24f);
            GUI.Label(aboutRect, about, smallOne);
            if (Chip(new Rect(right - sw, y, sw, 24f), sysText, showSystems)) showSystems = !showSystems;
            y += 30f;
            if (showSystems)
            {
                GUI.Label(new Rect(x0, y, 70f, 24f), "스킬 방식", smallOne);
                x = x0 + 74f;
                string[] systems = { "1안 · 기물마다 칸 하나", "2안 · 하나가 강해짐", "3안 · 기물마다 칸 여럿" };
                for (int i = 0; i < 3; i++)
                {
                    float bw = TextW(systems[i], label) + 24f;
                    if (Chip(new Rect(x, y, bw, 24f), systems[i], lo.system == i + 1) && lo.system != i + 1) Change(PawnRushLoadout.OpSystem, i + 1);
                    x += bw + 6f;
                }
                GUI.Label(new Rect(x + 6f, y, right - x - 6f, 24f), "세트를 고르면 저절로 맞춰져요", smallOne);
                y += 30f;
            }
            return y;
        }

        // ---------------------------------------------------------------- the board

        static bool ShowsSlot(PawnRushLoadout lo, int k) => lo.Plays(k) || (lo.system == PawnRushLoadout.Grow && k == 1);

        /// <summary>A column of the board: in the 4판 tab the game's two slots (F, E), and R or Q only while one holds a
        /// skill (11-26: "각 캐릭터당 스킬 슬롯은 2개고").</summary>
        bool ColumnShown(PawnRushLoadout lo, int k) => ShowsSlot(lo, k) && (discarded || k < 2 || AnyIn(lo, k));

        // ---------------------------------------------------------------- the set 4판's grades

        static readonly Color[] GradeTint =
            { Color.clear, new Color(0.48f, 0.88f, 0.56f, 1f), new Color(0.5f, 0.74f, 1f, 1f), new Color(0.88f, 0.62f, 1f, 1f) };

        static string GradeName(int g) => g == 1 ? "초급" : g == 2 ? "중급" : g == 3 ? "상급" : "";

        /// <summary>"초급 6초": the grade and its cooldown as the numbers have it now (empty for a skill of no grade).</summary>
        string GradeText(int id)
        {
            int g = PawnRushSkillIds.Grade4(id);
            if (g == 0) return "";
            float cd = Skills == null ? 0f : g == 3 ? Skills.s4High : g == 2 ? Skills.s4Mid : Skills.s4Basic;
            return $"{GradeName(g)} {cd:0.#}초";
        }

        float GradeTagW(int id) => PawnRushSkillIds.Grade4(id) == 0 ? 0f : TextW(GradeText(id), smallOne) + 10f;

        /// <summary>The grade's tag in its colour at (x, y); returns its width (0 for a skill of no grade).</summary>
        float GradeTag(float x, float y, int id, float h = 16f)
        {
            int g = PawnRushSkillIds.Grade4(id);
            if (g == 0) return 0f;
            float w = GradeTagW(id);
            var r = new Rect(x, y, w, h);
            Fill(r, new Color(GradeTint[g].r, GradeTint[g].g, GradeTint[g].b, 0.2f));
            var oldColor = smallOne.normal.textColor;
            var oldAlign = smallOne.alignment;
            smallOne.normal.textColor = GradeTint[g];
            smallOne.alignment = TextAnchor.MiddleCenter;
            GUI.Label(r, GradeText(id), smallOne);
            smallOne.normal.textColor = oldColor;
            smallOne.alignment = oldAlign;
            return w;
        }

        /// <summary>Under a piece's name, its two grades ("초 · 상"), coloured; null when it holds no 4판 skill.</summary>
        static string GradeMix(PawnRushLoadout lo, int row)
        {
            string mix = null;
            for (int k = 0; k < PawnRushLoadout.Slots; k++)
            {
                int g = PawnRushSkillIds.Grade4(lo.Raw(row, k));
                if (g == 0) continue;
                string c = ColorUtility.ToHtmlStringRGB(GradeTint[g]);
                string one = $"<color=#{c}>{GradeName(g).Substring(0, 1)}</color>";
                mix = mix == null ? one : mix + " · " + one;
            }
            return mix;
        }

        string SetFourNote()
        {
            string cds = Skills != null ? $"초급 {Skills.s4Basic:0.#}초 · 중급 {Skills.s4Mid:0.#}초 · 상급 {Skills.s4High:0.#}초" : "초급 · 중급 · 상급";
            return $"4판: 기물마다 두 칸 (F · E), 어느 등급이든 섞어 넣어요 ({cds}). 칸이 열리는 때 (1코스 · 2코스 미니게임 장소)는 아직 코스에 안 붙였어요.";
        }

        /// <summary>The five pieces always; the king's row only while it holds a skill (no king in Pawn Rush).</summary>
        static bool ShowsRow(PawnRushLoadout lo, int row)
        {
            if (PawnRushLoadout.RowPieces[row] != PieceKind.King) return true;
            for (int k = 0; k < PawnRushLoadout.Slots; k++)
                if (lo.Raw(row, k) != 0) return true;
            return false;
        }

        static string SystemNote(PawnRushLoadout lo) =>
            lo.system == PawnRushLoadout.One ? "1안: 기물마다 F 하나와 Q 궁극기 (팀 게이지). 빈 Q는 궁극기 없음."
            : lo.system == PawnRushLoadout.Grow ? "2안: F가 미들게임 모양에서 엔드게임 모양(→)으로 자라요. 단계 2부터 → 칸 (코스: 미니게임 2 뒤)."
            : "3안: 칸마다 쿨타임이 따로예요. 한 번에 스킬 하나만 돌아요. R은 시험 씬에서만 (온라인에는 키가 없어요).";

        /// <summary>Every piece's slots under their keys; returns where they end (the notes under them included).</summary>
        float DrawBoard(PawnRushLoadout lo, Rect r, Event e)
        {
            GUI.Label(new Rect(r.x, r.y, r.width, 22f), "<b>기물별 칸</b>  <size=11><color=#9EA9BD>칸을 누르면 오른쪽에서 넣을 스킬을 골라요</color></size>", label);
            float y = r.y + 28f;
            bool grow = lo.system == PawnRushLoadout.Grow;
            int cols = 0;
            for (int k = 0; k < lo.SlotCount; k++) if (ColumnShown(lo, k)) colIndex[cols++] = k;
            int rows = 0;
            for (int row = 0; row < PawnRushLoadout.Rows; row++) if (ShowsRow(lo, row)) rowIndex[rows++] = row;
            const float nameW = 88f, colGap = 8f;
            float stageW = grow ? 112f : 0f;
            // A column nobody uses is narrower, so the names in the others have room.
            float weights = 0f;
            for (int c = 0; c < cols; c++)
            {
                colW[c] = AnyIn(lo, colIndex[c]) || (grow && colIndex[c] == 1) ? 1f : 0.6f;
                weights += colW[c];
            }
            float spread = r.width - nameW - stageW - colGap * (cols - 1), cx0 = r.x + nameW;
            for (int c = 0; c < cols; c++)
            {
                colW[c] = spread * colW[c] / Mathf.Max(0.1f, weights);
                colX[c] = cx0;
                cx0 += colW[c] + colGap;
            }

            // The columns' heads: the key, and what the slot is.
            for (int c = 0; c < cols; c++)
            {
                int k = colIndex[c];
                bool grown = grow && k == 1;
                GUI.Label(new Rect(colX[c] + 4f, y, 30f, 24f), grown ? "→" : KeyOf(k), keyHead);
                string what = grown ? "자란 모양" : k == PawnRushLoadout.UltSlot ? (AnyIn(lo, k) ? "궁극기" : "궁극기 (없음)")
                    : k == 0 ? "첫째 칸" : k == 1 ? "둘째 칸" : "셋째 칸";
                GUI.Label(new Rect(colX[c] + 30f, y + 2f, colW[c] - 32f, 22f), Fit(what, smallOne, colW[c] - 32f), smallOne);
            }
            if (grow) GUI.Label(new Rect(r.xMax - stageW + 8f, y + 2f, stageW - 8f, 22f), "단계 (손으로)", smallOne);
            y += 28f;

            bool room = ShowLock != null && ShowLock();
            float noteH = room ? 42f : 24f;
            float rowH = Mathf.Clamp((r.yMax - noteH - y) / Mathf.Max(1, rows), 46f, 78f);
            for (int i = 0; i < rows; i++)
            {
                int row = rowIndex[i];
                var kind = PawnRushLoadout.RowPieces[row];
                float ry = y + i * rowH;
                if (i % 2 == 0) Fill(new Rect(r.x, ry, r.width, rowH), RowTint);
                GUI.Label(new Rect(r.x, ry, 36f, rowH), Glyph(kind), glyph);
                string mix = GradeMix(lo, row);
                if (mix == null) GUI.Label(new Rect(r.x + 38f, ry, nameW - 40f, rowH), ChessPieces.Name(kind), pieceName);
                else
                {
                    GUI.Label(new Rect(r.x + 38f, ry - 8f, nameW - 40f, rowH), ChessPieces.Name(kind), pieceName);
                    GUI.Label(new Rect(r.x + 38f, ry + rowH * 0.5f + 4f, nameW - 38f, 16f), mix, smallOne);
                }
                for (int c = 0; c < cols; c++)
                    DrawSlot(lo, row, colIndex[c], new Rect(colX[c], ry + 4f, colW[c], rowH - 8f), e);
                if (grow) DrawStages(lo, row, new Rect(r.xMax - stageW + 8f, ry, stageW - 8f, rowH));
            }

            float ny = y + rows * rowH + 6f;
            GUI.Label(new Rect(r.x, ny, r.width, 18f), Fit(discarded ? SystemNote(lo) : SetFourNote(), smallOne, r.width), smallOne);
            if (room)
                GUI.Label(new Rect(r.x, ny + 18f, r.width, 18f), lo.hostOnly
                    ? "방: 방장만 바꿀 수 있어요. 다른 사람은 보기만 해요."
                    : "방: 누구나 바꿀 수 있고, 바꾸면 모두에게 똑같이 적용돼요.", smallOne);

            // A click on the board away from every slot lets the picked slot (or skill) go.
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                selRow = selSlot = -1;
                pickId = 0;
            }
            return ny + (room ? 36f : 18f);
        }

        static bool AnyIn(PawnRushLoadout lo, int k)
        {
            for (int row = 0; row < PawnRushLoadout.Rows; row++)
                if (lo.Raw(row, k) != 0) return true;
            return false;
        }

        void DrawSlot(PawnRushLoadout lo, int row, int k, Rect box, Event e)
        {
            int id = lo.Raw(row, k);
            var entry = PawnRushSkillBook.Find(id);
            bool mouse = box.Contains(e.mousePosition);
            bool selected = selRow == row && selSlot == k;
            bool target = mouse && (dragId != 0 || pickId != 0);   // a skill dropped or clicked here goes in
            Fill(box, selected ? PickFill : entry != null ? (mouse ? CardHover : Card) : (mouse ? new Color(1f, 1f, 1f, 0.08f) : SlotEmpty));
            Frame(box, selected || target ? Lit : entry != null ? CardEdge : Edge, selected || target ? 2f : 1f);
            if (entry != null)
            {
                bool cross = mouse && dragId == 0 && pickId == 0;
                // The skill's picture at the left; its name beside it (on two lines if it needs them) and under a one-line
                // name what it does - or, put on another piece, whose it is.
                float s = Mathf.Min(box.height - 8f, 56f);
                DrawIcon(new Rect(box.x + 4f, box.y + (box.height - s) * 0.5f, s, s), entry);
                float tx = box.x + s + 11f, tw = box.xMax - tx - 5f - (cross ? 18f : 0f), subW = box.xMax - tx - 5f;
                measure.text = entry.name;
                bool twoLines = cardNameWrap.CalcHeight(measure, tw) > 22f;
                const float lineH = 17f, subH = 16f;
                float nameH = twoLines ? lineH * 2f : lineH;
                string sub = entry.home != PawnRushLoadout.RowPieces[row] ? $"({ChessPieces.Name(entry.home)} 것)"
                    : twoLines ? null : PawnRushSkillBook.Card(id);
                bool room = box.height >= nameH + subH + 6f;
                bool graded = PawnRushSkillIds.Grade4(id) > 0 && room;   // (the 4판's grade leads the line under the name)
                bool withSub = sub != null && room;
                float top = box.y + (box.height - nameH - (withSub || graded ? subH : 0f)) * 0.5f;
                if (twoLines) GUI.Label(new Rect(tx, top - 1f, tw, nameH + 3f), entry.name, cardNameWrap);
                else GUI.Label(new Rect(tx, top - 1f, tw, lineH + 3f), Fit(entry.name, cardName, tw), cardName);
                float sx = tx;
                if (graded) sx += GradeTag(tx, top + nameH + 1f, id, subH - 1f) + 5f;
                if (withSub && subW - (sx - tx) > 20f) GUI.Label(new Rect(sx, top + nameH + 1f, subW - (sx - tx), subH), Fit(sub, smallOne, subW - (sx - tx)), smallOne);
                if (mouse && dragId == 0) hoverNow = id;
                if (cross)
                {
                    var x = new Rect(box.xMax - 20f, box.y + 3f, 17f, 17f);
                    Fill(x, new Color(0f, 0f, 0f, 0.55f));
                    GUI.Label(x, "×", center);
                    if (e.type == EventType.MouseDown && e.button == 0 && x.Contains(e.mousePosition))
                    {
                        Put(row, k, 0);
                        e.Use();
                        return;
                    }
                }
            }
            else GUI.Label(box, selected ? "고르세요 →" : "비어 있음", emptyText);

            if (e.type == EventType.MouseUp && e.button == 0 && dragId != 0 && mouse)
            {
                Put(row, k, dragId);
                dragId = 0;
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0 && mouse)
            {
                if (pickId != 0) Put(row, k, pickId);
                else if (selected) selRow = selSlot = -1;
                else
                {
                    selRow = row;
                    selSlot = k;
                    listPiece = row;   // the list turns to this piece's skills
                    scroll = 0f;
                }
                e.Use();
            }
        }

        /// <summary>The growing system's stage of a row, set by hand for now.</summary>
        void DrawStages(PawnRushLoadout lo, int row, Rect r)
        {
            float cy = r.y + r.height * 0.5f - 12f;
            for (int stage = 1; stage <= 3; stage++)
                if (Chip(new Rect(r.x + (stage - 1) * 34f, cy, 30f, 24f), stage.ToString(), lo.stages[row] == stage) && lo.stages[row] != stage)
                    Change(PawnRushLoadout.OpStage + row, stage);
        }

        // ---------------------------------------------------------------- the skills

        bool InTab(SkillEntry entry) => listPiece < 0 || entry.home == PawnRushLoadout.RowPieces[listPiece];

        /// <summary>The 4판 tab lists only the set 4판's skills; "폐기된 스킬" every other.</summary>
        bool InSetTab(SkillEntry entry) => (entry.origin == SkillOrigin.Set4) != discarded;

        /// <summary>On the board in the tab's row (any row with no tab).</summary>
        bool OnBoardHere(int id) =>
            placedRows.TryGetValue(id, out int mask) && (listPiece < 0 || (mask & (1 << listPiece)) != 0);

        void BuildLines(PawnRushLoadout lo)
        {
            lines.Clear();
            listed.Clear();
            ideasHidden = 0;
            // First what is on the board now, in the board's order (where each sits shows on it), then the rest by where
            // they come from.
            int start = lines.Count;
            lines.Add(new Line { head = listPiece < 0 ? "지금 칸에 있는 스킬" : $"지금 {ChessPieces.Name(PawnRushLoadout.RowPieces[listPiece])} 칸에 있는 스킬" });
            for (int row = 0; row < PawnRushLoadout.Rows; row++)
            {
                if (listPiece >= 0 && row != listPiece) continue;
                for (int k = 0; k < PawnRushLoadout.Slots; k++)
                {
                    var entry = PawnRushSkillBook.Find(lo.Raw(row, k));
                    if (entry != null && InSetTab(entry) && listed.Add(entry.id)) lines.Add(new Line { entry = entry });
                }
            }
            if (lines.Count == start + 1) lines.RemoveAt(start);
            if (!discarded)
            {
                // The 4판's candidates by piece: the ones built first, then the ones still to come (faint).
                for (int row = 0; row < PawnRushLoadout.Rows; row++)
                {
                    var kind = PawnRushLoadout.RowPieces[row];
                    if (kind == PieceKind.King || (listPiece >= 0 && row != listPiece)) continue;
                    start = lines.Count;
                    lines.Add(new Line { head = ChessPieces.Name(kind) });
                    for (int pass = 0; pass < 2; pass++)
                        foreach (var entry in PawnRushSkillBook.All)
                        {
                            if (entry.origin != SkillOrigin.Set4 || entry.home != kind || entry.built != (pass == 0) || OnBoardHere(entry.id)) continue;
                            lines.Add(new Line { entry = entry });
                        }
                    if (lines.Count == start + 1) lines.RemoveAt(start);
                }
                return;
            }
            foreach (var origin in GroupOrder)
            {
                start = lines.Count;
                lines.Add(new Line { head = PawnRushSkillBook.OriginName(origin) });
                foreach (var entry in PawnRushSkillBook.All)
                {
                    if (entry.origin != origin || !InTab(entry) || OnBoardHere(entry.id)) continue;
                    if (!entry.built && !showIdeas)
                    {
                        ideasHidden++;
                        continue;
                    }
                    lines.Add(new Line { entry = entry });
                }
                if (lines.Count == start + 1) lines.RemoveAt(start);
            }
        }

        void DrawPicker(PawnRushLoadout lo, Rect r, Event e, bool withDetail)
        {
            // The heading says where a skill clicked goes.
            string head = selRow >= 0
                ? $"<color=#FFD94D><b>{ChessPieces.Name(PawnRushLoadout.RowPieces[selRow])} · {KeyOf(selSlot)} 칸</b></color>에 넣을 스킬을 누르세요"
                : pickId != 0 ? $"<color=#FFD94D><b>{PawnRushSkillBook.Name(pickId)}</b></color>: 넣을 칸을 누르세요"
                : "<b>스킬</b>  <size=11><color=#9EA9BD>끌어다 칸에 놓아도 돼요</color></size>";
            GUI.Label(new Rect(r.x, r.y, r.width, 22f), head, label);
            float y = r.y + 28f;

            // A tab a piece (the skills thought up for it; any skill goes on any piece).
            float x = r.x;
            for (int i = -1; i < PawnRushLoadout.Rows; i++)
            {
                if (i >= 0 && !ShowsRow(lo, i)) continue;
                string name = i < 0 ? "전체" : ChessPieces.Name(PawnRushLoadout.RowPieces[i]);
                float bw = TextW(name, label) + 22f;
                if (Chip(new Rect(x, y, bw, 26f), name, listPiece == i))
                {
                    listPiece = i;
                    scroll = 0f;
                }
                x += bw + 5f;
            }
            y += 32f;

            float detailH = withDetail ? Mathf.Clamp(r.height * 0.3f, 104f, 150f) : 0f;
            var view = new Rect(r.x, y, r.width, r.yMax - detailH - 34f - y);
            DrawList(lo, view, e);

            // The ones that are only plans, behind a switch.
            string ideas = showIdeas ? "계획만 있는 스킬 숨기기" : $"계획만 있는 스킬도 보기 ({ideasHidden})";
            float iw = TextW(ideas, label) + 24f;
            if (discarded && (showIdeas || ideasHidden > 0) && Chip(new Rect(r.x, view.yMax + 4f, iw, 24f), ideas, showIdeas))
            {
                showIdeas = !showIdeas;
                scroll = 0f;
            }
            if (!discarded)
                GUI.Label(new Rect(r.x + 2f, view.yMax + 4f, r.width - 4f, 24f), Fit("흐린 스킬은 준비 중이에요 (다음 묶음에서 만들어요). 예전 스킬은 '폐기된 스킬' 탭에 있어요.", smallOne, r.width - 4f), smallOne);
            if (withDetail) DrawDetail(lo, new Rect(r.x, r.yMax - detailH, r.width, detailH));
        }

        void DrawList(PawnRushLoadout lo, Rect view, Event e)
        {
            BuildLines(lo);
            float content = 0f;
            foreach (var line in lines) content += line.entry == null ? HeadH : SkillH + 4f;
            float maxScroll = Mathf.Max(0f, content - view.height);
            if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition))
            {
                scroll += e.delta.y * 24f;
                e.Use();
            }
            scroll = Mathf.Clamp(scroll, 0f, maxScroll);
            bool mouseInView = view.Contains(e.mousePosition);
            float w = view.width - (maxScroll > 0f ? 10f : 0f);

            GUI.BeginGroup(view);
            float gy = -scroll;
            foreach (var line in lines)
            {
                if (line.entry == null)
                {
                    if (gy + HeadH >= 0f && gy <= view.height)
                    {
                        GUI.Label(new Rect(2f, gy + 2f, w, 20f), $"<b>{line.head}</b>", smallOne);
                        Fill(new Rect(0f, gy + HeadH - 2f, w, 1f), Edge);
                    }
                    gy += HeadH;
                    continue;
                }
                var entry = line.entry;
                var r = new Rect(0f, gy, w, SkillH);
                gy += SkillH + 4f;
                if (r.yMax < 0f || r.y > view.height) continue;
                bool mouse = mouseInView && r.Contains(e.mousePosition);
                bool picked = pickId == entry.id;
                Fill(r, picked ? PickFill : mouse ? CardHover : Card);
                Frame(r, picked ? Lit : mouse ? CardEdge : Edge, picked ? 2f : 1f);
                DrawIcon(new Rect(r.x + 3f, r.y + 3f, SkillH - 6f, SkillH - 6f), entry, entry.built ? 1f : 0.55f);

                // A pill at the right: where it sits on the board now, or that it is only a plan.
                float pillW = 0f;
                string pill = placedAt.TryGetValue(entry.id, out string at) ? at : !entry.built ? (entry.origin == SkillOrigin.Set4 ? "준비 중" : "계획만") : null;
                if (pill != null)
                {
                    pillW = TextW(pill, smallOne) + 14f;
                    var p = new Rect(r.xMax - pillW - 6f, r.y + 5f, pillW, 17f);
                    Fill(p, entry.built ? new Color(1f, 0.85f, 0.3f, 0.2f) : new Color(1f, 1f, 1f, 0.08f));
                    var oldPill = smallOne.normal.textColor;
                    smallOne.normal.textColor = entry.built ? Lit : Faint;
                    var oldAlign = smallOne.alignment;
                    smallOne.alignment = TextAnchor.MiddleCenter;
                    GUI.Label(p, pill, smallOne);
                    smallOne.alignment = oldAlign;
                    smallOne.normal.textColor = oldPill;
                    pillW += 10f;
                }
                float gradeW = GradeTagW(entry.id);
                if (gradeW > 0f)
                {
                    GradeTag(r.xMax - pillW - gradeW - 6f, r.y + 5f, entry.id, 17f);
                    pillW += gradeW + 4f;
                }
                float tx = r.x + SkillH + 6f, tw = r.xMax - tx - 8f;
                var oldName = cardName.normal.textColor;
                cardName.normal.textColor = entry.built ? Ink : Faint;
                GUI.Label(new Rect(tx, r.y + 6f, tw - pillW, 20f), Fit(entry.name, cardName, tw - pillW), cardName);
                cardName.normal.textColor = oldName;
                GUI.Label(new Rect(tx, r.y + 26f, tw, 16f), Fit(PawnRushSkillBook.Card(entry.id), smallOne, tw), smallOne);

                if (!mouse) continue;
                hoverNow = entry.id;
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    if (!entry.built) Say(entry.origin == SkillOrigin.Set4 ? "아직 만드는 중인 스킬이에요 (다음 묶음에서 만들어요)" : "아직 칸에 넣을 수 없는 스킬이에요 (계획만 있어요)");
                    else if (selRow >= 0) Put(selRow, selSlot, entry.id);   // the slot picked on the board takes it
                    else
                    {
                        dragId = entry.id;
                        pickId = pickId == entry.id ? 0 : entry.id;
                    }
                    e.Use();
                }
            }
            GUI.EndGroup();
            if (maxScroll > 0f)
            {
                float barH = Mathf.Max(24f, view.height * view.height / content);
                Fill(new Rect(view.xMax - 5f, view.y, 4f, view.height), new Color(1f, 1f, 1f, 0.04f));
                Fill(new Rect(view.xMax - 5f, view.y + (view.height - barH) * (scroll / maxScroll), 4f, barH), CardEdge);
            }
        }

        /// <summary>What the skill under the mouse does (or the one picked, or the one in the picked slot).</summary>
        void DrawDetail(PawnRushLoadout lo, Rect r)
        {
            Fill(r, SlotEmpty);
            Frame(r, Edge);
            int id = hoverId != 0 ? hoverId : pickId != 0 ? pickId : selRow >= 0 ? lo.Raw(selRow, selSlot) : 0;
            var about = PawnRushSkillBook.Find(id);
            if (about == null)
            {
                GUI.Label(new Rect(r.x + 12f, r.y + 10f, r.width - 24f, r.height - 20f),
                    "① 왼쪽에서 칸을 누르고  ② 오른쪽에서 스킬을 누르면 들어가요.\n스킬을 끌어다 칸에 놓아도 돼요. 칸에 마우스를 올리면 × 로 비울 수 있어요.\n스킬에 마우스를 올리면 여기에 설명이 나와요.", small);
                return;
            }
            // Its picture big at the left, and beside it the name, where it comes from and the whole description.
            float s = Mathf.Min(r.height - 16f, 88f);
            DrawIcon(new Rect(r.x + 8f, r.y + 8f, s, s), about, about.built ? 1f : 0.55f);
            float tx = r.x + s + 20f, tw = r.xMax - tx - 10f;
            GUI.Label(new Rect(tx, r.y + 6f, tw, 20f), Fit(about.name, cardName, tw), cardName);
            string tags = TagNames(about.tags);
            string grade = GradeText(about.id);
            string meta = $"{ChessPieces.Name(about.home)} 스킬 · {PawnRushSkillBook.OriginName(about.origin)}" + (grade.Length > 0 ? " · " + grade : "")
                          + (tags.Length > 0 ? " · " + tags : "") + (about.built ? "" : about.origin == SkillOrigin.Set4 ? " · 준비 중 (다음 묶음)" : " · 계획만 (아직 못 넣음)")
                          + (placedAt.TryGetValue(about.id, out string at) ? " · 지금 " + at : "");
            GUI.Label(new Rect(tx, r.y + 25f, tw, 16f), Fit(meta, smallOne, tw), smallOne);
            var oldColor = small.normal.textColor;
            small.normal.textColor = new Color(0.82f, 0.85f, 0.9f);
            GUI.Label(new Rect(tx, r.y + 46f, tw, r.height - 50f), about.how, small);
            small.normal.textColor = oldColor;
        }

        // ---------------------------------------------------------------- the window

        void OnGUI()
        {
            if (!Open || Skills == null) return;
            if (Skills.loadout == null) Skills.loadout = PawnRushLoadout.Preset(0);
            EnsureStyles();
            GUI.depth = -20;
            var lo = Skills.loadout;
            var e = Event.current;
            Refresh(lo);
            hoverNow = 0;
            hoverSetNow = -1;
            if (selRow >= 0 && !ShowsRow(lo, selRow)) selRow = selSlot = -1;
            if (selSlot >= 0 && !ColumnShown(lo, selSlot)) selRow = selSlot = -1;
            if (listPiece >= 0 && !ShowsRow(lo, listPiece)) listPiece = -1;

            float w = Mathf.Clamp(Screen.width - 120f, 860f, 1260f), h = Mathf.Clamp(Screen.height - 90f, 500f, 800f);
            var area = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Area = area;
            Fill(area, Panel);
            Frame(area, Edge);
            float x0 = area.x + 16f, right = area.xMax - 16f, y = area.y + 12f;

            // The title line: the name, why nothing can be changed (if so), (a room) the lock, the key that closes it.
            GUI.Label(new Rect(x0, y, 300f, 26f), Title, title);
            float hx = right - 64f;
            GUI.Label(new Rect(hx, y, 64f, 26f), $"{toggleKey} 닫기", smallRight);
            if (ShowLock != null && ShowLock())
            {
                string lockText = lo.hostOnly ? "방장만 바꾸기: 켬" : "방장만 바꾸기: 끔";
                float lw = TextW(lockText, label) + 24f;
                hx -= lw + 10f;
                if (Chip(new Rect(hx, y, lw, 26f), lockText, lo.hostOnly)) Change(PawnRushLoadout.OpHostOnly, lo.hostOnly ? 0 : 1);
            }
            if (!Allowed(0))
            {
                string why = LockedNote != null ? LockedNote(0) : "";
                var oldWarn = smallOne.normal.textColor;
                smallOne.normal.textColor = Warn;
                GUI.Label(new Rect(x0 + 300f, y, hx - x0 - 310f, 26f), string.IsNullOrEmpty(why) ? "지금은 바꿀 수 없어요 (보기만)" : why, smallOne);
                smallOne.normal.textColor = oldWarn;
            }
            y += 36f;

            y = DrawSets(lo, x0, right, y);
            Fill(new Rect(x0, y, right - x0, 1f), Edge);
            y += 10f;

            // The two halves: every piece's slots, and the skills to put in them. The message line under both.
            float bottom = area.yMax - 32f, gap = 20f;
            float boardW = Mathf.Round((right - x0 - gap) * 0.62f);
            float boardEnd = DrawBoard(lo, new Rect(x0, y, boardW, bottom - y), e);
            // What a skill does: under the board when there is room there (a tall window), else under the list.
            float roomLeft = bottom - boardEnd - 10f;
            bool detailLeft = roomLeft >= 120f;
            DrawPicker(lo, new Rect(x0 + boardW + gap, y, right - x0 - boardW - gap, bottom - y), e, !detailLeft);
            if (detailLeft) DrawDetail(lo, new Rect(x0, boardEnd + 10f, boardW, Mathf.Min(roomLeft, 150f)));

            if (Time.unscaledTime < messageUntil)
            {
                var old = label.normal.textColor;
                label.normal.textColor = messageWarn ? Warn : Good;
                GUI.Label(new Rect(x0, area.yMax - 28f, right - x0, 22f), message, label);
                label.normal.textColor = old;
            }

            // The skill being dragged, under the mouse.
            if (dragId != 0)
            {
                var entry = PawnRushSkillBook.Find(dragId);
                if (entry != null && e.type == EventType.Repaint)
                {
                    float dw = TextW(entry.name, cardName) + 56f;
                    var d = new Rect(e.mousePosition.x + 10f, e.mousePosition.y - 19f, dw, 38f);
                    Fill(d, new Color(0.2f, 0.18f, 0.08f, 0.95f));
                    Frame(d, Lit, 2f);
                    DrawIcon(new Rect(d.x + 4f, d.y + 4f, 30f, 30f), entry);
                    GUI.Label(new Rect(d.x + 40f, d.y, d.width - 44f, d.height), entry.name, cardName);
                }
                if (e.type == EventType.MouseUp) dragId = 0;
            }
            if (e.type == EventType.Repaint)
            {
                hoverId = hoverNow;
                hoverSet = hoverSetNow;
            }
            // The window's own clicks go no further.
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseUp) && area.Contains(e.mousePosition)) e.Use();
        }
    }
}
