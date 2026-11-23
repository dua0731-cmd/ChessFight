using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChessFight.Network
{
    // R747 (user 11-21: "현재 언어가 한국어만 있는데 ... 필요한 언어 알려줘" -> "기반부터"): the game's languages and the
    // string table every player-facing text is read through.
    //
    // The Korean text is the key (gettext style): a call site keeps its Korean and wraps it, Loc.T("언어"), and a
    // language that has no line for it yet shows the Korean. Moving the ~1,300 player-facing strings over is then
    // wrapping, not inventing a key for each. Text with values in it goes through Loc.F with {0} {1} slots, so each
    // language can put them in its own order: Loc.F("낙하 {0} · 맞음 {1}", falls, hits).
    //
    // The table is one tab-separated text (Game loads it from Resources/Localization/Strings.txt):
    //
    //   # a comment
    //   ko       en         zh-Hans      <- the header: language codes, Korean first
    //   @ready   0          0            <- 1 = complete enough to be picked by itself (from Steam or Windows)
    //   언어     Language   语言
    //
    // In a cell, \n is a line break, \t a tab and \\ a backslash. An empty cell is "not translated yet".

    // Which letters a language needs; Game picks fonts by it.
    public enum Script { Hangul, Latin, LatinExtended, Cyrillic, SimplifiedChinese, TraditionalChinese, Japanese }

    public sealed class Language
    {
        // Code: the table's column name. Native: the name shown in the settings list. Steam: the API language name
        // SteamApps.GetCurrentGameLanguage returns. System: UnityEngine.SystemLanguage's name ("" for none).
        public readonly string Code, Native, Steam, System;
        public readonly Script Script;

        public Language(string code, string native, string steam, string system, Script script)
        { Code = code; Native = native; Steam = steam; System = system; Script = script; }
    }

    public static class Languages
    {
        // The order is the settings list's: Korean, then by Steam's player share (2026 survey: English, Simplified
        // Chinese, Russian, Spanish, Brazilian Portuguese, German, Japanese, French, ...).
        public static readonly Language[] All =
        {
            new Language("ko", "한국어", "koreana", "Korean", Script.Hangul),
            new Language("en", "English", "english", "English", Script.Latin),
            new Language("zh-Hans", "简体中文", "schinese", "ChineseSimplified", Script.SimplifiedChinese),
            new Language("ru", "Русский", "russian", "Russian", Script.Cyrillic),
            new Language("es", "Español (España)", "spanish", "Spanish", Script.Latin),
            new Language("es-419", "Español (Latinoamérica)", "latam", "", Script.Latin),
            new Language("pt-BR", "Português (Brasil)", "brazilian", "Portuguese", Script.Latin),
            new Language("de", "Deutsch", "german", "German", Script.Latin),
            new Language("ja", "日本語", "japanese", "Japanese", Script.Japanese),
            new Language("fr", "Français", "french", "French", Script.Latin),
            new Language("zh-Hant", "繁體中文", "tchinese", "ChineseTraditional", Script.TraditionalChinese),
            new Language("pl", "Polski", "polish", "Polish", Script.LatinExtended),
            new Language("tr", "Türkçe", "turkish", "Turkish", Script.LatinExtended),
        };

        public static Language Find(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            foreach (var l in All) if (string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)) return l;
            return null;
        }

        // Steam's API name ("koreana", "schinese", ...) to a code; null for one the game does not have.
        public static string FromSteam(string steam)
        {
            if (string.IsNullOrEmpty(steam)) return null;
            foreach (var l in All) if (string.Equals(l.Steam, steam.Trim(), StringComparison.OrdinalIgnoreCase)) return l.Code;
            return null;
        }

        // R785 (user 11-23: "스팀이 한국어인데 영어로 시작해"): which Steam language name to follow. Steam answers
        // GetCurrentGameLanguage for the App ID the game runs under; Valve's test app (480, Spacewar) has English
        // only, so under it every player reads "english" whatever their Steam is in. There the Steam client's own
        // language (SteamUtils.GetSteamUILanguage) is taken instead; under the game's real App ID, its own.
        public static string SteamName(bool testApp, string game, string ui)
        {
            if (testApp && !string.IsNullOrEmpty(ui)) return ui;
            return game;
        }

        // UnityEngine.SystemLanguage's name to a code. Plain "Chinese" (old Unity) is taken as Simplified.
        public static string FromSystem(string system)
        {
            if (string.IsNullOrEmpty(system)) return null;
            if (system == "Chinese") return "zh-Hans";
            foreach (var l in All) if (l.System.Length > 0 && l.System == system) return l.Code;
            return null;
        }

        // The language to show: the player's own pick when the table has it, else Steam's, else Windows' - those two
        // only when the table marks the language ready, so a half-translated one is never forced on anyone - else
        // Korean.
        public static string Choose(string saved, string steam, string system, StringTable table)
        {
            table = table ?? StringTable.Empty;
            var pick = Find(saved);
            if (pick != null && table.Has(pick.Code)) return pick.Code;
            var s = Find(steam);
            if (s != null && table.Ready(s.Code)) return s.Code;
            var w = Find(system);
            if (w != null && table.Ready(w.Code)) return w.Code;
            return Loc.Source;
        }
    }

    public sealed class StringTable
    {
        public static readonly StringTable Empty = new StringTable(new[] { Loc.Source }, new bool[] { true }, new Dictionary<string, string[]>());

        readonly string[] codes;
        readonly bool[] ready;
        readonly Dictionary<string, string[]> rows;

        StringTable(string[] codes, bool[] ready, Dictionary<string, string[]> rows)
        { this.codes = codes; this.ready = ready; this.rows = rows; }

        // The languages the table has a column for, Korean first.
        public IList<string> Codes => Array.AsReadOnly(codes);
        // Korean texts with a line in the table.
        public int Count => rows.Count;

        public bool Has(string code) => Column(code) >= 0;
        public bool Ready(string code) { int c = Column(code); return c >= 0 && ready[c]; }

        // How many lines have this language filled in.
        public int Translated(string code)
        {
            int c = Column(code);
            if (c <= 0) return c == 0 ? rows.Count : 0;
            int n = 0;
            foreach (var r in rows.Values) if (!string.IsNullOrEmpty(r[c - 1])) n++;
            return n;
        }

        public bool Contains(string ko) => ko != null && rows.ContainsKey(ko);

        // R756: the Korean line whose text in this language is `text`, or null (Korean itself: `text` when the table
        // has it). Two lines with the same translation give the first. Built once per language, on first use.
        public string KoreanOf(string text, string code)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int c = Column(code);
            if (c < 0) return null;
            if (c == 0) return rows.ContainsKey(text) ? text : null;
            if (back == null) back = new Dictionary<string, string>[codes.Length];
            var map = back[c];
            if (map == null)
            {
                map = back[c] = new Dictionary<string, string>();
                foreach (var pair in rows)
                {
                    var t = pair.Value[c - 1];
                    if (!string.IsNullOrEmpty(t) && !map.ContainsKey(t)) map[t] = pair.Key;
                }
            }
            string ko;
            return map.TryGetValue(text, out ko) ? ko : null;
        }

        Dictionary<string, string>[] back;

        // The text in this language, or the Korean when the language has none yet (or the table does not know it).
        public string Get(string ko, string code)
        {
            if (ko == null) return "";
            int c = Column(code);
            if (c <= 0) return ko;
            string[] row;
            if (!rows.TryGetValue(ko, out row)) return ko;
            var text = row[c - 1];
            return string.IsNullOrEmpty(text) ? ko : text;
        }

        int Column(string code)
        {
            if (string.IsNullOrEmpty(code)) return -1;
            for (int i = 0; i < codes.Length; i++) if (string.Equals(codes[i], code, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        // Reads the table's text. Problems (a bad header, an unknown language, a line given twice) go to `problems`;
        // the table keeps whatever it could read.
        public static StringTable Parse(string text, List<string> problems = null)
        {
            if (string.IsNullOrEmpty(text)) return Empty;
            string[] header = null;
            bool[] ready = null;
            var rows = new Dictionary<string, string[]>();
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                var line = lines[n];
                if (n == 0 && line.Length > 0 && line[0] == '\uFEFF') line = line.Substring(1);
                if (line.Trim().Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                var cells = line.Split('\t');
                if (header == null)
                {
                    if (cells[0].Trim() != Loc.Source)
                    {
                        Note(problems, "줄 " + (n + 1) + ": 첫 줄은 언어 코드여야 하고 ko로 시작해야 합니다");
                        return Empty;
                    }
                    header = new string[cells.Length];
                    for (int i = 0; i < cells.Length; i++)
                    {
                        header[i] = cells[i].Trim();
                        if (Languages.Find(header[i]) == null) Note(problems, "줄 " + (n + 1) + ": 모르는 언어 코드 '" + header[i] + "'");
                    }
                    ready = new bool[header.Length];
                    ready[0] = true;
                    continue;
                }
                if (cells[0] == "@ready")
                {
                    for (int i = 1; i < header.Length && i < cells.Length; i++) ready[i] = cells[i].Trim() == "1";
                    continue;
                }
                if (cells.Length > header.Length)
                    Note(problems, "줄 " + (n + 1) + ": 칸이 언어보다 많습니다(" + cells.Length + " > " + header.Length + ")");
                var ko = Unescape(cells[0]);
                if (ko.Length == 0) { Note(problems, "줄 " + (n + 1) + ": 한국어 칸이 비었습니다"); continue; }
                var row = new string[header.Length - 1];
                for (int i = 1; i < header.Length; i++) row[i - 1] = i < cells.Length ? Unescape(cells[i]) : "";
                if (rows.ContainsKey(ko)) Note(problems, "줄 " + (n + 1) + ": 같은 한국어가 또 있습니다(뒤의 것을 씀) - " + ko);
                // A translation must keep every {N} of the Korean, no more and no fewer (Loc.F would show Korean).
                var want = Slots(ko);
                for (int i = 0; i < row.Length; i++)
                    if (row[i].Length > 0 && Slots(row[i]) != want)
                        Note(problems, "줄 " + (n + 1) + ": " + header[i + 1] + "의 {칸}이 한국어와 다릅니다(" + want + " / " + Slots(row[i]) + ") - " + ko);
                rows[ko] = row;
            }
            if (header == null) return Empty;
            return new StringTable(header, ready, rows);
        }

        static void Note(List<string> problems, string text) { if (problems != null) problems.Add(text); }

        // The format slots a text uses, as a sorted list of their numbers ("0,1"; "" for none). {{ and }} are not slots.
        public static string Slots(string text)
        {
            var found = new SortedSet<int>();
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '{') continue;
                if (i + 1 < text.Length && text[i + 1] == '{') { i++; continue; }
                int j = i + 1, number = 0;
                bool digits = false;
                while (j < text.Length && text[j] >= '0' && text[j] <= '9') { number = number * 10 + (text[j] - '0'); j++; digits = true; }
                if (digits && j < text.Length && (text[j] == '}' || text[j] == ':' || text[j] == ',')) found.Add(number);
            }
            return string.Join(",", found);
        }

        public static string Unescape(string cell)
        {
            if (cell.IndexOf('\\') < 0) return cell;
            var sb = new StringBuilder(cell.Length);
            for (int i = 0; i < cell.Length; i++)
            {
                char c = cell[i];
                if (c == '\\' && i + 1 < cell.Length)
                {
                    char e = cell[i + 1];
                    if (e == 'n') { sb.Append('\n'); i++; continue; }
                    if (e == 't') { sb.Append('\t'); i++; continue; }
                    if (e == '\\') { sb.Append('\\'); i++; continue; }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        // The cell form of a text (the other way round), for tools that write the table.
        public static string Escape(string text) =>
            (text ?? "").Replace("\\", "\\\\").Replace("\r\n", "\n").Replace("\n", "\\n").Replace("\t", "\\t");
    }

    public static class Loc
    {
        public const string Source = "ko";

        // The language shown now (a table column), and the table. Without a table everything stays Korean,
        // which is also what editor tools and batch captures that never load one get.
        public static string Lang { get; private set; } = Source;
        public static StringTable Table { get; private set; } = StringTable.Empty;
        // R756: the language shown before the last change (Lang itself after a reload of the table), for Again.
        public static string Previous { get; private set; } = Source;
        // Bumped on every change of language or table, for views that redraw only when it moves.
        public static int Version { get; private set; }
        public static event Action Changed;
        // Where a Changed listener's exception goes (Game: Debug.LogException); Core has no log of its own.
        public static Action<Exception> Error;

        public static Language Current => Languages.Find(Lang) ?? Languages.All[0];

        public static void Load(StringTable table)
        {
            Table = table ?? StringTable.Empty;
            Previous = Lang;
            if (!Table.Has(Lang)) Lang = Source;
            Raise();
        }

        // Shows this language; false (and nothing changes) for a code the table has no column for.
        public static bool Use(string code)
        {
            var language = Languages.Find(code);
            if (language == null || !Table.Has(language.Code)) return false;
            if (language.Code == Lang) return true;
            Previous = Lang;
            Lang = language.Code;
            Raise();
            return true;
        }

        // This Korean text in the language shown.
        public static string T(string ko) => Table.Get(ko, Lang);

        // R756: words already on screen in the language before the last change, in the language now - for a label
        // built once (a tab, a button, a mode card) that no view draws again. Text the table does not know (a
        // name, a number, a line with values put in) comes back as it was.
        public static string Again(string shown)
        {
            if (string.IsNullOrEmpty(shown)) return shown;
            var ko = Table.Contains(shown) ? shown : Table.KoreanOf(shown, Previous);
            return ko == null ? shown : T(ko);
        }

        // The same with values put into its {0} {1} slots. A translation whose slots do not fit falls back to the
        // Korean pattern, so a typo in the table shows Korean instead of throwing.
        public static string F(string ko, params object[] args)
        {
            var pattern = T(ko);
            try { return string.Format(CultureInfo.InvariantCulture, pattern, args); }
            catch (FormatException)
            {
                try { return string.Format(CultureInfo.InvariantCulture, ko, args); }
                catch (FormatException) { return ko; }
            }
        }

        static void Raise()
        {
            Version++;
            var changed = Changed;
            if (changed == null) return;
            foreach (var d in changed.GetInvocationList())
            {
                // One listener that throws must not stop the others from switching.
                try { ((Action)d)(); }
                catch (Exception e) { var error = Error; if (error != null) error(e); }
            }
        }
    }
}
