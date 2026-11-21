using System.Collections.Generic;

namespace ChessFight.Network
{
    // Where each player of a team is called to when the hall's kings summon their teams onto the chessboard (the throne
    // contest, JY 2026-11-22: "실제 체스랑 같은 기물 위치로 소환됐으면 좋겠고"): its own starting square, as on a board at the
    // start of a game. Files 1-8 are a-h; rank 1 is the team's back rank and 2 its pawns' (black turns them into 8 and 7).
    // A piece takes its own squares first - rooks a and h, knights b and g, bishops c and f, the queen d, pawns d e c f b g
    // a h - and a piece whose squares are taken (a second queen) or that has none (a king: e1 is the hall's king's own)
    // takes the first free square of the back rank (d c f b g a h), then of the pawns' rank, then further up the board.
    // The same answer on every PC for the same list.
    public static class ChessSummons
    {
        static readonly int[] PawnFiles = { 4, 5, 3, 6, 2, 7, 1, 8 };
        static readonly int[] BackFiles = { 4, 3, 6, 2, 7, 1, 8 };

        static int[] Own(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.Rook: return new[] { 1, 8 };
                case PieceKind.Knight: return new[] { 2, 7 };
                case PieceKind.Bishop: return new[] { 3, 6 };
                case PieceKind.Queen: return new[] { 4 };
                default: return new int[0];
            }
        }

        // file[i], rank[i] for kinds[i] (rank in the team's own terms: 1 back, 2 pawns, 3...).
        public static void Assign(IReadOnlyList<PieceKind> kinds, int[] file, int[] rank)
        {
            var taken = new HashSet<int>();   // rank * 10 + file
            taken.Add(15);                    // e1: the hall's king
            int n = kinds.Count;
            var done = new bool[n];
            // Their own squares first, in the order given.
            for (int i = 0; i < n; i++)
            {
                if (kinds[i] == PieceKind.Pawn)
                {
                    foreach (int f in PawnFiles)
                        if (taken.Add(20 + f)) { file[i] = f; rank[i] = 2; done[i] = true; break; }
                }
                else
                    foreach (int f in Own(kinds[i]))
                        if (taken.Add(10 + f)) { file[i] = f; rank[i] = 1; done[i] = true; break; }
            }
            // Then whoever is left: the back rank, the pawns' rank, further up.
            for (int i = 0; i < n; i++)
            {
                if (done[i]) continue;
                for (int r = 1; r <= 4 && !done[i]; r++)
                    foreach (int f in r == 1 ? BackFiles : PawnFiles)
                        if (taken.Add(r * 10 + f)) { file[i] = f; rank[i] = r; done[i] = true; break; }
            }
        }
    }
}
