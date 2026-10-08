using System;
using System.Text;

namespace ChessFight.Network
{
    // The name a player picks on the name screen (R84, Docs/Architecture/UI.md
    // "이름 설정 화면"): what may be typed, the short reason shown in place of the
    // character count when it may not, and the names the "추천 이름" button offers.
    // Kept free of Unity so the rules are tested with the rest of Core, and so the
    // session can check a name another player publishes before showing it.
    public static class PlayerNames
    {
        public const int MinLength = 2, MaxLength = 12;

        // What "추천 이름" offers, one at a time.
        public static readonly string[] Suggestions =
        {
            "날쌘 나이트", "느긋한 룩", "용감한 폰", "수줍은 비숍", "배고픈 퀸", "졸린 킹",
            "번개 폰", "철벽 룩", "춤추는 비숍", "외로운 나이트", "반짝이는 폰", "성난 룩"
        };

        // Refused anywhere in the name (spaces and case ignored).
        static readonly string[] Banned = { "바보", "멍청", "운영자", "관리자", "admin" };
        // Refused as a whole word only, so a name such as "Sigma" stays allowed.
        static readonly string[] BannedWords = { "gm" };

        public struct Verdict
        {
            public bool Ok;
            // Empty when nothing needs saying (an empty field, a syllable still being typed).
            public string Error;
            public Verdict(bool ok, string error) { Ok = ok; Error = error ?? ""; }
        }

        // `typing`: the field still has the focus, so a lone consonant or vowel is a
        // syllable on its way rather than a mistake.
        public static Verdict Check(string name, bool typing)
        {
            string t = (name ?? "").Trim();
            if (t.Length == 0) return new Verdict(false, "");
            foreach (char c in t)
                if (!Allowed(c)) return new Verdict(false, "한글·영어·숫자만");
            bool jamo = HasJamo(t);
            if (jamo && !typing) return new Verdict(false, "다 쓰지 않은 글자");
            if (t.Length < MinLength) return new Verdict(false, MinLength + "자 이상");
            if (t.Length > MaxLength) return new Verdict(false, MaxLength + "자까지");
            if (t.Contains("  ")) return new Verdict(false, "띄어쓰기는 한 칸");
            if (IsBanned(t)) return new Verdict(false, "쓸 수 없는 말");
            if (jamo) return new Verdict(false, "");
            return new Verdict(true, "");
        }

        // A name as it is kept: trimmed, runs of spaces made one.
        public static string Tidy(string name)
        {
            var sb = new StringBuilder();
            bool space = false;
            foreach (char c in (name ?? "").Trim())
            {
                if (c == ' ') { if (!space) sb.Append(' '); space = true; }
                else { sb.Append(c); space = false; }
            }
            return sb.ToString();
        }

        // A name another player published, safe to show as it is.
        public static bool Showable(string name) => !string.IsNullOrEmpty(name) && name == Tidy(name) && Check(name, false).Ok;

        // A suggestion other than `current`.
        public static string Suggest(string current, Random random)
        {
            if (random == null) random = new Random();
            string pick = Suggestions[random.Next(Suggestions.Length)];
            for (int i = 0; i < 8 && pick == current; i++) pick = Suggestions[random.Next(Suggestions.Length)];
            if (pick == current) pick = Suggestions[(Array.IndexOf(Suggestions, current) + 1) % Suggestions.Length];
            return pick;
        }

        static bool Allowed(char c) =>
            (c >= '가' && c <= '힣') || IsJamo(c) || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == ' ';

        // Hangul compatibility consonants and vowels, ㄱ to ㅣ: what the IME shows before
        // a syllable is complete.
        static bool IsJamo(char c) => c >= 'ㄱ' && c <= 'ㅣ';

        static bool HasJamo(string s)
        {
            foreach (char c in s) if (IsJamo(c)) return true;
            return false;
        }

        static bool IsBanned(string t)
        {
            string flat = t.ToLowerInvariant().Replace(" ", "");
            foreach (var w in Banned) if (flat.Contains(w)) return true;
            foreach (var word in t.ToLowerInvariant().Split(' '))
                foreach (var w in BannedWords) if (word == w) return true;
            return false;
        }
    }
}
