using System;

namespace ChessFight.Network
{
    // Pawn Rush course 01 online (R92): what the host tells every other PC about the course besides the
    // pawns (those go in the ragdoll snapshots). The round's two mini-games, and for each of the four
    // mission stations (plaza 1 white, plaza 1 black, plaza 2 white, plaza 2 black) its progress, whether
    // it is done, and what the game shows beyond that (MiniGameBase.StateBits). Sent whole a few times
    // a second, unreliable: the newest one wins, so a lost one costs nothing.
    public sealed class PawnRushNetState
    {
        public const uint Magic = 0x43465031;   // "CFP1"
        public const int Stations = 4;
        public const int Bytes = 4 + 8 + 4 + 1 + 1 + Stations * (4 + 1 + 8);   // 70

        public uint Tick;
        public sbyte First = -1, Second = -1;
        public readonly float[] Progress = new float[Stations];
        public readonly bool[] Completed = new bool[Stations];
        public readonly ulong[] Bits = new ulong[Stations];

        public byte[] Write(ulong match)
        {
            var b = new byte[Bytes];
            int i = 0;
            PutU32(b, ref i, Magic);
            PutU64(b, ref i, match);
            PutU32(b, ref i, Tick);
            b[i++] = (byte)First;
            b[i++] = (byte)Second;
            for (int s = 0; s < Stations; s++)
            {
                PutU32(b, ref i, (uint)BitConverter.SingleToInt32Bits(Progress[s]));
                b[i++] = (byte)(Completed[s] ? 1 : 0);
                PutU64(b, ref i, Bits[s]);
            }
            return b;
        }

        // False for anything that is not a course state of this match (another channel's packet,
        // another match, a short or damaged one).
        public static bool Read(byte[] b, ulong match, PawnRushNetState into)
        {
            if (b == null || b.Length != Bytes) return false;
            int i = 0;
            if (U32(b, ref i) != Magic || U64(b, ref i) != match) return false;
            into.Tick = U32(b, ref i);
            into.First = (sbyte)b[i++];
            into.Second = (sbyte)b[i++];
            for (int s = 0; s < Stations; s++)
            {
                float p = BitConverter.Int32BitsToSingle((int)U32(b, ref i));
                into.Progress[s] = float.IsNaN(p) ? 0f : Math.Max(0f, Math.Min(1f, p));
                into.Completed[s] = b[i++] != 0;
                into.Bits[s] = U64(b, ref i);
            }
            return true;
        }

        static void PutU32(byte[] b, ref int i, uint v) { for (int k = 0; k < 4; k++) b[i++] = (byte)(v >> (8 * k)); }
        static void PutU64(byte[] b, ref int i, ulong v) { for (int k = 0; k < 8; k++) b[i++] = (byte)(v >> (8 * k)); }
        static uint U32(byte[] b, ref int i) { uint v = 0; for (int k = 0; k < 4; k++) v |= (uint)b[i++] << (8 * k); return v; }
        static ulong U64(byte[] b, ref int i) { ulong v = 0; for (int k = 0; k < 8; k++) v |= (ulong)b[i++] << (8 * k); return v; }
    }
}
