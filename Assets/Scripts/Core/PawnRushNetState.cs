using System;

namespace ChessFight.Network
{
    // Pawn Rush course 01 online (R92): what the host tells every other PC about the course besides the
    // pawns (those go in the ragdoll snapshots). The round's two mini-games, and for each of the four
    // mission stations (plaza 1 white, plaza 1 black, plaza 2 white, plaza 2 black) its progress, whether
    // it is done, and what the game shows beyond that (MiniGameBase.StateBits). Sent whole a few times
    // a second, unreliable: the newest one wins, so a lost one costs nothing.
    //
    // CFP2 (R125): also the promotion pads (the round's two pieces and which pads are spent), the race clock,
    // who has reached the crown platform and when, and the winner once a team has a majority on it.
    public sealed class PawnRushNetState
    {
        public const uint Magic = 0x43465032;   // "CFP2"
        public const int Stations = 4;
        public const int MaxFinishers = 12;
        // Fixed part: 70 bytes as CFP1, then the pads (3), the winner (1), the clock (4) and the finisher count (1).
        public const int FixedBytes = 4 + 8 + 4 + 1 + 1 + Stations * (4 + 1 + 8) + 3 + 1 + 4 + 1;   // 79
        public const int FinisherBytes = 1 + 2;   // the seat (SkillWire.Ref) and the time, 0.1 s

        public uint Tick;
        public sbyte First = -1, Second = -1;
        public readonly float[] Progress = new float[Stations];
        public readonly bool[] Completed = new bool[Stations];
        public readonly ulong[] Bits = new ulong[Stations];
        // The round's pad pieces (plaza 1, plaza 2; PieceKind) and the spent pads, a bit per station.
        public byte PadFirst, PadSecond, PadsUsed;
        // 0 white, 1 black, -1 nobody yet (PawnRushFinish).
        public sbyte Winner = -1;
        // Seconds since the race started, on the host.
        public float Clock;
        // Everyone who has stood on the crown platform, with the race clock when they first did.
        public int FinisherCount;
        public readonly byte[] FinisherSeat = new byte[MaxFinishers];
        public readonly float[] FinisherTime = new float[MaxFinishers];

        public int Bytes => FixedBytes + Math.Min(FinisherCount, MaxFinishers) * FinisherBytes;

        public byte[] Write(ulong match)
        {
            int finishers = Math.Max(0, Math.Min(FinisherCount, MaxFinishers));
            var b = new byte[FixedBytes + finishers * FinisherBytes];
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
            b[i++] = PadFirst;
            b[i++] = PadSecond;
            b[i++] = PadsUsed;
            b[i++] = (byte)Winner;
            PutU32(b, ref i, (uint)BitConverter.SingleToInt32Bits(Clock));
            b[i++] = (byte)finishers;
            for (int f = 0; f < finishers; f++)
            {
                b[i++] = FinisherSeat[f];
                int tenths = (int)Math.Round(Math.Max(0f, FinisherTime[f]) * 10f);
                ushort t = (ushort)Math.Min(ushort.MaxValue, tenths);
                b[i++] = (byte)t;
                b[i++] = (byte)(t >> 8);
            }
            return b;
        }

        // False for anything that is not a course state of this match (another channel's packet,
        // another match, a short or damaged one).
        public static bool Read(byte[] b, ulong match, PawnRushNetState into)
        {
            if (b == null || b.Length < FixedBytes || b.Length > FixedBytes + MaxFinishers * FinisherBytes) return false;
            if (b[FixedBytes - 1] > MaxFinishers || b.Length != FixedBytes + b[FixedBytes - 1] * FinisherBytes) return false;
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
            into.PadFirst = b[i++];
            into.PadSecond = b[i++];
            into.PadsUsed = (byte)(b[i++] & 0x0f);
            sbyte winner = (sbyte)b[i++];
            into.Winner = winner == 0 || winner == 1 ? winner : (sbyte)-1;
            float clock = BitConverter.Int32BitsToSingle((int)U32(b, ref i));
            into.Clock = float.IsNaN(clock) || float.IsInfinity(clock) ? 0f : Math.Max(0f, clock);
            into.FinisherCount = b[i++];
            for (int f = 0; f < into.FinisherCount; f++)
            {
                into.FinisherSeat[f] = b[i++];
                into.FinisherTime[f] = (b[i] | b[i + 1] << 8) / 10f;
                i += 2;
            }
            return true;
        }

        static void PutU32(byte[] b, ref int i, uint v) { for (int k = 0; k < 4; k++) b[i++] = (byte)(v >> (8 * k)); }
        static void PutU64(byte[] b, ref int i, ulong v) { for (int k = 0; k < 8; k++) b[i++] = (byte)(v >> (8 * k)); }
        static uint U32(byte[] b, ref int i) { uint v = 0; for (int k = 0; k < 4; k++) v |= (uint)b[i++] << (8 * k); return v; }
        static ulong U64(byte[] b, ref int i) { ulong v = 0; for (int k = 0; k < 8; k++) v |= (ulong)b[i++] << (8 * k); return v; }
    }
}
