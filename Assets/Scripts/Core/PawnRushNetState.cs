using System;

namespace ChessFight.Network
{
    // Pawn Rush online (R92): what the host tells every other PC about the course besides the pawns (those go in the
    // ragdoll snapshots). Sent whole with every snapshot, unreliable: the newest one wins, so a lost one costs nothing
    // (a decided race goes reliably as well).
    //
    // CFP2 (R125): the promotion pads, the race clock, who has reached the crown platform and the winner.
    // CFP3 (R126, mini-games v1.0 §1.2): the host's time (a client applies the state when the pawns it draws reach that
    // moment, so a gate, a bridge or a capstan moves in step with the bodies around it), and for each station its game,
    // its progress, done, and a block of up to BlockBytes the game writes itself (MiniGameBase.WriteNet): planks
    // carried and by whom, squares painted, a sled's place on its path. The mini-game spec budgets 24 bytes a station;
    // the block takes up to 32 so a game never has to squeeze.
    // CFP4 (R130): who is choosing a piece on each station's promotion pad (a seat, 255 nobody), so that player's PC
    // shows the choice and sends the piece back (the lab input's pieceAsk / pieceSeq).
    // CFP5 (R508, the section round as the match): as many stations as the course has (course 01's two plazas: 4; a
    // round's three mini-game sections: 6), and the round's sections' own state - what someone standing on something
    // changed and the shared clock cannot tell (the grand stair hall's broken glass, the garden's tipped lotus pads, the
    // carriage yard's final gate) - as one run of bytes the sections write and read in the round's order
    // (ISectionNetPart). The round itself needs no bytes: every PC draws it from the match's number.
    public sealed class PawnRushNetState
    {
        public const uint Magic = 0x43465035;   // "CFP5"
        public const int MaxStations = 6;
        public const int MaxFinishers = 12;
        public const int BlockBytes = 32;
        public const int MaxPartBytes = 96;
        // magic, match, tick, host time, the two games, the pads (3), the winner, the clock, then the counts: finishers,
        // stations, part bytes
        public const int HeaderBytes = 4 + 8 + 4 + 4 + 1 + 1 + 3 + 1 + 4 + 1 + 1 + 1;   // 33
        // Where the finisher count sits (the tests damage it).
        public const int FinisherCountAt = 30;
        public const byte NoChooser = 255;
        public const int FinisherBytes = 1 + 2;   // the seat (SkillWire.Ref) and the time, 0.1 s
        public const int StationFixedBytes = 1 + 2 + 1 + 1;   // game, progress, flags, block length
        public const int MaxBytes = HeaderBytes + MaxStations + MaxFinishers * FinisherBytes + MaxStations * (StationFixedBytes + BlockBytes) + MaxPartBytes;

        public uint Tick;
        // The host's clock (ms, the snapshots' hostTimeMs) when this state was taken.
        public uint HostTimeMs;
        // Course 01's two drawn games (-1 for a round: its games come with its sections).
        public sbyte First = -1, Second = -1;
        // Course 01's pad pieces (plaza 1, plaza 2; PieceKind) and the spent pads, a bit per station.
        public byte PadFirst, PadSecond, PadsUsed;
        // The stations, in the course's order (course 01: plaza 1 white, plaza 1 black, plaza 2 white, plaza 2 black; a
        // round: each mini-game section's white then black, in the round's order).
        public int StationCount;
        // The seat choosing a piece on each station's pad.
        public readonly byte[] PadChooser = { NoChooser, NoChooser, NoChooser, NoChooser, NoChooser, NoChooser };
        // 0 white, 1 black, -1 nobody yet (PawnRushFinish).
        public sbyte Winner = -1;
        // Seconds since the race started, on the host.
        public float Clock;
        // Everyone who has stood on the crown platform, with the race clock when they first did.
        public int FinisherCount;
        public readonly byte[] FinisherSeat = new byte[MaxFinishers];
        public readonly float[] FinisherTime = new float[MaxFinishers];
        public readonly sbyte[] Game = { -1, -1, -1, -1, -1, -1 };
        public readonly float[] Progress = new float[MaxStations];
        public readonly bool[] Completed = new bool[MaxStations];
        public readonly int[] BlockLength = new int[MaxStations];
        public readonly byte[][] Block = { new byte[BlockBytes], new byte[BlockBytes], new byte[BlockBytes], new byte[BlockBytes], new byte[BlockBytes], new byte[BlockBytes] };
        // The sections' own state (ISectionNetPart), in the round's order.
        public int PartsLength;
        public readonly byte[] Parts = new byte[MaxPartBytes];

