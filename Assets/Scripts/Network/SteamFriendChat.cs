using System;
using Steamworks;
using UnityEngine;

namespace ChessFight.Network
{
    // R862 (Docs/Architecture/UI.md "친구 1:1 대화"): one-to-one chat with a Steam friend, outside any party or match,
    // carried as Steam friend messages. This PC listens for them (SetListenForFriendsMessages), so a friend's line -
    // typed in ChessFight or in their own Steam chat - arrives here (GameConnectedFriendChatMsg_t); a line sent here
    // (ReplyToFriendMessage) reaches the friend's Steam chat as plain text, and ChessFight shows it when they are in
    // it. Plain text on purpose: a friend outside the game reads the very words, with no prefix of ours.
    //
    // Owned by NetworkRuntime next to the session (Steam's callbacks run in SteamSession.Tick). The lines go to Book
    // (Core FriendChatBook, the lobby's 1:1 window) and, for a friend's line, also to the session's chat log as a
    // Friend line, so a match's chat box shows it as it comes.
    public sealed class SteamFriendChat : IDisposable
    {
        // Steam's own limit for one chat message is far above this; a longer one is cut by the cleaning anyway.
        const int ReadBuffer = 8192;

        readonly SteamSession session;
        readonly Callback<GameConnectedFriendChatMsg_t> received;
        readonly ChatThrottle throttle = new ChatThrottle();
        bool disposed;

        public readonly FriendChatBook Book = new FriendChatBook();
        // False when Steam refused to hand friend messages to the game: the window still sends, but hears nothing.
        public bool Listening { get; private set; }
        // A friend's line just arrived (for the lobby's notice).
        public event Action<FriendChatLine> Heard;

        public SteamFriendChat(SteamSession session)
        {
            this.session = session;
            received = Callback<GameConnectedFriendChatMsg_t>.Create(OnMessage);
            Listening = SteamFriends.SetListenForFriendsMessages(true);
            if (!Listening) Debug.LogWarning("[ChessFight] Steam이 친구 메시지 받기를 거절했습니다(SetListenForFriendsMessages).");
        }

        // Whether a line can go to this friend now.
        public bool CanSend(ulong friend) => !disposed && session.Online && friend != 0 && friend != session.Self && session.IsFriend(friend);

        // Sends one line: "" once it is on its way (or there was nothing to send), otherwise why not, for the window
        // to show. The game chat's limits: 80 characters, one line a second.
        public string Send(ulong friend, string typed)
        {
            string text = FriendChatText.Outgoing(typed);
            if (text == "") return "";
            if (!CanSend(friend)) return Loc.T("지금은 이 친구에게 메시지를 보낼 수 없어요.");
            if (!throttle.TryPass(Time.realtimeSinceStartup)) return Loc.T("1초에 한 번까지 보낼 수 있어요.");
            // Steam says false when this account is rate limited or chat restricted.
            if (!SteamFriends.ReplyToFriendMessage(new CSteamID(friend), text)) return Loc.T("메시지를 보내지 못했어요.");
            Book.Add(friend, true, text, Time.realtimeSinceStartup);
            return "";
        }

        void OnMessage(GameConnectedFriendChatMsg_t c)
        {
            if (disposed) return;
            ulong friend = c.m_steamIDUser.m_SteamID;
            // Steam's callback is for lines received; one from this account (another of its PCs) is not a friend's.
            if (friend == 0 || friend == session.Self) return;
            int length = SteamFriends.GetFriendMessage(c.m_steamIDUser, c.m_iMessageID, out string data, ReadBuffer, out EChatEntryType type);
            // Only typed lines: Steam also reports typing, game invites and the like here.
            if (length <= 0 || type != EChatEntryType.k_EChatEntryTypeChatMsg) return;
            double now = Time.realtimeSinceStartup;
            var line = Book.Add(friend, false, data, now);
            if (line == null) return;
            // The real name: the chat box turns it into "플레이어 N" itself while streamer mode hides names (R792).
            session.Chat.Add(ChatChannel.Friend, friend, session.RealName(friend), line.Text, false, false, now);
            try { Heard?.Invoke(line); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { if (session.Online) SteamFriends.SetListenForFriendsMessages(false); }
            catch (InvalidOperationException) { }
            received?.Dispose();
        }
    }
}
