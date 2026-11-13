using System;
using System.IO;

namespace ChessFight.Network
{
    /// <summary>
    /// Byte arrays of an exact length, made once per length and handed out again (R297): a packet keeps the length its
    /// reader checks, without a new array for every packet. An array is good until the same pool hands out its length
    /// again, so nothing that takes one keeps it - a delay queue copies it first.
    /// </summary>
    public sealed class PacketPool
    {
        readonly byte[][] byLength;

        public PacketPool(int maxLength) { byLength = new byte[maxLength + 1][]; }

        public byte[] Get(int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (length >= byLength.Length) return new byte[length];
            return byLength[length] ?? (byLength[length] = new byte[length]);
        }
    }

    /// <summary>
    /// One packet at a time through a reused stream, writer and reader (R297): the same BinaryWriter and BinaryReader calls
    /// as before, so the same bytes, without a new MemoryStream, writer, reader and ToArray for every packet. A packet
    /// written here comes from Pool (see PacketPool for how long it lasts). One read here is copied in first and the stream
    /// ends where it does, so reading past it still throws EndOfStreamException.
    /// </summary>
    public sealed class PacketStreams
    {
        readonly MemoryStream output, input;
        readonly BinaryWriter writer;
        readonly BinaryReader reader;
        public readonly PacketPool Pool;

        public PacketStreams(int maxBytes)
        {
            output = new MemoryStream(maxBytes);
            input = new MemoryStream(maxBytes);
            writer = new BinaryWriter(output);
            reader = new BinaryReader(input);
            Pool = new PacketPool(maxBytes);
        }

        public BinaryWriter Begin()
        {
            output.Position = 0;
            output.SetLength(0);
            return writer;
        }

        /// <summary>What was written since Begin, in a pooled array of its length.</summary>
        public byte[] Finish()
        {
            writer.Flush();
            int length = (int)output.Length;
            byte[] bytes = Pool.Get(length);
            Buffer.BlockCopy(output.GetBuffer(), 0, bytes, 0, length);
            return bytes;
        }

        public BinaryReader Open(byte[] bytes) => Open(bytes, bytes.Length);

        public BinaryReader Open(byte[] bytes, int length)
        {
            input.Position = 0;
            input.SetLength(length);
            Buffer.BlockCopy(bytes, 0, input.GetBuffer(), 0, length);
            return reader;
        }
    }
}