        int Stations => Math.Max(0, Math.Min(StationCount, MaxStations));

        public int Bytes
        {
            get
            {
                int n = HeaderBytes + Stations + Math.Max(0, Math.Min(FinisherCount, MaxFinishers)) * FinisherBytes;
                for (int s = 0; s < Stations; s++) n += StationFixedBytes + Math.Max(0, Math.Min(BlockLength[s], BlockBytes));
                return n + Math.Max(0, Math.Min(PartsLength, MaxPartBytes));
            }
        }

        // R297: the written state's array, reused for the next one of its length (every byte is written again).
        static readonly PacketPool pool = new PacketPool(MaxBytes);

        public byte[] Write(ulong match)
        {
            int finishers = Math.Max(0, Math.Min(FinisherCount, MaxFinishers)), stations = Stations;
            int parts = Math.Max(0, Math.Min(PartsLength, MaxPartBytes));
            var b = pool.Get(Bytes);
            int i = 0;
            PutU32(b, ref i, Magic);
            PutU64(b, ref i, match);
            PutU32(b, ref i, Tick);
            PutU32(b, ref i, HostTimeMs);
            b[i++] = (byte)First;
            b[i++] = (byte)Second;
            b[i++] = PadFirst;
            b[i++] = PadSecond;
            b[i++] = PadsUsed;
            b[i++] = (byte)Winner;
            PutU32(b, ref i, (uint)BitConverter.SingleToInt32Bits(Clock));
            b[i++] = (byte)finishers;
            b[i++] = (byte)stations;
            b[i++] = (byte)parts;
            for (int s = 0; s < stations; s++) b[i++] = PadChooser[s];
            for (int f = 0; f < finishers; f++)
            {
                b[i++] = FinisherSeat[f];
                PutU16(b, ref i, (ushort)Math.Min(ushort.MaxValue, (int)Math.Round(Math.Max(0f, FinisherTime[f]) * 10f)));
            }
            for (int s = 0; s < stations; s++)
            {
                int length = Math.Max(0, Math.Min(BlockLength[s], BlockBytes));
                b[i++] = (byte)Game[s];
                PutU16(b, ref i, (ushort)Math.Round(Math.Max(0f, Math.Min(1f, Progress[s])) * 65535f));
                b[i++] = (byte)(Completed[s] ? 1 : 0);
                b[i++] = (byte)length;
                Array.Copy(Block[s], 0, b, i, length);
                i += length;
            }
            Array.Copy(Parts, 0, b, i, parts);
            return b;
        }

        // False for anything that is not a course state of this match (another channel's packet,
        // another match, a short, long or damaged one).
        public static bool Read(byte[] b, ulong match, PawnRushNetState into)
        {
            if (b == null || b.Length < HeaderBytes || b.Length > MaxBytes) return false;
            int i = 0;
            if (U32(b, ref i) != Magic || U64(b, ref i) != match) return false;
            uint tick = U32(b, ref i), hostTime = U32(b, ref i);
            sbyte first = (sbyte)b[i++], second = (sbyte)b[i++];
            byte padFirst = b[i++], padSecond = b[i++], padsUsed = (byte)(b[i++] & 0x3f);
            sbyte winner = (sbyte)b[i++];
            float clock = BitConverter.Int32BitsToSingle((int)U32(b, ref i));
            int finishers = b[i++], stations = b[i++], parts = b[i++];
            if (finishers > MaxFinishers || stations > MaxStations || parts > MaxPartBytes) return false;
            int choosersAt = i;
            i += stations;
            if (i + finishers * FinisherBytes > b.Length) return false;
            int at = i + finishers * FinisherBytes;
            // Walk the stations once to check the lengths add up before anything is written.
            for (int s = 0; s < stations; s++)
            {
                if (at + StationFixedBytes > b.Length) return false;
                int length = b[at + 4];
                if (length > BlockBytes) return false;
                at += StationFixedBytes + length;
            }
            if (at + parts != b.Length) return false;

            into.Tick = tick;
            into.HostTimeMs = hostTime;
            into.First = first;
            into.Second = second;
            into.PadFirst = padFirst;
            into.PadSecond = padSecond;
            into.PadsUsed = padsUsed;
            into.StationCount = stations;
            for (int s = 0; s < MaxStations; s++) into.PadChooser[s] = s < stations ? b[choosersAt + s] : NoChooser;
            into.Winner = winner == 0 || winner == 1 ? winner : (sbyte)-1;
            into.Clock = float.IsNaN(clock) || float.IsInfinity(clock) ? 0f : Math.Max(0f, clock);
            into.FinisherCount = finishers;
            for (int f = 0; f < finishers; f++)
            {
                into.FinisherSeat[f] = b[i++];
                into.FinisherTime[f] = U16(b, ref i) / 10f;
            }
            for (int s = 0; s < stations; s++)
            {
                into.Game[s] = (sbyte)b[i++];
                into.Progress[s] = U16(b, ref i) / 65535f;
                into.Completed[s] = (b[i++] & 1) != 0;
                int length = b[i++];
                into.BlockLength[s] = length;
                Array.Copy(b, i, into.Block[s], 0, length);
                i += length;
            }
            into.PartsLength = parts;
            Array.Copy(b, i, into.Parts, 0, parts);
            return true;
        }

