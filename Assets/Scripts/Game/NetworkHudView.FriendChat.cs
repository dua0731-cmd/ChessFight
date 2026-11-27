using System;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // R862 (Docs/Architecture/UI.md "친구 1:1 대화"): the friends card's side of the friend 1:1 chat - a "대화" button on
    // every friend (party members too) with the count of that friend's unread lines on it, and the same total on the
    // top bar's "친구" button. The window itself is FriendChatWindow, on a panel of its own; the lobby wires the two.
    public sealed partial class NetworkHudView
    {
        // A friend's "대화" was pressed: open the 1:1 window on them.
        public event Action<ulong> ChatFriend;
        // How many of a friend's lines wait unread (the lobby reads the book); null shows none.
        public Func<ulong, int> FriendUnread;
        // The friend whose window is open now (0 for none), marked on their button.
        public ulong ChattingWith;
        // Every friend's unread lines together, for the "친구" button.
        public int FriendUnreadTotal;
        // The 1:1 chat can be used now (Steam on, not matching): the friends card then opens even when no invite can go.
        public bool FriendChatAvailable;

        bool friendChatStyled;

        // The friends list again, for the badges (the lobby calls it when the book changes while the card is open).
        public void RedrawFriends() { if (FriendsOpen) DrawFriends(); }

        void AddTalkButton(VisualElement row, ulong id)
        {
            if (!friendChatStyled && root != null)
            {
                friendChatStyled = true;
                var sheet = Resources.Load<StyleSheet>("FriendChat");
                if (sheet != null) root.styleSheets.Add(sheet);
            }
            var button = new Button { text = Loc.T("대화"), focusable = false };
            button.AddToClassList("fc-talk");
            button.EnableInClassList("fc-talk-on", id != 0 && id == ChattingWith);
            int unread = FriendUnread != null ? FriendUnread(id) : 0;
            if (unread > 0)
            {
                var badge = new Label(unread > 99 ? "99+" : unread.ToString()) { pickingMode = PickingMode.Ignore };
                badge.AddToClassList("fc-badge");
                button.Add(badge);
            }
            Action talk = () => { MenuSound.Play(MenuSound.Kind.Open); ChatFriend?.Invoke(id); };
            button.clicked += talk;
            actions[button] = talk;
            friendButtons.Add(button);
            row.Add(button);
        }

        // The "친구" button's words: the friends online, and a red count of unread friend lines after them.
        string FriendsButtonText(bool online, int friendsOnline)
        {
            if (!online) return Loc.T("친구");
            string text = Loc.T("친구") + $" <color=#E2B866><b>{friendsOnline}</b></color>";
            if (FriendUnreadTotal > 0) text += $" <color=#FF4D62><b>●{(FriendUnreadTotal > 99 ? "99+" : FriendUnreadTotal.ToString())}</b></color>";
            return text;
        }
    }
}
