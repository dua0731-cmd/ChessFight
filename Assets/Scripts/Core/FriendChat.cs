using System;
using System.Collections.Generic;

namespace ChessFight.Network
{
    // R862 (the user 2026-11-27: "로비 친구 탭에서 친구를 눌러 파티 없이 1:1 대화"; Docs/Architecture/UI.md "친구 1:1 대화"):
    // a conversation with one Steam friend, outside any party or match. Lines travel as Steam friend messages
    // (Network/SteamFriendChat), so a friend who is not in ChessFight reads them in their own Steam chat and what they
    // type there comes back here; a friend in ChessFight reads them in the game. Pure rules, so the tests can pin them.

    // One line of a conversation: something this player or the friend said.
    public sealed class FriendChatLine
    {
        // Counted per conversation from 1.
        public int Number;
        public ulong Friend;
        public bool Mine;
        public string Text = "";
        // When it was sent or arrived, in local seconds.
        public double Time;
    }

    public static class FriendChatText
    {
        // A friend may write from the Steam client, where lines run longer than the game's 80 characters: those are
        // kept up to this many, cleaned the same way. What this player sends keeps the game's own limit (ChatText).
        public const int MaxIncoming = 300;
        // A friend message toast shows this many characters of the line, then "…".
        public const int PreviewLength = 28;

        // What this player typed, made into one line to send ("" for nothing): the game chat's rules.
        public static string Outgoing(string typed) => ChatText.Clean(typed);

        // What arrived from the friend, made into one line to show ("" for nothing to show).
        public static string Incoming(string wire) => ChatText.Clean(wire, MaxIncoming);

        // The start of a line for a short notice.
        public static string Preview(string text)
        {
            text = ChatText.Clean(text, MaxIncoming);
            if (text.Length <= PreviewLength) return text;
            int cut = char.IsHighSurrogate(text[PreviewLength - 1]) ? PreviewLength - 1 : PreviewLength;
            return text.Substring(0, cut).TrimEnd() + "…";
        }
    }

    // Every conversation this run (nothing is kept between runs: Steam keeps the real history), the last Capacity
    // lines of each, and how many of the friend's lines wait unread in each.
    public sealed class FriendChatBook
    {
        public const int Capacity = 100;
        // Conversations kept; past this the one quiet longest goes, unless it still has unread lines.
        public const int ConversationsKept = 40;

        readonly Dictionary<ulong, List<FriendChatLine>> lines = new Dictionary<ulong, List<FriendChatLine>>();
        readonly Dictionary<ulong, int> numbers = new Dictionary<ulong, int>(), unread = new Dictionary<ulong, int>();
        // The friends talked with, the latest first.
        readonly List<ulong> recent = new List<ulong>();
        static readonly List<FriendChatLine> none = new List<FriendChatLine>();

        // Changes whenever a line is added or something is read, so a view redraws only then.
        public int Version { get; private set; }
        // The conversation on screen just now (0 for none): the friend's lines there are read as they come.
        public ulong Viewing { get; set; }
        public IReadOnlyList<ulong> Recent => recent;
        // The friend who last wrote to this player (0 for nobody yet).
        public ulong LastFrom { get; private set; }

        // Null when there is nothing to add (no friend, an empty line).
        public FriendChatLine Add(ulong friend, bool mine, string text, double time)
        {
            text = mine ? FriendChatText.Outgoing(text) : FriendChatText.Incoming(text);
            if (friend == 0 || text == "") return null;
            if (!lines.TryGetValue(friend, out var list)) lines[friend] = list = new List<FriendChatLine>();
            numbers.TryGetValue(friend, out int number);
            numbers[friend] = ++number;
            var line = new FriendChatLine { Number = number, Friend = friend, Mine = mine, Text = text, Time = time };
            list.Add(line);
            if (list.Count > Capacity) list.RemoveAt(0);
            if (!mine)
            {
                LastFrom = friend;
                if (friend != Viewing) unread[friend] = Unread(friend) + 1;
            }
            recent.Remove(friend);
            recent.Insert(0, friend);
            Trim();
            Version++;
            return line;
        }

        void Trim()
        {
            for (int i = recent.Count - 1; i >= 0 && recent.Count > ConversationsKept; i--)
            {
                ulong friend = recent[i];
                if (Unread(friend) > 0 || friend == Viewing) continue;
                recent.RemoveAt(i);
                lines.Remove(friend); numbers.Remove(friend); unread.Remove(friend);
            }
        }

        public IReadOnlyList<FriendChatLine> Lines(ulong friend) => lines.TryGetValue(friend, out var list) ? list : none;

        public int Unread(ulong friend) => unread.TryGetValue(friend, out int n) ? n : 0;

        public int TotalUnread
        {
            get
            {
                int total = 0;
                foreach (var pair in unread) total += pair.Value;
                return total;
            }
        }

        public void MarkRead(ulong friend)
        {
            if (Unread(friend) == 0) return;
            unread.Remove(friend);
            Version++;
        }
    }
}
