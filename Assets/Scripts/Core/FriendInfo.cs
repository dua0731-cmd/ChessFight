namespace ChessFight.Network
{
    // How useful a friend is to invite right now, most useful last so it sorts
    // descending into a sensible order.
    public enum FriendPresence { Offline, Away, Online, InGame }

    // A plain snapshot of one Steam friend. It lives in Core, with no Steam types,
    // so the presentation assembly can show a friend list without referencing the
    // Steam adapter.
    public struct FriendInfo
    {
        public ulong Id;
        public string Name;
        public FriendPresence Presence;
    }
}
