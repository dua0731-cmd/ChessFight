using System;
namespace ChessFight.ProtectKing
{
    // Zero is an AI seat. Steam IDs are never accepted from an input packet.
    public sealed class PlayerRoster
    {
        public const int Capacity = 12;
        readonly ulong[] owners = new ulong[Capacity];
        public ulong Owner(int slot) => owners[slot];
        public int Find(ulong peer) => peer == 0 ? -1 : Array.IndexOf(owners, peer);
        public int Count { get { int n=0; foreach(var id in owners) if(id!=0)n++; return n; } }
        public int Assign(ulong peer)
        {
            if(peer==0)return -1;
            int existing=Find(peer); if(existing>=0)return existing;
            // Alternate teams, filling each team's King first.
            for(int piece=0;piece<6;piece++)
                for(int team=0;team<2;team++) {
                    int slot=team*6+piece;
                    if(owners[slot]==0) { owners[slot]=peer; return slot; }
                }
            return -1;
        }
        public int Release(ulong peer) { int slot=Find(peer); if(slot>=0)owners[slot]=0; return slot; }
        public void Clear() => Array.Clear(owners,0,owners.Length);
        public void Apply(ulong[] value) { if(value.Length!=Capacity)throw new ArgumentException(); Array.Copy(value,owners,Capacity); }
    }
}