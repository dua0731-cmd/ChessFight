using System.Collections.Generic;
using System.Text;

namespace ChessFight.Network
{
    // R792 (user 11-23, "1 2 3 진행": settings → 소셜 → 방송 → 스트리머 모드; Docs/Architecture/UI.md §12 R792): what this
    // PC's screens keep off a stream. Other players' names show as "플레이어 N" (N in the order this run first meets them,
    // the same all run long; this PC's own name, and the bots', stay), their Steam pictures not at all, and the room
    // number and party code as dots (the copy buttons still copy the real one). Nothing changes for anyone else.
    // GameSettings sets it from the settings (Set); the name sources ask it (SteamSession.Name, the friends list, the
    // chat, the records and ranking tabs, the codes).
    public static class StreamerMode
    {
        public static bool HideNames { get; private set; }
        public static bool HideCodes { get; private set; }
        // Moves whenever either changes, for views that redraw only then.
        public static int Version { get; private set; }

        static readonly Dictionary<ulong, int> numbers = new Dictionary<ulong, int>();

        public static void Set(bool names, bool codes)
        {
            if (names == HideNames && codes == HideCodes) return;
            HideNames = names;
            HideCodes = codes;
            Version++;
        }

        // A player's number, from 1, kept for the run.
        public static int Number(ulong id)
        {
            if (!numbers.TryGetValue(id, out int n)) numbers[id] = n = numbers.Count + 1;
            return n;
        }

        public static string Alias(ulong id) => Loc.F("플레이어 {0}", Number(id));

        // The name to show for a player: the real one when names are not hidden, for this PC's own player, or with no id.
        public static string Name(ulong id, string real, bool self) => HideNames && !self && id != 0 ? Alias(id) : real;

        // A stored name with no id (the records tab's players): "플레이어 N" by its place in the list shown, from 1.
        public static string Listed(int place, string real, bool self) => HideNames && !self ? Loc.F("플레이어 {0}", place) : real;

        // A room number or party code as shown: every letter and digit a dot, the spaces between the groups kept.
        public static string Code(string code)
        {
            if (!HideCodes || string.IsNullOrEmpty(code)) return code;
            var text = new StringBuilder(code.Length);
            foreach (char c in code) text.Append(char.IsLetterOrDigit(c) ? '●' : c);
            return text.ToString();
        }

        // For the tests: back to the start.
        public static void Reset()
        {
            numbers.Clear();
            Set(false, false);
        }
    }
}