        public void CopyFrom(PawnRushNetState o)
        {
            Tick = o.Tick; HostTimeMs = o.HostTimeMs; First = o.First; Second = o.Second;
            PadFirst = o.PadFirst; PadSecond = o.PadSecond; PadsUsed = o.PadsUsed; Winner = o.Winner; Clock = o.Clock;
            StationCount = o.StationCount;
            Array.Copy(o.PadChooser, PadChooser, MaxStations);
            FinisherCount = o.FinisherCount;
            Array.Copy(o.FinisherSeat, FinisherSeat, MaxFinishers);
            Array.Copy(o.FinisherTime, FinisherTime, MaxFinishers);
            for (int s = 0; s < MaxStations; s++)
            {
                Game[s] = o.Game[s]; Progress[s] = o.Progress[s]; Completed[s] = o.Completed[s]; BlockLength[s] = o.BlockLength[s];
                Array.Copy(o.Block[s], Block[s], BlockBytes);
            }
            PartsLength = o.PartsLength;
            Array.Copy(o.Parts, Parts, MaxPartBytes);
        }

        static void PutU16(byte[] b, ref int i, ushort v) { b[i++] = (byte)v; b[i++] = (byte)(v >> 8); }
        static void PutU32(byte[] b, ref int i, uint v) { for (int k = 0; k < 4; k++) b[i++] = (byte)(v >> (8 * k)); }
        static void PutU64(byte[] b, ref int i, ulong v) { for (int k = 0; k < 8; k++) b[i++] = (byte)(v >> (8 * k)); }
        static ushort U16(byte[] b, ref int i) { ushort v = (ushort)(b[i] | b[i + 1] << 8); i += 2; return v; }
        static uint U32(byte[] b, ref int i) { uint v = 0; for (int k = 0; k < 4; k++) v |= (uint)b[i++] << (8 * k); return v; }
        static ulong U64(byte[] b, ref int i) { ulong v = 0; for (int k = 0; k < 8; k++) v |= (ulong)b[i++] << (8 * k); return v; }
    }

    // A mini-game's block, written and read a field at a time (little endian, the same both ways).
    public struct NetBlock
    {
        public byte[] Buffer;
        public int Start, At, End;

        public NetBlock(byte[] buffer, int start, int length)
        {
            Buffer = buffer;
            Start = At = start;
            End = start + length;
        }

        public int Written => At - Start;
        public bool Left(int bytes) => At + bytes <= End;

        public void U8(int v) { if (Left(1)) Buffer[At++] = (byte)v; }
        public void U16(int v) { if (Left(2)) { Buffer[At++] = (byte)v; Buffer[At++] = (byte)(v >> 8); } }
        public void U32(uint v) { if (Left(4)) for (int k = 0; k < 4; k++) Buffer[At++] = (byte)(v >> (8 * k)); }
        public void U64(ulong v) { if (Left(8)) for (int k = 0; k < 8; k++) Buffer[At++] = (byte)(v >> (8 * k)); }
        // A value in 0..range as 16 bits.
        public void Unit16(float value, float range) => U16((int)Math.Round(Math.Max(0f, Math.Min(1f, value / range)) * 65535f));

        public int ReadU8() => Left(1) ? Buffer[At++] : 0;
        public int ReadU16() { if (!Left(2)) return 0; int v = Buffer[At] | Buffer[At + 1] << 8; At += 2; return v; }
        public uint ReadU32() { if (!Left(4)) return 0; uint v = 0; for (int k = 0; k < 4; k++) v |= (uint)Buffer[At++] << (8 * k); return v; }
        public ulong ReadU64() { if (!Left(8)) return 0; ulong v = 0; for (int k = 0; k < 8; k++) v |= (ulong)Buffer[At++] << (8 * k); return v; }
        public float ReadUnit16(float range) => ReadU16() / 65535f * range;
    }
}
