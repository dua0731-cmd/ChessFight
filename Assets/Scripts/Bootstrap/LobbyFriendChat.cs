using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.Game
{
    // R862 (Docs/Architecture/UI.md "친구 1:1 대화"): the lobby's half of the friend 1:1 chat. It puts the window
    // (FriendChatWindow) over the lobby and wires it and the friends card (NetworkHudView.FriendChat) to the runtime's
    // Steam friend chat (Network/SteamFriendChat): a friend's "대화" opens the window, a line typed there goes as a
    // Steam friend message, and a friend's line that arrives with their window shut shows as a notice and a red
    // count on their "대화" button and on the "친구" button. LobbyBootstrap adds it (Attach).
    [DisallowMultipleComponent]
    public sealed class LobbyFriendChat : MonoBehaviour
    {
        // The window's line under the name follows the friend list, read this often.
        const float PresenceSeconds = 5f;

        NetworkRuntime runtime;
        NetworkHudView hud;
        Func<bool> matching;
        FriendChatWindow window;
        GameObject host;
        SteamFriendChat chat;
        int drawnVersion = -1;
        float presenceAt;
        readonly Dictionary<ulong, FriendInfo> presence = new Dictionary<ulong, FriendInfo>();

        SteamSession session => runtime.Session;

        public void Attach(NetworkRuntime owner, NetworkHudView view, Func<bool> busy)
        {
            runtime = owner; hud = view; matching = busy;
            // A panel of its own, on a root object of its own, never a child of this one: this object holds the
            // lobby HUD's document, and a UIDocument under another must share its PanelSettings (Unity asserts).
            host = new GameObject("Friend Chat");
            SceneManager.MoveGameObjectToScene(host, gameObject.scene);
            try
            {
                window = host.AddComponent<FriendChatWindow>();
                window.Build();
            }
            catch (Exception e) { Debug.LogException(e); window = null; }
            hud.ChatFriend += Open;
            hud.FriendUnread = id => chat != null ? chat.Book.Unread(id) : 0;
        }

        void Open(ulong friend)
        {
            if (window == null || chat == null || friend == 0) return;
            if (window.IsOpen && window.Friend == friend) { window.Close(); return; }
            presenceAt = 0;
            window.Open(friend);
        }

        void Update()
        {
            if (runtime == null || hud == null) return;
            if (chat != runtime.FriendChat)
            {
                if (chat != null) chat.Heard -= OnHeard;
                chat = runtime.FriendChat;
                if (chat != null)
                {
                    chat.Heard += OnHeard;
                    window?.Connect(chat.Book, id => session.RealName(id), StateOf, chat.Send);
                }
                drawnVersion = -1;
            }
            bool usable = chat != null && session.Online && !matching();
            hud.FriendChatAvailable = usable;
            if (window != null)
            {
                window.Allowed = usable && hud.Tab == LobbyTab.Play;
                window.BesideFriends = hud.FriendsOpen;
                hud.ChattingWith = window.Friend;
            }
            if (chat == null) return;
            hud.FriendUnreadTotal = chat.Book.TotalUnread;
            int version = chat.Book.Version * 31 + (int)(hud.ChattingWith & 0xFFFF);
            if (version != drawnVersion) { drawnVersion = version; hud.RedrawFriends(); }
            if (window != null && window.IsOpen && Time.unscaledTime >= presenceAt)
            {
                presenceAt = Time.unscaledTime + PresenceSeconds;
                presence.Clear();
                foreach (var friend in session.Friends()) presence[friend.Id] = friend;
            }
        }

        // The line under the friend's name in the window.
        string StateOf(ulong id)
        {
            if (!presence.TryGetValue(id, out var friend)) return "";
            if (friend.Presence == FriendPresence.InGame)
                return string.IsNullOrEmpty(friend.Detail) ? Loc.T("이 게임 중") : Loc.T("이 게임 중") + " · " + friend.Detail;
            return friend.Presence == FriendPresence.Offline ? Loc.T("오프라인 · 다음에 접속하면 받아요")
                 : friend.Presence == FriendPresence.Away ? Loc.T("자리 비움 · Steam 채팅으로 받아요") : Loc.T("Steam 온라인 · Steam 채팅으로 받아요");
        }

        // A friend's line with their window shut: a notice with its start (the name as streamer mode allows, R792).
        void OnHeard(FriendChatLine line)
        {
            if (window != null && window.IsOpen && window.Friend == line.Friend && window.Allowed) return;
            string name = StreamerMode.Name(line.Friend, session.RealName(line.Friend), false);
            hud.ShowToast(Loc.F("{0}님의 메시지: {1}", name, GameSettings.Filter(FriendChatText.Preview(line.Text))));
            MenuSound.Play(MenuSound.Kind.Open);
        }

        void OnDestroy()
        {
            if (chat != null) chat.Heard -= OnHeard;
            if (hud != null) hud.ChatFriend -= Open;
            if (host != null) Destroy(host);
        }
    }
}
