using System;
namespace ChessFight.Network
{
    // Alternating rows share one numbered clue each; positions are centered and staggered.
    public sealed class TypewriterMemory
    {
        public const int RowCount = 5, KeyCount = 23;
        public readonly string Sequence, Keys;
        TypewriterMemory(string sequence, string keys) { Sequence = sequence; Keys = keys; }
        public static int RowLength(int row) => row % 2 == 0 ? 5 : 4;
        public static int RowStart(int row) => row / 2 * 9 + (row % 2 == 0 ? 0 : 5);
        public static int KeyRow(int index)
        {
            if (index < 0 || index >= KeyCount) return -1;
            for (int row = 0; row < RowCount; row++)
                if (index < RowStart(row) + RowLength(row)) return row;
            return -1;
        }
        public bool Safe(int index) => KeyRow(index) >= 0 && Keys[index] == Sequence[KeyRow(index)];
        public static TypewriterMemory Generate(Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            char[] alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
            for (int i = alphabet.Length - 1; i > 0; i--) { int j = random.Next(i + 1); char c = alphabet[i]; alphabet[i] = alphabet[j]; alphabet[j] = c; }
            string sequence = new string(alphabet, 0, RowCount);
            char[] keys = new char[KeyCount]; float previousX = 0;
            for (int row = 0; row < RowCount; row++)
            {
                int columns = RowLength(row), start = RowStart(row);
                int[] candidates = new int[columns]; int count = 0;
                for (int col = 0; col < columns; col++)
                    if (row == 0 || Math.Abs(col - (columns - 1) * .5f - previousX) <= .5f) candidates[count++] = col;
                int safe = candidates[random.Next(count)];
                char[] pool = new char[26 - RowCount]; Array.Copy(alphabet, RowCount, pool, 0, pool.Length);
                for (int i = pool.Length - 1; i > 0; i--) { int j = random.Next(i + 1); char c = pool[i]; pool[i] = pool[j]; pool[j] = c; }
                int next = 0;
                for (int col = 0; col < columns; col++) keys[start + col] = col == safe ? sequence[row] : pool[next++];
                previousX = safe - (columns - 1) * .5f;
            }
            return new TypewriterMemory(sequence, new string(keys));
        }
    }
}
