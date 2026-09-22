using System;

namespace ChessFight.Network
{
    // Bots are roster entries without a Steam account. They let a small team test
    // 12-player capacity, but they are never peers: nothing is ever sent to them
    // and nothing is ever accepted from them.
    //
    // A SteamID64 for an individual account always begins with universe/type bits
    // 0x0110_0001, so the top nibble is 0x0. Tagging bots with 0xB in the top
    // nibble keeps them permanently outside the Steam identifier space.
    //
    // Layout: [4 bits tag 0xB][44 bits owning leader][12 bits index in that party].
    // Embedding the owner means two different parties in the same match cannot
    // generate colliding bot IDs, and the host can verify that a declared bot
    // really belongs to the party leader who asked for it.
    public static class BotIdentity
    {
        public const ulong Tag = 0xB000000000000000UL;
        const ulong TagMask = 0xF000000000000000UL;
        const ulong OwnerMask = 0x00000FFFFFFFFFFFUL;
        const ulong IndexMask = 0x0000000000000FFFUL;

        // A party never needs more bots than the team it is reserved into.
        public const int MaxPerParty = TeamReservations.TeamSize;

        public static bool IsBot(ulong id) => (id & TagMask) == Tag;
        public static ulong Id(ulong owner, int index)
        {
            if (owner == 0) throw new ArgumentOutOfRangeException(nameof(owner));
            if (index < 0 || index > (int)IndexMask) throw new ArgumentOutOfRangeException(nameof(index));
            return Tag | ((owner & OwnerMask) << 12) | (uint)index;
        }
        public static int Index(ulong id) => IsBot(id) ? (int)(id & IndexMask) : -1;
        public static bool OwnedBy(ulong id, ulong owner) => IsBot(id) && ((id >> 12) & OwnerMask) == (owner & OwnerMask);

        public static ulong[] Fill(ulong owner, int count, int offset = 0)
        {
            if (count < 0) count = 0;
            var ids = new ulong[count];
            for (int i = 0; i < count; i++) ids[i] = Id(owner, offset + i);
            return ids;
        }
    }
}
