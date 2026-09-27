namespace ChessFight.Network
{
    // The Queen of the Hill pieces (Docs/GameModes/QueenOfTheHill/DESIGN.md §6.2)
    // and what each one is made of, as plain numbers with no Unity in them, so the
    // table and the promotion rule are tested on their own (M11). Every
    // character keeps the pawn's ragdoll - the same size and the same physics -
    // and a piece only changes these multipliers and its look.
    public enum PieceKind : byte
    {
        Pawn = 0,
        Rook = 1,
        Bishop = 2,
        Knight = 3,
        King = 4,
        Queen = 5,
    }

    public readonly struct PieceStats
    {
        // Pushes, hits, tackles and bumps move it 1 / Weight as far: the king (3)
        // is hard to shift, the knight (0.8) flies.
        public readonly float Weight;
        // Run and sprint speed, times.
        public readonly float Move;
        // Climbing speed on walls and ropes, times.
        public readonly float Climb;
        // Whether it may sprint at all.
        public readonly bool Sprint;
        // Squash and stagger do not take (the king's passive).
        public readonly bool StatusImmune;

        public PieceStats(float weight, float move, float climb, bool sprint, bool statusImmune)
        {
            Weight = weight;
            Move = move;
            Climb = climb;
            Sprint = sprint;
            StatusImmune = statusImmune;
        }
    }

    public static class ChessPieces
    {
        public const int Count = 6;

        static readonly PieceStats PawnStats = new PieceStats(1f, 1f, 1f, true, false);

        public static bool IsValid(int kind) => kind >= 0 && kind < Count;

        // DESIGN §6.2, the starting values.
        public static PieceStats Stats(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.Rook: return new PieceStats(1.5f, 1f, 0.7f, true, false);
                case PieceKind.Bishop: return new PieceStats(1f, 1f, 1f, true, false);
                case PieceKind.Knight: return new PieceStats(0.8f, 1f, 1f, true, false);
                case PieceKind.King: return new PieceStats(3f, 0.8f, 1f, false, true);
                case PieceKind.Queen: return new PieceStats(2f, 1f, 1f, true, false);
                default: return PawnStats;
            }
        }

        public static string Name(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.Rook: return "룩";
                case PieceKind.Bishop: return "비숍";
                case PieceKind.Knight: return "나이트";
                case PieceKind.King: return "킹";
                case PieceKind.Queen: return "퀸";
                default: return "폰";
            }
        }

        // Promotion at the gate (DESIGN §6.2): only a pawn is promoted, to a rook,
        // a bishop, a knight or the king, and a team has one king at most. The queen
        // is never picked; the second phase crowns her.
        public static bool CanPromote(PieceKind from, PieceKind to, int kingsInTeam)
        {
            if (from != PieceKind.Pawn) return false;
            switch (to)
            {
                case PieceKind.Rook:
                case PieceKind.Bishop:
                case PieceKind.Knight:
                    return true;
                case PieceKind.King:
                    return kingsInTeam <= 0;
                default:
                    return false;
            }
        }
    }
}
