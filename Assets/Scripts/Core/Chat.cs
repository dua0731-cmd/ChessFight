using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ChessFight.Network
{
    // Who a chat line goes to (Docs/Architecture/UI.md "채팅", R61). There is no chat
    // server: party lines go through the party lobby's Steam chat, match lines
    // through the match room's. Team lines travel through the match room as well,
    // and each PC shows only those of its own team.
    public enum ChatChannel { Party, Team, All }

    // One line in the chat box: something a player said, or a notice such as
    // "○○ 님이 파티에 들어왔어요".
    public sealed class ChatEntry
    {
        public ChatChannel Channel;
        // Counted per channel from 1, like the move numbers on a scoresheet.
        public int Number;
        // The Steam ID of whoever said it; 0 for a notice.
        public ulong Sender;
        // The sender's Steam name when the line arrived.
        public string Name = "";
        public string Text = "";
        public bool Notice, Mine;
        // When it arrived, in local seconds, so the match layout can fade it out.
        public double Time;
    }

    // The rules for a line of text and how it travels. Pure, so the tests can pin it.
    public static class ChatText
    {
        // Characters per line, and seconds between two lines from one player.
        public const int MaxLength = 80;
        public const double MinInterval = 1.0;
        // Every chat line starts with this. The match room's chat also carries the
        // JSON seat requests ("{...}"), and this tells the two apart.
        public const string Prefix = "CFC1|";

        public static string Label(ChatChannel channel) =>
            channel == ChatChannel.Party ? "파티" : channel == ChatChannel.All ? "전체" : "팀";

        // What a player typed, made into one line: control and invisible format
        // characters go, any run of white space becomes one space, and the line is
        // cut at MaxLength without splitting a surrogate pair. Empty means nothing
        // to send.
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var line = new StringBuilder(Math.Min(text.Length, MaxLength + 1));
            bool gap = false;
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c)) { gap = line.Length > 0; continue; }
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format) continue;
                if (gap) { line.Append(' '); gap = false; }
                line.Append(c);
                if (line.Length > MaxLength) break;
            }
            if (line.Length > MaxLength)
            {
                int cut = char.IsHighSurrogate(line[MaxLength - 1]) ? MaxLength - 1 : MaxLength;
                line.Length = cut;
            }
            return line.ToString().TrimEnd();
        }

        // "CFC1|p|text" to the party, "CFC1|a|text" to everyone in the match,
        // "CFC1|0|text" or "CFC1|1|text" to one team of it.
        public static string Encode(ChatChannel channel, int team, string text) =>
            Prefix + Code(channel, team) + "|" + Clean(text);

        static char Code(ChatChannel channel, int team) =>
            channel == ChatChannel.Party ? 'p' : channel == ChatChannel.All ? 'a' : team == 1 ? '1' : '0';

        // False for anything that is not a chat line, or one with nothing left to show.
        public static bool TryDecode(string wire, out ChatChannel channel, out int team, out string text)
        {
            channel = ChatChannel.All; team = 0; text = "";
            if (wire == null || wire.Length < Prefix.Length + 2 || !wire.StartsWith(Prefix, StringComparison.Ordinal) ||
                wire[Prefix.Length + 1] != '|') return false;
            switch (wire[Prefix.Length])
            {
                case 'p': channel = ChatChannel.Party; break;
                case 'a': channel = ChatChannel.All; break;
                case '0': channel = ChatChannel.Team; team = 0; break;
                case '1': channel = ChatChannel.Team; team = 1; break;
                default: return false;
            }
            text = Clean(wire.Substring(Prefix.Length + 2));
            return text.Length > 0;
        }

        // The channel after `current` among those usable right now, for the Tab key
        // (파티 → 팀 → 전체, the order the user asked for);
        // `current` itself when nothing else is usable.
        public static ChatChannel Next(ChatChannel current, Func<ChatChannel, bool> usable)
        {
            for (int step = 1; step <= 3; step++)
            {
                var next = (ChatChannel)(((int)current + step) % 3);
                if (usable(next)) return next;
            }
            return current;
        }
    }

    // One line per MinInterval from this PC.
    public sealed class ChatThrottle
    {
        double last = double.NegativeInfinity;

        public bool TryPass(double now)
        {
            if (now < last + ChatText.MinInterval) return false;
            last = now;
            return true;
        }
    }

    // What the chat box shows: the last Capacity lines of each channel, numbered
    // per channel, and how many arrived in each while it was not being looked at.
    public sealed class ChatLog
    {
        public const int Capacity = 50;

        readonly List<ChatEntry> entries = new List<ChatEntry>();
        readonly int[] numbers = new int[3], unread = new int[3];

        // Changes whenever a line is added or a channel cleared, so a view redraws
        // only then.
        public int Version { get; private set; }
        public IReadOnlyList<ChatEntry> Entries => entries;

        public ChatEntry Add(ChatChannel channel, ulong sender, string name, string text, bool notice, bool mine, double time)
        {
            int c = (int)channel;
            var entry = new ChatEntry
            {
                Channel = channel, Number = ++numbers[c], Sender = sender, Name = name ?? "",
                Text = text ?? "", Notice = notice, Mine = mine, Time = time
            };
            entries.Add(entry);
            if (!mine) unread[c]++;
            if (entries.Count(e => e.Channel == channel) > Capacity)
                entries.RemoveAt(entries.FindIndex(e => e.Channel == channel));
            Version++;
            return entry;
        }

        public IEnumerable<ChatEntry> In(ChatChannel channel) => entries.Where(e => e.Channel == channel);

        // A new party or a new match starts its channel afresh, numbering included.
        public void Clear(ChatChannel channel)
        {
            int c = (int)channel;
            if (entries.RemoveAll(e => e.Channel == channel) == 0 && numbers[c] == 0 && unread[c] == 0) return;
            numbers[c] = 0; unread[c] = 0;
            Version++;
        }

        public int Unread(ChatChannel channel) => unread[(int)channel];
        public void MarkRead(ChatChannel channel) => unread[(int)channel] = 0;
    }
}
