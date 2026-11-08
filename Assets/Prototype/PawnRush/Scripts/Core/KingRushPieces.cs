namespace ChessFight.Network
{
    // Mode-local identity only. Never apply the Queen Hill physical stat table.
    public enum KingRushPiece { Pawn, Rook, Bishop, Knight, King, Queen }
    public enum KingRushSection { Red1, Blue1, Red2, Blue2, Red3, Final }

    public static class KingRushPieces
    {
        public static bool CanPromote(KingRushPiece from, KingRushPiece to) =>
            from == KingRushPiece.Pawn && (to == KingRushPiece.Rook || to == KingRushPiece.Bishop ||
                to == KingRushPiece.Knight || to == KingRushPiece.Queen);
        public static bool IsBlue(KingRushSection section) => section == KingRushSection.Blue1 ||
            section == KingRushSection.Blue2 || section == KingRushSection.Final;
        public static string Name(KingRushPiece piece)
        {
            switch (piece)
            {
                case KingRushPiece.Rook: return "룩";
                case KingRushPiece.Bishop: return "비숍";
                case KingRushPiece.Knight: return "나이트";
                case KingRushPiece.King: return "킹";
                case KingRushPiece.Queen: return "퀸";
                default: return "폰";
            }
        }
    }
}
