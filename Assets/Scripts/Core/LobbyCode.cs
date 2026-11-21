using System.Globalization;
using System.Text;

namespace ChessFight.Network
{
    // R737 (user 11-21: "초대가 번호로 되어 있어서 가독성이 떨어지는데" -> "숫자 수를 1023 5012이런식으로 줄일 수 있냐고" ->
    // "10자리"): how a party code and a room's number are shown, copied and read back.
    //
    // They are the Steam lobby ID. Its upper 32 bits are the same for every lobby Steam makes (universe 1, account
    // type chat, instance flags lobby | matchmaking lobby = 0x01860000; seen on every lobby in the logs), so only the
    // lower 32 bits - the lobby's account number, up to 10 digits - are shown: "42 6890 6151" instead of
    // "10 9775 2451 8606 1991". Steam is not changed; joining puts the upper half back on. A lobby whose upper half
    // is something else (none so far) is shown and copied whole, and the whole 17-18 digit number still joins.
    public static class LobbyCode
    {
        public const ulong LobbyHigh = 0x01860000UL << 32;
        const ulong HighMask = 0xFFFFFFFFUL << 32;

        // The number a player sees and copies: the short one when the lobby has the usual upper half.
        public static ulong Short(ulong id) => (id & HighMask) == LobbyHigh ? id & 0xFFFFFFFFUL : id;

        // The plain digits, for the clipboard.
        public static string Plain(ulong id) => id == 0 ? "" : Short(id).ToString(CultureInfo.InvariantCulture);

        // Fours from the right, the shorter head on its own: "42 6890 6151".
        public static string Format(ulong id)
        {
            if (id == 0) return "—";
            string digits = Plain(id);
            int head = digits.Length % 4 == 0 ? 4 : digits.Length % 4;
            var text = new StringBuilder(digits.Substring(0, head));
            for (int i = head; i < digits.Length; i += 4) text.Append(' ').Append(digits, i, 4);
            return text.ToString();
        }

        // What a player typed or pasted: the digits, with any of the separators people use between them
        // (spaces, ·, -, ., commas). Up to 10 digits (anything that fits 32 bits) is a short number and gets the upper
        // half back; a longer one is a whole lobby ID (the old codes). Anything else is not a code.
        public static bool TryRead(string text, out ulong id)
        {
            id = 0;
            if (string.IsNullOrEmpty(text)) return false;
            var digits = new StringBuilder();
            foreach (char c in text)
            {
                if (c >= '0' && c <= '9') digits.Append(c);
                else if (!(char.IsWhiteSpace(c) || c == '·' || c == '•' || c == '-' || c == '.' || c == ',')) return false;
            }
            if (digits.Length == 0 || !ulong.TryParse(digits.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out ulong value) || value == 0)
                return false;
            id = value <= 0xFFFFFFFFUL ? LobbyHigh | value : value;
            return true;
        }
    }
}
