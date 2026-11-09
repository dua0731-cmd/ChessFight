using System;
namespace ChessFight.Network
{
    // One clue per stair row. Distractors exclude all clue letters, so only one key is safe per row.
    public sealed class TypewriterMemory
    {
        public readonly string Sequence, Keys;
        public readonly int Columns;
        TypewriterMemory(string sequence, string keys, int columns) { Sequence = sequence; Keys = keys; Columns = columns; }
        public bool Safe(int index) => index >= 0 && index < Keys.Length && Keys[index] == Sequence[index / Columns];
        public static TypewriterMemory Generate(Random random, int rows = 5, int columns = 4)
        {
            if (random == null || rows < 1 || rows > 12 || columns < 2 || columns > 8) throw new ArgumentException("Invalid typewriter layout.");
            char[] alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
            for (int i = alphabet.Length - 1; i > 0; i--) { int j = random.Next(i + 1); char c = alphabet[i]; alphabet[i] = alphabet[j]; alphabet[j] = c; }
            string sequence = new string(alphabet, 0, rows);
            char[] keys = new char[rows * columns]; int previous = random.Next(columns);
            for (int row = 0; row < rows; row++)
            {
                int safe = row == 0 ? previous : random.Next(Math.Max(0, previous - 1), Math.Min(columns - 1, previous + 1) + 1);
                // Shuffle a fresh distractor pool, retaining unique keys within each row.
                char[] pool = new char[26 - rows]; Array.Copy(alphabet, rows, pool, 0, pool.Length);
                for (int i = pool.Length - 1; i > 0; i--) { int j = random.Next(i + 1); char c = pool[i]; pool[i] = pool[j]; pool[j] = c; }
                int next = 0;
                for (int col = 0; col < columns; col++) keys[row * columns + col] = col == safe ? sequence[row] : pool[next++];
                previous = safe;
            }
            return new TypewriterMemory(sequence, new string(keys), columns);
        }
    }
}
